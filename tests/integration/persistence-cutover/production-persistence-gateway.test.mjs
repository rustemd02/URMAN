import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import {
  LEGACY_GAMEPLAY_STORAGE_KEYS,
  LEGACY_RESET_MARKER_KEY,
  LEGACY_RESET_MARKER_VALUE,
  LEGACY_RESET_NOTICE,
  PRODUCTION_SAVE_KEY,
  createProductionPersistenceGateway,
} from '../../../src/runtime/persistence/production-persistence-gateway.mjs';
import { RuntimeKernel } from '../../../src/runtime/kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../../../src/runtime/registries/runtime-registries.mjs';
import { decodeGameSnapshotV2, encodeGameSnapshotV2, restoreGameSnapshotV2 } from '../../../src/runtime/persistence/game-snapshot-v2.mjs';
import { DeterministicScheduler } from '../../../src/runtime/world/deterministic-scheduler.mjs';
import { LogicalClock } from '../../../src/runtime/world/logical-clock.mjs';
import { OwnerRngStreams } from '../../../src/runtime/world/seeded-rng.mjs';
import { checkRuntimeArchitecture } from '../../../scripts/check-runtime-architecture.mjs';

const ROOT = process.cwd();
const MODULE_FINGERPRINT = 'a'.repeat(64);
const CAMPAIGN_FINGERPRINT = 'b'.repeat(64);

function createPack(campaignFingerprint = CAMPAIGN_FINGERPRINT) {
  return Object.freeze({
    schemaVersion: 1,
    packVersion: '1.0.0',
    campaign: Object.freeze({
      id: 'test.persistence:campaign/main',
      exactVersion: '1.0.0',
      entrypoint: 'test.persistence:scene/start',
      orderedModules: Object.freeze([{ moduleId: 'test.persistence', exactVersion: '1.0.0' }]),
      roleBindings: Object.freeze({}),
      capabilityRequirements: Object.freeze([]),
      narrativeOrder: Object.freeze(['test.persistence:scene/start']),
      invariants: Object.freeze([]),
    }),
    registries: Object.freeze({ capabilities: Object.freeze([]) }),
    dependencyGraph: Object.freeze({ nodes: Object.freeze(['test.persistence']), edges: Object.freeze([]) }),
    moduleFingerprints: Object.freeze([{ moduleId: 'test.persistence', exactVersion: '1.0.0', sha256: MODULE_FINGERPRINT }]),
    campaignFingerprint,
  });
}

function createStorage(entries = []) {
  const values = new Map(entries);
  const removed = [];
  return {
    getItem: (key) => values.has(key) ? values.get(key) : null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => {
      removed.push(key);
      values.delete(key);
    },
    values,
    removed,
  };
}

function runtimeHandlers() {
  return Object.freeze({
    mark: (command) => ({ effects: [{ op: 'state.set', key: 'investigation', value: command.payload }], value: command.payload }),
  });
}

function createRuntime(pack) {
  const capabilityRegistry = new CapabilityRegistry(pack);
  const kernel = new RuntimeKernel({
    initialState: { investigation: { status: 'stable' } },
    handlers: runtimeHandlers(),
    capabilityRegistry,
  });
  const clock = new LogicalClock({ tick: 4 });
  const rngStreams = new OwnerRngStreams({ seed: 'persistence-cutover' });
  const scheduler = new DeterministicScheduler();
  return { capabilityRegistry, kernel, clock, rngStreams, scheduler };
}

/** Test double for the closed RuntimePersistencePort exposed by RuntimeBootstrap. */
function createRuntimePersistence(pack) {
  const capabilityRegistry = new CapabilityRegistry(pack);
  let live = createRuntime(pack);
  let restoreCalls = 0;
  return Object.freeze({
    capture: () => encodeGameSnapshotV2({ pack, ...live }),
    decode: (snapshot) => decodeGameSnapshotV2(snapshot, { pack }),
    restore: async (snapshot) => {
      restoreCalls += 1;
      live = await restoreGameSnapshotV2(snapshot, {
        pack,
        capabilityRegistry,
        handlers: runtimeHandlers(),
      });
    },
    live: () => live,
    restoreCalls: () => restoreCalls,
  });
}

