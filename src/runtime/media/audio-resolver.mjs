import { deepFreeze } from '../contracts/json-value.mjs';
import { AssetResolver, TextResolver, validateManifestClosure } from '../resolvers/content-resolvers.mjs';
import { ResolverErrorCode, resolverError } from '../resolvers/resolver-errors.mjs';

export class AudioResolver {
  #assets;
  #texts;

  constructor(pack, { assetResolver = undefined, textResolver = undefined, resolveFileUrl = undefined } = {}) {
    validateManifestClosure(pack);
    this.#assets = assetResolver ?? new AssetResolver(pack, { resolveFileUrl, validate: false });
    this.#texts = textResolver ?? new TextResolver(pack, { validate: false });
  }

  resolve(assetRef, { locale = undefined, variables = undefined, variant = undefined, outcomeKey = undefined } = {}) {
    const asset = this.#assets.resolve(assetRef, variant);
    if (asset.kind !== 'audio') {
      throw resolverError(ResolverErrorCode.WrongAssetKind, `${assetRef} is not an audio asset.`, {}, { assetId: assetRef, expectedKind: 'audio', actualKind: asset.kind });
    }
    if (asset.accessibility.decorative) {
      return deepFreeze({ asset, captions: null, transcript: null, nonAudioCue: null });
    }
    const captionTextId = asset.accessibility.captionTextId;
    const transcriptTextId = asset.accessibility.audioDescriptionTextId;
    const captions = this.#texts.resolve(captionTextId, locale, variables);
    const transcript = this.#texts.resolve(transcriptTextId, locale, variables);
    const resolvedOutcomeKey = outcomeKey ?? assetRef;
    if (typeof resolvedOutcomeKey !== 'string' || !resolvedOutcomeKey) {
      throw resolverError(ResolverErrorCode.InvalidPack, 'Audio outcome key must be a non-empty string.', {}, { assetId: assetRef });
    }
    return deepFreeze({
      asset,
      captions,
      transcript,
      nonAudioCue: {
        outcomeKey: resolvedOutcomeKey,
        text: transcript.text,
        segments: transcript.segments,
      },
    });
  }
}
