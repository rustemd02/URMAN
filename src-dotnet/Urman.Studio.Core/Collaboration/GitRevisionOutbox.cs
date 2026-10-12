using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Collaboration;

public enum GitOutgoingState { Pending, Sent }

public sealed record GitOutgoingFile(string RelativePath, string? BeforeSha256, string? AfterSha256, int Bytes);

public sealed record GitOutgoingRevision(string OperationId, string BaseRevision, string? CommitRevision,
    string Branch, string RemoteBranch, IReadOnlyList<GitOutgoingFile> Files, GitOutgoingState State,
    IReadOnlyList<string> ExcludedPaths);

/// <summary>Freezes registered authored text against a cached origin branch and sends it only after explicit confirmation.</summary>
public sealed class GitRevisionOutbox(StudioWorkspace workspace)
{
    private const string OutboxPath = ".urman-studio/outbox";
    private const string ArchivePath = ".urman-studio/outbox-archive";
    private const string OperationLocksPath = ".urman-studio/outbox-locks";
    private const string HeroRecipePath = "game/content/studio/hero_house.recipe.json";
    private const int MaxFiles = 2000;
    // shortcut: keep the retained queue bounded until an explicit archive policy exists.
    private const int MaxOperations = 64;
    private const int ListedReceiptBudget = 16 * 1024 * 1024;
    private const int ContentBudget = 8 * 1024 * 1024;
    private const int PathBudget = 256 * 1024;
    private const int ReceiptBudget = 2 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Freeze saved registered source files only. This is offline and never creates a commit.</summary>
    public async Task<GitOutgoingRevision> PrepareAsync(string branch, CancellationToken token = default)
    {
        try { return await PrepareCoreAsync(branch, token).ConfigureAwait(false); }
        catch (Exception error) when (NeedsSanitizedRefusal(error))
        {
            throw Refusal("Подготовка очереди не завершена. Проверьте сохранённые исходники и повторите действие.");
        }
    }

    private async Task<GitOutgoingRevision> PrepareCoreAsync(string branch, CancellationToken token)
    {
        GitRevisionInbox.ValidateBranch(branch);
        token.ThrowIfCancellationRequested();
        if (!RepositoryExists()) throw Refusal("Рабочая папка не является Git-репозиторием.");

        var sources = workspace.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (sources.Length > MaxFiles) throw Refusal("В подготовку можно включить не более 2000 исходных файлов.");
        var captured = new Dictionary<string, SourceCapture>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            token.ThrowIfCancellationRequested();
            ValidateSourcePath(file.RelativePath);
            StudioFileTransaction.EnsureNoLinks(workspace.Root, file.RelativePath);
            if (file.Dirty) throw Refusal("Сначала сохраните все текстовые черновики.");
            var source = file.CaptureSource();
            if (source.Dirty) throw Refusal("Сначала сохраните все текстовые черновики.");
            captured.Add(source.RelativePath, new(file, source));
        }

        var baseRevision = await RevisionAsync(
            ["rev-parse", "--verify", "refs/remotes/origin/" + branch + "^{commit}"], token).ConfigureAwait(false);
        var objectFormat = await TextAsync(["rev-parse", "--show-object-format"], 32, token).ConfigureAwait(false);
        if (objectFormat is not ("sha1" or "sha256")) throw Refusal("Формат объектов этого Git-репозитория не поддерживается.");
        var tree = await ReadBaseTreeAsync(baseRevision, token).ConfigureAwait(false);
        var candidates = new Dictionary<string, PendingFile>(StringComparer.Ordinal);
        var excluded = new SortedSet<string>(StringComparer.Ordinal);
        var totalBytes = 0L;

        foreach (var (path, capture) in captured.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (capture.Source.IsNew)
            {
                excluded.Add(path + " (новый несохранённый исходник)");
                continue;
            }
            var after = Utf8.GetBytes(capture.Source.Text);
            if (after.Length > ContentBudget) throw Refusal("Отдельный исходник превышает лимит 8 MiB.");

            if (!tree.TryGetValue(path, out var baseEntry))
            {
                AddPending(path, null, null, after, objectFormat, candidates, ref totalBytes);
                continue;
            }

            if (baseEntry.Mode != "100644" || baseEntry.Type != "blob")
            {
                excluded.Add(path + " (в Git это не обычный текстовый файл)");
                continue;
            }

            var currentOid = GitBlobHash(objectFormat, after);
            if (string.Equals(currentOid, baseEntry.ObjectId, StringComparison.Ordinal)) continue;

            var before = await ReadBlobAsync(baseEntry.ObjectId, token).ConfigureAwait(false);
            var beforeText = Utf8.GetString(before);
            _ = AuthoredFile.CreateNew(workspace.Root, path, beforeText).Snapshot();
            AddPending(path, before, baseEntry.ObjectId, after, objectFormat, candidates, ref totalBytes);
        }

        var heroBytes = ReadOptionalHeroRecipe();
        if (heroBytes is not null)
        {
            _ = Utf8.GetString(heroBytes);
            _ = AuthoredFile.CreateNew(workspace.Root, HeroRecipePath, Utf8.GetString(heroBytes)).Snapshot();
            if (tree.TryGetValue(HeroRecipePath, out var baseHero))
            {
                if (baseHero.Mode != "100644" || baseHero.Type != "blob")
                    excluded.Add(HeroRecipePath + " (в Git это не обычный текстовый файл)");
                else
                {
                    var currentOid = GitBlobHash(objectFormat, heroBytes);
                    if (!string.Equals(currentOid, baseHero.ObjectId, StringComparison.Ordinal))
                    {
                        var before = await ReadBlobAsync(baseHero.ObjectId, token).ConfigureAwait(false);
                        var beforeText = Utf8.GetString(before);
                        _ = AuthoredFile.CreateNew(workspace.Root, HeroRecipePath, beforeText).Snapshot();
                        AddPending(HeroRecipePath, before, baseHero.ObjectId, heroBytes, objectFormat, candidates, ref totalBytes);
                    }
                }
            }
            else
            {
                AddPending(HeroRecipePath, null, null, heroBytes, objectFormat, candidates, ref totalBytes);
            }
        }

