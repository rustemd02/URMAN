using Godot;

namespace Urman.Godot;

/// <summary>Measured floor UVs and unique handmade faces. Colour supplies
/// pigment; paper curl, frame joints and textile folds remain geometry.</summary>
public static class CivicSurfaceLibrary
{
    public const string Root = "res://assets/textures/realism_20260930/";
    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();
    private static readonly Dictionary<string, (string Atlas, int Index)> Signs = new()
    {
        ["КАРА-УРМАН УРТА МӘКТӘБЕ\nКАРА-УРМАНСКАЯ СРЕДНЯЯ ШКОЛА"] = ("a",0),
        ["МӘДӘНИЯТ ЙОРТЫ\nДОМ КУЛЬТУРЫ"] = ("a",1),
        ["«КАРА УРМАН» СОВХОЗЫ\nИДАРӘСЕ · КОНТОРА"] = ("a",2), ["ПОЧТА"] = ("a",3),
        ["МАКТАУ ТАКТАСЫ\nДОСКА ПОЧЁТА"] = ("a",4), ["Часть рамок снята"] = ("a",5),
        ["М.  А."] = ("a",6), ["УРМАН КАМИЛЛӘРЕ — КАРА-УРМАН МӘКТӘБЕ"] = ("a",7),
        ["ГАРДЕРОБ · ЧИК"] = ("a",8), ["КАССА"] = ("a",9), ["ЗАЛ · 96 УРЫН"] = ("a",10),
        ["КОСТЮМЕРНАЯ"] = ("a",11), ["ИНСТРУМЕНТЫ"] = ("a",12),
        ["Мәктәп почмагы\nШкольный уголок"] = ("a",13),
        ["УРМАН — балалар рәсемнәре\nЛес — рисунки детей"] = ("a",14), ["Габдулла Тукай"] = ("a",15),
        ["УЧИТЕЛЬСКАЯ-2\nне открывать"] = ("b",0), ["5 «А»\nтечёт крыша"] = ("b",1),
        ["6 «Б»\nна ремонте"] = ("b",2), ["КЛАДОВАЯ"] = ("b",3), ["Проход закрыт"] = ("b",4),
        ["КАРТА · КАРА-УРМАН"] = ("b",5), ["ТАТАРСТАН"] = ("b",6), ["КАРА-УРМАН\nстарый план"] = ("b",7),
        ["Снимок забрали для архива"] = ("b",8), ["Сцену собрал Габдулла Сабиров"] = ("b",9),
        ["Наҗия апа — не трогать, ещё дошью"] = ("b",10),
        ["Автобус — борылышта\nАвтобус — у поворота"] = ("b",11), ["САБАНТУЙ"] = ("b",12), ["Сабантуй"] = ("b",12),
        ["1–4 класс"] = ("b",13), ["5–9 класс"] = ("b",14), ["ЧӘЙ ВАКЫТЫ"] = ("b",15)
    };

    public static Node3D Sign(Node3D parent, string text, Vector3 at, float yaw, Vector2 size)
    {
        if (!Signs.TryGetValue(text, out var cell)) throw new InvalidOperationException("Uninventoried civic sign: " + text);
        var exterior = cell.Atlas == "a" && cell.Index < 4;
        var pigment = exterior ? Face("signs_exterior_v1_atlas.png",2,2,cell.Index,.55f)
            : cell.Atlas == "b" && cell.Index == 10 ? Face("costume_tag_v1_basecolor.png")
            : Face("signs_civic_"+cell.Atlas+(cell.Atlas == "b" ? "_v2_atlas.png" : "_v1_atlas.png"),4,4,cell.Index);
        var node = FramedFace(parent,"HandmadeSign",at,yaw,size,pigment,paper:!exterior);
        node.SetMeta("readableText",text); node.SetMeta("decorativeOnly",true); return node;
    }

