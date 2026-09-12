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

        for (var frame = 0; frame < 2; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var spinnerTarget = main.ConnectedWorld.GetZoneInstance("village_day")?
            .GetNodeOrNull<InteractionTarget>("Discovery_babai-yard-childhood-spinner");
        var journalScreen = (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?
            .GetNodeOrNull<Control>("Screen");
        if (spinnerTarget is null
            || journalScreen is null
            || journalScreen.Visible
            || !spinnerTarget.IsAvailable()
            || !string.IsNullOrEmpty(spinnerTarget.JournalEntryId)
            || !PhysicalRayHits(spinnerTarget))
        { Fail("Yard spinner did not expose a ray-only first-action target without an open journal."); return; }

        var spinnerStateBefore = bridge.SelectRuntimeState().GetRawText();
        var spinnerJournalCountBefore = bridge.JournalEntries().Count;
        var firstSpinnerStateChanges = 0;
        void OnFirstSpinnerStateChanged() => firstSpinnerStateChanges++;
        bridge.RuntimeStateChanged += OnFirstSpinnerStateChanged;
        spinnerTarget.Interact();
        for (var frame = 0; frame < 60 && firstSpinnerStateChanges == 0; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var frame = 0; frame < 2; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bridge.RuntimeStateChanged -= OnFirstSpinnerStateChanged;
        if (journalScreen.Visible
            || firstSpinnerStateChanges == 0
            || spinnerStateBefore == bridge.SelectRuntimeState().GetRawText()
            || bridge.JournalEntries().Count != spinnerJournalCountBefore + 1
            || !bridge.JournalEntries().Any(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-babai-yard-childhood-spinner")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-babai-yard-childhood-spinner") != "confirmed"
            || !spinnerTarget.IsAvailable()
            || spinnerTarget.CollisionLayer != 4u)
        { Fail("Yard spinner first action did not commit once while leaving the journal UI closed and the ray target enabled."); return; }

        if (!PhysicalRayHits(spinnerTarget))
        { Fail("Yard spinner repeat target lost its physical ray hit after the first action."); return; }
        var spinnerRepeatStateBefore = bridge.SelectRuntimeState().GetRawText();
        var spinnerRepeatJournalCount = bridge.JournalEntries().Count;
        var repeatSpinnerStateChanges = 0;
        void OnRepeatSpinnerStateChanged() => repeatSpinnerStateChanges++;
        bridge.RuntimeStateChanged += OnRepeatSpinnerStateChanged;
        spinnerTarget.Interact();
        for (var frame = 0; frame < 3; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bridge.RuntimeStateChanged -= OnRepeatSpinnerStateChanged;
        if (journalScreen.Visible
            || repeatSpinnerStateChanges != 0
            || spinnerRepeatStateBefore != bridge.SelectRuntimeState().GetRawText()
            || spinnerRepeatJournalCount != bridge.JournalEntries().Count)
        { Fail("Yard spinner repeat created a runtime or journal event, or opened the journal UI."); return; }

        if (bridge.LearnedVocabulary().Any(entry => entry.Id == ChapterPrefix + "vocabulary/tt_yul")
            || await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-yul-road"))
        { Fail("The road word or its optional question was exposed before reading the sign reverse."); return; }

        foreach (var slug in new[] { "arrival-bench-race-notches", "arrival-insulated-well", "main-street-sign-reverse", "babai-yard-sled-repair", "house-exterior-porch-nook", "fap-exterior-service-path", "zirat-outer-rest-bench", "connective-street-return-bench", "fap-exterior-care-porch", "main-street-side-window", "connective-street-repair-bench", "babai-yard-loose-side-gate-board", "kara-old-forestry-side-track", "kara-warm-window-clearing", "kara-branch-profile", "main-street-fenced-service-lane", "connective-street-shed-bypass", "zirat-outer-culvert-crossing", "house-exterior-rear-minaret-view" })
            if (!await Discover(bridge, slug)) return;
        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
        var restBenchSnow = main.ConnectedWorld.GetNode<Node3D>("Act1CoreWorldGreybox/ZiratMemoryField/DiscoveryRestBench/SnowOnRepairedSeat");
        if (restBenchSnow.Scale.X > .01f)
        { Fail("Roadside bench discovery did not reveal its repaired seat."); return; }
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (var slug in new[] { "house-interior-photo-back", "house-interior-language-tin" })
            if (!await Discover(bridge, slug)) return;
        if (!await bridge.LoadSlotAsync("checkpoint")) { Fail("Interior discovery checkpoint failed to restore."); return; }
        await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
        var house = main.ConnectedWorld.GetZoneInstance("house_old_pc")!;
        if (Mathf.Abs(house.GetNode<Node3D>("DiscoveryFamilyPhoto").Rotation.Y - Mathf.Pi) > .01f
            || house.GetNode<Node3D>("DiscoverySewingTin/HingedLid").Rotation.X > -1.8f
            || !bridge.LearnedVocabulary().Any(entry => entry.Term == "өй" && entry.Status == "confirmed")
            || restBenchSnow.Scale.X > .01f
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-babai-yard-childhood-spinner") != "confirmed"
            || main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("RestBenchCollision").CollisionLayer != 0
            || bridge.ActiveSceneId != Scene("house"))
        { Fail("Restored photo, tin or explicit home vocabulary did not match shared discovery state."); return; }
        var core = main.ConnectedWorld.GetNode<Node3D>("Act1CoreWorldGreybox");
        var bypassCollision = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("Act1BypassCollision");
        var mainBypassGate = bypassCollision.GetNode<CollisionShape3D>("MainStreetServiceGate");
        var shedBypassGate = bypassCollision.GetNode<CollisionShape3D>("ConnectiveShedBypassGate");
        var mainBypassHinge = (Node3D)core.FindChild("MainStreetServiceGateHinge", true, false);
        var shedBypassHinge = (Node3D)core.FindChild("ConnectiveVariantCGateHinge", true, false);
        var culvertSnow = core.GetNode<Node3D>("ZiratMemoryField/ZiratOuterCulvertCrossing/FootbridgeSnowCap");
        var culvertBody = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("ZiratOuterCulvertCollision");
        if (bypassCollision.CollisionLayer != 0 || !mainBypassGate.Disabled || !shedBypassGate.Disabled
            || Mathf.Abs(mainBypassHinge.RotationDegrees.Y - 92f) > .1f
            || Mathf.Abs(Mathf.Abs(shedBypassHinge.RotationDegrees.Y) - 92f) > .1f
            || culvertSnow.Visible || culvertBody.CollisionLayer != 0
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-house-exterior-rear-minaret-view") != "confirmed")
        { Fail("Loaded bypasses or culvert lost their reveal or retained exterior collision indoors."); return; }
        var arrivalSnow = core.GetNode<Node3D>("Arrival/DiscoveryArrivalBench/SnowCap");
        var wellMitten = core.GetNode<Node3D>("MainStreet/DiscoveryArrivalWellDetail/TiedMitten");
        var signBoard = core.GetNode<Node3D>("MainStreet/DiscoveryMainStreetSign/Board");
        var sledCover = core.GetNode<Node3D>("BabaiEbiYard/BabaiYardSled/SledBlueRepairCover");
        var nookMitten = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade/StoredMendedMitten");
        var serviceGate = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("FapServiceGateCollision");
        var yardGate = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("BabaiYardSideGateCollision");
        var yardBoard = core.GetNode<Node3D>("BabaiYardSideGateExploration/BabaiYardLooseSideGate/LooseBoardHinge");
        var serviceFence = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("FapServiceFenceCollision");
        var bagShelf = core.GetNode<Node3D>("ConnectiveStreetReturn/ConnectiveReturnCareBench/BagRestShelf");
        var carePorch = core.GetNode<Node3D>("FapExterior/FapClinicAuthoredKitPresentation/FapAuthoredEntryPorch");
        var careCup = carePorch.GetNode<Node3D>("FapCareEnamelCup");
        var careNapkin = carePorch.GetNode<Node3D>("FapCareDryNapkin");
        var windowReveal = core.FindChild("MainStreetSideWindowReveal", true, false) as Node3D;
        var windowHousehold = windowReveal?.GetNode<Node3D>("SillHouseholdDetail");
        var windowFog = windowReveal?.GetNode<Node3D>("FogCircleAndEars");
        var handleWrap = core.FindChild("FinishedHandleWrap", true, false) as Node3D;
        if (bagShelf.Rotation.Z != 0 || careCup.Rotation.X != 0
            || Mathf.Abs(careCup.Position.Z - .52f) > .01f || !careNapkin.Visible
            || windowHousehold?.Visible != true || windowFog?.Visible != true || handleWrap?.Visible != true
            || main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("ConnectiveRepairBenchCollision").CollisionLayer != 0
            || main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("ConnectiveReturnCareBenchCollision").CollisionLayer != 0)
        { Fail("Loaded care and street discoveries lost their reveal or retained indoor physics."); return; }
        if (yardGate.CollisionLayer != 0 || Mathf.Abs(yardBoard.Rotation.Y - Mathf.Pi / 2) > .01f
            || arrivalSnow.Visible || Mathf.Abs(wellMitten.Rotation.Y - Mathf.Pi) > .01f
            || Mathf.Abs(signBoard.RotationDegrees.Y - 176f) > .1f
            || sledCover.Visible || !nookMitten.Visible
            || serviceGate.CollisionLayer != 0 || serviceFence.CollisionLayer != 0)
        { Fail("Loaded outdoor discoveries lost their presentation or retained exterior physics indoors."); return; }

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

        if (!await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-yul-road"))
        { Fail("The confirmed sign word did not unlock its optional Alsu question."); return; }

        if (!await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (var slug in new[] { "fap-interior-height-marks", "fap-interior-repaired-desk-object" })
            if (!await Discover(bridge, slug)) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var repairedLight = main.ConnectedWorld.GetZoneInstance("fap_clinic")!
            .GetNode<OmniLight3D>("DiscoveryRepairedLamp/RepairedDeskLight");
        if (!repairedLight.IsVisibleInTree()) { Fail("The repaired lamp did not light its desk."); return; }

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
        var houseRinatHost = house.GetNode<Node3D>("Act1NpcPresentation");
        var houseRinat = houseRinatHost.GetNodeOrNull<Node3D>("Npc_rinat");
        if (houseRinat is null
            || !ReferenceEquals(houseRinat, rinatActor)
            || Mathf.Abs(houseRinat.Position.X - 4.05f) > .01f
            || Mathf.Abs(houseRinat.Position.Y) > .01f
            || Mathf.Abs(houseRinat.Position.Z + 2.65f) > .01f)
        { Fail("Rinat was not staged as a live actor beside the home evidence target."); return; }
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
        if (!ReferenceEquals(rinatActor.GetParent(), houseRinatHost))
        { Fail("Rinat left the house before the internal-register response completed."); return; }

        if (!await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")) return;
        if (!await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")) return;
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal")
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || await bridge.ChooseDialogueAsync(Dialogue("mansur_pc_request"), "ask-for-help", "ask-about-javap")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" }))
        { Fail("The language comparison failed or Mansur's question was exposed before learning the word."); return; }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var mansurTarget = house.GetNode<InteractionTarget>("MansurNpc");
        if (!mansurTarget.IsAvailable() || !PhysicalRayHits(mansurTarget))
        { Fail("Mansur cannot be approached after the archive comparison."); return; }
        var investigationScene = bridge.ActiveSceneId;
        mansurTarget.Interact();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var mansurDialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
        var javapQuestion = mansurDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText(ChapterPrefix + "text/choice-mansur-javap"));
        if (!mansurDialogue.IsOpen || javapQuestion is null)
        { Fail("The learned word did not open a usable question in Mansur's dialogue."); return; }
        javapQuestion.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!mansurDialogue.IsOpen
            || mansurDialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != bridge.ResolveText(ChapterPrefix + "text/dialogue-mansur-javap-reply")
            || bridge.ActiveSceneId != investigationScene)
        { Fail("Mansur's response was missing or reset the active investigation scene."); return; }
        mansurDialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        if (!await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")) return;
        if (!await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")) return;
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch"))
        { Fail("Could not read the edge sketch."); return; }
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ReferenceEquals(rinatActor.GetParent(), houseRinatHost)
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
        if (!ReferenceEquals(rinatActor.GetParent(), people) || rinatActor.GlobalPosition.Z > -120f)
        { Fail("Rinat did not move from the house to the forest stage at zirat-road."); return; }

        var ziratClue = Interaction("zirat-roadside-clue");
        if (!bridge.IsInteractionAvailable(ziratClue)
            || !await bridge.DispatchInteractionAsync(ziratClue)
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" })
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("The zirat roadside interaction did not grant Marat's route clue through RuntimeBridge.");
            return;
        }

        if (!await Advance(bridge, "zirat-road-to-forest", "forest-approach")) return;
        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var approachAudio = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden"
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") == "completed"
            || approachAudio?.LastAssetId is "urman.chapter1:asset/audio-marat-voice" or "urman.chapter1:asset/audio-rinat-interruption")
        { Fail("Optional forest approach started the finale before the endpoint."); return; }
        if (!await Advance(bridge, "forest-approach-to-forest", "forest")) return;
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
        if (audioCue.LastPresentedText != "Ринат: «Не отвечай».")
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
        if (!ReferenceEquals(rinatActor.GetParent(), people)
            || rinatActor.GlobalPosition.Z < -5f || ambience.ActivePlayerIndex < 0
            || Mathf.Abs(house.GetNode<Node3D>("Act1NpcPresentation/Npc_gulsina").RotationDegrees.Y - 28f) > .1f)
        { Fail("New Game did not restore early NPC staging and ambience."); return; }
        if (house.GetNode<Node3D>("DiscoveryFamilyPhoto").Rotation.Y != 0
            || house.GetNode<Node3D>("DiscoverySewingTin/HingedLid").Rotation.X != 0
            || repairedLight.Visible
            || restBenchSnow.Scale.X != 1f
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-zirat-outer-rest-bench") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-house-interior-photo-back") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-babai-yard-childhood-spinner") != "hidden"
            || bridge.JournalEntries().Any(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-babai-yard-childhood-spinner"))
        { Fail("New Game retained optional discovery presentation or knowledge."); return; }
        if (yardGate.CollisionLayer != 1 || yardBoard.Rotation.Y != 0
            || !arrivalSnow.Visible || wellMitten.Rotation.Y != 0
            || Mathf.Abs(signBoard.RotationDegrees.Y + 4f) > .1f
            || !sledCover.Visible || nookMitten.Visible
            || serviceGate.CollisionLayer != 1 || serviceFence.CollisionLayer != 1
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-fap-exterior-service-path") != "hidden")
        { Fail("New Game retained an outdoor reveal or failed to close the service gate."); return; }
        if (Mathf.Abs(bagShelf.Rotation.Z - Mathf.Pi / 2) > .01f
            || Mathf.Abs(careCup.Rotation.X - Mathf.Pi) > .01f
            || Mathf.Abs(careCup.Position.Z - .18f) > .01f || careNapkin.Visible
            || windowHousehold?.Visible != false || windowFog?.Visible != false || handleWrap?.Visible != false)
        { Fail("New Game retained care or street discovery presentation."); return; }
        if (bypassCollision.CollisionLayer != 1 || mainBypassGate.Disabled || shedBypassGate.Disabled
            || mainBypassHinge.Rotation.Y != 0 || shedBypassHinge.Rotation.Y != 0
            || !culvertSnow.Visible || culvertBody.CollisionLayer != 1
            || new[] { "main-street-fenced-service-lane", "connective-street-shed-bypass", "zirat-outer-culvert-crossing", "house-exterior-rear-minaret-view" }
                .Any(slug => KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-" + slug) != "hidden"))
        { Fail("New Game retained a bypass opening, culvert reveal or rear-view knowledge."); return; }
        GD.Print("chapter-one-flow-smoke: authored route -> visible people -> final silence -> menu -> fresh session");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private static bool PhysicalRayHits(InteractionTarget target)
    {
        var eye = target.GlobalPosition + new Vector3(0f, .6f, 1.5f);
        var hit = target.GetWorld3D().DirectSpaceState.IntersectRay(
            PhysicsRayQueryParameters3D.Create(eye, target.GlobalPosition, 5));
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target;
    }

    private async Task<bool> Discover(RuntimeBridge bridge, string slug)
    {
        var scene = bridge.ActiveSceneId;
        var main = (Main)GetTree().GetFirstNodeInGroup("zone_manager");
        var target = main.ConnectedWorld!.FindChild("Discovery_" + slug, true, false) as InteractionTarget;
        if (target is null) { Fail("Discovery has no physical target: " + slug); return false; }
        if (target.CollisionLayer != 4u) { Fail("Discovery selection volume blocks player physics: " + slug); return false; }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var side = slug is "house-interior-language-tin" or "fap-interior-height-marks" ? -1f : 1f;
        if (slug == "fap-exterior-service-path") side = -1f;
        var eye = target.GlobalPosition + new Vector3(0, .6f, side * 1.5f);
        GD.Print($"discovery-physical-ray slug={slug} at={target.GlobalPosition} eye={eye}");
        var hit = target.GetWorld3D().DirectSpaceState.IntersectRay(
            PhysicsRayQueryParameters3D.Create(eye, target.GlobalPosition, 5));
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != target)
        { Fail("Discovery ray is occluded or misses its authored target: " + slug); return false; }
        if (!await bridge.DispatchInteractionAsync(Interaction("discover-" + slug))
            || bridge.ActiveSceneId != scene
            || bridge.IsInteractionAvailable(Interaction("discover-" + slug))
            || !bridge.JournalEntries().Any(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-" + slug))
        { Fail("Optional discovery failed its single-use journal/world contract: " + slug); return false; }
        return true;
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
