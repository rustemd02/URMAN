import assert from 'node:assert/strict';
import test from 'node:test';

import {
  CampaignMismatch,
  CapabilitySnapshotIncompatible,
  SnapshotIntegrityError,
  UnsupportedSnapshotVersion,
} from '../../../src/runtime/persistence/snapshot-errors.mjs';
import {
  decodeGameSnapshotV2,
  encodeGameSnapshotV2,
  restoreGameSnapshotV2,
} from '../../../src/runtime/persistence/game-snapshot-v2.mjs';
import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';
import { DeterministicScheduler } from '../../../src/runtime/world/deterministic-scheduler.mjs';
import { LogicalClock } from '../../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../../src/runtime/world/seeded-rng.mjs';

const MODULE_FINGERPRINT = 'a'.repeat(64);
const CAMPAIGN_FINGERPRINT = 'b'.repeat(64);
const CAPABILITY = Object.freeze({ protocolId: 'test.runtime:capability/puzzle', exactVersion: '1.0.0' });

function pack({ capabilities = [] } = {}) {
  return Object.freeze({
    schemaVersion: 1,
    packVersion: '1.0.0',
    campaign: Object.freeze({
      id: 'test.runtime:campaign/main',
      exactVersion: '1.0.0',
      entrypoint: 'test.runtime:scene/start',
      orderedModules: Object.freeze([{ moduleId: 'test.runtime', exactVersion: '1.0.0' }]),
      roleBindings: Object.freeze({}),
      capabilityRequirements: Object.freeze(capabilities),
      narrativeOrder: Object.freeze(['test.runtime:scene/start']),
      invariants: Object.freeze([]),
    }),
    registries: Object.freeze({ capabilities: Object.freeze([]) }),
    dependencyGraph: Object.freeze({ nodes: Object.freeze(['test.runtime']), edges: Object.freeze([]) }),
    moduleFingerprints: Object.freeze([{ moduleId: 'test.runtime', exactVersion: '1.0.0', sha256: MODULE_FINGERPRINT }]),
    campaignFingerprint: CAMPAIGN_FINGERPRINT,
  });
}

function capabilityRegistry(contentPack, descriptors = []) {
  return new CapabilityRegistry(contentPack, descriptors.map(({ protocolId, exactVersion }) => ({ protocolId, exactVersion, create: () => ({}) })));
}

function kernel(contentPack, { initialState = {}, handlers = {}, registryDescriptors = [] } = {}) {
  return new RuntimeKernel({ initialState, handlers, capabilityRegistry: capabilityRegistry(contentPack, registryDescriptors) });
}

function world() {
  const clock = new LogicalClock({ tick: 4 });
  const rngStreams = new OwnerRngStreams({ seed: 'snapshot-run' });
  rngStreams.stream('capability/a').nextUint32();
  rngStreams.stream('capability/a').nextUint32();
  const scheduler = new DeterministicScheduler();
  scheduler.schedule({ jobId: 'job/late', ownerId: 'capability/a', dueTick: 8, payload: { kind: 'late' } });
  return { clock, rngStreams, scheduler };
}

function statefulDescriptor(trace = []) {
  return Object.freeze({
    protocolId: CAPABILITY.protocolId,
    exactVersion: CAPABILITY.exactVersion,
    stateSchemaVersion: 1,
    validateConfig: (config) => config && typeof config.label === 'string',
    create: ({ capabilityInstanceId }) => {
      let progress = 0;
      return {
        restore: (state) => {
          trace.push(`restore:${capabilityInstanceId}`);
          progress = state.progress;
        },
        start: () => trace.push(`start:${capabilityInstanceId}`),
        handle: () => ({ outcomeId: 'test.runtime:outcome/resolved', capabilityInstanceId, progress }),
        snapshot: () => ({ progress }),
        stop: () => trace.push(`stop:${capabilityInstanceId}`),
        dispose: () => trace.push(`dispose:${capabilityInstanceId}`),
      };
    },
  });
}

function capabilityBinding(capabilityInstanceId, label) {
  return {
    capabilityInstanceId,
    protocolId: CAPABILITY.protocolId,
    exactVersion: CAPABILITY.exactVersion,
    config: { label },
    claims: [{ resourceId: `resource/${label}`, mode: 'exclusive', lifecycleScope: 'capability' }],
  };
}

function capabilitySnapshot(capabilityInstanceId, progress) {
  return {
    capabilityInstanceId,
    protocolId: CAPABILITY.protocolId,
    exactVersion: CAPABILITY.exactVersion,
    stateSchemaVersion: 1,
    state: { progress },
  };
}

