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
        uniform float texture_strength = 0.95;
        uniform float roughness_value = 0.9;
        uniform float specular_value = 0.2;
        uniform float wet_grade = 0.0;
        uniform float leaf_transmission = 0.0;
        uniform bool vertex_pigment = false;
        uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool has_albedo_texture = false;
        // Safe mode skips the three triplanar texture reads below. The normal
        // medium profile keeps the full painterly material unchanged.
        uniform bool low_quality = false;
        uniform vec2 texture_scale = vec2(1.0);
        // Prod-ready phase 2: grounding darken near the ground line (0 = off),
        // per-world-cell tint jitter that de-clones repeated kits, and a
        // large-value macro breakup that keeps long walls/fields from
        // reading as one flat pour.
        uniform float ground_darken = 0.0;
        uniform float cell_jitter = 0.0;
        // Phase 7 life: gentle vertex sway for foliage/grass materials.
        // Global switch honors the reduced-motion accessibility contract.
        uniform float wind_sway = 0.0;
        uniform bool wind_enabled = false;
        // Winter: snow resting on up-facing surfaces (roofs, fence rails,
        // woodpiles, well, wall caps) and full-snow ground families.
        uniform float snow_coverage = 0.0;
        uniform vec4 snow_color : source_color = vec4(0.93, 0.95, 0.97, 1.0);
        // Packed-snow trail mask (session presentation): the player's steps
        // press the snow down — darker, flatter, matte.
        // Live frost sparkle: view-dependent micro-glints on snow surfaces.
        uniform float snow_sparkle = 0.0;
        uniform sampler2D trample_map : hint_default_black, filter_linear, repeat_disable;
        uniform bool has_trample_map = false;
        uniform vec2 trample_origin = vec2(0.0);
        uniform float trample_extent = 0.0;

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

        float painter_value_noise(vec2 p) {
            vec2 i = floor(p);
            vec2 f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);
            float a = fract(sin(dot(i, vec2(127.1, 311.7))) * 43758.5453);
            float b = fract(sin(dot(i + vec2(1.0, 0.0), vec2(127.1, 311.7))) * 43758.5453);
            float c = fract(sin(dot(i + vec2(0.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
            float d = fract(sin(dot(i + vec2(1.0, 1.0), vec2(127.1, 311.7))) * 43758.5453);
            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        vec3 cell_tint(vec3 p, float strength) {
            vec2 cell = floor(p.xz / 6.0);
            float h = fract(sin(dot(cell, vec2(12.9898, 78.233))) * 43758.5453);
            float h2 = fract(h * 91.17);
            vec3 tint = vec3(
                mix(0.97, 1.07, h),
                mix(0.96, 1.02, h2),
                mix(0.95, 1.03, h));
            return mix(vec3(1.0), tint, strength);
        }

        void vertex() {
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
            if (wind_enabled && wind_sway > 0.0) {
                float phase = TIME * 1.6 + world_position.x * 0.55 + world_position.z * 0.4;
                float gust = sin(phase) * 0.65 + sin(phase * 2.3 + 1.7) * 0.35;
                float reach = max(VERTEX.y, 0.0);
                VERTEX.x += gust * wind_sway * reach;
                VERTEX.z += gust * wind_sway * 0.6 * reach;
            }
        }

        void fragment() {
            float wet_factor = clamp(wet_grade, 0.0, 1.0);
            if (low_quality) {
                ALBEDO = mix(shadow_color.rgb, base_color.rgb, 0.82)
                    * mix(vec3(1.0), vec3(0.90, 0.95, 0.98), wet_factor);
                if (snow_coverage > 0.0) {
                    float snow_up = clamp(normalize(world_normal).y, 0.0, 1.0);
                    float snow_cover = smoothstep(0.35, 0.75, snow_up) * snow_coverage;
                    ALBEDO = mix(ALBEDO, snow_color.rgb, snow_cover);
                }
                ROUGHNESS = clamp(roughness_value - wet_factor * 0.26, 0.05, 1.0);
                SPECULAR = clamp(specular_value + wet_factor * 0.22, 0.0, 1.0);
                METALLIC = 0.0;
            } else {
            // Quieter stroke wash: with the promoted candidate textures the
            // texture now carries the value rhythm; strokes only keep the
            // painted plane alive.
            float stroke = sin(
                (world_position.x * 0.37 + world_position.z * 0.43 + world_position.y * 0.19)
                * brush_scale * 2.1);
            float broad_stroke = sin(
                (world_position.x * 0.11 + world_position.z * 0.07 + world_position.y * 0.05)
                * brush_scale * 0.85
                + cos((world_position.x * 0.17 - world_position.z * 0.09) * brush_scale * 0.62));
            float wash = clamp(
                0.80 + stroke * variation * 0.10 + broad_stroke * variation * 0.18,
                0.55,
                1.0);
            float upward = clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)) * 0.08 + 0.92, 0.84, 1.0);
            vec3 texture_color = has_albedo_texture
                ? triplanar_albedo(world_position, normalize(world_normal))
                : vec3(1.0);
            // Keep the source hue and broad brush value while avoiding the
            // crushed-to-clay contrast of the previous texture grade.
            vec3 graded_texture = pow(clamp(texture_color, 0.0, 1.0), vec3(1.02));
            graded_texture = clamp((graded_texture - vec3(0.5)) * 1.18 + vec3(0.5), 0.0, 1.0);
            // Texture-dominant balance: the albedo painting owns the value
            // range; base_color acts as a light tint over it.
            vec3 tinted_texture = base_color.rgb * (vec3(0.30) + graded_texture * 1.75);
            vec3 painted_color = has_albedo_texture
                ? mix(base_color.rgb, tinted_texture, texture_strength)
                : base_color.rgb;
            vec3 painted_shadow = has_albedo_texture ? painted_color * 0.86 : shadow_color.rgb;
            float edge_wash = 0.95 + 0.05 * clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
            float macro_pigment = clamp(
                0.98 + stroke * variation * 0.08 + broad_stroke * variation * 0.16,
                0.88,
                1.06);
            // ~3 m value breakup: broad stains across walls and fields.
            float macro_stain = mix(0.93, 1.05,
                painter_value_noise(world_position.xz * 0.33 + world_position.y * 0.11));
            // Grounding: darken the first metres above the ground line so
            // facades/fences sit into the dirt instead of floating on it.
            float ground_line = 1.0 - ground_darken
                * (1.0 - clamp(world_position.y / 3.0, 0.0, 1.0));
            wet_factor = clamp(wet_grade * (0.74 + 0.26 * upward), 0.0, 1.0);
            ALBEDO = mix(painted_shadow, painted_color, wash)
                * macro_pigment
                * macro_stain
                * cell_tint(world_position, cell_jitter)
                * ground_line
                * upward
                * edge_wash
                * mix(vec3(1.0), vec3(0.90, 0.95, 0.98), wet_factor);
            ROUGHNESS = clamp(roughness_value - wet_factor * 0.26, 0.05, 1.0);
            SPECULAR = clamp(specular_value + wet_factor * 0.22, 0.0, 1.0);
            METALLIC = 0.0;
            }
            // Thin leaves receive light on their reverse face; this remains
            // light-dependent, not emission that would glow in the forest.
            if (vertex_pigment) {
                ALBEDO *= COLOR.rgb;
            }
            // Winter snow blanket: up-facing surfaces take a matte, slightly
            // mottled snow layer on top of the existing paint, so roofs,
            // rails, woodpiles and wall caps are snowed without new geometry.
            if (snow_coverage > 0.0) {
                float snow_up = clamp(normalize(world_normal).y, 0.0, 1.0);
                float snow_cover = smoothstep(0.30, 0.72, snow_up) * snow_coverage;
                snow_cover *= 0.72 + 0.48 * painter_value_noise(
                    world_position.xz * 0.5 + world_position.y * 0.21);
                snow_cover = clamp(snow_cover, 0.0, 1.0);
                vec3 snow_albedo = snow_color.rgb
                    * (0.95 + 0.05 * painter_value_noise(world_position.xz * 1.9));
                ALBEDO = mix(ALBEDO, snow_albedo, snow_cover);
                ROUGHNESS = mix(ROUGHNESS, 0.92, snow_cover);
                SPECULAR = mix(SPECULAR, 0.05, snow_cover);
            }
            // Trail compression: only near-horizontal surfaces inside the
            // rolling window; packed snow is darker, rougher and matte.
            if (has_trample_map && trample_extent > 0.0) {
                float trail_ground = smoothstep(0.55, 0.85, normalize(world_normal).y);
                if (trail_ground > 0.0) {
                    vec2 trail_uv = (world_position.xz - trample_origin) / trample_extent;
                    if (trail_uv.x >= 0.0 && trail_uv.x <= 1.0
                        && trail_uv.y >= 0.0 && trail_uv.y <= 1.0) {
                        float packed = clamp(texture(trample_map, trail_uv).a, 0.0, 1.0)
                            * trail_ground;
                        ALBEDO = mix(ALBEDO, ALBEDO * vec3(0.84, 0.87, 0.90), packed);
                        ROUGHNESS = mix(ROUGHNESS, 0.82, packed * 0.7);
                        SPECULAR = mix(SPECULAR, 0.03, packed);
                        // Light ridge on the rising edge of the pressed trail
                        // reads as displaced snow pushed aside by the step.
                        vec2 trail_texel = 1.0 / vec2(textureSize(trample_map, 0));
                        float trail_dx = texture(trample_map, trail_uv + vec2(trail_texel.x * 2.0, 0.0)).a;
                        float trail_dy = texture(trample_map, trail_uv + vec2(0.0, trail_texel.y * 2.0)).a;
                        float trail_ridge = clamp((trail_dx - packed) + (trail_dy - packed), 0.0, 1.0);
                        ALBEDO += vec3(0.90, 0.94, 1.0) * trail_ridge * 0.12;
                    }
                }
            }
            // Frost sparkle: sparse high-frequency glints that only appear on
            // snow and only at grazing view angles, so walking past the field
            // makes the surface shimmer like real frost without confetti.
            if (snow_sparkle > 0.0) {
                float flake_a = painter_value_noise(world_position.xz * 34.0 + world_position.y * 7.0);
                float flake_b = painter_value_noise(world_position.xz * 61.0 + 19.3);
                float flakes = smoothstep(0.74, 0.98, flake_a * 0.55 + flake_b * 0.45);
                float grazing = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 1.6);
                float glint = flakes * grazing * snow_sparkle;
                ALBEDO += vec3(0.85, 0.90, 1.00) * glint * 0.34;
                SPECULAR = clamp(SPECULAR + glint * 0.25, 0.0, 1.0);
            }
            BACKLIGHT = ALBEDO * leaf_transmission;
        }
        """;

    private static readonly Shader PainterlyShader = new() { Code = ShaderSource };
    private static readonly Dictionary<string, (string Path, Vector2 Scale)> SurfaceTextures = new(StringComparer.Ordinal)
    {
        // Phase 1A promotion (prod-ready plan): per-family candidate chosen
        // from the v2-v6 comparison strips; see evidence/act1_prod_ready/.
        // Facades/fences carry the calm plank read (v4); furniture/props the
        // cleaner planks (v3); road keeps painterly ruts (v3), open terrain
        // the softer mottle (v5).
        ["wood"] = ("res://assets/textures/painterly/weathered_wood_boards_v4_albedo.png", new Vector2(0.65f, 0.65f)),
        // Imported Blender modules have different real-world repetition
        // scales. Keep one shared albedo source, but give the facade, fence,
        // furniture and bark semantic owners so a board tile is not repeated
        // at the same density on every surface. This is a material-library
        // calibration; all mappings reuse the same source texture.
        ["wood_facade"] = ("res://assets/textures/painterly/weathered_wood_boards_v4_albedo.png", new Vector2(0.65f, 0.65f)),
        ["wood_fence"] = ("res://assets/textures/painterly/weathered_wood_boards_v2_albedo.png", new Vector2(0.8f, 0.8f)),
        ["wood_furniture"] = ("res://assets/textures/painterly/weathered_wood_boards_v3_albedo.png", new Vector2(0.95f, 0.95f)),
        ["wood_prop"] = ("res://assets/textures/painterly/weathered_wood_boards_v3_albedo.png", new Vector2(0.9f, 0.9f)),
        ["wood_bark"] = ("res://assets/textures/painterly/weathered_wood_boards_v4_albedo.png", new Vector2(1.05f, 1.05f)),
        ["earth"] = ("res://assets/textures/painterly/damp_earth_v3_albedo.png", new Vector2(0.55f, 1.1f)),
        ["terrain"] = ("res://assets/textures/painterly/damp_earth_v5_albedo.png", new Vector2(1.3f, 2.6f)),
        ["wet_road"] = ("res://assets/textures/painterly/damp_earth_v3_albedo.png", new Vector2(0.55f, 1.1f)),
        // Wet shoulders and ditch planes reuse the selected damp-earth family;
        // their semantic grade supplies the restrained reflective difference.
        ["wet_ground"] = ("res://assets/textures/painterly/damp_earth_v5_albedo.png", new Vector2(1.0f, 2.1f)),
        ["plaster"] = ("res://assets/textures/painterly/aged_plaster_v3_albedo.png", new Vector2(1.5f, 1.15f)),
        ["foliage"] = ("res://assets/textures/painterly/pine_foliage_v2_albedo.png", new Vector2(1.4f, 1.4f)),
        // These surfaces have no v1 predecessor. They are scoped to the
        // authored stone/fabric presentation anchors in the benchmark scenes;
        // existing wood/plaster/earth/foliage mappings remain v1.
        ["stone"] = ("res://assets/textures/painterly/mossy_stone_v3_albedo.png", new Vector2(1.5f, 1.5f)),
        ["fabric"] = ("res://assets/textures/painterly/old_fabric_v3_albedo.png", new Vector2(2.0f, 2.0f)),
        // GeneratedCharacterKitDressing keeps the semantic name `cloth` in
        // node metadata. Make that owner explicit instead of silently
        // falling back to a texture-less shader material.
        ["cloth"] = ("res://assets/textures/painterly/old_fabric_v3_albedo.png", new Vector2(2.0f, 2.0f))
    };

    // Winter albedos are produced by a separate ImageGen task (brief:
    // docs/production/URMAN_WINTER_TEXTURE_BRIEF_RU.md). They are wired
    // conditionally: until the PNG lands, the surface renders with its
    // painterly base; the moment the file exists it is picked up.
    private static readonly Dictionary<string, (string Path, Vector2 Scale)> WinterTextures = new(StringComparer.Ordinal)
    {
        ["snow_ground"] = ("res://assets/textures/painterly/snow_fresh_v1_albedo.png", new Vector2(1.2f, 1.2f)),
        ["snow_grass"] = ("res://assets/textures/painterly/snow_grass_peek_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["snow_road"] = ("res://assets/textures/painterly/snow_road_v1_albedo.png", new Vector2(1.1f, 2.2f)),
        ["snow_trampled"] = ("res://assets/textures/painterly/snow_trampled_v1_albedo.png", new Vector2(1.6f, 1.6f)),
        ["snow_roof"] = ("res://assets/textures/painterly/snow_roof_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["ice"] = ("res://assets/textures/painterly/ice_patch_v1_albedo.png", new Vector2(1.4f, 1.4f)),
        ["bark_birch_winter"] = ("res://assets/textures/painterly/birch_bark_winter_v1_albedo.png", new Vector2(0.55f, 1.1f)),
        ["rowan_berries"] = ("res://assets/textures/painterly/rowan_berries_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["wattle"] = ("res://assets/textures/painterly/wattle_weave_v1_albedo.png", new Vector2(0.9f, 0.9f)),
        ["frost_window"] = ("res://assets/textures/painterly/frost_window_v1_albedo.png", new Vector2(1.0f, 1.0f))
    };

    private static readonly Dictionary<string, ShaderMaterial> Materials = new(StringComparer.OrdinalIgnoreCase);

    private static bool _lowQualityMaterials;
    private static bool _windMotion = true;

    public static bool LowQualityMaterials => _lowQualityMaterials;

    /// <summary>
    /// Phase 7 life: global vertex-sway switch. Reduced motion must disable
    /// it; graphics presets never touch this owner.
    /// </summary>
    /// <summary>
    /// Pushes the session snow-trample mask onto the ground materials that
    /// sample it. Presentation only; pass a null texture to disable.
    /// </summary>
    public static void SetSnowTrample(Texture2D? mask, Vector2 origin, float extent)
    {
        foreach (var material in Materials.Values)
        {
            if (!material.GetShaderParameter("has_trample_map").AsBool()
                && mask is null)
            {
                continue;
            }

            material.SetShaderParameter("has_trample_map", mask is not null);
            if (mask is not null)
            {
                material.SetShaderParameter("trample_map", mask);
                material.SetShaderParameter("trample_origin", origin);
                material.SetShaderParameter("trample_extent", extent);
            }
        }
    }

    public static void SetWindMotion(bool enabled)
    {
        _windMotion = enabled;
        foreach (var material in Materials.Values)
        {
            material.SetShaderParameter("wind_enabled", enabled);
        }
    }

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
        material.SetShaderParameter("vertex_pigment", surface is "terrain" or "wet_road");
        material.SetShaderParameter("shadow_color", shadow);
        // Keep the brush rhythm stable for a semantic surface. The old cache
        // count made the same material change appearance with call order and
        // amplified broad world-space banding on long walls and roads.
        material.SetShaderParameter("brush_scale", surface switch
        {
            "earth" => 0.34f,
            "wet_ground" => 0.30f,
            "wood" or "wood_facade" or "wood_fence" => 0.32f,
            "wood_furniture" or "wood_prop" => 0.28f,
            "wood_bark" => 0.25f,
            "plaster" => 0.26f,
            "foliage" => 0.29f,
            "grass" => 0.38f,
            "roof" or "roof_metal" => 0.24f,
            "bark_birch" or "bark_pine" => 0.25f,
            "leaf_birch" => 0.30f,
            "log_wall" or "wallpaper" or "wall_institution" => 0.22f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.18f,
            "stone" => 0.27f,
            "fabric" or "cloth" => 0.24f,
            "water" => 0.18f,
            _ => 0.30f
        });
        material.SetShaderParameter("variation", surface switch
        {
            "earth" => 0.14f,
            "wet_ground" => 0.11f,
            "wood" or "wood_facade" or "wood_fence" => 0.14f,
            "wood_furniture" or "wood_prop" => 0.11f,
            "wood_bark" => 0.13f,
            "plaster" => 0.10f,
            "foliage" => 0.15f,
            "grass" or "leaf_birch" => 0.14f,
            "roof" or "roof_metal" or "bark_birch" or "bark_pine" => 0.11f,
            "log_wall" or "wallpaper" or "wall_institution" => 0.08f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.06f,
            "stone" => 0.12f,
            "fabric" or "cloth" => 0.08f,
            "water" => 0.06f,
            _ => 0.10f
        });
        material.SetShaderParameter("texture_strength", surface switch
        {
            "foliage" => 0.95f,
            "grass" or "leaf_birch" => 0.95f,
            "roof" or "roof_metal" => 0.94f,
            "bark_birch" or "bark_pine" => 0.94f,
            "log_wall" or "wallpaper" or "wall_institution" => 0.92f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.88f,
            "earth" => 0.95f,
            "terrain" => 0.95f,
            "wet_ground" => 0.92f,
            "wet_road" => 0.95f,
            "wood" => 0.95f,
            "wood_facade" => 0.95f,
            "wood_fence" => 0.95f,
            "wood_furniture" => 0.92f,
            "wood_prop" => 0.92f,
            "wood_bark" => 0.95f,
            "plaster" => 0.95f,
            "stone" => 0.95f,
            "fabric" => 0.88f,
            "cloth" => 0.88f,
            "water" => 0.0f,
            _ => 0.90f
        });
        // Phase 2: grounding darken near the dirt line + world-cell tint
        // jitter that de-clones repeated houses/fences/trees.
        material.SetShaderParameter("ground_darken", surface switch
        {
            "bark_birch" or "bark_pine" => 0.25f,
            "roof" or "roof_metal" => 0.0f,
            "wood" or "wood_facade" or "wood_fence" => 0.16f,
            "plaster" => 0.16f,
            "wood_prop" => 0.10f,
            "wood_furniture" => 0.0f,
            "wood_bark" => 0.22f,
            "stone" => 0.14f,
            _ => 0.0f
        });
        material.SetShaderParameter("snow_sparkle", surface switch
        {
            "snow_ground" => 0.55f,
            "snow_roof" => 0.65f,
            "snow_road" => 0.30f,
            "snow_trampled" => 0.18f,
            "snow_grass" => 0.35f,
            "ice" => 0.22f,
            "roof" or "roof_metal" => 0.45f,
            "wood_fence" or "wood_prop" or "stone" => 0.30f,
            "bark_birch" or "bark_birch_winter" or "bark_pine" => 0.22f,
            "foliage" or "leaf_birch" or "rowan_berries" => 0.20f,
            "grass" or "grass_tuft" => 0.35f,
            _ => 0.0f
        });
        material.SetShaderParameter("snow_coverage", surface switch
        {
            // Full-snow families own their own albedo, no blanket needed.
            "snow_ground" or "snow_road" or "snow_trampled" or "snow_grass" or "snow_roof" or "ice" => 0.0f,
            "roof" or "roof_metal" => 0.88f,
            "wood_fence" => 0.62f,
            "wood_prop" or "wood_carved" => 0.55f,
            "wood" or "wood_facade" => 0.45f,
            "stone" => 0.50f,
            "plaster" or "wall_institution" => 0.42f,
            "bark_birch" or "bark_birch_winter" or "bark_pine" => 0.30f,
            "foliage" or "leaf_birch" or "rowan_berries" => 0.34f,
            "grass" or "grass_tuft" => 0.72f,
            "fabric" or "cloth" or "fabric_pattern" => 0.30f,
            _ => 0.0f
        });
        material.SetShaderParameter("cell_jitter", surface switch
        {
            "grass" => 0.20f,
            "grass_tuft" => 0.0f,
            "roof" or "roof_metal" => 0.30f,
            "bark_birch" or "bark_pine" => 0.20f,
            "leaf_birch" => 0.15f,
            "wood" or "wood_facade" => 0.35f,
            "wood_fence" => 0.28f,
            "plaster" => 0.30f,
            "wood_prop" => 0.15f,
            "wood_bark" => 0.20f,
            "foliage" => 0.15f,
            "terrain" => 0.20f,
            "earth" => 0.10f,
            "stone" => 0.20f,
            _ => 0.0f
        });
        // Keep the wet-weather response semantic and bounded: authored relief
        // owns puddle/rut silhouettes, while these values only separate surface
        // response without adding textures or a second material owner.
        var surfaceGrade = surface switch
        {
            "snow_ground" => (Roughness: 0.93f, Specular: 0.06f, WetGrade: 0.0f),
            "snow_road" => (Roughness: 0.86f, Specular: 0.12f, WetGrade: 0.35f),
            "snow_trampled" => (Roughness: 0.88f, Specular: 0.10f, WetGrade: 0.22f),
            "snow_grass" => (Roughness: 0.93f, Specular: 0.06f, WetGrade: 0.0f),
            "snow_roof" => (Roughness: 0.94f, Specular: 0.05f, WetGrade: 0.10f),
            "ice" => (Roughness: 0.42f, Specular: 0.38f, WetGrade: 0.90f),
            "grass" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.45f),
            "grass_tuft" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.45f),
            "roof" => (Roughness: 0.88f, Specular: 0.10f, WetGrade: 0.50f),
            "roof_metal" => (Roughness: 0.72f, Specular: 0.20f, WetGrade: 0.65f),
            "bark_birch" => (Roughness: 0.92f, Specular: 0.08f, WetGrade: 0.20f),
            "bark_pine" => (Roughness: 0.92f, Specular: 0.08f, WetGrade: 0.22f),
            "leaf_birch" => (Roughness: 0.95f, Specular: 0.06f, WetGrade: 0.12f),
            "log_wall" or "wallpaper" or "wall_institution" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.02f),
            "carpet" => (Roughness: 0.98f, Specular: 0.04f, WetGrade: 0.0f),
            "fabric_pattern" => (Roughness: 0.96f, Specular: 0.05f, WetGrade: 0.02f),
            "ornament_trim" or "wood_carved" => (Roughness: 0.88f, Specular: 0.10f, WetGrade: 0.15f),
            "earth" => (Roughness: 0.78f, Specular: 0.16f, WetGrade: 0.70f),
            "wet_ground" => (Roughness: 0.56f, Specular: 0.32f, WetGrade: 0.94f),
            // Wet compacted dirt is not a continuous water film. Keep broad
            // road facets rough; the separate puddle surfaces own clear glints.
            "wet_road" => (Roughness: 0.82f, Specular: 0.12f, WetGrade: 0.72f),
            "wood" => (Roughness: 0.84f, Specular: 0.15f, WetGrade: 0.36f),
            "wood_facade" => (Roughness: 0.82f, Specular: 0.17f, WetGrade: 0.42f),
            "wood_fence" => (Roughness: 0.84f, Specular: 0.14f, WetGrade: 0.44f),
            "wood_furniture" => (Roughness: 0.89f, Specular: 0.10f, WetGrade: 0.16f),
            "wood_prop" => (Roughness: 0.86f, Specular: 0.12f, WetGrade: 0.28f),
            "wood_bark" => (Roughness: 0.90f, Specular: 0.08f, WetGrade: 0.24f),
            "stone" => (Roughness: 0.91f, Specular: 0.12f, WetGrade: 0.22f),
            "foliage" => (Roughness: 0.95f, Specular: 0.07f, WetGrade: 0.12f),
            "plaster" => (Roughness: 0.96f, Specular: 0.06f, WetGrade: 0.04f),
            "fabric" or "cloth" => (Roughness: 0.98f, Specular: 0.04f, WetGrade: 0.01f),
            "water" => (Roughness: 0.38f, Specular: 0.45f, WetGrade: 0.98f),
            _ => (Roughness: 0.90f, Specular: 0.20f, WetGrade: 0.0f)
        };
        material.SetShaderParameter("roughness_value", surfaceGrade.Roughness);
        material.SetShaderParameter("specular_value", surfaceGrade.Specular);
        material.SetShaderParameter("wet_grade", surfaceGrade.WetGrade);
        material.SetShaderParameter("leaf_transmission", surface is "foliage" or "leaf_birch" ? 0.45f : 0f);
        material.SetShaderParameter("wind_sway", surface switch
        {
            "foliage" => 0.05f,
            "leaf_birch" => 0.07f,
            "grass_tuft" => 0.12f,
            "grass" => 0.0f,
            _ => 0.0f
        });
        material.SetShaderParameter("wind_enabled", _windMotion);
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
        else if (!SuppressTextureLoadsForHeadlessTests
            && WinterTextures.TryGetValue(surface, out var winterDescriptor)
            && global::Godot.FileAccess.FileExists(winterDescriptor.Path))
        {
            var texture = ResourceLoader.Load<Texture2D>(winterDescriptor.Path);
            if (texture is not null)
            {
                material.SetShaderParameter("albedo_texture", texture);
                material.SetShaderParameter("has_albedo_texture", true);
                material.SetShaderParameter("texture_scale", winterDescriptor.Scale);
            }
        }

        Materials.Add(cacheKey, material);
        return material;
    }
}
