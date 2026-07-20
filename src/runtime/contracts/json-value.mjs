function jsonBoundaryError(label, path, reason) {
  return new TypeError(`${label}${path}: ${reason}.`);
}

function snapshotJsonTree(value, label, path, ancestors, normalizeNegativeZero = false) {
  if (value === null || typeof value === 'string' || typeof value === 'boolean') return value;
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw jsonBoundaryError(label, path, 'number must be finite');
    return normalizeNegativeZero && Object.is(value, -0) ? 0 : value;
  }
  if (typeof value !== 'object') throw jsonBoundaryError(label, path, `unsupported ${typeof value}`);
  if (ancestors.has(value)) throw jsonBoundaryError(label, path, 'cyclic value');
  ancestors.add(value);
  try {
    const descriptors = Object.getOwnPropertyDescriptors(value);
    if (Array.isArray(value)) {
      const lengthDescriptor = descriptors.length;
      if (!lengthDescriptor || !Object.hasOwn(lengthDescriptor, 'value') || !Number.isSafeInteger(lengthDescriptor.value) || lengthDescriptor.value < 0) {
        throw jsonBoundaryError(label, path, 'array length must be a safe non-negative integer');
      }
      const length = lengthDescriptor.value;
      const allowedKeys = new Set(['length']);
      const normalized = [];
      for (let index = 0; index < length; index += 1) {
        const key = String(index);
        allowedKeys.add(key);
        const descriptor = descriptors[key];
        if (!descriptor) throw jsonBoundaryError(label, `${path}/${index}`, 'sparse arrays are not JSON values');
        if (!descriptor.enumerable || !Object.hasOwn(descriptor, 'value')) {
          throw jsonBoundaryError(label, `${path}/${index}`, 'array entries must be enumerable data properties');
        }
        normalized.push(snapshotJsonTree(descriptor.value, label, `${path}/${index}`, ancestors, normalizeNegativeZero));
      }
      for (const key of Reflect.ownKeys(descriptors)) {
        if (typeof key !== 'string' || !allowedKeys.has(key)) {
          throw jsonBoundaryError(label, path, 'arrays cannot contain extra properties');
        }
      }
      return normalized;
    }
    const prototype = Object.getPrototypeOf(value);
    if (prototype !== Object.prototype && prototype !== null) throw jsonBoundaryError(label, path, 'object must have a plain prototype');
    const normalized = {};
    const keys = Reflect.ownKeys(descriptors).sort((left, right) => String(left).localeCompare(String(right)));
    for (const key of keys) {
      if (typeof key !== 'string') throw jsonBoundaryError(label, path, 'symbol keys are not JSON members');
      const descriptor = descriptors[key];
      if (!descriptor?.enumerable || !Object.hasOwn(descriptor, 'value')) {
        throw jsonBoundaryError(label, `${path}/${key}`, 'members must be enumerable data properties');
      }
      Object.defineProperty(normalized, key, {
        configurable: true,
        enumerable: true,
        writable: true,
        value: snapshotJsonTree(descriptor.value, label, `${path}/${key}`, ancestors, normalizeNegativeZero),
      });
    }
    return normalized;
  } finally {
    ancestors.delete(value);
  }
}

export function assertJsonValue(value, label = 'JSON value') {
  snapshotJsonTree(value, label, '', new Set());
  return value;
}

export function deepFreeze(value) {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value;
  for (const child of Object.values(value)) deepFreeze(child);
  return Object.freeze(value);
}

export function cloneJsonValue(value, label = 'JSON value') {
  return deepFreeze(snapshotJsonTree(value, label, '', new Set()));
}

// JSON.stringify(-0) is 0. Persisted data therefore canonicalizes -0 before it
// enters state, the occurrence ledger, or an event, while command normalization
// keeps -0 intact for the sign-sensitive canonical command fingerprint.
export function clonePersistedJsonValue(value, label = 'Persisted JSON value') {
  return deepFreeze(snapshotJsonTree(value, label, '', new Set(), true));
}

export function canonicalJson(value, label = 'JSON value') {
  const normalized = snapshotJsonTree(value, label, '', new Set());
  const encode = (entry) => {
    if (typeof entry === 'number') return Object.is(entry, -0) ? '-0' : JSON.stringify(entry);
    if (entry === null || typeof entry !== 'object') return JSON.stringify(entry);
    if (Array.isArray(entry)) return `[${entry.map(encode).join(',')}]`;
    return `{${Object.keys(entry).sort().map((key) => `${JSON.stringify(key)}:${encode(entry[key])}`).join(',')}}`;
  };
  return encode(normalized);
}

function numberFingerprint(value) {
  if (Number.isNaN(value)) return 'number:NaN';
  if (value === Infinity) return 'number:+Infinity';
  if (value === -Infinity) return 'number:-Infinity';
  if (Object.is(value, -0)) return 'number:-0';
  return `number:${value}`;
}

