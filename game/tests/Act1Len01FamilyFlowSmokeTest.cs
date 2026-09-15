using System.Linq;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Starts a normal demo session and exercises the family investigation through
/// production dialogue, archive and journal owners. Revisit actions are invoked
/// on the physical targets; the corridor/walkthrough smokes own camera and walking
/// coverage. This is a state/interaction regression, never a human-duration test.
/// Run only through eng/run-smoke-guarded.sh after the C# build.
/// </summary>
public partial class Act1Len01FamilyFlowSmokeTest : Node
{
    private const string Prefix = "urman.chapter1:";
    private const string Official = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string Register = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string Message = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string Niva = "urman.oldpc:document/doc_household_misc_niva_receipt";
    private const string Electricity = "urman.oldpc:document/doc_household_electric_receipts";
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private DialogueUi _dialogue = null!;
    private OldPcUi _pc = null!;
    private FirstPersonController _player = null!;

    public override async void _Ready()
    {
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")!.Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(8);
            Require(await this.StartThroughMainMenuAsync(_demo), "Normal New Game failed.");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _dialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
            _pc = (OldPcUi)GetTree().GetFirstNodeInGroup("old_pc_ui");
            _player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(4);
            Require(!_demo.IntroVisible && !_player.ModalOpen, "Intro did not release input.");

            await Move("arrival-enter-house", "house_old_pc", "entry");
            Require(!Known("clue_family_avoids_marat") && Beat("house-warmth-and-pause") != "completed",
                "Entering the house fabricated a family conversation.");
            await Talk("talk-mansur", "mansur_pc_request");
            Require(!Npc("mansur", "pc_access_granted") && !Available("oldpc-power"),
                "Opening a request granted access before Mansur answered.");
            await CloseTalk();
            await RoundTrip("len01-unanswered-request");
            Require(!Npc("mansur", "pc_access_granted") && !Known("clue_mansur_household_request"),
                "An unanswered request turned into permission after load.");

            await Talk("talk-gulsina", "gulsina_yaramyy");
            Require(HasChoice("choice-gulsina-marat") && !Known("clue_marat_official_death_version"),
                "The family question is still hidden until the official document.");
            await Choose("choice-gulsina-stay-for-tea");
            await CloseTalk();
            Require(Beat("house-warmth-and-pause") == "completed" && !Known("clue_family_avoids_marat"),
                "Sitting for tea did not play the home beat, or invented a reply about Marat.");

            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-why");
            await CloseTalk();
            Require(Npc("mansur", "pc_access_granted") && Known("clue_mansur_household_request")
                && !Known("clue_marat_official_death_version"), "The domestic request did not grant just the requested access.");

            await OpenPc();
            await Search("нет-такого-слова-len01");
            Require(SelectableResults() == 0 && !Known("clue_household_niva_list"), "An empty search granted its target.");
            await Search("квитанци");
            Require(HasDocument(Niva) && HasDocument(Electricity), "Receipt search lost the two meaningful candidates.");
            await ReadFromPc(Electricity);
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            Require(!HasChoice("choice-mansur-niva-repairs"), "A solution was selectable before finding the Niva list.");
            await Choose("choice-mansur-electric-receipts");
            Require(!Npc("mansur", "household_help_done"), "An unrelated receipt completed the request.");
            await CloseTalk();
            await RoundTrip("len01-wrong-receipt");
            Require(Known("clue_household_electric_receipts") && !Known("clue_household_niva_list")
                && !Npc("mansur", "household_help_done"), "Load lost the wrong-source state or silently completed the task.");

            await OpenPc();
            await Search("Нива");
            await ReadFromPc(Niva);
            Require(!Npc("mansur", "household_help_done"), "Merely opening the list completed the return conversation.");
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            await Choose("choice-mansur-niva-wiring");
            Require(!Npc("mansur", "household_help_done"), "Reading the wiring instruction backwards was accepted.");
            await CloseTalk();

            await OpenPc();
            await Search("Нива");
            await ReadFromPc(Niva);
            await ClosePc();
            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-check-household-list");
            await Choose("choice-mansur-niva-repairs");
            await CloseTalk();
            Require(Npc("mansur", "household_help_done") && Known("clue_household_request_checked")
                && !Known("clue_family_avoids_marat"), "The checked household request fabricated a Marat clue.");
            await RoundTrip("len01-checked-list");
            await Talk("talk-mansur", "mansur_pc_request");
            Require(!HasChoice("choice-mansur-check-household-list"), "A completed request restarted after load.");
            await CloseTalk();
            Require(_bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
                .EnumerateArray().Count(id => id.GetString() == Niva) == 1, "Rereading duplicated the document history.");

            // Direct opening and broad searches remain possible; neither may
            // fabricate the human answers that the first investigation needs.
            await OpenPc();
            await Search("Марат");
            Require(!HasDocument(Register) && !HasDocument(Message), "Search exposed later sources before their real prerequisites.");
            await ReadFromPc(Official);
            await ClosePc();
            Require(!Known("clue_family_avoids_marat") && !Available("house-to-route"),
                "Opening the official notice bypassed the actual family response.");
            Require(!await _bridge.OpenDocumentAsync(Register), "The direct document reader bypassed Naila's permission.");
            var registerRejected = false;
            try { await _bridge.HandleOldPcInputAsync(Input("open", Register)); }
            catch (InvalidOperationException) { registerRejected = true; }
            Require(registerRejected && !Known("clue_marat_case_boundary_marker"), "Direct old-PC opening bypassed register access.");

            await Talk("talk-mansur", "mansur_pc_request");
            await Choose("choice-mansur-marat");
            await CloseTalk();
            Require(Known("clue_family_notice_by_phone") && Known("clue_family_avoids_marat") && Available("house-to-route"),
                "Showing the actual notice did not record Mansur's answer as an alternative family path.");
            var familySources = new[] { Official, Knowledge("clue_family_notice_by_phone") };
            Require(!await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-scope"), new[] { Niva, Official }),
                "Unrelated documents supported a family conclusion.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-erased"), familySources)
                && !Known("clue_notice_call_not_request"), "A false erased-record hypothesis was accepted as evidence.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-family-call-scope"), familySources)
                && Known("clue_notice_call_not_request") && !_bridge.IsOldPcDocumentAccessible(Message),
                "The family comparison failed or unlocked Marat's later message without the central contradiction.");
            await RoundTrip("len01-family-comparison");

            await Move("house-to-route", "village_day", "from_house");
            await Talk("talk-alsu", "alsu_route_context");
            await Choose("choice-alsu-versions");
            await CloseTalk();
            await Move("route-to-fap", "fap_clinic", "waiting_room");
            await Talk("talk-naila", "naila_medical_record");
            Require(!Npc("naila", "record_access_granted") && HasChoice("choice-naila-family-request-entry"),
                "The checked family question was lost on load, or Naila auto-granted access.");
            await Choose("choice-naila-family-request-entry");
            Require(Npc("naila", "record_access_granted") && Known("clue_naila_request_entry_scope")
                && !Known("clue_naila_record_scope") && !Known("clue_marat_case_boundary_marker"),
                "Naila's answer about requests invented her separate response to the internal category.");
            await CloseTalk();

            Require(await _bridge.DispatchInteractionAsync(Interaction("fap-to-document-desk")), "FAP desk did not unlock after a concrete question.");
            Require(await _bridge.DispatchInteractionAsync(Interaction("fap-document-desk-to-official-record")), "FAP notice transition failed.");
            await Move("official-to-internal-register", "house_old_pc", "entry");
            await OpenPc();
            await Search("реестр");
            await ReadFromPc(Register);
            await ClosePc();
            Require(!Known("contradiction_marat_official_vs_internal"), "Opening the register completed the human verification.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { Official, Register }),
                "The first wording comparison failed.");
            var beforeRevisit = _bridge.ActiveSceneId;
            await Revisit("HouseExit");
            await Revisit("RoadToFap");
            Require(_bridge.ActiveSceneId == beforeRevisit && _bridge.CurrentZoneId == "fap_clinic",
                "Returning to Naila rewound the active evidence scene.");
            await Talk("talk-naila", "naila_medical_record");
            await Choose("choice-naila-contradiction");
            await CloseTalk();
            var scopeSources = new[] { Register, Knowledge("clue_naila_record_scope") };
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope-lie"), scopeSources)
                && !Known("contradiction_marat_official_vs_internal"), "A refusal was promoted to proof of a lie.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopeSources)
                && Known("contradiction_marat_official_vs_internal"), "The actual Naila answer could not be checked against the register.");
            await Revisit("OfficialRecordExitToStreet");
            await Revisit("ReturnToHouseRegister");
            Require(_bridge.ActiveSceneId == beforeRevisit && _bridge.CurrentZoneId == "house_old_pc",
                "Returning home rewound the active evidence scene.");

            await Talk("talk-gulsina", "gulsina_yaramyy");
            await Choose("choice-gulsina-naila-word");
            await CloseTalk();
            Require(Known("clue_gulsina_warning_context"), "Gulsina's new contextual answer was not recorded.");
            var warnings = new[] { Knowledge("clue_gulsina_warning_context"), Knowledge("clue_naila_record_scope") };
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-agreement"), warnings)
                && !Known("clue_yaramyy_contexts_distinguished"), "A shared word proved agreement between two people.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-rule"), warnings)
                && !Known("clue_do_not_answer_rule"), "The ordinary word revealed the finale's rule.");
            Require(await _bridge.CompareJournalSourcesAsync(Interaction("compare-warning-contexts"), warnings),
                "The two actual warnings could not be distinguished.");
            await RoundTrip("len01-warning-contexts");
            Require(Known("clue_yaramyy_contexts_distinguished") && !Known("clue_do_not_answer_rule"),
                "Loading the context comparison lost it or revealed the finale.");

            // A second ordinary New Game proves reset and the formerly missing
            // early question without seeding knowledge, inventory or debug state.
            Require(await _bridge.StartNewGameAsync(), "The normal session reset failed.");
            await Frames(6);
            if (_demo.IntroVisible)
            {
                _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
                await Frames(4);
            }
            await Move("arrival-enter-house", "house_old_pc", "entry");
            Require(!Known("clue_household_niva_list") && !Npc("mansur", "household_help_done")
                && !Known("clue_naila_record_scope"), "A new game retained investigation results.");
            await Talk("talk-gulsina", "gulsina_yaramyy");
            await Choose("choice-gulsina-marat");
            await CloseTalk();
            Require(Known("clue_family_avoids_marat") && !Known("clue_marat_official_death_version")
                && !Available("house-to-route"), "The early family question requires the notice or bypasses finding it.");

            GD.Print("act1-len01-family-flow: normal start; unanswered permission; domestic search/wrong source/reread/return; family-source comparison; Naila alternative; preserved-world revisits; warning hypotheses; five save states; clean new game. Technical coverage only.");
            await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("act1-len01-family-flow: " + exception);
            if (GodotObject.IsInstanceValid(_demo)) await GodotSmokeCleanup.ReleaseAsync(_demo);
            GetTree().Quit(1);
        }
    }

    private async Task Talk(string interaction, string dialogue)
    {
        Require(Available(interaction), "Conversation is unavailable: " + interaction);
        _bridge.OpenDialogueUi(Prefix + "dialogue/" + dialogue);
        await Frames(5);
        Require(_dialogue.IsOpen && _player.ModalOpen, "Conversation UI failed to open: " + dialogue);
    }

    private bool HasChoice(string text) => _dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
        .GetChildren().OfType<Button>().Any(button => !button.Disabled && button.Text == _bridge.ResolveText(Prefix + "text/" + text));

    private async Task Choose(string text)
    {
        var choice = _dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().SingleOrDefault(button => !button.Disabled
                && button.Text == _bridge.ResolveText(Prefix + "text/" + text));
        Require(choice is not null, "Missing dialogue choice: " + text);
        choice!.EmitSignal(Button.SignalName.Pressed);
        await Frames(5);
    }

    private async Task CloseTalk()
    {
        _dialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(3);
        Require(!_dialogue.IsOpen && !_player.ModalOpen, "Closing dialogue did not release input.");
    }

    private async Task OpenPc()
    {
        Require(Available("oldpc-power"), "The physical computer is not available.");
        _bridge.OpenOldPcUi();
        await Frames(4);
        Require(_pc.GetNode<Control>("Screen").Visible && _player.ModalOpen, "Archive UI failed to open.");
    }

    private async Task Search(string query)
    {
        var field = _pc.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
        field.Text = query;
        field.EmitSignal(LineEdit.SignalName.TextSubmitted, query);
        await Frames(6);
    }

    private ItemList Results => _pc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
    private int SelectableResults() => Enumerable.Range(0, Results.ItemCount).Count(Results.IsItemSelectable);
    private bool HasDocument(string id) => Enumerable.Range(0, Results.ItemCount)
        .Any(index => Results.IsItemSelectable(index) && Results.GetItemMetadata(index).AsString() == id);

    private async Task ReadFromPc(string id)
    {
        var index = Enumerable.Range(0, Results.ItemCount)
            .FirstOrDefault(index => Results.IsItemSelectable(index) && Results.GetItemMetadata(index).AsString() == id, -1);
        Require(index >= 0, "The archive search cannot open " + id);
        Results.EmitSignal(ItemList.SignalName.ItemSelected, index);
        await Frames(6);
        Require(_pc.ActiveDocumentId == id, "The archive opened the wrong document: " + id);
    }

    private async Task ClosePc()
    {
        _pc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        Require(!_pc.GetNode<Control>("Screen").Visible && !_player.ModalOpen, "Closing the archive did not release input.");
    }

    private async Task Move(string interaction, string zone, string spawn)
    {
        Require(await _bridge.DispatchInteractionAsync(Interaction(interaction)), "Transition rejected: " + interaction);
        _demo.DemoMain.SwitchZone(zone, spawn);
        await Frames(5);
    }

    private async Task Revisit(string targetName)
    {
        var target = _demo.DemoMain.ConnectedWorld?.FindChild(targetName, true, false) as InteractionTarget;
        Require(target?.PresentationRepeatAvailable?.Invoke() == true, "Physical revisit is unavailable: " + targetName);
        target!.PresentationRepeat!.Invoke();
        await Frames(6);
    }

    private async Task RoundTrip(string slot)
    {
        Require(!_player.ModalOpen, "Save scenario accidentally retained a modal.");
        Require(await _bridge.SaveSlotAsync(slot), "Save failed: " + slot);
        Require(await _bridge.LoadSlotAsync(slot), "Load failed: " + slot);
        await Frames(6);
    }

    private bool Available(string id) => _bridge.IsInteractionAvailable(Interaction(id));
    private bool Known(string id) => _bridge.SelectRuntimeState().GetProperty("knowledge")
        .GetProperty(Knowledge(id)).GetProperty("status").GetString() == "confirmed";
    private string? Beat(string id) => _bridge.SelectRuntimeState().GetProperty("beats").GetProperty(Prefix + "beat/" + id).GetString();
    private bool Npc(string id, string key) => _bridge.SelectRuntimeState().GetProperty("npc").TryGetProperty(Prefix + "character/" + id, out var npc)
        && npc.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    private static string Knowledge(string id) => Prefix + "knowledge/" + id;
    private static string Interaction(string id) => Prefix + "interaction/" + id;
    private static JsonElement Input(string type, string documentId) => JsonSerializer.SerializeToElement(new { type, documentId });
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
