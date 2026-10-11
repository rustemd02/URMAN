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
    private const int MaximumPackagePayloadBytes = 256 * 1024;
    private const int MaximumPackageEntries = 403; // manifest/context/entities plus at most 200 source baseline/current pairs

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

        var options = PackageJsonOptions();
        var encoding = new UTF8Encoding(false);
        var contextBytes = encoding.GetBytes(BuildText(id, null, problem, revision, capture));
        var entityBytes = encoding.GetBytes(entities.ToJsonString(options));
        var initialPayloadBytes = (long)contextBytes.Length + entityBytes.Length;
        if (initialPayloadBytes > MaximumPackagePayloadBytes)
            throw new InvalidOperationException("Пакет с полными исходниками превышает бюджет 256 КБ; файл не записан.");
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["context.txt"] = contextBytes,
            ["entities.json"] = entityBytes
        };
        var sourceManifest = new JsonArray();
        var sourceIndex = 0;
        foreach (var source in CapturePackageSources(dependencies, initialPayloadBytes))
        {
            var stem = $"sources/{sourceIndex++:D3}";
            string? baseRef = null;
            string currentRef;
            if (source.Snapshot.IsNew)
            {
                currentRef = stem + ".current.txt";
                payloads.Add(currentRef, source.CurrentPayload);
            }
            else if (source.ReusesBase)
            {
                currentRef = stem + ".txt";
                baseRef = currentRef;
                payloads.Add(currentRef, source.BaseBytes!);
            }
            else
            {
                baseRef = stem + ".base.txt";
                currentRef = source.CurrentEncoding == "splice-v1" ? stem + ".current.overlay.json" : stem + ".current.txt";
                payloads.Add(baseRef, source.BaseBytes!);
                payloads.Add(currentRef, source.CurrentPayload);
            }

            sourceManifest.Add(new JsonObject
            {
                ["path"] = source.Snapshot.RelativePath,
                ["baseHash"] = source.Snapshot.BaseSha256,
                ["currentHash"] = source.Snapshot.Sha256,
                ["baseRef"] = baseRef,
                ["currentRef"] = currentRef,
                ["currentEncoding"] = source.CurrentEncoding,
                ["isNew"] = source.Snapshot.IsNew,
                ["dirty"] = source.Snapshot.Dirty
            });
        }

        var files = new JsonArray(payloads.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => (JsonNode?)new JsonObject
        {
            ["path"] = item.Key,
            ["bytes"] = item.Value.Length,
            ["sha256"] = AtomicFile.Sha256(item.Value)
        }).ToArray());
        var manifest = new JsonObject
        {
            ["kind"] = "urman.studio-context",
            ["schemaVersion"] = 3,
            ["caseId"] = caseId,
            ["subject"] = id,
            ["baseRevision"] = revision,
            ["created"] = DateTimeOffset.Now.ToString("O"),
            ["entities"] = dependencies.Count,
            ["truncated"] = capture.Truncated,
            ["pendingDependencyIds"] = new JsonArray(capture.Pending.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["dependencyDiscovery"] = "legacy-id-shaped-strings",
            ["payloadHashAlgorithm"] = "sha256",
            ["files"] = files,
            ["sources"] = sourceManifest,
            ["note"] = "Исходник currentHash — SHA-256 полных текущих байтов UTF-8 после восстановления и строгой UTF-8-проверки. currentEncoding=splice-v1 хранит в currentRef JSON-наложение: offsetBytes/removeBytes измеряются в байтах UTF-8 baseRef, insertBase64 — Base64 точной вставляемой последовательности байтов. files[].sha256 и files[].bytes проверяют точные байты файлов в архиве, включая JSON-наложение, а не восстановленный исходник. truncated означает остановленный обход; pendingDependencyIds — обнаруженные, но не приложенные IDs, не полный список пропущенных транзитивных зависимостей. Абсолютные пути пользователя и данные сохранений игроков не включены."
        };
        var manifestBytes = encoding.GetBytes(manifest.ToJsonString(options));
        if (payloads.Values.Sum(bytes => (long)bytes.Length) + manifestBytes.Length > MaximumPackagePayloadBytes)
            throw new InvalidOperationException("Пакет с полными исходниками превышает бюджет 256 КБ; файл не записан.");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "manifest.json", manifestBytes);
            foreach (var (name, bytes) in payloads.OrderBy(item => item.Key, StringComparer.Ordinal))
                Write(zip, name, bytes);
        }
        AtomicFile.WriteAllBytes(path, buffer.ToArray(), overwrite: false);
        return (path, dependencies.Count, new FileInfo(path).Length);
    }

    private sealed record PackageSource(
        AuthoredSourceSnapshot Snapshot,
        byte[]? BaseBytes,
        byte[] CurrentPayload,
        string CurrentEncoding,
        bool ReusesBase);

    private IReadOnlyList<PackageSource> CapturePackageSources(IReadOnlyList<string> dependencies, long initialPayloadBytes)
    {
        var paths = dependencies.Select(dependency => workspace.Locate(dependency)?.RelativePath
                ?? throw new InvalidOperationException("Источник сущности больше не существует."))
            .Distinct(StringComparer.Ordinal).ToArray();
        var result = new List<PackageSource>(paths.Length);
        var utf8 = new UTF8Encoding(false, true);
        var payloadBytes = initialPayloadBytes;
        long reconstructedCurrentBytes = 0;
        foreach (var path in paths)
        {
            if (!IsSafeSourcePath(path) || !workspace.HasFile(path))
                throw new InvalidDataException("Источник сущности не входит в список разрешённых авторских файлов.");
            var snapshot = workspace.File(path).CaptureSource();
            if (!string.Equals(snapshot.RelativePath, path, StringComparison.Ordinal)
                || snapshot.IsNew != (snapshot.BaseText is null && snapshot.BaseSha256 is null)
                || snapshot.IsNew && (snapshot.BaseText is not null || snapshot.BaseSha256 is not null)
                || !snapshot.IsNew && (snapshot.BaseText is null || snapshot.BaseSha256 is null || !IsSha256(snapshot.BaseSha256))
                || !IsSha256(snapshot.Sha256))
                throw new InvalidDataException("Неполный снимок авторского источника.");

            var identical = !snapshot.IsNew && string.Equals(snapshot.BaseText, snapshot.Text, StringComparison.Ordinal);
            var baseByteCount = snapshot.IsNew ? 0 : StrictUtf8ByteCount(utf8, snapshot.BaseText!);
            var currentByteCount = StrictUtf8ByteCount(utf8, snapshot.Text);
            if (baseByteCount > MaximumPackagePayloadBytes || currentByteCount > MaximumPackagePayloadBytes
                || currentByteCount > MaximumPackagePayloadBytes - reconstructedCurrentBytes)
                throw new InvalidOperationException("Суммарный размер восстановленного текста исходников превышает 256 КБ; файл не записан.");
            if (snapshot.IsNew && payloadBytes + currentByteCount > MaximumPackagePayloadBytes
                || !snapshot.IsNew && payloadBytes + baseByteCount > MaximumPackagePayloadBytes)
                throw new InvalidOperationException("Пакет с полными исходниками превышает бюджет 256 КБ; файл не записан.");
            reconstructedCurrentBytes += currentByteCount;

            byte[]? baseBytes = null;
            if (!snapshot.IsNew)
            {
                if (snapshot.BaseText is null || snapshot.BaseSha256 is null || !IsSha256(snapshot.BaseSha256))
                    throw new InvalidDataException("У сохранённого источника отсутствует исходная версия.");
                baseBytes = StrictUtf8Bytes(utf8, snapshot.BaseText);
                if (!string.Equals(AtomicFile.Sha256(baseBytes), snapshot.BaseSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("Хеш исходной версии источника не совпадает.");
            }

            if (identical)
            {
                if (!string.Equals(AtomicFile.Sha256(baseBytes!), snapshot.Sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("Хеш текущей версии источника не совпадает.");
                payloadBytes += baseByteCount;
                result.Add(new(snapshot, baseBytes, baseBytes!, "utf8", true));
                continue;
            }

            if (payloadBytes + baseByteCount > MaximumPackagePayloadBytes)
                throw new InvalidOperationException("Пакет с полными исходниками превышает бюджет 256 КБ; файл не записан.");
            var currentBytes = StrictUtf8Bytes(utf8, snapshot.Text);
            if (!string.Equals(AtomicFile.Sha256(currentBytes), snapshot.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Хеш текущей версии источника не совпадает.");
            if (snapshot.IsNew)
            {
                payloadBytes += currentBytes.Length;
                result.Add(new(snapshot, null, currentBytes, "utf8", false));
                continue;
            }

            var overlayBytes = CreateSourceOverlay(baseBytes!, currentBytes);
            var useOverlay = overlayBytes.Length < currentBytes.Length;
            var currentPayload = useOverlay ? overlayBytes : currentBytes;
            if (payloadBytes + baseBytes!.Length + currentPayload.Length > MaximumPackagePayloadBytes)
                throw new InvalidOperationException("Пакет с полными исходниками превышает бюджет 256 КБ; файл не записан.");
            payloadBytes += baseBytes.Length + (long)currentPayload.Length;
            result.Add(new(snapshot, baseBytes, currentPayload, useOverlay ? "splice-v1" : "utf8", false));
        }

        return result;
    }

    private static byte[] CreateSourceOverlay(byte[] baseBytes, byte[] currentBytes)
    {
        var offset = 0;
        var prefixLimit = Math.Min(baseBytes.Length, currentBytes.Length);
        while (offset < prefixLimit && baseBytes[offset] == currentBytes[offset]) offset++;
        var suffix = 0;
        while (suffix < baseBytes.Length - offset && suffix < currentBytes.Length - offset
            && baseBytes[baseBytes.Length - 1 - suffix] == currentBytes[currentBytes.Length - 1 - suffix]) suffix++;
        var insertLength = currentBytes.Length - offset - suffix;
        var overlay = new JsonObject
        {
            ["kind"] = "urman.source-overlay",
            ["schemaVersion"] = 1,
            ["offsetBytes"] = offset,
            ["removeBytes"] = baseBytes.Length - offset - suffix,
            ["insertBase64"] = Convert.ToBase64String(currentBytes, offset, insertLength)
        };
        return new UTF8Encoding(false, true).GetBytes(overlay.ToJsonString());
    }

    private static byte[] StrictUtf8Bytes(UTF8Encoding encoding, string text)
    {
        try { return encoding.GetBytes(text); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Авторский источник нельзя представить строгим UTF-8.", error); }
    }

    private static int StrictUtf8ByteCount(UTF8Encoding encoding, string text)
    {
        try { return encoding.GetByteCount(text); }
        catch (EncoderFallbackException error) { throw new InvalidDataException("Авторский источник нельзя представить строгим UTF-8.", error); }
    }

    private static bool IsSha256(string value) => value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl)) return false;
        var segments = path.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.StartsWith(".", StringComparison.Ordinal))) return false;
        if (segments.Any(segment => segment.EndsWith(' ') || segment.EndsWith('.') || segment.IndexOfAny("<>\"|?*".ToCharArray()) >= 0)) return false;
        var root = segments[0];
        var contentSource = root == "content" && segments.Length >= 3
            && (segments[1] == "modules" || segments[1] == "campaigns" || segments[1] == "studio");
        var gameSource = root == "game" && segments.Length >= 4 && segments[1] == "content"
            && (segments[2] == "world" || segments[2] == "cutscenes" || segments[2] == "animations");
        var underAllowedRoot = contentSource || gameSource;
        if (!underAllowedRoot) return false;
        var extension = Path.GetExtension(path);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase) && segments[0] == "content" && segments[1] == "modules";
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
        var bytes = new UTF8Encoding(false).GetBytes(text);
        if (File.Exists(path))
        {
            var existing = new FileInfo(path);
            if (existing.Length == bytes.LongLength && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                return path;

            throw new IOException("A different request already exists at this path; it was left unchanged.");
        }

        AtomicFile.WriteAllBytes(path, bytes, overwrite: false);
        return path;
    }

    /// <summary>Preserve validated pilot job artifacts without following project metadata links or replacing a prior attempt.</summary>
    public static void WriteHouseWindowJob(string root, HouseWindowRequest request, HouseWindowProposal? proposal = null)
    {
        string text;
        if (proposal is null)
            text = CodexProposalProvider.SerializeRequest(request);
        else
        {
            HouseWindowProposalValidator.Validate(request, proposal);
            text = JsonSerializer.Serialize(proposal);
        }
        var relative = ".urman-studio/ai-jobs/" + request.RequestId + "/" + (proposal is null ? "request.json" : "proposal.json");
        StudioFileTransaction.EnsureNoLinks(root, relative, allowStudioMetadata: true);
        AtomicFile.WriteAllBytes(Path.Combine(root, relative), new UTF8Encoding(false).GetBytes(text), overwrite: false);
    }

    public sealed record ContextPackage(JsonObject Manifest, JsonArray Entities, string Text, bool HasIntegrityReceipts)
    {
        public IReadOnlyList<ContextPackageSource>? Sources { get; init; }
    }

    public sealed record ContextPackageSource(
        string RelativePath,
        string? BaseHash,
        string CurrentHash,
        string? BaseText,
        string CurrentText,
        bool IsNew,
        bool Dirty);

    /// <summary>Read-only data preview; never extracts entries or writes into a checkout.</summary>
    public static ContextPackage ReadPackage(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 1024 * 1024) throw new InvalidDataException("Архив контекста превышает 1 МБ.");
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        if (zip.Entries.Count is < 3 or > MaximumPackageEntries)
            throw new InvalidDataException("В архиве неверное количество файлов.");
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (payloads.ContainsKey(entry.FullName))
                throw new InvalidDataException("Архив содержит повторные файлы.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) is not (0 or 0x8000) || (entry.ExternalAttributes & (int)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new InvalidDataException("Ссылки внутри пакета не поддерживаются.");
            if (entry.Length < 0 || entry.Length > MaximumPackagePayloadBytes || total + entry.Length > MaximumPackagePayloadBytes)
                throw new InvalidDataException("Распакованный контекст превышает 256 КБ.");
            using var input = entry.Open();
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = input.Read(buffer)) > 0)
            {
                if (output.Length + count > entry.Length || total + output.Length + count > MaximumPackagePayloadBytes)
                    throw new InvalidDataException("Размер содержимого не совпадает с архивом.");
                output.Write(buffer, 0, count);
            }
            if (output.Length != entry.Length) throw new InvalidDataException("Размер содержимого не совпадает с архивом.");
            total += output.Length;
            payloads.Add(entry.FullName, output.ToArray());
        }
        try
        {
            if (!payloads.ContainsKey("manifest.json") || !payloads.ContainsKey("context.txt") || !payloads.ContainsKey("entities.json"))
                throw new InvalidDataException("В архиве отсутствуют обязательные файлы.");
            var encoding = new UTF8Encoding(false, true);
            var manifest = ParsePackageJson(encoding.GetString(payloads["manifest.json"])) as JsonObject
                ?? throw new InvalidDataException("Манифест должен быть объектом.");
            if (PackageString(manifest, "kind") != "urman.studio-context")
                throw new InvalidDataException("Версия пакета не поддерживается.");
            return manifest["schemaVersion"]?.GetValue<int>() switch
            {
                1 => ReadLegacyPackage(manifest, payloads, encoding),
                2 => ReadPackageSources(manifest, payloads, encoding, 2),
                3 => ReadPackageSources(manifest, payloads, encoding, 3),
                _ => throw new InvalidDataException("Версия пакета не поддерживается.")
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("Некорректные данные пакета контекста.", error);
        }
    }

    private static ContextPackage ReadLegacyPackage(JsonObject manifest, IReadOnlyDictionary<string, byte[]> payloads, UTF8Encoding encoding)
    {
        if (payloads.Count != 3 || payloads.Keys.Any(name => name is not ("manifest.json" or "context.txt" or "entities.json")))
            throw new InvalidDataException("Архив v1 содержит неизвестные или повторные файлы.");
        var entities = ParsePackageJson(encoding.GetString(payloads["entities.json"])) as JsonArray
            ?? throw new InvalidDataException("Сущности должны быть массивом.");
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

    private static ContextPackage ReadPackageSources(JsonObject manifest, IReadOnlyDictionary<string, byte[]> payloads, UTF8Encoding encoding, int schemaVersion)
    {
        RequireExactFields(manifest, "kind", "schemaVersion", "caseId", "subject", "baseRevision", "created", "entities",
            "truncated", "pendingDependencyIds", "dependencyDiscovery", "payloadHashAlgorithm", "files", "sources", "note");
        if (manifest["schemaVersion"]?.GetValue<int>() != schemaVersion || PackageString(manifest, "dependencyDiscovery") != "legacy-id-shaped-strings"
            || PackageString(manifest, "payloadHashAlgorithm") != "sha256")
            throw new InvalidDataException("Манифест v2 имеет неверные значения.");
        _ = PackageString(manifest, "caseId");
        var subject = PackageString(manifest, "subject");
        _ = PackageString(manifest, "baseRevision");
        _ = PackageString(manifest, "created");
        _ = PackageString(manifest, "note");
        var entities = ParsePackageJson(encoding.GetString(payloads["entities.json"])) as JsonArray
            ?? throw new InvalidDataException("Сущности должны быть массивом.");
        if (entities.Count is < 1 or > 200 || manifest["entities"]?.GetValue<int>() != entities.Count)
            throw new InvalidDataException("Количество сущностей не совпадает с манифестом.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var entityPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var item = entity as JsonObject ?? throw new InvalidDataException("Сущность должна быть объектом.");
            RequireExactFields(item, "id", "file", "state", "data");
            var id = PackageString(item, "id");
            if (!ids.Add(id) || item["data"] is null)
                throw new InvalidDataException("Повторная сущность или отсутствующие данные.");
            if (item["data"] is JsonObject data && data.ContainsKey("id") && data["id"]?.GetValue<string>() != id)
                throw new InvalidDataException("ID сущности не совпадает с данными.");
            _ = PackageString(item, "state");
            var source = PackageString(item, "file");
            if (!IsSafeSourcePath(source)) throw new InvalidDataException("Источник должен иметь безопасный авторский адрес.");
            entityPaths.Add(source);
        }
        if (!ids.Contains(subject)) throw new InvalidDataException("Данные выбранной сущности отсутствуют.");

        var truncated = manifest["truncated"]?.GetValue<bool>() ?? throw new InvalidDataException("Неверный признак неполноты.");
        var pendingIds = manifest["pendingDependencyIds"] as JsonArray ?? throw new InvalidDataException("Неверный список незавершённых зависимостей.");
        var seenPending = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pending in pendingIds)
            if (pending?.GetValue<string>() is not { Length: > 0 } pendingId || !seenPending.Add(pendingId))
                throw new InvalidDataException("Неверный список незавершённых зависимостей.");
        if (truncated != (pendingIds.Count > 0)) throw new InvalidDataException("Признак неполноты не совпадает со списком зависимостей.");

        var sources = manifest["sources"] as JsonArray ?? throw new InvalidDataException("Список источников должен быть массивом.");
        if (sources.Count is < 1 or > 200 || sources.Count != entityPaths.Count)
            throw new InvalidDataException("Список источников не совпадает с файлами сущностей.");
        var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
        var sourceRefs = new HashSet<string>(StringComparer.Ordinal);
        var previewSources = new List<ContextPackageSource>(sources.Count);
        var expectedPayloadNames = new HashSet<string>(new[] { "manifest.json", "context.txt", "entities.json" }, StringComparer.Ordinal);
        long reconstructedCurrentTotal = 0;
        foreach (var source in sources)
        {
            var item = source as JsonObject ?? throw new InvalidDataException("Источник должен быть объектом.");
            if (schemaVersion == 2)
                RequireExactFields(item, "path", "baseHash", "currentHash", "baseRef", "currentRef", "isNew", "dirty");
            else
                RequireExactFields(item, "path", "baseHash", "currentHash", "baseRef", "currentRef", "currentEncoding", "isNew", "dirty");
            var relativePath = PackageString(item, "path");
            if (!IsSafeSourcePath(relativePath) || !entityPaths.Contains(relativePath) || !sourcePaths.Add(relativePath))
                throw new InvalidDataException("Источник отсутствует у сущностей, повторяется или имеет небезопасный путь.");
            var baseHash = PackageNullableString(item, "baseHash");
            var currentHash = PackageString(item, "currentHash");
            var baseRef = PackageNullableString(item, "baseRef");
            var currentRef = PackageString(item, "currentRef");
            var currentEncoding = schemaVersion == 2 ? "utf8" : PackageString(item, "currentEncoding");
            var isNew = item["isNew"]?.GetValue<bool>() ?? throw new InvalidDataException("Неверный признак нового источника.");
            var dirty = item["dirty"]?.GetValue<bool>() ?? throw new InvalidDataException("Неверный признак черновика.");
            if (!IsSha256(currentHash) || isNew != (baseHash is null && baseRef is null)
                || isNew && (baseHash is not null || baseRef is not null)
                || !isNew && (baseHash is null || baseRef is null || !IsSha256(baseHash))
                || currentEncoding is not ("utf8" or "splice-v1")
                || currentEncoding == "splice-v1" && (schemaVersion != 3 || isNew || baseRef is null || baseRef == currentRef))
                throw new InvalidDataException("Источник содержит несовместимые данные базовой версии.");
            var currentRefSafe = currentEncoding == "splice-v1"
                ? IsSafePackageOverlayRef(currentRef) : IsSafePackageSourceRef(currentRef);
            if (!currentRefSafe || baseRef is not null && !IsSafePackageSourceRef(baseRef))
                throw new InvalidDataException("Ссылка на исходник должна указывать внутрь пакета.");
            if (!sourceRefs.Add(currentRef) || baseRef is not null && baseRef != currentRef && !sourceRefs.Add(baseRef))
                throw new InvalidDataException("Повторная ссылка на файл исходника.");
            expectedPayloadNames.Add(currentRef);
            if (baseRef is not null) expectedPayloadNames.Add(baseRef);

            byte[] currentBytes;
            string currentText;
            if (currentEncoding == "splice-v1")
            {
                var baseBytes = payloads.TryGetValue(baseRef!, out var basePayload)
                    ? basePayload : throw new InvalidDataException("Исходная версия отсутствует в архиве.");
                if (!string.Equals(AtomicFile.Sha256(baseBytes), baseHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Хеш исходной версии источника не совпадает.");
                _ = encoding.GetString(baseBytes);
                var overlayBytes = payloads.TryGetValue(currentRef, out var currentPayload)
                    ? currentPayload : throw new InvalidDataException("Текущий исходник отсутствует в архиве.");
                currentBytes = ApplySourceOverlay(baseBytes, overlayBytes, MaximumPackagePayloadBytes - reconstructedCurrentTotal);
                currentText = encoding.GetString(currentBytes);
            }
            else
            {
                currentBytes = payloads.TryGetValue(currentRef, out var currentPayload)
                    ? currentPayload : throw new InvalidDataException("Текущий исходник отсутствует в архиве.");
                if (currentBytes.LongLength > MaximumPackagePayloadBytes - reconstructedCurrentTotal)
                    throw new InvalidDataException("Суммарный восстановленный текст исходников превышает 256 КБ.");
                currentText = encoding.GetString(currentBytes);
            }
            if (!string.Equals(AtomicFile.Sha256(currentBytes), currentHash, StringComparison.Ordinal))
                throw new InvalidDataException("Хеш текущего исходника не совпадает.");
            string? baseText = null;
            if (baseRef is not null)
            {
                var baseBytes = payloads.TryGetValue(baseRef, out var basePayload)
                    ? basePayload : throw new InvalidDataException("Исходная версия отсутствует в архиве.");
                baseText = encoding.GetString(baseBytes);
                if (!string.Equals(AtomicFile.Sha256(baseBytes), baseHash, StringComparison.Ordinal)
                    || currentEncoding == "utf8" && (baseRef == currentRef) != baseBytes.AsSpan().SequenceEqual(currentBytes))
                    throw new InvalidDataException("Хеш или совместное использование исходника не совпадает.");
            }
            if (currentBytes.LongLength > MaximumPackagePayloadBytes - reconstructedCurrentTotal)
                throw new InvalidDataException("Суммарный восстановленный текст исходников превышает 256 КБ.");
            reconstructedCurrentTotal += currentBytes.LongLength;
            previewSources.Add(new(relativePath, baseHash, currentHash, baseText, currentText, isNew, dirty));
        }
        if (!sourcePaths.SetEquals(entityPaths)) throw new InvalidDataException("Не для каждой сущности передан исходный файл.");
        if (!payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedPayloadNames))
            throw new InvalidDataException("Архив содержит неизвестные или неиспользуемые файлы.");
        ValidateV2Receipts(manifest, payloads);
        return new ContextPackage(manifest, entities, encoding.GetString(payloads["context.txt"]), true) { Sources = previewSources };
    }

    private static void ValidateV2Receipts(JsonObject manifest, IReadOnlyDictionary<string, byte[]> payloads)
    {
        var receipts = manifest["files"] as JsonArray ?? throw new InvalidDataException("Неверные контрольные суммы.");
        if (receipts.Count != payloads.Count - 1) throw new InvalidDataException("Список контрольных сумм неполон.");
        var checkedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var receipt in receipts)
        {
            var item = receipt as JsonObject ?? throw new InvalidDataException("Неверная контрольная сумма.");
            RequireExactFields(item, "path", "bytes", "sha256");
            var name = PackageString(item, "path");
            if (name == "manifest.json" || !checkedNames.Add(name) || !payloads.TryGetValue(name, out var bytes)
                || item["bytes"]?.GetValue<long>() != bytes.LongLength
                || PackageString(item, "sha256") != AtomicFile.Sha256(bytes))
                throw new InvalidDataException("Контрольная сумма или размер не совпадает.");
        }
        if (!checkedNames.SetEquals(payloads.Keys.Where(name => name != "manifest.json")))
            throw new InvalidDataException("Список контрольных сумм не совпадает с файлами архива.");
    }

    private static bool IsSafePackageSourceRef(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("sources/", StringComparison.Ordinal)
            || path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl)) return false;
        var segments = path.Split('/');
        return segments.Length == 2 && segments[0] == "sources" && segments[1].EndsWith(".txt", StringComparison.Ordinal)
            && segments[1].Length > 4 && !segments[1].StartsWith(".", StringComparison.Ordinal)
            && !segments[1].EndsWith(' ') && !segments[1].EndsWith('.') && segments[1].IndexOfAny("<>\"|?*".ToCharArray()) < 0;
    }

    private static bool IsSafePackageOverlayRef(string path) =>
        !string.IsNullOrWhiteSpace(path) && path.StartsWith("sources/", StringComparison.Ordinal)
        && path.Split('/') is ["sources", var name]
        && name.EndsWith(".overlay.json", StringComparison.Ordinal) && name.Length > ".overlay.json".Length
        && !name.StartsWith(".", StringComparison.Ordinal) && !name.EndsWith(' ') && !name.EndsWith('.')
        && name.IndexOfAny("<>\"|?*".ToCharArray()) < 0 && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl);

    private static byte[] ApplySourceOverlay(byte[] baseBytes, byte[] overlayBytes, long maximumResultBytes)
    {
        var encoding = new UTF8Encoding(false, true);
        var overlay = ParsePackageJson(encoding.GetString(overlayBytes)) as JsonObject
            ?? throw new InvalidDataException("Наложение исходника должно быть объектом.");
        RequireExactFields(overlay, "kind", "schemaVersion", "offsetBytes", "removeBytes", "insertBase64");
        if (PackageString(overlay, "kind") != "urman.source-overlay" || overlay["schemaVersion"]?.GetValue<int>() != 1)
            throw new InvalidDataException("Версия наложения исходника не поддерживается.");
        var offset = overlay["offsetBytes"]?.GetValue<int>() ?? throw new InvalidDataException("Неверное смещение наложения.");
        var remove = overlay["removeBytes"]?.GetValue<int>() ?? throw new InvalidDataException("Неверная длина наложения.");
        if (offset < 0 || offset > baseBytes.Length || remove < 0 || remove > baseBytes.Length - offset)
            throw new InvalidDataException("Диапазон наложения выходит за пределы базового исходника.");
        var insertBase64 = overlay["insertBase64"]?.GetValue<string>()
            ?? throw new InvalidDataException("В наложении отсутствуют вставляемые байты.");
        if (insertBase64.Length > MaximumPackagePayloadBytes)
            throw new InvalidDataException("Вставка наложения превышает бюджет пакета.");
        var insert = Convert.FromBase64String(insertBase64);
        if (!string.Equals(Convert.ToBase64String(insert), insertBase64, StringComparison.Ordinal))
            throw new InvalidDataException("Вставка наложения записана неканоническим Base64.");
        var resultLength = (long)offset + insert.LongLength + (baseBytes.Length - offset - remove);
        if (resultLength > MaximumPackagePayloadBytes || resultLength > maximumResultBytes)
            throw new InvalidDataException("Восстановленный исходник превышает бюджет текста.");
        var result = new byte[(int)resultLength];
        Buffer.BlockCopy(baseBytes, 0, result, 0, offset);
        Buffer.BlockCopy(insert, 0, result, offset, insert.Length);
        var suffixStart = offset + remove;
        Buffer.BlockCopy(baseBytes, suffixStart, result, offset + insert.Length, baseBytes.Length - suffixStart);
        return result;
    }

    private static void RequireExactFields(JsonObject value, params string[] expected)
    {
        if (value.Count != expected.Length || value.Any(property => !expected.Contains(property.Key, StringComparer.Ordinal)))
            throw new InvalidDataException("Структура пакета содержит неизвестные или отсутствующие поля.");
    }

    private static string? PackageNullableString(JsonObject value, string key) => value[key] is null
        ? null : value[key]!.GetValue<string>();

    private static System.Text.Json.JsonSerializerOptions PackageJsonOptions() => new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

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
