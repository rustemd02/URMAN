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

        var provider = new AgentBAct1ExteriorLayer();
        ArrayMesh pine;
        try { pine = provider.FoliageMesh("WinterPine", "kara"); }
        finally { provider.Free(); }
        var pineBounds = pine.GetAabb();
        var dead = LoadMeshFromScene(DeadTreeScenePath, "WinterDeadTree_LOD1");
        var deadBounds = dead.Mesh.GetAabb();
        var deadTrunkRadius = MeasureTrunkRadius(dead.Mesh, deadBounds, .08f, .22f);

        var ground = GroundBoundsLocal();
        var rng = new RandomNumberGenerator { Seed = 0x55524d36 };
        var exclusions = new (Vector2 Centre, float Radius)[]
        {
            (Vector2.Zero, 4.5f),
            (new Vector2(BranchF01.X, BranchF01.Z), 5.5f),
            (PrologueMittenPoint, 3f),
            (new Vector2(PrologueF03.X, PrologueF03.Z), 11f)
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

        // Young spruce crowding the track: the "чаща" the prints push through.
        var undergrowth = new List<Transform3D>();
        for (var index = 0; index < 520; index++)
        {
            var progress = rng.RandfRange(-4f, PrologueRouteLength + 12f);
            var side = (rng.Randf() < .5f ? -1f : 1f) * Mathf.Lerp(2.3f, 15f, Mathf.Pow(rng.Randf(), 1.6f));
            var point = AlongRoute(progress, side) + new Vector2(rng.RandfRange(-1.2f, 1.2f), rng.RandfRange(-1.2f, 1.2f));
            var height = rng.RandfRange(1.8f, 6.8f);
            var heightScale = height / pineBounds.Size.Y;
            var girth = heightScale * rng.RandfRange(.75f, 1.15f);
            if (!Free(point, 1.6f + girth * Mathf.Max(pineBounds.Size.X, pineBounds.Size.Z) * .22f)) continue;
            var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                * new Basis(Vector3.Right, Lean(6f)) * new Basis(Vector3.Back, Lean(6f))
                * Basis.FromScale(new Vector3(girth, heightScale, girth));
            var y = ForestGroundHeight(point.X, point.Y) - pineBounds.Position.Y * heightScale - .05f;
            undergrowth.Add(new Transform3D(basis, new Vector3(point.X, y, point.Y)));
        }
        AddThicketBatches(thicket, "Undergrowth", pine, Transform3D.Identity, undergrowth, castShadow: false);

        // The trail itself: trunks close on both sides of a gap that wanders
        // across the line of the prints, so the walk is a squeeze between trees
        // and not a cleared ride. No collision: the lane can never trap anyone.
        var trailPines = new List<Transform3D>();
        var trailSnags = new List<Transform3D>();
        for (var progress = 5f; progress < PrologueRouteLength - 9f; progress += rng.RandfRange(1.3f, 2.3f))
        {
            var weave = Mathf.Sin(progress * .19f) * .75f + Mathf.Sin(progress * .071f + 1.3f) * .35f;
            foreach (var edge in new[] { -1f, 1f })
            {
                if (rng.Randf() < .22f) continue;
                var gap = rng.RandfRange(1.45f, 2.3f);
                var point = AlongRoute(progress + rng.RandfRange(-.5f, .5f), weave + edge * gap);
                var clearOfSetPieces = true;
                foreach (var (centre, radius) in exclusions)
                    if (point.DistanceTo(centre) < radius) clearOfSetPieces = false;
                if (!clearOfSetPieces) continue;
                if (rng.Randf() < .62f)
                {
                    // Tall, thin spruce: the beam catches the trunk, the crown is lost above.
                    var height = rng.RandfRange(7f, 13f);
                    var heightScale = height / pineBounds.Size.Y;
                    var girth = heightScale * rng.RandfRange(.34f, .5f);
                    var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                        * new Basis(Vector3.Right, Lean(4f)) * new Basis(Vector3.Back, Lean(4f))
                        * Basis.FromScale(new Vector3(girth, heightScale, girth));
                    var y = ForestGroundHeight(point.X, point.Y) - pineBounds.Position.Y * heightScale - .05f;
                    trailPines.Add(new Transform3D(basis, new Vector3(point.X, y, point.Y)));
                }
                else
                {
                    var height = rng.RandfRange(6f, 12f);
                    var heightScale = height / deadBounds.Size.Y;
                    var girth = heightScale * rng.RandfRange(.8f, 1.15f);
                    // A dead tree leaning in over the trail from its side.
                    var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                        * new Basis(Vector3.Right, Lean(7f)) * new Basis(Vector3.Back, Lean(7f))
                        * Basis.FromScale(new Vector3(girth, heightScale, girth));
                    var root = new Vector3(point.X, ForestGroundHeight(point.X, point.Y) - deadBounds.Position.Y * heightScale - .1f, point.Y);
                    trailSnags.Add(new Transform3D(basis, root) * dead.Local);
                }
            }
        }
        AddThicketBatches(thicket, "TrailPines", pine, Transform3D.Identity, trailPines, castShadow: true);
        AddThicketBatches(thicket, "TrailSnags", dead.Mesh, Transform3D.Identity, trailSnags, castShadow: true);

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

        GD.Print($"w01-prologue-thicket: undergrowth={undergrowth.Count} snags={snags.Count} hollows={hollows.Count} "
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
