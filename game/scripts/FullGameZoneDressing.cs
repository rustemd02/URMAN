using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only dressing for the authored Acts 2–5 zones.
/// The geometry is intentionally modular and low-poly, but each zone has a
/// readable human context instead of inheriting one anonymous forest path.
/// Narrative state and interaction ownership remain in RuntimeBridge.
/// </summary>
public static class FullGameZoneDressing
{
    public static void Build(Node3D root, string zoneId)
    {
        var dressing = new Node3D
        {
            Name = "ProductionDressing"
        };
        dressing.SetMeta("zoneId", zoneId);
        dressing.SetMeta("status", "greybox-production-pass");
        root.AddChild(dressing);

        switch (zoneId)
        {
            case "fullgame_act2_house": Act2House(dressing); break;
            case "fullgame_act2_river": Act2River(dressing); break;
            case "fullgame_act2_mosque": Act2MosqueCourtyard(dressing); break;
            case "fullgame_act2_council": Act2Council(dressing); break;
            case "fullgame_act3_archive": Act3Archive(dressing); break;
            case "fullgame_act3_soviet": Act3Soviet(dressing); break;
            case "fullgame_act3_water": Act3Water(dressing); break;
            case "fullgame_act4_tukay": Act4Tukay(dressing); break;
            case "fullgame_act4_1552": Act4Kazan1552(dressing); break;
            case "fullgame_act4_pact": Act4Pact(dressing); break;
            case "fullgame_act5_boundary": Act5Boundary(dressing); break;
            case "fullgame_act5_epilogue": Act5Epilogue(dressing); break;
            default: throw new ArgumentOutOfRangeException(nameof(zoneId), zoneId, "Unknown full-game zone.");
        }

        // Character-kit silhouettes are deliberately added after the zone
        // geometry so they sit in the authored composition without taking
        // ownership of progression or collisions.
        FullGameNpcDressing.Build(dressing, zoneId);
    }

    private static void Act2House(Node3D root)
    {
        GeneratedModularKitDressing.Attach(
            root,
            "act2-family-house",
            ["HouseA_", "FenceA_", "WellA_", "WoodpileA_"],
            new(-3.8f, 1.4f, -7.4f));
        AddBox(root, "FamilyHouseDoor", new(1.0f, 2.05f, 0.1f), new(-5.35f, 1.03f, -4.94f), "4c382d", "wood");
        AddBox(root, "PorchBench", new(2.1f, 0.16f, 0.42f), new(-3.5f, 0.72f, -4.25f), "70523c", "wood");
        AddLedgerTable(root, new(1.0f, 0, -5.7f), "6f513a");
        AddBox(root, "FamilyChest", new(1.4f, 0.72f, 0.82f), new(3.8f, 0.36f, -6.3f), "5b4938", "wood");
        AddPanel(root, "FamilyCloth", new(1.6f, 1.1f, 0.06f), new(4.0f, 1.55f, -7.2f), "725344");
        AddLantern(root, new(2.9f, 1.55f, -5.1f), "d89b58", 1.35f);
    }

    private static void Act2River(Node3D root)
    {
        AddBox(root, "RiverSurface", new(9.5f, 0.045f, 18f), new(0, 0.05f, -6.2f), "3f6264");
        AddBox(root, "RiverBankLeft", new(2.3f, 0.16f, 17f), new(-5.1f, 0.08f, -6.2f), "5d674b", "earth");
        AddBox(root, "RiverBankRight", new(2.3f, 0.16f, 17f), new(5.1f, 0.08f, -6.2f), "5d674b", "earth");
        AddDock(root, new(-2.1f, 0.16f, -5.0f));
        AddReeds(root, new(-4.0f, 0, -7.0f), 7, "536b53");
        AddReeds(root, new(4.0f, 0, -11.0f), 6, "536b53");
        AddBox(root, "TwoNameMarker", new(0.12f, 1.55f, 0.12f), new(1.6f, 0.78f, -4.2f), "79664e", "wood");
        AddPanel(root, "RiverMarkerBoard", new(1.3f, 0.58f, 0.08f), new(1.6f, 1.42f, -4.2f), "85745b");
        AddLantern(root, new(-2.1f, 1.45f, -5.0f), "8bb2b0", 0.85f);
    }

