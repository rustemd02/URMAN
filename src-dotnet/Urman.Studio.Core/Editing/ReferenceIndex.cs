using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// The one link mechanism for map, quest, dialogue, item, scene and UI (spec
/// DATA06): any string field of an entity that equals another entity's ID — or
/// an ID-shaped value nested anywhere inside it — is a reference. "Where is
/// this used" and "what does deleting this break" (UX10) both read this index.
/// </summary>
public sealed class ReferenceIndex
{
    private readonly Dictionary<string, HashSet<string>> _usedBy = new(StringComparer.Ordinal);

    public ReferenceIndex(StudioWorkspace workspace)
    {
        foreach (var id in workspace.EntityIds)
        {
            if (workspace.Get(id) is not { } entity)
            {
                continue;
            }

            foreach (var value in Strings(entity))
            {
                if (value != id && value.Contains(':', StringComparison.Ordinal) && value.Contains('/', StringComparison.Ordinal))
                {
                    if (!_usedBy.TryGetValue(value, out var users))
                    {
                        _usedBy[value] = users = new HashSet<string>(StringComparer.Ordinal);
                    }

                    users.Add(id);
                }
            }
        }
    }

    /// <summary>Entities whose data mention <paramref name="id"/>, including IDs that are not top-level entities themselves (interactions inside a scene).</summary>
    public IReadOnlyList<string> UsedBy(string id) =>
        _usedBy.TryGetValue(id, out var users) ? users.Order(StringComparer.Ordinal).ToArray() : [];

    private static IEnumerable<string> Strings(JsonNode node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                yield return text;
                break;
            case JsonObject obj:
                foreach (var (_, child) in obj)
                {
                    if (child is null) continue;
                    foreach (var text in Strings(child)) yield return text;
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is null) continue;
                    foreach (var text in Strings(child)) yield return text;
                }

                break;
        }
    }
}
