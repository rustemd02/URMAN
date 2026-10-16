using Godot;

namespace Urman.Godot.Tests;

public partial class FullGameDressingCapture : Node
{
    private static readonly IReadOnlyDictionary<string, string> SceneByZone = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fullgame_act2_house"] = "res://scenes/zones/fullgame/act2_house.tscn",
        ["fullgame_act2_river"] = "res://scenes/zones/fullgame/act2_river.tscn",
        ["fullgame_act3_archive"] = "res://scenes/zones/fullgame/act3_archive.tscn",
        ["fullgame_act3_soviet"] = "res://scenes/zones/fullgame/act3_soviet.tscn",
        ["fullgame_act4_pact"] = "res://scenes/zones/fullgame/act4_pact.tscn",
        ["fullgame_act5_boundary"] = "res://scenes/zones/fullgame/act5_boundary.tscn",
        ["fullgame_act5_epilogue"] = "res://scenes/zones/fullgame/act5_epilogue.tscn"
    };

    private static readonly (string Name, string ZoneId, Vector3 Camera, Vector3 Target)[] Frames =
    [
        ("act2_house", "fullgame_act2_house", new Vector3(0, 1.7f, 11.5f), new Vector3(-2.5f, 1.35f, -6.2f)),
        ("act2_river", "fullgame_act2_river", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.35f, -6.2f)),
        ("act3_archive", "fullgame_act3_archive", new Vector3(0, 1.7f, 11.5f), new Vector3(0, 1.35f, -6.0f)),
        ("act3_soviet", "fullgame_act3_soviet", new Vector3(0, 1.7f, 11.5f), new Vector3(-0.65f, 1.35f, -3.65f)),
        ("act4_pact", "fullgame_act4_pact", new Vector3(0, 1.7f, 11.5f), new Vector3(0, 1.3f, -5.8f)),
        ("act5_boundary", "fullgame_act5_boundary", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.35f, -7.2f)),
        ("act5_epilogue", "fullgame_act5_epilogue", new Vector3(0, 1.7f, 12.5f), new Vector3(-3.8f, 1.35f, -7.5f))
    ];

    // W02 «Мостки» builds itself procedurally, so these frames are the only
    // way to look at the physical photo place before the author does. The
    // facts flag mirrors the authored state: parted reeds, settled board and
    // the water that has already answered.
    private static readonly (string Name, Vector3 Camera, Vector3 Target, bool ApplyFacts)[] RiverFrames =
    [
        ("w02_entry", new Vector3(0f, 2.9f, -2.5f), new Vector3(1.2f, 1.6f, 12f), false),
        ("w02_vista", new Vector3(0.2f, 2.9f, 15.2f), new Vector3(-18f, 0.9f, 24.5f), false),
        ("w02_bay", new Vector3(-15.4f, 2.2f, 24.9f), new Vector3(-46f, 0.8f, 27f), false),
        ("w02_bay_outside", new Vector3(-24f, 3.4f, 29.5f), new Vector3(-16f, 1.2f, 25f), false),
        ("w02_bay_high", new Vector3(-8f, 7f, 19f), new Vector3(-16f, 0.3f, 25f), false),
        ("w02_reflection", new Vector3(8.8f, 2.95f, 29.6f), new Vector3(5.2f, 0.15f, 32.6f), false),
        ("w02_reed_turn", new Vector3(22.6f, 2.65f, 39.8f), new Vector3(26.2f, 1.5f, 43.9f), false),
        ("w02_reed_open", new Vector3(22.6f, 2.65f, 39.8f), new Vector3(26.2f, 1.5f, 43.9f), true),
        ("w02_old_approach", new Vector3(38.4f, 2.95f, 62.2f), new Vector3(33.2f, 0.4f, 64.8f), false),
        ("w02_far_pier", new Vector3(46.6f, 3f, 83.2f), new Vector3(33.2f, 1.5f, 84f), false),
        ("w02_answer", new Vector3(35.4f, 3f, 82.4f), new Vector3(31f, 1.1f, 85.8f), true),
        ("w02_summer_bank", new Vector3(64.5f, 3.6f, 100.5f), new Vector3(79f, 2.6f, 117f), false)
    ];

    // The home side of the water chain becomes physical only in the
    // runtime-backed photoworlds home, so these frames instantiate the real
    // full-game scene. The camera stands at the authored house entry spawn and
    // looks at the family table; the close frame steps from the envelope back
    // towards that entry, which is the way a player approaches the table.
    private const int HomeFrameCount = 3;

    public override async void _Ready()
    {
        var outputDirectory = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/art/fullgame_frames");
        var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        if (directoryError != Error.Ok)
        {
            Fail($"Could not create full-game frame directory: {directoryError}.");
            return;
        }

        foreach (var frame in Frames)
        {
            if (!await CaptureAsync(frame.Name, frame.ZoneId, frame.Camera, frame.Target, outputDirectory))
            {
                return;
            }
        }

        foreach (var frame in RiverFrames)
        {
            if (!await CaptureRiverAsync(frame.Name, frame.Camera, frame.Target, frame.ApplyFacts, outputDirectory))
            {
                return;
            }
        }

        if (!await CaptureHomeAsync(outputDirectory))
        {
            return;
        }

        GD.Print($"fullgame-dressing-capture: {Frames.Length + RiverFrames.Length + HomeFrameCount} frames at 1920x1080 -> {outputDirectory}");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureHomeAsync(string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/full_game.tscn");
        if (packed is null)
        {
            Fail("Could not load the full-game runtime scene for the home frames.");
            return false;
        }

        var viewport = new SubViewport
        {
            Name = "Capture_home_runtime",
            Size = new Vector2I(1920, 1080),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var main = packed.Instantiate<Main>();
        main.InitialZoneId = "house_old_pc";
        main.InitialSpawnPointId = "entry";
        var bridge = main.GetNode<RuntimeBridge>("RuntimeBridge");
        bridge.CurrentZoneId = "house_old_pc";
        bridge.CurrentSpawnPointId = "entry";
        viewport.AddChild(main);

        for (var frame = 0; frame < 14; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // Only the authored route reaches the physical home; the capture walks
        // the real prologue skip and the Niva stop instead of faking a scene.
        if (!await bridge.DispatchInteractionAsync("urman.fullgame:interaction/pw-prologue-skip"))
        {
            Fail("The runtime-backed home capture could not skip the prologue.");
            return false;
        }
        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (!await bridge.DispatchInteractionAsync("urman.fullgame:interaction/pw-niva-stop"))
        {
            Fail("The runtime-backed home capture could not stop the Niva at the house.");
            return false;
        }

        // The house interior lives inside the connected world but shows only
        // while it is the active logical zone; walking in through the door is
        // normal gameplay, so the capture takes the same path.
        main.SwitchZone("house_old_pc", "entry");
        var settleDeadline = Time.GetTicksMsec() + 4000;
        while (Time.GetTicksMsec() < settleDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var playerCamera = main.GetNodeOrNull<Camera3D>("Player/Head/Camera3D");
        if (playerCamera is not null)
        {
            playerCamera.Current = false;
        }

        Act1ConnectedWorld? world = null;
        Node3D? zone = null;
        for (var attempt = 0; attempt < 900 && zone is null; attempt++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            world = GetTree().GetFirstNodeInGroup("act1_connected_world") as Act1ConnectedWorld;
            zone = world?.GetZoneInstance("house_old_pc") as Node3D;
        }
        if (world is null || zone is null)
        {
            Fail("The runtime-backed home did not expose the connected house zone.");
            return false;
        }

        var envelopeNode = zone.FindChild("FamilyWaterEnvelope", true, false) as Node3D;
        var tableNode = zone.FindChild("HouseInterior_TableTop_LOD0", true, false) as MeshInstance3D;
        var bookNode = zone.FindChild("FamilyTukayBookPresentation", true, false) as Node3D;
        if (envelopeNode is null || tableNode is null || bookNode is null)
        {
            Fail("The runtime-backed home is missing the family table, book or water envelope.");
            return false;
        }
        if (!world.TryGetWorldSpawn("house_old_pc", "entry", out var entry))
        {
            Fail("The runtime-backed home has no authored entry spawn.");
            return false;
        }

        GD.Print($"fullgame-home: zone={zone.GlobalPosition} rotation={zone.GlobalRotationDegrees} "
            + $"entry={entry.Position} table={tableNode.GlobalPosition} book={bookNode.GlobalPosition} "
            + $"envelope={envelopeNode.GlobalPosition}");
        GD.Print($"fullgame-home: envelopes={zone.FindChildren("FamilyWaterEnvelope", "", true, false).Count} "
            + $"things={zone.FindChildren("ArrivalPersonalThings", "", true, false).Count}");
        foreach (var node in new[] { bookNode, envelopeNode })
        {
            GD.Print($"fullgame-home: {node.Name} inTree={node.IsInsideTree()} visibleInTree={node.IsVisibleInTree()} "
                + $"visible={node.Visible} global={node.GlobalPosition}");
            foreach (var mesh in node.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            {
                GD.Print($"  {mesh.Name} visibleInTree={mesh.IsVisibleInTree()} visible={mesh.Visible} "
                    + $"at={mesh.GlobalPosition} size={mesh.GetAabb().Size}");
            }
        }

        var camera = new Camera3D { Name = "HomeCaptureCamera", Fov = 62f, Current = true };
        viewport.AddChild(camera);

        async Task<bool> ShotAsync(string name, Vector3 position, Vector3 look)
        {
            camera.Position = position;
            camera.LookAt(look, Vector3.Up);
            camera.ForceUpdateTransform();
            for (var settle = 0; settle < 8; settle++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            var image = viewport.GetTexture().GetImage();
            if (image is null || image.IsEmpty())
            {
                Fail("Home capture requires a real rendering driver.");
                return false;
            }

            var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_1080p.png");
            if (image.SavePng(outputPath) != Error.Ok)
            {
                Fail($"Could not save {outputPath}.");
                return false;
            }

            GD.Print($"fullgame-frame: {name} {image.GetWidth()}x{image.GetHeight()} camera={position}");
            var envelopeScreen = camera.UnprojectPosition(envelopeNode.GlobalPosition);
            var bookScreen = camera.UnprojectPosition(bookNode.GlobalPosition);
            GD.Print($"fullgame-frame: {name} screen envelope=({envelopeScreen.X:0},{envelopeScreen.Y:0}) "
                + $"book=({bookScreen.X:0},{bookScreen.Y:0})");
            return true;
        }

        var book = bookNode.GlobalPosition;
        var envelope = envelopeNode.GlobalPosition;
        var roomFront = tableNode.GlobalTransform.Basis.Z.Normalized();
        var cluster = (book + envelope) * .5f;
        var towardDoor = entry.Position - envelope;
        towardDoor = new Vector3(towardDoor.X, 0f, towardDoor.Z);
        towardDoor = towardDoor.LengthSquared() > .01f ? towardDoor.Normalized() : roomFront;

        // Front-room view of the family table, pulled back towards the door.
        if (!await ShotAsync("home_water_table",
            envelope + towardDoor * 2.2f + Vector3.Up * .8f,
            cluster + Vector3.Up * .02f))
        {
            return false;
        }

        // Close view straight down the book patch with the raised tier behind
        // it: the envelope lies on the tier, so one frame keeps both legible.
        if (!await ShotAsync("home_water_book",
            cluster + Vector3.Up * .62f + roomFront * .14f,
            cluster + Vector3.Up * .015f))
        {
            return false;
        }

        // Tight top-down of the envelope and its printed back on the tier.
        if (!await ShotAsync("home_water_envelope",
            envelope + Vector3.Up * .45f + roomFront * .10f,
            envelope + Vector3.Up * .01f))
        {
            return false;
        }

        await GodotSmokeCleanup.ReleaseAsync(main);
        viewport.Free();
        return true;
    }

    private async Task<bool> CaptureRiverAsync(
        string name,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        bool applyFacts,
        string outputDirectory)
    {
        const string scenePath = "res://scenes/photoworlds/W02/W02.tscn";
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load the W02 river scene: {scenePath}.");
            return false;
        }

        var viewport = new SubViewport
        {
            Name = $"Capture_{name}",
            Size = new Vector2I(1920, 1080),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var world = packed.Instantiate<Node3D>();
        viewport.AddChild(world);
        if (applyFacts && world is W02Controller controller)
        {
            controller.ApplyCaptureFacts(reedOpened: true, encounterResolved: true);
        }

        var camera = new Camera3D
        {
            Name = "CaptureCamera",
            Position = cameraPosition,
            Fov = 72,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);

        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            viewport.Free();
            Fail("W02 river capture requires a real rendering driver.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return false;
        }

        GD.Print($"fullgame-frame: {name} {image.GetWidth()}x{image.GetHeight()}");
        return true;
    }

    private async Task<bool> CaptureAsync(
        string name,
        string zoneId,
        Vector3 cameraPosition,
        Vector3 cameraTarget,
        string outputDirectory)
    {
        if (!SceneByZone.TryGetValue(zoneId, out var scenePath))
        {
            Fail($"No authored full-game capture scene is registered for {zoneId}.");
            return false;
        }

        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load full-game zone scene: {scenePath}.");
            return false;
        }

        var viewport = new SubViewport
        {
            Name = $"Capture_{name}",
            Size = new Vector2I(1920, 1080),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var zone = packed.Instantiate<FullGameZone>();
        viewport.AddChild(zone);

        var camera = new Camera3D
        {
            Name = "CaptureCamera",
            Position = cameraPosition,
            Fov = 75,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);

        for (var frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            viewport.Free();
            Fail("Full-game dressing capture requires a real rendering driver.");
            return false;
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{name}_1080p.png");
        var saveError = image.SavePng(outputPath);
        viewport.Free();
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return false;
        }

        GD.Print($"fullgame-frame: {name} {image.GetWidth()}x{image.GetHeight()}");
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
