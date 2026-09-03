using System.Globalization;
using System.Text;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Render-time probe for the three mandatory style scenes.
/// This is a test harness, not a gameplay telemetry owner.
/// </summary>
public partial class PerformanceBenchmark : Node
{
    private static readonly (string Name, string ScenePath, Vector3 Camera, Vector3 Target)[] Scenes =
    [
        ("day_street", "res://scenes/zones/style_benchmark_day_street.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0, 1.45f, -7.5f)),
        ("house_old_pc", "res://scenes/zones/style_benchmark_house_pc.tscn", new Vector3(-1.45f, 1.68f, 1.75f), new Vector3(0, 1.38f, -3.45f)),
        ("kara_urman_edge", "res://scenes/zones/style_benchmark_kara_urman_night.tscn", new Vector3(0, 1.7f, 12.5f), new Vector3(0.55f, 1.35f, -7.2f))
    ];

    private const int WarmupFrames = 24;
    private const int SampleFrames = 120;
    private const double LowPresetFloorFps = 30.0;

    public override async void _Ready()
    {
        var outputPath = ProjectSettings.GlobalizePath("res://../docs/urman_knowledge_base/performance/godot_m4pro_baseline.tsv");
        var outputDirectory = System.IO.Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory)
            || DirAccess.MakeDirRecursiveAbsolute(outputDirectory) != Error.Ok)
        {
            Fail($"Could not create performance report directory for {outputPath}.");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("host\tApple M4 Pro (local)\t" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        report.AppendLine("viewport\t1920x1080\trenderer\tForward+\tmsaa\t2x");
        report.AppendLine("scene\tnodes\tavg_frame_ms\tp95_frame_ms\tmax_frame_ms\tavg_fps\tlow_floor_30fps");

        foreach (var scene in Scenes)
        {
            var result = await MeasureAsync(scene.Name, scene.ScenePath, scene.Camera, scene.Target);
            if (result is null)
            {
                return;
            }

            var lowFloor = result.Value.AverageFps >= LowPresetFloorFps;
            report.AppendLine(string.Join('\t',
                scene.Name,
                result.Value.NodeCount.ToString(CultureInfo.InvariantCulture),
                result.Value.AverageFrameMs.ToString("F3", CultureInfo.InvariantCulture),
                result.Value.P95FrameMs.ToString("F3", CultureInfo.InvariantCulture),
                result.Value.MaxFrameMs.ToString("F3", CultureInfo.InvariantCulture),
                result.Value.AverageFps.ToString("F2", CultureInfo.InvariantCulture),
                lowFloor ? "PASS" : "FAIL"));

            if (!lowFloor)
            {
                Fail($"Performance benchmark fell below the 30 FPS low-preset floor: {scene.Name}.");
                return;
            }
        }

        using var reportFile = global::Godot.FileAccess.Open(outputPath, global::Godot.FileAccess.ModeFlags.Write);
        if (reportFile is null)
        {
            Fail($"Could not write performance report: {outputPath}.");
            return;
        }

        reportFile.StoreString(report.ToString());
        GD.Print($"performance-benchmark: {Scenes.Length} scenes, 30 FPS floor PASS -> {outputPath}");
        GetTree().Quit(0);
    }

    private async Task<Measurement?> MeasureAsync(
        string name,
        string scenePath,
        Vector3 cameraPosition,
        Vector3 cameraTarget)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            Fail($"Could not load performance scene: {scenePath}.");
            return null;
        }

        var viewport = new SubViewport
        {
            Name = $"Benchmark_{name}",
            Size = new Vector2I(1920, 1080),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa2X
        };
        AddChild(viewport);
        viewport.AddChild(packed.Instantiate());

        var camera = new Camera3D
        {
            Name = "BenchmarkCamera",
            Position = cameraPosition,
            Fov = 75,
            Current = true
        };
        viewport.AddChild(camera);
        camera.LookAt(cameraTarget, Vector3.Up);

        for (var index = 0; index < WarmupFrames; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var samples = new double[SampleFrames];
        for (var index = 0; index < SampleFrames; index++)
        {
            var start = Time.GetTicksUsec();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            samples[index] = (Time.GetTicksUsec() - start) / 1000.0;
        }

        var sorted = samples.OrderBy(value => value).ToArray();
        var average = samples.Average();
        var p95 = sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)];
        var maximum = samples.Max();
        var nodes = CountNodes(viewport);
        viewport.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GD.Print($"performance-scene: {name} nodes={nodes} avg={average:F3}ms p95={p95:F3}ms max={maximum:F3}ms fps={1000.0 / average:F2}");
        return new Measurement(nodes, average, p95, maximum, 1000.0 / average);
    }

    private static int CountNodes(Node node)
    {
        var count = 1;
        foreach (var child in node.GetChildren())
        {
            count += CountNodes(child);
        }

        return count;
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private readonly record struct Measurement(
        int NodeCount,
        double AverageFrameMs,
        double P95FrameMs,
        double MaxFrameMs,
        double AverageFps);
}
