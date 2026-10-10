using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Storage;

/// <summary>A file replacement owned by one Studio command. Null bytes mean the target should be absent.</summary>
public sealed record StudioFileWrite(string RelativePath, string? ExpectedSha256, byte[]? Bytes);

/// <summary>A pending multi-file promotion that startup recovery could not safely roll back.</summary>
public sealed record PendingStudioFileTransaction(
    string Id,
    string Owner,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> Conflicts);

/// <summary>A read-only attempt to restore one committed file command into a fresh session.</summary>
public sealed record StudioFileHistoryLoadResult(StudioFileTransaction? Transaction, string? Warning);

/// <summary>
/// Durable before/after images for a bounded set of project files. Promotion is
/// journaled and each target uses <see cref="AtomicFile"/>; recovery only
/// touches targets whose current bytes still match one of this transaction's images.
/// </summary>
public sealed class StudioFileTransaction
{
    private const string TransactionDirectory = ".urman-studio/transactions";
    private const string JournalName = "journal.json";
    private const int JournalVersion = 1;
    private readonly string _root;
    private readonly string _directory;
    private readonly HashSet<string> _allowedPaths;
    private readonly IReadOnlyList<FileChange> _changes;
    private readonly Action? _afterPromotion;
    private readonly Func<Task>? _afterPromotionAsync;
    private string _currentSide;
    private bool _accepted;

    private StudioFileTransaction(
        string root,
        string directory,
        string id,
        string owner,
        HashSet<string> allowedPaths,
        IReadOnlyList<FileChange> changes,
        Action? afterPromotion,
        Func<Task>? afterPromotionAsync,
        string currentSide,
        bool accepted)
    {
        _root = root;
        _directory = directory;
        Id = id;
        Owner = owner;
        _allowedPaths = allowedPaths;
        _changes = changes;
        _afterPromotion = afterPromotion;
        _afterPromotionAsync = afterPromotionAsync;
        _currentSide = currentSide;
        _accepted = accepted;
    }

    public string Id { get; }
    public string Owner { get; }
    public IReadOnlyList<string> RelativePaths => _changes.Select(change => change.RelativePath).ToArray();
    internal bool IsAtAfterSide => _currentSide == "after";

    /// <summary>
    /// Save pre/post images, write an applying journal, then promote every file.
    /// The caller accepts the transaction only after its edit command is ready to push.
    /// </summary>
    public static StudioFileTransaction? Apply(
        string root,
        string owner,
        IReadOnlyList<StudioFileWrite> writes,
        IReadOnlySet<string> allowedPaths,
        Action? afterPromotion = null)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A stable transaction owner is required.", nameof(owner));
        var rootPath = NormalizeRoot(root);
        var allowlist = NormalizeAllowlist(rootPath, allowedPaths);
        var prepared = Prepare(rootPath, writes, allowlist);
        if (prepared.Count == 0 && afterPromotion is null) return null;

