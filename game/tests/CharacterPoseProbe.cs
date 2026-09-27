using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic for the kit skeleton and the held poses the fence scene drives.
/// Headless it prints measured bone axes; with URMAN_POSE_FRAMES set it also
/// renders the phone-boy and Tamara from three fixed cameras so a pose can be
/// judged in seconds instead of through the whole cutscene. Read-only: no
/// narrative state, no save writes.
/// </summary>
public partial class CharacterPoseProbe : Node
{
    private static readonly string[] Bones =
        ["root", "pelvis", "spine_01", "spine_03", "neck_01", "head",
         "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
         "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
         "thigh_r", "calf_r", "foot_r"];

    private string? _output;
    private Camera3D _camera = null!;

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            _output = System.Environment.GetEnvironmentVariable("URMAN_POSE_FRAMES");
            if (!string.IsNullOrEmpty(_output)) Directory.CreateDirectory(_output);
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("no start");
            GD.Print($"pose-probe graphics: adapter={RenderingServer.GetVideoAdapterName()} type={RenderingServer.GetVideoAdapterType()} " +
                     $"preset={GraphicsQuality.Preset} scale={GetViewport().Scaling3DScale} msaa={GetViewport().Msaa3D}");
            demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var quest = TamaraFenceQuest.Current(GetTree())!;
            var guy = quest.GuyActor;
            quest.DebugExposeGuyForTest(out _, out var skeleton, out var animation, out var phone);
            if (skeleton is null || animation is null)
            {
                GD.Print("pose-probe: no skeleton or animation player");
                GetTree().Quit(2);
                return;
            }

            GD.Print($"pose-probe: skeleton={skeleton.Name} bones={skeleton.GetBoneCount()} " +
                     $"phoneAttached={phone is not null}");
            if (string.IsNullOrEmpty(_output))
            {
                Dump(skeleton, "rest (idle clip playing)");
                animation.Pause();
                foreach (var bone in new[] { "upperarm_r", "lowerarm_r", "hand_r" })
                {
                    var index = skeleton.FindBone(bone);
                    if (index >= 0) skeleton.SetBonePose(index, Transform3D.Identity);
                }

                Dump(skeleton, "identity arms");
            }
            else
            {
                await RenderPoses(quest, guy, skeleton);
            }

