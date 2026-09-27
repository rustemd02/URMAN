using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Regression pass over the optional Tamara Gennadievna fence quest: impact
/// thresholds, the one-shot crash transaction, board pickups, partial and full
/// hand-over, load safety and the repaired fence. Uses the ordinary New Game
/// session and its real runtime owners; saves go to the guarded test session.
/// </summary>
public partial class TamaraFenceSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private FirstPersonController _player = null!;
    private TamaraFenceQuest _quest = null!;
    private VehicleController _niva = null!;
    private int _checks;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            if (System.Environment.GetEnvironmentVariable("URMAN_TAMARA_COLD_LOAD") == "1")
            {
                await CheckColdContinueAsync();
                GD.Print($"tamara-fence-cold-load: PASS {_checks} checks; pid={System.Environment.ProcessId}");
                exit = 0;
                return;
            }
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(30);
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            _quest = TamaraFenceQuest.Current(GetTree())
                ?? throw new InvalidOperationException("Tamara fence quest was not built with the world.");
            var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
            _niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");
            await Frames(5);

            // --- built world -------------------------------------------------
            Require(_quest.Panels.Count == 5, "five authored fence panels");
            Require(_quest.Panels.All(panel => panel.Body.CollisionLayer == 1u),
                "intact panels physically stop the car");
            Require(_quest.BoardTargets.Count == 6, "six board pickup targets");
            Require(_quest.HandInTarget is not null, "hand-over target exists");
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: false, Carried: 0, Delivered: 0 },
                "quest starts invisible: no crash, no boards");
            Require(!_bridge.ActiveObjectives().Any(objective => objective.QuestId == TamaraFenceQuest.QuestId),
                "no tamara objective before the crash");
            // The fence face must sit inside the car's physical reach band.
            var world = (Act1ConnectedWorld)GetTree().GetFirstNodeInGroup("act1_connected_world");
            Require(world.CanVehicleTraverse(new(-0.1f, 0f, -44f), new(1.85f, 0f, -44f),
                SettlementTravelMode.Car, out _), "the Niva's road reach covers the fence face");
            Require(await _bridge.SaveSlotAsync("tamara-before"), "pre-crash state saved");

            // --- weak hit does nothing --------------------------------------
            var panel = _quest.Panels[0];
            _quest.DebugHandleHardStop(_niva, 1.2f, panel.Body);
            await Frames(3);
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: false }, "a light touch is not a crash");
            Require(!_quest.FenceBreakApplied, "a light touch leaves the fence standing");

            // --- strong hit: crash, cutscene, skip ---------------------------
            _quest.AccelerateCutsceneForTest(1);
            _quest.DebugHandleHardStop(_niva, 4.6f, panel.Body);
            for (var frames = 0; frames < 600 && _quest.ActiveCutscene is null; frames++)
            {
                await Frames(1);
            }
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: true }, "the crash commits immediately");
            Require(_quest.FenceBreakApplied, "the impacted panels break");
            var brokenPanels = _quest.Panels.Select(candidate => candidate.Broken).ToArray();
            Require(_quest.ActiveCutscene is not null, "the scene starts before checking skip");
            {
                var scene = _quest.ActiveCutscene!;
                var camera = GetViewport().GetCamera3D();
                var initialPosition = camera.GlobalPosition;
                var maxSway = 0f;
                var until = Time.GetTicksMsec() + 500;
                while (Time.GetTicksMsec() < until)
                {
                    await Frames(1);
                    maxSway = Math.Max(maxSway, camera.GlobalPosition.DistanceTo(initialPosition));
                }
                Require(maxSway < .08f, "handheld sway stays around the authored camera position");
                var captionShown = false;
                scene.SetLineListenerForTest(_ => captionShown = true);
                until = Time.GetTicksMsec() + 4000;
                while (!captionShown && Time.GetTicksMsec() < until) await Frames(1);
                Require(captionShown, "a caption is active before skip");
                Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = true });
                await Frames(1);
                Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = false });
                until = Time.GetTicksMsec() + 400;
                while (_player.ModalOpen && Time.GetTicksMsec() < until) await Frames(1);
                Require(!_player.ModalOpen && GetViewport().GetCamera3D() != camera,
                    "skip cancels an active caption and restores the camera immediately");
            }

            for (var frames = 0; frames < 900 && _quest.ActiveCutscene is not null; frames++)
            {
                await Frames(1);
            }
            Require(_quest.ActiveCutscene is null, "the cutscene ends after a skip");
            Require(!_player.ModalOpen, "player control returns after the skip");
            Require(_bridge.ActiveObjectives().Any(objective => objective.QuestId == TamaraFenceQuest.QuestId),
                "the quest objective is live after the scene");
            var snapshot = _bridge.TamaraFenceSnapshotNow()!;
            Require(snapshot is { Carried: 0, Delivered: 0 }, "no boards yet after the crash");

            // --- repeated physics callbacks ----------------------------------
            _quest.DebugHandleHardStop(_niva, 5.0f, _quest.Panels[1].Body);
            await Frames(3);
            Require(_quest.ActiveCutscene is null, "a second impact never restarts the accident");

            // --- pickups and partial hand-over -------------------------------
            Require(await _bridge.DispatchWorldPropsAsync(System.Text.Json.Nodes.JsonNode.Parse(
                "[{\"propId\":\"tamara/board/6\",\"taken\":true}]")!.AsArray()), "legacy taken-place fixture applied");
            Require(!_bridge.IsInteractionAvailable("urman.chapter1:interaction/tamara-fence-take-board-6")
                && !await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/tamara-fence-take-board-6"),
                "legacy taken place rejects both presentation and direct pickup");
            Require(await _bridge.DispatchWorldPropsAsync(System.Text.Json.Nodes.JsonNode.Parse(
                "[{\"propId\":\"tamara/board/6\",\"taken\":false}]")!.AsArray()), "legacy fixture cleared");
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/tamara-fence-take-board-1"),
                "first board picked through its authored interaction");
            Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/tamara-fence-take-board-2"),
                "second board picked");
            Require(_bridge.TamaraFenceSnapshotNow() is { Carried: 2, Delivered: 0 }, "two boards carried");
            var board1 = _quest.BoardTargets[0];
            Require(!board1.IsAvailable(), "a picked spot cannot be picked again");
            Require(await _bridge.SaveSlotAsync("tamara-mid"), "partial progress saved");
            Require(await _bridge.TamaraFenceDeliverAsync(), "partial hand-over commits");
            Require(_bridge.TamaraFenceSnapshotNow() is { Carried: 0, Delivered: 2 }, "two boards delivered");
            Require(!board1.IsAvailable()
                && !await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/tamara-fence-take-board-1"),
                "partial delivery never respawns or reuses the same board");
            var dialogue = _bridge.RequireDialogue("urman.chapter1:dialogue/tamara_fence_hand_in");
            Require(_bridge.ResolveDialogueStartNodeId(dialogue) == "hand-2",
                "the hand-over dialogue reads the new count");
            Require(await _bridge.SaveSlotAsync("tamara-delivered"), "partial delivery saved");
            Require(await _bridge.LoadSlotAsync("tamara-delivered"), "partial delivery slot loads");
            await Frames(20);
            Require(_bridge.TamaraFenceSnapshotNow() is { Carried: 0, Delivered: 2 }
                && !_quest.BoardTargets[0].IsAvailable()
                && !await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/tamara-fence-take-board-1"),
                "loading partial delivery preserves the count and permanently taken boards");

            // --- loading mid-quest is safe -----------------------------------
            Require(await _bridge.LoadSlotAsync("tamara-mid"), "mid-quest slot loads");
            await Frames(20);
            Require(!_player.ModalOpen, "a load never leaves the camera locked");
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: true, Carried: 2, Delivered: 0 },
                "the loaded slot restores its own progress");
            Require(_quest.FenceBreakApplied, "the loaded world shows the broken fence");
            Require(_quest.Panels.Select(candidate => candidate.Broken).SequenceEqual(brokenPanels),
                "loading preserves the actual off-centre impact");
            Require(_quest.BoardTargets[0].IsAvailable() == false, "the loaded spot stays empty");

            // A modal at the shed must not silently consume the board remark.
            var previousPosition = _player.GlobalPosition;
            _player.SetModalOpen(true);
            _player.GlobalPosition = ((Node3D)_quest.BoardTargets[2].GetParent()).GlobalPosition;
            await Frames(8);
            Require(!_quest.RemarkSaid, "the board remark waits while a modal is open");
            _player.SetModalOpen(false);
            for (var frame = 0; frame < 30 && !_quest.RemarkSaid; frame++) await Frames(1);
            Require(_quest.RemarkSaid, "the board remark remains available after closing the modal");
            _player.GlobalPosition = previousPosition;

            // --- finish the quest --------------------------------------------
            // The loaded slot already carries boards 1 and 2; only the rest exist.
            for (var board = 3; board <= 6; board++)
            {
                Require(await _bridge.DispatchInteractionAsync(
                    $"urman.chapter1:interaction/tamara-fence-take-board-{board}"), $"board {board} pickup");
            }
            Require(_bridge.TamaraFenceSnapshotNow() is { Carried: 6 },
                "six boards carried after the load restored the first two");
            _player.GlobalPosition = new(-.2f, .1f, -44f);
            Require(await _bridge.TamaraFenceDeliverAsync(), "final hand-over commits");
            await Frames(5);
            Require(!_quest.FenceRepairApplied && _quest.FenceBreakApplied,
                "delivery does not rebuild the fence in front of the player");
            Require(_bridge.TamaraFenceSnapshotNow() is { Delivered: 6, Repaired: true },
                "six delivered boards complete and repair the fence");
            Require(!_bridge.ActiveObjectives().Any(objective => objective.QuestId == TamaraFenceQuest.QuestId),
                "the quest leaves the objective list");
            Require(_bridge.JournalEntries().Any(entry =>
                entry.EntryId == "urman.chapter1:knowledge/tamara_fence_settled"),
                "the notebook keeps a record of the settled fence");
            Require(!_quest.BoardTargets[0].IsAvailable(), "no pickups after the repair");
            Require(!await _bridge.TamaraFenceDeliverAsync(), "no hand-over after the repair");
            var settledEntries = _bridge.JournalEntries().Count(entry => entry.EntryId == "urman.chapter1:knowledge/tamara_fence_settled");
            Require(_quest.HandInTarget is { IsRepeatOnly: true } repeat && repeat.IsAvailable(),
                "Tamara remains available for a conversation after all boards are delivered");
            _quest.HandInTarget!.Interact();
            await Frames(3);
            var afterDialogue = (DialogueUi)GetTree().GetFirstNodeInGroup("dialogue_ui");
            Require(afterDialogue.IsOpen && _player.ModalOpen, "repeat conversation opens the ordinary dialogue UI");
            afterDialogue._UnhandledInput(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
            await Frames(2);
            Require(!_player.ModalOpen && _bridge.JournalEntries().Count(entry => entry.EntryId == "urman.chapter1:knowledge/tamara_fence_settled") == settledEntries,
                "closing the repeat releases controls without another journal reward");

            // --- the quiet callback: the Niva rolls past, the phone rises -----
            _quest.DebugExposeGuyForTest(out var guy, out _, out _, out var phone);
            guy.Visible = true;
            _niva.GlobalPosition = new(1.4f, 0f, -43.9f);
            _niva.RotationDegrees = new(0, 78f, 0);
            _niva.SetPlacementAvailability(true);
            _player.SetModalOpen(true);
            _player.SetVehicleControl(true);
            _niva.Enter(_player, restore: true);
            _player.GlobalPosition = _niva.ToGlobal(_niva.Definition.Seat);
            await Frames(8);
            Require(!_quest.QuietCallbackDone, "the phone callback waits while the driver's modal is open");
            _player.SetModalOpen(false);
            for (var frames = 0; frames < 120 && !_quest.QuietCallbackDone; frames++) await Frames(1);
            Require(_quest.QuietCallbackDone, "the passing Niva triggers the silent phone callback");
            Require(phone is not null && phone.Visible, "the phone rises during the callback");
            _player.SetVehicleControl(false);

            // --- the mended fence --------------------------------------------
            _player.GlobalPosition = new(-2f, .1f, -8f);
            for (var frames = 0; frames < 30 && !_quest.FenceRepairApplied; frames++) await Frames(1);
            Require(_quest.FenceRepairApplied, "the fence repairs once the yard is unattended");
            Require(_quest.Panels.All(candidate => candidate.Body.CollisionLayer == 1u),
                "the repaired fence is solid again");

            Require(await _bridge.SaveSlotAsync("tamara-done"), "finished quest saves");
            Require(await _bridge.LoadSlotAsync("tamara-done"), "finished quest loads");
            await Frames(20);
            Require(_bridge.TamaraFenceSnapshotNow() is { Delivered: 6, Repaired: true }
                    && _quest.HandInTarget is { IsRepeatOnly: true } loadedRepeat && loadedRepeat.IsAvailable(),
                "the finished state is stable across a load");
            Require(_quest.Panels.All(candidate => candidate.Body.CollisionLayer == 1u),
                "the repaired fence survives the load");

            Require(await _bridge.LoadSlotAsync("tamara-mid"), "load unfinished quest after repair");
            await Frames(10);
            Require(!_quest.FenceRepairApplied
                && _quest.Panels.Select(candidate => candidate.Broken).SequenceEqual(brokenPanels),
                "reverse loading restores the original breach and resets repair presentation");
            Require(await _bridge.LoadSlotAsync("tamara-before"), "load before the accident");
            await Frames(10);
            Require(!_quest.FenceBreakApplied && !_quest.FenceRepairApplied
                && _quest.Panels.All(candidate => candidate.Body.CollisionLayer == 1u
                    && candidate.Root.Transform.IsEqualApprox(candidate.IntactTransform)),
                "pre-crash load restores the intact fence and collision");

            _quest.AccelerateCutsceneForTest(25);
            _quest.DebugHandleHardStop(_niva, 4.6f, panel.Body);
            for (var frames = 0; frames < 600 && _quest.ActiveCutscene is null; frames++) await Frames(1);
            Require(_quest.ActiveCutscene is not null, "a fresh session can start the accident again");
            for (var frames = 0; frames < 600
                && _quest.TamaraActor.FindChild("TamaraConversationLook", true, false) is null
                && _quest.ActiveCutscene is not null; frames++) await Frames(1);
            Require(_quest.TamaraActor.FindChild("TamaraConversationLook", true, false) is LookAtModifier3D,
                "the active conversation owns a head-look modifier before loading");
            Require(await _bridge.LoadSlotAsync("tamara-before"), "load while the accident is active");
            for (var frames = 0; frames < 120 && _quest.ActiveCutscene is not null; frames++) await Frames(1);
            Require(_quest.ActiveCutscene is null && !_player.ModalOpen
                && _bridge.TamaraFenceSnapshotNow() is { Crashed: false }
                && !_quest.TamaraActor.Visible && !_quest.GuyActor.Visible
                && _quest.TamaraActor.FindChild("TamaraConversationLook", true, false) is null,
                $"the cancelled scene leaves the loaded session untouched (scene={_quest.ActiveCutscene is not null}, modal={_player.ModalOpen}, crashed={_bridge.TamaraFenceSnapshotNow()?.Crashed}, tamara={_quest.TamaraActor.Visible}, guy={_quest.GuyActor.Visible})");
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
            Require(_quest.Panels.All(candidate => candidate.Root.Transform.IsEqualApprox(candidate.IntactTransform)),
                "old impact tweens cannot move the restored fence");

            // Loading a visible unfinished quest during arrival must project Idle,
            // rather than carry Walk over from the cancelled scene.
            _quest.AccelerateCutsceneForTest(4);
            _quest.DebugHandleHardStop(_niva, 4.6f, panel.Body);
            _quest.DebugExposeGuyForTest(out _, out _, out var turnPlayer, out _);
            for (var frames = 0; frames < 600
                && turnPlayer?.CurrentAnimation != "PhoneGuy_Walk"; frames++) await Frames(1);
            Require(_quest.ActiveCutscene is not null && turnPlayer?.CurrentAnimation == "PhoneGuy_Walk",
                "arrival is interrupted during the guy's walk");
            Require(await _bridge.LoadSlotAsync("tamara-mid"), "unfinished quest loads during arrival");
            var loadedGuyPosition = _quest.GuyActor.GlobalPosition;
            var loadedGuyRotation = _quest.GuyActor.Rotation;
            await Frames(20);
            Require(_quest.ActiveCutscene is null && !_player.ModalOpen && _quest.GuyActor.Visible
                && turnPlayer?.CurrentAnimation == "PhoneGuy_Idle"
                && _quest.GuyActor.GlobalPosition.IsEqualApprox(loadedGuyPosition)
                && _quest.GuyActor.Rotation.IsEqualApprox(loadedGuyRotation)
                && _quest.TamaraActor.FindChild("TamaraConversationLook", true, false) is null,
                "loaded visible guy holds Idle and restored placement after the old arrival is cancelled");

            exit = 0;
            GD.Print($"tamara-fence-smoke: {_checks} checks passed");
        }
        catch (Exception error)
        {
            GD.PushError("tamara-fence-smoke failed: " + error);
            GD.Print(error.StackTrace);
        }
        finally
        {
            GetTree().Quit(exit);
        }
    }

    private async Task CheckColdContinueAsync()
    {
        // A preceding process creates checkpoint inside the same userdata guard.
        // Never create or repair a save here: Continue must discover it normally.
        for (var frame = 0; frame < 900 && _demo.MainMenu?.ContinueButton?.Visible != true; frame++)
            await Frames(1);
        var button = _demo.MainMenu?.ContinueButton;
        Require(_demo.MainMenuVisible && button is { Visible: true, Disabled: false },
            "the fresh process offers Continue");
        button!.EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 900 && _demo.MainMenuVisible; frame++) await Frames(1);
        Require(!_demo.MainMenuVisible && !_demo.IntroVisible, "Continue dismisses menu without replaying intro");
        _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
        _player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
        _quest = TamaraFenceQuest.Current(GetTree())!;
        await Frames(20);
        Require(_bridge.TamaraFenceSnapshotNow() is
            { Crashed: true, CutsceneWatched: true, Repaired: false, Carried: 0, Delivered: 0 },
            $"checkpoint restores watched crash without inventing boards: {_bridge.TamaraFenceSnapshotNow()}");
        Require(_quest.FenceBreakApplied && _quest.Panels.Any(panel => panel.Broken),
            "the broken fence projects into the fresh world");
        Require(_quest.Panels.Where(panel => panel.Broken).All(panel => panel.Body.CollisionLayer == 0),
            "broken panels do not restore an invisible blocker");
        Require(_quest.TamaraActor.IsVisibleInTree() && _quest.GuyActor.IsVisibleInTree(),
            "both quest actors remain visible");
        Require(_quest.ActiveCutscene is null && !_player.ModalOpen,
            "the watched scene does not restart or lock controls");
        var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
        _niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");
        Require(_niva.Driver == _player && _player.VehicleControlled
            && GetViewport().GetCamera3D() == _niva.VehicleCamera,
            "checkpoint restores the Niva driver and vehicle camera");
        Require(_bridge.ActiveObjectives().Any(objective => objective.QuestId == TamaraFenceQuest.QuestId),
            "the board objective survives restart");
        Input.ActionPress("interact");
        try
        {
            for (var frame = 0; frame < 2; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally { Input.ActionRelease("interact"); }
        await Frames(4);
        Require(_niva.Driver is null && !_player.VehicleControlled
            && GetViewport().GetCamera3D() == _player.GetNode<Camera3D>("Head/Camera3D"),
            "ordinary exit restores the on-foot camera and controls");
        var position = _player.GlobalPosition;
        Input.ActionPress("move_backward");
        try { await Frames(20); }
        finally { Input.ActionRelease("move_backward"); }
        Require(_player.GlobalPosition.DistanceTo(position) > .05f, "ordinary movement works after Continue");
    }

    private void Require(bool condition, string what)
    {
        _checks++;
        if (condition) return;
        throw new InvalidOperationException($"tamara-fence-smoke check failed: {what}");
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
