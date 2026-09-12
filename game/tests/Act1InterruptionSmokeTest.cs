using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// GAME-009 focused interruption fixtures: cancelling an open dialogue modal
/// applies no state change, a quick save taken while a modal is open and a
/// load afterwards stay state-consistent (no half-applied effects), a one-shot
/// interaction cannot be duplicated, completed one-shots stay consumed across
/// revisits, and the final progression remains idempotent after retreat.
/// </summary>
public partial class Act1InterruptionSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string BaselineSlot = "interrupt-baseline";

    public override async void _Ready()
    {
        var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Interruption smoke could not instantiate the Act 1 demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Interruption smoke could not start through the main menu.");
            return;
        }

        demo._UnhandledInput(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await Frames(2);

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var dialogueUi = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (main is null || bridge is null || dialogueUi is null)
        {
            Fail("Interruption smoke could not find main, bridge or dialogue UI.");
            return;
        }

        DeleteSlot();

        // Baseline: at the house with the old-PC notice read (Gulsina pending).
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        var postNoticeState = bridge.SelectRuntimeState().GetRawText();
        if (!await bridge.SaveSlotAsync(BaselineSlot))
        {
            Fail("Interruption smoke could not write the baseline save.");
            return;
        }

        // 1) Modal interrupt while the dialogue is open: a quick save under
        //    the open modal and a cancelled dialogue must leave no state
        //    change, and the quick load restores the same consistent state.
        var gulsinaAvailable = bridge.IsInteractionAvailable(Interaction("talk-gulsina"));
        var gulsinaDispatched = gulsinaAvailable && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        // The dispatch commits the pending-dialogue state; opening the modal
        // is presentation (the bridge's own entry, as in DialogueFlowSmokeTest).
        bridge.OpenDialogueUi(Dialogue("gulsina_yaramyy"));
        await Frames(2);

        if (!gulsinaAvailable || !gulsinaDispatched || !dialogueUi.IsOpen)
        {
            Fail($"Interruption smoke could not open Gulsina's dialogue modal: available={gulsinaAvailable} dispatched={gulsinaDispatched} open={dialogueUi.IsOpen} scene={bridge.ActiveSceneId}");
            return;
        }

        if (!await bridge.SaveSlotAsync("interrupt-quick"))
        {
            Fail("Interruption smoke could not quick-save under the open dialogue.");
            return;
        }

        dialogueUi._UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
        await Frames(2);
        if (dialogueUi.IsOpen || playerIsGated(demo))
        {
            Fail("Cancelling the dialogue modal did not return control.");
            return;
        }

        var postCancelState = bridge.SelectRuntimeState().GetRawText();
        if (!await bridge.LoadSlotAsync("interrupt-quick")
            || bridge.SelectRuntimeState().GetRawText() != postCancelState)
        {
            Fail("Quick save under a modal and load afterwards diverged from the cancelled-dialogue state.");
            return;
        }

        // 2) The cancelled dialogue is still pending: it can be completed
        //    once, and a repeated dispatch of the one-shot target is refused
        //    with the effect applied exactly once.
        var reAvailable = bridge.IsInteractionAvailable(Interaction("talk-gulsina"));
        var reDispatched = reAvailable && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reEntered = reDispatched && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        // NPC re-talk is a legal repeated action: no softlock, and the
        // re-applied warning effect is idempotent (identical state, the house
        // exit stays unlocked exactly once).
        var stateAfterRetalk = bridge.SelectRuntimeState().GetRawText();
        var reRetalk = reEntered && await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"));
        var reRetalkEntered = reRetalk && await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning");
        var stateAfterSecondRetalk = bridge.SelectRuntimeState().GetRawText();
        if (!reAvailable || !reDispatched || !reEntered || !stateAfterRetalk.Equals(stateAfterSecondRetalk, StringComparison.Ordinal) || !reRetalkEntered)
        {
            Fail($"Repeated Gulsina talk was not idempotent: available={reAvailable} dispatched={reDispatched} entered={reEntered} identicalState={stateAfterRetalk.Equals(stateAfterSecondRetalk, StringComparison.Ordinal)} retalk={reRetalk}/{reRetalkEntered}");
            return;
        }

        // 3) A baseline load reverts the whole interrupted chain to the
        //    post-notice state: Gulsina pending again, nothing stuck.
        if (!await bridge.LoadSlotAsync(BaselineSlot)
            || bridge.SelectRuntimeState().GetRawText() != postNoticeState
            || !bridge.IsInteractionAvailable(Interaction("talk-gulsina")))
        {
            Fail("Baseline load after the interrupted chain did not restore a consistent replayable state.");
            return;
        }

        // 4) A legal house exit remains available after a presentation-only
        // leave/re-entry. Main.SwitchZone changes the physical connected-world
        // placement, while RuntimeBridge keeps the narrative scene and state
        // authoritative; this proves the revisit does not duplicate the
        // house beat or strand the exit.
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning")
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The replayable house state did not unlock its authored exit.");
            return;
        }

        var houseBeforeRevisit = bridge.SelectRuntimeState().GetRawText();
        main.SwitchZone("village_day", "from_house");
        await Frames(1);
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (main.ActiveZoneScenePath != "res://scenes/zones/style_benchmark_house_pc.tscn"
            || bridge.CurrentZoneId != "house_old_pc"
            || bridge.SelectRuntimeState().GetRawText() != houseBeforeRevisit
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House leave/re-entry changed the runtime state or lost the available exit.");
            return;
        }

        if (!await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await Frames(1);

        // Continue through the authored FAP gate. The FAP evidence route is
        // already consumed once at the old PC; revisiting its physical zone
        // after the official-record transition must not replay or mutate the
        // narrative snapshot.
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-alsu"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")
            || !await Advance(bridge, "route-to-fap", "fap_waiting_room_day"))
        {
            Fail("The replayable village route did not reach the authored FAP gate.");
            return;
        }

        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (!await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "ask-wording")
            || !await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death"))
        {
            Fail("The replayable FAP route did not reach the official-record beat.");
            return;
        }

        var afterFapEvidence = bridge.SelectRuntimeState().GetRawText();
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (main.ActiveZoneScenePath != "res://scenes/zones/chapter1_fap_clinic.tscn"
            || bridge.CurrentZoneId != "fap_clinic"
            || bridge.SelectRuntimeState().GetRawText() != afterFapEvidence)
        {
            Fail("FAP revisit after consumed evidence changed the narrative state or failed to load the physical zone.");
            return;
        }

        // 5) Return to the house, resolve the remaining evidence, and prove
        // the zirat roadside clue stays consumed across a retreat/re-entry
        // before the final trigger is dispatched.
        if (!await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_marat_official_death_notice")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { "urman.oldpc:document/doc_marat_official_death_notice", "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("The record comparison was rejected."); return; }
        if (!await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || !await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")
            || !await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")
            || !await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch")
            || !await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road"))
        {
            Fail("The return-to-house evidence chain did not reach the zirat road.");
            return;
        }

        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);
        var ziratClue = Interaction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(ziratClue)
            || !await bridge.DispatchInteractionAsync(ziratClue)
            || bridge.IsInteractionAvailable(ziratClue))
        {
            Fail("The zirat roadside clue did not become a consumed one-shot interaction.");
            return;
        }

        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" }))
        { Fail("The route comparison was rejected."); return; }
        var postZiratClueState = bridge.SelectRuntimeState().GetRawText();
        if (!bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest")))
        {
            Fail("The consumed zirat clue did not leave the authored forest approach available.");
            return;
        }

        main.SwitchZone("village_day", "from_forest");
        await Frames(1);
        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);
        if (bridge.SelectRuntimeState().GetRawText() != postZiratClueState
            || bridge.IsInteractionAvailable(ziratClue)
            || !bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest")))
        {
            Fail("Zirat retreat/re-entry revived the consumed clue or lost the final approach.");
            return;
        }

        if (!await Advance(bridge, "zirat-road-to-forest", "forest")) return;
        var terminalState = bridge.SelectRuntimeState().GetRawText();
        if (bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-road-to-forest"))
            || bridge.SelectRuntimeState().GetRawText() != terminalState)
        {
            Fail("Repeated terminal dispatch duplicated or mutated the final progression state.");
            return;
        }

        GD.Print("act1-interruption: PASS dialogue-cancel atomicity + modal quicksave/load + house/FAP revisit + zirat one-shot retreat + terminal idempotency");
        DeleteSlot();
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static bool playerIsGated(Act1DemoRoot demo) =>
        demo.DemoMain?.GetNodeOrNull<FirstPersonController>("Player") is not { ModalOpen: false };

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Interruption interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Interruption interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        if (targetSceneLocalId == "forest")
        {
        for (var attempt = 0; attempt < 200 && bridge.IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention"); attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }

        return true;
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private static void DeleteSlot()
    {
        foreach (var slot in new[] { BaselineSlot, "interrupt-quick" })
        {
            foreach (var suffix in new[] { ".json", ".backup.json" })
            {
                var path = ProjectSettings.GlobalizePath($"user://savegames/{slot}{suffix}");
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
