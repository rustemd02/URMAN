using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string BathhouseObservationInteraction = "urman.chapter1:interaction/bathhouse-condensation-observation";
    private const string BathStoveKey = "bathhouse/stove";
    private const string BathSteamKey = "bathhouse/steam";
    private Node3D? _bathhouse;
    private Node3D? _bathCondensation;
    private MeshInstance3D? _bathEmbers;
    private MeshInstance3D? _bathWater;
    private Node3D? _bathPurchasedMatches;
    private OmniLight3D? _bathFireLight;
    private CpuParticles3D? _bathSteam;
    private CpuParticles3D? _bathSmoke;
    private InteractionTarget? _bathStoveTarget;
    private InteractionTarget? _bathWaterTarget;
    private InteractionTarget? _bathVentTarget;
    private AudioStreamWav? _bathSteamSound;
    private bool _bathIgnitionPending;
    internal bool BathIgnitionInProgress => _bathIgnitionPending;

    /// <summary>Only the small dwelling in the existing banya parcel is replaced.</summary>
    private void BuildBathhouse()
    {
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        var parcel = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation/BabaiYardAuthoredParcels/BabaiYardWestDepthBanyaYardParcel");
        var dwelling = FindDescendants<Node3D>(parcel).Single(node => node.Name == "VillageParcel_VariantC_BanyaYard_Dwelling");
        HidePresentationNode(dwelling);
        dwelling.SetMeta("suppressionReason", "accessible banya in the existing yard barn footprint; parcel, shed and fence retained");
        var oldBarn = core.GetNode<Node3D>("BabaiEbiYard/BabaiYardBarnVolume");
        _bathhouse = new Node3D { Name = "BabaiBathhouse", Rotation = oldBarn.GlobalRotation };
        core.AddChild(_bathhouse);
        _bathhouse.GlobalPosition = oldBarn.GlobalPosition;
        var low = float.PositiveInfinity;
        var high = float.NegativeInfinity;
        for (var x = -2; x <= 2; x++) for (var z = -3; z <= 3; z++)
        {
            var point = _bathhouse.ToGlobal(new(x, 0, z * .84f));
            var y = AgentBAct1HeightField.CollisionGround(point.X, point.Z);
            low = Math.Min(low, y); high = Math.Max(high, y);
        }
        _bathhouse.GlobalPosition = _bathhouse.GlobalPosition with { Y = high + .18f };
        _bathhouse.SetMeta("sourceKey", "act1/babai/bathhouse");
        _bathhouse.SetMeta("replacedPresentation", dwelling.GetPath().ToString());
        _bathhouse.SetMeta("preservedFootprintOwner", oldBarn.GetPath().ToString());
        _bathhouse.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        _bathhouse.SetMeta("groundContactPolicy", "floor above full sampled terrain footprint; foundation embedded to lowest sampled ground");
        RegisterAddressInheritedBuilding(_bathhouse, "act1/babai/bathhouse", "ADR-BABAI", "bathhouse");

        var footingDepth = high - low + .24f;
        FacilitySolid(_bathhouse, "BathStoneFoundation", new(4.18f, footingDepth, 5.18f), new(0, -footingDepth * .5f - .015f, 0), "67665b", "stone");
        FacilitySolid(_bathhouse, "BathFloor", new(4, .06f, 5), new(0, -.03f, 0), "5a4634", "wood_furniture");
        FacilitySolid(_bathhouse, "BathCeiling", new(4, .10f, 5), new(0, 2.60f, 0), "2c231b", "wood");
        FacilitySolid(_bathhouse, "BathWestWall", new(.20f, 2.60f, 5), new(-2, 1.30f, 0), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathFrontWall", new(4, 2.60f, .20f), new(0, 1.30f, 2.5f), "3a2e24", "wood");
        // Facilities03: the former outward arc hit the existing firewood
        // shelter's Post. Shift only this opening 18 cm along the same wall
        // and hang the full-width leaf on its opposite jamb. Preserve the
        // shelter, useful logs, footprint, landing and 98-degree outward arc.
        FacilitySolid(_bathhouse, "BathEastWallBack", new(.20f, 2.60f, 3.48f), new(2, 1.30f, -.76f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathEastWallFront", new(.20f, 2.60f, .27f), new(2, 1.30f, 2.365f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathEastDoorLintel", new(.20f, .48f, 1.25f), new(2, 2.36f, 1.605f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathRearBelowWindow", new(4, 1.225f, .20f), new(0, .6125f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathRearAboveWindowLow", new(4, .05f, .20f), new(0, 1.90f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathRearAboveWindowHigh", new(4, .435f, .20f), new(0, 2.3825f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathVentWallLeft", new(.54f, .24f, .20f), new(-1.73f, 2.045f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathVentWallRight", new(3.14f, .24f, .20f), new(.43f, 2.045f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathRearWindowLeft", new(2.275f, .65f, .20f), new(-.8625f, 1.55f, -2.5f), "3a2e24", "wood");
        FacilitySolid(_bathhouse, "BathRearWindowRight", new(.975f, .65f, .20f), new(1.5125f, 1.55f, -2.5f), "3a2e24", "wood");
        FacilityWindow(_bathhouse, "BathWindow", new(.65f, 1.55f, -2.49f), new(.75f, .65f));
        // The narrow board courses follow the real walls and stop at openings.
        for (var course = 1; course < 13; course++)
        {
            var y = course * .20f;
            AddVisualBox(_bathhouse, "BathWestBoardSeam" + course, new(.012f, .008f, 4.95f), new(-2.106f, y, 0), "211a14", "wood");
            AddVisualBox(_bathhouse, "BathFrontBoardSeam" + course, new(3.95f, .008f, .012f), new(0, y, 2.606f), "211a14", "wood");
        }
        var roof = AddVisualPitchedRoof(_bathhouse, "BathMetalRoof", 4.12f, 5.12f, 2.52f, .58f, .20f, "636a66");
        roof.MaterialOverride = PainterlyMaterialLibrary.ForColor("636a66", "metal");
        var snow = AddVisualPitchedRoof(_bathhouse, "BathRoofSettledSnow", 4.12f, 5.12f, 2.545f, .58f, .20f, "dce4e5");
        snow.MaterialOverride = PainterlyMaterialLibrary.ForColor("dce4e5", "snow_ground");
        core.GetNode<AgentBAct1ExteriorLayer>("AgentBExteriorWorld").ReconcileBuildingFoliage(_bathhouse);
        foreach (var x in new[] { -2.09f, 2.09f })
            AddVisualBox(_bathhouse, "BathEave" + x, new(.11f, .17f, 5.43f), new(x, 2.61f, 0), "2e251d", "wood");
        foreach (var z in new[] { -2.48f, 2.48f })
        foreach (var x in new[] { -1.90f, 1.90f })
            AddVisualBox(_bathhouse, $"BathCornerPost{x}_{z}", new(.16f, 2.55f, .17f), new(x, 1.275f, z), "33281f", "wood");

        FacilitySolid(_bathhouse, "BathPartitionLeft", new(2, 2.55f, .14f), new(-1, 1.275f, .35f), "4b3b2c", "wood_furniture");
        FacilitySolid(_bathhouse, "BathPartitionRight", new(.90f, 2.55f, .14f), new(1.55f, 1.275f, .35f), "4b3b2c", "wood_furniture");
        FacilitySolid(_bathhouse, "BathPartitionLintel", new(1.10f, .50f, .14f), new(.55f, 2.30f, .35f), "4b3b2c", "wood_furniture");
        var entrance = FacilityManualDoor(_bathhouse, "BathEntrance", "bathhouse/entrance", new(2.015f, 0, 2.13f), 1.10f, 2.10f, 180, -98);
        entrance.GeometryVersion = 2;
        entrance.LegacyAngleProjection = angle => Mathf.Pi - angle;
        entrance.Hinge.SetMeta("geometryRepair", "Facilities03: full sweep hit BabaiFirewoodShelter/Post; opening +0.18m Z, opposite hinge, same width and outward travel; old props angle maps pi-angle");
        // Facilities05: a hinge inside the partition made the first full-shape
        // sweep touch its right jamb. Mount on the wet face: the entire leaf
        // remains at Z <= .2725, 7.5 mm clear of the wall beginning at .28.
        // Width, closed/open angles and saved angle interpretation stay intact.
        var wetDoor = FacilityManualDoor(_bathhouse, "BathWetRoomDoor", "bathhouse/wet-door", new(0, 0, .24f), 1.10f, 2.0f, 90, 95);
        wetDoor.Hinge.SetMeta("geometryRepair", "Facilities05: BathPartitionRight first-sample contact; hinge Z .35 -> .24 on wet face; unchanged 90/185-degree saved angles, full leaf and 2mm sweep margin");
        entrance.Hinge.SetMeta("addressAccessPoint", true);
        BathEntrySteps();
        FacilityBench(_bathhouse, "BathChangingBench", new(-1.39f, 0, 1.22f), 1.28f, 90);
        AddVisualBox(_bathhouse, "BathCoatRail", new(1.42f, .10f, .055f), new(-.98f, 1.85f, 2.375f), "4a3a2b", "wood");
        for (var i = 0; i < 4; i++) FacilityRod(_bathhouse, "BathClothesHook" + i,
            new(-1.50f + .35f * i, 1.86f, 2.33f), new(-1.50f + .35f * i, 1.76f, 2.25f), .013f, "686b61");
        AddVisualBox(_bathhouse, "BathDryTowel", new(.36f, .69f, .018f), new(-1.1f, 1.48f, 2.28f), "c8baa0", "cloth_towel");
        FacilityLamp(_bathhouse, "BathChangingLamp", new(.06f, 2.24f, 2.21f), "f8d399", .38f, 3.3f);
        FacilityLamp(_bathhouse, "BathShieldedWetLamp", new(1.73f, 2.12f, -.56f), "ebbd79", .28f, 3.6f);
        for (var slat = 0; slat < 4; slat++)
            AddVisualBox(_bathhouse, "BathLampGuardSlat" + slat, new(.025f, .35f, .045f), new(1.48f + slat * .11f, 2.09f, -.40f), "4a3b2c", "wood");
        FacilityBench(_bathhouse, "BathWashBench", new(1.37f, 0, -1.44f), 1.65f, 90);
        FacilityVessel(_bathhouse, "BathWashBasin", new(1.37f, .56f, -1.88f), .22f, .17f, "b1b8b0", false);
        var bucket = FacilityVessel(_bathhouse, "BathWaterBucket", new(-.82f, .30f, 1.91f), .23f, .42f, "969e98", true);
        _bathWater = bucket.GetNode<MeshInstance3D>("Water");
        FacilityRod(bucket, "BucketHandleLeft", new(-.23f, .11f, 0), new(-.15f, .35f, 0), .009f, "696f65");
        FacilityRod(bucket, "BucketHandleTop", new(-.15f, .35f, 0), new(.15f, .35f, 0), .009f, "696f65");
        FacilityRod(bucket, "BucketHandleRight", new(.15f, .35f, 0), new(.23f, .11f, 0), .009f, "696f65");
        FacilitySolid(_bathhouse, "BathWaterBucketRest", new(.57f, .085f, .52f), new(-.82f, .045f, 1.91f), "45372a", "wood");
        FacilityVessel(_bathhouse, "BathLadleBowl", new(-.49f, .17f, 2.06f), .10f, .055f, "b0b5a7", false);
        FacilityRod(_bathhouse, "BathLadleHandle", new(-.49f, .17f, 2.05f), new(-.31f, .48f, 2.30f), .022f, "997b50");
        BuildBathStove();
        BuildBathCondensation();
        BuildBathAtmosphere();

        _bathStoveTarget = BathLocalTarget("BathStoveUse", "stove", "Растопить печь сухим поленом", new(-1.37f, .57f, -.51f), new(.58f, .38f, .12f), FireBathStove);
        _bathWaterTarget = BathLocalTarget("BathSteamUse", "water", "Поддать воды на камни", new(-1.34f, 1.06f, -1.03f), new(.74f, .18f, .68f), PourBathWater);
        _bathVentTarget = BathLocalTarget("BathVentUse", "vent", "Открыть небольшую отдушину", new(-1.30f, 2.045f, -2.365f), new(.34f, .26f, .06f), ToggleBathVent);
        BathLocalTarget("BathBucketFillUse", "bucket", "Долить воды из бачка", new(-.82f, .49f, 1.91f), new(.50f, .27f, .49f), RefillBathWater);
        // The visible covered water tank makes a refill finite and physically legible.
        FacilitySolid(_bathhouse, "BathCoveredWaterTank", new(.55f, .77f, .50f), new(.04f, .385f, 2.07f), "8b9590", "metal");
        AddVisualBox(_bathhouse, "BathTankLid", new(.60f, .035f, .54f), new(.04f, .79f, 2.07f), "747e78", "metal");
        FacilityRod(_bathhouse, "BathTankTap", new(-.26f, .16f, 2.07f), new(-.43f, .16f, 1.93f), .018f, "969f95");
        FacilityTarget("BathCondensationRead", BathhouseObservationInteraction, "Рассмотреть запотевшее стекло", _bathhouse,
            new(.65f, 1.55f, -2.465f), new(.67f, .59f, .04f));
        EnsureFacilityTick();
    }

    private void BathEntrySteps()
    {
        if (_bathhouse is null) return;
        // Carry15 identified the old loose log at (-32.173938, 4.706752)
        // beneath the former straight run. Keep its terrain support and pickup
        // envelope intact: turn only the new lower flight along the clear yard
        // side. The building, door and state IDs do not move.
        const float centerX = 2.54f;
        const float landingNearZ = 1.18f;
        const float landingEndZ = 2.30f;
        var approach = _bathhouse.ToGlobal(new(centerX, 0, 3.90f));
        var rise = Math.Max(.12f, _bathhouse.GlobalPosition.Y - AgentBAct1HeightField.CollisionGround(approach.X, approach.Z));
        var count = Math.Max(1, Mathf.CeilToInt(rise / .17f));
        void Tread(string name, Vector3 size, Vector3 at, float top)
        {
            var bottom = float.PositiveInfinity;
            foreach (var dx in new[] { -size.X * .5f, 0, size.X * .5f })
            foreach (var dz in new[] { -size.Z * .5f, 0, size.Z * .5f })
            {
                var world = _bathhouse.ToGlobal(at + new Vector3(dx, 0, dz));
                bottom = Math.Min(bottom, AgentBAct1HeightField.CollisionGround(world.X, world.Z) - _bathhouse.GlobalPosition.Y - .05f);
            }
            var height = Math.Max(.08f, top - bottom);
            FacilitySolid(_bathhouse, name, new(size.X, height, size.Z), new(at.X, top - height * .5f, at.Z), "8c7d64", "wood_furniture");
        }
        Tread("BathEntryLanding", new(1.12f, 0, landingEndZ - landingNearZ), new(centerX, 0, (landingNearZ + landingEndZ) * .5f), 0);
        // Facilities04: the fully opened leaf crosses the original narrow
        // landing. Give its free tip a supported outside route. Keep the old
        // landing's near edge by the loose log, and stop the new side below
        // the firewood shelter's eave instead of burying that existing object.
        // Carry20 identified the original buried axe, not a movable fixture:
        // its full local box ends at Z1.4927 and the surrounding snow mound at
        // most Z1.548. Trim only the surplus near edge of this new extension.
        // The previously walked centres, far edge and lower treads stay put.
        const float bypassNearZ = 1.57f;
        Tread("BathEntryLandingBypass", new(1.00f, 0, landingEndZ - bypassNearZ),
            new(3.60f, 0, (bypassNearZ + landingEndZ) * .5f), 0);
        for (var step = 1; step < count; step++)
            Tread("BathEntryTread" + step, new(2.12f, 0, .35f), new(3.04f, 0, landingEndZ + (step - .5f) * .32f), -rise * step / count);
        var first = _bathhouse.ToGlobal(new(centerX, 0, landingEndZ + (count - 1) * .32f + .55f));
        first.Y = AgentBAct1HeightField.CollisionGround(first.X, first.Z) + .04f;
        _bathhouse.SetMeta("entryApproach", first);
        _bathhouse.SetMeta("entryTurn", _bathhouse.ToGlobal(new(centerX, 0, 1.57f)));
        _bathhouse.SetMeta("entryClearDoor", _bathhouse.ToGlobal(new(centerX, 0, 2.95f)));
        _bathhouse.SetMeta("entryDoorBypass", new Vector3[]
        {
            _bathhouse.ToGlobal(new(3.70f, 0, 2.95f)),
            _bathhouse.ToGlobal(new(3.70f, 0, 1.84f)),
            _bathhouse.ToGlobal(new(3.14f, 0, 1.84f))
        });
        _bathhouse.SetMeta("entryThreshold", _bathhouse.ToGlobal(new(2.02f, 0, 1.40f)));
        _bathhouse.SetMeta("entryRepair", "Carry15: lower flight avoids the old loose log. Facilities04: same landing and route around the fully open leaf. Carry20: trim only new extension near edge Z1.43 -> 1.57, retaining X3.10..4.10/farZ2.30 and all walked centres; original axe and surrounding snow stay outside its complete solid. Old shelter, logs, wider lower treads and door geometry/version unchanged; native traversal required.");
    }

    private void BuildBathStove()
    {
        var bath = _bathhouse!;
        FacilitySolid(bath, "BathStoveHearth", new(1.22f, .025f, 1.32f), new(-1.33f, .013f, -1.09f), "827866", "stone");
        FacilitySolid(bath, "BathStoveRearHeatShield", new(.032f, 1.55f, 1.22f), new(-1.875f, .81f, -1.09f), "34312c", "metal");
        foreach (var x in new[] { -1.61f, -1.05f }) foreach (var z in new[] { -1.31f, -.85f })
            FacilitySolid(bath, $"BathStoveLeg{x}_{z}", new(.065f, .19f, .065f), new(x, .12f, z), "403f36", "metal");
        FacilitySolid(bath, "BathStoveFirebox", new(.71f, .63f, .66f), new(-1.33f, .525f, -1.09f), "494c44", "metal");
        FacilitySolid(bath, "BathStoneTray", new(.82f, .16f, .73f), new(-1.33f, .90f, -1.09f), "5e6057", "metal");
        for (var stone = 0; stone < 9; stone++)
        {
            var mesh = new MeshInstance3D { Name = "BathHeaterStone" + stone,
                Mesh = new SphereMesh { Radius = .115f, Height = .16f, RadialSegments = 8, Rings = 4 },
                Position = new(-1.57f + (stone % 3) * .235f, 1.00f + (stone % 2) * .025f, -1.31f + (stone / 3) * .22f),
                MaterialOverride = PainterlyMaterialLibrary.ForColor(stone % 2 == 0 ? "3f3d37" : "55524a", "stone", sheltered: true) };
            bath.AddChild(mesh);
        }
        AddVisualBox(bath, "BathFireboxDoorFrame", new(.50f, .34f, .035f), new(-1.33f, .57f, -.74f), "333a32", "metal");
        _bathEmbers = AddVisualBox(bath, "BathFireboxEmbers", new(.39f, .22f, .018f), new(-1.33f, .57f, -.715f), "be713c", "metal");
        _bathEmbers.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("a3572c"), EmissionEnabled = true,
            Emission = new Color("d67a31"), EmissionEnergyMultiplier = .55f, Roughness = 1 };
        for (var bar = 0; bar < 6; bar++) AddVisualBox(bath, "BathFireboxGrille" + bar, new(.018f, .25f, .015f), new(-1.495f + bar * .067f, .57f, -.702f), "383c33", "metal");
        FacilityRod(bath, "BathFireboxHandle", new(-1.05f, .47f, -.69f), new(-1.05f, .65f, -.69f), .024f, "4b4d41");
        AddVisualBox(bath, "BathAshPan", new(.42f, .11f, .075f), new(-1.33f, .29f, -.72f), "55594d", "metal");
        FacilityRod(bath, "BathChimney", new(-1.58f, .99f, -1.31f), new(-1.58f, 3.88f, -1.31f), .095f, "2e2c28");
        DiscoveryCylinder(bath, "BathChimneyWeatherCap", .16f, .12f, .05f, new(-1.58f, 3.93f, -1.31f), "666c65");
        FacilitySolid(bath, "BathStoveWoodGuard", new(.065f, .73f, 1.16f), new(-.71f, .39f, -1.09f), "4d3c2b", "wood_furniture");
        for (var i = 0; i < 5; i++)
        {
            var a = new Vector3(-1.79f + (i % 3) * .17f, .12f + (i / 3) * .17f, .74f);
            FacilityRod(bath, "BathDryFirewood" + i, a, a + new Vector3(0, 0, .55f), .075f, "987344");
        }
        FacilitySolid(bath, "BathWoodRackBottom", new(.64f, .065f, .67f), new(-1.54f, .037f, 1.03f), "3d3024", "wood");
        FacilitySolid(bath, "BathMatchShelf", new(.42f, .025f, .18f), new(-1.86f, 1.37f, .89f), "45372a", "wood");
        AddVisualBox(bath, "BathFamilyMatchbox", new(.075f, .016f, .052f), new(-1.80f, 1.3905f, .91f), "ccbd94", "paper");
        AddVisualBox(bath, "BathMatchboxStriker", new(.076f, .010f, .004f), new(-1.80f, 1.3905f, .938f), "66513a", "paper");
        FacilityLabel(bath, "BathMatchboxPrint", "СПИЧКИ", new(-1.80f, 1.393f, .941f), 0, .00026f);
        _bathPurchasedMatches = new Node3D { Name = "BathPurchasedMatchbox", Visible = false };
        bath.AddChild(_bathPurchasedMatches);
        _bathPurchasedMatches.SetMeta("presentationSource", "shop/used/matches");
        AddVisualBox(_bathPurchasedMatches, "Box", new(.075f, .016f, .052f), new(-1.80f, 1.3905f, .838f), "c7ae83", "paper");
        AddVisualBox(_bathPurchasedMatches, "Striker", new(.076f, .010f, .004f), new(-1.80f, 1.3905f, .866f), "66513a", "paper");
        FacilityLabel(_bathPurchasedMatches, "Print", "СПИЧКИ", new(-1.80f, 1.393f, .869f), 0, .00026f);
        _bathFireLight = new OmniLight3D { Name = "BathFireLight", Position = new(-1.33f, .64f, -.63f), LightColor = new Color("eea354"),
            LightEnergy = .18f, OmniRange = 1.45f, ShadowEnabled = false, Visible = false };
        bath.AddChild(_bathFireLight);
        _facilityLights.Add(_bathFireLight);
        _bathSteam = BathMist("BathSteam", new(-1.32f, 1.08f, -1.04f), 28, 2.6f, .21f, new Color(.81f, .82f, .75f, .18f));
        _bathSmoke = BathMist("BathChimneySmoke", new(-1.58f, 3.95f, -1.31f), 12, 4f, .32f, new Color(.44f, .45f, .43f, .22f));
    }

    private void BuildBathCondensation()
    {
        var bath = _bathhouse!;
        _bathCondensation = new Node3D { Name = "BathCondensation", Visible = false };
        bath.AddChild(_bathCondensation);
        var glassFilm = AddVisualBox(_bathCondensation, "WindowMoisture", new(.68f, .59f, .003f), new(.65f, 1.55f, -2.477f), "c5d1cb", "glass");
        glassFilm.MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.71f, .77f, .74f, .26f), Roughness = .32f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        for (var drop = 0; drop < 26; drop++)
        {
            var x = .34f + (drop * .137f) % .62f;
            var y = 1.29f + (drop * .191f) % .53f;
            var streak = AddVisualBox(_bathCondensation, "CondensationTrail" + drop, new(.005f, .017f + drop % 4 * .014f, .003f), new(x, y, -2.473f), "a6bdb5", "glass");
            streak.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        foreach (var x in new[] { -1.495f, -1.105f })
            AddVisualBox(bath, "BathVentFrameSide" + x, new(.04f, .35f, .035f), new(x, 2.045f, -2.383f), "594e3d", "wood");
        foreach (var y in new[] { 1.89f, 2.20f })
            AddVisualBox(bath, "BathVentFrameRail" + y, new(.43f, .04f, .035f), new(-1.30f, y, -2.383f), "594e3d", "wood");
        var slider = AddVisualBox(bath, "BathVentSlider", new(.34f, .26f, .025f), new(-1.30f, 2.045f, -2.355f), "9c825d", "wood");
        FacilityRod(slider, "BathVentFingerGrip", new(-.10f, -.025f, .025f), new(.10f, -.025f, .025f), .013f, "71654f");
        AddVisualBox(bath, "BathVentWeatherHood", new(.44f, .055f, .30f), new(-1.30f, 2.235f, -2.61f), "656e64", "metal");
    }

    private CpuParticles3D BathMist(string name, Vector3 at, int amount, float lifetime, float size, Color color)
    {
        var radial = new GradientTexture2D { Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 1), Gradient = new Gradient
            { Colors = [Colors.White, new Color(1, 1, 1, 0)], Offsets = [0, 1] } };
        var material = new StandardMaterial3D { AlbedoColor = color, AlbedoTexture = radial, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1 };
        var scale = new Curve(); scale.AddPoint(new(0, .25f)); scale.AddPoint(new(1, 1.6f));
        var particles = new CpuParticles3D { Name = name, Position = at, Emitting = false, Amount = amount, Lifetime = lifetime,
            Mesh = new QuadMesh { Size = new(size, size), Material = material }, LocalCoords = true,
            Direction = Vector3.Up, Spread = 12, Gravity = new(0, .015f, 0), InitialVelocityMin = .22f, InitialVelocityMax = .32f,
            ScaleAmountMin = .8f, ScaleAmountMax = 1.1f, ScaleAmountCurve = scale,
            ColorRamp = new Gradient { Offsets = [0, .15f, .72f, 1], Colors = [new Color(1, 1, 1, 0), Colors.White, Colors.White, new Color(1, 1, 1, 0)] },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _bathhouse!.AddChild(particles);
        return particles;
    }

    private InteractionTarget BathLocalTarget(string name, string key, string prompt, Vector3 at, Vector3 size, Func<Task> action)
    {
        var target = FacilityTarget(name, "urman.chapter1:local/bathhouse/" + key, prompt, _bathhouse!, at, size);
        // Keep the ray present across the save event. BathPlayerInside and
        // CommitFacilityProps own the transient re-entry guard.
        target.PresentationRepeatAvailable = () => FacilityExteriorActive;
        target.PresentationRepeat = () => _ = action();
        return target;
    }

    private bool BathPlayerInside()
    {
        if (_facilityBusy || _bathIgnitionPending || _runtimeBridge is null || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return false;
        return FacilityInteriorAt(player.GlobalPosition) == "bathhouse";
    }

    internal bool CanIgniteBathStove() => BathStoveIgnitionAvailable(out _);

    internal bool CanUseBathhouseMatches() => CanIgniteBathStove()
        && _runtimeBridge?.HasPocketShopItem("matches") == true;

    private bool BathStoveIgnitionAvailable(out string reason)
    {
        reason = "Нужно подойти к топке в бане.";
        if (_facilityBusy || _bathhouse is null || _bathStoveTarget is null
            || _runtimeBridge?.SessionIdentity is null || !FacilityExteriorActive
            || _runtimeBridge.CapturePlayTimeBlocks() != RuntimeBridge.PlayTimeBlock.None
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder
            || FacilityInteriorAt(player.GlobalPosition) != "bathhouse" || _bathhouse.ToLocal(player.GlobalPosition).Z >= .30f)
            return false;
        if (GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { HeldItem: null, ActionInProgress: false })
        { reason = "Сначала поставьте предмет и освободите руки."; return false; }
        var camera = GetViewport().GetCamera3D();
        // The legacy village-main-road node owns live ray targets but is
        // intentionally hidden by ApplyActiveZone. The real stove is rendered
        // under the connected bathhouse, so visibility must follow that owner.
        if (camera is null || !_bathhouse.GetNode<MeshInstance3D>("BathStoveFirebox").IsVisibleInTree()
            || !_bathStoveTarget.IsAvailable()
            || camera.GlobalPosition.DistanceTo(_bathStoveTarget.GlobalPosition) > 2.7f) return false;
        var stoveExclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var stoveExcludeOwner = (global::Godot.Collections.Array)stoveExclude;
        using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, _bathStoveTarget.GlobalPosition, 3,
            stoveExclude);
        ray.HitFromInside = true;
        using var stoveHit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (stoveHit.Count != 0) return false;
        // This is a live action precondition, independent of the cached visual
        // projection. The kernel repeats the resource checks when it commits.
        var props = _runtimeBridge.SelectWorldProps();
        if (FacilityNumber(props, BathStoveKey, "burnUntil") > _runtimeBridge.PlayTimeSeconds)
        { reason = "Полено ещё горит; добавлять дрова пока не нужно."; return false; }
        if (FacilityNumber(props, BathStoveKey, "logsRemaining", 5) < 1)
        { reason = "Сухие поленья на полке закончились. Остаточное тепло ещё держится после топки."; return false; }
        reason = string.Empty;
        return true;
    }

    private void RecordBathIgnitionResult(string result)
    {
        if (_bathStoveTarget is { } target && IsInstanceValid(target)) target.SetMeta("lastIgnitionResult", result);
    }

    private async Task FireBathStove()
    {
        if (_bathStoveTarget is { } target && IsInstanceValid(target))
            target.SetMeta("lastIgnitionActionNumber", target.GetMeta("lastIgnitionActionNumber", 0).AsInt32() + 1);
        RecordBathIgnitionResult("received");
        if (_bathIgnitionPending || _runtimeBridge is not { } bridge
            || bridge.SessionIdentity is not { } session)
        { RecordBathIgnitionResult("pending-or-session-unavailable"); return; }
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (!BathStoveIgnitionAvailable(out var reason))
        {
            RecordBathIgnitionResult("physical-or-resource-refusal: " + reason);
            player?.NotifyTraversal(reason);
            return;
        }
        _bathIgnitionPending = true;
        try
        {
            bool? purchased = false;
            if (bridge.HasPocketShopItem("matches")) purchased = await BathIgnitionChoiceUi.ChooseFor(this, bridge);
            if (purchased is not { } usePurchased || !IsInsideTree() || !ReferenceEquals(session, bridge.SessionIdentity))
            { RecordBathIgnitionResult("cancelled-or-session-changed"); return; }
            // The modal is already closed; the runtime checks the fresh physical
            // predicate and commits fuel, heat and optional custody in one plan.
            if (!await bridge.FireBathStoveAsync(usePurchased)
                || !IsInsideTree() || !ReferenceEquals(session, bridge.SessionIdentity))
            { RecordBathIgnitionResult("runtime-refused-or-session-changed"); return; }
            RefreshFacilityState();
            RecordBathIgnitionResult(usePurchased ? "committed-purchased-matches" : "committed-family-matches");
            UiFoley.PlayWorld(this, _bathhouse!.ToGlobal(new(-1.33f, .57f, -.72f)), "wood_tap");
            player?.NotifyTraversal(usePurchased
                ? "Полено занялось. Купленный коробок оставлен на полке; можно поддать воды на камни."
                : "Полено занялось от спичек с полки. Можно поддать немного воды на камни.");
        }
        catch (Exception error)
        {
            RecordBathIgnitionResult("exception: " + error.Message);
            GD.PushError("Bath ignition failed: " + error.Message);
            if (IsInsideTree() && ReferenceEquals(session, bridge.SessionIdentity)) player?.NotifyTraversal("Не удалось растопить печь. Можно попробовать ещё раз.");
        }
        finally { _bathIgnitionPending = false; }
    }

    private async Task PourBathWater()
    {
        if (!BathPlayerInside()) return;
        var now = _runtimeBridge!.PlayTimeSeconds;
        var water = FacilityNumber(_facilityProps, "bathhouse/water", "ladles", 8);
        var player = (FirstPersonController)GetTree().GetFirstNodeInGroup("player_controller");
        if (FacilityNumber(_facilityProps, BathStoveKey, "heatUntil") <= now) { player.NotifyTraversal("Камни холодные. Сначала нужно растопить печь."); return; }
        if (water < 1) { player.NotifyTraversal("Ведро пустое. В предбаннике осталась вода в закрытом бачке."); return; }
        if (FacilityNumber(_facilityProps, BathSteamKey, "until") > now) { player.NotifyTraversal("Пар ещё держится. Одного ковша достаточно."); return; }
        if (await CommitFacilityProps(new JsonArray {
            new JsonObject { ["propId"] = "bathhouse/water", ["ladles"] = water - 1 },
            new JsonObject { ["propId"] = BathSteamKey, ["until"] = now + (YardMechanism.Flag(_facilityProps, "bathhouse/vent", "open") ? 5 : 14), ["condensed"] = true } },
            "Вода зашипела на камнях. Стекло покрылось мелкими каплями; рядом отозвалась деревянная обшивка.",
            _bathhouse!.ToGlobal(new(-1.33f, 1.03f, -1.09f)), "hollow_board")) PlayBathSteam();
    }

    private async Task RefillBathWater()
    {
        if (!BathPlayerInside()) return;
        var water = FacilityNumber(_facilityProps, "bathhouse/water", "ladles", 8);
        if (water >= 8) { (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.NotifyTraversal("Воды в ведре достаточно."); return; }
        var tank = FacilityNumber(_facilityProps, "bathhouse/water", "tankLadles", 40);
        var fill = Math.Min(8 - water, tank);
        if (fill <= 0) { (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.NotifyTraversal("Бачок пуст. Для сегодняшней топки воды больше не осталось."); return; }
        await CommitFacilityProps(new JsonArray { new JsonObject { ["propId"] = "bathhouse/water", ["ladles"] = water + fill, ["tankLadles"] = tank - fill } },
            "Вода из бачка снова наполнила ведро.", _bathhouse!.ToGlobal(new(-.82f, .49f, 1.91f)), "metal_rattle");
    }

    private async Task ToggleBathVent()
    {
        if (!BathPlayerInside()) return;
        var open = YardMechanism.Flag(_facilityProps, "bathhouse/vent", "open");
        await CommitFacilityProps(new JsonArray {
            new JsonObject { ["propId"] = "bathhouse/vent", ["open"] = !open },
            new JsonObject { ["propId"] = BathSteamKey, ["until"] = 0, ["condensed"] = open && YardMechanism.Flag(_facilityProps, BathSteamKey, "condensed") } },
            open ? "Отдушина закрыта." : "Отдушина открыта. Пар рассеивается, капли на стекле высыхают.",
            _bathhouse!.ToGlobal(new(-1.30f, 2.045f, -2.36f)), "wood_tap");
    }

    private void ApplyBathhouseState()
    {
        if (_bathhouse is null) return;
        if (_bathCondensation is not null) _bathCondensation.Visible = YardMechanism.Flag(_facilityProps, BathSteamKey, "condensed");
        if (_bathWater is not null) _bathWater.Visible = FacilityNumber(_facilityProps, "bathhouse/water", "ladles", 8) > 0;
        if (_bathPurchasedMatches is not null) _bathPurchasedMatches.Visible = YardMechanism.Flag(_facilityProps, "shop/used/matches", "used");
        var logs = (int)Math.Clamp(FacilityNumber(_facilityProps, BathStoveKey, "logsRemaining", 5), 0, 5);
        for (var i = 0; i < 5; i++) _bathhouse.GetNode<Node3D>("BathDryFirewood" + i).Visible = i < logs;
        var vent = _bathhouse.GetNodeOrNull<Node3D>("BathVentSlider");
        if (vent is not null) vent.Position = new(YardMechanism.Flag(_facilityProps, "bathhouse/vent", "open") ? -.93f : -1.30f, 2.045f, -2.355f);
    }

    private void TickBathhouse(bool paused)
    {
        if (_bathhouse is null || _facilityBridge is null) return;
        var now = _facilityBridge.PlayTimeSeconds;
        var burn = FacilityNumber(_facilityProps, BathStoveKey, "burnUntil") > now;
        var hot = FacilityNumber(_facilityProps, BathStoveKey, "heatUntil") > now;
        var steam = FacilityNumber(_facilityProps, BathSteamKey, "until") > now;
        if (_bathEmbers is not null) _bathEmbers.Visible = hot;
        if (_bathFireLight is not null)
        {
            _bathFireLight.Visible = burn && FacilityExteriorActive;
            // W4/P4: .17f is this light's steady authored energy (it is built at
            // .18f but never rendered before this line runs; the only other
            // writer is the constructor). Re-pushing that same float every tick
            // cannot change the frame, so only a real difference is written.
            if (_bathFireLight.LightEnergy != .17f) _bathFireLight.LightEnergy = .17f;
        }
        if (_bathSteam is not null) { _bathSteam.Emitting = steam && FacilityExteriorActive && !paused; _bathSteam.Visible = FacilityExteriorActive; _bathSteam.SpeedScale = paused ? 0 : 1; }
        if (_bathSmoke is not null) { _bathSmoke.Emitting = burn && FacilityExteriorActive && !paused; _bathSmoke.Visible = FacilityExteriorActive; _bathSmoke.SpeedScale = paused ? 0 : 1; }
        TickBathAtmosphere(burn && !paused, GetProcessDeltaTime());
        if (_bathStoveTarget is not null) _bathStoveTarget.Prompt = burn ? "Проверить топку" : "Растопить печь сухим поленом";
        if (_bathWaterTarget is not null) _bathWaterTarget.Prompt = hot ? "Поддать воды на камни" : "Камни холодные — проверить печь";
        if (_bathVentTarget is not null) _bathVentTarget.Prompt = YardMechanism.Flag(_facilityProps, "bathhouse/vent", "open") ? "Закрыть отдушину" : "Открыть небольшую отдушину";
    }

    private void PlayBathSteam()
    {
        if (DisplayServer.GetName() == "headless" || _bathhouse is null) return;
        _bathSteamSound ??= CreateBathSteamSound();
        AudioSettingsService.EnsureBuses();
        var player = new WorldFoleyPlayer { Name = "BathWaterOnStones", Stream = _bathSteamSound, Bus = AudioSettingsService.SfxBus,
            VolumeDb = -18, UnitSize = 1.2f, MaxDistance = 7 };
        player.SetMeta("audioProvenance", "original deterministic noise synthesis in Act1ConnectedWorld.Bathhouse.cs; no source recording or voice");
        player.AddToGroup("world_foley");
        AddChild(player); player.GlobalPosition = _bathhouse.ToGlobal(new(-1.33f, 1.03f, -1.09f));
        player.Finished += () => player.QueueFree();
        player.RequestPlay(GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true });
    }

    private static AudioStreamWav CreateBathSteamSound()
    {
        const int rate = 22050;
        var frames = rate * 2;
        var bytes = new byte[frames * 2];
        var random = new Random(4129);
        double low = 0;
        for (var i = 0; i < frames; i++)
        {
            var t = (double)i / rate;
            var noise = random.NextDouble() * 2 - 1;
            low += .13 * (noise - low);
            var envelope = Math.Min(1, t / .035) * Math.Exp(-t * 1.7) * Math.Min(1, (2 - t) / .15);
            var value = (short)(Math.Clamp((noise - low * .65) * envelope * .40, -1, 1) * short.MaxValue);
            bytes[i * 2] = (byte)(value & 255); bytes[i * 2 + 1] = (byte)((value >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = bytes };
    }
}
