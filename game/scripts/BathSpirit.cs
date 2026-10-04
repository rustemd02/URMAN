using Godot;

namespace Urman.Godot;

/// <summary>
/// Мунча иясе, the owner of the bathhouse — redrawn after the author's
/// 2026-10-04 review (the old shape read as a friendly glowing-eyed gremlin).
/// The new presence is a tall, still, human-shaped silhouette built only from
/// the project's own low-poly primitives and painterly material families:
/// a dark hem like a wet robe, a too-wide shoulder shelf, a too-small
/// featureless head under wet strands of hair, and arms that hang past the
/// knees.  It has no eyes, no glow, no emission, no gore, no collision and no
/// interaction; it never blocks the player and never attacks.
///
/// It is only ever glimpsed in the washing-room corner behind the stove, at
/// the edge of the shielded wet lamp, in the steam.  It fades in once per
/// visit, only when the player is alone in the bath in dim light (night, or
/// the stove not burning), after a short delay, stays a few seconds with a
/// slow breathing cadence and one slow turn of the head, then fades out.  A
/// long cooldown prevents farming the scare.
///
/// Presentation only: no narrative state, no save data, no damage.  The class
/// exposes the same wiring contract as ForestEdgePresence:
/// <c>Initialize(Node3D world)</c> once and <c>Tick(delta, playerPosition,
/// audible)</c> from the owner's presentation tick.  The owner wires Tick; the
/// node itself is created by Act1ConnectedWorld.BuildBathAtmosphere.
/// </summary>
public partial class BathSpirit : Node3D
{
    public const int VoiceBudget = 2;
    public const float BathHalfWidth = 1.89f;
    public const float BathHalfDepth = 2.39f;
    public const float WetRoomLocalZ = .10f;
    public const float ArmDelayNightSeconds = 2.5f;
    public const float ArmDelayDimSeconds = 4.5f;
    public const float FadeInSeconds = 1.4f;
    public const float HoldNightSeconds = 5.2f;
    public const float HoldDimSeconds = 4.2f;
    public const float FadeOutSeconds = 1.1f;
    public const float CooldownSeconds = 165f;
    public const float BreathPeriodSeconds = 4.6f; // one breath cycle of the bed sample
    public const float BreathAmplitude = .02f;
    public const float HeadTurnStartSeconds = 1.1f;
    public const float HeadTurnSeconds = 1.6f;
    public const float CloseDissolveMeters = 1.6f;
    public const float NpcQuietRadiusMeters = 4.5f;
    public const float BedIdleDb = -31f;
    public const float BedActiveDb = -24f;
    public const float SilentDb = -80f;

    private enum Phase
    {
        Dormant,
        Arming,
        FadeIn,
        Hold,
        FadeOut,
        Cooldown,
    }

    private const string BedStreamPath = "res://assets/audio/act1/bath_spirit/spirit_breath_loop.wav";
    private const string ExhaleStreamPath = "res://assets/audio/act1/bath_spirit/spirit_exhale.wav";
    private const string WetStepStreamPath = "res://assets/audio/act1/bath_spirit/spirit_wet_step.wav";
    private const string CreakStreamPath = "res://assets/audio/act1/bath_spirit/spirit_creak.wav";

    private bool _initialized;
    private bool _headless;
    private Node3D? _world;
    private Node3D? _bathRoot;
    private Node3D _shoulders = null!;
    private Node3D _chest = null!;
    private Node3D _headPivot = null!;
    private MeshInstance3D[] _parts = [];
    private CpuParticles3D? _breathSteam;
    private OmniLight3D? _fireLight;
    private AudioStreamPlayer3D? _bedVoice;
    private AudioStreamPlayer3D? _cueVoice;
    private AudioStream? _bedStream;
    private AudioStream? _exhaleStream;
    private AudioStream? _wetStepStream;
    private AudioStream? _creakStream;

    private Vector3 _shouldersBase;
    private Vector3 _chestBase;

    private Phase _phase;
    private double _phaseTime;
    private double _breathClock;
    private double _cooldownLeft;
    private double _contextTimer;
    private ulong _lastTickMsec;
    private double _bedDb = SilentDb;
    private bool _visitSpent;
    private bool _alone = true;
    private bool _night;
    private bool _burning;
    private bool _cueCreakPlayed;
    private bool _cueExhalePlayed;
    private int _manifestations;
    private int _bedStarts;

