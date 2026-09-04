using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// CAPTURE-004: supplemental directional evidence beyond the 44-frame core
/// contract — for each of the eight Act I visual zones this captures up and
/// down pitched views plus a high lateral from the production player camera
/// through the root viewport. No SubViewport, no gameplay state mutation.
/// </summary>
public partial class Act1SupplementalDirectionalCapture : Node
{
    private const int CaptureWidth = 1280;
    private const int CaptureHeight = 720;
    private const string OutputArgumentPrefix = "--urman-act1-supplemental-output=";
    private const string ReceiptFileName = "act1_supplemental_directional_receipt.json";

    private static readonly (string Zone, string Spawn, string Prefix, float PitchDegrees, string Kind)[]
        Frames =
        [
            ("village_day", "arrival", "arrival_up", 55f, "pitch-up"),
            ("village_day", "arrival", "arrival_down", -55f, "pitch-down"),
            ("village_day", "from_house", "main_street_high", 30f, "high-lateral"),
            ("house_old_pc", "entry", "house_interior_up", 55f, "pitch-up"),
            ("fap_clinic", "waiting_room", "fap_interior_up", 55f, "pitch-up"),
            ("zirat_road", "village_side", "zirat_up", 55f, "pitch-up"),
            ("zirat_road", "village_side", "zirat_ground", -50f, "ground-contact"),
            ("kara_urman_night", "village_path", "kara_up", 55f, "pitch-up"),
            ("kara_urman_night", "village_path", "kara_crowns", 62f, "crown-line"),
            ("kara_urman_night", "village_path", "kara_ground", -50f, "ground-contact")
        ];

    public override async void _Ready()
    {
        try
        {
            await CaptureAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Act I supplemental capture failed: {exception}", exception);
        }
    }

    private async Task CaptureAsync()
    {
        var outputDirectory = Path.GetFullPath(RequireArgument(OutputArgumentPrefix));
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Supplemental output directory does not exist: {outputDirectory}");
        }

        if (RenderingServer.GetRenderingDevice() is null)
        {
            throw new InvalidOperationException("The supplemental capture requires a real rendering device.");
        }

        var packedMain = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Could not load the production main scene.");
        var main = packedMain.Instantiate<Main>()
            ?? throw new InvalidOperationException("Production main did not instantiate.");
        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        await WaitForFramesAsync(18);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController
            ?? throw new InvalidOperationException("Production first-person player is missing.");
        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var viewport = GetViewport();
        player.SetPhysicsProcess(false);
        camera.Current = true;

        var captures = new List<FrameReceipt>(Frames.Length);
        foreach (var (zone, spawn, prefix, pitchDegrees, kind) in Frames)
        {
            main.SwitchZone(zone, spawn);
            await WaitForFramesAsync(10);
            camera.Current = true;

            var baselinePitch = -pitchDegrees;
            player.GetNode<Node3D>("Head").RotationDegrees = new Vector3(baselinePitch, 0, 0);
            await WaitForFramesAsync(4);

            var image = viewport.GetTexture().GetImage()
                ?? throw new InvalidOperationException($"Empty root-viewport image for {prefix}.");
            if (image.GetWidth() != CaptureWidth || image.GetHeight() != CaptureHeight)
            {
                throw new InvalidOperationException($"{prefix} rendered {image.GetWidth()}x{image.GetHeight()}.");
            }

            var fileName = $"{prefix}.png";
            var outputPath = Path.Combine(outputDirectory, fileName);
            if (File.Exists(outputPath))
            {
                throw new IOException($"Refusing to overwrite existing capture {outputPath}.");
            }

            if (image.SavePng(outputPath) != Error.Ok)
            {
                throw new IOException($"Could not save capture {outputPath}.");
            }

            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputPath))).ToLowerInvariant();
            captures.Add(new FrameReceipt(
                prefix,
                zone,
                pitchDegrees,
                kind,
                fileName,
                sha256,
                image.GetWidth(),
                image.GetHeight()));
            GD.Print($"act1-supplemental-capture frame={captures.Count}/{Frames.Length} id={prefix} sha256={sha256[..12]}");
        }

        WriteReceipt(outputDirectory, captures);
        GD.Print($"act1-supplemental-capture: PASS frames={captures.Count} output={outputDirectory}");
        main.Free();
        await WaitForFramesAsync(2);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await WaitForFramesAsync(1);
        GetTree().Quit(0);
    }

    private static string RequireArgument(string prefix)
    {
        var argument = OS.GetCmdlineArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
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

    private static void WriteReceipt(string outputDirectory, IReadOnlyList<FrameReceipt> captures)
    {
        var receipt = new Receipt
        {
            SchemaVersion = 1,
            Kind = "urman.godot_act1_supplemental_directional_capture",
            CapturedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ProductionScene = "res://scenes/main.tscn",
            RootViewport = true,
            SubViewportUsed = false,
            Frames = captures.ToList()
        };

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"urman-act1-supplemental-{Guid.NewGuid():N}.json");
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(receipt, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) + System.Environment.NewLine);
        File.Move(temporaryPath, Path.Combine(outputDirectory, ReceiptFileName), true);
    }

    private sealed record FrameReceipt(
        [property: JsonPropertyName("frame_id")] string FrameId,
        [property: JsonPropertyName("zone")] string Zone,
        [property: JsonPropertyName("pitch_degrees")] float PitchDegrees,
        [property: JsonPropertyName("evidence_kind")] string EvidenceKind,
        [property: JsonPropertyName("output_file")] string OutputFile,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height);

    private sealed class Receipt
    {
        [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = 1;
        [JsonPropertyName("kind")] public string Kind { get; set; } = "urman.godot_act1_supplemental_directional_capture";
        [JsonPropertyName("captured_at_utc")] public string CapturedAtUtc { get; set; } = string.Empty;
        [JsonPropertyName("production_scene")] public string ProductionScene { get; set; } = "res://scenes/main.tscn";
        [JsonPropertyName("root_viewport")] public bool RootViewport { get; set; } = true;
        [JsonPropertyName("subviewport_used")] public bool SubViewportUsed { get; set; }
        [JsonPropertyName("frames")] public List<FrameReceipt> Frames { get; set; } = [];
    }
}
