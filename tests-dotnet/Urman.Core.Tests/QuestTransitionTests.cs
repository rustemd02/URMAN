using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Contracts;
using Urman.Core.Quests;
using Urman.Core.Runtime;
using Xunit;

namespace Urman.Core.Tests;

// Authored stage transitions: branches, joins, named outcomes and loops on top
// of the sequential stage list (URMAN Studio spec QUEST06–QUEST11).
public sealed class QuestTransitionTests
{
    private static object Objective(string id, bool optional = false) => new
    {
        id, optional, startConditions = Array.Empty<object>(), completionConditions = Array.Empty<object>(),
        completionEffects = new[] { new { op = $"done.{id}" } }, failureEffects = Array.Empty<object>()
    };

    private static object On(string kind, string? objectiveId = null) =>
        objectiveId is null ? new { kind } : new { kind, objectiveId };

    private static readonly JsonElement Branching = JsonSerializer.SerializeToElement(new
    {
        id = "sample:quest/fence-choice",
        stages = new object[]
        {
            new
            {
                id = "talk",
                objectives = new[] { Objective("accept"), Objective("refuse"), Objective("ask-guy") },
                composition = new { mode = "any", objectiveIds = new[] { "accept", "refuse", "ask-guy" } },
                transitions = new object[]
                {
                    new { id = "to-boards", on = On("objective-complete", "accept"), conditions = Array.Empty<object>(), effects = new[] { new { op = "fact.accepted" } }, target = new { stageId = "boards" } },
                    new { id = "refused", on = On("objective-complete", "refuse"), conditions = Array.Empty<object>(), effects = new[] { new { op = "world.gate-broken" } }, target = new { end = "failed", outcomeId = "refused" } },
                    new { id = "via-guy", on = On("objective-complete", "ask-guy"), conditions = Array.Empty<object>(), effects = Array.Empty<object>(), target = new { stageId = "return" } }
                }
            },
            new
            {
                id = "boards",
                objectives = new[] { Objective("board-a"), Objective("board-b"), Objective("board-c") },
                composition = new { mode = "threshold", objectiveIds = new[] { "board-a", "board-b", "board-c" }, threshold = 2 },
                transitions = new object[]
                {
                    new { id = "lost-board", on = On("objective-fail", "board-a"), conditions = Array.Empty<object>(), effects = Array.Empty<object>(), target = new { stageId = "boards" } },
                    new { id = "collected", on = On("stage-complete"), conditions = Array.Empty<object>(), effects = Array.Empty<object>(), target = new { stageId = "return" } }
                }
            },
            new
            {
                id = "return",
                objectives = new[] { Objective("hand-in") },
                composition = new { mode = "all", objectiveIds = new[] { "hand-in" } }
            }
        },
        outcomes = new
        {
            success = new[] { new { op = "quest.success" } },
            optional = Array.Empty<object>(),
            failure = new[] { new { op = "quest.failure" } }
        },
        retryPolicy = new { mode = "none", maximumAttempts = 1 },
        checkpointPolicy = new { mode = "stage" },
        cancelPolicy = new { allowed = false, effects = Array.Empty<object>() }
    });

    [Fact]
    public async Task AcceptBranchRunsThresholdGroupAndJoinsAtReturn()
    {
        var harness = Create(Branching);
        await harness.Dispatch("q/accept", "objective.complete", "accept");
        Assert.Equal(1, harness.StageIndex);
        Assert.Contains("fact.accepted", harness.Ops);

        await harness.Dispatch("q/a", "objective.complete", "board-a");
        Assert.Equal(1, harness.StageIndex);
        await harness.Dispatch("q/c", "objective.complete", "board-c");
        Assert.Equal(2, harness.StageIndex);

        await harness.Dispatch("q/hand", "objective.complete", "hand-in");
        Assert.Equal(QuestStatuses.Completed, harness.Status);
        Assert.Contains("quest.success", harness.Ops);
        Assert.False(harness.Instance.TryGetProperty("outcomeId", out _));
    }

    [Fact]
    public async Task RefuseBranchEndsWithNamedFailureAndItsOwnConsequence()
    {
        var harness = Create(Branching);
        var refused = await harness.Dispatch("q/refuse", "objective.complete", "refuse");
        Assert.Equal(CommandStatus.Committed, refused.Status);
        Assert.Equal(QuestStatuses.Failed, harness.Status);
        Assert.Equal("refused", harness.Instance.GetProperty("outcomeId").GetString());
        Assert.Contains("world.gate-broken", harness.Ops);
        Assert.Contains("quest.failure", harness.Ops);
        Assert.DoesNotContain("fact.accepted", harness.Ops);
    }