function unusedRuntimePersistence() {
  return Object.freeze({
    capture: () => { throw new Error('capture must not be called'); },
    decode: () => { throw new Error('decode must not be called'); },
    restore: () => { throw new Error('restore must not be called'); },
  });
}

test('production gateway captures, decodes and restores only through the closed RuntimePersistencePort', async () => {
  const pack = createPack();
  const storage = createStorage();
  const source = createRuntimePersistence(pack);
  const gateway = createProductionPersistenceGateway({ storage, runtimePersistence: source });
  await source.live().kernel.context.dispatch({ type: 'mark', actionOccurrenceId: 'persistence:mark', payload: { status: 'saved' } });

  const saved = gateway.save();
  assert.equal(saved.status, 'saved');
  assert.equal(saved.snapshot.snapshotSchemaVersion, 2);
  assert.equal(saved.snapshot.campaignLock.campaignFingerprint, CAMPAIGN_FINGERPRINT);
  assert.ok(storage.getItem(PRODUCTION_SAVE_KEY));

  const target = createRuntimePersistence(pack);
  const restoringGateway = createProductionPersistenceGateway({ storage, runtimePersistence: target });
  const restored = await restoringGateway.restore();
  assert.deepEqual(restored, { status: 'restored' });
  assert.equal(target.restoreCalls(), 1);
  assert.deepEqual(target.live().kernel.context.select((state) => state.investigation), { status: 'saved' });
});

test('campaign mismatch is reset-required and does not call the live RuntimePersistencePort restore', async () => {
  const storage = createStorage();
  const sourcePack = createPack();
  const source = createRuntimePersistence(sourcePack);
  createProductionPersistenceGateway({ storage, runtimePersistence: source }).save();

  const target = createRuntimePersistence(createPack('c'.repeat(64)));
  const result = await createProductionPersistenceGateway({ storage, runtimePersistence: target }).restore();

  assert.equal(result.status, 'reset-required');
  assert.equal(result.reason, 'CampaignMismatch');
  assert.equal(result.reset.action, 'reset-current-v2-save');
  assert.equal(result.reset.requiresExplicitConfirmation, true);
  assert.equal(target.restoreCalls(), 0, 'a rejected snapshot must never replace the incumbent live run');
  assert.deepEqual(target.live().kernel.context.select((state) => state.investigation), { status: 'stable' });
});

test('malformed stored JSON is reset-required rather than a partial or best-effort load', () => {
  const storage = createStorage([[PRODUCTION_SAVE_KEY, '{not valid json']]);
  const result = createProductionPersistenceGateway({ storage, runtimePersistence: unusedRuntimePersistence() }).load();

  assert.equal(result.status, 'reset-required');
  assert.equal(result.reason, 'SnapshotIntegrityError');
  assert.equal(result.reset.requiresExplicitConfirmation, true);
});

test('legacy reset removes exactly the audited v1 keys, writes its marker only after success and never crosses Lab or unknown storage', () => {
  const storage = createStorage([
    ...LEGACY_GAMEPLAY_STORAGE_KEYS.map((key) => [key, 'retired']),
    ['urman_username', 'Aidar'],
    ['urman.content-lab.v1', 'isolated'],
    ['unrelated.origin.key', 'keep'],
    [PRODUCTION_SAVE_KEY, 'keep-current-v2'],
  ]);
  const gateway = createProductionPersistenceGateway({ storage, runtimePersistence: unusedRuntimePersistence() });

  const applied = gateway.applyLegacyReset();
  assert.deepEqual(applied, { status: 'reset-applied', notice: LEGACY_RESET_NOTICE });
  assert.deepEqual(storage.removed, [...LEGACY_GAMEPLAY_STORAGE_KEYS]);
  assert.equal(storage.getItem(LEGACY_RESET_MARKER_KEY), LEGACY_RESET_MARKER_VALUE);
  assert.equal(storage.getItem('urman_username'), 'Aidar');
  assert.equal(storage.getItem('urman.content-lab.v1'), 'isolated');
  assert.equal(storage.getItem('unrelated.origin.key'), 'keep');
  assert.equal(storage.getItem(PRODUCTION_SAVE_KEY), 'keep-current-v2');

  const repeated = gateway.applyLegacyReset();
  assert.deepEqual(repeated, { status: 'already-reset', notice: null });
  assert.deepEqual(storage.removed, [...LEGACY_GAMEPLAY_STORAGE_KEYS]);

  const failingStorage = createStorage(LEGACY_GAMEPLAY_STORAGE_KEYS.map((key) => [key, 'retired']));
  const remove = failingStorage.removeItem;
  failingStorage.removeItem = (key) => {
    if (key === 'urman.oldPcHub.state.v1') throw new Error('storage quota failure');
    remove(key);
  };
  const failed = createProductionPersistenceGateway({ storage: failingStorage, runtimePersistence: unusedRuntimePersistence() }).applyLegacyReset();
  assert.equal(failed.status, 'reset-failed');
  assert.equal(failingStorage.getItem(LEGACY_RESET_MARKER_KEY), null);
});

