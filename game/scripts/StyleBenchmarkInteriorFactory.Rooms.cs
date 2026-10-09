using Godot;

namespace Urman.Godot;

/// <summary>
/// The house is a two-storey dwelling on the inside and an ordinary log house
/// on the outside. The 8x7 log room stays the zal (stove, tea table, old PC).
/// A door in its east wall carries the player, through a short fade, to the
/// house wing: kitchen, hall with the attic stair, bedroom, toilet and the
/// attic where Aidar sleeps. The wing is authored under the village
/// (<see cref="WingOrigin"/>) inside the same interior zone, sealed on all
/// sides; its windows are frosted daylight panels because nothing exists
/// outside them. Nothing here changes the exterior facade or its contacts.
/// </summary>
public static partial class StyleBenchmarkInteriorFactory
{
    public const float InnerDoorHeight = 2.05f;
    public const float WingCeiling = 2.60f;
    public const float AtticFloor = 2.80f;

    public static readonly Vector3 WingOrigin = new(0f, -30f, 0f);
    /// <summary>Zal side of the wing door: player arrives here, facing west.</summary>
    public static readonly Vector3 ZalFromWingArrival = new(3.0f, .05f, 2.75f);
    /// <summary>Wing kitchen arrival (wing-local), facing north.</summary>
    public static readonly Vector3 WingKitchenArrival = new(-3.2f, .05f, 2.25f);
    public static readonly Vector3 WingAtticArrival = new(-2.4f, AtticFloor + .05f, 2.9f);
    public static Vector3 WingKitchenSpawn => WingOrigin + WingKitchenArrival;
    public static Vector3 WingAtticSpawn => WingOrigin + WingAtticArrival;

    private const float ZalDoorZ = 2.75f;
    private const float StairBottomX = 3.3f, StairTopX = -1.4f, StairZ0 = 2.45f, StairZ1 = 3.45f;
    private const float HoleWest = -1.5f, HoleEast = 3.4f, HoleNorth = 2.4f;
    private static ulong _wingTravelUntil;
    private static Material? _frostPane;

    private static void BuildHouseRooms(Node3D room)
    {
        room.SetMeta("houseRooms", "zal(stove)|kitchen|hall|bedroom|toilet|attic");
        room.SetMeta("houseWingOrigin", WingOrigin);
        room.SetMeta("houseWingPolicy", "outside: ordinary log house; inside: separate larger space reached by a fade door");
        var wing = new Node3D { Name = "BabaiHouseWing", Position = WingOrigin };
        room.AddChild(wing);
        // The wing lies under the village terrain; its own floor holds the player.
        FirstPersonController.SealedInteriorVolume = point =>
        {
            if (!GodotObject.IsInstanceValid(wing) || !wing.IsInsideTree()) return false;
            var local = wing.ToLocal(point);
            return Mathf.Abs(local.X) < 5.6f && Mathf.Abs(local.Z) < 4.2f && local.Y > -1.5f && local.Y < 7.5f;
        };

        BuildWingShell(wing);
        BuildKitchen(wing);
        BuildHall(wing);
        BuildBedroom(wing);
        BuildToilet(wing);
        BuildStairs(wing);
        BuildAttic(wing);

        // Two doors, one pair: zal east wall <-> kitchen south wall.
        BuildZalWingDoor(room, wing);
        BuildKitchenZalDoor(wing, room);
    }

    // ------------------------------------------------------------ shell

    private static void BuildWingShell(Node3D wing)
    {
        Block(wing, "WingFloor", new(10.4f, .2f, 7.4f), new(0, -.1f, 0), "72412f", "wood_floor_planked");
        // Ceiling slab of the ground floor / attic floor, cut around the stair hole.
        Slab(wing, "SlabWest", -5.2f, HoleWest, -3.7f, 3.7f);
        Slab(wing, "SlabNorth", HoleWest, 5.2f, -3.7f, HoleNorth);
        Slab(wing, "SlabEastCorner", HoleEast, 5.2f, HoleNorth, 3.7f);
        Slab(wing, "SlabSouthStrip", HoleWest, HoleEast, 3.5f, 3.7f);
        // Outer walls (faces at +-5.0 and +-3.5).
        WallRun(wing, "WingWallNorth", true, -3.6f, -5.2f, 5.2f, WingCeiling, .2f, [], "b3a78d", "plaster_domestic");
        WallRun(wing, "WingWallSouth", true, 3.6f, -5.2f, 5.2f, WingCeiling, .2f, [], "b3a78d", "plaster_domestic");
        WallRun(wing, "WingWallWest", false, -5.1f, -3.5f, 3.5f, WingCeiling, .2f, [], "b3a78d", "plaster_domestic");
        WallRun(wing, "WingWallEast", false, 5.1f, -3.5f, 3.5f, WingCeiling, .2f, [], "b3a78d", "plaster_domestic");
        AddPerimeterTrim(wing, "Wing", 5.0f, 3.5f, WingCeiling);
        // Partitions: kitchen | hall+bedroom, hall | bedroom, hall | toilet.
        WallRun(wing, "PartitionKitchen", false, -1.5f, -3.5f, 3.5f, WingCeiling, .12f,
            [new(1.4f, 1.1f, 0, InnerDoorHeight)], "a99e86", "plaster_domestic");
        WallRun(wing, "PartitionBedroom", true, .5f, -1.44f, 5.0f, WingCeiling, .12f,
            [new(1.6f, 1.0f, 0, InnerDoorHeight)], "8b7a6a", "wallpaper");
        WallRun(wing, "PartitionToilet", false, 3.4f, .5f, 3.5f, WingCeiling, .12f,
            [new(1.3f, .8f, 0, InnerDoorHeight - .05f)], "a3aaa0", "plaster_domestic");
        DoorCasing(wing, "KitchenHallCasing", false, -1.5f, 1.4f, 1.1f, InnerDoorHeight, .18f);
        DoorCasing(wing, "BedroomDoorCasing", true, .5f, 1.6f, 1.0f, InnerDoorHeight, .18f);
        DoorCasing(wing, "ToiletDoorCasing", false, 3.4f, 1.3f, .8f, InnerDoorHeight - .05f, .18f);
        // Bedroom door stands ajar; toilet door closed with a hook.
        var ajar = Prop(wing, "BedroomDoorLeaf", new(.92f, 2.0f, .04f), new(1.6f + .46f * .5f, 1.0f, .5f + .46f), "8b7a62", "wood");
        ajar.RotationDegrees = new(0, 60, 0);
        Prop(wing, "BedroomDoorHandle", new(.08f, .03f, .06f), new(1.6f + .46f * .5f + .3f, 1.02f, .5f + .46f + .03f), "b09a5c", "metal");
        var toiletLeaf = Prop(wing, "ToiletDoorLeaf", new(.04f, 2.0f, .74f), new(3.42f, 1.0f, 1.3f - .28f), "8b7a62", "wood");
        toiletLeaf.RotationDegrees = new(0, -50, 0);
    }

