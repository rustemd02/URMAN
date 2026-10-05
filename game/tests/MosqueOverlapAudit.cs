using System;
using Godot;
using Detector = Urman.Godot.MosqueOverlapAudit;

namespace Urman.Godot.Tests;

/// <summary>
/// Smoke scene for the mosque overlap detector. Boots the ordinary New Game
/// (same entry as the other Act I smokes), waits past the prologue, resolves
/// Act1CoreWorldGreybox/VillageMosqueComplex (or the node named by
/// URMAN_MOSQUE_AUDIT_ROOT) and runs MosqueOverlapAudit.Run on it. Writes the
/// markdown report to URMAN_MOSQUE_OVERLAP_REPORT (default
/// /tmp/urman_mosque_overlaps_runtime.md) and exits 0 for diagnostics. Set
/// URMAN_MOSQUE_OVERLAP_GATE=1 to exit 2 when any confirmed pair remains.
/// </summary>
public partial class MosqueOverlapAudit : Node
{
    private const string DefaultRootPath = "Act1CoreWorldGreybox/VillageMosqueComplex";
    private const string DefaultReportPath = "/tmp/urman_mosque_overlaps_runtime.md";

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var exit = 1;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(10);
            if (!await this.StartThroughMainMenuAsync(demo))
                throw new InvalidOperationException("ordinary New Game did not start through the main menu");
            demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
            for (var frame = 0; frame < 600 && demo.PrologueActive; frame++) await Frames(1);
            await Frames(10);
            var world = demo.DemoMain.ConnectedWorld
                ?? throw new InvalidOperationException("connected world did not build");
            var rootPath = OS.GetEnvironment("URMAN_MOSQUE_AUDIT_ROOT");
            if (string.IsNullOrEmpty(rootPath)) rootPath = DefaultRootPath;
            var mosque = world.GetNodeOrNull<Node3D>(rootPath)
                ?? throw new InvalidOperationException("mosque root not found: " + rootPath);
            GD.Print($"mosque-overlap-audit: running on {mosque.GetPath()}");
            var report = Detector.Run(mosque);
            report.Print();
            var reportPath = OS.GetEnvironment("URMAN_MOSQUE_OVERLAP_REPORT");
            if (string.IsNullOrEmpty(reportPath)) reportPath = DefaultReportPath;
            report.WriteMarkdown(reportPath, mosque.GetPath().ToString());
            GD.Print("mosque-overlap-audit: report=" + reportPath);
            exit = OS.GetEnvironment("URMAN_MOSQUE_OVERLAP_GATE") == "1" && report.Pairs.Count > 0 ? 2 : 0;
        }
        catch (Exception error)
        {
            GD.PrintErr("mosque-overlap-audit: FAIL " + error);
        }
        finally
        {
            if (GetChildCount() > 0) await GodotSmokeCleanup.ReleaseAsync(GetChild(0));
            GetTree().Quit(exit);
        }
    }

    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
