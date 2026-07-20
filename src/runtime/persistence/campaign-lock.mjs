import { canonicalJson, clonePersistedJsonValue } from '../contracts/json-value.mjs';
import { CampaignMismatch, SnapshotIntegrityError } from './snapshot-errors.mjs';

const EXACT_VERSION = /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
const SHA256 = /^[a-f0-9]{64}$/;

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new SnapshotIntegrityError(`${label} must be a non-empty string.`);
  return value;
}

function exactVersion(value, label) {
  if (typeof value !== 'string' || !EXACT_VERSION.test(value)) throw new SnapshotIntegrityError(`${label} must be an exact version.`);
  return value;
}

function fingerprint(value, label) {
  if (typeof value !== 'string' || !SHA256.test(value)) throw new SnapshotIntegrityError(`${label} must be a SHA-256 fingerprint.`);
  return value;
}

function requiredObject(value, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new SnapshotIntegrityError(`${label} must be an object.`);
  return value;
}

function exactObject(value, label, keys) {
  requiredObject(value, label);
  const actual = Object.keys(value).sort();
  const expected = [...keys].sort();
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    throw new SnapshotIntegrityError(`${label} has an invalid shape.`);
  }
  return value;
}

function dependency(entry, label) {
  exactObject(entry, label, ['moduleId', 'exactVersion']);
  return Object.freeze({
    moduleId: nonEmptyString(entry.moduleId, `${label}.moduleId`),
    exactVersion: exactVersion(entry.exactVersion, `${label}.exactVersion`),
  });
}

function requirement(entry, label) {
  exactObject(entry, label, ['protocolId', 'exactVersion']);
  return Object.freeze({
    protocolId: nonEmptyString(entry.protocolId, `${label}.protocolId`),
    exactVersion: exactVersion(entry.exactVersion, `${label}.exactVersion`),
  });
}

function lockedModule(entry, label) {
  exactObject(entry, label, ['moduleId', 'exactVersion', 'sha256']);
  return Object.freeze({
    moduleId: nonEmptyString(entry.moduleId, `${label}.moduleId`),
    exactVersion: exactVersion(entry.exactVersion, `${label}.exactVersion`),
    sha256: fingerprint(entry.sha256, `${label}.sha256`),
  });
}

function unique(entries, field, label) {
  const seen = new Set();
  for (const entry of entries) {
    if (seen.has(entry[field])) throw new SnapshotIntegrityError(`${label} has duplicate ${field} ${entry[field]}.`);
    seen.add(entry[field]);
  }
}

/**
 * Creates the run lock from the already-resolved compiled pack. Module order
 * is intentionally retained even though fingerprints have canonical ordering:
 * campaign composition is part of the saved identity.
 */
export function createCampaignLock(pack) {
  let source;
  try {
    source = clonePersistedJsonValue(pack, 'Compiled content pack');
  } catch (error) {
    throw new SnapshotIntegrityError(error instanceof Error ? error.message : String(error));
  }
  requiredObject(source, 'Compiled content pack');
  const campaign = requiredObject(source.campaign, 'Compiled content pack.campaign');
  if (!Array.isArray(campaign.orderedModules) || !Array.isArray(campaign.capabilityRequirements) || !Array.isArray(source.moduleFingerprints)) {
    throw new SnapshotIntegrityError('Compiled content pack lock inputs are invalid.');
  }

  const modules = campaign.orderedModules.map((entry, index) => dependency(entry, `Compiled content pack.campaign.orderedModules[${index}]`));
  unique(modules, 'moduleId', 'Compiled content pack.campaign.orderedModules');
  const fingerprints = new Map();
  for (const [index, entry] of source.moduleFingerprints.entries()) {
    const normalized = lockedModule(entry, `Compiled content pack.moduleFingerprints[${index}]`);
    if (fingerprints.has(normalized.moduleId)) throw new SnapshotIntegrityError(`Compiled content pack has duplicate module fingerprint ${normalized.moduleId}.`);
    fingerprints.set(normalized.moduleId, normalized);
  }
  if (fingerprints.size !== modules.length) throw new SnapshotIntegrityError('Compiled content pack module fingerprints do not exactly match campaign modules.');
  const lockedModules = modules.map((entry) => {
    const moduleFingerprint = fingerprints.get(entry.moduleId);
    if (!moduleFingerprint || moduleFingerprint.exactVersion !== entry.exactVersion) {
      throw new SnapshotIntegrityError(`Compiled content pack fingerprint does not match module ${entry.moduleId}.`);
    }
    return moduleFingerprint;
  });

  const capabilities = campaign.capabilityRequirements
    .map((entry, index) => requirement(entry, `Compiled content pack.campaign.capabilityRequirements[${index}]`))
    .sort((left, right) => left.protocolId.localeCompare(right.protocolId) || left.exactVersion.localeCompare(right.exactVersion));
  unique(capabilities, 'protocolId', 'Compiled content pack.campaign.capabilityRequirements');

  return Object.freeze({
    campaignId: nonEmptyString(campaign.id, 'Compiled content pack.campaign.id'),
    campaignExactVersion: exactVersion(campaign.exactVersion, 'Compiled content pack.campaign.exactVersion'),
    modules: Object.freeze(lockedModules),
    capabilities: Object.freeze(capabilities),
    campaignFingerprint: fingerprint(source.campaignFingerprint, 'Compiled content pack.campaignFingerprint'),
  });
}

export function normalizeCampaignLock(lock) {
  let source;
  try {
    source = clonePersistedJsonValue(lock, 'Campaign lock');
  } catch (error) {
    throw new SnapshotIntegrityError(error instanceof Error ? error.message : String(error));
  }
  exactObject(source, 'Campaign lock', ['campaignId', 'campaignExactVersion', 'modules', 'capabilities', 'campaignFingerprint']);
  if (!Array.isArray(source.modules) || !Array.isArray(source.capabilities)) throw new SnapshotIntegrityError('Campaign lock modules and capabilities must be arrays.');
  const modules = source.modules.map((entry, index) => lockedModule(entry, `Campaign lock.modules[${index}]`));
  const capabilities = source.capabilities.map((entry, index) => requirement(entry, `Campaign lock.capabilities[${index}]`));
  unique(modules, 'moduleId', 'Campaign lock.modules');
  unique(capabilities, 'protocolId', 'Campaign lock.capabilities');
  return Object.freeze({
    campaignId: nonEmptyString(source.campaignId, 'Campaign lock.campaignId'),
    campaignExactVersion: exactVersion(source.campaignExactVersion, 'Campaign lock.campaignExactVersion'),
    modules: Object.freeze(modules),
    capabilities: Object.freeze(capabilities),
    campaignFingerprint: fingerprint(source.campaignFingerprint, 'Campaign lock.campaignFingerprint'),
  });
}

export function assertCampaignLockMatches(lock, pack) {
  const actual = normalizeCampaignLock(lock);
  const expected = createCampaignLock(pack);
  if (canonicalJson(actual, 'Campaign lock') !== canonicalJson(expected, 'Compiled content campaign lock')) {
    throw new CampaignMismatch(expected.campaignFingerprint, actual.campaignFingerprint);
  }
  return actual;
}
