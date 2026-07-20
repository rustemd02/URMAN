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

function sceneDefinition(value) {
  if (!value || typeof value !== 'object'
    || typeof value.id !== 'string'
    || typeof value.sceneType !== 'string'
    || !Array.isArray(value.assetRefs)
    || !Array.isArray(value.textRefs)
    || !Array.isArray(value.entryConditions)
    || !Array.isArray(value.interactions)
    || !Array.isArray(value.onEnter)
    || !Array.isArray(value.onExit)) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Active scene definition is invalid.');
  }
  if (value.interactions.some((interaction) => interaction
    && typeof interaction === 'object'
    && typeof interaction.targetSceneId === 'string'
    && typeof interaction.targetDialogueId === 'string')) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'A scene interaction cannot target both a scene and a dialogue.');
  }
  return value;
}

function interactionModel(interaction, textResolver, locale) {
  if (!interaction || typeof interaction !== 'object' || typeof interaction.id !== 'string' || typeof interaction.labelTextId !== 'string') {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Active scene interaction is invalid.');
  }
  return Object.freeze({
    id: interaction.id,
    label: textResolver.resolve(interaction.labelTextId, locale),
    targetSceneId: typeof interaction.targetSceneId === 'string' ? interaction.targetSceneId : null,
    targetDialogueId: typeof interaction.targetDialogueId === 'string' ? interaction.targetDialogueId : null,
  });
}

