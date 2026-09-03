using System.Text.Json.Nodes;
using Urman.Content.Compilation;
using Xunit;

namespace Urman.Content.Tests;

public sealed class GameContentArtifactTests
{
    [Fact]
    public async Task GodotCompiledCampaignMatchesCurrentAuthoringContent()
    {
        var root = FindWorkspaceRoot();
        var result = await new ContentCompiler().CompileAsync(
            root,
            "urman.chapter1",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var artifactPath = Path.Combine(root, "game", "content", "urman.chapter1.compiled.v1.json");
        Assert.True(File.Exists(artifactPath), $"Godot compiled campaign is missing: {artifactPath}");
        var artifact = JsonNode.Parse(await File.ReadAllTextAsync(artifactPath, TestContext.Current.CancellationToken));
        Assert.True(JsonNode.DeepEquals(result.Pack, artifact), "Godot compiled campaign is stale; run the C# ContentCli compile command.");
    }

    private static string FindWorkspaceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "content", "campaigns")) &&
                File.Exists(Path.Combine(directory.FullName, "Urman.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the URMAN workspace root from the test runner directory.");
    }
}
