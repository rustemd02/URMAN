using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class ChapterOneFlowSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string FirstSnowPhoto = ChapterPrefix + "document/first-snow-photo";

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
        // The forest teaser returns the player to the village asynchronously
        // after its skip; let that land before any state invariant begins.
        for (var frame = 0; frame < 600 && bridge?.CurrentZoneId != "village_day"; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (bridge?.CurrentZoneId != "village_day")
        {
            Fail("Chapter 1 flow did not settle at the village after the prologue.");
            return;
        }
        if (bridge is null || bridge.ActiveSceneId != Scene("arrival_vehicle_dusk"))
        {
            Fail("Chapter 1 flow did not start at the authored arrival scene.");
            return;
        }
        if (!await RejectUnvisitedDoorRepeats(bridge, main, "ReturnToHouseRegister", "RoadToFap")) return;

        if (await bridge.OpenDocumentAsync(FirstSnowPhoto)
            || bridge.JournalEntries().Any(entry => entry.EntryId == FirstSnowPhoto))
        { Fail("The house photograph was available before its actual discovery."); return; }
        foreach (var action in new[] { "observe-photo-facade-windows", "observe-photo-yard", "inspect-photo-gate-repair" })
        {
            if (bridge.IsInteractionAvailable(Interaction(action))
                || await bridge.DispatchInteractionAsync(Interaction(action))
                || new[] { "clue_photo_facade_windows_observed", "clue_photo_yard_observed", "clue_photo_place_compared",
                    "clue_photo_gate_repair_checked", "clue_photo_question_answered" }
                    .Any(id => KnowledgeStatus(bridge.SelectRuntimeState(), id) != "hidden"))
            { Fail("An unread photograph supplied a physical observation or later result: " + action); return; }
        }
        if (!await Act1LocalReactionProof.RejectUnreadAsync(this, bridge)) return;

        var people = main.ConnectedWorld!.GetNode<Node3D>("Act1CoreWorldGreybox/Act1People");
        var rinatActor = people.GetNode<Node3D>("Npc_rinat");
        var peopleNames = string.Join(",", people.GetChildren().OfType<Node3D>().Select(child => child.Name.ToString()));
        // No duplicate imam: BuildMosqueInterior reparents the staged Timur
        // into the prayer hall at world build, so the exterior proof checks
        // Alsu outside and Timur at his mosque post instead of under Act1People.
        var mosqueTimur = main.ConnectedWorld.GetNodeOrNull<Node3D>(
            "Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior/Npc_timur_hazrat");
        if (!people.GetNode<Node3D>("Act1NpcPresentation/Npc_alsu").IsVisibleInTree()
            || mosqueTimur is not { } timurActor || !timurActor.IsVisibleInTree()
            || rinatActor.GlobalPosition.Z < -5f)
        { Fail($"Act I people are misplaced: Alsu/Timur hidden or Rinat starts at the late position. Act1People=[{peopleNames}]"); return; }

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
            // The written text names бабай, so the same action teaches the word
            // and with it the question about what to call him now. Conditions are
            // evaluated without committing the choice, so the repeat-action
            // assertions below still see the same runtime state.
            || !bridge.LearnedVocabulary().Any(entry => entry.Term == "бабай" && entry.Status == "confirmed")
            || !bridge.EvaluateConditions(bridge.RequireDialogue(Dialogue("mansur_pc_request"))
                .Nodes["ask-for-help"].Choices.Single(choice => choice.Id == "ask-how-to-call").Conditions)
            || !spinnerTarget.IsAvailable()
            || spinnerTarget.CollisionLayer != 4u)
        { Fail("Yard spinner first action did not commit once, leave the journal UI closed, teach бабай and open Mansur's question, or keep the ray target enabled."); return; }

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
            || bridge.LearnedVocabulary().Any(entry => entry.Id == ChapterPrefix + "vocabulary/tt_shurale")
            || await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-yul-road")
            || await bridge.ChooseDialogueAsync(Dialogue("timur_restraint"), "restraint", "ask-shurale-name"))
        { Fail("The road word, the Шүрәле word or one of their optional questions was exposed before its source was read."); return; }

        foreach (var slug in new[] { "arrival-bench-race-notches", "arrival-insulated-well", "main-street-sign-reverse", "babai-yard-sled-repair", "house-exterior-porch-nook", "fap-exterior-service-path", "zirat-outer-rest-bench", "connective-street-return-bench", "fap-exterior-care-porch", "main-street-side-window", "connective-street-repair-bench", "babai-yard-loose-side-gate-board", "kara-old-forestry-side-track", "kara-warm-window-clearing", "kara-branch-profile", "main-street-fenced-service-lane", "connective-street-shed-bypass", "zirat-outer-culvert-crossing", "house-exterior-rear-minaret-view" })
            if (!await Discover(bridge, slug)) return;
        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
        var restBenchSnow = main.ConnectedWorld.GetNode<Node3D>("Act1CoreWorldGreybox/ZiratMemoryField/DiscoveryRestBench/SnowOnRepairedSeat");
        if (restBenchSnow.Scale.X > .01f)
        { Fail("Roadside bench discovery did not reveal its repaired seat."); return; }
        await Act1ArrivalFlowProof.CompleteAsync(this, bridge);
        if (!await Advance(bridge, "arrival-enter-house", "house")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await RejectUnvisitedDoorRepeats(bridge, main, "HouseExit")) return;

        foreach (var slug in new[] { "house-interior-photo-back", "house-interior-language-tin" })
            if (!await Discover(bridge, slug)) return;
        if (!await bridge.LoadSlotAsync("checkpoint")) { Fail("Interior discovery checkpoint failed to restore."); return; }
        await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
        var house = main.ConnectedWorld.GetZoneInstance("house_old_pc")!;
        if (Mathf.Abs(Mathf.AngleDifference(house.GetNode<Node3D>("DiscoveryFamilyPhoto").Rotation.Y,
                Mathf.DegToRad(StyleBenchmarkInteriorFactory.PhotoYawDegrees) + Mathf.Pi)) > .01f
            || house.GetNode<Node3D>("DiscoverySewingTin/HingedLid").Rotation.X > -1.8f
            || !bridge.LearnedVocabulary().Any(entry => entry.Term == "өй" && entry.Status == "confirmed")
            || restBenchSnow.Scale.X > .01f
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-babai-yard-childhood-spinner") != "confirmed"
            || main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("RestBenchCollision").CollisionLayer != 0
            || bridge.ActiveSceneId != Scene("house"))
        { Fail("Restored photo, tin or explicit home vocabulary did not match shared discovery state."); return; }
        if (!await Act1LocalReactionProof.CompleteAsync(this, bridge, "tin")) return;
        if (!await bridge.OpenDocumentAsync(FirstSnowPhoto)
            || !await bridge.OpenDocumentAsync(FirstSnowPhoto)
            || !await bridge.SaveSlotAsync("first-snow-source")
            || !await bridge.LoadSlotAsync("first-snow-source")
            || bridge.JournalEntries().Count(entry => entry.EntryId == FirstSnowPhoto) != 1
            || bridge.ActiveSceneId != Scene("house")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_intent") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "hidden")
        { Fail("Rereading the photograph lost its source, duplicated it, changed the route, or supplied an unperformed location match."); return; }
        await DocumentImageUiProof.VerifyPaperAndJournalAsync(this, bridge, FirstSnowPhoto, "len01_first_snow");
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

        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "hidden")
        { Fail("The house inferred family avoidance without a question about Marat."); return; }
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
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Chapter 1 warning entry inferred an unasked family answer or skipped it at the house exit.");
            return;
        }
        if (!await bridge.ChooseDialogueAsync(Dialogue("gulsina_yaramyy"), "home-warning", "ask-marat")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        { Fail("A question about Marat lost its response or automatically completed the home pause."); return; }
        if (!await Act1FamilyMealProof.CompleteAsync(this, bridge, topic: "help")
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        { Fail("The actual home pause did not complete the remaining first-visit condition."); return; }

        if (!await Advance(bridge, "house-to-route", "crossroad_signs_inspect")) return;
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await VerifyPhotoReturn(bridge, main)) return;

        var alsuStep = "start";
        if (bridge.IsInteractionAvailable(Interaction("route-to-fap"))) alsuStep = "route-to-fap early available";
        else if (!bridge.IsInteractionAvailable(Interaction("talk-alsu"))) alsuStep = "talk-alsu unavailable";
        else
        {
            // A fresh load re-projects vehicles and Alsu's feet over a couple
            // of physics frames; a real player never talks in the same tick.
            // Wait for the world to settle instead of racing it.
            var walkReady = false;
            for (var frame = 0; frame < 120 && !walkReady; frame++)
            {
                var walk = AlsuStreetWalkPresentation.SessionOwner(GetTree());
                if (walk is not null && walk.PhysicalAccessReady) walkReady = true;
                else await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            if (!walkReady)
            {
                var walk = AlsuStreetWalkPresentation.SessionOwner(GetTree());
                GD.Print("alsu-never-ready: " + (walk is null ? "no-session-owner" : walk.DescribeWalkEligibility()));
                alsuStep = "alsu never physically ready";
            }
            else if (!await bridge.DispatchInteractionAsync(Interaction("talk-alsu")))
            {
                var walk = AlsuStreetWalkPresentation.SessionOwner(GetTree());
                GD.Print("alsu-dispatch-refused: " + (walk is null ? "no-session-owner" : walk.DescribeWalkEligibility()));
                alsuStep = "talk-alsu dispatch";
            }
        }
        if (alsuStep == "start" && !await Act1AlsuWalkProof.CompleteAsync(this, bridge)) alsuStep = "alsu-walk-proof";
        if (alsuStep == "start" && !await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "name-road")) alsuStep = "enter name-road";
        if (alsuStep == "start" && bridge.IsInteractionAvailable(Interaction("route-to-fap"))) alsuStep = "route-to-fap after enter";
        if (alsuStep == "start" && !await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-versions")) alsuStep = "ask-versions";
        if (alsuStep == "start" && KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_heard_versions") != "confirmed") alsuStep = "heard-versions not confirmed";
        if (alsuStep == "start" && KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "hidden") alsuStep = "conflict not hidden";
        if (alsuStep == "start" && bridge.IsInteractionAvailable(Interaction("route-to-fap"))) alsuStep = "route-to-fap before compare";
        if (alsuStep == "start" && !await bridge.CompareJournalSourcesAsync(Interaction("compare-versions-scope"), new[] { OfficialNotice, ChapterPrefix + "knowledge/clue_alsu_heard_versions" })) alsuStep = "compare-versions";
        if (alsuStep == "start" && !bridge.IsInteractionAvailable(Interaction("route-to-fap"))) alsuStep = "route-to-fap still locked";
        if (alsuStep == "start") alsuStep = "ok";
        if (alsuStep != "ok")
        {
            Fail("Chapter 1 skipped checking Alsu's secondhand accounts against the notice before the FAP route (" + alsuStep + ").");
            return;
        }

        if (!await bridge.ChooseDialogueAsync(Dialogue("alsu_route_context"), "name-road", "ask-yul-road"))
        { Fail("The confirmed sign word did not unlock its optional Alsu question."); return; }

        if (!await Advance(bridge, "route-to-fap", "fap_waiting_room_day")) return;
        main.SwitchZone("fap_clinic", "waiting_room");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await VerifyFapReturnBeforeQuestion(bridge, main)) return;

        foreach (var slug in new[] { "fap-interior-height-marks", "fap-interior-repaired-desk-object", "fap-service-cabinet" })
            if (!await Discover(bridge, slug)) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var repairedLight = main.ConnectedWorld.GetZoneInstance("fap_clinic")!
            .GetNode<OmniLight3D>("DiscoveryRepairedLamp/RepairedDeskLight");
        if (!repairedLight.IsVisibleInTree()) { Fail("The repaired lamp did not light its desk."); return; }

        var nailaTarget = main.ConnectedWorld.GetZoneInstance("fap_clinic")!
            .GetNode<InteractionTarget>("NailaNpc");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk"))
            || !nailaTarget.IsAvailable() || !PhysicalRayHits(nailaTarget))
        { Fail("Naila's first encounter was unavailable or granted access before a question."); return; }
        nailaTarget.Interact();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var nailaDialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
        var wordingQuestion = nailaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText(ChapterPrefix + "text/dialogue-naila-ask-record"));
        if (!nailaDialogue.IsOpen || wordingQuestion is null
            || bridge.IsInteractionAvailable(Interaction("fap-to-document-desk"))
            || await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "press-contradiction"))
        { Fail("Naila lacked an initial question, exposed the contradiction, or granted access on greeting."); return; }
        wordingQuestion.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!nailaDialogue.IsOpen
            || nailaDialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != bridge.ResolveText(ChapterPrefix + "text/dialogue-naila-wording-detail")
            || !bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        { Fail("Naila's wording answer did not grant access to the record."); return; }
        nailaDialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        if (!await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "official-wording", "ask-transfer"))
        { Fail("Naila's alternative handover question was unavailable."); return; }

        if (!await Advance(bridge, "fap-to-document-desk", "fap_pressure_document_desk")) return;
        if (!nailaTarget.IsAvailable())
        { Fail("Naila became unavailable after opening access to her document desk."); return; }
        if (!await Advance(bridge, "fap-document-desk-to-official-record", "evidence-official-death")) return;
        if (!await Advance(bridge, "official-to-internal-register", "evidence-internal-register")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var houseRinatHost = house.GetNode<Node3D>("Act1NpcPresentation");
        var houseRinat = houseRinatHost.GetNodeOrNull<Node3D>("Npc_rinat");
        var houseRinatTarget = house.GetNode<InteractionTarget>("InternalRegisterToRinat");
        if (houseRinat is null
            || !ReferenceEquals(houseRinat, rinatActor)
            || !houseRinat.IsVisibleInTree()
            || Mathf.Abs(houseRinat.Position.X - StyleBenchmarkInteriorFactory.RinatAnchor.X) > .01f
            || Mathf.Abs(houseRinat.Position.Y - StyleBenchmarkInteriorFactory.RinatAnchor.Y) > .01f
            || Mathf.Abs(houseRinat.Position.Z - StyleBenchmarkInteriorFactory.RinatAnchor.Z) > .01f
            || houseRinatTarget.GlobalPosition.DistanceTo(houseRinat.GlobalPosition + Vector3.Up * .89f) > .01f)
        { Fail("Rinat was not staged as a live actor beside the home evidence target."); return; }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_case_boundary_marker") != "hidden")
        { Fail("Entering the internal-register scene granted a clue before opening its document."); return; }
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/rec_marat_case_register_conflict")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_case_boundary_marker") != "confirmed")
        { Fail("The internal record could not be read."); return; }
        if (await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"),
                new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("Reading the documents supplied their unselected field excerpts."); return; }
        await Act1SourceExcerptProof.RecordNoticeCauseAsync(this, bridge);
        await Act1SourceExcerptProof.RecordRegisterFieldsAsync(this, bridge);

        // This route asks about the raw category before comparing the two
        // documents. The corridor test performs those actions in reverse.
        // Neither obtaining a reply nor reading a record supplies the inference.
        main.SwitchZone("fap_clinic", "waiting_room");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!nailaTarget.IsAvailable() || !PhysicalRayHits(nailaTarget))
        { Fail("Naila was not physically reachable with the raw internal category."); return; }
        nailaTarget.Interact();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var categoryQuestion = nailaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>()
            .FirstOrDefault(button => button.Text == bridge.ResolveText(ChapterPrefix + "text/choice-naila-contradiction"));
        if (!nailaDialogue.IsOpen || categoryQuestion is null)
        { Fail("The raw register did not open Naila's category question before the document comparison."); return; }
        categoryQuestion.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "hidden"
            || await bridge.EnterDialogueNodeAsync(Dialogue("naila_medical_record"), "separate-record"))
        { Fail("The category question supplied an answer before identifying the fields."); return; }
        foreach (var text in new[] { "choice-naila-show-external-wording", "choice-naila-ask-category-scope" })
        {
            var fieldQuestion = nailaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
                .GetChildren().OfType<Button>().SingleOrDefault(button => !button.Disabled
                    && button.Text == bridge.ResolveText(ChapterPrefix + "text/" + text));
            if (!nailaDialogue.IsOpen || fieldQuestion is null)
            { Fail("Naila's field-specific question was absent from the actual dialogue: " + text); return; }
            fieldQuestion.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_record_shared_formulation") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "hypothesis_category_is_diagnosis") != "hidden")
        { Fail("The accurate field reading failed or invented an obligatory mistake."); return; }
        nailaDialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        var scopePair = new[] { "urman.oldpc:document/rec_marat_case_register_conflict", ChapterPrefix + "knowledge/clue_naila_record_scope" };
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_naila_record_scope") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_record_wording_mismatch") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden"
            || await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopePair))
        { Fail("Naila's reply inferred a discrepancy without comparing both documentary sources."); return; }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" })
            || KnowledgeStatus(bridge.SelectRuntimeState(), "contradiction_marat_official_vs_internal") != "hidden"
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-record-scope"), scopePair))
        { Fail("The reply-first investigation order could not reach the grounded record conclusion."); return; }
        // The newly understood discrepancy must not erase the original
        // questions; the player can still correct a misunderstanding.
        foreach (var questionId in new[] { "ask-wording", "ask-transfer" })
            if (!await bridge.ChooseDialogueAsync(Dialogue("naila_medical_record"), "follow-up", questionId))
            { Fail("An original question to Naila disappeared after interpreting the records."); return; }
        if (!await Act1LocalReactionProof.CompleteAsync(this, bridge, "lamp")) return;
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        if (!bridge.IsInteractionAvailable(Interaction("internal-register-to-rinat"))
            || !await bridge.DispatchInteractionAsync(Interaction("internal-register-to-rinat"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("rinat_internal_register"), "dangerous-category")
            || bridge.IsInteractionAvailable(Interaction("internal-register-to-saved-message"))
            || !await bridge.ChooseDialogueAsync(Dialogue("rinat_internal_register"), "dangerous-category", "present-category"))
        {
            Fail("Chapter 1 flow could not apply Rinat's authored internal-register dialogue.");
            return;
        }
        if (!ReferenceEquals(rinatActor.GetParent(), houseRinatHost))
        { Fail("Rinat left the house before the internal-register response completed."); return; }

        if (!await Advance(bridge, "internal-register-to-saved-message", "evidence-saved-message")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_was_afraid_before_death") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("saved-message-to-boundary-source")))
        { Fail("Entering the saved-message scene read Marat's fear for the player."); return; }
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/msg_marat_saved_last_normal"))
        { Fail("Could not open Marat's saved message."); return; }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_message_read") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_was_afraid_before_death") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("saved-message-to-boundary-source"))
            || bridge.IsOldPcDocumentAccessible("urman.oldpc:document/tw_shurale_urman_boundary")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_message_question_prepared") != "hidden")
        { Fail("Reading the message skipped its source check or prevented preparing the question."); return; }
        await Act1SourceExcerptProof.RecordMessageVoiceAsync(this, bridge);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_message_question_prepared") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden")
        { Fail("The actual selected message did not prepare a question or answered for Alsu."); return; }
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var messageReader = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        if (messageReader?.IsOpen == true)
            messageReader._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        var alsuTarget = main.FindChildren("*", "", true, false).OfType<InteractionTarget>()
            .FirstOrDefault(target => target.InteractionId == Interaction("talk-alsu") && target.IsAvailable());
        if (alsuTarget is null || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden")
        { Fail("The prepared message lost Alsu's physical target or credited an unheard reply."); return; }
        alsuTarget.Interact();
        if (!await Act1LocalReactionProof.ChooseVisibleAsync(this, bridge, "choice-alsu-show-message")) return;
        var beforeAlsuTerminal = bridge.SelectRuntimeState();
        if (!await bridge.EnterDialogueNodeAsync(Dialogue("alsu_route_context"), "message-bounded-reply")
            || beforeAlsuTerminal.GetProperty("knowledge").GetRawText() != bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
            || beforeAlsuTerminal.GetProperty("journal").GetRawText() != bridge.SelectRuntimeState().GetProperty("journal").GetRawText())
        { Fail("Entering Alsu's reply without choosing the quotation supplied her account."); return; }
        if (!await Act1LocalReactionProof.ChooseVisibleAsync(this, bridge, "choice-alsu-quote-heard-voice")
            || !await Act1LocalReactionProof.VerifyVisibleReplyAsync(this, bridge, "dialogue-alsu-message-bounded-reply")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "confirmed"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "hypothesis_message_proves_sighting") != "hidden"
            || bridge.ActiveSceneId != Scene("evidence-saved-message"))
        { Fail("The accurate first reading could not reach Alsu's actual response without a forced error or scene rewind."); return; }
        ((DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui"))._UnhandledInput(
            new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        main.SwitchZone("house_old_pc", "entry");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!await Advance(bridge, "saved-message-to-boundary-source", "evidence-tatarwiki-boundary")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_folklore_as_survival_rule") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_voice_answer_is_dangerous_hint") != "hidden")
        { Fail("An evidence-scene transition supplied an unread voice interpretation."); return; }
        if (!await bridge.OpenDocumentAsync("urman.oldpc:document/tw_shurale_urman_boundary")
            || await bridge.ChooseDialogueAsync(Dialogue("mansur_pc_request"), "ask-for-help", "ask-about-javap")
            || !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" }))
        { Fail("The language comparison failed or Mansur's question was exposed before learning the word."); return; }
        if (bridge.IsOldPcDocumentAccessible("urman.oldpc:document/doc_kara_urman_edge_sketch")
            || await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch"))
        { Fail("A direct sketch open replaced rereading the record with a translated word."); return; }
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

        // Re-open the same conversation: the бабай word learned at the yard
        // spinner opens its own question, and its reply is the authored one
        // rather than a placeholder.
        mansurTarget.Interact();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var babaiQuestion = mansurDialogue.IsOpen
            ? mansurDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
                .GetChildren().OfType<Button>()
                .FirstOrDefault(button => button.Text == bridge.ResolveText(ChapterPrefix + "text/choice-mansur-babai-name"))
            : null;
        if (babaiQuestion is null)
        { Fail("The understood бабай word did not open its question in Mansur's dialogue."); return; }
        babaiQuestion.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!mansurDialogue.IsOpen
            || mansurDialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line").Text != bridge.ResolveText(ChapterPrefix + "text/dialogue-mansur-babai-name-reply")
            || bridge.ActiveSceneId != investigationScene)
        { Fail("Mansur's answer about the name was missing or reset the active investigation scene."); return; }
        mansurDialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        if (!await Advance(bridge, "boundary-source-to-reread", "evidence-tatarwiki-reread")) return;
        if (BeatState(bridge.SelectRuntimeState(), "language-reread") == "completed"
            || bridge.IsInteractionAvailable(Interaction("reread-to-edge-sketch"))
            || bridge.IsOldPcDocumentAccessible("urman.oldpc:document/doc_kara_urman_edge_sketch"))
        { Fail("Translation or reopening substituted for applying the source to the old record."); return; }
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-reread-response"), new[] { "urman.oldpc:document/rec_marat_case_register_conflict", "urman.oldpc:document/tw_shurale_urman_boundary" }))
        { Fail("The translated words could not be applied to the internal register."); return; }
        if (!await Act1SourceReturnsProof.CompleteAsync(this, bridge, accountingFirst: true, askIntentAgain: true)) return;
        if (!await Advance(bridge, "reread-to-edge-sketch", "evidence-edge-sketch")) return;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_kara_urman_edge_is_rule_boundary") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_mansur_allowed_pc_access_deliberately") != "hypothesis"
            || bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road"))
            || !await bridge.OpenDocumentAsync("urman.oldpc:document/doc_kara_urman_edge_sketch"))
        { Fail("Could not read the edge sketch."); return; }
        main.SwitchZone("village_day", "from_house");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ReferenceEquals(rinatActor.GetParent(), houseRinatHost)
            || !bridge.IsInteractionAvailable(Interaction("route-to-mosque"))
            || !await bridge.DispatchInteractionAsync(Interaction("route-to-mosque"))
            || !await bridge.EnterDialogueNodeAsync(Dialogue("timur_restraint"), "restraint")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_timur_warns_against_marat_path") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "hidden"
            || bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road"))
            || bridge.ActiveSceneId != Scene("evidence-edge-sketch"))
        { Fail("Timur's greeting granted a conclusion, lost the investigation scene, or moved Rinat prematurely."); return; }

        // A confirmed optional word still opens its own reply. It cannot replace
        // explaining the purpose of the main-route visit.
        if (bridge.RequireDialogue(Dialogue("timur_restraint")).Nodes["restraint"].Choices
                .Single(choice => choice.Id == "ask-shurale-name").NextNodeId != "shurale-reply"
            || bridge.RequireDialogue(Dialogue("timur_restraint")).Nodes["shurale-reply"].TextId != ChapterPrefix + "text/dialogue-timur-shurale-reply"
            || !bridge.EvaluateConditions(bridge.RequireDialogue(Dialogue("timur_restraint"))
                .Nodes["restraint"].Choices.Single(choice => choice.Id == "ask-shurale-name").Conditions))
        { Fail("The confirmed Шүрәле word did not open Timur's question or its reply was unwired."); return; }

        // This order prepares the evidence before the substantive conversation;
        // LEN01 exercises a doubtful conversation, save/load, reread and return.
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-purpose-landmarks"), new[]
                { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/doc_kara_urman_edge_sketch" })
            || bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road"))
            || !await bridge.ChooseDialogueAsync(Dialogue("timur_restraint"), "restraint", "tell-register")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_timur_warns_against_marat_path") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road"))
            || !await bridge.ChooseDialogueAsync(Dialogue("timur_restraint"), "boundary-reply", "discuss-route-check")
            || !await bridge.ChooseDialogueAsync(Dialogue("timur_restraint"), "route-question", "name-landmark-check")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("edge-sketch-to-zirat-road")))
        { Fail("Checking the route's purpose and discussing it with Timur did not control the main-route departure."); return; }

        var preZiratState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(preZiratState, "clue_marat_last_route_near_zirat") != "hidden")
        {
            Fail("The zirat roadside clue was granted before the player reached the zirat-road scene.");
            return;
        }
        if (!await Advance(bridge, "edge-sketch-to-zirat-road", "zirat-road")) return;
        main.SwitchZone("zirat_road", "village_side");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ReferenceEquals(rinatActor.GetParent(), people)
            || rinatActor.GlobalPosition.Z is < -88f or > -80f)
        { Fail("Rinat did not reach the visible roadside stage before the forest."); return; }
        if (!await Act1RinatRoadsideProof.ObserveAsync(this, bridge)) return;

        if (!await VerifyTagBeforeLandmarks(bridge)) return;

        if (!await Advance(bridge, "zirat-road-to-forest", "forest-approach")) return;
        main.SwitchZone("kara_urman_night", "village_path");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var approachAudio = GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi;
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden"
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") == "completed"
            || approachAudio?.LastAssetId is "urman.chapter1:asset/audio-marat-voice" or "urman.chapter1:asset/audio-rinat-interruption")
        { Fail("Optional forest approach started the finale before the endpoint."); return; }
        if (!await Act1RinatRoadsideProof.EnterFinaleAsync(this, bridge)) return;
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
        {
            if (!Act1RinatRoadsideProof.LookAtIntervention(this)) return;
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        if (audioCue.LastStartedAssetId != "urman.chapter1:asset/audio-rinat-interruption"
            || audioCue.VisibleText != "Ринат говорит: «Не отвечай»."
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "hidden"
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") == "completed")
        {
            Fail($"Rinat's warning did not start visibly before committing the physical intervention (visible='{audioCue.VisibleText}').");
            return;
        }
        for (var attempt = 0; attempt < 300 && BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") != "completed"; attempt++)
        {
            if (!Act1RinatRoadsideProof.LookAtIntervention(this)) return;
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        }
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_do_not_answer_rule") != "confirmed"
            || BeatState(bridge.SelectRuntimeState(), "cliffhanger-hard-cut") != "completed"
            || audioCue.IsPresenting
            || rinatActor.GetMeta("rinatInterventionPhase", "missing").AsString() != "visible-landed-stop-hand")
        {
            Fail("The physical stop gesture and completed warning did not commit the final rule: "
                + rinatActor.GetMeta("rinatInterventionPhase", "missing").AsString());
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
            || demo.DemoEnded || bridge.ActiveSceneId != Scene("arrival_vehicle_dusk")
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_internal_wording_reread") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_family_avoids_marat") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_heard_versions") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_intent") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_route_check_discussed") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_message_read") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_message_question_prepared") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_alsu_message_reply") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_record_shared_formulation") != "hidden"
            || BeatState(bridge.SelectRuntimeState(), "boundary-source-reopened") == "completed")
        { Fail("A second playthrough retained the completed ending."); return; }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ReferenceEquals(rinatActor.GetParent(), people)
            || rinatActor.GlobalPosition.Z < -5f || ambience.ActivePlayerIndex < 0
            || Mathf.Abs(house.GetNode<Node3D>("Act1NpcPresentation/Npc_gulsina").RotationDegrees.Y - 28f) > .1f)
        { Fail("New Game did not restore early NPC staging and ambience."); return; }
        if (Mathf.Abs(Mathf.AngleDifference(house.GetNode<Node3D>("DiscoveryFamilyPhoto").Rotation.Y,
                Mathf.DegToRad(StyleBenchmarkInteriorFactory.PhotoYawDegrees))) > .01f
            || house.GetNode<Node3D>("DiscoverySewingTin/HingedLid").Rotation.X != 0
            || repairedLight.Visible
            || restBenchSnow.Scale.X != 1f
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-zirat-outer-rest-bench") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-house-interior-photo-back") != "hidden"
            || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-fap-service-cabinet") != "hidden"
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

    private async Task<bool> VerifyTagBeforeLandmarks(RuntimeBridge bridge)
    {
        try
        {
            const string sketch = "urman.oldpc:document/doc_kara_urman_edge_sketch";
            const string marks = ChapterPrefix + "knowledge/clue_zirat_roadside_marks";
            var pair = new[] { sketch, marks };
            var scene = bridge.ActiveSceneId;
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            if (Known("clue_sketch_field_landmarks") || Known("clue_zirat_roadside_marks")
                || !await bridge.DispatchInteractionAsync(Interaction("zirat-roadside-clue")))
                throw new InvalidOperationException("The tag-first check did not begin with two unobserved sources.");
            await DenyUnobservedRoute("after reading only the two notches");
            if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-center"), pair)
                || KnowledgeStatus(bridge.SelectRuntimeState(), "hypothesis_route_enters_zirat") != "hypothesis")
                throw new InvalidOperationException("The premature route hypothesis could not be retained as a hypothesis.");
            if (!await bridge.SaveSlotAsync("chapter-tag-before-landmarks")
                || !await bridge.LoadSlotAsync("chapter-tag-before-landmarks"))
                throw new InvalidOperationException("The separately read tag could not be saved and restored.");
            for (var frame = 0; frame < 240 && player.ModalOpen; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (player.ModalOpen || bridge.ActiveSceneId != scene || bridge.CurrentZoneId != "zirat_road"
                || !Known("clue_zirat_roadside_marks")
                || bridge.JournalEntries().Count(entry => entry.EntryId == marks) != 1)
                throw new InvalidOperationException("Load lost or duplicated the roadside tag source.");
            await DenyUnobservedRoute("after loading the tag and a mistaken version");

            var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
            journal.Open(bridge, sketch);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (journal.ActiveEntryId != sketch) throw new InvalidOperationException("The real sketch could not be reread.");
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await bridge.CompareJournalSourcesAsync(Interaction("revise-inside"), new[]
                    { ChapterPrefix + "knowledge/hypothesis_route_enters_zirat", sketch })
                || KnowledgeStatus(bridge.SelectRuntimeState(), "hypothesis_route_enters_zirat") != "contradicted")
                throw new InvalidOperationException("Rereading the sketch did not allow the bounded source correction.");
            await DenyUnobservedRoute("after rereading and correcting the hypothesis");
            await Act1RouteLandmarksProof.ObserveAsync(this, bridge);
            if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), pair)
                || !Known("clue_marat_last_route_near_zirat") || !bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
                || Known("clue_sketch_tag_base_checked") || Known("clue_sketch_place_compared"))
                throw new InvalidOperationException("Actual field observation did not unlock the route independently of optional tag-base exploration.");
            GD.Print("route-source-order-proof: tag -> wrong version -> save/load -> source reread/correction -> field view -> main-route comparison");
            return true;

            bool Known(string id) => KnowledgeStatus(bridge.SelectRuntimeState(), id) == "confirmed";
            async Task DenyUnobservedRoute(string phase)
            {
                if (Known("clue_sketch_field_landmarks") || Known("clue_marat_last_route_near_zirat")
                    || bridge.IsInteractionAvailable(Interaction("zirat-road-to-forest"))
                    || bridge.JournalActions(pair).Any(action => action.Id == Interaction("compare-route-match"))
                    || await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), pair)
                    || Known("clue_marat_last_route_near_zirat"))
                    throw new InvalidOperationException("The missing physical field source was bypassed " + phase + ".");
            }
        }
        catch (Exception exception)
        {
            Fail("route-source-order-proof: " + exception);
            return false;
        }
    }

    private async Task<bool> RejectUnvisitedDoorRepeats(RuntimeBridge bridge, Main main, params string[] names)
    {
        try
        {
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var carry = (CarryCoordinator)GetTree().GetFirstNodeInGroup("carry_coordinator");
            bool ReadyForSnapshot()
            {
                if (player.ModalOpen || carry.ActionInProgress) return false;
                var state = bridge.SelectRuntimeState();
                if (!state.TryGetProperty(Urman.Core.World.CustodyStore.DefaultStateKey, out var custody)
                    || custody.ValueKind != JsonValueKind.Array) return false;
                var registered = custody.EnumerateArray().Select(item => item.GetProperty("itemId").GetString())
                    .ToHashSet(StringComparer.Ordinal);
                return carry.ItemIds.All(id => registered.Contains(id));
            }
            // New Game publishes its authored custody set on the first normal
            // runtime notification. Observe that real initialization before
            // attributing any later state change to a rejected door.
            for (var frame = 0; frame < 240 && !ReadyForSnapshot(); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!ReadyForSnapshot()) throw new InvalidOperationException("Door rejection started before modal/custody initialization completed.");
            foreach (var name in names)
            {
                var target = main.ConnectedWorld!.FindChild(name, true, false) as InteractionTarget
                    ?? throw new InvalidOperationException("Missing existing door: " + name);
                if (target.PresentationRepeat is null || target.PresentationRepeatAvailable is null)
                    throw new InvalidOperationException("Missing repeat owner for existing door: " + name);
                var zone = bridge.CurrentZoneId;
                var before = bridge.SelectRuntimeState();
                if (target.PresentationRepeatAvailable())
                    throw new InvalidOperationException("An unearned door exposed its local repeat: " + name);
                target.PresentationRepeat();
                for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var after = bridge.SelectRuntimeState();
                if (bridge.CurrentZoneId != zone || after.GetRawText() != before.GetRawText())
                {
                    var changed = before.EnumerateObject().Select(field => field.Name)
                        .Concat(after.EnumerateObject().Select(field => field.Name)).Distinct()
                        .Where(key => !before.TryGetProperty(key, out var oldValue)
                            || !after.TryGetProperty(key, out var newValue) || oldValue.GetRawText() != newValue.GetRawText());
                    throw new InvalidOperationException($"A rejected local door changed state: {name}; zone={zone}->{bridge.CurrentZoneId}; fields={string.Join(", ", changed)}");
                }
            }
            GD.Print("investigation-door-proof: unearned repeat denied without scene or knowledge change: " + string.Join(", ", names));
            return true;
        }
        catch (Exception exception)
        {
            Fail("investigation-door-proof: " + exception);
            return false;
        }
    }

    private async Task<bool> VerifyFapReturnBeforeQuestion(RuntimeBridge bridge, Main main)
    {
        try
        {
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            await WaitForZone("fap_clinic");
            var scene = bridge.ActiveSceneId;
            if (scene != Scene("fap_waiting_room_day") || bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
                throw new InvalidOperationException("The refusal check must start at the first FAP visit before Naila grants access.");
            var before = bridge.SelectRuntimeState().GetRawText();
            await UseReturn("OfficialRecordExitToStreet", "village_day");
            if (!await bridge.SaveSlotAsync("chapter-fap-before-question")
                || !await bridge.LoadSlotAsync("chapter-fap-before-question"))
                throw new InvalidOperationException("An unfinished FAP visit could not be saved and restored outside.");
            await WaitForZone("village_day");
            RequireUnchanged("after restoring the unasked visit");
            await UseReturn("RoadToFap", "fap_clinic");
            RequireUnchanged("after returning to Naila");
            GD.Print("investigation-door-proof: first FAP refusal -> street save/load -> FAP; scene, evidence and absent permission preserved");
            return true;

            void RequireUnchanged(string phase)
            {
                var nowScene = bridge.ActiveSceneId;
                var nowState = bridge.SelectRuntimeState();
                var nowText = nowState.GetRawText();
                var desk = bridge.IsInteractionAvailable(Interaction("fap-to-document-desk"));
                var marker = KnowledgeStatus(nowState, "clue_marat_case_boundary_marker");
                if (nowScene != scene
                    || !RuntimeStateComparison.SameIgnoringNpcFacing(nowText, before)
                    || desk
                    || marker != "hidden")
                {
                    var changedFields = "?";
                    try
                    {
                        using var beforeDoc = System.Text.Json.JsonDocument.Parse(before);
                        var names = new System.Collections.Generic.HashSet<string>();
                        foreach (var field in beforeDoc.RootElement.EnumerateObject()) names.Add(field.Name);
                        foreach (var field in nowState.EnumerateObject()) names.Add(field.Name);
                        var diffs = new System.Collections.Generic.List<string>();
                        foreach (var key in names)
                        {
                            var hasBefore = beforeDoc.RootElement.TryGetProperty(key, out var oldValue);
                            var hasNow = nowState.TryGetProperty(key, out var newValue);
                            if (!hasBefore || !hasNow || oldValue.GetRawText() != newValue.GetRawText()) diffs.Add(key);
                        }
                        changedFields = string.Join(",", diffs);
                        if (beforeDoc.RootElement.TryGetProperty("world.props", out var oldProps)
                            && nowState.TryGetProperty("world.props", out var newProps)
                            && oldProps.GetRawText() != newProps.GetRawText())
                            changedFields += $" props-before={oldProps.GetRawText()} props-after={newProps.GetRawText()}";
                    }
                    catch (System.Exception parseError) { changedFields = "parse:" + parseError.GetType().Name; }
                    GD.Print($"door-proof-diff {phase}: scene={scene}->{nowScene} desk={desk} marker={marker} sameText={nowText == before} fields=[{changedFields}]");
                    throw new InvalidOperationException("A local door invented progress " + phase + ".");
                }
            }
            async Task UseReturn(string name, string zone)
            {
                var target = main.ConnectedWorld!.FindChild(name, true, false) as InteractionTarget;
                if (target?.PresentationRepeatAvailable?.Invoke() != true || !target.IsAvailable())
                    throw new InvalidOperationException("The earned FAP return is unavailable: " + name);
                target.Interact();
                await WaitForZone(zone);
                RequireUnchanged("after " + name);
            }
            async Task WaitForZone(string zone)
            {
                for (var frame = 0; frame < 240 && (bridge.CurrentZoneId != zone || player.ModalOpen); frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (bridge.CurrentZoneId != zone || player.ModalOpen)
                    throw new InvalidOperationException("The actual return did not finish in " + zone + ".");
            }
        }
        catch (Exception exception)
        {
            Fail("investigation-door-proof: " + exception);
            return false;
        }
    }

    private async Task<bool> VerifyPhotoReturn(RuntimeBridge bridge, Main main)
    {
        try
        {
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
            var scene = bridge.ActiveSceneId;
            for (var frame = 0; frame < 240 && (bridge.CurrentZoneId != "village_day" || player.ModalOpen); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (bridge.CurrentZoneId != "village_day" || player.ModalOpen)
                throw new InvalidOperationException("The photo return started before the actual street transition completed.");
            if (!Known("clue_first_snow_source_read") || Known("clue_photo_facade_windows_observed")
                || Known("clue_photo_yard_observed") || Known("clue_photo_gate_repair_checked")
                || bridge.IsInteractionAvailable(Interaction("inspect-photo-gate-repair")))
                throw new InvalidOperationException("Photo return requires the actual read photo and an unchecked yard.");
            await Observe("observe-photo-facade-windows", "clue_photo_facade_windows_observed");
            RequireWindowsOnly("after the separate facade observation");
            var windowsId = ChapterPrefix + "knowledge/clue_photo_facade_windows_observed";
            journal.Open(bridge, windowsId);
            for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var windowsBody = journal.GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
            if (journal.ActiveEntryId != windowsId || !windowsBody.IsVisibleInTree()
                || !windowsBody.Text.Contains("Теперь сверю вход во двор", StringComparison.Ordinal))
                throw new InvalidOperationException("The actual windows journal entry has no visible next observation hint.");
            CloseJournal();
            for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await bridge.SaveSlotAsync("chapter-photo-windows-only")
                || !await bridge.LoadSlotAsync("chapter-photo-windows-only"))
                throw new InvalidOperationException("The unfinished photo observation could not save and return.");
            for (var frame = 0; frame < 240 && (bridge.CurrentZoneId != "village_day" || player.ModalOpen); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (bridge.ActiveSceneId != scene || bridge.CurrentZoneId != "village_day" || player.ModalOpen)
                throw new InvalidOperationException("The unfinished photo observation did not return to the actual street.");
            RequireWindowsOnly("after restoring the unfinished observation");
            if (bridge.IsInteractionAvailable(Interaction("observe-photo-facade-windows"))
                || await bridge.DispatchInteractionAsync(Interaction("observe-photo-facade-windows")))
                throw new InvalidOperationException("Restoring the windows source allowed a second result.");
            RequireWindowsOnly("after refusing the repeated facade observation");
            await Observe("observe-photo-yard", "clue_photo_yard_observed");
            if (Known("clue_photo_place_compared") || Known("clue_photo_question_answered"))
                throw new InvalidOperationException("Looking at the yard silently matched the photograph or invented Mansur's reply.");
            await Compare("compare-photo-neighbor", ChapterPrefix + "knowledge/discovery-main-street-side-window");
            if (Known("clue_photo_place_compared") || bridge.IsInteractionAvailable(Interaction("inspect-photo-gate-repair")))
                throw new InvalidOperationException("A neighboring window substituted for the photograph's actual yard.");
            await Compare("compare-photo-yard", ChapterPrefix + "knowledge/clue_photo_yard_observed");
            if (!Known("clue_photo_place_compared") || Known("clue_photo_gate_repair_checked"))
                throw new InvalidOperationException("The photo comparison skipped the separate physical repair inspection.");
            await Observe("inspect-photo-gate-repair", "clue_photo_gate_repair_checked");
            await Revisit("ReturnToHouseRegister", "house_old_pc");
            if (!await Act1LocalReactionProof.CompleteAsync(this, bridge, "photo")) return false;
            await Revisit("HouseExit", "village_day");
            if (bridge.ActiveSceneId != scene || !Known("clue_photo_question_answered")
                || Known("clue_route_check_intent") || Known("clue_marat_last_route_near_zirat"))
                throw new InvalidOperationException("The local photo return rewound the main scene or invented the forest route.");
            GD.Print("photo-return-proof: actual facade windows; look-away refused; visible next lead; unfinished save restored; actual gate and birch; wrong photo pair; explicit match; separate repair; visible Mansur reply");
            return true;

            bool Known(string id) => KnowledgeStatus(bridge.SelectRuntimeState(), id) == "confirmed";
            void RequireWindowsOnly(string phase)
            {
                if (!Known("clue_photo_facade_windows_observed")
                    || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/clue_photo_facade_windows_observed") != 1
                    || new[] { "clue_photo_yard_observed", "clue_photo_place_compared", "clue_photo_gate_repair_checked",
                        "clue_photo_question_answered", "clue_route_check_intent", "clue_marat_last_route_near_zirat" }.Any(Known)
                    || bridge.IsInteractionAvailable(Interaction("inspect-photo-gate-repair")))
                    throw new InvalidOperationException("Raw windows were lost, duplicated or interpreted as later knowledge " + phase + ".");
            }
            async Task Observe(string action, string knowledge)
            {
                var target = main.ConnectedWorld!.FindChild("Observation_" + action, true, false) as InteractionTarget
                    ?? throw new InvalidOperationException("Photo source has no actual observation target: " + action);
                var original = player.CapturePortableTransform();
                try
                {
                    var reference = target.GetMeta("observationReferenceEye").AsVector3();
                    using var groundRay = PhysicsRayQueryParameters3D.Create(reference + Vector3.Up * 3f,
                        reference + Vector3.Down * 8f, 1u, new global::Godot.Collections.Array<Rid> { player.GetRid() });
                    var ground = player.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
                    if (ground.Count == 0 || ground["normal"].AsVector3().Dot(Vector3.Up) <= .7f)
                        throw new InvalidOperationException("Photo reference has no walkable support: " + action + " reference=" + reference);
                    var feet = ground["position"].AsVector3() + Vector3.Up * .05f;
                    if (!player.CanStandAt(feet))
                        throw new InvalidOperationException("Photo reference cannot fit a standing capsule: " + action + " feet=" + feet
                            + " support=" + (ground["collider"].AsGodotObject() as Node)?.GetPath());
                    player.ApplyZoneSpawn(feet, 0f);
                    for (var frame = 0; frame < 12; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (!player.IsOnFloor() || player.IsCrouching || player.ModalOpen || !player.CanStandAt(player.GlobalPosition))
                        throw new InvalidOperationException("Photo reference is not a real standing view: " + action
                            + " feet=" + player.GlobalPosition + " floor=" + player.IsOnFloor() + " modal=" + player.ModalOpen);
                    var camera = player.GetNode<Camera3D>("Head/Camera3D");
                    var point = target.GetMeta("observationLookAt").AsVector3();
                    var delta = point - camera.GlobalPosition;
                    var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length()));
                    var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
                    player.ApplySmokeLook(pitch, yaw + 180f);
                    if (target.IsAvailable() || await bridge.DispatchInteractionAsync(Interaction(action)) || Known(knowledge))
                        throw new InvalidOperationException("Looking away still completed the physical photo observation: " + action);
                    player.ApplySmokeLook(pitch, yaw);
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    var ray = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
                    // A human counts the row with a slight turn from the box
                    // aim: the corner eye that fits a standing player cannot
                    // frame all three windows while staring at the end one.
                    // Sweep a small deterministic yaw range for an aim where
                    // the ray still hits the box AND the live guard passes.
                    var sweptYaw = yaw;
                    var swept = false;
                    for (var step = 0; step <= 30 && !swept; step++)
                    {
                        foreach (var offset in step == 0 ? new[] { 0f } : new[] { step * .5f, -step * .5f })
                        {
                            player.ApplySmokeLook(pitch, yaw + offset);
                            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                            ray.ForceRaycastUpdate();
                            var sweepHit = ray.IsColliding() ? ray.GetCollider() as Node : null;
                            if (sweepHit == target && main.ConnectedWorld!.CanUseObservationInteraction(Interaction(action)))
                            { sweptYaw = yaw + offset; swept = true; break; }
                        }
                    }
                    GD.Print("photo-source-sweep action=" + action + " baseYaw=" + yaw + " sweptYaw=" + sweptYaw + " swept=" + swept);
                    player.ApplySmokeLook(pitch, sweptYaw);
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    ray.ForceRaycastUpdate();
                    ray.ForceRaycastUpdate();
                    var hit = ray.IsColliding() ? ray.GetCollider() as Node : null;
                    var semantic = bridge.IsInteractionAvailable(Interaction(action));
                    var liveView = main.ConnectedWorld!.CanUseObservationInteraction(Interaction(action));
                    var available = target.IsAvailable();
                    GD.Print("photo-source-view action=" + action + " feet=" + player.GlobalPosition + " eye=" + camera.GlobalPosition
                        + " target=" + point + " normal-ray=" + hit?.GetPath() + " hit=" + ray.GetCollisionPoint()
                        + " semantic=" + semantic + " liveView=" + liveView + " available=" + available
                        + " activeZone=" + main.ConnectedWorld!.ActiveZoneId + " scene=" + bridge.ActiveSceneId);
                    if (!available || hit != target)
                    {
                        DescribePhotoView(target, camera);
                        await CapturePhotoView(action, blocked: true);
                        throw new InvalidOperationException("The actual photo observation is unavailable: " + action
                            + " semantic=" + semantic + " liveView=" + liveView + " targetAvailable=" + available
                            + " normalRayHitTarget=" + (hit == target));
                    }
                    await CapturePhotoView(action, blocked: false);
                    target.Interact();
                    for (var frame = 0; frame < 180 && !Known(knowledge); frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (!Known(knowledge) || bridge.ActiveSceneId != scene)
                        throw new InvalidOperationException("The actual photo observation did not retain its local source: " + action);
                    var sourceId = ChapterPrefix + "knowledge/" + knowledge;
                    for (var frame = 0; frame < 180 && (!journal.GetNode<Control>("Screen").Visible
                            || journal.ActiveEntryId != sourceId); frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (!journal.GetNode<Control>("Screen").Visible || journal.ActiveEntryId != sourceId)
                        throw new InvalidOperationException("The observed photo source never reached its actual reader: " + action);
                    CloseJournal();
                    for (var frame = 0; frame < 180 && player.ModalOpen; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (player.ModalOpen) throw new InvalidOperationException("The photo source retained input after closing: " + action);
                }
                finally { player.ApplyPortableTransform(original); }
            }
            async Task CapturePhotoView(string action, bool blocked)
            {
                var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
                if (string.IsNullOrEmpty(output) || DisplayServer.GetName() == "headless")
                {
                    GD.Print("photo-source-frame: " + action + "; capture=not-run; no image output or rendering device");
                    return;
                }
                for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var picture = GetViewport().GetTexture().GetImage();
                System.IO.Directory.CreateDirectory(output);
                var path = System.IO.Path.Combine(output, "photo_source_" + (blocked ? "blocked_" : "ready_") + action + ".png");
                if (System.IO.File.Exists(path) || picture.IsEmpty() || picture.SavePng(path) != Error.Ok)
                    throw new InvalidOperationException("The photo guard diagnostic could not capture a fresh real view: " + path);
                GD.Print("photo-source-frame: " + action + "; blocked=" + blocked + "; actual=" + path);
            }
            async Task Compare(string action, string second)
            {
                journal.Open(bridge);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
                var sources = new[] { FirstSnowPhoto, second };
                for (var slot = 0; slot < 2; slot++)
                {
                    var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
                    var index = Enumerable.Range(1, picker.ItemCount - 1).FirstOrDefault(i => picker.GetItemMetadata(i).AsString() == sources[slot], -1);
                    if (index < 1) throw new InvalidOperationException("The real photo source is absent from the journal picker: " + sources[slot]);
                    picker.Select(index);
                    picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
                }
                for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var button = journal.GetNode<VBoxContainer>("Screen/Book/Layout/Comparisons/Layout/Hypotheses").GetChildren()
                    .OfType<Button>().SingleOrDefault(candidate => !candidate.Disabled && candidate.IsVisibleInTree()
                        && candidate.Text == bridge.ResolveText(ChapterPrefix + "text/" + action));
                if (button is null) throw new InvalidOperationException("The authored photo comparison is absent from the actual journal: " + action);
                button.EmitSignal(Button.SignalName.Pressed);
                for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                CloseJournal();
                for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            void DescribePhotoView(InteractionTarget target, Camera3D camera)
            {
                var facade = main.ConnectedWorld!.GetNode<Node3D>(
                    "Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/BabaiApproachDwellingFacade");
                var basis = facade.GlobalTransform.Basis.Z;
                var front = new Vector3(basis.X, 0, basis.Z).Normalized();
                if (target.InteractionId == Interaction("observe-photo-yard"))
                {
                    var gateCenter = target.GlobalPosition - front * .40f;
                    var offset = camera.GlobalPosition - gateCenter;
                    offset.Y = 0;
                    GD.Print("photo-view-range offset=" + offset.Length() + " required=(1.3,7.2); front="
                        + offset.Dot(front) + " required>1.1; referenceEye="
                        + target.GetMeta("observationReferenceEye").AsVector3() + " actualEye=" + camera.GlobalPosition);
                }
                var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                foreach (var proxy in main.FindChildren("*", "", true, false).OfType<InteractionTarget>())
                    if (!proxy.IsQueuedForDeletion() && !exclusions.Contains(proxy.GetRid())) exclusions.Add(proxy.GetRid());
                var viewport = camera.GetViewport().GetVisibleRect().Size;
                var aspect = viewport.X / Math.Max(viewport.Y, 1f);
                var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * .5f);
                var halfWidth = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent : tangent * aspect;
                var halfHeight = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent / aspect : tangent;
                var points = target.HasMeta("observationLandmarks")
                    ? target.GetMeta("observationLandmarks").AsGodotArray()
                    : new global::Godot.Collections.Array { target.GetMeta("observationLookAt") };
                var index = 0;
                foreach (var value in points)
                {
                    var landmark = value.AsVector3();
                    var local = camera.ToLocal(landmark);
                    var x = local.X / -local.Z;
                    var y = local.Y / -local.Z;
                    var framed = local.Z < -.05f && Math.Abs(x) <= halfWidth && Math.Abs(y) <= halfHeight;
                    using var trace = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, landmark, 3u, exclusions);
                    var obstruction = player.GetWorld3D().DirectSpaceState.IntersectRay(trace);
                    var distance = obstruction.Count == 0 ? 0f : obstruction["position"].AsVector3().DistanceTo(landmark);
                    var visible = obstruction.Count == 0 || distance < .30f;
                    GD.Print("photo-view-landmark index=" + index++ + " world=" + landmark + " local=" + local
                        + " ratios=" + new Vector2(x, y) + " limits=" + new Vector2(halfWidth, halfHeight)
                        + " framed=" + framed + " nativeFrustum=" + camera.IsPositionInFrustum(landmark)
                        + " visible=" + visible + " occluder="
                        + (obstruction.Count == 0 ? "none" : (obstruction["collider"].AsGodotObject() as Node)?.GetPath().ToString())
                        + " hitToLandmark=" + distance + " fov=" + camera.Fov + " keepAspect=" + camera.KeepAspect
                        + " viewport=" + viewport);
                }
            }
            void CloseJournal()
            {
                if (journal.GetNode<Control>("Screen").Visible)
                    journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            }
            async Task Revisit(string name, string zone)
            {
                var target = main.ConnectedWorld!.FindChild(name, true, false) as InteractionTarget;
                for (var frame = 0; frame < 180 && player.ModalOpen; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (target is null) throw new InvalidOperationException("Missing existing return target: " + name);
                GD.Print("photo-return-door: before target=" + name + " scene=" + bridge.ActiveSceneId
                    + " zone=" + bridge.CurrentZoneId + " worldZone=" + main.ConnectedWorld.ActiveZoneId
                    + " modal=" + player.ModalOpen + " original=" + bridge.IsInteractionAvailable(target.InteractionId)
                    + " repeat=" + target.PresentationRepeatAvailable?.Invoke() + " available=" + target.IsAvailable());
                if (player.ModalOpen || target.PresentationRepeatAvailable?.Invoke() != true || !target.IsAvailable())
                    throw new InvalidOperationException("The earned local return is unavailable: " + name);
                target.Interact();
                for (var frame = 0; frame < 240 && (bridge.CurrentZoneId != zone || player.ModalOpen); frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (bridge.CurrentZoneId != zone || player.ModalOpen || bridge.ActiveSceneId != scene)
                    throw new InvalidOperationException("The local return did not finish: " + name + "; scene=" + scene + "->" + bridge.ActiveSceneId
                        + "; expectedZone=" + zone + "; actualZone=" + bridge.CurrentZoneId + "; modal=" + player.ModalOpen
                        + "; original=" + bridge.IsInteractionAvailable(target.InteractionId) + "; repeat=" + target.PresentationRepeatAvailable?.Invoke());
                GD.Print("photo-return-door: after target=" + name + " scene=" + bridge.ActiveSceneId + " zone=" + bridge.CurrentZoneId + " modal=" + player.ModalOpen);
            }
        }
        catch (Exception exception)
        {
            Fail("photo-return-proof: " + exception);
            return false;
        }
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
        if (scene is null)
        {
            Fail("A discovery requires an active authored scene: " + slug);
            return false;
        }
        var main = (Main)GetTree().GetFirstNodeInGroup("zone_manager");
        var target = main.ConnectedWorld!.FindChild("Discovery_" + slug, true, false) as InteractionTarget;
        if (target is null) { Fail("Discovery has no physical target: " + slug); return false; }
        var observation = slug switch
        {
            "arrival-insulated-well" => "observe-well-tie",
            "zirat-outer-rest-bench" => "observe-bench-plank",
            "kara-branch-profile" => "observe-branch-profile",
            _ => null
        };
        if (observation is not null)
        {
            // One supported local start; every change of observation angle and
            // retreat below uses ordinary walking. Branches reload real saves.
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            var original = player.CapturePortableTransform();
            try
            {
                var reference = target.GetMeta("observationReferenceEye").AsVector3();
                var question = main.ConnectedWorld.FindChild("Observation_" + observation, true, false) as InteractionTarget;
                if (question is null) { Fail("Missing observation question: " + observation); return false; }
                // The mitten question starts on the open street side. Its
                // conclusion requires a second view behind the fastening;
                // using that rear reference for both steps aims through the well.
                var questionReference = slug switch
                {
                    "arrival-insulated-well" => question.GlobalPosition + Vector3.Right * 1.55f,
                    "zirat-outer-rest-bench" => reference + Vector3.Back * .60f,
                    // The retained FallenLog_K1 trunk spans X 1.50-3.69 at
                    // Z -116.92..-116.13 (kit bounds); the default Back*2.05
                    // stance at X 3.87 overlaps its east end with the player
                    // capsule, and the ground just north stays lumpy. Stand
                    // east of the trunk and south of the mound instead.
                    "kara-branch-profile" => reference + Vector3.Back * .75f + Vector3.Right * .6f,
                    _ => reference + Vector3.Back * 2.05f
                };
                if (!await PlaceStanding(questionReference)) return false;
                var dialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
                var shortName = slug == "arrival-insulated-well" ? "well"
                    : slug == "zirat-outer-rest-bench" ? "bench" : "profile";
                var observedId = shortName == "profile" ? "observation-branch-overlap" : "observation-" + (shortName == "well" ? "well-tie" : "bench-plank");
                var initialSlot = "ex11-" + shortName + "-unobserved";
                var unfinishedSlot = "ex11-" + shortName + "-unfinished";
                if (!await SaveExploration(bridge, initialSlot) || !await OpenQuestion()) return false;
                var observedKnowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
                if (!await Act1LocalReactionProof.ChooseVisibleAsync(this, bridge, shortName + "-wrong-attempt")
                    || !await Act1LocalReactionProof.VerifyVisibleReplyAsync(this, bridge,
                        "ex11-" + shortName + (shortName == "bench" ? "-pry-refusal" : "-front-refusal"))) return false;
                if (!Unfinished() || observedKnowledge != bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText())
                { Fail("Wrong observation choice supplied an unearned conclusion: " + slug); return false; }
                await CaptureExplorationView("ex11_" + shortName + "_wrong");
                if (!await Act1LocalReactionProof.ChooseVisibleAsync(this, bridge, "observation-check-in-world")) return false;
                if (dialogue.IsOpen || !Unfinished() || !await bridge.LoadSlotAsync(initialSlot))
                { Fail("The wrong-attempt branch could not return to its real pre-observation save: " + slug); return false; }
                await ExplorationFrames(8);
                if (!await OpenQuestion()
                    || !await Act1LocalReactionProof.ChooseVisibleAsync(this, bridge, "observation-leave")) return false;
                if (dialogue.IsOpen || !Unfinished() || !await bridge.SaveSlotAsync(unfinishedSlot))
                { Fail("Leaving the question completed it or prevented an unfinished save: " + slug); return false; }
                // The kara ground north of the profile start rises into the
                // capsule (terrain mound by the deadfall); step back toward
                // the eye side instead of further north.
                var retreat = shortName == "profile" ? questionReference - Vector3.Back * .65f
                    : shortName == "well" ? questionReference + Vector3.Right * .65f
                    : questionReference + Vector3.Back * .65f;
                if (!await WalkExplorationLeg(bridge, player, retreat, shortName + " leave unanswered question")) return false;
                if (!Unfinished() || !await bridge.LoadSlotAsync(unfinishedSlot))
                { Fail("An unfinished observation was lost on return: " + slug); return false; }
                await ExplorationFrames(8);
                if (!Unfinished() || question.IsAvailable())
                { Fail("Load lost the recorded question or repeated its one-shot source: " + slug); return false; }
                var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
                journal.Open(bridge, ChapterPrefix + "knowledge/" + observedId);
                await ExplorationFrames(3);
                if (journal.ActiveEntryId != ChapterPrefix + "knowledge/" + observedId
                    || !journal.GetNode<Control>("Screen").Visible || !Unfinished())
                { Fail("The saved unfinished question is not rereadable: " + slug); return false; }
                await CaptureExplorationView("ex11_" + shortName + "_unfinished_return");
                journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await ExplorationFrames(3);
                // The authored Niva parking overlaps the well's east stance at
                // reference Z (hull tail corner). Stand 0.6 m further out on the
                // same open side; the rear-fastening view is taken from `reference`.
                var wellOpenSide = new Vector3(questionReference.X, reference.Y, reference.Z + .6f);
                if (shortName == "well"
                    && !await WalkExplorationLeg(bridge, player, wellOpenSide,
                        "well walk around the open side before viewing the rear fastening")) return false;
                if (!await WalkExplorationLeg(bridge, player, reference, shortName + " change the actual observation angle")) return false;
                AimObservation(target.GetMeta("observationLookAt").AsVector3());
                await ExplorationFrames(3);
                if (!target.IsAvailable() || target.CollisionLayer != 4u
                    || !ExplorationRayHits(player, target))
                {
                    Fail("Observed discovery has no actual manual ray after walking: " + slug + "; "
                        + ViewDetails(target, target.GetMeta("observationLookAt").AsVector3()));
                    return false;
                }
                await PressExplorationInteract();
                // The kara branch profile stays ambiguous by design: its
                // authored result is a hypothesis (plus the tt_shurale word),
                // not a confirmation. Every other manual observation confirms.
                var expectedDiscoveryStatus = slug == "kara-branch-profile" ? "hypothesis" : "confirmed";
                for (var frame = 0; frame < 180 && KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-" + slug) != expectedDiscoveryStatus; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (bridge.ActiveSceneId != scene || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-" + slug) != expectedDiscoveryStatus
                    || bridge.IsInteractionAvailable(target.InteractionId)
                    || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-" + slug) != 1)
                { Fail("Manual observation did not record exactly one grounded result: " + slug); return false; }
                if (!string.IsNullOrEmpty(target.JournalEntryId))
                {
                    for (var frame = 0; frame < 180 && !journal.GetNode<Control>("Screen").Visible; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (!journal.GetNode<Control>("Screen").Visible || journal.ActiveEntryId != target.JournalEntryId)
                    { Fail("Manual observation did not show its authored journal source: " + slug); return false; }
                }
                await CaptureExplorationView("ex11_" + shortName + "_checked");
                if (journal.GetNode<Control>("Screen").Visible)
                    journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
                await ExplorationFrames(3);
                if (!await bridge.SaveSlotAsync("ex11-" + shortName + "-checked")
                    || !await bridge.LoadSlotAsync("ex11-" + shortName + "-checked"))
                { Fail("Checked observation failed save/load: " + slug); return false; }
                await ExplorationFrames(8);
                if (await bridge.DispatchInteractionAsync(target.InteractionId)
                    || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-" + slug) != 1)
                { Fail("Loaded observation duplicated its result: " + slug); return false; }
                if (shortName == "well"
                    && !await WalkExplorationLeg(bridge, player, wellOpenSide, "well return around the same open side")) return false;
                if (!await WalkExplorationLeg(bridge, player, questionReference, shortName + " return from checked observation")) return false;
                GD.Print("ex11-observation-proof: " + shortName + "; wrong/leave/unfinished-load/manual-check/checked-load/physical-return passed; human interest not measured");
                return true;

                bool Unfinished() => bridge.ActiveSceneId == scene
                    && KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-" + slug) == "hidden"
                    && KnowledgeStatus(bridge.SelectRuntimeState(), observedId) == "confirmed"
                    && bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/" + observedId) == 1;

                async Task<bool> OpenQuestion()
                {
                    AimObservation(question.GlobalPosition);
                    await ExplorationFrames(3);
                    if (!question.IsAvailable() || !ExplorationRayHits(player, question))
                    { Fail("Actual observation question ray is unavailable: " + observation + "; " + ViewDetails(question, question.GlobalPosition)); return false; }
                    await PressExplorationInteract();
                    for (var frame = 0; frame < 180 && !dialogue.IsOpen; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (dialogue.IsOpen && Unfinished()) return true;
                    Fail("The actual question did not open without completing its answer: " + observation);
                    return false;
                }

                async Task<bool> PlaceStanding(Vector3 point)
                {
                    using var groundRay = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * 3f,
                        point + Vector3.Down * 8f, player.CollisionMask,
                        new global::Godot.Collections.Array<Rid> { player.GetRid() });
                    var ground = player.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
                    if (ground.Count == 0 || ground["normal"].AsVector3().Dot(Vector3.Up) <= .7f)
                    { Fail("Observation has no standing collision surface: " + slug + " at=" + point); return false; }
                    var feet = ground["position"].AsVector3() + Vector3.Up * .05f;
                    if (!player.CanStandAt(feet))
                    {
                        var space = player.GetWorld3D().DirectSpaceState;
                        using var shape = new CapsuleShape3D { Radius = player.BodyRadius, Height = player.StandingBodyHeight - .015f };
                        using var query = new PhysicsShapeQueryParameters3D { Shape = shape, CollisionMask = player.CollisionMask,
                            Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() }, Margin = .002f };
                        query.Transform = new(Basis.Identity, feet + Vector3.Up * (player.StandingBodyHeight * .5f + .01f));
                        var blockers = space.IntersectShape(query, 8).Select(hitShape =>
                        {
                            var collider = hitShape["collider"].AsGodotObject() as CollisionObject3D;
                            var owner = collider?.ShapeOwnerGetOwner(collider.ShapeFindOwner(hitShape["shape"].AsInt32())) as Node3D;
                            return $"{collider?.GetPath()} shape={hitShape["shape"].AsInt32()} owner={owner?.GetPath()} at={owner?.GlobalPosition} geo={(owner?.HasMeta("geometryOwner") == true ? owner.GetMeta("geometryOwner").AsString() : "-")}";
                        }).ToArray();
                        Fail($"Observation start is not a clear standing capsule: {slug}; at={point} feet={feet} blockers=[{string.Join(" | ", blockers)}]"); return false;
                    }
                    player.ApplyZoneSpawn(feet, 0f);
                    for (var frame = 0; frame < 12; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (!player.IsOnFloor() || player.IsCrouching)
                    { Fail("Observation cannot fit a standing player: " + slug + " feet=" + player.GlobalPosition); return false; }
                    return true;
                }

                void AimObservation(Vector3 point)
                {
                    AimExploration(player, point);
                }

                string ViewDetails(InteractionTarget subject, Vector3 point)
                {
                    var camera = player.GetNode<Camera3D>("Head/Camera3D");
                    var delta = point - camera.GlobalPosition;
                    var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
                    foreach (var proxy in main.ConnectedWorld.FindChildren("*", "", true, false).OfType<InteractionTarget>())
                        exclusions.Add(proxy.GetRid());
                    using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3u, exclusions);
                    var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                    var collider = hit.Count == 0 ? "clear" : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString();
                    var hitDetail = hit.Count == 0 ? "" : " shape=" + hit["shape"].AsInt32()
                        + " hit=" + hit["position"].AsVector3();
                    return "semantic=" + bridge.IsInteractionAvailable(subject.InteractionId)
                        + " eye=" + camera.GlobalPosition + " point=" + point + " reach=" + delta.Length()
                        + " facing=" + (-camera.GlobalTransform.Basis.Z).Normalized().Dot(delta.Normalized())
                        + " occluder=" + collider + hitDetail;
                }
            }
            finally
            {
                player.ApplyPortableTransform(original);
            }
        }
        if (slug == "house-exterior-porch-nook")
            return await DiscoverPorchNook(bridge, main, target, scene);
        if (slug is "house-interior-photo-back" or "house-interior-language-tin"
            or "fap-interior-height-marks" or "fap-interior-repaired-desk-object" or "fap-service-cabinet")
            return await DiscoverInterior(bridge, main, target, scene, slug);
        if (target.CollisionLayer != 4u) { Fail("Discovery selection volume blocks player physics: " + slug); return false; }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var side = slug == "fap-exterior-service-path" ? -1f : 1f;
        var eye = target.GlobalPosition + new Vector3(0, .6f, side * 1.5f);
        GD.Print($"discovery-physical-ray slug={slug} at={target.GlobalPosition} eye={eye}");
        var hit = target.GetWorld3D().DirectSpaceState.IntersectRay(
            PhysicsRayQueryParameters3D.Create(eye, target.GlobalPosition, 5));
        if (hit.Count == 0 || hit["collider"].AsGodotObject() != target)
        {
            var hitDescription = hit.Count == 0 ? "no-hit" :
                "occluder=" + (hit["collider"].AsGodotObject() as Node)?.GetPath()
                + " shape=" + hit["shape"].AsInt32() + " hit=" + hit["position"].AsVector3();
            Fail("Discovery ray is occluded or misses its authored target: " + slug
                + "; " + hitDescription + "; target=" + target.GlobalPosition + "; eye=" + eye);
            return false;
        }
        if (!await bridge.DispatchInteractionAsync(Interaction("discover-" + slug))
            || bridge.ActiveSceneId != scene
            || bridge.IsInteractionAvailable(Interaction("discover-" + slug))
            || !bridge.JournalEntries().Any(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-" + slug))
        { Fail("Optional discovery failed its single-use journal/world contract: " + slug); return false; }
        return true;
    }

    private Task<bool> DiscoverPorchNook(RuntimeBridge bridge, Main main, InteractionTarget target, string scene)
    {
        var crate = main.ConnectedWorld!.GetNode<Node3D>(target.GetMeta("activePropPath").AsString());
        // Both fixed spots lie on the existing walkthrough's house approach
        // between (-26.05, .20) and (-26.05, 2.60). They clear the real bucket
        // at (-26.60, .20); the former facade-relative points overlapped it.
        // Looking down from the path reaches above the bucket, beside the shovel.
        // This proves a standing local view; the walkthrough owns travel here.
        var approaches = new[]
        {
            new Vector3(-26.05f, crate.GlobalPosition.Y, .20f),
            new Vector3(-26.05f, crate.GlobalPosition.Y, .70f)
        };
        return DiscoverFromStandingViews(bridge, target, scene, "house-exterior-porch-nook", approaches);
    }

    private async Task ExplorationFrames(int count)
    {
        for (var frame = 0; frame < count; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task PressExplorationInteract()
    {
        try { Input.ActionPress("interact"); await ExplorationFrames(2); }
        finally { Input.ActionRelease("interact"); }
        await ExplorationFrames(3);
    }

    private async Task<bool> SaveExploration(RuntimeBridge bridge, string slot)
    {
        if (await bridge.SaveSlotAsync(slot)) return true;
        Fail("Cannot save the actually reached exploration state: " + slot);
        return false;
    }

    private async Task<bool> LoadExploration(RuntimeBridge bridge, string slot)
    {
        if (await bridge.LoadSlotAsync(slot)) return true;
        Fail("Cannot load the actually reached exploration state: " + slot);
        return false;
    }

    private static void AimExploration(FirstPersonController player, Vector3 point)
    {
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var delta = point - camera.GlobalPosition;
            player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        }
    }

    private static bool ExplorationRayHits(FirstPersonController player, InteractionTarget target)
    {
        var ray = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        ray.ForceRaycastUpdate();
        return ray.IsColliding() && ray.GetCollider() == target;
    }

    private async Task<bool> WalkExplorationLeg(RuntimeBridge bridge, FirstPersonController player,
        Vector3 requested, string phase, Node3D? requiredFloor = null)
    {
        if (player.ModalOpen || player.IsCrouching || !player.IsOnFloor())
        { Fail("Exploration walk requires ordinary standing controls: " + phase); return false; }
        using var ray = PhysicsRayQueryParameters3D.Create(requested + Vector3.Up * 1.8f,
            requested + Vector3.Down * 3f, player.CollisionMask,
            new global::Godot.Collections.Array<Rid> { player.GetRid() });
        var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        var body = hit.Count > 0 ? hit["collider"].AsGodotObject() as CollisionObject3D : null;
        var contact = body is null ? null : body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
        var source = contact?.HasMeta("authoredSourceMesh") == true ? contact.GetMeta("authoredSourceMesh").AsString() : string.Empty;
        if (hit.Count == 0 || hit["normal"].AsVector3().Y <= .7f
            || (requiredFloor is not null && body != requiredFloor && source != requiredFloor.GetPath().ToString()))
        { Fail("Exploration destination has no actual required floor: " + phase + "; requested=" + requested + "; owner=" + body?.GetPath() + "; source=" + source); return false; }
        var destination = hit["position"].AsVector3();
        if (!player.CanStandAt(destination + Vector3.Up * .015f))
        {
            var space = player.GetWorld3D().DirectSpaceState;
            using var shape = new CapsuleShape3D { Radius = player.BodyRadius, Height = player.StandingBodyHeight - .015f };
            using var query = new PhysicsShapeQueryParameters3D { Shape = shape, CollisionMask = player.CollisionMask,
                Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() }, Margin = .002f };
            query.Transform = new(Basis.Identity, destination + Vector3.Up * (player.StandingBodyHeight * .5f + .025f));
            var blockers = space.IntersectShape(query, 8).Select(hitShape =>
                $"{(hitShape["collider"].AsGodotObject() as Node)?.GetPath()} shape={hitShape["shape"].AsInt32()}").ToArray();
            var ring = new List<string>();
            foreach (var offset in new[] { new Vector2(.65f, 0), new Vector2(-.65f, 0), new Vector2(0, .65f), new Vector2(0, -.65f),
                new Vector2(.45f, .45f), new Vector2(-.45f, .45f), new Vector2(.45f, -.45f), new Vector2(-.45f, -.45f) })
            {
                var probe = destination + new Vector3(offset.X, 0, offset.Y);
                using var groundProbe = PhysicsRayQueryParameters3D.Create(probe + Vector3.Up * 1.8f,
                    probe + Vector3.Down * 3f, player.CollisionMask,
                    new global::Godot.Collections.Array<Rid> { player.GetRid() });
                var groundHit = space.IntersectRay(groundProbe);
                var verdict = "no-ground";
                if (groundHit.Count > 0 && groundHit["normal"].AsVector3().Y > .7f)
                    verdict = player.CanStandAt(groundHit["position"].AsVector3() + Vector3.Up * .015f) ? "fit" : "blocked";
                ring.Add($"{offset}={verdict}");
            }
            Fail($"Exploration destination does not fit a standing capsule: {phase}; requested={requested} ground={destination} blockers=[{string.Join(" | ", blockers)}] ring=[{string.Join(" ", ring)}]"); return false;
        }
        var started = player.GlobalPosition;
        var revision = player.PresentationTransformRevision;
        var recoveries = player.FallRecoveries;
        var clamps = player.EdgeClamps;
        var scene = bridge.ActiveSceneId;
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var began = Time.GetTicksMsec();
        using var cancellation = new System.Threading.CancellationTokenSource();
        var watchdog = Task.Delay(12000, cancellation.Token);
        try
        {
            for (var frame = 0; frame < 600 && Distance() >= .09f; frame++)
            {
                if (watchdog.IsCompleted || Time.GetTicksMsec() - began >= 12000)
                { Fail("Exploration walk timed out: " + phase); return false; }
                var delta = destination - player.GlobalPosition;
                player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward");
                var next = ExplorationFrames(1);
                if (await Task.WhenAny(next, watchdog) != next || watchdog.IsCompleted)
                { Fail("Exploration walk lost actual physics frames: " + phase); return false; }
                await next;
            }
            Input.ActionRelease("move_forward");
            for (var frame = 0; frame < 4; frame++)
            {
                var next = ExplorationFrames(1);
                if (await Task.WhenAny(next, watchdog) != next || watchdog.IsCompleted)
                { Fail("Exploration release lost actual physics frames: " + phase); return false; }
                await next;
            }
        }
        finally { Input.ActionRelease("move_forward"); cancellation.Cancel(); }
        if (Time.GetTicksMsec() - began >= 12000 || Distance() >= .09f
            || !player.IsOnFloor() || player.ModalOpen || player.IsCrouching
            || Math.Abs(player.GlobalPosition.Y - destination.Y) > .08f
            || !player.CanStandAt(player.GlobalPosition) || player.PresentationTransformRevision != revision
            || player.FallRecoveries != recoveries || player.EdgeClamps != clamps
            || bridge.ActiveSceneId != scene || knowledge != bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText())
        {
            var collisions = Enumerable.Range(0, player.GetSlideCollisionCount()).Select(index =>
            {
                var collision = player.GetSlideCollision(index);
                return $"{(collision.GetCollider() as Node)?.GetPath()} normal={collision.GetNormal()}";
            });
            Fail($"Exploration ordinary walk failed {phase}: {started} -> {player.GlobalPosition}, expected {destination}; contacts={string.Join(';', collisions)}");
            return false;
        }
        GD.Print($"exploration-physical-walk: {phase}; {started} -> {player.GlobalPosition}; actualFloor={body?.GetPath()}; source={source}; no recovery/progress; fixture start only");
        return true;
        float Distance() => new Vector2(player.GlobalPosition.X - destination.X, player.GlobalPosition.Z - destination.Z).Length();
    }

    private async Task CaptureExplorationView(string name)
    {
        var directory = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("URMAN_IMAGE_UI_OUTPUT must be absolute.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "exploration/" + name);
        if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new IOException(path);
        GD.Print("exploration-capture: " + path);
    }

    private async Task<bool> DiscoverInterior(RuntimeBridge bridge, Main main, InteractionTarget target,
        string scene, string slug)
    {
        var zoneId = slug.StartsWith("house-", StringComparison.Ordinal) ? "house_old_pc" : "fap_clinic";
        var room = main.ConnectedWorld!.GetZoneInstance(zoneId);
        if (room is null || main.ConnectedWorld.ActiveZoneId != zoneId || target.GetParent() != room)
        { Fail("Interior discovery is not in the active authored room: " + slug); return false; }
        var propName = slug switch
        {
            "house-interior-photo-back" => "DiscoveryFamilyPhoto",
            "house-interior-language-tin" => "DiscoverySewingTin",
            "fap-interior-height-marks" => "DiscoveryHeightMarks",
            "fap-interior-repaired-desk-object" => "DiscoveryRepairedLamp",
            "fap-service-cabinet" => "FapServiceCabinetFixing",
            _ => string.Empty
        };
        var prop = room.GetNodeOrNull<Node3D>(propName);
        Node3D? floor = zoneId == "house_old_pc" ? room.GetNodeOrNull<Node3D>("Floor")
            : room.FindChild("FapInteriorShell_Floor_LOD0", true, false) as MeshInstance3D;
        var floorMesh = floor as MeshInstance3D ?? floor?.GetNodeOrNull<MeshInstance3D>("Visible");
        if (prop is null || !prop.IsVisibleInTree() || floor is null || floorMesh?.Mesh is null)
        { Fail("Interior discovery has no visible prop or authored floor: " + slug); return false; }
        var floorBounds = (room.GlobalTransform.AffineInverse() * floorMesh.GlobalTransform) * floorMesh.Mesh.GetAabb();
        if (slug == "fap-interior-height-marks")
        {
            var jamb = room.FindChild("FapInteriorShell_DoorFrameRight_LOD0", true, false) as MeshInstance3D;
            var jambBounds = jamb?.Mesh is null ? (Aabb?)null
                : (room.GlobalTransform.AffineInverse() * jamb.GlobalTransform) * jamb.Mesh.GetAabb();
            GD.Print($"standing-discovery-mount slug={slug} room={room.GlobalTransform} floor={floorBounds} "
                + $"propLocal={room.ToLocal(prop.GlobalPosition)} targetLocal={room.ToLocal(target.GlobalPosition)} "
                + $"actualJamb={jamb?.GetPath()} jambLocalBounds={jambBounds}");
        }
        Vector3[] approaches;
        if (slug == "fap-service-cabinet")
        {
            if (!target.HasMeta("accessAnchor"))
            { Fail("The service cabinet lost its authored open-side approach."); return false; }
            approaches = [target.GetMeta("accessAnchor").AsVector3()];
        }
        else
        {
            // Photo and tin face local +Z (the photo is mounted at 90 degrees;
            // the chest-mounted tin at 180). The entrance marks face -Z into
            // the clinic; the desk lamp is approached from its open +Z side.
            // Derive positions from the actual prop basis, never world +Z.
            var front = prop.GlobalBasis.Z * (slug == "fap-interior-height-marks" ? -1f : 1f);
            front.Y = 0;
            front = front.Normalized();
            var side = prop.GlobalBasis.X;
            side.Y = 0;
            side = side.Normalized();
            approaches = [prop.GlobalPosition + front * 1.35f,
                prop.GlobalPosition + front * 1.65f - side * .20f];
        }
        for (var index = 0; index < approaches.Length; index++)
        {
            var local = room.ToLocal(approaches[index]);
            approaches[index] = room.ToGlobal(new(local.X, floorBounds.End.Y + .04f, local.Z));
        }
        if (slug == "fap-service-cabinet")
            return await DiscoverFapServiceCorner(bridge, target, room, floor, approaches[0], scene);
        return await DiscoverFromStandingViews(bridge, target, scene, slug, approaches, floor);
    }

    private async Task<bool> DiscoverFapServiceCorner(RuntimeBridge bridge, InteractionTarget target,
        Node3D room, Node3D floor, Vector3 anchor, string scene)
    {
        var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
        var original = player.CapturePortableTransform();
        var aisle = anchor + room.GlobalBasis.Z * .95f;
        global::Godot.Collections.Dictionary hit = new();
        // The clinic furniture was rearranged (the cot now stands on the old
        // +Z aisle). Keep the rule — a standable point on the actual floor
        // within a metre of the corner — and try the room's four directions.
        foreach (var direction in new[] { room.GlobalBasis.Z, -room.GlobalBasis.X, room.GlobalBasis.X, -room.GlobalBasis.Z })
        {
            foreach (var reach in new[] { .95f, .75f, 1.15f })
            {
                var candidate = anchor + direction.Normalized() * reach;
                using var probe = PhysicsRayQueryParameters3D.Create(candidate + Vector3.Up,
                    candidate + Vector3.Down * 1.5f, player.CollisionMask,
                    new global::Godot.Collections.Array<Rid> { player.GetRid() });
                var found = player.GetWorld3D().DirectSpaceState.IntersectRay(probe);
                if (found.Count > 0 && found["normal"].AsVector3().Y > .7f
                    && !((found["collider"].AsGodotObject() as Node)?.Name.ToString().Contains("Furniture", StringComparison.Ordinal) ?? false)
                    && player.CanStandAt(found["position"].AsVector3() + Vector3.Up * .05f))
                { aisle = candidate; hit = found; break; }
            }
            if (hit.Count > 0) break;
        }
        var entryId = ChapterPrefix + "knowledge/discovery-fap-service-cabinet";
        try
        {
            // The one local fixture is in the open aisle, not at the discovery.
            if (hit.Count == 0 || hit["normal"].AsVector3().Y <= .7f
                || !player.CanStandAt(hit["position"].AsVector3() + Vector3.Up * .05f))
            {
                var detail = hit.Count == 0 ? "no floor under the aisle"
                    : $"floor={(hit["collider"].AsGodotObject() as Node)?.GetPath()} normal={hit["normal"].AsVector3()} "
                      + $"standable={player.CanStandAt(hit["position"].AsVector3() + Vector3.Up * .05f)}";
                Fail($"FAP service corner has no clear aisle fixture: anchor={anchor} aisle={aisle} {detail}.");
                return false;
            }
            player.ApplyZoneSpawn(hit["position"].AsVector3() + Vector3.Up * .05f, 0);
            await ExplorationFrames(12);
            if (!await WalkExplorationLeg(bridge, player, anchor, "FAP enter the service corner", floor)) return false;
            AimExploration(player, target.GlobalPosition);
            await ExplorationFrames(3);
            if (!target.IsAvailable() || !ExplorationRayHits(player, target)
                || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-fap-service-cabinet") != "hidden")
            { Fail("Walking into the service corner did not expose the actual unread lower hinge."); return false; }
            if (!await SaveExploration(bridge, "ex10-fap-corner-unread")) return false;
            if (!await WalkExplorationLeg(bridge, player, aisle, "FAP leave the unread service corner", floor)
                || !await LoadExploration(bridge, "ex10-fap-corner-unread")) return false;
            await ExplorationFrames(8);
            if (KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-fap-service-cabinet") != "hidden")
            { Fail("An unread corner save supplied the hinge observation."); return false; }
            if (!await WalkExplorationLeg(bridge, player, aisle, "FAP return to the aisle from an unread save", floor)
                || !await WalkExplorationLeg(bridge, player, anchor, "FAP return to inspect the hinge", floor)) return false;
            AimExploration(player, target.GlobalPosition);
            await ExplorationFrames(3);
            if (!target.IsAvailable() || !ExplorationRayHits(player, target))
            { Fail("Returned service-corner ray misses the unchanged hinge."); return false; }
            await PressExplorationInteract();
            var journal = (JournalUi)GetTree().GetFirstNodeInGroup("journal_ui");
            for (var frame = 0; frame < 180 && !journal.GetNode<Control>("Screen").Visible; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!journal.GetNode<Control>("Screen").Visible || journal.ActiveEntryId != entryId
                || bridge.ActiveSceneId != scene || KnowledgeStatus(bridge.SelectRuntimeState(), "discovery-fap-service-cabinet") != "confirmed"
                || bridge.JournalEntries().Count(entry => entry.EntryId == entryId) != 1)
            { Fail("Manual hinge inspection did not produce its single visible household source."); return false; }
            await CaptureExplorationView("ex10_fap_service_corner_source");
            journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await ExplorationFrames(3);
            if (!await WalkExplorationLeg(bridge, player, aisle, "FAP safely leave the inspected corner", floor)
                || !await SaveExploration(bridge, "ex10-fap-corner-read") || !await LoadExploration(bridge, "ex10-fap-corner-read")) return false;
            await ExplorationFrames(8);
            if (!await WalkExplorationLeg(bridge, player, anchor, "FAP revisit the loaded inspected corner", floor)) return false;
            if (await bridge.DispatchInteractionAsync(target.InteractionId)
                || bridge.JournalEntries().Count(entry => entry.EntryId == entryId) != 1 || bridge.ActiveSceneId != scene)
            { Fail("Loaded service-corner repeat duplicated or advanced the source."); return false; }
            await CaptureExplorationView("ex10_fap_service_corner_return");
            return await WalkExplorationLeg(bridge, player, aisle, "FAP final supported return to the aisle", floor);
        }
        finally { player.ApplyPortableTransform(original); }
    }

    private async Task<bool> DiscoverFromStandingViews(RuntimeBridge bridge, InteractionTarget target,
        string scene, string slug, Vector3[] approaches, Node3D? requiredFloor = null)
    {
        var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
        GD.Print($"standing-discovery-routing-before slug={slug} zone={bridge.CurrentZoneId} "
            + $"layer={target.CollisionLayer} activeLayer={target.ActiveCollisionLayer} mask={target.CollisionMask} "
            + $"semantic={bridge.IsInteractionAvailable(target.InteractionId)} cached={target.IsSemanticallyAvailable()}");
        // A presentation return switches the connected zone synchronously;
        // SetWorldLocation then refreshes semantic targets on the next process
        // frame. Layer 0 during that interval is disabled, not a player blocker.
        // Wait for that existing owner without changing layers or story state.
        if (target.ActiveCollisionLayer != 4u || (target.CollisionLayer & player.CollisionMask) != 0)
        { Fail("Discovery selection volume has a physical collision layer: " + slug); return false; }
        for (var frame = 0; frame < 120 && (target.CollisionLayer != 4u || !target.IsSemanticallyAvailable()); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (target.CollisionLayer != 4u || target.CollisionMask != 0u || !target.IsSemanticallyAvailable())
        {
            Fail($"Discovery routing did not expose its existing ray-only target: {slug}; "
                + $"zone={bridge.CurrentZoneId} layer={target.CollisionLayer} mask={target.CollisionMask} "
                + $"semantic={bridge.IsInteractionAvailable(target.InteractionId)} cached={target.IsSemanticallyAvailable()}");
            return false;
        }
        GD.Print($"standing-discovery-routing-ready slug={slug} layer={target.CollisionLayer} "
            + $"mask={target.CollisionMask} playerMask={player.CollisionMask}");
        var original = player.CapturePortableTransform();
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var interactionRay = player.GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        try
        {
            foreach (var approach in approaches)
            {
                using var groundRay = PhysicsRayQueryParameters3D.Create(approach + Vector3.Up * 1.4f,
                    approach + Vector3.Down * 1.2f, 1u,
                    new global::Godot.Collections.Array<Rid> { player.GetRid() });
                var ground = player.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
                if (ground.Count == 0 || ground["normal"].AsVector3().Dot(Vector3.Up) <= .7f)
                {
                    GD.Print($"standing-discovery-view slug={slug} rejected={approach} reason=no-walkable-ground");
                    continue;
                }
                var support = ground["collider"].AsGodotObject() as Node;
                var contact = support is CollisionObject3D body
                    ? body.ShapeOwnerGetOwner(body.ShapeFindOwner(ground["shape"].AsInt32())) as Node : null;
                var source = contact?.HasMeta("authoredSourceMesh") == true
                    ? contact.GetMeta("authoredSourceMesh").AsString() : string.Empty;
                if (requiredFloor is not null && support != requiredFloor && source != requiredFloor.GetPath().ToString())
                {
                    GD.Print($"standing-discovery-view slug={slug} rejected={approach} reason=not-room-floor "
                        + $"support={support?.GetPath()} shape={contact?.GetPath()} source={source}");
                    continue;
                }
                var feet = ground["position"].AsVector3() + Vector3.Up * .05f;
                if (!player.CanStandAt(feet))
                {
                    GD.Print($"standing-discovery-view slug={slug} rejected={approach} feet={feet} reason=standing-capsule-blocked "
                        + StandingBlockers(feet));
                    continue;
                }
                player.ApplyZoneSpawn(feet, 0f);
                for (var frame = 0; frame < 12; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                if (!player.IsOnFloor() || player.IsCrouching || player.ModalOpen || !player.CanStandAt(player.GlobalPosition))
                {
                    GD.Print($"standing-discovery-view slug={slug} rejected={approach} feet={player.GlobalPosition} "
                        + $"floor={player.IsOnFloor()} crouched={player.IsCrouching} modal={player.ModalOpen}");
                    continue;
                }
                var delta = target.GlobalPosition - camera.GlobalPosition;
                player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                    Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                interactionRay.ForceRaycastUpdate();
                var collider = interactionRay.IsColliding() ? interactionRay.GetCollider() as Node : null;
                GD.Print($"standing-discovery-view slug={slug} approach={approach} feet={player.GlobalPosition} eye={camera.GlobalPosition} "
                    + $"support={support?.GetPath()} source={source} "
                    + $"target={target.GlobalPosition} mask={interactionRay.CollisionMask} "
                    + $"collider={collider?.GetPath()} hit={interactionRay.GetCollisionPoint()}");
                if (collider != target) continue;
                if (!target.IsAvailable()
                    || !await bridge.DispatchInteractionAsync(Interaction("discover-" + slug))
                    || bridge.ActiveSceneId != scene
                    || bridge.IsInteractionAvailable(Interaction("discover-" + slug))
                    || bridge.JournalEntries().Count(entry => entry.EntryId == ChapterPrefix + "knowledge/discovery-" + slug) != 1)
                { Fail("Discovery failed its available, single-use journal/world contract from a real standing view: " + slug); return false; }
                return true;
            }
            Fail("No authored view fits a standing player on its real support and reaches the discovery with the normal interaction ray: " + slug);
            return false;
        }
        finally
        {
            player.ApplyPortableTransform(original);
        }

        string StandingBlockers(Vector3 feet)
        {
            var body = (CapsuleShape3D)player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
            // Diagnostic only. CanStandAt above remains the actual acceptance
            // owner; this uses the standing height of first_person_player.tscn.
            const float standingHeight = 1.8f;
            using var shape = new CapsuleShape3D { Radius = body.Radius, Height = standingHeight - .015f };
            using var query = new PhysicsShapeQueryParameters3D
            {
                Shape = shape,
                Transform = new(Basis.Identity, feet + Vector3.Up * (standingHeight * .5f + .01f)),
                CollisionMask = player.CollisionMask,
                Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() },
                Margin = .002f
            };
            var contacts = player.GetWorld3D().DirectSpaceState.IntersectShape(query, 8);
            return "mask=" + player.CollisionMask + " standing-height=" + standingHeight
                + " current-height=" + player.BodyHeight + " colliders="
                + string.Join(" | ", contacts.Select(hit => (hit["collider"].AsGodotObject() as Node)?.GetPath()
                    + " shape=" + hit["shape"].AsInt32()));
        }
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

internal static class Act1LocalReactionProof
{
    private const string Prefix = "urman.chapter1:";
    private sealed record Reaction(string Character, string Dialogue, string Source, string Terminal,
        string Choice, string Reply, string? QuestionChoice, string? ReturnChoice,
        string? Flag, string? OutcomeKnowledge, string[] ParentNodes);

    private static Reaction Definition(string kind) => kind switch
    {
        "tin" => new("gulsina", "gulsina_yaramyy", "discovery-house-interior-language-tin", "open-tin-reply",
            "choice-gulsina-open-tin", "dialogue-gulsina-open-tin", null, "choice-alsu-street-questions",
            "tin_lid_noticed", null, new[] { "home-warning" }),
        "lamp" => new("naila", "naila_medical_record", "discovery-fap-interior-repaired-desk-object", "desk-light-reply",
            "choice-naila-desk-light", "dialogue-naila-desk-light-reply", null, "choice-naila-return-from-light",
            "desk_light_noticed", null, new[] { "official-wording", "follow-up" }),
        "photo" => new("mansur", "mansur_pc_request", "clue_photo_gate_repair_checked", "photo-response",
            "mansur-photo-ask-seam", "mansur-photo-reply", "ask-mansur-photo", null,
            null, "clue_photo_question_answered", new[] { "photo-question" }),
        "underdeck" => new("mansur", "mansur_pc_request", "clue_underdeck_rattle_quiet", "underdeck-quiet-reply",
            "choice-mansur-underdeck-quiet", "dialogue-mansur-underdeck-quiet", null, "choice-alsu-street-questions",
            "underdeck_quiet_noticed", null, new[] { "ask-for-help", "follow-up" }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown local reaction proof.")
    };

    internal static async Task<bool> CompleteAsync(Node owner, RuntimeBridge bridge, string kind,
        bool verifyReturn = true, Func<string, Task<bool>>? interactPhysically = null)
    {
        try
        {
            var spec = Definition(kind);
            var tree = owner.GetTree();
            var main = (Main)tree.GetFirstNodeInGroup("zone_manager");
            var player = (FirstPersonController)tree.GetFirstNodeInGroup("player_controller");
            var dialogue = (DialogueUi)tree.GetFirstNodeInGroup("dialogue_ui");
            var scene = bridge.ActiveSceneId;
            var zone = bridge.CurrentZoneId;
            Check(zone == (kind == "lamp" ? "fap_clinic" : "house_old_pc") && Known(spec.Source)
                && !Outcome() && !player.ModalOpen,
                "Local reaction requires its actual source, an unanswered NPC and the visible host zone: " + kind);
            var beforeTerminal = bridge.SelectRuntimeState();
            Check(await bridge.EnterDialogueNodeAsync(Prefix + "dialogue/" + spec.Dialogue, spec.Terminal),
                "The source-backed terminal was not readable before its explicit choice: " + kind);
            var afterTerminal = bridge.SelectRuntimeState();
            foreach (var field in new[] { "knowledge", "npc", "journal", "vocabulary", "pressure", "beats" })
                Check(beforeTerminal.GetProperty(field).GetRawText() == afterTerminal.GetProperty(field).GetRawText(),
                    "Entering a reply without its choice granted progress: " + kind + "/" + field);
            await OpenQuestion();
            Check(!Outcome(), "Opening the source-holder conversation acknowledged unfinished help: " + kind);
            if (verifyReturn)
            {
                await Close();
                await RoundTrip("unanswered");
                Check(Known(spec.Source) && !Outcome(), "Loading an unanswered local question invented the reply: " + kind);
                await OpenQuestion();
            }
            await Choice(spec.Choice);
            Check(await VerifyVisibleReplyAsync(owner, bridge, spec.Reply), "Local reaction text did not remain visible: " + kind);
            Check(Outcome() && bridge.ActiveSceneId == scene && bridge.CurrentZoneId == zone,
                "The explicit local reply did not retain its consequence and original investigation scene: " + kind);
            if (spec.ReturnChoice is not null)
            {
                Check(HasChoice(spec.ReturnChoice), "The visible reply lost its return to ordinary questions: " + kind);
                await Choice(spec.ReturnChoice);
                Check(dialogue.IsOpen && !HasChoice(spec.QuestionChoice ?? spec.Choice),
                    "Returning from a local reply repeated the completed question: " + kind);
            }
            await Close();
            if (verifyReturn) await RoundTrip("answered");
            Check(Known(spec.Source) && Outcome(), "Save/load lost the source or explicit local consequence: " + kind);
            var beforeRepeat = bridge.SelectRuntimeState();
            var authored = bridge.RequireDialogue(Prefix + "dialogue/" + spec.Dialogue);
            foreach (var parentNode in spec.ParentNodes)
            {
                var choice = authored.Nodes[parentNode].Choices.Single(entry =>
                    entry.TextId == Prefix + "text/" + spec.Choice);
                Check(!await bridge.ChooseDialogueAsync(authored.Id, parentNode, choice.Id),
                    "Repeating an acknowledged local question was accepted: " + kind + "/" + parentNode);
            }
            var afterRepeat = bridge.SelectRuntimeState();
            foreach (var field in new[] { "knowledge", "npc", "journal", "pressure", "beats" })
                Check(beforeRepeat.GetProperty(field).GetRawText() == afterRepeat.GetProperty(field).GetRawText(),
                    "A rejected repeat changed local or story state: " + kind + "/" + field);
            if (spec.OutcomeKnowledge is not null)
                Check(bridge.JournalEntries().Count(entry => entry.EntryId == Prefix + "knowledge/" + spec.OutcomeKnowledge) == 1,
                    "The photo reply was omitted from the journal or duplicated.");
            GD.Print("local-reaction-proof: " + kind + "; real source, exact visible reply, return choice and repeat rejection; save-return=" + verifyReturn);
            return true;

            bool Known(string id) => bridge.SelectRuntimeState().GetProperty("knowledge")
                .GetProperty(Prefix + "knowledge/" + id).GetProperty("status").GetString() == "confirmed";
            bool Outcome() => spec.OutcomeKnowledge is not null ? Known(spec.OutcomeKnowledge)
                : bridge.SelectRuntimeState().GetProperty("npc").TryGetProperty(Prefix + "character/" + spec.Character, out var npc)
                    && npc.TryGetProperty(spec.Flag!, out var value) && value.ValueKind == JsonValueKind.True;
            bool HasChoice(string id) => dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren()
                .OfType<Button>().Any(button => !button.Disabled && button.IsVisibleInTree()
                    && button.Text == bridge.ResolveText(Prefix + "text/" + id));
            async Task OpenQuestion()
            {
                var action = "talk-" + spec.Character;
                var target = main.FindChildren("*", "", true, false).OfType<InteractionTarget>()
                    .FirstOrDefault(candidate => !candidate.IsQueuedForDeletion()
                        && candidate.InteractionId == Prefix + "interaction/" + action);
                Check(target is not null, "No physical source-holder target: " + action);
                GD.Print("local-reaction-target-before: action=" + action + " zone=" + bridge.CurrentZoneId
                    + " semantic=" + bridge.IsInteractionAvailable(target!.InteractionId)
                    + " cached=" + target.IsSemanticallyAvailable() + " physical=" + bridge.CanPhysicallyUseInteraction(target.InteractionId));
                // A known door changes location synchronously and queues its
                // ordinary target refresh. Let that published state reach the
                // physical target before attempting the next player's action.
                for (var frame = 0; frame < 30 && !target.IsAvailable(); frame++)
                    await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(target.IsAvailable(), "No available physical source-holder target: " + action
                    + "; semantic=" + bridge.IsInteractionAvailable(target.InteractionId)
                    + "; cached=" + target.IsSemanticallyAvailable() + "; zone=" + bridge.CurrentZoneId);
                if (interactPhysically is null) target!.Interact();
                else Check(await interactPhysically(action), "Mapped input did not reach the actual source holder: " + action);
                for (var frame = 0; frame < 180 && (!dialogue.IsOpen || !HasChoice(spec.QuestionChoice ?? spec.Choice)); frame++)
                    await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(dialogue.IsOpen && player.ModalOpen && HasChoice(spec.QuestionChoice ?? spec.Choice),
                    "Actual source-holder dialogue lacks the earned question: " + kind);
                if (spec.QuestionChoice is not null) await Choice(spec.QuestionChoice);
            }
            async Task Choice(string id)
            {
                var button = dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren()
                    .OfType<Button>().SingleOrDefault(candidate => !candidate.Disabled && candidate.IsVisibleInTree()
                        && candidate.Text == bridge.ResolveText(Prefix + "text/" + id));
                Check(button is not null, "The actual local choice is absent: " + kind + "/" + id);
                button!.EmitSignal(Button.SignalName.Pressed);
                for (var frame = 0; frame < 5; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            async Task Close()
            {
                dialogue._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
                for (var frame = 0; frame < 5; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(!dialogue.IsOpen && !player.ModalOpen, "Closing a local reaction retained player input: " + kind);
            }
            async Task RoundTrip(string suffix)
            {
                var slot = "local-reaction-" + kind + "-" + suffix;
                Check(await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot), "Local reaction save/load failed: " + slot);
                for (var frame = 0; frame < 5; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                for (var frame = 0; frame < 240 && player.ModalOpen; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(!player.ModalOpen && bridge.ActiveSceneId == scene && bridge.CurrentZoneId == zone,
                    "Local reaction load did not release the actual host zone: " + slot);
            }
        }
        catch (Exception exception)
        {
            GD.PushError("local-reaction-proof: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }

    internal static async Task<bool> VerifyVisibleReplyAsync(Node owner, RuntimeBridge bridge, string textId)
    {
        var dialogue = (DialogueUi)owner.GetTree().GetFirstNodeInGroup("dialogue_ui");
        var line = dialogue.GetNode<RichTextLabel>("Screen/Panel/Layout/Line");
        var expected = bridge.ResolveText(Prefix + "text/" + textId);
        for (var frame = 0; frame < 180 && line.Text != expected; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        if (dialogue.IsOpen && line.IsVisibleInTree() && line.Text == expected) return true;
        GD.PushError("local-reaction-visible-reply: expected=" + textId + "; open=" + dialogue.IsOpen
            + "; actual=" + line.Text);
        owner.GetTree().Quit(1);
        return false;
    }

    internal static async Task<bool> ChooseVisibleAsync(Node owner, RuntimeBridge bridge, string textId)
    {
        var dialogue = (DialogueUi)owner.GetTree().GetFirstNodeInGroup("dialogue_ui");
        Button? button = null;
        for (var frame = 0; frame < 180 && button is null; frame++)
        {
            button = dialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices").GetChildren().OfType<Button>()
                .SingleOrDefault(candidate => !candidate.Disabled && candidate.IsVisibleInTree()
                    && candidate.Text == bridge.ResolveText(Prefix + "text/" + textId));
            if (button is null) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!dialogue.IsOpen || button is null)
        {
            GD.PushError("local-reaction-choice: the authored visible choice is absent: " + textId);
            owner.GetTree().Quit(1);
            return false;
        }
        button.EmitSignal(Button.SignalName.Pressed);
        for (var frame = 0; frame < 5; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    internal static async Task<bool> RejectUnreadAsync(Node owner, RuntimeBridge bridge)
    {
        try
        {
            var before = bridge.SelectRuntimeState();
            foreach (var kind in new[] { "tin", "lamp", "photo", "underdeck" })
            {
                var spec = Definition(kind);
                var dialogue = bridge.RequireDialogue(Prefix + "dialogue/" + spec.Dialogue);
                Check(!await bridge.EnterDialogueNodeAsync(dialogue.Id, spec.Terminal),
                    "An unread local source exposed its terminal reply: " + kind);
                foreach (var parent in spec.ParentNodes)
                {
                    var choice = dialogue.Nodes[parent].Choices.Single(entry => entry.TextId == Prefix + "text/" + spec.Choice);
                    Check(!await bridge.ChooseDialogueAsync(dialogue.Id, parent, choice.Id),
                        "A source-free local question granted its outcome: " + kind + "/" + parent);
                }
            }
            Check(!await bridge.EnterDialogueNodeAsync(Prefix + "dialogue/alsu_route_context", "message-bounded-reply"),
                "Alsu's message reply appeared before its source.");
            foreach (var choice in new[] { "quote-heard-voice", "correct-seen-person" })
                Check(!await bridge.ChooseDialogueAsync(Prefix + "dialogue/alsu_route_context", "message-question", choice),
                    "A source-free message question produced Alsu's reply: " + choice);
            var after = bridge.SelectRuntimeState();
            foreach (var field in new[] { "knowledge", "npc", "journal", "vocabulary", "pressure", "beats" })
                Check(before.GetProperty(field).GetRawText() == after.GetProperty(field).GetRawText(),
                    "Rejected unread reactions mutated the clean runtime: " + field);
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError("local-reaction-unread: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal static class Act1RinatRoadsideProof
{
    private const string Prefix = "urman.chapter1:";
    private const string Observe = Prefix + "interaction/observe-rinat-roadside";
    private const string Presence = Prefix + "knowledge/clue_rinat_at_roadside";
    private const string PresenceBeat = Prefix + "beat/rinat-visible-before-edge";

    // This fixture proves the normal runtime observation and persistence at a
    // standing camera position. Physical approach and visual quality still need
    // the separate traversal and native frame review.
    internal static async Task<bool> ObserveAsync(Node owner, RuntimeBridge bridge, bool verifyReturn = false)
    {
        try
        {
            var tree = owner.GetTree();
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Rinat observation has no world owner.");
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Rinat observation has no player.");
            var scene = bridge.ActiveSceneId;
            Check(bridge.CurrentZoneId == "zirat_road" && scene == Prefix + "scene/zirat-road"
                && (main.ConnectedWorld is null || main.ConnectedWorld.ActiveZoneId == "zirat_road"),
                "Rinat proof did not reach the actual roadside zone.");
            Check(!Known(Presence) && !Completed(PresenceBeat)
                && !bridge.IsInteractionAvailable(Prefix + "interaction/zirat-road-to-forest")
                && !Known(Prefix + "knowledge/clue_do_not_answer_rule"),
                "Road entry silently completed Rinat's visible presence or opened the forest.");

            await StandAtObservation();
            Aim(Target().GetMeta("observationLookAt").AsVector3(), away: true);
            await Frames(2);
            Check(!Target().IsAvailable() && !await bridge.DispatchInteractionAsync(Observe)
                && !Known(Presence) && !Completed(PresenceBeat),
                "Looking away still confirmed the physically guarded roadside observation.");
            if (verifyReturn)
            {
                await RoundTrip("len01-rinat-roadside-pending");
                Check(!Known(Presence) && !Completed(PresenceBeat)
                    && !bridge.IsInteractionAvailable(Prefix + "interaction/zirat-road-to-forest"),
                    "Saving an abandoned observation supplied its result.");
                await StandAtObservation();
            }

            var target = Target();
            Aim(target.GetMeta("observationLookAt").AsVector3());
            await Frames(2);
            Check(target.CollisionLayer == 4u && target.IsAvailable(),
                "Rinat and the lamp were unavailable from the authored standing view.");
            target.Interact();
            for (var frame = 0; frame < 180 && !Known(Presence); frame++) await Frames(1);
            await Frames(3);
            Check(Known(Presence) && Completed(PresenceBeat)
                && bridge.ActiveSceneId == scene && bridge.CurrentZoneId == "zirat_road"
                && !Known(Prefix + "knowledge/clue_do_not_answer_rule")
                && !Completed(Prefix + "beat/cliffhanger-hard-cut") && !player.ModalOpen,
                "The live roadside view did not record presence, changed the scene, or disclosed the final rule.");
            Check(!bridge.IsInteractionAvailable(Observe)
                && bridge.JournalEntries().Count(entry => entry.EntryId == Presence) == 1,
                "The roadside observation stayed repeatable or duplicated its source.");
            if (verifyReturn)
            {
                await RoundTrip("len01-rinat-roadside-observed");
                Check(Known(Presence) && Completed(PresenceBeat)
                    && bridge.JournalEntries().Count(entry => entry.EntryId == Presence) == 1
                    && !Known(Prefix + "knowledge/clue_do_not_answer_rule"),
                    "Loading lost the witnessed presence, duplicated its source, or supplied the warning.");
            }
            GD.Print("rinat-roadside-proof: real target and standing live view; look-away rejected; presence recorded once; final rule hidden"
                + (verifyReturn ? "; pending and observed saves restored" : "")
                + "; traversal and artistic acceptance remain separate");
            return true;

            InteractionTarget Target()
            {
                var target = main.FindChild("RinatRoadsideObservation", true, false) as InteractionTarget;
                Check(target is not null && target.InteractionId == Observe
                    && target.HasMeta("observationReferenceEye") && target.HasMeta("observationLookAt"),
                    "The current world lacks Rinat's physical observation target or view metadata.");
                return target!;
            }

            async Task StandAtObservation()
            {
                var reference = Target().GetMeta("observationReferenceEye").AsVector3();
                // The restored player may already occupy the reference. Its own
                // capsule is not terrain; using that hit would put feet on its head.
                using var legacyRay = PhysicsRayQueryParameters3D.Create(reference + Vector3.Up * 3f,
                    reference + Vector3.Down * 8f, 1u);
                var legacyHit = player.GetWorld3D().DirectSpaceState.IntersectRay(legacyRay);
                using var ray = PhysicsRayQueryParameters3D.Create(reference + Vector3.Up * 3f,
                    reference + Vector3.Down * 8f, 1u,
                    new global::Godot.Collections.Array<Rid> { player.GetRid() });
                var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Check(hit.Count > 0 && hit["collider"].AsGodotObject() != player
                    && hit["normal"].AsVector3().Dot(Vector3.Up) > .7f,
                    "Rinat's observation reference has no standing collision surface.");
                var ground = hit["position"].AsVector3();
                var collider = hit["collider"].AsGodotObject() is Node body ? body.GetPath().ToString() : "<unnamed>";
                var previousCollider = legacyHit.Count > 0 && legacyHit["collider"].AsGodotObject() is Node previous
                    ? previous.GetPath().ToString() : "<none>";
                GD.Print("rinat-standing-probe: before feet=" + player.GlobalPosition
                    + " ground=" + ground + " collider=" + collider + " unfiltered=" + previousCollider
                    + " floor=" + player.IsOnFloor() + " crouch=" + player.IsCrouching
                    + " modal=" + player.ModalOpen
                    + " fade=" + (main.GetNodeOrNull<ColorRect>("Act1ZoneTransition/Fade")?.Color.A ?? 0f));
                player.ApplyZoneSpawn(ground + Vector3.Up * .05f, 0f);
                for (var frame = 0; frame < 12; frame++)
                    await owner.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                var settled = "feet=" + player.GlobalPosition + " ground=" + ground + " collider=" + collider
                    + " floor=" + player.IsOnFloor() + " crouch=" + player.IsCrouching
                    + " modal=" + player.ModalOpen + " velocity=" + player.Velocity
                    + " fade=" + (main.GetNodeOrNull<ColorRect>("Act1ZoneTransition/Fade")?.Color.A ?? 0f);
                GD.Print("rinat-standing-probe: after " + settled);
                Check(player.IsOnFloor() && !player.IsCrouching && !player.ModalOpen,
                    "Rinat's observation test player failed to stand on the real ground: " + settled);
            }

            void Aim(Vector3 point, bool away = false)
            {
                var eye = player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition;
                var delta = point - eye;
                if (away) delta = -delta;
                player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
                    Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
            }

            async Task RoundTrip(string slot)
            {
                var zone = bridge.CurrentZoneId;
                var spawn = bridge.CurrentSpawnPointId;
                Check(!player.ModalOpen && await bridge.SaveSlotAsync(slot) && await bridge.LoadSlotAsync(slot),
                    "Rinat observation save/load failed: " + slot);
                await Frames(6);
                Check(bridge.ActiveSceneId == scene && bridge.CurrentZoneId == zone
                    && bridge.CurrentSpawnPointId == spawn
                    && (main.ConnectedWorld is null || main.ConnectedWorld.ActiveZoneId == zone),
                    "Rinat observation load restored the wrong scene or physical zone.");
            }

            bool Known(string id) => bridge.SelectRuntimeState().GetProperty("knowledge")
                .GetProperty(id).GetProperty("status").GetString() == "confirmed";
            bool Completed(string id) => bridge.SelectRuntimeState().GetProperty("beats")
                .TryGetProperty(id, out var beat) && beat.GetString() == "completed";
            async Task Frames(int count)
            {
                for (var frame = 0; frame < count; frame++) await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception exception)
        {
            GD.PushError("rinat-roadside-proof: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }


    internal static async Task<bool> EnterFinaleAsync(Node owner, RuntimeBridge bridge)
    {
        try
        {
            var tree = owner.GetTree();
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Finale proof has no world owner.");
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Finale proof has no player.");
            Check(bridge.CurrentZoneId == "kara_urman_night"
                && bridge.ActiveSceneId == Prefix + "scene/forest-approach" && !player.ModalOpen,
                "Finale approach is outside the actual night zone or still holds a modal.");
            var target = main.FindChild("KaraForestApproachEndpoint", true, false) as InteractionTarget;
            Check(target is not null && target.InteractionId == Prefix + "interaction/forest-approach-to-forest"
                && target.HasMeta("observationReferenceEye") && target.HasMeta("observationLookAt"),
                "Finale approach lacks its actual endpoint or standing-view metadata.");
            var reference = target!.GetMeta("observationReferenceEye").AsVector3();
            using var groundRay = PhysicsRayQueryParameters3D.Create(reference + Vector3.Up * 3f,
                reference + Vector3.Down * 8f, 1u,
                new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var ground = player.GetWorld3D().DirectSpaceState.IntersectRay(groundRay);
            Check(ground.Count > 0 && ground["normal"].AsVector3().Dot(Vector3.Up) > .7f,
                "Finale approach has no standing collision surface.");
            player.ApplyZoneSpawn(ground["position"].AsVector3() + Vector3.Up * .05f, 0f);
            for (var frame = 0; frame < 12; frame++)
                await owner.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            Check(player.IsOnFloor() && !player.IsCrouching,
                "The finale reference does not fit an ordinary standing player.");
            var camera = player.GetNode<Camera3D>("Head/Camera3D");
            var point = target.GetMeta("observationLookAt").AsVector3();
            var direction = point - camera.GlobalPosition;
            player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
            using var targetRay = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 5u,
                new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(targetRay);
            Check(target.IsAvailable() && hit.Count > 0 && hit["collider"].AsGodotObject() == target,
                "The standing finale approach cannot aim at its actual endpoint.");
            target.Interact();
            for (var frame = 0; frame < 180 && bridge.ActiveSceneId != Prefix + "scene/forest"; frame++)
                await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(bridge.ActiveSceneId == Prefix + "scene/forest" && bridge.CurrentZoneId == "kara_urman_night",
                "The actual finale endpoint did not enter the voice scene in place.");
            var state = bridge.SelectRuntimeState();
            Check(state.GetProperty("knowledge").GetProperty(Prefix + "knowledge/clue_do_not_answer_rule")
                    .GetProperty("status").GetString() == "hidden"
                && (!state.GetProperty("beats").TryGetProperty(Prefix + "beat/cliffhanger-hard-cut", out var beat)
                    || beat.GetString() != "completed"),
                "Entering the final endpoint granted the warning before its physical and audio presentation.");
            if (!LookAtIntervention(owner)) return false;
            GD.Print("rinat-finale-approach: actual endpoint interacted from standing collision ground; eye="
                + camera.GlobalPosition + " target=" + point + "; warning and hard cut still pending");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError("rinat-finale-approach: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }


    internal static bool LookAtIntervention(Node owner)
    {
        try
        {
            var tree = owner.GetTree();
            var main = tree.GetFirstNodeInGroup("zone_manager") as Main
                ?? throw new InvalidOperationException("Intervention view has no world owner.");
            var player = tree.GetFirstNodeInGroup("player_controller") as FirstPersonController
                ?? throw new InvalidOperationException("Intervention view has no player.");
            var camera = player.GetNode<Camera3D>("Head/Camera3D");
            var actor = main.FindChildren("Npc_rinat", "", true, false).OfType<Node3D>()
                .FirstOrDefault(candidate => !candidate.IsQueuedForDeletion() && candidate.IsVisibleInTree()
                    && candidate.GetMeta("rinatStage", "missing").AsString() == "forest");
            Check(actor is not null, "Intervention view has no visible forest Rinat actor.");
            // Test-only ordinary look from the player's existing position. The
            // presentation owner still proves the actual head, lamp and hand
            // landmarks are in frame and visible before committing the finale.
            var direction = actor!.GlobalPosition + Vector3.Up * .95f - camera.GlobalPosition;
            player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError("rinat-intervention-view: " + exception);
            owner.GetTree().Quit(1);
            return false;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