    /// <summary>True while the short manifestation (fade in, hold, fade out) is running.</summary>
    public bool Manifesting => _phase is Phase.FadeIn or Phase.Hold or Phase.FadeOut;

    /// <summary>Completed manifestations this session; presentation telemetry only.</summary>
    public int Manifestations => _manifestations;

    /// <summary>Seconds of play time left before another manifestation may start.</summary>
    public double CooldownRemaining => _cooldownLeft;

    /// <summary>True once the geometry, materials and the two-voice pool exist.</summary>
    public bool IsReady => _initialized;

    /// <summary>Bed loop starts this session; diagnostics only.</summary>
    public int BedStarts => _bedStarts;

    /// <summary>
    /// Builds the silhouette, its steam breath and the shared two-voice pool.
    /// Idempotent; safe to call again by a later wiring owner.
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_initialized) return;
        _initialized = true;
        _world = world;
        _bathRoot = GetParent() as Node3D ?? world;
        _headless = DisplayServer.GetName() == "headless";
        var robe = PainterlyMaterialLibrary.ForColor("171310", "wood_furniture", sheltered: true);
        var skin = PainterlyMaterialLibrary.ForColor("3d362e", "wood_prop", sheltered: true);
        var hair = PainterlyMaterialLibrary.ForColor("0b0908", "wood_prop", sheltered: true);

        _parts = new MeshInstance3D[16];
        var partIndex = 0;
        MeshInstance3D Part(string name, Mesh mesh, Vector3 at, Material material, Vector3 rotation = default, Node3D? parent = null)
        {
            var part = new MeshInstance3D
            {
                Name = name,
                Mesh = mesh,
                Position = at,
                RotationDegrees = rotation,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Transparency = 1f
            };
            (parent ?? this).AddChild(part);
            _parts[partIndex++] = part;
            return part;
        }

        // A long wet hem instead of legs: wrong, robe-like, and it dissolves
        // into the floor shadow before the player can read a stance.
        Part("SpiritHem", new CylinderMesh { TopRadius = .20f, BottomRadius = .43f, Height = 1.30f, RadialSegments = 10, Rings = 1 },
            new(0, .65f, 0), robe);
        Part("SpiritTorso", new CylinderMesh { TopRadius = .17f, BottomRadius = .23f, Height = .64f, RadialSegments = 10, Rings = 1 },
            new(0, 1.56f, 0), robe);
        _chest = Part("SpiritChest", new BoxMesh { Size = new(.40f, .36f, .25f) }, new(0, 1.66f, .01f), robe);
        _shoulders = Part("SpiritShoulderShelf", new BoxMesh { Size = new(.66f, .15f, .22f) }, new(0, 1.92f, 0), robe);
        Part("SpiritNeck", new CylinderMesh { TopRadius = .048f, BottomRadius = .058f, Height = .20f, RadialSegments = 8 },
            new(0, 2.06f, 0), skin);
        _headPivot = new Node3D { Name = "SpiritHeadPivot", Position = new(0, 2.13f, 0) };
        AddChild(_headPivot);
        var head = new MeshInstance3D
        {
            Name = "SpiritHead",
            Mesh = new SphereMesh { Radius = .10f, Height = .20f, RadialSegments = 10, Rings = 6 },
            Position = new(0, .075f, .005f),
            Scale = new(.94f, 1.03f, .9f),
            MaterialOverride = skin,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transparency = 1f
        };
        _headPivot.AddChild(head);
        _parts[partIndex++] = head;
        // Wet hair hangs flat and uneven; the face stays an unreadable shadow.
        for (var strand = 0; strand < 6; strand++)
        {
            var angle = -1.25f + strand * .5f;
            var at = new Vector3(Mathf.Sin(angle) * .078f, .085f - (strand % 2) * .035f, Mathf.Cos(angle) * .062f - .01f);
            var strandMesh = new MeshInstance3D
            {
                Name = "SpiritHair" + strand,
                Mesh = new BoxMesh { Size = new(.026f, .34f, .012f) },
                Position = at,
                RotationDegrees = new(-7f + strand * 2f, 0, Mathf.Sin(angle) * 14f),
                MaterialOverride = hair,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Transparency = 1f
            };
            _headPivot.AddChild(strandMesh);
            _parts[partIndex++] = strandMesh;
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var suffix = side < 0 ? "Left" : "Right";
            Part("SpiritArm" + suffix, new BoxMesh { Size = new(.075f, .98f, .075f) },
                new(side * .38f, 1.42f, .04f), robe, new(4f, 0, side * 7f));
            Part("SpiritHand" + suffix, new BoxMesh { Size = new(.10f, .19f, .08f) },
                new(side * .43f, .86f, .06f), skin, new(6f, 0, side * 4f));
        }
        _shouldersBase = _shoulders.Position;
        _chestBase = _chest.Position;

        _breathSteam = BuildBreathSteam();
        SetMeta("presentationOnly", true);
        SetMeta("folklore", "мунча иясе: owner of the bathhouse, lives behind the stove (Proposal, mythology.md)");
        SetMeta("redrawReason", "author 2026-10-04: make it scarier and less obvious; no friendly glowing eyes");
        SetMeta("manifestationRules", "once per visit, alone, dim/night, fade 1.4s, hold 4.2-5.2s, fade 1.1s, cooldown 165s");
        SetMeta("redrawnParts", partIndex);
        SetMeta("voiceBudget", VoiceBudget);
        Visible = false;
        BuildVoices();
        _fireLight = _bathRoot.GetNodeOrNull<OmniLight3D>("BathFireLight");
    }

    /// <summary>
    /// One presentation frame from the owner's tick.  The player position is
    /// world-space; audible is the owner's zone/pause gate.  The spirit itself
    /// additionally requires the player to be inside the bath volume.
    /// </summary>
    public void Tick(double delta, Vector3 playerPosition, bool audible)
    {
        if (!_initialized) return;
        var step = (float)Mathf.Clamp(delta, 0, .1);
        // A later owner wiring Tick a second time cannot double-drive the
        // state machine: each accepted call advances by no more than the real
        // time since the previous one, so a same-frame duplicate is a no-op.
        var nowMsec = Time.GetTicksMsec();
        if (_lastTickMsec != 0)
            step = (float)Mathf.Clamp((nowMsec - _lastTickMsec) / 1000.0, 0, step);
        _lastTickMsec = nowMsec;
        var bath = _bathRoot;
        if (bath is null || !IsInstanceValid(bath)) return;
        var local = bath.ToLocal(playerPosition);
        var inside = Mathf.Abs(local.X) < BathHalfWidth && Mathf.Abs(local.Z) < BathHalfDepth
            && local.Y > -.25f && local.Y < 2.55f;
        var wetRoom = inside && local.Z < WetRoomLocalZ;

        _contextTimer += step;
        if (_contextTimer >= 1.0)
        {
            _contextTimer = 0;
            RefreshContext(inside);
        }

        var dim = !_burning;
        var bedAllowed = inside && audible && (dim || _night);
        if (!audible)
        {
            UpdateBed(step, false, false);
            return;
        }
        _cooldownLeft = Mathf.Max(0, _cooldownLeft - step);
        UpdateBed(step, bedAllowed, Manifesting);

        if (!inside && _phase == Phase.Arming) _phase = Phase.Dormant;
        if (!inside && Manifesting && _phase != Phase.FadeOut) StartFadeOut();
        if (!inside) _visitSpent = false;

        switch (_phase)
        {
            case Phase.Dormant:
                if (wetRoom && audible && bedAllowed && _alone && !_visitSpent && _cooldownLeft <= 0)
                {
                    _phase = Phase.Arming;
                    _phaseTime = 0;
                }
                break;
            case Phase.Arming:
                if (!wetRoom || !_alone || !bedAllowed) { _phase = Phase.Dormant; break; }
                _phaseTime += step;
                if (_phaseTime >= (_night ? ArmDelayNightSeconds : ArmDelayDimSeconds))
                    BeginManifestation();
                break;
            case Phase.FadeIn:
                _phaseTime += step;
                _breathClock += step;
                var fadeLevel = Mathf.SmoothStep(0f, 1f, (float)(_phaseTime / FadeInSeconds));
                ApplyLevel(fadeLevel);
                Breathe();
                if (_phaseTime >= FadeInSeconds) { _phase = Phase.Hold; _phaseTime = 0; }
                if (DistanceToPlayer(playerPosition) < CloseDissolveMeters) StartFadeOut();
                break;
            case Phase.Hold:
                _phaseTime += step;
                _breathClock += step;
                ApplyLevel(1f);
                Breathe();
                TurnHead((float)_phaseTime);
                if (_phaseTime >= 1.4 && !_cueCreakPlayed)
                {
                    _cueCreakPlayed = true;
                    PlayCue(_creakStream, .96f, -17f);
                }
                if (_phaseTime >= (_night ? HoldNightSeconds : HoldDimSeconds)) StartFadeOut();
                if (DistanceToPlayer(playerPosition) < CloseDissolveMeters) StartFadeOut();
                break;
            case Phase.FadeOut:
                _phaseTime += step;
                var remaining = Mathf.SmoothStep(0f, 1f, (float)(_phaseTime / FadeOutSeconds));
                ApplyLevel(1f - remaining);
                Breathe();
                if (_phaseTime >= FadeOutSeconds)
                {
                    FinishManifestation();
                }
                break;
            case Phase.Cooldown:
                if (_cooldownLeft <= 0) _phase = Phase.Dormant;
                break;
        }
    }

    private void BeginManifestation()
    {
        _phase = Phase.FadeIn;
        _phaseTime = 0;
        _breathClock = 0;
        _visitSpent = true;
        _cueCreakPlayed = false;
        _cueExhalePlayed = false;
        Visible = true;
        ApplyLevel(0f);
        if (_breathSteam is not null) { _breathSteam.Visible = true; _breathSteam.Emitting = true; }
        PlayCue(_wetStepStream, .90f, -16f);
    }

    private void StartFadeOut()
    {
        if (_phase == Phase.FadeOut) return;
        _phase = Phase.FadeOut;
        _phaseTime = 0;
        if (!_cueExhalePlayed)
        {
            _cueExhalePlayed = true;
            PlayCue(_exhaleStream, .88f, -15f);
        }
    }

    private void FinishManifestation()
    {
        _phase = Phase.Cooldown;
        _phaseTime = 0;
        _cooldownLeft = CooldownSeconds;
        _manifestations++;
        Visible = false;
        ResetPose();
        if (_breathSteam is not null) { _breathSteam.Emitting = false; _breathSteam.Visible = false; }
        SetMeta("manifestations", _manifestations);
        SetMeta("lastManifestationReason", _night ? "night" : "dim-lamp");
    }

    /// <summary>Refreshes the 1 Hz context with bounded, non-per-frame work.</summary>
    private void RefreshContext(bool inside)
    {
        if (_world is not null && IsInstanceValid(_world))
        {
            var zone = _world.GetMeta("activeZoneId", "").AsString();
            _night = zone == "kara_urman_night";
        }
        _burning = _fireLight is { Visible: true };
        if (inside && _phase == Phase.Dormant) _alone = NoNpcNearBath();
    }

    /// <summary>
    /// "Alone" means no staged NPC stands within a few metres of the banya.
    /// Runs at most once per second while the player is inside and dormant.
    /// </summary>
    private bool NoNpcNearBath()
    {
        if (_world is null || !IsInstanceValid(_world) || _bathRoot is null) return true;
        var centre = _bathRoot.GlobalPosition;
        foreach (var node in _world.FindChildren("Npc_*", "Node3D", true, false))
        {
            if (node is Node3D npc && npc.IsVisibleInTree() && npc.GlobalPosition.DistanceTo(centre) < NpcQuietRadiusMeters)
                return false;
        }
        return true;
    }

    private float DistanceToPlayer(Vector3 playerPosition) => GlobalPosition.DistanceTo(playerPosition);

    private void Breathe()
    {
        var breath = .5f + .5f * Mathf.Sin((float)_breathClock * Mathf.Tau / BreathPeriodSeconds);
        _chest.Scale = new Vector3(1f + BreathAmplitude * breath, 1f, 1f + BreathAmplitude * .6f * breath);
        _chest.Position = _chestBase + new Vector3(0, .004f * breath, 0);
        _shoulders.Position = _shouldersBase + new Vector3(0, .006f * breath, 0);
    }

    private void TurnHead(float holdSeconds)
    {
        var progress = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp((holdSeconds - HeadTurnStartSeconds) / HeadTurnSeconds, 0f, 1f));
        _headPivot.Rotation = new Vector3(0, .30f * progress, 0);
    }

    private void ResetPose()
    {
        _chest.Scale = Vector3.One;
        _chest.Position = _chestBase;
        _shoulders.Position = _shouldersBase;
        _headPivot.Rotation = Vector3.Zero;
    }

    private void ApplyLevel(float level)
    {
        var transparency = 1f - Mathf.Clamp(level, 0f, 1f);
        for (var index = 0; index < _parts.Length; index++) _parts[index].Transparency = transparency;
    }

    private CpuParticles3D BuildBreathSteam()
    {
        var radial = new GradientTexture2D
        {
            Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 1),
            Gradient = new Gradient { Colors = [Colors.White, new Color(1, 1, 1, 0)], Offsets = [0, 1] }
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(.78f, .79f, .76f, .10f),
            AlbedoTexture = radial,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1
        };
        var scale = new Curve();
        scale.AddPoint(new(.0f, .5f));
        scale.AddPoint(new(1f, 1.5f));
        var particles = new CpuParticles3D
        {
            Name = "SpiritBreathSteam",
            Position = new(0, 1.95f, .06f),
            Emitting = false,
            Amount = 10,
            Lifetime = 3.0f,
            Mesh = new QuadMesh { Size = new(.16f, .16f), Material = material },
            LocalCoords = true,
            Direction = Vector3.Up,
            Spread = 9,
            Gravity = new(0, .012f, 0),
            InitialVelocityMin = .10f,
            InitialVelocityMax = .18f,
            ScaleAmountMin = .7f,
            ScaleAmountMax = 1.05f,
            ScaleAmountCurve = scale,
            ColorRamp = new Gradient
            {
                Offsets = [0, .2f, .7f, 1],
                Colors = [new Color(1, 1, 1, 0), Colors.White, Colors.White, new Color(1, 1, 1, 0)]
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        AddChild(particles);
        return particles;
    }

    private void BuildVoices()
    {
        AudioSettingsService.EnsureBuses();
        _bedStream = LoadLoop(BedStreamPath);
        _exhaleStream = LoadOneShot(ExhaleStreamPath);
        _wetStepStream = LoadOneShot(WetStepStreamPath);
        _creakStream = LoadOneShot(CreakStreamPath);
        var bedVoice = new AudioStreamPlayer3D
        {
            Name = "SpiritBedVoice",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = SilentDb,
            MaxDistance = 9f,
            UnitSize = 2.4f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 5200f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1,
            Stream = _bedStream
        };
        var cueVoice = new AudioStreamPlayer3D
        {
            Name = "SpiritCueVoice",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = -16f,
            MaxDistance = 10f,
            UnitSize = 2.0f,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = 5600f,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1
        };
        AddChild(bedVoice);
        AddChild(cueVoice);
        _bedVoice = bedVoice;
        _cueVoice = cueVoice;
        SetMeta("audioPool", "2 x AudioStreamPlayer3D on SFX: SpiritBedVoice (loop) + SpiritCueVoice (one-shot)");
        SetMeta("audioReady", _bedStream is not null);
    }

    private static AudioStream? LoadLoop(string path)
    {
        var stream = LoadStream(path);
        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        }
        return stream;
    }

    private static AudioStream? LoadOneShot(string path)
    {
        var stream = LoadStream(path);
        if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        }
        return stream;
    }

    private static AudioStream? LoadStream(string path)
    {
        // Ignore the cache: the loop flag is written into the resource, and the
        // four files are unique to the spirit, so no other owner can be affected.
        return ResourceLoader.Exists(path)
            ? ResourceLoader.Load<AudioStream>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
            : null;
    }

    private void PlayCue(AudioStream? stream, float pitch, float volumeDb)
    {
        if (_headless || stream is null || _cueVoice is null) return;
        _cueVoice.Stop();
        _cueVoice.Stream = stream;
        _cueVoice.PitchScale = pitch;
        _cueVoice.VolumeDb = volumeDb;
        _cueVoice.Play();
    }

    private void UpdateBed(float step, bool allowed, bool manifesting)
    {
        if (_bedStream is null || _bedVoice is null || _headless) return;
        var target = allowed ? (manifesting ? BedActiveDb : BedIdleDb) : SilentDb;
        _bedDb = Mathf.MoveToward(_bedDb, target, step * 14f);
        _bedVoice.VolumeDb = (float)_bedDb;
        if (allowed)
        {
            if (!_bedVoice.Playing)
            {
                _bedDb = SilentDb;
                _bedVoice.VolumeDb = SilentDb;
                _bedVoice.Play();
                _bedStarts++;
            }
            return;
        }
        if (_bedVoice.Playing && _bedDb <= SilentDb + .5f) _bedVoice.Stop();
    }
}
