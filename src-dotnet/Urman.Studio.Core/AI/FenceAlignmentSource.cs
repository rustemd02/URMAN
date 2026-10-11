using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.AI;

/// <summary>Changes an approved fence source scope without reformatting unrelated JSON.</summary>
/// <remarks>The caller must bind run ID and bay count to live geometry; lot source does not contain generated clipped runs.</remarks>
public static class FenceAlignmentSource
{
    private const int MaxBayCount = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Rewrite(
        string sourcePath,
        string sourceText,
        string lotId,
        string runId,
        int bayCount,
        IReadOnlyList<int> interiorPostIndices,
        bool align)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(lotId);
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(interiorPostIndices);

        var resourcePath = sourcePath switch
        {
            "game/content/world/act1_open_part.world.v1.json" => "res://content/world/act1_open_part.world.v1.json",
            "game/content/world/act1_far_bank.world.v1.json" => "res://content/world/act1_far_bank.world.v1.json",
            _ => throw new ArgumentException("Fence alignment can edit only the two active Act 1 yard sources.", nameof(sourcePath))
        };
        if (string.IsNullOrWhiteSpace(lotId) || lotId.Contains('|', StringComparison.Ordinal))
            throw new ArgumentException("A fence lot ID must be non-empty and cannot contain '|'.", nameof(lotId));
        if (bayCount is < 2 or > MaxBayCount)
            throw new ArgumentOutOfRangeException(nameof(bayCount), $"Bay count must be between 2 and {MaxBayCount}.");
        if (interiorPostIndices.Count is < 1 or > 2)
            throw new ArgumentException("Select one or two interior fence posts.", nameof(interiorPostIndices));

        var requested = new List<int>(interiorPostIndices.Count);
        var requestedSet = new HashSet<int>();
        foreach (var index in interiorPostIndices)
        {
            if (index < 1 || index >= bayCount || !requestedSet.Add(index))
                throw new ArgumentException("Fence posts must be unique interior bay indices.", nameof(interiorPostIndices));
            requested.Add(index);
        }
        ValidateRunId(resourcePath, lotId, runId);

        ValidateDocument(sourceText);
        var root = JsonSpanDocument.Parse(sourceText);
        if (root.RootIsArray)
            throw new InvalidDataException("Fence source root must be an object.");
        var lotsIndex = FindUniqueProperty(root, "lots", required: true);
        var lots = JsonSpanDocument.Parse(root.RawAt(lotsIndex));
        if (!lots.RootIsArray)
            throw new InvalidDataException("Fence source 'lots' must be an array.");

        var lotIds = new HashSet<string>(StringComparer.Ordinal);
        var selectedLotIndex = -1;
        for (var index = 0; index < lots.Count; index++)
        {
            if (lots.Get(index) is not JsonObject lot)
                throw new InvalidDataException($"Fence source lots[{index}] must be an object.");
            var foundId = RequiredString(lot, "id", $"lots[{index}]");
            var kind = RequiredString(lot, "kind", $"lots[{index}]");
            if (!lotIds.Add(foundId))
                throw new InvalidDataException($"Fence source contains duplicate lot ID '{foundId}'.");
            if (foundId == lotId)
            {
                if (kind.Equals("keep", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A keep lot cannot receive fence alignment.");
                selectedLotIndex = index;
            }
        }
        if (selectedLotIndex < 0)
            throw new InvalidDataException($"Fence source does not contain lot '{lotId}'.");

        var lotText = lots.RawAt(selectedLotIndex);
        var selectedLot = JsonSpanDocument.Parse(lotText);
        if (selectedLot.RootIsArray)
            throw new InvalidDataException("Selected fence lot must be an object.");
        var alignmentIndex = FindUniqueProperty(selectedLot, "fenceAlignment", required: false);
        if (alignmentIndex < 0)
        {
            if (!align)
                return sourceText;

            var alignment = new JsonObject { [runId] = ToJsonArray(requested) };
            var updatedLot = AddProperty(selectedLot, lotText, "fenceAlignment", alignment);
            return ReplaceLotAndValidate(root, lots, lotsIndex, selectedLotIndex, updatedLot);
        }

        var alignmentText = selectedLot.RawAt(alignmentIndex);
        var alignmentMap = JsonSpanDocument.Parse(alignmentText);
        if (alignmentMap.RootIsArray)
            throw new InvalidDataException("Selected lot 'fenceAlignment' must be an object.");
        var runIndex = FindUniqueProperty(alignmentMap, runId, required: false);
        if (runIndex < 0)
        {
            if (!align)
                return sourceText;

            var updatedMap = AddProperty(alignmentMap, alignmentText, runId, ToJsonArray(requested));
            selectedLot.ReplaceRaw(alignmentIndex, updatedMap);
            return ReplaceLotAndValidate(root, lots, lotsIndex, selectedLotIndex, selectedLot.ToText());
        }

        var postsText = alignmentMap.RawAt(runIndex);
        var posts = JsonSpanDocument.Parse(postsText);
        if (!posts.RootIsArray || posts.Count >= bayCount)
            throw new InvalidDataException("Selected run alignment must be an interior-post array for this fence run.");

        var existing = new List<int>(posts.Count);
        var existingSet = new HashSet<int>();
        for (var index = 0; index < posts.Count; index++)
        {
            var raw = posts.RawAt(index);
            if (!int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var postIndex)
                || postIndex < 1 || postIndex >= bayCount || !existingSet.Add(postIndex))
                throw new InvalidDataException("Selected run alignment contains an invalid or duplicate interior post index.");
            existing.Add(postIndex);
        }

        var next = align
            ? existing.Concat(requested.Where(index => !existingSet.Contains(index))).ToList()
            : existing.Where(index => !requestedSet.Contains(index)).ToList();
        if (next.SequenceEqual(existing))
            return sourceText;

        if (next.Count == 0)
        {
            alignmentMap.RemoveAt(runIndex);
            if (alignmentMap.Count == 0)
                selectedLot.RemoveAt(alignmentIndex);
            else
                selectedLot.ReplaceRaw(alignmentIndex, alignmentMap.ToText());
        }
        else
        {
            alignmentMap.ReplaceRaw(runIndex, JsonSerializer.Serialize(next));
            selectedLot.ReplaceRaw(alignmentIndex, alignmentMap.ToText());
        }

        return ReplaceLotAndValidate(root, lots, lotsIndex, selectedLotIndex, selectedLot.ToText());
    }

