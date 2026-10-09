using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-DEPTH.11 / author photo reference T2 (09.10.2026): Agent B's village
/// houses are a plain 24-vertex box ("*_Body", AB_log_wall) wrapped by thin
/// full-footprint plates ("*_LogBand*"), so every log house on the street reads
/// as a crate with painted stripes. Each visible log body now gets a real round
/// log crown: 0.26 m courses whose ends run past the corners and whose two
/// directions are offset by half a course, as a notched frame is laid. Window and
/// door openings of the same house are measured from their own frame/glass/door
/// meshes and the courses stop at them. The flat bands and corner boards give way;
/// the body box stays as the core behind the logs, its own collision untouched.
/// One ArrayMesh per house keeps the draw cost at one call.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const float CrownCourse = .26f;
    private const float CrownDepth = .17f;
    private const float CrownOverhang = .20f;
    private const int CrownSegments = 14;

    private static int BuildAgentBLogCrowns(Node3D root)
    {
        var kit = FindDescendants<Node3D>(root).FirstOrDefault(n => n.Name == "AgentB_VillageBuildingsKit");
        if (kit is null || kit.HasMeta("logCrownsBuilt")) return 0;
        kit.SetMeta("logCrownsBuilt", true);
        var meshes = FindDescendants<MeshInstance3D>(kit).ToArray();
        var built = 0;
        var skipped = new List<string>();
        foreach (var body in meshes)
        {
            var name = body.Name.ToString();
            if (!(name.EndsWith("_Body", StringComparison.Ordinal) || name.EndsWith("_AnnexBody", StringComparison.Ordinal))) continue;
            // Own visibility, not IsVisibleInTree: the zone may be parked while the
            // world is assembled; suppressed duplicates (HouseBabai_*) are hidden directly.
            string? skip = !body.Visible ? "hidden"
                : body.Mesh is not ArrayMesh candidate ? "mesh=" + (body.Mesh?.GetType().Name ?? "null")
                : candidate.GetSurfaceCount() != 1 ? "surfaces=" + candidate.GetSurfaceCount()
                : candidate.SurfaceGetMaterial(0)?.ResourceName != "AB_log_wall" ? "material=" + candidate.SurfaceGetMaterial(0)?.ResourceName
                : null;
            if (skip is not null) { skipped.Add(name + ":" + skip); continue; }
            var mesh = (ArrayMesh)body.Mesh!;
            var house = name[..name.IndexOf('_')];
            var crown = BuildLogCrown(body, mesh, meshes.Where(m => m != body
                && m.Name.ToString().StartsWith(house + "_", StringComparison.Ordinal)).ToArray(), name.EndsWith("_AnnexBody", StringComparison.Ordinal));
            if (crown is null) { skipped.Add(name + ":no-crown"); continue; }
            body.GetParent().AddChild(crown);
            crown.GlobalTransform = body.GlobalTransform;
            foreach (var part in meshes.Where(m => m.Name.ToString().StartsWith(house + "_", StringComparison.Ordinal)
                && (m.Name.ToString().Contains("_LogBand", StringComparison.Ordinal) || m.Name.ToString().Contains("_CornerTrim", StringComparison.Ordinal))))
            {
                if (name.EndsWith("_AnnexBody", StringComparison.Ordinal)) continue;
                part.Visible = false;
                part.SetMeta("suppressionReason", "replaced by the round log crown (ACT1-DEPTH.11/T2)");
            }
            built++;
        }
        GD.Print($"act1-log-crowns: houses={built} skipped={skipped.Count} [{string.Join(", ", skipped.Take(12))}]");
        return built;
    }

    private static MeshInstance3D? BuildLogCrown(MeshInstance3D body, ArrayMesh mesh, MeshInstance3D[] houseParts, bool annex)
    {
        var points = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (points.Length == 0) return null;
        var min = points[0];
        var max = points[0];
        foreach (var p in points) { min = min.Min(p); max = max.Max(p); }
        var size = max - min;
        if (size.X < 1.2f || size.Z < 1.2f || size.Y < 1.0f) return null;

        // Openings in the body's own space: one box per window/door group.
        var toLocal = body.GlobalTransform.AffineInverse();
        var groups = new Dictionary<string, Aabb>(StringComparer.Ordinal);
        foreach (var part in houseParts)
        {
            var partName = part.Name.ToString();
            var isWindow = partName.Contains("_Win", StringComparison.Ordinal)
                && (partName.Contains("Frame", StringComparison.Ordinal) || partName.EndsWith("_Glass", StringComparison.Ordinal));
            var isDoor = partName.Contains("_Door", StringComparison.Ordinal) && !partName.Contains("Threshold", StringComparison.Ordinal);
            if ((!isWindow && !isDoor) || part.Mesh is null) continue;
            var key = partName[..(partName.LastIndexOf('_') is var cut and > 0 ? cut : partName.Length)];
            var box = part.GetAabb();
            var corners = Enumerable.Range(0, 8).Select(i => toLocal * (part.GlobalTransform * box.GetEndpoint(i))).ToArray();
            var local = new Aabb(corners[0], Vector3.Zero);
            foreach (var c in corners) local = local.Expand(c);
            groups[key] = groups.TryGetValue(key, out var existing) ? existing.Merge(local) : local;
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var courses = Mathf.Max(3, Mathf.RoundToInt(size.Y / CrownCourse));
        var course = size.Y / courses;
        var logs = 0;
        // Walls: (axis along the wall, fixed coordinate, outward sign). X-running
        // walls start at the sill; Z-running walls are offset by half a course.
        foreach (var (alongX, face, outward) in new[]
                 { (true, max.Z, 1f), (true, min.Z, -1f), (false, max.X, 1f), (false, min.X, -1f) })
        {
            var start = alongX ? min.X : min.Z;
            var end = alongX ? max.X : max.Z;
            var shift = alongX ? 0f : course * .5f;
            for (var k = 0; k < courses; k++)
            {
                var y0 = min.Y + shift + k * course;
                var centreY = y0 + course * .5f;
                if (centreY + course * .5f > max.Y + .02f) continue;
                var a = start - CrownOverhang;
                var b = end + CrownOverhang;
                // The wall face of the log sits a little proud of the body face.
                var d = face + outward * (CrownDepth * .5f - .05f);
                var cuts = new List<(float From, float To)>();
                foreach (var opening in groups.Values)
                {
                    var openFace = alongX ? opening.Position.Z + opening.Size.Z * .5f : opening.Position.X + opening.Size.X * .5f;
                    if (Mathf.Abs(openFace - face) > .45f) continue;
                    if (centreY + course * .45f < opening.Position.Y || centreY - course * .45f > opening.End.Y) continue;
                    var o0 = alongX ? opening.Position.X : opening.Position.Z;
                    var o1 = alongX ? opening.End.X : opening.End.Z;
                    cuts.Add((o0 - .03f, o1 + .03f));
                }
                foreach (var (from, to) in Subtract(a, b, cuts))
                {
                    if (to - from < .25f) continue;
                    AppendCrownLog(surface, alongX, from, to, centreY, d, course * .5f, CrownDepth * .5f);
                    logs++;
                }
            }
        }
        if (logs == 0) return null;
        surface.SetMaterial(PainterlyMaterialLibrary.ForColor(annex ? "76634f" : "7f6b56", "wood_log_uv"));
        var crown = new MeshInstance3D
        {
            Name = body.Name + "_LogCrown",
            Mesh = surface.Commit(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On
        };
        crown.SetMeta("presentationOnly", true);
        crown.SetMeta("logCourses", courses);
        crown.SetMeta("logPieces", logs);
        crown.SetMeta("openingsCut", groups.Count);
        return crown;
    }

    private static IEnumerable<(float From, float To)> Subtract(float a, float b, List<(float From, float To)> cuts)
    {
        var pieces = new List<(float, float)> { (a, b) };
        foreach (var (c0, c1) in cuts)
        {
            var next = new List<(float, float)>();
            foreach (var (p0, p1) in pieces)
            {
                if (c1 <= p0 || c0 >= p1) { next.Add((p0, p1)); continue; }
                if (c0 > p0) next.Add((p0, c0));
                if (c1 < p1) next.Add((c1, p1));
            }
            pieces = next;
        }
        return pieces;
    }

    private static void AppendCrownLog(SurfaceTool surface, bool alongX, float from, float to, float y, float d, float ry, float rd)
    {
        Vector3 P(float along, float depth, float height) => alongX ? new(along, height, depth) : new(depth, height, along);
        Vector3 N(float along, float depth, float height) => P(along, depth, height).Normalized();
        void Tri((Vector3 P, Vector3 N, Vector2 U) a, (Vector3 P, Vector3 N, Vector2 U) b, (Vector3 P, Vector3 N, Vector2 U) c)
        {
            // Same front-face rule as TimberHomeStyle.AppendChamferedBox.
            if ((b.P - a.P).Cross(c.P - a.P).Dot(a.N + b.N + c.N) < 0f) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c }) { surface.SetNormal(v.N); surface.SetUV(v.U); surface.AddVertex(v.P); }
        }
        var ring = new (float D, float Y, Vector3 Nrm, float Arc)[CrownSegments + 1];
        var arc = 0f;
        for (var i = 0; i <= CrownSegments; i++)
        {
            var t = Mathf.Tau * i / CrownSegments;
            var dd = rd * Mathf.Cos(t);
            var yy = ry * Mathf.Sin(t);
            if (i > 0) arc += new Vector2(dd - ring[i - 1].D, yy - ring[i - 1].Y).Length();
            ring[i] = (dd, yy, N(0f, dd / (rd * rd), yy / (ry * ry)), arc);
        }
        for (var i = 0; i < CrownSegments; i++)
        {
            var r0 = ring[i];
            var r1 = ring[i + 1];
            var a = (P(from, d + r0.D, y + r0.Y), r0.Nrm, new Vector2(from, r0.Arc));
            var b = (P(to, d + r0.D, y + r0.Y), r0.Nrm, new Vector2(to, r0.Arc));
            var c = (P(to, d + r1.D, y + r1.Y), r1.Nrm, new Vector2(to, r1.Arc));
            var e = (P(from, d + r1.D, y + r1.Y), r1.Nrm, new Vector2(from, r1.Arc));
            Tri(a, b, c);
            Tri(a, c, e);
        }
        foreach (var (at, sign) in new[] { (from, -1f), (to, 1f) })
        {
            var normal = N(sign, 0f, 0f);
            var hub = (P(at, d, y), normal, Vector2.Zero);
            for (var i = 0; i < CrownSegments; i++)
            {
                var r0 = ring[i];
                var r1 = ring[i + 1];
                Tri(hub, (P(at, d + r0.D, y + r0.Y), normal, new Vector2(r0.D, r0.Y)),
                    (P(at, d + r1.D, y + r1.Y), normal, new Vector2(r1.D, r1.Y)));
            }
        }
    }
}
