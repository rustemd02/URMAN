using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Actual controller inputs in the live village; local fixtures are explicit and never save progression.</summary>
public partial class Act1PlayerMovementSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private FirstPersonController _player = null!;
    private readonly List<object> _events = new();
    private int _checks;
    private static readonly Vector2 ScreenMouseProbe = new(64, 32);
    private bool _observeMouseMotion;
    private (Vector2 Relative, Vector2 ScreenRelative)? _observedMouseMotion;
    private static readonly string[] Actions = { "move_forward", "move_right", "jump", "sprint", "crouch" };
    private string Output => System.Environment.GetEnvironmentVariable("URMAN_PLAYER_OUTPUT")
        ?? throw new InvalidOperationException("Set URMAN_PLAYER_OUTPUT to a new absolute evidence directory.");

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            if (!Path.IsPathFullyQualified(Output)) throw new InvalidOperationException("Evidence path must be absolute.");
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Check(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(8);
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            Check(!_player.ModalOpen && _player.VisibleBodyMeshCount == 6,
                "ordinary controller has the six source-derived lower-body meshes");
            foreach (var action in new[] { "jump", "sprint" })
                Check(InputBindingService.Capture().Any(binding => binding.Action == action)
                    && InputMap.ActionGetEvents(action).OfType<InputEventKey>().Any()
                    && InputMap.ActionGetEvents(action).OfType<InputEventJoypadButton>().Any(),
                    action + " has keyboard, gamepad and remapping");
            var beforeOldSettings = InputBindingService.Capture();
            InputBindingService.Apply(beforeOldSettings.Where(binding => binding.Action is not ("jump" or "sprint")));
            Check(InputBindingService.Capture().SequenceEqual(beforeOldSettings),
                "loading older settings retains the new project-default actions");

            var start = new Vector3(0, AgentBAct1HeightField.CollisionGround(0, 18) + .03f, 18);
            await Place(start);
            await CheckCapturedMouseResolution();
            var walking = await MeasureTravel(start, sprint: false);
            var running = await MeasureTravel(start, sprint: true);
            var analogue = await MeasureTravel(start, sprint: false, strength: .35f);
            var diagonal = await MeasureTravel(start, sprint: false, diagonal: true);
            Check(walking > 1.25f && walking < 2.0f && running > walking * 1.35f && running < walking * 1.65f,
                $"real run is faster than unchanged walking: walk={walking:F3}m run={running:F3}m per 30 physics ticks");
            Check(analogue > .05f && analogue < walking * .55f && diagonal <= walking * 1.06f,
                $"analogue approach stays gradual and diagonals stay bounded: analogue={analogue:F3}, diagonal={diagonal:F3}");

            await Place(start);
            _player.ApplySmokeLook(-78, 0);
            await Frames(3);
            await Capture("01_standing_look_down");
            CheckPresentedLowerBody("standing");
            CheckSoleSupport("standing");
            Input.ActionPress("move_forward");
            await Frames(12);
            await Capture("02_walking_lower_body");
            CheckPresentedLowerBody("walking");
            Input.ActionRelease("move_forward");
            await Frames(8);
            await Place(start);

            var jumps = _player.GroundedJumps;
            var baseY = _player.GlobalPosition.Y;
            Input.ActionPress("jump");
            await Frames(2);
            Input.ActionRelease("jump");
            Check(_player.GroundedJumps == jumps + 1 && !_player.IsOnFloor(), "a grounded mapped press starts one real jump");
            var peak = _player.GlobalPosition.Y;
            for (var frame = 0; frame < 75; frame++)
            {
                if (frame == 5) Input.ActionPress("jump");
                await Frames(1);
                peak = Math.Max(peak, _player.GlobalPosition.Y);
            }
            Check(_player.GroundedJumps == jumps + 1 && _player.IsOnFloor()
                && peak - baseY > .30f && peak - baseY < .58f,
                $"airborne repress and held landing cannot double-jump or repeat; peak={peak - baseY:F3}m");
            Input.ActionRelease("jump");
            await Frames(3);

            await Press("crouch");
            Check(_player.IsCrouching && _player.BodyHeight < 1.10f && _player.BodyHeight > 1.05f,
                "crouch changes the actual capsule to human low-passage height");
            _player.ApplySmokeLook(-78, 0);
            await Frames(10);
            await Capture("03_crouched_look_down");
            CheckPresentedLowerBody("crouched");
            CheckSoleSupport("crouched");
            var roof = Fixture("PlayerHeadroomFixture", new(1.6f, .12f, 1.6f),
                _player.GlobalPosition + Vector3.Up * 1.34f);
            try
            {
                await Frames(3);
                Check(!_player.CanStandAt(_player.GlobalPosition), "the real low roof rejects the standing capsule");
                await Press("crouch");
                await Press("jump");
                Check(_player.IsCrouching && _player.GroundedJumps == jumps + 1 && _player.IsOnFloor(),
                    "standing and jumping cannot force the player's body through a low roof");
            }
            finally { roof.QueueFree(); await Frames(3); }
            await Press("crouch");
            Check(!_player.IsCrouching && Math.Abs(_player.BodyHeight - _player.StandingBodyHeight) < .001f,
                "leaving the obstruction restores the original standing collider");

            await Place(start);
            var wall = Fixture("PlayerSprintWallFixture", new(2f, 2f, .12f), start + new Vector3(0, 1, -1.6f));
            try
            {
                await Frames(3);
                Input.ActionPress("move_forward"); Input.ActionPress("sprint");
                await Frames(45);
                Check(_player.GlobalPosition.Z > wall.GlobalPosition.Z + .30f && _player.IsOnWall(),
                    "sprinting stops the actual capsule at a physical wall");
            }
            finally { ReleaseInputs(); wall.QueueFree(); await Frames(3); }

            await Place(start);
            var beforeModal = _player.GlobalPosition;
            _player.SetModalOpen(true);
            Input.ActionPress("move_forward"); Input.ActionPress("sprint"); Input.ActionPress("jump");
            await Frames(5);
            Check(_player.GlobalPosition.DistanceTo(beforeModal) < .002f && _player.GroundedJumps == jumps + 1,
                "modal input cannot run or jump");
            _player.SetModalOpen(false);
            Input.ActionRelease("move_forward");
            await Frames(3);
            Check(_player.GroundedJumps == jumps + 1, "holding accept-era jump across close cannot start a world jump");
            ReleaseInputs(); await Frames(3);

            await CheckSessionTransition(start);

            var layer = _player.CollisionLayer;
            var mask = _player.CollisionMask;
            Check(_player.TryBeginVehicleControl(out var entryReason), "free grounded hands permit transport possession: " + entryReason);
            Check(_player.VehicleControlled && _player.CollisionLayer == 0 && _player.CollisionMask == 0
                && !_player.GetNode<Node3D>("AidarLowerBody").Visible,
                "vehicle handover disables pedestrian collision and lower-body presentation");
            var seat = _player.GlobalPosition + Vector3.Up * .50f;
            _player.GlobalPosition = seat;
            Input.ActionPress("move_forward"); Input.ActionPress("jump");
            await Frames(5);
            Check(_player.GlobalPosition.DistanceTo(seat) < .002f && _player.GroundedJumps == jumps + 1,
                "the seated pedestrian neither walks, jumps nor clamps the vehicle-owned position");
            ReleaseInputs();
            _player.ApplyZoneSpawn(start, 0);
            _player.SetVehicleControl(false);
            await Frames(6);
            Check(!_player.VehicleControlled && _player.CollisionLayer == layer && _player.CollisionMask == mask
                && _player.GetNode<Camera3D>("Head/Camera3D").Current && _player.IsOnFloor(),
                "safe exit restores the same capsule, camera and ordinary support");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-player-movement: " + error); _events.Add(new { kind = "failure", error = error.ToString() }); }
        finally
        {
            ReleaseInputs();
            try
            {
                Directory.CreateDirectory(Output);
                using var file = new FileStream(Path.Combine(Output, "player-movement-receipt.json"),
                    FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(file, new { passed = exit == 0, checks = _checks,
                    humanArtReview = "external/not-run", events = _events }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception error) { exit = 1; GD.PrintErr("Cannot preserve player movement evidence: " + error); }
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    public override void _Input(InputEvent inputEvent)
    {
        // Observe the event after the real viewport transform without consuming
        // it; the ordinary player's _UnhandledInput must still perform the turn.
        if (_observeMouseMotion && inputEvent is InputEventMouseMotion motion
            && motion.ScreenRelative.IsEqualApprox(ScreenMouseProbe))
            _observedMouseMotion = (motion.Relative, motion.ScreenRelative);
    }

    private async Task CheckCapturedMouseResolution()
    {
        var originalSize = DisplayServer.WindowGetSize();
        var originalMode = DisplayServer.WindowGetMode();
        var originalPosition = DisplayServer.WindowGetPosition();
        var originalMouseMode = Input.MouseMode;
        var originalLook = _player.CapturePortableTransform().RotationDegrees;
        var samples = new List<(Vector2 Turn, Vector2 Relative)>();
        try
        {
            Check(DisplayServer.GetName() != "headless" && !_player.ModalOpen
                && !_player.VehicleControlled && originalMouseMode == Input.MouseModeEnum.Captured,
                "mouse resolution proof uses ordinary captured gameplay in a native window");
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(size);
                await Frames(8);
                _player.ApplySmokeLook(0, 0);
                await Frames(3);
                Check(DisplayServer.WindowGetSize() == size && !_player.ModalOpen
                    && Input.MouseMode == Input.MouseModeEnum.Captured,
                    $"actual captured mouse window is {size}");
                var before = _player.CapturePortableTransform().RotationDegrees;
                _observedMouseMotion = null;
                _observeMouseMotion = true;
                using var motion = new InputEventMouseMotion
                {
                    Position = (Vector2)size * .5f,
                    GlobalPosition = (Vector2)size * .5f,
                    Relative = ScreenMouseProbe,
                    ScreenRelative = ScreenMouseProbe
                };
                Input.ParseInputEvent(motion);
                await Frames(3);
                _observeMouseMotion = false;
                var after = _player.CapturePortableTransform().RotationDegrees;
                var turn = new Vector2(Mathf.Wrap((float)(after.Y - before.Y), -180f, 180f),
                    (float)(after.X - before.X));
                var expected = -ScreenMouseProbe * _player.MouseSensitivity;
                var observed = _observedMouseMotion
                    ?? throw new InvalidOperationException("The parsed screen motion did not reach the real viewport input pipeline.");
                _events.Add(new { kind = "captured-mouse-resolution", window = size.ToString(),
                    logicalSize = GetTree().Root.ContentScaleSize.ToString(),
                    contentScaleFactor = GetTree().Root.ContentScaleFactor,
                    injectedScreenRelative = ScreenMouseProbe.ToString(),
                    deliveredRelative = observed.Relative.ToString(),
                    deliveredScreenRelative = observed.ScreenRelative.ToString(),
                    sensitivity = _player.MouseSensitivity, expectedYawPitch = expected.ToString(),
                    actualYawPitch = turn.ToString(), inputPath = "Input.ParseInputEvent -> viewport -> FirstPersonController._UnhandledInput" });
                Check(observed.ScreenRelative.IsEqualApprox(ScreenMouseProbe)
                    && turn.DistanceTo(expected) < .01f,
                    $"screen-relative motion gives the actual expected yaw and pitch at {size}: {turn}");
                samples.Add((turn, observed.Relative));
            }
            Check(samples[0].Relative.DistanceTo(samples[1].Relative) > .5f,
                "the two actual viewports transform Relative differently, exercising the original resolution regression");
            Check(samples[0].Turn.DistanceTo(samples[1].Turn) < .01f,
                "the same screen motion gives equal yaw and pitch at 720p and 1080p");
        }
        finally
        {
            _observeMouseMotion = false;
            DisplayServer.WindowSetSize(originalSize);
            DisplayServer.WindowSetMode(originalMode);
            DisplayServer.WindowSetPosition(originalPosition);
            Input.MouseMode = originalMouseMode;
            _player.ApplySmokeLook((float)originalLook.X, (float)originalLook.Y);
            await Frames(6);
        }
    }

    private async Task CheckSessionTransition(Vector3 start)
    {
        await Place(start);
        var pose = _player.CapturePortableTransform();
        var layer = _player.CollisionLayer;
        var mask = _player.CollisionMask;
        var mode = Input.MouseMode;
        var crouched = _player.IsCrouching;
        var jumps = _player.GroundedJumps;
        try
        {
            _player.SetSessionTransition(true);
            foreach (var action in new[] { "move_forward", "sprint", "jump", "crouch", "interact" }) Input.ActionPress(action);
            _player._UnhandledInput(new InputEventMouseMotion
                { Relative = ScreenMouseProbe, ScreenRelative = ScreenMouseProbe });
            await Frames(5);
            Check(_player.CapturePortableTransform().Equals(pose) && _player.Velocity == Vector3.Zero
                && !_player.IsSprinting && _player.IsCrouching == crouched && _player.GroundedJumps == jumps,
                "session projection holds the exact supplied pose, camera and stance despite held inputs");
            Check(_player.ModalOpen && !_player.CanEnterVehicle(out _)
                && !_player.VehicleControlled && _player.CollisionLayer == layer && _player.CollisionMask == mask
                && Input.MouseMode == mode,
                "session projection gates interaction without taking modal, mouse or transport ownership");
            // The release boundary must retain a real UI modal opened during
            // restoration; ending a load cannot silently close that owner.
            _player.SetModalOpen(true);
            _player.SetSessionTransition(false);
            Check(_player.ModalOpen && Input.MouseMode == Input.MouseModeEnum.Visible,
                "ending session projection preserves the real modal and its mouse mode");
            ReleaseInputs(); Input.ActionRelease("interact");
            _player.SetModalOpen(false);
            await Frames(3);
            Check(!_player.ModalOpen && _player.IsOnFloor(), "normal control resumes after load and modal release");
        }
        finally
        {
            ReleaseInputs(); Input.ActionRelease("interact");
            _player.SetSessionTransition(false);
            _player.SetModalOpen(false);
        }
    }

    private async Task<float> MeasureTravel(Vector3 start, bool sprint, float strength = 1f, bool diagonal = false)
    {
        await Place(start);
        var from = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        try
        {
            Input.ActionPress("move_forward", strength);
            if (diagonal) Input.ActionPress("move_right", strength);
            if (sprint) Input.ActionPress("sprint");
            await Frames(30);
        }
        finally { ReleaseInputs(); }
        var distance = new Vector2(_player.GlobalPosition.X - from.X, _player.GlobalPosition.Z - from.Z).Length();
        Check(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries,
            "measured movement uses input with no relocation or fall recovery");
        _events.Add(new { kind = "movement", sprint, strength, diagonal, distance, physicsTicks = 30 });
        return distance;
    }

    private async Task Place(Vector3 point)
    {
        ReleaseInputs();
        Check(_player.CanStandAt(point), "the explicit local road fixture fits the real standing capsule");
        _player.ApplyZoneSpawn(point, 0);
        await Frames(6);
        Check(_player.IsOnFloor(), "local start has actual ground contact");
    }

    private void CheckSoleSupport(string stance)
    {
        var soles = _player.VisibleBodySoles;
        foreach (var sole in soles)
        {
            using var ray = PhysicsRayQueryParameters3D.Create(sole + Vector3.Up * .12f, sole - Vector3.Up * .12f,
                _player.CollisionMask, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
            var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            Check(hit.Count > 0 && Math.Abs(hit["position"].AsVector3().Y - sole.Y) < .045f,
                stance + " imported boot sole has a nearby real support");
        }
        _events.Add(new { kind = "body-soles", stance, soles = soles.Select(point => point.ToString()).ToArray() });
    }

    private StaticBody3D Fixture(string name, Vector3 size, Vector3 position)
    {
        var body = new StaticBody3D { Name = name, Position = position, CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("ad8445") } });
        AddChild(body);
        return body;
    }

    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_PLAYER_CAPTURE") != "1") return;
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "player/" + name);
        Directory.CreateDirectory(Output);
        var file = Path.Combine(Output, name + ".png");
        if (File.Exists(file)) throw new IOException("Refusing to overwrite " + file);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(file) != Error.Ok) throw new IOException(file);
        _events.Add(new { kind = "game-camera", file });
    }

    private static void ReleaseInputs() { foreach (var action in Actions) Input.ActionRelease(action); }
    private async Task Press(string action) { Input.ActionPress(action); await Frames(2); Input.ActionRelease(action); await Frames(3); }
    private async Task Frames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true })
                throw new InvalidOperationException("The actual pause menu interrupted the player movement check.");
        }
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        GD.Print("act1-player-movement: " + message);
        _events.Add(new { kind = "check", message, passed = true });
    }
}
