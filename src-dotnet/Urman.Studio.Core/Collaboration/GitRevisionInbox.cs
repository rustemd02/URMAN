using System.Diagnostics;
using System.Text;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.Collaboration;

public sealed record GitIncomingFile(string RelativePath, string? BaseText, string? IncomingText);

public sealed record GitIncomingRevision(string OperationId, string BaseRevision, string IncomingRevision,
    string LocalRevision, string Branch, IReadOnlyList<GitIncomingFile> Files, IReadOnlyList<string> ExcludedPaths);

/// <summary>Receive into a private Git ref for data-only preview, without changing HEAD, index or authored files.</summary>
public sealed class GitRevisionInbox(StudioWorkspace workspace,
    Func<IReadOnlyList<StudioCheckpointCatalogSource>>? catalogSources = null)
{
    private const string HeroRecipePath = "game/content/studio/hero_house.recipe.json";
    private const int ContentBudget = 8 * 1024 * 1024;
    private const int PathBudget = 256 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public async Task<GitIncomingRevision> ReceiveAsync(string branch, CancellationToken token = default)
    {
        ValidateBranch(branch);
        token.ThrowIfCancellationRequested();
        var operation = Guid.NewGuid().ToString("N");
        var inboxRef = "refs/urman-studio/inbox/" + operation;
        var receiveStarted = false;
        try
        {
            if (!Directory.Exists(Path.Combine(workspace.Root, ".git")) && !File.Exists(Path.Combine(workspace.Root, ".git")))
                throw Refusal();
            // Snapshot provider descriptors on the caller thread before the first Git await.
            // Catalogue Capture callbacks read live form state and are not needed by inbox.
            var ownedCatalogSources = SnapshotCatalogSources();
            var catalogValidators = ownedCatalogSources.ToDictionary(source => source.Path, StringComparer.Ordinal);
            var allowed = workspace.Files.Select(file => file.RelativePath).ToHashSet(StringComparer.Ordinal);
            StudioFileTransaction.EnsureNoLinks(workspace.Root, HeroRecipePath);
            if (File.Exists(Path.Combine(workspace.Root, HeroRecipePath))) allowed.Add(HeroRecipePath);
            foreach (var source in ownedCatalogSources) allowed.Add(source.Path);
            foreach (var path in allowed)
            {
                ValidatePath(path);
                StudioFileTransaction.EnsureNoLinks(workspace.Root, path);
            }

            var local = await RevisionAsync(["rev-parse", "--verify", "HEAD^{commit}"], token).ConfigureAwait(false);
            // shortcut: trusted origin may transfer larger history before the preview cap; add a transport budget before accepting arbitrary remotes.
            receiveStarted = true;
            await RunAsync(["-c", "fetch.writeCommitGraph=false", "fetch", "--no-tags", "--no-write-fetch-head",
                "--no-recurse-submodules", "--no-auto-maintenance", "--no-prune", "--no-prune-tags", "--refmap=",
                "origin", "refs/heads/" + branch + ":" + inboxRef],
                4096, token).ConfigureAwait(false);
            var incoming = await RevisionAsync(["rev-parse", "--verify", inboxRef + "^{commit}"], token).ConfigureAwait(false);
            var ancestor = await RevisionAsync(["merge-base", local, incoming], token).ConfigureAwait(false);
            var names = await RunAsync(["diff", "--no-ext-diff", "--no-textconv", "--name-only", "-z", "--no-renames", ancestor, incoming, "--"],
                PathBudget, token).ConfigureAwait(false);
            var paths = NulFields(names);
            if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length) throw Refusal();
            var files = new List<GitIncomingFile>();
            var excluded = new List<string>();
            var remaining = ContentBudget;
            foreach (var path in paths.Order(StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                ValidatePath(path);
                var registeredPath = allowed.Contains(path);
                var possibleNewSource = !registeredPath && !catalogValidators.ContainsKey(path)
                    && StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(path);
                if (!registeredPath && !possibleNewSource)
                {
                    excluded.Add(path);
                    continue;
                }

                if (possibleNewSource)
                {
                    StudioFileTransaction.EnsureNoLinks(workspace.Root, path);
                    var fullPath = Path.Combine(workspace.Root, path.Replace('/', Path.DirectorySeparatorChar));
                    if (workspace.HasFile(path) || File.Exists(fullPath) || Directory.Exists(fullPath))
                    {
                        excluded.Add(path);
                        continue;
                    }
                }

                var before = await ReadSourceAsync(ancestor, path, remaining, catalogValidators, token).ConfigureAwait(false);
                remaining -= before.Bytes;
                var after = await ReadSourceAsync(incoming, path, remaining, catalogValidators, token).ConfigureAwait(false);
                remaining -= after.Bytes;

                var incomingAddsSource = before.Text is null && after.Text is not null;
                if (incomingAddsSource && possibleNewSource)
                {
                    // A source can be created only from a pure Git addition while both the
                    // workspace and disk still have no local identity for that path.
                    StudioFileTransaction.EnsureNoLinks(workspace.Root, path);
                    var fullPath = Path.Combine(workspace.Root, path.Replace('/', Path.DirectorySeparatorChar));
                    if (!StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(path) || catalogValidators.ContainsKey(path)
                        || workspace.HasFile(path) || File.Exists(fullPath) || Directory.Exists(fullPath)
                        || !AuthoredFile.CreateNew(workspace.Root, path, after.Text!).RootIsArray)
                    {
                        excluded.Add(path);
                        continue;
                    }
                }
                else if (possibleNewSource)
                {
                    excluded.Add(path);
                    continue;
                }

                files.Add(new GitIncomingFile(path, before.Text, after.Text));
            }
            token.ThrowIfCancellationRequested();
            if (await RevisionAsync(["rev-parse", "--verify", "HEAD^{commit}"], token).ConfigureAwait(false) != local) throw Refusal();
            return new GitIncomingRevision(operation, ancestor, incoming, local, branch,
                files.AsReadOnly(), excluded.AsReadOnly());
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or FormatException or OverflowException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            throw Refusal();
        }
        finally
        {
            if (receiveStarted)
            {
                // The preview owns decoded text, so it does not need to pin Git objects.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await RunAsync(["update-ref", "--no-deref", "-d", inboxRef], 4096, cleanup.Token).ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or OperationCanceledException or InvalidOperationException
                    or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
            }
        }
    }

    private async Task<(string? Text, int Bytes)> ReadSourceAsync(string revision, string path, int remaining,
        IReadOnlyDictionary<string, StudioCheckpointCatalogSource> catalogValidators, CancellationToken token)
    {
        var records = NulFields(await RunAsync(["ls-tree", "-z", revision, "--", path], PathBudget, token).ConfigureAwait(false));
        if (records.Length == 0) return (null, 0);
        if (records.Length != 1) throw Refusal();
        var entry = records[0];
        var tab = entry.IndexOf('\t');
        if (tab < 0 || entry[(tab + 1)..] != path) throw Refusal();
        var fields = entry[..tab].Split(' ');
        if (fields.Length != 3 || fields[0] != "100644" || fields[1] != "blob" || !IsHash(fields[2])) throw Refusal();
        // Read the exact object pinned by ls-tree, never a working-tree file or a filtered checkout.
        var bytes = await RunAsync(["cat-file", "blob", fields[2]], remaining, token).ConfigureAwait(false);
        if (catalogValidators.TryGetValue(path, out var catalog)) catalog.Validate(bytes);
        var text = Utf8.GetString(bytes);
        if (catalog is null) _ = AuthoredFile.CreateNew(workspace.Root, path, text).Snapshot();
        return (text, bytes.Length);
    }

    private StudioCheckpointCatalogSource[] SnapshotCatalogSources()
    {
        if (catalogSources is null) return [];
        var sources = catalogSources()?.ToArray() ?? throw Refusal();
        if (sources.Length != 2) throw Refusal();
        var seenOwnersAndPaths = new HashSet<(string Owner, string Path)>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source is null || source.Owner is null || source.Path is null || source.Capture is null
                || source.Validate is null || workspace.HasFile(source.Path)
                || !StudioFileTransaction.IsOwnedCatalogPath(source.Owner, source.Path)
                || !seenOwnersAndPaths.Add((source.Owner, source.Path)) || !seenPaths.Add(source.Path))
                throw Refusal();
            ValidatePath(source.Path);
            StudioFileTransaction.EnsureNoLinks(workspace.Root, source.Path);
        }
        return sources.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray();
    }

    private async Task<string> RevisionAsync(string[] arguments, CancellationToken token)
    {
        var revision = Utf8.GetString(await RunAsync(arguments, 4096, token).ConfigureAwait(false)).Trim();
        if (!IsHash(revision)) throw Refusal();
        return revision;
    }

    private Task<byte[]> RunAsync(string[] arguments, int outputLimit, CancellationToken token) =>
        GitRepositoryProcess.RunAsync(workspace.Root, arguments, outputLimit, token);

    private static string[] NulFields(byte[] bytes)
    {
        if (bytes.Length == 0) return [];
        var text = Utf8.GetString(bytes);
        if (!text.EndsWith('\0')) throw Refusal();
        return text[..^1].Split('\0');
    }

    internal static bool IsHash(string value) => value.Length is 40 or 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static void ValidateBranch(string branch)
    {
        if (string.IsNullOrEmpty(branch) || branch.Length > 256 || branch[0] == '-'
            || branch.Contains("..", StringComparison.Ordinal)
            || branch.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/'))
            || branch.Split('/').Any(part => part.Length == 0 || part.StartsWith('.') || part.EndsWith('.')
                || part.EndsWith(".lock", StringComparison.Ordinal))) throw new ArgumentException("Недопустимое имя ветки.", nameof(branch));
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':')
            || path.Any(char.IsControl) || path.Split('/').Any(part => part.Length == 0 || part is "." or "..")) throw Refusal();
    }

    private static IOException Refusal() => new("Получение ревизии не завершено: проверьте Git, ветку, доступ и допустимый объём исходников. Рабочие файлы не изменены.");
}

