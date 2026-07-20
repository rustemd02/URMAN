import { deepFreeze } from '../../contracts/json-value.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

function nonEmptyString(value, field) {
  if (typeof value !== 'string' || !value) {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `${field} must be a non-empty string.`);
  }
  return value;
}

export function activeSessionScope(value) {
  return nonEmptyString(value, 'Active session scope');
}

export function activeActionOccurrenceId(scope, action, sequence) {
  nonEmptyString(scope, 'Active session scope');
  nonEmptyString(action, 'Active action');
  if (!Number.isSafeInteger(sequence) || sequence < 1) {
    throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Active action sequence must be a positive safe integer.');
  }
  return `${scope}:${action}:${sequence}`;
}

export function requireRuntimeContext(context) {
  if (!context || typeof context !== 'object'
    || typeof context.dispatch !== 'function'
    || typeof context.select !== 'function'
    || typeof context.subscribe !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Active provider requires a RuntimeContext.');
  }
  return context;
}

export function requireResolver(resolver, method, label) {
  if (!resolver || typeof resolver[method] !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `${label} must expose ${method}().`);
  }
  return resolver;
}

export function requireContentRegistry(registry) {
  if (!registry || typeof registry.get !== 'function' || typeof registry.category !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Active provider requires a ContentRegistry.');
  }
  return registry;
}

export function normalizePresentationHost(host = {}) {
  if (!host || typeof host !== 'object' || Array.isArray(host)) {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Presentation host must be an object.');
  }
  const noOp = () => undefined;
  const normalized = {
    present: typeof host.present === 'function' ? host.present : noOp,
    subscribeInput: typeof host.subscribeInput === 'function' ? host.subscribeInput : null,
    navigate: typeof host.navigate === 'function' ? host.navigate : noOp,
    handoff: typeof host.handoff === 'function' ? host.handoff : noOp,
    startMedia: typeof host.startMedia === 'function' ? host.startMedia : null,
    reportFailure: typeof host.reportFailure === 'function' ? host.reportFailure : noOp,
  };
  return Object.freeze(normalized);
}

/** Prevents host event loops from retaining a rejected provider promise. */
export function containedInputHandler(host, handler) {
  if (typeof handler !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Input handler must be a function.');
  }
  return async (input) => {
    try {
      return await handler(input);
    } catch (error) {
      host.reportFailure(error);
      return Object.freeze({
        status: 'rejected',
        error: Object.freeze({
          code: typeof error?.code === 'string' ? error.code : ActiveProviderErrorCode.InvalidInput,
          message: error instanceof Error ? error.message : 'Active provider input failed.',
        }),
      });
    }
  };
}

export function immutableModel(value) {
  return deepFreeze(structuredClone(value));
}
