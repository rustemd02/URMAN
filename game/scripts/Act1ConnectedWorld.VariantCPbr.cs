using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string VariantCOutbuildingPbrPath = "res://assets/models/act1/neighbor-outbuilding-c-pbr-v1.glb";
    private const string VariantCOutbuildingCatalogId = "urman.catalog:urman_village_exterior_kit/villageparcel-variantc-banyayard";
    private const string VariantCOutbuildingName = "VillageParcel_VariantC_BanyaYard_Outbuilding";
    private const string VariantCOutbuildingComponentPath = "Visual/VillageParcel_VariantC_BanyaYard/" + VariantCOutbuildingName;
    private const string VariantCOutbuildingMountedName = "VariantCOutbuildingPbrV1";
    private static readonly HashSet<string> VariantCOutbuildingMeshNames = new(StringComparer.Ordinal)
    {
        "VariantC_Utility_Door_LOD0", "VariantC_Utility_Foundation_LOD0",
        "VariantC_Utility_OpenBay_LOD0", "VariantC_Utility_Post_00_LOD0",
        "VariantC_Utility_Post_01_LOD0", "VariantC_Utility_RearBatten_LOD0",
        "VariantC_Utility_Roof_LOD0", "VariantC_Utility_SideBatten_LOD0",
        "VariantC_Utility_Threshold_LOD0", "VariantC_Utility_Wall_LOD0"
    };

    // Late presentation graft: keep source paths, bench anchors and completed collision.
    private void GraftPrivateVariantCOutbuildingPbrFamily()
    {
        using var naming = InvariantNameScope();
        var authored = AuthoredWorld
            ?? throw new InvalidOperationException("Variant C outbuilding PBR needs the authored world.");
        var packed = ResourceLoader.Load<PackedScene>(VariantCOutbuildingPbrPath)
            ?? throw new InvalidOperationException($"Missing Variant C outbuilding: {VariantCOutbuildingPbrPath}.");
        foreach (var id in authored.ObjectIds.Where(id => authored.IsAddressedCatalogProp(id, VariantCOutbuildingCatalogId)
            && !authored.HasHouseModelOverride(id)))
        {
            var house = authored.ObjectRoot(id)
                ?? throw new InvalidOperationException($"Missing addressed Variant C household: {id}.");
            var source = house.GetNodeOrNull<Node3D>(VariantCOutbuildingComponentPath)
                ?? throw new InvalidOperationException("Variant C household lost its original outbuilding.");
            if (!house.IsVisibleInTree() || !source.IsVisibleInTree()) continue;
            if (house.GetNodeOrNull<StaticBody3D>("Collision") is not { } collision || collision.GetChildCount() == 0)
                throw new InvalidOperationException("Variant C outbuilding requires completed original household collision.");
            GraftVariantCOutbuildingPbr(source, packed);
        }

        var connective = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/Act1AuthoredExteriorKitPresentation/NeighborParcels/ConnectiveStreet/DeepParcel/ConnectiveStreetDeepBanyaYardParcel")
            ?? throw new InvalidOperationException("Variant C PBR needs the existing connective parcel.");
        var shed = FindDescendants<Node3D>(connective).Single(node => node.Name == VariantCOutbuildingName);
        if (!connective.IsVisibleInTree() || !shed.IsVisibleInTree()) return;
        GraftVariantCOutbuildingPbr(shed, packed);
    }

    private static void GraftVariantCOutbuildingPbr(Node3D source, PackedScene packed)
    {
        if (!source.IsVisibleInTree() || source.GetNodeOrNull(VariantCOutbuildingMountedName) is not null)
            throw new InvalidOperationException("Variant C outbuilding is hidden or already grafted.");
        var originals = VariantAMeshInstances(source).ToArray();
        if (originals.Length != 10 || originals.Any(mesh => mesh.Mesh is null || mesh.GetParent() != source)
            || !UniqueVariantAMeshNames(originals, "Variant C source outbuilding").SetEquals(VariantCOutbuildingMeshNames))
            throw new InvalidDataException("Variant C source must retain its exact ten semantic meshes.");
        var live = originals.ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);
        Node3D? imported = null;
        Node3D? candidate = null;
        var hidden = new List<(MeshInstance3D Mesh, bool Visible)>();
        try
        {
            imported = packed.Instantiate<Node3D>()
                ?? throw new InvalidDataException("Variant C candidate has no 3D root.");
            var roots = new[] { imported }.Concat(FindDescendants<Node3D>(imported))
                .Where(node => node.Name == VariantCOutbuildingName).ToArray();
            if (roots.Length != 1 || !IsIdentityTransform(roots[0].Transform)
                || !IsIdentityBasis(ComposeImportedAncestorBasis(roots[0])))
                throw new InvalidDataException("Variant C candidate needs one original outbuilding root with identity transform.");
            candidate = roots[0];
            var nodes = new Node[] { candidate }.Concat(FindDescendants<Node>(candidate)).ToArray();
            if (nodes.Any(node => (node.GetType() != typeof(Node3D) && node.GetType() != typeof(MeshInstance3D))
                    || node.GetScript().VariantType != Variant.Type.Nil))
                throw new InvalidDataException("Variant C candidate must contain plain presentation nodes only.");
            var meshes = VariantAMeshInstances(candidate).ToArray();
            if (meshes.Length != 10 || !UniqueVariantAMeshNames(meshes, "Variant C PBR candidate").SetEquals(VariantCOutbuildingMeshNames)
                || meshes.Any(mesh => mesh.GetParent() != candidate))
                throw new InvalidDataException("Variant C candidate must contain the exact ten original semantic meshes.");
            var triangles = 0;
            foreach (var mesh in meshes)
            {
                if (mesh.Mesh is not ArrayMesh geometry || geometry.GetSurfaceCount() != 1
                    || geometry.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles
                    || geometry.SurfaceGetMaterial(0) is not BaseMaterial3D material
                    || material.ResourceName != "URMAN_VariantC_Outbuilding_GeneratedPBR"
                    || material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
                    || material.NextPass is not null || mesh.MaterialOverride is not null || mesh.MaterialOverlay is not null
                    || mesh.GetSurfaceOverrideMaterial(0) is not null)
                    throw new InvalidDataException("Variant C candidate lost its single opaque generated PBR surface.");
                using var arrays = geometry.SurfaceGetArrays(0);
                var indices = arrays[(int)Mesh.ArrayType.Index];
                var count = indices.VariantType == Variant.Type.Nil
                    ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length : indices.AsInt32Array().Length;
                var name = mesh.Name.ToString();
                if (count != (name is "VariantC_Utility_Roof_LOD0" or "VariantC_Utility_Wall_LOD0" ? 12 : 28) * 3)
                    throw new InvalidDataException($"Variant C candidate changed source topology: {name}.");
                triangles += count / 3;
                mesh.Transform = live[name].Transform;
                mesh.Visible = live[name].Visible;
                mesh.CastShadow = live[name].CastShadow;
                if (name == "VariantC_Utility_Roof_LOD0")
                {
                    var roof = (BaseMaterial3D)material.Duplicate();
                    roof.NextPass = PainterlyMaterialLibrary.ForImportedRoofSnow();
                    mesh.SetSurfaceOverrideMaterial(0, roof);
                }
            }
            if (triangles != 248)
                throw new InvalidDataException("Variant C candidate must retain 248 source triangles.");
            if (candidate != imported)
            {
                candidate.GetParent().RemoveChild(candidate);
                ClearExtractedSceneOwnership(candidate);
                imported.Free();
            }
            else ClearExtractedSceneOwnership(candidate);
            imported = null;
            candidate.Transform = Transform3D.Identity;
            candidate.Name = VariantCOutbuildingMountedName;
            candidate.SetMeta("presentationOnly", true);
            candidate.SetMeta("visualOnly", true);
            candidate.SetMeta("assetSource", VariantCOutbuildingPbrPath);
            candidate.SetMeta("collisionOwner", "unchanged original source collision; OpenBay and bench anchors retained");
            source.AddChild(candidate);
            foreach (var mesh in originals)
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