    /// <summary>
    /// P2 / VIS-098: skirting and a two-step cornice along an unbroken rectangular
    /// perimeter (inner faces at ±halfX, ±halfZ). Partitions that meet the outer walls
    /// simply hide the run where they cross it. Visual only.
    /// </summary>
    private static void AddPerimeterTrim(Node3D room, string prefix, float halfX, float halfZ, float ceiling)
    {
        void Long(string name, float z, float y, float height, float depth, string color) =>
            Block(room, prefix + name, new(halfX * 2f, height, depth), new(0, y, z - Math.Sign(z) * depth * .5f),
                color, "wood_painted_trim", collision: false);
        void Short(string name, float x, float y, float height, float depth, float inset, string color) =>
            Block(room, prefix + name, new(depth, height, halfZ * 2f - inset * 2f), new(x - Math.Sign(x) * depth * .5f, y, 0),
                color, "wood_painted_trim", collision: false);
        foreach (var z in new[] { -halfZ, halfZ })
            Long(z < 0 ? "SkirtingNorth" : "SkirtingSouth", z, .05f, .10f, .022f, "5d4a38");
        foreach (var x in new[] { -halfX, halfX })
            Short(x < 0 ? "SkirtingWest" : "SkirtingEast", x, .05f, .10f, .022f, .022f, "5d4a38");
        foreach (var (step, height, depth, y) in new[] { ("Upper", .032f, .085f, ceiling - .016f), ("Lower", .05f, .045f, ceiling - .057f) })
        {
            foreach (var z in new[] { -halfZ, halfZ })
                Long("Cornice" + step + (z < 0 ? "North" : "South"), z, y, height, depth, "b8ae98");
            foreach (var x in new[] { -halfX, halfX })
                Short("Cornice" + step + (x < 0 ? "West" : "East"), x, y, height, depth, .085f, "b8ae98");
        }
    }

    private static void Slab(Node3D wing, string name, float x0, float x1, float z0, float z1)
    {
        var size = new Vector3(x1 - x0, .2f, z1 - z0);
        var center = new Vector3((x0 + x1) * .5f, WingCeiling + .1f, (z0 + z1) * .5f);
        // P2 / VIS-098: the wing ceiling and the attic floor above it read as laid boards.
        Block(wing, name, size, center, "8f7f68", "wood_floor_planked");
        Prop(wing, name + "Boards", new(size.X, .02f, size.Z), center + new Vector3(0, .11f, 0), "7d6c55", "wood_floor_planked");
    }

    // ------------------------------------------------------------ kitchen

