using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Narrative;

namespace Urman.Core.Quests;

public static class QuestStatuses
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class ObjectiveStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed record QuestCapabilityRequest(
    string CapabilityInstanceId,
    string ProtocolId,
    string ExactVersion,
    string ConfigRef,
    JsonElement Config,
    string OutcomeSchemaRef);

public sealed record QuestCapabilityCleanup(
    IReadOnlyList<string> CapabilityInstanceIds,
    string Reason,
    IReadOnlyList<string> ReleaseClaimOwnerIds,
    IReadOnlyList<string> ReleaseClaimLifecycleScopes);

public sealed record QuestCapabilityCleanupExtension(
    IReadOnlyList<string>? ReleaseClaimOwnerIds = null,
    IReadOnlyList<string>? ReleaseClaimLifecycleScopes = null);

public sealed record QuestRun(
    JsonElement Instance,
    IReadOnlyList<QuestCapabilityRequest> CapabilityRequests);

public sealed record QuestReduction(
    JsonElement NextState,
    IReadOnlyList<QuestCapabilityRequest> CapabilityRequests,
    IReadOnlyList<QuestCapabilityCleanup> CapabilityCleanups,
    CommandPlan Plan);

public static class QuestLifecycleReducer
{
    public static string ObjectiveOccurrenceId(string questInstanceId, string stageId, string objectiveId, string triggerId) =>
        $"quest-trigger:{Required(questInstanceId, "Quest instance ID")}:{Required(stageId, "Quest stage ID")}:{Required(objectiveId, "Quest objective ID")}:{Required(triggerId, "Quest trigger ID")}";

    public static QuestRun CreateRun(
        JsonElement definition,
        string questInstanceId,
        JsonElement generatedBindings = default,
        Func<JsonElement, JsonElement, bool>? evaluateCondition = null,
        JsonElement evaluationContext = default,
        Func<string, JsonElement>? configResolver = null)
    {
        var definitionNode = Definition(definition);
        var state = new JsonObject
        {
            ["questId"] = RequiredString(definitionNode, "id"),
            ["questInstanceId"] = Required(questInstanceId, "Quest instance ID"),
            ["status"] = QuestStatuses.Active,
            ["attempt"] = 1,
            ["stageIndex"] = 0,
            ["objectives"] = new JsonObject(),
            ["generatedBindings"] = generatedBindings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? new JsonObject()
                : Parse(generatedBindings),
            ["capabilitySnapshots"] = new JsonObject(),
            ["activeCapabilities"] = new JsonObject(),
            ["checkpoint"] = null,
            ["originCheckpoint"] = null
        };
        var requests = new List<QuestCapabilityRequest>();
        ActivateStage(
            definitionNode,
            state,
            evaluateCondition ?? ((_, _) => true),
            ContextOrEmpty(evaluationContext),
            requests,
            configResolver ?? (_ => JsonSerializer.SerializeToElement(new { })));
        var origin = SnapshotCheckpoint(state);
        state["originCheckpoint"] = origin.DeepClone();
        state["checkpoint"] = origin.DeepClone();
        return new(ToElement(state), requests);
    }

