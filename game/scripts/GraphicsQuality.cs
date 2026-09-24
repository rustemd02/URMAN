using Godot;

namespace Urman.Godot;

/// <summary>
/// What each graphics preset costs beyond materials. "low" is for weak and
/// integrated GPUs: FSR upscaling from a lower render scale, hard shadows from
/// a smaller atlas over a shorter distance, coarser mesh LOD, no SSAO. Medium
/// and high keep the look the scenes were authored at; medium's scale and MSAA
/// are the declared startup contract.
/// </summary>
public static class GraphicsQuality
{
    public static string Preset { get; private set; } = "medium";
    public static bool Low => Preset == "low";

    /// <summary>
    /// First-run preset: integrated and software GPUs start on low so the game
    /// is playable before the player finds the setting; everything else medium.
    /// </summary>
    public static string DefaultPreset()
    {
        var adapter = RenderingServer.GetVideoAdapterType();
        return adapter is RenderingDevice.DeviceType.IntegratedGpu or RenderingDevice.DeviceType.Cpu ? "low" : "medium";
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
    public static void ConfigureEnvironment(global::Godot.Environment environment)
    {
        if (Low) environment.SsaoEnabled = false;
    }

    public static void ConfigureSun(DirectionalLight3D sun)
    {
        if (!sun.ShadowEnabled) return;
        sun.DirectionalShadowMode = Low ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Parallel4Splits;
        sun.DirectionalShadowMaxDistance = Preset switch { "low" => 45f, "high" => 120f, _ => 80f };
    }
}
