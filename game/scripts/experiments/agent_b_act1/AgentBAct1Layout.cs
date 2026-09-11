using Godot;
using System.Collections.Generic;

namespace Urman.Experiments.AgentBAct1;

/// <summary>
/// Agent B Act I master layout: single compact Kyrlay village, one authored
/// terrain, one continuous route. Coordinates follow the 2026-08-17 master
/// layout contract where it is compatible with a connected world.
/// Ground plane y=0. Eye height handled by the first person player (y+1.7).
/// +Z = arrival side (village entrance), -Z = Kara-Urman direction.
/// 1 BU = 1 metre. All route corridors keep >= 3.5 m free envelope.
/// </summary>
public static class AgentBAct1Layout
{
    public const float EyeHeight = 1.7f;

    // ---- Terrain envelope -------------------------------------------------
    public const float TerrainMinX = -64f;
    public const float TerrainMaxX = 64f;
    public const float TerrainMinZ = -152f;
    public const float TerrainMaxZ = 104f;

    // ---- Main road spline control points (x, z), crown width ~5.6 m -------
    public static readonly Vector2[] MainRoadAxis = new[]
    {
        new Vector2(0f, 40f),      // off-map arrival tail (fog eats it)
        new Vector2(0f, 9f),       // arrival spawn
        new Vector2(-0.6f, -1.5f), // village entrance pinch
        new Vector2(-1.2f, -8f),   // main street
        new Vector2(0f, -19f),     // main street end / returns begin
        new Vector2(-1f, -30f),    // return street
        new Vector2(0f, -41.5f),   // return street mid
        new Vector2(-0.4f, -53.5f) // zirat roadside
    };

    // Zirat road continues at 4.2 m width.
    public static readonly Vector2[] ZiratRoadAxis = new[]
    {
        new Vector2(-0.4f, -53.5f),
        new Vector2(0.3f, -64f),
        new Vector2(0f, -76f),
        new Vector2(-0.6f, -89.5f)
    };

    // Kara approach narrows to 3.5 m, slightly winding.
    public static readonly Vector2[] KaraRoadAxis = new[]
    {
        new Vector2(-0.6f, -89.5f),
        new Vector2(0.9f, -96f),
        new Vector2(-0.4f, -103f),
        new Vector2(1.1f, -110f),
        new Vector2(0f, -117f),
        new Vector2(0.6f, -122.5f)  // cliffhanger endpoint
    };

    // FAP branch leaves main street at (3.8, -8.2) and bends east.
    public static readonly Vector2[] FapBranchAxis = new[]
    {
        new Vector2(0f, -10f),
        new Vector2(4.5f, -12.5f),
        new Vector2(10f, -17f),
        new Vector2(17f, -21.5f),
        new Vector2(23f, -25f),
        new Vector2(28f, -26.2f)
    };

    // House path from main street into the babai yard.
    public static readonly Vector2[] HousePathAxis = new[]
    {
        new Vector2(-1.2f, -8f),
        new Vector2(-6f, -5.5f),
        new Vector2(-12f, -2.5f),
        new Vector2(-19f, 0f),
        new Vector2(-24f, 1.2f)
    };

    // ---- Zone anchors ------------------------------------------------------
    // Arrival: road enters between two leaning fences and a well landmark.
    public static readonly Vector3 ArrivalSpawn = new(0f, 0.05f, 9f);
    // Babai/ebi yard: house rotated east, front veranda around x=-26.
    public static readonly Vector3 BabaiHouseCenter = new(-30f, 0f, -1f);
    public static readonly Vector3 BabaiYardSpawn = new(-24.4f, 0.05f, 1.7f);

