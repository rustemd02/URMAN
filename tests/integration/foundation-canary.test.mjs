import assert from 'node:assert/strict';
import { cp, mkdtemp, readFile, readdir, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { compileContent } from '../../scripts/content/compile-content.mjs';
import { CapabilityHost } from '../../src/runtime/capabilities/capability-host.mjs';
import { RuntimeKernel } from '../../src/runtime/kernel/runtime-kernel.mjs';
import { SceneRegistry, CapabilityRegistry, ContentRegistry } from '../../src/runtime/registries/runtime-registries.mjs';
import { AudioResolver } from '../../src/runtime/media/audio-resolver.mjs';
import { AssetResolver, TextResolver, validateManifestClosure } from '../../src/runtime/resolvers/content-resolvers.mjs';
import { encodeGameSnapshotV2, restoreGameSnapshotV2 } from '../../src/runtime/persistence/game-snapshot-v2.mjs';
import { createQuestRun, reduceQuestLifecycle } from '../../src/runtime/quests/quest-reducer.mjs';
import { DeterministicScheduler } from '../../src/runtime/world/deterministic-scheduler.mjs';
import { planCustodyBatch } from '../../src/runtime/world/custody-store.mjs';
import { planEvidenceClaim } from '../../src/runtime/world/evidence-provenance.mjs';
import { LogicalClock } from '../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../src/runtime/world/seeded-rng.mjs';
import {
  CANARY_SIGNAL_OUTCOME,
  CANARY_SIGNAL_PROTOCOL,
  CANARY_SIGNAL_VERSION,
  createCanarySignalDescriptor,
} from '../helpers/mock-capabilities/canary-signal-provider.mjs';

const FIXTURE_ROOT = 'tests/fixtures/content/canary';
const CORE_MODULE = 'common/module.json';
const ROUTE_MODULE = 'route/module.json';
const QUEST_ID = 'canary.core:quest/tune-signal';
const QUEST_INSTANCE_ID = 'canary.session/tune-signal';
const CAPABILITY_INSTANCE_ID = 'capability:canary.session/tune-signal:tune:match-pulse';

function fixturePaths(root = FIXTURE_ROOT, variant = 'amber') {
  return Object.freeze({
    campaignPath: path.posix.join(root, 'campaigns', variant, 'campaign.json'),
    moduleManifestPaths: [path.posix.join(root, CORE_MODULE), path.posix.join(root, ROUTE_MODULE)],
  });
}

async function compileCanary(variant = 'amber', { cwd = process.cwd(), root = FIXTURE_ROOT } = {}) {
  return compileContent({ cwd, ...fixturePaths(root, variant) });
}

function capabilityRegistry(pack) {
  return new CapabilityRegistry(pack, [Object.freeze({
    protocolId: CANARY_SIGNAL_PROTOCOL,
    exactVersion: CANARY_SIGNAL_VERSION,
    create: () => Object.freeze({}),
  })]);
}

function definition(pack, id) {
  for (const entries of Object.values(pack.registries)) {
    const found = entries.find((entry) => entry.id === id);
    if (found) return found;
  }
  throw new Error(`Canary definition ${id} is missing.`);
}

function createQuestController(definitionValue, initialInstance) {
  let instance = structuredClone(initialInstance);
  let lifecycle;
  let lastReduction;
  const handler = (command, context) => {
    const reduced = reduceQuestLifecycle({
      definition: definitionValue,
      instance,
      command: command.payload,
      stateKey: 'canary.quest',
      evaluationContext: context.state,
      configResolver: () => ({ mode: 'accessible' }),
      capabilityLifecycle: lifecycle,
      planEffects: (effects) => ({
        effects: effects.map((effect) => ({ op: 'state.set', key: 'canary.quest.effect', value: effect })),
        events: [],
        claims: [],
      }),
    });
    lastReduction = reduced;
    if (!reduced.plan.rejection) instance = reduced.nextState;
    return reduced.plan;
  };
  return Object.freeze({
    handlers: Object.freeze({ 'canary.quest': handler }),
    setLifecycle: (value) => { lifecycle = value; },
    lastReduction: () => lastReduction,
    instance: () => structuredClone(instance),
  });
}

function worldHandler(command, context) {
  const custody = planCustodyBatch({
    state: context.state,
    operations: [{
      op: 'transfer',
      itemId: 'canary.item/signal-note',
      fromOwnerId: 'canary.actor/guide',
      toOwnerId: 'canary.actor/player',
    }],
    claimOwnerId: 'canary.action/world',
    claimLifecycleScope: 'canary.action',
  });
  const evidence = planEvidenceClaim({
    state: context.state,
    evidenceId: 'canary.evidence/signal-note',
    claimantId: 'canary.actor/player',
    sourceId: 'canary.item/signal-note',
    metadata: { phase: command.payload.phase },
  });
  return Object.freeze({
    claims: custody.claims,
    effects: [...custody.effects, ...evidence.effects],
    events: [...custody.events, ...evidence.events],
    value: { custody: custody.value, evidence: evidence.value },
  });
}

function runtimeHandlers(controller) {
  return Object.freeze({
    ...controller.handlers,
    'canary.claim': (command) => command.payload,
    'canary.release': (command) => ({ releaseClaimOwnerIds: [command.payload.ownerId] }),
    'canary.world': worldHandler,
    'canary.scheduled': (command) => ({
      effects: [{ op: 'state.increment', key: 'canary.scheduled', delta: 1 }],
      events: [{ type: 'canary.scheduled.fire', payload: command.payload }],
      value: command.payload,
    }),
    'canary.pulse': (command) => ({ events: [{ type: 'canary.runtime.pulse', payload: command.payload }] }),
  });
}

function recordEvents(runtime) {
  const events = [];
  const subscriptions = [
    'quest.lifecycle',
    'world.custody.changed',
    'world.evidence.claimed',
    'canary.scheduled.fire',
    'canary.runtime.pulse',
  ].map((eventType) => runtime.context.subscribe(eventType, (event) => events.push(event)));
  return Object.freeze({
    events,
    dispose: () => subscriptions.forEach((subscription) => subscription.dispose()),
  });
}

async function createLiveRun(pack, { seed = 'canary-seed' } = {}) {
  const questDefinition = definition(pack, QUEST_ID);
  const questRun = createQuestRun(questDefinition, {
    questInstanceId: QUEST_INSTANCE_ID,
    configResolver: () => ({ mode: 'accessible' }),
  });
  const controller = createQuestController(questDefinition, questRun.instance);
  const runtime = new RuntimeKernel({
    initialState: {
      'world.custody': [{
        itemId: 'canary.item/signal-note',
        custodyOwnerId: 'canary.actor/guide',
        condition: { intact: true },
      }],
      'world.evidence': [],
    },
    handlers: runtimeHandlers(controller),
    capabilityRegistry: capabilityRegistry(pack),
  });
  const events = recordEvents(runtime);
  const clock = new LogicalClock();
  const rngStreams = new OwnerRngStreams({ seed });
  const scheduler = new DeterministicScheduler();
  const trace = [];
  const descriptor = createCanarySignalDescriptor({ trace });
  const host = new CapabilityHost({ runtimeContext: runtime.context, clock, rngStreams, scheduler, descriptors: [descriptor] });
  controller.setLifecycle(host.questLifecycle());

  const request = questRun.capabilityRequests[0];
  assert.equal(request.protocolId, CANARY_SIGNAL_PROTOCOL);
  const binding = Object.freeze({
    capabilityInstanceId: request.capabilityInstanceId,
    protocolId: request.protocolId,
    exactVersion: request.exactVersion,
    config: request.config,
    claims: [{ resourceId: 'canary.core:resource/signal-console', mode: 'exclusive', lifecycleScope: 'capability-session' }],
  });
  host.createSession(binding);
  host.startSession(binding.capabilityInstanceId);
  const refresh = await runtime.context.dispatch({
    type: 'canary.quest', actionOccurrenceId: 'canary.action/refresh', payload: { type: 'quest.refresh' },
  });
  assert.equal(refresh.status, 'committed');
  const claim = await runtime.context.dispatch({
    type: 'canary.claim', actionOccurrenceId: 'canary.action/capability-claim', payload: host.claimPlan(binding.capabilityInstanceId),
  });
  assert.equal(claim.status, 'committed');
  const pulse = await runtime.context.dispatch({
    type: 'canary.pulse', actionOccurrenceId: 'canary.action/pulse', payload: { id: 'before-save' },
  });
  assert.equal(pulse.status, 'committed');
  const world = await runtime.context.dispatch({
    type: 'canary.world', actionOccurrenceId: 'canary.action/world', payload: { phase: 'mid-objective' },
  });
  assert.equal(world.status, 'committed');
  const release = await runtime.context.dispatch({
    type: 'canary.release', actionOccurrenceId: 'canary.action/world-release', payload: { ownerId: 'canary.action/world' },
  });
  assert.equal(release.status, 'committed');
  assert.equal(host.handle(binding.capabilityInstanceId, { mode: 'accessible' }).outcomeId, CANARY_SIGNAL_OUTCOME);
  clock.advance(2);

  return Object.freeze({ pack, runtime, clock, rngStreams, scheduler, host, descriptor, controller, binding, trace, events });
}

function snapshotOf(run) {
  return encodeGameSnapshotV2({
    pack: run.pack,
    kernel: run.runtime,
    clock: run.clock,
    rngStreams: run.rngStreams,
    scheduler: run.scheduler,
    capabilityBindings: [run.binding],
    capabilitySnapshots: [run.host.snapshotSession(run.binding.capabilityInstanceId)],
    capabilityDescriptors: [run.descriptor],
  });
}

async function restoreRun(snapshot, pack) {
  const controller = createQuestController(definition(pack, QUEST_ID), snapshot.runtime.state['canary.quest']);
  const trace = [];
  const descriptor = createCanarySignalDescriptor({ trace });
  const restored = await restoreGameSnapshotV2(snapshot, {
    pack,
    handlers: runtimeHandlers(controller),
    capabilityRegistry: capabilityRegistry(pack),
    capabilityDescriptors: [descriptor],
  });
  controller.setLifecycle(restored.capabilityHost.questLifecycle());
  const events = recordEvents(restored.kernel);
  return Object.freeze({
    pack,
    runtime: restored.kernel,
    clock: restored.clock,
    rngStreams: restored.rngStreams,
    scheduler: restored.scheduler,
    host: restored.capabilityHost,
    descriptor,
    controller,
    binding: snapshot.capabilityBindings[0],
    trace,
    events,
  });
}

async function completeRun(run) {
  run.clock.advance(1);
  const due = run.scheduler.claimDueForOwner(run.binding.capabilityInstanceId, run.clock.now());
  assert.equal(due.length, 1);
  const scheduled = await run.runtime.context.dispatch({
    type: 'canary.scheduled', actionOccurrenceId: due[0].occurrenceId, payload: due[0].payload,
  });
  assert.equal(scheduled.status, 'committed');
  run.scheduler.acknowledge(due[0].jobId, due[0].occurrenceId);
  const complete = await run.runtime.context.dispatch({
    type: 'canary.quest', actionOccurrenceId: 'canary.action/complete', payload: { type: 'objective.complete', objectiveId: 'match-pulse' },
  });
  assert.equal(complete.status, 'committed');
  for (const cleanup of run.controller.lastReduction().capabilityCleanups) {
    run.host.questLifecycle().finalizeCommitted(cleanup, complete);
  }
  const traceLength = run.trace.length;
  const afterDisposePulse = await run.runtime.context.dispatch({
    type: 'canary.pulse', actionOccurrenceId: 'canary.action/pulse-after-dispose', payload: { id: 'after-dispose' },
  });
  assert.equal(afterDisposePulse.status, 'committed');
  assert.equal(run.trace.length, traceLength, 'disposed provider must not retain event listeners');
  assert.deepEqual(run.runtime.context.query(({ claims }) => claims), []);
  assert.deepEqual(run.scheduler.exportSnapshot().jobs, []);
  return Object.freeze({
    kernel: run.runtime.exportSnapshot(),
    clock: run.clock.exportSnapshot(),
    rngStreams: run.rngStreams.exportSnapshot(),
    scheduler: run.scheduler.exportSnapshot(),
    events: structuredClone(run.events.events),
  });
}

async function mutableCanary() {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'canary-foundation-'));
  await cp(FIXTURE_ROOT, directory, { recursive: true });
  return directory;
}

