using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private WorldFoleyPlayer? _houseShutterFoley;
    private object? _houseShutterAudioSession;
    private int _houseShutterAudioGeneration = -1;

    private void StartHouseShutterAudio()
    {
        if (_housePhysicalAction != HousePhysicalAction.Shutter
            || _housePhysicalPhase != HousePhysicalPhase.Moving
            || _houseShutterAudioGeneration == _housePhysicalActionGeneration
            || !CanKeepHouseShutterAudio(_housePhysicalActionSession)
            || !TryGetHouseShutterAudioPosition(out var at))
            return;

        CancelHouseShutterAudio();
        // One request per actual moving action, including silent headless playback.
        // Pose projections and the reduced-motion instant path never call this hook.
        _houseShutterAudioGeneration = _housePhysicalActionGeneration;
        _houseShutterAudioSession = _housePhysicalActionSession;
        _houseShutterFoley = UiFoley.PlayWorld(_world, at, "door_creak", -23f, 8f, 1.5f)
            as WorldFoleyPlayer;
    }

    private void ProcessHouseShutterAudio()
    {
        if (_houseShutterFoley is not { } player) return;
        if (!GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion()
            || !CanKeepHouseShutterAudio(_houseShutterAudioSession)
            || !TryGetHouseShutterAudioPosition(out var at))
        {
            CancelHouseShutterAudio();
            return;
        }

        // Follow the two actual pin hinges, rather than putting the sound at
        // the player or an authored marker that may not match the live shutter.
        player.GlobalPosition = at;
    }

    private bool CanKeepHouseShutterAudio(object? session) =>
        _world is not null && GodotObject.IsInstanceValid(_world) && _world.IsVisibleInTree()
        && IsHousePhysicalActionCurrent(_bridge, session, allowModal: false)
        && GetTree().GetFirstNodeInGroup("player_controller")
            is FirstPersonController { SessionTransitionActive: false };

    private bool TryGetHouseShutterAudioPosition(out Vector3 at)
    {
        at = default;
        if (_shutterPivot is null || !GodotObject.IsInstanceValid(_shutterPivot)
            || _shutterPivot.GetNodeOrNull<Node3D>("TimberShutterBody/HingeTop") is not { } top
            || _shutterPivot.GetNodeOrNull<Node3D>("TimberShutterBody/HingeBottom") is not { } bottom
            || !GodotObject.IsInstanceValid(top) || !GodotObject.IsInstanceValid(bottom)
            || !top.IsInsideTree() || !bottom.IsInsideTree()
            || top.IsQueuedForDeletion() || bottom.IsQueuedForDeletion())
            return false;

        at = (top.GlobalPosition + bottom.GlobalPosition) * .5f;
        return true;
    }

    private void CancelHouseShutterAudio()
    {
        if (_houseShutterFoley is { } player && GodotObject.IsInstanceValid(player)
            && !player.IsQueuedForDeletion())
        {
            player.Cancel();
            player.QueueFree();
        }
        _houseShutterFoley = null;
        _houseShutterAudioSession = null;
    }
}
