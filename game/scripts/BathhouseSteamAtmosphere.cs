using Godot;

namespace Urman.Godot;

/// <summary>
/// Old-banya interior atmosphere after the author's 2026-10-04 request: make
/// the bath so atmospheric that you can almost smell it.  Three bounded,
/// presentation-only layers sit on top of the existing bath state:
///
/// * Steam: a low-count emitter over the stove stones, a thinner one in the
///   washing-room corner above the basin, and a short one-shot puff at the wet
///   door when it opens, when the player steps into the steam room, or when
///   the already-existing "Поддать воды" pour state rises.  All three share
///   one material, one quad, one scale curve and one colour ramp.
/// * Light and haze: a cheap two-quad translucent haze inside the wet room
///   only (never a global WorldEnvironment change) and a subtle heat flicker
///   on the existing BathShieldedWetLamp.  The мунча иясе and its corner stay
///   outside the haze quads so the spirit keeps its readability.
/// * Sound: exactly three AudioStreamPlayer3D voices on SFX, distance-culled
///   and silent whenever the player is outside the bath — the room loop (CC0
///   steam hiss + reused bank stove crackle + two dry creaks), the tub drip
///   loop, and a one-shot cue voice (water scoop, steam puff, reused bank
///   stove_kindling on ignition).
///
/// CPU emitters, not GPU: on the author's M1 Low preset the project uses
/// CPUParticles3D for every plume (see VillageChimneySmoke).  Godot 4 CPU
/// particles have no ParticleProcessMaterial, so the shared-resource budget
/// goal is met with the one StandardMaterial3D + QuadMesh + Curve + Gradient
/// shared by all three emitters.
///
/// Wiring contract (the owner calls it): <c>Initialize(Node3D world)</c> once
/// after adding this node under the bath house, then
/// <c>Tick(delta, playerPosition, audible)</c> from the ordinary presentation
/// tick.  <c>audible</c> is the owner's zone/pause gate.  A duplicate same-frame
/// Tick is a no-op thanks to the real-time step clamp, like BathSpirit.
/// Presentation only: no narrative state, no save data, no collision, and no
/// change to the existing stove/water/vent interactions.
/// </summary>
public partial class BathhouseSteamAtmosphere : Node3D
{
    public const int StoneSteamAmount = 6;
    public const int CornerSteamAmount = 5;
    public const int BurstAmount = 8;
    public const int EmitterCount = 3;
    public const int ParticleBudget = StoneSteamAmount + CornerSteamAmount + BurstAmount;
    public const int HazeQuadCount = 2;
    public const int VoiceBudget = 3;
    public const float VisualRangeMeters = 9f;
    public const float InsideHalfWidth = 2.05f;
    public const float InsideHalfDepth = 2.6f;
    public const float WetRoomLocalZ = .12f;
    public const float BedDbInside = -22f;
    public const float BedDbFire = -19.5f;
    public const float DripDbInside = -24f;
    public const float CueDbScoop = -15f;
    public const float CueDbBurst = -25f;
    public const float CueDbKindling = -17f;
    public const float SilentDb = -80f;
    public const float FadeDbPerSecond = 26f;
    public const float SteamHazeAlpha = .05f;
    public const float SteamyHazeAlpha = .085f;

    private const string BedStreamPath = "res://assets/audio/act1/banya/banya_room_loop.wav";
    private const string DripStreamPath = "res://assets/audio/act1/banya/drip_loop.wav";
    private const string ScoopStreamPath = "res://assets/audio/act1/banya/water_scoop.wav";
    private const string BurstStreamPath = "res://assets/audio/act1/banya/steam_burst.wav";
    private const string KindlingStreamPath = "res://assets/audio/act1/sound_mood/stove_kindling.wav";

    private static readonly Vector3 StoneAnchorLocal = new(-1.33f, 1.10f, -1.09f);
    private static readonly Vector3 CornerAnchorLocal = new(1.50f, 1.30f, -2.02f);
    private static readonly Vector3 BurstAnchorLocal = new(.06f, 1.06f, .10f);
    private static readonly Vector3 BedAnchorLocal = new(0f, 1.5f, .5f);
    private static readonly Vector3 DripAnchorLocal = new(1.62f, .55f, -.08f);
    private static readonly Vector3 StoveCueAnchorLocal = new(-1.33f, .7f, -.8f);
    private static readonly Vector3 DoorCueAnchorLocal = new(.2f, 1.2f, .1f);
    private static readonly Vector3 TubCueAnchorLocal = new(1.62f, .62f, -.08f);

