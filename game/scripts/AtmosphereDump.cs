using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>Diagnostic: with URMAN_ATMOSPHERE_DUMP=&lt;file&gt; every applied outdoor atmosphere profile is recorded, so moving values from code to data can be checked for equality. A frame now states which profile was actually applied (zone default from authored data, URMAN_ATMOSPHERE_PHASE capture override or Studio preview), its identity, snow response, the authored weather transcription used by the presentation systems (VIS-043) and the VIS-042 near-plane readability audit.</summary>
public static class AtmosphereDump
{
    private static readonly JsonObject Profiles = [];

    public static void Write(
        string profile,
        global::Godot.Environment environment,
        DirectionalLight3D? sun,
        AtmosphereProfile? authored = null,
        string selectorSource = "zone",
        float nearPlaneLuminance = 0f,
        bool blackFrameRisk = false)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ATMOSPHERE_DUMP") is not { Length: > 0 } path) return;
        var sky = environment.Sky?.SkyMaterial as ProceduralSkyMaterial;
        var entry = new JsonObject
        {
            ["appliedProfile"] = profile,
            ["identity"] = authored?.Identity,
            // VIS-040/042: which path chose this state. A capture that says
            // "zone" is the ordinary play frame; "capturePhaseOverride" is a
            // staged authored state and is labelled as such in the receipt.
            ["selectorSource"] = selectorSource,
            ["phaseOnlyState"] = authored?.PhaseOnly,
            ["ambientEnergy"] = environment.AmbientLightEnergy, ["ambientColor"] = environment.AmbientLightColor.ToHtml(),
            ["ambientSky"] = environment.AmbientLightSkyContribution, ["fogColor"] = environment.FogLightColor.ToHtml(),
            ["fogDensity"] = environment.FogDensity, ["fogHeight"] = environment.FogHeight, ["fogHeightDensity"] = environment.FogHeightDensity,
            ["fogAerial"] = environment.FogAerialPerspective, ["fogSky"] = environment.FogSkyAffect, ["fogSun"] = environment.FogSunScatter,
            ["exposure"] = environment.TonemapExposure, ["ssao"] = environment.SsaoIntensity,
            ["skyTop"] = sky?.SkyTopColor.ToHtml(), ["skyHorizon"] = sky?.SkyHorizonColor.ToHtml(), ["groundHorizon"] = sky?.GroundHorizonColor.ToHtml(),
            ["groundBottom"] = sky?.GroundBottomColor.ToHtml(), ["skyAngle"] = sky?.SunAngleMax, ["cover"] = sky?.SkyCoverModulate.ToHtml(),
            ["sunColor"] = sun?.LightColor.ToHtml(), ["sunEnergy"] = sun?.LightEnergy, ["shadowOpacity"] = sun?.ShadowOpacity,
            ["sunRotation"] = sun is null ? null : $"{sun.RotationDegrees.X:0.###},{sun.RotationDegrees.Y:0.###}"
        };
        if (authored is not null)
        {
            entry["snowColor"] = authored.SnowColor.ToHtml();
            entry["snowCoverage"] = authored.SnowCoverage;
            entry["snowSparkle"] = authored.SnowSparkle;
            entry["snowTintStrength"] = authored.SnowTintStrength;
            entry["snowEffectiveTint"] = authored.EffectiveSnowColor.ToHtml();
            // VIS-042: the authored readability floor check for this state.
            entry["nearPlaneLuminance"] = nearPlaneLuminance;
            entry["blackFrameRisk"] = blackFrameRisk;
            // VIS-043: the one authored transcription of the weather constants the
            // presentation systems read, plus the two budgets to be measured
            // separately per weather state at the fixed observation point.
            entry["windDirection"] = $"{authored.WindDirection.X:0.###},{authored.WindDirection.Y:0.###}";
            entry["windSpeed"] = authored.WindSpeed;
            entry["blizzardSpeed"] = authored.BlizzardSpeed;
            entry["flakeBudget"] = authored.FlakeBudget;
            entry["blizzardFlakeBudget"] = authored.BlizzardFlakeBudget;
            entry["windStreakStretch"] = authored.StreakStretch;
            entry["plumeColor"] = authored.SmokeColor.ToHtml();
            entry["plumeOpacity"] = authored.SmokeOpacity;
        }
        Profiles[profile] = entry;
        System.IO.File.WriteAllText(path, Profiles.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
