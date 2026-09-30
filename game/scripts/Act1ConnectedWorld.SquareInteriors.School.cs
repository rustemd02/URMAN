using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Old sovkhoz school, 28 x 11 m, two floors of 3.4 m. Ground floor: corridor
    // along the square-side front, staff room, two classrooms, canteen and a
    // stair hall. First floor: corridor, six doors, one open school corner.
    private void BuildSchoolInterior(Node3D school, Material outer, Material trim)
    {
        const float w = 28f, d = 11f, fh = 3.4f, t = .35f;
        var ix = w * .5f - t; var iz = d * .5f - t;              // inner half sizes
        var cream = Mat("e4dfd1", "wall_institution");
        var mint = Mat("a6b4a4", "wall_institution");
        var floorMat = Mat("8f7551", "wood_floor_painted");
        var body = SBody(school, "SchoolFurnitureBody");
        const float corridorZ = 2.9f;                            // corridor partition

        // ---- outer walls (both floors in one run) -------------------------------------------
        var front = new List<Opening> { new(0, 0, 1.7f, 2.45f, Door: true) };
        var back = new List<Opening>();
        for (var i = 0; i < 9; i++)
        {
            var x = -w * .5f + w * (i + .5f) / 9f;
            if (Mathf.Abs(x) > 1.5f) front.Add(new(x, .9f, 1.15f, 1.5f, Lit: i is 2 or 3));
            front.Add(Mathf.Abs(x) > 1.5f ? new(x, fh + .9f, 1.15f, 1.5f, Boarded: i % 3 != 1) : new(0, fh + .9f, 1.7f, 1.5f));
            back.Add(new(x, .9f, 1.15f, 1.5f));
            back.Add(new(x, fh + .9f, 1.15f, 1.5f, Boarded: i % 4 == 2));
        }
        // Stair-hall window on the east end: the forest close to the school.
        var east = new List<Opening> { new(-1.2f, 1.2f, 1.2f, 1.6f), new(-1.2f, fh + 1.2f, 1.2f, 1.6f) };
        var west = new List<Opening> { new(-1.5f, 1.2f, 1.2f, 1.6f), new(-1.5f, fh + 1.2f, 1.2f, 1.6f) };
        SWall(school, "FrontWall", new(0, 0, d * .5f - t * .5f), 0, w, fh * 2, t, outer, cream, trim, front);
        SWall(school, "BackWall", new(0, 0, -d * .5f + t * .5f), 180, w, fh * 2, t, outer, cream, trim, back);
        SWall(school, "EastWall", new(w * .5f - t * .5f, 0, 0), 90, d - t * 2, fh * 2, t, outer, cream, trim, east);
        SWall(school, "WestWall", new(-w * .5f + t * .5f, 0, 0), -90, d - t * 2, fh * 2, t, outer, cream, trim, west);

        // ---- floors and ceilings ---------------------------------------------------------------
        SBox(school, null, "GroundFloor", new(ix * 2, .02f, iz * 2), new(0, .011f, 0), floorMat, shadow: false);
        // Linoleum in the corridor was replaced later than the classroom
        // floorboards. It also gives the entrance a distinct footstep surface.
        SBox(school, null, "CorridorLinoleum", new(ix * 2, .023f, iz - corridorZ),
            new(0, .027f, (iz + corridorZ) * .5f), Mat("b9b6a3", "floor_institution"), shadow: false);
        // First-floor slab with the stairwell hole (x 12.0..13.65, z -5.15..0.65).
        const float holeX0 = 12.0f, holeZ1 = .45f;
        SSlab(school, "SlabWest", new(holeX0 + ix, .3f, iz * 2), new((-ix + holeX0) * .5f, fh - .15f, 0), floorMat);
        SSlab(school, "SlabEastFront", new(ix - holeX0, .3f, iz - holeZ1), new((holeX0 + ix) * .5f, fh - .15f, (holeZ1 + iz) * .5f), floorMat);
        // Visible floor skin on the slab (same treatment as the ground floor).
        SBox(school, null, "UpperFloorWest", new(holeX0 + ix, .02f, iz * 2), new((-ix + holeX0) * .5f, fh + .011f, 0), floorMat, shadow: false);
        SBox(school, null, "UpperFloorEast", new(ix - holeX0, .02f, iz - holeZ1), new((holeX0 + ix) * .5f, fh + .011f, (holeZ1 + iz) * .5f), floorMat, shadow: false);
        SBox(school, null, "SlabUndersideWest", new(holeX0 + ix, .04f, iz * 2), new((-ix + holeX0) * .5f, fh - .32f, 0), cream, shadow: false);
        SBox(school, null, "SlabUndersideEastFront", new(ix - holeX0, .04f, iz - holeZ1), new((holeX0 + ix) * .5f, fh - .32f, (holeZ1 + iz) * .5f), cream, shadow: false);
        SBox(school, null, "UpperCeiling", new(ix * 2, .06f, iz * 2), new(0, fh * 2 - .03f, 0), cream, shadow: false);


        // ---- partitions ---------------------------------------------------------------------------
        var roomXs = new[] { -8.3f, -.5f, 7.3f, 10.3f };
        var doorX = new[] { -11.0f, -4.4f, 3.4f, 8.8f };
        for (var floor = 0; floor < 2; floor++)
        {
            var y = floor * fh;
            var openings = doorX.Select(x => new Opening(x, 0, 1.0f, 2.2f, Door: true)).ToList();
            openings.Add(new(12.0f, 0, 2.4f, 2.4f, Door: true));
            var partitionHeight = floor == 0 ? fh - .3f : fh;
            SWall(school, $"CorridorWall{floor}", new(0, y, corridorZ), 0, ix * 2, partitionHeight, .16f, mint, cream, trim, openings);
            foreach (var x in roomXs)
                SWall(school, $"RoomWall{floor}_{x}", new(x, y, (-iz + corridorZ - .08f) * .5f), 90, corridorZ - .08f + iz, partitionHeight, .16f, cream, cream, trim, new List<Opening>());
        }
        SBox(school, null, "StairHallFloor", new(ix - 10.38f, .02f, iz * 2), new((10.38f + ix) * .5f, .011f, 0), floorMat, shadow: false);

        // ---- lights -----------------------------------------------------------------------------------
        foreach (var floor in new[] { 0, 1 })
        {
            var y = floor * fh + fh - .42f;
            foreach (var x in new[] { -10.5f, -4.4f, 3.4f, 8.7f, 12.0f })
                SLight(school, new(x, y, -1.2f), floor == 0 ? .75f : .28f, 6.5f);
            foreach (var x in new[] { -9f, -3f, 3f, 9f })
                SLight(school, new(x, y, 4.0f), floor == 0 ? .7f : .26f, 6f);
        }

        // ---- stairs (20 solid steps, 17 cm rise, 28 cm run) ----------------------------------------------
        var stairBody = SBody(school, "SchoolStairBody");
        for (var i = 0; i < 20; i++)
        {
            var top = (i + 1) * .17f;
            SBox(school, stairBody, $"Step{i}", new(1.65f, top, .28f), new(12.83f, top * .5f, -5.15f + .14f + i * .28f), Mat("7d6548", "wood_furniture"));
        }
        SBox(school, stairBody, "StairRailUpper", new(.06f, 1.0f, 5.8f), new(11.95f, fh + .5f, -2.25f), Mat("5a4a3a", "wood"));
        SBox(school, stairBody, "StairRailLower", new(.06f, .95f, 5.6f), new(11.98f, 1.9f, -2.35f), Mat("5a4a3a", "wood"), new(Mathf.RadToDeg(Mathf.Atan2(3.4f, 5.6f)), 0, 0));

        // Sectional cast-iron heating under the windows, separate from the wall.
        for (var floor = 0; floor < 2; floor++)
        foreach (var x in new[] { -10.9f, -4.7f, 1.55f, 7.8f })
            RuralPropModels.Radiator(school, $"WindowRadiator{floor}_{x}", new(x, floor * fh + .13f, -4.96f));
        BuildSchoolGround(school, body, cream, trim, corridorZ);
        BuildSchoolUpper(school, body, cream, trim, corridorZ, fh);
        school.SetMeta("interior", "school ground floor + first floor; presentation and physics only");
    }

    private void BuildSchoolGround(Node3D school, StaticBody3D body, Material cream, Material trim, float corridorZ)
    {
        var gray = Mat("6f6b63", "stone");
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
        PublicDocument(school, "school-class-photo", new(-6.4f, 1.55f, corridorZ + .17f),
            Vector3.Zero, new(1.1f, .82f), image: "res://assets/images/school-class-2005.png");
        PublicDocument(school, "school-transport-notice", new(3.4f, 1.57f, 5.13f),
            new(0, 180, 0), new(.64f, .48f));
        SPicture(school, "HonourBoard", new(6.0f, 1.55f, corridorZ + .17f), 0, new(1.5f, 1.0f), null, "5f7462", null);
        SLabel(school, "МАКТАУ ТАКТАСЫ\nДОСКА ПОЧЁТА", new(6.0f, 2.2f, corridorZ + .2f), 0, 30, new Color(.95f, .9f, .75f));
        SLabel(school, "Часть рамок снята", new(6.0f, 1.55f, corridorZ + .2f), 0, 18, new Color(.9f, .88f, .8f));
        // Pencil height marks on the door frame of the first class (initials only).
        var marks = new Node3D { Name = "HeightMarks", Position = new(-2.95f + .02f, 0, corridorZ + .09f) };
        school.AddChild(marks);
        for (var i = 0; i < 6; i++)
            SBox(marks, null, "Mark" + i, new(.05f, .006f, .004f), new(i % 2 == 0 ? -.025f : .025f, .82f + i * .085f, 0), Mat(i % 2 == 0 ? "3a3a3a" : "4a3a2a", "stone"), shadow: false);
        SLabel(marks, "М.  А.", new(0, .74f, .01f), 0, 14, new Color(.22f, .2f, .18f), .003f);
        SquareLook(school, "SchoolHeightMarks", "Осмотреть косяк", marks.Position + new Vector3(0, 1.05f, .1f), new(.5f, 1.3f, .3f),
            "Карандашные отметки роста на косяке: «М.» и «А.». Двое стояли рядом и спорили, кто выше. «М.» — выше на палец.");
        SLabel(school, "УРМАН КАМИЛЛӘРЕ — КАРА-УРМАН МӘКТӘБЕ", new(-10.5f, 2.6f, corridorZ + .12f), 0, 22, new Color(.25f, .3f, .27f), .004f);

        // ---- Staff room: x -13.65 .. -8.38 -------------------------------------------------------------
        SDesk(school, body, "StaffDesk", new(-11.4f, 0, -3.9f), 0, 1.4f, .7f);
        SChair(school, "StaffChair", new(-11.4f, 0, -3.15f), 180);
        SDesk(school, body, "TeaTable", new(-9.2f, 0, .4f), 90, 1.0f, .7f);
        RuralPropModels.Teapot(school, "Kettle", new(-9.2f, .75f, .4f));
        SBox(school, null, "Register", new(.32f, .04f, .23f), new(-11.1f, .77f, -3.9f), Mat("3b5f4a", "cloth"), shadow: false);
        PublicDocument(school, "school-staff-note", new(-10.7f, .775f, -3.9f),
            Vector3.Zero, new(.39f, .28f), flat: true);
        SquareLook(school, "SchoolRegister", "Открыть журнал", new(-11.1f, .8f, -3.9f), new(.6f, .3f, .5f),
            "Классный журнал. На последней странице подряд одиннадцать фамилий. Раньше страницы кончались раньше, чем классы.");
        SShelf(school, body, "StaffBooks", new(-13.35f, 0, -1.3f), 90, 2.2f, 2.0f);
        for (var i = 0; i < 3; i++)
        {
            SBox(school, null, "GeraniumPot" + i, new(.16f, .16f, .16f), new(-13.0f + i * .0f, 1.2f, -3.1f + i * .55f), Mat("a2573d", "stone"), shadow: false);
            school.AddChild(new MeshInstance3D { Name = "Geranium" + i, Mesh = new SphereMesh { Radius = .17f, Height = .3f }, Position = new(-13.0f, 1.42f, -3.1f + i * .55f), MaterialOverride = Mat("3f6a3a", "foliage") });
        }
        SPicture(school, "StaffNotice", new(-11.4f, 1.7f, -5.05f), 0, new(.5f, .7f), null, "d9d0b8", null);

        // ---- Classrooms ------------------------------------------------------------------------------------
        BuildClassroom(school, body, -8.22f, -.58f, "1–4 класс", tukay: true, drawings: true);
        BuildClassroom(school, body, -.42f, 7.22f, "5–9 класс", tukay: false, drawings: false);

        // ---- Canteen: x 7.38 .. 10.22 ---------------------------------------------------------------------
        SDesk(school, body, "CanteenTable", new(8.8f, 0, -1.2f), 90, 2.2f, .8f);
        foreach (var z in new[] { -2.0f, -.4f })
        foreach (var x in new[] { 8.25f, 9.35f }) SChair(school, $"CanteenChair{z}_{x}", new(x, 0, z), x < 9 ? 90 : -90);
        SDesk(school, body, "SamovarTable", new(9.7f, 0, -4.7f), 0, .9f, .5f);
        RuralPropModels.Samovar(school, "Samovar", new(9.7f, .75f, -4.7f));
        SPicture(school, "CanteenMenu", new(8.8f, 1.6f, -5.03f), 0, new(1.2f, .8f), null, "e8e2d0", null);
        SLabel(school, "ЧӘЙ ВАКЫТЫ\nСуп · перемяч · чай", new(8.8f, 1.6f, -5.0f), 0, 20, new Color(.2f, .18f, .14f));
        // ---- Stair-hall wall: pencil "лесенка" and the window on the forest ---------------------------------
        SquareLook(school, "SchoolStairWindow", "Выглянуть в окно", new(13.4f, 1.9f, -1.2f), new(.5f, 1.5f, 1.4f),
            "Из окна лестницы лес виден вплотную: ели стоят почти у ограды. Другие здания площади он не так теснит — только школу.");
    }

    private void BuildClassroom(Node3D school, StaticBody3D body, float x0, float x1, string title, bool tukay, bool drawings)
    {
        var boardX = x0 + .09f;
        RuralPropGeometry.Block(school, "BoardBacking_" + title, new(.06f, 1.25f, 3.3f), new(boardX, 1.55f, -1.0f), RuralPropMaterials.Surface("wood"), .012f);
        RuralPropGeometry.Block(school, "BoardSheet_" + title, new(.018f, 1.1f, 3.1f), new(boardX + .038f, 1.55f, -1.0f), RuralPropMaterials.Surface("steel", "304839"), .004f);
        var boardMaterial = new StandardMaterial3D {
            ResourceName = "SchoolChalkboardUniqueFace", Roughness = .9f, Metallic = 0,
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://assets/textures/realism_20260929/school_green_chalkboard_v1_basecolor.png") };
        boardMaterial.SetMeta("surfaceUVContract", "one full unique bitmap on actual 3.1 x 1.1 m face; no repeated writing or painted frame");
        RuralPropGeometry.Part(school, "Chalkboard_" + title, new QuadMesh { Size = new(3.1f, 1.1f) }, new(boardX + .048f, 1.55f, -1), boardMaterial, new(0, 90, 0));
        var tray = RuralPropMaterials.Surface("metal");
        RuralPropGeometry.Block(school, "ChalkTrayBase_" + title, new(.14f, .012f, 3f), new(boardX + .075f, .96f, -1), tray, .004f);
        RuralPropGeometry.Block(school, "ChalkTrayLip_" + title, new(.012f, .035f, 3f), new(boardX + .14f, .973f, -1), tray, .004f);
        RuralPropGeometry.Tube(school, "WhiteChalk_" + title, new(boardX + .09f, .972f, -.85f), new(boardX + .09f, .972f, -.76f), .005f, RuralPropMaterials.Surface("concrete", "fffdf5"), 12);
        SLabel(school, title == "1–4 класс" ? "Ә ә   Ө ө   Ү ү\nҖ җ   Ң ң   Һ һ" : "Татар теле\nАлга таба!", new(boardX + .066f, 1.55f, -1.0f), 90, 46, new Color(.9f, .92f, .85f));
        SDesk(school, body, "TeacherDesk_" + title, new(x0 + 1.0f, 0, -1.0f), 90, 1.3f, .65f);
        SChair(school, "TeacherChair_" + title, new(x0 + .55f, 0, -1.0f), -90);
        for (var row = 0; row < 4; row++)
        for (var col = 0; col < 3; col++)
        {
            var x = x0 + 2.0f + row * 1.25f; var z = -3.9f + col * 2.35f;
            SDesk(school, body, $"PupilDesk_{title}_{row}{col}", new(x, 0, z), 90, 1.15f, .5f, .68f);
            SChair(school, $"PupilChair_{title}_{row}{col}", new(x + .42f, 0, z - .28f), -90, "6a8a72");
            SChair(school, $"PupilChair2_{title}_{row}{col}", new(x + .42f, 0, z + .28f), -90, "6a8a72");
        }
        if (tukay)
            SPicture(school, "TukayPortrait_" + title, new(boardX + .05f, 2.55f, -1.0f), 90, new(.5f, .65f), null, "6b5a4a", "Габдулла Тукай");
        SPicture(school, "Map_" + title, new(x1 - .1f, 1.6f, -1.0f), -90, new(1.4f, .95f), null, "a8b59a", null);
        SLabel(school, tukay ? "КАРТА · КАРА-УРМАН" : "ТАТАРСТАН", new(x1 - .12f, 1.6f, -1.0f), -90, 26, new Color(.2f, .25f, .18f));
        if (drawings)
        {
            for (var i = 0; i < 6; i++)
                SPicture(school, $"Drawing{i}", new(x0 + 2.1f + i * 1.0f, 1.65f, -5.0f), 0, new(.4f, .3f), null, i == 3 ? "3c4a3c" : (i % 2 == 0 ? "a8c48a" : "c8b06a"), null);
            SLabel(school, "УРМАН — балалар рәсемнәре\nЛес — рисунки детей", new(x0 + 4.1f, 2.2f, -5.02f), 0, 22, new Color(.25f, .22f, .18f));
            SquareLook(school, "SchoolDrawings", "Осмотреть рисунки", new(x0 + 5.1f, 1.65f, -4.95f), new(.7f, .5f, .4f),
                "Дети нарисовали лес: ёлки, снег, солнце. На одном листе у самой кромки стоит высокий тёмный человек. Подписи нет.");
        }
        SShelf(school, body, "ClassShelf_" + title, new(x1 - .2f, 0, -4.4f), -90, 1.6f, 1.7f);
    }

    private void BuildSchoolUpper(Node3D school, StaticBody3D body, Material cream, Material trim, float corridorZ, float fh)
    {
        var y = fh;
        var doorMat = Mat("6a5a45", "wood");
        string[] signs = { "УЧИТЕЛЬСКАЯ-2\nне открывать", "5 «А»\nтечёт крыша", "6 «Б»\nна ремонте", "КЛАДОВАЯ", "" };
        var doorXs = new[] { -11.0f, -4.4f, 3.4f, 8.8f };
        for (var i = 0; i < doorXs.Length; i++)
        {
            if (i == 1) continue;                                          // the open school corner
            SBox(school, body, "ClosedDoor" + i, new(1.0f, 2.2f, .06f), new(doorXs[i], y + 1.1f, corridorZ + .02f), doorMat);
            SLabel(school, signs[i], new(doorXs[i] + .0f, y + 1.55f, corridorZ + .09f), 0, 16, new Color(.2f, .18f, .15f), .004f);
        }
        // Upper corridor dressing.
        SPicture(school, "UpperMap", new(-8f, y + 1.6f, corridorZ + .17f), 0, new(1.6f, 1.1f), null, "b5b08a", null);
        SLabel(school, "КАРА-УРМАН\nстарый план", new(-8f, y + 1.6f, corridorZ + .2f), 0, 20, new Color(.35f, .12f, .1f));
        SLabel(school, "Проход закрыт", new(4.5f, y + 2.05f, corridorZ + .13f), 0, 22, new Color(.55f, .1f, .08f));
        for (var i = 0; i < 4; i++)
            SChair(school, "StackedChair" + i, new(5.5f + (i % 2) * .5f, i < 2 ? y : y + .45f, 4.7f), i * 15, "8a6a48");

        // Open school corner (room x -8.22 .. -.58).
        SShelf(school, body, "MuseumBooks", new(-8.0f, y, -3f), 90, 2.4f, 1.9f, .32f, new[] { "b8a06a", "8b6b4e", "a9b39a" });
        SBox(school, body, "MuseumCase", new(2.2f, .9f, .7f), new(-3.5f, y + .45f, -4.4f), Mat("7d6548", "wood_furniture"));
        SBox(school, null, "MuseumCaseGlass", new(2.1f, .5f, .6f), new(-3.5f, y + 1.05f, -4.4f), SquareGlass, shadow: false);
        for (var i = 0; i < 5; i++)
            SBox(school, null, "Medal" + i, new(.08f, .008f, .08f), new(-4.3f + i * .4f, y + .93f, -4.4f), Mat("c9953f", "metal"), shadow: false);
        SDesk(school, body, "MuseumTable", new(-3.5f, y, -1.6f), 0, 1.6f, .8f);
        SBox(school, null, "Kulmak", new(1.3f, .02f, .45f), new(-3.5f, y + .755f, -1.6f), Mat("e8e2d0", "cloth"), shadow: false);
        SBox(school, null, "KulmakStripe", new(1.3f, .022f, .06f), new(-3.5f, y + .756f, -1.6f), Mat("b8412f", "cloth"), shadow: false);
        SBox(school, body, "SpinningWheelBase", new(.5f, .5f, .3f), new(-1.1f, y + .25f, -4.6f), Mat("7d6548", "wood_furniture"));
        school.AddChild(new MeshInstance3D { Name = "SpinningWheel", Mesh = new TorusMesh { InnerRadius = .3f, OuterRadius = .34f }, Position = new(-1.1f, y + .7f, -4.6f), RotationDegrees = new(90, 0, 0), MaterialOverride = Mat("7d6548", "wood_furniture") });
        SPicture(school, "MuseumTowel", new(-5.0f, y + 1.7f, -5.0f), 0, new(.5f, 1.2f), null, "e8e2d0", null);
        SLabel(school, "Мәктәп почмагы\nШкольный уголок", new(-5.0f, y + 2.6f, -5.02f), 0, 24, new Color(.25f, .22f, .18f));
        SPicture(school, "MuseumEmptyFrame", new(-2.0f, y + 1.7f, -5.0f), 0, new(.9f, .65f), null, "b6ad98", "Снимок забрали для архива");
        SquareLook(school, "SchoolMuseumCase", "Осмотреть витрину", new(-3.5f, y + 1.0f, -4.4f), new(2.3f, .8f, .8f),
            "В витрине медали Сабантуя, вымпелы и значки. Дальше в ней пустое место: новые вещи сюда давно не приносили.");
        SquareLook(school, "SchoolUpperWindow", "Выглянуть в окно", new(-1.5f, y + 1.9f, -5.1f), new(1.4f, 1.3f, .4f),
            "С этажа лес виден над оградой целиком — тёмный, ровный, до самого неба. Ни одного огня.");
    }
}
