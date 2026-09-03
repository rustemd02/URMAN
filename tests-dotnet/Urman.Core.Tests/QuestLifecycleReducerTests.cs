using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Quests;
using Urman.Core.Runtime;
using Xunit;

namespace Urman.Core.Tests;

public sealed class QuestLifecycleReducerTests
{
    private static readonly JsonElement Definition = JsonSerializer.SerializeToElement(new
    {
        id = "sample:quest/modular-route",
        stages = new object[]
        {
            new
            {
                id = "arrival",
                objectives = new object[]
                {
                    new
                    {
                        id = "inspect", optional = false, startConditions = Array.Empty<object>(), completionConditions = Array.Empty<object>(),
                        completionEffects = new[] { new { op = "knowledge.set" } }, failureEffects = Array.Empty<object>(),
                        capability = new
                        {
                            protocolId = "sample:capability/inspect", exactVersion = "1.0.0",
                            configRef = "sample:config/inspect", outcomeSchemaRef = "sample:outcome/inspect"
                        }
                    },
                    new
                    {
                        id = "optional-note", optional = true, startConditions = Array.Empty<object>(), completionConditions = Array.Empty<object>(),
                        completionEffects = Array.Empty<object>(), failureEffects = Array.Empty<object>()
                    }
                },
                composition = new { mode = "all", objectiveIds = new[] { "inspect", "optional-note" } }
            },
            new
            {
                id = "archive",
                objectives = new object[]
                {
                    new
                    {
                        id = "read", optional = false, startConditions = Array.Empty<object>(), completionConditions = Array.Empty<object>(),
                        completionEffects = Array.Empty<object>(), failureEffects = Array.Empty<object>()
                    },
                    new
                    {
                        id = "ask", optional = false, startConditions = Array.Empty<object>(), completionConditions = Array.Empty<object>(),
                        completionEffects = Array.Empty<object>(), failureEffects = Array.Empty<object>()
                    }
                },
                composition = new { mode = "threshold", objectiveIds = new[] { "read", "ask" }, threshold = 1 }
            }
        },
        outcomes = new
        {
            success = new[] { new { op = "quest.set" } },
            optional = new[] { new { op = "quest.optional" } },
            failure = new[] { new { op = "quest.failed" } }
        },
        retryPolicy = new { mode = "stage", maximumAttempts = 2 },
        checkpointPolicy = new { mode = "stage" },
        cancelPolicy = new { allowed = true, effects = new[] { new { op = "quest.cancelled" } } }
    });

    [Fact]
    public async Task ParallelObjectivesThresholdAndCapabilityRequestsMatchLegacyLifecycle()
    {
        var harness = CreateHarness(Definition);
        var request = Assert.Single(harness.Initial.CapabilityRequests);
        Assert.Equal("capability:instance/modular-route:arrival:inspect", request.CapabilityInstanceId);
        Assert.Equal("sample:config/inspect", request.Config.GetProperty("configRef").GetString());

        var inspected = await harness.Dispatch("quest/inspect", new { type = "objective.complete", objectiveId = "inspect" });
        Assert.Equal(CommandStatus.Committed, inspected.Status);
        Assert.Equal(1, harness.Instance.GetProperty("stageIndex").GetInt32());
        Assert.Equal(QuestStatuses.Active, harness.Instance.GetProperty("status").GetString());

        var completed = await harness.Dispatch("quest/read", new { type = "objective.complete", objectiveId = "read" });
        Assert.Equal(CommandStatus.Committed, completed.Status);
        Assert.Equal(QuestStatuses.Completed, harness.Instance.GetProperty("status").GetString());
        Assert.Equal(QuestStatuses.Completed, harness.Kernel.SelectState().GetProperty("questState").GetProperty("status").GetString());
    }

    [Fact]
    public async Task FailedQuestRetriesFromStageCheckpointAndRejectsUnknownObjectiveAtomically()
    {
        var harness = CreateHarness(Definition);
        await harness.Dispatch("quest/advance-before-fail", new { type = "objective.complete", objectiveId = "inspect" });
        await harness.Dispatch("quest/fail", new { type = "objective.fail", objectiveId = "read" });
        Assert.Equal(QuestStatuses.Failed, harness.Instance.GetProperty("status").GetString());

        var retried = await harness.Dispatch("quest/retry", new { type = "quest.retry" });
        Assert.Equal(CommandStatus.Committed, retried.Status);
        Assert.Equal(2, harness.Instance.GetProperty("attempt").GetInt32());
        Assert.Equal(1, harness.Instance.GetProperty("stageIndex").GetInt32());
        Assert.Equal(ObjectiveStatuses.Active, harness.Instance.GetProperty("objectives").GetProperty("archive").GetProperty("read").GetProperty("status").GetString());

        var before = harness.Kernel.SelectState();
        var invalid = await harness.Dispatch("quest/missing", new { type = "objective.complete", objectiveId = "missing" });
        Assert.Equal(CommandStatus.Rejected, invalid.Status);
        Assert.Equal("UnknownQuestObjective", invalid.Error?.Code);
        Assert.True(JsonElement.DeepEquals(before, harness.Kernel.SelectState()));
    }

