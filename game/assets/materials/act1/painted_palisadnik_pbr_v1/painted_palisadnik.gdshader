shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_disable;
uniform sampler2D metallic_roughness_texture : filter_linear_mipmap, repeat_disable;

varying vec3 pigment_multiplier;

void vertex() {
    // CUSTOM0 is linear RGBFloat, not packed/sRGB vertex colour.
    pigment_multiplier = CUSTOM0.rgb;
}

void fragment() {
    // The lot's source-green ratio changes pigment, never atlas placement.
    // Pale cap roles carry identity. Bright worn wood may reach white.
    ALBEDO = clamp(texture(albedo_texture, UV).rgb * pigment_multiplier, vec3(0.0), vec3(1.0));
    vec3 response = texture(metallic_roughness_texture, UV).rgb;
    ROUGHNESS = response.g;
    METALLIC = response.b;
    SPECULAR = 0.5;
    NORMAL_MAP = texture(normal_texture, UV).rgb;
    NORMAL_MAP_DEPTH = 1.0;
}
