import { requireContentRegistry } from './active-provider-contracts.mjs';
import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

export function routeDefinition(value) {
  if (!value || typeof value !== 'object' || value.sceneType !== 'route' || typeof value.id !== 'string' || !Array.isArray(value.interactions)) {
    throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, 'Route definition is invalid.');
  }
  return value;
}

/** Content graph owner: one edge is one declared scene interaction. */
export class RouteGraphNavigator {
  #content;
  #entry;

  constructor(contentRegistry, entryDefinition = undefined) {
    this.#content = requireContentRegistry(contentRegistry);
    this.#entry = entryDefinition === undefined ? null : routeDefinition(entryDefinition);
  }

  scene(sceneId) {
    if (this.#entry?.id === sceneId) return this.#entry;
    return routeDefinition(this.#content.get(sceneId));
  }

  target(sceneId) {
    if (this.#entry?.id === sceneId) return this.#entry;
    const value = this.#content.get(sceneId);
    if (!value || typeof value !== 'object' || typeof value.id !== 'string'
      || !Array.isArray(value.entryConditions) || !Array.isArray(value.onEnter)) {
      throw activeProviderError(ActiveProviderErrorCode.MissingDefinition, `Route target ${sceneId} is invalid.`);
    }
    return value;
  }

  interaction(sceneId, interactionId) {
    const interaction = this.scene(sceneId).interactions.find((entry) => entry.id === interactionId);
    if (!interaction) throw activeProviderError(ActiveProviderErrorCode.InvalidInput, `Route interaction ${interactionId} is unavailable from ${sceneId}.`);
    return interaction;
  }

  isInternalTarget(targetSceneId) {
    if (typeof targetSceneId !== 'string') return false;
    return this.target(targetSceneId).sceneType === 'route';
  }
}
