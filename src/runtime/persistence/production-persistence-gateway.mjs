import { clonePersistedJsonValue } from '../contracts/json-value.mjs';
import { SnapshotCodecError, SnapshotIntegrityError } from './snapshot-errors.mjs';

export const PRODUCTION_SAVE_KEY = 'urman.mvp.save.v2';
export const LEGACY_RESET_MARKER_KEY = 'urman.persistence.reset.v2';
export const LEGACY_RESET_MARKER_VALUE = 'completed';
export const LEGACY_GAMEPLAY_STORAGE_KEYS = Object.freeze([
  'urman.mvp.save.v1',
  'urman.oldPcHub.state.v1',
  'game.notepad.files.v1',
  'game.notepad.activeFile.v1',
]);
// The MM-10 audit found no additional retired capability/test namespaces.
export const RETIRED_CAPABILITY_OR_TEST_STORAGE_KEYS = Object.freeze([]);
export const LEGACY_RESET_KEYS = Object.freeze([
  ...LEGACY_GAMEPLAY_STORAGE_KEYS,
  ...RETIRED_CAPABILITY_OR_TEST_STORAGE_KEYS,
]);
export const LEGACY_RESET_NOTICE = 'Прогресс ранней pre-MVP версии сброшен. Имя и настройки сохранены.';

function storagePort(storage) {
  if (!storage || typeof storage.getItem !== 'function' || typeof storage.setItem !== 'function' || typeof storage.removeItem !== 'function') {
    throw new TypeError('Production persistence requires a browser-like storage port with getItem, setItem and removeItem.');
  }
  return storage;
}

function frozen(value) {
  return Object.freeze(value);
}

function errorDetails(error) {
  return error.details === undefined ? undefined : clonePersistedJsonValue(error.details, 'Persistence error details');
}

function legacyResetProposal(reason, message, details = undefined) {
  const proposal = {
    action: 'reset-pre-mvp-save',
    reason,
    message,
    markerKey: LEGACY_RESET_MARKER_KEY,
    retiredKeys: LEGACY_RESET_KEYS,
    preservedKeys: Object.freeze(['urman_username', 'urman.content-lab.v1']),
    notice: LEGACY_RESET_NOTICE,
    requiresExplicitConfirmation: false,
  };
  if (details !== undefined) proposal.details = clonePersistedJsonValue(details, 'Reset proposal details');
  return frozen(proposal);
}

function currentV2ResetProposal(error, saveKey) {
  if (!(error instanceof SnapshotCodecError)) throw error;
  return frozen({
    action: 'reset-current-v2-save',
    reason: error.code,
    message: error.message,
    saveKey,
    preservedKeys: Object.freeze([
      LEGACY_RESET_MARKER_KEY,
      'urman_username',
      'urman.content-lab.v1',
    ]),
    requiresExplicitConfirmation: true,
    ...(errorDetails(error) === undefined ? {} : { details: errorDetails(error) }),
  });
}

function resetRequired(error, proposal) {
  return frozen({
    status: 'reset-required',
    reason: error.code,
    message: error.message,
    reset: proposal,
  });
}

function resetFailure(error) {
  return frozen({
    status: 'reset-failed',
    message: error instanceof Error ? error.message : String(error),
    reset: legacyResetProposal('StorageResetFailed', 'Не удалось полностью сбросить раннее сохранение.'),
  });
}

function parseStoredSnapshot(serialized) {
  if (typeof serialized !== 'string') {
    throw new SnapshotIntegrityError('Stored GameSnapshotV2 must be a JSON string.');
  }
  try {
    return JSON.parse(serialized);
  } catch (error) {
    throw new SnapshotIntegrityError(`Stored GameSnapshotV2 is invalid JSON: ${error instanceof Error ? error.message : String(error)}`);
  }
}

/**
 * The single production persistence boundary. Its storage is injected by the
 * composition root; it never discovers a browser global. The runtime receives
 * only the closed persistence port and remains the sole live-state owner.
 */
