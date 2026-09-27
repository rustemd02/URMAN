using System.Security.Cryptography;
using System.Text;

namespace Urman.Studio.Core.Storage;

/// <summary>
/// Crash-safe file writes (spec SAVE03): the new content goes to a temporary
/// file in the same directory, is flushed to disk and then renamed over the
/// target. A failure at any step leaves the previous complete version in place
/// and surfaces as an exception; nothing reports "saved" before the rename.
/// </summary>
public static class AtomicFile
{
    /// <summary>Test hook: throw from inside the write to simulate a full disk or lost permission.</summary>
    public static Action<string>? FaultInjection { get; set; }

    public static void WriteAllText(string path, string text) => WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.studio-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                FaultInjection?.Invoke(path);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Sha256OfFile(string path) => Sha256(File.ReadAllBytes(path));
}