    public static StandardMaterial3D Face(string file, int columns = 1, int rows = 1, int index = 0, float roughness = .88f)
    {
        var key = $"{file}:{columns}:{rows}:{index}:{roughness}";
        if (Materials.TryGetValue(key, out var saved)) return saved;
        const float inset = .012f; // Cell padding also prevents neighbouring atlas ink in mip levels.
        var material = new StandardMaterial3D { ResourceName = "Civic_" + key,
            Roughness = roughness, Metallic = 0, AlbedoColor = Colors.White,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Uv1Scale = new((1 - 2 * inset) / columns, (1 - 2 * inset) / rows, 1),
            Uv1Offset = new((index % columns + inset) / columns, (index / columns + inset) / rows, 0) };
        var path = Root + file;
        if (ResourceLoader.Exists(path)) material.AlbedoTexture = ResourceLoader.Load<Texture2D>(path);
        else GD.PushWarning("Civic handmade surface missing: " + path);
        material.SetMeta("sourceTexture", path);
        material.SetMeta("uniqueAtlasCell", index);
        material.SetMeta("surfaceUVContract", $"full unique face; {columns} x {rows}, row-major cell {index}; no repeated writing");
        Materials[key] = material;
        return material;
    }

    /// <summary>VIS-094: a public facade identified by its function, not by a random
    /// tint. The painterly owner keeps the paint, the macro wavelength, the base
    /// soiling and the repair mask; this library only names the building role so the
    /// civic builder call sites stay readable. A sheltered (interior) call never takes
    /// the soiling, and signage stays on <see cref="Face"/>, so a board is never
    /// dimmed by a wall finish.</summary>
    public static Material PublicFacade(string role, string tint, bool sheltered = false) =>
        PainterlyMaterialLibrary.ForCivicSurface(tint, role, sheltered);

    /// <summary>VIS-080/038: the one call a canopy, porch or room owner needs. It
    /// lowers the per-instance snow shelter on every mesh under <paramref name="root"/>
    /// instead of copying a material per protected object. 1 = no settled snow.</summary>
    public static void SetSnowShelter(Node3D root, float shelter = 1f)
    {
        // GeometryInstance3D also matches MeshInstance3D and MultiMeshInstance3D, so one
        // walk covers every painted surface under the owner.
        foreach (var instance in root.FindChildren("*", nameof(GeometryInstance3D), true, false)
                     .OfType<GeometryInstance3D>())
            PainterlyMaterialLibrary.SetSnowShelter(instance, shelter);
    }

    public static StandardMaterial3D Parquet()
    {
        const string key = "parquet";
        if (Materials.TryGetValue(key, out var saved)) return saved;
        var material = new StandardMaterial3D { ResourceName = "Civic_HerringboneOak_MetricUV",
            AlbedoTexture = ResourceLoader.Load<Texture2D>(Root + "herringbone_oak_v1_basecolor.png"),
            Roughness = .43f, Metallic = 0, MetallicSpecular = .38f,
            Uv1Scale = new(1 / 1.4f, 1 / 1.4f, 1), TextureRepeat = true };
        material.SetMeta("surfaceTileMetres", 1.4f);
        material.SetMeta("footstepSurface", "herringbone_parquet");
        material.SetMeta("sourceTexture", Root + "herringbone_oak_v1_basecolor.png");
        Materials[key] = material; return material;
    }

