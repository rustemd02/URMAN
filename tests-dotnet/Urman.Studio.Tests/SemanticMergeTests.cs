using System.Text.Json.Nodes;
using Urman.Core.Quests;
using Urman.Studio.Core.Collaboration;
using Urman.Studio.Core.Quests;
using Xunit;

namespace Urman.Studio.Tests;

// Spike STUDIO.X4: merging two authors' revisions by entity and field (A19).
public sealed class SemanticMergeTests
{
    // A content module is an array of entities with namespaced IDs.
    private static JsonArray Module() => JsonNode.Parse("""
    [
      { "id": "m:text/line-1", "value": { "default": "Эти уже не нормальные." }, "purpose": "ui" },
      { "id": "m:text/line-2", "value": { "default": "Шесть досок принесёшь." }, "purpose": "ui" },
      { "id": "m:world/board-3", "position": [ 91.0, 0.0, 47.0 ], "rotation": 0 },
      { "id": "m:world/gate", "position": [ 52.0, 0.0, 28.2 ], "rotation": 0, "state": "closed" }
    ]
    """)!.AsArray();

    private static JsonObject Entity(JsonNode? module, string id) =>
        module!.AsArray().OfType<JsonObject>().Single(item => (string)item["id"]! == id);

    [Fact]
    public void DifferentEntitiesAndDifferentFieldsMergeWithoutQuestions()
    {
        var mine = Module();
        Entity(mine, "m:world/gate")["position"] = new JsonArray(55.2, 0.0, 28.2);
        var theirs = Module();
        Entity(theirs, "m:world/gate")["rotation"] = 12;
        Entity(theirs, "m:text/line-2")["value"]!["default"] = "Шесть досок. Сухих.";
        theirs.Add(new JsonObject { ["id"] = "m:world/birch-9a1f", ["position"] = new JsonArray(10.0, 0.0, 5.0) });

        var result = SemanticMerge.Merge(Module(), mine, theirs);

        Assert.Empty(result.Conflicts);
        var gate = Entity(result.Merged, "m:world/gate");
        Assert.Equal(55.2, (double)gate["position"]![0]!);
        Assert.Equal(12, (int)gate["rotation"]!);
        Assert.Equal("Шесть досок. Сухих.", (string)Entity(result.Merged, "m:text/line-2")["value"]!["default"]!);
        Assert.Equal("m:world/birch-9a1f", (string)result.Merged!.AsArray()[^1]!["id"]!);
    }

    [Fact]
    public void SameFieldChangedTwiceIsAFieldConflictThatKeepsLocalWorkUntilDecided()
    {
        var mine = Module();
        Entity(mine, "m:text/line-1")["value"]!["default"] = "Эти уже не годятся.";
        var theirs = Module();
        Entity(theirs, "m:text/line-1")["value"]!["default"] = "Эти уже не нормальные, Айдар Ришатович.";

        var result = SemanticMerge.Merge(Module(), mine, theirs);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(MergeConflictKind.Field, conflict.Kind);
        Assert.Equal("[id=m:text/line-1]/value/default", conflict.Path);
        Assert.Equal("Эти уже не нормальные.", (string)conflict.Base!);
        Assert.Equal("Эти уже не годятся.", (string)Entity(result.Merged, "m:text/line-1")["value"]!["default"]!);
    }

    [Fact]
    public void DeletionAgainstEditIsAlwaysAnExplicitConflict()
    {
        var mine = Module();
        Entity(mine, "m:world/board-3")["position"] = new JsonArray(95.0, 0.0, 47.0);
        var theirs = Module();
        theirs.Remove(Entity(theirs, "m:world/board-3"));

        var result = SemanticMerge.Merge(Module(), mine, theirs);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(MergeConflictKind.DeleteEdit, conflict.Kind);
        Assert.Null(conflict.Theirs);
        Assert.Contains(result.Merged!.AsArray(), item => (string)item!["id"]! == "m:world/board-3");
    }

    [Fact]
    public void UnchangedEntityDeletedByTheOtherSideIsRemovedQuietly()
    {
        var theirs = Module();
        theirs.Remove(Entity(theirs, "m:world/board-3"));
        var result = SemanticMerge.Merge(Module(), Module(), theirs);
        Assert.Empty(result.Conflicts);
        Assert.DoesNotContain(result.Merged!.AsArray(), item => (string)item!["id"]! == "m:world/board-3");
    }

    [Fact]
    public void StepsAreMatchedByIdSoTwoInsertionsMergeAndTwoReordersConflict()
    {
        JsonArray Steps(params string[] ids) => new(ids.Select(id => (JsonNode?)new JsonObject { ["id"] = id }).ToArray());
        var @base = Steps("boards", "return");

        var inserted = SemanticMerge.Merge(@base, Steps("boards", "ask-guy", "return"), Steps("move-car", "boards", "return"));
        Assert.Empty(inserted.Conflicts);
        Assert.Equal(["move-car", "boards", "ask-guy", "return"], inserted.Merged!.AsArray().Select(item => (string)item!["id"]!));

        var reordered = SemanticMerge.Merge(Steps("a", "b", "c"), Steps("b", "a", "c"), Steps("a", "c", "b"));
        Assert.Equal(MergeConflictKind.Order, Assert.Single(reordered.Conflicts).Kind);
    }