    private bool _initialized;
    private bool _headless;
    private Node3D? _bath;
    private Node3D? _wetDoorHinge;
    private OmniLight3D? _wetLamp;
    private float _wetLampBaseEnergy = .28f;

    private CpuParticles3D? _stoneSteam;
    private CpuParticles3D? _cornerSteam;
    private CpuParticles3D? _steamBurst;
    private CpuParticles3D?[] _emitters = [];
    private MeshInstance3D? _hazeLower;
    private MeshInstance3D? _hazeUpper;
    private StandardMaterial3D? _hazeMaterial;

    private AudioStreamPlayer3D? _bedVoice;
    private AudioStreamPlayer3D? _dripVoice;
    private AudioStreamPlayer3D? _cueVoice;
    private AudioStream? _bedStream;
    private AudioStream? _dripStream;
    private AudioStream? _scoopStream;
    private AudioStream? _burstStream;
    private AudioStream? _kindlingStream;
    private float _bedDb = SilentDb;
    private float _dripDb = SilentDb;

    private Vector3 _bedGlobal;
    private Vector3 _dripGlobal;
    private Vector3 _stoveCueGlobal;
    private Vector3 _doorCueGlobal;
    private Vector3 _tubCueGlobal;

    private bool _present;
    private bool _prevInside;
    private bool _prevWetRoom;
    private bool _prevDoorOpen;
    private bool _prevSteamy;
    private int _bursts;
    private double _time;
    private ulong _lastTickMsec;

    /// <summary>Owner push: true while the poured-water steam window is open.</summary>
    public bool Steamy { get; set; }

    /// <summary>Owner push: true while the stove fire is burning.</summary>
    public bool FireBurning { get; set; }

    /// <summary>True once the bath root, the emitters and the three voices exist.</summary>
    public bool IsReady => _initialized;

    /// <summary>One-shot steam puffs triggered this session; diagnostics only.</summary>
    public int Bursts => _bursts;

    /// <summary>
    /// Locates the bath house, builds the shared steam resources, the haze
    /// quads, the lamp reference and the three-voice pool.  Idempotent; safe
    /// to call again by a later wiring owner.
    /// </summary>
    public void Initialize(Node3D world)
    {
        if (_initialized) return;
        _initialized = true;
        _headless = DisplayServer.GetName() == "headless";
        var parent = GetParent() as Node3D;
        _bath = parent is not null
            && (parent.Name == "BabaiBathhouse" || parent.GetNodeOrNull<Node3D>("BathStoveFirebox") is not null)
            ? parent
            : world.FindChild("BabaiBathhouse", true, false) as Node3D;
        if (_bath is null)
        {
            SetMeta("banyaAtmosphereError", "BabaiBathhouse was not found in the world");
            return;
        }

        _wetDoorHinge = _bath.GetNodeOrNull<Node3D>("BathWetRoomDoorHinge");
        _wetLamp = _bath.GetNodeOrNull<OmniLight3D>("BathShieldedWetLamp");
        _wetLampBaseEnergy = _wetLamp?.LightEnergy ?? .28f;
        _bedGlobal = _bath.ToGlobal(BedAnchorLocal);
        _dripGlobal = _bath.ToGlobal(DripAnchorLocal);
        _stoveCueGlobal = _bath.ToGlobal(StoveCueAnchorLocal);
        _doorCueGlobal = _bath.ToGlobal(DoorCueAnchorLocal);
        _tubCueGlobal = _bath.ToGlobal(TubCueAnchorLocal);
        _prevDoorOpen = WetDoorOpen();

        BuildSteam();
        BuildHaze();
        BuildVoices();

        SetMeta("presentationOnly", true);
        SetMeta("banyaSteamEmitters", EmitterCount);
        SetMeta("banyaSteamParticles", ParticleBudget);
        SetMeta("banyaHazeQuads", HazeQuadCount);
        SetMeta("banyaVoiceBudget", VoiceBudget);
        SetMeta("banyaSoundBed", "CC0 steam hiss + reused bank stove crackle + dry creaks; drip loop; scoop/burst/kindling cues");
        SetMeta("banyaAtmosphereReady", _bath is not null);
        GD.Print($"banya-atmosphere: emitters={EmitterCount} particles={ParticleBudget} hazeQuads={HazeQuadCount} voices={VoiceBudget} ready={IsReady}");
    }

