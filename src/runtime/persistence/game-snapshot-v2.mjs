import { CapabilityHost } from '../capabilities/capability-host.mjs';
import { clonePersistedJsonValue } from '../contracts/json-value.mjs';
import { RuntimeKernel } from '../kernel/runtime-kernel.mjs';
import { CapabilityRegistry } from '../registries/runtime-registries.mjs';
import { DeterministicScheduler } from '../world/deterministic-scheduler.mjs';
import { LogicalClock } from '../world/logical-clock.mjs';
import { OwnerRngStreams } from '../world/seeded-rng.mjs';
import { assertCampaignLockMatches, createCampaignLock, normalizeCampaignLock } from './campaign-lock.mjs';
import {
  CapabilitySnapshotIncompatible,
  SnapshotCodecError,
  SnapshotIntegrityError,
  UnsupportedSnapshotVersion,
} from './snapshot-errors.mjs';

export const GAME_SNAPSHOT_V2_SCHEMA_VERSION = 2;

const EXACT_VERSION = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
const STAGING_CLEANUP_COMMAND = 'runtime.snapshot.staging-cleanup';
const KERNEL_VALIDATION_PACK = Object.freeze({
  campaign: Object.freeze({ capabilityRequirements: Object.freeze([]) }),
  registries: Object.freeze({ capabilities: Object.freeze([]) }),
});
const KERNEL_VALIDATION_REGISTRY = new CapabilityRegistry(KERNEL_VALIDATION_PACK);

function errorMessage(error) {
  return error instanceof Error ? error.message : String(error);
}

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new SnapshotIntegrityError(`${label} must be a non-empty string.`);
  return value;
}

function exactVersion(value, label) {
  if (typeof value !== 'string' || !EXACT_VERSION.test(value)) throw new SnapshotIntegrityError(`${label} must be an exact version.`);
  return value;
}

function exactObject(value, label, keys) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new SnapshotIntegrityError(`${label} must be an object.`);
  const actual = Object.keys(value).sort();
  const expected = [...keys].sort();
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    throw new SnapshotIntegrityError(`${label} has an invalid shape.`);
  }
  return value;
}

function safeSequence(value, label) {
  if (!Number.isSafeInteger(value) || value < 0) throw new SnapshotIntegrityError(`${label} must be a safe non-negative integer.`);
  return value;
}

function jsonClone(value, label) {
  try {
    return clonePersistedJsonValue(value, label);
  } catch (error) {
    throw new SnapshotIntegrityError(errorMessage(error));
  }
}

function normalizeClaim(claim, label) {
  exactObject(claim, label, ['resourceId', 'mode', 'lifecycleScope']);
  if (!['exclusive', 'shared'].includes(claim.mode)) throw new SnapshotIntegrityError(`${label}.mode is invalid.`);
  return Object.freeze({
    resourceId: nonEmptyString(claim.resourceId, `${label}.resourceId`),
    mode: claim.mode,
    lifecycleScope: nonEmptyString(claim.lifecycleScope, `${label}.lifecycleScope`),
  });
}

function normalizeBinding(binding, index) {
  const label = `GameSnapshotV2.capabilityBindings[${index}]`;
  exactObject(binding, label, ['capabilityInstanceId', 'protocolId', 'exactVersion', 'config', 'claims']);
  if (!Array.isArray(binding.claims)) throw new SnapshotIntegrityError(`${label}.claims must be an array.`);
  const claims = binding.claims.map((claim, claimIndex) => normalizeClaim(claim, `${label}.claims[${claimIndex}]`));
  const resources = new Set();
  for (const claim of claims) {
    if (resources.has(claim.resourceId)) throw new SnapshotIntegrityError(`${label}.claims has duplicate resource ${claim.resourceId}.`);
    resources.add(claim.resourceId);
  }
  return Object.freeze({
    capabilityInstanceId: nonEmptyString(binding.capabilityInstanceId, `${label}.capabilityInstanceId`),
    protocolId: nonEmptyString(binding.protocolId, `${label}.protocolId`),
    exactVersion: exactVersion(binding.exactVersion, `${label}.exactVersion`),
    config: jsonClone(binding.config, `${label}.config`),
    claims: Object.freeze(claims),
  });
}

