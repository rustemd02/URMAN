using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// One outdoor light/sky/fog profile from authored data (spec WORLD12).
/// VIS-067 colour script: two blocks are optional and backwards compatible —
/// <c>snow</c> (per-profile snow/ground colour response, STYLE RECIPE W1/W3) and
/// <c>identity</c> (the short authored-state name). A profile without them keeps
/// the previous neutral snow look (<see cref="NeutralSnowColor"/>, tint 0), so
/// nothing that predates the colour script changes.
///
/// VIS-040/042/043 add three more optional blocks, all backwards compatible:
/// <c>zones</c> (now the single selector the code reads — see
/// <see cref="AtmosphereProfiles.ResolveZoneProfileId"/>), <c>weather</c> (the
/// one authored transcription of the existing weather owner's snowfall constants,
/// so falling snow, wind streaks and chimney drift stop contradicting each other
/// across a transition) and <c>plume</c> (the chimney smoke's own colour response,
/// VIS-043/VIS-072). A profile without weather/plume keeps exactly today's
/// behaviour.
/// </summary>
public sealed record AtmosphereProfile(
    float AmbientEnergy, Color AmbientColor, float AmbientSky,
    Color FogColor, float FogDensity, float FogHeight, float FogHeightDensity, float FogAerial, float FogSkyAffect, float FogSunScatter,
    float Exposure, float SsaoIntensity, float SsaoRadius,
    Color SkyTop, Color SkyHorizon, Color GroundHorizon, Color GroundBottom, float SunAngleMax, float SunCurve, Color Cover,
    Color SunColor, float SunEnergy, float ShadowOpacity, Vector3 SunRotation,
    Color SnowColor, float SnowCoverage, float SnowSparkle, float SnowTintStrength, string Identity,
    IReadOnlyList<string> Zones,
    Vector2 WindDirection, float WindSpeed, float BlizzardSpeed, float FlakeBudget, float BlizzardFlakeBudget, float StreakStretch,
    Color SmokeColor, float SmokeOpacity, bool HasPlumeBlock)
{
    /// <summary>The painterly shader's own default snow albedo. A missing snow
    /// block resolves here with tint 0, so the profile is visually unchanged.</summary>
    public static readonly Color NeutralSnowColor = new(0.93f, 0.95f, 0.97f);

    /// <summary>True when no zone claims the state: it is reachable only through
    /// the capture override (URMAN_ATMOSPHERE_PHASE) or Studio preview, never by
    /// accident in ordinary play. This is what keeps the golden hour from becoming
    /// a permanent filter (STYLE RECIPE §8 «вечный золотой закат»).</summary>
    public bool PhaseOnly => Zones.Count == 0;

    /// <summary>The single snow blend rule. PainterlyMaterialLibrary applies the
    /// same expression to the surface shader; the airborne snow tint uses this one
    /// so falling flakes and the ground cannot drift apart (VIS-043).</summary>
    public Color EffectiveSnowColor =>
        NeutralSnowColor.Lerp(SnowColor, Mathf.Clamp(SnowTintStrength, 0f, 1f));

    /// <summary>
    /// A cheap authored estimate of what a near-plane, fully lit surface returns
    /// before shadows: ambient and key contributions weighted by the tone-map
    /// exposure. VIS-042's "a pure black frame is a STOP" rule: it is checked on
    /// the values, so a night state that cannot show three spatial planes fails
    /// loudly at load time instead of shipping as a dark rectangle.
    /// </summary>
    public float NearPlaneLuminance =>
        Exposure * (AmbientEnergy * Luma(AmbientColor) + SunEnergy * Luma(SunColor) * (1f - ShadowOpacity));

    /// <summary>Below this the frame has no readable near plane (VIS-042).</summary>
    public const float BlackFrameLuminanceFloor = 0.055f;

    private static float Luma(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
}

/// <summary>
/// Outdoor atmosphere profiles, authored in res://content/world/atmosphere.v1.json
/// and edited by URMAN Studio. A missing profile is an error, not a code default.
/// </summary>
public static class AtmosphereProfiles
{
    public const string Path = "res://content/world/atmosphere.v1.json";

    /// <summary>
    /// The weather owner's own snowfall constants (AgentBAct1ExteriorLayer:
    /// direction (-1, -.28, .22), velocity 6–10 m/s fair and 11–16 m/s in the
    /// opening blizzard, 4000/5200 flakes). Transcribed here once so the smoke,
    /// the streaked flake shader and the capture readback all read one authored
    /// value instead of a second hard-coded copy. It does not move the emitter:
    /// the weather owner stays the only writer of the particle process parameters.
    /// The vector type is spelled out below: a target-typed <c>new(...)</c> followed
    /// by <c>.Normalized()</c> has no target type at that point.
    /// </summary>
    public static readonly Vector2 AuthoredWindDirection = new Vector2(-1f, 0.22f).Normalized();
    public const float AuthoredWindSpeed = 8f;
    public const float AuthoredBlizzardSpeed = 13.5f;
    public const float AuthoredFlakeBudget = 4000f;
    public const float AuthoredBlizzardFlakeBudget = 5200f;
    public const float AuthoredStreakStretch = 0.012f;

    /// <summary>Today's plume look, kept as the default so a profile without a
    /// <c>local</c> block cannot change the chimney smoke.</summary>
    public static readonly Color DefaultSmokeColor = new(0.50f, 0.52f, 0.54f);
    public const float DefaultSmokeOpacity = 0.36f;

    private static Dictionary<string, AtmosphereProfile>? _profiles;
    private static List<string>? _order;

    /// <summary>The profile actually applied to the outdoor environment, so the
    /// presentation systems (chimney drift, plume tint) can read the authored
    /// weather/local values without becoming a second atmosphere owner.</summary>
    public static string AppliedProfileId { get; private set; } = string.Empty;
    public static AtmosphereProfile? Applied { get; private set; }

    /// <summary>Called by the single atmosphere writer right after it applies a
    /// profile. Presentation consumers only read it; nobody else selects a state.</summary>
    public static void MarkApplied(string profileId, AtmosphereProfile profile)
    {
        AppliedProfileId = profileId;
        Applied = profile;
    }

    /// <summary>Studio preview: forget the loaded file so the next zone refresh reads edited values.</summary>
    public static void Reload(string? json = null)
    {
        if (json is null)
        {
            // Dropping the cache is what makes the next EnsureLoaded() read the
            // file on disk again; the authored order has to be dropped with it,
            // otherwise ResolveZoneProfileId would walk stale claims.
            _profiles = null;
            _order = null;
            return;
        }

        var parsed = Parse(json);
        _profiles = parsed.Profiles;
        _order = parsed.Order;
    }

    private static void EnsureLoaded()
    {
        if (_profiles is not null) return;
        var parsed = Parse(global::Godot.FileAccess.GetFileAsString(Path));
        _profiles = parsed.Profiles;
        _order = parsed.Order;
    }

    public static AtmosphereProfile Get(string id)
    {
        EnsureLoaded();
        return _profiles!.TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Atmosphere profile {id} is missing from {Path}.");
    }

    /// <summary>True when the authored file carries this profile id. Used by the
    /// capture override so an unknown URMAN_ATMOSPHERE_PHASE fails loudly instead
    /// of silently falling back (same principle as VIS-003).</summary>
    public static bool Has(string id)
    {
        EnsureLoaded();
        return _profiles!.ContainsKey(id);
    }

    /// <summary>Every authored profile id, for error messages that list the
    /// accepted URMAN_ATMOSPHERE_PHASE values.</summary>
    public static IReadOnlyCollection<string> Ids
    {
        get
        {
            EnsureLoaded();
            return _profiles!.Keys;
        }
    }

    /// <summary>
    /// VIS-040/042/067: the zone-to-state choice is data, not a hard-coded
    /// expression. A profile is claimed by <c>"zoneId"</c> (any time of day) or by
    /// <c>"zoneId@night"</c> / <c>"zoneId@day"</c>; the suffixed claim wins, so the
    /// village can hold a green night and a frost morning without the night state
    /// ever leaking into daytime play. A zone nobody claims returns null and the
    /// caller refuses out loud, which also documents a phase-only state
    /// (<see cref="AtmosphereProfile.PhaseOnly"/>) as intentional.
    /// </summary>
    public static string? ResolveZoneProfileId(string zoneId, bool night)
    {
        EnsureLoaded();
        var ordered = _order!;
        var suffix = night ? "@night" : "@day";
        string? bare = null;
        foreach (var id in ordered)
        {
            var zones = _profiles![id].Zones;
            for (var index = 0; index < zones.Count; index++)
            {
                var claim = zones[index];
                if (string.Equals(claim, zoneId + suffix, StringComparison.Ordinal)) return id;
                if (string.Equals(claim, zoneId, StringComparison.Ordinal) && bare is null) bare = id;
            }
        }
        return bare;
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
        // Optional VIS-067 blocks. A profile authored before the colour script
        // simply omits them; the neutral snow response keeps its previous frame.
        var snow = p.TryGetProperty("snow", out var snowElement) ? snowElement : (JsonElement?)null;
        var identity = p.TryGetProperty("identity", out var identityElement)
            ? identityElement.GetString() ?? string.Empty
            : string.Empty;
        var zones = p.TryGetProperty("zones", out var zonesElement) && zonesElement.ValueKind == JsonValueKind.Array
            ? zonesElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty)
                .Where(value => value.Length > 0).ToArray()
            : Array.Empty<string>();
        // Optional VIS-043 weather transcription. Absent means the authored
        // constants above, which are the weather owner's live values, so a
        // profile written before this block keeps today's snow exactly.
        var weather = p.TryGetProperty("weather", out var weatherElement) ? weatherElement : (JsonElement?)null;
        var wind = weather is { } w && w.TryGetProperty("direction", out var direction)
            ? new Vector2(direction[0].GetSingle(), direction[1].GetSingle())
            : AuthoredWindDirection;
        if (wind.LengthSquared() < .0001f) wind = AuthoredWindDirection;
        // Optional VIS-043 plume response. Absent keeps the chimney smoke exactly
        // as it was drawn before the colour script.
        var plume = p.TryGetProperty("plume", out var plumeElement) ? plumeElement : (JsonElement?)null;
        return new(
            F(ambient, "energy"), C(ambient, "color"), F(ambient, "skyContribution"),
            C(fog, "color"), F(fog, "density"), F(fog, "height"), F(fog, "heightDensity"), F(fog, "aerialPerspective"), F(fog, "skyAffect"), F(fog, "sunScatter"),
            F(p, "exposure"), F(ssao, "intensity"), F(ssao, "radius"),
            C(sky, "top"), C(sky, "horizon"), C(sky, "groundHorizon"), C(sky, "groundBottom"), F(sky, "sunAngleMax"), F(sky, "sunCurve"),
            new Color(cover[0].GetSingle(), cover[1].GetSingle(), cover[2].GetSingle(), cover[3].GetSingle()),
            C(sun, "color"), F(sun, "energy"), F(sun, "shadowOpacity"), new Vector3(rotation[0].GetSingle(), rotation[1].GetSingle(), 0f),
            snow is { } s ? C(s, "color") : AtmosphereProfile.NeutralSnowColor,
            snow is { } sc ? F(sc, "coverage") : 1f,
            snow is { } sp ? F(sp, "sparkle") : 1f,
            snow is { } ts ? F(ts, "tintStrength") : 0f,
            identity,
            zones,
            wind.Normalized(),
            weather is { } ws ? F(ws, "speed") : AuthoredWindSpeed,
            weather is { } wb ? F(wb, "blizzardSpeed") : AuthoredBlizzardSpeed,
            weather is { } wf ? F(wf, "flakes") : AuthoredFlakeBudget,
            weather is { } wz ? F(wz, "blizzardFlakes") : AuthoredBlizzardFlakeBudget,
            weather is { } wst ? F(wst, "streak") : AuthoredStreakStretch,
            plume is { } pc ? C(pc, "color") : DefaultSmokeColor,
            plume is { } po ? F(po, "opacity") : DefaultSmokeOpacity,
            plume.HasValue);
    }

    private static (Dictionary<string, AtmosphereProfile> Profiles, List<string> Order) Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var profiles = new Dictionary<string, AtmosphereProfile>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var entity in document.RootElement.GetProperty("entities").EnumerateArray())
        {
            var parameters = entity.GetProperty("params");
            var id = parameters.GetProperty("profile").GetString()!;
            if (!profiles.TryAdd(id, FromParams(parameters)))
                throw new InvalidOperationException($"Duplicate atmosphere profile '{id}' in {Path}.");
            order.Add(id);
        }
        return (profiles, order);
    }
}
