using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// ACT1-LEGS.2 gait review probe: walks the arrival street on flat snow,
/// looking down at the visible body, and saves one frame every few physics
/// ticks so the whole stride cycle can be reviewed as a sequence.
/// </summary>
public partial class GaitCaptureProbe : Node
{
    private string _output = null!;
    private int _saved;

    public override async void _Ready()
    {
        _output = System.Environment.GetEnvironmentVariable("URMAN_GAIT_OUTPUT") ?? throw new InvalidOperationException("Set URMAN_GAIT_OUTPUT to a new absolute directory.");
        Directory.CreateDirectory(_output);

        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            GD.PushError("Gait probe could not load main.");
            GetTree().Quit(1);
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        for (var index = 0; index < 10; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null)
        {
            GD.PushError("Gait probe could not find the player.");
            GetTree().Quit(1);
            return;
        }

        // Look steeply down at the visible body, then walk a straight flat stretch.
        var camera = Descendants(player).OfType<Camera3D>().First();
        camera.RotationDegrees = new Vector3(-72f, 0f, 0f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        player.SetPhysicsProcess(true);
        Input.ActionPress("move_forward");
        for (var tick = 0; tick < 96; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (tick % 6 != 0) continue;
            await CaptureFrame(tick);
        }

        Input.ActionRelease("move_forward");
        GD.Print($"gait-capture-probe: PASS saved {_saved} stride frames to {_output}");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task CaptureFrame(int tick)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var file = Path.Combine(_output, $"gait_{_saved:00}_tick{tick:000}.png");
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(file) != Error.Ok)
        {
            GD.PushError($"Gait probe could not save {file}.");
            GetTree().Quit(1);
            return;
        }
        _saved++;
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