    private static void AddDock(Node3D root, Vector3 origin)
    {
        AddBox(root, "RiverDockPlatform", new(3.4f, 0.18f, 4.8f), origin + new Vector3(0, 0.09f, 0), "5f4938", "wood");
        AddBox(root, "RiverDockRailLeft", new(0.14f, 1.15f, 3.4f), origin + new Vector3(-1.48f, 0.68f, 0.45f), "5f4938", "wood");
        AddBox(root, "RiverDockRailRight", new(0.14f, 1.15f, 3.4f), origin + new Vector3(1.48f, 0.68f, 0.45f), "5f4938", "wood");
        AddBox(root, "RiverDockStep", new(2.15f, 0.16f, 0.9f), origin + new Vector3(0, 0.08f, -2.6f), "4b3b2f", "wood");
        AddBox(root, "RiverDockCrossbar", new(2.8f, 0.14f, 0.12f), origin + new Vector3(0, 0.98f, 1.95f), "4b3b2f", "wood");
    }

    private static void Act2MosqueCourtyard(Node3D root)
    {
        AddBox(root, "PrayerHallWall", new(11f, 2.8f, 0.28f), new(0, 1.4f, -8.2f), "9a8c78", "plaster");
        AddBox(root, "CourtyardGateLeft", new(0.34f, 2.65f, 1.35f), new(-2.2f, 1.32f, -7.55f), "6f5947", "wood");
        AddBox(root, "CourtyardGateRight", new(0.34f, 2.65f, 1.35f), new(2.2f, 1.32f, -7.55f), "6f5947", "wood");
        AddBox(root, "CourtyardLintel", new(4.75f, 0.3f, 1.35f), new(0, 2.58f, -7.55f), "765f4b", "wood");
        AddCylinder(root, "MinaretSilhouette", 0.28f, 4.1f, new(4.15f, 2.05f, -7.95f), "877963", "plaster");
        AddBox(root, "CourtyardRug", new(4.4f, 0.025f, 2.2f), new(0, 0.04f, -3.6f), "665246", "earth");
        AddBox(root, "CourtyardWaterBasin", new(1.0f, 0.38f, 0.76f), new(-3.0f, 0.19f, -4.5f), "696b61", "plaster");
        AddLantern(root, new(0, 2.3f, -6.8f), "d9a15e", 1.05f);
    }

    private static void Act2Council(Node3D root)
    {
        AddBox(root, "CouncilTable", new(5.6f, 0.18f, 1.4f), new(0, 0.86f, -5.8f), "5e4635", "wood");
        for (var side = -1; side <= 1; side += 2)
        {
            AddBox(root, $"CouncilBench{side}", new(5.2f, 0.48f, 0.48f), new(0, 0.34f, -5.8f + side * 1.35f), "705540", "wood");
        }

        AddPanel(root, "CouncilNoticeBoard", new(3.3f, 1.65f, 0.08f), new(-4.2f, 1.55f, -7.8f), "806d59", "wood");
        AddLedgerTable(root, new(1.55f, 0, -3.45f), "72543a");
        AddBox(root, "CouncilBell", new(0.22f, 0.34f, 0.22f), new(4.2f, 2.05f, -7.75f), "a4835f");
        AddLantern(root, new(0, 2.35f, -5.9f), "dda866", 1.4f);
    }

    private static void Act3Archive(Node3D root)
    {
        for (var index = 0; index < 3; index++)
        {
            AddArchiveShelf(root, new(-4.3f + index * 4.2f, 0, -7.3f), 3.1f, 1.2f);
        }

        AddLedgerTable(root, new(0, 0, -3.4f), "574231");
        AddBox(root, "ArchiveWindow", new(2.6f, 1.4f, 0.06f), new(0, 1.85f, -8.1f), "657878");
        AddPaperStack(root, new(-1.2f, 1.0f, -3.4f), "c4b594");
        AddPaperStack(root, new(1.35f, 1.0f, -3.45f), "a89778");
        AddLantern(root, new(2.6f, 2.15f, -4.4f), "dba262", 1.15f);
    }

