using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;

namespace Urman.Core.Narrative;

public static class NarrativeCommandHandlers
{
    public const string SceneEnter = "scene.enter";
    public const string KnowledgeSetStatus = "knowledge.set-status";
    public const string VocabularyLearn = "vocabulary.learn";
    public const string JournalRecord = "journal.record";
    public const string DialogueChoose = "dialogue.choose";
    public const string QuestSetStage = "quest.set-stage";
    public const string ContentApply = "content.apply";
    public const string PhotoWorldCaptionPrepare = "photoworlds.caption.prepare";

    public static Dictionary<string, RuntimeCommandHandler> Create() => new(StringComparer.Ordinal)
    {
        [SceneEnter] = HandleSceneEnter,
        [KnowledgeSetStatus] = HandleKnowledgeSetStatus,
        [VocabularyLearn] = HandleVocabularyLearn,
        [JournalRecord] = HandleJournalRecord,
        [DialogueChoose] = HandleDialogueChoose,
        [QuestSetStage] = HandleQuestSetStage,
        [ContentApply] = HandleContentApply,
        [PhotoWorldCaptionPrepare] = HandlePhotoWorldCaptionPrepare
    };

    private static CommandPlan HandleSceneEnter(GameCommand command, RuntimeCommandContext context)
    {
        var sceneId = ContentIdFrom(command.Payload, "sceneId");
        return PlanSet("activeScene", JsonSerializer.SerializeToElement(sceneId), "scene.entered", new { sceneId });
    }

    private static CommandPlan HandleKnowledgeSetStatus(GameCommand command, RuntimeCommandContext context)
    {
        var knowledgeId = ContentIdFrom(command.Payload, "knowledgeId");
        var status = RequiredString(command.Payload, "status");
        if (status is not ("hypothesis" or "confirmed" or "contradicted"))
        {
            throw new ArgumentException($"Unknown knowledge status {status}.");
        }

        var knowledge = ObjectState(context.State, "knowledge");
        knowledge[knowledgeId] = new JsonObject { ["status"] = status };
        return PlanSet("knowledge", ToElement(knowledge), "knowledge.changed", new { knowledgeId, status });
    }

    private static CommandPlan HandleVocabularyLearn(GameCommand command, RuntimeCommandContext context)
    {
        var wordId = ContentIdFrom(command.Payload, "wordId");
        var sourceId = ContentIdFrom(command.Payload, "sourceId");
        var status = command.Payload.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : "confirmed";
        if (status is not ("guessed" or "confirmed"))
        {
            throw new ArgumentException($"Unknown vocabulary status {status}.");
        }

        var vocabulary = ObjectState(context.State, "vocabulary");
        // Observe commands can queue behind a confirmation. Resolve the ladder
        // against the committed kernel state, not the caller's earlier snapshot.
        if (vocabulary[wordId] is JsonObject current
            && current["status"] is JsonValue existing
            && existing.TryGetValue<string>(out var previous)
            && previous == "confirmed") status = "confirmed";
        vocabulary[wordId] = new JsonObject
        {
            ["status"] = status,
            ["sourceId"] = sourceId
        };
        return PlanSet("vocabulary", ToElement(vocabulary), "vocabulary.learned", new { wordId, sourceId, status });
    }

    private static CommandPlan HandleJournalRecord(GameCommand command, RuntimeCommandContext context)
    {
        var entryId = ContentIdFrom(command.Payload, "entryId");
        var sourceId = ContentIdFrom(command.Payload, "sourceId");
        var journal = ArrayState(context.State, "journal");
        if (!journal.OfType<JsonObject>().Any(item => item["entryId"]?.GetValue<string>() == entryId))
        {
            journal.Add(new JsonObject { ["entryId"] = entryId, ["sourceId"] = sourceId });
        }

        return PlanSet("journal", ToElement(journal), "journal.changed", new { entryId, sourceId });
    }

    private static CommandPlan HandleDialogueChoose(GameCommand command, RuntimeCommandContext context)
    {
        var dialogueId = ContentIdFrom(command.Payload, "dialogueId");
        var nodeId = RequiredString(command.Payload, "nodeId");
        var choiceId = RequiredString(command.Payload, "choiceId");
        var choices = ArrayState(context.State, "dialogueChoices");
        choices.Add(new JsonObject
        {
            ["dialogueId"] = dialogueId,
            ["nodeId"] = nodeId,
            ["choiceId"] = choiceId
        });
        return PlanSet("dialogueChoices", ToElement(choices), "dialogue.choice.committed", new { dialogueId, nodeId, choiceId });
    }

    private static CommandPlan HandleQuestSetStage(GameCommand command, RuntimeCommandContext context)
    {
        var questId = ContentIdFrom(command.Payload, "questId");
        var stageId = new SpawnPointId(RequiredString(command.Payload, "stageId")).Value;
        var status = RequiredString(command.Payload, "status");
        if (status is not ("inactive" or "active" or "completed" or "failed" or "cancelled"))
        {
            throw new ArgumentException($"Unknown quest status {status}.");
        }

        var quests = ObjectState(context.State, "quests");
        quests[questId] = new JsonObject { ["stageId"] = stageId, ["status"] = status };
        return PlanSet("quests", ToElement(quests), "quest.changed", new { questId, stageId, status });
    }

