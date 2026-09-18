using System.Text.Json.Nodes;
using Urman.Content.Compilation;
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
        Assert.Equal("148a6fd5ce06241a180cd05a0e84d9cf94221ceed7e9ee8dae01effd9603965f", result.Pack["campaignFingerprint"]!.GetValue<string>());
        var house = result.Pack["registries"]!["scenes"]!.AsArray()
            .Single(scene => scene!["id"]!.GetValue<string>() == "urman.chapter1:scene/house");
        Assert.Contains(
            house!["interactions"]!.AsArray(),
            interaction => interaction!["id"]!.GetValue<string>() == "urman.chapter1:interaction/oldpc-power");
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