    private static void BuildKitchen(Node3D wing)
    {
        var root = Group(wing, "Kitchen", "kitchen");
        FrostWindow(root, "KitchenWindowNorth", new(-3.2f, 1.7f, -3.5f), 0);
        FrostWindow(root, "KitchenWindowWest", new(-5.0f, 1.44f, -.6f), 90);
        // Base cabinets, worktop, sink and a tap along the north wall.
        Block(root, "KitchenCabinets", new(2.7f, .84f, .58f), new(-3.55f, .42f, -3.2f), "8a7a5c", "wood_furniture_interior");
        Prop(root, "KitchenWorktop", new(2.76f, .04f, .62f), new(-3.55f, .86f, -3.18f), "5d4a38", "wood_prop");
        for (var i = 0; i < 4; i++)
            Prop(root, $"KitchenCabinetDoor{i}", new(.6f, .72f, .02f), new(-4.6f + i * .68f, .42f, -2.9f), "9c8b68", "wood_furniture_interior");
        Cylinder(root, "KitchenSink", .24f, .12f, new(-3.2f, .84f, -3.2f), "c9cfcc", "enamel", false);
        Prop(root, "KitchenTapStem", new(.03f, .22f, .03f), new(-3.2f, 1.0f, -3.42f), "9a958a", "iron");
        Prop(root, "KitchenTapSpout", new(.03f, .03f, .16f), new(-3.2f, 1.1f, -3.34f), "9a958a", "iron");
        Cylinder(root, "KitchenWaterBucket", .17f, .32f, new(-4.6f, 1.05f, -3.15f), "7d8a8e", "iron", false);
        // Wall shelves with jars, plates and a bread tin.
        Prop(root, "KitchenShelfLow", new(1.2f, .04f, .24f), new(-4.3f, 1.55f, -3.36f), "765842", "wood_prop");
        Prop(root, "KitchenShelfHigh", new(1.2f, .04f, .24f), new(-4.3f, 1.95f, -3.36f), "765842", "wood_prop");
        for (var i = 0; i < 5; i++)
        {
            Jar(root, $"KitchenJarLow{i}", new(-4.8f + i * .24f, 1.57f, -3.36f), i % 2 == 0 ? "a33b30" : "c49a3b");
            Cylinder(root, $"KitchenPlate{i}", .11f, .015f, new(-4.8f + i * .24f, 2.13f, -3.42f), "d9d3c2", "enamel", false, new(90, 0, 0));
        }
        Prop(root, "KitchenBreadTin", new(.34f, .22f, .24f), new(-2.3f, 1.99f, -3.36f), "bfb59a", "painted"); // VIS-095: painted sheet, dielectric
        // Dining table by the west window with a washed cotton cloth, four stools and tea things.
        var table = new Vector3(-3.85f, 0, -.6f);
        Block(root, "KitchenTableTop", new(1.3f, .05f, .86f), table + new Vector3(0, .74f, 0), "6b4e36", "wood_furniture_interior");
        RuralPropGeometry.Part(root,"KitchenCottonTablecloth",RuralPropGeometry.DrapedCloth(1.3f,.86f,.18f),table+new Vector3(0,.768f,0),RuralPropMaterials.Surface("cloth"));
        foreach (var (dx, dz) in new[] { (-.58f, -.35f), (.58f, -.35f), (-.58f, .35f), (.58f, .35f) })
            Prop(root, $"KitchenTableLeg{dx}{dz}", new(.06f, .72f, .06f), table + new Vector3(dx, .36f, dz), "5a412d", "wood_furniture_interior");
        foreach (var (dx, dz) in new[] { (-.35f, -.72f), (.35f, -.72f), (-.35f, .72f), (.35f, .72f) })
        {
            RuralPropModels.Stool(root,$"KitchenStool{dx}{dz}",table+new Vector3(dx,0,dz));
            Collider(root,$"KitchenStoolContact{dx}{dz}",new(.32f,.46f,.32f),table+new Vector3(dx,.23f,dz));
        }
        RuralPropModels.Samovar(root,"KitchenSamovar",table+new Vector3(-.3f,.772f,0));
        RuralPropModels.Teapot(root,"KitchenTeapot",table+new Vector3(.15f,.772f,.1f));
        for(var i=0;i<3;i++)RuralPropModels.Cup(root,$"KitchenCup{i}",table+new Vector3(.35f+i*.13f,.772f,-.15f+i*.12f));
        RuralPropGeometry.Part(root,"KitchenBowlOfSweets",RuralPropGeometry.Lathe("sweet-bowl",[new(0,0),new(.07f,0),new(.11f,.04f),new(.105f,.045f),new(.063f,.008f),new(0,.008f)]),table+new Vector3(.4f,.77f,.28f),RuralPropMaterials.Surface("earthenware"));
        // Tall sideboard against the partition, a tiled floor rug and a wall clock.
        Block(root, "KitchenSideboard", new(.46f, 1.9f, 1.3f), new(-1.79f, .95f, -2.2f), "7a6148", "wood_furniture_interior");
        Prop(root, "KitchenSideboardGlass", new(.02f, .8f, 1.0f), new(-1.55f, 1.3f, -2.2f), "6f8079", "glass");
        Prop(root, "KitchenRug", new(1.0f, .012f, 2.4f), new(-3.4f, .006f, .55f), "8e3b31", "carpet");
        Cylinder(root, "KitchenClock", .15f, .04f, new(-1.55f, 1.95f, .4f), "d6cdb5", "enamel", false, new(0, 0, 90));
        // Coat hooks, boots and a towel by the door to the zal.
        Prop(root, "KitchenHookRail", new(1.4f, .05f, .05f), new(-4.3f, 1.8f, 3.46f), "5d4a38", "wood_prop");
        Prop(root, "KitchenCoat", new(.5f, 1.0f, .08f), new(-4.55f, 1.25f, 3.4f), "58656a", "fabric_upholstery");
        Prop(root, "KitchenShawl", new(.38f, .6f, .05f), new(-4.0f, 1.45f, 3.42f), "8a5c4a", "fabric_pattern");
        Prop(root, "KitchenBootTray", new(.7f, .04f, .4f), new(-4.3f, .02f, 3.2f), "3e3a34", "plastic_abs");
        Prop(root, "KitchenTowel", new(.04f, .55f, .3f), new(-1.52f, 1.35f, 2.6f), "b3ac9d", "cloth_towel");
        for (var i = 0; i < 3; i++)
        {
            Prop(root, $"KitchenOnionPlait{i}", new(.10f, .7f, .10f), new(-4.6f + i * .3f, 1.75f, -3.44f), "b07a3e", "hay_bundle");
            Prop(root, $"KitchenHerbBundle{i}", new(.14f, .3f, .10f), new(-2.2f + i * .3f, 2.3f, 3.4f), i == 1 ? "6f7a4d" : "8a8653", "foliage");
        }
        Light(root, "KitchenBulb", new(-3.2f, 2.25f, 0f), "ead7b9", 1.0f, 6.5f, shadow: true);
        Prop(root, "KitchenBulbGlass", new(.07f, .09f, .07f), new(-3.2f, 2.25f, 0f), "f0e6c8", "glass");
        Prop(root, "KitchenBulbCord", new(.012f, .35f, .012f), new(-3.2f, 2.42f, 0f), "2e2c29", "fabric_upholstery");
    }

    // ------------------------------------------------------------ hall

    private static void BuildHall(Node3D wing)
    {
        var root = Group(wing, "Hall", "hall");
        Prop(root, "HallRunner", new(1.0f, .012f, 1.9f), new(1.0f, .006f, 1.45f), "6f5d4a", "carpet");
        Block(root, "HallShoeShelf", new(.9f, .5f, .3f), new(2.7f, .25f, .66f), "6e5540", "wood_furniture_interior");
        for (var i = 0; i < 3; i++)
            Prop(root, $"HallFeltBoot{i}", new(.12f, .2f, .28f), new(2.4f + i * .3f, .6f, .66f), i == 1 ? "5e5853" : "7b7064", "fabric_upholstery");
        Prop(root, "HallMirror", new(.03f, .8f, .5f), new(3.33f, 1.5f, 2.05f), "9fb0b0", "glass");
        Prop(root, "HallMirrorFrame", new(.04f, .88f, .58f), new(3.335f, 1.5f, 2.05f), "5d4a38", "wood");
        // Framed family photographs on the bedroom partition.
        for (var i = 0; i < 3; i++)
            Prop(root, $"HallPhoto{i}", new(.03f, .28f, .22f), new(.3f + i * .35f, 1.6f - (i % 2) * .12f, .43f), "3e3229", "wood_prop");
        Block(root, "HallGrandfatherClock", new(.4f, 1.9f, .32f), new(-1.2f, .95f, 2.2f), "5d4634", "wood_furniture_interior");
        Cylinder(root, "HallClockFace", .12f, .02f, new(-1.2f, 1.55f, 2.035f), "d6cdb5", "enamel", false, new(90, 0, 0));
        Light(root, "HallBulb", new(1.0f, 2.25f, 1.4f), "d8b88e", .95f, 5.0f, shadow: false);
        Prop(root, "HallBulbGlass", new(.07f, .09f, .07f), new(1.0f, 2.25f, 1.4f), "f0e6c8", "glass");
    }