async function readJson(file) {
  return JSON.parse(await readFile(file, 'utf8'));
}

async function writeJson(file, value) {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}

async function allFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.map(async (entry) => {
    const target = path.join(directory, entry.name);
    return entry.isDirectory() ? allFiles(target) : [target];
  }));
  return nested.flat();
}

test('foundation canary compiles two campaign variants and swaps role-bound content without renderer changes', async () => {
  const [amber, indigo] = await Promise.all([compileCanary('amber'), compileCanary('indigo')]);
  assert.equal(amber.ok, true, JSON.stringify(amber.diagnostics));
  assert.equal(indigo.ok, true, JSON.stringify(indigo.diagnostics));
  assert.notEqual(amber.pack.campaignFingerprint, indigo.pack.campaignFingerprint);
  assert.deepEqual(amber.pack.campaign.orderedModules.map(({ moduleId }) => moduleId), ['canary.core', 'canary.route']);
  assert.deepEqual(indigo.pack.campaign.orderedModules.map(({ moduleId }) => moduleId), ['canary.route', 'canary.core']);
  assert.deepEqual(amber.pack.campaign.narrativeOrder.slice(-2), [QUEST_ID, 'canary.route:quest/read-marker']);
  assert.deepEqual(indigo.pack.campaign.narrativeOrder.slice(-2), ['canary.route:quest/read-marker', QUEST_ID]);

  const closure = validateManifestClosure(amber.pack);
  assert.ok(closure.assetIds.includes('canary.core:asset/signal-tone'));
  assert.ok(closure.textIds.includes('canary.core:text/guide-line'));
  const content = ContentRegistry.fromPack(amber.pack);
  assert.equal(content.get(QUEST_ID).id, QUEST_ID);
  const scenes = new SceneRegistry(amber.pack, [{ sceneType: 'static', create: (scene) => ({ sceneId: scene.id }) }]);
  assert.deepEqual(scenes.create('canary.core:scene/landing'), { sceneId: 'canary.core:scene/landing' });

  const resolveFileUrl = (_file, metadata) => `canary://${metadata.moduleId}/logical/${metadata.assetId}`;
  const amberAssets = new AssetResolver(amber.pack, { resolveFileUrl });
  const indigoAssets = new AssetResolver(indigo.pack, { resolveFileUrl });
  const amberGuide = definition(amber.pack, amber.pack.campaign.roleBindings.guide);
  const indigoGuide = definition(indigo.pack, indigo.pack.campaign.roleBindings.guide);
  const amberVisual = amberAssets.resolve(amberGuide.assetIds[0]);
  const indigoVisual = indigoAssets.resolve(indigoGuide.assetIds[0]);
  assert.notEqual(amberVisual.assetId, indigoVisual.assetId);
  assert.match(amberVisual.url, /^canary:\/\//);
  assert.equal(amberVisual.url.includes('/assets/'), false, 'renderer receives only host-resolved URL');

  const text = new TextResolver(amber.pack);
  const indigoText = new TextResolver(indigo.pack);
  const amberName = text.resolve(amberVisual.accessibility.altTextId, 'ru');
  const indigoName = indigoText.resolve(indigoVisual.accessibility.altTextId, 'ru');
  assert.deepEqual(
    [amberName.text, indigoName.text],
    ['Янтарный проводник', 'Индиговый проводник'],
    'role binding selects the NPC asset and its logical text without a renderer branch',
  );
  const model = text.resolve('canary.core:text/guide-line', 'tt', { term: { vocabularyId: 'canary.core:vocabulary/lantern' } });
  assert.equal(model.resolvedLocale, 'tt');
  assert.deepEqual(model.segments.filter(({ type }) => type === 'vocabulary').map(({ vocabularyId }) => vocabularyId), ['canary.core:vocabulary/lantern']);
  const audio = new AudioResolver(amber.pack, { assetResolver: amberAssets, textResolver: text });
  const resolvedAudio = audio.resolve('canary.core:asset/signal-tone', { locale: 'ru', outcomeKey: CANARY_SIGNAL_OUTCOME });
  assert.equal(resolvedAudio.captions.text, 'Низкий импульс сигнала.');
  assert.equal(resolvedAudio.nonAudioCue.outcomeKey, CANARY_SIGNAL_OUTCOME);
});

test('foundation canary restores an active quest deterministically and disposes every owned resource', async () => {
  const compiled = await compileCanary('amber');
  assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
  const source = await createLiveRun(compiled.pack);
  assert.equal(source.runtime.context.select((state) => state['canary.quest'].status), 'active');
  const snapshot = snapshotOf(source);
  assert.equal(snapshot.runtime.state['canary.quest'].status, 'active');
  assert.equal(snapshot.capabilitySnapshots[0].capabilityInstanceId, CAPABILITY_INSTANCE_ID);
  assert.equal(snapshot.world.scheduler.jobs.length, 1, 'mid-objective save retains the pending scheduled effect');
  const sourcePrefix = structuredClone(source.events.events);
  const restored = await restoreRun(snapshot, compiled.pack);
  assert.deepEqual(restored.trace.slice(0, 2), [`restore:${CAPABILITY_INSTANCE_ID}`, `start:${CAPABILITY_INSTANCE_ID}`]);

  const [sourceResult, restoredResult] = await Promise.all([completeRun(source), completeRun(restored)]);
  assert.deepEqual(restoredResult.kernel, sourceResult.kernel);
  assert.deepEqual(restoredResult.clock, sourceResult.clock);
  assert.deepEqual(restoredResult.rngStreams, sourceResult.rngStreams);
  assert.deepEqual(restoredResult.scheduler, sourceResult.scheduler);
  assert.deepEqual([...sourcePrefix, ...restoredResult.events], sourceResult.events);
  assert.equal(restoredResult.scheduler.fired.length, 1, 'restored scheduled job fires exactly once');
  source.events.dispose();
  restored.events.dispose();
});

test('same seed and action log produce the same state and ordered event log', async () => {
  const compiled = await compileCanary('amber');
  assert.equal(compiled.ok, true, JSON.stringify(compiled.diagnostics));
  const first = await createLiveRun(compiled.pack, { seed: 'same-seed' });
  const second = await createLiveRun(compiled.pack, { seed: 'same-seed' });
  const [firstResult, secondResult] = await Promise.all([completeRun(first), completeRun(second)]);
  assert.deepEqual(firstResult, secondResult);
  first.events.dispose();
  second.events.dispose();
});

test('canary preflight failures have stable module, source and pointer diagnostics', async () => {
  const cases = [
    {
      name: 'duplicate ID',
      mutate: async (root) => {
        const file = path.join(root, 'common', 'definitions.json');
        const definitions = await readJson(file);
        definitions.push({ ...definitions[0] });
        await writeJson(file, definitions);
      },
      code: 'DuplicateId',
      moduleId: 'canary.core',
      sourcePath: 'common/definitions.json',
      pointer: '/21/id',
    },
    {
      name: 'missing provider',
      mutate: async (root) => {
        const file = path.join(root, 'campaigns', 'amber', 'campaign.json');
        const campaign = await readJson(file);
        campaign.capabilityRequirements = [{ protocolId: 'canary.core:capability/missing', exactVersion: '1.0.0' }];
        await writeJson(file, campaign);
      },
      code: 'MissingCapability',
      moduleId: 'urman.compiler',
      sourcePath: 'campaigns/amber/campaign.json',
      pointer: '/capabilityRequirements/0',
    },
    {
      name: 'missing asset',
      mutate: async (root) => {
        const file = path.join(root, 'common', 'definitions.json');
        const definitions = await readJson(file);
        definitions.find(({ id }) => id === 'canary.core:scene/landing').assetRefs = ['canary.core:asset/missing'];
        await writeJson(file, definitions);
      },
      code: 'MissingAsset',
      moduleId: 'canary.core',
      sourcePath: 'common/definitions.json',
      pointer: '/17/assetRefs/0',
    },
    {
      name: 'unknown opcode',
      mutate: async (root) => {
        const file = path.join(root, 'common', 'definitions.json');
        const definitions = await readJson(file);
        definitions.find(({ id }) => id === 'canary.core:scene/landing').onEnter = [{ op: 'unrecognized.effect' }];
        await writeJson(file, definitions);
      },
      code: 'UnknownOpcode',
      moduleId: 'canary.core',
      sourcePath: 'common/definitions.json',
      pointer: '/17/onEnter/0/op',
    },
    {
      name: 'unreachable required entry',
      mutate: async (root) => {
        const file = path.join(root, 'campaigns', 'amber', 'campaign.json');
        const campaign = await readJson(file);
        campaign.invariants.push({
          id: 'canary.core:invariant/workshop-reachable',
          description: 'Workshop must be reachable.',
          severity: 'error',
          kind: 'required-reachable',
          targetId: 'canary.route:scene/workshop',
        });
        await writeJson(file, campaign);
      },
      code: 'UnreachableRequiredEntry',
      moduleId: 'urman.compiler',
      sourcePath: 'campaigns/amber/campaign.json',
      pointer: '/invariants/1/targetId',
    },
  ];
  for (const scenario of cases) {
    const root = await mutableCanary();
    await scenario.mutate(root);
    const first = await compileCanary('amber', { cwd: root, root: '.' });
    const second = await compileCanary('amber', { cwd: root, root: '.' });
    assert.equal(first.ok, false, scenario.name);
    assert.deepEqual(first, second, `${scenario.name} diagnostic is deterministic`);
    const diagnostic = first.diagnostics.find(({ code, moduleId, sourcePath, jsonPointer }) => code === scenario.code
      && moduleId === scenario.moduleId && sourcePath === scenario.sourcePath && jsonPointer === scenario.pointer);
    assert.ok(diagnostic, `${scenario.name} must retain source/path/pointer`);
  }
});

test('fixture and mock provider remain free of current-campaign identifiers', async () => {
  const files = [
    ...(await allFiles(FIXTURE_ROOT)),
    'tests/helpers/mock-capabilities/canary-signal-provider.mjs',
  ];
  for (const file of files) {
    const source = await readFile(file, 'utf8');
    assert.equal(/urman/i.test(source), false, `${file} must remain neutral`);
  }
});
