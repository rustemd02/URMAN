using Godot;
using System.Buffers.Binary;
using System.Text.Json;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // A/B pilot only. Four opaque, immobile surround members retain the culling
    // footprint of one window; glass, backing cards and every physical owner stay.
    private void BatchPaintedWindowSurrounds(Node3D presentation)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_WINDOW_BATCH_PILOT") != "1"
            || presentation.HasMeta("windowBatchPilotCompleted")) return;
        var material = PainterlyMaterialLibrary.ForColor("b8b9b4", "wood_painted_trim");
        var contacts = FindDescendants<CollisionShape3D>(this).Where(shape => shape.HasMeta("authoredSourceMesh"))
            .Select(shape => shape.GetMeta("authoredSourceMesh").AsString()).ToHashSet(StringComparer.Ordinal);
        var cache = new Dictionary<(ulong Rid, bool OriginalShadow), WindowBatchChannels>();
        var readRefusals = new Dictionary<(ulong Rid, bool OriginalShadow), WindowBatchRefusal>();
        var merged = new Dictionary<WindowBatchMeshKey, (ArrayMesh Render, ArrayMesh Shadow)>();
        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        var refusalExamples = new Dictionary<string, object>(StringComparer.Ordinal);
        var groups = FindDescendants<MeshInstance3D>(presentation)
            .Where(mesh => mesh.HasMeta("windowSurroundPaint") && WindowSurroundStem(mesh.Name.ToString()) is not null)
            .GroupBy(mesh => (Parent: mesh.GetParent(), Stem: WindowSurroundStem(mesh.Name.ToString())!))
            .ToArray();
        var batches = 0; var sourceTriangles = 0; var shadowTriangles = 0;
        var maxFootprintGrowth = 0f;
        foreach (var group in groups)
        {
            var members = group.OrderBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal).ToArray();
            MeshInstance3D? batch = null; ArrayMesh? published = null; ArrayMesh? shadow = null;
            var ownsUnpublishedResources = false;
            try
            {
                if (group.Key.Parent is not Node3D parent || members.Length != 4
                    || members.Select(mesh => mesh.Name.ToString()[group.Key.Stem.Length..]).Distinct().Count() != 4)
                    throw new WindowBatchRefusal("incomplete-four-member-window");
                foreach (var member in members) CheckWindowBatchMember(member, members[0], material, contacts);
                var originals = members.Select(member => (ArrayMesh)member.Mesh!).ToArray();
                WindowBatchChannels Channels(ArrayMesh source, bool originalShadow)
                {
                    var readKey = (source.GetRid().Id, originalShadow);
                    if (readRefusals.TryGetValue(readKey, out var refusal)) throw refusal;
                    if (!cache.TryGetValue(readKey, out var value))
                    {
                        try { cache[readKey] = value = ReadWindowBatchChannels(source, originalShadow); }
                        catch (WindowBatchRefusal error) { readRefusals[readKey] = error; throw; }
                    }
                    return value;
                }
                var visibleInputs = originals.Select(source => Channels(source, false)).ToArray();
                var shadowInputs = originals.Select(source => Channels(source.ShadowMesh
                    ?? throw new WindowBatchRefusal("missing-original-shadow-mesh"), true)).ToArray();
                var transforms = members.Select(member => member.Transform).ToArray();
                var keyParts = originals.Select((source, index) => new WindowBatchPartKey(source.GetRid().Id, transforms[index])).ToArray();
                var key = new WindowBatchMeshKey(keyParts[0], keyParts[1], keyParts[2], keyParts[3], material.GetRid().Id);
                if (merged.TryGetValue(key, out var shared)) (published, shadow) = shared;
                else
                {
                    // Preserve the importer's resource reuse across placements;
                    // unique per-house copies would defeat renderer instancing.
                    ownsUnpublishedResources = true;
                    published = MergeWindowBatchChannels(visibleInputs, transforms);
                    shadow = MergeWindowBatchChannels(shadowInputs, transforms);
                    published.ShadowMesh = shadow;
                    published.SurfaceSetMaterial(0, material);
                }
                var visibleCheck = CheckWindowBatchReadback(members, visibleInputs, published, parent.GlobalTransform, "render");
                var shadowCheck = CheckWindowBatchReadback(members, shadowInputs, shadow, parent.GlobalTransform, "original-shadow");
                batch = new MeshInstance3D
                {
                    Name = group.Key.Stem + "_SurroundBatch", Mesh = published, MaterialOverride = material,
                    Visible = false, Layers = members[0].Layers, CastShadow = members[0].CastShadow,
                    GIMode = members[0].GIMode, GILightmapTexelScale = members[0].GILightmapTexelScale,
                    LodBias = members[0].LodBias, IgnoreOcclusionCulling = members[0].IgnoreOcclusionCulling
                };
                parent.AddChild(batch);
                if (batch.GlobalTransform != parent.GlobalTransform
                    || batch.GetActiveMaterial(0)?.GetRid() != material.GetRid()
                    || members.Where((member, index) => member.Transform != transforms[index]
                        || member.Mesh != originals[index] || !member.IsVisibleInTree()).Any())
                    throw new WindowBatchRefusal("publication-changed-source-or-parent-pose");
                var sourcePaths = members.Select(member => member.GetPath().ToString()).ToArray();
                var sourceIds = members.Select(member => member.GetInstanceId()).ToArray();
                // VIS-060: a batch may only ever cover the culling footprint of the
                // pieces it replaces. If the merged mesh reached past the union of
                // its four sources, the window would be culled as one larger box
                // and panes would start vanishing at the frame edge - the exact
                // failure this card is gated on, so it is refused here rather than
                // discovered in a capture. The merged geometry is a concatenation in
                // the same parent space, so a real growth is always a defect.
                var union = ParentSpaceBox(parent, members[0]);
                foreach (var member in members.Skip(1)) union = union.Merge(ParentSpaceBox(parent, member));
                var footprint = published.GetAabb();
                var growth = Math.Max(Math.Max(
                        footprint.End.X - union.Position.X - union.Size.X,
                        union.Position.X - footprint.Position.X),
                    Math.Max(Math.Max(footprint.End.Y - union.Position.Y - union.Size.Y,
                        union.Position.Y - footprint.Position.Y),
                        Math.Max(footprint.End.Z - union.Position.Z - union.Size.Z,
                            union.Position.Z - footprint.Position.Z)));
                if (!float.IsFinite(growth) || growth > .001f)
                    throw new WindowBatchRefusal("batched-culling-footprint-grew", new
                    {
                        sourceUnion = union.ToString(), published = footprint.ToString(),
                        growthMillimetres = growth * 1000f, window = group.Key.Stem
                    });
                maxFootprintGrowth = Math.Max(maxFootprintGrowth, growth * 1000f);
                batch.SetMeta("windowSurroundPaint", "b8b9b4");
                batch.SetMeta("windowBatchPilot", true);
                batch.SetMeta("windowBatchCullingFootprint", union.ToString());
                batch.SetMeta("windowBatchCullingGrowthMillimetres", growth * 1000f);
                batch.SetMeta("windowBatchSourcePaths", sourcePaths);
                batch.SetMeta("windowBatchSourceInstanceIds", string.Join("|", sourceIds));
                batch.SetMeta("windowBatchOriginalMeshRids", string.Join("|", originals.Select(mesh => mesh.GetRid().Id)));
                batch.SetMeta("presentationOwnership", "presentation-only; exact four-part window batch");
                batch.SetMeta("windowBatchGeometryAudit", JsonSerializer.Serialize(new
                {
                    visibleCheck, shadowCheck, materialBefore = material.GetRid().Id,
                    materialAfter = batch.GetActiveMaterial(0)!.GetRid().Id,
                    sourcePaths, sourceIds, publishedInstance = batch.GetInstanceId(),
                    originalParent = parent.GetPath().ToString(), parentPose = parent.GlobalTransform.ToString(),
                    sourceFormats = originals.Select(mesh => (long)mesh.SurfaceGetFormat(0)).ToArray(),
                    publishedFormat = (long)published.SurfaceGetFormat(0),
                    sourceShadowFormats = originals.Select(mesh => (long)mesh.ShadowMesh!.SurfaceGetFormat(0)).ToArray(),
                    publishedShadowFormat = (long)shadow.SurfaceGetFormat(0),
                    importedLods = 0, shadowSource = "original ShadowMesh arrays; no generated proxy",
                    shadowReads = shadowInputs.Select(input => input.ReadMethod).ToArray(),
                    culling = "one window AABB; four original pieces keep their triangle order and channels",
                    cullingUnionOfSources = union.ToString(), publishedCullingBounds = footprint.ToString(),
                    cullingGrowthMillimetres = growth * 1000f,
                    surroundInstancesBefore = members.Length, surroundInstancesAfter = 1
                }));
                merged.TryAdd(key, (published, shadow));
                ownsUnpublishedResources = false;
                // Publish only after both render resources pass readback. Original
                // nodes, Mesh objects, transforms and existing metadata remain valid.
                batch.Visible = true;
                foreach (var member in members)
                {
                    member.SetMeta("windowBatchOwner", batch.GetPath().ToString());
                    member.SetMeta("windowBatchOriginalVisible", member.Visible);
                    member.Visible = false;
                }
                batches++; sourceTriangles += visibleCheck.Triangles; shadowTriangles += shadowCheck.Triangles;
                GD.Print("act1-window-batch: " + JsonSerializer.Serialize(new
                {
                    path = batch.GetPath().ToString(), sourceIds, publishedInstance = batch.GetInstanceId(),
                    visibleCheck, shadowCheck, material = material.GetRid().Id,
                    meshRid = published.GetRid().Id, shadowMeshRid = shadow.GetRid().Id,
                    shadowReads = shadowInputs.Select(input => input.ReadMethod).ToArray()
                }));
            }
            catch (WindowBatchRefusal refusal)
            {
                if (batch is not null && GodotObject.IsInstanceValid(batch)) batch.Free();
                if (ownsUnpublishedResources) { published?.Dispose(); shadow?.Dispose(); }
                skipped[refusal.Message] = skipped.GetValueOrDefault(refusal.Message) + 1;
                if (refusalExamples.Count < 8 && !refusalExamples.ContainsKey(refusal.Message))
                    refusalExamples[refusal.Message] = new
                    {
                        parent = group.Key.Parent.GetPath().ToString(), window = group.Key.Stem,
                        details = refusal.Details
                    };
            }
        }
        presentation.SetMeta("windowBatchPilotCompleted", true);
        presentation.SetMeta("windowBatchPilotCount", batches);
        GD.Print("act1-window-batch-summary: " + JsonSerializer.Serialize(new
        {
            scope = "opt-in exact four-part windows; not performance acceptance", groups = groups.Length,
            batches, hiddenOriginals = batches * 4, sourceTriangles, shadowTriangles,
            surroundInstancesBefore = batches * 4, surroundInstancesAfter = batches,
            maximumCullingGrowthMillimetres = maxFootprintGrowth,
            pilotEnvironmentVariable = "URMAN_WINDOW_BATCH_PILOT",
            pilotActive = System.Environment.GetEnvironmentVariable("URMAN_WINDOW_BATCH_PILOT") == "1",
            importedResourceCount = cache.Count, publishedSharedMeshes = merged.Count, skipped, refusalExamples,
            originalPackedShadowResources = cache.Values.Count(value => value.ReadMethod == "original-shadow-unorm16-buffer"),
            packedShadowReadExamples = cache.Values.Where(value => value.PackedShadowRead is not null)
                .Take(8).Select(value => value.PackedShadowRead).ToArray(),
            unchangedCollisionOwners = true
        }));
    }

    /// <summary>A member's world bounds expressed in the batch parent's own space:
    /// the space the merged mesh and therefore its culling AABB live in.</summary>
    private static Aabb ParentSpaceBox(Node3D parent, MeshInstance3D member)
        => parent.GlobalTransform.AffineInverse() * (member.GlobalTransform * member.GetAabb());

    private static string? WindowSurroundStem(string name)
    {
        foreach (var suffix in new[] { "_Jamb1_LOD0", "_Jamb-1_LOD0", "_Rail1_LOD0", "_Rail-1_LOD0" })
            if (name.Contains("Window", StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        return null;
    }

    private static void CheckWindowBatchMember(MeshInstance3D mesh, MeshInstance3D first, Material material,
        HashSet<string> contacts)
    {
        if (mesh.Mesh is not ArrayMesh || mesh.Skin is not null || mesh.GetChildCount() != 0
            || contacts.Contains(mesh.GetPath().ToString())) throw new WindowBatchRefusal("non-static-or-physical-source");
        if (!mesh.Visible || !mesh.IsVisibleInTree()) throw new WindowBatchRefusal("source-not-visible");
        if (mesh.MaterialOverride != material || mesh.GetActiveMaterial(0) != material || mesh.MaterialOverlay is not null
            || material.NextPass is not null || material is not ShaderMaterial shader || shader.Shader is null
            || shader.Shader.Code.Contains("instance uniform", StringComparison.Ordinal)
            || shader.GetShaderParameter("local_wood_texture").AsBool()
            || shader.GetShaderParameter("has_snow_micro").AsBool()
            || shader.GetShaderParameter("trample_ground_surface").AsBool()
            || shader.GetShaderParameter("wind_sway").AsSingle() != 0f)
            throw new WindowBatchRefusal("material-or-instance-override");
        if (mesh.Layers != first.Layers || mesh.CastShadow != first.CastShadow || mesh.GIMode != first.GIMode
            || mesh.GILightmapTexelScale != first.GILightmapTexelScale || mesh.LodBias != first.LodBias
            || mesh.IgnoreOcclusionCulling != first.IgnoreOcclusionCulling || mesh.Transparency != 0
            || mesh.CustomAabb != new Aabb() || mesh.ExtraCullMargin != 0 || !mesh.VisibilityParent.IsEmpty
            || mesh.VisibilityRangeBegin != 0 || mesh.VisibilityRangeEnd != 0
            || mesh.VisibilityRangeBeginMargin != 0 || mesh.VisibilityRangeEndMargin != 0
            || mesh.VisibilityRangeFadeMode != GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled)
            throw new WindowBatchRefusal("different-render-or-visibility-settings");
        if (!mesh.Transform.IsFinite() || mesh.Transform.Basis.Determinant() <= .0000001f)
            throw new WindowBatchRefusal("singular-or-reflected-transform");
        var basis = mesh.Transform.Basis; var scale = basis.X.Length();
        if (Math.Abs(basis.Y.Length() - scale) > scale * .00001f || Math.Abs(basis.Z.Length() - scale) > scale * .00001f
            || Math.Abs(basis.X.Dot(basis.Y)) > scale * scale * .00001f
            || Math.Abs(basis.X.Dot(basis.Z)) > scale * scale * .00001f
            || Math.Abs(basis.Y.Dot(basis.Z)) > scale * scale * .00001f)
            throw new WindowBatchRefusal("non-uniform-or-sheared-local-basis");
    }

    private sealed class WindowBatchRefusal(string reason, object? details = null) : Exception(reason)
    {
        public object? Details { get; } = details;
    }
    private readonly record struct WindowBatchPartKey(ulong Mesh, Transform3D Transform);
    private sealed record WindowBatchMeshKey(WindowBatchPartKey First, WindowBatchPartKey Second,
        WindowBatchPartKey Third, WindowBatchPartKey Fourth, ulong Material);
    private sealed record WindowBatchChannels(Vector3[] Points, Vector3[] Normals, float[] Tangents,
        Vector2[] Uv, Vector2[] Uv2, Color[] Colors, int[] Indices, string ReadMethod = "surface-arrays",
        WindowPackedShadowRead? PackedShadowRead = null)
    {
        public int Layout => (Normals.Length > 0 ? 1 : 0) | (Tangents.Length > 0 ? 2 : 0)
            | (Uv.Length > 0 ? 4 : 0) | (Uv2.Length > 0 ? 8 : 0) | (Colors.Length > 0 ? 16 : 0);
    }

    private static WindowBatchChannels ReadWindowBatchChannels(ArrayMesh mesh, bool originalShadow = false)
    {
        if (mesh.GetSurfaceCount() != 1 || mesh.GetBlendShapeCount() != 0
            || mesh.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles)
            throw new WindowBatchRefusal("surface-topology-or-blend-shapes");
        var unsupported = Mesh.ArrayFormat.FlagUse2DVertices | Mesh.ArrayFormat.FlagUsesEmptyVertexArray
            | Mesh.ArrayFormat.FormatBones | Mesh.ArrayFormat.FormatWeights
            | Mesh.ArrayFormat.FormatCustom0 | Mesh.ArrayFormat.FormatCustom1
            | Mesh.ArrayFormat.FormatCustom2 | Mesh.ArrayFormat.FormatCustom3;
        var format = mesh.SurfaceGetFormat(0);
        if ((format & unsupported) != 0)
            throw new WindowBatchRefusal("custom-or-skin-format");
        // Use the original rendering surface for LOD metadata. ImporterMesh.FromMesh
        // decodes arrays first and is unsafe for the compressed position-only case below.
        using var raw = RenderingServer.MeshGetSurface(mesh.GetRid(), 0);
        if (!raw.TryGetValue("format", out var rawFormat) || rawFormat.VariantType != Variant.Type.Int
            || !raw.TryGetValue("vertex_count", out var countValue) || countValue.VariantType != Variant.Type.Int
            || countValue.AsInt64() is <= 0 or > 4096
            || !raw.TryGetValue("vertex_data", out var vertexData) || vertexData.VariantType != Variant.Type.PackedByteArray
            || vertexData.AsByteArray().Length == 0)
            throw new WindowBatchRefusal("missing-or-unbounded-raw-surface", new
            {
                path = mesh.ResourcePath, rid = mesh.GetRid().Id, format = (long)format,
                rawKeys = raw.Keys.Select(key => key.AsString()).ToArray()
            });
        var versionMask = ((ulong)RenderingServer.ArrayFormat.FlagFormatVersionMask
            << (int)RenderingServer.ArrayFormat.FlagFormatVersionShift);
        if (((ulong)rawFormat.AsInt64() & ~versionMask) != ((ulong)format & ~versionMask)
            || ((ulong)rawFormat.AsInt64() & versionMask) != (ulong)RenderingServer.ArrayFormat.FlagFormatCurrentVersion)
            throw new WindowBatchRefusal("raw-surface-version-or-semantic-format-differs", new
            {
                path = mesh.ResourcePath, rid = mesh.GetRid().Id, format = (long)format,
                rawFormat = rawFormat.AsInt64(), originalShadow
            });
        if (raw.TryGetValue("lods", out var lods))
        {
            if (lods.VariantType != Variant.Type.Array) throw new WindowBatchRefusal("unknown-raw-lod-layout");
            using var levels = lods.AsGodotArray();
            if (levels.Count != 0) throw new WindowBatchRefusal("source-has-imported-lod");
        }
        using var arrays = mesh.SurfaceGetArrays(0);
        if (arrays.Count != (int)Mesh.ArrayType.Max)
            throw new WindowBatchRefusal("unexpected-surface-array-count");
        var readMethod = "surface-arrays";
        WindowPackedShadowRead? packedRead = null;
        if (arrays[(int)Mesh.ArrayType.Vertex].VariantType == Variant.Type.Nil && originalShadow)
        {
            arrays[(int)Mesh.ArrayType.Vertex] = ReadPackedWindowShadowPositions(mesh, raw, arrays, out var decodedRead);
            packedRead = decodedRead;
            readMethod = "original-shadow-unorm16-buffer";
        }
        if (arrays[(int)Mesh.ArrayType.Vertex].VariantType != Variant.Type.PackedVector3Array)
            throw new WindowBatchRefusal("non-3d-or-unknown-vertex-array", new
            {
                path = mesh.ResourcePath, rid = mesh.GetRid().Id, originalShadow,
                format = (long)format, rawFormat = rawFormat.AsInt64(), rawVertices = countValue.AsInt64(),
                vertexBytes = vertexData.AsByteArray().Length,
                arrayTypes = arrays.Select(value => value.VariantType.ToString()).ToArray()
            });
        foreach (var (slot, type) in new[] {
                     (Mesh.ArrayType.Normal, Variant.Type.PackedVector3Array),
                     (Mesh.ArrayType.Tangent, Variant.Type.PackedFloat32Array),
                     (Mesh.ArrayType.TexUV, Variant.Type.PackedVector2Array),
                     (Mesh.ArrayType.TexUV2, Variant.Type.PackedVector2Array),
                     (Mesh.ArrayType.Color, Variant.Type.PackedColorArray),
                     (Mesh.ArrayType.Index, Variant.Type.PackedInt32Array) })
            if (arrays[(int)slot].VariantType != Variant.Type.Nil && arrays[(int)slot].VariantType != type)
                throw new WindowBatchRefusal("unexpected-packed-channel-type");
        var allowed = new[] { Mesh.ArrayType.Vertex, Mesh.ArrayType.Normal, Mesh.ArrayType.Tangent,
            Mesh.ArrayType.Color, Mesh.ArrayType.TexUV, Mesh.ArrayType.TexUV2, Mesh.ArrayType.Index };
        for (var slot = 0; slot < arrays.Count; slot++)
            if (!allowed.Contains((Mesh.ArrayType)slot) && arrays[slot].VariantType != Variant.Type.Nil)
                throw new WindowBatchRefusal("unknown-custom-or-skin-channel");
        bool Has(Mesh.ArrayType channel) => arrays[(int)channel].VariantType != Variant.Type.Nil;
        var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = Has(Mesh.ArrayType.Normal) ? arrays[(int)Mesh.ArrayType.Normal].AsVector3Array() : [];
        var tangents = Has(Mesh.ArrayType.Tangent) ? arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array() : [];
        var uv = Has(Mesh.ArrayType.TexUV) ? arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array() : [];
        var uv2 = Has(Mesh.ArrayType.TexUV2) ? arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array() : [];
        var colors = Has(Mesh.ArrayType.Color) ? arrays[(int)Mesh.ArrayType.Color].AsColorArray() : [];
        var indices = Has(Mesh.ArrayType.Index) ? arrays[(int)Mesh.ArrayType.Index].AsInt32Array() : [];
        if (indices.Length == 0) indices = Enumerable.Range(0, points.Length).ToArray();
        if (points.Length == 0 || points.Length > 4096 || indices.Length == 0 || indices.Length % 3 != 0
            || indices.Any(index => index < 0 || index >= points.Length) || points.Any(point => !point.IsFinite())
            || normals.Length != 0 && normals.Length != points.Length
            || tangents.Length != 0 && tangents.Length != points.Length * 4
            || uv.Length != 0 && uv.Length != points.Length || uv2.Length != 0 && uv2.Length != points.Length
            || colors.Length != 0 && colors.Length != points.Length)
            throw new WindowBatchRefusal("invalid-or-unbounded-vertex-channels");
        if (normals.Any(normal => !normal.IsFinite()) || tangents.Any(value => !float.IsFinite(value))
            || uv.Any(value => !value.IsFinite()) || uv2.Any(value => !value.IsFinite())
            || colors.Any(value => !float.IsFinite(value.R) || !float.IsFinite(value.G)
                || !float.IsFinite(value.B) || !float.IsFinite(value.A)))
            throw new WindowBatchRefusal("non-finite-vertex-channel");
        if (points.Length != countValue.AsInt64()) throw new WindowBatchRefusal("raw-and-decoded-vertex-count-differ");
        return new(points, normals, tangents, uv, uv2, colors, indices, readMethod, packedRead);
    }

    private sealed record WindowPackedShadowRead(string SourceRole, string Path, ulong Rid, long PublicFormat,
        long RawFormat, int VertexCount, int IndexCount, int VertexBytes, int IndexBytes, int DecodedVertices, string Bounds);

    private static Vector3[] ReadPackedWindowShadowPositions(ArrayMesh mesh,
        global::Godot.Collections.Dictionary raw, global::Godot.Collections.Array arrays, out WindowPackedShadowRead read)
    {
        // Godot a13da4feb servers/rendering/rendering_server.cpp:1448-1456 fills
        // compressed positions without normals, then continues before ret[i] = arr_3d
        // at 1497. Decode only that exact layout from this ORIGINAL shadow's buffer.
        // This is not a shadow reconstructed from the visible mesh or a new proxy.
        var format = (RenderingServer.ArrayFormat)raw["format"].AsInt64();
        var channels = (ulong)format & ((1UL << (int)Mesh.ArrayType.Max) - 1);
        var expectedChannels = (ulong)(Mesh.ArrayFormat.FormatVertex | Mesh.ArrayFormat.FormatIndex);
        var allowedFormat = expectedChannels | (ulong)RenderingServer.ArrayFormat.FlagCompressAttributes
            | ((ulong)RenderingServer.ArrayFormat.FlagFormatVersionMask
                << (int)RenderingServer.ArrayFormat.FlagFormatVersionShift);
        var vertexCount = raw["vertex_count"].AsInt32();
        if (!BitConverter.IsLittleEndian || channels != expectedChannels
            || ((ulong)format & ~allowedFormat) != 0
            || (format & RenderingServer.ArrayFormat.FlagCompressAttributes) == 0
            || !raw.TryGetValue("aabb", out var boxValue) || boxValue.VariantType != Variant.Type.Aabb
            || !raw.TryGetValue("index_data", out var indexData) || indexData.VariantType != Variant.Type.PackedByteArray
            || !raw.TryGetValue("index_count", out var countValue) || countValue.VariantType != Variant.Type.Int
            || countValue.AsInt64() is <= 0 or > 24576
            || arrays[(int)Mesh.ArrayType.Index].VariantType != Variant.Type.PackedInt32Array)
            throw new WindowBatchRefusal("unsupported-packed-shadow-layout", new
            {
                path = mesh.ResourcePath, rid = mesh.GetRid().Id, format = (long)format,
                vertexCount, arrayTypes = arrays.Select(value => value.VariantType.ToString()).ToArray()
            });
        var stride = RenderingServer.MeshSurfaceGetFormatVertexStride(format, vertexCount);
        var offset = RenderingServer.MeshSurfaceGetFormatOffset(format, vertexCount, (int)Mesh.ArrayType.Vertex);
        var indexStride = RenderingServer.MeshSurfaceGetFormatIndexStride(format, vertexCount);
        var bytes = raw["vertex_data"].AsByteArray(); var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        var indexBytes = indexData.AsByteArray(); var box = boxValue.AsAabb();
        if (stride != 8 || offset != 0 || indexStride != 2 || bytes.Length != vertexCount * 8
            || indices.Length != countValue.AsInt64() || indexBytes.Length != indices.Length * 2
            || !box.Position.IsFinite() || !box.Size.IsFinite()
            || box.Size.X < 0 || box.Size.Y < 0 || box.Size.Z < 0
            || new[] { "attribute_data", "skin_data", "blend_shape_data" }.Any(key => raw.ContainsKey(key)))
            throw new WindowBatchRefusal("packed-shadow-buffer-layout-differs", new
            {
                sourceRole = "original-shadow", path = mesh.ResourcePath, rid = mesh.GetRid().Id,
                format = (long)format, vertexCount, indexCount = countValue.AsInt64(), stride, offset, indexStride,
                vertexBytes = bytes.Length, indexBytes = indexBytes.Length, bounds = box.ToString()
            });
        for (var i = 0; i < indices.Length; i++)
            if (indices[i] != BinaryPrimitives.ReadUInt16LittleEndian(indexBytes.AsSpan(i * 2, 2)))
                throw new WindowBatchRefusal("original-shadow-index-readback-differs");
        var points = new Vector3[vertexCount];
        for (var i = 0; i < points.Length; i++)
        {
            float Coordinate(int axis) => (float)(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 8 + axis * 2, 2)) / 65535.0);
            points[i] = new Vector3(Coordinate(0), Coordinate(1), Coordinate(2)) * box.Size + box.Position;
        }
        read = new("original-shadow", mesh.ResourcePath, mesh.GetRid().Id, (long)mesh.SurfaceGetFormat(0),
            (long)format, vertexCount, indices.Length, bytes.Length, indexBytes.Length, points.Length, box.ToString());
        return points;
    }

    private static ArrayMesh MergeWindowBatchChannels(WindowBatchChannels[] inputs, Transform3D[] transforms)
    {
        if (inputs.Any(input => input.Layout != inputs[0].Layout)) throw new WindowBatchRefusal("different-vertex-layouts");
        var points = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<float>();
        var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var colors = new List<Color>(); var indices = new List<int>();
        for (var part = 0; part < inputs.Length; part++)
        {
            var input = inputs[part]; var transform = transforms[part]; var offset = points.Count;
            points.AddRange(input.Points.Select(point => transform * point));
            var normalBasis = transform.Basis.Inverse().Transposed();
            normals.AddRange(input.Normals.Select(normal => (normalBasis * normal).Normalized()));
            // Tangents ride with the surface frame, exactly like normals: the
            // merged batch is verified in each member's own world frame, so a
            // plain Basis rotation here breaks the readback on rotated parts.
            var tangentBasis = transform.Basis.Inverse().Transposed();
            for (var i = 0; i < input.Tangents.Length; i += 4)
            {
                var tangent = (tangentBasis * new Vector3(input.Tangents[i], input.Tangents[i + 1], input.Tangents[i + 2])).Normalized();
                tangents.AddRange([tangent.X, tangent.Y, tangent.Z, input.Tangents[i + 3]]);
            }
            uv.AddRange(input.Uv); uv2.AddRange(input.Uv2); colors.AddRange(input.Colors);
            indices.AddRange(input.Indices.Select(index => index + offset));
        }
        var arrays = new global::Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points.ToArray(); arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        if (normals.Count > 0) arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        if (tangents.Count > 0) arrays[(int)Mesh.ArrayType.Tangent] = tangents.ToArray();
        if (uv.Count > 0) arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        if (uv2.Count > 0) arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
        if (colors.Count > 0) arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var result = new ArrayMesh(); result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return result;
    }

    private sealed record WindowBatchReadback(int Vertices, int Triangles, int Layout,
        float MaximumWorldPointError, float MaximumWorldNormalError, float MaximumTangentError,
        string OriginalWorldBounds, string PublishedWorldBounds);

    private sealed record WindowBatchDifference(string SourcePath, int Part, int SourceVertex, int PublishedVertex,
        string SourceLocal, string PublishedLocal, string BeforeWorld, string AfterWorld, double? Distance,
        string SourceFrame, string PublishedFrame);

    private sealed record WindowBatchDifferenceReport(string SourceRole, int Layout, int Vertices, int Triangles,
        float PointError, float NormalError, float TangentError,
        WindowBatchDifference? WorstPoint, WindowBatchDifference? WorstNormal, WindowBatchDifference? WorstTangent,
        string OriginalWorldBounds, string PublishedWorldBounds)
    {
        public float PointLimit => .00003f;
        public float NormalLimit => .0001f;
        public float TangentLimit => .0002f;
        public double? PointErrorValue => float.IsFinite(PointError) ? PointError : null;
        public double? NormalErrorValue => float.IsFinite(NormalError) ? NormalError : null;
        public double? TangentErrorValue => float.IsFinite(TangentError) ? TangentError : null;
        public bool FailedPoint => !float.IsFinite(PointError) || PointError > PointLimit;
        public bool FailedNormal => !float.IsFinite(NormalError) || NormalError > NormalLimit;
        public bool FailedTangent => !float.IsFinite(TangentError) || TangentError > TangentLimit;
        public string Scope => "exact refusal observation; thresholds and publication decisions unchanged";
    }

    private static WindowBatchReadback CheckWindowBatchReadback(MeshInstance3D[] members,
        WindowBatchChannels[] sources, ArrayMesh published, Transform3D parentGlobal, string sourceRole)
    {
        var output = ReadWindowBatchChannels(published);
        if (output.Points.Length != sources.Sum(source => source.Points.Length)
            || output.Indices.Length != sources.Sum(source => source.Indices.Length)
            || output.Layout != sources[0].Layout) throw new WindowBatchRefusal("published-topology-or-channel-loss");
        var vertexOffset = 0; var indexOffset = 0; var pointError = 0f; var normalError = 0f; var tangentError = 0f;
        WindowBatchDifference? worstPoint = null, worstNormal = null, worstTangent = null;
        var beforeBounds = new Aabb(members[0].GlobalTransform * sources[0].Points[0], Vector3.Zero);
        var afterBounds = new Aabb(parentGlobal * output.Points[0], Vector3.Zero);
        for (var part = 0; part < sources.Length; part++)
        {
            var input = sources[part]; var sourceGlobal = members[part].GlobalTransform;
            for (var i = 0; i < input.Indices.Length; i++)
                if (output.Indices[indexOffset + i] != input.Indices[i] + vertexOffset)
                    throw new WindowBatchRefusal("published-triangle-order-changed");
            for (var i = 0; i < input.Points.Length; i++)
            {
                var j = vertexOffset + i;
                var beforePoint = sourceGlobal * input.Points[i]; var afterPoint = parentGlobal * output.Points[j];
                WindowBatchDifference Difference(Vector3 beforeLocal, Vector3 afterLocal, Vector3 beforeWorld,
                    Vector3 afterWorld, float distance) => new(members[part].GetPath().ToString(), part, i, j,
                    beforeLocal.ToString(), afterLocal.ToString(), beforeWorld.ToString(), afterWorld.ToString(),
                    float.IsFinite(distance) ? distance : null, sourceGlobal.ToString(), parentGlobal.ToString());
                var pointDistance = beforePoint.DistanceTo(afterPoint);
                if (worstPoint is null || !float.IsFinite(pointDistance) || pointDistance > pointError)
                    worstPoint = Difference(input.Points[i], output.Points[j], beforePoint, afterPoint, pointDistance);
                pointError = Mathf.Max(pointError, pointDistance);
                beforeBounds = beforeBounds.Expand(beforePoint); afterBounds = afterBounds.Expand(afterPoint);
                if (input.Normals.Length > 0)
                {
                    var beforeNormal = (sourceGlobal.Basis.Inverse().Transposed() * input.Normals[i]).Normalized();
                    var afterNormal = (parentGlobal.Basis.Inverse().Transposed() * output.Normals[j]).Normalized();
                    var normalDistance = beforeNormal.DistanceTo(afterNormal);
                    if (worstNormal is null || !float.IsFinite(normalDistance) || normalDistance > normalError)
                        worstNormal = Difference(input.Normals[i], output.Normals[j], beforeNormal, afterNormal, normalDistance);
                    normalError = Mathf.Max(normalError, normalDistance);
                }
                if (input.Tangents.Length > 0)
                {
                    var a = new Vector3(input.Tangents[i * 4], input.Tangents[i * 4 + 1], input.Tangents[i * 4 + 2]);
                    var b = new Vector3(output.Tangents[j * 4], output.Tangents[j * 4 + 1], output.Tangents[j * 4 + 2]);
                    var beforeTangent = (sourceGlobal.Basis * a).Normalized(); var afterTangent = (parentGlobal.Basis * b).Normalized();
                    var tangentDistance = beforeTangent.DistanceTo(afterTangent);
                    if (worstTangent is null || !float.IsFinite(tangentDistance) || tangentDistance > tangentError)
                        worstTangent = Difference(a, b, beforeTangent, afterTangent, tangentDistance);
                    tangentError = Mathf.Max(tangentError, tangentDistance);
                    if (input.Tangents[i * 4 + 3] != output.Tangents[j * 4 + 3])
                        throw new WindowBatchRefusal("published-tangent-handedness-changed");
                }
                if (input.Uv.Length > 0 && input.Uv[i] != output.Uv[j]
                    || input.Uv2.Length > 0 && input.Uv2[i] != output.Uv2[j]
                    || input.Colors.Length > 0 && input.Colors[i] != output.Colors[j])
                    throw new WindowBatchRefusal("published-uv-or-color-changed");
            }
            vertexOffset += input.Points.Length; indexOffset += input.Indices.Length;
        }
        // Bounds are float round-trip limits, never a geometry simplification.
        // The merged tangent readback is exact only up to the renderer's own
        // 16-bit octahedral codec on retained directions (measured worst drift
        // 1.43e-4 in B67/ON07, tangent-only, points/normals passing). Interpolated
        // far-rail directions elsewhere in this project already use a 2e-4
        // allowance for exactly that codec, so the merged check uses the same.
        if (!float.IsFinite(pointError) || !float.IsFinite(normalError) || !float.IsFinite(tangentError)
            || pointError > .00003f || normalError > .0001f || tangentError > .0002f)
            throw new WindowBatchRefusal("published-world-geometry-or-frame-differs", new WindowBatchDifferenceReport(
                sourceRole, output.Layout, output.Points.Length, output.Indices.Length / 3,
                pointError, normalError, tangentError, worstPoint, worstNormal, worstTangent,
                beforeBounds.ToString(), afterBounds.ToString()));
        return new(output.Points.Length, output.Indices.Length / 3, output.Layout, pointError, normalError, tangentError,
            beforeBounds.ToString(), afterBounds.ToString());
    }
}
