using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Quests;
using Urman.Studio.Core.Quests;
using Xunit;

namespace Urman.Studio.Tests;

// Spike STUDIO.X3: one quest model behind both the list and the graph view.
public sealed class QuestFlowTests
{
    private static JsonObject Linear() => JsonNode.Parse("""
    {
      "schemaVersion": 1,
      "id": "sample:quest/fence",
      "titleTextId": "sample:text/fence",
      "stages": [
        { "id": "talk", "objectives": [ { "id": "accept", "titleTextId": "sample:text/accept", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] } ],
          "composition": { "mode": "all", "objectiveIds": [ "accept" ] } },
        { "id": "boards", "objectives": [
            { "id": "a", "titleTextId": "sample:text/a", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] },
            { "id": "b", "titleTextId": "sample:text/b", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] },
            { "id": "c", "titleTextId": "sample:text/c", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] } ],
          "composition": { "mode": "threshold", "objectiveIds": [ "a", "b", "c" ], "threshold": 2 } },
        { "id": "return", "objectives": [ { "id": "hand-in", "titleTextId": "sample:text/hand-in", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] } ],
          "composition": { "mode": "all", "objectiveIds": [ "hand-in" ] } }
      ],
      "outcomes": { "success": [], "optional": [], "failure": [] },
      "retryPolicy": { "mode": "none", "maximumAttempts": 1 },
      "checkpointPolicy": { "mode": "stage" },
      "cancelPolicy": { "allowed": false, "effects": [] }
    }
    """)!.AsObject();

    // talk{ accept -> boards(2 of 3) -> return | refuse -> [failed/refused] | ask-guy -> return } join at return
    private static JsonObject Prototype()
    {
        var quest = Linear();
        quest = QuestFlowEditor.AddBranchArm(quest, "talk", "refuse", "sample:text/refuse", "refused",
            FlowTarget.Ending("failed", "refused"), new JsonArray(new JsonObject { ["op"] = "npc.set-state" }));
        quest = QuestFlowEditor.AddBranchArm(quest, "talk", "ask-guy", "sample:text/ask-guy", "via-guy", FlowTarget.Stage("return"));
        return quest;
    }

    [Fact]
    public void LinearQuestIsAPlainSequenceEndingInSuccess()
    {
        Assert.Equal("talk boards return [end:completed]", QuestOutline.Describe(QuestOutline.Build(new QuestFlow(Linear()))));
    }

    [Fact]
    public void BranchesThresholdGroupAndJoinAppearInTheListWithoutFlattening()
    {
        var outline = QuestOutline.Describe(QuestOutline.Build(new QuestFlow(Prototype())));
        Assert.Equal("talk{next: boards | refused: [end:failed/refused] | via-guy: } join:return return [end:completed]", outline);
    }

    [Fact]
    public void InsertingFromTheListKeepsTheBranchesAndTheJoin()
    {
        var edited = QuestFlowEditor.InsertStageOnEdge(Prototype(), "boards", null, "s-5f2c0a91", "ask-dry", "sample:text/ask-dry");
        var outline = QuestOutline.Describe(QuestOutline.Build(new QuestFlow(edited)));
        Assert.Equal("talk{next: boards s-5f2c0a91 | refused: [end:failed/refused] | via-guy: } join:return return [end:completed]", outline);

        var removed = QuestFlowEditor.RemoveStage(edited, "s-5f2c0a91");
        Assert.Equal(
            QuestOutline.Describe(QuestOutline.Build(new QuestFlow(Prototype()))),
            QuestOutline.Describe(QuestOutline.Build(new QuestFlow(removed))));
    }

    [Fact]
    public void GraphAndListSurviveSaveAndReload()
    {
        var edited = QuestFlowEditor.InsertStageOnEdge(Prototype(), "boards", null, "s-5f2c0a91", "ask-dry", "sample:text/ask-dry");
        var reloaded = JsonNode.Parse(edited.ToJsonString())!.AsObject();
        var before = new QuestFlow(edited);
        var after = new QuestFlow(reloaded);
        Assert.Equal(before.AllEdges(), after.AllEdges());
        Assert.Equal(QuestOutline.Describe(QuestOutline.Build(before)), QuestOutline.Describe(QuestOutline.Build(after)));
    }

    [Fact]
    public void LoopIsShownAsAJumpNotExpanded()
    {
        var quest = QuestFlowEditor.AddBranchArm(Linear(), "return", "not-enough", "sample:text/not-enough", "back", FlowTarget.Stage("boards"));
        var outline = QuestOutline.Describe(QuestOutline.Build(new QuestFlow(quest)));
        Assert.Equal("talk boards return{next: [end:completed] | back: ->boards}", outline);
    }

    [Fact]
    public void RemovingABranchPointIsRefusedWithAReason()
    {
        var error = Assert.Throws<QuestEditException>(() => QuestFlowEditor.RemoveStage(Prototype(), "talk"));
        Assert.Contains("несколько выходов", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EditedQuestIsExecutedByTheGameReducerAlongEitherBranch()
    {
        var definition = JsonSerializer.SerializeToElement(QuestFlowEditor.InsertStageOnEdge(
            Prototype(), "boards", null, "s-5f2c0a91", "ask-dry", "sample:text/ask-dry"));
        Assert.Empty(QuestLifecycleReducer.TransitionProblems(JsonNode.Parse(definition.GetRawText())!["stages"]!.AsArray()));

        string Run(params string[] objectives)
        {
            var state = QuestLifecycleReducer.CreateRun(definition, "instance/fence").Instance;
            var path = new List<string>();
            foreach (var objective in objectives)
            {
                var reduction = QuestLifecycleReducer.Reduce(definition, state,
                    JsonSerializer.SerializeToElement(new { type = "objective.complete", objectiveId = objective }), "q",
                    planEffects: (_, _) => new());
                Assert.Null(reduction.Plan.Rejection);
                state = reduction.NextState;
                path.Add((string)JsonNode.Parse(definition.GetRawText())!["stages"]![state.GetProperty("stageIndex").GetInt32()]!["id"]!);
            }

            var outcome = state.TryGetProperty("outcomeId", out var id) ? "/" + id.GetString() : "";
            return $"{string.Join(">", path)} {state.GetProperty("status").GetString()}{outcome}";
        }

        Assert.Equal("boards>boards>s-5f2c0a91>return>return completed", Run("accept", "a", "c", "ask-dry", "hand-in"));
        Assert.Equal("talk failed/refused", Run("refuse"));
        Assert.Equal("return>return completed", Run("ask-guy", "hand-in"));
    }
}
