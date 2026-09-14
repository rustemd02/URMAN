using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only visual evidence harness. It renders the three mandatory style
/// benchmark scenes at three camera distances and three FOV values, then
/// assembles one contact sheet per scene. No gameplay scene, runtime state or
/// material registry is modified by this capture.
/// </summary>
public partial class StyleMotionSweepCapture : Node
{
    private const int TileWidth = 640;
    private const int TileHeight = 360;
    private const int SheetColumns = 3;
    private const int SheetRows = 3;
    private const int WarmupFrames = 8;

    private static readonly float[] Fovs = [65f, 75f, 90f];

    private static readonly HashSet<string> V3CandidateBasenames =
    [
        "weathered_wood_boards_albedo.png",
        "aged_plaster_albedo.png",
        "damp_earth_albedo.png",
        "pine_foliage_albedo.png",
        "mossy_stone_albedo.png",
        "old_fabric_albedo.png"
    ];

    private static readonly HashSet<string> V4CandidateBasenames =
    [
        "weathered_wood_boards_albedo.png",
        "damp_earth_albedo.png"
    ];

    private static readonly HashSet<string> V5CandidateBasenames =
    [
        "weathered_wood_boards_albedo.png",
        "damp_earth_albedo.png"
    ];

    private static readonly HashSet<string> V6CandidateBasenames =
    [
        "weathered_wood_boards_albedo.png",
        "damp_earth_albedo.png"
    ];

