using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// The street face of every yard along the village roads, the way a Tatar
/// village street reads: the house looks onto the street over a low picket
/// palisadnik, beside it stand the painted plank gates (капка) with a sun over
/// them and a wicket, and a solid board fence closes the yard to the
/// neighbour's plot (research plot H02, streets ST01/ST02).
///
/// Built from the address registry, after it is imported: every house by a
/// street road gets its own frontage, coloured per yard. The fence line keeps
/// clear of every road and path, of the straight walk from each door to the
/// road graph (an open wicket stands there), of spawn points and of small
/// gameplay collision, so no route, door or interaction is closed off. Old
/// loose fence pieces standing on the new line are hidden unless they carry
/// collision. All frontage geometry of the village is merged into a few
/// vertex-coloured meshes: three draw calls, not hundreds.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const float FrontageSample = .25f;

    private static readonly string[] GatePaints = ["2f7d55", "2e6aa8", "2a8a86", "7d3a33", "3d6f3a", "35608f"];
    private static readonly string[] PalisadePaints = ["7fae78", "9cc3d8", "d9d5c6", "6f9fc4", "a8c38a"];

    private sealed class FrontageMesh
    {
        private readonly List<Vector3> _vertices = [];
        private readonly List<Vector3> _normals = [];
        private readonly List<Color> _colours = [];
        public int Triangles => _vertices.Count / 3;

        private void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color colour)
        {
            // Godot front faces are clockwise seen from outside.
            if ((b - a).Cross(c - a).Dot(outward) > 0) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c }) { _vertices.Add(v); _normals.Add(outward); _colours.Add(colour); }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color colour)
        {
            Tri(a, b, c, outward, colour);
            Tri(a, c, d, outward, colour);
        }

        /// <summary>Box from a centre and three half-axes (need not be axis aligned).</summary>
        public void Box(Vector3 centre, Vector3 x, Vector3 y, Vector3 z, Color colour, bool bottom = false)
        {
            Vector3 P(int i, int j, int k) => centre + x * i + y * j + z * k;
            var nx = x.Normalized(); var ny = y.Normalized(); var nz = z.Normalized();
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), nx, colour);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -nx, colour);
            Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), ny, colour);
            if (bottom) Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -ny, colour);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), nz, colour);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -nz, colour);
        }

        /// <summary>Upright board standing on the ground at <paramref name="foot"/>.</summary>
        public void Upright(Vector3 foot, float height, float width, float depth, Vector3 along, Vector3 across, Color colour, bool pointed = false)
        {
            var top = pointed ? height - width * .6f : height;
            Box(foot + Vector3.Up * (top * .5f - .06f), along * (width * .5f), Vector3.Up * (top * .5f + .06f), across * (depth * .5f), colour);
            if (!pointed) return;
            var baseY = foot + Vector3.Up * top;
            var apex = foot + Vector3.Up * height;
            var l = along * (width * .5f); var d = across * (depth * .5f);
            Quad(baseY - l - d, baseY - l + d, apex + d, apex - d, (-along + Vector3.Up).Normalized(), colour);
            Quad(baseY + l - d, baseY + l + d, apex + d, apex - d, (along + Vector3.Up).Normalized(), colour);
            Tri(baseY - l + d, baseY + l + d, apex + d, across, colour);
            Tri(baseY - l - d, baseY + l - d, apex - d, -across, colour);
        }

        /// <summary>A bar between two points with a square section.</summary>
        public void Bar(Vector3 a, Vector3 b, float thickness, Vector3 side, Color colour)
        {
            var axis = b - a;
            var up = axis.Cross(side).Normalized();
            if (up.Y < 0) up = -up;
            Box((a + b) * .5f, axis * .5f, up * (thickness * .5f), side.Normalized() * (thickness * .5f), colour);
        }

        public ArrayMesh Commit(Material material)
        {
            var arrays = new global::Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = _colours.ToArray();
            var mesh = new ArrayMesh();
            if (_vertices.Count > 0)
            {
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                mesh.SurfaceSetMaterial(0, material);
            }
            return mesh;
        }
    }

    private sealed record FrontageRun(Vector3 A, Vector3 B, string Kind);

    private readonly record struct Obstacle(Vector2 Centre, float Radius);

    private void BuildStreetFrontages()
    {
        var registry = AddressRegistry!;
        var axes = new (string Id, Vector2[] Points, float HalfWidth)[]
        {
            ("main", AgentBAct1Layout.MainRoadAxis, 2.8f)
            // The far bank's generic parcels carry their own yard fences (relayout v3).
            // The FAP branch keeps its authored service loop and holding walks
            // unfenced, and the zirat road its open memory field.
        };

        // Everything the fence line must leave open.
        var obstacles = new List<Obstacle>();
        foreach (var shape in FindDescendants<CollisionShape3D>(this))
        {
            if (!shape.IsInsideTree() || shape.Disabled || shape.GetParent() is not CollisionObject3D owner) continue;
            // Only what the player uses: interactions, carried things, triggers,
            // people and vehicles, and old working gates. Decorative colliders
            // (fence posts, props) do not break the street line.
            var gameplay = owner is InteractionTarget or CarryableProp or Area3D or CharacterBody3D
                || owner.Name.ToString().Contains("Gate", StringComparison.Ordinal);
            if (!gameplay) continue;
            var radius = shape.Shape switch
            {
                BoxShape3D box => new Vector2(box.Size.X, box.Size.Z).Length() * .5f * shape.GlobalBasis.Scale.X,
                SphereShape3D sphere => sphere.Radius,
                CylinderShape3D cylinder => cylinder.Radius,
                CapsuleShape3D capsule => capsule.Radius,
                _ => -1f
            };
            // Small bodies only: props, gates, triggers. Houses are handled by their footprints.
            if (radius < 0 || radius > 2.2f) continue;
            var p = shape.GlobalPosition;
            obstacles.Add(new(new(p.X, p.Z), radius + .35f));
        }
        foreach (var placement in Act1WorldLayout.Placements)
        foreach (var spawn in placement.SpawnPoints.Values)
        {
            var p = placement.Origin + spawn.Position;
            obstacles.Add(new(new(p.X, p.Z), 1.6f));
        }
        var accessLines = new List<(Vector2 A, Vector2 B)>();
        foreach (var access in registry.AccessPoints.Values)
        {
            var nearest = registry.Graph.Nearest(access.Position);
            if (nearest is not { } link) continue;
            accessLines.Add((new((float)access.Position.X, (float)access.Position.Z), new((float)link.Point.X, (float)link.Point.Z)));
        }
        var footprints = registry.Buildings.Values
            .Where(b => b.Footprint.Count >= 3)
            .Select(b => b.Footprint.Select(p => new Vector2((float)p.X, (float)p.Z)).ToArray())
            .ToList();

        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        string? lastReason = null;
        bool Reject(string why) { reasons[why] = reasons.GetValueOrDefault(why) + 1; lastReason = why; return false; }
        bool Clear(Vector3 point)
        {
            lastReason = null;
            var q = new Vector2(point.X, point.Z);
            var (roadDistance, roadHalf) = AgentBAct1HeightField.RoadInfo(point.X, point.Z);
            if (roadDistance < roadHalf + .6f) return Reject("axis");
            foreach (var road in registry.Graph.Roads.Values)
            {
                var clearance = (float)road.Width * .5f + .6f;
                for (var i = 1; i < road.Points.Count; i++)
                {
                    var a = new Vector2((float)road.Points[i - 1].X, (float)road.Points[i - 1].Z);
                    var b = new Vector2((float)road.Points[i].X, (float)road.Points[i].Z);
                    if (SegmentDistance(q, a, b) < clearance) return Reject("road:" + road.Id.Split('/')[0]);
                }
            }
            foreach (var (a, b) in accessLines) if (SegmentDistance(q, a, b) < .8f) return Reject("access");
            foreach (var obstacle in obstacles) if (q.DistanceTo(obstacle.Centre) < obstacle.Radius) return Reject("obstacle");
            foreach (var polygon in footprints)
            {
                if (Geometry2D.IsPointInPolygon(q, polygon)) return Reject("footprint");
                for (var i = 0; i < polygon.Length; i++)
                    if (SegmentDistance(q, polygon[i], polygon[(i + 1) % polygon.Length]) < .35f) return Reject("footprint");
            }
            return true;
        }

        var runs = new List<FrontageRun>();
        var gates = new List<(Vector3 Start, Vector3 End, Vector3 Normal, int Paint)>();
        var wickets = new List<(Vector3 Start, Vector3 End, Vector3 Yardward, int Paint)>();
        var built = 0;
        foreach (var building in registry.Buildings.Values.OrderBy(b => b.BuildingId, StringComparer.Ordinal))
        {
            // The shop's front stays open to the street: people walk up to it.
            if (building.Role is not ("residential" or "council" or "school") || building.Footprint.Count < 3) continue;
            if (building.SourceKey.EndsWith("/babai", StringComparison.Ordinal)) continue;
            // Generic parcels of the authored plots carry their own yard fence and gate.
            if (building.SourceKey.StartsWith("urman.world:", StringComparison.Ordinal)) continue;
            // Tamara Gennadievna's plot owns its own street face: the quest's
            // breakable fence stands on the shoulder, roadward of this line.
            if (building.SourceKey.EndsWith("/ReturnEastHouseA8Silhouette", StringComparison.Ordinal)) continue;
            var centre = new Vector2((float)building.Position.X, (float)building.Position.Z);
            // The street this house belongs to: the nearest street road it
            // actually faces with room for a fence line in front.
            var candidates = new List<(int Axis, int Seg, float Distance)>();
            for (var a = 0; a < axes.Length; a++)
            for (var i = 1; i < axes[a].Points.Length; i++)
                candidates.Add((a, i, SegmentDistance(centre, axes[a].Points[i - 1], axes[a].Points[i])));
            var chosen = false;
            (string Id, Vector2[] Points, float HalfWidth) axis = default;
            Vector2 p0 = default, tangent2 = default, normal2 = default;
            Vector2[] corners = [];
            float s0 = 0, s1 = 0, face = 0, minLine = 0;
            foreach (var candidate in candidates.Where(c => c.Distance <= 26f).OrderBy(c => c.Distance))
            {
                axis = axes[candidate.Axis];
                p0 = axis.Points[candidate.Seg - 1]; var p1 = axis.Points[candidate.Seg];
                tangent2 = (p1 - p0).Normalized();
                normal2 = new Vector2(-tangent2.Y, tangent2.X);
                if ((centre - p0).Dot(normal2) < 0) normal2 = -normal2;
                var segLength = p0.DistanceTo(p1);
                var along = (centre - p0).Dot(tangent2);
                // End segments run on past their last point (the arrival tail).
                var endSegment = candidate.Seg == 1 || candidate.Seg == axis.Points.Length - 1;
                if (along < (endSegment ? -14f : -4f) || along > segLength + (endSegment ? 14f : 4f)) continue;
                corners = building.Footprint.Select(p => new Vector2((float)p.X, (float)p.Z) - p0).ToArray();
                s0 = corners.Min(c => c.Dot(tangent2)); s1 = corners.Max(c => c.Dot(tangent2));
                face = corners.Min(c => c.Dot(normal2));
                minLine = axis.HalfWidth + 1.1f;
                if (face >= minLine + .2f) { chosen = true; break; }
            }
            if (!chosen)
            {
                if (OS.GetEnvironment("URMAN_FRONTAGE_DEBUG") == "1")
                    GD.Print($"frontage-skip|{building.SourceKey}|nearest={candidates.Min(c => c.Distance):0.0}");
                continue;
            }
            // Houses set far back get their street fence on a common line and a deep front yard.
            var palisade = face - 2.4f >= minLine ? Math.Min(face - 2.4f, axis.HalfWidth + 4.5f)
                : face - 1.0f >= minLine ? minLine : (float?)null;
            var line = palisade ?? face;

            // Plot boundaries: halfway to the next house on the same side of the street.
            var others = registry.Buildings.Values.Where(o => o.BuildingId != building.BuildingId && o.Footprint.Count >= 3)
                .Select(o => o.Footprint.Select(p => new Vector2((float)p.X, (float)p.Z) - p0).ToArray())
                .Where(o => o.Min(c => c.Dot(normal2)) < line + 14f && o.Max(c => c.Dot(normal2)) > line - .5f)
                .Select(o => (S0: o.Min(c => c.Dot(tangent2)), S1: o.Max(c => c.Dot(tangent2)))).ToArray();
            var right = Math.Min(s1 + 10f, others.Where(o => o.S0 > s1 - .5f).Select(o => (s1 + o.S0) * .5f - .25f).DefaultIfEmpty(s1 + 10f).Min());
            var left = Math.Max(s0 - 6f, others.Where(o => o.S1 < s0 + .5f).Select(o => (s0 + o.S1) * .5f + .25f).DefaultIfEmpty(s0 - 6f).Max());

            Vector3 At(float s, float d)
            {
                var q = p0 + tangent2 * s + normal2 * d;
                return new(q.X, AgentBAct1HeightField.CollisionGround(q.X, q.Y), q.Y);
            }
            var hash = (int)(Urman.Core.Determinism.SeededRng.HashText(building.BuildingId) & 0x7fffffffu);
            var paint = hash % GatePaints.Length;

            void Emit(float from, float to, float d, string kind)
            {
                // Sample the line, keep only the clear stretches. A short gap left
                // for the walk from a door to the road becomes an open wicket.
                var start = (float?)null;
                var gapStart = (float?)null; var gapIsAccess = true;
                for (var s = from; s <= to + .001f; s += FrontageSample)
                {
                    var ok = Clear(At(s, d));
                    if (!ok) { gapStart ??= s; gapIsAccess &= lastReason == "access"; }
                    else if (gapStart is { } g)
                    {
                        if (gapIsAccess && s - g >= .5f && s - g <= 2.6f && start is null && runs.Count > 0)
                            wickets.Add((At(g - FrontageSample * .5f, d), At(s - FrontageSample * .5f, d), new Vector3(normal2.X, 0, normal2.Y), paint));
                        gapStart = null; gapIsAccess = true;
                    }
                    if (ok && start is null) start = s;
                    if ((!ok || s + FrontageSample > to + .001f) && start is { } begin)
                    {
                        var end = ok ? s : s - FrontageSample;
                        if (end - begin >= .7f) runs.Add(new(At(begin, d), At(end, d), kind + "|" + hash));
                        start = null;
                    }
                }
            }

            // Gate on the side of the door, or where there is more room.
            var access = registry.AccessPoints.Values.FirstOrDefault(a => a.BuildingId == building.BuildingId);
            var accessS = access is null ? (s0 + s1) * .5f
                : (new Vector2((float)access.Position.X, (float)access.Position.Z) - p0).Dot(tangent2);
            var gateRight = accessS > s1 - .5f || (accessS >= s0 + .5f && right - s1 >= s0 - left);
            var gateEnd = float.NaN;
            float room = 0;
            foreach (var (tryRight, total) in new[] { (gateRight, 4.4f), (!gateRight, 4.4f), (gateRight, 3.3f), (!gateRight, 3.3f) })
            {
                var from = tryRight ? s1 + .15f : s0 - .15f;
                room = tryRight ? right - from : from - left;
                if (room < total + .2f) continue;
                var sign = tryRight ? 1f : -1f;
                var to = from + sign * total;
                var clear = true;
                for (var s = Math.Min(from, to); s <= Math.Max(from, to) && clear; s += FrontageSample) clear &= Clear(At(s, line));
                if (!clear) continue;
                gates.Add((At(from, line), At(to, line), new Vector3(-normal2.X, 0, -normal2.Y), paint));
                gateRight = tryRight; gateEnd = to;
                break;
            }
            if (float.IsNaN(gateEnd)) gateEnd = gateRight ? s1 + .15f : s0 - .15f;
            if (gateRight) { Emit(gateEnd + .1f, right, line, "solid"); Emit(left, s0 - .1f, line, "solid"); }
            else { Emit(left, gateEnd - .1f, line, "solid"); Emit(s1 + .1f, right, line, "solid"); }
            if (palisade is not null)
            {
                Emit(s0, s1, line, "picket");
                if (face - line <= 3.5f)
                    // Short palisadnik returns back to the house corners.
                    foreach (var s in new[] { s0, s1 })
                    {
                        var a = At(s, line); var b = At(s, face - .1f);
                        if (Clear(a) && Clear(b) && a.DistanceTo(b) >= .6f) runs.Add(new(a, b, "picket|" + hash));
                    }
                else
                    // A house set deep in its plot: the plot boundaries run back from the street.
                    foreach (var s in new[] { left + .1f, right - .1f })
                    {
                        var a = At(s, line + .1f); var b = At(s, Math.Min(face + 2f, line + 9f));
                        if (Clear(a) && Clear(b) && a.DistanceTo(b) >= .6f) runs.Add(new(a, b, "solid|" + hash));
                    }
            }
            built++;
            if (OS.GetEnvironment("URMAN_FRONTAGE_DEBUG") == "1")
                GD.Print(System.FormattableString.Invariant($"frontage-house|{building.SourceKey}|{axis.Id}|hash={hash}|s0={s0:0.0} s1={s1:0.0} face={face:0.0} line={line:0.0} left={left:0.0} right={right:0.0} gateRight={gateRight} room={room:0.0}"));
        }

        if (OS.GetEnvironment("URMAN_FRONTAGE_DEBUG") == "1")
            foreach (var run in runs)
                GD.Print(System.FormattableString.Invariant($"frontage-run|{run.Kind}|{run.A.X:0.0},{run.A.Z:0.0}|{run.B.X:0.0},{run.B.Z:0.0}"));
        // Merge everything into a few meshes: wood (vertex coloured), snow, iron
        // hardware and collision. VIS-090: gate hardware is its own material so a
        // hinge strap answers the low sun as metal, not as another painted plank.
        var wood = new FrontageMesh();
        var snow = new FrontageMesh();
        var metal = new FrontageMesh();
        var body = new StaticBody3D { Name = "StreetFrontageCollision", CollisionLayer = 1u, CollisionMask = 0u };
        body.SetMeta("collisionOwner", "street-frontage");
        var snowColour = new Color("eef2f6");
        var rng = new RandomNumberGenerator { Seed = 20260925 };
        foreach (var run in runs)
        {
            var parts = run.Kind.Split('|');
            var kind = parts[0]; var hash = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            var flat = new Vector3(run.B.X - run.A.X, 0, run.B.Z - run.A.Z);
            var length = flat.Length();
            if (length < .5f) continue;
            var along = flat / length;
            var across = new Vector3(-along.Z, 0, along.X);
            Vector3 Ground(float t)
            {
                var p = run.A + flat * t;
                return new(p.X, AgentBAct1HeightField.CollisionGround(p.X, p.Z) - .05f, p.Z);
            }
            var picket = kind == "picket";
            var height = picket ? 1.05f : 1.85f;
            var raw = new Color(GatePaints[hash % GatePaints.Length]);
            var painted = picket ? new Color(PalisadePaints[hash % PalisadePaints.Length])
                : (hash / 7) % 3 == 0 ? raw.Darkened(.25f) : new Color("5b544a");
            var boardWidth = picket ? .085f : .14f;
            var pitch = picket ? .16f : .152f;
            var boards = Math.Max(2, (int)(length / pitch));
            for (var i = 0; i <= boards; i++)
            {
                var t = i / (float)boards;
                var tint = 1f + rng.RandfRange(-.07f, .07f);
                var colour = new Color(painted.R * tint, painted.G * tint, painted.B * tint);
                var h = height + (picket ? 0 : rng.RandfRange(-.03f, .03f));
                wood.Upright(Ground(t) + across * (picket ? 0 : .02f), h, boardWidth, picket ? .02f : .025f, along, across, colour, pointed: picket);
            }
            // Posts and rails on the yard side.
            var posts = Math.Max(1, (int)Math.Ceiling(length / (picket ? 2.0f : 2.4f)));
            var postColour = new Color("5b4d3e");
            for (var i = 0; i <= posts; i++)
            {
                var foot = Ground(i / (float)posts) + across * (picket ? .03f : .07f);
                wood.Upright(foot, height + .12f, .1f, .1f, along, across, postColour);
                snow.Box(foot + Vector3.Up * (height + .15f), along * .06f, Vector3.Up * .025f, across * .06f, snowColour);
            }
            foreach (var railHeight in picket ? new[] { .3f, .8f } : new[] { .35f, 1.45f })
            {
                var a = Ground(0) + Vector3.Up * railHeight + across * (picket ? .03f : .06f);
                var b = Ground(1) + Vector3.Up * railHeight + across * (picket ? .03f : .06f);
                wood.Bar(a, b, .055f, across, postColour);
            }
            // Snow settled along the top.
            if (!picket)
                for (var i = 0; i < boards; i += 3)
                {
                    var a = Ground(i / (float)boards) + Vector3.Up * (height + .015f) + across * .02f;
                    var b = Ground(Math.Min(1f, (i + 3) / (float)boards)) + Vector3.Up * (height + .015f) + across * .02f;
                    snow.Bar(a, b, .05f, across, snowColour);
                }
            var mid = (run.A + run.B) * .5f;
            body.AddChild(new CollisionShape3D
            {
                Name = $"Frontage_{body.GetChildCount()}",
                Position = new Vector3(mid.X, AgentBAct1HeightField.CollisionGround(mid.X, mid.Z) + height * .5f, mid.Z),
                Rotation = new Vector3(0, Mathf.Atan2(along.X, along.Z), 0),
                Shape = new BoxShape3D { Size = new Vector3(.14f, height, length) }
            });
        }
        foreach (var (start, end, streetward, paintIndex) in gates)
            AddFrontageGate(wood, snow, metal, body, start, end, streetward, paintIndex);
        foreach (var (start, end, yardward, paintIndex) in wickets)
            AddFrontageWicket(wood, snow, start, end, yardward, paintIndex);

        var frontage = new Node3D { Name = "StreetFrontages" };
        frontage.SetMeta("presentationRole", "street faces of the yards: palisadnik, painted gates with a wicket, board fences");
        AddChild(frontage);
        var woodMaterial = VehicleVisualFactory.FrontageWoodMaterial();
        frontage.AddChild(new MeshInstance3D { Name = "FrontageWood", Mesh = wood.Commit(woodMaterial) });
        frontage.AddChild(new MeshInstance3D
        {
            Name = "FrontageSnow", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = snow.Commit(new StandardMaterial3D { AlbedoColor = snowColour, VertexColorUseAsAlbedo = true, Roughness = .9f })
        });
        // VIS-090: gate hardware is committed as its own mesh so the straps answer
        // light as iron (dulled metal response), not as another painted board.
        frontage.AddChild(new MeshInstance3D
        {
            Name = "FrontageIron",
            Mesh = metal.Commit(PainterlyMaterialLibrary.ForColor("494540", "iron"))
        });
        frontage.AddChild(body);
        var hidden = HideFencesUnderFrontage(runs, gates.Select(g => (g.Start, g.End)).ToList());
        SetMeta("streetFrontageHouses", built);
        SetMeta("streetFrontageRuns", runs.Count);
        SetMeta("streetFrontageGates", gates.Count);
        SetMeta("streetFrontageWickets", wickets.Count);
        SetMeta("streetFrontageHiddenFencePieces", hidden);
        GD.Print("act1-street-frontage: rejected " + string.Join(", ", reasons.OrderByDescending(r => r.Value).Select(r => $"{r.Key}={r.Value}")));
        GD.Print($"act1-street-frontage: houses={built} runs={runs.Count} gates={gates.Count} wickets={wickets.Count} wood_tris={wood.Triangles} iron_tris={metal.Triangles} hidden_old_fence_pieces={hidden}");
    }

    /// <summary>
    /// Tatar yard gate: two plank leaves between heavy posts under a small gable
    /// roof, a frieze with a rising sun over them, rhombs on the leaves, and an
    /// open wicket beside them. Collision covers the leaves; the wicket is open.
    /// </summary>
    private static void AddFrontageGate(FrontageMesh wood, FrontageMesh snow, FrontageMesh metal,
        StaticBody3D body, Vector3 start, Vector3 end, Vector3 streetward, int paintIndex)
    {
        var flat = new Vector3(end.X - start.X, 0, end.Z - start.Z);
        var total = flat.Length();
        var along = flat / total;
        var up = Vector3.Up;
        var paint = new Color(GatePaints[paintIndex]);
        var trim = new Color("f1efe6");
        var sun = new Color("e2b33c");
        var post = paint.Darkened(.35f);
        var snowColour = new Color("eef2f6");
        var ground = Math.Min(start.Y, end.Y);
        Vector3 P(float s, float y) => new Vector3(start.X, ground, start.Z) + along * s + up * y;
        // Gate span, then a 1 m wicket beside it.
        var gate = total - 1.3f;
        var mid = (gate + .1f) * .5f;
        foreach (var s in new[] { 0f, gate + .1f, total - .1f })
        {
            wood.Upright(P(s, -.05f), 2.75f, .22f, .22f, along, streetward, post);
            snow.Box(P(s, 2.72f), along * .13f, up * .03f, streetward * .13f, snowColour);
        }
        var boards = (int)((gate - .2f) / .145f);
        for (var i = 0; i < boards; i++)
            wood.Upright(P(.2f + i * .145f, .08f), 1.98f, .135f, .045f, along, streetward, paint * (1f + (i % 3 - 1) * .03f));
        foreach (var y in new[] { .2f, 1.1f, 2.0f })
            wood.Bar(P(.16f, y) + streetward * .035f, P(gate - .02f, y) + streetward * .035f, .06f, streetward, trim);
        // Two leaves meet in the middle of a wide gate; a narrow one is a single leaf.
        var leaves = gate > 2.6f ? new[] { mid * .55f, mid * 1.45f } : new[] { mid };
        if (gate > 2.6f)
            wood.Bar(P(mid, .15f) + streetward * .035f, P(mid, 2.05f) + streetward * .035f, .05f, streetward, trim);
        foreach (var c in leaves)
        {
            var centre = P(c, 1.35f) + streetward * .045f;
            var a = along * .28f; var v = up * .38f;
            wood.Bar(centre - v, centre - a, .035f, streetward, trim);
            wood.Bar(centre - a, centre + v, .035f, streetward, trim);
            wood.Bar(centre + v, centre + a, .035f, streetward, trim);
            wood.Bar(centre + a, centre - v, .035f, streetward, trim);
        }
        // VIS-090: hardware that explains how the leaves hang. Two 50 x 8 mm
        // strap hinges per leaf edge at 0.42 m and 1.58 m, bedded 17 mm into the
        // 45 mm plank (never standing off it on an invisible mount), a latch bar
        // over the meeting stile and a keep. Four to six members per gate, real
        // section, so a low sun finds a secondary rhythm on the street line.
        var iron = new Color("494540");
        foreach (var s in new[] { .1f, gate - .1f })
            foreach (var y in new[] { .42f, 1.58f })
                metal.Bar(P(s, y) + streetward * .02f,
                    P(s + (s < mid ? .55f : -.55f), y) + streetward * .02f, .05f, streetward, iron);
        metal.Bar(P(mid, .82f) + streetward * .03f, P(mid, 1.38f) + streetward * .03f, .045f, streetward, iron);
        metal.Box(P(mid + .14f, 1.06f) + streetward * .03f, along * .03f, up * .05f, streetward * .035f, iron);
        // Frieze with the rising sun, and the gable roof over gate and wicket.
        wood.Box(P(mid, 2.35f) + streetward * .01f, along * mid, up * .28f, streetward * .03f, paint.Darkened(.1f));
        var sunCentre = P(mid, 2.1f) + streetward * .05f;
        var rayLength = Math.Min(.48f, mid * .6f);
        for (var r = 0; r <= 8; r++)
        {
            var angle = Mathf.Pi * r / 8f;
            var dir = along * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            wood.Bar(sunCentre + dir * .2f, sunCentre + dir * rayLength, .03f, streetward, sun);
        }
        for (var r = 0; r < 8; r++)
        {
            var a0 = Mathf.Pi * r / 8f; var a1 = Mathf.Pi * (r + 1) / 8f;
            wood.Bar(sunCentre + (along * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * .17f,
                sunCentre + (along * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * .17f, .06f, streetward, sun);
        }
        const float ridge = 3.25f;
        foreach (var side in new[] { -1f, 1f })
        {
            var eave = P(-.3f, 2.72f) + streetward * (side * .55f);
            var eaveEnd = P(total + .2f, 2.72f) + streetward * (side * .55f);
            var top = P(-.3f, ridge); var topEnd = P(total + .2f, ridge);
            var outward = (streetward * side + up).Normalized();
            wood.Quad(eave, eaveEnd, topEnd, top, outward, post);
            var lift = up * .05f;
            snow.Quad(eave + lift, eaveEnd + lift, topEnd + lift, top + lift, outward, snowColour);
        }
        // Wicket, standing open into the yard.
        var hinge = P(total - .2f, 0);
        var open = (along * -.35f - streetward * .94f).Normalized();
        for (var i = 0; i < 6; i++)
            wood.Upright(hinge + open * (.08f + i * .15f), 1.85f, .13f, .04f, open, streetward, paint);
        wood.Bar(hinge + open * .05f + up * .4f, hinge + open * .9f + up * .4f, .05f, streetward, trim);
        wood.Bar(hinge + open * .05f + up * 1.5f, hinge + open * .9f + up * 1.5f, .05f, streetward, trim);
        body.AddChild(new CollisionShape3D
        {
            Name = $"FrontageGate_{body.GetChildCount()}",
            Position = P(mid, 1.1f),
            Rotation = new Vector3(0, Mathf.Atan2(along.X, along.Z), 0),
            Shape = new BoxShape3D { Size = new Vector3(.2f, 2.2f, gate + .1f) }
        });
    }

    /// <summary>
    /// An open wicket in a gap the fence leaves for a door's way to the road:
    /// two posts and the leaf swung into the yard. No collision: it is a way in.
    /// </summary>
    private static void AddFrontageWicket(FrontageMesh wood, FrontageMesh snow, Vector3 start, Vector3 end, Vector3 yardward, int paintIndex)
    {
        var flat = new Vector3(end.X - start.X, 0, end.Z - start.Z);
        var width = flat.Length();
        if (width < .3f) return;
        var along = flat / width;
        var paint = new Color(GatePaints[paintIndex]);
        var post = paint.Darkened(.35f);
        foreach (var p in new[] { start - along * .08f, end + along * .08f })
        {
            wood.Upright(p + Vector3.Down * .05f, 2.0f, .14f, .14f, along, yardward, post);
            snow.Box(p + Vector3.Up * 1.97f, along * .09f, Vector3.Up * .025f, yardward * .09f, new Color("eef2f6"));
        }
        var hinge = end;
        var leafDir = (-along * .3f + yardward * .95f).Normalized();
        var leafWidth = Math.Min(width, 1.1f);
        var boards = Math.Max(3, (int)(leafWidth / .15f));
        for (var i = 0; i < boards; i++)
            wood.Upright(hinge + leafDir * (.08f + i * leafWidth / boards) + Vector3.Up * .06f, 1.7f, .13f, .035f, leafDir,
                new Vector3(-leafDir.Z, 0, leafDir.X), paint);
        var side = new Vector3(-leafDir.Z, 0, leafDir.X);
        foreach (var y in new[] { .45f, 1.45f })
            wood.Bar(hinge + leafDir * .05f + Vector3.Up * y, hinge + leafDir * leafWidth + Vector3.Up * y, .05f, side, new Color("f1efe6"));
    }

    /// <summary>Hide loose presentation fence pieces standing on the new line; never ones with collision.</summary>
    private int HideFencesUnderFrontage(List<FrontageRun> runs, List<(Vector3 Start, Vector3 End)> gates)
    {
        var lines = runs.Select(r => (new Vector2(r.A.X, r.A.Z), new Vector2(r.B.X, r.B.Z)))
            .Concat(gates.Select(g => (new Vector2(g.Start.X, g.Start.Z), new Vector2(g.End.X, g.End.Z)))).ToList();
        var colliders = FindDescendants<CollisionShape3D>(this).Where(s => s.IsInsideTree())
            .Select(s => new Vector2(s.GlobalPosition.X, s.GlobalPosition.Z)).ToList();
        var words = new[] { "Fence", "Picket", "Paling", "Boundary", "Slat" };
        var hidden = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            var name = mesh.Name.ToString();
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null || !words.Any(w => name.Contains(w, StringComparison.Ordinal))) continue;
            if (name.StartsWith("Frontage", StringComparison.Ordinal) || name.StartsWith("Ravine", StringComparison.Ordinal)) continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            if (box.Size.X > 25f || box.Size.Z > 25f) continue;
            var c = box.GetCenter(); var q = new Vector2(c.X, c.Z);
            if (!lines.Any(l => SegmentDistance(q, l.Item1, l.Item2) < 1.1f)) continue;
            if (colliders.Any(p => p.DistanceTo(q) < .8f)) continue;
            for (Node? n = mesh; n is not null && n != this; n = n.GetParent())
                if (IsProtectedGameplayNode(n)) goto next;
            HidePresentationNode(mesh);
            hidden++;
            next:;
        }
        return hidden;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-6f ? 0f : Math.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }
}