    [Fact]
    public async Task SecondBranchJoinsTheSameStageWithoutTheGroup()
    {
        var harness = Create(Branching);
        await harness.Dispatch("q/guy", "objective.complete", "ask-guy");
        Assert.Equal(2, harness.StageIndex);
        await harness.Dispatch("q/hand", "objective.complete", "hand-in");
        Assert.Equal(QuestStatuses.Completed, harness.Status);
    }

    [Fact]
    public async Task FailureTransitionReentersStageWithFreshObjectivesInsteadOfFailingQuest()
    {
        var harness = Create(Branching);
        await harness.Dispatch("q/accept", "objective.complete", "accept");
        await harness.Dispatch("q/b", "objective.complete", "board-b");
        await harness.Dispatch("q/fail-a", "objective.fail", "board-a");
        Assert.Equal(QuestStatuses.Active, harness.Status);
        Assert.Equal(1, harness.StageIndex);
        var boards = harness.Instance.GetProperty("objectives").GetProperty("boards");
        Assert.Equal(ObjectiveStatuses.Active, boards.GetProperty("board-b").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReplayedOccurrenceDoesNotApplyTheBranchTwice()
    {
        var harness = Create(Branching);
        await harness.Dispatch("q/refuse", "objective.complete", "refuse");
        var count = harness.Ops.Count(op => op == "world.gate-broken");
        var replay = await harness.Dispatch("q/refuse", "objective.complete", "refuse");
        Assert.NotEqual(CommandStatus.Committed, replay.Status);
        Assert.Equal(count, harness.Ops.Count(op => op == "world.gate-broken"));
    }

    [Fact]
    public void QuestsWithoutTransitionsKeepSequentialOrder()
    {
        var node = JsonNode.Parse(Branching.GetRawText())!.AsObject();
        foreach (var stage in node["stages"]!.AsArray())
        {
            stage!.AsObject().Remove("transitions");
        }

        var definition = JsonSerializer.SerializeToElement(node);
        var run = QuestLifecycleReducer.CreateRun(definition, "instance/seq");
        var reduction = QuestLifecycleReducer.Reduce(definition, run.Instance,
            JsonSerializer.SerializeToElement(new { type = "objective.complete", objectiveId = "refuse" }), "q",
            planEffects: (_, _) => new());
        Assert.Equal(1, reduction.NextState.GetProperty("stageIndex").GetInt32());
    }

    [Fact]
    public void UnknownTargetsAndObjectivesAreRefusedWhenTheDefinitionLoads()
    {
        var node = JsonNode.Parse(Branching.GetRawText())!.AsObject();
        var transitions = node["stages"]![0]!["transitions"]!.AsArray();
        transitions[0]!["target"] = new JsonObject { ["stageId"] = "missing" };
        transitions[1]!["on"] = new JsonObject { ["kind"] = "objective-complete", ["objectiveId"] = "ghost" };
        var problems = QuestLifecycleReducer.TransitionProblems(node["stages"]!.AsArray()).ToArray();
        Assert.Contains(problems, problem => problem.Contains("unknown stage missing", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("unknown objective ghost", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => QuestLifecycleReducer.CreateRun(JsonSerializer.SerializeToElement(node), "instance/bad"));
    }

    private static Harness Create(JsonElement definition)
    {
        var instance = QuestLifecycleReducer.CreateRun(definition, "instance/fence").Instance;
        var ops = new List<string>();
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
                evaluationContext: context.State);
            if (reduction.Plan.Rejection is null)
            {
                instance = reduction.NextState;
                ops.AddRange((reduction.Plan.Events ?? [])
                    .Where(item => item.Type == "authored.effect")
                    .Select(item => item.Payload.GetProperty("op").GetString()!));
            }

            return reduction.Plan;
        };
        var kernel = new RuntimeKernel(JsonSerializer.SerializeToElement(new { }), new Dictionary<string, RuntimeCommandHandler>
        {
            ["quest"] = handler
        });
        return new Harness(kernel, () => instance, ops);
    }

    private sealed class Harness(RuntimeKernel kernel, Func<JsonElement> instance, List<string> ops)
    {
        public JsonElement Instance => instance();
        public List<string> Ops => ops;
        public int StageIndex => Instance.GetProperty("stageIndex").GetInt32();
        public string Status => Instance.GetProperty("status").GetString()!;

        public ValueTask<CommandDispatchResult> Dispatch(string occurrenceId, string type, string objectiveId) =>
            kernel.DispatchAsync(
                new(occurrenceId, "quest", JsonSerializer.SerializeToElement(new { type, objectiveId })),
                TestContext.Current.CancellationToken);
    }
}