        var changedPaths = await ChangedPathsAsync(baseRevision, token).ConfigureAwait(false);
        await ValidateIntroducedResourceReferencesAsync(baseRevision, tree, candidates, token).ConfigureAwait(false);
        foreach (var path in changedPaths)
        {
            if (path.StartsWith(".urman-studio/", StringComparison.Ordinal)) continue;
            if (!candidates.ContainsKey(path))
                excluded.Add(path + " (не входит в сохранённый набор исходников)");
        }
        foreach (var path in candidates.Keys) excluded.RemoveWhere(value => value.StartsWith(path + " (", StringComparison.Ordinal));
        if (candidates.TryGetValue(HeroRecipePath, out var recipe))
        {
            var recipeDocument = JsonDocument.Parse(Utf8.GetString(recipe.After));
            var modelPath = ReadModelPath(recipeDocument.RootElement);
            var resource = modelPath is null ? null : ResourcePath(modelPath);
            if (resource is not null && changedPaths.Contains(resource, StringComparer.Ordinal))
                throw Refusal("Рецепт ссылается на модель, изменённую вне текстового набора. Сначала включите согласованный asset flow.");
        }

        if (candidates.Count == 0 && excluded.Count == 0) throw Refusal("Нет сохранённых текстовых изменений для подготовки.");
        if (candidates.Count > MaxFiles || excluded.Count > MaxFiles) throw Refusal("Список исходников превышает установленный предел.");
        if (Encoding.UTF8.GetByteCount(string.Join('\0', candidates.Keys.Concat(excluded))) > PathBudget)
            throw Refusal("Список включённых и исключённых путей превышает допустимый размер.");
        EnsureOutboxCapacity();

        RevalidateSources(captured);
        if (!SameBytes(heroBytes, ReadOptionalHeroRecipe()))
            throw Refusal("Рецепт дома изменился во время подготовки. Обновите сравнение.");

