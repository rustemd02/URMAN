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
        new(
            "village-to-fap-branch",
            new(0f, 0.025f, -10f),
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
