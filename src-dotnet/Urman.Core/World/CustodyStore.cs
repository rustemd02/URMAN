using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;

namespace Urman.Core.World;

public static class CustodyStore
{
    public const string DefaultStateKey = "world.custody";

    public static CommandPlan PlanBatch(
        JsonElement state,
        JsonElement operations,
        string claimOwnerId,
        string claimLifecycleScope,
        string stateKey = DefaultStateKey,
        string eventType = "world.custody.changed")
    {
        RequireObject(state, "Custody state source");
        RequireArray(operations, "Custody operations");
        stateKey = Required(stateKey, "Custody state key");
        eventType = Required(eventType, "Custody event type");
        var normalized = operations.EnumerateArray().Select((operation, index) => NormalizeOperation(operation, index)).ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Custody operations must be a non-empty array.", nameof(operations));
        }

        var claims = ItemResourceClaims(normalized, claimOwnerId, claimLifecycleScope);
        var next = ReadItems(state, stateKey).Select(item => item.DeepClone().AsObject()).ToList();
        var indexById = next.Select((item, index) => (Id: RequiredString(item, "itemId"), Index: index))
            .ToDictionary(entry => entry.Id, entry => entry.Index, StringComparer.Ordinal);
        string? invalidReason = null;
        foreach (var operation in normalized)
        {
            var itemId = RequiredString(operation, "itemId");
            if (!indexById.TryGetValue(itemId, out var itemIndex))
            {
                invalidReason = $"Custody item {itemId} does not exist.";
                break;
            }

            var item = next[itemIndex];
            var operationType = RequiredString(operation, "op");
            var expectedOwner = operationType == "consume"
                ? RequiredString(operation, "ownerId")
                : RequiredString(operation, "fromOwnerId");
            if (RequiredString(item, "custodyOwnerId") != expectedOwner)
            {
                invalidReason = $"Custody item {itemId} is not held by {expectedOwner}.";
                break;
            }

            if (operationType == "consume")
            {
                next.RemoveAt(itemIndex);
                indexById.Remove(itemId);
                for (var index = itemIndex; index < next.Count; index++)
                {
                    indexById[RequiredString(next[index], "itemId")] = index;
                }
            }
            else
            {
                item["custodyOwnerId"] = RequiredString(operation, "toOwnerId");
            }
        }

        if (invalidReason is not null)
        {
            return new(
                Effects: [new(StateEffectOperation.Increment, stateKey, Delta: double.NaN)],
                Claims: claims,
                Value: JsonSerializer.SerializeToElement(new { invalidReason }));
        }

        var items = new JsonArray(next.Select(item => (JsonNode?)item).ToArray());
        return new(
            Effects: [new(StateEffectOperation.Set, stateKey, JsonSerializer.SerializeToElement(items))],
            Events: [new(eventType, JsonSerializer.SerializeToElement(new { operations = normalized.Select(item => JsonSerializer.SerializeToElement(item)).ToArray() }))],
            Claims: claims,
            Value: JsonSerializer.SerializeToElement(new { items = JsonSerializer.SerializeToElement(items) }));
    }

    public static IReadOnlyList<ResourceClaim> ItemResourceClaims(JsonElement operations, string ownerId, string lifecycleScope)
    {
        RequireArray(operations, "Item claim operations");
        return ItemResourceClaims(
            operations.EnumerateArray().Select((operation, index) => NormalizeOperation(operation, index)).ToArray(),
            ownerId,
            lifecycleScope);
    }

    private static IReadOnlyList<ResourceClaim> ItemResourceClaims(
        IReadOnlyList<JsonObject> operations,
        string ownerId,
        string lifecycleScope)
    {
        ownerId = Required(ownerId, "Item claim owner ID");
        lifecycleScope = Required(lifecycleScope, "Item claim lifecycle scope");
        return operations
            .Select(operation => RequiredString(operation, "itemId"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(itemId => new ResourceClaim($"item/{itemId}", ownerId, lifecycleScope, ResourceClaimMode.Exclusive))
            .ToArray();
    }

    private static IReadOnlyList<JsonObject> ReadItems(JsonElement state, string stateKey)
    {
        if (!state.TryGetProperty(stateKey, out var items))
        {
            return [];
        }

        RequireArray(items, $"Custody state {stateKey}");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        return items.EnumerateArray().Select((item, index) =>
        {
            RequireObject(item, $"Custody item {index}");
            RequireExactProperties(item, ["itemId", "custodyOwnerId", "condition"], $"Custody item {index}");
            var normalized = JsonNode.Parse(item.GetRawText())!.AsObject();
            var itemId = RequiredString(normalized, "itemId");
            _ = RequiredString(normalized, "custodyOwnerId");
            if (!ids.Add(itemId))
            {
                throw new ArgumentException($"Duplicate custody item {itemId}.");
            }

            return normalized;
        }).ToArray();
    }

    private static JsonObject NormalizeOperation(JsonElement operation, int index)
    {
        RequireObject(operation, $"Custody operation {index}");
        var type = RequiredString(operation, "op");
        var expected = type switch
        {
            "claim" or "transfer" => new[] { "op", "itemId", "fromOwnerId", "toOwnerId" },
            "consume" => new[] { "op", "itemId", "ownerId" },
            _ => throw new ArgumentException($"Unknown custody operation {type}.")
        };
        RequireExactProperties(operation, expected, $"Custody operation {index}");
        var result = JsonNode.Parse(operation.GetRawText())!.AsObject();
        _ = RequiredString(result, "itemId");
        if (type == "consume")
        {
            _ = RequiredString(result, "ownerId");
        }
        else
        {
            _ = RequiredString(result, "fromOwnerId");
            _ = RequiredString(result, "toOwnerId");
        }

        return result;
    }

    private static void RequireExactProperties(JsonElement value, IReadOnlyCollection<string> expected, string label)
    {
        var actual = value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException($"{label} has an invalid shape.");
        }
    }

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? Required(element.GetString(), property)
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string RequiredString(JsonObject value, string property) =>
        value[property] is JsonValue element && element.TryGetValue<string>(out var result)
            ? Required(result, property)
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string Required(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{label} must be a non-empty string.");

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{label} must be an object.");
    }

    private static void RequireArray(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new ArgumentException($"{label} must be an array.");
    }
}