    public static QuestReduction Reduce(
        JsonElement definition,
        JsonElement instance,
        JsonElement command,
        string stateKey,
        Func<JsonElement, JsonElement, bool>? evaluateCondition = null,
        Func<JsonElement, JsonElement, CommandPlan>? planEffects = null,
        Func<string, JsonElement>? configResolver = null,
        JsonElement evaluationContext = default,
        IReadOnlyList<string>? releaseClaimOwnerIds = null,
        IReadOnlyList<string>? releaseClaimLifecycleScopes = null,
        Func<IReadOnlyList<string>, string, QuestCapabilityCleanupExtension?>? prepareCapabilityCleanup = null)
    {
        var definitionNode = Definition(definition);
        RequireObject(instance, "Quest instance");
        RequireObject(command, "Quest lifecycle command");
        stateKey = Required(stateKey, "Quest state key");
        var original = Parse(instance).AsObject();
        var state = (JsonObject)original.DeepClone();
        if (RequiredString(state, "questId") != RequiredString(definitionNode, "id"))
        {
            throw new ArgumentException("Quest instance definition ID mismatch.");
        }

        var commandType = RequiredString(command, "type");
        var predicate = evaluateCondition ?? ContentRuleEngine.Evaluate;
        var effectPlanner = planEffects ?? DefaultEffectPlanner;
        var context = ContextOrEmpty(evaluationContext);
        var effects = new List<StateEffect>();
        var events = new List<EventDraft>();
        var claims = new List<ResourceClaim>();
        var capabilityRequests = new List<QuestCapabilityRequest>();
        var capabilityCleanups = new List<QuestCapabilityCleanup>();
        var ownerReleases = new List<string>(releaseClaimOwnerIds ?? []);
        var scopeReleases = new List<string>(releaseClaimLifecycleScopes ?? []);
        var effectContext = context;

        QuestReduction Reject(string code, string message, object? details = null) => new(
            ToElement(original),
            [],
            [],
            new(Rejection: new(code, message, details is null ? null : JsonSerializer.SerializeToElement(details))));

        void AppendPlan(JsonArray authoredEffects)
        {
            var plan = effectPlanner(ToElement(authoredEffects), effectContext);
            if (plan.Rejection is not null)
            {
                throw new ArgumentException($"Quest effect planner rejected authored effects: {plan.Rejection.Code}.");
            }

            effects.AddRange(plan.Effects ?? []);
            events.AddRange(plan.Events ?? []);
            claims.AddRange(plan.Claims ?? []);
            if (plan.Value.ValueKind == JsonValueKind.Object)
            {
                effectContext = plan.Value.Clone();
            }
        }

        void AppendCleanup(string reason, IReadOnlyList<string>? instanceIds = null)
        {
            var active = ActiveCapabilities(state);
            var ids = (instanceIds ?? active.Select(entry => entry.Key).ToArray())
                .Where(active.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (ids.Length == 0)
            {
                return;
            }

            var extension = prepareCapabilityCleanup?.Invoke(ids, reason);
            foreach (var id in ids)
            {
                active.Remove(id);
            }

            var cleanupOwners = ids
                .Concat(extension?.ReleaseClaimOwnerIds ?? [])
                .Select(value => Required(value, "Capability cleanup owner ID"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var cleanupScopes = (extension?.ReleaseClaimLifecycleScopes ?? [])
                .Select(value => Required(value, "Capability cleanup lifecycle scope"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            capabilityCleanups.Add(new(ids, reason, cleanupOwners, cleanupScopes));
            ownerReleases.AddRange(cleanupOwners);
            scopeReleases.AddRange(cleanupScopes);
        }

        var status = RequiredString(state, "status");
        if (status != QuestStatuses.Active && commandType != "quest.retry")
        {
            return Reject("QuestNotActive", $"Quest {RequiredString(state, "questInstanceId")} is not active.", new { status });
        }

        switch (commandType)
        {
            case "quest.refresh":
                ActivateStage(definitionNode, state, predicate, context, capabilityRequests, configResolver ?? EmptyConfig);
                events.Add(LifecycleEvent(state, "refreshed"));
                break;

            case "objective.complete":
            case "objective.fail":
            {
                var stage = StageAt(definitionNode, RequiredInt(state, "stageIndex"));
                var objectives = ObjectiveMap(stage);
                var objectiveId = RequiredString(command, "objectiveId");
                if (!objectives.TryGetValue(objectiveId, out var objective))
                {
                    return Reject("UnknownQuestObjective", $"Quest objective {objectiveId} is not active in this stage.");
                }

                var stageStatuses = RequiredObject(RequiredObject(state, "objectives"), RequiredString(stage, "id"));
                if (stageStatuses[objectiveId] is not JsonObject record || RequiredString(record, "status") != ObjectiveStatuses.Active)
                {
                    return Reject("QuestObjectiveNotActive", $"Quest objective {objectiveId} is not active.");
                }

                if (commandType == "objective.complete" && !ConditionsPass(RequiredArray(objective, "completionConditions"), predicate, context))
                {
                    return Reject("QuestCompletionConditionsFailed", $"Quest objective {objectiveId} completion conditions failed.");
                }

                var completed = commandType == "objective.complete";
                record["status"] = completed ? ObjectiveStatuses.Completed : ObjectiveStatuses.Failed;
                if (objective["capability"] is JsonObject)
                {
                    AppendCleanup(
                        completed ? "objective-complete" : "objective-fail",
                        [CapabilityInstanceId(RequiredString(state, "questInstanceId"), RequiredString(stage, "id"), objectiveId)]);
                }

                AppendPlan(RequiredArray(objective, completed ? "completionEffects" : "failureEffects"));
                if (completed && RequiredBool(objective, "optional"))
                {
                    AppendPlan(RequiredArray(RequiredObject(definitionNode, "outcomes"), "optional"));
                }

                events.Add(LifecycleEvent(state, completed ? "objective-completed" : "objective-failed", new JsonObject { ["objectiveId"] = objectiveId }));
                if (completed && RequiredString(RequiredObject(definitionNode, "checkpointPolicy"), "mode") == "objective")
                {
                    state["checkpoint"] = SnapshotCheckpoint(state);
                }

                if (!completed && !RequiredBool(objective, "optional"))
                {
                    state["status"] = QuestStatuses.Failed;
                    AppendCleanup("quest-failed");
                    AppendPlan(RequiredArray(RequiredObject(definitionNode, "outcomes"), "failure"));
                    events.Add(LifecycleEvent(state, "failed", new JsonObject { ["objectiveId"] = objectiveId }));
                }
                else if (IsStageComplete(stage, stageStatuses))
                {
                    var stageIndex = RequiredInt(state, "stageIndex");
                    if (stageIndex == RequiredArray(definitionNode, "stages").Count - 1)
                    {
                        state["status"] = QuestStatuses.Completed;
                        AppendCleanup("quest-completed");
                        AppendPlan(RequiredArray(RequiredObject(definitionNode, "outcomes"), "success"));
                        events.Add(LifecycleEvent(state, "completed"));
                    }
                    else
                    {
                        var outgoing = objectives
                            .Where(entry => entry.Value["capability"] is JsonObject)
                            .Select(entry => CapabilityInstanceId(RequiredString(state, "questInstanceId"), RequiredString(stage, "id"), entry.Key))
                            .ToArray();
                        AppendCleanup("stage-advance", outgoing);
                        state["stageIndex"] = stageIndex + 1;
                        ActivateStage(definitionNode, state, predicate, context, capabilityRequests, configResolver ?? EmptyConfig);
                        if (RequiredString(RequiredObject(definitionNode, "checkpointPolicy"), "mode") != "none")
                        {
                            state["checkpoint"] = SnapshotCheckpoint(state);
                        }

                        events.Add(LifecycleEvent(state, "stage-advanced"));
                    }
                }

                break;
            }

            case "quest.cancel":
            {
                var policy = RequiredObject(definitionNode, "cancelPolicy");
                if (!RequiredBool(policy, "allowed"))
                {
                    return Reject("QuestCancelForbidden", $"Quest {RequiredString(state, "questInstanceId")} cannot be cancelled.");
                }

                state["status"] = QuestStatuses.Cancelled;
                AppendCleanup("quest-cancel");
                AppendPlan(RequiredArray(policy, "effects"));
                events.Add(LifecycleEvent(state, "cancelled"));
                break;
            }

            case "quest.retry":
            {
                if (status != QuestStatuses.Failed)
                {
                    return Reject("QuestRetryUnavailable", $"Quest {RequiredString(state, "questInstanceId")} is not failed.");
                }

                var policy = RequiredObject(definitionNode, "retryPolicy");
                var retryMode = RequiredString(policy, "mode");
                var attempt = RequiredInt(state, "attempt");
                if (retryMode == "none" || attempt >= RequiredInt(policy, "maximumAttempts"))
                {
                    return Reject("QuestRetryExhausted", $"Quest {RequiredString(state, "questInstanceId")} has no remaining retries.");
                }

                AppendCleanup("quest-retry");
                RestoreCheckpoint(state, retryMode == "quest" ? state["originCheckpoint"] : state["checkpoint"]);
                state["activeCapabilities"] = new JsonObject();
                state["attempt"] = attempt + 1;
                ActivateStage(definitionNode, state, predicate, context, capabilityRequests, configResolver ?? EmptyConfig);
                events.Add(LifecycleEvent(state, "retried"));
                break;
            }

            default:
                return Reject("UnknownQuestLifecycleCommand", $"Unknown quest lifecycle command {commandType}.");
        }

        effects.Add(new(StateEffectOperation.Set, stateKey, ToElement(state)));
        var value = JsonSerializer.SerializeToElement(new
        {
            questInstanceId = RequiredString(state, "questInstanceId"),
            status = RequiredString(state, "status")
        });
        return new(
            ToElement(state),
            capabilityRequests,
            capabilityCleanups,
            new(
                effects,
                events,
                claims,
                ownerReleases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                scopeReleases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                Value: value));
    }

    private static CommandPlan DefaultEffectPlanner(JsonElement effects, JsonElement state)
    {
        var plan = ContentRuleEngine.PlanEffects(effects, state);
        return new(plan.Effects, plan.Events, Value: plan.ProjectedState);
    }

    private static JsonElement EmptyConfig(string _) => JsonSerializer.SerializeToElement(new { });

    private static void ActivateStage(
        JsonObject definition,
        JsonObject state,
        Func<JsonElement, JsonElement, bool> evaluateCondition,
        JsonElement evaluationContext,
        ICollection<QuestCapabilityRequest> capabilityRequests,
        Func<string, JsonElement> configResolver)
    {
        var stage = StageAt(definition, RequiredInt(state, "stageIndex"));
        var objectives = ObjectiveMap(stage);
        var allStatuses = RequiredObject(state, "objectives");
        var stageId = RequiredString(stage, "id");
        var statuses = allStatuses[stageId] as JsonObject ?? new JsonObject();
        foreach (var (objectiveId, objective) in objectives)
        {
            if (statuses[objectiveId] is not JsonObject record || RequiredString(record, "status") == ObjectiveStatuses.Pending)
            {
                var active = ConditionsPass(RequiredArray(objective, "startConditions"), evaluateCondition, evaluationContext);
                record = new JsonObject { ["status"] = active ? ObjectiveStatuses.Active : ObjectiveStatuses.Pending };
                statuses[objectiveId] = record;
            }

            if (RequiredString(record, "status") == ObjectiveStatuses.Active && objective["capability"] is JsonObject capability)
            {
                RequestCapability(state, stageId, objectiveId, capability, capabilityRequests, configResolver);
            }
        }

        allStatuses[stageId] = statuses;
    }

    private static void RequestCapability(
        JsonObject state,
        string stageId,
        string objectiveId,
        JsonObject capability,
        ICollection<QuestCapabilityRequest> requests,
        Func<string, JsonElement> configResolver)
    {
        var instanceId = CapabilityInstanceId(RequiredString(state, "questInstanceId"), stageId, objectiveId);
        var active = ActiveCapabilities(state);
        if (active.ContainsKey(instanceId))
        {
            return;
        }

        var configRef = RequiredString(capability, "configRef");
        var config = configResolver(configRef);
        if (config.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException($"Quest capability config {configRef} is undefined.");
        }

        var request = new QuestCapabilityRequest(
            instanceId,
            RequiredString(capability, "protocolId"),
            RequiredString(capability, "exactVersion"),
            configRef,
            config.Clone(),
            RequiredString(capability, "outcomeSchemaRef"));
        active[instanceId] = new JsonObject
        {
            ["capabilityInstanceId"] = request.CapabilityInstanceId,
            ["protocolId"] = request.ProtocolId,
            ["exactVersion"] = request.ExactVersion,
            ["configRef"] = request.ConfigRef,
            ["config"] = Parse(request.Config),
            ["outcomeSchemaRef"] = request.OutcomeSchemaRef
        };
        requests.Add(request);
    }

    private static bool IsStageComplete(JsonObject stage, JsonObject statuses)
    {
        var composition = RequiredObject(stage, "composition");
        var configured = RequiredArray(composition, "objectiveIds").Select(RequiredStringNode).ToArray();
        var completed = configured.Count(id => statuses[id] is JsonObject record && RequiredString(record, "status") == ObjectiveStatuses.Completed);
        return RequiredString(composition, "mode") switch
        {
            "all" => configured
                .Where(id => !RequiredBool(ObjectiveMap(stage)[id], "optional"))
                .All(id => statuses[id] is JsonObject record && RequiredString(record, "status") == ObjectiveStatuses.Completed),
            "any" => completed > 0,
            "threshold" => completed >= RequiredInt(composition, "threshold"),
            var mode => throw new ArgumentException($"Quest stage composition {mode} is invalid.")
        };
    }

    private static bool ConditionsPass(
        JsonArray conditions,
        Func<JsonElement, JsonElement, bool> evaluateCondition,
        JsonElement evaluationContext) =>
        conditions.All(condition => condition is not null && evaluateCondition(ToElement(condition), evaluationContext));

    private static JsonObject SnapshotCheckpoint(JsonObject state) => new()
    {
        ["stageIndex"] = state["stageIndex"]!.DeepClone(),
        ["objectives"] = state["objectives"]!.DeepClone(),
        ["capabilitySnapshots"] = state["capabilitySnapshots"]!.DeepClone(),
        ["generatedBindings"] = state["generatedBindings"]!.DeepClone()
    };

    private static void RestoreCheckpoint(JsonObject state, JsonNode? checkpoint)
    {
        if (checkpoint is not JsonObject value)
        {
            throw new ArgumentException("Quest retry requires a checkpoint.");
        }

        state["stageIndex"] = value["stageIndex"]!.DeepClone();
        state["objectives"] = value["objectives"]!.DeepClone();
        state["capabilitySnapshots"] = value["capabilitySnapshots"]!.DeepClone();
        state["generatedBindings"] = value["generatedBindings"]!.DeepClone();
        state["status"] = QuestStatuses.Active;
    }

    private static EventDraft LifecycleEvent(JsonObject state, string name, JsonObject? additional = null)
    {
        var payload = new JsonObject
        {
            ["questId"] = RequiredString(state, "questId"),
            ["questInstanceId"] = RequiredString(state, "questInstanceId"),
            ["name"] = name,
            ["stageIndex"] = RequiredInt(state, "stageIndex")
        };
        if (additional is not null)
        {
            foreach (var (key, value) in additional)
            {
                payload[key] = value?.DeepClone();
            }
        }

        return new("quest.lifecycle", ToElement(payload));
    }

    private static JsonObject Definition(JsonElement definition)
    {
        RequireObject(definition, "Quest definition");
        var node = Parse(definition).AsObject();
        _ = RequiredString(node, "id");
        var stages = RequiredArray(node, "stages");
        if (stages.Count == 0)
        {
            throw new ArgumentException("Quest definition needs stages.");
        }

        foreach (var stageNode in stages)
        {
            if (stageNode is not JsonObject stage)
            {
                throw new ArgumentException("Quest stage must be an object.");
            }

            _ = ObjectiveMap(stage);
        }

        _ = RequiredObject(node, "outcomes");
        _ = RequiredObject(node, "retryPolicy");
        _ = RequiredObject(node, "checkpointPolicy");
        _ = RequiredObject(node, "cancelPolicy");
        return node;
    }

    private static JsonObject StageAt(JsonObject definition, int stageIndex)
    {
        var stages = RequiredArray(definition, "stages");
        if (stageIndex < 0 || stageIndex >= stages.Count || stages[stageIndex] is not JsonObject stage)
        {
            throw new ArgumentException("Quest instance has an invalid active stage.");
        }

        return stage;
    }

    private static Dictionary<string, JsonObject> ObjectiveMap(JsonObject stage)
    {
        _ = RequiredString(stage, "id");
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var objectiveNode in RequiredArray(stage, "objectives"))
        {
            if (objectiveNode is not JsonObject objective || !result.TryAdd(RequiredString(objective, "id"), objective))
            {
                throw new ArgumentException("Quest stage has invalid or duplicate objective IDs.");
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("Quest stage needs objectives.");
        }

        var composition = RequiredObject(stage, "composition");
        var ids = RequiredArray(composition, "objectiveIds").Select(RequiredStringNode).ToArray();
        if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !result.ContainsKey(id)))
        {
            throw new ArgumentException("Quest stage composition references an invalid objective.");
        }

        var mode = RequiredString(composition, "mode");
        if (mode is not ("all" or "any" or "threshold"))
        {
            throw new ArgumentException("Quest stage composition is invalid.");
        }

        if (mode == "threshold")
        {
            var threshold = RequiredInt(composition, "threshold");
            if (threshold < 1 || threshold > ids.Length)
            {
                throw new ArgumentException("Quest stage threshold is invalid.");
            }
        }

        return result;
    }

    private static string CapabilityInstanceId(string questInstanceId, string stageId, string objectiveId) =>
        $"capability:{questInstanceId}:{stageId}:{objectiveId}";

    private static JsonObject ActiveCapabilities(JsonObject state)
    {
        if (state["activeCapabilities"] is not JsonObject active)
        {
            active = new JsonObject();
            state["activeCapabilities"] = active;
        }

        return active;
    }

    private static JsonElement ContextOrEmpty(JsonElement context) =>
        context.ValueKind == JsonValueKind.Object ? context.Clone() : JsonSerializer.SerializeToElement(new { });

    private static JsonNode Parse(JsonElement value) =>
        JsonNode.Parse(value.GetRawText()) ?? throw new ArgumentException("JSON value cannot be null.");

    private static JsonObject RequiredObject(JsonObject owner, string property) =>
        owner[property] as JsonObject ?? throw new ArgumentException($"{property} must be an object.");

    private static JsonArray RequiredArray(JsonObject owner, string property) =>
        owner[property] as JsonArray ?? throw new ArgumentException($"{property} must be an array.");

    private static string RequiredString(JsonObject owner, string property) =>
        owner[property] is JsonValue value && value.TryGetValue<string>(out var result)
            ? Required(result, property)
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string RequiredString(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? Required(value.GetString(), property)
            : throw new ArgumentException($"{property} must be a non-empty string.");

    private static string RequiredStringNode(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result)
            ? Required(result, "Quest objective ID")
            : throw new ArgumentException("Quest objective ID must be a non-empty string.");

    private static int RequiredInt(JsonObject owner, string property) =>
        owner[property] is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : throw new ArgumentException($"{property} must be an integer.");

    private static bool RequiredBool(JsonObject owner, string property) =>
        owner[property] is JsonValue value && value.TryGetValue<bool>(out var result)
            ? result
            : throw new ArgumentException($"{property} must be a boolean.");

    private static string Required(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"{label} must be a non-empty string.");

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"{label} must be an object.");
        }
    }

    private static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);
}
