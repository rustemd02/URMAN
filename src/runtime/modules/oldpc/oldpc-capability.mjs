import { clonePersistedJsonValue, deepFreeze } from '../../contracts/json-value.mjs';
import { OldPcContentCatalog } from './oldpc-content-catalog.mjs';
import { OldPcErrorCode, oldPcError } from './oldpc-errors.mjs';

const DEFAULT_COMMAND_TYPES = Object.freeze({
  search: 'oldpc.search',
  open: 'oldpc.document.open',
  save: 'oldpc.document.save',
});

function nonEmptyString(value, label, code = OldPcErrorCode.InvalidInput) {
  if (typeof value !== 'string' || !value) throw oldPcError(code, `${label} must be a non-empty string.`);
  return value;
}

function requireRuntimeContext(runtimeContext) {
  if (!runtimeContext || typeof runtimeContext !== 'object'
    || typeof runtimeContext.dispatch !== 'function'
    || typeof runtimeContext.select !== 'function') {
    throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC capability requires RuntimeContext select() and dispatch().');
  }
  return runtimeContext;
}

function commandTypes(value = {}) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC command types must be an object.');
  }
  const result = {};
  for (const key of Object.keys(DEFAULT_COMMAND_TYPES)) {
    result[key] = nonEmptyString(value[key] ?? DEFAULT_COMMAND_TYPES[key], `Old-PC ${key} command type`, OldPcErrorCode.InvalidContext);
  }
  return Object.freeze(result);
}

function exactKeys(value, allowed, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw oldPcError(OldPcErrorCode.InvalidSnapshot, `${label} must be an object.`);
  }
  const actual = Object.keys(value).sort();
  const expected = [...allowed].sort();
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    throw oldPcError(OldPcErrorCode.InvalidSnapshot, `${label} has an invalid shape.`);
  }
}

function normalizeSnapshot(snapshot, catalog) {
  const state = clonePersistedJsonValue(snapshot, 'Old-PC capability snapshot');
  exactKeys(state, ['activeDocumentId', 'activeSection', 'nextActionSequence', 'query', 'savedDocumentIds'], 'Old-PC capability snapshot');
  if (state.activeDocumentId !== null) catalog.get(nonEmptyString(state.activeDocumentId, 'Old-PC active document', OldPcErrorCode.InvalidSnapshot));
  if (typeof state.activeSection !== 'string' || !catalog.sections().includes(state.activeSection)) {
    throw oldPcError(OldPcErrorCode.InvalidSnapshot, 'Old-PC snapshot activeSection is invalid.');
  }
  if (typeof state.query !== 'string' || !Number.isSafeInteger(state.nextActionSequence) || state.nextActionSequence < 1) {
    throw oldPcError(OldPcErrorCode.InvalidSnapshot, 'Old-PC snapshot contains an invalid query or action sequence.');
  }
  if (!Array.isArray(state.savedDocumentIds) || new Set(state.savedDocumentIds).size !== state.savedDocumentIds.length) {
    throw oldPcError(OldPcErrorCode.InvalidSnapshot, 'Old-PC snapshot savedDocumentIds must be unique.');
  }
  for (const documentId of state.savedDocumentIds) catalog.get(nonEmptyString(documentId, 'Old-PC saved document', OldPcErrorCode.InvalidSnapshot));
  return state;
}

function defaultSnapshot(catalog) {
  const sections = catalog.sections();
  return Object.freeze({
    activeDocumentId: null,
    activeSection: sections.includes('archive_search') ? 'archive_search' : sections[0],
    nextActionSequence: 1,
    query: '',
    savedDocumentIds: Object.freeze([]),
  });
}

function isCommitted(result) { return result && result.status === 'committed'; }

/**
 * A stateful capability session with a deliberately tiny authority surface:
 * its snapshot holds presentation choices and non-gating bookmarks only. Every search/open/save intent
 * travels through RuntimeContext.dispatch, and it changes the local projection
 * only after the transaction commits.
 */
export class OldPcCapabilitySession {
  #runtime;
  #catalog;
  #instanceId;
  #isAccessible;
  #commands;
  #state;
  #listeners = new Set();
  #started = false;
  #disposed = false;
  #restored = false;

