using Urman.Content.Reporting;
using Xunit;

namespace Urman.Content.Tests;

public sealed class AudioProductionReporterTests
{
    [Fact]
    public async Task CurrentWorkspaceSeparatesLogicalVoiceRefsFromTechnicalAmbientFiles()
    {
        var report = await new AudioProductionReporter().InspectAsync(FindWorkspaceRoot(), TestContext.Current.CancellationToken);

        Assert.Empty(report.Diagnostics);
        Assert.Equal(4, report.CampaignsInspected);
        Assert.Equal(8, report.Assets.Count);
        Assert.Equal(8, report.LogicalReferenceCount);
        Assert.Equal(0, report.AuthoredFileCount);
        Assert.Equal(0, report.MissingFileCount);
        Assert.All(report.Assets, asset =>
        {
            Assert.True(asset.CaptionClosed);
            Assert.True(asset.TranscriptClosed);
            Assert.Equal("logical-ref", asset.Status);
        });
        Assert.Equal(4, report.AmbientStems.Count);
        Assert.All(report.AmbientStems, stem => Assert.True(stem.PhysicalFileExists));
        Assert.True(report.HasOpenAuthoring);
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "content"))
                && File.Exists(Path.Combine(current.FullName, "Urman.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("URMAN workspace root was not found.");
    }
}
