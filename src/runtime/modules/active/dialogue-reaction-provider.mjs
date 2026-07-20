import { ActiveLifecycle } from './active-lifecycle.mjs';
import {
  activeActionOccurrenceId,
  activeSessionScope,
  containedInputHandler,
  immutableModel,
  normalizePresentationHost,
  requireContentRegistry,
  requireResolver,
  requireRuntimeContext,
} from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

function dialogueDefinition(value) {
  if (!value || typeof value !== 'object' || typeof value.id !== 'string' || typeof value.startNodeId !== 'string' || !Array.isArray(value.nodes)) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Dialogue definition is invalid.');
  }
  return value;
}

export class DialogueReactionSession {
  #runtime;
  #content;
  #assets;
  #texts;
  #host;
  #locale;
  #scope;
  #definition;
  #roleBindings;
  #variables;
  #nodeId;
  #lifecycle = new ActiveLifecycle();
  #sequence = 0;
  #entryResult = null;
  #entryAlreadyCommitted;
  #initPromise = null;
  #handleTail = Promise.resolve();
  #disposePromise = null;

  constructor(dialogueId, {
    runtimeContext,
    contentRegistry,
    assetResolver,
    textResolver,
    roleBindings,
    resolveVariables = () => ({}),
    locale = undefined,
    presentationHost = undefined,
    sessionScope = undefined,
    entryAlreadyCommitted = false,
  } = {}) {
    this.#runtime = requireRuntimeContext(runtimeContext);
    this.#content = requireContentRegistry(contentRegistry);
    this.#assets = requireResolver(assetResolver, 'resolve', 'Asset resolver');
    this.#texts = requireResolver(textResolver, 'resolve', 'Text resolver');
    if (!roleBindings || typeof roleBindings !== 'object' || Array.isArray(roleBindings)) {
      throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Dialogue role bindings are required.');
    }
    this.#roleBindings = Object.freeze({ ...roleBindings });
    if (typeof resolveVariables !== 'function') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Dialogue variable resolver must be a function.');
    }
    this.#variables = resolveVariables;
    this.#definition = dialogueDefinition(this.#content.get(dialogueId));
    this.#nodeId = this.#definition.startNodeId;
    this.#locale = locale;
    this.#host = normalizePresentationHost(presentationHost);
    this.#scope = activeSessionScope(sessionScope ?? `active-dialogue:${dialogueId}`);
    this.#entryAlreadyCommitted = entryAlreadyCommitted === true;
  }

  get disposed() { return this.#lifecycle.disposed; }
  get entryResult() { return this.#entryResult; }

  render() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot render a disposed dialogue session.');
    const node = this.#node();
    const characterId = this.#roleBindings[node.speakerRole];
    if (typeof characterId !== 'string') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `Dialogue role ${node.speakerRole} has no campaign binding.`);
    }
    const character = this.#content.get(characterId);
    const portrait = Array.isArray(character.assetIds) && character.assetIds.length ? this.#assets.resolve(character.assetIds[0]) : null;
    return immutableModel({
      kind: 'dialogue',
      dialogueId: this.#definition.id,
      nodeId: node.id,
      speaker: { role: node.speakerRole, characterId, portrait },
      text: this.#texts.resolve(node.textId, this.#locale, this.#variables(node.textId)),
      choices: node.choices.map((choice) => Object.freeze({
        id: choice.id,
        label: this.#texts.resolve(choice.textId, this.#locale, this.#variables(choice.textId)),
      })),
    });
  }

  async init() {
    if (this.#disposePromise || this.#lifecycle.disposed) {
      throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed dialogue session.');
    }
    if (this.#initPromise) return this.#initPromise;
    this.#initPromise = this.#initialize();
    return this.#initPromise;
  }

  async #initialize() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed dialogue session.');
    const model = this.render();
    this.#entryResult = this.#entryAlreadyCommitted
      ? Object.freeze({ status: 'committed', transactionId: null, value: null })
      : await this.#activateNode();
    if (this.#entryResult.status !== 'committed') return model;
    if (this.#host.subscribeInput) this.#lifecycle.add(this.#host.subscribeInput(containedInputHandler(this.#host, (input) => this.handle(input))));
    this.#host.present(model);
    return model;
  }

  handle(input) {
    const current = this.#handleTail.then(() => this.#handle(input));
    this.#handleTail = current.catch(() => undefined);
    return current;
  }

  async #handle(input) {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot handle input after dialogue disposal.');
    if (this.#entryResult?.status !== 'committed') throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Dialogue entry was not committed.');
    if (!input || typeof input !== 'object' || input.type !== 'dialogue.choose' || typeof input.choiceId !== 'string') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Dialogue input must be dialogue.choose with choiceId.');
    }
    const node = this.#node();
    const choice = node.choices.find((entry) => entry.id === input.choiceId);
    if (!choice) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, `Unknown dialogue choice ${input.choiceId}.`);
    const targetNode = typeof choice.nextNodeId === 'string'
      ? this.#definition.nodes.find((entry) => entry.id === choice.nextNodeId)
      : null;
    if (typeof choice.nextNodeId === 'string' && !targetNode) {
      throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Dialogue node ${choice.nextNodeId} is missing.`);
    }
    this.#sequence += 1;
    const result = await this.#runtime.dispatch(Object.freeze({
      type: 'runtime.dialogue',
      actionOccurrenceId: activeActionOccurrenceId(this.#scope, 'choose', this.#sequence),
      payload: immutableModel({
        dialogueId: this.#definition.id,
        nodeId: node.id,
        choiceId: choice.id,
        conditions: choice.conditions,
        effects: choice.effects,
        targetNode: targetNode
          ? { nodeId: targetNode.id, conditions: targetNode.conditions, effects: targetNode.effects }
          : null,
      }),
    }));
    if (result.status === 'committed' && targetNode) {
      this.#nodeId = choice.nextNodeId;
      this.#entryResult = result;
      this.#host.present(this.render());
    }
    return result;
  }

  async dispose() {
    if (this.#disposePromise) return this.#disposePromise;
    this.#disposePromise = this.#dispose();
    return this.#disposePromise;
  }

  async #dispose() {
    if (this.#initPromise) {
      try { await this.#initPromise; } catch { /* disposal owns remaining cleanup */ }
    }
    this.#lifecycle.dispose();
  }

  #node() {
    const node = this.#definition.nodes.find((entry) => entry.id === this.#nodeId);
    if (!node) throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Dialogue node ${this.#nodeId} is missing.`);
    return node;
  }

  async #activateNode() {
    const node = this.#node();
    this.#sequence += 1;
    return this.#runtime.dispatch(Object.freeze({
      type: 'runtime.dialogue',
      actionOccurrenceId: activeActionOccurrenceId(this.#scope, 'enter', this.#sequence),
      payload: immutableModel({
        dialogueId: this.#definition.id,
        nodeId: node.id,
        conditions: node.conditions,
        effects: node.effects,
      }),
    }));
  }
}

export function createDialogueReactionSession(dialogueId, context) {
  return new DialogueReactionSession(dialogueId, context);
}
