using System.Text.Json;
using Urman.Core.Contracts;
using Urman.Core.Narrative;
using Urman.Core.Runtime;
using Xunit;

namespace Urman.Core.Tests;

public sealed class NarrativeStateTests
{
    [Fact]
    public async Task JournalDialogueVocabularyQuestAndKnowledgeShareOneKernelState()
    {
        using var kernel = new RuntimeKernel(NarrativeState.CreateInitial(), NarrativeCommandHandlers.Create());
        var commands = new[]
        {
            Command("n:1", NarrativeCommandHandlers.KnowledgeSetStatus, new
            {
                knowledgeId = "urman.chapter1:knowledge/clue_do_not_answer_rule",
                status = "confirmed"
            }),
            Command("n:2", NarrativeCommandHandlers.VocabularyLearn, new
            {
                wordId = "urman.chapter1:vocabulary/yaramyy",
                sourceId = "urman.chapter1:dialogue/gulsina_yaramyy"
            }),
            Command("n:3", NarrativeCommandHandlers.JournalRecord, new
            {
                entryId = "urman.chapter1:knowledge/clue_do_not_answer_rule",
                sourceId = "urman.oldpc:document/msg_marat_saved_last_normal"
            }),
            Command("n:4", NarrativeCommandHandlers.DialogueChoose, new
            {
                dialogueId = "urman.chapter1:dialogue/gulsina_yaramyy",
                nodeId = "warning",
                choiceId = "ask-meaning"
            }),
            Command("n:5", NarrativeCommandHandlers.QuestSetStage, new
            {
                questId = "urman.chapter1:quest/quest_language_reread",
                stageId = "reread-source",
                status = "active"
            }),
            // A delayed observation must attach its context without erasing
            // a confirmation that committed while the observer was waiting.
            Command("n:6", NarrativeCommandHandlers.VocabularyLearn, new
            {
                wordId = "urman.chapter1:vocabulary/yaramyy",
                sourceId = "urman.oldpc:document/doc_household_radio_log",
                status = "guessed"
            })
        };

        foreach (var command in commands)
        {
            var result = await kernel.DispatchAsync(command, TestContext.Current.CancellationToken);
            Assert.Equal(CommandStatus.Committed, result.Status);
        }

        var state = kernel.SelectState();
        Assert.Equal("confirmed", state.GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/clue_do_not_answer_rule").GetProperty("status").GetString());
        Assert.Equal("confirmed", state.GetProperty("vocabulary").GetProperty("urman.chapter1:vocabulary/yaramyy").GetProperty("status").GetString());
        Assert.Single(state.GetProperty("journal").EnumerateArray());
        Assert.Single(state.GetProperty("dialogueChoices").EnumerateArray());
        Assert.Equal("active", state.GetProperty("quests").GetProperty("urman.chapter1:quest/quest_language_reread").GetProperty("status").GetString());
        Assert.Equal(6, kernel.CaptureSnapshot().EventSequence);
    }

    private static GameCommand Command(string occurrenceId, string type, object payload) =>
        new(occurrenceId, type, JsonSerializer.SerializeToElement(payload));
}
