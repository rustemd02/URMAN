using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Board joints remain visible at street distance. All joints share one small
    // mesh/material; the existing timber textures and physical shell own the wall.
    private void BuildMosqueTimberJoints(Node3D complex)
    {
        var room = _mosqueRoom!;
        using var stream = new SurfaceTool(); stream.Begin(Mesh.PrimitiveType.Triangles);
        var joints = 0;
        void Quad(Transform3D frame, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var points = new[] { a, b, c, d };
            stream.SetNormal((frame.Basis * normal).Normalized());
            // Clockwise winding, with the declared outward normal.
            var reverse = (c-a).Cross(b-a).Dot(normal) < 0;
            foreach (var index in reverse ? new[] {0,2,1,0,3,2} : new[] {0,1,2,0,2,3})
            { var p=frame*points[index]; stream.SetUV(new(p.X+p.Z,p.Y)); stream.AddVertex(p); }
            joints++;
        }
        foreach (var mesh in FindDescendants<MeshInstance3D>(room)
            .Concat(new[] {"MosqueHallWest","MosqueHallEastLeft","MosqueHallEastRight","MosqueHallEastLintel"}
                .Select(name=>complex.GetNode<MeshInstance3D>(name))).ToArray())
        {
            if (mesh.Mesh is not BoxMesh box || !mesh.Visible) continue;
            var name=mesh.Name.ToString(); var size=box.Size;
            var frame=room.GlobalTransform.AffineInverse()*mesh.GlobalTransform;
            var firstRow=Mathf.Ceil((frame.Origin.Y-size.Y*.5f+.02f)/.18f)*.18f-frame.Origin.Y;
            if (name.StartsWith("MosqueClad", StringComparison.Ordinal))
            {
                var side=mesh.Position.Z>0?1f:-1f; var z=side*(size.Z*.5f+.001f);
                for(var y=firstRow;y<size.Y*.5f-.03f;y+=.18f)
                    Quad(frame,new(-size.X*.5f,y-.003f,z),new(size.X*.5f,y-.003f,z),
                        new(size.X*.5f,y+.003f,z),new(-size.X*.5f,y+.003f,z),Vector3.Back*side);
            }
            else if(name.StartsWith("MosqueHall", StringComparison.Ordinal))
            {
                var side=name=="MosqueHallWest"?-1f:1f;var x=side*(size.X*.5f+.001f);
                for(var y=firstRow;y<size.Y*.5f-.03f;y+=.18f)
                    Quad(frame,new(x,y-.003f,-size.Z*.5f),new(x,y-.003f,size.Z*.5f),
                        new(x,y+.003f,size.Z*.5f),new(x,y+.003f,-size.Z*.5f),Vector3.Right*side);
            }
            else if(name.StartsWith("TimberShaftFacet",StringComparison.Ordinal)
                ||name.StartsWith("LanternTimberApron",StringComparison.Ordinal))
            {
                var z=size.Z*.5f+.001f;
                foreach(var x in new[]{-.27f,0f,.27f})
                    Quad(frame,new(x-.003f,-size.Y*.5f,z),new(x+.003f,-size.Y*.5f,z),
                        new(x+.003f,size.Y*.5f,z),new(x-.003f,size.Y*.5f,z),Vector3.Back);
            }
        }
        var result=new MeshInstance3D {Name="MosqueTimberBoardJoints",Mesh=stream.Commit(),
            MaterialOverride=PainterlyMaterialLibrary.ForColor("4c6154","wood_painted_green"),
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        room.AddChild(result);result.SetMeta("jointCount",joints);
        result.SetMeta("presentationRole","batched visible horizontal hall boards / vertical shaft boards; no contact, texture copies or per-board nodes");
    }
}
