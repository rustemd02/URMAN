import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

const EXACT_VERSION = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function exactVersion(value, label) {
  if (typeof value !== 'string' || !EXACT_VERSION.test(value)) throw new TypeError(`${label} must be an exact version.`);
  return value;
}

function ownDataFields(value, fieldNames, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new TypeError(`${label} must be an object.`);
  const descriptors = Object.getOwnPropertyDescriptors(value);
  const result = {};
  for (const fieldName of fieldNames) {
    const descriptor = descriptors[fieldName];
    if (!descriptor || !Object.hasOwn(descriptor, 'value')) throw new TypeError(`${label}.${fieldName} must be an own data property.`);
    result[fieldName] = descriptor.value;
  }
  return result;
}

function normalizeClaims(claims, ownerId) {
  if (claims === undefined) return Object.freeze([]);
  if (!Array.isArray(claims)) throw new TypeError('Capability claims must be an array.');
  const resourceIds = new Set();
  return Object.freeze(claims.map((claim, index) => {
    const fields = ownDataFields(claim, ['resourceId', 'mode', 'lifecycleScope'], `Capability claim ${index}`);
    if (typeof fields.resourceId !== 'string' || !fields.resourceId
      || !['exclusive', 'shared'].includes(fields.mode)
      || typeof fields.lifecycleScope !== 'string' || !fields.lifecycleScope) {
      throw new TypeError(`Capability claim ${index} is invalid.`);
    }
    if (resourceIds.has(fields.resourceId)) throw new TypeError(`Duplicate capability claim ${fields.resourceId}.`);
    resourceIds.add(fields.resourceId);
    return Object.freeze({ ...fields, ownerId });
  }));
}

class CleanupRegistry {
  #disposed = false;
  #disposers = [];

  add(disposer) {
    if (typeof disposer !== 'function') throw new TypeError('Capability cleanup must be a function.');
    if (this.#disposed) {
      disposer();
      return Object.freeze({ dispose() {} });
    }
    let disposed = false;
    const guarded = () => {
      if (disposed) return;
      disposed = true;
      disposer();
    };
    this.#disposers.push(guarded);
    return Object.freeze({ dispose: guarded });
  }

  dispose() {
    if (this.#disposed) return;
    this.#disposed = true;
    const errors = [];
    for (const disposer of [...this.#disposers].reverse()) {
      try { disposer(); } catch (error) { errors.push(error); }
    }
    this.#disposers = [];
    if (errors.length) throw errors[0];
  }
}

function normalizedDescriptor(descriptor) {
  const fields = ownDataFields(descriptor, ['protocolId', 'exactVersion', 'stateSchemaVersion', 'validateConfig', 'create'], 'Capability descriptor');
  if (typeof fields.protocolId !== 'string' || !fields.protocolId
    || !EXACT_VERSION.test(fields.exactVersion)
    || !Number.isSafeInteger(fields.stateSchemaVersion) || fields.stateSchemaVersion < 1
    || typeof fields.validateConfig !== 'function'
    || typeof fields.create !== 'function') {
    throw new TypeError('Capability descriptor is invalid.');
  }
  return Object.freeze({ ...fields });
}

function exactSnapshot(snapshot, instanceId, descriptor) {
  if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot)
    || Object.keys(snapshot).sort().join(',') !== 'capabilityInstanceId,exactVersion,protocolId,state,stateSchemaVersion') {
    throw new TypeError('Capability snapshot has an invalid shape.');
  }
  if (snapshot.capabilityInstanceId !== instanceId
    || snapshot.protocolId !== descriptor.protocolId
    || snapshot.exactVersion !== descriptor.exactVersion
    || snapshot.stateSchemaVersion !== descriptor.stateSchemaVersion) {
    throw new TypeError(`Capability snapshot is incompatible for ${instanceId}.`);
  }
  return clonePersistedJsonValue(snapshot.state, `Capability ${instanceId} snapshot state`);
}

function presentationMethods(session, instanceId) {
  const descriptors = Object.getOwnPropertyDescriptors(session);
  const render = descriptors.render;
  const subscribe = descriptors.subscribe;
  if (!render || !Object.hasOwn(render, 'value') || typeof render.value !== 'function'
    || !subscribe || !Object.hasOwn(subscribe, 'value') || typeof subscribe.value !== 'function') {
    throw new TypeError(`Capability ${instanceId} does not implement the presentation port.`);
  }
  return Object.freeze({ render: render.value, subscribe: subscribe.value });
}

