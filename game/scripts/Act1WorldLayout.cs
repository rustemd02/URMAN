using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Deterministic presentation layout for the five logical Chapter 1 zones.
/// The data deliberately contains no narrative, save or interaction state;
/// Main and Act1ConnectedWorld use it only to compose the existing scenes and
/// map their logical spawn points into one world-space territory.
/// </summary>
public static class Act1WorldLayout
{
    public const string ConnectedWorldRootName = "Act1ConnectedWorld";
    public const string VisualOnlyPolicy =
        "shared ground and connector road surfaces are collidable traversal surfaces; shoulders, fences and distant framing are visual-only; existing zone interaction and narrative owners remain authoritative";

    public readonly record struct SpawnTransform(Vector3 Position, float YawDegrees);

    public readonly record struct ZonePlacement(
        string ZoneId,
        string PlacementId,
        string ScenePath,
        Vector3 Origin,
        bool Interior,
        IReadOnlyDictionary<string, SpawnTransform> SpawnPoints);

    public readonly record struct ConnectorPlacement(
        string ConnectorId,
        Vector3 Start,
        Vector3 End,
        float Width,
        string SurfaceColor);

    // The local scenes keep their authored dimensions. These origins match the
    // existing Agent B terrain/road axis so the authored presentation kits can
    // sit on the same compact first-person route without rebasing gameplay.
    private static readonly Vector3 HouseOrigin = new(-28f, 0f, 0f);
    private static readonly Vector3 FapOrigin = new(28f, 0f, -30f);
    private static readonly Vector3 ZiratOrigin = new(0f, 0f, -70f);
    private static readonly Vector3 KaraUrmanOrigin = new(0f, 0f, -115f);

    private static readonly IReadOnlyList<ZonePlacement> _placements =
    [
        new(
            "village_day",
            "village-main-road",
            "res://scenes/zones/style_benchmark_day_street.tscn",
            Vector3.Zero,
            false,
            SpawnPoints(
                ("arrival", new SpawnTransform(new(0f, 0.05f, 9f), 0f)),
                ("from_house", new SpawnTransform(AgentBAct1Layout.HouseDoorApproach, AgentBAct1Layout.HouseDoorYawDegrees + 180f)),
                ("from_fap", new SpawnTransform(new(28f, 0.05f, -25f), 80f)),
                ("from_forest", new SpawnTransform(new(1.8f, 0.05f, -12.5f), 180f)),
                ("default", new SpawnTransform(new(0f, 0.05f, 9f), 0f)))),
        new(
            "house_old_pc",
            "babay-abi-house",
            "res://scenes/zones/style_benchmark_house_pc.tscn",
            HouseOrigin,
            true,
            SpawnPoints(
                ("entry", new SpawnTransform(StyleBenchmarkInteriorFactory.Entry, 0f)),
                ("wing_kitchen", new SpawnTransform(StyleBenchmarkInteriorFactory.WingKitchenSpawn, 0f)),
                ("attic", new SpawnTransform(StyleBenchmarkInteriorFactory.WingAtticSpawn, 90f)),
                ("default", new SpawnTransform(StyleBenchmarkInteriorFactory.Entry, 0f)))),
        new(
            "fap_clinic",
            "fap-clinic-yard",
            "res://scenes/zones/chapter1_fap_clinic.tscn",
            FapOrigin,
            true,
            SpawnPoints(
                ("waiting_room", new SpawnTransform(new(0f, 0.05f, 4.8f), 0f)),
                ("default", new SpawnTransform(new(0f, 0.05f, 4.8f), 0f)))),
        new(
            "zirat_road",
            "zirat-return-road",
            "res://scenes/zones/chapter1_zirat_road.tscn",
            ZiratOrigin,
            false,
            SpawnPoints(
                ("village_side", new SpawnTransform(new(0f, 0.05f, 16.5f), 0f)),
                ("default", new SpawnTransform(new(0f, 0.05f, 16.5f), 0f)))),
        new(
            "kara_urman_night",
            "kara-urman-edge",
            "res://scenes/zones/style_benchmark_kara_urman_night.tscn",
            KaraUrmanOrigin,
            false,
            SpawnPoints(
                ("village_path", new SpawnTransform(new(0f, 0.05f, 12f), 0f)),
                ("default", new SpawnTransform(new(0f, 0.05f, 12f), 0f))))
    ];

