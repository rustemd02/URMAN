using Godot;

namespace Urman.Godot;

/// <summary>
/// The flash-forward location of the Act I prologue (author feedback
/// 2026-09-29): a deep night forest far past the Kara-Urman edge - an old
/// logging track between very tall pines and dead standing trees, not the
/// village's own forest border. It is a separate presentation location built
/// away from the village terrain; it owns its ground, its walkable window and
/// its staged events, and writes no story, knowledge or save state.
/// </summary>
public partial class PrologueDeepForest : Node3D
{
    // Far from the village terrain window (-64..94, -152..104) so no village
    // geometry is inside the fog range.
    public static readonly Vector3 Origin = new(0f, 0f, 2600f);
    public const float TrackLength = 230f;
    public const float ClearingZ = -216f;
    public const float ClearingRadius = 11f;
    private const float HalfWidth = 40f;
    private const float WalkableHalfWidth = 10.5f;

    private readonly RandomNumberGenerator _rng = new() { Seed = 0x55524d31 };
    private readonly List<Vector3> _colliderTrees = new();
    // Live trunks further in, where eyes can watch from.
    private readonly List<Vector3> _deepTrunks = new();

    public Node3D? Silhouette { get; private set; }
    public Node3D? Mitten { get; private set; }
    public IReadOnlyList<Node3D> EyeGlints => _eyeGlints;
    private readonly List<Node3D> _eyeGlints = new();

    /// <summary>Local X of the track centre at local Z (the track runs toward -Z).</summary>
    public static float TrackX(float z) => 5.5f * Mathf.Sin(z / 23f) + 2.6f * Mathf.Sin(z / 9.7f + 1.3f);

    /// <summary>Local ground height: a trodden track between rising snow banks.</summary>
    public static float Ground(float x, float z)
    {
        var baseline = 0.45f * Mathf.Sin(z / 19f) + 0.2f * Mathf.Sin(z / 7.3f);
        var d = Mathf.Abs(x - TrackX(z));
        var clearing = new Vector2(x - TrackX(ClearingZ), z - ClearingZ).Length();
        var bankWeight = Mathf.SmoothStep(1.6f, 9f, d) * Mathf.SmoothStep(ClearingRadius * .55f, ClearingRadius, clearing);
        var bumps = 0.85f * Mathf.Sin(x / 10.5f) * Mathf.Cos(z / 12.5f) + 0.38f * Mathf.Sin((x - z) / 5.9f);
        return baseline + bankWeight * (bumps + 0.09f * Mathf.Max(0f, d - 2f));
    }

    public float Progress(Vector3 globalPosition) => -(globalPosition.Z - Origin.Z);

    public Vector3 ToWorld(float x, float z, float lift = 0f) => Origin + new Vector3(x, Ground(x, z) + lift, z);

    public Vector3 StartPosition => ToWorld(TrackX(2f), 2f, .05f);

    /// <summary>Keeps the walker inside the corridor of the dark track.</summary>
    public static (Vector3 Position, float Ground) Guard(Vector3 world)
    {
        var local = world - Origin;
        var z = Mathf.Clamp(local.Z, -TrackLength - 12f, 8f);
        var cx = TrackX(z);
        var limit = new Vector2(local.X - TrackX(ClearingZ), z - ClearingZ).Length() < ClearingRadius + 2f
            ? ClearingRadius + 1.5f : WalkableHalfWidth;
        var x = Mathf.Clamp(local.X, cx - limit, cx + limit);
        return (Origin + new Vector3(x, local.Y, z), Origin.Y + Ground(x, z));
    }

    public override void _Ready()
    {
        Name = "PrologueDeepForest";
        Position = Origin;
        SetMeta("presentationOnly", true);
        SetMeta("presentationOwner", nameof(Act1DemoRoot));
        BuildGround();
        BuildTrees();
        BuildTreeColliders();
        BuildFootprints();
        BuildWatchingHollows();
        BuildMitten();
        BuildSilhouette();
        BuildEyeGlints();
        BuildBoundaryPost();
        BuildFallenSpruce();
        BuildHuntersHut();
        BuildBushes();
        BuildSnowfall();
    }

