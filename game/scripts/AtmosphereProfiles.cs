using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>One outdoor light/sky/fog profile from authored data (spec WORLD12).</summary>
public sealed record AtmosphereProfile(
    float AmbientEnergy, Color AmbientColor, float AmbientSky,
    Color FogColor, float FogDensity, float FogHeight, float FogHeightDensity, float FogAerial, float FogSkyAffect, float FogSunScatter,
    float Exposure, float SsaoIntensity, float SsaoRadius,
    Color SkyTop, Color SkyHorizon, Color GroundHorizon, Color GroundBottom, float SunAngleMax, float SunCurve, Color Cover,
    Color SunColor, float SunEnergy, float ShadowOpacity, Vector3 SunRotation);

/// <summary>
/// Outdoor atmosphere profiles, authored in res://content/world/atmosphere.v1.json
/// and edited by URMAN Studio. A missing profile is an error, not a code default.
/// </summary>
public static class AtmosphereProfiles
{
    public const string Path = "res://content/world/atmosphere.v1.json";
    private static Dictionary<string, AtmosphereProfile>? _profiles;

    /// <summary>Studio preview: forget the loaded file so the next zone refresh reads edited values.</summary>
    public static void Reload(string? json = null)
    {
        _profiles = json is null ? null : Parse(json);
    }

    public static AtmosphereProfile Get(string id)
    {
        _profiles ??= Parse(global::Godot.FileAccess.GetFileAsString(Path));
        return _profiles.TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Atmosphere profile {id} is missing from {Path}.");
    }

    public static AtmosphereProfile FromParams(JsonElement p)
    {
        Color C(JsonElement owner, string name) => Color.FromHtml(owner.GetProperty(name).GetString()!);
        float F(JsonElement owner, string name) => owner.GetProperty(name).GetSingle();
        var ambient = p.GetProperty("ambient");
        var fog = p.GetProperty("fog");
        var ssao = p.GetProperty("ssao");
        var sky = p.GetProperty("sky");
        var sun = p.GetProperty("sun");
        var cover = sky.GetProperty("cover");
        var rotation = sun.GetProperty("rotation");
        return new(
            F(ambient, "energy"), C(ambient, "color"), F(ambient, "skyContribution"),
            C(fog, "color"), F(fog, "density"), F(fog, "height"), F(fog, "heightDensity"), F(fog, "aerialPerspective"), F(fog, "skyAffect"), F(fog, "sunScatter"),
            F(p, "exposure"), F(ssao, "intensity"), F(ssao, "radius"),
            C(sky, "top"), C(sky, "horizon"), C(sky, "groundHorizon"), C(sky, "groundBottom"), F(sky, "sunAngleMax"), F(sky, "sunCurve"),
            new Color(cover[0].GetSingle(), cover[1].GetSingle(), cover[2].GetSingle(), cover[3].GetSingle()),
            C(sun, "color"), F(sun, "energy"), F(sun, "shadowOpacity"), new Vector3(rotation[0].GetSingle(), rotation[1].GetSingle(), 0f));
    }

    private static Dictionary<string, AtmosphereProfile> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("entities").EnumerateArray()
            .ToDictionary(entity => entity.GetProperty("params").GetProperty("profile").GetString()!, entity => FromParams(entity.GetProperty("params")), StringComparer.Ordinal);
    }
}
