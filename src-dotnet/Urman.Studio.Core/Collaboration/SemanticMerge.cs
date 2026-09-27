using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Collaboration;

public enum MergeConflictKind
{
    /// <summary>One value changed differently on both sides.</summary>
    Field,
    /// <summary>One side deleted an entity the other side changed.</summary>
    DeleteEdit,
    /// <summary>Both sides reordered the same list differently.</summary>
    Order
}

/// <summary>
/// A decision the author has to make. <see cref="Path"/> addresses entities by
/// their IDs (<c>stages[id=boards]/composition/threshold</c>), never by array
/// index, so it stays valid while the rest of the merge is resolved.
/// </summary>
public sealed record MergeConflict(MergeConflictKind Kind, string Path, JsonNode? Base, JsonNode? Mine, JsonNode? Theirs);

public sealed record MergeResult(JsonNode? Merged, IReadOnlyList<MergeConflict> Conflicts, int AutoMerged);

/// <summary>
/// Three-way merge of authored JSON documents by entity and field (spec
/// COLLAB02–COLLAB05). Arrays whose items carry an <c>id</c> are merged as
/// sets of entities plus an order, not as lines or indexes. Unresolved
/// conflicts keep the local value in <see cref="MergeResult.Merged"/> so the
/// author's work is never replaced before a decision.
/// </summary>
public static class SemanticMerge
{
    public static MergeResult Merge(JsonNode? @base, JsonNode? mine, JsonNode? theirs)
    {
        var conflicts = new List<MergeConflict>();
        var auto = 0;
        var merged = MergeNode(@base, mine, theirs, "", conflicts, ref auto);
        return new(merged, conflicts, auto);
    }

    private static JsonNode? MergeNode(JsonNode? @base, JsonNode? mine, JsonNode? theirs, string path, List<MergeConflict> conflicts, ref int auto)
    {
        if (Same(mine, theirs))
        {
            return Clone(mine);
        }

        if (Same(@base, mine))
        {
            auto++;
            return Clone(theirs);
        }

        if (Same(@base, theirs))
        {
            return Clone(mine);
        }

        if (mine is JsonObject mineObject && theirs is JsonObject theirsObject)
        {
            return MergeObject(@base as JsonObject, mineObject, theirsObject, path, conflicts, ref auto);
        }

        if (mine is JsonArray mineArray && theirs is JsonArray theirsArray &&
            (IsEntityList(mineArray) || IsEntityList(theirsArray)) &&
            IsEntityListOrEmpty(mineArray) && IsEntityListOrEmpty(theirsArray) &&
            (@base is null || @base is JsonArray baseArray && IsEntityListOrEmpty(baseArray)))
        {
            return MergeEntityList(@base as JsonArray ?? [], mineArray, theirsArray, path, conflicts, ref auto);
        }

        conflicts.Add(new(MergeConflictKind.Field, path, Clone(@base), Clone(mine), Clone(theirs)));
        return Clone(mine);
    }

    private static JsonObject MergeObject(JsonObject? @base, JsonObject mine, JsonObject theirs, string path, List<MergeConflict> conflicts, ref int auto)
    {
        var result = new JsonObject();
        // Keep the local key order and append keys only the other side added.
        var keys = mine.Select(entry => entry.Key)
            .Concat(theirs.Select(entry => entry.Key).Where(key => !mine.ContainsKey(key)))
            .ToArray();
        foreach (var key in keys)
        {
            var baseValue = @base?[key];
            var baseHas = @base?.ContainsKey(key) ?? false;
            var mineHas = mine.ContainsKey(key);
            var theirsHas = theirs.ContainsKey(key);
            var childPath = $"{path}/{key}";
            if (!mineHas || !theirsHas)
            {
                // Added or removed on one side only.
                var present = mineHas ? mine[key] : theirs[key];
                var removedSideUnchanged = baseHas && Same(baseValue, present);
                if (!baseHas)
                {
                    result[key] = Clone(present);
                    if (!mineHas)
                    {
                        auto++;
                    }
                }
                else if (!removedSideUnchanged)
                {
                    conflicts.Add(new(MergeConflictKind.Field, childPath, Clone(baseValue), Clone(mine[key]), Clone(theirs[key])));
                    if (mineHas)
                    {
                        result[key] = Clone(present);
                    }
                }
                else if (!theirsHas)
                {
                    auto++;
                }

                continue;
            }

            result[key] = MergeNode(baseValue, mine[key], theirs[key], childPath, conflicts, ref auto);
        }

        return result;
    }