  constructor({ runtimeContext, contentRegistry, moduleId, capabilityInstanceId, isAccessible = () => true, commandTypes: types = undefined, snapshot = undefined } = {}) {
    this.#runtime = requireRuntimeContext(runtimeContext);
    this.#catalog = new OldPcContentCatalog({ contentRegistry, moduleId: nonEmptyString(moduleId, 'Old-PC module ID', OldPcErrorCode.InvalidContext) });
    this.#instanceId = nonEmptyString(capabilityInstanceId, 'Old-PC capability instance ID', OldPcErrorCode.InvalidContext);
    if (typeof isAccessible !== 'function') throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC accessibility evaluator must be a function.');
    this.#isAccessible = isAccessible;
    this.#commands = commandTypes(types);
    this.#state = snapshot === undefined ? defaultSnapshot(this.#catalog) : normalizeSnapshot(snapshot, this.#catalog);
    this.#restored = snapshot !== undefined;
  }

  get disposed() { return this.#disposed; }

  start() {
    this.#assertOpen();
    this.#started = true;
  }

  restore(snapshot) {
    this.#assertOpen();
    if (this.#started || this.#restored) throw oldPcError(OldPcErrorCode.InvalidSnapshot, 'Old-PC capability can restore exactly once before start.');
    this.#state = normalizeSnapshot(snapshot, this.#catalog);
    this.#restored = true;
  }

  snapshot() {
    this.#assertOpen();
    return clonePersistedJsonValue(this.#state, 'Old-PC capability snapshot');
  }

  subscribe(listener) {
    this.#assertOpen();
    if (typeof listener !== 'function') throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC subscriber must be a function.');
    this.#listeners.add(listener);
    let disposed = false;
    return Object.freeze({
      get disposed() { return disposed; },
      dispose: () => {
        if (disposed) return;
        disposed = true;
        this.#listeners.delete(listener);
      },
    });
  }

  render() {
    this.#assertOpen();
    const isAccessible = (document) => Boolean(this.#isAccessible(document, this.#runtime.select((state) => state)));
    return deepFreeze({
      activeDocumentId: this.#state.activeDocumentId,
      activeSection: this.#state.activeSection,
      query: this.#state.query,
      savedDocumentIds: [...this.#state.savedDocumentIds],
      results: this.#catalog.search(this.#state.query, { section: this.#state.activeSection, isAccessible }),
      suggestedTerms: this.#catalog.suggestedTerms({ isAccessible }),
    });
  }

  handle(input) {
    this.#assertOpen();
    if (!this.#started) throw oldPcError(OldPcErrorCode.InvalidInput, 'Old-PC capability must start before handling input.');
    if (!input || typeof input !== 'object' || Array.isArray(input) || typeof input.type !== 'string') {
      throw oldPcError(OldPcErrorCode.InvalidInput, 'Old-PC input must have a type.');
    }
    if (input.type === 'search') return this.#search(input.query);
    if (input.type === 'open') return this.#open(input.documentId);
    if (input.type === 'save') return this.#save(input.documentId);
    if (input.type === 'section') return this.#section(input.section);
    throw oldPcError(OldPcErrorCode.InvalidInput, `Unknown old-PC input ${input.type}.`);
  }

  stop() { this.#close(); }
  dispose() { this.#close(); }

  #assertOpen() {
    if (this.#disposed) throw oldPcError(OldPcErrorCode.Disposed, 'Old-PC capability is disposed.');
  }

  #close() {
    if (this.#disposed) return;
    this.#disposed = true;
    this.#listeners.clear();
  }

  #emit() {
    const model = this.render();
    for (const listener of [...this.#listeners]) {
      try { listener(model); } catch { /* presentation listeners never gain runtime authority */ }
    }
  }

  #nextOccurrence(action) {
    const sequence = this.#state.nextActionSequence;
    this.#state = Object.freeze({ ...this.#state, nextActionSequence: sequence + 1 });
    return `${this.#instanceId}:oldpc.${action}:${sequence}`;
  }

  #dispatch(action, type, payload, apply) {
    const actionOccurrenceId = this.#nextOccurrence(action);
    const command = clonePersistedJsonValue({ type, actionOccurrenceId, payload }, `Old-PC ${action} command`);
    let dispatched;
    try {
      dispatched = this.#runtime.dispatch(command);
    } catch (error) {
      return deepFreeze({ status: 'rejected', actionOccurrenceId, error: error instanceof Error ? error.message : String(error) });
    }
    Promise.resolve(dispatched).then((result) => {
      if (!this.#disposed && isCommitted(result)) apply();
      if (!this.#disposed && isCommitted(result)) this.#emit();
    }).catch(() => undefined);
    return deepFreeze({ status: 'pending', actionOccurrenceId, command });
  }

  #search(query) {
    if (typeof query !== 'string') throw oldPcError(OldPcErrorCode.InvalidInput, 'Old-PC search query must be a string.');
    return this.#dispatch('search', this.#commands.search, { capabilityInstanceId: this.#instanceId, query }, () => {
      this.#state = Object.freeze({ ...this.#state, activeSection: 'archive_search', query });
    });
  }

  #open(documentId) {
    const document = this.#catalog.get(nonEmptyString(documentId, 'Old-PC document ID'));
    const accessible = Boolean(this.#isAccessible(document, this.#runtime.select((state) => state)));
    if (!accessible) throw oldPcError(OldPcErrorCode.LockedDocument, `Old-PC document ${documentId} is locked.`, { documentId });
    return this.#dispatch('open', this.#commands.open, {
      capabilityInstanceId: this.#instanceId,
      documentId: document.id,
      knowledgeRefs: document.knowledgeRefs,
      presentationIds: document.presentationIds,
    }, () => {
      this.#state = Object.freeze({ ...this.#state, activeDocumentId: document.id });
    });
  }

  #save(documentId) {
    const document = this.#catalog.get(nonEmptyString(documentId, 'Old-PC document ID'));
    return this.#dispatch('save', this.#commands.save, {
      capabilityInstanceId: this.#instanceId,
      documentId: document.id,
    }, () => {
      if (this.#state.savedDocumentIds.includes(document.id)) return;
      this.#state = Object.freeze({ ...this.#state, savedDocumentIds: [...this.#state.savedDocumentIds, document.id].sort() });
    });
  }

  #section(section) {
    nonEmptyString(section, 'Old-PC section');
    if (!this.#catalog.sections().includes(section)) throw oldPcError(OldPcErrorCode.InvalidInput, `Unknown old-PC section ${section}.`, { section });
    this.#state = Object.freeze({ ...this.#state, activeDocumentId: null, activeSection: section, query: '' });
    this.#emit();
    return this.render();
  }
}

/** CapabilityHost-compatible descriptor. It uses deferred commit projection so handle() stays JSON-synchronous. */
export function createOldPcCapabilityDescriptor({ contentRegistry, moduleId, protocolId, exactVersion = '1.0.0', stateSchemaVersion = 1, isAccessible = undefined, commandTypes: types = undefined } = {}) {
  nonEmptyString(protocolId, 'Old-PC capability protocol ID', OldPcErrorCode.InvalidContext);
  nonEmptyString(exactVersion, 'Old-PC capability exact version', OldPcErrorCode.InvalidContext);
  if (!Number.isSafeInteger(stateSchemaVersion) || stateSchemaVersion < 1) {
    throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC capability stateSchemaVersion must be positive.');
  }
  return Object.freeze({
    protocolId,
    exactVersion,
    stateSchemaVersion,
    validateConfig: (config) => {
      if (!config || typeof config !== 'object' || Array.isArray(config)
        || Object.keys(config).sort().join(',') !== 'moduleId'
        || config.moduleId !== moduleId) {
        throw oldPcError(OldPcErrorCode.InvalidContext, 'Old-PC capability config must contain its exact moduleId.');
      }
      return true;
    },
    create: ({ capabilityInstanceId, config, context }) => {
      const session = new OldPcCapabilitySession({
        runtimeContext: context.runtime,
        contentRegistry,
        moduleId: config.moduleId,
        capabilityInstanceId,
        isAccessible,
        commandTypes: types,
      });
      return Object.freeze({
        restore: (snapshot) => session.restore(snapshot.state ?? snapshot),
        start: () => session.start(),
        render: () => session.render(),
        handle: (input) => session.handle(input),
        subscribe: (listener) => session.subscribe(listener),
        snapshot: () => session.snapshot(),
        stop: () => session.stop(),
        dispose: () => session.dispose(),
      });
    },
  });
}
