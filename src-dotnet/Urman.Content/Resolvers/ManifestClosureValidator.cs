using System.Text.Json.Nodes;

namespace Urman.Content.Resolvers;

public static class ManifestClosureValidator
{
    public static ManifestClosure Validate(JsonObject pack)
    {
        var catalog = ResolverCatalog.Create(pack);
        foreach (var entry in catalog.Category("assets").Values)
        {
            var asset = entry.Value;
            _ = ResolverJson.RequiredString(asset, "kind", entry.Origin);
            _ = ResolverJson.RequiredString(asset, "file", entry.Origin);
            _ = ResolverJson.RequiredString(asset, "mediaType", entry.Origin);
            var accessibility = ContentResolverSupport.AccessibilityFor(asset, entry.Origin);
            foreach (var (key, textId) in new[]
                     {
                         ("altTextId", accessibility.AltTextId),
                         ("captionTextId", accessibility.CaptionTextId),
                         ("audioDescriptionTextId", accessibility.AudioDescriptionTextId)
                     })
            {
                if (textId is not null)
                {
                    _ = catalog.Require("texts", textId, ResolverErrors.Reference(entry.Origin, $"/accessibility/{key}"));
                }
            }

            var kind = asset["kind"]!.GetValue<string>();
            if (!accessibility.Decorative && string.Equals(kind, "image", StringComparison.Ordinal))
            {
                ContentResolverSupport.RequireAccessibilityField(accessibility.AltTextId, asset, "altTextId", entry.Origin);
            }

            if (!accessibility.Decorative && string.Equals(kind, "audio", StringComparison.Ordinal))
            {
                ContentResolverSupport.RequireAccessibilityField(accessibility.CaptionTextId, asset, "captionTextId", entry.Origin);
                ContentResolverSupport.RequireAccessibilityField(accessibility.AudioDescriptionTextId, asset, "audioDescriptionTextId", entry.Origin);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var variants = ContentResolverSupport.RequiredArray(asset, "variants", entry.Origin);
            for (var index = 0; index < variants.Count; index++)
            {
                var variantOrigin = ResolverErrors.Reference(entry.Origin, $"/variants/{index}");
                var variant = variants[index] as JsonObject
                    ?? throw ResolverErrors.Create(ResolverErrorCode.InvalidPack, $"{asset["id"]}.variants/{index} is invalid.", variantOrigin);
                var id = ResolverJson.RequiredString(variant, "id", variantOrigin);
                _ = ResolverJson.RequiredString(variant, "file", variantOrigin);
                if (!ids.Add(id))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.InvalidPack,
                        $"{asset["id"]} has duplicate variant {id}.",
                        variantOrigin);
                }
            }
        }

        EnsureReferences(catalog, catalog.Category("characters").Values, "assetIds", "assets");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "assetRefs", "assets");
        EnsureReferences(catalog, catalog.Category("documents").Values, "assetRefs", "assets");
        EnsureReferences(catalog, catalog.Category("capabilities").Values, "requiredAssets", "assets");
        EnsureReferences(catalog, catalog.Category("capabilities").Values, "requiredAudio", "assets", "audio");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "textRefs", "texts");

        foreach (var entry in catalog.Category("dialogues").Values)
        {
            var nodes = ContentResolverSupport.RequiredArray(entry.Value, "nodes", entry.Origin);
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var nodeOrigin = ResolverErrors.Reference(entry.Origin, $"/nodes/{nodeIndex}");
                var node = ResolverJson.RequiredObject(nodes[nodeIndex], nodeOrigin.SourcePointer);
                var textId = ResolverJson.RequiredString(node, "textId", nodeOrigin);
                _ = catalog.Require("texts", textId, ResolverErrors.Reference(nodeOrigin, "/textId"));
                var choices = ContentResolverSupport.RequiredArray(node, "choices", nodeOrigin);
                for (var choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
                {
                    var choiceOrigin = ResolverErrors.Reference(nodeOrigin, $"/choices/{choiceIndex}");
                    var choice = ResolverJson.RequiredObject(choices[choiceIndex], choiceOrigin.SourcePointer);
                    var choiceTextId = ResolverJson.RequiredString(choice, "textId", choiceOrigin);
                    _ = catalog.Require("texts", choiceTextId, ResolverErrors.Reference(choiceOrigin, "/textId"));
                }
            }
        }

        foreach (var entry in catalog.Category("quests").Values)
        {
            var titleTextId = ResolverJson.RequiredString(entry.Value, "titleTextId", entry.Origin);
            _ = catalog.Require("texts", titleTextId, ResolverErrors.Reference(entry.Origin, "/titleTextId"));
            var stages = ContentResolverSupport.RequiredArray(entry.Value, "stages", entry.Origin);
            for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stageOrigin = ResolverErrors.Reference(entry.Origin, $"/stages/{stageIndex}");
                var stage = ResolverJson.RequiredObject(stages[stageIndex], stageOrigin.SourcePointer);
                var objectives = ContentResolverSupport.RequiredArray(stage, "objectives", stageOrigin);
                for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex++)
                {
                    var objectiveOrigin = ResolverErrors.Reference(stageOrigin, $"/objectives/{objectiveIndex}");
                    var objective = ResolverJson.RequiredObject(objectives[objectiveIndex], objectiveOrigin.SourcePointer);
                    var objectiveTextId = ResolverJson.RequiredString(objective, "titleTextId", objectiveOrigin);
                    _ = catalog.Require("texts", objectiveTextId, ResolverErrors.Reference(objectiveOrigin, "/titleTextId"));
                }
            }
        }

        return new ManifestClosure(catalog.Ids("assets"), catalog.Ids("texts"), catalog.Ids("vocabulary"));
    }

    private static void EnsureReferences(
        ResolverCatalog catalog,
        IEnumerable<ResolverCatalogEntry> entries,
        string field,
        string category,
        string? expectedAssetKind = null)
    {
        foreach (var entry in entries)
        {
            var values = ContentResolverSupport.RequiredArray(entry.Value, field, entry.Origin);
            for (var index = 0; index < values.Count; index++)
            {
                var reference = ResolverErrors.Reference(entry.Origin, $"/{field}/{index}");
                if (values[index] is not JsonValue value
                    || !value.TryGetValue<string>(out var id)
                    || string.IsNullOrEmpty(id))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.InvalidPack,
                        $"{entry.Origin.Id}.{field}/{index} must be a non-empty string.",
                        reference);
                }

                var target = catalog.Require(category, id, reference);
                if (expectedAssetKind is not null
                    && !string.Equals(target.Value["kind"]?.GetValue<string>(), expectedAssetKind, StringComparison.Ordinal))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.WrongAssetKind,
                        $"{id} must be an {expectedAssetKind} asset.",
                        reference,
                        new Dictionary<string, object?>
                        {
                            ["expectedKind"] = expectedAssetKind,
                            ["actualKind"] = target.Value["kind"]?.GetValue<string>()
                        });
                }
            }
        }
    }
}
