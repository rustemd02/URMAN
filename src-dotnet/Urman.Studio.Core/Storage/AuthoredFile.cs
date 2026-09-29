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
public sealed record ExternalChange(string RelativePath, bool Reloaded, int AutoMerged, IReadOnlyList<MergeConflict> Conflicts, MergeResult? Merge = null, byte[]? TheirBytes = null);

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
        _base = _document.ToNode();
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
                Dirty = !JsonNode.DeepEquals(_document.ToNode(), _base);
            }

            return;
        }

        if (index >= 0)
        {
            if (!JsonNode.DeepEquals(_document.Get(index), value))
            {
                _document.Replace(index, value);
                Dirty = !JsonNode.DeepEquals(_document.ToNode(), _base);
            }
        }
        else
        {
            _document.Add(value, RootIsArray ? null : key);
            Dirty = !JsonNode.DeepEquals(_document.ToNode(), _base);
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
        Dirty = !JsonNode.DeepEquals(_document.ToNode(), _base);
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

    public JsonNode Snapshot() => _document.ToNode();

    /// <summary>A header field of an object-root file (for example a world plot's name).</summary>
    public JsonNode? Header(string key) =>
        ReferenceEquals(_outer, _document) || _outer.IndexOfKey(key) is not (var index and >= 0) ? null : _outer.Get(index)?.DeepClone();

    public bool ChangedOnDisk() => IsNew ? File.Exists(FullPath) : File.Exists(FullPath) && AtomicFile.Sha256OfFile(FullPath) != LoadedSha256;

    public void Save()
    {
        if (ChangedOnDisk())
        {
            throw new ExternalChangeException(RelativePath);
        }

        if (IsNew && _document.Count == 0)
        {
            Dirty = false; // an undone creation leaves nothing to write
            return;
        }

        var text = Text;
        var bytes = new UTF8Encoding(false).GetBytes(text);
        AtomicFile.WriteAllBytes(FullPath, bytes);
        _markdownText = text;
        LoadedSha256 = AtomicFile.Sha256(bytes);
        _base = _document.ToNode();
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
            _base = incoming.ToNode();
            LoadedSha256 = AtomicFile.Sha256(bytes);
            return new(RelativePath, true, 0, []);
        }

        var merge = SemanticMerge.Merge(_base, _document.ToNode(), incoming.ToNode());
        if (merge.Conflicts.Count > 0)
        {
            return new(RelativePath, false, merge.AutoMerged, merge.Conflicts, merge, bytes);
        }

        AdoptMerged(merge.Merged!, incomingOuter, incoming, bytes);
        return new(RelativePath, true, merge.AutoMerged, []);
    }

    /// <summary>After the author resolved every conflict: their file becomes the base, the decided result the local state.</summary>
    public void ApplyResolution(JsonNode resolved, byte[] theirBytes)
    {
        var (incomingOuter, incoming) = Open(Encoding.UTF8.GetString(theirBytes));
        AdoptMerged(resolved, incomingOuter, incoming, theirBytes);
    }

    private void AdoptMerged(JsonNode merged, JsonSpanDocument incomingOuter, JsonSpanDocument incoming, byte[] bytes)
    {
        // Start from the disk text (their formatting, their untouched entities)
        // and re-apply only the entities whose merged value differs from it.
        var theirs = incoming.ToNode();
        _markdownText = Encoding.UTF8.GetString(bytes);
        _outer = incomingOuter;
        _document = incoming;
        foreach (var (key, value) in Entries(merged))
        {
            var theirValue = Lookup(theirs, key);
            if (!JsonNode.DeepEquals(theirValue, value))
            {
                PutIn(_document, key, value);
            }
        }

        foreach (var (key, _) in Entries(theirs))
        {
            if (Lookup(merged, key) is null && IndexIn(_document, key) >= 0)
            {
                _document.RemoveAt(IndexIn(_document, key));
            }
        }

        _base = theirs;
        LoadedSha256 = AtomicFile.Sha256(bytes);
        Dirty = !JsonNode.DeepEquals(_document.ToNode(), theirs);
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
}
