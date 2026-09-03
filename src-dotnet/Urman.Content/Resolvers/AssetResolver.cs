using System.Text.Json.Nodes;
using Urman.Core.Serialization;

namespace Urman.Content.Resolvers;

public sealed class AssetResolver
{
    private readonly ResolverCatalog _catalog;
    private readonly Func<string, AssetResolutionMetadata, string> _resolveFileUrl;

    public AssetResolver(
        JsonObject pack,
        Func<string, AssetResolutionMetadata, string>? resolveFileUrl = null,
        bool validate = true)
    {
        _catalog = ResolverCatalog.Create(pack);
        _resolveFileUrl = resolveFileUrl ?? ((file, _) => file);
        if (validate)
        {
            _ = ManifestClosureValidator.Validate(pack);
        }
    }

    public IReadOnlyList<string> Ids() => _catalog.Ids("assets");

    public ResolvedAsset Resolve(string assetRef) => ResolveInternal(assetRef, null);

    public ResolvedAsset Resolve(string assetRef, string variantId) =>
        ResolveInternal(assetRef, new AssetVariantSelector(Id: variantId));

    public ResolvedAsset Resolve(string assetRef, AssetVariantSelector selector) =>
        ResolveInternal(assetRef, selector ?? throw new ArgumentNullException(nameof(selector)));

    private ResolvedAsset ResolveInternal(string assetRef, AssetVariantSelector? selector)
    {
        var entry = _catalog.Require("assets", assetRef, ResolverErrors.AbsentOrigin("assets", assetRef));
        ValidateSelector(selector);
        var baseFile = ResolverJson.RequiredString(entry.Value, "file", entry.Origin);
        var options = new List<(string? Id, string File)> { (null, baseFile) };
        foreach (var node in ContentResolverSupport.RequiredArray(entry.Value, "variants", entry.Origin))
        {
            var variant = ResolverJson.RequiredObject(node, entry.Origin.SourcePointer + "/variants");
            options.Add((
                ResolverJson.RequiredString(variant, "id", entry.Origin),
                ResolverJson.RequiredString(variant, "file", entry.Origin)));
        }

        (string? Id, string File) selected;
        if (selector?.Id is not null)
        {
            selected = options.FirstOrDefault(option => string.Equals(option.Id, selector.Id, StringComparison.Ordinal));
            if (selected.File is null)
            {
                throw ResolverErrors.Create(
                    ResolverErrorCode.MissingVariant,
                    $"Asset {assetRef} has no variant {selector.Id}.",
                    entry.Origin,
                    new Dictionary<string, object?> { ["assetId"] = assetRef, ["variantId"] = selector.Id });
            }
        }
        else if (selector?.Seed is not null || selector?.State is not null)
        {
            var input = new JsonObject
            {
                ["assetId"] = assetRef,
                ["seed"] = selector.Seed?.DeepClone(),
                ["state"] = selector.State?.DeepClone()
            };
            var index = (int)(ContentResolverSupport.Fnv1A(CanonicalJson.Serialize(input)) % (uint)options.Count);
            selected = options[index];
        }
        else
        {
            selected = options[0];
        }

        string url;
        try
        {
            url = _resolveFileUrl(selected.File, new AssetResolutionMetadata(assetRef, selected.Id, entry.Origin.ModuleId));
        }
        catch (Exception error) when (error is not ResolverException)
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidResolvedUrl,
                error.Message,
                entry.Origin,
                new Dictionary<string, object?> { ["assetId"] = assetRef, ["variantId"] = selected.Id },
                error);
        }

        if (string.IsNullOrEmpty(url))
        {
            throw ResolverErrors.Create(
                ResolverErrorCode.InvalidResolvedUrl,
                $"Asset URL for {assetRef} must be a non-empty string.",
                entry.Origin,
                new Dictionary<string, object?> { ["assetId"] = assetRef, ["variantId"] = selected.Id });
        }

        return new ResolvedAsset(
            assetRef,
            ResolverJson.RequiredString(entry.Value, "kind", entry.Origin),
            ResolverJson.RequiredString(entry.Value, "mediaType", entry.Origin),
            url,
            selected.Id,
            entry.Value["sha256"]?.GetValue<string>(),
            ContentResolverSupport.AccessibilityFor(entry.Value, entry.Origin));
    }

    private static void ValidateSelector(AssetVariantSelector? selector)
    {
        if (selector?.Id is not null && selector.Id.Length == 0)
        {
            throw ResolverErrors.Create(ResolverErrorCode.InvalidPack, "Asset variant selector ID must be a non-empty string.");
        }
    }
}