    // ------------------------------------------------------------ bedroom

    private static void BuildBedroom(Node3D wing)
    {
        var root = Group(wing, "Bedroom", "bedroom");
        FrostWindow(root, "BedroomWindowNorth", new(3.6f, 1.44f, -3.5f), 0);
        FrostWindow(root, "BedroomWindowEast", new(5.0f, 1.44f, -2.6f), -90);
        // Double bed, head to the north wall: painted frame, mattress, quilt, tall pillow pile.
        Block(root, "BedFrame", new(1.7f, .34f, 2.1f), new(1.3f, .17f, -2.4f), "6b4e36", "wood_furniture_interior");
        Prop(root, "BedHeadboard", new(1.8f, .8f, .08f), new(1.3f, .74f, -3.42f), "5a412d", "wood_furniture_interior");
        Prop(root, "BedMattress", new(1.6f, .22f, 2.0f), new(1.3f, .45f, -2.4f), "d5cdb8", "fabric_upholstery");
        Prop(root, "BedQuilt", new(1.64f, .10f, 1.35f), new(1.3f, .61f, -1.95f), "8e3b31", "fabric_pattern");
        Prop(root, "BedQuiltBorder", new(1.68f, .08f, .18f), new(1.3f, .62f, -1.32f), "c9b48e", "fabric_pattern");
        for (var i = 0; i < 4; i++)
            Prop(root, $"BedPillow{i}", new(.5f, .16f + i * .01f, .4f), new(.85f + (i % 2) * .9f, .64f + (i / 2) * .16f, -3.15f), "e4ddc9", "fabric_upholstery");
        Prop(root, "BedWallCarpet", new(2.0f, 1.3f, .03f), new(1.3f, 1.75f, -3.47f), "7a2f2a", "carpet");
        // Wardrobe on the east wall, a chest at the foot of the bed, a dresser with a mirror.
        Block(root, "BedroomWardrobe", new(.62f, 2.1f, 1.3f), new(4.66f, 1.05f, -.9f), "6e5540", "wood_furniture_interior");
        Prop(root, "WardrobeDoorSeam", new(.02f, 1.9f, .02f), new(4.34f, 1.05f, -.9f), "3e2a24", "wood");
        Block(root, "BedroomChest", new(.9f, .5f, .5f), new(1.3f, .25f, -.98f), "5f4a38", "wood_furniture_interior");
        Block(root, "BedroomDresser", new(.5f, .9f, 1.1f), new(-1.14f, .45f, -2.5f), "7a6148", "wood_furniture_interior");
        Prop(root, "DresserMirror", new(.03f, .8f, .6f), new(-1.36f, 1.55f, -2.5f), "9fb0b0", "glass");
        Prop(root, "DresserMirrorFrame", new(.04f, .88f, .68f), new(-1.38f, 1.55f, -2.5f), "5d4a38", "wood");
        Cylinder(root, "DresserVase", .06f, .22f, new(-1.14f, 1.01f, -2.2f), "3d6a73", "enamel", false);
        // Bedside table with a lamp, a runner and a small prayer rug.
        Block(root, "BedsideTable", new(.42f, .58f, .42f), new(2.46f, .29f, -3.1f), "7a6148", "wood_furniture_interior");
        Cylinder(root, "BedsideLampStem", .03f, .28f, new(2.46f, .72f, -3.1f), "6c573e", "wood_prop", false);
        Cylinder(root, "BedsideLampShade", .11f, .16f, new(2.46f, .93f, -3.1f), "d8b88e", "fabric_pattern", false);
        Prop(root, "BedroomRunner", new(.9f, .012f, 2.0f), new(2.75f, .006f, -1.7f), "8e3b31", "carpet");
        Prop(root, "PrayerRug", new(.7f, .012f, 1.0f), new(3.7f, .006f, -.35f), "3f5d59", "carpet");
        Light(root, "BedroomBulb", new(2.0f, 2.25f, -1.5f), "d8b88e", .9f, 5.5f, shadow: true);
        Prop(root, "BedroomBulbGlass", new(.07f, .09f, .07f), new(2.0f, 2.25f, -1.5f), "f0e6c8", "glass");
        Light(root, "BedsideGlow", new(2.46f, 1.0f, -3.05f), "e8b878", .5f, 2.2f, shadow: false);
    }

    // ------------------------------------------------------------ toilet

