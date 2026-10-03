using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal sealed record PublicFenceMemberRecord(MeshInstance3D Mesh, ArrayMesh Source,
        Transform3D OriginalTransform, bool OriginalVisible, string Action, int SourceTriangles,
        int PublishedTriangles, int CapTriangles)
    {
        internal CollisionShape3D[] Contacts { get; set; } = Array.Empty<CollisionShape3D>();
    }
    internal sealed record PublicFenceJunctionRecord(Node3D Fence, Node3D Building,
        MeshInstance3D RearWall, Aabb WallBounds, float CutZ, PublicFenceMemberRecord[] Members);
    internal PublicFenceJunctionRecord? CouncilFenceJunction { get; private set; }

    private readonly record struct FenceCutVertex(Vector3 Point, Vector3 Normal, Vector2 Uv,
        Plane Tangent, Color Color, Vector2 Uv2)
    {
        internal FenceCutVertex Lerp(FenceCutVertex other, float t)
        {
            var normal = Normal.Lerp(other.Normal, t).Normalized();
            var tangent = Tangent.Normal.Lerp(other.Tangent.Normal, t);
            tangent = (tangent - normal * tangent.Dot(normal)).Normalized();
            return new(Point.Lerp(other.Point, t), normal, Uv.Lerp(other.Uv, t),
                new Plane(tangent, t < .5f ? Tangent.D : other.Tangent.D),
                Color.Lerp(other.Color, t), Uv2.Lerp(other.Uv2, t));
        }
    }

    private void RepairCouncilFenceJunction(PublicBuildingRoom council)
    {
        if (CouncilFenceJunction is not null || council.Id != "council"
            || council.Building.Name != "EastStreetHorizonFacade")
            throw new InvalidOperationException("The measured fence junction belongs only to the existing council annex.");
        var fence = FindDescendants<Node3D>(this).Single(node => node.Name == "EastStreetHorizonFence");
        var wall = FindDescendants<MeshInstance3D>(council.Building)
            .Single(mesh => mesh.Name.ToString().EndsWith("_SeniRear_Wall_LOD0", StringComparison.Ordinal));
        var wallBounds = PublicBuildingShell.Bounds(council.Building, wall);
        var cutZ = wallBounds.GetCenter().Z;
        var records = new List<PublicFenceMemberRecord>();
        // Council10 identifies this exact run. Its first post, two pickets and
        // first footing are entirely inside the occupied annex. End the three
        // rails within the real rear wall, retaining the whole external run.
        foreach (var mesh in FindDescendants<MeshInstance3D>(fence))
        {
            if (mesh.Mesh is not ArrayMesh source || !mesh.Name.ToString().StartsWith("FenceSegment_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected mesh in the measured council fence: " + mesh.Name);
            var toBuilding = council.Building.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            var allPoints = Enumerable.Range(0, source.GetSurfaceCount()).SelectMany(surface => SurfaceVertices(source, surface)).ToArray();
            var distances = allPoints.Select(point => (toBuilding * point).Z - cutZ).ToArray();
            var sourceTriangles = FenceTriangleCount(source);
            var action = "retained";
            var publishedTriangles = sourceTriangles;
            var capTriangles = 0;
            var wasVisible = mesh.Visible;
            if (distances.Min() >= 0)
            {
                mesh.Visible = false;
                mesh.SetMeta("retirementReason", "Council10: this exact fence member lay wholly inside the occupied seni; external run retained at the rear wall");
                action = "inside-member-hidden";
                publishedTriangles = 0;
            }
            else if (distances.Max() > 0)
            {
                if (!mesh.Name.ToString().Contains("_Rail", StringComparison.Ordinal) || source.GetSurfaceCount() != 1)
                    throw new InvalidOperationException("The measured wall junction unexpectedly crosses another fence member: " + mesh.Name);
                var (result, caps) = TrimCouncilFenceRail(source, toBuilding, cutZ);
                mesh.Mesh = result;
                action = "rail-ended-in-wall";
                capTriangles = caps;
                publishedTriangles = FenceTriangleCount(result);
            }
            if (action != "retained")
            {
                mesh.SetMeta("publicFenceJunctionRepair", action);
                mesh.SetMeta("publicFenceJunctionPlaneZ", cutZ);
                mesh.SetMeta("publicFenceJunctionWall", wall.GetPath().ToString());
            }
            records.Add(new(mesh, source, mesh.GlobalTransform, wasVisible, action,
                sourceTriangles, publishedTriangles, capTriangles));
        }
        if (records.Count(r => r.Action == "inside-member-hidden") != 4
            || records.Count(r => r.Action == "rail-ended-in-wall") != 3
            || records.Count(r => r.Action == "retained") != 8)
            throw new InvalidOperationException("The council fence no longer matches the measured four-member intrusion and three-rail junction.");
        CouncilFenceJunction = new(fence, council.Building, wall, wallBounds, cutZ, records.ToArray());
        GD.Print($"act1-council-fence-junction: fence={fence.GetPath()} wall={wall.GetPath()} localCutZ={cutZ} hiddenMembers=4 trimmedRails=3 retainedMembers=8 sourceAsset=unchanged");
    }

    private static int FenceTriangleCount(ArrayMesh mesh)
    {
        var count = 0;
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            using var arrays = mesh.SurfaceGetArrays(surface);
            var indices = arrays[(int)Mesh.ArrayType.Index];
            count += (indices.VariantType == Variant.Type.Nil || indices.AsInt32Array().Length == 0
                ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length : indices.AsInt32Array().Length) / 3;
        }
        return count;
    }

    private static (ArrayMesh Mesh, int CapTriangles) TrimCouncilFenceRail(ArrayMesh source,
        Transform3D toBuilding, float cutZ)
    {
        using var arrays = source.SurfaceGetArrays(0);
        var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normalValue = arrays[(int)Mesh.ArrayType.Normal];
        var uvValue = arrays[(int)Mesh.ArrayType.TexUV];
        var normals = normalValue.VariantType == Variant.Type.Nil ? Array.Empty<Vector3>() : normalValue.AsVector3Array();
        var uvs = uvValue.VariantType == Variant.Type.Nil ? Array.Empty<Vector2>() : uvValue.AsVector2Array();
        var tangentValue = arrays[(int)Mesh.ArrayType.Tangent];
        var colorValue = arrays[(int)Mesh.ArrayType.Color];
        var uv2Value = arrays[(int)Mesh.ArrayType.TexUV2];
        var tangents = tangentValue.VariantType == Variant.Type.Nil ? Array.Empty<float>() : tangentValue.AsFloat32Array();
        var colors = colorValue.VariantType == Variant.Type.Nil ? Array.Empty<Color>() : colorValue.AsColorArray();
        var uv2 = uv2Value.VariantType == Variant.Type.Nil ? Array.Empty<Vector2>() : uv2Value.AsVector2Array();
        var indexValue = arrays[(int)Mesh.ArrayType.Index];
        var indices = indexValue.VariantType == Variant.Type.Nil ? Array.Empty<int>() : indexValue.AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, points.Length).ToArray();
        if (points.Length == 0 || indices.Length % 3 != 0
            || (normals.Length != 0 && normals.Length != points.Length)
            || (uvs.Length != 0 && uvs.Length != points.Length)
            || (tangents.Length != 0 && tangents.Length != points.Length * 4)
            || (colors.Length != 0 && colors.Length != points.Length)
            || (uv2.Length != 0 && uv2.Length != points.Length))
            throw new InvalidOperationException($"The source rail has an incomplete present vertex channel: vertices={points.Length}, normals={normals.Length}, uv={uvs.Length}, tangents={tangents.Length}, colors={colors.Length}, uv2={uv2.Length}, indices={indices.Length}.");
        var unsupported = Mesh.ArrayFormat.FormatBones | Mesh.ArrayFormat.FormatWeights
            | Mesh.ArrayFormat.FormatCustom0 | Mesh.ArrayFormat.FormatCustom1
            | Mesh.ArrayFormat.FormatCustom2 | Mesh.ArrayFormat.FormatCustom3;
        if ((source.SurfaceGetFormat(0) & unsupported) != 0 || source.GetBlendShapeCount() != 0
            || source.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles)
            throw new InvalidOperationException("A static fence rail unexpectedly contains animated or custom vertex channels.");
        var vertices = Enumerable.Range(0, points.Length).Select(index => new FenceCutVertex(points[index],
            normals.Length == 0 ? Vector3.Zero : normals[index], uvs.Length == 0 ? Vector2.Zero : uvs[index],
            tangents.Length == 0 ? default : new Plane(
                new Vector3(tangents[index * 4], tangents[index * 4 + 1], tangents[index * 4 + 2]), tangents[index * 4 + 3]),
            colors.Length == 0 ? Colors.White : colors[index], uv2.Length == 0 ? Vector2.Zero : uv2[index])).ToArray();
        var (output, capTriangles) = ClipCouncilFenceRail(vertices, indices, toBuilding, cutZ);
        var baseIndices = Enumerable.Range(0, output.Count).ToArray();
        var lods = new global::Godot.Collections.Dictionary();
        // Copy the authored/imported levels rather than generating replacements.
        // Each level receives the same cut and closed end; its distance is unchanged.
        using var imported = ImporterMesh.FromMesh(source);
        var lodCount = imported.GetSurfaceLodCount(0);
        for (var lod = 0; lod < lodCount; lod++)
        {
            var (lodVertices, _) = ClipCouncilFenceRail(vertices, imported.GetSurfaceLodIndices(0, lod), toBuilding, cutZ);
            lods[imported.GetSurfaceLodSize(0, lod)] = Enumerable.Range(output.Count, lodVertices.Count).ToArray();
            output.AddRange(lodVertices);
        }
        var published = new global::Godot.Collections.Array();
        published.Resize((int)Mesh.ArrayType.Max);
        published[(int)Mesh.ArrayType.Vertex] = output.Select(vertex => vertex.Point).ToArray();
        // The crooked middle rail is authored without UVs. Missing channels stay
        // absent; only real source channels are interpolated and published.
        if (normals.Length != 0) published[(int)Mesh.ArrayType.Normal] = output.Select(vertex => vertex.Normal).ToArray();
        if (uvs.Length != 0) published[(int)Mesh.ArrayType.TexUV] = output.Select(vertex => vertex.Uv).ToArray();
        published[(int)Mesh.ArrayType.Index] = baseIndices;
        if (tangents.Length != 0) published[(int)Mesh.ArrayType.Tangent] = output.SelectMany(vertex =>
            new[] { vertex.Tangent.Normal.X, vertex.Tangent.Normal.Y, vertex.Tangent.Normal.Z, vertex.Tangent.D }).ToArray();
        if (colors.Length != 0) published[(int)Mesh.ArrayType.Color] = output.Select(vertex => vertex.Color).ToArray();
        if (uv2.Length != 0) published[(int)Mesh.ArrayType.TexUV2] = output.Select(vertex => vertex.Uv2).ToArray();
        var result = new ArrayMesh();
        result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, published, lods: lods);
        result.SurfaceSetMaterial(0, source.SurfaceGetMaterial(0));
        result.SurfaceSetName(0, source.SurfaceGetName(0));
        if (source.ShadowMesh is not null)
        {
            // Shadow vertices and all LOD indices use exactly the published positions.
            var shadowArrays = new global::Godot.Collections.Array();
            shadowArrays.Resize((int)Mesh.ArrayType.Max);
            shadowArrays[(int)Mesh.ArrayType.Vertex] = published[(int)Mesh.ArrayType.Vertex];
            shadowArrays[(int)Mesh.ArrayType.Index] = baseIndices;
            var shadow = new ArrayMesh();
            shadow.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, shadowArrays, lods: lods);
            result.ShadowMesh = shadow;
        }
        result.SetMeta("publicFenceSourceLodCount", lodCount);
        result.SetMeta("publicFencePublishedLodCount", lodCount);
        result.SetMeta("publicFenceSourceFormat", (long)source.SurfaceGetFormat(0));
        result.SetMeta("publicFencePublishedFormat", (long)result.SurfaceGetFormat(0));
        result.SetMeta("publicFenceSourceVertexCount", points.Length);
        result.SetMeta("publicFenceSourceNormalCount", normals.Length);
        result.SetMeta("publicFenceSourceUvCount", uvs.Length);
        GD.Print($"act1-council-fence-format: source={source.ResourceName} vertices={points.Length} normals={normals.Length} uv={uvs.Length} tangents={tangents.Length} colors={colors.Length} uv2={uv2.Length} lods={lodCount} sourceFormat={(long)source.SurfaceGetFormat(0)} publishedFormat={(long)result.SurfaceGetFormat(0)}");
        return (result, capTriangles);
    }

    private static (List<FenceCutVertex> Vertices, int CapTriangles) ClipCouncilFenceRail(
        FenceCutVertex[] vertices, int[] indices, Transform3D toBuilding, float cutZ)
    {
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new InvalidOperationException("An imported fence LOD must contain actual triangles.");
        var output = new List<FenceCutVertex>();
        var rim = new List<Vector3>();
        for (var triangle = 0; triangle < indices.Length; triangle += 3)
        {
            var input = indices.Skip(triangle).Take(3).Select(index => vertices[index]).ToArray();
            var polygon = new List<FenceCutVertex>();
            for (var index = 0; index < 3; index++)
            {
                var a = input[index]; var b = input[(index + 1) % 3];
                var da = cutZ - (toBuilding * a.Point).Z;
                var db = cutZ - (toBuilding * b.Point).Z;
                if (da >= 0) polygon.Add(a);
                if ((da >= 0) == (db >= 0)) continue;
                var intersection = a.Lerp(b, da / (da - db));
                polygon.Add(intersection);
                if (rim.All(point => point.DistanceSquaredTo(intersection.Point) > 1e-12f)) rim.Add(intersection.Point);
            }
            for (var index = 1; index + 1 < polygon.Count; index++)
            {
                if ((polygon[index].Point - polygon[0].Point).Cross(polygon[index + 1].Point - polygon[0].Point).LengthSquared() < 1e-14f) continue;
                output.Add(polygon[0]); output.Add(polygon[index]); output.Add(polygon[index + 1]);
            }
        }
        if (rim.Count < 3 || output.Count == 0)
            throw new InvalidOperationException("The measured rail cut produced no supported closed end.");
        var center = rim.Aggregate(Vector3.Zero, (sum, point) => sum + point) / rim.Count;
        var centerInBuilding = toBuilding * center;
        rim = rim.OrderBy(point => Math.Atan2((toBuilding * point).Y - centerInBuilding.Y,
            (toBuilding * point).X - centerInBuilding.X)).ToList();
        var localNormal = (toBuilding.Basis.Transposed() * Vector3.Back).Normalized();
        var localTangent = toBuilding.Basis.Inverse() * Vector3.Right;
        localTangent = (localTangent - localNormal * localTangent.Dot(localNormal)).Normalized();
        FenceCutVertex Cap(Vector3 point)
        {
            var at = toBuilding * point;
            var uv = new Vector2(at.X, at.Y);
            return new(point, localNormal, uv, new Plane(localTangent, 1), vertices[0].Color, uv);
        }
        // Every source rail is convex. A center fan closes the end in the
        // middle of the existing wall; Godot front faces use clockwise order.
        var capTriangles = 0;
        for (var index = 0; index < rim.Count; index++)
        {
            var a = rim[index]; var b = rim[(index + 1) % rim.Count];
            if ((a - center).Cross(b - center).LengthSquared() < 1e-14f) continue;
            if ((a - center).Cross(b - center).Dot(localNormal) > 0) (a, b) = (b, a);
            output.Add(Cap(center)); output.Add(Cap(a)); output.Add(Cap(b)); capTriangles++;
        }
        return (output, capTriangles);
    }

    private void FinalizeCouncilFenceJunctionContacts()
    {
        var repair = CouncilFenceJunction ?? throw new InvalidOperationException("Missing council fence junction projection.");
        var shapes = FindDescendants<CollisionShape3D>(this).Where(shape => shape.HasMeta("authoredSourceMesh")).ToArray();
        foreach (var member in repair.Members)
        {
            var contacts = shapes.Where(shape => shape.GetMeta("authoredSourceMesh").AsString() == member.Mesh.GetPath().ToString()).ToArray();
            if (member.Action == "inside-member-hidden" && contacts.Length != 0)
                throw new InvalidOperationException("An invisible intruding fence member still owns collision: " + member.Mesh.Name);
            if (member.Action == "rail-ended-in-wall")
            {
                // Keep the existing deferred collider owner and layer policy.
                // Some low rails intentionally have no generic blocker. Where
                // one was built, its old bounding box must not enter the seni.
                if (contacts.Length > 1) throw new InvalidOperationException("The trimmed rail has multiple physical owners.");
                foreach (var contact in contacts)
                {
                    var owner = contact.GetParent<Node3D>();
                    var surface = new ConcavePolygonShape3D { BackfaceCollision = true };
                    surface.SetFaces(member.Mesh.Mesh.GetFaces().Select(point => member.Mesh.GlobalBasis * point).ToArray());
                    contact.Transform = owner.GlobalTransform.AffineInverse() * new Transform3D(Basis.Identity, member.Mesh.GlobalPosition);
                    contact.Shape = surface;
                    contact.SetMeta("publicFenceJunctionContact", "same deferred owner; exact retained rail and closed wall end");
                }
            }
            member.Contacts = contacts;
        }
        GD.Print($"act1-council-fence-contacts: retainedSourceContacts={repair.Members.Sum(member => member.Contacts.Length)} hiddenMemberContacts=0 newBodyOwners=0");
    }
}
