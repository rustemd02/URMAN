using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly record struct BabaiClipVertex(Vector3 Position, Vector3 Normal, Vector2 Uv, Color Color)
    {
        public BabaiClipVertex Between(BabaiClipVertex other, float t) => new(Position.Lerp(other.Position, t),
            Normal.Lerp(other.Normal, t).Normalized(), Uv.Lerp(other.Uv, t), Color.Lerp(other.Color, t));
    }

    // Subtract an axis-aligned opening in the supplied frame. Polygon clipping
    // keeps the original perimeter, materials and interpolated UVs; dropping
    // intersecting triangles wholesale would destroy long boards beside a hole.
    internal static void CutBabaiMeshOpening(MeshInstance3D instance, Node3D frame, Aabb opening)
    {
        var source = instance.Mesh ?? throw new System.InvalidOperationException("Window source mesh is missing.");
        var transform = instance.GetParent() is null ? instance.Transform
            : frame.GlobalTransform.AffineInverse() * instance.GlobalTransform;
        var result = new ArrayMesh();
        for (var section = 0; section < source.GetSurfaceCount(); section++)
        {
            using var arrays = source.SurfaceGetArrays(section);
            var positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            Color[] colors = arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            int[] indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            var emitted = 0;
            for (var offset = 0; offset + 2 < (indices.Length > 0 ? indices.Length : positions.Length); offset += 3)
            {
                var polygon = new List<BabaiClipVertex>();
                for (var corner = 0; corner < 3; corner++)
                {
                    var index = indices.Length > 0 ? indices[offset + corner] : offset + corner;
                    polygon.Add(new(positions[index], normals.Length > index ? normals[index] : Vector3.Up,
                        uvs.Length > index ? uvs[index] : Vector2.Zero, colors.Length > index ? colors[index] : Colors.White));
                }
                for (var plane = 0; plane < 6 && polygon.Count >= 3; plane++)
                {
                    var axis = plane / 2;
                    var minimum = plane % 2 == 0;
                    var limit = minimum ? opening.Position[axis] : opening.End[axis];
                    float Distance(BabaiClipVertex v) => minimum ? (transform * v.Position)[axis] - limit : limit - (transform * v.Position)[axis];
                    var inside = new List<BabaiClipVertex>(); var outside = new List<BabaiClipVertex>();
                    for (var edge = 0; edge < polygon.Count; edge++)
                    {
                        var a = polygon[edge]; var b = polygon[(edge + 1) % polygon.Count];
                        var da = Distance(a); var db = Distance(b);
                        (da >= 0 ? inside : outside).Add(a);
                        if ((da >= 0) != (db >= 0))
                        {
                            var crossing = a.Between(b, da / (da - db));
                            inside.Add(crossing); outside.Add(crossing);
                        }
                    }
                    for (var triangle = 1; triangle + 1 < outside.Count; triangle++)
                    foreach (var v in new[] { outside[0], outside[triangle], outside[triangle + 1] })
                    {
                        surface.SetNormal(v.Normal); surface.SetUV(v.Uv); surface.SetColor(v.Color);
                        surface.AddVertex(v.Position); emitted++;
                    }
                    polygon = inside;
                }
            }
            if (emitted == 0) continue;
            surface.Commit(result);
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, source.SurfaceGetMaterial(section));
        }
        instance.Mesh = result;
        instance.SetMeta("realWindowOpening", true);
    }
}
