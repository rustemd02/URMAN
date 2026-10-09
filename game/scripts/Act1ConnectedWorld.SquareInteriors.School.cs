using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Compact village school: one storey, 20 x 11 m. A clear front corridor serves
    // the staff room, two small combined classes and the relocated school museum.
    // No scaling of desks, door clearances or interaction targets is involved.
    private void BuildSchoolInterior(Node3D school, Material outer, Material trim)
    {
        const float w = 20f, d = 11f, h = 3.4f, t = .35f, corridorZ = 2.9f;
        var ix = w * .5f - t; var iz = d * .5f - t;
        // P2: warm cream over Soviet school green; the corridor takes a taller green band.
        var cream = Mat("ebe2c8", "wall_institution");
        var wallDado = PainterlyMaterialLibrary.ForDadoWall("ebe2c8", "759a80", 1.4f);
        var mint = PainterlyMaterialLibrary.ForDadoWall("e3e5c9", "6c8f78", 1.5f);
        var body = SBody(school, "SchoolFurnitureBody");
        school.SetMeta("footstepSurface", "herringbone_parquet");
        school.SetMeta("woodCreak", true);
        school.SetMeta("interiorHalfSize", new Vector2(ix, iz));
        school.SetMeta("interiorCeiling", h);
        school.SetMeta("storeys", 1);
        InteriorReflectionProbes.Add(school, "SchoolReflectionProbe", new(0, h * .5f, 0), new(ix * 2, h, iz * 2));
        AddHallTrim(school, "SchoolHall", ix, iz, h, front: false);
        var front = new List<Opening> { new(0, 0, 1.7f, 2.45f, Door: true) };
        var back = new List<Opening>();
        foreach (var x in new[] { -8f, -5f, -2.5f, 2.5f, 5f, 8f })
            front.Add(new(x, .9f, 1.15f, 1.5f, Lit: true));
        foreach (var x in new[] { -8f, -4.8f, -2f, 1.3f, 3.8f, 8f })
            back.Add(new(x, .9f, 1.15f, 1.5f));
        var frontWall = SWall(school, "FrontWall", new(0, 0, d*.5f-t*.5f), 0, w, h, t, outer, wallDado, trim, front);
        SWall(school, "BackWall", new(0, 0, -d*.5f+t*.5f), 180, w, h, t, outer, wallDado, trim, back);
        SWall(school, "EastWall", new(w*.5f-t*.5f, 0, 0), 90, d-t*2, h, t, outer, wallDado, trim,
            new List<Opening> { new(-3.9f, 1.1f, 1.0f, 1.4f), new(3.1f, 1.1f, 1.15f, 1.4f) });
        SWall(school, "WestWall", new(-w*.5f+t*.5f, 0, 0), -90, d-t*2, h, t, outer, wallDado, trim,
            new List<Opening> { new(-1.5f, 1.1f, 1.15f, 1.4f) });
        // Painted red-brown floor boards: the same textured planking, tinted.
        var floorPaint = (StandardMaterial3D)CivicSurfaceLibrary.Parquet().Duplicate();
        floorPaint.AlbedoColor = new Color(1f, .66f, .52f);
        CivicSurfaceLibrary.Floor(school, "GroundFloor", new(ix*2, iz*2), new(0,.002f,0)).MaterialOverride = floorPaint;
        SBox(school, null, "SchoolCeiling", new(ix*2,.08f,iz*2), new(0,h-.04f,0), cream, shadow:false);
        var corridorWall = SWall(school, "CorridorWall0", new(0,0,corridorZ), 0, ix*2, h, .16f, mint, wallDado, trim,
            new List<Opening> { new(-7.8f,0,1.1f,2.2f,Door:true), new(-2.5f,0,1.1f,2.2f,Door:true),
                new(2.5f,0,1.1f,2.2f,Door:true), new(6.4f,0,1.1f,2.2f,Door:true) });
        ToneWallSkins(corridorWall);   // the corridor face is the skin
        // Moulding line on the corridor dado top, split around the four class doors (visual only).
        var rail = Mat("efe6cc", "wood_painted_trim");
        foreach (var (x0, x1) in new[] { (-9.6f, -8.4f), (-7.2f, -3.1f), (-1.9f, 1.9f), (3.1f, 5.8f), (7.0f, 9.6f) })
            SBox(school, null, $"CorridorDadoRail{x0}", new(x1 - x0, .06f, .04f), new((x0 + x1) * .5f, 1.53f, corridorZ + .10f), rail, shadow: false);
        foreach (var x in new[] { -6.2f, -.5f, 5.2f })
            ToneWallSkins(SWall(school,"RoomWall0_"+x,new(x,0,(-iz+corridorZ-.08f)*.5f),90,corridorZ-.08f+iz,h,.16f,
                wallDado,wallDado,trim,new List<Opening>()));
        var lamps = new List<VisualInstance3D>();
        foreach (var x in new[] { -8f, -3.5f, 2.2f, 7.5f })
        {
            lamps.Add(SLight(school,new(x,h-.42f,-1.2f),.72f,5.5f,"ffcf9a"));
            lamps.Add(SLight(school,new(x,h-.42f,4f),.6f,5f,"ffcf9a"));
        }
        foreach (var x in new[] { -8f, -4.8f, 1.3f, 8f })
            RuralPropModels.Radiator(school,"WindowRadiator0_"+x,new(x,.13f,-4.96f));
        BuildSchoolGround(school,body,cream,trim,corridorZ);
        BuildSchoolMuseum(school,body);
        BuildSchoolDoorSet(school,frontWall,corridorWall,lamps);
        school.SetMeta("interior", "single-storey school; combined classes, staff tea corner and ground-floor museum");
    }

    private void BuildSchoolGround(Node3D school, StaticBody3D body, Material cream, Material trim, float corridorZ)
    {
        // Corridor: hook rail with a cloakroom bench between the two class doors,
        // class photo, honour board, notice board, radiator under the window.
        SBox(school, null, "HookRail", new(2.6f, .06f, .05f), new(-.5f + 0f, 1.35f, corridorZ + .12f), Mat("5a4433", "wood"), shadow: false);
        for (var i = 0; i < 9; i++)
        {
            var x = -1.7f + i * .29f;
            SBox(school, null, "Hook" + i, new(.025f, .06f, .08f), new(x, 1.33f, corridorZ + .16f), Mat("2b2b2b", "metal"), shadow: false);
            if (i is 1 or 2 or 4 or 6 or 7)
                SBox(school, null, "SmallCoat" + i, new(.24f, .55f, .07f), new(x, 1.0f, corridorZ + .19f), Mat(i % 2 == 0 ? "5c6b7a" : "7a5c4d", "fabric"), shadow: false);
        }
        // The blue mitten pair on its string (a reference, not a clue).
        var mitten = new Node3D { Name = "BlueMittenPair", Position = new(.95f, 1.13f, corridorZ + .2f) };
        school.AddChild(mitten);
        SBox(mitten, null, "MittenA", new(.09f, .19f, .04f), new(-.05f, 0, 0), Mat("2f4f8a", "fabric"), new(0, 0, 6), false);
        SBox(mitten, null, "MittenB", new(.09f, .19f, .04f), new(.05f, -.01f, 0), Mat("2f4f8a", "fabric"), new(0, 0, -6), false);
        SBox(mitten, null, "MittenString", new(.16f, .015f, .015f), new(0, .09f, 0), Mat("d8d2c4", "fabric"), shadow: false);
        SquareLook(school, "SchoolBlueMitten", "Осмотреть варежки", mitten.Position, new(.5f, .5f, .3f),
            "Синие варежки на верёвочке, детские. Крючок низкий — его повесили под чей-то рост. Чьи, никто не подписал.");
        RuralPropModels.Bench(school, "CloakBench", new(-.5f, 0, corridorZ + .50f), 0, 2.4f);
        body.AddChild(new CollisionShape3D { Name = "CloakBenchContact", Position = new(-.5f, .43f, corridorZ + .5f), Shape = new BoxShape3D { Size = new(2.4f, .09f, .42f) } });
        PublicDocument(school, "school-class-photo", new(-5.6f, 1.55f, corridorZ + .17f),
            Vector3.Zero, new(1.1f, .82f), image: "res://assets/images/school-class-2005.png");
        PublicDocument(school, "school-transport-notice", new(1.25f, 1.85f, corridorZ + .17f),
            Vector3.Zero, new(.64f, .48f));
        SPicture(school, "HonourBoard", new(8.0f, 1.55f, corridorZ + .17f), 0, new(2.2f, .55f), null, "5f7462", null);
        SLabel(school, "МАКТАУ ТАКТАСЫ\nДОСКА ПОЧЁТА", new(8.0f, 2.2f, corridorZ + .2f), 0, 30, new Color(.95f, .9f, .75f));
        SLabel(school, "Часть рамок снята", new(8.0f, 1.55f, corridorZ + .2f), 0, 18, new Color(.9f, .88f, .8f));
        // Pencil marks are a unique generated wood-face, kept on the actual jamb.
        var marks = new Node3D { Name="HeightMarks",Position=new(-3.07f,0,corridorZ+.09f) };school.AddChild(marks);
        var growthMat=(StandardMaterial3D)CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,6).Duplicate();
        growthMat.Uv1Scale=new(.28f/2,.976f/4,1);growthMat.Uv1Offset=new(.38f/2,(3+.012f)/4,0);
        CivicSurfaceLibrary.Paper(marks,"PencilGrowthMarks",new(.12f,.70f),new(0,1.04f,.012f),growthMat,0);
        marks.SetMeta("readableText","М. / А.");
        SquareLook(school, "SchoolHeightMarks", "Осмотреть косяк", marks.Position + new Vector3(0, 1.05f, .1f), new(.5f, 1.3f, .3f),
            "Карандашные отметки роста на косяке: «М.» и «А.». Двое стояли рядом и спорили, кто выше. «М.» — выше на палец.");
        SLabel(school, "УРМАН КАМИЛЛӘРЕ — КАРА-УРМАН МӘКТӘБЕ", new(-8.0f, 2.6f, corridorZ + .12f), 0, 22, new Color(.25f, .3f, .27f), .004f);

        // ---- Staff room: x -9.65 .. -6.28 -------------------------------------------------------------
        SDesk(school, body, "StaffDesk", new(-8.0f, 0, -3.9f), 0, 1.4f, .7f);
        SChair(school, "StaffChair", new(-8.0f, 0, -3.15f), 180);
        SDesk(school, body, "TeaTable", new(-7.1f, 0, .4f), 90, 1.0f, .7f);
        RuralPropModels.Teapot(school, "Kettle", new(-7.1f, .75f, .4f));
        RuralPropGeometry.Block(school,"Register",new(.32f,.04f,.23f),new(-8.0f,.77f,-3.9f),RuralPropMaterials.Surface("cloth"),.004f);
        RuralPropGeometry.Block(school,"JournalCover",new(.32f,.006f,.23f),new(-8.0f,.790f,-3.9f),RuralPropMaterials.Surface("upholstery","315442"),.002f);
        var journal=CivicSurfaceLibrary.Paper(school,"HandmadeJournalTitle",new(.28f,.07f),new(-8.0f,.795f,-3.9f),CivicSurfaceLibrary.Face("craft_details_v1_atlas.png",2,4,2),.001f);
        journal.RotationDegrees=new(-90,0,0);
        PublicDocument(school, "school-staff-note", new(-7.6f, .775f, -3.9f),
            Vector3.Zero, new(.39f, .28f), flat: true);
        SquareLook(school, "SchoolRegister", "Открыть журнал", new(-8.0f, .8f, -3.9f), new(.6f, .3f, .5f),
            "Классный журнал. На последней странице подряд одиннадцать фамилий. Раньше страницы кончались раньше, чем классы.");
        SShelf(school, body, "StaffBooks", new(-9.45f, 0, -1.3f), 90, 2.2f, 2.0f);
        for (var i = 0; i < 3; i++)
        {
            SBox(school, null, "GeraniumPot" + i, new(.16f, .16f, .16f), new(-9.1f + i * .0f, 1.2f, -3.1f + i * .55f), Mat("a2573d", "stone"), shadow: false);
            school.AddChild(new MeshInstance3D { Name = "Geranium" + i, Mesh = new SphereMesh { Radius = .17f, Height = .3f }, Position = new(-9.1f, 1.42f, -3.1f + i * .55f), MaterialOverride = Mat("3f6a3a", "foliage") });
        }
        SPicture(school, "StaffNotice", new(-6.30f, 1.7f, -2.4f), -90, new(.7f, .35f), null, "d9d0b8", null);

        // ---- Classrooms ------------------------------------------------------------------------------------
        BuildClassroom(school, body, -6.12f, -.58f, "1–4 класс", tukay: true, drawings: true);
        BuildClassroom(school, body, -.42f, 5.12f, "5–9 класс", tukay: false, drawings: false);

        SLabel(school,"1–4 класс",new(-2.5f,2.45f,corridorZ+.12f),0,22,new(.2f,.18f,.15f));
        SLabel(school,"5–9 класс",new(2.5f,2.45f,corridorZ+.12f),0,22,new(.2f,.18f,.15f));

        // Tea corner occupies the staff room; the former canteen bay is now the museum.
        SDesk(school, body, "SamovarTable", new(-7.1f, 0, -4.6f), 0, .9f, .5f);
        RuralPropModels.Samovar(school, "Samovar", new(-7.1f, .75f, -4.6f));
        SPicture(school, "CanteenMenu", new(-6.30f, 1.6f, -.4f), -90, new(.7f, .35f), null, "e8e2d0", null);
        // Stable inspection ID remains, but this is now the end of the single-storey corridor.
        SquareLook(school, "SchoolStairWindow", "Выглянуть в окно", new(9.4f, 1.9f, 3.9f), new(.5f, 1.5f, 1.4f),
            "Из окна в конце коридора лес виден вплотную: ели стоят почти у ограды.");
    }

    private void BuildClassroom(Node3D school, StaticBody3D body, float x0, float x1, string title, bool tukay, bool drawings)
    {
        var boardX = x0 + .09f;
        RuralPropGeometry.Block(school, "BoardBacking_" + title, new(.06f, 1.25f, 3.3f), new(boardX, 1.55f, -1.0f), RuralPropMaterials.Surface("wood"), .012f);
        RuralPropGeometry.Block(school, "BoardSheet_" + title, new(.018f, 1.1f, 3.1f), new(boardX + .038f, 1.55f, -1.0f), RuralPropMaterials.Surface("steel", "304839"), .004f);
        var boardMaterial = new StandardMaterial3D {
            ResourceName = "SchoolChalkboardUniqueFace", Roughness = .9f, Metallic = 0,
            AlbedoTexture = ResourceLoader.Load<Texture2D>(CivicSurfaceLibrary.Root + "chalkboard_lessons_v1_atlas.png"),
            Uv1Scale = new(1,.5f,1), Uv1Offset = new(0,title == "1–4 класс" ? 0 : .5f,0) };
        boardMaterial.SetMeta("surfaceUVContract", "one full unique bitmap on actual 3.1 x 1.1 m face; no repeated writing or painted frame");
        RuralPropGeometry.Part(school, "Chalkboard_" + title, new QuadMesh { Size = new(3.1f, 1.1f) }, new(boardX + .048f, 1.55f, -1), boardMaterial, new(0, 90, 0));
        // Varnished casing: top ledge and two side stiles stand proud of the slate so the board has depth.
        var casing = RuralPropMaterials.Surface("wood", "e0b27c");
        RuralPropGeometry.Block(school, "BoardLedge_" + title, new(.10f, .045f, 3.42f), new(boardX + .05f, 2.2f, -1.0f), casing, .008f);
        foreach (var zs in new[] { -1f, 1f })
            RuralPropGeometry.Block(school, $"BoardStile_{title}_{zs}", new(.06f, 1.25f, .11f), new(boardX + .05f, 1.55f, -1.0f + zs * 1.6f), casing, .006f);
        var tray = RuralPropMaterials.Surface("metal");
        RuralPropGeometry.Block(school, "ChalkTrayBase_" + title, new(.14f, .012f, 3f), new(boardX + .075f, .96f, -1), tray, .004f);
        RuralPropGeometry.Block(school, "ChalkTrayLip_" + title, new(.012f, .035f, 3f), new(boardX + .14f, .973f, -1), tray, .004f);
        RuralPropGeometry.Tube(school, "WhiteChalk_" + title, new(boardX + .09f, .972f, -.85f), new(boardX + .09f, .972f, -.76f), .005f, RuralPropMaterials.Surface("concrete", "fffdf5"), 12);

        SDesk(school, body, "TeacherDesk_" + title, new(x0 + 1.0f, 0, -1.0f), 90, 1.3f, .65f);
        SChair(school, "TeacherChair_" + title, new(x0 + .55f, 0, -1.0f), -90);
        for (var row = 0; row < 3; row++)
        for (var col = 0; col < 2; col++)
        {
            var x = x0 + 1.8f + row * 1.15f; var z = -3.6f + col * 3.0f;
            SDesk(school, body, $"PupilDesk_{title}_{row}{col}", new(x, 0, z), 90, 1.15f, .5f, .68f);
            SChair(school, $"PupilChair_{title}_{row}{col}", new(x + .42f, 0, z - .28f), -90, "6a8a72");
            SChair(school, $"PupilChair2_{title}_{row}{col}", new(x + .42f, 0, z + .28f), -90, "6a8a72");
        }
        if (tukay)
            SPicture(school, "TukayPortrait_" + title, new(x0 + .02f, 2.55f, -1.0f), 90, new(.5f, .65f), null, "6b5a4a", "Габдулла Тукай");
        SPicture(school, "Map_" + title, new(x1 - .02f, 1.6f, -1.0f), -90, new(1.4f, .95f), null, "a8b59a", null);
        SLabel(school, tukay ? "КАРТА · КАРА-УРМАН" : "ТАТАРСТАН", new(x1 - .035f, 1.6f, -1.0f), -90, 26, new Color(.2f, .25f, .18f));
        if (drawings)
        {
            for (var i = 0; i < 6; i++)
                SPicture(school, $"Drawing{i}", new(x0 + .8f + (i % 3) * .75f, 1.4f + (i / 3) * .5f, 2.78f), 180, new(.4f, .3f), null, i == 3 ? "3c4a3c" : (i % 2 == 0 ? "a8c48a" : "c8b06a"), null);
            SLabel(school, "УРМАН — балалар рәсемнәре\nЛес — рисунки детей", new(x0 + 1.6f, 2.48f, 2.76f), 180, 22, new Color(.25f, .22f, .18f));
            SquareLook(school, "SchoolDrawings", "Осмотреть рисунки", new(x0 + 2.3f, 1.65f, 2.72f), new(.7f, .5f, .4f),
                "Дети нарисовали лес: ёлки, снег, солнце. На одном листе у самой кромки стоит высокий тёмный человек. Подписи нет.");
        }
        SShelf(school, body, "ClassShelf_" + title, new(x1 - .2f, 0, -4.4f), -90, 1.6f, 1.7f);
    }

    private void BuildSchoolMuseum(Node3D school, StaticBody3D body)
    {
        SShelf(school,body,"MuseumBooks",new(5.48f,0,-2.4f),90,2.1f,1.9f,.32f,new[]{"b8a06a","8b6b4e","a9b39a"});
        SBox(school,body,"MuseumCase",new(2.0f,.9f,.7f),new(7.5f,.45f,-4.4f),Mat("7d6548","wood_furniture"));
        SBox(school,null,"MuseumCaseGlass",new(1.9f,.5f,.6f),new(7.5f,1.05f,-4.4f),SquareGlass,shadow:false);
        for(var i=0;i<5;i++)
            SBox(school,null,"Medal"+i,new(.08f,.008f,.08f),new(6.7f+i*.4f,.93f,-4.4f),Mat("c9953f","metal"),shadow:false);
        SDesk(school,body,"MuseumTable",new(7.7f,0,-1.3f),0,1.5f,.7f);
        CivicSurfaceLibrary.HangingTextile(school,"MuseumTowel",new(.5f,1.2f),new(5.33f,1.7f,.7f),
            CivicSurfaceLibrary.Face("museum_towel_v1_basecolor.png",roughness:.97f));
        var towel=school.GetNode<Node3D>("MuseumTowel"); towel.RotationDegrees=new(0,90,0);
        RuralPropGeometry.Tube(school,"TowelDisplayRod",new(5.32f,2.32f,.4f),new(5.32f,2.32f,1f),.012f,RuralPropMaterials.Surface("wood"));
        SLabel(school,"Мәктәп почмагы\nШкольный уголок",new(7.5f,2.6f,2.76f),180,24,new(.25f,.22f,.18f));
        SPicture(school,"MuseumEmptyFrame",new(9.63f,1.7f,.7f),-90,new(.9f,.65f),null,"b6ad98","Снимок забрали для архива");
        SquareLook(school,"SchoolMuseumCase","Осмотреть витрину",new(7.5f,1f,-4.4f),new(2.1f,.8f,.8f),
            "В витрине медали Сабантуя, вымпелы и значки. Дальше в ней пустое место: новые вещи сюда давно не приносили.");
        SquareLook(school,"SchoolUpperWindow","Выглянуть в окно",new(9.4f,1.9f,-3.1f),new(.4f,1.3f,1.4f),
            "За школьной оградой стоит тёмный лес — ровный, до самого неба. Ни одного огня.");
    }

    // Entrance and classroom doors (author 2026-10-04). The entrance is a real
    // double door opening outward under the porch canopy; the school's closure
    // announcement is pinned to the left leaf and swings with it. Classroom
    // doors open into their rooms, where the swing reaches no desk, drawing or
    // shelf; the corridor side keeps its hooks, bench and wall notices clear.
    private void BuildSchoolDoorSet(Node3D school, Node3D frontWall, Node3D corridorWall, List<VisualInstance3D> lamps)
    {
        var entrance = SquareInteriorDoor(frontWall, "SquareSchoolEntranceL", "square/school/entrance-l", 0, 1.7f, 2.45f, .35f, 1, -1, .75f);
        SquareInteriorDoor(frontWall, "SquareSchoolEntranceR", "square/school/entrance-r", 0, 1.7f, 2.45f, .35f, 1, 1, .75f);
        // The sheet is physical and moves with its leaf; the single readable copy
        // of this text stays the existing school-transport-notice document, so the
        // paper opens nothing and adds no journal entry.
        var notice = CivicSurfaceLibrary.Paper(entrance.Hinge, "SquareSchoolEntranceNotice", new(.24f, .32f),
            new(-.036f, 1.5f, .40f), CivicSurfaceLibrary.Face("quest_papers_v2_atlas.png", 2, 2, 0), .003f);
        notice.RotationDegrees = new(0, -90, 3.5f);
        notice.SetMeta("readableText", "Для родителей. Занятия в нашей школе прекращены: детей осталось мало. Оставшихся школьников возят учиться в соседнее село.");
        notice.SetMeta("physicalDocumentPolicy", "announcement pinned on the entrance leaf; the single readable document remains urman.chapter1:document/school-transport-notice");
        foreach (var (x, name, key) in new[]
        {
            (-7.8f, "SquareSchoolStaffDoor", "square/school/staff"),
            (-2.5f, "SquareSchoolClassADoor", "square/school/class-1-4"),
            (2.5f, "SquareSchoolClassBDoor", "square/school/class-5-9"),
            (6.4f, "SquareSchoolMuseumDoor", "square/school/museum")
        })
            SquareInteriorDoor(corridorWall, name, key, x, 1.1f, 2.2f, .16f, -1, -1, .90f);
        SquareLightSwitch(school, "SquareSchoolLightSwitch", "school-light", new(1.3f, 1.42f, 5.1375f), lamps);
        EnsureFacilityTick();
    }
}
