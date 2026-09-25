using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    // One shader for every soft or grained cabin finish. The Blender boxes have
    // no UVs, so the grain is procedural noise in object space: fine pebbling
    // on plastic, a coarser hide on vinyl, pile on carpet, curls on sheepskin.
    private static Shader? _grainShader;

    private static Shader GrainShader => _grainShader ??= new Shader { Code = """
        shader_type spatial;
        render_mode diffuse_burley;
        uniform vec4 base_color : source_color;
        uniform float grain_scale = 300.0;
        uniform float albedo_variation = 0.06;
        uniform float bump = 0.3;
        uniform float roughness_value = 0.8;
        uniform float specular_value = 0.3;
        uniform float fuzz = 0.0;
        uniform bool use_vertex_color = false;
        varying vec3 local_position;
        varying vec3 local_normal;
        float hash3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
        float noise3(vec3 p) {
            vec3 i = floor(p); vec3 f = fract(p); f = f * f * (3.0 - 2.0 * f);
            return mix(mix(mix(hash3(i), hash3(i + vec3(1,0,0)), f.x), mix(hash3(i + vec3(0,1,0)), hash3(i + vec3(1,1,0)), f.x), f.y),
                       mix(mix(hash3(i + vec3(0,0,1)), hash3(i + vec3(1,0,1)), f.x), mix(hash3(i + vec3(0,1,1)), hash3(i + vec3(1,1,1)), f.x), f.y), f.z);
        }
        void vertex() { local_position = VERTEX; local_normal = NORMAL; }
        void fragment() {
            vec3 p = local_position * grain_scale;
            // Fade the grain where it would alias into shimmer at a distance.
            float fade = 1.0 - smoothstep(0.4, 1.5, length(fwidth(p)));
            float a = noise3(p) * 0.65 + noise3(p * 2.7 + 11.0) * 0.35;
            float curl = mix(0.5, a, fade);
            vec3 colour = base_color.rgb * (1.0 - albedo_variation + 2.0 * albedo_variation * curl);
            if (use_vertex_color) colour *= COLOR.rgb;
            ALBEDO = colour;
            ROUGHNESS = roughness_value;
            SPECULAR = specular_value;
            float dx = noise3(p + vec3(0.37, 0.0, 0.0)) - noise3(p - vec3(0.37, 0.0, 0.0));
            float dy = noise3(p + vec3(0.0, 0.37, 0.0)) - noise3(p - vec3(0.0, 0.37, 0.0));
            NORMAL_MAP = normalize(vec3(dx * bump * fade, dy * bump * fade, 1.0)) * 0.5 + 0.5;
            if (fuzz > 0.0) {
                AO = mix(1.0, 0.55 + 0.45 * curl, fuzz);
                AO_LIGHT_AFFECT = 0.4;
                RIM = fuzz * 0.6; RIM_TINT = 0.6;
            }
        }
        """ };

    /// <summary>Weathered and painted fence wood; the colour comes from the vertices.</summary>
    public static Material FrontageWoodMaterial() => Grain("ffffff", 70f, .1f, .5f, .86f, .22f, vertexColor: true);

    private static Material Grain(string color, float scale, float variation, float bump, float roughness,
        float specular = .3f, float fuzz = 0f, bool vertexColor = false)
    {
        var material = new ShaderMaterial { Shader = GrainShader };
        material.SetShaderParameter("base_color", Color.FromHtml(color));
        material.SetShaderParameter("grain_scale", scale);
        material.SetShaderParameter("albedo_variation", variation);
        material.SetShaderParameter("bump", bump);
        material.SetShaderParameter("roughness_value", roughness);
        material.SetShaderParameter("specular_value", specular);
        material.SetShaderParameter("fuzz", fuzz);
        material.SetShaderParameter("use_vertex_color", vertexColor);
        return material;
    }
}