    // The hero facade is mounted at (-28, -3.4) with the authored 0.82
    // scale. These values are the centre of its real GLB portal after the
    // imported preview origin is removed (local portal centre x=-1.42,
    // y=1.425, z=1.325). Target and player approach share this portal.
    public const float HouseDoorYawDegrees = 64.72228f;
    public static readonly Vector3 HouseDoorPortalCenter = new(
        -27.51474f,
        (float)AgentBAct1HeightField.Ground(-28f, -3.4f) + .03f + 1.425f * .82f,
        -1.88315f);
    public static readonly Vector3 HouseDoorOutwardDirection =
        new(.90424865f, 0f, .42700631f);
    private static readonly Vector3 HouseDoorApproachXZ = new(
        HouseDoorPortalCenter.X + HouseDoorOutwardDirection.X * 1.5f,
        0f,
        HouseDoorPortalCenter.Z + HouseDoorOutwardDirection.Z * 1.5f);
    public static readonly Vector3 HouseDoorApproach = new(
        HouseDoorApproachXZ.X,
        AgentBAct1HeightField.CollisionGround(HouseDoorApproachXZ.X, HouseDoorApproachXZ.Z) + .05f,
        HouseDoorApproachXZ.Z);
    public static readonly Vector3 HouseDoorProxySize = new(1.02f, 1.84f, .18f);
    // FAP clinic sits east of the branch end, facade to the road.
    public static readonly Vector3 FapCenter = new(30f, 0f, -28f);
    public static readonly Vector3 FapSpawn = new(24f, 0.05f, -24f);
    // Return street houses line the road z -20..-50.
    public static readonly Vector3 ReturnStreetSpawn = new(-7f, 0.05f, -41.5f);
    // Zirat east of the road, low fence, gated entry at path head.
    public static readonly Vector3 ZiratCenter = new(6.5f, 0f, -70f);
    public static readonly Vector3 ZiratSpawn = new(0f, 0.05f, -53.5f);
    // Night forest edge begins ~ z=-92; cliffhanger clearing at -122.5.
    public static readonly Vector3 KaraApproachSpawn = new(0f, 0.05f, -103f);
    public static readonly Vector3 CliffhangerEndpoint = new(0.6f, 0.05f, -122.5f);

    // ---- Traversal waypoints (physical walk, in order) ---------------------
    public sealed record Waypoint(string Id, Vector3 Position);

    public static readonly IReadOnlyList<Waypoint> Route = new List<Waypoint>
    {
        new("arrival", new Vector3(0f, 0.05f, 9f)),
        new("house_yard", new Vector3(-24.4f, 0.05f, 1.7f)),
        new("house_exterior", HouseDoorApproach),
        new("back_to_street", new Vector3(-3.5f, 0.05f, -2.8f)),
        new("main_street", new Vector3(-2.2f, 0.05f, -8f)),
        new("fap_branch", new Vector3(10f, 0.05f, -17f)),
        new("fap_exterior", new Vector3(24f, 0.05f, -24f)),
        new("return_street", new Vector3(-7f, 0.05f, -41.5f)),
        new("zirat_roadside", new Vector3(-3.2f, 0.05f, -68f)),
        new("kara_approach", new Vector3(0f, 0.05f, -103f)),
        new("cliffhanger", new Vector3(0.6f, 0.05f, -122.5f))
    };

