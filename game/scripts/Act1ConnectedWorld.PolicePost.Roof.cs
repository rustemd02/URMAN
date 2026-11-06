using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildPoliceRoof(Node3D room)
    {
        // Closed primitive slopes have correct outside winding and real thickness.
        room.GetNode<MeshInstance3D>("Roof").Visible=false;
        // Retire the old roof and its snow together: their different pitch
        // otherwise cuts through the replacement roof and gable.
        room.GetNode<MeshInstance3D>("RoofSnow").Visible=false;
        var slope=Mathf.Atan2(2.0f,6.4f);
        foreach(var sign in new[]{-1f,1f})
        {
            var rotation=new Vector3(0,0,-sign*slope);
            var roof=FacilitySolid(room,"PoliceMetalRoof"+sign,new(Mathf.Sqrt(6.4f*6.4f+4),.12f,9.8f),
                new(sign*3.2f,4.1f,0),"626b6b","roof_metal",rotation);
            roof.MaterialOverride=RuralPropMaterials.Surface("steel","80534a");
            // Keep the lower metal apron visible below settled snow.
            AddVisualBox(room,"PoliceRoofSnow"+sign,new(5.2f,.045f,9.6f),
                new(sign*2.6f,5.1f-2.6f*2f/6.4f+.10f,0),"dce3de","snow_roof").Rotation=rotation;
            var fascia=AddVisualBox(room,"PoliceEaveFascia"+sign,new(.10f,.20f,9.8f),new(sign*6.38f,3.10f,0),"ddd7c4","wood_painted_trim");
            fascia.MaterialOverride=RuralPropMaterials.Surface("steel","d8d3c3");
            for(var i=0;i<18;i++)
            {
                var seam=AddVisualBox(room,"PoliceRoofStandingSeam"+sign+"_"+i,
                    new(Mathf.Sqrt(6.4f*6.4f+4),.035f,.022f),new(sign*3.2f,4.18f,-4.65f+i*.55f),"765048","metal");
                seam.Rotation=rotation;
                seam.MaterialOverride=RuralPropMaterials.Surface("steel","765048");
            }
        }
        var ridge=AddVisualBox(room,"PoliceRoofRidgeCap",new(.26f,.10f,9.94f),new(0,5.17f,0),"80534a","metal");
        ridge.MaterialOverride=RuralPropMaterials.Surface("steel","80534a");
        using var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        foreach(var sign in new[]{-1f,1f})
        {
            var a=new Vector3(-6,3.0f,sign*4.515f);var b=new Vector3(6,3.0f,sign*4.515f);var c=new Vector3(0,5.1f,sign*4.515f);
            foreach(var face in new[]{new[]{a,c,b},new[]{a,b,c}})
            {
                st.SetNormal((face[1]-face[0]).Cross(face[2]-face[0]).Normalized());
                foreach(var v in face){st.SetUV(new(v.X/2,v.Y/2));st.AddVertex(v);}
            }
        }
        st.Index();room.AddChild(new MeshInstance3D{Name="PoliceGableInfill",Mesh=st.Commit(),
            MaterialOverride=PoliceExteriorBrickMaterial()});
    }
}
