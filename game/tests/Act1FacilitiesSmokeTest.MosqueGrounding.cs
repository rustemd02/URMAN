using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private void CheckMosqueGrounding()
    {
        var pose = _player.CapturePortableTransform();
        var state = _bridge.SelectRuntimeState().GetRawText();
        var records = _world.MosqueGrounding;
        var cut = _world.MosqueTerrainCut ?? throw new InvalidOperationException("No mosque terrain publication.");
        var failures = new List<string>();
        var observations = new List<object>();
        var terrainRayOccluders = new HashSet<string>(StringComparer.Ordinal);
        var allContacts = _world.FindChildren("*", nameof(CollisionShape3D), true, false).OfType<CollisionShape3D>().ToArray();
        var terrainBody = cut.Contact.GetParent<StaticBody3D>();
        var space = _world.GetWorld3D().DirectSpaceState;
        var exclude = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
        void Require(bool condition, string message) { if (!condition) failures.Add(message); }
        object V(Vector3 p) => new { x = p.X, y = p.Y, z = p.Z };
        Node? HitOwner(global::Godot.Collections.Dictionary hit)
        {
            if (hit.Count == 0 || hit["collider"].AsGodotObject() is not CollisionObject3D body) return null;
            return body.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
        }
        global::Godot.Collections.Dictionary TerrainHit(Vector3 from, Vector3 to)
        {
            var terrainExcluded = new global::Godot.Collections.Array<Rid> { _player.GetRid() };
            using var ray = PhysicsRayQueryParameters3D.Create(from, to, 1, terrainExcluded);
            var hit = space.IntersectRay(ray);
            for (var retry = 0; retry < 16 && hit.Count > 0 && HitOwner(hit) != cut.Contact; retry++)
            {
                if (hit["collider"].AsGodotObject() is not CollisionObject3D other || other == terrainBody) break;
                terrainRayOccluders.Add(other.GetPath().ToString());
                terrainExcluded.Add(other.GetRid());
                ray.Exclude = terrainExcluded;
                hit = space.IntersectRay(ray);
            }
            Require(hit.Count == 0 || HitOwner(hit) == cut.Contact, "Terrain-owner ray could not inspect past another shape within its bounded query.");
            return hit;
        }
        float TerrainY(Vector3 point, string label)
        {
            var hit = TerrainHit(point with { Y = 30 }, point with { Y = -10 });
            var owns = hit.Count > 0 && HitOwner(hit) == cut.Contact;
            Require(owns, label + ": ray did not reach the exact existing terrain shape.");
            return owns ? hit["position"].AsVector3().Y : float.NaN;
        }

        Require(records.Count == 8 && records.Select(r => r.Mesh.GetPath().ToString()).Distinct().Count() == 8,
            "Grounding must cover all five walls, both posts and the retained snow bank once.");
        foreach (var record in records)
        {
            var mesh = record.Mesh;
            var original = record.OriginalSize;
            var bounds = mesh.Mesh.GetAabb();
            var faces = MosqueRenderedFaces(mesh.Mesh);
            var publishedContactFaces = mesh.Mesh.GetFaces();
            var sourcePath = mesh.GetPath().ToString();
            var contacts = allContacts.Where(c => !c.Disabled && c.HasMeta("authoredSourceMesh")
                && c.GetMeta("authoredSourceMesh").AsString() == sourcePath).ToArray();
            Require(mesh.GlobalTransform == record.OriginalTransform, mesh.Name + ": original node pose moved.");
            Require(Math.Abs(bounds.Position.X + original.X * .5f) < .0001f && Math.Abs(bounds.End.X - original.X * .5f) < .0001f
                && Math.Abs(bounds.Position.Z + original.Z * .5f) < .0001f && Math.Abs(bounds.End.Z - original.Z * .5f) < .0001f,
                mesh.Name + ": original XZ footprint changed.");
            Require(contacts.Length == (record.VisualOnly ? 0 : 1), mesh.Name + ": incorrect paired-contact count.");
            float? geometryError = null;
            if (!record.VisualOnly && contacts.Length == 1 && contacts[0].Shape is ConcavePolygonShape3D shape)
            {
                var physical = shape.GetFaces();
                Require(physical.Length == publishedContactFaces.Length, mesh.Name + ": physical/published triangle counts differ.");
                var maxError = 0f;
                for (var i = 0; i < Math.Min(physical.Length, publishedContactFaces.Length); i++)
                    maxError = Math.Max(maxError, (contacts[0].GlobalTransform * physical[i]).DistanceTo(mesh.GlobalTransform * publishedContactFaces[i]));
                geometryError = maxError;
                Require(maxError < .0001f, mesh.Name + ": paired collision differs from the mesh's actual published triangle representation.");
                Require(contacts[0].GetParent() == _mosque.GetParent().GetNode<StaticBody3D>("MosqueCollisionProxy"),
                    mesh.Name + ": courtyard contact acquired another owner.");
            }
            else if (!record.VisualOnly) Require(false, mesh.Name + ": contact is not the expected triangle surface.");
            var samples = new List<object>();
            if (record.VisualOnly)
            {
                var minimumGap = float.PositiveInfinity;
                var maximumGap = float.NegativeInfinity;
                foreach (var vertex in faces.Distinct())
                {
                    var point = mesh.ToGlobal(vertex);
                    var ground = TerrainY(point, mesh.Name + " snow");
                    var gap = point.Y - ground;
                    minimumGap = Math.Min(minimumGap, gap); maximumGap = Math.Max(maximumGap, gap);
                    Require(float.IsFinite(gap) && gap >= -.027f && gap <= record.HeightAboveTerrain + .001f,
                        mesh.Name + ": snow surface leaves its original shallow terrain envelope.");
                }
                samples.Add(new { distinctSurfaceVertices = faces.Distinct().Count(), minimumGap, maximumGap, visualOnly = true });
            }
            else foreach (var span in record.Spans)
            {
                var center = (span.Min + span.Max) * .5f;
                var minWorld = mesh.ToGlobal(span.Min);
                var maxWorld = mesh.ToGlobal(span.Max);
                var grounds = new List<float>();
                foreach (var local in new[] { center, new(span.Min.X, 0, span.Min.Z), new(span.Min.X, 0, span.Max.Z),
                    new(span.Max.X, 0, span.Min.Z), new(span.Max.X, 0, span.Max.Z) })
                {
                    var ground = TerrainY(mesh.ToGlobal(local), mesh.Name + " span");
                    grounds.Add(ground);
                    Require(float.IsFinite(ground) && minWorld.Y <= ground - .045f, mesh.Name + ": masonry bottom floats above actual terrain.");
                    Require(float.IsFinite(ground) && maxWorld.Y >= ground + record.HeightAboveTerrain - .002f,
                        mesh.Name + ": masonry top is buried below its normal courtyard height.");
                }
                var normalLocal = original.X < original.Z ? Vector3.Right : Vector3.Back;
                var halfThickness = original.X < original.Z ? original.X * .5f : original.Z * .5f;
                var at = mesh.ToGlobal(center + normalLocal * halfThickness);
                at.Y = TerrainY(at, mesh.Name + " side") + record.HeightAboveTerrain * .5f;
                var normal = (mesh.GlobalBasis * normalLocal).Normalized();
                using var sideRay = PhysicsRayQueryParameters3D.Create(at + normal * .12f, at - normal * .12f, 3, exclude);
                var hit = space.IntersectRay(sideRay);
                var owner = HitOwner(hit);
                var paired = owner?.HasMeta("authoredSourceMesh") == true && owner.GetMeta("authoredSourceMesh").AsString() == sourcePath;
                Require(paired, mesh.Name + ": real side ray misses this masonry contact.");
                samples.Add(new { localMin = V(span.Min), localMax = V(span.Max), sourceTerrainMin = span.TerrainMin,
                    sourceTerrainMax = span.TerrainMax, span.CoveredArea, span.TerrainTriangles, actualGroundY = grounds,
                    sideOwner = owner?.GetPath().ToString(), sidePoint = hit.Count == 0 ? null : V(hit["position"].AsVector3()), paired });
            }
            var renderCacheError = faces.Length == publishedContactFaces.Length
                ? faces.Zip(publishedContactFaces, (a, b) => (mesh.GlobalBasis * (a - b)).Length()).DefaultIfEmpty(0).Max() : float.NaN;
            observations.Add(new { mesh = sourcePath, originalSize = V(original), visibleVertices = faces.Length,
                publishedContactVertices = publishedContactFaces.Length, renderCacheError, geometryError,
                contacts = contacts.Select(c => c.GetPath().ToString()).ToArray(), record.VisualOnly, samples });
        }

        var published = MosqueRenderedFaces(cut.Published);
        var source = MosqueRenderedFaces(cut.Source);
        var contactPublication = cut.Published.GetFaces();
        var physicalTerrain = ((ConcavePolygonShape3D)cut.Contact.Shape).GetFaces();
        var toRoom = cut.RoomTransform.AffineInverse() * cut.MeshTransform;
        var sourceInsideArea = 0d;
        var sourceArea = 0d;
        var publishedInsideArea = 0d;
        var publishedArea = 0d;
        var outsidePreserved = 0;
        var outsideMissing = 0;
        var triangleCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        string Key(Vector3 a, Vector3 b, Vector3 c)
        {
            string Point(Vector3 p) => BitConverter.SingleToInt32Bits(p.X) + "/" + BitConverter.SingleToInt32Bits(p.Y) + "/" + BitConverter.SingleToInt32Bits(p.Z);
            var x = Point(a); var y = Point(b); var z = Point(c);
            // Cyclic rotations preserve winding; reversed vertices must not
            // pass as an unchanged outward-facing surface.
            return new[] { x + ";" + y + ";" + z, y + ";" + z + ";" + x, z + ";" + x + ";" + y }
                .Min(StringComparer.Ordinal)!;
        }
        var originalKeys = Enumerable.Range(0, source.Length / 3).Select(i => Key(source[i * 3], source[i * 3 + 1], source[i * 3 + 2])).ToHashSet(StringComparer.Ordinal);
        var candidateParents = new List<int>();
        for (var i = 0; i < published.Length; i += 3)
        {
            var key = Key(published[i], published[i + 1], published[i + 2]);
            triangleCounts[key] = triangleCounts.GetValueOrDefault(key) + 1;
            publishedInsideArea += MosqueTriangleInsideArea(toRoom * published[i], toRoom * published[i + 1], toRoom * published[i + 2], cut.HalfSize);
            publishedArea += MosqueTriangleArea(toRoom * published[i], toRoom * published[i + 1], toRoom * published[i + 2]);
        }
        for (var i = 0; i < source.Length; i += 3)
        {
            var a = toRoom * source[i]; var b = toRoom * source[i + 1]; var c = toRoom * source[i + 2];
            var inside = MosqueTriangleInsideArea(a, b, c, cut.HalfSize);
            sourceInsideArea += inside; sourceArea += MosqueTriangleArea(a, b, c);
            var whollyOutside = new[] { a, b, c }.All(p => p.X <= -cut.HalfSize.X)
                || new[] { a, b, c }.All(p => p.X >= cut.HalfSize.X)
                || new[] { a, b, c }.All(p => p.Z <= -cut.HalfSize.Y)
                || new[] { a, b, c }.All(p => p.Z >= cut.HalfSize.Y);
            if (!whollyOutside) { candidateParents.Add(i); continue; }
            var key = Key(source[i], source[i + 1], source[i + 2]);
            if (triangleCounts.GetValueOrDefault(key) > 0) { triangleCounts[key]--; outsidePreserved++; }
            else outsideMissing++;
        }
        var fragments = new List<object>();
        var fragmentFailures = 0;
        for (var i = 0; i < published.Length; i += 3)
        {
            if (originalKeys.Contains(Key(published[i], published[i + 1], published[i + 2]))) continue;
            var observation = MosqueFragmentParent(published[i], published[i + 1], published[i + 2], source, candidateParents);
            if (!observation.Fits) fragmentFailures++;
            fragments.Add(new { triangle = i / 3, observation.Fits, observation.Parent, observation.PlaneError,
                observation.MinBarycentric, observation.MaxBarycentric, observation.WindingAligned });
        }
        Require(fragmentFailures == 0, "A clipped terrain fragment changed source plane, source bounds or winding.");
        var terrainError = 0f;
        Require(contactPublication.Length == physicalTerrain.Length, "Terrain GetFaces publication/physics counts differ.");
        for (var i = 0; i < Math.Min(contactPublication.Length, physicalTerrain.Length); i++)
            terrainError = Math.Max(terrainError, (cut.Mesh.GlobalTransform * contactPublication[i]).DistanceTo(cut.Contact.GlobalTransform * physicalTerrain[i]));
        var terrainRenderCacheError = published.Length == contactPublication.Length
            ? published.Zip(contactPublication, (a, b) => (cut.Mesh.GlobalBasis * (a - b)).Length()).DefaultIfEmpty(0).Max() : float.NaN;
        Require(terrainError < .0001f && cut.SourcePhysicsMaximumError < .0001f, "Terrain render/physics transform coupling changed.");
        Require(sourceInsideArea > 90 && publishedInsideArea < .00001, "Terrain triangles still enter the occupied mosque footprint.");
        Require(Math.Abs((sourceArea - sourceInsideArea) - publishedArea) < .0002 && outsideMissing == 0,
            "Terrain outside the mosque footprint was lost or changed.");
        Require(cut.Mesh.Mesh == cut.Published && cut.Mesh.GlobalTransform == cut.MeshTransform
            && _mosque.GlobalTransform == cut.RoomTransform, "Terrain publication changed its owner or the mosque floor.");
        Require(cut.Source.SurfaceGetMaterial(0) == cut.Published.SurfaceGetMaterial(0), "Terrain cut changed the existing material.");
        Require(cut.BabaiApron.Mesh == cut.BabaiApronMesh && cut.BabaiApron.GlobalTransform == cut.BabaiApronTransform,
            "Mosque cut changed the Babai yard apron.");

        var roomSamples = new List<object>();
        var carpet = _mosque.GetNode<MeshInstance3D>("MosquePrayerCarpet");
        var carpetBody = _mosque.GetNode<StaticBody3D>("MosquePrayerCarpetBody");
        var carpetContact = carpetBody.GetNode<CollisionShape3D>("Contact");
        var carpetBounds = carpet.Mesh.GetAabb();
        var carpetTop = carpet.ToGlobal(carpetBounds.GetCenter() + Vector3.Up * carpetBounds.Size.Y * .5f).Y;
        Require(carpetContact.Shape is BoxShape3D carpetShape && carpetShape.Size.IsEqualApprox(carpetBounds.Size)
            && carpetContact.GlobalTransform.IsEqualApprox(carpet.GlobalTransform)
            && !carpetContact.Disabled && carpetBody.CollisionLayer == 2
            && carpetBody.GetMeta("authoredSourceMesh", "").AsString() == carpet.GetPath().ToString(),
            "The existing visible carpet and its single active thin support disagree.");
        foreach (var local in new[] { new Vector3(1.2f, 0, 0), new(-3.5f, 0, 0), new(-3.85f, 0, -2.55f), new(-4.075f, 0, 2.8f) })
        {
            var at = _mosque.ToGlobal(local);
            using var floorRay = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .30f, at - Vector3.Up * .30f, 3, exclude);
            var hit = space.IntersectRay(floorRay);
            var body = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as Node;
            var floor = body == carpetBody && Math.Abs(hit["position"].AsVector3().Y - carpetTop) < .0001f;
            Require(floor, "West hall point does not rest on the actual visible carpet top: " + local);
            using var timberRay = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .30f, at - Vector3.Up * .30f, 3,
                new global::Godot.Collections.Array<Rid> { _player.GetRid(), carpetBody.GetRid() });
            var timberHit = space.IntersectRay(timberRay);
            var timberBody = timberHit.Count == 0 ? null : timberHit["collider"].AsGodotObject() as Node;
            var timberPreserved = timberBody == _mosque.GetNode<StaticBody3D>("MosqueTimberFloorBody")
                && Math.Abs(timberHit["position"].AsVector3().Y - at.Y) < .0001f;
            Require(timberPreserved, "The original timber floor beneath the thin carpet changed: " + local);
            var soilHit = TerrainHit(at + Vector3.Up * 3f, at - Vector3.Up * 5f);
            var soilAbsent = soilHit.Count == 0;
            Require(soilAbsent, "Original terrain still crosses the occupied hall: " + local);
            Require(_player.CanStandAt(at + Vector3.Up * .035f), "West hall point lacks a clear standing capsule: " + local);
            roomSamples.Add(new { local = V(local), world = V(at), floor, floorOwner = body?.GetPath().ToString(), carpetTop,
                timberPreserved, timberOwner = timberBody?.GetPath().ToString(),
                timberRayExcludesOnlyPlayerAndCarpet = true, soilAbsent });
        }
        Require(pose.Equals(_player.CapturePortableTransform()) && state == _bridge.SelectRuntimeState().GetRawText(),
            "Read-only mosque proof changed player pose or gameplay state.");
        var path = Path.Combine(Output, "mosque-courtyard-terrain-geometry.json");
        var payload = JsonSerializer.Serialize(new { schemaVersion = 1, passed = failures.Count == 0,
            geometryChangedByObservation = false, runtimeTerrainOwner = cut.Contact.GetPath().ToString(),
            renderGeometrySource = "SurfaceGetArrays plus indices", contactGeometrySource = "published GetFaces; Godot TriangleMesh snappedf grid 0.0001m",
            renderedAppearance = "separate actual frame review required", observations, terrainRayOccluders,
            terrain = new { sourceVertices = source.Length, publishedVertices = published.Length,
                contactPublicationVertices = contactPublication.Length, physicalVertices = physicalTerrain.Length, terrainRenderCacheError,
                cut.ChangedTriangles, cut.OutputVertices, sourceInsideArea, publishedInsideArea, sourceArea, publishedArea,
                outsidePreserved, outsideMissing, fragmentFailures, fragments, terrainError, cut.SourcePhysicsMaximumError, roomSamples }, failures },
            new JsonSerializerOptions { WriteIndented = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        using (var file = new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file)) writer.Write(payload);
        _events.Add(new { kind = "mosque-courtyard-terrain", path, failures = failures.ToArray(), actualGroundingOwners = records.Count });
        Check(failures.Count == 0, "all courtyard owners meet terrain and the entire occupied hall shares one exact render/physics terrain cut: " + string.Join(" | ", failures.Take(4)));
    }

    private static Vector3[] MosqueRenderedFaces(Mesh mesh)
    {
        var result = new List<Vector3>();
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            var arrays = mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var value = arrays[(int)Mesh.ArrayType.Index];
            var indices = value.VariantType == Variant.Type.Nil ? Array.Empty<int>() : value.AsInt32Array();
            if (indices.Length == 0) result.AddRange(vertices);
            else foreach (var index in indices) result.Add(vertices[index]);
        }
        if (result.Count % 3 != 0) throw new InvalidOperationException("Rendered mosque mesh lost its triangle topology.");
        return result.ToArray();
    }

    private static double MosqueTriangleArea(Vector3 a, Vector3 b, Vector3 c)
        => Math.Abs(((double)b.X - a.X) * ((double)c.Z - a.Z) - ((double)c.X - a.X) * ((double)b.Z - a.Z)) * .5;

    private static double MosqueTriangleInsideArea(Vector3 a, Vector3 b, Vector3 c, Vector2 half)
    {
        var polygon = new List<(double X, double Z)> { (a.X, a.Z), (b.X, b.Z), (c.X, c.Z) };
        foreach (var distance in new Func<(double X, double Z), double>[] { p => p.X + half.X, p => half.X - p.X,
            p => p.Z + half.Y, p => half.Y - p.Z })
        {
            if (polygon.Count == 0) return 0;
            var next = new List<(double X, double Z)>();
            var previous = polygon[^1]; var prior = distance(previous);
            foreach (var current in polygon)
            {
                var now = distance(current);
                if ((now >= 0) != (prior >= 0))
                {
                    var t = prior / (prior - now);
                    next.Add((previous.X + (current.X - previous.X) * t, previous.Z + (current.Z - previous.Z) * t));
                }
                if (now >= 0) next.Add(current);
                previous = current; prior = now;
            }
            polygon = next;
        }
        double result = 0;
        for (var i = 1; i + 1 < polygon.Count; i++)
            result += Math.Abs((polygon[i].X - polygon[0].X) * (polygon[i + 1].Z - polygon[0].Z)
                - (polygon[i + 1].X - polygon[0].X) * (polygon[i].Z - polygon[0].Z)) * .5;
        return result;
    }

    private static (bool Fits, int Parent, double PlaneError, double MinBarycentric, double MaxBarycentric, bool WindingAligned)
        MosqueFragmentParent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3[] source, List<int> candidates)
    {
        var best = (Fits: false, Parent: -1, PlaneError: double.PositiveInfinity,
            MinBarycentric: double.NegativeInfinity, MaxBarycentric: double.PositiveInfinity, WindingAligned: false);
        var bestScore = double.PositiveInfinity;
        foreach (var i in candidates)
        {
            var a = source[i]; var b = source[i + 1]; var c = source[i + 2];
            var ex = (double)b.X - a.X; var ey = (double)b.Y - a.Y; var ez = (double)b.Z - a.Z;
            var fx = (double)c.X - a.X; var fy = (double)c.Y - a.Y; var fz = (double)c.Z - a.Z;
            var nx = ey * fz - ez * fy; var ny = ez * fx - ex * fz; var nz = ex * fy - ey * fx;
            var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-12) continue;
            var drop = Math.Abs(nx) >= Math.Abs(ny) && Math.Abs(nx) >= Math.Abs(nz) ? 0 : Math.Abs(ny) >= Math.Abs(nz) ? 1 : 2;
            var axis0 = (drop + 1) % 3; var axis1 = (drop + 2) % 3;
            var eu = (double)b[axis0] - a[axis0]; var ev = (double)b[axis1] - a[axis1];
            var fu = (double)c[axis0] - a[axis0]; var fv = (double)c[axis1] - a[axis1];
            var denominator = eu * fv - ev * fu;
            var plane = 0d; var minBary = double.PositiveInfinity; var maxBary = double.NegativeInfinity;
            foreach (var p in new[] { p0, p1, p2 })
            {
                var du = (double)p[axis0] - a[axis0]; var dv = (double)p[axis1] - a[axis1];
                var u = (du * fv - dv * fu) / denominator; var v = (eu * dv - ev * du) / denominator;
                plane = Math.Max(plane, Math.Abs(((double)p.X - a.X) * nx + ((double)p.Y - a.Y) * ny + ((double)p.Z - a.Z) * nz) / length);
                minBary = Math.Min(minBary, Math.Min(u, v)); maxBary = Math.Max(maxBary, u + v);
            }
            var ax = (double)p1.X - p0.X; var ay = (double)p1.Y - p0.Y; var az = (double)p1.Z - p0.Z;
            var bx = (double)p2.X - p0.X; var by = (double)p2.Y - p0.Y; var bz = (double)p2.Z - p0.Z;
            var winding = (ay * bz - az * by) * nx + (az * bx - ax * bz) * ny + (ax * by - ay * bx) * nz > 0;
            var fits = winding && plane <= .00003 && minBary >= -.0001 && maxBary <= 1.0001;
            var score = Math.Max(plane / .00003, Math.Max(-minBary / .0001, (maxBary - 1) / .0001)) + (winding ? 0 : 1);
            if (score < bestScore) { bestScore = score; best = (fits, i / 3, plane, minBary, maxBary, winding); }
            if (fits) return (true, i / 3, plane, minBary, maxBary, true);
        }
        return best;
    }

    private async System.Threading.Tasks.Task WalkMosqueWestHall()
    {
        await WalkTo(_mosque.ToGlobal(new(-3.5f, 0, 0)), "cross the complete mosque hall to the formerly buried west carpet");
        await Capture("09b_mosque_west_hall", _mosque.ToGlobal(new(-4.70f, .65f, -1.10f)));
        await WalkTo(_mosque.ToGlobal(new(-3.85f, 0, -2.55f)), "reach the existing west reading bench on the actual timber floor");
        await Capture("09c_mosque_reading_bench", _mosque.ToGlobal(new(-3.85f, .65f, -3.26f)));
        await WalkTo(_mosque.ToGlobal(new(-3.5f, 0, 0)), "return through the clear west aisle");
        await WalkTo(_mosque.ToGlobal(new(-4.075f, 0, 2.8f)), "approach the existing mosque bookshelf without climbing soil");
        CheckMosqueShelfBooks();
        await Capture("09d_mosque_bookshelf", _mosque.ToGlobal(new(-4.075f, 1.0f, 3.63f)));
        await WalkTo(_mosque.ToGlobal(new(-3.5f, 0, 0)), "leave the shelf along the same supported aisle");
        await WalkTo(_mosque.ToGlobal(new(1.20f, 0, 0)), "return across the actual hall to Timur and the vestibule");
    }
}
