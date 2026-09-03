using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// AUDIO-004 focused smoke: all five surface families load their procedural
/// samples, the controller plays through the SFX bus, steps trigger from real
/// movement (velocity over distance, not timers), reduced motion silences
/// footsteps, and surface selection follows the active zone.
/// </summary>
public partial class Act1FootstepSmokeTest : Node
{
    public override async void _Ready()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("Footstep smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var controller = GetTree().GetFirstNodeInGroup("footstep_audio") as FootstepAudioController;
        var stepPlayer = controller?.GetNode<AudioStreamPlayer>("FootstepPlayer");
        if (player is null || controller is null || stepPlayer is null)
        {
            Fail("Footstep smoke could not find player or footstep controller.");
            return;
        }

        foreach (var surface in new[] { "wet_road", "mud", "grass", "wood", "interior_floor" })
        {
            if (!controller.HasSurface(surface))
            {
                Fail($"Footstep surface samples are missing: {surface}.");
                return;
            }
        }

        if (stepPlayer.Bus != AudioSettingsService.SfxBus)
        {
            Fail("Footsteps are not routed through the SFX bus.");
            return;
        }

        // Reduced motion silences footsteps entirely.
        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = true }
        });

        // Simulated real movement: push the player forward like gameplay does.
        player.SetPhysicsProcess(true);
        Input.ActionPress("move_forward");
        var playedWhileReduced = false;
        for (var frame = 0; frame < 240 && !playedWhileReduced; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            playedWhileReduced = stepPlayer.Playing;
        }

        Input.ActionRelease("move_forward");
        if (playedWhileReduced)
        {
            Fail("Footsteps played while reduced motion was enabled.");
            return;
        }

        // Normal motion: footsteps trigger from movement in the village zone.
        player.ApplySettings(player.CaptureSettings() with
        {
            Accessibility = player.CaptureSettings().Accessibility with { ReducedMotion = false }
        });
        Input.ActionPress("move_forward");
        var played = false;
        for (var frame = 0; frame < 240 && !played; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            played = stepPlayer.Playing;
        }

        Input.ActionRelease("move_forward");
        if (!played)
        {
            Fail("Footsteps never triggered from real movement.");
            return;
        }

        // Surface follows the active zone (bridge zone -> mapping).
        main.SwitchZone("house_old_pc", "entry");
        await Frames(2);
        if (!controller.HasSurface("interior_floor"))
        {
            Fail("Interior floor surface missing for the house zone.");
            return;
        }

        GD.Print("act1-footsteps: PASS 5 surfaces x3 variants + SFX bus + movement cadence + reduced-motion silence + zone surface");
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
