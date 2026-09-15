using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class OldPcFlowSmokeTest : Node
{
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string InternalRegister = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string SavedMessage = "urman.oldpc:document/msg_marat_saved_last_normal";

    public override async void _Ready()
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

        foreach (var unread in new[] { OfficialNotice, InternalRegister })
            if (!await RejectUnreadSave(bridge, unread)) return;

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
            || !bridge.IsOldPcDocumentAccessible(InternalRegister))
        { Fail("Naila's authored answer did not unlock the register after the notice."); return; }

        await bridge.HandleOldPcInputAsync(Input("open", InternalRegister));
        if (bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.CompareJournalSourcesAsync("urman.chapter1:interaction/compare-records-contradiction", new[] { OfficialNotice, InternalRegister }))
        { Fail("The register must require an explicit source comparison."); return; }
        if (bridge.IsOldPcDocumentAccessible(SavedMessage)
            || !await bridge.ChooseDialogueAsync("urman.chapter1:dialogue/naila_medical_record", "follow-up", "press-contradiction")
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

        var runtime = bridge.SelectRuntimeState();
        var contradictionQuest = runtime.GetProperty("quests")
            .GetProperty("urman.chapter1:quest/quest_marat_first_contradiction");
        if (contradictionQuest.GetProperty("status").GetString() != "completed" ||
            runtime.GetProperty("beats").GetProperty("urman.chapter1:beat/first-contradiction").GetString() != "completed")
        {
            Fail("Old PC evidence did not complete the authored contradiction quest through the shared kernel.");
            return;
        }

        GD.Print("oldpc-flow-smoke: rejected unread/locked saves without side effects -> repeated valid save -> official notice -> rejected early register -> Naila permission -> internal register -> raw comparison -> Naila category response -> source check -> rejected early message -> Rinat question -> saved message unlocked");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static JsonElement Input(string type, string documentId) =>
        JsonSerializer.SerializeToElement(new { type, documentId });

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
