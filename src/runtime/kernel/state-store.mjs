import { clonePersistedJsonValue, deepFreeze } from '../contracts/json-value.mjs';

function immutableClone(value) { return deepFreeze(structuredClone(value)); }

function validKey(key) {
  return typeof key === 'string' && key.length > 0 && !['__proto__', 'constructor', 'prototype'].includes(key);
}

export class StateStore {
  #state;

  constructor(initialState = {}) {
    if (!initialState || typeof initialState !== 'object' || Array.isArray(initialState)) {
      throw new TypeError('Runtime initial state must be a JSON object.');
    }
    this.#state = clonePersistedJsonValue(initialState, 'Runtime initial state');
  }

  select(selector) {
    if (typeof selector !== 'function') throw new TypeError('selector must be a function.');
    return immutableClone(selector(this.#state));
  }

  snapshot() {
    return immutableClone(this.#state);
  }

  reduceTransaction(effects) {
    const draft = structuredClone(this.#state);
    for (const effect of effects) {
      if (!effect || !validKey(effect.key)) throw new TypeError('State effect key is invalid.');
      if (effect.op === 'state.set') {
        if (!Object.hasOwn(effect, 'value')) throw new TypeError('state.set requires value.');
        draft[effect.key] = clonePersistedJsonValue(effect.value, `State effect ${effect.key}`);
      } else if (effect.op === 'state.delete') {
        delete draft[effect.key];
      } else if (effect.op === 'state.increment') {
        if (!Number.isFinite(effect.delta)) throw new TypeError('state.increment requires finite delta.');
        const current = Object.hasOwn(draft, effect.key) ? draft[effect.key] : 0;
        if (typeof current !== 'number' || !Number.isFinite(current)) throw new TypeError(`Cannot increment non-number state ${effect.key}.`);
        draft[effect.key] = current + effect.delta;
      } else {
        throw new TypeError(`Unknown state effect ${effect.op}.`);
      }
    }
    this.#state = clonePersistedJsonValue(draft, 'Runtime state');
    return this.#state;
  }
}
