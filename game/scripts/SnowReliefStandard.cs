using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

/// <summary>
/// VIS-077 / VIS-079: one place that states the geometric snow standard of the visual
/// reset contract and one place that answers "may snow relief stand here?".
///
/// The standard splits snow into three tiers, and the tiers are geometry, not material:
///   mass  — целина, сугробы, обочины: metres long, 18–40 cm above the ground;
///   edge  — the readable lip of a trodden channel, a gate opening, a porch step, a ditch:
///           4–12 cm, which is the same band the trample field already respects (K02);
///   micro — the last millimetres, and it stays in the shader (snow micro-detail is
///           ±0.002 m in <c>PainterlyMaterialLibrary</c>; this class never writes it).
/// A hero view therefore carries at least two authored elevation levels (mass + edge)
/// before any texture is considered, which is the grayscale test of VIS-077.
///
/// Publication is additive and owned by Act1ConnectedWorld.PublishSnowExclusions together
/// with the verified winter routes; every consumer only reads.
/// With nothing published the queries return "clear", so behaviour before publication
/// — and in tests that never build the world — is unchanged.
/// </summary>
internal readonly struct SnowCorridor
{
    public readonly Vector2 A;
    public readonly Vector2 B;
    /// <summary>Half the width the corridor itself occupies (a walk, a lane, a rut pair).</summary>
    public readonly float Half;
    public readonly string Owner;

    public SnowCorridor(Vector2 a, Vector2 b, float half, string owner)
    {
        A = a; B = b; Half = half; Owner = owner;
    }

    /// <summary>Signed clearance from the corridor's occupied band: negative inside it.</summary>
    public float Clearance(Vector2 point) => SegmentDistance(point, A, B) - Half;

    internal static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared <= 1e-6f) return p.DistanceTo(a);
        var t = Mathf.Clamp((p - a).Dot(ab) / lengthSquared, 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }
}

internal static class SnowReliefStandard
{
    // Tier ids written as the "snowTier" meta on every relief mesh this standard touches,
    // so a geometry-only capture can count the levels that exist in a frame.
    public const string TierMass = "mass";
    public const string TierEdge = "edge";
    public const string TierMicro = "micro";

    // Large mass: an authored wind/shovel pile, not a decal.
    public const float MassHeightMin = .18f;
    public const float MassHeightMax = .40f;
    /// <summary>The second, smaller swell on the crest of a mass: reads as settled snow.</summary>
    public const float MassCrestBump = .07f;

    // Medium edge: the lip the eye actually reads on a trodden line or an opening.
    public const float EdgeHeightMin = .04f;
    public const float EdgeHeightMax = .12f;

    // Micro belongs to the shader; geometry never goes below the edge tier.
    public const float MicroHeightMax = .004f;

    /// <summary>Elevation band of the VIS-077 grayscale gate: rise levels are counted in
    /// 12 cm steps, the same step the trample field tolerates (K02, PainterlyMaterialLibrary).</summary>
    public const float ElevationBand = .12f;

    /// <summary>How far a drift must stand from a carriageway edge before it may be built.</summary>
    public const float MassRoadClearance = 1.1f;
    /// <summary>Maximum ground slope (m per m) a large mass may sit on; steeper ground is
    /// left to the terrain so a drift never floats on a slope.</summary>
    public const float MassMaximumSlope = .34f;
    /// <summary>Margin around every solid box and building footprint a mass keeps clear.</summary>
    public const float MassSolidMargin = 1.0f;

    private static readonly List<SnowCorridor> _corridors = new();
    private static readonly List<Aabb> _solids = new();
    private static readonly List<Vector2[]> _footprints = new();
    // Publication is additive and owner-keyed: the world publishes its access walks, gates,
    // solids and footprints once on the first frame, while a verified route publishes its own
    // corridor every time its path array is re-committed. Owners keep that from duplicating,
    // and no pass can wipe what another owner already relies on.
    private static readonly HashSet<string> _owners = new(StringComparer.Ordinal);
    private static int _publishedRevision;

    public static int PublishedRevision => _publishedRevision;
    public static IReadOnlyList<SnowCorridor> Corridors => _corridors;
    public static int CorridorCount => _corridors.Count;
    public static int SolidCount => _solids.Count;
    public static int FootprintCount => _footprints.Count;

    public static void Clear()
    {
        _corridors.Clear(); _solids.Clear(); _footprints.Clear(); _owners.Clear();
        _publishedRevision++;
    }

