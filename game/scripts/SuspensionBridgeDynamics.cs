using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// 2026-10-04: makes the old suspension bridge over the Kara-Urman gorge alive.
///
/// The deck and its collision strip are cut into <see cref="Act1ConnectedWorld.SuspensionDeckSections"/>
/// rigid sections.  Every frame a few sine terms give each section a vertical
/// travelling wave (amplitude grows with the player's speed and with the mass of
/// motion — a landing impulse — while the player is on the deck), a lateral sway
/// and a roll, plus a slow gusty idle sway that keeps the span breathing in the
/// permanent bad weather.  The wave is strongest around the walker and decays
/// with distance; the ends keep a node of the standing wave, so the deck never
/// pulls away from the abutments.
///
/// Safety contract: every animated <em>visual</em> node (boards, repairs, stringers,
/// hand ropes, hangers) and every <em>collision</em> shape of the deck and rope
/// walls is driven by the exact same per-section <see cref="Transform3D"/>, so
/// collision and visuals cannot separate.  No physics body per plank: one
/// StaticBody3D with 14+2 box strips whose overlaps only grow (0.14 m between
/// moving sections, a static apron ramp reaching 0.35 m under the first and last
/// moving boxes).  Amplitudes are clamped, and no transform is written while the
/// bridge is broken or far away, so nothing here can open a fall-through.
///
/// Wiring (project owner): <see cref="Initialize"/> once after the bridge exists
/// (BuildSuspensionBridge already does), then <see cref="Tick"/> from the owner's
/// presentation tick, next to Act1ConnectedWorld.WatchSuspensionBridge().
/// The 14 sections are &lt;= 40 as required; work is O(sections + collected nodes),
/// no per-frame allocations.
/// </summary>
public sealed class SuspensionBridgeDynamics
{
    public const int VoiceBudget = 3;
    public const int MaxSections = 20;
    public const int DefaultSections = 14;
    public const float MaxVerticalAmplitude = .30f;
    public const float MaxLateralAmplitude = .16f;
    public const float MaxRollRadians = .036f;      // ~2.1 degrees
    public const float SleepDistanceMeters = 84f;
    public const float WakeDistanceMeters = 78f;
    public const float AudibleDistanceMeters = 62f;

    private const float WalkAmpY = .18f;
    private const float WalkAmpX = .14f;
    private const float WalkRoll = .030f;
    private const float PresenceSag = .05f;
    private const float PresenceSigma = 5.5f;
    private const float IdleAmpY = .028f;
    private const float IdleAmpX = .045f;
    private const float IdleRoll = .010f;
    private const float WaveK = 1.15f;              // rad/m, ~5.5 m wavelength
    private const float WalkOmega = 3.1f;           // rad/s, ~0.5 Hz
    private const float IdleOmegaY = .70f;
    private const float IdleOmegaX = .50f;
    private const float IdleOmegaR = .42f;
    private const float SilentDb = -80f;

    private const string GroanPath = "res://assets/audio/act1/bridge/bridge_groan_loop.wav";
    private const string RattlePath = "res://assets/audio/act1/bridge/bridge_rope_rattle.wav";

    private static readonly string[] CreakPaths =
    [
        "res://assets/audio/act1/foley/parquet/creak_00.wav",
        "res://assets/audio/act1/foley/parquet/creak_01.wav",
        "res://assets/audio/act1/foley/parquet/creak_02.wav",
    ];

    // Only the moving set is collected; the static aprons, sills, ties and posts
    // stay on the rims with their own names and are deliberately skipped.
    private static readonly string[] VisualPrefixes =
        ["SuspensionPlank_", "SuspensionPatch_", "SuspensionBatten_", "SuspensionStringer_", "SuspensionHandRope_", "SuspensionHanger_"];
    private static readonly string[] CollisionPrefixes =
        ["SuspensionDeckShape_", "SuspensionRopeWall_"];

    private readonly struct Entry
    {
        public readonly Node3D Node;
        public readonly Transform3D Rest;
        public readonly int Section;

        public Entry(Node3D node, Transform3D rest, int section)
        {
            Node = node;
            Rest = rest;
            Section = section;
        }
    }

    private readonly RandomNumberGenerator _random = new();
    private readonly float[] _sectionZ = new float[MaxSections];
    private readonly Vector3[] _sectionPivot = new Vector3[MaxSections];
    private readonly Transform3D[] _sectionDelta = new Transform3D[MaxSections];
    private Entry[] _entries = [];
    private int _entryCount;
    private int _sectionCount;

