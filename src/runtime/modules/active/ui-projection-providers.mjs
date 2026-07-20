import { ActiveLifecycle } from './active-lifecycle.mjs';
import {
  activeActionOccurrenceId,
  activeSessionScope,
  containedInputHandler,
  immutableModel,
  normalizePresentationHost,
  requireContentRegistry,
  requireRuntimeContext,
} from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

function selector(value, label) {
  if (typeof value !== 'function') throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `${label} selector must be a function.`);
  return value;
}

/** A read-only projection. Its selector is supplied by the composition host. */
export class RuntimeProjectionPresenter {
  #runtime;
  #host;
  #kind;
  #selector;
  #commandBuilder;
  #scope;
  #sequence = 0;
  #lifecycle = new ActiveLifecycle();
  #initialized = false;

  constructor(kind, {
    runtimeContext,
    presentationHost = undefined,
    selectProjection,
    buildCommand = undefined,
    sessionScope = undefined,
  } = {}) {
    if (typeof kind !== 'string' || !kind) throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Projection kind must be a non-empty string.');
    this.#runtime = requireRuntimeContext(runtimeContext);
    this.#host = normalizePresentationHost(presentationHost);
    this.#kind = kind;
    this.#selector = selector(selectProjection, kind);
    if (buildCommand !== undefined && typeof buildCommand !== 'function') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `${kind} command builder must be a function.`);
    }
    this.#commandBuilder = buildCommand ?? null;
    this.#scope = activeSessionScope(sessionScope ?? `active-ui:${kind}`);
  }

  get disposed() { return this.#lifecycle.disposed; }

  init() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, `Cannot initialize disposed ${this.#kind} presentation.`);
    if (this.#initialized) return this.render();
    this.#initialized = true;
    if (this.#host.subscribeInput) this.#lifecycle.add(this.#host.subscribeInput(containedInputHandler(this.#host, (input) => this.handle(input))));
    return this.render();
  }

  render() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, `Cannot render disposed ${this.#kind} presentation.`);
    const model = immutableModel({ kind: this.#kind, projection: this.#runtime.select(this.#selector) });
    this.#host.present(model);
    return model;
  }

  async handle(input) {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, `Cannot handle input after ${this.#kind} disposal.`);
    if (!this.#commandBuilder) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, `${this.#kind} does not accept actions.`);
    this.#sequence += 1;
    const actionOccurrenceId = activeActionOccurrenceId(this.#scope, 'action', this.#sequence);
    const command = this.#commandBuilder(input, actionOccurrenceId);
    if (!command || typeof command !== 'object' || typeof command.type !== 'string' || command.actionOccurrenceId !== actionOccurrenceId) {
      throw activeProviderError(ActiveProviderErrorCode.InvalidInput, `${this.#kind} command builder must preserve its generated occurrence ID.`);
    }
    return this.#runtime.dispatch(command);
  }

  dispose() { this.#lifecycle.dispose(); }
}

export class VocabularyProjectionPresenter extends RuntimeProjectionPresenter {
  #content;

  constructor(options = {}) {
    super('vocabulary', options);
    this.#content = requireContentRegistry(options.contentRegistry);
  }

  vocabularyIds() {
    return immutableModel(this.#content.category('vocabulary').map((entry) => entry.id));
  }
}

export function createJournalPresenter(options) { return new RuntimeProjectionPresenter('journal', options); }
export function createHudPresenter(options) { return new RuntimeProjectionPresenter('hud', options); }
export function createInventoryPresenter(options) { return new RuntimeProjectionPresenter('inventory', options); }
export function createVocabularyPresenter(options) { return new VocabularyProjectionPresenter(options); }
