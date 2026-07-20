import type { JsonValue } from '../contracts/runtime-contracts.mjs';
export const SnapshotErrorCode: Readonly<Record<'UnsupportedSnapshotVersion' | 'CampaignMismatch' | 'CapabilitySnapshotIncompatible' | 'SnapshotIntegrityError', string>>;
export class SnapshotCodecError extends Error { readonly code: string; readonly details?: JsonValue }
export class UnsupportedSnapshotVersion extends SnapshotCodecError { constructor(version: JsonValue) }
export class CampaignMismatch extends SnapshotCodecError { constructor(expectedFingerprint: string, actualFingerprint: string) }
export class CapabilitySnapshotIncompatible extends SnapshotCodecError { constructor(capabilityInstanceId: string, reason: string, details?: JsonValue) }
export class SnapshotIntegrityError extends SnapshotCodecError { constructor(message: string, details?: JsonValue) }
