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

        // Street -> FAP. Resolve the branch target from the connected world so
        // the physical walk follows the current authored route placement.
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

        // FAP -> official record -> house. The document is opened and closed
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

        // This is intentionally a long physical walk: it catches a broken
        // floor, a wrong collision layer or a bad spawn that teleport-based
        // tests cannot see.
        if (!await InteractAt(player, ray, Interaction("zirat-roadside-clue")))
        {
            return;
        }

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
        try
        {
            var lastProgressFrame = 0;
            var lastDistance = initialDistance;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                var before = player.GlobalPosition;
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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
        }

        Fail($"Physical walkthrough could not reach {label}: start={start}, target={destination}, actual={player.GlobalPosition}, remaining={HorizontalDistance(player.GlobalPosition, destination):F2}m.");
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
