using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Host-independent contract for every environment family exported by the
/// project-original modular GLB. The test uses the production presentation
/// adapter but never attaches its provisional gameplay proxy: it validates the
/// imported artifact, selective visibility, LOD policy, semantic materials and
/// removal of imported physics without depending on a local Blender runtime.
/// </summary>
public partial class GeneratedModularKitContractSmokeTest : Node
{
    private const float Lod0End = 24f;
    private const float Lod0EndMargin = 3f;
    private const float Lod1Begin = 18f;
    private const float Lod1BeginMargin = 3f;
    private const float Lod1End = 72f;
    private const float Lod1EndMargin = 6f;

    private static readonly FamilyContract[] Families =
    [
        new("HouseA_", 12, ["plaster", "wood_facade", "stone", "shader"]),
        new("FenceA_", 6, ["wood_fence"]),
        new("RoadDirt_", 1, ["earth"]),
        new("PineA_", 2, ["wood_bark", "foliage"]),
        new("TableA_", 5, ["wood_furniture"]),
        new("OldPc_", 8, ["plaster", "shader"]),
        new("WellA_", 7, ["stone", "wood_prop", "shader"]),
        new("WoodpileA_", 4, ["wood_bark"]),
        new("GateA_", 4, ["wood_fence", "cloth"])
    ];

    public override async void _Ready()
    {
        PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests = true;
        var host = new Node3D { Name = "GeneratedModularKitContractHost" };
        AddChild(host);

        try
        {
            if (ResourceLoader.Load<PackedScene>(GeneratedModularKitDressing.ScenePath) is null)
            {
                Fail($"Generated kit contract could not load {GeneratedModularKitDressing.ScenePath}.");
                return;
            }

            foreach (var family in Families)
            {
                Node3D instance;
                try
                {
                    instance = GeneratedModularKitDressing.AttachPresentationOnly(
                        host,
                        $"contract-{family.Prefix.ToLowerInvariant()}",
                        [family.Prefix],
                        Vector3.Zero,
                        uniformScale: 1f,
                        yawDegrees: 0f);
                }
                catch (Exception exception)
                {
                    Fail($"Generated kit contract could not attach {family.Prefix}: {exception.Message}");
                    return;
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var error = ValidateFamily(instance, family);
                await GodotSmokeCleanup.ReleaseAsync(instance);
                if (error.Length > 0)
                {
                    Fail($"Generated kit contract failed for {family.Prefix}: {error}");
                    return;
                }
            }

            GD.Print("generated-modular-kit-contract-smoke: 10 GLB environment families pass exact LOD/material/presentation-only contracts");
            await GodotSmokeCleanup.ReleaseAsync(host);
            PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests = false;
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Fail($"Generated kit contract unexpected failure: {exception}");
        }
    }

    private static string ValidateFamily(Node3D instance, FamilyContract family)
    {
        if (instance.GetMeta("assetSource").AsString() != GeneratedModularKitDressing.ScenePath
            || !instance.GetMeta("variant").AsString().StartsWith("contract-", StringComparison.Ordinal)
            || !instance.GetMeta("presentationOnlyInstance").AsBool()
            || instance.GetMeta("collisionShapeCount").AsInt32() != 0
            || instance.GetMeta("importedCollisionObjectsRemoved").AsInt32() <= 0
            || instance.GetMeta("importedCollisionShapesRemoved").AsInt32() <= 0
            || instance.GetMeta("collisionPolicy").AsString().Contains("presentation-only instance has no physics body", StringComparison.Ordinal) == false)
        {
            return "presentation-only source, imported-collision removal or metadata contract is missing";
        }

        var collisionObjects = instance
            .FindChildren("*", nameof(CollisionObject3D), recursive: true, owned: false)
            .OfType<CollisionObject3D>()
            .ToArray();
        var collisionShapes = instance
            .FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false)
            .OfType<CollisionShape3D>()
            .ToArray();
        if (collisionObjects.Length != 0 || collisionShapes.Length != 0)
        {
            return $"presentation-only import retained physics descendants ({collisionObjects.Length} CollisionObject3D, {collisionShapes.Length} CollisionShape3D)";
        }

        var allMeshes = instance
            .FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false)
            .OfType<MeshInstance3D>()
            .ToArray();
        var visible = allMeshes.Where(mesh => mesh.Visible).ToArray();
        var visibleLod0 = visible.Where(mesh => NameOf(mesh).Contains("_LOD0", StringComparison.Ordinal)).ToArray();
        var visibleLod1 = visible.Where(mesh => NameOf(mesh).Contains("_LOD1", StringComparison.Ordinal)).ToArray();
        if (visible.Length != family.ExpectedPerLod * 2
            || visibleLod0.Length != family.ExpectedPerLod
            || visibleLod1.Length != family.ExpectedPerLod
            || visible.Any(mesh => !NameOf(mesh).StartsWith(family.Prefix, StringComparison.Ordinal)
                                   || NameOf(mesh).EndsWith("-col", StringComparison.Ordinal))
            || allMeshes.Where(mesh => NameOf(mesh).EndsWith("-col", StringComparison.Ordinal)).Any(mesh => mesh.Visible))
        {
            return $"visible LOD prefix contract is {visibleLod0.Length}/{visibleLod1.Length} of {visible.Length}, expected {family.ExpectedPerLod}/{family.ExpectedPerLod} of {family.ExpectedPerLod * 2}; visible={string.Join(",", visible.Select(NameOf))}";
        }

