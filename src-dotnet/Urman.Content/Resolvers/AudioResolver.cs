using System.Text.Json.Nodes;

namespace Urman.Content.Resolvers;

public sealed record NonAudioCue(
    string OutcomeKey,
    string Text,
    IReadOnlyList<ResolvedTextSegment> Segments);

public sealed record ResolvedAudio(
    ResolvedAsset Asset,
    ResolvedText? Captions,
    ResolvedText? Transcript,
    NonAudioCue? NonAudioCue);

public sealed record AudioResolveOptions(
    string? Locale = null,
    IReadOnlyDictionary<string, object?>? Variables = null,
    AssetVariantSelector? Variant = null,
    string? OutcomeKey = null);

public sealed class AudioResolver
{
    private readonly AssetResolver _assets;
    private readonly TextResolver _texts;

    public AudioResolver(
        JsonObject pack,
        AssetResolver? assetResolver = null,
        TextResolver? textResolver = null,
        Func<string, AssetResolutionMetadata, string>? resolveFileUrl = null)
    {
        _ = ManifestClosureValidator.Validate(pack);
        _assets = assetResolver ?? new AssetResolver(pack, resolveFileUrl, validate: false);
        _texts = textResolver ?? new TextResolver(pack, validate: false);
    }

    public ResolvedAudio Resolve(string assetRef, AudioResolveOptions? options = null)
    {
        options ??= new AudioResolveOptions();
        var asset = options.Variant is null
            ? _assets.Resolve(assetRef)
            : _assets.Resolve(assetRef, options.Variant);
        if (!string.Equals(asset.Kind, "audio", StringComparison.Ordinal))
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.WrongAssetKind,
                $"{assetRef} is not an audio asset.",
                extra: new Dictionary<string, object?>
                {
                    ["assetId"] = assetRef,
                    ["expectedKind"] = "audio",
                    ["actualKind"] = asset.Kind
                });
        }

        if (asset.Accessibility.Decorative)
        {
            return new ResolvedAudio(asset, null, null, null);
        }

        var captions = _texts.Resolve(
            asset.Accessibility.CaptionTextId!,
            options.Locale,
            options.Variables);
        var transcript = _texts.Resolve(
            asset.Accessibility.AudioDescriptionTextId!,
            options.Locale,
            options.Variables);
        var outcomeKey = options.OutcomeKey ?? assetRef;
        if (string.IsNullOrEmpty(outcomeKey))
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidPack,
                "Audio outcome key must be a non-empty string.",
                extra: new Dictionary<string, object?> { ["assetId"] = assetRef });
        }

        return new ResolvedAudio(
            asset,
            captions,
            transcript,
            new NonAudioCue(outcomeKey, transcript.Text, transcript.Segments));
    }
}