function functionFingerprint(value) {
  try {
    return JSON.stringify(Function.prototype.toString.call(value));
  } catch {
    return 'uninspectable-function';
  }
}

function symbolFingerprint(value) {
  const globalKey = Symbol.keyFor(value);
  if (globalKey !== undefined) return `global:${JSON.stringify(globalKey)}`;
  return `local:${JSON.stringify(value.description)}`;
}

function descriptorValueFingerprint(value) {
  switch (typeof value) {
    case 'undefined': return 'undefined';
    case 'string': return `string:${JSON.stringify(value)}`;
    case 'number': return numberFingerprint(value);
    case 'boolean': return `boolean:${value ? 'true' : 'false'}`;
    case 'bigint': return `bigint:${value.toString()}`;
    case 'symbol': return `symbol:${symbolFingerprint(value)}`;
    case 'function': return `function:${functionFingerprint(value)}`;
    default: return value === null ? 'null' : 'object';
  }
}

function prototypeFingerprint(prototype) {
  if (prototype === null) return 'null';
  try {
    const constructorDescriptor = Object.getOwnPropertyDescriptor(prototype, 'constructor');
    if (constructorDescriptor && Object.hasOwn(constructorDescriptor, 'value')) {
      // A non-function constructor value is untrusted input.  In particular,
      // String(value) could invoke user-defined coercion hooks or getters.
      // The fallback fingerprint must be both side-effect free and reproducible
      // after a JSON snapshot, so it only records a safe primitive tag here.
      return `constructor-value:${descriptorValueFingerprint(constructorDescriptor.value)}`;
    }
    return 'constructor:inherited-or-accessor';
  } catch {
    return 'uninspectable-prototype';
  }
}

function opaqueValueFingerprint(value) {
  try {
    return `date:${numberFingerprint(Date.prototype.getTime.call(value))}`;
  } catch {
    // Not a Date, or an object which does not safely expose a Date value.
  }
  try {
    return `boxed-number:${numberFingerprint(Number.prototype.valueOf.call(value))}`;
  } catch {
    // Not a boxed Number.
  }
  try {
    return `boxed-string:${JSON.stringify(String.prototype.valueOf.call(value))}`;
  } catch {
    // Not a boxed String.
  }
  try {
    return `boxed-boolean:${Boolean.prototype.valueOf.call(value)}`;
  } catch {
    // Not a boxed Boolean.
  }
  try {
    return `boxed-bigint:${BigInt.prototype.valueOf.call(value).toString()}`;
  } catch {
    // Not a boxed BigInt.
  }
  try {
    return `boxed-symbol:${symbolFingerprint(Symbol.prototype.valueOf.call(value))}`;
  } catch {
    // No safely readable intrinsic value is available.
  }
  return null;
}

export function boundaryFingerprint(value) {
  const seen = new Map();
  const encodeKey = (key) => typeof key === 'string' ? `key:${JSON.stringify(key)}` : `symbol-key:${symbolFingerprint(key)}`;
  const encode = (entry, path) => {
    if (entry === null) return 'null';
    if (typeof entry === 'string' || typeof entry === 'boolean') return `${typeof entry}:${JSON.stringify(entry)}`;
    if (typeof entry === 'number') return numberFingerprint(entry);
    if (typeof entry === 'bigint') return `bigint:${entry.toString()}`;
    if (typeof entry === 'undefined') return 'undefined';
    if (typeof entry === 'symbol') return `symbol:${symbolFingerprint(entry)}`;
    if (typeof entry === 'function') return `function:${functionFingerprint(entry)}`;
    if (seen.has(entry)) return `reference:${seen.get(entry)}`;
    seen.set(entry, path);
    try {
      const prototype = Object.getPrototypeOf(entry);
      const opaqueValue = !Array.isArray(entry) && prototype !== Object.prototype && prototype !== null
        ? opaqueValueFingerprint(entry)
        : null;
      if (opaqueValue) return `opaque-object:${prototypeFingerprint(prototype)}:${opaqueValue}`;
      const descriptors = Object.getOwnPropertyDescriptors(entry);
      const keys = Reflect.ownKeys(descriptors).sort((left, right) => encodeKey(left).localeCompare(encodeKey(right)));
      const members = keys.map((key) => {
        const descriptor = descriptors[key];
        const keyToken = encodeKey(key);
        if (Object.hasOwn(descriptor, 'value')) return `${keyToken}=${encode(descriptor.value, `${path}/${keyToken}`)}`;
        return `${keyToken}=accessor:${descriptor.get ? functionFingerprint(descriptor.get) : 'none'}:${descriptor.set ? functionFingerprint(descriptor.set) : 'none'}`;
      });
      const kind = Array.isArray(entry) ? 'array' : prototype === Object.prototype || prototype === null ? 'object' : `opaque-object:${prototypeFingerprint(prototype)}`;
      return `${kind}:{${members.join(',')}}`;
    } catch {
      return 'uninspectable-object';
    }
  };
  return encode(value, '$');
}
