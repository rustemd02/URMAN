using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildMosqueWashroom()
    {
        var room = _mosqueRoom!;
        var wash = new Node3D { Name = "MosqueTaharatRoom" };
        room.AddChild(wash);
        wash.SetMeta("presentationRole", "enclosed ablution alcove under the late first turn of the minaret stair, with a west side extension; no invented ritual interaction");
        wash.SetMeta("clearFloorRect", new Rect2(1.17f, -2.07f, 1.76f, 1.10f));
        wash.SetMeta("standingHeadroom", 2.20f);
        // A full room cannot fit between the .39 m mast and the 1.20 m tread
        // edge. This low side alcove extends west; only its east end lies below
        // the winders. Its complete outer rectangle is X[1.03,3.07],
        // Z[-2.21,-.83]. Where it overlaps the actual treads, the polar angles
        // about (3.83,-2) are 129.3..195.5 degrees: treads 14..17, whose lowest
        // underside is 2.32 m. The soffit top is 2.28, leaving .04 m clearance.
        // The entry tread starts at 126 degrees, entirely outside this alcove.
        foreach (var (name, z) in new[] { ("TaharatPartition0.84", -2.14f), ("TaharatPartition2.23", -.90f) })
            FacilitySolid(wash, name, new(2.04f, 2.20f, .14f), new(2.05f, 1.10f, z), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatEastWall", new(.14f, 2.20f, 1.10f), new(3.00f, 1.10f, -1.52f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorNorth", new(.14f, 2.20f, .09f), new(1.10f, 1.10f, -2.095f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorSouth", new(.14f, 2.20f, .10f), new(1.10f, 1.10f, -.95f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorHeader", new(.14f, .04f, 1.05f), new(1.10f, 2.18f, -1.525f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatStairSoffit", new(2.04f, .08f, 1.38f), new(2.05f, 2.24f, -1.52f), "ddd5c0", "wood_painted_trim");
        var tile = new StandardMaterial3D { AlbedoColor = new Color("52665f"), Roughness = .96f, MetallicSpecular = .08f };
        var floor = AddVisualBox(wash, "TaharatTileFloor", new(1.76f, .006f, 1.10f), new(2.05f, .006f, -1.52f), "52665f", "stone");
        floor.MaterialOverride = tile;
        var grout = new List<Transform3D>();
        for (var i = 1; i < 6; i++) grout.Add(MosqueBoxTransform(new(1.17f + i * .29f, .0092f, -1.52f), new(.004f, .0004f, 1.08f), 0));
        for (var i = 1; i < 4; i++) grout.Add(MosqueBoxTransform(new(2.05f, .0092f, -2.07f + i * .275f), new(1.74f, .0004f, .004f), 0));
        MosqueBatch(wash, "TaharatFloorGrout", grout, new StandardMaterial3D { AlbedoColor = new Color("a2a494"), Roughness = 1f });
        foreach (var (name, z) in new[] { ("TaharatSplashback0.918", -2.062f), ("TaharatSplashback2.152", -.978f) })
            AddVisualBox(wash, name, new(1.75f, 1.18f, .012f), new(2.05f, .60f, z), "9da99a", "ceramic");
        AddVisualBox(wash, "TaharatEastSplashback", new(.016f, 1.18f, 1.08f), new(2.922f, .60f, -1.52f), "9da99a", "ceramic");
        // The smaller leaf keeps the original state key and opens into the
        // empty north-west strip. Its full .96 m sweep cannot reach the seat
        // (nearest corner is 1.40 m from the hinge), cabinet, or stair mast.
        var door = FacilityManualDoor(room, "MosqueTaharat", "mosque/taharat", new(1.10f, 0, -2.00f), .96f, 2.12f, 0, 90);
        door.Hinge.GetNode<MeshInstance3D>("MosqueTaharatLeaf").MaterialOverride = MosqueFinish("walnut", "a4977b", .8f);
        MosquePrintedSign(door.Hinge, "MosqueTaharatSign", "Тахарат", new(-.045f, 1.76f, .48f), -90, .52f, "signs-details", 2);
        foreach (var (name, z) in new[] { ("TaharatDoorCasing0.94", -2.05f), ("TaharatDoorCasing2.12", -1.00f) })
            MosqueWood(wash, name, new(.035f, 2.16f, .045f), new(1.015f, 1.08f, z));
        MosqueWood(wash, "TaharatDoorLintel", new(.035f, .045f, 1.095f), new(1.015f, 2.1775f, -1.525f));
        // Clear standing position around (1.90,-1.52), on the ordinary hall
        // floor. No ducking, basement, ramp, or stair route is introduced.
        FacilitySolid(wash, "MosqueWashCabinet", new(.28f, .40f, .48f), new(2.78f, .20f, -1.55f), "6f796c", "wood_painted_trim");
        FacilityVessel(wash, "MosqueWashBasin", new(2.70f, .45f, -1.55f), .215f, .10f, "b6c0b5", false);
        FacilityRod(wash, "MosqueWaterRiser", new(2.88f, .11f, -1.64f), new(2.88f, .84f, -1.64f), .015f, "919b94");
        FacilityRod(wash, "MosqueTapSpout", new(2.88f, .83f, -1.64f), new(2.70f, .83f, -1.55f), .014f, "919b94");
        FacilityRod(wash, "MosqueBasinDrain", new(2.70f, .39f, -1.55f), new(2.88f, .07f, -1.55f), .024f, "919b94");
        MosqueWood(wash, "TaharatSeat", new(.32f, .055f, .30f), new(2.48f, .42f, -1.16f), true);
        foreach (var (oldX, x) in new[] { (4.47f, 2.36f), (4.71f, 2.60f) })
        foreach (var (oldZ, z) in new[] { (1.76f, -1.26f), (1.96f, -1.06f) })
            MosqueWood(wash, "TaharatSeatLeg" + oldX + "_" + oldZ, new(.04f, .3925f, .04f), new(x, .19625f, z));
        FacilityRod(wash, "MosqueTowelRail", new(1.51f, 1.54f, -1.01f), new(1.92f, 1.54f, -1.01f), .012f, "89958c");
        AddVisualBox(wash, "MosqueWashTowel", new(.31f, .44f, .014f), new(1.71f, 1.31f, -1.02f), "b1ad93", "cloth_towel");
        DiscoveryCylinder(wash, "TaharatFloorDrain", .045f, .045f, .003f, new(2.74f, .011f, -1.16f), "78867c");
        FacilityLamp(wash, "TaharatLamp", new(2.05f, 2.03f, -1.52f), "ffe5c5", .30f, 1.65f);
    }
}
