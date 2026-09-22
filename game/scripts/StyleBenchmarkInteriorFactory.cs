using Godot;

namespace Urman.Godot;

/// <summary>
/// Room-local contract paired with HERO_HOUSE_CONTRACT in the exterior source.
/// Only architectural members are cut to fit. Furniture is moved rigidly from
/// its published module; people and the separately authored CRT keep their size.
/// </summary>
public static class StyleBenchmarkInteriorFactory
{
    public const string ContractVersion = "hero-house-eight-by-seven-v1";
    public const string ExteriorComponent = "HeroHouse_TimberPlaster";
    public const float ClearWidth = 8f;
    public const float ClearDepth = 7f;
    public const float CeilingHeight = 2.60f;
    public const float WallThickness = .20f;
    public const float InteriorFinishThickness = .002f;
    public const float DoorX = -2.8224f;
    public const float DoorWidth = 1.30f;
    public const float DoorHeight = 2.25f;
    public const float WindowWidth = 1.06f;
    public const float WindowSill = .74f;
    public const float WindowHead = 2.14f;
    public static readonly Vector3 RoomOffset = new(1.658f, .246f, -2.634f);
    public static readonly Vector3 Entry = new(DoorX, .05f, 2.30f);
    public static readonly Vector3 ExitTarget = new(DoorX, 1.05f, 3.40f);
    public static readonly Vector3 PcAnchor = new(0, 1.15f, -2.68f);
    public static readonly Vector3 MansurAnchor = new(1.20f, 0, .85f);
    public static readonly Vector3 GulsinaAnchor = new(-1.75f, 0, -.50f);
    public static readonly Vector3 RinatAnchor = new(2.60f, 0, -1.75f);
    public static readonly Vector3 PhotoAnchor = new(-3.60f, 1.47f, -2.50f);
    public const float PhotoYawDegrees = 90f;
    public static readonly Vector3 TinAnchor = new(-1.25f, .85f, 2.78f);
    public static readonly Vector3 StoveAnchor = new(-3.15f, 0, -.45f);
    public static readonly Vector3 ChestAnchor = new(-.75f, -.02f, 2.85f);
    public static readonly Vector3 WarmWaterAnchor = new(-2.90f, 1.254f, -.40f);

    public readonly record struct Window(string Name, Vector3 Center, float YawDegrees);
    public static readonly IReadOnlyList<Window> Windows = new Window[]
    {
        new("Front0", new(-.85f, 1.44f, 3.60f), 180),
        new("Front1", new(1f, 1.44f, 3.60f), 180),
        new("Front2", new(2.85f, 1.44f, 3.60f), 180),
        new("Rear0", new(-2.55f, 1.44f, -3.60f), 0),
        new("Rear1", new(2.55f, 1.44f, -3.60f), 0),
        new("Left0", new(-4.10f, 1.44f, .60f), 90),
        new("Right0", new(4.10f, 1.44f, .90f), -90)
    };

    public static Transform3D TransformFromFacade(Node3D facade) =>
        facade.GlobalTransform * new Transform3D(Basis.Identity, RoomOffset);

