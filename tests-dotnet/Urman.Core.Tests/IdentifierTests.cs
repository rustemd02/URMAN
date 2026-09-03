using Urman.Core.Contracts;
using Xunit;

namespace Urman.Core.Tests;

public sealed class IdentifierTests
{
    [Fact]
    public void ContentId_AcceptsCanonicalNamespacedId()
    {
        var id = new ContentId("urman.chapter1:scene/arrival_vehicle_dusk");
        Assert.Equal("urman.chapter1:scene/arrival_vehicle_dusk", id.Value);
    }

    [Theory]
    [InlineData("chapter1")]
    [InlineData("scene/arrival")]
    [InlineData("urman.chapter1:scene/")]
    public void ContentId_RejectsLegacyOrIncompleteId(string value)
    {
        Assert.Throws<ArgumentException>(() => new ContentId(value));
    }

    [Fact]
    public void LogicalWorldIds_DoNotAcceptEnginePaths()
    {
        Assert.Throws<ArgumentException>(() => new WorldLocationId("res://scenes/house.tscn"));
    }
}