        var transaction = CreateStaging(rootPath, owner, allowlist, prepared, afterPromotion, afterPromotionAsync: null);
        transaction.WriteJournal("applying", rollbackSide: "before", targetSide: "after", side: "before");
        var promotionStarted = false;
        try
        {
            transaction.EnsureCurrentSide("before");
            promotionStarted = true;
            transaction.PromoteSide("after");
            afterPromotion?.Invoke();
            transaction._currentSide = "after";
            return transaction;
        }
        catch (Exception error)
        {
            if (!promotionStarted)
            {
                transaction.WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
                throw;
            }

            try
            {
                transaction.RestoreTo("before", acceptEitherSide: true);
                afterPromotion?.Invoke();
                transaction._currentSide = "before";
                transaction.WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
            }
            catch (Exception rollbackError)
            {
                throw new StudioFileTransactionException(transaction.Id, "File promotion failed and safe rollback is pending.",
                    new AggregateException(error, rollbackError));
            }

            throw new StudioFileTransactionException(transaction.Id, "File promotion failed; the recorded previous files were restored.", error);
        }
    }

    /// <summary>Promote files, then await the native import and live-view barrier before acceptance.</summary>
    public static async Task<StudioFileTransaction?> ApplyAsync(
        string root,
        string owner,
        IReadOnlyList<StudioFileWrite> writes,
        IReadOnlySet<string> allowedPaths,
        Action nativeBarrier,
        Func<Task> afterPromotionAsync)
    {
        ArgumentNullException.ThrowIfNull(nativeBarrier);
        ArgumentNullException.ThrowIfNull(afterPromotionAsync);
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A stable transaction owner is required.", nameof(owner));
        var rootPath = NormalizeRoot(root);
        var allowlist = NormalizeAllowlist(rootPath, allowedPaths);
        var prepared = Prepare(rootPath, writes, allowlist);
        if (prepared.Count == 0) return null;

        var transaction = CreateStaging(rootPath, owner, allowlist, prepared, nativeBarrier, afterPromotionAsync);
        transaction.WriteJournal("applying", rollbackSide: "before", targetSide: "after", side: "before");
        var promotionStarted = false;
        try
        {
            transaction.EnsureCurrentSide("before");
            promotionStarted = true;
            transaction.PromoteSide("after");
            await transaction.RunBarriersAsync(afterPromotionAsync);
            transaction._currentSide = "after";
            return transaction;
        }
        catch (Exception error)
        {
            if (!promotionStarted)
            {
                transaction.WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
                throw;
            }

            try
            {
                transaction.RestoreTo("before", acceptEitherSide: true);
                await transaction.RunBarriersAsync(afterPromotionAsync);
                transaction._currentSide = "before";
                transaction.WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
            }
            catch (Exception rollbackError)
            {
                throw new StudioFileTransactionException(transaction.Id, "File promotion failed and safe rollback is pending.",
                    new AggregateException(error, rollbackError));
            }

            throw new StudioFileTransactionException(transaction.Id, "File promotion failed; the recorded previous files were restored.", error);
        }
    }

    private static StudioFileTransaction CreateStaging(
        string root,
        string owner,
        HashSet<string> allowlist,
        IReadOnlyList<PreparedChange> prepared,
        Action? nativeBarrier,
        Func<Task>? afterPromotionAsync)
    {
        var id = Guid.NewGuid().ToString("N");
        var transactionRoot = ResolveInternal(root, TransactionDirectory);
        EnsureNoLinks(root, TransactionDirectory, allowStudioMetadata: true);
        Directory.CreateDirectory(transactionRoot);
        EnsureNoLinks(root, TransactionDirectory, allowStudioMetadata: true);
        var directory = Path.Combine(transactionRoot, id);
        Directory.CreateDirectory(directory);
        EnsureNoLinks(root, $"{TransactionDirectory}/{id}", allowStudioMetadata: true);

        var changes = new List<FileChange>(prepared.Count);
        for (var index = 0; index < prepared.Count; index++)
        {
            var item = prepared[index];
            var beforeImage = item.BeforeBytes is null ? null : ImageName("before", index);
            var afterImage = item.AfterBytes is null ? null : ImageName("after", index);
            changes.Add(new(item.RelativePath, item.ExpectedSha256, item.BeforeSha256, item.AfterSha256, beforeImage, afterImage));
        }

        var transaction = new StudioFileTransaction(root, directory, id, owner, allowlist, changes,
            nativeBarrier, afterPromotionAsync, "before", accepted: false);
        // No target can be promoted until an atomic applying journal replaces this staging state.
        transaction.WriteJournal("staging", rollbackSide: "before", targetSide: "after", side: "before");
        for (var index = 0; index < prepared.Count; index++)
        {
            var item = prepared[index];
            if (changes[index].BeforeImage is { } beforeImage)
                AtomicFile.WriteAllBytes(Path.Combine(directory, beforeImage), item.BeforeBytes!);
            if (changes[index].AfterImage is { } afterImage)
                AtomicFile.WriteAllBytes(Path.Combine(directory, afterImage), item.AfterBytes!);
        }

        return transaction;
    }

    private async Task RunBarriersAsync(Func<Task>? viewBarrier)
    {
        _afterPromotion?.Invoke();
        if (viewBarrier is not null) await viewBarrier();
        else if (_afterPromotionAsync is not null) await _afterPromotionAsync();
    }

    /// <summary>Commit the applying journal once its command is accepted into the edit stack.</summary>
    internal void Accept()
    {
        if (_accepted) return;
        EnsureCurrentSide(_currentSide);
        // Persist acceptance before EditSession exposes the command as undoable.
        // A crash during later Undo/Redo can then recover the last accepted side.
        WriteJournal("committed", rollbackSide: _currentSide, targetSide: _currentSide, side: _currentSide, accepted: true);
        _accepted = true;
    }

    /// <summary>Retire or restore only this command's journal; project files may belong to a newer command.</summary>
    internal void SetRedoDiscarded(bool discarded)
    {
        if (!_accepted) throw new InvalidOperationException("An unaccepted file transaction cannot enter persistent redo history.");

        EnsureNoLinks(_root, Path.GetRelativePath(_root, _directory).Replace('\\', '/'), allowStudioMetadata: true);
        var manifest = ReadManifest(_root, _directory);
        if (manifest.Id != Id || manifest.Owner != Owner || !manifest.Accepted
            || manifest.Side != _currentSide || manifest.RollbackSide != _currentSide || manifest.TargetSide != _currentSide
            || !manifest.Changes.SequenceEqual(_changes))
        {
            throw new InvalidDataException($"Transaction {Id} no longer matches its in-memory accepted command.");
        }

        var imageConflicts = ValidateManifestImages(_root, _directory, manifest);
        if (imageConflicts.Count > 0)
            throw new InvalidDataException($"Transaction {Id} images need inspection: {string.Join(", ", imageConflicts)}.");

        var currentStatus = discarded ? "committed" : "retired";
        var targetStatus = discarded ? "retired" : "committed";
        if (manifest.Status != currentStatus)
            throw new InvalidDataException($"Transaction {Id} has status '{manifest.Status}' while changing redo history.");

        WriteJournal(targetStatus, _currentSide, _currentSide, _currentSide, accepted: true);
    }

    /// <summary>Undo a promotion that could not be attached to its edit command.</summary>
    internal void Cancel()
    {
        if (_accepted) throw new InvalidOperationException("An accepted file transaction cannot be cancelled.");
        if (_afterPromotionAsync is not null) throw new InvalidOperationException("This file transaction requires asynchronous cancellation.");
        RestoreTo("before", acceptEitherSide: true);
        _afterPromotion?.Invoke();
        _currentSide = "before";
        WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
    }

    internal async Task CancelAsync(Func<Task>? viewBarrier = null)
    {
        if (_accepted) throw new InvalidOperationException("An accepted file transaction cannot be cancelled.");
        RestoreTo("before", acceptEitherSide: true);
        await RunBarriersAsync(viewBarrier);
        _currentSide = "before";
        WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
    }

    /// <summary>Paths that no longer match the recorded side needed by this undo/redo.</summary>
    internal IReadOnlyList<string> Conflicts(bool forward)
    {
        var expectedSide = forward ? "before" : "after";
        return ConflictsForSide(expectedSide, acceptEitherSide: false);
    }

    /// <summary>Restore saved bytes only. This never invokes Blender, AI, or another generator.</summary>
    internal void Restore(bool forward)
    {
        if (!_accepted) throw new InvalidOperationException("An unaccepted file transaction cannot be undone.");
        if (_afterPromotionAsync is not null) throw new InvalidOperationException("This file transaction requires asynchronous undo/redo.");
        var sourceSide = forward ? "before" : "after";
        var targetSide = forward ? "after" : "before";
        var conflicts = ConflictsForSide(sourceSide, acceptEitherSide: false);
        if (conflicts.Count > 0) throw new StudioFileTransactionConflictException(Id, conflicts);

        WriteJournal("applying", rollbackSide: sourceSide, targetSide: targetSide, side: sourceSide);
        try
        {
            PromoteSide(targetSide);
            _afterPromotion?.Invoke();
            WriteJournal("committed", rollbackSide: targetSide, targetSide: targetSide, side: targetSide);
            _currentSide = targetSide;
        }
        catch (Exception error)
        {
            try
            {
                RestoreTo(sourceSide, acceptEitherSide: true);
                _afterPromotion?.Invoke();
                WriteJournal("committed", rollbackSide: sourceSide, targetSide: sourceSide, side: sourceSide);
                _currentSide = sourceSide;
            }
            catch (Exception rollbackError)
            {
                throw new StudioFileTransactionException(Id, "Undo/redo failed and safe rollback is pending.",
                    new AggregateException(error, rollbackError));
            }

            throw new StudioFileTransactionException(Id, "Undo/redo failed; the previous file state was restored.", error);
        }
    }

    internal async Task RestoreAsync(bool forward, Func<Task> viewBarrier)
    {
        ArgumentNullException.ThrowIfNull(viewBarrier);
        if (!_accepted) throw new InvalidOperationException("An unaccepted file transaction cannot be undone.");
        var sourceSide = forward ? "before" : "after";
        var targetSide = forward ? "after" : "before";
        var conflicts = ConflictsForSide(sourceSide, acceptEitherSide: false);
        if (conflicts.Count > 0) throw new StudioFileTransactionConflictException(Id, conflicts);

        WriteJournal("applying", rollbackSide: sourceSide, targetSide: targetSide, side: sourceSide);
        try
        {
            PromoteSide(targetSide);
            await RunBarriersAsync(viewBarrier);
            WriteJournal("committed", rollbackSide: targetSide, targetSide: targetSide, side: targetSide);
            _currentSide = targetSide;
        }
        catch (Exception error)
        {
            try
            {
                RestoreTo(sourceSide, acceptEitherSide: true);
                await RunBarriersAsync(viewBarrier);
                WriteJournal("committed", rollbackSide: sourceSide, targetSide: sourceSide, side: sourceSide);
                _currentSide = sourceSide;
            }
            catch (Exception rollbackError)
            {
                throw new StudioFileTransactionException(Id, "Undo/redo failed and safe rollback is pending.",
                    new AggregateException(error, rollbackError));
            }

            throw new StudioFileTransactionException(Id, "Undo/redo failed; the previous file state was restored.", error);
        }
    }

    /// <summary>Inspect interrupted promotions without touching files outside the caller's exact allowlist.</summary>
    public static IReadOnlyList<PendingStudioFileTransaction> InspectPending(string root, IReadOnlySet<string> allowedPaths)
    {
        var rootPath = NormalizeRoot(root);
        var allowlist = NormalizeAllowlist(rootPath, allowedPaths);
        return InspectPending(rootPath, path => TryNormalizeAllowed(rootPath, path, allowlist, out _));
    }

    /// <summary>Inspect interrupted promotions with a caller-owned exact-path policy.</summary>
    public static IReadOnlyList<PendingStudioFileTransaction> InspectPending(string root, Func<string, bool> allowedPath)
    {
        var rootPath = NormalizeRoot(root);
        var transactionRoot = ResolveInternal(rootPath, TransactionDirectory);
        if (!Directory.Exists(transactionRoot)) return [];
        EnsureNoLinks(rootPath, TransactionDirectory, allowStudioMetadata: true);

        var pending = new List<PendingStudioFileTransaction>();
        foreach (var directory in Directory.EnumerateDirectories(transactionRoot).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(directory);
            try
            {
                EnsureNoLinks(rootPath, $"{TransactionDirectory}/{id}", allowStudioMetadata: true);
                if (!File.Exists(Path.Combine(directory, JournalName)))
                {
                    // A newly-created empty directory precedes the first journal write; the
                    // protocol cannot promote targets before an applying journal exists.
                    var entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
                    if (entries.Length == 0) continue;
                    pending.Add(new(id, "", [], ["journal.json is missing; staged metadata exists, so promotion cannot be ruled out"]));
                    continue;
                }

                var manifest = ReadManifest(rootPath, directory);
                if (manifest.Status == "staging")
                {
                    var stagedConflicts = manifest.Changes
                        .Where(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, allowedPath, out _))
                        .Select(path => $"{path.RelativePath} (outside allowlist)")
                        .Concat(ConflictsForManifest(rootPath, directory, manifest, allowedPath, "before", acceptEitherSide: false))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (stagedConflicts.Length > 0)
                        pending.Add(new(manifest.Id, manifest.Owner, manifest.Changes.Select(change => change.RelativePath).ToArray(), stagedConflicts));
                    continue;
                }
                if (manifest.Status != "applying") continue;
                var invalidPaths = manifest.Changes
                    .Where(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, allowedPath, out _))
                    .Select(change => change.RelativePath)
                    .ToArray();
                var conflicts = invalidPaths.Select(path => $"{path} (outside allowlist)")
                    .Concat(ValidateManifestImages(rootPath, directory, manifest))
                    .Concat(ConflictsForManifest(rootPath, directory, manifest, allowedPath, manifest.RollbackSide, acceptEitherSide: true))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (conflicts.Length == 0) conflicts = ["applying transaction was not fully recovered; inspect journal and retry startup recovery"];
                pending.Add(new(manifest.Id, manifest.Owner, manifest.Changes.Select(change => change.RelativePath).ToArray(), conflicts));
            }
            catch (Exception error)
            {
                pending.Add(new(id, "", [], [error.Message]));
            }
        }

        return pending;
    }

    /// <summary>
    /// Read one accepted file-only pilot command for a new session. This deliberately
    /// refuses to order multiple journals and never changes a journal or target file.
    /// </summary>
    public static StudioFileHistoryLoadResult LoadSingleCommittedFileCommand(
        string root,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet)
    {
        ArgumentNullException.ThrowIfNull(allowedPath);
        ArgumentNullException.ThrowIfNull(allowedFileSet);
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A stable transaction owner is required.", nameof(owner));

        var rootPath = NormalizeRoot(root);
        var transactionRoot = ResolveInternal(rootPath, TransactionDirectory);
        if (!Directory.Exists(transactionRoot)) return new(null, null);
        EnsureNoLinks(rootPath, TransactionDirectory, allowStudioMetadata: true);

        var matching = new List<(string Directory, Manifest Manifest)>();
        var unreadable = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(transactionRoot))
        {
            var id = Path.GetFileName(directory);
            try
            {
                EnsureNoLinks(rootPath, $"{TransactionDirectory}/{id}", allowStudioMetadata: true);
                var journalPath = Path.Combine(directory, JournalName);
                if (!File.Exists(journalPath))
                {
                    if (Directory.EnumerateFileSystemEntries(directory).Any())
                        unreadable.Add($"{id}: journal.json is missing");
                    continue;
                }

                var manifest = ReadManifest(rootPath, directory);
                if (manifest.Status == "committed" && manifest.Accepted && string.Equals(manifest.Owner, owner, StringComparison.Ordinal))
                    matching.Add((directory, manifest));
            }
            catch (Exception error)
            {
                // An unreadable journal cannot safely be ruled out as a newer pilot command.
                unreadable.Add($"{id}: {error.Message}");
            }
        }

        if (unreadable.Count > 0)
        {
            return new(null, "Persistent HeroHouse Undo was not restored because transaction metadata needs inspection: "
                + string.Join("; ", unreadable.Take(4)));
        }

        if (matching.Count == 0) return new(null, null);
        if (matching.Count > 1)
        {
            return new(null, "Persistent HeroHouse Undo was not restored: multiple committed file commands exist, and their order is ambiguous. "
                + "The editor is available; the pilot history needs a manual decision.");
        }

        var (manifestDirectory, candidate) = matching[0];
        if (candidate.Side != candidate.RollbackSide || candidate.Side != candidate.TargetSide)
        {
            return new(null, $"Persistent HeroHouse Undo was not restored: transaction {candidate.Id} has inconsistent committed-side metadata.");
        }

        var normalizedPaths = new List<string>(candidate.Changes.Count);
        foreach (var change in candidate.Changes)
        {
            if (!TryNormalizeForRecovery(rootPath, change.RelativePath, allowedPath, out var normalized))
            {
                return new(null, $"Persistent HeroHouse Undo was not restored: {change.RelativePath} is outside its path policy.");
            }
            normalizedPaths.Add(normalized);
        }

        if (normalizedPaths.Distinct(StringComparer.Ordinal).Count() != normalizedPaths.Count
            || !allowedFileSet(normalizedPaths))
        {
            return new(null, $"Persistent HeroHouse Undo was not restored: transaction {candidate.Id} does not contain the exact pilot file set.");
        }

        var imageConflicts = ValidateManifestImages(rootPath, manifestDirectory, candidate);
        if (imageConflicts.Count > 0)
        {
            return new(null, $"Persistent HeroHouse Undo was not restored: saved transaction images need inspection ({string.Join(", ", imageConflicts)}).");
        }

        var currentConflicts = ConflictsForManifest(rootPath, manifestDirectory, candidate, allowedPath, candidate.Side, acceptEitherSide: false);
        if (currentConflicts.Count > 0)
        {
            return new(null, $"Persistent HeroHouse Undo was not restored because files changed since the transaction: {string.Join(", ", currentConflicts)}.");
        }

        var allowlist = normalizedPaths.ToHashSet(StringComparer.Ordinal);
        return new(FromManifest(rootPath, manifestDirectory, candidate, allowlist, afterPromotion: null), null);
    }

    /// <summary>Roll back interrupted promotions only when every current file is still one of the saved images.</summary>
    public static IReadOnlyList<PendingStudioFileTransaction> Recover(
        string root,
        IReadOnlySet<string> allowedPaths,
        Action? afterRecovery = null)
    {
        var rootPath = NormalizeRoot(root);
        var allowlist = NormalizeAllowlist(rootPath, allowedPaths);
        return Recover(rootPath, path => TryNormalizeAllowed(rootPath, path, allowlist, out _), afterRecovery);
    }

    /// <summary>Recover applying journals using a caller-owned path policy for dynamic, recipe-scoped outputs.</summary>
    public static IReadOnlyList<PendingStudioFileTransaction> Recover(
        string root,
        Func<string, bool> allowedPath,
        Action? afterRecovery = null)
    {
        var rootPath = NormalizeRoot(root);
        var transactionRoot = ResolveInternal(rootPath, TransactionDirectory);
        if (!Directory.Exists(transactionRoot)) return [];
        EnsureNoLinks(rootPath, TransactionDirectory, allowStudioMetadata: true);

        foreach (var directory in Directory.EnumerateDirectories(transactionRoot).Order(StringComparer.Ordinal))
        {
            try
            {
                EnsureNoLinks(rootPath, $"{TransactionDirectory}/{Path.GetFileName(directory)}", allowStudioMetadata: true);
                var journalPath = Path.Combine(directory, JournalName);
                if (!File.Exists(journalPath)) continue;

                var manifest = ReadManifest(rootPath, directory);
                if (manifest.Status == "staging")
                {
                    // Staging is written before images and before any project target. Confirm
                    // every target still has its recorded preimage before retiring metadata.
                    if (manifest.Changes.Any(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, allowedPath, out _))) continue;
                    if (ConflictsForManifest(rootPath, directory, manifest, allowedPath, "before", acceptEitherSide: false).Count > 0) continue;
                    var stagingAllowlist = manifest.Changes.Select(change => NormalizeRelativePath(change.RelativePath)).ToHashSet(StringComparer.Ordinal);
                    var staging = FromManifest(rootPath, directory, manifest, stagingAllowlist, afterRecovery);
                    staging.WriteJournal("recovered", "before", "after", "before");
                    continue;
                }

                if (manifest.Status != "applying") continue;
                if (manifest.Changes.Any(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, allowedPath, out _))) continue;
                if (ValidateManifestImages(rootPath, directory, manifest).Count > 0) continue;
                var conflicts = ConflictsForManifest(rootPath, directory, manifest, allowedPath, manifest.RollbackSide, acceptEitherSide: true);
                if (conflicts.Count > 0) continue;

                var allowlist = manifest.Changes.Select(change => NormalizeRelativePath(change.RelativePath)).ToHashSet(StringComparer.Ordinal);
                var transaction = FromManifest(rootPath, directory, manifest, allowlist, afterRecovery);
                transaction.RestoreTo(manifest.RollbackSide, acceptEitherSide: true);
                afterRecovery?.Invoke();
                // Preserve history only for a command that had already been accepted.
                // An unaccepted first Apply is retired as recovered and is never hydrated.
                transaction.WriteJournal(manifest.Accepted ? "committed" : "recovered",
                    manifest.RollbackSide, manifest.RollbackSide, manifest.RollbackSide, manifest.Accepted);
                transaction._currentSide = manifest.RollbackSide;
            }
            catch
            {
                // InspectPending reports the exact journal/image/target failure below. Startup
                // must remain available for a manual decision instead of failing in _Ready.
            }
        }

        return InspectPending(rootPath, allowedPath);
    }

    private IReadOnlyList<string> ConflictsForSide(string side, bool acceptEitherSide)
    {
        var conflicts = new List<string>();
        foreach (var change in _changes)
        {
            try
            {
                EnsureAllowed(change.RelativePath);
                EnsureNoLinks(_root, change.RelativePath);
                var actual = CurrentSha256(change.RelativePath);
                var beforeMatches = string.Equals(actual, change.BeforeSha256, StringComparison.Ordinal);
                var afterMatches = string.Equals(actual, change.AfterSha256, StringComparison.Ordinal);
                if (acceptEitherSide ? !beforeMatches && !afterMatches : !string.Equals(actual, side == "before" ? change.BeforeSha256 : change.AfterSha256, StringComparison.Ordinal))
                {
                    conflicts.Add(change.RelativePath);
                }
            }
            catch
            {
                conflicts.Add(change.RelativePath);
            }
        }

        return conflicts;
    }

    private void EnsureCurrentSide(string side)
    {
        var conflicts = ConflictsForSide(side, acceptEitherSide: false);
        if (conflicts.Count > 0) throw new StudioFileTransactionConflictException(Id, conflicts);
    }

    private void PromoteSide(string side)
    {
        ValidateImages(side);
        var sourceSide = side == "after" ? "before" : "after";
        EnsureCurrentSide(sourceSide);
        for (var index = 0; index < _changes.Count; index++)
        {
            var change = _changes[index];
            EnsureCurrentFileSide(change, sourceSide, acceptEitherSide: false);
            var bytes = ReadImage(change, side, index);
            var target = ResolveTarget(change.RelativePath);
            if (bytes is null)
            {
                if (File.Exists(target)) File.Delete(target);
            }
            else
            {
                AtomicFile.WriteAllBytes(target, bytes);
            }
        }
    }

    private void RestoreTo(string side, bool acceptEitherSide)
    {
        ValidateImages(side);
        var conflicts = ConflictsForSide(side, acceptEitherSide);
        if (conflicts.Count > 0) throw new StudioFileTransactionConflictException(Id, conflicts);
        for (var index = 0; index < _changes.Count; index++)
        {
            var change = _changes[index];
            EnsureCurrentFileSide(change, side, acceptEitherSide);
            var bytes = ReadImage(change, side, index);
            var target = ResolveTarget(change.RelativePath);
            var expectedHash = side == "before" ? change.BeforeSha256 : change.AfterSha256;
            if (string.Equals(CurrentSha256(change.RelativePath), expectedHash, StringComparison.Ordinal))
            {
                continue;
            }

            if (bytes is null)
            {
                if (File.Exists(target)) File.Delete(target);
            }
            else
            {
                AtomicFile.WriteAllBytes(target, bytes);
            }
        }
    }

    private void EnsureCurrentFileSide(FileChange change, string side, bool acceptEitherSide)
    {
        EnsureAllowed(change.RelativePath);
        EnsureNoLinks(_root, change.RelativePath);
        var actual = CurrentSha256(change.RelativePath);
        var beforeMatches = string.Equals(actual, change.BeforeSha256, StringComparison.Ordinal);
        var afterMatches = string.Equals(actual, change.AfterSha256, StringComparison.Ordinal);
        if (acceptEitherSide ? !beforeMatches && !afterMatches
            : !string.Equals(actual, side == "before" ? change.BeforeSha256 : change.AfterSha256, StringComparison.Ordinal))
        {
            throw new StudioFileTransactionConflictException(Id, [change.RelativePath]);
        }
    }

    private static IReadOnlyList<string> ConflictsForManifest(
        string root,
        string directory,
        Manifest manifest,
        Func<string, bool> allowedPath,
        string side,
        bool acceptEitherSide)
    {
        var conflicts = new List<string>();
        foreach (var change in manifest.Changes)
        {
            if (!TryNormalizeForRecovery(root, change.RelativePath, allowedPath, out var normalized))
            {
                conflicts.Add($"{change.RelativePath} (outside allowlist)");
                continue;
            }

            try
            {
                EnsureNoLinks(root, normalized);
                var actual = CurrentSha256(root, normalized);
                var beforeMatches = string.Equals(actual, change.BeforeSha256, StringComparison.Ordinal);
                var afterMatches = string.Equals(actual, change.AfterSha256, StringComparison.Ordinal);
                if (acceptEitherSide ? !beforeMatches && !afterMatches : !string.Equals(actual, side == "before" ? change.BeforeSha256 : change.AfterSha256, StringComparison.Ordinal))
                {
                    conflicts.Add(normalized);
                }
            }
            catch
            {
                conflicts.Add(normalized);
            }
        }

        return conflicts;
    }

    private static IReadOnlyList<string> ValidateManifestImages(string root, string directory, Manifest manifest)
    {
        var conflicts = new List<string>();
        for (var index = 0; index < manifest.Changes.Count; index++)
        {
            var change = manifest.Changes[index];
            Validate(change.BeforeImage, change.BeforeSha256);
            Validate(change.AfterImage, change.AfterSha256);

            void Validate(string? imageName, string? expectedHash)
            {
                if (expectedHash is null) return;
                var display = $"{manifest.Id}/{imageName ?? "(missing image name)"}";
                try
                {
                    if (imageName is null) throw new InvalidDataException("image name is missing");
                    var imagePath = Path.Combine(directory, imageName);
                    EnsureNoLinks(root, Path.GetRelativePath(root, imagePath).Replace('\\', '/'), allowStudioMetadata: true);
                    if (!File.Exists(imagePath)) throw new FileNotFoundException("image file is missing");
                    if (!string.Equals(AtomicFile.Sha256OfFile(imagePath), expectedHash, StringComparison.Ordinal))
                        throw new InvalidDataException("image SHA-256 does not match the journal");
                }
                catch (Exception error)
                {
                    conflicts.Add($"{display}: {error.Message}");
                }
            }
        }

        return conflicts;
    }

    private byte[]? ReadImage(FileChange change, string side, int index)
    {
        var hash = side == "before" ? change.BeforeSha256 : change.AfterSha256;
        if (hash is null) return null;
        var name = side == "before" ? change.BeforeImage : change.AfterImage;
        var path = Path.Combine(_directory, name ?? throw new InvalidDataException("Missing transaction image name."));
        EnsureNoLinks(_root, Path.GetRelativePath(_root, path).Replace('\\', '/'), allowStudioMetadata: true);
        var bytes = File.ReadAllBytes(path);
        if (!string.Equals(AtomicFile.Sha256(bytes), hash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Transaction image {Id}/{name} failed its SHA-256 receipt.");
        }

        return bytes;
    }

    private string? CurrentSha256(string relativePath) => CurrentSha256(_root, relativePath);

    private static string? CurrentSha256(string root, string relativePath)
    {
        var path = ResolveTarget(root, relativePath);
        if (Directory.Exists(path)) throw new IOException($"Transaction target is a directory: {relativePath}");
        return File.Exists(path) ? AtomicFile.Sha256OfFile(path) : null;
    }

    private string ResolveTarget(string relativePath) => ResolveTarget(_root, relativePath);

    private static string ResolveTarget(string root, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        EnsureNoLinks(root, normalized);
        return ResolveInternal(root, normalized);
    }

    private void EnsureAllowed(string relativePath)
    {
        if (!TryNormalizeAllowed(_root, relativePath, _allowedPaths, out _))
        {
            throw new InvalidOperationException($"File path is outside the transaction allowlist: {relativePath}");
        }
    }

    private void WriteJournal(string status, string rollbackSide, string targetSide, string side, bool? accepted = null)
    {
        var files = new JsonArray();
        foreach (var change in _changes)
        {
            files.Add(new JsonObject
            {
                ["relativePath"] = change.RelativePath,
                ["expectedSha256"] = change.ExpectedSha256,
                ["beforeSha256"] = change.BeforeSha256,
                ["afterSha256"] = change.AfterSha256,
                ["beforeImage"] = change.BeforeImage,
                ["afterImage"] = change.AfterImage
            });
        }

        var manifest = new JsonObject
        {
            ["version"] = JournalVersion,
            ["id"] = Id,
            ["owner"] = Owner,
            ["status"] = status,
            ["rollbackSide"] = rollbackSide,
            ["targetSide"] = targetSide,
            ["side"] = side,
            ["accepted"] = accepted ?? _accepted,
            ["files"] = files
        };
        var journalPath = Path.Combine(_directory, JournalName);
        EnsureNoLinks(_root, Path.GetRelativePath(_root, journalPath).Replace('\\', '/'), allowStudioMetadata: true);
        AtomicFile.WriteAllText(journalPath, manifest.ToJsonString());
    }

    private static IReadOnlyList<PreparedChange> Prepare(string root, IReadOnlyList<StudioFileWrite> writes, HashSet<string> allowlist)
    {
        var normalizedWrites = new List<(string RelativePath, string? ExpectedSha256, byte[]? Bytes)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var write in writes)
        {
            if (!TryNormalizeAllowed(root, write.RelativePath, allowlist, out var relativePath))
            {
                throw new InvalidOperationException($"File path is outside the transaction allowlist: {write.RelativePath}");
            }

            if (!seen.Add(relativePath)) throw new ArgumentException($"Duplicate transaction file: {relativePath}", nameof(writes));
            if (write.ExpectedSha256 is { } expected && !IsSha256(expected))
            {
                throw new ArgumentException($"Invalid expected SHA-256 for {relativePath}.", nameof(writes));
            }

            normalizedWrites.Add((relativePath, write.ExpectedSha256?.ToLowerInvariant(), write.Bytes?.ToArray()));
        }

        var prepared = new List<PreparedChange>(normalizedWrites.Count);
        foreach (var write in normalizedWrites)
        {
            EnsureNoLinks(root, write.RelativePath);
            var path = ResolveInternal(root, write.RelativePath);
            if (Directory.Exists(path)) throw new IOException($"Transaction target is a directory: {write.RelativePath}");
            var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            var beforeSha = before is null ? null : AtomicFile.Sha256(before);
            if (!string.Equals(beforeSha, write.ExpectedSha256, StringComparison.Ordinal))
            {
                throw new StudioFileTransactionConflictException("new", [write.RelativePath]);
            }

            var after = write.Bytes;
            var afterSha = after is null ? null : AtomicFile.Sha256(after);
            if (string.Equals(beforeSha, afterSha, StringComparison.Ordinal)) continue;
            prepared.Add(new(write.RelativePath, write.ExpectedSha256, before, after, beforeSha, afterSha));
        }

        return prepared;
    }

    private static HashSet<string> NormalizeAllowlist(string root, IReadOnlySet<string> allowedPaths)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in allowedPaths)
        {
            var normalized = NormalizeRelativePath(path);
            _ = ResolveInternal(root, normalized);
            result.Add(normalized);
        }

        return result;
    }

    private static bool TryNormalizeAllowed(string root, string relativePath, HashSet<string> allowlist, out string normalized)
    {
        try
        {
            normalized = NormalizeRelativePath(relativePath);
            _ = ResolveInternal(root, normalized);
            return allowlist.Contains(normalized);
        }
        catch
        {
            normalized = relativePath;
            return false;
        }
    }

    private static bool TryNormalizeForRecovery(string root, string relativePath, Func<string, bool> allowedPath, out string normalized)
    {
        try
        {
            normalized = NormalizeRelativePath(relativePath);
            _ = ResolveInternal(root, normalized);
            return allowedPath(normalized);
        }
        catch
        {
            normalized = relativePath;
            return false;
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains('\\') || relativePath.Contains(':'))
        {
            throw new ArgumentException("Transaction paths must be project-relative paths using '/'.", nameof(relativePath));
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException("Transaction paths cannot contain empty, '.' or '..' segments.", nameof(relativePath));
        }

        if (segments[0].Equals(".urman-studio", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Studio transaction metadata cannot be a transaction target.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    private static string NormalizeRoot(string root)
    {
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"Project root does not exist: {full}");
        return full;
    }

    private static string ResolveInternal(string root, string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var fromRoot = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(fromRoot) || fromRoot == ".." || fromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Transaction path escapes the project root.", nameof(relativePath));
        }

        return full;
    }

    private static void EnsureNoLinks(string root, string relativePath, bool allowStudioMetadata = false)
    {
        var normalized = allowStudioMetadata ? NormalizeInternalRelativePath(relativePath) : NormalizeRelativePath(relativePath);
        var current = root;
        foreach (var segment in normalized.Split('/'))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null || info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Symbolic links are not allowed in a transaction path: {relativePath}");
            }
        }
    }

    private static string NormalizeInternalRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains('\\') || relativePath.Contains(':'))
        {
            throw new ArgumentException("Internal transaction paths must be relative and use '/'.", nameof(relativePath));
        }

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException("Internal transaction paths cannot contain empty, '.' or '..' segments.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    private void ValidateImages(string side)
    {
        for (var index = 0; index < _changes.Count; index++)
        {
            _ = ReadImage(_changes[index], side, index);
        }
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static string ImageName(string side, int index) => $"{side}-{index:D4}.bin";

    private static Manifest ReadManifest(string root, string directory)
    {
        var journal = Path.Combine(directory, JournalName);
        EnsureNoLinks(root, Path.GetRelativePath(root, journal).Replace('\\', '/'), allowStudioMetadata: true);
        var node = JsonNode.Parse(File.ReadAllText(journal))?.AsObject() ?? throw new InvalidDataException("Transaction journal is not an object.");
        if ((int?)node["version"] != JournalVersion) throw new InvalidDataException("Unsupported transaction journal version.");
        var status = (string?)node["status"] ?? throw new InvalidDataException("Transaction status is missing.");
        if (status is not ("staging" or "applying" or "committed" or "recovered" or "retired")) throw new InvalidDataException("Transaction status is invalid.");
        var rollbackSide = (string?)node["rollbackSide"] ?? throw new InvalidDataException("Transaction rollback side is missing.");
        var targetSide = (string?)node["targetSide"] ?? throw new InvalidDataException("Transaction target side is missing.");
        var side = (string?)node["side"] ?? throw new InvalidDataException("Transaction side is missing.");
        if (!IsSide(rollbackSide) || !IsSide(targetSide) || !IsSide(side)) throw new InvalidDataException("Transaction journal has an invalid side.");
        // Older committed journals predate this field and are accepted by definition.
        // Older applying journals are ambiguous (they could be Apply or Undo/Redo), so
        // they remain unaccepted and will never be restored into persistent history.
        var accepted = node["accepted"] is JsonValue acceptedNode
            ? acceptedNode.GetValue<bool>()
            : status == "committed";
        if ((status is "committed" or "retired") && !accepted)
            throw new InvalidDataException("A committed or retired transaction is marked unaccepted.");
        var files = node["files"]?.AsArray() ?? throw new InvalidDataException("Transaction files are missing.");
        var changes = files.Select((entry, index) =>
        {
            var item = entry?.AsObject() ?? throw new InvalidDataException("Invalid transaction file entry.");
            var path = (string?)item["relativePath"] ?? throw new InvalidDataException("Transaction path is missing.");
            var expected = (string?)item["expectedSha256"];
            var beforeHash = (string?)item["beforeSha256"];
            var afterHash = (string?)item["afterSha256"];
            if (expected is not null && !IsSha256(expected) || beforeHash is not null && !IsSha256(beforeHash) || afterHash is not null && !IsSha256(afterHash))
            {
                throw new InvalidDataException($"Transaction SHA-256 receipt is invalid: {path}");
            }

            var beforeImage = (string?)item["beforeImage"];
            var afterImage = (string?)item["afterImage"];
            if ((beforeHash is null) != (beforeImage is null) || (afterHash is null) != (afterImage is null))
            {
                throw new InvalidDataException($"Transaction image receipt is incomplete: {path}");
            }

            if (!string.Equals(expected, beforeHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Transaction expected hash does not match its before image: {path}");
            }

            if (beforeImage is not null && beforeImage != ImageName("before", index) || afterImage is not null && afterImage != ImageName("after", index))
            {
                throw new InvalidDataException($"Transaction image path is invalid: {path}");
            }

            return new FileChange(path, expected, beforeHash, afterHash, beforeImage, afterImage);
        }).ToArray();
        var id = (string?)node["id"] ?? throw new InvalidDataException("Transaction ID is missing.");
        if (!string.Equals(id, Path.GetFileName(directory), StringComparison.Ordinal) || !Guid.TryParseExact(id, "N", out _))
        {
            throw new InvalidDataException("Transaction directory and ID do not match.");
        }

        return new(id, (string?)node["owner"] ?? "", status, rollbackSide, targetSide, side, accepted, changes);
    }

    private static StudioFileTransaction FromManifest(string root, string directory, Manifest manifest, HashSet<string> allowlist, Action? afterPromotion) =>
        new(root, directory, manifest.Id, manifest.Owner, allowlist, manifest.Changes, afterPromotion, null,
            manifest.Side, accepted: manifest.Accepted);

    private static bool IsSide(string side) => side is "before" or "after";

    private sealed record PreparedChange(string RelativePath, string? ExpectedSha256, byte[]? BeforeBytes, byte[]? AfterBytes, string? BeforeSha256, string? AfterSha256);
    private sealed record FileChange(string RelativePath, string? ExpectedSha256, string? BeforeSha256, string? AfterSha256, string? BeforeImage, string? AfterImage);
    private sealed record Manifest(string Id, string Owner, string Status, string RollbackSide, string TargetSide, string Side, bool Accepted, IReadOnlyList<FileChange> Changes);
}

public sealed class StudioFileTransactionConflictException(string transactionId, IReadOnlyList<string> relativePaths)
    : IOException($"File transaction {transactionId} conflicts with changed files: {string.Join(", ", relativePaths)}.")
{
    public string TransactionId { get; } = transactionId;
    public IReadOnlyList<string> RelativePaths { get; } = relativePaths;
}

public sealed class StudioFileTransactionException(string transactionId, string message, Exception innerException)
    : IOException($"File transaction {transactionId}: {message}", innerException)
{
    public string TransactionId { get; } = transactionId;
}
