using Godot;

namespace Urman.Godot;

/// <summary>
/// AUDIO-004: presentation-only footstep cadence driven by the player's real
/// movement. Holds no gameplay state — surface selection is a pure zone/space
/// mapping, steps play through the SFX bus, and reduced motion never changes
/// movement itself.
/// </summary>
public partial class FootstepAudioController : Node
{
    private FirstPersonController? _actor;
    private AudioStreamPlayer? _stepPlayer;
    private readonly System.Collections.Generic.Dictionary<string, AudioStream[]> _bySurface = new();
    private readonly System.Random _random = new();
    private float _distanceSinceStep;
    private int _variant;

    /// <summary>Metres of travel between steps (walk cadence).</summary>
    public float StepIntervalMeters { get; set; } = 1.9f;

    public override void _Ready()
    {
        AddToGroup("footstep_audio");
        AudioSettingsService.EnsureBuses();
        LoadSurface("wet_road");
        LoadSurface("mud");
        LoadSurface("grass");
        LoadSurface("wood");
        LoadSurface("interior_floor");
        _stepPlayer = new AudioStreamPlayer { Name = "FootstepPlayer", Bus = AudioSettingsService.SfxBus };
        _stepPlayer.VolumeDb = -14f;
        AddChild(_stepPlayer);
    }

    public override void _ExitTree()
    {
        // Release the last step stream synchronously so the evidence wrapper
        // does not report a false-positive resource leak at exit.
        if (_stepPlayer is not null)
        {
            _stepPlayer.Stop();
            _stepPlayer.Stream = null;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_actor is null)
        {
            _actor = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
            if (_actor is null)
            {
                return;
            }
        }

        if (_actor.ReducedMotion || _actor.Velocity.LengthSquared() < 0.04f || !_actor.IsOnFloor())
        {
            _distanceSinceStep = 0f;
            return;
        }

        _distanceSinceStep += _actor.Velocity.Length() * (float)delta;
        if (_distanceSinceStep < StepIntervalMeters)
        {
            return;
        }

        _distanceSinceStep = 0f;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        PlayStep(SurfaceForZone(bridge?.CurrentZoneId ?? "village_day"));
    }

    /// <summary>Test/debug hook: play one step of the given surface now.</summary>
    public void PlayStep(string surface)
    {
        if (_stepPlayer is null
            || !_bySurface.TryGetValue(surface, out var variants)
            || variants.Length == 0)
        {
            return;
        }

        _variant = (_variant + 1) % variants.Length;
        _stepPlayer.Stream = variants[_variant];
        _stepPlayer.PitchScale = 0.94f + (float)_random.NextDouble() * 0.12f;
        _stepPlayer.Play();
    }

    public bool HasSurface(string surface) => _bySurface.ContainsKey(surface);

    private static string SurfaceForZone(string zoneId) => zoneId switch
    {
        "house_old_pc" => "interior_floor",
        "fap_clinic" => "interior_floor",
        "kara_urman_night" => "grass",
        "zirat_road" => "grass",
        _ => "wet_road"
    };

    private void LoadSurface(string surface)
    {
        var variants = new System.Collections.Generic.List<AudioStream>();
        for (var variant = 0; variant < 3; variant++)
        {
            var path = $"res://assets/audio/act1/footsteps/step_{surface}_{variant:00}.wav";
            if (ResourceLoader.Exists(path))
            {
                variants.Add(ResourceLoader.Load<AudioStream>(path));
            }
        }

        if (variants.Count > 0)
        {
            _bySurface[surface] = variants.ToArray();
        }
    }
}
