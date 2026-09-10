using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Test-only evidence harness for the winter snow trample field: spawns the
/// production world, walks the player along the arrival street and saves
/// frames so the packed trail can be reviewed. Gated by
/// URMAN_TRAMPLE_SHOT_DIR; production behaviour is never affected.
/// </summary>
public partial class WinterTrampleCapture : Node
{
    private string _directory = string.Empty;

    public override async void _Ready()
    {
        _directory = System.Environment.GetEnvironmentVariable("URMAN_TRAMPLE_SHOT_DIR") ?? string.Empty;
        if (string.IsNullOrEmpty(_directory))
        {
            GD.Print("winter-trample-capture: URMAN_TRAMPLE_SHOT_DIR is not set; nothing captured.");
            GetTree().Quit(0);
            return;
        }

        System.IO.Directory.CreateDirectory(_directory);

        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            GD.PushError("winter-trample-capture: could not instantiate main.tscn.");
            GetTree().Quit(1);
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);

        for (var i = 0; i < 120; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null)
        {
            GD.PushError("winter-trample-capture: player controller is unavailable.");
            GetTree().Quit(1);
            return;
        }

        Save("01_before");
        await Walk(player, 420);
        Save("02_after_trail");
        await Walk(player, 420);
        Save("03_after_long_trail");

        GD.Print($"winter-trample-capture: saved frames to {_directory}");
        GetTree().Quit(0);
    }

    private async System.Threading.Tasks.Task Walk(FirstPersonController player, int physicsFrames)
    {
        Input.ActionPress("move_forward");
        for (var i = 0; i < physicsFrames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        Input.ActionRelease("move_forward");
        for (var i = 0; i < 6; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Save(string name)
    {
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(System.IO.Path.Combine(_directory, $"{name}.png"));
    }
}
