using Godot;

namespace Urman.Godot;

/// <summary>Technical opacity masks for airborne ice and powder. These are
/// rendering resources, not generated artwork or a replacement for material maps.</summary>
public static class WinterParticleSurfaces
{
    private static Texture2D? _mask;
    // W4/P3: the airborne-snow shader program is a compile-time constant, so one
    // Shader resource serves every emitter. The open-snow QuadMesh and its
    // StandardMaterial3D are immutable after creation (no caller writes Size,
    // Material or a shader parameter on them), so they are reused per
    // (size, opacity). The room-exclusion ShaderMaterial is deliberately NOT
    // cached: AgentBAct1ExteriorLayer writes per-emitter uniforms into it. It is
    // however remembered in _airborneMaterials, so an applied atmosphere state can
    // re-assert the powder tint (VIS-043) without the light layer owning the
    // material itself.
    private static Shader? _roomExclusionShader;
    private static readonly Dictionary<(float Size, float Opacity), QuadMesh> _openSnowMeshes = new();
    private static Gradient? _fade;

    /// <summary>
    /// VIS-043: the authored colour of falling powder for the applied atmosphere
    /// state. The neutral value is today's constant, so nothing changes until a
    /// profile pushes a tint. Only the room-exclusion (world-space streaked)
    /// airborne material is tracked: the ground puffs and wheel spray keep their
    /// own neutral value, so the prologue and the vehicle are untouched.
    /// </summary>
    public static Color AirborneSnowTint { get; private set; } = new(.93f, .95f, .98f);

    /// <summary>Live room-exclusion materials this factory handed out. The village
    /// weather owner creates exactly one, so the cap is headroom, not a policy; a
    /// dead wrapper is pruned before anything is refused.</summary>
    private static readonly List<ShaderMaterial> _airborneMaterials = new();
    private const int AirborneMaterialLimit = 8;

    /// <summary>
    /// Re-tints every live airborne snow material, preserving each material's own
    /// alpha (the opacity the emitter was built with). Called by the single
    /// atmosphere writer next to PainterlyMaterialLibrary.SetSnowMood, so the
    /// powder in front of the camera and the snow on the ground cannot state two
    /// different weathers. No emitter, material or uniform is created here.
    /// </summary>
    public static void SetAirborneSnowTint(Color tint)
    {
        if (tint == AirborneSnowTint) return;
        AirborneSnowTint = tint;
        for (var index = _airborneMaterials.Count - 1; index >= 0; index--)
        {
            var material = _airborneMaterials[index];
            if (!GodotObject.IsInstanceValid(material))
            {
                _airborneMaterials.RemoveAt(index);
                continue;
            }
            var current = (Color)material.GetShaderParameter("snow_tint");
            material.SetShaderParameter("snow_tint", new Color(tint.R, tint.G, tint.B, current.A));
        }
    }

    private static void TrackAirborneMaterial(ShaderMaterial material)
    {
        _airborneMaterials.RemoveAll(candidate => !GodotObject.IsInstanceValid(candidate));
        // Already tracked through another cache entry: never grow the list for one
        // material, the tint write would simply repeat.
        if (_airborneMaterials.Contains(material)) return;
        if (_airborneMaterials.Count >= AirborneMaterialLimit)
        {
            global::Godot.GD.PushWarning(
                $"WinterParticleSurfaces: airborne snow material cap {AirborneMaterialLimit} reached; "
                + "a new emitter will keep the neutral tint until an old one is freed.");
            return;
        }
        _airborneMaterials.Add(material);
    }
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
        if (roomExclusion)
        {
            // Same code as before, interned once: Snow(.026f, .50f, true) is
            // built by AgentBAct1ExteriorLayer.CreateSnowflakeMesh, and the
            // material below stays per-call because that owner writes
            // snow_velocity and the shelter_* uniforms into it afterwards.
            _roomExclusionShader ??= new Shader { Code = """
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
                """ };
            var snow = new ShaderMaterial { Shader = _roomExclusionShader };
            snow.SetShaderParameter("snow_mask", _mask);
            // The tint starts at the neutral constant and is re-asserted whenever an
            // atmosphere state changes (SetAirborneSnowTint), so an emitter built
            // before the first zone switch still matches the applied profile.
            snow.SetShaderParameter("snow_tint", new Color(
                AirborneSnowTint.R, AirborneSnowTint.G, AirborneSnowTint.B, opacity));
            snow.SetShaderParameter("flake_size", size);
            TrackAirborneMaterial(snow);
            return new QuadMesh { Size = new(size, size), Material = snow };
        }
        // Identical property values for identical parameters, returned once and
        // reused. Callers only read the mesh (as CpuParticles3D.Mesh); the
        // shared albedo mask is the same single _mask texture as before.
        if (!_openSnowMeshes.TryGetValue((size, opacity), out var cached))
        {
            cached = new QuadMesh
            {
                Size = new(size, size),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new(.93f, .95f, .98f, opacity), AlbedoTexture = _mask,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    VertexColorUseAsAlbedo = true
                }
            };
            _openSnowMeshes[(size, opacity)] = cached;
        }
        return cached;
    }
    public static Gradient Fade()
    {
        // W4/P3: the ramp is a constant and every caller only assigns it to
        // CpuParticles3D.ColorRamp (no offsets/colors are ever mutated), so the
        // same value is returned instead of a fresh allocation.
        if (_fade is null)
        {
            _fade = new Gradient
            {
                Colors = [new(1, 1, 1, 0), Colors.White, Colors.White, new(1, 1, 1, 0)],
                Offsets = [0, .12f, .65f, 1]
            };
        }
        return _fade;
    }

    /// <summary>
    /// Test-only, like PainterlyMaterialLibrary.ClearCacheForHeadlessTests: the
    /// retained mask, shader, gradient and per-parameter meshes would otherwise
    /// outlive the smoke scene and be reported as resources still in use at
    /// exit. Called after the scene is freed, so no live node still holds them;
    /// the next Snow/Fade call rebuilds every piece lazily.
    /// </summary>
    public static void ClearCacheForHeadlessTests()
    {
        // Release the holders of the shared resources before the shared
        // resources themselves: each cached mesh owns its material, and those
        // materials reference _mask.
        foreach (var mesh in _openSnowMeshes.Values)
        {
            mesh.Material?.Dispose();
            mesh.Dispose();
        }
        _openSnowMeshes.Clear();
        // The tracked room-exclusion materials belong to the emitter that was just
        // freed; drop the list and return the tint to the neutral constant so the
        // next scene starts from the same state as the first one.
        _airborneMaterials.Clear();
        AirborneSnowTint = new Color(.93f, .95f, .98f);
        _roomExclusionShader?.Dispose();
        _roomExclusionShader = null;
        _fade?.Dispose();
        _fade = null;
        _mask?.Dispose();
        _mask = null;
    }
}