function presentationSubscription(value, instanceId) {
  if (!value || typeof value !== 'object' || typeof value.dispose !== 'function') {
    throw new TypeError(`Capability ${instanceId} presentation subscribe must return a disposable subscription.`);
  }
  return value;
}

/**
 * Provider lifecycle coordinator. It has no state writer: providers receive a
 * limited RuntimeContext and return their effects through ordinary commands.
 */
export class CapabilityHost {
  #runtime;
  #clock;
  #rngStreams;
  #scheduler;
  #descriptors = new Map();
  #sessions = new Map();

  constructor({ runtimeContext, clock, rngStreams, scheduler, descriptors = [] } = {}) {
    if (!runtimeContext || typeof runtimeContext !== 'object' || typeof runtimeContext.select !== 'function' || typeof runtimeContext.query !== 'function'
      || typeof runtimeContext.dispatch !== 'function' || typeof runtimeContext.subscribe !== 'function'
      || !runtimeContext.capabilities || typeof runtimeContext.capabilities.require !== 'function') {
      throw new TypeError('CapabilityHost requires a RuntimeContext.');
    }
    if (!clock || typeof clock.now !== 'function') throw new TypeError('CapabilityHost requires a logical clock.');
    if (!rngStreams || typeof rngStreams.stream !== 'function') throw new TypeError('CapabilityHost requires owner RNG streams.');
    if (!scheduler || typeof scheduler.schedule !== 'function' || typeof scheduler.cancelOwner !== 'function'
      || typeof scheduler.claimDueForOwner !== 'function' || typeof scheduler.acknowledge !== 'function' || typeof scheduler.release !== 'function') {
      throw new TypeError('CapabilityHost requires a deterministic scheduler.');
    }
    this.#runtime = runtimeContext;
    this.#clock = clock;
    this.#rngStreams = rngStreams;
    this.#scheduler = scheduler;
    for (const descriptor of descriptors) this.register(descriptor);
  }

  register(descriptor) {
    const normalized = normalizedDescriptor(descriptor);
    if (this.#descriptors.has(normalized.protocolId)) throw new TypeError(`Duplicate capability host descriptor ${normalized.protocolId}.`);
    this.#descriptors.set(normalized.protocolId, normalized);
    return this;
  }

  #descriptor(protocolId, version) {
    protocolId = nonEmptyString(protocolId, 'Capability protocol ID');
    version = exactVersion(version, 'Capability exact version');
    const descriptor = this.#descriptors.get(protocolId);
    if (!descriptor) throw new TypeError(`Capability host has no descriptor for ${protocolId}.`);
    if (descriptor.exactVersion !== version) throw new TypeError(`Capability ${protocolId} requires ${version}, host has ${descriptor.exactVersion}.`);
    // The kernel registry is the production provider catalog and is therefore
    // consulted before a session can exist.
    this.#runtime.capabilities.require(protocolId, version);
    return descriptor;
  }

