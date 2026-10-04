using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
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
                    mesh.MaterialOverride=PainterlyMaterialLibrary.ForColor("deded1","plaster_domestic",sheltered:true);
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
        PolicePaintBatch(room,"PoliceLowerWallPaint",wallPaint,PainterlyMaterialLibrary.ForColor("aabdad","wall_institution",sheltered:true));
        PolicePaintBatch(room,"PoliceSkirting",skirting,RuralPropMaterials.Surface("steel","69766b"));
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
            _=>null
        };
    }
}