function dialogueStartEntry(value) {
  if (!value || typeof value !== 'object' || typeof value.id !== 'string' || typeof value.startNodeId !== 'string' || !Array.isArray(value.nodes)) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Dialogue handoff target is invalid.');
  }
  const node = value.nodes.find((entry) => entry?.id === value.startNodeId);
  if (!node || !Array.isArray(node.conditions) || !Array.isArray(node.effects)) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Dialogue start node ${value.startNodeId} is missing or invalid.`);
  }
  return immutableModel({
    dialogueId: value.id,
    nodeId: node.id,
    entryConditions: node.conditions,
    effects: node.effects,
  });
}

/**
 * Generic static, scripted and presentation session. It only projects content
 * through resolvers; state changes remain commands for the kernel owner.
 */
export class ScenePresentationSession {
  #definition;
  #runtime;
  #assets;
  #texts;
  #audio;
  #content;
  #locale;
  #host;
  #scope;
  #lifecycle = new ActiveLifecycle();
  #sequence = 0;
  #initialized = false;
  #exitSent = false;
  #entryResult = null;
  #audioStarted = false;
  #entryAlreadyCommitted;
  #initPromise = null;
  #disposePromise = null;
  #handleTail = Promise.resolve();

  constructor(definition, {
    runtimeContext,
    assetResolver,
    textResolver,
    audioResolver = undefined,
    contentRegistry = undefined,
    locale = undefined,
    presentationHost = undefined,
    sessionScope = undefined,
    entryAlreadyCommitted = false,
  } = {}) {
    this.#definition = sceneDefinition(definition);
    this.#runtime = requireRuntimeContext(runtimeContext);
    this.#assets = requireResolver(assetResolver, 'resolve', 'Asset resolver');
    this.#texts = requireResolver(textResolver, 'resolve', 'Text resolver');
    this.#audio = audioResolver === undefined ? null : requireResolver(audioResolver, 'resolve', 'Audio resolver');
    this.#content = contentRegistry === undefined ? null : requireContentRegistry(contentRegistry);
    this.#locale = locale;
    this.#host = normalizePresentationHost(presentationHost);
    this.#scope = activeSessionScope(sessionScope ?? `active-scene:${this.#definition.id}`);
    this.#entryAlreadyCommitted = entryAlreadyCommitted === true;
  }

  get disposed() { return this.#lifecycle.disposed; }
  get entryResult() { return this.#entryResult; }

  render() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot render a disposed scene session.');
    const assets = this.#definition.assetRefs.map((assetRef) => this.#assets.resolve(assetRef));
    const texts = this.#definition.textRefs.map((textRef) => this.#texts.resolve(textRef, this.#locale));
    return immutableModel({
      kind: 'scene',
      sceneId: this.#definition.id,
      sceneType: this.#definition.sceneType,
      title: texts[0] ?? null,
      texts,
      assets,
      interactions: this.#definition.interactions.map((interaction) => interactionModel(interaction, this.#texts, this.#locale)),
    });
  }

  async init() {
    if (this.#disposePromise || this.#lifecycle.disposed) {
      throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed scene session.');
    }
    if (this.#initPromise) return this.#initPromise;
    this.#initPromise = this.#initialize();
    return this.#initPromise;
  }

  async #initialize() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed scene session.');
    this.#initialized = true;
    const model = this.render();
    this.#entryResult = this.#entryAlreadyCommitted
      ? Object.freeze({ status: 'committed', transactionId: null, value: null })
      : await this.#dispatch('enter', {
        sceneId: this.#definition.id,
        entryConditions: this.#definition.entryConditions,
        effects: this.#definition.onEnter,
      });
    if (this.#entryResult.status !== 'committed') return model;
    if (this.#host.subscribeInput) this.#lifecycle.add(this.#host.subscribeInput(containedInputHandler(this.#host, (input) => this.handle(input))));
    this.#host.present(model);
    await this.#startAudio(modelsForAudio(this.#definition.assetRefs, this.#definition.onEnter, this.#assets, this.#audio, this.#locale));
    return model;
  }

  handle(input) {
    const current = this.#handleTail.then(() => this.#handle(input));
    this.#handleTail = current.catch(() => undefined);
    return current;
  }

  async #handle(input) {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot handle input after scene disposal.');
    if (this.#entryResult?.status !== 'committed') throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Scene entry was not committed.');
    if (this.#exitSent) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Scene transition is already committed.');
    if (!input || typeof input !== 'object' || input.type !== 'scene.interact' || typeof input.interactionId !== 'string') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Scene input must be scene.interact with interactionId.');
    }
    const interaction = this.#definition.interactions.find((entry) => entry.id === input.interactionId);
    if (!interaction) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, `Unknown scene interaction ${input.interactionId}.`);
    const targetEntry = this.#targetEntry(interaction);
    const hasTarget = typeof interaction.targetSceneId === 'string' || typeof interaction.targetDialogueId === 'string';
    const result = await this.#dispatch('interact', {
      sceneId: this.#definition.id,
      interactionId: interaction.id,
      conditions: interaction.conditions,
      effects: interaction.effects,
      exitEffects: hasTarget ? this.#definition.onExit : [],
      targetSceneId: interaction.targetSceneId ?? null,
      targetDialogueId: interaction.targetDialogueId ?? null,
      targetEntry,
    });
    if (result.status === 'committed' && typeof interaction.targetSceneId === 'string') {
      this.#exitSent = true;
      this.#host.navigate(Object.freeze({
        fromSceneId: this.#definition.id,
        targetSceneId: interaction.targetSceneId,
        entryAlreadyCommitted: targetEntry !== null,
      }));
    } else if (result.status === 'committed' && typeof interaction.targetDialogueId === 'string') {
      this.#exitSent = true;
      this.#host.handoff(Object.freeze({
        fromSceneId: this.#definition.id,
        targetDialogueId: interaction.targetDialogueId,
        entryAlreadyCommitted: true,
      }));
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
    if (this.#lifecycle.disposed) return;
    try {
      if (this.#entryResult?.status === 'committed' && !this.#exitSent) {
        await this.#dispatch('exit', { sceneId: this.#definition.id, effects: this.#definition.onExit });
      }
    } finally { this.#lifecycle.dispose(); }
  }

  async #dispatch(action, payload) {
    this.#sequence += 1;
    return this.#runtime.dispatch(Object.freeze({
      type: 'runtime.scene',
      actionOccurrenceId: activeActionOccurrenceId(this.#scope, action, this.#sequence),
      payload: immutableModel({ action, ...payload }),
    }));
  }

  async #startAudio(resources) {
    if (!this.#host.startMedia || this.#audioStarted) return;
    this.#audioStarted = true;
    for (const resource of resources) {
      const cleanup = await this.#host.startMedia(resource);
      if (cleanup) this.#lifecycle.add(cleanup);
    }
  }

  #targetEntry(interaction) {
    if (typeof interaction.targetSceneId !== 'string' && typeof interaction.targetDialogueId !== 'string') return null;
    if (!this.#content) {
      throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Scene transitions require a ContentRegistry for target entry preflight.');
    }
    if (typeof interaction.targetDialogueId === 'string') return dialogueStartEntry(this.#content.get(interaction.targetDialogueId));
    const target = sceneDefinition(this.#content.get(interaction.targetSceneId));
    return immutableModel({
      sceneId: target.id,
      entryConditions: target.entryConditions,
      effects: target.onEnter,
    });
  }
}

function modelsForAudio(assetRefs, enterEffects, assetResolver, audioResolver, locale) {
  if (!audioResolver) return [];
  const resources = [];
  const requested = enterEffects
    .filter((effect) => effect?.op === 'audio.request' && typeof effect.assetId === 'string')
    .map((effect) => effect.assetId);
  for (const assetRef of new Set([...assetRefs, ...requested])) {
    const asset = assetResolver.resolve(assetRef);
    if (asset.kind === 'audio') resources.push(audioResolver.resolve(assetRef, { locale }));
  }
  return resources;
}

export function createScenePresentationSession(definition, context) {
  return new ScenePresentationSession(definition, context);
}