    private static void BuildToilet(Node3D wing)
    {
        var root = Group(wing, "Toilet", "toilet");
        FrostWindow(root, "ToiletWindow", new(5.0f, 1.7f, 2.55f), -90, .46f, .46f, curtains: false);
        Prop(root, "ToiletBoardLining", new(.02f, 1.2f, 2.8f), new(4.99f, .6f, 2.0f), "b5ad98", "wood_furniture_interior");
        var bowl = new Vector3(4.2f, 0, 3.0f);
        Block(root, "ToiletPedestal", new(.32f, .32f, .42f), bowl + new Vector3(0, .16f, .0f), "e2e1da", "enamel");
        Cylinder(root, "ToiletBowlRim", .21f, .08f, bowl + new Vector3(0, .38f, -.04f), "e8e7e0", "enamel", false);
        Cylinder(root, "ToiletSeat", .20f, .025f, bowl + new Vector3(0, .43f, -.04f), "7a5a3e", "wood_furniture_interior", false);
        Prop(root, "ToiletCistern", new(.42f, .24f, .18f), new(4.2f, 1.9f, 3.35f), "d8d6cd", "enamel");
        Prop(root, "ToiletPipe", new(.04f, 1.4f, .04f), new(4.2f, 1.15f, 3.4f), "807b70", "painted"); // VIS-095: painted sheet, dielectric
        Prop(root, "ToiletChain", new(.008f, .55f, .008f), new(4.36f, 1.55f, 3.3f), "9a958a", "iron");
        Cylinder(root, "ToiletChainPull", .02f, .07f, new(4.36f, 1.25f, 3.3f), "d8d6cd", "enamel", false);
        // Wall-mounted washstand and towel.
        Prop(root, "WashstandTank", new(.14f, .34f, .30f), new(4.86f, 1.45f, 1.5f), "b8c0bf", "enamel");
        Prop(root, "WashstandSpout", new(.08f, .06f, .04f), new(4.78f, 1.26f, 1.5f), "807b70", "iron");
        Block(root, "WashstandBasin", new(.36f, .12f, .44f), new(4.72f, .86f, 1.5f), "c9cfcc", "enamel");
        Prop(root, "ToiletTowel", new(.02f, .5f, .3f), new(3.5f, 1.35f, 2.0f), "b3ac9d", "cloth_towel");
        Prop(root, "ToiletPaperNail", new(.05f, .02f, .02f), new(3.5f, 1.05f, 1.2f), "3b3936", "iron");
        Light(root, "ToiletBulb", new(4.2f, 2.25f, 2.0f), "e3c79a", .6f, 3.0f, shadow: false);
        Prop(root, "ToiletBulbGlass", new(.06f, .08f, .06f), new(4.2f, 2.25f, 2.0f), "f0e6c8", "glass");
    }

    // ------------------------------------------------------------ stairs

