using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Quests;

/// <summary>A refused edit with a message the author can act on.</summary>
public sealed class QuestEditException(string message) : InvalidOperationException(message);

/// <summary>
/// Structural edits of a quest's flow. Every operation takes a definition and
/// returns a changed deep copy, so the caller's undo history can keep both.
/// The list view and the graph view call the same operations; neither owns a
/// separate order or nesting that could drift from the executed transitions.
/// </summary>
public static class QuestFlowEditor
{
    /// <summary>Put a new single-objective stage on an existing edge (between a stage and where it led).</summary>
    public static JsonObject InsertStageOnEdge(
        JsonObject definition,
        string fromStageId,
        string? transitionId,
        string newStageId,
        string objectiveId,
        string titleTextId)
    {
        var result = (JsonObject)definition.DeepClone();
        RequireFreshStageId(result, newStageId);
        MaterializeImplicitEdge(result, fromStageId);
        var from = StageNode(result, fromStageId);
        var transition = transitionId is null
            ? Transitions(from).OfType<JsonObject>().FirstOrDefault(item => (string)item["on"]!["kind"]! == "stage-complete")
            : Transitions(from).OfType<JsonObject>().FirstOrDefault(item => (string)item["id"]! == transitionId);
        if (transition is null)
        {
            throw new QuestEditException($"У этапа «{fromStageId}» нет перехода «{transitionId ?? "дальше"}».");
        }

        var oldTarget = transition["target"]!.DeepClone();
        transition["target"] = new JsonObject { ["stageId"] = newStageId };
        var stage = NewStage(newStageId, objectiveId, titleTextId);
        stage["transitions"] = new JsonArray(new JsonObject
        {
            ["id"] = "next",
            ["on"] = new JsonObject { ["kind"] = "stage-complete" },
            ["conditions"] = new JsonArray(),
            ["effects"] = new JsonArray(),
            ["target"] = oldTarget
        });
        var stages = Stages(result);
        stages.Insert(IndexOf(stages, fromStageId) + 1, stage);
        return result;
    }

    /// <summary>
    /// Add a choice to a stage: a new objective whose completion leaves the
    /// stage through its own transition. The stage then completes on any one
    /// of its choices.
    /// </summary>
    public static JsonObject AddBranchArm(
        JsonObject definition,
        string stageId,
        string objectiveId,
        string titleTextId,
        string transitionId,
        FlowTarget target,
        JsonArray? effects = null)
    {
        var result = (JsonObject)definition.DeepClone();
        MaterializeImplicitEdge(result, stageId);
        var stage = StageNode(result, stageId);
        var objectives = stage["objectives"]!.AsArray();
        if (objectives.OfType<JsonObject>().Any(item => (string)item["id"]! == objectiveId))
        {
            throw new QuestEditException($"В этапе уже есть цель «{objectiveId}».");
        }

        if (target.StageId is { } targetStage && !Stages(result).OfType<JsonObject>().Any(item => (string)item["id"]! == targetStage))
        {
            throw new QuestEditException($"Этапа «{targetStage}» нет в квесте.");
        }

        objectives.Add(NewObjective(objectiveId, titleTextId));
        var composition = stage["composition"]!.AsObject();
        composition["mode"] = "any";
        composition.Remove("threshold");
        composition["objectiveIds"]!.AsArray().Add(objectiveId);
        // The existing way out of the stage becomes the first choice's own edge,
        // so completing the new choice cannot also fall through to it.
        var transitions = Transitions(stage);
        var stageComplete = transitions.OfType<JsonObject>().FirstOrDefault(item => (string)item["on"]!["kind"]! == "stage-complete");
        if (stageComplete is not null)
        {
            var firstObjective = (string)objectives[0]!["id"]!;
            stageComplete["on"] = new JsonObject { ["kind"] = "objective-complete", ["objectiveId"] = firstObjective };
        }

        transitions.Add(new JsonObject
        {
            ["id"] = transitionId,
            ["on"] = new JsonObject { ["kind"] = "objective-complete", ["objectiveId"] = objectiveId },
            ["conditions"] = new JsonArray(),
            ["effects"] = effects?.DeepClone() ?? new JsonArray(),
            ["target"] = TargetNode(target)
        });
        return result;
    }