    // ---- Authored walk chain (ordered open-road nodes) ---------------------
    // The continuous pedestrian corridor the route physically follows. It
    // hugs the road centreline, turns into the babai yard through its gate,
    // comes back out, branches to the FAP gate, returns to the main street
    // and rides south through the zirat roadside to the Kara cliffhanger.
    public static readonly IReadOnlyList<Vector2> WalkChain = new List<Vector2>
    {
        new(0f, 9f),
        new(-0.4f, 2f),
        new(-1.2f, -1.5f),
        new(-4f, -5.8f),        // onto the house path
        new(-10f, -3.5f),
        new(-16f, -1f),
        new(-20f, 1.5f),
        new(-23f, 2.6f),
        new(-25.2f, 2.6f),      // through the open yard gate (gap z 1.75-3.45)
        new(-26.05f, 2.6f),    // inside the original yard gate
        new(-26.05f, .2f),
        new(HouseDoorApproach.X, HouseDoorApproach.Z),
        new(-26.05f, .2f),
        new(-26.05f, 2.6f),
        new(-25.2f, 2.6f),
        new(-22.5f, 2.4f),
        new(-16f, -0.5f),
        new(-8f, -3.5f),
        new(-3.5f, -2.8f),      // back_to_street
        new(-2.5f, -5.5f),
        new(-2.2f, -8f),        // main_street
        new(0f, -10f),          // FAP branch
        new(4.5f, -12.5f),
        new(10f, -17f),         // fap_branch
        new(17f, -21.5f),
        new(23f, -25f),
        new(24f, -24f),         // fap_exterior (outside the gate)
        new(23f, -24.5f),       // turn around and take the branch back west
        new(17f, -21f),
        new(8f, -16f),
        new(-0.5f, -12f),
        new(-1.5f, -16f),       // south down the main street again
        new(-0.5f, -24f),
        new(-0.5f, -32f),
        new(-1f, -38f),
        new(-7f, -41.5f),       // return_street
        new(-0.8f, -46f),
        new(-0.4f, -53.5f),
        new(-2.4f, -57f),       // swing west before the zirat south fence
        new(-3.0f, -62f),
        new(-3.2f, -68f),       // zirat_roadside, safely west of the cemetery
        new(-3.0f, -74f),
        new(-2.6f, -79f),
        new(-1.6f, -84f),
        new(-0.6f, -89.5f),     // rejoin the Kara road
        new(0.9f, -96f),
        new(0f, -103f),         // kara_approach
        new(1.1f, -110f),
        new(0f, -117f),
        new(0.6f, -122.5f)      // cliffhanger
    };

    // ---- Per-zone visual review points (capture spec) -----------------------
    public sealed record ReviewPoint(string Id, Vector3 Camera, Vector3 Target);