    /// <summary>
    /// One presentation frame from the owner's tick.  The player position is
    /// world-space; audible is the owner's zone/pause gate.  The atmosphere
    /// additionally requires the player to be inside the bath volume before
    /// any voice may start, so the bed is silent outside the banya.
    /// </summary>
    public void Tick(double delta, Vector3 playerPosition, bool audible)
    {
        if (!_initialized || _bath is null || !IsInstanceValid(_bath)) return;
        var step = (float)Mathf.Clamp(delta, 0, .1);
        var nowMsec = Time.GetTicksMsec();
        if (_lastTickMsec != 0)
            step = (float)Mathf.Clamp((nowMsec - _lastTickMsec) / 1000.0, 0, step);
        _lastTickMsec = nowMsec;
        _time += step;

        var local = _bath.ToLocal(playerPosition);
        var inside = Mathf.Abs(local.X) < InsideHalfWidth && Mathf.Abs(local.Z) < InsideHalfDepth
            && local.Y > -.35f && local.Y < 2.7f;
        var wetRoom = inside && local.Z < WetRoomLocalZ;
        var visible = audible
            && _bath.GlobalPosition.DistanceSquaredTo(playerPosition) < VisualRangeMeters * VisualRangeMeters;
        var steamyRising = Steamy && !_prevSteamy;

        UpdateSteam(step, visible);
        UpdateTriggers(inside, wetRoom, audible, steamyRising);
        UpdateHaze(visible);
        UpdateLamp(visible);
        UpdateAudio(step, audible && inside);

        _prevInside = inside;
        _prevWetRoom = wetRoom;
        _prevSteamy = Steamy;
    }

    /// <summary>Water-scoop cue for the presentation-only "Зачерпнуть воды" target.</summary>
    public void PlayWaterScoop()
    {
        if (PausedAtListener()) return;
        PlayCue(_scoopStream, CueDbScoop, _tubCueGlobal);
    }

    /// <summary>Reuses the bank stove_kindling.wav on a successful stove ignition.</summary>
    public void PlayStoveKindling()
    {
        if (PausedAtListener()) return;
        PlayCue(_kindlingStream, CueDbKindling, _stoveCueGlobal);
    }