export function createProductionPersistenceGateway({ storage, runtimePersistence, saveKey = PRODUCTION_SAVE_KEY } = {}) {
  const port = storagePort(storage);
  if (!runtimePersistence || typeof runtimePersistence.capture !== 'function' || typeof runtimePersistence.decode !== 'function' || typeof runtimePersistence.restore !== 'function') {
    throw new TypeError('Production persistence requires a RuntimePersistencePort with capture, decode and restore.');
  }
  if (typeof saveKey !== 'string' || !saveKey) throw new TypeError('Production persistence saveKey must be a non-empty string.');
  let pendingCurrentV2Reset = null;

  const registerCurrentV2Reset = (error) => {
    const proposal = currentV2ResetProposal(error, saveKey);
    pendingCurrentV2Reset = proposal;
    return resetRequired(error, proposal);
  };

  const gateway = {
    get saveKey() { return saveKey; },

    describeLegacyReset(reason = 'PreMvpSaveRetired') {
      return legacyResetProposal(reason, 'Для продолжения нужен чистый запуск с новым форматом сохранения.');
    },

    save() {
      const snapshot = runtimePersistence.capture();
      port.setItem(saveKey, JSON.stringify(snapshot));
      return frozen({
        status: 'saved',
        snapshot,
        campaignFingerprint: snapshot.campaignLock.campaignFingerprint,
      });
    },

    load() {
      pendingCurrentV2Reset = null;
      const serialized = port.getItem(saveKey);
      if (serialized === null) return frozen({ status: 'missing' });
      try {
        const snapshot = runtimePersistence.decode(parseStoredSnapshot(serialized));
        return frozen({ status: 'ready', snapshot });
      } catch (error) {
        if (error instanceof SnapshotCodecError) return registerCurrentV2Reset(error);
        throw error;
      }
    },

    async restore() {
      const loaded = gateway.load();
      if (loaded.status !== 'ready') return loaded;
      try {
        await runtimePersistence.restore(loaded.snapshot);
        return frozen({ status: 'restored' });
      } catch (error) {
        if (error instanceof SnapshotCodecError) return registerCurrentV2Reset(error);
        throw error;
      }
    },

    applyLegacyReset() {
      if (port.getItem(LEGACY_RESET_MARKER_KEY) === LEGACY_RESET_MARKER_VALUE) {
        return frozen({ status: 'already-reset', notice: null });
      }
      try {
        for (const key of LEGACY_RESET_KEYS) port.removeItem(key);
        port.setItem(LEGACY_RESET_MARKER_KEY, LEGACY_RESET_MARKER_VALUE);
        return frozen({ status: 'reset-applied', notice: LEGACY_RESET_NOTICE });
      } catch (error) {
        return resetFailure(error);
      }
    },

    discardCurrentV2Save(proposal) {
      if (proposal !== pendingCurrentV2Reset) {
        return frozen({
          status: 'current-v2-save-discard-rejected',
          message: 'Current V2 save discard requires the pending reset proposal.',
        });
      }
      try {
        port.removeItem(saveKey);
        pendingCurrentV2Reset = null;
        return frozen({ status: 'current-v2-save-discarded' });
      } catch (error) {
        return frozen({
          status: 'current-v2-save-discard-failed',
          message: error instanceof Error ? error.message : String(error),
        });
      }
    },
  };

  return frozen(gateway);
}

/** The one browser-only storage adapter. Its host is injected by `main`. */
export function createBrowserProductionPersistenceGateway({ browserWindow, runtimePersistence, saveKey = PRODUCTION_SAVE_KEY } = {}) {
  if (!browserWindow || typeof browserWindow !== 'object') {
    throw new TypeError('Browser production persistence requires an injected browser host.');
  }
  return createProductionPersistenceGateway({
    storage: browserWindow.localStorage,
    runtimePersistence,
    saveKey,
  });
}