    private static void Act3Soviet(Node3D root)
    {
        for (var index = 0; index < 3; index++)
        {
            AddBox(root, $"FileCabinet{index}", new(1.35f, 2.4f, 0.72f), new(-4.3f + index * 4.2f, 1.2f, -7.4f), "59605b", "plaster");
            AddBox(root, $"CabinetLabel{index}", new(0.72f, 0.12f, 0.035f), new(-4.3f + index * 4.2f, 1.55f, -7.02f), "a18b68");
        }

        AddBox(root, "SovietDesk", new(4.0f, 0.18f, 1.25f), new(0, 0.82f, -3.8f), "554538", "wood");
        GeneratedModularKitDressing.Attach(
            root,
            "act3-soviet-old-pc",
            ["TableA_", "OldPc_"],
            new(-0.65f, 0.82f, -3.65f));
        AddPanel(root, "BaranovFolderBoard", new(2.8f, 1.7f, 0.06f), new(3.9f, 1.35f, -7.7f), "734b3f", "plaster");
        AddLantern(root, new(-2.8f, 2.2f, -4.2f), "d18f61", 1.0f);
    }

    private static void Act3Water(Node3D root)
    {
        AddBox(root, "BlackWater", new(12f, 0.04f, 16f), new(0, 0.06f, -7.1f), "263f45");
        AddBox(root, "WaterBank", new(10f, 0.22f, 2.2f), new(0, 0.11f, -0.9f), "4d5c49", "earth");
        for (var index = 0; index < 10; index++)
        {
            var x = -4.3f + index * 0.95f;
            AddStone(root, new(x, 0.22f, -1.55f - index % 2 * 0.2f), 0.38f + index % 3 * 0.08f, "68685d");
        }

        AddReeds(root, new(-4.7f, 0, -4.0f), 8, "415c50");
        AddReeds(root, new(4.6f, 0, -9.0f), 8, "415c50");
        AddBox(root, "WaterNameMarker", new(0.14f, 1.6f, 0.14f), new(1.8f, 0.8f, -2.1f), "75644f", "wood");
        AddBox(root, "WaterMarkerCloth", new(0.9f, 0.55f, 0.04f), new(1.8f, 1.46f, -2.15f), "667b76");
        AddLantern(root, new(-1.8f, 1.5f, -2.0f), "76939a", 0.8f);
    }

    private static void Act4Tukay(Node3D root)
    {
        AddBox(root, "TukayDesk", new(4.7f, 0.17f, 1.35f), new(0, 0.84f, -5.8f), "674a38", "wood");
        AddBox(root, "OpenNotebook", new(1.45f, 0.045f, 0.82f), new(-0.55f, 0.96f, -5.62f), "c2af85");
        AddBox(root, "InkBottle", new(0.18f, 0.28f, 0.18f), new(0.75f, 1.08f, -5.72f), "303b38");
        AddArchiveShelf(root, new(-4.2f, 0, -7.4f), 2.8f, 1.1f);
        AddPanel(root, "PoemCloth", new(2.2f, 1.35f, 0.05f), new(3.8f, 1.5f, -7.7f), "775345");
        AddLantern(root, new(2.2f, 2.05f, -5.1f), "dda267", 1.3f);
    }

    private static void Act4Kazan1552(Node3D root)
    {
        AddBox(root, "HistoricalMapTable", new(5.4f, 0.17f, 1.65f), new(0, 0.86f, -5.8f), "594532", "wood");
        AddBox(root, "MapBoard", new(3.4f, 1.85f, 0.06f), new(-3.8f, 1.45f, -7.8f), "8c785d", "wood");
        AddBox(root, "MapStripe", new(2.8f, 0.06f, 0.04f), new(-3.8f, 1.58f, -7.73f), "b29c6f");
        AddStone(root, new(3.4f, 0.3f, -7.5f), 0.65f, "6e6b59");
        AddStone(root, new(4.35f, 0.24f, -7.7f), 0.45f, "77715e");
        AddBox(root, "BorderMarker", new(0.16f, 1.5f, 0.16f), new(2.0f, 0.75f, -3.4f), "76563f", "wood");
        AddLantern(root, new(0, 2.2f, -4.2f), "d59e62", 1.1f);
    }

