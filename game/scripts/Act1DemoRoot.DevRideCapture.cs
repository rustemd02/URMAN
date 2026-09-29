using Godot;

namespace Urman.Godot;

// Development only: URMAN_PROLOGUE_CAPTURE=<empty output dir> boots straight into the
// Niva ride, saves a handful of stills of the cabin and the car, and quits. Nothing
// happens when the variable is unset, so it never affects a normal run.
public partial class Act1DemoRoot
{
    private static string? DevRideCaptureDir => System.Environment.GetEnvironmentVariable("URMAN_PROLOGUE_CAPTURE");

    private async void DevRideCaptureBoot()
    {
        var dir = DevRideCaptureDir;
        if (string.IsNullOrEmpty(dir)) return;
        System.IO.Directory.CreateDirectory(dir);
        for (var frame = 0; frame < 600 && !(MainMenuVisible && _main is not null && _player is not null); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _mainMenu?.Dismiss();
        _mainMenu = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        BuildPrologueOverlay();
        _ = RunPrologueNivaRideAsync();
        for (var frame = 0; frame < 900 && _rideCamera is null; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (System.Environment.GetEnvironmentVariable("URMAN_PROLOGUE_NUMBERS") == "1")
        {
            for (var frame = 0; frame < 900 && _driverSkeleton is null; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(4.5), SceneTreeTimer.SignalName.Timeout);
            DrainPrologueColour(1f, 0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            DrainPrologueColour(0f, 0);
            var probeForest = new PrologueDeepForest();
            _main!.AddChild(probeForest);
            GD.Print($"ride-num forest eyes placed: {probeForest.EyeGlints.Count}");
            foreach (var glint in probeForest.EyeGlints)
                GD.Print($"ride-num eye at progress {probeForest.Progress(glint.GlobalPosition):F0} m, {Mathf.Abs(glint.Position.X - PrologueDeepForest.TrackX(glint.Position.Z)):F1} m off track, h {glint.Position.Y - PrologueDeepForest.Ground(glint.Position.X, glint.Position.Z):F1}");
            probeForest.QueueFree();
            // Modifier (IK, look-at) results exist only inside skeleton_updated.
            var printed = false;
            _driverSkeleton!.SkeletonUpdated += () => { if (!printed) { printed = true; PrintDriverNumbers(); } };
            for (var frame = 0; frame < 10 && !printed; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit();
            return;
        }
                var earlyShot = true;

        async System.Threading.Tasks.Task Shot(string name)
        {
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            GD.Print($"ride-capture: {name}");
        }

        if (earlyShot)
        {
            await Shot("shake_a");
            _rideLook = new Vector2(70, -26);
            ApplyPrologueRideLook();
            await ToSignal(GetTree().CreateTimer(.35), SceneTreeTimer.SignalName.Timeout);
            await Shot("shake_b");
        }
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        foreach (var (name, look) in new[] { ("cabin_driver_close", new Vector2(35, 0)), ("cabin_driver_wide", new Vector2(58, -6)), ("cabin_forward", new Vector2(0, 0)) })
        {
            _rideLook = look;
            ApplyPrologueRideLook();
            await Shot(name);
        }

        var outside = new Camera3D { Fov = 55f };
        _rideNiva!.AddChild(outside);
        foreach (var (name, at, target) in new[]
        {
            ("car_front_left", new Vector3(-3.6f, 1.5f, -4.2f), new Vector3(0, .9f, 0)),
            ("car_side_driver_window", new Vector3(-3.4f, 1.35f, .3f), new Vector3(-.4f, 1.2f, 0)),
            ("car_rear_over_seats", new Vector3(0f, 1.55f, 1.3f), new Vector3(0, 1.1f, -1.5f)),
            ("car_top_cabin", new Vector3(0f, 3.1f, -.2f), new Vector3(0, .9f, -.2f))
        })
        {
            outside.Position = at;
            outside.LookAt(_rideNiva.ToGlobal(target), Vector3.Up);
            outside.MakeCurrent();
            await Shot(name);
        }
        GetTree().Quit();
    }

    private void PrintDriverNumbers()
    {
        var sk = _driverSkeleton!;
        Vector3 At(string bone) => _rideNiva!.ToLocal(sk.ToGlobal(sk.GetBoneGlobalPose(sk.FindBone(bone)).Origin));
        foreach (var bone in new[] { "pelvis", "Head", "upperarm_l", "lowerarm_l", "hand_l", "hand_r", "calf_l", "calf_r", "foot_l", "foot_r", "ball_l", "ball_r", "middle_02_l", "middle_02_r" })
            GD.Print($"ride-num {bone,-12} {At(bone):F3}");
        var wheel = _rideNiva!.GetNodeOrNull<Node3D>("SteeringWheel");
        var grip = (At("middle_02_l") + At("middle_02_r")) / 2f;
        GD.Print($"ride-num grip centre {grip:F3}  wheel node {wheel?.Position:F3}  delta {(grip - (wheel?.Position ?? Vector3.Zero)):F3}");
        if (wheel is not null)
        {
            var normal = (_rideNiva.GlobalBasis.Inverse() * wheel.GlobalBasis * Vector3.Back).Normalized();
            foreach (var finger in new[] { "middle_02_l", "middle_02_r" })
            {
                var q = At(finger) - wheel.Position; var off = q.Dot(normal); q -= normal * off;
                GD.Print($"ride-num {finger} radial {q.Length():F3} (rim .205) off-plane {off:F3}");
            }
        }
        GD.Print($"ride-num knee height l {At("calf_l").Y:F3} r {At("calf_r").Y:F3} (wheel bottom ~0.92)");
        foreach (var side in new[] { "l", "r" })
        {
            var sh = At("upperarm_" + side); var el = At("lowerarm_" + side); var wr = At("hand_" + side);
            var bend = Mathf.RadToDeg((sh - el).AngleTo(wr - el));
            GD.Print($"ride-num elbow {side}: angle {bend:F0} deg (180 = locked straight), elbow {(sh.Y - el.Y) * 100:F0} cm below shoulder");
        }
        GD.Print($"ride-num hands apart {(At("middle_02_l") - At("middle_02_r")).Length():F3} m (rim diameter 0.41)");
        var crown = _driver!.FindChildren("Mansur_Hair_LOD0", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (crown is not null) { var box = crown.GetAabb(); GD.Print($"ride-num hair mesh top (rest pose, approx) y={_rideNiva!.ToLocal(crown.ToGlobal(box.Position + box.Size)).Y:F3}"); }
        GD.Print($"ride-num crown ~ y={At("Head").Y + .16f:F3} (headliner 1.636); soles y {At("ball_l").Y:F3}/{At("ball_r").Y:F3} (floor ~0.51-0.60)");
        var look = sk.FindChildren("*", nameof(LookAtModifier3D), false, false).OfType<LookAtModifier3D>().FirstOrDefault();
        GD.Print($"ride-num lookat axis={look?.ForwardAxis} active={look?.Active}");
    }
}
