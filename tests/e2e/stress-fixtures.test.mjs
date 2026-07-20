import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

import { compileContent } from '../../scripts/content/compile-content.mjs';
import { CapabilityHost } from '../../src/runtime/capabilities/capability-host.mjs';
import { encodeGameSnapshotV2, restoreGameSnapshotV2 } from '../../src/runtime/persistence/game-snapshot-v2.mjs';
import { RuntimeKernel } from '../../src/runtime/kernel/runtime-kernel.mjs';
import { createQuestRun, reduceQuestLifecycle } from '../../src/runtime/quests/quest-reducer.mjs';
import { CapabilityRegistry } from '../../src/runtime/registries/runtime-registries.mjs';
import { DeterministicScheduler } from '../../src/runtime/world/deterministic-scheduler.mjs';
import { LogicalClock } from '../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../src/runtime/world/seeded-rng.mjs';

const CAMPAIGN = 'tests/fixtures/content/stress/campaign.json';
const MODULE = 'tests/fixtures/content/stress/module.json';
const PROPOSALS = Object.freeze([
  'spatial-placement', 'spatial-audio-probe', 'crafting', 'scheduler-window',
  'role-binding', 'evidence-compare', 'timed-choice', 'custody-transfer',
  'audio-workbench', 'environment-sim', 'stealth-space', 'content-instantiator',
]);
const CONTRACTS_URL = new URL('../fixtures/content/stress/contracts.json', import.meta.url);
const CONTRACT_SCHEMA_URL = new URL('../fixtures/content/stress/contracts/stress-contracts.schema.json', import.meta.url);

async function compileStressPack() {
  const result = await compileContent({ campaignPath: CAMPAIGN, moduleManifestPaths: [MODULE] });
  assert.equal(result.ok, true, JSON.stringify(result.diagnostics));
  return result.pack;
}

async function loadStressContracts() {
  const [contractSource, schemaSource] = await Promise.all([
    readFile(CONTRACTS_URL, 'utf8'),
    readFile(CONTRACT_SCHEMA_URL, 'utf8'),
  ]);
  const contracts = JSON.parse(contractSource).contracts;
  const schema = JSON.parse(schemaSource);
  assert.equal(contracts.length, PROPOSALS.length);
  return Object.freeze({
    byProtocol: new Map(contracts.map((contract) => [contract.protocolId, Object.freeze(contract)])),
    byConfigRef: new Map(contracts.map((contract) => [contract.configRef, Object.freeze(contract)])),
    schema,
  });
}

