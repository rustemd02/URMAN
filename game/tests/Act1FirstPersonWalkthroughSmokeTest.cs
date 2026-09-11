using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// A bounded, test-only physical walkthrough of the Act 1 demo. Unlike the
/// deterministic corridor smoke, this route never writes a player position:
/// movement is driven through the production input actions and CharacterBody3D
/// collision, while interactions still use the first-person camera ray.
/// It is evidence of a traversable build, not a substitute for a human pass.
/// </summary>
public partial class Act1FirstPersonWalkthroughSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const float InteractionStandOff = 1.5f;

    private float _lookPitch;
    private float _walkedMeters;
    private bool _failed;

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 walkthrough could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 walkthrough could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = main.GetNodeOrNull<FirstPersonController>("Player");
        var ray = player?.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay");
        if (bridge is null || player is null || ray is null || !demo.IntroVisible)
        {
            Fail("Act 1 walkthrough did not expose the demo bridge, player, ray and intro.");
            return;
        }

        if (!main.EnableAct1ConnectedWorld || main.ConnectedWorld is null)
        {
            Fail("Act 1 walkthrough did not start through the connected-world demo mode.");
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
        if (demo.IntroVisible || player.ModalOpen)
        {
            Fail("Act 1 walkthrough could not dismiss the intro card with E.");
            return;
        }

        // Narrow mode still starts through the ordinary menu/arrival and walks
        // every metre. It verifies the optional loop without replaying dialogue.
        if (OS.GetEnvironment("URMAN_DISCOVERY_ROUTE_ONLY") == "fap-service")
        {
            foreach (var point in AgentBAct1Layout.FapBranchAxis)
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"service-approach-{point.X}-{point.Y}")) return;
            var gate = main.ConnectedWorld.GetZoneInstance("village_day")!.GetNode<StaticBody3D>("FapServiceGateCollision");
            if (gate.CollisionLayer != 1) { Fail("Service gate starts without its closed collider."); return; }
            foreach (var point in new Vector2[] { new(32,-24.2f), new(38,-23.8f), new(41.5f,-25), new(41.5f,-28.4f), new(40.8f,-28.4f), new(35.25f,-28.0f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"service-return-{point.X}-{point.Y}")) return;
            var sceneBefore = bridge.ActiveSceneId;
            if (!await InteractAt(player, ray, Interaction("discover-fap-exterior-service-path"))) return;
            (GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi)?.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
            await Frames(35);
            if (gate.CollisionLayer != 0 || bridge.ActiveSceneId != sceneBefore)
            { Fail("Service gate action did not open its collider while preserving the scene."); return; }
            foreach (var point in new Vector2[] { new(34.5f,-26.6f), new(34.5f,-24.0f), new(32,-24.2f), new(28,-26.2f) })
                if (!await WalkTo(player, new(point.X, player.GlobalPosition.Y, point.Y), $"service-gate-exit-{point.X}-{point.Y}")) return;
            GD.Print($"act1-discovery-walk: PASS loop=fap-service mode=physical-characterbody-walk distance={_walkedMeters:F2}m no-player-teleport=true");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
            return;
        }

        // Arrival -> house. Keep the authored signpost clear, then resolve the
        // door's connected-world position from its named interaction target.
        var arrivalTarget = FindInteraction(Interaction("arrival-enter-house"), GetTree().Root);
        if (arrivalTarget is null)
        {
            Fail("Act 1 walkthrough could not locate the connected-world arrival door.");
            return;
        }

        var expectedHouseExterior = AgentBAct1Layout.HouseExteriorSpawn;
        if (Mathf.Abs(arrivalTarget.GlobalPosition.X - expectedHouseExterior.X) > 0.01f
            || Mathf.Abs(arrivalTarget.GlobalPosition.Z - expectedHouseExterior.Z) > 0.01f)
        {
            Fail($"Act 1 walkthrough HouseDoor is not at the declared house exterior anchor: target={arrivalTarget.GlobalPosition}, expected={expectedHouseExterior}.");
            return;
        }

        // Follow the existing authored pedestrian chain onto the house path,
        // then use its declared axis through the open yard gate; a single
        // diagonal walk would cut across the terrain outside the collision-
        // safe route envelope.
        for (var index = 1; index <= 3; index++)
        {
            var pathPoint = AgentBAct1Layout.WalkChain[index];
            if (!await WalkTo(
                    player,
                    new Vector3(pathPoint.X, player.GlobalPosition.Y, pathPoint.Y),
                    $"arrival-house-path-{index}"))
            {
                return;
            }
        }

        for (var index = 0; index < AgentBAct1Layout.HousePathAxis.Length; index++)
        {
            var pathPoint = AgentBAct1Layout.HousePathAxis[index];
            if (!await WalkTo(
                    player,
                    new Vector3(pathPoint.X, player.GlobalPosition.Y, pathPoint.Y),
                    $"arrival-house-axis-{index}"))
            {
                return;
            }
        }

        var houseApproach = new Vector3(
            expectedHouseExterior.X,
            player.GlobalPosition.Y,
            expectedHouseExterior.Z + InteractionStandOff);
        if (!await WalkTo(player, houseApproach, "house-door-approach")
            || !await InteractAt(player, ray, Interaction("arrival-enter-house")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "house_old_pc", "house", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;

        // House -> Mansur -> old PC -> street. The household request is a
        // real player-facing gate: find Mansur with the camera ray, close the
        // dialogue through its existing UI owner, then approach the computer.
        if (!await InteractAt(player, ray, Interaction("talk-mansur")))
        {
            return;
        }
        await Frames(5);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (mansurDialogue is null || !mansurDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Mansur's request dialogue.");
            return;
        }

        var mansurChoices = mansurDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var offerHelp = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-offer-help"));
        var askWhy = mansurChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-mansur-why"));
        if (offerHelp is null || askWhy is null)
        {
            Fail("Mansur's start node did not expose both the offer-help and state-gated question choices.");
            return;
        }
        offerHelp.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        mansurDialogue.GetNode<Button>("Screen/Panel/Layout/Continue")
            .EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (mansurDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        {
            Fail("Closing Mansur's dialogue did not release the player or grant old-PC access.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("oldpc-power")))
        {
            return;
        }
        await Frames(6);
        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        if (oldPc is null || !oldPc.GetNode<Control>("Screen").Visible || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open the old-PC archive UI.");
            return;
        }

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House exit became available before the first old-PC clue was read.");
            return;
        }

        var oldPcResults = oldPc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var officialNoticeIndex = Enumerable.Range(0, oldPcResults.ItemCount)
            .FirstOrDefault(index => oldPcResults.GetItemMetadata(index).AsString() == OfficialNotice, -1);
        if (officialNoticeIndex < 0)
        {
            Fail("Physical walkthrough could not locate the official Marat notice in the old-PC archive.");
            return;
        }

        oldPcResults.EmitSignal(ItemList.SignalName.ItemSelected, officialNoticeIndex);
        await Frames(8);
        var oldPcState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(oldPcState, "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The official Marat notice confirmed, but the house exit unlocked before Gulsina's warning.");
            return;
        }

        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("House exit became available before the physical Gulsina warning dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-gulsina")))
        {
            return;
        }
        await Frames(5);
        var gulsinaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (gulsinaDialogue is null || !gulsinaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Gulsina's warning dialogue before leaving the house.");
            return;
        }
        var gulsinaChoices = gulsinaDialogue.GetNode<VBoxContainer>("Screen/Panel/Layout/Choices")
            .GetChildren().OfType<Button>().ToArray();
        var stayForTea = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-stay-for-tea"));
        var askYaramyy = gulsinaChoices.FirstOrDefault(button => button.Text == bridge.ResolveText("urman.chapter1:text/choice-gulsina-yaramyy"));
        if (stayForTea is null || askYaramyy is null)
        {
            Fail("Gulsina's start node did not expose both the tea alternative and guessed ярамый question.");
            return;
        }
        stayForTea.EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        gulsinaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue")
            .EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        var gulsinaState = bridge.SelectRuntimeState();
        if (gulsinaDialogue.IsOpen || player.ModalOpen
            || VocabularyStatus(gulsinaState, "tt_yaramyy") != "guessed"
            || !NpcState(gulsinaState, "gulsina", "warning_heard")
            || !bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Closing Gulsina's dialogue did not resolve ярамый, commit the warning state, or unlock the house exit.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("house-to-route")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "village_day", "crossroad_signs_inspect", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (HasFailed()) return;

        // The street conversation is a physical detective beat, not an
        // optional hint: Alsu's account must confirm the contradiction before
        // the FAP route becomes available.
        if (bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("FAP route became available before Alsu's contradiction dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-alsu")))
        {
            return;
        }
        await Frames(5);
        var alsuDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (alsuDialogue is null || !alsuDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Alsu's route dialogue before the FAP.");
            return;
        }
        alsuDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (alsuDialogue.IsOpen || player.ModalOpen
            || KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_versions_conflict") != "confirmed"
            || !bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("Closing Alsu's dialogue did not confirm the Marat contradiction or unlock the FAP route.");
            return;
        }

        // Street -> FAP is an actual walk along the authored branch, not an
        // interaction at the main-road sign followed by a clinic teleport.
        var fapEntry = FindInteraction(Interaction("route-to-fap"), GetTree().Root);
        var fapApron = AgentBAct1Layout.FapBranchAxis[^1];
        if (fapEntry is null || new Vector2(fapEntry.GlobalPosition.X,
                fapEntry.GlobalPosition.Z).DistanceTo(fapApron) > 0.01f)
        {
            Fail("FAP transition is not at the authored clinic entry apron.");
            return;
        }
        foreach (var point in AgentBAct1Layout.FapBranchAxis.SkipLast(1))
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                    $"fap-branch-{point.X}-{point.Y}")) return;
        }
        if (!await InteractAt(player, ray, Interaction("route-to-fap")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "fap_clinic", "fap_waiting_room_day", "res://scenes/zones/chapter1_fap_clinic.tscn");
        if (HasFailed()) return;

        // Naila's authored dialogue gates the document desk. Prove the desk is
        // unavailable first, then use the same physical dialogue UI path.
        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("FAP document desk became available before Naila's dialogue.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("talk-naila")))
        {
            return;
        }
        await Frames(5);
        var nailaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (nailaDialogue is null || !nailaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical walkthrough did not open Naila's dialogue before the document desk.");
            return;
        }
        nailaDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (nailaDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("Closing Naila's dialogue did not unlock the FAP document desk.");
            return;
        }

        // FAP -> official record -> street -> house. The document is opened and closed
        // through the same UI path, but no state is injected by the test.
        if (!await InteractAt(player, ray, Interaction("fap-to-document-desk"))
            || !await InteractAt(player, ray, Interaction("fap-document-desk-to-official-record")))
        {
            return;
        }
        await Frames(5);
        AssertDocument(OfficialNotice);
        CloseDocument();
        await Frames(3);

        if (!await InteractAt(player, ray, Interaction("official-leave-clinic")))
            return;
        await Frames(5);
        AssertState(main, bridge, "village_day", "evidence-official-death", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (HasFailed()) return;
        foreach (var point in AgentBAct1Layout.FapBranchAxis.Reverse().Skip(1))
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                $"fap-return-{point.X}-{point.Y}")) return;
            if (point == new Vector2(23f, -25f))
            {
                // Physically enter and leave the actual neighboring holding;
                // presentation-camera checkpoints alone cannot prove access.
                foreach (var yardPoint in new Vector2[]
                         { new(19f, -22.5f), new(24f, -17f), new(23f, -12f), new(21f, -8.5f),
                           new(23f, -12f), new(24f, -17f), new(19f, -22.5f), point })
                {
                    if (!await WalkTo(player, new(yardPoint.X, player.GlobalPosition.Y, yardPoint.Y),
                        $"east-holding-walk-{yardPoint.X}-{yardPoint.Y}")) return;
                    if (yardPoint == new Vector2(23f, -12f))
                    {
                        var position = player.GlobalPosition;
                        var ground = AgentBAct1HeightField.Ground(position.X, position.Z);
                        GD.Print($"yard-ground-check: player_y={position.Y:F3} ground_y={ground:F3}");
                        if (Math.Abs(position.Y - ground) > .12)
                        {
                            Fail("Yard movement does not follow the shared terrain surface.");
                            return;
                        }
                    }
                }
            }
        }
        // Persist the actual street position, not a fresh clinic/house spawn.
        var returnPosition = player.GlobalPosition;
        if (!await bridge.SaveSlotAsync("walk-fap-return")
            || !await bridge.LoadSlotAsync("walk-fap-return"))
        {
            Fail("Could not save and resume the FAP return walk.");
            return;
        }
        await Frames(5);
        AssertState(main, bridge, "village_day", "evidence-official-death", "res://scenes/zones/style_benchmark_day_street.tscn");
        if (player.GlobalPosition.DistanceTo(returnPosition) > 0.15f
            || !bridge.IsInteractionAvailable(Interaction("official-to-internal-register")))
        {
            Fail("FAP return resume lost the street position or house interaction.");
            return;
        }
        foreach (var point in AgentBAct1Layout.HousePathAxis)
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                $"house-return-{point.X}-{point.Y}")) return;
        }
        if (!await InteractAt(player, ray, Interaction("official-to-internal-register")))
        {
            return;
        }
        await Frames(5);
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        if (HasFailed()) return;

        // The second house visit checks that the physical route remains usable
        // after the evidence transition. Rinat and the shared archive steps
        // are kept here because they are the Act 1 state gates for the forest.
        await InteractAt(player, ray, Interaction("oldpc-power"));
        await Frames(4);
        var archive = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        var archiveRows = archive!.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var registerIndex = Enumerable.Range(0, archiveRows.ItemCount).Single(index => archiveRows.GetItemMetadata(index).AsString() == "urman.oldpc:document/rec_marat_case_register_conflict");
        archiveRows.EmitSignal(ItemList.SignalName.ItemSelected, registerIndex);
        await Frames(6);
        if (archive.ActiveDocumentId != "urman.oldpc:document/rec_marat_case_register_conflict")
        { Fail("The returning player could not read the internal register on the old PC."); return; }
        archive.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-records-contradiction"), new[] { OfficialNotice, "urman.oldpc:document/rec_marat_case_register_conflict" }))
        { Fail("The record comparison was rejected."); return; }
        if (!await InteractAt(player, ray, Interaction("internal-register-to-rinat")))
        {
            return;
        }
        await Frames(5);
        var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (dialogue is null || !dialogue.IsOpen)
        {
            Fail("Physical walkthrough did not open the Rinat dialogue.");
            return;
        }
        dialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);

        if (!await InteractAt(player, ray, Interaction("internal-register-to-saved-message")))
        {
            return;
        }
        await Frames(5);
        CloseDocument();
        await Frames(3);

        foreach (var interaction in new[]
        {
            "saved-message-to-boundary-source",
            "boundary-source-to-reread",
            "reread-to-edge-sketch"
        })
        {
            if (interaction == "boundary-source-to-reread"
                && !await bridge.CompareJournalSourcesAsync(Interaction("compare-voice-link"), new[] { "urman.oldpc:document/msg_marat_saved_last_normal", "urman.oldpc:document/tw_shurale_urman_boundary" }))
            { Fail("The voice comparison was rejected."); return; }
            if (!await InteractAt(player, ray, Interaction(interaction)))
            {
                return;
            }
            await Frames(5);
            CloseDocument();
            await Frames(3);
        }

        if (!await InteractAt(player, ray, Interaction("edge-sketch-to-zirat-road")))
        {
            return;
        }
        await Frames(4);
        AssertState(main, bridge, "zirat_road", "zirat-road", "res://scenes/zones/chapter1_zirat_road.tscn");
        if (HasFailed()) return;

        // Inspect the last inhabited holding via its open gate and side seni,
        // then return to the cemetery route without teleporting the player.
        foreach (var point in new Vector2[]
                 { new(0f, -59.5f), new(-3.8f, -59.5f), new(-12f, -59.5f),
                   new(-19f, -63.2f), new(-24f, -64.5f), new(-25f, -68.3f),
                   new(-28.6f, -67.4f), new(-25f, -68.3f), new(-24f, -64.5f),
                   new(-19f, -63.2f), new(-12f, -59.5f), new(-3.8f, -59.5f), new(0f, -59.5f) })
        {
            if (!await WalkTo(player, new Vector3(point.X, player.GlobalPosition.Y, point.Y),
                    $"zirat-holding-{point.X}-{point.Y}")) return;
        }

        // This is intentionally a long physical walk: it catches a broken
        // floor, a wrong collision layer or a bad spawn that teleport-based
        // tests cannot see.
        if (!await InteractAt(player, ray, Interaction("zirat-roadside-clue")))
        {
            return;
        }

        if (!await bridge.CompareJournalSourcesAsync(Interaction("compare-route-match"), new[] { "urman.oldpc:document/doc_kara_urman_edge_sketch", "urman.chapter1:knowledge/clue_zirat_roadside_marks" }))
        { Fail("The route comparison was rejected."); return; }
        var ziratState = bridge.SelectRuntimeState();
        if (KnowledgeStatus(ziratState, "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("Physical walkthrough inspected the zirat roadside clue without confirming Marat's last-route clue.");
            return;
        }

        var ziratClueTarget = FindInteraction(Interaction("zirat-roadside-clue"), GetTree().Root);
        if (ziratClueTarget is null
            || bridge.IsInteractionAvailable(Interaction("zirat-roadside-clue"))
            || ziratClueTarget.IsAvailable()
            || ziratClueTarget.CollisionLayer != 0
            || ziratClueTarget.CollisionMask != 0)
        {
            Fail("Confirmed zirat clue remained available or collidable after dispatch.");
            return;
        }

        if (!await InteractAt(player, ray, Interaction("zirat-road-to-forest")))
        {
            return;
        }
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        if (HasFailed()) return;

        for (var attempt = 0; attempt < 200 && bridge.IsInteractionAvailable("urman.chapter1:interaction/forest-rinat-intervention"); attempt++)
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
        var state = bridge.SelectRuntimeState();
        if (state.GetProperty("beats").GetProperty($"{ChapterPrefix}beat/cliffhanger-hard-cut").GetString() != "completed")
        {
            Fail("Physical walkthrough reached Kara-Urman without committing the Act 1 cliffhanger.");
            return;
        }

        GD.Print($"act1-first-person-walkthrough: PASS mode=physical-characterbody-walk (real movement/ray/input; distinct from the capture harness's presentation waypoint audit) distance={_walkedMeters:F2}m final-zone={bridge.CurrentZoneId} cliffhanger=completed");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task<bool> WalkTo(FirstPersonController player, Vector3 destination, string label)
    {
        var start = player.GlobalPosition;
        var initialDistance = HorizontalDistance(player.GlobalPosition, destination);
        if (initialDistance <= 0.30f)
        {
            return true;
        }

        SetYaw(player, YawTo(player.GlobalPosition, destination));
        var maxFrames = Math.Clamp((int)Math.Ceiling(initialDistance / Math.Max(player.WalkSpeed, 0.1f) * 60.0 * 2.4), 120, 1200);
        Input.ActionPress("move_forward");
        var lastBlockingShape = string.Empty;
        try
        {
            var lastProgressFrame = 0;
            var lastDistance = initialDistance;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var before = player.GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                for (var i = 0; i < player.GetSlideCollisionCount(); i++)
                {
                    var hit = player.GetSlideCollision(i);
                    if (Mathf.Abs(hit.GetNormal().Y) < 0.5f)
                        lastBlockingShape = (hit.GetColliderShape() as Node)?.GetPath().ToString() ?? hit.GetCollider().ToString();
                }
                _walkedMeters += HorizontalDistance(before, player.GlobalPosition);
                var currentDistance = HorizontalDistance(player.GlobalPosition, destination);
                if (currentDistance <= 0.30f)
                {
                    return true;
                }

                // The connected-world exterior terrain can hold the body on a
                // slope contact for a few frames; only a long no-progress run
                // is a real stall. Nudge the yaw toward the target so the
                // slide direction follows the remaining vector.
                if (Math.Abs(lastDistance - currentDistance) > 0.002f)
                {
                    lastProgressFrame = frame;
                    lastDistance = currentDistance;
                    SetYaw(player, YawTo(player.GlobalPosition, destination));
                }
                else if (frame - lastProgressFrame > 150)
                {
                    break;
                }
            }
        }
        finally
        {
            Input.ActionRelease("move_forward");
            await PhysicsFrames(2);
            // Optional evidence from the real walked positions, never camera
            // teleportation. Ordinary headless smoke pays no rendering cost.
            var captureDirectory = System.Environment.GetEnvironmentVariable("URMAN_WALK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(captureDirectory))
            {
                if (RenderingServer.GetRenderingDevice() is null
                    || !System.IO.Path.IsPathFullyQualified(captureDirectory)
                    || !System.IO.Directory.Exists(captureDirectory))
                {
                    Fail("Walk capture requires a rendering device and an existing absolute output directory.");
                }
                else
                {
                    await Frames(2);
                    RenderingServer.ForceDraw(false);
                    using var image = GetTree().Root.GetTexture().GetImage();
                    var frameName = label.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                    if (image.SavePng(System.IO.Path.Combine(captureDirectory, frameName + ".png")) != Error.Ok)
                        Fail($"Could not save physical walk frame {label}.");
                    GD.Print($"walk-frame: {label} actual-player-position={player.GlobalPosition}");
                    foreach (var node in GetTree().Root.FindChildren("*", "MeshInstance3D", true, false))
                    {
                        var mesh = (MeshInstance3D)node;
                        var meshName = mesh.Name.ToString();
                        if (!mesh.IsVisibleInTree() || mesh.Mesh is null
                            || !new[] { "Fence", "Gate", "Wall", "Door", "Foundation", "Roof" }
                                .Any(kind => meshName.Contains(kind, StringComparison.OrdinalIgnoreCase))) continue;
                        foreach (var h in new[] { .4f, .8f, 1.2f, 1.6f })
                        {
                            var from = mesh.ToLocal(start + Vector3.Up * h);
                            var to = mesh.ToLocal(player.GlobalPosition + Vector3.Up * h);
                            if (!mesh.GetAabb().IntersectsSegment(from, to)) continue;
                            // A wall AABB includes its deliberate doorway;
                            // only an actual triangle crossing is evidence.
                            var faces = mesh.Mesh.GetFaces();
                            var crossed = false;
                            for (var triangle = 0; triangle + 2 < faces.Length; triangle += 3)
                            {
                                if (Geometry3D.SegmentIntersectsTriangle(from, to, faces[triangle],
                                        faces[triangle + 1], faces[triangle + 2]).VariantType == Variant.Type.Nil) continue;
                                crossed = true;
                                break;
                            }
                            if (crossed)
                            {
                                GD.Print($"walk-obstacle: {label} height={h} mesh={mesh.GetPath()}");
                                break;
                            }
                        }
                    }
                }
            }
        }

        Fail($"Physical walkthrough could not reach {label}: start={start}, target={destination}, actual={player.GlobalPosition}, remaining={HorizontalDistance(player.GlobalPosition, destination):F2}m, blocker={lastBlockingShape}.");
        return false;
    }

    private async Task<bool> InteractAt(
        FirstPersonController player,
        RayCast3D ray,
        string interactionId)
    {
        var expected = FindInteraction(interactionId, GetTree().Root);
        if (expected is null)
        {
            Fail($"Physical walkthrough could not locate named InteractionTarget {interactionId} in the connected world.");
            return false;
        }

        var targetPosition = expected.GlobalPosition;
        // Naila is staged behind the left waiting bench; use the clear right
        // aisle for the production movement path before turning to her.
        var approach = interactionId == Interaction("talk-naila")
            ? new Vector3(expected.GlobalPosition.X + 1.8f, player.GlobalPosition.Y, expected.GlobalPosition.Z)
            : ApproachPosition(player, expected);
        if (!await WalkTo(player, approach, $"approach-{interactionId}"))
        {
            return false;
        }

        AimAt(player, targetPosition);
        await PhysicsFrames(3);
        ray.ForceRaycastUpdate();
        if (!ray.IsColliding() || ray.GetCollider() is not InteractionTarget target
            || target.InteractionId != interactionId || !target.IsAvailable())
        {
            var collider = ray.GetCollider();
            var actual = collider is InteractionTarget hit
                ? hit.InteractionId
                : collider is GodotObject objectHit
                    ? $"<{objectHit.GetType().Name}:{objectHit.GetInstanceId()}>"
                    : "<none>";
            var expectedInfo = expected is null
                ? "target-node=<none>"
                : $"target-node={expected.GlobalPosition} layer={expected.CollisionLayer} available={expected.IsAvailable()}";
            Fail($"Physical walkthrough ray expected {interactionId}, got {actual}, colliding={ray.IsColliding()}; player={player.GlobalPosition}, connected-world target={targetPosition}, yaw={player.RotationDegrees.Y:F1}, pitch={_lookPitch:F1}, ray={ray.TargetPosition}, {expectedInfo}.");
            return false;
        }

        await PressInteract();
        await Frames(6);
        return true;
    }

    private void AimAt(FirstPersonController player, Vector3 target)
    {
        var cameraPosition = player.GlobalPosition + Vector3.Up * 1.7f;
        var delta = target - cameraPosition;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        // Godot's positive Y rotation turns local -Z toward -X, so negate X
        // when converting a world-space target to the controller's yaw.
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
        var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, Math.Max(horizontal, 0.001f)));
        SetYaw(player, yaw);
        // The smoke owns no real mouse device. Apply only the controller's
        // test-only look state; body position remains physics-driven.
        player.ApplySmokeLook(pitch, yaw);
        _lookPitch = pitch;
    }

    private static Vector3 ApproachPosition(FirstPersonController player, InteractionTarget target)
    {
        var delta = player.GlobalPosition - target.GlobalPosition;
        var direction = Mathf.Abs(delta.Z) >= Mathf.Abs(delta.X)
            ? new Vector3(0f, 0f, Mathf.Sign(delta.Z))
            : new Vector3(Mathf.Sign(delta.X), 0f, 0f);
        if (direction == Vector3.Zero)
        {
            direction = new Vector3(0f, 0f, 1f);
        }

        return new Vector3(
            target.GlobalPosition.X + direction.X * InteractionStandOff,
            player.GlobalPosition.Y,
            target.GlobalPosition.Z + direction.Z * InteractionStandOff);
    }

    private void SetYaw(FirstPersonController player, float yaw)
    {
        player.RotationDegrees = new Vector3(0, yaw, 0);
    }

    private void AssertState(Main main, RuntimeBridge bridge, string zone, string sceneLocalId, string scenePath)
    {
        if (main.ActiveZoneScenePath != scenePath
            || bridge.CurrentZoneId != zone
            || bridge.ActiveSceneId != $"{ChapterPrefix}scene/{sceneLocalId}")
        {
            Fail($"Physical walkthrough state mismatch: zone={bridge.CurrentZoneId}, scene={bridge.ActiveSceneId}, path={main.ActiveZoneScenePath}; expected {zone}/{sceneLocalId}/{scenePath}.");
        }
    }

    private void AssertDocument(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        if (ui is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Physical walkthrough did not open {documentId}.");
        }
    }

    private void CloseDocument()
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        ui?.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
    }

    private async Task PressInteract()
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = true,
            Echo = false
        });
        await PhysicsFrames(1);
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = Key.E,
            PhysicalKeycode = Key.E,
            Pressed = false,
            Echo = false
        });
        await PhysicsFrames(2);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private static InteractionTarget? FindInteraction(string interactionId, Node node)
    {
        if (node is InteractionTarget target && target.InteractionId == interactionId)
        {
            return target;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindInteraction(interactionId, child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to) =>
        new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private static float YawTo(Vector3 from, Vector3 to) =>
        Mathf.RadToDeg(Mathf.Atan2(-(to.X - from.X), -(to.Z - from.Z)));

    private static string KnowledgeStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string VocabularyStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("vocabulary").GetProperty($"{ChapterPrefix}vocabulary/{localId}").GetProperty("status").GetString()!;

    private static bool NpcState(System.Text.Json.JsonElement state, string localId, string stateKey) =>
        state.GetProperty("npc").GetProperty($"{ChapterPrefix}character/{localId}").GetProperty(stateKey).GetBoolean();

    private bool HasFailed() => _failed;

    private void Fail(string message)
    {
        _failed = true;
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
