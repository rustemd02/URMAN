using Godot;

namespace Urman.Godot;

/// <summary>
/// The first part of the Niva ride (author feedback 2026-09-29): the winter
/// road from the district bus stop to Kara-Urman - open snow fields, birch
/// and pine groves, a power line, the village sign - and then the forest wall
/// the road cuts through before the bridge. This is the "far half shown in
/// transit": a presentation location away from the village terrain, never a
/// free-roam area. It writes no story or save state.
/// </summary>
public partial class PrologueApproachRoad : Node3D
{
    public static readonly Vector3 Origin = new(0f, 0f, 6000f);
    public const float RoadLength = 640f;
    public const float ForestStart = -390f;
    private const float FieldHalfWidth = 170f;

    private readonly RandomNumberGenerator _rng = new() { Seed = 0x55524d32 };

    public Node3D? EdgeFigure { get; private set; }
    public float EdgeFigureZ { get; private set; }

    public static float RoadX(float z) => 13f * Mathf.Sin(z / 95f) + 5f * Mathf.Sin(z / 38f + .7f);

    public static float Ground(float x, float z)
    {
        var rolls = .9f * Mathf.Sin(x / 41f) * Mathf.Cos(z / 57f) + .35f * Mathf.Sin((x + z) / 23f);
        var d = Mathf.Abs(x - RoadX(z));
        return .2f + rolls * Mathf.SmoothStep(4f, 18f, d);
    }

    /// <summary>World position on the road centre at local Z.</summary>
    public Vector3 RoadPoint(float z) => Origin + new Vector3(RoadX(z), Ground(RoadX(z), z) + .06f, z);

    public override void _Ready()
    {
        Name = "PrologueApproachRoad";
        Position = Origin;
        SetMeta("presentationOnly", true);
        SetMeta("presentationOwner", nameof(Act1DemoRoot));
        BuildGround();
        BuildRoad();
        BuildTrees();
        BuildPowerLine();
        BuildBusStop();
        BuildVillageSign();
        BuildEdgeFigure();
    }

    private void BuildGround()
    {
        const float cell = 5f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (var z = 40f; z > -RoadLength - 60f; z -= cell)
        for (var x = -FieldHalfWidth; x < FieldHalfWidth; x += cell)
        {
            Vector3 P(float px, float pz) => new(px, Ground(px, pz), pz);
            var a = P(x, z); var b = P(x + cell, z); var c = P(x, z - cell); var d = P(x + cell, z - cell);
            foreach (var v in new[] { a, c, b, b, c, d }) { st.SetUV(new Vector2(v.X, v.Z) * .08f); st.AddVertex(v); }
        }
        st.GenerateNormals();
        AddChild(new MeshInstance3D { Name = "ApproachFields", Mesh = st.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor("d7dde2", "snow_ground") });
    }

