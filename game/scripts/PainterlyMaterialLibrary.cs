using Godot;

namespace Urman.Godot;

public static class PainterlyMaterialLibrary
{
    private const string VertexDeformation = """
            // Ground microrelief is normal detail: its sub-centimetre height
            // cannot be represented by metre-wide base triangles. Displacing
            // newly refined vertices here would split coarse/fine borders.
            if (has_snow_micro && !low_quality && !trample_ground_surface) {
                float up = smoothstep(0.45, 0.85, world_normal.y);
                float height = (textureLod(snow_micro_response, world_position.xz * snow_micro_scale, 0.0).r - 0.5)
                    * 0.004 * snow_relief_scale * up;
                VERTEX += transpose(MODEL_NORMAL_MATRIX) * vec3(0.0, height, 0.0);
                world_position.y += height;
            }
            vec4 trail = snow_trample_at(world_position);
            float pressed_height = (trail.r + trail.g) * smoothstep(0.55, 0.85, world_normal.y);
            VERTEX += transpose(MODEL_NORMAL_MATRIX) * vec3(0.0, pressed_height, 0.0);
            world_position.y += pressed_height;
            if (wind_enabled && wind_sway > 0.0) {
                float phase = TIME * 1.6 + world_position.x * 0.55 + world_position.z * 0.4;
                float gust = sin(phase) * 0.65 + sin(phase * 2.3 + 1.7) * 0.35;
                float reach = max(VERTEX.y, 0.0);
                VERTEX.x += gust * wind_sway * reach;
                VERTEX.z += gust * wind_sway * 0.6 * reach;
            }
        """;

