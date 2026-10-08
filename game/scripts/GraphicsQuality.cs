using System.Globalization;
using Godot;

namespace Urman.Godot;

/// <summary>
/// What each graphics preset costs beyond materials. "low" is for weak and
/// lower-power integrated GPUs: FSR upscaling from a lower render scale, hard shadows from
/// a smaller atlas over a shorter distance, coarser mesh LOD, no SSAO. Medium
/// and high keep the look the scenes were authored at; medium's scale and MSAA
/// are the declared startup contract.
/// </summary>
public static class GraphicsQuality
{
    public static string Preset { get; private set; } = "medium";
    public static bool Low => Preset == "low";

    // VIS-029 readback of the raster/LOD/shadow budget the last Apply() published.
    // These are copies of values Apply already computes: no new writer, no preset
    // number changed, nothing here can alter a frame. The defaults mirror the
    // "medium" tuple so a snapshot taken before any Apply still describes the
    // declared startup contract instead of zeros. A before/after pair on the station
    // must show the same string on both sides, otherwise the two runs measured
    // different budgets and the comparison is void (the card forbids comparing
    // presets against each other).
    private static float _meshLodThreshold = 1.5f;
    private static int _directionalShadowAtlas = 4096;
    private static int _positionalShadowAtlas = 2048;
    private static float _presetShadowDistance = 80f;
    private static float _scaling3DScale = .9f;
    private static string _msaa = "Msaa2X";
    private static string _shadowFilter = "SoftLow";

    /// <summary>Coarse mesh LOD threshold currently in force for the gameplay viewport.</summary>
    public static float MeshLodThreshold => _meshLodThreshold;

    /// <summary>
    /// One machine-readable line describing the cost budget of the current frame.
    /// Called from measurement sites only (foliage census, receipts); it never
    /// applies anything. The line is assembled from invariant fragments because
    /// joining interpolated strings with <c>+</c> yields a plain string, which does
    /// not bind to <c>FormattableString.Invariant</c> and would print the numbers
    /// in the current culture.
    /// </summary>
    public static string BudgetSnapshot() => string.Concat(
        System.FormattableString.Invariant($"preset={Preset},lod_threshold={_meshLodThreshold:F2},scale={_scaling3DScale:F2},msaa={_msaa},"),
        System.FormattableString.Invariant($"shadow_filter={_shadowFilter},directional_shadow_distance={_presetShadowDistance:F0},"),
        System.FormattableString.Invariant($"directional_shadow_atlas={_directionalShadowAtlas},positional_shadow_atlas={_positionalShadowAtlas},"),
        System.FormattableString.Invariant($"ssao_gate={(Low ? "off" : "authored")},probe={ProbeOverrideSummary()}"));

    /// <summary>
    /// Apple silicon's unified-memory GPU reports IntegratedGpu; that does not
    /// make it a low-end adapter. Other integrated and software GPUs retain low.
    /// </summary>
    public static string DefaultPreset()
    {
        var adapter = RenderingServer.GetVideoAdapterType();
        var appleSilicon = RenderingServer.GetVideoAdapterName().StartsWith("Apple M", StringComparison.Ordinal);
        return adapter == RenderingDevice.DeviceType.Cpu
            || (adapter == RenderingDevice.DeviceType.IntegratedGpu && !appleSilicon) ? "low" : "medium";
    }

    public static void Apply(Viewport viewport, string preset)
    {
        Preset = preset is "low" or "high" ? preset : "medium";
        var (scale, msaa, lodThreshold, atlas, positionalAtlas) = Preset switch
        {
            "low" => (.7f, Viewport.Msaa.Disabled, 4f, 2048, 1024),
            "high" => (1f, Viewport.Msaa.Msaa4X, 1f, 4096, 4096),
            _ => (.9f, Viewport.Msaa.Msaa2X, 1.5f, 4096, 2048)
        };
        // FSR 1 keeps edges sharp at a reduced scale where bilinear blurs them.
        viewport.Scaling3DMode = scale < 1f && Low ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = scale;
        viewport.Msaa3D = msaa;
        viewport.ScreenSpaceAA = Low ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        viewport.MeshLodThreshold = lodThreshold;
        viewport.PositionalShadowAtlasSize = positionalAtlas;
        RenderingServer.DirectionalShadowAtlasSetSize(atlas, true);
        var filter = Preset switch
        {
            "low" => RenderingServer.ShadowQuality.Hard,
            "high" => RenderingServer.ShadowQuality.SoftMedium,
            _ => RenderingServer.ShadowQuality.SoftLow
        };
        RenderingServer.DirectionalSoftShadowFilterSetQuality(filter);
        RenderingServer.PositionalSoftShadowFilterSetQuality(filter);
        // VIS-029 readback: record what this Apply call published. The values are
        // already computed here; storing them adds no writer and changes no preset
        // number. BudgetSnapshot is taken after the probe scale override below so it
        // describes the frame the engine really draws, not only the preset name.
        _meshLodThreshold = lodThreshold;
        _positionalShadowAtlas = positionalAtlas;
        _directionalShadowAtlas = atlas;
        _shadowFilter = filter.ToString();
        _msaa = msaa.ToString();
        _presetShadowDistance = Preset switch { "low" => 45f, "high" => 120f, _ => 80f };
        if (ProbeScaleOverride() is { } probeScale) viewport.Scaling3DScale = probeScale;
        _scaling3DScale = viewport.Scaling3DScale;
        if (viewport.World3D?.Environment is { } environment) ConfigureEnvironment(environment);
        foreach (var sun in viewport.GetTree().Root.FindChildren("*", nameof(DirectionalLight3D), true, false).OfType<DirectionalLight3D>())
            ConfigureSun(sun);
    }

