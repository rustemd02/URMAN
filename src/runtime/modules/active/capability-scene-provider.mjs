import { ActiveLifecycle } from './active-lifecycle.mjs';
import { containedInputHandler, immutableModel, normalizePresentationHost, requireContentRegistry, requireRuntimeContext } from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';
import { ScenePresentationSession } from './scene-presentation-provider.mjs';

function capabilitySceneDefinition(value) {
  if (!value || value.sceneType !== 'capability' || typeof value.capabilityConfigRef !== 'string' || !value.capabilityConfigRef) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Capability scene requires capabilityConfigRef.');
  }
  return value;
}

function requireAdapter(value) {
  if (!value || typeof value.create !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Capability scene requires a capability adapter with create().');
  }
  return value;
}

function resolveCapabilityDefinition(contentRegistry, capabilityConfigRef) {
  let definition;
  let capabilities;
  try {
    definition = contentRegistry.get(capabilityConfigRef);
    capabilities = contentRegistry.category('capabilities');
  } catch {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Capability config ${capabilityConfigRef} is not registered.`);
  }
  if (!capabilities.some((entry) => entry.id === capabilityConfigRef)
    || typeof definition.protocolId !== 'string'
    || !definition.protocolId
    || typeof definition.exactVersion !== 'string'
    || !definition.exactVersion) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Capability config ${capabilityConfigRef} must resolve to a capability definition.`);
  }
  return immutableModel(definition);
}

function requireCapabilityProvider(runtimeContext, definition) {
  if (!runtimeContext.capabilities || typeof runtimeContext.capabilities.require !== 'function') {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Capability scene requires RuntimeContext.capabilities.require().');
  }
  const descriptor = runtimeContext.capabilities.require(definition.protocolId, definition.exactVersion);
  if (!descriptor || typeof descriptor !== 'object'
    || descriptor.protocolId !== definition.protocolId
    || descriptor.exactVersion !== definition.exactVersion) {
    throw activeProviderError(ActiveProviderErrorCode.InvalidContext, `Runtime capability provider ${definition.protocolId}@${definition.exactVersion} is invalid.`);
  }
  return Object.freeze({
    protocolId: descriptor.protocolId,
    exactVersion: descriptor.exactVersion,
    create: descriptor.create,
  });
}

/** Bridges a capability scene to the existing capability owner without duplicating lifecycle logic. */
export class CapabilitySceneSession {
  #definition;
  #capabilityDefinition;
  #providerDescriptor;
  #presentation;
  #adapter;
  #capability = null;
  #host;
  #lifecycle = new ActiveLifecycle();
  #initPromise = null;
  #disposePromise = null;
  #cleanupPromise = null;
  #handleTail = Promise.resolve();

  constructor(definition, context = {}) {
    this.#definition = capabilitySceneDefinition(definition);
    const runtimeContext = requireRuntimeContext(context.runtimeContext);
    const contentRegistry = requireContentRegistry(context.contentRegistry);
    this.#capabilityDefinition = resolveCapabilityDefinition(contentRegistry, this.#definition.capabilityConfigRef);
    this.#providerDescriptor = requireCapabilityProvider(runtimeContext, this.#capabilityDefinition);
    this.#host = normalizePresentationHost(context.presentationHost);
    this.#presentation = new ScenePresentationSession(definition, {
      ...context,
      runtimeContext,
      contentRegistry,
      presentationHost: {
        present: this.#host.present,
        navigate: this.#host.navigate,
        handoff: this.#host.handoff,
        startMedia: this.#host.startMedia,
      },
    });
    this.#adapter = requireAdapter(context.capabilitySceneAdapter);
  }

  get disposed() { return this.#lifecycle.disposed || this.#presentation.disposed; }
  get entryResult() { return this.#presentation.entryResult; }

  render() {
    if (this.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot render a disposed capability scene.');
    const model = this.#presentation.render();
    return immutableModel({ ...model, capabilityConfigRef: this.#definition.capabilityConfigRef });
  }

  async init() {
    if (this.#disposePromise || this.disposed) {
      throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed capability scene.');
    }
    if (this.#initPromise) return this.#initPromise;
    this.#initPromise = this.#initialize();
    return this.#initPromise;
  }

  async #initialize() {
    if (this.disposed) throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot initialize a disposed capability scene.');
    const model = await this.#presentation.init();
    if (this.entryResult?.status !== 'committed') return this.render();
    if (this.#host.subscribeInput) this.#lifecycle.add(this.#host.subscribeInput(containedInputHandler(this.#host, (input) => this.handle(input))));
    try {
      this.#capability = await this.#adapter.create(Object.freeze({
        sceneId: this.#definition.id,
        capabilityConfigRef: this.#definition.capabilityConfigRef,
        capabilityConfig: this.#capabilityDefinition,
        providerDescriptor: this.#providerDescriptor,
      }));
      if (!this.#capability || typeof this.#capability.start !== 'function' || typeof this.#capability.handle !== 'function' || typeof this.#capability.dispose !== 'function') {
        throw activeProviderError(ActiveProviderErrorCode.InvalidContext, 'Capability scene adapter must create start(), handle() and dispose() lifecycle methods.');
      }
      await this.#capability.start();
    } catch (error) {
      await this.#cleanup();
      throw error;
    }
    return immutableModel({ ...model, capabilityConfigRef: this.#definition.capabilityConfigRef });
  }

  handle(input) {
    const current = this.#handleTail.then(() => this.#handle(input));
    this.#handleTail = current.catch(() => undefined);
    return current;
  }

  async #handle(input) {
    if (this.disposed) {
      throw activeProviderError(ActiveProviderErrorCode.Disposed, 'Cannot handle input after capability scene disposal.');
    }
    if (input?.type === 'capability.handle') {
      if (!this.#capability) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Capability scene is not active.');
      return this.#capability.handle(input.payload ?? null);
    }
    return this.#presentation.handle(input);
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
    return this.#cleanup();
  }

  async #cleanup() {
    if (this.#cleanupPromise) return this.#cleanupPromise;
    this.#cleanupPromise = this.#performCleanup();
    return this.#cleanupPromise;
  }

  async #performCleanup() {
    let failure = null;
    try {
      this.#lifecycle.dispose();
    } catch (error) { failure = error; }
    const capability = this.#capability;
    this.#capability = null;
    try {
      if (capability) await capability.dispose();
    } catch (error) { failure ??= error; }
    try {
      await this.#presentation.dispose();
    } catch (error) { failure ??= error; }
    if (failure) throw failure;
  }
}

export function createCapabilitySceneSession(definition, context) {
  return new CapabilitySceneSession(definition, context);
}
