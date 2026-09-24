using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// The ravine that splits Kyrlay in two (author, 2026-09-25: "строй по схеме",
/// "пролёт обрушен"). A frozen stream runs north from the river east of the
/// FAP. The accessible half of Act I lies west of it; the far bank carries the
/// second half of the village along its own lane, seen across the ravine. The
/// only bridge has lost its middle span: both ends still stand, the stringers
/// hang into the ravine, and villagers have set trestles across the near end.
/// The ravine sides are the boundary, as the river banks are; the rim fence
/// and the trestles make that visible and keep a player from sliding in.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal const float RavineBridgeZ = -25f;
    // Rim fence and trestles stand this far from the stream centre, on level
    // ground just past the lip.
    private const float RavineRimOffset = 5.0f;
    private const float RavineFenceGap = 1.9f;

    /// <summary>Lane along the far bank; the second half's houses face it.</summary>
    internal static readonly Vector2[] RavineFarLane =
    [
        new(58.0f, RavineBridgeZ), new(63.0f, -24.4f), new(69.0f, -21.0f), new(69.0f, -4.0f),
        new(68.6f, 12.0f), new(69.0f, 26.0f)
    ];

    internal static readonly Vector2[] RavineFarLaneSouth =
    [
        new(69.0f, -21.0f), new(68.6f, -36.0f), new(69.2f, -58.0f)
    ];

    /// <summary>Trampled lane from the corner of the FAP service loop to the bridge.</summary>
    internal static readonly Vector2[] RavineBridgeApproach =
    [
        new(41.5f, RavineBridgeZ), new(43.5f, -25.1f), new(45.3f, RavineBridgeZ)
    ];

    // Second half of the village. Five houses that used to close the FAP's
    // east field keep their names and address ids; four more complete the
    // lane. West row faces east, east row faces west.
    private static readonly (string Name, float X, float Z, float Yaw)[] RavineFarBankHouses =
    [
        ("FapEastHorizonAuthoredHouse", 62.5f, -52f, 90f),
        ("FapEastViewHouse", 62.5f, -38f, 90f),
        ("FapRightFieldHouse", 62.5f, -12f, 90f),
        ("MainStreetEastFarParcelHouse", 62.5f, 2f, 90f),
        ("FarBankNorthHouse", 62.5f, 16f, 90f),
        ("FarBankSouthHouse", 77.0f, -44f, -90f),
        ("FapEastFieldViewHouse", 77.0f, -30f, -90f),
        ("FarBankMidHouse", 77.0f, -4f, -90f),
        ("FarBankEndHouse", 77.0f, 20f, -90f)
    ];

    private static float RavineCentreX(float z) => (float)AgentBAct1HeightField.RavineCentre(z);

    private static void AddVillageRavine(Node3D core)
    {
        var ravine = new Node3D { Name = "VillageRavine" };
        ravine.SetMeta("presentationOnly", true);
        ravine.SetMeta("collisionOwner", "ravine-blocker");
        ravine.SetMeta("navigationOwner", "none");
        ravine.SetMeta("interactionOwner", "none");
        ravine.SetMeta("presentationRole",
            "ravine with a frozen stream splitting the village; accessible half west, second half on the far bank");
        core.AddChild(ravine);
        var proxy = new StaticBody3D { Name = "RavineCollisionProxy", CollisionLayer = 2, CollisionMask = 0 };
        proxy.SetMeta("collisionOwner", "ravine-blocker");
        proxy.SetMeta("collisionStatus", "authored-blocker-layer-2");
        ravine.AddChild(proxy);

        AddRavineStream(ravine);
        AddRavineBanks(ravine);
        AddRavineRimFence(ravine, proxy);
        AddCollapsedRavineBridge(ravine, proxy);
        AddRavineFarBank(ravine);
    }

    private static void AddRavineStream(Node3D ravine)
    {
        var ice = PainterlyMaterialLibrary.ForColor("5c6f74", "ice");
        var water = PainterlyMaterialLibrary.ForColor("33463f", "water");
        var index = 0;
        for (var z = -84f; z <= 66f; z += 3f, index++)
        {
            var x = RavineCentreX(z);
            var heading = Mathf.RadToDeg(Mathf.Atan2(RavineCentreX(z + 1.5f) - RavineCentreX(z - 1.5f), 3f));
            var slab = new MeshInstance3D
            {
                Name = $"RavineIce_{index}",
                Position = new Vector3(x, (float)AgentBAct1HeightField.Ground(x, z) + .06f, z),
                RotationDegrees = new Vector3(0f, heading, 0f),
                Mesh = new BoxMesh { Size = new Vector3(1.9f + .5f * Mathf.Sin(z * .7f), .16f, 3.4f) },
                MaterialOverride = index % 5 == 2 ? water : ice
            };
            slab.SetMeta("visualOnly", true);
            ravine.AddChild(slab);
        }
    }

    /// <summary>
    /// The slopes hold less snow than the fields: dark earth and dry grass
    /// low down by the stream, grey thin snow higher up, and bare willow scrub
    /// on the bed. Without them the cut reads as flat white from the rim.
    /// </summary>
    private static void AddRavineBanks(Node3D ravine)
    {
        foreach (var side in new[] { -1f, 1f })
        foreach (var (name, inner, outer, colour, surface) in new[]
                 {
                     ("Lower", .9f, 2.7f, "5e584c", "earth"),
                     ("Upper", 2.7f, 4.7f, "a4adb2", "snow_ground")
                 })
        {
            const float step = 1.5f;
            const int across = 4;
            var rows = Mathf.CeilToInt((68f + 86f) / step) + 1;
            var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 Point(int row, int column)
            {
                var z = -86f + row * step;
                var d = Mathf.Lerp(inner, outer, column / (float)across);
                var x = RavineCentreX(z) + side * d;
                return new Vector3(x, AgentBAct1HeightField.CollisionGround(x, z) + .05f, z);
            }
            for (var row = 0; row < rows - 1; row++)
            for (var column = 0; column < across; column++)
            {
                var a = Point(row, column); var b = Point(row, column + 1);
                var c = Point(row + 1, column); var d = Point(row + 1, column + 1);
                // Both windings: the strip is seen from the rim and from the bed.
                var quad = new[] { a, c, b, b, c, d, a, b, c, b, d, c };
                foreach (var vertex in quad)
                {
                    tool.SetUV(new Vector2(vertex.X * .25f, vertex.Z * .25f));
                    tool.AddVertex(vertex);
                }
            }
            tool.GenerateNormals();
            var bank = new MeshInstance3D
            {
                Name = $"RavineBank{name}_{(side < 0 ? "West" : "East")}",
                Mesh = tool.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface)
            };
            bank.SetMeta("visualOnly", true);
            ravine.AddChild(bank);
        }
        var index = 0;
        for (var z = -80f; z <= 64f; z += 6.5f, index++)
        {
            var side = index % 2 == 0 ? -1f : 1f;
            var x = RavineCentreX(z) + side * (1.6f + .5f * Mathf.Sin(z));
            AddVisualTree(ravine, $"RavineWillow_{index}", new Vector3(x, 0f, z), 2.4f + .8f * Mathf.Abs(Mathf.Sin(z * .37f)),
                VegetationStyle.Broadleaf, "4a4a3e");
        }
    }

    /// <summary>
    /// An old rail fence where the gardens end at the ravine. It runs from the
    /// river bank to the north forest and opens only at the bridge, where the
    /// trestles close the gap.
    /// </summary>
    private static void AddRavineRimFence(Node3D ravine, StaticBody3D proxy)
    {
        const float step = 4f;
        var index = 0;
        for (var z = -80f; z < 70f; z += step, index++)
        {
            var z0 = z;
            var z1 = Math.Min(z + step, 70f);
            // Open only across the bridge approach.
            if (z1 > RavineBridgeZ - RavineFenceGap && z0 < RavineBridgeZ + RavineFenceGap)
            {
                if (z0 < RavineBridgeZ - RavineFenceGap) z1 = RavineBridgeZ - RavineFenceGap;
                else if (z1 > RavineBridgeZ + RavineFenceGap) z0 = RavineBridgeZ + RavineFenceGap;
                else continue;
            }
            var a = new Vector3(RavineCentreX(z0) - RavineRimOffset, 0f, z0);
            var b = new Vector3(RavineCentreX(z1) - RavineRimOffset, 0f, z1);
            AddVisualFenceRun(ravine, $"RavineRimFence_{index}", a, b);
            var middle = (a + b) * .5f;
            var length = new Vector2(b.X - a.X, b.Z - a.Z).Length();
            proxy.AddChild(new CollisionShape3D
            {
                Name = $"RavineRimBlocker_{index}",
                Position = new Vector3(middle.X, AgentBAct1HeightField.CollisionGround(middle.X, middle.Z) + .7f, middle.Z),
                RotationDegrees = new Vector3(0f, Mathf.RadToDeg(Mathf.Atan2(b.X - a.X, b.Z - a.Z)), 0f),
                Shape = new BoxShape3D { Size = new Vector3(.3f, 1.4f, length + .3f) }
            });
        }
    }

    private static void AddCollapsedRavineBridge(Node3D ravine, StaticBody3D proxy)
    {
        var z = RavineBridgeZ;
        var cx = RavineCentreX(z);
        var bridge = new Node3D { Name = "RavineBridgeCollapsed", Position = new Vector3(cx, 0f, z) };
        bridge.SetMeta("presentationOnly", true);
        bridge.SetMeta("visualOnly", true);
        bridge.SetMeta("presentationRole",
            "emergency bridge over the ravine: the middle span has fallen in, both ends still stand; not crossable");
        ravine.AddChild(bridge);

        float Ground(float localX, float localZ = 0f) => AgentBAct1HeightField.CollisionGround(cx + localX, z + localZ);
        var bed = (float)AgentBAct1HeightField.Ground(cx, z);
        var nearDeck = Ground(-5.6f) + .34f;
        var farDeck = Ground(5.6f) + .34f;

        foreach (var (side, deck) in new[] { (-1f, nearDeck), (1f, farDeck) })
        {
            var label = side < 0 ? "Near" : "Far";
            // Log crib abutment, laid crosswise the way village bridges are.
            var cribX = side * 5.6f;
            var cribBase = Ground(cribX) - .45f;
            for (var level = 0; cribBase + level * .28f < deck - .2f; level++)
            {
                var y = cribBase + .14f + level * .28f;
                foreach (var offset in new[] { -1f, 1f })
                {
                    var along = level % 2 == 0;
                    var from = along ? new Vector3(cribX - 1.1f, y, offset * 1.25f) : new Vector3(cribX + offset * .8f, y, -1.6f);
                    var to = along ? new Vector3(cribX + 1.1f, y, offset * 1.25f) : new Vector3(cribX + offset * .8f, y, 1.6f);
                    AddRavineLog(bridge, $"Bridge{label}Crib_{level}_{(offset < 0 ? "a" : "b")}", from, to, .14f, "5b4a3a");
                }
            }
            // Stringers: land end on the bank, open end past the lip.
            var landX = side * 8.0f;
            var openX = side < 0 ? -2.3f : 3.1f;
            foreach (var stringerZ in new[] { -1.1f, 0f, 1.1f })
                AddRavineLog(bridge, $"Bridge{label}Stringer_{stringerZ:0.0}",
                    new Vector3(landX, deck - .2f, stringerZ), new Vector3(openX, deck - .2f, stringerZ), .13f, "4f4032");
            // Deck boards, thinning toward the broken edge.
            var board = 0;
            for (var x = landX; side < 0 ? x < openX - .1f : x > openX + .1f; x -= side * .3f, board++)
            {
                var nearEdge = Math.Abs(x - openX) < 1.2f;
                if (nearEdge && board % 3 == 1) continue;
                var length = nearEdge ? 1.6f + .6f * Mathf.Sin(board * 2.3f) : 3.0f;
                AddVisualBox(bridge, $"Bridge{label}Board_{board}", new Vector3(.26f, .06f, length),
                    new Vector3(x, deck - .04f, nearEdge ? .5f * Mathf.Sin(board * 1.7f) : 0f),
                    board % 4 == 0 ? "6b5a45" : "5f4f3d", "wood", yawDegrees: nearEdge ? 4f * Mathf.Sin(board) : 0f);
            }
            // Railing on both sides; the last post by the break leans out.
            foreach (var railZ in new[] { -1.55f, 1.55f })
            {
                var postIndex = 0;
                for (var x = landX; side < 0 ? x <= openX + .05f : x >= openX - .05f; x -= side * 1.45f, postIndex++)
                {
                    var last = Math.Abs(x - openX) < 1.45f;
                    AddVisualBox(bridge, $"Bridge{label}RailPost_{railZ:0.0}_{postIndex}", new Vector3(.12f, 1.05f, .12f),
                        new Vector3(x, deck + .5f, railZ), "5b4a3a", "wood", rollDegrees: last ? side * 14f : 0f);
                }
                var railEnd = openX + side * .9f;
                AddRavineLog(bridge, $"Bridge{label}Handrail_{railZ:0.0}",
                    new Vector3(landX, deck + .98f, railZ), new Vector3(railEnd, deck + .9f, railZ * 1.04f), .05f, "6b5a45");
            }
        }

        // The fallen middle: stringers hang from the near end to the bed, one
        // lies across the stream, boards are scattered on the ice.
        foreach (var (name, from, to) in new[]
                 {
                     ("BridgeFallenStringer_a", new Vector3(-2.2f, nearDeck - .3f, -1.1f), new Vector3(.9f, bed + .35f, -1.6f)),
                     ("BridgeFallenStringer_b", new Vector3(-2.2f, nearDeck - .3f, 1.1f), new Vector3(1.2f, bed + .3f, 2.1f)),
                     ("BridgeFallenStringer_c", new Vector3(3.0f, farDeck - .3f, 0f), new Vector3(.4f, bed + .25f, .6f)),
                     ("BridgeFallenStringer_d", new Vector3(-1.8f, bed + .2f, -2.4f), new Vector3(2.0f, bed + .22f, -.4f))
                 })
            AddRavineLog(bridge, name, from, to, .13f, "4a3b2e");
        for (var i = 0; i < 7; i++)
        {
            AddVisualBox(bridge, $"BridgeFallenBoard_{i}", new Vector3(.26f, .06f, 1.4f + .5f * Mathf.Sin(i * 1.9f)),
                new Vector3(-1.4f + i * .45f, bed + .14f + .05f * (i % 2), 1.6f * Mathf.Sin(i * 2.1f)),
                "5f4f3d", "wood", yawDegrees: 35f * Mathf.Sin(i * 1.3f) + 20f, rollDegrees: 8f * Mathf.Cos(i));
        }

        // Trestles (kozly) across the near end, on the bank before the lip.
        var trestleX = -RavineRimOffset;
        var trestleGround = Ground(trestleX);
        foreach (var poleY in new[] { .95f, .5f })
            AddRavineLog(bridge, $"BridgeTrestlePole_{poleY:0.00}",
                new Vector3(trestleX, trestleGround + poleY, -RavineFenceGap - .2f),
                new Vector3(trestleX, trestleGround + poleY + .04f, RavineFenceGap + .2f), .06f, "7a6a55");
        foreach (var legZ in new[] { -1.4f, 1.4f })
        foreach (var lean in new[] { -1f, 1f })
            AddRavineLog(bridge, $"BridgeTrestleLeg_{legZ:0.0}_{lean:0}",
                new Vector3(trestleX + lean * .45f, trestleGround - .05f, legZ),
                new Vector3(trestleX - lean * .1f, trestleGround + 1.05f, legZ), .05f, "6b5a45");
        foreach (var lean in new[] { -1f, 1f })
            AddRavineLog(bridge, $"BridgeTrestleCross_{lean:0}",
                new Vector3(trestleX + .08f, trestleGround + .2f, lean * -1.3f),
                new Vector3(trestleX + .08f, trestleGround + 1.2f, lean * 1.3f), .07f, "8a7858");
        proxy.AddChild(new CollisionShape3D
        {
            Name = "RavineBridgeTrestleBlocker",
            Position = new Vector3(cx + trestleX, trestleGround + .75f, z),
            Shape = new BoxShape3D { Size = new Vector3(.5f, 1.5f, RavineFenceGap * 2f + .6f) }
        });
    }

    private static void AddRavineFarBank(Node3D ravine)
    {
        var farBank = new Node3D { Name = "RavineFarBank" };
        farBank.SetMeta("presentationOnly", true);
        farBank.SetMeta("presentationRole", "second half of the village beyond the ravine; seen, not reached, in Act I");
        ravine.AddChild(farBank);

        foreach (var (name, points, width) in new[]
                 {
                     ("RavineBridgeApproachLane", RavineBridgeApproach, 1.8f),
                     ("RavineFarLane", RavineFarLane, 3.2f),
                     ("RavineFarLaneSouth", RavineFarLaneSouth, 2.8f)
                 })
        {
            using var curve = new Curve3D();
            foreach (var point in points) curve.AddPoint(new Vector3(point.X, 0f, point.Y));
            AddVisualLandformSurface(farBank, name, width, .025f, curve.GetBakedLength(), new(0f, .02f, 0f),
                "cbd3d8", "snow_trampled", 0f, true, curve);
        }

        foreach (var (name, x, z, yaw) in RavineFarBankHouses)
        {
            AddDistantHouse(farBank, new Vector3(x, 0f, z), yaw, name);
            // A front fence toward the lane with a gap for the gate: the H02
            // plot line, 3 m in front of the house.
            var front = Math.Sign(69f - x);
            var fenceX = x + front * 4.7f;
            AddVisualFenceRun(farBank, name + "FrontFenceSouth", new Vector3(fenceX, 0f, z - 5.5f), new Vector3(fenceX, 0f, z - 1.2f), true);
            AddVisualFenceRun(farBank, name + "FrontFenceNorth", new Vector3(fenceX, 0f, z + 1.2f), new Vector3(fenceX, 0f, z + 5.5f), true);
        }
        foreach (var (name, x, z, height, style, colour) in new[]
                 {
                     ("FarBankBirch_a", 58.5f, -45f, 8.4f, VegetationStyle.Birch, "596047"),
                     ("FarBankBirch_b", 59.0f, 9f, 7.8f, VegetationStyle.Birch, "596047"),
                     ("FarBankBroadleaf", 77.5f, -17f, 8.8f, VegetationStyle.Broadleaf, "48553f"),
                     ("FarBankConifer_a", 79.0f, 8f, 9.6f, VegetationStyle.Conifer, "30483f"),
                     ("FarBankBirch_c", 77.0f, -56f, 8.0f, VegetationStyle.Birch, "596047")
                 })
            AddVisualTree(farBank, name, new Vector3(x, 0f, z), height, style, colour);
    }

    /// <summary>A round log between two points (local to <paramref name="parent"/>).</summary>
    private static MeshInstance3D AddRavineLog(Node3D parent, string name, Vector3 from, Vector3 to, float radius, string colour)
    {
        var axis = to - from;
        var up = axis.Normalized();
        var side = Math.Abs(up.Dot(Vector3.Forward)) > .9f ? Vector3.Right : Vector3.Forward;
        var x = side.Cross(up).Normalized();
        var z = x.Cross(up).Normalized();
        var log = new MeshInstance3D
        {
            Name = name,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.06f, Height = axis.Length(), RadialSegments = 8, Rings = 1 },
            Transform = new Transform3D(new Basis(x, up, z), (from + to) * .5f),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, "wood")
        };
        log.SetMeta("visualOnly", true);
        parent.AddChild(log);
        return log;
    }
}
