import type {
  AssetDefinition,
  CommonJsonValue,
  CompiledContentPack,
  TextDefinition,
} from '../../content/generated/content-types.js';

export const ResolverErrorCode: Readonly<Record<string, string>>;

export class ResolverError extends Error {
  readonly code: string;
  readonly details: Readonly<Record<string, unknown>>;
}

export interface AssetVariantSelector {
  readonly id?: string;
  readonly seed?: CommonJsonValue;
  readonly state?: CommonJsonValue;
}

export interface ResolvedAsset {
  readonly assetId: string;
  readonly kind: AssetDefinition['kind'];
  readonly mediaType: string;
  readonly url: string;
  readonly variantId: string | null;
  readonly sha256?: string;
  readonly accessibility: AssetDefinition['accessibility'];
}

export interface VocabularyToken {
  readonly vocabularyId: string;
}

export type TextVariable = string | number | boolean | VocabularyToken;

export interface TextSegment {
  readonly type: 'text';
  readonly value: string;
}

export interface VocabularySegment {
  readonly type: 'vocabulary';
  readonly vocabularyId: string;
  readonly language: string;
  readonly term: string;
  readonly meaning: string;
  readonly meaningLocale: string;
}

export interface ResolvedText {
  readonly textId: string;
  readonly purpose: TextDefinition['purpose'];
  readonly requestedLocale: string;
  readonly resolvedLocale: string;
  readonly text: string;
  readonly segments: readonly (TextSegment | VocabularySegment)[];
}

export function validateManifestClosure(pack: CompiledContentPack): Readonly<{
  assetIds: readonly string[];
  textIds: readonly string[];
  vocabularyIds: readonly string[];
}>;

export class AssetResolver {
  constructor(pack: CompiledContentPack, options?: {
    resolveFileUrl?: (file: string, metadata: Readonly<{ assetId: string; variantId: string | null; moduleId: string }>) => string;
    validate?: boolean;
  });
  ids(): readonly string[];
  resolve(assetRef: string, variant?: string | AssetVariantSelector): ResolvedAsset;
}

export class TextResolver {
  constructor(pack: CompiledContentPack, options?: { validate?: boolean });
  ids(): readonly string[];
  resolve(textRef: string, locale?: string, variables?: Readonly<Record<string, TextVariable>>): ResolvedText;
}
