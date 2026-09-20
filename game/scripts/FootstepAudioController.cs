using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// AUDIO-004: presentation-only footstep cadence driven by the player's real
/// XZ displacement per rendered frame. Holds no gameplay state — surface
/// selection follows the actual floor contact, then the zone/space fallback;
/// steps use SFX and reduced motion does not mute sound.
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

    /// <summary>Metres of travel between steps (walk cadence). Half the
    /// visible body's stride cycle, so a heard step and a planted foot agree.</summary>
    public float StepIntervalMeters { get; set; } = FirstPersonController.GaitCycleMeters * .5f;

    /// <summary>Last resolved surface, exposed for the existing smoke contract.</summary>
    internal string LastSurface { get; private set; } = string.Empty;

    /// <summary>Total real PlayStep triggers, for cadence diagnostics.</summary>
    internal int StepTriggerCount { get; private set; }

    public override void _Ready()
    {
        AddToGroup("footstep_audio");
        AudioSettingsService.EnsureBuses();
        LoadSurface("snow_packed");
        LoadSurface("snow_soft");
        LoadSurface("wood");
        LoadSurface("interior_floor");
        LoadSurface("grass");
        LoadSurface("mud");
        LoadSurface("wet_road");
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

    public override void _Process(double delta)
    {
        // Frame time, not physics ticks: when the frame rate drops (dense
        // forest off-road), several physics ticks run inside one frame and a
        // tick-driven cadence would bunch steps into the same audible moment.
        // Accumulating real displacement per rendered frame keeps every step
        // evenly spaced in wall time even during hitches.
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
        if (!_actor.IsOnFloor() || _actor.ModalOpen || _actor.IsClimbingLadder || _actor.VehicleControlled)
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
        LastSurface = SurfaceUnderfoot(bridge?.CurrentZoneId ?? "village_day", position);
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
        StepTriggerCount++;
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

    private string SurfaceUnderfoot(string zoneId, Vector2 position)
    {
        // A board, crate or loft can stand above snow at the same XZ. The
        // active slide contact is the support the player actually stepped on;
        // a nearby wall or a carried item must not change the footstep family.
        if (_actor is not null)
        {
            for (var index = 0; index < _actor.GetSlideCollisionCount(); index++)
            {
                var contact = _actor.GetSlideCollision(index);
                if (contact.GetNormal().Dot(_actor.UpDirection) < Mathf.Cos(_actor.FloorMaxAngle)
                    || contact.GetCollider() is not Node support
                    || !support.HasMeta("footstepSurface")) continue;
                var surface = support.GetMeta("footstepSurface").AsString();
                if (_bySurface.ContainsKey(surface)) return surface;
            }

            // Floor snapping can support a standing or tangentially moving
            // capsule without adding a slide collision. Resolve that support
            // at the actual feet, once per step, rather than reverting a wood
            // platform to the snow family underneath its XZ.
            using var query = PhysicsRayQueryParameters3D.Create(
                _actor.GlobalPosition + _actor.UpDirection * .03f,
                _actor.GlobalPosition - _actor.UpDirection * .08f,
                _actor.CollisionMask,
                new global::Godot.Collections.Array<Rid> { _actor.GetRid() });
            var hit = _actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count > 0
                && hit["normal"].AsVector3().Dot(_actor.UpDirection) >= Mathf.Cos(_actor.FloorMaxAngle)
                && hit["collider"].AsGodotObject() is Node floor && floor.HasMeta("footstepSurface"))
            {
                var surface = floor.GetMeta("footstepSurface").AsString();
                if (_bySurface.ContainsKey(surface)) return surface;
            }
        }
        return SurfaceForZone(zoneId, position);
    }

    private static string SurfaceForZone(string zoneId, Vector2 position) => zoneId switch
    {
        "house_old_pc" => "wood",
        "fap_clinic" => "interior_floor",
        _ => SurfaceForExterior(position)
    };

    private static string SurfaceForExterior(Vector2 position)
    {
        var road = AgentBAct1HeightField.RoadInfo(position.X, position.Y);
        if (road.Distance < road.HalfWidth) return "snow_packed";
        // Off-road winter ground is not one sound: trampled snow near the
        // road edge, soft snow further out, and exposed mud where the thaw
        // or traffic wore through. The physics road frame already measures
        // distance from the travelled surface, so reuse it as the selector.
        var off = road.Distance - road.HalfWidth;
        if (off < .6f) return "snow_packed";
        if (off < 2.2f) return "snow_soft";
        return "mud";
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
