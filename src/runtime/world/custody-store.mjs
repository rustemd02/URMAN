import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

const DEFAULT_STATE_KEY = 'world.custody';

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function assertStateKey(value) { return nonEmptyString(value, 'Custody state key'); }

function exactObject(value, label, keys) {
  if (!value || typeof value !== 'object' || Array.isArray(value)
    || Object.keys(value).sort().join(',') !== [...keys].sort().join(',')) {
    throw new TypeError(`${label} has an invalid shape.`);
  }
  return value;
}

function normalizeItem(item, label) {
  exactObject(item, label, ['itemId', 'custodyOwnerId', 'condition']);
  return Object.freeze({
    itemId: nonEmptyString(item.itemId, `${label}.itemId`),
    custodyOwnerId: nonEmptyString(item.custodyOwnerId, `${label}.custodyOwnerId`),
    condition: clonePersistedJsonValue(item.condition, `${label}.condition`),
  });
}

function readItems(state, stateKey) {
  if (!state || typeof state !== 'object' || Array.isArray(state)) throw new TypeError('Custody state source must be an object.');
  const source = Object.hasOwn(state, stateKey) ? state[stateKey] : [];
  if (!Array.isArray(source)) throw new TypeError(`Custody state ${stateKey} must be an array.`);
  const ids = new Set();
  return source.map((item, index) => {
    const normalized = normalizeItem(item, `Custody item ${index}`);
    if (ids.has(normalized.itemId)) throw new TypeError(`Duplicate custody item ${normalized.itemId}.`);
    ids.add(normalized.itemId);
    return normalized;
  });
}

function normalizeOperation(operation, index) {
  if (!operation || typeof operation !== 'object' || Array.isArray(operation)) throw new TypeError(`Custody operation ${index} must be an object.`);
  if (operation.op === 'claim' || operation.op === 'transfer') {
    exactObject(operation, `Custody operation ${index}`, ['op', 'itemId', 'fromOwnerId', 'toOwnerId']);
    return Object.freeze({
      op: operation.op,
      itemId: nonEmptyString(operation.itemId, `Custody operation ${index}.itemId`),
      fromOwnerId: nonEmptyString(operation.fromOwnerId, `Custody operation ${index}.fromOwnerId`),
      toOwnerId: nonEmptyString(operation.toOwnerId, `Custody operation ${index}.toOwnerId`),
    });
  }
  if (operation.op === 'consume') {
    exactObject(operation, `Custody operation ${index}`, ['op', 'itemId', 'ownerId']);
    return Object.freeze({
      op: operation.op,
      itemId: nonEmptyString(operation.itemId, `Custody operation ${index}.itemId`),
      ownerId: nonEmptyString(operation.ownerId, `Custody operation ${index}.ownerId`),
    });
  }
  throw new TypeError(`Unknown custody operation ${operation.op}.`);
}

/**
 * Builds one kernel transaction fragment. The caller uses the same fragment
 * with any authored quest effects, so every batch is prevalidated before the
 * kernel changes state.
 */
export function planCustodyBatch({
  state,
  operations,
  claimOwnerId,
  claimLifecycleScope,
  stateKey = DEFAULT_STATE_KEY,
  eventType = 'world.custody.changed',
} = {}) {
  stateKey = assertStateKey(stateKey);
  if (!Array.isArray(operations) || operations.length === 0) throw new TypeError('Custody operations must be a non-empty array.');
  eventType = nonEmptyString(eventType, 'Custody event type');
  const normalized = operations.map(normalizeOperation);
  // Every custody mutation is protected by the kernel's resource arbiter.
  // The distinct action/capability owner and lifecycle are mandatory so a
  // competing action is rejected before any effect batch can be considered.
  const claims = itemResourceClaims(normalized, { ownerId: claimOwnerId, lifecycleScope: claimLifecycleScope });
  const next = readItems(state, stateKey).map((item) => ({ ...item }));
  const indexById = new Map(next.map((item, index) => [item.itemId, index]));
  let invalidReason;

  for (const operation of normalized) {
    const itemIndex = indexById.get(operation.itemId);
    if (itemIndex === undefined) {
      invalidReason = `Custody item ${operation.itemId} does not exist.`;
      break;
    }
    const item = next[itemIndex];
    const expectedOwner = operation.op === 'consume' ? operation.ownerId : operation.fromOwnerId;
    if (item.custodyOwnerId !== expectedOwner) {
      invalidReason = `Custody item ${operation.itemId} is not held by ${expectedOwner}.`;
      break;
    }
    if (operation.op === 'consume') {
      next.splice(itemIndex, 1);
      indexById.delete(operation.itemId);
      for (let index = itemIndex; index < next.length; index += 1) indexById.set(next[index].itemId, index);
    } else {
      next[itemIndex] = { ...item, custodyOwnerId: operation.toOwnerId };
    }
  }

  if (invalidReason) {
    // Kernel preflights claims before effects. This intentionally-invalid
    // primitive therefore becomes ResourceConflict when another transaction
    // owns the item, otherwise a zero-effect PreflightError with this reason.
    return Object.freeze({
      claims,
      effects: Object.freeze([{ op: 'state.increment', key: stateKey, delta: Number.NaN }]),
      events: Object.freeze([]),
      value: Object.freeze({ invalidReason }),
    });
  }

  const items = clonePersistedJsonValue(next, 'Custody transaction result');
  return Object.freeze({
    claims,
    effects: Object.freeze([{ op: 'state.set', key: stateKey, value: items }]),
    events: Object.freeze([{ type: eventType, payload: Object.freeze({ operations: clonePersistedJsonValue(normalized, 'Custody operation event') }) }]),
    value: Object.freeze({ items }),
  });
}

/** Resource claims stay generic kernel claims; this helper only derives them. */
export function itemResourceClaims(operations, { ownerId, lifecycleScope }) {
  ownerId = nonEmptyString(ownerId, 'Item claim owner ID');
  lifecycleScope = nonEmptyString(lifecycleScope, 'Item claim lifecycle scope');
  if (!Array.isArray(operations)) throw new TypeError('Item claim operations must be an array.');
  const itemIds = [...new Set(operations.map((operation, index) => normalizeOperation(operation, index).itemId))].sort();
  return Object.freeze(itemIds.map((itemId) => Object.freeze({
    resourceId: `item/${itemId}`,
    ownerId,
    mode: 'exclusive',
    lifecycleScope,
  })));
}
