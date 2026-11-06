using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Dictionary<string,StandardMaterial3D> PoliceFinishes=new();

    private static StandardMaterial3D PoliceMetricFinish(string file,string tint,Vector3 repeat,float roughness=.9f)
    {
        var key=file+":"+tint+":"+repeat;
        if(PoliceFinishes.TryGetValue(key,out var saved))return saved;
        var material=new StandardMaterial3D
        {
            AlbedoColor=Color.FromHtml(tint), Roughness=roughness, MetallicSpecular=.12f,
            Uv1Triplanar=true, Uv1WorldTriplanar=true, Uv1Scale=repeat,
            TextureFilter=BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        var path="res://assets/textures/"+file;
        material.AlbedoTexture=ResourceLoader.Load<Texture2D>(path)
            ??throw new InvalidOperationException("Police finish is missing: "+path);
        PoliceFinishes[key]=material;
        return material;
    }

    private static StandardMaterial3D PoliceExteriorBrickMaterial()=>PoliceMetricFinish(
        "realism_20260929/public_red_brick_source_v1_basecolor.png","c4b8a8",new(1,1.5f,1));

    // Opt in only this new placement; existing houses and drivable vehicles keep their materials.
    private static void ApplyPoliceSurfaceMaterials(Node3D room)
    {
        var wallPaint=new List<Transform3D>();var skirting=new List<Transform3D>();
        foreach(var mesh in room.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>())
        {
            if(mesh.MaterialOverride is ShaderMaterial painted)
            {
                if(PoliceMappedMaterial(painted) is { } mapped)mesh.MaterialOverride=mapped;
                if(mesh.Visible&&mesh.Name.ToString().StartsWith("Police",StringComparison.Ordinal)
                    &&painted.GetMeta("surface","").AsString()=="plaster")
                {
                    mesh.MaterialOverride=PoliceMetricFinish("painterly/aged_plaster_v3_albedo.png","e7e5dc",new(.6f,.6f,.6f));
                    if(mesh.Mesh is BoxMesh box&&mesh.GetParent()==room)
                    {
                        var bottom=mesh.Position.Y-box.Size.Y*.5f;var top=Math.Min(1.10f,mesh.Position.Y+box.Size.Y*.5f);
                        if(bottom<top&&bottom<.1f)
                        {
                            var size=new Vector3(box.Size.X+.004f,top-bottom,box.Size.Z+.004f);
                            wallPaint.Add(new Transform3D(Basis.Identity.Scaled(size),new(mesh.Position.X,(bottom+top)*.5f,mesh.Position.Z)));
                            size=new(box.Size.X+.008f,.08f,box.Size.Z+.008f);
                            skirting.Add(new Transform3D(Basis.Identity.Scaled(size),new(mesh.Position.X,.04f,mesh.Position.Z)));
                        }
                    }
                }
            }
        }
        PolicePaintBatch(room,"PoliceLowerWallPaint",wallPaint,PoliceMetricFinish("painterly/aged_plaster_v3_albedo.png","aebdb0",new(.6f,.6f,.6f)));
        PolicePaintBatch(room,"PoliceSkirting",skirting,RuralPropMaterials.Surface("steel","69766b"));
        BuildPoliceBrickFacades(room);
    }

    private static void BuildPoliceBrickFacades(Node3D room)
    {
        // Thin exterior finish follows the existing structural shell and its
        // exact openings; interior paint and every physical contact stay intact.
        void Face(string name,Vector3 size,Vector3 at)
        {
            var mesh=new MeshInstance3D {Name=name,Mesh=new BoxMesh {Size=size},Position=at,
                MaterialOverride=PoliceExteriorBrickMaterial()};
            room.AddChild(mesh);
        }
        foreach(var sign in new[]{-1f,1f})
            Face("PoliceBrickSide"+sign,new(.024f,3,9.025f),new(sign*6.014f,1.5f,0));
        Face("PoliceBrickRear",new(12.025f,3,.024f),new(0,1.5f,-4.514f));
        foreach(var mesh in room.GetChildren().OfType<MeshInstance3D>().ToArray())
        {
            var name=mesh.Name.ToString();
            if(mesh.Mesh is not BoxMesh box||!(name.StartsWith("PoliceFrontPier",StringComparison.Ordinal)
                ||name.StartsWith("PoliceWindowBelow",StringComparison.Ordinal)
                ||name.StartsWith("PoliceWindowAbove",StringComparison.Ordinal)||name=="PoliceDoorLintel"))continue;
            Face("PoliceBrick_"+name,new(box.Size.X,box.Size.Y,.024f),new(mesh.Position.X,mesh.Position.Y,4.514f));
        }
        if(room.GetNodeOrNull<MeshInstance3D>("Plinth") is { } plinth)
            plinth.MaterialOverride=RuralPropMaterials.Surface("concrete","aaa79e");
        if(room.GetNodeOrNull<MeshInstance3D>("NameBoard") is { } signBoard)
            signBoard.MaterialOverride=RuralPropMaterials.Surface("steel","234d75");
        if(room.GetNodeOrNull<MeshInstance3D>("Canopy") is { } canopy)
            canopy.MaterialOverride=RuralPropMaterials.Surface("steel","80534a");
        room.SetMeta("exteriorStyle","rural Tatarstan district police point: warm brick, red-brown standing-seam gable roof, ivory trim, blue sign; reference MVD Novy Yavash");
    }
    private static void PolicePaintBatch(Node3D room,string name,List<Transform3D> transforms,Material material)
    {
        var batch=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=new BoxMesh(),InstanceCount=transforms.Count};
        for(var i=0;i<transforms.Count;i++)batch.SetInstanceTransform(i,transforms[i]);
        room.AddChild(new MultiMeshInstance3D {Name=name,Multimesh=batch,MaterialOverride=material});
    }
    private static Material? PoliceMappedMaterial(ShaderMaterial source)
    {
        var kind=source.GetMeta("surface","").AsString();
        var tint=source.GetShaderParameter("base_color").AsColor().ToHtml(false);
        return kind switch
        {
            "metal"=>RuralPropMaterials.Surface("steel",tint),
            "rubber"=>RuralPropMaterials.Surface("rubber",tint),
            "paper"=>CivicSurfaceLibrary.Face("quest_papers_v2_atlas.png",2,2,3),
            "plaster"=>PoliceMetricFinish("painterly/aged_plaster_v3_albedo.png","e7e5dc",new(.6f,.6f,.6f)),
            "floor_institution"=>RuralPropMaterials.Surface("concrete","b8b6aa"),
            _=>null
        };
    }
}