    private Node3D? _world;
    private Node3D? _root;
    private Node3D? _intact;
    private StaticBody3D? _deckBody;
    private Vector3 _centre;
    private float _spanHalf;
    private bool _initialized;
    private bool _sleeping;
    private bool _headless;

    private AudioStreamPlayer3D? _groan;
    private AudioStreamPlayer3D? _creak;
    private AudioStreamPlayer3D? _rattle;
    private AudioStream? _rattleStream;
    private AudioStream?[] _creakStreams = [];

    private Vector3 _lastPlayer;
    private ulong _lastTickMsec;
    private float _time;
    private float _speed;
    private float _drive;
    private float _fall;
    private float _gust = .4f;
    private float _lastGust = .4f;
    private float _gustTarget = .4f;
    private float _gustClock = 2f;
    private float _creakDistance;
    private float _rattleCooldown;
    private float _amplitude;
    private int _creakIndex;
    private bool _reducedMotion;
    private FirstPersonController? _player;
    private int _playerLookupCooldown;

    /// <summary>True once the sections, nodes and voices were collected.</summary>
    public bool IsReady => _initialized;
    /// <summary>Animated rigid sections (14 in the current build).</summary>
    public int Sections => _sectionCount;
    /// <summary>Visual and collision nodes moved by the same section transforms.</summary>
    public int AnimatedNodes => _entryCount;
    /// <summary>Current sway magnitude, 0..~1 (1 = clamped maximum).</summary>
    public float SwayAmplitude => _amplitude;
    /// <summary>True while the player is far away and no transforms are written.</summary>
    public bool Sleeping => _sleeping;