    internal static void Build(Node3D room)
    {
        room.SetMeta("heroHouseContract", ContractVersion);
        room.SetMeta("heroHouseClearDimensions", new Vector3(ClearWidth, CeilingHeight, ClearDepth));
        Block(room, "Floor", new(8.4f, .18f, 7.4f), new(0, -.09f, 0), "777068", "wood_floor_painted");
        Block(room, "Ceiling", new(8.4f, .16f, 7.4f), new(0, 2.68f, 0), "695746", "wood");

        Wall(room, "FrontWall", 4.2f, 3.6f, false,
            [new(DoorX, DoorWidth, 0, DoorHeight),
             new(-.85f, WindowWidth, WindowSill, WindowHead),
             new(1f, WindowWidth, WindowSill, WindowHead),
             new(2.85f, WindowWidth, WindowSill, WindowHead)]);
        Wall(room, "BackWall", 4.2f, -3.6f, false,
            [new(-2.55f, WindowWidth, WindowSill, WindowHead), new(2.55f, WindowWidth, WindowSill, WindowHead)]);
        Wall(room, "LeftWall", 3.5f, -4.1f, true, [new(.60f, WindowWidth, WindowSill, WindowHead)]);
        Wall(room, "RightWall", 3.5f, 4.1f, true, [new(.90f, WindowWidth, WindowSill, WindowHead)]);

        // Exposed beams sit above the clear ceiling datum, never through heads.
        foreach (var x in new[] { -2.25f, 2.25f })
            Block(room, "CeilingBeam" + (x < 0 ? "Left" : "Right"), new(.20f, .18f, 7f),
                new(x, 2.69f, 0), "493629", "wood", collision: false);
        foreach (var window in Windows) BuildWindow(room, window);

        // A closed interior leaf is physical even while the route target is
        // unavailable. The thin ray target sits on its room-facing side.
        Block(room, "HouseExitDoorPanel", new(DoorWidth, DoorHeight, .08f),
            new(DoorX, DoorHeight * .5f, 3.58f), "4b382c", "wood");
        foreach (var side in new[] { -1f, 1f })
            Block(room, side < 0 ? "HouseExitDoorFrameLeft" : "HouseExitDoorFrameRight",
                new(.10f, 2.35f, .15f), new(DoorX + side * .70f, 1.175f, 3.43f), "5d4a38", "wood", false);
        Block(room, "HouseExitDoorFrameTop", new(1.50f, .10f, .15f),
            new(DoorX, 2.30f, 3.43f), "5d4a38", "wood", false);
        Block(room, "HouseExitDoorThreshold", new(DoorWidth, .025f, .34f),
            new(DoorX, .0125f, 3.49f), "574636", "wood", false);

        AttachFurniture(room);
        // The photograph can turn over on this shallow shelf without rotating
        // through a wall. Its frame bottom rests on the shelf at 1.11m.
        Block(room, "FamilyPhotoShelf", new(.72f, .08f, 1.10f), new(-3.63f, 1.07f, -2.50f), "765842", "wood");
        foreach (var z in new[] { -2.90f, -2.10f })
            Block(room, "FamilyPhotoShelfBracket" + (z < -2.5f ? "Rear" : "Front"),
                new(.44f, .14f, .065f), new(-3.77f, .96f, z), "493629", "wood", false);
    }

    private readonly record struct Opening(float Center, float Width, float Bottom, float Top);

    private static void Wall(Node3D room, string name, float halfLength, float at, bool sideWall, Opening[] openings)
    {
        var cursor = -halfLength;
        var index = 0;
        foreach (var opening in openings.OrderBy(value => value.Center))
        {
            var left = opening.Center - opening.Width * .5f;
            var right = opening.Center + opening.Width * .5f;
            Segment(cursor, left, 0, CeilingHeight, "Pier" + index);
            Segment(left, right, 0, opening.Bottom, "Sill" + index);
            Segment(left, right, opening.Top, CeilingHeight, "Head" + index);
            cursor = right;
            index++;
        }
        Segment(cursor, halfLength, 0, CeilingHeight, "Pier" + index);

        void Segment(float a, float b, float bottom, float top, string suffix)
        {
            if (b - a < .001f || top - bottom < .001f) return;
            var size = sideWall ? new Vector3(WallThickness, top - bottom, b - a)
                : new Vector3(b - a, top - bottom, WallThickness);
            var center = sideWall ? new Vector3(at, (bottom + top) * .5f, (a + b) * .5f)
                : new Vector3((a + b) * .5f, (bottom + top) * .5f, at);
            var segment = Block(room, name + suffix, size, center,
                sideWall ? "786b5a" : "827461", "wallpaper");
            var lining = segment.GetNode<MeshInstance3D>("Visible");
            // The exterior shell already has faces at both structural datums.
            // Seat the plaster finish 2mm into the room and away from the outer
            // facade; carry that same thickness around every reveal and joint.
            // Collision, glass, furniture and opening anchors retain their datums.
            var inward = sideWall ? new Vector3(-Math.Sign(at), 0, 0)
                : new Vector3(0, 0, -Math.Sign(at));
            lining.Position = inward * InteriorFinishThickness;
            ((BoxMesh)lining.Mesh).Size = size + (sideWall
                ? new Vector3(0, InteriorFinishThickness * 2, InteriorFinishThickness * 2)
                : new Vector3(InteriorFinishThickness * 2, InteriorFinishThickness * 2, 0));
            lining.SetMeta("interiorFinishThickness", InteriorFinishThickness);
        }
    }

