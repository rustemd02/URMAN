using System.Globalization;
using Godot;

namespace Urman.Godot;

/// <summary>
/// What each graphics preset costs beyond materials. All presets below high
/// render 3D under a pixel budget (FSR 1 upscaling), use FXAA instead of MSAA,
/// two shadow cascades and half-resolution cheap-sample SSAO/SSIL. "low" is for
/// weak and integrated GPUs: a smaller budget, a shorter shadow range, coarser
/// mesh LOD, no SSAO/SSIL. High keeps the full authored look.
/// </summary>
public static class GraphicsQuality
{
    public static string Preset { get; private set; } = "medium";
    /// <summary>Interior lamps whose soft shadows follow the preset (on for high only).</summary>
    public const string SoftShadowLampGroup = "urman_soft_shadow_lamp";
    public static bool Low => Preset == "low";

    // VIS-029 readback of the raster/LOD/shadow budget the last Apply() published.
    // These are copies of values Apply already computes: no new writer, no preset
    // number changed, nothing here can alter a frame. The defaults mirror the
    // "medium" tuple so a snapshot taken before any Apply still describes the
    // declared startup contract instead of zeros. A before/after pair on the station
    // must show the same string on both sides, otherwise the two runs measured
    // different budgets and the comparison is void (the card forbids comparing
    // presets against each other).
    private static float _meshLodThreshold = 2.2f;
    private static int _directionalShadowAtlas = 4096;
    private static int _positionalShadowAtlas = 1024;
    private static float _presetShadowDistance = 56f;
    private static float _scaling3DScale = .85f;
    private static string _msaa = "Disabled";
    private static string _shadowFilter = "SoftVeryLow";

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

    // Pixel budget: the 3D render target never exceeds this many pixels, however
    // large the window or the Retina backing store is. A full-screen Retina window
    // is 4+ megapixels; shading, fog, SSAO and shadows all scale with that number,
    // while FSR 1 hides the lower internal resolution. Presets still cap the scale
    // from above, so a small window keeps its higher scale.
    private static long PixelBudget(string preset) => preset switch
    {
        "low" => 1_300_000L,
        "high" => 0L,
        _ => 2_000_000L
    };

    private static readonly HashSet<ulong> HookedViewports = new();

    private static Vector2I RenderSurface(Viewport viewport) =>
        viewport is Window window ? window.Size : (Vector2I)viewport.GetVisibleRect().Size;

    private static float BudgetedScale(Viewport viewport, float presetScale)
    {
        var budget = PixelBudget(Preset);
        if (budget <= 0) return presetScale;
        var surface = RenderSurface(viewport);
        var pixels = (long)surface.X * surface.Y;
        if (pixels <= 0) return presetScale;
        var fitted = Mathf.Sqrt((float)budget / pixels);
        return Mathf.Clamp(Mathf.Min(presetScale, fitted), .5f, 1f);
    }

    private static void ApplyScale(Viewport viewport, float presetScale)
    {
        var scale = ProbeScaleOverride() ?? BudgetedScale(viewport, presetScale);
        // FSR 1 keeps edges sharp at a reduced scale where bilinear blurs them.
        viewport.Scaling3DMode = scale < .995f && Preset != "high" ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        viewport.Scaling3DScale = scale;
        _scaling3DScale = scale;
    }

    private static float PresetScale() => Preset switch { "low" => .65f, "high" => 1f, _ => .85f };

