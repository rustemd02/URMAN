using Godot;

namespace Urman.Godot;

/// <summary>
/// Session-only immersion details around the driven Niva: winter exhaust
/// vapor, snow sliding off the roof when the car first pulls away, snow
/// carried into the driver's footwell, live dashboard lamps/backlight, and
/// the quiet tick of the cooling exhaust after shutdown. Presentation only:
/// no collision, no save or story state. Reads only public VehicleController
/// state (EngineRunning, Speed, ParkingBrake, Headlights, Driver) plus the
/// inside/outside flag handed to Tick.
///
/// Wiring: the node locates its VehicleController through the parent chain
/// and drives itself from _PhysicsProcess, so one line is enough --
///   root.AddChild(new VehicleImmersionDetails { Name = "ImmersionDetails" });
/// in VehicleVisualFactory.Niva(), or inside VehicleController.Configure after
/// AddChild(_visual.Root). Explicit owners may instead call
/// Initialize(vehicleRoot, vehicle) once and Tick(delta, inside) per tick;
/// the self-driven path then stops on its own.
/// </summary>
public partial class VehicleImmersionDetails : Node3D
{
    private const float StepMax = .05f;
    private const float TeleportDistance = 3f;
    private const float RoofSnowTriggerSpeed = 1f;
    private const float ColdVaporSeconds = 18f;
    private const float FootSnowMeltSeconds = 95f;
    private const float CoolingSeconds = 12f;
    private const int TickMixRate = 22050;
    private static readonly Vector3 ExhaustTip = new(-.52f, .47f, 1.92f);
    private static readonly Vector3 FootSnowBase = new(.30f, .11f, .22f);

    private VehicleController? _vehicle;
    private Node3D? _visual;
    private bool _initialized;
    private bool _externallyDriven;

    private CpuParticles3D? _vaporIdle;
    private CpuParticles3D? _vaporCold;
    private CpuParticles3D? _roofSnow;
    private MeshInstance3D? _footSnow;
    private StandardMaterial3D? _footSnowMaterial;
    private MeshInstance3D? _brakeLamp;
    private MeshInstance3D[] _gaugeGlows = Array.Empty<MeshInstance3D>();
    private AudioStreamPlayer3D? _tickAudio;
    private AudioStreamGeneratorPlayback? _tickPlayback;
    private Vector2[][] _tickBuffers = Array.Empty<Vector2[]>();
    private Vector2[] _silence = Array.Empty<Vector2>();
    private Vector2[] _pendingTick = Array.Empty<Vector2>();

