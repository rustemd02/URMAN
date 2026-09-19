using System.Reflection;
using System.Text.Json;
using Godot;

namespace Urman.Godot.Tests;

public partial class AddressWorldSmokeTest
{
    private sealed record VergeTriangle(Vector3 A, Vector3 B, Vector3 C)
    {
        public Vector3 Center => (A + B + C) / 3;
        public Vector3 Cross => (B - A).Cross(C - A);
    }

    // Actual world contacts only. No isolated replacement body, moved source,
    // collider exclusion, actor placement, rendered fixture or generated route.
    private void VerifyInclinedVergeContacts()
    {
        var registry = _world.AddressRegistry!;
        var beforePose = _player.CapturePortableTransform();
        var beforeState = _bridge.SelectRuntimeState().GetRawText();
        string RegistrySnapshot() => JsonSerializer.Serialize(new { registry.Buildings, registry.Parcels,
            registry.Addresses, registry.AccessPoints, registry.AddressAliases });
        var beforeRegistry = RegistrySnapshot();
        var started = Time.GetTicksMsec();
        var contacts = Descendants(_world).OfType<CollisionShape3D>()
            .Where(s => s.HasMeta("contactPolicy") && s.GetMeta("contactPolicy").AsString() == "authored-solid-inclined-verge")
            .OrderBy(s => s.GetPath().ToString(), StringComparer.Ordinal).ToArray();
        Require(contacts.Length is >= 3 and <= 512, "bounded actual inclined-verge contact census exists");
        var allSourceContacts = Descendants(_world).OfType<CollisionShape3D>()
            .Where(s => s.HasMeta("authoredSourceMesh")).GroupBy(s => s.GetMeta("authoredSourceMesh").AsString())
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var sourceTransforms = contacts.Select(s => _world.GetNode<MeshInstance3D>(s.GetMeta("authoredSourceMesh").AsString()))
            .Distinct().ToDictionary(m => m, m => m.GlobalTransform);
        var census = new List<object>();
        foreach (var shape in contacts)
        {
            var sourcePath = shape.GetMeta("authoredSourceMesh").AsString();
            var source = _world.GetNode<MeshInstance3D>(sourcePath);
            var sameSource = allSourceContacts[sourcePath];
            var good = shape.Shape is ConvexPolygonShape3D && sameSource.Length == 1
                && shape.GetParent() is StaticBody3D { CollisionLayer: 2 }
                && source.IsVisibleInTree() && !shape.Disabled;
            Check(good, "inclined verge has one existing-owner convex contact: " + sourcePath);
            census.Add(new { source = sourcePath, shape = shape.GetPath().ToString(),
                shapeClass = shape.Shape?.GetClass().ToString(), sameSourceContacts = sameSource.Select(s => s.GetPath().ToString()).ToArray(),
                visible = source.IsVisibleInTree(), shape.Disabled, correctOwner = good });
        }
        var selected = contacts.OrderBy(s => s.GetPath().ToString().Contains("/ReturnStreetDistantLowFacade/", StringComparison.Ordinal)
                && s.Name.ToString().Contains("VergeRight", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(s => s.GetPath().ToString(), StringComparer.Ordinal)
            .GroupBy(s => s.GetParent().GetParent().GetPath().ToString(), StringComparer.Ordinal)
            .Select(g => g.First()).Take(6).ToArray();
        Require(selected.Length == 6, "actual rays cover six different authored placements");
        var records = new List<object>();
        foreach (var shape in selected)
        {
            var source = _world.GetNode<MeshInstance3D>(shape.GetMeta("authoredSourceMesh").AsString());
            try
            {
                var faces = VergePublishedTriangles(source);
                var center = source.GlobalTransform * source.Mesh!.GetAabb().GetCenter();
                var normal = source.GlobalBasis.Z.Normalized();
                var front = faces.Where(t => Math.Abs(t.Cross.Normalized().Dot(normal)) > .85f
                        && (t.Center - center).Dot(normal) > 0)
                    .OrderByDescending(t => t.Cross.LengthSquared()).Take(2).ToArray();
                Check(front.Length == 2, "two actual beam face samples exist: " + source.GetPath());
                var rays = new List<object>();
                foreach (var triangle in front)
                {
                    var outward = triangle.Cross.Normalized();
                    if (outward.Dot(triangle.Center - center) < 0) outward = -outward;
                    var from = triangle.Center + outward * .035f;
                    var to = triangle.Center - outward * .015f;
                    var actual = VergeTriangleDistance(faces, from, to);
                    var hit = VergePhysicalRay(from, to);
                    var matches = actual is not null && hit.Shape == shape && hit.Point is { } point
                        && Math.Abs(from.DistanceTo(point) - actual.Value) <= .003f;
                    Check(matches, "ray through actual inclined timber hits its own solid: " + source.GetPath());
                    rays.Add(new { kind = "inside-visible-beam", from = P(from), to = P(to), actualTriangleDistance = actual,
                        physical = VergeHitEvidence(hit), matched = matches });
                }
                var (frame, bounds) = Act1ConnectedWorld.AuthoredKitMeshBounds(source);
                var centre = bounds.GetCenter();
                var outsideClear = 0;
                // Grid is inside the old upright envelope but outside the real
                // triangles. Other real owners are recorded, never excluded.
                foreach (var x in new[] { -.42f, -.21f, 0, .21f, .42f })
                foreach (var y in new[] { -.42f, -.21f, 0, .21f, .42f })
                {
                    var point = centre + new Vector3(bounds.Size.X * x, bounds.Size.Y * y, 0);
                    var from = frame * (point + Vector3.Back * (bounds.Size.Z * .5f + .035f));
                    var to = frame * (point - Vector3.Back * (bounds.Size.Z * .5f + .035f));
                    if (VergeTriangleDistance(faces, from, to) is not null) continue;
                    var hit = VergePhysicalRay(from, to);
                    var phantom = hit.Shape == shape || hit.Source == source.GetPath().ToString();
                    Check(!phantom, "air outside inclined timber is not filled by its proxy: " + source.GetPath());
                    if (!hit.HasHit) outsideClear++;
                    rays.Add(new { kind = "outside-visible-beam-inside-old-envelope", from = P(from), to = P(to),
                        actualTriangleHit = false, physical = VergeHitEvidence(hit), phantomContact = phantom });
                }
                Check(outsideClear > 0, "at least one measured empty ray crosses the former upright envelope: " + source.GetPath());
                records.Add(new { source = source.GetPath().ToString(), contact = shape.GetPath().ToString(),
                    triangleCount = faces.Length, outsideClear, sourceTransform = source.GlobalTransform.ToString(), rays });
            }
            catch (Exception error)
            {
                Check(false, "inclined-verge diagnostic could not complete: " + source.GetPath() + ": " + error.Message);
                records.Add(new { source = source.GetPath().ToString(), error = error.ToString(), coverageComplete = false });
            }
        }
        var returnWest = VerifyReturnWestVisibleGable();
        var unchanged = beforePose.Equals(_player.CapturePortableTransform())
            && beforeState == _bridge.SelectRuntimeState().GetRawText() && beforeRegistry == RegistrySnapshot()
            && sourceTransforms.All(p => p.Key.GlobalTransform == p.Value);
        Check(unchanged, "verge contact queries preserve player, progress, registry and all source transforms");
        var payload = JsonSerializer.Serialize(new { schemaVersion = 1,
            loadedModule = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,
            scope = "verge-contacts", auditRun = false, acceptance = false, geometryChangedByDiagnostic = false,
            contactCount = contacts.Length, testedPlacements = selected.Length, sourceStateUnchanged = unchanged,
            elapsedMilliseconds = Time.GetTicksMsec() - started, census, records, returnWest }, new JsonSerializerOptions { WriteIndented = true });
        var destination = Path.Combine(_output, "address-verge-contacts.json");
        var temporary = destination + ".writing";
        using (var file = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file)) { writer.Write(payload); writer.Flush(); file.Flush(true); }
        File.Move(temporary, destination, false);
    }

    private object VerifyReturnWestVisibleGable()
    {
        var source = Descendants(_world).OfType<Node3D>().Single(n => n.Name == "ReturnWestBanyaSilhouette");
        var leaf = Descendants(source).OfType<MeshInstance3D>().Single(m => m.Name.ToString().Contains("SeniEntry_Door0_Leaf_LOD0", StringComparison.Ordinal));
        var h024 = Descendants(_world).OfType<Node3D>().Single(n => n.Name == "ReturnStreetDistantLowFacade");
        var gable = Descendants(h024).OfType<MeshInstance3D>().Single(m => m.Name == "DwellingFacade_Front_BoardedGable_LOD0");
        var bounds = leaf.Mesh!.GetAabb(); var center = leaf.GlobalTransform * bounds.GetCenter();
        var outward = leaf.GlobalBasis * (bounds.Size.X < bounds.Size.Z ? Vector3.Right : Vector3.Back);
        outward.Y = 0; outward = outward.Normalized();
        using var walker = new AddressWalkProbe(_world);
        var supported = walker.TrySupport(Act1ConnectedWorld.AddressGround(center + outward * 1.05f), out var feet);
        Check(supported, "ReturnWest comparison still uses the actual supported approach");
        var localEye = _player.ToLocal(_player.GetNode<Camera3D>("Head/Camera3D").GlobalPosition);
        var eye = feet + Vector3.Up * localEye.Y + outward * localEye.Z + new Vector3(outward.Z, 0, -outward.X) * localEye.X;
        var end = center - outward * .08f;
        var beam = Descendants(h024).OfType<MeshInstance3D>().Single(m => m.Name == "DwellingFacade_Front_VergeRight_LOD0");
        var beamDistance = VergeTriangleDistance(VergePublishedTriangles(beam), eye, end);
        var gableDistance = VergeTriangleDistance(VergePublishedTriangles(gable), eye, end);
        var doorDistance = VergeTriangleDistance(VergePublishedTriangles(leaf), eye, end);
        var hit = VergePhysicalRay(eye, end);
        Check(beamDistance is null && hit.Source != beam.GetPath().ToString(), "B39 ReturnWest ray no longer hits the oversized verge proxy");
        var stillVisuallyBlocked = gable.IsVisibleInTree() && gableDistance is not null && doorDistance is not null
            && gableDistance.Value < doorDistance.Value;
        Check(stillVisuallyBlocked, "the real H024 gable still precedes ReturnWest's door in the sightline");
        var unbound = !source.HasMeta("building_id") && !_world.AddressRegistry!.Buildings.Values.Any(b => b.SourceKey == "act1/holding/ReturnWestBanyaSilhouette");
        Check(unbound, "verge repair does not bind or number the obstructed ReturnWest house");
        return new { source = source.GetPath().ToString(), leaf = leaf.GetPath().ToString(), gable = gable.GetPath().ToString(),
            supported, feet = P(feet), eye = P(eye), end = P(end), beamDistance, gableDistance, doorDistance,
            physical = VergeHitEvidence(hit), stillVisuallyBlocked, unbound, accepted = false };
    }

    private (bool HasHit, CollisionShape3D? Shape, Vector3? Point, string? Source, string? Collider, string? Owner) VergePhysicalRay(Vector3 from, Vector3 to)
    {
        using var query = PhysicsRayQueryParameters3D.Create(from, to, 3,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return (false, null, null, null, null, null);
        var body = hit["collider"].AsGodotObject() as CollisionObject3D;
        var owner = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
        return (true, owner as CollisionShape3D, hit["position"].AsVector3(), owner?.HasMeta("authoredSourceMesh") == true
            ? owner.GetMeta("authoredSourceMesh").AsString() : null, body?.GetPath().ToString(), owner?.GetPath().ToString());
    }
    private static object VergeHitEvidence((bool HasHit, CollisionShape3D? Shape, Vector3? Point, string? Source, string? Collider, string? Owner) hit) => new {
        hit.HasHit, hit.Collider, hit.Owner, shape = hit.Shape?.GetPath().ToString(), hit.Source,
        point = hit.Point is { } p ? P(p) : null, collisionMask = 3 };

    private static VergeTriangle[] VergePublishedTriangles(MeshInstance3D mesh)
    {
        if (mesh.Mesh is not ArrayMesh array) throw new InvalidOperationException("Expected the actual imported ArrayMesh: " + mesh.GetPath());
        var triangles = new List<VergeTriangle>();
        for (var surface = 0; surface < array.GetSurfaceCount(); surface++)
        {
            if (array.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles) throw new InvalidOperationException("Non-triangle surface");
            var arrays = array.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            int[] indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? [] : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
            if (vertices.Length == 0 || indices.Length % 3 != 0 || triangles.Count + indices.Length / 3 > 4096)
                throw new InvalidOperationException("Incomplete or unbounded triangle data");
            for (var i = 0; i < indices.Length; i += 3)
            {
                if (indices.Skip(i).Take(3).Any(index => index < 0 || index >= vertices.Length)) throw new InvalidOperationException("Invalid triangle index");
                var triangle = new VergeTriangle(mesh.ToGlobal(vertices[indices[i]]), mesh.ToGlobal(vertices[indices[i + 1]]), mesh.ToGlobal(vertices[indices[i + 2]]));
                if (triangle.Cross.LengthSquared() < 1e-14f) throw new InvalidOperationException("Degenerate triangle");
                triangles.Add(triangle);
            }
        }
        if (triangles.Count == 0) throw new InvalidOperationException("No published triangles");
        return triangles.ToArray();
    }
    private static float? VergeTriangleDistance(IReadOnlyList<VergeTriangle> faces, Vector3 from, Vector3 to)
    {
        var length = from.DistanceTo(to); if (length <= .000001f) throw new InvalidOperationException("Zero diagnostic ray");
        var direction = (to - from) / length; float? closest = null;
        foreach (var t in faces)
        {
            var edge1 = t.B - t.A; var edge2 = t.C - t.A; var h = direction.Cross(edge2); var determinant = edge1.Dot(h);
            if (Math.Abs(determinant) < 1e-8f) continue;
            var inverse = 1 / determinant; var s = from - t.A; var u = inverse * s.Dot(h);
            if (u < -1e-6f || u > 1 + 1e-6f) continue;
            var q = s.Cross(edge1); var v = inverse * direction.Dot(q);
            if (v < -1e-6f || u + v > 1 + 1e-6f) continue;
            var distance = inverse * edge2.Dot(q);
            if (distance >= 0 && distance <= length && (closest is null || distance < closest.Value)) closest = distance;
        }
        return closest;
    }
}
