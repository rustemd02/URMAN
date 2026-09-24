using System.Text.Json.Nodes;
using Urman.Content.Compilation;
using Urman.Content.Resolvers;
using Urman.Content.Simulation;
using Xunit;

namespace Urman.Content.Tests;

public sealed class ContentCompilerParityTests
{
    [Fact]
    public async Task ChapterOne_MatchesFrozenJavascriptGoldenPack()
    {
        var root = FindWorkspaceRoot();
        var result = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Pack);

        var goldenPath = Path.Combine(root, "tests-dotnet", "fixtures", "content", "urman.chapter1.compiled.v1.json");
        var golden = JsonNode.Parse(await File.ReadAllTextAsync(goldenPath, TestContext.Current.CancellationToken));
        Assert.True(JsonNode.DeepEquals(golden, result.Pack));
        Assert.Equal("dea4bec7beef21050e247082a132a0c12d88b596b3a6ef29ba8b2e9eaf1edf5b", result.Pack["campaignFingerprint"]!.GetValue<string>());
        var house = result.Pack["registries"]!["scenes"]!.AsArray()
            .Single(scene => scene!["id"]!.GetValue<string>() == "urman.chapter1:scene/house");
        Assert.Contains(
            house!["interactions"]!.AsArray(),
            interaction => interaction!["id"]!.GetValue<string>() == "urman.chapter1:interaction/oldpc-power");
    }

    [Fact]
    public async Task ChapterOne_ChatThreadsHandOutTermsAndNeverDocuments()
    {
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(compilation.Diagnostics);
        var chats = compilation.Pack!["registries"]!["chats"]!.AsArray();
        Assert.Equal(
            ["urman.oldpc:chat/alsu", "urman.oldpc:chat/mansur", "urman.oldpc:chat/rinat", "urman.oldpc:chat/self"],
            chats.Select(chat => chat!["id"]!.GetValue<string>()).ToArray());
        // The rinat thread waits for the same alerted state that opens the saved
        // message; the others are readable from the first visit.
        Assert.Single(chats[2]!["requires"]!.AsArray());
        // A chat hands out search terms, never a document id: the schema forbids
        // a colon in reveals, and this proves the shipped pack obeys it.
        foreach (var chat in chats)
        foreach (var node in chat!["nodes"]!.AsArray())
        {
            foreach (var term in node!["reveals"]!.AsArray())
                Assert.DoesNotContain(':', term!.GetValue<string>());
            foreach (var choice in node!["choices"]!.AsArray())
            foreach (var term in choice!["reveals"]!.AsArray())
                Assert.DoesNotContain(':', term!.GetValue<string>());
        }
    }

    [Fact]
    public async Task ChapterOne_HintsLeadToTermsAndStayWithinTheAuthoredLength()
    {
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(compilation.Diagnostics);
        var hints = compilation.Pack!["registries"]!["hints"]!.AsArray();
        Assert.Equal([1L, 2L, 3L], hints.Select(hint => hint!["level"]!.GetValue<long>()).Order().ToArray());
        var texts = new TextResolver(compilation.Pack!, validate: false);
        foreach (var hint in hints)
        {
            // A hint leads to a term or a place, never to a document id: the
            // schema forbids a colon, and this proves the shipped pack obeys it.
            Assert.DoesNotContain(':', hint!["pointsTo"]!.GetValue<string>());
            var text = texts.Resolve(hint["textId"]!.GetValue<string>(), "ru").Text;
            Assert.InRange(text.Length, 1, 300);
        }
    }

    [Fact]
    public async Task ChapterOne_SourceExcerptFieldsStayWholeParagraphs()
    {
        // RuntimeBridge.SourceExcerpts accepts a selection only when it equals a
        // whole paragraph starting with the binding's heading (Naila's register
        // step and Alsu's voice note depend on it). Deepening a document must
        // keep each heading at the start of exactly one paragraph; merging the
        // fields into prose once blocked the investigation.
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(compilation.Diagnostics);
        var documents = compilation.Pack!["registries"]!["documents"]!.AsArray();
        foreach (var (id, heading) in new[]
                 {
                     ("urman.oldpc:document/doc_marat_official_death_notice", "Причина закрытия дела:"),
                     ("urman.oldpc:document/rec_marat_case_register_conflict", "Внешняя формулировка:"),
                     ("urman.oldpc:document/rec_marat_case_register_conflict", "Категория:"),
                     ("urman.oldpc:document/msg_marat_saved_last_normal", "Сегодня опять слышал")
                 })
        {
            var body = documents.Single(document => document!["id"]!.GetValue<string>() == id)!["bodyMarkdown"]!.GetValue<string>();
            var paragraphs = System.Text.RegularExpressions.Regex.Split(body, @"\r?\n\s*\r?\n")
                .Select(paragraph => paragraph.Trim());
            Assert.True(paragraphs.Count(paragraph => paragraph.StartsWith(heading, StringComparison.Ordinal)) == 1,
                $"{id} must start exactly one paragraph with '{heading}'");
        }
    }

    [Fact]
    public async Task ChapterOne_TierADocumentsCarryTheirAuthoredDepth()
    {
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(compilation.Diagnostics);
        var documents = compilation.Pack!["registries"]!["documents"]!.AsArray();
        var tatarTerms = compilation.Pack!["registries"]!["vocabulary"]!.AsArray()
            .Where(entry => entry!["language"]!.GetValue<string>() == "tt")
            .Select(entry => entry!["term"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var tierA = documents
            .Where(document => document!["oldPc"]?["tier"]?.GetValue<string>() == "A")
            .ToArray();
        // The twelve investigation records of §6.2 tier A.
        var expected = new[]
        {
            "urman.oldpc:document/doc_marat_official_death_notice",
            "urman.oldpc:document/rec_marat_case_register_conflict",
            "urman.oldpc:document/rec_internal_accounting_line_1987",
            "urman.oldpc:document/rec_internal_accounting_damaged",
            "urman.oldpc:document/doc_kara_urman_edge_sketch",
            "urman.oldpc:document/doc_letter_from_kazan",
            "urman.oldpc:document/doc_school_notebook_marat_page",
            "urman.oldpc:document/rec_violations_compensation_summary",
            "urman.oldpc:document/rec_internal_accounting_blank_1970s",
            "urman.oldpc:document/tw_zirat_customs",
            "urman.oldpc:document/doc_marat_medical_card",
            "urman.oldpc:document/rec_fanis_explanatory_niva"
        };
        Assert.Equal(expected.Order(StringComparer.Ordinal),
            tierA.Select(document => document!["id"]!.GetValue<string>()).Order(StringComparer.Ordinal));

        foreach (var document in tierA)
        {
            var id = document!["id"]!.GetValue<string>();
            var body = document["bodyMarkdown"]!.GetValue<string>();
            Assert.True(body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 350,
                id + " keeps the authored tier-A depth of at least 350 words");
            var terms = document["oldPc"]!["searchTerms"]!.AsArray()
                .Select(term => term!.GetValue<string>()).ToArray();
            Assert.True(terms.Length >= 5, id + " carries at least five search terms");
            Assert.Contains(terms, term => tatarTerms.Contains(term));
            Assert.True(document["oldPc"]!["suggestedTerms"]!.AsArray().Count >= 2,
                id + " suggests at least two terms to the player");
            var bodyText = document["bodyMarkdown"]!.GetValue<string>();
            var links = bodyText.Split("doc:urman.oldpc:document/", StringSplitOptions.None).Length - 1;
            Assert.True(links >= 2, id + " links at least two related records");
        }
    }

    [Fact]
    public async Task ChapterOne_HouseholdAndNetworkTiersStayContrastNotKeys()
    {
        // §6.2/§6.5: ten household records of at least 150 words and ten or more
        // short linked network pages. Neither tier may open behind an
        // investigation gate or hand out investigation knowledge when read.
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            ["urman.chapter1", "urman.core", "urman.oldpc"],
            TestContext.Current.CancellationToken);

        Assert.Empty(compilation.Diagnostics);
        var documents = compilation.Pack!["registries"]!["documents"]!.AsArray();
        JsonNode?[] Tier(string tier) => documents
            .Where(document => document!["oldPc"]?["tier"]?.GetValue<string>() == tier)
            .ToArray();
        var tierB = Tier("B");
        Assert.Equal(10, tierB.Length);
        foreach (var document in tierB)
        {
            var id = document!["id"]!.GetValue<string>();
            var body = document["bodyMarkdown"]!.GetValue<string>();
            Assert.True(body.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 150,
                id + " keeps the household depth of at least 150 words");
            Assert.Empty(document["accessConditions"]!.AsArray());
            Assert.DoesNotContain(document["openEffects"]!.AsArray(),
                effect => effect!["op"]!.GetValue<string>() == "knowledge.set-status");
        }

        var tierC = Tier("C");
        Assert.True(tierC.Length >= 10, "at least ten network pages");
        foreach (var document in tierC)
        {
            var id = document!["id"]!.GetValue<string>();
            Assert.True(document["bodyMarkdown"]!.GetValue<string>().Contains("](doc:urman.oldpc:document/", StringComparison.Ordinal),
                id + " links at least one related record");
            Assert.DoesNotContain(document["openEffects"]!.AsArray(),
                effect => effect!["op"]!.GetValue<string>() == "knowledge.set-status"
                    && effect["knowledgeId"]!.GetValue<string>() != "urman.chapter1:knowledge/clue_mansur_unsent_note_read");
        }
    }

    [Fact]
    public async Task ChapterOne_SimulationIsDeterministic()
    {
        var root = FindWorkspaceRoot();
        var compilation = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(compilation.Pack);
        var simulator = new CampaignSimulator();

        var first = await simulator.SimulateNarrativeOrderAsync(compilation.Pack, TestContext.Current.CancellationToken);
        var second = await simulator.SimulateNarrativeOrderAsync(compilation.Pack, TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        // 17 after the authored finale beat replaced the earlier rinat beat in
        // 04f039e; the frozen pack below carries that content.
        Assert.Equal(17, first.Steps);
        Assert.Equal(17, first.EventSequence);
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "content")) && File.Exists(Path.Combine(current.FullName, "Urman.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("URMAN workspace root was not found.");
    }
}
