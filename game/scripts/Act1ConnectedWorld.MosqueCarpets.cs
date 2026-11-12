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
        carpet.SetMeta("presentationRole","dark woven floor with separate prayer rugs aligned to the existing qibla");
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
                ALBEDO=texture(cloth,mirrored).rgb*vec3(.48,.57,.48);
                ROUGHNESS=.98; SPECULAR=.12;
            }
            """};
        var material=new ShaderMaterial{Shader=shader};
        material.SetShaderParameter("cloth",MosqueTexture(MosqueWarmMaterials+"bookcloth-albedo.png"));
        var surface=new MeshInstance3D{Name="MosquePatternedCarpetSurface",Mesh=field.Commit(),MaterialOverride=material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        surface.SetMeta("textureConsumer",MosqueWarmMaterials+"bookcloth-albedo.png");surface.SetMeta("textureUpDirection",q);
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
        for(var row=0;row<3;row++)for(var column=0;column<6;column++)
            BuildMosqueIndividualRug(room,$"MosquePrayerRug{row}_{column}",new(-4.30f+column*1.05f,floorTop+.0045f,-.65f+row*1.75f),
                q,column%3==1?"9c8a74":"a5aa8c");
        BuildMosqueIndividualRug(room,"MosqueImamPrayerRug",new(-.55f,floorTop+.0045f,-2.20f),q,"a6aa89");
        carpet.SetMeta("individualPrayerRugs",19);
        return carpet;
    }

    private void BuildMosqueIndividualRug(Node3D room,string name,Vector3 at,Vector3 q,string tint)
    {
        var rug=new MeshInstance3D{Name=name,Position=at,Rotation=new(0,Mathf.Atan2(-q.X,-q.Z),0),
            Mesh=new BoxMesh{Size=new(.76f,.003f,1.30f)},MaterialOverride=MosqueFinish("carpet",tint,1.3f),
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        room.AddChild(rug);rug.SetMeta("qiblaLocalDirection",q);
        // The existing uninterrupted 2 mm carpet remains the save/footwear
        // contact. Individual cloths have visual thickness, never step colliders.
        var border=new MosqueBookSurface();
        void Strip(Vector2 a,Vector2 b,float width)
        {
            var normal=new Vector2(-(b-a).Y,(b-a).X).Normalized()*width*.5f;
            Vector3 P(Vector2 p)=>new(p.X,.0022f,p.Y);
            border.Quad(P(a-normal),P(b-normal),P(b+normal),P(a+normal),Vector3.Up);
        }
        var frame=new[]{new Vector2(-.34f,-.60f),new Vector2(.34f,-.60f),new Vector2(.34f,.60f),new Vector2(-.34f,.60f)};
        for(var edge=0;edge<4;edge++)Strip(frame[edge],frame[(edge+1)%4],.018f);
        // A woven pointed head makes orientation legible without script.
        var head=new[]{new Vector2(-.25f,.48f),new Vector2(-.25f,-.24f),new Vector2(0,-.49f),new Vector2(.25f,-.24f),new Vector2(.25f,.48f)};
        for(var edge=0;edge<head.Length-1;edge++)Strip(head[edge],head[edge+1],.013f);
        var mesh=new ArrayMesh();border.Commit(mesh,"WovenBorder",MosqueFinish("paper","aa936b",.3f));
        rug.AddChild(new MeshInstance3D{Name="BoundEdgeAndPrayerHead",Mesh=mesh,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        var tassels=new List<Transform3D>();
        foreach(var end in new[]{-1f,1f})for(var i=0;i<24;i++)
            tassels.Add(MosqueBoxTransform(new(-.345f+i*.03f,0,end*.666f),new(.004f,.0015f,.032f),0));
        MosqueBatch(rug,"ShortWovenFringe",tassels,MosqueFinish("paper","9f916f",.3f));
    }
}
