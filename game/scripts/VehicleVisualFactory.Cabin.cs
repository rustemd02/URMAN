using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Reads the two cabin switches owned by the VehicleController lane:
/// <c>HighBeams</c> and <c>Wipers</c> (input actions <c>vehicle_high_beam</c>
/// and <c>vehicle_wipers</c>). Those members may not exist yet while that lane
/// lands, so the bridge binds their getters by reflection once and degrades to
/// "off" without this lane taking a compile-time dependency on a file it must
/// not edit. The presentation never samples input itself; the controller
/// remains the single owner of the state and of its persistence.
/// </summary>
internal static class VehicleCabinState
{
    public const string HighBeamAction = "vehicle_high_beam";
    public const string WipersAction = "vehicle_wipers";

    private static readonly Func<VehicleController, bool> Off = static _ => false;
    private static readonly Func<VehicleController, bool> HighBeamsReader = Bind("HighBeams");
    private static readonly Func<VehicleController, bool> WipersReader = Bind("Wipers");

    /// <summary>True once the controller lane supplies the HighBeams property.</summary>
    public static bool HighBeamsBound => !ReferenceEquals(HighBeamsReader, Off);

    /// <summary>True once the controller lane supplies the Wipers property.</summary>
    public static bool WipersBound => !ReferenceEquals(WipersReader, Off);

    /// <summary>Diagnostic: both toggle actions exist in the input map.</summary>
    public static bool ActionsMapped => InputMap.HasAction(HighBeamAction) && InputMap.HasAction(WipersAction);

    public static bool ReadHighBeams(VehicleController controller) => HighBeamsReader(controller);

    public static bool ReadWipers(VehicleController controller) => WipersReader(controller);

    private static Func<VehicleController, bool> Bind(string propertyName)
    {
        var getter = typeof(VehicleController)
            .GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.GetGetMethod();
        if (getter is null || getter.ReturnType != typeof(bool) || getter.GetParameters().Length != 0)
        {
            return Off;
        }

        try
        {
            return (Func<VehicleController, bool>)getter.CreateDelegate(typeof(Func<VehicleController, bool>));
        }
        catch (ArgumentException)
        {
            return Off;
        }
    }
}

/// <summary>Geometry the factory hands back to the self-driven cabin node.</summary>
internal readonly record struct NivaCabinParts(
    MeshInstance3D Snow, ShaderMaterial SnowMaterial, Node3D WiperLeft, Node3D WiperRight, MeshInstance3D HighBeamTell);

/// <summary>
/// The lived-in Niva cabin: a snow layer on the windscreen that builds up on
/// the road and clears under the two live wipers, and the high-beam look of the
/// lamps plus the blue dash tell-tale. The authored Blender model keeps its
/// parked wiper beams; the moving pair overlays them, so the parked pose is the
/// model's own and only the sweep is new. Everything here is presentation: no
/// collision, no save state, no fleet or physics writes, no per-frame
/// allocations.
/// </summary>
public partial class VehicleNivaCabin : Node3D
{
    // --- windscreen frame (tools/blender/generate_niva.py) -------------------
    // NivaGlass_Windshield: (-.745,1.12,-.69) (.745,1.12,-.69) (.715,1.63,-.31) (-.715,1.63,-.31)
    public static readonly Vector3 WindscreenBottomLeft = new(-.745f, 1.12f, -.69f);
    public static readonly Vector3 WindscreenBottomRight = new(.745f, 1.12f, -.69f);
    public static readonly Vector3 WindscreenTopRight = new(.715f, 1.63f, -.31f);
    public static readonly Vector3 WindscreenTopLeft = new(-.715f, 1.63f, -.31f);
    public static readonly Vector3 WindscreenNormal = new Vector3(0f, .38f, -.51f).Normalized();
    // Parked blades authored in the model (NivaWiper_L / NivaWiper_R); the live
    // arms pivot exactly there so the parked pose matches the model.
    public static readonly Vector3 WiperPivotLeft = new(-.06f, 1.20f, -.665f);
    public static readonly Vector3 WiperPivotRight = new(.16f, 1.20f, -.665f);
    public static readonly Vector3 WiperOuterLeft = new(-.52f, 1.15f, -.70f);
    public static readonly Vector3 WiperOuterRight = new(.62f, 1.15f, -.70f);
    public const float WiperSweepRadians = 1.02f;   // ~58.5 degrees of upward sweep
    public const float WiperCycleSeconds = 1.45f;   // out stroke, return and pause
    // --- snow model -----------------------------------------------------------
    public const float SnowStartAmount = .12f;      // a parked car already carries a dusting
    public const float SnowBaseRatePerSecond = .0042f;
    public const float WipeAreaRefillSeconds = 70f; // cleared sector refills in about a minute
    public const float SnowMaxAlpha = .85f;         // never fully blocks the driver's view
    private const float SurfaceSampleSeconds = .6f;
    private const float MaxSpeedForSnow = 7f;       // the Niva's authored top speed
    private const float SnowRoadFactor = 1f;
    private const float SnowSurfaceFactor = 2.2f;   // open snow and drifts
    private const float SnowIceFactor = .6f;
    // --- high beams -----------------------------------------------------------
    private const float HighBeamEnergyFactor = 1.55f;
    private const float HighBeamRangeFactor = 2f;
    private const float HighBeamAngleDegrees = 27f;
    private static readonly Color HighBeamColor = new(1f, .9f, .72f);

