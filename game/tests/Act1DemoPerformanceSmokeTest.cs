using System.Globalization;
using Godot;
using Urman.Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Measures the real Act 1 demo entrypoint rather than the isolated style
/// SubViewport benchmark. This is diagnostic evidence only; it owns no
/// gameplay settings and never changes the demo scene.
/// </summary>
public partial class Act1DemoPerformanceSmokeTest : Node
{
    private const int WarmupFrames = 20;
    private const int SampleFrames = 60;

    public override async void _Ready()
    {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn");
        var demo = packed?.Instantiate<Act1DemoRoot>();
        if (demo is null)
        {
            Fail("Act 1 demo scene could not be instantiated for the real-entrypoint performance probe.");
            return;
        }

        AddChild(demo);
        for (var frame = 0; frame < WarmupFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var samples = new double[SampleFrames];
        for (var frame = 0; frame < SampleFrames; frame++)
        {
            var start = Time.GetTicksUsec();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            samples[frame] = (Time.GetTicksUsec() - start) / 1000.0;
        }

        var sorted = samples.OrderBy(value => value).ToArray();
        var average = samples.Average();
        var p95 = sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)];
        var maximum = samples.Max();
        var fps = 1000.0 / average;
        var nodes = CountNodes(demo);
        var renderer = RenderingServer.GetRenderingDevice() is null
            ? "unavailable"
            : "Godot RenderingDevice";
        var player = demo.DemoMain.GetNodeOrNull<FirstPersonController>("Player");
        var effectiveScale = GetViewport().Scaling3DScale;
        var effectiveMsaa = GetViewport().Msaa3D;

        GD.Print(string.Join(' ',
            "act1-demo-entrypoint-performance:",
            $"nodes={nodes}",
            $"avg={average.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"p95={p95.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"max={maximum.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"fps={fps.ToString("F2", CultureInfo.InvariantCulture)}",
            $"renderer={renderer}",
            $"preset={player?.GraphicsPreset ?? "unknown"}",
            $"scale={effectiveScale.ToString("F2", CultureInfo.InvariantCulture)}",
            $"msaa={effectiveMsaa}"));

        await GodotSmokeCleanup.ReleaseAsync(demo);
        GetTree().Quit(fps >= 10.0 ? 0 : 1);
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
}
