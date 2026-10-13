using System.Text.Json;
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

public sealed record StudioFileHistoryStackLoadResult(IReadOnlyList<StudioFileTransaction> Transactions, int Cursor, string? Warning);

/// <summary>
/// Durable before/after images for a bounded set of project files. Promotion is
/// journaled and each target uses <see cref="AtomicFile"/>; recovery only
/// touches targets whose current bytes still match one of this transaction's images.
/// </summary>
public sealed class StudioFileTransaction
{
    private const string TransactionDirectory = ".urman-studio/transactions";
    private const string JournalName = "journal.json";
    private const string HistoryFencePath = ".urman-studio/history-fence.json";
    private const string HistorySequencePath = ".urman-studio/history-sequence.json";
    private const string HistorySequenceLock = ".urman-studio/history-sequence.lock";
    private const int JournalVersion = 1;
    internal const int MaximumAuthoredSourceBytes = 8 * 1024 * 1024;
    public const string AuthoredSourceDeletionOwnerPrefix = "urman.studio:authored-source-delete/";
    private const string ShopCatalogOwner = "urman.studio:shop-catalog";
    private const string ShopCatalogPath = "game/content/urman.shop.v1.json";
    private const string VehicleCatalogOwner = "urman.studio:vehicle-catalog";
    private const string VehicleCatalogPath = "game/content/vehicles/act1_vehicles.v1.json";
    private readonly string _root;
    private readonly string _directory;
    private readonly HashSet<string> _allowedPaths;
    private readonly IReadOnlyList<FileChange> _changes;
    private readonly Action? _afterPromotion;
    private readonly Func<Task>? _afterPromotionAsync;
    private string _currentSide;
    private bool _accepted;
    private long? _acceptedOrder;
    private string? _commandLabel;

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
    public string? CommandLabel => _commandLabel;
    public IReadOnlyList<string> RelativePaths => _changes.Select(change => change.RelativePath).ToArray();
    internal bool IsAtAfterSide => _currentSide == "after";

    public static bool IsAuthoredSourceDeletionOwner(string owner) =>
        owner.StartsWith(AuthoredSourceDeletionOwnerPrefix, StringComparison.Ordinal)
        && Guid.TryParseExact(owner[AuthoredSourceDeletionOwnerPrefix.Length..], "N", out var operationId)
        && string.Equals(owner[AuthoredSourceDeletionOwnerPrefix.Length..], operationId.ToString("N"), StringComparison.Ordinal);

    public static bool IsOwnedCatalogPath(string owner, string path) =>
        owner == ShopCatalogOwner && path == ShopCatalogPath
        || owner == VehicleCatalogOwner && path == VehicleCatalogPath;

    private static bool IsOwnedCatalogOwner(string owner) => owner is ShopCatalogOwner or VehicleCatalogOwner;

    private static bool IsGeometryReplacementOwner(string owner) =>
        Urman.Studio.Core.AI.FenceAlignmentProtocol.IsTransactionOwner(owner)
        || Urman.Studio.Core.AI.RoadSegmentSmoothSource.IsTransactionOwner(owner);

    private static bool IsGeometryReplacementPath(string owner, string path) =>
        Urman.Studio.Core.AI.FenceAlignmentProtocol.IsTransactionPath(owner, path)
        || Urman.Studio.Core.AI.RoadSegmentSmoothSource.IsTransactionPath(owner, path);

    private static bool IsGeometryReplacementFileSet(string owner, IReadOnlyList<string> paths) =>
        Urman.Studio.Core.AI.FenceAlignmentProtocol.IsTransactionOwner(owner)
            ? Urman.Studio.Core.AI.FenceAlignmentProtocol.IsTransactionFileSet(paths)
            : Urman.Studio.Core.AI.RoadSegmentSmoothSource.IsTransactionOwner(owner)
                && Urman.Studio.Core.AI.RoadSegmentSmoothSource.IsTransactionFileSet(paths);

    private static bool IsBoundedSourceOwner(string owner) =>
        IsAuthoredSourceDeletionOwner(owner)
        || owner is ShopCatalogOwner or VehicleCatalogOwner
        || IsGeometryReplacementOwner(owner);

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
        var prepared = Prepare(rootPath, writes, allowlist,
            IsBoundedSourceOwner(owner) ? MaximumAuthoredSourceBytes : null);
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
            transaction.EnsureCurrentSide("after");
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
                transaction.EnsureCurrentSide("before");
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
        var prepared = Prepare(rootPath, writes, allowlist,
            IsBoundedSourceOwner(owner) ? MaximumAuthoredSourceBytes : null);
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
            transaction.EnsureCurrentSide("after");
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
                transaction.EnsureCurrentSide("before");
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
        long requiredBytes = 1 << 20;
        checked
        {
            requiredBytes += 6L * (owner.Length + prepared.Sum(item => (long)item.RelativePath.Length) + 512L * prepared.Count);
            foreach (var item in prepared)
            {
                var before = item.BeforeBytes?.LongLength ?? 0;
                var after = item.AfterBytes?.LongLength ?? 0;
                requiredBytes += before + after + Math.Max(before, after);
            }
        }
        EnsureDiskCapacity(transactionRoot, requiredBytes, prepared.Select(item => ResolveTarget(root, item.RelativePath)));
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

    internal static byte[] ReadBoundedFile(string path, int maximumBytes)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var declaredLength = input.Length;
        if (declaredLength < 0 || declaredLength > maximumBytes)
            throw new InvalidDataException("Authored source exceeds the 8 MiB transaction image budget.");

