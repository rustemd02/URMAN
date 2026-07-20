import type { RuntimeState, StateEffect } from '../contracts/runtime-contracts.mjs';

export class StateStore {
  constructor(initialState?: RuntimeState);
  select<T>(selector: (state: RuntimeState) => T): T;
  snapshot(): RuntimeState;
  reduceTransaction(effects: readonly StateEffect[]): RuntimeState;
}
