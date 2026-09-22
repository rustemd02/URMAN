using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only root-viewport evidence harness for the connected Act I greybox.
/// The shell wrapper launches this scene once per frame specification. Each
/// process owns one fresh Main scene and writes one 1280x720 PNG, so no
/// multi-frame SubViewport readback is involved.
/// </summary>
public partial class Act1VisualReviewCapture : Node
{
    private const int CaptureWidth = 1280;
    private const int CaptureHeight = 720;
    private const int WarmupFrames = 24;
    private const string FrameArgumentPrefix = "--urman-act1-frame=";
    private const string OutputArgumentPrefix = "--urman-act1-output=";
    // Optional animation-phase hold: extra frames waited after the production
    // camera is set, so the same subject can be captured at different moments.
    private const string HoldFramesArgumentPrefix = "--urman-act1-hold-frames=";
    private const string GraphicsArgumentPrefix = "--urman-act1-graphics=";
    private const string ReceiptFileName = "act1_visual_review_receipt.json";

    private static readonly Vector2I CaptureSize = new(CaptureWidth, CaptureHeight);

    private static readonly IReadOnlyDictionary<string, FrameSpec> FrameSpecs =
        new Dictionary<string, FrameSpec>(StringComparer.Ordinal)
        {
            // Material review uses the existing production-camera capture,
            // without changing the six-frame forest acceptance contract.
            ["house_table_materials"] = new(
                "house_table_materials", "house_old_pc", "entry",
                new Vector3(.1f, .05f, -1.40f), new Vector3(0, .87f, -2.30f),
                "house_interior", "babay-abi-house"),
            ["house_plaster_materials"] = new(
                "house_plaster_materials", "house_old_pc", "entry",
                new Vector3(-1.90f, .05f, -1.15f), new Vector3(-4f, 1.10f, -1.60f),
                "house_interior", "babay-abi-house"),
            ["snow_arrival_ground"] = new(
                "snow_arrival_ground", "village_day", "arrival",
                new Vector3(0f, AgentBAct1HeightField.CollisionGround(0f, 4f) + .05f, 4f),
                new Vector3(1.5f, AgentBAct1HeightField.CollisionGround(1.5f, 1f), 1f)),
            ["birch_rear_house"] = new(
                "birch_rear_house", "village_day", "from_house",
                new Vector3(-31.6f, AgentBAct1HeightField.CollisionGround(-31.6f, -10.5f) + .05f, -10.5f),
                new Vector3(-33.82144f, AgentBAct1HeightField.CollisionGround(-33.82144f, -12.23175f) + 1.3f, -12.23175f)),
            ["painted_arrival_west"] = new(
                "painted_arrival_west", "village_day", "arrival",
                new Vector3(0f, AgentBAct1HeightField.CollisionGround(0f, 4f) + .05f, 4f),
                new Vector3(-7.2f, 2.2f, 3.2f)),
            ["painted_street_east"] = new(
                "painted_street_east", "village_day", "from_house",
                new Vector3(1f, AgentBAct1HeightField.CollisionGround(1f, -9f) + .05f, -9f),
                new Vector3(9.8f, 1.8f, -10.5f)),
            ["kara_forest_forward"] = new(
                "kara_forest_forward",
                "kara_urman_night",
                "village_path",
                new Vector3(0f, 0.05f, -105.8f),
                new Vector3(0f, 1.45f, -119.5f)),
            ["kara_forest_back"] = new(
                "kara_forest_back",
                "kara_urman_night",
                "village_path",
                new Vector3(0.8f, 0.05f, -107.2f),
                new Vector3(0.3f, 1.5f, -97.5f)),
            ["kara_forest_left"] = new(
                "kara_forest_left",
                "kara_urman_night",
                "village_path",
                new Vector3(-2.15f, 0.05f, -108.6f),
                new Vector3(-10.5f, 1.7f, -116.5f)),
            ["kara_forest_right"] = new(
                "kara_forest_right",
                "kara_urman_night",
                "village_path",
                new Vector3(2.05f, 0.05f, -109.4f),
                new Vector3(10.5f, 1.7f, -118.5f)),
            ["zirat_forward"] = new(
                "zirat_forward",
                "zirat_road",
                "village_side",
                new Vector3(-0.9f, 0.05f, -54.1f),
                new Vector3(0f, 1.45f, -73f)),
            ["zirat_back"] = new(
                "zirat_back",
                "zirat_road",
                "village_side",
                new Vector3(1.15f, 0.05f, -57.3f),
                new Vector3(0.2f, 1.45f, -41.5f)),

            // Cast close-ups for the M6 in-motion review. The camera anchors match
            // the core-world capture frames so the same subject can be compared
            // between the still set and these phase samples.
            ["naila_close"] = new(
                "naila_close",
                "fap_clinic",
                "waiting_room",
                new Vector3(29.8f, 0.05f, -28.6f),
                new Vector3(29.8f, 1.45f, -30f), "fap_interior"),
            ["mansur_close"] = new(
                "mansur_close",
                "house_old_pc",
                "entry",
                new Vector3(-24.7f, 0.05f, -0.2f),
                new Vector3(-25.2f, 1.48f, -1.45f), "house_interior"),
            ["gulsina_close"] = new(
                "gulsina_close",
                "house_old_pc",
                "entry",
                new Vector3(-32.0f, 0.05f, -1.15f),
                new Vector3(-31.2f, 1.10f, -2.8f), "house_interior"),
            ["alsu_close"] = new(
                "alsu_close",
                "village_day",
                "arrival",
                new Vector3(0.1f, AgentBAct1HeightField.CollisionGround(0.1f, 4.1f) + .05f, 4.1f),
                new Vector3(0.85f, 1.35f, 2.05f), "main_street"),
            ["rinat_close"] = new(
                "rinat_close",
                "village_day",
                "arrival",
                new Vector3(-0.8f, AgentBAct1HeightField.CollisionGround(-0.8f, -1.5f) + .05f, -1.5f),
                new Vector3(-1.5f, 1.4f, -3.8f), "main_street"),
            ["timur_close"] = new(
                "timur_close",
                "village_day",
                "from_house",
                new Vector3(-1.4f, AgentBAct1HeightField.CollisionGround(-1.4f, -17.6f) + .05f, -17.6f),
                new Vector3(-3.8f, 1.4f, -19f), "connective_street_return")
        };

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        WriteIndented = true
    };

    public override async void _Ready()
    {
        try
        {
            await CaptureAsync();
        }
        catch (Exception exception)
        {
            Fail($"Act I visual review capture failed: {exception}");
        }
    }

    private async Task CaptureAsync()
    {
        var frameId = RequireArgument(FrameArgumentPrefix);
        if (!FrameSpecs.TryGetValue(frameId, out var spec))
        {
            throw new InvalidOperationException($"Unknown Act I visual review frame '{frameId}'.");
        }

        var holdFrames = 0;
        string? requestedGraphics = null;
        foreach (var argument in OS.GetCmdlineArgs())
        {
            if (argument.StartsWith(GraphicsArgumentPrefix, StringComparison.Ordinal))
            {
                if (requestedGraphics is not null)
                    throw new InvalidOperationException("Graphics preset was specified more than once.");
                requestedGraphics = argument[GraphicsArgumentPrefix.Length..];
                if (requestedGraphics is not ("low" or "medium" or "high"))
                    throw new InvalidOperationException($"Invalid graphics preset: '{requestedGraphics}'.");
            }
            if (!argument.StartsWith(HoldFramesArgumentPrefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(argument[HoldFramesArgumentPrefix.Length..], out holdFrames)
                || holdFrames < 0 || holdFrames > 600)
            {
                throw new InvalidOperationException($"Invalid animation-phase hold: '{argument}'.");
            }
        }

        var outputDirectory = Path.GetFullPath(RequireArgument(OutputArgumentPrefix));
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Capture output directory does not exist: {outputDirectory}");
        }

        var viewport = GetViewport();
        if (RenderingServer.GetRenderingDevice() is null)
        {
            throw new InvalidOperationException(
                "Act I visual review requires a real Godot 3D rendering device; headless readback is not evidence.");
        }

        var packedMain = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Could not load the current res://scenes/main.tscn.");
        var main = packedMain.Instantiate<Main>()
            ?? throw new InvalidOperationException("The current main scene did not instantiate Main.");
        main.InitialZoneId = spec.ZoneId;
        main.InitialSpawnPointId = spec.SpawnPointId;
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        await WaitForFramesAsync(WarmupFrames);

        var connectedWorld = main.ConnectedWorld
            ?? throw new InvalidOperationException("Main did not create Act1ConnectedWorld.");
        if (!connectedWorld.IsBuilt)
        {
            throw new InvalidOperationException("Act1ConnectedWorld was not built before capture.");
        }

        if (!string.Equals(connectedWorld.ActiveZoneId, spec.ZoneId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Connected world active zone is '{connectedWorld.ActiveZoneId}', expected '{spec.ZoneId}'.");
        }

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
            ?? throw new InvalidOperationException("Main did not expose RuntimeBridge.");
        if (!string.Equals(bridge.CurrentZoneId, spec.ZoneId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"RuntimeBridge active zone is '{bridge.CurrentZoneId}', expected '{spec.ZoneId}'.");
        }

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Main did not expose the production first-person player.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        // Exercise the same application/persistence path as Settings. This
        // scene must run under protected_run so preferences are restored.
        if (requestedGraphics is not null)
            player.ApplySettings(player.CaptureSettings() with { GraphicsPreset = requestedGraphics });
        var space = spec.LocalSpace is null ? null : connectedWorld.GetNode<Node3D>(spec.LocalSpace);
        var position = space is null ? spec.PlayerPosition : space.ToGlobal(spec.PlayerPosition);
        var target = space is null ? spec.Target : space.ToGlobal(spec.Target);

        // Let the production spawn resolve its initially crouched stance before
        // freezing. Ignore OS mouse warps while the capture window gains focus;
        // otherwise they can rotate Head beneath a separately aimed camera.
        player.SetProcessUnhandledInput(false);
        player.SetModalOpen(true);
        player.ApplyZoneSpawn(position, 0f);
        for (var frame = 0; frame < 2; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.SetPhysicsProcess(false);
        camera.Current = true;
        camera.Rotation = Vector3.Zero;
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-(target.X - player.GlobalPosition.X), -(target.Z - player.GlobalPosition.Z)));
        player.ApplySmokeLook(0, yaw);
        var delta = target - camera.GlobalPosition;
        player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())), yaw);
        await WaitForFramesAsync(WarmupFrames);
        if (holdFrames > 0)
        {
            await WaitForFramesAsync(holdFrames);
        }

        if (viewport.GetCamera3D() != camera)
        {
            throw new InvalidOperationException("The production camera is not the camera rendering the root viewport.");
        }

        var lookAlignment = (-camera.GlobalBasis.Z).Normalized().Dot((target - camera.GlobalPosition).Normalized());
        if (lookAlignment < .999f || Math.Abs(camera.GlobalBasis.X.Normalized().Y) > .001f)
            throw new InvalidOperationException($"Capture camera drifted or rolled: alignment={lookAlignment}, rightY={camera.GlobalBasis.X.Y}.");

        var image = viewport.GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            throw new InvalidOperationException("The root viewport returned an empty image.");
        }

        if (image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
        {
            throw new InvalidOperationException(
                $"The root viewport readback is {image.GetWidth()}x{image.GetHeight()}, expected {CaptureWidth}x{CaptureHeight}.");
        }

        image.Convert(Image.Format.Rgba8);
        var phaseId = holdFrames > 0 ? $"{spec.Id}_h{holdFrames}" : spec.Id;
        if (requestedGraphics is not null) phaseId += "_" + requestedGraphics;
        var outputName = phaseId + ".png";
        var outputPath = Path.Combine(outputDirectory, outputName);
        if (File.Exists(outputPath))
        {
            throw new IOException($"Refusing to overwrite an existing capture: {outputPath}");
        }

        var saveError = image.SavePng(outputPath);
        if (saveError != Error.Ok)
        {
            throw new IOException($"Could not save {outputPath}: {saveError}.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputPath))).ToLowerInvariant();
        WriteReceipt(
            outputDirectory,
            new FrameReceipt
            {
                FrameId = phaseId,
                Zone = spec.Label,
                ActiveZoneId = connectedWorld.ActiveZoneId,
                SpawnPointId = spec.SpawnPointId,
                GraphicsPreset = player.GraphicsPreset,
                Scaling3DScale = viewport.Scaling3DScale,
                Crouching = player.IsCrouching,
                Camera = new CameraReceipt
                {
                    GlobalPosition = ScalarVector.From(camera.GlobalPosition),
                    Target = ScalarVector.From(target),
                    Forward = ScalarVector.From(-camera.GlobalBasis.Z),
                    Up = ScalarVector.From(camera.GlobalBasis.Y)
                },
                Output = outputName,
                Width = image.GetWidth(),
                Height = image.GetHeight(),
                Sha256 = sha256
            });

        GD.Print(string.Join(
            ' ',
            "act1-visual-review-capture:",
            $"frame={phaseId}",
            $"zone={connectedWorld.ActiveZoneId}",
            $"camera_global_position={FormatVector(camera.GlobalPosition)}",
            $"target={FormatVector(target)}",
            $"graphics={player.GraphicsPreset}",
            $"output={outputPath}",
            $"sha256={sha256}"));

        main.ProcessMode = Node.ProcessModeEnum.Disabled;
        foreach (var shape in FindDescendants<CollisionShape3D>(main).ToArray())
        {
            shape.Free();
        }
        main.QueueFree();
        await WaitForFramesAsync(2);
        GetTree().Quit(0);
    }

    private static IEnumerable<T> FindDescendants<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static string RequireArgument(string prefix)
    {
        var argument = OS.GetCmdlineArgs()
            .FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null || argument.Length == prefix.Length)
        {
            throw new InvalidOperationException($"Missing required command-line argument '{prefix}<value>'.");
        }

        return argument[prefix.Length..];
    }

    private async Task WaitForFramesAsync(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static string FormatVector(Vector3 value) =>
        $"({value.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)};{value.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)};{value.Z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)})";

    private static void WriteReceipt(string outputDirectory, FrameReceipt frame)
    {
        var receiptPath = Path.Combine(outputDirectory, ReceiptFileName);
        var receipt = File.Exists(receiptPath)
            ? JsonSerializer.Deserialize<Receipt>(File.ReadAllText(receiptPath), ReceiptJsonOptions)
                ?? throw new InvalidDataException($"Could not deserialize {receiptPath}.")
            : new Receipt();

        if (receipt.Frames.Any(existing => string.Equals(existing.FrameId, frame.FrameId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"Receipt already contains frame '{frame.FrameId}'.");
        }

        receipt.Frames.Add(frame);
        receipt.Frames = receipt.Frames.OrderBy(existing => existing.FrameId, StringComparer.Ordinal).ToList();
        receipt.FrameCount = receipt.Frames.Count;
        // This harness writes exactly one distinct frame per process; partial
        // and material-only runs must not claim the full six-frame campaign.
        receipt.CaptureProcessCount = receipt.Frames.Count;
        receipt.Viewport = new ViewportReceipt { Width = CaptureWidth, Height = CaptureHeight };
        receipt.Kind = "urman.godot_act1_visual_review_capture";
        receipt.SchemaVersion = 1;
        receipt.ProductionScene = "res://scenes/main.tscn";
        receipt.RootViewport = true;
        receipt.SubViewportUsed = false;
        receipt.RendererExpectation = "Godot 4.7.1 .NET Forward+ / real 3D rendering device";
        receipt.CapturedAtUtc = DateTimeOffset.UtcNow.ToString("O");

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"urman-act1-visual-review-receipt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(receipt, ReceiptJsonOptions) + System.Environment.NewLine);
            File.Move(temporaryPath, receiptPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record FrameSpec(
        string Id,
        string ZoneId,
        string SpawnPointId,
        Vector3 PlayerPosition,
        Vector3 Target,
        // Interiors are entered through their logical Main zone (fap_clinic,
        // house_old_pc) while the connected world reports the visual zone label
        // (fap_interior, house_interior). Exteriors use the same value for both.
        string? VisualZone = null,
        string? LocalSpace = null)
    {
        // Receipt label: the authored visual zone, which differs from the logical
        // zone for interiors (fap_interior vs fap_clinic).
        public string Label => VisualZone ?? ZoneId;
    }

    private sealed class Receipt
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("captured_at_utc")]
        public string CapturedAtUtc { get; set; } = string.Empty;

        [JsonPropertyName("renderer_expectation")]
        public string RendererExpectation { get; set; } = string.Empty;

        [JsonPropertyName("production_scene")]
        public string ProductionScene { get; set; } = string.Empty;

        [JsonPropertyName("root_viewport")]
        public bool RootViewport { get; set; }

        [JsonPropertyName("subviewport_used")]
        public bool SubViewportUsed { get; set; }

        [JsonPropertyName("capture_process_count")]
        public int CaptureProcessCount { get; set; }

        [JsonPropertyName("frame_count")]
        public int FrameCount { get; set; }

        [JsonPropertyName("viewport")]
        public ViewportReceipt Viewport { get; set; } = new();

        [JsonPropertyName("frames")]
        public List<FrameReceipt> Frames { get; set; } = [];
    }

    private sealed class ViewportReceipt
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    private sealed class FrameReceipt
    {
        [JsonPropertyName("frame_id")]
        public string FrameId { get; set; } = string.Empty;

        [JsonPropertyName("zone")]
        public string Zone { get; set; } = string.Empty;

        [JsonPropertyName("active_zone_id")]
        public string ActiveZoneId { get; set; } = string.Empty;

        [JsonPropertyName("spawn_point_id")]
        public string SpawnPointId { get; set; } = string.Empty;

        [JsonPropertyName("graphics_preset")]
        public string GraphicsPreset { get; set; } = string.Empty;

        [JsonPropertyName("scaling_3d_scale")]
        public double Scaling3DScale { get; set; }

        [JsonPropertyName("crouching")]
        public bool Crouching { get; set; }

        [JsonPropertyName("camera")]
        public CameraReceipt Camera { get; set; } = new();

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }

    private sealed class CameraReceipt
    {
        [JsonPropertyName("forward")]
        public ScalarVector Forward { get; set; } = new();

        [JsonPropertyName("up")]
        public ScalarVector Up { get; set; } = new();

        [JsonPropertyName("global_position")]
        public ScalarVector GlobalPosition { get; set; } = new();

        [JsonPropertyName("target")]
        public ScalarVector Target { get; set; } = new();
    }

    private sealed class ScalarVector
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }

        public static ScalarVector From(Vector3 value) => new()
        {
            X = value.X,
            Y = value.Y,
            Z = value.Z
        };
    }
}