        var id = Guid.NewGuid().ToString("N");
        var remoteBranch = "codex/studio-" + id;
        var directory = OperationDirectory(id);
        EnsureMetadataPath(OutboxPath);
        Directory.CreateDirectory(Path.Combine(workspace.Root, OutboxPath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureMetadataPath(OutboxPath);
        Directory.CreateDirectory(directory);
        var relativeDirectory = OperationRelativeDirectory(id);
        EnsureMetadataPath(relativeDirectory);
        var hooksRelative = relativeDirectory + "/hooks-empty";
        Directory.CreateDirectory(Absolute(hooksRelative));
        EnsureEmptyHooksDirectory(hooksRelative);

        var files = candidates.Values.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        var capsuleFiles = new CapsuleFile[files.Length];
        for (var index = 0; index < files.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var file = files[index];
            var beforeImage = file.Before is null ? null : "before-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
            var afterImage = "after-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
            if (beforeImage is not null)
                WritePrivateFile(relativeDirectory + "/" + beforeImage, file.Before!);
            WritePrivateFile(relativeDirectory + "/" + afterImage, file.After);
            capsuleFiles[index] = new(file.Path, beforeImage, afterImage, file.Before?.Length ?? 0,
                file.After.Length, file.Before is null ? null : AtomicFile.Sha256(file.Before),
                AtomicFile.Sha256(file.After), file.BaseObjectId, GitBlobHash(objectFormat, file.After));
        }

        var endpoint = await PushEndpointAsync(hooksRelative, token).ConfigureAwait(false);
        var fingerprint = Fingerprint(endpoint);
        var receipt = new Receipt(1, id, baseRevision, null, branch, remoteBranch, fingerprint,
            "Pending", capsuleFiles, excluded.ToArray());
        await UpdatePrivateRefAsync(hooksRelative, "refs/urman-studio/outbox/" + id, baseRevision,
            new string('0', baseRevision.Length), token).ConfigureAwait(false);
        RevalidateSources(captured);
        if (!SameBytes(heroBytes, ReadOptionalHeroRecipe()))
            throw Refusal("Рецепт дома изменился во время подготовки. Обновите сравнение.");
        WriteReceipt(relativeDirectory, receipt, createOnly: true);
        return Public(receipt);
    }

    /// <summary>Read the exact frozen before/after text for the explicit author preview.</summary>
    public async Task<GitIncomingFile> ReadFileAsync(string operationId, string relativePath, CancellationToken token = default)
    {
        try { return await ReadFileCoreAsync(operationId, relativePath, token).ConfigureAwait(false); }
        catch (Exception error) when (NeedsSanitizedRefusal(error))
        {
            throw Refusal("Файлы сохранённого preview не удалось проверить. Очередь оставлена без изменений.");
        }
    }

    private async Task<GitIncomingFile> ReadFileCoreAsync(string operationId, string relativePath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var receipt = LoadReceipt(operationId);
        var file = receipt.Files.SingleOrDefault(item => item.Path == relativePath)
            ?? throw Refusal("Файл не входит в сохранённый запрос.");
        var before = file.BeforeImage is null ? null : ReadSnapshot(receipt.Id, file.BeforeImage, file.BeforeBytes, file.BeforeSha256);
        var after = ReadSnapshot(receipt.Id, file.AfterImage, file.AfterBytes, file.AfterSha256);
        var beforeText = before is null ? null : Utf8.GetString(before);
        var afterText = Utf8.GetString(after);
        ValidateSourceText(file.Path, afterText);
        if (beforeText is not null) ValidateSourceText(file.Path, beforeText);
        await Task.CompletedTask.ConfigureAwait(false);
        return new(file.Path, beforeText, afterText);
    }

    /// <summary>Commit the frozen snapshot and push only its unique branch after explicit confirmation.</summary>
    public async Task<GitOutgoingRevision> SendAsync(string operationId, CancellationToken token = default)
    {
        try { return await SendCoreAsync(operationId, token).ConfigureAwait(false); }
        catch (Exception error) when (NeedsSanitizedRefusal(error))
        {
            throw Refusal("Отправка не завершена. Операция и её снимок сохранены в очереди.");
        }
    }

    private async Task<GitOutgoingRevision> SendCoreAsync(string operationId, CancellationToken token)
    {
        ValidateOperationId(operationId);
        var relativeDirectory = OperationRelativeDirectory(operationId);
        using var lease = AcquireOperationLease(operationId);
        token.ThrowIfCancellationRequested();

        var receipt = LoadReceipt(operationId);
        if (receipt.Files.Length == 0) throw Refusal("В этом запросе нет поддерживаемых текстовых файлов для отправки.");
        var hooksRelative = relativeDirectory + "/hooks-empty";
        Directory.CreateDirectory(Absolute(hooksRelative));
        EnsureEmptyHooksDirectory(hooksRelative);
        var endpoint = await PushEndpointAsync(hooksRelative, token).ConfigureAwait(false);
        var currentFingerprint = Fingerprint(endpoint);
        if (!string.Equals(receipt.OriginFingerprint, currentFingerprint, StringComparison.Ordinal))
            throw Refusal("Адрес origin изменился после подготовки. Создайте новый запрос.");

        var objectFormat = await TextAsync(WithHooks(hooksRelative, ["rev-parse", "--show-object-format"]), 32, token).ConfigureAwait(false);
        if (objectFormat is not ("sha1" or "sha256") || receipt.BaseRevision.Length != ObjectHashLength(objectFormat))
            throw Refusal("Формат ревизии сохранённого запроса не совпадает с репозиторием.");
        var files = LoadAndValidateAllFiles(receipt);
        await VerifyCapsuleBaseAsync(receipt, files, objectFormat, hooksRelative, token).ConfigureAwait(false);
        var pinned = await RevisionAsync(WithHooks(hooksRelative,
            ["rev-parse", "--verify", "refs/urman-studio/outbox/" + operationId + "^{commit}"]), token).ConfigureAwait(false);
        if (pinned == receipt.BaseRevision && receipt.CommitRevision is null)
        {
            var (tree, _) = await BuildIsolatedTreeAsync(receipt, files, objectFormat, hooksRelative, token).ConfigureAwait(false);
            var commit = await CreateCommitAsync(receipt, tree, hooksRelative, token).ConfigureAwait(false);
            await VerifyCommitAsync(receipt, commit, objectFormat, hooksRelative, token).ConfigureAwait(false);
            await UpdatePrivateRefAsync(hooksRelative, "refs/urman-studio/outbox/" + operationId, commit,
                receipt.BaseRevision, token).ConfigureAwait(false);
            pinned = commit;
            receipt = receipt with { CommitRevision = commit };
            WriteReceipt(relativeDirectory, receipt);
        }
        else if (receipt.CommitRevision is null)
        {
            await VerifyCommitAsync(receipt, pinned, objectFormat, hooksRelative, token).ConfigureAwait(false);
            receipt = receipt with { CommitRevision = pinned };
            WriteReceipt(relativeDirectory, receipt);
        }
        else if (pinned != receipt.CommitRevision)
        {
            throw Refusal("Локальная сохранённая ревизия не совпадает с закреплённой ревизией.");
        }

        if (receipt.CommitRevision is null) throw Refusal("Не удалось закрепить подготовленную ревизию.");
        await VerifyCommitAsync(receipt, receipt.CommitRevision, objectFormat, hooksRelative, token).ConfigureAwait(false);
        var remote = await RemoteBranchRevisionAsync(endpoint, receipt.RemoteBranch, hooksRelative, token).ConfigureAwait(false);
        if (remote == receipt.CommitRevision)
        {
            receipt = receipt with { State = "Sent" };
            WriteReceipt(relativeDirectory, receipt);
            return Public(receipt);
        }
        if (remote is not null) throw Refusal("Ветка операции уже существует с другой ревизией; она не изменена.");
        if (receipt.State == "Sent") throw Refusal("Ранее отправленная ветка больше не указывает на эту ревизию.");

        try
        {
            await RunGitAsync(WithHooks(hooksRelative,
                ["push", "--no-verify", "--no-follow-tags", "--recurse-submodules=no", endpoint,
                    receipt.CommitRevision + ":refs/heads/" + receipt.RemoteBranch]), 4096, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (token.IsCancellationRequested)
                throw Refusal("Отправка не подтверждена. Операция оставлена в очереди для безопасного повтора.");
            var afterFailure = await RemoteBranchRevisionAsync(endpoint, receipt.RemoteBranch, hooksRelative, CancellationToken.None).ConfigureAwait(false);
            if (afterFailure == receipt.CommitRevision)
            {
                receipt = receipt with { State = "Sent" };
                WriteReceipt(relativeDirectory, receipt);
                return Public(receipt);
            }
            if (afterFailure is not null) throw Refusal("Ветка операции занята другой ревизией; исход операции сохранён в очереди.");
            throw Refusal("Отправка не подтверждена. Операция оставлена в очереди для безопасного повтора.");
        }

        var verified = await RemoteBranchRevisionAsync(endpoint, receipt.RemoteBranch, hooksRelative, token).ConfigureAwait(false);
        if (verified != receipt.CommitRevision)
            throw Refusal("Git завершил отправку, но точная ревизия ветки не подтверждена.");
        receipt = receipt with { State = "Sent" };
        WriteReceipt(relativeDirectory, receipt);
        return Public(receipt);
    }

    /// <summary>Move a verified sent capsule out of the active queue without deleting its files or Git ref.</summary>
    public void ArchiveSent(string operationId)
    {
        try
        {
            ValidateOperationId(operationId);
            using var lease = AcquireOperationLease(operationId);
            var active = OperationRelativeDirectory(operationId);
            var archived = ArchivedOperationRelativeDirectory(operationId);
            EnsureMetadataPath(active);
            if (!Directory.Exists(Absolute(active))) throw Refusal("Запрос уже отсутствует в активной очереди.");
            var receipt = LoadReceipt(operationId);
            if (receipt.State != "Sent" || receipt.CommitRevision is null)
                throw Refusal("В активную очередь можно убрать только подтверждённо отправленный запрос.");
            _ = LoadAndValidateAllFiles(receipt);

            EnsureMetadataPath(ArchivePath);
            Directory.CreateDirectory(Absolute(ArchivePath));
            EnsureMetadataPath(ArchivePath);
            EnsureMetadataPath(archived);
            if (Directory.Exists(Absolute(archived)) || File.Exists(Absolute(archived)))
                throw Refusal("Архив уже содержит этот запрос; существующие данные не изменены.");
            Directory.Move(Absolute(active), Absolute(archived));
        }
        catch (Exception error) when (NeedsSanitizedRefusal(error))
        {
            throw Refusal("Архивирование не завершено. Запрос и его снимок сохранены.");
        }
    }

    /// <summary>Load bounded local queue metadata only; this performs no Git command or network request.</summary>
    public IReadOnlyList<GitOutgoingRevision> List()
    {
        try { return ListCore(); }
        catch (Exception error) when (NeedsSanitizedRefusal(error))
        {
            throw Refusal("Очередь исходящих операций повреждена или недоступна. Сохранённые данные не изменены.");
        }
    }

    private IReadOnlyList<GitOutgoingRevision> ListCore()
    {
        EnsureMetadataPath(OutboxPath);
        var root = Absolute(OutboxPath);
        if (!Directory.Exists(root)) return Array.Empty<GitOutgoingRevision>();
        var directories = Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal).Take(MaxOperations + 1).ToArray();
        if (directories.Length > MaxOperations) throw Refusal("Очередь исходящих операций превышает предел.");
        var items = new List<GitOutgoingRevision>();
        var listedBytes = 0L;
        foreach (var directory in directories)
        {
            var id = Path.GetFileName(directory);
            if (!Guid.TryParseExact(id, "N", out _)) continue;
            var receiptPath = OperationRelativeDirectory(id) + "/receipt.json";
            EnsureMetadataPath(receiptPath);
            if (!File.Exists(Absolute(receiptPath))) continue;
            var receiptLength = new FileInfo(Absolute(receiptPath)).Length;
            listedBytes += receiptLength;
            if (listedBytes > ListedReceiptBudget) throw Refusal("Метаданные очереди превышают лимит чтения.");
            items.Add(Public(LoadReceipt(id)));
        }
        return items.AsReadOnly();
    }

    private void EnsureOutboxCapacity()
    {
        EnsureMetadataPath(OutboxPath);
        var root = Absolute(OutboxPath);
        if (Directory.Exists(root) && Directory.EnumerateDirectories(root).Take(MaxOperations).Count() >= MaxOperations)
            throw Refusal("Очередь исходящих операций заполнена. Отправьте или разберите существующие запросы.");
    }

    private async Task<(string Tree, IReadOnlyDictionary<string, string> ObjectIds)> BuildIsolatedTreeAsync(
        Receipt receipt, IReadOnlyList<PendingFile> files, string objectFormat, string hooksRelative, CancellationToken token)
    {
        var indexRelative = OperationRelativeDirectory(receipt.Id) + "/index";
        EnsureMetadataPath(indexRelative);
        var indexPath = Absolute(indexRelative);
        if (File.Exists(indexPath)) File.Delete(indexPath);
        var environment = new Dictionary<string, string?> { ["GIT_INDEX_FILE"] = indexPath };
        await RunGitAsync(WithHooks(hooksRelative, ["read-tree", receipt.BaseRevision]), 4096, token, environment: environment).ConfigureAwait(false);
        var objects = new Dictionary<string, string>(StringComparer.Ordinal);
        using var indexInfo = new MemoryStream();
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var objectBytes = await RunGitAsync(WithHooks(hooksRelative, ["hash-object", "-w", "--no-filters", "--stdin"]),
                256, token, file.After, environment).ConfigureAwait(false);
            var objectId = Utf8.GetString(objectBytes).Trim();
            var expectedId = GitBlobHash(objectFormat, file.After);
            if (!string.Equals(objectId, expectedId, StringComparison.Ordinal)) throw Refusal("Исходник не прошёл проверку Git blob.");
            objects.Add(file.Path, objectId);
            var record = Utf8.GetBytes("100644 blob " + objectId + "\t" + file.Path + "\0");
            indexInfo.Write(record);
        }
        await RunGitAsync(WithHooks(hooksRelative, ["update-index", "-z", "--index-info"]),
            4096, token, indexInfo.ToArray(), environment).ConfigureAwait(false);
        var tree = await RevisionAsync(WithHooks(hooksRelative, ["write-tree"]), token, environment).ConfigureAwait(false);
        return (tree, objects);
    }

