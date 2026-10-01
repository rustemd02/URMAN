using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Relayout v3 stage 3: the civic square Мәйдан lies west of the main street in the middle of
    // the open part (plan.json); DK, school and post face it, the office faces it across the street.
    // Civic square is surrounded by old and new inhabited quarters. School and
    // DK have real interiors; office/post are closed. All transforms and terrain
    // clearances move together with the connected road and prologue route.
    private static readonly Vector3 SquareRingCentre = new(AgentBAct1Layout.CivicCentre.X, 0, AgentBAct1Layout.CivicCentre.Y);
    // The former central school owns the village's school archive and its
    // existing document IDs. The small school building remains a later annex.
    private Node3D? _squareSchool;

    private void BuildSovkhozSquare(Node3D core)
    {
        var square = new Node3D { Name = "SovkhozSquare" };
        square.SetMeta("presentationOwner", nameof(Act1ConnectedWorld));
        square.SetMeta("scope", "central civic square; school/archive and DK enterable; housing continues north and south");
        core.AddChild(square);

        var plaster = PainterlyMaterialLibrary.ForColor("d9cdb4", "plaster");
        var brick = PainterlyMaterialLibrary.ForColor("9a5a45", "stone");
        var trim = PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim");

        // Old two-storey school: long, many windows, upper floor partly boarded.
        var school = SquareBuilding(square, "OldSchool", new(-17.5f, 71.5f), 28f, 11f, 3.4f, 2, PainterlyMaterialLibrary.ForColor("e2d6b8", "plaster"), roofPitch: 16f, hollow: true);
        _squareSchool = school;
        SquareWeatherShelter(school, new(0, 3.4f, 0), new(13.65f, 3.4f, 5.15f));
        SquarePorch(school, 11f, 3.2f, trim, open: true);
        BuildSchoolInterior(school, PainterlyMaterialLibrary.ForColor("e2d6b8", "plaster"), PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim"));
        SquareSign(school, new(0, 3.6f, 5.7f), "КАРА-УРМАН УРТА МӘКТӘБЕ\nКАРА-УРМАНСКАЯ СРЕДНЯЯ ШКОЛА", 62, new Color(.15f, .18f, .28f));

        // House of culture: a tall hall behind a four-column portico and pediment.
        var club = SquareBuilding(square, "HouseOfCulture", new(-37.5f, 48f), 18f, 14f, 6.8f, 1, PainterlyMaterialLibrary.ForColor("e8dcc4", "plaster"), roofPitch: 12f, hollow: true);
        SquareWeatherShelter(club, new(0, 3.4f, 0), new(8.65f, 3.4f, 6.65f));
        SquarePortico(club, 18f, 14f, 6.8f, trim, open: true);
        BuildClubInterior(club, PainterlyMaterialLibrary.ForColor("e8dcc4", "plaster"), PainterlyMaterialLibrary.ForColor("eeeae0", "wood_painted_trim"));
        SquareSign(club, new(0, 7.6f, 10.12f), "МӘДӘНИЯТ ЙОРТЫ\nДОМ КУЛЬТУРЫ", 54, new Color(.55f, .12f, .1f), plate: false);
        SquarePoster(club, new(-6.2f, 1.6f, 7.2f), "САБАНТУЙ\nиюнь");

        // Former sovkhoz office: two storeys, brick, a faded plaque.
        var office = SquareBuilding(square, "SovkhozOffice", new(11f, 52f), 14f, 9f, 3.2f, 2, brick, roofPitch: 22f);
        SquareWindows(office, 14f, 9f, 3.2f, 2, 5, trim, lit: index => index == 0);
        SquarePorch(office, 9f, 2.6f, trim);
        SquareSign(office, new(2.9f, 2.2f, 4.58f), "«КАРА УРМАН» СОВХОЗЫ\nИДАРӘСЕ · КОНТОРА", 30, new Color(.15f, .15f, .15f));

        // The closed post office: boarded door and a note about letters.
        var post = SquareBuilding(square, "PostOffice", new(-15.5f, 27f), 9f, 7f, 3.1f, 1, PainterlyMaterialLibrary.ForColor("aebfcf", "wood_painted_blue"), roofPitch: 28f);
        SquareWindows(post, 9f, 7f, 3.1f, 1, 2, trim, lit: _ => false, boarded: (_, _) => true);
        SquarePorch(post, 7f, 2.3f, trim, boardedDoor: true);
        SquareSign(post, new(0, 2.7f, 3.62f), "ПОЧТА", 120, new Color(.12f, .25f, .55f));
        SquareSign(post, new(.95f, 1.2f, 3.64f), "Почта ябык.\nХатлар — кибеттә,\nРазиләдә.\n\nПочта закрыта.\nПисьма — в магазине,\nу Разили.", 18, new Color(.1f, .1f, .12f), paper: true);

        // The plate hangs on a real wall piece between openings (never over a
        // window); `surface` names that piece so the mount check reads its triangles.
        void Address(Node3D owner, string suffix, string number, string cadastral, float signX, string surface, float depth, float apron, string role,
            IReadOnlyList<Vector3>? approach = null)
        {
            var access = approach is { Count: > 0 } ? approach[^1] : AddressGround(owner.ToGlobal(new Vector3(0, 0, depth * .5f + apron + 1.05f)));
            var sign = owner.ToGlobal(new Vector3(signX, 1.65f, depth * .5f + .035f));
            var outward = owner.GlobalBasis.Z.Normalized();
            RegisterAddressedBuilding(new(owner, "act1/square/" + suffix, "BLD-SQUARE-" + suffix,
                "PAR-SQUARE-" + suffix, "ADR-SQUARE-" + suffix, "urman", number,
                cadastral, access, sign, outward, role, ApproachPath: approach, SignSurfaceName: surface));
        }
        Address(school, "SCHOOL", "12", "URM-Q03-P0001", -10.885f, "Pier1Skin", 11f, 1.3f, "school");
        // Foot of the central steps, then the portico landing before the door. Both
        // are walked by the physical verifier; the plate is read on the portico floor.
        var clubFoot = AddressGround(club.ToGlobal(new Vector3(0, 0, 7f + 3f + SquareStepsRun(club, 10f) + .5f)));
        var clubLanding = club.ToGlobal(new Vector3(0, .01f, 7f + 1.5f));
        Address(club, "DK", "14", "URM-Q03-P0002", -4.4f, "Pier1Skin", 14f, 0f, "culture", new[] { clubFoot, clubLanding });
        Address(office, "OFFICE", "16", "URM-Q03-P0003", -4.2f, "Walls", 9f, 1.3f, "office");
        Address(post, "POST", "18", "URM-Q03-P0004", -3.7f, "Walls", 7f, 1.3f, "post");

        // Where the bus used to turn: a concrete pavilion by the road.
        BuildBusPavilion(square, new(-4.4f, 20f));
        BuildPlazaPaving(square);
        BuildWinterCivicGarden(square, new[] { school, club, office, post });
        BuildNorthWinterRoads(core);
        AddOpenPartStreets(core);
    }

    private void SquareWeatherShelter(Node3D owner, Vector3 centre, Vector3 half)
        => GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld")
            .RegisterWeatherShelter(owner, centre, half);

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
            body.SetMeta("footstepSurface","herringbone_parquet");
            // Enterable: only the plinth is solid (its top is the interior floor);
            // walls, partitions and ceilings come from the interior builder.
            body.AddChild(new CollisionShape3D { Position = new(0, -plinthHeight * .5f, 0), Shape = new BoxShape3D { Size = new Vector3(width + .3f, plinthHeight, depth + .3f) } });
        }
        else
        {
            // The plinth overhangs the walls by 15 cm; the walls themselves stand on it,
            // so a plate on the wall face is in open air rather than inside the collider.
            body.AddChild(new CollisionShape3D { Name = "PlinthShape", Position = new(0, -plinthHeight * .5f, 0), Shape = new BoxShape3D { Size = new Vector3(width + .3f, plinthHeight, depth + .3f) } });
            body.AddChild(new CollisionShape3D { Name = "WallsShape", Position = new(0, wallHeight * .5f, 0), Shape = new BoxShape3D { Size = new Vector3(width, wallHeight, depth) } });
        }
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

    // A tread must be deeper than the capsule's rounded toe so a standing body settles on it.
    private const float SquareTreadDepth = .45f;

    private static int SquareStepCount(Node3D building, float front, out float drop)
    {
        float Ground(float z)
        {
            var at = building.ToGlobal(new Vector3(0, 0, z));
            return AgentBAct1HeightField.CollisionGround(at.X, at.Z);
        }
        drop = building.GlobalPosition.Y - Ground(front + 1.4f);
        // Where the ground falls away the lowest tread must still start one ordinary
        // riser above the ground at the foot of the flight, not above the ground at its head.
        for (var pass = 0; pass < 3 && drop >= .02f; pass++)
        {
            var count = Mathf.Clamp(Mathf.CeilToInt(drop / .17f), 1, 10);
            drop = Mathf.Max(drop, building.GlobalPosition.Y - Ground(front + count * SquareTreadDepth + .2f));
        }
        return drop < .02f ? 0 : Mathf.Clamp(Mathf.CeilToInt(drop / .17f), 1, 10);
    }

    /// <summary>Horizontal run of the steps that <see cref="SquareSteps"/> builds from `front`.</summary>
    private static float SquareStepsRun(Node3D building, float front) => SquareStepCount(building, front, out _) * SquareTreadDepth;

    // Solid steps from the door sill down to the real ground in front of it.
    private static void SquareSteps(Node3D building, float front, float width)
    {
        var count = SquareStepCount(building, front, out var drop);
        if (count == 0) return;
        var stone = PainterlyMaterialLibrary.ForColor("8c877d", "stone_foundation");
        var body = new StaticBody3D { Name = "SquareStepsBody", CollisionLayer = 2, CollisionMask = 0 };
        building.AddChild(body);
        for (var i = 0; i < count; i++)
        {
            var top = -drop + (i + 1) * drop / count;
            var depth = SquareTreadDepth * (count - i);
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
        if (paper)
        {
            var note = CivicSurfaceLibrary.FramedFace(building,"PostClosureNotice",at,0,new(.72f,.54f),CivicSurfaceLibrary.Face("postal_closure_v1_basecolor.png"));
            note.SetMeta("readableText",text); return;
        }
        var size = building.Name.ToString() switch {
            "OldSchool" => new Vector2(4.2f,1.4f), "HouseOfCulture" => new Vector2(3.3f,1.1f),
            "SovkhozOffice" => new Vector2(2.4f,.8f), _ => new Vector2(1.8f,.6f) };
        CivicSurfaceLibrary.Sign(building,text,at,0,size);
    }

    private static void SquarePoster(Node3D building, Vector3 at, string text)
    {
        var poster = CivicSurfaceLibrary.FramedFace(building,"SabantuyPoster",at,0,new(1.1f,1.45f),CivicSurfaceLibrary.Face("notices_v2_atlas.png",2,4,4));
        poster.SetMeta("readableText","САБАНТУЙ"); // No date is printed on this village notice.
    }

    private void BuildBusPavilion(Node3D square, Vector2 at)
    {
        var ground = AgentBAct1HeightField.CollisionGround(at.X, at.Y);
        var pavilion = new Node3D { Name = "OldBusPavilion", Position = new Vector3(at.X, ground, at.Y), RotationDegrees = new Vector3(0, 90, 0) };
        square.AddChild(pavilion);
        var concrete = RuralPropMaterials.Surface("concrete");
        RuralPropGeometry.Block(pavilion, "Back", new(4.4f, 2.5f, .2f), new(0, 1.25f, -.9f), concrete, .025f);
        RuralPropGeometry.Block(pavilion, "Roof", new(4.8f, .2f, 2.1f), new(0, 2.6f, -.2f), concrete, .025f);
        Box(pavilion, "RoofSnow", new(4.8f, .14f, 2.1f), new(0, 2.77f, -.2f), PainterlyMaterialLibrary.ForColor("e6ebef", "snow_roof"));
        foreach (var side in new[] { -2.1f, 2.1f })
            RuralPropGeometry.Block(pavilion, $"Side{side}", new(.2f, 2.5f, 1.4f), new(side, 1.25f, -.35f), concrete, .025f);
        RuralPropModels.Bench(pavilion, "Bench", new(0, 0, -.53f), 0, 3.4f, false);
        // One real mural face with a measured UV island, slab thickness and
        // perimeter mortar. The image supplies pigment; no guessed normal map
        // is derived from the colours of the ceramic pieces.
        RuralPropGeometry.Block(pavilion, "MosaicBacking", new(2.05f, 1.05f, .026f), new(0, 1.45f, -.791f), concrete, .007f);
        var mosaicMaterial = new StandardMaterial3D { ResourceName = "BusCeramicMosaic",
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://assets/textures/realism_20260929/bus_wheat_sun_mosaic_v1_basecolor.png"),
            Roughness = .85f, Metallic = 0 };
        mosaicMaterial.SetMeta("surfaceUVContract", "TX29-22 single complete 2x1 m wall panel; full unique UV; no repeat");
        RuralPropGeometry.Part(pavilion, "MosaicPanel", new QuadMesh { Size = new(2, 1) }, new(0, 1.45f, -.776f), mosaicMaterial);
        CivicSurfaceLibrary.Sign(pavilion,"Автобус — борылышта\nАвтобус — у поворота",new(0,2.2f,-.77f),0,new(1.5f,.24f));
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Name = "BenchSeatContact", Position = new(0, .46f, -.53f), Shape = new BoxShape3D { Size = new(3.4f, .04f, .405f) } });
        body.AddChild(new CollisionShape3D { Position = new(0, 1.25f, -.9f), Shape = new BoxShape3D { Size = new Vector3(4.4f, 2.5f, .2f) } });
        foreach (var side in new[] { -2.1f, 2.1f })
            body.AddChild(new CollisionShape3D { Position = new(side, 1.25f, -.35f), Shape = new BoxShape3D { Size = new Vector3(.2f, 2.5f, 1.4f) } });
        pavilion.AddChild(body);
    }

    // The square's surface: packed, swept winter snow over the paving, laid on the levelled
    // terrain (AgentBAct1HeightField.LevelPlaza) as a top surface with no collider of its own.
    private static void BuildPlazaPaving(Node3D square)
    {
        var r = AgentBAct1Layout.PlazaRect;
        const float cell = 1f;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 P(float x, float z) => new(x, AgentBAct1HeightField.CollisionGround(x, z) + .02f, z);
        for (var z = r.Z0; z < r.Z1 - .001f; z += cell)
        for (var x = r.X0; x < r.X1 - .001f; x += cell)
        {
            var x1 = Mathf.Min(x + cell, r.X1); var z1 = Mathf.Min(z + cell, r.Z1);
            foreach (var v in new[] { P(x, z), P(x1, z1), P(x, z1), P(x, z), P(x1, z), P(x1, z1) })
            {
                surface.SetUV(new Vector2(v.X, v.Z) * .25f);
                surface.AddVertex(v);
            }
        }
        surface.Index();
        surface.GenerateNormals();
        var paving = new MeshInstance3D
        {
            Name = "MaidanPaving", Mesh = surface.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("d5d8d8", "snow_trampled"),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        paving.SetMeta("presentationOnly", true);
        paving.SetMeta("collisionOwner", "AgentB_TerrainCollision");
        paving.SetMeta("mapLabel", "Мәйдан");
        square.AddChild(paving);
        // A low stone kerb marks the edge of the square where it meets the yards.
        var kerb = PainterlyMaterialLibrary.ForColor("8f8a82", "stone");
        foreach (var (a, b) in new[] { (new Vector2(r.X0, r.Z0), new Vector2(r.X1, r.Z0)), (new Vector2(r.X0, r.Z1), new Vector2(r.X1, r.Z1)),
                     (new Vector2(r.X0, r.Z0), new Vector2(r.X0, r.Z1)) })
        {
            var mid = (a + b) * .5f; var length = a.DistanceTo(b);
            var along = b - a; var yaw = Mathf.Atan2(along.X, along.Y);
            var y = AgentBAct1HeightField.CollisionGround(mid.X, mid.Y);
            var piece = new MeshInstance3D
            {
                Name = "MaidanKerb" + square.GetChildCount(), Mesh = new BoxMesh { Size = new Vector3(.25f, .12f, length) },
                MaterialOverride = kerb, Position = new Vector3(mid.X, y + .04f, mid.Y), Rotation = new Vector3(0, yaw, 0)
            };
            piece.SetMeta("presentationOnly", true);
            square.AddChild(piece);
        }
    }

    // A winter village square: a modest permanent gathering platform and a
    // planted perimeter, not a working fountain. Everything is within the ring's
    // car-free inner island (the vehicle lane starts outside radius 6.1 m).
    private void BuildWinterCivicGarden(Node3D square, IReadOnlyList<Node3D> buildings)
    {
        var centre = new Vector2(SquareRingCentre.X,SquareRingCentre.Z);
        var garden = new Node3D { Name = "WinterCivicGarden", Position = new(centre.X,0,centre.Y) };
        garden.SetMeta("scope","low winter festival platform; radius 4.75 m maximum furniture envelope; paths to actual entrances");
        square.AddChild(garden);
        var high = AgentBAct1HeightField.CollisionGround(centre.X,centre.Y); var low = high;
        for(var i=0;i<48;i++)
        {
            var angle = i/48f*Mathf.Tau;
            var height = AgentBAct1HeightField.CollisionGround(centre.X+Mathf.Cos(angle)*3.5f,centre.Y+Mathf.Sin(angle)*3.5f);
            high = Mathf.Max(high,height); low = Mathf.Min(low,height);
        }
        var top = high+.12f;
        var body = new StaticBody3D { Name = "FestivalPlatformBody", CollisionLayer = 2, CollisionMask = 0 };
        garden.AddChild(body);
        var stone = RuralPropMaterials.Surface("concrete");
        // Broad 6 cm risers are walkable in winter and retain actual support down to the ground.
        foreach(var (radius,rise) in new[] { (3.5f,0f),(3.2f,.06f) })
        {
            var height=top+rise-low+.2f; var y=top+rise-height*.5f;
            RuralPropGeometry.Part(garden,"PlatformStep"+radius,
                RuralPropGeometry.Lathe("FestivalStep"+radius,new[] { new Vector2(0,-height*.5f),new Vector2(radius-.018f,-height*.5f),new Vector2(radius,-height*.5f+.02f),new Vector2(radius,height*.5f-.008f),new Vector2(radius-.012f,height*.5f),new Vector2(0,height*.5f) },96),
                new(0,y,0),stone);
            body.AddChild(new CollisionShape3D { Position = new(0,y,0), Shape = new CylinderShape3D { Radius=radius,Height=height } });
        }
        // A restrained wooden apron gives the centre a tangible human scale.
        var deck = new Node3D { Name = "FestivalDeck", Position = new(0,top+.064f,0) }; garden.AddChild(deck);
        for(var i=-8;i<=8;i++)
        {
            var x=i*.34f; var length=2*Mathf.Sqrt(Mathf.Max(0,2.95f*2.95f-x*x));
            if(length<.15f)continue;
            RuralPropGeometry.Block(deck,"OakDeckBoard"+i,new(.332f,.035f,length),new(x,-.0175f,0),RuralPropMaterials.Surface("wood"),.004f);
        }
        foreach(var angle in new[] { .80f,2.08f,2.88f,4.73f })
        {
            var x=Mathf.Cos(angle)*4.3f; var z=Mathf.Sin(angle)*4.3f;
            var ground=AgentBAct1HeightField.CollisionGround(centre.X+x,centre.Y+z);
            var bench=RuralPropModels.Bench(garden,"SquareBench"+angle,new(x,ground,z),Mathf.RadToDeg(-angle)-90,1.3f);
            RuralPropGeometry.AttachMemberContacts(bench,body);
        }
        // Bare winter shrubs retain branching silhouettes and leave every approach clear.
        foreach(var angle in new[] { .38f,1.25f,3.66f,5.35f })
        {
            var p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*4.1f;
            var y=AgentBAct1HeightField.CollisionGround(centre.X+p.X,centre.Y+p.Y);
            var shrub=new Node3D { Name="WinterSpirea"+angle,Position=new(p.X,y,p.Y) };garden.AddChild(shrub);
            for(var i=0;i<9;i++)
            {
                var a=i/9f*Mathf.Tau;var tip=new Vector3(Mathf.Cos(a)*.20f,.25f+(i%3)*.045f,Mathf.Sin(a)*.20f);
                var joint=new Vector3(tip.X*.42f,.13f,tip.Z*.42f);
                RuralPropGeometry.Tube(shrub,"Stem"+i,new(0,0,0),joint,.006f,RuralPropMaterials.Surface("wood"),8);
                RuralPropGeometry.Tube(shrub,"Twig"+i,joint,tip,.003f,RuralPropMaterials.Surface("wood"),8);
            }
        }
        // Paths are top surfaces conforming to the same collision terrain, not
        // raised roadside plates. Their actual endpoints are the entrance aprons.
        foreach(var building in buildings)
        {
            var depth=building.Name.ToString() switch { "OldSchool"=>11f,"HouseOfCulture"=>14f,"SovkhozOffice"=>9f,_=>7f };
            var apron=building.Name.ToString()=="HouseOfCulture" ? 3f+SquareStepsRun(building,10f)+.5f : 1.7f;
            var target=building.ToGlobal(new Vector3(0,0,depth*.5f+apron));
            var direction=(new Vector2(target.X,target.Z)-centre).Normalized();
            // A short broad ramp bridges the level platform to its downhill
            // approach instead of leaving a tall concrete lip on uneven ground.
            using(var ramp=new SurfaceTool())
            {
                ramp.Begin(Mesh.PrimitiveType.Triangles);
                var rampTangent=new Vector2(-direction.Y,direction.X)*.78f;
                Vector3 P(float distance,float side,bool bottom=false)
                {
                    var p=centre+direction*distance+rampTangent*side;
                    var y=distance<3.3f ? top+.06f : AgentBAct1HeightField.CollisionGround(p.X,p.Y)+.016f;
                    return new(p.X-centre.X,bottom ? low-.1f : y,p.Y-centre.Y);
                }
                void RampVertex(Vector3 p){ramp.SetUV(new(p.X,p.Z));ramp.AddVertex(p);}
                var a=P(3.15f,-1);var b=P(3.15f,1);var c=P(4.35f,1);var d=P(4.35f,-1);
                RampVertex(a);RampVertex(b);RampVertex(c);RampVertex(a);RampVertex(c);RampVertex(d);
                foreach(var (e,f) in new[]{(a,d),(c,b),(d,c)})
                {var eb=new Vector3(e.X,low-.1f,e.Z);var fb=new Vector3(f.X,low-.1f,f.Z);RampVertex(e);RampVertex(f);RampVertex(fb);RampVertex(e);RampVertex(fb);RampVertex(eb);}
                ramp.Index();ramp.GenerateNormals();var mesh=ramp.Commit();
                garden.AddChild(new MeshInstance3D { Name=building.Name+"PlatformRamp",Mesh=mesh,MaterialOverride=stone });
                body.AddChild(new CollisionShape3D { Name=building.Name+"RampContact",Shape=mesh.CreateTrimeshShape() });
            }
            var from=centre+direction*4.35f; var to=new Vector2(target.X,target.Z);
            using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
            var length=from.DistanceTo(to);var count=Mathf.Max(1,Mathf.CeilToInt(length/.6f));var tangent=new Vector2(-direction.Y,direction.X)*.78f;
            void V(int i,float side)
            {
                var p=from.Lerp(to,i/(float)count)+tangent*side;
                s.SetUV(new(side*.78f,i/(float)count*length));
                s.AddVertex(new(p.X-centre.X,AgentBAct1HeightField.CollisionGround(p.X,p.Y)+.012f,p.Y-centre.Y));
            }
            for(var i=0;i<count;i++){V(i,-1);V(i,1);V(i+1,1);V(i,-1);V(i+1,1);V(i+1,-1);}
            s.Index();s.GenerateNormals();garden.AddChild(new MeshInstance3D { Name=building.Name+"ApproachPath",Mesh=s.Commit(),MaterialOverride=stone,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }
}
