using System.Text.RegularExpressions;

namespace Urman.Core.Persistence;

public sealed partial class AtomicSaveGameStore
{
    private readonly string _directory;
    private readonly SaveGameV3Codec _codec;

    public AtomicSaveGameStore(string directory, SaveGameV3Codec? codec = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _codec = codec ?? new SaveGameV3Codec();
    }

    public async Task SaveAsync(string slot, SaveGameV3 save, CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);
        Directory.CreateDirectory(_directory);
        var target = SlotPath(slot);
        var backup = BackupPath(slot);
        var temporary = Path.Combine(_directory, $".{slot}.{Guid.NewGuid():N}.tmp");
        var bytes = _codec.Encode(save);

        try
        {
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

            if (File.Exists(target))
            {
                File.Replace(temporary, target, backup, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporary, target);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<SaveLoadResult> LoadAsync(
        string slot,
        string expectedCampaignFingerprint,
        CancellationToken cancellationToken = default)
    {
        ValidateSlot(slot);
        Exception? primaryFailure = null;
        try
        {
            var primary = await File.ReadAllBytesAsync(SlotPath(slot), cancellationToken);
            return new(_codec.Decode(primary, expectedCampaignFingerprint), false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            primaryFailure = exception;
        }

        try
        {
            var backup = await File.ReadAllBytesAsync(BackupPath(slot), cancellationToken);
            return new(_codec.Decode(backup, expectedCampaignFingerprint), true);
        }
        catch (Exception backupFailure) when (backupFailure is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("Neither the primary nor backup SaveGameV3 slot is readable.", new AggregateException(primaryFailure, backupFailure));
        }
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
