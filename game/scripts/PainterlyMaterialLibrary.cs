using Godot;

namespace Urman.Godot;

public static class PainterlyMaterialLibrary
{
    private const string ShaderSource = """
        shader_type spatial;
        render_mode diffuse_burley, specular_schlick_ggx;

        uniform vec4 base_color : source_color;
        uniform vec4 shadow_color : source_color;
        uniform float brush_scale = 0.7;
        uniform float variation = 0.16;
        uniform float texture_strength = 0.82;
        uniform float roughness_value = 0.9;
        uniform float specular_value = 0.2;
        uniform float wet_grade = 0.0;
        uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool has_albedo_texture = false;
        // Safe mode skips the three triplanar texture reads below. The normal
        // medium profile keeps the full painterly material unchanged.
        uniform bool low_quality = false;
        uniform vec2 texture_scale = vec2(1.0);

        varying vec3 world_position;
        varying vec3 world_normal;

        vec3 triplanar_albedo(vec3 position, vec3 normal) {
            vec3 blend = pow(abs(normal), vec3(4.0));
            blend /= max(blend.x + blend.y + blend.z, 0.0001);
            vec3 x_projection = texture(albedo_texture, position.yz * texture_scale).rgb;
            vec3 y_projection = texture(albedo_texture, position.xz * texture_scale).rgb;
            vec3 z_projection = texture(albedo_texture, position.xy * texture_scale).rgb;
            return x_projection * blend.x + y_projection * blend.y + z_projection * blend.z;
        }

        void vertex() {
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
        }

        void fragment() {
            float wet_factor = clamp(wet_grade, 0.0, 1.0);
            if (low_quality) {
                ALBEDO = mix(shadow_color.rgb, base_color.rgb, 0.82)
                    * mix(vec3(1.0), vec3(0.88, 0.92, 0.94), wet_factor);
                ROUGHNESS = clamp(roughness_value - wet_factor * 0.14, 0.05, 1.0);
                SPECULAR = clamp(specular_value + wet_factor * 0.16, 0.0, 1.0);
                METALLIC = 0.0;
            } else {
            float stroke = sin((world_position.x + world_position.z * 0.63 + world_position.y * 0.28) * brush_scale * 5.0);
            // A second, broader pigment band keeps the painterly source legible
            // on large first-person surfaces. It is deliberately low-frequency:
            // the scene should read as painted planes, not procedural static.
            float broad_stroke = sin(
                (world_position.x * 0.31 + world_position.z * 0.93 + world_position.y * 0.17) * brush_scale * 1.8
                + sin((world_position.x * 0.13 + world_position.z * 0.21) * brush_scale * 0.7));
            float wash = clamp(
                0.64 + stroke * variation * 0.30 + broad_stroke * variation * 0.54,
                0.18,
                0.98);
            float upward = clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)) * 0.14 + 0.86, 0.72, 1.0);
            vec3 texture_color = has_albedo_texture
                ? triplanar_albedo(world_position, normalize(world_normal))
                : vec3(1.0);
            // Keep the hand-painted source legible at first-person distance. The
            // old grade compressed the albedo into a flat mid-tone on broad road,
            // wall and foliage surfaces; this wider range preserves brush marks
            // without introducing a photographic normal-map look.
            vec3 graded_texture = pow(clamp(texture_color, 0.0, 1.0), vec3(1.08));
            graded_texture = clamp((graded_texture - vec3(0.5)) * 1.35 + vec3(0.5), 0.0, 1.0);
            vec3 tinted_texture = base_color.rgb * (vec3(0.42) + graded_texture * 1.08);
            vec3 painted_color = has_albedo_texture
                ? mix(base_color.rgb, tinted_texture, texture_strength)
                : base_color.rgb;
            vec3 painted_shadow = has_albedo_texture ? painted_color * 0.68 : shadow_color.rgb;
            float edge_wash = 0.92 + 0.08 * clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
            float macro_pigment = clamp(
                0.94 + stroke * variation * 0.18 + broad_stroke * variation * 0.40,
                0.80,
                1.08);
            wet_factor = clamp(wet_grade * (0.82 + 0.18 * upward), 0.0, 1.0);
            ALBEDO = mix(painted_shadow, painted_color, wash)
                * macro_pigment
                * upward
                * edge_wash
                * mix(vec3(1.0), vec3(0.88, 0.92, 0.94), wet_factor);
            ROUGHNESS = clamp(roughness_value - wet_factor * 0.14, 0.05, 1.0);
            SPECULAR = clamp(specular_value + wet_factor * 0.16, 0.0, 1.0);
            METALLIC = 0.0;
            }
        }
        """;