function normalizeCapabilitySnapshot(snapshot, index) {
  const label = `GameSnapshotV2.capabilitySnapshots[${index}]`;
  exactObject(snapshot, label, ['capabilityInstanceId', 'protocolId', 'exactVersion', 'stateSchemaVersion', 'state']);
  if (!Number.isSafeInteger(snapshot.stateSchemaVersion) || snapshot.stateSchemaVersion < 1) {
    throw new SnapshotIntegrityError(`${label}.stateSchemaVersion must be a positive safe integer.`);
  }
  return Object.freeze({
    capabilityInstanceId: nonEmptyString(snapshot.capabilityInstanceId, `${label}.capabilityInstanceId`),
    protocolId: nonEmptyString(snapshot.protocolId, `${label}.protocolId`),
    exactVersion: exactVersion(snapshot.exactVersion, `${label}.exactVersion`),
    stateSchemaVersion: snapshot.stateSchemaVersion,
    state: jsonClone(snapshot.state, `${label}.state`),
  });
}

function uniqueBy(entries, field, label) {
  const seen = new Set();
  for (const entry of entries) {
    if (seen.has(entry[field])) throw new SnapshotIntegrityError(`${label} has duplicate ${field} ${entry[field]}.`);
    seen.add(entry[field]);
  }
}

function normalizeRuntimeSnapshot(snapshot) {
  exactObject(snapshot, 'GameSnapshotV2.runtime', ['state', 'claims', 'occurrences', 'eventSequence']);
  if (!snapshot.state || typeof snapshot.state !== 'object' || Array.isArray(snapshot.state)) {
    throw new SnapshotIntegrityError('GameSnapshotV2.runtime.state must be a JSON object.');
  }
  if (!Array.isArray(snapshot.claims) || !Array.isArray(snapshot.occurrences)) {
    throw new SnapshotIntegrityError('GameSnapshotV2.runtime claims and occurrences must be arrays.');
  }
  safeSequence(snapshot.eventSequence, 'GameSnapshotV2.runtime.eventSequence');
  const normalized = jsonClone(snapshot, 'GameSnapshotV2.runtime');
  // RuntimeKernel owns the detailed ledger and resource-claim shape. Build a
  // throwaway instance so decode validates that complete nested contract before
  // campaign or provider compatibility decides whether a run may be staged.
  try {
    RuntimeKernel.fromSnapshot(normalized, { capabilityRegistry: KERNEL_VALIDATION_REGISTRY });
  } catch (error) {
    throw new SnapshotIntegrityError(`GameSnapshotV2.runtime is invalid: ${errorMessage(error)}`);
  }
  return normalized;
}

function normalizeWorldSnapshot(snapshot) {
  exactObject(snapshot, 'GameSnapshotV2.world', ['clock', 'rngStreams', 'scheduler']);
  // OwnerRngStreams intentionally accepts a forward-compatible input shape
  // for its standalone API. A saved run has no such compatibility policy:
  // every persisted member must be known before staging begins.
  exactObject(snapshot.rngStreams, 'GameSnapshotV2.world.rngStreams', ['masterSeed', 'streams']);
  try {
    LogicalClock.fromSnapshot(snapshot.clock);
    OwnerRngStreams.fromSnapshot(snapshot.rngStreams);
    DeterministicScheduler.fromSnapshot(snapshot.scheduler);
  } catch (error) {
    throw new SnapshotIntegrityError(`GameSnapshotV2.world is invalid: ${errorMessage(error)}`);
  }
  return jsonClone(snapshot, 'GameSnapshotV2.world');
}

