using Godot;

namespace Urman.Godot;

/// <summary>
/// VIS-016: the two session track owners — the footprint field
/// (<see cref="SnowTrampleField"/>) and the Niva's pressed ruts
/// (<see cref="VehicleSnowTracks"/>) — must never draw a trace through a solid object
/// or at a height that is not the one they walked on. Both already reject a stamp that
/// leaves the exterior terrain surface, but the interpolated segment between two frames
/// can still cut a corner of a fence, a wall or a stack, and a stamp written on one
/// level can be read at another.
///
/// This helper is the single answer to both questions, with the same height band the
/// snow shader already uses (K02: <c>abs(position.y - support) &lt; 0.12</c> in
/// PainterlyMaterialLibrary), so the CPU gate and the GPU gate cannot disagree.
/// Presentation-only: no collider, no navigation, no save state is touched here.
/// </summary>
internal static class SnowTrackObstruction
{
    /// <summary>K02 — the height a snow impression may disagree with its support by, in
    /// metres. The same number the trample shader uses; raising it would let a print show
    /// on a roof, a stack or the far side of a kerb.</summary>
    public const float MaximumSupportStep = .12f;

    /// <summary>Terrain (1), authored blockers such as rim fences (2) and prop bodies (4).</summary>
    private const uint SolidLayers = 1u | 2u | 4u;

    /// <summary>The only collision owner that is ground rather than an object.</summary>
    private const string TerrainOwner = "act1-exterior-terrain";

    /// <summary>How many times a probe may step over the walker's own body before the
    /// segment is given up on. A walked or driven segment is short, so four self-hits is
    /// already more than a hull or a capsule can present on one step.</summary>
    private const int MaxSelfProbes = 4;

    /// <summary>How far past a self-hit the next probe restarts, in metres: enough to
    /// clear the surface the ray stopped on, small enough not to skip a thin rail.</summary>
    private const float SelfProbeSkip = .004f;

    /// <summary>True when a wall, fence, stack or building face stands strictly between two
    /// XZ positions at walking height. <paramref name="footY"/> is the height of the feet
    /// (or the wheel hub) whose trace is about to be written.
    /// <paramref name="excludeRid"/> is the RID id of the walker itself, whose body is on
    /// one of the queried layers.</summary>
    public static bool CrossesSolid(World3D space, Vector2 from, Vector2 to, float footY, ulong excludeRid)
    {
        if (space is null) return false;
        var end = new Vector3(to.X, footY + .14f, to.Y);
        var cursor = new Vector3(from.X, footY + .14f, from.Y);
        if (cursor.DistanceSquaredTo(end) <= .0004f) return false;
        var state = space.DirectSpaceState;
        // PhysicsRayQueryParameters3D.Exclude accepts only RIDs taken from a live
        // GodotObject, and this binding gives no way to rebuild one from the raw id the
        // callers hold, so the walker is stepped over instead of excluded: a probe that
        // stops on its own collider restarts just past that point and keeps going. The
        // answer is the one the exclude list would have produced — a wall, a fence or a
        // stack behind the walker is still found — and the snow shader's own height band
        // (K02) stays the only other gate.
        for (var probe = 0; probe < MaxSelfProbes && cursor.DistanceSquaredTo(end) > .0004f; probe++)
        {
            using var query = PhysicsRayQueryParameters3D.Create(cursor, end, SolidLayers);
            using var hit = state.IntersectRay(query);
            if (hit.Count == 0) return false;
            if (excludeRid != 0 && hit["rid"].AsRid().Id == excludeRid)
            {
                cursor = hit["position"].AsVector3() + (end - cursor).Normalized() * SelfProbeSkip;
                continue;
            }
            if (hit["collider"].AsGodotObject() is not Node body) return false;
            // The shared terrain is the surface the trace is written on, not an obstacle;
            // anything else only counts when it faces the walker (a wall, a fence, a stack),
            // so a step or a low kerb does not erase the print.
            if (body.GetMeta("collisionOwner", "").AsString() == TerrainOwner) return false;
            if (body.GetMeta("presentationOnly", false).AsBool() || body.GetMeta("visualOnly", false).AsBool()) return false;
            return hit["normal"].AsVector3().Y < .6f;
        }
        return false;
    }

    /// <summary>Height-consistency gate: a print may only be written where the surface the
    /// walker is standing on is the surface the trace will be read from.</summary>
    public static bool SupportMatches(float support, float reference)
        => !float.IsNaN(support) && !float.IsNaN(reference) && Mathf.Abs(support - reference) <= MaximumSupportStep;
}
