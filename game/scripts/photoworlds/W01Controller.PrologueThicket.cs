using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// The nightmare's Meshy pine forest is built only when pw-prologue is shown.
/// Local batches keep distant crowns cheap; nearby trunks and the closed hut
/// have contacts while the authored walking lane remains clear.
/// </summary>
public partial class W01Controller
{
    private const string DeadTreeScenePath = "res://assets/models/act1/urman_winter_dead_tree.glb";
    private const float ThicketCellSize = 32f;
    private Node3D? _prologueThicket;

    private void SetPrologueThicketVisible(bool visible)
    {
        if (visible && _prologueThicket is null)
        {
            try { BuildPrologueThicket(); }
            catch (Exception error)
            {
                // The prologue stays playable on the authored forest alone.
                GD.PushWarning($"W01 prologue thicket was not built: {error.Message}");
                _prologueThicket = new Node3D { Name = "PrologueThicket" };
                AddChild(_prologueThicket);
            }
        }
        if (_prologueThicket is { } thicket && GodotObject.IsInstanceValid(thicket))
        {
            thicket.Visible = visible;
            foreach(var body in Descendants<StaticBody3D>(thicket))body.CollisionLayer=visible?1u:0u;
        }
    }

    // Forest tuning.
    private const float PrologueBirchShare = .24f;
    private const float PrologueTreeSink = .028f;