    public static MeshInstance3D Floor(Node3D parent, string name, Vector2 size, Vector3 at)
    {
        using var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        void V(float x, float z) { s.SetNormal(Vector3.Up); var u=x+at.X; var v=z+at.Z; s.SetUV(new((u-v)*.70710678f,(u+v)*.70710678f)); s.AddVertex(new(x, 0, z)); }
        var x = size.X * .5f; var z = size.Y * .5f;
        // Godot treats clockwise winding, seen from above, as the front face.
        V(-x,-z); V(x,z); V(-x,z); V(-x,-z); V(x,-z); V(x,z);
        var node = new MeshInstance3D { Name = name, Position = at, Mesh = s.Commit(), MaterialOverride = Parquet(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        node.SetMeta("footstepSurface", "herringbone_parquet"); node.SetMeta("woodCreak", true);
        parent.AddChild(node); return node;
    }

    public static MeshInstance3D Paper(Node3D parent, string name, Vector2 size, Vector3 at, Material material, float curl = .004f)
    {
        using var s = new SurfaceTool(); s.Begin(Mesh.PrimitiveType.Triangles);
        const int nx = 12, ny = 16;
        void V(int x, int y)
        {
            var u = x / (float)nx; var v = y / (float)ny;
            // A loose lower corner lifts; the sheet's middle stays attached to its backing.
            var lift = curl * (Mathf.Pow(u, 8) * Mathf.Pow(v, 6) + .3f * Mathf.Sin(u * Mathf.Pi) * Mathf.Pow(v, 5));
            s.SetUV(new(u,v)); s.AddVertex(new((u-.5f)*size.X, (.5f-v)*size.Y, lift));
        }
        for (var y=0;y<ny;y++) for(var x=0;x<nx;x++)
        { V(x,y); V(x,y+1); V(x+1,y+1); V(x,y); V(x+1,y+1); V(x+1,y); }
        s.Index(); s.GenerateNormals();
        var mesh = new MeshInstance3D { Name = name, Position = at, Mesh = s.Commit(), MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(mesh); return mesh;
    }

    public static MeshInstance3D HangingTextile(Node3D parent,string name,Vector2 size,Vector3 at,Material pigment)
    {
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        const int nx=24,ny=32;
        void V(int i,int j)
        {
            var u=i/(float)nx;var v=j/(float)ny;
            var fold=Mathf.Sin(u*Mathf.Pi*6)*(.008f+.006f*v)+Mathf.Sin(u*Mathf.Pi*12)*.002f;
            var hem=Mathf.Pow(v,8)*Mathf.Sin(u*Mathf.Pi*3)*.006f;
            s.SetUV(new(u,v));s.AddVertex(new((u-.5f)*size.X,(.5f-v)*size.Y+hem,fold));
        }
        for(var j=0;j<ny;j++)for(var i=0;i<nx;i++){V(i,j);V(i,j+1);V(i+1,j+1);V(i,j);V(i+1,j+1);V(i+1,j);}
        s.Index();s.GenerateNormals();
        var textile=new MeshInstance3D { Name=name,Position=at,Mesh=s.Commit(),MaterialOverride=pigment };
        textile.SetMeta("surfaceUVContract","full unique textile flat-pattern; physical folds and hanging hem");
        parent.AddChild(textile);return textile;
    }

    public static Node3D FramedFace(Node3D parent, string name, Vector3 at, float yaw, Vector2 size, Material pigment, bool paper = true)
    {
        var root = new Node3D { Name = name, Position = at, RotationDegrees = new(0,yaw,0) };
        parent.AddChild(root);
        root.SetMeta("surfacePolicy", "unique ImageGen pigment; real backing, four separate bevelled frame rails and curled paper");
        var wood = RuralPropMaterials.Surface("wood");
        RuralPropGeometry.Block(root,"Backing",new(size.X+.04f,size.Y+.04f,.012f),new(0,0,-.02f),RuralPropMaterials.Surface("plywood"),.002f);
        foreach (var sign in new[] {-1f,1f})
        {
            RuralPropGeometry.Block(root,"FrameVertical"+sign,new(.035f,size.Y+.07f,.034f),new(sign*(size.X*.5f+.0175f),0,-.001f),wood,.004f);
            RuralPropGeometry.Block(root,"FrameHorizontal"+sign,new(size.X,.035f,.034f),new(0,sign*(size.Y*.5f+.0175f),-.001f),wood,.004f);
        }
        Paper(root,"HandmadeFace",size,new(0,0,.006f),pigment,paper ? .004f : 0);
        return root;
    }
}
