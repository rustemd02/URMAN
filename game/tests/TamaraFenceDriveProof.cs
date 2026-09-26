using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// One real drive: ordinary input, the Niva leaves its parking, runs down the
/// return street and crashes into Tamara Gennadievna's fence through the actual
/// physics path (HardStop), not a debug hook. Verifies the reach contract the
/// debug-hook smoke cannot prove on its own.
/// </summary>
public partial class TamaraFenceDriveProof : Node
{
    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("no start");
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
            // Dismiss the authored intro overlay the way a player does.
            for (var i = 0; i < 600 && player.ModalOpen; i++)
            {
                demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            if (player.ModalOpen) throw new InvalidOperationException("intro overlay did not close");
            var quest = TamaraFenceQuest.Current(GetTree())!;
            var fleet = (VehicleFleet)GetTree().GetFirstNodeInGroup("vehicle_fleet");
            var niva = fleet.Vehicles.Single(vehicle => vehicle.Definition.Id == "babay-niva");
            quest.AccelerateCutsceneForTest(30);

            // Park on the return street aimed at her fence, then drive normally.
            // The player's own approach: down the street, then the wheel turns
            // toward the breach. Before the driveway existed the road graph
            // refused this angle ("no road here") long before any contact.
            niva.GlobalPosition = new(0.2f, 0f, -39.6f);
            niva.RotationDegrees = new(0, -30f, 0);
            niva.SetPlacementAvailability(true);
            player.SetVehicleControl(true);
            niva.Enter(player, restore: true);
            player.GlobalPosition = niva.ToGlobal(niva.Definition.Seat);
            await Frames(6);
            Input.ActionPress("carry_use");
            await Frames(2);
            Input.ActionRelease("carry_use");
            await Frames(6);
            if (!niva.EngineRunning) throw new InvalidOperationException("the Niva's ignition did not start");
            Input.ActionPress("move_forward");
            Input.ActionPress("move_right");
            var crashed = false;
            for (var frames = 0; frames < 900 && !crashed; frames++)
            {
                await Frames(1);
                crashed = bridge.TamaraFenceSnapshotNow()?.Crashed == true;
            }

            Input.ActionRelease("move_forward");
            Input.ActionRelease("move_right");
            Input.ActionRelease("carry_use");
            if (!crashed) throw new InvalidOperationException(
                $"the Niva never reached the fence: pos={niva.GlobalPosition} speed={niva.Speed} stops={niva.CollisionStops}");
            for (var frames = 0; frames < 900 && quest.ActiveCutscene is not null; frames++) await Frames(1);
            if (quest.ActiveCutscene is not null) throw new InvalidOperationException("cutscene did not end");
            if (player.ModalOpen) throw new InvalidOperationException("controls stayed locked");
            GD.Print($"tamara-fence-drive: PASS crash through ordinary input at {niva.GlobalPosition}");
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError("tamara-fence-drive failed: " + error);
        }
        finally
        {
            GetTree().Quit(exit);
        }
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
