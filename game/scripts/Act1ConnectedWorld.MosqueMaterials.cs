using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string MosqueWarmMaterials = "res://assets/materials/mosque/warm_hall_v1/";
    private readonly Dictionary<string, Material> _mosqueWarmFinishes = new();
    private Shader? _mosqueWarmShader;

    // Reference, not reconstruction: timber lining and roof minaret typology:
    // https://tatarica.org/ru/razdely/kultura/iskusstvo/arhitektura/zdaniya-i-sooruzheniya/medina
    // Rural timber example, Bolshaya Yelga (1879):
    // https://tatarica.org/ru/razdely/municipalnye-obrazovaniya/municipalnye-rajony/rybno-slobodskij-rajon-1/bolshaya-elga
    // Rural masonry comparison, Nizhnyaya Bereske, conservation assessment:
    // https://mincult.tatarstan.ru/file/pub/pub_957053.pdf
    // The wallpaper, joinery and office are an artistic proposal for this
    // fictional village, not an attributed historical or religious practice.
    private Material MosqueFinish(string texture, string tint = "ffffff", float repeatMetres = 1f, float roughness = .91f)
    {
        var key = $"{texture}/{tint}/{repeatMetres}/{roughness}";
        if (_mosqueWarmFinishes.TryGetValue(key, out var existing)) return existing;
        var path = MosqueWarmMaterials + texture + "-albedo.png";
        _mosqueWarmShader ??= new Shader { Code = """
            shader_type spatial;
            render_mode diffuse_burley;
            uniform sampler2D weave : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
            uniform vec4 tint : source_color = vec4(1.0);
            uniform float tile_metres = 1.0;
            uniform float roughness_value = 0.91;
            varying vec3 piece_position;
            varying vec3 piece_normal;
            vec2 mirror_uv(vec2 uv) { return 1.0 - abs(mod(uv, 2.0) - 1.0); }
            void vertex() {
                vec3 instance_scale=vec3(length(MODEL_MATRIX[0].xyz),length(MODEL_MATRIX[1].xyz),length(MODEL_MATRIX[2].xyz));
                piece_position=VERTEX*instance_scale; piece_normal=NORMAL;
            }
            void fragment() {
                vec3 w=pow(abs(piece_normal),vec3(6.0)); w/=max(w.x+w.y+w.z,0.0001);
                vec3 p=piece_position/tile_metres;
                vec3 c=texture(weave,mirror_uv(p.zy)).rgb*w.x
                      +texture(weave,mirror_uv(p.xz)).rgb*w.y
                      +texture(weave,mirror_uv(p.xy)).rgb*w.z;
                ALBEDO=c*tint.rgb; ROUGHNESS=roughness_value; SPECULAR=0.18;
            }
            """ };
        var finish = new ShaderMaterial { Shader = _mosqueWarmShader };
        finish.SetShaderParameter("tint", new Color(tint));
        finish.SetShaderParameter("tile_metres", repeatMetres);
        finish.SetShaderParameter("roughness_value", roughness);
        if (ResourceLoader.Exists(path)) finish.SetShaderParameter("weave", ResourceLoader.Load<Texture2D>(path));
        finish.SetMeta("textureSource", path);
        finish.SetMeta("textureRecipe", "built-in image_gen; flat albedo; no lettering, religious symbols or baked lighting");
        _mosqueWarmFinishes.Add(key, finish);
        return finish;
    }

    private MeshInstance3D MosqueWood(Node3D parent, string name, Vector3 size, Vector3 at,
        bool solid = false, string tint = "ffffff", Vector3 rotation = default)
    {
        var part = solid ? FacilitySolid(parent, name, size, at, "8d7453", "wood_furniture", rotation)
            : AddVisualBox(parent, name, size, at, "8d7453", "wood_furniture");
        part.Rotation = rotation;
        part.MaterialOverride = MosqueFinish("walnut", tint, .75f);
        part.Mesh = MosqueJoineryBox(size, Mathf.Min(.012f, Mathf.Min(size.X, Mathf.Min(size.Y, size.Z)) * .22f));
        return part;
    }

    private static ArrayMesh MosqueJoineryBox(Vector3 size, float radius)
    {
        // Rounded edges catch the window light; furniture retains its exact
        // outside envelope and conservative matching box contact.
        using var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles);
        var half = size * .5f; var core = half - Vector3.One * radius;
        float Axis(float h, int i) => i switch
        { 0 => -h, 1 => -h + radius * .293f, 2 => -h + radius, 3 => h - radius, 4 => h - radius * .293f, _ => h };
        for (var axis = 0; axis < 3; axis++) foreach (var side in new[] { -1f, 1f })
        {
            var u = (axis + 1) % 3; var v = (axis + 2) % 3;
            Vector3 Point(int i, int j)
            {
                var p = Vector3.Zero; p[axis] = side * half[axis];
                p[u] = Axis(half[u], i); p[v] = Axis(half[v], j); return p;
            }
            for (var i = 0; i < 5; i++) for (var j = 0; j < 5; j++)
            {
                var a = Point(i, j); var b = Point(i + 1, j); var c = Point(i + 1, j + 1); var d = Point(i, j + 1);
                foreach (var p in side > 0 ? new[] { a, c, b, a, d, c } : new[] { a, b, c, a, c, d })
                {
                    var nearest = new Vector3(Mathf.Clamp(p.X, -core.X, core.X),
                        Mathf.Clamp(p.Y, -core.Y, core.Y), Mathf.Clamp(p.Z, -core.Z, core.Z));
                    var normal = (p - nearest).Normalized();
                    tool.SetNormal(normal); tool.SetUV(new(p[u], p[v]));
                    tool.AddVertex(nearest + normal * radius);
                }
            }
        }
        tool.Index(); return tool.Commit();
    }

    private void MosqueBookFinish(MeshInstance3D book, string tint = "ffffff")
    {
        for (var surface = 0; surface < book.Mesh.GetSurfaceCount(); surface++)
        {
            var cloth = book.Mesh is ArrayMesh mesh && mesh.SurfaceGetName(surface) == "ClothBoardsAndSpine";
            book.SetSurfaceOverrideMaterial(surface, MosqueFinish(cloth ? "bookcloth" : "paper", tint,
                cloth ? .35f : .25f, .97f));
        }
    }

    private void MosquePaper(Node3D parent, string name, Vector3 at, Vector2 size, Vector3 rotation)
    {
        var paper = AddVisualBox(parent, name, new(size.X, .002f, size.Y), at, "e8ddc4", "paper");
        paper.RotationDegrees = rotation;
        paper.MaterialOverride = MosqueFinish("paper", "ffffff", .30f, 1f);
    }
}
