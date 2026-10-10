using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-DEPTH.10 / author photo reference T2 (09.10.2026): the street dwellings of
/// the village exterior kit (VillageParcel variants and the plain dwelling facade)
/// stand on 0.2 m wall slabs ("*_Wall_LOD0", URMAN_Wood_Weathered), so every log
/// house reads as a crate with a striped texture. Each such wall now carries round
/// log courses: 0.26 m crowns whose outer skin stands only ~4 cm proud of the slab
/// (the TimberHomeStyle casings already planted on that face stay in front), the
/// four main walls run their logs past the corners, side walls are offset half a
/// course as a notched frame is laid, and every window/door recess on the same
/// side is cut out. The plain corner posts give way to the crossing log ends.
/// Plaster walls, sheds and bathhouses keep their authored walls. On the hero
/// house the outer slab plane gives way to full round crowns; the backing and
/// opening reveals remain. One crown mesh per wall keeps the cost bounded;
/// collision and LOD are untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const float WallCourse = .26f;
    private const float WallLogDepth = .18f;
    private const float WallLogProud = .035f;
    private const float WallLogOverhang = .18f;
    private const int WallLogSegments = 14;
    private static readonly string[] MainWallSides = ["_Street_Wall_", "_Rear_Wall_", "_Left_Wall_", "_Right_Wall_"];
    private static readonly string[] SkippedWallOwners = ["HeroYardShed", "OutbuildingShed", "_Shed_", "Banya"];

    private static int BuildDwellingLogWalls(Node3D root)
    {
        var walls = 0;
        var pieces = 0;
        var ownerless = 0;
        var footed = 0;
        foreach (var wall in FindDescendants<MeshInstance3D>(root).ToArray())
        {
            var name = wall.Name.ToString();
            if (!name.Contains("_Wall_LOD0", StringComparison.Ordinal) || wall.HasMeta("logCrownBuilt")) continue;
            if (SkippedWallOwners.Any(owner => name.Contains(owner, StringComparison.Ordinal))) continue;
            if (!wall.Visible || wall.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() != 1) continue;
            // The hero house (ACT1-DEPTH.9) stands on slabs that carry the log texture
            // itself; its thin course pieces only showed as dark rods along the wall.
            var hero = name.StartsWith("HeroHouse_", StringComparison.Ordinal);
            if (mesh.SurfaceGetMaterial(0)?.ResourceName != (hero ? "URMAN_Hero_Log" : "URMAN_Wood_Weathered")) continue;
            if (wall.GetParent() is not Node3D parent) continue;
            // Public buildings borrow dwelling facades but must read as their own
            // type (ACT1-PUBLIC.FACADES): shop, school and council/DK stay unlogged.
            if (IsPublicFacadeWall(wall)) continue;
            wall.SetMeta("logCrownBuilt", true);

            var points = mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var min = points[0];
            var max = points[0];
            foreach (var p in points) { min = min.Min(p); max = max.Max(p); }
            var size = max - min;
            var alongX = size.X >= size.Z;
            var thickness = alongX ? size.Z : size.X;
            if (thickness is < .08f or > .4f || size.Y < 1.2f || Mathf.Max(size.X, size.Z) < 1.0f) continue;

            // Outward: away from the dwelling's own centre, measured in the wall's space.
            // The house is the nearest "*_Dwelling"/"*Facade" ancestor: kit meshes can each sit
            // in their own node, so the direct parent alone does not know where "inside" is.
            var owner = DwellingOwner(wall);
            if (owner is null) { ownerless++; owner = parent; }
            var houseParts = owner.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToArray();
            var dwelling = houseParts
                .Where(m => m.Name.ToString().Contains("_Wall_LOD0", StringComparison.Ordinal))
                .Select(m => m.GlobalTransform * m.GetAabb().GetCenter()).ToArray();
            // The house's own foundation sits exactly under it; averaging every wall
            // of an owner that also holds entry halls or a neighbour drifted outside
            // some houses and put their courses on the inner face (round 11 frames).
            var footing = houseParts.Where(m => m.Name.ToString().Contains("_Foundation_LOD0", StringComparison.Ordinal)
                    || m.Name.ToString().Contains("_FootingCap_LOD0", StringComparison.Ordinal))
                .OrderByDescending(m => m.GetAabb().Size.X * m.GetAabb().Size.Z).FirstOrDefault();
            var houseCentre = footing is not null ? footing.GlobalTransform * footing.GetAabb().GetCenter()
                : dwelling.Length > 0 ? dwelling.Aggregate(Vector3.Zero, (a, b) => a + b) / dwelling.Length : parent.GlobalPosition;
            if (footing is not null) footed++;
            var toWall = wall.GlobalTransform.AffineInverse();
            var centreLocal = toWall * houseCentre;
            var mid = (min + max) * .5f;
            var outward = (alongX ? mid.Z - centreLocal.Z : mid.X - centreLocal.X) >= 0f ? 1f : -1f;
            var face = alongX ? (outward > 0 ? max.Z : min.Z) : (outward > 0 ? max.X : min.X);

            // Openings on this side: every recess/door/glass of the same side prefix.
            var sidePrefix = name[..name.IndexOf("Wall_LOD0", StringComparison.Ordinal)];
            var openings = new List<Aabb>();
            foreach (var part in houseParts)
            {
                var partName = part.Name.ToString();
                if (!partName.StartsWith(sidePrefix, StringComparison.Ordinal) || partName == name || part.Mesh is null) continue;
                if (!(partName.Contains("Recess", StringComparison.Ordinal) || partName.Contains("_Glass", StringComparison.Ordinal)
                    || partName.Contains("_Leaf", StringComparison.Ordinal))) continue;
                var box = part.GetAabb();
                var corners = Enumerable.Range(0, 8).Select(i => toWall * (part.GlobalTransform * box.GetEndpoint(i))).ToArray();
                var local = new Aabb(corners[0], Vector3.Zero);
                foreach (var c in corners) local = local.Expand(c);
                openings.Add(local);
            }

            // The hero house keeps its own notched corner ends, so its crown stops at the corners.
            var main = !hero && MainWallSides.Any(side => name.Contains(side, StringComparison.Ordinal));
            var sideWall = name.Contains("_Left_Wall_", StringComparison.Ordinal) || name.Contains("_Right_Wall_", StringComparison.Ordinal);
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var courses = Mathf.Max(3, Mathf.RoundToInt(size.Y / WallCourse));
            var course = size.Y / courses;
            var start = (alongX ? min.X : min.Z) - (main ? WallLogOverhang : 0f);
            var end = (alongX ? max.X : max.Z) + (main ? WallLogOverhang : 0f);
            // On the reference house, reveal the complete round crown rather
            // than a shallow cap in front of the flat wall slab. Keep its outer
            // limit in place so the authored casings and reveals stay clear.
            var logRadiusDepth = hero ? .12f : WallLogDepth * .5f;
            var depthCentre = face + outward * (WallLogProud - logRadiusDepth);
            var logs = 0;
            for (var k = 0; k < courses; k++)
            {
                var centreY = min.Y + course * (k + .5f) + (sideWall ? course * .5f : 0f);
                if (centreY + course * .5f > max.Y + .01f) continue;
                var cuts = openings
                    .Where(o => centreY + course * .42f > o.Position.Y && centreY - course * .42f < o.End.Y)
                    .Select(o => alongX ? (o.Position.X - .02f, o.End.X + .02f) : (o.Position.Z - .02f, o.End.Z + .02f))
                    .ToList();
                foreach (var (from, to) in SubtractSpans(start, end, cuts))
                {
                    if (to - from < .2f) continue;
                    AppendWallLog(surface, alongX, from, to, centreY, depthCentre,
                        course * (hero ? .49f : .64f), logRadiusDepth, hero);
                    logs++;
                }
            }
            if (logs == 0) continue;
            surface.SetMaterial(PainterlyMaterialLibrary.ForColor(hero ? "8b7763" : "85715c", "wood_log_uv"));
            if (hero)
            {
                // The old slender courses of this side would poke through as rods.
                var sideLogs = sidePrefix + "Log_";
                foreach (var oldCourse in houseParts.Where(m => m.Name.ToString().StartsWith(sideLogs, StringComparison.Ordinal)))
                {
                    oldCourse.Visible = false;
                    oldCourse.SetMeta("suppressionReason", "hero wall crown replaces the slender course (ACT1-DEPTH.9/T2)");
                }
            }
            var crown = new MeshInstance3D
            {
                Name = name.Replace("_Wall_LOD0", "_LogCrown"),
                Mesh = surface.Commit(),
                Transform = wall.Transform,
                VisibilityRangeEnd = wall.VisibilityRangeEnd,
                VisibilityRangeEndMargin = wall.VisibilityRangeEndMargin,
                VisibilityRangeFadeMode = wall.VisibilityRangeFadeMode
            };
            crown.SetMeta("presentationOnly", true);
            crown.SetMeta("reference", "author photo T2 09.10.2026: round log courses with crossing corner ends");
            parent.AddChild(crown);
            if (hero)
            {
                // Remove only the exterior plane that masks the crown profile.
                // Keep the wall's inner face and window/door reveal surfaces,
                // as well as the separately authored collision proxy.
                wall.Mesh = RemoveHeroWallFront(mesh, alongX, face);
                wall.SetMeta("logCrownBacking", "T2: exterior plane replaced; inner backing and opening reveals preserved");
            }
            walls++;
            pieces += logs;

            if (!main) continue;
            foreach (var post in houseParts
                .Where(m => m.Name.ToString().StartsWith(sidePrefix[..sidePrefix.LastIndexOf('_', sidePrefix.Length - 2)] + "_Corner_", StringComparison.Ordinal)))
            {
                post.Visible = false;
                post.SetMeta("suppressionReason", "crossing log ends replace the plain corner post (ACT1-DEPTH.10/T2)");
            }
        }
        if (walls > 0) GD.Print($"act1-dwelling-log-walls: walls={walls} logs={pieces} ownerless={ownerless} footed={footed}");
        return walls;
    }

    private static ArrayMesh RemoveHeroWallFront(ArrayMesh source, bool alongX, float face)
    {
        var arrays = source.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var uv = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil
            ? Array.Empty<Vector2>() : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        bool OnFront(int i) => Mathf.Abs((alongX ? vertices[i].Z : vertices[i].X) - face) < .001f;
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            if (OnFront(indices[i]) && OnFront(indices[i + 1]) && OnFront(indices[i + 2])) continue;
            for (var j = 0; j < 3; j++)
            {
                var index = indices[i + j];
                tool.SetNormal(normals[index]);
                // The source wall slab has no UV channel. Give retained backing
                // and reveals metric coordinates; keep authored UVs when present.
                tool.SetUV(index < uv.Length ? uv[index]
                    : new Vector2(alongX ? vertices[index].X : vertices[index].Z, vertices[index].Y));
                tool.AddVertex(vertices[index]);
            }
        }
        tool.SetMaterial(source.SurfaceGetMaterial(0));
        var result = tool.Commit();
        result.ResourceName = source.ResourceName + "_CrownBacking";
        return result;
    }

    private static Node3D? DwellingOwner(Node wall)
    {
        for (var node = wall.GetParent(); node is not null; node = node.GetParent())
        {
            var name = node.Name.ToString();
            if (node is Node3D owner && (name.EndsWith("_Dwelling", StringComparison.Ordinal)
                || name.EndsWith("Facade", StringComparison.Ordinal) || name.Contains("_Dwelling_", StringComparison.Ordinal)))
                return owner;
        }
        return null;
    }

    private static readonly string[] PublicFacadeOwners = ["WestReturnMidFacade", "EastReturnMidFacade", "EastStreetHorizonFacade"];

    private static bool IsPublicFacadeWall(Node wall)
    {
        for (var node = wall.GetParent(); node is not null; node = node.GetParent())
            if (PublicFacadeOwners.Contains(node.Name.ToString())) return true;
        return false;
    }

    private static IEnumerable<(float From, float To)> SubtractSpans(float a, float b, List<(float From, float To)> cuts)
    {
        var spans = new List<(float, float)> { (a, b) };
        foreach (var (c0, c1) in cuts)
        {
            var next = new List<(float, float)>();
            foreach (var (s0, s1) in spans)
            {
                if (c1 <= s0 || c0 >= s1) { next.Add((s0, s1)); continue; }
                if (c0 > s0) next.Add((s0, c0));
                if (c1 < s1) next.Add((c1, s1));
            }
            spans = next;
        }
        return spans;
    }

    private static void AppendWallLog(SurfaceTool surface, bool alongX, float from, float to, float y, float d, float ry, float rd, bool referenceHero)
    {
        Vector3 P(float along, float depth, float height) => alongX ? new(along, height, depth) : new(depth, height, along);
        void Tri((Vector3 P, Vector3 N, Vector2 U) a, (Vector3 P, Vector3 N, Vector2 U) b, (Vector3 P, Vector3 N, Vector2 U) c)
        {
            // Same front-face rule as TimberHomeStyle.AppendChamferedBox.
            if ((b.P - a.P).Cross(c.P - a.P).Dot(a.N + b.N + c.N) < 0f) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c }) { surface.SetNormal(v.N); surface.SetUV(v.U); surface.AddVertex(v.P); }
        }
        var ring = new (float D, float Y, Vector3 N, float Arc)[WallLogSegments + 1];
        var arc = 0f;
        for (var i = 0; i <= WallLogSegments; i++)
        {
            var t = Mathf.Tau * i / WallLogSegments;
            var dd = rd * Mathf.Cos(t);
            var yy = ry * Mathf.Sin(t);
            if (i > 0) arc += new Vector2(dd - ring[i - 1].D, yy - ring[i - 1].Y).Length();
            ring[i] = (dd, yy, P(0f, dd / (rd * rd), yy / (ry * ry)).Normalized(), arc);
        }
        for (var i = 0; i < WallLogSegments; i++)
        {
            var r0 = ring[i];
            var r1 = ring[i + 1];
            // W01's photographed fibre is vertical in the texture (V).
            // On the reference house that axis follows the log length.
            Vector2 Uv(float length, float girth) => referenceHero ? new(girth, length) : new(length, girth);
            var a = (P(from, d + r0.D, y + r0.Y), r0.N, Uv(from, r0.Arc));
            var b = (P(to, d + r0.D, y + r0.Y), r0.N, Uv(to, r0.Arc));
            var c = (P(to, d + r1.D, y + r1.Y), r1.N, Uv(to, r1.Arc));
            var e = (P(from, d + r1.D, y + r1.Y), r1.N, Uv(from, r1.Arc));
            Tri(a, b, c);
            Tri(a, c, e);
        }
        foreach (var (at, sign) in new[] { (from, -1f), (to, 1f) })
        {
            var normal = P(sign, 0f, 0f);
            var hub = (P(at, d, y), normal, new Vector2(.5f, .5f));
            for (var i = 0; i < WallLogSegments; i++)
            {
                var r0 = ring[i];
                var r1 = ring[i + 1];
                Tri(hub, (P(at, d + r0.D, y + r0.Y), normal, new Vector2(.5f + .5f * r0.D / rd, .5f + .5f * r0.Y / ry)),
                    (P(at, d + r1.D, y + r1.Y), normal, new Vector2(.5f + .5f * r1.D / rd, .5f + .5f * r1.Y / ry)));
            }
        }
    }
}
