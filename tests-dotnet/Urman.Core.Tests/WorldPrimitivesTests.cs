using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Determinism;
using Urman.Core.Runtime;
using Urman.Core.World;
using Xunit;

namespace Urman.Core.Tests;

public sealed class WorldPrimitivesTests
{
    [Fact]
    public void SchedulerReoffersUnacknowledgedJobAfterRestoreAndFiresOnceAfterAcknowledgement()
    {
        var scheduler = new DeterministicScheduler();
        scheduler.Schedule(new(
            "job/reveal",
            "capability/reveal",
            4,
            JsonSerializer.SerializeToElement(new { kind = "reveal" })));
        var extracted = Assert.Single(scheduler.ClaimDue(4));
        Assert.Equal("scheduled:job/reveal:4", extracted.OccurrenceId);

        scheduler.Release(extracted.JobId, extracted.OccurrenceId);
        var liveRetry = Assert.Single(scheduler.ClaimDue(4));
        Assert.Equal(extracted.OccurrenceId, liveRetry.OccurrenceId);

        var restoredBeforeAck = DeterministicScheduler.Restore(scheduler.CaptureSnapshot());
        var reoffered = Assert.Single(restoredBeforeAck.ClaimDue(4));
        restoredBeforeAck.Acknowledge(reoffered.JobId, reoffered.OccurrenceId);

        var afterFire = DeterministicScheduler.Restore(restoredBeforeAck.CaptureSnapshot());
        Assert.Empty(afterFire.ClaimDue(100));
        Assert.Throws<ArgumentException>(() => afterFire.Schedule(new(
            "job/reveal",
            "capability/reveal",
            5,
            JsonSerializer.SerializeToElement<object?>(null))));
    }

    [Fact]
    public async Task CustodyBatchClaimsItemAndCompetingConsumeHasNoPartialEffects()
    {
        var initialState = JsonSerializer.SerializeToElement(new
        {
            custody = new[]
            {
                new { itemId = "item/key", custodyOwnerId = "npc/keeper", condition = new { durability = 1 } }
            }
        });
        RuntimeCommandHandler consume = (command, context) => CustodyStore.PlanBatch(
            context.State,
            JsonSerializer.SerializeToElement(new[]
            {
                new
                {
                    op = "consume",
                    itemId = command.Payload.GetProperty("itemId").GetString(),
                    ownerId = "npc/keeper"
                }
            }),
            command.Payload.GetProperty("claimOwnerId").GetString()!,
            "action",
            "custody");
        using var kernel = new RuntimeKernel(initialState, new Dictionary<string, RuntimeCommandHandler> { ["consume"] = consume });

        var first = await kernel.DispatchAsync(Command("consume/one", "consume", new { itemId = "item/key", claimOwnerId = "action/one" }), TestContext.Current.CancellationToken);
        var second = await kernel.DispatchAsync(Command("consume/two", "consume", new { itemId = "item/key", claimOwnerId = "action/two" }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, first.Status);
        Assert.Equal(CommandStatus.Rejected, second.Status);
        Assert.Equal("ResourceConflict", second.Error?.Code);
        Assert.Empty(kernel.SelectState().GetProperty("custody").EnumerateArray());
        var claim = Assert.Single(CustodyStore.ItemResourceClaims(
            JsonSerializer.SerializeToElement(new[] { new { op = "consume", itemId = "item/key", ownerId = "npc/keeper" } }),
            "capability/item-use",
            "capability"));
        Assert.Equal("item/item/key", claim.ResourceId);
        Assert.Equal(ResourceClaimMode.Exclusive, claim.Mode);
    }

    [Fact]
    public void EvidenceClaimsRetainDeterministicProvenanceChain()
    {
        var first = EvidenceProvenance.PlanClaim(
            JsonSerializer.SerializeToElement(new { }),
            "evidence/letter",
            "actor/a",
            "document/letter",
            JsonSerializer.SerializeToElement(new { page = 1 }),
            "evidence");
        var state = JsonSerializer.SerializeToElement(new
        {
            evidence = first.Effects!.Single().Value
        });
        var derived = EvidenceProvenance.PlanClaim(
            state,
            "evidence/inference",
            "actor/a",
            "evidence/letter",
            stateKey: "evidence");

        Assert.Equal(
            ["document/letter", "evidence/letter"],
            derived.Value.GetProperty("provenanceChain").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Throws<ArgumentException>(() => EvidenceProvenance.PlanClaim(
            state,
            "evidence/letter",
            "actor/a",
            "document/letter",
            stateKey: "evidence"));
    }

    private static GameCommand Command(string occurrenceId, string type, object payload) =>
        new(occurrenceId, type, JsonSerializer.SerializeToElement(payload));
}
