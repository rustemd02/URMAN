using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.Core.Storage;

/// <summary>An unsaved file found after Studio stopped unexpectedly.</summary>
public sealed record RecoveredDraft(string RelativePath, string DraftText, DateTimeOffset SavedAt, bool DiskChangedSince, string DiskSha256AtDraft);

/// <summary>
/// Autosaved drafts (spec SAVE02, SAVE06). Dirty files are written as whole
/// texts to <c>.urman-studio/drafts/</c> in the checkout, beside a small
/// manifest; the authored files themselves change only on an explicit or
/// timed save. After a crash the drafts are offered next to the last saved
/// version; the author does not have to hunt for temporary files.
/// </summary>
public sealed class DraftStore(string root)
{
    public string Directory { get; } = Path.Combine(root, ".urman-studio", "drafts");

    public int Write(StudioWorkspace workspace, DateTimeOffset now)
    {
        var written = 0;
        foreach (var file in workspace.Files)
        {
            var draftPath = DraftPath(file.RelativePath);
            if (!file.Dirty)
            {
                Discard(file.RelativePath);
                continue;
            }

            AtomicFile.WriteAllText(draftPath, file.Text);
            AtomicFile.WriteAllText(draftPath + ".meta.json", new JsonObject
            {
                ["relativePath"] = file.RelativePath,
                ["savedAt"] = now.ToString("O"),
                ["diskSha256"] = file.LoadedSha256
            }.ToJsonString());
            written++;
        }

        return written;
    }

    public IReadOnlyList<RecoveredDraft> Recover()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var drafts = new List<RecoveredDraft>();
        foreach (var meta in System.IO.Directory.EnumerateFiles(Directory, "*.meta.json").Order(StringComparer.Ordinal))
        {
            var draft = meta[..^".meta.json".Length];
            if (!File.Exists(draft)) continue; // a manifest without its draft cannot be recovered
            var node = JsonNode.Parse(File.ReadAllText(meta))!.AsObject();
            var relative = (string)node["relativePath"]!;
            var diskSha = (string)node["diskSha256"]!;
            var full = Path.Combine(root, relative);
            var changed = !File.Exists(full) || AtomicFile.Sha256OfFile(full) != diskSha;
            drafts.Add(new(relative, File.ReadAllText(meta[..^".meta.json".Length]), DateTimeOffset.Parse((string)node["savedAt"]!), changed, diskSha));
        }

        return drafts;
    }

    public void Discard(string relativePath)
    {
        var path = DraftPath(relativePath);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".meta.json")) File.Delete(path + ".meta.json");
    }

    private string DraftPath(string relativePath) =>
        Path.Combine(Directory, relativePath.Replace('/', '~') + ".draft");
}
