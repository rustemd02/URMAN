function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function uint32(value, label) {
  if (!Number.isInteger(value) || value < 0 || value > 0xffffffff) {
    throw new TypeError(`${label} must be an unsigned 32-bit integer.`);
  }
  return value >>> 0;
}

function hashText(text) {
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash || 0x6d2b79f5;
}

function normalizeSeed(seed, label = 'RNG seed') {
  if (typeof seed === 'string') return hashText(nonEmptyString(seed, label));
  return uint32(seed, label) || 0x6d2b79f5;
}

function step(state) {
  let next = state >>> 0;
  next ^= next << 13;
  next ^= next >>> 17;
  next ^= next << 5;
  return next >>> 0;
}

export class SeededRngStream {
  #seed;
  #state;
  #position;

  constructor({ seed, state = undefined, position = 0 } = {}) {
    this.#seed = normalizeSeed(seed);
    this.#state = state === undefined ? this.#seed : uint32(state, 'RNG state');
    this.#position = Number.isSafeInteger(position) && position >= 0
      ? position
      : (() => { throw new TypeError('RNG position must be a safe non-negative integer.'); })();
  }

  nextUint32() {
    if (this.#position === Number.MAX_SAFE_INTEGER) throw new RangeError('RNG position would exceed the safe integer range.');
    this.#state = step(this.#state);
    this.#position += 1;
    return this.#state;
  }

  nextFloat() { return this.nextUint32() / 0x1_0000_0000; }
  get position() { return this.#position; }

  exportSnapshot() {
    return Object.freeze({ seed: this.#seed, state: this.#state, position: this.#position });
  }
}

/** Owner-scoped streams make quest ordering irrelevant to generated outcomes. */
export class OwnerRngStreams {
  #masterSeed;
  #streams = new Map();

  constructor({ seed = 0 } = {}) {
    this.#masterSeed = normalizeSeed(seed, 'RNG master seed');
  }

  stream(ownerId) {
    ownerId = nonEmptyString(ownerId, 'RNG owner ID');
    let stream = this.#streams.get(ownerId);
    if (!stream) {
      stream = new SeededRngStream({ seed: hashText(`${this.#masterSeed}:${ownerId}`) });
      this.#streams.set(ownerId, stream);
    }
    return stream;
  }

  exportSnapshot() {
    return Object.freeze({
      masterSeed: this.#masterSeed,
      streams: Object.freeze([...this.#streams.entries()]
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([ownerId, stream]) => Object.freeze({ ownerId, ...stream.exportSnapshot() }))),
    });
  }

  static fromSnapshot(snapshot) {
    if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot)
      || !Object.hasOwn(snapshot, 'masterSeed') || !Array.isArray(snapshot.streams)) {
      throw new TypeError('RNG streams snapshot is invalid.');
    }
    const result = new OwnerRngStreams({ seed: snapshot.masterSeed });
    const ownerIds = new Set();
    for (const entry of snapshot.streams) {
      if (!entry || typeof entry !== 'object' || Array.isArray(entry)
        || Object.keys(entry).sort().join(',') !== 'ownerId,position,seed,state') {
        throw new TypeError('RNG stream snapshot entry is invalid.');
      }
      const ownerId = nonEmptyString(entry.ownerId, 'RNG snapshot owner ID');
      if (ownerIds.has(ownerId)) throw new TypeError(`Duplicate RNG stream owner ${ownerId}.`);
      ownerIds.add(ownerId);
      result.#streams.set(ownerId, new SeededRngStream(entry));
    }
    return result;
  }
}
