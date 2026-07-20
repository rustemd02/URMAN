export const ActiveProviderErrorCode = Object.freeze({
  InvalidContext: 'InvalidActiveProviderContext',
  InvalidInput: 'InvalidActiveProviderInput',
  MissingDefinition: 'MissingActiveProviderDefinition',
  Disposed: 'ActiveProviderDisposed',
});

export class ActiveProviderError extends Error {
  constructor(code, message, details = {}) {
    super(message);
    this.name = 'ActiveProviderError';
    this.code = code;
    this.details = Object.freeze({ ...details });
  }
}

export function activeProviderError(code, message, details = {}) {
  return new ActiveProviderError(code, message, details);
}