    private const string ShaderSource = """
        shader_type spatial;
        render_mode diffuse_burley, specular_schlick_ggx;

        uniform vec4 base_color : source_color;
        uniform bool edge_frost = false;
        uniform vec4 shadow_color : source_color;
        uniform float brush_scale = 0.7;
        uniform float variation = 0.16;
        uniform float texture_strength = 0.95;
        uniform float roughness_value = 0.9;
        uniform float specular_value = 0.2;
        uniform float metallic_value = 0.0;
        uniform float finish_grain = 0.0;
        uniform float wet_grade = 0.0;
        uniform float leaf_transmission = 0.0;
        uniform bool vertex_pigment = false;
        uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform bool has_albedo_texture = false;
        uniform bool cut_wood_end = false;
        // Directional wall/curtain patterns keep their vertical texture axis
        // on world Y on both wall orientations. Other established families
        // retain their existing projections (including floors and furniture).
        uniform bool upright_texture = false;
        // Opt-in local projection: the carried plank's grain follows local Z.
        // Offsets align its three rigid pieces with one texture swatch. Hay
        // bundles also enable upright_texture: the paired ZYX permutation
        // preserves local Y as the vertical fiber axis on both side faces.
        // Lighting, weather and snow continue to use the world-space varyings.
        uniform bool local_wood_texture = false;
        uniform bool local_floor_texture = false;
        // W02 rails are authored along local Z; map that length to texture V.
        uniform bool local_fence_rail = false;
        uniform vec3 local_wood_offset = vec3(0.0);
        // Packed hay already has a continuous circumferential/vertical UV
        // layout; preserve it instead of projecting fibers through the stack.
        uniform bool authored_uv_texture = false;
        uniform bool bound_uv_pigment = false;
        // Low uses one projected albedo read; medium keeps triplanar blending.
        uniform bool low_quality = false;
        uniform vec2 texture_scale = vec2(1.0);
        // Prod-ready phase 2: grounding darken near the ground line (0 = off),
        // per-world-cell tint jitter that de-clones repeated kits, and a
        // large-value macro breakup that keeps long walls/fields from
        // reading as one flat pour.
        uniform float ground_darken = 0.0;
        // VIS-033: the contact band is measured from each instance's own support
        // height (set per instance, so one shared material serves every post),
        // not from world Y = 0. The 3 m default keeps the legacy curve.
        instance uniform float ground_base_y = 0.0;
        uniform float ground_contact_height = 3.0;
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
        uniform bool snow_material = false;
        uniform bool has_snow_micro = false;
        uniform sampler2D snow_micro_response : filter_linear_mipmap_anisotropic, repeat_enable;
        uniform sampler2D snow_micro_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform vec2 snow_roughness_range = vec2(0.82, 0.95);
        uniform float snow_relief_scale = 1.0;
        uniform sampler2D trample_map : hint_default_black, filter_linear, repeat_disable;
        uniform bool has_trample_map = false;
        uniform bool trample_ground_surface = false;
        uniform vec2 trample_origin = vec2(0.0);
        uniform float trample_extent = 0.0;
        // Trodden village paths (ribbons whose UV.x runs 0..1 across): packed snow in the
        // middle with two worn foot lines, a ragged edge that fades into fresh snow.
        uniform bool soft_path_edges = false;
        // ── VIS-092: per-family material response, deliberately not asset-pack PBR.
        // Every block below is opt-in and its identity value is the pre-existing
        // behaviour, so a family that declares nothing renders exactly as before.
        // Relief comes from ALU noise first (no sampler, no 4K obligation); a map is
        // only bound where the drawn grain genuinely needs one, and normal, roughness
        // and wear are three independent slots so a family never pays for an ORM it
        // does not use.
        uniform float proc_relief = 0.0;
        uniform vec2 proc_relief_freq = vec2(6.0, 26.0);
        uniform float proc_roughness = 0.0;
        uniform bool has_detail_normal = false;
        uniform sampler2D detail_normal_map : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform float detail_normal_scale = 0.0;
        uniform bool has_detail_roughness = false;
        uniform sampler2D detail_roughness_map : hint_white, filter_linear, repeat_enable;
        uniform float detail_roughness_delta = 0.0;
        uniform bool has_wear_mask = false;
        uniform sampler2D wear_mask_map : hint_default_black, filter_linear, repeat_enable;
        uniform float wear_roughness_delta = 0.0;
        uniform float wear_metallic = 0.0;
        uniform vec4 wear_tint : source_color = vec4(1.0);
        // Explicit repeatability: how many tiles fit in one world metre. A family
        // must not be able to inherit a silent 1 m period, and the same field must
        // not shout at every distance (STYLE RECIPE §5).
        uniform vec2 detail_scale = vec2(1.0);
        // ── VIS-093: wear is placed by a physical cause (end grain, splash line,
        // handle contact), never by an even decorative pass.
        uniform float wear_bottom_gain = 0.0;
        uniform float wear_bottom_height = 0.35;
        uniform float wear_end_grain = 0.0;
        // ── VIS-080: snow settles from the piece's own shape. The band is per
        // family, a rolled rail caps along its own top face, and sheltered meshes
        // lose the cap through an instance value instead of a copied material.
        uniform vec2 snow_settle_band = vec2(0.30, 0.72);
        uniform bool snow_follows_local_normal = false;
        instance uniform float snow_shelter = 0.0;
        instance uniform vec3 instance_pigment_mul = vec3(1.0);
        // ── VIS-094: civic plaster carries authored macro heterogeneity (the
        // wavelength stays in the 2..8 m band) and a soiling line that starts at
        // the wall's own support, so a repair reads as a repair and not as a decal.
        uniform vec2 macro_scale = vec2(0.33, 0.11);
        uniform float macro_gain = 1.0;
        uniform float base_grime = 0.0;
        uniform float base_grime_height = 0.9;
        uniform vec4 base_grime_color : source_color = vec4(0.42, 0.40, 0.36, 1.0);
        // ── VIS-095: hoarfrost dulls the highlight of exposed horizontal metal;
        // age and frost live in roughness, not in a blanket specular increase.
        uniform float frost_roughness = 0.0;
        // ── VIS-034/081: the snow micro field keeps its sub-centimetre amplitude
        // (the vertex term is untouched) but its period becomes a declared value.
        uniform vec2 snow_micro_scale = vec2(1.0);
        uniform bool sparkle_grazing_only = false;

        varying vec3 world_position;
        varying vec3 world_normal;
        varying vec3 local_wood_position;
        varying vec3 local_wood_normal;

        vec3 triplanar_albedo(vec3 position, vec3 normal) {
            vec3 blend = pow(abs(normal), vec3(4.0));
            blend /= max(blend.x + blend.y + blend.z, 0.0001);
            vec2 x_uv = upright_texture ? position.zy : position.yz;
            vec3 x_projection = texture(albedo_texture, x_uv * texture_scale).rgb;
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

        vec4 snow_trample_at(vec3 position) {
            if (!trample_ground_surface || !has_trample_map || trample_extent <= 0.0) return vec4(0.0);
            vec2 uv = (position.xz - trample_origin) / trample_extent;
            if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) return vec4(0.0);
            vec4 field = textureLod(trample_map, uv, 0.0);
            float support = field.b / max(field.a, 0.00001);
            return abs(position.y - support) < 0.12 ? field : vec4(0.0);
        }

        // VIS-092. One projected read for the micro-response slots, choosing the
        // same dominant plane the albedo layer already chose, so relief can never
        // drift out of register with the painted grain. Blending three planes for
        // sub-centimetre detail would triple the sampler cost at the exact axis
        // where the family's own construction says which face the eye reads.
        vec2 response_uv(vec3 position, vec3 normal) {
            if (authored_uv_texture) return UV * detail_scale;
            vec3 axis = abs(normal);
            if (axis.y >= max(axis.x, axis.z)) return position.xz * detail_scale;
            if (axis.x >= axis.z) return (upright_texture ? position.zy : position.yz) * detail_scale;
            return position.xy * detail_scale;
        }

        // Maps the two in-plane slope channels back onto the plane response_uv
        // selected, using the identical test so the pair cannot diverge.
        vec3 response_slope(vec3 normal, vec2 slope) {
            vec3 axis = abs(normal);
            if (axis.y >= max(axis.x, axis.z)) return vec3(slope.x, 0.0, slope.y);
            if (axis.x >= axis.z) {
                return upright_texture ? vec3(0.0, slope.y, slope.x) : vec3(0.0, slope.x, slope.y);
            }
            return vec3(slope.x, slope.y, 0.0);
        }

        // Procedural relief: two central differences of the existing value noise.
        // Cheap ALU instead of a normal map, which is what keeps wood, plaster and
        // metal answering light differently without importing an ORM for each.
        vec2 proc_relief_slope(vec2 uv, vec2 freq) {
            vec2 p = uv * freq;
            float nx = painter_value_noise(p + vec2(0.5, 0.0)) - painter_value_noise(p - vec2(0.5, 0.0));
            float ny = painter_value_noise(p + vec2(0.0, 0.5)) - painter_value_noise(p - vec2(0.0, 0.5));
            return vec2(nx, ny);
        }

        // VIS-034/095. Detail has to disappear before it becomes shimmer, and a
        // frozen or trodden reading must never be paid for with a hard pattern.
        float response_detail_fade(vec2 uv) {
            float step = max(length(dFdx(uv)), length(dFdy(uv)));
            return 1.0 - smoothstep(0.05, 0.14, step);
        }

        void vertex() {
            local_wood_position = local_fence_rail ? VERTEX.xzy
                : (local_floor_texture ? VERTEX : (VERTEX + local_wood_offset).zyx);
            local_wood_normal = local_fence_rail ? NORMAL.xzy
                : (local_floor_texture ? NORMAL : NORMAL.zyx);
            world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
        """ + "\n" + VertexDeformation + "\n" + """
        }

        void fragment() {
            float wet_factor = clamp(wet_grade, 0.0, 1.0);
            if (edge_frost && has_albedo_texture) {
                // RGBA edge pigment overlays intact glass, including on Low.
                vec4 frost = texture(albedo_texture, UV);
                ALBEDO = mix(base_color.rgb, frost.rgb, frost.a);
                ROUGHNESS = mix(0.30, 0.86, frost.a);
                SPECULAR = 0.35;
                METALLIC = 0.0;
            } else if (low_quality) {
                vec3 low_color = base_color.rgb * instance_pigment_mul;
                if (has_albedo_texture) {
                    vec3 position = (local_wood_texture || local_floor_texture) ? local_wood_position : world_position;
                    vec3 axis = abs((local_wood_texture || local_floor_texture) ? local_wood_normal : world_normal);
                    vec2 uv = authored_uv_texture ? UV
                        : axis.y >= max(axis.x, axis.z) ? position.xz
                        : axis.x >= axis.z ? (upright_texture ? position.zy : position.yz) : position.xy;
                    vec3 detail = texture(albedo_texture, uv * texture_scale).rgb;
                    vec3 tinted = snow_material
                        ? low_color * (vec3(0.90) + detail * 0.12)
                        : low_color * (vec3(0.30) + detail * 1.75);
                    low_color = mix(low_color, tinted, texture_strength);
                }
                ALBEDO = mix(shadow_color.rgb * instance_pigment_mul, low_color, 0.82)
                    * mix(vec3(1.0), vec3(0.90, 0.95, 0.98), wet_factor);
                if (snow_coverage > 0.0) {
                    float snow_up = clamp(normalize(world_normal).y, 0.0, 1.0);
                    float snow_cover = smoothstep(0.35, 0.75, snow_up) * snow_coverage
                        * (1.0 - clamp(snow_shelter, 0.0, 1.0));
                    ALBEDO = mix(ALBEDO, snow_color.rgb, snow_cover);
                }
                ROUGHNESS = clamp(roughness_value - wet_factor * 0.26, 0.05, 1.0);
                SPECULAR = clamp(specular_value + wet_factor * 0.22, 0.0, 1.0);
                METALLIC = metallic_value;
            } else {
            vec3 pigment_position = bound_uv_pigment ? vec3(UV, 0.0) : world_position;
            // VIS-038: a family shares one cached material and each instance carries
            // its own pigment ratio, so a fence of six tints is six instances of one
            // material state instead of six material states. Identity is vec3(1).
            vec3 pigment = base_color.rgb * instance_pigment_mul;
            // Quieter stroke wash: with the promoted candidate textures the
            // texture now carries the value rhythm; strokes only keep the
            // painted plane alive.
            float stroke = sin(
                (pigment_position.x * 0.37 + pigment_position.z * 0.43 + pigment_position.y * 0.19)
                * brush_scale * 2.1);
            float broad_stroke = sin(
                (pigment_position.x * 0.11 + pigment_position.z * 0.07 + pigment_position.y * 0.05)
                * brush_scale * 0.85
                + cos((pigment_position.x * 0.17 - pigment_position.z * 0.09) * brush_scale * 0.62));
            float wash = clamp(
                0.80 + stroke * variation * 0.10 + broad_stroke * variation * 0.18,
                0.55,
                1.0);
            float upward = clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)) * 0.08 + 0.92, 0.84, 1.0);
            vec3 albedo_position = (local_wood_texture || local_floor_texture) ? local_wood_position : world_position;
            vec3 albedo_normal = (local_wood_texture || local_floor_texture) ? local_wood_normal : world_normal;
            vec3 texture_color = has_albedo_texture
                ? (authored_uv_texture
                    ? texture(albedo_texture, UV * texture_scale).rgb
                    : triplanar_albedo(albedo_position, normalize(albedo_normal)))
                : vec3(1.0);
            // source_color samples are already linear. Contrast around 0.5
            // crushed dark pigment before the existing tint and lighting.
            // Texture-dominant balance: the albedo painting owns the value
            // range; base_color acts as a light tint over it.
            vec3 tinted_texture = snow_material
                ? pigment * (vec3(0.90) + texture_color * 0.12)
                : pigment * (vec3(0.30) + texture_color * 1.75);
            vec3 painted_color = has_albedo_texture
                ? mix(pigment, tinted_texture, texture_strength)
                : pigment;
            // End caps use their local cross-section UVs, so grain belongs
            // to each log instead of projecting board stripes across a pile.
            if (cut_wood_end) {
                vec2 grain = UV * 2.0 - vec2(1.0);
                grain += vec2(sin(grain.y * 7.0), cos(grain.x * 6.0)) * 0.035;
                float ring = smoothstep(0.72, 0.98,
                    sin(length(grain) * 33.0 + sin(grain.x * 9.0) * 0.35));
                // Fade rings below pixel size instead of shimmering at distance.
                ring *= 1.0 - smoothstep(0.65, 1.5, fwidth(length(grain) * 33.0));
                painted_color *= 1.0 - ring * 0.22;
            }
            vec3 painted_shadow = has_albedo_texture || cut_wood_end ? painted_color * 0.86 : shadow_color.rgb * instance_pigment_mul;
            float edge_wash = 0.95 + 0.05 * clamp(dot(normalize(world_normal), vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
            float macro_pigment = clamp(
                0.98 + stroke * variation * 0.08 + broad_stroke * variation * 0.16,
                0.88,
                1.06);
            // ~3 m value breakup by default: broad stains across walls and fields.
            // VIS-094 makes the wavelength and its strength declared per family so a
            // civic facade can hold 2..8 m heterogeneity instead of inheriting one
            // shared number, while macro_gain=1 keeps every existing building as-is.
            float macro_stain = mix(0.93, 1.05,
                painter_value_noise(pigment_position.xz * macro_scale.x + pigment_position.y * macro_scale.y));
            macro_stain = 1.0 + (macro_stain - 1.0) * macro_gain;
            // Grounding: darken the first metres above the ground line so
            // facades/fences sit into the dirt instead of floating on it.
            float ground_line = 1.0 - ground_darken
                * (1.0 - clamp((world_position.y - ground_base_y) / ground_contact_height, 0.0, 1.0));
            wet_factor = clamp(wet_grade * (0.74 + 0.26 * upward), 0.0, 1.0);
            ALBEDO = mix(painted_shadow, painted_color, wash)
                * macro_pigment
                * macro_stain
                * cell_tint(pigment_position, cell_jitter)
                * ground_line
                * upward
                * edge_wash
                * mix(vec3(1.0), vec3(0.90, 0.95, 0.98), wet_factor);
            ROUGHNESS = clamp(roughness_value - wet_factor * 0.26, 0.05, 1.0);
            SPECULAR = clamp(specular_value + wet_factor * 0.22, 0.0, 1.0);
            METALLIC = metallic_value;
            }
            // Painted enamel and bare heater iron need a restrained surface
            // response, not the masonry albedo formerly bound to both props.
            // This grain affects roughness only and fades below a pixel.
            if (finish_grain > 0.0 && !low_quality) {
                vec2 finish_uv = (world_position.xz + world_position.y * vec2(0.73, 0.41)) * 24.0;
                float finish_detail = 1.0 - smoothstep(0.35, 1.0,
                    max(length(dFdx(finish_uv)), length(dFdy(finish_uv))));
                ROUGHNESS = clamp(ROUGHNESS
                    + (painter_value_noise(finish_uv) - 0.5) * finish_grain * finish_detail,
                    0.05, 1.0);
            }
            // VIS-092/093/094/095: the family response block. Everything in it is
            // gated by its own authored value and by !low_quality, exactly like
            // finish_grain and the snow micro field, so a preset that cannot afford
            // the reads keeps the previous image. Nothing here adds a fourth texture
            // fetch unless a family explicitly binds one.
            if (!low_quality
                && (proc_relief > 0.0 || proc_roughness > 0.0 || has_detail_normal
                    || has_detail_roughness || has_wear_mask || wear_bottom_gain > 0.0
                    || wear_end_grain > 0.0 || base_grime > 0.0 || frost_roughness > 0.0)) {
                vec3 response_position = (local_wood_texture || local_floor_texture)
                    ? local_wood_position : world_position;
                vec3 response_normal = normalize((local_wood_texture || local_floor_texture)
                    ? local_wood_normal : world_normal);
                vec2 detail_sample_uv = response_uv(response_position, response_normal);
                float response_detail = response_detail_fade(detail_sample_uv);
                // 1. Micro relief. Anisotropic by construction: the frequency pair is
                // authored per family, so boards stretch along the grain and plaster
                // does not inherit a plank rhythm.
                vec2 relief = vec2(0.0);
                // The fade is the cost guard, not only an anti-shimmer term: relief is
                // five value-noise evaluations (20 sin), and beyond ~0.14 UV units per
                // pixel it is provably sub-pixel, so it is skipped instead of computed
                // and multiplied away. Long village facades therefore pay it only close.
                float detail_budget = response_detail > 0.001 ? 1.0 : 0.0;
                if (proc_relief > 0.0 && detail_budget > 0.0)
                    relief = proc_relief_slope(detail_sample_uv, proc_relief_freq) * proc_relief;
                if (has_detail_normal && detail_normal_scale > 0.0 && detail_budget > 0.0) {
                    vec3 micro = texture(detail_normal_map, detail_sample_uv).rgb * 2.0 - 1.0;
                    relief += vec2(micro.x, micro.y) * detail_normal_scale;
                }
                relief *= response_detail;
                if (dot(relief, relief) > 0.000001) {
                    NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(response_slope(response_normal, relief), 0.0)).xyz);
                }
                // 2. Roughness of the material itself: drawn first, then procedural,
                // so age reads as a distribution rather than as more specular.
                float rough_add = 0.0;
                if (has_detail_roughness && detail_roughness_delta != 0.0) {
                    rough_add += (texture(detail_roughness_map, detail_sample_uv).r - 0.5)
                        * detail_roughness_delta * response_detail;
                }
                if (proc_roughness > 0.0 && detail_budget > 0.0) {
                    rough_add += (painter_value_noise(detail_sample_uv * proc_relief_freq * 0.5) - 0.5)
                        * proc_roughness * response_detail;
                }
                // 3. Wear and exposed substrate, placed by cause. The mask is rare:
                // a family that ships without one simply never enters this branch.
                if (has_wear_mask) {
                    float wear = clamp(texture(wear_mask_map, detail_sample_uv).r * response_detail, 0.0, 1.0);
                    if (wear > 0.001) {
                        rough_add += wear * wear_roughness_delta;
                        METALLIC = mix(METALLIC, wear_metallic, wear);
                        ALBEDO = mix(ALBEDO, ALBEDO * wear_tint.rgb, wear * wear_tint.a);
                    }
                }
                // 4. Splash/snowmelt line measured from the instance's own support
                // (VIS-033's ground_base_y), so a board wears at its lower end on a
                // slope and on a flat yard without a material per placement.
                if (wear_bottom_gain > 0.0) {
                    float below = 1.0 - clamp((world_position.y - ground_base_y)
                        / max(wear_bottom_height, 0.001), 0.0, 1.0);
                    float ragged = 0.70 + 0.55 * painter_value_noise(
                        response_position.xz * 2.7 + response_position.y * 1.3);
                    float bottom_wear = clamp(below * below * ragged * wear_bottom_gain, 0.0, 0.85);
                    ALBEDO = mix(ALBEDO, ALBEDO * vec3(0.74, 0.70, 0.64) + vec3(0.015, 0.02, 0.026), bottom_wear);
                    rough_add += bottom_wear * 0.14;
                }
                // 5. End grain drinks what the board face sheds (VIS-093): the cut
                // face already draws its rings, so its wear follows the rings.
                if (wear_end_grain > 0.0 && cut_wood_end) {
                    float rings = smoothstep(0.55, 1.0, abs(sin(length(UV * 2.0 - 1.0) * 33.0)));
                    rough_add += rings * wear_end_grain;
                    ALBEDO *= 1.0 - rings * wear_end_grain * 0.35;
                }
                // 6. Civic soiling that starts at the wall's own base and thins out
                // upward, capped so a facade never turns into a mould patch (VIS-094).
                if (base_grime > 0.0) {
                    float rise = 1.0 - clamp((world_position.y - ground_base_y)
                        / max(base_grime_height, 0.001), 0.0, 1.0);
                    float grime_ragged = 0.55 + 0.65 * painter_value_noise(
                        response_position.xz * 0.85 + response_position.y * 0.35);
                    float grime = clamp(base_grime * pow(rise, 1.6) * grime_ragged, 0.0, 0.45);
                    ALBEDO = mix(ALBEDO, ALBEDO * base_grime_color.rgb, grime);
                    rough_add += grime * 0.10;
                }
                ROUGHNESS = clamp(ROUGHNESS + rough_add, 0.05, 1.0);
                // 7. Hoarfrost: a thin dull skin on exposed up-facing metal raises
                // roughness and eats the highlight instead of adding a shine.
                if (frost_roughness > 0.0) {
                    float frost_up = smoothstep(0.45, 0.85, normalize(world_normal).y);
                    ROUGHNESS = clamp(ROUGHNESS + frost_roughness * frost_up, 0.05, 1.0);
                    SPECULAR = max(0.0, SPECULAR - frost_roughness * frost_up * 0.5);
                }
            }
            // Thin leaves receive light on their reverse face; this remains
            // light-dependent, not emission that would glow in the forest.
            if (vertex_pigment) {
                ALBEDO *= snow_material ? mix(vec3(1.0), COLOR.rgb, 0.28) : COLOR.rgb;
            }
            // Winter snow blanket: up-facing surfaces take a matte, slightly
            // mottled snow layer on top of the existing paint, so roofs,
            // rails, woodpiles and wall caps are snowed without new geometry.
            if (snow_coverage > 0.0) {
                float snow_up = clamp(normalize(world_normal).y, 0.0, 1.0);
                // VIS-080. Three authored parts, none of them a global overlay:
                // the settle band is per family (a pitched sheet caps only where it
                // is nearly horizontal, a wall only along its parapet), a bundle in
                // the zyx local frame caps along its own top face instead of along
                // world Y after a roll, and a mesh under a canopy or inside a room
                // lowers the per-instance shelter value rather than copying material.
                // The xzy rail permutation moves its length axis into Y, so rails
                // keep the world band on purpose.
                float settle_up = snow_follows_local_normal
                    ? clamp(normalize(local_wood_normal).y * (FRONT_FACING ? 1.0 : -1.0), 0.0, 1.0)
                    : snow_up;
                float snow_cover = smoothstep(snow_settle_band.x, snow_settle_band.y, settle_up) * snow_coverage;
                snow_cover *= 1.0 - clamp(snow_shelter, 0.0, 1.0);
                snow_cover *= 0.72 + 0.48 * painter_value_noise(
                    world_position.xz * 0.5 + world_position.y * 0.21);
                snow_cover = clamp(snow_cover, 0.0, 1.0);
                vec3 snow_albedo = snow_color.rgb
                    * (0.95 + 0.05 * painter_value_noise(world_position.xz * 1.9));
                ALBEDO = mix(ALBEDO, snow_albedo, snow_cover);
                ROUGHNESS = mix(ROUGHNESS, 0.92, snow_cover);
                SPECULAR = mix(SPECULAR, 0.05, snow_cover);
                METALLIC = mix(METALLIC, 0.0, snow_cover);
            }
            // One generated height field supplies geometry, normal and
            // roughness. Grain changes only the light-dependent BRDF; there
            // is no added albedo, emission, TIME noise or shadow sparkle.
            if (snow_material) {
                METALLIC = 0.0;
                ROUGHNESS = clamp(ROUGHNESS, snow_roughness_range.x, snow_roughness_range.y);
                if (has_snow_micro && !low_quality) {
                    // VIS-034/081: the declared period replaces the implicit 1 m tile.
                    // The vertex amplitude stays (r-0.5)*0.004, i.e. +/-0.002 m, the
                    // value measured for real winded snow microrelief [K02]; making the
                    // period bigger is a pattern decision, not a dune substitute.
                    vec2 micro_uv = world_position.xz * snow_micro_scale;
                    vec3 response = texture(snow_micro_response, micro_uv).rgb;
                    float pixel_metres = max(length(dFdx(world_position.xz)), length(dFdy(world_position.xz)));
                    float detail = 1.0 - smoothstep(0.006, 0.03, pixel_metres);
                    vec3 micro = texture(snow_micro_normal, micro_uv).rgb * 2.0 - 1.0;
                    vec3 slope = vec3(micro.x, 0.0, micro.y) / max(micro.z, 0.2);
                    vec3 surface_normal = normalize(world_normal + slope * snow_relief_scale
                        * smoothstep(0.45, 0.85, world_normal.y) * detail);
                    NORMAL = normalize((VIEW_MATRIX * vec4(surface_normal, 0.0)).xyz);
                    float grain = response.b * detail * snow_sparkle;
                    if (sparkle_grazing_only) {
                        // A crystal flashes where the light reflects off its facet.
                        // A flat specular lift over the whole sheet is what read as
                        // glitter under diffuse sky light (VIS-081 acceptance).
                        float grazing = 1.0 - clamp(dot(normalize(NORMAL), normalize(-VERTEX)), 0.0, 1.0);
                        grain *= smoothstep(0.12, 0.70, grazing);
                    }
                    ROUGHNESS = clamp(mix(snow_roughness_range.x, snow_roughness_range.y, response.g)
                        - grain * 0.04, snow_roughness_range.x, snow_roughness_range.y);
                    SPECULAR = specular_value + grain * 0.08;
                }
            }
            if (soft_path_edges) {
                float across = abs(UV.x - 0.5) * 2.0;
                float ragged = painter_value_noise(world_position.xz * 1.3)
                    + 0.5 * painter_value_noise(world_position.xz * 4.7) - 0.75;
                float trodden = 1.0 - smoothstep(0.38, 0.92, across + ragged * 0.32);
                float lines = 1.0 - smoothstep(0.0, 0.14, abs(across - 0.26 + ragged * 0.08));
                float prints = smoothstep(0.4, 0.8, painter_value_noise(world_position.xz * vec2(3.9, 4.3)));
                vec3 packed_snow = ALBEDO * (1.0 - 0.1 * lines * prints) * mix(1.0, 0.96, painter_value_noise(world_position.xz * 0.8));
                vec3 fresh = snow_color.rgb * (0.96 + 0.05 * painter_value_noise(world_position.xz * 1.9));
                ALBEDO = mix(fresh, packed_snow, trodden);
                ROUGHNESS = mix(0.93, ROUGHNESS, trodden);
            }
            vec4 trail = snow_trample_at(world_position);
            if (trail.a > 0.0) {
                float texel = trample_extent / float(textureSize(trample_map, 0).x);
                vec4 left = snow_trample_at(world_position - vec3(texel, 0.0, 0.0));
                vec4 right = snow_trample_at(world_position + vec3(texel, 0.0, 0.0));
                vec4 back = snow_trample_at(world_position - vec3(0.0, 0.0, texel));
                vec4 front = snow_trample_at(world_position + vec3(0.0, 0.0, texel));
                float dx = (right.r + right.g - left.r - left.g) / (2.0 * texel);
                float dz = (front.r + front.g - back.r - back.g) / (2.0 * texel);
                float up = smoothstep(0.55, 0.85, world_normal.y);
                NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(-dx, 0.0, -dz, 0.0)).xyz * up);
                float packed = clamp(-trail.r / 0.035, 0.0, 1.0) * up;
                // VIS-034: packed snow is denser and cooler, not a cut-out hole. The
                // old uniform 0.91 greyed the print down; the same luminance now shifts
                // toward blue so a footprint stays inside the snow family.
                ALBEDO *= mix(vec3(1.0), vec3(0.905, 0.930, 0.985), packed);
                ROUGHNESS = mix(ROUGHNESS, clamp(0.79, snow_roughness_range.x, snow_roughness_range.y), packed);
            }
            BACKLIGHT = ALBEDO * leaf_transmission;
        }
        """;

