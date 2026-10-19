using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    private readonly List<WorldFoleyPlayer> _cellarFoley = [];
    private readonly RandomNumberGenerator _cellarAudioRandom = new();
    private object? _cellarAudioSession;
    private CarryableProp.CarryState _cellarAudioPreviousState;
    private Vector3 _cellarAudioPreviousPosition;
    private bool _cellarAudioPrimed;
    private bool _cellarAudioHeldThisVisit;
    private double _cellarSettleRemaining;
    private int _cellarPreviousCreak = -1;

    private void ProcessCellarAudio(double delta, RuntimeBridge bridge, object session)
    {
        if (!_active || !_carryAttached || !ReferenceEquals(_session, session)
            || !ReferenceEquals(_bridge, bridge) || CellarModalOpen())
        {
            CancelCellarAudio();
            return;
        }
        if (!ReferenceEquals(_cellarAudioSession, session))
        {
            CancelCellarAudio();
            _cellarAudioSession = session;
            _cellarAudioRandom.Randomize();
            _cellarSettleRemaining = _cellarAudioRandom.RandfRange(45f, 90f);
        }
        PruneCellarFoley();
        ObserveCellarBoxContact();
        _cellarSettleRemaining -= Math.Max(0, delta);
        if (_cellarSettleRemaining > 0) return;
        // Drop elapsed intervals rather than catching up after a paused/slow frame.
        _cellarSettleRemaining = _cellarAudioRandom.RandfRange(45f, 90f);
        PlayCellarWoodSettle();
    }

    private void ObserveCellarBoxContact()
    {
        if (_carryCoordinator.ActionInProgress) return;
        var action = _carryCoordinator.PendingAction;
        if (!action.IsCompleted) return;
        var state = _familyBox.State;
        var position = _familyBox.GlobalPosition;
        if (!_cellarAudioPrimed)
        {
            _cellarAudioPrimed = true;
            _cellarAudioPreviousState = state;
            _cellarAudioPreviousPosition = position;
            // A restored held box is silent now; a later new placement command
            // may still produce its actual contact with the support.
            _cellarAudioHeldThisVisit = state == CarryableProp.CarryState.Held;
            return;
        }
        // The canonical owner is idle after a successful ordinary command and
        // its physical state changed. Loads/revisits establish a silent baseline;
        // synchronous reset/reapply projections are never observed as transitions.
        var committedCommand = action.IsCompletedSuccessfully && action.Result;
        if (committedCommand && state == CarryableProp.CarryState.Held
            && _cellarAudioPreviousState is CarryableProp.CarryState.World
                or CarryableProp.CarryState.Placed or CarryableProp.CarryState.Combined)
        {
            _cellarAudioHeldThisVisit = true;
            PlayCellarFoley(_cellarAudioPreviousPosition, "wood_tap", -21f);
        }
        else if (committedCommand && _cellarAudioPreviousState == CarryableProp.CarryState.Held
            && state == CarryableProp.CarryState.Placed && _cellarAudioHeldThisVisit)
        {
            PlayCellarFoley(position, "hollow_board", -20f);
        }
        if (state != CarryableProp.CarryState.Held) _cellarAudioHeldThisVisit = false;
        _cellarAudioPreviousState = state;
        _cellarAudioPreviousPosition = position;
    }

    private void PlayCellarWoodSettle()
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)) return;
        // These are existing physical timber members beside the three linger
        // compositions, not an ambient emitter following the listener.
        Vector3? nearest = null;
        var nearestDistance = 8f;
        void ConsiderTimber(MeshInstance3D? timber)
        {
            if (timber is not { Mesh: { } mesh }) return;
            var bounds = mesh.GetAabb();
            var localListener = timber.ToLocal(player.GlobalPosition);
            var localSource = bounds.GetCenter();
            localSource.X = Mathf.Clamp(localListener.X, bounds.Position.X, bounds.End.X);
            localSource.Z = Mathf.Clamp(localListener.Z, bounds.Position.Z, bounds.End.Z);
            var source = timber.ToGlobal(localSource);
            var distance = source.DistanceTo(player.GlobalPosition);
            if (distance >= nearestDistance) return;
            nearestDistance = distance;
            nearest = source;
        }
        ConsiderTimber(_world.GetNodeOrNull<MeshInstance3D>("SupportedTimberEnvelope/RoofCrossbeam10"));
        ConsiderTimber(_world.GetNodeOrNull<MeshInstance3D>("SupportedTimberEnvelope/RoofCrossbeam58"));
        if (_world.GetNodeOrNull<Node3D>("B05DaylightBoards") is { } daylightFrame)
            foreach (var child in daylightFrame.GetChildren())
                if (child is MeshInstance3D member && member.Name.ToString().StartsWith("LightOpeningEdge", StringComparison.Ordinal))
                    ConsiderTimber(member);
        if (nearest is not { } at) return;
        var variant = (int)_cellarAudioRandom.RandiRange(0, _cellarPreviousCreak < 0 ? 2 : 1);
        if (_cellarPreviousCreak >= 0 && variant >= _cellarPreviousCreak) variant++;
        _cellarPreviousCreak = variant;
        PlayCellarFoley(at, $"parquet/creak_{variant:00}", -29f);
    }

    private void PlayCellarFoley(Vector3 at, string sample, float volumeDb)
    {
        if (UiFoley.PlayWorld(_world, at, sample, volumeDb, 8f, 1.5f) is WorldFoleyPlayer player)
            _cellarFoley.Add(player);
    }

    private void PruneCellarFoley()
    {
        for (var index = _cellarFoley.Count - 1; index >= 0; index--)
            if (!GodotObject.IsInstanceValid(_cellarFoley[index]) || _cellarFoley[index].IsQueuedForDeletion())
                _cellarFoley.RemoveAt(index);
    }

    private void CancelCellarAudio()
    {
        foreach (var player in _cellarFoley)
        {
            if (!GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion()) continue;
            player.Cancel();
            player.QueueFree();
        }
        _cellarFoley.Clear();
        _cellarAudioSession = null;
        _cellarAudioPrimed = false;
        _cellarAudioHeldThisVisit = false;
        _cellarSettleRemaining = 0;
    }
}