    private void BuildPrologueThicket()
    {
        var started = Time.GetTicksMsec();
        var thicket = new Node3D { Name = "PrologueThicket" };
        thicket.SetMeta("presentationOnly", true);
        AddChild(thicket);
        _prologueThicket = thicket;

        var dead = LoadMeshFromScene(DeadTreeScenePath, "WinterDeadTree_LOD1");
        var deadBounds = dead.Mesh.GetAabb();
        var deadTrunkRadius = MeasureTrunkRadius(dead.Mesh, deadBounds, .08f, .22f);

        var originalGround = GroundBoundsLocal();
        var ground = originalGround.Grow(32f);
        BuildPrologueBackdropGround(thicket, originalGround);
        BuildPrologueStop(thicket);
        var rng = new RandomNumberGenerator { Seed = 0x55524d36 };
        var exclusions = new (Vector2 Centre, float Radius)[]
        {
            (Vector2.Zero, 3.5f),
            (new Vector2(BranchF01.X, BranchF01.Z), 5.5f),
            (new Vector2(PrologueHutPosition.X, PrologueHutPosition.Z), 3.5f),
            (new Vector2(PrologueF03.X, PrologueF03.Z), 3.8f)
        };
        var hutTrail = PrologueRoutePoint(PrologueRouteLength * .58f, 0);
        var hutStart = new Vector2(hutTrail.X, hutTrail.Z);
        var hutEnd = new Vector2(PrologueHutPosition.X, PrologueHutPosition.Z);
        var hutApproach = hutEnd - hutStart;
        bool Free(Vector2 point, float lane)
        {
            if (point.X < ground.Position.X + 1f || point.X > ground.End.X - 1f
                || point.Y < ground.Position.Z + 1f || point.Y > ground.End.Z - 1f) return false;
            if (PrologueRouteProgress(point).Lateral < lane) return false;
            if (InsidePrologueStopClearing(point)) return false;
            foreach (var (centre, radius) in exclusions)
                if (point.DistanceTo(centre) < radius) return false;
            var alongHut = Mathf.Clamp((point - hutStart).Dot(hutApproach) / hutApproach.LengthSquared(), 0, 1);
            if (point.DistanceTo(hutStart + hutApproach * alongHut) < 1.3f) return false;
            return true;
        }
        Vector2 AlongRoute(float progress, float side)
        {
            var at = PrologueRoutePoint(progress, side);
            // RoutePoint deliberately clamps gameplay to the route. The forest
            // must continue behind the start and beyond its end, not collapse
            // those extra rows onto the same endpoint.
            if (progress < 0 || progress > PrologueRouteLength)
            {
                var end = progress > PrologueRouteLength;
                var a = end ? PrologueTrailNodes[^2] : PrologueTrailNodes[0];
                var b = end ? PrologueTrailNodes[^1] : PrologueTrailNodes[1];
                var direction = new Vector2(b.X-a.X,b.Z-a.Z).Normalized();
                var extra = end ? progress-PrologueRouteLength : progress;
                at += new Vector3(direction.X,0,direction.Y)*extra;
            }
            return new Vector2(at.X, at.Z);
        }
        float Lean(float maxDegrees) => Mathf.DegToRad(rng.RandfRange(-maxDegrees, maxDegrees));

        // The stand: the project's own Meshy spruce and birch. The full models
        // stand where the torch can reach them, the lighter ones behind, and the
        // four-triangle cards only where the dark has already taken the detail.
        const string pinePath = "res://assets/models/act1/urman_winter_pine_meshy_v1.glb";
        const string birchPath = "res://assets/models/act1/urman_winter_birch_meshy_v2.glb";
        var pineHero = LoadMeshFromScene(pinePath, "WinterPine_LOD0");
        var pine = LoadMeshFromScene(pinePath, "WinterPine_LOD1");
        var pineFar = LoadMeshFromScene(pinePath, "WinterPine_LOD2");
        var birchHero = LoadMeshFromScene(birchPath, "WinterBirch_LOD0");
        var birch = LoadMeshFromScene(birchPath, "WinterBirch_LOD1");
        var birchFar = LoadMeshFromScene(birchPath, "WinterBirch_LOD2");
        // Every tree goes into one list per species; the level of detail is chosen
        // at runtime by the camera's distance to each cell (see AddThicketLodBatches).
        var pines = new List<Transform3D>();
        var birches = new List<Transform3D>();
        var trunks=new StaticBody3D { Name="PrologueMeshyTrunkContacts",CollisionLayer=1,CollisionMask=0 };
        thicket.AddChild(trunks);
        var planted = new List<(Vector2 At, float Radius)>();
        var trees = 0;
        void Plant(Vector2 point, float lateral)
        {
            var white = rng.Randf() < PrologueBirchShare;
            var height = white ? rng.RandfRange(12f, 19f) : rng.RandfRange(15f, 25f);
            var girth = height * (white ? rng.RandfRange(.7f, .9f) : rng.RandfRange(.62f, .86f));
            var trunk = (white ? .016f : .022f) * girth;
            if (!Free(point, 1.05f + trunk)) return;
            foreach (var (at, radius) in planted)
                if (at.DistanceSquaredTo(point) < (radius+trunk+1.15f)*(radius+trunk+1.15f)) return;
            planted.Add((point,trunk));
            var basis = new Basis(Vector3.Up,rng.RandfRange(0,Mathf.Tau))
                * new Basis(Vector3.Right,Lean(white ? 4f : 2f))
                * Basis.FromScale(new Vector3(girth,height,girth));
            // Set a little into the snow: the modelled root flare stays under the drift.
            var pose = new Transform3D(basis,new Vector3(point.X,ForestGroundHeight(point.X,point.Y)-height*PrologueTreeSink,point.Y));
            (white ? birches : pines).Add(pose);
            if(lateral<7f)trunks.AddChild(new CollisionShape3D {
                Position=pose.Origin+Vector3.Up*1.80f,Shape=new CylinderShape3D { Radius=trunk,Height=3f } });
            trees++;
        }
        for (var progress=-18f; progress<PrologueRouteLength+25f; progress+=rng.RandfRange(2.6f,4.2f))
            foreach (var edge in new[]{-1f,1f})
            {
                var lateral=rng.RandfRange(2.3f,5.2f);
                Plant(AlongRoute(progress,edge*lateral),lateral);
            }
        // Fill a real area at both ends as well as along the walk. In particular
        // looking back at spawn no longer reveals the end of a tree strip.
        for(var index=0;index<2400;index++)
        {
            var progress=rng.RandfRange(-32f,PrologueRouteLength+40f);
            var lateral=rng.RandfRange(4.5f,48f);
            var point=AlongRoute(progress,(rng.Randf()<.5f?-1:1)*lateral)
                +new Vector2(rng.RandfRange(-2f,2f),rng.RandfRange(-2f,2f));
            Plant(point,PrologueRouteProgress(point).Lateral);
        }
        AddThicketLodBatches(thicket,"MeshyPine",pineHero,pine,pineFar,pines);
        AddThicketLodBatches(thicket,"MeshyBirch",birchHero,birch,birchFar,birches);
        var undergrowth=new List<Transform3D>();
        for(var index=0;index<170;index++)
        {
            var point=AlongRoute(rng.RandfRange(-18f,PrologueRouteLength+22f),
                (rng.Randf()<.5f?-1:1)*rng.RandfRange(3.4f,17f));
            var height=rng.RandfRange(2.1f,4.5f);
            if(!Free(point,2.5f))continue;
            undergrowth.Add(new Transform3D(new Basis(Vector3.Up,rng.RandfRange(0,Mathf.Tau))
                *Basis.FromScale(new Vector3(height*.8f,height,height*.8f)),
                new(point.X,ForestGroundHeight(point.X,point.Y)-.04f,point.Y)));
        }
        AddThicketBatches(thicket,"MeshyYoungPines",pine.Mesh,pine.Local,undergrowth,castShadow:false,cell:16f,end:42f,endMargin:6f);

        // Dead standing snags, leaning; the near ones carry hollows and broken stubs.
        var snags = new List<Transform3D>();
        var hollows = new List<Transform3D>();
        var stubs = new List<Transform3D>();
        for (var index = 0; index < 150; index++)
        {
            var progress = rng.RandfRange(-4f, PrologueRouteLength + 12f);
            var side = (rng.Randf() < .5f ? -1f : 1f) * Mathf.Lerp(3.2f, 26f, Mathf.Pow(rng.Randf(), 1.3f));
            var point = AlongRoute(progress, side);
            var height = rng.RandfRange(7f, 17f);
            var heightScale = height / deadBounds.Size.Y;
            var girth = heightScale * rng.RandfRange(.85f, 1.35f);
            var trunk = deadTrunkRadius * girth;
            if (!Free(point, 2.2f + trunk * 2.5f)) continue;
            var lean = new Basis(Vector3.Right, Lean(9f)) * new Basis(Vector3.Back, Lean(9f));
            var yaw = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau));
            var basis = lean * yaw * Basis.FromScale(new Vector3(girth, heightScale, girth));
            var root = new Vector3(point.X, ForestGroundHeight(point.X, point.Y) - deadBounds.Position.Y * heightScale - .1f, point.Y);
            snags.Add(new Transform3D(basis, root) * dead.Local);

