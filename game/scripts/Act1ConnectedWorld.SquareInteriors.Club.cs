using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // A modest single-storey village club, 16 x 12 m with a 4.2 m ceiling.
    // A low stage, 48 seats, cloakroom and two working wings fit the actual shell.
    private void BuildClubInterior(Node3D club, Material outer, Material trim)
    {
        const float w = 16f, d = 12f, h = 4.2f, t = .35f, wingH = 3.4f, foyerH = 3.4f;
        var ix = w * .5f - t; var iz = d * .5f - t;
        var cream = Mat("e6dfd1", "wall_institution");
        var green = Mat("708b75", "wall_institution");
        club.SetMeta("footstepSurface", "herringbone_parquet"); club.SetMeta("woodCreak", true);
        var body = SBody(club, "ClubFurnitureBody");
        const float foyerZ = 2.85f, stageZ = -1.45f, wingX = 4.8f, deck = .6f;

        // ---- outer walls -----------------------------------------------------------------------
        var front = new List<Opening> { new(0, 0, 2.0f, 2.45f, Door: true) };
        var back = new List<Opening>();
        foreach (var x in new[] { -6.0f, -2.8f, 2.8f, 6.0f })
        {
            front.Add(new(x, .95f, 1.2f, 1.7f, Lit: x is > -3f and < 0f));
            back.Add(new(x, .95f, 1.2f, 1.7f));
        }
        var side = new List<Opening> { new(3.4f, .95f, 1.15f, 1.5f), new(-1.6f, .95f, 1.15f, 1.5f), new(-4.4f, .95f, 1.15f, 1.5f) };
        SWall(club, "FrontWall", new(0, 0, d * .5f - t * .5f), 0, w, h, t, outer, cream, trim, front);
        SWall(club, "BackWall", new(0, 0, -d * .5f + t * .5f), 180, w, h, t, outer, cream, trim, back);
        SWall(club, "EastWall", new(w * .5f - t * .5f, 0, 0), 90, d - t * 2, h, t, outer, cream, trim, side);
        SWall(club, "WestWall", new(-w * .5f + t * .5f, 0, 0), -90, d - t * 2, h, t, outer, cream, trim, side);
        CivicSurfaceLibrary.Floor(club,"ClubFloor",new(ix*2,iz*2),new(0,.002f,0));

        SBox(club, null, "HallCeiling", new(wingX * 2 - .1f, .08f, foyerZ + iz), new(0, h - .04f, (foyerZ - iz) * .5f), Mat("f1ead6", "plaster"), shadow: false);
        SBox(club, null, "FoyerCeiling", new(ix * 2, .08f, iz - foyerZ), new(0, foyerH - .04f, (foyerZ + iz) * .5f), cream, shadow: false);
        foreach (var wingSign in new[] { -1f, 1f })
            SBox(club, null, "WingCeiling", new(ix - wingX, .08f, iz * 2), new(wingSign * (ix + wingX) * .5f, wingH - .04f, 0), cream, shadow: false);

        // ---- partitions ---------------------------------------------------------------------------
        // Foyer / hall wall: the double door and two side doors.
        SWall(club, "FoyerWall", new(0, 0, foyerZ), 180, wingX * 2, h, .3f, cream, green, trim,
            new List<Opening> { new(0, 0, 2.4f, 2.7f, Door: true), new(-3.8f, 0, 1.1f, 2.2f, Door: true), new(3.8f, 0, 1.1f, 2.2f, Door: true) });
        // Foyer wall pieces beyond the hall span (x beyond wings): solid.
        foreach (var s in new[] { -1f, 1f })
        {
            SWall(club, "FoyerWing", new(s * (ix + wingX) * .5f, 0, foyerZ), 180, ix - wingX, foyerH, .3f, cream, cream, trim, new List<Opening>());
            var wingWall = SWall(club, "WingWall", new(s * wingX, 0, 0), s > 0 ? 90 : -90, iz * 2, h, .3f, cream, cream, trim, new List<Opening>
            {
                new(s > 0 ? -4.3f : 4.3f, 0, 1.0f, 2.2f, Door: true),     // foyer → wing front room (costume west / instruments east)
                new(s > 0 ? -1.6f : 1.6f, 0, 1.0f, 2.2f, Door: true),      // hall → same room
                new(s > 0 ? 3.6f : -3.6f, deck, 1.0f, 2.2f, Door: true)    // stage → backstage
            });
            SBox(club, null, "WingCross", new(ix - wingX, wingH, .16f), new(s * (ix + wingX) * .5f, wingH * .5f, 0), cream);
            // Stage-side platform reached by backstage stairs.
            var stair = SBody(club, "BackstageStairBody");
            stair.SetMeta("footstepSurface","herringbone_parquet");
            for (var i = 0; i < 4; i++)
            {
                var top = (i + 1) * .15f;
                SBox(club, stair, $"BackStep{s}_{i}", new(.36f, top, 1.2f), new(s * (ix - .3f - i * .36f), top * .5f, -3.6f), Mat("7d6548", "wood_furniture"));
                CivicSurfaceLibrary.Floor(club,$"BackStepParquet{s}_{i}",new(.36f,1.2f),new(s*(ix-.3f-i*.36f),top+.002f,-3.6f));
            }
            SBox(club, stair, $"BackPlatform{s}", new(ix - wingX - 1.6f, deck, 2.2f), new(s * (wingX + (ix - wingX - 1.6f) * .5f), deck * .5f, -3.6f), Mat("7d6548", "wood_furniture"));
            CivicSurfaceLibrary.Floor(club,$"BackLandingParquet{s}",new(ix-wingX-1.6f,2.2f),new(s*(wingX+(ix-wingX-1.6f)*.5f),deck+.002f,-3.6f));
        }
        // Keep both backstage passages clear of crates and equipment.

        // ---- lights ---------------------------------------------------------------------------------------
        foreach (var (x, z, e) in new[] { (-2.8f, 0.6f, 1.6f), (2.8f, 0.6f, 1.6f), (0f, 2.6f, 1.4f) })
            AddSquareLight(club, new(x, h - .45f, z), e, 11f, "ffe2b0");
        AddSquareLight(club, new(0, 3.65f, -3.6f), 1.1f, 9f, "ffd9a0");
        AddSquareLight(club, new(0, foyerH - .45f, 4.3f), 1.3f, 8f, "ffd9a0");
        foreach (var s in new[] { -1f, 1f })
        {
            AddSquareLight(club, new(s * 6.3f, wingH - .35f, 3.4f), 1.1f, 6f, "ffd9a0");
            AddSquareLight(club, new(s * 6.3f, wingH - .35f, -3.5f), .8f, 6f, "ffd0a0");
        }

        BuildClubFoyer(club, body, trim);
        BuildClubHall(club, body, trim, h, deck, stageZ);
        BuildClubWings(club, body, trim);
        club.SetMeta("interiorHalfSize",new Vector2(ix,iz));
        club.SetMeta("interiorCeiling",h);
        club.SetMeta("storeys",1);
        club.SetMeta("interior", "single-storey village club; 48 seats, low stage and two working wings");
    }

    private void BuildClubFoyer(Node3D club, StaticBody3D body, Material trim)
    {
        // Cloakroom counter with numbered tokens, ticket window, poster boards.
        SBox(club, body, "CloakCounter", new(2.4f, 1.05f, .5f), new(-6.2f, .52f, 4.9f), Mat("7d6548", "wood_furniture"));
        for (var i = 0; i < 12; i++)
            SBox(club, null, "Token" + i, new(.07f, .07f, .01f), new(-7.3f + i * .2f, 1.09f, 4.7f), Mat("c9953f", "metal"), shadow: false);
        SLabel(club, "ГАРДЕРОБ · ЧИК", new(-6.2f, 2.3f, 3.05f), 0, 30, new Color(.25f, .2f, .15f));
        SBox(club, body, "CoatRack", new(2.4f, 1.9f, .1f), new(-6.2f, .95f, 3.05f), Mat("5a4433", "wood"));
        SBox(club, body, "TicketBooth", new(1.4f, 1.6f, .8f), new(6.8f, .8f, 4.8f), Mat("8a6a48", "wood_furniture"));
        SLabel(club, "КАССА", new(6.8f, 1.95f, 4.38f), 0, 28, new Color(.25f, .2f, .15f));
        SPicture(club, "SabantuyPhoto", new(-2.25f, 1.75f, 3.05f), 0, new(1.4f, .93f), "res://assets/images/council-sabantuy-2005.png", "8d8672", "Сабантуй");
        SPicture(club, "ConcertBill", new(2.25f, 1.75f, 3.05f), 0, new(1.2f, .6f), null, "d9b44a", null);

        // A hand-painted folk border has real board thickness, not coloured blocks.
        RuralPropGeometry.Block(club,"FoyerFriezeBacking",new(14.6f,.22f,.025f),new(0,2.85f,5.5f),RuralPropMaterials.Surface("wood"),.003f);
        for(var i=0;i<8;i++)
            CivicSurfaceLibrary.Paper(club,"TulipFrieze"+i,new(1.825f,.20f),new(-6.3875f+i*1.825f,2.85f,5.52f),CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,0),0);
        var foyerBench=RuralPropModels.Bench(club,"FoyerBench",new(2.9f,0,5.2f),180,1.6f);
        RuralPropGeometry.AttachMemberContacts(foyerBench,body);
        SBox(club, null, "EntryMat", new(2.2f, .012f, .8f), new(0, .038f, 5.05f), Mat("646a60", "fabric"), shadow: false);
        // Photo sorting is a current use of the club, already mentioned in the
        // village archive. The equipment is kept on a side table, not on stage.
        SDesk(club, body, "ArchivePhotoTable", new(-2.5f, 0, 4.7f), 180, 1.3f, .65f);
        SBox(club, null, "ArchiveScanner", new(.45f, .10f, .32f), new(-2.5f, .8f, 4.7f), Mat("c4c4bd", "plastic_abs"), shadow: false);
        var envelope=CivicSurfaceLibrary.Paper(club,"ArchiveEnvelope",new(.34f,.085f),new(-3.05f,.78f,4.7f),CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,4),.002f);
        envelope.RotationDegrees=new(-90,0,0);
        SquareLook(club, "ClubSabantuyBoard", "Осмотреть фотографию", new(-2.25f, 1.75f, 3.1f), new(1.7f, 1.2f, .3f),
            "Тот же Сабантуй, что и в альбоме сельсовета: люди у сцены, шест, дети в первом ряду. Кадр сняли с этого самого места.");
    }

    private void BuildClubHall(Node3D club, StaticBody3D body, Material trim, float h, float deck, float stageZ)
    {
        var wood = Mat("7d6548", "wood_furniture");
        var red = RuralPropMaterials.Surface("velvet");
        var stage = SBody(club, "StageBody");
        stage.SetMeta("footstepSurface","herringbone_parquet");
        SBox(club, stage, "StageDeck", new(9.5f, deck, 4.2f), new(0, deck * .5f, stageZ - 2.1f), wood);
        CivicSurfaceLibrary.Floor(club,"StageParquet",new(9.5f,4.2f),new(0,deck+.002f,stageZ-2.1f));
        SBox(club, null, "StageFront", new(9.5f, .3f, .06f), new(0, .45f, stageZ + .03f), Mat("5d3a2f", "wood_furniture"), shadow: false);
        // Proscenium with ornament, curtains, backdrop.
        SBox(club, null, "ProsceniumBeam", new(9.5f, .48f, .28f), new(0, h - .36f, stageZ - .1f), Mat("efe3c8", "plaster"));
        for(var i=0;i<4;i++)
            CivicSurfaceLibrary.Paper(club,"PortalFolkOrnament"+i,new(2.375f,.30f),new(-3.5625f+i*2.375f,h-.36f,stageZ+.085f),CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,0),0);
        foreach (var s in new[] { -1f, 1f })
        {
            SCurtain(club, "PleatedStageCurtain" + s, new(s * 3.95f, deck + 1.45f, stageZ - .35f), 1.5f, 2.8f, red);
            SBox(club, null, "CurtainRail" + s, new(1.65f, .045f, .045f),
                new(s * 3.95f, deck + 2.91f, stageZ - .35f), Mat("494a47", "metal"), shadow: false);
        }
        CivicSurfaceLibrary.HangingTextile(club,"HandpaintedStageBackdrop",new(9.3f,2.325f),new(0,deck+1.7f,-5.50f),CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,1,.97f));
        SLabel(club, "САБАНТУЙ", new(0, deck + 2.55f, -5.47f), 0, 90, new Color(.7f, .15f, .1f));
        SPicture(club, "StageTukay", new(0, 3.25f, stageZ + .1f), 0, new(.6f, .8f), null, "6b5a4a", null);
        // Flags on stage right and left.
        foreach (var (x, c1, c2, c3) in new[] { (-4.2f, "2e8b3d", "ffffff", "c8322b"), (4.2f, "ffffff", "2a4fa8", "c8322b") })
        {
            SBox(club, null, "FlagPole", new(.04f, 2.8f, .04f), new(x, deck + 1.4f, -5.3f), Mat("8a8a86", "metal"), shadow: false);
            foreach (var (i, c) in new[] { c1, c2, c3 }.Select((c, i) => (i, c)))
                SBox(club, null, "FlagBand" + i, new(.9f, .2f, .02f), new(x + (x < 0 ? .45f : -.45f), deck + 2.55f - i * .2f, -5.3f), Mat(c, "cloth"), shadow: false);
        }
        // Piano, microphone, loudspeakers.
        SBox(club, stage, "UprightPiano", new(1.5f, 1.25f, .6f), new(3.4f, deck + .62f, -4.6f), Mat("2a2420", "wood_furniture"));
        SBox(club, null, "PianoKeys", new(1.3f, .04f, .3f), new(3.4f, deck + .8f, -4.25f), Mat("efe9dc", "plastic_abs"), shadow: false);
        SBox(club, null, "MicStand", new(.03f, 1.5f, .03f), new(-1.2f, deck + .75f, stageZ - 1.3f), Mat("222222", "metal"), shadow: false);
        foreach (var s in new[] { -1f, 1f })
            SBox(club, body, "Speaker" + s, new(.6f, 1.0f, .5f), new(s * 4.5f, deck + .5f + 0f, stageZ - .5f), Mat("2b2b2b", "plastic_abs"));

        // Four rows in two blocks. Distinct pads, armrests and floor-mounted
        // steel supports give 48 seats without unique meshes for every chair.
        var seatT = new List<Transform3D>(); var backT = new List<Transform3D>();
        var armT = new List<Transform3D>(); var supportT = new List<Transform3D>();
        var rows = new[] { 2.0f, 1.15f, .3f, -.55f };
        foreach (var z in rows)
        foreach (var block in new[] { -1f, 1f })
            for (var i = 0; i < 6; i++)
            {
                var x = block * (.75f + i * .5f);
                seatT.Add(new(new Basis(Vector3.Right, -Mathf.Pi * .5f), new(x, .46f, z)));
                backT.Add(new(new Basis(Vector3.Right, Mathf.DegToRad(-9)), new(x, .75f, z + .2f)));
                foreach (var side in new[] { -1f, 1f })
                {
                    armT.Add(new(Basis.Identity, new(x + side * .23f, .63f, z + .01f)));
                    supportT.Add(new(Basis.Identity, new(x + side * .23f, .32f, z + .05f)));
                }
            }
        void Instances(string name, Mesh mesh, List<Transform3D> transforms, Material material)
        {
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = mesh, InstanceCount = transforms.Count };
            for (var i = 0; i < transforms.Count; i++) multi.SetInstanceTransform(i, transforms[i]);
            club.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multi, MaterialOverride = material });
        }
        Instances("HallSeatPads", RuralPropGeometry.BowedPanel(.43f, .40f, .075f, .045f, .018f), seatT, RuralPropMaterials.Surface("upholstery"));
        Instances("HallBackPads", RuralPropGeometry.BowedPanel(.43f, .45f, .065f, .055f, .028f), backT, RuralPropMaterials.Surface("upholstery"));
        Instances("HallWoodArmrests", RuralPropGeometry.BevelBox(new(.055f, .045f, .42f), .012f), armT, RuralPropMaterials.Surface("wood"));
        Instances("HallSteelSupports", RuralPropGeometry.BevelBox(new(.027f, .63f, .027f), .007f), supportT, RuralPropMaterials.Surface("steel", "5e625e"));
        foreach (var z in rows)
        foreach (var block in new[] { -1f, 1f })
            body.AddChild(new CollisionShape3D { Name = $"SeatRow{z}_{block}", Position = new(block * 2.0f, .35f, z + .05f), Shape = new BoxShape3D { Size = new(2.95f, .7f, .55f) } });
        club.SetMeta("hallSeats", 48);

        // Chandeliers.
        foreach (var (x, z) in new[] { (-2.8f, .6f), (2.8f, .6f), (0f, 2.6f) })
        {
            SBox(club, null, "ChandelierRod", new(.03f, .4f, .03f), new(x, h - .35f, z), Mat("2b2b2b", "metal"), shadow: false);
            club.AddChild(new MeshInstance3D { Name = "Chandelier", Mesh = new TorusMesh { InnerRadius = .35f, OuterRadius = .42f }, Position = new(x, h - .6f, z), MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, .9f, .6f), EmissionEnabled = true, Emission = new Color(1f, .8f, .5f), EmissionEnergyMultiplier = .6f } });
        }
        // Radiators sit below the ordinary single-storey windows.
        foreach (var x in new[] { -6.0f, -2.8f, 2.8f, 6.0f })
            RuralPropModels.Radiator(club, "Radiator" + x, new(x, .13f, 5.45f), 1.1f, 180);

        // Actual timber uprights support the low stage portal and its maker plaque.
        foreach (var sideSign in new[] { -1f, 1f })
            SBox(club, stage, "ProsceniumPost" + sideSign, new(.26f,h-deck,.24f),
                new(sideSign*4.55f,(h+deck)*.5f,stageZ-.1f),wood);
        // Stage inscriptions are generated visual carriers with exact existing inspection semantics.
        CivicSurfaceLibrary.FramedFace(club,"StageMakerPlaque",new(-4.55f,deck+.9f,stageZ+.05f),0,new(.58f,.27f),CivicSurfaceLibrary.Face("notices_v2_atlas.png",2,4,5));
        // Stage inspections.
        SquareLook(club, "ClubStagePlaque", "Прочитать табличку", new(-4.55f, deck + .9f, stageZ + .1f), new(.6f, .4f, .4f),
            "На портале табличка мастера: «Сцену собрал Габдулла Сабиров». Доски пригнаны плотно, а у края уже заметен ремонт.");
        SquareLook(club, "ClubStageBoard", "Постучать по половице", new(0f, deck, -3.3f), new(1.6f, .3f, 1.6f),
            "Под ногой половица отзывается гулко. Под сценой пусто — и пусто слишком глубоко для подпола.", "hollow_board");
    }

    private void BuildClubWings(Node3D club, StaticBody3D body, Material trim)
    {
        // West wing front: costume room (x -7.65 .. -4.95, z 0.4 .. 5.65).
        SBox(club, body, "SewingTable", new(1.3f, .78f, .6f), new(-6.5f, .39f, 4.8f), Mat("8a6a48", "wood_furniture"));
        SBox(club, null, "SewingMachine", new(.5f, .35f, .25f), new(-6.6f, .95f, 4.8f), Mat("1f1f1f", "metal"), shadow: false);
        SChair(club, "SewingChair", new(-6.5f, 0, 4.05f), 0);
        for (var i = 0; i < 2; i++)
        {
            var x = -7.0f + i * .0f; var z = 2.0f + i * 1.2f;
            var mannequin = new Node3D { Name = "Mannequin" + i, Position = new(-6.4f, 0, z) };
            club.AddChild(mannequin);
            SBox(mannequin, null, "Stand", new(.5f, .12f, .5f), new(0, .06f, 0), Mat("3b3b3b", "metal"));
            body.AddChild(new CollisionShape3D { Name = "MannequinShape" + i, Position = new(-6.4f, .9f, z), Shape = new BoxShape3D { Size = new(.5f, 1.8f, .5f) } });
            var dress = Mat(i == 0 ? "2f5f4a" : "8a3b34", "fabric");
            SBox(mannequin, null, "Torso", new(.34f, .62f, .2f), new(0, 1.3f, 0), dress);
            mannequin.AddChild(new MeshInstance3D { Name = "Skirt", Mesh = new CylinderMesh { TopRadius = .16f, BottomRadius = .32f, Height = .95f, RadialSegments = 12 }, Position = new(0, .6f, 0), MaterialOverride = dress });
            SBox(mannequin, null, "Neck", new(.08f, .12f, .08f), new(0, 1.66f, 0), Mat("d8c9ad", "plastic_abs"));
            mannequin.AddChild(new MeshInstance3D { Name = "Head", Mesh = new SphereMesh { Radius = .11f, Height = .24f }, Position = new(0, 1.8f, 0), MaterialOverride = Mat("d8c9ad", "plastic_abs") });
            mannequin.AddChild(new MeshInstance3D { Name = "Kalfak", Mesh = new CylinderMesh { TopRadius = .09f, BottomRadius = .12f, Height = .26f, RadialSegments = 10 }, Position = new(0, 1.98f, 0), MaterialOverride = Mat("b8412f", "cloth") });
        }
        SBox(club, body, "CostumeRack", new(.06f, 1.9f, 2.4f), new(-7.5f, .95f, 4.0f), Mat("5a4433", "wood"));
        for (var i = 0; i < 6; i++)
            SBox(club, null, "Costume" + i, new(.12f, 1.1f, .3f), new(-7.42f, 1.15f, 3.0f + i * .35f), Mat(new[] { "2f4f8a", "b8412f", "4c6b48", "d1b46a", "6b4f7a", "8a3b34" }[i], "fabric"), shadow: false);
        CivicSurfaceLibrary.Paper(club,"CostumeTag",new(.20f,.10f),new(-6.4f,1.35f,2.13f),CivicSurfaceLibrary.Face("costume_tag_v1_basecolor.png"),.003f);
        SquareLook(club, "ClubMannequinTag", "Прочитать бирку", new(-6.4f, 1.35f, 2.0f), new(.6f, .6f, .4f),
            "К платью пришита бирка: «Наҗия апа — не трогать, ещё дошью». Иголка воткнута в подол.");

        // East wing front: instrument room (x 4.95 .. 7.65, z 0.4 .. 5.65).
        SChair(club, "AccordionChair", new(6.4f, 0, 4.4f), 200);
        SBox(club, null, "Accordion", new(.4f, .3f, .22f), new(6.4f, .62f, 4.4f), Mat("3a2f2a", "wood_furniture"), shadow: false);
        SBox(club, null, "AccordionBellows", new(.3f, .26f, .2f), new(6.4f, .62f, 4.22f), Mat("c9b98a", "cloth"), shadow: false);
        SBox(club, body, "InstrumentCases", new(.6f, .35f, 1.4f), new(7.3f, .18f, 3.0f), Mat("3b2f26", "wood_furniture"));
        SBox(club, null, "KubyzCase", new(.4f, .06f, 1.1f), new(7.3f, .38f, 3.0f), Mat("6b5a4a", "wood_furniture"), shadow: false);
        foreach (var z in new[] { 1.6f, 2.2f })
            SBox(club, null, "MusicStand", new(.5f, .04f, .3f), new(5.4f, 1.15f, z), Mat("2b2b2b", "metal"), shadow: false);
        SBox(club, body, "TowelPrizes", new(.5f, .8f, .5f), new(5.4f, .4f, 4.9f), Mat("e8e2d0", "cloth"));
        SquareLook(club, "ClubAccordion", "Осмотреть гармонь", new(6.4f, .65f, 4.4f), new(.6f, .5f, .5f),
            "Гармонь лежит на стуле. Мех тёплый — на нём недавно играли. Значит, сюда ещё кто-то заходит.");
        // Backstage rooms (x ±4.95..±7.65, z -5.65 .. -1.0): props.
        foreach (var s in new[] { -1f, 1f })
        {
            SBox(club, body, "PropTrestle" + s, new(1.6f, .8f, .7f), new(s * 6.5f, .4f, -1.9f), Mat("8a6a48", "wood_furniture"));
            SBox(club, body, "PropCrates" + s, new(.8f, .7f, .9f), new(s * 6.6f, .35f, -5.0f), Mat("a08560", "wood_furniture"));
        }
        SLabel(club, "КОСТЮМЕРНАЯ", new(-7.1f, 2.6f, 0.45f), 0, 24, new Color(.25f, .2f, .15f));
        SLabel(club, "ИНСТРУМЕНТЫ", new(7.1f, 2.6f, 0.45f), 0, 24, new Color(.25f, .2f, .15f));
    }
}
