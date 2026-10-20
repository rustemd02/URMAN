using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W02Controller
{
    private const string BoardContactAudioPath = "res://assets/audio/act1/foley/wood_tap.wav";
    private const string WaterAnswerAudioPath = "res://assets/audio/water_edge_ambience.wav";
    private readonly List<WorldFoleyPlayer> _waterResponseFoley = [];
    private object? _waterAudioSession;
    private int? _waterAnswerAudioGeneration;
    private bool WaterAudioTransitionActive => GetTree().GetFirstNodeInGroup("player_controller")
        is not FirstPersonController { SessionTransitionActive: false };

    private void ProcessWaterResponseAudio()
    {
        if (_waterAudioSession is null) return;
        if (!TryGetCurrentEncounterProjection(allowModal: false, allowResolved: true, out var projection)
            || projection is null || !ReferenceEquals(projection.SessionIdentity, _waterAudioSession)
            || WaterAudioTransitionActive)
        {
            CancelWaterResponseAudio();
            return;
        }
        for (var index = _waterResponseFoley.Count - 1; index >= 0; index--)
            if (!GodotObject.IsInstanceValid(_waterResponseFoley[index]) || _waterResponseFoley[index].IsQueuedForDeletion())
                _waterResponseFoley.RemoveAt(index);
        if (_waterResponseFoley.Count == 0 && _waterAnswerAudioGeneration is null) _waterAudioSession = null;
    }

    private void PlayEncounterBoardContact()
    {
        if (_looseBoard is not { } board || !GodotObject.IsInstanceValid(board)
            || WaterAudioTransitionActive
            || !TryGetCurrentEncounterProjection(allowModal: false, allowResolved: false, out var projection)
            || projection is null) return;
        _waterAudioSession = projection.SessionIdentity;
        if (UiFoley.PlayWorld(_world!, board.GlobalPosition, "wood_tap", -21f, 12f, 1.5f) is WorldFoleyPlayer player)
            _waterResponseFoley.Add(player);
    }

    private void CompleteWaterAnswerAudio(bool dispatched, object session, int generation)
    {
        // A successful projection resets the motion generation; retain only this
        // pending command's audio token, which cancellation/load/modal clears.
        if (_waterAnswerAudioGeneration != generation || !ReferenceEquals(_waterAudioSession, session)) return;
        _waterAnswerAudioGeneration = null;
        if (!dispatched || !TryGetCurrentEncounterProjection(allowModal: false, allowResolved: true, out var projection)
            || projection is not { EncounterResolved: true } || !ReferenceEquals(projection.SessionIdentity, session)
            || WaterAudioTransitionActive
            || DisplayServer.GetName() == "headless") return;
        var ring = _encounterRings.Find(candidate => GodotObject.IsInstanceValid(candidate) && !candidate.IsQueuedForDeletion());
        if (ring is null || ResourceLoader.Load<AudioStreamWav>(WaterAnswerAudioPath) is not { } source) return;
        // This existing procedural candidate also serves a looping legacy bed.
        // Duplicate it before disabling the loop; no shared resource is changed.
        var stream = (AudioStreamWav)source.Duplicate();
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        AudioSettingsService.EnsureBuses();
        var player = new WorldFoleyPlayer
        {
            Name = "W02WaterAnswer", Stream = stream, Bus = AudioSettingsService.SfxBus,
            VolumeDb = -22f, UnitSize = 2f, MaxDistance = 14f
        };
        player.SetMeta("audioProvenance", "Project-original procedural water candidate from tools/audio/generate_ambient_audio.py; not a field recording or voice");
        player.AddToGroup("world_foley");
        _world!.AddChild(player);
        player.GlobalPosition = ring.GlobalPosition;
        player.Finished += () => player.QueueFree();
        _waterResponseFoley.Add(player);
        player.RequestPlay(paused: false);
    }

    private void CancelWaterResponseAudio()
    {
        foreach (var player in _waterResponseFoley)
        {
            if (!GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion()) continue;
            player.Cancel();
            player.QueueFree();
        }
        _waterResponseFoley.Clear();
        _waterAudioSession = null;
        _waterAnswerAudioGeneration = null;
    }
}
