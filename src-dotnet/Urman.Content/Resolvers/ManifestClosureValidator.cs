using System.Text.Json.Nodes;

namespace Urman.Content.Resolvers;

public static class ManifestClosureValidator
{
    public static IReadOnlyList<ContentReference> ReadReferences(string category, JsonObject entity)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(entity);

        var id = entity["id"] is JsonValue idValue
            && idValue.TryGetValue<string>(out var value)
            && !string.IsNullOrEmpty(value)
                ? value
                : "entity";
        return ReadReferences(category, entity, new ResolverOrigin(id, "unknown-module", string.Empty));
    }

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
            ResolveReferences(catalog, entry.Origin, ReadReferences("assets", asset, entry.Origin));

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

        EnsureReferences(catalog, catalog.Category("characters").Values, "characters", "assets");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "assets");
        EnsureReferences(catalog, catalog.Category("documents").Values, "documents", "assets");
        EnsureReferences(catalog, catalog.Category("documents").Values, "documents", "knowledge");
        EnsureReferences(catalog, catalog.Category("capabilities").Values, "capabilities", "assets");
        EnsureReferences(catalog, catalog.Category("capabilities").Values, "capabilities", "assets", "audio");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "texts");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "scenes");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "dialogues");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "documents");
        EnsureReferences(catalog, catalog.Category("scenes").Values, "scenes", "knowledge");

        foreach (var entry in catalog.Category("dialogues").Values)
        {
            ResolveReferences(catalog, entry.Origin, ReadReferences("dialogues", entry.Value, entry.Origin));
        }

        foreach (var entry in catalog.Category("quests").Values)
        {
            ResolveReferences(catalog, entry.Origin, ReadReferences("quests", entry.Value, entry.Origin));
        }

        foreach (var entry in catalog.Category("knowledge").Values)
        {
            ResolveReferences(catalog, entry.Origin, ReadReferences("knowledge", entry.Value, entry.Origin));
        }

        foreach (var entry in catalog.Category("vocabulary").Values)
        {
            ResolveReferences(catalog, entry.Origin, ReadReferences("vocabulary", entry.Value, entry.Origin));
        }

        return new ManifestClosure(catalog.Ids("assets"), catalog.Ids("texts"), catalog.Ids("vocabulary"));
    }

    private static IReadOnlyList<ContentReference> ReadReferences(
        string category,
        JsonObject entity,
        ResolverOrigin origin)
    {
        var references = new List<ContentReference>();
        switch (category)
        {
            case "assets":
            {
                var accessibility = ContentResolverSupport.AccessibilityFor(entity, origin);
                AddOptionalReference(references, accessibility.AltTextId, "/accessibility/altTextId", "texts");
                AddOptionalReference(references, accessibility.CaptionTextId, "/accessibility/captionTextId", "texts");
                AddOptionalReference(references, accessibility.AudioDescriptionTextId, "/accessibility/audioDescriptionTextId", "texts");
                break;
            }
            case "characters":
                AddArrayReferences(references, entity, "assetIds", "assets", origin);
                break;
            case "scenes":
                AddArrayReferences(references, entity, "assetRefs", "assets", origin);
                AddArrayReferences(references, entity, "textRefs", "texts", origin);
                AddSceneInteractionReferences(references, entity, origin);
                break;
            case "documents":
                AddArrayReferences(references, entity, "assetRefs", "assets", origin);
                AddArrayReferences(references, entity, "knowledgeRefs", "knowledge", origin);
                break;
            case "capabilities":
                AddArrayReferences(references, entity, "requiredAssets", "assets", origin);
                AddArrayReferences(references, entity, "requiredAudio", "assets", origin, "audio");
                break;
            case "dialogues":
                AddDialogueReferences(references, entity, origin);
                break;
            case "quests":
                AddQuestReferences(references, entity, origin);
                break;
            case "knowledge":
                AddArrayReferences(references, entity, "sourceIds", null, origin);
                AddArrayReferences(references, entity, "reveals", null, origin);
                AddArrayReferences(references, entity, "contradicts", null, origin);
                AddArrayReferences(references, entity, "unlocks", null, origin);
                break;
            case "vocabulary":
                AddArrayReferences(references, entity, "rereadTargets", null, origin);
                AddArrayReferences(references, entity, "applicationTargets", null, origin);
                break;
            default:
                throw new NotSupportedException($"Typed references are not defined for resolver category '{category}'.");
        }

        return references.AsReadOnly();
    }

    private static void AddDialogueReferences(List<ContentReference> references, JsonObject entity, ResolverOrigin origin)
    {
        var nodes = ContentResolverSupport.RequiredArray(entity, "nodes", origin);
        for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            var nodePointer = $"/nodes/{nodeIndex}";
            var nodeOrigin = ResolverErrors.Reference(origin, nodePointer);
            var node = ResolverJson.RequiredObject(nodes[nodeIndex], nodeOrigin.SourcePointer);
            var textId = ResolverJson.RequiredString(node, "textId", nodeOrigin);
            references.Add(new ContentReference($"{nodePointer}/textId", "texts", textId));

            var choices = ContentResolverSupport.RequiredArray(node, "choices", nodeOrigin);
            for (var choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
            {
                var choicePointer = $"{nodePointer}/choices/{choiceIndex}";
                var choiceOrigin = ResolverErrors.Reference(origin, choicePointer);
                var choice = ResolverJson.RequiredObject(choices[choiceIndex], choiceOrigin.SourcePointer);
                var choiceTextId = ResolverJson.RequiredString(choice, "textId", choiceOrigin);
                references.Add(new ContentReference($"{choicePointer}/textId", "texts", choiceTextId));
            }
        }
    }

    private static void AddQuestReferences(List<ContentReference> references, JsonObject entity, ResolverOrigin origin)
    {
        var titleTextId = ResolverJson.RequiredString(entity, "titleTextId", origin);
        references.Add(new ContentReference("/titleTextId", "texts", titleTextId));

        var stages = ContentResolverSupport.RequiredArray(entity, "stages", origin);
        for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
        {
            var stagePointer = $"/stages/{stageIndex}";
            var stageOrigin = ResolverErrors.Reference(origin, stagePointer);
            var stage = ResolverJson.RequiredObject(stages[stageIndex], stageOrigin.SourcePointer);
            var objectives = ContentResolverSupport.RequiredArray(stage, "objectives", stageOrigin);
            for (var objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex++)
            {
                var objectivePointer = $"{stagePointer}/objectives/{objectiveIndex}";
                var objectiveOrigin = ResolverErrors.Reference(origin, objectivePointer);
                var objective = ResolverJson.RequiredObject(objectives[objectiveIndex], objectiveOrigin.SourcePointer);
                var objectiveTextId = ResolverJson.RequiredString(objective, "titleTextId", objectiveOrigin);
                references.Add(new ContentReference($"{objectivePointer}/titleTextId", "texts", objectiveTextId));
            }
        }
    }

    private static void AddSceneInteractionReferences(List<ContentReference> references, JsonObject entity, ResolverOrigin origin)
    {
        var interactions = ContentResolverSupport.RequiredArray(entity, "interactions", origin);
        for (var interactionIndex = 0; interactionIndex < interactions.Count; interactionIndex++)
        {
            var interactionPointer = $"/interactions/{interactionIndex}";
            var interactionOrigin = ResolverErrors.Reference(origin, interactionPointer);
            var interaction = ResolverJson.RequiredObject(interactions[interactionIndex], interactionOrigin.SourcePointer);
            var labelTextId = ResolverJson.RequiredString(interaction, "labelTextId", interactionOrigin);
            references.Add(new ContentReference($"{interactionPointer}/labelTextId", "texts", labelTextId));
            AddOptionalStringReference(references, interaction, "targetSceneId", "scenes", interactionPointer, interactionOrigin);
            AddOptionalStringReference(references, interaction, "targetDialogueId", "dialogues", interactionPointer, interactionOrigin);
            AddOptionalStringReference(references, interaction, "targetDocumentId", "documents", interactionPointer, interactionOrigin);
            AddOptionalStringReference(references, interaction, "targetJournalEntryId", "knowledge", interactionPointer, interactionOrigin);
        }
    }

    private static void AddArrayReferences(
        List<ContentReference> references,
        JsonObject entity,
        string field,
        string? targetCategory,
        ResolverOrigin origin,
        string? expectedAssetKind = null)
    {
        var values = ContentResolverSupport.RequiredArray(entity, field, origin);
        for (var index = 0; index < values.Count; index++)
        {
            var pointer = $"/{field}/{index}";
            var reference = ResolverErrors.Reference(origin, pointer);
            if (values[index] is not JsonValue value
                || !value.TryGetValue<string>(out var id)
                || string.IsNullOrEmpty(id))
            {
                throw ResolverErrors.Create(
                    ResolverErrorCode.InvalidPack,
                    $"{origin.Id}.{field}/{index} must be a non-empty string.",
                    reference);
            }

            var resolvedTargetCategory = targetCategory;
            if (resolvedTargetCategory is null)
            {
                if (!ResolverCatalog.TryGetCategoryForLogicalId(id, out var inferredTargetCategory))
                {
                    throw ResolverErrors.Create(
                        ResolverErrorCode.InvalidPack,
                        $"{origin.Id}.{field}/{index} must reference a canonical ContentId with a known resolver kind.",
                        reference,
                        new Dictionary<string, object?> { ["targetId"] = id });
                }

                resolvedTargetCategory = inferredTargetCategory;
            }

            references.Add(new ContentReference(pointer, resolvedTargetCategory, id, expectedAssetKind));
        }
    }

    private static void AddOptionalReference(
        List<ContentReference> references,
        string? id,
        string pointer,
        string targetCategory)
    {
        if (id is not null)
        {
            references.Add(new ContentReference(pointer, targetCategory, id));
        }
    }

    private static void AddOptionalStringReference(
        List<ContentReference> references,
        JsonObject entity,
        string field,
        string targetCategory,
        string entityPointer,
        ResolverOrigin entityOrigin)
    {
        if (!entity.ContainsKey(field))
        {
            return;
        }

        var id = ResolverJson.RequiredString(entity, field, entityOrigin);
        references.Add(new ContentReference($"{entityPointer}/{field}", targetCategory, id));
    }

    private static void EnsureReferences(
        ResolverCatalog catalog,
        IEnumerable<ResolverCatalogEntry> entries,
        string sourceCategory,
        string targetCategory,
        string? expectedAssetKind = null)
    {
        foreach (var entry in entries)
        {
            var references = ReadReferences(sourceCategory, entry.Value, entry.Origin);
            ResolveReferences(
                catalog,
                entry.Origin,
                references.Where(reference =>
                    string.Equals(reference.TargetCategory, targetCategory, StringComparison.Ordinal)
                    && string.Equals(reference.ExpectedAssetKind, expectedAssetKind, StringComparison.Ordinal)));
        }
    }

    private static void ResolveReferences(
        ResolverCatalog catalog,
        ResolverOrigin origin,
        IEnumerable<ContentReference> references)
    {
        foreach (var reference in references)
        {
            var pointer = ResolverErrors.Reference(origin, reference.Pointer);
            var target = catalog.Require(reference.TargetCategory, reference.TargetId, pointer);
            if (reference.ExpectedAssetKind is not null
                && !string.Equals(target.Value["kind"]?.GetValue<string>(), reference.ExpectedAssetKind, StringComparison.Ordinal))
            {
                throw ResolverErrors.Create(
                    ResolverErrorCode.WrongAssetKind,
                    $"{reference.TargetId} must be an {reference.ExpectedAssetKind} asset.",
                    pointer,
                    new Dictionary<string, object?>
                    {
                        ["expectedKind"] = reference.ExpectedAssetKind,
                        ["actualKind"] = target.Value["kind"]?.GetValue<string>()
                    });
            }
        }
    }
}
