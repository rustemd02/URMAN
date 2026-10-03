using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>Instance-local repair of a measured authored wall. The imported asset,
/// other houses, source IDs, UVs and all faces outside the opening are retained.</summary>
internal static class PublicBuildingShell
{
    private readonly record struct Vertex(Vector3 Point, Vector3 Normal, Vector2 Uv)
    {
        internal Vertex Lerp(Vertex other, float amount) => new(Point.Lerp(other.Point, amount),
            Normal.Lerp(other.Normal, amount).Normalized(), Uv.Lerp(other.Uv, amount));
    }

    internal static Aabb Bounds(Node3D building, MeshInstance3D mesh)
    {
        var transform = building.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var box = mesh.GetAabb();
        var result = new Aabb(transform * box.Position, Vector3.Zero);
        for (var i = 0; i < 8; i++) result = result.Expand(transform * box.GetEndpoint(i));
        return result;
    }

    /// <param name="alongX">True for the seni XY wall; false for the main YZ wall.</param>
    internal static void OpenDoor(Node3D building, MeshInstance3D mesh, bool alongX,
        float lowU, float highU, float floorY, float headY)
    {
        if (mesh.Mesh is not ArrayMesh source || lowU >= highU || floorY >= headY)
            throw new InvalidOperationException("A public entrance needs the real triangle wall and a positive opening.");
        var bounds = Bounds(building, mesh);
        if (headY > bounds.End.Y + .002f)
            throw new InvalidOperationException("The entrance repair exceeds the existing wall height.");
        var toBuilding = building.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var fromBuilding = toBuilding.AffineInverse();
        var result = new ArrayMesh();
        var removedArea = 0f;
        for (var surfaceIndex = 0; surfaceIndex < source.GetSurfaceCount(); surfaceIndex++)
        {
            using var arrays = source.SurfaceGetArrays(surfaceIndex);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (indices.Length == 0) indices = Enumerable.Range(0, points.Length).ToArray();
            var output = new List<Vertex>();
            for (var index = 0; index < indices.Length; index += 3)
            {
                var triangle = indices.Skip(index).Take(3).Select(i => new Vertex(toBuilding * points[i],
                    normals.Length == points.Length ? normals[i] : Vector3.Up,
                    uvs.Length == points.Length ? uvs[i] : Vector2.Zero)).ToList();
                var remaining = triangle;
                foreach (var (axis, boundary, direction) in new[]
                {
                    (alongX ? 0 : 2, lowU, 1f), (alongX ? 0 : 2, highU, -1f),
                    (1, floorY, 1f), (1, headY, -1f)
                })
                {
                    var outside = Clip(remaining, axis, boundary, -direction);
                    Triangulate(outside, output);
                    remaining = Clip(remaining, axis, boundary, direction);
                    if (remaining.Count < 3) break;
                }
                for (var i = 1; i + 1 < remaining.Count; i++)
                    removedArea += (remaining[i].Point - remaining[0].Point)
                        .Cross(remaining[i + 1].Point - remaining[0].Point).Length() * .5f;
            }
            if (output.Count == 0) continue;
            using var builder = new SurfaceTool();
            builder.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var vertex in output)
            {
                builder.SetNormal(vertex.Normal);
                builder.SetUV(vertex.Uv);
                builder.AddVertex(fromBuilding * vertex.Point);
            }
            builder.SetMaterial(source.SurfaceGetMaterial(surfaceIndex));
            builder.Commit(result);
        }

        // The new cut is a real 20cm reveal, so it has visible thickness and
        // receives exactly the same triangle contact as the retained wall.
        var minDepth = alongX ? bounds.Position.Z : bounds.Position.X;
        var maxDepth = alongX ? bounds.End.Z : bounds.End.X;
        Vector3 At(float u, float y, float depth) => alongX ? new(u, y, depth) : new(depth, y, u);
        using (var reveals = new SurfaceTool())
        {
            reveals.Begin(Mesh.PrimitiveType.Triangles);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                var vertices = new[] { a, b, c, a, c, d };
                if ((b - a).Cross(c - a).Dot(normal) > 0)
                    vertices = new[] { a, c, b, a, d, c };
                foreach (var point in vertices)
                {
                    reveals.SetNormal((toBuilding.Basis.Transposed() * normal).Normalized());
                    reveals.SetUV(new(alongX ? point.X : point.Z, point.Y));
                    reveals.AddVertex(fromBuilding * point);
                }
            }
            Quad(At(lowU, floorY, minDepth), At(lowU, headY, minDepth),
                At(lowU, headY, maxDepth), At(lowU, floorY, maxDepth),
                alongX ? Vector3.Right : Vector3.Back);
            Quad(At(highU, floorY, minDepth), At(highU, floorY, maxDepth),
                At(highU, headY, maxDepth), At(highU, headY, minDepth),
                alongX ? Vector3.Left : Vector3.Forward);
            Quad(At(lowU, headY, minDepth), At(highU, headY, minDepth),
                At(highU, headY, maxDepth), At(lowU, headY, maxDepth), Vector3.Down);
            reveals.SetMaterial(source.SurfaceGetMaterial(0));
            reveals.Commit(result);
        }
        if (removedArea < .001f)
            throw new InvalidOperationException("The proposed public entrance did not remove any measured wall surface.");
        mesh.Mesh = result;
        mesh.SetMeta("publicEntranceRepair", true);
        mesh.SetMeta("publicEntranceSourceBounds", bounds);
        mesh.SetMeta("publicEntranceCutRect", new Vector4(lowU, floorY, highU, headY));
        mesh.SetMeta("publicEntranceRemovedArea", removedArea);
        mesh.SetMeta("contactPolicy", "retained authored faces plus new reveal; existing AuthoredSurfaceContact builder");
    }

    private static List<Vertex> Clip(List<Vertex> polygon, int axis, float boundary, float direction)
    {
        var result = new List<Vertex>();
        if (polygon.Count == 0) return result;
        static float Coordinate(Vector3 point, int component) => component == 0 ? point.X : component == 1 ? point.Y : point.Z;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var da = (Coordinate(a.Point, axis) - boundary) * direction;
            var db = (Coordinate(b.Point, axis) - boundary) * direction;
            var aInside = da >= 0f;
            var bInside = db >= 0f;
            if (aInside) result.Add(a);
            if (aInside != bInside) result.Add(a.Lerp(b, da / (da - db)));
        }
        return result;
    }

    private static void Triangulate(List<Vertex> polygon, List<Vertex> output)
    {
        for (var i = 1; i + 1 < polygon.Count; i++)
        {
            if ((polygon[i].Point - polygon[0].Point).Cross(polygon[i + 1].Point - polygon[0].Point).LengthSquared() < 1e-12f) continue;
            output.Add(polygon[0]); output.Add(polygon[i]); output.Add(polygon[i + 1]);
        }
    }
}
