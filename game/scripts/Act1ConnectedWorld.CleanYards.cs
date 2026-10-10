using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Regex LooseParcelDressing = new(
        @"^VillageParcel_(?:VariantA_TimberGable|VariantB_PlasterAnnex|VariantC_BanyaYard)_(?:Branch_\d{2}_LOD0|Moss_\d{2}_LOD0|Sedge_\d{2}_Blade_\d{2}_LOD0|Shrub_\d{2}_Clump_\d{2}_LOD0)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OldParcelBoundary = new(@"^Variant[ABC]_Yard_(Post|.*Rail|Picket|Plank|Threshold|Gate|FoundationStone)", RegexOptions.Compiled);

    private void ComposeCleanVillageYards()
    {
        var hidden = 0; var homes = new HashSet<string>(StringComparer.Ordinal);
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        foreach (var mesh in FindDescendants<MeshInstance3D>(core).ToArray())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            var name = mesh.Name.ToString();
            var isLooseDressing = LooseParcelDressing.IsMatch(name);
            var isRetiredFencePost = IsOrphanedGeneratedFencePost(mesh, core);
            if (!isLooseDressing && !OldParcelBoundary.IsMatch(name) && name != "PorchTrimScrap" && !isRetiredFencePost) continue;
            if (isLooseDressing || isRetiredFencePost)
            {
                // These are exact visual leaves; relocation/container metadata
                // alone does not make the mesh gameplay-owned. Keep physical,
                // interaction, mechanism and carryable owners intact.
                if (HasFunctionalYardOwner(mesh, core)) continue;
            }
            else
            {
                // Keep the existing boundary-fence and porch cleanup safeguards unchanged.
                var protectedOwner = false;
                for (Node? owner = mesh; owner is not null && owner != core; owner = owner.GetParent())
                {
                    var ownerName = owner.Name.ToString();
                    if (ownerName is "TamaraFenceQuest" or "BabaiYardSideGateExploration" or "YardMechanisms"
                        || ownerName.Contains("Bypass", StringComparison.Ordinal) || ownerName.Contains("Connective", StringComparison.Ordinal)
                        || owner.HasMeta("worldPropId") || owner.HasMeta("mechanismOwner") || owner.HasMeta("babaiRelocated"))
                    { protectedOwner = true; break; }
                }
                if (protectedOwner || FindDescendants<InteractionTarget>(mesh).Any()) continue;
            }
            mesh.SetMeta("yardCleanup", isLooseDressing
                ? "loose branches, shrub fragments and litter-like summer ground dressing removed from occupied winter yard"
                : isRetiredFencePost
                    ? "orphaned generated fence post removed with its already-suppressed visual-only rail"
                    : "redundant kit fence fragment removed; actual lot boundary belongs to YardFences");
            HidePresentationNode(mesh);
            hidden++;
            var home = mesh.GetParent();
            while (home is not null && home != core && !home.HasMeta("logicalAnchor") && !home.HasMeta(AuthoredWorldPlot.AuthoredIdMeta)) home = home.GetParent();
            homes.Add(home?.GetPath().ToString() ?? mesh.GetParent().GetPath().ToString());
        }
        SetMeta("villageYardCleanupHidden", hidden);
        SetMeta("villageYardCleanupOwners", homes.Count);
        GD.Print($"act1-clean-yards: removed={hidden} owners={homes.Count}; real fences, storage and interactions retained");
    }

    private static bool HasFunctionalYardOwner(Node mesh, Node core)
    {
        if (FindDescendants<Node>(mesh).Any(IsProtectedGameplayNode)) return true;
        for (Node? owner = mesh; owner is not null && owner != core; owner = owner.GetParent())
        {
            if (IsProtectedGameplayNode(owner) || owner is CarryableProp
                || owner.HasMeta("mechanismOwner") || owner.HasMeta("carryItemId")
                || owner.HasMeta("collisionOwner")
                    && !string.Equals(owner.GetMeta("collisionOwner").AsString(), "none", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsOrphanedGeneratedFencePost(MeshInstance3D post, Node core)
    {
        if (!HasTrueMeta(post, "visualOnly") || post.Mesh is not BoxMesh box
            || !box.Size.IsEqualApprox(new Vector3(0.13f, 1.15f, 0.13f)))
            return false;

        var name = post.Name.ToString();
        var postToken = name.LastIndexOf("Post", StringComparison.Ordinal);
        if (postToken <= 0 || !int.TryParse(name.AsSpan(postToken + "Post".Length), out _)) return false;
        var runRailName = name[..postToken] + "Rail";
        if (post.GetParent()?.GetNodeOrNull<MeshInstance3D>(runRailName) is not { } rail
            || rail.IsVisibleInTree() || !HasTrueMeta(rail, "visualOnly") || !HasTrueMeta(rail, "connectedWorldHidden")
            || HasFunctionalYardOwner(rail, core))
            return false;

        // AddVisualFenceRun's posts are exact sibling BoxMesh leaves at this size;
        // their matching rail is the hidden visualOnly mesh from that same source.
        return true;
    }
}
