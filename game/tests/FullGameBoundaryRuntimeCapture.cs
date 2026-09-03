using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Runtime-backed, presentation-only receipt for the authored Act 5 boundary.
/// Unlike the broad dressing capture, this harness instantiates full_game.tscn
/// so FullGameZone builds the real compiled interaction targets. It does not
/// dispatch story commands, mutate saves, or alter the packed scene.
/// </summary>
public partial class FullGameBoundaryRuntimeCapture : Node
{
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const int WarmupFrames = 10;
    private const string BoundaryZoneId = "fullgame_act5_boundary";
    private const string ChoiceInteraction = "urman.fullgame:interaction/act5-aidar-choice";
    private const string EpilogueInteraction = "urman.fullgame:interaction/act5-boundary-to-epilogue";

    private static readonly Vector3 StandardCamera = new(0f, 1.7f, 12.5f);
    private static readonly Vector3 StandardTarget = new(0f, 1.35f, -7.2f);
    private static readonly Vector3 MarkerCamera = new(0f, 1.7f, -1.8f);
    private static readonly Vector3 MarkerTarget = new(0f, 1.12f, -6.25f);

    private string _outputDirectory = string.Empty;

    public override async void _Ready()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            WriteFailureReceipt(exception);
            GD.PushError($"fullgame-boundary-runtime: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync()
    {
        _outputDirectory = ProjectSettings.GlobalizePath(
            "res://../docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture");
        PrepareOutputDirectory();

        var viewport = new SubViewport
        {
            Name = "FullGameBoundaryRuntimeViewport",
            Size = new Vector2I(CaptureWidth, CaptureHeight),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);

        try
        {
            var packed = ResourceLoader.Load<PackedScene>("res://scenes/full_game.tscn")
                ?? throw new InvalidOperationException("Full-game scene could not be loaded.");
            var main = packed.Instantiate<Main>()
                ?? throw new InvalidOperationException("Full-game scene did not instantiate Main.");
            main.InitialZoneId = BoundaryZoneId;
            main.InitialSpawnPointId = "entry";
            var bridge = main.GetNode<RuntimeBridge>("RuntimeBridge");
            bridge.CurrentZoneId = BoundaryZoneId;
            bridge.CurrentSpawnPointId = "entry";
            viewport.AddChild(main);

            await WaitForFramesAsync(WarmupFrames);
            var playerCamera = main.GetNodeOrNull<Camera3D>("Player/Head/Camera3D");
            if (playerCamera is not null)
            {
                playerCamera.Current = false;
            }

            var zone = main.GetNode<Node3D>("ZoneHost").GetChild(0) as FullGameZone
                ?? throw new InvalidOperationException("Runtime-backed capture did not load FullGameZone.");
            if (main.ActiveZoneScenePath != "res://scenes/zones/fullgame/act5_boundary.tscn")
            {
                throw new InvalidOperationException($"Unexpected boundary scene: {main.ActiveZoneScenePath}");
            }

            var dressing = zone.GetNodeOrNull<Node3D>("ProductionDressing")
                ?? throw new InvalidOperationException("Boundary production dressing is missing.");
            var kit = dressing.GetNodeOrNull<Node3D>("GeneratedModularKit")
                ?? throw new InvalidOperationException("Boundary GeneratedModularKit is missing.");
            var markerTargets = zone.GetChildren()
                .OfType<InteractionTarget>()
                .Where(target => target.InteractionId is ChoiceInteraction or EpilogueInteraction)
                .ToDictionary(target => target.InteractionId, StringComparer.Ordinal);
            if (markerTargets.Count != 2)
            {
                throw new InvalidOperationException(
                    $"Runtime-backed boundary expected both authored interaction targets; found {markerTargets.Count}.");
            }

            var gateMeshes = kit
                .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Visible && mesh.Name.ToString().StartsWith("GateA_", StringComparison.Ordinal))
                .ToArray();
            if (gateMeshes.Length != 8)
            {
                throw new InvalidOperationException($"Expected 8 visible GateA LOD meshes, found {gateMeshes.Length}.");
            }

            var clearance = gateMeshes
                .SelectMany(mesh => markerTargets.Values.Select(target =>
                {
                    var delta = mesh.GlobalPosition - target.GlobalPosition;
                    return new Vector2(delta.X, delta.Z).Length();
                }))
                .Min();
            var behindMarkers = gateMeshes.All(mesh => markerTargets.Values.All(target =>
                mesh.GlobalPosition.Z < target.GlobalPosition.Z - 1f));
            if (clearance < 1f || !behindMarkers)
            {
                throw new InvalidOperationException(
                    $"GateA marker clearance failed: min_horizontal={clearance.ToString("0.000", CultureInfo.InvariantCulture)}m, behind={behindMarkers}.");
            }

            var camera = new Camera3D
            {
                Name = "FullGameBoundaryRuntimeCamera",
                Fov = 75f,
                Current = true
            };
            viewport.AddChild(camera);

            var captures = new List<CaptureReceipt>(2)
            {
                await CaptureAsync(viewport, camera, "standard", StandardCamera, StandardTarget, gateMeshes, markerTargets.Values),
                await CaptureAsync(viewport, camera, "markers", MarkerCamera, MarkerTarget, gateMeshes, markerTargets.Values)
            };
            var markerCapture = captures.Single(capture => capture.Name == "markers");
            if (!markerCapture.ScreenSpace.MarkersInsideViewport || !markerCapture.ScreenSpace.GateDoesNotOverlapMarkers)
            {
                throw new InvalidOperationException(
                    $"Boundary marker camera failed screen-space visibility: inside={markerCapture.ScreenSpace.MarkersInsideViewport}, "
                    + $"gate_overlap={markerCapture.ScreenSpace.GateDoesNotOverlapMarkers == false}.");
            }

            var runtimeState = bridge.ActiveSceneId ?? string.Empty;
            var receipt = new
            {
                schema_version = 1,
                kind = "urman.godot_fullgame_boundary_runtime_capture",
                captured_at_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                status = "OPEN",
                technical_status = "PASS",
                acceptance = "OPEN",
                renderer_requirement = "Godot 4.7.1 .NET Forward+ with a real Metal or equivalent RenderingDevice",
                resolution = new { width = CaptureWidth, height = CaptureHeight },
                runtime_backed = new
                {
                    packed_scene = "res://scenes/full_game.tscn",
                    runtime_bridge_present = true,
                    compiled_scene_resolved = bridge.RequireScene("urman.fullgame:scene/act5-boundary").Id,
                    godot_zone_scene_path = main.ActiveZoneScenePath,
                    kernel_active_scene_before_story_dispatch = runtimeState,
                    story_commands_dispatched = false,
                    save_or_narrative_state_changed = false
                },
                gate = new
                {
                    visible_lod_meshes = gateMeshes.Length,
                    required_interaction_ids = new[] { ChoiceInteraction, EpilogueInteraction },
                    found_interaction_ids = markerTargets.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    minimum_horizontal_clearance_m = clearance,
                    behind_both_markers = behindMarkers,
                    policy = "GateA remains behind both runtime-authored Act 5 interaction markers by >=1m"
                },
                captures
            };
            WriteJson("fullgame_boundary_runtime_manifest.json", receipt);
            WriteText("README.md", BuildReadme(captures, clearance, behindMarkers, main.ActiveZoneScenePath, runtimeState));
            GD.Print($"fullgame-boundary-runtime: OPEN technical=PASS captures={captures.Count} markers=2 gate_clearance={clearance.ToString("0.000", CultureInfo.InvariantCulture)}m");
            await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(0);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(viewport))
            {
                viewport.Free();
            }
        }
    }

    private async Task<CaptureReceipt> CaptureAsync(
        SubViewport viewport,
        Camera3D camera,
        string name,
        Vector3 position,
        Vector3 target,
        IReadOnlyList<MeshInstance3D> gateMeshes,
        IEnumerable<InteractionTarget> markerTargets)
    {
        camera.Position = position;
        camera.LookAt(target, Vector3.Up);
        camera.Current = true;
        camera.ForceUpdateTransform();
        await WaitForFramesAsync(WarmupFrames);
        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty() || image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
        {
            throw new InvalidOperationException($"Boundary {name} capture returned an invalid image.");
        }

        var screenSpace = EvaluateScreenSpace(camera, new Vector2I(CaptureWidth, CaptureHeight), gateMeshes, markerTargets);

        var outputPath = System.IO.Path.Combine(_outputDirectory, $"godot_act5_boundary_runtime_{name}_1080p.png");
        if (image.SavePng(outputPath) != Error.Ok)
        {
            throw new InvalidOperationException($"Could not save {outputPath}.");
        }

        return new CaptureReceipt(
            name,
            ToRepositoryPath(outputPath),
            CaptureWidth,
            CaptureHeight,
            HashFile(outputPath),
            screenSpace);
    }

    private static ScreenSpaceReceipt EvaluateScreenSpace(
        Camera3D camera,
        Vector2I viewportSize,
        IReadOnlyList<MeshInstance3D> gateMeshes,
        IEnumerable<InteractionTarget> markerTargets)
    {
        var gateRects = gateMeshes
            .Select(mesh => ProjectMeshRect(camera, mesh))
            .Where(rect => rect.HasValue)
            .Select(rect => rect!.Value)
            .ToArray();
        var markerProjections = markerTargets
            .OrderBy(target => target.InteractionId, StringComparer.Ordinal)
            .Select(target =>
            {
                var point = target.GlobalPosition;
                var projected = camera.UnprojectPosition(point);
                var inside = !camera.IsPositionBehind(point)
                             && projected.X >= 16f
                             && projected.X <= viewportSize.X - 16f
                             && projected.Y >= 16f
                             && projected.Y <= viewportSize.Y - 16f;
                var overlapsGate = gateRects.Any(rect => ContainsExpanded(rect, projected, 8f));
                return new MarkerProjection(target.InteractionId, projected.X, projected.Y, inside, overlapsGate);
            })
            .ToArray();

        return new ScreenSpaceReceipt(
            markerProjections.Length == 2 && markerProjections.All(marker => marker.InsideViewport),
            markerProjections.Length == 2 && markerProjections.All(marker => !marker.OverlapsGate),
            markerProjections);
    }

    private static Rect2? ProjectMeshRect(Camera3D camera, MeshInstance3D mesh)
    {
        var aabb = mesh.GetAabb();
        var projected = new List<Vector2>(8);
        for (var index = 0; index < 8; index++)
        {
            var local = aabb.Position + new Vector3(
                (index & 1) == 0 ? 0f : aabb.Size.X,
                (index & 2) == 0 ? 0f : aabb.Size.Y,
                (index & 4) == 0 ? 0f : aabb.Size.Z);
            var world = mesh.GlobalTransform * local;
            if (!camera.IsPositionBehind(world))
            {
                projected.Add(camera.UnprojectPosition(world));
            }
        }

        if (projected.Count == 0)
        {
            return null;
        }

        var min = projected.Aggregate((left, right) => new Vector2(
            Mathf.Min(left.X, right.X),
            Mathf.Min(left.Y, right.Y)));
        var max = projected.Aggregate((left, right) => new Vector2(
            Mathf.Max(left.X, right.X),
            Mathf.Max(left.Y, right.Y)));
        return new Rect2(min, max - min);
    }

    private static bool ContainsExpanded(Rect2 rect, Vector2 point, float margin) =>
        point.X >= rect.Position.X - margin
        && point.X <= rect.End.X + margin
        && point.Y >= rect.Position.Y - margin
        && point.Y <= rect.End.Y + margin;

    private static async Task WaitForFramesAsync(int count)
    {
        var tree = Engine.GetMainLoop() as SceneTree
            ?? throw new InvalidOperationException("SceneTree is unavailable for capture warmup.");
        for (var index = 0; index < count; index++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private void PrepareOutputDirectory()
    {
        Directory.CreateDirectory(_outputDirectory);
        foreach (var file in Directory.EnumerateFiles(_outputDirectory))
        {
            var name = System.IO.Path.GetFileName(file);
            if (name.StartsWith("godot_act5_boundary_runtime_", StringComparison.Ordinal)
                || name is "fullgame_boundary_runtime_manifest.json" or "README.md")
            {
                File.Delete(file);
            }
        }
    }

    private void WriteFailureReceipt(Exception exception)
    {
        if (_outputDirectory.Length == 0)
        {
            _outputDirectory = ProjectSettings.GlobalizePath(
                "res://../docs/urman_knowledge_base/art/fullgame_boundary_runtime_capture");
        }

        Directory.CreateDirectory(_outputDirectory);
        WriteJson("fullgame_boundary_runtime_manifest.json", new
        {
            schema_version = 1,
            kind = "urman.godot_fullgame_boundary_runtime_capture",
            status = "OPEN",
            technical_status = "FAIL",
            acceptance = "OPEN",
            error = exception.Message,
            captures = Array.Empty<object>()
        });
        WriteText("README.md", $"# Act 5 runtime-backed boundary capture\n\nStatus: OPEN / technical FAIL.\n\nError: {exception.Message}\n");
    }

    private void WriteJson(string fileName, object value) => File.WriteAllText(
        System.IO.Path.Combine(_outputDirectory, fileName),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    private void WriteText(string fileName, string value) => File.WriteAllText(
        System.IO.Path.Combine(_outputDirectory, fileName), value);

    private static string ToRepositoryPath(string absolutePath) =>
        absolutePath.Replace(ProjectSettings.GlobalizePath("res://../"), string.Empty, StringComparison.Ordinal)
            .Replace(System.IO.Path.DirectorySeparatorChar, '/');

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string BuildReadme(
        IReadOnlyList<CaptureReceipt> captures,
        float clearance,
        bool behindMarkers,
        string godotZoneScenePath,
        string kernelActiveScene)
    {
        var clearanceText = clearance.ToString("0.000", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            "# Act 5 runtime-backed boundary capture",
            "",
            "Status: **OPEN / technical PASS**. This is a production-candidate receipt, not an art lock.",
            "",
            "The harness instantiates `res://scenes/full_game.tscn` with the real `RuntimeBridge`, loads the authored Act 5 boundary, and verifies that `FullGameZone` creates both compiled interaction targets:",
            "",
            $"- `{ChoiceInteraction}`",
            $"- `{EpilogueInteraction}`",
            "",
            $"GateA visible LOD meshes: **8** (four LOD0 + four LOD1). Minimum horizontal clearance to either runtime marker: **{clearanceText} m**. Behind both markers by >=1 m: **{behindMarkers}**.",
            $"Godot zone scene: `{godotZoneScenePath}`. Kernel entry scene before story dispatch: `{kernelActiveScene}` (the harness does not dispatch a story transition). No story commands, saves, materials, shaders or packed scenes were mutated.",
            "",
            "Captures:",
            ""
        };
        lines.AddRange(captures.Select(c => $"- `{c.Name}` — `{c.Output}`, {c.Width}x{c.Height}, SHA-256 `{c.Sha256}`, markers inside={c.ScreenSpace.MarkersInsideViewport}, gate overlap={c.ScreenSpace.Markers.Any(marker => marker.OverlapsGate)}"));
        lines.AddRange([
            "",
            "The dedicated `markers` camera keeps both runtime interaction markers inside the viewport with no GateA screen-space overlap. The `standard` camera is a distant composition check and may overlap the marker projection. Remaining gates: screen-space visibility during observed traversal, GateA family repetition, cultural/level-art review, release-host performance, accessibility, audio and full playthrough."
        ]);
        return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
    }

    private sealed record CaptureReceipt(
        string Name,
        string Output,
        int Width,
        int Height,
        string Sha256,
        ScreenSpaceReceipt ScreenSpace);

    private sealed record ScreenSpaceReceipt(
        bool MarkersInsideViewport,
        bool GateDoesNotOverlapMarkers,
        IReadOnlyList<MarkerProjection> Markers);

    private sealed record MarkerProjection(
        string InteractionId,
        float X,
        float Y,
        bool InsideViewport,
        bool OverlapsGate);
}
