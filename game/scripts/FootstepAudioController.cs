using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// AUDIO-004: presentation-only footstep cadence driven by the player's real
/// XZ displacement. Holds no gameplay state — surface selection is a pure
/// zone/space mapping, steps play through the SFX bus, and reduced motion does
/// not mute sound.
/// </summary>
public partial class FootstepAudioController : Node
{
    private FirstPersonController? _actor;
    private AudioStreamPlayer? _stepPlayer;
    private readonly System.Collections.Generic.Dictionary<string, AudioStreamRandomizer> _bySurface = new();
    private float _distanceSinceStep;
    private Vector2? _lastPosition;
    private int _transformRevision = -1;
    private string? _activeSurface;

    /// <summary>Metres of travel between steps (walk cadence).</summary>
    public float StepIntervalMeters { get; set; } = .55f;

    /// <summary>Last resolved surface, exposed for the existing smoke contract.</summary>
    internal string LastSurface { get; private set; } = string.Empty;

    public override void _Ready()
    {
        AddToGroup("footstep_audio");
        AudioSettingsService.EnsureBuses();
        LoadSurface("snow_packed");
        LoadSurface("snow_soft");
        LoadSurface("wood");
        LoadSurface("interior_floor");
        _stepPlayer = new AudioStreamPlayer
        {
            Name = "FootstepPlayer",
            Bus = AudioSettingsService.SfxBus,
            MaxPolyphony = 4
        };
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
        _activeSurface = null;
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

        if (_transformRevision != _actor.PresentationTransformRevision)
        {
            _transformRevision = _actor.PresentationTransformRevision;
            ResetStep();
            return;
        }

        var position = new Vector2(_actor.GlobalPosition.X, _actor.GlobalPosition.Z);
        if (!_actor.IsOnFloor() || _actor.ModalOpen)
        {
            ResetStep();
            return;
        }

        if (_lastPosition is not Vector2 previous)
        {
            _lastPosition = position;
            return;
        }

        _lastPosition = position;
        var movement = position - previous;
        var distance = movement.Length();
        // Match SnowTrampleField: do not turn a direct spawn/debug teleport
        // into a burst of presentation cues.
        if (distance > Mathf.Max(.5f, _actor.Velocity.Length() * (float)delta * 2f + .1f))
        {
            ResetStep();
            return;
        }

        if (distance < .0001f)
        {
            return;
        }

        _distanceSinceStep += distance;
        var interval = Mathf.Max(.01f, StepIntervalMeters);
        if (_distanceSinceStep < interval)
        {
            return;
        }

        _distanceSinceStep %= interval;
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        LastSurface = SurfaceForZone(bridge?.CurrentZoneId ?? "village_day", position);
        PlayStep(LastSurface);
    }

    private void ResetStep()
    {
        _lastPosition = null;
        _distanceSinceStep = 0f;
    }

    /// <summary>Test/debug hook: play one step of the given surface now.</summary>
    public void PlayStep(string surface)
    {
        LastSurface = surface;
        if (_stepPlayer is null
            || !_bySurface.TryGetValue(surface, out var randomizer)
            || randomizer.StreamsCount == 0)
        {
            return;
        }

        if (_activeSurface != surface)
        {
            _stepPlayer.Stream = randomizer;
            _activeSurface = surface;
        }

        _stepPlayer.Play();
    }

    public bool HasSurface(string surface) => _bySurface.ContainsKey(surface);

    private static string SurfaceForZone(string zoneId, Vector2 position) => zoneId switch
    {
        "house_old_pc" => "wood",
        "fap_clinic" => "interior_floor",
        _ => SurfaceForExterior(position)
    };

    private static string SurfaceForExterior(Vector2 position)
    {
        var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
        return road.Distance < road.HalfWidth ? "snow_packed" : "snow_soft";
    }

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
            var randomizer = new AudioStreamRandomizer
            {
                PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,
                RandomPitch = 1.06f
            };
            for (var index = 0; index < variants.Count; index++)
            {
                randomizer.AddStream(index, variants[index], 1f);
            }

            _bySurface[surface] = randomizer;
        }
    }
}
