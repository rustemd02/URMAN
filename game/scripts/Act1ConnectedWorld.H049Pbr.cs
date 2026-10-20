using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string H049ObjectId = "urman.world:act1/open-part/h049-house";
    private const string H049ModelPath = "res://assets/models/act1/h049-outbuilding-pbr-v1.glb";
    private const string H049ComponentPath =
        "Visual/VillageParcel_VariantA_TimberGable/VillageParcel_VariantA_TimberGable_Outbuilding";
    private const string H049MountedName = "H049OutbuildingPbrV1";

    private void GraftH049OutbuildingPbr()
    {
        using var naming = InvariantNameScope();
        var authored = AuthoredWorld
            ?? throw new InvalidOperationException("H049 PBR presentation requires the authored world.");
        if (!authored.IsAddressedCatalogProp(H049ObjectId, VariantAPbrCatalogId)
            || authored.HasHouseModelOverride(H049ObjectId)) return;
        var house = authored.ObjectRoot(H049ObjectId)
            ?? throw new InvalidOperationException("The addressed H049 household is missing.");
        var component = house.GetNodeOrNull<Node3D>(H049ComponentPath)
            ?? throw new InvalidOperationException("H049 lost its original Variant A outbuilding.");
        if (!house.IsVisibleInTree() || !component.IsVisibleInTree()
            || component.GetNodeOrNull<Node3D>(H049MountedName) is not null
            || house.GetNodeOrNull<StaticBody3D>("Collision") is not { } collision
            || collision.GetChildCount() == 0)
            throw new InvalidOperationException("H049 needs its visible original outbuilding and completed source collision before grafting.");

        var originals = VariantAMeshInstances(component).ToArray();
        if (originals.Length != 21 || originals.Any(mesh => mesh.Mesh is null
                || !mesh.Name.ToString().StartsWith("VariantA_Shed_", StringComparison.Ordinal))
            || originals.Select(mesh => mesh.Name.ToString()).Distinct(StringComparer.Ordinal).Count() != 21
            || originals.Count(mesh => mesh.Name == "VariantA_Shed_SideWindow_Inset_LOD0") != 1)
            throw new InvalidDataException("H049 no longer has its 20 opaque source meshes and one window inset.");
        var sourceBounds = originals.Select(mesh => PublicBuildingShell.Bounds(component, mesh))
            .Aggregate((a, b) => a.Merge(b));
        if (!sourceBounds.Position.IsFinite() || !sourceBounds.Size.IsFinite()
            || sourceBounds.Position.DistanceTo(new Vector3(-2.07f, .02f, -1.4307481f)) > .002f
            || sourceBounds.End.DistanceTo(new Vector3(2.07f, 3.12f, 1.445f)) > .002f)
            throw new InvalidDataException("H049 source bounds differ from the measured closed storage shed.");

        var packed = ResourceLoader.Load<PackedScene>(H049ModelPath)
            ?? throw new InvalidOperationException($"The H049 candidate is missing: {H049ModelPath}.");
        Node3D? imported = null;
        Node3D? wrapper = null;
        var hidden = new List<(MeshInstance3D Mesh, bool Visible)>();
        try
        {
            imported = packed.Instantiate<Node3D>()
                ?? throw new InvalidDataException("H049 candidate has no 3D root.");
            var nodes = new Node[] { imported }.Concat(FindDescendants<Node>(imported)).ToArray();
            if (nodes.Any(node => (node.GetType() != typeof(Node3D) && node.GetType() != typeof(MeshInstance3D))
                    || node.GetScript().VariantType != Variant.Type.Nil)
                || nodes.OfType<Node3D>().Any(node => !IsIdentityTransform(node.Transform)))
                throw new InvalidDataException("H049 candidate permits only plain Node3D containers and MeshInstance3D nodes, with identity transforms and no scripts.");
            var meshes = VariantAMeshInstances(imported).ToArray();
            if (meshes.Length != 1 || meshes[0].Mesh is not ArrayMesh candidateMesh
                || candidateMesh.GetSurfaceCount() != 2 || meshes[0].MaterialOverride is not null
                || meshes[0].MaterialOverlay is not null)
                throw new InvalidDataException("H049 candidate must retain one mesh and two imported PBR surfaces.");
            var candidateBounds = candidateMesh.GetAabb();
            if (!candidateBounds.Position.IsFinite() || !candidateBounds.Size.IsFinite()
                || candidateBounds.Position.DistanceTo(new Vector3(-.952642024f, -.696305990f, -.677977920f)) > .0001f
                || candidateBounds.End.DistanceTo(new Vector3(.949992955f, .709483087f, .658375025f)) > .0001f)
                throw new InvalidDataException("H049 candidate must retain its measured raw Meshy bounds.");

            var roofSurface = -1;
            BaseMaterial3D? roofMaterial = null;
            var materialNames = new HashSet<string>(StringComparer.Ordinal);
            for (var surface = 0; surface < 2; surface++)
            {
                if (candidateMesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles
                    || meshes[0].GetSurfaceOverrideMaterial(surface) is not null
                    || candidateMesh.SurfaceGetMaterial(surface) is not BaseMaterial3D material
                    || material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
                    || material.NextPass is not null || !materialNames.Add(material.ResourceName))
                    throw new InvalidDataException("H049 requires two distinct opaque imported PBR materials without extra passes.");
                var expectedTriangles = material.ResourceName switch
                {
                    "BakedMaterial" => 5415,
                    "H049_Roof_PBR" => 123,
                    _ => throw new InvalidDataException("H049 candidate has an unexpected material role.")
                };
                using var arrays = candidateMesh.SurfaceGetArrays(surface);
                var indices = arrays[(int)Mesh.ArrayType.Index];
                var count = indices.VariantType == Variant.Type.Nil
                    ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length
                    : indices.AsInt32Array().Length;
                if (count != expectedTriangles * 3)
                    throw new InvalidDataException("H049 body/roof triangles differ from the accepted source partition.");
                if (material.ResourceName == "H049_Roof_PBR")
                {
                    roofSurface = surface;
                    roofMaterial = material;
                }
            }
            if (roofMaterial is null || roofSurface < 0)
                throw new InvalidDataException("H049 candidate has no isolated roof surface.");
            var roof = (BaseMaterial3D)roofMaterial.Duplicate();
            roof.NextPass = PainterlyMaterialLibrary.ForImportedRoofSnow();
            meshes[0].SetSurfaceOverrideMaterial(roofSurface, roof);

            // The live component already owns parcel growth, clamp and yaw.
            var scale = 3.10f / candidateBounds.Size.Y;
            var sourceCentre = sourceBounds.GetCenter();
            var candidateCentre = candidateBounds.GetCenter();
            wrapper = new Node3D
            {
                Name = H049MountedName,
                Scale = Vector3.One * scale,
                Position = new Vector3(sourceCentre.X, sourceBounds.Position.Y, sourceCentre.Z)
                    - new Vector3(candidateCentre.X, candidateBounds.Position.Y, candidateCentre.Z) * scale
            };
            ClearExtractedSceneOwnership(imported);
            wrapper.AddChild(imported);
            imported = null;
            wrapper.SetMeta("presentationOnly", true);
            wrapper.SetMeta("visualOnly", true);
            wrapper.SetMeta("assetSource", H049ModelPath);
            wrapper.SetMeta("sourceObjectId", H049ObjectId);
            wrapper.SetMeta("replacementFor", H049ComponentPath);
            wrapper.SetMeta("sourceCandidateSha256", "4e7c9d6cedf4eba93beedcde2c7bb2c031c85ab4af7f2050e444de1773c8bd68");
            wrapper.SetMeta("runtimeAssetSha256", "16331d91eab3fb1ad011e044007c14ebd6f160470f40b443dee090bd743de96e");
            wrapper.SetMeta("collisionOwner", "unchanged authored household source triangles");
            component.AddChild(wrapper);
            foreach (var mesh in originals)
            {
                hidden.Add((mesh, mesh.Visible));
                mesh.Visible = false;
            }
            wrapper = null;
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
            if (wrapper is not null && GodotObject.IsInstanceValid(wrapper))
            {
                wrapper.GetParent()?.RemoveChild(wrapper);
                wrapper.Free();
            }
        }
    }
}
