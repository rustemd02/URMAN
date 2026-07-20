import type { JsonValue } from '../contracts/runtime-contracts.mjs';
import type { RuntimePersistencePort } from '../bootstrap/runtime-bootstrap.mjs';
import type { GameSnapshotV2 } from './game-snapshot-v2.mjs';

export interface BrowserStoragePort {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}
export const PRODUCTION_SAVE_KEY: 'urman.mvp.save.v2';
export const LEGACY_RESET_MARKER_KEY: 'urman.persistence.reset.v2';
export const LEGACY_RESET_MARKER_VALUE: 'completed';
export const LEGACY_GAMEPLAY_STORAGE_KEYS: readonly [
  'urman.mvp.save.v1',
  'urman.oldPcHub.state.v1',
  'game.notepad.files.v1',
  'game.notepad.activeFile.v1',
];
export const RETIRED_CAPABILITY_OR_TEST_STORAGE_KEYS: readonly [];
export const LEGACY_RESET_KEYS: readonly string[];
export const LEGACY_RESET_NOTICE: string;
export interface LegacyResetProposal {
  readonly action: 'reset-pre-mvp-save';
  readonly reason: string;
  readonly message: string;
  readonly markerKey: typeof LEGACY_RESET_MARKER_KEY;
  readonly retiredKeys: readonly string[];
  readonly preservedKeys: readonly ['urman_username', 'urman.content-lab.v1'];
  readonly notice: string;
  readonly requiresExplicitConfirmation: false;
  readonly details?: JsonValue;
}
export interface CurrentV2ResetProposal {
  readonly action: 'reset-current-v2-save';
  readonly reason: string;
  readonly message: string;
  readonly saveKey: string;
  readonly preservedKeys: readonly [typeof LEGACY_RESET_MARKER_KEY, 'urman_username', 'urman.content-lab.v1'];
  readonly requiresExplicitConfirmation: true;
  readonly details?: JsonValue;
}
export type PersistenceLoadResult =
  | { readonly status: 'missing' }
  | { readonly status: 'ready'; readonly snapshot: GameSnapshotV2 }
  | { readonly status: 'reset-required'; readonly reason: string; readonly message: string; readonly reset: CurrentV2ResetProposal };
export interface ProductionPersistenceGateway {
  readonly saveKey: string;
  describeLegacyReset(reason?: string): LegacyResetProposal;
  save(): { readonly status: 'saved'; readonly snapshot: GameSnapshotV2; readonly campaignFingerprint: string };
  load(): PersistenceLoadResult;
  restore(): Promise<PersistenceLoadResult | { readonly status: 'restored' }>;
  applyLegacyReset():
    | { readonly status: 'reset-applied'; readonly notice: string }
    | { readonly status: 'already-reset'; readonly notice: null }
    | { readonly status: 'reset-failed'; readonly message: string; readonly reset: LegacyResetProposal };
  discardCurrentV2Save(proposal: CurrentV2ResetProposal):
    | { readonly status: 'current-v2-save-discarded' }
    | { readonly status: 'current-v2-save-discard-failed'; readonly message: string }
    | { readonly status: 'current-v2-save-discard-rejected'; readonly message: string };
}
export function createProductionPersistenceGateway(options: { readonly storage: BrowserStoragePort; readonly runtimePersistence: RuntimePersistencePort; readonly saveKey?: string }): ProductionPersistenceGateway;
export function createBrowserProductionPersistenceGateway(options: { readonly browserWindow: object; readonly runtimePersistence: RuntimePersistencePort; readonly saveKey?: string }): ProductionPersistenceGateway;