    private void BuildSteam()
    {
        var radial = new GradientTexture2D
        {
            Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 1f),
            Gradient = new Gradient { Colors = [Colors.White, new Color(1, 1, 1, 0)], Offsets = [0f, 1f] }
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(.83f, .83f, .78f, .15f), AlbedoTexture = radial,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1f
        };
        var quad = new QuadMesh { Size = new(.20f, .20f), Material = material };
        var scale = new Curve();
        scale.AddPoint(new(.0f, .30f));
        scale.AddPoint(new(1f, 1.70f));
        var ramp = new Gradient
        {
            Offsets = [0f, .18f, .70f, 1f],
            Colors = [new Color(1, 1, 1, 0), Colors.White, Colors.White, new Color(1, 1, 1, 0)]
        };
        _stoneSteam = MakeSteam("BathSteamOverStones", StoneAnchorLocal, StoneSteamAmount, 3.0f, 1.4f, 11f, .18f, .30f, .014f, quad, scale, ramp);
        _cornerSteam = MakeSteam("BathSteamWashCorner", CornerAnchorLocal, CornerSteamAmount, 3.6f, 1.8f, 15f, .12f, .22f, .010f, quad, scale, ramp);
        _steamBurst = MakeSteam("BathSteamDoorBurst", BurstAnchorLocal, BurstAmount, 1.6f, 0f, 26f, .55f, .95f, .020f, quad, scale, ramp);
        _steamBurst.OneShot = true;
        _steamBurst.Explosiveness = .9f;
        _emitters = [_stoneSteam, _cornerSteam, _steamBurst];
        _hazeMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.66f, .64f, .58f, SteamHazeAlpha), AlbedoTexture = radial,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1f
        };
    }

    private CpuParticles3D MakeSteam(string name, Vector3 anchorLocal, int amount, float lifetime, float preprocess,
        float spread, float velocityMin, float velocityMax, float gravityUp,
        QuadMesh quad, Curve scale, Gradient ramp)
    {
        var emitter = new CpuParticles3D
        {
            Name = name,
            Position = AnchorPosition(anchorLocal),
            Emitting = false,
            Visible = false,
            Amount = amount,
            Lifetime = lifetime,
            Preprocess = preprocess,
            Mesh = quad,
            LocalCoords = true,
            Direction = Vector3.Up,
            Spread = spread,
            Gravity = new(0, gravityUp, 0),
            InitialVelocityMin = velocityMin,
            InitialVelocityMax = velocityMax,
            ScaleAmountMin = .8f,
            ScaleAmountMax = 1.1f,
            ScaleAmountCurve = scale,
            ColorRamp = ramp,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(emitter);
        return emitter;
    }

    private void BuildHaze()
    {
        // Two translucent quads, wet room only.  They stay clear of the
        // spirit's corner (X -1.66, Z -2.02) so the мунча иясе is not veiled.
        // Both quads end at Z <= .25, short of the partition face at .28, so
        // no haze edge ever pokes through the doorway into the changing room.
        _hazeLower = MakeHaze("BathWetHazeLower", new(.35f, 1.55f, -.95f), new Vector2(2.9f, 2.4f));
        _hazeUpper = MakeHaze("BathWetHazeUpper", new(.35f, 2.02f, -.75f), new Vector2(2.4f, 1.9f));
    }

    private MeshInstance3D MakeHaze(string name, Vector3 at, Vector2 size)
    {
        var quad = new MeshInstance3D
        {
            Name = name,
            Position = AnchorPosition(at),
            Mesh = new PlaneMesh { Size = size },
            MaterialOverride = _hazeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        quad.SetMeta("visualOnly", true);
        AddChild(quad);
        return quad;
    }

    private void BuildVoices()
    {
        AudioSettingsService.EnsureBuses();
        _bedStream = LoadLoop(BedStreamPath);
        _dripStream = LoadLoop(DripStreamPath);
        _scoopStream = LoadOneShot(ScoopStreamPath);
        _burstStream = LoadOneShot(BurstStreamPath);
        _kindlingStream = LoadOneShot(KindlingStreamPath);
        _bedVoice = MakeVoice("BanyaBedVoice", _bedStream, 11f, 3.4f, 4800f);
        _dripVoice = MakeVoice("BanyaDripVoice", _dripStream, 7f, 1.3f, 6200f);
        _cueVoice = MakeVoice("BanyaCueVoice", null, 9f, 1.6f, 6400f);
        _bedVoice.GlobalPosition = _bedGlobal;
        _dripVoice.GlobalPosition = _dripGlobal;
        _cueVoice.GlobalPosition = _doorCueGlobal;
        SetMeta("banyaAudioReady",
            _bedStream is not null && _dripStream is not null && _scoopStream is not null && _burstStream is not null);
        SetMeta("banyaAudioPaths", string.Join("; ", BedStreamPath, DripStreamPath, ScoopStreamPath, BurstStreamPath, KindlingStreamPath));
        SetMeta("banyaListening", "external/not-run");
    }

    private AudioStreamPlayer3D MakeVoice(string name, AudioStream? stream, float maxDistance, float unitSize, float cutoff)
    {
        var voice = new AudioStreamPlayer3D
        {
            Name = name,
            Stream = stream,
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = SilentDb,
            MaxDistance = maxDistance,
            UnitSize = unitSize,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            AttenuationFilterCutoffHz = cutoff,
            AttenuationFilterDb = -6f,
            MaxPolyphony = 1
        };
        AddChild(voice);
        return voice;
    }

    private void UpdateSteam(float step, bool visible)
    {
        if (visible != _present)
        {
            _present = visible;
            foreach (var emitter in _emitters)
            {
                if (emitter is null) continue;
                emitter.Visible = visible;
                if (!visible) emitter.Emitting = false;
            }
            if (visible)
            {
                if (_stoneSteam is not null) _stoneSteam.Emitting = true;
                if (_cornerSteam is not null) _cornerSteam.Emitting = true;
            }
        }
        if (Steamy != _prevSteamy)
        {
            if (_stoneSteam is not null) _stoneSteam.Amount = Steamy ? StoneSteamAmount + 2 : StoneSteamAmount;
            if (_cornerSteam is not null) _cornerSteam.Amount = Steamy ? CornerSteamAmount + 2 : CornerSteamAmount;
            if (_hazeMaterial is not null)
                _hazeMaterial.AlbedoColor = _hazeMaterial.AlbedoColor with { A = Steamy ? SteamyHazeAlpha : SteamHazeAlpha };
        }
        // Presentation freezes with the world (the same contract the village
        // chimney plumes follow); every other frame this is four float compares.
        var speed = step > 0 ? 1f : 0f;
        foreach (var emitter in _emitters)
        {
            if (emitter is null) continue;
            if (emitter.SpeedScale != speed) emitter.SpeedScale = speed;
        }
    }

    private void UpdateTriggers(bool inside, bool wetRoom, bool audible, bool steamyRising)
    {
        var doorOpen = WetDoorOpen();
        if (audible)
        {
            if (inside && !_prevWetRoom && wetRoom) TriggerBurst(playCue: true);
            else if (_prevInside && !_prevDoorOpen && doorOpen) TriggerBurst(playCue: true);
            else if (inside && steamyRising) TriggerBurst(playCue: false);
        }
        _prevDoorOpen = doorOpen;
    }

    private void TriggerBurst(bool playCue)
    {
        if (_steamBurst is not null)
        {
            _steamBurst.Restart();
            _steamBurst.Emitting = true;
        }
        _bursts++;
        if (playCue) PlayCue(_burstStream, CueDbBurst, _doorCueGlobal);
    }

    private void UpdateHaze(bool visible)
    {
        if (_hazeLower is not null && _hazeLower.Visible != visible) _hazeLower.Visible = visible;
        if (_hazeUpper is not null && _hazeUpper.Visible != visible) _hazeUpper.Visible = visible;
    }

    private void UpdateLamp(bool visible)
    {
        if (!visible || _wetLamp is null || !IsInstanceValid(_wetLamp)) return;
        var t = (float)_time;
        var wave = Mathf.Sin(t * 9.3f) * .5f + Mathf.Sin(t * 23.7f + 1.3f) * .3f + Mathf.Sin(t * 2.1f) * .2f;
        var heat = FireBurning || Steamy ? 1f : .45f;
        var energy = _wetLampBaseEnergy + wave * .055f * heat;
        if (Mathf.Abs(_wetLamp.LightEnergy - energy) > .0015f) _wetLamp.LightEnergy = energy;
    }

    private void UpdateAudio(float step, bool inside)
    {
        var bedTarget = inside ? (FireBurning ? BedDbFire : BedDbInside) : SilentDb;
        var dripTarget = inside ? DripDbInside : SilentDb;
        _bedDb = Mathf.MoveToward(_bedDb, bedTarget, step * FadeDbPerSecond);
        _dripDb = Mathf.MoveToward(_dripDb, dripTarget, step * FadeDbPerSecond);
        FollowVoice(_bedVoice, _bedStream, ref _bedDb, inside);
        FollowVoice(_dripVoice, _dripStream, ref _dripDb, inside);
    }

    private void FollowVoice(AudioStreamPlayer3D? voice, AudioStream? stream, ref float level, bool inside)
    {
        if (voice is null || stream is null || _headless) return;
        voice.VolumeDb = level;
        if (inside)
        {
            if (voice.Playing) return;
            level = SilentDb;
            voice.VolumeDb = SilentDb;
            voice.Play();
            return;
        }
        if (voice.Playing && level <= SilentDb + .5f) voice.Stop();
    }

    private void PlayCue(AudioStream? stream, float volumeDb, Vector3 at)
    {
        if (!_initialized || _headless || stream is null || _cueVoice is null || !_cueVoice.IsInsideTree()) return;
        _cueVoice.Stop();
        _cueVoice.GlobalPosition = at;
        _cueVoice.Stream = stream;
        _cueVoice.VolumeDb = volumeDb;
        _cueVoice.PitchScale = 1f;
        _cueVoice.Play();
    }

    private bool PausedAtListener() =>
        GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true };

    private bool WetDoorOpen()
    {
        if (_wetDoorHinge is null || !IsInstanceValid(_wetDoorHinge)) return false;
        return Mathf.Abs(_wetDoorHinge.Rotation.Y - Mathf.Pi * .5f) > .25f;
    }

    private Vector3 AnchorPosition(Vector3 bathLocal) =>
        GetParent() == _bath ? bathLocal : ToLocal(_bath!.ToGlobal(bathLocal));

    private static AudioStream? LoadLoop(string path)
    {
        var stream = LoadStream(path);
        if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        return stream;
    }

    private static AudioStream? LoadOneShot(string path) => LoadStream(path);

    private static AudioStream? LoadStream(string path)
    {
        // The banya WAVs are unique to this system; the imported loop flag is
        // written into the resource, so ignore the shared cache (BathSpirit
        // does the same).  A missing import degrades to silence, never an error.
        return ResourceLoader.Exists(path)
            ? ResourceLoader.Load<AudioStream>(path, cacheMode: ResourceLoader.CacheMode.Ignore)
            : null;
    }
}
