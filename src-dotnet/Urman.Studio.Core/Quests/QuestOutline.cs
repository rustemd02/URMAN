namespace Urman.Studio.Core.Quests;

/// <summary>An entry of the list view of a quest. The list is derived from <see cref="QuestFlow"/>; it never stores logic of its own.</summary>
public abstract record OutlineItem;

/// <summary>A stage; its objectives with an all/any/N-of-M composition form the parallel group.</summary>
public sealed record StageItem(string StageId) : OutlineItem;

/// <summary>A branch point: each arm starts with the edge that leaves the stage, the arms meet again at <see cref="JoinStageId"/>.</summary>
public sealed record BranchItem(string StageId, IReadOnlyList<BranchArm> Arms, string? JoinStageId) : OutlineItem;

public sealed record BranchArm(FlowEdge Edge, IReadOnlyList<OutlineItem> Items);

/// <summary>The quest ends here with the given status and optional named outcome.</summary>
public sealed record EndItem(FlowTarget Target) : OutlineItem;

/// <summary>A jump back to an earlier stage (a loop). Shown as a link, never expanded, so nothing is duplicated or flattened.</summary>
public sealed record JumpItem(string StageId) : OutlineItem;

public static class QuestOutline
{
    public static IReadOnlyList<OutlineItem> Build(QuestFlow flow) =>
        Walk(flow, flow.StartStageId, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    private static List<OutlineItem> Walk(QuestFlow flow, string? start, HashSet<string> stops, HashSet<string> placed)
    {
        var items = new List<OutlineItem>();
        var current = start;
        while (current is not null && !stops.Contains(current))
        {
            if (!placed.Add(current))
            {
                items.Add(new JumpItem(current));
                return items;
            }

            var edges = flow.Outgoing(current);
            if (edges.Count == 1)
            {
                items.Add(new StageItem(current));
                if (edges[0].Target.IsEnd)
                {
                    items.Add(new EndItem(edges[0].Target));
                    return items;
                }

                current = edges[0].Target.StageId;
                continue;
            }

            var join = FindJoin(flow, edges, placed);
            var innerStops = new HashSet<string>(stops, StringComparer.Ordinal);
            if (join is not null)
            {
                innerStops.Add(join);
            }

            var arms = edges
                .Select(edge => new BranchArm(
                    edge,
                    edge.Target.IsEnd
                        ? [new EndItem(edge.Target)]
                        : Walk(flow, edge.Target.StageId, innerStops, placed)))
                .ToArray();
            items.Add(new BranchItem(current, arms, join));
            current = join;
        }

        return items;
    }

    // The join of a branch is the nearest stage every continuing arm reaches.
    // Arms that end the quest do not take part; with fewer than two
    // continuing arms there is no join and the rest stays inside its arm.
    private static string? FindJoin(QuestFlow flow, IReadOnlyList<FlowEdge> edges, HashSet<string> placed)
    {
        var continuing = edges
            .Where(edge => edge.Target.StageId is not null && !placed.Contains(edge.Target.StageId))
            .Select(edge => flow.Reachable(edge.Target.StageId!))
            .ToArray();
        if (continuing.Length < 2)
        {
            return null;
        }

        return continuing[0].Keys
            .Where(stage => !placed.Contains(stage) && continuing.All(reach => reach.ContainsKey(stage)))
            .OrderBy(stage => continuing.Sum(reach => reach[stage]))
            .ThenBy(stage => flow.StageOrder.ToList().IndexOf(stage))
            .FirstOrDefault();
    }

    /// <summary>A compact, stable text form used by tests and diagnostics.</summary>
    public static string Describe(IReadOnlyList<OutlineItem> items) => string.Join(" ", items.Select(Describe));

    private static string Describe(OutlineItem item) => item switch
    {
        StageItem stage => stage.StageId,
        EndItem end => $"[{end.Target}]",
        JumpItem jump => $"->{jump.StageId}",
        BranchItem branch => $"{branch.StageId}{{{string.Join(" | ", branch.Arms.Select(arm => $"{arm.Edge.TransitionId ?? "next"}: {Describe(arm.Items)}"))}}}" +
            (branch.JoinStageId is null ? "" : $" join:{branch.JoinStageId}"),
        _ => throw new ArgumentOutOfRangeException(nameof(item))
    };
}
