using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Prologue art pass (author request 2026-11-06): in the nightmare the photo
/// forest closes in. A thicket layer exists only for pw-prologue and is built
/// the first time the prologue is shown, so W01 episode loads pay nothing:
/// young spruce undergrowth crowding the prints, dead standing snags leaning
/// at odd angles with hollows and snapped-off branch stubs, and dead trees
/// lying in the snow beside the track. All of it uses the game's own tree
/// models (winter.pine.v1 and the winter dead tree) in MultiMesh batches, with
/// no collision; the authored route keeps its clear standing lane.
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
        if (_prologueThicket is { } thicket && GodotObject.IsInstanceValid(thicket)) thicket.Visible = visible;
    }

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

        var ground = GroundBoundsLocal();
        var rng = new RandomNumberGenerator { Seed = 0x55524d36 };
        var exclusions = new (Vector2 Centre, float Radius)[]
        {
            (Vector2.Zero, 3.5f),
            (new Vector2(BranchF01.X, BranchF01.Z), 5.5f),
            (PrologueMittenPoint, 2.2f),
            (new Vector2(PrologueF03.X, PrologueF03.Z), 7.5f)
        };
        bool Free(Vector2 point, float lane)
        {
            if (point.X < ground.Position.X + 1f || point.X > ground.End.X - 1f
                || point.Y < ground.Position.Z + 1f || point.Y > ground.End.Z - 1f) return false;
            if (PrologueRouteProgress(point).Lateral < lane) return false;
            foreach (var (centre, radius) in exclusions)
                if (point.DistanceTo(centre) < radius) return false;
            return true;
        }
        Vector2 AlongRoute(float progress, float side)
        {
            var at = PrologueRoutePoint(progress, side);
            return new Vector2(at.X, at.Z);
        }
        float Lean(float maxDegrees) => Mathf.DegToRad(rng.RandfRange(-maxDegrees, maxDegrees));

        // The night forest is its own stand of solid trees (PrologueTreeMeshes):
        // old spruce, pine and birch, three shapes of each. The daytime forest
        // of the photograph is hidden while the nightmare lasts.
        var kinds = new (string Name, ArrayMesh[] Meshes, float MinHeight, float MaxHeight, float Trunk, float Weight)[]
        {
            ("Spruce", new[] { PrologueTreeMeshes.Spruce(11), PrologueTreeMeshes.Spruce(12), PrologueTreeMeshes.Spruce(13) }, 11f, 21f, .02f, .5f),
            ("Pine", new[] { PrologueTreeMeshes.Pine(21), PrologueTreeMeshes.Pine(22), PrologueTreeMeshes.Pine(23) }, 14f, 24f, .023f, .28f),
            ("Birch", new[] { PrologueTreeMeshes.Birch(31), PrologueTreeMeshes.Birch(32), PrologueTreeMeshes.Birch(33) }, 9f, 17f, .015f, .22f)
        };
        var near = kinds.Select(kind => kind.Meshes.Select(_ => new List<Transform3D>()).ToArray()).ToArray();
        var far = kinds.Select(kind => kind.Meshes.Select(_ => new List<Transform3D>()).ToArray()).ToArray();
        var planted = new List<(Vector2 At, float Radius)>();
        var trees = 0;
        void Plant(Vector2 point, float lateral)
        {
            var roll = rng.Randf();
            var kindIndex = roll < kinds[0].Weight ? 0 : roll < kinds[0].Weight + kinds[1].Weight ? 1 : 2;
            var kind = kinds[kindIndex];
            var height = rng.RandfRange(kind.MinHeight, kind.MaxHeight);
            var scale = height / 10f;
            var girth = scale * rng.RandfRange(.85f, 1.2f);
            var trunk = kind.Trunk * 10f * girth;
            // The trail is a squeeze, never a wall: every stem keeps a body's width off it.
            if (!Free(point, .85f + trunk)) return;
            foreach (var (at, radius) in planted)
                if (at.DistanceSquaredTo(point) < (radius + trunk + .5f) * (radius + trunk + .5f)) return;
            planted.Add((point, trunk));
            var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                * new Basis(Vector3.Right, Lean(3.5f)) * new Basis(Vector3.Back, Lean(3.5f))
                * Basis.FromScale(new Vector3(girth, scale, girth));
            var transform = new Transform3D(basis, new Vector3(point.X, ForestGroundHeight(point.X, point.Y), point.Y));
            (lateral < 9f ? near : far)[kindIndex][rng.RandiRange(0, kind.Meshes.Length - 1)].Add(transform);
            trees++;
        }
        // Along the trail: stems close on both sides, so the path threads between them.
        for (var progress = 3f; progress < PrologueRouteLength - 5f; progress += rng.RandfRange(1.1f, 2.1f))
            foreach (var edge in new[] { -1f, 1f })
            {
                if (rng.Randf() < .2f) continue;
                var lateral = rng.RandfRange(1.15f, 2.6f);
                Plant(AlongRoute(progress + rng.RandfRange(-.6f, .6f), edge * lateral), lateral);
            }
        // The body of the forest, thinning with distance from the trail.
        for (var index = 0; index < 2600; index++)
        {
            var progress = rng.RandfRange(-14f, PrologueRouteLength + 16f);
            var lateral = Mathf.Lerp(2.6f, 46f, Mathf.Pow(rng.Randf(), 1.35f));
            var point = AlongRoute(progress, (rng.Randf() < .5f ? -1f : 1f) * lateral)
                + new Vector2(rng.RandfRange(-1.5f, 1.5f), rng.RandfRange(-1.5f, 1.5f));
            Plant(point, PrologueRouteProgress(point).Lateral);
        }
        for (var kindIndex = 0; kindIndex < kinds.Length; kindIndex++)
            for (var variant = 0; variant < kinds[kindIndex].Meshes.Length; variant++)
            {
                AddThicketBatches(thicket, $"{kinds[kindIndex].Name}{variant}Near", kinds[kindIndex].Meshes[variant], Transform3D.Identity, near[kindIndex][variant], castShadow: true);
                AddThicketBatches(thicket, $"{kinds[kindIndex].Name}{variant}Far", kinds[kindIndex].Meshes[variant], Transform3D.Identity, far[kindIndex][variant], castShadow: false);
            }

        // Young spruce under the old trees: the low wall the beam cannot see through.
        var young = new[] { PrologueTreeMeshes.YoungSpruce(41), PrologueTreeMeshes.YoungSpruce(42) };
        var undergrowth = new[] { new List<Transform3D>(), new List<Transform3D>() };
        for (var index = 0; index < 700; index++)
        {
            var progress = rng.RandfRange(-4f, PrologueRouteLength + 12f);
            var side = (rng.Randf() < .5f ? -1f : 1f) * Mathf.Lerp(1.7f, 18f, Mathf.Pow(rng.Randf(), 1.5f));
            var point = AlongRoute(progress, side) + new Vector2(rng.RandfRange(-1.2f, 1.2f), rng.RandfRange(-1.2f, 1.2f));
            var height = rng.RandfRange(1.3f, 4.6f);
            var scale = height / 10f;
            var girth = scale * rng.RandfRange(1f, 1.5f);
            if (!Free(point, 1.05f + girth * 2f)) continue;
            var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                * new Basis(Vector3.Right, Lean(6f)) * new Basis(Vector3.Back, Lean(6f))
                * Basis.FromScale(new Vector3(girth, scale, girth));
            undergrowth[index & 1].Add(new Transform3D(basis, new Vector3(point.X, ForestGroundHeight(point.X, point.Y) - .05f, point.Y)));
        }
        AddThicketBatches(thicket, "Young0", young[0], Transform3D.Identity, undergrowth[0], castShadow: false);
        AddThicketBatches(thicket, "Young1", young[1], Transform3D.Identity, undergrowth[1], castShadow: false);

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
        AddThicketBatches(thicket, "DeadSnags", dead.Mesh, Transform3D.Identity, snags, castShadow: true);

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
        AddThicketBatches(thicket, "Fallen", dead.Mesh, Transform3D.Identity, fallen, castShadow: false);

        var hollowMaterial = new StandardMaterial3D { AlbedoColor = new Color(.035f, .028f, .022f), Roughness = 1f };
        AddThicketBatches(thicket, "Hollows", new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6, Material = hollowMaterial },
            Transform3D.Identity, hollows, castShadow: false);
        var bark = dead.Mesh.GetSurfaceCount() > 0 ? dead.Mesh.SurfaceGetMaterial(0) : null;
        AddThicketBatches(thicket, "BrokenStubs", new CylinderMesh
        {
            TopRadius = .55f, BottomRadius = 1f, Height = 1f, RadialSegments = 6, Rings = 1,
            Material = bark ?? new StandardMaterial3D { AlbedoColor = new Color(.24f, .2f, .17f), Roughness = 1f }
        }, Transform3D.Identity, stubs, castShadow: false);

        GD.Print($"w01-prologue-thicket: trees={trees} undergrowth={undergrowth[0].Count + undergrowth[1].Count} snags={snags.Count} hollows={hollows.Count} "
            + $"stubs={stubs.Count} fallen={fallen.Count} built={Time.GetTicksMsec() - started}ms");
    }

    private void AddThicketBatches(Node3D parent, string name, Mesh mesh, Transform3D local, List<Transform3D> transforms, bool castShadow)
    {
        foreach (var group in transforms.GroupBy(transform => new Vector2I(
                     Mathf.FloorToInt(transform.Origin.X / ThicketCellSize), Mathf.FloorToInt(transform.Origin.Z / ThicketCellSize))))
        {
            var origin = new Vector3((group.Key.X + .5f) * ThicketCellSize, 0f, (group.Key.Y + .5f) * ThicketCellSize);
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
                // Night fog closes at ~64 m; nothing past it needs drawing.
                VisibilityRangeEnd = 90f,
                VisibilityRangeEndMargin = 10f,
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