    private void BuildGround()
    {
        const float cell = 1.6f;
        var nx = (int)(HalfWidth * 2 / cell);
        var nz = (int)((TrackLength + 40f) / cell);
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 P(int i, int k)
        {
            var x = -HalfWidth + i * cell;
            var z = 20f - k * cell;
            return new Vector3(x, Ground(x, z), z);
        }
        var faces = new List<Vector3>();
        for (var k = 0; k < nz; k++)
        for (var i = 0; i < nx; i++)
        {
            var a = P(i, k); var b = P(i + 1, k); var c = P(i, k + 1); var d = P(i + 1, k + 1);
            foreach (var v in new[] { a, c, b, b, c, d })
            {
                st.SetUV(new Vector2(v.X, v.Z) * .25f);
                st.AddVertex(v);
                faces.Add(v);
            }
        }
        st.GenerateNormals();
        var ground = new MeshInstance3D
        {
            Name = "DeepForestSnow",
            Mesh = st.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("c9d0d6", "snow_ground")
        };
        AddChild(ground);
        var body = new StaticBody3D { Name = "DeepForestGroundBody" };
        body.SetMeta("footstepSurface", "snow_soft");
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = faces.ToArray() } });
        AddChild(body);

        // The trodden track itself: a narrow darker packed strip.
        var track = new SurfaceTool();
        track.Begin(Mesh.PrimitiveType.Triangles);
        for (var z = 16f; z > -TrackLength - 6f; z -= 1.2f)
        {
            var z2 = z - 1.2f;
            Vector3 E(float zz, float side) { var x = TrackX(zz) + side; return new Vector3(x, Ground(x, zz) + .025f, zz); }
            var l0 = E(z, -.85f); var r0 = E(z, .85f); var l1 = E(z2, -.85f); var r1 = E(z2, .85f);
            foreach (var v in new[] { l0, l1, r0, r0, l1, r1 }) { track.SetUV(new Vector2(v.X, v.Z) * .5f); track.AddVertex(v); }
        }
        track.GenerateNormals();
        AddChild(new MeshInstance3D
        {
            Name = "DeepForestTrack",
            Mesh = track.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor("aab3bb", "snow_trampled")
        });
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

    // Very tall, dense trees: two MultiMesh families per chunk (near the track
    // gets the finer LOD), so hundreds of 25-38 m trunks stay cheap to draw.
    private void BuildTrees()
    {
        const string pineScene = "res://assets/models/act1/urman_winter_pine.glb";
        const string deadScene = "res://assets/models/act1/urman_winter_dead_tree.glb";
        var pineNear = TreeMesh(pineScene, "WinterPine_LOD1");
        var pineFar = TreeMesh(pineScene, "WinterPine_LOD2");
        var deadNear = TreeMesh(deadScene, "WinterDeadTree_LOD1");
        var deadFar = TreeMesh(deadScene, "WinterDeadTree_LOD2");
        if (pineNear is null || pineFar is null || deadNear is null || deadFar is null)
        {
            GD.PushError("prologue-deep-forest: tree meshes are unavailable.");
            return;
        }

        const float chunk = 38f;
        for (var chunkStart = 18f; chunkStart > -TrackLength - 30f; chunkStart -= chunk)
        {
            var near = new List<Transform3D>(); var far = new List<Transform3D>();
            var deadN = new List<Transform3D>(); var deadF = new List<Transform3D>();
            for (var z = chunkStart; z > chunkStart - chunk; z -= 3.6f)
            for (var x = -HalfWidth; x <= HalfWidth; x += 3.6f)
            {
                var px = x + _rng.RandfRange(-1.5f, 1.5f);
                var pz = z + _rng.RandfRange(-1.5f, 1.5f);
                var d = Mathf.Abs(px - TrackX(pz));
                var clearing = new Vector2(px - TrackX(ClearingZ), pz - ClearingZ).Length();
                if (d < 2.4f + _rng.Randf() * 1.4f || clearing < ClearingRadius + _rng.Randf() * 2f) continue;
                // Keep the way round the fallen spruce's root plate open.
                var offset = px - TrackX(pz);
                if (Mathf.Abs(pz - FallenSpruceZ) < 7f && offset > 3f && offset < 11f) continue;
                // And the approach to the hunter's hut visible from the track.
                if (Mathf.Abs(pz - HutZ) < 6f && offset < -2f && offset > -17f) continue;
                // Thin the second row a little so the track reads as a way in.
                if (d < 6f && _rng.Randf() < .35f) continue;
                var dead = _rng.Randf() < .16f;
                var height = dead ? _rng.RandfRange(11f, 19f) : _rng.RandfRange(24f, 38f);
                var girth = dead ? height * _rng.RandfRange(.55f, .75f) : height * _rng.RandfRange(.42f, .6f);
                var basis = new Basis(Vector3.Up, _rng.RandfRange(0, Mathf.Tau))
                    * new Basis(new Vector3(1, 0, 0), _rng.RandfRange(-.04f, .04f))
                    * Basis.FromScale(new Vector3(girth, height, girth));
                var transform = new Transform3D(basis, new Vector3(px, Ground(px, pz) - .15f, pz));
                var isNear = d < 16f;
                (dead ? (isNear ? deadN : deadF) : (isNear ? near : far)).Add(transform);
                if (d < 7.5f && d >= 2.4f) _colliderTrees.Add(new Vector3(px, Ground(px, pz), pz));
                else if (!dead && d is > 8f and < 28f) _deepTrunks.Add(new Vector3(px, Ground(px, pz), pz));
            }
            AddFamily($"Pines{chunkStart:0}", pineNear.Value, near);
            AddFamily($"PinesFar{chunkStart:0}", pineFar.Value, far);
            AddFamily($"Dead{chunkStart:0}", deadNear.Value, deadN);
            AddFamily($"DeadFar{chunkStart:0}", deadFar.Value, deadF);
        }
    }

    private void AddFamily(string name, (Mesh Mesh, Transform3D Local) source, List<Transform3D> transforms)
    {
        if (transforms.Count == 0) return;
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = source.Mesh, InstanceCount = transforms.Count };
        for (var index = 0; index < transforms.Count; index++) multi.SetInstanceTransform(index, transforms[index] * source.Local);
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multi });
    }

    // The nearest trunks are solid; the corridor is closed by the thicket, not
    // by an invisible wall line.
    private void BuildTreeColliders()
    {
        var body = new StaticBody3D { Name = "DeepForestTrunks" };
        foreach (var tree in _colliderTrees)
            body.AddChild(new CollisionShape3D { Position = tree + Vector3.Up * 1.5f, Shape = new CylinderShape3D { Radius = .38f, Height = 3f } });
        AddChild(body);
    }

    // Fresh prints of one person lead along the track and stop in the middle
    // of the clearing.
    private void BuildFootprints()
    {
        // Deep-snow boot prints (author feedback 2026-09-29: the flat grey boxes read as
        // dots). Each print is a trampled rim of loose snow, a shadowed sole hollow and a
        // deeper heel, with an uneven stride and a slight toe-out.
        var soles = new List<Transform3D>();
        var heels = new List<Transform3D>();
        var rims = new List<Transform3D>();
        var left = true;
        for (var z = -8f; z > ClearingZ; z -= _rng.RandfRange(.68f, .82f))
        {
            var x = TrackX(z) + (left ? -.13f : .13f) + _rng.RandfRange(-.04f, .04f);
            var yaw = Mathf.Atan2(TrackX(z - .5f) - TrackX(z + .5f), -1f) + (left ? .12f : -.12f) + _rng.RandfRange(-.06f, .06f);
            var basis = new Basis(Vector3.Up, yaw);
            var ground = Ground(x, z);
            var at = new Vector3(x, ground, z);
            rims.Add(new Transform3D(basis.Scaled(new Vector3(1f, 1f, 2.1f)), at + Vector3.Up * .006f));
            soles.Add(new Transform3D(basis.Scaled(new Vector3(.95f, 1f, 1.65f)), at + basis * new Vector3(0, .011f, -.03f)));
            heels.Add(new Transform3D(basis, at + basis * new Vector3(0, .014f, .1f)));
            left = !left;
        }
        AddPrintLayer("DeepForestPrintRims", rims, new CylinderMesh { TopRadius = .085f, BottomRadius = .1f, Height = .012f, RadialSegments = 14 }, "e9eef3", "snow_trampled");
        AddPrintLayer("DeepForestPrintSoles", soles, new CylinderMesh { TopRadius = .06f, BottomRadius = .065f, Height = .01f, RadialSegments = 14 }, "8795a8", "snow_trampled");
        AddPrintLayer("DeepForestPrintHeels", heels, new CylinderMesh { TopRadius = .045f, BottomRadius = .05f, Height = .01f, RadialSegments = 12 }, "6e7c90", "snow_trampled");
    }

    private void AddPrintLayer(string name, List<Transform3D> transforms, Mesh mesh, string color, string surface)
    {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (var index = 0; index < transforms.Count; index++) multi.SetInstanceTransform(index, transforms[index]);
        AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    // A01.1 on the trunks: uneven hollows and knots facing the track. Never a
    // pair of identical stamps, never an eyeball.
    private void BuildWatchingHollows()
    {
        var material = PainterlyMaterialLibrary.ForColor("120e0b", "wood_bark");
        var index = 0;
        foreach (var tree in _colliderTrees)
        {
            if (_rng.Randf() > .22f) continue;
            var toTrack = new Vector3(TrackX(tree.Z) - tree.X, 0, 0).Normalized();
            if (toTrack == Vector3.Zero) toTrack = Vector3.Right;
            var height = _rng.RandfRange(1.4f, 2.6f);
            var pair = _rng.Randf() < .6f;
            var spread = _rng.RandfRange(.09f, .17f);
            for (var eye = 0; eye < (pair ? 2 : 1); eye++)
            {
                var side = pair ? (eye == 0 ? -spread : spread) : 0f;
                var along = new Vector3(0, 0, 1) * side;
                var size = _rng.RandfRange(.035f, .075f) * (eye == 1 ? _rng.RandfRange(.7f, 1.1f) : 1f);
                AddChild(new MeshInstance3D
                {
                    Name = $"TrunkHollow{index}_{eye}",
                    Position = tree + toTrack * .3f + along + Vector3.Up * (height + (eye == 1 ? _rng.RandfRange(-.05f, .05f) : 0f)),
                    Scale = new Vector3(.35f, 1f, _rng.RandfRange(.8f, 1.35f)),
                    Mesh = new SphereMesh { Radius = size, Height = size * 2, RadialSegments = 8, Rings = 4 },
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                });
            }
            index++;
        }
    }

    // A small blue child's mitten on a broken branch far above the reach of a
    // grown man. The motif is left unexplained.
    private void BuildMitten()
    {
        const float z = -58f;
        var x = TrackX(z) + 2.1f;
        var root = new Node3D { Name = "HighBranchMitten", Position = new Vector3(x, Ground(x, z), z) };
        AddChild(root);
        var trunk = new MeshInstance3D
        {
            Position = new Vector3(.9f, 6f, 0),
            Mesh = new CylinderMesh { TopRadius = .16f, BottomRadius = .3f, Height = 12f, RadialSegments = 10 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("3b3129", "bark_pine")
        };
        root.AddChild(trunk);
        root.AddChild(new MeshInstance3D
        {
            Position = new Vector3(.45f, 3.35f, 0), RotationDegrees = new Vector3(0, 0, 68),
            Mesh = new CylinderMesh { TopRadius = .025f, BottomRadius = .06f, Height = 1.1f, RadialSegments = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("3b3129", "wood_bark")
        });
        Mitten = new Node3D { Name = "Mitten", Position = new Vector3(.08f, 3.18f, 0), RotationDegrees = new Vector3(0, 0, -8) };
        root.AddChild(Mitten);
        var wool = PainterlyMaterialLibrary.ForColor("2f4f8a", "fabric");
        Mitten.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .065f, Height = .24f }, MaterialOverride = wool });
        Mitten.AddChild(new MeshInstance3D { Position = new Vector3(.06f, .02f, 0), RotationDegrees = new Vector3(0, 0, -35), Mesh = new CapsuleMesh { Radius = .028f, Height = .1f }, MaterialOverride = wool });
        Mitten.AddChild(new MeshInstance3D { Position = new Vector3(0, .12f, 0), Mesh = new CylinderMesh { TopRadius = .068f, BottomRadius = .068f, Height = .05f }, MaterialOverride = PainterlyMaterialLibrary.ForColor("d8d2c4", "fabric") });
    }

    // Something tall and thin between the trunks - a figure only in outline,
    // never lit, never close. It is shown for a moment and then it is not there.
    private void BuildSilhouette()
    {
        const float z = -113f;
        var x = TrackX(z) + 15.5f;
        Silhouette = new ProloguePresence { Name = "TallFigure", Position = new Vector3(x, Ground(x, z) + 1.4f, z), Visible = false };
        AddChild(Silhouette);
    }

    // Pairs of faint glints deep in the dark: animal eyes, or not.
    private void BuildEyeGlints()
    {
        // Yellow eyes with a slit pupil, set on real trunks deep off the track, facing
        // the walker. Author feedback 2026-09-29: the old ones were two loose dots in
        // the air. Whose they are stays unclear: bird, animal, something else.
        var iris = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(.98f, .78f, .16f),
            EmissionEnabled = true,
            Emission = new Color(.95f, .62f, .08f),
            EmissionEnergyMultiplier = 1.6f
        };
        var pupil = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(.02f, .015f, .01f) };
        var wanted = new[] { -150f, -158f, -166f, -171f, -177f, -184f };
        var used = new HashSet<Vector3>();
        foreach (var targetZ in wanted)
        {
            var tree = _deepTrunks
                .Where(candidate => !used.Contains(candidate) && Mathf.Abs(candidate.Z - targetZ) < 5f
                    && Mathf.Abs(candidate.X - TrackX(candidate.Z)) is > 9f and < 26f)
                .OrderBy(candidate => Mathf.Abs(candidate.Z - targetZ)).FirstOrDefault();
            if (tree == default) continue;
            used.Add(tree);
            var toTrack = new Vector3(TrackX(tree.Z) - tree.X, 0, 0).Normalized();
            var height = _rng.RandfRange(1.3f, 3.4f);
            // Peering round the trunk: on its edge, not in the middle of the bark.
            var edge = new Vector3(0, 0, _rng.Randf() < .5f ? -.3f : .3f);
            var pair = new Node3D
            {
                Name = $"Glint{_eyeGlints.Count}",
                Transform = new Transform3D(Basis.LookingAt(toTrack, Vector3.Up, true), tree + toTrack * .34f + edge + Vector3.Up * height),
                Visible = false
            };
            var spacing = _rng.RandfRange(.07f, .11f);
            foreach (var side in new[] { -spacing * .5f, spacing * .5f })
            {
                pair.AddChild(new MeshInstance3D { Position = new Vector3(side, 0, 0), Scale = new Vector3(1f, .78f, .5f),
                    Mesh = new SphereMesh { Radius = .026f, Height = .052f, RadialSegments = 10, Rings = 6 }, MaterialOverride = iris,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                pair.AddChild(new MeshInstance3D { Position = new Vector3(side, 0, .014f),
                    Mesh = new BoxMesh { Size = new Vector3(.007f, .036f, .004f) }, MaterialOverride = pupil,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            }
            AddChild(pair);
            _eyeGlints.Add(pair);
        }
    }

    // An old square boundary post at the clearing edge, cut with marks nobody
    // in the village would read aloud.
    private void BuildBoundaryPost()
    {
        var z = ClearingZ + ClearingRadius - 2f;
        var x = TrackX(z) - 3.2f;
        var post = new Node3D { Name = "BoundaryPost", Position = new Vector3(x, Ground(x, z), z), RotationDegrees = new Vector3(3, 24, -5) };
        AddChild(post);
        post.AddChild(new MeshInstance3D { Position = new Vector3(0, .8f, 0), Mesh = new BoxMesh { Size = new Vector3(.2f, 1.6f, .2f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("4a4036", "wood") });
        var cut = PainterlyMaterialLibrary.ForColor("1c1712", "wood");
        for (var mark = 0; mark < 4; mark++)
            post.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.05f + mark * .12f, .101f), RotationDegrees = new Vector3(0, 0, mark % 2 == 0 ? 35 : -35), Mesh = new BoxMesh { Size = new Vector3(.12f, .014f, .004f) }, MaterialOverride = cut });
        post.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.62f, 0), Mesh = new BoxMesh { Size = new Vector3(.26f, .05f, .26f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("e4e8ec", "snow_roof") });
    }

    public const float FallenSpruceZ = -74f;
    public const float HutZ = -142f;
    public OmniLight3D? HutLight { get; private set; }
    public MeshInstance3D? HutWindow { get; private set; }

    // A spruce down across the track at chest height, its crown buried in the
    // left bank: the way on is around the root plate on the right.
    private void BuildFallenSpruce()
    {
        var cx = TrackX(FallenSpruceZ);
        var root = new Node3D { Name = "FallenSpruce", Position = new Vector3(cx, Ground(cx, FallenSpruceZ), FallenSpruceZ), RotationDegrees = new Vector3(0, 12, 0) };
        AddChild(root);
        var bark = PainterlyMaterialLibrary.ForColor("3a3028", "bark_pine");
        root.AddChild(new MeshInstance3D { Position = new Vector3(-3.5f, 1.05f, 0), RotationDegrees = new Vector3(0, 0, 86), Mesh = new CylinderMesh { TopRadius = .2f, BottomRadius = .34f, Height = 15f, RadialSegments = 10 }, MaterialOverride = bark });
        root.AddChild(new MeshInstance3D { Position = new Vector3(4.3f, 1.2f, 0), RotationDegrees = new Vector3(0, 90, 0), Scale = new Vector3(1, 1, .3f), Mesh = new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.3f, Height = 1f, RadialSegments = 12 }, MaterialOverride = PainterlyMaterialLibrary.ForColor("4a3d31", "earth") });
        root.AddChild(new MeshInstance3D { Position = new Vector3(-3.5f, 1.4f, 0), RotationDegrees = new Vector3(0, 0, 86), Scale = new Vector3(1, 1, .25f), Mesh = new CylinderMesh { TopRadius = .2f, BottomRadius = .45f, Height = 14.5f, RadialSegments = 8 }, MaterialOverride = PainterlyMaterialLibrary.ForColor("e6eaee", "snow_roof") });
        var body = new StaticBody3D { Name = "FallenSpruceBody" };
        body.AddChild(new CollisionShape3D { Position = new Vector3(-3.5f, 1.05f, 0), Shape = new BoxShape3D { Size = new Vector3(15f, 2.1f, .7f) } });
        body.AddChild(new CollisionShape3D { Position = new Vector3(4.3f, 1.2f, 0), Shape = new BoxShape3D { Size = new Vector3(.6f, 2.6f, 2.6f) } });
        root.AddChild(body);
    }

    // A hunter's winter hut off the track, one window lit. The light goes out
    // when someone comes close; the door stays shut.
    private void BuildHuntersHut()
    {
        var x = TrackX(HutZ) - 13f;
        var hut = new Node3D { Name = "HuntersHut", Position = new Vector3(x, Ground(x, HutZ) - .25f, HutZ), RotationDegrees = new Vector3(0, -80, 0) };
        AddChild(hut);
        var logs = PainterlyMaterialLibrary.ForColor("5a4a3b", "log_wall");
        hut.AddChild(new MeshInstance3D { Position = new Vector3(0, 1.3f, 0), Mesh = new BoxMesh { Size = new Vector3(4.2f, 2.6f, 3.6f) }, MaterialOverride = logs });
        foreach (var side in new[] { -1f, 1f })
            hut.AddChild(new MeshInstance3D { Position = new Vector3(0, 3.05f, side * .95f), RotationDegrees = new Vector3(side * 38f, 0, 0), Mesh = new BoxMesh { Size = new Vector3(4.6f, .14f, 2.45f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("e8ecef", "snow_roof") });
        hut.AddChild(new MeshInstance3D { Position = new Vector3(-.9f, 1f, 1.81f), Mesh = new BoxMesh { Size = new Vector3(.8f, 1.8f, .05f) }, MaterialOverride = PainterlyMaterialLibrary.ForColor("3b2f25", "wood") });
        HutWindow = new MeshInstance3D
        {
            Position = new Vector3(.9f, 1.45f, 1.81f),
            Mesh = new BoxMesh { Size = new Vector3(.55f, .45f, .04f) },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, .72f, .36f) }
        };
        hut.AddChild(HutWindow);
        HutLight = new OmniLight3D { Position = new Vector3(.9f, 1.45f, 2.3f), LightColor = new Color(1f, .7f, .4f), LightEnergy = 1.4f, OmniRange = 7f };
        hut.AddChild(HutLight);
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Position = new Vector3(0, 1.3f, 0), Shape = new BoxShape3D { Size = new Vector3(4.2f, 2.6f, 3.6f) } });
        hut.AddChild(body);
    }

    public void PutOutHutLight()
    {
        if (HutLight is not null) HutLight.Visible = false;
        if (HutWindow is not null) HutWindow.MaterialOverride = PainterlyMaterialLibrary.ForColor("15120f", "wood");
    }

    private readonly List<Node3D> _bushes = new();
    public IReadOnlyList<Node3D> Bushes => _bushes;

    // Low snowy undergrowth by the track: the thing that moves when something
    // unseen goes through it.
    private void BuildBushes()
    {
        var leaves = PainterlyMaterialLibrary.ForColor("2c3a2e", "foliage");
        var snow = PainterlyMaterialLibrary.ForColor("e3e8ec", "snow_roof");
        for (var z = 4f; z > ClearingZ + ClearingRadius; z -= _rng.RandfRange(4f, 9f))
        foreach (var side in new[] { -1f, 1f })
        {
            if (_rng.Randf() < .35f) continue;
            var offset = side * _rng.RandfRange(2.6f, 8f);
            var x = TrackX(z) + offset;
            if (Mathf.Abs(z - FallenSpruceZ) < 7f && offset > 3f && offset < 11f) continue;
            var bush = new Node3D { Name = $"Bush{_bushes.Count}", Position = new Vector3(x, Ground(x, z) - .1f, z), RotationDegrees = new Vector3(0, _rng.RandfRange(0, 360), 0) };
            for (var lobe = 0; lobe < 3; lobe++)
            {
                var r = _rng.RandfRange(.35f, .6f);
                var at = new Vector3(_rng.RandfRange(-.4f, .4f), r * .7f, _rng.RandfRange(-.4f, .4f));
                bush.AddChild(new MeshInstance3D { Position = at, Scale = new Vector3(1, .75f, 1), Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 8, Rings = 5 }, MaterialOverride = leaves });
                bush.AddChild(new MeshInstance3D { Position = at + Vector3.Up * r * .5f, Scale = new Vector3(.85f, .35f, .85f), Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 8, Rings = 4 }, MaterialOverride = snow });
            }
            AddChild(bush);
            _bushes.Add(bush);
        }
    }

    /// <summary>A bush jerks and drops its snow, as if something went through it.</summary>
    public void Shake(Node3D bush, float strength = 1f)
    {
        var tween = bush.CreateTween();
        var base_ = bush.RotationDegrees;
        for (var index = 0; index < 5; index++)
        {
            var amount = (5 - index) * 3.2f * strength;
            tween.TweenProperty(bush, "rotation_degrees", base_ + new Vector3(index % 2 == 0 ? amount : -amount, 0, index % 2 == 0 ? -amount * .6f : amount * .6f), .07);
        }
        tween.TweenProperty(bush, "rotation_degrees", base_, .12);
        SnowPuff(bush.GlobalPosition + Vector3.Up * .8f);
    }

    public void SnowPuff(Vector3 at)
    {
        var puff = new CpuParticles3D
        {
            Emitting = false, OneShot = true, Amount = 60, Lifetime = 1.4f, Explosiveness = .9f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = .5f,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = .6f, InitialVelocityMax = 1.8f,
            Gravity = new Vector3(.3f, -1.6f, 0), LocalCoords = false,
            Mesh = WinterParticleSurfaces.Snow(.06f, .65f),
            ColorRamp = WinterParticleSurfaces.Fade(),
            ScaleAmountMin = .35f, ScaleAmountMax = 1.2f
        };
        AddChild(puff);
        puff.GlobalPosition = at;
        puff.Emitting = true;
        puff.Finished += puff.QueueFree;
    }

    // Falling snow around the viewer with gusts across the track.
    private void BuildSnowfall()
    {
        var snow = new CpuParticles3D
        {
            Name = "DeepForestSnowfall",
            Amount = 2600,
            Lifetime = 5f,
            Preprocess = 5f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(22, 1, 22),
            Direction = new Vector3(.35f, -1, 0),
            Spread = 18f,
            Gravity = new Vector3(1.1f, -2.4f, .2f),
            InitialVelocityMin = .6f,
            InitialVelocityMax = 1.4f,
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.3f,
            LocalCoords = false,
            Mesh = WinterParticleSurfaces.Snow(.042f, .65f),
            ColorRamp = WinterParticleSurfaces.Fade(),
            LifetimeRandomness = .35f, Randomness = .4f
        };
        AddChild(snow);
        Snowfall = snow;
    }

    public CpuParticles3D? Snowfall { get; private set; }

    /// <summary>Keeps the snowfall volume above the viewer.</summary>
    public void FollowViewer(Vector3 viewer)
    {
        if (Snowfall is not null) Snowfall.GlobalPosition = viewer + Vector3.Up * 9f;
    }
}
