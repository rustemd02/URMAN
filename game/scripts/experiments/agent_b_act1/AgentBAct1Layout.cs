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
    // Public centre between inhabited quarters; the street continues behind school.
    // Relayout v3, stage 3 (2026-10-01): the civic centre is the paved square Мәйдан
    // west of the main street (docs/production/village_relayout_2026-10-01/plan.json).
    public static readonly Vector2 CivicCentre = new(-15.5f, 48f);

    /// <summary>The paved square Мәйдан: x0, x1, z0, z1 (plan.json "plaza").</summary>
    public static readonly (float X0, float X1, float Z0, float Z1) PlazaRect = (-28f, -3f, 33f, 63f);

    public static bool InsidePlaza(Vector2 point, float margin = 0f)
        => point.X >= PlazaRect.X0 - margin && point.X <= PlazaRect.X1 + margin
           && point.Y >= PlazaRect.Z0 - margin && point.Y <= PlazaRect.Z1 + margin;

    /// <summary>Pedestrian walk round the square's central garden, linked to the main street.</summary>
    public static readonly Vector2[] PlazaWalkAxis =
    {
        // West side at x -21.8: in front of the DK steps, not across them; a node at the DK foot.
        new(-1.8f, 48f), new(-5.5f, 48f), new(-5.5f, 36f), new(-21.8f, 36f), new(-21.8f, 48f), new(-21.8f, 60f),
        new(-5.5f, 60f), new(-5.5f, 48f)
    };

    /// <summary>The paved carriageway once round the square's garden (centre -15.5, 48), entered
    /// from the main street at its north-east corner and left at the south-east one: clear of the
    /// DK steps (x -23), the school apron (z 64), the post office apron (z 32) and the garden
    /// (radius 4.75). Cars drive it anticlockwise (the prologue ride does).</summary>
    public static readonly Vector2[] PlazaDriveAxis =
    {
        new(-1.2f, 56f), new(-5f, 58.6f), new(-12f, 58.8f), new(-18.5f, 57.6f), new(-21.4f, 53.5f), new(-21.6f, 43f),
        new(-19f, 38.4f), new(-12f, 37.4f), new(-5f, 37.4f), new(-1.2f, 35f)
    };

    // ---- Terrain envelope -------------------------------------------------
    public const float TerrainMinX = -64f;
    public const float TerrainMaxX = 64f;
    public const float TerrainMinZ = -152f;
    public const float TerrainMaxZ = 232f;

    // ---- Main road spline control points (x, z), crown width ~5.6 m -------
    // Relayout v3, stage 4: Тукай урамы runs straight north from the arrival to the
    // last plots at z 150; the cross streets of the open part (Яңа, Чишмә, Усал, Бакча,
    // Кыр) live in the open-part plot data (OpenPartPlotPath) with their households.
    public static readonly Vector2[] MainRoadAxis = new[]
    {
        new Vector2(0f, 150f),
        new Vector2(0f, 130f),
        new Vector2(0f, 105f),
        new Vector2(0f, 84f),
        new Vector2(0f, 62f),
        new Vector2(0f, 40f),
        new Vector2(0f, 9f),
        new Vector2(-0.6f, -1.5f),
        new Vector2(-1.2f, -8f),
        new Vector2(0f, -19f),
        new Vector2(-1f, -30f),
        new Vector2(0f, -41.5f),
        new Vector2(-0.4f, -53.5f),
    };

    internal const string OpenPartPlotPath = "res://content/world/act1_open_part.world.v1.json";

    /// <summary>The village mosque's minaret corner (the complex fronts east, onto the square's west side).</summary>
    public static readonly Vector2 MosqueAnchor = new(-29.5f, 25f);

    /// <summary>Foot path from the mosque yard gate to the walk round Мәйдан.</summary>
    public static readonly Vector2[] MosqueWalkAxis =
    {
        new(-21.8f, 36f), new(-21.8f, 24f), new(MosqueAnchor.X - .2f, 24f)
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

    // Relayout v3: the FAP street (Урман урамы) leaves the main street at a right angle
    // at z -24 and runs straight to the FAP gate and on to the ravine bridge.
    public static readonly Vector2[] FapBranchAxis = new[]
    {
        new Vector2(-0.5f, -24f),
        new Vector2(7f, -24f),
        new Vector2(14f, -24f),
        new Vector2(21f, -24.1f),
        new Vector2(28f, -24.2f)
    };

    // The FAP service street reaches the intact prologue bridge. The bridge
    // owns its deck; this axis owns the connected carriageway and road graph.
    public static readonly Vector2[] BridgeApproachAxis =
    {
        new(28f, -24.2f), new(32f, -24.2f), new(37f, -24f), new(38.5f, -25f), new(41.5f, -25f), new(45.3f, -25f),
    };

    // House path from main street into the babai yard.
    public static readonly Vector2[] HousePathAxis = new[]
    {
        // Relayout v3 stage 5: a few steps from the carriageway to the street gate.
        new Vector2(-1.2f, BabaiRelocation.Apply(new Vector2(-25.2f, 2.6f)).Y),
        BabaiRelocation.Apply(new Vector2(-24f, 2.5f)),
        BabaiRelocation.Apply(new Vector2(-25.2f, 2.6f))
    };


    // ---- Zone anchors ------------------------------------------------------
    // Arrival: road enters between two leaning fences and a well landmark.
    public static readonly Vector3 ArrivalSpawn = new(0f, 0.05f, 9f);
    // Babai/ebi yard: house rotated east, front veranda around x=-26.
    public static readonly Vector3 BabaiHouseCenter = BabaiRelocation.Apply(new Vector3(-30f, 0f, -1f));
    public static readonly Vector3 BabaiYardSpawn = BabaiRelocation.Apply(new Vector3(-24.4f, 0.05f, 1.7f));

    // The hero facade is mounted at (-28, -3.4) with the authored 0.82
    // scale. These values are the centre of its real GLB portal after the
    // imported preview origin is removed (local portal centre x=-1.42,
    // y=1.425, z=1.325). Target and player approach share this portal.
    public const float HouseDoorYawDegrees = 64.72228f + BabaiRelocation.YawDegrees;
    private static readonly Lazy<Vector3> HouseDoorPortalCenterValue = new(() => new Vector3(
        BabaiRelocation.DoorX,
        (float)AgentBAct1HeightField.Ground(-28f, -3.4f) + BabaiRelocation.Lift + .03f + 1.425f * .82f,
        BabaiRelocation.DoorZ));
    public static Vector3 HouseDoorPortalCenter => HouseDoorPortalCenterValue.Value;
    public static readonly Vector3 HouseDoorOutwardDirection = new(1f, 0f, 0f);
    private static readonly Lazy<Vector3> HouseDoorApproachValue = new(() =>
    {
        var portal = HouseDoorPortalCenter;
        var x = portal.X + HouseDoorOutwardDirection.X * 1.5f;
        var z = portal.Z + HouseDoorOutwardDirection.Z * 1.5f;
        return new Vector3(x, AgentBAct1HeightField.CollisionGround(x, z) + .05f, z);
    });
    public static Vector3 HouseDoorApproach => HouseDoorApproachValue.Value;
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

    // Former sovkhoz square (village expansion, square slice): circles kept
    // clear of generated trees around the public buildings built in
    // Act1ConnectedWorld.SovkhozSquare. Keep in sync with
    // tools/world/generate_north_street.py SQUARE_BUILDING_CLEARANCE.
    public static readonly (float X, float Z, float Radius)[] SquareBuildingClearance =
    {
        (-17.5f, 71.5f, 15f),   // school
        (-37.5f, 48f, 12f),     // house of culture
        (11f, 52f, 9f),         // former sovkhoz office
        (-15.5f, 27f, 6f),      // closed post office
        (-15.5f, 48f, 17f),     // the square itself
        (-4.4f, 20f, 4f),       // bus pavilion
        (MosqueAnchor.X - 7f, MosqueAnchor.Y - 1f, 11f), // mosque hall and courtyard
    };

    public static bool InsideSquareBuildingClearance(Vector2 point)
    {
        foreach (var (x, z, radius) in SquareBuildingClearance)
            if (new Vector2(point.X - x, point.Y - z).Length() < radius) return true;
        return false;
    }

    // ---- Traversal waypoints (physical walk, in order) ---------------------
    public sealed record Waypoint(string Id, Vector3 Position);

    private static readonly Lazy<IReadOnlyList<Waypoint>> RouteValue = new(() => new List<Waypoint>
    {
        new("arrival", new Vector3(0f, 0.05f, 9f)),
        new("house_yard", BabaiRelocation.Apply(new Vector3(-24.4f, 0.05f, 1.7f))),
        new("house_exterior", HouseDoorApproach),
        new("back_to_street", new Vector3(-2.4f, 0.05f, -2.8f)),
        new("main_street", new Vector3(-2.2f, 0.05f, -8f)),
        new("fap_branch", new Vector3(10f, 0.05f, -24f)),
        new("fap_exterior", new Vector3(24f, 0.05f, -24f)),
        new("return_street", new Vector3(-7f, 0.05f, -41.5f)),
        new("zirat_roadside", new Vector3(-3.2f, 0.05f, -68f)),
        new("kara_approach", new Vector3(0f, 0.05f, -103f)),
        new("cliffhanger", new Vector3(0.6f, 0.05f, -122.5f))
    });
    public static IReadOnlyList<Waypoint> Route => RouteValue.Value;

    // ---- Authored walk chain (ordered open-road nodes) ---------------------
    // The continuous pedestrian corridor the route physically follows. It
    // hugs the road centreline, turns into the babai yard through its gate,
    // comes back out, branches to the FAP gate, returns to the main street
    // and rides south through the zirat roadside to the Kara cliffhanger.
    private static readonly Lazy<IReadOnlyList<Vector2>> WalkChainValue = new(() => new List<Vector2>
    {
        new(0f, 9f),
        new(-0.4f, 2f),
        new(-1.2f, HousePathAxis[0].Y),          // onto the house path at the street gate
        BabaiRelocation.Apply(new Vector2(-24f, 2.5f)),
        BabaiRelocation.Apply(new Vector2(-25.2f, 2.6f)),      // through the open yard gate
        BabaiRelocation.Apply(new Vector2(-26.05f, 2.6f)),     // inside the original yard gate
        BabaiRelocation.Apply(new Vector2(-26.05f, .2f)),
        new(HouseDoorApproach.X, HouseDoorApproach.Z),
        BabaiRelocation.Apply(new Vector2(-26.05f, .2f)),
        BabaiRelocation.Apply(new Vector2(-26.05f, 2.6f)),
        BabaiRelocation.Apply(new Vector2(-25.2f, 2.6f)),
        BabaiRelocation.Apply(new Vector2(-24f, 2.5f)),
        new(-1.2f, HousePathAxis[0].Y),
        new(-2.4f, -2.8f),      // back_to_street
        new(-2.5f, -5.5f),
        new(-2.2f, -8f),        // main_street
        new(-0.8f, -16f),
        new(-0.5f, -24f),       // FAP street
        new(4.5f, -24f),
        new(10f, -24f),         // fap_branch
        new(17f, -24f),
        new(23f, -24.5f),
        new(24f, -24f),         // fap_exterior (outside the gate)
        new(23f, -24.5f),       // turn around and take the street back west
        new(17f, -24f),
        new(8f, -24f),
        new(-0.5f, -24f),       // south down the main street again
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
    });
    public static IReadOnlyList<Vector2> WalkChain => WalkChainValue.Value;

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

/// <summary>
/// Relayout v3, stage 5 (2026-10-02): the babai and әби household stands on the main street.
/// The whole yard as it was authored (house, interior, bath, workshop, gates, yard props) is
/// carried by one rigid transform: turned 25.3° so the street door faces the street (+X) and
/// moved so that door is 6 m from the carriageway edge. Authored coordinates stay in their old
/// frame; <see cref="Apply"/> maps them to the street. Pure constants: safe in static initialisers.
/// </summary>
public static class BabaiRelocation
{
    public const float PivotX = -27.51474f, PivotZ = -1.88315f;   // the old street-door portal
    public const float DoorX = -9.5f, DoorZ = -1.88315f;           // where that door stands now
    public const float YawDegrees = 90f - 64.72228f;              // turn so the door faces +X
    // The old yard stood ~0.6 m above the street; it settles to street level at its gate.
    public const float Lift = -.6f;

    private static float Cos => Mathf.Cos(Mathf.DegToRad(YawDegrees));
    private static float Sin => Mathf.Sin(Mathf.DegToRad(YawDegrees));

    public static Vector2 Apply(Vector2 old)
    {
        float dx = old.X - PivotX, dz = old.Y - PivotZ;
        return new(DoorX + dx * Cos + dz * Sin, DoorZ - dx * Sin + dz * Cos);
    }

    public static Vector3 Apply(Vector3 old)
    {
        var p = Apply(new Vector2(old.X, old.Z));
        return new(p.X, old.Y, p.Y);
    }

    public static Vector2 Inverse(Vector2 now)
    {
        float dx = now.X - DoorX, dz = now.Y - DoorZ;
        return new(PivotX + dx * Cos - dz * Sin, PivotZ + dx * Sin + dz * Cos);
    }

    /// <summary>The rigid transform itself (rotation about the old door, then the move).</summary>
    public static Transform3D Transform =>
        new Transform3D(Basis.Identity, new Vector3(DoorX, Lift, DoorZ))
        * new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(YawDegrees)), Vector3.Zero)
        * new Transform3D(Basis.Identity, new Vector3(-PivotX, 0, -PivotZ));

    // Yard frame: forward toward the street, side to the right of the door (old frame
    // forward (.904, .427)); the yard is forward -12..5.8 m, side -15.5..10 m from the door.
    public const float YardBack = -12f, YardFront = 5.8f, YardSideMin = -14.2f, YardSideMax = 10f;

    /// <summary>Distance outside the new yard rectangle (0 inside).</summary>
    public static float OutsideNewYard(float x, float z)
    {
        float forward = x - DoorX, side = -(z - DoorZ);
        return Mathf.Max(0, Mathf.Max(Mathf.Max(YardBack - forward, forward - YardFront),
            Mathf.Max(YardSideMin - side, side - YardSideMax)));
    }

    /// <summary>True when an authored (old-frame) point lies in the old yard.</summary>
    public static bool InsideOldYard(float x, float z, float margin = 0f)
    {
        float dx = x - PivotX, dz = z - PivotZ;
        var forward = dx * .90424865f + dz * .42700631f;
        var side = dx * .42700631f - dz * .90424865f;
        return forward >= YardBack - margin && forward <= YardFront - .4f + margin
            && side >= YardSideMin - margin && side <= YardSideMax + margin;
    }
}
