using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
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
    private readonly string _root = Path.GetFullPath(root);
    private readonly HashSet<string> _pendingArtifacts = new(OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public string Directory { get; } = Path.Combine(Path.GetFullPath(root), ".urman-studio", "drafts");

    public int Write(StudioWorkspace workspace, DateTimeOffset now)
    {
        EnsureWorkspace(workspace);
        GuardMetadata(Directory);
        var written = 0;
        foreach (var file in workspace.Files)
        {
            var draftPath = DraftPath(file.RelativePath);
            if (_pendingArtifacts.Contains(draftPath)) continue;
            StudioFileTransaction.EnsureNoLinks(_root, file.RelativePath);
            GuardMetadata(draftPath);
            GuardMetadata(draftPath + ".meta.json");
            if (!file.Dirty)
            {
                Discard(file.RelativePath);
                continue;
            }

            AtomicFile.WriteAllText(draftPath, file.Text);
            AtomicFile.WriteAllText(draftPath + ".meta.json", new JsonObject
            {
                ["relativePath"] = file.RelativePath,
                ["savedAt"] = now.ToString("O", CultureInfo.InvariantCulture),
                ["diskSha256"] = file.LoadedSha256
            }.ToJsonString());
            written++;
        }

        return written;
    }

    public IReadOnlyList<RecoveredDraft> Recover(StudioWorkspace workspace, out IReadOnlyList<string> warnings)
    {
        EnsureWorkspace(workspace);
        var problems = new List<string>();
        warnings = problems;
        string[] metadata;
        try
        {
            GuardMetadata(Directory);
            if (!System.IO.Directory.Exists(Directory)) return [];
            metadata = System.IO.Directory.EnumerateFiles(Directory, "*.meta.json").Order(StringComparer.Ordinal).ToArray();
        }
        catch (Exception error) when (IsDraftFailure(error))
        {
            problems.Add("Черновики недоступны; сохранённые артефакты оставлены без изменений.");
            return [];
        }

        var drafts = new List<RecoveredDraft>();
        foreach (var meta in metadata)
        {
            _pendingArtifacts.Add(meta[..^".meta.json".Length]);
            try
            {
                GuardMetadata(meta);
                var node = JsonNode.Parse(File.ReadAllText(meta)) as JsonObject
                    ?? throw new InvalidDataException();
                var relative = RequiredString(node, "relativePath");
                // Workspace membership is checked before any source path IO.
                if (!workspace.HasFile(relative)) throw new InvalidDataException();
                StudioFileTransaction.EnsureNoLinks(_root, relative);
                var draft = DraftPath(relative);
                if (!string.Equals(meta, draft + ".meta.json", PathComparison)) throw new InvalidDataException();
                GuardMetadata(draft);
                var diskSha = RequiredString(node, "diskSha256");
                if (diskSha.Length != 64 || diskSha.Any(character => !Uri.IsHexDigit(character))) throw new InvalidDataException();
                if (!DateTimeOffset.TryParseExact(RequiredString(node, "savedAt"), "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var savedAt)) throw new InvalidDataException();
                var text = File.ReadAllText(draft);
                var parsed = AuthoredFile.CreateNew(_root, relative, text);
                var keys = parsed.Keys().ToArray();
                if (parsed.RootIsArray != workspace.File(relative).RootIsArray
                    || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length || keys.Contains(EditSession.OrderKey)
                    || parsed.RootIsArray && keys.Any(key => parsed.Get(key) is not JsonObject entity
                        || entity["id"] is not JsonValue id || !id.TryGetValue<string>(out var value) || value != key))
                    throw new InvalidDataException();
                var full = Path.Combine(_root, relative);
                if (!File.Exists(full)) throw new InvalidDataException();
                var changed = !string.Equals(AtomicFile.Sha256OfFile(full), diskSha, StringComparison.OrdinalIgnoreCase);
                drafts.Add(new(relative, text, savedAt, changed, diskSha));
            }
            catch (Exception error) when (IsDraftFailure(error))
            {
                problems.Add("Один черновик не восстановлен: метаданные, источник или содержимое недоступны либо некорректны. Все его артефакты сохранены.");
            }
        }

        return drafts;
    }

    public void Discard(string relativePath)
    {
        StudioFileTransaction.EnsureNoLinks(_root, relativePath);
        var path = DraftPath(relativePath);
        if (_pendingArtifacts.Contains(path)) return;
        GuardMetadata(path);
        GuardMetadata(path + ".meta.json");
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".meta.json")) File.Delete(path + ".meta.json");
    }

    public void AcceptRecoveredDraft(string relativePath) => _pendingArtifacts.Remove(DraftPath(relativePath));

    private string DraftPath(string relativePath)
    {
        StudioFileTransaction.EnsureNoLinks(_root, relativePath);
        if (relativePath.Contains('~')) throw new ArgumentException("Draft paths cannot contain '~'.", nameof(relativePath));
        return Path.Combine(Directory, relativePath.Replace('/', '~') + ".draft");
    }

    private void GuardMetadata(string path) => StudioFileTransaction.EnsureNoLinks(_root,
        Path.GetRelativePath(_root, path).Replace('\\', '/'), allowStudioMetadata: true);

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private void EnsureWorkspace(StudioWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (!string.Equals(_root, Path.GetFullPath(workspace.Root), PathComparison))
            throw new ArgumentException("Draft store and workspace must have the same root.", nameof(workspace));
    }

    private static string RequiredString(JsonObject node, string key) =>
        node[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text : throw new InvalidDataException();

    private static bool IsDraftFailure(Exception error) => error is IOException or UnauthorizedAccessException
        or JsonException or ArgumentException or InvalidOperationException or FormatException;
}
