import { clonePersistedJsonValue } from './json-value.mjs';

export const CommandStatus = Object.freeze({
  Committed: 'committed',
  Rejected: 'rejected',
});

export const RuntimeErrorCode = Object.freeze({
  Preflight: 'PreflightError',
  CommandRejected: 'CommandRejected',
  DuplicateOccurrence: 'DuplicateOccurrence',
  OccurrenceConflict: 'OccurrenceConflict',
  ResourceConflict: 'ResourceConflict',
  UnknownCommand: 'UnknownCommand',
});

export class RuntimeFailure extends Error {
  constructor(code, message, details = undefined) {
    super(message);
    this.name = this.constructor.name;
    this.code = code;
    if (details !== undefined) this.details = clonePersistedJsonValue(details, 'Runtime error details');
  }
}

export class PreflightError extends RuntimeFailure {
  constructor(message, details = undefined) { super(RuntimeErrorCode.Preflight, message, details); }
}

export class ConflictError extends RuntimeFailure {
  constructor(message, details = undefined) { super(RuntimeErrorCode.ResourceConflict, message, details); }
}

export class UnknownCommandError extends RuntimeFailure {
  constructor(type) { super(RuntimeErrorCode.UnknownCommand, `Unknown command ${type}.`, { type }); }
}

export class DuplicateOccurrence extends RuntimeFailure {
  constructor(actionOccurrenceId, savedResult) {
    super(RuntimeErrorCode.DuplicateOccurrence, `Occurrence ${actionOccurrenceId} was already processed.`, { actionOccurrenceId });
    this.savedResult = savedResult;
  }
}

export class OccurrenceConflict extends RuntimeFailure {
  constructor(actionOccurrenceId) {
    super(RuntimeErrorCode.OccurrenceConflict, `Occurrence ${actionOccurrenceId} has a different command fingerprint.`, { actionOccurrenceId });
  }
}

export const RegistryErrorCode = Object.freeze({
  DuplicateRegistration: 'DuplicateRegistration',
  InvalidRegistration: 'InvalidRegistration',
  IncompatibleExactVersion: 'IncompatibleExactVersion',
  MissingRegistration: 'MissingRegistration',
});
