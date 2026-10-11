using System.Text;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Collaboration;

namespace Urman.Studio.Core.Storage;

/// <summary>Raised when the file on disk changed after Studio loaded it; saving over it would lose someone's work.</summary>
public sealed class ExternalChangeException(string path) : IOException($"Файл изменён вне Studio: {path}. Сначала посмотрите изменения.")
{
    public string RelativePath { get; } = path;
}

/// <summary>What happened when a file changed on disk while it was open.</summary>
public sealed record ExternalChange(string RelativePath, bool Reloaded, int AutoMerged, IReadOnlyList<MergeConflict> Conflicts, MergeResult? Merge = null, byte[]? TheirBytes = null)
{
    public string? MineText { get; init; }
}

/// <summary>An immutable full-source snapshot captured from an authored file.</summary>
public sealed record AuthoredSourceSnapshot(
    string RelativePath,
    string? BaseText,
    string Text,
    string? BaseSha256,
    string Sha256,
    bool IsNew,
    bool Dirty);

/// <summary>
/// One authored JSON file inside the project. Entities are addressed by their
/// <c>id</c> (array-root modules) or by property name (object-root files).
/// The text loaded from disk is kept as the merge base, so an outside edit
/// can be merged with unsaved local work instead of replacing it (COLLAB07).
/// </summary>
public sealed class AuthoredFile
{
    // The file as a whole, and the list its entities live in: the root array
    // of a module, or the "entities" array of an object-root world plot.
    private JsonSpanDocument _outer;
    private JsonSpanDocument _document;
    private JsonNode _base;
    // Serialized source state corresponding to the loaded baseline. Entity
    // snapshots alone do not include wrapper fields (or source formatting).
    private string _loadedText;
    // A Markdown source (document, chat, hint) is one entity; its last known
    // text keeps untouched front-matter lines byte for byte on save.
    private readonly bool _markdown;
    private string _markdownText = "";

    private AuthoredFile(string root, string relativePath, byte[] bytes, bool isNew = false)
    {
        Root = root;
        RelativePath = relativePath;
        _markdown = relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        _markdownText = Encoding.UTF8.GetString(bytes);
        (_outer, _document) = Open(_markdownText);
        _base = SourceSnapshot();
        _loadedText = _markdownText;
        LoadedSha256 = isNew ? "" : AtomicFile.Sha256(bytes);
        IsNew = isNew;
    }

    /// <summary>A file Studio created in this session; it reaches the disk on the first save that gives it content.</summary>
    public bool IsNew { get; private set; }

    public static AuthoredFile CreateNew(string root, string relativePath, string initialText) =>
        new(root, relativePath, new UTF8Encoding(false).GetBytes(initialText), isNew: true);

    private (JsonSpanDocument Outer, JsonSpanDocument Entities) Open(string text)
    {
        if (_markdown)
        {
            var single = JsonSpanDocument.Parse("[\n" + MarkdownSource.Parse(text).ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n]\n");
            return (single, single);
        }

        var outer = JsonSpanDocument.Parse(text);
        if (!outer.RootIsArray && outer.IndexOfKey("entities") is var index and >= 0 && outer.Get(index) is JsonArray)
        {
            return (outer, JsonSpanDocument.Parse(outer.RawAt(index), outer.IndentUnit));
        }

        return (outer, outer);
    }

    public string Root { get; }
    public string RelativePath { get; }
    public string FullPath => Path.Combine(Root, RelativePath);
    public string LoadedSha256 { get; private set; }
    public bool Dirty { get; private set; }
    public bool RootIsArray => _document.RootIsArray;

    public static AuthoredFile Load(string root, string relativePath) =>
        new(root, relativePath, File.ReadAllBytes(Path.Combine(root, relativePath)));

    public IEnumerable<string> Keys()
    {
        for (var index = 0; index < _document.Count; index++)
        {
            yield return KeyOf(index);
        }
    }

    public JsonNode? Get(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : _document.Get(index)?.DeepClone();
    }

