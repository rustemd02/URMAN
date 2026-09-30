using Godot;

namespace Urman.Godot;

/// <summary>An ambiguous interruption of the treeline, with no face or combat body.
/// World depth hides the wisps behind actual trunks; the centre stays empty.</summary>
internal partial class ProloguePresence : Node3D
{
    private readonly List<ShaderMaterial> _materials = new();

    public override void _Ready()
    {
        var shader = new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never;
            uniform float veil = 0.0;
            uniform float phase = 0.0;
            float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
            float noise(vec2 p) {
                vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
                return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
            }
            void fragment() {
                float edge = pow(max(0.0, sin(UV.x * 3.14159)), 1.8);
                float ends = smoothstep(0.0,0.16,UV.y)*(1.0-smoothstep(0.73,1.0,UV.y));
                float layers=noise(UV*vec2(4,8)+vec2(phase*.11,-phase*.16));
                ALBEDO=vec3(0.007,0.010,0.012);
                ALPHA=veil*edge*ends*(0.35+layers*.65);
            }
            """ };
        // A broken near-vertical mass and two branchlike, incomplete gestures.
        // Their separate offsets never resolve into a standing humanoid.
        Ribbon(shader, new(-.42f, -.45f, 0), new(-.14f, .55f, -.18f), new(.03f, 1.7f, -.35f), .66f);
        Ribbon(shader, new(.08f, -.2f, -.32f), new(.3f, .8f, -.6f), new(.19f, 1.9f, -.7f), .43f);
        Ribbon(shader, new(-.2f, .65f, -.2f), new(-1f, .91f, -.15f), new(-1.5f, .72f, .14f), .23f);
        Ribbon(shader, new(.15f, .7f, -.5f), new(.93f, 1.04f, -.35f), new(1.3f, .87f, -.18f), .19f);
        SetVeil(.34f, 0);
    }

    private void Ribbon(Shader shader, Vector3 a, Vector3 b, Vector3 c, float width)
    {
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 24;
        Vector3 Point(float t) => (1-t)*(1-t)*a + 2*(1-t)*t*b + t*t*c;
        Vector3 Vertex(float t, float side) => Point(t) + Vector3.Right * width * side * (.72f + .18f*Mathf.Sin(t*9));
        for (var i=0; i<segments; i++)
        {
            var t0=i/(float)segments; var t1=(i+1)/(float)segments;
            foreach (var (t, side) in new[] { (t0,-.5f),(t1,-.5f),(t0,.5f),(t0,.5f),(t1,-.5f),(t1,.5f) })
            { surface.SetUV(new(side+.5f,t)); surface.AddVertex(Vertex(t,side)); }
        }
        surface.GenerateNormals();
        var material = new ShaderMaterial { Shader=shader };
        _materials.Add(material);
        AddChild(new MeshInstance3D { Mesh=surface.Commit(), MaterialOverride=material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public void SetVeil(float opacity, float seconds)
    {
        foreach (var material in _materials)
        { material.SetShaderParameter("veil", opacity); material.SetShaderParameter("phase", seconds); }
    }
}
