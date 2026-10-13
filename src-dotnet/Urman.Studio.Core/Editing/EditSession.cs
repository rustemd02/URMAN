using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Editing;

/// <summary>One entity before and after an edit. A null value means "absent" (created or deleted).</summary>
public sealed record EntityChange(string RelativePath, string Key, JsonNode? Before, JsonNode? After)
{
    public bool SourceText { get; init; }
}

/// <summary>A user-visible undo step: a drag, a brush stroke or a composite action is one command (SAVE01).</summary>
public sealed record EditCommand(string Label, IReadOnlyList<EntityChange> Changes, StudioFileTransaction? Files = null);

/// <summary>Read-only display metadata; command snapshots and journal images remain private.</summary>
public sealed record EditHistoryStep(string Label, IReadOnlyList<string> Paths, int EntityChangeCount, string? TransactionId)
{
    public string? TransactionOwner { get; init; }
}

/// <summary>
/// Undo/redo would overwrite something that changed since this command — most
/// often another author's merged work (SAVE05). Nothing was applied.
/// </summary>
public sealed class UndoConflictException : InvalidOperationException
{
    public UndoConflictException(string label, IReadOnlyList<EntityChange> blocked, IReadOnlyList<string>? blockedFiles = null)
        : base($"«{label}» нельзя отменить автоматически: эти объекты уже изменены после неё."
            + ((blockedFiles?.Count ?? 0) == 0 ? "" : $" Изменены файлы: {string.Join(", ", blockedFiles!)}."))
    {
        Blocked = blocked;
        BlockedFiles = blockedFiles ?? [];
    }

    public IReadOnlyList<EntityChange> Blocked { get; }
    public IReadOnlyList<string> BlockedFiles { get; }
}

/// <summary>
/// Typed edits over a <see cref="StudioWorkspace"/> with undo and redo. Every
/// edit is recorded as entity snapshots; undo only reverts an entity that still
/// holds the value this command produced, so undoing your own step never rolls
/// back someone else's work that arrived afterwards.
/// </summary>
public sealed class EditSession(StudioWorkspace workspace)
{
    private readonly Stack<EditCommand> _undo = new();
    private readonly Stack<EditCommand> _redo = new();
    private List<EntityChange>? _open;
    private string? _openLabel;
    private PendingFiles? _openFiles;
    private bool _asyncOperationBusy;

    public StudioWorkspace Workspace { get; } = workspace;
    public bool CanUndo => !_asyncOperationBusy && _undo.Count > 0;
    public bool CanRedo => !_asyncOperationBusy && _redo.Count > 0;
    public bool IsBusy => _asyncOperationBusy;
    public string? UndoLabel => _undo.TryPeek(out var command) ? command.Label : null;
    public string? RedoLabel => _redo.TryPeek(out var command) ? command.Label : null;
    public event Action? Changed;

    /// <summary>Next action first, using the actual stack order rather than journal names or timestamps.</summary>
    public IReadOnlyList<EditHistoryStep> History(bool redo = false) => (redo ? _redo : _undo)
        .Select(command => new EditHistoryStep(command.Label,
            command.Changes.Select(change => change.RelativePath).Concat(command.Files?.RelativePaths ?? [])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            command.Changes.Count, command.Files?.Id) { TransactionOwner = command.Files?.Owner }).ToArray();

    /// <summary>Group several edits (a drag, a template) into one undo step.</summary>
    public IDisposable Begin(string label)
    {
        EnsureAvailable();
        if (_open is not null)
        {
            return new Scope(() => { });
        }

        _open = [];
        _openLabel = label;
        return new Scope(() =>
        {
            CompleteOpen();
        });
    }

    /// <summary>
    /// Pilot-only shortcut: restore at most one standalone, file-only HeroHouse command.
    /// Entity edits and mixed command order remain session-only and are not inferred here.
    /// </summary>
    public string? RestoreSingleCommittedFileCommand(
        string label,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet)
    {
        EnsureAvailable();
        if (_open is not null || _undo.Count != 0 || _redo.Count != 0)
            throw new InvalidOperationException("Persistent pilot history can only be restored into a new, empty edit session.");

        var result = StudioFileTransaction.LoadSingleCommittedFileCommand(
            Workspace.Root, owner, allowedPath, allowedFileSet);
        if (result.Transaction is not { } transaction) return result.Warning;

        var command = new EditCommand(label, [], transaction);
        if (transaction.IsAtAfterSide)
            _undo.Push(command);
        else
            _redo.Push(command);

        Changed?.Invoke();
        return result.Warning;
    }

