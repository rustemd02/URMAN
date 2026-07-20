import { cloneJsonValue, deepFreeze } from '../../contracts/json-value.mjs';
import { OldPcErrorCode, oldPcError } from './oldpc-errors.mjs';

const OLD_PC_TYPES = new Set([
  'document',
  'record',
  'message',
  'tatarwiki_article',
  'folder_note',
  'corrupted_fragment',
]);
const CANON_STATUSES = new Set(['canon', 'soft_canon', 'hypothesis', 'proposal', 'in_world_lie']);
const RELIABILITY_STATUSES = new Set([
  'official_lie',
  'partial_truth',
  'personal_memory',
  'village_record',
  'pact_record',
  'folklore_mask',
  'corrupted',
  'unverified',
]);

function nonEmptyString(value, label, details = {}) {
  if (typeof value !== 'string' || !value) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, `${label} must be a non-empty string.`, details);
  }
  return value;
}

function requireArray(value, label, details = {}) {
  if (!Array.isArray(value) || value.some((entry) => typeof entry !== 'string' || !entry)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, `${label} must be an array of non-empty strings.`, details);
  }
  return value;
}

function requireJsonArray(value, label, details = {}) {
  if (!Array.isArray(value)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, `${label} must be an array.`, details);
  }
  return value;
}

function ownData(value, field, details = {}) {
  const descriptor = value && typeof value === 'object' && !Array.isArray(value)
    ? Object.getOwnPropertyDescriptor(value, field)
    : null;
  if (!descriptor || !Object.hasOwn(descriptor, 'value')) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, `${field} must be an own data field.`, details);
  }
  return descriptor.value;
}

function localizedDefault(value, details) {
  const localized = ownData(value, 'title', details);
  if (!localized || typeof localized !== 'object' || Array.isArray(localized)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, 'Document title must be localized text.', details);
  }
  return nonEmptyString(ownData(localized, 'default', details), 'Document title.default', details);
}

function metadata(value, documentId) {
  const details = { documentId };
  const oldPc = ownData(value, 'oldPc', details);
  if (!oldPc || typeof oldPc !== 'object' || Array.isArray(oldPc)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, 'Old-PC document metadata must be an object.', details);
  }
  const type = nonEmptyString(ownData(oldPc, 'type', details), 'oldPc.type', details);
  const pcSection = nonEmptyString(ownData(oldPc, 'pcSection', details), 'oldPc.pcSection', details);
  const canonStatus = nonEmptyString(ownData(oldPc, 'canonStatus', details), 'oldPc.canonStatus', details);
  const reliability = nonEmptyString(ownData(oldPc, 'reliability', details), 'oldPc.reliability', details);
  const searchTerms = requireArray(ownData(oldPc, 'searchTerms', details), 'oldPc.searchTerms', details);
  const suggestedTerms = requireArray(ownData(oldPc, 'suggestedTerms', details), 'oldPc.suggestedTerms', details);
  if (!OLD_PC_TYPES.has(type) || !CANON_STATUSES.has(canonStatus) || !RELIABILITY_STATUSES.has(reliability)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, 'Old-PC document metadata has an unknown enum value.', details);
  }
  return Object.freeze({
    type,
    pcSection,
    canonStatus,
    reliability,
    searchTerms: Object.freeze([...new Set(searchTerms)]),
    suggestedTerms: Object.freeze([...new Set(suggestedTerms)]),
  });
}

function modulePrefix(moduleId) {
  nonEmptyString(moduleId, 'Old-PC module ID');
  return `${moduleId}:document/`;
}