    private bool _wasRunning;
    private bool _wasInside;
    private bool _roofSnowArmed = true;
    private bool _dashGlowOn;
    private float _engineOnSeconds;
    private float _melt;
    private float _footSnowAlpha;
    private float _footSnowWrittenMelt;
    private float _coolingLeft;
    private float _nextTick;
    private int _tickVariant;
    private int _pendingTickOffset;
    private uint _rng = 0x6d2b79f5u;
    private Vector3 _lastPosition = new(float.NaN, 0f, 0f);

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("collisionOwner", "none");
        SetMeta("savePolicy", "session-only detail state; never serialized");
        SetProcess(false);
        if (_vehicle is null) LocateVehicle();
        // The visual parent is still adding children during _Ready. Build the
        // sibling presentation nodes on the existing first physics tick below.
    }

    public override void _ExitTree()
    {
        if (_tickAudio is not null && GodotObject.IsInstanceValid(_tickAudio))
        {
            _tickAudio.Stop();
            _tickAudio.Stream = null;
        }
        _tickPlayback = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_externallyDriven) return;
        if (_vehicle is null || !GodotObject.IsInstanceValid(_vehicle))
        {
            _vehicle = null;
            LocateVehicle();
            if (_vehicle is null) return;
        }
        if (!_initialized) Initialize(VisualRoot(), _vehicle);
        Update(delta, _vehicle.Driver is not null);
    }

    /// <summary>
    /// Builds the presentation nodes under the authored visual root. Idempotent
    /// and Niva-only; any other vehicle kind leaves the node inert.
    /// </summary>
    public void Initialize(Node3D vehicleRoot, VehicleController vehicle)
    {
        if (_initialized || vehicle is null) return;
        _initialized = true;
        if (vehicle.Definition is null || vehicle.Definition.Kind != VehicleKind.Niva) return;
        _vehicle = vehicle;
        _visual = vehicleRoot ?? this;
        BuildVapor();
        BuildRoofSnow();
        BuildFootSnow();
        BuildDash();
        BuildCoolingTicks();
        SetMeta("detailNodes", "vapor, roof snow, footwell snow, dash lamps, cooling ticks");
    }

    /// <summary>Owner-driven update; switches the node off its own physics tick.</summary>
    public void Tick(double delta, bool inside)
    {
        _externallyDriven = true;
        if (!_initialized)
        {
            if (_vehicle is null || !GodotObject.IsInstanceValid(_vehicle))
            {
                _vehicle = null;
                LocateVehicle();
            }
            if (_vehicle is not null) Initialize(VisualRoot(), _vehicle);
        }
        Update(delta, inside);
    }

    private void Update(double delta, bool inside)
    {
        var vehicle = _vehicle;
        if (!_initialized || vehicle is null || !GodotObject.IsInstanceValid(vehicle)) return;
        var dt = Mathf.Min((float)delta, StepMax);
        if (dt <= 0f) return;
        var position = vehicle.GlobalPosition;
        if (!float.IsNaN(_lastPosition.X)
            && new Vector2(position.X - _lastPosition.X, position.Z - _lastPosition.Z).LengthSquared()
                > TeleportDistance * TeleportDistance)
            ResetForSession(inside);
        _lastPosition = position;

        var running = vehicle.EngineRunning;
        if (_wasRunning && !running) BeginCooling();
        else if (!_wasRunning && running) StopCooling();
        _wasRunning = running;
        _engineOnSeconds = running ? _engineOnSeconds + dt : 0f;

        UpdateVapor(running);
        UpdateRoofSnow(Mathf.Abs(vehicle.Speed));
        UpdateFootSnow(dt, inside);
        UpdateDash(vehicle.ParkingBrake, vehicle.Headlights);
        UpdateCooling(dt);
        _wasInside = inside;
    }

    private void ResetForSession(bool inside)
    {
        _roofSnowArmed = true;
        _melt = 0f;
        _wasRunning = false;
        _wasInside = inside;
        _engineOnSeconds = 0f;
        StopCooling();
    }

    private void LocateVehicle()
    {
        for (var node = GetParent(); node is not null; node = node.GetParent())
        {
            if (node is not VehicleController controller) continue;
            _vehicle = controller;
            return;
        }
    }

    private Node3D VisualRoot()
        => _vehicle?.GetNodeOrNull<Node3D>("VehicleVisual") ?? (Node3D?)_vehicle ?? this;

    private void UpdateVapor(bool running)
    {
        if (_vaporIdle is null || _vaporCold is null) return;
        SetEmitting(_vaporIdle, running);
        SetEmitting(_vaporCold, running && _engineOnSeconds < ColdVaporSeconds);
    }

    private static void SetEmitting(CpuParticles3D emitter, bool value)
    {
        if (emitter.Emitting != value) emitter.Emitting = value;
    }

    private void UpdateRoofSnow(float speed)
    {
        if (!_roofSnowArmed || _roofSnow is null || speed < RoofSnowTriggerSpeed) return;
        _roofSnowArmed = false;
        _roofSnow.Emitting = true;
    }

    private void UpdateFootSnow(float dt, bool inside)
    {
        if (_footSnow is null || _footSnowMaterial is null) return;
        if (inside && !_wasInside) _melt = 1f;
        if (_melt <= 0f)
        {
            if (_footSnow.Visible) _footSnow.Visible = false;
            return;
        }
        _melt = Mathf.Max(0f, _melt - dt / FootSnowMeltSeconds);
        var alpha = .82f * Mathf.Min(1f, _melt * 1.5f);
        if (Mathf.Abs(alpha - _footSnowAlpha) > .01f)
        {
            _footSnowAlpha = alpha;
            _footSnowMaterial.AlbedoColor = new Color(.92f, .95f, .98f, alpha);
        }
        if (Mathf.Abs(_melt - _footSnowWrittenMelt) > .01f)
        {
            _footSnowWrittenMelt = _melt;
            var spread = .45f + .55f * _melt;
            _footSnow.Scale = new Vector3(FootSnowBase.X * spread,
                FootSnowBase.Y * (.35f + .65f * _melt), FootSnowBase.Z * spread);
        }
        if (!_footSnow.Visible) _footSnow.Visible = true;
    }

    private void UpdateDash(bool parkingBrake, bool headlights)
    {
        if (_brakeLamp is not null && _brakeLamp.Visible != parkingBrake) _brakeLamp.Visible = parkingBrake;
        if (_dashGlowOn == headlights) return;
        _dashGlowOn = headlights;
        for (var index = 0; index < _gaugeGlows.Length; index++) _gaugeGlows[index].Visible = headlights;
    }

    private void BeginCooling()
    {
        if (_visual is null) return;
        _coolingLeft = CoolingSeconds;
        _nextTick = .3f + Random01() * .6f;
        _pendingTick = Array.Empty<Vector2>();
        _pendingTickOffset = 0;
        if (_tickAudio is null) return;
        if (!_tickAudio.Playing) _tickAudio.Play();
        _tickPlayback = _tickAudio.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    private void StopCooling()
    {
        _coolingLeft = 0f;
        _pendingTick = Array.Empty<Vector2>();
        _pendingTickOffset = 0;
        if (_tickAudio is null)
        {
            _tickPlayback = null;
            return;
        }
        if (_tickAudio.Playing) _tickAudio.Stop();
        _tickPlayback = null;
    }

    private void UpdateCooling(float dt)
    {
        if (_coolingLeft <= 0f) return;
        _coolingLeft -= dt;
        var playback = _tickPlayback;
        var available = playback?.GetFramesAvailable() ?? 0;
        if (playback is not null && available > 0)
        {
            if (_pendingTick.Length > 0)
            {
                var count = Math.Min(_pendingTick.Length - _pendingTickOffset, available);
                if (count > 0)
                {
                    playback.PushBuffer(_pendingTick.AsSpan(_pendingTickOffset, count));
                    _pendingTickOffset += count;
                }
                if (_pendingTickOffset >= _pendingTick.Length)
                {
                    _pendingTick = Array.Empty<Vector2>();
                    _pendingTickOffset = 0;
                    _nextTick = NextTickDelay();
                }
            }
            else
            {
                _nextTick -= dt;
                if (_nextTick <= 0f && _tickBuffers.Length > 0)
                {
                    _pendingTick = _tickBuffers[_tickVariant];
                    _tickVariant = (_tickVariant + 1) % _tickBuffers.Length;
                    _pendingTickOffset = 0;
                }
                else
                {
                    var fill = Math.Min(Math.Min(available, Math.Max(1, (int)(dt * TickMixRate) + 2)), _silence.Length);
                    playback.PushBuffer(_silence.AsSpan(0, fill));
                }
            }
        }
        if (_coolingLeft <= 0f) StopCooling();
    }

    private float NextTickDelay()
        => (.35f + Random01() * 1.1f) * (1f + (CoolingSeconds - _coolingLeft) / CoolingSeconds);

    private float Random01()
    {
        _rng ^= _rng << 13;
        _rng ^= _rng >> 17;
        _rng ^= _rng << 5;
        return (_rng & 0xFFFFFFu) / 16777215f;
    }

    private void BuildVapor()
    {
        if (_visual is null) return;
        _vaporIdle = VaporEmitter("ExhaustVaporIdle", 10, 1.7f, .75f, .11f);
        _vaporIdle.Position = ExhaustTip;
        _visual.AddChild(_vaporIdle);
        _vaporCold = VaporEmitter("ExhaustVaporCold", 12, 1.15f, 1.15f, .16f);
        _vaporCold.Position = ExhaustTip;
        _visual.AddChild(_vaporCold);
    }

    private static CpuParticles3D VaporEmitter(string name, int amount, float lifetime, float scale, float opacity)
        => new()
        {
            Name = name,
            Emitting = false,
            Amount = amount,
            Lifetime = lifetime,
            LifetimeRandomness = .45f,
            Preprocess = .1f,
            LocalCoords = false,
            Mesh = WinterParticleSurfaces.Snow(.22f + scale * .1f, opacity),
            Direction = new Vector3(0f, .34f, .8f),
            Spread = 18f,
            Gravity = new Vector3(0f, .09f, .03f),
            InitialVelocityMin = .2f * scale,
            InitialVelocityMax = .6f * scale,
            ScaleAmountMin = .55f * scale,
            ScaleAmountMax = 1.15f * scale,
            ScaleAmountCurve = GrowthCurve(),
            ColorRamp = WinterParticleSurfaces.Fade(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

    private void BuildRoofSnow()
    {
        if (_visual is null) return;
        _roofSnow = new CpuParticles3D
        {
            Name = "RoofSnowSlip",
            Emitting = false,
            OneShot = true,
            Explosiveness = .92f,
            Amount = 30,
            Lifetime = 1.6f,
            LocalCoords = false,
            Mesh = WinterParticleSurfaces.Snow(.085f, .55f),
            ColorRamp = WinterParticleSurfaces.Fade(),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(.64f, .04f, .78f),
            Direction = new Vector3(0f, -.55f, .84f),
            Spread = 55f,
            Gravity = new Vector3(0f, -6.5f, 0f),
            InitialVelocityMin = .3f,
            InitialVelocityMax = 1.1f,
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.2f,
            Position = new Vector3(0f, 1.72f, .62f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _visual.AddChild(_roofSnow);
    }

    private void BuildFootSnow()
    {
        if (_visual is null) return;
        _footSnowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.92f, .95f, .98f, .82f),
            Roughness = .9f,
            Metallic = 0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _footSnow = new MeshInstance3D
        {
            Name = "FootwellSnow",
            Mesh = RegisterMesh(new SphereMesh { Radius = .5f, Height = 1f, RadialSegments = 12, Rings = 6 }),
            MaterialOverride = _footSnowMaterial,
            Position = new Vector3(-.44f, .53f, -.30f),
            Scale = FootSnowBase,
            Visible = false
        };
        _visual.AddChild(_footSnow);
    }

    private void BuildDash()
    {
        if (_visual is null) return;
        _brakeLamp = new MeshInstance3D
        {
            Name = "HandbrakeTelltale",
            Mesh = RegisterMesh(new QuadMesh { Size = new Vector2(.0125f, .0095f) }),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, .15f, .1f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            Position = new Vector3(-.509f, 1.024f, -.402f),
            Visible = false
        };
        _visual.AddChild(_brakeLamp);

        var glowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, .86f, .62f, .3f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _gaugeGlows = new[]
        {
            GaugeGlow("SpeedometerGlow", new Vector3(-.52f, 1.102f, -.384f), glowMaterial),
            GaugeGlow("TachometerGlow", new Vector3(-.36f, 1.102f, -.384f), glowMaterial)
        };
        for (var index = 0; index < _gaugeGlows.Length; index++) _visual.AddChild(_gaugeGlows[index]);
    }

    private static MeshInstance3D GaugeGlow(string name, Vector3 position, Material material)
        => new()
        {
            Name = name,
            Mesh = RegisterMesh(new CylinderMesh
            {
                TopRadius = .052f,
                BottomRadius = .052f,
                Height = .0012f,
                RadialSegments = 20,
                Rings = 1
            }),
            MaterialOverride = material,
            Position = position,
            RotationDegrees = new Vector3(90f, 0f, 0f),
            Visible = false
        };

    private void BuildCoolingTicks()
    {
        _tickBuffers = BuildTickBuffers();
        _silence = new Vector2[TickMixRate / 20];
        if (DisplayServer.GetName() == "headless" || _visual is null) return;
        AudioSettingsService.EnsureBuses();
        _tickAudio = new AudioStreamPlayer3D
        {
            Name = "ExhaustCoolingTicks",
            Bus = AudioSettingsService.SfxBus,
            VolumeDb = -18f,
            UnitSize = 2.4f,
            MaxDistance = 16f,
            Stream = new AudioStreamGenerator { MixRate = TickMixRate, BufferLength = .35f },
            Autoplay = false,
            Position = new Vector3(-.45f, .42f, 1.35f)
        };
        _visual.AddChild(_tickAudio);
    }

    private static Vector2[][] BuildTickBuffers()
    {
        var buffers = new Vector2[6][];
        var seed = 0x2545f491u;
        for (var variant = 0; variant < buffers.Length; variant++)
        {
            var length = (int)(TickMixRate * (.018f + variant * .004f));
            var buffer = new Vector2[length];
            var frequency = 1450f + variant * 320f;
            for (var frame = 0; frame < length; frame++)
            {
                var progress = (float)frame / length;
                var envelope = (1f - progress) * (1f - progress);
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                var noise = (seed & 65535) / 32767.5f - 1f;
                var sample = (Mathf.Sin(frame * frequency * Mathf.Tau / TickMixRate) * .6f + noise * .4f)
                    * envelope * .55f;
                buffer[frame] = new Vector2(sample, sample);
            }
            buffers[variant] = buffer;
        }
        return buffers;
    }

    private static Curve GrowthCurve()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, .55f));
        curve.AddPoint(new Vector2(1f, 1.5f));
        return curve;
    }

    /// <summary>
    /// The vehicle smoke audit walks every MeshInstance3D under the vehicle and
    /// requires the same triangle bookkeeping the factory meshes publish: the
    /// exact triangle corner count of the primitive in expectedTriangleCorners
    /// and the authored-corner count in manualTriangleCorners.
    /// </summary>
    private static Mesh RegisterMesh(Mesh mesh)
    {
        var corners = 0;
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.SurfaceGetArrays(surface);
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            corners += indices.Length > 0 ? indices.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
        }
        mesh.SetMeta("expectedTriangleCorners", corners);
        mesh.SetMeta("manualTriangleCorners", 0);
        return mesh;
    }
}