    private static readonly StringName SnowAmountParam = "snow_amount";
    private static readonly StringName WipeAreaParam = "wipe_area";
    private static readonly StringName WipeProgressParam = "wipe_progress";
    private static readonly StringName SnowMoodParam = "snow_mood";

    private VehicleController? _controller;
    private VehicleFleet? _fleet;
    private ShaderMaterial? _snowMaterial;
    private Node3D? _wiperLeft;
    private Node3D? _wiperRight;
    private MeshInstance3D? _highBeamTell;
    private SpotLight3D[] _lamps = Array.Empty<SpotLight3D>();
    private float[] _lampBaseEnergy = Array.Empty<float>();
    private float[] _lampBaseRange = Array.Empty<float>();
    private float[] _lampBaseAngle = Array.Empty<float>();
    private Color[] _lampBaseColor = Array.Empty<Color>();
    private bool _highBeamLook;
    private bool _highBeamInitialised;
    private bool _wipersWereOn;
    private float _wiperAngle;
    private float _appliedWiperAngle = -1f;
    private float _passSeconds;
    private float _wipeArea;
    private float _wipeProgress;
    private float _snowAmount = SnowStartAmount;
    private float _snowApplied = -1f;
    private float _areaApplied = -1f;
    private float _progressApplied = -1f;
    private float _surfaceTimer;
    private float _surfaceFactor = 1f;
    private readonly List<StandardMaterial3D> _bodySnow = new();
    private readonly List<Color> _bodySnowBase = new();
    private Vector3 _appliedSnowMood = new(float.NaN, 0f, 0f);

    public override void _Ready()
    {
        SetMeta("presentationOwnership", "presentation-only; no collision, no save state");
        SetMeta("cabinWeather", "windscreen snow layer, two live wipers, high-beam dash tell");
        var parts = VehicleVisualFactory.BuildNivaCabin(this);
        _snowMaterial = parts.SnowMaterial;
        _wiperLeft = parts.WiperLeft;
        _wiperRight = parts.WiperRight;
        _highBeamTell = parts.HighBeamTell;
        _lamps = VehicleVisualFactory.CabinHeadlights(this);
        _lampBaseEnergy = new float[_lamps.Length];
        _lampBaseRange = new float[_lamps.Length];
        _lampBaseAngle = new float[_lamps.Length];
        _lampBaseColor = new Color[_lamps.Length];
        for (var index = 0; index < _lamps.Length; index++)
        {
            var lamp = _lamps[index];
            _lampBaseEnergy[index] = lamp.LightEnergy;
            _lampBaseRange[index] = lamp.SpotRange;
            _lampBaseAngle[index] = lamp.SpotAngle;
            _lampBaseColor[index] = lamp.LightColor;
        }

        _controller = ResolveController();
        SetMeta("highBeamsBound", VehicleCabinState.HighBeamsBound);
        SetMeta("wipersBound", VehicleCabinState.WipersBound);
        SetMeta("cabinActionsMapped", VehicleCabinState.ActionsMapped);
        BindVehicleSnow();
        ApplySnowUniforms(force: true);
        ApplySnowMood();
        ApplyHighBeamLook(false);
    }

    public override void _Process(double delta)
    {
        if (_controller is not null && !GodotObject.IsInstanceValid(_controller))
        {
            _controller = null;
        }

        AnimateCabin(Mathf.Min((float)delta, .05f));
    }