function normalizedDocument(value, moduleId) {
  const id = nonEmptyString(ownData(value, 'id'), 'Document ID');
  if (!id.startsWith(modulePrefix(moduleId))) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, `Document ${id} is not owned by ${moduleId}.`, { documentId: id, moduleId });
  }
  const details = { documentId: id };
  const title = localizedDefault(value, details);
  const bodyMarkdown = nonEmptyString(ownData(value, 'bodyMarkdown', details), 'Document bodyMarkdown', details);
  const accessConditions = requireJsonArray(ownData(value, 'accessConditions', details), 'Document accessConditions', details);
  const openEffects = ownData(value, 'openEffects', details);
  if (!Array.isArray(openEffects)) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, 'Document openEffects must be an array.', details);
  }
  const sourceFile = nonEmptyString(ownData(value, 'sourceFile', details), 'Document sourceFile', details);
  const knowledgeRefs = requireArray(ownData(value, 'knowledgeRefs', details), 'Document knowledgeRefs', details);
  const meta = metadata(value, id);
  const presentationIds = openEffects
    .filter((effect) => effect && typeof effect === 'object' && effect.op === 'scene.request' && typeof effect.sceneId === 'string')
    .map((effect) => effect.sceneId);
  if (new Set(presentationIds).size !== presentationIds.length) {
    throw oldPcError(OldPcErrorCode.InvalidDefinition, 'Document cannot request the same presentation twice.', details);
  }
  return cloneJsonValue({
    id,
    title,
    bodyMarkdown,
    sourceFile,
    knowledgeRefs,
    accessConditions,
    openEffects,
    presentationIds,
    oldPc: meta,
  }, `Old-PC document ${id}`);
}

function normalizeSearchTerm(value) {
  return value.trim().toLocaleLowerCase('ru');
}

function searchableText(document) {
  return [
    document.id,
    document.title,
    document.bodyMarkdown,
    document.sourceFile,
    document.oldPc.pcSection,
    document.oldPc.type,
    ...document.oldPc.searchTerms,
    ...document.oldPc.suggestedTerms,
  ].join('\n').toLocaleLowerCase('ru');
}

/**
 * Immutable projection of a module's standard DocumentDefinition records.
 * It deliberately receives a ContentRegistry rather than raw authoring files:
 * Markdown/frontmatter parsing belongs exclusively to the content compiler.
 */
export class OldPcContentCatalog {
  #byId;
  #documents;

  constructor({ contentRegistry, moduleId }) {
    if (!contentRegistry || typeof contentRegistry.category !== 'function') {
      throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC catalog requires a ContentRegistry.');
    }
    const documents = contentRegistry.category('documents')
      .filter((entry) => typeof entry?.id === 'string' && entry.id.startsWith(modulePrefix(moduleId)))
      .map((entry) => normalizedDocument(entry, moduleId))
      .sort((left, right) => left.id.localeCompare(right.id));
    if (documents.length === 0) {
      throw oldPcError(OldPcErrorCode.InvalidDefinition, `Module ${moduleId} has no old-PC documents.`, { moduleId });
    }
    this.#byId = new Map(documents.map((document) => [document.id, document]));
    this.#documents = Object.freeze(documents);
  }

  ids() { return Object.freeze(this.#documents.map((document) => document.id)); }

  get(documentId) {
    const document = this.#byId.get(documentId);
    if (!document) throw oldPcError(OldPcErrorCode.MissingDocument, `Unknown old-PC document ${documentId}.`, { documentId });
    return document;
  }

  sections() {
    return Object.freeze([...new Set(this.#documents.map((document) => document.oldPc.pcSection))].sort());
  }

  /** Search retains the authored search terms, while conditions stay owned by the caller/kernel. */
  search(query = '', { section = undefined, isAccessible = () => true } = {}) {
    if (typeof query !== 'string') throw oldPcError(OldPcErrorCode.InvalidInput, 'Old-PC search query must be a string.');
    if (section !== undefined && (typeof section !== 'string' || !section)) {
      throw oldPcError(OldPcErrorCode.InvalidInput, 'Old-PC section must be a non-empty string.');
    }
    if (typeof isAccessible !== 'function') throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC accessibility evaluator must be a function.');
    const normalized = normalizeSearchTerm(query);
    return Object.freeze(this.#documents.filter((document) => {
      if (section && section !== 'archive_search' && document.oldPc.pcSection !== section) return false;
      const accessible = Boolean(isAccessible(document));
      return (!normalized || searchableText(document).includes(normalized)) && (accessible || normalized.length >= 3);
    }));
  }

  suggestedTerms({ isAccessible = () => true } = {}) {
    if (typeof isAccessible !== 'function') throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC accessibility evaluator must be a function.');
    const terms = new Set();
    for (const document of this.#documents) {
      if (!isAccessible(document)) continue;
      for (const term of document.oldPc.suggestedTerms) terms.add(term);
      for (const term of document.oldPc.searchTerms.slice(0, 2)) terms.add(term);
    }
    return Object.freeze([...terms].slice(0, 18));
  }
}

export function createOldPcContentCatalog(options) {
  return new OldPcContentCatalog(options);
}

export { normalizeSearchTerm };