    public bool Contains(string key) => IndexOf(key) >= 0;

    /// <summary>Set, add (when absent) or remove (when <paramref name="value"/> is null and <paramref name="remove"/>) an entity.</summary>
    public void Put(string key, JsonNode? value, bool remove = false)
    {
        var index = IndexOf(key);
        if (remove)
        {
            if (index >= 0)
            {
                _document.RemoveAt(index);
                UpdateDirty();
            }

            return;
        }

        if (index >= 0)
        {
            if (!JsonNode.DeepEquals(_document.Get(index), value))
            {
                _document.Replace(index, value);
                UpdateDirty();
            }
        }
        else
        {
            _document.Add(value, RootIsArray ? null : key);
            UpdateDirty();
        }
    }

    /// <summary>Reorder entities (array-root or "entities" lists) to the given key order.</summary>
    public void Reorder(IReadOnlyList<string> keys)
    {
        var current = Keys().ToList();
        if (!current.Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException("The new order must contain exactly the current entities.");
        }

        _document.Reorder(keys.Select(key => current.IndexOf(key)).ToArray());
        UpdateDirty();
    }

    public string Text
    {
        get
        {
            if (_markdown)
            {
                return _document.Count == 0 ? "" : MarkdownSource.Write((JsonObject)_document.Get(0)!, _markdownText);
            }

            if (!ReferenceEquals(_outer, _document))
            {
                _outer.ReplaceRaw(_outer.IndexOfKey("entities"), _document.ToText());
            }

            return _outer.ToText();
        }
    }

    /// <summary>Capture full saved and current source without exposing an absolute path.</summary>
    public AuthoredSourceSnapshot CaptureSource()
    {
        StudioFileTransaction.EnsureNoLinks(Root, RelativePath);
        if (Directory.Exists(FullPath) || ChangedOnDisk())
        {
            throw new ExternalChangeException(RelativePath);
        }

        var utf8 = new UTF8Encoding(false, true);
        string? baseText = null;
        string? baseSha256 = null;
        if (!IsNew)
        {
            var baselineBytes = utf8.GetBytes(_loadedText);
            var baselineSha256 = AtomicFile.Sha256(baselineBytes);
            if (!string.Equals(baselineSha256, LoadedSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Authored source baseline is not valid UTF-8: {RelativePath}");
            }

            baseText = _loadedText;
            baseSha256 = LoadedSha256;
        }

        var text = Text;
        var bytes = utf8.GetBytes(text);
        return new(RelativePath, baseText, text, baseSha256, AtomicFile.Sha256(bytes), IsNew, Dirty);
    }

    /// <summary>Capture IDs present in the loaded baseline but deleted from this draft.</summary>
    public string[] GetDeletedEntityIds()
    {
        if (IsNew) return [];

        var current = EntityIds(_document.ToNode()).ToHashSet(StringComparer.Ordinal);
        return EntityIds(_base).Where(id => !current.Contains(id)).ToArray();
    }

    /// <summary>Read entity IDs from source text without accessing the filesystem.</summary>
    public static string[] EntityIdsInSource(string relativePath, string text)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(text);

        if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            if (text.Length == 0) return [];
            var markdown = new JsonArray { MarkdownSource.Parse(text) };
            return EntityIds(markdown);
        }

