using System;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>Native physics traversal of the rebuilt minaret. No artistic or cultural acceptance.</summary>
public partial class Act1MosqueLayoutSmokeTest : Node
{
    private FirstPersonController _player = null!;
    private int _checks;

    // Native macOS focus can arrive after startup. This affects only this diagnostic scene.
    public override void _Process(double delta)
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true } pause)
        { DisplayServer.WindowMoveToForeground(); pause.Resume(); }
    }

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var exit = 1;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(demo), "ordinary new game starts");
            demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
            for (var frame = 0; frame < 600 && demo.PrologueActive; frame++) await Frames(1);
            await Frames(10);
            var world = demo.DemoMain.ConnectedWorld!;
            _player = demo.DemoMain.GetNode<FirstPersonController>("Player");
            var room = world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior");
            var qibla = room.GetMeta("qiblaWorldDirection").AsVector3();
            var bearing = (Mathf.RadToDeg(Mathf.Atan2(qibla.X, qibla.Z)) + 360) % 360;
            Require(Math.Abs(bearing - 195.1725f) < .01f, "qibla bearing is Kazan-to-Kaaba relative to true north +Z");
            var prayerWall = room.GetNode<Node3D>("MosqueQiblaWall");
            Require((-prayerWall.GlobalBasis.Z).Normalized().Dot(qibla) > .99999f, "mihrab and minbar share the documented qibla");
            var carpet = room.GetNode<MeshInstance3D>("MosquePrayerCarpet");
            Require((room.GlobalBasis * carpet.GetMeta("textureUpDirection").AsVector3()).Normalized().Dot(qibla) > .99999f,
                "woven prayer-cell heads point toward qibla");
            var pattern = carpet.GetNode<MeshInstance3D>("MosquePatternedCarpetSurface");
            Require(pattern.MaterialOverride is StandardMaterial3D { AlbedoTexture: not null }, "prayer carpet uses the imported patterned textile");
            var stair = room.GetNode<Node3D>("MosqueMinaretAccess");
            var anchors = stair.GetMeta("walkAnchors").AsGodotArray<Vector3>();
            Require(anchors.Count == 14, "route includes every stair landing and each turn");
            _player.ApplyZoneSpawn(anchors[0], 0);
            await Frames(12);
            for (var i = 1; i < anchors.Count; i++) await WalkTo(anchors[i]);
            Require(world.TryGetMosqueAdhanPoint(out var adhan), "future prayer-time owner can resolve the real gallery anchor");
            await WalkTo(adhan.Origin + Vector3.Up * .035f);
            Require(Math.Abs(room.ToLocal(_player.GlobalPosition).Y - 10.08f) < .08f && _player.IsOnFloor(), "player stands on the actual gallery floor");
            for (var i = anchors.Count - 1; i >= 0; i--) await WalkTo(anchors[i]);
            Require(Math.Abs(room.ToLocal(_player.GlobalPosition).Y) < .08f && _player.IsOnFloor(), "stairs are traversable back down into the vestibule");
            GD.Print($"act1-mosque-layout: PASS {_checks} physics/qibla/asset checks; art, local cultural review and azan audio remain external");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-mosque-layout: FAIL " + error); }
        finally { Input.ActionRelease("move_forward"); GetTree().Quit(exit); }
    }

    private async System.Threading.Tasks.Task WalkTo(Vector3 goal)
    {
        var stalled = 0;
        var last = _player.GlobalPosition;
        for (var frame = 0; frame < 600; frame++)
        {
            var direction = goal - _player.GlobalPosition; direction.Y = 0;
            if (direction.Length() < .12f) break;
            _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
            Input.ActionPress("move_forward");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            stalled = _player.GlobalPosition.DistanceTo(last) < .002f ? stalled + 1 : 0;
            last = _player.GlobalPosition;
            if (stalled > 90)
            {
                using var collision = new KinematicCollision3D();
                if (_player.TestMove(_player.GlobalTransform, direction.Normalized() * .20f, collision, .001f, false, 8))
                    for (var contact = 0; contact < collision.GetCollisionCount(); contact++)
                        GD.Print($"act1-mosque-blocker: collider={(collision.GetCollider(contact) as Node)?.GetPath()} point={collision.GetPosition(contact)} normal={collision.GetNormal(contact)} shape={collision.GetColliderShape(contact)} travel={collision.GetTravel()}");
                GD.Print($"act1-mosque-controller: height={_player.BodyHeight} radius={_player.BodyRadius} modal={_player.ModalOpen} velocity={_player.Velocity} step={_player.LastStepRejection}; carry={(GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator)?.DescribeAim()}");
                break;
            }
        }
        Input.ActionRelease("move_forward");
        await Frames(8);
        Require(new Vector2(goal.X - _player.GlobalPosition.X, goal.Z - _player.GlobalPosition.Z).Length() < .24f
            && Math.Abs(goal.Y - _player.GlobalPosition.Y) < .12f,
            $"ordinary controller reached {goal}; actual {_player.GlobalPosition}");
    }

    private async System.Threading.Tasks.Task Frames(int count)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private void Require(bool condition, string label)
    { _checks++; if (!condition) throw new InvalidOperationException(label); }
}