        if (instance.GetMeta("visibleMeshCount").AsInt32() != visible.Length
            || instance.GetMeta("lod0Count").AsInt32() != family.ExpectedPerLod
            || instance.GetMeta("lod1Count").AsInt32() != family.ExpectedPerLod)
        {
            return "adapter metadata does not match exact visible LOD counts";
        }

        if (visibleLod0.Any(mesh => !MatchesLod0(mesh)) || visibleLod1.Any(mesh => !MatchesLod1(mesh)))
        {
            return "one or more visible meshes violates the 0-24m / 18-72m self-fade policy";
        }

        var lod0Names = visibleLod0.Select(NameOf).ToHashSet(StringComparer.Ordinal);
        var lod1Names = visibleLod1.Select(NameOf).ToHashSet(StringComparer.Ordinal);
        if (lod0Names.Any(name => !lod1Names.Contains(name.Replace("_LOD0", "_LOD1", StringComparison.Ordinal)))
            || lod1Names.Any(name => !lod0Names.Contains(name.Replace("_LOD1", "_LOD0", StringComparison.Ordinal))))
        {
            return "visible LOD0/LOD1 names are not a one-to-one published pair";
        }

        if (family.Prefix == "OldPc_")
        {
            var requiredHeroDetails = new HashSet<string>(StringComparer.Ordinal)
            {
                "OldPc_DriveSlot_LOD0",
                "OldPc_DriveSlot_LOD1",
                "OldPc_LabelPlate_LOD0",
                "OldPc_LabelPlate_LOD1"
            };
            var actualHeroDetails = visible
                .Select(NameOf)
                .Where(name => requiredHeroDetails.Contains(name))
                .ToHashSet(StringComparer.Ordinal);
            if (!actualHeroDetails.SetEquals(requiredHeroDetails))
            {
                return $"OldPc hero-detail pair contract failed: {string.Join(", ", actualHeroDetails.OrderBy(name => name, StringComparer.Ordinal))}";
            }
        }

        // Blender names the two source collision render meshes HouseA-col and
        // OldPc-col. Godot's importer normalizes the hyphenated node names to
        // HouseA and OldPc, so compare the normalized logical names and reject
        // every other hidden non-LOD helper.
        var hiddenNonLodNames = allMeshes
            .Where(mesh => !IsPublishedLodName(NameOf(mesh)))
            .Select(mesh => NormalizeImportedCollisionName(NameOf(mesh)))
            .ToHashSet(StringComparer.Ordinal);
        if (!hiddenNonLodNames.SetEquals(["HouseA", "OldPc"]))
        {
            return $"unexpected hidden imported collision render meshes: {string.Join(", ", hiddenNonLodNames.OrderBy(name => name, StringComparer.Ordinal))}";
        }

        var actualOwners = visible
            .Select(mesh => mesh.GetMeta("painterlyMaterial").AsString())
            .ToHashSet(StringComparer.Ordinal);
        if (!actualOwners.SetEquals(family.AllowedSemanticOwners)
            || visible.Any(mesh => mesh.MaterialOverride is not ShaderMaterial
                                   || mesh.GetMeta("presentationOwnership").AsString() != "presentation-only"
                                   || mesh.GetMeta("collisionPolicy").AsString().Contains("no physics body", StringComparison.Ordinal) == false))
        {
            return $"ShaderMaterial semantic-owner contract failed (owners: {string.Join(", ", actualOwners.OrderBy(owner => owner, StringComparer.Ordinal))})";
        }

        return string.Empty;
    }

    private static bool MatchesLod0(MeshInstance3D mesh) =>
        Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, 0f)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, 0f)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, Lod0End)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, Lod0EndMargin)
        && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
        && mesh.GetMeta("visibilityRange").AsString() == "0-24m";

    private static bool MatchesLod1(MeshInstance3D mesh) =>
        Mathf.IsEqualApprox(mesh.VisibilityRangeBegin, Lod1Begin)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeBeginMargin, Lod1BeginMargin)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeEnd, Lod1End)
        && Mathf.IsEqualApprox(mesh.VisibilityRangeEndMargin, Lod1EndMargin)
        && mesh.VisibilityRangeFadeMode == GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
        && mesh.GetMeta("visibilityRange").AsString() == "18-72m";

    private static string NameOf(Node node) => node.Name.ToString();

    private static bool IsPublishedLodName(string name) =>
        name.Contains("_LOD0", StringComparison.Ordinal)
        || name.Contains("_LOD1", StringComparison.Ordinal);

    private static string NormalizeImportedCollisionName(string name) =>
        name.EndsWith("-col", StringComparison.Ordinal) ? name[..^4] : name;

    private void Fail(string message)
    {
        PainterlyMaterialLibrary.SuppressTextureLoadsForHeadlessTests = false;
        GD.PushError(message);
        GetTree().Quit(1);
    }

    private sealed record FamilyContract(string Prefix, int ExpectedPerLod, string[] AllowedSemanticOwners);
}