    private VehicleController? ResolveController()
        => GetParent() is { } visual ? visual.GetParent() as VehicleController : null;

    private void AnimateCabin(float dt)
    {
        var controller = _controller;
        var wipersOn = controller is not null && VehicleCabinState.ReadWipers(controller);
        if (wipersOn)
        {
            _passSeconds += dt;
            if (_passSeconds >= WiperCycleSeconds)
            {
                _passSeconds -= WiperCycleSeconds;
                // One full pass wipes the whole swept sector; later passes just
                // keep it clear while fresh snow falls.
                _wipeArea = 1f;
            }

            var phase = _passSeconds / WiperCycleSeconds;
            var stroke = .5f - .5f * Mathf.Cos(Mathf.Tau * phase);
            _wiperAngle = WiperSweepRadians * stroke;
            _wipeProgress = phase <= .5f ? stroke : 1f;
        }
        else
        {
            if (_wipersWereOn)
            {
                // The return stroke pushes the last snow down, so the sector
                // stays clear even if the switch is flipped mid-pass.
                _wipeArea = Mathf.Max(_wipeArea, _wipeProgress);
                _wipeProgress = 0f;
                _passSeconds = 0f;
            }

            if (_wiperAngle > 0f)
            {
                _wiperAngle = Mathf.Max(0f, _wiperAngle - dt * WiperSweepRadians * 1.8f);
            }

            _wipeArea = Mathf.Max(0f, _wipeArea - dt / WipeAreaRefillSeconds);
        }

        _wipersWereOn = wipersOn;
        if (_wiperAngle != _appliedWiperAngle)
        {
            _appliedWiperAngle = _wiperAngle;
            var rotation = new Vector3(0f, 0f, _wiperAngle);
            if (_wiperLeft is not null) _wiperLeft.Rotation = rotation;
            if (_wiperRight is not null) _wiperRight.Rotation = rotation;
        }

        if (controller is not null)
        {
            AccumulateSnow(controller, dt);
        }

        ApplySnowUniforms(force: false);
        ApplySnowMood();
        UpdateHighBeams(controller);
    }

