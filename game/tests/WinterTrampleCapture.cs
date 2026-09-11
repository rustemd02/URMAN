using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only production-GPU evidence for real snow trample movement. The
/// player walks the same six-metre street segment twice; no footprint stamps
/// or player teleports are authored by this harness.
/// </summary>
public partial class WinterTrampleCapture : Node
{
    private const int ExpectedWidth = 1920;
    private const int ExpectedHeight = 1080;
    private const int PhysicsFps = 60;
    private const int VideoFps = 30;
    private const int SaveEveryPhysicsFrames = PhysicsFps / VideoFps;
    // Turn near the half-stride point so the reverse pass lands in the
    // previous soles instead of laying a second interleaved trail.
    private const float TrackLengthMeters = 5.85f;
    private const float TrackToleranceMeters = .25f;
    private const int MaxPassPhysicsFrames = 150;

    private string _directory = string.Empty;
    private Vector2I _captureSize;
    private int _videoFrameIndex;
    private int _observedStampCount;
    private int _nextExpectedFootSide = 1;
    private bool _moveHeld;
    private readonly List<double> _stampUpdateMs = new();
    private readonly List<double> _windowShiftMs = new();
    private Vector2? _observedWindowCentre;
    private readonly List<double> _stampRedrawMs = new();
    private readonly List<double> _stampMeshMs = new();

    public override async void _Ready()
    {
        try
        {
            await CaptureAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"winter-trample-capture failed: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            ReleaseMoveInput();
        }
    }

