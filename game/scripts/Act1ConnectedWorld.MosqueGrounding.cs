using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal sealed record MosqueGroundSpan(Vector3 Min, Vector3 Max, float TerrainMin, float TerrainMax,
        double CoveredArea, int TerrainTriangles);
    internal sealed record MosqueGroundingRecord(MeshInstance3D Mesh, Vector3 OriginalSize,
        Transform3D OriginalTransform, float HeightAboveTerrain, bool VisualOnly, MosqueGroundSpan[] Spans);
    internal sealed record MosqueTerrainCutRecord(MeshInstance3D Mesh, ArrayMesh Source, ArrayMesh Published,
        CollisionShape3D Contact, Transform3D MeshTransform, Transform3D RoomTransform, Vector2 HalfSize,
        int ChangedTriangles, int OutputVertices, float SourcePhysicsMaximumError,
        MeshInstance3D BabaiApron, Mesh BabaiApronMesh, Transform3D BabaiApronTransform);
    private readonly List<MosqueGroundingRecord> _mosqueGrounding = new();
    internal IReadOnlyList<MosqueGroundingRecord> MosqueGrounding => _mosqueGrounding;
    internal MosqueTerrainCutRecord? MosqueTerrainCut { get; private set; }

    // Mosque04: the old courtyard shared a single high origin while its real
    // ground rises six metres westwards. Keep every XZ footprint and owner;
    // individual masonry spans follow that slope at normal courtyard height.
    private void GroundMosqueCourtyard(Node3D complex)
    {
        var contact = GetNode<CollisionShape3D>(
            "Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainCollision/AgentB_TerrainFaces");
        if (contact.Shape is not ConcavePolygonShape3D terrain)
            throw new InvalidOperationException("Mosque courtyard needs its actual existing terrain faces.");
        var worldFaces = terrain.GetFaces().Select(p => contact.GlobalTransform * p).ToArray();
        foreach (var name in new[] { "MosqueYardWest", "MosqueYardNorth", "MosqueYardSouth",
            "MosqueYardGateLeft", "MosqueYardGateRight", "MosqueYardGatePostLeft", "MosqueYardGatePostRight" })
        {
            var mesh = complex.GetNode<MeshInstance3D>(name);
            if (mesh.Mesh is not BoxMesh box || mesh.GlobalBasis.Y.Normalized().Dot(Vector3.Up) < .99999f)
                throw new InvalidOperationException("Unexpected courtyard owner: " + name);
            var size = box.Size;
            var transform = mesh.GlobalTransform;
            var inverse = transform.AffineInverse();
            var localFaces = worldFaces.Select(p => inverse * p).ToArray();
            var alongX = size.X >= size.Z;
            var length = alongX ? size.X : size.Z;
            var sections = length <= 1.11f ? 1 : Mathf.CeilToInt(length / .75f);
            var spans = new List<MosqueGroundSpan>(sections);
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            for (var section = 0; section < sections; section++)
            {
                var from = Mathf.Lerp(-length * .5f, length * .5f, section / (float)sections);
                var to = Mathf.Lerp(-length * .5f, length * .5f, (section + 1f) / sections);
                var min = new Vector3(alongX ? from : -size.X * .5f, 0, alongX ? -size.Z * .5f : from);
                var max = new Vector3(alongX ? to : size.X * .5f, 0, alongX ? size.Z * .5f : to);
                var ground = MosqueTerrainBounds(localFaces, min, max);
                min.Y = ground.Minimum - .05f / transform.Basis.Y.Length();
                max.Y = ground.Maximum + size.Y;
                using var stone = new BoxMesh { Size = max - min };
                surface.AppendFrom(stone, 0, new Transform3D(Basis.Identity, (min + max) * .5f));
                spans.Add(new(min, max, ground.Minimum, ground.Maximum, ground.Area, ground.Triangles));
            }
            mesh.Mesh = surface.Commit();
            mesh.SetMeta("mosqueCourtyardGrounded", true);
            mesh.SetMeta("groundingSource", contact.GetPath().ToString());
            mesh.SetMeta("groundingPolicy", "same XZ owner; short terrain-supported masonry spans; existing late authored-triangle contact");
            _mosqueGrounding.Add(new(mesh, size, transform, size.Y, false, spans.ToArray()));
        }
        GroundMosqueSnowBank(complex.GetNode<MeshInstance3D>("MosqueYardSnowBank"), worldFaces);
    }

    private static (float Minimum, float Maximum, double Area, int Triangles) MosqueTerrainBounds(
        Vector3[] faces, Vector3 min, Vector3 max)
    {
        var low = float.PositiveInfinity;
        var high = float.NegativeInfinity;
        double area = 0;
        var triangles = 0;
        for (var i = 0; i < faces.Length; i += 3)
        {
            var a = faces[i]; var b = faces[i + 1]; var c = faces[i + 2];
            if (Math.Max(a.X, Math.Max(b.X, c.X)) < min.X || Math.Min(a.X, Math.Min(b.X, c.X)) > max.X
                || Math.Max(a.Z, Math.Max(b.Z, c.Z)) < min.Z || Math.Min(a.Z, Math.Min(b.Z, c.Z)) > max.Z) continue;
            var polygon = new List<Vector3> { a, b, c };
            polygon = ClipFoundationTriangle(polygon, p => p.X - min.X);
            polygon = ClipFoundationTriangle(polygon, p => max.X - p.X);
            polygon = ClipFoundationTriangle(polygon, p => p.Z - min.Z);
            polygon = ClipFoundationTriangle(polygon, p => max.Z - p.Z);
            if (polygon.Count < 3) continue;
            double pieceArea = 0;
            for (var p = 0; p < polygon.Count; p++)
                pieceArea += (double)polygon[p].X * polygon[(p + 1) % polygon.Count].Z
                    - (double)polygon[(p + 1) % polygon.Count].X * polygon[p].Z;
            pieceArea = Math.Abs(pieceArea) * .5;
            if (pieceArea < 1e-8) continue;
            area += pieceArea; triangles++;
            foreach (var point in polygon) { low = Math.Min(low, point.Y); high = Math.Max(high, point.Y); }
        }
        var expected = (double)(max.X - min.X) * (max.Z - min.Z);
        if (!float.IsFinite(low) || Math.Abs(area - expected) > Math.Max(.0001, expected * .0001))
            throw new InvalidOperationException($"Incomplete courtyard terrain coverage {area}/{expected}.");
        return (low, high, area, triangles);
    }

    private void GroundMosqueSnowBank(MeshInstance3D mesh, Vector3[] worldFaces)
    {
        if (mesh.Mesh is not BoxMesh box) throw new InvalidOperationException("Missing retained courtyard snow bank.");
        var size = box.Size;
        var transform = mesh.GlobalTransform;
        var inverse = transform.AffineInverse();
        var localFaces = worldFaces.Select(p => inverse * p).ToArray();
        var min = new Vector3(-size.X * .5f, 0, -size.Z * .5f);
        var max = new Vector3(size.X * .5f, 0, size.Z * .5f);
        var bounds = MosqueTerrainBounds(localFaces, min, max);
        var along = Mathf.CeilToInt(size.X / .30f);
        const int across = 8;
        var points = new Vector3[along + 1, across + 1];
        for (var x = 0; x <= along; x++) for (var z = 0; z <= across; z++)
        {
            var px = Mathf.Lerp(min.X, max.X, x / (float)along);
            var pz = Mathf.Lerp(min.Z, max.Z, z / (float)across);
            var taper = Mathf.Clamp(Math.Min(px - min.X, max.X - px) / .65f, 0, 1);
            var rise = size.Y * Mathf.Sin(Mathf.Pi * z / across) * Mathf.SmoothStep(0, 1, taper);
            points[x, z] = new(px, MosqueTerrainHeight(localFaces, px, pz) + rise - .025f, pz);
        }
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = (b - a).Cross(c - a).Normalized();
            if (normal.Y < 0) { (b, c) = (c, b); normal = -normal; }
            // Godot front faces are clockwise. Supply the outward normal while
            // reversing the mathematical counter-clockwise winding.
            foreach (var point in new[] { a, c, b })
            {
                surface.SetNormal(normal); surface.SetUV(new Vector2(point.X, point.Z));
                surface.AddVertex(point);
            }
        }
        for (var x = 0; x < along; x++) for (var z = 0; z < across; z++)
        {
            Triangle(points[x, z], points[x + 1, z], points[x + 1, z + 1]);
            Triangle(points[x, z], points[x + 1, z + 1], points[x, z + 1]);
        }
        surface.Index();
        mesh.Mesh = surface.Commit();
        mesh.SetMeta("mosqueCourtyardGrounded", true);
        mesh.SetMeta("groundingPolicy", "retained snow footprint follows actual terrain; softly tapered edges; visual only");
        _mosqueGrounding.Add(new(mesh, size, transform, size.Y, true,
            new[] { new MosqueGroundSpan(min, max, bounds.Minimum, bounds.Maximum, bounds.Area, bounds.Triangles) }));
    }

    private static float MosqueTerrainHeight(Vector3[] faces, float x, float z)
    {
        for (var i = 0; i < faces.Length; i += 3)
        {
            var a = faces[i]; var b = faces[i + 1]; var c = faces[i + 2];
            var determinant = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Math.Abs(determinant) < 1e-9f) continue;
            var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / determinant;
            var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / determinant;
            if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
                return u * a.Y + v * b.Y + (1 - u - v) * c.Y;
        }
        throw new InvalidOperationException($"Missing actual courtyard terrain at {x}/{z}.");
    }

    private void ExcludeMosqueHallTerrain()
    {
        var room = _mosqueRoom!;
        var exterior = GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        var mesh = FindDescendants<MeshInstance3D>(exterior.GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Terrain_Main");
        var contact = exterior.GetNode<CollisionShape3D>("AgentB_TerrainCollision/AgentB_TerrainFaces");
        if (mesh.Mesh is not ArrayMesh original || original.GetSurfaceCount() != 1
            || contact.Shape is not ConcavePolygonShape3D terrain)
            throw new InvalidOperationException("Mosque terrain cut lost the existing single render/physics owner.");
        var toContact = contact.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var beforeVisible = original.GetFaces();
        var beforePhysical = terrain.GetFaces();
        if (beforeVisible.Length != beforePhysical.Length) throw new InvalidOperationException("Terrain owners disagree before mosque cut.");
        var priorError = 0f;
        for (var i = 0; i < beforeVisible.Length; i++)
            priorError = Math.Max(priorError, (toContact * beforeVisible[i]).DistanceTo(beforePhysical[i]));
        if (priorError > .0001f) throw new InvalidOperationException("Terrain owner transform mismatch before mosque cut: " + priorError);
        // Asymmetric west extension uses a centred cut frame, while the room origin/entrance stay stable.
        var cutFrame = new Node3D { Name = "MosqueTerrainCutFrame", Position = new(-.7f, 0, 0) };
        room.AddChild(cutFrame);
        var halfSize = new Vector2(6.20f, 4.85f); // Existing wall centre lines; the cut stays under those walls.
        var apron = FindDescendants<MeshInstance3D>(exterior.GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Single(node => node.Name == "Apron_BabaiYard");
        var apronMesh = apron.Mesh;
        var apronTransform = apron.GlobalTransform;
        var (result, changed, vertices) = AgentBAct1ExteriorLayer.ClipGroundFootprint(mesh, original, cutFrame, halfSize);
        if (changed == 0) { result.Dispose(); throw new InvalidOperationException("Mosque terrain cut removed no intersecting ground."); }
        mesh.Mesh = result;
        var published = result.GetFaces();
        terrain.SetFaces(published.Select(p => toContact * p).ToArray());
        // Do not overwrite the earlier home's metadata or call its apron/fence
        // fitting routine. Only this mesh and its existing paired shape change.
        mesh.SetMeta("mosqueRoomTerrainCut", room.GetPath().ToString());
        contact.SetMeta("mosqueRoomTerrainCut", room.GetPath().ToString());
        room.SetMeta("mosqueTerrainCutHalfSize", halfSize);
        MosqueTerrainCut = new(mesh, original, result, contact, mesh.GlobalTransform, cutFrame.GlobalTransform,
            halfSize, changed, vertices, priorError, apron, apronMesh, apronTransform);
        GD.Print($"act1-mosque-terrain-cut: affectedTriangles={changed} publishedVertices={published.Length} physicsVertices={terrain.GetFaces().Length} sameOwner=true");
    }

    private void CompleteMosqueCourtyardContacts(Node3D complex, StaticBody3D proxy)
    {
        foreach (var name in new[] { "MosqueYardGatePostLeft", "MosqueYardGatePostRight" })
        {
            var mesh = complex.GetNode<MeshInstance3D>(name);
            if (proxy.GetChildren().OfType<CollisionShape3D>().Any(shape => shape.HasMeta("authoredSourceMesh")
                && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString()))
                throw new InvalidOperationException("Duplicate courtyard post contact: " + name);
            proxy.AddChild(AuthoredSurfaceContact(proxy, mesh));
        }
    }
}
