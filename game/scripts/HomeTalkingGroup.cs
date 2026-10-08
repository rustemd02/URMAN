using Godot;

namespace Urman.Godot;

/// <summary>
/// VIS-051 — one home talking group. The card asks for the opposite of more
/// furniture: the lamp, the cup, the calendar and the photograph that already
/// stand in the room have to belong to a seated person, and that person has to
/// read before the wallpaper does. This class therefore moves existing pieces
/// only: it never adds, removes or renames a node, never touches a gameplay
/// owner, and it keeps the camera out of it entirely — the framing problem and
/// the placement problem are reported as two separate problems.
/// </summary>
public static class HomeTalkingGroup
{
    /// <summary>The card's own core of the group: three to five meaningful things.
    /// Anything further in the room is background and is left alone.</summary>
    public const int CoreItemCount = 5;

    /// <summary>Family pieces carry meaning (a photograph, a wall calendar): they are
    /// re-seated but never stepped aside, because the room has to stay recognisable.</summary>
    internal static readonly string[] FamilyPieces = ["FamilyPhoto", "WallCalendar", "Photo", "Calendar"];

    public readonly record struct Report(int Considered, int ReSeated, int AlreadyOnSupport, int SteppedAside,
        int FaceBlockedForFraming, float WorstGapMetres, int InstancesBefore, int InstancesAfter, string Detail);

    /// <summary>Seats the existing core group onto the surfaces that really carry it
    /// and inside the seated hand's reach. <paramref name="seatedFloorPoint"/> is the
    /// person's floor point, <paramref name="seatedYawDegrees"/> the direction they
    /// face, <paramref name="guestEyePoint"/> where the player's eye stands during the
    /// conversation. Returns numbers, not opinions: the human decides the frame.</summary>
    public static Report SeatOntoSurfaces(Node3D room, Vector3 seatedFloorPoint, float seatedYawDegrees,
        Vector3 guestEyePoint, params string[] coreItemTokens)
    {
        var tokens = coreItemTokens.Length == 0
            ? new[] { "Lamp", "Cup", "Kettle", "Teapot", "Samovar", "FamilyPhoto", "WallCalendar", "Document" }
            : coreItemTokens;
        var face = seatedFloorPoint + Vector3.Up
            * (RuralPropModels.SeatCrownMetres + RuralPropModels.FaceAboveSeatMetres);
        var before = CountVisibleMeshes(room);
        var considered = 0; var reseated = 0; var already = 0; var aside = 0; var blocked = 0; var worst = 0f;
        var detail = new System.Text.StringBuilder();
        foreach (var item in Members(room, tokens))
        {
            if (considered >= CoreItemCount) break;
            if (HasGameplayOwner(item)) continue;
            considered++;
            var box = item.GlobalTransform * item.GetAabb();
            var centre = box.GetCenter();
            var crown = SupportCrownUnder(room, new Vector2(centre.X, centre.Z), face.Y);
            // A piece is only re-seated when a plate really is meant to carry it: the
            // crown sits just below, at or slightly above its own base. Anything else
            // is hung on a wall or stands on the floor, and moving it from here would
            // be a guess, which is exactly what the card forbids.
            var gap = crown is { } found ? box.Position.Y - found : float.NaN;
            var carried = crown is not null && gap > -.06f && gap < .12f;
            if (carried)
            {
                if (Mathf.Abs(gap) > .010f)
                {
                    item.GlobalPosition += Vector3.Up * (crown!.Value - box.Position.Y);
                    reseated++;
                    worst = Mathf.Max(worst, Mathf.Abs(gap));
                    detail.Append(item.Name).Append(":gap ")
                        .Append(System.Math.Round(gap * 1000f).ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append("mm→0 ");
                }
                else already++;
            }
            else if (crown is null) detail.Append(item.Name).Append(":no-carrier-in-band ");
            // A piece that only accidentally crosses the face line steps aside along
            // the table edge. A family piece is reported for the framing decision
            // instead, because removing it would remove the room's own history.
            var after = item.GlobalTransform * item.GetAabb();
            if (RuralPropModels.CoversFace(face, guestEyePoint, after))
            {
                var isFamily = false;
                foreach (var family in FamilyPieces)
                    if (item.Name.ToString().Contains(family, StringComparison.Ordinal)) { isFamily = true; break; }
                if (isFamily || !carried) { blocked++; detail.Append(item.Name).Append(":blocks-face→framing "); }
                else
                {
                    var across = new Vector3(-Mathf.Cos(Mathf.DegToRad(seatedYawDegrees)), 0f,
                        Mathf.Sin(Mathf.DegToRad(seatedYawDegrees)));
                    var side = (after.GetCenter() - face).Dot(across) >= 0f ? 1f : -1f;
                    var shift = across * (after.Size.Length() * .5f + .08f) * side;
                    shift.Y = 0f;
                    var landing = SupportCrownUnder(room, new Vector2(after.GetCenter().X + shift.X,
                        after.GetCenter().Z + shift.Z), face.Y);
                    if (landing is not null)
                    {
                        item.GlobalPosition += shift + Vector3.Up * (landing.Value - after.Position.Y);
                        aside++;
                        detail.Append(item.Name).Append(":stepped-off-face ");
                    }
                    else blocked++;
                }
            }
        }
        var after2 = CountVisibleMeshes(room);
        if (after2 != before)
            GD.PushError($"VIS-051: the talking group changed the room's visible mesh count {before}→{after2}; "
                + "this pass is allowed to move pieces only");
        var text = detail.ToString();
        GD.Print($"act1-home-group: considered={considered} reseated={reseated} alreadyOnSupport={already} "
            + $"steppedAside={aside} framing={blocked} worstGapMm={worst * 1000f:F0} instances={before}->{after2}");
        return new Report(considered, reseated, already, aside, blocked, worst, before, after2, text);
    }

    private static IEnumerable<MeshInstance3D> Members(Node3D room, string[] tokens)
    {
        foreach (var mesh in room.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            var name = mesh.Name.ToString();
            foreach (var token in tokens)
                if (name.Contains(token, StringComparison.Ordinal)) { yield return mesh; break; }
        }
    }

    private static int CountVisibleMeshes(Node3D room) => room.FindChildren("*", nameof(MeshInstance3D), true, false)
        .OfType<MeshInstance3D>().Count(m => m.IsVisibleInTree() && m.Mesh is not null);

    private static bool HasGameplayOwner(Node3D item) =>
        item.FindChildren("*", nameof(InteractionTarget), true, false).Count > 0 || item is InteractionTarget;

    /// <summary>The crown of the highest finished plate under this plan point that
    /// lies between the seat and a standing elbow. A wall, a floor or a ceiling is
    /// not a carrier: the plate has to be thin enough to be a table top, a shelf or
    /// a sill, and it has to be below the face line.</summary>
    private static float? SupportCrownUnder(Node3D room, Vector2 point, float belowFaceY)
    {
        float? best = null;
        foreach (var mesh in room.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            if (box.Size.Y > .12f || box.Size.Length() > 25f) continue;
            var crown = box.End.Y;
            if (crown < RuralPropModels.SeatCrownMetres - .10f || crown > 1.15f) continue;
            if (crown >= belowFaceY) continue;
            if (point.X < box.Position.X || point.X > box.End.X || point.Y < box.Position.Z || point.Y > box.End.Z) continue;
            if (best is null || crown > best.Value) best = crown;
        }
        return best;
    }
}
