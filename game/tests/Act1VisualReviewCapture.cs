using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

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
    private const int ExpectedFrameCount = 6;
    private const int WarmupFrames = 24;
    private const string FrameArgumentPrefix = "--urman-act1-frame=";
    private const string OutputArgumentPrefix = "--urman-act1-output=";
    private const string ReceiptFileName = "act1_visual_review_receipt.json";

    private static readonly Vector2I CaptureSize = new(CaptureWidth, CaptureHeight);

    private static readonly IReadOnlyDictionary<string, FrameSpec> FrameSpecs =
        new Dictionary<string, FrameSpec>(StringComparer.Ordinal)
        {
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
                new Vector3(0.2f, 1.45f, -41.5f))
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

        // Freeze only the test-owned player process after applying the actual
        // production camera transform. The PNG is still rendered by this
        // Camera3D through the root viewport, not by a substitute camera.
        player.ApplyZoneSpawn(spec.PlayerPosition, 0f);
        player.SetPhysicsProcess(false);
        camera.Current = true;
        camera.LookAt(spec.Target, Vector3.Up);
        await WaitForFramesAsync(WarmupFrames);

        if (viewport.GetCamera3D() != camera)
        {
            throw new InvalidOperationException("The production camera is not the camera rendering the root viewport.");
        }

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
        var outputPath = Path.Combine(outputDirectory, spec.FileName);
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
                FrameId = spec.Id,
                Zone = spec.ZoneId,
                ActiveZoneId = connectedWorld.ActiveZoneId,
                SpawnPointId = spec.SpawnPointId,
                Camera = new CameraReceipt
                {
                    GlobalPosition = ScalarVector.From(camera.GlobalPosition),
                    Target = ScalarVector.From(spec.Target)
                },
                Output = spec.FileName,
                Width = image.GetWidth(),
                Height = image.GetHeight(),
                Sha256 = sha256
            });

        GD.Print(string.Join(
            ' ',
            "act1-visual-review-capture:",
            $"frame={spec.Id}",
            $"zone={connectedWorld.ActiveZoneId}",
            $"camera_global_position={FormatVector(camera.GlobalPosition)}",
            $"target={FormatVector(spec.Target)}",
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
        receipt.CaptureProcessCount = ExpectedFrameCount;
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
        Vector3 Target)
    {
        public string FileName => $"{Id}.png";
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
