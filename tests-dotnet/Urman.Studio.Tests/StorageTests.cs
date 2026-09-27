using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;
using Xunit;

namespace Urman.Studio.Tests;

// P1.1: Studio edits real authored files without reformatting them, writes
// atomically, notices outside edits and keeps undo from clobbering them.
public sealed class StorageTests : IDisposable
{
    private readonly string _root;

    public StorageTests()
    {
        // A private copy of the authored sources; the checkout is never written.
        var repo = RepoRoot();
        _root = Path.Combine(Path.GetTempPath(), "urman-studio-tests", Guid.NewGuid().ToString("N"));
        foreach (var folder in new[] { "content/modules", "content/campaigns", "game/content/world" })
        {
            CopyDirectory(Path.Combine(repo, folder), Path.Combine(_root, folder));
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void EveryAuthoredFileRoundTripsByteForByte()
    {
        var workspace = StudioWorkspace.Open(_root);
        Assert.True(workspace.Files.Count > 20);
        foreach (var file in workspace.Files)
        {
            Assert.Equal(File.ReadAllText(file.FullPath), file.Text);
            Assert.False(file.Dirty, file.RelativePath);
        }
    }

    [Fact]
    public void EditingOneEntityChangesOnlyItsOwnLinesInEveryFileStyle()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        // Indented module (tamara-fence.json), compact one-entity-per-line module
        // (investigation-family.json) and an object-root world plot.
        foreach (var id in new[]
                 {
                     "urman.chapter1:text/tamara-hand-fit",
                     FirstIdIn(workspace, "content/modules/urman-chapter1/investigation-family.json"),
                     "urman.world:act1/tamara-plot/board-03"
                 })
        {
            var address = workspace.Locate(id)!;
            var before = File.ReadAllText(workspace.File(address.RelativePath).FullPath).Split('\n');
            var entity = workspace.Get(id)!.AsObject();
            entity["studioNote"] = "проверка";
            session.Set(address.RelativePath, address.Key, entity, "пометка");
            var after = workspace.File(address.RelativePath).Text.Split('\n');
            var changed = DiffLineCount(before, after);
            var entityLines = before.Count(line => line.Contains(id, StringComparison.Ordinal)) == 1 &&
                              before.Single(line => line.Contains(id, StringComparison.Ordinal)).TrimStart().StartsWith('{')
                ? 1
                : 30;
            Assert.True(changed >= 1 && changed <= entityLines + 2, $"{address.RelativePath}: {changed} lines changed");
            Assert.Equal("проверка", (string)workspace.Get(id)!["studioNote"]!);
            Assert.NotNull(JsonNode.Parse(workspace.File(address.RelativePath).Text));
        }
    }

    [Fact]
    public void AddingAndRemovingEntitiesKeepsTheFileValidAndItsStyle()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        const string path = "content/modules/urman-chapter1/tamara-fence.json";
        var id = workspace.NewId("urman.chapter1", "text", "Новая реплика");
        Assert.Matches("^urman\\.chapter1:text/novaya-replika-[0-9a-f]{8}$", id);
        session.Set(path, id, new JsonObject { ["schemaVersion"] = 1, ["id"] = id, ["value"] = new JsonObject { ["default"] = "Ну?" }, ["purpose"] = "ui" }, "реплика");
        var text = workspace.File(path).Text;
        Assert.NotNull(JsonNode.Parse(text));
        Assert.Contains("\n  \"id\": \"" + id + "\"", text, StringComparison.Ordinal);
        Assert.Equal(id, workspace.Locate(id)!.Key);

        session.Undo();
        Assert.Equal(File.ReadAllText(Path.Combine(_root, path)), workspace.File(path).Text);

        session.Delete(path, "urman.chapter1:character/tamara", "удалить");
        Assert.NotNull(JsonNode.Parse(workspace.File(path).Text));
        Assert.Null(workspace.Get("urman.chapter1:character/tamara"));
        session.Undo();
        Assert.NotNull(workspace.Get("urman.chapter1:character/tamara"));
    }

