import { ActiveLifecycle } from './active-lifecycle.mjs';
import { immutableModel, requireRuntimeContext } from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

/** Projects caller-supplied kernel data; it does not own journal state. */
export class JournalProjection {
  #runtime;
  #selector;
  #lifecycle = new ActiveLifecycle();

  constructor(runtimeContext, selector = () => []) {
    this.#runtime = requireRuntimeContext(runtimeContext);
    if (typeof selector !== 'function') throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Journal selector must be a function.');
    this.#selector = selector;
  }

  render() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot render a disposed journal projection.');
    return immutableModel({ kind: 'journal', entries: this.#runtime.select(this.#selector) });
  }

  dispose() { this.#lifecycle.dispose(); }
}
