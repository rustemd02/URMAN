using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string VariantBOutbuildingPbrPath = "res://assets/models/act1/neighbor-outbuilding-b-pbr-v1.glb";
    private const string VariantBOutbuildingCatalogId = "urman.catalog:urman_village_exterior_kit/villageparcel-variantb-plasterannex";
    private const string VariantBOutbuildingName = "VillageParcel_VariantB_PlasterAnnex_Outbuilding";
    private const string VariantBOutbuildingComponentPath = "Visual/VillageParcel_VariantB_PlasterAnnex/" + VariantBOutbuildingName;
    private const string VariantBOutbuildingMountedName = "VariantBOutbuildingPbrV1";
    private const string VariantBRearGableName = "VariantB_Banya_GableRear_LOD0";

    // Same post-collision presentation graft as the existing Variant A/H049 owners.
    // Explicit Studio model overrides retain their own complete visual model.
    private void GraftPrivateVariantBOutbuildingPbrFamily()
    {
        using var naming = InvariantNameScope();
        var authored = AuthoredWorld
            ?? throw new InvalidOperationException("Variant B outbuilding PBR needs the authored world.");
        var ids = authored.ObjectIds.Where(id => authored.IsAddressedCatalogProp(id, VariantBOutbuildingCatalogId)
            && !authored.HasHouseModelOverride(id)).ToArray();
        if (ids.Length == 0) return;
        var packed = ResourceLoader.Load<PackedScene>(VariantBOutbuildingPbrPath)
            ?? throw new InvalidOperationException($"Missing Variant B outbuilding: {VariantBOutbuildingPbrPath}.");
        foreach (var id in ids)
        {
            var house = authored.ObjectRoot(id)
                ?? throw new InvalidOperationException($"Missing addressed Variant B household: {id}.");
            var component = house.GetNodeOrNull<Node3D>(VariantBOutbuildingComponentPath)
                ?? throw new InvalidOperationException("Variant B household lost its original outbuilding.");
            // Existing scene dressing deliberately retires some presentations.
            if (!house.IsVisibleInTree() || !component.IsVisibleInTree()) continue;
            if (house.GetNodeOrNull<StaticBody3D>("Collision") is not { } collision || collision.GetChildCount() == 0)
                throw new InvalidOperationException("Variant B outbuilding requires completed original household collision.");
            GraftVariantBOutbuildingPbr(house, packed);
        }
    }

    private static void GraftVariantBOutbuildingPbr(Node3D house, PackedScene packed)
    {
        var source = house.GetNodeOrNull<Node3D>(VariantBOutbuildingComponentPath)
            ?? throw new InvalidOperationException("Variant B household lost its original outbuilding.");
        if (!source.IsVisibleInTree() || source.GetNodeOrNull(VariantBOutbuildingMountedName) is not null)
            throw new InvalidOperationException("Variant B outbuilding is hidden or already grafted.");
        var originals = VariantAMeshInstances(source).ToArray();
        var originalNames = UniqueVariantAMeshNames(originals, "Variant B source outbuilding");
        var glassNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "VariantB_Banya_RearWindow_LOD0", "VariantB_Banya_WindowFront_Inset_LOD0"
        };
        if (originals.Length != 20 || originals.Any(mesh => mesh.Mesh is null || mesh.GetParent() != source)
            || originalNames.Any(name => !name.StartsWith("VariantB_Banya_", StringComparison.Ordinal))
            || !glassNames.IsSubsetOf(originalNames))
            throw new InvalidDataException("Variant B source must retain 18 opaque meshes and two original window insets.");
        var opaque = originals.Where(mesh => !glassNames.Contains(mesh.Name.ToString()))
            .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);
        var expectedNames = opaque.Keys.Append(VariantBRearGableName).ToHashSet(StringComparer.Ordinal);
        Node3D? imported = null;
        Node3D? candidate = null;
        var hidden = new List<(MeshInstance3D Mesh, bool Visible)>();
        try
        {
            imported = packed.Instantiate<Node3D>()
                ?? throw new InvalidDataException("Variant B candidate has no 3D root.");
            var roots = new[] { imported }.Concat(FindDescendants<Node3D>(imported))
                .Where(node => node.Name == VariantBOutbuildingName).ToArray();
            if (roots.Length != 1 || !IsIdentityTransform(roots[0].Transform)
                || !IsIdentityBasis(ComposeImportedAncestorBasis(roots[0])))
                throw new InvalidDataException("Variant B candidate needs one original outbuilding root with identity transform.");
            candidate = roots[0];
            var nodes = new Node[] { candidate }.Concat(FindDescendants<Node>(candidate)).ToArray();
            if (nodes.Any(node => (node.GetType() != typeof(Node3D) && node.GetType() != typeof(MeshInstance3D))
                    || node.GetScript().VariantType != Variant.Type.Nil))
                throw new InvalidDataException("Variant B candidate must contain plain presentation nodes only.");
            var meshes = VariantAMeshInstances(candidate).ToArray();
            if (meshes.Length != 19 || !UniqueVariantAMeshNames(meshes, "Variant B PBR candidate").SetEquals(expectedNames)
                || meshes.Any(mesh => mesh.GetParent() != candidate))
                throw new InvalidDataException("Variant B candidate must contain the same 18 opaque meshes plus the rear gable.");
            var triangles = 0;
            foreach (var mesh in meshes)
            {
                if (mesh.Mesh is not ArrayMesh geometry || geometry.GetSurfaceCount() != 1
                    || geometry.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles
                    || geometry.SurfaceGetMaterial(0) is not BaseMaterial3D material
                    || material.ResourceName != "URMAN_VariantB_Outbuilding_GeneratedPBR"
                    || material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
                    || material.NextPass is not null || mesh.MaterialOverride is not null || mesh.MaterialOverlay is not null
                    || mesh.GetSurfaceOverrideMaterial(0) is not null)
                    throw new InvalidDataException("Variant B candidate lost its single opaque generated PBR surface.");
                using var arrays = geometry.SurfaceGetArrays(0);
                var indices = arrays[(int)Mesh.ArrayType.Index];
                var count = indices.VariantType == Variant.Type.Nil
                    ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length : indices.AsInt32Array().Length;
                if (mesh.Name == VariantBRearGableName && count != 8 * 3)
                    throw new InvalidDataException("Variant B rear gable must retain the eight source-derived triangles.");
                triangles += count / 3;
                if (opaque.TryGetValue(mesh.Name.ToString(), out var live))
                {
                    mesh.Transform = live.Transform;
                    mesh.Visible = live.Visible;
                }
                // Share the imported atlas. Snow is the existing roof-only pass,
                // assigned to a private material so walls keep their PBR surface.
                if (mesh.Name.ToString() is "VariantB_Banya_Roof_LOD0" or "VariantB_Banya_EntryRoof_LOD0")
                {
                    var roof = (BaseMaterial3D)material.Duplicate();
                    roof.NextPass = PainterlyMaterialLibrary.ForImportedRoofSnow();
                    mesh.SetSurfaceOverrideMaterial(0, roof);
                }
            }
            if (triangles != 452)
                throw new InvalidDataException("Variant B candidate must retain 444 original opaque and eight rear-gable triangles.");
            if (candidate != imported)
            {
                candidate.GetParent().RemoveChild(candidate);
                ClearExtractedSceneOwnership(candidate);
                imported.Free();
            }
            else ClearExtractedSceneOwnership(candidate);
            imported = null;
            candidate.Transform = Transform3D.Identity;
            candidate.Name = VariantBOutbuildingMountedName;
            candidate.SetMeta("presentationOnly", true);
            candidate.SetMeta("visualOnly", true);
            candidate.SetMeta("assetSource", VariantBOutbuildingPbrPath);
            candidate.SetMeta("collisionOwner", "unchanged original authored household source triangles");
            source.AddChild(candidate);
            foreach (var mesh in opaque.Values)
            {
                hidden.Add((mesh, mesh.Visible));
                mesh.Visible = false;
            }
            candidate = null;
        }
        catch
        {
            foreach (var (mesh, visible) in hidden)
                if (GodotObject.IsInstanceValid(mesh)) mesh.Visible = visible;
            throw;
        }
        finally
        {
            if (imported is not null && GodotObject.IsInstanceValid(imported)) imported.Free();
            if (candidate is not null && GodotObject.IsInstanceValid(candidate)) candidate.Free();
        }
    }
}