    private static readonly IReadOnlyList<ConnectorPlacement> _connectors =
    [
        new(
            "arrival-to-house-yard",
            new(-1.8f, 0.025f, 9.5f),
            HouseOrigin + new Vector3(3.6f, 0.025f, 1.7f),
            4.8f,
            "685b49"),
        new(
            "residential-road-extension",
            new(0f, 0.025f, 8f),
            new(0f, 0.025f, -19f),
            5.6f,
            "685b49"),
        // Relayout v3: the FAP street is straight (z -24); this is its last stretch to the FAP gate.
        new(
            "village-to-fap-branch",
            new(20f, 0.025f, -24.1f),
            FapOrigin + new Vector3(0f, 0.025f, 3.8f),
            4.6f,
            "625747"),
        new(
            "house-to-zirat-return",
            new(0f, 0.025f, -19f),
            ZiratOrigin + new Vector3(-0.4f, 0.025f, 16.5f),
            4.8f,
            "625847"),
        new(
            "zirat-to-kara-urman",
            ZiratOrigin + new Vector3(0f, 0.025f, -19.5f),
            KaraUrmanOrigin + new Vector3(0f, 0.025f, 12f),
            4f,
            "403c31"),
        new(
            "fap-yard-loop",
            FapOrigin + new Vector3(-5.0f, 0.025f, 4.4f),
            FapOrigin + new Vector3(5.0f, 0.025f, 1.6f),
            2.6f,
            "766957"),
        new(
            "kara-edge-approach",
            KaraUrmanOrigin + new Vector3(0f, 0.025f, 12f),
            KaraUrmanOrigin + new Vector3(0f, 0.025f, -7.5f),
            3.5f,
            "3b392f")
    ];

    public static IReadOnlyList<ZonePlacement> Placements => _placements;

    public static IReadOnlyList<ConnectorPlacement> Connectors => _connectors;

    public static IReadOnlyList<string> PlacementIds { get; } =
        _placements.Select(placement => placement.PlacementId).ToArray();

    public static IReadOnlyList<string> ZoneIds { get; } =
        _placements.Select(placement => placement.ZoneId).ToArray();

    public static bool ContainsZone(string zoneId) =>
        _placements.Any(placement => string.Equals(placement.ZoneId, zoneId, StringComparison.Ordinal));

    public static bool TryGetPlacement(string zoneId, out ZonePlacement placement)
    {
        placement = default;
        foreach (var candidate in _placements)
        {
            if (!string.Equals(candidate.ZoneId, zoneId, StringComparison.Ordinal))
            {
                continue;
            }

            placement = candidate;
            return true;
        }

        return false;
    }

    public static bool TryGetWorldSpawn(
        string zoneId,
        string spawnPointId,
        out SpawnTransform worldSpawn)
    {
        worldSpawn = default;
        if (!TryGetPlacement(zoneId, out var placement))
        {
            return false;
        }

        if (!placement.SpawnPoints.TryGetValue(spawnPointId, out var localSpawn))
        {
            return false;
        }

        worldSpawn = new(placement.Origin + localSpawn.Position, localSpawn.YawDegrees);
        return true;
    }

    private static IReadOnlyDictionary<string, SpawnTransform> SpawnPoints(
        params (string Id, SpawnTransform Transform)[] entries)
    {
        var points = new Dictionary<string, SpawnTransform>(StringComparer.Ordinal);
        foreach (var (id, transform) in entries)
        {
            points.Add(id, transform);
        }

        return points;
    }
}