test('GameSnapshotV2 roundtrip keeps canonical kernel/world state, provenance, custody and occurrence ledger', async () => {
  const contentPack = pack();
  const source = kernel(contentPack, {
    initialState: {
      custody: [{ itemId: 'item/recording', custodyOwnerId: 'npc/keeper', condition: { worn: false } }],
      evidence: [{ evidenceId: 'evidence/recording', claimantId: 'actor/player', sourceId: 'asset/recording', provenanceChain: ['asset/recording'], metadata: { room: 'archive' } }],
      quests: { active: { checkpoint: { stage: 2 }, generatedBindings: { host: 'npc/keeper' } } },
    },
    handlers: {
      mark: (command) => ({ effects: [{ op: 'state.set', key: 'mark', value: command.payload }], value: command.payload }),
    },
  });
  await source.context.dispatch({ type: 'mark', actionOccurrenceId: 'action/mark', payload: { present: true } });
  const services = world();
  const snapshot = encodeGameSnapshotV2({ pack: contentPack, kernel: source, ...services });
  const restored = await restoreGameSnapshotV2(snapshot, {
    pack: contentPack,
    handlers: { mark: (command) => ({ effects: [{ op: 'state.set', key: 'mark', value: command.payload }], value: command.payload }) },
    capabilityRegistry: capabilityRegistry(contentPack),
  });

  assert.deepEqual(restored.kernel.exportSnapshot(), source.exportSnapshot());
  assert.deepEqual(restored.clock.exportSnapshot(), services.clock.exportSnapshot());
  assert.deepEqual(restored.rngStreams.exportSnapshot(), services.rngStreams.exportSnapshot());
  assert.deepEqual(restored.scheduler.exportSnapshot(), services.scheduler.exportSnapshot());
  const duplicate = await restored.kernel.context.dispatch({ type: 'mark', actionOccurrenceId: 'action/mark', payload: { present: true } });
  assert.equal(duplicate.error.code, 'DuplicateOccurrence');
  assert.deepEqual(restored.kernel.context.select((state) => state.custody), source.context.select((state) => state.custody));
  assert.deepEqual(restored.kernel.context.select((state) => state.evidence), source.context.select((state) => state.evidence));
});

test('GameSnapshotV2 restores parallel same-protocol capability instances before any starts', async () => {
  const contentPack = pack({ capabilities: [CAPABILITY] });
  const source = kernel(contentPack, { registryDescriptors: [CAPABILITY] });
  const services = world();
  const bindings = [capabilityBinding('capability/a', 'alpha'), capabilityBinding('capability/b', 'beta')];
  const snapshots = [capabilitySnapshot('capability/a', 3), capabilitySnapshot('capability/b', 7)];
  const descriptor = statefulDescriptor();
  const saved = encodeGameSnapshotV2({
    pack: contentPack,
    kernel: source,
    ...services,
    capabilityBindings: bindings,
    capabilitySnapshots: snapshots,
    capabilityDescriptors: [descriptor],
  });
  const trace = [];
  const restored = await restoreGameSnapshotV2(saved, {
    pack: contentPack,
    capabilityRegistry: capabilityRegistry(contentPack, [descriptor]),
    capabilityDescriptors: [statefulDescriptor(trace)],
  });

  assert.deepEqual(trace, ['restore:capability/a', 'restore:capability/b', 'start:capability/a', 'start:capability/b']);
  assert.deepEqual(restored.capabilityHost.handle('capability/a', null), {
    outcomeId: 'test.runtime:outcome/resolved', capabilityInstanceId: 'capability/a', progress: 3,
  });
  assert.deepEqual(restored.capabilityHost.handle('capability/b', null), {
    outcomeId: 'test.runtime:outcome/resolved', capabilityInstanceId: 'capability/b', progress: 7,
  });
});

test('restored scheduler preserves its fire ledger, so an overdue job fires exactly once', async () => {
  const contentPack = pack();
  const source = kernel(contentPack);
  const services = world();
  const saved = encodeGameSnapshotV2({ pack: contentPack, kernel: source, ...services });
  const restored = await restoreGameSnapshotV2(saved, { pack: contentPack, capabilityRegistry: capabilityRegistry(contentPack) });
  const due = restored.scheduler.claimDue(8);
  assert.deepEqual(due.map(({ jobId, occurrenceId }) => [jobId, occurrenceId]), [['job/late', 'scheduled:job/late:8']]);
  restored.scheduler.acknowledge(due[0].jobId, due[0].occurrenceId);
  const afterFire = encodeGameSnapshotV2({
    pack: contentPack,
    kernel: restored.kernel,
    clock: restored.clock,
    rngStreams: restored.rngStreams,
    scheduler: restored.scheduler,
  });
  const reloaded = await restoreGameSnapshotV2(afterFire, { pack: contentPack, capabilityRegistry: capabilityRegistry(contentPack) });
  assert.deepEqual(reloaded.scheduler.claimDue(100), []);
});