    private static readonly SweepScene[] Scenes =
    [
        new(
            "day_street",
            "village_day",
            "res://scenes/zones/style_benchmark_day_street.tscn",
            [
                new(0, 1.7f, 5f, 0, 1.45f, -2.5f),
                new(0, 1.7f, 12.5f, 0, 1.45f, -7.5f),
                new(0, 1.7f, 19f, 0.2f, 1.45f, -13.5f)
            ],
            "HouseA_project_original"),
        new(
            "house_old_pc",
            "house_old_pc",
            "res://scenes/zones/style_benchmark_house_pc.tscn",
            [
                new(-1.25f, 1.68f, 0.25f, 0, 1.38f, -2.7f),
                new(-1.45f, 1.68f, 1.75f, 0, 1.38f, -3.45f),
                new(-1.45f, 1.68f, 3.15f, 0.2f, 1.38f, -4.35f)
            ],
            string.Empty),
        new(
            "kara_urman_edge",
            "kara_urman_night",
            "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            [
                new(0, 1.7f, 5f, 0.55f, 1.35f, -3.8f),
                new(0, 1.7f, 12.5f, 0.55f, 1.35f, -7.2f),
                new(0.2f, 1.7f, 17f, 0.4f, 1.35f, -12f)
            ],
            "PineA_project_original")
    ];

    public override async void _Ready()
    {
        if (Name.ToString().Contains("TextureV3", StringComparison.OrdinalIgnoreCase))
        {
            await RunCandidateSweepAsync("v3", "texture_candidate_motion_sweep");
            return;
        }

        if (Name.ToString().Contains("TextureV4", StringComparison.OrdinalIgnoreCase))
        {
            await RunCandidateSweepAsync("v4", "texture_candidate_motion_sweep_v4");
            return;
        }

        if (Name.ToString().Contains("TextureV5", StringComparison.OrdinalIgnoreCase))
        {
            await RunCandidateSweepAsync("v5", "texture_candidate_motion_sweep_v5");
            return;
        }

        if (Name.ToString().Contains("TextureV6", StringComparison.OrdinalIgnoreCase))
        {
            await RunCandidateSweepAsync("v6", "texture_candidate_motion_sweep_v6");
            return;
        }

        try
        {
            var outputDirectory = ProjectSettings.GlobalizePath(
                "res://../docs/urman_knowledge_base/art/style_motion_sweep");
            var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
            if (directoryError != Error.Ok)
            {
                Fail($"Could not create style-motion-sweep directory: {directoryError}.");
                return;
            }

            var manifestScenes = new List<object>(Scenes.Length);
            foreach (var scene in Scenes)
            {
                var capture = await CaptureSceneAsync(scene, outputDirectory);
                if (capture is null)
                {
                    return;
                }

                manifestScenes.Add(capture);
            }

            var manifestPath = System.IO.Path.Combine(outputDirectory, "style_motion_sweep_manifest.json");
            var manifestJson = JsonSerializer.Serialize(
                new
                {
                    schema_version = 1,
                    kind = "urman.godot_style_motion_sweep",
                    captured_at_utc = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    renderer_expectation = "Godot 4.7.1 .NET Forward+ / Metal or equivalent real 3D driver",
                    world = "zone scene assembled with Act1ConnectedWorld, the same owner the game uses",
                    tile = new { width = TileWidth, height = TileHeight },
                    sheet = new { columns = SheetColumns, rows = SheetRows, width = 1920, height = 1080 },
                    columns = new[] { 65, 75, 90 },
                    rows = new[] { "near", "mid", "far" },
                    scenes = manifestScenes
                },
                new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(manifestPath, manifestJson + System.Environment.NewLine);

            GD.Print($"style-motion-sweep: {Scenes.Length} contact sheets at 1920x1080 -> {outputDirectory}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Fail($"Style motion sweep failed: {exception}");
        }
    }

    /// <summary>
    /// Test-only variant used by focused Painterly candidate passes.
    /// It keeps the exact camera grid and scene ownership checks of the
    /// baseline sweep, but swaps versioned albedo textures on duplicated
    /// presentation materials in memory. Source scenes, runtime registries,
    /// collisions and narrative state are never edited.
    /// </summary>
    public async Task RunCandidateSweepAsync(string candidateVersion, string outputSubdirectory)
    {
        if (candidateVersion is not ("v3" or "v4" or "v5" or "v6"))
        {
            Fail($"Unsupported texture candidate sweep version '{candidateVersion}'.");
            return;
        }

        try
        {
            var outputDirectory = ProjectSettings.GlobalizePath(
                $"res://../docs/urman_knowledge_base/art/{outputSubdirectory}");
            var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
            if (directoryError != Error.Ok)
            {
                Fail($"Could not create candidate motion-sweep directory: {directoryError}.");
                return;
            }

            var manifestScenes = new List<object>(Scenes.Length);
            foreach (var scene in Scenes)
            {
                var capture = await CaptureCandidateSceneAsync(scene, outputDirectory, candidateVersion);
                if (capture is null)
                {
                    return;
                }

                manifestScenes.Add(capture);
            }

            var manifestPath = System.IO.Path.Combine(outputDirectory, "texture_candidate_motion_sweep_manifest.json");
            var manifestJson = JsonSerializer.Serialize(
                new
                {
                    schema_version = 1,
                    kind = "urman.godot_texture_candidate_motion_sweep",
                    candidate_version = candidateVersion,
                    captured_at_utc = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    renderer_expectation = "Godot 4.7.1 .NET Forward+ / Metal or equivalent real 3D driver",
                    tile = new { width = TileWidth, height = TileHeight },
                    sheet = new { columns = SheetColumns, rows = SheetRows, width = 1920, height = 1080 },
                    columns = new[] { 65, 75, 90 },
                    rows = new[] { "near", "mid", "far" },
                    scenes = manifestScenes
                },
                new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(manifestPath, manifestJson + System.Environment.NewLine);

            GD.Print($"texture-candidate-motion-sweep: version={candidateVersion} {Scenes.Length} contact sheets at 1920x1080 -> {outputDirectory}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Fail($"Texture candidate motion sweep failed: {exception}");
        }
    }

    private async Task<object?> CaptureSceneAsync(SweepScene scene, string outputDirectory)
    {
        if (!Act1WorldLayout.TryGetPlacement(scene.ZoneId, out var placement))
        {
            Fail($"Style motion sweep has no connected Act I placement for '{scene.ZoneId}'.");
            return null;
        }

        // The shipped first-person world is the zone scene plus the connected
        // Act I owner, which hides the benchmark ground/window/boundary
        // stand-ins and mounts the winter exterior. Sweeping the raw scene
        // would photograph geometry the player never sees, so the sweep
        // assembles the same world the game assembles and photographs that.
        var viewport = new SubViewport
        {
            Name = $"Capture_{scene.Name}",
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var world = new Act1ConnectedWorld { Name = $"StyleSweepWorld_{scene.Name}" };
        viewport.AddChild(world);
        world.SetActiveLogicalZone(scene.ZoneId);
        StripPhysicsForRenderOnlyCapture(world);

        var zoneInstance = world.GetChildren().OfType<Node3D>().FirstOrDefault(child =>
            child.HasMeta("logicalZoneId")
            && child.GetMeta("logicalZoneId").AsString() == scene.ZoneId);
        if (scene.ExpectedImportedModule.Length > 0
            && (zoneInstance is null
                || !zoneInstance.HasMeta("styleImportedModules")
                || zoneInstance.GetMeta("styleImportedModules").AsString() != scene.ExpectedImportedModule))
        {
            viewport.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Fail($"Style benchmark {scene.Name} did not materialize the expected imported module {scene.ExpectedImportedModule}.");
            return null;
        }

        var camera = new Camera3D { Name = "CaptureCamera", Fov = Fovs[0], Current = true };
        viewport.AddChild(camera);

        var sheet = Image.CreateEmpty(TileWidth * SheetColumns, TileHeight * SheetRows, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.035f, 0.043f, 0.045f, 1f));
        var frames = new List<object>(SheetRows * SheetColumns);

        for (var row = 0; row < SheetRows; row++)
        {
            for (var column = 0; column < SheetColumns; column++)
            {
                var spec = scene.Cameras[row];
                var position = placement.Origin + spec.Position;
                var target = placement.Origin + spec.Target;
                camera.Position = position;
                camera.Fov = Fovs[column];
                camera.LookAt(target, Vector3.Up);

                for (var frame = 0; frame < WarmupFrames; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                var image = viewport.GetTexture().GetImage();
                if (image is null || image.IsEmpty() || image.GetWidth() != TileWidth || image.GetHeight() != TileHeight)
                {
                    viewport.Free();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Fail($"Style motion sweep requires a real {TileWidth}x{TileHeight} rendering driver for {scene.Name} row {row}, column {column}.");
                    return null;
                }

                // SubViewport readback may use RGB8 while the contact sheet is
                // RGBA8. BlitRect requires an exact format match; normalize the
                // temporary readback before compositing so a failed blit cannot
                // silently produce an all-background evidence image.
                image.Convert(Image.Format.Rgba8);
                sheet.BlitRect(
                    image,
                    new Rect2I(0, 0, TileWidth, TileHeight),
                    new Vector2I(column * TileWidth, row * TileHeight));
                frames.Add(new
                {
                    row = row,
                    distance = row switch
                    {
                        0 => "near",
                        1 => "mid",
                        _ => "far"
                    },
                    column = column,
                    fov = Fovs[column],
                    camera = new
                    {
                        position = new { x = position.X, y = position.Y, z = position.Z },
                        target = new { x = target.X, y = target.Y, z = target.Z }
                    }
                });
            }
        }

        viewport.Free();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{scene.Name}_motion_sweep_1080p.png");
        var saveError = sheet.SavePng(outputPath);
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return null;
        }

        GD.Print($"style-motion-sweep: {scene.Name} {sheet.GetWidth()}x{sheet.GetHeight()} -> {outputPath}");
        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        var relativeOutputPath = System.IO.Path.GetRelativePath(repoRoot, outputPath).Replace('\\', '/');
        return new
        {
            name = scene.Name,
            scene = scene.ScenePath,
            zone_id = scene.ZoneId,
            assembled_world = "Act1ConnectedWorld",
            world_origin = new { x = placement.Origin.X, y = placement.Origin.Y, z = placement.Origin.Z },
            output = relativeOutputPath,
            expected_imported_module = scene.ExpectedImportedModule,
            frames
        };
    }

    private async Task<object?> CaptureCandidateSceneAsync(
        SweepScene scene,
        string outputDirectory,
        string candidateVersion)
    {
        var packed = ResourceLoader.Load<PackedScene>(scene.ScenePath);
        if (packed is null)
        {
            Fail($"Could not load style benchmark {scene.ScenePath}.");
            return null;
        }

        var sheet = Image.CreateEmpty(TileWidth * SheetColumns, TileHeight * SheetRows, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.035f, 0.043f, 0.045f, 1f));
        var frames = new List<object>(SheetRows * SheetColumns);
        var replacementCount = 0;

        for (var row = 0; row < SheetRows; row++)
        {
            for (var column = 0; column < SheetColumns; column++)
            {
                var frame = await CaptureCandidateTileAsync(
                    scene,
                    packed,
                    row,
                    column,
                    scene.Cameras[row],
                    Fovs[column],
                    candidateVersion);
                if (frame is null)
                {
                    return null;
                }

                replacementCount += frame.ReplacementCount;
                frame.Image.Convert(Image.Format.Rgba8);
                sheet.BlitRect(
                    frame.Image,
                    new Rect2I(0, 0, TileWidth, TileHeight),
                    new Vector2I(column * TileWidth, row * TileHeight));
                frames.Add(new
                {
                    row,
                    distance = row switch
                    {
                        0 => "near",
                        1 => "mid",
                        _ => "far"
                    },
                    column,
                    fov = Fovs[column],
                    camera = new
                    {
                        position = new { x = frame.Camera.Position.X, y = frame.Camera.Position.Y, z = frame.Camera.Position.Z },
                        target = new { x = frame.Camera.Target.X, y = frame.Camera.Target.Y, z = frame.Camera.Target.Z }
                    }
                });
            }
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{scene.Name}_texture_{candidateVersion}_motion_sweep_1080p.png");
        var saveError = sheet.SavePng(outputPath);
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return null;
        }

        GD.Print($"texture-candidate-motion-sweep: {scene.Name} {sheet.GetWidth()}x{sheet.GetHeight()} replaced_meshes={replacementCount}");
        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        var relativeOutputPath = System.IO.Path.GetRelativePath(repoRoot, outputPath).Replace('\\', '/');
        return new
        {
            name = scene.Name,
            scene = scene.ScenePath,
            output = relativeOutputPath,
            expected_imported_module = scene.ExpectedImportedModule,
            replacement_count = replacementCount,
            frames
        };
    }

    private async Task<CandidateCapturedTile?> CaptureCandidateTileAsync(
        SweepScene scene,
        PackedScene packed,
        int row,
        int column,
        SweepCamera cameraSpec,
        float fov,
        string candidateVersion)
    {
        var viewport = new SubViewport
        {
            Name = $"CandidateCapture_{scene.Name}_{row}_{column}",
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var sceneInstance = packed.Instantiate<Node3D>();
        viewport.AddChild(sceneInstance);
        StripPhysicsForRenderOnlyCapture(sceneInstance);
        var replacementCount = ReplaceCandidateMaterials(sceneInstance, candidateVersion);
        var camera = new Camera3D
        {
            Name = "CandidateCaptureCamera",
            Position = cameraSpec.Position,
            Fov = fov,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(cameraSpec.Target, Vector3.Up);

        for (var frame = 0; frame < WarmupFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (scene.ExpectedImportedModule.Length > 0
            && sceneInstance.GetMeta("styleImportedModules").AsString() != scene.ExpectedImportedModule)
        {
            viewport.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Fail($"Texture candidate benchmark {scene.Name} did not materialize expected imported module {scene.ExpectedImportedModule}.");
            return null;
        }

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty() || image.GetWidth() != TileWidth || image.GetHeight() != TileHeight)
        {
            viewport.Free();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Fail($"Texture candidate motion sweep requires a real {TileWidth}x{TileHeight} rendering driver for {scene.Name} row {row}, column {column}.");
            return null;
        }

        var captured = new CandidateCapturedTile(image, cameraSpec, cameraSpec.Target, replacementCount);
        viewport.Free();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return captured;
    }

    private static int ReplaceCandidateMaterials(Node root, string candidateVersion)
    {
        var replacements = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.MaterialOverride is ShaderMaterial source)
            {
                var texture = source.GetShaderParameter("albedo_texture").AsGodotObject() as Texture2D;
                var basename = texture is null ? string.Empty : System.IO.Path.GetFileName(texture.ResourcePath);
                if (basename.EndsWith("_albedo.png", StringComparison.Ordinal))
                {
                    var sourceStem = basename[..^"_albedo.png".Length];
                    foreach (var knownVersion in new[] { "_v2", "_v3", "_v4" })
                    {
                        if (sourceStem.EndsWith(knownVersion, StringComparison.Ordinal))
                        {
                            sourceStem = sourceStem[..^knownVersion.Length];
                            break;
                        }
                    }

                    var candidateBasenames = candidateVersion switch
                    {
                        "v4" => V4CandidateBasenames,
                        "v5" => V5CandidateBasenames,
                        "v6" => V6CandidateBasenames,
                        _ => V3CandidateBasenames
                    };
                    if (!candidateBasenames.Contains($"{sourceStem}_albedo.png"))
                    {
                        continue;
                    }

                    var candidateName = $"{sourceStem}_{candidateVersion}_albedo.png";
                    var candidatePath = $"res://assets/textures/painterly/{candidateName}";
                    var candidateAbsolutePath = ProjectSettings.GlobalizePath(candidatePath);
                    var candidate = System.IO.File.Exists(candidateAbsolutePath)
                        ? ResourceLoader.Load<Texture2D>(candidatePath)
                        : null;
                    if (candidate is not null)
                    {
                        var duplicate = source.Duplicate() as ShaderMaterial;
                        if (duplicate is not null)
                        {
                            duplicate.SetShaderParameter("albedo_texture", candidate);
                            duplicate.SetShaderParameter("has_albedo_texture", true);
                            mesh.MaterialOverride = duplicate;
                            replacements++;
                        }
                    }
                }
            }

            replacements += ReplaceCandidateMaterials(child, candidateVersion);
        }

        return replacements;
    }

    private static void StripPhysicsForRenderOnlyCapture(Node root)
    {
        // This sweep never moves a player or queries interactions. Disable and
        // remove only collision shapes from each disposable scene clone so
        // StaticBody3D-owned presentation meshes (road relief included) remain
        // visible while Shape3D RIDs are released before SubViewport teardown.
        if (root is CollisionObject3D collisionObject)
        {
            collisionObject.CollisionLayer = 0;
            collisionObject.CollisionMask = 0;
            collisionObject.InputRayPickable = false;
        }

        foreach (var child in root.GetChildren().ToArray())
        {
            if (child is CollisionObject3D childCollisionObject)
            {
                ReparentVisibleMeshesAndFree(childCollisionObject, root);
                child.Free();
                continue;
            }

            StripPhysicsForRenderOnlyCapture(child);
        }
    }

    private static void ReparentVisibleMeshesAndFree(CollisionObject3D collisionObject, Node newParent)
    {
        foreach (var mesh in FindMeshes(collisionObject).ToArray())
        {
            var globalTransform = mesh.GlobalTransform;
            mesh.GetParent()?.RemoveChild(mesh);
            newParent.AddChild(mesh);
            mesh.GlobalTransform = globalTransform;
        }

        static IEnumerable<MeshInstance3D> FindMeshes(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    yield return mesh;
                }

                foreach (var nested in FindMeshes(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record SweepScene(
        string Name,
        string ZoneId,
        string ScenePath,
        SweepCamera[] Cameras,
        string ExpectedImportedModule);

    private sealed record SweepCamera(Vector3 Position, Vector3 Target)
    {
        public SweepCamera(float x, float y, float z, float targetX, float targetY, float targetZ)
            : this(new Vector3(x, y, z), new Vector3(targetX, targetY, targetZ))
        {
        }
    }

    private sealed record CandidateCapturedTile(
        Image Image,
        SweepCamera Camera,
        Vector3 Target,
        int ReplacementCount);
}
