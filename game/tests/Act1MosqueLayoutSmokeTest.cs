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
            if (OS.GetEnvironment("URMAN_MINARET_FIXTURE") == "1")
            {
                // Focused fixture uses the exact production builder and controller, before
                // repeating the route in the complete connected world.
                var fixture = new Node3D { Name = "MinaretPhysicsFixture" }; AddChild(fixture);
                var owner = new Act1ConnectedWorld();
                typeof(Act1ConnectedWorld).GetField("_mosqueRoom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(owner, fixture);
                typeof(Act1ConnectedWorld).GetMethod("BuildMosqueAccessibleMinaret", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(owner, null);
                owner.Free();
                var floor = new StaticBody3D { CollisionLayer = 2, Position = new(0,-.05f,0) };
                floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(20,.10f,20) } }); fixture.AddChild(floor);
                _player = ResourceLoader.Load<PackedScene>("res://scenes/player/first_person_player.tscn").Instantiate<FirstPersonController>(); fixture.AddChild(_player);
                var points = fixture.GetNode<Node3D>("MosqueMinaretAccess").GetMeta("walkAnchors").AsGodotArray<Vector3>();
                _player.ApplyZoneSpawn(points[0],0); await Frames(12);
                for (var i=1;i<points.Count;i++) { GD.Print($"fixture-tread: {i}"); await WalkTo(points[i]); }
                await WalkTo(fixture.GetNode<Node3D>("MosqueMinaretGallery/MosqueAdhanAnchor").GlobalPosition+Vector3.Up*.035f);
                Require(Math.Abs(_player.GlobalPosition.Y-Act1ConnectedWorld.MinaretGalleryHeight)<.08f && _player.IsOnFloor(),"actual fixture gallery");
                for(var i=points.Count-1;i>=0;i--) await WalkTo(points[i]);
                Require(Math.Abs(_player.GlobalPosition.Y)<.08f && _player.IsOnFloor(),"actual fixture descent");
                GD.Print($"act1-mosque-layout: FIXTURE PASS {_checks}"); exit=0; return;
            }
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
            Require(anchors.Count == 62, "route includes the entrance and all 60 spiral treads");
            _player.ApplyZoneSpawn(anchors[0], 0);
            await Frames(12);
            for (var i = 1; i < anchors.Count; i++) await WalkTo(anchors[i]);
            Require(world.TryGetMosqueAdhanPoint(out var adhan), "future prayer-time owner can resolve the real gallery anchor");
            await WalkTo(adhan.Origin + Vector3.Up * .035f);
            Require(Math.Abs(room.ToLocal(_player.GlobalPosition).Y - Act1ConnectedWorld.MinaretGalleryHeight) < .08f && _player.IsOnFloor(), "player stands on the actual gallery floor");
            for (var i = anchors.Count - 1; i >= 0; i--) await WalkTo(anchors[i]);
            Require(Math.Abs(room.ToLocal(_player.GlobalPosition).Y) < .08f && _player.IsOnFloor(), "stairs are traversable back down into the vestibule");
            GD.Print($"act1-mosque-layout: PASS {_checks} physics/qibla/asset checks; art, local cultural review and azan audio remain external");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-mosque-layout: FAIL " + error); }
        finally { Input.ActionRelease("move_forward"); if (GetChildCount() > 0) await GodotSmokeCleanup.ReleaseAsync(GetChild(0)); GetTree().Quit(exit); }
    }

    private async System.Threading.Tasks.Task WalkTo(Vector3 goal)
    {
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var stalled = 0;
        var last = _player.GlobalPosition;
        for (var frame = 0; frame < 600; frame++)
        {
            var direction = goal - _player.GlobalPosition; direction.Y = 0;
            if (direction.Length() < .055f) break;
            _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Z)));
            Input.ActionPress("move_forward");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            stalled = _player.GlobalPosition.DistanceTo(last) < .002f ? stalled + 1 : 0;
            last = _player.GlobalPosition;
            if (stalled > 90)
            {
                for (var h = .08f; h < 2f; h += .25f)
                {
                    using var ray = PhysicsRayQueryParameters3D.Create(_player.GlobalPosition + Vector3.Up * h,
                        _player.GlobalPosition + Vector3.Up * h + direction.Normalized() * 1.5f, 3);
                    using var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                    if (hit.Count > 0) GD.Print($"spiral-probe: height={h} collider={(hit["collider"].AsGodotObject() as Node)?.GetPath()} at={hit["position"]} normal={hit["normal"]}");
                }
                GD.Print($"spiral-floor: on={_player.IsOnFloor()} floorNormal={_player.GetFloorNormal()} slides={_player.GetSlideCollisionCount()}");
                for(var k=0;k<_player.GetSlideCollisionCount();k++) { using var slide=_player.GetSlideCollision(k); GD.Print($"spiral-slide: collider={(slide.GetCollider() as Node)?.GetPath()} normal={slide.GetNormal()} point={slide.GetPosition()}"); }
                var lifted=_player.GlobalTransform; lifted.Origin+=Vector3.Up*.005f;
                using var collision = new KinematicCollision3D();
                if (_player.TestMove(lifted, direction.Normalized() * .20f, collision, .001f, false, 8))
                    for (var contact = 0; contact < collision.GetCollisionCount(); contact++)
                        GD.Print($"act1-mosque-blocker: collider={(collision.GetCollider(contact) as Node)?.GetPath()} point={collision.GetPosition(contact)} normal={collision.GetNormal(contact)} shape={collision.GetColliderShape(contact)} travel={collision.GetTravel()}");
                GD.Print($"act1-mosque-controller: height={_player.BodyHeight} radius={_player.BodyRadius} modal={_player.ModalOpen} velocity={_player.Velocity} step={_player.LastStepRejection}; carry={(GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator)?.DescribeAim()}");
                break;
            }
        }
        Input.ActionRelease("move_forward");
        await Frames(8);
        Require(revision == _player.PresentationTransformRevision && recoveries == _player.FallRecoveries && clamps == _player.EdgeClamps,
            "route uses only walking input, without placement or world recovery");
        Require(new Vector2(goal.X - _player.GlobalPosition.X, goal.Z - _player.GlobalPosition.Z).Length() < .12f
            && Math.Abs(goal.Y - _player.GlobalPosition.Y) < .36f,
            $"ordinary controller reached {goal}; actual {_player.GlobalPosition}");
    }

    private async System.Threading.Tasks.Task Frames(int count)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private void Require(bool condition, string label)
    { _checks++; if (!condition) throw new InvalidOperationException(label); }
}
