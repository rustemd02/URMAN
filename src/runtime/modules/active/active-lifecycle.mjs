import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

function disposable(value) {
  return value && typeof value.dispose === 'function' ? value : null;
}

/** Owns host registrations made by a single active presentation session. */
export class ActiveLifecycle {
  #cleanups = new Set();
  #disposed = false;

  get disposed() { return this.#disposed; }

  add(value) {
    if (this.#disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Active lifecycle is already disposed.');
    const handle = disposable(value);
    if (!handle) throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Active lifecycle cleanup must expose dispose().');
    this.#cleanups.add(handle);
    return handle;
  }

  remove(value) {
    this.#cleanups.delete(value);
  }

  dispose() {
    if (this.#disposed) return;
    this.#disposed = true;
    const failures = [];
    for (const cleanup of [...this.#cleanups].reverse()) {
      try { cleanup.dispose(); } catch (error) { failures.push(error); }
    }
    this.#cleanups.clear();
    if (failures.length) throw failures[0];
  }
}
