using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Exercises the compact Act 1 detective corridor through the same production
/// path a player uses: first-person camera ray, physical target, mapped E input,
/// zone transition and diegetic document/dialogue UI. This is intentionally a
/// deterministic smoke route, not a substitute for a human playthrough.
/// </summary>
public partial class Act1FirstPersonCorridorSmokeTest : Node
{
    private const string ChapterPrefix = "urman.chapter1:";
    private const string OfficialNotice = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string SavedMessage = "urman.oldpc:document/msg_marat_saved_last_normal";
    private const string BoundarySource = "urman.oldpc:document/tw_shurale_urman_boundary";
    private const string EdgeSketch = "urman.oldpc:document/doc_kara_urman_edge_sketch";
    private const float InteractionStandOff = 1.5f;

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 first-person corridor could not instantiate the demo entrypoint.");
            return;
        }

        AddChild(demo);
        await Frames(8);

        if (!await this.StartThroughMainMenuAsync(demo))
        {
            Fail("Act 1 first-person corridor could not start through the main menu.");
            return;
        }

        var main = demo.DemoMain;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = main.GetNodeOrNull<FirstPersonController>("Player");
        var ray = player?.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay");
        if (bridge is null || player is null || ray is null || !demo.IntroVisible)
        {
            Fail("Act 1 first-person corridor did not expose the demo bridge, player, ray and intro.");
            return;
        }

        if (!main.EnableAct1ConnectedWorld || main.ConnectedWorld is null)
        {
            Fail("Act 1 first-person corridor did not start through the connected-world demo mode.");
            return;
        }

        // The launch smoke owns the intro-specific control wording; here we
        // dismiss it through the same mapped keyboard event as a player.
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
            Fail("Act 1 first-person corridor could not dismiss its intro card with E.");
            return;
        }

        await InteractAt(player, ray, Interaction("arrival-enter-house"));
        await Frames(4);
        AssertState(main, bridge, "house_old_pc", "house", "res://scenes/zones/style_benchmark_house_pc.tscn");
        AssertRouteFacing(main, 0f, "house-entry");

        await InteractAt(player, ray, Interaction("talk-mansur"));
        await Frames(5);
        var mansurDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (mansurDialogue is null || !mansurDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Mansur's request dialogue before the old PC.");
            return;
        }
        mansurDialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (mansurDialogue.IsOpen || player.ModalOpen
            || !bridge.IsInteractionAvailable(Interaction("oldpc-power")))
        {
            Fail("Mansur's dialogue did not release the player or grant old-PC access in the corridor.");
            return;
        }

        await InteractAt(player, ray, Interaction("oldpc-power"));
        await Frames(6);
        var oldPc = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi;
        if (oldPc is null || !oldPc.GetNode<Control>("Screen").Visible || !player.ModalOpen)
        {
            Fail("Physical old-PC target did not open the archive UI in the corridor.");
            return;
        }

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Corridor house exit became available before the first old-PC clue was read.");
            return;
        }

        var oldPcResults = oldPc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var officialNoticeIndex = Enumerable.Range(0, oldPcResults.ItemCount)
            .FirstOrDefault(index => oldPcResults.GetItemMetadata(index).AsString() == OfficialNotice, -1);
        if (officialNoticeIndex < 0)
        {
            Fail("Corridor could not locate the official Marat notice in the old-PC archive.");
            return;
        }

        oldPcResults.EmitSignal(ItemList.SignalName.ItemSelected, officialNoticeIndex);
        await Frames(8);
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_official_death_version") != "confirmed"
            || bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("The official Marat notice confirmed, but the corridor house exit unlocked before Gulsina's warning.");
            return;
        }

        oldPc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(3);

        if (bridge.IsInteractionAvailable(Interaction("house-to-route")))
        {
            Fail("Corridor house exit became available before the physical Gulsina warning dialogue.");
            return;
        }

        await InteractAt(player, ray, Interaction("talk-gulsina"));
        await Frames(5);
        var gulsinaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (gulsinaDialogue is null || !gulsinaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Gulsina's warning dialogue before leaving the house.");
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

        await InteractAt(player, ray, Interaction("house-to-route"));
        await Frames(4);
        AssertState(main, bridge, "village_day", "crossroad_signs_inspect", "res://scenes/zones/style_benchmark_day_street.tscn");
        AssertRouteFacing(main, 0f, "house-exit-to-fap");

        if (bridge.IsInteractionAvailable(Interaction("route-to-fap")))
        {
            Fail("FAP route became available before Alsu's contradiction dialogue.");
            return;
        }
        await InteractAt(player, ray, Interaction("talk-alsu"));
        await Frames(5);
        var alsuDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (alsuDialogue is null || !alsuDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Alsu's route dialogue before the FAP.");
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

        await InteractAt(player, ray, Interaction("route-to-fap"));
        await Frames(4);
        AssertState(main, bridge, "fap_clinic", "fap_waiting_room_day", "res://scenes/zones/chapter1_fap_clinic.tscn");
        AssertRouteFacing(main, 0f, "fap-entry-to-desk");

        if (bridge.IsInteractionAvailable(Interaction("fap-to-document-desk")))
        {
            Fail("FAP document desk became available before Naila's dialogue.");
            return;
        }
        await InteractAt(player, ray, Interaction("talk-naila"));
        await Frames(5);
        var nailaDialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (nailaDialogue is null || !nailaDialogue.IsOpen || !player.ModalOpen)
        {
            Fail("Physical corridor did not open Naila's dialogue before the document desk.");
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

        await InteractAt(player, ray, Interaction("fap-to-document-desk"));
        await Frames(3);

        await InteractAt(player, ray, Interaction("fap-document-desk-to-official-record"));
        await Frames(6);
        AssertDocument(OfficialNotice);
        await SaveOpenDocumentToJournal(OfficialNotice);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("official-to-internal-register"));
        await Frames(5);
        AssertState(main, bridge, "house_old_pc", "evidence-internal-register", "res://scenes/zones/style_benchmark_house_pc.tscn");
        AssertRouteFacing(main, 0f, "return-to-house-pc");

        await InteractAt(player, ray, Interaction("internal-register-to-rinat"));
        await Frames(5);
        var dialogue = GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi;
        if (dialogue is null || !dialogue.IsOpen)
        {
            Fail("Physical Rinat target did not open the authored dialogue UI.");
            return;
        }
        dialogue.GetNode<Button>("Screen/Panel/Layout/Continue").EmitSignal(Button.SignalName.Pressed);
        await Frames(4);
        if (dialogue.IsOpen || !RinatAlerted(bridge.SelectRuntimeState()))
        {
            Fail("Rinat dialogue did not close cleanly or commit the alerted state.");
            return;
        }

        await InteractAt(player, ray, Interaction("internal-register-to-saved-message"));
        await Frames(6);
        AssertDocument(SavedMessage);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("saved-message-to-boundary-source"));
        await Frames(6);
        AssertDocument(BoundarySource);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("boundary-source-to-reread"));
        await Frames(6);
        AssertDocument(BoundarySource);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("reread-to-edge-sketch"));
        await Frames(6);
        AssertDocument(EdgeSketch);
        CloseDocument();
        await Frames(3);

        await InteractAt(player, ray, Interaction("edge-sketch-to-zirat-road"));
        await Frames(4);
        AssertState(main, bridge, "zirat_road", "zirat-road", "res://scenes/zones/chapter1_zirat_road.tscn");
        AssertRouteFacing(main, 0f, "zirat-entry-to-forest");

        await InteractAt(player, ray, Interaction("zirat-roadside-clue"));
        if (KnowledgeStatus(bridge.SelectRuntimeState(), "clue_marat_last_route_near_zirat") != "confirmed")
        {
            Fail("Corridor zirat roadside interaction did not confirm Marat's last-route clue.");
            return;
        }

        await InteractAt(player, ray, Interaction("zirat-road-to-forest"));
        await Frames(8);
        AssertState(main, bridge, "kara_urman_night", "forest", "res://scenes/zones/style_benchmark_kara_urman_night.tscn");
        AssertRouteFacing(main, 0f, "forest-entry-to-cliffhanger");
        var state = bridge.SelectRuntimeState();
        if (state.GetProperty("beats").GetProperty($"{ChapterPrefix}beat/cliffhanger-hard-cut").GetString() != "completed"
            || state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/clue_do_not_answer_rule").GetProperty("status").GetString() != "confirmed")
        {
            Fail("First-person corridor reached Kara-Urman without committing the Act 1 cliffhanger.");
            return;
        }

        GD.Print("act1-first-person-corridor: arrival -> old PC -> FAP document -> Rinat dialogue -> evidence documents -> zirat -> Kara-Urman cliffhanger");
        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(0);
    }

    private async Task InteractAt(
        FirstPersonController player,
        RayCast3D ray,
        string interactionId)
    {
        var expected = FindInteraction(interactionId, GetTree().Root);
        if (expected is null)
        {
            Fail($"First-person corridor could not locate named InteractionTarget {interactionId} in the connected world.");
            return;
        }

        var targetPosition = expected.GlobalPosition;
        var playerPosition = ApproachPosition(player, expected);
        var cameraPosition = playerPosition + Vector3.Up * 1.7f;
        var delta = targetPosition - cameraPosition;
        var horizontal = new Vector2(delta.X, delta.Z).Length();
        var pitch = Mathf.RadToDeg(Mathf.Atan2(delta.Y, horizontal));
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z));
        player.ApplyPortableTransform(new PlayerTransform(
            new(playerPosition.X, playerPosition.Y, playerPosition.Z),
            new(pitch, yaw, 0)));
        await PhysicsFrames(3);
        ray.ForceRaycastUpdate();
        if (!ray.IsColliding() || ray.GetCollider() is not InteractionTarget target
            || target.InteractionId != interactionId || !target.IsAvailable())
        {
            var actual = ray.GetCollider() is InteractionTarget hit ? hit.InteractionId : "<none>";
            var expectedInfo = $"target-node={expected.GlobalPosition} layer={expected.CollisionLayer} available={expected.IsAvailable()}";
            Fail($"First-person corridor ray expected {interactionId}, got {actual} at connected-world target {targetPosition}; player={player.GlobalPosition} rayEnabled={ray.Enabled} {expectedInfo}.");
            return;
        }

        await PressInteract();
        await Frames(6);
    }

    private void AssertDocument(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        if (ui is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Physical evidence interaction did not open {documentId}.");
        }
    }

    private void CloseDocument()
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        ui?.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
    }

    private async Task SaveOpenDocumentToJournal(string documentId)
    {
        var ui = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (ui is null || bridge is null || !ui.IsOpen || ui.OpenDocumentId != documentId)
        {
            Fail($"Cannot save physical document {documentId}: DocumentUi is not open on the expected record.");
            return;
        }

        ui.GetNode<Button>("Screen/Document/Layout/Footer/Save").EmitSignal(Button.SignalName.Pressed);
        await Frames(8);
        if (!bridge.JournalEntries().Any(entry => entry.EntryId == documentId))
        {
            Fail($"Physical DocumentUi save did not add {documentId} to the shared journal.");
        }
    }

    private static bool RinatAlerted(System.Text.Json.JsonElement state) =>
        state.GetProperty("npc").GetProperty($"{ChapterPrefix}character/rinat")
            .GetProperty("alerted").GetBoolean();

    private static string KnowledgeStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("knowledge").GetProperty($"{ChapterPrefix}knowledge/{localId}").GetProperty("status").GetString()!;

    private static string VocabularyStatus(System.Text.Json.JsonElement state, string localId) =>
        state.GetProperty("vocabulary").GetProperty($"{ChapterPrefix}vocabulary/{localId}").GetProperty("status").GetString()!;

    private static bool NpcState(System.Text.Json.JsonElement state, string localId, string stateKey) =>
        state.GetProperty("npc").GetProperty($"{ChapterPrefix}character/{localId}").GetProperty(stateKey).GetBoolean();

    private void AssertRouteFacing(Main main, float expectedYaw, string transition)
    {
        var difference = Mathf.Abs(Mathf.PosMod(main.LastSpawnYawDegrees - expectedYaw + 180f, 360f) - 180f);
        if (difference > 2f)
        {
            Fail($"Act 1 route transition {transition} declared spawn yaw {main.LastSpawnYawDegrees:0.0}°, expected {expectedYaw:0.0}° toward the next landmark.");
        }
    }

    private void AssertState(Main main, RuntimeBridge bridge, string zone, string sceneLocalId, string scenePath)
    {
        if (main.ActiveZoneScenePath != scenePath
            || bridge.CurrentZoneId != zone
            || bridge.ActiveSceneId != $"{ChapterPrefix}scene/{sceneLocalId}")
        {
            Fail($"First-person corridor state mismatch: zone={bridge.CurrentZoneId}, scene={bridge.ActiveSceneId}, path={main.ActiveZoneScenePath}; expected {zone}/{sceneLocalId}/{scenePath}.");
        }
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

    private static string Interaction(string localId) => $"{ChapterPrefix}interaction/{localId}";

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