    private async Task<string> CreateCommitAsync(Receipt receipt, string tree, string hooksRelative, CancellationToken token)
    {
        var bytes = await RunGitAsync(WithHooks(hooksRelative,
            ["commit-tree", tree, "-p", receipt.BaseRevision, "-m", "URMAN authored text outbox " + receipt.Id]), 4096, token)
            .ConfigureAwait(false);
        var commit = Utf8.GetString(bytes).Trim();
        if (!GitRevisionInbox.IsHash(commit)) throw Refusal("Git не вернул ревизию коммита.");
        return commit;
    }

    private async Task VerifyCommitAsync(Receipt receipt, string commit, string objectFormat, string hooksRelative, CancellationToken token)
    {
        ValidateHash(commit, objectFormat);
        var parents = (await TextAsync(WithHooks(hooksRelative, ["rev-list", "--parents", "-n", "1", commit]), 4096, token)
            .ConfigureAwait(false)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parents.Length != 2 || parents[0] != commit || parents[1] != receipt.BaseRevision)
            throw Refusal("Сохранённый коммит не является дочерней ревизией выбранной основы.");
        var changed = NulFields(await RunGitAsync(WithHooks(hooksRelative,
            ["diff-tree", "--no-commit-id", "-r", "--name-only", "-z", "--no-renames", receipt.BaseRevision, commit, "--"]),
            PathBudget, token).ConfigureAwait(false)).Order(StringComparer.Ordinal).ToArray();
        var expectedPaths = receipt.Files.Select(file => file.Path).Order(StringComparer.Ordinal).ToArray();
        if (!changed.SequenceEqual(expectedPaths, StringComparer.Ordinal))
            throw Refusal("В сохранённом коммите обнаружены пути вне утверждённого набора исходников.");
        var entries = await ReadTreeEntriesAsync(commit, expectedPaths, hooksRelative, token).ConfigureAwait(false);
        if (entries.Count != receipt.Files.Length) throw Refusal("Сохранённый коммит не содержит весь утверждённый набор исходников.");
        foreach (var file in receipt.Files)
        {
            var entry = entries.GetValueOrDefault(file.Path);
            if (entry is null || entry.Mode != "100644" || entry.Type != "blob" || entry.ObjectId != file.AfterObjectId)
                throw Refusal("Сохранённый коммит не совпадает с замороженным текстовым preview.");
        }
    }

