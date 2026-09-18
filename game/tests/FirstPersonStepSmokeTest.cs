using Godot;

namespace Urman.Godot.Tests;

/// <summary>Isolated collision fixtures for the production walking controller.
/// The complete FAP stairs remain covered by the separate architecture scene.</summary>
public partial class FirstPersonStepSmokeTest : Node
{
    private const float FloorY = 5f;
    private Node3D _fixture = null!;
    private FirstPersonController _player = null!;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _fixture = new Node3D { Name = "ExplicitCollisionFixtures" };
            AddChild(_fixture);
            Box("Floor", new(20, .30f, 10), new(6, FloorY - .15f, -1));
            _player = ResourceLoader.Load<PackedScene>("res://scenes/player/first_person_player.tscn")
                .Instantiate<FirstPersonController>();
            _player.Position = new(0, FloorY + .02f, 1.1f);
            _fixture.AddChild(_player);
            foreach (var (z, height) in new[] { (-.20f, .12f), (-.55f, .25f), (-.90f, .38f) })
                Box("Riser" + height, new(2.2f, height, .40f), new(0, FloorY + height * .5f, z));
            Box("UpperDeck", new(2.2f, .50f, 1.20f), new(0, FloorY + .25f, -1.65f));
            Box("TooHigh", new(2.2f, .60f, .70f), new(4, FloorY + .30f, -.35f));
            Box("ThinRung", new(1.3f, .16f, .05f), new(-2.6f, FloorY + .08f, -.025f));
            Box("CeilingStep", new(2.2f, .16f, 1.4f), new(8, FloorY + .08f, -.70f));
            Box("LowCeiling", new(2.8f, .12f, 2.10f), new(8, FloorY + 1.96f, -.05f));
            SteepSlope();

            await StartAt(0);
            var steps = _player.StepsClimbed;
            await WalkTo(-1.7f, forward: true);
            Require(_player.IsOnFloor() && Math.Abs(_player.GlobalPosition.Y - FloorY - .50f) < .025f
                && _player.StepsClimbed > steps,
                "ordinary input ascends .12/.13/.13/.12m risers and stands on the real deck");
            await WalkTo(1.1f, forward: false);
            Require(_player.IsOnFloor() && Math.Abs(_player.GlobalPosition.Y - FloorY) < .025f,
                "ordinary input descends the same steps to the real floor");

            await StartAt(4);
            steps = _player.StepsClimbed;
            await PushAgainst();
            Require(_player.GlobalPosition.Z > .25f && Math.Abs(_player.GlobalPosition.Y - FloorY) < .025f
                && _player.StepsClimbed == steps,
                "a wall higher than the human step limit remains blocking");

            await StartAt(-2.6f);
            steps = _player.StepsClimbed;
            await PushAgainst();
            Require(_player.GlobalPosition.Z > .20f && Math.Abs(_player.GlobalPosition.Y - FloorY) < .025f
                && _player.StepsClimbed == steps,
                "a 5cm-deep rung cannot replace a tread or trigger automatic ladder climbing");

            await StartAt(8);
            steps = _player.StepsClimbed;
            await PushAgainst();
            Require(_player.GlobalPosition.Z > -.10f && _player.GlobalPosition.Y < FloorY + .11f
                && _player.StepsClimbed == steps,
                "a low ceiling refuses the standing step without clipping or forced crouch");
            Input.ActionPress("crouch"); await Frames(2); Input.ActionRelease("crouch"); await Frames(2);
            await WalkTo(-.85f, forward: true);
            Require(_player.IsCrouching && _player.IsOnFloor()
                && Math.Abs(_player.GlobalPosition.Y - FloorY - .16f) < .025f,
                "the player's explicit crouch uses the smaller real capsule on the same step");
            Input.ActionPress("crouch"); await Frames(2); Input.ActionRelease("crouch"); await Frames(2);
            Require(_player.IsCrouching, "the same ceiling prevents standing inside it");

