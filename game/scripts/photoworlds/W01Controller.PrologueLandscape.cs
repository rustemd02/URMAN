using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private Aabb _prologueLandscapeBounds;

    // One road centreline drives the snow cut, both wheel ruts and the distant
    // continuation. The first 24 m, including every playable road contact, stay put.
    private static float PrologueRoadBend(float v)
    {
        var distance = Mathf.Max(0f, Mathf.Abs(v) - 24f);
        return .008f * distance * distance;
    }

    private static bool InsidePrologueRoadContinuation(Vector2 point)
    {
        var (along, across) = PrologueStopAxes();
        var origin = new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z);
        var offset = point - origin;
        var v = offset.Dot(across);
        var centre = (StopRoadNear + StopRoadFar) * .5f + PrologueRoadBend(v);
        return Mathf.Abs(v) > 24f && Mathf.Abs(v) < 190f
            && Mathf.Abs(offset.Dot(along) - centre) < 5.5f;
    }

    private float PrologueLandscapeHeight(float x, float z)
    {
        var bounds = _prologueLandscapeBounds;
        var inset = Mathf.Min(Mathf.Min(x - bounds.Position.X, bounds.End.X - x),
            Mathf.Min(z - bounds.Position.Z, bounds.End.Z - z));
        // Offset both the foot and crest: the visible boundary is a wooded slope,
        // never the imported ground's straight rectangular outline.
        inset += 3f * Mathf.Sin(x * .047f + z * .031f)
            + 2f * Mathf.Sin(z * .079f - x * .021f);
        var variation = 2f * Mathf.Sin(x * .033f - z * .024f)
            + 1f * Mathf.Cos(z * .052f + x * .019f);
        float rise;
        if (inset >= 20f)
            rise = .35f * (1f - TerrainSmoothStep(20f, 40f, inset));
        else if (inset >= 10f)
            rise = Mathf.Lerp(.35f, 13f + variation, 1f - TerrainSmoothStep(10f, 20f, inset));
        else
            rise = 13f + variation + 3f * (1f - TerrainSmoothStep(-30f, 10f, inset));

        var (along, across) = PrologueStopAxes();
        var offset = new Vector2(x - PrologueTrailNodes[0].X, z - PrologueTrailNodes[0].Z);
        var v = offset.Dot(across);
        var roadDistance = Mathf.Abs(offset.Dot(along)
            - (StopRoadNear + StopRoadFar) * .5f - PrologueRoadBend(v));
        // Let the road bend through a shallow cutting instead of running into
        // the enclosing snow bank. Its shoulders still conceal the far bend.
        var cutting = TerrainSmoothStep(24f, 36f, Mathf.Abs(v))
            * (1f - TerrainSmoothStep(8f, 16f, roadDistance));
        rise *= 1f - cutting;
        if (inset < -70f)
        {
            var rolling = 9f + 6f * Mathf.Sin(x * .012f + z * .009f)
                + 4f * Mathf.Cos(z * .017f - x * .006f);
            rise = Mathf.Lerp(rise, rolling, TerrainSmoothStep(70f, 180f, -inset));
        }
        return ForestGroundHeight(x, z) + rise - .025f;
    }

    private void BuildPrologueLandscape(Node3D thicket, Aabb bounds)
    {
        _prologueLandscapeBounds = bounds;
        var landscape = new Node3D { Name = "ForestBeyondTrail" };
        thicket.AddChild(landscape);
        var source = Descendants<MeshInstance3D>(_groundVisual!).First(node => node.Mesh is not null);
        var centre = new Vector2(bounds.GetCenter().X, bounds.GetCenter().Z);
        var half = new Vector2(bounds.Size.X, bounds.Size.Z) * .5f;
        // The inner ring overlaps below the existing ground. Its rising part has
        // real contact before the guard boundary; the outer edge is beyond Far=160.
        var offsets = new[] { -44f, -30f, -21f, -15f, -9f, 0f, 28f, 80f, 190f, 640f };
        const int segments = 192;
        var points = new Vector3[offsets.Length, segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = index * Mathf.Tau / segments;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var edge = Mathf.Min(half.X / Mathf.Max(Mathf.Abs(direction.X), .0001f),
                half.Y / Mathf.Max(Mathf.Abs(direction.Y), .0001f));
            for (var ring = 0; ring < offsets.Length; ring++)
            {
                var point = centre + direction * (edge + offsets[ring]);
                points[ring, index] = new Vector3(point.X, PrologueLandscapeHeight(point.X, point.Y), point.Y);
            }
        }
        using var surface = new SurfaceTool();
        using var contact = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(0);
        contact.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(Vector3 point, bool collidable)
        {
            // MetricUV is the existing imported ground's 8 m repeat contract.
            surface.SetUV2(new Vector2(point.X, point.Z) / 8f);
            surface.SetColor(new Color(0f, 0f, 0f, 0f));
            surface.AddVertex(point);
            if (collidable) contact.AddVertex(point);
        }
        for (var ring = 0; ring < offsets.Length - 1; ring++)
        for (var index = 0; index < segments; index++)
        {
            var next = (index + 1) % segments;
            var collidable = ring < 5;
            foreach (var point in new[] { points[ring, index], points[ring + 1, index], points[ring, next],
                         points[ring + 1, index], points[ring + 1, next], points[ring, next] })
                Vertex(point, collidable);
        }
        surface.Index();
        surface.GenerateNormals();
        landscape.AddChild(new MeshInstance3D
        {
            Name = "NightSnowSlopesAndHorizon", Mesh = surface.Commit(),
            MaterialOverride = source.MaterialOverride,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        var bank = new StaticBody3D { Name = "SnowBankContact", CollisionLayer = 1u, CollisionMask = 0u };
        bank.SetMeta("footstepSurface", "snow_soft");
        bank.AddChild(new CollisionShape3D { Shape = contact.Commit().CreateTrimeshShape() });
        landscape.AddChild(bank);
        BuildPrologueDistantTreeLine(landscape, centre, half);
        ContinuePrologueRoad(landscape);
        GD.Print($"w01-night-landscape: terrain_triangles={(offsets.Length - 1) * segments * 2} "
            + $"contact_triangles={5 * segments * 2} distant_trees=160 road_continuation=190m");
    }

    private void BuildPrologueDistantTreeLine(Node3D owner, Vector2 centre, Vector2 half)
    {
        // Small solid silhouettes, no alpha cards, textures, imported daytime
        // forest, wind shader, per-tree contacts or shadow-map draws.
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int sides = 7;
        foreach (var tier in new[] { (Y: .22f, Radius: .21f, Top: .69f),
                     (Y: .42f, Radius: .17f, Top: .86f), (Y: .62f, Radius: .12f, Top: 1f) })
        for (var side = 0; side < sides; side++)
        {
            var a = side * Mathf.Tau / sides;
            var b = (side + 1) * Mathf.Tau / sides;
            surface.AddVertex(new Vector3(Mathf.Cos(a) * tier.Radius, tier.Y, Mathf.Sin(a) * tier.Radius));
            surface.AddVertex(new Vector3(0f, tier.Top, 0f));
            surface.AddVertex(new Vector3(Mathf.Cos(b) * tier.Radius, tier.Y, Mathf.Sin(b) * tier.Radius));
        }
        using var trunk = new CylinderMesh { TopRadius = .012f, BottomRadius = .022f,
            Height = .55f, RadialSegments = 4, Rings = 1 };
        surface.AppendFrom(trunk, 0, new Transform3D(Basis.Identity, new Vector3(0f, .275f, 0f)));
        surface.GenerateNormals();
        var mesh = surface.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = new Color(.075f, .085f, .09f), Roughness = 1f, MetallicSpecular = 0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        const int count = 160;
        var poses = new List<Transform3D>();
        var rng = new RandomNumberGenerator { Seed = 0x4e49474854 };
        for (var index = 0; index < count; index++)
        {
            var angle = (index + rng.RandfRange(-.35f, .35f)) * Mathf.Tau / count;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var edge = Mathf.Min(half.X / Mathf.Max(Mathf.Abs(direction.X), .0001f),
                half.Y / Mathf.Max(Mathf.Abs(direction.Y), .0001f));
            var point = centre + direction * (edge + rng.RandfRange(10f, 42f));
            // Keep the road's cutting open to its next bend.
            if (InsidePrologueRoadContinuation(point)) continue;
            var height = rng.RandfRange(10f, 19f);
            var scale = new Vector3(height * rng.RandfRange(.8f, 1.2f), height, height);
            var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau)).Scaled(scale);
            poses.Add(new Transform3D(basis,
                new Vector3(point.X, PrologueLandscapeHeight(point.X, point.Y) - .08f, point.Y)));
        }
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh, InstanceCount = poses.Count };
        for (var index = 0; index < poses.Count; index++) multi.SetInstanceTransform(index, poses[index]);
        owner.AddChild(new MultiMeshInstance3D { Name = "DistantNightTreeLine", Multimesh = multi,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private Vector3 PrologueContinuingRoadPoint(float u, float v, float lift, bool wheelTrack)
    {
        if (wheelTrack) u += .13f * Mathf.Sin(v * .16f) + .045f * Mathf.Sin(v * .63f);
        var point = StopPoint(u + PrologueRoadBend(v), v);
        point.Y = Mathf.Max(ForestGroundHeight(point.X, point.Z), PrologueLandscapeHeight(point.X, point.Z)) + lift;
        return point;
    }

    private void ContinuePrologueRoad(Node3D owner)
    {
        if (_stop?.GetNodeOrNull<MeshInstance3D>("Road") is not { Mesh: { } mesh } road) return;
        var (along, across) = PrologueStopAxes();
        var origin = new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z);
        var bent = new ArrayMesh();
        var minimumV = float.MaxValue;
        var maximumV = float.MinValue;
        // Warp the complete existing road, including both ruts, by the same
        // centreline. No untouched straight road remains behind the bend.
        for (var index = 0; index < mesh.GetSurfaceCount(); index++)
        {
            using var arrays = mesh.SurfaceGetArrays(index);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                var original = vertices[vertex];
                var offset = new Vector2(original.X, original.Z) - origin;
                var v = offset.Dot(across);
                minimumV = Mathf.Min(minimumV, v);
                maximumV = Mathf.Max(maximumV, v);
                var lift = original.Y - ForestGroundHeight(original.X, original.Z);
                vertices[vertex] = PrologueContinuingRoadPoint(offset.Dot(along), v, lift, false);
                if (Mathf.Abs(v) > 24f)
                    normals[vertex] = PrologueLandscapeNormal(vertices[vertex]);
            }
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;
            arrays[(int)Mesh.ArrayType.Normal] = normals;
            bent.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            bent.SurfaceSetMaterial(index, mesh.SurfaceGetMaterial(index));
        }
        road.Mesh = bent;
        using var continuation = new SurfaceTool();
        ArrayMesh? extended = null;
        for (var materialIndex = 0; materialIndex < mesh.GetSurfaceCount(); materialIndex++)
        {
            continuation.Begin(Mesh.PrimitiveType.Triangles);
            continuation.SetMaterial(mesh.SurfaceGetMaterial(materialIndex));
            void Strip(float from, float to, float lift, bool wheelTrack, float start, float end)
            {
                var rows = Mathf.Max(1, Mathf.CeilToInt(to - from));
                var columns = Mathf.CeilToInt(Mathf.Abs(end - start) / 2f);
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var u0 = Mathf.Lerp(from, to, (float)row / rows);
                    var u1 = Mathf.Lerp(from, to, (row + 1f) / rows);
                    var v0 = Mathf.Lerp(start, end, (float)column / columns);
                    var v1 = Mathf.Lerp(start, end, (column + 1f) / columns);
                    var a = PrologueContinuingRoadPoint(u0, v0, lift, wheelTrack);
                    var b = PrologueContinuingRoadPoint(u1, v0, lift, wheelTrack);
                    var c = PrologueContinuingRoadPoint(u1, v1, lift, wheelTrack);
                    var d = PrologueContinuingRoadPoint(u0, v1, lift, wheelTrack);
                    // Negative side is traversed towards the stop, keeping winding identical.
                    foreach (var point in new[] { a, b, c, a, c, d })
                    {
                        continuation.SetNormal(PrologueLandscapeNormal(point));
                        continuation.SetUV(new Vector2(point.X, point.Z) * .3f);
                        continuation.AddVertex(point);
                    }
                }
            }
            foreach (var (start, end) in new[] { (-190f, minimumV), (maximumV, 190f) })
                if (materialIndex == 0) Strip(StopRoadNear, StopRoadFar, .08f, false, start, end);
                else
                {
                    Strip(StopRoadNear + 1.42f, StopRoadNear + 1.78f, .092f, true, start, end);
                    Strip(StopRoadFar - 1.78f, StopRoadFar - 1.42f, .092f, true, start, end);
                }
            extended = continuation.Commit(extended);
            continuation.Clear();
        }
        owner.AddChild(new MeshInstance3D { Name = "RoadBeyondBothBends", Mesh = extended,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private Vector3 PrologueLandscapeNormal(Vector3 point)
    {
        const float sample = .25f;
        float Height(float x, float z) => Mathf.Max(ForestGroundHeight(x, z), PrologueLandscapeHeight(x, z));
        return new Vector3(Height(point.X - sample, point.Z) - Height(point.X + sample, point.Z),
            2f * sample, Height(point.X, point.Z - sample) - Height(point.X, point.Z + sample)).Normalized();
    }
}
