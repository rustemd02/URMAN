using System.Text.RegularExpressions;

namespace Urman.Core.Persistence;

public sealed partial class AtomicSaveGameStore
{
    private readonly string _directory;
    private readonly SaveGameV3Codec _codec;
    private readonly IReadOnlyCollection<SaveGameFingerprintCompatibility> _fingerprintCompatibility;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public AtomicSaveGameStore(
        string directory,
        SaveGameV3Codec? codec = null,
        IReadOnlyCollection<SaveGameFingerprintCompatibility>? fingerprintCompatibility = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _codec = codec ?? new SaveGameV3Codec();
        _fingerprintCompatibility = fingerprintCompatibility?.ToArray() ?? Array.Empty<SaveGameFingerprintCompatibility>();
    }

    public async Task SaveAsync(
        string slot,
        SaveGameV3 save,
        CancellationToken cancellationToken = default,
        Func<SaveGameV3, SaveGameV3>? prepareForLoad = null)
    {
        ValidateSlot(slot);
        var target = SlotPath(slot);
        var backup = BackupPath(slot);
        var temporary = Path.Combine(_directory, $".{slot}.{Guid.NewGuid():N}.tmp");

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            var bytes = _codec.Encode(save);
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (!File.Exists(target))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, target);
                return;
            }

            string? backupDestination = backup;
            try
            {
                await ReadCandidateAsync(target, save.CampaignFingerprint, prepareForLoad, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                // Preserve the last usable backup when replacing a damaged primary.
                backupDestination = null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(temporary, target, backupDestination, ignoreMetadataErrors: true);
        }
        finally
        {
            _writeGate.Release();
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<SaveLoadResult> LoadAsync(
        string slot,
        string expectedCampaignFingerprint,
        CancellationToken cancellationToken = default,
        Func<SaveGameV3, SaveGameV3>? prepareForLoad = null)
    {
        ValidateSlot(slot);
        Exception? primaryFailure = null;
        try
        {
            return new(await ReadCandidateAsync(SlotPath(slot), expectedCampaignFingerprint, prepareForLoad, cancellationToken), false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            primaryFailure = exception;
        }

        try
        {
            return new(await ReadCandidateAsync(BackupPath(slot), expectedCampaignFingerprint, prepareForLoad, cancellationToken), true);
        }
        catch (Exception backupFailure) when (backupFailure is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Neither the primary nor backup SaveGameV3 slot is readable.", new AggregateException(primaryFailure, backupFailure));
        }
    }

    private async Task<SaveGameV3> ReadCandidateAsync(string path, string expectedCampaignFingerprint,
        Func<SaveGameV3, SaveGameV3>? prepareForLoad, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var save = _codec.Decode(bytes, expectedCampaignFingerprint, _fingerprintCompatibility);
        return prepareForLoad is null ? save : prepareForLoad(save);
    }

    public string SlotPath(string slot)
    {
        ValidateSlot(slot);
        return Path.Combine(_directory, $"{slot}.savegame-v3.json");
    }

    public string BackupPath(string slot)
    {
        ValidateSlot(slot);
        return Path.Combine(_directory, $"{slot}.savegame-v3.backup.json");
    }

    private static void ValidateSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || !SlotPattern().IsMatch(slot))
        {
            throw new ArgumentException("Save slot may contain only lowercase letters, digits, dots, underscores and hyphens.", nameof(slot));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlotPattern();
}