    /// <summary>
    /// VIS-105: the snow the model itself carries (sill shelves, the gutter line)
    /// is collected once so the current atmosphere state can reach it. Presentation
    /// only: the materials are the vehicle's own surface overrides, no geometry,
    /// no collision and no save state changes hands here.
    /// </summary>
    private void BindVehicleSnow()
    {
        _bodySnow.Clear();
        _bodySnowBase.Clear();
        if (GetParent() is not { } visual) return;
        foreach (var node in visual.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D instance) continue;
            // Surface count belongs to the Mesh resource, the active material to the
            // instance: the vehicle's own snow is a surface override, so reading the
            // instance is the only way to see the finish the renderer actually uses.
            if (instance.Mesh is not { } mesh) continue;
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (instance.GetActiveMaterial(surface) is not StandardMaterial3D solid) continue;
                if (solid.GetMeta("vehicleFinish", string.Empty).AsString() != "snow") continue;
                var baseHex = solid.GetMeta("vehicleSnowBaseColor", string.Empty).AsString();
                _bodySnow.Add(solid);
                _bodySnowBase.Add(baseHex.Length == 6 ? Color.FromHtml(baseHex) : solid.AlbedoColor);
            }
        }
        SetMeta("vehicleSnowSurfaces", _bodySnow.Count);
    }

    /// <summary>
    /// One ratio per channel against the library's neutral snow, so the windscreen
    /// layer and the model's settled snow answer the same authored light state as
    /// the village snow (STYLE RECIPE W1). A profile without a snow block leaves
    /// the ratio at one and changes nothing.
    /// </summary>
    private void ApplySnowMood()
    {
        var ratio = Vector3.One;
        if (AtmosphereProfiles.Applied is { } profile)
        {
            var effective = profile.EffectiveSnowColor;
            var neutral = AtmosphereProfile.NeutralSnowColor;
            ratio = new Vector3(
                Mathf.Clamp(effective.R / Mathf.Max(neutral.R, .0001f), .5f, 1.5f),
                Mathf.Clamp(effective.G / Mathf.Max(neutral.G, .0001f), .5f, 1.5f),
                Mathf.Clamp(effective.B / Mathf.Max(neutral.B, .0001f), .5f, 1.5f));
        }
        // Vector3 carries no IsNaN member: the sentinel is the NaN X the field is
        // initialised with, which is exactly what float.IsNaN reads.
        if (!float.IsNaN(_appliedSnowMood.X) && _appliedSnowMood.DistanceSquaredTo(ratio) < 1e-8f) return;
        _appliedSnowMood = ratio;
        _snowMaterial?.SetShaderParameter(SnowMoodParam, ratio);
        for (var index = 0; index < _bodySnow.Count; index++)
        {
            var base_ = _bodySnowBase[index];
            _bodySnow[index].AlbedoColor = new Color(base_.R * ratio.X, base_.G * ratio.Y,
                base_.B * ratio.Z, base_.A);
        }
    }

    private void AccumulateSnow(VehicleController controller, float dt)
    {
        _surfaceTimer -= dt;
        if (_surfaceTimer <= 0f)
        {
            _surfaceTimer = SurfaceSampleSeconds;
            _surfaceFactor = SampleSurfaceFactor(controller);
        }

        var speed = Mathf.Clamp(Mathf.Abs(controller.Speed) / MaxSpeedForSnow, 0f, 1f);
        _snowAmount = Mathf.Min(1f,
            _snowAmount + dt * SnowBaseRatePerSecond * _surfaceFactor * (.45f + 1.75f * speed));
    }

    /// <summary>
    /// Drifts throw more snow at the glass than the packed carriageway: one
    /// public fleet surface query every <see cref="SurfaceSampleSeconds"/>.
    /// </summary>
    private float SampleSurfaceFactor(VehicleController controller)
    {
        if (_fleet is null || !GodotObject.IsInstanceValid(_fleet))
        {
            _fleet = GetTree()?.GetFirstNodeInGroup("vehicle_fleet") as VehicleFleet;
        }

        if (_fleet is null)
        {
            return SnowRoadFactor;
        }

        return _fleet.SurfaceAt(controller.GlobalPosition, -controller.GlobalBasis.Z) switch
        {
            VehicleSurfaceKind.Snow => SnowSurfaceFactor,
            VehicleSurfaceKind.Ice => SnowIceFactor,
            _ => SnowRoadFactor
        };
    }

    private void ApplySnowUniforms(bool force)
    {
        if (_snowMaterial is null)
        {
            return;
        }

        if (force || Mathf.Abs(_snowApplied - _snowAmount) > .0015f)
        {
            _snowApplied = _snowAmount;
            _snowMaterial.SetShaderParameter(SnowAmountParam, _snowAmount);
        }

        if (force || Mathf.Abs(_areaApplied - _wipeArea) > .0015f)
        {
            _areaApplied = _wipeArea;
            _snowMaterial.SetShaderParameter(WipeAreaParam, _wipeArea);
        }

        if (force || Mathf.Abs(_progressApplied - _wipeProgress) > .0015f)
        {
            _progressApplied = _wipeProgress;
            _snowMaterial.SetShaderParameter(WipeProgressParam, _wipeProgress);
        }
    }

    private void UpdateHighBeams(VehicleController? controller)
    {
        var high = controller is not null && VehicleCabinState.ReadHighBeams(controller);
        if (_highBeamInitialised && high == _highBeamLook)
        {
            return;
        }

        ApplyHighBeamLook(high);
    }

    private void ApplyHighBeamLook(bool high)
    {
        _highBeamLook = high;
        _highBeamInitialised = true;
        for (var index = 0; index < _lamps.Length; index++)
        {
            var lamp = _lamps[index];
            if (!GodotObject.IsInstanceValid(lamp)) continue;
            lamp.LightEnergy = high ? _lampBaseEnergy[index] * HighBeamEnergyFactor : _lampBaseEnergy[index];
            lamp.SpotRange = high ? _lampBaseRange[index] * HighBeamRangeFactor : _lampBaseRange[index];
            lamp.SpotAngle = high ? HighBeamAngleDegrees : _lampBaseAngle[index];
            lamp.LightColor = high ? HighBeamColor : _lampBaseColor[index];
        }

        if (_highBeamTell is not null)
        {
            _highBeamTell.Visible = high;
        }
    }
}