    public static readonly IReadOnlyList<ReviewPoint> ReviewPoints = new List<ReviewPoint>
    {
        // Arrival
        new("arrival_forward", new Vector3(0f, EyeHeight, 9f), new Vector3(-0.6f, 1.2f, -1.5f)),
        new("arrival_back", new Vector3(0f, EyeHeight, 9f), new Vector3(0f, 1.4f, 40f)),
        new("arrival_left", new Vector3(0f, EyeHeight, 9f), new Vector3(-9f, 1.2f, 8.5f)),
        new("arrival_right", new Vector3(0f, EyeHeight, 9f), new Vector3(9f, 1.2f, 8f)),
        new("arrival_depth", new Vector3(0f, EyeHeight, 9f), new Vector3(-30f, 1.0f, -1f)),
        // Babai yard / house
        new("babai_forward", new Vector3(-24.4f, EyeHeight, 1.7f), new Vector3(-30f, 1.2f, -1f)),
        new("babai_back", new Vector3(-24.4f, EyeHeight, 1.7f), new Vector3(-6f, 1.2f, 6f)),
        new("babai_left", new Vector3(-24.4f, EyeHeight, 1.7f), new Vector3(-25f, 1.2f, -8f)),
        new("babai_right", new Vector3(-24.4f, EyeHeight, 1.7f), new Vector3(-14f, 1.2f, 3f)),
        new("babai_depth", new Vector3(-24.4f, EyeHeight, 1.7f), new Vector3(4f, 1.0f, -20f)),
        // House exterior approach (old PC house landmark, east facade)
        new("house_forward", new Vector3(-25.9f, EyeHeight, 0.9f), new Vector3(-30f, 1.1f, -1f)),
        new("house_back", new Vector3(-25.9f, EyeHeight, 0.9f), new Vector3(-16f, 1.2f, 6f)),
        new("house_left", new Vector3(-25.9f, EyeHeight, 0.9f), new Vector3(-27f, 1.2f, -9f)),
        new("house_right", new Vector3(-25.9f, EyeHeight, 0.9f), new Vector3(-24f, 1.2f, 10f)),
        new("house_depth", new Vector3(-25.9f, EyeHeight, 0.9f), new Vector3(-6f, 1.0f, -25f)),
        // Main street
        new("street_forward", new Vector3(-2.2f, EyeHeight, -8f), new Vector3(0f, 1.1f, -41f)),
        new("street_back", new Vector3(-2.2f, EyeHeight, -8f), new Vector3(0f, 1.3f, 20f)),
        new("street_left", new Vector3(-2.2f, EyeHeight, -8f), new Vector3(-8f, 1.1f, -9f)),
        new("street_right", new Vector3(-2.2f, EyeHeight, -8f), new Vector3(5f, 1.2f, -9f)),
        new("street_depth", new Vector3(-2.2f, EyeHeight, -8f), new Vector3(1f, 1.0f, -60f)),
        // FAP exterior
        new("fap_forward", new Vector3(24f, EyeHeight, -24f), new Vector3(30f, 1.2f, -28f)),
        new("fap_back", new Vector3(24f, EyeHeight, -24f), new Vector3(6f, 1.2f, -13f)),
        new("fap_left", new Vector3(24f, EyeHeight, -24f), new Vector3(23f, 1.1f, -33f)),
        new("fap_right", new Vector3(24f, EyeHeight, -24f), new Vector3(34f, 1.3f, -22f)),
        new("fap_depth", new Vector3(24f, EyeHeight, -24f), new Vector3(0f, 1.0f, -70f)),
        // Return street
        new("return_forward", new Vector3(-7f, EyeHeight, -41.5f), new Vector3(0f, 1.1f, -56f)),
        new("return_back", new Vector3(-7f, EyeHeight, -41.5f), new Vector3(-2.2f, 1.2f, -8f)),
        new("return_left", new Vector3(-7f, EyeHeight, -41.5f), new Vector3(-12f, 1.1f, -42f)),
        new("return_right", new Vector3(-7f, EyeHeight, -41.5f), new Vector3(1.5f, 1.2f, -42f)),
        new("return_depth", new Vector3(-7f, EyeHeight, -41.5f), new Vector3(30f, 1.0f, -28f)),
        // Zirat
        new("zirat_forward", new Vector3(0f, EyeHeight, -53.5f), new Vector3(0.3f, 1.2f, -70f)),
        new("zirat_back", new Vector3(0f, EyeHeight, -53.5f), new Vector3(-2f, 1.3f, -30f)),
        new("zirat_left", new Vector3(1.2f, EyeHeight, -56.5f), new Vector3(6.5f, 1.0f, -70f)),
        new("zirat_right", new Vector3(0f, EyeHeight, -53.5f), new Vector3(-8f, 1.1f, -55f)),
        new("zirat_depth", new Vector3(0f, EyeHeight, -53.5f), new Vector3(0f, 1.0f, -100f)),
        // Kara approach
        new("kara_forward", new Vector3(0f, EyeHeight, -103f), new Vector3(0.6f, 1.3f, -122.5f)),
        new("kara_back", new Vector3(0f, EyeHeight, -103f), new Vector3(0f, 1.2f, -70f)),
        new("kara_left", new Vector3(0f, EyeHeight, -103f), new Vector3(-8f, 1.2f, -104f)),
        new("kara_right", new Vector3(0f, EyeHeight, -103f), new Vector3(8f, 1.2f, -103.5f)),
        new("kara_depth", new Vector3(0f, EyeHeight, -103f), new Vector3(0.6f, 1.6f, -126f)),
        // Cliffhanger endpoint
        new("cliff_forward", new Vector3(0.6f, EyeHeight, -122.5f), new Vector3(0.6f, 1.5f, -132f)),
        new("cliff_back", new Vector3(0.6f, EyeHeight, -122.5f), new Vector3(0f, 1.4f, -90f)),
        new("cliff_up", new Vector3(0.6f, EyeHeight, -122.5f), new Vector3(0.6f, 30f, -123f)),
        new("cliff_down", new Vector3(0.6f, EyeHeight, -122.5f), new Vector3(0.6f, -30f, -123f)),
        new("cliff_left", new Vector3(0.6f, EyeHeight, -122.5f), new Vector3(-7f, 1.3f, -123f))
    };

    // ---- Colour anchors (shared with the compositor) -------------------------
    public const string GroundGrass = "5a6248";
    public const string GroundMud = "4a4136";
    public const string RoadCrown = "6a5a49";
    public const string RoadRut = "3d352c";
    public const string WarmWindow = "e8b04a";
}