    /// <summary>Adds one corridor unless its owner has been published before. A degenerate
    /// segment claims nothing, so the same owner can be retried after a route is rebuilt.</summary>
    public static bool PublishCorridor(string owner, Vector2 a, Vector2 b, float half)
    {
        if (a.DistanceSquaredTo(b) <= 1e-4f || half <= 0f) return false;
        if (!_owners.Add("c:" + owner)) return false;
        _corridors.Add(new SnowCorridor(a, b, half, owner));
        return true;
    }

    /// <summary>A world-space solid (wall, foundation, porch, deck, prop body).</summary>
    public static bool PublishSolid(string owner, Aabb box)
    {
        if (!_owners.Add("s:" + owner)) return false;
        _solids.Add(box);
        return true;
    }

    /// <summary>An authored building footprint polygon in the XZ plane.</summary>
    public static bool PublishFootprint(string owner, Vector2[] polygon)
    {
        if (polygon is not { Length: >= 3 }) return false;
        if (!_owners.Add("f:" + owner)) return false;
        _footprints.Add(polygon);
        return true;
    }

    /// <summary>Every polyline corridor of a verified route, one owner per segment.</summary>
    public static int PublishPolyline(string owner, Vector3[] path, float half)
    {
        var added = 0;
        for (var i = 1; i < path.Length; i++)
            if (PublishCorridor($"{owner}#{i}", new Vector2(path[i - 1].X, path[i - 1].Z),
                    new Vector2(path[i].X, path[i].Z), half)) added++;
        return added;
    }

    /// <summary>Drops every corridor whose owner starts with <paramref name="ownerPrefix"/>
    /// (pass the published owner plus the '#' the polyline appender adds). Used when a route
    /// is re-committed, so a retired path stops holding snow relief clear of ground it no
    /// longer crosses. Returns how many corridors went away.</summary>
    public static int RetireCorridors(string ownerPrefix)
    {
        var retired = 0;
        for (var i = _corridors.Count - 1; i >= 0; i--)
        {
            if (!_corridors[i].Owner.StartsWith(ownerPrefix, StringComparison.Ordinal)) continue;
            _owners.Remove("c:" + _corridors[i].Owner);
            _corridors.RemoveAt(i);
            retired++;
        }
        if (retired > 0) _publishedRevision++;
        return retired;
    }

    /// <summary>Smallest signed clearance to any published corridor; <see cref="float.MaxValue"/>
    /// when nothing is published.</summary>
    public static float CorridorClearance(Vector2 point)
    {
        var best = float.MaxValue;
        foreach (var corridor in _corridors) best = Mathf.Min(best, corridor.Clearance(point));
        return best;
    }

    public static bool InsideCorridor(Vector2 point, float margin) => CorridorClearance(point) < margin;

    public static bool InsideSolid(Vector2 point, float margin)
    {
        foreach (var box in _solids)
        {
            if (point.X < box.Position.X - margin || point.X > box.End.X + margin) continue;
            if (point.Y < box.Position.Z - margin || point.Y > box.End.Z + margin) continue;
            return true;
        }
        return false;
    }

    public static bool InsideFootprint(Vector2 point, float margin)
    {
        foreach (var polygon in _footprints)
        {
            if (PointInPolygon(polygon, point)) return true;
            for (var i = 0; i < polygon.Length; i++)
                if (SnowCorridor.SegmentDistance(point, polygon[i], polygon[(i + 1) % polygon.Length]) < margin) return true;
        }
        return false;
    }

    /// <summary>The unified exclusion answer used by both the mass tier and the bank rework:
    /// a point is blocked for snow relief when a required corridor, a solid or an authored
    /// footprint reaches it.</summary>
    public static bool Blocked(Vector2 point, float margin)
        => InsideCorridor(point, margin) || InsideSolid(point, margin) || InsideFootprint(point, margin);

    /// <summary>Vertical rise levels present in a set of relief rises, counted in
    /// <see cref="ElevationBand"/> steps — the static half of the VIS-077 gate.</summary>
    public static int ElevationLevelCount(IEnumerable<float> rises)
    {
        var bands = new HashSet<int>();
        foreach (var rise in rises)
            if (rise > MicroHeightMax) bands.Add((int)MathF.Floor(rise / ElevationBand));
        return bands.Count;
    }

    internal static bool PointInPolygon(Vector2[] polygon, Vector2 point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)
                && point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y + 1e-6f) + polygon[i].X)
                inside = !inside;
        return inside;
    }
}