    private async Task CaptureAsync()
    {
        _directory = System.Environment.GetEnvironmentVariable("URMAN_TRAMPLE_SHOT_DIR") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_directory))
            throw new InvalidOperationException("URMAN_TRAMPLE_SHOT_DIR is not set.");
        var freshVariant = System.Environment.GetEnvironmentVariable("URMAN_TRAMPLE_FRESH") == "1";

        _directory = Path.GetFullPath(_directory);
        Directory.CreateDirectory(_directory);
        var snapshotsDirectory = Path.Combine(_directory, "snapshots");
        var videoDirectory = Path.Combine(_directory, "video_frames");
        Directory.CreateDirectory(snapshotsDirectory);
        Directory.CreateDirectory(videoDirectory);
        EnsureFresh(Path.Combine(snapshotsDirectory, "untrampled.png"));
        EnsureFresh(Path.Combine(snapshotsDirectory, "firstpass.png"));
        EnsureFresh(Path.Combine(snapshotsDirectory, "repeat.png"));
        EnsureFresh(Path.Combine(_directory, "winter_trample_receipt.json"));
        if (Directory.EnumerateFiles(videoDirectory, "*.png").Any())
            throw new IOException($"Refusing to reuse non-empty video frame directory: {videoDirectory}");

        if (RenderingServer.GetRenderingDevice() is null)
            throw new InvalidOperationException("Winter trample capture requires a real production GPU rendering device.");

        Engine.PhysicsTicksPerSecond = PhysicsFps;

        var viewport = GetTree().Root;
        var windowSize = DisplayServer.WindowGetSize();
        if (windowSize.X != ExpectedWidth || windowSize.Y != ExpectedHeight)
            throw new InvalidOperationException($"Expected a 1920x1080 window, got {windowSize.X}x{windowSize.Y}.");
        _captureSize = windowSize;

        var packedMain = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Could not load production res://scenes/main.tscn.");
        var main = packedMain.Instantiate<Main>()
            ?? throw new InvalidOperationException("Production main.tscn did not instantiate Main.");
        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        await WaitForFramesAsync(18);
        var rootWindow = GetTree().Root;
        rootWindow.ContentScaleSize = windowSize;
        rootWindow.Size = windowSize;
        await WaitForFramesAsync(2);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Production first-person player is missing.");
        var field = FindDescendants(main).OfType<SnowTrampleField>().SingleOrDefault()
            ?? throw new InvalidOperationException("Production SnowTrampleField is missing.");
        var startX = freshVariant ? 23f : -1.4f;
        var startZ = freshVariant ? -11f : 5f;
        var groundY = AgentBAct1HeightField.CollisionGround(startX, startZ);
        var startPose = new Vector3(startX, groundY + .05f, startZ);
        var road = AgentBAct1HeightField.RoadInfo(startX, startZ);
        if (freshVariant)
        {
            if (road.Distance <= road.HalfWidth + .25)
                throw new InvalidOperationException($"Fresh capture start is too close to the road: distance={road.Distance:F2}m half_width={road.HalfWidth:F2}m.");
        }
        else if (road.Distance > road.HalfWidth - .25)
        {
            throw new InvalidOperationException($"Capture start is outside the cleared street: distance={road.Distance:F2}m half_width={road.HalfWidth:F2}m.");
        }

        // Spawn/load resets presentation step tracking. Later displacement is real input.
        player.ApplyZoneSpawn(startPose, 0f);
        await WaitForGroundedAsync(player);
        await WaitForRenderedFrameAsync();

        var cameraPosition = freshVariant
            ? new Vector3(startX + 4.8f, groundY + 8.45f, startZ - .5f)
            : new Vector3(-6.2f, 8.5f, 4.5f);
        var cameraTarget = freshVariant
            ? new Vector3(startX, groundY - .05f, startZ - 3f)
            : new Vector3(-1.4f, 0f, 2f);
        var camera = new Camera3D
        {
            Name = "WinterTramplePresentationCamera",
            Fov = 55f,
            Current = true,
            Position = cameraPosition
        };
        AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);
        var fixedCameraTransform = camera.GlobalTransform;
        await WaitForRenderedFrameAsync();
        AssertRootCamera(viewport, camera);

        var actualStart = player.GlobalPosition;
        var initialStamps = ReadStampMetadata(field);
        if (initialStamps.Count != 0)
            throw new InvalidOperationException($"Setup produced SnowTrampleField stamps before walking: {initialStamps.Count}.");
        _observedStampCount = 0;
        _nextExpectedFootSide = 1;
        SaveFrame(viewport, Path.Combine(snapshotsDirectory, "untrampled.png"), camera, fixedCameraTransform);
        await SaveCloseAsync(viewport, camera, snapshotsDirectory, "untrampled_close", freshVariant, startX, startZ, groundY);

        var firstPass = await WalkPassAsync(
            player, field, viewport, camera, fixedCameraTransform, videoDirectory, 0f, new Vector2(0f, -1f));
        var firstStamps = ReadStampMetadata(field);
        var firstStampCount = firstStamps.Count;
        var firstFieldBounds = ReadFieldHeightBounds(field);
        AssertFieldContract(field, firstFieldBounds, freshVariant, "first pass");
        if (firstStampCount <= 0)
            throw new InvalidOperationException($"First physical pass produced no SnowTrampleField stamps; metadata={firstStampCount}.");
        if (firstStampCount != _observedStampCount)
            throw new InvalidOperationException($"First pass metadata observations ended at {_observedStampCount}, field reports {firstStampCount}.");
        AssertStampDirection(firstStamps, new Vector2(0f, -1f), "first pass");
        SaveFrame(viewport, Path.Combine(snapshotsDirectory, "firstpass.png"), camera, fixedCameraTransform);
        await SaveCloseAsync(viewport, camera, snapshotsDirectory, "firstpass_close", freshVariant, startX, startZ, groundY);

        // Rotate in place for the return pass; this changes yaw only and does
        // not relocate the body or stamp a path.
        player.ApplySmokeLook(0f, 180f);
        await WaitForGroundedAsync(player);
        await WaitForRenderedFrameAsync();
        var repeatPass = await WalkPassAsync(
            player, field, viewport, camera, fixedCameraTransform, videoDirectory, 180f, new Vector2(0f, 1f));
        var repeatStamps = ReadStampMetadata(field);
        var repeatStampCount = repeatStamps.Count;
        var repeatFieldBounds = ReadFieldHeightBounds(field);
        if (repeatStampCount <= firstStampCount)
            throw new InvalidOperationException($"Repeat physical pass did not grow stamps: first={firstStampCount}, repeat={repeatStampCount}.");
        if (repeatStampCount != _observedStampCount)
            throw new InvalidOperationException($"Repeat pass metadata observations ended at {_observedStampCount}, field reports {repeatStampCount}.");
        AssertStampDirection(repeatStamps, new Vector2(0f, 1f), "repeat pass");
        GD.Print("winter-trample-geometry: " + field.GetMeta("snowTrampleGeometry").AsString());
        SaveFrame(viewport, Path.Combine(snapshotsDirectory, "repeat.png"), camera, fixedCameraTransform);
        await SaveCloseAsync(viewport, camera, snapshotsDirectory, "repeat_close", freshVariant, startX, startZ, groundY);

        if (_videoFrameIndex is < 90 or > 180)
            throw new InvalidOperationException($"Expected 90-180 video frames, captured {_videoFrameIndex}.");

        WriteReceipt(
            viewport,
            player,
            camera,
            cameraTarget,
            actualStart,
            firstPass,
            repeatPass,
            firstStampCount,
            repeatStampCount,
            repeatStamps,
            freshVariant,
            firstFieldBounds,
            repeatFieldBounds);
        GD.Print(
            $"winter-trample-capture: PASS root_gpu=true resolution={ExpectedWidth}x{ExpectedHeight} "
            + $"video_frames={_videoFrameIndex} first_stamps={firstStampCount} repeat_stamps={repeatStampCount} output={_directory}");
        if (System.Environment.GetEnvironmentVariable("URMAN_CAPTURE_BENCHMARK") == "1")
            await Act1FullRouteCoreWorldCapture.CapturePerformanceAsync(this, main, player,
                FindDescendants(player).OfType<Camera3D>().First(), _directory);
        main.Free();
        await WaitForFramesAsync(2);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await WaitForFramesAsync(1);
        GetTree().Quit(0);
    }

    private async Task SaveCloseAsync(
        Viewport viewport,
        Camera3D camera,
        string directory,
        string name,
        bool freshVariant,
        float startX,
        float startZ,
        float groundY)
    {
        var previous = camera.GlobalTransform;
        camera.GlobalPosition = freshVariant
            ? new Vector3(startX + 1.7f, groundY + 1.85f, startZ - 1.5f)
            : new Vector3(-3.1f, 1.9f, 3.5f);
        camera.LookAt(freshVariant
            ? new Vector3(startX, groundY - .25f, startZ - 3f)
            : new Vector3(-1.4f, -.2f, 2f), Vector3.Up);
        await WaitForRenderedFrameAsync();
        SaveFrame(viewport, Path.Combine(directory, name + ".png"), camera, camera.GlobalTransform);
        camera.GlobalTransform = previous;
        await WaitForRenderedFrameAsync();
    }

    private async Task<PassReceipt> WalkPassAsync(
        FirstPersonController player,
        SnowTrampleField field,
        Viewport viewport,
        Camera3D camera,
        Transform3D fixedCameraTransform,
        string videoDirectory,
        float yawDegrees,
        Vector2 expectedDirection)
    {
        var start = player.GlobalPosition;
        var physicsFrames = 0;
        try
        {
            Input.ActionPress("move_forward");
            _moveHeld = true;
            while (HorizontalDistance(start, player.GlobalPosition) < TrackLengthMeters - .05f)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                physicsFrames++;
                if (physicsFrames > MaxPassPhysicsFrames)
                    throw new InvalidOperationException($"Player did not complete the six-metre pass after {physicsFrames} physics frames.");
                ObserveStampMetadata(field, expectedDirection, yawDegrees == 0f ? "first pass" : "repeat pass");

                if (physicsFrames % SaveEveryPhysicsFrames == 0)
                {
                    RenderingServer.ForceDraw(false);
                    SaveFrame(
                        viewport,
                        Path.Combine(videoDirectory, $"frame_{_videoFrameIndex++:D4}.png"),
                        camera,
                        fixedCameraTransform);
                }
            }
        }
        finally
        {
            ReleaseMoveInput();
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        ObserveStampMetadata(field, expectedDirection, yawDegrees == 0f ? "first pass" : "repeat pass");
        if (!player.IsOnFloor())
            throw new InvalidOperationException($"Player was not grounded after {yawDegrees:F0}-degree physical pass.");
        await WaitForRenderedFrameAsync();
        var end = player.GlobalPosition;
        var distance = HorizontalDistance(start, end);
        if (distance < TrackLengthMeters - TrackToleranceMeters || distance > TrackLengthMeters + TrackToleranceMeters)
            throw new InvalidOperationException($"Physical pass length is {distance:F3}m, expected about {TrackLengthMeters:F1}m.");
        if (Mathf.Abs(end.X - start.X) > .15f)
            throw new InvalidOperationException($"Physical pass drifted sideways by {Mathf.Abs(end.X - start.X):F3}m.");

        return new PassReceipt(VectorReceipt.From(start), VectorReceipt.From(end), distance, physicsFrames, yawDegrees);
    }

    private async Task WaitForGroundedAsync(FirstPersonController player)
    {
        var stable = 0;
        for (var index = 0; index < 60; index++)
        {
            await ToSignal(player.GetTree(), SceneTree.SignalName.PhysicsFrame);
            stable = player.IsOnFloor() && Mathf.Abs(player.Velocity.Y) < .01f ? stable + 1 : 0;
            if (stable >= 3) return;
        }

        throw new InvalidOperationException("Production player did not settle onto the real floor.");
    }

    private void SaveFrame(Viewport viewport, string path, Camera3D camera, Transform3D fixedCameraTransform)
    {
        AssertRootCamera(viewport, camera);
        if (camera.GlobalTransform != fixedCameraTransform || !Mathf.IsEqualApprox(camera.Fov, 55f))
            throw new InvalidOperationException("Presentation camera changed between matched captures.");

        using var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
            throw new InvalidOperationException($"Root viewport returned an empty image: {path}");
        if (image.GetWidth() != ExpectedWidth || image.GetHeight() != ExpectedHeight)
            throw new InvalidOperationException($"Capture rendered {image.GetWidth()}x{image.GetHeight()}, expected {ExpectedWidth}x{ExpectedHeight}: {path}");

        image.Convert(Image.Format.Rgba8);
        var bytes = image.GetData().ToArray();
        if (bytes.Length == 0 || bytes.All(value => value == bytes[0]))
            throw new InvalidOperationException($"Capture has no pixel variance: {path}");
        EnsureFresh(path);
        if (image.SavePng(path) != Error.Ok)
            throw new IOException($"Could not save capture: {path}");
    }

    private void WriteReceipt(
        Viewport viewport,
        FirstPersonController player,
        Camera3D camera,
        Vector3 cameraTarget,
        Vector3 actualStart,
        PassReceipt firstPass,
        PassReceipt repeatPass,
        int firstStampCount,
        int repeatStampCount,
        StampMetadata repeatStamps,
        bool freshVariant,
        HeightBounds firstFieldBounds,
        HeightBounds repeatFieldBounds)
    {
        var path = Path.Combine(_directory, "winter_trample_receipt.json");
        EnsureFresh(path);
        var receipt = new
        {
            schema_version = 1,
            kind = "urman.winter_trample_physical_capture",
            variant = freshVariant ? "fresh_snow" : "arrival_road",
            production_scene = "res://scenes/main.tscn",
            root_viewport = true,
            gpu_renderer = RenderingServer.GetVideoAdapterName(),
            resolution = new { width = _captureSize.X, height = _captureSize.Y },
            preset = player.GraphicsPreset,
            camera = new
            {
                name = camera.Name.ToString(),
                position = VectorReceipt.From(camera.GlobalPosition),
                target = VectorReceipt.From(cameraTarget),
                fov = camera.Fov,
                projection = "perspective",
                child_of_player = false
            },
            physics_fps = PhysicsFps,
            video_fps = VideoFps,
            save_every_physics_frames = SaveEveryPhysicsFrames,
            video_frame_count = _videoFrameIndex,
            player_actual_start = VectorReceipt.From(actualStart),
            first_pass = firstPass,
            repeat_pass = repeatPass,
            snow_trample_stamp_count = new
            {
                first_pass = firstStampCount,
                repeat_pass = repeatStampCount
            },
            field_height_bounds = new
            {
                resolution = 1024,
                first_pass = firstFieldBounds,
                repeat_pass = repeatFieldBounds
            },
            stamp_update_cpu_ms = new { samples = _stampUpdateMs.Count, mean = _stampUpdateMs.Average(), max = _stampUpdateMs.Max(), redraw_mean = _stampRedrawMs.Average(), mesh_mean = _stampMeshMs.Average() },
            window_shift_cpu_ms = new { samples = _windowShiftMs.Count, max = _windowShiftMs.Count > 0 ? _windowShiftMs.Max() : 0 },
            snow_trample_last = new
            {
                foot_side = repeatStamps.FootSide,
                position_x = repeatStamps.Position.X,
                position_z = repeatStamps.Position.Y,
                rotation = repeatStamps.Rotation
            },
            snapshots_directory = Path.GetFullPath(Path.Combine(_directory, "snapshots")),
            video_frames_directory = Path.GetFullPath(Path.Combine(_directory, "video_frames"))
        };
        File.WriteAllText(path, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine);
    }

    private static StampMetadata ReadStampMetadata(SnowTrampleField field)
    {
        var count = field.HasMeta("snowTrampleStampCount")
            ? field.GetMeta("snowTrampleStampCount").AsInt32()
            : 0;
        if (count == 0)
            return new StampMetadata(0, 0, Vector2.Zero, 0f);

        if (!field.HasMeta("snowTrampleLastFootSide")
            || !field.HasMeta("snowTrampleLastPosition")
            || !field.HasMeta("snowTrampleLastRotation"))
            throw new InvalidOperationException("SnowTrampleField stamp count is positive but last-stamp metadata is incomplete.");

        return new StampMetadata(
            count,
            field.GetMeta("snowTrampleLastFootSide").AsInt32(),
            field.GetMeta("snowTrampleLastPosition").AsVector2(),
            field.GetMeta("snowTrampleLastRotation").AsSingle());
    }

    private void ObserveStampMetadata(SnowTrampleField field, Vector2 expectedDirection, string passName)
    {
        var metadata = ReadStampMetadata(field);
        if (metadata.Count == _observedStampCount)
            return;
        if (metadata.Count != _observedStampCount + 1)
            throw new InvalidOperationException(
                $"{passName} skipped SnowTrampleField metadata stamps: observed={_observedStampCount}, current={metadata.Count}.");
        if (metadata.FootSide != _nextExpectedFootSide)
            throw new InvalidOperationException(
                $"{passName} foot side did not alternate: expected={_nextExpectedFootSide}, actual={metadata.FootSide}, stamp={metadata.Count}.");
        AssertStampDirection(metadata, expectedDirection, passName);
        _stampUpdateMs.Add(field.GetMeta("snowTrampleUpdateMs").AsDouble());
        var centre = field.GetMeta("snowTrampleWindowCentre").AsVector2();
        if (_observedWindowCentre is Vector2 previousCentre && centre != previousCentre)
            _windowShiftMs.Add(field.GetMeta("snowTrampleUpdateMs").AsDouble());
        _observedWindowCentre = centre;
        _stampRedrawMs.Add(field.GetMeta("snowTrampleRedrawMs").AsDouble());
        _stampMeshMs.Add(field.GetMeta("snowTrampleMeshMs").AsDouble());
        if (field.GetMeta("snowTrampleMinHeight").AsSingle() < -.05f || field.GetMeta("snowTrampleMaxHeight").AsSingle() > .02f)
            throw new InvalidOperationException("Footprint height bounds failed.");
        _observedStampCount = metadata.Count;
        _nextExpectedFootSide = -_nextExpectedFootSide;
    }

    private static HeightBounds ReadFieldHeightBounds(SnowTrampleField field) =>
        new(
            field.GetMeta("snowTrampleMinHeight").AsSingle(),
            field.GetMeta("snowTrampleMaxHeight").AsSingle());

    private static void AssertFieldContract(SnowTrampleField field, HeightBounds bounds, bool freshVariant, string passName)
    {
        var minimumOutOfRange = freshVariant
            ? bounds.Min < -.05f || bounds.Min > -.02f
            : bounds.Min < -.015f || bounds.Min > -.003f;
        if (field.GetMeta("snowTrampleResolution").AsInt32() != 1024
            || minimumOutOfRange
            || bounds.Max is < .005f or > .02f)
        {
            throw new InvalidOperationException(
                $"{passName} footprint contract failed for {(freshVariant ? "fresh" : "road")} variant: "
                + $"resolution={field.GetMeta("snowTrampleResolution").AsInt32()} min={bounds.Min:F4} max={bounds.Max:F4}.");
        }
    }

    private static void AssertStampDirection(StampMetadata metadata, Vector2 expectedDirection, string passName)
    {
        if (metadata.FootSide is not (-1 or 1))
            throw new InvalidOperationException($"{passName} reported invalid foot side {metadata.FootSide}.");
        if (!float.IsFinite(metadata.Position.X) || !float.IsFinite(metadata.Position.Y) || !float.IsFinite(metadata.Rotation))
            throw new InvalidOperationException($"{passName} reported non-finite SnowTrampleField metadata.");

        var direction = new Vector2(Mathf.Cos(metadata.Rotation), Mathf.Sin(metadata.Rotation));
        if (direction.Dot(expectedDirection) < .8f)
            throw new InvalidOperationException(
                $"{passName} stamp direction {metadata.Rotation:F3} did not follow expected XZ direction {expectedDirection}.");
    }

    private static void AssertRootCamera(Viewport viewport, Camera3D camera)
    {
        if (viewport.GetCamera3D() != camera)
            throw new InvalidOperationException("The fixed presentation camera is not rendering the production root viewport.");
    }

    private async Task WaitForRenderedFrameAsync()
    {
        await WaitForFramesAsync(2);
        RenderingServer.ForceDraw(false);
    }

    private async Task WaitForFramesAsync(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void ReleaseMoveInput()
    {
        if (_moveHeld)
        {
            Input.ActionRelease("move_forward");
            _moveHeld = false;
        }
        else
        {
            Input.ActionRelease("move_forward");
        }
    }

    private static void EnsureFresh(string path)
    {
        if (File.Exists(path))
            throw new IOException($"Refusing to overwrite existing capture: {path}");
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();

    private static IEnumerable<Node> FindDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in FindDescendants(child))
                yield return nested;
        }
    }

    private sealed record PassReceipt(
        VectorReceipt start_position,
        VectorReceipt end_position,
        float distance_meters,
        int physics_frames,
        float yaw_degrees);

    private sealed record StampMetadata(int Count, int FootSide, Vector2 Position, float Rotation);

    private sealed record HeightBounds(float Min, float Max);

    private sealed record VectorReceipt(float X, float Y, float Z)
    {
        public static VectorReceipt From(Vector3 value) => new(value.X, value.Y, value.Z);
    }
}
