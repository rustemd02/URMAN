using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Editing;

/// <summary>An immutable point of the preview camera; value equality keeps two worlds apart.</summary>
public readonly record struct WorldPoint(float X, float Y, float Z)
{
    public static WorldPoint Origin => new(0f, 0f, 0f);
}

/// <summary>
/// What an author expects to get back per world: which section was open, what was
/// selected and where the preview camera stood (spec AI-13, "сохранить per-world
/// UI state"). It is editor state only and never holds authored content.
/// </summary>
public sealed record WorldAuthoringState
{
    public string? Section { get; init; }
    public string? Selection { get; init; }
    public WorldPoint Pivot { get; init; } = WorldPoint.Origin;
    public float Yaw { get; init; }
    public float Pitch { get; init; }
    public float Distance { get; init; }
    public bool TopView { get; init; }

    /// <summary>
    /// A fresh empty state. It is deliberately a factory and not a cached
    /// instance: a shared value could leak one world's view into another.
    /// </summary>
    public static WorldAuthoringState Empty() => new();
}

/// <summary>
/// Keeps <see cref="WorldAuthoringState"/> per <see cref="CampaignWorldContext.StateId"/>
/// in the ignored <c>.urman-studio/world-authoring-state.v1.json</c> of the
/// checkout, next to the existing preferences and layout files. Switching worlds
/// returns the previous view of that world and never mixes two worlds. The store
/// writes nothing outside that one file and follows the storage rules of the
/// checkout: symbolic links are refused and a file changed by somebody else is
/// never overwritten.
/// </summary>
public sealed class WorldAuthoringStateStore
{
    public const string RelativePath = ".urman-studio/world-authoring-state.v1.json";

    private readonly string _root;
    private readonly string _path;
    private JsonObject? _data;
    private bool _loaded;

    private WorldAuthoringStateStore(string root)
    {
        _root = Path.GetFullPath(root);
        _path = Path.Combine(_root, RelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>Opens the state file of a checkout; a missing or damaged file is an empty state, never an error.</summary>
    public static WorldAuthoringStateStore Open(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new WorldAuthoringStateStore(root);
    }

    /// <summary>Opens the state file next to the workspace the editor has open.</summary>
    public static WorldAuthoringStateStore Open(StudioWorkspace workspace) => Open(workspace.Root);

    public string FilePath => _path;

    /// <summary>SHA-256 of the file as it was read, or null when it did not exist at load time.</summary>
    public string? LoadedSha256 { get; private set; }

    /// <summary>True when somebody else changed or created the file after this store read it.</summary>
    public bool ChangedOnDisk
    {
        get
        {
            _ = Data;
            var current = File.Exists(_path) ? AtomicFile.Sha256(File.ReadAllBytes(_path)) : null;
            return !string.Equals(current, LoadedSha256, StringComparison.Ordinal);
        }
    }

    /// <summary>Ids this checkout has stored state for, ordered.</summary>
    public IReadOnlyList<string> KnownStateIds => Data.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();

    public WorldAuthoringState Get(CampaignWorldContext context) => Get(context.StateId);

    public WorldAuthoringState Get(string stateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        return Data[stateId] is JsonObject stored ? FromJson(stored) : WorldAuthoringState.Empty();
    }

    /// <summary>Stores the state of one world without touching any other world's entry.</summary>
    public void Put(CampaignWorldContext context, WorldAuthoringState state) => Put(context.StateId, state);

    public void Put(string stateId, WorldAuthoringState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        ArgumentNullException.ThrowIfNull(state);
        Data[stateId] = ToJson(state);
    }

    /// <summary>
    /// Writes the file atomically. Nothing is written when the state did not
    /// change. A file that changed on disk after it was read is refused with a
    /// clear error instead of being overwritten (minimal compare-and-swap, the
    /// same rule <see cref="AuthoredFile"/> applies).
    /// </summary>
    public bool Save()
    {
        var text = Data.ToJsonString();
        var bytes = new UTF8Encoding(false).GetBytes(text);
        RefuseLinks(RelativePath);
        var current = File.Exists(_path) ? AtomicFile.Sha256(File.ReadAllBytes(_path)) : null;
        if (!string.Equals(current, LoadedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Файл состояния автора «{RelativePath}» изменён вне Studio. Обновите его перед сохранением, чтобы не потерять чужой выбор мира.");
        }

        if (current is not null && File.ReadAllText(_path) == text)
        {
            return false;
        }

        AtomicFile.WriteAllBytes(_path, bytes);
        LoadedSha256 = AtomicFile.Sha256(bytes);
        return true;
    }

    /// <summary>Forgets the in-memory copy so the next read takes the file from disk again.</summary>
    public void ReloadFromDisk()
    {
        _data = null;
        _loaded = false;
        LoadedSha256 = null;
    }

    private JsonObject Data
    {
        get
        {
            LoadOnce();
            return _data!;
        }
    }

    private void LoadOnce()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        RefuseLinks(RelativePath);
        if (!File.Exists(_path))
        {
            _data = new JsonObject();
            return;
        }

        var bytes = File.ReadAllBytes(_path);
        LoadedSha256 = AtomicFile.Sha256(bytes);
        try
        {
            _data = JsonNode.Parse(bytes) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            _data = new JsonObject();
        }
    }

    /// <summary>
    /// Refuses a path that leaves the checkout or touches a symbolic link. The
    /// same rule the file transaction applies to authored files; kept local so
    /// this store cannot be pointed at a directory outside the checkout.
    /// </summary>
    private void RefuseLinks(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\') || relativePath.Contains(':'))
        {
            throw new ArgumentException("Путь состояния автора должен быть относительным и использовать '/'.", nameof(relativePath));
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException("Путь состояния автора не может содержать пустые сегменты, '.' или '..'.", nameof(relativePath));
        }

        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null || info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Символические ссылки недопустимы в пути состояния автора: {relativePath}");
            }
        }
    }

    private static WorldAuthoringState FromJson(JsonObject stored) => new()
    {
        Section = String(stored["section"]),
        Selection = String(stored["selection"]),
        Pivot = stored["pivot"] is JsonArray pivot && pivot.Count == 3
            ? new WorldPoint(Number(pivot[0]), Number(pivot[1]), Number(pivot[2]))
            : WorldPoint.Origin,
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
            Math.Round(state.Pivot.X, 3),
            Math.Round(state.Pivot.Y, 3),
            Math.Round(state.Pivot.Z, 3)),
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
