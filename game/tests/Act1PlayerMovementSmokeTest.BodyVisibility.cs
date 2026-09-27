using Godot;

namespace Urman.Godot.Tests;

public partial class Act1PlayerMovementSmokeTest
{
    private readonly record struct BodyViewTriangle(string Mesh, Vector3 A, Vector3 B, Vector3 C);

    private void CheckPresentedLowerBody(string pose)
    {
        // Read the mesh posed by the renderer, not just source bounds or bone
        // counts. The old flat waist cap passed both of those checks while
        // completely covering the boots in the actual first-person picture.
        if (DisplayServer.GetName() == "headless")
        {
            _events.Add(new { kind = "body-visibility", pose, status = "not-run/headless" });
            return;
        }
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        var body = _player.GetNode<Node3D>("AidarLowerBody");
        var triangles = new List<BodyViewTriangle>();
        foreach (var mesh in body.FindChildren("*", nameof(MeshInstance3D), true, false)
                     .OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree()))
        {
            if (pose == "standing" && (mesh.Name == "CouncilWitness_Body_LOD0"
                || mesh.Name.ToString().StartsWith("CouncilWitness_Trouser", StringComparison.Ordinal)))
            {
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    var source = mesh.Mesh.SurfaceGetArrays(surface);
                    var points = source[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    var weights = source[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                    Check(weights.Length == points.Length * 4
                        && weights.All(w => float.IsFinite(w) && w >= 0 && w <= 1)
                        && weights.Chunk(4).All(group => Math.Abs(group.Sum() - 1) < .0001f),
                        mesh.Name + " garment weights are bounded and normalized");
                    if (mesh.Name.ToString().Contains("Trouser", StringComparison.Ordinal))
                    {
                        var calf = Enumerable.Range(0, points.Length).Where(i => points[i].Y < .35f).ToArray();
                        var thigh = Enumerable.Range(0, points.Length).Where(i => points[i].Y > .60f && weights[i * 4 + 2] < .5f).ToArray();
                        Check(calf.Length > 0 && thigh.Length > 0
                            && calf.All(i => weights[i * 4 + 1] > .9999f)
                            && thigh.All(i => weights[i * 4] > .9999f),
                            mesh.Name + " calf follows knee and thigh follows hip without constant cross-blending");
                    }
                }
            }
            using var baked = mesh.BakeMeshFromCurrentSkeletonPose();
            for (var surface = 0; surface < baked.GetSurfaceCount(); surface++)
            {
                var arrays = baked.SurfaceGetArrays(surface);
                var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()
                    .Select(point => mesh.GlobalTransform * point).ToArray();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (indices.Length == 0) indices = Enumerable.Range(0, points.Length).ToArray();
                for (var index = 0; index < indices.Length; index += 3)
                    triangles.Add(new(mesh.Name.ToString(), points[indices[index]], points[indices[index + 1]], points[indices[index + 2]]));
            }
        }
        var visibleArea = new Dictionary<string, float>(StringComparer.Ordinal);
        Vector3[] WaistSeam(string side) => triangles.Where(triangle => triangle.Mesh == "CouncilWitness_Trouser" + side + "_LOD0")
            .SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
            .Where(point => Math.Abs(_player.ToLocal(point).X) < .001f).Distinct().ToArray();
        var leftSeam = WaistSeam("Left");
        var rightSeam = WaistSeam("Right");
        Check(leftSeam.Length >= 5 && rightSeam.Length >= 5
            && leftSeam.All(point => rightSeam.Any(other => other.DistanceTo(point) < .002f))
            && rightSeam.All(point => leftSeam.Any(other => other.DistanceTo(point) < .002f)),
            pose + " actual trouser halves remain joined at the posed pelvis seam");
        _events.Add(new { kind = "body-waist-seam", pose,
            left = leftSeam.Select(point => point.ToString()).ToArray(), right = rightSeam.Select(point => point.ToString()).ToArray() });
        foreach (var triangle in triangles.Where(triangle => triangle.Mesh.Contains("Boot", StringComparison.Ordinal)
                     || triangle.Mesh.Contains("Trouser", StringComparison.Ordinal)))
        {
            visibleArea.TryAdd(triangle.Mesh, 0);
            if (camera.IsPositionBehind(triangle.A) || camera.IsPositionBehind(triangle.B) || camera.IsPositionBehind(triangle.C)) continue;
            var a = camera.UnprojectPosition(triangle.A);
            var b = camera.UnprojectPosition(triangle.B);
            var c = camera.UnprojectPosition(triangle.C);
            var area = Math.Abs((b - a).Cross(c - a)) * .5f;
            if (area < .001f) continue;
            var samples = new[] { (triangle.A + triangle.B + triangle.C) / 3,
                triangle.A * .6f + triangle.B * .2f + triangle.C * .2f,
                triangle.A * .2f + triangle.B * .6f + triangle.C * .2f,
                triangle.A * .2f + triangle.B * .2f + triangle.C * .6f };
            foreach (var point in samples)
            {
                if (!camera.IsPositionInFrustum(point)) continue;
                var offset = point - camera.GlobalPosition;
                var distance = offset.Length();
                var direction = offset / distance;
                if (triangles.Any(blocker => BodyTriangleDistance(camera.GlobalPosition, direction, blocker) is var depth
                        && depth > camera.Near && depth < distance - .0015f)) continue;
                using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3,
                    new global::Godot.Collections.Array<Rid> { _player.GetRid() });
                var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if (hit.Count > 0 && hit["position"].AsVector3().DistanceTo(point) > .01f) continue;
                visibleArea[triangle.Mesh] += area / samples.Length;
            }
        }
        _events.Add(new { kind = "body-visibility", pose, source = "native renderer posed triangles",
            camera = camera.GlobalPosition.ToString(), projectedVisiblePixels = visibleArea,
            limit = "geometric visibility only; the saved native picture still requires visual review" });
        var scale = GetViewport().GetVisibleRect().Size.X * GetViewport().GetVisibleRect().Size.Y / (1280f * 720f);
        foreach (var side in new[] { "Left", "Right" })
        {
            Check(visibleArea.GetValueOrDefault("CouncilWitness_Trouser" + side + "_LOD0") >= 32f * scale,
                pose + " actual " + side + " trouser has visible projected surface outside the coat");
            if (pose == "standing")
                Check(visibleArea.GetValueOrDefault("CouncilWitness_Boot" + side + "_LOD0") >= 24f * scale,
                    "standing actual " + side + " shaped boot is visible from the game camera");
        }
    }

    private static float BodyTriangleDistance(Vector3 origin, Vector3 direction, BodyViewTriangle triangle)
    {
        var edge1 = triangle.B - triangle.A;
        var edge2 = triangle.C - triangle.A;
        var cross = direction.Cross(edge2);
        var determinant = edge1.Dot(cross);
        if (Math.Abs(determinant) < .0000001f) return float.PositiveInfinity;
        var inverse = 1f / determinant;
        var toOrigin = origin - triangle.A;
        var u = toOrigin.Dot(cross) * inverse;
        if (u < 0 || u > 1) return float.PositiveInfinity;
        var q = toOrigin.Cross(edge1);
        var v = direction.Dot(q) * inverse;
        if (v < 0 || u + v > 1) return float.PositiveInfinity;
        return edge2.Dot(q) * inverse;
    }
}