    /// <summary>Remove a stage with a single way out; everything that led to it now leads there.</summary>
    public static JsonObject RemoveStage(JsonObject definition, string stageId)
    {
        var flow = new QuestFlow(definition);
        if (flow.StartStageId == stageId && flow.StageOrder.Count == 1)
        {
            throw new QuestEditException("Нельзя удалить единственный этап квеста.");
        }

        var exits = flow.Outgoing(stageId);
        if (exits.Select(edge => edge.Target.ToString()).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            throw new QuestEditException("У этапа несколько выходов. Сначала оставьте одну ветку.");
        }

        var result = (JsonObject)definition.DeepClone();
        var stages = Stages(result);
        var index = IndexOf(stages, stageId);
        MaterializeImplicitEdge(result, stageId);
        if (index > 0)
        {
            MaterializeImplicitEdge(result, (string)stages[index - 1]!["id"]!);
        }

        var replacement = StageNode(result, stageId)["transitions"]!.AsArray()
            .OfType<JsonObject>().First()["target"]!;
        foreach (var stage in stages.OfType<JsonObject>())
        {
            foreach (var transition in Transitions(stage).OfType<JsonObject>())
            {
                if (transition["target"]!["stageId"] is JsonNode target && (string)target! == stageId)
                {
                    transition["target"] = replacement.DeepClone();
                }
            }
        }

        stages.RemoveAt(index);
        return result;
    }

    /// <summary>
    /// Replace the implicit "next stage in the list" edge of a stage with an
    /// explicit transition to the same place, so reordering or inserting
    /// stages cannot silently change where it leads.
    /// </summary>
    public static void MaterializeImplicitEdge(JsonObject definition, string stageId)
    {
        var implicitEdge = new QuestFlow(definition).Outgoing(stageId).FirstOrDefault(edge => edge.TransitionId is null);
        if (implicitEdge is null)
        {
            return;
        }

        var stage = StageNode(definition, stageId);
        var transitions = Transitions(stage);
        var id = "next";
        for (var suffix = 2; transitions.OfType<JsonObject>().Any(item => (string)item["id"]! == id); suffix++)
        {
            id = $"next-{suffix}";
        }

        transitions.Add(new JsonObject
        {
            ["id"] = id,
            ["on"] = new JsonObject { ["kind"] = "stage-complete" },
            ["conditions"] = new JsonArray(),
            ["effects"] = new JsonArray(),
            ["target"] = TargetNode(implicitEdge.Target)
        });
    }

    private static JsonObject NewStage(string stageId, string objectiveId, string titleTextId) => new()
    {
        ["id"] = stageId,
        ["objectives"] = new JsonArray(NewObjective(objectiveId, titleTextId)),
        ["composition"] = new JsonObject { ["mode"] = "all", ["objectiveIds"] = new JsonArray(objectiveId) }
    };

    private static JsonObject NewObjective(string objectiveId, string titleTextId) => new()
    {
        ["id"] = objectiveId,
        ["titleTextId"] = titleTextId,
        ["optional"] = false,
        ["startConditions"] = new JsonArray(),
        ["completionConditions"] = new JsonArray(),
        ["completionEffects"] = new JsonArray(),
        ["failureEffects"] = new JsonArray()
    };

    private static JsonObject TargetNode(FlowTarget target)
    {
        if (target.StageId is not null)
        {
            return new JsonObject { ["stageId"] = target.StageId };
        }

        var node = new JsonObject { ["end"] = target.End };
        if (target.OutcomeId is not null)
        {
            node["outcomeId"] = target.OutcomeId;
        }

        return node;
    }

    private static void RequireFreshStageId(JsonObject definition, string stageId)
    {
        if (Stages(definition).OfType<JsonObject>().Any(item => (string)item["id"]! == stageId))
        {
            throw new QuestEditException($"Этап «{stageId}» уже существует.");
        }
    }

    private static JsonArray Stages(JsonObject definition) => definition["stages"]!.AsArray();

    private static int IndexOf(JsonArray stages, string stageId)
    {
        for (var index = 0; index < stages.Count; index++)
        {
            if ((string)stages[index]!["id"]! == stageId)
            {
                return index;
            }
        }

        throw new QuestEditException($"Этапа «{stageId}» нет в квесте.");
    }

    private static JsonObject StageNode(JsonObject definition, string stageId) =>
        Stages(definition)[IndexOf(Stages(definition), stageId)]!.AsObject();

    private static JsonArray Transitions(JsonObject stage)
    {
        if (stage["transitions"] is JsonArray transitions)
        {
            return transitions;
        }

        transitions = [];
        stage["transitions"] = transitions;
        return transitions;
    }
}
