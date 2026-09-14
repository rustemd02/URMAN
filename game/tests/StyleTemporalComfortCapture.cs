using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only temporal evidence harness. It renders a controlled first-person
/// camera path in the three mandatory style scenes with the same small bob
/// amplitude as the player controller, then records frame-to-frame luminance
/// metrics and a final-frame contact sheet. It never instantiates gameplay
/// state, dispatches interactions or changes production materials.
/// </summary>
public partial class StyleTemporalComfortCapture : Node
{
    private const int TileWidth = 480;
    private const int TileHeight = 270;
    private const int WarmupFrames = 8;
    private const int MotionFrames = 12;
    private const int SampleStride = 16;
    private const float HeadBobAmplitude = 0.024f;
    private const float HeadSwayAmplitude = 0.012f;

    private static readonly float[] Fovs = [65f, 75f, 90f];

    private static readonly TemporalScene[] Scenes =
    [
        new(
            "day_street",
            "res://scenes/zones/style_benchmark_day_street.tscn",
            new Vector3(0, 1.7f, 12.5f),
            new Vector3(0, 1.45f, -7.5f),
            "HouseA_project_original"),
        new(
            "house_old_pc",
            "res://scenes/zones/style_benchmark_house_pc.tscn",
            new Vector3(-1.45f, 1.68f, 1.75f),
            new Vector3(0, 1.38f, -3.45f),
            string.Empty),
        new(
            "kara_urman_edge",
            "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            new Vector3(0, 1.7f, 12.5f),
            new Vector3(0.55f, 1.35f, -7.2f),
            "PineA_project_original")
    ];