            await StartAt(12);
            steps = _player.StepsClimbed;
            await PushAgainst();
            Require(_player.GlobalPosition.Z > -.15f && _player.GlobalPosition.Y < FloorY + .20f
                && _player.StepsClimbed == steps,
                "a 55-degree surface cannot be climbed by repeatedly treating it as a stair");
            GD.Print("first-person-step: PASS physical ascent/descent, high-wall refusal, ceiling/stance clearance and steep-slope refusal; FAP/art/playtest require separate evidence");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("first-person-step: FAIL " + error); }
        finally
        {
            Input.ActionRelease("move_forward"); Input.ActionRelease("move_backward"); Input.ActionRelease("crouch");
            try { if (_fixture is not null) await GodotSmokeCleanup.ReleaseAsync(_fixture); }
            catch (Exception error) { exit = 1; GD.PrintErr("first-person-step cleanup: " + error); }
            GetTree().Quit(exit);
        }
    }

    private async Task StartAt(float x)
    {
        _player.ApplyZoneSpawn(new(x, FloorY + .02f, 1.1f), 0);
        await Frames(16);
        Require(_player.IsOnFloor() && !_player.IsCrouching && !_player.ModalOpen,
            "explicit isolated fixture starts standing on its collision floor");
    }

    private async Task WalkTo(float z, bool forward)
    {
        var revision = _player.PresentationTransformRevision;
        var recovery = _player.FallRecoveries;
        var clamps = _player.EdgeClamps;
        var action = forward ? "move_forward" : "move_backward";
        bool Reached() => forward ? _player.GlobalPosition.Z <= z : _player.GlobalPosition.Z >= z;
        Input.ActionPress(action);
        try { for (var frame = 0; frame < 360 && !Reached(); frame++) await Frames(1); }
        finally { Input.ActionRelease(action); }
        await Frames(8);
        Require(Reached() && revision == _player.PresentationTransformRevision
            && recovery == _player.FallRecoveries && clamps == _player.EdgeClamps,
            $"actual {action} reaches Z={z}; at={_player.GlobalPosition}, step={_player.LastStepRejection}, no placement/recovery");
    }

    private async Task PushAgainst()
    {
        var revision = _player.PresentationTransformRevision;
        Input.ActionPress("move_forward");
        try { await Frames(90); }
        finally { Input.ActionRelease("move_forward"); }
        await Frames(3);
        Require(revision == _player.PresentationTransformRevision && _player.FallRecoveries == 0 && _player.EdgeClamps == 0,
            "a refused ascent does not use placement or world recovery");
    }

    private void Box(string name, Vector3 size, Vector3 at)
    {
        var body = new StaticBody3D { Name = name, Position = at, CollisionLayer = 1u };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size } });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        _fixture.AddChild(body);
    }

    private void SteepSlope()
    {
        var rise = Mathf.Tan(Mathf.DegToRad(55));
        var vertices = new[] { new Vector3(-1.1f, 0, 0), new Vector3(1.1f, 0, 0),
            new Vector3(-1.1f, rise, -1), new Vector3(1.1f, rise, -1),
            new Vector3(-1.1f, -.1f, 0), new Vector3(1.1f, -.1f, 0),
            new Vector3(-1.1f, -.1f, -1), new Vector3(1.1f, -.1f, -1) };
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        // Unlike the banks, this prism's vertex rows run toward negative Z.
        foreach (var index in new[] { 0,2,3,0,3,1, 4,5,7,4,7,6, 0,1,5,0,5,4,
                     2,6,7,2,7,3, 0,4,6,0,6,2, 1,3,7,1,7,5 })
            surface.AddVertex(vertices[index]);
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var body = new StaticBody3D { Name = "ActualSteepSurface", Position = new(12, FloorY, 0), CollisionLayer = 1u };
        body.AddChild(new MeshInstance3D { Mesh = mesh });
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
        _fixture.AddChild(body);
    }

    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        GD.Print("first-person-step: " + label);
    }
}
