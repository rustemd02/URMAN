using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Quests;

/// <summary>Where an edge of the quest flow leads: another stage or the end of the quest.</summary>
public sealed record FlowTarget(string? StageId, string? End, string? OutcomeId)
{
    public static FlowTarget Stage(string stageId) => new(stageId, null, null);
    public static FlowTarget Ending(string end, string? outcomeId = null) => new(null, end, outcomeId);
    public bool IsEnd => End is not null;
    public override string ToString() => IsEnd ? $"end:{End}{(OutcomeId is null ? "" : "/" + OutcomeId)}" : StageId!;
}

/// <summary>
/// One edge of the flow. <see cref="TransitionId"/> is null for the implicit
/// "next stage in the list" edge that the runtime follows when a stage has no
/// stage-complete transition.
/// </summary>
public sealed record FlowEdge(string FromStageId, string? TransitionId, string Trigger, string? ObjectiveId, FlowTarget Target);

/// <summary>
/// Read-only graph view of a quest definition exactly as
/// <c>Urman.Core.Quests.QuestLifecycleReducer</c> executes it: authored
/// transitions plus the implicit sequential edge. Studio's list and graph
/// editors are both projections of this one structure (spec QUEST06, DATA07).
/// </summary>
public sealed class QuestFlow
{
    private readonly List<string> _order;
    private readonly Dictionary<string, JsonObject> _stages;

    public QuestFlow(JsonObject definition)
    {
        Definition = definition;
        _order = [];
        _stages = new(StringComparer.Ordinal);
        foreach (var stage in (definition["stages"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var id = (string)stage["id"]!;
            _order.Add(id);
            _stages[id] = stage;
        }
    }

    public JsonObject Definition { get; }
    public IReadOnlyList<string> StageOrder => _order;
    public string StartStageId => _order[0];
    public JsonObject Stage(string id) => _stages[id];
    public bool HasStage(string id) => _stages.ContainsKey(id);

    public IReadOnlyList<FlowEdge> Outgoing(string stageId)
    {
        var stage = _stages[stageId];
        var edges = new List<FlowEdge>();
        var transitions = (stage["transitions"] as JsonArray ?? []).OfType<JsonObject>().ToArray();
        foreach (var transition in transitions)
        {
            var on = transition["on"]!.AsObject();
            var target = transition["target"]!.AsObject();
            edges.Add(new(
                stageId,
                (string)transition["id"]!,
                (string)on["kind"]!,
                on["objectiveId"] is JsonNode objective ? (string)objective! : null,
                target["stageId"] is JsonNode next
                    ? FlowTarget.Stage((string)next!)
                    : FlowTarget.Ending((string)target["end"]!, target["outcomeId"] is JsonNode outcome ? (string)outcome! : null)));
        }

        if (ReachesImplicitNext(stage, transitions))
        {
            var index = _order.IndexOf(stageId);
            edges.Add(new(
                stageId,
                null,
                "stage-complete",
                null,
                index == _order.Count - 1 ? FlowTarget.Ending("completed") : FlowTarget.Stage(_order[index + 1])));
        }

        return edges;
    }

    // The runtime falls back to the next stage when the stage completes and
    // no stage-complete transition exists. That fallback is unreachable only
    // when every objective the composition counts already leaves the stage
    // through its own objective-complete transition.
    private static bool ReachesImplicitNext(JsonObject stage, IReadOnlyList<JsonObject> transitions)
    {
        if (transitions.Any(transition => (string)transition["on"]!["kind"]! == "stage-complete"))
        {
            return false;
        }

        var leaving = transitions
            .Where(transition => (string)transition["on"]!["kind"]! == "objective-complete" && transition["conditions"] is JsonArray { Count: 0 })
            .Select(transition => (string)transition["on"]!["objectiveId"]!)
            .ToHashSet(StringComparer.Ordinal);
        var counted = (stage["composition"]!["objectiveIds"] as JsonArray ?? []).Select(node => (string)node!).ToArray();
        var mode = (string)stage["composition"]!["mode"]!;
        return mode switch
        {
            "any" => !counted.All(leaving.Contains),
            "threshold" => counted.Count(id => !leaving.Contains(id)) >= (int)stage["composition"]!["threshold"]!,
            _ => !counted.Any(leaving.Contains)
        };
    }

    public IReadOnlyList<FlowEdge> AllEdges() => _order.SelectMany(Outgoing).ToArray();

    /// <summary>Stages reachable from <paramref name="from"/>, in breadth-first order with their distance.</summary>
    public IReadOnlyDictionary<string, int> Reachable(string from)
    {
        var distance = new Dictionary<string, int>(StringComparer.Ordinal) { [from] = 0 };
        var queue = new Queue<string>([from]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in Outgoing(current))
            {
                if (edge.Target.StageId is { } next && !distance.ContainsKey(next))
                {
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return distance;
    }
}