    private static void Act4Pact(Node3D root)
    {
        AddBox(root, "PactWall", new(11f, 2.75f, 0.26f), new(0, 1.38f, -8.1f), "55433a", "wood");
        AddBox(root, "PactLedgerStand", new(2.0f, 1.05f, 1.0f), new(0, 0.58f, -5.6f), "513b31", "wood");
        AddBox(root, "PactLedger", new(1.25f, 0.18f, 0.82f), new(0, 1.18f, -5.58f), "98774d", "wood");
        AddBox(root, "LedgerCord", new(0.08f, 0.08f, 2.1f), new(-0.62f, 1.3f, -5.58f), "b69a70");
        AddBox(root, "WitnessChair", new(0.85f, 1.05f, 0.82f), new(2.7f, 0.52f, -6.2f), "5b463a", "wood");
        AddPanel(root, "PactCloth", new(2.6f, 1.25f, 0.05f), new(-3.8f, 1.45f, -7.75f), "64463e");
        AddLantern(root, new(0, 2.0f, -5.0f), "c88455", 1.2f);
    }

    private static void Act5Boundary(Node3D root)
    {
        GeneratedModularKitDressing.Attach(
            root,
            "act5-boundary-forest",
            ["PineA_", "GateA_"],
            new(-5.2f, 1.6f, -8.4f));
        // GateA is the authored threshold owner for this composition; do not
        // duplicate it with the former procedural post/board silhouette.
        AddReeds(root, new(-3.7f, 0, -7.5f), 5, "3c5548");
        AddReeds(root, new(3.7f, 0, -8.1f), 5, "3c5548");
        AddLantern(root, new(0, 1.85f, -6.75f), "c98055", 1.45f);
    }

    private static void Act5Epilogue(Node3D root)
    {
        // The canonical epilogue keeps one authored HouseA silhouette in the
        // frame. The distant house remains a quiet procedural background cue;
        // the imported module owns only presentation and its provisional
        // layer-2 proxy, never progression or interaction state.
        GeneratedModularKitDressing.Attach(
            root,
            "act5-epilogue-house",
            ["HouseA_", "WoodpileA_"],
            new(-5.0f, 1.4f, -8.1f));
        AddHouse(root, "DistantHouse", new(5.7f, 0, -12.2f), "66685f", "42413b", warmWindow: false);
        AddBox(root, "EmptyBench", new(2.1f, 0.16f, 0.45f), new(-2.8f, 0.7f, -4.0f), "55483d", "wood");
        AddBox(root, "EpilogueMarker", new(0.16f, 1.4f, 0.16f), new(2.2f, 0.7f, -7.0f), "665743", "wood");
        AddBox(root, "MarkerCloth", new(0.78f, 0.48f, 0.04f), new(2.2f, 1.4f, -7.05f), "696d62");
        AddLantern(root, new(-2.8f, 1.55f, -3.55f), "9d7958", 0.45f);
    }

    private static void AddHouse(Node3D root, string name, Vector3 origin, string wallColor, string roofColor, bool warmWindow = true)
    {
        AddBox(root, name, new(5.8f, 2.65f, 4.8f), origin + new Vector3(0, 1.32f, 0), wallColor, "plaster");
        AddRotatedBox(root, $"{name}RoofLeft", new(3.5f, 0.24f, 5.2f), origin + new Vector3(-1.35f, 3.2f, 0), new(0, 0, 24), roofColor, "wood");
        AddRotatedBox(root, $"{name}RoofRight", new(3.5f, 0.24f, 5.2f), origin + new Vector3(1.35f, 3.2f, 0), new(0, 0, -24), roofColor, "wood");
        AddBox(root, $"{name}Window", new(1.05f, 0.78f, 0.05f), origin + new Vector3(0.95f, 1.45f, 2.43f), warmWindow ? "c9985e" : "535852");
        AddBox(root, $"{name}Door", new(1.0f, 2.05f, 0.1f), origin + new Vector3(-1.55f, 1.03f, 2.46f), "4c382d", "wood");
    }