function schemaAtReference(schema, reference) {
  const [, fragment = ''] = reference.split('#');
  assert.match(reference, /^contracts\/stress-contracts\.schema\.json#\/\$defs\/[a-z0-9-]+-(?:config|event|outcome)$/);
  return fragment.replace(/^\//, '').split('/').reduce((node, key) => node?.[key], schema);
}

function validatesFixtureSchema(schema, value) {
  if (!schema || typeof schema !== 'object' || Array.isArray(schema)) return false;
  if (Object.hasOwn(schema, 'const') && JSON.stringify(schema.const) !== JSON.stringify(value)) return false;
  if (Array.isArray(schema.enum) && !schema.enum.some((candidate) => JSON.stringify(candidate) === JSON.stringify(value))) return false;
  if (schema.type === 'object') {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
    if ((schema.required ?? []).some((key) => !Object.hasOwn(value, key))) return false;
    if (schema.additionalProperties === false && Object.keys(value).some((key) => !Object.hasOwn(schema.properties ?? {}, key))) return false;
    return Object.entries(schema.properties ?? {}).every(([key, propertySchema]) => (
      !Object.hasOwn(value, key) || validatesFixtureSchema(propertySchema, value[key])
    ));
  }
  return schema.type === undefined || schema.type === typeof value;
}

function contractFor(contracts, manifest) {
  const contract = contracts.byProtocol.get(manifest.protocolId);
  assert.ok(contract, `missing fixture contract for ${manifest.protocolId}`);
  assert.equal(contract.protocolId, manifest.protocolId);
  return contract;
}

function capabilityDescriptors(pack, contracts, trace) {
  return pack.registries.capabilities.map((manifest) => Object.freeze({
    protocolId: manifest.protocolId,
    exactVersion: manifest.exactVersion,
    stateSchemaVersion: manifest.stateSchemaVersion,
    validateConfig: (config) => {
      const contract = contractFor(contracts, manifest);
      return validatesFixtureSchema(schemaAtReference(contracts.schema, manifest.configSchemaRef), config);
    },
    create: ({ capabilityInstanceId, context }) => {
      const contract = contractFor(contracts, manifest);
      let handled = 0;
      let restored = false;
      return {
        restore: (snapshot) => {
          handled = snapshot.handled;
          restored = true;
          trace.push(`restore:${capabilityInstanceId}`);
        },
        start: () => {
          trace.push(`start:${capabilityInstanceId}`);
          context.subscribe('stress.capability.pulse', () => trace.push(`pulse:${capabilityInstanceId}`));
        },
        handle: ({ mode }) => {
          handled += 1;
          const event = Object.freeze({
            ...contract.event,
            payload: Object.freeze({ ...contract.event.payload, inputMode: mode }),
          });
          const outcome = Object.freeze({ ...contract.outcome });
          trace.push(`event:${capabilityInstanceId}:${event.type}`);
          return Object.freeze({ outcomeId: outcome.outcomeId, outcome, event, mode, handled });
        },
        snapshot: () => ({ handled, restored }),
        stop: () => trace.push(`stop:${capabilityInstanceId}`),
        dispose: () => trace.push(`dispose:${capabilityInstanceId}`),
      };
    },
  }));
}

function handlers() {
  return Object.freeze({
    'stress.claim': (command) => Object.freeze({ claims: command.payload.claims }),
    'stress.release': (command) => Object.freeze({ releaseClaimOwnerIds: command.payload.ownerIds }),
    'stress.pulse': (command) => Object.freeze({ events: [{ type: 'stress.capability.pulse', payload: command.payload }] }),
  });
}

function bindingsFor(pack, contracts) {
  return Object.freeze(pack.registries.capabilities.map((manifest) => Object.freeze({
    capabilityInstanceId: `stress-session:${manifest.protocolId}`,
    protocolId: manifest.protocolId,
    exactVersion: manifest.exactVersion,
    config: Object.freeze({ ...contractFor(contracts, manifest).config }),
    claims: Object.freeze(manifest.resourceClaims.map((claim) => Object.freeze({
      resourceId: claim.resourceId,
      mode: claim.mode,
      lifecycleScope: 'capability-session',
    }))),
  })));
}

async function liveRun(pack, contracts, trace) {
  const descriptors = capabilityDescriptors(pack, contracts, trace);
  const capabilityRegistry = new CapabilityRegistry(pack, descriptors);
  const kernel = new RuntimeKernel({ initialState: {}, handlers: handlers(), capabilityRegistry });
  const clock = new LogicalClock();
  const rngStreams = new OwnerRngStreams({ seed: 'mm60-stress-seed' });
  const scheduler = new DeterministicScheduler();
  const capabilityHost = new CapabilityHost({ runtimeContext: kernel.context, clock, rngStreams, scheduler, descriptors });
  const bindings = bindingsFor(pack, contracts);
  for (const binding of bindings) {
    capabilityHost.createSession(binding);
    capabilityHost.startSession(binding.capabilityInstanceId);
    const claim = await kernel.context.dispatch({
      type: 'stress.claim',
      actionOccurrenceId: `stress.claim:${binding.capabilityInstanceId}`,
      payload: capabilityHost.claimPlan(binding.capabilityInstanceId),
    });
    assert.equal(claim.status, 'committed');
  }
  return Object.freeze({ descriptors, capabilityRegistry, kernel, clock, rngStreams, scheduler, capabilityHost, bindings });
}

test('all 12 MM-02 Proposal fixtures compile and bind an exact mock capability contract', async () => {
  const [pack, contracts] = await Promise.all([compileStressPack(), loadStressContracts()]);
  assert.deepEqual(pack.registries.quests.map((quest) => quest.id.split('/').at(-1)), [...PROPOSALS].sort());
  assert.equal(pack.registries.capabilities.length, PROPOSALS.length);
  assert.equal(pack.campaign.capabilityRequirements.length, PROPOSALS.length);
  for (const proposal of PROPOSALS) {
    const quest = pack.registries.quests.find((entry) => entry.id === `stress.matrix:quest/${proposal}`);
    const provider = pack.registries.capabilities.find((entry) => entry.protocolId === `stress.matrix:capability/${proposal}`);
    assert.ok(quest, `missing Proposal quest ${proposal}`);
    assert.ok(provider, `missing Proposal provider ${proposal}`);
    const contract = contractFor(contracts, provider);
    assert.equal(quest.stages[0].objectives[0].capability.protocolId, provider.protocolId);
    assert.equal(quest.stages[0].objectives[0].capability.configRef, contract.configRef);
    assert.equal(quest.stages[0].objectives[0].capability.outcomeSchemaRef, provider.outcomeSchemaRef);
    assert.equal(provider.configSchemaRef, contract.configSchemaRef);
    assert.equal(provider.eventSchemaRef, contract.eventSchemaRef);
    assert.equal(provider.outcomeSchemaRef, contract.outcomeSchemaRef);
    assert.equal(validatesFixtureSchema(schemaAtReference(contracts.schema, provider.configSchemaRef), contract.config), true);
    assert.equal(validatesFixtureSchema(schemaAtReference(contracts.schema, provider.eventSchemaRef), { ...contract.event, payload: { ...contract.event.payload, inputMode: 'standard' } }), true);
    assert.equal(validatesFixtureSchema(schemaAtReference(contracts.schema, provider.outcomeSchemaRef), contract.outcome), true);
    assert.equal(provider.accessibility.alternativeInput, true);
    assert.equal(provider.lifecycle.supportsRestore, true);
    const initial = createQuestRun(quest, {
      questInstanceId: `stress-instance:${proposal}`,
      configResolver: (configRef) => contracts.byConfigRef.get(configRef)?.config,
    });
    assert.equal(initial.capabilityRequests[0].protocolId, provider.protocolId);
    assert.deepEqual(initial.capabilityRequests[0].config, contract.config);
    const cancelled = reduceQuestLifecycle({
      definition: quest,
      instance: initial.instance,
      command: { type: 'quest.cancel' },
      stateKey: 'stress.quest',
      capabilityLifecycle: { prepareCleanup: ({ capabilityInstanceIds }) => ({ releaseClaimOwnerIds: capabilityInstanceIds }) },
    });
    assert.equal(cancelled.nextState.status, 'cancelled');
    assert.deepEqual(cancelled.capabilityCleanups[0].capabilityInstanceIds, [initial.capabilityRequests[0].capabilityInstanceId]);
  }
});

test('stress mock providers preserve lifecycle, accessibility outcome, snapshot restore, cancel cleanup and atomic conflicts', async () => {
  const [pack, contracts] = await Promise.all([compileStressPack(), loadStressContracts()]);
  const sourceTrace = [];
  const run = await liveRun(pack, contracts, sourceTrace);

  for (const binding of run.bindings) {
    const contract = contracts.byProtocol.get(binding.protocolId);
    const standard = run.capabilityHost.handle(binding.capabilityInstanceId, { mode: 'standard' });
    const accessible = run.capabilityHost.handle(binding.capabilityInstanceId, { mode: 'accessible' });
    assert.equal(standard.outcomeId, accessible.outcomeId, `${binding.protocolId} accessibility must preserve outcome`);
    assert.deepEqual(standard.outcome, contract.outcome, `${binding.protocolId} preserves its declared outcome`);
    assert.deepEqual(accessible.outcome, contract.outcome, `${binding.protocolId} preserves its accessible outcome`);
    assert.deepEqual(standard.event, { ...contract.event, payload: { ...contract.event.payload, inputMode: 'standard' } });
    assert.deepEqual(accessible.event, { ...contract.event, payload: { ...contract.event.payload, inputMode: 'accessible' } });
    assert.throws(() => run.capabilityHost.createSession({
      capabilityInstanceId: `stress-invalid:${binding.protocolId}`,
      protocolId: binding.protocolId,
      exactVersion: binding.exactVersion,
      config: { ...contract.config, inputMode: 'unsupported' },
    }), /config is invalid/);
  }
  assert.equal(sourceTrace.filter((entry) => entry.startsWith('start:')).length, PROPOSALS.length);

  const firstConflict = await run.kernel.context.dispatch({
    type: 'stress.claim',
    actionOccurrenceId: 'stress.conflict:first',
    payload: { claims: [{ resourceId: 'stress.matrix:resource/one-item', ownerId: 'stress.conflict:first', mode: 'exclusive', lifecycleScope: 'action' }] },
  });
  const secondConflict = await run.kernel.context.dispatch({
    type: 'stress.claim',
    actionOccurrenceId: 'stress.conflict:second',
    payload: { claims: [{ resourceId: 'stress.matrix:resource/one-item', ownerId: 'stress.conflict:second', mode: 'exclusive', lifecycleScope: 'action' }] },
  });
  assert.equal(firstConflict.status, 'committed');
  assert.equal(secondConflict.status, 'rejected');
  assert.equal(secondConflict.error.code, 'ResourceConflict');

  const snapshot = encodeGameSnapshotV2({
    pack,
    kernel: run.kernel,
    clock: run.clock,
    rngStreams: run.rngStreams,
    scheduler: run.scheduler,
    capabilityBindings: run.bindings,
    capabilitySnapshots: run.bindings.map(({ capabilityInstanceId }) => run.capabilityHost.snapshotSession(capabilityInstanceId)),
    capabilityDescriptors: run.descriptors,
  });

  const restoredTrace = [];
  const restoredDescriptors = capabilityDescriptors(pack, contracts, restoredTrace);
  const restored = await restoreGameSnapshotV2(snapshot, {
    pack,
    handlers: handlers(),
    capabilityRegistry: new CapabilityRegistry(pack, restoredDescriptors),
    capabilityDescriptors: restoredDescriptors,
  });
  assert.equal(restoredTrace.filter((entry) => entry.startsWith('restore:')).length, PROPOSALS.length);
  assert.equal(restoredTrace.filter((entry) => entry.startsWith('start:')).length, PROPOSALS.length);
  for (const binding of run.bindings) {
    assert.equal(restored.capabilityHost.snapshotSession(binding.capabilityInstanceId).state.handled, 2);
  }

  const release = await restored.kernel.context.dispatch({
    type: 'stress.release',
    actionOccurrenceId: 'stress.cancel:release-claims',
    payload: { ownerIds: [...run.bindings.map(({ capabilityInstanceId }) => capabilityInstanceId), 'stress.conflict:first'] },
  });
  assert.equal(release.status, 'committed');
  restored.capabilityHost.finalizeCommittedCleanup({
    capabilityInstanceIds: run.bindings.map(({ capabilityInstanceId }) => capabilityInstanceId),
    reason: 'proposal-cancel',
  }, release);
  assert.deepEqual(restored.kernel.context.query(({ claims }) => claims), []);
  assert.equal(restoredTrace.filter((entry) => entry.startsWith('stop:')).length, PROPOSALS.length);
  assert.equal(restoredTrace.filter((entry) => entry.startsWith('dispose:')).length, PROPOSALS.length);

  const traceBeforePulse = restoredTrace.length;
  const pulse = await restored.kernel.context.dispatch({
    type: 'stress.pulse', actionOccurrenceId: 'stress.cancel:post-dispose-pulse', payload: { phase: 'after-dispose' },
  });
  assert.equal(pulse.status, 'committed');
  assert.equal(restoredTrace.length, traceBeforePulse, 'disposed mocks retain no event subscriptions');
});