    private static JsonArray MergeEntityList(JsonArray @base, JsonArray mine, JsonArray theirs, string path, List<MergeConflict> conflicts, ref int auto)
    {
        var baseById = ById(@base);
        var mineById = ById(mine);
        var theirsById = ById(theirs);
        var merged = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in mineById.Keys.Concat(theirsById.Keys).Concat(baseById.Keys).Distinct(StringComparer.Ordinal))
        {
            var childPath = $"{path}[id={id}]";
            var inBase = baseById.TryGetValue(id, out var baseItem);
            var inMine = mineById.TryGetValue(id, out var mineItem);
            var inTheirs = theirsById.TryGetValue(id, out var theirsItem);
            if (inMine && inTheirs)
            {
                merged[id] = MergeNode(baseItem, mineItem, theirsItem, childPath, conflicts, ref auto);
            }
            else if (!inBase)
            {
                merged[id] = Clone(inMine ? mineItem : theirsItem);
                if (!inMine)
                {
                    auto++;
                }
            }
            else if (inMine || inTheirs)
            {
                // Deleted on one side. Clean when the survivor is unchanged.
                var survivor = inMine ? mineItem : theirsItem;
                if (Same(baseItem, survivor))
                {
                    dropped.Add(id);
                    if (inMine)
                    {
                        auto++;
                    }
                }
                else
                {
                    conflicts.Add(new(MergeConflictKind.DeleteEdit, childPath, Clone(baseItem), Clone(mineItem), Clone(theirsItem)));
                    if (inMine)
                    {
                        merged[id] = Clone(mineItem);
                    }
                    else
                    {
                        dropped.Add(id);
                    }
                }
            }
            else
            {
                dropped.Add(id);
            }
        }

        var order = MergeOrder(Ids(@base), Ids(mine), Ids(theirs), path, conflicts, ref auto)
            .Where(merged.ContainsKey)
            .ToList();
        foreach (var id in merged.Keys.Where(id => !order.Contains(id)))
        {
            order.Add(id);
        }

        return new JsonArray(order.Select(id => merged[id]).ToArray());
    }

    // Relative order of the entities both sides kept. When only one side
    // reordered, its order wins; new items are placed after their nearest
    // preceding neighbour from the side that added them.
    private static List<string> MergeOrder(IReadOnlyList<string> @base, IReadOnlyList<string> mine, IReadOnlyList<string> theirs, string path, List<MergeConflict> conflicts, ref int auto)
    {
        var common = mine.Where(id => theirs.Contains(id) && @base.Contains(id)).ToArray();
        var baseOrder = @base.Where(common.Contains).ToArray();
        var mineOrder = mine.Where(common.Contains).ToArray();
        var theirsOrder = theirs.Where(common.Contains).ToArray();
        List<string> order;
        if (mineOrder.SequenceEqual(theirsOrder) || theirsOrder.SequenceEqual(baseOrder))
        {
            order = [.. mineOrder];
        }
        else if (mineOrder.SequenceEqual(baseOrder))
        {
            order = [.. theirsOrder];
            auto++;
        }
        else
        {
            conflicts.Add(new(MergeConflictKind.Order, path,
                new JsonArray(baseOrder.Select(id => (JsonNode?)id).ToArray()),
                new JsonArray(mineOrder.Select(id => (JsonNode?)id).ToArray()),
                new JsonArray(theirsOrder.Select(id => (JsonNode?)id).ToArray())));
            order = [.. mineOrder];
        }

        Insert(mine);
        Insert(theirs);
        return order;

        void Insert(IReadOnlyList<string> side)
        {
            for (var index = 0; index < side.Count; index++)
            {
                var id = side[index];
                if (order.Contains(id) || @base.Contains(id))
                {
                    continue;
                }

                var anchor = index == 0 ? -1 : order.IndexOf(side[index - 1]);
                order.Insert(anchor + 1, id);
            }
        }
    }

    private static bool IsEntityList(JsonArray array) =>
        array.Count > 0 && array.All(item => item is JsonObject entity && entity["id"] is JsonValue);

    private static bool IsEntityListOrEmpty(JsonArray array) => array.Count == 0 || IsEntityList(array);

    private static Dictionary<string, JsonNode?> ById(JsonArray array) =>
        array.OfType<JsonObject>().ToDictionary(item => (string)item["id"]!, item => (JsonNode?)item, StringComparer.Ordinal);

    private static string[] Ids(JsonArray array) => array.OfType<JsonObject>().Select(item => (string)item["id"]!).ToArray();

    private static bool Same(JsonNode? left, JsonNode? right) => JsonNode.DeepEquals(left, right);

    private static JsonNode? Clone(JsonNode? node) => node?.DeepClone();
}
