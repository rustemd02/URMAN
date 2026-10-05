using Godot;

namespace Urman.Godot;

/// <summary>Eight shared window resources, independent of the number of houses.</summary>
public static class VillageWindowMaterials
{
    private const string ShaderSource = """
        shader_type spatial;
        render_mode diffuse_burley, specular_schlick_ggx;
        uniform sampler2D frost_map : source_color;
        uniform vec4 room_color : source_color;
        uniform float lamp_energy = 0.75;
        uniform float curtain_phase = 0.0;
        void fragment() {
            vec4 frost = texture(frost_map, UV);
            // Room light filtered through cloth and real frost alpha. The dark
            // RGB in fully transparent texels must never darken the glass.
            float folds = 0.82 + 0.09 * sin(UV.x * 38.0 + curtain_phase);
            float rim = min(min(UV.x, 1.0-UV.x), min(UV.y, 1.0-UV.y));
            float ice = clamp(frost.a * 0.75 + (1.0-smoothstep(0.0,0.025,rim))*0.18,0.0,0.9);
            ALBEDO = mix(room_color.rgb * folds * 0.67, frost.rgb, ice);
            EMISSION = room_color.rgb * folds * lamp_energy * (1.0-ice*0.7);
            ROUGHNESS = mix(0.42,0.83,ice);
            SPECULAR = 0.3;
        }
        """;
    private static Shader? _shader;
    private static readonly ShaderMaterial[] Materials = new ShaderMaterial[8];
    private static bool _night;

    public static ShaderMaterial For(string stableId)
    {
        var index = (int)(TimberHomeStyle.StableHash(stableId) % 8);
        if (Materials[index] is { } cached) return cached;
        var material = new ShaderMaterial { Shader = _shader ??= new Shader {Code=ShaderSource} };
        string[] colors = ["ffd09a", "edc79b", "ffdaa8", "e8d3af", "f4c58b", "f7d6aa", "ecc9a3", "ffd5a0"];
        material.SetShaderParameter("room_color", Color.FromHtml(colors[index]));
        material.SetShaderParameter("curtain_phase", index * 0.83f);
        material.SetShaderParameter("lamp_energy", _night ? 1.20f : 0.72f);
        material.SetShaderParameter("frost_map", ResourceLoader.Load<Texture2D>("res://assets/textures/painterly/frost_window_v1_albedo.png"));
        return Materials[index] = material;
    }

    public static void SetNight(bool night)
    {
        if (_night == night) return;
        _night = night;
        foreach (var material in Materials)
            material?.SetShaderParameter("lamp_energy", night ? 1.20f : 0.72f);
    }

    // Called only after the scene is freed. Production shares this fixed cache
    // across new games instead of repeatedly constructing window resources.
    public static void ClearCacheForHeadlessTests()
    {
        Array.Clear(Materials);_shader=null;_night=false;
    }
}