function descriptorCatalog(descriptors) {
  if (!Array.isArray(descriptors)) throw new SnapshotIntegrityError('Capability descriptor catalog must be an array.');
  const byProtocol = new Map();
  for (const [index, descriptor] of descriptors.entries()) {
    if (!descriptor || typeof descriptor !== 'object' || Array.isArray(descriptor)) {
      throw new SnapshotIntegrityError(`Capability descriptor ${index} must be an object.`);
    }
    const protocol = Object.getOwnPropertyDescriptor(descriptor, 'protocolId');
    const version = Object.getOwnPropertyDescriptor(descriptor, 'exactVersion');
    const schema = Object.getOwnPropertyDescriptor(descriptor, 'stateSchemaVersion');
    const validateConfig = Object.getOwnPropertyDescriptor(descriptor, 'validateConfig');
    const create = Object.getOwnPropertyDescriptor(descriptor, 'create');
    if (!protocol || !Object.hasOwn(protocol, 'value') || !version || !Object.hasOwn(version, 'value') || !schema || !Object.hasOwn(schema, 'value')
      || !validateConfig || !Object.hasOwn(validateConfig, 'value') || !create || !Object.hasOwn(create, 'value')
      || typeof validateConfig.value !== 'function' || typeof create.value !== 'function') {
      throw new SnapshotIntegrityError(`Capability descriptor ${index} must expose own data lifecycle fields.`);
    }
    const normalized = Object.freeze({
      protocolId: nonEmptyString(protocol.value, `Capability descriptor ${index}.protocolId`),
      exactVersion: exactVersion(version.value, `Capability descriptor ${index}.exactVersion`),
      stateSchemaVersion: schema.value,
    });
    if (!Number.isSafeInteger(normalized.stateSchemaVersion) || normalized.stateSchemaVersion < 1) {
      throw new SnapshotIntegrityError(`Capability descriptor ${index}.stateSchemaVersion must be a positive safe integer.`);
    }
    if (byProtocol.has(normalized.protocolId)) throw new SnapshotIntegrityError(`Capability descriptor catalog has duplicate protocol ${normalized.protocolId}.`);
    byProtocol.set(normalized.protocolId, normalized);
  }
  return byProtocol;
}

function validateCapabilityCompatibility(snapshot, descriptors) {
  const descriptorByProtocol = descriptorCatalog(descriptors);
  const required = new Map(snapshot.campaignLock.capabilities.map((entry) => [entry.protocolId, entry.exactVersion]));
  const bindingsById = new Map(snapshot.capabilityBindings.map((entry) => [entry.capabilityInstanceId, entry]));
  const snapshotsById = new Map(snapshot.capabilitySnapshots.map((entry) => [entry.capabilityInstanceId, entry]));
  for (const [instanceId, binding] of bindingsById) {
    const saved = snapshotsById.get(instanceId);
    if (!saved) throw new CapabilitySnapshotIncompatible(instanceId, 'a binding has no state snapshot');
    if (binding.protocolId !== saved.protocolId || binding.exactVersion !== saved.exactVersion) {
      throw new CapabilitySnapshotIncompatible(instanceId, 'binding protocol identity differs from state snapshot');
    }
    const requiredVersion = required.get(binding.protocolId);
    if (requiredVersion !== binding.exactVersion) {
      throw new CapabilitySnapshotIncompatible(instanceId, 'protocol is not an exact campaign requirement', {
        protocolId: binding.protocolId,
        exactVersion: binding.exactVersion,
      });
    }
    const descriptor = descriptorByProtocol.get(binding.protocolId);
    if (!descriptor) throw new CapabilitySnapshotIncompatible(instanceId, 'host provider is missing', { protocolId: binding.protocolId });
    if (descriptor.exactVersion !== binding.exactVersion) {
      throw new CapabilitySnapshotIncompatible(instanceId, 'host provider exact version differs', {
        required: binding.exactVersion,
        registered: descriptor.exactVersion,
      });
    }
    if (descriptor.stateSchemaVersion !== saved.stateSchemaVersion) {
      throw new CapabilitySnapshotIncompatible(instanceId, 'state schema version differs', {
        required: saved.stateSchemaVersion,
        registered: descriptor.stateSchemaVersion,
      });
    }
  }
  for (const instanceId of snapshotsById.keys()) {
    if (!bindingsById.has(instanceId)) throw new CapabilitySnapshotIncompatible(instanceId, 'state snapshot has no binding');
  }
}