            exit = 0;
        }
        catch (Exception error)
        {
            GD.PushError("pose-probe failed: " + error);
        }
        finally
        {
            GetTree().Quit(exit);
        }
    }

    private async Task RenderPoses(TamaraFenceQuest quest, Node3D guy, Skeleton3D skeleton)
    {
        _camera = new Camera3D { Name = "PoseProbeCamera", Fov = 45f, Near = .05f };
        GetTree().Root.AddChild(_camera);
        _camera.MakeCurrent();

        // The boy stands on the empty return street, well clear of the wreck,
        // and keeps facing +Z so the measured bone aims read plainly.
        var spot = new Vector3(0f, 0f, -52f);
        guy.GlobalPosition = new Vector3(spot.X, Ground(spot.X, spot.Z), spot.Z);
        guy.RotationDegrees = new Vector3(0, 0, 0);
        guy.Visible = true;
        quest.SetGuyFilming(true);
        quest.UpdateGuyFilming(0, 0f);
        await Frames(4);
        await Shoot(new(spot.X, 1.35f, spot.Z + 2.4f), spot + new Vector3(0, 1.2f, 0), "guy_front");
        await Shoot(new(spot.X + 1.4f, 1.4f, spot.Z + 2.0f), spot + new Vector3(0, 1.15f, 0), "guy_three_quarter");
        await Shoot(spot + new Vector3(1.9f, 1.3f, .35f), spot + new Vector3(0, 1.15f, 0), "guy_side");
        await Shoot(new(spot.X, 1.5f, spot.Z - 2.6f), spot + new Vector3(0, 1.2f, 0), "guy_back");

        quest.SetGuyFilming(false);
        GeneratedCharacterKitDressing.PlayClip(guy, "Idle");
        await Frames(4);
        await Shoot(new(spot.X, 1.35f, spot.Z + 2.4f), spot + new Vector3(0, 1.2f, 0), "guy_idle_front");

        var tamara = quest.TamaraActor;
        var tamaraSpot = new Vector3(-1.5f, 0f, -51f);
        tamara.GlobalPosition = new Vector3(tamaraSpot.X, Ground(tamaraSpot.X, tamaraSpot.Z), tamaraSpot.Z);
        tamara.RotationDegrees = new Vector3(0, 0, 0);
        tamara.Visible = true;
        GeneratedCharacterKitDressing.PlayClip(tamara, "Idle");
        await Frames(6);
        var cuffSubject = System.Environment.GetEnvironmentVariable("URMAN_POSE_CUFFS");
        if (cuffSubject is "1" or "PhoneGuy")
        {
            var actor = cuffSubject == "PhoneGuy" ? guy : tamara;
            var prefix = cuffSubject == "PhoneGuy" ? "PhoneGuy" : "Tamara";
            var body = actor.FindChildren("*", nameof(MeshInstance3D), true, false)
                .OfType<MeshInstance3D>().First(mesh => mesh.Name == prefix + "_Body_LOD0");
            var rig = body.GetNode<Skeleton3D>(body.Skeleton);
            var pose = actor.FindChildren("*", nameof(AnimationPlayer), true, false)
                .OfType<AnimationPlayer>().First(player => player.HasAnimation(prefix + "_Idle"));
            foreach (var clip in new[] { "Idle", cuffSubject == "PhoneGuy" ? "Filming" : "Talk", "Walk" })
            {
                if (actor == guy) quest.SetGuyFilming(clip == "Filming");
                if (!GeneratedCharacterKitDressing.PlayClip(actor, clip == "Filming" ? "Idle" : clip))
                    throw new InvalidOperationException("Missing " + prefix + " clip: " + clip);
                pose.Seek(.4, true);
                pose.Pause();
                await Frames(4);
                await Shoot(actor.GlobalPosition + new Vector3(0, 1.35f, 2.4f),
                    actor.GlobalPosition + new Vector3(0, 1.2f, 0), "cuffs_" + clip);
                foreach (var side in new[] { "l", "r" })
                {
                    var hand = rig.FindBone("hand_" + side);
                    if (hand < 0) throw new InvalidOperationException("Missing hand bone: " + side);
                    var wrist = rig.GlobalTransform * rig.GetBoneGlobalPose(hand).Origin;
                    await Shoot(wrist + new Vector3(Mathf.Sign(wrist.X - actor.GlobalPosition.X) * .3f, .08f, .4f),
                        wrist, "cuff_" + clip + "_" + side);
                }
            }
            return;
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_POSE_LOOK") == "1")
        {
            var body = tamara.FindChildren("*", nameof(MeshInstance3D), true, false)
                .OfType<MeshInstance3D>().First(mesh => mesh.Name == "Tamara_Body_LOD0");
            var rig = body.GetNode<Skeleton3D>(body.Skeleton);
            var head = rig.FindBone("Head");
            if (head < 0) throw new InvalidOperationException("Tamara rig has no Head bone.");
            var basis = rig.GlobalBasis * rig.GetBoneGlobalRest(head).Basis;
            GD.Print($"tamara-look: rest axes X={basis.X} Y={basis.Y} Z={basis.Z}");
            var target = new Node3D { Name = "TamaraLookProbe" };
            GetTree().Root.AddChild(target);
            target.GlobalPosition = tamara.GlobalPosition + new Vector3(0, 1.56f, 3);
            var look = new LookAtModifier3D
            {
                Name = "TamaraLook", BoneName = "Head",
                ForwardAxis = basis.Z.Dot(tamara.GlobalBasis.Z) > 0
                    ? SkeletonModifier3D.BoneAxis.PlusZ : SkeletonModifier3D.BoneAxis.MinusZ,
                PrimaryRotationAxis = Vector3.Axis.Y, UseSecondaryRotation = false,
                UseAngleLimitation = true, PrimaryLimitAngle = Mathf.DegToRad(35),
                Duration = .25f
            };
            rig.AddChild(look);
            look.TargetNode = look.GetPathTo(target);
            foreach (var angle in new[] { 0, -25, 25 })
            {
                target.GlobalPosition = tamara.GlobalPosition + new Vector3(
                    Mathf.Sin(Mathf.DegToRad(angle)) * 3, 1.56f, Mathf.Cos(Mathf.DegToRad(angle)) * 3);
                await Frames(20);
                await Shoot(tamara.GlobalPosition + new Vector3(0, 1.56f, 1.2f),
                    tamara.GlobalPosition + new Vector3(0, 1.5f, 0), "look_" + angle);
            }
            return;
        }
        if (System.Environment.GetEnvironmentVariable("URMAN_POSE_QUALITY") == "1")
        {
            var pose = tamara.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().First();
            pose.Seek(.2, true);
            pose.Pause();
            foreach (var preset in new[] { "low", "medium", "high" })
            {
                PainterlyMaterialLibrary.SetGraphicsPreset(preset);
                GraphicsQuality.Apply(GetViewport(), preset);
                GetWindow().GrabFocus();
                await Frames(20);
                var timings = new List<double>();
                var drawn = Engine.GetFramesDrawn();
                var focused = GetWindow().HasFocus();
                for (var frame = 0; frame < 120; frame++)
                {
                    var started = Time.GetTicksUsec();
                    await Frames(1);
                    timings.Add((Time.GetTicksUsec() - started) / 1000.0);
                    focused &= GetWindow().HasFocus();
                }
                File.WriteAllText(Path.Combine(_output!, "quality_" + preset + ".json"),
                    System.Text.Json.JsonSerializer.Serialize(new { preset, focused,
                        drawn = Engine.GetFramesDrawn() - drawn, milliseconds = timings }));
                if (GetViewport().World3D.Environment.SsaoEnabled != (preset != "low"))
                    throw new InvalidOperationException("Village SSAO did not follow the selected preset: " + preset);
                await Shoot(new(tamaraSpot.X, 1.35f, tamaraSpot.Z + 2.4f),
                    tamaraSpot + new Vector3(0, 1.2f, 0), "quality_" + preset);
                GD.Print($"pose-probe quality: preset={GraphicsQuality.Preset} scale={GetViewport().Scaling3DScale} msaa={GetViewport().Msaa3D}");
            }
            return;
        }
        await Shoot(new(tamaraSpot.X, 1.35f, tamaraSpot.Z + 2.4f), tamaraSpot + new Vector3(0, 1.2f, 0), "tamara_idle");
        await Shoot(tamara.GlobalPosition+new Vector3(0,1.56f,.85f), tamara.GlobalPosition+new Vector3(0,1.56f,0), "tamara_face_idle");
        await Shoot(tamara.GlobalPosition + new Vector3(.85f, 1.56f, .06f),
            tamara.GlobalPosition + new Vector3(0, 1.56f, 0), "tamara_face_profile");
        await Shoot(tamara.GlobalPosition + new Vector3(.15f, 1.56f, -.85f),
            tamara.GlobalPosition + new Vector3(0, 1.56f, 0), "tamara_hair_rear");
        GeneratedCharacterKitDressing.PlayClip(tamara, "Talk");
        await Frames(20);
        await Shoot(new(tamaraSpot.X, 1.35f, tamaraSpot.Z + 2.4f), tamaraSpot + new Vector3(0, 1.2f, 0), "tamara_talk");
        await Shoot(tamara.GlobalPosition+new Vector3(.65f,1.56f,.75f), tamara.GlobalPosition+new Vector3(0,1.56f,0), "tamara_face_talk");
        GeneratedCharacterKitDressing.PlayClip(tamara, "Walk");
        await Frames(12);
        await Shoot(tamaraSpot + new Vector3(2.2f, 1.3f, .6f), tamaraSpot + new Vector3(0, 1.1f, 0), "tamara_walk");
        // The shared hair bind correction also affects the other female heads.
        foreach (var prefix in new[] { "Naila", "Alsu" })
        {
            tamara.Visible = false;
            var actor = new Node3D { Name = prefix + "HairProbe", Position = tamara.GlobalPosition };
            GetTree().Root.AddChild(actor);
            var instance = GeneratedCharacterKitDressing.Attach(actor, prefix + "HairProbe", prefix, Vector3.Zero);
            foreach (var clip in new[] { "Idle", "Talk" })
            {
                if (!GeneratedCharacterKitDressing.PlayClip(instance, clip))
                    throw new InvalidOperationException("Missing " + prefix + " clip: " + clip);
                await Frames(6);
                await Shoot(actor.GlobalPosition + new Vector3(0, 1.48f, .95f),
                    actor.GlobalPosition + new Vector3(0, 1.48f, 0), prefix + "_face_" + clip);
            }
            actor.Free();
        }
        tamara.Visible = true;
        if (System.Environment.GetEnvironmentVariable("URMAN_POSE_MOTION") == "1")
        {
            _camera.GlobalPosition = tamaraSpot + new Vector3(1.35f, 1.15f, 2.7f);
            _camera.LookAt(tamaraSpot + new Vector3(0, .85f, 0), Vector3.Up);
            foreach (var clip in new[] { "Idle", "Talk", "Walk" })
            {
                if (!GeneratedCharacterKitDressing.PlayClip(tamara, clip))
                    throw new InvalidOperationException("Missing Tamara clip: " + clip);
                var timestamps = new List<ulong>();
                var started = Time.GetTicksMsec();
                do
                {
                    await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
                    await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "tamara-motion/" + clip);
                    timestamps.Add(Time.GetTicksMsec() - started);
                    using var frame = GetViewport().GetTexture().GetImage();
                    if (frame.SaveJpg(Path.Combine(_output!, $"motion_{clip}_{timestamps.Count:000}.jpg"), .9f) != Error.Ok)
                        throw new IOException("Could not save Tamara motion frame.");
                } while (Time.GetTicksMsec() - started < 3000);
                File.WriteAllText(Path.Combine(_output!, $"motion_{clip}.json"),
                    System.Text.Json.JsonSerializer.Serialize(timestamps));
            }
        }
        GD.Print($"pose-probe: frames -> {_output}");
    }

    private async Task Shoot(Vector3 position, Vector3 look, string name)
    {
        _camera.GlobalPosition = position;
        _camera.LookAt(look, Vector3.Up);
        await Frames(3);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "pose-probe/" + name);
        using var image = GetViewport().GetTexture().GetImage();
        image.Convert(Image.Format.Rgba8);
        var path = Path.Combine(_output!, name + ".png");
        image.SavePng(path);
        GD.Print($"pose-probe: saved {path}");
    }

    private float Ground(float x, float z) =>
        Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z);

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Dump(Skeleton3D skeleton, string label)
    {
        GD.Print($"pose-probe: --- {label} ---");
        foreach (var name in Bones)
        {
            var index = skeleton.FindBone(name);
            if (index < 0)
            {
                GD.Print($"pose-probe:   {name}: (absent)");
                continue;
            }

            var parent = skeleton.GetBoneParent(index);
            var parentPose = parent >= 0 ? skeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
            var global = skeleton.GetBoneGlobalPose(index);
            var y = global.Basis.Y.Normalized();
            var z = global.Basis.Z.Normalized();
            GD.Print($"pose-probe:   {name} parent={(parent >= 0 ? skeleton.GetBoneName(parent) : "-")} " +
                     $"aim=({y.X:0.00},{y.Y:0.00},{y.Z:0.00}) front=({z.X:0.00},{z.Y:0.00},{z.Z:0.00}) " +
                     $"origin=({global.Origin.X:0.00},{global.Origin.Y:0.00},{global.Origin.Z:0.00}) " +
                     $"parentY=({parentPose.Basis.Y.X:0.00},{parentPose.Basis.Y.Y:0.00},{parentPose.Basis.Y.Z:0.00})");
        }
    }
}
