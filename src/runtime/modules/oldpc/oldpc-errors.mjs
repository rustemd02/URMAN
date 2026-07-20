export const OldPcErrorCode = Object.freeze({
  InvalidContext: 'InvalidOldPcContext',
  InvalidDefinition: 'InvalidOldPcDefinition',
  MissingDocument: 'MissingOldPcDocument',
  LockedDocument: 'LockedOldPcDocument',
  InvalidInput: 'InvalidOldPcInput',
  InvalidSnapshot: 'InvalidOldPcSnapshot',
  Disposed: 'OldPcDisposed',
});

export class OldPcError extends Error {
  constructor(code, message, details = {}) {
    super(message);
    this.name = 'OldPcError';
    this.code = code;
    this.details = Object.freeze({ ...details });
  }
}

export function oldPcError(code, message, details = {}) {
  return new OldPcError(code, message, details);
}
