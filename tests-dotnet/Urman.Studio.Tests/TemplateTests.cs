using System.Text.Json.Nodes;
using Urman.Content.Compilation;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Templates;
using Xunit;

namespace Urman.Studio.Tests;

// QUEST13: the "choice with two outcomes" template makes ordinary content the
// real compiler accepts, in its own files, as one undoable command.
public sealed class TemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "urman-studio-tests", Guid.NewGuid().ToString("N"));

    public TemplateTests()
    {
        var repo = RepoRoot();
        foreach (var folder in new[] { "content", "game/content/world" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(repo, folder), "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(_root, folder, Path.GetRelativePath(Path.Combine(repo, folder), file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TwoOutcomeQuestRequest SampleRequest() => new(
        "urman.chapter1", "content/modules/urman-chapter1", "content/campaigns/urman.chapter1/campaign.json",
        "Проверка шаблона", "Сосед", "Resident", [6.0, 0.0, -36.0], 180, "Поможешь с калиткой?", "Помогу.", "Некогда.",
        "Моток проволоки", [8.0, 0.0, -30.0], "urman.catalog:urman_village_exterior_kit/woodpile-stackedlogs",
        "urman.catalog:urman_village_exterior_kit/gate-crookedtimber", [4.0, 0.0, -33.0], 90, [6.0, 0.0, -36.0], [4.0, 2.5, 4.0]);

    [Fact]
    public async Task TemplateCompilesWithTheRealCompilerAndUndoesAsOneStep()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var ids = TwoOutcomeQuestTemplate.Create(session, SampleRequest());

        Assert.Equal($"новый квест «Проверка шаблона»", session.UndoLabel);
        Assert.NotNull(workspace.Get(ids.QuestId));
        Assert.NotNull(workspace.Get(ids.GateEntityId));
        foreach (var file in workspace.Files.Where(file => file.Dirty))
        {
            file.Save();
        }

        var result = await new ContentCompiler().CompileAsync(_root, "urman.chapter1", [], TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => $"{d.Code} {d.SourcePath}{d.JsonPointer}: {d.Message}")));
        Assert.Contains(result.Pack!["registries"]!["quests"]!.AsArray(), quest => (string)quest!["id"]! == ids.QuestId);

        // Reopening sees the same data and nothing to save.
        var reopened = StudioWorkspace.Open(_root);
        Assert.All(reopened.Files, file => Assert.False(file.Dirty));
        Assert.Equal(workspace.Get(ids.QuestId)!.ToJsonString(), reopened.Get(ids.QuestId)!.ToJsonString());
        Assert.Contains("\"entities\": [\n    {", File.ReadAllText(Path.Combine(_root, ids.WorldFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void UndoingTheTemplateRemovesEverythingItAdded()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var manifestBefore = workspace.File("content/modules/urman-chapter1/module.json").Text;
        var ids = TwoOutcomeQuestTemplate.Create(session, SampleRequest());
        session.Undo();
        Assert.Null(workspace.Get(ids.QuestId));
        Assert.Equal(manifestBefore, workspace.File("content/modules/urman-chapter1/module.json").Text);
        foreach (var file in workspace.Files) file.Save();
        Assert.False(File.Exists(Path.Combine(_root, ids.ModuleFile)));
    }

    [Fact]
    public void ConditionsReadAsPhrasesAndFactsLinkDialogueQuestAndMap()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var ids = TwoOutcomeQuestTemplate.Create(session, SampleRequest());
        var phrases = new ConditionPhrases(new EntityCatalog(workspace));
        var quest = workspace.Get(ids.QuestId)!;
        var acceptCondition = quest["stages"]![0]!["objectives"]![0]!["completionConditions"]![0]!.AsObject();
        Assert.Equal("Сосед: согласился помочь = да", phrases.Describe(acceptCondition));
        var gate = workspace.Get(ids.GateEntityId)!;
        Assert.Equal("Сосед: калитка починена = да", phrases.DescribeAll(gate["params"]!["states"]![0]!["when"]!.AsArray()));
        Assert.StartsWith("Требуется новая механика", phrases.Describe(new JsonObject { ["op"] = "teleport.anywhere" }), StringComparison.Ordinal);
        Assert.StartsWith("Требуется новая механика", phrases.Describe(new JsonObject { ["op"] = "time.phase", ["phase"] = "evening" }), StringComparison.Ordinal);
        Assert.DoesNotContain(ConditionPhrases.Conditions.Concat(ConditionPhrases.Effects), kind => ConditionPhrases.NotExecuted.Contains(kind.Op));
        Assert.Empty(ConditionPhrases.FindNotExecuted(workspace));

        var facts = new FactIndex(workspace);
        var accepted = facts.Uses(new StoryFact(ids.CharacterId, "accepted"));
        Assert.Contains(accepted, use => use.Writes && use.EntityId == ids.DialogueId);
        Assert.Contains(accepted, use => !use.Writes && use.EntityId == ids.QuestId);
        var repaired = facts.Uses(new StoryFact(ids.CharacterId, "gate_repaired"));
        Assert.Contains(repaired, use => use.Writes && use.EntityId == ids.QuestId);
        Assert.Contains(repaired, use => !use.Writes && use.EntityId == ids.GateEntityId);
        Assert.Contains(new StoryFact(ids.CharacterId, "noticed"), facts.WrittenBy(ids.TriggerInteractionId));
        Assert.Equal([ids.PickupInteractionId], facts.WritingSources(new StoryFact(ids.CharacterId, "item_taken")));
        Assert.Equal([ids.DialogueId], facts.WritingSources(new StoryFact(ids.CharacterId, "accepted")));
    }

    [Fact]
    public void ANewDraftTravelsAsDataWithItsDependencies()
    {
        var workspace = StudioWorkspace.Open(_root);
        var session = new EditSession(workspace);
        var ids = TwoOutcomeQuestTemplate.Create(session, SampleRequest());
        var context = new Urman.Studio.Core.Handoff.DeveloperContext(workspace, new EntityCatalog(workspace));
        var text = context.Text(ids.QuestId, problem: "калитка не чинится");
        Assert.Contains("новый черновик", text, StringComparison.Ordinal);
        Assert.Contains(ids.QuestId, text, StringComparison.Ordinal);
        Assert.Contains("Проблема: калитка не чинится", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, text, StringComparison.Ordinal);
        var (path, entities, _) = context.WritePackage(ids.QuestId, Path.Combine(_root, "handoff"), "калитка не чинится");
        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        var packed = JsonNode.Parse(new StreamReader(zip.GetEntry("entities.json")!.Open()).ReadToEnd())!.AsArray();
        Assert.Contains(packed, entity => (string)entity!["id"]! == ids.QuestId);
        Assert.Contains(packed, entity => (string)entity!["id"]! == ids.CharacterId);
        Assert.True(entities >= 5);
        Assert.DoesNotContain(_root, new StreamReader(zip.GetEntry("manifest.json")!.Open()).ReadToEnd(), StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Urman.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
}
