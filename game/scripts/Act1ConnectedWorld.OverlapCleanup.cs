using Godot;

namespace Urman.Godot;

/// <summary>
/// Several generation passes placed buildings on the same ground, so in the
/// 2026-09-24 structure audit one house stood inside another in many places.
/// These have no address and no gameplay; the addressed or canonical building
/// at each spot stays. See docs/production/act1_takeover_evidence_2026-09-16/
/// village-structure-audit-20260924/README.md.
/// </summary>
public partial class Act1ConnectedWorld
{
    // Only buildings with no address anywhere under them. Where two addressed
    // houses share one plot (H008/H044/H014, H019, H013, H045, H012, A8/H046),
    // one of them has to move, which is the new village layout's job
    // (ACT1-VILLAGE-LAYOUT), not a hide.
    internal static readonly (string Name, string Reason)[] OverlapSuppressions =
    [
        ("ReturnEastShed", "shed half inside ReturnEastHouseA8Silhouette"),
        ("EastStreetFarHoldingShed", "shed inside FapRightFieldHouse"),
        ("ArrivalEastNearAuthoredShed", "shed inside ArrivalEastNearAuthoredFacade"),
        ("ArrivalForwardWestShed", "shed inside ForwardWestParcel")
    ];

    private void HideOverlappingStructures()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        foreach (var (name, reason) in OverlapSuppressions)
        {
            var node = core.FindChild(name, true, false)
                ?? throw new InvalidOperationException($"Overlap cleanup target is missing: {name}.");
            var gameplay = new[] { node }.Concat(FindDescendants<Node>(node)).Where(IsProtectedGameplayNode).ToArray();
            if (gameplay.Length > 0)
                throw new InvalidOperationException($"Overlap cleanup target carries gameplay: {node.GetPath()} => "
                    + string.Join('|', gameplay.Select(item => item.GetPath().ToString())));
            node.SetMeta("connectedWorldSuppressionReason", "overlap audit 2026-09-24: " + reason);
            HidePresentationNode(node);
        }
        GD.Print($"act1-overlap-cleanup: hidden={OverlapSuppressions.Length}");
    }
}
