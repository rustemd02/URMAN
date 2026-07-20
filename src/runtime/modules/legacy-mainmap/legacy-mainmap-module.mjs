const MODULE_ID = 'urman.legacy.mainmap';
const SCENE_ID = 'urman.legacy.mainmap:scene/village_greybox';

function fail(message, details = {}) {
  const error = new Error(message);
  error.name = 'LegacyMainMapModuleError';
  error.code = 'LegacyMainMapModuleError';
  error.details = Object.freeze({ ...details });
  throw error;
}

function requireDevelopment(context) {
  if (!context || typeof context !== 'object' || Array.isArray(context) || context.environment !== 'development') {
    fail('Legacy MainMap may be created only by an explicit development registration.', { environment: context?.environment });
  }
  return context;
}

function createSession(scene) {
  if (!scene || typeof scene !== 'object' || Array.isArray(scene)
    || typeof scene.init !== 'function' || typeof scene.destroy !== 'function') {
    fail('Legacy MainMap scene factory must return init(container) and destroy() methods.');
  }
  let disposed = false;
  let initialized = false;
  return Object.freeze({
    get disposed() { return disposed; },
    init(container) {
      if (disposed) fail('Legacy MainMap session is disposed.');
      if (initialized) fail('Legacy MainMap session may be initialized once.');
      initialized = true;
      try {
        return scene.init(container);
      } catch (error) {
        // The descriptor owns a created scene for the whole session.  A failed
        // init is terminal: release it here so callers cannot need a separate
        // best-effort dispose after an exception.
        disposed = true;
        try { scene.destroy(); } catch { /* preserve the init failure */ }
        throw error;
      }
    },
    dispose() {
      if (disposed) return;
      disposed = true;
      scene.destroy();
    },
  });
}

/**
 * The map has no production auto-registration.  A composition root must
 * explicitly create this descriptor and call `register` with an environment
 * marked `development`; its scene factory owns the host-specific TypeScript
 * adapter and injects the role bindings/action callback.
 */
export function createLegacyMainMapModuleDescriptor({ createScene } = {}) {
  if (typeof createScene !== 'function') {
    fail('Legacy MainMap descriptor requires a createScene factory.');
  }
  const descriptor = {
    moduleId: MODULE_ID,
    sceneId: SCENE_ID,
    devOnly: true,
    create(context) {
      const developmentContext = requireDevelopment(context);
      return createSession(createScene(Object.freeze({ ...developmentContext })));
    },
    register(registry, context) {
      requireDevelopment(context);
      if (!registry || typeof registry.register !== 'function') {
        fail('Legacy MainMap registration requires an explicit dev registry with register().');
      }
      registry.register(Object.freeze({
        id: SCENE_ID,
        moduleId: MODULE_ID,
        devOnly: true,
        create: () => descriptor.create(context),
      }));
      return descriptor;
    },
  };
  return Object.freeze(descriptor);
}

export const LEGACY_MAINMAP_MODULE_ID = MODULE_ID;
export const LEGACY_MAINMAP_SCENE_ID = SCENE_ID;
