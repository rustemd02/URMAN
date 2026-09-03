using System.Text.Json;
using Urman.Core.Capabilities;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Contracts;
using Xunit;

namespace Urman.Core.Tests;

public sealed class CapabilityHostTests
{
    private const string DocumentId = "urman.oldpc:document/msg_marat_saved_last_normal";

    [Fact]
    public void OldPc_LifecycleAndSnapshotRoundTrip()
    {
        var registry = Registry();
        CapabilitySessionSnapshot snapshot;
        using (var host = new CapabilityHost(registry))
        {
            host.Create("oldpc:house", OldPcCapabilityProvider.Protocol, "1.0.0", Config());
            host.Start("oldpc:house");
            var opened = host.Handle("oldpc:house", Input(new { type = "open", documentId = DocumentId }));
            _ = host.Handle("oldpc:house", Input(new { type = "save", documentId = DocumentId }));
            Assert.Equal(DocumentId, opened.GetProperty("activeDocumentId").GetString());
            snapshot = host.Capture("oldpc:house");
        }

        using var restored = new CapabilityHost(registry);
        restored.Create("oldpc:house", OldPcCapabilityProvider.Protocol, "1.0.0", Config(), snapshot);
        restored.Start("oldpc:house");
        var state = restored.Capture("oldpc:house").State;

        Assert.Equal(DocumentId, state.GetProperty("activeDocumentId").GetString());
        Assert.Equal(DocumentId, state.GetProperty("savedDocumentIds")[0].GetString());
        Assert.Equal(3, state.GetProperty("nextActionSequence").GetInt64());
    }

    [Fact]
    public void Registry_RequiresExactCapabilityVersion()
    {
        var registry = Registry();

        Assert.Throws<InvalidOperationException>(() => registry.Require(OldPcCapabilityProvider.Protocol, "2.0.0"));
    }

    [Fact]
    public void Host_RejectsSnapshotFromDifferentInstance()
    {
        var registry = Registry();
        using var host = new CapabilityHost(registry);
        var snapshot = new CapabilitySessionSnapshot(
            "oldpc:other",
            OldPcCapabilityProvider.Protocol,
            "1.0.0",
            1,
            JsonSerializer.SerializeToElement(new
            {
                activeDocumentId = (string?)null,
                activeSection = "archive_search",
                nextActionSequence = 1,
                query = string.Empty,
                savedDocumentIds = Array.Empty<string>()
            }));

        Assert.Throws<InvalidDataException>(() =>
            host.Create("oldpc:house", OldPcCapabilityProvider.Protocol, "1.0.0", Config(), snapshot));
    }

    private static CapabilityRegistry Registry() => new(
        [new OldPcCapabilityProvider([new OldPcDocumentDescriptor(DocumentId, "saved_messages")])]);

    private static JsonElement Config() => JsonSerializer.SerializeToElement(new { moduleId = OldPcCapabilityProvider.ModuleId });

    private static JsonElement Input(object value) => JsonSerializer.SerializeToElement(value);
}
