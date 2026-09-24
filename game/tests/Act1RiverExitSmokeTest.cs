using Godot;
using Urman.Experiments.AgentBAct1;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// A player who slid onto the river ice walks out by the plank steps on the
/// village bank from every exit, and the forest bank still cannot be climbed:
/// the ravine is a boundary, not a pit.
/// </summary>
public partial class Act1RiverExitSmokeTest : Node
{
    public override async void _Ready()
    {
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(8);
            Require(await this.StartThroughMainMenuAsync(demo), "the demo starts through the main menu");
            var player = demo.DemoMain.GetNode<FirstPersonController>("Player");
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(3);
            var river = demo.DemoMain.ConnectedWorld!.FindChild("VillageForestRiver", true, false) as Node3D
                ?? throw new InvalidOperationException("the river is not built");
            Require(river.GetMeta("riverBankStepCount", 0).AsInt32() == Act1ConnectedWorld.RiverStepXs.Length,
                "every exit has its steps");

            foreach (var x in Act1ConnectedWorld.RiverStepXs)
            {
                var centre = (float)AgentBAct1HeightField.RiverMeander(x + 3f);
                var bed = (float)AgentBAct1HeightField.Ground(x + 3f, centre) + .9f;
                player.ApplyPortableTransform(new PlayerTransform(new(x + 3f, bed, centre), new(0, 0, 0)));
                await PhysicsFrames(20);

                // The forest bank stays a wall of snow.
                await Walk(player, new Vector3(x + 3f, 0f, centre - 12f), 150);
                // The bank lip is 4.6 m out; the drift on it starts at 5.2 m.
                Require(player.GlobalPosition.Z > centre - 5.2f
                        && player.GlobalPosition.Y < AgentBAct1HeightField.CollisionGround(x + 3f, centre - 6.5f) + .3f,
                    $"the forest bank at x={x} cannot be climbed: {player.GlobalPosition}");

                var (bottom, top) = Act1ConnectedWorld.RiverStepLine(x);
                Require(await Walk(player, new Vector3(x, 0f, bottom.Z - .4f), 400), $"reach the steps at x={x}: {player.GlobalPosition}");
                Require(await Walk(player, top + new Vector3(0f, 0f, 1.4f), 400), $"climb the steps at x={x}: {player.GlobalPosition}");
                var ground = AgentBAct1HeightField.CollisionGround(player.GlobalPosition.X, player.GlobalPosition.Z);
                Require(player.GlobalPosition.Y > ground - .2f && player.GlobalPosition.Y < ground + 1.4f,
                    $"standing on the village bank at x={x}: {player.GlobalPosition} ground={ground:F2}");
                Require(player.GlobalPosition.Z > top.Z + .9f,
                    $"out of the ravine at x={x}");
            }

            GD.Print($"act1-river-exit: PASS exits={Act1ConnectedWorld.RiverStepXs.Length} forest bank holds, village steps climb out");
            await GodotSmokeCleanup.ReleaseAsync(demo);
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("act1-river-exit: " + exception.Message);
            GetTree().Quit(1);
        }
    }

    private async Task<bool> Walk(FirstPersonController player, Vector3 target, int frames)
    {
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var offset = target - player.GlobalPosition;
                offset.Y = 0f;
                if (offset.Length() < .35f) return true;
                player.RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(-offset.X, -offset.Z)), 0f);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            return false;
        }
        finally
        {
            Input.ActionRelease("move_forward");
            await PhysicsFrames(2);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}
