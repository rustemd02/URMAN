using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private void BuildForestHorizon()
    {
        if (_forestVisual is not { } forest || _groundVisual is not { } ground)
            throw new InvalidOperationException("W01 horizon requires the existing ground and forest presentation.");
        var source = Descendants<MeshInstance3D>(ground).First(node => node.Mesh is not null);
        var mesh = source.Mesh!;
        var toForest = forest.GlobalTransform.AffineInverse() * source.GlobalTransform;
        var normalToForest = toForest.Basis.Inverse().Transposed();
        var bounds = toForest * mesh.GetAabb();
        var center = bounds.GetCenter();
        var halfX = bounds.Size.X * .5f;
        var halfZ = bounds.Size.Z * .5f;
        var edge = new Dictionary<Vector2, (Vector3 Position, Vector3 Normal, Vector2 Uv2, Color Mask)>();
        for (var surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
        {
            using var arrays = mesh.SurfaceGetArrays(surfaceIndex);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var uv2 = arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
            var colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            if (normals.Length != vertices.Length || uv2.Length != vertices.Length || colors.Length != vertices.Length)
                throw new InvalidOperationException("W01 horizon requires the authored ground normals, UV2 and snow masks.");
            for (var index = 0; index < vertices.Length; index++)
            {
                var point = toForest * vertices[index];
                if (Mathf.Abs(point.X - bounds.Position.X) > .001f
                    && Mathf.Abs(point.X - bounds.End.X) > .001f
                    && Mathf.Abs(point.Z - bounds.Position.Z) > .001f
                    && Mathf.Abs(point.Z - bounds.End.Z) > .001f)
                    continue;
                edge[new Vector2(point.X, point.Z)] =
                    (point, (normalToForest * normals[index]).Normalized(), uv2[index], colors[index]);
            }
        }

        // Keep every authored edge vertex: a coarser inner rim would cut across the ground triangles.
        var rim = edge.Values.OrderBy(vertex =>
            Mathf.Atan2(vertex.Position.Z - center.Z, vertex.Position.X - center.X)).ToArray();
        var nearLeft = edge[new Vector2(bounds.Position.X, bounds.Position.Z)];
        var nearRight = edge[new Vector2(bounds.End.X, bounds.Position.Z)];
        var farLeft = edge[new Vector2(bounds.Position.X, bounds.End.Z)];
        var uvAlongX = (nearRight.Uv2 - nearLeft.Uv2) / bounds.Size.X;
        var uvAlongZ = (farLeft.Uv2 - nearLeft.Uv2) / bounds.Size.Z;
        // Beyond the far-frustum corners for Far <= 160 m, FOV <= 90 degrees and aspect <= 32:9.
        var distances = new[] { 0f, 12f, 36f, 80f, 640f };
        var points = new Vector3[distances.Length, rim.Length];
        for (var ring = 0; ring < distances.Length; ring++)
        for (var index = 0; index < rim.Length; index++)
        {
            var original = rim[index].Position;
            var distance = distances[ring];
            var x = original.X + (original.X - center.X) / halfX * distance;
            var z = original.Z + (original.Z - center.Z) / halfZ * distance;
            var blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(distance / 80f, 0f, 1f));
            var distantHeight = 4f + 4f * Mathf.Sin(x * .014f + z * .008f)
                + 2f * Mathf.Cos(z * .027f - x * .005f)
                + 1.5f * Mathf.Sin(x * .037f + z * .019f);
            points[ring, index] = new Vector3(x, Mathf.Lerp(original.Y, distantHeight, blend), z);
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(int ring, int index)
        {
            var original = rim[index];
            var point = points[ring, index];
            surface.SetUV2(original.Uv2 + uvAlongX * (point.X - original.Position.X)
                + uvAlongZ * (point.Z - original.Position.Z));
            surface.SetColor(original.Mask.Lerp(new Color(0f, 0f, 0f, 0f),
                Mathf.Clamp(distances[ring] / 36f, 0f, 1f)));
            surface.AddVertex(point);
        }
        for (var ring = 0; ring < distances.Length - 1; ring++)
        for (var index = 0; index < rim.Length; index++)
        {
            var next = (index + 1) % rim.Length;
            Vertex(ring, index);
            Vertex(ring + 1, index);
            Vertex(ring, next);
            Vertex(ring + 1, index);
            Vertex(ring + 1, next);
            Vertex(ring, next);
        }
        surface.Index();
        surface.GenerateNormals();
        var horizonMesh = surface.Commit();
        // Preserve the imported lighting normal at the seam as well as its height and snow masks.
        using (var arrays = horizonMesh.SurfaceGetArrays(0))
        {
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (var index = 0; index < vertices.Length; index++)
                if (edge.TryGetValue(new Vector2(vertices[index].X, vertices[index].Z), out var original))
                    normals[index] = original.Normal;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            horizonMesh.ClearSurfaces();
            horizonMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        var horizon = new MeshInstance3D
        {
            Name = "ForestSnowHorizon",
            Mesh = horizonMesh,
            MaterialOverride = source.MaterialOverride,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        horizon.SetMeta("presentationOnly", true);
        horizon.SetMeta("collisionOwner", "none");
        horizon.SetMeta("navigationOwner", "none");
        forest.AddChild(horizon);
    }
}