    private static readonly Shader PainterlyShader = new() { Code = ShaderSource };
    private static readonly Dictionary<string, (string Path, Vector2 Scale)> SurfaceTextures = new(StringComparer.Ordinal)
    {
        ["wood"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(3.2f, 3.2f)),
        // Imported Blender modules have different real-world repetition
        // scales. Keep one shared albedo source, but give the facade, fence,
        // furniture and bark semantic owners so a board tile is not repeated
        // at the same density on every surface. This is a material-library
        // calibration only; the shader and source textures remain unchanged.
        ["wood_facade"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(1.8f, 1.8f)),
        ["wood_fence"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(2.2f, 2.2f)),
        ["wood_furniture"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(2.7f, 2.7f)),
        ["wood_prop"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(2.4f, 2.4f)),
        ["wood_bark"] = ("res://assets/textures/painterly/weathered_wood_boards_albedo.png", new Vector2(1.35f, 1.35f)),
        ["earth"] = ("res://assets/textures/painterly/damp_earth_albedo.png", new Vector2(2.0f, 5.0f)),
        ["plaster"] = ("res://assets/textures/painterly/aged_plaster_albedo.png", new Vector2(2.6f, 1.8f)),
        ["foliage"] = ("res://assets/textures/painterly/pine_foliage_albedo.png", new Vector2(2.2f, 2.2f)),
        // These surfaces have no v1 predecessor. They are scoped to the
        // authored stone/fabric presentation anchors in the benchmark scenes;
        // existing wood/plaster/earth/foliage mappings remain v1.
        ["stone"] = ("res://assets/textures/painterly/mossy_stone_v2_albedo.png", new Vector2(2.4f, 2.4f)),
        ["fabric"] = ("res://assets/textures/painterly/old_fabric_v2_albedo.png", new Vector2(3.0f, 3.0f)),
        // GeneratedCharacterKitDressing keeps the semantic name `cloth` in
        // node metadata. Make that owner explicit instead of silently
        // falling back to a texture-less shader material.
        ["cloth"] = ("res://assets/textures/painterly/old_fabric_v2_albedo.png", new Vector2(3.0f, 3.0f))
    };

    private static readonly Dictionary<string, ShaderMaterial> Materials = new(StringComparer.OrdinalIgnoreCase);

    private static bool _lowQualityMaterials;

    public static bool LowQualityMaterials => _lowQualityMaterials;

    /// <summary>
    /// Collision-only headless tests can skip image-backed surface textures;
    /// visual scene and capture paths leave this disabled.
    /// </summary>
    public static bool SuppressTextureLoadsForHeadlessTests { get; set; }

    /// <summary>
    /// Applies the explicit graphics profile to cached presentation materials.
    /// Low mode avoids triplanar texture reads while preserving scene geometry,
    /// colors, interactions and gameplay owners.
    /// </summary>
    public static void SetGraphicsPreset(string graphicsPreset)
    {
        _lowQualityMaterials = string.Equals(graphicsPreset, "low", StringComparison.OrdinalIgnoreCase);
        foreach (var material in Materials.Values)
        {
            material.SetShaderParameter("low_quality", _lowQualityMaterials);
        }
    }

