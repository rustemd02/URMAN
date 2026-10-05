using System;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Qibla orientation owner. The mosque complex is authored square to the
/// village grid so its shell, plinth, courtyard and entrance populate the
/// layout; this lane then yaws the whole complex exactly once, before the
/// plinth samples the actual terrain and before the room, door, stairs,
/// minaret, plate anchor and approach path exist, so every one of them
/// inherits the same true bearing instead of compensating inside a straight
/// shell (the old skewed mihrab insert and crooked carpet rows).
///
/// Truth used here: the fictional village has no surveyed coordinate, so the
/// regional proxy is Kazan 55.79N 49.12E and the target is the Kaaba
/// 21.4225N 39.8262E. The initial great-circle bearing is
/// atan2(sin Δλ cos φ2, cos φ1 sin φ2 − sin φ1 cos φ2 cos Δλ) = 195.1725°
/// clockwise from true north. The world frame is north = +Z, east = +X
/// (the same convention MosqueQiblaBearingDegrees and the smoke checks use).
/// </summary>
public partial class Act1ConnectedWorld
{
    /// <summary>The complex yaw that puts the hall's own −Z wall (the mihrab
    /// wall, the carpet rectangle and the minaret axis) on the true qibla:
    /// bearing 195.1725° − 180° = +15.1725° about +Y.</summary>
    internal const float MosqueBuildingQiblaYawDegrees = MosqueQiblaBearingDegrees - 180f;

    /// <summary>Yaw the whole mosque complex onto the qibla about the vertical
    /// line through the authored walk-axis end. That pivot keeps the village
    /// approach threshold where MosqueWalkAxis, the height field and the route
    /// graph already expect it (the gate moves &lt; 0.10 m); everything farther
    /// west swings with the building and the foundation/terrain cut follows in
    /// building-local coordinates. Call this before any terrain sampling or
    /// child build: that ordering is what keeps plinth, floor, shell, roof,
    /// minaret, door, steps, plate anchor and approach path coherent.</summary>
    private void RotateMosqueComplexToQibla(Node3D complex)
    {
        if (complex.HasMeta("mosqueQiblaRotationApplied"))
            throw new InvalidOperationException("The mosque qibla rotation must be applied exactly once per world.");
        if (complex.Name != "VillageMosqueComplex")
            throw new InvalidOperationException("The qibla rotation expects the authored VillageMosqueComplex owner.");
        var approachEnd = AgentBAct1Layout.MosqueWalkAxis[^1];
        var pivot = new Vector3(approachEnd.X, 0f, approachEnd.Y);
        var yaw = new Basis(Vector3.Up, Mathf.DegToRad(MosqueBuildingQiblaYawDegrees));
        var atPivot = new Transform3D(Basis.Identity, pivot);
        // Rotate in world space so the result does not depend on the parent
        // core transform; the rotation is about +Y only, so every floor, stair
        // and gallery height stays exactly where it was.
        complex.GlobalTransform = atPivot * new Transform3D(yaw, Vector3.Zero) * atPivot.AffineInverse() * complex.GlobalTransform;
        complex.SetMeta("mosqueQiblaRotationApplied", true);
        complex.SetMeta("mosqueQiblaBearingDegrees", MosqueQiblaBearingDegrees);
        complex.SetMeta("mosqueBuildingYawDegrees", MosqueBuildingQiblaYawDegrees);
        complex.SetMeta("mosqueQiblaPivotWorld", pivot);
        complex.SetMeta("mosqueQiblaRotationPolicy",
            "single rigid yaw of the whole complex about the MosqueWalkAxis end, applied before plinth terrain sampling, room, door, stairs, plate anchor and address registration; walk-axis threshold moves < 0.10 m, route data stays valid");
        // The address plate is created later from the registration's world
        // SignPoint (already yawed with the room) but its facing comes from the
        // caller's literal Vector3.Right. Realign that facing once the registry
        // has built the plate; the same deferred pattern the frontage
        // reconciliation already uses.
        CallDeferred(nameof(AlignMosqueAddressPlateToQibla));
    }

    /// <summary>Deferred post-build fixup: the mosque plate must face the
    /// yawed hall's real east wall (the registration passes the pre-yaw world
    /// axis). Runs after BuildAddressRegistry; idempotent and silent when the
    /// registry was not built (fixtures).</summary>
    private void AlignMosqueAddressPlateToQibla()
    {
        var complex = GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        if (complex is null || !complex.HasMeta("mosqueQiblaRotationApplied"))
            return;
        var outward = complex.GlobalBasis * Vector3.Right;
        outward.Y = 0;
        if (outward.LengthSquared() < .00000001f)
            return;
        outward = outward.Normalized();
        SetMeta("mosqueAddressPlateQiblaOutward", outward);
        if (GetNodeOrNull<Node3D>("SettlementAddressPresentation/AddressPlate_ADR-MOSQUE") is not { } plate)
        {
            SetMeta("mosqueAddressPlateAligned", false);
            return;
        }
        plate.GlobalBasis = new Basis(Vector3.Up, Mathf.Atan2(outward.X, outward.Z));
        plate.SetMeta("qiblaAlignedToBuildingWall", true);
        plate.SetMeta("qiblaPlateOutward", outward);
        SetMeta("mosqueAddressPlateAligned", true);
    }
}