    [Fact]
    public void TakingTheIncomingOrderMovesWholeEntitiesInsteadOfReplacingThemWithTheirIds()
    {
        JsonArray Lines(params string[] ids) =>
            new(ids.Select(id => (JsonNode?)new JsonObject { ["id"] = id, ["value"] = new JsonObject { ["default"] = id } }).ToArray());
        var @base = Lines("a", "b", "c");
        var mine = Lines("b", "a", "c");
        var theirs = Lines("a", "c", "b");

        var merge = SemanticMerge.Merge(@base, mine, theirs);
        var conflict = Assert.Single(merge.Conflicts);
        Assert.Equal(MergeConflictKind.Order, conflict.Kind);
        Assert.Equal("", conflict.Path);

        var kept = ConflictResolution.Apply(merge, new Dictionary<MergeConflict, ConflictChoice> { [conflict] = ConflictChoice.KeepMine });
        Assert.Equal(["b", "a", "c"], kept.AsArray().Select(item => (string)item!["id"]!));

        var taken = ConflictResolution.Apply(merge, new Dictionary<MergeConflict, ConflictChoice> { [conflict] = ConflictChoice.TakeTheirs });

        Assert.Equal(["a", "c", "b"], taken.AsArray().Select(item => (string)item!["id"]!));
        Assert.All(taken.AsArray(), item => Assert.Equal((string)item!["id"]!, (string)item["value"]!["default"]!));
    }

    [Fact]
    public void OrderConflictInsideAStageListKeepsEntitiesAndUnmentionedOnesInPlace()
    {
        JsonArray Stages(params string[] ids) => new(ids.Select(id => (JsonNode?)new JsonObject { ["id"] = id }).ToArray());
        JsonObject Quest(params string[] ids) => new() { ["id"] = "q", ["stages"] = Stages(ids) };
        var @base = Quest("talk", "boards", "return");
        var mine = Quest("boards", "talk", "return");
        var theirs = Quest("talk", "return", "boards");

        var merge = SemanticMerge.Merge(@base, mine, theirs);
        var conflict = Assert.Single(merge.Conflicts);
        Assert.Equal("/stages", conflict.Path);

        var taken = ConflictResolution.Apply(merge, new Dictionary<MergeConflict, ConflictChoice> { [conflict] = ConflictChoice.TakeTheirs });

        Assert.Equal(["talk", "return", "boards"], taken["stages"]!.AsArray().Select(item => (string)item!["id"]!));
        Assert.All(taken["stages"]!.AsArray(), item => Assert.IsType<JsonObject>(item));
    }

    [Fact]
    public void CleanTextMergeCanStillBreakAReferenceSoTheResultIsCheckedSemantically()
    {
        var quest = JsonNode.Parse("""
        { "stages": [
          { "id": "talk", "objectives": [ { "id": "accept" } ], "composition": { "mode": "all", "objectiveIds": [ "accept" ] } },
          { "id": "boards", "objectives": [ { "id": "a" } ], "composition": { "mode": "all", "objectiveIds": [ "a" ] } },
          { "id": "return", "objectives": [ { "id": "hand-in" } ], "composition": { "mode": "all", "objectiveIds": [ "hand-in" ] } } ] }
        """)!.AsObject();
        var mine = (JsonObject)quest.DeepClone();
        mine["stages"]![0]!["transitions"] = JsonNode.Parse("""[ { "id": "skip", "on": { "kind": "stage-complete" }, "conditions": [], "effects": [], "target": { "stageId": "boards" } } ]""");
        var theirs = (JsonObject)quest.DeepClone();
        theirs["stages"]!.AsArray().RemoveAt(1);

        var result = SemanticMerge.Merge(quest, mine, theirs);

        Assert.Empty(result.Conflicts);
        var problems = QuestLifecycleReducer.TransitionProblems(result.Merged!["stages"]!.AsArray()).ToArray();
        Assert.Contains(problems, problem => problem.Contains("unknown stage boards", StringComparison.Ordinal));
    }

    [Fact]
    public void BinaryResourcesAreNeverBlended()
    {
        Assert.Equal(BinaryMergeOutcome.TakeTheirs, BinaryMerge.Decide("a", "a", "b"));
        Assert.Equal(BinaryMergeOutcome.TakeMine, BinaryMerge.Decide("a", "b", "a"));
        Assert.Equal(BinaryMergeOutcome.Conflict, BinaryMerge.Decide("a", "b", "c"));
        Assert.Equal(BinaryMergeOutcome.TakeMine, BinaryMerge.Decide("a", "b", "b"));
    }

    [Fact]
    public void QuestFlowEditsFromTwoAuthorsMergeIntoAnExecutableQuest()
    {
        var @base = JsonNode.Parse("""
        { "id": "q", "stages": [
          { "id": "talk", "objectives": [ { "id": "accept", "titleTextId": "t", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] } ], "composition": { "mode": "all", "objectiveIds": [ "accept" ] } },
          { "id": "return", "objectives": [ { "id": "hand-in", "titleTextId": "t", "optional": false, "startConditions": [], "completionConditions": [], "completionEffects": [], "failureEffects": [] } ], "composition": { "mode": "all", "objectiveIds": [ "hand-in" ] } } ] }
        """)!.AsObject();
        var mine = QuestFlowEditor.InsertStageOnEdge(@base, "talk", null, "s-mine", "ask-guy", "t");
        var theirs = QuestFlowEditor.InsertStageOnEdge(@base, "return", null, "s-theirs", "stack-boards", "t");

        var result = SemanticMerge.Merge(@base, mine, theirs);

        Assert.Empty(result.Conflicts);
        Assert.Empty(QuestLifecycleReducer.TransitionProblems(result.Merged!["stages"]!.AsArray()));
        Assert.Equal("talk s-mine return s-theirs [end:completed]",
            QuestOutline.Describe(QuestOutline.Build(new QuestFlow(result.Merged!.AsObject()))));
    }
}
