using System.Text.Json.Nodes;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Storage;
using Xunit;

namespace Urman.Studio.Tests;

// CONTENT editors: documents, old-PC chats and hints are Markdown sources;
// Studio edits them as entities and writes back only what changed.
public sealed class MarkdownSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urman-studio-tests", Guid.NewGuid().ToString("N"));

    public MarkdownSourceTests()
    {
        var repo = RepoRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repo, "content/modules"), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, "content/modules", Path.GetRelativePath(Path.Combine(repo, "content/modules"), file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void DocumentsChatsAndHintsAreIndexedAndRoundTrip()
    {
        var workspace = StudioWorkspace.Open(_root);
        var sources = workspace.Files.Where(file => file.RelativePath.EndsWith(".md", StringComparison.Ordinal)).ToArray();
        Assert.True(sources.Length >= 60, $"{sources.Length} Markdown sources");
        Assert.DoesNotContain(sources, file => file.RelativePath.EndsWith("README.md", StringComparison.Ordinal) || file.RelativePath.Contains("HANDOFF", StringComparison.Ordinal));
        foreach (var file in sources)
        {
            Assert.Equal(File.ReadAllText(file.FullPath).Replace("\r\n", "\n", StringComparison.Ordinal), file.Text);
        }

        var document = workspace.Get("urman.chapter1:document/arrival-mother-message")!.AsObject();
        Assert.Equal("Сообщение от мамы", (string)document["title"]!["default"]!);
        Assert.StartsWith("# Мама", (string)document[MarkdownSource.BodyKey]!, StringComparison.Ordinal);
        Assert.Equal("Документ", new EntityCatalog(workspace).Describe("urman.chapter1:document/arrival-mother-message").KindLabel);
    }

    [Fact]
    public void EditingTitleAndBodyRewritesOnlyThoseAndUndoRestoresTheFile()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        const string id = "urman.chapter1:document/arrival-mother-message";
        var file = workspace.File(workspace.Locate(id)!.RelativePath);
        var before = file.Text;

        session.SetField(id, ["title", "translations", "ru"], "Сообщение мамы", "заголовок");
        var body = (string)workspace.Get(id)![MarkdownSource.BodyKey]!;
        session.SetField(id, [MarkdownSource.BodyKey], body.Replace("Как доехали?", "Как добрались?", StringComparison.Ordinal), "текст");
        var changed = before.Split('\n').Zip(file.Text.Split('\n')).Count(pair => pair.First != pair.Second);
        Assert.Equal(2, changed); // the title line and the one body line
        Assert.Contains("\"ru\":\"Сообщение мамы\"", file.Text, StringComparison.Ordinal); // Cyrillic stays readable

        file.Save();
        Assert.Equal(file.Text, File.ReadAllText(file.FullPath));
        var reread = StudioWorkspace.Open(_root);
        Assert.Equal("Сообщение мамы", (string)reread.Get(id)!["title"]!["translations"]!["ru"]!);

        session.Undo();
        session.Undo();
        Assert.Equal(before, file.Text);
    }

    [Fact]
    public void PlainTextValuesStayPlainAndNewKeysGoBeforeTheBody()
    {
        const string original = "---\nschemaVersion: 1\nid: urman.test:document/x\n# comment\nformat: markdown\n---\n# Body\n\ntext\n";
        var entity = MarkdownSource.Parse(original);
        Assert.Equal(1, (int)entity["schemaVersion"]!);
        Assert.Equal(original, MarkdownSource.Write(entity, original));
        entity["format"] = "plain";
        entity["knowledgeRefs"] = new JsonArray("urman.test:knowledge/y");
        Assert.Equal("---\nschemaVersion: 1\nid: urman.test:document/x\n# comment\nformat: plain\nknowledgeRefs: [\"urman.test:knowledge/y\"]\n---\n# Body\n\ntext\n",
            MarkdownSource.Write(entity, original));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Urman.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
