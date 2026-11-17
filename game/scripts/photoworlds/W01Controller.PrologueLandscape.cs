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
        inset += 3f * Mathf.Sin(x * .047f + z * .031f)
            + 2f * Mathf.Sin(z * .079f - x * .021f);
        // Broad wooded rolls, not the old 13 m rectangular embankment. The
        // interior stays under the imported ground; the slope grows over 40 m.
        var envelope = 1f - TerrainSmoothStep(0f, 40f, inset);
        var rolling = 3.2f + 1.1f * Mathf.Sin(x * .029f + z * .017f)
            + .7f * Mathf.Cos(z * .043f - x * .013f)
            + .4f * Mathf.Sin(x * .061f + z * .037f);
        var rise = envelope * rolling;
        var (along, across) = PrologueStopAxes();
        var offset = new Vector2(x - PrologueTrailNodes[0].X, z - PrologueTrailNodes[0].Z);
        var v = offset.Dot(across);
        var roadDistance = Mathf.Abs(offset.Dot(along)
            - (StopRoadNear + StopRoadFar) * .5f - PrologueRoadBend(v));
        // Leave the same low road and real return walk; blend the wooded
        // shoulders over 24 m instead of slicing a sheer-sided snow corridor.
        var cutting = TerrainSmoothStep(24f, 36f, Mathf.Abs(v))
            * (1f - TerrainSmoothStep(8f, 32f, roadDistance));
        return ForestGroundHeight(x, z) + rise * (1f - cutting) - .025f;
    }

    private void BuildPrologueLandscape(Node3D thicket, Aabb bounds)
    {
        _prologueLandscapeBounds = bounds;
        var landscape = new Node3D { Name = "ForestBeyondTrail" };
        thicket.AddChild(landscape);
        var source = Descendants<MeshInstance3D>(_groundVisual!).First(node => node.Mesh is not null);
        var centre = new Vector2(bounds.GetCenter().X, bounds.GetCenter().Z);
        const float step = 4f;
        var outer = bounds.Grow(96f);
        var inner = new Rect2(new Vector2(bounds.Position.X + 44f, bounds.Position.Z + 44f),
            new Vector2(bounds.Size.X - 88f, bounds.Size.Z - 88f));
        using var surface = new SurfaceTool();
        using var contact = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(0);
        contact.Begin(Mesh.PrimitiveType.Triangles);
        var triangles = 0;
        var contactTriangles = 0;
        Vector3 Point(float x, float z) => new(x, PrologueLandscapeHeight(x, z), z);
        void Triangle(Vector3 a, Vector3 b, Vector3 c, bool collidable)
        {
            foreach (var point in new[] { a, b, c })
            {
                surface.SetUV2(new Vector2(point.X, point.Z) / 8f);
                surface.SetColor(new Color(0f, 0f, 0f, 0f));
                surface.AddVertex(point);
                if (collidable) contact.AddVertex(point);
            }
            triangles++;
            if (collidable) contactTriangles++;
        }
        // Four-metre cells resolve the roadside rolls in every direction.
        // Sparse radial rings previously interpolated a 52/110 m-wide triangle
        // across the road cutting and exposed the two giant planar trapezoids.
        for (var z = outer.Position.Z; z < outer.End.Z - .01f; z += step)
        for (var x = outer.Position.X; x < outer.End.X - .01f; x += step)
        {
            var endX = Mathf.Min(x + step, outer.End.X);
            var endZ = Mathf.Min(z + step, outer.End.Z);
            if (inner.HasPoint(new Vector2(x + .01f, z + .01f))
                && inner.HasPoint(new Vector2(endX - .01f, endZ - .01f))) continue;
            var a = Point(x, z); var b = Point(endX, z);
            var c = Point(endX, endZ); var d = Point(x, endZ);
            var collidable = x < bounds.End.X + 8f && endX > bounds.Position.X - 8f
                && z < bounds.End.Z + 8f && endZ > bounds.Position.Z - 8f;
            Triangle(a, b, c, collidable); Triangle(a, c, d, collidable);
        }
        // Continue the same ground past the far frustum. Only these unseen
        // outer rings are coarse; they share every vertex of the near rim.
        var rim = new List<Vector2>();
        for (var x = outer.Position.X; x < outer.End.X; x += step) rim.Add(new(x, outer.Position.Z));
        for (var z = outer.Position.Z; z < outer.End.Z; z += step) rim.Add(new(outer.End.X, z));
        for (var x = outer.End.X; x > outer.Position.X; x -= step) rim.Add(new(x, outer.End.Z));
        for (var z = outer.End.Z; z > outer.Position.Z; z -= step) rim.Add(new(outer.Position.X, z));
        var half = new Vector2(outer.Size.X, outer.Size.Z) * .5f;
        Vector3 FarPoint(Vector2 point, float distance)
        {
            var offset = point - centre;
            return Point(point.X + offset.X / half.X * distance, point.Y + offset.Y / half.Y * distance);
        }
        var rings = new[] { 0f, 64f, 224f, 544f };
        for (var ring = 0; ring < rings.Length - 1; ring++)
        for (var index = 0; index < rim.Count; index++)
        {
            var next = (index + 1) % rim.Count;
            var a = FarPoint(rim[index], rings[ring]); var b = FarPoint(rim[index], rings[ring + 1]);
            var c = FarPoint(rim[next], rings[ring + 1]); var d = FarPoint(rim[next], rings[ring]);
            // The rectangular rim runs clockwise in Godot's X/Z plane.
            Triangle(a, b, d, false); Triangle(b, c, d, false);
        }
        surface.Index(); surface.GenerateNormals();
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
        BuildPrologueDistantTreeLine(landscape, centre, new Vector2(bounds.Size.X, bounds.Size.Z) * .5f);
        ContinuePrologueRoad(landscape);
        GD.Print($"w01-night-landscape: terrain_triangles={triangles} contact_triangles={contactTriangles} "
            + "ground_step=4m wooded_rolls=1..5.4m road_continuation=190m");
    }

    private void BuildPrologueDistantTreeLine(Node3D owner, Vector2 centre, Vector2 half)
    {
        // Reuse the actual winter pine and the thicket's resident-cell/LOD
        // lifecycle. Neither the existing stand seed nor its placements change.
        foreach (var key in new[] { "PineLod0", "PineLod1", "PineLod2" }) EnsureThicketMesh(key);
        var poses = new List<Transform3D>();
        var rng = new RandomNumberGenerator { Seed = 0x4e49474854 };
        var trunks = new StaticBody3D { Name = "RoadsidePineTrunkContacts", CollisionLayer = 1u, CollisionMask = 0u };
        owner.AddChild(trunks);
        void Plant(Vector2 point, float height, bool reachable = false)
        {
            var girth = height * rng.RandfRange(.70f, .90f);
            var basis = new Basis(Vector3.Up, rng.RandfRange(0f, Mathf.Tau))
                * Basis.FromScale(new Vector3(girth, height, girth));
            var y = Mathf.Max(ForestGroundHeight(point.X, point.Y), PrologueLandscapeHeight(point.X, point.Y));
            var pose = new Transform3D(basis, new Vector3(point.X, y - height * PrologueTreeSink, point.Y));
            poses.Add(pose);
            if (reachable) trunks.AddChild(new CollisionShape3D
            {
                Position = pose.Origin + Vector3.Up * 1.80f,
                Shape = new CylinderShape3D { Radius = .022f * girth, Height = 3f }
            });
        }
        for (var index = 0; index < 160; index++)
        {
            var angle = (index + rng.RandfRange(-.35f, .35f)) * Mathf.Tau / 160f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var edge = Mathf.Min(half.X / Mathf.Max(Mathf.Abs(direction.X), .0001f),
                half.Y / Mathf.Max(Mathf.Abs(direction.Y), .0001f));
            var point = centre + direction * (edge + rng.RandfRange(10f, 42f));
            if (InsidePrologueRoadContinuation(point)) continue;
            Plant(point, rng.RandfRange(12f, 21f));
        }
        // Two irregular rows follow each bend, leaving the full carriageway
        // and return reflectors open. Crowns, rather than a transverse wall,
        // overlap in perspective where the road curves out of sight.
        rng.Seed = 0x524f414453;
        var (along, across) = PrologueStopAxes();
        var origin = new Vector2(PrologueTrailNodes[0].X, PrologueTrailNodes[0].Z);
        foreach (var end in new[] { -1f, 1f })
        foreach (var side in new[] { -1f, 1f })
        for (var distance = 28f; distance < 146f; distance += rng.RandfRange(5f, 8f))
        for (var row = 0; row < 2; row++)
        {
            var v = end * (distance + rng.RandfRange(-1.2f, 1.2f));
            var lateral = side * (row == 0 ? rng.RandfRange(9f, 13f) : rng.RandfRange(20f, 29f));
            var u = (StopRoadNear + StopRoadFar) * .5f + PrologueRoadBend(v) + lateral;
            Plant(origin + along * u + across * v, rng.RandfRange(15f, 24f), row == 0 && distance < 60f);
        }
        AddThicketLodBatches(owner, "HorizonRoadPines", "PineLod0", "PineLod1", "PineLod2", poses);
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