    private static CommandPlan HandleContentApply(GameCommand command, RuntimeCommandContext context)
    {
        // Check the evidence against the transaction state, including after load/restart.
        if (command.Payload.TryGetProperty("journalSources", out var sourceValues))
        {
            if (sourceValues.ValueKind != JsonValueKind.Array || sourceValues.GetArrayLength() != 2)
                throw new ArgumentException("A journal comparison requires two sources.");
            var sources = sourceValues.EnumerateArray().Select(value => new ContentId(value.GetString()!).Value).ToArray();
            var found = context.State.GetProperty("journal").EnumerateArray()
                .Select(entry => entry.GetProperty("sourceId").GetString()).ToHashSet(StringComparer.Ordinal);
            if (sources[0] == sources[1] || sources.Any(id => !found.Contains(id)))
                return new(Rejection: new("JournalSourcesMissing", "Find both sources before comparing them."));
        }

        var conditions = command.Payload.TryGetProperty("conditions", out var conditionValue)
            ? conditionValue
            : JsonSerializer.SerializeToElement(Array.Empty<object>());
        if (!ContentRuleEngine.EvaluateAll(conditions, context.State))
        {
            return new(Rejection: new("ContentConditionRejected", "Content conditions rejected this action."));
        }

        var effects = command.Payload.TryGetProperty("effects", out var effectValue)
            ? effectValue
            : JsonSerializer.SerializeToElement(Array.Empty<object>());
        var plan = ContentRuleEngine.PlanEffects(effects, context.State);
        var plannedEffects = plan.Effects.ToList();
        var plannedEvents = plan.Events.ToList();
        if (command.Payload.TryGetProperty("activeSceneId", out var activeSceneElement))
        {
            var activeSceneId = new ContentId(activeSceneElement.GetString()!).Value;
            plannedEffects.Add(new(StateEffectOperation.Set, "activeScene", JsonSerializer.SerializeToElement(activeSceneId)));
            plannedEvents.Add(new("scene.entered", JsonSerializer.SerializeToElement(new { sceneId = activeSceneId })));
        }

        if (command.Payload.TryGetProperty("dialogueChoice", out var dialogueChoice))
        {
            var dialogueId = ContentIdFrom(dialogueChoice, "dialogueId");
            var nodeId = RequiredString(dialogueChoice, "nodeId");
            var choiceId = RequiredString(dialogueChoice, "choiceId");
            var choices = ArrayState(context.State, "dialogueChoices");
            choices.Add(new JsonObject
            {
                ["dialogueId"] = dialogueId,
                ["nodeId"] = nodeId,
                ["choiceId"] = choiceId
            });
            plannedEffects.Add(new(StateEffectOperation.Set, "dialogueChoices", ToElement(choices)));
            plannedEvents.Add(new("dialogue.choice.committed", JsonSerializer.SerializeToElement(new { dialogueId, nodeId, choiceId })));
        }

        return new(plannedEffects, plannedEvents, Value: plan.ProjectedState);
    }

    private static CommandPlan HandlePhotoWorldCaptionPrepare(GameCommand command, RuntimeCommandContext context)
    {
        var photoId = RequiredString(command.Payload, "photoId");
        var captionText = RequiredString(command.Payload, "captionText");
        var preparedById = ContentIdFrom(command.Payload, "preparedById");
        var namespaceState = PhotoWorldState.PrepareCaption(context.State, photoId, captionText, preparedById);
        var caption = namespaceState.GetProperty("captions").GetProperty(photoId);
        var sourceAuthorId = caption.GetProperty("sourceAuthorId").GetString()!;
        return new(
            Effects: [new(StateEffectOperation.Set, "photoworlds", namespaceState)],
            Events: [new("photoworlds.caption.prepared", JsonSerializer.SerializeToElement(new { photoId, preparedById, sourceAuthorId }))],
            Value: namespaceState);
    }

    private static CommandPlan PlanSet(string key, JsonElement value, string eventType, object payload) => new(
        Effects: [new(StateEffectOperation.Set, key, value)],
        Events: [new(eventType, JsonSerializer.SerializeToElement(payload))]);

    private static string ContentIdFrom(JsonElement payload, string property)
    {
        var value = RequiredString(payload, property);
        return new ContentId(value).Value;
    }

    private static string RequiredString(JsonElement payload, string property) =>
        payload.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException($"Command payload {property} must be a non-empty string.");

    private static JsonObject ObjectState(JsonElement state, string property) =>
        JsonNode.Parse(state.GetProperty(property).GetRawText())!.AsObject();

    private static JsonArray ArrayState(JsonElement state, string property) =>
        JsonNode.Parse(state.GetProperty(property).GetRawText())!.AsArray();

    private static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);
}
