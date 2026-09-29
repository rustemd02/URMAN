using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Village expansion, square slice (plan 2026-09-29, ТЗ04 R072/R159): the
    // former sovkhoz centre around the ring at (0.5, 186). Babai's tour says
    // the big school once had two hundred children, the post office is shut
    // and letters come with Razilya's bread, and the whole village gathers
    // here once a year for Sabantuy. Exterior architecture only: doors are
    // shut, no interior is promised. The working school, the council and the
    // shop keep their existing addresses in the old street.
    private static readonly Vector3 SquareRingCentre = new(.5f, 0, 186f);

    private void BuildSovkhozSquare(Node3D core)
    {
        var square = new Node3D { Name = "SovkhozSquare" };
        square.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        square.SetMeta("scope", "exterior only; closed doors; see docs/production/act1_village_expansion_plan_2026-09-29.md");
        core.AddChild(square);

        var plaster = PainterlyMaterialLibrary.ForColor("d9cdb4", "plaster");
        var brick = PainterlyMaterialLibrary.ForColor("9a5a45", "stone");
        var trim = PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim");

        // Old two-storey school: long, many windows, upper floor partly boarded.
        var school = SquareBuilding(square, "OldSchool", new(-3f, 204f), 28f, 11f, 3.4f, 2, PainterlyMaterialLibrary.ForColor("e2d6b8", "plaster"), roofPitch: 16f, hollow: true);
        SquarePorch(school, 11f, 3.2f, trim, open: true);
        BuildSchoolInterior(school, PainterlyMaterialLibrary.ForColor("e2d6b8", "plaster"), PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim"));
        SquareSign(school, new(0, 3.95f, 5.6f), "КАРА-УРМАН УРТА МӘКТӘБЕ\nКАРА-УРМАНСКАЯ СРЕДНЯЯ ШКОЛА", 62, new Color(.15f, .18f, .28f));
        SquareSign(school, new(-6.5f, 1.35f, 5.58f), "1974", 44, new Color(.35f, .3f, .25f), plate: false);

        // House of culture: a tall hall behind a four-column portico and pediment.
        var club = SquareBuilding(square, "HouseOfCulture", new(25f, 188f), 18f, 14f, 6.8f, 1, PainterlyMaterialLibrary.ForColor("e8dcc4", "plaster"), roofPitch: 12f, hollow: true);
        SquarePortico(club, 18f, 14f, 6.8f, trim, open: true);
        BuildClubInterior(club, PainterlyMaterialLibrary.ForColor("e8dcc4", "plaster"), PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim"));
        SquareSign(club, new(0, 7.35f, 9.35f), "МӘДӘНИЯТ ЙОРТЫ\nДОМ КУЛЬТУРЫ", 54, new Color(.55f, .12f, .1f), plate: false);
        SquarePoster(club, new(-6.2f, 1.6f, 7.03f), "САБАНТУЙ\nиюнь");

        // Former sovkhoz office: two storeys, brick, a faded plaque.
        var office = SquareBuilding(square, "SovkhozOffice", new(-23f, 190f), 14f, 9f, 3.2f, 2, brick, roofPitch: 22f);
        SquareWindows(office, 14f, 9f, 3.2f, 2, 5, trim, lit: index => index == 0);
        SquarePorch(office, 9f, 2.6f, trim);
        SquareSign(office, new(2.9f, 2.2f, 4.58f), "«КАРА УРМАН» СОВХОЗЫ\nИДАРӘСЕ · КОНТОРА", 30, new Color(.15f, .15f, .15f));

        // The closed post office: boarded door and a note about letters.
        var post = SquareBuilding(square, "PostOffice", new(22f, 205f), 9f, 7f, 3.1f, 1, PainterlyMaterialLibrary.ForColor("aebfcf", "wood_painted_blue"), roofPitch: 28f);
        SquareWindows(post, 9f, 7f, 3.1f, 1, 2, trim, lit: _ => false, boarded: (_, _) => true);
        SquarePorch(post, 7f, 2.3f, trim, boardedDoor: true);
        SquareSign(post, new(0, 2.7f, 3.62f), "ПОЧТА", 120, new Color(.12f, .25f, .55f));
        SquareSign(post, new(.95f, 1.2f, 3.64f), "Почта ябык.\nХатлар — кибеттә,\nРазиләдә.\n\nПочта закрыта.\nПисьма — в магазине,\nу Разили.", 18, new Color(.1f, .1f, .12f), paper: true);

        // Where the bus used to turn: a concrete pavilion by the road.
        BuildBusPavilion(square, new(-9f, 176f));
        BuildSabantuyPole(square);
    }

    private Node3D SquareBuilding(Node3D square, string name, Vector2 centre, float width, float depth, float floorHeight,
        int floors, Material walls, float roofPitch, bool hollow = false)
    {
        // Facing the ring: local +Z is the front.
        var toRing = new Vector2(SquareRingCentre.X - centre.X, SquareRingCentre.Z - centre.Y);
        var yaw = Mathf.Atan2(toRing.X, toRing.Y);
        var basis = new Basis(Vector3.Up, yaw);
        // Sit on the real ground (no floating): plinth reaches the lowest corner.
        var low = float.MaxValue; var high = float.MinValue; var sum = 0f; var count = 0;
        for (var ix = -2; ix <= 2; ix++)
        for (var iz = -2; iz <= 2; iz++)
        {
            var local = new Vector3(width * .5f * ix / 2f, 0, depth * .5f * iz / 2f);
            var world = basis * local + new Vector3(centre.X, 0, centre.Y);
            var ground = AgentBAct1HeightField.CollisionGround(world.X, world.Z);
            low = Mathf.Min(low, ground); high = Mathf.Max(high, ground); sum += ground; count++;
        }
        // Floor clears the highest sampled ground (interiors must never be
        // pierced by terrain); the downhill side gets a taller plinth.
        var floorY = high + .25f;
        var building = new Node3D { Name = name, Position = new Vector3(centre.X, floorY, centre.Y), Basis = basis };
        building.SetMeta("plinthDrop", floorY - low);
        square.AddChild(building);
        var plinthHeight = floorY - low + .4f;
        Box(building, "Plinth", new(width + .3f, plinthHeight, depth + .3f), new(0, -plinthHeight * .5f, 0), PainterlyMaterialLibrary.ForColor("7d786f", "stone_foundation"));
        var wallHeight = floorHeight * floors;
        if (!hollow) Box(building, "Walls", new(width, wallHeight, depth), new(0, wallHeight * .5f, 0), walls);
        var trim = PainterlyMaterialLibrary.ForColor("c9bfa9", "plaster");
        // Exterior belt courses and cornice: a solid box for solid shells, a perimeter
        // frame for hollow (enterable) ones so nothing fills the rooms.
        void Course(string name, float y, float height, float over)
        {
            if (!hollow) { Box(building, name, new(width + over, height, depth + over), new(0, y, 0), trim); return; }
            Box(building, name + "F", new(width + over, height, .16f), new(0, y, depth * .5f + over * .5f - .06f), trim);
            Box(building, name + "B", new(width + over, height, .16f), new(0, y, -depth * .5f - over * .5f + .06f), trim);
            Box(building, name + "L", new(.16f, height, depth + over), new(-width * .5f - over * .5f + .06f, y, 0), trim);
            Box(building, name + "R", new(.16f, height, depth + over), new(width * .5f + over * .5f - .06f, y, 0), trim);
        }
        for (var floor = 1; floor < floors; floor++)
            Course($"Belt{floor}", floor * floorHeight, .18f, .12f);
        Course("Cornice", wallHeight + .15f, .3f, .4f);
        // Gable roof along the width, snow on both slopes.
        var rise = Mathf.Tan(Mathf.DegToRad(roofPitch)) * (depth * .5f + .4f);
        var slope = Mathf.Sqrt((depth * .5f + .4f) * (depth * .5f + .4f) + rise * rise);
        foreach (var side in new[] { -1f, 1f })
        {
            var roof = new MeshInstance3D
            {
                Name = side < 0 ? "RoofBack" : "RoofFront",
                Mesh = new BoxMesh { Size = new Vector3(width + .8f, .16f, slope) },
                MaterialOverride = PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof"),
                Position = new Vector3(0, wallHeight + .3f + rise * .5f, side * (depth * .5f + .4f) * .5f),
                RotationDegrees = new Vector3(side * roofPitch, 0, 0)
            };
            building.AddChild(roof);
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var gable = new MeshInstance3D
            {
                Name = side < 0 ? "GableLeft" : "GableRight",
                Mesh = new PrismMesh { Size = new Vector3(depth + .6f, rise, .2f) },
                MaterialOverride = walls,
                Position = new Vector3(side * width * .5f, wallHeight + .3f + rise * .5f, 0),
                RotationDegrees = new Vector3(0, 90, 0)
            };
            building.AddChild(gable);
        }
        var body = new StaticBody3D { Name = "SquareBuildingBody" };
        if (hollow)
        {
            // Enterable: only the plinth is solid (its top is the interior floor);
            // walls, partitions and ceilings come from the interior builder.
            body.AddChild(new CollisionShape3D { Position = new(0, -plinthHeight * .5f, 0), Shape = new BoxShape3D { Size = new Vector3(width + .3f, plinthHeight, depth + .3f) } });
        }
        else
            body.AddChild(new CollisionShape3D { Position = new(0, (wallHeight - plinthHeight) * .5f, 0), Shape = new BoxShape3D { Size = new Vector3(width + .3f, wallHeight + plinthHeight, depth + .3f) } });
        building.AddChild(body);
        return building;
    }

    private static void Box(Node3D parent, string name, Vector3 size, Vector3 position, Material material, Vector3? rotation = null) =>
        parent.AddChild(new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = position, RotationDegrees = rotation ?? Vector3.Zero, MaterialOverride = material });

    private static void SquareWindows(Node3D building, float width, float depth, float floorHeight, int floors, int perRow,
        Material trim, Func<int, bool> lit, Func<int, int, bool>? boarded = null, bool tall = false)
    {
        var glass = PainterlyMaterialLibrary.ForColor("37424c", "frost_window");
        var warm = new StandardMaterial3D { AlbedoColor = new Color(1f, .78f, .45f), EmissionEnabled = true, Emission = new Color(1f, .7f, .4f), EmissionEnergyMultiplier = .8f };
        var planks = PainterlyMaterialLibrary.ForColor("6e5a45", "wood");
        var windowHeight = tall ? floorHeight * .62f : 1.45f;
        var windowWidth = tall ? 1.3f : 1.15f;
        for (var floor = 0; floor < floors; floor++)
        for (var side = 0; side < 2; side++)
        for (var index = 0; index < perRow; index++)
        {
            var x = -width * .5f + width * (index + .5f) / perRow;
            // The front centre keeps room for the door and porch.
            if (side == 0 && floor == 0 && Mathf.Abs(x) < 1.6f) continue;
            var y = floor * floorHeight + floorHeight * (tall ? .52f : .55f);
            var z = (side == 0 ? 1 : -1) * (depth * .5f + .04f);
            var frame = new Node3D { Position = new Vector3(x, y, z), RotationDegrees = new Vector3(0, side == 0 ? 0 : 180, 0) };
            building.AddChild(frame);
            Box(frame, "Frame", new(windowWidth + .16f, windowHeight + .16f, .06f), Vector3.Zero, trim);
            var isBoarded = boarded?.Invoke(floor, index) == true;
            var isLit = side == 0 && floor == 0 && lit(index);
            Box(frame, "Glass", new(windowWidth, windowHeight, .07f), new(0, 0, .01f), isLit ? warm : glass);
            Box(frame, "Mullion", new(.06f, windowHeight, .08f), new(0, 0, .02f), trim);
            Box(frame, "Sill", new(windowWidth + .3f, .07f, .22f), new(0, -windowHeight * .5f - .06f, .08f), PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof"));
            if (isBoarded)
                for (var plank = 0; plank < 3; plank++)
                    Box(frame, $"Board{plank}", new(windowWidth + .25f, .16f, .04f), new(0, (plank - 1) * windowHeight * .32f, .07f), planks, new Vector3(0, 0, plank == 1 ? -9 : 4));
        }
    }

    private static void SquarePorch(Node3D building, float depth, float width, Material trim, bool boardedDoor = false, bool open = false)
    {
        var front = depth * .5f;
        SquareSteps(building, front, width);
        if (!open)
        {
            Box(building, "Door", new(1.2f, 2.2f, .08f), new(0, 1.1f, front + .05f), PainterlyMaterialLibrary.ForColor("5b4636", "wood"));
            Box(building, "DoorFrame", new(1.45f, 2.4f, .05f), new(0, 1.2f, front + .02f), trim);
        }
        Box(building, "Canopy", new(width + .4f, .12f, 1.3f), new(0, 2.65f, front + .65f), PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof"));
        foreach (var side in new[] { -1f, 1f })
            Box(building, $"CanopyPost{side}", new(.1f, 2.65f, .1f), new(side * (width * .5f), 1.33f, front + 1.2f), trim);
        if (boardedDoor)
        {
            var planks = PainterlyMaterialLibrary.ForColor("6e5a45", "wood");
            Box(building, "DoorBoardA", new(1.6f, .18f, .05f), new(0, 1.4f, front + .12f), planks, new Vector3(0, 0, 24));
            Box(building, "DoorBoardB", new(1.6f, .18f, .05f), new(0, .9f, front + .12f), planks, new Vector3(0, 0, -20));
        }
    }

    // Solid steps from the door sill down to the real ground in front of it.
    private static void SquareSteps(Node3D building, float front, float width)
    {
        var outside = building.ToGlobal(new Vector3(0, 0, front + 1.4f));
        var drop = building.GlobalPosition.Y - AgentBAct1HeightField.CollisionGround(outside.X, outside.Z);
        if (drop < .02f) return;
        var count = Mathf.Clamp(Mathf.CeilToInt(drop / .17f), 1, 10);
        var stone = PainterlyMaterialLibrary.ForColor("8c877d", "stone_foundation");
        var body = new StaticBody3D { Name = "SquareStepsBody", CollisionLayer = 2, CollisionMask = 0 };
        building.AddChild(body);
        for (var i = 0; i < count; i++)
        {
            var top = -drop + (i + 1) * drop / count;
            var depth = .34f * (count - i);
            var height = top + drop + .3f;
            var size = new Vector3(width, height, depth);
            var at = new Vector3(0, top - height * .5f, front + depth * .5f);
            Box(building, $"Step{i}", size, at, stone);
            body.AddChild(new CollisionShape3D { Name = $"Step{i}Shape", Position = at, Shape = new BoxShape3D { Size = size } });
        }
    }

    private static void SquarePortico(Node3D building, float width, float depth, float height, Material trim, bool open = false)
    {
        var front = depth * .5f;
        var column = PainterlyMaterialLibrary.ForColor("efe9dc", "plaster");
        var porticoBody = new StaticBody3D { Name = "PorticoFloorBody", CollisionLayer = 2, CollisionMask = 0 };
        porticoBody.AddChild(new CollisionShape3D { Position = new(0, -.175f, front + 1.5f), Shape = new BoxShape3D { Size = new(10f, .35f, 3f) } });
        building.AddChild(porticoBody);
        Box(building, "PorticoFloor", new(10f, .35f, 3f), new(0, -.175f, front + 1.5f), PainterlyMaterialLibrary.ForColor("8c877d", "stone_foundation"));
        SquareSteps(building, front + 3f, 6f);
        foreach (var x in new[] { -3.9f, -1.3f, 1.3f, 3.9f })
            building.AddChild(new MeshInstance3D { Name = $"Column{x:0.0}", Mesh = new CylinderMesh { TopRadius = .24f, BottomRadius = .28f, Height = height - .3f, RadialSegments = 12 }, Position = new Vector3(x, (height - .3f) * .5f + .1f, front + 2.6f), MaterialOverride = column });
        Box(building, "Entablature", new(10.2f, .6f, 3.2f), new(0, height + .05f, front + 1.5f), trim);
        building.AddChild(new MeshInstance3D { Name = "Pediment", Mesh = new PrismMesh { Size = new Vector3(10.2f, 1.9f, .5f) }, Position = new Vector3(0, height + 1.3f, front + 2.85f), MaterialOverride = trim });
        if (!open)
            foreach (var x in new[] { -.9f, .9f })
                Box(building, $"DoubleDoor{x}", new(.85f, 2.8f, .08f), new(x * .5f, 1.4f, front + .05f), PainterlyMaterialLibrary.ForColor("6a4a33", "wood"));
        var body = new StaticBody3D { Name = "PorticoBody" };
        foreach (var x in new[] { -3.9f, -1.3f, 1.3f, 3.9f })
            body.AddChild(new CollisionShape3D { Position = new Vector3(x, height * .5f, front + 2.6f), Shape = new CylinderShape3D { Radius = .3f, Height = height } });
        building.AddChild(body);
    }

    private static void SquareSign(Node3D building, Vector3 at, string text, int fontSize, Color color, bool plate = true, bool paper = false)
    {
        var lines = text.Split('\n');
        var longest = lines.Max(line => line.Length);
        if (plate || paper)
        {
            var size = new Vector3(Mathf.Max(.5f, longest * fontSize * .0028f), Mathf.Max(.3f, lines.Length * fontSize * .0062f), .04f);
            Box(building, "Plate", size, at + new Vector3(0, 0, -.025f), paper ? PainterlyMaterialLibrary.ForColor("efece2", "cloth") : PainterlyMaterialLibrary.ForColor("f1efe8", "plastic_abs"));
        }
        building.AddChild(new Label3D { Text = text, Position = at + new Vector3(0, 0, .005f), FontSize = fontSize, PixelSize = .005f, Modulate = color, OutlineSize = 0, HorizontalAlignment = HorizontalAlignment.Center });
    }

    private static void SquarePoster(Node3D building, Vector3 at, string text)
    {
        Box(building, "PosterBoard", new(1.3f, 1.7f, .06f), at + new Vector3(0, 0, -.03f), PainterlyMaterialLibrary.ForColor("6b5846", "wood"));
        Box(building, "Poster", new(1.1f, 1.45f, .02f), at + new Vector3(0, 0, .01f), PainterlyMaterialLibrary.ForColor("d9b44a", "cloth"));
        building.AddChild(new Label3D { Text = text, Position = at + new Vector3(0, .1f, .025f), FontSize = 40, PixelSize = .005f, Modulate = new Color(.55f, .1f, .08f), OutlineSize = 0 });
    }

    private void BuildBusPavilion(Node3D square, Vector2 at)
    {
        var ground = AgentBAct1HeightField.CollisionGround(at.X, at.Y);
        var pavilion = new Node3D { Name = "OldBusPavilion", Position = new Vector3(at.X, ground, at.Y), RotationDegrees = new Vector3(0, 90, 0) };
        square.AddChild(pavilion);
        var concrete = PainterlyMaterialLibrary.ForColor("a7a39a", "stone");
        Box(pavilion, "Back", new(4.4f, 2.5f, .2f), new(0, 1.25f, -.9f), concrete);
        Box(pavilion, "Roof", new(4.8f, .2f, 2.1f), new(0, 2.6f, -.2f), concrete);
        Box(pavilion, "RoofSnow", new(4.8f, .14f, 2.1f), new(0, 2.77f, -.2f), PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof"));
        foreach (var side in new[] { -2.1f, 2.1f })
            Box(pavilion, $"Side{side}", new(.2f, 2.5f, 1.4f), new(side, 1.25f, -.35f), concrete);
        Box(pavilion, "Bench", new(3.4f, .08f, .4f), new(0, .45f, -.6f), PainterlyMaterialLibrary.ForColor("6b5846", "wood"));
        // A faded mosaic of a wheat sheaf and a sun on the back wall.
        Box(pavilion, "MosaicSun", new(.7f, .7f, .03f), new(1.1f, 1.8f, -.79f), PainterlyMaterialLibrary.ForColor("c9953f", "stone"), new Vector3(0, 0, 45));
        Box(pavilion, "MosaicSheaf", new(.35f, 1.1f, .03f), new(-.8f, 1.5f, -.79f), PainterlyMaterialLibrary.ForColor("b58b3a", "stone"));
        pavilion.AddChild(new Label3D { Text = "Автобус соңгы тапкыр: 2014\nПоследний рейс: 2014", Position = new Vector3(0, 2.2f, -.78f), FontSize = 22, PixelSize = .005f, Modulate = new Color(.2f, .2f, .22f), OutlineSize = 0 });
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Position = new(0, 1.25f, -.9f), Shape = new BoxShape3D { Size = new Vector3(4.4f, 2.5f, .2f) } });
        foreach (var side in new[] { -2.1f, 2.1f })
            body.AddChild(new CollisionShape3D { Position = new(side, 1.25f, -.35f), Shape = new BoxShape3D { Size = new Vector3(.2f, 2.5f, 1.4f) } });
        pavilion.AddChild(body);
    }

    // The Sabantuy climbing pole stands in the ring all year, snow on its cap.
    private void BuildSabantuyPole(Node3D square)
    {
        var ground = AgentBAct1HeightField.CollisionGround(SquareRingCentre.X, SquareRingCentre.Z);
        var pole = new Node3D { Name = "SabantuyPole", Position = new Vector3(SquareRingCentre.X, ground, SquareRingCentre.Z) };
        square.AddChild(pole);
        pole.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .07f, BottomRadius = .13f, Height = 9f, RadialSegments = 10 }, Position = new Vector3(0, 4.5f, 0), MaterialOverride = PainterlyMaterialLibrary.ForColor("8a7155", "wood") });
        pole.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .28f, OuterRadius = .34f }, Position = new Vector3(0, 8.7f, 0), MaterialOverride = PainterlyMaterialLibrary.ForColor("b8412f", "wood_painted_trim") });
        pole.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = .16f, Height = .2f }, Position = new Vector3(0, 9.05f, 0), MaterialOverride = PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof") });
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Position = new(0, 1.5f, 0), Shape = new CylinderShape3D { Radius = .18f, Height = 3f } });
        pole.AddChild(body);
    }
}
