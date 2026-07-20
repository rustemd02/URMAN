export interface RuntimeSceneSession {
  init(): Promise<unknown>;
  dispose(): Promise<void>;
}

export interface RuntimePersistencePort {
  capture(): object;
  decode(snapshot: object): object;
  restore(snapshot: object): Promise<{ readonly campaignFingerprint: string; readonly capabilityInstanceIds: readonly string[] }>;
}

/** Browser-facing runtime facade; it intentionally excludes state internals. */
export interface RuntimePresentationPort {
  readonly campaign: any;
  createScene(sceneId: string, presentationHost: object, options?: { readonly entryAlreadyCommitted?: boolean }): RuntimeSceneSession;
  createDialogue(dialogueId: string, presentationHost: object, options?: { readonly entryAlreadyCommitted?: boolean }): RuntimeSceneSession;
  oldPcPresentation(): { render(): unknown; handle(input: unknown): unknown; subscribe(listener: (model: unknown) => void): { readonly disposed: boolean; dispose(): void } };
  snapshot(): object;
  restore(snapshot: object): Promise<{ readonly campaignFingerprint: string; readonly capabilityInstanceIds: readonly string[] }>;
}

export interface RuntimeBootstrapOptions {
  readonly pack: object;
  readonly resolveFileUrl?: (file: string, context: { readonly assetId: string; readonly variantId: string | null; readonly moduleId: string }) => string;
  readonly locale?: string;
  readonly seed?: string;
}

export class RuntimeBootstrap {
  constructor(options: RuntimeBootstrapOptions);
  readonly pack: any;
  readonly campaign: any;
  readonly campaignFingerprint: string;
  readonly runtimeContext: any;
  readonly contentRegistry: any;
  readonly capabilityHost: any;
  readonly persistence: RuntimePersistencePort;
  readonly presentation: RuntimePresentationPort;
  oldPcPresentation(): { render(): unknown; handle(input: unknown): unknown; subscribe(listener: (model: unknown) => void): { readonly disposed: boolean; dispose(): void } };
  createScene(sceneId: string, presentationHost: object, options?: { readonly entryAlreadyCommitted?: boolean }): RuntimeSceneSession;
  createDialogue(dialogueId: string, presentationHost: object, options?: { readonly entryAlreadyCommitted?: boolean }): RuntimeSceneSession;
  snapshot(): object;
  restore(snapshot: object): Promise<{ readonly campaignFingerprint: string; readonly capabilityInstanceIds: readonly string[] }>;
}

export const PRODUCTION_OLD_PC_INSTANCE_ID: string;
