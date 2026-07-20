/**
 * Content Lab must not drop the last owner reference when its capability
 * cleanup fails. A replacement is therefore created only after disposal.
 */
export function replaceActiveLabRun(activeRun, dispose, create) {
  if (activeRun !== null) {
    try {
      dispose(activeRun);
    } catch (error) {
      return Object.freeze({ replaced: false, activeRun, error });
    }
  }
  return Object.freeze({ replaced: true, activeRun: create(), error: null });
}
