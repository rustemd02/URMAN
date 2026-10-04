using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Regex LooseParcelDressing = new(@"^VillageParcel_.*_(Branch|Moss|Sedge|Shrub)_", RegexOptions.Compiled);
    private static readonly Regex OldParcelBoundary = new(@"^Variant[ABC]_Yard_(Post|.*Rail|Picket|Plank|Threshold|Gate|FoundationStone)", RegexOptions.Compiled);

    private void ComposeCleanVillageYards()
    {
        var hidden = 0; var homes = new HashSet<string>(StringComparer.Ordinal);
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        foreach (var mesh in FindDescendants<MeshInstance3D>(core).ToArray())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            var name = mesh.Name.ToString();
            if (!LooseParcelDressing.IsMatch(name) && !OldParcelBoundary.IsMatch(name) && name != "PorchTrimScrap") continue;
            // Functional bypass gates, clue/carry props and the player's working yard
            // retain their physical owners. This pass clears anonymous kit dressing.
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
            mesh.SetMeta("yardCleanup", LooseParcelDressing.IsMatch(name) ? "loose branches, shrub fragments and litter-like summer ground dressing removed from occupied winter yard"
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
}