    private static readonly Shader PainterlyShader = new() { Code = ShaderSource };
    private static readonly Shader RigidPainterlyShader = new()
    {
        // Opaque materials whose deformation inputs are all inert preserve their
        // visible finish without the VERTEX writes, which is what lets the
        // renderer reach its shared shadow material.
        Code = ShaderSource.Replace(VertexDeformation, string.Empty, StringComparison.Ordinal)
    };

    static PainterlyMaterialLibrary()
    {
        // ShaderSource is assembled as literal + "\n" + VertexDeformation + "\n" +
        // literal, so the removal above is exact by construction. If a future edit
        // breaks that seam, the replacement silently becomes a no-op and every
        // deformation-capable material would keep the full shader - a lost
        // optimization, never a wrong image. Report it instead of hiding it.
        if (string.Equals(RigidPainterlyShader.Code, ShaderSource, StringComparison.Ordinal))
            global::Godot.GD.PushWarning(
                "PainterlyMaterialLibrary: the rigid shader no longer differs from the full one; "
                + "the vertex deformation block was not removed.");
    }
    private static readonly Shader TwoSidedPainterlyShader = new()
    {
        // Imported opaque sheets may explicitly expose both sides. Keep all
        // painterly parameters and opaque rendering; this is not a cutout.
        Code = ShaderSource
            .Replace("render_mode diffuse_burley, specular_schlick_ggx;", "render_mode diffuse_burley, specular_schlick_ggx, cull_disabled;")
            .Replace("float snow_up = clamp(normalize(world_normal).y,", "float snow_up = clamp(normalize(world_normal).y * (FRONT_FACING ? 1.0 : -1.0),")
    };
    private static readonly Shader CutoutShader = new()
    {
        Code = ShaderSource
            .Replace("render_mode diffuse_burley, specular_schlick_ggx;", "render_mode diffuse_burley, specular_schlick_ggx, cull_disabled;")
            .Replace("float snow_up = clamp(normalize(world_normal).y,", "float snow_up = clamp(normalize(world_normal).y * (FRONT_FACING ? 1.0 : -1.0),")
            .Replace("void fragment() {", "uniform sampler2D cutout_texture : filter_linear_mipmap_anisotropic, repeat_disable;\nvoid fragment() {\nALPHA = texture(cutout_texture, UV).a;\nALPHA_SCISSOR_THRESHOLD = 0.2;")
    };