    /// <summary>Restore the explicitly ordered file-only stack; unsupported mixed history remains fenced.</summary>
    public string? RestoreCommittedFileHistory(
        string legacyLabel, string owner, Func<string, bool> allowedPath, Func<IReadOnlyList<string>, bool> allowedFileSet)
    {
        EnsureAvailable();
        if (_open is not null || _undo.Count != 0 || _redo.Count != 0)
            throw new InvalidOperationException("Persistent history requires an empty edit session.");
        var result = StudioFileTransaction.LoadCommittedFileHistory(Workspace.Root, owner, allowedPath, allowedFileSet);
        return RestoreCommittedFileHistoryResult(legacyLabel, result);
    }

    public string? RestoreCommittedFileHistory(
        string legacyLabel,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet,
        Func<string, bool> sourceDeletionOwner,
        Func<string, bool> sourceDeletionPath,
        bool includeOwnedCatalogs = false,
        bool includeHouseFamilies = false,
        bool includeFenceAlignments = false,
        bool includeRoadSmooth = false,
        Func<string, bool>? sourceCreationOwner = null,
        Func<string, bool>? sourceCreationPath = null)
    {
        EnsureAvailable();
        if (_open is not null || _undo.Count != 0 || _redo.Count != 0)
            throw new InvalidOperationException("Persistent history requires an empty edit session.");
        if ((sourceCreationOwner is null) != (sourceCreationPath is null))
            throw new ArgumentException("Authored-source creation history requires both owner and path policies.");

        var result = StudioFileTransaction.LoadCommittedFileHistoryWithAuthoredSourceDeletions(
            Workspace.Root, owner, allowedPath, allowedFileSet, sourceDeletionOwner, sourceDeletionPath,
            (path, beforeBytes, beforeSha256) => () =>
                Workspace.ReconcileRecoveredAuthoredJsonSource(path, beforeBytes, beforeSha256),
            includeOwnedCatalogs,
            includeHouseFamilies,
            includeFenceAlignments, includeRoadSmooth,
            sourceCreationOwner,
            sourceCreationPath,
            sourceCreationOwner is null ? null : (path, afterBytes, afterSha256) => () =>
                Workspace.ReconcileCreatedAuthoredJsonSource(path, afterBytes, afterSha256));
        return RestoreCommittedFileHistoryResult(legacyLabel, result);
    }

    private string? RestoreCommittedFileHistoryResult(string legacyLabel, StudioFileHistoryStackLoadResult result)
    {
        for (var index = 0; index < result.Cursor; index++)
        {
            var transaction = result.Transactions[index];
            _undo.Push(new(transaction.CommandLabel ?? legacyLabel, [], transaction));
        }
        for (var index = result.Transactions.Count - 1; index >= result.Cursor; index--)
        {
            var transaction = result.Transactions[index];
            _redo.Push(new(transaction.CommandLabel ?? legacyLabel, [], transaction));
        }
        if (result.Transactions.Count > 0) Changed?.Invoke();
        return result.Warning;
    }

    /// <summary>Attach source/model file writes to this command. Call inside the same Begin scope as the entity edits.</summary>
    public void ApplyFiles(string label, string owner, IReadOnlyList<StudioFileWrite> writes, IReadOnlySet<string> allowedPaths, Action? afterPromotion = null)
    {
        EnsureAvailable();
        if (writes.Count == 0 && afterPromotion is null) return;
        var pending = new PendingFiles(label, owner, writes.Select(write => write with { Bytes = write.Bytes?.ToArray() }).ToArray(),
            allowedPaths.ToHashSet(StringComparer.Ordinal), afterPromotion);
        if (_open is not null)
        {
            if (_openFiles is not null)
            {
                throw new InvalidOperationException("В одной команде можно присоединить только один пакет файлов; передайте все файлы одним вызовом.");
            }

            _openFiles = pending;
            return;
        }

        var transaction = StudioFileTransaction.Apply(Workspace.Root, pending.Owner, pending.Writes, pending.AllowedPaths, pending.AfterPromotion);
        if (transaction is not null)
        {
            var command = new EditCommand(label, [], transaction);
            try
            {
                Push(command);
            }
            catch
            {
                if (!IsInUndoStack(command)) transaction.Cancel();
                throw;
            }
        }
    }

