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
        if (viewport.World3D?.Environment is { } environment) ConfigureEnvironment(environment);
        foreach (var sun in viewport.GetTree().Root.FindChildren("*", nameof(DirectionalLight3D), true, false).OfType<DirectionalLight3D>())
            ConfigureSun(sun);
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
    }
}
