using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Play;

/// <summary>One test run's own folder and the files it owns.</summary>
public sealed record RunFolder(string RunId, string Directory, string LogPath);

/// <summary>
/// File discipline for test runs (spec §21): every run gets its own folder
/// under <c>.urman-studio/runs/</c> with a manifest naming the files it owns.
/// Pruning removes only files listed in a run's manifest, only for unpinned
/// runs beyond the newest <see cref="Keep"/>; nothing else is ever deleted.
/// </summary>
public sealed class RunFolders(string root)
{
    public const int Keep = 10;
    public string Directory { get; } = Path.Combine(root, ".urman-studio", "runs");

    public RunFolder Create(
        string revisionLabel,
        DateTimeOffset now,
        string? campaignFingerprint = null,
        IReadOnlyDictionary<string, string>? houseSources = null)
    {
        var runId = FormattableString.Invariant($"run-{now:yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}");
        var manifest = new JsonObject
        {
            ["runId"] = runId,
            ["revision"] = revisionLabel,
            ["started"] = now.ToString("O"),
            ["pinned"] = false,
            ["files"] = new JsonArray("game.log", "manifest.json")
        };
        if (campaignFingerprint is not null || houseSources is not null)
        {
            if (campaignFingerprint is not null && !IsCanonicalSha256(campaignFingerprint))
            {
                throw new ArgumentException("Campaign fingerprint must be 64 lowercase hexadecimal characters.", nameof(campaignFingerprint));
            }

            var sourceMap = new JsonObject();
            if (houseSources is not null)
            {
                foreach (var (path, hash) in houseSources.OrderBy(source => source.Key, StringComparer.Ordinal))
                {
                    if (!IsCanonicalHouseSourcePath(path))
                    {
                        throw new ArgumentException("House source paths must be canonical project-relative game/ paths.", nameof(houseSources));
                    }

                    if (!IsCanonicalSha256(hash))
                    {
                        throw new ArgumentException("House source hashes must be 64 lowercase hexadecimal characters.", nameof(houseSources));
                    }

                    sourceMap[path] = hash;
                }
            }

            manifest["evidenceKind"] = "prelaunch-source";
            if (campaignFingerprint is not null)
            {
                manifest["campaignFingerprint"] = campaignFingerprint;
            }

            if (houseSources is not null)
            {
                manifest["houseSources"] = sourceMap;
            }
        }

        var directory = Path.Combine(Directory, runId);
        System.IO.Directory.CreateDirectory(directory);
        var log = Path.Combine(directory, "game.log");
        AtomicFile.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToJsonString());
        Prune();
        return new(runId, directory, log);
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsCanonicalHouseSourcePath(string? path) =>
        path is not null
        && !Path.IsPathRooted(path)
        && path.StartsWith("game/", StringComparison.Ordinal)
        && !path.Contains('\\')
        && path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));

    public int Prune()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return 0;
        }

        var runs = System.IO.Directory.EnumerateDirectories(Directory, "run-*")
            .Select(directory => (directory, manifest: Path.Combine(directory, "manifest.json")))
            .Where(run => File.Exists(run.manifest))
            .Select(run => (run.directory, node: JsonNode.Parse(File.ReadAllText(run.manifest))!.AsObject()))
            .Where(run => run.node["pinned"]?.GetValue<bool>() != true)
            .OrderByDescending(run => (string)run.node["started"]!, StringComparer.Ordinal)
            .Skip(Keep)
            .ToArray();
        foreach (var (directory, node) in runs)
        {
            foreach (var file in node["files"]!.AsArray().Select(item => (string)item!))
            {
                var path = Path.GetFullPath(Path.Combine(directory, file));
                if (path.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            if (!System.IO.Directory.EnumerateFileSystemEntries(directory).Any())
            {
                System.IO.Directory.Delete(directory);
            }
        }

        return runs.Length;
    }
}