    /// <summary>Apply a file-only command and keep its journal open through the live-view barrier.</summary>
    public async Task ApplyFilesAsync(string label, string owner, IReadOnlyList<StudioFileWrite> writes,
        IReadOnlySet<string> allowedPaths, Action nativeBarrier, Func<Task> afterPromotionAsync)
    {
        EnsureAvailable();
        if (_open is not null) throw new InvalidOperationException("Async file transactions must be a standalone file-only command.");
        if (writes.Count == 0) return;
        ArgumentNullException.ThrowIfNull(nativeBarrier);
        ArgumentNullException.ThrowIfNull(afterPromotionAsync);

        _asyncOperationBusy = true;
        try
        {
            Changed?.Invoke();
            var transaction = await StudioFileTransaction.ApplyAsync(Workspace.Root, owner,
                writes.Select(write => write with { Bytes = write.Bytes?.ToArray() }).ToArray(),
                allowedPaths.ToHashSet(StringComparer.Ordinal), nativeBarrier, afterPromotionAsync);
            if (transaction is null) return;

            var command = new EditCommand(label, [], transaction);
            try
            {
                Push(command);
            }
            catch
            {
                if (!IsInUndoStack(command)) await transaction.CancelAsync(afterPromotionAsync);
                throw;
            }
        }
        finally
        {
            _asyncOperationBusy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Delete one clean, registered authored JSON source as a durable Undo/Redo command.</summary>
    public async Task DeleteAuthoredJsonSourceAsync(string relativePath, string expectedText, string label,
        string operationId, Func<Task> afterPromotionAsync)
    {
        EnsureAvailable();
        if (_open is not null) throw new InvalidOperationException("Завершите текущее составное действие перед удалением исходника.");
        if (!Guid.TryParseExact(operationId, "N", out var parsedOperationId))
            throw new ArgumentException("Для удаления исходника требуется идентификатор входящей версии GUID N.", nameof(operationId));
        var owner = StudioFileTransaction.AuthoredSourceDeletionOwnerPrefix + parsedOperationId.ToString("N");
        if (!StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(relativePath) || !Workspace.HasFile(relativePath))
            throw new InvalidOperationException("Удаление разрешено только для зарегистрированного JSON-исходника Studio.");

        var file = Workspace.File(relativePath);
        if (!file.RootIsArray)
            throw new InvalidOperationException("Удаляются только зарегистрированные JSON-исходники со списком объектов; манифесты и служебные файлы защищены.");
        if (!string.Equals(file.Text, expectedText, StringComparison.Ordinal))
            throw new InvalidOperationException("Снимок исходника устарел. Обновите сравнение перед удалением.");
        if (file.Dirty || file.IsNew)
            throw new InvalidOperationException("Сначала сохраните или восстановите локальный черновик; удаление не должно его терять.");

        StudioFileTransaction.EnsureNoLinks(Workspace.Root, relativePath);
        var expectedSha256 = file.LoadedSha256;
        if (expectedSha256.Length != 64 || !expectedSha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new InvalidDataException("У зарегистрированного исходника нет проверенной базовой SHA-256.");
        var diskBytes = StudioFileTransaction.ReadBoundedFile(file.FullPath, StudioFileTransaction.MaximumAuthoredSourceBytes);
        if (!string.Equals(AtomicFile.Sha256(diskBytes), expectedSha256, StringComparison.Ordinal))
            throw new ExternalChangeException(relativePath);

        var writes = new[] { new StudioFileWrite(relativePath, expectedSha256, null) };
        var allowedPaths = new HashSet<string>(StringComparer.Ordinal) { relativePath };
        await ApplyFilesAsync(label, owner, writes, allowedPaths,
            () => Workspace.ReconcileDeletedAuthoredJsonSource(relativePath, file, expectedText, expectedSha256),
            afterPromotionAsync);
    }

    /// <summary>Create one absent authored JSON entity source as a durable Undo/Redo command.</summary>
    public async Task CreateAuthoredJsonSourceAsync(string relativePath, byte[] utf8, string label,
        string operationId, Func<Task> afterPromotionAsync)
    {
        EnsureAvailable();
        if (_open is not null) throw new InvalidOperationException("Завершите текущее составное действие перед созданием исходника.");
        ArgumentNullException.ThrowIfNull(utf8);
        ArgumentNullException.ThrowIfNull(afterPromotionAsync);
        if (!Guid.TryParseExact(operationId, "N", out var parsedOperationId))
            throw new ArgumentException("Для создания исходника требуется идентификатор входящей версии GUID N.", nameof(operationId));

        var bytes = utf8.ToArray();
        Workspace.EnsureCanCreateAuthoredJsonSource(relativePath, bytes);
        var owner = StudioFileTransaction.AuthoredSourceCreationOwnerPrefix + parsedOperationId.ToString("N");
        var expectedSha256 = AtomicFile.Sha256(bytes);
        var writes = new[] { new StudioFileWrite(relativePath, null, bytes) };
        var allowedPaths = new HashSet<string>(StringComparer.Ordinal) { relativePath };
        await ApplyFilesAsync(label, owner, writes, allowedPaths,
            () => Workspace.ReconcileCreatedAuthoredJsonSource(relativePath, bytes, expectedSha256),
            afterPromotionAsync);
    }

    /// <summary>Replace (or create) an entity by ID in its file.</summary>
    public void Set(string relativePath, string key, JsonNode? value, string label) => Apply(relativePath, key, value, remove: false, label);

    public void Delete(string relativePath, string key, string label) => Apply(relativePath, key, null, remove: true, label);

    public const string OrderKey = "#order";

    /// <summary>Adopt reviewed source into a draft as one undo step, retaining its actual disk baseline.</summary>
    public void ReplaceDraft(string relativePath, string expectedText, string text, string label)
    {
        EnsureAvailable();
        if (_open is not null) throw new InvalidOperationException("Завершите текущее составное действие перед объединением.");
        var file = Workspace.File(relativePath);
        StudioFileTransaction.EnsureNoLinks(Workspace.Root, relativePath);
        if (file.Text != expectedText || file.ChangedOnDisk())
            throw new InvalidOperationException("Локальная версия изменилась. Откройте сравнение заново.");
        if (text == expectedText) return;
        var previousConflicts = Workspace.IdentityConflicts.ToHashSet(StringComparer.Ordinal);
        var command = new EditCommand(label, [new EntityChange(relativePath, "",
            JsonValue.Create(expectedText), JsonValue.Create(text)) { SourceText = true }]);
        StudioFileTransaction.FenceIncompleteHistory(Workspace.Root);
        file.ReplaceDraftText(text);
        try
        {
            Workspace.Reindex();
            if (Workspace.IdentityConflicts.Any(conflict => !previousConflicts.Contains(conflict)))
                throw new InvalidOperationException("Объединение создаёт повторяющиеся ID. Исправьте входящую версию.");
            Push(command);
        }
        catch
        {
            if (!IsInUndoStack(command))
            {
                file.ReplaceDraftText(expectedText);
                Workspace.Reindex();
            }
            throw;
        }
    }

    /// <summary>Change the order of a file's entities (a list order is behaviour: cutscene actions, stages). One undo step.</summary>
    public void SetOrder(string relativePath, IReadOnlyList<string> keys, string label)
    {
        EnsureAvailable();
        var file = Workspace.File(relativePath);
        var before = new JsonArray(file.Keys().Select(key => (JsonNode?)key).ToArray());
        var after = new JsonArray(keys.Select(key => (JsonNode?)key).ToArray());
        if (JsonNode.DeepEquals(before, after)) return;
        if (file.RootIsArray && file.Keys().Any(Workspace.IsAmbiguous))
            throw new InvalidOperationException("Порядок не изменён: в файле есть неоднозначные ID. Исправьте исходник вручную.");
        StudioFileTransaction.FenceIncompleteHistory(Workspace.Root);
        file.Reorder(keys);
        var change = new EntityChange(relativePath, OrderKey, before, after);
        if (_open is not null)
        {
            _open.Add(change);
        }
        else
        {
            var command = new EditCommand(label, [change]);
            try { Push(command); }
            catch
            {
                if (IsInUndoStack(command)) throw;
                RevertChanges(command.Changes);
                throw;
            }
        }
    }

    /// <summary>Change one field of an entity addressed by its ID; the path is a list of property names.</summary>
    public void SetField(string id, IReadOnlyList<string> path, JsonNode? value, string label)
    {
        EnsureAvailable();
        var address = Workspace.Locate(id) ?? throw new KeyNotFoundException($"Нет объекта с ID {id}.");
        var entity = Workspace.File(address.RelativePath).Get(address.Key)!.AsObject();
        JsonObject parent = entity;
        foreach (var name in path.Take(path.Count - 1))
        {
            parent = parent[name] as JsonObject ?? throw new KeyNotFoundException($"У {id} нет поля {string.Join('.', path)}.");
        }

        parent[path[^1]] = value?.DeepClone();
        Set(address.RelativePath, address.Key, entity, label);
    }

    public void Undo()
    {
        EnsureAvailable();
        if (!_undo.TryPop(out var command))
        {
            return;
        }

        try
        {
            EnsureAuthoredSourceCreationTransition(command, forward: false);
            EnsureCanRevert(command, forward: false);
            command.Files?.Restore(forward: false);
            try
            {
                Revert(command, forward: false);
            }
            catch
            {
                command.Files?.Restore(forward: true);
                throw;
            }
            _redo.Push(command);
        }
        catch
        {
            _undo.Push(command);
            throw;
        }

        Changed?.Invoke();
    }

    public void Redo()
    {
        EnsureAvailable();
        if (!_redo.TryPop(out var command))
        {
            return;
        }

        try
        {
            EnsureAuthoredSourceCreationTransition(command, forward: true);
            EnsureCanRevert(command, forward: true);
            command.Files?.Restore(forward: true);
            try
            {
                Revert(command, forward: true);
            }
            catch
            {
                command.Files?.Restore(forward: false);
                throw;
            }
            _undo.Push(command);
        }
        catch
        {
            _redo.Push(command);
            throw;
        }

        Changed?.Invoke();
    }

    /// <summary>Undo a file command only after its source and live view are restored.</summary>
    public async Task UndoAsync(Func<Task> fileViewBarrier)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(fileViewBarrier);
        if (!_undo.TryPeek(out var top) || top.Files is null)
        {
            Undo();
            return;
        }

        _asyncOperationBusy = true;
        try
        {
            Changed?.Invoke();
            var command = _undo.Peek();
            EnsureAuthoredSourceCreationTransition(command, forward: false);
            EnsureCanRevert(command, forward: false);
            await command.Files!.RestoreAsync(forward: false, fileViewBarrier);
            try
            {
                Revert(command, forward: false);
            }
            catch
            {
                await command.Files.RestoreAsync(forward: true, fileViewBarrier);
                throw;
            }
            _undo.Pop();
            _redo.Push(command);
        }
        finally
        {
            _asyncOperationBusy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Redo saved file images only after the live view can load them.</summary>
    public async Task RedoAsync(Func<Task> fileViewBarrier)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(fileViewBarrier);
        if (!_redo.TryPeek(out var top) || top.Files is null)
        {
            Redo();
            return;
        }

        _asyncOperationBusy = true;
        try
        {
            Changed?.Invoke();
            var command = _redo.Peek();
            EnsureAuthoredSourceCreationTransition(command, forward: true);
            EnsureCanRevert(command, forward: true);
            await command.Files!.RestoreAsync(forward: true, fileViewBarrier);
            try
            {
                Revert(command, forward: true);
            }
            catch
            {
                await command.Files.RestoreAsync(forward: false, fileViewBarrier);
                throw;
            }
            _redo.Pop();
            _undo.Push(command);
        }
        finally
        {
            _asyncOperationBusy = false;
            Changed?.Invoke();
        }
    }

    private void Apply(string relativePath, string key, JsonNode? value, bool remove, string label)
    {
        EnsureAvailable();
        var file = Workspace.File(relativePath);
        var before = file.Get(key);
        var after = remove ? null : value?.DeepClone();
        if (JsonNode.DeepEquals(before, after))
        {
            return;
        }

        if (file.RootIsArray && Workspace.IsAmbiguous(key))
            throw new InvalidOperationException("Объект не изменён: ID повторяется. Исправьте исходник вручную.");
        var beforeKeys = file.Keys().ToArray();
        StudioFileTransaction.FenceIncompleteHistory(Workspace.Root);
        file.Put(key, after, remove);
        Workspace.Reindex();
        var change = new EntityChange(relativePath, key, before, after);
        var changes = new List<EntityChange> { change };
        var afterKeys = file.Keys().ToArray();
        if (!beforeKeys.SequenceEqual(afterKeys, StringComparer.Ordinal))
            changes.Add(new EntityChange(relativePath, OrderKey,
                new JsonArray(beforeKeys.Select(item => (JsonNode?)item).ToArray()),
                new JsonArray(afterKeys.Select(item => (JsonNode?)item).ToArray())));
        if (_open is not null)
        {
            _open.AddRange(changes);
        }
        else
        {
            var command = new EditCommand(label, changes);
            try { Push(command); }
            catch
            {
                if (IsInUndoStack(command)) throw;
                RevertChanges(command.Changes);
                throw;
            }
        }
    }

    private void Push(EditCommand command)
    {
        var retired = new List<StudioFileTransaction>();
        try
        {
            if (_redo.Any(redo => redo.Files is not null))
                StudioFileTransaction.FenceIncompleteHistory(Workspace.Root);
            // shortcut: redo journals retire one by one; use shared history metadata if full-stack recovery becomes supported.
            foreach (var redo in _redo)
            {
                if (redo.Files is not { } files) continue;
                files.SetRedoDiscarded(discarded: true);
                retired.Add(files);
            }

            command.Files?.Accept(command.Label);
        }
        catch (Exception error)
        {
            var failures = new List<Exception> { error };
            for (var index = retired.Count - 1; index >= 0; index--)
            {
                try { retired[index].SetRedoDiscarded(discarded: false); }
                catch (Exception restoreError) { failures.Add(restoreError); }
            }

            if (failures.Count > 1)
                throw new AggregateException("The edit was not accepted, and persistent redo history could not be fully restored.", failures);
            throw;
        }

        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke();
    }

    private void CompleteOpen()
    {
        var changes = Coalesce(_open!);
        var label = _openLabel!;
        var pendingFiles = _openFiles;
        StudioFileTransaction? transaction = null;
        try
        {
            if (pendingFiles is not null)
            {
                transaction = StudioFileTransaction.Apply(Workspace.Root, pendingFiles.Owner, pendingFiles.Writes,
                    pendingFiles.AllowedPaths, pendingFiles.AfterPromotion);
            }
        }
        catch
        {
            ClearOpen();
            RevertChanges(changes);
            throw;
        }

        ClearOpen();
        if (changes.Count == 0 && transaction is null) return;

        var command = new EditCommand(label, changes, transaction);
        try
        {
            Push(command);
        }
        catch
        {
            // Changed observers run after the command is already accepted. Do not
            // turn an observer failure into a file/entity rollback behind its back.
            if (IsInUndoStack(command)) throw;
            transaction?.Cancel();
            RevertChanges(changes);
            throw;
        }
    }

    private void ClearOpen()
    {
        _open = null;
        _openLabel = null;
        _openFiles = null;
    }

    private bool IsInUndoStack(EditCommand command) => _undo.Any(item => ReferenceEquals(item, command));

    private void EnsureAvailable()
    {
        if (_asyncOperationBusy) throw new InvalidOperationException("Дождитесь завершения файловой транзакции и обновления вида.");
    }

    private void EnsureCanRevert(EditCommand command, bool forward)
    {
        var blocked = EntityConflicts(command, forward);
        var blockedFiles = command.Files?.Conflicts(forward) ?? [];
        if (blocked.Length > 0 || blockedFiles.Count > 0)
        {
            throw new UndoConflictException(command.Label, blocked, blockedFiles);
        }
    }

    private void EnsureAuthoredSourceCreationTransition(EditCommand command, bool forward)
    {
        if (command.Files is not { } transaction
            || !StudioFileTransaction.IsAuthoredSourceCreationOwner(transaction.Owner)) return;
        if (transaction.RelativePaths.Count != 1)
            throw new InvalidDataException("Команда создания исходника должна менять ровно один файл.");

        var relativePath = transaction.RelativePaths[0];
        var expectedAfterSha256 = transaction.AfterSha256For(relativePath)
            ?? throw new InvalidDataException("Команда создания исходника не содержит проверенного образа.");
        Workspace.EnsureAuthoredJsonSourceCreationTransition(relativePath, expectedAfterSha256, forward);
    }

    private EntityChange[] EntityConflicts(EditCommand command, bool forward)
    {
        JsonNode? Current(EntityChange change) => change.SourceText
            ? Workspace.File(change.RelativePath).ChangedOnDisk() ? null : JsonValue.Create(Workspace.File(change.RelativePath).Text)
            : change.Key == OrderKey
            ? new JsonArray(Workspace.File(change.RelativePath).Keys().Select(key => (JsonNode?)key).ToArray())
            : Workspace.File(change.RelativePath).Get(change.Key);
        return command.Changes
            .Where(change => !JsonNode.DeepEquals(Current(change), forward ? change.Before : change.After))
            .ToArray();
    }

    private void RevertChanges(IReadOnlyList<EntityChange> changes)
    {
        foreach (var change in changes.Where(change => change.Key != OrderKey).Reverse()
            .Concat(changes.Where(change => change.Key == OrderKey).Reverse()))
        {
            if (change.SourceText)
            {
                Workspace.File(change.RelativePath).ReplaceDraftText(change.Before!.GetValue<string>());
            }
            else if (change.Key == OrderKey)
            {
                Workspace.File(change.RelativePath).Reorder(change.Before!.AsArray().Select(key => (string)key!).ToArray());
            }
            else
            {
                Workspace.File(change.RelativePath).Put(change.Key, change.Before, remove: change.Before is null);
            }
        }

        Workspace.Reindex();
    }

    private void Revert(EditCommand command, bool forward)
    {
        var blocked = EntityConflicts(command, forward);
        if (blocked.Length > 0)
        {
            throw new UndoConflictException(command.Label, blocked);
        }

        var orderedChanges = forward ? command.Changes : command.Changes.Reverse();
        // Restore membership before applying the saved order, including deleted entities.
        foreach (var change in orderedChanges.Where(change => change.Key != OrderKey)
            .Concat(orderedChanges.Where(change => change.Key == OrderKey)))
        {
            var target = forward ? change.After : change.Before;
            if (change.SourceText)
            {
                Workspace.File(change.RelativePath).ReplaceDraftText(target!.GetValue<string>());
                continue;
            }
            if (change.Key == OrderKey)
            {
                Workspace.File(change.RelativePath).Reorder(target!.AsArray().Select(key => (string)key!).ToArray());
                continue;
            }

            Workspace.File(change.RelativePath).Put(change.Key, target, remove: target is null);
        }

        Workspace.Reindex();
    }

    // Several edits of the same entity inside one command collapse to its
    // first "before" and last "after" (a whole drag is one change).
    private static List<EntityChange> Coalesce(List<EntityChange> changes) =>
        changes
            .GroupBy(change => (change.RelativePath, change.Key, change.SourceText))
            .Select(group => new EntityChange(group.Key.RelativePath, group.Key.Key, group.First().Before, group.Last().After) { SourceText = group.Key.SourceText })
            .Where(change => !JsonNode.DeepEquals(change.Before, change.After))
            .ToList();

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }

    private sealed record PendingFiles(string Label, string Owner, IReadOnlyList<StudioFileWrite> Writes,
        IReadOnlySet<string> AllowedPaths, Action? AfterPromotion);
}
