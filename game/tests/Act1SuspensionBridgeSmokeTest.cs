using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Relayout v3: the gorge and its suspension bridge. With ordinary controller input the
/// player crosses to the forest side and back on day one (the bridge holds); after the
/// night of the blizzard a crossing to the forest and back brings the bridge down behind
/// him. The broken state survives save/load and the gorge cannot then be crossed.
/// </summary>
public partial class Act1SuspensionBridgeSmokeTest : Node
{
    private FirstPersonController _player = null!;
    private int _checks;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(demo), "ordinary New Game");
            demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
            for (var frame = 0; frame < 600 && demo.PrologueActive; frame++) await Frames(1);
            await Frames(10);
            var world = demo.DemoMain.ConnectedWorld!;
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _player = demo.DemoMain.GetNode<FirstPersonController>("Player");
            var x = Act1ConnectedWorld.SuspensionBridgeX;
            var near = new Vector3(x, 0, Act1ConnectedWorld.GorgeNearRim(x) + 3.5f);
            var far = new Vector3(x, 0, Act1ConnectedWorld.GorgeFarRim(x) - 3.5f);
            Require(world.FindChild("SuspensionBridge", true, false) is Node3D, "the suspension bridge stands over the gorge");
            Require(AgentBAct1HeightField.Ground(x, Act1ConnectedWorld.GorgeCentreZ(x)) < AgentBAct1HeightField.Ground(x, near.Z) - 7,
                "the gorge under the bridge is deep");

            Spawn(near);
            await Frames(10);
            await Walk(far, "day one: cross the bridge to the forest side");
            await Walk(near, "day one: cross back");
            Require(!world.SuspensionBridgeBroken, "before the night the bridge holds");

            Require(await bridge.SetFirstNightPassedAsync(true), "the night of the blizzard has passed");
            await Frames(10);
            await Walk(far, "after the night: cross to the forest side");
            await Walk(near, "after the night: cross back");
            for (var frame = 0; frame < 300 && !world.SuspensionBridgeBroken; frame++) await Frames(1);
            Require(world.SuspensionBridgeBroken, "the bridge gives way behind him on the way back");
            var deck = (StaticBody3D)world.FindChild("SuspensionDeckBody", true, false)!;
            Require(deck.CollisionLayer == 0, "the deck no longer carries anyone");

            Require(await bridge.SaveSlotAsync("suspension-bridge-proof") && await bridge.LoadSlotAsync("suspension-bridge-proof"),
                "save and load");
            await Frames(20);
            Require(world.SuspensionBridgeBroken && deck.CollisionLayer == 0, "the broken bridge survives save/load");
            Spawn(near);
            await Frames(10);
            var before = _player.GlobalPosition;
            await Walk(far, "after the collapse the gorge stops him", expectReach: false);
            Require(_player.GlobalPosition.Z > Act1ConnectedWorld.GorgeNearRim(x) - 1f, "he stays on the village rim");
            GD.Print($"act1-suspension-bridge: PASS {_checks} checks; from {before}");
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PrintErr("act1-suspension-bridge: FAIL " + error.Message);
        }
        finally
        {
            Input.ActionRelease("move_forward");
            GetTree().Quit(exit);
        }
    }

    private void Spawn(Vector3 at)
    {
        at.Y = (float)AgentBAct1HeightField.CollisionGround(at.X, at.Z) + .05f;
        _player.ApplyZoneSpawn(at, 0);
    }

    private async Task Walk(Vector3 goal, string label, bool expectReach = true)
    {
        var stalled = 0;
        var last = _player.GlobalPosition;
        for (var frame = 0; frame < 900; frame++)
        {
            var delta = goal - _player.GlobalPosition; delta.Y = 0;
            if (delta.Length() < .3f) break;
            _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
            Input.ActionPress("move_forward");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            stalled = _player.GlobalPosition.DistanceTo(last) < .002f ? stalled + 1 : 0;
            last = _player.GlobalPosition;
            if (stalled > 90) break;
        }
        Input.ActionRelease("move_forward");
        await Frames(5);
        var remaining = new Vector2(goal.X - _player.GlobalPosition.X, goal.Z - _player.GlobalPosition.Z).Length();
        GD.Print($"act1-suspension-bridge: {label}: at {_player.GlobalPosition}, {remaining:0.00} m left");
        if (expectReach) Require(remaining < .5f && _player.IsOnFloor(), label);
    }

    private void Require(bool condition, string label)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(label);
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
