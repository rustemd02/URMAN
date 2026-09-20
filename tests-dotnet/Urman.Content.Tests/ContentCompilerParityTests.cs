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
        Assert.Equal("9349c1d653e183748abb02e5b6c31a5b5ddf92da13ef16280d96cd11e8172d79", result.Pack["campaignFingerprint"]!.GetValue<string>());
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
