import { createCapabilitySceneSession } from './capability-scene-provider.mjs';
import { createRouteNavigationSession } from './route-navigation-provider.mjs';
import { createScenePresentationSession } from './scene-presentation-provider.mjs';

function sessionContext(context) {
  if (!context || typeof context !== 'object') return {};
  return context;
}

function descriptor(sceneType, create) {
  return Object.freeze({ sceneType, create: (definition, context) => create(definition, sessionContext(context)) });
}

/** Exact scene-type registrations for SceneRegistry; campaign content stays data-only. */
export function createActiveSceneDescriptors() {
  return Object.freeze([
    descriptor('static', createScenePresentationSession),
    descriptor('scripted', createScenePresentationSession),
    descriptor('presentation', createScenePresentationSession),
    descriptor('capability', createCapabilitySceneSession),
    descriptor('route', createRouteNavigationSession),
  ]);
}
