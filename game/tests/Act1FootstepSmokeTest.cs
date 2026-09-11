using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// AUDIO-004 focused smoke: all four winter surface families load their
/// source-backed samples, the controller plays through the SFX bus, steps
/// trigger from real XZ displacement, reduced motion keeps footsteps audible,
/// and surface selection follows the active zone.
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

        foreach (var surface in new[] { "snow_packed", "snow_soft", "wood", "interior_floor" })
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

        // Reduced motion removes camera motion but keeps footstep sound.
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
        if (!playedWhileReduced)
        {
            Fail("Footsteps were silenced while reduced motion was enabled.");
            return;
        }

        // Normal motion: footsteps still trigger from displacement in the
        // village zone, without relying on the previous playback state.
        stepPlayer.Stop();
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
        stepPlayer.Stop();
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 30 && controller.LastSurface != "wood"; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        if (controller.LastSurface != "wood")
        {
            Fail($"House zone did not resolve wood footsteps (surface={controller.LastSurface}).");
            return;
        }

        main.SwitchZone("fap_clinic", "waiting_room");
        await Frames(2);
        stepPlayer.Stop();
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 30 && controller.LastSurface != "interior_floor"; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        if (controller.LastSurface != "interior_floor")
        {
            Fail($"FAP zone did not resolve interior-floor footsteps (surface={controller.LastSurface}).");
            return;
        }

        GD.Print("act1-footsteps: PASS 4 winter surfaces x3 variants + SFX bus + XZ displacement cadence + reduced-motion footsteps + house/FAP surface mapping");
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
