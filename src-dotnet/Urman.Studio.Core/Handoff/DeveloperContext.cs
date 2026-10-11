using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.AI;
using Urman.Studio.Core.Storage;

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
    public string Revision() => ResolveRevision(workspace.Root);

    /// <summary>Git resolves normal checkouts, packed refs and worktree gitfiles identically.</summary>
    public static string ResolveRevision(string root)
    {
        try
        {
            if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git"))) return "неизвестна";
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = root, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "rev-parse", "--verify", "HEAD^{commit}" }) start.ArgumentList.Add(argument);
            foreach (var name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_NAMESPACE" })
                start.Environment.Remove(name);
            start.Environment["GIT_NO_LAZY_FETCH"] = "1";
            using var process = Process.Start(start);
            if (process is null) return "неизвестна";
            var output = ReadBoundedAsync(process.StandardOutput);
            var errors = ReadBoundedAsync(process.StandardError);
            try
            {
                if (!process.WaitForExit(2000) || !Task.WaitAll([output, errors], 1000) || process.ExitCode != 0)
                    return "неизвестна";
                var head = output.Result.Trim();
                return head.Length is 40 or 64 && head.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
                    ? head : "неизвестна";
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1000);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception or AggregateException)
        {
            return "неизвестна";
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[256];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) != 0)
        {
            if (text.Length + count > 4096) throw new IOException("Git revision output exceeds its bound.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
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
        if (workspace.Locate(id) is null || workspace.Get(id) is null)
            return "Объект больше не существует. Выберите существующий объект для передачи контекста.";
        return BuildText(id, step, problem, Revision(), CaptureDependencies(id));
    }

    private string BuildText(string id, string? step, string? problem, string revision, DependencyCapture capture)
    {
        var summary = catalog.Describe(id);
        var builder = new StringBuilder();
        builder.AppendLine($"{summary.KindLabel} «{summary.Name}», ID {id}.");
        builder.AppendLine($"Проект URMAN, ревизия {revision}; состояние: {DraftState(id)}.");
        if (step is not null) builder.AppendLine($"Выделено: {step}.");
        var parents = new ReferenceIndex(workspace).UsedBy(id).Take(6).Select(user => $"{catalog.Describe(user).KindLabel} «{catalog.NameOf(user)}» ({user})").ToArray();
        if (parents.Length > 0) builder.AppendLine("Используется в: " + string.Join("; ", parents) + ".");
        var dependencies = capture.Entities.Where(dependency => dependency != id).Take(8).Select(dependency => $"«{catalog.NameOf(dependency)}» ({dependency})").ToArray();
        if (dependencies.Length > 0) builder.AppendLine("Ссылается на: " + string.Join("; ", dependencies) + ".");
        if (capture.Truncated)
            builder.AppendLine($"Контекст неполный: включены первые {capture.Entities.Count} объектов; ещё {capture.Pending.Count} обнаруженных зависимостей не приложены. Их собственные зависимости ещё не просмотрены.");
        builder.AppendLine($"Файл: {summary.RelativePath}.");
        if (!string.IsNullOrWhiteSpace(problem)) builder.AppendLine($"Проблема: {problem.Trim()}");
        if (DraftState(id) != "сохранён локально")
        {
            builder.AppendLine("Этого объекта ещё нет в общей истории: разработчику нужен пакет с черновиком, одного ID недостаточно.");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>The first 200 existing entities reached through ID-shaped strings, including the subject.</summary>
    public IReadOnlyList<string> Dependencies(string id) => CaptureDependencies(id).Entities;

    private sealed record DependencyCapture(IReadOnlyList<string> Entities, IReadOnlyList<string> Pending)
    {
        public bool Truncated => Pending.Count > 0;
    }

    private DependencyCapture CaptureDependencies(string id)
    {
        if (workspace.Locate(id) is null || workspace.Get(id) is null)
            throw new InvalidOperationException("Объект больше не существует. Выберите существующий объект для передачи контекста.");
        // shortcut: legacy v1 discovers ID-shaped strings; v2 must use typed references and include non-entity assets.
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

        return new DependencyCapture(result, queue.ToArray());
    }

    /// <summary>A portable package: manifest, readable text and the entities themselves (HAND04, HAND05).</summary>
    public (string Path, int Entities, long Bytes) WritePackage(string id, string directory, string? problem = null)
    {
        var capture = CaptureDependencies(id);
        var dependencies = capture.Entities;
        var revision = Revision();
        var caseId = $"ctx-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")}";
        var path = Path.Combine(directory, caseId + ".zip");
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

        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
        };
        var encoding = new UTF8Encoding(false);
        var contextBytes = encoding.GetBytes(BuildText(id, null, problem, revision, capture));
        var entityBytes = encoding.GetBytes(entities.ToJsonString(options));
        var manifest = new JsonObject
        {
            ["kind"] = "urman.studio-context",
            ["schemaVersion"] = 1,
            ["caseId"] = caseId,
            ["subject"] = id,
            ["baseRevision"] = revision,
            ["created"] = DateTimeOffset.Now.ToString("O"),
            ["entities"] = dependencies.Count,
            ["truncated"] = capture.Truncated,
            ["pendingDependencyIds"] = new JsonArray(capture.Pending.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["dependencyDiscovery"] = "legacy-id-shaped-strings",
            ["payloadHashAlgorithm"] = "sha256",
            ["files"] = new JsonArray(
                new JsonObject { ["path"] = "context.txt", ["bytes"] = contextBytes.Length, ["sha256"] = AtomicFile.Sha256(contextBytes) },
                new JsonObject { ["path"] = "entities.json", ["bytes"] = entityBytes.Length, ["sha256"] = AtomicFile.Sha256(entityBytes) }),
            ["note"] = "Приложенные сущности переданы целиком; truncated означает остановленный обход; pendingDependencyIds — обнаруженные, но не приложенные IDs, не полный список пропущенных транзитивных зависимостей. Пути пользователя и сохранения не включены."
        };
        var manifestBytes = encoding.GetBytes(manifest.ToJsonString(options));
        if ((long)manifestBytes.Length + contextBytes.Length + entityBytes.Length > 256 * 1024)
            throw new InvalidOperationException("Пакет превышает бюджет текста 256 КБ. Выберите меньший объект или область; данные не обрезаны и пакет не записан.");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "manifest.json", manifestBytes);
            Write(zip, "context.txt", contextBytes);
            Write(zip, "entities.json", entityBytes);
        }
        AtomicFile.WriteAllBytes(path, buffer.ToArray(), overwrite: false);
        return (path, dependencies.Count, new FileInfo(path).Length);
    }

    private static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    /// <summary>Export the trusted manual house prompt without overwriting an existing request.</summary>
    public static string WriteHouseWindowRequest(string root, HouseWindowRequest request)
    {
        var text = CodexProposalProvider.CreateClipboardPrompt(request);
        var relative = ".urman-studio/handoff/request-" + request.RequestId + ".txt";
        StudioFileTransaction.EnsureNoLinks(root, relative, allowStudioMetadata: true);
        var path = Path.Combine(root, relative);
        AtomicFile.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text), overwrite: false);
        return path;
    }

    public sealed record ContextPackage(JsonObject Manifest, JsonArray Entities, string Text, bool HasIntegrityReceipts);

    /// <summary>Read-only data preview; never extracts entries or writes into a checkout.</summary>
    public static ContextPackage ReadPackage(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 1024 * 1024) throw new InvalidDataException("Архив контекста превышает 1 МБ.");
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        string[] names = ["manifest.json", "context.txt", "entities.json"];
        if (zip.Entries.Count != names.Length || zip.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != names.Length
            || zip.Entries.Any(entry => !names.Contains(entry.FullName, StringComparer.Ordinal)))
            throw new InvalidDataException("Архив содержит неизвестные или повторные файлы.");
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (((entry.ExternalAttributes >> 16) & 0xF000) is not (0 or 0x8000) || (entry.ExternalAttributes & (int)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new InvalidDataException("Ссылки внутри пакета не поддерживаются.");
            if (entry.Length < 0 || entry.Length > 256 * 1024 || total + entry.Length > 256 * 1024)
                throw new InvalidDataException("Распакованный контекст превышает 256 КБ.");
            using var input = entry.Open();
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = input.Read(buffer)) > 0)
            {
                if (output.Length + count > entry.Length || total + output.Length + count > 256 * 1024)
                    throw new InvalidDataException("Размер содержимого не совпадает с архивом.");
                output.Write(buffer, 0, count);
            }
            if (output.Length != entry.Length) throw new InvalidDataException("Размер содержимого не совпадает с архивом.");
            total += output.Length;
            payloads.Add(entry.FullName, output.ToArray());
        }
        try
        {
            var encoding = new UTF8Encoding(false, true);
            var manifest = ParsePackageJson(encoding.GetString(payloads["manifest.json"])) as JsonObject
                ?? throw new InvalidDataException("Манифест должен быть объектом.");
            var entities = ParsePackageJson(encoding.GetString(payloads["entities.json"])) as JsonArray
                ?? throw new InvalidDataException("Сущности должны быть массивом.");
            if (PackageString(manifest, "kind") != "urman.studio-context" || manifest["schemaVersion"]?.GetValue<int>() != 1)
                throw new InvalidDataException("Версия пакета не поддерживается.");
            var subject = PackageString(manifest, "subject");
            _ = PackageString(manifest, "baseRevision");
            if (entities.Count is < 1 or > 200 || manifest["entities"]?.GetValue<int>() != entities.Count)
                throw new InvalidDataException("Количество сущностей не совпадает с манифестом.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                var item = entity as JsonObject ?? throw new InvalidDataException("Сущность должна быть объектом.");
                if (!ids.Add(PackageString(item, "id")) || item["data"] is null)
                    throw new InvalidDataException("Повторная сущность или отсутствующие данные.");
                if (item["data"] is JsonObject data && data.ContainsKey("id") && data["id"]?.GetValue<string>() != PackageString(item, "id"))
                    throw new InvalidDataException("ID сущности не совпадает с данными.");
                _ = PackageString(item, "state");
                var source = PackageString(item, "file");
                if (Path.IsPathRooted(source) || source.Contains('\\') || source.Contains(':') || source.Any(char.IsControl)
                    || source.Split('/').Any(segment => segment.Length == 0 || segment is "." or "..")
                    || source.Split('/')[0].StartsWith('.'))
                    throw new InvalidDataException("Источник должен иметь безопасный относительный адрес.");
            }
            if (!ids.Contains(subject)) throw new InvalidDataException("Данные выбранной сущности отсутствуют.");
            var hasReceipts = manifest.ContainsKey("files") || manifest.ContainsKey("payloadHashAlgorithm");
            if (hasReceipts)
            {
                var receipts = manifest["files"] as JsonArray ?? throw new InvalidDataException("Неверные контрольные суммы.");
                if (PackageString(manifest, "payloadHashAlgorithm") != "sha256" || receipts.Count != 2)
                    throw new InvalidDataException("Неверные контрольные суммы.");
                var checkedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var receipt in receipts)
                {
                    var item = receipt as JsonObject ?? throw new InvalidDataException("Неверная контрольная сумма.");
                    var name = PackageString(item, "path");
                    if (name is not ("context.txt" or "entities.json") || !checkedNames.Add(name)
                        || item["bytes"]?.GetValue<long>() != payloads[name].LongLength
                        || PackageString(item, "sha256") != AtomicFile.Sha256(payloads[name]))
                        throw new InvalidDataException("Контрольная сумма или размер не совпадает.");
                }
            }
            if (manifest.ContainsKey("truncated")) _ = manifest["truncated"]?.GetValue<bool>() ?? throw new InvalidDataException("Неверный признак неполноты.");
            return new ContextPackage(manifest, entities, encoding.GetString(payloads["context.txt"]), hasReceipts);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("Некорректные данные пакета контекста.", error);
        }
    }

    private static string PackageString(JsonObject value, string key)
    {
        return value[key]?.GetValue<string>() is { Length: > 0 } text && !string.IsNullOrWhiteSpace(text)
            ? text : throw new InvalidDataException("Отсутствует обязательное поле пакета.");
    }

    private static JsonNode? ParsePackageJson(string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
        RefuseDuplicateFields(document.RootElement);
        return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
    }

    private static void RefuseDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Повторное поле JSON.");
                RefuseDuplicateFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RefuseDuplicateFields(item);
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
