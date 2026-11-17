using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const int VariantAPbrBatchVertexLimit = 3900;

    private static void BatchPrivateVariantAStaticMeshes(AuthoredWorldDirector world,
        IReadOnlyList<string> houseIds)
    {
        // Both the imported ArrayMesh and the derived exact batches stay local to
        // this one graft pass. Matching node transforms makes color variants reuse
        // the same generated geometry without a process-wide resource cache.
        var channels = new Dictionary<(ulong Rid, bool Shadow), WindowBatchChannels>();
        var sharedGeometry = new Dictionary<string, VariantAPbrBatchGeometry>(StringComparer.Ordinal);
        foreach (var houseId in houseIds)
        {
            var house = world.ObjectRoot(houseId);
            var candidate = house?.GetNodeOrNull<Node3D>(VariantAPbrDwellingPath + "/" + VariantAPbrMountedRootName);
            if (candidate is null || !candidate.IsVisibleInTree()) continue;
            BatchVariantAHouseMeshes(candidate, channels, sharedGeometry);
        }
    }

    private static void BatchVariantAHouseMeshes(Node3D candidate,
        Dictionary<(ulong Rid, bool Shadow), WindowBatchChannels> channels,
        Dictionary<string, VariantAPbrBatchGeometry> sharedGeometry)
    {
        if (candidate.HasMeta("variantAPbrStaticBatchingComplete")) return;
        var members = new List<VariantAPbrBatchSource>();
        foreach (var source in candidate.GetChildren().OfType<MeshInstance3D>()
                     .Where(mesh => mesh.Name.ToString().StartsWith(VariantAPbrMeshPrefix, StringComparison.Ordinal))
                     .OrderBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal))
        {
            if (!TryReadVariantAPbrBatchSource(source, channels, out var member)) continue;
            members.Add(member);
        }

        foreach (var materialGroup in members.GroupBy(member =>
                     (MaterialSlot: member.MaterialSlot, MaterialRid: member.Material.GetRid().Id)))
        {
            var ordered = materialGroup.OrderBy(member => member.Instance.Name.ToString(), StringComparer.Ordinal).ToArray();
            if (ordered.Length < 2 || !ShareVariantAPbrRenderContract(ordered)) continue;

            foreach (var chunk in SplitVariantAPbrBatch(ordered))
            {
                if (chunk.Length < 2) continue;
                TryPublishVariantAPbrBatch(candidate, materialGroup.Key.MaterialSlot,
                    materialGroup.First().Material, chunk, sharedGeometry);
            }
        }
        candidate.SetMeta("variantAPbrStaticBatchingComplete", true);
    }

    private static bool TryReadVariantAPbrBatchSource(MeshInstance3D source,
        Dictionary<(ulong Rid, bool Shadow), WindowBatchChannels> channels,
        out VariantAPbrBatchSource member)
    {
        member = null!;
        var name = source.Name.ToString();
        if (name.Contains("Door", StringComparison.Ordinal) || name.Contains("Handle", StringComparison.Ordinal)
            || name.EndsWith("_LogCrown", StringComparison.Ordinal)
            || source.GetScript().VariantType != Variant.Type.Nil
            || source.GetChildCount() != 0 || source.Skin is not null
            || source.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() != 1
            || mesh.GetBlendShapeCount() != 0 || mesh.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles
            || source.GetSurfaceOverrideMaterial(0) is not null
            || source.MaterialOverlay is not null
            || !source.Visible || !source.IsVisibleInTree()
            || source.Transparency != 0f || source.CustomAabb != new Aabb() || source.ExtraCullMargin != 0f
            || !source.VisibilityParent.IsEmpty || source.VisibilityRangeBegin != 0f
            || source.VisibilityRangeEnd != 0f || source.VisibilityRangeBeginMargin != 0f
            || source.VisibilityRangeEndMargin != 0f
            || source.VisibilityRangeFadeMode != GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled
            || !source.Transform.IsFinite() || source.Transform.Basis.Determinant() <= .0000001f
            || !HasUniformOrthogonalBasis(source.Transform.Basis))
            return false;

        var material = source.GetActiveMaterial(0);
        if (material is null || material.NextPass is not null
            || !TryGetVariantAPbrMaterialSlot(source, material, out var materialSlot))
            return false;

        try
        {
            var render = ReadVariantAPbrChannels(mesh, originalShadow: false, channels);
            WindowBatchChannels? shadow = null;
            if (source.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
            {
                if (mesh.ShadowMesh is null) shadow = render;
                else if (mesh.ShadowMesh is ArrayMesh shadowMesh)
                    shadow = ReadVariantAPbrChannels(shadowMesh, originalShadow: true, channels);
                else return false;
            }

            member = new(source, mesh, material, materialSlot, render, shadow);
            return true;
        }
        catch (WindowBatchRefusal)
        {
            // The batch is an optional renderer optimization. Any imported LOD,
            // channel or shadow layout outside the existing exact merger's contract
            // leaves the original mesh live.
            return false;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static WindowBatchChannels ReadVariantAPbrChannels(ArrayMesh mesh, bool originalShadow,
        Dictionary<(ulong Rid, bool Shadow), WindowBatchChannels> channels)
    {
        var key = (mesh.GetRid().Id, originalShadow);
        if (!channels.TryGetValue(key, out var decoded))
            channels[key] = decoded = ReadWindowBatchChannels(mesh, originalShadow);
        return decoded;
    }

    private static bool TryGetVariantAPbrMaterialSlot(MeshInstance3D source, Material material,
        out string materialSlot)
    {
        materialSlot = string.Empty;
        if (material is StandardMaterial3D standard
            && standard.Transparency == BaseMaterial3D.TransparencyEnum.Disabled
            && standard.AlbedoColor.A >= 1f && !standard.HeightmapEnabled)
        {
            // This imported GLB's static opaque surfaces share one compatible
            // vertex contract; the material itself is still assigned per batch.
            materialSlot = "standard-opaque-pbr";
            return true;
        }

        if (material is not ShaderMaterial shaderMaterial
            || !PainterlyMaterialLibrary.UsesSharedShadowMaterial(shaderMaterial)
            || !material.HasMeta("surface")
            || !string.Equals(material.GetMeta("surface").AsString(), "wood_painted_trim", StringComparison.Ordinal)
            || shaderMaterial.Shader is not { } shader
            || shader.Code.Contains("ALPHA", StringComparison.Ordinal)
            || shader.Code.Contains("discard", StringComparison.Ordinal)
            || shader.Code.Contains("INSTANCE_ID", StringComparison.Ordinal)
            || shader.Code.Contains("INSTANCE_CUSTOM", StringComparison.Ordinal)
            || shaderMaterial.GetShaderParameter("base_color").VariantType != Variant.Type.Color
            || shaderMaterial.GetShaderParameter("base_color").AsColor().A < 1f
            || HasVariantAPbrInstanceShaderOverrides(source))
            return false;

        // The rigid painterly shader has no vertex deformation, alpha path or
        // instance-ID dependency. Its world-position projection remains identical
        // after exact local transforms are appended into the same house root.
        materialSlot = "painterly-trim:" + shader.GetRid().Id.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool HasVariantAPbrInstanceShaderOverrides(MeshInstance3D source) =>
        source.GetInstanceShaderParameter("ground_base_y").VariantType != Variant.Type.Nil
        || source.GetInstanceShaderParameter("snow_shelter").VariantType != Variant.Type.Nil
        || source.GetInstanceShaderParameter("instance_pigment_mul").VariantType != Variant.Type.Nil;

    private static bool ShareVariantAPbrRenderContract(VariantAPbrBatchSource[] members)
    {
        var first = members[0].Instance;
        return members.All(member => ReferenceEquals(member.Instance.GetParent(), first.GetParent())
            && member.Instance.Layers == first.Layers && member.Instance.CastShadow == first.CastShadow
            && member.Instance.GIMode == first.GIMode
            && member.Instance.GILightmapTexelScale == first.GILightmapTexelScale
            && member.Instance.LodBias == first.LodBias
            && member.Instance.IgnoreOcclusionCulling == first.IgnoreOcclusionCulling);
    }

    private static bool HasUniformOrthogonalBasis(Basis basis)
    {
        var scale = basis.X.Length();
        return float.IsFinite(scale) && scale > .0001f
            && Math.Abs(basis.Y.Length() - scale) <= scale * .00001f
            && Math.Abs(basis.Z.Length() - scale) <= scale * .00001f
            && Math.Abs(basis.X.Dot(basis.Y)) <= scale * scale * .00001f
            && Math.Abs(basis.X.Dot(basis.Z)) <= scale * scale * .00001f
            && Math.Abs(basis.Y.Dot(basis.Z)) <= scale * scale * .00001f;
    }

    private static IEnumerable<VariantAPbrBatchSource[]> SplitVariantAPbrBatch(
        VariantAPbrBatchSource[] members)
    {
        var chunk = new List<VariantAPbrBatchSource>();
        var renderVertices = 0;
        var shadowVertices = 0;
        foreach (var member in members)
        {
            var nextRender = renderVertices + member.Render.Points.Length;
            var nextShadow = shadowVertices + (member.Shadow?.Points.Length ?? 0);
            if (chunk.Count > 0 && Math.Max(nextRender, nextShadow) > VariantAPbrBatchVertexLimit)
            {
                yield return chunk.ToArray();
                chunk.Clear();
                renderVertices = 0;
                shadowVertices = 0;
            }

            chunk.Add(member);
            renderVertices += member.Render.Points.Length;
            shadowVertices += member.Shadow?.Points.Length ?? 0;
        }
        if (chunk.Count > 0) yield return chunk.ToArray();
    }

    private static void TryPublishVariantAPbrBatch(Node3D candidate, string materialSlot,
        Material material, VariantAPbrBatchSource[] members,
        Dictionary<string, VariantAPbrBatchGeometry> sharedGeometry)
    {
        MeshInstance3D? batch = null;
        var hiddenSources = new List<(MeshInstance3D Instance, bool WasVisible)>();
        try
        {
            var renderInputs = members.Select(member => member.Render).ToArray();
            var transforms = members.Select(member => member.Instance.Transform).ToArray();
            var key = VariantAPbrGeometryKey(materialSlot, members);
            if (!sharedGeometry.TryGetValue(key, out var geometry))
            {
                var render = MergeWindowBatchChannels(renderInputs, transforms);
                ArrayMesh? shadow = null;
                if (members[0].Shadow is not null)
                    shadow = MergeWindowBatchChannels(members.Select(member => member.Shadow!).ToArray(), transforms);
                render.ShadowMesh = shadow;
                geometry = new(render, shadow);
                sharedGeometry.Add(key, geometry);
            }

            var renderCheck = CheckWindowBatchReadback(members.Select(member => member.Instance).ToArray(),
                renderInputs, geometry.Render, candidate.GlobalTransform, "variant-a-render");
            WindowBatchReadback? shadowCheck = null;
            if (geometry.Shadow is not null)
                shadowCheck = CheckWindowBatchReadback(members.Select(member => member.Instance).ToArray(),
                    members.Select(member => member.Shadow!).ToArray(), geometry.Shadow,
                    candidate.GlobalTransform, "variant-a-original-shadow");
            if (renderCheck.Vertices > VariantAPbrBatchVertexLimit
                || shadowCheck is { Vertices: > VariantAPbrBatchVertexLimit }
                || geometry.Render.SurfaceGetMaterial(0) is not null)
                throw new WindowBatchRefusal("published-batch-exceeds-bounds-or-has-unexpected-material");

            var sourceUnion = ParentSpaceBox(candidate, members[0].Instance);
            foreach (var member in members.Skip(1))
                sourceUnion = sourceUnion.Merge(ParentSpaceBox(candidate, member.Instance));
            var batchBounds = geometry.Render.GetAabb();
            var growth = Math.Max(Math.Max(
                    Math.Max(batchBounds.End.X - sourceUnion.End.X, sourceUnion.Position.X - batchBounds.Position.X),
                    Math.Max(sourceUnion.End.X - batchBounds.End.X, batchBounds.Position.X - sourceUnion.Position.X)),
                Math.Max(
                    Math.Max(Math.Max(batchBounds.End.Y - sourceUnion.End.Y, sourceUnion.Position.Y - batchBounds.Position.Y),
                        Math.Max(sourceUnion.End.Y - batchBounds.End.Y, batchBounds.Position.Y - sourceUnion.Position.Y)),
                    Math.Max(Math.Max(batchBounds.End.Z - sourceUnion.End.Z, sourceUnion.Position.Z - batchBounds.Position.Z),
                        Math.Max(sourceUnion.End.Z - batchBounds.End.Z, batchBounds.Position.Z - sourceUnion.Position.Z))));
            if (!float.IsFinite(growth) || growth > .001f)
                throw new WindowBatchRefusal("variant-a-batch-culling-footprint-grew");

            var first = members[0].Instance;
            batch = new MeshInstance3D
            {
                Name = "VariantAPbrStaticBatch_" + candidate.GetChildCount().ToString(CultureInfo.InvariantCulture),
                Mesh = geometry.Render,
                MaterialOverride = material,
                Visible = false,
                Layers = first.Layers,
                CastShadow = first.CastShadow,
                GIMode = first.GIMode,
                GILightmapTexelScale = first.GILightmapTexelScale,
                LodBias = first.LodBias,
                IgnoreOcclusionCulling = first.IgnoreOcclusionCulling
            };
            candidate.AddChild(batch);
            var sourcePoseChanged = false;
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                if (!member.Instance.IsVisibleInTree() || member.Instance.Mesh != member.RenderMesh
                    || member.Instance.Transform != transforms[index])
                {
                    sourcePoseChanged = true;
                    break;
                }
            }
            if (batch.GlobalTransform != candidate.GlobalTransform
                || batch.GetActiveMaterial(0)?.GetRid() != material.GetRid() || sourcePoseChanged)
                throw new WindowBatchRefusal("variant-a-batch-publication-changed-source-pose");

            batch.SetMeta("variantAPbrExactBatch", true);
            batch.SetMeta("presentationOnly", true);
            batch.SetMeta("visualOnly", true);
            batch.SetMeta("variantAPbrBatchSourceMeshes", members.Length);
            batch.SetMeta("variantAPbrBatchTriangleCount", renderCheck.Triangles);
            batch.SetMeta("variantAPbrBatchMaterialSlot", materialSlot);
            batch.SetMeta("variantAPbrBatchSourceBounds", sourceUnion.ToString());
            batch.SetMeta("variantAPbrBatchBoundsGrowthMillimetres", growth * 1000f);
            batch.Visible = true;
            foreach (var member in members)
            {
                hiddenSources.Add((member.Instance, member.Instance.Visible));
                member.Instance.SetMeta("variantAPbrBatchOwner", batch.GetPath().ToString());
                member.Instance.Visible = false;
            }
            batch = null;
        }
        catch (WindowBatchRefusal)
        {
            RestoreVariantAPbrBatchSources(hiddenSources);
            if (batch is not null && GodotObject.IsInstanceValid(batch))
            {
                candidate.RemoveChild(batch);
                batch.Free();
            }
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException)
        {
            RestoreVariantAPbrBatchSources(hiddenSources);
            if (batch is not null && GodotObject.IsInstanceValid(batch))
            {
                candidate.RemoveChild(batch);
                batch.Free();
            }
        }
    }

    private static void RestoreVariantAPbrBatchSources(IEnumerable<(MeshInstance3D Instance, bool WasVisible)> sources)
    {
        foreach (var (instance, wasVisible) in sources)
        {
            if (!GodotObject.IsInstanceValid(instance)) continue;
            instance.Visible = wasVisible;
            if (instance.HasMeta("variantAPbrBatchOwner")) instance.RemoveMeta("variantAPbrBatchOwner");
        }
    }

    private static string VariantAPbrGeometryKey(string materialSlot, VariantAPbrBatchSource[] members)
    {
        var key = new StringBuilder(materialSlot);
        foreach (var member in members)
        {
            key.Append('|').Append(member.RenderMesh.GetRid().Id.ToString(CultureInfo.InvariantCulture));
            AppendVariantAPbrTransformKey(key, member.Instance.Transform);
            var shadow = member.RenderMesh.ShadowMesh as ArrayMesh;
            key.Append('|').Append(shadow is null
                ? "render"
                : shadow.GetRid().Id.ToString(CultureInfo.InvariantCulture));
            key.Append('|').Append(member.Instance.CastShadow);
        }
        return key.ToString();
    }

    private static void AppendVariantAPbrTransformKey(StringBuilder key, Transform3D transform)
    {
        Append(transform.Basis.X.X); Append(transform.Basis.X.Y); Append(transform.Basis.X.Z);
        Append(transform.Basis.Y.X); Append(transform.Basis.Y.Y); Append(transform.Basis.Y.Z);
        Append(transform.Basis.Z.X); Append(transform.Basis.Z.Y); Append(transform.Basis.Z.Z);
        Append(transform.Origin.X); Append(transform.Origin.Y); Append(transform.Origin.Z);

        void Append(float value) => key.Append('|')
            .Append(BitConverter.SingleToInt32Bits(value).ToString("X8", CultureInfo.InvariantCulture));
    }

    private sealed record VariantAPbrBatchSource(MeshInstance3D Instance, ArrayMesh RenderMesh,
        Material Material, string MaterialSlot, WindowBatchChannels Render, WindowBatchChannels? Shadow);

    private sealed record VariantAPbrBatchGeometry(ArrayMesh Render, ArrayMesh? Shadow);
}