        using var output = new MemoryStream(checked((int)declaredLength));
        var buffer = new byte[4096];
        var remaining = declaredLength;
        while (remaining > 0)
        {
            var count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (count == 0) throw new EndOfStreamException("Authored source changed while it was being read.");
            output.Write(buffer, 0, count);
            remaining -= count;
        }

        if (input.ReadByte() != -1) throw new InvalidDataException("Authored source grew while it was being read.");
        return output.ToArray();
    }

    /// <summary>Commit the applying journal once its command is accepted into the edit stack.</summary>
    internal void Accept(string label)
    {
        if (_accepted) return;
        if (string.IsNullOrWhiteSpace(label) || label.Length > 1024) throw new ArgumentException("A command label of at most 1024 characters is required.", nameof(label));
        EnsureNoLinks(_root, HistorySequenceLock, allowStudioMetadata: true);
        // The stable lock inode is retained; replacing it would split concurrent allocators.
        using var sequenceLock = new FileStream(ResolveInternal(_root, HistorySequenceLock), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        EnsureCurrentSide(_currentSide);
        _acceptedOrder ??= AllocateAcceptedOrder();
        _commandLabel = label;
        // Persist acceptance before EditSession exposes the command as undoable.
        // A crash during later Undo/Redo can then recover the last accepted side.
        WriteJournal("committed", rollbackSide: _currentSide, targetSide: _currentSide, side: _currentSide, accepted: true);
        _accepted = true;
    }

    private long AllocateAcceptedOrder()
    {
        EnsureNoLinks(_root, HistorySequencePath, allowStudioMetadata: true);
        long largestOrder = 0;
        foreach (var directory in Directory.EnumerateDirectories(ResolveInternal(_root, TransactionDirectory)))
        {
            EnsureNoLinks(_root, Path.GetRelativePath(_root, directory).Replace('\\', '/'), allowStudioMetadata: true);
            var manifest = ReadManifest(_root, directory);
            largestOrder = Math.Max(largestOrder, manifest.AcceptedOrder ?? 0);
        }
        var lastOrder = ReadHistorySequence(_root, largestOrder);
        var path = ResolveInternal(_root, HistorySequencePath);
        if (lastOrder == long.MaxValue) throw new InvalidDataException("The persistent history sequence is exhausted.");
        var next = lastOrder + 1;
        var text = new JsonObject { ["version"] = 1, ["scope"] = "studio-file-history", ["lastOrder"] = next }.ToJsonString();
        EnsureDiskCapacity(path, (1L << 20) + System.Text.Encoding.UTF8.GetByteCount(text), []);
        AtomicFile.WriteAllText(path, text);
        return next;
    }

    private static long ReadHistorySequence(string root, long largestOrder)
    {
        EnsureNoLinks(root, HistorySequencePath, allowStudioMetadata: true);
        var path = ResolveInternal(root, HistorySequencePath);
        long lastOrder = 0;
        if (File.Exists(path))
        {
            try
            {
                var counter = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                if (counter is null || (int?)counter["version"] != 1 || (string?)counter["scope"] != "studio-file-history"
                    || counter["lastOrder"] is not JsonValue value || !value.TryGetValue<long>(out lastOrder)
                    || lastOrder < 0 || lastOrder < largestOrder)
                    throw new InvalidDataException("The persistent history sequence needs inspection.");
            }
            catch (Exception error) when (error is System.Text.Json.JsonException or InvalidOperationException or FormatException or OverflowException)
            {
                throw new InvalidDataException("The persistent history sequence needs inspection.", error);
            }
        }
        else if (largestOrder != 0)
            throw new InvalidDataException("The persistent history sequence is missing; recorded command orders were preserved.");

        return lastOrder;
    }

    /// <summary>Retire or restore only this command's journal; project files may belong to a newer command.</summary>
    internal void SetRedoDiscarded(bool discarded)
    {
        if (!_accepted) throw new InvalidOperationException("An unaccepted file transaction cannot enter persistent redo history.");

        EnsureNoLinks(_root, Path.GetRelativePath(_root, _directory).Replace('\\', '/'), allowStudioMetadata: true);
        var manifest = ReadManifest(_root, _directory);
        if (manifest.Id != Id || manifest.Owner != Owner || !manifest.Accepted
            || manifest.AcceptedOrder != _acceptedOrder || manifest.CommandLabel != _commandLabel
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
        EnsureCurrentSide("before");
        _currentSide = "before";
        WriteJournal("recovered", rollbackSide: "before", targetSide: "after", side: "before");
    }

    internal async Task CancelAsync(Func<Task>? viewBarrier = null)
    {
        if (_accepted) throw new InvalidOperationException("An accepted file transaction cannot be cancelled.");
        RestoreTo("before", acceptEitherSide: true);
        await RunBarriersAsync(viewBarrier);
        EnsureCurrentSide("before");
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

        EnsureRestoreCapacity();
        WriteJournal("applying", rollbackSide: sourceSide, targetSide: targetSide, side: sourceSide);
        try
        {
            PromoteSide(targetSide);
            _afterPromotion?.Invoke();
            EnsureCurrentSide(targetSide);
            WriteJournal("committed", rollbackSide: targetSide, targetSide: targetSide, side: targetSide);
            _currentSide = targetSide;
        }
        catch (Exception error)
        {
            try
            {
                RestoreTo(sourceSide, acceptEitherSide: true);
                _afterPromotion?.Invoke();
                EnsureCurrentSide(sourceSide);
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

        EnsureRestoreCapacity();
        WriteJournal("applying", rollbackSide: sourceSide, targetSide: targetSide, side: sourceSide);
        try
        {
            PromoteSide(targetSide);
            await RunBarriersAsync(viewBarrier);
            EnsureCurrentSide(targetSide);
            WriteJournal("committed", rollbackSide: targetSide, targetSide: targetSide, side: targetSide);
            _currentSide = targetSide;
        }
        catch (Exception error)
        {
            try
            {
                RestoreTo(sourceSide, acceptEitherSide: true);
                await RunBarriersAsync(viewBarrier);
                EnsureCurrentSide(sourceSide);
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
        => InspectPendingCore(root, allowedPath, null);

    private static IReadOnlyList<PendingStudioFileTransaction> InspectPendingCore(
        string root, Func<string, bool> allowedPath, Func<string, string, bool>? allowedOwnedPath)
    {
        ArgumentNullException.ThrowIfNull(allowedPath);
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
                var manifestAllowedPath = RecoveryPathPolicy(rootPath, directory, manifest, allowedPath, allowedOwnedPath);
                var recoveryByteLimit = RecoveryByteLimit(manifest);
                if (manifest.Status == "staging")
                {
                    var stagedConflicts = manifest.Changes
                        .Where(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, manifestAllowedPath, out _))
                        .Select(path => $"{path.RelativePath} (outside allowlist)")
                        .Concat(ConflictsForManifest(rootPath, directory, manifest, manifestAllowedPath, "before", acceptEitherSide: false,
                            maximumTargetBytes: recoveryByteLimit))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (stagedConflicts.Length > 0)
                        pending.Add(new(manifest.Id, manifest.Owner, manifest.Changes.Select(change => change.RelativePath).ToArray(), stagedConflicts));
                    continue;
                }
                if (manifest.Status != "applying") continue;
                var invalidPaths = manifest.Changes
                    .Where(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, manifestAllowedPath, out _))
                    .Select(change => change.RelativePath)
                    .ToArray();
                var conflicts = invalidPaths.Select(path => $"{path} (outside allowlist)")
                    .Concat(ValidateManifestImages(rootPath, directory, manifest, recoveryByteLimit))
                    .Concat(ConflictsForManifest(rootPath, directory, manifest, manifestAllowedPath, manifest.RollbackSide,
                        acceptEitherSide: true, maximumTargetBytes: recoveryByteLimit))
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

    // shortcut: unsupported command order fences restart hydration; replace with an explicit ordered ledger and migration decision.
    internal static void FenceIncompleteHistory(string root)
    {
        var rootPath = NormalizeRoot(root);
        EnsureNoLinks(rootPath, HistoryFencePath, allowStudioMetadata: true);
        var path = ResolveInternal(rootPath, HistoryFencePath);
        if (File.Exists(path)) return;
        AtomicFile.WriteAllText(path, new JsonObject
        {
            ["version"] = 1,
            ["scope"] = "studio-command-history",
            ["reason"] = "unsupported-command-order"
        }.ToJsonString());
    }

    /// <summary>
    /// Read one accepted file-only pilot command for a new session. This deliberately
    /// refuses to order multiple journals and never changes a journal or target file.
    /// </summary>
    public static StudioFileHistoryLoadResult LoadSingleCommittedFileCommand(
        string root, string owner, Func<string, bool> allowedPath, Func<IReadOnlyList<string>, bool> allowedFileSet)
    {
        var result = LoadCommittedFileHistory(root, owner, allowedPath, allowedFileSet);
        return result.Transactions.Count == 1
            ? new(result.Transactions[0], result.Warning)
            : new(null, result.Warning ?? (result.Transactions.Count == 0 ? null : "Use ordered file history to restore multiple commands."));
    }

    /// <summary>Validate explicit file command order, cursor and aggregate source boundary before hydrating any history.</summary>
    public static StudioFileHistoryStackLoadResult LoadCommittedFileHistory(
        string root,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet)
    {
        return LoadCommittedFileHistoryCore(root, owner, allowedPath, allowedFileSet,
            sourceDeletionOwner: null, sourceDeletionPath: null, sourceDeletionAfterPromotion: null);
    }

    internal static StudioFileHistoryStackLoadResult LoadCommittedFileHistoryWithAuthoredSourceDeletions(
        string root,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet,
        Func<string, bool> sourceDeletionOwner,
        Func<string, bool> sourceDeletionPath,
        Func<string, byte[], string, Action> sourceDeletionAfterPromotion,
        bool includeOwnedCatalogs = false,
        bool includeHouseFamilies = false,
        bool includeFenceAlignments = false,
        bool includeRoadSmooth = false)
    {
        ArgumentNullException.ThrowIfNull(sourceDeletionOwner);
        ArgumentNullException.ThrowIfNull(sourceDeletionPath);
        ArgumentNullException.ThrowIfNull(sourceDeletionAfterPromotion);
        return LoadCommittedFileHistoryCore(root, owner, allowedPath, allowedFileSet,
            sourceDeletionOwner, sourceDeletionPath, sourceDeletionAfterPromotion, includeOwnedCatalogs, includeHouseFamilies,
            includeFenceAlignments, includeRoadSmooth);
    }

    private static StudioFileHistoryStackLoadResult LoadCommittedFileHistoryCore(
        string root,
        string owner,
        Func<string, bool> allowedPath,
        Func<IReadOnlyList<string>, bool> allowedFileSet,
        Func<string, bool>? sourceDeletionOwner,
        Func<string, bool>? sourceDeletionPath,
        Func<string, byte[], string, Action>? sourceDeletionAfterPromotion,
        bool includeOwnedCatalogs = false,
        bool includeHouseFamilies = false,
        bool includeFenceAlignments = false,
        bool includeRoadSmooth = false)
    {
        ArgumentNullException.ThrowIfNull(allowedPath);
        ArgumentNullException.ThrowIfNull(allowedFileSet);
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A stable transaction owner is required.", nameof(owner));

        var rootPath = NormalizeRoot(root);
        EnsureNoLinks(rootPath, HistoryFencePath, allowStudioMetadata: true);
        var historyFence = ResolveInternal(rootPath, HistoryFencePath);
        if (File.Exists(historyFence) || Directory.Exists(historyFence))
            return new([], 0, "История Undo после перезапуска недоступна: порядок файловых и других команд не сохранён полностью. "
                + "Журналы и исходные версии сохранены; для восстановления нужен явный выбор истории.");
        var transactionRoot = ResolveInternal(rootPath, TransactionDirectory);
        if (!Directory.Exists(transactionRoot)) return new([], 0, null);
        EnsureNoLinks(rootPath, TransactionDirectory, allowStudioMetadata: true);

        var matching = new List<(string Directory, Manifest Manifest, bool IsSourceDeletion, bool IsCatalog, bool IsHouseFamily, bool IsGeometryReplacement, string? SourcePath, byte[]? SourceBeforeBytes)>();
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
                var isSourceDeletion = sourceDeletionOwner?.Invoke(manifest.Owner) == true;
                var isCatalog = includeOwnedCatalogs && IsOwnedCatalogOwner(manifest.Owner);
                var isHouseFamily = includeHouseFamilies
                    && Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionOwner(manifest.Owner);
                var isGeometryReplacement = includeFenceAlignments
                    && Urman.Studio.Core.AI.FenceAlignmentProtocol.IsTransactionOwner(manifest.Owner)
                    || includeRoadSmooth && Urman.Studio.Core.AI.RoadSegmentSmoothSource.IsTransactionOwner(manifest.Owner);
                if (manifest.Status != "committed" || !manifest.Accepted)
                    continue;
                if (!isSourceDeletion && !isCatalog && !isHouseFamily && !isGeometryReplacement
                    && !string.Equals(manifest.Owner, owner, StringComparison.Ordinal))
                {
                    if (manifest.AcceptedOrder is not null)
                        unreadable.Add($"{id}: accepted ordered command is outside the requested history");
                    continue;
                }

                string? sourcePath = null;
                byte[]? sourceBeforeBytes = null;
                if (isSourceDeletion)
                {
                    if (manifest.Changes.Count != 1)
                    {
                        unreadable.Add($"{id}: authored source deletion does not contain exactly one file");
                        continue;
                    }

                    var change = manifest.Changes[0];
                    if (!TryNormalizeRecoveryPath(rootPath, change.RelativePath, out var normalizedSourcePath)
                        || sourceDeletionPath is null || !sourceDeletionPath(normalizedSourcePath)
                        || change.BeforeSha256 is null || change.AfterSha256 is not null
                        || !TryReadVerifiedAuthoredEntityListBeforeImage(rootPath, directory, change, out sourceBeforeBytes))
                    {
                        unreadable.Add($"{id}: authored source deletion path or saved before image is not valid");
                        continue;
                    }
                    sourcePath = normalizedSourcePath;
                }

                if (isCatalog && (manifest.Changes.Count != 1
                    || !IsOwnedCatalogPath(manifest.Owner, manifest.Changes[0].RelativePath)
                    || manifest.Changes[0].BeforeSha256 is null
                    || manifest.Changes[0].AfterSha256 is null))
                {
                    unreadable.Add($"{id}: catalogue transaction is not an exact one-file replacement");
                    continue;
                }

                if (isGeometryReplacement && (manifest.Changes.Count != 1
                    || !IsGeometryReplacementPath(
                        manifest.Owner, manifest.Changes[0].RelativePath)
                    || !HasValidGeometryReplacementImageStates(manifest.Changes)))
                {
                    unreadable.Add($"{id}: geometry transaction is not an exact one-file replacement");
                    continue;
                }

                matching.Add((directory, manifest, isSourceDeletion, isCatalog, isHouseFamily, isGeometryReplacement, sourcePath, sourceBeforeBytes));
            }
            catch (Exception error)
            {
                // An unreadable journal cannot safely be ruled out as a newer pilot command.
                unreadable.Add($"{id}: {error.Message}");
            }
        }

        if (unreadable.Count > 0)
        {
            return new([], 0, "История Undo после перезапуска не восстановлена: метаданные транзакции требуют проверки: "
                + string.Join("; ", unreadable.Take(4)));
        }

        if (matching.Count == 0) return new([], 0, null);
        if (matching.Count > 1 && (matching.Any(item => item.Manifest.AcceptedOrder is null)
            || matching.Select(item => item.Manifest.AcceptedOrder).Distinct().Count() != matching.Count))
            return new([], 0, "История Undo не восстановлена: порядок журналов отсутствует или повторяется. Журналы сохранены для явного выбора истории.");

        matching = matching.OrderBy(item => item.Manifest.AcceptedOrder ?? 0).ToList();
        if (matching.Any(item => item.Manifest.AcceptedOrder is not null))
            _ = ReadHistorySequence(rootPath, matching.Max(item => item.Manifest.AcceptedOrder ?? 0));
        var cursor = 0;
        var redoStarted = false;
        var chain = new Dictionary<string, string?>(StringComparer.Ordinal);
        var boundary = new Dictionary<string, string?>(StringComparer.Ordinal);
        var boundedAuthoredSourcePaths = new HashSet<string>(StringComparer.Ordinal);
        var transactions = new List<StudioFileTransaction>();
        foreach (var (manifestDirectory, candidate, isSourceDeletion, isCatalog, isHouseFamily, isGeometryReplacement, sourcePath, sourceBeforeBytes) in matching)
        {
            if (candidate.Side != candidate.RollbackSide || candidate.Side != candidate.TargetSide)
                return new([], 0, "История Undo не восстановлена: стороны журнала противоречат друг другу.");
            if (candidate.Side == "before") redoStarted = true;
            else if (redoStarted)
                return new([], 0, "История Undo не восстановлена: последовательность отменённых команд противоречива.");
            else cursor++;

            var normalizedPaths = new List<string>(candidate.Changes.Count);
            Func<string, bool> transactionPathAllowed = allowedPath;
            if (isSourceDeletion) transactionPathAllowed = sourceDeletionPath!;
            else if (isCatalog) transactionPathAllowed = path => IsOwnedCatalogPath(candidate.Owner, path);
            else if (isHouseFamily) transactionPathAllowed = path =>
                Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionPath(candidate.Owner, path)
                && allowedPath(path);
            else if (isGeometryReplacement) transactionPathAllowed = path =>
                IsGeometryReplacementPath(candidate.Owner, path)
                && allowedPath(path);
            foreach (var change in candidate.Changes)
            {
                if (!TryNormalizeForRecovery(rootPath, change.RelativePath, transactionPathAllowed, out var normalized))
                    return new([], 0, "История Undo не восстановлена: файл вне разрешённых границ.");
                normalizedPaths.Add(normalized);
            }
            if (normalizedPaths.Distinct(StringComparer.Ordinal).Count() != normalizedPaths.Count
                || (isSourceDeletion
                    ? normalizedPaths.Count != 1 || !string.Equals(normalizedPaths[0], sourcePath, StringComparison.Ordinal)
                    : isCatalog
                        ? normalizedPaths.Count != 1 || !IsOwnedCatalogPath(candidate.Owner, normalizedPaths[0])
                        : isHouseFamily
                            ? !Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionFileSet(normalizedPaths)
                                || !HasValidHouseFamilyImageStates(candidate.Changes)
                            : isGeometryReplacement
                                ? !IsGeometryReplacementFileSet(candidate.Owner, normalizedPaths)
                                    || !HasValidGeometryReplacementImageStates(candidate.Changes)
                                : !allowedFileSet(normalizedPaths)))
                return new([], 0, isSourceDeletion
                    ? "История Undo не восстановлена: путь исходника удаления изменился."
                    : isCatalog
                        ? "История Undo не восстановлена: путь каталога не соответствует его владельцу."
                        : isHouseFamily
                            ? "История Undo не восстановлена: набор файлов семейного дома не соответствует контракту операции."
                            : isGeometryReplacement
                                ? "История Undo не восстановлена: файл изменения дорожки или забора не соответствует контракту операции."
                                : "История Undo не восстановлена: набор файлов не соответствует контракту изменения дома.");
            if (ValidateManifestImages(rootPath, manifestDirectory, candidate,
                    isSourceDeletion || isCatalog || isGeometryReplacement ? MaximumAuthoredSourceBytes : null).Count > 0)
                return new([], 0, "История Undo не восстановлена: сохранённые версии файлов повреждены или недоступны.");

            foreach (var change in candidate.Changes)
            {
                var path = NormalizeRelativePath(change.RelativePath);
                if (isSourceDeletion || isCatalog || isGeometryReplacement) boundedAuthoredSourcePaths.Add(path);
                if (chain.TryGetValue(path, out var previous) && previous != change.BeforeSha256)
                    return new([], 0, "История Undo не восстановлена: исходные версии команд не образуют непрерывную цепочку.");
                chain[path] = change.AfterSha256;
                boundary.TryAdd(path, change.BeforeSha256);
                if (candidate.Side == "after") boundary[path] = change.AfterSha256;
            }
            Action? afterPromotion = null;
            if (isSourceDeletion)
            {
                if (sourceBeforeBytes is null || candidate.Changes[0].BeforeSha256 is not { } beforeSha256
                    || sourceDeletionAfterPromotion is null || sourcePath is null)
                    return new([], 0, "История Undo не восстановлена: не удалось подготовить восстановление удалённого исходника.");
                afterPromotion = sourceDeletionAfterPromotion(sourcePath, sourceBeforeBytes.ToArray(), beforeSha256);
            }

            transactions.Add(FromManifest(rootPath, manifestDirectory, candidate,
                normalizedPaths.ToHashSet(StringComparer.Ordinal), afterPromotion));
        }
        foreach (var (path, expectedHash) in boundary)
        {
            EnsureNoLinks(rootPath, path);
            var currentHash = boundedAuthoredSourcePaths.Contains(path)
                ? CurrentSha256(rootPath, path, MaximumAuthoredSourceBytes)
                : CurrentSha256(rootPath, path);
            if (currentHash != expectedHash)
                return new([], 0, "История Undo не восстановлена: файлы изменились после сохранённых команд. Текущие файлы оставлены без изменений.");
        }
        return new(transactions, cursor, null);
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
        => RecoverCore(root, allowedPath, null, afterRecovery);

    /// <summary>Recover with an extra owner-scoped path policy for deletion, catalogue and family transactions.</summary>
    public static IReadOnlyList<PendingStudioFileTransaction> RecoverWithOwnedPathPolicy(
        string root,
        Func<string, bool> allowedPath,
        Func<string, string, bool> allowedOwnedPath,
        Action? afterRecovery = null)
        => RecoverCore(root, allowedPath, allowedOwnedPath, afterRecovery);

    private static IReadOnlyList<PendingStudioFileTransaction> RecoverCore(
        string root,
        Func<string, bool> allowedPath,
        Func<string, string, bool>? allowedOwnedPath,
        Action? afterRecovery)
    {
        ArgumentNullException.ThrowIfNull(allowedPath);
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
                var manifestAllowedPath = RecoveryPathPolicy(rootPath, directory, manifest, allowedPath, allowedOwnedPath);
                var recoveryByteLimit = RecoveryByteLimit(manifest);
                if (manifest.Status == "staging")
                {
                    // Staging is written before images and before any project target. Confirm
                    // every target still has its recorded preimage before retiring metadata.
                    if (manifest.Changes.Any(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, manifestAllowedPath, out _))) continue;
                    if (ConflictsForManifest(rootPath, directory, manifest, manifestAllowedPath, "before", acceptEitherSide: false,
                        maximumTargetBytes: recoveryByteLimit).Count > 0) continue;
                    var stagingAllowlist = manifest.Changes.Select(change => NormalizeRelativePath(change.RelativePath)).ToHashSet(StringComparer.Ordinal);
                    var staging = FromManifest(rootPath, directory, manifest, stagingAllowlist, afterRecovery);
                    staging.WriteJournal("recovered", "before", "after", "before");
                    continue;
                }

                if (manifest.Status != "applying") continue;
                if (manifest.Changes.Any(change => !TryNormalizeForRecovery(rootPath, change.RelativePath, manifestAllowedPath, out _))) continue;
                if (ValidateManifestImages(rootPath, directory, manifest, recoveryByteLimit).Count > 0) continue;
                var conflicts = ConflictsForManifest(rootPath, directory, manifest, manifestAllowedPath, manifest.RollbackSide,
                    acceptEitherSide: true, maximumTargetBytes: recoveryByteLimit);
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

        return InspectPendingCore(rootPath, allowedPath, allowedOwnedPath);
    }

    private static Func<string, bool> RecoveryPathPolicy(
        string root,
        string directory,
        Manifest manifest,
        Func<string, bool> allowedPath,
        Func<string, string, bool>? allowedOwnedPath)
    {
        if (IsGeometryReplacementOwner(manifest.Owner))
        {
            if (manifest.Changes.Count != 1
                || !TryNormalizeRecoveryPath(root, manifest.Changes[0].RelativePath, out var fenceSourcePath)
                || !IsGeometryReplacementPath(manifest.Owner, fenceSourcePath)
                || !HasValidGeometryReplacementImageStates(manifest.Changes)
                || allowedOwnedPath is not null && !allowedOwnedPath(manifest.Owner, fenceSourcePath))
                return _ => false;

            var fenceChange = manifest.Changes[0];
            var fenceVerified = manifest.Status switch
            {
                "staging" => fenceChange.BeforeSha256 is { } fenceBeforeSha256
                    && CurrentSha256(root, fenceSourcePath, MaximumAuthoredSourceBytes) == fenceBeforeSha256,
                "applying" => ValidateManifestImages(root, directory, manifest, MaximumAuthoredSourceBytes).Count == 0,
                _ => false
            };
            return fenceVerified
                ? path => TryNormalizeRecoveryPath(root, path, out var fenceNormalized)
                    && IsGeometryReplacementPath(manifest.Owner, fenceNormalized)
                    && (allowedPath(fenceNormalized) || allowedOwnedPath?.Invoke(manifest.Owner, fenceNormalized) == true)
                : _ => false;
        }

        if (allowedOwnedPath is null) return allowedPath;
        if (Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionOwner(manifest.Owner))
        {
            var familyPaths = new List<string>(manifest.Changes.Count);
            foreach (var familyChange in manifest.Changes)
            {
                if (!TryNormalizeRecoveryPath(root, familyChange.RelativePath, out var normalized)
                    || !Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionPath(manifest.Owner, normalized)
                    || !allowedOwnedPath(manifest.Owner, normalized))
                    return _ => false;
                familyPaths.Add(normalized);
            }

            if (!Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionFileSet(familyPaths)
                || !HasValidHouseFamilyImageStates(manifest.Changes))
                return _ => false;

            return path => TryNormalizeRecoveryPath(root, path, out var normalized)
                && (allowedPath(normalized) || allowedOwnedPath(manifest.Owner, normalized));
        }
        if (manifest.Changes.Count != 1) return allowedPath;
        var change = manifest.Changes[0];
        if (!TryNormalizeRecoveryPath(root, change.RelativePath, out var sourcePath)
            || !allowedOwnedPath(manifest.Owner, sourcePath))
            return allowedPath;

        var isSourceDeletion = IsAuthoredSourceDeletionOwner(manifest.Owner)
            && change.BeforeSha256 is not null && change.AfterSha256 is null;
        var isCatalogReplacement = IsOwnedCatalogPath(manifest.Owner, sourcePath)
            && change.BeforeSha256 is not null && change.AfterSha256 is not null;
        if (!isSourceDeletion && !isCatalogReplacement) return allowedPath;

        var verified = isSourceDeletion
            ? manifest.Status switch
            {
                "staging" => change.BeforeSha256 is { } beforeSha256
                    && IsValidCurrentAuthoredEntityList(root, sourcePath, beforeSha256),
                "applying" => IsValidAuthoredEntityListBeforeImage(root, directory, change),
                _ => false
            }
            : isCatalogReplacement && (manifest.Status switch
                {
                    "staging" => change.BeforeSha256 is not null
                        && CurrentSha256(root, sourcePath, MaximumAuthoredSourceBytes) == change.BeforeSha256,
                    "applying" => ValidateManifestImages(root, directory, manifest, MaximumAuthoredSourceBytes).Count == 0,
                    _ => false
                });
        return verified ? path => allowedPath(path) || string.Equals(path, sourcePath, StringComparison.Ordinal) : allowedPath;
    }

    private static bool HasValidHouseFamilyImageStates(IReadOnlyList<FileChange> changes)
    {
        if (changes.Count == 0) return false;
        foreach (var change in changes)
        {
            if (Urman.Studio.Core.AI.HouseFamilyWindowProtocol.IsTransactionWorldPath(change.RelativePath))
            {
                if (change.BeforeSha256 is null || change.AfterSha256 is null) return false;
            }
            else if (change.BeforeSha256 is not null || change.AfterSha256 is null)
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasValidGeometryReplacementImageStates(IReadOnlyList<FileChange> changes) =>
        changes.Count == 1 && changes[0].BeforeSha256 is not null && changes[0].AfterSha256 is not null;

    // Bound source reads; path access still depends on the allowlist or verified owner policy above.
    private static int? RecoveryByteLimit(Manifest manifest) =>
        manifest.Owner.StartsWith(AuthoredSourceDeletionOwnerPrefix, StringComparison.Ordinal)
            || IsOwnedCatalogOwner(manifest.Owner)
            || IsGeometryReplacementOwner(manifest.Owner)
            ? MaximumAuthoredSourceBytes
            : null;

    private static bool IsValidAuthoredEntityListBeforeImage(string root, string directory, FileChange change)
        => TryReadVerifiedAuthoredEntityListBeforeImage(root, directory, change, out _);

    private static bool TryReadVerifiedAuthoredEntityListBeforeImage(
        string root,
        string directory,
        FileChange change,
        out byte[] beforeBytes)
    {
        beforeBytes = [];
        if (change.BeforeSha256 is null || change.AfterSha256 is not null || change.BeforeImage is null) return false;

        try
        {
            var imagePath = Path.Combine(directory, change.BeforeImage);
            var relativeImagePath = Path.GetRelativePath(root, imagePath).Replace('\\', '/');
            EnsureNoLinks(root, relativeImagePath, allowStudioMetadata: true);
            var bytes = ReadBoundedFile(imagePath, MaximumAuthoredSourceBytes);
            if (!string.Equals(AtomicFile.Sha256(bytes), change.BeforeSha256, StringComparison.Ordinal)) return false;
            if (!IsAuthoredEntityListJson(bytes)) return false;
            beforeBytes = bytes;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or JsonException or System.Security.SecurityException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsValidCurrentAuthoredEntityList(string root, string sourcePath, string expectedSha256)
    {
        try
        {
            EnsureNoLinks(root, sourcePath);
            var bytes = ReadBoundedFile(ResolveTarget(root, sourcePath), MaximumAuthoredSourceBytes);
            return string.Equals(AtomicFile.Sha256(bytes), expectedSha256, StringComparison.Ordinal)
                && IsAuthoredEntityListJson(bytes);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or JsonException or System.Security.SecurityException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsAuthoredEntityListJson(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Array) return true;
        if (element.ValueKind != JsonValueKind.Object) return false;

        JsonElement entities = default;
        var foundEntities = false;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, "entities", StringComparison.Ordinal)) continue;
            if (foundEntities) return false;
            entities = property.Value;
            foundEntities = true;
        }

        return foundEntities && entities.ValueKind == JsonValueKind.Array;
    }

    private static bool TryNormalizeRecoveryPath(string root, string path, out string normalized)
    {
        try
        {
            normalized = NormalizeRelativePath(path);
            _ = ResolveInternal(root, normalized);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            normalized = path;
            return false;
        }
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
        EnsureRestoreCapacity();
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
        EnsureRestoreCapacity();
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

    private void EnsureRestoreCapacity()
    {
        long requiredBytes = 1 << 20;
        checked
        {
            requiredBytes += 6L * (Owner.Length + _changes.Sum(change => (long)change.RelativePath.Length) + 512L * _changes.Count);
            foreach (var change in _changes)
                requiredBytes += Math.Max(ImageLength(change.BeforeImage), ImageLength(change.AfterImage));
        }
        EnsureDiskCapacity(_directory, requiredBytes, _changes.Select(change => ResolveTarget(change.RelativePath)));

        long ImageLength(string? name)
        {
            if (name is null) return 0;
            var path = Path.Combine(_directory, name);
            EnsureNoLinks(_root, Path.GetRelativePath(_root, path).Replace('\\', '/'), allowStudioMetadata: true);
            return new FileInfo(path).Length;
        }
    }

    private static void EnsureDiskCapacity(string metadataPath, long requiredBytes, IEnumerable<string> targets)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var drives = DriveInfo.GetDrives();
        var metadataDrive = DriveFor(metadataPath);
        foreach (var target in targets)
        {
            // shortcut: pilot writes require one volume; bucket capacities before supporting cross-volume transactions.
            if (!string.Equals(DriveFor(target).Name, metadataDrive.Name, comparison))
                throw new IOException("Запись приостановлена: файлы и журнал находятся на разных дисках. Проверьте состояние восстановления.");
        }
        if (metadataDrive.AvailableFreeSpace < requiredBytes)
            throw new IOException("Недостаточно свободного места для изменения и сохранения исходных версий. Файлы истории не удалялись.");

        DriveInfo DriveFor(string path)
        {
            path = Path.GetFullPath(path);
            while (!Directory.Exists(path))
                path = Path.GetDirectoryName(path) ?? throw new IOException("Не удалось определить диск для записи.");
            var drive = drives.Where(candidate =>
            {
                var name = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Name));
                var prefix = Path.EndsInDirectorySeparator(name) ? name : name + Path.DirectorySeparatorChar;
                return string.Equals(path, name, comparison) || path.StartsWith(prefix, comparison);
            }).OrderByDescending(candidate => candidate.Name.Length).FirstOrDefault();
            if (drive is null || !drive.IsReady)
                throw new IOException("Не удалось проверить свободное место на диске. Запись приостановлена; проверьте состояние восстановления.");
            return drive;
        }
    }

    private static IReadOnlyList<string> ConflictsForManifest(
        string root,
        string directory,
        Manifest manifest,
        Func<string, bool> allowedPath,
        string side,
        bool acceptEitherSide,
        int? maximumTargetBytes = null)
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
                var targetPath = ResolveTarget(root, normalized);
                if (Directory.Exists(targetPath)) throw new IOException($"Transaction target is a directory: {normalized}");
                var actual = !File.Exists(targetPath)
                    ? null
                    : maximumTargetBytes is { } limit
                        ? AtomicFile.Sha256(ReadBoundedFile(targetPath, limit))
                        : AtomicFile.Sha256OfFile(targetPath);
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

    private static IReadOnlyList<string> ValidateManifestImages(
        string root,
        string directory,
        Manifest manifest,
        int? maximumImageBytes = null)
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
                    var actualHash = maximumImageBytes is { } limit
                        ? AtomicFile.Sha256(ReadBoundedFile(imagePath, limit))
                        : AtomicFile.Sha256OfFile(imagePath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
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
        var bytes = IsBoundedSourceOwner(Owner)
            ? ReadBoundedFile(path, MaximumAuthoredSourceBytes)
            : File.ReadAllBytes(path);
        if (!string.Equals(AtomicFile.Sha256(bytes), hash, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Transaction image {Id}/{name} failed its SHA-256 receipt.");
        }

        return bytes;
    }

    private string? CurrentSha256(string relativePath) => CurrentSha256(_root, relativePath,
        IsBoundedSourceOwner(Owner) ? MaximumAuthoredSourceBytes : null);

    private static string? CurrentSha256(string root, string relativePath) => CurrentSha256(root, relativePath, null);

    private static string? CurrentSha256(string root, string relativePath, int? maximumBytes)
    {
        var path = ResolveTarget(root, relativePath);
        if (Directory.Exists(path)) throw new IOException($"Transaction target is a directory: {relativePath}");
        if (!File.Exists(path)) return null;
        return maximumBytes is { } limit
            ? AtomicFile.Sha256(ReadBoundedFile(path, limit))
            : AtomicFile.Sha256OfFile(path);
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
            ["acceptedOrder"] = _acceptedOrder,
            ["commandLabel"] = _commandLabel,
            ["files"] = files
        };
        var journalPath = Path.Combine(_directory, JournalName);
        EnsureNoLinks(_root, Path.GetRelativePath(_root, journalPath).Replace('\\', '/'), allowStudioMetadata: true);
        var text = manifest.ToJsonString();
        EnsureDiskCapacity(_directory, checked((1L << 20) + System.Text.Encoding.UTF8.GetByteCount(text)), []);
        AtomicFile.WriteAllText(journalPath, text);
    }

    private static IReadOnlyList<PreparedChange> Prepare(
        string root, IReadOnlyList<StudioFileWrite> writes, HashSet<string> allowlist, int? maximumSourceBytes)
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
            var before = File.Exists(path)
                ? maximumSourceBytes is { } maximum ? ReadBoundedFile(path, maximum) : File.ReadAllBytes(path)
                : null;
            var beforeSha = before is null ? null : AtomicFile.Sha256(before);
            if (!string.Equals(beforeSha, write.ExpectedSha256, StringComparison.Ordinal))
            {
                throw new StudioFileTransactionConflictException("new", [write.RelativePath]);
            }

            var after = write.Bytes;
            if (maximumSourceBytes is { } maximumAfter && after is { } afterBytes && afterBytes.LongLength > maximumAfter)
                throw new InvalidDataException("Transaction source exceeds the 8 MiB image budget.");
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

    public static void EnsureNoLinks(string root, string relativePath, bool allowStudioMetadata = false)
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

        long? order = null;
        if (node["acceptedOrder"] is not null)
        {
            if (node["acceptedOrder"] is not JsonValue orderNode || !orderNode.TryGetValue<long>(out var number) || number <= 0)
                throw new InvalidDataException("Transaction command order is invalid.");
            order = number;
        }
        var label = (string?)node["commandLabel"];
        if (label is not null && (string.IsNullOrWhiteSpace(label) || label.Length > 1024)
            || order is not null && label is null)
            throw new InvalidDataException("An ordered transaction command label is missing.");
        return new(id, (string?)node["owner"] ?? "", status, rollbackSide, targetSide, side, accepted, changes, order, label);
    }

    private static StudioFileTransaction FromManifest(string root, string directory, Manifest manifest, HashSet<string> allowlist, Action? afterPromotion) =>
        new(root, directory, manifest.Id, manifest.Owner, allowlist, manifest.Changes, afterPromotion, null,
            manifest.Side, accepted: manifest.Accepted) { _acceptedOrder = manifest.AcceptedOrder, _commandLabel = manifest.CommandLabel };

    private static bool IsSide(string side) => side is "before" or "after";

    private sealed record PreparedChange(string RelativePath, string? ExpectedSha256, byte[]? BeforeBytes, byte[]? AfterBytes, string? BeforeSha256, string? AfterSha256);
    private sealed record FileChange(string RelativePath, string? ExpectedSha256, string? BeforeSha256, string? AfterSha256, string? BeforeImage, string? AfterImage);
    private sealed record Manifest(string Id, string Owner, string Status, string RollbackSide, string TargetSide, string Side, bool Accepted, IReadOnlyList<FileChange> Changes, long? AcceptedOrder, string? CommandLabel);
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