    private void BuildRoad()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const float step = 3f;
        for (var z = 40f; z > -RoadLength - 40f; z -= step)
        {
            Vector3 E(float zz, float side) { var x = RoadX(zz) + side; return new Vector3(x, Ground(x, zz) + .04f, zz); }
            var z2 = z - step;
            var l0 = E(z, -2.8f); var r0 = E(z, 2.8f); var l1 = E(z2, -2.8f); var r1 = E(z2, 2.8f);
            foreach (var v in new[] { l0, l1, r0, r0, l1, r1 }) { st.SetUV(new Vector2(v.X, v.Z) * .3f); st.AddVertex(v); }
            // Plough berms on both shoulders.
            foreach (var side in new[] { -1f, 1f })
            {
                var i0 = E(z, side * 2.9f); var o0 = E(z, side * 4.1f) + Vector3.Up * .35f;
                var i1 = E(z2, side * 2.9f); var o1 = E(z2, side * 4.1f) + Vector3.Up * .35f;
                foreach (var v in side < 0 ? new[] { o0, o1, i0, i0, o1, i1 } : new[] { i0, i1, o0, o0, i1, o1 })
                { st.SetUV(new Vector2(v.X, v.Z) * .3f); st.AddVertex(v); }
            }
        }
        st.GenerateNormals();
        AddChild(new MeshInstance3D { Name = "ApproachRoad", Mesh = st.Commit(), MaterialOverride = PainterlyMaterialLibrary.ForColor("9da3a6", "snow_road") });
    }

    private static (Mesh Mesh, Transform3D Local)? TreeMesh(string scene, string node)
    {
        if (ResourceLoader.Load<PackedScene>(scene) is not { } packed) return null;
        var root = packed.Instantiate<Node3D>();
        try
        {
            if (root.FindChild(node, true, false) is not MeshInstance3D mesh || mesh.Mesh is null) return null;
            var local = Transform3D.Identity;
            for (Node? cursor = mesh; cursor is Node3D spatial && cursor != root; cursor = cursor.GetParent())
                local = spatial.Transform * local;
            return (mesh.Mesh, local);
        }
        finally { root.Free(); }
    }

    // Groves in the fields, then a dense tall forest the road cuts through.
    private void BuildTrees()
    {
        var pine = TreeMesh("res://assets/models/act1/urman_winter_pine.glb", "WinterPine_LOD2");
        var pineNear = TreeMesh("res://assets/models/act1/urman_winter_pine.glb", "WinterPine_LOD1");
        var dead = TreeMesh("res://assets/models/act1/urman_winter_dead_tree.glb", "WinterDeadTree_LOD2");
        if (pine is null || pineNear is null || dead is null) return;
        var far = new List<Transform3D>(); var near = new List<Transform3D>(); var bare = new List<Transform3D>();
        void Tree(float x, float z, float height, bool leafless)
        {
            var girth = height * (leafless ? _rng.RandfRange(.6f, .8f) : _rng.RandfRange(.42f, .58f));
            var t = new Transform3D(new Basis(Vector3.Up, _rng.RandfRange(0, Mathf.Tau)) * Basis.FromScale(new Vector3(girth, height, girth)),
                new Vector3(x, Ground(x, z) - .1f, z));
            if (leafless) bare.Add(t);
            else if (Mathf.Abs(x - RoadX(z)) < 22f) near.Add(t);
            else far.Add(t);
        }
        // Field groves: birch-like leafless clumps and small pine islands.
        for (var grove = 0; grove < 26; grove++)
        {
            var gz = _rng.RandfRange(-RoadLength * .58f, 20f);
            var side = _rng.Randf() < .5f ? -1f : 1f;
            var gx = RoadX(gz) + side * _rng.RandfRange(28f, 150f);
            var leafless = _rng.Randf() < .55f;
            for (var index = 0; index < 5 + _rng.RandiRange(0, 9); index++)
                Tree(gx + _rng.RandfRange(-9f, 9f), gz + _rng.RandfRange(-9f, 9f), leafless ? _rng.RandfRange(9f, 15f) : _rng.RandfRange(12f, 22f), leafless);
        }
        // The forest wall: tall and close to both shoulders.
        for (var z = ForestStart + 25f; z > -RoadLength - 40f; z -= 4.2f)
        for (var offset = 7f; offset < 110f; offset += 4.2f)
        foreach (var side in new[] { -1f, 1f })
        {
            if (_rng.Randf() < .12f) continue;
            var x = RoadX(z) + side * (offset + _rng.RandfRange(-1.4f, 1.4f));
            var edge = Mathf.SmoothStep(ForestStart + 25f, ForestStart - 15f, z);
            if (_rng.Randf() > edge) continue;
            Tree(x, z + _rng.RandfRange(-1.5f, 1.5f), _rng.RandfRange(24f, 36f), _rng.Randf() < .1f);
        }
        Add("GroveAndForestFar", pine.Value, far);
        Add("ForestNear", pineNear.Value, near);
        Add("LeaflessGroves", dead.Value, bare);
    }

    private void Add(string name, (Mesh Mesh, Transform3D Local) source, List<Transform3D> transforms)
    {
        if (transforms.Count == 0) return;
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = source.Mesh, InstanceCount = transforms.Count };
        for (var index = 0; index < transforms.Count; index++) multi.SetInstanceTransform(index, transforms[index] * source.Local);
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multi });
    }

    private void BuildPowerLine()
    {
        var wood = PainterlyMaterialLibrary.ForColor("5a4a3a", "wood");
        var wire = new StandardMaterial3D { AlbedoColor = new Color(.1f, .1f, .1f), Roughness = 1f };
        Vector3? previous = null;
        for (var z = 30f; z > ForestStart + 20f; z -= 46f)
        {
            var x = RoadX(z) + 11f;
            var foot = new Vector3(x, Ground(x, z), z);
            AddChild(new MeshInstance3D { Name = $"Pole{(int)-z}", Position = foot + Vector3.Up * 4.2f, RotationDegrees = new Vector3(_rng.RandfRange(-2, 2), 0, _rng.RandfRange(-3, 3)), Mesh = new CylinderMesh { TopRadius = .1f, BottomRadius = .14f, Height = 8.4f, RadialSegments = 7 }, MaterialOverride = wood });
            AddChild(new MeshInstance3D { Position = foot + Vector3.Up * 7.9f, Mesh = new BoxMesh { Size = new Vector3(1.6f, .1f, .1f) }, MaterialOverride = wood });
            var top = foot + Vector3.Up * 7.95f;
            if (previous is { } last)
            {
                foreach (var side in new[] { -.7f, .7f })
                {
                    var a = last + Vector3.Right * side; var b = top + Vector3.Right * side;
                    var mid = (a + b) * .5f + Vector3.Down * .6f;
                    Span(a, mid, wire); Span(mid, b, wire);
                }
            }
            previous = top;
        }
    }

    private void Span(Vector3 a, Vector3 b, Material material)
    {
        var length = a.DistanceTo(b);
        var segment = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .012f, BottomRadius = .012f, Height = length, RadialSegments = 4 }, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(segment);
        var up = (b - a).Normalized();
        var side = up.Cross(Vector3.Forward).LengthSquared() > .01f ? up.Cross(Vector3.Forward).Normalized() : Vector3.Right;
        segment.Transform = new Transform3D(new Basis(side, up, side.Cross(up)), (a + b) * .5f);
    }

    // The district bus stop where babai picked Aidar up (the bus is backstory).
    private void BuildBusStop()
    {
        const float z = 26f;
        var x = RoadX(z) - 6.2f;
        var stop = new Node3D { Name = "DistrictBusStop", Position = new Vector3(x, Ground(x, z), z), RotationDegrees = new Vector3(0, 90, 0) };
        AddChild(stop);
        var metal = PainterlyMaterialLibrary.ForColor("5d6f7c", "wood_painted_blue");
        stop.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.2f, -.9f), Mesh = new BoxMesh { Size = new Vector3(3.4f, 2.4f, .08f) }, MaterialOverride = metal });
        stop.AddChild(new MeshInstance3D { Position = new Vector3(0, 2.45f, -.35f), Mesh = new BoxMesh { Size = new Vector3(3.6f, .1f, 1.3f) }, MaterialOverride = metal });
        stop.AddChild(new MeshInstance3D { Position = new Vector3(0, 2.52f, -.35f), Mesh = new BoxMesh { Size = new Vector3(3.6f, .1f, 1.3f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("e8ecef", "snow_roof") });
        foreach (var side in new[] { -1.7f, 1.7f })
            stop.AddChild(new MeshInstance3D { Position = new Vector3(side, 1.2f, -.35f), Mesh = new BoxMesh { Size = new Vector3(.08f, 2.4f, 1.2f) }, MaterialOverride = metal });
        stop.AddChild(new MeshInstance3D { Position = new Vector3(0, .45f, -.6f), Mesh = new BoxMesh { Size = new Vector3(2.8f, .08f, .4f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("6b5846", "wood") });
        stop.AddChild(new Label3D { Text = "Кара-Урман борылышы\nПоворот на Кара-Урман", Position = new Vector3(0, 2.15f, -.84f), FontSize = 42, PixelSize = .006f, Modulate = new Color(.95f, .95f, .9f), OutlineSize = 6 });
    }

    private void BuildVillageSign()
    {
        const float z = -205f;
        var x = RoadX(z) + 4.6f;
        var sign = new Node3D { Name = "VillageSign", Position = new Vector3(x, Ground(x, z), z) };
        AddChild(sign);
        var post = PainterlyMaterialLibrary.ForColor("7c858c", "wood");
        foreach (var side in new[] { -.7f, .7f })
            sign.AddChild(new MeshInstance3D { Position = new Vector3(side, 1.2f, 0), Mesh = new CylinderMesh { TopRadius = .04f, BottomRadius = .04f, Height = 2.4f }, MaterialOverride = post });
        sign.AddChild(new MeshInstance3D { Position = new Vector3(0, 2.1f, 0), Mesh = new BoxMesh { Size = new Vector3(1.9f, .8f, .05f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("f1f0ea", "plastic_abs") });
        sign.AddChild(new Label3D { Text = "КАРА УРМАН\nКАРА-УРМАН", Position = new Vector3(0, 2.1f, .03f), FontSize = 64, PixelSize = .005f, Modulate = new Color(.08f, .09f, .1f), OutlineSize = 0 });
        sign.AddChild(new MeshInstance3D { Position = new Vector3(0, 2.1f, -.03f), Mesh = new BoxMesh { Size = new Vector3(1.94f, .84f, .02f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("30343a", "plastic_abs") });
    }

    // Someone stands in the snow where the forest begins, too far to make out.
    private void BuildEdgeFigure()
    {
        EdgeFigureZ = ForestStart - 30f;
        var x = RoadX(EdgeFigureZ) - 26f;
        EdgeFigure = new Node3D { Name = "ForestEdgeFigure", Position = new Vector3(x, Ground(x, EdgeFigureZ), EdgeFigureZ) };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(.03f, .03f, .035f), Roughness = 1f };
        EdgeFigure.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.6f, 0), Scale = new Vector3(.6f, 1f, .5f), Mesh = new CapsuleMesh { Radius = .28f, Height = 3.2f }, MaterialOverride = dark });
        EdgeFigure.AddChild(new MeshInstance3D { Position = new Vector3(0, 3.35f, 0), Mesh = new SphereMesh { Radius = .17f, Height = .36f }, MaterialOverride = dark });
        AddChild(EdgeFigure);
    }
}
