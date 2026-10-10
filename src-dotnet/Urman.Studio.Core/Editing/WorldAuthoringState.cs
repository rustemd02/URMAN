using System.IO;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// What an author expects to get back per world: which section was open, what was
/// selected and where the preview camera stood (spec AI-13, "сохранить per-world
/// UI state"). It is editor state only and never holds authored content.
/// </summary>
public sealed record WorldAuthoringState
{
    public string? Section { get; init; }
    public string? Selection { get; init; }
    public float[] Pivot { get; init; } = [0f, 0f, 0f];
    public float Yaw { get; init; }
    public float Pitch { get; init; }
    public float Distance { get; init; }
    public bool TopView { get; init; }

    public static WorldAuthoringState Empty { get; } = new();
}

/// <summary>
/// Keeps <see cref="WorldAuthoringState"/> per <see cref="CampaignWorldContext.StateId"/>
/// in the ignored <c>.urman-studio/world-authoring-state.v1.json</c> of the
/// checkout, next to the existing preferences and layout files. Switching worlds
/// therefore returns the previous view of that world and never mixes two worlds.
/// The store writes nothing outside that file: no content/, no game/.
/// </summary>
public sealed class WorldAuthoringStateStore
{
    public const string RelativePath = ".urman-studio/world-authoring-state.v1.json";

    private readonly string _path;
    private JsonObject? _data;

    private WorldAuthoringStateStore(string root) => _path = Path.Combine(Path.GetFullPath(root), RelativePath);

    /// <summary>Opens the state file of a checkout; a missing or damaged file is an empty state, never an error.</summary>
    public static WorldAuthoringStateStore Open(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new WorldAuthoringStateStore(root);
    }

    /// <summary>Opens the state file next to the workspace the editor has open.</summary>
    public static WorldAuthoringStateStore Open(StudioWorkspace workspace) => Open(workspace.Root);

    public string FilePath => _path;

    private JsonObject Data
    {
        get
        {
            if (_data is not null)
            {
                return _data;
            }

            if (!File.Exists(_path))
            {
                return _data = new JsonObject();
            }

            try
            {
                return _data = JsonNode.Parse(File.ReadAllText(_path)) as JsonObject ?? new JsonObject();
            }
            catch (System.Text.Json.JsonException)
            {
                return _data = new JsonObject();
            }
        }
    }

    /// <summary>Ids this checkout has stored state for, newest file order is not implied.</summary>
    public IReadOnlyList<string> KnownStateIds =>
        Data.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();

    public WorldAuthoringState Get(CampaignWorldContext context) => Get(context.StateId);

    public WorldAuthoringState Get(string stateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        return Data[stateId] is JsonObject stored ? FromJson(stored) : WorldAuthoringState.Empty;
    }

    /// <summary>Stores the state of one world without touching any other world's entry.</summary>
    public void Put(CampaignWorldContext context, WorldAuthoringState state) => Put(context.StateId, state);

    public void Put(string stateId, WorldAuthoringState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        ArgumentNullException.ThrowIfNull(state);
        Data[stateId] = ToJson(state);
    }

    /// <summary>Writes the file atomically; nothing is written when the state did not change.</summary>
    public bool Save()
    {
        var text = Data.ToJsonString();
        if (File.Exists(_path) && File.ReadAllText(_path) == text)
        {
            return false;
        }

        AtomicFile.WriteAllText(_path, text);
        return true;
    }

    private static WorldAuthoringState FromJson(JsonObject stored) => new()
    {
        Section = String(stored["section"]),
        Selection = String(stored["selection"]),
        Pivot = stored["pivot"] is JsonArray pivot && pivot.Count == 3
            ? new[] { Number(pivot[0]), Number(pivot[1]), Number(pivot[2]) }
            : new[] { 0f, 0f, 0f },
        Yaw = Number(stored["yaw"]),
        Pitch = Number(stored["pitch"]),
        Distance = Number(stored["distance"]),
        TopView = stored["topView"] is JsonValue top && top.TryGetValue<bool>(out var flag) && flag
    };

    private static JsonObject ToJson(WorldAuthoringState state) => new()
    {
        ["section"] = state.Section,
        ["selection"] = state.Selection,
        ["pivot"] = new JsonArray(
            Math.Round(state.Pivot.Length > 0 ? state.Pivot[0] : 0f, 3),
            Math.Round(state.Pivot.Length > 1 ? state.Pivot[1] : 0f, 3),
            Math.Round(state.Pivot.Length > 2 ? state.Pivot[2] : 0f, 3)),
        ["yaw"] = Math.Round(state.Yaw, 3),
        ["pitch"] = Math.Round(state.Pitch, 3),
        ["distance"] = Math.Round(state.Distance, 3),
        ["topView"] = state.TopView
    };

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static float Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? (float)number : 0f;
}