function parseSnapshot(snapshot, { pack, capabilityDescriptors = [] } = {}) {
  const source = jsonClone(snapshot, 'GameSnapshotV2');
  exactObject(source, 'GameSnapshotV2', [
    'snapshotSchemaVersion',
    'savedSequence',
    'campaignLock',
    'runtime',
    'world',
    'capabilityBindings',
    'capabilitySnapshots',
  ]);
  if (!Number.isSafeInteger(source.snapshotSchemaVersion) || source.snapshotSchemaVersion < 1) {
    throw new SnapshotIntegrityError('GameSnapshotV2.snapshotSchemaVersion must be a positive safe integer.');
  }
  if (source.snapshotSchemaVersion !== GAME_SNAPSHOT_V2_SCHEMA_VERSION) {
    throw new UnsupportedSnapshotVersion(source.snapshotSchemaVersion);
  }
  safeSequence(source.savedSequence, 'GameSnapshotV2.savedSequence');
  const runtime = normalizeRuntimeSnapshot(source.runtime);
  if (source.savedSequence !== runtime.eventSequence) {
    throw new SnapshotIntegrityError('GameSnapshotV2.savedSequence must equal runtime.eventSequence.');
  }
  if (!Array.isArray(source.capabilityBindings) || !Array.isArray(source.capabilitySnapshots)) {
    throw new SnapshotIntegrityError('GameSnapshotV2 capability bindings and snapshots must be arrays.');
  }
  const parsed = Object.freeze({
    snapshotSchemaVersion: GAME_SNAPSHOT_V2_SCHEMA_VERSION,
    savedSequence: source.savedSequence,
    campaignLock: normalizeCampaignLock(source.campaignLock),
    runtime,
    world: normalizeWorldSnapshot(source.world),
    capabilityBindings: Object.freeze(source.capabilityBindings.map(normalizeBinding)),
    capabilitySnapshots: Object.freeze(source.capabilitySnapshots.map(normalizeCapabilitySnapshot)),
  });
  uniqueBy(parsed.capabilityBindings, 'capabilityInstanceId', 'GameSnapshotV2.capabilityBindings');
  uniqueBy(parsed.capabilitySnapshots, 'capabilityInstanceId', 'GameSnapshotV2.capabilitySnapshots');
  assertCampaignLockMatches(parsed.campaignLock, pack);
  validateCapabilityCompatibility(parsed, capabilityDescriptors);
  return parsed;
}

/** Strict, JSON-only decode. It never receives or mutates a live run. */
export function decodeGameSnapshotV2(snapshot, options = {}) {
  return parseSnapshot(snapshot, options);
}

export function tryDecodeGameSnapshotV2(snapshot, options = {}) {
  try {
    return Object.freeze({ ok: true, snapshot: decodeGameSnapshotV2(snapshot, options) });
  } catch (error) {
    if (error instanceof SnapshotCodecError) return Object.freeze({ ok: false, error });
    throw error;
  }
}

/**
 * Captures the one kernel-owned domain state, occurrence ledger and claims
 * together with deterministic world services. Quest state, custody, evidence,
 * checkpoints and generated bindings are all state owned by that kernel.
 */
export function encodeGameSnapshotV2({
  pack,
  kernel,
  clock,
  rngStreams,
  scheduler,
  capabilityBindings = [],
  capabilitySnapshots = [],
  capabilityDescriptors = [],
} = {}) {
  if (!kernel || typeof kernel.exportSnapshot !== 'function') throw new SnapshotIntegrityError('GameSnapshotV2 encoding requires a RuntimeKernel.');
  if (!clock || typeof clock.exportSnapshot !== 'function') throw new SnapshotIntegrityError('GameSnapshotV2 encoding requires a logical clock.');
  if (!rngStreams || typeof rngStreams.exportSnapshot !== 'function') throw new SnapshotIntegrityError('GameSnapshotV2 encoding requires owner RNG streams.');
  if (!scheduler || typeof scheduler.exportSnapshot !== 'function') throw new SnapshotIntegrityError('GameSnapshotV2 encoding requires a deterministic scheduler.');
  if (!Array.isArray(capabilityBindings) || !Array.isArray(capabilitySnapshots)) {
    throw new SnapshotIntegrityError('GameSnapshotV2 capability bindings and snapshots must be arrays.');
  }
  const runtime = jsonClone(kernel.exportSnapshot(), 'Runtime kernel snapshot');
  const snapshot = Object.freeze({
    snapshotSchemaVersion: GAME_SNAPSHOT_V2_SCHEMA_VERSION,
    savedSequence: runtime.eventSequence,
    campaignLock: createCampaignLock(pack),
    runtime,
    world: Object.freeze({
      clock: jsonClone(clock.exportSnapshot(), 'Logical clock snapshot'),
      rngStreams: jsonClone(rngStreams.exportSnapshot(), 'RNG streams snapshot'),
      scheduler: jsonClone(scheduler.exportSnapshot(), 'Scheduler snapshot'),
    }),
    capabilityBindings: jsonClone(capabilityBindings, 'Capability bindings'),
    capabilitySnapshots: jsonClone(capabilitySnapshots, 'Capability snapshots'),
  });
  return parseSnapshot(snapshot, { pack, capabilityDescriptors });
}

