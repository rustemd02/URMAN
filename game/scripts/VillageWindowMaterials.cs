using Godot;

namespace Urman.Godot;

/// <summary>Eight shared window resources, independent of the number of houses.
///
/// VIS-041/VIS-096: one pane is one light node. The room's lamp, the curtain
/// mask, the frost and the winter sky all answer to the same surface, so a
/// window reads as glass with a room behind it by day and as the village's own
/// light source at night — never as a glowing rectangle. The authored lamp pair
/// (0.72 day / 1.20 night, K03) is unchanged; what changes is that daytime
/// emission additionally passes through <see cref="DayGain"/>, so the same lamp
/// cannot out-shine the winter daylight, and that no channel is ever allowed to
/// reach white (<see cref="EmissionCeiling"/>).
///
/// Frost is one texture reused with per-house UV variants and per-house density,
/// never eight unique painted panes (asset policy L10/L28).</summary>
public static class VillageWindowMaterials
{
    private const string ShaderSource = """
        shader_type spatial;
        render_mode diffuse_burley, specular_schlick_ggx;
        uniform sampler2D frost_map : source_color;
        uniform sampler2D frost_normal : hint_normal;
        uniform bool frost_normal_used = false;
        uniform vec4 room_color : source_color;
        uniform float lamp_energy = 0.75;
        uniform float curtain_phase = 0.0;
        uniform float day_gain = 1.0;
        uniform float frost_strength = 0.75;
        uniform vec2 frost_uv = vec2(1.0, 1.0);
        uniform vec2 frost_offset = vec2(0.0, 0.0);
        uniform float emission_ceiling = 0.95;
        void fragment() {
            // One texture, reused with a per-house scale, offset and density.
            vec2 fuv = UV * frost_uv + frost_offset;
            vec4 frost = texture(frost_map, fuv);
            // Room light filtered through cloth and real frost alpha. The dark
            // RGB in fully transparent texels must never darken the glass.
            float folds = 0.82 + 0.09 * sin(UV.x * 38.0 + curtain_phase);
            float rim = min(min(UV.x, 1.0-UV.x), min(UV.y, 1.0-UV.y));
            float ice = clamp(frost.a * frost_strength
                + (1.0 - smoothstep(0.0, 0.025, rim)) * 0.18, 0.0, 0.9);
            vec3 room = room_color.rgb * folds;
            vec3 warm = room * lamp_energy * day_gain * (1.0 - ice * 0.7);
            EMISSION = min(warm, vec3(emission_ceiling));
            // By day the same surface is glass: a dim room behind it, a cold sky
            // sheen and frost on top. Cold is material response, never emission.
            vec3 sky_ice = mix(vec3(0.62, 0.71, 0.80), frost.rgb, ice);
            ALBEDO = mix(mix(room * 0.16, sky_ice, 0.35), frost.rgb, ice);
            ROUGHNESS = mix(0.42, 0.83, ice);
            SPECULAR = mix(0.30, 0.52, ice);
            if (frost_normal_used) {
                // Frost is a relief on the pane, not a picture of relief: the
                // perturbation applies only where frost actually sits, so the clear
                // part of the glass keeps the flat specular of the authored pane.
                vec3 fn = texture(frost_normal, fuv).xyz * 2.0 - 1.0;
                NORMAL_MAP = normalize(vec3(fn.xy * 0.55, max(fn.z, 0.25))) * 0.5 + 0.5;
                NORMAL_MAP_DEPTH = ice;
            }
        }
        """;
    private static Shader? _shader;
    private static readonly ShaderMaterial[] Materials = new ShaderMaterial[8];
    private static bool _night;

    /// <summary>Authored lamp energies (K03). Read as the source values, they are
    /// the pair the A/B has to keep; the day value additionally carries
    /// <see cref="DayGain"/>.</summary>
    public const float DayLampEnergy = .72f;
    public const float NightLampEnergy = 1.20f;
    /// <summary>Daytime self-glow of a lit pane, as a fraction of its lamp energy.
    /// A warm pane in winter daylight must not compete with the sun; 0.22 keeps a
    /// hint of life in the glass while the albedo and frost carry the read.</summary>
    public const float DayGain = .22f;
    /// <summary>Per-channel emission cap. No pane is allowed to reach white
    /// (VIS-096: "no full-white emission").</summary>
    public const float EmissionCeiling = .95f;

    /// <summary>Optional relief map for the frost (asset request HOUSE-W01 in
    /// <c>docs/urman_knowledge_base/art/asset_requests/HOUSE.md</c>). The slot is
    /// wired and the toggle is off until the file exists, so today's picture is
    /// exactly the picture before this change: no shader path is declared that the
    /// repository cannot feed.</summary>
    public const string FrostNormalPath = "res://assets/textures/painterly/frost_window_v1_normal.png";

    public static ShaderMaterial For(string stableId)
    {
        var index = (int)(TimberHomeStyle.StableHash(stableId) % 8);
        if (Materials[index] is { } cached) return cached;
        var material = new ShaderMaterial { Shader = _shader ??= new Shader {Code=ShaderSource} };
        string[] colors = ["ffd09a", "edc79b", "ffdaa8", "e8d3af", "f4c58b", "f7d6aa", "ecc9a3", "ffd5a0"];
        material.SetShaderParameter("room_color", Color.FromHtml(colors[index]));
        material.SetShaderParameter("curtain_phase", index * 0.83f);
        material.SetShaderParameter("lamp_energy", _night ? NightLampEnergy : DayLampEnergy);
        material.SetShaderParameter("day_gain", _night ? 1f : DayGain);
        material.SetShaderParameter("emission_ceiling", EmissionCeiling);
        // Eight frost reads from one map: mirrored, shifted and with a bounded
        // density difference, so a street of houses never repeats one pattern.
        material.SetShaderParameter("frost_uv",
            new Vector2(index % 2 == 0 ? 1f : -1f, 1f + (index % 3) * .18f));
        material.SetShaderParameter("frost_offset", new Vector2((index * .17f) % 1f, (index * .29f) % 1f));
        material.SetShaderParameter("frost_strength", .60f + (index % 5) * .07f);
        material.SetShaderParameter("frost_map", ResourceLoader.Load<Texture2D>("res://assets/textures/painterly/frost_window_v1_albedo.png"));
        // Relief slot is wired but stays off until the map exists (asset request
        // HOUSE-W01): a declared shader path with no file would change the picture
        // silently through a default normal, and the visual reset must not depend
        // on an unfinished asset.
        if (ResourceLoader.Exists(FrostNormalPath)
            && ResourceLoader.Load<Texture2D>(FrostNormalPath) is { } relief)
        {
            material.SetShaderParameter("frost_normal", relief);
            material.SetShaderParameter("frost_normal_used", true);
        }
        return Materials[index] = material;
    }

    public static void SetNight(bool night)
    {
        if (_night == night) return;
        _night = night;
        foreach (var material in Materials)
        {
            material?.SetShaderParameter("lamp_energy", night ? NightLampEnergy : DayLampEnergy);
            material?.SetShaderParameter("day_gain", night ? 1f : DayGain);
        }
    }

    // Called only after the scene is freed. Production shares this fixed cache
    // across new games instead of repeatedly constructing window resources.
    public static void ClearCacheForHeadlessTests()
    {
        Array.Clear(Materials);_shader=null;_night=false;
    }
}
