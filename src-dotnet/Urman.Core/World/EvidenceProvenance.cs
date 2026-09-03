using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;

namespace Urman.Core.World;

public static class EvidenceProvenance
{
    public const string DefaultStateKey = "world.evidence";

    public static CommandPlan PlanClaim(
        JsonElement state,
        string evidenceId,
        string claimantId,
        string sourceId,
        JsonElement metadata = default,
        string stateKey = DefaultStateKey,
        string eventType = "world.evidence.claimed")
    {
        if (state.ValueKind != JsonValueKind.Object) throw new ArgumentException("Evidence state source must be an object.");
        stateKey = Required(stateKey, "Evidence state key");
        evidenceId = Required(evidenceId, "Evidence ID");
        claimantId = Required(claimantId, "Evidence claimant ID");
        sourceId = Required(sourceId, "Evidence source ID");
        eventType = Required(eventType, "Evidence event type");
        var evidence = CurrentEvidence(state, stateKey);
        if (evidence.ContainsKey(evidenceId)) throw new ArgumentException($"Evidence claim {evidenceId} already exists.");
        if (sourceId == evidenceId) throw new ArgumentException("Evidence cannot cite itself as its source.");

        var provenance = evidence.TryGetValue(sourceId, out var source)
            ? RequiredArray(source, "provenanceChain").Select(RequiredStringNode).Append(sourceId).ToArray()
            : [sourceId];
        if (provenance.Contains(evidenceId, StringComparer.Ordinal)) throw new ArgumentException("Evidence provenance cycle is not allowed.");
        var record = new JsonObject
        {
            ["evidenceId"] = evidenceId,
            ["claimantId"] = claimantId,
            ["sourceId"] = sourceId,
            ["provenanceChain"] = new JsonArray(provenance.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["metadata"] = metadata.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? null
                : JsonNode.Parse(metadata.GetRawText())
        };
        evidence.Add(evidenceId, record);
        var next = new JsonArray(evidence.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (JsonNode?)entry.Value.DeepClone()).ToArray());
        return new(
            Effects: [new(StateEffectOperation.Set, stateKey, JsonSerializer.SerializeToElement(next))],
            Events: [new(eventType, JsonSerializer.SerializeToElement(new { evidenceId, claimantId, sourceId, provenanceChain = provenance }))],
            Value: JsonSerializer.SerializeToElement(record));
    }

    private static Dictionary<string, JsonObject> CurrentEvidence(JsonElement state, string stateKey)
    {
        if (!state.TryGetProperty(stateKey, out var records)) return new(StringComparer.Ordinal);
        if (records.ValueKind != JsonValueKind.Array) throw new ArgumentException($"Evidence state {stateKey} must be an array.");
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (entry, index) in records.EnumerateArray().Select((entry, index) => (entry, index)))
        {
            if (entry.ValueKind != JsonValueKind.Object) throw new ArgumentException($"Evidence claim {index} must be an object.");
            var expected = new[] { "evidenceId", "claimantId", "sourceId", "provenanceChain", "metadata" };
            var actual = entry.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new ArgumentException($"Evidence claim {index} has an invalid shape.");
            }

            var node = JsonNode.Parse(entry.GetRawText())!.AsObject();
            var evidenceId = RequiredString(node, "evidenceId");
            _ = RequiredString(node, "claimantId");
            _ = RequiredString(node, "sourceId");
            var chain = RequiredArray(node, "provenanceChain");
            if (chain.Count == 0 || chain.Any(value => string.IsNullOrWhiteSpace(RequiredStringNode(value))))
            {
                throw new ArgumentException($"Evidence claim {index} needs provenance.");
            }

            if (!result.TryAdd(evidenceId, node)) throw new ArgumentException($"Duplicate evidence claim {evidenceId}.");
        }

        return result;
    }

    private static JsonArray RequiredArray(JsonObject owner, string property) =>
        owner[property] as JsonArray ?? throw new ArgumentException($"{property} must be an array.");

    private static string RequiredString(JsonObject owner, string property) =>
        owner[property] is JsonValue value && value.TryGetValue<string>(out var result)
            ? Required(result, property)
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string RequiredStringNode(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result)
            ? Required(result, "Evidence provenance source ID")
            : throw new ArgumentException("Evidence provenance source ID must be a non-empty string.");

    private static string Required(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{label} must be a non-empty string.");
}