    [Fact]
    public async Task QuestRetryPolicyRestartsFromOriginCheckpoint()
    {
        var definition = JsonNodeClone(Definition);
        definition.GetProperty("retryPolicy").GetProperty("mode");
        var node = System.Text.Json.Nodes.JsonNode.Parse(definition.GetRawText())!.AsObject();
        node["retryPolicy"]!["mode"] = "quest";
        var harness = CreateHarness(JsonSerializer.SerializeToElement(node));

        await harness.Dispatch("quest-policy/advance", new { type = "objective.complete", objectiveId = "inspect" });
        await harness.Dispatch("quest-policy/fail", new { type = "objective.fail", objectiveId = "read" });
        await harness.Dispatch("quest-policy/retry", new { type = "quest.retry" });

        Assert.Equal(0, harness.Instance.GetProperty("stageIndex").GetInt32());
        Assert.Equal(ObjectiveStatuses.Active, harness.Instance.GetProperty("objectives").GetProperty("arrival").GetProperty("inspect").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CancelAndRetryReleaseCapabilityOwnersAndRetryRequestsFreshSession()
    {
        var cleanupCalls = new List<(IReadOnlyList<string> Ids, string Reason)>();
        QuestCapabilityCleanupExtension Extension(IReadOnlyList<string> ids, string reason)
        {
            cleanupCalls.Add((ids, reason));
            return new(ReleaseClaimLifecycleScopes: ["capability-session"]);
        }

        var harness = CreateHarness(Definition, Extension);
        var capabilityId = "capability:instance/modular-route:arrival:inspect";
        await harness.Dispatch("quest-capability/fail", new { type = "objective.fail", objectiveId = "inspect" });
        Assert.Equal((capabilityId, "objective-fail"), (Assert.Single(cleanupCalls).Ids.Single(), cleanupCalls[0].Reason));
        Assert.Equal([capabilityId], harness.LastReduction.Plan.ReleaseClaimOwnerIds);
        Assert.Empty(harness.Instance.GetProperty("activeCapabilities").EnumerateObject());

        await harness.Dispatch("quest-capability/retry", new { type = "quest.retry" });
        Assert.Equal(capabilityId, Assert.Single(harness.LastReduction.CapabilityRequests).CapabilityInstanceId);
        Assert.True(harness.Instance.GetProperty("activeCapabilities").TryGetProperty(capabilityId, out _));

        var cancelled = CreateHarness(Definition, Extension);
        await cancelled.Dispatch("quest-capability/cancel", new { type = "quest.cancel" });
        Assert.Equal([capabilityId], cancelled.LastReduction.Plan.ReleaseClaimOwnerIds);
        Assert.Empty(cancelled.Instance.GetProperty("activeCapabilities").EnumerateObject());
    }

    [Fact]
    public void ObjectiveOccurrenceIdIsIndependentOfCampaignOrdering()
    {
        Assert.Equal(
            "quest-trigger:instance/modular-route:arrival:inspect:completed",
            QuestLifecycleReducer.ObjectiveOccurrenceId("instance/modular-route", "arrival", "inspect", "completed"));
    }

    private static Harness CreateHarness(
        JsonElement definition,
        Func<IReadOnlyList<string>, string, QuestCapabilityCleanupExtension?>? cleanup = null)
    {
        var initial = QuestLifecycleReducer.CreateRun(
            definition,
            "instance/modular-route",
            configResolver: configRef => JsonSerializer.SerializeToElement(new { configRef }));
        var instance = initial.Instance;
        QuestReduction? last = null;
        RuntimeCommandHandler handler = (command, context) =>
        {
            var reduction = QuestLifecycleReducer.Reduce(
                definition,
                instance,
                command.Payload,
                "questState",
                planEffects: (effects, _) => new(
                    Events: effects.EnumerateArray()
                        .Select(effect => new EventDraft("authored.effect", JsonSerializer.SerializeToElement(new { op = effect.GetProperty("op").GetString() })))
                        .ToArray()),
                configResolver: configRef => JsonSerializer.SerializeToElement(new { configRef }),
                evaluationContext: context.State,
                prepareCapabilityCleanup: cleanup);
            last = reduction;
            if (reduction.Plan.Rejection is null)
            {
                instance = reduction.NextState;
            }

            return reduction.Plan;
        };
        var kernel = new RuntimeKernel(JsonSerializer.SerializeToElement(new { }), new Dictionary<string, RuntimeCommandHandler>
        {
            ["quest"] = handler
        });
        return new(kernel, initial, () => instance, () => last!);
    }

    private static JsonElement JsonNodeClone(JsonElement value) => JsonDocument.Parse(value.GetRawText()).RootElement.Clone();

    private sealed class Harness(
        RuntimeKernel kernel,
        QuestRun initial,
        Func<JsonElement> instance,
        Func<QuestReduction> lastReduction)
    {
        public RuntimeKernel Kernel { get; } = kernel;
        public QuestRun Initial { get; } = initial;
        public JsonElement Instance => instance();
        public QuestReduction LastReduction => lastReduction();

        public ValueTask<CommandDispatchResult> Dispatch(string occurrenceId, object payload) => Kernel.DispatchAsync(
            new(occurrenceId, "quest", JsonSerializer.SerializeToElement(payload)),
            TestContext.Current.CancellationToken);
    }
}
