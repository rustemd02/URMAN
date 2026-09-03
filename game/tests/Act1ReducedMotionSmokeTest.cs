using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-010 focused smoke: with reduced motion enabled, zone transitions cut
/// instantly (the fade overlay clears on the same switch instead of tweening)
/// while the transition overlay itself still exists for normal motion.
/// </summary>
public partial class Act1ReducedMotionSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Reduced motion smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        main.EnableZoneTransitionFade = true;
        AddChild(main);
        await Frames(3);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var overlay = main.GetNode<CanvasLayer>("Act1ZoneTransition").GetNode<ColorRect>("Fade");
        if (player is null || overlay is null)
        {
            Fail("Reduced motion smoke could not find player or transition overlay.");
            return;
        }

        player.SetPhysicsProcess(false);

        // 1) Reduced motion on: the zone switch clears the overlay instantly.
        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = true }
        });
        if (!player.ReducedMotion)
        {
            Fail("Reduced motion setting did not reach the player.");
            return;
        }

        main.SwitchZone("house_old_pc", "entry");
        await Frames(2);
        if (overlay.Color.A > 0.001f)
        {
            Fail($"Reduced-motion zone switch left a visible overlay (alpha={overlay.Color.A}).");
            return;
        }

        // 2) Reduced motion off: the animated transition is present again
        //    (overlay visibly mid-fade right after the switch).
        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = false }
        });
        main.SwitchZone("village_day", "arrival");
        await Frames(1);
        if (overlay.Color.A <= 0.001f)
        {
            Fail("Normal motion lost the zone transition fade entirely.");
            return;
        }

        // Headless frames are not vsync-capped; wait the tween duration in
        // real time instead of counting frames.
        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        await Frames(2);
        if (overlay.Color.A > 0.001f)
        {
            Fail($"The normal transition fade never cleared (alpha={overlay.Color.A}).");
            return;
        }

        GD.Print("act1-reduced-motion: PASS reduced-motion instant cut + normal-motion animated fade intact");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