    private static void BuildWindow(Node3D room, Window window)
    {
        var root = new Node3D { Name = "HeroRoomWindow" + window.Name, Position = window.Center,
            RotationDegrees = new(0, window.YawDegrees, 0) };
        root.SetMeta("openingCenter", window.Center);
        root.SetMeta("clearOpening", new Vector2(WindowWidth, WindowHead - WindowSill));
        room.AddChild(root);
        var height = WindowHead - WindowSill;
        // Closed winter glazing has its own thin contact plane, rather than a
        // solid wall painted with a window or an opening the player can crawl through.
        var glazing = Block(root, "Glazing", new(WindowWidth - .10f, height - .10f, .035f),
            new(0, 0, .07f), "8d9f9f", "glass");
        var glass = glazing.GetNode<MeshInstance3D>("Visible");
        // A single sheet avoids rendering six translucent cube faces. The
        // winter pane stays closed physically, but now shows the real village.
        glass.Mesh = new QuadMesh { Size = new(WindowWidth - .10f, height - .10f) };
        glass.MaterialOverride = WindowGlassMaterial();
        glass.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        glass.SetMeta("windowSurface", "transparent winter glazing; edge frost; actual exterior view");
        foreach (var sign in new[] { -1f, 1f })
        {
            Block(root, sign < 0 ? "JambLeft" : "JambRight", new(.075f, height + .14f, .14f),
                new(sign * (WindowWidth * .5f + .0375f), 0, .13f), "718078", "wood", false);
            Block(root, sign < 0 ? "BottomRail" : "TopRail", new(WindowWidth, .07f, .14f),
                new(0, sign * height * .5f, .13f), "718078", "wood", false);
        }
        Block(root, "Mullion", new(.045f, height, .055f), new(0, 0, .16f), "5d4a38", "wood", false);
        Block(root, "SillBoard", new(1.24f, .065f, .30f), new(0, -height * .5f - .035f, .16f), "765842", "wood", false);
        // Two fabrics have different optical roles and real folds. Both hang
        // from the rod; neither is a painted rectangle against the window.
        var rod = new MeshInstance3D { Name = "CurtainRod", Position = new(0, .82f, .28f),
            RotationDegrees = new(0, 0, 90),
            Mesh = new CylinderMesh { Height = 1.40f, TopRadius = .014f, BottomRadius = .014f, RadialSegments = 8 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("5d4a38", "wood", sheltered: true) };
        root.AddChild(rod);
        foreach (var sign in new[] { -1f, 1f })
        {
            Block(root, sign < 0 ? "RodBracketLeft" : "RodBracketRight", new(.05f, .06f, .25f),
                new(sign * .61f, .82f, .155f), "5d4a38", "wood", false);
            DrapedFabric(root, sign < 0 ? "CurtainLeft" : "CurtainRight", sign * .53f,
                .25f, .81f, -.68f, .28f, .025f, 3, false);
        }
        DrapedFabric(root, "SheerCurtain", 0, 1.04f, .81f, -.67f, .22f, .010f, 7, true);
    }

    private static ShaderMaterial WindowGlassMaterial() => new()
    {
        Shader = new Shader { Code = """
            shader_type spatial;
            render_mode blend_mix, cull_disabled, diffuse_burley, specular_schlick_ggx;
            void fragment() {
                vec2 edge_distance = min(UV, vec2(1.0) - UV);
                float edge = min(edge_distance.x, edge_distance.y);
                float scallop = 0.007 * sin(UV.x * 49.0 + sin(UV.y * 23.0));
                float frost = 1.0 - smoothstep(0.015, 0.075 + scallop, edge);
                ALBEDO = mix(vec3(0.54, 0.66, 0.69), vec3(0.86, 0.91, 0.90), frost);
                ROUGHNESS = mix(0.16, 0.79, frost);
                SPECULAR = mix(0.46, 0.12, frost);
                ALPHA = mix(0.065, 0.59, frost);
            }
            """ }
    };

    private static void DrapedFabric(Node3D root, string name, float centerX, float width,
        float top, float bottom, float depth, float foldDepth, int folds, bool sheer)
    {
        const int columns = 24;
        const int rows = 6;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var vertices = new Vector3[2, rows + 1, columns + 1];
        for (var layer = 0; layer < 2; layer++)
        for (var y = 0; y <= rows; y++)
        for (var x = 0; x <= columns; x++)
        {
            var u = x / (float)columns;
            var v = y / (float)rows;
            var phase = u * Mathf.Tau * folds;
            var gathered = Mathf.Lerp(.88f, 1f, v);
            vertices[layer, y, x] = new(centerX + (u - .5f) * width * gathered,
                Mathf.Lerp(top, bottom, v) + .006f * Mathf.Sin(phase) * v,
                depth + foldDepth * Mathf.Sin(phase) * Mathf.Lerp(.65f, 1f, v) + layer * .003f);
        }
        for (var y = 0; y < rows; y++)
        for (var x = 0; x < columns; x++)
        {
            Quad(vertices[0,y,x], vertices[0,y + 1,x], vertices[0,y + 1,x + 1], vertices[0,y,x + 1]);
            Quad(vertices[1,y,x + 1], vertices[1,y + 1,x + 1], vertices[1,y + 1,x], vertices[1,y,x]);
        }
        for (var y = 0; y < rows; y++)
        {
            Quad(vertices[1,y,0], vertices[1,y + 1,0], vertices[0,y + 1,0], vertices[0,y,0]);
            Quad(vertices[0,y,columns], vertices[0,y + 1,columns], vertices[1,y + 1,columns], vertices[1,y,columns]);
        }
        for (var x = 0; x < columns; x++)
        {
            Quad(vertices[0,0,x], vertices[0,0,x + 1], vertices[1,0,x + 1], vertices[1,0,x]);
            Quad(vertices[1,rows,x], vertices[1,rows,x + 1], vertices[0,rows,x + 1], vertices[0,rows,x]);
        }
        surface.GenerateNormals();
        Material material = sheer ? new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.81f, .79f, .71f, .10f), Roughness = .95f,
            MetallicSpecular = 0f, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        } : PainterlyMaterialLibrary.ForColor("aca590", "fabric_pattern", sheltered: true);
        var fabric = new MeshInstance3D { Name = name, Mesh = surface.Commit(), MaterialOverride = material,
            CastShadow = sheer ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On };
        fabric.SetMeta("householdRole", sheer ? "light transmitting sheer on rod" : "short privacy curtain gathered on rod");
        root.AddChild(fabric);

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            foreach (var vertex in new[] { a,b,c, a,c,d })
            {
                surface.SetUV(new((vertex.X - centerX) / width + .5f, (top - vertex.Y) / (top - bottom)));
                surface.AddVertex(vertex);
            }
        }
    }

    private static void AttachFurniture(Node3D room)
    {
        var kit = GeneratedModularKitDressing.AttachPresentationOnly(room, "style-house-interior-eight-by-seven",
            ["HouseInterior_Table", "HouseInterior_Chair", "HouseInterior_Cupboard", "HouseInterior_Daybed",
             "HouseInterior_StorageChest", "HouseInterior_Hearth", "HouseInterior_LeftWallCupboard",
             "HouseInterior_StorageBasket", "HouseInterior_RugField", "HouseInterior_OldPc"],
            Vector3.Zero, 1, 0);
        kit.Name = "GeneratedHouseInteriorAct1";
        kit.Position = Vector3.Zero;
        kit.SetMeta("stylePresentationModule", "HouseInterior_project_original");
        kit.SetMeta("heroHouseContract", ContractVersion);
        kit.SetMeta("presentationOnly", true);
        kit.SetMeta("visualOnly", true);
        kit.SetMeta("collisionOwner", "none");
        kit.SetMeta("scaledHouseFurnitureMeshCount", 0);
        kit.SetMeta("houseInteriorCompositionPass", "rigid furniture groups; 8x7 shell is built from the exterior opening contract");
        var body = new StaticBody3D { Name = "HouseInteriorFurnitureCollision", CollisionLayer = 1, CollisionMask = 1 };
        body.SetMeta("collisionOwner", "house-interior-floor-furniture");
        room.AddChild(body);
        var count = 0;
        foreach (var mesh in kit.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.Visible || mesh.Mesh is null) continue;
            var name = mesh.Name.ToString();
            Vector3 source, destination;
            var yaw = 0f;
            if (name.StartsWith("HouseInterior_Table", StringComparison.Ordinal)
                || name.StartsWith("HouseInterior_OldPcDocumentFolio", StringComparison.Ordinal))
            { source = new(0, 0, -3.60f); destination = new(0, 0, -2.60f); }
            else if (name.StartsWith("HouseInterior_Chair", StringComparison.Ordinal))
            { source = new(-2.15f, 0, -1.25f); destination = new(-1.25f, 0, -1.20f); }
            else if (name.StartsWith("HouseInterior_Cupboard", StringComparison.Ordinal))
            { source = new(-4.82f, 0, -3.88f); destination = new(3.52f, 0, -2.40f); yaw = -90; }
            else if (name.StartsWith("HouseInterior_Daybed", StringComparison.Ordinal))
            { source = new(4.78f, 0, 1.25f); destination = new(3.05f, 0, .45f); }
            else if (name.StartsWith("HouseInterior_StorageChest", StringComparison.Ordinal))
            { source = new(3.98f, 0, -4.20f); destination = ChestAnchor; yaw = 180; }
            else if (name.StartsWith("HouseInterior_Hearth", StringComparison.Ordinal))
            { source = new(-5f, 0, .55f); destination = StoveAnchor; }
            else if (name.StartsWith("HouseInterior_LeftWallCupboard", StringComparison.Ordinal))
            { source = new(-5.62f, 2.10f, 2.65f); destination = new(-3.63f, 1.86f, 2.15f); }
            else if (name.StartsWith("HouseInterior_StorageBasket", StringComparison.Ordinal))
            { source = new(-4.45f, 0, -.70f); destination = new(-3.55f, 0, -1.65f); }
            else if (name.StartsWith("HouseInterior_RugField", StringComparison.Ordinal))
            { source = new(-1.35f, .07f, .45f); destination = new(-.4f, .017f, .50f); }
            else if (name.StartsWith("HouseInterior_OldPc", StringComparison.Ordinal))
            { source = new(0, 0, -4.68f); destination = new(0, -.25f, -3.40f); }
            else throw new InvalidOperationException($"Unmapped hero room furniture: {name}.");

            var previous = room.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            var move = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(yaw)), destination)
                * new Transform3D(Basis.Identity, -source);
            mesh.GlobalTransform = room.GlobalTransform * move * previous;
            mesh.SetMeta("heroRoomRigidPlacement", true);
            mesh.SetMeta("heroRoomSourceTransform", previous);
            // The flue was authored for a taller ceiling. Trim this architectural
            // member only; its lower collar and stove remain at the source size.
            if (name.StartsWith("HouseInterior_HearthFlue_", StringComparison.Ordinal))
            {
                mesh.Visible = false;
                continue;
            }
            if (!name.EndsWith("_LOD0", StringComparison.Ordinal) || !NeedsContact(name)) continue;
            var relative = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            var shape = new CollisionShape3D { Name = name + "_Contact",
                Position = relative.Origin,
                Shape = new ConvexPolygonShape3D { Points = mesh.Mesh.GetFaces().Select(v => relative.Basis * v).Distinct().ToArray() } };
            shape.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
            body.AddChild(shape);
            count++;
        }
        Block(room, "HearthFlueToCeiling", new(.20f, 1.30f, .20f), StoveAnchor + new Vector3(0, 1.95f, 0), "3e3934", "metal", false);
        Block(room, "HearthCeilingFirestop", new(.48f, .035f, .48f), StoveAnchor + new Vector3(0, 2.58f, 0), "777067", "metal", false);
        room.SetMeta("houseInteriorFloorCollisionProxies", count);
        room.SetMeta("houseInteriorPresentation", "8x7 opening-matched room; project-original GLB furniture with unchanged dimensions");
    }

    private static bool NeedsContact(string name) =>
        new[] { "TableTop", "TableLeg", "Chair", "CupboardBody", "Daybed", "StorageChestBody",
            "StorageChestLid", "HearthBase", "HearthBody", "HearthTop", "LeftWallCupboardBody", "StorageBasketBody" }
        .Any(part => name.StartsWith("HouseInterior_" + part, StringComparison.Ordinal));

    /// <summary>Rigidly seat a complete prop on the actual tilted chest lid.</summary>
    public static void SeatOnChest(Node3D room, Node3D prop)
    {
        var support = room.GetNode<Node3D>("GeneratedHouseInteriorAct1")
            .FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Single(mesh => mesh.Name == "HouseInterior_StorageChestLid_LOD0");
        var origin = room.ToLocal(prop.GlobalPosition);
        var from = support.ToLocal(room.ToGlobal(new(origin.X, 4f, origin.Z)));
        var to = support.ToLocal(room.ToGlobal(new(origin.X, -1f, origin.Z)));
        var faces = support.Mesh.GetFaces();
        var point = Vector3.Zero;
        var normal = Vector3.Up;
        var highest = float.NegativeInfinity;
        for (var i = 0; i + 2 < faces.Length; i += 3)
        {
            var hit = Geometry3D.SegmentIntersectsTriangle(from, to, faces[i], faces[i + 1], faces[i + 2]);
            if (hit.VariantType == Variant.Type.Nil) continue;
            var world = support.ToGlobal(hit.AsVector3());
            if (world.Y <= highest) continue;
            highest = world.Y;
            point = world;
            normal = (support.GlobalBasis.Inverse().Transposed()
                * (faces[i + 1] - faces[i]).Cross(faces[i + 2] - faces[i])).Normalized();
            if (normal.Y < 0) normal = -normal;
        }
        if (float.IsNegativeInfinity(highest)) throw new InvalidOperationException($"No chest lid below {prop.Name}.");
        prop.GlobalBasis = new Basis(new Quaternion(prop.GlobalBasis.Y.Normalized(), normal)) * prop.GlobalBasis;
        var vertices = prop.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Where(mesh => mesh.Mesh is not null).SelectMany(mesh => mesh.Mesh.GetFaces().Select(mesh.ToGlobal)).ToArray();
        if (vertices.Length == 0) throw new InvalidOperationException($"Cannot seat an empty prop: {prop.Name}.");
        prop.GlobalPosition -= normal * vertices.Min(vertex => (vertex - point).Dot(normal));
        prop.SetMeta("furnitureSupportMesh", support.GetPath().ToString());
    }

    private static StaticBody3D Block(Node3D parent, string name, Vector3 size, Vector3 at,
        string color, string surface, bool collision = true)
    {
        var body = new StaticBody3D { Name = name, Position = at, CollisionLayer = collision ? 1u : 0u, CollisionMask = 0 };
        body.SetMeta("collisionOwner", collision ? "house-interior-architecture" : "none");
        var mesh = new MeshInstance3D { Name = "Visible", Mesh = new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface, sheltered: true) };
        body.AddChild(mesh);
        if (collision) body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }
}
