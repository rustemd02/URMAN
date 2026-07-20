import {
  CommandStatus,
  ConflictError,
  DuplicateOccurrence,
  OccurrenceConflict,
  PreflightError,
  RuntimeFailure,
  UnknownCommandError,
} from '../contracts/runtime-contracts.mjs';
import { boundaryFingerprint, canonicalJson, cloneJsonValue, clonePersistedJsonValue, deepFreeze } from '../contracts/json-value.mjs';
import { CapabilityRegistry } from '../registries/runtime-registries.mjs';
import { OrderedEventBus } from './ordered-event-bus.mjs';
import { StateStore } from './state-store.mjs';

function immutableClone(value) {
  return deepFreeze(structuredClone(value));
}

function validKey(key) {
  return typeof key === 'string' && key.length > 0 && !['__proto__', 'constructor', 'prototype'].includes(key);
}

function rejected(error) {
  return Object.freeze({ status: CommandStatus.Rejected, error: Object.freeze(error) });
}

function checkedArray(value, field) {
  if (value === undefined) return [];
  if (!Array.isArray(value)) throw new TypeError(`${field} must be an array.`);
  return value;
}

function preflightClaims(plan) {
  if (plan && typeof plan.then === 'function') {
    Promise.resolve(plan).catch(() => undefined);
    throw new TypeError('Command handler must return a synchronous transaction plan.');
  }
  if (!plan || typeof plan !== 'object' || Array.isArray(plan)) {
    throw new TypeError('Command handler must return a synchronous transaction plan.');
  }
  const claims = checkedArray(plan.claims, 'claims').map((claim) => {
    if (!claim || !validKey(claim.resourceId) || !validKey(claim.ownerId) || !validKey(claim.lifecycleScope) || !['exclusive', 'shared'].includes(claim.mode)) {
      throw new TypeError('Resource claim is invalid.');
    }
    return Object.freeze({ resourceId: claim.resourceId, ownerId: claim.ownerId, mode: claim.mode, lifecycleScope: claim.lifecycleScope });
  });
  const releaseClaimOwnerIds = checkedArray(plan.releaseClaimOwnerIds, 'releaseClaimOwnerIds').map((ownerId) => {
    if (!validKey(ownerId)) throw new TypeError('Resource claim release owner is invalid.');
    return ownerId;
  });
  const releaseClaimLifecycleScopes = checkedArray(plan.releaseClaimLifecycleScopes, 'releaseClaimLifecycleScopes').map((scope) => {
    if (!validKey(scope)) throw new TypeError('Resource claim lifecycle release scope is invalid.');
    return scope;
  });
  return Object.freeze({ claims, releaseClaimOwnerIds, releaseClaimLifecycleScopes });
}

function preflightCommit(plan) {
  const effects = checkedArray(plan.effects, 'effects').map((effect) => {
    if (!effect || !validKey(effect.key)) throw new TypeError('State effect key is invalid.');
    if (effect.op === 'state.set') {
      if (!Object.hasOwn(effect, 'value')) throw new TypeError('state.set requires value.');
      return Object.freeze({ op: effect.op, key: effect.key, value: clonePersistedJsonValue(effect.value, `State effect ${effect.key}`) });
    }
    if (effect.op === 'state.delete') return Object.freeze({ op: effect.op, key: effect.key });
    if (effect.op === 'state.increment' && Number.isFinite(effect.delta)) {
      return Object.freeze({ op: effect.op, key: effect.key, delta: effect.delta });
    }
    throw new TypeError(`Unknown or invalid state effect ${effect.op}.`);
  });
  const events = checkedArray(plan.events, 'events').map((event) => {
    if (!event || typeof event.type !== 'string' || !event.type) throw new TypeError('Event type must be non-empty.');
    return Object.freeze({ type: event.type, payload: clonePersistedJsonValue(event.payload, `Event ${event.type} payload`) });
  });
  const value = clonePersistedJsonValue(Object.hasOwn(plan, 'value') ? plan.value : null, 'Command result value');
  return Object.freeze({ effects, events, value });
}

