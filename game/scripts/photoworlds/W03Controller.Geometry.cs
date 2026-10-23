using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private const string HouseKitScenePath = "res://assets/models/act1/urman_village_exterior_kit.glb";
    private const string HeroHouseRootName = "HeroHouse_TimberPlaster";
    private const float HouseGroundMinX = -48f;
    private const float HouseGroundMaxX = 48f;
    private const float HouseGroundMinZ = -8f;
    private const float HouseGroundMaxZ = 94f;
    private const float HouseGroundStep = .75f;

    private static readonly Vector2[][] HouseWalkRoutes =
    [
        [new(0, 0), new(0, 7), new(0, 10), new(0, 8.6f), new(-9.5f, 8.6f), new(-10, 20)],
        [new(0, 10), new(0, 8.6f), new(3.5f, 8.6f), new(6.6f, 12), new(8.2f, 14.5f), new(9.3f, 17.4f), new(10, 20)],
        [new(-10, 20), new(-13.5f, 22.4f), new(-18.5f, 25.4f), new(-23.5f, 28.8f), new(-25, 30)],
        [new(-25, 30), new(-25, 34), new(-25, 38)],
        [new(-25, 48), new(-25, 43), new(-25, 38)],
        [new(-25, 38), new(-20, 38), new(-14, 38), new(-8, 38), new(-2, 38), new(4, 38), new(10, 38), new(16, 38), new(22, 38), new(26, 39), new(28, 40)],
        [new(28, 40), new(29.3f, 43), new(30, 46.5f), new(30, 50), new(30, 53)],
        [new(30, 53), new(27, 58), new(23, 62), new(18, 67), new(12, 72)],
        [new(12, 72), new(8.5f, 76), new(4.3f, 80), new(0, 83)]
    ];

    private Node3D _world = null!;
    private Aabb? _houseRoomBounds;

    private void BuildHouseWorld()
    {
        _world = new Node3D { Name = "W03World", Visible = true };
        _world.SetMeta("presentationRole", "walkable summer courtyard; paired doors lead to the same H04 garden");
        AddChild(_world);

        var house = BuildImportedHeroHouse();
        BuildHouseGround();
        BuildHousePassageFrames();
        BuildH04AppleTree();
        BuildH04GardenTable();
        BuildH04GardenChair();
        BuildH07ShadeTree();
        BuildH07GardenBench();
        BuildH05PhotographerStudy(house);
        BuildH06WindowClothDetail();
        BuildHouseShutter(house);
    }

    private void BuildHouseGround()
    {
        var columns = Mathf.RoundToInt((HouseGroundMaxX - HouseGroundMinX) / HouseGroundStep) + 1;
        var rows = Mathf.RoundToInt((HouseGroundMaxZ - HouseGroundMinZ) / HouseGroundStep) + 1;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var faces = new List<Vector3>((columns - 1) * (rows - 1) * 6);

        Vector3 Point(int column, int row)
        {
            var x = HouseGroundMinX + column * HouseGroundStep;
            var z = HouseGroundMinZ + row * HouseGroundStep;
            return new Vector3(x, HouseGroundHeightAt(x, z), z);
        }

        Vector3 NormalAt(Vector3 point)
        {
            const float delta = .20f;
            var dx = (HouseGroundHeightAt(point.X + delta, point.Z)
                - HouseGroundHeightAt(point.X - delta, point.Z)) / (2f * delta);
            var dz = (HouseGroundHeightAt(point.X, point.Z + delta)
                - HouseGroundHeightAt(point.X, point.Z - delta)) / (2f * delta);
            return new Vector3(-dx, 1f, -dz).Normalized();
        }

        void Emit(Vector3 a, Vector3 b, Vector3 c)
        {
            foreach (var point in new[] { a, b, c })
            {
                surface.SetNormal(NormalAt(point));
                surface.SetColor(HouseGroundColor(point.X, point.Z));
                surface.SetUV(new Vector2(point.X, point.Z) * .12f);
                surface.AddVertex(point);
                faces.Add(point);
            }
        }

        for (var row = 0; row + 1 < rows; row++)
        for (var column = 0; column + 1 < columns; column++)
        {
            var a = Point(column, row);
            var b = Point(column, row + 1);
            var c = Point(column + 1, row);
            var d = Point(column + 1, row + 1);
            // Keep the same clockwise upward ground winding used by W02.
            Emit(a, d, b);
            Emit(a, c, d);
        }

        var terrainMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = .96f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        var terrain = new MeshInstance3D
        {
            Name = "W03GroundSurface",
            Mesh = surface.Commit(),
            MaterialOverride = terrainMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        terrain.SetMeta("presentationRole", "continuous planted ground with broad stone-and-earth walk links");
        terrain.SetMeta("footstepSurface", "grass");
        _world!.AddChild(terrain);

        var body = new StaticBody3D
        {
            Name = "W03GroundBody",
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        body.SetMeta("contactPolicy", "same authored height field as W03GroundSurface and the player guard");
        body.SetMeta("footstepSurface", "grass");
        _world.AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Name = "W03GroundTriangles",
            Shape = new ConcavePolygonShape3D { BackfaceCollision = true, Data = faces.ToArray() }
        });
    }

    private static float HouseSmoothRange(float from, float to, float value) =>
        Mathf.SmoothStep(0f, 1f, Mathf.Clamp((value - from) / (to - from), 0f, 1f));

    private float HouseGroundHeightAt(float x, float z)
    {
        var frontRise = HouseSmoothRange(0f, 10f, z);
        var frontFall = 1f - HouseSmoothRange(20f, 31f, z);
        var houseReach = 1f - HouseSmoothRange(11f, 19f, Mathf.Abs(x));
        var houseYard = .30f * frontRise * frontFall * houseReach;

        var eastwardYardGrade = .20f
            * HouseSmoothRange(-25f, 28f, x)
            * HouseSmoothRange(32f, 36f, z)
            * (1f - HouseSmoothRange(56f, 64f, z));
        var ground = Mathf.Max(houseYard, eastwardYardGrade);

        // The same actual inner wall bounds own shelter and the raised floor.
        // Feather outside those bounds; the full room remains at its real floor.
        if (_houseRoomBounds is not { } room) return ground;
        var roomX = HouseSmoothRange(room.Position.X - .60f, room.Position.X, x)
            * (1f - HouseSmoothRange(room.End.X, room.End.X + .60f, x));
        var roomZ = HouseSmoothRange(room.Position.Z - .60f, room.Position.Z, z)
            * (1f - HouseSmoothRange(room.End.Z, room.End.Z + .60f, z));
        var roomFloor = roomX * roomZ;
        return Mathf.Lerp(ground, room.Position.Y, roomFloor);
    }

    private static Color HouseGroundColor(float x, float z)
    {
        var routeDistance = HouseWalkRouteDistance(new Vector2(x, z));
        var path = 1f - HouseSmoothRange(1.12f, 2.15f, routeDistance);
        var wash = .5f + .5f * Mathf.Sin(x * .095f + z * .073f) * Mathf.Sin(z * .12f - x * .047f);
        var meadow = new Color(.43f + wash * .035f, .49f + wash * .038f, .34f + wash * .030f);
        var earth = new Color(.59f + wash * .055f, .50f + wash * .045f, .38f + wash * .035f);
        var frontApron = 1f - HouseSmoothRange(.85f, 2.4f, routeDistance);
        var apronBlend = frontApron * (1f - HouseSmoothRange(10f, 14f, z));
        var stone = new Color(.69f, .62f, .51f);
        return meadow.Lerp(earth, path * .78f).Lerp(stone, apronBlend * .44f);
    }

    private static float HouseWalkRouteDistance(Vector2 point)
    {
        var closest = float.MaxValue;
        foreach (var route in HouseWalkRoutes)
        for (var index = 0; index + 1 < route.Length; index++)
        {
            var start = route[index];
            var run = route[index + 1] - start;
            var t = run.LengthSquared() < .0001f
                ? 0f
                : Mathf.Clamp((point - start).Dot(run) / run.LengthSquared(), 0f, 1f);
            closest = Mathf.Min(closest, point.DistanceTo(start + run * t));
        }

        return closest;
    }

    private Node3D BuildImportedHeroHouse()
    {
        if (!ResourceLoader.Exists(HouseKitScenePath))
            throw new InvalidOperationException($"The authored W03 house kit is missing: {HouseKitScenePath}");
        var packed = ResourceLoader.Load<PackedScene>(HouseKitScenePath)
            ?? throw new InvalidOperationException($"The authored W03 house kit could not load: {HouseKitScenePath}");
        var importedRoot = packed.Instantiate<Node3D>()
            ?? throw new InvalidOperationException($"The authored W03 house kit has no Node3D root: {HouseKitScenePath}");
        var component = FindNamedNode<Node3D>(importedRoot, HeroHouseRootName)
            ?? throw new InvalidOperationException($"The authored W03 house component is missing: {HeroHouseRootName}");
        var sourceParent = component.GetParent()
            ?? throw new InvalidOperationException($"The authored W03 house component has no GLB parent: {HeroHouseRootName}");
        var importedBasis = Act1ConnectedWorld.ComposeImportedAncestorBasis(component);
        sourceParent.RemoveChild(component);
        Act1ConnectedWorld.ClearExtractedSceneOwnership(component);
        importedRoot.Free();

        var placement = new Node3D
        {
            Name = "HeroHousePlacement",
            // The authored porch deck centre becomes H01; the street facade
            // faces the H00 approach instead of presenting the rear glazing.
            Position = new Vector3(-1.1644f, .085f, 11.546f),
            RotationDegrees = new Vector3(0f, 180f, 0f)
        };
        placement.SetMeta("assetSource", HouseKitScenePath);
        placement.SetMeta("componentSource", HeroHouseRootName);
        _world!.AddChild(placement);
        component.Transform = new Transform3D(importedBasis, Vector3.Zero);
        placement.AddChild(component);

        // Only the actual main room is enclosed. The porch and the two garden
        // passage frames remain outdoors; terrain's soft floor blend is not a
        // room boundary. Inner wall faces also follow this imported placement.
        Aabb RoomWall(string side)
        {
            var mesh = FindNamedNode<MeshInstance3D>(component, $"HeroHouse_{side}_Wall_LOD0")
                ?? throw new InvalidOperationException($"The W03 room is missing its authored {side} wall.");
            return PublicBuildingShell.Bounds(_world, mesh);
        }
        var leftWall = RoomWall("Left");
        var rightWall = RoomWall("Right");
        var rearWall = RoomWall("Rear");
        var streetWall = RoomWall("Street");
        var roomMin = new Vector3(Mathf.Min(leftWall.End.X, rightWall.End.X),
            Mathf.Max(Mathf.Max(leftWall.Position.Y, rightWall.Position.Y),
                Mathf.Max(rearWall.Position.Y, streetWall.Position.Y)), Mathf.Min(rearWall.End.Z, streetWall.End.Z));
        var roomMax = new Vector3(Mathf.Max(leftWall.Position.X, rightWall.Position.X),
            Mathf.Min(Mathf.Min(leftWall.End.Y, rightWall.End.Y),
                Mathf.Min(rearWall.End.Y, streetWall.End.Y)), Mathf.Max(rearWall.Position.Z, streetWall.Position.Z));
        if (roomMax.X <= roomMin.X || roomMax.Y <= roomMin.Y || roomMax.Z <= roomMin.Z)
            throw new InvalidOperationException("The W03 authored walls do not enclose a valid room.");
        _houseRoomBounds = new Aabb(roomMin, roomMax - roomMin);

        foreach (var mesh in FindNamedNodes<MeshInstance3D>(component))
        {
            if (!IsW03WinterDecoration(mesh.Name.ToString())) continue;
            mesh.Visible = false;
            mesh.SetMeta("seasonalFilter", "confirmed winter snow and icicle decoration hidden for the summer photo-world copy");
        }

        var houseBody = new StaticBody3D
        {
            Name = "HeroHouseBody",
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        houseBody.SetMeta("contactPolicy", "actual imported LOD0 house faces; carved door opening remains open");
        houseBody.SetMeta("footstepSurface", "wood");
        _world.AddChild(houseBody);
        var contacts = 0;
        // H05 owns this real pane's contacts so its visible off-mullion point
        // resolves to the witness collider. Every other pane remains solid.
        var windowGlass = FindNamedNode<MeshInstance3D>(component, "HeroHouse_Rear_Window0_Glass_LOD0");
        foreach (var mesh in FindNamedNodes<MeshInstance3D>(component))
        {
            if (mesh.Mesh is null || !mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("DoorClosed", StringComparison.Ordinal)
                || IsW03WinterDecoration(mesh.Name.ToString())
                || ReferenceEquals(mesh, windowGlass))
                continue;

            var vertices = mesh.Mesh.GetFaces();
            if (vertices.Length < 3) continue;
            houseBody.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(houseBody, mesh));
            contacts++;
        }
        if (contacts == 0)
            throw new InvalidOperationException("The imported W03 hero house produced no authored collision surfaces.");
        houseBody.SetMeta("authoredContactSurfaceCount", contacts);
        return component;
    }

    private void BuildHousePassageFrames()
    {
        BuildFrameAtAnchor("H02PassageFrame", "H02", blueAccents: true);
        BuildFrameAtAnchor("H03PassageFrame", "H03", blueAccents: false);
        BuildPortalFrame("H02PassageExitFrame", new Vector3(-25f, 0f, 30f), 0f, blueAccents: true);
        BuildPortalFrame("H03PassageExitFrame", new Vector3(-25f, 0f, 48f), 180f, blueAccents: false);
    }

    private Node3D BuildFrameAtAnchor(string name, string anchorName, bool blueAccents)
    {
        var anchor = GetNodeOrNull<Node3D>($"Anchors/{anchorName}")
            ?? throw new InvalidOperationException($"W03 is missing its fixed passport anchor: {anchorName}");
        var frame = new Node3D { Name = name };
        _world!.AddChild(frame);
        frame.GlobalTransform = anchor.GlobalTransform;
        BuildPortalFrameDecoration(frame, blueAccents);
        return frame;
    }

    private void BuildPortalFrame(string name, Vector3 position, float yawDegrees, bool blueAccents)
    {
        var frame = new Node3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        _world!.AddChild(frame);
        BuildPortalFrameDecoration(frame, blueAccents);
    }

    private static void BuildPortalFrameDecoration(Node3D frame, bool blueAccents)
    {
        var frameBody = new StaticBody3D
        {
            Name = "TimberGateStructure",
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        frameBody.SetMeta("footstepSurface", "grass");
        frame.AddChild(frameBody);
        var timber = RuralPropMaterials.Surface("wood", "765a43");
        var blue = RuralPropMaterials.Surface("wood", blueAccents ? "506b7a" : "6d7184");
        var lime = RuralPropMaterials.Surface("concrete", "ddd0b3");
        AddSolidBlock(frameBody, "LeftStoneFoot", new Vector3(.52f, .12f, .48f), new Vector3(-1.12f, .02f, 0f), lime, .025f);
        AddSolidBlock(frameBody, "RightStoneFoot", new Vector3(.52f, .12f, .48f), new Vector3(1.12f, .02f, 0f), lime, .025f);
        AddSolidBlock(frameBody, "LeftOakPost", new Vector3(.22f, 2.62f, .26f), new Vector3(-1.08f, 1.31f, 0f), timber, .016f);
        AddSolidBlock(frameBody, "RightOakPost", new Vector3(.22f, 2.62f, .26f), new Vector3(1.08f, 1.31f, 0f), timber, .016f);
        AddSolidBlock(frameBody, "DeepLintel", new Vector3(2.56f, .24f, .32f), new Vector3(0f, 2.61f, 0f), timber, .025f);
        AddSolidBlock(frameBody, "BlueLintelInlay", new Vector3(1.43f, .075f, .345f), new Vector3(0f, 2.60f, .005f), blue, .014f);
        AddSolidBlock(frameBody, "LeftFootIronCollar", new Vector3(.27f, .16f, .31f), new Vector3(-1.08f, .30f, 0f), blue, .012f);
        AddSolidBlock(frameBody, "RightFootIronCollar", new Vector3(.27f, .16f, .31f), new Vector3(1.08f, .30f, 0f), blue, .012f);
        AddSolidBlock(frameBody, "FlushStoneSill", new Vector3(1.78f, .035f, .26f), new Vector3(0f, .008f, 0f), lime, .012f);
        AddSolidTube(frameBody, "LeftDiagonalBrace", new Vector3(-.96f, 2.02f, .12f), new Vector3(-.48f, 2.48f, .12f), .048f, timber);
        AddSolidTube(frameBody, "RightDiagonalBrace", new Vector3(.96f, 2.02f, .12f), new Vector3(.48f, 2.48f, .12f), .048f, timber);
        AddSolidBlock(frameBody, "CarvedLintelEndLeft", new Vector3(.20f, .31f, .36f), new Vector3(-1.16f, 2.62f, .02f), blue, .02f);
        AddSolidBlock(frameBody, "CarvedLintelEndRight", new Vector3(.20f, .31f, .36f), new Vector3(1.16f, 2.62f, .02f), blue, .02f);
        frame.SetMeta("portalClearWidthMetres", 1.86f);
        frame.SetMeta("portalClearHeightMetres", 2.37f);
        frame.SetMeta("landingPolicy", "flush stone apron over the same continuous ground height field");
    }

    private void BuildH04AppleTree()
    {
        var anchor = RequireAnchor("H04");
        var position = anchor.GlobalPosition + new Vector3(-2f, 2.34f, 3.12f);
        var tree = new StaticBody3D
        {
            Name = "H04AppleTreeWitness",
            Position = position,
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        tree.SetMeta("presentationRole", "one mature apple tree; the visible fruit is the composition witness");
        _world!.AddChild(tree);

        var root = new Node3D { Name = "AppleTreeModel", Position = new Vector3(.5f, -2.34f, -.62f) };
        tree.AddChild(root);
        var trunk = RuralPropMaterials.Surface("wood", "77523c");
        var barkLight = RuralPropMaterials.Surface("wood", "947253");
        var leavesA = SolidMaterial("718c52", .92f);
        var leavesB = SolidMaterial("85965a", .94f);
        var leavesC = SolidMaterial("5f784a", .96f);
        RuralPropGeometry.Tube(root, "CrookedMainTrunk", new Vector3(0f, .04f, 0f), new Vector3(.07f, 2.10f, -.04f), .18f, trunk, 12);
        RuralPropGeometry.Tube(root, "NorthBough", new Vector3(.02f, 1.32f, -.02f), new Vector3(-.52f, 2.65f, -.08f), .10f, barkLight, 10);
        RuralPropGeometry.Tube(root, "WestBough", new Vector3(.04f, 1.52f, -.02f), new Vector3(-1.05f, 2.43f, .16f), .095f, trunk, 10);
        RuralPropGeometry.Tube(root, "EastBough", new Vector3(.06f, 1.58f, -.03f), new Vector3(.78f, 2.68f, -.16f), .10f, barkLight, 10);
        RuralPropGeometry.Tube(root, "UpperFork", new Vector3(.05f, 1.86f, -.04f), new Vector3(.24f, 3.18f, .10f), .075f, trunk, 10);
        RuralPropGeometry.Tube(root, "LowReach", new Vector3(0f, 1.18f, 0f), new Vector3(.76f, 1.98f, .36f), .075f, barkLight, 10);

        AddTreeCrown(root, "CrownWest", new Vector3(-.78f, 2.62f, .12f), new Vector3(.92f, .86f, .88f), leavesA);
        AddTreeCrown(root, "CrownNorth", new Vector3(-.28f, 3.20f, -.02f), new Vector3(.94f, .90f, .87f), leavesB);
        AddTreeCrown(root, "CrownEast", new Vector3(.58f, 2.86f, -.18f), new Vector3(.90f, .83f, .90f), leavesC);
        AddTreeCrown(root, "CrownFront", new Vector3(-.08f, 2.58f, .46f), new Vector3(.88f, .82f, .74f), leavesB);
        AddTreeCrown(root, "CrownHigh", new Vector3(.18f, 3.55f, .08f), new Vector3(.75f, .72f, .70f), leavesA);

        var applePositions = new[]
        {
            new Vector3(-.50f, 2.34f, .62f), new Vector3(-.98f, 2.72f, .16f),
            new Vector3(.30f, 2.56f, .46f), new Vector3(.72f, 2.94f, -.40f),
            new Vector3(-.18f, 3.28f, -.38f), new Vector3(.55f, 3.27f, .16f),
            new Vector3(-.42f, 2.82f, -.48f)
        };
        var appleIndex = 0;
        foreach (var apple in applePositions)
        {
            var radius = appleIndex == 0 ? .14f : .095f;
            var tint = appleIndex % 3 == 0 ? "bd5b43" : appleIndex % 3 == 1 ? "c78a45" : "9a573a";
            var node = RuralPropGeometry.Part(root, "AppleFruit" + appleIndex, LowPolySphere(radius), apple,
                SolidMaterial(tint, .70f));
            AddSphereContact(tree, "AppleFruitContact" + appleIndex, root.Position + apple, radius + .035f);
            RuralPropGeometry.Tube(root, "AppleStem" + appleIndex, apple + new Vector3(0f, radius * .68f, 0f),
                apple + new Vector3(.015f, radius * 1.35f, 0f), .012f, trunk, 8);
            if (appleIndex == 0)
                tree.SetMeta("witnessFruitNode", node.GetPath().ToString());
            appleIndex++;
        }

        tree.AddChild(new CollisionShape3D
        {
            Name = "AppleTreeTrunkContact",
            Shape = new CylinderShape3D { Radius = .19f, Height = 1.92f },
            Position = root.Position + new Vector3(0f, 1.0f, 0f)
        });
    }

    private void BuildH04GardenTable()
    {
        var anchor = RequireAnchor("H04");
        var table = new StaticBody3D
        {
            Name = "H04GardenTable",
            Position = anchor.GlobalPosition + new Vector3(-2.2f, 0f, 1.9f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        table.SetMeta("presentationRole", "one hand-built plank table beneath the H04 apple canopy");
        table.SetMeta("footstepSurface", "wood");
        _world!.AddChild(table);

        var wood = RuralPropMaterials.Surface("wood", "765a41");
        var wornEdge = RuralPropMaterials.Surface("wood", "987858");
        for (var plank = -1; plank <= 1; plank++)
            AddSolidBlock(table, "TabletopPlank" + plank, new Vector3(1.52f, .105f, .255f),
                new Vector3(0f, .77f, plank * .258f), plank == 0 ? wornEdge : wood, .014f);
        AddSolidBlock(table, "TableApronFront", new Vector3(1.38f, .16f, .075f), new Vector3(0f, .64f, .36f), wood, .012f);
        AddSolidBlock(table, "TableApronBack", new Vector3(1.38f, .16f, .075f), new Vector3(0f, .64f, -.36f), wood, .012f);
        AddSolidBlock(table, "TableApronLeft", new Vector3(.075f, .16f, .64f), new Vector3(-.69f, .64f, 0f), wornEdge, .012f);
        AddSolidBlock(table, "TableApronRight", new Vector3(.075f, .16f, .64f), new Vector3(.69f, .64f, 0f), wornEdge, .012f);
        foreach (var x in new[] { -.62f, .62f })
        foreach (var z in new[] { -.27f, .27f })
            AddSolidBlock(table, "TurnedTableLeg", new Vector3(.11f, .68f, .11f), new Vector3(x, .35f, z), wood, .012f);
        AddSolidBlock(table, "LowerTableCrossRail", new Vector3(1.14f, .075f, .08f), new Vector3(0f, .30f, 0f), wornEdge, .008f);
    }

    private void BuildH04GardenChair()
    {
        var anchor = RequireAnchor("H04");
        var seatPosition = anchor.GlobalPosition + new Vector3(4f, .46f, .8f);
        var chair = new StaticBody3D
        {
            Name = "H04GardenChairWitness",
            Position = seatPosition,
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        chair.SetMeta("presentationRole", "one weathered hand-built garden chair; seat surface is the witness");
        chair.SetMeta("footstepSurface", "wood");
        _world!.AddChild(chair);
        var root = new Node3D { Name = "GardenChairModel", Position = new Vector3(0f, -.46f, 0f) };
        chair.AddChild(root);
        var wood = RuralPropMaterials.Surface("wood", "795d45");
        var edge = RuralPropMaterials.Surface("wood", "9b7857");
        foreach (var z in new[] { -.13f, 0f, .13f })
            RuralPropGeometry.Block(root, "SeatSlat", new Vector3(.48f, .045f, .12f), new Vector3(0f, .457f, z), wood, .012f);
        RuralPropGeometry.Block(root, "UpperBackSlat", new Vector3(.46f, .11f, .042f), new Vector3(0f, .80f, -.218f), edge, .014f, new Vector3(-7f, 0f, 0f));
        RuralPropGeometry.Block(root, "LowerBackSlat", new Vector3(.44f, .095f, .04f), new Vector3(0f, .65f, -.19f), wood, .012f, new Vector3(-7f, 0f, 0f));
        foreach (var x in new[] { -.19f, .19f })
        {
            RuralPropGeometry.Tube(root, "FrontTurnedLeg", new Vector3(x * 1.08f, .035f, .19f), new Vector3(x, .432f, .145f), .025f, wood, 12);
            RuralPropGeometry.Tube(root, "RearLeg", new Vector3(x * 1.05f, .035f, -.22f), new Vector3(x, .84f, -.225f), .025f, edge, 12);
            RuralPropGeometry.Tube(root, "SideSeatRail", new Vector3(x, .405f, .16f), new Vector3(x, .405f, -.20f), .022f, wood, 10);
            RuralPropGeometry.Tube(root, "BackSupport", new Vector3(x, .42f, -.17f), new Vector3(x, .86f, -.235f), .026f, wood, 12);
        }
        RuralPropGeometry.Tube(root, "LowerCrossBrace", new Vector3(-.19f, .21f, -.16f), new Vector3(.19f, .21f, -.16f), .018f, edge, 10);
        AddBoxContact(chair, "SeatSurfaceContact", new Vector3(.52f, .09f, .47f), Vector3.Zero);
        AddBoxContact(chair, "BackrestContact", new Vector3(.48f, .31f, .07f), new Vector3(0f, .29f, -.22f));
    }

    private void BuildH07ShadeTree()
    {
        var anchor = RequireAnchor("H07");
        var tree = new StaticBody3D
        {
            Name = "H07ShadeTree",
            Position = anchor.GlobalPosition + new Vector3(.5f, 0f, 3.0f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        tree.SetMeta("presentationRole", "broad summer shade tree beside the H07 resting place");
        _world!.AddChild(tree);
        var model = new Node3D { Name = "BroadleafShadeTreeModel" };
        tree.AddChild(model);

        var bark = RuralPropMaterials.Surface("wood", "715540");
        var branch = RuralPropMaterials.Surface("wood", "927253");
        var foliageA = SolidMaterial("657d4e", .94f);
        var foliageB = SolidMaterial("7c8d52", .92f);
        var foliageC = SolidMaterial("899458", .95f);
        RuralPropGeometry.Tube(model, "ShadeTreeTrunk", new Vector3(0f, .05f, 0f), new Vector3(.10f, 2.72f, -.03f), .22f, bark, 14);
        RuralPropGeometry.Tube(model, "ShadeTreeBoughWest", new Vector3(.06f, 1.72f, 0f), new Vector3(-1.14f, 3.05f, -.10f), .12f, branch, 11);
        RuralPropGeometry.Tube(model, "ShadeTreeBoughEast", new Vector3(.08f, 1.86f, -.02f), new Vector3(1.28f, 3.02f, .10f), .13f, bark, 11);
        RuralPropGeometry.Tube(model, "ShadeTreeBoughNorth", new Vector3(.08f, 2.15f, -.02f), new Vector3(.22f, 3.34f, -1.0f), .10f, branch, 11);
        RuralPropGeometry.Tube(model, "ShadeTreeBoughFront", new Vector3(.06f, 1.96f, .02f), new Vector3(-.18f, 3.08f, 1.05f), .10f, bark, 11);
        AddTreeCrown(model, "ShadeCanopyWest", new Vector3(-1.02f, 3.25f, -.04f), new Vector3(1.35f, .96f, 1.18f), foliageA);
        AddTreeCrown(model, "ShadeCanopyCentre", new Vector3(-.03f, 3.62f, -.04f), new Vector3(1.28f, 1.04f, 1.22f), foliageB);
        AddTreeCrown(model, "ShadeCanopyEast", new Vector3(1.04f, 3.23f, .08f), new Vector3(1.30f, .93f, 1.12f), foliageC);
        AddTreeCrown(model, "ShadeCanopyFront", new Vector3(-.04f, 3.16f, .84f), new Vector3(1.20f, .87f, 1.02f), foliageB);
        AddTreeCrown(model, "ShadeCanopyBack", new Vector3(.12f, 3.20f, -.82f), new Vector3(1.25f, .90f, 1.06f), foliageA);
        AddBoxContact(tree, "ShadeTreeTrunkContact", new Vector3(.40f, 2.72f, .40f), new Vector3(.02f, 1.38f, 0f));
    }

    private void BuildH07GardenBench()
    {
        var anchor = RequireAnchor("H07");
        var bench = new StaticBody3D
        {
            Name = "H07GardenBench",
            Position = anchor.GlobalPosition + new Vector3(2.4f, 0f, 1.2f),
            RotationDegrees = new Vector3(0f, 45f, 0f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        bench.SetMeta("presentationRole", "slatted garden bench in the H07 tree shadow, facing back toward the house");
        bench.SetMeta("footstepSurface", "wood");
        _world!.AddChild(bench);
        var wood = RuralPropMaterials.Surface("wood", "74583f");
        var pale = RuralPropMaterials.Surface("wood", "987756");
        for (var slat = -1; slat <= 1; slat++)
            AddSolidBlock(bench, "BenchSeatSlat" + slat, new Vector3(1.56f, .052f, .14f),
                new Vector3(0f, .455f, slat * .15f), slat == 0 ? pale : wood, .01f);
        AddSolidBlock(bench, "BenchBackLowerSlat", new Vector3(1.48f, .13f, .055f), new Vector3(0f, .69f, -.28f), wood, .01f,
            new Vector3(-5f, 0f, 0f));
        AddSolidBlock(bench, "BenchBackUpperSlat", new Vector3(1.52f, .13f, .055f), new Vector3(0f, .90f, -.31f), pale, .01f,
            new Vector3(-5f, 0f, 0f));
        foreach (var x in new[] { -.62f, .62f })
        {
            AddSolidBlock(bench, "BenchFrontLeg", new Vector3(.09f, .44f, .10f), new Vector3(x, .23f, .18f), wood, .01f);
            AddSolidBlock(bench, "BenchRearLeg", new Vector3(.09f, .87f, .10f), new Vector3(x, .44f, -.26f), pale, .01f,
                new Vector3(-4f, 0f, 0f));
            AddSolidBlock(bench, "BenchArmRest", new Vector3(.09f, .09f, .48f), new Vector3(x, .77f, -.01f), wood, .012f);
            AddSolidTube(bench, "BenchSideRail", new Vector3(x, .36f, .19f), new Vector3(x, .36f, -.24f), .032f, wood, 10);
        }
        AddSolidBlock(bench, "BenchCrossBrace", new Vector3(1.16f, .075f, .08f), new Vector3(0f, .24f, -.22f), wood, .008f);
        AddBoxContact(bench, "BenchSeatContact", new Vector3(1.62f, .10f, .50f), new Vector3(0f, .455f, 0f));
    }

    private void BuildH05PhotographerStudy(Node3D house)
    {
        var h05 = RequireAnchor("H05");
        var stance = new Marker3D
        {
            Name = "H05PhotographerWitness",
            Position = h05.GlobalPosition,
            Rotation = h05.GlobalRotation
        };
        _world!.AddChild(stance);

        var board = new StaticBody3D
        {
            Name = "H05CameraStudyFence",
            Position = new Vector3(30f, .2f, 41f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        board.SetMeta("presentationRole", "ground-set limewashed garden fence panel carrying the real camera-strap shadow");
        board.SetMeta("footstepSurface", "grass");
        _world.AddChild(board);
        var plaster = RuralPropMaterials.Surface("concrete", "d7cbb0");
        var timber = RuralPropMaterials.Surface("wood", "6d5844");
        AddSolidBlock(board, "LimewashedFenceInfill", new Vector3(.10f, 1.85f, 1.54f), new Vector3(0f, .99f, 0f), plaster, .02f);
        AddSolidBlock(board, "FenceCapRail", new Vector3(.18f, .14f, 1.72f), new Vector3(0f, 1.98f, 0f), timber, .02f);
        AddSolidBlock(board, "FencePostLeft", new Vector3(.18f, 2.02f, .14f), new Vector3(0f, 1.02f, -.82f), timber, .02f);
        AddSolidBlock(board, "FencePostRight", new Vector3(.18f, 2.02f, .14f), new Vector3(0f, 1.02f, .82f), timber, .02f);
        AddSolidBlock(board, "FenceMasonryToe", new Vector3(.58f, .10f, 1.95f), new Vector3(.02f, .05f, 0f), RuralPropMaterials.Surface("concrete", "9c8b70"), .02f);
        AddSolidTube(board, "UpperPeg", new Vector3(-.10f, 1.65f, -.26f), new Vector3(-.28f, 1.65f, -.26f), .027f, RuralPropMaterials.Surface("metal", "5c6667"), 12);

        // The ribbon hangs in front of a sunlit plaster face, so the real
        // directional light produces its shadow on the same surface the player sees.
        var strap = new StaticBody3D
        {
            Name = "H05StrapShadowWitness",
            Position = new Vector3(29.80f, 1.42f, 40.96f),
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        strap.SetMeta("presentationRole", "fabric camera strap with a cast shadow on the adjacent lime panel");
        _world.AddChild(strap);
        var strapRibbon = new Node3D { Name = "CameraStrapRibbon", Position = new Vector3(0f, 0f, 0f) };
        strap.AddChild(strapRibbon);
        var points = new[]
        {
            new Vector3(0f, .38f, .19f), new Vector3(0f, .25f, .30f), new Vector3(0f, .02f, .32f),
            new Vector3(0f, -.25f, .22f), new Vector3(0f, -.34f, .02f), new Vector3(0f, -.25f, -.19f),
            new Vector3(0f, .00f, -.27f), new Vector3(0f, .27f, -.19f)
        };
        AddRibbon(strapRibbon, "WovenShoulderStrap", points, .064f, .012f, RuralPropMaterials.Surface("cloth", "81654c"));
        var cameraAttachmentLeft = new Vector3(-.16f, -.62f, .11f);
        var cameraAttachmentRight = new Vector3(0f, -.62f, .11f);
        var strapLeather = RuralPropMaterials.Surface("cloth", "765940");
        AddRibbon(strapRibbon, "CameraStrapTailLeft", new[] { points[0], new Vector3(-.05f, .13f, .17f), cameraAttachmentLeft },
            .052f, .012f, strapLeather);
        AddRibbon(strapRibbon, "CameraStrapTailRight", new[] { points[^1], new Vector3(-.02f, .08f, -.12f), cameraAttachmentRight },
            .052f, .012f, strapLeather);
        foreach (var point in points)
            AddSphereContact(strap, "StrapLoopContact", point, .075f);
        AddSphereContact(strap, "StrapWitnessContact", Vector3.Zero, .11f);
        AddSphereContact(strap, "CameraStrapAttachmentLeft", cameraAttachmentLeft, .07f);
        AddSphereContact(strap, "CameraStrapAttachmentRight", cameraAttachmentRight, .07f);
        RuralPropGeometry.Part(strapRibbon, "StrapShoulderPad", RuralPropGeometry.BowedPanel(.24f, .22f, .035f, .035f, .018f),
            new Vector3(0f, -.04f, .26f), RuralPropMaterials.Surface("cloth", "70553f"));

        // A compact box camera and lens explain what the loop belongs to, while
        // leaving the diagonal sight line from H05 to the real house pane open.
        var cameraRig = new Node3D { Name = "CameraRig", Position = new Vector3(-.12f, .48f, .05f) };
        board.AddChild(cameraRig);
        AddSolidBlock(cameraRig, "MetalCameraBody", new Vector3(.25f, .16f, .19f), new Vector3(-.16f, .04f, .02f),
            RuralPropMaterials.Surface("metal", "3e4748"), .035f);
        RuralPropGeometry.Tube(cameraRig, "CameraLens", new Vector3(-.31f, .06f, .02f), new Vector3(-.43f, .06f, .02f),
            .062f, RuralPropMaterials.Surface("metal", "1f272a"), 16);
        var lensGlass = RuralPropGeometry.Part(cameraRig, "BlueGlassLens", new CylinderMesh { TopRadius = .047f, BottomRadius = .047f, Height = .018f, RadialSegments = 16 },
            new Vector3(-.44f, .06f, .02f), SolidMaterial("587687", .28f));
        lensGlass.RotationDegrees = new Vector3(0f, 0f, 90f);
        AddBoxContact(board, "CameraBodyContact", new Vector3(.25f, .16f, .19f), new Vector3(-.28f, .52f, .07f));
        AddSphereContact(board, "CameraLensContact", new Vector3(-.50f, .54f, .07f), .065f);

        var pane = FindNamedNode<MeshInstance3D>(house, "HeroHouse_Rear_Window0_Glass_LOD0")
            ?? throw new InvalidOperationException("The shipped W03 hero house is missing its rear window composition subject.");
        // Keep the existing in-pane offset clear of the actual central mullion.
        var windowWitnessPosition = _world.ToLocal(pane.ToGlobal(new Vector3(.22f, 0f, 0f)));
        var windowWitness = new StaticBody3D
        {
            Name = "H05WindowCompositionWitness",
            Position = windowWitnessPosition,
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        windowWitness.SetMeta("presentationRole", "actual imported HeroHouse Rear Window 0 glass composition point visible from H05");
        _world.AddChild(windowWitness);
        windowWitness.AddChild(Act1ConnectedWorld.AuthoredSurfaceContact(windowWitness, pane));
    }

    private void BuildH06WindowClothDetail()
    {
        var anchor = RequireAnchor("H06");
        var window = new Node3D { Name = "H06WindowFrame", Position = anchor.GlobalPosition };
        window.SetMeta("presentationRole", "supported exterior window detail; cloth moves from its hook to the real sill");
        _world!.AddChild(window);

        var body = new StaticBody3D
        {
            Name = "H06TimberWallBody",
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        body.SetMeta("footstepSurface", "grass");
        window.AddChild(body);
        var timber = RuralPropMaterials.Surface("wood", "76583f");
        var paleTimber = RuralPropMaterials.Surface("wood", "a18a67");
        var plaster = RuralPropMaterials.Surface("concrete", "d4c4a3");
        var steel = RuralPropMaterials.Surface("metal", "4e5c5d");

        AddSolidBlock(body, "GroundSillBeam", new Vector3(3.0f, .20f, .34f), new Vector3(0f, .11f, 0f), plaster, .025f);
        AddSolidBlock(body, "LeftStructuralPost", new Vector3(.24f, 2.92f, .26f), new Vector3(-1.23f, 1.51f, 0f), timber, .018f);
        AddSolidBlock(body, "RightStructuralPost", new Vector3(.24f, 2.92f, .26f), new Vector3(1.23f, 1.51f, 0f), timber, .018f);
        AddSolidBlock(body, "LowerWallUnderWindow", new Vector3(2.22f, .70f, .22f), new Vector3(0f, .55f, .02f), plaster, .012f);
        AddSolidBlock(body, "UpperWallOverWindow", new Vector3(2.22f, .42f, .22f), new Vector3(0f, 2.61f, .02f), plaster, .012f);
        AddSolidBlock(body, "WideExteriorLintel", new Vector3(2.62f, .15f, .36f), new Vector3(0f, 2.91f, 0f), timber, .018f);
        AddSolidBlock(body, "LeftWindowJamb", new Vector3(.12f, 1.25f, .27f), new Vector3(-.70f, 1.73f, -.015f), paleTimber, .014f);
        AddSolidBlock(body, "RightWindowJamb", new Vector3(.12f, 1.25f, .27f), new Vector3(.70f, 1.73f, -.015f), paleTimber, .014f);
        AddSolidBlock(body, "WindowHeadCasing", new Vector3(1.55f, .13f, .31f), new Vector3(0f, 2.36f, -.02f), paleTimber, .014f);
        AddSolidBlock(body, "StoneWindowSill", new Vector3(1.66f, .12f, .42f), new Vector3(0f, 1.08f, -.055f), RuralPropMaterials.Surface("concrete", "c4b493"), .02f);
        AddSolidBlock(body, "SillDripMoulding", new Vector3(1.56f, .045f, .08f), new Vector3(0f, 1.015f, -.24f), paleTimber, .008f);
        AddSolidBlock(body, "WindowMullion", new Vector3(.055f, 1.20f, .12f), new Vector3(0f, 1.73f, -.12f), timber, .008f);
        AddSolidBlock(body, "WindowCrossRail", new Vector3(1.30f, .06f, .12f), new Vector3(0f, 1.70f, -.12f), timber, .008f);
        AddSolidBlock(body, "RecessedBlueShadow", new Vector3(1.30f, 1.16f, .035f), new Vector3(0f, 1.73f, .086f),
            RuralPropMaterials.Surface("wood", "46545c"), .006f);
        AddSolidBlock(body, "DimBlueWindowPane", new Vector3(1.23f, 1.10f, .022f), new Vector3(0f, 1.73f, -.068f),
            SolidMaterial("8299a0", .32f), .002f);
        AddSolidBlock(body, "LeftRafter", new Vector3(.13f, .62f, 2.35f), new Vector3(-.87f, 3.10f, -.16f), timber, .012f,
            new Vector3(-8f, 0f, -14f));
        AddSolidBlock(body, "RightRafter", new Vector3(.13f, .62f, 2.35f), new Vector3(.87f, 3.10f, -.16f), timber, .012f,
            new Vector3(-8f, 0f, 14f));
        AddSolidBlock(body, "LeanToRoofBoard", new Vector3(3.25f, .13f, 2.55f), new Vector3(0f, 3.35f, -.22f),
            RuralPropMaterials.Surface("wood", "5b534b"), .02f, new Vector3(-8f, 0f, 0f));

        var cloth = new Node3D { Name = "Cloth", Position = new Vector3(0f, 1.74f, -.205f) };
        window.AddChild(cloth);
        RuralPropGeometry.Part(cloth, "WovenWindowCloth",
            RuralPropGeometry.BowedPanel(.91f, .92f, .02f, .035f, .045f), Vector3.Zero,
            RuralPropMaterials.Surface("cloth", "d5c4a5"));
        for (var fold = -2; fold <= 2; fold++)
        {
            var x = fold * .15f;
            RuralPropGeometry.Tube(cloth, "SoftClothFold", new Vector3(x, -.43f, .025f), new Vector3(x * .92f, .43f, .025f), .009f,
                RuralPropMaterials.Surface("cloth", fold % 2 == 0 ? "eee2c7" : "bcae91"), 8);
        }

        var hook = new Node3D { Name = "ClothHook", Position = new Vector3(-.55f, 2.32f, -.20f) };
        window.AddChild(hook);
        RuralPropGeometry.Tube(hook, "ForgedHookStem", Vector3.Zero, new Vector3(0f, 0f, .11f), .018f, steel, 12);
        RuralPropGeometry.Tube(hook, "ForgedHookBend", new Vector3(0f, 0f, .11f), new Vector3(.035f, .015f, .16f), .018f, steel, 12);
        RuralPropGeometry.Tube(hook, "ForgedHookTip", new Vector3(.035f, .015f, .16f), new Vector3(.095f, .035f, .14f), .016f, steel, 12);

        var shelf = new Node3D
        {
            Name = "ClothShelf",
            Position = new Vector3(0f, 1.22f, -.27f),
            Rotation = new Vector3(Mathf.Pi * .5f, 0f, 0f)
        };
        window.AddChild(shelf);
        AddSolidBlock(body, "ClothShelfBoard", new Vector3(1.10f, .075f, .33f), shelf.Position - Vector3.Up * .04f, paleTimber, .014f);
        AddSolidTube(body, "ClothShelfBracketLeft", new Vector3(-.43f, .84f, -.10f), new Vector3(-.43f, 1.13f, -.15f), .028f, timber);
        AddSolidTube(body, "ClothShelfBracketRight", new Vector3(.43f, .84f, -.10f), new Vector3(.43f, 1.13f, -.15f), .028f, timber);
        window.SetMeta("windowClearOpeningMetres", new Vector2(1.30f, 1.16f));
        window.SetMeta("supportPolicy", "paired ground-set posts, lower wall, lintel, and cantilevered roof braces");
    }

    private void BuildHouseShutter(Node3D house)
    {
        var window = FindNamedNode<MeshInstance3D>(house, "HeroHouse_Street_Window2_Glass_LOD0")
            ?? throw new InvalidOperationException("The shipped W03 hero house is missing its second street window for the shutter action.");
        var pivot = new Node3D
        {
            Name = "ShutterPivot",
            Transform = _world.GlobalTransform.AffineInverse() * window.GlobalTransform
                * new Transform3D(Basis.Identity, new Vector3(-.50f, 0f, .12f))
        };
        _world!.AddChild(pivot);
        var shutter = new StaticBody3D
        {
            Name = "TimberShutterBody",
            CollisionLayer = 1u,
            CollisionMask = 1u
        };
        pivot.AddChild(shutter);
        var wood = RuralPropMaterials.Surface("wood", "5f7180");
        var edge = RuralPropMaterials.Surface("wood", "927b60");
        for (var plank = 0; plank < 3; plank++)
            AddSolidBlock(shutter, "VerticalShutterPlank" + plank, new Vector3(.15f, 1.38f, .065f),
                new Vector3(.23f + plank * .145f, 0f, 0f), wood, .01f);
        AddSolidBlock(shutter, "UpperCrossRail", new Vector3(.47f, .10f, .09f), new Vector3(.43f, .55f, .015f), edge, .01f);
        AddSolidBlock(shutter, "LowerCrossRail", new Vector3(.47f, .10f, .09f), new Vector3(.43f, -.55f, .015f), edge, .01f);
        AddSolidBlock(shutter, "DiagonalBrace", new Vector3(.075f, .90f, .065f), new Vector3(.43f, 0f, .03f), edge, .008f,
            new Vector3(0f, 0f, -26f));
        AddSolidTube(shutter, "HingeTop", new Vector3(-.035f, .48f, -.01f), new Vector3(-.035f, .57f, -.01f), .035f,
            RuralPropMaterials.Surface("metal", "546265"), 12);
        AddSolidTube(shutter, "HingeBottom", new Vector3(-.035f, -.57f, -.01f), new Vector3(-.035f, -.48f, -.01f), .035f,
            RuralPropMaterials.Surface("metal", "546265"), 12);
        var target = new Marker3D { Name = "ShutterTarget", Position = new Vector3(.43f, .05f, .22f) };
        pivot.AddChild(target);
        pivot.SetMeta("actionSurface", "three real planks, two rails, a diagonal brace, and two metal pin hinges");
    }

    private Node3D RequireAnchor(string name) => GetNodeOrNull<Node3D>($"Anchors/{name}")
        ?? throw new InvalidOperationException($"W03 is missing its fixed passport anchor: {name}");

    private static void AddTreeCrown(Node3D root, string name, Vector3 at, Vector3 scale, Material material)
    {
        var sphere = new SphereMesh { Radius = .78f, Height = 1.56f, RadialSegments = 9, Rings = 5 };
        RuralPropGeometry.Part(root, name, sphere, at, material).Scale = scale;
    }

    private static bool IsW03WinterDecoration(string nodeName) =>
        nodeName.Contains("RoofSnow", StringComparison.Ordinal)
        || nodeName.Contains("PorchRoofSnow", StringComparison.Ordinal)
        || nodeName.Contains("Icicle", StringComparison.Ordinal);

    private static MeshInstance3D AddSolidBlock(
        Node3D parent, string name, Vector3 size, Vector3 at, Material material, float bevel = .01f,
        Vector3? rotationDegrees = null)
    {
        var rotation = rotationDegrees ?? Vector3.Zero;
        var mesh = RuralPropGeometry.Block(parent, name, size, at, material, bevel, rotation);
        if (parent is StaticBody3D body)
        {
            body.AddChild(new CollisionShape3D
            {
                Name = name + "Contact",
                Shape = new BoxShape3D { Size = size },
                Position = at,
                RotationDegrees = rotation
            });
        }
        return mesh;
    }

    private static MeshInstance3D AddSolidTube(
        Node3D parent, string name, Vector3 a, Vector3 b, float radius, Material material, int sides = 16)
    {
        var mesh = RuralPropGeometry.Tube(parent, name, a, b, radius, material, sides);
        if (parent is StaticBody3D body)
        {
            body.AddChild(new CollisionShape3D
            {
                Name = name + "Contact",
                Shape = new CylinderShape3D { Radius = radius + .012f, Height = a.DistanceTo(b) },
                Transform = mesh.Transform
            });
        }
        return mesh;
    }

    private static void AddBoxContact(StaticBody3D body, string name, Vector3 size, Vector3 at)
        => body.AddChild(new CollisionShape3D
        {
            Name = name,
            Shape = new BoxShape3D { Size = size },
            Position = at
        });

    private static void AddSphereContact(StaticBody3D body, string name, Vector3 at, float radius)
        => body.AddChild(new CollisionShape3D
        {
            Name = name,
            Shape = new SphereShape3D { Radius = radius },
            Position = at
        });

    private static void AddRibbon(
        Node3D parent, string name, IReadOnlyList<Vector3> centreline, float width, float thickness, Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            surface.AddVertex(a);
            surface.AddVertex(b);
            surface.AddVertex(c);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        for (var index = 0; index + 1 < centreline.Count; index++)
        {
            var start = centreline[index];
            var end = centreline[index + 1];
            var tangent = (end - start).Normalized();
            var side = new Vector3(0f, -tangent.Z, tangent.Y).Normalized() * (width * .5f);
            var front = Vector3.Right * (thickness * .5f);
            var back = -front;
            var a = start - side;
            var b = start + side;
            var c = end + side;
            var d = end - side;
            Quad(a + front, b + front, c + front, d + front);
            Quad(d + back, c + back, b + back, a + back);
            Quad(a + front, d + front, d + back, a + back);
            Quad(b + back, c + back, c + front, b + front);
        }

        surface.GenerateNormals();
        RuralPropGeometry.Part(parent, name, surface.Commit(), Vector3.Zero, material);
    }

    private static Mesh LowPolySphere(float radius)
        => new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 12, Rings = 6 };

    private static StandardMaterial3D SolidMaterial(string hex, float roughness)
        => new()
        {
            AlbedoColor = Color.FromHtml(hex),
            Roughness = roughness,
            Metallic = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

    private static T? FindNamedNode<T>(Node root, string name) where T : Node
    {
        if (root is T match && root.Name.ToString() == name) return match;
        foreach (var child in root.GetChildren())
        {
            if (child is not Node node) continue;
            if (FindNamedNode<T>(node, name) is { } found) return found;
        }
        return null;
    }

    private static IEnumerable<T> FindNamedNodes<T>(Node root) where T : Node
    {
        if (root is T match) yield return match;
        foreach (var child in root.GetChildren())
        {
            if (child is not Node node) continue;
            foreach (var found in FindNamedNodes<T>(node)) yield return found;
        }
    }
}
