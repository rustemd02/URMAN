using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Editing;

/// <summary>Where an entity lives: the file and its key inside it.</summary>
public sealed record EntityAddress(string RelativePath, string Key);

/// <summary>
/// The authored files of one URMAN checkout that Studio edits: narrative
/// modules and campaigns under <c>content/</c> and world plots under
/// <c>game/content/world/</c>. Compiled packs, schemas and caches are not
/// sources and are never opened for editing (spec §19.2).
/// </summary>
public sealed class StudioWorkspace
{
    private static readonly string[] SourceGlobs = ["content/modules", "content/campaigns", "content/studio", "game/content/world", "game/content/cutscenes", "game/content/animations"];
    private readonly Dictionary<string, AuthoredFile> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntityAddress> _index = new(StringComparer.Ordinal);

    private StudioWorkspace(string root) => Root = Path.GetFullPath(root);

    public string Root { get; }
    public IReadOnlyCollection<AuthoredFile> Files => _files.Values;

    public static StudioWorkspace Open(string root)
    {
        var workspace = new StudioWorkspace(root);
        foreach (var folder in SourceGlobs)
        {
            var full = Path.Combine(workspace.Root, folder);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(full, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(workspace.Root, path).Replace('\\', '/');
                workspace._files[relative] = AuthoredFile.Load(workspace.Root, relative);
            }

            // Markdown content sources (documents, old-PC chats and hints):
            // only files with front matter, never READMEs or hand-off notes.
            if (!folder.StartsWith("content/modules", StringComparison.Ordinal)) continue;
            foreach (var path in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (!Storage.MarkdownSource.IsSource(System.IO.File.ReadAllText(path))) continue;
                var relative = Path.GetRelativePath(workspace.Root, path).Replace('\\', '/');
                workspace._files[relative] = AuthoredFile.Load(workspace.Root, relative);
            }
        }

        workspace.Reindex();
        return workspace;
    }

    public AuthoredFile File(string relativePath) => _files[relativePath];

    public bool HasFile(string relativePath) => _files.ContainsKey(relativePath);

    public const string FactLabelsPath = "content/studio/facts.v1.json";

    /// <summary>The editor-only file of human names for story facts; created on first use.</summary>
    public AuthoredFile FactLabels() => HasFile(FactLabelsPath) ? File(FactLabelsPath) : CreateFile(FactLabelsPath, "[\n]\n");

    /// <summary>Register a new authored file (a new side quest's module file or world plot). Nothing is written until it is saved.</summary>
    public AuthoredFile CreateFile(string relativePath, string initialText)
    {
        if (_files.ContainsKey(relativePath) || System.IO.File.Exists(Path.Combine(Root, relativePath)))
        {
            throw new InvalidOperationException($"Файл {relativePath} уже существует.");
        }

        var file = AuthoredFile.CreateNew(Root, relativePath, initialText);
        _files[relativePath] = file;
        return file;
    }

    public EntityAddress? Locate(string id) => _index.GetValueOrDefault(id);

    public JsonNode? Get(string id) => Locate(id) is { } address ? _files[address.RelativePath].Get(address.Key) : null;

    public IEnumerable<string> EntityIds => _index.Keys;

    public void Reindex()
    {
        _index.Clear();
        foreach (var file in _files.Values)
        {
            if (!file.RootIsArray)
            {
                continue; // a manifest: its keys are fields, edited as one document
            }

            foreach (var key in file.Keys())
            {
                _index.TryAdd(key, new(file.RelativePath, key));
            }
        }
    }

    /// <summary>
    /// A fresh, never-reused ID in the author's namespace (spec DATA01–DATA02):
    /// the readable part is only a hint, uniqueness comes from the random part.
    /// </summary>
    public string NewId(string ns, string kind, string hint = "")
    {
        while (true)
        {
            var random = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
            var slug = Slug(hint);
            var id = $"{ns}:{kind}/{(slug.Length == 0 ? "" : slug + "-")}{random}";
            if (!_index.ContainsKey(id))
            {
                return id;
            }
        }
    }

    /// <summary>Files whose disk content no longer matches what Studio loaded.</summary>
    public IReadOnlyList<AuthoredFile> ChangedOnDisk() => _files.Values.Where(file => file.ChangedOnDisk()).ToArray();

    private static string Slug(string text)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z",
            ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
            ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch",
            ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['ә'] = "a", ['ө'] = "o", ['ү'] = "u",
            ['җ'] = "zh", ['ң'] = "n", ['һ'] = "h"
        };
        var builder = new System.Text.StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(ch);
            }
            else if (map.TryGetValue(ch, out var latin))
            {
                builder.Append(latin);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }

            if (builder.Length >= 24)
            {
                break;
            }
        }

        return builder.ToString().Trim('-');
    }
}