public static partial class VehicleVisualFactory
{
    /// <summary>Windscreen snow layer, the two live wiper bases and the dash tell-tale.</summary>
    internal static NivaCabinParts BuildNivaCabin(Node3D cabin)
    {
        var material = new ShaderMaterial { Shader = SnowShader };
        var snow = BuildWindscreenSnow(cabin, material);
        var left = BuildWiper(cabin, "WiperLeft", VehicleNivaCabin.WiperPivotLeft, VehicleNivaCabin.WiperOuterLeft);
        var right = BuildWiper(cabin, "WiperRight", VehicleNivaCabin.WiperPivotRight, VehicleNivaCabin.WiperOuterRight);
        var tell = BuildHighBeamTell(cabin);
        return new(snow, material, left, right, tell);
    }

    /// <summary>The authored headlight pair, found by the name Headlights() gives it.</summary>
    internal static SpotLight3D[] CabinHeadlights(Node3D cabin)
    {
        var parent = cabin.GetParent();
        if (parent is null)
        {
            return Array.Empty<SpotLight3D>();
        }

        var lamps = new List<SpotLight3D>(2);
        for (var index = 0; index < parent.GetChildCount(); index++)
        {
            if (parent.GetChild(index) is SpotLight3D lamp
                && lamp.Name.ToString().StartsWith("Headlight", StringComparison.Ordinal))
            {
                lamps.Add(lamp);
            }
        }

        return lamps.ToArray();
    }