        return EntityIds(JsonSpanDocument.Parse(text).ToNode());
    }

    public JsonNode Snapshot() => _document.ToNode();

    /// <summary>Replace the full authored draft without changing its on-disk baseline.</summary>
    public void ReplaceDraftText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // Parse first so malformed input leaves the current draft and baseline untouched.
        var (outer, document) = Open(text);
        _outer = outer;
        _document = document;
        _markdownText = text;
        UpdateDirty();
    }

    /// <summary>Compare a Git base and incoming source with the current draft, without adopting either revision.</summary>
    public MergeResult PreviewMerge(string? baseText, string? incomingText)
    {
        return SemanticMerge.Merge(baseText is null ? null : ReadSource(baseText), SourceSnapshot(),
            incomingText is null ? null : ReadSource(incomingText));
    }

    private JsonNode ReadSource(string text) => _markdown
        ? Open(text).Entities.ToNode() : JsonSpanDocument.Parse(text).ToNode();

    private JsonNode SourceSnapshot()
    {
        var mine = Snapshot();
        if (ReferenceEquals(_outer, _document)) return mine;
        var wrapper = (JsonObject)_outer.ToNode();
        wrapper["entities"] = mine;
        return wrapper;
    }

    public string RenderMergedSource(JsonNode merged) => RenderMergedSource(merged, Text);

    private string RenderMergedSource(JsonNode merged, string sourceText)
    {
        if (_markdown)
            return merged is JsonArray { Count: 1 } array && array[0] is JsonObject entity
                ? MarkdownSource.Write(entity, sourceText)
                : throw new InvalidOperationException("Источник Markdown должен содержать один документ.");
        var document = JsonSpanDocument.Parse(sourceText);
        var source = document.ToNode();
        if ((source is JsonArray) != (merged is JsonArray))
            throw new InvalidOperationException("Объединение не может менять тип корня источника.");
        if (merged is JsonArray items && items.Any(item => item is not JsonObject obj || obj["id"] is not JsonValue))
            return merged.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n";
        foreach (var (key, value) in Entries(merged))
            if (!Entries(source).Any(entry => entry.Key == key) || !JsonNode.DeepEquals(Lookup(source, key), value))
                PutIn(document, key, value);
        foreach (var (key, _) in Entries(source))
            if (!Entries(merged).Any(entry => entry.Key == key)) document.RemoveAt(IndexIn(document, key));
        if (merged is JsonArray ordered)
            document.Reorder(ordered.Select(item => document.IndexOfId((string)item!["id"]!)).ToArray());
        return document.ToText();
    }

    /// <summary>A header field of an object-root file (for example a world plot's name).</summary>
    public JsonNode? Header(string key) =>
        ReferenceEquals(_outer, _document) || _outer.IndexOfKey(key) is not (var index and >= 0) ? null : _outer.Get(index)?.DeepClone();

    public bool ChangedOnDisk() => IsNew ? File.Exists(FullPath) : !File.Exists(FullPath) || AtomicFile.Sha256OfFile(FullPath) != LoadedSha256;

    public void Save()
    {
        if (ChangedOnDisk())
        {
            throw new ExternalChangeException(RelativePath);
        }

        if (IsNew && _document.Count == 0 && !Dirty)
        {
            Dirty = false; // an undone creation leaves nothing to write
            return;
        }

        var text = Text;
        var bytes = new UTF8Encoding(false).GetBytes(text);
        AtomicFile.WriteAllBytes(FullPath, bytes);
        _markdownText = text;
        LoadedSha256 = AtomicFile.Sha256(bytes);
        _base = SourceSnapshot();
        _loadedText = text;
        Dirty = false;
        IsNew = false;
    }

    /// <summary>
    /// Pick up an outside edit. Without local changes the file is simply
    /// reloaded. With local changes the three versions are merged by entity
    /// and field; on any conflict nothing local is touched and the conflicts
    /// are returned for the comparison view.
    /// </summary>
    public ExternalChange PullFromDisk()
    {
        var bytes = File.ReadAllBytes(FullPath);
        var (incomingOuter, incoming) = Open(Encoding.UTF8.GetString(bytes));
        if (!Dirty)
        {
            _markdownText = Encoding.UTF8.GetString(bytes);
            _outer = incomingOuter;
            _document = incoming;
            _base = ReadSource(Encoding.UTF8.GetString(bytes));
            _loadedText = Encoding.UTF8.GetString(bytes);
            LoadedSha256 = AtomicFile.Sha256(bytes);
            return new(RelativePath, true, 0, []);
        }

        var merge = SemanticMerge.Merge(_base, SourceSnapshot(), ReadSource(Encoding.UTF8.GetString(bytes)));
        if (merge.Conflicts.Count > 0)
        {
            return new(RelativePath, false, merge.AutoMerged, merge.Conflicts, merge, bytes) { MineText = Text };
        }

        AdoptMerged(merge.Merged!, incomingOuter, incoming, bytes);
        return new(RelativePath, true, merge.AutoMerged, []);
    }

    /// <summary>After the author resolved every conflict: their file becomes the base, the decided result the local state.</summary>
    public void ApplyResolution(JsonNode resolved, byte[] theirBytes, string? expectedText = null)
    {
        if (expectedText is not null)
        {
            StudioFileTransaction.EnsureNoLinks(Root, RelativePath);
            if (Text != expectedText || !File.Exists(FullPath)
                || AtomicFile.Sha256OfFile(FullPath) != AtomicFile.Sha256(theirBytes))
                throw new InvalidOperationException("Сравнение устарело: локальный черновик или входящий файл изменился. Обновите сравнение.");
        }
        var (incomingOuter, incoming) = Open(Encoding.UTF8.GetString(theirBytes));
        AdoptMerged(resolved, incomingOuter, incoming, theirBytes);
    }

    private void AdoptMerged(JsonNode merged, JsonSpanDocument incomingOuter, JsonSpanDocument incoming, byte[] bytes)
    {
        var incomingText = Encoding.UTF8.GetString(bytes);
        ReplaceDraftText(RenderMergedSource(merged, incomingText));
        _loadedText = incomingText;
        _base = ReadSource(incomingText);
        LoadedSha256 = AtomicFile.Sha256(bytes);
        UpdateDirty();
    }

    private void UpdateDirty()
    {
        var text = Text;
        // MarkdownSource.Write normalizes CRLF to LF; compare in its serialized
        // form so an untouched CRLF source does not become dirty on first edit.
        if (_markdown)
        {
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            var loaded = _loadedText.Replace("\r\n", "\n", StringComparison.Ordinal);
            Dirty = !string.Equals(text, loaded, StringComparison.Ordinal);
            return;
        }

        Dirty = !string.Equals(text, _loadedText, StringComparison.Ordinal);
    }

    private int IndexOf(string key) => IndexIn(_document, key);

    private string KeyOf(int index) =>
        _document.RootIsArray
            ? (string?)(_document.Get(index) as JsonObject)?["id"] ?? $"#{index}"
            : _document.KeyAt(index)!;

    private static int IndexIn(JsonSpanDocument document, string key) =>
        document.RootIsArray ? document.IndexOfId(key) : document.IndexOfKey(key);

    private static void PutIn(JsonSpanDocument document, string key, JsonNode? value)
    {
        var index = IndexIn(document, key);
        if (index >= 0)
        {
            document.Replace(index, value);
        }
        else
        {
            document.Add(value, document.RootIsArray ? null : key);
        }
    }

    private static IEnumerable<(string Key, JsonNode? Value)> Entries(JsonNode node) =>
        node is JsonArray array
            ? array.OfType<JsonObject>().Select(item => ((string)item["id"]!, (JsonNode?)item))
            : node.AsObject().Select(entry => (entry.Key, entry.Value));

    private static JsonNode? Lookup(JsonNode node, string key) =>
        node is JsonArray array
            ? array.OfType<JsonObject>().FirstOrDefault(item => (string?)item["id"] == key)
            : node.AsObject()[key];

    private static string[] EntityIds(JsonNode? source)
    {
        var entities = source switch
        {
            JsonArray array => array,
            JsonObject wrapper when wrapper["entities"] is JsonArray array => array,
            _ => null
        };
        if (entities is null) return [];

        return entities.OfType<JsonObject>()
            .Select(entity => entity["id"])
            .OfType<JsonValue>()
            .Select(value => value.TryGetValue<string>(out var id) ? id : null)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToArray();
    }
}
