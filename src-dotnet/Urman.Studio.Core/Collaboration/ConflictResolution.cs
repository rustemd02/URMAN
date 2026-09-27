using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Collaboration;

public enum ConflictChoice
{
    Undecided,
    KeepMine,
    TakeTheirs
}

/// <summary>
/// Applies the author's decisions to a merge result. Conflict paths address
/// entities by ID (<c>[id=…]</c>) and fields by name, so each decision lands on
/// the right entity however the lists were reordered. Mine is already in the
/// merged result, so only "take theirs" changes anything.
/// </summary>
public static class ConflictResolution
{
    public static JsonNode Apply(MergeResult merge, IReadOnlyDictionary<MergeConflict, ConflictChoice> choices)
    {
        if (choices.Values.Any(choice => choice == ConflictChoice.Undecided) || choices.Count < merge.Conflicts.Count)
        {
            throw new InvalidOperationException("Не по всем конфликтам принято решение.");
        }

        var result = merge.Merged!.DeepClone();
        foreach (var conflict in merge.Conflicts.Where(conflict => choices[conflict] == ConflictChoice.TakeTheirs))
        {
            // An order conflict carries the incoming sequence of IDs, not a
            // replacement value. Taking it moves the entities; writing that
            // sequence verbatim would replace the entities with their own IDs.
            result = conflict.Kind == MergeConflictKind.Order
                ? Reorder(result, conflict.Path, Ids(conflict.Theirs))
                : Set(result, conflict.Path, conflict.Theirs?.DeepClone());
        }

        return result;
    }

    /// <summary>
    /// Put an entity list into the incoming order. Entities the list does not
    /// mention keep their places, so a decision about the order never moves
    /// work the other side has not seen.
    /// </summary>
    public static JsonNode Reorder(JsonNode root, string path, IReadOnlyList<string> order)
    {
        if (NodeAt(root, path) is not JsonArray array)
        {
            throw new InvalidOperationException($"Путь {path} не найден.");
        }

        var byId = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var entity in array.OfType<JsonObject>())
        {
            if ((string?)entity["id"] is { } id) byId[id] = entity;
        }

        var incoming = order.Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var inserted = false;
        var items = new List<JsonNode?>();

        void EmitIncoming()
        {
            if (inserted) return;
            inserted = true;
            foreach (var id in incoming) if (emitted.Add(id)) items.Add(byId[id].DeepClone());
        }

        foreach (var item in array)
        {
            var id = (string?)(item as JsonObject)?["id"];
            if (id is not null && byId.ContainsKey(id))
            {
                if (!emitted.Contains(id)) EmitIncoming();
                continue;
            }

            items.Add(item?.DeepClone());
        }

        EmitIncoming();
        return Set(root, path, new JsonArray(items.ToArray()));
    }

    private static IReadOnlyList<string> Ids(JsonNode? value) =>
        value is JsonArray array
            ? array.Select(item => (string?)item).Where(id => id is not null).Select(id => id!).ToArray()
            : [];

    /// <summary>Set (or remove, when null) the value at a merge path.</summary>
    public static JsonNode Set(JsonNode root, string path, JsonNode? value)
    {
        var segments = Segments(path).ToList();
        if (segments.Count == 0) return value ?? new JsonObject();
        JsonNode parent = root;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            parent = Child(parent, segments[index]) ?? throw new InvalidOperationException($"Путь {path} не найден.");
        }

        var last = segments[^1];
        switch (parent)
        {
            case JsonArray array when last.StartsWith("[id=", StringComparison.Ordinal):
            {
                var id = last[4..^1];
                var index = array.Select((item, i) => (item, i)).FirstOrDefault(pair => (string?)(pair.item as JsonObject)?["id"] == id, (null, -1)).Item2;
                if (value is null) { if (index >= 0) array.RemoveAt(index); }
                else if (index >= 0) array[index] = value;
                else array.Add(value);
                break;
            }
            case JsonObject obj:
                if (value is null) obj.Remove(last); else obj[last] = value;
                break;
            default:
                throw new InvalidOperationException($"Путь {path} не найден.");
        }

        return root;
    }

    private static JsonNode? NodeAt(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var segment in Segments(path))
        {
            if (current is null) return null;
            current = Child(current, segment);
        }

        return current;
    }

    private static JsonNode? Child(JsonNode parent, string segment) =>
        segment.StartsWith("[id=", StringComparison.Ordinal)
            ? (parent as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item => (string?)item["id"] == segment[4..^1])
            : (parent as JsonObject)?[segment];

    private static IEnumerable<string> Segments(string path)
    {
        var index = 0;
        while (index < path.Length)
        {
            if (path[index] == '/') { index++; continue; }
            if (path[index] == '[')
            {
                var end = path.IndexOf(']', index);
                yield return path[index..(end + 1)];
                index = end + 1;
                continue;
            }

            var next = path.IndexOfAny(['/', '['], index);
            if (next < 0) next = path.Length;
            yield return path[index..next];
            index = next;
        }
    }
}