    private static MeshInstance3D BuildWindscreenSnow(Node3D parent, ShaderMaterial material)
    {
        var outward = VehicleNivaCabin.WindscreenNormal * .006f;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenBottomLeft + outward, 0f, 0f);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenBottomRight + outward, 1f, 0f);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenTopRight + outward, 1f, 1f);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenBottomLeft + outward, 0f, 0f);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenTopRight + outward, 1f, 1f);
        AddSnowVertex(tool, VehicleNivaCabin.WindscreenTopLeft + outward, 0f, 1f);
        var mesh = tool.Commit();
        tool.Dispose();
        mesh.SetMeta("expectedTriangleCorners", 6);
        mesh.SetMeta("manualTriangleCorners", 6);
        var instance = new MeshInstance3D
        {
            Name = "WindscreenSnow",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        instance.SetMeta("presentationOwnership", "presentation-only; snow layer on the authored windscreen glass");
        parent.AddChild(instance);
        return instance;
    }

    private static void AddSnowVertex(SurfaceTool tool, Vector3 position, float u, float v)
    {
        tool.SetNormal(VehicleNivaCabin.WindscreenNormal);
        tool.SetTangent(new Plane(Vector3.Right, 1f));
        tool.SetUV(new Vector2(u, v));
        tool.AddVertex(position);
    }

    /// <summary>
    /// One wiper: a mount whose local X is the parked arm direction, local Y the
    /// in-plane up and local Z the spindle axis, plus a pivot child the node
    /// rotates about local Z. The arm and blade overlay the authored parked
    /// wiper beams, so the parked pose is indistinguishable from the model.
    /// </summary>
    private static Node3D BuildWiper(Node3D parent, string name, Vector3 pivot, Vector3 outer)
    {
        var xAxis = (outer - pivot).Normalized();
        var yAxis = VehicleNivaCabin.WindscreenNormal.Cross(xAxis);
        if (yAxis.Y < 0f)
        {
            yAxis = -yAxis;
        }

        yAxis = yAxis.Normalized();
        var zAxis = xAxis.Cross(yAxis).Normalized();
        var mount = new Node3D
        {
            Name = name + "Mount",
            Transform = new Transform3D(new Basis(xAxis, yAxis, zAxis), pivot)
        };
        parent.AddChild(mount);
        var spinner = new Node3D { Name = name + "Pivot" };
        mount.AddChild(spinner);
        var length = (outer - pivot).Length();
        var arm = new Batch(spinner, name + "ArmMesh");
        // Spindle boss, arm above the rubber blade, blade pressed toward the glass.
        arm.Cylinder(.019f, .016f, new Vector3(0f, 0f, -.003f), "1d1f1d", "plastic", new Vector3(90f, 0f, 0f));
        arm.Beam(new Vector3(.014f, 0f, 0f), new Vector3(length, 0f, 0f), .0068f, "1c1f1c", "plastic");
        arm.Beam(new Vector3(.02f, .006f, -.005f), new Vector3(length - .015f, .006f, -.005f), .0105f, "111311", "rubber");
        arm.Finish();
        spinner.SetMeta("presentationOwnership", "presentation-only; live windscreen wiper");
        return spinner;
    }

    private static MeshInstance3D BuildHighBeamTell(Node3D parent)
    {
        var tell = new MeshInstance3D
        {
            Name = "HighBeamTell",
            Mesh = new BoxMesh { Size = new(.016f, .011f, .0022f) },
            Position = new(-.486f, 1.024f, -.4016f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new(.07f, .11f, .22f),
                EmissionEnabled = true,
                Emission = new(.30f, .52f, .95f),
                EmissionEnergyMultiplier = 1.8f,
                Roughness = .35f,
                MetallicSpecular = .2f
            }
        };
        // The validator recomputes the corner count from the mesh arrays; a
        // hand-written constant drifts whenever a primitive's tessellation
        // changes, so measure the primitive we actually built.
        var tellCorners = 0;
        for (var surface = 0; surface < tell.Mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = tell.Mesh.SurfaceGetArrays(surface);
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            tellCorners += indices.Length > 0 ? indices.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
        }
        tell.Mesh.SetMeta("expectedTriangleCorners", tellCorners);
        tell.Mesh.SetMeta("manualTriangleCorners", tellCorners);
        tell.SetMeta("presentationOwnership",
            "presentation-only; overlay on the authored blue NivaCab_Tell3 dash lamp");
        tell.Visible = false;
        parent.AddChild(tell);
        return tell;
    }

    // ------------------------------------------------------------------------
    // Snow shader. A small procedural layer whose alpha is the snow amount
    // minus the sector the blades have passed: two angular sectors around the
    // authored pivots, each with a soft leading edge that follows the blade.
    // ------------------------------------------------------------------------

    private static Shader? _snowShader;

    private static Shader SnowShader => _snowShader ??= BuildSnowShader();

    private static string Num(float value)
        => value.ToString("0.0#####", System.Globalization.CultureInfo.InvariantCulture);

    private static Shader BuildSnowShader()
    {
        // In-plane basis: X = world X, U = the glass's own up direction.
        var up = (VehicleNivaCabin.WindscreenTopLeft - VehicleNivaCabin.WindscreenBottomLeft).Normalized();
        var shear = -up.X / up.Y;
        var squash = up.Y;
        var leftDirection = VehicleNivaCabin.WiperOuterLeft - VehicleNivaCabin.WiperPivotLeft;
        var rightDirection = VehicleNivaCabin.WiperOuterRight - VehicleNivaCabin.WiperPivotRight;
        var parkLeft = Mathf.Atan2(leftDirection.Dot(up), leftDirection.X);
        var parkRight = Mathf.Atan2(rightDirection.Dot(up), rightDirection.X);
        var pivotLeftU = (VehicleNivaCabin.WiperPivotLeft.X - VehicleNivaCabin.WindscreenBottomLeft.X)
            / (VehicleNivaCabin.WindscreenBottomRight.X - VehicleNivaCabin.WindscreenBottomLeft.X);
        var pivotRightU = (VehicleNivaCabin.WiperPivotRight.X - VehicleNivaCabin.WindscreenBottomLeft.X)
            / (VehicleNivaCabin.WindscreenBottomRight.X - VehicleNivaCabin.WindscreenBottomLeft.X);
        var pivotV = (VehicleNivaCabin.WiperPivotLeft.Y - VehicleNivaCabin.WindscreenBottomLeft.Y)
            / (VehicleNivaCabin.WindscreenTopLeft.Y - VehicleNivaCabin.WindscreenBottomLeft.Y);
        var xSpan = VehicleNivaCabin.WindscreenBottomRight.X - VehicleNivaCabin.WindscreenBottomLeft.X;
        var ySpan = VehicleNivaCabin.WindscreenTopLeft.Y - VehicleNivaCabin.WindscreenBottomLeft.Y;
        var code = SnowShaderTemplate
            .Replace("__SWEEP__", Num(VehicleNivaCabin.WiperSweepRadians))
            .Replace("__X_SPAN__", Num(xSpan))
            .Replace("__Y_SPAN__", Num(ySpan))
            .Replace("__SHEAR__", Num(shear))
            .Replace("__SQUASH__", Num(squash))
            .Replace("__PIVOT_UL__", Num(pivotLeftU))
            .Replace("__PIVOT_UR__", Num(pivotRightU))
            .Replace("__PIVOT_V__", Num(pivotV))
            .Replace("__PARK_L__", Num(parkLeft))
            .Replace("__PARK_R__", Num(parkRight))
            .Replace("__MAX_ALPHA__", Num(VehicleNivaCabin.SnowMaxAlpha));
        return new Shader { Code = code };
    }

    private const string SnowShaderTemplate = """
shader_type spatial;
render_mode cull_disabled, blend_mix, diffuse_burley;

uniform float snow_amount : hint_range(0.0, 1.0) = 0.0;
uniform float wipe_area : hint_range(0.0, 1.0) = 0.0;
uniform float wipe_progress : hint_range(0.0, 1.0) = 0.0;
// VIS-105: the snow on the glass answers the same authored light state as the
// village snow (PainterlyMaterialLibrary.SetSnowMood). It is a ratio against the
// library's neutral snow, so a profile that declares no snow block multiplies by
// vec3(1.0) and the layer keeps the exact colour it had before this existed.
uniform vec3 snow_mood = vec3(1.0);

const float SWEEP = __SWEEP__;
const float X_SPAN = __X_SPAN__;
const float Y_SPAN = __Y_SPAN__;
const float SHEAR = __SHEAR__;
const float SQUASH = __SQUASH__;
const float PIVOT_UL = __PIVOT_UL__;
const float PIVOT_UR = __PIVOT_UR__;
const float PIVOT_V = __PIVOT_V__;
const float PARK_L = __PARK_L__;
const float PARK_R = __PARK_R__;
const float REACH = 0.50;

float cabin_hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float cabin_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(cabin_hash(i), cabin_hash(i + vec2(1.0, 0.0)), f.x),
               mix(cabin_hash(i + vec2(0.0, 1.0)), cabin_hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float wrap_pi(float angle) { return mod(angle + 3.14159265, 6.2831853) - 3.14159265; }

// Plane coordinates from UV: X along the glass, Y up along its own slope.
vec2 plane_from_uv(float u, float v, float pivot_u) {
    float dx = (u - pivot_u) * X_SPAN;
    float dy = (v - PIVOT_V) * Y_SPAN;
    return vec2(dx - dy * SHEAR, dy / SQUASH);
}

float sector_radial(vec2 plane) { return 1.0 - smoothstep(REACH - 0.06, REACH, length(plane)); }

// The wiped sector of one blade, without any wipe progress: only the angular
// span the blade actually sweeps is ever cleared by the persistent area.
float sector_bounds(vec2 plane, float park, float side) {
    float angle = atan(plane.y, plane.x);
    float s = wrap_pi(side * (park - angle)) / SWEEP;
    return sector_radial(plane) * step(0.0, s) * step(s, 1.0);
}

// How much of the fragment's angular sector the current pass has cleared.
float sector_clear(vec2 plane, float park, float side, float progress) {
    float angle = atan(plane.y, plane.x);
    float s = wrap_pi(side * (park - angle)) / SWEEP;
    float swept = step(0.0, s) * step(s, 1.0) * smoothstep(s - 0.10, s + 0.015, progress);
    return sector_radial(plane) * swept;
}

void fragment() {
    vec2 plane_left = plane_from_uv(UV.x, UV.y, PIVOT_UL);
    vec2 plane_right = plane_from_uv(UV.x, UV.y, PIVOT_UR);
    float cleared_blade = max(sector_clear(plane_left, PARK_L, 1.0, wipe_progress),
                              sector_clear(plane_right, PARK_R, -1.0, wipe_progress));
    float cleared_area = max(sector_bounds(plane_left, PARK_L, 1.0),
                             sector_bounds(plane_right, PARK_R, -1.0)) * wipe_area;
    float cleared = clamp(max(cleared_blade, cleared_area), 0.0, 1.0);
    float grain = cabin_noise(UV * vec2(26.0, 9.0)) * 0.65 + cabin_noise(UV * vec2(71.0, 23.0) + 17.0) * 0.35;
    float drift = 1.0 - smoothstep(0.0, 0.8, UV.y);
    float borders = smoothstep(0.0, 0.06, UV.x) * (1.0 - smoothstep(0.94, 1.0, UV.x))
                  * smoothstep(0.0, 0.05, UV.y) * (1.0 - smoothstep(0.95, 1.0, UV.y));
    float density = snow_amount * (0.55 + 0.65 * grain) * mix(0.62, 1.0, drift) * borders;
    density = clamp(density, 0.0, __MAX_ALPHA__) * (1.0 - cleared);
    ALBEDO = vec3(0.90, 0.93, 0.97) * snow_mood;
    ROUGHNESS = 0.93;
    SPECULAR = 0.08;
    ALPHA = density;
}
""";
}
