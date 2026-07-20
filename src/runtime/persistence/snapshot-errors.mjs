import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

export const SnapshotErrorCode = Object.freeze({
  UnsupportedSnapshotVersion: 'UnsupportedSnapshotVersion',
  CampaignMismatch: 'CampaignMismatch',
  CapabilitySnapshotIncompatible: 'CapabilitySnapshotIncompatible',
  SnapshotIntegrityError: 'SnapshotIntegrityError',
});

/**
 * Persistence failures are intentionally machine-readable. Callers can offer
 * a clean-reset path without attempting to load only the fields they happen
 * to understand.
 */
export class SnapshotCodecError extends Error {
  constructor(code, message, details = undefined) {
    super(message);
    this.name = this.constructor.name;
    this.code = code;
    if (details !== undefined) this.details = clonePersistedJsonValue(details, 'Snapshot error details');
  }
}

export class UnsupportedSnapshotVersion extends SnapshotCodecError {
  constructor(version) {
    super(
      SnapshotErrorCode.UnsupportedSnapshotVersion,
      `Game snapshot schema version ${String(version)} is unsupported.`,
      { version },
    );
  }
}

export class CampaignMismatch extends SnapshotCodecError {
  constructor(expectedFingerprint, actualFingerprint) {
    super(
      SnapshotErrorCode.CampaignMismatch,
      'Saved campaign lock does not match the selected campaign.',
      { expectedFingerprint, actualFingerprint },
    );
  }
}

export class CapabilitySnapshotIncompatible extends SnapshotCodecError {
  constructor(capabilityInstanceId, reason, details = {}) {
    super(
      SnapshotErrorCode.CapabilitySnapshotIncompatible,
      `Capability snapshot ${capabilityInstanceId} is incompatible: ${reason}.`,
      { capabilityInstanceId, reason, ...details },
    );
  }
}

export class SnapshotIntegrityError extends SnapshotCodecError {
  constructor(message, details = undefined) {
    super(SnapshotErrorCode.SnapshotIntegrityError, message, details);
  }
}
