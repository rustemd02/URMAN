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
        var sky = interior ? null : new Sky
        {
            RadianceSize = Sky.RadianceSizeEnum.Size256,
            SkyMaterial = new ProceduralSkyMaterial
            {
                SkyTopColor = night ? Color.FromHtml("172631") : Color.FromHtml("334f5b"),
                SkyHorizonColor = night ? Color.FromHtml("63727a") : Color.FromHtml("d6cbb9"),
                SkyCurve = night ? 0.18f : 0.12f,
                GroundBottomColor = night ? Color.FromHtml("182221") : Color.FromHtml("3f4b3d"),
                GroundHorizonColor = night ? Color.FromHtml("47595a") : Color.FromHtml("a9ad9b"),
                GroundCurve = 0.16f,
                SunAngleMax = night ? 1.2f : 4.2f,
                SunCurve = 0.08f
            }
        };
        var environment = new global::Godot.Environment
        {
            BackgroundMode = interior ? global::Godot.Environment.BGMode.Color : global::Godot.Environment.BGMode.Sky,
            Sky = sky,
            BackgroundColor = night ? Color.FromHtml("17232a") : interior ? Color.FromHtml("3b2b21") : Color.FromHtml("52646b"),
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
            // Kara-Urman needs a readable cold value floor: the previous
            // calibration collapsed the walkable path and foreground trees
            // into one blue-black mass at first-person distance.
            AmbientLightColor = night ? Color.FromHtml("7b9096") : houseInterior ? Color.FromHtml("9d9489") : interior ? Color.FromHtml("98a59f") : Color.FromHtml("a9b2a4"),
            AmbientLightEnergy = night ? 0.64f : houseInterior ? 0.54f : interior ? 0.68f : 0.80f,
            FogEnabled = !interior,
            FogLightColor = night ? Color.FromHtml("52666d") : Color.FromHtml("87918b"),
            FogDensity = night ? 0.0034f : 0.0032f,
            FogHeight = 1.2f,
            FogHeightDensity = night ? 0.075f : 0.05f,
            TonemapMode = global::Godot.Environment.ToneMapper.Filmic
        };
        AddChild(new WorldEnvironment { Environment = environment, Name = "WorldEnvironment" });

        if (!interior)
        {
            var sun = new DirectionalLight3D
            {
                Name = "MainDirectionalLight",
                RotationDegrees = night ? new(-52, -28, 0) : new(-48, -32, 0),
                LightColor = night ? Color.FromHtml("859aa1") : Color.FromHtml("d6d0bd"),
                LightEnergy = night ? 0.82f : 1.08f,
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
            new(2.15f, 0.88f, -1.7f),
            "4d5551",
            "urman.chapter1:interaction/talk-rinat",
            "Поговорить с Ринатом",
            dialogueId: "urman.chapter1:dialogue/rinat_no_key");

        MakeInteractionBox(
            "HouseDoor",
            new(1.2f, 2.1f, 0.25f),
            // Existing persistent-world exterior anchor, not the local
            // house_old_pc interior entry transform.
            new(AgentBAct1Layout.HouseExteriorSpawn.X, 1.05f, AgentBAct1Layout.HouseExteriorSpawn.Z),
            "6d5844",
            "urman.chapter1:interaction/arrival-enter-house",
            "Войти в дом бабая и әби",
            "house_old_pc",
            "entry");
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
        MakeBox("BackWall", new(12, 3.4f, 0.25f), new(0, 1.7f, -5), "827461", surface: "plaster");
        MakeBox("LeftWall", new(0.25f, 3.4f, 10), new(-6, 1.7f, 0), "786b5a", surface: "plaster");
        MakeBox("RightWall", new(0.25f, 3.4f, 10), new(6, 1.7f, 0), "786b5a", surface: "plaster");
        MakeBox("FrontWallLeft", new(5.25f, 3.4f, 0.25f), new(-3.375f, 1.7f, 5), "827461", surface: "plaster");
        MakeBox("FrontWallRight", new(5.25f, 3.4f, 0.25f), new(3.375f, 1.7f, 5), "827461", surface: "plaster");
        MakeBox("FrontWallLintel", new(1.5f, 1.15f, 0.25f), new(0, 2.825f, 5), "827461", surface: "plaster");
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
        MakeBox("Table", new(3.2f, 0.14f, 1.35f), new(0, 0.82f, -3.6f), "57402e", surface: "wood");
        MakeBox("TableLegL", new(0.18f, 0.82f, 0.18f), new(-1.35f, 0.4f, -3.6f), "463326", surface: "wood");
        MakeBox("TableLegR", new(0.18f, 0.82f, 0.18f), new(1.35f, 0.4f, -3.6f), "463326", surface: "wood");
        MakeDisc("Mouse", new(0.24f, 0.055f, 0.34f), new(0.92f, 0.96f, -2.82f), "6f6d61");
        MakeBox("DocumentStack", new(0.72f, 0.045f, 0.95f), new(-1.05f, 0.94f, -3.0f), "b0a17e", collision: false);
        MakeBox("DocumentShadow", new(0.76f, 0.025f, 0.98f), new(-1.01f, 0.91f, -3.04f), "5f5140", collision: false);
        MakeRotatedBox("DocumentTopPage", new(0.58f, 0.018f, 0.76f), new(-0.95f, 0.98f, -3.02f), new(0, -4, 1), "d3c39d");
        MakeBox("DocumentRedMark", new(0.36f, 0.022f, 0.025f), new(-0.95f, 1.0f, -3.18f), "9b5a4c", collision: false);
        MakeCup(new(-1.82f, 1.08f, -3.18f));
        MakeRotatedBox("ChairSeat", new(0.9f, 0.12f, 0.86f), new(-2.15f, 0.52f, -1.25f), new(0, -18, 0), "684b37", "wood");
        MakeRotatedBox("ChairBack", new(0.9f, 1.05f, 0.12f), new(-2.02f, 1.0f, -0.83f), new(0, -18, 0), "684b37", "wood");
        MakeBox("Cupboard", new(1.45f, 2.35f, 0.72f), new(-4.85f, 1.17f, -3.9f), "70563e", surface: "wood");
        MakeBox("CupboardDoorLine", new(0.06f, 2.08f, 0.05f), new(-4.85f, 1.2f, -3.51f), "3f3026", collision: false);
        MakeDisc("CupboardKnobLeft", new(0.055f, 0.045f, 0.055f), new(-5.02f, 1.25f, -3.46f), "c39b59");
        MakeDisc("CupboardKnobRight", new(0.055f, 0.045f, 0.055f), new(-4.68f, 1.25f, -3.46f), "c39b59");
        MakeBox("WovenRug", new(3.8f, 0.025f, 2.25f), new(-1.25f, 0.025f, 0.4f), "714939", collision: false, surface: "fabric");
        MakeBox("RugStripeA", new(3.8f, 0.032f, 0.16f), new(-1.25f, 0.04f, 0.05f), "8d765c", collision: false, surface: "fabric");
        MakeBox("RugStripeB", new(3.8f, 0.032f, 0.16f), new(-1.25f, 0.04f, 0.72f), "45615a", collision: false, surface: "fabric");
        MakeBox("WallShelf", new(2.2f, 0.12f, 0.45f), new(-3.85f, 2.05f, -4.6f), "503b2c", surface: "wood");
        MakeBox("CurtainLeft", new(0.42f, 1.55f, 0.05f), new(2.05f, 2.1f, -4.78f), "8d806f", collision: false);
        MakeBox("CurtainRight", new(0.42f, 1.55f, 0.05f), new(3.05f, 2.1f, -4.78f), "8d806f", collision: false);
        MakeInteractionBox(
            "OldPc",
            new(1.4f, 1.15f, 0.72f),
            new(0, 1.46f, -3.68f),
            "3b403c",
            "urman.chapter1:interaction/oldpc-power",
            "Включить старый компьютер");
        var oldPc = GeneratedModularKitDressing.AttachPresentationOnly(
            this,
            "style-house-old-pc",
            ["OldPc_"],
            new(0f, 1.46f, -3.68f),
            uniformScale: 1.0f,
            yawDegrees: 0f);
        oldPc.Name = "GeneratedOldPcAct1";
        oldPc.SetMeta("stylePresentationModule", "OldPc_project_original");
        SetMeta("styleImportedModules", "OldPc_project_original");
        MakeCrtHeroDetail();
        MakeBox("LampStem", new(0.08f, 0.95f, 0.08f), new(2.15f, 1.48f, -3.25f), "6c573e", collision: false);
        MakeBox("LampShade", new(0.72f, 0.42f, 0.72f), new(2.15f, 2.0f, -3.25f), "b98a53", collision: false);
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
        AttachAct1Npc(
            "gulsina",
            "Gulsina",
            "family keeper",
            new(-3.2f, 0, -2.8f),
            yawDegrees: 28f);
        MakeInteractionBox(
            "GulsinaNpc",
            new(0.62f, 1.68f, 0.46f),
            new(-3.2f, 0.84f, -2.8f),
            "6f6257",
            "urman.chapter1:interaction/talk-gulsina",
            "Поговорить с әби",
            dialogueId: "urman.chapter1:dialogue/gulsina_yaramyy");
        MakeInteractionBox(
            "HouseExit",
            new(1.3f, 2.1f, 0.22f),
            new(0, 1.05f, 4.82f),
            "685848",
            "urman.chapter1:interaction/house-to-route",
            "Выйти на улицу",
            "village_day",
            "from_house");
        var rinatSeat = MakeBox(
            "RinatAbsentChairSeat",
            new(0.9f, 0.12f, 0.8f),
            new(3.15f, 0.52f, -2.82f),
            "684b37",
            collision: false,
            surface: "wood");
        rinatSeat.SetMeta("act1CharacterCue", "rinat-absent-presence");
        var rinatCoat = MakeBox(
            "RinatAbsentCoat",
            new(0.92f, 1.2f, 0.1f),
            new(3.15f, 1.25f, -2.42f),
            "58656a",
            collision: false,
            surface: "fabric");
        rinatCoat.SetMeta("act1CharacterCue", "rinat-absent-presence");
        var rinatRadio = MakeBox(
            "RinatVoiceRadio",
            new(0.34f, 0.18f, 0.26f),
            new(3.15f, 1.03f, -2.15f),
            "313a37",
            collision: false,
            surface: "wood");
        rinatRadio.SetMeta("act1CharacterCue", "rinat-voice-anchor");
        MakeInteractionBox(
            "InternalRegisterToRinat",
            new(0.65f, 1.78f, 0.48f),
            new(3.15f, 0.89f, -2.45f),
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
            Position = new(2.15f, 2.1f, -3.25f),
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
            LightEnergy = 0.70f,
            OmniRange = 5.5f,
            ShadowEnabled = false
        });

        AddChild(new OmniLight3D
        {
            Name = "RoomFill",
            Position = new(0, 2.25f, 1.7f),
            LightColor = Color.FromHtml("958878"),
            LightEnergy = 0.52f,
            OmniRange = 6.0f,
            ShadowEnabled = false
        });

        AddChild(new OmniLight3D
        {
            Name = "CrtScreenGlow",
            Position = new(0, 1.5f, -3.0f),
            LightColor = Color.FromHtml("78a59a"),
            LightEnergy = 0.62f,
            OmniRange = 3.2f,
            ShadowEnabled = false
        });
    }

    private void BuildKaraUrmanNight()
    {
        MakeBox("Ground", new(38, 0.25f, 38), new(0, -0.125f, 0), "273126");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathNear", 3.8f, 13f, new(0f, 0f, 9f), "403c31");
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathMiddle", 3.6f, 11f, new(0.45f, 0f, -2.3f), "3b392f", yawDegrees: -5f);
        PainterlyEnvironmentDetails.AddRoadRelief(this, "PathFar", 3.3f, 10f, new(-0.1f, 0f, -12.3f), "37362e", yawDegrees: 7f);
        for (var index = 0; index < 30; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            var row = index / 2;
            var x = side * (4.2f + (row % 3) * 1.7f);
            var z = -13 + row * 2.1f;
            MakePine(new(x, 0, z), 5.4f + (index % 5) * 0.5f);
        }

        // The imported pines are deliberately offset from the repeated
        // procedural rows: one keeps the existing layer-2 proxy while the two
        // scaled/rotated variants remain visual-only, so walkability and
        // interaction-layer ownership stay unchanged.
        GeneratedModularKitDressing.Attach(
            this,
            "style-forest-pine",
            ["PineA_"],
            new(8.6f, 1.6f, -14.6f));
        AttachPresentationPine("GeneratedPineA_MidLeft", new(-8.2f, 1.44f, -5.8f), 0.90f, -18f);
        AttachPresentationPine("GeneratedPineA_NearRight", new(8.8f, 1.76f, 3.0f), 1.10f, 27f);
        SetMeta("styleImportedModules", "PineA_project_original");
        SetMeta("styleImportedPineInstances", 3);

        for (var index = 0; index < 7; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            PainterlyEnvironmentDetails.AddBirch(this, new Vector3(side * (5.8f + index % 3 * 1.4f), 0, -9 + index * 3.6f), 5.4f + index % 2 * 0.7f);
        }

        for (var index = 0; index < 4; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            PainterlyEnvironmentDetails.AddBirch(this, new Vector3(side * (7.4f + index * 0.8f), 0, -13.5f + index * 6.4f), 6.2f - index * 0.35f);
        }

        for (var index = 0; index < 18; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            MakeGrassTuft(new(side * (2.4f + index % 5 * 0.44f), 0.08f, 13 - index * 1.55f), 0.5f + index % 3 * 0.1f);
        }

        for (var index = 0; index < 18; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            AddShrub(new Vector3(side * (3.2f + index % 4 * 0.7f), 0, 11.5f - index * 1.55f), 0.82f + index % 3 * 0.16f, "2f4336");
        }

        // Break the repeated crown rhythm with ground-level material cues and
        // a restrained boundary motif. The forest should feel observed, not
        // decorated as a monster arena.
        MakeDisc("BoundaryStoneNear", new(0.9f, 0.16f, 0.72f), new(-2.15f, 0.08f, 4.75f), "656860", surface: "stone");
        // The owned PathNear relief dips beneath the boundary patch offsets;
        // seat the cluster against that authored surface rather than the
        // higher relief from another benchmark scene.
        PainterlyEnvironmentDetails.AddPuddleCluster(this, "BoundaryWetPatch", new(0.72f, 0.078f, 4.25f), new(0.68f, 1.18f), "4a585b", [-0.005065963f, -0.007065952f, -0.009065956f]);
        MakeFallenLog(new(-3.2f, 0.34f, 4.6f), 2.4f, new(0, -21, 70));
        MakeFallenLog(new(3.9f, 0.28f, -1.4f), 1.8f, new(0, 28, 74));
        MakeStump(new(-4.9f, 0.0f, 0.8f), 0.42f);
        MakeStump(new(4.7f, 0.0f, -4.6f), 0.34f);
        MakeBoundaryCharm(new(-1.9f, 0.0f, -7.6f), new(0.73f, 0.45f, 0.14f));
        MakeBoundaryCharm(new(2.05f, 0.0f, -7.4f), new(0.78f, 0.56f, 0.18f));
        MakeBranch(new(-4.4f, 3.2f, -5.2f), new(-2.0f, 2.5f, -7.0f), "40352f");
        MakeBranch(new(4.2f, 3.0f, -4.1f), new(2.2f, 2.25f, -6.4f), "3a302c");
        MakeFireflies();

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
    }

    private void BuildFapClinic()
    {
        MakeBox("Floor", new(12, 0.2f, 12), new(0, -0.1f, 0), "5b625d", surface: "wood");
        MakeBox("BackWall", new(12, 3.4f, 0.25f), new(0, 1.7f, -6), "7e837e", surface: "plaster");
        MakeBox("FrontWall", new(12, 3.4f, 0.25f), new(0, 1.7f, 6), "747d78", surface: "plaster");
        MakeBox("LeftWall", new(0.25f, 3.4f, 12), new(-6, 1.7f, 0), "7b8580", surface: "plaster");
        MakeBox("RightWall", new(0.25f, 3.4f, 12), new(6, 1.7f, 0), "77807a", surface: "plaster");
        MakeBox("ClinicCeiling", new(12, 0.18f, 12), new(0, 3.4f, 0), "7b8580", collision: false, surface: "plaster");
        MakeBox("ClinicCeilingFixtureHousing", new(1.35f, 0.12f, 0.52f), new(0, 3.22f, -0.5f), "65736d", collision: false);
        MakeBox("ClinicCeilingFixtureLens", new(0.88f, 0.025f, 0.22f), new(0, 3.145f, -0.5f), "9ca9a2", collision: false);
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
        // Two small reverse-view anchors: an enamel bin beside the sink and a
        // muted lower-wall paint band under the right window. Both are visual-only.
        MakeCylinder("FapClinicEnamelWasteBinBody", 0.22f, 0.44f, new(4.18f, 0.24f, -0.78f), "899890", "plaster");
        MakeCylinder("FapClinicEnamelWasteBinRim", 0.245f, 0.04f, new(4.18f, 0.48f, -0.78f), "b3bbb1", "plaster");
        MakeBox("FapClinicRightWallLowerBand", new(0.04f, 0.32f, 2.35f), new(5.84f, 0.74f, 1.25f), "7e8d83", collision: false, surface: "plaster");

        AttachAct1Npc(
            "naila",
            "Naila",
            "medical clerk",
            new(-4.0f, 0, -0.5f),
            yawDegrees: 8f);
        MakeInteractionBox(
            "NailaNpc",
            new(0.62f, 1.68f, 0.46f),
            new(-4.0f, 0.84f, -0.5f),
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
            "OfficialRecordToInternalRegister",
            new(1.3f, 2.1f, 0.25f),
            new(0, 1.05f, 5.82f),
            "625849",
            "urman.chapter1:interaction/official-to-internal-register",
            "Вернуться к старому компьютеру и сверить реестр",
            "house_old_pc",
            "entry");

        AddChild(new OmniLight3D
        {
            Name = "ColdCeilingLamp",
            Position = new(-0.2f, 2.8f, -2.8f),
            LightColor = Color.FromHtml("d0b49d"),
            LightEnergy = 1.45f,
            OmniRange = 6.6f,
            ShadowEnabled = false
        });
        AddChild(new OmniLight3D
        {
            Name = "FapWindowColdFill",
            Position = new(-4.7f, 2.0f, 1.25f),
            LightColor = Color.FromHtml("9bb8bc"),
            LightEnergy = 1.15f,
            OmniRange = 5.2f,
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
            component.SetMeta("interiorSilhouettes", "examination cot|folding privacy screen|wall medicine cabinet|enamel instrument trolley|waiting bench");
            component.SetMeta("meshCount", component.GetChildren().OfType<MeshInstance3D>().Count());
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
        MakeFence(-5.2f, 12, 25);
        for (var index = 0; index < 18; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            MakePine(new(side * (8.2f + index % 3), 0, 15 - index * 2.3f), 4.8f + index % 4 * 0.4f);
        }

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
        MakeRotatedBox("ZiratRouteTraceTag", new(0.62f, 0.16f, 0.035f), new(-3.55f, 0.72f, -5.8f), new(0, -8, -5), "8d765b", "wood");
        MakeInteractionBox(
            "ZiratRoadsideClue",
            new(0.9f, 1.35f, 0.7f),
            new(-3.55f, 0.72f, -5.8f),
            "8d765b",
            "urman.chapter1:interaction/zirat-roadside-clue",
            "Осмотреть след у зиратской дороги");

        MakeBox("ZiratFence", new(9, 1.25f, 0.18f), new(-6.2f, 0.62f, -5.8f), "575044", surface: "wood");
        MakeInteractionBox(
            "ZiratRoadToForest",
            new(2.6f, 1.5f, 0.3f),
            new(0, 0.75f, -19.5f),
            "39453b",
            "urman.chapter1:interaction/zirat-road-to-forest",
            "Подойти к кромке Кара-Урмана",
            "kara_urman_night",
            "village_path");
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

    private void MakeCrtHeroDetail()
    {
        MakeBox("CrtInnerScreen", new(0.74f, 0.43f, 0.018f), new(0, 1.54f, -3.305f), "6e968b", collision: false);
        for (var row = 0; row < 4; row++)
        {
            MakeBox($"CrtScanline{row}", new(0.62f, 0.012f, 0.01f), new(0, 1.41f + row * 0.085f, -3.292f), row == 2 ? "9db5a3" : "547e74", collision: false);
        }
        for (var column = 0; column < 3; column++)
        {
            MakeBox($"CrtVent{column}", new(0.035f, 0.42f, 0.02f), new(1.25f + (column - 1) * 0.11f, 1.58f, -3.315f), "343a35", collision: false);
        }
        PainterlyEnvironmentDetails.AddCable(this, new Vector3(1.58f, 0.88f, -3.7f), new Vector3(1.58f, 0.46f, -2.8f));
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
        AddChild(branch);
        branch.LookAt(end, Vector3.Up);
        branch.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
    }

    private void MakeFireflies()
    {
        var glow = new StandardMaterial3D
        {
            AlbedoColor = Color.FromHtml("d4c38b"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("9a8d5f"),
            EmissionEnergyMultiplier = 1.5f
        };
        var points = new[]
        {
            new Vector3(-2.7f, 1.35f, 2.2f), new Vector3(2.45f, 1.8f, 0.2f),
            new Vector3(-3.1f, 2.05f, -3.7f), new Vector3(3.2f, 1.25f, -5.5f)
        };
        for (var index = 0; index < points.Length; index++)
        {
            AddChild(new MeshInstance3D
            {
                Name = $"Firefly{index}",
                Position = points[index],
                Scale = Vector3.One * 0.045f,
                Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 6, Rings = 3 },
                MaterialOverride = glow
            });
        }
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
            Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.18f, Height = 0.22f, RadialSegments = 8 },
            MaterialOverride = Material("8d6b55", "plaster")
        });
        AddChild(new MeshInstance3D
        {
            Name = "TeaSurface",
            Position = position + new Vector3(0, 0.115f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.125f, BottomRadius = 0.125f, Height = 0.012f, RadialSegments = 8 },
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

        AddChild(body);
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

    private static void SuppressPrimitiveVisual(StaticBody3D body, string reason)
    {
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

        var npc = GeneratedCharacterKitDressing.Attach(host, characterId, prefix, anchor);
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

    private void MakeInteractionBox(
        string name,
        Vector3 size,
        Vector3 position,
        string color,
        string interactionId,
        string prompt,
        string targetZoneId = "",
        string targetSpawnPointId = "entry",
        string dialogueId = "",
        string documentId = "")
    {
        var body = new InteractionTarget
        {
            Name = name,
            Position = position,
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
    }

    private static Material Material(string htmlColor, string surface = "") =>
        PainterlyMaterialLibrary.ForColor(htmlColor, surface);
}
