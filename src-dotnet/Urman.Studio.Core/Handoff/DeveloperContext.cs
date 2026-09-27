using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.Core.Handoff;

/// <summary>
/// Exact context for a developer (spec HAND01–HAND05): readable text to paste
/// into a chat, and a portable package with a manifest. A new, unsaved or
/// unsent draft travels as its data with the entities it depends on, because
/// its ID alone would mean nothing in another copy of the project. User paths
/// and saves are never included.
/// </summary>
public sealed class DeveloperContext(StudioWorkspace workspace, EntityCatalog catalog)
{
    public string Revision()
    {
        var git = Path.Combine(workspace.Root, ".git");
        try
        {
            var head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
            if (head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                var name = head[5..];
                var reference = Path.Combine(git, name);
                head = File.Exists(reference) ? File.ReadAllText(reference).Trim() : PackedReference(git, name) ?? name;
            }

            return head.Length >= 7 ? head[..7] : head;
        }
        catch (IOException)
        {
            return "неизвестна";
        }
    }

    // A checkout that ran "git gc" keeps branch tips only in .git/packed-refs.
    private static string? PackedReference(string git, string name)
    {
        var path = Path.Combine(git, "packed-refs");
        if (!File.Exists(path)) return null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] is '#' or '^') continue;
            var space = line.IndexOf(' ');
            if (space > 0 && line[(space + 1)..] == name) return line[..space];
        }

        return null;
    }

    /// <summary>Whether the entity exists only in this Studio (unsaved) or only in this checkout (not yet in the shared history).</summary>
    public string DraftState(string id)
    {
        var address = workspace.Locate(id);
        if (address is null) return "не найден";
        var file = workspace.File(address.RelativePath);
        if (file.IsNew) return "новый черновик: ещё не сохранён и не отправлен";
        if (file.Dirty && !JsonNode.DeepEquals(file.Get(address.Key), SavedValue(file.FullPath, address.Key)))
        {
            return "черновик: есть несохранённые изменения";
        }

        return "сохранён локально";
    }

    public string Text(string id, string? step = null, string? problem = null)
    {
        var summary = catalog.Describe(id);
        var builder = new StringBuilder();
        builder.AppendLine($"{summary.KindLabel} «{summary.Name}», ID {id}.");
        builder.AppendLine($"Проект URMAN, ревизия {Revision()}; состояние: {DraftState(id)}.");
        if (step is not null) builder.AppendLine($"Выделено: {step}.");
        var parents = new ReferenceIndex(workspace).UsedBy(id).Take(6).Select(user => $"{catalog.Describe(user).KindLabel} «{catalog.NameOf(user)}» ({user})").ToArray();
        if (parents.Length > 0) builder.AppendLine("Используется в: " + string.Join("; ", parents) + ".");
        var dependencies = Dependencies(id).Where(dependency => dependency != id).Take(8).Select(dependency => $"«{catalog.NameOf(dependency)}» ({dependency})").ToArray();
        if (dependencies.Length > 0) builder.AppendLine("Ссылается на: " + string.Join("; ", dependencies) + ".");
        builder.AppendLine($"Файл: {summary.RelativePath}.");
        if (!string.IsNullOrWhiteSpace(problem)) builder.AppendLine($"Проблема: {problem.Trim()}");
        if (DraftState(id) != "сохранён локально")
        {
            builder.AppendLine("Этого объекта ещё нет в общей истории: разработчику нужен пакет с черновиком, одного ID недостаточно.");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>The entity and every entity it references, transitively, that exists in this project.</summary>
    public IReadOnlyList<string> Dependencies(string id)
    {
        var result = new List<string>();
        var queue = new Queue<string>([id]);
        var seen = new HashSet<string>(StringComparer.Ordinal) { id };
        while (queue.Count > 0 && result.Count < 200)
        {
            var current = queue.Dequeue();
            result.Add(current);
            if (workspace.Get(current) is not { } entity) continue;
            foreach (var value in Strings(entity))
            {
                if (value.Contains(':') && workspace.Locate(value) is not null && seen.Add(value))
                {
                    queue.Enqueue(value);
                }
            }
        }

        return result;
    }

    /// <summary>A portable package: manifest, readable text and the entities themselves (HAND04, HAND05).</summary>
    public (string Path, int Entities, long Bytes) WritePackage(string id, string directory, string? problem = null)
    {
        Directory.CreateDirectory(directory);
        var caseId = $"ctx-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";
        var path = Path.Combine(directory, caseId + ".zip");
        var dependencies = Dependencies(id);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entities = new JsonArray();
            foreach (var dependency in dependencies)
            {
                var address = workspace.Locate(dependency)!;
                entities.Add(new JsonObject
                {
                    ["id"] = dependency,
                    ["file"] = address.RelativePath,
                    ["state"] = DraftState(dependency),
                    ["data"] = workspace.Get(dependency)!.DeepClone()
                });
            }

            Write(zip, "manifest.json", new JsonObject
            {
                ["kind"] = "urman.studio-context",
                ["schemaVersion"] = 1,
                ["caseId"] = caseId,
                ["subject"] = id,
                ["baseRevision"] = Revision(),
                ["created"] = DateTimeOffset.Now.ToString("O"),
                ["entities"] = dependencies.Count,
                ["note"] = "Сущности приложены целиком; пути пользователя и сохранения не включены."
            }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) }));
            Write(zip, "context.txt", Text(id, problem: problem));
            Write(zip, "entities.json", entities.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All) }));
        }

        return (path, dependencies.Count, new FileInfo(path).Length);
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(new UTF8Encoding(false).GetBytes(text));
    }

    private static JsonNode? SavedValue(string fullPath, string key)
    {
        if (!File.Exists(fullPath)) return null;
        var node = JsonNode.Parse(File.ReadAllText(fullPath));
        var list = node as JsonArray ?? node?["entities"] as JsonArray;
        return list?.OfType<JsonObject>().FirstOrDefault(item => (string?)item["id"] == key) ?? node?[key];
    }

    private static IEnumerable<string> Strings(JsonNode node)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                yield return text;
                break;
            case JsonObject obj:
                foreach (var (_, child) in obj) if (child is not null) foreach (var text in Strings(child)) yield return text;
                break;
            case JsonArray array:
                foreach (var child in array) if (child is not null) foreach (var text in Strings(child)) yield return text;
                break;
        }
    }
}
