using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly Dictionary<string, MeshInstance3D> _winterAccessMeshes = new(StringComparer.Ordinal);

    // The physical audit owns the route. A visible path is created only after
    // the standing body and connected street graph have accepted that route.
    private void RefreshVerifiedWinterFootpaths()
    {
        var accepted = _addressVerifiedPaths.Where(pair => AddressRegistry!.AccessPoints[pair.Key].State == "verified")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var id in _winterAccessMeshes.Keys.Where(id => !accepted.ContainsKey(id)).ToArray())
        {
            _winterAccessMeshes[id].QueueFree();
            _winterAccessMeshes.Remove(id);
        }
        foreach (var (id, path) in accepted)
        {
            var revision = string.Join(";", path.Select(point => point.ToString()));
            if (_winterAccessMeshes.TryGetValue(id, out var previous))
            {
                if (previous.GetMeta("routeRevision").AsString() == revision) continue;
                previous.QueueFree(); _winterAccessMeshes.Remove(id);
            }
            using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            var walked = 0f; var triangles = 0;
            for (var segment = 0; segment < path.Length - 1; segment++)
            {
                var a = path[segment]; var b = path[segment + 1];
                var flat = new Vector2(b.X - a.X, b.Z - a.Z);
                if (flat.LengthSquared() < .0001f) continue;
                var side = new Vector3(flat.Y, 0, -flat.X).Normalized() * .32f;
                var count = Mathf.Max(1, Mathf.CeilToInt(flat.Length() / .45f));
                Vector3 Point(float t, float lateral)
                {
                    var p = a.Lerp(b, t) + side * lateral;
                    return new(p.X, AgentBAct1HeightField.CollisionGround(p.X, p.Z) + .014f, p.Z);
                }
                void Vertex(float t, float lateral)
                {
                    surface.SetNormal(Vector3.Up);
                    surface.SetUV(new((lateral + 1) * .32f, walked + flat.Length() * t));
                    surface.SetColor(Colors.White); surface.AddVertex(ToLocal(Point(t, lateral)));
                }
                for (var i = 0; i < count; i++)
                {
                    var t0 = i / (float)count; var t1 = (i + 1) / (float)count;
                    var centre = a.Lerp(b, (t0 + t1) * .5f);
                    var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Z);
                    // Do not paint a terrain strip over a raised porch, tread,
                    // interior floor or bridge which owns its own surface.
                    if (Mathf.Abs(centre.Y - .035f - ground) > .12f) continue;
                    Vertex(t0, -1); Vertex(t1, 1); Vertex(t1, -1);
                    Vertex(t0, -1); Vertex(t0, 1); Vertex(t1, 1); triangles += 2;
                }
                walked += flat.Length();
            }
            if (triangles == 0) continue;
            surface.Index();
            var mesh = new MeshInstance3D { Name = "WinterPath_" + id, Mesh = surface.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("c9cdcd", "snow_road"),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            mesh.SetMeta("accessId", id); mesh.SetMeta("routeRevision", revision);
            mesh.SetMeta("routeOwner", "AddressAccessVerifier + SettlementGraph");
            AddChild(mesh); _winterAccessMeshes.Add(id, mesh);
        }
    }

    private static void BuildNorthWinterRoads(Node3D core)
    {
        var root = new Node3D { Name = "NorthContinuousWinterRoads" };
        core.AddChild(root);
        root.SetMeta("supportOwner", nameof(AgentBAct1HeightField));
        root.SetMeta("surfacePolicy", "continuous metre UV road ribbons, sampled against actual collision ground; no independent collision slab");
        var main = AgentBAct1Layout.MainRoadAxis.Where(point => point.Y >= 40).ToArray();
        var axes = new (string Name, Vector2[] Points, float Half)[]
        {
            ("Main", main, 2.8f), ("Lower", AgentBAct1Layout.EastStreetAxis, 2f),
            ("West", AgentBAct1Layout.WestSpurAxis, 1.75f), ("Square", AgentBAct1Layout.SquareRingAxis, 2.4f),
            ("EastLane", AgentBAct1Layout.NorthEastStreetAxis, 1.75f), ("Service", AgentBAct1Layout.WestServiceAxis, 1.75f),
            ("BridgeApproach", AgentBAct1Layout.BridgeApproachAxis.Where(p => p.X <= 41.5f).ToArray(), 2.3f),
            ("SchoolRearLink", AgentBAct1Layout.NorthCrossStreetAxis, 2f), ("NorthReturnLink", AgentBAct1Layout.NorthReturnStreetAxis, 2.4f)
        };
        var offset = .012f;
        foreach (var (name, points, half) in axes)
        {
            var closed = points[0].IsEqualApprox(points[^1]);
            var sides = new Vector2[points.Length];
            for (var k = 0; k < points.Length; k++)
            {
                var previous = k == 0 ? (closed ? points[^2] : points[0]) : points[k - 1];
                var next = k == points.Length - 1 ? (closed ? points[1] : points[^1]) : points[k + 1];
                var incoming = (points[k] - previous).Normalized();
                var outgoing = (next - points[k]).Normalized();
                if (incoming == Vector2.Zero) incoming = outgoing;
                if (outgoing == Vector2.Zero) outgoing = incoming;
                var n0 = new Vector2(incoming.Y, -incoming.X);
                var n1 = new Vector2(outgoing.Y, -outgoing.X);
                var miter = (n0 + n1).Normalized();
                sides[k] = miter / Mathf.Max(.5f, miter.Dot(n1));
            }
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var walked = 0f;
            for (var segment = 0; segment < points.Length - 1; segment++)
            {
                var a = points[segment]; var b = points[segment + 1];
                var length = a.DistanceTo(b);
                var count = Mathf.Max(1, Mathf.CeilToInt(length / .75f));
                const int cross = 8;
                Vector3 P(int i, int j)
                {
                    var lateral = (j / (float)cross * 2 - 1) * half;
                    var t = i / (float)count;
                    var p = a.Lerp(b, t) + sides[segment].Lerp(sides[segment + 1], t) * lateral;
                    return new(p.X, AgentBAct1HeightField.CollisionGround(p.X, p.Y) + offset, p.Y);
                }
                void V(int i, int j)
                {
                    surface.SetUV(new(j / (float)cross * half * 2, walked + i / (float)count * length));
                    surface.SetColor(Colors.White);
                    surface.AddVertex(root.ToLocal(new Vector3(P(i, j).X, P(i, j).Y, P(i, j).Z)));
                }
                for (var i = 0; i < count; i++)
                for (var j = 0; j < cross; j++)
                {
                    V(i, j); V(i + 1, j + 1); V(i + 1, j);
                    V(i, j); V(i, j + 1); V(i + 1, j + 1);
                }
                walked += length;
            }
            surface.Index(); surface.GenerateNormals();
            var mesh = surface.Commit();
            root.AddChild(new MeshInstance3D { Name = name, Mesh = mesh,
                MaterialOverride = PainterlyMaterialLibrary.ForColor("c2c8c9", "snow_road") });
            offset += .001f;
        }
    }
}
