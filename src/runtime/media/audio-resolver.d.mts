import type { CompiledContentPack } from '../../content/generated/content-types.js';
import type {
  AssetResolver,
  AssetVariantSelector,
  ResolvedAsset,
  ResolvedText,
  TextResolver,
  TextVariable,
} from '../resolvers/content-resolvers.mjs';

export interface ResolvedAudio {
  readonly asset: ResolvedAsset;
  readonly captions: ResolvedText | null;
  readonly transcript: ResolvedText | null;
  readonly nonAudioCue: Readonly<{
    outcomeKey: string;
    text: string;
    segments: ResolvedText['segments'];
  }> | null;
}

export class AudioResolver {
  constructor(pack: CompiledContentPack, options?: {
    assetResolver?: AssetResolver;
    textResolver?: TextResolver;
    resolveFileUrl?: (file: string, metadata: Readonly<{ assetId: string; variantId: string | null; moduleId: string }>) => string;
  });
  resolve(assetRef: string, options?: {
    locale?: string;
    variables?: Readonly<Record<string, TextVariable>>;
    variant?: string | AssetVariantSelector;
    outcomeKey?: string;
  }): ResolvedAudio;
}