    public override async void _Ready()
    {
        try
        {
            var outputDirectory = ProjectSettings.GlobalizePath(
                "res://../docs/urman_knowledge_base/art/style_temporal_sweep");
            var directoryError = DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
            if (directoryError != Error.Ok)
            {
                Fail($"Could not create style-temporal-sweep directory: {directoryError}.");
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

            var manifestPath = System.IO.Path.Combine(outputDirectory, "style_temporal_sweep_manifest.json");
            var manifestJson = JsonSerializer.Serialize(
                new
                {
                    schema_version = 1,
                    kind = "urman.godot_style_temporal_sweep",
                    captured_at_utc = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    renderer_expectation = "Godot 4.7.1 .NET Forward+ / Metal or equivalent real 3D driver",
                    tile = new { width = TileWidth, height = TileHeight },
                    contact_sheet = new { columns = 3, rows = 2, width = TileWidth * 3, height = TileHeight * 2 },
                    fovs = Fovs,
                    motion_frames = MotionFrames,
                    sample_stride = SampleStride,
                    head_bob_amplitude = HeadBobAmplitude,
                    head_sway_amplitude = HeadSwayAmplitude,
                    modes = new[] { "head_bob_on", "reduced_motion" },
                    status = "technical_temporal_evidence_only",
                    external_review_required = true,
                    scenes = manifestScenes
                },
                new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(manifestPath, manifestJson + System.Environment.NewLine);

            GD.Print($"style-temporal-sweep: {Scenes.Length} scenes, {MotionFrames} frames × {Fovs.Length} FOV × 2 modes");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Fail($"Style temporal sweep failed: {exception}");
        }
    }

    private async Task<object?> CaptureSceneAsync(TemporalScene scene, string outputDirectory)
    {
        var packed = ResourceLoader.Load<PackedScene>(scene.ScenePath);
        if (packed is null)
        {
            Fail($"Could not load style benchmark {scene.ScenePath}.");
            return null;
        }

        var sheet = Image.CreateEmpty(TileWidth * Fovs.Length, TileHeight * 2, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.035f, 0.043f, 0.045f, 1f));
        var modes = new List<object>(2);
        RenderOnlySanitization? sceneSanitization = null;

        for (var modeIndex = 0; modeIndex < 2; modeIndex++)
        {
            var reducedMotion = modeIndex == 1;
            var modeMetrics = new List<object>(Fovs.Length);
            for (var column = 0; column < Fovs.Length; column++)
            {
                var tile = await CapturePathAsync(scene, packed, Fovs[column], reducedMotion);
                if (tile is null)
                {
                    return null;
                }

                if (sceneSanitization is null)
                {
                    sceneSanitization = tile.Sanitization;
                }
                else if (sceneSanitization != tile.Sanitization)
                {
                    Fail($"Render-only sanitization drifted between temporal samples for {scene.Name}.");
                    return null;
                }

                tile.FinalImage.Convert(Image.Format.Rgba8);
                sheet.BlitRect(
                    tile.FinalImage,
                    new Rect2I(0, 0, TileWidth, TileHeight),
                    new Vector2I(column * TileWidth, modeIndex * TileHeight));
                modeMetrics.Add(new
                {
                    fov = Fovs[column],
                    mode = reducedMotion ? "reduced_motion" : "head_bob_on",
                    frame_count = tile.FrameCount,
                    mean_luminance_delta = tile.MeanLuminanceDelta,
                    max_luminance_delta = tile.MaxLuminanceDelta,
                    min_luminance = tile.MinLuminance,
                    max_luminance = tile.MaxLuminance,
                    black_pixel_fraction = tile.BlackPixelFraction,
                    camera_vertical_peak = tile.CameraVerticalPeak,
                    camera_horizontal_peak = tile.CameraHorizontalPeak,
                    render_valid = true
                });
            }

            modes.Add(new
            {
                mode = reducedMotion ? "reduced_motion" : "head_bob_on",
                metrics = modeMetrics
            });
        }

        var outputPath = System.IO.Path.Combine(outputDirectory, $"godot_{scene.Name}_temporal_sweep_1080p.png");
        var saveError = sheet.SavePng(outputPath);
        if (saveError != Error.Ok)
        {
            Fail($"Could not save {outputPath}: {saveError}.");
            return null;
        }

        var repoRoot = ProjectSettings.GlobalizePath("res://..");
        var relativeOutputPath = System.IO.Path.GetRelativePath(repoRoot, outputPath).Replace('\\', '/');
        GD.Print($"style-temporal-sweep: {scene.Name} {sheet.GetWidth()}x{sheet.GetHeight()} -> {outputPath}");
        if (sceneSanitization is null)
        {
            Fail($"Style temporal sweep did not record render-only sanitization for {scene.Name}.");
            return null;
        }
        return new
        {
            name = scene.Name,
            scene = scene.ScenePath,
            output = relativeOutputPath,
            expected_imported_module = scene.ExpectedImportedModule,
            render_only_sanitization = new
            {
                visual_mesh_count = sceneSanitization.VisualMeshCount,
                collision_shape_count_before = sceneSanitization.CollisionShapeCountBefore,
                collision_shapes_removed = sceneSanitization.CollisionShapesRemoved,
                collision_shape_count_after = sceneSanitization.CollisionShapeCountAfter,
                active_physics_query_owner_count = sceneSanitization.ActivePhysicsQueryOwnerCount
            },
            modes
        };
    }

    private async Task<TemporalCapture?> CapturePathAsync(
        TemporalScene scene,
        PackedScene packed,
        float fov,
        bool reducedMotion)
    {
        var viewport = new SubViewport
        {
            Name = $"Temporal_{scene.Name}_{fov}_{(reducedMotion ? "reduced" : "bob")}",
            Size = new Vector2I(TileWidth, TileHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        var sceneInstance = packed.Instantiate<Node3D>();
        viewport.AddChild(sceneInstance);
        var sanitization = SanitizePhysicsForRenderOnlyCapture(sceneInstance);
        if (sanitization.VisualMeshCount <= 0
            || sanitization.CollisionShapeCountAfter != 0
            || sanitization.ActivePhysicsQueryOwnerCount != 0)
        {
            await FreeViewportAsync(viewport);
            Fail(
                $"Render-only sanitization for {scene.Name} removed authored visuals or left physics queries active "
                + $"(visual_meshes={sanitization.VisualMeshCount}, "
                + $"collision_shapes_before={sanitization.CollisionShapeCountBefore}, "
                + $"collision_shapes_after={sanitization.CollisionShapeCountAfter}, "
                + $"active_physics_query_owners={sanitization.ActivePhysicsQueryOwnerCount}).");
            return null;
        }
        var camera = new Camera3D
        {
            Name = "TemporalCaptureCamera",
            Position = scene.Camera,
            Fov = fov,
            Current = true
        };
        viewport.AddChild(camera);

        for (var frame = 0; frame < WarmupFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (scene.ExpectedImportedModule.Length > 0
            && sceneInstance.GetMeta("styleImportedModules").AsString() != scene.ExpectedImportedModule)
        {
            await FreeViewportAsync(viewport);
            Fail($"Style benchmark {scene.Name} did not materialize expected imported module {scene.ExpectedImportedModule}.");
            return null;
        }

        Image? previous = null;
        Image? finalImage = null;
        double luminanceDeltaSum = 0;
        var maxLuminanceDelta = 0.0;
        var minLuminance = 1.0;
        var maxLuminance = 0.0;
        var blackPixels = 0L;
        var sampledPixels = 0L;
        var cameraVerticalPeak = 0.0f;
        var cameraHorizontalPeak = 0.0f;

        for (var frame = 0; frame < MotionFrames; frame++)
        {
            var phase = (float)(frame * Math.PI * 2.0 / MotionFrames);
            var horizontal = Mathf.Sin(phase) * HeadSwayAmplitude;
            var vertical = reducedMotion ? 0f : Mathf.Sin(phase * 2f) * HeadBobAmplitude;
            camera.Position = scene.Camera + new Vector3(horizontal, vertical, 0);
            camera.LookAt(scene.Target + new Vector3(Mathf.Sin(phase) * 0.045f, 0, 0), Vector3.Up);
            cameraVerticalPeak = Mathf.Max(cameraVerticalPeak, Mathf.Abs(vertical));
            cameraHorizontalPeak = Mathf.Max(cameraHorizontalPeak, Mathf.Abs(horizontal));

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var image = viewport.GetTexture().GetImage();
            if (image is null || image.IsEmpty() || image.GetWidth() != TileWidth || image.GetHeight() != TileHeight)
            {
                await FreeViewportAsync(viewport);
                Fail($"Style temporal sweep requires a real {TileWidth}x{TileHeight} rendering driver for {scene.Name} FOV {fov}.");
                return null;
            }

            image.Convert(Image.Format.Rgba8);
            finalImage = image;
            var metrics = MeasureFrame(image, previous);
            luminanceDeltaSum += metrics.MeanDelta;
            maxLuminanceDelta = Math.Max(maxLuminanceDelta, metrics.MaxDelta);
            minLuminance = Math.Min(minLuminance, metrics.MinLuminance);
            maxLuminance = Math.Max(maxLuminance, metrics.MaxLuminance);
            blackPixels += metrics.BlackPixels;
            sampledPixels += metrics.SampledPixels;
            previous = image;
        }

        await FreeViewportAsync(viewport);
        if (finalImage is null || sampledPixels == 0)
        {
            Fail($"Style temporal sweep produced no valid final frame for {scene.Name} FOV {fov}.");
            return null;
        }

        return new TemporalCapture(
            finalImage,
            MotionFrames,
            luminanceDeltaSum / Math.Max(1, MotionFrames - 1),
            maxLuminanceDelta,
            minLuminance,
            maxLuminance,
            (double)blackPixels / sampledPixels,
            cameraVerticalPeak,
            cameraHorizontalPeak,
            sanitization);
    }

    private static FrameMetrics MeasureFrame(Image image, Image? previous)
    {
        double deltaSum = 0;
        var maxDelta = 0.0;
        var minLuminance = 1.0;
        var maxLuminance = 0.0;
        var blackPixels = 0L;
        var sampledPixels = 0L;

        for (var y = 0; y < image.GetHeight(); y += SampleStride)
        {
            for (var x = 0; x < image.GetWidth(); x += SampleStride)
            {
                var current = Luminance(image.GetPixel(x, y));
                minLuminance = Math.Min(minLuminance, current);
                maxLuminance = Math.Max(maxLuminance, current);
                if (current <= 0.01)
                {
                    blackPixels++;
                }

                if (previous is not null)
                {
                    var delta = Math.Abs(current - Luminance(previous.GetPixel(x, y)));
                    deltaSum += delta;
                    maxDelta = Math.Max(maxDelta, delta);
                }

                sampledPixels++;
            }
        }

        return new FrameMetrics(
            previous is null ? 0 : deltaSum / sampledPixels,
            maxDelta,
            minLuminance,
            maxLuminance,
            blackPixels,
            sampledPixels);
    }

    private static double Luminance(Color color) =>
        Math.Clamp(color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722, 0, 1);

    /// <summary>
    /// Disables physics queries on the disposable capture clone without
    /// deleting a collider's visual children. Many benchmark meshes live below
    /// StaticBody3D nodes, so freeing CollisionObject3D would turn the temporal
    /// comparison into a capture of a different scene.
    /// </summary>
    private static RenderOnlySanitization SanitizePhysicsForRenderOnlyCapture(Node root)
    {
        var collisionShapeCountBefore = CountCollisionShapes(root);
        var collisionShapesRemoved = 0;
        DisablePhysicsQueriesAndRemoveShapes(root, ref collisionShapesRemoved);
        var collisionShapeCountAfter = CountCollisionShapes(root);
        var activePhysicsQueryOwnerCount = CountActivePhysicsQueryOwners(root);
        var visualMeshCount = CountVisualMeshes(root);
        return new RenderOnlySanitization(
            visualMeshCount,
            collisionShapeCountBefore,
            collisionShapesRemoved,
            collisionShapeCountAfter,
            activePhysicsQueryOwnerCount);
    }

    private static void DisablePhysicsQueriesAndRemoveShapes(Node root, ref int collisionShapesRemoved)
    {
        if (root is CollisionObject3D collisionObject)
        {
            // The clone never uses gameplay collisions. Keeping the body node
            // preserves any MeshInstance3D descendants, while zero layer/mask
            // removes it from physics-query participation.
            collisionObject.CollisionLayer = 0;
            collisionObject.CollisionMask = 0;
        }

        foreach (var child in root.GetChildren().ToArray())
        {
            if (child is CollisionShape3D)
            {
                child.Free();
                collisionShapesRemoved++;
                continue;
            }

            DisablePhysicsQueriesAndRemoveShapes(child, ref collisionShapesRemoved);
        }
    }

    private static int CountVisualMeshes(Node root) =>
        (root is MeshInstance3D ? 1 : 0) + root.GetChildren().Sum(CountVisualMeshes);

    private static int CountCollisionShapes(Node root) =>
        (root is CollisionShape3D ? 1 : 0) + root.GetChildren().Sum(CountCollisionShapes);

    private static int CountActivePhysicsQueryOwners(Node root)
    {
        var activeHere = root is CollisionObject3D collisionObject
            && (collisionObject.CollisionLayer != 0 || collisionObject.CollisionMask != 0)
            ? 1
            : 0;
        return activeHere + root.GetChildren().Sum(CountActivePhysicsQueryOwners);
    }

    private async Task FreeViewportAsync(SubViewport viewport)
    {
        viewport.Free();
        // Collision shapes and imported scene resources are queued for native
        // release. Let the parent SceneTree process one frame before creating
        // the next temporal sample, otherwise the evidence process can report
        // leaked Shape3D RIDs at shutdown.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record TemporalScene(
        string Name,
        string ScenePath,
        Vector3 Camera,
        Vector3 Target,
        string ExpectedImportedModule);

    private sealed record TemporalCapture(
        Image FinalImage,
        int FrameCount,
        double MeanLuminanceDelta,
        double MaxLuminanceDelta,
        double MinLuminance,
        double MaxLuminance,
        double BlackPixelFraction,
        float CameraVerticalPeak,
        float CameraHorizontalPeak,
        RenderOnlySanitization Sanitization);

    private sealed record RenderOnlySanitization(
        int VisualMeshCount,
        int CollisionShapeCountBefore,
        int CollisionShapesRemoved,
        int CollisionShapeCountAfter,
        int ActivePhysicsQueryOwnerCount);

    private sealed record FrameMetrics(
        double MeanDelta,
        double MaxDelta,
        double MinLuminance,
        double MaxLuminance,
        long BlackPixels,
        long SampledPixels);
}