/// <summary>
/// Relocated family zīrat (author, 2026-10-05: «перенести кладбище на пустырь где-то
/// огороды и сделай красиво»). The family graves, the open gate, the picket fence and
/// the grade path move from the roadside strip east of the zīrat road (x≈6.6,
/// z −70…−88) to a vacant field behind the last western holding's kitchen gardens:
/// plot x −22.4…−9.1, z −74…−88, entered from the road by a short snow-trodden path
/// and a small open gate. The road, its drainage, the culvert and the route trace tag
/// stay on the axis where the Act I clues live; the two family inscriptions keep their
/// authored offsets relative to the path reading point, so the reading geometry is the
/// same, only translated.
/// </summary>
public static class ZiratPlotLayout
{
    /// <summary>East picket fence with the open gate, facing the road (kit fence yaw 90°).</summary>
    public static readonly Vector3 GateFenceAnchor = new(-9.06f, 0f, -81f);

    /// <summary>Authored grade path: it enters through the gate and runs west into the plot.</summary>
    public static readonly Vector3 PathAnchor = new(-9.45f, 0f, -81f);

    /// <summary>Marker group reference anchors; the individual stones are recomposed below.</summary>
    public static readonly Vector3 LowMarkerAnchor = new(-11.3f, 0f, -81f);
    public static readonly Vector3 FarMarkerAnchor = new(-20.0f, 0f, -81f);

    /// <summary>Birch windbreak belt, a column outside the west fence (yaw 270°).</summary>
    public static readonly Vector3 BirchWindbreakAnchor = new(-31.5f, 0f, -81f);

    public static readonly Vector3 NorthEastCorner = new(-9.2f, 0f, -74f);
    public static readonly Vector3 NorthWestCorner = new(-22.4f, 0f, -74f);
    public static readonly Vector3 SouthWestCorner = new(-22.4f, 0f, -88f);
    public static readonly Vector3 SouthEastCorner = new(-9.2f, 0f, -88f);

    /// <summary>Visitor bench inside the north side, facing the rows and the gorge beyond.</summary>
    public static readonly Vector3 BenchAnchor = new(-12.6f, 0f, -77.4f);
    public const float BenchYawDegrees = 180f;

    /// <summary>Road-verge to gate approach; the centreline is conformed to the terrain.</summary>
    public static readonly Vector2[] ApproachAxis =
    [
        new(-2.5f, -79.9f), new(-5.6f, -80.3f), new(-9.1f, -80.55f)
    ];

    /// <summary>
    /// The thirteen kit stones recomposed as two quiet rows that face north (yaw 0,
    /// the authored front): the family pair keeps its exact path-relative offsets so
    /// the compiled inscriptions are read from the same place as before; the other
    /// stones take the north and south rows. Names are the authored kit nodes.
    /// </summary>
    public readonly record struct StonePlacement(string NodeName, float X, float Z, float YawDegrees);

    public static readonly StonePlacement[] RelocatedStones =
    [
        // The family pair — exact translation of the old reading geometry (yaw 0).
        new("ZiratMarkerGroup_Low_Marker_00_LOD0", -11.57f, -81.58f, 0f),
        new("ZiratMarkerGroup_Low_Marker_01_LOD0", -10.87f, -82.66f, 0f),
        // Near row flanking the entrance path.
        new("ZiratMarkerGroup_Low_Marker_02_LOD0", -13.60f, -79.70f, -6f),
        new("ZiratMarkerGroup_Low_Marker_03_LOD0", -15.20f, -82.80f, 6f),
        new("ZiratMarkerGroup_Low_Marker_04_LOD0", -14.10f, -84.30f, -9f),
        new("ZiratMarkerGroup_Low_Companion_00_LOD0", -12.40f, -83.60f, 8f),
        new("ZiratMarkerGroup_Low_Companion_01_LOD0", -16.30f, -85.60f, -4f),
        // Back row along the west fence.
        new("ZiratMarkerGroup_Far_Marker_00_LOD0", -19.70f, -79.60f, 7f),
        new("ZiratMarkerGroup_Far_Marker_01_LOD0", -20.70f, -80.80f, -5f),
        new("ZiratMarkerGroup_Far_Marker_02_LOD0", -19.00f, -84.30f, 9f),
        new("ZiratMarkerGroup_Far_Marker_03_LOD0", -19.90f, -85.50f, -8f),
        new("ZiratMarkerGroup_Far_Companion_00_LOD0", -20.90f, -78.70f, 3f),
        new("ZiratMarkerGroup_Far_Companion_01_LOD0", -21.10f, -86.80f, -6f),
    ];
}
