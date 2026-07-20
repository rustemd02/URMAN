import { clonePersistedJsonValue } from '../contracts/json-value.mjs';

const DEFAULT_STATE_KEY = 'world.evidence';

function nonEmptyString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

function exactObject(value, label, keys) {
  if (!value || typeof value !== 'object' || Array.isArray(value)
    || Object.keys(value).sort().join(',') !== [...keys].sort().join(',')) {
    throw new TypeError(`${label} has an invalid shape.`);
  }
  return value;
}

function normalizeEvidence(entry, index) {
  exactObject(entry, `Evidence claim ${index}`, ['evidenceId', 'claimantId', 'sourceId', 'provenanceChain', 'metadata']);
  if (!Array.isArray(entry.provenanceChain) || entry.provenanceChain.length === 0) throw new TypeError(`Evidence claim ${index} needs provenance.`);
  return Object.freeze({
    evidenceId: nonEmptyString(entry.evidenceId, `Evidence claim ${index}.evidenceId`),
    claimantId: nonEmptyString(entry.claimantId, `Evidence claim ${index}.claimantId`),
    sourceId: nonEmptyString(entry.sourceId, `Evidence claim ${index}.sourceId`),
    provenanceChain: Object.freeze(entry.provenanceChain.map((sourceId, chainIndex) => nonEmptyString(sourceId, `Evidence claim ${index}.provenanceChain[${chainIndex}]`))),
    metadata: clonePersistedJsonValue(entry.metadata, `Evidence claim ${index}.metadata`),
  });
}

function currentEvidence(state, stateKey) {
  if (!state || typeof state !== 'object' || Array.isArray(state)) throw new TypeError('Evidence state source must be an object.');
  const records = Object.hasOwn(state, stateKey) ? state[stateKey] : [];
  if (!Array.isArray(records)) throw new TypeError(`Evidence state ${stateKey} must be an array.`);
  const byId = new Map();
  for (const [index, entry] of records.entries()) {
    const normalized = normalizeEvidence(entry, index);
    if (byId.has(normalized.evidenceId)) throw new TypeError(`Duplicate evidence claim ${normalized.evidenceId}.`);
    byId.set(normalized.evidenceId, normalized);
  }
  return byId;
}

/** Records a source chain rather than inventing a second owner for evidence. */
export function planEvidenceClaim({ state, evidenceId, claimantId, sourceId, metadata = null, stateKey = DEFAULT_STATE_KEY, eventType = 'world.evidence.claimed' } = {}) {
  stateKey = nonEmptyString(stateKey, 'Evidence state key');
  evidenceId = nonEmptyString(evidenceId, 'Evidence ID');
  claimantId = nonEmptyString(claimantId, 'Evidence claimant ID');
  sourceId = nonEmptyString(sourceId, 'Evidence source ID');
  eventType = nonEmptyString(eventType, 'Evidence event type');
  const evidence = currentEvidence(state, stateKey);
  if (evidence.has(evidenceId)) throw new TypeError(`Evidence claim ${evidenceId} already exists.`);
  if (sourceId === evidenceId) throw new TypeError('Evidence cannot cite itself as its source.');
  const source = evidence.get(sourceId);
  const provenanceChain = source
    ? [...source.provenanceChain, source.evidenceId]
    : [sourceId];
  if (provenanceChain.includes(evidenceId)) throw new TypeError('Evidence provenance cycle is not allowed.');
  const record = Object.freeze({
    evidenceId,
    claimantId,
    sourceId,
    provenanceChain: Object.freeze(provenanceChain),
    metadata: clonePersistedJsonValue(metadata, 'Evidence metadata'),
  });
  const next = clonePersistedJsonValue([...evidence.values(), record].sort((left, right) => left.evidenceId.localeCompare(right.evidenceId)), 'Evidence claim result');
  return Object.freeze({
    effects: Object.freeze([{ op: 'state.set', key: stateKey, value: next }]),
    events: Object.freeze([{ type: eventType, payload: Object.freeze({ evidenceId, claimantId, sourceId, provenanceChain: Object.freeze(provenanceChain) }) }]),
    value: record,
  });
}
