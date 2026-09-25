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

    /// <summary>
    /// Addressed houses standing inside a neighbour (author, 2026-09-25: "убери
    /// рандомные постройки застрявшие в текстурах"). Each moves to its own
    /// plot; building, parcel and address ids stay, the registry imports the
    /// new place. Targets are the anchors measured in the 2026-09-16 address
    /// remediation proposal where it has one (H014, H019, H024, H044, H045,
    /// H046), otherwise the nearest free plot beside the old one.
    /// </summary>
    internal static readonly (string Name, float X, float Z, string Reason)[] OverlapRelocations =
    [
        ("ReturnStreetDistantLowFacade", -24f, -47f, "H024 inside ReturnWestFarHouse1Silhouette; proposal west plot"),
        ("ArrivalLeftHorizonDomesticFacade", -31.5f, 29.5f, "H045 inside WestArrivalMidFacade; proposal west plot"),
        ("ArrivalReverseWestDomesticFacade", -11.5f, 35.5f, "H044 inside ArrivalWestNearAuthoredTimberGableParcel; proposal plot"),
        ("FapReverseWestDomesticFacade", 6.2f, -49.6f, "H046 inside ReturnEastHouseA8Silhouette; proposal plot"),
        ("MainStreetEastNeighborFacade", 9.8f, 4f, "H019 inside ArrivalForwardEastFacade; proposal plot"),
        ("ArrivalWestLateralFacade", -31.5f, 17f, "H014 inside ArrivalWestNearAuthoredTimberGableParcel; proposal plot"),
        ("ArrivalReverseEdgeHouseWest", -7.8f, 58.5f, "H001 inside ArrivalFarWestFacade; next plot north"),
        ("ArrivalReverseFarEastHouse", 18.5f, 54f, "H005 inside ArrivalReverseEdgeHouseEast; next plot east"),
        ("ConnectiveEastHouseA6Silhouette", 8.5f, -30f, "H022 overlapping FapOppositeFieldNeighborHouse; 2 m north"),
        ("ReturnEastHouseA8Silhouette", 9.5f, -45.3f, "H023 overlapping FapOppositeFieldNeighborHouse; into the plot H046 left"),
        // Sheds follow the houses they belong to.
        ("MainStreetEastNeighborShed", 5.3f, 8.5f, "H019's shed, beside its house on the new plot"),
        ("FapReverseWestDomesticShed", 6.0f, -54.5f, "H046's shed, behind its house on the new plot")
    ];

    private void RelocateOverlappingHouses()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        foreach (var (name, x, z, reason) in OverlapRelocations)
        {
            var node = core.FindChild(name, true, false) as Node3D
                ?? throw new InvalidOperationException($"Overlap relocation target is missing: {name}.");
            var from = node.GlobalPosition;
            var lift = (float)(Experiments.AgentBAct1.AgentBAct1HeightField.Ground(x, z)
                - Experiments.AgentBAct1.AgentBAct1HeightField.Ground(from.X, from.Z));
            node.GlobalPosition = new Vector3(x, from.Y + lift, z);
            node.SetMeta("overlapRelocation", $"2026-09-25: {reason}; from ({from.X:0.0},{from.Z:0.0})");
        }
        GD.Print($"act1-overlap-cleanup: relocated={OverlapRelocations.Length}");
    }

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