    private static readonly Dictionary<string, (string Path, Vector2 Scale)> SurfaceTextures = new(StringComparer.Ordinal)
    {
        // Phase 1A promotion (prod-ready plan): per-family candidate chosen
        // from the v2-v6 comparison strips; see evidence/act1_prod_ready/.
        // Facades/fences carry the calm plank read (v4); furniture/props the
        // cleaner planks (v3); road keeps painterly ruts (v3), open terrain
        // the softer mottle (v5).
        ["wood"] = ("res://assets/textures/painterly/weathered_wood_boards_v4_albedo.png", new Vector2(0.5f, 0.5f)),
        // Catalogue maps are scoped by actual finish. Legacy wood/fence/prop
        // owners remain independent; new paint does not recolor every house.
        ["wood_facade"] = ("res://assets/textures/painterly/urman_w01_v01_basecolor.png", Vector2.One),
        ["wood_log_uv"] = ("res://assets/textures/painterly/urman_w01_v01_basecolor.png", Vector2.One),
        ["wood_painted_blue"] = ("res://assets/textures/painterly/urman_w03_v01_basecolor.png", Vector2.One),
        ["wood_painted_green"] = ("res://assets/textures/painterly/urman_w04_v02_basecolor.png", Vector2.One),
        ["wood_painted_trim"] = ("res://assets/textures/painterly/urman_w05_v02_basecolor.png", new Vector2(2f, 2f)),
        ["wood_floor_painted"] = ("res://assets/textures/painterly/urman_w09_v01_basecolor.png", Vector2.One),
        ["wood_fence"] = ("res://assets/textures/painterly/weathered_wood_boards_v2_albedo.png", new Vector2(0.55f, 0.55f)),
        // Opt-in pieces with known local grain axes. Mixed imported fence
        // meshes keep the legacy material until their individual axes are mapped.
        ["wood_fence_vertical"] = ("res://assets/textures/painterly/urman_w02_v02_basecolor.png", Vector2.One),
        ["wood_fence_rail"] = ("res://assets/textures/painterly/urman_w02_v02_basecolor.png", Vector2.One),
        ["wood_fence_uv"] = ("res://assets/textures/painterly/urman_w02_v02_basecolor.png", Vector2.One),
        ["wood_furniture"] = ("res://assets/textures/painterly/weathered_wood_boards_v3_albedo.png", new Vector2(0.95f, 0.95f)),
        // W08: opt-in finished furniture; the legacy owner also serves floors
        // and exterior benches, which must not be varnished by a global swap.
        ["wood_furniture_interior"] = ("res://assets/textures/painterly/urman_w08_v01_basecolor.png", new Vector2(1f / 1.2f, 1f / 1.2f)),
        ["wood_prop"] = ("res://assets/textures/painterly/weathered_wood_boards_v3_albedo.png", new Vector2(0.9f, 0.9f)),
        ["wood_bark"] = ("res://assets/textures/painterly/bark_pine_v1_albedo.png", new Vector2(1.05f, 1.05f)),
        ["bark_pine"] = ("res://assets/textures/painterly/urman_f02_v02_basecolor.png", Vector2.One),
        ["plaster_domestic"] = ("res://assets/textures/painterly/urman_b01_v01_basecolor.png", Vector2.One),
        ["cloth_table"] = ("res://assets/textures/painterly/urman_t01_v02_basecolor.png", new Vector2(2f, 2f)),
        ["cloth_curtain"] = ("res://assets/textures/painterly/urman_t04_v01_basecolor.png", new Vector2(2f, 2f)),
        // The profile provides three repeats around the stack and .75 V per
        // local metre. Unit scale retains that existing physical UV mapping.
        ["hay_fibers"] = ("res://assets/textures/painterly/hay_fibers_v1_albedo.png", Vector2.One),
        // Authored bundles have no UVs. Their metre-sized local bounds keep
        // fibers attached to each bundle at 1.4 repeats per metre.
        ["hay_bundle"] = ("res://assets/textures/painterly/hay_fibers_v1_albedo.png", new Vector2(1.4f, 1.4f)),
        ["earth"] = ("res://assets/textures/painterly/damp_earth_v3_albedo.png", new Vector2(0.55f, 1.1f)),
        ["terrain"] = ("res://assets/textures/painterly/damp_earth_v5_albedo.png", new Vector2(1.3f, 2.6f)),
        ["wet_road"] = ("res://assets/textures/painterly/damp_earth_v3_albedo.png", new Vector2(0.55f, 1.1f)),
        // Wet shoulders and ditch planes reuse the selected damp-earth family;
        // their semantic grade supplies the restrained reflective difference.
        ["wet_ground"] = ("res://assets/textures/painterly/damp_earth_v5_albedo.png", new Vector2(1.0f, 2.1f)),
        ["plaster"] = ("res://assets/textures/painterly/aged_plaster_v3_albedo.png", new Vector2(0.7f, 0.55f)),
        ["foliage"] = ("res://assets/textures/painterly/pine_foliage_v2_albedo.png", new Vector2(1.4f, 1.4f)),
        // These surfaces have no v1 predecessor. They are scoped to the
        // authored stone/fabric presentation anchors in the benchmark scenes;
        // existing wood/plaster/earth/foliage mappings remain v1.
        ["stone"] = ("res://assets/textures/painterly/mossy_stone_v3_albedo.png", new Vector2(1.5f, 1.5f)),
        ["stone_foundation"] = ("res://assets/textures/painterly/urman_b07_v02_basecolor.png", Vector2.One),
        ["fabric"] = ("res://assets/textures/painterly/old_fabric_v3_albedo.png", new Vector2(2.0f, 2.0f)),
        ["cloth_towel"] = ("res://assets/textures/painterly/urman_t07_v01_basecolor.png", new Vector2(2.0f, 2.0f)),
        ["fabric_upholstery"] = ("res://assets/textures/painterly/urman_t08_v01_basecolor.png", new Vector2(2.0f, 2.0f)),
        // T10 has its own semantic owner; clothes and upholstery keep theirs.
        ["cloth_clinic"] = ("res://assets/textures/painterly/urman_t10_v02_basecolor.png", new Vector2(2.0f, 2.0f)),
        ["plastic_abs"] = ("res://assets/textures/painterly/urman_m05_v01_basecolor.png", new Vector2(2.0f, 2.0f)),
        // Folded privacy curtains share the woven source with upholstery,
        // but use a finer physical repeat. The sheer layer owns transparency.
        ["fabric_pattern"] = ("res://assets/textures/painterly/old_fabric_v3_albedo.png", new Vector2(2.6f, 2.6f)),
        ["carpet"] = ("res://assets/textures/painterly/carpet_palas_v1_albedo.png", new Vector2(0.25f, 0.40f)),
        // Image V runs downward; wall height runs upward. Keep stems below flowers.
        // VIS-035: the repeat doubles (0.91 m -> 1.82 m) so the ornament stops reading
        // as a grid across a 5 m wall and stops competing with the seated face.
        ["wallpaper"] = ("res://assets/textures/painterly/wallpaper_old_v1_albedo.png", new Vector2(0.55f, -0.55f)),
        ["log_wall"] = ("res://assets/textures/painterly/log_wall_v1_albedo.png", new Vector2(0.9f, 0.9f)),
        ["wall_institution"] = ("res://assets/textures/painterly/urman_b03_v01_basecolor.png", new Vector2(0.55f, 0.55f)),
        // B04: a dedicated one-metre linoleum field, not the wall plaster.
        // Keep actual floor panels and contact wear in their existing owners.
        ["floor_institution"] = ("res://assets/textures/painterly/urman_b04_v01_basecolor.png", Vector2.One),
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
        ["snow_ground"] = ("res://assets/textures/painterly/urman_s01_v01_basecolor.png", new Vector2(.5f, .5f)),
        ["snow_grass"] = ("res://assets/textures/painterly/snow_grass_peek_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["snow_road"] = ("res://assets/textures/painterly/urman_s02_v02_basecolor.png", new Vector2(.5f, .5f)),
        ["snow_trampled"] = ("res://assets/textures/painterly/snow_trampled_v1_albedo.png", new Vector2(1.6f, 1.6f)),
        ["snow_roof"] = ("res://assets/textures/painterly/snow_roof_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["ice"] = ("res://assets/textures/painterly/ice_patch_v1_albedo.png", new Vector2(1.4f, 1.4f)),
        ["bark_birch_winter"] = ("res://assets/textures/painterly/urman_f01_v03_basecolor.png", new Vector2(1f / .75f, 1f / 1.5f)),
        ["rowan_berries"] = ("res://assets/textures/painterly/rowan_berries_v1_albedo.png", new Vector2(1.0f, 1.0f)),
        ["wattle"] = ("res://assets/textures/painterly/wattle_weave_v1_albedo.png", new Vector2(0.9f, 0.9f)),
        ["frost_window"] = ("res://assets/textures/painterly/frost_window_v1_albedo.png", new Vector2(1.0f, 1.0f))
    };

    // VIS-092/093/094/095. Response maps are the second, optional half of a family:
    // relief and roughness are authored as numbers first (ALU, no new sampler), and a
    // drawn map only refines them where the drawn grain is the point. Every path here
    // is bound conditionally, exactly like WinterTextures: while the file is absent the
    // family keeps the procedural response and the current image, so an undelivered
    // map is never a black slot. Requests live in
    // docs/urman_knowledge_base/art/asset_requests/MAT.md and the gate
    // eng/verify-painterly-textures.sh --contract fails if a declared path is neither
    // on disk nor registered there.
    private const string ResponseRoot = "res://assets/textures/response/";

    private sealed record FamilyResponse
    {
        /// <summary>Amplitude of the procedural micro-normal, 0 = no relief.</summary>
        public float Relief { get; init; }
        /// <summary>Noise cells per metre along (U,V) of the projected plane. An
        /// anisotropic pair is how a plank keeps its grain direction.</summary>
        public Vector2 ReliefFreq { get; init; } = new(6f, 6f);
        /// <summary>Half-width of the procedural roughness spread.</summary>
        public float ReliefRoughness { get; init; }
        public string? NormalMap { get; init; }
        public float NormalScale { get; init; }
        public string? RoughnessMap { get; init; }
        public float RoughnessDelta { get; init; }
        public string? WearMap { get; init; }
        public float WearRoughness { get; init; }
        public float WearMetallic { get; init; }
        public string WearTint { get; init; } = "ffffff";
        public float WearTintBlend { get; init; }
        /// <summary>Tiles per world metre for the drawn slots (explicit period).</summary>
        public float TilesPerMetre { get; init; } = 1f;
        /// <summary>Splash/snowmelt wear measured from the instance's own support.</summary>
        public float WearBottom { get; init; }
        public float WearBottomHeight { get; init; } = .35f;
        /// <summary>Cut end grain wears because it absorbs what the face sheds.</summary>
        public float EndGrainWear { get; init; }
        /// <summary>Hoarfrost dulling on exposed up-facing surfaces.</summary>
        public float FrostRoughness { get; init; }
    }

    private static readonly Dictionary<string, FamilyResponse> FamilyResponses = new(StringComparer.Ordinal)
    {
        // Facade boards: long along the wall, tight across it. The wear map is the
        // VIS-093 request; until it ships the bottom wear alone carries history.
        ["wood_facade"] = new() { Relief = .38f, ReliefFreq = new(2.6f, 22f), ReliefRoughness = .10f,
            NormalMap = ResponseRoot + "wood_grain_v1_normal.png", NormalScale = .35f,
            RoughnessMap = ResponseRoot + "wood_grain_v1_roughness.png", RoughnessDelta = .14f,
            WearMap = ResponseRoot + "wood_wear_v1_mask.png", WearRoughness = .18f, WearMetallic = 0f,
            WearTint = "6b5a48", WearTintBlend = .55f, TilesPerMetre = .5f,
            WearBottom = .45f, WearBottomHeight = .30f, FrostRoughness = .04f },
        ["wood_fence"] = new() { Relief = .45f, ReliefFreq = new(3.2f, 30f), ReliefRoughness = .12f,
            NormalMap = ResponseRoot + "wood_grain_v1_normal.png", NormalScale = .30f,
            WearMap = ResponseRoot + "wood_wear_v1_mask.png", WearRoughness = .20f,
            WearTint = "6b5a48", WearTintBlend = .50f, TilesPerMetre = .5f,
            WearBottom = .60f, WearBottomHeight = .34f, FrostRoughness = .05f },
        ["wood"] = new() { Relief = .30f, ReliefFreq = new(3.5f, 24f), ReliefRoughness = .08f,
            WearBottom = .35f, WearBottomHeight = .28f },
        ["wood_prop"] = new() { Relief = .30f, ReliefFreq = new(3.5f, 24f), ReliefRoughness = .08f,
            WearBottom = .25f, WearBottomHeight = .18f },
        ["wood_furniture"] = new() { Relief = .26f, ReliefFreq = new(4f, 26f), ReliefRoughness = .07f,
            WearBottom = .18f, WearBottomHeight = .10f },
        ["wood_furniture_interior"] = new() { Relief = .24f, ReliefFreq = new(4f, 28f), ReliefRoughness = .06f },
        ["wood_cut"] = new() { Relief = .18f, ReliefFreq = new(9f, 9f), EndGrainWear = .22f },
        ["wood_carved"] = new() { Relief = .34f, ReliefFreq = new(6f, 20f), ReliefRoughness = .08f },
        ["wood_bark"] = new() { Relief = .70f, ReliefFreq = new(5f, 16f), ReliefRoughness = .14f },
        ["bark_birch"] = new() { Relief = .55f, ReliefFreq = new(4f, 14f), ReliefRoughness = .12f },
        ["bark_birch_winter"] = new() { Relief = .55f, ReliefFreq = new(4f, 14f), ReliefRoughness = .12f },
        ["bark_pine"] = new() { Relief = .72f, ReliefFreq = new(4.5f, 13f), ReliefRoughness = .15f },
        // Plaster is isotropic and macro-driven: relief stays under 5 mm equivalent so
        // it never competes with the 2..8 m heterogeneity the civic block owns (VIS-094).
        ["plaster"] = new() { Relief = .26f, ReliefFreq = new(8f, 8f), ReliefRoughness = .14f,
            NormalMap = ResponseRoot + "civic_plaster_v1_normal.png", NormalScale = .28f,
            RoughnessMap = ResponseRoot + "civic_plaster_v1_roughness.png", RoughnessDelta = .16f,
            TilesPerMetre = .45f, WearBottom = .20f, WearBottomHeight = .40f },
        ["wall_institution"] = new() { Relief = .26f, ReliefFreq = new(8f, 8f), ReliefRoughness = .14f,
            NormalMap = ResponseRoot + "civic_plaster_v1_normal.png", NormalScale = .28f,
            RoughnessMap = ResponseRoot + "civic_plaster_v1_roughness.png", RoughnessDelta = .16f,
            WearMap = ResponseRoot + "civic_repair_v1_mask.png", WearRoughness = -.06f,
            WearTint = "d9d6cb", WearTintBlend = .35f, TilesPerMetre = .45f,
            WearBottom = .22f, WearBottomHeight = .45f },
        ["stone"] = new() { Relief = .45f, ReliefFreq = new(6f, 6f), ReliefRoughness = .12f,
            WearBottom = .20f, WearBottomHeight = .22f },
        ["stone_foundation"] = new() { Relief = .48f, ReliefFreq = new(6f, 6f), ReliefRoughness = .12f,
            WearBottom = .30f, WearBottomHeight = .26f },
        // Metal is split by physical finish, not by specular (VIS-095).
        ["metal"] = new() { Relief = .20f, ReliefFreq = new(3.5f, 3.5f), ReliefRoughness = .06f,
            NormalMap = ResponseRoot + "metal_painted_v1_normal.png", NormalScale = .16f,
            WearMap = ResponseRoot + "metal_wear_v1_mask.png", WearRoughness = .30f, WearMetallic = .05f,
            WearTint = "6b4a35", WearTintBlend = .80f, TilesPerMetre = .7f,
            FrostRoughness = .10f },
        ["zinc_sheet"] = new() { Relief = .24f, ReliefFreq = new(1.6f, 1.6f), ReliefRoughness = .10f,
            NormalMap = ResponseRoot + "metal_galvanized_v1_normal.png", NormalScale = .30f,
            RoughnessMap = ResponseRoot + "metal_galvanized_v1_roughness.png", RoughnessDelta = .18f,
            WearMap = ResponseRoot + "metal_wear_v1_mask.png", WearRoughness = .26f, WearMetallic = .10f,
            WearTint = "6b4a35", WearTintBlend = .85f, TilesPerMetre = .55f,
            FrostRoughness = .14f },
        ["iron"] = new() { Relief = .30f, ReliefFreq = new(4f, 4f), ReliefRoughness = .14f,
            NormalMap = ResponseRoot + "metal_wrought_v1_normal.png", NormalScale = .26f,
            WearMap = ResponseRoot + "metal_wear_v1_mask.png", WearRoughness = .28f, WearMetallic = .10f,
            WearTint = "5a3a2b", WearTintBlend = .80f, TilesPerMetre = .7f,
            FrostRoughness = .08f },
        ["enamel"] = new() { Relief = .05f, ReliefFreq = new(9f, 9f), ReliefRoughness = .03f,
            WearMap = ResponseRoot + "enamel_chip_v1_mask.png", WearRoughness = .22f, WearMetallic = 0f,
            WearTint = "4c4a44", WearTintBlend = .90f, TilesPerMetre = 1.4f,
            WearBottom = .12f, WearBottomHeight = .06f },
        ["roof"] = new() { Relief = .28f, ReliefFreq = new(2.4f, 2.4f), ReliefRoughness = .10f,
            NormalMap = ResponseRoot + "roof_sheet_v1_normal.png", NormalScale = .30f,
            TilesPerMetre = .5f, FrostRoughness = .10f },
        ["roof_metal"] = new() { Relief = .24f, ReliefFreq = new(2.2f, 2.2f), ReliefRoughness = .08f,
            NormalMap = ResponseRoot + "roof_sheet_v1_normal.png", NormalScale = .26f,
            WearMap = ResponseRoot + "metal_wear_v1_mask.png", WearRoughness = .26f, WearMetallic = .08f,
            WearTint = "6b4a35", WearTintBlend = .75f, TilesPerMetre = .5f, FrostRoughness = .14f },
        ["log_wall"] = new() { Relief = .40f, ReliefFreq = new(2.2f, 18f), ReliefRoughness = .10f,
            WearBottom = .30f, WearBottomHeight = .35f },
        ["cloth"] = new() { Relief = .22f, ReliefFreq = new(16f, 16f), ReliefRoughness = .05f },
        ["fabric_upholstery"] = new() { Relief = .24f, ReliefFreq = new(15f, 15f), ReliefRoughness = .06f },
        ["fabric_pattern"] = new() { Relief = .20f, ReliefFreq = new(18f, 18f), ReliefRoughness = .05f },
        ["cloth_towel"] = new() { Relief = .30f, ReliefFreq = new(13f, 13f), ReliefRoughness = .06f },
        ["cloth_clinic"] = new() { Relief = .16f, ReliefFreq = new(18f, 18f), ReliefRoughness = .04f },
        ["carpet"] = new() { Relief = .32f, ReliefFreq = new(20f, 20f), ReliefRoughness = .06f },
        ["grass"] = new() { Relief = .30f, ReliefFreq = new(12f, 12f), ReliefRoughness = .06f },
        ["hay_fibers"] = new() { Relief = .35f, ReliefFreq = new(8f, 20f), ReliefRoughness = .08f },
        ["hay_bundle"] = new() { Relief = .35f, ReliefFreq = new(8f, 20f), ReliefRoughness = .08f },
        ["earth"] = new() { Relief = .22f, ReliefFreq = new(9f, 9f), ReliefRoughness = .10f },
        ["paper"] = new() { Relief = .10f, ReliefFreq = new(26f, 26f), ReliefRoughness = .08f },
        ["leather"] = new() { Relief = .34f, ReliefFreq = new(7f, 9f), ReliefRoughness = .12f },
        ["plastic_abs"] = new() { Relief = .06f, ReliefFreq = new(22f, 22f), ReliefRoughness = .05f },
        ["wallpaper"] = new() { Relief = .12f, ReliefFreq = new(10f, 10f), ReliefRoughness = .06f }
    };

    // VIS-094. A civic facade is identified by its function, so ФАП / ДК / школа stop
    // sharing one grey plane. Sheltered (clean, indoors, protected) facades never take
    // the soiling line: base_grime stays 0 there.
    private sealed record CivicFinish
    {
        public Vector2 MacroScale { get; init; } = new(.33f, .11f);
        public float MacroGain { get; init; } = 1f;
        public float Grime { get; init; }
        public float GrimeHeight { get; init; } = .9f;
        public string GrimeTint { get; init; } = "6b6659";
    }

    private static readonly Dictionary<string, CivicFinish> CivicFinishes = new(StringComparer.Ordinal)
    {
        // ФАП: a working, washed institution. Faint, low, cool soiling only.
        ["clinic"] = new() { MacroScale = new(.42f, .14f), MacroGain = 1.15f, Grime = .075f,
            GrimeHeight = .55f, GrimeTint = "6d7272" },
        // ДК: the oldest public paint, largest patched field, warm repair tone.
        ["club"] = new() { MacroScale = new(.20f, .07f), MacroGain = 1.45f, Grime = .155f,
            GrimeHeight = 1.05f, GrimeTint = "6b6153" },
        // Школа: daily traffic, mid wavelength, dirt at the plinth line.
        ["school"] = new() { MacroScale = new(.29f, .10f), MacroGain = 1.30f, Grime = .115f,
            GrimeHeight = .80f, GrimeTint = "666459" },
        // Сельсовет/контора: formal, patched, but cleaner than the club.
        ["admin"] = new() { MacroScale = new(.24f, .09f), MacroGain = 1.20f, Grime = .095f,
            GrimeHeight = .70f, GrimeTint = "6a6659" },
        // Мечеть: maintained, no decorative decay; the difference is wavelength only.
        ["sacred"] = new() { MacroScale = new(.26f, .09f), MacroGain = 1.10f, Grime = .035f,
            GrimeHeight = .45f, GrimeTint = "6f6b60" }
    };

    // VIS-038. The authored pigment a shared family material carries. A run that
    // needs another tint keeps this material and rides the instance path instead of
    // allocating one material state per colour.
    private static readonly Dictionary<string, string> FamilyTintBase = new(StringComparer.Ordinal)
    {
        ["wood_fence"] = "92816b",
        ["wood_fence_uv"] = "979a92",
        ["wood_fence_vertical"] = "979a92",
        ["wood_fence_rail"] = "979a92",
        ["wood_facade"] = "6f6353",
        ["plaster"] = "92958a",
        ["wall_institution"] = "aabdad"
    };

    private static readonly Dictionary<string, ShaderMaterial> Materials = new(StringComparer.OrdinalIgnoreCase);


    private static bool _lowQualityMaterials;
    private static bool _windMotion = true;
    // W4/P2: last value broadcast to the cached materials. Nullable so the first
    // SetWindMotion always broadcasts; _windMotion itself stays the value that
    // ForColor stamps into newly created materials (line 893).
    private static bool? _windMotionBroadcast;
    private static Texture2D? _boundTrampleMask;
    private static Vector2 _boundTrampleOrigin;
    private static float _boundTrampleExtent;
    private static int _boundTrampleMaterialCount = -1;
    // VIS-067/068 per-atmosphere snow response. Each cached material carries a
    // family-specific snow_coverage/snow_sparkle stamped by ForColor; the profile
    // colour script must modulate those authored values, never flatten them
    // (W3: flat snow is an anti-example). The base is captured the first time the
    // material is seen, so re-applying a profile recomputes from source instead of
    // compounding. Keyed weakly by the material instance the cache already owns.
    // VIS-081/095 adds the frost term: hoarfrost is a property of the light state,
    // so the atmosphere profile scales each family's own authored dulling rather
    // than writing one global number over every metal.
    private static readonly System.Collections.Generic.Dictionary<ShaderMaterial, (float Coverage, float Sparkle, float Frost)> _snowBase = [];
    private static Color? _snowMoodColor;
    private static float _snowMoodCoverage = 1f;
    private static float _snowMoodSparkle = 1f;
    private static float _snowMoodFrost = 1f;
    private static int _snowMoodMaterialCount = -1;

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
        // Updating pixels in the same ImageTexture does not change its binding.
        // Newly cached materials still require the normal binding pass.
        if (Materials.Count == _boundTrampleMaterialCount
            && ReferenceEquals(mask, _boundTrampleMask)
            && origin == _boundTrampleOrigin && extent == _boundTrampleExtent) return;
        foreach (var material in Materials.Values)
        {
            // Other surfaces never sample the mask; rebinding them stalls every footstep.
            if (!material.GetShaderParameter("trample_ground_surface").AsBool()) continue;
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
        _boundTrampleMask = mask;
        _boundTrampleOrigin = origin;
        _boundTrampleExtent = extent;
        _boundTrampleMaterialCount = Materials.Count;
    }

    public static void SetWindMotion(bool enabled)
    {
        // W4/P2: FirstPersonController pushes the setting whenever preferences
        // change, not only when this switch flips. Re-sending one identical
        // boolean to all cached materials is a no-op, so only the first call and
        // real transitions walk the cache. _windMotion equals
        // _windMotionBroadcast after that first call, so new materials still
        // pick up the current value in ForColor.
        if (_windMotionBroadcast == enabled) return;
        _windMotion = enabled;
        foreach (var material in Materials.Values)
        {
            material.SetShaderParameter("wind_enabled", enabled);
        }
        _windMotionBroadcast = enabled;
    }

    /// <summary>
    /// VIS-067/068/070: the snow/ground colour response of an atmosphere profile.
    /// STYLE RECIPE W1 lets the snow read blue, pinkish, peach or lilac with the
    /// light state, but it must stay believable snow — never flat white, never a
    /// pure overexposed white (W3, design_style §снег). The authored <paramref
    /// name="tint"/> is blended toward the shader's own neutral snow albedo by
    /// <paramref name="tintStrength"/> (0 keeps the pre-colour-script look exactly),
    /// while <paramref name="coverageScale"/> and <paramref name="sparkleScale"/>
    /// modulate each family's authored coverage/sparkle rather than overriding them,
    /// so roof, fence and trodden-path structure survives. Presentation only, fully
    /// additive: no new geometry, texture, light owner or post-process.
    /// VIS-081/095 adds <paramref name="frostScale"/> (optional, default 1 = today):
    /// hoarfrost belongs to the light state, so a profile dims or lifts each metal's
    /// own authored frost dulling instead of flattening every family to one number.
    /// The sparkle scale now reaches above 1 (the frost-morning profile authors 1.2);
    /// clamping it to 1 silently discarded that authored value.
    /// </summary>
    public static void SetSnowMood(Color tint, float coverageScale, float sparkleScale, float tintStrength,
        float frostScale = 1f)
    {
        // The neutral baseline lives in the shader (vec4 0.93,0.95,0.97). Repeated
        // zone crossings re-derive the effective colour from source instead of
        // stacking tints, so an authored state can be left as cleanly as it entered.
        var strength = (float)System.Math.Clamp(tintStrength, 0f, 1f);
        var effective = AtmosphereProfile.NeutralSnowColor.Lerp(tint, strength);
        var coverage = (float)System.Math.Clamp(coverageScale, 0f, 1f);
        var sparkle = (float)System.Math.Clamp(sparkleScale, 0f, 2f);
        var frost = (float)System.Math.Clamp(frostScale, 0f, 2f);
        // Skip the full cache walk only when nothing changed and no material was
        // added since the last broadcast (the same guard SetSnowTrample uses).
        if (_snowMoodMaterialCount == Materials.Count
            && _snowMoodColor.HasValue && _snowMoodColor.Value == effective
            && _snowMoodCoverage == coverage && _snowMoodSparkle == sparkle
            && _snowMoodFrost == frost) return;
        foreach (var material in Materials.Values)
        {
            if (!_snowBase.TryGetValue(material, out var base_))
            {
                base_ = (material.GetShaderParameter("snow_coverage").AsSingle(),
                         material.GetShaderParameter("snow_sparkle").AsSingle(),
                         material.GetShaderParameter("frost_roughness").AsSingle());
                _snowBase[material] = base_;
            }
            material.SetShaderParameter("snow_color", effective);
            // A sheltered surface baked at coverage 0 stays snowless; multiplying its
            // own base preserves the family distinction the shader already encodes.
            material.SetShaderParameter("snow_coverage", base_.Coverage * coverage);
            material.SetShaderParameter("snow_sparkle", base_.Sparkle * sparkle);
            material.SetShaderParameter("frost_roughness", base_.Frost * frost);
        }
        _snowMoodColor = effective;
        _snowMoodCoverage = coverage;
        _snowMoodSparkle = sparkle;
        _snowMoodFrost = frost;
        _snowMoodMaterialCount = Materials.Count;
    }

    /// <summary>
    /// Collision-only headless tests can skip image-backed surface textures;
    /// visual scene and capture paths leave this disabled.
    /// </summary>
    public static bool SuppressTextureLoadsForHeadlessTests { get; set; }

    /// <summary>
    /// Applies the explicit graphics profile to cached presentation materials.
    /// Low mode replaces triplanar blending with one albedo read while
    /// preserving scene geometry, interactions and gameplay owners.
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
        _boundTrampleMask = null;
        _boundTrampleMaterialCount = -1;
        // The snow-response memo walks the same cache: with the materials gone,
        // the captured per-family base and the last broadcast have to go too, so
        // the next atmosphere profile re-derives from the recreated cache.
        _snowBase.Clear();
        _snowMoodColor = null;
        _snowMoodCoverage = 1f;
        _snowMoodSparkle = 1f;
        _snowMoodFrost = 1f;
        _snowMoodMaterialCount = -1;
        // W4/P2: the cache is gone, so the wind memo is dropped too; the next
        // SetWindMotion re-broadcasts into the recreated cache while new
        // materials already inherit _windMotion from ForColor.
        _windMotionBroadcast = null;
        // ShaderMaterial/ImageTexture wrappers are managed Godot objects. A
        // headless smoke process can otherwise reach native shutdown before
        // the wrappers' finalizers release their renderer RIDs. This method is
        // test-only; production gameplay never forces a collection.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static Material ForCutout(string htmlColor, Texture2D alphaTexture, string surface)
    {
        var cacheKey = $"cutout:{surface}:{htmlColor}:{alphaTexture.GetInstanceId()}";
        if (Materials.TryGetValue(cacheKey, out var existing)) return existing;
        var material = (ShaderMaterial)ForColor(htmlColor, surface).Duplicate();
        material.Shader = CutoutShader;
        material.SetShaderParameter("cutout_texture", alphaTexture);
        Materials[cacheKey] = material;
        return material;
    }

    /// <summary>
    /// Diagnostics only: reports whether this material reaches the renderer's shared
    /// shadow material. The engine requires an opaque back-culled material that does
    /// not write VERTEX, uses no alpha clip/discard and no world_vertex_coords
    /// (SceneShaderForwardClustered::ShaderData::uses_shared_shadow_material). The
    /// rigid variant is exactly that case here: it is the only shader built from
    /// ShaderSource with the vertex deformation removed, while the cutout and
    /// two-sided variants use their own shaders. Reading the shader identity never
    /// changes which material a mesh uses.
    /// </summary>
    public static bool UsesSharedShadowMaterial(Material? material) =>
        material is ShaderMaterial { Shader: { } shader } && ReferenceEquals(shader, RigidPainterlyShader);

    public static Material PreserveSourceCulling(Material painted, Material? source)
    {
        if (source is not BaseMaterial3D { CullMode: BaseMaterial3D.CullModeEnum.Disabled }
            || painted is not ShaderMaterial shader
            || (shader.Shader != PainterlyShader && shader.Shader != RigidPainterlyShader))
            return painted;

        // The shared opaque material remains unchanged for solid geometry.
        // One variant per cached painterly material also receives the existing
        // graphics, snow-trample and motion updates through Materials.Values.
        var key = $"source-cull-disabled:{painted.GetInstanceId()}";
        if (Materials.TryGetValue(key, out var existing)) return existing;
        var material = (ShaderMaterial)shader.Duplicate();
        material.Shader = TwoSidedPainterlyShader;
        material.SetMeta("sourceCullingPreserved", true);
        Materials.Add(key, material);
        return material;
    }

    public static ShaderMaterial ForMovingCloth(string htmlColor)
    {
        var key = $"moving-cloth:{htmlColor}";
        if (Materials.TryGetValue(key, out var existing)) return existing;
        var material = (ShaderMaterial)ForColor(htmlColor, "cloth", sheltered: true).Duplicate();
        material.SetShaderParameter("authored_uv_texture", true);
        material.SetShaderParameter("bound_uv_pigment", true);
        material.SetShaderParameter("ground_darken", 0f);
        // VIS-007 §5.8: cell_tint is quantised on a 6 m world grid; on a walking
        // body it would step through tints. Deforming cloth never takes it.
        material.SetShaderParameter("cell_jitter", 0f);
        Materials[key] = material;
        return material;
    }

    /// <summary>
    /// VIS-033 pilot: a static upright whose foot darkens over the first
    /// <paramref name="contactHeight"/> metres above its own support. Pair it with
    /// <see cref="SetGroundContact"/> on each instance; the material stays shared.
    /// </summary>
    public static ShaderMaterial ForGroundContact(string htmlColor, string surface, float darken = .22f, float contactHeight = .18f)
    {
        var key = FormattableString.Invariant($"ground-contact:{surface}:{htmlColor}:{darken:R}:{contactHeight:R}");
        if (Materials.TryGetValue(key, out var existing)) return existing;
        var material = (ShaderMaterial)ForColor(htmlColor, surface).Duplicate();
        material.SetShaderParameter("ground_darken", darken);
        material.SetShaderParameter("ground_contact_height", contactHeight);
        Materials[key] = material;
        return material;
    }

    /// <summary>World height of the surface this instance stands on (per-instance, no material copy).</summary>
    public static void SetGroundContact(GeometryInstance3D instance, float supportWorldY) =>
        instance.SetInstanceShaderParameter("ground_base_y", supportWorldY);

    public static ShaderMaterial ForLocalWoodPiece(string htmlColor, Vector3 pieceOffset)
    {
        // Each board piece shares the same object-space grain but has its own
        // offset. Keep the variants in the normal cache so live graphics and
        // motion settings reach them as well as the base wood material.
        var cacheKey = FormattableString.Invariant(
            $"local-wood:{htmlColor}:{pieceOffset.X:R}:{pieceOffset.Y:R}:{pieceOffset.Z:R}");
        if (Materials.TryGetValue(cacheKey, out var existing)) return existing;

        var material = (ShaderMaterial)ForColor(htmlColor, "wood_prop", sheltered: true).Duplicate();
        material.SetShaderParameter("local_wood_texture", true);
        material.SetShaderParameter("local_wood_offset", pieceOffset);
        material.SetShaderParameter("texture_scale", new Vector2(.55f, .30f));
        material.SetShaderParameter("snow_coverage", .14f);
        // VIS-007 §5.4: a carried board must not change tint when it crosses a
        // 6 m world cell (mode E: movable rigid-local piece).
        material.SetShaderParameter("cell_jitter", 0f);
        Materials[cacheKey] = material;
        return material;
    }

    /// <summary>
    /// VIS-080: how sheltered this one mesh is from settling snow. 0 (the shader and
    /// material default, so every existing caller is unchanged) means fully exposed;
    /// 1 removes the snow cap without touching geometry and without a copied material.
    /// Owners under canopies, verandas, porches and inside rooms pass 1.
    /// </summary>
    public static void SetSnowShelter(GeometryInstance3D instance, float shelter) =>
        instance.SetInstanceShaderParameter("snow_shelter",
            (float)System.Math.Clamp(shelter, 0f, 1f));

    /// <summary>
    /// VIS-038: the pigment difference between visually identical members of one
    /// family rides the instance path, so a fence of several tints is several
    /// instances of one material state instead of one material state per tint.
    /// The ratio is computed in linear light because <c>base_color</c> is a
    /// <c>source_color</c> uniform. Returns false, leaving the caller on the exact
    /// <see cref="ForColor"/> path, when the target is too far from the family base
    /// for a multiplier to reproduce it (near-black channels).
    /// </summary>
    public static bool SetInstanceTint(GeometryInstance3D instance, string surface, string targetHtmlColor)
    {
        if (!FamilyTintBase.TryGetValue(surface, out var baseHex)) return false;
        var from = Color.FromHtml(baseHex);
        var to = Color.FromHtml(targetHtmlColor);
        var mul = new Vector3(
            LinearRatio(to.R, from.R),
            LinearRatio(to.G, from.G),
            LinearRatio(to.B, from.B));
        // A multiplier that has to saturate is a different pigment, not a variant of
        // this family, so the caller keeps the exact per-colour material.
        if (mul.X >= SaturationRatio || mul.Y >= SaturationRatio || mul.Z >= SaturationRatio
            || mul.X <= .025f || mul.Y <= .025f || mul.Z <= .025f) return false;
        instance.SetInstanceShaderParameter("instance_pigment_mul", mul);
        return true;
    }

    private const float SaturationRatio = 4f;

    private static float LinearRatio(float target, float baseValue)
    {
        var t = ToLinear(target);
        var b = ToLinear(baseValue);
        if (b < .0001f) return t < .0001f ? 1f : SaturationRatio;
        return (float)System.Math.Clamp(t / b, .02f, SaturationRatio);
    }

    /// <summary>sRGB component (0..1 from Color.FromHtml) to linear, matching the
    /// conversion the engine applies to a source_color uniform.</summary>
    private static float ToLinear(float srgb) =>
        srgb <= .04045f ? srgb / 12.92f : (float)System.Math.Pow((srgb + .055f) / 1.055f, 2.4);

    /// <summary>
    /// VIS-038/093: the shared material of a family, authored on that family's base
    /// pigment. Pair it with <see cref="SetInstanceTint"/>; members that need an
    /// exact colour outside the multiplier range keep using <see cref="ForColor"/>.
    /// </summary>
    public static ShaderMaterial ForSharedTint(string surface, bool sheltered = false)
    {
        var baseHex = FamilyTintBase.TryGetValue(surface, out var authored) ? authored : "8b8577";
        return (ShaderMaterial)ForColor(baseHex, surface, sheltered);
    }

    /// <summary>
    /// VIS-094: a public facade identified by its function. The roles change the
    /// wavelength of the authored macro breakup (kept inside the 2..8 m band), the
    /// density of the base soiling and, where delivered, a rare repair-patch mask.
    /// Nothing here touches signage materials, and a sheltered (interior) call never
    /// takes the soiling, so a working clinic stays clean and a wall is not moulded.
    /// </summary>
    public static Material ForCivicSurface(string htmlColor, string role, bool sheltered = false)
    {
        var surface = role switch
        {
            "clinic" or "club" or "school" or "admin" or "sacred" => "wall_institution",
            _ => "plaster"
        };
        var key = $"civic:{surface}:{htmlColor}:{(sheltered ? "sheltered" : "exposed")}:{role}";
        if (Materials.TryGetValue(key, out var cached)) return cached;
        var material = (ShaderMaterial)ForColor(htmlColor, surface, sheltered).Duplicate();
        material.SetMeta("surface", surface);
        material.SetMeta("civicRole", role);
        var finish = CivicFinishes.TryGetValue(role, out var authored) ? authored : new CivicFinish();
        material.SetShaderParameter("macro_scale", finish.MacroScale);
        material.SetShaderParameter("macro_gain", finish.MacroGain);
        // Interior/protected civic surfaces keep the painted plane but never the
        // ground-line soiling: the physical cause (splash, meltwater, dust) is absent.
        material.SetShaderParameter("base_grime", sheltered ? 0f : finish.Grime);
        material.SetShaderParameter("base_grime_height", finish.GrimeHeight);
        material.SetShaderParameter("base_grime_color", Color.FromHtml(finish.GrimeTint));
        // The difference stays visible in the material, not in a decal splat: gain is
        // capped by the shader at 0.45 so no facade can be re-read as abandoned.
        Materials[key] = material;
        return material;
    }

    /// <summary>VIS-038: the receipt a material-count check reads. Only ever grows with
    /// genuinely distinct parameter sets; see <see cref="DescribeCachedMaterials"/>.</summary>
    public static int CachedMaterialCount => Materials.Count;

    /// <summary>VIS-038: cache introspection for the render diagnostics receipt. Reads
    /// parameters only; it never changes which material a mesh uses.</summary>
    public static global::Godot.Collections.Array DescribeCachedMaterials()
    {
        var rows = new global::Godot.Collections.Array();
        foreach (var (key, material) in Materials)
        {
            rows.Add(new global::Godot.Collections.Dictionary
            {
                ["key"] = key,
                ["surface"] = material.GetMeta("surface", "").AsString(),
                ["shader"] = ReferenceEquals(material.Shader, RigidPainterlyShader) ? "rigid"
                    : ReferenceEquals(material.Shader, CutoutShader) ? "cutout"
                    : ReferenceEquals(material.Shader, TwoSidedPainterlyShader) ? "two-sided"
                    : "deforming",
                ["hasAlbedo"] = material.GetShaderParameter("has_albedo_texture").AsBool(),
                ["hasDetailNormal"] = material.GetShaderParameter("has_detail_normal").AsBool(),
                ["hasDetailRoughness"] = material.GetShaderParameter("has_detail_roughness").AsBool(),
                ["hasWearMask"] = material.GetShaderParameter("has_wear_mask").AsBool(),
                ["metallic"] = material.GetShaderParameter("metallic_value").AsSingle(),
                ["roughness"] = material.GetShaderParameter("roughness_value").AsSingle(),
                ["snowCoverage"] = material.GetShaderParameter("snow_coverage").AsSingle(),
                ["baseColor"] = material.GetShaderParameter("base_color").AsColor().ToHtml(false)
            });
        }
        return rows;
    }

    /// <summary>Preserve the source finish and render state, disabling only
    /// snow. Shared variants receive the normal graphics and motion updates.</summary>
    public static ShaderMaterial WithoutSnow(ShaderMaterial source)
    {
        var key = $"without-snow:{source.GetInstanceId()}";
        if (Materials.TryGetValue(key, out var existing)) return existing;
        var material = (ShaderMaterial)source.Duplicate();
        material.SetShaderParameter("snow_coverage", 0f);
        material.SetShaderParameter("snow_sparkle", 0f);
        material.SetShaderParameter("has_snow_micro", false);
        Materials[key] = material;
        return material;
    }

    /// <summary>
    /// The same painterly finish without weather: no settled snow on top faces.
    /// Returns the material itself when it is not a cached painterly one.
    /// </summary>
    public static Material Sheltered(Material material)
    {
        foreach (var (key, value) in Materials)
        {
            if (!ReferenceEquals(value, material) || !key.EndsWith(":exposed", StringComparison.Ordinal)) continue;
            var parts = key.Split(':');
            return ForColor(parts[1], parts[0], sheltered: true);
        }
        return material;
    }

    /// <summary>A trodden snow path: the trampled-snow material with a soft, ragged edge into
    /// fresh snow (needs ribbon UVs, x across 0..1). Packed snow is only a shade darker and
    /// bluer than fresh, never a grey strip, so the caller's tone is pulled toward it.</summary>
    public static Material ForPath(string htmlColor)
    {
        var cacheKey = $"path:{htmlColor}";
        if (Materials.TryGetValue(cacheKey, out var existing)) return existing;
        var tone = Color.FromHtml(htmlColor).Lerp(new Color(.85f, .88f, .92f), .35f);
        var material = (ShaderMaterial)ForColor(tone.ToHtml(false), "snow_trampled").Duplicate();
        material.SetMeta("surface", "snow_trampled");
        material.SetShaderParameter("soft_path_edges", true);
        Materials.Add(cacheKey, material);
        return material;
    }

    // VIS-007 step 3: every surface name is an explicit mode choice. Families with
    // a map are listed in SurfaceTextures; these are tuned without a map, and these
    // are semantic labels that deliberately take the flat painterly default (other
    // owners such as the police post remap them). Anything else is a typo or a new
    // branch that skipped the choice: it still renders, but loudly.
    private static readonly HashSet<string> TunedWithoutMap = new(StringComparer.Ordinal)
    {
        "bark_birch", "enamel", "grass", "grass_tuft", "iron", "leaf_birch", "ornament_trim",
        "roof", "roof_metal", "water", "wood_carved", "wood_cut",
        // VIS-105 (transport): the vehicle finishes are their own modes, so a
        // car never inherits the village's matte `metal`/`rubber` default and
        // never falls back to an untuned grey. Each one is a response contract,
        // not a colour: paint carries no metal (VIS-095), the two steels differ
        // by roughness and highlight shape, the tyre stays matte and dark, the
        // bumper plastic sits between them. Maps are the M07/M08 lane's job.
        "vehicle_paint", "vehicle_bare_metal", "vehicle_trim_metal", "vehicle_rubber",
        "vehicle_plastic",
        // VIS-092/095: `metal` used to be the most-called iron-family name with no
        // branch at all, so the village's steel answered light like masonry. It now
        // has a tuned response without an albedo map (the map stays an M01 request).
        // `zinc_sheet` is the new explicit exposed-galvanised mode; `paper` and
        // `leather` gained their own roughness/fibre response for the same reason.
        "metal", "zinc_sheet", "paper", "leather"
    };
    private static readonly HashSet<string> FlatByDesign = new(StringComparer.Ordinal)
    {
        "ceramic", "glass", "painted", "rubber"
    };
    private static readonly HashSet<string> ReportedUnknownSurfaces = new(StringComparer.Ordinal);

    public static Material ForColor(string htmlColor, string surface = "", bool sheltered = false)
    {
        // VIS-038: "fabric" and "cloth" wrote byte-identical parameters (same map,
        // scale, brush, finish and snow); one cache entry serves both names.
        if (surface == "fabric") surface = "cloth";
        var cacheKey = $"{surface}:{htmlColor}:{(sheltered ? "sheltered" : "exposed")}";
        if (Materials.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        if (surface.Length > 0
            && !SurfaceTextures.ContainsKey(surface)
            && !TunedWithoutMap.Contains(surface)
            && !FlatByDesign.Contains(surface)
            && ReportedUnknownSurfaces.Add(surface))
        {
            global::Godot.GD.PushWarning(
                $"PainterlyMaterialLibrary: surface '{surface}' has no declared mode; it gets the flat world-space default.");
        }

        var color = Color.FromHtml(htmlColor);
        // New maps keep the established wood responses; texture/projection and
        // cache identity remain specific to each actual surface.
        var finishSurface = surface switch
        {
            "wood_painted_blue" or "wood_painted_green" or "wood_painted_trim" or "wood_log_uv" => "wood_facade",
            "wood_floor_painted" => "wood_furniture_interior",
            "wood_fence_vertical" or "wood_fence_rail" or "wood_fence_uv" => "wood_fence",
            "plaster_domestic" => "wall_institution",
            "stone_foundation" => "stone",
            "cloth_table" or "cloth_curtain" or "cloth_towel" => "cloth",
            _ => surface
        };
        var shadow = new Color(color.R * 0.54f, color.G * 0.56f, color.B * 0.58f, color.A);
        var material = new ShaderMaterial { Shader = PainterlyShader };
        material.SetMeta("surface", surface);
        material.SetShaderParameter("base_color", color);
        // VIS-038/080 identity stamps for the two instance uniforms. The material
        // carries the neutral value, so a mesh that never sets an instance parameter
        // renders exactly as it did, and one that does overrides only itself. Stamping
        // the material too keeps the result identical whichever way the engine resolves
        // an unset instance uniform (declared default or material fallback).
        material.SetShaderParameter("instance_pigment_mul", new Vector3(1f, 1f, 1f));
        material.SetShaderParameter("snow_shelter", 0f);
        material.SetShaderParameter("edge_frost", surface == "frost_window");
        material.SetShaderParameter("cut_wood_end", surface == "wood_cut");
        material.SetShaderParameter("upright_texture", surface is "log_wall" or "fabric_pattern" or "hay_bundle"
            or "wood_facade" or "wood_painted_blue" or "wood_painted_green" or "wood_painted_trim" or "wood_floor_painted"
            or "bark_birch_winter" or "bark_pine" or "wallpaper" or "wood_fence_vertical" or "wood_fence_rail");
        material.SetShaderParameter("local_wood_texture", surface == "hay_bundle");
        material.SetShaderParameter("local_floor_texture", surface is "wood_floor_painted" or "wood_fence_vertical" or "wood_fence_rail");
        material.SetShaderParameter("local_fence_rail", surface == "wood_fence_rail");
        material.SetShaderParameter("authored_uv_texture", surface is "hay_fibers" or "cloth_table" or "cloth_curtain" or "wood_fence_uv" or "wood_log_uv");
        // VIS-095 through VIS-105: metal only where the object is actually metal.
        // Body paint keeps metallic 0 and reads by highlight shape and roughness;
        // the two vehicle steels differ by age, not by a chrome trick.
        // VIS-095 village metals: `metal` is the ambiguous name in this project (a
        // bathhouse roof, a bucket, a sign backing and an enamel basin all use it), so
        // it stays painted sheet at metallic 0 — the honest default for anything whose
        // finish is unknown. Owners that are provably exposed zinc adopt `zinc_sheet`.
        // Wrought iron is metal everywhere it is used, so it carries metallic and pays
        // for it with a rough, aged highlight rather than a shine.
        material.SetShaderParameter("metallic_value", surface switch
        {
            "iron" => 0.78f,
            "zinc_sheet" => 0.90f,
            "vehicle_bare_metal" => 0.85f,
            "vehicle_trim_metal" => 1.0f,
            _ => 0f
        });
        material.SetShaderParameter("finish_grain", surface switch
        {
            "iron" => 0.10f,
            "enamel" => 0.035f,
            // Lacquer orange-peel and panel age, rolled-steel brushing on the
            // exposed parts. Below one pixel at a few metres, so the distance
            // read stays a clean colour mass.
            "vehicle_paint" => 0.05f,
            "vehicle_bare_metal" => 0.12f,
            "vehicle_trim_metal" => 0.04f,
            "vehicle_rubber" => 0.07f,
            "vehicle_plastic" => 0.05f,
            _ => 0f
        });
        material.SetShaderParameter("vertex_pigment", surface is "terrain" or "wet_road" or "snow_road"
            // The Niva generator bakes road grime into the panel vertex colours;
            // the family that owns those meshes keeps them (VehicleVisualFactory).
            || surface == "vehicle_paint");
        material.SetShaderParameter("shadow_color", shadow);
        material.SetMeta("sheltered", sheltered);
        // Keep the brush rhythm stable for a semantic surface. The old cache
        // count made the same material change appearance with call order and
        // amplified broad world-space banding on long walls and roads.
        material.SetShaderParameter("brush_scale", finishSurface switch
        {
            "earth" => 0.34f,
            "wet_ground" => 0.30f,
            "wood" or "wood_facade" or "wood_fence" => 0.32f,
            "wood_furniture" or "wood_furniture_interior" or "wood_prop" => 0.28f,
            "wood_bark" => 0.25f,
            "plaster" => 0.26f,
            "floor_institution" => 0.30f,
            "foliage" => 0.29f,
            "grass" => 0.38f,
            "roof" or "roof_metal" => 0.24f,
            "bark_birch" or "bark_pine" => 0.25f,
            "leaf_birch" => 0.30f,
            "log_wall" or "wallpaper" or "wall_institution" => 0.22f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.18f,
            "stone" => 0.27f,
            "fabric" or "fabric_upholstery" or "cloth" or "cloth_clinic" or "cloth_towel" => 0.24f,
            "iron" or "enamel" or "plastic_abs" or "zinc_sheet" => 0.18f,
            "water" => 0.18f,
            // VIS-105: a car body is a metre-scale object. The village's brush
            // period would put several strokes across one door, so the transport
            // families take the tightest rhythm in the library and stay a single
            // colour mass at a distance while still answering the light.
            "vehicle_paint" or "vehicle_plastic" or "vehicle_trim_metal" => 0.14f,
            "vehicle_bare_metal" or "vehicle_rubber" => 0.16f,
            _ => 0.30f
        });
        material.SetShaderParameter("variation", finishSurface switch
        {
            "earth" => 0.14f,
            "wet_ground" => 0.11f,
            "wood" or "wood_facade" or "wood_fence" => 0.14f,
            "wood_furniture" or "wood_furniture_interior" or "wood_prop" => 0.11f,
            "wood_bark" => 0.13f,
            "plaster" => 0.10f,
            "foliage" => 0.15f,
            "grass" or "leaf_birch" => 0.14f,
            "roof" or "roof_metal" or "bark_birch" or "bark_pine" => 0.11f,
            "log_wall" or "wall_institution" => 0.08f,
            // VIS-035: the hero-room pair. Wallpaper carried more ornamental
            // contrast than the table and the seated face next to it, so only the
            // conflicting plane is quieted (-12.5% macro contrast, its own repeat
            // doubled) while the pattern itself and NPC cloth stay untouched.
            "wallpaper" => 0.07f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.06f,
            "stone" => 0.12f,
            "fabric" or "cloth" => 0.08f,
            // VIS-035, same reasoning as wallpaper: the interior textile planes that
            // compete with furniture and faces lose a little contrast. NPC cloth keeps
            // its authored value (the cloth_table/cloth_curtain alias is shared with
            // character clothing and is deliberately not split here).
            "fabric_upholstery" or "cloth_clinic" or "cloth_towel" => 0.07f,
            "hay_fibers" or "hay_bundle" => 0.06f,
            "iron" or "enamel" or "plastic_abs" => 0.04f,
            "water" => 0.06f,
            // Transport keeps the painting quiet: the panel grime arrives from the
            // authored vertex colours, not from a second decorative layer.
            "vehicle_paint" or "vehicle_bare_metal" => 0.05f,
            "vehicle_rubber" or "vehicle_plastic" or "vehicle_trim_metal" => 0.04f,
            _ => 0.10f
        });
        material.SetShaderParameter("texture_strength", finishSurface switch
        {
            "hay_fibers" or "hay_bundle" => 1.0f,
            "foliage" => 0.95f,
            "grass" or "leaf_birch" => 0.95f,
            "roof" or "roof_metal" => 0.94f,
            "bark_birch" or "bark_pine" => 0.94f,
            "log_wall" or "wall_institution" => 0.92f,
            "wallpaper" => 0.90f,
            "ornament_trim" or "carpet" or "fabric_pattern" or "wood_carved" => 0.88f,
            "earth" => 0.95f,
            "terrain" => 0.95f,
            "wet_ground" => 0.92f,
            "wet_road" => 0.95f,
            "wood" => 0.95f,
            "wood_facade" => 0.95f,
            "wood_fence" => 0.95f,
            "wood_furniture" or "wood_furniture_interior" => 0.92f,
            "wood_prop" => 0.92f,
            "wood_bark" => 0.95f,
            "plaster" => 0.95f,
            "stone" => 0.95f,
            "fabric" or "fabric_upholstery" => 0.88f,
            "cloth" or "cloth_clinic" => 0.88f,
            "water" => 0.0f,
            _ => 0.90f
        });
        // World Y is not height above support: lowered rooms and downhill
        // facades otherwise receive arbitrary dirt. Existing shadows/SSAO own contact.
        material.SetShaderParameter("ground_darken", 0f);
        var snowSparkle = finishSurface switch
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
        };
        material.SetShaderParameter("snow_sparkle", sheltered ? 0f : snowSparkle);
        // VIS-081: a real crystal flash is a glint, not a coat of glitter. Only the
        // snow families and the caps that already carry sparkle take the view term;
        // everything else keeps the previous, angle-independent image.
        material.SetShaderParameter("sparkle_grazing_only",
            snowSparkle > 0f || surface is "snow_ground" or "snow_road" or "snow_trampled" or "snow_grass" or "snow_roof" or "ice");
        var snowCoverage = finishSurface switch
        {
            // Full-snow families own their own albedo, no blanket needed.
            "snow_ground" or "snow_road" or "snow_trampled" or "snow_grass" or "snow_roof" or "ice" => 0.0f,
            "roof" or "roof_metal" => 0.88f,
            "wood_fence" => 0.62f,
            "wood_prop" or "wood_carved" => 0.55f,
            "wood" or "wood_facade" => 0.45f,
            // VIS-080: trim, boards and window surrounds are the families that leak
            // indoors. FacilitySolid/AddVisualBox never pass sheltered, so the mosque
            // ceiling, vestibule ceiling and interior window piers built from
            // wood_painted_trim took the roof blanket. The family now caps only where
            // it is genuinely near-horizontal and half as thick as before; a canopy
            // post is still outdoors and still reads snowed.
            "wood_painted_trim" => 0.28f,
            "stone" => 0.50f,
            "plaster" or "wall_institution" => 0.34f,
            "bark_birch" or "bark_birch_winter" or "bark_pine" => 0.30f,
            "foliage" or "leaf_birch" or "rowan_berries" => 0.34f,
            "grass" or "grass_tuft" => 0.72f,
            "fabric" or "fabric_upholstery" or "cloth" or "cloth_clinic" or "fabric_pattern" or "cloth_towel" => 0.30f,
            _ => 0.0f
        };
        material.SetShaderParameter("snow_coverage", sheltered ? 0f : snowCoverage);
        // VIS-080: the settle band is the shape half of the snow contract. A pitched
        // sheet and a rendered wall only catch a cap where the surface is close to
        // horizontal, so the blanket follows the construction instead of painting a
        // uniform white lid over every angle (карточка: «не класть снег на вертикальные
        // и защищённые поверхности одинаково»). The default band stays 0.30..0.72, so a
        // family not listed here is unchanged.
        material.SetShaderParameter("snow_settle_band", finishSurface switch
        {
            "roof" or "roof_metal" => new Vector2(0.62f, 0.86f),
            "plaster" or "wall_institution" => new Vector2(0.74f, 0.92f),
            "stone" or "stone_foundation" => new Vector2(0.58f, 0.84f),
            "wood_painted_trim" => new Vector2(0.66f, 0.90f),
            "wood_facade" or "wood" => new Vector2(0.52f, 0.80f),
            "log_wall" => new Vector2(0.58f, 0.86f),
            _ => new Vector2(0.30f, 0.72f)
        });
        // VIS-080: only the zyx local frame keeps its own vertical after the
        // permutation, so a bundle or a carried board caps along its own top face.
        // The xzy rail permutation moves length into Y and stays on the world band.
        material.SetShaderParameter("snow_follows_local_normal", surface is "hay_bundle" or "hay_fibers");
        material.SetShaderParameter("cell_jitter", finishSurface switch
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
        var surfaceGrade = finishSurface switch
        {
            "snow_ground" => (Roughness: 0.90f, Specular: 0.28f, WetGrade: 0.0f),
            "snow_road" => (Roughness: 0.68f, Specular: 0.28f, WetGrade: 0.0f),
            "snow_trampled" => (Roughness: 0.81f, Specular: 0.28f, WetGrade: 0.0f),
            "snow_grass" => (Roughness: 0.93f, Specular: 0.06f, WetGrade: 0.0f),
            "snow_roof" => (Roughness: 0.92f, Specular: 0.28f, WetGrade: 0.0f),
            "ice" => (Roughness: 0.26f, Specular: 0.40f, WetGrade: 0.0f),
            "grass" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.45f),
            "hay_fibers" or "hay_bundle" => (Roughness: 0.96f, Specular: 0.05f, WetGrade: 0.0f),
            "grass_tuft" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.45f),
            "roof" => (Roughness: 0.88f, Specular: 0.10f, WetGrade: 0.50f),
            "roof_metal" => (Roughness: 0.72f, Specular: 0.20f, WetGrade: 0.65f),
            "bark_birch" => (Roughness: 0.92f, Specular: 0.08f, WetGrade: 0.20f),
            "bark_pine" => (Roughness: 0.92f, Specular: 0.08f, WetGrade: 0.22f),
            "leaf_birch" => (Roughness: 0.95f, Specular: 0.06f, WetGrade: 0.12f),
            "log_wall" or "wallpaper" or "wall_institution" => (Roughness: 0.94f, Specular: 0.06f, WetGrade: 0.02f),
            // Worn lino keeps a faint sheen the walls do not have, but stays far from
            // the wet-weather response: this is indoor surface wear, not water.
            "floor_institution" => (Roughness: 0.87f, Specular: 0.15f, WetGrade: 0.05f),
            "plastic_abs" => (Roughness: 0.72f, Specular: 0.22f, WetGrade: 0.0f),
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
            "wood_furniture" or "wood_furniture_interior" => (Roughness: 0.89f, Specular: 0.10f, WetGrade: 0.16f),
            "wood_prop" => (Roughness: 0.86f, Specular: 0.12f, WetGrade: 0.28f),
            "wood_bark" => (Roughness: 0.90f, Specular: 0.08f, WetGrade: 0.24f),
            "stone" => (Roughness: 0.91f, Specular: 0.12f, WetGrade: 0.22f),
            "foliage" => (Roughness: 0.95f, Specular: 0.07f, WetGrade: 0.12f),
            "plaster" => (Roughness: 0.96f, Specular: 0.06f, WetGrade: 0.04f),
            "fabric" or "fabric_upholstery" or "cloth" or "cloth_clinic" or "cloth_towel" => (Roughness: 0.98f, Specular: 0.04f, WetGrade: 0.01f),
            "iron" => (Roughness: 0.62f, Specular: 0.30f, WetGrade: 0.0f),
            "enamel" => (Roughness: 0.30f, Specular: 0.40f, WetGrade: 0.0f),
            // VIS-092/095: the five families that used to fall through to the masonry
            // default. They are separated by roughness distribution and highlight
            // shape, never by a metallic value painted onto a dielectric finish:
            // `metal` here is painted or unknown sheet steel, so it keeps metallic 0.
            "metal" => (Roughness: 0.58f, Specular: 0.32f, WetGrade: 0.0f),
            "zinc_sheet" => (Roughness: 0.52f, Specular: 0.34f, WetGrade: 0.0f),
            "glass" => (Roughness: 0.14f, Specular: 0.52f, WetGrade: 0.0f),
            "leather" => (Roughness: 0.64f, Specular: 0.12f, WetGrade: 0.10f),
            "paper" => (Roughness: 0.94f, Specular: 0.05f, WetGrade: 0.06f),
            "painted" => (Roughness: 0.55f, Specular: 0.26f, WetGrade: 0.0f),
            "ceramic" => (Roughness: 0.36f, Specular: 0.30f, WetGrade: 0.0f),
            "rubber" => (Roughness: 0.88f, Specular: 0.10f, WetGrade: 0.05f),
            "water" => (Roughness: 0.38f, Specular: 0.45f, WetGrade: 0.98f),
            // VIS-105/VIS-095. Vehicle paint is lacquer over steel: metallic 0,
            // low roughness, a strong tight sky highlight. The two steels separate
            // by roughness and highlight shape, never by a chrome trick on paint.
            // The tyre is the darkest and dullest member, so paint, rubber, metal
            // and glass stay distinguishable where their colours are close.
            "vehicle_paint" => (Roughness: 0.30f, Specular: 0.44f, WetGrade: 0.05f),
            "vehicle_bare_metal" => (Roughness: 0.46f, Specular: 0.55f, WetGrade: 0.0f),
            "vehicle_trim_metal" => (Roughness: 0.20f, Specular: 0.70f, WetGrade: 0.0f),
            "vehicle_rubber" => (Roughness: 0.86f, Specular: 0.12f, WetGrade: 0.10f),
            "vehicle_plastic" => (Roughness: 0.62f, Specular: 0.26f, WetGrade: 0.04f),
            _ => (Roughness: 0.90f, Specular: 0.20f, WetGrade: 0.0f)
        };
        material.SetShaderParameter("roughness_value", surfaceGrade.Roughness);
        material.SetShaderParameter("specular_value", surfaceGrade.Specular);
        material.SetShaderParameter("wet_grade", surfaceGrade.WetGrade);
        // VIS-105 on the VIS-092/095 response slots: transport declares its own
        // micro relief, its own roughness distribution and its own hoarfrost, so a
        // frozen bonnet dulls instead of shining and a tyre never reads as painted
        // metal. Every value here is a response, not a colour; the vehicle families
        // still have no albedo map (that is the M07/M08 texture lane), and Low
        // keeps the flat image exactly like every other family.
        if (surface.StartsWith("vehicle_", StringComparison.Ordinal))
        {
            material.SetShaderParameter("detail_scale", new Vector2(surface switch
            {
                "vehicle_paint" => 2.2f,
                "vehicle_bare_metal" => 1.6f,
                "vehicle_rubber" => 3.0f,
                "vehicle_plastic" => 3.0f,
                _ => 1.2f
            }, surface switch
            {
                "vehicle_paint" => 2.2f,
                "vehicle_bare_metal" => 5.0f,
                _ => 3.0f
            }));
            material.SetShaderParameter("proc_relief", surface switch
            {
                "vehicle_paint" => .05f,
                "vehicle_bare_metal" => .08f,
                "vehicle_trim_metal" => .02f,
                "vehicle_rubber" => .10f,
                _ => .06f
            });
            material.SetShaderParameter("proc_relief_freq", surface switch
            {
                // Rolled steel is brushed along the panel; lacquer pebbles evenly;
                // rubber carries the mould's fine hide.
                "vehicle_bare_metal" => new Vector2(3f, 34f),
                "vehicle_paint" => new Vector2(26f, 26f),
                "vehicle_rubber" => new Vector2(20f, 12f),
                _ => new Vector2(30f, 30f)
            });
            material.SetShaderParameter("proc_roughness", surface switch
            {
                "vehicle_paint" => .10f,
                "vehicle_bare_metal" => .18f,
                "vehicle_trim_metal" => .04f,
                "vehicle_rubber" => .12f,
                _ => .10f
            });
            material.SetShaderParameter("frost_roughness", surface switch
            {
                "vehicle_paint" => .10f,
                "vehicle_bare_metal" => .16f,
                "vehicle_trim_metal" => .06f,
                "vehicle_rubber" => .06f,
                _ => .08f
            });
        }
        // VIS-092/093: the village family response. Placed after the transport block
        // so the two owners cannot overwrite each other: transport declares its own
        // rows and is absent from this table. A family with no row keeps every
        // identity value and renders exactly as before, which is what makes the wave
        // additive rather than a global restyle.
        ApplyFamilyResponse(material, surface);
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
            && ResourceLoader.Exists(winterDescriptor.Path))
        {
            var texture = ResourceLoader.Load<Texture2D>(winterDescriptor.Path);
            if (texture is not null)
            {
                material.SetShaderParameter("albedo_texture", texture);
                material.SetShaderParameter("has_albedo_texture", true);
                material.SetShaderParameter("texture_scale", winterDescriptor.Scale);
            }
        }

        var snowMaterial = surface is "snow_ground" or "snow_road" or "snow_trampled" or "snow_grass" or "snow_roof" or "ice";
        material.SetShaderParameter("snow_material", snowMaterial);
        material.SetShaderParameter("trample_ground_surface", surface is "snow_ground" or "snow_road" or "snow_trampled" or "snow_grass");
        material.SetMeta("snowTrampleBlocked", surface is "water" or "ice");
        if (snowMaterial)
        {
            material.SetShaderParameter("snow_roughness_range", surface switch
            {
                "snow_road" => new Vector2(.55f, .78f),
                "snow_trampled" => new Vector2(.72f, .88f),
                "ice" => new Vector2(.18f, .35f),
                _ => new Vector2(.82f, .95f)
            });
            material.SetShaderParameter("snow_relief_scale", surface switch
            {
                "snow_road" => .4f, "snow_trampled" => .65f, "ice" => .15f, _ => 1f
            });
            // VIS-034/081: the micro field's repeat period becomes a declared value
            // instead of the implicit 1 m tile, and it is deliberately different per
            // state so a road, a trodden track and an open field do not shout with the
            // same grain at 10 m (STYLE RECIPE §5). The vertex amplitude is untouched:
            // (r-0.5)*0.004 stays ±0.002 m, the measured winded-snow relief [K02];
            // a bigger period is a pattern decision, never a substitute for a dune.
            material.SetShaderParameter("snow_micro_scale", surface switch
            {
                "snow_road" => new Vector2(.55f, .55f),
                "snow_trampled" => new Vector2(.70f, .70f),
                "snow_grass" => new Vector2(.90f, .90f),
                "snow_roof" => new Vector2(1.10f, 1.10f),
                "ice" => new Vector2(.40f, .40f),
                _ => new Vector2(.80f, .80f)
            });
            if (surface == "ice") material.SetShaderParameter("snow_sparkle", 0f);
            if (!SuppressTextureLoadsForHeadlessTests)
            {
                foreach (var textureName in new[] { "snow_micro_response", "snow_micro_normal" })
                {
                    var path = $"res://assets/textures/painterly/{textureName}.png";
                    material.SetShaderParameter(textureName, ResourceLoader.Load<Texture2D>(path)
                        ?? throw new InvalidOperationException($"Snow response texture missing: {path}"));
                }
                material.SetShaderParameter("has_snow_micro", true);
            }
        }

        // The vertex deformation block is inert exactly when all three of its
        // inputs are: snow_trample_at returns vec4(0) unless trample_ground_surface
        // is set (its own guard), the microrelief branch requires has_snow_micro,
        // and the wind branch requires wind_sway > 0. Removing a block that
        // provably adds zero to VERTEX cannot change the visible result, and
        // leaving VERTEX unwritten is what lets the renderer use its shared shadow
        // material (uses_vertex_time && uses_vertex, see the engine's
        // SceneShaderForwardClustered::ShaderData). The previous version restricted
        // this to a hand-listed set of families; the flags themselves are the proof.
        //
        // Immutability of those flags: wind_sway and trample_ground_surface are
        // written only here, and has_snow_micro is written here (true for snow
        // surfaces only) and lowered to false by WithoutSnow. No code raises any of
        // them on an existing material, so a material inert at creation stays inert.
        //
        // Exclusions that stay: cutout/two-sided/sheer variants use other shaders
        // and never match PainterlyShader, so their alpha and culling contracts are
        // untouched; snow surfaces keep the original variant because their
        // microrelief and trample terms are live, and because a headless run that
        // skips the micro texture must not change which variant is chosen.
        var inertDeformation = !snowMaterial
            && material.Shader == PainterlyShader
            && material.GetShaderParameter("wind_sway").AsSingle() == 0f
            && !material.GetShaderParameter("has_snow_micro").AsBool()
            && !material.GetShaderParameter("trample_ground_surface").AsBool();
        if (inertDeformation) material.Shader = RigidPainterlyShader;

        Materials.Add(cacheKey, material);
        return material;
    }

