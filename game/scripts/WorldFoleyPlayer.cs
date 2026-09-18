using Godot;

namespace Urman.Godot;

/// <summary>
/// Owns the pending native start of one source-positioned foley sound. A 3D
/// Play request is queued until physics; pausing an absent playback cannot
/// pause that future start. PauseMenuUi still owns the actual pause state.
/// </summary>
public partial class WorldFoleyPlayer : AudioStreamPlayer3D
{
    private bool _pendingStart;
    private bool _paused;

    internal void RequestPlay(bool paused)
    {
        _pendingStart = true;
        SetWorldPaused(paused);
    }

    internal void SetWorldPaused(bool paused)
    {
        _paused = paused;
        if (paused && Playing && !HasStreamPlayback())
        {
            // Pause can arrive between Play and the next native physics tick.
            // Cancel that queued start and keep only this existing request.
            Stop();
            _pendingStart = true;
        }
        StreamPaused = paused;
        if (!paused && _pendingStart)
        {
            _pendingStart = false;
            Play();
        }
        // While a queued start becomes native, apply the requested state to
        // the newly created playback as well. Ordinary playback needs no poll.
        SetProcess(paused && !_pendingStart);
    }

    public override void _Process(double delta)
    {
        if (_paused) StreamPaused = true;
    }

    internal void Cancel()
    {
        _pendingStart = false;
        SetProcess(false);
        Stop();
    }

    public override void _ExitTree()
    {
        Cancel();
        Stream = null;
    }
}
