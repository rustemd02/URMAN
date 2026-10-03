using Godot;

namespace Urman.Godot;

/// <summary>Technical opacity masks for airborne ice and powder. These are
/// rendering resources, not generated artwork or a replacement for material maps.</summary>
public static class WinterParticleSurfaces
{
    private static Texture2D? _mask;
    public static QuadMesh Snow(float size, float opacity, bool roomExclusion = false)
    {
        if (_mask is null)
        {
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var px = (x + .5f - 32f) / 32f;
                var py = (y + .5f - 32f) / 32f;
                var radius = Mathf.Sqrt(px * px + py * py);
                var edge = 1f - Mathf.SmoothStep(.18f, 1f, radius);
                var grain = .86f + .14f * Mathf.Sin(x * 1.7f + Mathf.Cos(y * 1.1f));
                image.SetPixel(x, y, new Color(1, 1, 1, edge * edge * grain));
            }
            _mask = ImageTexture.CreateFromImage(image);
        }
        Material material;
        if (roomExclusion)
        {
            var snow = new ShaderMaterial { Shader = new Shader { Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled;
                uniform sampler2D snow_mask : source_color;
                uniform vec4 snow_tint : source_color;
                uniform vec3 snow_velocity = vec3(0.0);
                uniform float flake_size = 0.044;
                uniform bool shelter_enabled = false;
                uniform mat4 shelter_from_world;
                uniform vec3 shelter_centre;
                uniform vec3 shelter_half;
                varying float in_room;
                varying float near_fade;
                void vertex() {
                    vec3 local = (shelter_from_world * MODEL_MATRIX[3]).xyz - shelter_centre;
                    in_room = shelter_enabled && all(lessThan(abs(local), shelter_half)) ? 1.0 : 0.0;
                    // Project the existing world wind into the camera plane. A short
                    // exposure stretches powder along its motion, never along screen Y.
                    vec3 view_position = (VIEW_MATRIX * MODEL_MATRIX[3]).xyz;
                    vec3 velocity = (VIEW_MATRIX * vec4(snow_velocity, 0.0)).xyz;
                    vec2 wind = velocity.xy - view_position.xy * velocity.z / min(view_position.z, -0.6);
                    float speed = length(wind);
                    vec2 along = speed > 0.001 ? wind / speed : vec2(0.0, 1.0);
                    vec2 across = vec2(along.y, -along.x);
                    mat4 facing = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
                    facing[0].xyz = (INV_VIEW_MATRIX[0].xyz * across.x + INV_VIEW_MATRIX[1].xyz * across.y)
                        * length(MODEL_MATRIX[0].xyz);
                    facing[1].xyz = (INV_VIEW_MATRIX[0].xyz * along.x + INV_VIEW_MATRIX[1].xyz * along.y)
                        // Bound the streak inside the occupied room's 0.15m shelter margin.
                        * (length(MODEL_MATRIX[1].xyz) + min(speed * 0.012, 0.16) / flake_size);
                    facing[2].xyz *= length(MODEL_MATRIX[2].xyz);
                    near_fade = smoothstep(0.6, 1.8, distance(MODEL_MATRIX[3].xyz, INV_VIEW_MATRIX[3].xyz));
                    MODELVIEW_MATRIX = VIEW_MATRIX * facing;
                }
                void fragment() {
                    if (in_room > 0.5) discard;
                    vec4 flake = texture(snow_mask, UV) * snow_tint * COLOR;
                    ALBEDO = flake.rgb;
                    ALPHA = flake.a * near_fade;
                }
                """ } };
            snow.SetShaderParameter("snow_mask", _mask);
            snow.SetShaderParameter("snow_tint", new Color(.93f, .95f, .98f, opacity));
            snow.SetShaderParameter("flake_size", size);
            material = snow;
        }
        else material = new StandardMaterial3D
        {
            AlbedoColor = new(.93f, .95f, .98f, opacity), AlbedoTexture = _mask,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true
        };
        return new QuadMesh
        {
            Size = new(size, size),
            Material = material
        };
    }
    public static Gradient Fade() => new()
    {
        Colors = [new(1, 1, 1, 0), Colors.White, Colors.White, new(1, 1, 1, 0)],
        Offsets = [0, .12f, .65f, 1]
    };
}
