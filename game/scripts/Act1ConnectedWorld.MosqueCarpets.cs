using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string MosqueCarpetTexturePath = MosqueWarmMaterials + "carpet-albedo.png";
    private const float MosqueCarpetThickness = .002f;

    private MeshInstance3D BuildMosquePrayerMatField(Node3D room)
    {
        var timber = room.GetNode<MeshInstance3D>("MosqueTimberFloor");
        var floor = (BoxMesh)timber.Mesh;
        var centre = timber.Position;
        var floorTop = centre.Y + floor.Size.Y * .5f;
        var carpet = FacilitySolid(room, "MosquePrayerCarpet", new(floor.Size.X, MosqueCarpetThickness, floor.Size.Z),
            new(centre.X, floorTop + MosqueCarpetThickness*.5f, centre.Z), "4b6257", "fabric");
        // This exact 2 mm support and body name belong to footwear/save support.
        var q = MosqueLocalQibla.Normalized(); var across = new Vector3(-q.Z,0,q.X);
        var x0 = centre.X-floor.Size.X*.5f; var x1 = centre.X+floor.Size.X*.5f;
        var z0 = centre.Z-floor.Size.Z*.5f; var z1 = centre.Z+floor.Size.Z*.5f;
        carpet.SetMeta("carpetFloorRect",new Rect2(x0,z0,x1-x0,z1-z0));
        carpet.SetMeta("carpetTopMetres",floorTop+MosqueCarpetThickness);
        carpet.SetMeta("qiblaBearingDegrees",MosqueQiblaBearingDegrees);
        carpet.SetMeta("textureUpDirection",q);
        carpet.SetMeta("presentationRole","continuous wool carpet with quiet aligned woven rows and a bound border");
        using var field=new SurfaceTool();field.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(SurfaceTool stream,float a,float b,float c,float d,float y)
        {
            foreach(var p in new Vector3[]{new(a,y,b),new(c,y,b),new(c,y,d),new(a,y,b),new(c,y,d),new(a,y,d)})
            {
                stream.SetNormal(Vector3.Up);
                stream.SetUV(new(p.Dot(across)/3.6f,-p.Dot(q)/5.5f));
                stream.AddVertex(p-carpet.Position);
            }
        }
        const float frame=.24f;
        Quad(field,x0+frame,z0+frame,x1-frame,z1-frame,floorTop+.0025f);
        var shader=new Shader{Code="""
            shader_type spatial;
            render_mode diffuse_burley;
            uniform sampler2D cloth : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
            void fragment() {
                vec2 mirrored = 1.0 - abs(mod(UV, 2.0) - 1.0);
                ALBEDO=texture(cloth,mirrored).rgb;
                ROUGHNESS=.98; SPECULAR=.12;
            }
            """};
        var material=new ShaderMaterial{Shader=shader};
        if(ResourceLoader.Exists(MosqueCarpetTexturePath))material.SetShaderParameter("cloth",ResourceLoader.Load<Texture2D>(MosqueCarpetTexturePath));
        var surface=new MeshInstance3D{Name="MosquePatternedCarpetSurface",Mesh=field.Commit(),MaterialOverride=material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        surface.SetMeta("textureConsumer",MosqueCarpetTexturePath);surface.SetMeta("textureUpDirection",q);
        surface.SetMeta("textureTileMetres",new Vector2(3.6f,5.5f));carpet.AddChild(surface);
        using var edge=new SurfaceTool();edge.Begin(Mesh.PrimitiveType.Triangles);
        Quad(edge,x0,z0,x1,z0+frame,floorTop+.0026f);Quad(edge,x0,z1-frame,x1,z1,floorTop+.0026f);
        Quad(edge,x0,z0+frame,x0+frame,z1-frame,floorTop+.0026f);Quad(edge,x1-frame,z0+frame,x1,z1-frame,floorTop+.0026f);
        carpet.AddChild(new MeshInstance3D{Name="MosquePrayerCarpetBorder",Mesh=edge.Commit(),
            MaterialOverride=MosqueFinish("bookcloth","b1a88c",.6f),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        using var line=new SurfaceTool();line.Begin(Mesh.PrimitiveType.Triangles);
        const float piping=.018f;
        Quad(line,x0+frame-piping,z0+frame-piping,x1-frame+piping,z0+frame,floorTop+.0027f);
        Quad(line,x0+frame-piping,z1-frame,x1-frame+piping,z1-frame+piping,floorTop+.0027f);
        Quad(line,x0+frame-piping,z0+frame,x0+frame,z1-frame,floorTop+.0027f);
        Quad(line,x1-frame,z0+frame,x1-frame+piping,z1-frame,floorTop+.0027f);
        carpet.AddChild(new MeshInstance3D{Name="MosquePrayerCarpetBorderLine",Mesh=line.Commit(),
            MaterialOverride=PainterlyMaterialLibrary.ForColor("b8a67a","cloth",sheltered:true),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        return carpet;
    }
}
