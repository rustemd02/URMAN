using System;
using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    private const string CellarWoodTexturePath = "res://assets/textures/painterly/urman_w05_v02_basecolor.png";
    private const float FloorMinX = -42.5f;
    private const float FloorMaxX = 42.5f;
    private const float FloorMinZ = -8f;
    private const float FloorMaxZ = 87f;
    private const float FloorThickness = .24f;
    private StaticBody3D _floorBody = null!;
    private MeshInstance3D _floorSurface = null!;
    private CarryableProp _familyBox = null!;
    private Node3D _boxRest = null!;
    private Node3D _boxStart = null!;
    private Node3D _responseObject = null!;
    private Node3D _responseRest = null!;
    private Node3D _bichuraMount = null!;
    private Node3D _niche = null!;
    private readonly Node3D[] _householdTraces = new Node3D[3];
    private StandardMaterial3D _wood = null!;
    private StandardMaterial3D _darkWood = null!;

    private void BuildCellarWorld()
    {
        _world = new Node3D { Name = "W05World", Visible = false };
        _world.SetMeta("artAcceptance", "engineering source candidate; A-CELLAR-KIT and A-BICHURA acceptance pending");
        _world.SetMeta("passportBoundsMetres", "85x95; x[-42.5,42.5], z[-8,87]");
        AddChild(_world);
        _wood = CellarWood("CellarDryWood", "d3ad77");
        _darkWood = CellarWood("CellarStructuralWood", "987344");
        BuildCellarFloor();
        BuildCellarStructure();
        BuildCellarTable();
        BuildCellarShelf("LeftHouseholdShelf", new Vector3(-16.5f, 0, 24f));
        BuildCellarShelf("RightHouseholdShelf", new Vector3(16.5f, 0, 24f));
        BuildCellarBox();
        BuildCellarResponse();
        BuildCellarNiche();
        BuildCellarLightWell();
        BuildCellarThreshold();
        BuildCellarLights();
        BuildCellarRouteMarkers();
    }

    private static StandardMaterial3D CellarWood(string name, string tint)
    {
        var material = new StandardMaterial3D
        {
            ResourceName = name,
            AlbedoColor = Color.FromHtml(tint),
            AlbedoTexture = ResourceLoader.Load<Texture2D>(CellarWoodTexturePath),
            Roughness = .88f,
            MetallicSpecular = .18f,
            Uv1Scale = new Vector3(.34f, .34f, 1f)
        };
        material.SetMeta("sourceTexture", CellarWoodTexturePath);
        return material;
    }

    // The surface, side faces and collision all come from these same boards.
    // The shallow ramps hit every passport foot height without hidden support planes.
    private void BuildCellarFloor()
    {
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            void Vertex(Vector3 point)
            {
                tool.SetNormal(normal);
                tool.SetUV(Mathf.Abs(normal.Y) > .5f ? new Vector2(point.X, point.Z)
                    : new Vector2(point.X + point.Z, point.Y));
                tool.AddVertex(point);
            }
            if ((b - a).Cross(c - a).Dot(normal) > 0f) (b, d) = (d, b);
            Vertex(a); Vertex(b); Vertex(c); Vertex(a); Vertex(c); Vertex(d);
        }
        float[] rows = [FloorMinZ, 0f, 10f, 22f, 28f, 38f, 40f, 53f, 56f, 60f, 72f, 76f, FloorMaxZ];
        const int boardCount = 106;
        var width = (FloorMaxX - FloorMinX) / boardCount;
        for (var board = 0; board < boardCount; board++)
        for (var row = 0; row + 1 < rows.Length; row++)
        {
            var x0 = FloorMinX + board * width;
            var x1 = x0 + width;
            var z0 = rows[row];
            var z1 = rows[row + 1];
            var y0 = CellarFloorHeight(z0);
            var y1 = CellarFloorHeight(z1);
            var a = new Vector3(x0, y0, z0);
            var b = new Vector3(x1, y0, z0);
            var c = new Vector3(x1, y1, z1);
            var d = new Vector3(x0, y1, z1);
            var down = Vector3.Down * FloorThickness;
            Quad(a, b, c, d, new Vector3(0f, z1 - z0, y0 - y1).Normalized());
            Quad(d + down, c + down, b + down, a + down, Vector3.Down);
            Quad(a + down, b + down, b, a, Vector3.Forward);
            Quad(b + down, c + down, c, b, Vector3.Right);
            Quad(c + down, d + down, d, c, Vector3.Back);
            Quad(d + down, a + down, a, d, Vector3.Left);
        }
        var mesh = tool.Commit();
        _floorSurface = new MeshInstance3D { Name = "W05FloorSurface", Mesh = mesh, MaterialOverride = _wood };
        _floorSurface.SetMeta("footstepSurface", "wood");
        _world.AddChild(_floorSurface);
        _floorBody = Body(_world, "W05FloorBody", Vector3.Zero);
        _floorBody.AddChild(new CollisionShape3D { Name = "BoardSurfaceContact", Shape = mesh.CreateTrimeshShape() });
        // The same batched mesh carries board-edge grooves; one draw call, no per-board nodes.
        using var joints = new SurfaceTool();
        joints.Begin(Mesh.PrimitiveType.Triangles);
        for (var index = 1; index < boardCount; index++)
        for (var row = 0; row + 1 < rows.Length; row++)
        {
            var x = FloorMinX + index * width;
            var a = new Vector3(x - .003f, CellarFloorHeight(rows[row]) + .001f, rows[row]);
            var b = new Vector3(x + .003f, a.Y, a.Z);
            var c = new Vector3(b.X, CellarFloorHeight(rows[row + 1]) + .001f, rows[row + 1]);
            var d = new Vector3(a.X, c.Y, c.Z);
            foreach (var point in new[] { a, b, c, a, c, d })
            {
                joints.SetNormal(Vector3.Up);
                joints.SetUV(new Vector2(point.X, point.Z));
                joints.AddVertex(point);
            }
        }
        _world.AddChild(new MeshInstance3D { Name = "InlaidBoardJoints", Mesh = joints.Commit(),
            MaterialOverride = RuralPropMaterials.Surface("wood", "5c4430") });
    }

    private static float CellarFloorHeight(float z) => z < 28f ? 0f
        : z < 38f ? (z - 28f) * .02f
        : z <= 60f ? .2f
        : z < 72f ? .2f * (72f - z) / 12f : 0f;

    private void BuildCellarStructure()
    {
        var structure = Body(_world, "SupportedTimberEnvelope", Vector3.Zero);
        for (var z = -6f; z <= 86f; z += 8f)
        {
            var floor = CellarFloorHeight(z);
            Solid(structure, $"UnderfloorJoist{z}", new Vector3(85f, .38f, .46f),
                new Vector3(0, floor - FloorThickness - .19f, z), _darkWood);
            foreach (var x in new[] { -40f, -24f, 24f, 40f })
            {
                Solid(structure, $"Footing{x}_{z}", new Vector3(1.2f, .3f, 1.2f),
                    new Vector3(x, floor - .95f, z), _darkWood);
                Solid(structure, $"JoistPost{x}_{z}", new Vector3(.48f, .42f, .48f),
                    new Vector3(x, floor - .59f, z), _darkWood);
            }
        }
        // All corridor-side posts remain outside the nine authored walking links.
        foreach (var z in new[] { -6f, 10f, 26f, 42f, 58f, 74f, 86f })
        {
            foreach (var x in new[] { -40f, -26f, 26f, 40f })
            {
                var floor = CellarFloorHeight(z);
                Solid(structure, $"MainPost{x}_{z}", new Vector3(.6f, 6.4f - floor, .6f),
                    new Vector3(x, (6.4f + floor) * .5f, z), _darkWood);
            }
            Solid(structure, $"RoofCrossbeam{z}", new Vector3(85f, .65f, .7f), new Vector3(0, 6.4f, z), _darkWood);
        }
        foreach (var x in new[] { -42.1f, 42.1f })
        {
            Solid(structure, $"SideWall{x}", new Vector3(.8f, 6.8f, 95f), new Vector3(x, 3.1f, 39.5f), _wood);
            Solid(structure, $"LongitudinalPlate{x}", new Vector3(.8f, .5f, 95f), new Vector3(x, 6.7f, 39.5f), _darkWood);
        }
        foreach (var z in new[] { -7.6f, 86.6f })
            Solid(structure, $"EndWall{z}", new Vector3(85f, 6.8f, .8f), new Vector3(0, 3.1f, z), _wood);
        // Boards over B05 have a real light opening; no continuous opaque roof under the slit.
        Solid(structure, "RoofNear", new Vector3(84.2f, .22f, 56f), new Vector3(0, 6.95f, 20f), _wood);
        Solid(structure, "RoofFar", new Vector3(84.2f, .22f, 30f), new Vector3(0, 6.95f, 72f), _wood);
        Solid(structure, "RoofSlitLeft", new Vector3(25.8f, .22f, 9f), new Vector3(-29.2f, 6.95f, 52.5f), _wood);
        Solid(structure, "RoofSlitRight", new Vector3(56.1f, .22f, 9f), new Vector3(14.05f, 6.95f, 52.5f), _wood);
        var beam = Body(_world, "B04CrossingBeam", Vector3.Zero);
        Solid(beam, "LowBeam", new Vector3(12f, .56f, .8f), new Vector3(0, 3.02f, 40f), _darkWood);
        foreach (var side in new[] { -5.8f, 5.8f })
            Solid(beam, $"BeamSupport{side}", new Vector3(.4f, 2.54f, .5f), new Vector3(side, 1.47f, 40f), _darkWood);
        beam.SetMeta("minimumHeadroomMetres", 2.54f);
    }

    private void BuildCellarTable()
    {
        var table = Body(_world, "B01HouseholdTable", new Vector3(0, 0, 15.4f));
        BuildTableMembers(table, new Vector3(4.8f, 1.06f, 1.45f));
        var ring = new MeshInstance3D
        {
            Name = "DryCupRing", Position = new Vector3(-1.35f, 1.066f, -.28f),
            Mesh = new TorusMesh { InnerRadius = .105f, OuterRadius = .119f, Rings = 20, RingSegments = 8 },
            Scale = new Vector3(1f, .018f, 1f), MaterialOverride = RuralPropMaterials.Surface("wood", "6e4d30")
        };
        table.AddChild(ring);
        _householdTraces[0] = Marker(table, "DryCircleWitness", new Vector3(-1.35f, 1.07f, -.28f));
        Solid(table, "WornFrontEdge", new Vector3(1.1f, .045f, .06f), new Vector3(0, 1.027f, -.725f),
            RuralPropMaterials.Surface("wood", "e8c28c"));
        _householdTraces[1] = Marker(table, "WornEdgeWitness", new Vector3(0, 1.06f, -.74f));
        var cloth = Body(table, "FoldedHouseholdCloth", new Vector3(1.35f, 1.08f, -.20f));
        var clothMaterial = RuralPropMaterials.Surface("cloth", "83958a");
        for (var fold = 0; fold < 3; fold++)
            Solid(cloth, $"WovenFold{fold}", new Vector3(.62f, .018f, .42f), new Vector3(fold * .008f, fold * .019f, 0), clothMaterial, .005f);
        _householdTraces[2] = Marker(cloth, "FoldedClothWitness", Vector3.Up * .06f);
    }

    private void BuildTableMembers(StaticBody3D table, Vector3 size)
    {
        Solid(table, "Top", new Vector3(size.X, .12f, size.Z), new Vector3(0, size.Y - .06f, 0), _wood);
        foreach (var x in new[] { -size.X * .5f + .17f, size.X * .5f - .17f })
        foreach (var z in new[] { -size.Z * .5f + .17f, size.Z * .5f - .17f })
            Solid(table, $"Leg{x}_{z}", new Vector3(.16f, size.Y - .12f, .16f),
                new Vector3(x, (size.Y - .12f) * .5f, z), _darkWood);
        Solid(table, "UnderTopRail", new Vector3(size.X - .25f, .22f, .08f),
            new Vector3(0, size.Y - .25f, size.Z * .5f - .12f), _darkWood);
    }

    private void BuildCellarShelf(string name, Vector3 at)
    {
        var shelf = Body(_world, name, at);
        foreach (var x in new[] { -1.9f, 1.9f })
        foreach (var z in new[] { -6f, 0f, 6f })
            Solid(shelf, $"Upright{x}_{z}", new Vector3(.22f, 3.4f, .22f), new Vector3(x, 1.7f, z), _darkWood);
        for (var level = 0; level < 4; level++)
        {
            var y = .42f + level * .88f;
            Solid(shelf, $"Shelf{level}", new Vector3(4.05f, .12f, 12.2f), new Vector3(0, y, 0), _wood);
            foreach (var z in new[] { -5.9f, 0f, 5.9f })
                Solid(shelf, $"CrossSupport{level}_{z}", new Vector3(4f, .14f, .16f), new Vector3(0, y - .13f, z), _darkWood);
        }
        Solid(shelf, "BackRailLow", new Vector3(.12f, .22f, 12f), new Vector3(at.X < 0 ? -1.9f : 1.9f, 1f, 0), _darkWood);
        Solid(shelf, "BackRailHigh", new Vector3(.12f, .22f, 12f), new Vector3(at.X < 0 ? -1.9f : 1.9f, 2.65f, 0), _darkWood);
        // Household groupings follow the bays and leave the box's target support visibly empty.
        foreach (var z in new[] { -4.6f, 3.6f, 5.1f })
        {
            var bundle = Body(shelf, $"LinenBundle{z}", new Vector3(0f, 1.38f, z));
            Solid(bundle, "FoldedLinen", new Vector3(1.4f, .26f, .9f), new Vector3(0, .13f, 0),
                RuralPropMaterials.Surface("cloth", "8e9580"));
        }
    }

    private void BuildCellarBox()
    {
        var bench = Body(_world, "FamilyBoxBench", new Vector3(-14.3f, 0f, 20f));
        BuildTableMembers(bench, new Vector3(1.25f, .66f, 1.15f));
        _boxStart = Marker(_world, "FamilyBoxStartPose", new Vector3(-14.02f, .66f, 20f));
        _boxRest = Marker(_world, "FamilyBoxReturnedPose", new Vector3(-14.70f, 1.405f, 22f));
        var rest = Body(_world, "EmptyFamilyBoxSupport", new Vector3(-14.70f, 1.405f, 22f));
        Solid(rest, "SupportPad", new Vector3(1f, .045f, .85f), new Vector3(0, -.0225f, 0), _darkWood);
        _familyBox = CarryableProp.Create("carry-w05-family-box", "Семейная коробка", CarryableProp.ItemClass.Light,
            _boxStart.Position, 0f, "d3ad77", "wood", CarryableProp.ItemKind.Crate, new Vector3(.2f, .1f, .14f));
        // Keep the canonical custody/input owner and replace only this box's generated presentation.
        foreach (var child in _familyBox.GetChildren())
        {
            _familyBox.RemoveChild(child);
            child.Free();
        }
        var body = _familyBox;
        Solid(body, "BoxBottom", new Vector3(.20f, .012f, .14f), new Vector3(0, .006f, 0), _wood, .003f);
        foreach (var x in new[] { -.094f, .094f })
            Solid(body, $"Side{x}", new Vector3(.012f, .075f, .14f), new Vector3(x, .0495f, 0), _wood, .003f);
        foreach (var z in new[] { -.064f, .064f })
            Solid(body, $"End{z}", new Vector3(.176f, .075f, .012f), new Vector3(0, .0495f, z), _wood, .003f);
        Solid(body, "Lid", new Vector3(.198f, .012f, .138f), new Vector3(0, .094f, 0), _darkWood, .003f);
        Solid(body, "WornCorner", new Vector3(.027f, .008f, .026f), new Vector3(.079f, .096f, -.054f),
            RuralPropMaterials.Surface("wood", "dbbd90"), .004f);
        _carryCoordinator = CarryCoordinator.Create([_familyBox]);
        TryAttachCellarCarry();
    }

    private void BuildCellarResponse()
    {
        var table = Body(_world, "B04NeighbouringObjectTable", new Vector3(2.55f, .2f, 40.25f));
        BuildTableMembers(table, new Vector3(1.05f, .66f, 1.15f));
        _responseObject = Body(table, "NeighbouringWoodenCup", new Vector3(-.25f, .66f, -.15f));
        var cup = (StaticBody3D)_responseObject;
        var clay = RuralPropMaterials.Surface("wood", "8d9472");
        // Hollow cup assembled from real wall members; no filled proxy cylinder.
        Solid(cup, "CupBase", new Vector3(.17f, .018f, .17f), new Vector3(0, .009f, 0), clay, .004f);
        foreach (var x in new[] { -.077f, .077f })
            Solid(cup, $"CupSide{x}", new Vector3(.016f, .16f, .17f), new Vector3(x, .098f, 0), clay, .005f);
        foreach (var z in new[] { -.077f, .077f })
            Solid(cup, $"CupEnd{z}", new Vector3(.138f, .16f, .016f), new Vector3(0, .098f, z), clay, .005f);
        _responseRest = Marker(table, "ResponseCupPlacedPose", new Vector3(.24f, .66f, .15f));
        _bichuraMount = new Node3D { Name = "BichuraMount", Position = new Vector3(3.6f, .2f, 42.5f) };
        _bichuraMount.SetMeta("assetState", "missing original imported A-BICHURA; no substitute creature");
        _world.AddChild(_bichuraMount);
    }

    private void BuildCellarNiche()
    {
        var at = new Vector3(17.4f, .2f, 58f);
        var shelf = Body(_world, "B06NicheShelf", at);
        foreach (var x in new[] { -1.25f, 1.25f })
            Solid(shelf, $"NicheSide{x}", new Vector3(.18f, 2.4f, 1.55f), new Vector3(x, 1.2f, 0), _darkWood);
        foreach (var y in new[] { .06f, .88f, 1.65f, 2.4f })
            Solid(shelf, $"NicheBoard{y}", new Vector3(2.65f, .12f, 1.55f), new Vector3(0, y, 0), _wood);
        Solid(shelf, "NicheBack", new Vector3(2.6f, 2.45f, .12f), new Vector3(0, 1.22f, .72f), _darkWood);
        var lining = Body(shelf, "NicheWornLining", Vector3.Zero);
        Solid(lining, "WornBase", new Vector3(1.1f, .015f, .76f), new Vector3(-.45f, .128f, -.30f),
            RuralPropMaterials.Surface("wood", "c4ad7e"), .003f);
        _niche = Marker(shelf, "ActualNicheWitness", new Vector3(-.65f, .42f, -.58f));
        _niche.SetMeta("meaning", "ordinary empty storage niche; the real family spool remains outside the photo world");
        var linen = Body(shelf, "TopShelfLinen", new Vector3(.15f, 1.72f, 0));
        Solid(linen, "FoldedCover", new Vector3(1.05f, .22f, .8f), new Vector3(0, .11f, 0),
            RuralPropMaterials.Surface("cloth", "849187"));
    }

    private void BuildCellarLightWell()
    {
        var frame = Body(_world, "B05DaylightBoards", Vector3.Zero);
        foreach (var x in new[] { -16.3f, -14f })
            Solid(frame, $"LightOpeningEdge{x}", new Vector3(.18f, .4f, 9.2f), new Vector3(x, 6.73f, 52.5f), _darkWood);
        // A low bench stands beside, never across, the B04-B05-B07 walking line.
        var bench = Body(_world, "B05RestBench", new Vector3(-18f, .2f, 53f));
        BuildTableMembers(bench, new Vector3(1.2f, .52f, 3.1f));
    }

    private void BuildCellarThreshold()
    {
        var frame = Body(_world, "B07HomeThreshold", new Vector3(0f, 0, 77.7f));
        foreach (var x in new[] { -2.2f, 2.2f })
            Solid(frame, $"ThresholdPost{x}", new Vector3(.35f, 3.6f, .42f), new Vector3(x, 1.8f, 0), _darkWood);
        Solid(frame, "ThresholdLintel", new Vector3(4.8f, .36f, .44f), new Vector3(0, 3.6f, 0), _darkWood);
        Solid(frame, "ThresholdSill", new Vector3(4.8f, .035f, .44f), new Vector3(0, .0175f, 0), _wood);
        Marker(frame, "ReturnWitness", new Vector3(-1.9f, 1.2f, -.25f));
    }

    private void BuildCellarLights()
    {
        foreach (var at in new[] { new Vector3(0, 4.7f, 11), new Vector3(-12, 4.7f, 24),
                     new Vector3(12, 4.7f, 24), new Vector3(0, 4.7f, 40), new Vector3(15, 4.7f, 56),
                     new Vector3(0, 4.7f, 76) })
        {
            var light = new OmniLight3D
            {
                Name = $"CellarBounce{_lights.Count}", Position = at,
                LightColor = Color.FromHtml("ffe0a6"), LightEnergy = 1.1f,
                OmniRange = 21f, OmniAttenuation = .7f, ShadowEnabled = false
            };
            _world.AddChild(light);
            _lights.Add(light);
        }
        var slit = new SpotLight3D
        {
            Name = "B05DaylightThroughActualSlit", Position = new Vector3(-15.1f, 6.6f, 53f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            LightColor = Color.FromHtml("d7e7e1"), LightEnergy = 3.2f,
            SpotRange = 10f, SpotAngle = 36f, ShadowEnabled = true
        };
        _world.AddChild(slit);
        _lights.Add(slit);
    }

    private void BuildCellarRouteMarkers()
    {
        (string From, string To)[] links = [("B00", "B01"), ("B01", "B02"), ("B01", "B03"),
            ("B02", "B04"), ("B03", "B04"), ("B04", "B05"), ("B04", "B06"), ("B05", "B07"), ("B06", "B07")];
        var routes = new Node3D { Name = "PassportWalkingLinks" };
        _world.AddChild(routes);
        foreach (var (from, to) in links)
        {
            var route = Marker(routes, from + "_" + to, Vector3.Zero);
            route.SetMeta("bidirectional", true);
            route.SetMeta("fromAnchor", $"Anchors/{from}");
            route.SetMeta("toAnchor", $"Anchors/{to}");
            route.SetMeta("minimumRouteWidthMetres", 1.5f);
            route.SetMeta("contactPolicy", "continuous visible supported board floor; no portal or invisible traversal floor");
        }
    }

    private static StaticBody3D Body(Node3D parent, string name, Vector3 at)
    {
        var body = new StaticBody3D { Name = name, Position = at, CollisionLayer = 1u, CollisionMask = 1u };
        body.SetMeta("footstepSurface", "wood");
        parent.AddChild(body);
        return body;
    }

    private static Marker3D Marker(Node3D parent, string name, Vector3 at)
    {
        var marker = new Marker3D { Name = name, Position = at };
        parent.AddChild(marker);
        return marker;
    }

    private static void Solid(StaticBody3D parent, string name, Vector3 size, Vector3 at, Material material, float bevel = .012f)
    {
        var mesh = RuralPropGeometry.Block(parent, name, size, at, material, bevel);
        parent.AddChild(new CollisionShape3D { Name = name + "Contact", Position = at,
            Shape = mesh.Mesh!.CreateConvexShape() });
    }
}
