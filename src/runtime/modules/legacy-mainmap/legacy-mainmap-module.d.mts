export interface LegacyMainMapScene {
  init(container: HTMLElement): void;
  destroy(): void;
}

export interface LegacyMainMapSession {
  readonly disposed: boolean;
  init(container: HTMLElement): void;
  dispose(): void;
}

export interface LegacyMainMapDevelopmentContext {
  readonly environment: 'development';
  readonly [key: string]: unknown;
}

export interface LegacyMainMapRegistry {
  register(descriptor: {
    readonly id: typeof LEGACY_MAINMAP_SCENE_ID;
    readonly moduleId: typeof LEGACY_MAINMAP_MODULE_ID;
    readonly devOnly: true;
    create(): LegacyMainMapSession;
  }): void;
}

export interface LegacyMainMapModuleDescriptor {
  readonly moduleId: typeof LEGACY_MAINMAP_MODULE_ID;
  readonly sceneId: typeof LEGACY_MAINMAP_SCENE_ID;
  readonly devOnly: true;
  create(context: LegacyMainMapDevelopmentContext): LegacyMainMapSession;
  register(registry: LegacyMainMapRegistry, context: LegacyMainMapDevelopmentContext): LegacyMainMapModuleDescriptor;
}

export function createLegacyMainMapModuleDescriptor(options: {
  readonly createScene: (context: LegacyMainMapDevelopmentContext) => LegacyMainMapScene;
}): LegacyMainMapModuleDescriptor;

export const LEGACY_MAINMAP_MODULE_ID: 'urman.legacy.mainmap';
export const LEGACY_MAINMAP_SCENE_ID: 'urman.legacy.mainmap:scene/village_greybox';
