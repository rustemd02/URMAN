using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string VariantBDwellingPbrPath = "res://assets/models/act1/neighbor-house-b-pbr-v2.glb";
    private const string VariantBDwellingCatalogId = "urman.catalog:urman_village_exterior_kit/villageparcel-variantb-plasterannex";
    private const string VariantBDwellingName = "VillageParcel_VariantB_PlasterAnnex_Dwelling";
    private const string VariantBDwellingComponentPath = "Visual/VillageParcel_VariantB_PlasterAnnex/" + VariantBDwellingName;
    private const string VariantBDwellingMountedName = "VariantBDwellingPbrV2Opaque";
    private const string VariantBDwellingMeshPrefix = "VariantB_PlasterAnnex_Dwelling_";
    private const string VariantBDwellingPbrMaterial = "URMAN_NeighborB_PlasterAnnex_Generated_PBR_v2";

    // Replace only the candidate's generated opaque surfaces. Source glass,
    // snow and the 23 preserved roof/detail surfaces remain on the live dwelling.
    private void GraftPrivateVariantBDwellingPbrFamily()
    {
        using var naming = InvariantNameScope();
        var authored = AuthoredWorld
            ?? throw new InvalidOperationException("Variant B dwelling PBR needs the authored world.");
        var ids = authored.ObjectIds.Where(id => authored.IsAddressedCatalogProp(id, VariantBDwellingCatalogId)
            && !authored.HasHouseModelOverride(id)).ToArray();
        if (ids.Length == 0) return;

        var packed = ResourceLoader.Load<PackedScene>(VariantBDwellingPbrPath)
            ?? throw new InvalidOperationException($"Missing Variant B dwelling: {VariantBDwellingPbrPath}.");
        var tintedMaterials = new Dictionary<(ulong MaterialId, Color Tint), BaseMaterial3D>();
        foreach (var id in ids)
        {
            var house = authored.ObjectRoot(id)
                ?? throw new InvalidOperationException($"Missing addressed Variant B household: {id}.");
            var source = house.GetNodeOrNull<Node3D>(VariantBDwellingComponentPath)
                ?? throw new InvalidOperationException("Addressed Variant B household lost its original dwelling.");
            if (!house.IsVisibleInTree() || !source.IsVisibleInTree()) continue;
            GraftVariantBDwellingPbr(source, packed, tintedMaterials);
        }
    }

    private static void GraftVariantBDwellingPbr(
        Node3D source,
        PackedScene packed,
        Dictionary<(ulong MaterialId, Color Tint), BaseMaterial3D> tintedMaterials)
    {
        if (source.GetNodeOrNull<Node3D>(VariantBDwellingMountedName) is not null)
            throw new InvalidOperationException("Variant B dwelling PBR was already grafted.");

        var sourceMeshes = VariantAMeshInstances(source)
            .Where(mesh => mesh.Name.ToString().StartsWith(VariantBDwellingMeshPrefix, StringComparison.Ordinal))
            .ToArray();
        if (sourceMeshes.Any(mesh => mesh.Mesh is null || mesh.GetParent() != source))
            throw new InvalidDataException("Variant B dwelling source meshes must be non-empty direct children.");
        UniqueVariantAMeshNames(sourceMeshes, "Variant B dwelling source");
        var sourceByName = sourceMeshes.ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);
        if (sourceByName.Count != 126)
            throw new InvalidDataException($"Variant B dwelling has {sourceByName.Count} semantic meshes; expected 126 after wall crowns.");

        var sourceGlass = sourceMeshes.Where(mesh => mesh.Name.ToString().EndsWith("_Glass_LOD0", StringComparison.Ordinal)).ToArray();
        var sourceSnow = sourceMeshes.Where(mesh => mesh.Name == VariantBDwellingMeshPrefix + "RoofSnow_LOD0").ToArray();
        if (sourceGlass.Length != 9 || sourceGlass.Any(mesh => mesh.Mesh!.SurfaceGetMaterial(0)?.ResourceName != "URMAN_Window_DimGlass")
            || sourceSnow.Length != 1 || sourceSnow[0].Mesh!.SurfaceGetMaterial(0)?.ResourceName != "URMAN_Hero_RoofSnow")
            throw new InvalidDataException("Variant B dwelling must keep its nine original glass panes and separate roof snow layer.");

        var sourceOpaqueNames = sourceByName.Keys
            .Where(name => !name.EndsWith("_Glass_LOD0", StringComparison.Ordinal)
                && name != VariantBDwellingMeshPrefix + "RoofSnow_LOD0")
            .ToHashSet(StringComparer.Ordinal);
        if (sourceOpaqueNames.Count != 116)
            throw new InvalidDataException($"Variant B dwelling exposes {sourceOpaqueNames.Count} opaque semantic meshes; expected 116.");

        Node3D? importedRoot = null;
        Node3D? candidateRoot = null;
        var candidateAttached = false;
        var hiddenOriginals = new List<(MeshInstance3D Mesh, bool Visible)>();
        try
        {
            importedRoot = packed.Instantiate<Node3D>()
                ?? throw new InvalidDataException("Variant B dwelling candidate has no 3D root.");
            if (importedRoot.Name != VariantBDwellingName || !IsIdentityTransform(importedRoot.Transform)
                || !IsIdentityBasis(ComposeImportedAncestorBasis(importedRoot)))
                throw new InvalidDataException("Variant B dwelling candidate needs one identity-transform authored root.");
            candidateRoot = importedRoot;

            var candidateNodes = new Node[] { candidateRoot }.Concat(FindDescendants<Node>(candidateRoot)).ToArray();
            if (candidateNodes.Any(node => (node.GetType() != typeof(Node3D) && node.GetType() != typeof(MeshInstance3D))
                    || node.GetScript().VariantType != Variant.Type.Nil))
                throw new InvalidDataException("Variant B dwelling candidate must contain presentation nodes only.");

            var candidateMeshes = VariantAMeshInstances(candidateRoot).ToArray();
            if (candidateMeshes.Length != 116 || candidateMeshes.Any(mesh => mesh.Mesh is null || mesh.GetParent() != candidateRoot))
                throw new InvalidDataException("Variant B dwelling candidate must contain 116 direct semantic meshes.");
            var candidateNames = UniqueVariantAMeshNames(candidateMeshes, "Variant B dwelling candidate");
            if (!candidateNames.SetEquals(sourceOpaqueNames))
                throw new InvalidDataException("Variant B dwelling candidate does not exactly match the live opaque mesh names.");

            var pbrMeshes = new List<MeshInstance3D>(93);
            var preservedMeshes = new List<MeshInstance3D>(23);
            foreach (var mesh in candidateMeshes)
            {
                var name = mesh.Name.ToString();
                if (!sourceByName.TryGetValue(name, out var live))
                    throw new InvalidDataException($"Variant B candidate mesh has no exact live source match: {name}.");
                if (mesh.Mesh is not ArrayMesh geometry || geometry.GetSurfaceCount() == 0
                    || mesh.MaterialOverride is not null || mesh.MaterialOverlay is not null
                    || Enumerable.Range(0, geometry.GetSurfaceCount())
                        .Any(surface => mesh.GetSurfaceOverrideMaterial(surface) is not null))
                    throw new InvalidDataException($"Variant B candidate mesh has an invalid or overridden semantic surface: {name}.");

                var material = geometry.SurfaceGetMaterial(0);
                if (material?.ResourceName == VariantBDwellingPbrMaterial)
                {
                    if (geometry.GetSurfaceCount() != 1
                        || geometry.SurfaceGetPrimitiveType(0) != Mesh.PrimitiveType.Triangles
                        || material is not BaseMaterial3D pbr || pbr.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
                        || pbr.NextPass is not null)
                        throw new InvalidDataException($"Variant B candidate PBR material is not a single opaque pass: {name}.");
                    mesh.Transform = live.Transform;
                    mesh.Visible = live.Visible;
                    mesh.CastShadow = live.CastShadow;
                    mesh.MaterialOverlay = live.MaterialOverlay;
                    if (VariantBDwellingPaintTint(live) is { } tint)
                        mesh.SetSurfaceOverrideMaterial(0, TintedVariantBDwellingMaterial(pbr, tint, tintedMaterials));
                    pbrMeshes.Add(mesh);
                }
                else
                {
                    if (geometry.GetSurfaceCount() != live.Mesh!.GetSurfaceCount())
                        throw new InvalidDataException($"Variant B preserved source surface count changed: {name}.");
                    for (var surface = 0; surface < geometry.GetSurfaceCount(); surface++)
                    {
                        if (geometry.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles
                            || geometry.SurfaceGetMaterial(surface)?.ResourceName
                                != live.Mesh.SurfaceGetMaterial(surface)?.ResourceName)
                            throw new InvalidDataException($"Variant B preserved source material changed: {name} surface {surface}.");
                    }
                    preservedMeshes.Add(mesh);
                }
            }
            if (pbrMeshes.Count != 93 || preservedMeshes.Count != 23)
                throw new InvalidDataException($"Variant B candidate has {pbrMeshes.Count} generated and {preservedMeshes.Count} source-material meshes; expected 93 and 23.");

            foreach (var mesh in preservedMeshes)
            {
                candidateRoot.RemoveChild(mesh);
                mesh.Free();
            }
            ClearExtractedSceneOwnership(candidateRoot);
            candidateRoot.Name = VariantBDwellingMountedName;
            candidateRoot.SetMeta("presentationOnly", true);
            candidateRoot.SetMeta("visualOnly", true);
            candidateRoot.SetMeta("assetSource", VariantBDwellingPbrPath);
            candidateRoot.SetMeta("opaqueReplacementFor", VariantBDwellingName);
            candidateRoot.SetMeta("collisionOwner", "unchanged original authored household collision");
            source.AddChild(candidateRoot);
            candidateAttached = true;
            importedRoot = null;

            foreach (var mesh in pbrMeshes)
            {
                var live = sourceByName[mesh.Name.ToString()];
                hiddenOriginals.Add((live, live.Visible));
                live.Visible = false;
            }
            source.SetMeta("variantBDwellingPbrV2MeshCount", pbrMeshes.Count);
            source.SetMeta("variantBDwellingPbrV2PreservedRoofDetailCount", preservedMeshes.Count);
            source.SetMeta("variantBDwellingPbrV2PreservedGlassCount", sourceGlass.Length);
            source.SetMeta("variantBDwellingPbrV2PreservedSnowCount", sourceSnow.Length);
            candidateRoot = null;
        }
        catch
        {
            foreach (var (mesh, visible) in hiddenOriginals)
                if (GodotObject.IsInstanceValid(mesh)) mesh.Visible = visible;
            if (candidateAttached && candidateRoot is not null && GodotObject.IsInstanceValid(candidateRoot))
            {
                source.RemoveChild(candidateRoot);
                candidateRoot.Free();
                candidateRoot = null;
            }
            throw;
        }
        finally
        {
            if (importedRoot is not null && GodotObject.IsInstanceValid(importedRoot)) importedRoot.Free();
            if (candidateRoot is not null && GodotObject.IsInstanceValid(candidateRoot)
                && candidateRoot.GetParent() is null)
                candidateRoot.Free();
        }
    }

    private static Color? VariantBDwellingPaintTint(MeshInstance3D live)
    {
        if (live.MaterialOverride is { } instancePaint)
            return VariantBDwellingMaterialTint(instancePaint, live.Name.ToString());
        if (live.Mesh?.SurfaceGetMaterial(0)?.ResourceName == "URMAN_Wood_Dark")
        {
            var surfacePaint = live.GetSurfaceOverrideMaterial(0)
                ?? throw new InvalidDataException($"Variant B wood-dark paint override is missing: {live.Name}.");
            return VariantBDwellingMaterialTint(surfacePaint, live.Name.ToString());
        }
        return null;
    }

    private static Color VariantBDwellingMaterialTint(Material paint, string meshName)
    {
        if (paint is ShaderMaterial shader)
        {
            var color = shader.GetShaderParameter("base_color");
            if (color.VariantType == Variant.Type.Color) return color.AsColor();
        }
        else if (paint is BaseMaterial3D material)
        {
            return material.AlbedoColor;
        }
        throw new InvalidDataException($"Variant B paint has no readable color tint: {meshName}.");
    }

    private static BaseMaterial3D TintedVariantBDwellingMaterial(
        BaseMaterial3D candidate,
        Color tint,
        Dictionary<(ulong MaterialId, Color Tint), BaseMaterial3D> cache)
    {
        var key = (candidate.GetInstanceId(), tint);
        if (cache.TryGetValue(key, out var material)) return material;
        material = candidate.Duplicate() as BaseMaterial3D
            ?? throw new InvalidDataException("Variant B PBR material could not be duplicated for paint tint.");
        material.AlbedoColor = candidate.AlbedoColor * tint;
        cache.Add(key, material);
        return material;
    }
}
