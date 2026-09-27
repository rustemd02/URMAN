using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>Diagnostic: with URMAN_ATMOSPHERE_DUMP=&lt;file&gt; every applied outdoor atmosphere profile is recorded, so moving values from code to data can be checked for equality.</summary>
public static class AtmosphereDump
{
    private static readonly JsonObject Profiles = [];

    public static void Write(string profile, global::Godot.Environment environment, DirectionalLight3D? sun)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_ATMOSPHERE_DUMP") is not { Length: > 0 } path) return;
        var sky = environment.Sky?.SkyMaterial as ProceduralSkyMaterial;
        Profiles[profile] = new JsonObject
        {
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
        System.IO.File.WriteAllText(path, Profiles.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
