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
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
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

            // --- weak hit does nothing --------------------------------------
            var panel = _quest.Panels[2];
            _quest.DebugHandleHardStop(_niva, 1.2f, panel.Body);
            await Frames(3);
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: false }, "a light touch is not a crash");
            Require(!_quest.FenceBreakApplied, "a light touch leaves the fence standing");

            // --- strong hit: crash, cutscene, skip ---------------------------
            _quest.AccelerateCutsceneForTest(30);
            _quest.DebugHandleHardStop(_niva, 4.6f, panel.Body);
            for (var frames = 0; frames < 600 && _quest.ActiveCutscene is null && !_quest.FenceBreakApplied; frames++)
            {
                await Frames(1);
            }
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: true }, "the crash commits immediately");
            Require(_quest.FenceBreakApplied, "the impacted panels break");
            if (_quest.ActiveCutscene is { } scene)
            {
                scene.RequestSkip();
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
            var dialogue = _bridge.RequireDialogue("urman.chapter1:dialogue/tamara_fence_hand_in");
            Require(_bridge.ResolveDialogueStartNodeId(dialogue) == "hand-2",
                "the hand-over dialogue reads the new count");

            // --- loading mid-quest is safe -----------------------------------
            Require(await _bridge.LoadSlotAsync("tamara-mid"), "mid-quest slot loads");
            await Frames(20);
            Require(!_player.ModalOpen, "a load never leaves the camera locked");
            Require(_bridge.TamaraFenceSnapshotNow() is { Crashed: true, Carried: 2, Delivered: 0 },
                "the loaded slot restores its own progress");
            Require(_quest.FenceBreakApplied, "the loaded world shows the broken fence");
            Require(_quest.BoardTargets[0].IsAvailable() == false, "the loaded spot stays empty");

            // --- finish the quest --------------------------------------------
            // The loaded slot already carries boards 1 and 2; only the rest exist.
            for (var board = 3; board <= 6; board++)
            {
                Require(await _bridge.DispatchInteractionAsync(
                    $"urman.chapter1:interaction/tamara-fence-take-board-{board}"), $"board {board} pickup");
            }
            Require(_bridge.TamaraFenceSnapshotNow() is { Carried: 6 },
                "six boards carried after the load restored the first two");
            Require(await _bridge.TamaraFenceDeliverAsync(), "final hand-over commits");
            Require(_bridge.TamaraFenceSnapshotNow() is { Delivered: 6, Repaired: true },
                "six delivered boards complete and repair the fence");
            Require(!_bridge.ActiveObjectives().Any(objective => objective.QuestId == TamaraFenceQuest.QuestId),
                "the quest leaves the objective list");
            Require(_bridge.JournalEntries().Any(entry =>
                entry.EntryId == "urman.chapter1:knowledge/tamara_fence_settled"),
                "the notebook keeps a record of the settled fence");
            Require(!_quest.BoardTargets[0].IsAvailable(), "no pickups after the repair");
            Require(!await _bridge.TamaraFenceDeliverAsync(), "no hand-over after the repair");

            // --- the quiet callback: the Niva rolls past, the phone rises -----
            _quest.DebugExposeGuyForTest(out var guy, out _, out _, out var phone);
            guy.Visible = true;
            _niva.GlobalPosition = new(1.4f, 0f, -43.9f);
            _niva.RotationDegrees = new(0, 78f, 0);
            _niva.SetPlacementAvailability(true);
            _player.SetVehicleControl(true);
            _niva.Enter(_player, restore: true);
            _player.GlobalPosition = _niva.ToGlobal(_niva.Definition.Seat);
            for (var frames = 0; frames < 120 && !_quest.QuietCallbackDone; frames++) await Frames(1);
            Require(_quest.QuietCallbackDone, "the passing Niva triggers the silent phone callback");
            Require(phone is not null && phone.Visible, "the phone rises during the callback");
            _player.SetVehicleControl(false);

            // --- the mended fence --------------------------------------------
            _quest.MarkRepairDeferredByLoadForTest();
            _player.GlobalPosition = new(-2f, .1f, -8f);
            for (var frames = 0; frames < 30 && !_quest.FenceRepairApplied; frames++) await Frames(1);
            Require(_quest.FenceRepairApplied, "the fence repairs once the yard is unattended");
            Require(_quest.Panels.All(candidate => candidate.Body.CollisionLayer == 1u),
                "the repaired fence is solid again");

            Require(await _bridge.SaveSlotAsync("tamara-done"), "finished quest saves");
            Require(await _bridge.LoadSlotAsync("tamara-done"), "finished quest loads");
            await Frames(20);
            Require(_bridge.TamaraFenceSnapshotNow() is { Delivered: 6, Repaired: true },
                "the finished state is stable across a load");
            Require(_quest.Panels.All(candidate => candidate.Body.CollisionLayer == 1u),
                "the repaired fence survives the load");

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
