function assertTick(value, label) {
  if (!Number.isSafeInteger(value) || value < 0) {
    throw new TypeError(`${label} must be a safe non-negative integer.`);
  }
  return value;
}

/**
 * Serializable game time. It deliberately has no wall-clock API: authored
 * progression advances it only through an explicit kernel-owned action.
 */
export class LogicalClock {
  #tick;

  constructor({ tick = 0 } = {}) {
    this.#tick = assertTick(tick, 'Logical clock tick');
  }

  now() { return this.#tick; }

  advance(delta = 1) {
    assertTick(delta, 'Logical clock delta');
    if (delta > Number.MAX_SAFE_INTEGER - this.#tick) {
      throw new RangeError('Logical clock would exceed the safe integer range.');
    }
    this.#tick += delta;
    return this.#tick;
  }

  exportSnapshot() {
    return Object.freeze({ tick: this.#tick });
  }

  static fromSnapshot(snapshot) {
    if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot)
      || Object.keys(snapshot).length !== 1 || !Object.hasOwn(snapshot, 'tick')) {
      throw new TypeError('Logical clock snapshot must contain exactly tick.');
    }
    return new LogicalClock({ tick: snapshot.tick });
  }
}