internal static class GitRepositoryProcess
{
    internal static async Task<byte[]> RunAsync(string root, IReadOnlyList<string> arguments, int outputLimit,
        CancellationToken token, byte[]? standardInput = null, IReadOnlyDictionary<string, string?>? environmentOverrides = null)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null
        };
        var hooksPath = Path.Combine(Path.GetTempPath(), "urman-studio-empty-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(hooksPath);
        if (Directory.EnumerateFileSystemEntries(hooksPath).Any())
        {
            Directory.Delete(hooksPath);
            throw Refusal();
        }
        // Give every Git child an owned empty hooks directory. This covers receive fetch/ref cleanup as well as outbox plumbing.
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("core.hooksPath=" + Path.GetFullPath(hooksPath).Replace('\\', '/'));
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("commit.gpgsign=false");
        start.ArgumentList.Add("--literal-pathspecs");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var name in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_OBJECT_DIRECTORY",
            "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_NAMESPACE", "GIT_INDEX_FILE", "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT" })
            start.Environment.Remove(name);
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("GIT_CONFIG_KEY_", StringComparison.Ordinal)
            || name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.Ordinal)).ToArray()) start.Environment.Remove(name);
        start.Environment["GIT_NO_LAZY_FETCH"] = "1";
        start.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "Never";
        if (environmentOverrides is not null)
            foreach (var (name, value) in environmentOverrides)
                if (value is null) start.Environment.Remove(name); else start.Environment[name] = value;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            using var process = Process.Start(start) ?? throw Refusal();
            try
            {
                var output = ReadBoundedAsync(process.StandardOutput.BaseStream, outputLimit, timeout);
                var errors = DiscardAsync(process.StandardError.BaseStream, timeout.Token);
                var input = standardInput is null ? Task.CompletedTask : WriteInputAsync(process.StandardInput.BaseStream, standardInput, timeout.Token);
                await Task.WhenAll(process.WaitForExitAsync(timeout.Token), output, errors, input).ConfigureAwait(false);
                if (process.ExitCode != 0) throw Refusal();
                return await output.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw Refusal(); }
            finally
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception
                        or OperationCanceledException) { }
                }
            }
        }
        finally
        {
            try { Directory.Delete(hooksPath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private static async Task WriteInputAsync(Stream stream, byte[] bytes, CancellationToken token)
    {
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
        stream.Close();
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationTokenSource timeout)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > limit)
            {
                timeout.Cancel();
                throw Refusal();
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    private static async Task DiscardAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        while (await stream.ReadAsync(buffer, token).ConfigureAwait(false) != 0) { }
    }

    private static IOException Refusal() => new("Операция Git не завершена. Проверьте доступ и повторите действие; рабочие файлы не изменены.");
}