function withStagingCleanupHandler(handlers = {}) {
  const entries = handlers instanceof Map ? [...handlers.entries()] : Object.entries(handlers);
  if (entries.some(([type]) => type === STAGING_CLEANUP_COMMAND)) {
    throw new SnapshotIntegrityError(`Runtime handlers reserve ${STAGING_CLEANUP_COMMAND}.`);
  }
  entries.push([STAGING_CLEANUP_COMMAND, (command) => {
    const owners = command.payload?.ownerIds;
    if (!Array.isArray(owners) || owners.some((ownerId) => typeof ownerId !== 'string' || !ownerId)) {
      throw new TypeError('Staging cleanup owners are invalid.');
    }
    return { releaseClaimOwnerIds: owners };
  }]);
  return new Map(entries);
}

function uniqueRollbackOccurrenceId(runtimeSnapshot) {
  const used = new Set(runtimeSnapshot.occurrences.map(({ actionOccurrenceId }) => actionOccurrenceId));
  let suffix = 0;
  while (used.has(`runtime-snapshot-rollback:${suffix}`)) suffix += 1;
  return `runtime-snapshot-rollback:${suffix}`;
}

async function disposeStage(host, kernel, bindings, runtimeSnapshot) {
  if (!host || !kernel || bindings.length === 0) return;
  const ownerIds = bindings.map(({ capabilityInstanceId }) => capabilityInstanceId).sort();
  try {
    await kernel.context.dispatch({
      type: STAGING_CLEANUP_COMMAND,
      actionOccurrenceId: uniqueRollbackOccurrenceId(runtimeSnapshot),
      payload: { ownerIds },
    });
  } catch {
    // The stage is discarded regardless; still attempt every provider cleanup.
  }
  for (const ownerId of [...ownerIds].reverse()) {
    try { host.disposeSession(ownerId); } catch { /* cleanup is best-effort only on an abandoned stage */ }
  }
}

/**
 * Builds an entirely new run. Validation, campaign locking and capability
 * compatibility happen before an old run could be swapped by composition code.
 * Every capability is restored before any capability receives start().
 */
export async function restoreGameSnapshotV2(snapshot, {
  pack,
  handlers = {},
  capabilityRegistry,
  capabilityDescriptors = [],
  startCapabilities = true,
} = {}) {
  const parsed = decodeGameSnapshotV2(snapshot, { pack, capabilityDescriptors });
  let stagedKernel;
  let stagedHost;
  let stage = 'runtime';
  let currentCapabilityId = null;
  const createdBindings = [];
  try {
    stagedKernel = RuntimeKernel.fromSnapshot(parsed.runtime, {
      handlers: withStagingCleanupHandler(handlers),
      capabilityRegistry,
    });
    stage = 'world';
    const clock = LogicalClock.fromSnapshot(parsed.world.clock);
    const rngStreams = OwnerRngStreams.fromSnapshot(parsed.world.rngStreams);
    const scheduler = DeterministicScheduler.fromSnapshot(parsed.world.scheduler);
    stage = 'capability';
    stagedHost = new CapabilityHost({
      runtimeContext: stagedKernel.context,
      clock,
      rngStreams,
      scheduler,
      descriptors: capabilityDescriptors,
    });
    const snapshotsById = new Map(parsed.capabilitySnapshots.map((entry) => [entry.capabilityInstanceId, entry]));
    for (const binding of parsed.capabilityBindings) {
      currentCapabilityId = binding.capabilityInstanceId;
      stagedHost.createSession({ ...binding, snapshot: snapshotsById.get(binding.capabilityInstanceId) });
      createdBindings.push(binding);
    }
    if (startCapabilities !== false) {
      for (const binding of createdBindings) {
        currentCapabilityId = binding.capabilityInstanceId;
        stagedHost.startSession(binding.capabilityInstanceId);
      }
    }
    return Object.freeze({
      campaignLock: parsed.campaignLock,
      kernel: stagedKernel,
      clock,
      rngStreams,
      scheduler,
      capabilityHost: stagedHost,
      capabilityInstanceIds: Object.freeze(createdBindings.map(({ capabilityInstanceId }) => capabilityInstanceId)),
    });
  } catch (error) {
    await disposeStage(stagedHost, stagedKernel, createdBindings, parsed.runtime);
    if (error instanceof SnapshotCodecError) throw error;
    if (stage === 'capability') {
      throw new CapabilitySnapshotIncompatible(currentCapabilityId ?? 'capability-host', errorMessage(error));
    }
    throw new SnapshotIntegrityError(`GameSnapshotV2 ${stage} restore failed: ${errorMessage(error)}`);
  }
}