    [Fact]
    public void FailedWriteKeepsThePreviousWholeVersionAndReportsTheError()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var address = workspace.Locate("urman.chapter1:text/tamara-hand-fit")!;
        var file = workspace.File(address.RelativePath);
        var original = File.ReadAllBytes(file.FullPath);
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "реплика");

        AtomicFile.FaultInjection = _ => throw new IOException("No space left on device");
        try
        {
            Assert.Throws<IOException>(file.Save);
        }
        finally
        {
            AtomicFile.FaultInjection = null;
        }

        Assert.Equal(original, File.ReadAllBytes(file.FullPath));
        Assert.True(file.Dirty);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(file.FullPath)!, "*.tmp"));
        file.Save();
        Assert.Contains("Эти годятся.", File.ReadAllText(file.FullPath), StringComparison.Ordinal);
        Assert.False(file.Dirty);
    }

    [Fact]
    public void OutsideEditOfAnotherEntityMergesIntoUnsavedWork()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var file = workspace.File("content/modules/urman-chapter1/tamara-fence.json");
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");

        // A developer edits a different line in the same file on disk.
        var disk = File.ReadAllText(file.FullPath).Replace("Доски где?", "Доски-то где?", StringComparison.Ordinal);
        File.WriteAllText(file.FullPath, disk);
        Assert.Throws<ExternalChangeException>(file.Save);

        var change = file.PullFromDisk();
        Assert.True(change.Reloaded);
        Assert.Empty(change.Conflicts);
        Assert.Contains("Доски-то где?", file.Text, StringComparison.Ordinal);
        Assert.Contains("Эти годятся.", file.Text, StringComparison.Ordinal);
        file.Save();
        Assert.Contains("Эти годятся.", File.ReadAllText(file.FullPath), StringComparison.Ordinal);
    }

    [Fact]
    public void OutsideEditOfTheSameFieldIsAConflictAndLocalWorkStays()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var file = workspace.File("content/modules/urman-chapter1/tamara-fence.json");
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");
        File.WriteAllText(file.FullPath, File.ReadAllText(file.FullPath).Replace("\"Эти подойдут.\"", "\"Эти сойдут.\"", StringComparison.Ordinal));

        var change = file.PullFromDisk();

        Assert.False(change.Reloaded);
        Assert.Contains(change.Conflicts, conflict => conflict.Path.Contains("tamara-hand-fit", StringComparison.Ordinal));
        Assert.Contains("Эти годятся.", file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UndoDoesNotRollBackSomeoneElsesLaterChange()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var file = workspace.File("content/modules/urman-chapter1/tamara-fence.json");
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");
        file.Save();
        // Another author's revision arrives and changes the same entity.
        File.WriteAllText(file.FullPath, File.ReadAllText(file.FullPath).Replace("Эти годятся.", "Эти годятся, Айдар.", StringComparison.Ordinal));
        file.PullFromDisk();

        var error = Assert.Throws<UndoConflictException>(session.Undo);
        Assert.Single(error.Blocked);
        Assert.Contains("Эти годятся, Айдар.", file.Text, StringComparison.Ordinal);
        Assert.True(session.CanUndo);
    }

    [Fact]
    public void ADragIsOneUndoStep()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        const string board = "urman.world:act1/tamara-plot/board-03";
        var start = workspace.Get(board)!.ToJsonString();
        using (session.Begin("переместить доску"))
        {
            for (var step = 1; step <= 20; step++)
            {
                session.SetField(board, ["params", "position"], new JsonArray(14.5 + step * .05, 0.0, -47.4), "шаг");
            }
        }

        Assert.Equal("переместить доску", session.UndoLabel);
        session.Undo();
        Assert.Equal(start, workspace.Get(board)!.ToJsonString());
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void DraftsSurviveACrashAndPointAtTheLastSavedVersion()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");
        var drafts = new DraftStore(_root);
        Assert.Equal(1, drafts.Write(workspace, DateTimeOffset.Parse("2026-09-27T12:00:00Z")));

        // "Crash": a fresh workspace sees the saved file and the recovered draft.
        var recovered = Assert.Single(new DraftStore(_root).Recover());
        Assert.Equal("content/modules/urman-chapter1/tamara-fence.json", recovered.RelativePath);
        Assert.Contains("Эти годятся.", recovered.DraftText, StringComparison.Ordinal);
        Assert.False(recovered.DiskChangedSince);
        Assert.DoesNotContain("Эти годятся.", File.ReadAllText(Path.Combine(_root, recovered.RelativePath)), StringComparison.Ordinal);
    }

    [Fact]
    public void AConflictIsResolvedFieldByFieldAndKeepsOtherLocalWork()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var file = workspace.File("content/modules/urman-chapter1/tamara-fence.json");
        session.SetField("urman.chapter1:text/tamara-hand-fit", ["value", "default"], "Эти годятся.", "моя правка");
        session.SetField("urman.chapter1:text/tamara-hand-stack", ["value", "default"], "Сложи вот тут.", "вторая правка");
        File.WriteAllText(file.FullPath, File.ReadAllText(file.FullPath).Replace("\"Эти подойдут.\"", "\"Эти сойдут.\"", StringComparison.Ordinal));

        var change = file.PullFromDisk();
        var conflict = Assert.Single(change.Conflicts);
        var resolved = Urman.Studio.Core.Collaboration.ConflictResolution.Apply(change.Merge!,
            new Dictionary<Urman.Studio.Core.Collaboration.MergeConflict, Urman.Studio.Core.Collaboration.ConflictChoice> { [conflict] = Urman.Studio.Core.Collaboration.ConflictChoice.TakeTheirs });
        file.ApplyResolution(resolved, change.TheirBytes!);

        Assert.Contains("Эти сойдут.", file.Text, StringComparison.Ordinal);
        Assert.Contains("Сложи вот тут.", file.Text, StringComparison.Ordinal);
        file.Save();
        Assert.Contains("Сложи вот тут.", File.ReadAllText(file.FullPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ReorderingIsOneUndoableStepAndKeepsTheFileStyle()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        const string path = "game/content/world/act1_tamara_plot.world.v1.json";
        var original = workspace.File(path).Text;
        var keys = workspace.File(path).Keys().ToList();
        var reversed = Enumerable.Reverse(keys).ToList();
        session.SetOrder(path, reversed, "порядок");
        Assert.Equal(reversed, workspace.File(path).Keys());
        Assert.NotNull(System.Text.Json.Nodes.JsonNode.Parse(workspace.File(path).Text));
        session.Undo();
        Assert.Equal(original, workspace.File(path).Text);
        session.Redo();
        Assert.Equal(reversed, workspace.File(path).Keys());
    }

    private static string FirstIdIn(StudioWorkspace workspace, string relativePath) =>
        workspace.File(relativePath).Keys().First();

    private static int DiffLineCount(string[] before, string[] after)
    {
        var prefix = 0;
        while (prefix < before.Length && prefix < after.Length && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < after.Length - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        return Math.Max(before.Length, after.Length) - prefix - suffix;
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Urman.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Urman.slnx not found.");
    }
}
