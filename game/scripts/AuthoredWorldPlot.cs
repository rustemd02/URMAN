using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// An authored piece of the world stored as data under
/// <c>res://content/world/</c> and edited by URMAN Studio. Entities are found by
/// their stable IDs, never by node names. A missing entity or field is an
/// error, not a silent default: the file is the only owner of these values, so
/// a code-side fallback would be a second owner that drifts from it.
/// </summary>
public sealed class AuthoredWorldPlot
{
    /// <summary>Node metadata naming the authored entity a world node was built from; Studio finds nodes by it.</summary>
    public const string AuthoredIdMeta = "authoredId";

    private readonly Dictionary<string, JsonElement> _entities;

    private AuthoredWorldPlot(string path, JsonElement root)
    {
        Path = path;
        Id = root.GetProperty("id").GetString()!;
        _entities = root.GetProperty("entities").EnumerateArray()
            .ToDictionary(entity => entity.GetProperty("id").GetString()!, entity => entity.Clone(), StringComparer.Ordinal);
    }

    public string Path { get; }
    public string Id { get; }

    public static AuthoredWorldPlot Load(string path)
    {
        if (!global::Godot.FileAccess.FileExists(path))
        {
            throw new InvalidOperationException($"Authored world plot {path} is missing.");
        }

        using var document = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        var root = document.RootElement;
        if (root.GetProperty("kind").GetString() != "urman.world-plot" || root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidOperationException($"Authored world plot {path} has an unsupported kind or schema version.");
        }

        return new(path, root.Clone());
    }

    public JsonElement Params(string localId)
    {
        var id = $"{Id}/{localId}";
        if (!_entities.TryGetValue(id, out var entity))
        {
            throw new InvalidOperationException($"Authored world plot {Path} has no entity {id}.");
        }

        return entity.GetProperty("params");
    }

    public IEnumerable<(string Id, JsonElement Params)> EntitiesOfKind(string kind) =>
        _entities.Values
            .Where(entity => entity.GetProperty("kind").GetString() == kind)
            .Select(entity => (entity.GetProperty("id").GetString()!, entity.GetProperty("params")));

    public float Number(string localId, string field) => Params(localId).GetProperty(field).GetSingle();

    public Vector3 Point(string localId, string field = "position") => ToVector3(Params(localId).GetProperty(field));

    public static Vector3 ToVector3(JsonElement value) =>
        new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());

    public static Vector2 ToVector2(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle());
}