function cloneClaims(claims) {
  return new Map([...claims].map(([resourceId, entries]) => [resourceId, entries.map((entry) => ({ ...entry }))]));
}

function applyClaims(currentClaims, releaseOwnerIds, releaseLifecycleScopes, requestedClaims) {
  const next = cloneClaims(currentClaims);
  const releases = new Set(releaseOwnerIds);
  const lifecycleReleases = new Set(releaseLifecycleScopes);
  for (const [resourceId, entries] of next) {
    const remaining = entries.filter(({ ownerId, lifecycleScope }) => !releases.has(ownerId) && !lifecycleReleases.has(lifecycleScope));
    if (remaining.length) next.set(resourceId, remaining);
    else next.delete(resourceId);
  }
  for (const claim of requestedClaims) {
    if (!claim || !validKey(claim.resourceId) || !validKey(claim.ownerId) || !validKey(claim.lifecycleScope) || !['exclusive', 'shared'].includes(claim.mode)) {
      throw new TypeError('Resource claim is invalid.');
    }
    const existing = (next.get(claim.resourceId) ?? []).filter(({ ownerId }) => ownerId !== claim.ownerId);
    if (existing.length && (claim.mode === 'exclusive' || existing.some(({ mode }) => mode === 'exclusive'))) {
      return { conflict: Object.freeze({ resourceId: claim.resourceId, requestedBy: claim.ownerId, heldBy: Object.freeze(existing.map(({ ownerId }) => ownerId).sort()) }) };
    }
    next.set(claim.resourceId, [...existing, { ...claim }].sort((left, right) => left.ownerId.localeCompare(right.ownerId)));
  }
  return { claims: next };
}

function claimsSnapshot(claims) {
  return Object.freeze([...claims.entries()].sort(([left], [right]) => left.localeCompare(right))
    .map(([resourceId, entries]) => Object.freeze({ resourceId, entries: immutableClone(entries) })));
}

function exactObject(value, field, requiredKeys, optionalKeys = []) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${field} must be an object.`);
  }
  const actualKeys = Object.keys(value).sort();
  const expectedKeys = [...requiredKeys, ...optionalKeys].sort();
  if (actualKeys.some((key) => !expectedKeys.includes(key))
    || requiredKeys.some((key) => !Object.hasOwn(value, key))) {
    throw new TypeError(`${field} has an invalid shape.`);
  }
  return value;
}

function nonEmptyString(value, field) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${field} must be a non-empty string.`);
  return value;
}

function snapshotResult(result) {
  if (!result || typeof result !== 'object' || Array.isArray(result)) {
    throw new TypeError('Cannot snapshot an invalid command result.');
  }
  if (result.status === CommandStatus.Committed) {
    return cloneJsonValue({
      status: CommandStatus.Committed,
      transactionId: result.transactionId,
      value: clonePersistedJsonValue(result.value, 'Committed command result snapshot'),
    }, 'Committed command result snapshot');
  }
  if (result.status === CommandStatus.Rejected && result.error instanceof RuntimeFailure) {
    const error = {
      code: result.error.code,
      message: result.error.message,
    };
    if (Object.hasOwn(result.error, 'details')) error.details = result.error.details;
    return cloneJsonValue({
      status: CommandStatus.Rejected,
      error,
    }, 'Rejected command result snapshot');
  }
  throw new TypeError('Cannot snapshot an invalid command result.');
}

function restoreResult(result, field) {
  if (!result || typeof result !== 'object' || Array.isArray(result)) {
    throw new TypeError(`${field} must be an object.`);
  }
  if (result.status === CommandStatus.Committed) {
    exactObject(result, field, ['status', 'transactionId', 'value']);
    return Object.freeze({
      status: CommandStatus.Committed,
      transactionId: nonEmptyString(result.transactionId, `${field}.transactionId`),
      value: clonePersistedJsonValue(result.value, `${field}.value`),
    });
  }
  if (result.status === CommandStatus.Rejected) {
    exactObject(result, field, ['status', 'error']);
    exactObject(result.error, `${field}.error`, ['code', 'message'], ['details']);
    const code = nonEmptyString(result.error.code, `${field}.error.code`);
    const message = nonEmptyString(result.error.message, `${field}.error.message`);
    const details = Object.hasOwn(result.error, 'details')
      ? cloneJsonValue(result.error.details, `${field}.error.details`)
      : undefined;
    return rejected(new RuntimeFailure(code, message, details));
  }
  throw new TypeError(`${field}.status must be a known command status.`);
}