    private static void ValidateRunId(string resourcePath, string lotId, string runId)
    {
        var prefix = $"{resourcePath}|{lotId}|";
        if (!runId.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Run ID does not belong to the selected source and lot.", nameof(runId));
        var tail = runId.AsSpan(prefix.Length);
        var separator = tail.IndexOf('|');
        if (separator <= 0 || separator == tail.Length - 1)
            throw new ArgumentException("Run ID must include a recognized fence edge and generated run identity.", nameof(runId));
        var edge = tail[..separator];
        if (!(edge.SequenceEqual("front") || edge.SequenceEqual("back")
            || edge.SequenceEqual("side-minus") || edge.SequenceEqual("side-plus")))
            throw new ArgumentException("Run ID has an unrecognized fence edge.", nameof(runId));
    }

    private static void ValidateDocument(string text)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(text);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
            ValidateUniqueProperties(document.RootElement);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException("Fence source is not valid UTF-8 text.", error);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Fence source is not one strict JSON document.", error);
        }
    }

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Fence source contains duplicate JSON property '{property.Name}'.");
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateUniqueProperties(item);
        }
    }

    private static int FindUniqueProperty(JsonSpanDocument document, string key, bool required)
    {
        var found = -1;
        for (var index = 0; index < document.Count; index++)
        {
            if (document.KeyAt(index) != key)
                continue;
            if (found >= 0)
                throw new InvalidDataException($"Fence source contains duplicate structural property '{key}'.");
            found = index;
        }
        if (required && found < 0)
            throw new InvalidDataException($"Fence source is missing required property '{key}'.");
        return found;
    }

    private static string RequiredString(JsonObject value, string property, string context)
    {
        if (!value.TryGetPropertyValue(property, out var node)
            || node is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var text)
            || string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Fence source {context} must have a non-empty string '{property}'.");
        return text;
    }

    private static JsonArray ToJsonArray(IEnumerable<int> indices)
    {
        var array = new JsonArray();
        foreach (var index in indices)
            array.Add(JsonValue.Create(index));
        return array;
    }

    private static string AddProperty(JsonSpanDocument document, string rawObject, string key, JsonNode value)
    {
        if (document.Count > 0)
        {
            document.Add(value, key);
            return document.ToText();
        }

        if (rawObject.Length < 2 || rawObject[0] != '{' || rawObject[^1] != '}')
            throw new InvalidDataException("Cannot add a property to the selected JSON object.");
        for (var index = 1; index < rawObject.Length - 1; index++)
        {
            if (!char.IsWhiteSpace(rawObject[index]))
                throw new InvalidDataException("An apparently empty JSON object contains unexpected data.");
        }
        var insertion = $"{JsonSerializer.Serialize(key)}: {value.ToJsonString()}";
        return rawObject.Insert(rawObject.Length - 1, insertion);
    }

    private static string ReplaceLotAndValidate(
        JsonSpanDocument root,
        JsonSpanDocument lots,
        int lotsIndex,
        int lotIndex,
        string updatedLot)
    {
        lots.ReplaceRaw(lotIndex, updatedLot);
        root.ReplaceRaw(lotsIndex, lots.ToText());
        var result = root.ToText();
        ValidateDocument(result);
        return result;
    }
}
