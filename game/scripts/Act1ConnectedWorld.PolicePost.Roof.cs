using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildPoliceRoof(Node3D room)
    {
        // Closed primitive slopes have correct outside winding and real thickness.
        room.GetNode<MeshInstance3D>("Roof").Visible=false;
        var slope=Mathf.Atan2(2.0f,6.4f);
        foreach(var sign in new[]{-1f,1f})
        {
            var rotation=new Vector3(0,0,-sign*slope);
            var roof=FacilitySolid(room,"PoliceMetalRoof"+sign,new(Mathf.Sqrt(6.4f*6.4f+4),.12f,9.8f),
                new(sign*3.2f,4.1f,0),"626b6b","roof_metal",rotation);
            roof.MaterialOverride=RuralPropMaterials.Surface("steel","68716a");
            AddVisualBox(room,"PoliceRoofSnow"+sign,new(6.66f,.025f,9.74f),new(sign*3.2f,4.174f,0),"dce3de","snow_roof").Rotation=rotation;
            AddVisualBox(room,"PoliceEaveFascia"+sign,new(.08f,.20f,9.8f),new(sign*6.38f,3.10f,0),"778274","wood_painted_trim");
        }
        using var st=new SurfaceTool();st.Begin(Mesh.PrimitiveType.Triangles);
        foreach(var sign in new[]{-1f,1f})
        {
            var a=new Vector3(-6,3.0f,sign*4.48f);var b=new Vector3(6,3.0f,sign*4.48f);var c=new Vector3(0,5.1f,sign*4.48f);
            foreach(var face in new[]{new[]{a,c,b},new[]{a,b,c}})
            {
                st.SetNormal((face[1]-face[0]).Cross(face[2]-face[0]).Normalized());
                foreach(var v in face){st.SetUV(new(v.X/2,v.Y/2));st.AddVertex(v);}
            }
        }
        st.Index();room.AddChild(new MeshInstance3D{Name="PoliceGableInfill",Mesh=st.Commit(),
            MaterialOverride=PainterlyMaterialLibrary.ForColor("c5c9be","plaster",sheltered:true)});
    }
}