function restoreSnapshot(snapshot) {
  const restored = clonePersistedJsonValue(snapshot, 'Runtime kernel snapshot');
  exactObject(restored, 'Runtime kernel snapshot', ['state', 'claims', 'occurrences', 'eventSequence']);
  if (!restored.state || typeof restored.state !== 'object' || Array.isArray(restored.state)) {
    throw new TypeError('Runtime kernel snapshot.state must be a JSON object.');
  }
  if (!Array.isArray(restored.claims)) throw new TypeError('Runtime kernel snapshot.claims must be an array.');
  if (!Array.isArray(restored.occurrences)) throw new TypeError('Runtime kernel snapshot.occurrences must be an array.');
  if (!Number.isSafeInteger(restored.eventSequence) || restored.eventSequence < 0) {
    throw new TypeError('Runtime kernel snapshot.eventSequence must be a safe non-negative integer.');
  }

  const claims = new Map();
  for (const [index, group] of restored.claims.entries()) {
    const field = `Runtime kernel snapshot.claims[${index}]`;
    exactObject(group, field, ['resourceId', 'entries']);
    const resourceId = group.resourceId;
    if (!validKey(resourceId)) throw new TypeError(`${field}.resourceId is invalid.`);
    if (claims.has(resourceId)) throw new TypeError(`Runtime kernel snapshot has duplicate resource claim ${resourceId}.`);
    if (!Array.isArray(group.entries) || group.entries.length === 0) {
      throw new TypeError(`${field}.entries must be a non-empty array.`);
    }
    const ownerIds = new Set();
    const entries = group.entries.map((entry, entryIndex) => {
      const entryField = `${field}.entries[${entryIndex}]`;
      exactObject(entry, entryField, ['resourceId', 'ownerId', 'mode', 'lifecycleScope']);
      if (entry.resourceId !== resourceId
        || !validKey(entry.ownerId)
        || !validKey(entry.lifecycleScope)
        || !['exclusive', 'shared'].includes(entry.mode)) {
        throw new TypeError(`${entryField} is invalid.`);
      }
      if (ownerIds.has(entry.ownerId)) {
        throw new TypeError(`Runtime kernel snapshot has duplicate claim owner ${entry.ownerId} for ${resourceId}.`);
      }
      ownerIds.add(entry.ownerId);
      return Object.freeze({
        resourceId,
        ownerId: entry.ownerId,
        mode: entry.mode,
        lifecycleScope: entry.lifecycleScope,
      });
    });
    if (entries.length > 1 && entries.some((entry) => entry.mode === 'exclusive')) {
      throw new TypeError(`Runtime kernel snapshot has conflicting claims for ${resourceId}.`);
    }
    claims.set(resourceId, entries);
  }

  const ledger = new Map();
  for (const [index, occurrence] of restored.occurrences.entries()) {
    const field = `Runtime kernel snapshot.occurrences[${index}]`;
    exactObject(occurrence, field, ['actionOccurrenceId', 'fingerprint', 'result']);
    const actionOccurrenceId = nonEmptyString(occurrence.actionOccurrenceId, `${field}.actionOccurrenceId`);
    if (ledger.has(actionOccurrenceId)) {
      throw new TypeError(`Runtime kernel snapshot has duplicate occurrence ${actionOccurrenceId}.`);
    }
    ledger.set(actionOccurrenceId, Object.freeze({
      fingerprint: nonEmptyString(occurrence.fingerprint, `${field}.fingerprint`),
      result: restoreResult(occurrence.result, `${field}.result`),
    }));
  }

  return Object.freeze({
    state: restored.state,
    claims,
    ledger,
    eventSequence: restored.eventSequence,
  });
}

export class RuntimeKernel {
  #store;
  #claims = new Map();
  #handlers = new Map();
  #ledger = new Map();
  #events = new OrderedEventBus();
  #eventSequence = 0;
  #tail = Promise.resolve();
  #capabilities;
  #context;

