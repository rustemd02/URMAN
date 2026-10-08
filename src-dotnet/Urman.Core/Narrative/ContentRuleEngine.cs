using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;

namespace Urman.Core.Narrative;

public sealed record ContentRulePlan(
    IReadOnlyList<StateEffect> Effects,
    IReadOnlyList<EventDraft> Events,
    JsonElement ProjectedState);

public static class ContentRuleEngine
{
    public static bool EvaluateAll(JsonElement conditions, JsonElement state)
    {
        RequireArray(conditions, "conditions");
        return conditions.EnumerateArray().All(condition => Evaluate(condition, state));
    }

    public static bool Evaluate(JsonElement condition, JsonElement state)
    {
        RequireObject(condition, "condition");
        RequireObject(state, "state");
        var operation = RequiredString(condition, "op");
        return operation switch
        {
            "all" => EvaluateAll(condition.GetProperty("conditions"), state),
            "any" => condition.GetProperty("conditions").EnumerateArray().Any(item => Evaluate(item, state)),
            "not" => !Evaluate(condition.GetProperty("condition"), state),
            "knowledge.status" => HasStatus(state, "knowledge", ContentId(condition, "knowledgeId"), RequiredString(condition, "status")),
            "vocabulary.status" => HasStatus(state, "vocabulary", ContentId(condition, "vocabularyId"), RequiredString(condition, "status")),
            "quest.status" => HasStatus(state, "quests", ContentId(condition, "questId"), RequiredString(condition, "status")),
            "npc.state" => NpcStateEquals(condition, state),
            "beat.state" => BeatStateEquals(condition, state),
            "location.is" => LocationEquals(condition, state),
            _ when PhotoWorldState.IsConditionOperation(operation) => PhotoWorldState.EvaluateCondition(condition, state),
            _ => throw new ArgumentException($"Unknown content condition operation {operation}.")
        };
    }

    public static ContentRulePlan PlanEffects(JsonElement effects, JsonElement state)
    {
        RequireArray(effects, "effects");
        RequireObject(state, "state");
        var staged = JsonNode.Parse(state.GetRawText())!.AsObject();
        var plannedEffects = new List<StateEffect>();
        var events = new List<EventDraft>();
        foreach (var effect in effects.EnumerateArray())
        {
            ApplyEffect(effect, staged, plannedEffects, events);
        }

        return new(plannedEffects, events, JsonSerializer.SerializeToElement(staged));
    }

    private static void ApplyEffect(
        JsonElement effect,
        JsonObject staged,
        ICollection<StateEffect> plannedEffects,
        ICollection<EventDraft> events)
    {
        RequireObject(effect, "effect");
        var operation = RequiredString(effect, "op");
        switch (operation)
        {
            case "knowledge.set-status":
                SetStatus(staged, plannedEffects, "knowledge", ContentId(effect, "knowledgeId"), RequiredString(effect, "status"));
                break;
            case "vocabulary.set-status":
                SetStatus(staged, plannedEffects, "vocabulary", ContentId(effect, "vocabularyId"), RequiredString(effect, "status"));
                break;
            case "npc.set-state":
                SetNpcState(staged, plannedEffects, effect);
                break;
            case "beat.set-state":
                SetMapValue(staged, plannedEffects, "beats", ContentId(effect, "beatId"), JsonValue.Create(RequiredString(effect, "state"))!);
                break;
            case "pressure.change":
                ChangePressure(staged, plannedEffects, effect);
                break;
            case "route.unlock":
                UnlockRoute(staged, plannedEffects, effect);
                break;
            case "audio.request":
                events.Add(new("runtime.audio.requested", JsonSerializer.SerializeToElement(new { assetId = ContentId(effect, "assetId") })));
                break;
            case "scene.request":
                RequestScene(staged, plannedEffects, effect);
                break;
            case "document.open":
                OpenDocument(staged, plannedEffects, events, effect);
                break;
            case "journal.record":
                RecordJournal(staged, plannedEffects, events, effect);
                break;
            default:
                if (PhotoWorldState.IsEffectOperation(operation))
                {
                    PhotoWorldState.ApplyEffect(effect, staged, events);
                    Commit(staged, plannedEffects, "photoworlds", staged["photoworlds"]!);
                    break;
                }
                throw new ArgumentException($"Unknown content effect operation {operation}.");
        }
    }

