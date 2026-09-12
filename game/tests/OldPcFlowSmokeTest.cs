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

        await bridge.HandleOldPcInputAsync(Input("open", OfficialNotice));
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
        if (!bridge.IsOldPcDocumentAccessible(SavedMessage) ||
            bridge.OldPcState().GetProperty("activeDocumentId").GetString() != InternalRegister)
        {
            Fail("Internal register did not update the shared investigation state.");
            return;
        }

        var runtime = bridge.SelectRuntimeState();
        var contradictionQuest = runtime.GetProperty("quests")
            .GetProperty("urman.chapter1:quest/quest_marat_first_contradiction");
        if (contradictionQuest.GetProperty("status").GetString() != "completed" ||
            runtime.GetProperty("beats").GetProperty("urman.chapter1:beat/first-contradiction").GetString() != "completed")
        {
            Fail("Old PC evidence did not complete the authored contradiction quest through the shared kernel.");
            return;
        }

        GD.Print("oldpc-flow-smoke: official notice -> rejected early register -> Naila answer -> internal register -> authored quest completed -> saved message unlocked");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private static JsonElement Input(string type, string documentId) =>
        JsonSerializer.SerializeToElement(new { type, documentId });

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