    // Optional, explicitly requested A/B knobs for the existing performance probe.
    // They are read only when the command line carries --urman-perf-probe, they
    // never change a preset, and an ordinary launch cannot reach them, so a
    // shipping frame is untouched. Their purpose is to separate costs that the
    // presets bundle together: render scale isolates pixel/fill cost, and the
    // directional shadow split count or distance isolates the shadow pass.
    private const string ProbeFlag = "--urman-perf-probe";
    private static bool _probeOverridesResolved;
    private static bool _probeOverridesActive;
    private static float _probeScale;
    private static int _probeShadowSplits;
    private static float _probeShadowDistance;

    private static void ResolveProbeOverrides()
    {
        if (_probeOverridesResolved) return;
        _probeOverridesResolved = true;
        // The probe is enabled by --urman-perf-probe or any --urman-perf-probe-*
        // option (the room's runner passes --urman-perf-probe-mode=...), exactly the
        // rule Act1DemoRoot uses to arm the probe.
        foreach (var argument in OS.GetCmdlineArgs())
            if (argument.StartsWith(ProbeFlag, StringComparison.Ordinal)) { _probeOverridesActive = true; break; }
        if (!_probeOverridesActive) return;
        _probeScale = ReadProbeFloat("URMAN_PERF_SCALE", 0f, 1f);
        _probeShadowDistance = ReadProbeFloat("URMAN_PERF_SHADOW_DISTANCE", 0f, 1000f);
        var splits = ReadProbeFloat("URMAN_PERF_SHADOW_SPLITS", 0f, 4f);
        _probeShadowSplits = splits is 2f or 4f ? (int)splits : 0;
    }

    private static float ReadProbeFloat(string name, float minimum, float maximum)
    {
        var raw = OS.GetEnvironment(name);
        if (string.IsNullOrWhiteSpace(raw)) return 0f;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value) || value < minimum || value > maximum)
            throw new InvalidOperationException($"{name} must be a number in [{minimum}, {maximum}] for a probe run.");
        return value;
    }

    private static float? ProbeScaleOverride()
    {
        ResolveProbeOverrides();
        return _probeOverridesActive && _probeScale > 0f ? _probeScale : null;
    }

    /// <summary>What an optional probe A/B actually changed, for the receipt.</summary>
    public static string ProbeOverrideSummary()
    {
        ResolveProbeOverrides();
        if (!_probeOverridesActive) return "none";
        var parts = new List<string>(3);
        if (_probeScale > 0f) parts.Add($"scale={_probeScale.ToString("F2", CultureInfo.InvariantCulture)}");
        if (_probeShadowSplits > 0) parts.Add($"shadow_splits={_probeShadowSplits}");
        if (_probeShadowDistance > 0f) parts.Add($"shadow_distance={_probeShadowDistance.ToString("F1", CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? "none" : string.Join(",", parts);
    }

    private static void ApplyProbeShadowOverride(DirectionalLight3D sun)
    {
        ResolveProbeOverrides();
        if (!_probeOverridesActive) return;
        if (_probeShadowSplits == 2) sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits;
        else if (_probeShadowSplits == 4) sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        if (_probeShadowDistance > 0f) sun.DirectionalShadowMaxDistance = _probeShadowDistance;
    }

    /// <summary>Environment effects the preset may switch off; called again whenever a zone rebuilds its environment.</summary>
    public static void ConfigureEnvironment(global::Godot.Environment environment, bool? authoredSsao = null)
    {
        // Remember the scene's intent, not the result of the previous preset.
        const string key = "graphicsAuthoredSsao";
        var enabled = authoredSsao ?? environment.GetMeta(key, environment.SsaoEnabled).AsBool();
        environment.SetMeta(key, enabled);
        environment.SsaoEnabled = enabled && !Low;
    }

    public static void ConfigureSun(DirectionalLight3D sun)
    {
        if (!sun.ShadowEnabled) return;
        sun.DirectionalShadowMode = Low ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Parallel4Splits;
        sun.DirectionalShadowMaxDistance = Preset switch { "low" => 45f, "high" => 120f, _ => 80f };
        ApplyProbeShadowOverride(sun);
    }
}