    private async Task VerifyCapsuleBaseAsync(Receipt receipt, IReadOnlyList<PendingFile> files, string objectFormat,
        string hooksRelative, CancellationToken token)
    {
        var paths = receipt.Files.Select(file => file.Path).ToArray();
        var entries = await ReadTreeEntriesAsync(receipt.BaseRevision, paths, hooksRelative, token).ConfigureAwait(false);
        for (var index = 0; index < receipt.Files.Length; index++)
        {
            var metadata = receipt.Files[index];
            var snapshot = files[index];
            if (metadata.BeforeImage is null)
            {
                if (entries.ContainsKey(metadata.Path))
                    throw Refusal("Добавленный исходник уже есть в выбранной базовой ревизии.");
                continue;
            }
            if (snapshot.Before is null || !entries.TryGetValue(metadata.Path, out var entry)
                || entry.Mode != "100644" || entry.Type != "blob" || entry.ObjectId != metadata.BaseObjectId
                || GitBlobHash(objectFormat, snapshot.Before) != entry.ObjectId)
                throw Refusal("Замороженный исходник не совпадает с выбранной базовой ревизией.");
        }
    }

    private async Task<string?> RemoteBranchRevisionAsync(string endpoint, string remoteBranch, string hooksRelative, CancellationToken token)
    {
        var bytes = await RunGitAsync(WithHooks(hooksRelative, ["ls-remote", "--refs", endpoint, "refs/heads/" + remoteBranch]),
            4096, token).ConfigureAwait(false);
        var output = Utf8.GetString(bytes).TrimEnd('\r', '\n');
        if (output.Length == 0) return null;
        var lines = output.Split('\n');
        if (lines.Length != 1) throw Refusal("Git origin вернул неоднозначный ответ для ветки операции.");
        var fields = lines[0].TrimEnd('\r').Split('\t');
        if (fields.Length != 2 || fields[1] != "refs/heads/" + remoteBranch || !GitRevisionInbox.IsHash(fields[0]))
            throw Refusal("Git origin вернул некорректный ответ для ветки операции.");
        return fields[0];
    }

