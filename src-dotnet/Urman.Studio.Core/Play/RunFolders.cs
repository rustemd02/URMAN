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

    public RunFolder Create(string revisionLabel, DateTimeOffset now)
    {
        var runId = $"run-{now:yyyyMMdd-HHmmss}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";
        var directory = Path.Combine(Directory, runId);
        System.IO.Directory.CreateDirectory(directory);
        var log = Path.Combine(directory, "game.log");
        AtomicFile.WriteAllText(Path.Combine(directory, "manifest.json"), new JsonObject
        {
            ["runId"] = runId,
            ["revision"] = revisionLabel,
            ["started"] = now.ToString("O"),
            ["pinned"] = false,
            ["files"] = new JsonArray("game.log", "manifest.json")
        }.ToJsonString());
        Prune();
        return new(runId, directory, log);
    }

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