    /// <summary>
    /// Finds the bridge, collects the animated nodes and creates the three audio
    /// voices.  Idempotent; safe to call from a later wiring owner.
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_initialized) return;
        _world = world;
        _root = world.FindChild("SuspensionBridge", true, false) as Node3D;
        _intact = _root?.FindChild("SuspensionBridgeIntact", true, false) as Node3D;
        _deckBody = _root?.FindChild("SuspensionDeckBody", true, false) as StaticBody3D;
        if (_root is null || _intact is null || _deckBody is null)
        {
            GD.PushWarning("suspension-bridge-dynamics: bridge nodes not found (SuspensionBridge/Intact/DeckBody)");
            return;
        }
        _headless = DisplayServer.GetName() == "headless";
        _random.Seed = 20261004;   // deterministic: the bridge must be reproducible for captures

        var entries = new List<Entry>(192);
        Collect(_intact, VisualPrefixes, entries);
        Collect(_deckBody, CollisionPrefixes, entries);
        if (entries.Count == 0)
        {
            GD.PushWarning("suspension-bridge-dynamics: no animated nodes found");
            return;
        }

        var zNear = float.NegativeInfinity;
        var zFar = float.PositiveInfinity;
        foreach (var entry in entries)
        {
            var z = entry.Rest.Origin.Z;
            if (z > zNear) zNear = z;
            if (z < zFar) zFar = z;
        }
        if (zNear - zFar < 1f)
        {
            GD.PushWarning("suspension-bridge-dynamics: deck span looks empty");
            return;
        }
        _sectionCount = DefaultSections;
        if (_root.HasMeta("deckSections"))
        {
            var meta = _root.GetMeta("deckSections").AsInt32();
            if (meta is > 0 and <= MaxSections) _sectionCount = meta;
        }

        var span = zNear - zFar;
        var sums = new Vector3[_sectionCount];
        var counts = new int[_sectionCount];
        var sectioned = new Entry[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var t = Mathf.Clamp((zNear - entry.Rest.Origin.Z) / span, 0f, 1f);
            var section = Mathf.Min(_sectionCount - 1, (int)(t * _sectionCount));
            sectioned[index] = new Entry(entry.Node, entry.Rest, section);
            sums[section] += entry.Rest.Origin;
            counts[section]++;
        }
        _centre = Vector3.Zero;
        for (var section = 0; section < _sectionCount; section++)
        {
            var origin = sums[section] / Mathf.Max(1, counts[section]);
            _sectionPivot[section] = origin;
            _sectionZ[section] = origin.Z;
            _sectionDelta[section] = Transform3D.Identity;
            _centre += origin;
        }
        _centre /= _sectionCount;
        _spanHalf = span * .5f;
        _entries = sectioned;
        _entryCount = sectioned.Length;
        _lastPlayer = _centre;
        BuildVoices();
        _initialized = true;
        GD.Print($"suspension-bridge-dynamics: nodes={_entryCount} sections={_sectionCount} span={span:0.0}m maxAmpY={MaxVerticalAmplitude} voices={VoiceBudget}");
    }

    private static void Collect(Node parent, string[] prefixes, List<Entry> into)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is not Node3D node) continue;
            var name = node.Name.ToString();
            foreach (var prefix in prefixes)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                into.Add(new Entry(node, node.Transform, 0));
                break;
            }
        }
    }

    /// <summary>
    /// One presentation frame from the owner's tick.  <paramref name="playerPosition"/>
    /// is world space; <paramref name="playerOnBridge"/> is the owner's deck gate.
    /// </summary>
    public void Tick(double delta, Vector3 playerPosition, bool playerOnBridge)
    {
        if (!_initialized || _entryCount == 0) return;
        if (!GodotObject.IsInstanceValid(_root) || !GodotObject.IsInstanceValid(_deckBody) || !GodotObject.IsInstanceValid(_intact))
        {
            _initialized = false;
            StopAudio();
            return;
        }
        // The collapse switches the deck to layer 0 before it tweens the pieces;
        // hand the transforms to that tween and stay out of its way.
        if (!_intact!.Visible || _deckBody!.CollisionLayer == 0)
        {
            _lastPlayer = playerPosition;
            StopAudio();
            return;
        }

        var step = (float)Mathf.Clamp(delta, 0, .1);
        var nowMsec = Time.GetTicksMsec();   // a duplicate same-frame drive stays a no-op
        var firstFrame = _lastTickMsec == 0;
        if (!firstFrame) step = (float)Mathf.Clamp((nowMsec - _lastTickMsec) / 1000.0, 0, step);
        _lastTickMsec = nowMsec;
        if (step <= 0f) return;

        var planar = playerPosition - _centre;
        planar.Y = 0f;
        var distance = planar.Length();
        if (_sleeping)
        {
            _lastPlayer = playerPosition;   // no stale speed spike when the player returns
            if (distance > WakeDistanceMeters) return;
            _sleeping = false;
        }
        else if (distance > SleepDistanceMeters)
        {
            _sleeping = true;
            RestoreRest();
            StopAudio();
            return;
        }

        _reducedMotion = _player is not null && GodotObject.IsInstanceValid(_player) && _player.ReducedMotion;
        if (_player is null || !GodotObject.IsInstanceValid(_player)) ResolvePlayer();

        _time += step;
        var move = firstFrame ? Vector3.Zero : playerPosition - _lastPlayer;
        _lastPlayer = playerPosition;
        var speed = Mathf.Min(8f, new Vector2(move.X, move.Z).Length() / step);
        _speed = Mathf.Lerp(_speed, speed, 1f - Mathf.Exp(-step * 5f));
        var onDeck = playerOnBridge && Mathf.Abs(playerPosition.X - _centre.X) < 2.5f;

        _drive = Mathf.MoveToward(_drive, onDeck ? Mathf.Clamp(.16f + .9f * (_speed / 3.4f), 0f, 1.35f) : 0f, step * 3.2f);
        _fall = Mathf.Max(0f, _fall - step * 1.7f);
        if (onDeck && move.Y < -.01f) _fall = Mathf.Max(_fall, Mathf.Min(1.1f, -move.Y / step * .16f));
        var motionScale = _reducedMotion ? .4f : 1f;
        var drive = Mathf.Min(1.75f, _drive + _fall) * motionScale;
        var damp = _reducedMotion ? .45f : 1f;

        _gustClock -= step;
        if (_gustClock <= 0f)
        {
            _gustTarget = _random.RandfRange(.15f, .9f);
            _gustClock = _random.RandfRange(2.2f, 6.5f);
        }
        _gust = Mathf.MoveToward(_gust, _gustTarget, step * .5f);
        var gusty = .4f + .6f * _gust;

        var playerZ = playerPosition.Z;
        _amplitude = 0f;
        for (var section = 0; section < _sectionCount; section++)
        {
            var t = (section + .5f) / _sectionCount;
            var envelope = Mathf.Sin(Mathf.Pi * t);
            var dz = _sectionZ[section] - playerZ;
            var local = onDeck ? Mathf.Exp(-(dz * dz) / (PresenceSigma * PresenceSigma)) : 0f;
            var phase = WaveK * dz - WalkOmega * _time;
            var dy = envelope * (WalkAmpY * drive * (.28f + .72f * local) * Mathf.Sin(phase)
                - PresenceSag * local * damp
                + IdleAmpY * gusty * damp * Mathf.Sin(IdleOmegaY * _time + t * 2.2f + .7f));
            var dx = envelope * (WalkAmpX * drive * local * Mathf.Sin(phase + 1.1f)
                + IdleAmpX * gusty * damp * Mathf.Sin(IdleOmegaX * _time + t * 1.6f + 2.4f));
            var roll = envelope * (WalkRoll * drive * (.4f + .6f * local) * Mathf.Sin(phase + 1.9f)
                + IdleRoll * gusty * damp * Mathf.Sin(IdleOmegaR * _time + t * 1.2f + 3.1f));
            dy = Mathf.Clamp(dy, -MaxVerticalAmplitude, MaxVerticalAmplitude);
            dx = Mathf.Clamp(dx, -MaxLateralAmplitude, MaxLateralAmplitude);
            roll = Mathf.Clamp(roll, -MaxRollRadians, MaxRollRadians);
            // The end sections sit on the static apron ramps: halve their motion so the
            // visual-to-collision step at the rims can never exceed ~2 cm.
            if (section == 0 || section == _sectionCount - 1)
            {
                dy *= .5f;
                dx *= .5f;
                roll *= .5f;
            }

            // Rotate the section about its own deck centre, then translate it.
            var basis = new Basis(Vector3.Back, roll);
            var pivot = _sectionPivot[section];
            _sectionDelta[section] = new Transform3D(basis, pivot + new Vector3(dx, dy, 0f) - basis * pivot);
            var magnitude = Mathf.Max(Mathf.Abs(dy) / MaxVerticalAmplitude, Mathf.Abs(dx) / MaxLateralAmplitude);
            if (magnitude > _amplitude) _amplitude = magnitude;
        }
        for (var index = 0; index < _entryCount; index++)
        {
            var entry = _entries[index];
            entry.Node.Transform = _sectionDelta[entry.Section] * entry.Rest;
        }

        UpdateAudio(step, distance, onDeck, playerPosition, move);
    }

    private void ResolvePlayer()
    {
        if (_playerLookupCooldown-- > 0) return;
        _playerLookupCooldown = 120;
        if (_world is null || !GodotObject.IsInstanceValid(_world)) return;
        _player = _world.GetTree()?.GetFirstNodeInGroup("player_controller") as FirstPersonController;
    }

    private void RestoreRest()
    {
        for (var index = 0; index < _entryCount; index++)
        {
            var entry = _entries[index];
            if (GodotObject.IsInstanceValid(entry.Node)) entry.Node.Transform = entry.Rest;
        }
    }

    private void UpdateAudio(float step, float distance, bool onDeck, Vector3 playerPosition, Vector3 move)
    {
        if (_headless) return;
        var audible = distance < AudibleDistanceMeters;
        if (_groan is not null && _groan.Stream is not null)
        {
            if (audible)
            {
                if (!_groan.Playing)
                {
                    _groan.VolumeDb = SilentDb;
                    _groan.Play();
                }
                var volume = -22f + 20f * _amplitude;   // the prepared loop is peak-limited; see tools/audio/prepare_bridge_sounds.py
                if (Mathf.Abs(_groan.VolumeDb - volume) > .5f) _groan.VolumeDb = volume;
                var pitch = .74f + .40f * _amplitude;
                if (Mathf.Abs(_groan.PitchScale - pitch) > .015f) _groan.PitchScale = pitch;
            }
            else if (_groan.Playing)
            {
                var volume = Mathf.MoveToward(_groan.VolumeDb, SilentDb, step * 40f);
                _groan.VolumeDb = volume;
                if (volume <= SilentDb + .5f) _groan.Stop();
            }
        }
        if (!audible)
        {
            _creakDistance = 0f;
            _rattleCooldown = 0f;
            return;
        }

        if (onDeck && _speed > .35f)
        {
            _creakDistance += new Vector2(move.X, move.Z).Length();
            if (_creakDistance >= Mathf.Clamp(_speed * .5f, 1.15f, 2.6f))
            {
                _creakDistance = 0f;
                PlayCreak(playerPosition);
            }
        }
        else
        {
            _creakDistance = 0f;
        }

        _rattleCooldown = Mathf.Max(0f, _rattleCooldown - step);
        var gustCrest = _gust > .72f && _lastGust <= .72f;
        _lastGust = _gust;
        if (_rattleCooldown <= 0f && (gustCrest || (_amplitude > .38f && _random.Randf() < step * (.35f + _amplitude))))
        {
            PlayRattle(onDeck ? playerPosition : _centre);
            _rattleCooldown = _random.RandfRange(1.6f, 3.4f);
        }
    }

    private void PlayCreak(Vector3 at)
    {
        if (_creak is null || _creakStreams.Length == 0) return;
        var stream = _creakStreams[_creakIndex++ % _creakStreams.Length];
        if (stream is null) return;
        _creak.Stream = stream;
        _creak.GlobalPosition = at + new Vector3(_random.RandfRange(-.35f, .35f), .12f, _random.RandfRange(-.35f, .35f));
        _creak.PitchScale = _random.RandfRange(.86f, 1.14f);
        _creak.VolumeDb = -13f + Mathf.Min(6f, _speed) * .7f + _random.RandfRange(-1.5f, 1.5f);
        _creak.Play();
    }

    private void PlayRattle(Vector3 near)
    {
        if (_rattle is null || _rattleStream is null) return;
        var z = Mathf.Clamp(near.Z + _random.RandfRange(-3.5f, 3.5f), _centre.Z - _spanHalf, _centre.Z + _spanHalf);
        var side = _random.Randf() < .5f ? -1f : 1f;
        _rattle.GlobalPosition = new Vector3(_centre.X + side * .78f, DeckYAt(z) + .95f, z);
        _rattle.PitchScale = _random.RandfRange(.9f, 1.2f);
        _rattle.VolumeDb = -10f + 9f * _amplitude + _random.RandfRange(-1.5f, 1.5f);
        _rattle.Play();
    }

    private float DeckYAt(float z)
    {
        var t = Mathf.Clamp((_centre.Z - z) / Mathf.Max(.01f, _spanHalf * 2f) + .5f, 0f, 1f);
        var index = Mathf.Min(_sectionCount - 1, (int)(t * _sectionCount));
        return _sectionPivot[index].Y;
    }

    private void BuildVoices()
    {
        if (_root is null) return;
        AudioSettingsService.EnsureBuses();
        _groan = new AudioStreamPlayer3D
        {
            Name = "BridgeSwayVoice",
            Bus = AudioSettingsService.AmbienceBus,
            VolumeDb = SilentDb,
            MaxDistance = 60f,
            UnitSize = 8f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 3400f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1,
            Stream = LoadLoop(GroanPath)
        };
        _creak = new AudioStreamPlayer3D
        {
            Name = "BridgeCreakVoice",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = -14f,
            MaxDistance = 28f,
            UnitSize = 3f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 5600f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 2
        };
        _rattle = new AudioStreamPlayer3D
        {
            Name = "BridgeRattleVoice",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = -14f,
            MaxDistance = 45f,
            UnitSize = 4.5f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 6200f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1
        };
        var streams = new AudioStream?[CreakPaths.Length];
        for (var index = 0; index < CreakPaths.Length; index++) streams[index] = LoadOneShot(CreakPaths[index]);
        _creakStreams = streams;
        _rattleStream = LoadOneShot(RattlePath);
        if (_rattle is not null) _rattle.Stream = _rattleStream;
        _root.AddChild(_groan);
        _root.AddChild(_creak);
        _root.AddChild(_rattle);
        if (_groan is not null) _groan.GlobalPosition = _centre + Vector3.Up * .4f;
        _root.SetMeta("audioPool", "3 x AudioStreamPlayer3D: BridgeSwayVoice (Ambience loop) + BridgeCreakVoice + BridgeRattleVoice (SFX one-shots)");
        _root.SetMeta("audioReady", _groan?.Stream is not null && _rattleStream is not null);
    }

    private void StopAudio()
    {
        if (_headless) return;
        _groan?.Stop();
        _creak?.Stop();
        _rattle?.Stop();
    }

    private static AudioStream? LoadLoop(string path)
    {
        var stream = LoadStream(path);
        if (stream is AudioStreamWav wav)
        {
            wav.LoopBegin = 0;
            wav.LoopEnd = Mathf.RoundToInt(wav.GetLength() * wav.MixRate);
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        }
        return stream;
    }

    private static AudioStream? LoadOneShot(string path)
    {
        var stream = LoadStream(path);
        if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        return stream;
    }

    private static AudioStream? LoadStream(string path)
    {
        // Ignore the cache: the loop flag is written into the resource and these
        // files belong to the bridge alone, so no other owner can be affected.
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path, cacheMode: ResourceLoader.CacheMode.Ignore) : null;
    }
}