    private static void BuildStairs(Node3D wing)
    {
        var root = Group(wing, "AtticStairs", "hall|attic");
        var run = (StairBottomX - StairTopX);
        var rise = AtticFloor;
        var angle = Mathf.RadToDeg(Mathf.Atan2(rise, run));
        var length = Mathf.Sqrt(run * run + rise * rise);
        var mid = new Vector3((StairBottomX + StairTopX) * .5f, rise * .5f, (StairZ0 + StairZ1) * .5f);
        var up = new Vector3(rise, run, 0).Normalized();
        // One smooth ramp owns the physics; visible treads follow it.
        var body = new StaticBody3D { Name = "StairRampCollision", CollisionLayer = 1, CollisionMask = 0,
            Position = mid - up * .05f, RotationDegrees = new(0, 0, -angle) };
        body.SetMeta("collisionOwner", "house-interior-architecture");
        body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new BoxShape3D { Size = new(length, .1f, StairZ1 - StairZ0) } });
        root.AddChild(body);
        const int steps = 14;
        var riser = rise / steps;
        var tread = run / steps;
        for (var i = 0; i < steps; i++)
        {
            var top = (i + .5f) * riser + .02f;
            Prop(root, $"StairTread{i}", new(tread, top, StairZ1 - StairZ0),
                new(StairBottomX - (i + .5f) * tread, top * .5f, (StairZ0 + StairZ1) * .5f), "7d6a52", "wood_furniture_interior");
        }
        // Handrail along the hall side.
        var rail = Prop(root, "StairHandrail", new(length, .05f, .05f), mid + new Vector3(0, .95f, StairZ0 - (StairZ1 + StairZ0) * .5f + .02f), "5d4a38", "wood_prop");
        rail.RotationDegrees = new(0, 0, -angle);
        for (var i = 0; i < 5; i++)
        {
            var x = StairBottomX - i * (run / 4f);
            Prop(root, $"StairBaluster{i}", new(.04f, .95f, .04f), new(x, (StairBottomX - x) / run * rise + .47f, StairZ0 + .02f), "5d4a38", "wood_prop");
        }
        // The hole's guard rails on the attic side.
        Block(root, "AtticGuardRailNorth", new(HoleEast - HoleWest - .2f, .06f, .05f), new((HoleEast + HoleWest) * .5f + .1f, AtticFloor + .95f, HoleNorth), "5d4a38", "wood_prop");
        Block(root, "AtticGuardRailEast", new(.05f, .06f, 3.5f - HoleNorth), new(HoleEast, AtticFloor + .95f, (3.5f + HoleNorth) * .5f), "5d4a38", "wood_prop");
        Collider(root, "AtticGuardBlockNorth", new(HoleEast - HoleWest - .2f, 1.0f, .06f), new((HoleEast + HoleWest) * .5f + .1f, AtticFloor + .5f, HoleNorth));
        Collider(root, "AtticGuardBlockEast", new(.06f, 1.0f, 3.5f - HoleNorth), new(HoleEast, AtticFloor + .5f, (3.5f + HoleNorth) * .5f));
        for (var i = 0; i < 5; i++)
            Prop(root, $"AtticGuardPost{i}", new(.05f, .95f, .05f), new(-1.3f + i * 1.15f, AtticFloor + .47f, HoleNorth), "5d4a38", "wood_prop");
    }

    // ------------------------------------------------------------ attic

    private static void BuildAttic(Node3D wing)
    {
        var root = Group(wing, "Attic", "attic");
        const float y0 = AtticFloor + .02f; // top of the floorboards
        const float knee = .9f, ridge = 2.9f, half = 3.5f;
        // Knee walls, gable ends, roof slabs.
        foreach (var sign in new[] { -1f, 1f })
        {
            Block(root, sign < 0 ? "AtticKneeWallNorth" : "AtticKneeWallSouth", new(10.4f, knee, .2f), new(0, y0 + knee * .5f, sign * (half + .1f)), "7d6a52", "wood");
            Block(root, sign < 0 ? "AtticGableWest" : "AtticGableEast", new(.2f, knee, 7.0f), new(sign * 5.1f, y0 + knee * .5f, 0), "7d6a52", "wood");
            var gable = new MeshInstance3D { Name = sign < 0 ? "AtticGableWestTriangle" : "AtticGableEastTriangle",
                Mesh = new PrismMesh { Size = new(7.0f, ridge - knee, .2f), LeftToRight = .5f },
                Position = new(sign * 5.1f, y0 + knee + (ridge - knee) * .5f, 0), RotationDegrees = new(0, 90, 0),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("7d6a52", "wood", sheltered: true) };
            root.AddChild(gable);
            Collider(root, sign < 0 ? "AtticGableWestBlock" : "AtticGableEastBlock", new(.2f, ridge, 7.0f), new(sign * 5.1f, y0 + ridge * .5f, 0));
            // Roof slabs: inner surface climbs from the knee wall to the ridge.
            var span = Mathf.Sqrt(half * half + (ridge - knee) * (ridge - knee));
            var angle = Mathf.RadToDeg(Mathf.Atan2(ridge - knee, half));
            var slab = Block(root, sign < 0 ? "AtticRoofNorth" : "AtticRoofSouth", new(10.4f, .12f, span + .1f),
                new(0, y0 + knee + (ridge - knee) * .5f + .06f, sign * half * .5f), "7a6a53", "wood");
            slab.RotationDegrees = new(sign < 0 ? -angle : angle, 0, 0);
        }
        Prop(root, "AtticRidgeBeam", new(10.4f, .16f, .16f), new(0, y0 + ridge - .04f, 0), "493629", "wood");
        for (var i = -2; i <= 2; i++)
            Prop(root, $"AtticCollarBeam{i}", new(.12f, .12f, 1.5f), new(i * 2.2f, y0 + 2.42f, 0), "493629", "wood");
        // Floor rug and a warm chimney column from the zal stove.
        Prop(root, "AtticRug", new(2.6f, .012f, 1.5f), new(3.0f, y0 + .006f, 0), "8e3b31", "carpet");
        Block(root, "AtticChimney", new(.6f, ridge - .05f, .6f), new(-2.6f, y0 + (ridge - .05f) * .5f, -.4f), "9a5b45", "stone");
        Prop(root, "AtticChimneyWhitewash", new(.62f, .7f, .62f), new(-2.6f, y0 + .35f, -.4f), "cfc6b3", "plaster");
        // Aidar's bed under the east gable window: iron frame, mattress, quilt, pillow.
        FrostWindow(root, "AtticWindowEast", new(5.0f, y0 + 1.45f, 0), -90, .7f, .9f, curtains: false);
        Block(root, "AtticBedFrame", new(2.0f, .3f, 1.0f), new(3.85f, y0 + .15f, 0), "3e3a34", "painted"); // VIS-095: painted frame, dielectric
        Prop(root, "AtticMattress", new(1.94f, .16f, .94f), new(3.85f, y0 + .38f, 0), "cfc7b0", "fabric_upholstery");
        Prop(root, "AtticQuilt", new(1.3f, .10f, .96f), new(3.4f, y0 + .5f, 0), "5f7480", "fabric_pattern");
        Prop(root, "AtticQuiltPatch", new(.3f, .012f, .3f), new(3.4f, y0 + .56f, .2f), "b98d4a", "fabric_pattern");
        Prop(root, "AtticPillow", new(.42f, .13f, .62f), new(4.6f, y0 + .5f, 0), "e4ddc9", "fabric_upholstery");
        Prop(root, "AtticBedBlanketFold", new(.5f, .09f, .94f), new(2.9f, y0 + .5f, 0), "8e3b31", "fabric_pattern");
        // Crate night table, lamp and a phone charger cable.
        Block(root, "AtticCrate", new(.5f, .45f, .5f), new(4.6f, y0 + .225f, -.95f), "8a6b50", "wood_prop");
        Cylinder(root, "AtticLampStem", .03f, .22f, new(4.6f, y0 + .56f, -.95f), "6c573e", "wood_prop", false);
        Cylinder(root, "AtticLampShade", .1f, .14f, new(4.6f, y0 + .74f, -.95f), "d8b88e", "fabric_pattern", false);
        Light(root, "AtticBedLamp", new(4.5f, y0 + .85f, -.9f), "e8b878", .7f, 3.4f, shadow: false);
        // Old family things: trunks, boxes, skis, a hanging jacket, hay sheaves, a cradle.
        Block(root, "AtticTrunk", new(1.0f, .5f, .6f), new(-4.1f, y0 + .25f, 2.7f), "5f4a38", "wood_furniture_interior");
        Prop(root, "AtticTrunkStrap", new(1.02f, .05f, .62f), new(-4.1f, y0 + .3f, 2.7f), "3e3a34", "iron");
        Block(root, "AtticBoxStackA", new(.6f, .5f, .5f), new(-4.3f, y0 + .25f, -2.7f), "a38f68", "hay_bundle");
        Block(root, "AtticBoxStackB", new(.5f, .4f, .45f), new(-4.3f, y0 + .7f, -2.7f), "8a7a5c", "hay_bundle");
        Block(root, "AtticBoxStackC", new(.6f, .45f, .5f), new(-3.5f, y0 + .225f, -2.9f), "a38f68", "hay_bundle");
        for (var i = 0; i < 2; i++)
            Prop(root, $"AtticSki{i}", new(.06f, .04f, 1.9f), new(-4.85f, y0 + 1.0f + i * .1f, 1.0f - i * .18f), "8a6b50", "wood_prop").RotationDegrees = new(-70, 0, 0);
        Prop(root, "AtticJacket", new(.45f, .85f, .08f), new(-2.9f, y0 + 1.6f, -3.35f), "3f4643", "fabric_upholstery");
        for (var i = 0; i < 3; i++)
            Prop(root, $"AtticHaySheaf{i}", new(.16f, .5f, .16f), new(-.5f + i * .7f, y0 + 2.42f - .28f, 0), "b49a5c", "hay_bundle");
        Prop(root, "AtticCradle", new(.9f, .35f, .5f), new(-1.1f, y0 + .18f, -2.7f), "8a6b50", "wood_prop");
        Prop(root, "AtticOldTv", new(.5f, .42f, .4f), new(-1.9f, y0 + .21f, 2.7f), "2e2c29", "plastic_abs");
        Prop(root, "AtticOldTvGlass", new(.36f, .3f, .02f), new(-1.9f, y0 + .23f, 2.5f), "3d4a4d", "glass");
        Light(root, "AtticBulb", new(-.6f, y0 + 2.35f, .3f), "d0c4a4", .85f, 6.5f, shadow: true);
        Prop(root, "AtticBulbGlass", new(.07f, .09f, .07f), new(-.6f, y0 + 2.35f, .3f), "f0e6c8", "glass");
        Prop(root, "AtticBulbCord", new(.012f, .35f, .012f), new(-.6f, y0 + 2.6f, .3f), "2e2c29", "fabric_upholstery");
    }

    // ------------------------------------------------------------ the two fade doors

    private static void BuildZalWingDoor(Node3D room, Node3D wing)
    {
        var root = Group(room, "ZalWingDoor", "zal");
        Block(root, "ZalWingDoorLeaf", new(.07f, InnerDoorHeight, .9f), new(3.965f, InnerDoorHeight * .5f, ZalDoorZ), "5e4a37", "wood");
        Prop(root, "ZalWingDoorPanelTop", new(.02f, .7f, .68f), new(3.92f, 1.6f, ZalDoorZ), "6b543f", "wood");
        Prop(root, "ZalWingDoorPanelBottom", new(.02f, .7f, .68f), new(3.92f, .6f, ZalDoorZ), "6b543f", "wood");
        Prop(root, "ZalWingDoorHandle", new(.06f, .03f, .12f), new(3.90f, 1.05f, ZalDoorZ - .32f), "b09a5c", "metal");
        DoorCasing(root, "ZalWingDoorCasing", false, 3.95f, ZalDoorZ, .9f, InnerDoorHeight, .12f);
        var dir = Vector3.Right;
        Portal(root, "ZalWingDoorTrigger", new(.5f, 2.0f, .9f), new(3.72f, 1.0f, ZalDoorZ), room, dir,
            () => (wing, WingKitchenArrival, 0f));
    }

    private static void BuildKitchenZalDoor(Node3D wing, Node3D room)
    {
        var root = Group(wing, "KitchenZalDoor", "kitchen");
        var x = WingKitchenArrival.X;
        Block(root, "KitchenZalDoorLeaf", new(.9f, InnerDoorHeight, .07f), new(x, InnerDoorHeight * .5f, 3.465f), "5e4a37", "wood");
        Prop(root, "KitchenZalDoorFelt", new(.78f, 1.85f, .02f), new(x, 1.0f, 3.42f), "5b544b", "fabric_upholstery");
        Prop(root, "KitchenZalDoorHandle", new(.12f, .03f, .06f), new(x + .32f, 1.05f, 3.39f), "b09a5c", "metal");
        DoorCasing(root, "KitchenZalDoorCasing", true, 3.45f, x, .9f, InnerDoorHeight, .12f);
        Portal(root, "KitchenZalDoorTrigger", new(.9f, 2.0f, .5f), new(x, 1.0f, 3.2f), wing, Vector3.Back,
            () => (room, ZalFromWingArrival, 90f));
    }

    /// <summary>
    /// A walk-through door: entering the trigger while facing the door fades to
    /// black, moves the player to the destination space and fades back.
    /// </summary>
    private static void Portal(Node3D parent, string name, Vector3 size, Vector3 at, Node3D space, Vector3 outward,
        Func<(Node3D Space, Vector3 Local, float Yaw)> destination)
    {
        var area = new Area3D { Name = name, Position = at, CollisionLayer = 0, CollisionMask = uint.MaxValue, Monitoring = true };
        area.AddChild(new CollisionShape3D { Name = "Shape", Shape = new BoxShape3D { Size = size } });
        area.SetMeta("houseWingPortal", true);
        area.BodyEntered += body =>
        {
            if (body is not FirstPersonController player) return;
            var facing = -player.GlobalBasis.Z;
            var door = (space.GlobalBasis * outward).Normalized();
            if (facing.Dot(door) < .2f) return;
            var (target, local, yaw) = destination();
            WingTravel(player, target, local, yaw);
        };
        parent.AddChild(area);
    }

    private static void WingTravel(FirstPersonController player, Node3D target, Vector3 local, float localYaw)
    {
        if (Time.GetTicksMsec() < _wingTravelUntil) return;
        var tree = player.GetTree();
        if (tree is null) return;
        _wingTravelUntil = Time.GetTicksMsec() + 1400;
        var layer = new CanvasLayer { Layer = 120, Name = "HouseWingFade" };
        var fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(fade);
        tree.Root.AddChild(layer);
        var tween = layer.CreateTween();
        tween.TweenProperty(fade, "color:a", 1f, .16f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(player) || !GodotObject.IsInstanceValid(target)) return;
            player.ApplyZoneSpawn(target.ToGlobal(local), target.GlobalRotationDegrees.Y + localYaw);
        }));
        tween.TweenProperty(fade, "color:a", 0f, .34f);
        tween.TweenCallback(Callable.From(layer.QueueFree));
    }

    // ------------------------------------------------------------ helpers

    private static Node3D Group(Node3D parent, string name, string role)
    {
        var node = new Node3D { Name = name };
        node.SetMeta("houseRoom", role);
        parent.AddChild(node);
        return node;
    }

    private static Node3D Prop(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface) =>
        Block(parent, name, size, at, color, surface, collision: false);

    private static void Collider(Node3D parent, string name, Vector3 size, Vector3 at)
    {
        var body = new StaticBody3D { Name = name, Position = at, CollisionLayer = 1, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "house-interior-architecture");
        body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
    }

    /// <summary>
    /// A straight wall run with rectangular openings. Alongx walls sit at z=at
    /// and span x; otherwise they sit at x=at and span z. Opening centres are
    /// measured along the wall's own axis.
    /// </summary>
    private static void WallRun(Node3D parent, string name, bool alongX, float at, float from, float to,
        float height, float thickness, Opening[] openings, string color, string surface)
    {
        var cursor = from;
        var index = 0;
        foreach (var opening in openings.OrderBy(value => value.Center))
        {
            var left = opening.Center - opening.Width * .5f;
            var right = opening.Center + opening.Width * .5f;
            Segment(cursor, left, 0, height, "Pier" + index);
            Segment(left, right, opening.Top, height, "Head" + index);
            cursor = right;
            index++;
        }
        Segment(cursor, to, 0, height, "Pier" + index);

        void Segment(float a, float b, float bottom, float top, string suffix)
        {
            if (b - a < .001f || top - bottom < .001f) return;
            var size = alongX ? new Vector3(b - a, top - bottom, thickness) : new Vector3(thickness, top - bottom, b - a);
            var center = alongX ? new Vector3((a + b) * .5f, (bottom + top) * .5f, at)
                : new Vector3(at, (bottom + top) * .5f, (a + b) * .5f);
            Block(parent, name + suffix, size, center, color, surface);
        }
    }

    private static void DoorCasing(Node3D parent, string name, bool alongX, float at, float center, float width, float height, float depth)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            var offset = center + side * (width * .5f + .04f);
            Prop(parent, name + (side < 0 ? "Left" : "Right"),
                alongX ? new(.08f, height + .06f, depth) : new(depth, height + .06f, .08f),
                alongX ? new(offset, (height + .06f) * .5f, at) : new(at, (height + .06f) * .5f, offset), "5d4a38", "wood");
        }
        Prop(parent, name + "Top", alongX ? new(width + .16f, .08f, depth) : new(depth, .08f, width + .16f),
            alongX ? new(center, height + .04f, at) : new(at, height + .04f, center), "5d4a38", "wood");
    }

    private static Material FrostPane() => _frostPane ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(.66f, .76f, .82f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    /// <summary>
    /// Wing windows show overcast winter daylight on a frosted pane: nothing
    /// exists outside the wing, so a real see-through opening would show void.
    /// Local +Z of the root faces into the room.
    /// </summary>
    private static void FrostWindow(Node3D parent, string name, Vector3 center, float yawDegrees,
        float width = WindowWidth, float height = 1.40f, bool curtains = true)
    {
        var root = new Node3D { Name = name, Position = center, RotationDegrees = new(0, yawDegrees, 0) };
        root.SetMeta("windowSurface", "frosted daylight panel; the wing has no exterior");
        parent.AddChild(root);
        root.AddChild(new MeshInstance3D { Name = "Pane", Mesh = new QuadMesh { Size = new(width, height) },
            Position = new(0, 0, .015f), MaterialOverride = FrostPane(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        foreach (var sign in new[] { -1f, 1f })
        {
            Prop(root, sign < 0 ? "JambLeft" : "JambRight", new(.07f, height + .1f, .08f), new(sign * (width * .5f + .03f), 0, .04f), "e0dccf", "wood");
            Prop(root, sign < 0 ? "BottomRail" : "TopRail", new(width, .07f, .08f), new(0, sign * (height * .5f), .04f), "e0dccf", "wood");
        }
        Prop(root, "Mullion", new(.04f, height, .05f), new(0, 0, .05f), "e0dccf", "wood");
        Prop(root, "Transom", new(width, .04f, .05f), new(0, height * .12f, .05f), "e0dccf", "wood");
        Prop(root, "SillBoard", new(width + .18f, .06f, .26f), new(0, -height * .5f - .04f, .12f), "b5ad98", "wood");
        if (!curtains) return;
        root.AddChild(new MeshInstance3D { Name = "CurtainRod", Position = new(0, .82f, .28f), RotationDegrees = new(0, 0, 90),
            Mesh = new CylinderMesh { Height = width + .3f, TopRadius = .014f, BottomRadius = .014f, RadialSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5d4a38", "wood", sheltered: true) });
        foreach (var sign in new[] { -1f, 1f })
            DrapedFabric(root, sign < 0 ? "CurtainLeft" : "CurtainRight", sign * .53f, .25f, .81f, -.68f, .28f, .025f, 3, false);
        DrapedFabric(root, "SheerCurtain", 0, 1.04f, .81f, -.67f, .22f, .010f, 7, true);
    }

    private static void Cylinder(Node3D parent, string name, float radius, float height, Vector3 at, string color,
        string surface, bool collision, Vector3? rotation = null)
    {
        var body = new StaticBody3D { Name = name, Position = at, RotationDegrees = rotation ?? Vector3.Zero,
            CollisionLayer = collision ? 1u : 0u, CollisionMask = 0 };
        body.SetMeta("collisionOwner", collision ? "house-interior-architecture" : "none");
        body.AddChild(new MeshInstance3D { Name = "Visible",
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 16 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true) });
        if (collision)
            body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new CylinderShape3D { Radius = radius, Height = height } });
        parent.AddChild(body);
    }

    private static void Jar(Node3D parent, string name, Vector3 standOn, string contents)
    {
        Cylinder(parent, name, .065f, .16f, standOn + new Vector3(0, .08f, 0), contents, "glass", false);
        Cylinder(parent, name + "Lid", .068f, .02f, standOn + new Vector3(0, .17f, 0), "b8b2a0", "iron", false);
    }

    private static void Light(Node3D parent, string name, Vector3 at, string color, float energy, float range, bool shadow)
    {
        parent.AddChild(new OmniLight3D { Name = name, Position = at, LightColor = Color.FromHtml(color),
            LightEnergy = energy, OmniRange = range, ShadowEnabled = shadow });
    }
}
