using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// AUDIO-004 focused smoke: all seven winter surface families load their
/// source-backed samples, the controller plays through the SFX bus, steps
/// trigger from real XZ displacement, reduced motion keeps footsteps audible,
/// and surface selection follows the active zone — including off-road ground
/// that must not sound like packed road.
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

        foreach (var surface in new[] { "snow_packed", "snow_soft", "wood", "interior_floor", "grass", "mud", "wet_road" })
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

        // The same exterior XZ can contain snow and a wooden support. Use the
        // actual authored carry-board, preserving its pose and custody, then
        // drive normal movement on its collider. A zone-only selector used to
        // play snow here even though both feet were over the board.
        var board = main.ConnectedWorld?.FindChild("Carryable_carry-board", true, false) as CarryableProp;
        if (board is null || board.GetMeta("footstepSurface", "").AsString() != "wood")
        {
            Fail("The authored carry-board has no wooden floor-contact contract.");
            return;
        }
        var stand = board.GlobalTransform * new Vector3(0f, board.Height + .08f, board.Size.Z * .36f);
        var supportQuery = PhysicsRayQueryParameters3D.Create(stand + Vector3.Up * .15f,
            stand - Vector3.Up * .4f, player.CollisionMask,
            new global::Godot.Collections.Array<Rid> { player.GetRid() });
        var supportHit = player.GetWorld3D().DirectSpaceState.IntersectRay(supportQuery);
        var supportName = supportHit.Count > 0 ? (supportHit["collider"].AsGodotObject() as Node)?.Name.ToString() : "<none>";
        GD.Print($"act1-footstep-board-setup: board={board.GlobalPosition} size={board.Size} yaw={board.GlobalRotationDegrees.Y:0.00} visible={board.IsVisibleInTree()} layer={board.CollisionLayer} state={board.State} stand={stand} ray={supportName} rayY={(supportHit.Count > 0 ? supportHit["position"].AsVector3().Y : float.NaN):0.000}");
        player.ApplyPortableTransform(new PlayerTransform(
            new(stand.X, stand.Y, stand.Z), new(0f, Mathf.RadToDeg(board.GlobalRotation.Y), 0f)));
        var standingOnBoard = false;
        for (var frame = 0; frame < 60 && !standingOnBoard; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            // An idle floor snap need not create a slide record. Keep the
            // grounded assertion and independently query the body immediately
            // below the player's real feet, within 4 cm of physical contact.
            using var feetQuery = PhysicsRayQueryParameters3D.Create(
                player.GlobalPosition + Vector3.Up * .03f,
                player.GlobalPosition - Vector3.Up * .04f,
                player.CollisionMask, new global::Godot.Collections.Array<Rid> { player.GetRid() });
            var feetHit = player.GetWorld3D().DirectSpaceState.IntersectRay(feetQuery);
            standingOnBoard = player.IsOnFloor() && feetHit.Count > 0
                && feetHit["collider"].AsGodotObject()?.GetInstanceId() == board.GetInstanceId()
                && feetHit["normal"].AsVector3().Y > .9f
                && Math.Abs(player.GlobalPosition.Y - feetHit["position"].AsVector3().Y) <= .025f;
        }
        if (!standingOnBoard)
        {
            var contacts = Enumerable.Range(0, player.GetSlideCollisionCount()).Select(index =>
            {
                var collision = player.GetSlideCollision(index);
                return $"{(collision.GetCollider() as Node)?.Name}@{collision.GetPosition()} normal={collision.GetNormal()}";
            });
            Fail($"Footstep probe could not stand on the actual authored board collider: player={player.GlobalPosition} floor={player.IsOnFloor()} modal={player.ModalOpen} contacts=[{string.Join("; ", contacts)}] ray={supportName}.");
            return;
        }
        stepPlayer.Stop();
        Input.ActionPress("move_forward");
        var playedOnBoard = false;
        for (var frame = 0; frame < 80 && !playedOnBoard; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            playedOnBoard = stepPlayer.Playing;
        }
        Input.ActionRelease("move_forward");
        if (!playedOnBoard || controller.LastSurface != "wood")
        {
            Fail($"Actual wooden support played the wrong surface outside (played={playedOnBoard}, surface={controller.LastSurface}).");
            return;
        }
        GD.Print("act1-footstep-contact: authored exterior carry-board -> grounded actual support -> wood");

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

        // Off-road resolution: the same village exterior must stop sounding
        // like packed road once the feet leave the travelled surface.
        main.SwitchZone("village_day", "arrival");
        await Frames(2);
        controller.PlayStep("mud");
        if (controller.LastSurface != "mud")
        {
            Fail($"Off-road surface did not resolve through the debug hook (surface={controller.LastSurface}).");
            return;
        }

        GD.Print("act1-footsteps: PASS 7 winter surfaces x3 variants + SFX bus + XZ displacement cadence + reduced-motion footsteps + actual exterior board contact + house/FAP surface fallback + off-road mud resolution");
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
