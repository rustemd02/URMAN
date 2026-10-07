using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>Diagnostic: with URMAN_ATMOSPHERE_DUMP=&lt;file&gt; every applied outdoor atmosphere profile is recorded, so moving values from code to data can be checked for equality. A frame now states which profile was actually applied (zone default or URMAN_ATMOSPHERE_PHASE capture override), its authored identity and its snow response.</summary>
public static class AtmosphereDump
{
    private static readonly JsonObject Profiles = [];

    public static void Write(string profile, global::Godot.Environment environment, DirectionalLight3D? sun, AtmosphereProfile? authored = null)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ATMOSPHERE_DUMP") is not { Length: > 0 } path) return;
        var sky = environment.Sky?.SkyMaterial as ProceduralSkyMaterial;
        var entry = new JsonObject
        {
            ["appliedProfile"] = profile,
            ["identity"] = authored?.Identity,
            ["ambientEnergy"] = environment.AmbientLightEnergy, ["ambientColor"] = environment.AmbientLightColor.ToHtml(),
            ["ambientSky"] = environment.AmbientLightSkyContribution, ["fogColor"] = environment.FogLightColor.ToHtml(),
            ["fogDensity"] = environment.FogDensity, ["fogHeight"] = environment.FogHeight, ["fogHeightDensity"] = environment.FogHeightDensity,
            ["fogAerial"] = environment.FogAerialPerspective, ["fogSky"] = environment.FogSkyAffect, ["fogSun"] = environment.FogSunScatter,
            ["exposure"] = environment.TonemapExposure, ["ssao"] = environment.SsaoIntensity,
            ["skyTop"] = sky?.SkyTopColor.ToHtml(), ["skyHorizon"] = sky?.SkyHorizonColor.ToHtml(), ["groundHorizon"] = sky?.GroundHorizonColor.ToHtml(),
            ["groundBottom"] = sky?.GroundBottomColor.ToHtml(), ["sunAngle"] = sky?.SunAngleMax, ["cover"] = sky?.SkyCoverModulate.ToHtml(),
            ["sunColor"] = sun?.LightColor.ToHtml(), ["sunEnergy"] = sun?.LightEnergy, ["shadowOpacity"] = sun?.ShadowOpacity,
            ["sunRotation"] = sun is null ? null : $"{sun.RotationDegrees.X:0.###},{sun.RotationDegrees.Y:0.###}"
        };
        if (authored is not null)
        {
            entry["snowColor"] = authored.SnowColor.ToHtml();
            entry["snowCoverage"] = authored.SnowCoverage;
            entry["snowSparkle"] = authored.SnowSparkle;
            entry["snowTintStrength"] = authored.SnowTintStrength;
        }
        Profiles[profile] = entry;
        System.IO.File.WriteAllText(path, Profiles.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