    /// <summary>
    /// Releases the presentation material cache after headless scene tests have
    /// freed their nodes. Runtime gameplay keeps the cache alive for reuse.
    /// </summary>
    public static void ClearCacheForHeadlessTests()
    {
        Materials.Clear();
        // ShaderMaterial/ImageTexture wrappers are managed Godot objects. A
        // headless smoke process can otherwise reach native shutdown before
        // the wrappers' finalizers release their renderer RIDs. This method is
        // test-only; production gameplay never forces a collection.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static Material ForColor(string htmlColor, string surface = "")
    {
        var cacheKey = $"{surface}:{htmlColor}";
        if (Materials.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        var color = Color.FromHtml(htmlColor);
        var shadow = new Color(color.R * 0.54f, color.G * 0.56f, color.B * 0.58f, color.A);
        var material = new ShaderMaterial { Shader = PainterlyShader };
        material.SetShaderParameter("base_color", color);
        material.SetShaderParameter("shadow_color", shadow);
        material.SetShaderParameter("brush_scale", 0.46f + (Materials.Count % 4) * 0.08f);
        material.SetShaderParameter("variation", surface.Length == 0 ? 0.18f : 0.28f);
        material.SetShaderParameter("texture_strength", surface switch
        {
            "foliage" => 0.92f,
            "earth" => 0.92f,
            "wood" => 0.86f,
            "wood_facade" => 0.88f,
            "wood_fence" => 0.86f,
            "wood_furniture" => 0.82f,
            "wood_prop" => 0.82f,
            "wood_bark" => 0.82f,
            "plaster" => 0.84f,
            "stone" => 0.88f,
            "fabric" => 0.8f,
            "cloth" => 0.8f,
            "water" => 0.0f,
            _ => 0.78f
        });
        // Keep the wet-weather response semantic and bounded: authored relief
        // owns puddle/rut silhouettes, while these values only separate surface
        // response without adding textures or a second material owner.
        var surfaceGrade = surface switch
        {
            "earth" => (Roughness: 0.66f, Specular: 0.38f, WetGrade: 0.78f),
            "wood" => (Roughness: 0.76f, Specular: 0.30f, WetGrade: 0.42f),
            "wood_facade" => (Roughness: 0.73f, Specular: 0.32f, WetGrade: 0.48f),
            "wood_fence" => (Roughness: 0.72f, Specular: 0.32f, WetGrade: 0.50f),
            "wood_furniture" => (Roughness: 0.82f, Specular: 0.25f, WetGrade: 0.22f),
            "wood_prop" => (Roughness: 0.76f, Specular: 0.29f, WetGrade: 0.34f),
            "wood_bark" => (Roughness: 0.74f, Specular: 0.30f, WetGrade: 0.38f),
            "stone" => (Roughness: 0.80f, Specular: 0.28f, WetGrade: 0.22f),
            "foliage" => (Roughness: 0.84f, Specular: 0.24f, WetGrade: 0.18f),
            "plaster" => (Roughness: 0.88f, Specular: 0.20f, WetGrade: 0.06f),
            "fabric" or "cloth" => (Roughness: 0.92f, Specular: 0.16f, WetGrade: 0.02f),
            "water" => (Roughness: 0.50f, Specular: 0.44f, WetGrade: 0.86f),
            _ => (Roughness: 0.90f, Specular: 0.20f, WetGrade: 0.0f)
        };
        material.SetShaderParameter("roughness_value", surfaceGrade.Roughness);
        material.SetShaderParameter("specular_value", surfaceGrade.Specular);
        material.SetShaderParameter("wet_grade", surfaceGrade.WetGrade);
        material.SetShaderParameter("low_quality", _lowQualityMaterials);
        if (!SuppressTextureLoadsForHeadlessTests
            && SurfaceTextures.TryGetValue(surface, out var textureDescriptor))
        {
            var texture = ResourceLoader.Load<Texture2D>(textureDescriptor.Path)
                ?? throw new InvalidOperationException($"Painterly texture is missing: {textureDescriptor.Path}.");
            material.SetShaderParameter("albedo_texture", texture);
            material.SetShaderParameter("has_albedo_texture", true);
            material.SetShaderParameter("texture_scale", textureDescriptor.Scale);
        }

        Materials.Add(cacheKey, material);
        return material;
    }
}
