using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Quests;
using Urman.Core.Runtime;

namespace Urman.Godot;

public sealed class QuestRuntimeCoordinator
{
    private readonly CompiledCampaignRepository _content;

    public QuestRuntimeCoordinator(CompiledCampaignRepository content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public GameCommand Command(string occurrenceId, string questId, string lifecycleType, string? objectiveId = null)
    {
        var payload = new JsonObject
        {
            ["questId"] = questId,
            ["type"] = lifecycleType
        };
        if (objectiveId is not null)
        {
            payload["objectiveId"] = objectiveId;
        }

        return new(occurrenceId, "quest.lifecycle", JsonSerializer.SerializeToElement(payload));
    }

    public CommandPlan Handle(GameCommand command, RuntimeCommandContext context)
    {
        var questId = command.Payload.GetProperty("questId").GetString()
            ?? throw new ArgumentException("Quest lifecycle command requires questId.");
        var quests = JsonNode.Parse(context.State.GetProperty("quests").GetRawText())!.AsObject();
        if (quests[questId] is not JsonObject instance)
        {
            return new(Rejection: new("UnknownQuest", $"Unknown quest {questId}."));
        }

        var reduction = QuestLifecycleReducer.Reduce(
            _content.RequireQuest(questId).Definition,
            JsonSerializer.SerializeToElement(instance),
            command.Payload,
            "__quest-instance",
            ContentRuleEngine.Evaluate,
            (effects, state) =>
            {
                var planned = ContentRuleEngine.PlanEffects(effects, state);
                return new(planned.Effects, planned.Events, Value: planned.ProjectedState);
            },
            evaluationContext: context.State);
        if (reduction.Plan.Rejection is not null)
        {
            return reduction.Plan;
        }

        quests[questId] = JsonNode.Parse(reduction.NextState.GetRawText());
        var effects = (reduction.Plan.Effects ?? [])
            .Where(effect => effect.Key != "__quest-instance")
            .Append(new(StateEffectOperation.Set, "quests", JsonSerializer.SerializeToElement(quests)))
            .ToArray();
        var events = (reduction.Plan.Events ?? []).ToList();
        events.AddRange(reduction.CapabilityRequests.Select(request => new EventDraft(
            "quest.capability.requested",
            JsonSerializer.SerializeToElement(new
            {
                capabilityInstanceId = request.CapabilityInstanceId,
                protocolId = request.ProtocolId,
                exactVersion = request.ExactVersion,
                configRef = request.ConfigRef,
                config = request.Config,
                outcomeSchemaRef = request.OutcomeSchemaRef
            }))));
        events.AddRange(reduction.CapabilityCleanups.Select(cleanup => new EventDraft(
            "quest.capability.cleanup-requested",
            JsonSerializer.SerializeToElement(new
            {
                capabilityInstanceIds = cleanup.CapabilityInstanceIds,
                reason = cleanup.Reason,
                releaseClaimOwnerIds = cleanup.ReleaseClaimOwnerIds,
                releaseClaimLifecycleScopes = cleanup.ReleaseClaimLifecycleScopes
            }))));
        return reduction.Plan with { Effects = effects, Events = events };
    }

    public async Task ReconcileAsync(RuntimeKernel kernel, Func<string, string> nextOccurrenceId)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(nextOccurrenceId);
        foreach (var quest in _content.Quests)
        {
            var maximumPasses = quest.Definition.GetProperty("stages")
                .EnumerateArray()
                .Sum(stage => stage.GetProperty("objectives").GetArrayLength()) + 1;
            for (var pass = 0; pass < maximumPasses; pass++)
            {
                var refreshed = await kernel.DispatchAsync(Command(
                    nextOccurrenceId($"quest-refresh:{quest.Id}"),
                    quest.Id,
                    "quest.refresh"));
                if (refreshed.Status != CommandStatus.Committed)
                {
                    break;
                }

                var state = kernel.SelectState();
                var instance = state.GetProperty("quests").GetProperty(quest.Id);
                if (instance.GetProperty("status").GetString() != QuestStatuses.Active)
                {
                    break;
                }

                var stageIndex = instance.GetProperty("stageIndex").GetInt32();
                var stage = quest.Definition.GetProperty("stages")[stageIndex];
                var stageId = stage.GetProperty("id").GetString()!;
                var statuses = instance.GetProperty("objectives").GetProperty(stageId);
                JsonElement? completable = null;
                foreach (var objective in stage.GetProperty("objectives").EnumerateArray())
                {
                    var objectiveId = objective.GetProperty("id").GetString()!;
                    var completionConditions = objective.GetProperty("completionConditions");
                    if (completionConditions.GetArrayLength() > 0 &&
                        statuses.TryGetProperty(objectiveId, out var objectiveState) &&
                        objectiveState.GetProperty("status").GetString() == ObjectiveStatuses.Active &&
                        ContentRuleEngine.EvaluateAll(completionConditions, state))
                    {
                        completable = objective.Clone();
                        break;
                    }
                }

                if (completable is null)
                {
                    break;
                }

                var objectiveToComplete = completable.Value.GetProperty("id").GetString()!;
                var attempt = instance.GetProperty("attempt").GetInt32();
                var completed = await kernel.DispatchAsync(Command(
                    QuestLifecycleReducer.ObjectiveOccurrenceId(
                        $"instance:{quest.Id}",
                        stageId,
                        objectiveToComplete,
                        $"completed-attempt-{attempt}"),
                    quest.Id,
                    "objective.complete",
                    objectiveToComplete));
                if (completed.Status != CommandStatus.Committed)
                {
                    break;
                }
            }
        }
    }
}
