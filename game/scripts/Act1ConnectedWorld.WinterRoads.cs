using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly Dictionary<string, MeshInstance3D> _winterAccessMeshes = new(StringComparer.Ordinal);
    // The revision is a pure function of the verified path array, and committing a
    // route stores a freshly built array (Compact returns a new Vector3[]), so the
    // array instance identifies its own contents. Remembering that instance lets an
    // unchanged route skip rebuilding its revision string on every later attach:
    // each published address walks every already verified route, so this removes an
    // O(routes x points) string build and its garbage from every publication.
    private readonly Dictionary<string, (Vector3[] Path, string Revision)> _winterAccessRevisions = new(StringComparer.Ordinal);

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
            _winterAccessRevisions.Remove(id);
        }
        foreach (var (id, path) in accepted)
        {
            if (!_winterAccessRevisions.TryGetValue(id, out var recorded) || !ReferenceEquals(recorded.Path, path))
                _winterAccessRevisions[id] = recorded = (path, string.Join(";", path.Select(point => point.ToString())));
            var revision = recorded.Revision;
            if (_winterAccessMeshes.TryGetValue(id, out var previous))
            {
                if (previous.GetMeta("routeRevision").AsString() == revision) continue;
                previous.QueueFree(); _winterAccessMeshes.Remove(id);
                // A re-committed route no longer owns the corridor it published; the next
                // lines publish the new one, so snow relief is never held clear of a path
                // that no longer exists.
                SnowReliefStandard.RetireCorridors($"winterPath:{id}#");
            }
            using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            var walked = 0f; var triangles = 0;
            // VIS-078: the path is a trodden channel, not a decal on the snow. Its floor
            // stays 1 cm over the ground; the snow kicked out of it forms a soft berm
            // either side (crest 4–7 cm, by route, so no two door paths are stamped
            // alike) that feathers back into the untouched snow 0.6–0.95 m out.
            var crest = .04f + .03f * (TimberHomeStyle.StableHash(id) % 100) / 100f;
            float Rise(float lateral) => Mathf.Abs(lateral) switch
            {
                < .75f => .010f + .004f * Mathf.Abs(lateral),
                < 1.2f => .035f,
                < 1.7f => crest,
                _ => -.004f
            };
            var routeLength = 0f;
            for (var segment = 0; segment < path.Length - 1; segment++)
                routeLength += new Vector2(path[segment + 1].X - path[segment].X, path[segment + 1].Z - path[segment].Z).Length();
            for (var segment = 0; segment < path.Length - 1; segment++)
            {
                var a = path[segment]; var b = path[segment + 1];
                var flat = new Vector2(b.X - a.X, b.Z - a.Z);
                if (flat.LengthSquared() < .0001f) continue;
                var across = new Vector3(flat.Y, 0, -flat.X).Normalized();
                var count = Mathf.Max(1, Mathf.CeilToInt(flat.Length() / .45f));
                var segmentStart = walked;
                var segmentLength = flat.Length();
                Vector3 Point(float t, float lateral)
                {
                    // VIS-012: the last metres before the door are shovelled to a
                    // 1 m working width; the street end stays a 0.64 m trodden line.
                    var along = segmentStart + segmentLength * t;
                    var halfWidth = Mathf.Lerp(.32f, .50f, Mathf.SmoothStep(routeLength - 3f, routeLength - 1f, along));
                    var p = a.Lerp(b, t) + across * halfWidth * lateral;
                    return new(p.X, AgentBAct1HeightField.CollisionGround(p.X, p.Z) + Rise(lateral), p.Z);
                }
                void Vertex(float t, float lateral)
                {
                    // UV.x beyond 0..1 is the material's fresh-snow side (soft_path_edges).
                    surface.SetUV(new((lateral + 1) * .5f, walked + flat.Length() * t));
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
                    for (var band = 0; band < Laterals.Length - 1; band++)
                    {
                        var l0 = Laterals[band]; var l1 = Laterals[band + 1];
                        Vertex(t0, l0); Vertex(t1, l1); Vertex(t1, l0);
                        Vertex(t0, l0); Vertex(t0, l1); Vertex(t1, l1); triangles += 2;
                    }
                }
                walked += flat.Length();
            }
            if (triangles == 0) continue;
            surface.GenerateNormals();
            surface.Index();
            var mesh = new MeshInstance3D { Name = "WinterPath_" + id, Mesh = surface.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForPath("c9cdcd"),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            mesh.SetMeta("accessId", id); mesh.SetMeta("routeRevision", revision);
            mesh.SetMeta("snowScale", "medium"); // VIS-077 tier: trodden channel with berms
            mesh.SetMeta("routeOwner", "AddressAccessVerifier + SettlementGraph");
            // VIS-077: this ribbon is the medium-edge tier of the snow standard — a trodden
            // floor 1 cm over the ground with a 4–7 cm berm, never a flat decal.
            mesh.SetMeta("snowTier", SnowReliefStandard.TierEdge);
            // VIS-079/016: the route is now also a required corridor, so the street banks and
            // the new drifts keep this exact line clear, and SnowTrampleField reads a print
            // inside it as compacted snow rather than as a fresh step.
            if (SnowReliefStandard.PublishPolyline($"winterPath:{id}", path, WinterRouteCorridorHalf) > 0
                && _snowReliefClipped) ReworkStreetSnowBanks();
            AddChild(mesh); _winterAccessMeshes.Add(id, mesh);
            // The heap is conformed through global transforms, so the path is in the tree first.
            AddShovelHeap(mesh, id, path, routeLength);
        }
    }

    /// <summary>
    /// VIS-012: the snow thrown off the cleared door end lies in one heap beside the
    /// path, 1.6 m short of the door, on whichever side is open ground (not a
    /// building footprint, not a porch or deck). Visual only; it never blocks.
    /// </summary>
    // Cross-section samples in half-widths: floor, channel wall, berm crest, feathered edge.
    private static readonly float[] Laterals = [-1.9f, -1.45f, -1f, -.5f, 0f, .5f, 1f, 1.45f, 1.9f];

    private void AddShovelHeap(Node3D pathMesh, string accessId, Vector3[] path, float routeLength)
    {
        if (routeLength < 3.5f) return;
        var target = routeLength - 1.6f;
        var walked = 0f;
        for (var segment = 0; segment < path.Length - 1; segment++)
        {
            var a = path[segment]; var b = path[segment + 1];
            var flat = new Vector2(b.X - a.X, b.Z - a.Z);
            var length = flat.Length();
            if (length < .01f || walked + length < target) { walked += length; continue; }
            var at = a.Lerp(b, (target - walked) / length);
            var across = new Vector2(flat.Y, -flat.X) / length;
            var footprints = AddressRegistry?.Buildings.Values
                .Where(building => building.Footprint.Count >= 3)
                .Select(building => building.Footprint.Select(q => new Vector2((float)q.X, (float)q.Z)).ToArray())
                .ToArray() ?? [];
            // Prefer a side by the access id so neighbouring houses do not all
            // throw their snow the same way; fall back to the other side.
            var first = TimberHomeStyle.StableHash(accessId) % 2 == 0 ? 1f : -1f;
            foreach (var sign in new[] { first, -first })
            {
                var centre = new Vector2(at.X, at.Z) + across * sign * 1.0f;
                var ground = AgentBAct1HeightField.CollisionGround(centre.X, centre.Y);
                var pathGround = AgentBAct1HeightField.CollisionGround(at.X, at.Z);
                if (Mathf.Abs(ground - pathGround) > .12f || Mathf.Abs(at.Y - .035f - pathGround) > .12f) continue;
                var clear = true;
                foreach (var offset in new[] { 0f, -.6f, .6f })
                {
                    var probe = centre + across * sign * .45f + new Vector2(flat.X, flat.Y) / length * offset;
                    if (footprints.Any(polygon => PointInPolygon(polygon, probe))) { clear = false; break; }
                }
                if (!clear) continue;
                // Near-round footprint: the detail is conformed to the terrain in its
                // own frame, so it is not rotated afterwards.
                var heap = AddYardSnowDetail(pathMesh, "ShovelHeap", new(.85f, .26f, .95f),
                    new Vector3(centre.X, ground, centre.Y), "e4eaee");
                heap.SetMeta("presentationRole", "snow thrown off the shovelled door end of this access path");
                return;
            }
            return;
        }
    }

    private static bool PointInPolygon(Vector2[] polygon, Vector2 point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)
                && point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y + 1e-6f) + polygon[i].X)
                inside = !inside;
        return inside;
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
            // Relayout v3: the open part's cross streets and the FAP street are drawn by
            // AddOpenPartStreets from the open-part plot.
            ("Main", main, 2.25f)
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
