using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class ChapterOneFlowSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";

    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Chapter 1 flow could not instantiate the Act 1 demo entrypoint.");
            return;
        }

        AddChild(demo);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Chapter 1 flow could not start through the main menu.");
            return;
        }
        demo._UnhandledInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || bridge.ActiveSceneId != Scene("arrival_vehicle_dusk"))
        {
            Fail("Chapter 1 flow did not start at the authored arrival scene.");
            return;
        }

        var people = main.ConnectedWorld!.GetNode<Node3D>("Act1CoreWorldGreybox/Act1People");
        var rinatActor = people.GetNode<Node3D>("Npc_rinat");
        if (!people.GetNode<Node3D>("Act1NpcPresentation/Npc_alsu").IsVisibleInTree()
            || !people.GetNode<Node3D>("Npc_timur_hazrat").IsVisibleInTree()
            || rinatActor.GlobalPosition.Z < -5f)
        { Fail("Act I exterior people are hidden or Rinat starts at the late position."); return; }

        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Chapter 1 old-PC clue unlocked the house exit before Gulsina's warning dialogue.");
            return;
        }

        if (!bridge.IsInteractionAvailable(Interaction("talk-gulsina"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning")
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Chapter 1 Gulsina warning dialogue did not unlock the house exit through the shared runtime path.");
            return;
        }

        if (!await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (bridge.IsInteractionAvailable(Interaction("route-to-fap"))
            || !bridge.IsInteractionAvailable(Interaction("talk-alsu"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-alsu"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")
            || !bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("Chapter 1 Alsu route dialogue did not unlock the FAP route through the shared runtime path.");
            return;
        }

        if (!await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk"))
            || !bridge.IsInteractionAvailable(Interaction("talk-naila"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("Chapter 1 flow could not apply Naila's authored medical-record dialogue gate.");
            return;
        }

        if (!await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")) return;
        if (!await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")) return;
        if (!await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("The first evidence comparison was rejected."); return; }

        if (!bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category"))
        {
            Fail("Chapter 1 flow could not apply Rinat's authored internal-register dialogue.");
            return;
        }

        if (!await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")) return;
        if (!await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")) return;
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" }))
        { Fail("The language evidence comparison was rejected."); return; }
        if (!await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")) return;
        if (!await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")) return;
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch"))
        { Fail("Could not read the edge sketch."); return; }
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (rinatActor.GlobalPosition.Z > -100f
            || !bridge.IsInteractionAvailable(Interaction("route-to-mosque"))
            || !await bridge.DispatchInteractionAsync(Interaction("route-to-mosque"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("timur_restraint"), "restraint")
            || bridge.RequireDialogue(Dialogue("timur_restraint")).Nodes["restraint"].Choices.Count != 3
            || bridge.ActiveSceneId != Scene("evidence-edge-sketch"))
        { Fail("Timur's physical world conversation lost the investigation scene or Rinat was not pre-staged."); return; }

        var preZiratState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(preZiratState, "clue_marat_last_route_near_zirat") != "hidden")
        {
            Fail("The zirat roadside clue was granted before the player reached the zirat-road scene.");
            return;
        }
        if (!await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road")) return;
        main.SwitchZone("zirat_road", "village_side");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var ziratClue = Interaction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(ziratClue)
            || !await bridge.DispatchInteractionAsync(ziratClue)
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" })
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("The zirat roadside interaction did not grant Marat's route clue through RuntimeBridge.");
            return;
        }

        if (!await Advance(bridge, "zirat-road-to-forest", "forest")) return;
        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var state = bridge.SelectRuntimeState();
        var audioCue = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        if (bridge.CurrentZoneId != "kara_urman_night"
            || KnowledgeStatus(state, "clue_do_not_answer_rule") != "hidden"
            || BeatState(state, "cliffhanger-hard-cut") == "completed"
            || audioCue?.LastPresentedText != "Ринат говорит: «Не отвечай»."
            || audioCue.LastAssetId != "urman.chapter1:asset/audio-rinat-interruption"
            || audioCue.VisibleText != "Голос повторяет детскую фразу с неправильной паузой: «Казанский… не отставай»."
            || audioCue.PresentedHistory.Count < 2
            || !audioCue.PresentedHistory.Contains("Голос повторяет детскую фразу с неправильной паузой: «Казанский… не отставай».")
            || !audioCue.PresentedHistory.Contains("Ринат говорит: «Не отвечай».")
            || audioCue.LastOutcomeKey?.StartsWith("runtime-event:", StringComparison.Ordinal) != true)
        {
            Fail("Chapter 1 flow reached the forest without the final rule, ordered Marat/Rinat cues or equivalent audio presentation.");
            return;
        }

        for (var attempt = 0; attempt < 200 && audioCue.LastStartedAssetId != "urman.chapter1:asset/audio-rinat-interruption"; attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        if (audioCue.VisibleText != "Ринат говорит: «Не отвечай»."
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "confirmed")
        {
            Fail($"Act 1 cliffhanger did not advance from Marat's cue to Rinat's warning (visible='{audioCue.VisibleText}').");
            return;
        }

        // Logical voice refs do not have physical recordings yet. Subtitles
        // must still preserve the authored cue when audio descriptions are
        // disabled, rather than silently dropping the cliffhanger line.
        audioCue.ApplyAccessibilitySettings(new AccessibilitySettingsSnapshot(
            Subtitles: true,
            AudioDescriptions: false));
        audioCue.Present(CompiledCampaignRepository.Load().ResolveAudio(
            "urman.chapter1:asset/audio-rinat-interruption",
            "runtime-test:caption-fallback"));
        if (audioCue.LastPresentedText != "Ринат прерывает Айдара до ответа.")
        {
            Fail("Audio cue did not fall back to the authored caption when audio descriptions were disabled.");
            return;
        }

        // The fallback caption request above intentionally adds a third cue to
        // the presentation queue. Give the queue enough real frames to drain
        // before asserting the closing card; this is not a gameplay timeout.
        for (var frame = 0; frame < 200 && !demo.DemoEnded; frame++)
        {
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        if (!demo.DemoEnded
            || demo.EndingTitleText != "НЕ ОТВЕЧАЙ"
            || demo.EndingCaptionText != "Конец Акта I")
        {
            Fail($"Act 1 demo did not present the НЕ ОТВЕЧАЙ / Конец Акта I fade-to-black after the cliffhanger (ended={demo.DemoEnded}, pending={demo.EndingPending}, delay={demo.EndingDelaySeconds:F3}, presenting={audioCue.IsPresenting}, visible='{audioCue.VisibleText}', history={audioCue.PresentedHistory.Count}).");
            return;
        }

        var ambience = GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        if (ambience?.ActivePlayerIndex != -1 || ambience.CurrentStemId != string.Empty)
        { Fail("The final hard cut left the ambience bed active."); return; }
        var returnButton = demo.FindChild("ReturnToMenu", true, false) as Button;
        if (returnButton is null) { Fail("Ending has no return-to-menu action."); return; }
        returnButton.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!demo.MainMenuVisible || audioCue.IsPresenting)
        { Fail("Ending did not return to a silent main menu."); return; }
        if (!await this.StartThroughMainMenuAsync(demo)
            || demo.DemoEnded || bridge.ActiveSceneId != Scene("arrival_vehicle_dusk"))
        { Fail("A second playthrough retained the completed ending."); return; }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (rinatActor.GlobalPosition.Z < -5f || ambience.ActivePlayerIndex < 0)
        { Fail("New Game did not restore early Rinat staging and ambience."); return; }
        GD.Print("chapter-one-flow-smoke: authored route -> visible people -> final silence -> menu -> fresh session");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Chapter 1 interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Chapter 1 interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        return true;
    }

    private static string KnowledgeStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string BeatState(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("beats").TryGetProperty($"{ChapterPrefix}beat/{localId}", out var beat) ? beat.GetString()! : "not-started";

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