    private static void SetStatus(JsonObject staged, ICollection<StateEffect> effects, string registry, string id, string status)
    {
        var map = RequireObject(staged, registry);
        // Vocabulary is a ladder, not a slot: unknown -> guessed -> confirmed.
        // Scene-entry hints write "guessed" while the player only hears a word,
        // and a discovery writes "confirmed" once they understand it. Both are
        // correct on their own, but with last-write-wins the hint erased work
        // the player had already done whenever the scene happened to load after
        // the discovery. A word that has been understood is never un-learned by
        // hearing it again, so a write that would step back down the ladder is
        // ignored. Knowledge keeps its own richer statuses and last-write-wins.
        if (registry == "vocabulary"
            && map[id] is JsonObject current
            && current["status"] is JsonValue existing
            && existing.TryGetValue<string>(out var previous)
            && VocabularyRank(status) < VocabularyRank(previous))
        {
            return;
        }

        map[id] = new JsonObject { ["status"] = status };
        Commit(staged, effects, registry, map);
    }

    private static int VocabularyRank(string status) => status switch
    {
        "confirmed" => 2,
        "guessed" => 1,
        _ => 0,
    };

    private static void SetNpcState(JsonObject staged, ICollection<StateEffect> effects, JsonElement effect)
    {
        var npc = RequireObject(staged, "npc");
        var characterId = ContentId(effect, "characterId");
        var character = npc[characterId] as JsonObject ?? new JsonObject();
        character[RequiredString(effect, "stateKey")] = JsonNode.Parse(effect.GetProperty("value").GetRawText());
        npc[characterId] = character;
        Commit(staged, effects, "npc", npc);
    }

    private static void ChangePressure(JsonObject staged, ICollection<StateEffect> effects, JsonElement effect)
    {
        if (!effect.GetProperty("delta").TryGetInt32(out var delta))
        {
            throw new ArgumentException("pressure.change delta must be an integer.");
        }

        var pressure = Math.Clamp(staged["pressure"]?.GetValue<int>() ?? 0, 0, 3);
        staged["pressure"] = Math.Clamp(checked(pressure + delta), 0, 3);
        Commit(staged, effects, "pressure", staged["pressure"]!);
    }

