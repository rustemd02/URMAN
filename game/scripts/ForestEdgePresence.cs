using Godot;

namespace Urman.Godot;

/// <summary>
/// Creepy presence that grows as the player approaches the forest edge: real
/// recordings of ravens, owls, fox and branch cracks, plus the project's forest
/// foley, placed around the KaraForestEdge anchor. It is deliberately louder and
/// denser than the prologue forest so the author hears the forest as a scary
/// neighbour (request 2026-10-04), yet it keeps a fixed three-voice budget and
/// no per-frame allocation. Presentation only.
/// </summary>
public partial class ForestEdgePresence : Node3D
{
    public const int VoiceBudget = 3;
    public const float EdgeRadiusMeters = 60f;
    public const float BaseGapSeconds = 9f;
    public const float CloseGapSeconds = 2.5f;

    private readonly AudioStreamPlayer3D[] _voices = new AudioStreamPlayer3D[VoiceBudget];
    private readonly RandomNumberGenerator _random = new();
    private Node3D? _edge;
    private double _now;
    private double _nextCue;
    private int _cuesStarted;

    public int CuesStarted => _cuesStarted;
    public bool AnchorFound => _edge is not null;
    public int PlayingVoices => _voices.Count(voice => voice is not null && voice.Playing);

    public void Initialize(Node3D world)
    {
        if (_voices[0] is not null) return;
        _random.Randomize();
        AudioSettingsService.EnsureBuses();
        for (var index = 0; index < _voices.Length; index++)
        {
            var voice = new AudioStreamPlayer3D
            {
                Name = $"ForestEdgeVoice{index}",
                Bus = AudioSettingsService.AmbienceBus,
                VolumeDb = -18f,
                MaxDistance = 130f,
                UnitSize = 12f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                AttenuationFilterCutoffHz = 6000f,
                AttenuationFilterDb = -6f,
                MaxPolyphony = 1
            };
            AddChild(voice);
            _voices[index] = voice;
        }

        _edge = world.FindChild("KaraForestEdge", true, false) as Node3D;
        SetMeta("anchorPath", _edge?.GetPath().ToString() ?? "missing");
        SetMeta("voiceBudget", VoiceBudget);
        SetMeta("edgeRadiusMeters", EdgeRadiusMeters);
        GD.Print($"forest-edge-presence: anchor={_edge is not null} voices={VoiceBudget} radius={EdgeRadiusMeters:0}m");
    }

    /// <summary>One cue every few seconds as the player closes on the forest.</summary>
    public void Tick(double delta, Vector3 listener, bool audible)
    {
        if (_voices[0] is null) return;
        _now += Math.Min(delta, .5);
        if (!audible || _edge is null || !IsInstanceValid(_edge))
        {
            return;
        }

        var distance = listener.DistanceTo(_edge.GlobalPosition);
        if (distance > EdgeRadiusMeters || _now < _nextCue)
        {
            return;
        }

        var slot = Array.FindIndex(_voices, voice => !voice.Playing);
        if (slot < 0)
        {
            return;
        }

        var closeness = Mathf.Clamp(1f - distance / EdgeRadiusMeters, 0f, 1f);
        var (sample, radius) = PickCue(closeness);
        var stream = ResourceLoader.Load<AudioStream>(sample);
        if (stream is null)
        {
            return;
        }

        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        }

        var angle = _random.RandfRange(0f, Mathf.Tau);
        var spread = _random.RandfRange(8f, Mathf.Min(radius, 42f));
        var voice = _voices[slot];
        voice.Stream = stream;
        voice.GlobalPosition = _edge.GlobalPosition + new Vector3(Mathf.Cos(angle) * spread, _random.RandfRange(2f, 9f), Mathf.Sin(angle) * spread);
        voice.VolumeDb = -20f + closeness * 9f + _random.RandfRange(-1.5f, 1.5f);
        voice.PitchScale = _random.RandfRange(.94f, 1.06f);
        voice.SetMeta("cue", sample);
        voice.Play();
        _cuesStarted++;
        _nextCue = _now + Mathf.Lerp(BaseGapSeconds, CloseGapSeconds, closeness) * _random.RandfRange(.7f, 1.3f);
    }

    /// <summary>
    /// Existing project foley plus, when present, the real CC0 winter corvid and
    /// forest-edge recordings prepared by the sound batch.
    /// </summary>
    private (string Sample, float Radius) PickCue(float closeness)
    {
        var raven = ResourceLoader.Exists("res://assets/audio/act1/sound_mood/raven.wav")
            ? "res://assets/audio/act1/sound_mood/raven.wav" : "res://assets/audio/act1/foley/forest/owl_tawny.wav";
        var magpie = ResourceLoader.Exists("res://assets/audio/act1/sound_mood/magpie.wav")
            ? "res://assets/audio/act1/sound_mood/magpie.wav" : "res://assets/audio/act1/foley/forest/owl_eagle.wav";
        string[] close = ["res://assets/audio/act1/foley/forest/branch_crack.wav", "res://assets/audio/act1/foley/forest/brush_rustle.wav", raven];
        string[] mid = ["res://assets/audio/act1/foley/forest/fox_scream.wav", magpie, "res://assets/audio/act1/foley/forest/owl_eagle.wav", raven];
        if (ResourceLoader.Exists("res://assets/audio/act1/sound_mood/forest_branch.wav"))
            mid = [.. mid, "res://assets/audio/act1/sound_mood/forest_branch.wav"];
        string[] far = ["res://assets/audio/act1/foley/forest/wolf_far.wav", "res://assets/audio/act1/foley/forest/owl_tawny.wav", "res://assets/audio/act1/foley/forest/low_moan.wav"];
        var table = closeness > .66f ? close : closeness > .33f ? mid : far;
        var sample = table[_random.RandiRange(0, table.Length - 1)];
        var radius = table == close ? 16f : table == mid ? 34f : 70f;
        return (sample, radius);
    }
}
