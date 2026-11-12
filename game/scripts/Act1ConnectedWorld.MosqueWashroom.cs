using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildMosqueWashroom()
    {
        var room = _mosqueRoom!;
        var wash = new Node3D { Name = "MosqueTaharatRoom" };
        room.AddChild(wash);
        wash.SetMeta("presentationRole", "separate enclosed ablution room at the entrance; no invented ritual interaction");
        wash.SetMeta("clearFloorRect", new Rect2(3.62f, .91f, 1.58f, 1.25f));
        // Full-height walls meet the existing ceiling; no window, shoe shelf,
        // entrance leaf or minaret footprint is changed. Approach from the west.
        foreach (var z in new[] { .84f, 2.23f })
            FacilitySolid(wash, "TaharatPartition" + z, new(1.82f, 3.74f, .14f), new(4.39f, 1.87f, z), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorNorth", new(.14f, 3.74f, .08f), new(3.55f, 1.87f, .88f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorSouth", new(.14f, 3.74f, .08f), new(3.55f, 1.87f, 2.19f), "818b7b", "plaster");
        FacilitySolid(wash, "TaharatDoorHeader", new(.14f, 1.40f, 1.23f), new(3.55f, 3.04f, 1.535f), "818b7b", "plaster");
        var tile = new StandardMaterial3D { AlbedoColor = new Color("52665f"), Roughness = .96f, MetallicSpecular = .08f };
        var floor = AddVisualBox(wash, "TaharatTileFloor", new(1.58f, .006f, 1.25f), new(4.41f, .006f, 1.535f), "52665f", "stone");
        floor.MaterialOverride = tile;
        var grout = new List<Transform3D>();
        for (var i = 0; i < 5; i++) grout.Add(MosqueBoxTransform(new(3.64f + i * .31f, .0092f, 1.535f), new(.004f, .0004f, 1.23f), 0));
        for (var i = 1; i < 4; i++) grout.Add(MosqueBoxTransform(new(4.41f, .0092f, .91f + i * .31f), new(1.56f, .0004f, .004f), 0));
        MosqueBatch(wash, "TaharatFloorGrout", grout, new StandardMaterial3D { AlbedoColor = new Color("a2a494"), Roughness = 1f });
        foreach (var z in new[] { .918f, 2.152f })
            AddVisualBox(wash, "TaharatSplashback" + z, new(1.57f, 1.18f, .012f), new(4.41f, .60f, z), "9da99a", "ceramic");
        AddVisualBox(wash, "TaharatEastSplashback", new(.016f, 1.18f, 1.23f), new(5.198f, .60f, 1.535f), "9da99a", "ceramic");
        var door = FacilityManualDoor(room, "MosqueTaharat", "mosque/taharat", new(3.55f, 0, .98f), 1.10f, 2.28f, 0, 90);
        door.Hinge.GetNode<MeshInstance3D>("MosqueTaharatLeaf").MaterialOverride = MosqueFinish("walnut", "a4977b", .8f);
        MosquePrintedSign(room, "MosqueTaharatSign", "Тахарат", new(3.472f, 2.58f, 1.54f), -90, .52f, "signs-details", 2);
        foreach (var z in new[] { .94f, 2.12f }) MosqueWood(wash, "TaharatDoorCasing" + z, new(.035f, 2.32f, .045f), new(3.465f, 1.16f, z));
        MosqueWood(wash, "TaharatDoorLintel", new(.035f, .065f, 1.225f), new(3.465f, 2.32f, 1.53f));
        // Compact seat and low washing basin leave a standing/turning space
        // by the west door. Water is a fixture, not an unlicensed audio loop.
        FacilitySolid(wash, "MosqueWashCabinet", new(.28f, .40f, .48f), new(5.04f, .20f, 1.52f), "6f796c", "wood_painted_trim");
        FacilityVessel(wash, "MosqueWashBasin", new(4.96f, .45f, 1.52f), .215f, .10f, "b6c0b5", false);
        FacilityRod(wash, "MosqueWaterRiser", new(5.15f, .11f, 1.43f), new(5.15f, .84f, 1.43f), .015f, "919b94");
        FacilityRod(wash, "MosqueTapSpout", new(5.15f, .83f, 1.43f), new(4.96f, .83f, 1.52f), .014f, "919b94");
        FacilityRod(wash, "MosqueBasinDrain", new(4.96f, .39f, 1.52f), new(5.15f, .07f, 1.52f), .024f, "919b94");
        MosqueWood(wash, "TaharatSeat", new(.32f, .055f, .30f), new(4.59f, .42f, 1.86f), true);
        foreach (var x in new[] { 4.47f, 4.71f }) foreach (var z in new[] { 1.76f, 1.96f })
            MosqueWood(wash, "TaharatSeatLeg" + x + "_" + z, new(.04f, .3925f, .04f), new(x, .19625f, z));
        FacilityRod(wash, "MosqueTowelRail", new(4.01f, 1.54f, 2.10f), new(4.42f, 1.54f, 2.10f), .012f, "89958c");
        AddVisualBox(wash, "MosqueWashTowel", new(.31f, .44f, .014f), new(4.21f, 1.31f, 2.09f), "b1ad93", "cloth_towel");
        DiscoveryCylinder(wash, "TaharatFloorDrain", .045f, .045f, .003f, new(4.91f, .011f, 1.95f), "78867c");
        FacilityLamp(wash, "TaharatLamp", new(4.40f, 2.93f, 1.52f), "ffe5c5", .38f, 1.65f);
    }
}