  createSession({ capabilityInstanceId, protocolId, exactVersion: version, config, claims = [], snapshot = undefined } = {}) {
    capabilityInstanceId = nonEmptyString(capabilityInstanceId, 'Capability instance ID');
    if (this.#sessions.has(capabilityInstanceId)) throw new TypeError(`Capability instance ${capabilityInstanceId} already exists.`);
    const descriptor = this.#descriptor(protocolId, version);
    const normalizedConfig = clonePersistedJsonValue(config, `Capability ${capabilityInstanceId} config`);
    const valid = descriptor.validateConfig(normalizedConfig);
    if (valid !== undefined && valid !== true) throw new TypeError(`Capability ${capabilityInstanceId} config is invalid.`);
    const cleanup = new CleanupRegistry();
    const record = {
      capabilityInstanceId,
      descriptor,
      cleanup,
      claims: normalizeClaims(claims, capabilityInstanceId),
      started: false,
      stopped: false,
      disposed: false,
      restored: false,
      session: null,
    };
    const ownerSubscribe = (eventType, handler) => {
      this.#assertOwnerOpen(record);
      const subscription = this.#runtime.subscribe(eventType, handler);
      cleanup.add(() => subscription.dispose());
      return subscription;
    };
    const ownerRuntime = Object.freeze({
      select: (selector) => { this.#assertOwnerOpen(record); return this.#runtime.select(selector); },
      query: (selector) => { this.#assertOwnerOpen(record); return this.#runtime.query(selector); },
      dispatch: (command) => { this.#assertOwnerOpen(record); return this.#runtime.dispatch(command); },
      subscribe: ownerSubscribe,
      capabilities: this.#runtime.capabilities,
    });
    const ownerContext = Object.freeze({
      runtime: ownerRuntime,
      clock: Object.freeze({ now: () => { this.#assertOwnerOpen(record); return this.#clock.now(); } }),
      rng: this.#rngStreams.stream(capabilityInstanceId),
      scheduler: Object.freeze({
        schedule: (job) => { this.#assertOwnerOpen(record); return this.#scheduler.schedule({ ...job, ownerId: capabilityInstanceId }); },
        claimDue: () => { this.#assertOwnerOpen(record); return this.#scheduler.claimDueForOwner(capabilityInstanceId, this.#clock.now()); },
        acknowledge: (jobId, occurrenceId) => { this.#assertOwnerOpen(record); return this.#scheduler.acknowledge(jobId, occurrenceId); },
        release: (jobId, occurrenceId) => { this.#assertOwnerOpen(record); return this.#scheduler.release(jobId, occurrenceId); },
        takeDue: () => { this.#assertOwnerOpen(record); return this.#scheduler.takeDueForOwner(capabilityInstanceId, this.#clock.now()); },
      }),
      claims: Object.freeze({ requested: () => record.claims }),
      subscribe: ownerSubscribe,
      cleanup: Object.freeze({ add: (disposer) => { this.#assertOwnerOpen(record); return cleanup.add(disposer); } }),
    });
    let created;
    try {
      created = descriptor.create(Object.freeze({ capabilityInstanceId, config: normalizedConfig, context: ownerContext }));
    } catch (error) {
      record.stopped = true;
      record.disposed = true;
      try { cleanup.dispose(); } catch { /* preserve provider create error */ }
      this.#scheduler.cancelOwner(capabilityInstanceId);
      throw error;
    }
    if (!created || typeof created !== 'object' || Array.isArray(created)) {
      record.stopped = true;
      record.disposed = true;
      try { cleanup.dispose(); } catch { /* invalid provider result remains the primary error */ }
      this.#scheduler.cancelOwner(capabilityInstanceId);
      throw new TypeError(`Capability ${capabilityInstanceId} create must return a session object.`);
    }
    record.session = created;
    this.#sessions.set(capabilityInstanceId, record);
    if (snapshot !== undefined) {
      try {
        this.restoreSession(capabilityInstanceId, snapshot);
      } catch (error) {
        record.stopped = true;
        record.disposed = true;
        try { cleanup.dispose(); } catch { /* preserve restore error */ }
        this.#scheduler.cancelOwner(capabilityInstanceId);
        try { if (typeof created.dispose === 'function') created.dispose(); } catch { /* preserve restore error */ }
        this.#sessions.delete(capabilityInstanceId);
        throw error;
      }
    }
    return Object.freeze({ capabilityInstanceId, protocolId: descriptor.protocolId, exactVersion: descriptor.exactVersion });
  }

  restoreSession(capabilityInstanceId, snapshot) {
    const record = this.#record(capabilityInstanceId);
    if (record.started || record.restored) throw new TypeError(`Capability ${capabilityInstanceId} can only restore once before start.`);
    if (typeof record.session.restore !== 'function') throw new TypeError(`Capability ${capabilityInstanceId} does not support restore.`);
    record.session.restore(exactSnapshot(snapshot, capabilityInstanceId, record.descriptor));
    record.restored = true;
  }

  startSession(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (record.disposed || record.stopped) throw new TypeError(`Capability ${capabilityInstanceId} is not startable.`);
    if (record.started) return;
    try {
      if (typeof record.session.start === 'function') record.session.start();
      record.started = true;
    } catch (error) {
      try { this.#teardown(record, { dispose: true }); } catch { /* preserve start error after full cleanup attempt */ }
      throw error;
    }
  }

  handle(capabilityInstanceId, input) {
    const record = this.#record(capabilityInstanceId);
    if (!record.started || record.stopped || record.disposed) throw new TypeError(`Capability ${capabilityInstanceId} is not active.`);
    if (typeof record.session.handle !== 'function') throw new TypeError(`Capability ${capabilityInstanceId} does not handle inputs.`);
    return clonePersistedJsonValue(record.session.handle(clonePersistedJsonValue(input, `Capability ${capabilityInstanceId} input`)), `Capability ${capabilityInstanceId} outcome`);
  }

  /**
   * Host-owned web-presentation seam. It intentionally exposes neither the
   * provider session nor its RuntimeContext: consumers only get immutable
   * JSON models, ordinary host input forwarding, and a disposable listener.
   */
  presentation(capabilityInstanceId) {
    const record = this.#presentationRecord(capabilityInstanceId);
    return Object.freeze({
      render: () => this.#renderPresentation(record),
      handle: (input) => this.handle(record.capabilityInstanceId, input),
      subscribe: (listener) => this.#subscribePresentation(record, listener),
    });
  }

  snapshotSession(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (record.disposed) throw new TypeError(`Capability ${capabilityInstanceId} is disposed.`);
    if (typeof record.session.snapshot !== 'function') throw new TypeError(`Capability ${capabilityInstanceId} does not support snapshots.`);
    return Object.freeze({
      capabilityInstanceId,
      protocolId: record.descriptor.protocolId,
      exactVersion: record.descriptor.exactVersion,
      stateSchemaVersion: record.descriptor.stateSchemaVersion,
      state: clonePersistedJsonValue(record.session.snapshot(), `Capability ${capabilityInstanceId} snapshot`),
    });
  }

  claimPlan(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (!record.started || record.stopped || record.disposed) {
      throw new TypeError(`Capability ${capabilityInstanceId} is not active and cannot claim resources.`);
    }
    return Object.freeze({ claims: record.claims });
  }

  stopSession(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (record.stopped || record.disposed) return this.cleanupPlan(capabilityInstanceId);
    this.#assertClaimsReleased([capabilityInstanceId]);
    this.#teardown(record, { dispose: false });
    return this.cleanupPlan(capabilityInstanceId);
  }

  disposeSession(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (record.disposed) return this.cleanupPlan(capabilityInstanceId);
    this.#assertClaimsReleased([capabilityInstanceId]);
    this.#teardown(record, { dispose: true });
    return this.cleanupPlan(capabilityInstanceId);
  }

  async stopWithClaimRelease(capabilityInstanceId, commitCleanup) {
    const result = await this.#commitCleanup(capabilityInstanceId, commitCleanup);
    this.stopSession(capabilityInstanceId);
    return result;
  }

  async disposeWithClaimRelease(capabilityInstanceId, commitCleanup) {
    const result = await this.#commitCleanup(capabilityInstanceId, commitCleanup);
    this.disposeSession(capabilityInstanceId);
    return result;
  }

  /**
   * Generic quest seam: reducer merges `prepareCleanup()` into the same kernel
   * transaction as quest state. The coordinator calls `finalizeCommitted()`
   * only after that transaction result is committed, so teardown cannot race a
   * claim release or leave a second claim owner behind.
   */
  questLifecycle() {
    return Object.freeze({
      prepareCleanup: ({ capabilityInstanceIds }) => {
        if (!Array.isArray(capabilityInstanceIds)) throw new TypeError('Quest capability cleanup IDs must be an array.');
        const releaseClaimOwnerIds = capabilityInstanceIds.map((capabilityInstanceId) => this.cleanupPlan(capabilityInstanceId).releaseClaimOwnerIds[0]);
        return Object.freeze({ releaseClaimOwnerIds: Object.freeze([...new Set(releaseClaimOwnerIds)].sort()) });
      },
      finalizeCommitted: (cleanup, result, { dispose = true } = {}) => this.finalizeCommittedCleanup(cleanup, result, { dispose }),
    });
  }

  finalizeCommittedCleanup(cleanup, result, { dispose = true } = {}) {
    if (!result || result.status !== 'committed') throw new TypeError('Capability cleanup can finalize only after a committed kernel transaction.');
    if (!cleanup || typeof cleanup !== 'object' || !Array.isArray(cleanup.capabilityInstanceIds)) {
      throw new TypeError('Capability cleanup record is invalid.');
    }
    const instanceIds = [...new Set(cleanup.capabilityInstanceIds)].sort();
    this.#assertClaimsReleased(instanceIds);
    let firstError;
    for (const capabilityInstanceId of instanceIds) {
      try {
        if (dispose) this.disposeSession(capabilityInstanceId);
        else this.stopSession(capabilityInstanceId);
      } catch (error) {
        firstError ??= error;
      }
    }
    if (firstError) throw firstError;
    return Object.freeze({ capabilityInstanceIds: Object.freeze(instanceIds), finalized: true });
  }

  cleanupPlan(capabilityInstanceId) {
    this.#record(capabilityInstanceId);
    return Object.freeze({ releaseClaimOwnerIds: Object.freeze([capabilityInstanceId]) });
  }

  #record(capabilityInstanceId) {
    capabilityInstanceId = nonEmptyString(capabilityInstanceId, 'Capability instance ID');
    const record = this.#sessions.get(capabilityInstanceId);
    if (!record) throw new TypeError(`Unknown capability instance ${capabilityInstanceId}.`);
    return record;
  }

  #presentationRecord(capabilityInstanceId) {
    const record = this.#record(capabilityInstanceId);
    if (!record.started || record.stopped || record.disposed) {
      throw new TypeError(`Capability ${capabilityInstanceId} presentation is not active.`);
    }
    presentationMethods(record.session, capabilityInstanceId);
    return record;
  }

  #renderPresentation(record) {
    const active = this.#presentationRecord(record.capabilityInstanceId);
    const { render } = presentationMethods(active.session, active.capabilityInstanceId);
    return clonePersistedJsonValue(render.call(active.session), `Capability ${active.capabilityInstanceId} presentation`);
  }

  #subscribePresentation(record, listener) {
    const active = this.#presentationRecord(record.capabilityInstanceId);
    if (typeof listener !== 'function') throw new TypeError('Capability presentation listener must be a function.');
    const { subscribe } = presentationMethods(active.session, active.capabilityInstanceId);
    let disposed = false;
    let providerSubscription;
    const dispose = () => {
      if (disposed) return;
      disposed = true;
      providerSubscription?.dispose();
    };
    const forward = (model) => {
      if (disposed || active.stopped || active.disposed) return;
      try {
        listener(clonePersistedJsonValue(model, `Capability ${active.capabilityInstanceId} presentation update`));
      } catch {
        // Presentation observers are not allowed to affect provider lifecycle.
      }
    };
    try {
      providerSubscription = presentationSubscription(subscribe.call(active.session, forward), active.capabilityInstanceId);
      active.cleanup.add(dispose);
    } catch (error) {
      try { dispose(); } catch { /* preserve the subscribe boundary error */ }
      throw error;
    }
    return Object.freeze({
      get disposed() { return disposed; },
      dispose,
    });
  }

  #assertOwnerOpen(record) {
    if (record.stopped || record.disposed) {
      throw new TypeError(`Capability ${record.capabilityInstanceId} owner context is closed.`);
    }
  }

  #assertClaimsReleased(capabilityInstanceIds) {
    const pendingOwners = this.#runtime.query(({ claims }) => {
      const owners = new Set(capabilityInstanceIds);
      return claims.flatMap(({ resourceId, entries }) => entries
        .filter(({ ownerId }) => owners.has(ownerId))
        .map(({ ownerId }) => Object.freeze({ resourceId, ownerId })));
    });
    if (pendingOwners.length) {
      throw new TypeError(`Capability claim release did not commit for ${pendingOwners.map(({ ownerId }) => ownerId).sort().join(', ')}.`);
    }
  }

  #teardown(record, { dispose }) {
    if (record.disposed || (record.stopped && !dispose)) return;
    let firstError;
    // Close owner callbacks before invoking provider code, so retained context
    // cannot add a new subscription/job while the lifecycle is ending.
    const shouldStop = !record.stopped;
    if (shouldStop) {
      record.stopped = true;
      try {
        if (typeof record.session.stop === 'function') record.session.stop();
      } catch (error) {
        firstError = error;
      }
      // A provider can synchronously request dispose from stop(). That nested
      // lifecycle has already cleaned and terminally marked this record, so
      // the outer teardown must not invoke dispose() again.
      if (record.disposed) {
        if (firstError) throw firstError;
        return;
      }
    }
    try { this.#scheduler.cancelOwner(record.capabilityInstanceId); } catch (error) { firstError ??= error; }
    try { record.cleanup.dispose(); } catch (error) { firstError ??= error; }
    // A registered disposer can itself synchronously call disposeSession().
    // That nested lifecycle has already run provider dispose, so checkpoint
    // terminal state again after cleanup before the outer provider call.
    if (record.disposed) {
      if (firstError) throw firstError;
      return;
    }
    if (dispose) {
      // Provider code is untrusted lifecycle code and may synchronously call
      // back into the host. Mark terminal before invoking it to make that
      // re-entry a no-op instead of recursively disposing the same session.
      record.disposed = true;
      try {
        if (typeof record.session.dispose === 'function') record.session.dispose();
      } catch (error) {
        firstError ??= error;
      }
    }
    if (firstError) throw firstError;
  }

  async #commitCleanup(capabilityInstanceId, commitCleanup) {
    if (typeof commitCleanup !== 'function') throw new TypeError('Capability claim release requires a commitCleanup function.');
    const result = await commitCleanup(this.cleanupPlan(capabilityInstanceId));
    if (!result || result.status !== 'committed') {
      throw new TypeError(`Capability ${capabilityInstanceId} claim release did not commit.`);
    }
    this.#assertClaimsReleased([capabilityInstanceId]);
    return result;
  }
}