  constructor({ initialState = {}, handlers = {}, capabilityRegistry } = {}) {
    if (!(capabilityRegistry instanceof CapabilityRegistry)) {
      throw new TypeError('RuntimeKernel requires a preflighted CapabilityRegistry.');
    }
    this.#store = new StateStore(initialState);
    this.#capabilities = capabilityRegistry;
    const entries = handlers instanceof Map ? handlers.entries() : Object.entries(handlers);
    for (const [type, handler] of entries) {
      if (this.#handlers.has(type)) throw new TypeError(`Duplicate command handler ${type}.`);
      if (typeof handler !== 'function') throw new TypeError(`Command handler ${type} is not a function.`);
      this.#handlers.set(type, handler);
    }
    const capabilities = Object.freeze({ require: (protocolId, exactVersion) => this.#capabilities.require(protocolId, exactVersion) });
    this.#context = Object.freeze({
      select: (selector) => this.select(selector),
      query: (selector) => this.query(selector),
      dispatch: (command) => this.dispatch(command),
      subscribe: (eventType, handler) => this.subscribe(eventType, handler),
      capabilities,
    });
  }

  static fromSnapshot(snapshot, { handlers = {}, capabilityRegistry } = {}) {
    const restored = restoreSnapshot(snapshot);
    const kernel = new RuntimeKernel({
      initialState: restored.state,
      handlers,
      capabilityRegistry,
    });
    kernel.#claims = restored.claims;
    kernel.#ledger = restored.ledger;
    kernel.#eventSequence = restored.eventSequence;
    return kernel;
  }

  get context() { return this.#context; }

  select(selector) {
    return this.#store.select(selector);
  }

  query(selector) {
    if (typeof selector !== 'function') throw new TypeError('Runtime query must be a function.');
    return immutableClone(selector(Object.freeze({
      state: this.#store.snapshot(),
      claims: claimsSnapshot(this.#claims),
    })));
  }

  subscribe(eventType, handler) { return this.#events.subscribe(eventType, handler); }

  dispatch(command) {
    const operation = this.#tail.then(() => this.#dispatchSerial(command));
    this.#tail = operation.catch(() => undefined);
    return operation;
  }

  #record(actionOccurrenceId, fingerprint, result) {
    this.#ledger.set(actionOccurrenceId, Object.freeze({ fingerprint, result }));
    return result;
  }

  #dispatchSerial(command) {
    let fingerprint;
    let preparedCommand;
    let commandType;
    let actionOccurrenceId;
    try {
      if (!command || typeof command !== 'object' || Array.isArray(command)) throw new TypeError('Command must be an object.');
      const descriptors = Object.getOwnPropertyDescriptors(command);
      const occurrenceDescriptor = descriptors.actionOccurrenceId;
      if (occurrenceDescriptor && Object.hasOwn(occurrenceDescriptor, 'value')) actionOccurrenceId = occurrenceDescriptor.value;
      const typeDescriptor = descriptors.type;
      if (typeDescriptor && Object.hasOwn(typeDescriptor, 'value')) commandType = typeDescriptor.value;
      const payloadDescriptor = descriptors.payload;
      if (typeof commandType !== 'string' || !commandType || typeof actionOccurrenceId !== 'string' || !actionOccurrenceId) {
        throw new TypeError('Command type and actionOccurrenceId are required as own data properties.');
      }
      if (!payloadDescriptor || !Object.hasOwn(payloadDescriptor, 'value')) throw new TypeError('Command payload must be an own data property.');
      preparedCommand = cloneJsonValue({
        type: commandType,
        actionOccurrenceId,
        payload: payloadDescriptor.value,
      }, 'Command');
      fingerprint = canonicalJson({ type: preparedCommand.type, payload: preparedCommand.payload }, 'Command fingerprint');
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (typeof actionOccurrenceId !== 'string' || !actionOccurrenceId) return rejected(new PreflightError(message));
      const invalidEnvelope = boundaryFingerprint(command);
      fingerprint = canonicalJson({ invalidCommand: message, invalidEnvelope });
      const prior = this.#ledger.get(actionOccurrenceId);
      if (prior) {
        if (prior.fingerprint === fingerprint) return rejected(new DuplicateOccurrence(actionOccurrenceId, prior.result));
        return rejected(new OccurrenceConflict(actionOccurrenceId));
      }
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError(message)));
    }
    const prior = this.#ledger.get(actionOccurrenceId);
    if (prior) {
      if (prior.fingerprint === fingerprint) return rejected(new DuplicateOccurrence(actionOccurrenceId, prior.result));
      return rejected(new OccurrenceConflict(actionOccurrenceId));
    }
    const handler = this.#handlers.get(commandType);
    if (!handler) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new UnknownCommandError(commandType)));
    }
    const transactionId = `tx:${actionOccurrenceId}`;
    let plan;
    try {
      plan = handler(preparedCommand, Object.freeze({
        state: this.#store.snapshot(),
        select: (selector) => this.#store.select(selector),
      })) ?? {};
    } catch (error) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError(error instanceof Error ? error.message : String(error))));
    }
    let preparedClaims;
    let claimsResult;
    try {
      if (plan && typeof plan.then === 'function') {
        Promise.resolve(plan).catch(() => undefined);
        throw new TypeError('Command handler must return a synchronous transaction plan.');
      }
      if (plan.rejection) {
        if (typeof plan.rejection !== 'object' || Array.isArray(plan.rejection)) throw new TypeError('Command rejection must be an object.');
        const code = plan.rejection.code ?? 'CommandRejected';
        const message = plan.rejection.message ?? 'Command rejected.';
        if (typeof code !== 'string' || !code || typeof message !== 'string' || !message) throw new TypeError('Command rejection code and message must be non-empty strings.');
        const details = Object.hasOwn(plan.rejection, 'details')
          ? cloneJsonValue(plan.rejection.details, 'Command rejection details')
          : undefined;
        return this.#record(actionOccurrenceId, fingerprint, rejected(new RuntimeFailure(code, message, details)));
      }
      preparedClaims = preflightClaims(plan);
      claimsResult = applyClaims(this.#claims, preparedClaims.releaseClaimOwnerIds, preparedClaims.releaseClaimLifecycleScopes, preparedClaims.claims);
    } catch (error) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError(error instanceof Error ? error.message : String(error))));
    }
    if (claimsResult.conflict) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new ConflictError(`Resource ${claimsResult.conflict.resourceId} is already claimed.`, claimsResult.conflict)));
    }
    let preparedCommit;
    try {
      preparedCommit = preflightCommit(plan);
    } catch (error) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError(error instanceof Error ? error.message : String(error))));
    }
    if (preparedCommit.events.length > Number.MAX_SAFE_INTEGER - this.#eventSequence) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError('Event sequence would exceed the safe integer range.')));
    }
    try {
      this.#store.reduceTransaction(preparedCommit.effects);
    } catch (error) {
      return this.#record(actionOccurrenceId, fingerprint, rejected(new PreflightError(error instanceof Error ? error.message : String(error))));
    }
    this.#claims = claimsResult.claims;
    const result = Object.freeze({
      status: CommandStatus.Committed,
      transactionId,
      value: preparedCommit.value,
    });
    this.#record(actionOccurrenceId, fingerprint, result);
    for (const draft of preparedCommit.events) {
      const event = Object.freeze({
        type: draft.type,
        payload: immutableClone(draft.payload),
        sequence: ++this.#eventSequence,
        transactionId,
      });
      this.#events.publish(event);
    }
    return result;
  }

  exportSnapshot() {
    return Object.freeze({
      state: this.#store.snapshot(),
      claims: claimsSnapshot(this.#claims),
      occurrences: Object.freeze([...this.#ledger.entries()].sort(([left], [right]) => left.localeCompare(right))
        .map(([actionOccurrenceId, entry]) => Object.freeze({
          actionOccurrenceId,
          fingerprint: entry.fingerprint,
          result: snapshotResult(entry.result),
        }))),
      eventSequence: this.#eventSequence,
    });
  }
}
