using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>Extend the existing plinth down to its actual terrain. The old
    /// top, footprint, material and node remain; the normal late contact rebuild
    /// consumes this mesh. No other mosque, stair or courtyard geometry moves.</summary>
    private void ExtendMosquePlinthToTerrain(MeshInstance3D plinth)
    {
        if (plinth.Mesh is not BoxMesh original || plinth.GetParent() is not Node3D parent)
            throw new InvalidOperationException("The mosque foundation requires its existing box plinth owner.");
        if (plinth.HasMeta("foundationTerrainSupportRepair"))
            throw new InvalidOperationException("The mosque foundation was already repaired in this world.");
        var transform = plinth.GlobalTransform;
        if (transform.Basis.Y.Normalized().Dot(Vector3.Up) < .99999f
            || Math.Abs(transform.Basis.X.Y) > .00001f || Math.Abs(transform.Basis.Z.Y) > .00001f)
            throw new InvalidOperationException("The mosque plinth must remain upright above its unchanged terrain.");
        var terrainNode = GetNode<CollisionShape3D>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        if (terrainNode.Shape is not ConcavePolygonShape3D terrain)
            throw new InvalidOperationException("Missing actual mosque terrain triangle owner.");
        var faces = terrain.GetFaces();
        var toPlinth = transform.AffineInverse() * terrainNode.GlobalTransform;
        var size = original.Size;
        var halfX = size.X * .5f;
        var halfZ = size.Z * .5f;
        var originalTop = transform * new Vector3(0, size.Y * .5f, 0);
        var originalBottom = transform * new Vector3(0, -size.Y * .5f, 0);
        // Capture exactly the caller's old local floor calculation before any
        // mesh/position change; the caller also keeps that already computed value.
        var originalFloor = parent.ToGlobal(plinth.Position + Vector3.Up * (size.Y * .5f + .03f));
        var minimum = float.PositiveInfinity;
        var maximum = float.NegativeInfinity;
        var lowest = Vector3.Zero;
        var coveredArea = 0d;
        var coveredTriangles = 0;
        for (var index = 0; index < faces.Length; index += 3)
        {
            var a = toPlinth * faces[index];
            var b = toPlinth * faces[index + 1];
            var c = toPlinth * faces[index + 2];
            if (Math.Max(a.X, Math.Max(b.X, c.X)) < -halfX || Math.Min(a.X, Math.Min(b.X, c.X)) > halfX
                || Math.Max(a.Z, Math.Max(b.Z, c.Z)) < -halfZ || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > halfZ) continue;
            var polygon = new List<Vector3> { a, b, c };
            polygon = ClipFoundationTriangle(polygon, p => p.X + halfX);
            polygon = ClipFoundationTriangle(polygon, p => halfX - p.X);
            polygon = ClipFoundationTriangle(polygon, p => p.Z + halfZ);
            polygon = ClipFoundationTriangle(polygon, p => halfZ - p.Z);
            if (polygon.Count < 3) continue;
            double area = 0;
            for (var vertex = 0; vertex < polygon.Count; vertex++)
            {
                var p = polygon[vertex]; var q = polygon[(vertex + 1) % polygon.Count];
                area += (double)p.X * q.Z - (double)q.X * p.Z;
            }
            area = Math.Abs(area) * .5;
            if (area < .00000001) continue;
            coveredArea += area;
            coveredTriangles++;
            foreach (var point in polygon)
            {
                if (point.Y < minimum) { minimum = point.Y; lowest = point; }
                maximum = Math.Max(maximum, point.Y);
            }
        }
        var expectedArea = (double)size.X * size.Z;
        if (coveredTriangles == 0 || !float.IsFinite(minimum)
            || Math.Abs(coveredArea - expectedArea) > Math.Max(.001, expectedArea * .00002))
            throw new InvalidOperationException($"Incomplete actual terrain under mosque plinth: {coveredArea}/{expectedArea} m².");

        const float embedMetres = .05f;
        var bottom = Math.Min(-size.Y * .5f, minimum - embedMetres / transform.Basis.Y.Length());
        var top = size.Y * .5f;
        var newSize = size with { Y = top - bottom };
        var replacement = (BoxMesh)original.Duplicate();
        replacement.Size = newSize;
        plinth.Mesh = replacement;
        plinth.Position += plinth.Basis * new Vector3(0, (top + bottom) * .5f, 0);
        var preservedTop = plinth.GlobalTransform * new Vector3(0, newSize.Y * .5f, 0);
        if (preservedTop.DistanceTo(originalTop) > .00001f)
            throw new InvalidOperationException("Foundation repair moved the existing mosque top plane.");
        plinth.SetMeta("foundationTerrainSupportRepair", true);
        plinth.SetMeta("foundationOriginalSize", size);
        plinth.SetMeta("foundationRepairedSize", newSize);
        plinth.SetMeta("foundationOriginalTopWorld", originalTop);
        plinth.SetMeta("foundationOriginalBottomWorld", originalBottom);
        plinth.SetMeta("foundationOriginalRoomFloorWorld", originalFloor);
        plinth.SetMeta("foundationLowestTerrainWorld", transform * lowest);
        plinth.SetMeta("foundationHighestTerrainWorldY", (transform * new Vector3(0, maximum, 0)).Y);
        plinth.SetMeta("foundationFootprintArea", expectedArea);
        plinth.SetMeta("foundationCoveredTerrainArea", coveredArea);
        plinth.SetMeta("foundationCoveredTerrainTriangles", coveredTriangles);
        plinth.SetMeta("foundationEmbedMetres", embedMetres);
        plinth.SetMeta("foundationTerrainOwner", terrainNode.GetPath().ToString());
        plinth.SetMeta("foundationContactPolicy", "existing plinth extended downward; late authored surface contact rebuild; no second collider");
    }

    private static List<Vector3> ClipFoundationTriangle(List<Vector3> polygon, Func<Vector3, float> insideDistance)
    {
        var output = new List<Vector3>();
        if (polygon.Count == 0) return output;
        var previous = polygon[^1]; var previousDistance = insideDistance(previous);
        foreach (var current in polygon)
        {
            var distance = insideDistance(current);
            if ((previousDistance >= 0) != (distance >= 0))
                output.Add(previous.Lerp(current, previousDistance / (previousDistance - distance)));
            if (distance >= 0) output.Add(current);
            previous = current; previousDistance = distance;
        }
        return output;
    }
}
