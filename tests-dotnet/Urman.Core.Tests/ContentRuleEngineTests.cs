using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;
using Xunit;

namespace Urman.Core.Tests;

public sealed class ContentRuleEngineTests
{
    [Fact]
    public async Task ContentApply_EvaluatesConditionsAndStagesEffectsInOrder()
    {
        using var kernel = new RuntimeKernel(NarrativeState.CreateInitial(), NarrativeCommandHandlers.Create());
        var result = await kernel.DispatchAsync(Command("rules:1", new
        {
            conditions = new object[]
            {
                new { op = "knowledge.status", knowledgeId = "urman.chapter1:knowledge/topic_marat_unresolved", status = "hidden" },
                new { op = "not", condition = new { op = "vocabulary.status", vocabularyId = "urman.chapter1:vocabulary/tt_urman", status = "confirmed" } }
            },
            effects = new object[]
            {
                new { op = "knowledge.set-status", knowledgeId = "urman.chapter1:knowledge/topic_marat_unresolved", status = "confirmed" },
                new { op = "vocabulary.set-status", vocabularyId = "urman.chapter1:vocabulary/tt_urman", status = "guessed" },
                new { op = "pressure.change", delta = 5 },
                new { op = "route.unlock", routeNodeId = "urman.chapter1:scene/kara-urman-edge" },
                new { op = "scene.request", sceneId = "urman.chapter1:scene/evidence-edge-sketch" },
                new { op = "audio.request", assetId = "urman.chapter1:asset/forest-voice" }
            }
        }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, result.Status);
        var state = kernel.SelectState();
        Assert.Equal("confirmed", state.GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/topic_marat_unresolved").GetProperty("status").GetString());
        Assert.Equal("guessed", state.GetProperty("vocabulary").GetProperty("urman.chapter1:vocabulary/tt_urman").GetProperty("status").GetString());
        Assert.Equal(3, state.GetProperty("pressure").GetInt32());
        Assert.Equal("urman.chapter1:scene/kara-urman-edge", state.GetProperty("routes")[0].GetString());
        Assert.Equal("runtime.audio.requested", Assert.Single(result.Events).Type);
    }

    [Fact]
    public async Task VocabularyStatusNeverStepsBackDownItsLadder()
    {
        using var kernel = new RuntimeKernel(NarrativeState.CreateInitial(), NarrativeCommandHandlers.Create());

        // A discovery understands the word.
        var learned = await kernel.DispatchAsync(Command("rules:learn", new
        {
            conditions = Array.Empty<object>(),
            effects = new object[]
            {
                new { op = "vocabulary.set-status", vocabularyId = "urman.chapter1:vocabulary/tt_babai", status = "confirmed" }
            }
        }), TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Committed, learned.Status);
        Assert.Equal("confirmed", VocabularyStatus(kernel, "tt_babai"));

        // Entering a scene where the word is only heard must not erase that.
        var heardAgain = await kernel.DispatchAsync(Command("rules:hear", new
        {
            conditions = Array.Empty<object>(),
            effects = new object[]
            {
                new { op = "vocabulary.set-status", vocabularyId = "urman.chapter1:vocabulary/tt_babai", status = "guessed" }
            }
        }), TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Committed, heardAgain.Status);
        Assert.Equal("confirmed", VocabularyStatus(kernel, "tt_babai"));

        // Guessing first and confirming later still improves the word.
        var guessed = await kernel.DispatchAsync(Command("rules:guess", new
        {
            conditions = Array.Empty<object>(),
            effects = new object[]
            {
                new { op = "vocabulary.set-status", vocabularyId = "urman.chapter1:vocabulary/tt_zirat", status = "guessed" }
            }
        }), TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Committed, guessed.Status);
        Assert.Equal("guessed", VocabularyStatus(kernel, "tt_zirat"));
        await kernel.DispatchAsync(Command("rules:confirm", new
        {
            conditions = Array.Empty<object>(),
            effects = new object[]
            {
                new { op = "vocabulary.set-status", vocabularyId = "urman.chapter1:vocabulary/tt_zirat", status = "confirmed" }
            }
        }), TestContext.Current.CancellationToken);
        Assert.Equal("confirmed", VocabularyStatus(kernel, "tt_zirat"));
    }

    private static string VocabularyStatus(RuntimeKernel kernel, string localId) =>
        kernel.SelectState().GetProperty("vocabulary")
            .GetProperty($"urman.chapter1:vocabulary/{localId}").GetProperty("status").GetString()!;

    [Fact]
    public async Task ContentApply_RejectionDoesNotMutateState()
    {
        using var kernel = new RuntimeKernel(NarrativeState.CreateInitial(), NarrativeCommandHandlers.Create());
        var before = kernel.SelectState();
        var result = await kernel.DispatchAsync(Command("rules:reject", new
        {
            conditions = new[]
            {
                new { op = "knowledge.status", knowledgeId = "urman.chapter1:knowledge/topic_marat_unresolved", status = "confirmed" }
            },
            effects = new[]
            {
                new { op = "pressure.change", delta = 1 }
            }
        }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Rejected, result.Status);
        Assert.Equal("ContentConditionRejected", result.Error?.Code);
        Assert.True(JsonElement.DeepEquals(before, kernel.SelectState()));
    }

    [Fact]
    public async Task ContentApply_OpeningDocumentAndRecordingJournalUseSharedPresentationState()
    {
        using var kernel = new RuntimeKernel(NarrativeState.CreateInitial(), NarrativeCommandHandlers.Create());
        var result = await kernel.DispatchAsync(Command("document:open", new
        {
            conditions = Array.Empty<object>(),
            effects = new object[]
            {
                new { op = "document.open", documentId = "urman.fullgame:document/baranov-1967" },
                new { op = "journal.record", entryId = "urman.fullgame:document/baranov-1967", sourceId = "urman.fullgame:document/baranov-1967" }
            }
        }), TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Committed, result.Status);
        var state = kernel.SelectState();
        Assert.Equal("urman.fullgame:document/baranov-1967", state.GetProperty("presentation").GetProperty("openedDocumentIds")[0].GetString());
        Assert.Equal("urman.fullgame:document/baranov-1967", state.GetProperty("journal")[0].GetProperty("entryId").GetString());
        Assert.Contains(result.Events, gameEvent => gameEvent.Type == "document.opened");
        Assert.Contains(result.Events, gameEvent => gameEvent.Type == "journal.changed");
    }

    private static GameCommand Command(string occurrenceId, object payload) =>
        new(occurrenceId, NarrativeCommandHandlers.ContentApply, JsonSerializer.SerializeToElement(payload));
}