test('current V2 discard requires one pending mismatch proposal, consumes it on success and preserves unrelated keys', async () => {
  const storage = createStorage([
    [PRODUCTION_SAVE_KEY, 'rejected'],
    [LEGACY_RESET_MARKER_KEY, LEGACY_RESET_MARKER_VALUE],
    ['urman_username', 'Aidar'],
    ['urman.content-lab.v1', 'isolated'],
    ['unrelated.origin.key', 'keep'],
  ]);
  const noProposalGateway = createProductionPersistenceGateway({ storage, runtimePersistence: unusedRuntimePersistence() });
  assert.deepEqual(noProposalGateway.discardCurrentV2Save(undefined), {
    status: 'current-v2-save-discard-rejected',
    message: 'Current V2 save discard requires the pending reset proposal.',
  });
  assert.equal(storage.getItem(PRODUCTION_SAVE_KEY), 'rejected');

  const sourcePack = createPack();
  const source = createRuntimePersistence(sourcePack);
  const proposalStorage = createStorage();
  createProductionPersistenceGateway({ storage: proposalStorage, runtimePersistence: source }).save();
  proposalStorage.setItem(LEGACY_RESET_MARKER_KEY, LEGACY_RESET_MARKER_VALUE);
  proposalStorage.setItem('urman_username', 'Aidar');
  proposalStorage.setItem('urman.content-lab.v1', 'isolated');
  proposalStorage.setItem('unrelated.origin.key', 'keep');
  const target = createRuntimePersistence(createPack('c'.repeat(64)));
  const gateway = createProductionPersistenceGateway({ storage: proposalStorage, runtimePersistence: target });
  const mismatch = await gateway.restore();
  assert.equal(mismatch.status, 'reset-required');

  const result = gateway.discardCurrentV2Save(mismatch.reset);
  assert.deepEqual(result, { status: 'current-v2-save-discarded' });
  assert.deepEqual(proposalStorage.removed, [PRODUCTION_SAVE_KEY]);
  assert.equal(proposalStorage.getItem(LEGACY_RESET_MARKER_KEY), LEGACY_RESET_MARKER_VALUE);
  assert.equal(proposalStorage.getItem('urman_username'), 'Aidar');
  assert.equal(proposalStorage.getItem('urman.content-lab.v1'), 'isolated');
  assert.equal(proposalStorage.getItem('unrelated.origin.key'), 'keep');
  assert.deepEqual(gateway.discardCurrentV2Save(mismatch.reset), {
    status: 'current-v2-save-discard-rejected',
    message: 'Current V2 save discard requires the pending reset proposal.',
  });
});

test('a failed authorized V2 discard retains its pending proposal for one retry', async () => {
  const sourcePack = createPack();
  const storage = createStorage();
  createProductionPersistenceGateway({ storage, runtimePersistence: createRuntimePersistence(sourcePack) }).save();
  const gateway = createProductionPersistenceGateway({
    storage,
    runtimePersistence: createRuntimePersistence(createPack('c'.repeat(64))),
  });
  const mismatch = await gateway.restore();
  assert.equal(mismatch.status, 'reset-required');

  const removeItem = storage.removeItem;
  storage.removeItem = () => { throw new Error('simulated storage failure'); };
  assert.deepEqual(gateway.discardCurrentV2Save(mismatch.reset), {
    status: 'current-v2-save-discard-failed',
    message: 'simulated storage failure',
  });
  assert.ok(storage.getItem(PRODUCTION_SAVE_KEY), 'a failed discard leaves the current save intact');

  storage.removeItem = removeItem;
  assert.deepEqual(gateway.discardCurrentV2Save(mismatch.reset), { status: 'current-v2-save-discarded' });
  assert.equal(storage.getItem(PRODUCTION_SAVE_KEY), null);
});