test('incompatible campaign, unsupported version and state schema reject before a supplied run can be replaced', async () => {
  const contentPack = pack({ capabilities: [CAPABILITY] });
  const source = kernel(contentPack, { initialState: { preserved: 'yes' }, registryDescriptors: [CAPABILITY] });
  const services = world();
  const descriptor = statefulDescriptor();
  const saved = encodeGameSnapshotV2({
    pack: contentPack,
    kernel: source,
    ...services,
    capabilityBindings: [capabilityBinding('capability/a', 'alpha')],
    capabilitySnapshots: [capabilitySnapshot('capability/a', 2)],
    capabilityDescriptors: [descriptor],
  });
  const before = source.exportSnapshot();

  const wrongCampaign = structuredClone(saved);
  wrongCampaign.campaignLock.campaignFingerprint = 'c'.repeat(64);
  await assert.rejects(
    restoreGameSnapshotV2(wrongCampaign, {
      pack: contentPack,
      capabilityRegistry: capabilityRegistry(contentPack, [descriptor]),
      capabilityDescriptors: [descriptor],
    }),
    (error) => error instanceof CampaignMismatch && error.code === 'CampaignMismatch',
  );

  const legacy = structuredClone(saved);
  legacy.snapshotSchemaVersion = 1;
  assert.throws(() => decodeGameSnapshotV2(legacy, { pack: contentPack, capabilityDescriptors: [descriptor] }), UnsupportedSnapshotVersion);

  const newerDescriptor = { ...descriptor, stateSchemaVersion: 2 };
  assert.throws(
    () => decodeGameSnapshotV2(saved, { pack: contentPack, capabilityDescriptors: [newerDescriptor] }),
    (error) => error instanceof CapabilitySnapshotIncompatible && error.code === 'CapabilitySnapshotIncompatible',
  );
  assert.deepEqual(source.exportSnapshot(), before);
});

test('a failed staged capability restore disposes previously restored sessions without touching a source run', async () => {
  const contentPack = pack({ capabilities: [CAPABILITY] });
  const source = kernel(contentPack, { initialState: { preserved: 'source' }, registryDescriptors: [CAPABILITY] });
  const services = world();
  const sourceBefore = source.exportSnapshot();
  const workingDescriptor = statefulDescriptor();
  const saved = encodeGameSnapshotV2({
    pack: contentPack,
    kernel: source,
    ...services,
    capabilityBindings: [capabilityBinding('capability/a', 'alpha'), capabilityBinding('capability/b', 'beta')],
    capabilitySnapshots: [capabilitySnapshot('capability/a', 1), capabilitySnapshot('capability/b', 2)],
    capabilityDescriptors: [workingDescriptor],
  });
  const trace = [];
  const failingDescriptor = {
    ...statefulDescriptor(trace),
    create: ({ capabilityInstanceId }) => ({
      restore: () => {
        trace.push(`restore:${capabilityInstanceId}`);
        if (capabilityInstanceId === 'capability/b') throw new Error('state rejected');
      },
      stop: () => trace.push(`stop:${capabilityInstanceId}`),
      dispose: () => trace.push(`dispose:${capabilityInstanceId}`),
      snapshot: () => ({ progress: 0 }),
    }),
  };

  await assert.rejects(
    restoreGameSnapshotV2(saved, {
      pack: contentPack,
      capabilityRegistry: capabilityRegistry(contentPack, [failingDescriptor]),
      capabilityDescriptors: [failingDescriptor],
    }),
    (error) => error instanceof CapabilitySnapshotIncompatible && error.details.capabilityInstanceId === 'capability/b',
  );
  assert.deepEqual(trace, [
    'restore:capability/a',
    'restore:capability/b',
    'dispose:capability/b',
    'stop:capability/a',
    'dispose:capability/a',
  ]);
  assert.deepEqual(source.exportSnapshot(), sourceBefore);
});

test('strict decode rejects unknown fields and mismatched saved sequence without field dropping', () => {
  const contentPack = pack();
  const source = kernel(contentPack);
  const services = world();
  const saved = encodeGameSnapshotV2({ pack: contentPack, kernel: source, ...services });
  const extra = structuredClone(saved);
  extra.legacy = true;
  assert.throws(() => decodeGameSnapshotV2(extra, { pack: contentPack }), SnapshotIntegrityError);
  const sequence = structuredClone(saved);
  sequence.savedSequence = 4;
  assert.throws(() => decodeGameSnapshotV2(sequence, { pack: contentPack }), SnapshotIntegrityError);
  const rngExtra = structuredClone(saved);
  rngExtra.world.rngStreams.ignored = true;
  assert.throws(() => decodeGameSnapshotV2(rngExtra, { pack: contentPack }), SnapshotIntegrityError);
});