    private static void AddLedgerTable(Node3D root, Vector3 origin, string color)
    {
        AddBox(root, "LedgerTable", new(2.8f, 0.16f, 1.1f), origin + new Vector3(0, 0.86f, 0), color, "wood");
        AddBox(root, "LedgerTableLegL", new(0.16f, 0.86f, 0.16f), origin + new Vector3(-1.15f, 0.43f, 0), color, "wood");
        AddBox(root, "LedgerTableLegR", new(0.16f, 0.86f, 0.16f), origin + new Vector3(1.15f, 0.43f, 0), color, "wood");
        AddPaperStack(root, origin + new Vector3(-0.55f, 0.95f, -0.08f), "bdac84");
    }

    private static void AddArchiveShelf(Node3D root, Vector3 origin, float height, float width)
    {
        AddBox(root, "ArchiveShelf", new(width, height, 0.28f), origin + new Vector3(0, height * 0.5f, 0), "5b4639", "wood");
        for (var row = 0; row < 3; row++)
        {
            AddBox(root, "ArchiveShelfRow", new(width * 0.82f, 0.28f, 0.34f), origin + new Vector3(0, 0.42f + row * 0.75f, -0.2f), row % 2 == 0 ? "7c5d46" : "6a6f5f", "wood");
        }
    }

    private static void AddPaperStack(Node3D root, Vector3 origin, string color)
    {
        AddBox(root, "PaperStack", new(0.82f, 0.06f, 0.62f), origin, color);
        AddBox(root, "PaperStackTop", new(0.74f, 0.025f, 0.56f), origin + new Vector3(0.04f, 0.05f, -0.03f), "d7c49a");
    }

    private static void AddReeds(Node3D root, Vector3 origin, int count, string color)
    {
        for (var index = 0; index < count; index++)
        {
            var offset = new Vector3((index % 3 - 1) * 0.22f, 0, (index / 3) * 0.48f);
            AddRotatedBox(root, "Reed", new(0.035f, 0.9f + index % 3 * 0.18f, 0.035f), origin + offset + new Vector3(0, 0.45f, 0), new(index % 2 == 0 ? -7 : 9, index * 23, 0), color);
        }
    }

    private static void AddLantern(Node3D root, Vector3 position, string lightColor, float energy)
    {
        AddBox(root, "StoryLantern", new(0.28f, 0.42f, 0.28f), position, "8b6547", "wood");
        root.AddChild(new OmniLight3D
        {
            Name = "StoryLanternLight",
            Position = position + new Vector3(0, -0.04f, 0),
            LightColor = Color.FromHtml(lightColor),
            LightEnergy = energy,
            OmniRange = 4.0f,
            ShadowEnabled = false
        });
    }

    private static void AddStone(Node3D root, Vector3 position, float size, string color)
    {
        var stone = new MeshInstance3D
        {
            Name = "RiverStone",
            Position = position,
            Scale = new Vector3(size, size * 0.65f, size * 0.82f),
            Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 3 },
            MaterialOverride = Material(color)
        };
        root.AddChild(stone);
    }

    private static void AddCylinder(Node3D root, string name, float radius, float height, Vector3 position, string color, string surface = "")
    {
        root.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.1f, Height = height, RadialSegments = 8 },
            MaterialOverride = Material(color, surface)
        });
    }

    private static void AddPanel(Node3D root, string name, Vector3 size, Vector3 position, string color, string surface = "") =>
        AddBox(root, name, size, position, color, surface);

    private static void AddRotatedBox(Node3D root, string name, Vector3 size, Vector3 position, Vector3 rotation, string color, string surface = "")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotation,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color, surface)
        };
        root.AddChild(mesh);
    }

    private static void AddBox(Node3D root, string name, Vector3 size, Vector3 position, string color, string surface = "")
    {
        root.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color, surface)
        });
    }

    private static Material Material(string htmlColor, string surface = "") =>
        PainterlyMaterialLibrary.ForColor(htmlColor, surface);
}
