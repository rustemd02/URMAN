using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class OldPcFlowSmokeTest : Node
{
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string InternalRegister = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string SavedMessage = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string BoundaryArticle = "urman.oldpc:document/tw_shurale_urman_boundary";

    public override async void _Ready()
    {
        try
        {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Old PC flow could not instantiate main.");
            return;
        }

        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || !bridge.IsOldPcDocumentAccessible(OfficialNotice) || bridge.IsOldPcDocumentAccessible(InternalRegister))
        {
            Fail("Old PC initial access projection is incorrect.");
            return;
        }

        foreach (var unread in new[] { OfficialNotice, InternalRegister,
                     "urman.oldpc:document/msg_mansur_unsent_note", "urman.oldpc:document/rec_internal_accounting_damaged" })
            if (!await RejectUnreadSave(bridge, unread)) return;
        foreach (var gated in new[] { "urman.oldpc:document/msg_mansur_unsent_note", "urman.oldpc:document/rec_internal_accounting_damaged" })
            if (bridge.IsOldPcDocumentAccessible(gated) || await bridge.OpenDocumentAsync(gated))
            { Fail("A fresh archive exposed a source before its existing investigation prerequisites: " + gated); return; }

        await bridge.HandleOldPcInputAsync(Input("open", OfficialNotice));
        await bridge.HandleOldPcInputAsync(Input("save", OfficialNotice));
        await bridge.HandleOldPcInputAsync(Input("save", OfficialNotice));
        if (bridge.JournalEntries().Count(entry => entry.EntryId == OfficialNotice) != 1
            || bridge.OldPcState().GetProperty("savedDocumentIds").EnumerateArray()
                .Count(id => id.GetString() == OfficialNotice) != 1)
        { Fail("Saving an already opened document twice created duplicate evidence."); return; }
        if (bridge.IsOldPcDocumentAccessible(InternalRegister))
        { Fail("The first notice bypassed Naila's record permission."); return; }
        var sceneBeforeRejectedOpen = bridge.ActiveSceneId;
        var earlyOpenRejected = false;
        try { await bridge.HandleOldPcInputAsync(Input("open", InternalRegister)); }
        catch (InvalidOperationException) { earlyOpenRejected = true; }
        if (!earlyOpenRejected || bridge.ActiveSceneId != sceneBeforeRejectedOpen)
        { Fail("An early register open was accepted or changed the scene."); return; }
        if (!await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "official-wording", "ask-wording")
            || bridge.IsOldPcDocumentAccessible(InternalRegister)
            || await bridge.OpenDocumentAsync(InternalRegister))
        { Fail("Naila's permission bypassed checking the earlier account against the notice."); return; }
        if (!await Act1AlsuWalkProof.CompleteAsync(this, bridge)
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "name-road", "ask-versions")
            || bridge.IsOldPcDocumentAccessible(InternalRegister)
            || !await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-versions-scope", new[]
                { OfficialNotice, "urman.chapter1:knowledge/clue_alsu_heard_versions" })
            || !bridge.IsOldPcDocumentAccessible(InternalRegister))
        { Fail("The read notice, checked account and Naila's actual answer did not jointly unlock the register."); return; }

        await bridge.HandleOldPcInputAsync(Input("open", InternalRegister));
        if (await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-records-contradiction", new[] { OfficialNotice, InternalRegister }))
        { Fail("The archive compared fields before the actual source selections."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        if (bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-records-contradiction", new[] { OfficialNotice, InternalRegister }))
        { Fail("The register must require an explicit source comparison."); return; }
        if (bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "follow-up", "press-contradiction")
            || Known(bridge, "clue_naila_record_scope")
            || await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/naila_medical_record", "separate-record")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "record-question", "show-external-wording")
            || Known(bridge, "clue_naila_record_scope")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "matching-formulation", "ask-category-scope")
            || bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-record-scope", new[] { InternalRegister, "urman.chapter1:knowledge/clue_naila_record_scope" }))
        { Fail("The old PC bypassed the source-holder check or rejected the grounded interpretation."); return; }
        if (bridge.IsOldPcDocumentAccessible(SavedMessage) ||
            bridge.OldPcState().GetProperty("activeDocumentId").GetString() != InternalRegister)
        {
            Fail("The source comparison changed the active document or skipped the pending Rinat question.");
            return;
        }
        if (await bridge.OpenDocumentAsync(SavedMessage))
        { Fail("The direct document reader skipped the pending Rinat question."); return; }
        var earlyMessageRejected = false;
        try { await bridge.HandleOldPcInputAsync(Input("open", SavedMessage)); }
        catch (InvalidOperationException) { earlyMessageRejected = true; }
        if (!earlyMessageRejected
            || !await bridge.EnterDialogueNodeAsync("urman.chapter1:dialogue/rinat_internal_register", "dangerous-category")
            || bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/rinat_internal_register", "dangerous-category", "present-category")
            || !bridge.IsOldPcDocumentAccessible(SavedMessage))
        { Fail("Only Rinat's concrete category response should unlock Marat's saved message."); return; }

        await bridge.HandleOldPcInputAsync(Input("open", SavedMessage));
        await bridge.HandleOldPcInputAsync(Input("save", SavedMessage));
        await bridge.HandleOldPcInputAsync(Input("open", SavedMessage));
        if (!Known(bridge, "clue_marat_message_read") || Known(bridge, "clue_marat_was_afraid_before_death")
            || bridge.IsOldPcDocumentAccessible(BoundaryArticle) || await bridge.OpenDocumentAsync(BoundaryArticle))
        { Fail("Opening, saving or rereading a message replaced checking its words with Alsu."); return; }
        var earlyArticleRejected = false;
        try { await bridge.HandleOldPcInputAsync(Input("open", BoundaryArticle)); }
        catch (InvalidOperationException) { earlyArticleRejected = true; }
        if (!earlyArticleRejected
            || bridge.OldPcState().GetProperty("activeDocumentId").GetString() != SavedMessage
            || Known(bridge, "clue_message_question_prepared")
            || bridge.IsOldPcDocumentAccessible(BoundaryArticle))
        { Fail("The archive reader bypassed the message gate or a prepared question counted as an answer."); return; }
        await Act1SourceExcerptProof.RecordMessageVoiceAsync(this, bridge);
        if (!Known(bridge, "clue_message_question_prepared") || Known(bridge, "clue_alsu_message_reply")
            || bridge.OldPcState().GetProperty("activeDocumentId").GetString() != SavedMessage)
        { Fail("Selecting the actual message changed the archive document or supplied Alsu's reply."); return; }
        if (!await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "message-return", "show-saved-message")
            || Known(bridge, "clue_alsu_message_reply")
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/alsu_route_context", "message-question", "quote-heard-voice")
            || !Known(bridge, "clue_alsu_message_reply") || !Known(bridge, "clue_marat_was_afraid_before_death")
            || !bridge.IsOldPcDocumentAccessible(BoundaryArticle)
            || bridge.OldPcState().GetProperty("activeDocumentId").GetString() != SavedMessage)
        { Fail("The checked quotation and Alsu's reply did not unlock the article while preserving the open document."); return; }

        var runtime = bridge.SelectRuntimeState();
        var contradictionQuest = runtime.GetProperty("quests")
            .GetProperty("urman.chapter1:quest/quest_marat_first_contradiction");
        if (contradictionQuest.GetProperty("status").GetString() != "completed" ||
            runtime.GetProperty("beats").GetProperty("urman.chapter1:beat/first-contradiction").GetString() != "completed")
        {
            Fail("Old PC evidence did not complete the authored contradiction quest through the shared kernel.");
            return;
        }

        GD.Print("oldpc-flow-smoke: rejected unread/locked saves without side effects -> repeated valid save -> notice -> Naila permission alone insufficient -> Alsu source check -> register -> raw comparison -> Naila field-specific category response -> source check -> rejected early message -> Rinat question -> message reading and saving cannot bypass its source check -> checked quotation and Alsu reply unlock article");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
        }
        catch (Exception error) { Fail(error.ToString()); }
    }

    private static JsonElement Input(string type, string documentId) =>
        JsonSerializer.SerializeToElement(new { type, documentId });

    private static bool Known(RuntimeBridge bridge, string localId) => bridge.SelectRuntimeState()
        .GetProperty("knowledge").GetProperty("urman.chapter1:knowledge/" + localId)
        .GetProperty("status").GetString() == "confirmed";

    private async Task<bool> RejectUnreadSave(RuntimeBridge bridge, string documentId)
    {
        var before = bridge.SelectRuntimeState();
        var capability = bridge.OldPcState().GetRawText();
        var scene = bridge.ActiveSceneId;
        var rejected = false;
        try { await bridge.HandleOldPcInputAsync(Input("save", documentId)); }
        catch (InvalidOperationException) { rejected = true; }
        var after = bridge.SelectRuntimeState();
        if (!rejected || bridge.ActiveSceneId != scene
            || bridge.OldPcState().GetRawText() != capability
            || after.GetProperty("journal").GetRawText() != before.GetProperty("journal").GetRawText()
            || after.GetProperty("knowledge").GetRawText() != before.GetProperty("knowledge").GetRawText())
        {
            Fail("Saving an unread document was accepted or changed state before rejecting: " + documentId);
            return false;
        }
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