            if (Mathf.Abs(PrologueRouteProgress(point).Lateral) > 13f) continue;
            var axis = (lean * Vector3.Up).Normalized();
            var toRoute = AlongRoute(PrologueRouteProgress(point).Progress, 0f) - point;
            var facing = new Vector3(toRoute.X, 0f, toRoute.Y).Normalized();
            if (facing == Vector3.Zero) facing = Vector3.Forward;
            if (rng.Randf() < .55f)
            {
                // A hollow facing the track, as if something watched from it.
                var at = root + axis * rng.RandfRange(1.2f, 3.4f) + facing * trunk * .86f;
                var size = trunk * rng.RandfRange(.55f, .85f);
                var look = Basis.LookingAt(facing, Vector3.Up);
                hollows.Add(new Transform3D(look * Basis.FromScale(new Vector3(size * .7f, size * 1.25f, size * .35f)), at));
            }
            var stubCount = rng.RandiRange(1, 3);
            for (var stub = 0; stub < stubCount; stub++)
            {
                var around = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau)) * Vector3.Forward;
                var upTilt = Mathf.DegToRad(rng.RandfRange(-25f, 35f));
                var outward = (around * Mathf.Cos(upTilt) + Vector3.Up * Mathf.Sin(upTilt)).Normalized();
                var length = rng.RandfRange(.35f, 1.1f) * Mathf.Clamp(girth / heightScale, .8f, 1.3f);
                var at = root + axis * rng.RandfRange(1.8f, Mathf.Min(6f, height * .55f)) + outward * (trunk * .8f + length * .5f);
                var stubBasis = BasisFromUp(outward) * Basis.FromScale(new Vector3(trunk * .32f, length, trunk * .32f));
                stubs.Add(new Transform3D(stubBasis, at));
            }
        }
        AddThicketBatches(thicket, "DeadSnags", dead.Mesh, Transform3D.Identity, snags, castShadow: true, cell: 20f, end: 62f, endMargin: 8f);

        // Dead trees lying in the snow: never across the track.
        var fallen = new List<Transform3D>();
        for (var attempt = 0; attempt < 120 && fallen.Count < 18; attempt++)
        {
            var progress = rng.RandfRange(4f, PrologueRouteLength);
            var side = (rng.Randf() < .5f ? -1f : 1f) * rng.RandfRange(3.4f, 16f);
            var point = AlongRoute(progress, side);
            var length = rng.RandfRange(8f, 15f);
            var heightScale = length / deadBounds.Size.Y;
            var girth = heightScale * rng.RandfRange(.9f, 1.3f);
            var direction = new Vector2(1f, 0f).Rotated(rng.RandfRange(0f, Mathf.Tau));
            var clear = true;
            for (var sample = 0f; sample <= 1.001f && clear; sample += .1f)
                clear = Free(point + direction * length * sample, 2.3f);
            if (!clear) continue;
            var yaw = Mathf.Atan2(direction.X, direction.Y);
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.DegToRad(rng.RandfRange(82f, 88f)))
                * new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                * Basis.FromScale(new Vector3(girth, heightScale, girth));
            var y = ForestGroundHeight(point.X, point.Y) + deadTrunkRadius * girth * .55f;
            fallen.Add(new Transform3D(basis, new Vector3(point.X, y, point.Y)) * dead.Local);
        }
        AddThicketBatches(thicket, "Fallen", dead.Mesh, Transform3D.Identity, fallen, castShadow: false, cell: 20f, end: 50f, endMargin: 8f);

        var hollowMaterial = new StandardMaterial3D { AlbedoColor = new Color(.035f, .028f, .022f), Roughness = 1f };
        AddThicketBatches(thicket, "Hollows", new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6, Material = hollowMaterial },
            Transform3D.Identity, hollows, castShadow: false, cell: 20f, end: 40f, endMargin: 6f);
        var bark = dead.Mesh.GetSurfaceCount() > 0 ? dead.Mesh.SurfaceGetMaterial(0) : null;
        AddThicketBatches(thicket, "BrokenStubs", new CylinderMesh
        {
            TopRadius = .55f, BottomRadius = 1f, Height = 1f, RadialSegments = 6, Rings = 1,
            Material = bark ?? new StandardMaterial3D { AlbedoColor = new Color(.24f, .2f, .17f), Roughness = 1f }
        }, Transform3D.Identity, stubs, castShadow: false, cell: 20f, end: 40f, endMargin: 6f);

        BuildPrologueForestLandmarks(thicket);
        GD.Print($"w01-prologue-thicket: trees={trees} undergrowth={undergrowth.Count} snags={snags.Count} hollows={hollows.Count} "
            + $"stubs={stubs.Count} fallen={fallen.Count} built={Time.GetTicksMsec() - started}ms");
    }

    // Distance rings, in metres from the camera. Cells are tested whole, so the hero
    // ring uses small cells; a tree is drawn at full detail only inside ~HeroRing,
    // at the 6k-triangle model out to NearRing, and as a four-triangle card beyond.
    private const float HeroRing = 16f, NearRing = 54f, FarRing = 100f;

    private void AddThicketLodBatches(Node3D parent, string name,
        (Mesh Mesh, Transform3D Local) hero, (Mesh Mesh, Transform3D Local) near, (Mesh Mesh, Transform3D Local) far,
        List<Transform3D> poses)
    {
        AddThicketBatches(parent, name + "Hero", hero.Mesh, hero.Local, poses, castShadow: true,
            cell: 10f, begin: 0f, end: HeroRing, beginMargin: 0f, endMargin: 4f);
        AddThicketBatches(parent, name + "Near", near.Mesh, near.Local, poses, castShadow: true,
            cell: 16f, begin: HeroRing, end: NearRing, beginMargin: 4f, endMargin: 8f);
        AddThicketBatches(parent, name + "Far", far.Mesh, far.Local, poses, castShadow: false,
            cell: ThicketCellSize, begin: NearRing, end: FarRing, beginMargin: 8f, endMargin: 10f);
    }

    private void AddThicketBatches(Node3D parent, string name, Mesh mesh, Transform3D local, List<Transform3D> transforms, bool castShadow,
        float cell = ThicketCellSize, float begin = 0f, float end = 90f, float beginMargin = 0f, float endMargin = 10f)
    {
        foreach (var group in transforms.GroupBy(transform => new Vector2I(
                     Mathf.FloorToInt(transform.Origin.X / cell), Mathf.FloorToInt(transform.Origin.Z / cell))))
        {
            var origin = new Vector3((group.Key.X + .5f) * cell, 0f, (group.Key.Y + .5f) * cell);
            var items = group.ToArray();
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = items.Length };
            for (var index = 0; index < items.Length; index++)
            {
                var transform = items[index] * local;
                transform.Origin -= origin;
                multi.SetInstanceTransform(index, transform);
            }
            var batch = new MultiMeshInstance3D
            {
                Name = $"Thicket{name}_{group.Key.X}_{group.Key.Y}",
                Multimesh = multi,
                Position = origin,
                CastShadow = castShadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeBegin = begin,
                VisibilityRangeBeginMargin = beginMargin,
                VisibilityRangeEnd = end,
                VisibilityRangeEndMargin = endMargin,
                VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
            };
            batch.SetMeta("presentationOnly", true);
            parent.AddChild(batch);
        }
    }

    private Aabb GroundBoundsLocal()
    {
        if (_groundVisual is not null && GodotObject.IsInstanceValid(_groundVisual)
            && Descendants<MeshInstance3D>(_groundVisual).FirstOrDefault(node => node.Mesh is not null) is { } mesh)
            return GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.Mesh!.GetAabb();
        return new Aabb(new Vector3(-30f, -10f, -20f), new Vector3(160f, 40f, 200f));
    }

    private static (Mesh Mesh, Transform3D Local) LoadMeshFromScene(string scenePath, string nodeName)
    {
        if (ResourceLoader.Load<PackedScene>(scenePath) is not { } packed)
            throw new InvalidOperationException($"Missing scene: {scenePath}");
        var root = packed.Instantiate<Node3D>();
        try
        {
            if (root.FindChild(nodeName, true, false) is not MeshInstance3D node || node.Mesh is null)
                throw new InvalidOperationException($"Missing mesh {nodeName} in {scenePath}");
            var local = Transform3D.Identity;
            for (Node? cursor = node; cursor is Node3D spatial && cursor != root; cursor = cursor.GetParent())
                local = spatial.Transform * local;
            return (node.Mesh, local);
        }
        finally { root.Free(); }
    }

    /// <summary>Typical trunk radius in a low height band (fraction of the mesh height).</summary>
    private static float MeasureTrunkRadius(Mesh mesh, Aabb bounds, float fromFraction, float toFraction)
    {
        var low = bounds.Position.Y + bounds.Size.Y * fromFraction;
        var high = bounds.Position.Y + bounds.Size.Y * toFraction;
        var centre = bounds.GetCenter();
        var radii = new List<float>();
        foreach (var vertex in mesh.GetFaces())
            if (vertex.Y >= low && vertex.Y <= high)
                radii.Add(new Vector2(vertex.X - centre.X, vertex.Z - centre.Z).Length());
        if (radii.Count == 0) return bounds.Size.X * .06f;
        radii.Sort();
        return radii[(int)(radii.Count * .6f)];
    }

    private static Basis BasisFromUp(Vector3 up)
    {
        up = up.Normalized();
        var side = Mathf.Abs(up.Dot(Vector3.Forward)) > .95f ? Vector3.Right : up.Cross(Vector3.Forward).Normalized();
        var forward = side.Cross(up).Normalized();
        return new Basis(side, up, forward);
    }
}
