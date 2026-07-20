import { ActiveLifecycle } from './active-lifecycle.mjs';
import { activeActionOccurrenceId, activeSessionScope, containedInputHandler, immutableModel, normalizePresentationHost, requireRuntimeContext } from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';
import { ExternalHandoffAdapter } from './route-handoff-adapter.mjs';
import { RouteGraphNavigator, routeDefinition } from './route-graph-navigator.mjs';
import { RouteInputController } from './route-input-controller.mjs';
import { RoutePresentationRenderer } from './route-presentation-renderer.mjs';

/** Coordinates narrow route owners without owning content, host navigation or state. */
export class RouteNavigationSession {
  #runtime;
  #host;
  #graph;
  #input = new RouteInputController();
  #handoff;
  #renderer;
  #lifecycle = new ActiveLifecycle();
  #scope;
  #sceneId;
  #sequence = 0;
  #exitSent = false;
  #initialized = false;
  #entryResult = null;
  #entryAlreadyCommitted;
  #initPromise = null;
  #disposePromise = null;
  #handleTail = Promise.resolve();

  constructor(definition, {
    runtimeContext,
    contentRegistry,
    assetResolver,
    textResolver,
    locale = undefined,
    presentationHost = undefined,
    sessionScope = undefined,
    entryAlreadyCommitted = false,
  } = {}) {
    const initial = routeDefinition(definition);
    this.#runtime = requireRuntimeContext(runtimeContext);
    this.#host = normalizePresentationHost(presentationHost);
    this.#graph = new RouteGraphNavigator(contentRegistry, initial);
    this.#handoff = new ExternalHandoffAdapter(this.#host);
    this.#renderer = new RoutePresentationRenderer({ assetResolver, textResolver, locale });
    this.#sceneId = initial.id;
    this.#scope = activeSessionScope(sessionScope ?? `active-route:${initial.id}`);
    this.#entryAlreadyCommitted = entryAlreadyCommitted === true;
  }

  get disposed() { return this.#lifecycle.disposed; }
  get entryResult() { return this.#entryResult; }

  render() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot render a disposed route session.');
    return this.#renderer.render(this.#graph.scene(this.#sceneId));
  }

  async init() {
    if (this.#disposePromise || this.#lifecycle.disposed) {
      throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed route session.');
    }
    if (this.#initPromise) return this.#initPromise;
    this.#initPromise = this.#initialize();
    return this.#initPromise;
  }

  async #initialize() {
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed route session.');
    this.#initialized = true;
    const model = this.render();
    const scene = this.#graph.scene(this.#sceneId);
    this.#entryResult = this.#entryAlreadyCommitted
      ? Object.freeze({ status: 'committed', transactionId: null, value: null })
      : await this.#dispatch({ action: 'enter', sceneId: scene.id, entryConditions: scene.entryConditions, effects: scene.onEnter });
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
    if (this.#lifecycle.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot handle input after route disposal.');
    if (this.#entryResult?.status !== 'committed') throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Route entry was not committed.');
    if (this.#exitSent) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Route handoff is already committed.');
    const interaction = this.#graph.interaction(this.#sceneId, this.#input.action(input));
    const targetSceneId = interaction.targetSceneId ?? null;
    const current = this.#graph.scene(this.#sceneId);
    const target = targetSceneId ? this.#graph.target(targetSceneId) : null;
    const result = await this.#dispatch({
      action: 'choose',
      fromSceneId: this.#sceneId,
      interactionId: interaction.id,
      conditions: interaction.conditions,
      effects: interaction.effects,
      exitEffects: targetSceneId ? current.onExit : [],
      targetSceneId,
      targetEntry: target
        ? { sceneId: target.id, entryConditions: target.entryConditions, effects: target.onEnter }
        : null,
    });
    if (result.status !== 'committed' || !targetSceneId) return result;
    if (target?.sceneType === 'route') {
      this.#sceneId = targetSceneId;
      this.#entryResult = result;
      this.#host.present(this.render());
    } else {
      this.#exitSent = true;
      this.#handoff.handoff(this.#sceneId, targetSceneId, true);
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
        const scene = this.#graph.scene(this.#sceneId);
        await this.#dispatch({ action: 'exit', sceneId: scene.id, effects: scene.onExit });
      }
    } finally {
      this.#lifecycle.dispose();
    }
  }

  async #dispatch(payload) {
    this.#sequence += 1;
    return this.#runtime.dispatch(Object.freeze({
      type: 'runtime.route',
      actionOccurrenceId: activeActionOccurrenceId(this.#scope, payload.action, this.#sequence),
      payload: immutableModel(payload),
    }));
  }
}

export function createRouteNavigationSession(definition, context) {
  return new RouteNavigationSession(definition, context);
}
