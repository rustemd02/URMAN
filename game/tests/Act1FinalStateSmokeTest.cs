using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// STATE-005 focused final-state fixtures: the terminal knowledge
/// `clue_do_not_answer_rule` cannot be confirmed before Rinat's forest-edge
/// intervention, a pre-forest save restores with no final knowledge, the
/// ending completes exactly once after restore, and a post-terminal repeat
/// dispatch cannot duplicate the terminal beat.
/// </summary>
public partial class Act1FinalStateSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string MatrixSlot = "final-state-matrix";
    private const string MaratCue = ChapterPrefix + "asset/audio-marat-voice";
    private const string RinatCue = ChapterPrefix + "asset/audio-rinat-interruption";

    private Act1DemoRoot? _demo;
    private bool _finished;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            await RunAsync();
            if (!_finished) throw new InvalidOperationException("The authored state setup stopped before its final assertion.");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-final-state: FAIL " + error); }
        finally
        {
            try { if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo); }
            catch (Exception cleanupError)
            {
                exit = 1;
                GD.PrintErr("act1-final-state: cleanup failed: " + cleanupError);
            }
            GetTree().Quit(exit);
        }
    }

    private async Task RunAsync()
    {
        var demo = _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn")?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Final-state smoke could not instantiate the Act 1 demo entrypoint.");
            return;
        }

        AddChild(demo);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Final-state smoke could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            Fail("Final-state smoke could not find the runtime bridge.");
            return;
        }

        DeleteSlot();

        demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
        await Frames(2);
        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);

        // Authored chain to the zirat road, mirroring the campaign gates.
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = OfficialNotice
        }));
        if (!bridge.IsInteractionAvailable(Interaction("talk-gulsina"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-gulsina"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("gulsina_yaramyy"), "home-warning")
            || !await bridge.ChooseDialogueAsync(Dialogue("gulsina_yaramyy"), "home-warning", "ask-marat")
            || !await Act1FamilyMealProof.CompleteAsync(this, bridge)
            || !await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("talk-alsu"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-alsu"))
            || !await Act1AlsuWalkProof.CompleteAsync(this, bridge)
            || !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")
            || !await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-versions")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"),
                new[] { OfficialNotice, ChapterPrefix + "knowledge/clue_alsu_heard_versions" })
            || !await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(1);
        if (!bridge.IsInteractionAvailable(Interaction("talk-naila"))
            || !await bridge.DispatchInteractionAsync(Interaction("talk-naila"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "official-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "ask-wording")
            || !await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")
            || !await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")
            || !await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await Frames(1);
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_marat_official_death_notice")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict"))
        { Fail("The record comparison sources could not be read."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { "urman.oldpc:document/doc_marat_official_death_notice", "urman.oldpc:document/rec_marat_case_register_conflict" })
            || !await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "follow-up")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "follow-up", "press-contradiction")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "record-question", "show-external-wording")
            || !await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "matching-formulation", "ask-category-scope")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.chapter1:knowledge/clue_naila_record_scope" }))
        { Fail("The record comparison was rejected."); return; }
        if (!bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || !await bridge.ChooseDialogueAsync(Dialogue("rinat_internal_register"), "dangerous-category", "present-category")
            || !await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await Act1StateFlowProof.MessageReturnAsync(this, bridge)
            || !await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.oldpc:document/tw_shurale_urman_boundary" })
            || !await Act1SourceReturnsProof.CompleteAsync(this, bridge)
            || !await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch")
            || !await Act1StateFlowProof.RouteDiscussionAsync(this, bridge)
            || !await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road")) return;
        main.SwitchZone("zirat_road", "village_side");
        await Frames(1);
        if (!await Act1RinatRoadsideProof.ObserveAsync(this, bridge)) return;

        // 1) Before the roadside clue the forest approach is locked and an
        //    out-of-order dispatch is rejected without state change.
        var postRinatState = bridge.SelectRuntimeState();
        if (bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-road-to-forest"))
            || bridge.SelectRuntimeState().GetRawText() != postRinatState.GetRawText()
            || FinalKnowledge(postRinatState) != "hidden"
            || BeatIsCompleted(postRinatState, "cliffhanger-hard-cut"))
        {
            Fail("The forest approach was not gated by the zirat roadside clue.");
            return;
        }

        if (!bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || !await bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue")))
        {
            Fail("The zirat roadside clue could not be confirmed before the field observation.");
            return;
        }
        await Act1RouteLandmarksProof.ObserveAsync(this, bridge);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" })
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden"
            || bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || await bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue")))
        {
            Fail("The zirat roadside clue did not confirm once without granting final knowledge.");
            return;
        }

        // 2) Pre-forest save: no final knowledge, cliffhanger not completed.
        if (!await bridge.SaveSlotAsync(MatrixSlot))
        {
            Fail("Final-state smoke could not write the pre-forest snapshot.");
            return;
        }

        // 3) Reach the forest approach, then cross its existing endpoint.
        if (!bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
            || !await Advance(bridge, "zirat-road-to-forest", "forest-approach")
            || !await Act1StateFlowProof.EnterForestAsync(this, bridge)
            || !await WaitForIntervention(bridge, failedLoadProbe: true)
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed"
            || !BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut"))
        {
            Fail("The forest approach endpoint did not complete the single terminal beat.");
            return;
        }

        // 4) Restore the pre-forest save: the final knowledge is gone again
        //    (the save predates the reveal; nothing leaks through the load).
        if (!await bridge.LoadSlotAsync(MatrixSlot)
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden"
            || BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut")
            || bridge.ActiveSceneId != Scene("zirat-road"))
        {
            Fail("Loading the pre-forest save did not restore the pre-reveal state exactly.");
            return;
        }

        // 5) Complete the ending again after the restore: exactly once.
        if (!await Advance(bridge, "zirat-road-to-forest", "forest-approach")
            || !await Act1StateFlowProof.EnterForestAsync(this, bridge)
            || !await WaitForIntervention(bridge, failedLoadProbe: false)
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed"
            || !BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut"))
        {
            Fail("The restored session could not complete the terminal beat once more.");
            return;
        }

        // 6) Post-terminal repeat: the approach endpoint is scene-locked and
        //    cannot duplicate the terminal beat or mutate the state.
        var terminalState = bridge.SelectRuntimeState().GetRawText();
        if (bridge.IsInteractionAvailable(Interaction("forest-approach-to-forest"))
            || await bridge.DispatchInteractionAsync(Interaction("forest-approach-to-forest"))
            || bridge.SelectRuntimeState().GetRawText() != terminalState)
        {
            Fail("A post-terminal forest dispatch mutated the terminal state.");
            return;
        }

        if (!await bridge.SaveSlotAsync(MatrixSlot) || !await bridge.LoadSlotAsync(MatrixSlot)
            || (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.IsPresenting == true
            || FinalKnowledge(bridge.SelectRuntimeState()) != "confirmed")
        { Fail("Completed save replayed the finale or lost its result."); return; }

        GD.Print("act1-final-state: PASS gated approach + pre-reveal save/load + late failed-load recovery + menu cancellation/Continue + single terminal beat + idempotent post-terminal");
        DeleteSlot();
        _finished = true;
    }

    private async Task<bool> WaitForIntervention(RuntimeBridge bridge, bool failedLoadProbe)
    {
        Act1StateFlowProof.VerifyLegacyBoundaryCharmsHidden(this);
        if (FinalKnowledge(bridge.SelectRuntimeState()) != "hidden")
        { Fail("Forest entry revealed the rule before Rinat."); return false; }
        var pending = bridge.SelectRuntimeState().GetRawText();
        if (await bridge.DispatchInteractionAsync(Interaction("forest-rinat-intervention"))
            || bridge.SelectRuntimeState().GetRawText() != pending)
        { Fail("A direct pending intervention bypassed Rinat's physical/audio presentation."); return false; }
        // Keep the real endpoint position while saving. Moving back to the
        // zone entry here would replace the attained encounter with a fixture.
        if (!await bridge.SaveSlotAsync(RuntimeBridge.CheckpointSlot)
            || !await bridge.LoadSlotAsync(RuntimeBridge.CheckpointSlot)) return false;
        await Frames(2);
        var cue = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi
            ?? throw new InvalidOperationException("The pending finale has no audio/caption owner.");
        if (cue.LastStartedAssetId != MaratCue
            || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden")
        { Fail("Loading mid-finale lost the ordered replay or revealed the rule."); return false; }
        return failedLoadProbe
            ? await FailedLoadDuringInterventionAsync(bridge, cue)
            : await MainMenuDuringInterventionAsync(bridge, cue);
    }

    private async Task<bool> FailedLoadDuringInterventionAsync(RuntimeBridge bridge, AudioCueUi cue)
    {
        await WaitForRinatStepAsync(bridge, cue);
        var before = bridge.SelectRuntimeState().GetRawText();
        var session = bridge.SessionIdentity;
        // This slot is never created and cannot name a user's existing save.
        var missingSlot = "final-state-missing-" + Guid.NewGuid().ToString("N");
        var replayed = new List<string>();
        void RecordCue(string id) { if (id is MaratCue or RinatCue) replayed.Add(id); }
        cue.CueStarted += RecordCue;
        try
        {
            if (bridge.IsSlotAvailable(missingSlot) || await bridge.LoadSlotAsync(missingSlot)
                || bridge.ActiveSceneId != Scene("forest")
                || !ReferenceEquals(session, bridge.SessionIdentity)
                || bridge.SelectRuntimeState().GetRawText() != before)
            { Fail("A missing test slot changed the attained pending finale or replaced its session."); return false; }
            await WaitForRinatStepAsync(bridge, cue);
            if (!replayed.SequenceEqual(new[] { MaratCue, RinatCue }))
            { Fail("The failed late load did not replay both cues in order before Rinat's new physical step."); return false; }
            if (!await VerifyBehindObserverAsync(bridge, cue)) return false;
            GD.Print("act1-final-state: PASS missing test-slot load during the real Rinat step; same forest state, ordered cue/step recovery and completion");
            return true;
        }
        finally { cue.CueStarted -= RecordCue; }
    }

    private async Task<bool> MainMenuDuringInterventionAsync(RuntimeBridge bridge, AudioCueUi cue)
    {
        var demo = _demo ?? throw new InvalidOperationException("The menu interruption has no demo owner.");
        var pause = GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi
            ?? throw new InvalidOperationException("The menu interruption has no pause shell.");
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("The menu interruption has no player.");
        if (!await bridge.SaveSlotAsync(MainMenuUi.ContinueSlot)) return false;
        await WaitForRinatStepAsync(bridge, cue);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        await Frames(2);
        if (!pause.IsOpen || !cue.IsPaused || !player.ModalOpen || pause.MainMenuButton is not { } menuButton)
        { Fail("Real Escape did not pause the pending Rinat presentation."); return false; }
        menuButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!pause.IsOpen || demo.MainMenuVisible || !menuButton.Text.EndsWith("точно?", StringComparison.Ordinal))
        { Fail("The first main-menu press bypassed its confirmation."); return false; }
        var pending = bridge.SelectRuntimeState().GetRawText();
        var checkpoint = SlotFileFingerprint(RuntimeBridge.CheckpointSlot);
        var quick = SlotFileFingerprint(MainMenuUi.ContinueSlot);
        menuButton.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (!demo.MainMenuVisible || pause.IsOpen || !player.ModalOpen || cue.IsPresenting)
        { Fail("The confirmed main menu did not stop the pending presentation and gate gameplay."); return false; }
        var until = Time.GetTicksMsec() + 3000;
        do
        {
            if (!demo.MainMenuVisible || demo.DemoEnded || demo.EndingPending || cue.IsPresenting
                || bridge.ActiveSceneId != Scene("forest")
                || bridge.SelectRuntimeState().GetRawText() != pending)
            { Fail("The cancelled Rinat task advanced knowledge or the ending behind the main menu."); return false; }
            await Frames(1);
        } while (Time.GetTicksMsec() < until);
        if (SlotFileFingerprint(RuntimeBridge.CheckpointSlot) != checkpoint
            || SlotFileFingerprint(MainMenuUi.ContinueSlot) != quick)
        { Fail("The cancelled Rinat task changed a save behind the main menu."); return false; }
        var candidate = await bridge.FindContinueAsync();
        if (candidate?.Slot != MainMenuUi.ContinueSlot
            || demo.MainMenu?.ContinueButton is not { Visible: true, Disabled: false } continueButton)
        { Fail("Normal Continue did not offer the prepared pending quick save."); return false; }
        var replayed = new List<string>();
        void RecordCue(string id) { if (id is MaratCue or RinatCue) replayed.Add(id); }
        cue.CueStarted += RecordCue;
        try
        {
            continueButton.EmitSignal(BaseButton.SignalName.Pressed);
            until = Time.GetTicksMsec() + 10000;
            while (demo.MainMenuVisible && Time.GetTicksMsec() < until) await Frames(1);
            if (demo.MainMenuVisible || demo.IntroVisible || player.ModalOpen
                || bridge.IsDebugSession || bridge.ActiveSceneId != Scene("forest")
                || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden")
            { Fail("Normal Continue did not restore the pending physical forest scene."); return false; }
            await VerifyCloseObserverAsync(bridge, cue);
            if (!replayed.SequenceEqual(new[] { MaratCue, RinatCue }))
            { Fail("Continue did not replay the ordered voices before Rinat's new physical step."); return false; }
            if (!await Act1StateFlowProof.WaitForTerminalAsync(this, bridge)) return false;
            GD.Print("act1-final-state: PASS real Pause/main-menu confirmation; state and both save files unchanged for 3 seconds; normal Continue replayed and completed the pending finale");
            return true;
        }
        finally { cue.CueStarted -= RecordCue; }
    }

    private async Task WaitForRinatStepAsync(RuntimeBridge bridge, AudioCueUi cue)
    {
        var actor = _demo?.DemoMain.FindChild("Npc_rinat", true, false) as Node3D
            ?? throw new InvalidOperationException("The late interruption has no physical Rinat actor.");
        Vector3? stepStart = null;
        var deadline = Time.GetTicksMsec() + 15000;
        while (Time.GetTicksMsec() < deadline)
        {
            if (bridge.ActiveSceneId != Scene("forest") || FinalKnowledge(bridge.SelectRuntimeState()) != "hidden"
                || BeatIsCompleted(bridge.SelectRuntimeState(), "cliffhanger-hard-cut"))
                Fail("The late interruption missed the pending finale.");
            if (cue.LastStartedAssetId == RinatCue && cue.IsPresenting
                && actor.GetMeta("rinatInterventionPhase", "pending").AsString() == "physical-step-and-stop-hand")
            {
                stepStart ??= actor.GlobalPosition;
                if (stepStart.Value.DistanceTo(actor.GlobalPosition) > .002f) return;
            }
            else stepStart = null;
            await Frames(1);
        }
        Fail($"Rinat's cue and actual in-progress step were not reached: cue={cue.LastStartedAssetId}, phase={actor.GetMeta("rinatInterventionPhase", "pending")}.");
    }

    private async Task<bool> VerifyBehindObserverAsync(RuntimeBridge bridge, AudioCueUi cue)
    {
        var player = FinalPlayer();
        var actor = FinalActor();
        var presence = RinatPresencePresentation.Current(GetTree())
            ?? throw new InvalidOperationException("The behind-view fixture has no presentation owner.");
        await WaitForUnseenLandingAsync(bridge, cue, actor, captureFeet: false);
        var before = actor.GlobalPosition;
        var back = -Flat(actor.GlobalBasis.Z).Normalized();
        await PlaceLocalObserverAsync(player, new[] { 1.4f, 1.7f, 2f }.SelectMany(distance =>
            new[] { 0f, -.35f, .35f }.Select(yaw => before + back.Rotated(Vector3.Up, yaw) * distance)), "behind-view");
        if (!Act1RinatRoadsideProof.LookAtIntervention(this)) return false;
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var dot = ObserverDot(actor, player);
        AssertPendingFinale(bridge);
        if (dot >= -.8f || presence.CanSeeInterventionGesture()
            || !camera.IsPositionInFrustum(actor.GlobalPosition + Vector3.Up * 1.55f)
            || !camera.IsPositionInFrustum(actor.GetNode<Node3D>("RinatHandLamp").GlobalPosition))
            Fail($"The grounded behind-view fixture did not reject the framed actor from his back: dot={dot:0.000}.");
        GD.Print($"state-final-local-fixture: behind-view rejected; player={player.GlobalPosition} actor={actor.GlobalPosition} dot={dot:0.000}; ground/CanStand validated, no traversal claim");
        await CaptureLocalFixtureAsync(actor, "behind-view-negative");
        if (!await Act1StateFlowProof.WaitForTerminalAsync(this, bridge)) return false;
        if (Flat(actor.GlobalPosition - before).Length() < .14f || ObserverDot(actor, player) <= .9f
            || !presence.CanSeeInterventionGesture())
            Fail("Rinat did not perform a new physical turn/step and show the complete gesture after the behind-view rejection.");
        if (DisplayServer.GetName() != "headless")
        {
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "behind-observer-landed");
            var turnedFeet = ReadRenderedFootOrdering(actor, "behind-observer-landed");
            if (turnedFeet.CenterDistance <= .05f)
                Fail($"Rinat faced the observer with reversed or collapsed rendered foot ordering: {turnedFeet.CenterDistance:0.0000}m.");
        }
        GD.Print("state-final-local-fixture: behind-view recovered through the real grounded step and full live gesture; one terminal effect");
        return true;
    }

    private async Task VerifyCloseObserverAsync(RuntimeBridge bridge, AudioCueUi cue)
    {
        var player = FinalPlayer();
        var actor = FinalActor();
        AssertPendingFinale(bridge);
        if (cue.LastStartedAssetId != MaratCue)
            Fail("The close-view fixture missed Marat's restored cue before Rinat's step.");
        var start = actor.GlobalPosition;
        var forward = Flat(actor.GlobalBasis.Z).Normalized();
        await PlaceLocalObserverAsync(player, new[] { .9f, .95f, .85f }
            .Select(distance => start + forward * distance), "close-observer");
        var observer = player.GlobalPosition;
        var initialDirection = Flat(observer - start).Normalized();
        var distanceToActor = Flat(observer - start).Length();
        if (cue.LastStartedAssetId != MaratCue || actor.GlobalPosition.DistanceTo(start) > .002f
            || distanceToActor < .85f || distanceToActor > 1.0f)
            Fail($"Close-view placement did not precede the live step at 0.85–1.0m: distance={distanceToActor:0.000}, cue={cue.LastStartedAssetId}.");
        float? initialFootWidth = null;
        if (DisplayServer.GetName() != "headless")
        {
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "close-before-step");
            initialFootWidth = ReadRenderedFootOrdering(actor, "close-before-step").CenterDistance;
        }
        await WaitForUnseenLandingAsync(bridge, cue, actor, captureFeet: true, initialFootWidth: initialFootWidth);
        var displacement = Flat(actor.GlobalPosition - start);
        var tangentError = Mathf.Abs(displacement.Normalized().Dot(initialDirection));
        if (displacement.Length() < .14f || displacement.Length() > .18f || tangentError > .08f
            || ObserverDot(actor, player) <= .9f || Flat(player.GlobalPosition - observer).Length() > .01f)
            Fail($"Close observer caused extra steps or the wrong final facing: moved={displacement.Length():0.000}m tangentError={tangentError:0.000} frontDot={ObserverDot(actor, player):0.000}.");
        await CaptureLocalFixtureAsync(actor, "close-observer-feet-landed");
        if (initialFootWidth is { } expectedWidth)
        {
            await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "close-landed");
            var landedFeet = ReadRenderedFootOrdering(actor, "close-landed");
            if (Mathf.Abs(landedFeet.CenterDistance - expectedWidth) > .025f || landedFeet.ClearGap < .02f)
                Fail($"The lateral landing crossed or spread the rendered boots: initial={expectedWidth:0.0000}m landed={landedFeet.CenterDistance:0.0000}m clearGap={landedFeet.ClearGap:0.0000}m.");
        }
        else GD.Print("state-final-rendered-stance: external/not-run in headless mode");
        GD.Print($"state-final-local-fixture: close observer one tangent step={displacement.Length():0.000}m frontDot={ObserverDot(actor, player):0.000}; actor and quest state never assigned by fixture");

        // Make room for the ordinary whole-actor look through real mapped input.
        // Looking down keeps the final visual condition pending during this walk.
        var landed = actor.GlobalPosition;
        var walkStart = player.GlobalPosition;
        LookAtFeet(player, actor, straightDown: true);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.S, PhysicalKeycode = Key.S, Pressed = true });
        try
        {
            for (var frame = 0; frame < 90 && Flat(player.GlobalPosition - walkStart).Length() < .60f; frame++)
            {
                AssertPendingFinale(bridge);
                if (player.ModalOpen) Fail("The pending close encounter blocked ordinary backward input.");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.S, PhysicalKeycode = Key.S, Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        AssertPendingFinale(bridge);
        var walked = Flat(player.GlobalPosition - walkStart).Length();
        if (walked < .55f || walked > .85f || !player.IsOnFloor() || player.IsCrouching
            || !player.CanStandAt(player.GlobalPosition) || actor.GlobalPosition.DistanceTo(landed) > .01f)
            Fail($"The close-view fixture could not make room by an ordinary grounded backward walk: {walked:0.000}m.");
        GD.Print($"state-final-local-fixture: mapped S moved player backward {walked:0.000}m; final knowledge remains hidden until the ordinary full view");
    }

    private async Task WaitForUnseenLandingAsync(RuntimeBridge bridge, AudioCueUi cue, Node3D actor,
        bool captureFeet, float? initialFootWidth = null)
    {
        var player = FinalPlayer();
        var deadline = Time.GetTicksMsec() + 15000;
        ulong movingSince = 0;
        ulong lastStanceCheck = 0;
        var captured = false;
        while (Time.GetTicksMsec() < deadline)
        {
            LookAtFeet(player, actor, straightDown: !captureFeet);
            AssertPendingFinale(bridge);
            if (RinatPresencePresentation.Current(GetTree())?.CanSeeInterventionGesture() == true)
                Fail("The deliberately incomplete local view unexpectedly exposed the full gesture.");
            var phase = actor.GetMeta("rinatInterventionPhase", "pending").AsString();
            if (cue.LastStartedAssetId == RinatCue && phase == "step-landed-stop-hand") return;
            if (phase == "physical-step-and-stop-hand")
            {
                if (movingSince == 0) movingSince = Time.GetTicksMsec();
                if (initialFootWidth is { } expectedWidth && Time.GetTicksMsec() - lastStanceCheck >= 120)
                {
                    await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "close-moving");
                    var feet = ReadRenderedFootOrdering(actor, "close-moving");
                    if (feet.CenterDistance < expectedWidth * .7f || feet.ClearGap < .02f)
                        Fail($"The lateral step crossed the rendered boots: signed distance={feet.CenterDistance:0.0000}m clearGap={feet.ClearGap:0.0000}m.");
                    lastStanceCheck = Time.GetTicksMsec();
                }
                if (captureFeet && !captured && Time.GetTicksMsec() - movingSince >= 300)
                {
                    await CaptureLocalFixtureAsync(actor, "close-observer-feet-moving");
                    captured = true;
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Fail("Rinat's real step did not land while the player kept the full gesture outside the view.");
    }

    private static (float CenterDistance, float ClearGap) ReadRenderedFootOrdering(Node3D actor, string label)
    {
        var started = Time.GetTicksMsec();
        GD.Print($"state-final-rendered-stance-begin: label={label} tick={started}");
        var axis = Flat(actor.GlobalBasis.X).Normalized();
        var bounds = new List<(Vector3 Center, float Min, float Max)>();
        foreach (var side in new[] { "Left", "Right" })
        {
            var boot = actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                .Single(mesh => mesh.IsVisibleInTree() && mesh.Name.ToString() == $"CouncilWitness_Boot{side}_LOD0");
            using var baked = boot.BakeMeshFromCurrentSkeletonPose();
            var vertices = Enumerable.Range(0, baked.GetSurfaceCount()).SelectMany(surface =>
                baked.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => boot.GlobalTransform * vertex).ToArray();
            if (vertices.Length == 0) Fail("The rendered stance has an empty boot: " + side);
            var center = vertices.Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex) / vertices.Length;
            bounds.Add((center, vertices.Min(vertex => vertex.Dot(axis)), vertices.Max(vertex => vertex.Dot(axis))));
        }
        var distance = (bounds[1].Center - bounds[0].Center).Dot(axis);
        var clearGap = bounds[1].Min - bounds[0].Max;
        if (!float.IsFinite(distance) || !float.IsFinite(clearGap)) Fail("The rendered stance contains non-finite coordinates.");
        GD.Print($"state-final-rendered-stance: label={label} left={bounds[0].Center} right={bounds[1].Center} axis={axis} signedCenterDistance={distance:0.0000}m clearGap={clearGap:0.0000}m milliseconds={Time.GetTicksMsec() - started}");
        return (distance, clearGap);
    }

    private async Task PlaceLocalObserverAsync(FirstPersonController player, IEnumerable<Vector3> candidates, string label)
    {
        foreach (var at in candidates)
        {
            using var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 2f, at - Vector3.Up * 3f,
                1u, new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0 || hit["normal"].AsVector3().Y <= .85f) continue;
            var feet = hit["position"].AsVector3() + Vector3.Up * .02f;
            if (!player.CanStandAt(feet)) continue;
            var support = (hit["collider"].AsGodotObject() as Node)?.GetPath();
            // Explicit local reproduction fixture, never a whole-route walk.
            player.ApplyPortableTransform(new PlayerTransform(new(feet.X, feet.Y, feet.Z),
                new(-82f, player.RotationDegrees.Y, 0f)));
            for (var frame = 0; frame < 18; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (frame >= 3 && player.IsOnFloor() && !player.IsCrouching) break;
            }
            if (!player.IsOnFloor() || player.IsCrouching || player.ModalOpen
                || !player.CanStandAt(player.GlobalPosition) || Flat(player.GlobalPosition - feet).Length() > .03f)
                Fail($"The local {label} fixture did not settle on its tested standing support: {support}.");
            GD.Print($"state-final-local-fixture: {label} grounded placement feet={player.GlobalPosition} support={support}; no actor/progress mutation, no traversal claim");
            return;
        }
        Fail("No real ground and standing clearance for local fixture: " + label);
    }

    private async Task CaptureLocalFixtureAsync(Node3D actor, string label)
    {
        var directory = OS.GetEnvironment("URMAN_FINAL_CAPTURE_DIR");
        if (DisplayServer.GetName() == "headless" || string.IsNullOrWhiteSpace(directory)) return;
        await Act1StateFlowProof.CaptureRenderedStepAsync(this, actor, directory,
            $"rinat_local_{label}_{Time.GetTicksMsec()}");
    }

    private FirstPersonController FinalPlayer() => GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
        ?? throw new InvalidOperationException("The local finale fixture has no player.");
    private Node3D FinalActor() => _demo?.DemoMain.FindChild("Npc_rinat", true, false) as Node3D
        ?? throw new InvalidOperationException("The local finale fixture has no Rinat actor.");
    private static Vector3 Flat(Vector3 value) => new(value.X, 0f, value.Z);
    private static float ObserverDot(Node3D actor, FirstPersonController player) =>
        Flat(actor.GlobalBasis.Z).Normalized().Dot(Flat(player.GlobalPosition - actor.GlobalPosition).Normalized());
    private static void LookAtFeet(FirstPersonController player, Node3D actor, bool straightDown)
    {
        var towards = actor.GlobalPosition + Vector3.Up * .08f - player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition;
        player.ApplySmokeLook(straightDown ? -82f : Mathf.RadToDeg(Mathf.Atan2(towards.Y, Flat(towards).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-towards.X, -towards.Z)));
    }
    private static void AssertPendingFinale(RuntimeBridge bridge)
    {
        var state = bridge.SelectRuntimeState();
        if (bridge.ActiveSceneId != Scene("forest") || FinalKnowledge(state) != "hidden"
            || BeatIsCompleted(state, "cliffhanger-hard-cut"))
            Fail("The local observer fixture leaked the terminal rule before the complete live gesture.");
    }

    private static string SlotFileFingerprint(string slot) => string.Join(":", new[]
        { ".savegame-v3.json", ".savegame-v3.backup.json" }.Select(suffix =>
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{slot}{suffix}");
            return System.IO.File.Exists(path)
                ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(path)))
                : "absent";
        }));

    private async Task<bool> Advance(RuntimeBridge bridge, string interactionLocalId, string targetSceneLocalId)
    {
        var interactionId = Interaction(interactionLocalId);
        if (!bridge.IsInteractionAvailable(interactionId))
        {
            Fail($"Final-state interaction was not available: {interactionId}.");
            return false;
        }

        if (!await bridge.DispatchInteractionAsync(interactionId)
            || bridge.ActiveSceneId != Scene(targetSceneLocalId))
        {
            Fail($"Final-state interaction did not reach {targetSceneLocalId}: {interactionId}.");
            return false;
        }

        return true;
    }

    private static string FinalKnowledge(JsonElement state) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/clue_do_not_answer_rule").GetProperty("status").GetString()!;

    private static bool BeatIsCompleted(JsonElement state, string localId) =>
        state.TryGetProperty("beats", out var beats)
        && beats.TryGetProperty($"{ChapterPrefix}beat/{localId}", out var beat)
        && beat.GetString() == "completed";

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static string Scene(string localId) => $"{ChapterPrefix}scene/{localId}";

    private static string Dialogue(string localId) => $"{ChapterPrefix}dialogue/{localId}";

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void DeleteSlot()
    {
        foreach (var suffix in new[] { ".savegame-v3.json", ".savegame-v3.backup.json" })
        {
            var path = ProjectSettings.GlobalizePath($"user://savegames/{MatrixSlot}{suffix}");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private static void Fail(string message) => throw new InvalidOperationException(message);
}

// Shared only by the three state regression scenes. These are named semantic
// setup actions, not a traversal or a timing proof; RuntimeBridge still owns
// every choice, comparison, observation and final presentation condition.
internal static class Act1StateFlowProof
{
    private const string Prefix = "urman.chapter1:";
    private const string RinatCue = Prefix + "asset/audio-rinat-interruption";

    internal static async Task<bool> MessageReturnAsync(Node owner, RuntimeBridge bridge)
    {
        var main = Main(owner);
        var scene = bridge.ActiveSceneId;
        Check(scene == Prefix + "scene/evidence-saved-message", "Message reply setup is outside its authored scene.");
        await Act1SourceExcerptProof.RecordMessageVoiceAsync(owner, bridge);
        Check(bridge.SelectRuntimeState().GetProperty("knowledge")
                .GetProperty(Prefix + "knowledge/clue_marat_was_afraid_before_death")
                .GetProperty("status").GetString() == "hidden",
            "Recording the message excerpt revealed Marat's fear before Alsu's return conversation.");
        main.SwitchZone("village_day", "from_house");
        await Frames(owner, 2);
        Check(bridge.IsInteractionAvailable(Prefix + "interaction/talk-alsu")
            && await bridge.EnterDialogueNodeAsync(Prefix + "dialogue/alsu_route_context", "message-return")
            && await bridge.ChooseDialogueAsync(Prefix + "dialogue/alsu_route_context", "message-return", "show-saved-message")
            && await bridge.ChooseDialogueAsync(Prefix + "dialogue/alsu_route_context", "message-question", "quote-heard-voice")
            && bridge.ActiveSceneId == scene,
            "Alsu's return conversation was rejected or rewound the investigation.");
        main.SwitchZone("house_old_pc", "entry");
        await Frames(owner, 2);
        GD.Print("state-flow-setup: actual message excerpt selection and return questions; no fabricated flags or traversal claim");
        return true;
    }

    internal static async Task<bool> RouteDiscussionAsync(Node owner, RuntimeBridge bridge)
    {
        Main(owner).SwitchZone("village_day", "from_house");
        await Frames(owner, 2);
        Check(await bridge.CompareJournalSourcesAsync(Prefix + "interaction/compare-route-purpose-landmarks", new[]
                { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/doc_kara_urman_edge_sketch" })
            && await bridge.DispatchInteractionAsync(Prefix + "interaction/route-to-mosque")
            && await bridge.EnterDialogueNodeAsync(Prefix + "dialogue/timur_restraint", "restraint")
            && await bridge.ChooseDialogueAsync(Prefix + "dialogue/timur_restraint", "restraint", "tell-register")
            && await bridge.ChooseDialogueAsync(Prefix + "dialogue/timur_restraint", "boundary-reply", "discuss-route-check")
            && await bridge.ChooseDialogueAsync(Prefix + "dialogue/timur_restraint", "route-question", "name-landmark-check")
            && bridge.IsInteractionAvailable(Prefix + "interaction/edge-sketch-to-zirat-road"),
            "The route purpose and Timur's real questions did not support the road check.");
        GD.Print("state-flow-setup: actual route-source comparison and Timur choices; no fabricated flags or traversal claim");
        return true;
    }

    internal static async Task<bool> EnterForestAsync(Node owner, RuntimeBridge bridge)
    {
        Check(bridge.ActiveSceneId == Prefix + "scene/forest-approach", "The finale fixture skipped the forest approach scene.");
        Main(owner).SwitchZone("kara_urman_night", "village_path");
        await Frames(owner, 2);
        return await Act1RinatRoadsideProof.EnterFinaleAsync(owner, bridge);
    }

    internal static async Task<bool> WaitForTerminalAsync(Node owner, RuntimeBridge bridge)
    {
        var tree = owner.GetTree();
        var cue = tree.GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi
            ?? throw new InvalidOperationException("The finale has no audio/caption owner.");
        var actor = Main(owner).FindChild("Npc_rinat", true, false) as Node3D
            ?? throw new InvalidOperationException("The finale has no real Rinat actor.");
        var captureDirectory = OS.GetEnvironment("URMAN_FINAL_CAPTURE_DIR");
        var capturePrefix = $"rinat_{owner.Name}_{Time.GetTicksMsec()}";
        var captureCount = 0;
        ulong lastCapture = 0;
        var deadline = Time.GetTicksMsec() + 15000;
        while (Time.GetTicksMsec() < deadline)
        {
            // Keep the ordinary test look on the whole approaching actor. Feet,
            // movement, visibility, input locks and narrative remain runtime-owned.
            if (!Act1RinatRoadsideProof.LookAtIntervention(owner)) return false;
            var state = bridge.SelectRuntimeState();
            var rule = state.GetProperty("knowledge").GetProperty(Prefix + "knowledge/clue_do_not_answer_rule")
                .GetProperty("status").GetString();
            var completed = state.GetProperty("beats").TryGetProperty(Prefix + "beat/cliffhanger-hard-cut", out var beat)
                && beat.GetString() == "completed";
            var phase = actor.GetMeta("rinatInterventionPhase", "pending").AsString();
            if (!string.IsNullOrWhiteSpace(captureDirectory) && DisplayServer.GetName() != "headless"
                && captureCount < 6 && Time.GetTicksMsec() - lastCapture >= 180
                && phase is "physical-step-and-stop-hand" or "step-landed-stop-hand" or "visible-landed-stop-hand")
            {
                await CaptureRenderedStepAsync(owner, actor, captureDirectory, $"{capturePrefix}_{captureCount:D2}");
                captureCount++;
                lastCapture = Time.GetTicksMsec();
            }
            if (rule == "confirmed" || completed)
            {
                Check(rule == "confirmed" && completed && !cue.IsPresenting && cue.LastStartedAssetId == RinatCue
                    && phase == "visible-landed-stop-hand"
                    && actor.GetMeta("rinatInterventionStepDistance", 0f).AsSingle() > .14f,
                    $"Terminal knowledge preceded Rinat's actual step/gesture or completed voice: phase={phase}, cue={cue.LastStartedAssetId}, presenting={cue.IsPresenting}.");
                GD.Print("state-final-presentation: actual Rinat step and visible stop gesture, completed audio/caption, then one terminal beat");
                return true;
            }
            Check(rule == "hidden" && bridge.ActiveSceneId == Prefix + "scene/forest",
                "The pending finale changed its knowledge or scene outside the staged intervention.");
            await Frames(owner, 1);
        }
        throw new InvalidOperationException($"The real final presentation did not complete within the local test timeout: phase={actor.GetMeta("rinatInterventionPhase", "pending")}, cue={cue.LastStartedAssetId}, presenting={cue.IsPresenting}.");
    }

    internal static async Task CaptureRenderedStepAsync(Node owner, Node3D actor, string directory, string name)
    {
        Check(System.IO.Path.IsPathFullyQualified(directory) && System.IO.Directory.Exists(directory),
            "Finale captures require an existing absolute evidence directory.");
        await WaitForRenderedFrameAsync(owner, name + "/capture");
        var path = System.IO.Path.Combine(directory, name + ".png");
        Check(!System.IO.File.Exists(path), "Refusing to overwrite a historical Rinat movement capture.");
        using var shot = owner.GetViewport().GetTexture().GetImage();
        Check(shot.SavePng(path) == Error.Ok, "Could not save the current physical Rinat movement frame.");
        GD.Print("state-final-rendered-capture: " + path);
        if (name.EndsWith("_00", StringComparison.Ordinal) || name.EndsWith("_01", StringComparison.Ordinal))
        {
            var tree = owner.GetTree();
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController;
            var camera = player?.GetNode<Camera3D>("Head/Camera3D");
            if (camera is not null)
            {
                var study = VerifyLegacyBoundaryCharmsHidden(owner);
                var probeStarted = Time.GetTicksMsec();
                GD.Print($"state-final-visible-probe-begin: capture={name} scope={study.GetPath()} tick={probeStarted}");
                Act1VisibleSurfaceProbe.Log(study, camera, name + "/head-occlusion-owner",
                    new(.46f, .15f), new(.51f, .25f), new(.45f, .31f), new(.50f, .48f));
                GD.Print($"state-final-visible-probe-end: capture={name} milliseconds={Time.GetTicksMsec() - probeStarted}");
            }
            var carry = tree.GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
            GD.Print($"state-final-visible-owner: capture={name} heldItem={carry?.HeldItem?.ItemId ?? "<none>"} heldPosition={carry?.HeldItem?.GlobalPosition.ToString() ?? "<none>"} actor={actor.GlobalPosition} actorYaw={actor.GlobalRotation.Y:0.000}");
        }
        var boots = actor.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.IsVisibleInTree() && mesh.Name.ToString().Contains("_Boot", StringComparison.Ordinal)
                && mesh.Name.ToString().Contains("_LOD0", StringComparison.Ordinal)).ToArray();
        Check(boots.Length == 2, "The real Rinat step does not expose both rendered boots.");
        var excludes = new global::Godot.Collections.Array<Rid>(Main(owner)
            .FindChildren("*", nameof(StaticBody3D), true, false).OfType<InteractionTarget>().Select(target => target.GetRid()));
        foreach (var contact in actor.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>())
            excludes.Add(contact.GetRid());
        var gaps = new List<float>();
        var centers = new Dictionary<string, Vector3>();
        foreach (var boot in boots)
        {
            using var baked = boot.BakeMeshFromCurrentSkeletonPose();
            var vertices = Enumerable.Range(0, baked.GetSurfaceCount()).SelectMany(surface =>
                baked.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                .Select(vertex => boot.GlobalTransform * vertex).ToArray();
            Check(vertices.Length > 0, "The rendered Rinat boot has no vertices.");
            centers[boot.Name.ToString().Contains("BootLeft", StringComparison.Ordinal) ? "left" : "right"]
                = vertices.Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex) / vertices.Length;
            var low = vertices.Min(vertex => vertex.Y);
            var bootGaps = new List<float>();
            foreach (var sole in vertices.Where(vertex => vertex.Y <= low + .0002f).Distinct())
            {
                using var query = PhysicsRayQueryParameters3D.Create(sole + Vector3.Up * .12f,
                    sole - Vector3.Up * .35f, 3u, excludes);
                var hit = actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
                Check(hit.Count > 0, $"No physical ground below rendered moving boot {boot.Name}: {sole}.");
                var gap = sole.Y - hit["position"].AsVector3().Y;
                Check(float.IsFinite(gap) && gap >= -.020f && gap <= .090f,
                    $"Rendered moving boot {boot.Name} intersects or floats above ground: {gap:0.0000}m.");
                bootGaps.Add(gap);
            }
            gaps.Add(bootGaps.Min());
            GD.Print($"state-final-rendered-foot: capture={name} boot={boot.Name} phase={actor.GetMeta("rinatInterventionPhase", "pending")} actor={actor.GlobalPosition} gapMin={bootGaps.Min():0.0000} gapMax={bootGaps.Max():0.0000}");
        }
        Check(gaps.Min() <= .025f, "Neither rendered foot supports Rinat during the physical step.");
        var lateral = actor.GlobalBasis.X;
        lateral.Y = 0f;
        var signedWidth = (centers["right"] - centers["left"]).Dot(lateral.Normalized());
        GD.Print($"state-final-rendered-turn-stance: capture={name} signedWidth={signedWidth:0.0000}m");
        Check(float.IsFinite(signedWidth) && signedWidth > .05f,
            $"The rendered turn crossed or collapsed Rinat's feet: {signedWidth:0.0000}m.");
    }

    internal static Node3D VerifyLegacyBoundaryCharmsHidden(Node owner)
    {
        var study = Main(owner).FindChild("kara-urman-edge", true, false) as Node3D
            ?? throw new InvalidOperationException("The connected finale has no retained Kara study source.");
        foreach (var name in new[] { "BoundaryCharm", "BoundaryCharm2" })
        {
            var charm = study.GetNodeOrNull<Node3D>(name)
                ?? throw new InvalidOperationException("The retained Kara study lost its named decoration owner: " + name);
            var meshes = charm.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
            Check(meshes.Length == 2 && meshes.All(mesh => !mesh.IsVisibleInTree()),
                $"Unsupported legacy cloth or cord remains visible beside Rinat: {charm.GetPath()}.");
        }
        GD.Print("state-final-legacy-boundary-charms: both study owners retained; all four cloth/cord meshes hidden in connected world");
        return study;
    }

    internal static async Task WaitForRenderedFrameAsync(Node owner, string label)
    {
        var started = Time.GetTicksMsec();
        var drawnBefore = Engine.GetFramesDrawn();
        var processBefore = Engine.GetProcessFrames();
        var physicsBefore = Engine.GetPhysicsFrames();
        var window = owner.GetWindow();
        var arguments = OS.GetCmdlineArgs();
        var forceBackgroundCapture = DisplayServer.GetName() != "headless" && !window.HasFocus()
            && arguments.Contains("--urman-smoke-background-input", StringComparer.Ordinal)
            && arguments.Any(argument => argument.StartsWith("res://tests/", StringComparison.Ordinal)
                && argument.EndsWith(".tscn", StringComparison.Ordinal));
        GD.Print($"state-final-render-sync-begin: label={label} tick={started} drawn={drawnBefore} process={processBefore} physics={physicsBefore} windowVisible={window.Visible} focus={window.HasFocus()} mode={window.Mode} renderLoop={RenderingServer.RenderLoopEnabled}");
        var drawn = new TaskCompletionSource<bool>();
        var callback = Callable.From(() => { drawn.TrySetResult(true); });
        var server = RenderingServer.Singleton;
        server.Connect(RenderingServer.SignalName.FramePostDraw, callback);
        try
        {
            var deadline = Task.Delay(5000);
            if (forceBackgroundCapture)
            {
                // macOS may stop drawing an occluded native window while its
                // process/physics loop continues. This explicit test opt-in
                // requests one actual viewport draw on the main thread. Keep
                // the real post-draw signal and readback as evidence.
                GD.Print($"state-final-render-sync-forced: label={label} forced screenshot render after native focus loss; one focus request + ForceDraw(false); no foreground or performance claim");
                window.GrabFocus();
                RenderingServer.ForceDraw(false);
            }
            var completed = await Task.WhenAny(drawn.Task, deadline);
            var frameInTime = completed == drawn.Task && Time.GetTicksMsec() - started <= 5000;
            if (!frameInTime)
                GD.Print($"state-final-render-sync-timeout: label={label} milliseconds={Time.GetTicksMsec() - started} drawnDelta={Engine.GetFramesDrawn() - drawnBefore} processDelta={Engine.GetProcessFrames() - processBefore} physicsDelta={Engine.GetPhysicsFrames() - physicsBefore} windowVisible={window.Visible} focus={window.HasFocus()} mode={window.Mode} renderLoop={RenderingServer.RenderLoopEnabled} treePaused={owner.GetTree().Paused}");
            Check(frameInTime,
                $"Native render did not supply a frame for the local capture within 5 seconds: {label}; rendering external/not-run for this checkpoint.");
            await drawn.Task;
            if (forceBackgroundCapture)
            {
                using var pixels = owner.GetViewport().GetTexture().GetImage();
                Check(!pixels.IsEmpty() && pixels.GetWidth() > 0 && pixels.GetHeight() > 0,
                    $"Forced native render emitted FramePostDraw but supplied no viewport pixels: {label}.");
                GD.Print($"state-final-render-sync-forced-readback: label={label} framePostDraw=True pixels={pixels.GetWidth()}x{pixels.GetHeight()} drawnDelta={Engine.GetFramesDrawn() - drawnBefore}; screenshot evidence only");
            }
        }
        finally
        {
            if (server.IsConnected(RenderingServer.SignalName.FramePostDraw, callback))
                server.Disconnect(RenderingServer.SignalName.FramePostDraw, callback);
        }
        GD.Print($"state-final-render-sync-end: label={label} milliseconds={Time.GetTicksMsec() - started} forcedScreenshotRender={forceBackgroundCapture}");
    }

    private static Main Main(Node owner) => owner.GetTree().GetFirstNodeInGroup("zone_manager") as Main
        ?? throw new InvalidOperationException("State setup has no physical world owner.");

    private static async Task Frames(Node owner, int count)
    {
        for (var frame = 0; frame < count; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
