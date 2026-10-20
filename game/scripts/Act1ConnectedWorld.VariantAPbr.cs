using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string VariantAPbrModelPath = "res://assets/models/act1/neighbor-house-a-pbr-v2.glb";
    private const string VariantAPbrCatalogId = "urman.catalog:urman_village_exterior_kit/villageparcel-varianta-timbergable";
    private const string VariantAPbrComponentName = "VillageParcel_VariantA_TimberGable_Dwelling";
    private const string VariantAPbrDwellingPath = "Visual/VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Dwelling";
    private const string VariantAPbrImportedRootName = "VillageParcel_VariantA_TimberGable_Dwelling";
    private const string VariantAPbrMountedRootName = "VariantAPbrV2Opaque";
    private const string VariantAPbrMeshPrefix = "VariantA_TimberGable_Dwelling_";

    /// <summary>
    /// Replaces the opaque presentation of current addressed generic Variant A
    /// households after their source geometry has supplied contacts and collision.
    /// AuthoredWorld keeps placement and explicit Studio model overrides.
    /// </summary>
    private void GraftPrivateVariantAPbrFamily()
    {
        using var naming = InvariantNameScope();
        var authoredWorld = AuthoredWorld
            ?? throw new InvalidOperationException("The authored world is missing before the Variant A PBR graft.");
        var houseIds = authoredWorld.ObjectIds
            .Where(id => authoredWorld.IsAddressedCatalogProp(id, VariantAPbrCatalogId)
                && !authoredWorld.HasHouseModelOverride(id))
            .ToArray();
        if (houseIds.Length == 0) return;

        // One packed scene shares imported meshes/materials across all instances.
        var packed = ResourceLoader.Load<PackedScene>(VariantAPbrModelPath)
            ?? throw new InvalidOperationException($"The Variant A PBR candidate is missing: {VariantAPbrModelPath}.");
        foreach (var houseId in houseIds)
        {
            var houseRoot = authoredWorld.ObjectRoot(houseId)
                ?? throw new InvalidOperationException($"The addressed Variant A house is missing: {houseId}.");
            if (!houseRoot.IsVisibleInTree())
                throw new InvalidOperationException($"The addressed Variant A house is not visible: {houseId}.");
            GraftVariantAPbr(houseRoot, packed);
        }
    }

    private static void GraftVariantAPbr(Node3D houseRoot, PackedScene packed)
    {
        var sourceComponent = houseRoot.GetNodeOrNull<Node3D>(VariantAPbrDwellingPath)
            ?? throw new InvalidOperationException("The addressed house lost its exact Variant A dwelling component.");
        if (!string.Equals(sourceComponent.Name.ToString(), VariantAPbrComponentName, StringComparison.Ordinal)
            || !sourceComponent.IsVisibleInTree())
        {
            throw new InvalidOperationException("The addressed Variant A dwelling is not the active visible source component.");
        }
        if (sourceComponent.GetNodeOrNull<Node3D>(VariantAPbrMountedRootName) is not null)
            throw new InvalidOperationException("The addressed Variant A PBR layer was already grafted.");

        var sourceMeshes = VariantAMeshInstances(sourceComponent)
            .Where(mesh => mesh.Name.ToString().StartsWith(VariantAPbrMeshPrefix, StringComparison.Ordinal))
            .ToArray();
        if (sourceMeshes.Any(mesh => mesh.Mesh is null))
            throw new InvalidDataException("The original addressed Variant A dwelling contains an empty semantic mesh node.");
        var sourceNames = UniqueVariantAMeshNames(sourceMeshes, "original addressed Variant A dwelling");
        var sourceBaseNames = sourceNames
            .Where(name => !name.EndsWith("_LogCrown", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (sourceBaseNames.Count != 122)
            throw new InvalidDataException($"The original addressed Variant A dwelling has {sourceBaseNames.Count} base meshes; expected 122.");

        var sourceGlass = sourceBaseNames.Where(name => name.Contains("Glass", StringComparison.Ordinal)).ToArray();
        var sourceSnow = sourceBaseNames.Where(name => name.Contains("Snow", StringComparison.Ordinal)).ToArray();
        // Godot's glTF import validates node names, replacing decimal points with underscores.
        var expectedCornerNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "VariantA_TimberGable_Dwelling_Corner_-3_0_-1_3_LOD0",
            "VariantA_TimberGable_Dwelling_Corner_-3_0_4_5_LOD0",
            "VariantA_TimberGable_Dwelling_Corner_3_0_-1_3_LOD0",
            "VariantA_TimberGable_Dwelling_Corner_3_0_4_5_LOD0",
        };
        var sourceCorners = sourceBaseNames
            .Where(name => name.StartsWith("VariantA_TimberGable_Dwelling_Corner_", StringComparison.Ordinal))
            .ToArray();
        var sourceCornerNames = sourceCorners.ToHashSet(StringComparer.Ordinal);
        var excludedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var excluded in sourceGlass.Concat(sourceSnow).Concat(sourceCorners))
        {
            if (!excludedNames.Add(excluded))
                throw new InvalidDataException("The original addressed Variant A glass, snow and corner exclusions overlap.");
        }
        if (sourceGlass.Length != 9 || sourceSnow.Length != 1 || sourceCorners.Length != 4
            || !sourceCornerNames.SetEquals(expectedCornerNames)
            || excludedNames.Count != 14)
        {
            throw new InvalidDataException(
                $"The original addressed Variant A preserved layer contract changed (glass={sourceGlass.Length}, snow={sourceSnow.Length}, corners={sourceCorners.Length}).");
        }

        var opaqueNames = sourceBaseNames.Where(name => !excludedNames.Contains(name))
            .ToHashSet(StringComparer.Ordinal);
        if (opaqueNames.Count != 108)
            throw new InvalidDataException($"The original addressed Variant A opaque contract has {opaqueNames.Count} meshes; expected 108.");
        var expectedCrowns = opaqueNames
            .Where(name => name.EndsWith("_Wall_LOD0", StringComparison.Ordinal))
            .Select(name => name.Replace("_Wall_LOD0", "_LogCrown", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (expectedCrowns.Count != 7)
            throw new InvalidDataException($"The original addressed Variant A source exposes {expectedCrowns.Count} wall crowns; expected 7.");
        var sourceCrowns = sourceNames.Where(name => name.EndsWith("_LogCrown", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (sourceCrowns.Count != 7 || !sourceCrowns.SetEquals(expectedCrowns))
            throw new InvalidDataException("The existing addressed dwelling does not contain exactly the seven expected generated wall crowns.");

        Node3D? importedRoot = null;
        Node3D? candidateRoot = null;
        var candidateAttached = false;
        var hiddenOriginals = new List<(MeshInstance3D Mesh, bool WasVisible)>();
        try
        {
            importedRoot = packed.Instantiate<Node3D>()
                ?? throw new InvalidOperationException($"The Variant A PBR candidate did not instantiate: {VariantAPbrModelPath}.");

            // FindDescendants intentionally excludes its root, so include a
            // matching scene root explicitly before searching nested roots.
            var importedRoots = new List<Node3D>();
            if (string.Equals(importedRoot.Name.ToString(), VariantAPbrImportedRootName, StringComparison.Ordinal))
                importedRoots.Add(importedRoot);
            importedRoots.AddRange(FindDescendants<Node3D>(importedRoot)
                .Where(node => string.Equals(node.Name.ToString(), VariantAPbrImportedRootName, StringComparison.Ordinal)));
            if (importedRoots.Count != 1)
                throw new InvalidDataException($"The Variant A PBR scene contains {importedRoots.Count} authored roots; expected exactly one.");

            candidateRoot = importedRoots[0];
            var importedBasis = ComposeImportedAncestorBasis(candidateRoot);
            if (!IsIdentityBasis(importedBasis) || !IsIdentityTransform(candidateRoot.Transform))
                throw new InvalidDataException("The Variant A PBR authored dwelling root must have identity basis and transform.");

            var candidateNodes = VariantAMeshInstances(candidateRoot).ToArray();
            if (candidateNodes.Length != 115 || candidateNodes.Any(mesh => mesh.Mesh is null))
                throw new InvalidDataException($"The Variant A PBR root has {candidateNodes.Length} mesh nodes; expected 115 non-empty meshes.");
            var candidateNames = UniqueVariantAMeshNames(candidateNodes, "Variant A PBR candidate");
            if (candidateNames.Any(name => !name.StartsWith(VariantAPbrMeshPrefix, StringComparison.Ordinal)))
                throw new InvalidDataException("The Variant A PBR candidate contains a mesh outside its authored semantic prefix.");
            if (candidateNames.Any(name => name.Contains("Glass", StringComparison.Ordinal)
                    || name.Contains("Snow", StringComparison.Ordinal)
                    || expectedCornerNames.Contains(name)))
            {
                throw new InvalidDataException("The Variant A PBR candidate must leave the existing glass, snow and corners visible.");
            }

            var candidateCrowns = candidateNames
                .Where(name => name.EndsWith("_LogCrown", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            var candidateOpaque = candidateNames
                .Where(name => !name.EndsWith("_LogCrown", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            var expectedNames = new HashSet<string>(opaqueNames, StringComparer.Ordinal);
            expectedNames.UnionWith(expectedCrowns);
            if (candidateOpaque.Count != 108 || candidateCrowns.Count != 7
                || !candidateOpaque.SetEquals(opaqueNames)
                || !candidateCrowns.SetEquals(expectedCrowns)
                || candidateNames.Count != 115 || !candidateNames.SetEquals(expectedNames))
            {
                throw new InvalidDataException("The Variant A PBR semantic mesh names do not match the live opaque-plus-crown contract.");
            }
            if (candidateRoot is CollisionObject3D or CollisionShape3D
                || FindDescendants<Node>(candidateRoot).Any(node => node is CollisionObject3D or CollisionShape3D))
            {
                throw new InvalidDataException("The Variant A PBR candidate must remain presentation-only.");
            }

            var sourceReplacementMeshes = sourceMeshes
                .Where(mesh => candidateNames.Contains(mesh.Name.ToString()))
                .ToArray();
            var liveReplacementNames = sourceReplacementMeshes.Select(mesh => mesh.Name.ToString())
                .ToHashSet(StringComparer.Ordinal);
            if (sourceReplacementMeshes.Length != 115 || !liveReplacementNames.SetEquals(expectedNames))
            {
                throw new InvalidDataException(
                    $"The live addressed dwelling has {sourceReplacementMeshes.Length} opaque-plus-crown meshes to replace; expected exactly 115.");
            }

            var liveOpaque = sourceReplacementMeshes
                .Where(mesh => opaqueNames.Contains(mesh.Name.ToString()))
                .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);
            if (liveOpaque.Values.Any(mesh => !ReferenceEquals(mesh.GetParent(), sourceComponent))
                || candidateNodes.Any(mesh => !ReferenceEquals(mesh.GetParent(), candidateRoot)))
            {
                throw new InvalidDataException("The Variant A live and candidate meshes must be direct dwelling children before copying local transforms.");
            }
            foreach (var mesh in candidateNodes)
            {
                var name = mesh.Name.ToString();
                if (!liveOpaque.TryGetValue(name, out var live)) continue;
                // DressParcel lifts selected roofs for added storeys. Copy node
                // placement, keeping the candidate's geometry, UVs and normals.
                mesh.Transform = live.Transform;
                mesh.Visible = live.Visible;
                // Regrade also overrides plain timber; only household paint
                // belongs above the generated PBR materials. Crowns keep their
                // constructively corrected first-floor geometry and placement.
                if (name.Contains("_Jamb", StringComparison.Ordinal)
                    || name.Contains("_Rail", StringComparison.Ordinal)
                    || name.Contains("_Verge", StringComparison.Ordinal)
                    || name.Contains("_Mullion", StringComparison.Ordinal))
                {
                    mesh.MaterialOverride = live.MaterialOverride;
                }
            }

            // The exported root has no preview-board transform or added Yup
            // basis. Parent it under the live dwelling at identity so its exact
            // authored geometry inherits the live household's scale, offset and placement.
            if (!ReferenceEquals(candidateRoot, importedRoot))
            {
                var importedParent = candidateRoot.GetParent()
                    ?? throw new InvalidDataException("The Variant A authored root is detached from its import scene.");
                importedParent.RemoveChild(candidateRoot);
                ClearExtractedSceneOwnership(candidateRoot);
                importedRoot.Free();
                importedRoot = null;
            }
            else
            {
                ClearExtractedSceneOwnership(candidateRoot);
                importedRoot = null;
            }

            candidateRoot.Transform = Transform3D.Identity;
            candidateRoot.Name = VariantAPbrMountedRootName;
            candidateRoot.SetMeta("presentationOnly", true);
            candidateRoot.SetMeta("visualOnly", true);
            candidateRoot.SetMeta("assetSource", VariantAPbrModelPath);
            candidateRoot.SetMeta("opaqueReplacementFor", VariantAPbrComponentName);
            sourceComponent.AddChild(candidateRoot);
            candidateAttached = true;

            foreach (var mesh in sourceReplacementMeshes)
            {
                hiddenOriginals.Add((mesh, mesh.Visible));
                mesh.Visible = false;
            }
            sourceComponent.SetMeta("variantAPbrOpaqueMeshCount", candidateOpaque.Count);
            sourceComponent.SetMeta("variantAPbrGeneratedCrownCount", candidateCrowns.Count);
            sourceComponent.SetMeta("variantAPbrPreservedGlassCount", sourceGlass.Length);
            sourceComponent.SetMeta("variantAPbrPreservedSnowCount", sourceSnow.Length);
            sourceComponent.SetMeta("variantAPbrPreservedCornerCount", sourceCorners.Length);
            candidateRoot = null;
        }
        catch
        {
            foreach (var (mesh, wasVisible) in hiddenOriginals)
            {
                if (GodotObject.IsInstanceValid(mesh)) mesh.Visible = wasVisible;
            }
            if (candidateAttached && candidateRoot is not null && GodotObject.IsInstanceValid(candidateRoot))
            {
                sourceComponent.RemoveChild(candidateRoot);
                candidateRoot.Free();
            }
            throw;
        }
        finally
        {
            if (importedRoot is not null && GodotObject.IsInstanceValid(importedRoot)) importedRoot.Free();
            if (candidateRoot is not null && GodotObject.IsInstanceValid(candidateRoot)
                && candidateRoot.GetParent() is null)
            {
                candidateRoot.Free();
            }
        }
    }

    private static IEnumerable<MeshInstance3D> VariantAMeshInstances(Node root)
    {
        if (root is MeshInstance3D rootMesh) yield return rootMesh;
        foreach (var mesh in FindDescendants<MeshInstance3D>(root)) yield return mesh;
    }

    private static HashSet<string> UniqueVariantAMeshNames(IEnumerable<MeshInstance3D> meshes, string owner)
    {
        var names = meshes.Select(mesh => mesh.Name.ToString()).ToArray();
        var unique = new HashSet<string>(names, StringComparer.Ordinal);
        if (unique.Count != names.Length)
            throw new InvalidDataException($"The {owner} contains duplicate semantic mesh names.");
        return unique;
    }

    private static bool IsIdentityTransform(Transform3D transform) =>
        transform.Origin.IsEqualApprox(Vector3.Zero) && IsIdentityBasis(transform.Basis);

    private static bool IsIdentityBasis(Basis basis) =>
        basis.X.IsEqualApprox(new Vector3(1f, 0f, 0f))
        && basis.Y.IsEqualApprox(new Vector3(0f, 1f, 0f))
        && basis.Z.IsEqualApprox(new Vector3(0f, 0f, 1f));
}