    /// <summary>
    /// VIS-092/093/094/095: stamp one family's authored material response and bind
    /// its drawn maps if — and only if — the files exist. The drawn slots are
    /// conditional the same way WinterTextures are, so an undelivered map never turns
    /// into a black sampler: the family keeps its procedural response and today's
    /// image until the asset arrives, and the gate proves that every declared path is
    /// either on disk or registered as a production request.
    /// </summary>
    private static void ApplyFamilyResponse(ShaderMaterial material, string surface)
    {
        if (!FamilyResponses.TryGetValue(surface, out var response)) return;
        material.SetShaderParameter("proc_relief", response.Relief);
        material.SetShaderParameter("proc_relief_freq", response.ReliefFreq);
        material.SetShaderParameter("proc_roughness", response.ReliefRoughness);
        material.SetShaderParameter("detail_scale", new Vector2(response.TilesPerMetre, response.TilesPerMetre));
        material.SetShaderParameter("wear_bottom_gain", response.WearBottom);
        material.SetShaderParameter("wear_bottom_height", response.WearBottomHeight);
        material.SetShaderParameter("wear_end_grain", response.EndGrainWear);
        material.SetShaderParameter("frost_roughness", response.FrostRoughness);
        if (SuppressTextureLoadsForHeadlessTests) return;
        // A wear mask only ever marks a small, physically caused area (chipped paint
        // at an edge, a rust streak below a lap joint, one repaired patch). Families
        // without that reason bind nothing.
        if (BindOptionalMap(material, "detail_normal_map", response.NormalMap))
        {
            material.SetShaderParameter("has_detail_normal", true);
            material.SetShaderParameter("detail_normal_scale", response.NormalScale);
        }
        if (BindOptionalMap(material, "detail_roughness_map", response.RoughnessMap))
        {
            material.SetShaderParameter("has_detail_roughness", true);
            material.SetShaderParameter("detail_roughness_delta", response.RoughnessDelta);
        }
        if (BindOptionalMap(material, "wear_mask_map", response.WearMap))
        {
            material.SetShaderParameter("has_wear_mask", true);
            material.SetShaderParameter("wear_roughness_delta", response.WearRoughness);
            material.SetShaderParameter("wear_metallic", response.WearMetallic);
            var wearColor = Color.FromHtml(response.WearTint);
            wearColor.A = response.WearTintBlend;
            material.SetShaderParameter("wear_tint", wearColor);
        }
    }

    /// <summary>True when the optional map exists and was bound. Missing files stay
    /// silent here because the same paths are enumerated by the asset-request
    /// document, which the contract gate cross-checks; a typo would fail there.</summary>
    private static bool BindOptionalMap(ShaderMaterial material, string uniformName, string? path)
    {
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) return false;
        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture is null) return false;
        material.SetShaderParameter(uniformName, texture);
        return true;
    }

}