    private async Task<string> PushEndpointAsync(string hooksRelative, CancellationToken token)
    {
        var push = await TextAsync(WithHooks(hooksRelative, ["remote", "get-url", "--push", "--all", "origin"]), 32 * 1024, token)
            .ConfigureAwait(false);
        var endpoints = push.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.TrimEnd('\r')).ToArray();
        if (endpoints.Length != 1 || string.IsNullOrWhiteSpace(endpoints[0])
            || endpoints[0].StartsWith("-", StringComparison.Ordinal) || endpoints[0].Any(char.IsControl))
            throw Refusal("Для очереди нужен настроенный Git origin.");
        return endpoints[0];
    }

    private static string Fingerprint(string endpoint) => AtomicFile.Sha256(Utf8.GetBytes(endpoint));

    private async Task<string> TextAsync(IReadOnlyList<string> arguments, int limit, CancellationToken token,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var bytes = await RunGitAsync(arguments, limit, token, environment: environment).ConfigureAwait(false);
        return Utf8.GetString(bytes).Trim();
    }

    private async Task<string> RevisionAsync(IReadOnlyList<string> arguments, CancellationToken token,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var value = await TextAsync(arguments, 4096, token, environment).ConfigureAwait(false);
        if (!GitRevisionInbox.IsHash(value)) throw Refusal("Git не вернул допустимую ревизию.");
        return value;
    }

    private Task<byte[]> RunGitAsync(IReadOnlyList<string> arguments, int outputLimit, CancellationToken token,
        byte[]? standardInput = null, IReadOnlyDictionary<string, string?>? environment = null) =>
        GitRepositoryProcess.RunAsync(workspace.Root, arguments, outputLimit, token, standardInput, environment);

    private async Task<Dictionary<string, TreeEntry>> ReadBaseTreeAsync(string revision, CancellationToken token) =>
        await ReadTreeEntriesAsync(revision,
            ["content/modules", "content/campaigns", "content/studio", "game/content/world", "game/content/cutscenes",
                "game/content/animations", "game/content/studio/hero_house.recipe.json"], null, token).ConfigureAwait(false);

    private async Task<Dictionary<string, TreeEntry>> ReadTreeEntriesAsync(string revision, IReadOnlyList<string>? paths,
        string? hooksRelative, CancellationToken token)
    {
        var args = new List<string> { "ls-tree", "-r", "-z", "--full-tree", revision, "--" };
        if (paths is not null) args.AddRange(paths);
        var command = hooksRelative is null ? args : WithHooks(hooksRelative, args);
        var records = NulFields(await RunGitAsync(command, PathBudget, token).ConfigureAwait(false));
        var entries = new Dictionary<string, TreeEntry>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var tab = record.IndexOf('\t');
            if (tab < 0) throw Refusal("Git вернул некорректное дерево исходников.");
            var fields = record[..tab].Split(' ');
            var path = record[(tab + 1)..];
            ValidatePath(path);
            if (fields.Length != 3 || !GitRevisionInbox.IsHash(fields[2])
                || !entries.TryAdd(path, new(fields[0], fields[1], fields[2])))
                throw Refusal("Git вернул неоднозначное дерево исходников.");
        }
        return entries;
    }

    private async Task<byte[]> ReadBlobAsync(string objectId, CancellationToken token) =>
        await RunGitAsync(["cat-file", "blob", objectId], ContentBudget, token).ConfigureAwait(false);

    private async Task<string[]> ChangedPathsAsync(string baseRevision, CancellationToken token)
    {
        var tracked = NulFields(await RunGitAsync(
            ["diff", "--no-ext-diff", "--no-textconv", "--name-only", "-z", "--no-renames", baseRevision, "--"],
            PathBudget, token).ConfigureAwait(false));
        var untracked = NulFields(await RunGitAsync(
            ["ls-files", "--others", "--exclude-standard", "-z"], PathBudget, token).ConfigureAwait(false));
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in tracked.Concat(untracked))
        {
            ValidatePath(path);
            if (!path.StartsWith(".urman-studio/", StringComparison.Ordinal)) paths.Add(path);
        }
        if (paths.Count > MaxFiles) throw Refusal("Слишком много изменённых и неподдерживаемых путей для безопасного preview.");
        return paths.ToArray();
    }

    private List<PendingFile> LoadAndValidateAllFiles(Receipt receipt)
    {
        var files = new List<PendingFile>(receipt.Files.Length);
        foreach (var item in receipt.Files)
        {
            var before = item.BeforeImage is null ? null : ReadSnapshot(receipt.Id, item.BeforeImage, item.BeforeBytes, item.BeforeSha256);
            var after = ReadSnapshot(receipt.Id, item.AfterImage, item.AfterBytes, item.AfterSha256);
            var beforeText = before is null ? null : Utf8.GetString(before);
            var afterText = Utf8.GetString(after);
            ValidateSourceText(item.Path, afterText);
            if (beforeText is not null) ValidateSourceText(item.Path, beforeText);
            files.Add(new(item.Path, before, item.BaseObjectId, after));
        }
        return files;
    }

    private byte[] ReadSnapshot(string id, string image, int expectedBytes, string? expectedSha256)
    {
        if (!IsImageName(image)) throw Refusal("В очереди указан недопустимый образ исходника.");
        var relative = OperationRelativeDirectory(id) + "/" + image;
        EnsureMetadataPath(relative);
        var bytes = StudioFileTransaction.ReadBoundedFile(Absolute(relative), ContentBudget);
        if (bytes.Length != expectedBytes || expectedSha256 is null
            || !string.Equals(AtomicFile.Sha256(bytes), expectedSha256, StringComparison.Ordinal))
            throw Refusal("Замороженный образ исходника повреждён. Очередь сохранена без изменений.");
        return bytes;
    }

    private Receipt LoadReceipt(string id)
    {
        ValidateOperationId(id);
        var relativeDirectory = OperationRelativeDirectory(id);
        var relativeReceipt = relativeDirectory + "/receipt.json";
        EnsureMetadataPath(relativeReceipt);
        var bytes = StudioFileTransaction.ReadBoundedFile(Absolute(relativeReceipt), ReceiptBudget);
        var receipt = JsonSerializer.Deserialize<Receipt>(bytes, JsonOptions) ?? throw Refusal("Некорректная запись очереди.");
        ValidateReceipt(receipt, id);
        var actualBytes = 0L;
        foreach (var file in receipt.Files)
        {
            if (file.BeforeImage is not null) ValidateSnapshotMetadata(id, file.BeforeImage, file.BeforeBytes);
            ValidateSnapshotMetadata(id, file.AfterImage, file.AfterBytes);
            actualBytes += file.BeforeBytes + (long)file.AfterBytes;
            if (actualBytes > ContentBudget) throw Refusal("Запись очереди превышает лимит исходников.");
        }
        return receipt;
    }

    private void ValidateReceipt(Receipt receipt, string id)
    {
        if (receipt.Version != 1 || receipt.Id != id || receipt.Files is null || receipt.ExcludedPaths is null
            || receipt.Files.Length > MaxFiles || receipt.ExcludedPaths.Length > MaxFiles
            || receipt.BaseRevision is null || !GitRevisionInbox.IsHash(receipt.BaseRevision)
            || receipt.CommitRevision is { } commit && !GitRevisionInbox.IsHash(commit)
            || receipt.CommitRevision is { } savedCommit && savedCommit.Length != receipt.BaseRevision.Length
            || !IsSha256(receipt.OriginFingerprint) || receipt.State is not ("Pending" or "Sent")
            || receipt.State == "Sent" && receipt.CommitRevision is null
            || receipt.Branch is null || receipt.RemoteBranch != "codex/studio-" + id)
            throw Refusal("Запись очереди не прошла проверку структуры. Файлы не изменены.");
        GitRevisionInbox.ValidateBranch(receipt.Branch);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in receipt.Files)
        {
            if (file is null || file.Path is null) throw Refusal("Запись очереди содержит пустой исходник.");
            ValidateSourcePath(file.Path);
            if (!seen.Add(file.Path) || file.AfterImage is null || file.AfterBytes < 0 || file.BeforeBytes < 0
                || file.BeforeImage is null && (file.BeforeBytes != 0 || file.BeforeSha256 is not null || file.BaseObjectId is not null)
                || file.BeforeImage is not null && (file.BeforeBytes <= 0 || !IsSha256(file.BeforeSha256)
                    || file.BaseObjectId is null || !GitRevisionInbox.IsHash(file.BaseObjectId)
                    || file.BaseObjectId.Length != receipt.BaseRevision.Length)
                || file.AfterBytes <= 0 || !IsSha256(file.AfterSha256) || !GitRevisionInbox.IsHash(file.AfterObjectId)
                || file.AfterObjectId.Length != receipt.BaseRevision.Length)
                throw Refusal("Запись очереди содержит недопустимый исходник. Файлы не изменены.");
            var ordinal = Array.IndexOf(receipt.Files, file).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            if (file.BeforeImage is not null && file.BeforeImage != "before-" + ordinal + ".txt"
                || file.AfterImage != "after-" + ordinal + ".txt")
                throw Refusal("Запись очереди содержит неверное имя образа исходника.");
        }
        foreach (var path in receipt.ExcludedPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > PathBudget) throw Refusal("Запись очереди содержит недопустимый исключённый путь.");
        }
        if (Encoding.UTF8.GetByteCount(string.Join('\0', receipt.ExcludedPaths.Concat(receipt.Files.Select(file => file.Path)))) > PathBudget)
            throw Refusal("Список исключённых путей превышает допустимый размер.");
    }

    private void ValidateSnapshotMetadata(string id, string image, int expectedBytes)
    {
        if (!IsImageName(image) || expectedBytes < 0 || expectedBytes > ContentBudget) throw Refusal("Недопустимый образ очереди.");
        var relative = OperationRelativeDirectory(id) + "/" + image;
        EnsureMetadataPath(relative);
        var info = new FileInfo(Absolute(relative));
        if (!info.Exists || info.Length != expectedBytes) throw Refusal("Образ очереди отсутствует или изменён. Он сохранён без перезаписи.");
    }

    private void WriteReceipt(string relativeDirectory, Receipt receipt)
        => WriteReceipt(relativeDirectory, receipt, createOnly: false);

    private void WriteReceipt(string relativeDirectory, Receipt receipt, bool createOnly)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions);
        if (bytes.Length > ReceiptBudget) throw Refusal("Запись очереди превышает допустимый размер.");
        var relative = relativeDirectory + "/receipt.json";
        EnsureMetadataPath(relative);
        AtomicFile.WriteAllBytes(Absolute(relative), bytes, overwrite: !createOnly);
    }

    private void WritePrivateFile(string relative, byte[] bytes)
    {
        EnsureMetadataPath(relative);
        AtomicFile.WriteAllBytes(Absolute(relative), bytes, overwrite: false);
    }

    private byte[]? ReadOptionalHeroRecipe()
    {
        StudioFileTransaction.EnsureNoLinks(workspace.Root, HeroRecipePath);
        var path = Absolute(HeroRecipePath);
        if (!File.Exists(path)) return null;
        return StudioFileTransaction.ReadBoundedFile(path, StudioFileTransaction.MaximumAuthoredSourceBytes);
    }

    private void RevalidateSources(IReadOnlyDictionary<string, SourceCapture> captured)
    {
        var current = workspace.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        if (current.Count != captured.Count) throw Refusal("Состав зарегистрированных исходников изменился во время подготовки.");
        foreach (var (path, original) in captured)
        {
            if (!current.TryGetValue(path, out var file) || !ReferenceEquals(file, original.File))
                throw Refusal("Состав зарегистрированных исходников изменился во время подготовки.");
            var latest = file.CaptureSource();
            if (latest.Dirty || latest.IsNew != original.Source.IsNew || latest.Sha256 != original.Source.Sha256
                || latest.BaseSha256 != original.Source.BaseSha256 || latest.Text != original.Source.Text)
                throw Refusal("Текстовый исходник изменился во время подготовки. Обновите сравнение.");
        }
    }

    private async Task UpdatePrivateRefAsync(string hooksRelative, string reference, string target, string expected, CancellationToken token)
    {
        await RunGitAsync(WithHooks(hooksRelative, ["update-ref", "--no-deref", reference, target, expected]),
            4096, token).ConfigureAwait(false);
    }

    private IReadOnlyList<string> WithHooks(string hooksRelative, IReadOnlyList<string> command)
    {
        // update-ref can invoke reference-transaction hooks; each Git mutation uses this owned empty directory,
        // while push also passes --no-verify to disable its pre-push hook.
        EnsureEmptyHooksDirectory(hooksRelative);
        var hooksPath = Absolute(hooksRelative).Replace('\\', '/');
        var result = new List<string>(command.Count + 4)
        {
            "-c", "core.hooksPath=" + hooksPath,
            "-c", "commit.gpgsign=false"
        };
        result.AddRange(command);
        return result;
    }

    private void EnsureEmptyHooksDirectory(string hooksRelative)
    {
        EnsureMetadataPath(hooksRelative);
        Directory.CreateDirectory(Absolute(hooksRelative));
        EnsureMetadataPath(hooksRelative);
        if (Directory.EnumerateFileSystemEntries(Absolute(hooksRelative)).Any())
            throw Refusal("Внутренний каталог отключённых Git hooks изменён. Операция остановлена.");
    }

    private void EnsureMetadataPath(string relative) =>
        StudioFileTransaction.EnsureNoLinks(workspace.Root, relative, allowStudioMetadata: true);

    private FileStream AcquireOperationLease(string id)
    {
        ValidateOperationId(id);
        EnsureMetadataPath(OperationLocksPath);
        Directory.CreateDirectory(Absolute(OperationLocksPath));
        EnsureMetadataPath(OperationLocksPath);
        var relative = OperationLocksPath + "/" + id + ".lock";
        EnsureMetadataPath(relative);
        try
        {
            return new FileStream(Absolute(relative), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            throw Refusal("Этот запрос сейчас обрабатывает другое окно Studio.");
        }
    }

    private string OperationDirectory(string id) => Absolute(OperationRelativeDirectory(id));
    private static string OperationRelativeDirectory(string id) => OutboxPath + "/" + id;
    private static string ArchivedOperationRelativeDirectory(string id) => ArchivePath + "/" + id;
    private string Absolute(string relative) => Path.Combine(workspace.Root, relative.Replace('/', Path.DirectorySeparatorChar));
    private bool RepositoryExists() => Directory.Exists(Absolute(".git")) || File.Exists(Absolute(".git"));

    private static void AddPending(string path, byte[]? before, string? baseObjectId, byte[] after, string objectFormat,
        IDictionary<string, PendingFile> candidates, ref long totalBytes)
    {
        if (after.Length == 0) throw Refusal("Пустые текстовые источники не передаются.");
        totalBytes += after.LongLength + (before?.LongLength ?? 0);
        if (totalBytes > ContentBudget || candidates.Count >= MaxFiles) throw Refusal("Подготовленные исходники превышают лимит 8 MiB или 2000 файлов.");
        if (before is not null && (baseObjectId is null || GitBlobHash(objectFormat, before) != baseObjectId))
            throw Refusal("Базовый текстовый образ не совпадает с выбранной ревизией.");
        candidates.Add(path, new(path, before, baseObjectId, after));
    }

    private async Task ValidateIntroducedResourceReferencesAsync(string baseRevision,
        IDictionary<string, TreeEntry> baseTree, IReadOnlyDictionary<string, PendingFile> candidates,
        CancellationToken token)
    {
        foreach (var (sourcePath, file) in candidates.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if (!sourcePath.EndsWith(".json", StringComparison.Ordinal)) continue;

            var beforeReferences = new HashSet<string>(StringComparer.Ordinal);
            if (file.Before is not null)
            {
                using var before = JsonDocument.Parse(Utf8.GetString(file.Before));
                CollectResourceReferences(before.RootElement, beforeReferences);
            }

            using var after = JsonDocument.Parse(Utf8.GetString(file.After));
            var afterReferences = new HashSet<string>(StringComparer.Ordinal);
            CollectResourceReferences(after.RootElement, afterReferences);
            foreach (var reference in afterReferences.Except(beforeReferences, StringComparer.Ordinal))
            {
                var relative = ResourcePath(reference);
                if (relative is null) throw Refusal("JSON-исходник ссылается на путь ресурса вне безопасной области.");
                StudioFileTransaction.EnsureNoLinks(workspace.Root, relative);
                if (candidates.ContainsKey(relative)) continue;

                if (!baseTree.TryGetValue(relative, out var entry))
                {
                    entry = (await ReadTreeEntriesAsync(baseRevision, [relative], null, token).ConfigureAwait(false))
                        .GetValueOrDefault(relative);
                    if (entry is not null) baseTree[relative] = entry;
                }
                if (entry is null || entry.Type != "blob" || entry.Mode is not ("100644" or "100755"))
                    throw Refusal("JSON-исходник ссылается на ресурс, которого нет в выбранной базовой ревизии или который не входит в текстовый пакет.");
            }
        }
    }

    private static void CollectResourceReferences(JsonElement element, ISet<string> references)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) CollectResourceReferences(property.Value, references);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectResourceReferences(item, references);
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (value is not null && value.StartsWith("res://", StringComparison.Ordinal)
                    && !value.EndsWith("/", StringComparison.Ordinal)) references.Add(value);
                break;
        }
    }

    private static string? ReadModelPath(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("modelPath", out var property))
            throw Refusal("У рецепта дома отсутствует поле modelPath.");
        if (property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.String) throw Refusal("Поле modelPath рецепта некорректно.");
        return property.GetString();
    }

    private static string? ResourcePath(string value)
    {
        const string prefix = "res://";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var path = "game/" + value[prefix.Length..];
        try { ValidatePath(path); }
        catch (IOException) { return null; }
        catch (ArgumentException) { return null; }
        return path;
    }

    private static void ValidateSourceText(string path, string text)
    {
        ValidateSourcePath(path);
        _ = AuthoredFile.CreateNew(".", path, text).Snapshot();
    }

    private static void ValidateSourcePath(string path)
    {
        if (path != HeroRecipePath && !StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(path)
            && !(path.StartsWith("content/modules/", StringComparison.Ordinal)
                && path.EndsWith(".md", StringComparison.Ordinal) && SafeSourceRelativePath(path)))
            throw Refusal("К очереди допускаются только зарегистрированные текстовые исходники.");
    }

    private static bool SafeSourceRelativePath(string path) => !Path.IsPathRooted(path)
        && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl)
        && path.Split('/').All(part => part.Length > 0 && part is not "." and not "..");

    private static void ValidatePath(string path)
    {
        if (!SafeSourceRelativePath(path)) throw Refusal("Git вернул путь, который нельзя безопасно обработать.");
    }

    private static bool IsImageName(string value) =>
        value.Length is 14 or 15 && (value.StartsWith("before-", StringComparison.Ordinal) || value.StartsWith("after-", StringComparison.Ordinal))
        && value.EndsWith(".txt", StringComparison.Ordinal)
        && value.SkipWhile(character => character != '-').Skip(1).Take(4).All(char.IsAsciiDigit);

    private static void ValidateOperationId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw Refusal("Недопустимый номер операции.");
    }

    private static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool NeedsSanitizedRefusal(Exception error) => error is not OutboxRefusalException
        && (error is IOException or JsonException or NotSupportedException or NullReferenceException
            or ArgumentException or InvalidOperationException or FormatException or OverflowException
            or UnauthorizedAccessException);

    private static int ObjectHashLength(string format) => format == "sha1" ? 40 : format == "sha256" ? 64 : 0;

    private static void ValidateHash(string value, string format)
    {
        if (value.Length != ObjectHashLength(format) || !GitRevisionInbox.IsHash(value))
            throw Refusal("Неверная длина Git-ревизии.");
    }

    private static string GitBlobHash(string format, byte[] bytes)
    {
        using var hash = IncrementalHash.CreateHash(format == "sha1" ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0"));
        hash.AppendData(bytes);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string[] NulFields(byte[] bytes)
    {
        if (bytes.Length == 0) return [];
        var text = Utf8.GetString(bytes);
        if (!text.EndsWith('\0')) throw Refusal("Git вернул неполный список путей.");
        return text[..^1].Split('\0');
    }

    private static bool SameBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private GitOutgoingRevision Public(Receipt receipt) => new(receipt.Id, receipt.BaseRevision, receipt.CommitRevision,
        receipt.Branch, receipt.RemoteBranch,
        receipt.Files.Select(file => new GitOutgoingFile(file.Path, file.BeforeSha256, file.AfterSha256,
            checked(file.BeforeBytes + file.AfterBytes))).ToArray(),
        receipt.State == "Sent" ? GitOutgoingState.Sent : GitOutgoingState.Pending, receipt.ExcludedPaths.ToArray());

    private static IOException Refusal(string message) => new OutboxRefusalException(message);

    private sealed class OutboxRefusalException(string message) : IOException(message);
    private sealed record SourceCapture(AuthoredFile File, AuthoredSourceSnapshot Source);
    private sealed record TreeEntry(string Mode, string Type, string ObjectId);
    private sealed record PendingFile(string Path, byte[]? Before, string? BaseObjectId, byte[] After);
    private sealed record CapsuleFile(string Path, string? BeforeImage, string AfterImage, int BeforeBytes, int AfterBytes,
        string? BeforeSha256, string? AfterSha256, string? BaseObjectId, string AfterObjectId);
    private sealed record Receipt(int Version, string Id, string BaseRevision, string? CommitRevision, string Branch,
        string RemoteBranch, string OriginFingerprint, string State, CapsuleFile[] Files, string[] ExcludedPaths);
}
