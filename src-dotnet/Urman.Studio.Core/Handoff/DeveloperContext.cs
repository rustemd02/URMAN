using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Urman.Content.Resolvers;
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
    private const int MaximumHouseWindowProposalBytes = 32 * 1024;
    private const int MaximumHouseWindowPreviewFailureBytes = 4 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

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
        return Text([id], step, problem);
    }

    public string Text(IReadOnlyList<string> rootIds, string? step = null, string? problem = null)
    {
        var roots = FreezeScopeEntityIds(rootIds);
        var capture = CaptureDependencies(roots);
        var tombstones = CaptureTombstones(capture.Missing);
        return BuildText(roots[0], roots, step, problem, Revision(), capture, tombstones, sourceSnapshotsIncluded: false);
    }

    private string BuildText(
        string id,
        IReadOnlyList<string> scopeEntityIds,
        string? step,
        string? problem,
        string revision,
        DependencyCapture capture,
        IReadOnlyList<PackageTombstone> tombstones,
        bool sourceSnapshotsIncluded)
    {
        var summary = catalog.Describe(id);
        var builder = new StringBuilder();
        builder.AppendLine($"{summary.KindLabel} «{summary.Name}», ID {id}.");
        builder.AppendLine($"Проект URMAN, ревизия {revision}; состояние: {DraftState(id)}.");
        builder.AppendLine($"Выбрано объектов: {scopeEntityIds.Count}; основной объект: {id}.");
        builder.AppendLine("Выбранные ID (корни, в заданном порядке): " + string.Join(", ", scopeEntityIds) + ".");
        if (step is not null) builder.AppendLine($"Выделено: {step}.");
        var parents = new ReferenceIndex(workspace).UsedBy(id).Take(6).Select(user => $"{catalog.Describe(user).KindLabel} «{catalog.NameOf(user)}» ({user})").ToArray();
        if (parents.Length > 0) builder.AppendLine("Используется в: " + string.Join("; ", parents) + ".");
        var selected = scopeEntityIds.ToHashSet(StringComparer.Ordinal);
        var dependencies = capture.Entities.Where(dependency => !selected.Contains(dependency)).Take(8).Select(dependency => $"«{catalog.NameOf(dependency)}» ({dependency})").ToArray();
        if (dependencies.Length > 0) builder.AppendLine("Дополнительные обнаруженные зависимости: " + string.Join("; ", dependencies) + ".");
        builder.AppendLine("Обход зависимостей частичный: известные типизированные поля сканируются у content/modules сущностей категорий assets, characters, scenes, documents, capabilities, dialogues, quests, knowledge и vocabulary; категория цели определяется каноническим ContentId в существующих реестрах. Knowledge и vocabulary обходятся только по известным массивам ContentId; допустимость цели в конкретном поле не проверяется. Texts остаются конечными целями: отсутствие переходов от них не означает полного покрытия. Сцены, диалоги и документы обходятся только по перечисленным полям; более глубокие ссылки, условия, эффекты, физические сущности и связи, другие поля и world/custom-источники не покрыты.");
        if (capture.Missing.Count > 0)
            builder.AppendLine("Нет живого payload объектов для отсутствующих типизированных зависимостей: " + string.Join(", ", capture.Missing) + ".");
        if (tombstones.Count > 0)
        {
            builder.AppendLine("Удаление подтверждено только сравнением загруженного базового снимка Studio с текущим черновиком (это не доказывает Git-историю или намерение удаления): "
                + string.Join(", ", tombstones.Select(tombstone => $"{tombstone.Id} в {tombstone.File}")) + ".");
            builder.AppendLine(sourceSnapshotsIncluded
                ? "Полные снимки указанных исходных файлов включены в пакет для просмотра; там могут быть соседние сущности и их черновики. Просмотр не применяет их к проекту."
                : "Полные исходные файлы в этом текстовом контексте не приложены.");
        }
        if (capture.Unsupported.Count > 0)
            builder.AppendLine("Источники без поддержанного обхода типизированных ссылок: " + string.Join(", ", capture.Unsupported) + ".");
        if (capture.Truncated)
            builder.AppendLine($"Достигнут предел 200 приложенных объектов: ещё {capture.Pending.Count} найденных целей не приложены, их связи не просмотрены.");
        builder.AppendLine($"Файл: {summary.RelativePath}.");
        if (!string.IsNullOrWhiteSpace(problem)) builder.AppendLine($"Проблема: {problem.Trim()}");
        if (DraftState(id) != "сохранён локально")
        {
            builder.AppendLine("Этого объекта ещё нет в общей истории: разработчику нужен пакет с черновиком, одного ID недостаточно.");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>The first 200 existing entities reached through known typed module references, including the subject.</summary>
    public IReadOnlyList<string> Dependencies(string id) => CaptureDependencies(FreezeScopeEntityIds([id])).Entities;

    private sealed record DependencyCapture(
        IReadOnlyList<string> Entities,
        IReadOnlyList<string> Pending,
        IReadOnlyList<string> Missing,
        IReadOnlyList<string> Unsupported)
    {
        public bool Truncated => Pending.Count > 0;
    }

    private string[] FreezeScopeEntityIds(IReadOnlyList<string> rootIds)
    {
        ArgumentNullException.ThrowIfNull(rootIds);
        var roots = rootIds.ToArray();
        if (roots.Length is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(rootIds), "Выберите от 1 до 200 исходных объектов.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root) || !seen.Add(root))
                throw new ArgumentException("Выбранные ID должны быть непустыми и уникальными.", nameof(rootIds));
            if (workspace.Locate(root) is null || workspace.Get(root) is null)
                throw new InvalidOperationException($"Объект {root} больше не существует. Выберите существующие объекты для передачи контекста.");
        }

        return roots;
    }

    private DependencyCapture CaptureDependencies(IReadOnlyList<string> roots)
    {
        var result = new List<string>();
        var queue = new Queue<string>(roots);
        var seen = new HashSet<string>(roots, StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var unsupported = new List<string>();
        while (queue.Count > 0 && result.Count < 200)
        {
            var current = queue.Dequeue();
            result.Add(current);
            var address = workspace.Locate(current);
            if (address is null || workspace.Get(current) is not JsonObject entity
                || !IsModuleEntityPath(address.RelativePath))
            {
                unsupported.Add(current);
                continue;
            }

            var sourceCategory = EntityCatalog.KindOf(current, null) switch
            {
                "asset" => "assets",
                "character" => "characters",
                "scene" => "scenes",
                "document" => "documents",
                "capability" => "capabilities",
                "dialogue" => "dialogues",
                "quest" => "quests",
                "knowledge" => "knowledge",
                "vocabulary" => "vocabulary",
                _ => null
            };
            if (sourceCategory is null)
            {
                unsupported.Add(current);
                continue;
            }

            IReadOnlyList<ContentReference> references;
            try
            {
                references = ManifestClosureValidator.ReadReferences(sourceCategory, entity);
            }
            catch (ResolverException error)
            {
                throw new InvalidDataException(
                    $"Некорректные типизированные ссылки в сущности {current} ({address.RelativePath}): {error.Message}",
                    error);
            }

            foreach (var reference in references)
            {
                var expectedKind = reference.TargetCategory switch
                {
                    "assets" => "asset",
                    "texts" => "text",
                    "characters" => "character",
                    "scenes" => "scene",
                    "documents" => "document",
                    "capabilities" => "capability",
                    "dialogues" => "dialogue",
                    "quests" => "quest",
                    "knowledge" => "knowledge",
                    "vocabulary" => "vocabulary",
                    _ => throw new InvalidDataException("Типизированная ссылка использует неподдержанную категорию.")
                };
                if (!string.Equals(EntityCatalog.KindOf(reference.TargetId, null), expectedKind, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        $"Ссылка {current} ({address.RelativePath}){reference.Pointer} ведёт к цели {reference.TargetId}, тип которой не совпадает с категорией {reference.TargetCategory}.");

                var target = workspace.Get(reference.TargetId);
                if (workspace.Locate(reference.TargetId) is null || target is null)
                {
                    missing.Add(reference.TargetId);
                    continue;
                }

                if (reference.ExpectedAssetKind is { } expectedAssetKind)
                {
                    if (reference.TargetCategory != "assets"
                        || target is not JsonObject asset
                        || asset["kind"] is not JsonValue assetKindValue
                        || !assetKindValue.TryGetValue<string>(out var actualAssetKind)
                        || !string.Equals(actualAssetKind, expectedAssetKind, StringComparison.Ordinal))
                        throw new InvalidDataException(
                            $"Ссылка {current} ({address.RelativePath}){reference.Pointer} ведёт к {reference.TargetId}, который должен быть ресурсом типа {expectedAssetKind}.");
                }

                if (seen.Add(reference.TargetId)) queue.Enqueue(reference.TargetId);
            }
        }

        return new DependencyCapture(result, queue.ToArray(), missing.Order(StringComparer.Ordinal).ToArray(), unsupported);
    }

    /// <summary>A portable package: manifest, readable text and the entities themselves (HAND04, HAND05).</summary>
    public (string Path, int Entities, long Bytes) WritePackage(string id, string directory, string? problem = null) =>
        WritePackage([id], directory, problem);

    public (string Path, int Entities, long Bytes) WritePackage(IReadOnlyList<string> rootIds, string directory, string? problem = null)
    {
        var roots = FreezeScopeEntityIds(rootIds);
        var id = roots[0];
        var capture = CaptureDependencies(roots);
        var dependencies = capture.Entities;
        var tombstones = CaptureTombstones(capture.Missing);
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
        var contextBytes = encoding.GetBytes(BuildText(id, roots, null, problem, revision, capture, tombstones, sourceSnapshotsIncluded: true));
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
        var packageSources = CapturePackageSources(dependencies, initialPayloadBytes, tombstones.Select(tombstone => tombstone.File).ToArray());
        ValidateTombstoneSnapshots(packageSources.Select(source => new ContextPackageSource(
            source.Snapshot.RelativePath,
            source.Snapshot.BaseSha256,
            source.Snapshot.Sha256,
            source.Snapshot.BaseText,
            source.Snapshot.Text,
            source.Snapshot.IsNew,
            source.Snapshot.Dirty)), tombstones);
        foreach (var source in packageSources)
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
            ["schemaVersion"] = 6,
            ["caseId"] = caseId,
            ["subject"] = id,
            ["scopeEntityIds"] = new JsonArray(roots.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["tombstones"] = new JsonArray(tombstones.Select(tombstone => (JsonNode?)new JsonObject
            {
                ["id"] = tombstone.Id,
                ["file"] = tombstone.File
            }).ToArray()),
            ["baseRevision"] = revision,
            ["created"] = DateTimeOffset.Now.ToString("O"),
            ["entities"] = dependencies.Count,
            ["truncated"] = capture.Truncated,
            ["pendingDependencyIds"] = new JsonArray(capture.Pending.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["dependencyDiscovery"] = "partial-typed-module-fields-v3",
            ["referenceCoverageComplete"] = false,
            ["unsupportedEntityIds"] = new JsonArray(capture.Unsupported.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["missingDependencyIds"] = new JsonArray(capture.Missing.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            ["payloadHashAlgorithm"] = "sha256",
            ["files"] = files,
            ["sources"] = sourceManifest,
            ["note"] = "Зависимости частично обнаруживаются по известным типизированным полям content/modules сущностей категорий assets, characters, scenes, documents, capabilities, dialogues, quests, knowledge и vocabulary. Категория целевой сущности определяется каноническим ContentId в существующих реестрах. Knowledge и vocabulary обходятся только по известным массивам ContentId; допустимость цели в конкретном поле не проверяется; texts остаются конечными целями, а отсутствие переходов от них не означает полного покрытия. Сцены, диалоги и документы обходятся только по перечисленным полям. Более глубокие ссылки, условия, эффекты, физические сущности и связи, другие поля и world/custom-источники не покрыты; referenceCoverageComplete=false сохраняется даже без pending. unsupportedEntityIds перечисляет включённые сущности без поддержанного обхода; missingDependencyIds — типизированные цели, отсутствующие в рабочем пространстве; tombstones — лишь те missingDependencyIds, которые встретились ровно один раз среди загруженных базовых снимков и отсутствуют во всех текущих ID проекта. Это подтверждает только различие сохранённой при загрузке базы и текущего черновика, не Git-историю и не причину удаления. Файлы исходников прилагаются целиком и могут содержать соседние сущности и их черновики; просмотр пакета не применяет их к проекту. pendingDependencyIds — найденные существующие цели за пределом 200 сущностей. currentHash и baseHash — SHA-256 полных текущего текста и загруженной базовой версии UTF-8, не фрагмента entities[].data; currentEncoding=splice-v1 хранит currentRef JSON-наложение: offsetBytes/removeBytes измеряются в байтах UTF-8 baseRef, insertBase64 — Base64 точной вставляемой последовательности байтов. files[].sha256 и files[].bytes проверяют точные байты файлов в архиве, включая JSON-наложение, а не восстановленный исходник. Абсолютные пути пользователя и данные сохранений игроков не включены."
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

    private sealed record PackageTombstone(string Id, string File);

    private static void ValidateTombstoneSnapshots(
        IEnumerable<ContextPackageSource> sources,
        IReadOnlyList<PackageTombstone> tombstones)
    {
        var currentEntityIds = new HashSet<string>(StringComparer.Ordinal);
        var baselineEntityIdPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var entityId in AuthoredFile.EntityIdsInSource(source.RelativePath, source.CurrentText))
                if (!currentEntityIds.Add(entityId))
                    throw new InvalidDataException($"Текущие исходники содержат повторный ID сущности {entityId}.");

            if (source.BaseText is null) continue;
            foreach (var entityId in AuthoredFile.EntityIdsInSource(source.RelativePath, source.BaseText))
            {
                if (!baselineEntityIdPaths.TryGetValue(entityId, out var paths))
                    baselineEntityIdPaths.Add(entityId, paths = []);
                paths.Add(source.RelativePath);
            }
        }

        foreach (var tombstone in tombstones)
        {
            if (!baselineEntityIdPaths.TryGetValue(tombstone.Id, out var baselinePaths)
                || baselinePaths.Count != 1
                || !string.Equals(baselinePaths[0], tombstone.File, StringComparison.Ordinal)
                || currentEntityIds.Contains(tombstone.Id))
                throw new InvalidDataException($"Удалённый объект {tombstone.Id} не подтверждён единственным базовым источником или всё ещё присутствует в текущем проекте.");
        }
    }

    private IReadOnlyList<PackageTombstone> CaptureTombstones(IReadOnlyList<string> missingIds)
    {
        if (missingIds.Count == 0) return [];

        var missing = missingIds.ToHashSet(StringComparer.Ordinal);
        var removedById = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in workspace.Files)
        {
            foreach (var id in file.GetDeletedEntityIds())
            {
                if (!missing.Contains(id)) continue;
                if (!removedById.TryGetValue(id, out var paths)) removedById.Add(id, paths = []);
                paths.Add(file.RelativePath);
            }
        }

        var result = new List<PackageTombstone>();
        foreach (var id in missingIds)
        {
            if (workspace.Locate(id) is not null || workspace.IsAmbiguous(id)
                || !removedById.TryGetValue(id, out var paths) || paths.Count != 1)
                continue;
            result.Add(new(id, paths[0]));
        }

        return result;
    }

    private IReadOnlyList<PackageSource> CapturePackageSources(
        IReadOnlyList<string> dependencies,
        long initialPayloadBytes,
        IReadOnlyList<string>? additionalPaths = null)
    {
        var paths = dependencies.Select(dependency => workspace.Locate(dependency)?.RelativePath
                ?? throw new InvalidOperationException("Источник сущности больше не существует."))
            .Concat(additionalPaths ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).ToArray();
        if (paths.Length > 200)
            throw new InvalidOperationException("Пакет включает больше 200 различных исходных файлов; файл не записан.");
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

    private static bool IsModuleEntityPath(string path) =>
        path.StartsWith("content/modules/", StringComparison.Ordinal) && IsSafeSourcePath(path);

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
            text = CodexProposalProvider.SerializeProposal(request, proposal);
        var relative = ".urman-studio/ai-jobs/" + request.RequestId + "/" + (proposal is null ? "request.json" : "proposal.json");
        StudioFileTransaction.EnsureNoLinks(root, relative, allowStudioMetadata: true);
        AtomicFile.WriteAllBytes(Path.Combine(root, relative), new UTF8Encoding(false).GetBytes(text), overwrite: false);
    }

    /// <summary>Record an explicit local preview failure for the exact stored request and proposal.</summary>
    public static void MarkHouseWindowPreviewFailed(string root, HouseWindowRequest request, HouseWindowProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(proposal);
        var requestBytes = StrictUtf8.GetBytes(AuthoringRequestV2.Serialize(request));
        var proposalBytes = StrictUtf8.GetBytes(CodexProposalProvider.SerializeProposal(request, proposal));
        if (proposalBytes.Length > MaximumHouseWindowProposalBytes)
            throw new InvalidDataException("The stored house-window proposal exceeds its size limit.");

        var requestRelative = HouseWindowJobRelativePath(request.RequestId, "request.json");
        var proposalRelative = HouseWindowJobRelativePath(request.RequestId, "proposal.json");
        var failureRelative = HouseWindowJobRelativePath(request.RequestId, "preview-failed.json");
        var storedRequest = ReadBoundedHouseWindowJobFile(root, requestRelative, HouseWindowProposalValidator.MaximumSerializedRequestBytes);
        var storedProposal = ReadBoundedHouseWindowJobFile(root, proposalRelative, MaximumHouseWindowProposalBytes);
        if (!storedRequest.AsSpan().SequenceEqual(requestBytes) || !storedProposal.AsSpan().SequenceEqual(proposalBytes))
            throw new InvalidDataException("The stored request or proposal does not match this preview failure.");

        var receipt = SerializeHouseWindowPreviewFailureReceipt(request.RequestId,
            AtomicFile.Sha256(requestBytes), AtomicFile.Sha256(proposalBytes));
        if (receipt.Length > MaximumHouseWindowPreviewFailureBytes)
            throw new InvalidDataException("The preview-failure receipt exceeds its size limit.");
        StudioFileTransaction.EnsureNoLinks(root, failureRelative, allowStudioMetadata: true);
        var path = Path.Combine(root, failureRelative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            var existing = ReadBoundedHouseWindowJobFile(root, failureRelative, MaximumHouseWindowPreviewFailureBytes);
            if (existing.AsSpan().SequenceEqual(receipt)) return;
            throw new IOException("A different preview-failure receipt already exists for this request.");
        }

        AtomicFile.WriteAllBytes(path, receipt, overwrite: false);
    }

    /// <summary>Remove only the local preview-failure receipt for an explicit request ID.</summary>
    public static void ClearHouseWindowPreviewFailure(string root, string requestId)
    {
        var relative = HouseWindowJobRelativePath(requestId, "preview-failed.json");
        StudioFileTransaction.EnsureNoLinks(root, relative, allowStudioMetadata: true);
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Read and verify the exact request/proposal pair with an explicit local preview-failure receipt.</summary>
    public static (HouseWindowRequest Request, HouseWindowProposal Proposal) ReadHouseWindowPreviewFailure(string root, string requestId)
    {
        var requestRelative = HouseWindowJobRelativePath(requestId, "request.json");
        var proposalRelative = HouseWindowJobRelativePath(requestId, "proposal.json");
        var failureRelative = HouseWindowJobRelativePath(requestId, "preview-failed.json");
        var failurePath = Path.Combine(root, failureRelative.Replace('/', Path.DirectorySeparatorChar));
        StudioFileTransaction.EnsureNoLinks(root, failureRelative, allowStudioMetadata: true);
        if (!File.Exists(failurePath))
            throw new InvalidOperationException("This request has no explicit local preview-failure receipt.");

        var receiptBytes = ReadBoundedHouseWindowJobFile(root, failureRelative, MaximumHouseWindowPreviewFailureBytes);
        var (receiptRequestId, requestHash, proposalHash) = ParseHouseWindowPreviewFailureReceipt(receiptBytes);
        if (!string.Equals(receiptRequestId, requestId, StringComparison.Ordinal))
            throw new InvalidDataException("The preview-failure receipt belongs to another request.");

        var requestBytes = ReadBoundedHouseWindowJobFile(root, requestRelative, HouseWindowProposalValidator.MaximumSerializedRequestBytes);
        var proposalBytes = ReadBoundedHouseWindowJobFile(root, proposalRelative, MaximumHouseWindowProposalBytes);
        if (!string.Equals(AtomicFile.Sha256(requestBytes), requestHash, StringComparison.Ordinal)
            || !string.Equals(AtomicFile.Sha256(proposalBytes), proposalHash, StringComparison.Ordinal))
            throw new InvalidDataException("The stored request or proposal no longer matches the preview-failure receipt.");

        HouseWindowRequest request;
        try
        {
            request = AuthoringRequestV2.Parse(StrictUtf8.GetString(requestBytes));
            if (!string.Equals(request.RequestId, requestId, StringComparison.Ordinal)
                || !StrictUtf8.GetBytes(AuthoringRequestV2.Serialize(request)).AsSpan().SequenceEqual(requestBytes))
                throw new InvalidDataException("The stored request is not the canonical request named by its receipt.");
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            throw new InvalidDataException("The stored request is invalid.", error);
        }

        HouseWindowProposal proposal;
        try
        {
            proposal = CodexProposalProvider.ParseProposal(StrictUtf8.GetString(proposalBytes), request);
            if (!StrictUtf8.GetBytes(CodexProposalProvider.SerializeProposal(request, proposal)).AsSpan().SequenceEqual(proposalBytes))
                throw new InvalidDataException("The stored proposal is not canonical.");
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or FormatException or NotSupportedException or CodexProposalProvider.CodexProposalException)
        {
            throw new InvalidDataException("The stored proposal is invalid.", error);
        }

        return (request, proposal);
    }

    private static string HouseWindowJobRelativePath(string requestId, string fileName)
    {
        if (!Guid.TryParseExact(requestId, "N", out var parsed)
            || !string.Equals(parsed.ToString("N"), requestId, StringComparison.Ordinal))
            throw new ArgumentException("RequestId must be a canonical 32-character lowercase hexadecimal GUID.", nameof(requestId));

        return $".urman-studio/ai-jobs/{requestId}/{fileName}";
    }

    private static byte[] ReadBoundedHouseWindowJobFile(string root, string relativePath, int maximumBytes)
    {
        StudioFileTransaction.EnsureNoLinks(root, relativePath, allowStudioMetadata: true);
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length < 0 || input.Length > maximumBytes)
            throw new InvalidDataException("A stored house-window job artifact exceeds its size limit.");

        using var output = new MemoryStream((int)input.Length);
        var buffer = new byte[4096];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + count > maximumBytes)
                throw new InvalidDataException("A stored house-window job artifact exceeds its size limit.");
            output.Write(buffer, 0, count);
        }

        return output.ToArray();
    }

    private static byte[] SerializeHouseWindowPreviewFailureReceipt(string requestId, string requestHash, string proposalHash) =>
        StrictUtf8.GetBytes(new JsonObject
        {
            ["requestId"] = requestId,
            ["requestHash"] = requestHash,
            ["proposalHash"] = proposalHash
        }.ToJsonString());

    private static (string RequestId, string RequestHash, string ProposalHash) ParseHouseWindowPreviewFailureReceipt(byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                MaxDepth = 4,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The preview-failure receipt is invalid JSON.", error);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The preview-failure receipt must be an object.");
            RefuseDuplicateFields(root);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || !fields.TryAdd(property.Name, property.Value.GetString()!))
                    throw new InvalidDataException("The preview-failure receipt has duplicate or invalid fields.");
            }

            if (fields.Count != 3 || !fields.TryGetValue("requestId", out var requestId)
                || !fields.TryGetValue("requestHash", out var requestHash)
                || !fields.TryGetValue("proposalHash", out var proposalHash)
                || !IsSha256(requestHash) || !IsSha256(proposalHash))
                throw new InvalidDataException("The preview-failure receipt has missing, unsupported, or invalid fields.");
            if (!Guid.TryParseExact(requestId, "N", out var parsedRequestId)
                || !string.Equals(parsedRequestId.ToString("N"), requestId, StringComparison.Ordinal))
                throw new InvalidDataException("The preview-failure receipt request ID is invalid.");

            if (!bytes.AsSpan().SequenceEqual(SerializeHouseWindowPreviewFailureReceipt(requestId, requestHash, proposalHash)))
                throw new InvalidDataException("The preview-failure receipt is not canonical JSON.");
            return (requestId, requestHash, proposalHash);
        }
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
                4 => ReadPackageSources(manifest, payloads, encoding, 4),
                5 => ReadPackageSources(manifest, payloads, encoding, 5),
                6 => ReadPackageSources(manifest, payloads, encoding, 6),
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
        var expectedManifestFields = schemaVersion == 6
            ? new[] { "kind", "schemaVersion", "caseId", "subject", "scopeEntityIds", "tombstones", "baseRevision", "created", "entities", "truncated",
                "pendingDependencyIds", "dependencyDiscovery", "referenceCoverageComplete", "unsupportedEntityIds", "missingDependencyIds",
                "payloadHashAlgorithm", "files", "sources", "note" }
            : schemaVersion == 5
            ? new[] { "kind", "schemaVersion", "caseId", "subject", "scopeEntityIds", "baseRevision", "created", "entities", "truncated",
                "pendingDependencyIds", "dependencyDiscovery", "referenceCoverageComplete", "unsupportedEntityIds", "missingDependencyIds",
                "payloadHashAlgorithm", "files", "sources", "note" }
            : schemaVersion == 4
            ? new[] { "kind", "schemaVersion", "caseId", "subject", "baseRevision", "created", "entities", "truncated",
                "pendingDependencyIds", "dependencyDiscovery", "referenceCoverageComplete", "unsupportedEntityIds", "missingDependencyIds",
                "payloadHashAlgorithm", "files", "sources", "note" }
            : new[] { "kind", "schemaVersion", "caseId", "subject", "baseRevision", "created", "entities", "truncated",
                "pendingDependencyIds", "dependencyDiscovery", "payloadHashAlgorithm", "files", "sources", "note" };
        RequireExactFields(manifest, expectedManifestFields);
        var discovery = PackageString(manifest, "dependencyDiscovery");
        var supportedDiscovery = schemaVersion switch
        {
            4 => discovery is "partial-typed-module-fields-v1" or "partial-typed-module-fields-v2" or "partial-typed-module-fields-v3",
            5 or 6 => discovery == "partial-typed-module-fields-v3",
            _ => discovery == "legacy-id-shaped-strings"
        };
        if (manifest["schemaVersion"]?.GetValue<int>() != schemaVersion
            || !supportedDiscovery
            || PackageString(manifest, "payloadHashAlgorithm") != "sha256"
            || schemaVersion >= 4 && manifest["referenceCoverageComplete"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Манифест содержит неверные значения версии или обнаружения зависимостей.");
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
        var orderedIds = new List<string>(entities.Count);
        var entityPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var item = entity as JsonObject ?? throw new InvalidDataException("Сущность должна быть объектом.");
            RequireExactFields(item, "id", "file", "state", "data");
            var id = PackageString(item, "id");
            if (!ids.Add(id) || item["data"] is null)
                throw new InvalidDataException("Повторная сущность или отсутствующие данные.");
            orderedIds.Add(id);
            if (item["data"] is JsonObject data && data.ContainsKey("id") && data["id"]?.GetValue<string>() != id)
                throw new InvalidDataException("ID сущности не совпадает с данными.");
            _ = PackageString(item, "state");
            var source = PackageString(item, "file");
            if (!IsSafeSourcePath(source)) throw new InvalidDataException("Источник должен иметь безопасный авторский адрес.");
            entityPaths.Add(source);
        }
        if (!ids.Contains(subject)) throw new InvalidDataException("Данные выбранной сущности отсутствуют.");

        var scopeIds = new HashSet<string>(StringComparer.Ordinal);
        if (schemaVersion >= 5)
        {
            var scopeIdsNode = manifest["scopeEntityIds"] as JsonArray
                ?? throw new InvalidDataException("Выбранные сущности должны быть массивом.");
            if (scopeIdsNode.Count is < 1 or > 200)
                throw new InvalidDataException("Число выбранных сущностей должно быть от 1 до 200.");
            var orderedScopeIds = new List<string>(scopeIdsNode.Count);
            foreach (var scopeIdNode in scopeIdsNode)
            {
                if (scopeIdNode?.GetValue<string>() is not { Length: > 0 } scopeId
                    || !scopeIds.Add(scopeId) || !ids.Contains(scopeId))
                    throw new InvalidDataException("Выбранные сущности должны быть непустыми, уникальными и включёнными в пакет.");
                orderedScopeIds.Add(scopeId);
            }
            if (!string.Equals(orderedScopeIds[0], subject, StringComparison.Ordinal)
                || !orderedIds.Take(orderedScopeIds.Count).SequenceEqual(orderedScopeIds, StringComparer.Ordinal))
                throw new InvalidDataException("Основная сущность должна быть первой, а порядок выбранных сущностей должен совпадать с началом списка объектов.");
        }

        var truncated = manifest["truncated"]?.GetValue<bool>() ?? throw new InvalidDataException("Неверный признак неполноты.");
        var pendingIds = manifest["pendingDependencyIds"] as JsonArray ?? throw new InvalidDataException("Неверный список незавершённых зависимостей.");
        var seenPending = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (var pending in pendingIds)
            if (pending?.GetValue<string>() is not { Length: > 0 } pendingId || !seenPending.Add(pendingId))
                throw new InvalidDataException("Незавершённые зависимости должны быть уникальны и не повторять приложенные сущности.");
        if (truncated != (pendingIds.Count > 0)) throw new InvalidDataException("Признак неполноты не совпадает со списком зависимостей.");
        var missingIds = new HashSet<string>(StringComparer.Ordinal);
        if (schemaVersion >= 4)
        {
            var unsupportedIds = manifest["unsupportedEntityIds"] as JsonArray
                ?? throw new InvalidDataException("Список неподдержанных сущностей должен быть массивом.");
            var seenUnsupported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unsupported in unsupportedIds)
                if (unsupported?.GetValue<string>() is not { Length: > 0 } unsupportedId
                    || !ids.Contains(unsupportedId) || !seenUnsupported.Add(unsupportedId))
                    throw new InvalidDataException("Неподдержанные сущности должны быть уникальны и входить в пакет.");

            var missingIdsNode = manifest["missingDependencyIds"] as JsonArray
                ?? throw new InvalidDataException("Список отсутствующих зависимостей должен быть массивом.");
            foreach (var missing in missingIdsNode)
                if (missing?.GetValue<string>() is not { Length: > 0 } missingId
                    || !missingIds.Add(missingId) || seenPending.Contains(missingId))
                    throw new InvalidDataException("Отсутствующие зависимости должны быть уникальны и не повторять приложенные или ожидающие сущности.");
        }

        var tombstones = new List<PackageTombstone>();
        var tombstoneIds = new HashSet<string>(StringComparer.Ordinal);
        var expectedSourcePaths = new HashSet<string>(entityPaths, StringComparer.Ordinal);
        if (schemaVersion == 6)
        {
            var tombstonesNode = manifest["tombstones"] as JsonArray
                ?? throw new InvalidDataException("Список удалённых объектов должен быть массивом.");
            foreach (var tombstoneNode in tombstonesNode)
            {
                var tombstone = tombstoneNode as JsonObject
                    ?? throw new InvalidDataException("Удалённый объект должен быть объектом.");
                RequireExactFields(tombstone, "id", "file");
                var tombstoneId = PackageString(tombstone, "id");
                var tombstoneFile = PackageString(tombstone, "file");
                if (!tombstoneIds.Add(tombstoneId) || !missingIds.Contains(tombstoneId)
                    || ids.Contains(tombstoneId) || seenPending.Contains(tombstoneId) || scopeIds.Contains(tombstoneId)
                    || !IsSafeSourcePath(tombstoneFile))
                    throw new InvalidDataException("Удалённые объекты должны быть уникальными отсутствующими зависимостями с безопасным исходным файлом.");
                tombstones.Add(new(tombstoneId, tombstoneFile));
                expectedSourcePaths.Add(tombstoneFile);
            }
        }

        var sources = manifest["sources"] as JsonArray ?? throw new InvalidDataException("Список источников должен быть массивом.");
        if (sources.Count is < 1 or > 200 || sources.Count != expectedSourcePaths.Count)
            throw new InvalidDataException("Список источников не совпадает с файлами сущностей и удалённых объектов.");
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
            if (!IsSafeSourcePath(relativePath) || !expectedSourcePaths.Contains(relativePath) || !sourcePaths.Add(relativePath))
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
                || currentEncoding == "splice-v1" && (schemaVersion < 3 || isNew || baseRef is null || baseRef == currentRef))
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
        if (!sourcePaths.SetEquals(expectedSourcePaths)) throw new InvalidDataException("Не для каждой сущности или удаления передан исходный файл.");
        if (schemaVersion == 6) ValidateTombstoneSnapshots(previewSources, tombstones);
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
