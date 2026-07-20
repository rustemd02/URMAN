export const CANARY_SIGNAL_PROTOCOL = 'canary.core:capability/signal';
export const CANARY_SIGNAL_VERSION = '1.0.0';
export const CANARY_SIGNAL_OUTCOME = 'canary.core:outcome/signal-resolved';

/**
 * A deliberately small provider used only by the foundation canary. Its
 * scheduled job is data-only; the test dispatches and acknowledges it through
 * the kernel, which keeps the mock faithful to the production contract.
 */
export function createCanarySignalDescriptor({ trace = [] } = {}) {
  return Object.freeze({
    protocolId: CANARY_SIGNAL_PROTOCOL,
    exactVersion: CANARY_SIGNAL_VERSION,
    stateSchemaVersion: 1,
    validateConfig: (config) => config?.mode === 'accessible',
    create: ({ capabilityInstanceId, context }) => {
      let handled = 0;
      let scheduled = false;
      return {
        restore: (state) => {
          handled = state.handled;
          scheduled = state.scheduled;
          trace.push(`restore:${capabilityInstanceId}`);
        },
        start: () => {
          trace.push(`start:${capabilityInstanceId}`);
          context.subscribe('canary.runtime.pulse', ({ payload }) => trace.push(`pulse:${payload.id}`));
          if (!scheduled) {
            context.scheduler.schedule({
              jobId: `${capabilityInstanceId}:signal-due`,
              dueTick: context.clock.now() + 3,
              payload: { outcomeId: CANARY_SIGNAL_OUTCOME },
            });
            scheduled = true;
          }
        },
        handle: ({ mode = 'standard' } = {}) => {
          handled += 1;
          return {
            outcomeId: CANARY_SIGNAL_OUTCOME,
            mode,
            nonce: context.rng.nextUint32(),
          };
        },
        snapshot: () => ({ handled, scheduled }),
        stop: () => trace.push(`stop:${capabilityInstanceId}`),
        dispose: () => trace.push(`dispose:${capabilityInstanceId}`),
      };
    },
  });
}
