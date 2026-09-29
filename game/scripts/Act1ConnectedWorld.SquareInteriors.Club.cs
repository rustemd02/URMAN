using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // House of culture, 18 x 14 m, hall 6.8 m high. Front to back: foyer, hall
    // with seven rows, stage 1.2 m high; wings: costume room and instrument
    // room in front, backstage stairs behind.
    private void BuildClubInterior(Node3D club, Material outer, Material trim)
    {
        const float w = 18f, d = 14f, h = 6.8f, t = .35f, wingH = 3.5f, foyerH = 5.2f;
        var ix = w * .5f - t; var iz = d * .5f - t;
        var cream = Mat("efe3c8", "plaster");
        var green = Mat("5d7a5a", "plaster");
        var floorMat = Mat("8a6d4b", "wood_floor_painted");
        var body = SBody(club, "ClubFurnitureBody");
        const float foyerZ = 3.85f, stageZ = -2.45f, wingX = 5.35f, deck = 1.2f;

        // ---- outer walls -----------------------------------------------------------------------
        var front = new List<Opening> { new(0, 0, 2.5f, 3.0f, Door: true) };
        var back = new List<Opening>();
        foreach (var x in new[] { -6.75f, -2.25f, 2.25f, 6.75f })
        {
            front.Add(new(x, 1.6f, 1.3f, 3.2f, Lit: x is > -3f and < 0f));
            back.Add(new(x, 1.6f, 1.3f, 3.2f));
        }
        var side = new List<Opening> { new(3.4f, 1.3f, 1.3f, 1.6f), new(-1.6f, 1.3f, 1.3f, 1.6f), new(-5.0f, 1.3f, 1.3f, 1.6f) };
        SWall(club, "FrontWall", new(0, 0, d * .5f - t * .5f), 0, w, h, t, outer, cream, trim, front);
        SWall(club, "BackWall", new(0, 0, -d * .5f + t * .5f), 180, w, h, t, outer, cream, trim, back);
        SWall(club, "EastWall", new(w * .5f - t * .5f, 0, 0), 90, d - t * 2, h, t, outer, cream, trim, side);
        SWall(club, "WestWall", new(-w * .5f + t * .5f, 0, 0), -90, d - t * 2, h, t, outer, cream, trim, side);
        SBox(club, null, "ClubFloor", new(ix * 2, .02f, iz * 2), new(0, .011f, 0), floorMat, shadow: false);
        SBox(club, null, "HallCeiling", new(wingX * 2 - .1f, .08f, foyerZ + iz), new(0, h - .04f, (foyerZ - iz) * .5f), Mat("f1ead6", "plaster"), shadow: false);
        SBox(club, null, "FoyerCeiling", new(ix * 2, .08f, iz - foyerZ), new(0, foyerH - .04f, (foyerZ + iz) * .5f), cream, shadow: false);
        foreach (var wingSign in new[] { -1f, 1f })
            SBox(club, null, "WingCeiling", new(ix - wingX, .08f, iz * 2), new(wingSign * (ix + wingX) * .5f, wingH - .04f, 0), cream, shadow: false);

        // ---- partitions ---------------------------------------------------------------------------
        // Foyer / hall wall: the double door and two side doors.
        SWall(club, "FoyerWall", new(0, 0, foyerZ), 180, wingX * 2, h, .3f, cream, green, trim,
            new List<Opening> { new(0, 0, 2.4f, 2.7f, Door: true), new(-4.2f, 0, 1.1f, 2.2f, Door: true), new(4.2f, 0, 1.1f, 2.2f, Door: true) });
        // Foyer wall pieces beyond the hall span (x beyond wings): solid.
        foreach (var s in new[] { -1f, 1f })
        {
            SWall(club, "FoyerWing", new(s * (ix + wingX) * .5f, 0, foyerZ), 180, ix - wingX, foyerH, .3f, cream, cream, trim, new List<Opening>());
            var wingWall = SWall(club, "WingWall", new(s * wingX, 0, 0), s > 0 ? 90 : -90, iz * 2, h, .3f, cream, cream, trim, new List<Opening>
            {
                new(s > 0 ? -5.2f : 5.2f, 0, 1.0f, 2.2f, Door: true),     // foyer → wing front room (costume west / instruments east)
                new(s > 0 ? -1.6f : 1.6f, 0, 1.0f, 2.2f, Door: true),      // hall → same room
                new(s > 0 ? 4.5f : -4.5f, deck, 1.0f, 2.2f, Door: true)    // stage → backstage
            });
            SBox(club, null, "WingCross", new(ix - wingX, wingH, .16f), new(s * (ix + wingX) * .5f, wingH * .5f, 0), cream);
            // Stage-side platform reached by backstage stairs.
            var stair = SBody(club, "BackstageStairBody");
            for (var i = 0; i < 7; i++)
            {
                var top = (i + 1) * .17f;
                SBox(club, stair, $"BackStep{s}_{i}", new(.3f, top, 1.4f), new(s * (ix - .3f - i * .3f), top * .5f, -4.6f), Mat("7d6548", "wood_furniture"));
            }
            SBox(club, stair, $"BackPlatform{s}", new(ix - wingX - 2.4f, deck, 2.2f), new(s * (wingX + (ix - wingX - 2.4f) * .5f), deck * .5f, -4.6f), Mat("7d6548", "wood_furniture"));
            SBox(club, stair, $"WingBackFloor{s}", new(ix - wingX, .04f, 4.8f), new(s * (ix + wingX) * .5f, .02f, -4.3f), floorMat, shadow: false);
        }
        // Wing back rooms: west has the stairs at x -8.3..; keep the pass to the deck clear.

        // ---- lights ---------------------------------------------------------------------------------------
        foreach (var (x, z, e) in new[] { (-2.8f, 0.6f, 1.6f), (2.8f, 0.6f, 1.6f), (0f, 2.6f, 1.4f) })
            AddSquareLight(club, new(x, h - .45f, z), e, 11f, "ffe2b0");
        AddSquareLight(club, new(0, 4.4f, -4.6f), 1.5f, 9f, "ffd9a0");
        AddSquareLight(club, new(0, foyerH - .45f, 5.2f), 1.3f, 8f, "ffd9a0");
        foreach (var s in new[] { -1f, 1f })
        {
            AddSquareLight(club, new(s * 7.1f, wingH - .35f, 3.4f), 1.1f, 6f, "ffd9a0");
            AddSquareLight(club, new(s * 7.1f, wingH - .35f, -3.5f), .8f, 6f, "ffd0a0");
        }

        BuildClubFoyer(club, body, trim);
        BuildClubHall(club, body, trim, h, deck, stageZ);
        BuildClubWings(club, body, trim);
        club.SetMeta("interior", "house of culture; presentation and physics only");
    }

    private void BuildClubFoyer(Node3D club, StaticBody3D body, Material trim)
    {
        // Cloakroom counter with numbered tokens, ticket window, poster boards.
        SBox(club, body, "CloakCounter", new(3.0f, 1.05f, .5f), new(-5.4f, .52f, 5.9f), Mat("7d6548", "wood_furniture"));
        for (var i = 0; i < 12; i++)
            SBox(club, null, "Token" + i, new(.07f, .07f, .01f), new(-6.6f + i * .22f, 1.09f, 5.7f), Mat("c9953f", "metal"), shadow: false);
        SLabel(club, "ГАРДЕРОБ · ЧИК", new(-5.4f, 2.3f, 6.55f), 180, 30, new Color(.25f, .2f, .15f));
        SBox(club, body, "CoatRack", new(2.8f, 1.9f, .1f), new(-5.4f, .95f, 6.55f), Mat("5a4433", "wood"));
        SBox(club, body, "TicketBooth", new(1.4f, 1.6f, .8f), new(5.6f, .8f, 6.0f), Mat("8a6a48", "wood_furniture"));
        SLabel(club, "КАССА", new(5.6f, 1.95f, 5.58f), 0, 28, new Color(.25f, .2f, .15f));
        SPicture(club, "Sabantuy2005", new(-2.6f, 1.75f, 4.05f), 0, new(1.5f, 1.0f), "res://assets/images/council-sabantuy-2005.png", "8d8672", "Сабантуй · 2005");
        SPicture(club, "ConcertBill", new(2.6f, 1.75f, 4.05f), 0, new(1.0f, 1.4f), null, "d9b44a", null);
        SLabel(club, "КИЧӘ · ВЕЧЕР\nСубботний концерт\nвход свободный", new(2.6f, 1.85f, 4.08f), 0, 24, new Color(.5f, .12f, .08f));
        // Tulip ornament frieze.
        for (var i = 0; i < 26; i++)
        {
            var x = -8.3f + i * .64f;
            SBox(club, null, "Tulip" + i, new(.16f, .22f, .02f), new(x, 2.85f, 6.5f), Mat(i % 2 == 0 ? "b8412f" : "4c6b48", "cloth"), shadow: false);
        }
        SBox(club, body, "FoyerBench", new(1.6f, .5f, .4f), new(2.9f, .25f, 6.2f), Mat("8a6a48", "wood_furniture"));
        SquareLook(club, "ClubSabantuyBoard", "Осмотреть фотографию", new(-2.6f, 1.75f, 4.1f), new(1.7f, 1.2f, .3f),
            "Тот же Сабантуй-2005, что и в альбоме сельсовета: люди у сцены, шест, дети в первом ряду. Кадр сняли с этого самого места.");
    }

    private void BuildClubHall(Node3D club, StaticBody3D body, Material trim, float h, float deck, float stageZ)
    {
        var wood = Mat("7d6548", "wood_furniture");
        var red = Mat("7a2a2a", "fabric");
        var stage = SBody(club, "StageBody");
        SBox(club, stage, "StageDeck", new(10.5f, deck, 4.2f), new(0, deck * .5f, stageZ - 2.1f), wood);
        for (var i = 0; i < 10; i++)
            SBox(club, null, "StagePlank" + i, new(.012f, .004f, 4.15f), new(-4.7f + i * 1.05f, deck + .003f, stageZ - 2.1f), Mat("574632", "wood_furniture"), shadow: false);
        SBox(club, null, "StageFront", new(10.5f, .4f, .06f), new(0, .9f, stageZ + .03f), Mat("5d3a2f", "wood_furniture"), shadow: false);
        // Proscenium with ornament, curtains, backdrop.
        SBox(club, null, "ProsceniumBeam", new(10.5f, 1.5f, .35f), new(0, h - .9f, stageZ - .1f), Mat("efe3c8", "plaster"));
        for (var i = 0; i < 17; i++)
            SBox(club, null, "ProsceniumTulip" + i, new(.3f, .5f, .02f), new(-4.8f + i * .6f, h - 1.05f, stageZ + .09f), Mat(i % 2 == 0 ? "b8412f" : "4c6b48", "cloth"), shadow: false);
        foreach (var s in new[] { -1f, 1f })
        {
            SBox(club, null, "Curtain" + s, new(1.5f, 4.4f, .3f), new(s * 4.3f, deck + 2.3f, stageZ - .35f), red);
            for (var i = 0; i < 6; i++)
                SBox(club, null, $"CurtainFold{s}_{i}", new(.1f, 4.4f, .12f), new(s * (3.65f + i * .26f), deck + 2.3f, stageZ - .18f), Mat("8f3838", "fabric"), shadow: false);
        }
        SBox(club, null, "Backdrop", new(10.3f, 4.6f, .05f), new(0, deck + 2.4f, -6.55f), Mat("9db9c9", "cloth"), shadow: false);
        SBox(club, null, "BackdropHill", new(10.3f, 1.4f, .06f), new(0, deck + .8f, -6.5f), Mat("8ba36b", "cloth"), shadow: false);
        SLabel(club, "САБАНТУЙ", new(0, deck + 3.3f, -6.47f), 0, 90, new Color(.7f, .15f, .1f));
        SPicture(club, "StageTukay", new(0, 5.85f, stageZ + .1f), 0, new(.6f, .8f), null, "6b5a4a", null);
        // Flags on stage right and left.
        foreach (var (x, c1, c2, c3) in new[] { (-4.7f, "2e8b3d", "ffffff", "c8322b"), (4.7f, "ffffff", "2a4fa8", "c8322b") })
        {
            SBox(club, null, "FlagPole", new(.04f, 3.4f, .04f), new(x, deck + 1.7f, -6.3f), Mat("8a8a86", "metal"), shadow: false);
            foreach (var (i, c) in new[] { c1, c2, c3 }.Select((c, i) => (i, c)))
                SBox(club, null, "FlagBand" + i, new(.9f, .2f, .02f), new(x + (x < 0 ? .45f : -.45f), deck + 3.1f - i * .2f, -6.3f), Mat(c, "cloth"), shadow: false);
        }
        // Piano, microphone, loudspeakers.
        SBox(club, stage, "UprightPiano", new(1.5f, 1.25f, .6f), new(3.4f, deck + .62f, -5.6f), Mat("2a2420", "wood_furniture"));
        SBox(club, null, "PianoKeys", new(1.3f, .04f, .3f), new(3.4f, deck + .8f, -5.25f), Mat("efe9dc", "plastic_abs"), shadow: false);
        SBox(club, null, "MicStand", new(.03f, 1.5f, .03f), new(-1.2f, deck + .75f, stageZ - 1.3f), Mat("222222", "metal"), shadow: false);
        foreach (var s in new[] { -1f, 1f })
            SBox(club, body, "Speaker" + s, new(.6f, 1.0f, .5f), new(s * 5.05f, deck + .5f + 0f, stageZ - .5f), Mat("2b2b2b", "plastic_abs"));

        // Seats: seven rows in two blocks; tip-up wooden seats on a shared rail.
        var seats = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(.44f, .05f, .42f) }, InstanceCount = 0 };
        var seatBacks = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(.44f, .5f, .04f) }, InstanceCount = 0 };
        var seatT = new List<Transform3D>(); var backT = new List<Transform3D>();
        var rows = new[] { 3.0f, 2.15f, 1.3f, .45f, -.4f, -1.25f };
        foreach (var z in rows)
        foreach (var block in new[] { -1f, 1f })
            for (var i = 0; i < 8; i++)
            {
                var x = block * (.75f + i * .5f);
                seatT.Add(new(Basis.Identity, new(x, .46f, z)));
                backT.Add(new(Basis.Identity, new(x, .75f, z + .2f)));
            }
        seats.InstanceCount = seatT.Count; seatBacks.InstanceCount = backT.Count;
        for (var i = 0; i < seatT.Count; i++) { seats.SetInstanceTransform(i, seatT[i]); seatBacks.SetInstanceTransform(i, backT[i]); }
        club.AddChild(new MultiMeshInstance3D { Name = "HallSeats", Multimesh = seats, MaterialOverride = Mat("8a3b34", "fabric_upholstery") });
        club.AddChild(new MultiMeshInstance3D { Name = "HallSeatBacks", Multimesh = seatBacks, MaterialOverride = Mat("8a3b34", "fabric_upholstery") });
        foreach (var z in rows)
        foreach (var block in new[] { -1f, 1f })
            body.AddChild(new CollisionShape3D { Name = $"SeatRow{z}_{block}", Position = new(block * 2.5f, .35f, z + .05f), Shape = new BoxShape3D { Size = new(3.95f, .7f, .55f) } });
        SLabel(club, "ЗАЛ · 96 УРЫН\n(из них ходят — человек десять)", new(0, 4.6f, 3.7f), 180, 26, new Color(.3f, .25f, .2f));

        // Chandeliers.
        foreach (var (x, z) in new[] { (-2.8f, .6f), (2.8f, .6f), (0f, 2.6f) })
        {
            SBox(club, null, "ChandelierRod", new(.03f, 1.1f, .03f), new(x, h - 1.0f, z), Mat("2b2b2b", "metal"), shadow: false);
            club.AddChild(new MeshInstance3D { Name = "Chandelier", Mesh = new TorusMesh { InnerRadius = .35f, OuterRadius = .42f }, Position = new(x, h - 1.6f, z), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, .9f, .6f), EmissionEnabled = true, Emission = new Color(1f, .8f, .5f), EmissionEnergyMultiplier = .6f } });
        }
        // Radiators under the tall windows.
        foreach (var x in new[] { -6.75f, -2.25f, 2.25f, 6.75f })
            SBox(club, null, "Radiator", new(1.1f, .6f, .1f), new(x, .35f, 6.5f), Mat("e8e6de", "metal"), shadow: false);

        // Stage inspections.
        SquareLook(club, "ClubStagePlaque", "Прочитать табличку", new(-4.6f, deck + .9f, stageZ - .3f), new(.6f, .4f, .4f),
            "На портале табличка: «Сцену собрал Габдулла Сабиров, 1987». Доски пригнаны без единого гвоздя на виду.");
        SquareLook(club, "ClubStageBoard", "Постучать по половице", new(0f, deck, -4.3f), new(1.6f, .3f, 1.6f),
            "Под ногой половица отзывается гулко. Под сценой пусто — и пусто слишком глубоко для подпола.", "hollow_board");
    }

    private void BuildClubWings(Node3D club, StaticBody3D body, Material trim)
    {
        // West wing front: costume room (x -8.65 .. -5.5, z 0.4 .. 6.65).
        SBox(club, body, "SewingTable", new(1.3f, .78f, .6f), new(-7.6f, .39f, 5.8f), Mat("8a6a48", "wood_furniture"));
        SBox(club, null, "SewingMachine", new(.5f, .35f, .25f), new(-7.8f, .95f, 5.8f), Mat("1f1f1f", "metal"), shadow: false);
        SChair(club, "SewingChair", new(-7.6f, 0, 5.05f), 0);
        for (var i = 0; i < 2; i++)
        {
            var x = -8.0f + i * .0f; var z = 2.0f + i * 1.2f;
            var mannequin = new Node3D { Name = "Mannequin" + i, Position = new(-7.5f, 0, z) };
            club.AddChild(mannequin);
            SBox(mannequin, null, "Stand", new(.5f, .12f, .5f), new(0, .06f, 0), Mat("3b3b3b", "metal"));
            body.AddChild(new CollisionShape3D { Name = "MannequinShape" + i, Position = new(-7.5f, .9f, z), Shape = new BoxShape3D { Size = new(.5f, 1.8f, .5f) } });
            var dress = Mat(i == 0 ? "2f5f4a" : "8a3b34", "fabric");
            SBox(mannequin, null, "Torso", new(.34f, .62f, .2f), new(0, 1.3f, 0), dress);
            mannequin.AddChild(new MeshInstance3D { Name = "Skirt", Mesh = new CylinderMesh { TopRadius = .16f, BottomRadius = .32f, Height = .95f, RadialSegments = 12 }, Position = new(0, .6f, 0), MaterialOverride = dress });
            SBox(mannequin, null, "Neck", new(.08f, .12f, .08f), new(0, 1.66f, 0), Mat("d8c9ad", "plastic_abs"));
            mannequin.AddChild(new MeshInstance3D { Name = "Head", Mesh = new SphereMesh { Radius = .11f, Height = .24f }, Position = new(0, 1.8f, 0), MaterialOverride = Mat("d8c9ad", "plastic_abs") });
            mannequin.AddChild(new MeshInstance3D { Name = "Kalfak", Mesh = new CylinderMesh { TopRadius = .09f, BottomRadius = .12f, Height = .26f, RadialSegments = 10 }, Position = new(0, 1.98f, 0), MaterialOverride = Mat("b8412f", "cloth") });
        }
        SBox(club, body, "CostumeRack", new(.06f, 1.9f, 2.4f), new(-8.5f, .95f, 4.0f), Mat("5a4433", "wood"));
        for (var i = 0; i < 6; i++)
            SBox(club, null, "Costume" + i, new(.12f, 1.1f, .3f), new(-8.42f, 1.15f, 3.0f + i * .35f), Mat(new[] { "2f4f8a", "b8412f", "4c6b48", "d1b46a", "6b4f7a", "8a3b34" }[i], "fabric"), shadow: false);
        SBox(club, null, "CostumeTag", new(.1f, .1f, .01f), new(-7.5f, 1.35f, 2.13f), Mat("efe9dc", "cloth"), shadow: false);
        SquareLook(club, "ClubMannequinTag", "Прочитать бирку", new(-7.5f, 1.35f, 2.0f), new(.6f, .6f, .4f),
            "К платью пришита бирка: «Наҗия апа, 2003 — не трогать, ещё дошью». Иголка воткнута в подол.");

        // East wing front: instrument room (x 5.5 .. 8.65, z 0.4 .. 6.65).
        SChair(club, "AccordionChair", new(7.2f, 0, 5.4f), 200);
        SBox(club, null, "Accordion", new(.4f, .3f, .22f), new(7.2f, .62f, 5.4f), Mat("3a2f2a", "wood_furniture"), shadow: false);
        SBox(club, null, "AccordionBellows", new(.3f, .26f, .2f), new(7.2f, .62f, 5.22f), Mat("c9b98a", "cloth"), shadow: false);
        SBox(club, body, "InstrumentCases", new(.6f, .35f, 1.4f), new(8.3f, .18f, 3.0f), Mat("3b2f26", "wood_furniture"));
        SBox(club, null, "KubyzCase", new(.4f, .06f, 1.1f), new(8.3f, .38f, 3.0f), Mat("6b5a4a", "wood_furniture"), shadow: false);
        foreach (var z in new[] { 1.6f, 2.2f })
            SBox(club, null, "MusicStand", new(.5f, .04f, .3f), new(6.4f, 1.15f, z), Mat("2b2b2b", "metal"), shadow: false);
        SBox(club, body, "TowelPrizes", new(.5f, .8f, .5f), new(6.2f, .4f, 5.9f), Mat("e8e2d0", "cloth"));
        SquareLook(club, "ClubAccordion", "Осмотреть гармонь", new(7.2f, .65f, 5.4f), new(.6f, .5f, .5f),
            "Гармонь лежит на стуле. Мех тёплый — на нём недавно играли. Значит, сюда ещё кто-то заходит.");
        // Backstage rooms (x ±5.5..±8.65, z -6.65 .. -1.0): props.
        foreach (var s in new[] { -1f, 1f })
        {
            SBox(club, body, "PropTrestle" + s, new(1.6f, .8f, .7f), new(s * 8.1f, .4f, -1.9f), Mat("8a6a48", "wood_furniture"));
            SBox(club, body, "PropCrates" + s, new(.8f, .7f, .9f), new(s * 7.6f, .35f, -6.0f), Mat("a08560", "wood_furniture"));
        }
        SLabel(club, "КОСТЮМЕРНАЯ", new(-7.1f, 2.6f, 0.45f), 0, 24, new Color(.25f, .2f, .15f));
        SLabel(club, "ИНСТРУМЕНТЫ", new(7.1f, 2.6f, 0.45f), 0, 24, new Color(.25f, .2f, .15f));
    }
}