    public static void Apply(Viewport viewport, string preset)
    {
        Preset = preset is "low" or "high" ? preset : "medium";
        // Medium and low drop MSAA for FXAA: MSAA multiplies the cost of every
        // overdraw-heavy foliage pass, FXAA is one cheap post pass.
        var (msaa, lodThreshold, atlas, positionalAtlas) = Preset switch
        {
            "low" => (Viewport.Msaa.Disabled, 4f, 2048, 1024),
            "high" => (Viewport.Msaa.Msaa4X, 1f, 4096, 4096),
            _ => (Viewport.Msaa.Disabled, 2.2f, 4096, 1024)
        };
        ApplyScale(viewport, PresetScale());
        if (viewport.GetInstanceId() is var id && HookedViewports.Add(id))
            viewport.SizeChanged += () =>
            {
                if (GodotObject.IsInstanceValid(viewport)) ApplyScale(viewport, PresetScale());
            };
        viewport.Msaa3D = msaa;
        viewport.ScreenSpaceAA = Preset == "high" ? Viewport.ScreenSpaceAAEnum.Disabled : Viewport.ScreenSpaceAAEnum.Fxaa;
        viewport.MeshLodThreshold = lodThreshold;
        viewport.PositionalShadowAtlasSize = positionalAtlas;
        RenderingServer.DirectionalShadowAtlasSetSize(atlas, true);
        var filter = Preset switch
        {
            "low" => RenderingServer.ShadowQuality.SoftVeryLow,
            "high" => RenderingServer.ShadowQuality.SoftMedium,
            _ => RenderingServer.ShadowQuality.SoftVeryLow
        };
        RenderingServer.DirectionalSoftShadowFilterSetQuality(filter);
        RenderingServer.PositionalSoftShadowFilterSetQuality(filter);
        // Screen-space effects run at half resolution with the cheapest sample counts
        // below high; high keeps the project defaults.
        if (Preset == "high")
        {
            RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Medium, true, .5f, 2, 2f, 50f);
            RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.Medium, true, .5f, 4, 2f, 50f);
        }
        else
        {
            RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.VeryLow, true, .5f, 1, 2f, 50f);
            RenderingServer.EnvironmentSetSsilQuality(RenderingServer.EnvironmentSsilQuality.VeryLow, true, .5f, 1, 2f, 50f);
        }
        // VIS-029 readback: record what this Apply call published. The values are
        // already computed here; storing them adds no writer and changes no preset
        // number. BudgetSnapshot is taken after the probe scale override above so it
        // describes the frame the engine really draws, not only the preset name.
        _meshLodThreshold = lodThreshold;
        _positionalShadowAtlas = positionalAtlas;
        _directionalShadowAtlas = atlas;
        _shadowFilter = filter.ToString();
        _msaa = msaa.ToString();
        _presetShadowDistance = ShadowDistance();
        _scaling3DScale = viewport.Scaling3DScale;
        if (viewport.World3D?.Environment is { } environment) ConfigureEnvironment(environment);
        foreach (var sun in viewport.GetTree().Root.FindChildren("*", nameof(DirectionalLight3D), true, false).OfType<DirectionalLight3D>())
            ConfigureSun(sun);
        // P2: soft lamp shadows in the walk-in rooms cost cube shadow maps, so only high pays.
        foreach (var lamp in viewport.GetTree().GetNodesInGroup(SoftShadowLampGroup).OfType<Light3D>())
            lamp.ShadowEnabled = Preset == "high";
    }

    private static float ShadowDistance() => Preset switch { "low" => 36f, "high" => 120f, _ => 56f };

    /// <summary>Sun meta: a scene whose fog hides everything past N metres says so, and the shadow range shrinks to it.</summary>
    public const string ShadowReachMeta = "graphicsShadowReach";

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
    /// <param name="authoredSsao">The owner's SSAO intent; passing it also records the owner's ambient energy.</param>
    /// <param name="interiorGi">True for a closed room: SDFGI uses fine, short cascades and no sky light.</param>
    public static void ConfigureEnvironment(global::Godot.Environment environment, bool? authoredSsao = null, bool? interiorGi = null)
    {
        // Remember the scene's intent, not the result of the previous preset.
        const string key = "graphicsAuthoredSsao";
        var enabled = authoredSsao ?? environment.GetMeta(key, environment.SsaoEnabled).AsBool();
        environment.SetMeta(key, enabled);
        environment.SsaoEnabled = enabled && !Low;

        // P2 / VIS-097/115/116: the frame looked flat because no light bounced at all.
        // The owner writes its values first and then calls here with authoredSsao, so
        // that call records the authored ambient; preset changes reuse the record and
        // never compound the scale.
        const string ambientKey = "graphicsAuthoredAmbient";
        if (authoredSsao is not null || !environment.HasMeta(ambientKey))
            environment.SetMeta(ambientKey, environment.AmbientLightEnergy);
        var authoredAmbient = environment.GetMeta(ambientKey).AsSingle();
        const string scopeKey = "graphicsInteriorGi";
        var interior = interiorGi ?? environment.GetMeta(scopeKey, false).AsBool();
        environment.SetMeta(scopeKey, interior);

        // Screen-space indirect light: soft colour bounce and contact warmth from
        // lit walls, floors and snow. Medium and high; low keeps its budget.
        environment.SsilEnabled = !Low;
        environment.SsilRadius = Preset == "high" ? 5f : 3f;
        environment.SsilIntensity = Preset == "high" ? 1.15f : 0.95f;
        environment.SsilSharpness = 0.98f;
        environment.SsilNormalRejection = 1f;

        // Signed-distance GI on high only: real multi-bounce light in rooms and off
        // the snow. With GI carrying the fill, the flat ambient floor is lowered so
        // the scene does not simply get brighter; shadows gain colour, not grey.
        var sdfgi = Preset == "high";
        environment.SdfgiEnabled = sdfgi;
        if (sdfgi)
        {
            environment.SdfgiUseOcclusion = true;
            environment.SdfgiReadSkyLight = !interior;
            environment.SdfgiBounceFeedback = interior ? 0.45f : 0.3f; // docs: > 0.5 risks a feedback loop
            environment.SdfgiCascades = interior ? 2 : 4;
            environment.SdfgiMinCellSize = interior ? 0.08f : 0.2f;
            environment.SdfgiEnergy = 1f;
            environment.SdfgiNormalBias = 1.1f;
            environment.SdfgiProbeBias = 1.1f;
        }
        environment.AmbientLightEnergy = authoredAmbient * (sdfgi ? 0.6f : 1f);

        // P2: a restrained bloom on high only, so lit windows and lamps bleed a little
        // into the night. The HDR threshold sits above any diffuse surface, so only
        // emissive glass and lamp discs bloom; the owner's own glow switch is still
        // honoured for every other preset (Glow stays off on medium and low).
        var glow = Preset == "high";
        environment.GlowEnabled = glow;
        if (glow)
        {
            environment.GlowIntensity = 0.55f;
            environment.GlowStrength = 0.85f;
            environment.GlowBloom = 0f;
            environment.GlowHdrThreshold = 1.15f;
            environment.GlowHdrScale = 1.5f;
            environment.GlowBlendMode = global::Godot.Environment.GlowBlendModeEnum.Softlight;
        }

        // Screen-space reflections: varnished parquet and glass in closed rooms on high.
        environment.SsrEnabled = Preset == "high" && interior;
        if (environment.SsrEnabled)
        {
            environment.SsrMaxSteps = 48;
            environment.SsrFadeIn = 0.15f;
            environment.SsrFadeOut = 2f;
            environment.SsrDepthTolerance = 0.2f;
        }
    }

    public static void ConfigureSun(DirectionalLight3D sun)
    {
        // P2: soft penumbrae instead of razor shadow edges. The angular size of the
        // winter sun disc widens the PCSS blur with distance from the caster (real
        // sun ≈ 0.53°; a hazy winter sky spreads it). Low filters without angular penumbrae.
        // PCSS blocker search is paid per shadowed pixel; only high keeps it.
        sun.LightAngularDistance = Preset == "high" ? 1.6f : 0f;
        if (!sun.ShadowEnabled) return;
        // Each cascade re-draws every caster in its range, so the split count is
        // the biggest shadow lever in a forest. Below high: two cascades.
        sun.DirectionalShadowMode = Preset == "high" ? DirectionalLight3D.ShadowMode.Parallel4Splits : DirectionalLight3D.ShadowMode.Parallel2Splits;
        var distance = ShadowDistance();
        if (sun.HasMeta(ShadowReachMeta)) distance = Mathf.Min(distance, sun.GetMeta(ShadowReachMeta).AsSingle());
        sun.DirectionalShadowMaxDistance = distance;
        ApplyProbeShadowOverride(sun);
    }
}
