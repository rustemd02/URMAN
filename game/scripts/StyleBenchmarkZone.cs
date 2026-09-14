using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class StyleBenchmarkZone : Node3D
{
    private static readonly string[] ForestFoliagePalette =
    [
        "26372f", // deep wet pine
        "2f4438", // cool moss shadow
        "354b3f", // muted mid green
        "3a4b3d", // desaturated olive
        "30483f"  // blue-green distance wash
    ];

    private const string FapClinicKitScenePath =
        "res://assets/models/act1/urman_fap_clinic_kit.glb";
    private const string FapClinicKitRootName = "URMAN_FapClinicKit";
    private const string FapInteriorSetComponentName = "FapInteriorSet";
    private const string ZiratRoadsideKitScenePath =
        "res://assets/models/act1/urman_zirat_roadside_kit.glb";
    private const string ZiratRoadsideKitRootName = "URMAN_ZiratRoadsideKit";
    private const string KaraForestEdgeKitScenePath =
        "res://assets/models/act1/urman_kara_forest_edge_kit.glb";
    private const string KaraForestEdgeKitRootName = "URMAN_KaraForestEdgeKit";

    private static readonly string[] ZiratRoadsideKitComponentNames =
    [
        "WetRoadShoulder_Left",
        "WetRoadShoulder_Right",
        "RoadsideDitch",
        "CulvertStoneCluster",
        "ZiratBoundaryFence",
        "ZiratOpenGate",
        "ZiratMarkerGroup_Low",
        "ZiratMarkerGroup_Far",
        "ZiratPathEdge",
        "ZiratBirchShrubMass",
        "ZiratDistantVillageMass"
    ];

    private static readonly string[] KaraForestEdgeKitComponentNames =
    [
        "ForestBank_Left",
        "ForestBank_Right",
        "MixedTreeCluster_Left",
        "MixedTreeCluster_Right",
        "CrookedPineMass",
        "BirchEdgeMass",
        "RootWall_Left",
        "RootWall_Right",
        "FallenLogCluster",
        "MossyBoulderCluster",
        "CrookedStump",
        "DistantForestMass_Low",
        "DistantForestMass_Tall"
    ];

    public enum BenchmarkKind
    {
        DayStreet,
        HouseOldPc,
        KaraUrmanNight,
        FapClinic,
        ZiratRoad
    }

    [Export]
    public BenchmarkKind ZoneKind { get; set; }

    public override void _Ready()
    {
        BuildEnvironment();
        switch (ZoneKind)
        {
            case BenchmarkKind.DayStreet:
                BuildDayStreet();
                break;
            case BenchmarkKind.HouseOldPc:
                BuildHouseOldPc();
                break;
            case BenchmarkKind.KaraUrmanNight:
                BuildKaraUrmanNight();
                break;
            case BenchmarkKind.FapClinic:
                BuildFapClinic();
                break;
            case BenchmarkKind.ZiratRoad:
                BuildZiratRoad();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void BuildEnvironment()
    {
        var night = ZoneKind == BenchmarkKind.KaraUrmanNight;
        var interior = ZoneKind is BenchmarkKind.HouseOldPc or BenchmarkKind.FapClinic;
        var houseInterior = ZoneKind == BenchmarkKind.HouseOldPc;
        var fapInterior = ZoneKind == BenchmarkKind.FapClinic;
        var zirat = ZoneKind == BenchmarkKind.ZiratRoad;
        var sky = interior ? null : new Sky
        {
            RadianceSize = Sky.RadianceSizeEnum.Size256,
            SkyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = night
                    ? Color.FromHtml("111e27")
                    : zirat ? Color.FromHtml("3b4e55") : Color.FromHtml("334b57"),
                SkyHorizonColor = night
                    ? Color.FromHtml("465b63")
                    : zirat ? Color.FromHtml("9aa8a5") : Color.FromHtml("a6b3b0"),
                SkyCurve = night ? 0.18f : 0.12f,
                GroundBottomColor = night ? Color.FromHtml("101b1d") : Color.FromHtml("2b3a35"),
                GroundHorizonColor = night
                    ? Color.FromHtml("344a4d")
                    : zirat ? Color.FromHtml("71807a") : Color.FromHtml("7f9186"),
                GroundCurve = 0.16f,
                SunAngleMax = night ? 1.2f : 4.2f,
                SunCurve = 0.08f
            }
        };
        var environment = new global::Godot.Environment
        {
            BackgroundMode = interior ? global::Godot.Environment.BGMode.Color : global::Godot.Environment.BGMode.Sky,
            Sky = sky,
            BackgroundColor = night
                ? Color.FromHtml("17232a")
                : houseInterior ? Color.FromHtml("3b2b21")
                : fapInterior ? Color.FromHtml("303e43")
                : Color.FromHtml("52646b"),
            AmbientLightSource = night || interior
                ? global::Godot.Environment.AmbientSource.Color
                : global::Godot.Environment.AmbientSource.Sky,
            // Kara-Urman needs a readable cold value floor: the previous
            // calibration collapsed the walkable path and foreground trees
            // into one blue-black mass at first-person distance.
            AmbientLightColor = night
                ? Color.FromHtml("748a91")
                : houseInterior ? Color.FromHtml("a89b8e")
                : fapInterior ? Color.FromHtml("879397")
                : zirat ? Color.FromHtml("99a6a2")
                : Color.FromHtml("a5b0ab"),
            // Keep the clinic's cold institutional base restrained so the
            // window and document pools can establish the room's depth.
            AmbientLightEnergy = night ? 0.82f : houseInterior ? 0.58f : fapInterior ? 0.48f : zirat ? 0.72f : 0.78f,
            SsaoEnabled = interior,
            SsaoIntensity = 0.55f,
            SsaoRadius = 0.30f,
            FogEnabled = !interior,
            FogLightColor = night
                ? Color.FromHtml("4d626a")
                : zirat ? Color.FromHtml("718080") : Color.FromHtml("758888"),
            FogDensity = night ? 0.0058f : zirat ? 0.0050f : 0.0046f,
            FogHeight = 0.9f,
            FogHeightDensity = night ? 0.10f : 0.06f,
            FogAerialPerspective = night ? 0.52f : 0.46f,
            FogSkyAffect = night ? 0.28f : 0.25f,
            FogSunScatter = night ? 0.08f : 0.06f,
            TonemapMode = global::Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = night ? 1.02f : fapInterior ? 1.04f : zirat ? 0.96f : 0.98f
        };
        AddChild(new WorldEnvironment { Environment = environment, Name = "WorldEnvironment" });

        if (!interior)
        {
            var sun = new DirectionalLight3D
            {
                Name = "MainDirectionalLight",
                RotationDegrees = night ? new(-52, -28, 0) : new(-48, -32, 0),
                LightColor = night
                    ? Color.FromHtml("748a92")
                    : zirat ? Color.FromHtml("c0c9c7") : Color.FromHtml("c9d0ce"),
                LightEnergy = night ? 0.78f : zirat ? 0.98f : 1.08f,
                ShadowEnabled = true
            };
            AddChild(sun);
        }
    }

    private void BuildDayStreet()
    {
        MakeBox("Ground", new(42, 0.25f, 42), new(0, -0.125f, 0), "59604b");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "Road", 5.2f, 42f, Vector3.Zero, "685b49");
        // Per-patch relief contact seating measured against Road (not a
        // cluster-wide shift): only patch 0 needed -9.661 mm.
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "PuddleNear", new(-0.65f, 0.112f, 5.2f), new(1.35f, 2.45f), patchYOffsetCorrections: [-0.009661116f, 0f, 0f]);
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "PuddleMiddle", new(0.2f, 0.105f, -1.8f), new(0.78f, 1.5f), "526066");
        // Keep the far cluster seated on the authored relief rather than
        // floating above the road crown at its three irregular patch offsets.
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "PuddleFar", new(0.92f, 0.092f, -9.5f), new(0.72f, 1.42f), "536066", [-0.005249664f, -0.007249661f, -0.009249665f]);

        MakeHouse(new(-7.5f, 0, -5), "75634e", "493f36", warmWindow: true);
        MakeHouse(new(7.7f, 0, -11), "69746b", "3e4440", warmWindow: false);
        MakeHouse(new(-8.4f, 0, -17), "806d58", "494039", warmWindow: false);
        // Keep one project-original Blender house in the camera composition so
        // the benchmark judges the actual imported modular asset, not only the
        // procedural greybox dressing used to lay out the street.
        GeneratedModularKitDressing.Attach(
            this,
            "style-day-house",
            ["HouseA_"],
            new(8.0f, 1.4f, -4.8f));
        SetMeta("styleImportedModules", "HouseA_project_original");
        MakeCollisionBox("VillageWellCollision", new(1.9f, 1.2f, 1.6f), new(-4.9f, 0.6f, 4.6f));
        var well = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-day-well",
            ["WellA_"],
            new(-4.9f, 0.0f, 4.6f),
            uniformScale: 1.0f,
            yawDegrees: -7f);
        well.Name = "GeneratedWellA";
        well.SetMeta("stylePresentationModule", "WellA_project_original");

        MakeCollisionBox("FirewoodCollision", new(1.6f, 1.2f, 1.1f), new(5.0f, 0.6f, 2.3f));
        var woodpile = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-day-woodpile",
            ["WoodpileA_"],
            new(5.0f, 0.0f, 2.3f),
            uniformScale: 1.0f,
            yawDegrees: 14f);
        woodpile.Name = "GeneratedWoodpileA";
        woodpile.SetMeta("stylePresentationModule", "WoodpileA_project_original");
        SetMeta("stylePresentationModules", "WellA_project_original|WoodpileA_project_original");
        MakeBox("BenchByFence", new(1.8f, 0.14f, 0.42f), new(4.9f, 0.7f, 6.1f), "6f543c", surface: "wood");
        MakeBox("BenchBack", new(1.8f, 0.7f, 0.12f), new(4.9f, 1.0f, 6.27f), "604a37", surface: "wood", collision: false);
        MakeFence(-4.4f, -2, 18);
        MakeFence(4.4f, -8, 16);
        MakeUtilityPole(new(5.8f, 0, 5.5f));
        MakeUtilityPole(new(-5.9f, 0, -13.5f));
        PainterlyEnvironmentDetails.AddCable(this, new Vector3(5.6f, 5.18f, 5.5f), new Vector3(-6.1f, 5.18f, -13.5f));
        PainterlyEnvironmentDetails.AddCable(this, new Vector3(6.0f, 5.18f, 5.5f), new Vector3(-5.7f, 5.18f, -13.5f));

        for (var index = 0; index < 14; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            var x = side * (12.5f + (index % 3) * 1.7f);
            var z = -19 + index * 2.9f;
            MakePine(new(x, 0, z), 4.2f + (index % 4) * 0.45f);
        }

        for (var index = 0; index < 15; index++)
        {
            MakePine(new(-18 + index * 2.55f, 0, -20.5f - index % 3 * 0.8f), 5.2f + index % 4 * 0.45f);
        }

        for (var index = 0; index < 6; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            PainterlyEnvironmentDetails.AddBirch(this, new Vector3(side * (8.5f + index), 0, -14 + index * 4.2f), 5.1f + index % 2 * 0.6f);
        }

        for (var index = 0; index < 24; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            MakeGrassTuft(new(side * (3.3f + index % 4 * 0.42f), 0.08f, 12 - index * 1.35f), 0.36f + index % 3 * 0.08f);
        }

        for (var index = 0; index < 14; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            AddShrub(new Vector3(side * (5.2f + index % 3 * 1.15f), 0, 9.5f - index * 2.35f), 0.72f + index % 3 * 0.12f);
        }

        // A second, deliberately small dressing layer makes the street read as
        // a lived-in village rather than a road through a primitive showcase.
        // These nodes are presentation-only and remain outside the interaction
        // graph and physics ownership.
        MakeHouse(new(9.8f, 0, -22.5f), "6f6859", "3f403b", warmWindow: false);
        MakeFence(-8.8f, 7.5f, 12);
        MakeFence(8.7f, 1.5f, 10);
        MakeClothesline(new(-6.0f, 0, 0.7f), new(-6.0f, 3.2f, -4.5f));
        MakeCrateStack(new(5.8f, 0.0f, 5.2f), 3);
        MakeHaySheaf(new(-5.7f, 0.0f, 8.4f), 1.15f);
        MakeSignpost(new(-2.05f, 0.0f, 5.8f));
        AttachAct1Npc(
            "alsu",
            "Alsu",
            "village guide",
            new(0.85f, 0, 2.05f),
            yawDegrees: 168f);
        MakeInteractionBox(
            "AlsuNpc",
            new(0.62f, 1.68f, 0.46f),
            new(0.85f, 0.84f, 2.05f),
            "6c655b",
            "urman.chapter1:interaction/talk-alsu",
            "Поговорить с Алсу",
            dialogueId: "urman.chapter1:dialogue/alsu_route_context");
        for (var index = 0; index < 10; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            AddShrub(new Vector3(side * (3.9f + index % 4 * 0.48f), 0, 10.5f - index * 2.2f), 0.48f + index % 3 * 0.1f, index % 3 == 0 ? "48553f" : "596047");
        }

        MakeInteractionBox(
            "VillageSign",
            new(1.9f, 1.1f, 0.16f),
            new(-2.1f, 1.25f, 3.2f),
            "80715d",
            "urman.chapter1:interaction/village-sign",
            "Прочитать выцветшую табличку");
        MakeInteractionBox(
            "RinatNpc",
            new(0.65f, 1.75f, 0.48f),
            new(-1.5f, 0.88f, -3.8f),
            "4d5551",
            "urman.chapter1:interaction/talk-rinat",
            "Поговорить с Ринатом",
            dialogueId: "urman.chapter1:dialogue/rinat_no_key");

        var houseDoor = MakeInteractionBox(
            "HouseDoor",
            AgentBAct1Layout.HouseDoorProxySize,
            // Align the interaction body to the actual portal in the authored
            // facade; the interior entry transform remains a separate spawn.
            AgentBAct1Layout.HouseDoorPortalCenter,
            "6d5844",
            "urman.chapter1:interaction/arrival-enter-house",
            "Войти в дом бабая и әби",
            "house_old_pc",
            "entry");
        houseDoor.RotationDegrees = new(0f, AgentBAct1Layout.HouseDoorYawDegrees, 0f);
        houseDoor.WorldFoleySample = "door_creak";
        var returnToHouseRegister = MakeInteractionBox(
            "ReturnToHouseRegister",
            AgentBAct1Layout.HouseDoorProxySize,
            AgentBAct1Layout.HouseDoorPortalCenter,
            "6d5844",
            "urman.chapter1:interaction/official-to-internal-register",
            "Войти и сверить справку с реестром на старом ПК",
            "house_old_pc",
            "entry");
        returnToHouseRegister.RotationDegrees =
            new(0f, AgentBAct1Layout.HouseDoorYawDegrees, 0f);
        returnToHouseRegister.WorldFoleySample = "door_creak";
        MakeInteractionBox(
            "RoadToFap",
            new(2.1f, 1.5f, 0.3f),
            new(3.8f, 0.75f, -8.2f),
            "7b6f5d",
            "urman.chapter1:interaction/route-to-fap",
            "Пойти к фельдшерскому пункту",
            "fap_clinic",
            "waiting_room");
    }

    private void BuildHouseOldPc()
    {
        MakeBox("Floor", new(12, 0.2f, 10), new(0, -0.1f, 0), "57483b", surface: "wood");
        // Collision for the rear wall's two real openings. The visible shell is
        // the authored HouseInterior_ GLB, whose rear wall carries the same two
        // rectangles (width 1.06, sill 0.99, head 2.51) matching the exterior's
        // two rear windows; the curtains hung on what used to be a sealed wall.
        MakeBox("BackWallLeft", new(1.87f, 3.4f, 0.25f), new(-5.065f, 1.7f, -5), "827461", surface: "wallpaper");
        MakeBox("BackWallMid", new(5.09f, 3.4f, 0.25f), new(-0.525f, 1.7f, -5), "827461", surface: "wallpaper");
        MakeBox("BackWallRight", new(2.92f, 3.4f, 0.25f), new(4.54f, 1.7f, -5), "827461", surface: "wallpaper");
        foreach (var (wallName, centreX) in new (string, float)[] { ("West", -3.60f), ("East", 2.55f) })
        {
            MakeBox($"BackWindow{wallName}UnderSill", new(1.06f, 0.99f, 0.25f), new(centreX, 0.495f, -5), "827461", surface: "wallpaper");
            MakeBox($"BackWindow{wallName}Head", new(1.06f, 0.89f, 0.25f), new(centreX, 2.955f, -5), "827461", surface: "wallpaper");
        }
        MakeBox("LeftWall", new(0.25f, 3.4f, 10), new(-6, 1.7f, 0), "786b5a", surface: "wallpaper");
        MakeBox("RightWall", new(0.25f, 3.4f, 10), new(6, 1.7f, 0), "786b5a", surface: "wallpaper");
        MakeBox("FrontWallLeft", new(5.25f, 3.4f, 0.25f), new(-3.375f, 1.7f, 5), "827461", surface: "wallpaper");
        MakeBox("FrontWallRight", new(5.25f, 3.4f, 0.25f), new(3.375f, 1.7f, 5), "827461", surface: "wallpaper");
        MakeBox("FrontWallLintel", new(1.5f, 1.15f, 0.25f), new(0, 2.825f, 5), "827461", surface: "wallpaper");
        MakeBox("HouseExitDoorPanel", new(1.32f, 2.12f, 0.08f), new(0, 1.06f, 4.84f), "4b382c", collision: false, surface: "wood");
        MakeBox("HouseExitDoorFrameLeft", new(0.12f, 2.35f, 0.12f), new(-0.8f, 1.175f, 4.77f), "5d4a38", collision: false, surface: "wood");
        MakeBox("HouseExitDoorFrameRight", new(0.12f, 2.35f, 0.12f), new(0.8f, 1.175f, 4.77f), "5d4a38", collision: false, surface: "wood");
        MakeBox("HouseExitDoorFrameTop", new(1.72f, 0.12f, 0.12f), new(0, 2.35f, 4.77f), "5d4a38", collision: false, surface: "wood");
        MakeBox("HouseExitDoorThreshold", new(1.72f, 0.08f, 0.42f), new(0, 0.04f, 4.68f), "574636", collision: false, surface: "wood");
        // Reverse-view threshold dressing is presentation-only; the clear
        // doorway remains owned by HouseExit and its interaction proxy.
        MakeBox("HouseThresholdCoatRail", new(1.55f, 0.08f, 0.10f), new(-2.35f, 2.18f, 4.80f), "5d4a38", collision: false, surface: "wood");
        MakeBox("HouseThresholdCoatPegLeft", new(0.08f, 0.28f, 0.08f), new(-2.86f, 2.00f, 4.79f), "5d4a38", collision: false, surface: "wood");
        MakeBox("HouseThresholdCoatPegRight", new(0.08f, 0.28f, 0.08f), new(-1.84f, 2.00f, 4.79f), "5d4a38", collision: false, surface: "wood");
        MakeRotatedBox("HouseThresholdMutedCoat", new(0.58f, 1.18f, 0.05f), new(-2.42f, 1.39f, 4.77f), new(0, 0, -3), "58656a", "fabric");
        MakeBox("HouseThresholdShoeChest", new(1.55f, 0.52f, 0.52f), new(2.42f, 0.30f, 4.48f), "624936", collision: false, surface: "wood");
        MakeBox("HouseThresholdShoeChestLid", new(1.70f, 0.08f, 0.58f), new(2.42f, 0.59f, 4.48f), "765842", collision: false, surface: "wood");
        MakeBox("HouseThresholdShoeChestPanel", new(1.18f, 0.24f, 0.025f), new(2.42f, 0.30f, 4.20f), "493629", collision: false, surface: "wood");
        MakeRotatedBox("HouseThresholdWornWallPatch", new(0.90f, 0.36f, 0.025f), new(2.45f, 2.58f, 4.80f), new(0, 0, -5), "6d5d4f", "plaster");
        MakeBox("Ceiling", new(12, 0.18f, 10), new(0, 3.4f, 0), "695746", surface: "wood");
        MakeBox("CeilingBeamLeft", new(0.22f, 0.28f, 10), new(-3.25f, 3.22f, 0), "493629", surface: "wood");
        MakeBox("CeilingBeamRight", new(0.22f, 0.28f, 10), new(3.25f, 3.22f, 0), "493629", surface: "wood");
        // I2: the stove burns wood, so a small indoor stock sits within reach of
        // the hearth door, and the wash towel hangs on the wall beside it, where
        // the basin chest stands - instead of at the entry rail with the coats.
        // Dressing only: the hearth corner is off the walk line to the PC.
        MakeBox("HearthFirewoodLogBottom", new(0.55f, 0.13f, 0.30f), new(-4.95f, 0.065f, 1.42f), "8a6b50", collision: false, surface: "wood");
        MakeBox("HearthFirewoodLogTop", new(0.48f, 0.12f, 0.27f), new(-4.97f, 0.19f, 1.44f), "9a7a55", collision: false, surface: "wood");
        MakeRotatedBox("HouseWashTowel", new(0.04f, 0.52f, 0.28f), new(-5.76f, 1.52f, 1.80f), new(0, 0, 2), "b3ac9d", "fabric");
        MakeBox("Table", new(3.2f, 0.14f, 1.35f), new(0, 0.82f, -3.6f), "57402e", surface: "wood");
        MakeBox("TableLegL", new(0.18f, 0.82f, 0.18f), new(-1.35f, 0.4f, -3.6f), "463326", surface: "wood");
        MakeBox("TableLegR", new(0.18f, 0.82f, 0.18f), new(1.35f, 0.4f, -3.6f), "463326", surface: "wood");
        MakeDisc("Mouse", new(0.09f, 0.035f, 0.13f), new(0.46f, 0.925f, -3.22f), "6f6d61");
        MakeBox("DocumentStack", new(0.36f, 0.035f, 0.46f), new(-1.05f, 0.915f, -3.30f), "b0a17e", collision: false);
        MakeBox("DocumentShadow", new(0.38f, 0.015f, 0.48f), new(-1.03f, 0.899f, -3.32f), "5f5140", collision: false);
        MakeRotatedBox("DocumentTopPage", new(0.30f, 0.008f, 0.40f), new(-0.98f, 0.941f, -3.29f), new(0, -4, 1), "d3c39d");
        MakeBox("DocumentRedMark", new(0.18f, 0.009f, 0.01f), new(-0.98f, 0.947f, -3.37f), "9b5a4c", collision: false);
        MakeCup(new(-1.34f, 0.94f, -3.62f));
        MakeRotatedBox("ChairSeat", new(0.9f, 0.12f, 0.86f), new(-2.15f, 0.52f, -1.25f), new(0, -18, 0), "684b37", "wood");
        MakeRotatedBox("ChairBack", new(0.9f, 1.05f, 0.12f), new(-2.02f, 1.0f, -0.83f), new(0, -18, 0), "684b37", "wood");
        MakeBox("Cupboard", new(1.45f, 2.35f, 0.72f), new(-4.85f, 1.17f, -3.9f), "70563e", surface: "wood");
        MakeBox("CupboardDoorLine", new(0.06f, 2.08f, 0.05f), new(-4.85f, 1.2f, -3.51f), "3f3026", collision: false);
        MakeDisc("CupboardKnobLeft", new(0.055f, 0.045f, 0.055f), new(-5.02f, 1.25f, -3.46f), "c39b59");
        MakeDisc("CupboardKnobRight", new(0.055f, 0.045f, 0.055f), new(-4.68f, 1.25f, -3.46f), "c39b59");
        MakeBox("WovenRug", new(3.8f, 0.025f, 2.25f), new(-1.25f, 0.025f, 0.4f), "714939", collision: false, surface: "carpet");
        MakeBox("RugStripeA", new(3.8f, 0.032f, 0.16f), new(-1.25f, 0.04f, 0.05f), "8d765c", collision: false, surface: "carpet");
        MakeBox("RugStripeB", new(3.8f, 0.032f, 0.16f), new(-1.25f, 0.04f, 0.72f), "45615a", collision: false, surface: "carpet");
        MakeBox("WallShelf", new(2.2f, 0.12f, 0.45f), new(-3.85f, 2.05f, -4.6f), "503b2c", surface: "wood");
        MakeBox("CurtainLeft", new(0.42f, 1.55f, 0.05f), new(2.05f, 2.1f, -4.78f), "8d806f", collision: false, surface: "fabric_pattern");
        MakeBox("CurtainRight", new(0.42f, 1.55f, 0.05f), new(3.05f, 2.1f, -4.78f), "8d806f", collision: false, surface: "fabric_pattern");
        var oldPcAnchor = new Vector3(0, 1.15f, -3.68f);
        MakeInteractionBox(
            "OldPc",
            new(0.94f, 0.66f, 0.50f),
            oldPcAnchor + new Vector3(.14f, 0, 0),
            "3b403c",
            "urman.chapter1:interaction/oldpc-power",
            "Включить старый компьютер");
        var oldPc = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-house-old-pc",
            ["OldPc_"],
            oldPcAnchor,
            uniformScale: 0.45f,
            yawDegrees: 0f);
        oldPc.Name = "GeneratedOldPcAct1";
        oldPc.SetMeta("stylePresentationModule", "OldPc_project_original");
        SetMeta("styleImportedModules", "OldPc_project_original");

        var houseInterior = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-house-interior",
            ["HouseInterior_"],
            Vector3.Zero,
            uniformScale: 1.0f,
            yawDegrees: 0f);
        houseInterior.Name = "GeneratedHouseInteriorAct1";
        // AttachPresentationOnly aligns its first selected mesh to the anchor.
        // This GLB is already authored in room-local coordinates, so keep the
        // returned presentation instance at the room origin while preserving
        // its imported scale and yaw.
        houseInterior.Position = Vector3.Zero;
        houseInterior.SetMeta("presentationOnly", true);
        houseInterior.SetMeta("visualOnly", true);
        houseInterior.SetMeta("presentationSource", GeneratedModularKitDressing.ScenePath);
        houseInterior.SetMeta("collisionOwner", "none");
        houseInterior.SetMeta("navigationOwner", "none");
        houseInterior.SetMeta("interactionOwner", "none");
        houseInterior.SetMeta("narrativeOwner", "none");
        houseInterior.SetMeta("rebasedLocalTransform", "room-local GLB origin restored after AttachPresentationOnly anchor alignment");
        houseInterior.SetMeta("stylePresentationModule", "HouseInterior_project_original");

        var collapsedHouseFloorBoards = 0;
        var hiddenHouseFloorBoards = 0;
        var removedHouseRugBands = 0;
        foreach (var mesh in Descendants(houseInterior).OfType<MeshInstance3D>())
        {
            var meshName = mesh.Name.ToString();
            if (meshName.StartsWith("HouseInterior_FloorBoard_", StringComparison.Ordinal))
            {
                var keepAsContinuousFloor = meshName is "HouseInterior_FloorBoard_02_LOD0" or "HouseInterior_FloorBoard_02_LOD1";
                if (keepAsContinuousFloor)
                {
                    // Keep one authored board as a continuous warm floor field;
                    // the other board seams read as a debug-like repeated strip
                    // from the first-person entry view.
                    mesh.Position = new Vector3(0f, mesh.Position.Y, mesh.Position.Z);
                    mesh.Scale = new Vector3(mesh.Scale.X * 6f, mesh.Scale.Y, mesh.Scale.Z);
                    mesh.SetMeta("presentationAdjustment", "single authored floor board widened to continuous field");
                    collapsedHouseFloorBoards++;
                }
                else
                {
                    mesh.Visible = false;
                    hiddenHouseFloorBoards++;
                }
            }
            else if (meshName.StartsWith("HouseInterior_RugBand", StringComparison.Ordinal))
            {
                // The broad authored rug field remains; these two long bands
                // are the crossing strip motif that dominates the entry floor.
                mesh.Visible = false;
                removedHouseRugBands++;
            }
        }
        houseInterior.SetMeta("collapsedFloorBoardCount", collapsedHouseFloorBoards);
        houseInterior.SetMeta("hiddenFloorBoardCount", hiddenHouseFloorBoards);
        houseInterior.SetMeta("removedRugBandCount", removedHouseRugBands);
        houseInterior.SetMeta("floorPresentationAdjustment", "authored HouseInterior floor seams collapsed to one continuous field; broad rug retained without crossing bands");

        // Recompose only the authored furniture silhouettes that disappear
        // behind the first-person camera on the reverse turn. These meshes
        // have no physics descendants; the gameplay proxies below remain at
        // their existing authored anchors.
        var movedHouseFurnitureMeshCount = 0;
        var scaledHouseFurnitureMeshCount = 0;
        foreach (var mesh in Descendants(houseInterior).OfType<MeshInstance3D>())
        {
            var meshName = mesh.Name.ToString();
            var offset = Vector3.Zero;
            var yawDegrees = 0f;
            var scale = 1f;
            if (meshName.StartsWith("HouseInterior_LeftWallCupboard", StringComparison.Ordinal))
            {
                // Rehang the small cupboard on the front-left wall so the
                // reverse turn has a grounded high/mid silhouette above the
                // existing threshold chest.
                offset = new Vector3(0.85f, 0f, 1.90f);
                yawDegrees = 90f;
                scale = 1.05f;
            }
            else if (meshName.StartsWith("HouseInterior_StorageChest", StringComparison.Ordinal))
            {
                offset = new Vector3(-7.95f, 0f, 7.90f);
                scale = 1.08f;
            }
            else if (meshName.StartsWith("HouseInterior_StorageBasket", StringComparison.Ordinal))
            {
                offset = new Vector3(0f, 0f, 4.00f);
                scale = 1.06f;
            }

            if (offset != Vector3.Zero)
            {
                mesh.Position += offset;
                movedHouseFurnitureMeshCount++;
            }

            if (yawDegrees != 0f)
            {
                mesh.RotationDegrees += new Vector3(0f, yawDegrees, 0f);
            }

            if (scale != 1f)
            {
                mesh.Scale *= scale;
                scaledHouseFurnitureMeshCount++;
            }
        }
        houseInterior.SetMeta("movedHouseFurnitureMeshCount", movedHouseFurnitureMeshCount);
        houseInterior.SetMeta("scaledHouseFurnitureMeshCount", scaledHouseFurnitureMeshCount);
        houseInterior.SetMeta(
            "houseInteriorCompositionPass",
            "authored front-left cupboard/chest; right-side daybed/shelf/runner brought forward; storage basket retained as near anchor; collision proxies unchanged");

        var replacedHouseVisuals = new[]
        {
            "Floor",
            "BackWall",
            "LeftWall",
            "RightWall",
            "FrontWallLeft",
            "FrontWallRight",
            "FrontWallLintel",
            "HouseExitDoorPanel",
            "HouseExitDoorFrameLeft",
            "HouseExitDoorFrameRight",
            "HouseExitDoorFrameTop",
            "HouseExitDoorThreshold",
            "Ceiling",
            "CeilingBeamLeft",
            "CeilingBeamRight",
            "Table",
            "TableLegL",
            "TableLegR",
            "ChairSeat",
            "ChairBack",
            "Cupboard",
            "CupboardDoorLine",
            "WovenRug",
            "RugStripeA",
            "RugStripeB",
            "WallShelf",
            "CurtainLeft",
            "CurtainRight"
        };
        foreach (var legacyVisualName in replacedHouseVisuals)
        {
            if (GetNodeOrNull<Node3D>(legacyVisualName) is { } legacyVisual)
            {
                SuppressPrimitiveVisual(
                    legacyVisual,
                    "authored HouseInterior_ GLB replaces the legacy primitive visual; collision remains unchanged");
            }
        }

        SetMeta("houseInteriorPresentation", "HouseInterior_ project-original GLB; presentation-only; legacy shell and furniture visuals suppressed");
        SetMeta("houseInteriorLegacyVisualsSuppressed", string.Join("|", replacedHouseVisuals));
        var daybedCollision = MakeCollisionBox(
            "HouseInteriorDaybedCollision",
            new(1.36f, 0.78f, 2.95f),
            new(4.78f, 0.39f, 1.25f));
        daybedCollision.SetMeta("collisionOwner", "house-interior-floor-furniture");
        var storageChestCollision = MakeCollisionBox(
            "HouseInteriorStorageChestCollision",
            new(1.98f, .86f, 1.07f),
            new(-3.97f, .43f, 3.70f));
        storageChestCollision.SetMeta("collisionOwner", "house-interior-floor-furniture");
        var hearthCollision = MakeCollisionBox(
            "HouseInteriorHearthCollision",
            new(1.10f, 1.04f, 0.90f),
            new(-5.00f, 0.64f, 0.55f));
        hearthCollision.SetMeta("collisionOwner", "house-interior-floor-furniture");
        SetMeta(
            "houseInteriorFloorCollisionProxies",
            "HouseInteriorDaybedCollision(1.36x0.78x2.95)@(4.78,0.39,1.25)|HouseInteriorStorageChestCollision(1.98x0.86x1.07)@(-3.97,0.43,3.70)|HouseInteriorHearthCollision(1.10x1.04x0.90)@(-5.00,0.64,0.55)");
        SetMeta(
            "houseInteriorLivedInCluster",
            "authored hearth with flue|left-wall cupboard|quiet tableware|floor storage basket; presentation-only GLB with hearth floor proxy");
        PainterlyEnvironmentDetails.AddCable(this, new Vector3(.60f, .94f, -3.78f), new Vector3(.60f, .10f, -2.8f));
        MakeCylinder("LampStem", .035f, .64f, new(2.15f, 1.32f, -3.25f), "6c573e");
        var lampShade = new MeshInstance3D
        {
            Name = "LampShade",
            Position = new(2.15f, 1.74f, -3.25f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.18f,
                BottomRadius = 0.28f,
                Height = 0.28f,
                RadialSegments = 8,
                CapTop = false,
                CapBottom = false
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Color.FromHtml("b98a53"),
                AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/painterly/old_fabric_v3_albedo.png"),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                Roughness = 0.96f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = Color.FromHtml("bd9766"),
                EmissionEnergyMultiplier = .25f
            }
        };
        lampShade.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(lampShade);
        MakeBox("FamilyPhoto", new(0.58f, 0.72f, 0.06f), new(-2.1f, 1.45f, -4.82f), "8a7358", collision: false);
        MakeBox("FamilyPhotoInner", new(0.42f, 0.55f, 0.025f), new(-2.1f, 1.45f, -4.77f), "b7a17c", collision: false);
        MakeBox("WallTextile", new(1.45f, 0.92f, 0.04f), new(4.25f, 1.85f, -4.78f), "69483f", collision: false);
        MakeBox("WallTextileStripe", new(1.15f, 0.12f, 0.025f), new(4.25f, 1.85f, -4.73f), "b28a58", collision: false);
        MakeBox("WallTextileStripeLower", new(1.15f, 0.09f, 0.025f), new(4.25f, 1.55f, -4.73f), "45615a", collision: false);
        MakeRotatedBox("WallCrack", new(0.03f, 0.78f, 0.02f), new(-3.48f, 2.2f, -4.81f), new(0, 0, 18), "66574a");
        MakeRotatedBox("WallCrackBranch", new(0.025f, 0.38f, 0.02f), new(-3.28f, 2.0f, -4.81f), new(0, 0, -46), "66574a");
        MakeShelfStillLife();
        MakeWallCalendar();
        MakeRadioAndHerbs();
        AttachAct1Npc(
            "mansur",
            "Mansur",
            "family elder",
            new(2.8f, 0, -1.45f),
            yawDegrees: -24f,
            presentationHostName: "MansurPresentation");
        MakeInteractionBox(
            "MansurNpc",
            new(0.62f, 1.8f, 0.46f),
            new(2.8f, 0.9f, -1.45f),
            "5e4638",
            "urman.chapter1:interaction/talk-mansur",
            "Поговорить с бабаем Мансуром",
            dialogueId: "urman.chapter1:dialogue/mansur_pc_request");
        // Gөlsinә stands clear of the table corner: at her old spot the
        // interaction approach point landed on the table's edge and a player
        // capsule could not stand on it.
        AttachAct1Npc(
            "gulsina",
            "Gulsina",
            "family keeper",
            new(-3.2f, 0, -2.3f),
            yawDegrees: 28f);
        MakeInteractionBox(
            "GulsinaNpc",
            new(0.62f, 1.68f, 0.46f),
            new(-3.2f, 0.84f, -2.3f),
            "6f6257",
            "urman.chapter1:interaction/talk-gulsina",
            "Поговорить с әби",
            dialogueId: "urman.chapter1:dialogue/gulsina_yaramyy");
        var houseExit = MakeInteractionBox(
            "HouseExit",
            new(1.3f, 2.1f, 0.22f),
            new(0, 1.05f, 4.82f),
            "685848",
            "urman.chapter1:interaction/house-to-route",
            "Выйти на улицу",
            "village_day",
            "from_house");
        houseExit.WorldFoleySample = "door_creak";
        // Connected-world staging brings the existing Rinat actor here
        // after the FAP visit; this target owns the home conversation.
        MakeInteractionBox(
            "InternalRegisterToRinat",
            new(0.65f, 1.78f, 0.48f),
            new(4.05f, 0.89f, -2.65f),
            "4c5652",
            "urman.chapter1:interaction/internal-register-to-rinat",
            "Спросить Рината о внутреннем реестре",
            dialogueId: "urman.chapter1:dialogue/rinat_internal_register");
        MakeInteractionBox(
            "InternalRegisterToSavedMessage",
            new(1.15f, 0.72f, 0.22f),
            new(2.4f, 1.35f, -4.72f),
            "6f735f",
            "urman.chapter1:interaction/internal-register-to-saved-message",
            "Открыть сохранённое сообщение Марата",
            documentId: "urman.oldpc:document/msg_marat_saved_last_normal");
        MakeInteractionBox(
            "SavedMessageToBoundarySource",
            new(1.15f, 0.72f, 0.22f),
            new(2.4f, 1.35f, -4.72f),
            "77705b",
            "urman.chapter1:interaction/saved-message-to-boundary-source",
            "Найти статью о лесной границе",
            documentId: "urman.oldpc:document/tw_shurale_urman_boundary");
        MakeInteractionBox(
            "BoundarySourceToReread",
            new(1.15f, 0.72f, 0.22f),
            new(2.4f, 1.35f, -4.72f),
            "687467",
            "urman.chapter1:interaction/boundary-source-to-reread",
            "Перечитать статью с понятыми словами",
            documentId: "urman.oldpc:document/tw_shurale_urman_boundary");
        MakeInteractionBox(
            "RereadToEdgeSketch",
            new(1.15f, 0.72f, 0.22f),
            new(2.4f, 1.35f, -4.72f),
            "756a58",
            "urman.chapter1:interaction/reread-to-edge-sketch",
            "Сопоставить статью с рисунком Марата",
            documentId: "urman.oldpc:document/doc_kara_urman_edge_sketch");
        MakeInteractionBox(
            "EdgeSketchToZiratRoad",
            new(1.3f, 2.1f, 0.22f),
            new(0, 1.05f, 4.82f),
            "5d5648",
            "urman.chapter1:interaction/edge-sketch-to-zirat-road",
            "Идти к дороге у зирата",
            "zirat_road",
            "village_side");

        var lamp = new OmniLight3D
        {
            Name = "WarmTableLamp",
            Position = new(2.15f, 1.70f, -3.25f),
            LightColor = Color.FromHtml("c5aa8d"),
            LightEnergy = 1.45f,
            OmniRange = 4.75f,
            ShadowEnabled = true
        };
        AddChild(lamp);

        AddChild(new OmniLight3D
        {
            Name = "WindowFill",
            Position = new(-3.6f, 1.85f, -3.6f),
            LightColor = Color.FromHtml("9aaeb0"),
            LightEnergy = 0.46f,
            OmniRange = 5.5f,
            ShadowEnabled = false
        });

        AddChild(new OmniLight3D
        {
            Name = "RoomFill",
            Position = new(0, 2.5f, .5f),
            LightColor = Color.FromHtml("958878"),
            LightEnergy = 0.56f,
            OmniRange = 8.5f,
            ShadowEnabled = false
        });

        AddChild(new OmniLight3D
        {
            Name = "CrtScreenGlow",
            Position = oldPcAnchor + new Vector3(0, .04f, .24f),
            LightColor = Color.FromHtml("78a59a"),
            LightEnergy = 0.78f,
            OmniRange = 3.8f,
            ShadowEnabled = false
        });

        AddChild(new OmniLight3D
        {
            Name = "HouseDocumentTaskLight",
            Position = new(-1.0f, 1.9f, -3.25f),
            LightColor = Color.FromHtml("c2a375"),
            LightEnergy = 0.72f,
            OmniRange = 2.9f,
            ShadowEnabled = true
        });
    }

    private void BuildKaraUrmanNight()
    {
        MakeBox("Ground", new(38, 0.25f, 38), new(0, -0.125f, 0), "273126");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathNear", 3.8f, 13f, new(0f, 0f, 9f), "403c31");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathMiddle", 3.6f, 11f, new(0.45f, 0f, -2.3f), "3b392f", yawDegrees: -5f);
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathFar", 3.3f, 10f, new(-0.1f, 0f, -12.3f), "37362e", yawDegrees: 7f);

        var karaAuthoredKit = AttachAuthoredKitComponents(
            KaraForestEdgeKitScenePath,
            KaraForestEdgeKitRootName,
            KaraForestEdgeKitComponentNames,
            "KaraForestEdgeAuthoredKit",
            "authored near/mid/far forest edge framing with an open central road window",
            ("ForestBank_Left", "KaraForestBankLeft", new(-6.2f, 0f, 3.0f), -12f, Vector3.One, "kara-benchmark@west-road-bank"),
            ("ForestBank_Right", "KaraForestBankRight", new(6.6f, 0f, -2.0f), 18f, Vector3.One * 0.98f, "kara-benchmark@east-road-bank"),
            ("MixedTreeCluster_Left", "KaraMixedTreeClusterLeft", new(-10.0f, 0f, -3.5f), -18f, Vector3.One * 0.96f, "kara-benchmark@west-side-tree-mass"),
            ("MixedTreeCluster_Right", "KaraMixedTreeClusterRight", new(10.0f, 0f, -8.5f), 22f, Vector3.One * 0.94f, "kara-benchmark@east-side-tree-mass"),
            ("CrookedPineMass", "KaraCrookedPineMass", new(-11.0f, 0f, -10.0f), -14f, Vector3.One, "kara-benchmark@west-side-crooked-pine"),
            ("BirchEdgeMass", "KaraBirchEdgeMass", new(11.0f, 0f, -14.0f), 17f, Vector3.One * 0.96f, "kara-benchmark@east-side-birch-edge"),
            ("RootWall_Left", "KaraRootWallLeft", new(-5.5f, 0f, -1.5f), -12f, Vector3.One * 0.96f, "kara-benchmark@west-root-wall"),
            ("RootWall_Right", "KaraRootWallRight", new(5.5f, 0f, -6.2f), 20f, Vector3.One * 0.92f, "kara-benchmark@east-root-wall"),
            ("FallenLogCluster", "KaraFallenLogCluster", new(-5.9f, 0f, -5.8f), 28f, Vector3.One * 0.82f, "kara-benchmark@west-ground-breakup-outside-route"),
            ("MossyBoulderCluster", "KaraMossyBoulderCluster", new(5.8f, 0f, -3.8f), -20f, Vector3.One * 0.86f, "kara-benchmark@east-ground-breakup-outside-route"),
            ("CrookedStump", "KaraCrookedStump", new(-5.4f, 0f, -7.2f), 10f, Vector3.One * 0.88f, "kara-benchmark@west-ground-landmark-outside-route"),
            ("DistantForestMass_Low", "KaraDistantForestMassLow", new(-13.5f, 0f, -30.0f), -8f, Vector3.One * 0.90f, "kara-benchmark@far-west-window-mass"),
            ("DistantForestMass_Tall", "KaraDistantForestMassTall", new(13.5f, 0f, -32.0f), 11f, Vector3.One * 0.72f, "kara-benchmark@far-east-window-mass"));
        RegradeAuthoredKitMaterials(karaAuthoredKit, KaraForestEdgeKitScenePath);
        var looseBoughCount = 0;
        foreach (var mesh in Descendants(karaAuthoredKit).OfType<MeshInstance3D>())
        {
            var name = mesh.Name.ToString();
            if (!name.StartsWith("MixedTreeCluster_Left_AngledBough_", StringComparison.Ordinal)
                && !name.StartsWith("MixedTreeCluster_Right_AngledBough_", StringComparison.Ordinal)
                && !name.StartsWith("CrookedPineMass_SideBough_", StringComparison.Ordinal)
                && !name.StartsWith("BirchEdgeMass_FineBranch_", StringComparison.Ordinal))
            {
                continue;
            }

            mesh.Hide();
            looseBoughCount++;
        }
        karaAuthoredKit.SetMeta("suppressedLooseBoughCount", looseBoughCount);

        // The imported pines are deliberately offset from the repeated
        // authored forest kit: one keeps the existing project-original style
        // module while the two scaled/rotated variants add a readable return
        // frame. They remain visual-only, so walkability and interaction-layer
        // ownership stay unchanged.
        GeneratedModularKitDressing.Attach(
            this,
            "style-forest-pine",
            ["PineA_"],
            new(8.6f, 1.6f, -14.6f));
        AttachPresentationPine("GeneratedPineA_MidLeft", new(-8.2f, 1.44f, -5.8f), 0.90f, -18f);
        AttachPresentationPine("GeneratedPineA_NearRight", new(8.8f, 1.76f, 3.0f), 1.10f, 27f);
        SetMeta("styleImportedModules", "PineA_project_original");
        SetMeta("styleImportedPineInstances", 3);

        // Break the repeated crown rhythm with ground-level material cues and
        // a restrained boundary motif. The forest should feel observed, not
        // decorated as a monster arena.
        MakeDisc("BoundaryStoneNear", new(0.9f, 0.16f, 0.72f), new(-2.15f, 0.08f, 4.75f), "656860", surface: "stone");
        // The owned PathNear relief dips beneath the boundary patch offsets;
        // seat the cluster against that authored surface rather than the
        // higher relief from another benchmark scene.
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "BoundaryWetPatch", new(0.72f, 0.078f, 4.25f), new(0.68f, 1.18f), "4a585b", [-0.005065963f, -0.007065952f, -0.009065956f]);
        MakeBoundaryCharm(new(-1.9f, 0.0f, -7.6f), new(0.73f, 0.45f, 0.14f));
        MakeBoundaryCharm(new(2.05f, 0.0f, -7.4f), new(0.78f, 0.56f, 0.18f));
        MakeBranch(new(-4.4f, 3.2f, -5.2f), new(-2.0f, 2.5f, -7.0f), "40352f");
        MakeBranch(new(4.2f, 3.0f, -4.1f), new(2.2f, 2.25f, -6.4f), "3a302c");

        MakeFence(-3.7f, -5.5f, 11);
        MakeBox("BoundaryPostLeft", new(0.16f, 1.55f, 0.16f), new(-1.6f, 0.78f, -7.7f), "786d58", surface: "wood");
        MakeBox("BoundaryPostRight", new(0.16f, 1.35f, 0.16f), new(1.6f, 0.68f, -7.7f), "786d58", surface: "wood");
        MakeBox("BoundaryThread", new(3.2f, 0.035f, 0.035f), new(0, 1.32f, -7.7f), "b7a07c", collision: false);
        MakeBox("BoundaryRibbonLeft", new(0.08f, 0.52f, 0.025f), new(-3.54f, 1.18f, -7.3f), "c8b895", collision: false);
        MakeBox("BoundaryRibbonRight", new(0.08f, 0.42f, 0.025f), new(3.58f, 1.08f, -8.4f), "c0aa89", collision: false);
        MakeBox("DistantWindow", new(0.72f, 0.48f, 0.06f), new(-5.4f, 1.8f, 10.4f), "d69a56", collision: false);

        // The boundary marker is presentation-only. An InteractionTarget here
        // used to reference an ID absent from the compiled campaign, which
        // made a missing-authored interaction look like valid progression.
        MakeBox(
            "BoundaryMarker",
            new(0.75f, 1.65f, 0.45f),
            new(1.35f, 0.82f, -7.5f),
            "665d4d",
            collision: false);
        MakeInteractionBox(
            "Act2Continuation",
            new(1.1f, 1.6f, 0.4f),
            new(-1.35f, 0.8f, -7.35f),
            "4d514a",
            "urman.fullgame:interaction/chapter1-forest-to-act2-house",
            "Продолжить к дому после границы",
            targetZoneId: "fullgame_act2_house",
            targetSpawnPointId: "entry");

        var lamp = new OmniLight3D
        {
            Name = "DistantWarmWindow",
            Position = new(-5.5f, 2.2f, 10.5f),
            LightColor = Color.FromHtml("c79d79"),
            LightEnergy = 2.1f,
            OmniRange = 5.5f,
            ShadowEnabled = false
        };
        AddChild(lamp);

        AddChild(new OmniLight3D
        {
            Name = "MoonFill",
            Position = new(0, 3.0f, 6.0f),
            LightColor = Color.FromHtml("8198a0"),
            LightEnergy = 1.10f,
            OmniRange = 14.0f,
            ShadowEnabled = false
        });

        SetMeta("styleAuthoredModules", "KaraForestEdgeKit_wave4");
        SetMeta("styleAuthoredMeshPolicy", "authored forest banks, roots, mixed trees, ground breakup and far masses; no repeated procedural cone rows");
        SetMeta("styleAuthoredReview", "candidate geometry only; 360-degree first-person and cultural/local review remain open");
    }

    private void BuildFapClinic()
    {
        MakeBox("Floor", new(12, 0.2f, 12), new(0, -0.1f, 0), "5b625d", surface: "wood");
        MakeBox("BackWall", new(12, 3.4f, 0.25f), new(0, 1.7f, -6), "7e837e", surface: "wall_institution");
        MakeBox("FrontWall", new(12, 3.4f, 0.25f), new(0, 1.7f, 6), "747d78", surface: "wall_institution");
        MakeBox("LeftWall", new(0.25f, 3.4f, 12), new(-6, 1.7f, 0), "7b8580", surface: "wall_institution");
        MakeBox("RightWall", new(0.25f, 3.4f, 12), new(6, 1.7f, 0), "77807a", surface: "wall_institution");
        MakeBox("ClinicCeiling", new(12, 0.18f, 12), new(0, 3.4f, 0), "7b8580", collision: false, surface: "plaster");
        MakeBox("ClinicCeilingFixtureHousing", new(1.35f, 0.12f, 0.52f), new(0, 3.22f, -0.5f), "65736d", collision: false);
        MakeBox("ClinicCeilingFixtureLens", new(0.88f, 0.025f, 0.22f), new(0, 3.145f, -0.5f), "9ca9a2", collision: false);
        // The clinic ceiling is a large plain plaster plane; two shallow ribs and
        // a second diffuser at the far end read as a built-in institutional
        // ceiling instead of an empty band above the room.
        MakeBox("ClinicCeilingRibNear", new(12, 0.09f, 0.26f), new(0, 3.30f, -3.9f), "6f7a75", collision: false, surface: "plaster");
        MakeBox("ClinicCeilingRibFar", new(12, 0.09f, 0.26f), new(0, 3.30f, 3.9f), "6f7a75", collision: false, surface: "plaster");
        MakeBox("ClinicCeilingFixtureHousingFar", new(1.35f, 0.12f, 0.52f), new(0, 3.22f, 2.4f), "65736d", collision: false);
        MakeBox("ClinicCeilingFixtureLensFar", new(0.88f, 0.025f, 0.22f), new(0, 3.145f, 2.4f), "9ca9a2", collision: false);
        AttachFapInteriorSet();

        MakeBox("ClinicDoorPanel", new(1.12f, 2.05f, 0.08f), new(0, 1.05f, 5.84f), "687871", collision: false);
        MakeBox("ClinicDoorFrameLeft", new(0.12f, 2.3f, 0.12f), new(-0.68f, 1.16f, 5.77f), "596860", collision: false);
        MakeBox("ClinicDoorFrameRight", new(0.12f, 2.3f, 0.12f), new(0.68f, 1.16f, 5.77f), "596860", collision: false);
        MakeBox("ClinicDoorFrameTop", new(1.48f, 0.12f, 0.12f), new(0, 2.3f, 5.77f), "596860", collision: false);
        MakeBox("ClinicDoorThreshold", new(1.45f, 0.08f, 0.46f), new(0, 0.04f, 5.55f), "65726a", collision: false);

        MakeBox("ClinicLinoleumField", new(11.55f, 0.025f, 11.55f), new(0, 0.015f, 0), "5d6a63", collision: false);
        for (var index = 0; index < 4; index++)
        {
            MakeBox(
                $"ClinicLinoleumSeam{index}",
                new(0.018f, 0.012f, 11.2f),
                new(-4.2f + index * 2.8f, 0.036f, 0),
                "4c5a53",
                collision: false);
        }

        MakeBox("ClinicWindowLeftFrosted", new(0.05f, 1.15f, 1.7f), new(-5.84f, 1.95f, 1.25f), "a3b0a5", collision: false);
        MakeBox("ClinicWindowLeftTrimTop", new(0.1f, 0.1f, 1.92f), new(-5.78f, 2.58f, 1.25f), "68776d", collision: false);
        MakeBox("ClinicWindowLeftTrimBottom", new(0.1f, 0.1f, 1.92f), new(-5.78f, 1.32f, 1.25f), "68776d", collision: false);
        MakeBox("ClinicWindowLeftTrimFront", new(0.1f, 1.35f, 0.1f), new(-5.78f, 1.95f, 0.34f), "68776d", collision: false);
        MakeBox("ClinicWindowLeftTrimBack", new(0.1f, 1.35f, 0.1f), new(-5.78f, 1.95f, 2.16f), "68776d", collision: false);
        MakeBox("ClinicWindowRightFrosted", new(0.05f, 1.15f, 1.7f), new(5.84f, 1.95f, 1.25f), "a3b0a5", collision: false);
        MakeBox("ClinicWindowRightTrimTop", new(0.1f, 0.1f, 1.92f), new(5.78f, 2.58f, 1.25f), "68776d", collision: false);
        MakeBox("ClinicWindowRightTrimBottom", new(0.1f, 0.1f, 1.92f), new(5.78f, 1.32f, 1.25f), "68776d", collision: false);
        MakeBox("ClinicWindowRightTrimFront", new(0.1f, 1.35f, 0.1f), new(5.78f, 1.95f, 0.34f), "68776d", collision: false);
        MakeBox("ClinicWindowRightTrimBack", new(0.1f, 1.35f, 0.1f), new(5.78f, 1.95f, 2.16f), "68776d", collision: false);

        MakeBox("ClinicRecordsBoard", new(2.15f, 1.12f, 0.06f), new(-3.35f, 2.16f, -5.83f), "75817a", collision: false);
        MakeBox("ClinicRecordsBoardFrameTop", new(2.34f, 0.08f, 0.08f), new(-3.35f, 2.75f, -5.77f), "596960", collision: false);
        MakeBox("ClinicRecordsBoardFrameBottom", new(2.34f, 0.08f, 0.08f), new(-3.35f, 1.57f, -5.77f), "596960", collision: false);
        MakeBox("ClinicRecordsBoardCardA", new(0.42f, 0.34f, 0.018f), new(-3.95f, 2.28f, -5.78f), "adb5a7", collision: false);
        MakeBox("ClinicRecordsBoardCardB", new(0.42f, 0.34f, 0.018f), new(-3.35f, 1.86f, -5.78f), "adb5a7", collision: false);
        MakeBox("ClinicCalendarFrame", new(1.12f, 1.42f, 0.06f), new(2.75f, 2.0f, -5.83f), "596960", collision: false);
        MakeBox("ClinicCalendarPage", new(0.92f, 1.2f, 0.018f), new(2.75f, 2.0f, -5.78f), "b1b9ae", collision: false);
        for (var row = 0; row < 3; row++)
        {
            MakeBox($"ClinicCalendarLine{row}", new(0.72f, 0.018f, 0.012f), new(2.75f, 2.28f - row * 0.22f, -5.765f), "7c897f", collision: false);
        }

        // Preserve the legacy bench collision footprints while replacing only
        // their visible BoxMesh seats with the authored waiting bench.
        SuppressPrimitiveVisual(
            MakeBox("WaitingBenchLeft", new(2.8f, 0.16f, 0.7f), new(-3.6f, 0.58f, 1.8f), "6b5b4a"),
            "authored FapInteriorSet waiting bench replaces the legacy visual seat; collision remains unchanged");
        SuppressPrimitiveVisual(
            MakeBox("WaitingBenchRight", new(2.8f, 0.16f, 0.7f), new(3.6f, 0.58f, 1.8f), "6b5b4a"),
            "authored FapInteriorSet waiting bench replaces the legacy visual seat; collision remains unchanged");

        MakeBox("ClinicDeskRecordStack", new(0.76f, 0.1f, 0.88f), new(-0.78f, 0.95f, -4.2f), "b89868", collision: false);
        MakeRotatedBox("ClinicDeskRecordTopPage", new(0.6f, 0.02f, 0.72f), new(-0.7f, 1.02f, -4.2f), new(0, -4, 1), "d0bd91");

        MakeBox("ClinicRadiator", new(1.45f, 0.62f, 0.16f), new(5.72f, 0.48f, 1.25f), "68766f", collision: false);
        for (var index = 0; index < 2; index++)
        {
            MakeCylinder($"ClinicRadiatorPipe{index}", 0.035f, 1.32f, new(5.45f + index * 0.5f, 1.08f, 1.25f), "596860");
        }

        MakeBox("DocumentDesk", new(3.6f, 0.16f, 1.5f), new(0, 0.82f, -4.2f), "5e4c3b", surface: "wood");
        MakeBox("ClinicPlainMirrorFrame", new(0.08f, 1.16f, 0.96f), new(5.79f, 2.08f, -1.35f), "68776d", collision: false);
        MakeBox("ClinicPlainMirror", new(0.04f, 0.98f, 0.78f), new(5.735f, 2.08f, -1.35f), "a4ada3", collision: false);
        var legacyVisualNames = new[]
        {
            "Floor",
            "BackWall",
            "FrontWall",
            "LeftWall",
            "RightWall",
            "ClinicCeiling",
            "ClinicCeilingFixtureHousing",
            "ClinicCeilingFixtureLens",
            "ClinicDoorPanel",
            "ClinicDoorFrameLeft",
            "ClinicDoorFrameRight",
            "ClinicDoorFrameTop",
            "ClinicDoorThreshold",
            "ClinicLinoleumField",
            "ClinicLinoleumSeam0",
            "ClinicLinoleumSeam1",
            "ClinicLinoleumSeam2",
            "ClinicLinoleumSeam3",
            "ClinicWindowLeftFrosted",
            "ClinicWindowLeftTrimTop",
            "ClinicWindowLeftTrimBottom",
            "ClinicWindowLeftTrimFront",
            "ClinicWindowLeftTrimBack",
            "ClinicWindowRightFrosted",
            "ClinicWindowRightTrimTop",
            "ClinicWindowRightTrimBottom",
            "ClinicWindowRightTrimFront",
            "ClinicWindowRightTrimBack",
            "ClinicRadiator",
            "ClinicRadiatorPipe0",
            "ClinicRadiatorPipe1",
            "ClinicRecordsBoard",
            "ClinicRecordsBoardFrameTop",
            "ClinicRecordsBoardFrameBottom",
            "ClinicRecordsBoardCardA",
            "ClinicRecordsBoardCardB",
            "ClinicDeskRecordStack",
            "ClinicDeskRecordTopPage",
            "DocumentDesk"
        };
        foreach (var legacyVisualName in legacyVisualNames)
        {
            if (GetNodeOrNull<Node3D>(legacyVisualName) is { } legacyVisual)
            {
                SuppressPrimitiveVisual(
                    legacyVisual,
                    "authored FapInteriorSet Wave14 replaces the legacy primitive visual; collision remains unchanged");
            }
        }
        SetMeta(
            "fapAuthoredInteriorShell",
            "FapInteriorSet authored floor|ceiling|four wall volumes|recessed threshold|window trims|ceiling practical");
        SetMeta("fapLegacyShellVisualSuppressed", true);
        SetMeta("fapLegacyVisualsSuppressed", string.Join("|", legacyVisualNames));

        // The authored counter is the reception anchor; keep Naila at its open
        // inner end so she reads immediately from the entry while the right
        // waiting-bench collision and center route stay unobstructed.
        AttachAct1Npc(
            "naila",
            "Naila",
            "medical clerk",
            new(1.8f, 0, 0.0f),
            yawDegrees: 8f);
        MakeInteractionBox(
            "NailaNpc",
            new(0.62f, 1.68f, 0.46f),
            new(1.8f, 0.84f, 0.0f),
            "6f6257",
            "urman.chapter1:interaction/talk-naila",
            "Поговорить с Наилей",
            dialogueId: "urman.chapter1:dialogue/naila_medical_record");
        MakeInteractionBox(
            "WaitingRoomToDesk",
            new(1.6f, 1.45f, 0.35f),
            new(0, 0.72f, -2.65f),
            "7b7467",
            "urman.chapter1:interaction/fap-to-document-desk",
            "Подойти к столу с документами");
        MakeInteractionBox(
            "DeskToOfficialRecord",
            new(1.3f, 0.12f, 0.9f),
            new(0, 0.94f, -4.1f),
            "b7a47e",
            "urman.chapter1:interaction/fap-document-desk-to-official-record",
            "Прочитать официальную справку о Марате",
            documentId: "urman.oldpc:document/doc_marat_official_death_notice");
        MakeInteractionBox(
            "OfficialRecordExitToStreet",
            new(1.3f, 2.1f, 0.25f),
            new(0, 1.05f, 5.82f),
            "625849",
            "urman.chapter1:interaction/official-leave-clinic",
            "Выйти из ФАПа и вернуться домой со справкой",
            "village_day",
            "from_fap");

        AddChild(new OmniLight3D
        {
            Name = "ColdCeilingLamp",
            Position = new(-0.2f, 2.8f, -0.7f),
            // Neutral blue-grey practical: it should describe the ceiling,
            // not flatten the whole clinic into a green wash.
            LightColor = Color.FromHtml("a4b1b1"),
            LightEnergy = 0.42f,
            OmniRange = 8.0f,
            ShadowEnabled = true
        });
        AddChild(new OmniLight3D
        {
            Name = "FapWindowColdFill",
            Position = new(-4.7f, 2.0f, -1.35f),
            LightColor = Color.FromHtml("7899a2"),
            LightEnergy = 0.44f,
            OmniRange = 6.0f,
            ShadowEnabled = false
        });
        AddChild(new OmniLight3D
        {
            Name = "FapDocumentTaskLight",
            Position = new(0.0f, 2.25f, -3.45f),
            // The record desk is the warm narrative focal point; the short
            // pool leaves the rear wall clinical and visually quiet.
            LightColor = Color.FromHtml("d5ad78"),
            LightEnergy = 1.42f,
            OmniRange = 3.15f,
            ShadowEnabled = true
        });
        AddChild(new OmniLight3D
        {
            Name = "FapNailaPractical",
            Position = new(2.65f, 2.15f, 1.05f),
            LightColor = Color.FromHtml("cda47c"),
            LightEnergy = 0.90f,
            OmniRange = 2.8f,
            ShadowEnabled = true
        });
        AddChild(new OmniLight3D
        {
            Name = "FapWindowOppositeFill",
            Position = new(4.7f, 2.0f, -1.35f),
            LightColor = Color.FromHtml("708790"),
            LightEnergy = 0.18f,
            OmniRange = 4.7f,
            ShadowEnabled = false
        });
    }

    private void AttachFapInteriorSet()
    {
        var packed = ResourceLoader.Load<PackedScene>(FapClinicKitScenePath)
            ?? throw new InvalidOperationException($"FAP clinic kit is missing: {FapClinicKitScenePath}");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"FAP clinic kit did not instantiate: {FapClinicKitScenePath}");

        try
        {
            var kitRoot = string.Equals(importedRoot.Name.ToString(), FapClinicKitRootName, StringComparison.Ordinal)
                ? importedRoot
                : importedRoot.GetNodeOrNull<Node3D>(FapClinicKitRootName)
                    ?? throw new InvalidOperationException($"FAP clinic kit is missing authored root '{FapClinicKitRootName}'");
            var collisionNodes = Descendants(kitRoot)
                .Where(node => node is CollisionObject3D or CollisionShape3D)
                .ToArray();
            if (collisionNodes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"FAP interior kit must be presentation-only; found {collisionNodes.Length} collision nodes");
            }

            var component = kitRoot.GetNodeOrNull<Node3D>(FapInteriorSetComponentName)
                ?? throw new InvalidOperationException(
                    $"FAP clinic kit is missing direct component '{FapInteriorSetComponentName}'");
            var sourceParent = component.GetParent()
                ?? throw new InvalidOperationException("FAP interior component has no authored root parent");
            var previewOrigin = component.Position;
            var importedBasis = ComposeImportedAncestorBasis(component);
            sourceParent.RemoveChild(component);
            ClearExtractedSceneOwnership(component);
            component.Transform = new Transform3D(importedBasis, Vector3.Zero);
            component.SetMeta("presentationOnly", true);
            component.SetMeta("visualOnly", true);
            component.SetMeta("presentationOnlyInstance", true);
            component.SetMeta("assetSource", FapClinicKitScenePath);
            component.SetMeta("authoredRoot", FapClinicKitRootName);
            component.SetMeta("authoredPreviewOrigin", previewOrigin);
            component.SetMeta("rebasedLocalTransform", "imported ancestor basis preserved; FapInteriorSet source origin is the room anchor");
            component.SetMeta("collisionOwner", "none");
            component.SetMeta("navigationOwner", "none");
            component.SetMeta("interactionOwner", "none");
            component.SetMeta("narrativeOwner", "none");
            component.SetMeta(
                "interiorSilhouettes",
                "authored 12 m shell|examination cot|folding privacy screen|wall medicine cabinet|enamel instrument trolley|waiting bench|wall radiator and pipes|open supply shelf|blank examination chart|coat hook rail|wash unit|attendant stool|blank records pinboard|full-volume records desk|reception counter|tall storage cabinet|partial-height zoning partition");
            var fapInteriorMeshes = Descendants(component).OfType<MeshInstance3D>().ToArray();
            var fapInteriorMeshNames = fapInteriorMeshes
                .Select(mesh => mesh.Name.ToString())
                .ToHashSet(StringComparer.Ordinal);
            var fapInteriorLodPairs = fapInteriorMeshNames
                .Where(name => name.EndsWith("_LOD0", StringComparison.Ordinal))
                .Select(name => name[..^5])
                .Where(baseName => fapInteriorMeshNames.Contains($"{baseName}_LOD1"))
                .ToHashSet(StringComparer.Ordinal);
            var materialOverrideCount = RegradeAuthoredKitMaterials(component, FapClinicKitScenePath);

            foreach (var mesh in fapInteriorMeshes)
            {
                var meshName = mesh.Name.ToString();
                var lod1 = meshName.EndsWith("_LOD1", StringComparison.Ordinal);
                var lod0 = meshName.EndsWith("_LOD0", StringComparison.Ordinal);
                var baseName = (lod0 || lod1) ? meshName[..^5] : string.Empty;
                var paired = baseName.Length > 0 && fapInteriorLodPairs.Contains(baseName);
                if (paired)
                {
                    mesh.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
                    mesh.VisibilityRangeBegin = lod1 ? 10f : 0f;
                    mesh.VisibilityRangeBeginMargin = lod1 ? 2f : 0f;
                    mesh.VisibilityRangeEnd = lod1 ? 28f : 14f;
                    mesh.VisibilityRangeEndMargin = lod1 ? 4f : 2f;
                    mesh.SetMeta("visibilityRange", lod1 ? "10-28m" : "0-14m");
                }
                else
                {
                    mesh.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled;
                    mesh.VisibilityRangeBegin = 0f;
                    mesh.VisibilityRangeBeginMargin = 0f;
                    mesh.VisibilityRangeEnd = 0f;
                    mesh.VisibilityRangeEndMargin = 0f;
                    mesh.SetMeta("visibilityRange", "unbounded");
                }

                mesh.SetMeta("presentationOwnership", "presentation-only");
                mesh.SetMeta("collisionPolicy", "no physics body; visual mesh only");
            }

            component.SetMeta("silhouetteCount", 17);
            component.SetMeta("meshCount", fapInteriorMeshes.Length);
            component.SetMeta(
                "lod0Count",
                fapInteriorMeshes.Count(mesh => mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)));
            component.SetMeta(
                "lod1Count",
                fapInteriorMeshes.Count(mesh => mesh.Name.ToString().EndsWith("_LOD1", StringComparison.Ordinal)));
            component.SetMeta("lodPairCount", fapInteriorLodPairs.Count);
            component.SetMeta(
                "unpairedLod0Count",
                fapInteriorMeshes.Count(mesh => mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal) && !fapInteriorLodPairs.Contains(mesh.Name.ToString()[..^5])));
            component.SetMeta("runtimeMeshTransformCount", 0);
            component.SetMeta("runtimeMaterialOverrideCount", materialOverrideCount);
            component.SetMeta(
                "fapInteriorPresentationPass",
                "source-authored Wave14 arrangement; imported ancestor basis only; constrained presentation-only value grade; center path and counter/partition composition remain source-owned");
            AddChild(component);
            SetMeta("fapInteriorPresentation", "FapInteriorSet project-original GLB; presentation-only; legacy replaced furniture visuals suppressed");
            importedRoot.Free();
        }
        catch
        {
            if (GodotObject.IsInstanceValid(importedRoot))
            {
                importedRoot.Free();
            }

            throw;
        }
    }

    private Node3D AttachAuthoredKitComponents(
        string scenePath,
        string rootName,
        IReadOnlyCollection<string> componentNames,
        string presentationName,
        string compositionRole,
        params (string ComponentName, string PlacementName, Vector3 Anchor, float YawDegrees, Vector3 Scale, string LogicalAnchor)[] placements)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Authored style kit is missing: {scenePath}");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"Authored style kit did not instantiate: {scenePath}");
        var presentation = default(Node3D);

        try
        {
            var kitRoot = string.Equals(importedRoot.Name.ToString(), rootName, StringComparison.Ordinal)
                ? importedRoot
                : importedRoot.GetNodeOrNull<Node3D>(rootName)
                    ?? throw new InvalidOperationException(
                        $"Authored style kit is missing root '{rootName}': {scenePath}");
            var collisionNodes = Descendants(kitRoot)
                .Where(node => node is CollisionObject3D or CollisionShape3D)
                .ToArray();
            if (collisionNodes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Authored style kit must be presentation-only; found {collisionNodes.Length} collision nodes: {scenePath}");
            }

            var missingComponents = componentNames
                .Where(componentName => kitRoot.GetNodeOrNull<Node3D>(componentName) is null)
                .ToArray();
            if (missingComponents.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Authored style kit is missing direct component roots: {string.Join('|', missingComponents)}");
            }

            var duplicatePlacements = placements
                .GroupBy(placement => placement.ComponentName, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePlacements.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Authored style kit benchmark cannot place one component more than once: {string.Join('|', duplicatePlacements)}");
            }

            var unknownPlacements = placements
                .Where(placement => !componentNames.Contains(placement.ComponentName, StringComparer.Ordinal))
                .Select(placement => placement.ComponentName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (unknownPlacements.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Authored style kit benchmark references unknown components: {string.Join('|', unknownPlacements)}");
            }

            presentation = new Node3D { Name = presentationName };
            presentation.SetMeta("presentationOnly", true);
            presentation.SetMeta("visualOnly", true);
            presentation.SetMeta("assetSource", scenePath);
            presentation.SetMeta("authoredRoot", rootName);
            presentation.SetMeta("componentContract", string.Join('|', componentNames));
            presentation.SetMeta("compositionRole", compositionRole);
            presentation.SetMeta("collisionOwner", "none");
            presentation.SetMeta("navigationOwner", "none");
            presentation.SetMeta("interactionOwner", "none");
            presentation.SetMeta("narrativeOwner", "none");
            presentation.SetMeta("runtimeStateOwnership", "RuntimeBridge");
            AddChild(presentation);

            foreach (var placement in placements)
            {
                var component = kitRoot.GetNodeOrNull<Node3D>(placement.ComponentName)
                    ?? throw new InvalidOperationException(
                        $"Authored style kit component disappeared during extraction: {placement.ComponentName}");
                AttachAuthoredKitComponent(
                    presentation,
                    rootName,
                    component,
                    placement.PlacementName,
                    placement.Anchor,
                    placement.YawDegrees,
                    placement.Scale,
                    placement.LogicalAnchor,
                    scenePath);
            }

            presentation.SetMeta("placedComponentCount", placements.Length);
            importedRoot.Free();
            return presentation;
        }
        catch
        {
            if (GodotObject.IsInstanceValid(importedRoot))
            {
                importedRoot.Free();
            }

            if (presentation is not null && GodotObject.IsInstanceValid(presentation))
            {
                presentation.Free();
            }

            throw;
        }
    }

    private static Node3D AttachAuthoredKitComponent(
        Node3D parent,
        string authoredRootName,
        Node3D component,
        string placementName,
        Vector3 anchor,
        float yawDegrees,
        Vector3 scale,
        string logicalAnchor,
        string assetSource)
    {
        var sourceParent = component.GetParent()
            ?? throw new InvalidOperationException(
                $"Authored style kit component '{component.Name}' has no authored root parent.");
        var authoredComponentName = component.Name.ToString();
        var authoredPreviewOrigin = component.Position;
        var importedAncestorBasis = ComposeImportedAncestorBasis(component);
        sourceParent.RemoveChild(component);
        ClearExtractedSceneOwnership(component);
        component.Transform = new Transform3D(importedAncestorBasis, Vector3.Zero);
        component.SetMeta("presentationOnly", true);
        component.SetMeta("visualOnly", true);
        component.SetMeta("presentationOnlyInstance", true);
        component.SetMeta("assetSource", assetSource);
        component.SetMeta("authoredRoot", authoredRootName);
        component.SetMeta("authoredComponent", authoredComponentName);
        component.SetMeta("authoredPreviewOrigin", authoredPreviewOrigin);
        component.SetMeta(
            "rebasedLocalTransform",
            "imported ancestor basis preserved; neutral preview-board origin removed before benchmark placement");
        component.SetMeta("collisionOwner", "none");
        component.SetMeta("navigationOwner", "none");
        component.SetMeta("interactionOwner", "none");
        component.SetMeta("narrativeOwner", "none");
        component.SetMeta("logicalAnchor", logicalAnchor);

        var meshes = Descendants(component).OfType<MeshInstance3D>().ToArray();
        foreach (var mesh in meshes)
        {
            mesh.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled;
            mesh.VisibilityRangeBegin = 0f;
            mesh.VisibilityRangeBeginMargin = 0f;
            mesh.VisibilityRangeEnd = 0f;
            mesh.VisibilityRangeEndMargin = 0f;
            mesh.SetMeta("presentationOwnership", "presentation-only");
            mesh.SetMeta("collisionPolicy", "no physics body; visual mesh only");
        }

        var placement = new Node3D
        {
            Name = placementName,
            Position = anchor,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f),
            Scale = scale
        };
        placement.SetMeta("presentationOnly", true);
        placement.SetMeta("visualOnly", true);
        placement.SetMeta("assetSource", assetSource);
        placement.SetMeta("authoredRoot", authoredRootName);
        placement.SetMeta("authoredComponent", authoredComponentName);
        placement.SetMeta("logicalAnchor", logicalAnchor);
        placement.SetMeta("collisionOwner", "none");
        placement.SetMeta("navigationOwner", "none");
        placement.SetMeta("interactionOwner", "none");
        parent.AddChild(placement);
        placement.AddChild(component);
        return placement;
    }

    private static int RegradeAuthoredKitMaterials(Node3D presentation, string scenePath)
    {
        var isZirat = scenePath.Contains("zirat", StringComparison.OrdinalIgnoreCase);
        var isFap = scenePath.Contains("fap_clinic", StringComparison.OrdinalIgnoreCase);
        var grade = isZirat
            ? new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["DampEarth"] = PainterlyMaterialLibrary.ForColor("56544a", "earth"),
                ["DampEarthDark"] = PainterlyMaterialLibrary.ForColor("4b473f", "earth"),
                ["PathDirt"] = PainterlyMaterialLibrary.ForColor("625847", "earth"),
                ["LeafLitter"] = PainterlyMaterialLibrary.ForColor("51493b", "earth"),
                ["DitchWater"] = PainterlyMaterialLibrary.ForColor("526066", "wet_ground"),
                ["WetRoad"] = PainterlyMaterialLibrary.ForColor("4f5553", "earth"),
                ["WetSheen"] = PainterlyMaterialLibrary.ForColor("596767", "wet_ground"),
                ["MossyStone"] = PainterlyMaterialLibrary.ForColor("62675c", "stone"),
                ["QuietStone"] = PainterlyMaterialLibrary.ForColor("62675c", "stone"),
                ["DistantStone"] = PainterlyMaterialLibrary.ForColor("555e57", "stone"),
                ["WeatheredWood"] = PainterlyMaterialLibrary.ForColor("594a39", "wood"),
                ["WeatheredWoodDark"] = PainterlyMaterialLibrary.ForColor("40352d", "wood_bark"),
                ["BirchBark"] = PainterlyMaterialLibrary.ForColor("68705a", "bark_birch"),
                ["BirchLeaves"] = PainterlyMaterialLibrary.ForColor("596047", "leaf_birch"),
                ["MossGreen"] = PainterlyMaterialLibrary.ForColor("596047", "foliage"),
                ["DitchGrass"] = PainterlyMaterialLibrary.ForColor("3c4d3e", "foliage"),
                ["RoadGrass"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
                ["ZiratGrass"] = PainterlyMaterialLibrary.ForColor("3c4d3e", "foliage"),
                ["ShrubGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
                ["DistantFoliage"] = PainterlyMaterialLibrary.ForColor("2c403b", "foliage"),
                ["DistantBark"] = PainterlyMaterialLibrary.ForColor("343630", "wood_bark"),
                ["DistantFence"] = PainterlyMaterialLibrary.ForColor("403b34", "wood"),
                ["DistantWall"] = PainterlyMaterialLibrary.ForColor("4b4f48", "plaster"),
                ["DistantRoof"] = PainterlyMaterialLibrary.ForColor("343a35", "wood")
            }
            : isFap
            ? new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                // Keep the source's material roles, but raise the usable
                // first-person value range so silhouettes survive the cold
                // institutional mood instead of collapsing into green-black.
                ["FapPaintedSage"] = PainterlyMaterialLibrary.ForColor("7b8d86", "plaster", sheltered: true),
                ["FapPaintedDustyBlue"] = PainterlyMaterialLibrary.ForColor("5f7a83", "plaster", sheltered: true),
                ["FapPaintedTimber"] = PainterlyMaterialLibrary.ForColor("806f58", "wood_furniture", sheltered: true),
                ["FapDarkTimber"] = PainterlyMaterialLibrary.ForColor("50473b", "wood_bark", sheltered: true),
                ["FapBirchPale"] = PainterlyMaterialLibrary.ForColor("a39579", "bark_birch", sheltered: true),
                ["FapBirchBarkMark"] = PainterlyMaterialLibrary.ForColor("6e604d", "bark_birch", sheltered: true),
                ["FapShrubGreen"] = PainterlyMaterialLibrary.ForColor("4f6351", "foliage", sheltered: true),
                ["FapShrubLight"] = PainterlyMaterialLibrary.ForColor("72846a", "foliage", sheltered: true),
                ["FapOldRoof"] = PainterlyMaterialLibrary.ForColor("3f4c49", "roof_metal", sheltered: true),
                ["FapRoofEdge"] = PainterlyMaterialLibrary.ForColor("56605a", "roof_metal", sheltered: true),
                ["FapWetStone"] = PainterlyMaterialLibrary.ForColor("59635f", "stone", sheltered: true),
                ["FapRainMetal"] = PainterlyMaterialLibrary.ForColor("708584", sheltered: true),
                ["FapRainMetalDark"] = PainterlyMaterialLibrary.ForColor("4b5d5b", sheltered: true),
                ["FapDoorWood"] = PainterlyMaterialLibrary.ForColor("695542", "wood", sheltered: true),
                ["FapDoorInset"] = PainterlyMaterialLibrary.ForColor("51453a", "wood", sheltered: true),
                ["FapFoundationStone"] = PainterlyMaterialLibrary.ForColor("6f7973", "stone", sheltered: true),
                ["FapVentDark"] = PainterlyMaterialLibrary.ForColor("3e4b47", sheltered: true),
                ["FapWindowCool"] = PainterlyMaterialLibrary.ForColor("779ba3", sheltered: true),
                ["FapWindowWarm"] = PainterlyMaterialLibrary.ForColor("b18b62", sheltered: true),
                ["FapShedWall"] = PainterlyMaterialLibrary.ForColor("788b80", "plaster", sheltered: true),
                ["FapNoticeBlank"] = PainterlyMaterialLibrary.ForColor("c1ae85", sheltered: true),
                ["FapPathEarth"] = PainterlyMaterialLibrary.ForColor("4f5145", "earth", sheltered: true),
                ["FapPathEarthDark"] = PainterlyMaterialLibrary.ForColor("3b4038", "earth", sheltered: true),
                ["FapPuddleWater"] = PainterlyMaterialLibrary.ForColor("465f61", "water", sheltered: true),
                ["FapWayfindingBlank"] = PainterlyMaterialLibrary.ForColor("8f987f", sheltered: true)
            }
            : new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["DampEarth"] = PainterlyMaterialLibrary.ForColor("34443b", "earth"),
                ["LeafLitter"] = PainterlyMaterialLibrary.ForColor("51493b", "earth"),
                ["PineBark"] = PainterlyMaterialLibrary.ForColor("40352d", "bark_pine"),
                ["WeatheredWood"] = PainterlyMaterialLibrary.ForColor("594a39", "wood"),
                ["CutWood"] = PainterlyMaterialLibrary.ForColor("8b7155", "wood"),
                ["PineFoliage"] = PainterlyMaterialLibrary.ForColor("30483f", "foliage"),
                ["FoliageBlueGreen"] = PainterlyMaterialLibrary.ForColor("48553f", "foliage"),
                ["BirchBark"] = PainterlyMaterialLibrary.ForColor("68705a", "bark_birch"),
                ["BirchLeaves"] = PainterlyMaterialLibrary.ForColor("596047", "leaf_birch"),
                ["Understory"] = PainterlyMaterialLibrary.ForColor("3c4d3e", "foliage"),
                ["RootDark"] = PainterlyMaterialLibrary.ForColor("3f332a", "wood_bark"),
                ["MossGreen"] = PainterlyMaterialLibrary.ForColor("596047", "foliage"),
                ["MossyStone"] = PainterlyMaterialLibrary.ForColor("62675c", "stone"),
                ["DistantBlueGreen"] = PainterlyMaterialLibrary.ForColor("2c403b", "foliage"),
                ["DistantFoliage"] = PainterlyMaterialLibrary.ForColor("243a34", "foliage"),
                ["DistantBark"] = PainterlyMaterialLibrary.ForColor("343630", "wood_bark")
            };

        var rebound = 0;
        foreach (var mesh in Descendants(presentation).OfType<MeshInstance3D>())
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            var meshName = mesh.Name.ToString();
            var fapFloor = meshName.StartsWith("FapInteriorShell_Floor", StringComparison.Ordinal);
            var fapCeiling = meshName.StartsWith("FapInteriorShell_Ceiling", StringComparison.Ordinal);
            if (isFap
                && ((meshName.StartsWith("FapInteriorShell_", StringComparison.Ordinal)
                     && meshName.Contains("Wall", StringComparison.Ordinal))
                    || meshName.StartsWith("FapInteriorWallPanel_", StringComparison.Ordinal)
                    || fapFloor || fapCeiling))
            {
                var wall = PainterlyMaterialLibrary.ForColor(
                    fapFloor ? "797d77" : fapCeiling ? "b5b4a4" : "7b8d86",
                    fapFloor ? "floor_institution" : fapCeiling ? string.Empty : "wall_institution", sheltered: true);
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    mesh.SetSurfaceOverrideMaterial(surface, wall);
                    rebound++;
                }
                continue;
            }

            var sourceName = mesh.GetActiveMaterial(0)?.ResourceName ?? string.Empty;
            if (!grade.TryGetValue(sourceName, out var material))
            {
                continue;
            }

            if (isFap && meshName.StartsWith("FapInteriorScreen_Panel", StringComparison.Ordinal))
            {
                material = PainterlyMaterialLibrary.ForColor("5f7a83", "cloth", sheltered: true);
            }

            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                mesh.SetSurfaceOverrideMaterial(surface, material);
                rebound++;
            }
        }

        presentation.SetMeta(
            "materialGrade",
            isZirat
                ? "Zirat damp earth/wet road/quiet boundary remapped to painterly wet palette"
                : isFap
                    ? "FAP cool institutional wall/floor separation with restrained timber, metal and paper value accents"
                    : "Kara damp earth/leaf litter/understory remapped to painterly wet palette");
        presentation.SetMeta("materialGradeReboundCount", rebound);
        return rebound;
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static Basis ComposeImportedAncestorBasis(Node3D component)
    {
        var basis = component.Transform.Basis;
        for (var ancestor = component.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is Node3D node)
            {
                basis = node.Transform.Basis * basis;
            }
        }

        return basis;
    }

    private static void ClearExtractedSceneOwnership(Node node)
    {
        node.Owner = null;
        foreach (var child in node.GetChildren())
        {
            ClearExtractedSceneOwnership(child);
        }
    }

    private void BuildZiratRoad()
    {
        MakeBox("Ground", new(34, 0.25f, 44), new(0, -0.125f, 0), "44493b");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "Road", 4.2f, 42f, Vector3.Zero, "625847");
        // Only the third Zirat patch clears the strict 5 mm gate; seat it
        // locally without changing the other two authored offsets.
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "ZiratWetPatch", new(-0.72f, 0.102f, 6.8f), new(0.76f, 1.32f), "4f5553", [0f, 0f, -0.006205320f]);
        var ziratAuthoredKit = AttachAuthoredKitComponents(
            ZiratRoadsideKitScenePath,
            ZiratRoadsideKitRootName,
            ZiratRoadsideKitComponentNames,
            "ZiratRoadsideAuthoredKit",
            "authored wet roadside, quiet boundary, path edge, birch transition and distant village closure",
            ("WetRoadShoulder_Left", "ZiratWetRoadShoulderLeft", new(0f, 0f, -1.5f), 0f, Vector3.One * 1.05f, "zirat-benchmark@wet-shoulder-left"),
            ("WetRoadShoulder_Right", "ZiratWetRoadShoulderRight", new(0f, 0f, -1.5f), 0f, Vector3.One * 1.05f, "zirat-benchmark@wet-shoulder-right"),
            ("RoadsideDitch", "ZiratRoadsideDitch", new(0f, 0f, -1.1f), 0f, Vector3.One, "zirat-benchmark@roadside-drainage"),
            ("CulvertStoneCluster", "ZiratCulvertStoneCluster", new(0f, 0f, -3.5f), 0f, Vector3.One, "zirat-benchmark@culvert-edge"),
            ("ZiratBoundaryFence", "ZiratAuthoredBoundaryFence", new(6.7f, 0f, -15.0f), 90f, Vector3.One * 0.96f, "zirat-benchmark@lateral-boundary-fence"),
            ("ZiratOpenGate", "ZiratAuthoredOpenGate", new(6.7f, 0f, -15.0f), 90f, Vector3.One * 0.96f, "zirat-benchmark@lateral-open-gate"),
            ("ZiratMarkerGroup_Low", "ZiratAuthoredMarkerGroupLow", new(6.2f, 0f, -9.0f), 0f, Vector3.One * 0.92f, "zirat-benchmark@quiet-marker-group-near"),
            ("ZiratMarkerGroup_Far", "ZiratAuthoredMarkerGroupFar", new(6.5f, 0f, -10.0f), 0f, Vector3.One * 0.92f, "zirat-benchmark@quiet-marker-group-far"),
            ("ZiratPathEdge", "ZiratAuthoredPathEdge", new(-4.0f, 0f, -10.0f), 0f, Vector3.One * 0.96f, "zirat-benchmark@side-path-edge"),
            ("ZiratBirchShrubMass", "ZiratAuthoredBirchShrubTransition", new(7.8f, 0f, -22.0f), 0f, Vector3.One * 0.94f, "zirat-benchmark@birch-forest-transition"),
            ("ZiratDistantVillageMass", "ZiratAuthoredDistantVillageTransition", new(-22.0f, 0f, 10.0f), 0f, Vector3.One, "zirat-benchmark@distant-village-transition"));
        RegradeAuthoredKitMaterials(ziratAuthoredKit, ZiratRoadsideKitScenePath);

        // Static staging for beat/rinat-visible-before-edge: keep Rinat on the
        // right shoulder, ahead of the player and outside the route corridor.
        // The kit has no dedicated Rinat prefix, so the muted civic witness is
        // the existing presentation proxy; conditional staging needs a new
        // state owner and is intentionally out of this benchmark slice.
        AttachAct1Npc(
            "rinat",
            "CouncilWitness",
            "village police officer",
            new(4.8f, 0, -13.8f),
            yawDegrees: 180f);

        // A small, non-religious roadside trace gives the zirat passage a
        // concrete investigative beat. The visible marker is presentation
        // only; the hidden InteractionTarget below owns the RuntimeBridge
        // dispatch and remains available only while the zirat-road scene is
        // active.
        MakeBox("ZiratRouteTracePost", new(0.14f, 0.86f, 0.14f), new(-3.55f, 0.43f, -5.8f), "594939", collision: false, surface: "wood");
        // A narrow vertical tag is a roadside trace, not a cross-shaped
        // grave marker. Keep the same clue location and interaction owner.
        MakeRotatedBox("ZiratRouteTraceTag", new(0.16f, 0.28f, 0.035f), new(-3.55f, 0.62f, -5.70f), new(0, -8, -5), "8d765b", "wood");
        // The two visible marks are the same concrete landmark drawn on Mansur's sketch.
        for (var mark = 0; mark < 2; mark++)
            MakeRotatedBox($"ZiratRouteTraceNotch{mark}", new(.10f, .012f, .008f),
                new(-3.55f, .59f + mark * .055f, -5.677f), new(0, -8, -5), "30281f", "wood");
        MakeInteractionBox(
            "ZiratRoadsideClue",
            new(0.9f, 1.35f, 0.7f),
            new(-3.55f, 0.72f, -5.8f),
            "8d765b",
            "urman.chapter1:interaction/zirat-roadside-clue",
            "Осмотреть след у зиратской дороги");

        MakeInteractionBox(
            "ZiratRoadToForest",
            new(2.6f, 1.5f, 0.3f),
            new(0, 0.75f, -19.5f),
            "39453b",
            "urman.chapter1:interaction/zirat-road-to-forest",
            "Подойти к кромке Кара-Урмана",
            "kara_urman_night",
            "village_path");

        SetMeta("styleAuthoredModules", "ZiratRoadsideKit_wave4");
        SetMeta("styleAuthoredMeshPolicy", "authored wet shoulders, ditch, quiet boundary and distant closure; no repeated procedural pine row");
        SetMeta("styleAuthoredReview", "candidate geometry only; 360-degree first-person and cultural/local review remain open");
    }

    private void MakeClothesline(Vector3 start, Vector3 end)
    {
        MakeCylinder("LaundryPoleA", 0.07f, 3.2f, start + new Vector3(0, 1.6f, 0), "594a39", "wood");
        MakeCylinder("LaundryPoleB", 0.07f, 3.2f, end + new Vector3(0, 1.6f, 0), "594a39", "wood");
        PainterlyEnvironmentDetails.AddCable(this, start + new Vector3(0, 3.0f, 0), end + new Vector3(0, 3.0f, 0));
        var colors = new[] { "a99b82", "6e8079", "b58e6c", "7d6f66" };
        for (var index = 0; index < colors.Length; index++)
        {
            var t = (index + 1) / (float)(colors.Length + 1);
            var position = start.Lerp(end, t) + new Vector3(0, 2.63f - index % 2 * 0.08f, 0);
            MakeRotatedBox($"LaundryCloth{index}", new(0.42f, 0.58f + index % 2 * 0.1f, 0.035f), position, new(0, 0, index % 2 == 0 ? -3 : 4), colors[index]);
        }
    }

    private void MakeCrateStack(Vector3 origin, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var row = index / 2;
            var column = index % 2;
            MakeBox($"CrateStack{index}", new(0.78f, 0.46f, 0.68f), origin + new Vector3(column * 0.72f, 0.23f + row * 0.47f, (row % 2) * 0.12f), index % 2 == 0 ? "76553e" : "614835", collision: false, surface: "wood");
            MakeBox($"CrateSlat{index}", new(0.62f, 0.035f, 0.035f), origin + new Vector3(column * 0.72f, 0.23f + row * 0.47f, 0.35f + (row % 2) * 0.12f), "9b7651", collision: false, surface: "wood");
        }
    }

    private void MakeHaySheaf(Vector3 origin, float height)
    {
        MakeCylinder("HaySheaf", 0.33f, height, origin + new Vector3(0, height * 0.5f, 0), "88734f", "foliage");
        for (var index = 0; index < 5; index++)
        {
            MakeRotatedBox($"HayBlade{index}", new(0.035f, height * 0.9f, 0.035f), origin + new Vector3((index - 2) * 0.08f, height * 0.56f, 0), new(index * 4 - 8, index * 28, index % 2 == 0 ? -4 : 5), "aa8d57", "foliage");
        }
    }

    private void MakeSignpost(Vector3 origin)
    {
        MakeCylinder("VillageSignPost", 0.08f, 1.95f, origin + new Vector3(0, 0.98f, 0), "594939", "wood");
        MakeRotatedBox("VillageSignBoard", new(1.15f, 0.46f, 0.08f), origin + new Vector3(0, 1.68f, 0), new(0, -4, 0), "806b4f", "wood");
        MakeRotatedBox("VillageSignArrow", new(0.42f, 0.04f, 0.025f), origin + new Vector3(0.12f, 1.68f, -0.045f), new(0, -4, 0), "c0a783");
        var label = new Label3D
        {
            Name = "VillageSignText",
            Text = "ФАП",
            Position = origin + new Vector3(-0.23f, 1.68f, 0.058f),
            Modulate = Color.FromHtml("c8b794"),
            OutlineModulate = Color.FromHtml("3c352b"),
            FontSize = 48,
            PixelSize = 0.0035f,
            OutlineSize = 4,
            DoubleSided = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled
        };
        label.SetMeta("wayfindingLandmark", "fap");
        AddChild(label);
    }

    private void MakeShelfStillLife()
    {
        MakeBox("StillLifeShelf", new(2.55f, 0.12f, 0.42f), new(-3.8f, 2.05f, -4.58f), "574331", collision: false, surface: "wood");
        for (var index = 0; index < 4; index++)
        {
            MakeCylinder($"ShelfJar{index}", 0.10f + index % 2 * 0.025f, 0.28f + index % 3 * 0.05f, new(-4.72f + index * 0.55f, 2.25f, -4.57f), index % 2 == 0 ? "9c8060" : "6c7c72", "plaster");
        }
        MakeBox("ShelfCloth", new(0.62f, 0.36f, 0.025f), new(-2.72f, 2.27f, -4.57f), "728478", collision: false);
    }

    private void MakeWallCalendar()
    {
        MakeBox("WallCalendarFrame", new(0.62f, 0.82f, 0.04f), new(-1.35f, 2.12f, -4.82f), "604936", collision: false, surface: "wood");
        MakeBox("WallCalendarPage", new(0.49f, 0.63f, 0.02f), new(-1.35f, 2.12f, -4.80f), "c9b98f", collision: false);
        for (var row = 0; row < 3; row++)
        {
            MakeBox($"CalendarLine{row}", new(0.28f, 0.018f, 0.012f), new(-1.35f, 2.30f - row * 0.14f, -4.785f), "8f7767", collision: false);
        }
    }

    private void MakeRadioAndHerbs()
    {
        MakeBox("OldRadio", new(0.78f, 0.36f, 0.42f), new(3.35f, 1.06f, -4.35f), "55534b", collision: false, surface: "wood");
        MakeBox("RadioGrille", new(0.42f, 0.14f, 0.02f), new(3.35f, 1.07f, -4.57f), "302f2e", collision: false);
        MakeDisc("RadioKnob", new(0.055f, 0.04f, 0.055f), new(3.62f, 1.14f, -4.58f), "b09162");
        for (var index = 0; index < 5; index++)
        {
            MakeRotatedBox($"HerbStem{index}", new(0.02f, 0.62f + index % 2 * 0.1f, 0.02f), new(4.88f + index * 0.06f, 1.48f, -4.58f), new(index * 8 - 16, 0, index % 2 == 0 ? -11 : 9), "6d7c55", "foliage");
        }
    }

    private void MakeFallenLog(Vector3 position, float length, Vector3 rotationDegrees) =>
        MakeLog(position, length, rotationDegrees, "5d4635");

    private void MakeStump(Vector3 position, float radius)
    {
        MakeCylinder("ForestStump", radius, 0.42f, position + new Vector3(0, 0.21f, 0), "554234", "wood");
        MakeDisc("ForestStumpTop", new(radius * 0.92f, 0.025f, radius * 0.92f), position + new Vector3(0, 0.435f, 0), "8b7657");
    }

    private void MakeBoundaryCharm(Vector3 position, Vector3 scale)
    {
        MakeBox("BoundaryCharmCord", new(0.035f, 1.0f, 0.035f), position + new Vector3(0, 0.5f, 0), "756452", collision: false);
        MakeRotatedBox("BoundaryCharmCloth", new Vector3(0.34f, 0.48f, 0.028f) * scale, position + new Vector3(0.12f, 0.9f, 0), new(0, 0, -7), "b3a279");
    }

    private void MakeBranch(Vector3 start, Vector3 end, string color)
    {
        var direction = end - start;
        var branch = new MeshInstance3D
        {
            Name = "ForestBranchSilhouette",
            Position = (start + end) * 0.5f,
            Mesh = new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.07f, Height = direction.Length(), RadialSegments = 6 },
            MaterialOverride = Material(color, "wood")
        };
        // Keep the numeric suffix recognized by connected-world suppression.
        AddChild(branch, forceReadableName: true);
        branch.LookAt(end, Vector3.Up);
        branch.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
    }

    private void MakeHouse(Vector3 origin, string wallColor, string roofColor, bool warmWindow)
    {
        MakeBox("House", new(6.2f, 2.8f, 5.2f), origin + new Vector3(0, 1.4f, 0), wallColor, surface: "plaster");
        MakeRotatedBox("RoofLeft", new(3.8f, 0.28f, 5.8f), origin + new Vector3(-1.45f, 3.4f, 0), new(0, 0, 24), roofColor, "wood");
        MakeRotatedBox("RoofRight", new(3.8f, 0.28f, 5.8f), origin + new Vector3(1.45f, 3.4f, 0), new(0, 0, -24), roofColor, "wood");
        MakeBox("Chimney", new(0.48f, 1.25f, 0.48f), origin + new Vector3(-1.75f, 3.65f, -0.8f), "4a4540", collision: false);
        MakeBox("Foundation", new(6.45f, 0.42f, 5.45f), origin + new Vector3(0, 0.18f, 0), "3e3a34");
        MakeBox("Window", new(1.05f, 0.85f, 0.06f), origin + new Vector3(0.9f, 1.55f, 2.63f), warmWindow ? "d9a15d" : "65716e", collision: false);
        MakeBox("WindowTopTrim", new(1.35f, 0.11f, 0.11f), origin + new Vector3(0.9f, 2.03f, 2.7f), "c0aa87", collision: false);
        MakeBox("WindowBottomTrim", new(1.35f, 0.11f, 0.11f), origin + new Vector3(0.9f, 1.08f, 2.7f), "c0aa87", collision: false);
        MakeBox("WindowLeftTrim", new(0.11f, 1.05f, 0.11f), origin + new Vector3(0.3f, 1.55f, 2.7f), "c0aa87", collision: false);
        MakeBox("WindowRightTrim", new(0.11f, 1.05f, 0.11f), origin + new Vector3(1.5f, 1.55f, 2.7f), "c0aa87", collision: false);
        MakeBox("Door", new(1.12f, 2.15f, 0.12f), origin + new Vector3(-1.65f, 1.08f, 2.66f), "4b382c", collision: false, surface: "wood");
        MakeBox("DoorAwning", new(1.75f, 0.14f, 1.05f), origin + new Vector3(-1.65f, 2.4f, 2.9f), "504137", collision: false, surface: "wood");
        MakeBox("Porch", new(2.1f, 0.22f, 1.15f), origin + new Vector3(-1.65f, 0.18f, 3.05f), "5d4a38", surface: "wood");
        MakeBox("PorchStep", new(1.65f, 0.18f, 0.62f), origin + new Vector3(-1.65f, 0.08f, 3.75f), "574636", surface: "wood");
        MakeBox("WindowCrossVertical", new(0.06f, 0.82f, 0.04f), origin + new Vector3(0.9f, 1.55f, 2.68f), "bca884", collision: false);
        MakeBox("WindowCrossHorizontal", new(1.02f, 0.06f, 0.04f), origin + new Vector3(0.9f, 1.55f, 2.68f), "bca884", collision: false);
    }

    private void MakeFence(float x, float z, int count) =>
        PainterlyEnvironmentDetails.AddFence(this, x, z, count);

    private void MakeLog(Vector3 position, float length, Vector3 rotationDegrees, string color)
    {
        AddChild(new MeshInstance3D
        {
            Name = "FirewoodLog",
            Position = position,
            RotationDegrees = rotationDegrees,
            Mesh = new CylinderMesh
            {
                TopRadius = 0.11f,
                BottomRadius = 0.13f,
                Height = length,
                RadialSegments = 7
            },
            MaterialOverride = Material(color, "wood")
        });
    }

    private void MakeCup(Vector3 position)
    {
        AddChild(new MeshInstance3D
        {
            Name = "TeaCup",
            Position = position,
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.038f, Height = 0.09f, RadialSegments = 16, CapTop = false },
            MaterialOverride = Material("8d6b55", "plaster")
        });
        AddChild(new MeshInstance3D
        {
            Name = "TeaSurface",
            Position = position + new Vector3(0, 0.037f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.051f, BottomRadius = 0.051f, Height = 0.006f, RadialSegments = 16 },
            MaterialOverride = Material("3d3029")
        });
    }

    private void MakePine(Vector3 origin, float height, string foliageColor = "")
    {
        PainterlyEnvironmentDetails.AddPine(
            this,
            origin,
            height,
            string.IsNullOrEmpty(foliageColor) ? ForestFoliageColor(origin) : foliageColor);
    }

    private static string ForestFoliageColor(Vector3 origin)
    {
        // A stable world-space phase keeps captures deterministic while
        // avoiding a visible left-to-right material stripe. The palette is
        // deliberately narrow: variation should separate depth planes, not
        // turn the village into a bright fantasy forest.
        var phase = Mathf.Abs(Mathf.Sin(origin.X * 1.31f + origin.Z * 0.77f) * 1000f);
        return ForestFoliagePalette[(int)phase % ForestFoliagePalette.Length];
    }

    private void AttachPresentationPine(string name, Vector3 anchor, float scale, float yawDegrees)
    {
        var instance = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-forest-pine",
            ["PineA_"],
            anchor,
            scale,
            yawDegrees);
        instance.Name = name;
        instance.SetMeta("stylePineScale", scale);
        instance.SetMeta("stylePineYawDegrees", yawDegrees);
    }

    private void MakeUtilityPole(Vector3 origin) =>
        PainterlyEnvironmentDetails.AddUtilityPole(this, origin);

    private void MakeGrassTuft(Vector3 origin, float height) =>
        PainterlyEnvironmentDetails.AddGrassTuft(this, origin, height);

    private void AddShrub(Vector3 origin, float size, string color = "3f503a") =>
        PainterlyEnvironmentDetails.AddShrub(this, origin, size, color);

    private void MakeDisc(string name, Vector3 size, Vector3 position, string color, string surface = "")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            Scale = size,
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1, RadialSegments = 12 },
            MaterialOverride = Material(color, surface)
        };
        AddChild(mesh);
    }

    private void MakeRotatedBox(
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotationDegrees,
        string color,
        string surface = "")
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotationDegrees,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color, surface)
        };
        AddChild(mesh);
    }

    private void MakeCylinder(
        string name,
        float radius,
        float height,
        Vector3 position,
        string color,
        string surface = "",
        Vector3? rotationDegrees = null)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotationDegrees ?? Vector3.Zero,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.08f, Height = height, RadialSegments = 8 },
            MaterialOverride = Material(color, surface)
        });
    }

    private StaticBody3D MakeBox(
        string name,
        Vector3 size,
        Vector3 position,
        string color,
        bool collision = true,
        string surface = "")
    {
        var body = new StaticBody3D { Name = name, Position = position };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color, surface)
        });
        if (collision)
        {
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        }

        // Preserve semantic families (House2, Foundation2) for the connected
        // world's explicit legacy suppression; anonymous names evade it.
        AddChild(body, forceReadableName: true);
        return body;
    }

    private StaticBody3D MakeCollisionBox(string name, Vector3 size, Vector3 position)
    {
        var body = MakeBox(name, size, position, "000000");
        foreach (var mesh in body.GetChildren().OfType<MeshInstance3D>())
        {
            mesh.Visible = false;
        }

        body.SetMeta("visualHidden", true);
        body.SetMeta("collisionOwner", "act1-greybox-helper");
        return body;
    }

    private static void SuppressPrimitiveVisual(Node3D body, string reason)
    {
        if (body is MeshInstance3D selfMesh)
        {
            selfMesh.Visible = false;
        }

        foreach (var mesh in body.GetChildren().OfType<MeshInstance3D>())
        {
            mesh.Visible = false;
        }

        body.SetMeta("visualSuppressed", true);
        body.SetMeta("visualSuppressionReason", reason);
    }

    private void AttachAct1Npc(
        string characterId,
        string prefix,
        string role,
        Vector3 anchor,
        float yawDegrees,
        string presentationHostName = "Act1NpcPresentation")
    {
        var host = GetNodeOrNull<Node3D>(presentationHostName);
        if (host is null)
        {
            host = new Node3D { Name = presentationHostName };
            host.SetMeta("zoneKind", ZoneKind.ToString());
            host.SetMeta("status", "generated-character-kit-v1");
            host.SetMeta("assetSource", GeneratedCharacterKitDressing.ScenePath);
            host.SetMeta("ownership", "presentation-only");
            host.SetMeta("collisionPolicy", "no physics body; interaction targets own raycast and collision");
            host.SetMeta("defaultAnimationClip", "Idle");
            AddChild(host);
        }

        var npc = GeneratedCharacterKitDressing.Attach(
            host,
            characterId,
            prefix,
            anchor,
            sheltered: ZoneKind is BenchmarkKind.HouseOldPc or BenchmarkKind.FapClinic);
        if (!GeneratedCharacterKitDressing.PlayClip(npc, "Idle"))
        {
            npc.QueueFree();
            throw new InvalidOperationException($"Act 1 NPC '{prefix}' could not play its authored Idle clip.");
        }

        npc.Name = $"Npc_{characterId}";
        npc.RotationDegrees = new Vector3(0, yawDegrees, 0);
        npc.SetMeta("role", role);
        npc.SetMeta("presentationStatus", "generated-character-kit-v1");
        npc.SetMeta("interactionOwnership", "none");
        npc.SetMeta("collisionLayer", 0);
        npc.SetMeta("assetStatus", "project-original low-poly GLB with face/clothing detail and Godot Idle/Tension playback; expression/audio polish open");
        host.SetMeta("npcCount", host.GetChildren().OfType<Node3D>().Count());
    }

    internal InteractionTarget MakeInteractionBox(
        string name,
        Vector3 size,
        Vector3 position,
        string color,
        string interactionId,
        string prompt,
        string targetZoneId = "",
        string targetSpawnPointId = "entry",
        string dialogueId = "",
        string documentId = "",
        bool rayOnly = false)
    {
        var body = new InteractionTarget
        {
            Name = name,
            Position = position,
            CollisionLayer = rayOnly ? 4u : 1u,
            CollisionMask = rayOnly ? 0u : 1u,
            InteractionId = interactionId,
            Prompt = prompt,
            TargetZoneId = targetZoneId,
            TargetSpawnPointId = targetSpawnPointId,
            DialogueId = dialogueId,
            DocumentId = documentId
        };
        var hiddenProxyVisual = new Node3D { Name = "HiddenInteractionProxyVisual" };
        hiddenProxyVisual.SetMeta("visualHidden", true);
        hiddenProxyVisual.AddChild(new MeshInstance3D
        {
            Name = "HiddenInteractionProxyMesh",
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color),
            Visible = false
        });
        // Keep the visual below a wrapper so InteractionTarget's availability
        // refresh cannot turn the proxy mesh back on; its collision shape and
        // StaticBody3D contract remain untouched for the interaction raycast.
        body.AddChild(hiddenProxyVisual);
        body.AddChild(new CollisionShape3D
        {
            Name = "InteractionProxyCollisionShape",
            Shape = new BoxShape3D { Size = size }
        });
        body.SetMeta("proxyVisualHidden", true);
        AddChild(body);
        return body;
    }

    private Material Material(string htmlColor, string surface = "") =>
        PainterlyMaterialLibrary.ForColor(
            htmlColor,
            surface,
            sheltered: ZoneKind is BenchmarkKind.HouseOldPc or BenchmarkKind.FapClinic);
}
