using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Runtime;
using Xunit;

namespace Urman.Core.Tests;

public sealed class RuntimeKernelTests
{
    [Fact]
    public async Task Dispatch_CommitsStateClaimsAndOrderedEventsAtomically()
    {
        using var kernel = CreateKernel(new Dictionary<string, RuntimeCommandHandler>
        {
            ["clue.confirm"] = (_, _) => new(
                Effects:
                [
                    new(StateEffectOperation.Set, "clue", JsonSerializer.SerializeToElement("confirmed")),
                    new(StateEffectOperation.Increment, "pressure", Delta: 2)
                ],
                Events:
                [
                    new("knowledge.changed", JsonSerializer.SerializeToElement(new { id = "clue" })),
                    new("journal.changed", JsonSerializer.SerializeToElement(new { id = "clue" }))
                ],
                Claims: [new("urman:resource/journal", "quest:clue", "quest", ResourceClaimMode.Shared)],
                Value: JsonSerializer.SerializeToElement(new { accepted = true }))
        });

        var result = await kernel.DispatchAsync(Command("occurrence:1", "clue.confirm", new { }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, result.Status);
        Assert.Equal([1L, 2L], result.Events.Select(gameEvent => gameEvent.Sequence));
        Assert.All(result.Events, gameEvent => Assert.Equal("tx:occurrence:1", gameEvent.TransactionId));
        var snapshot = kernel.CaptureSnapshot();
        Assert.Equal("confirmed", snapshot.State.GetProperty("clue").GetString());
        Assert.Equal(2, snapshot.State.GetProperty("pressure").GetDouble());
        Assert.Single(snapshot.Claims);
        Assert.Single(snapshot.Occurrences);
    }

    [Fact]
    public async Task OccurrenceLedger_RejectsDuplicateAndFingerprintConflict()
    {
        using var kernel = CreateKernel(new Dictionary<string, RuntimeCommandHandler>
        {
            ["counter.add"] = (command, _) => new(Effects:
            [
                new(StateEffectOperation.Increment, "counter", Delta: command.Payload.GetProperty("amount").GetDouble())
            ])
        });

        var first = await kernel.DispatchAsync(Command("occurrence:counter", "counter.add", new { amount = 1 }), TestContext.Current.CancellationToken);
        var duplicate = await kernel.DispatchAsync(Command("occurrence:counter", "counter.add", new { amount = 1 }), TestContext.Current.CancellationToken);
        var conflict = await kernel.DispatchAsync(Command("occurrence:counter", "counter.add", new { amount = 2 }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, first.Status);
        Assert.Equal("DuplicateOccurrence", duplicate.Error?.Code);
        Assert.Equal("OccurrenceConflict", conflict.Error?.Code);
        Assert.Equal(1, kernel.SelectState().GetProperty("counter").GetDouble());
        Assert.Single(kernel.CaptureSnapshot().Occurrences);
    }

    [Fact]
    public async Task ClaimConflict_DoesNotCommitStateOrEvents()
    {
        using var kernel = CreateKernel(new Dictionary<string, RuntimeCommandHandler>
        {
            ["resource.claim"] = (command, _) => new(
                Effects: [new(StateEffectOperation.Increment, "attempts", Delta: 1)],
                Events: [new("claim.committed", JsonSerializer.SerializeToElement(new { }))],
                Claims:
                [
                    new(
                        "urman:resource/old-pc",
                        command.Payload.GetProperty("owner").GetString()!,
                        "capability-session",
                        ResourceClaimMode.Exclusive)
                ])
        });

        var first = await kernel.DispatchAsync(Command("claim:1", "resource.claim", new { owner = "oldpc:1" }), TestContext.Current.CancellationToken);
        var second = await kernel.DispatchAsync(Command("claim:2", "resource.claim", new { owner = "oldpc:2" }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, first.Status);
        Assert.Equal("ResourceConflict", second.Error?.Code);
        Assert.Equal(1, kernel.SelectState().GetProperty("attempts").GetDouble());
        Assert.Equal(1, kernel.CaptureSnapshot().EventSequence);
    }

    [Fact]
    public async Task InvalidEvent_RollsBackPreparedStateAndSequence()
    {
        using var kernel = CreateKernel(new Dictionary<string, RuntimeCommandHandler>
        {
            ["invalid.event"] = (_, _) => new(
                Effects: [new(StateEffectOperation.Set, "changed", JsonSerializer.SerializeToElement(true))],
                Events:
                [
                    new("valid", JsonSerializer.SerializeToElement(new { })),
                    new(string.Empty, JsonSerializer.SerializeToElement(new { }))
                ])
        });

        var result = await kernel.DispatchAsync(Command("invalid:1", "invalid.event", new { }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.False(kernel.SelectState().TryGetProperty("changed", out _));
        Assert.Equal(0, kernel.CaptureSnapshot().EventSequence);
    }

    [Fact]
    public async Task Snapshot_RestoresStateClaimsLedgerAndSequence()
    {
        var handlers = new Dictionary<string, RuntimeCommandHandler>
        {
            ["visit"] = (_, _) => new(
                Effects: [new(StateEffectOperation.Set, "location", JsonSerializer.SerializeToElement("house"))],
                Events: [new("world.location.changed", JsonSerializer.SerializeToElement(new { location = "house" }))],
                Claims: [new("urman:resource/location", "player", "run", ResourceClaimMode.Exclusive)])
        };
        RuntimeSnapshot snapshot;
        using (var original = CreateKernel(handlers))
        {
            _ = await original.DispatchAsync(Command("visit:1", "visit", new { }), TestContext.Current.CancellationToken);
            snapshot = original.CaptureSnapshot();
        }

        using var restored = RuntimeKernel.Restore(snapshot, handlers);
        var duplicate = await restored.DispatchAsync(Command("visit:1", "visit", new { }), TestContext.Current.CancellationToken);

        Assert.Equal("DuplicateOccurrence", duplicate.Error?.Code);
        Assert.Equal("house", restored.SelectState().GetProperty("location").GetString());
        Assert.Single(restored.CaptureSnapshot().Claims);
        Assert.Equal(1, restored.CaptureSnapshot().EventSequence);
    }

    private static RuntimeKernel CreateKernel(IReadOnlyDictionary<string, RuntimeCommandHandler> handlers) =>
        new(JsonSerializer.SerializeToElement(new { }), handlers);

    private static GameCommand Command(string occurrenceId, string type, object payload) =>
        new(occurrenceId, type, JsonSerializer.SerializeToElement(payload));
}