test('static guard allows injected storage only in the named gateway and rejects every other production source', async () => {
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'urman-storage-gate-'));
  try {
    await mkdir(path.join(temporaryRoot, 'src/runtime/persistence'), { recursive: true });
    await mkdir(path.join(temporaryRoot, 'src/game'), { recursive: true });
    await writeFile(path.join(temporaryRoot, 'src/runtime/persistence/production-persistence-gateway.mjs'), 'export const storage = browserWindow.localStorage;\n');
    await writeFile(path.join(temporaryRoot, 'src/game/Game.ts'), 'export const unsafe = localStorage;\n');
    await writeFile(path.join(temporaryRoot, 'src/game/Other.ts'), "import { createBrowserProductionPersistenceGateway } from '../runtime/persistence/production-persistence-gateway.mjs';\nexport const unsafeFactory = createBrowserProductionPersistenceGateway;\n");
    await writeFile(path.join(temporaryRoot, 'src/main.ts'), "import { createBrowserProductionPersistenceGateway } from './runtime/persistence/production-persistence-gateway.mjs';\nexport const allowedFactory = createBrowserProductionPersistenceGateway;\n");
    const result = await checkRuntimeArchitecture({ cwd: temporaryRoot, requireRetiredOwnersAbsent: false });
    assert.equal(result.ok, false);
    assert.deepEqual(result.diagnostics.map((entry) => [entry.rule, entry.sourcePath]), [
      ['RawLocalStorage', 'src/game/Game.ts'],
      ['BrowserPersistenceFactoryCaller', 'src/game/Other.ts'],
    ]);
  } finally {
    await rm(temporaryRoot, { recursive: true, force: true });
  }
});

test('retired persistence and regex-validator paths have no source, package or production caller', async () => {
  const retiredPaths = [
    'src/systems/SaveSystem.ts',
    'src/systems/VocabularySystem.ts',
    'src/ui/NotebookUI.ts',
    'scripts/validate-old-pc-content.mjs',
    'scripts/validate-route-graph.mjs',
    'scripts/validate-clue-graph.mjs',
    'scripts/validate-content.mjs',
  ];
  for (const relativePath of retiredPaths) {
    assert.equal(existsSync(path.join(ROOT, relativePath)), false, `${relativePath} must remain retired`);
  }

  const packageJson = JSON.parse(await readFile(path.join(ROOT, 'package.json'), 'utf8'));
  for (const script of ['validate:old-pc', 'validate:route-graph', 'validate:clue-graph', 'validate:content']) {
    assert.equal(Object.hasOwn(packageJson.scripts, script), false, `${script} must not retain a legacy caller`);
  }
  assert.equal(typeof packageJson.scripts['content:check'], 'string');

  const sourcePaths = [
    'src/os/apps/oldPcHub.ts',
    'src/os/apps/notepad.ts',
  ];
  for (const relativePath of sourcePaths) {
    const source = await readFile(path.join(ROOT, relativePath), 'utf8');
    assert.doesNotMatch(source, /\blocalStorage\b|\bwindow\.URMAN\b/, `${relativePath} must not bypass the gateway`);
  }
  const gatewaySource = await readFile(path.join(ROOT, 'src/runtime/persistence/production-persistence-gateway.mjs'), 'utf8');
  assert.match(gatewaySource, /browserWindow\.localStorage/, 'the injected gateway remains the single browser storage boundary');
  assert.doesNotMatch(gatewaySource, /\bwindow\.URMAN\b/, 'the gateway cannot expose a gameplay global');

  const gameSource = await readFile(path.join(ROOT, 'src/game/Game.ts'), 'utf8');
  assert.match(gameSource, /import type \{ RuntimePresentationPort, RuntimeSceneSession \}/, 'Game receives only the closed presentation facade');
  assert.doesNotMatch(gameSource, /\bRuntimeBootstrap\b|runtime\.(?:runtimeContext|contentRegistry|capabilityHost|persistence)\b|virtual:urman-content-catalog|createBrowserProductionPersistenceGateway/, 'Game cannot access bootstrap, capability or browser composition internals');
});