    private static void UnlockRoute(JsonObject staged, ICollection<StateEffect> effects, JsonElement effect)
    {
        var routeId = ContentId(effect, "routeNodeId");
        var routes = RequireArray(staged, "routes").Select(node => node!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        routes.Add(routeId);
        var next = new JsonArray(routes.Order(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        staged["routes"] = next;
        Commit(staged, effects, "routes", next);
    }

    private static void RequestScene(JsonObject staged, ICollection<StateEffect> effects, JsonElement effect)
    {
        var sceneId = ContentId(effect, "sceneId");
        var presentation = RequireObject(staged, "presentation");
        var requested = RequireArray(presentation, "requestedSceneIds")
            .Select(node => node!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        requested.Add(sceneId);
        presentation["requestedSceneIds"] = new JsonArray(requested.Order(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        Commit(staged, effects, "presentation", presentation);
    }

    private static void OpenDocument(
        JsonObject staged,
        ICollection<StateEffect> effects,
        ICollection<EventDraft> events,
        JsonElement effect)
    {
        var documentId = ContentId(effect, "documentId");
        var presentation = RequireObject(staged, "presentation");
        var opened = RequireArray(presentation, "openedDocumentIds")
            .Select(node => node!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        opened.Add(documentId);
        var next = new JsonArray(opened
            .Order(StringComparer.Ordinal)
            .Select(value => (JsonNode?)JsonValue.Create(value))
            .ToArray());
        presentation["openedDocumentIds"] = next;
        Commit(staged, effects, "presentation", presentation);
        events.Add(new("document.opened", JsonSerializer.SerializeToElement(new { documentId })));
    }

    private static void RecordJournal(
        JsonObject staged,
        ICollection<StateEffect> effects,
        ICollection<EventDraft> events,
        JsonElement effect)
    {
        var entryId = ContentId(effect, "entryId");
        var sourceId = ContentId(effect, "sourceId");
        var journal = RequireArray(staged, "journal");
        if (!journal.OfType<JsonObject>().Any(item => item["entryId"]?.GetValue<string>() == entryId))
        {
            journal.Add(new JsonObject { ["entryId"] = entryId, ["sourceId"] = sourceId });
            Commit(staged, effects, "journal", journal);
        }

        events.Add(new("journal.changed", JsonSerializer.SerializeToElement(new { entryId, sourceId })));
    }

    private static void SetMapValue(
        JsonObject staged,
        ICollection<StateEffect> effects,
        string registry,
        string id,
        JsonNode value)
    {
        var map = RequireObject(staged, registry);
        map[id] = value;
        Commit(staged, effects, registry, map);
    }

    private static void Commit(JsonObject staged, ICollection<StateEffect> effects, string key, JsonNode value)
    {
        staged[key] = value.DeepClone();
        effects.Add(new(StateEffectOperation.Set, key, JsonSerializer.SerializeToElement(value)));
    }

    private static bool HasStatus(JsonElement state, string registry, string id, string status)
    {
        if (!state.GetProperty(registry).TryGetProperty(id, out var entry))
        {
            return status is "hidden" or "unknown" or "inactive";
        }

        return entry.ValueKind == JsonValueKind.String
            ? entry.GetString() == status
            : entry.TryGetProperty("status", out var actual) && actual.GetString() == status;
    }

    private static bool NpcStateEquals(JsonElement condition, JsonElement state)
    {
        var characterId = ContentId(condition, "characterId");
        var stateKey = RequiredString(condition, "stateKey");
        return state.GetProperty("npc").TryGetProperty(characterId, out var character) &&
            character.TryGetProperty(stateKey, out var actual) &&
            JsonElement.DeepEquals(actual, condition.GetProperty("value"));
    }

    private static bool BeatStateEquals(JsonElement condition, JsonElement state)
    {
        var beatId = ContentId(condition, "beatId");
        var expected = condition.TryGetProperty("state", out var stateValue)
            ? stateValue.GetString()
            : condition.GetProperty("phase").GetString();
        return state.GetProperty("beats").TryGetProperty(beatId, out var actual) &&
            (actual.ValueKind == JsonValueKind.String ? actual.GetString() : actual.GetProperty("state").GetString()) == expected;
    }

    private static bool LocationEquals(JsonElement condition, JsonElement state)
    {
        var expected = ContentId(condition, "locationId");
        return state.TryGetProperty("activeScene", out var activeScene) && activeScene.ValueKind == JsonValueKind.String && activeScene.GetString() == expected;
    }

    private static string ContentId(JsonElement value, string property) =>
        new Contracts.ContentId(RequiredString(value, property)).Value;

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{label} must be an object.");
        }
    }

    private static void RequireArray(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"{label} must be an array.");
        }
    }

    private static JsonObject RequireObject(JsonObject owner, string property) =>
        owner[property] as JsonObject ?? throw new ArgumentException($"Runtime state {property} must be an object.");

    private static JsonArray RequireArray(JsonObject owner, string property) =>
        owner[property] as JsonArray ?? throw new ArgumentException($"Runtime state {property} must be an array.");
}
