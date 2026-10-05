using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // This is a derivative of the four boots on this particular indoor actor.
    // The shared character kit, rig, ankle cuff and ground anchor stay authored.
    internal sealed record MosqueFootwearRecord(MeshInstance3D Node, ArrayMesh Source, ArrayMesh Published,
        Skin Skin, NodePath Skeleton, Transform3D LocalPose, Aabb SourceBounds, float CuffY,
        byte[][] SourceArrays, float RangeBegin, float RangeEnd, float BeginMargin, float EndMargin,
        GeometryInstance3D.VisibilityRangeFadeModeEnum Fade);
    private readonly List<MosqueFootwearRecord> _mosqueFootwear = new();
    internal IReadOnlyList<MosqueFootwearRecord> MosqueFootwear => _mosqueFootwear;

    private void ConfigureMosqueTimurFootwear(Node3D timur)
    {
        if (timur.GetMeta("characterId").AsString() != "timur_hazrat" || _mosqueFootwear.Count != 0)
            throw new InvalidOperationException("The mosque footwear derivative requires its one existing Timur instance.");
        var meshes = timur.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
        var staged = new List<MosqueFootwearRecord>();
        try
        {
            foreach (var side in new[] { "Left", "Right" })
            foreach (var lod in new[] { 0, 1 })
            {
                var mesh = meshes.Single(item => item.Name == $"TimurHazrat_Boot{side}_LOD{lod}");
                if (mesh.Mesh is not ArrayMesh source || mesh.Skin is not { } skin || source.GetBlendShapeCount() != 0)
                    throw new InvalidOperationException("Timur footwear no longer has the expected authored ArrayMesh and skin.");
                var bounds = source.GetAabb();
                if (bounds.Size.Y is < .13f or > .16f || bounds.Size.X is < .14f or > .17f)
                    throw new InvalidOperationException("Timur's source boot profile changed; the indoor derivative needs review.");
                // The top 15 mm remain exactly authored underneath the trouser.
                var cuffY = bounds.End.Y - .015f;
                var snapshots = Enumerable.Range(0, source.GetSurfaceCount())
                    .Select(surface => SurfaceBytes(source, surface)).ToArray();
                var shaped = CreateIndoorFootwearMesh(source);
                staged.Add(new(mesh, source, shaped, skin, mesh.Skeleton, mesh.Transform, bounds, cuffY,
                    snapshots, mesh.VisibilityRangeBegin, mesh.VisibilityRangeEnd, mesh.VisibilityRangeBeginMargin,
                    mesh.VisibilityRangeEndMargin, mesh.VisibilityRangeFadeMode));
            }
        }
        catch
        {
            foreach (var item in staged) item.Published.Dispose();
            throw;
        }
        var cloth = PainterlyMaterialLibrary.ForColor("6d6c64", "cloth", sheltered: true);
        foreach (var item in staged)
        {
            item.Node.Mesh = item.Published;
            item.Node.MaterialOverride = cloth;
            item.Node.SetMeta("mosqueFootwear", "indoor-sock-derived-v1");
        }
        _mosqueFootwear.AddRange(staged);
        // The same one existing actor also receives the sober mosque garment
        // (Act1ConnectedWorld.MosqueImamDress.cs). It only recolours the kit
        // cloth, re-derives the authored cap and adds one collar primitive;
        // the soles above, the rig, the anchor and the interaction stay owned
        // by their existing code.
        ConfigureMosqueImamDress(timur);
    }

    // Shared geometry only: state remains with world.props, while each actor
    // keeps its own mesh instances and final skin bindings.
    internal static ArrayMesh CreateIndoorFootwearMesh(ArrayMesh source)
    {
        var bounds = source.GetAabb();
        if (source.GetBlendShapeCount() != 0 || bounds.Size.Y is < .13f or > .16f || bounds.Size.X is < .14f or > .17f)
            throw new InvalidOperationException("The indoor footwear derivative needs the reviewed authored boot profile.");
        return DeriveMosqueSock(source, bounds, bounds.End.Y - .015f);
    }

    // GD.VarToBytes copies the data; the caller-owned surface array is released here.
    private static byte[] SurfaceBytes(Mesh mesh, int surface)
    {
        using var arrays = mesh.SurfaceGetArrays(surface);
        return GD.VarToBytes(arrays);
    }

    private static ArrayMesh DeriveMosqueSock(ArrayMesh source, Aabb bounds, float cuffY, bool shadow = false)
    {
        var result = new ArrayMesh { ResourceName = source.ResourceName + "_MosqueSock", CustomAabb = source.CustomAabb };
        try
        {
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            {
                using var arrays = source.SurfaceGetArrays(surface);
                var before = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var hasNormals = arrays[(int)Mesh.ArrayType.Normal].VariantType != Variant.Type.Nil;
                var oldNormals = hasNormals ? arrays[(int)Mesh.ArrayType.Normal].AsVector3Array() : Array.Empty<Vector3>();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (source.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles || before.Length == 0
                    || (hasNormals && oldNormals.Length != before.Length) || (!hasNormals && !shadow))
                    throw new InvalidOperationException("The authored sock derivative requires triangular surfaces and the visible mesh's normals.");
                var after = before.Select(point => ShapeMosqueSock(point, bounds, cuffY)).ToArray();
                var triangles = indices.Length == 0 ? Enumerable.Range(0, before.Length).ToArray() : indices;
                if (triangles.Length % 3 != 0 || triangles.Any(index => index < 0 || index >= before.Length))
                    throw new InvalidOperationException("Invalid authored footwear indices.");
                var normals = new Vector3[before.Length];
                for (var index = 0; hasNormals && index < triangles.Length; index += 3)
                {
                    var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                    var oldCross = (before[b] - before[a]).Cross(before[c] - before[a]);
                    var reference = oldNormals[a] + oldNormals[b] + oldNormals[c];
                    var orientation = oldCross.Dot(reference);
                    if (Math.Abs(orientation) < 1e-12f) throw new InvalidOperationException("Unoriented authored footwear face.");
                    var cross = (after[b] - after[a]).Cross(after[c] - after[a]) * Math.Sign(orientation);
                    if (cross.LengthSquared() < 1e-16f) throw new InvalidOperationException("Collapsed indoor footwear face.");
                    normals[a] += cross; normals[b] += cross; normals[c] += cross;
                }
                for (var vertex = 0; hasNormals && vertex < normals.Length; vertex++)
                {
                    if (normals[vertex].LengthSquared() < 1e-16f) throw new InvalidOperationException("Unused footwear vertex.");
                    normals[vertex] = normals[vertex].Normalized();
                }
                arrays[(int)Mesh.ArrayType.Vertex] = after;
                if (hasNormals) arrays[(int)Mesh.ArrayType.Normal] = normals;
                if (arrays[(int)Mesh.ArrayType.Tangent].VariantType != Variant.Type.Nil)
                {
                    // ensure_tangents may add this channel during Godot import,
                    // even when it was absent in the source GLB. Transform the
                    // direction with the deformation Jacobian, then orthogonalize
                    // to the new normal; its original handedness is unchanged.
                    var tangents = arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
                    if (!hasNormals || tangents.Length != before.Length * 4)
                        throw new InvalidOperationException("Invalid imported footwear tangent channel.");
                    for (var vertex = 0; vertex < before.Length; vertex++)
                    {
                        var offset = vertex * 4;
                        var tangent = TransformMosqueSockDirection(before[vertex],
                            new(tangents[offset], tangents[offset + 1], tangents[offset + 2]), bounds, cuffY);
                        tangent -= normals[vertex] * tangent.Dot(normals[vertex]);
                        if (tangent.LengthSquared() < 1e-12f)
                            tangent = normals[vertex].Cross(Math.Abs(normals[vertex].Y) < .9f ? Vector3.Up : Vector3.Right);
                        tangent = tangent.Normalized();
                        tangents[offset] = tangent.X; tangents[offset + 1] = tangent.Y; tangents[offset + 2] = tangent.Z;
                    }
                    arrays[(int)Mesh.ArrayType.Tangent] = tangents;
                }
                // Preserve every other attribute, including the actual bind indices
                // and weights. Godot's additional surface LOD indices also survive.
                using var lods = MosqueSockSurfaceLods(source, surface);
                var flags = source.SurfaceGetFormat(surface) &
                    (Mesh.ArrayFormat.FlagUse8BoneWeights | Mesh.ArrayFormat.FlagUseDynamicUpdate);
                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, lods: lods, flags: flags);
                result.SurfaceSetMaterial(surface, source.SurfaceGetMaterial(surface));
                result.SurfaceSetName(surface, source.SurfaceGetName(surface));
            }
            if (!shadow && source.ShadowMesh is { } originalShadow)
                result.ShadowMesh = DeriveMosqueSock(originalShadow, bounds, cuffY, shadow: true);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private static Vector3 TransformMosqueSockDirection(Vector3 point, Vector3 direction, Aabb bounds, float cuffY)
    {
        if (point.Y >= cuffY) return direction;
        var height = point.Y - bounds.Position.Y;
        var cuffHeight = cuffY - bounds.Position.Y;
        var t = Mathf.Clamp(height / cuffHeight, 0, 1);
        var scale = Mathf.Lerp(.73f, 1f, t * t * (3 - 2 * t));
        var derivative = .27f * 6 * t * (1 - t) / cuffHeight;
        var yScale = height <= .020f ? .0025f / .020f : (cuffHeight - .0025f) / (cuffHeight - .020f);
        return new(scale * direction.X + (point.X - bounds.GetCenter().X) * derivative * direction.Y,
            yScale * direction.Y, direction.Z);
    }

    private static Vector3 ShapeMosqueSock(Vector3 point, Aabb bounds, float cuffY)
    {
        if (point.Y >= cuffY) return point;
        var height = point.Y - bounds.Position.Y;
        var cuffHeight = cuffY - bounds.Position.Y;
        const float sourceSole = .020f;
        const float textileEdge = .0025f;
        var y = height <= sourceSole ? height * (textileEdge / sourceSole)
            : textileEdge + (height - sourceSole) * (cuffHeight - textileEdge) / (cuffHeight - sourceSole);
        var widthScale = Mathf.Lerp(.73f, 1f, Mathf.SmoothStep(0, cuffHeight, height));
        return new(bounds.GetCenter().X + (point.X - bounds.GetCenter().X) * widthScale,
            bounds.Position.Y + y, point.Z);
    }

    internal static global::Godot.Collections.Dictionary MosqueSockSurfaceLods(ArrayMesh mesh, int surface)
    {
        var result = new global::Godot.Collections.Dictionary();
        using var data = RenderingServer.MeshGetSurface(mesh.GetRid(), surface);
        if (!data.TryGetValue("lods", out var rawLods)) return result;
        using var levels = rawLods.AsGodotArray();
        if (levels.Count == 0) return result;
        var count = data["index_count"].AsInt32();
        var bytes = data["index_data"].AsByteArray();
        var stride = count > 0 ? bytes.Length / count : 0;
        if (count <= 0 || bytes.Length != stride * count || stride is not (2 or 4))
            throw new InvalidOperationException("Unsupported imported footwear LOD index encoding.");
        foreach (var value in levels)
        {
            using var level = value.AsGodotDictionary();
            var packed = level["index_data"].AsByteArray();
            if (packed.Length % stride != 0) throw new InvalidOperationException("Truncated footwear LOD indices.");
            var indices = new int[packed.Length / stride];
            for (var index = 0; index < indices.Length; index++)
                indices[index] = stride == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(packed.AsSpan(index * stride, stride))
                    : checked((int)BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(index * stride, stride)));
            result[level["edge_length"].AsSingle()] = indices;
        }
        return result;
    }
}
