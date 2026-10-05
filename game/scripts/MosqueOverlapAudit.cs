using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Mosque subtree overlap detector for the rebuilt Kara-Urman mosque.
///
/// Screen phase: every visible MeshInstance3D under the passed root is put into
/// world AABBs; pairs whose AABB overlap exceeds the tolerance (default 1 cm)
/// become candidates. Because the mosque contains annular spiral sectors and
/// merged facade shells, plain AABBs alone report the ring around the centre
/// mast and adjacent treads as deep overlaps, so each non-allowlisted candidate
/// runs a triangle-triangle narrow phase: only pairs with an actual surface
/// crossing are reported. Pairs that merely touch (shared sector faces, props
/// standing on shelves or carpets) clear the narrow phase.
///
/// The same pass checks every StaticBody3D/CollisionShape3D under the root for
/// a missing, hidden or displaced (> 5 cm) visual counterpart.
///
/// Intentional overlaps live in three places, all read here:
///   1. node meta "overlapAuditIgnore" = true  -> node and subtree skipped;
///   2. node meta "overlapAuditGroup" = "...",  -> pairs inside one group are
///      allowlisted (the project has no existing authoring-group convention);
///   3. the built-in rule list plus an optional JSON file with entries
///      { "rules": [ { "a": "wildcard", "b": "wildcard", "reason": "..." } ] }
///      (path argument, URMAN_MOSQUE_OVERLAP_ALLOWLIST, or the default
///      res://content/world/mosque_overlap_allowlist.v1.json if it exists).
///
/// Diagnostic, not a gate: findings are printed, the summary line is
/// machine-readable, and nothing throws on ordinary geometry. Headless-safe
/// (no rendering, no physics queries) and allocation-bounded (structs, arrays,
/// a swept AABB list and an explicit narrow-phase triangle budget).
/// </summary>
public static class MosqueOverlapAudit
{
    public const float DefaultToleranceMetres = .01f;
    public const float StructuralToleranceMetres = .02f;
    public const float ContactDisplacementMetres = .05f;
    public const string DefaultAllowlistPath = "res://content/world/mosque_overlap_allowlist.v1.json";

    private const int MaxNarrowPhaseTriangles = 4000;
    private const long MaxTriangleTests = 12_000_000;
    private const int MaxFindingsPrinted = 40;
    private const int MaxAllowlistedPrinted = 20;
    private const int AssemblyMeshLimit = 64;

    public readonly record struct Finding(string Kind, string A, string B, float PenetrationMetres, string Note)
    {
        public string ToLine() => FormattableString.Invariant(
            $"mosque-overlap-{Kind}: a={A} b={B} penetration={PenetrationMetres * 1000f:0.#}mm note={Note}");
    }

    public sealed class Report
    {
        public string RootPath = "";
        public int Meshes;
        public int MeshNodesIgnored;
        public int CharacterMeshesSkipped;
        public int EnvelopeMeshesSkipped;
        public int CandidatePairs;
        public int TouchingPairs;
        public int ContactsChecked;
        public bool NarrowPhaseBudgetExceeded;
        public float MaxPenetrationMetres;
        public readonly List<Finding> Pairs = new();
        public readonly List<Finding> Structural = new();
        public readonly List<Finding> ContactIssues = new();
        public readonly List<(string A, string B, string Reason)> Allowlisted = new();
        public readonly List<string> EnvelopeNames = new();

        public string SummaryLine => FormattableString.Invariant(
            $"mosque-overlap-audit: pairs={Pairs.Count} maxPenetration={MaxPenetrationMetres * 1000f:0.#}mm allowlisted={Allowlisted.Count} propStructure={Structural.Count} contactIssues={ContactIssues.Count} meshes={Meshes} envelopes={EnvelopeMeshesSkipped} touching={TouchingPairs} maxPenetrationMetres={MaxPenetrationMetres:0.####}");

        public void Print()
        {
            GD.Print(SummaryLine);
            PrintList("pair", Pairs);
            PrintList("prop-structure", Structural);
            PrintList("contact", ContactIssues);
            if (Allowlisted.Count > 0)
            {
                for (var index = 0; index < Allowlisted.Count; index++)
                {
                    if (index >= MaxAllowlistedPrinted)
                    {
                        GD.Print($"mosque-overlap-allowlisted: +{Allowlisted.Count - MaxAllowlistedPrinted} more");
                        break;
                    }
                    var (a, b, reason) = Allowlisted[index];
                    GD.Print($"mosque-overlap-allowlisted: a={a} b={b} reason={reason}");
                }
            }
            if (EnvelopeMeshesSkipped > 0)
                GD.Print("mosque-overlap-envelope: skipped=" + string.Join(',', EnvelopeNames));
            if (NarrowPhaseBudgetExceeded)
                GD.PushWarning("mosque-overlap-audit: narrow-phase triangle budget exceeded; remaining pairs are AABB-only.");
        }

        private static void PrintList(string kind, List<Finding> findings)
        {
            for (var index = 0; index < findings.Count; index++)
            {
                if (index >= MaxFindingsPrinted)
                {
                    GD.Print($"mosque-overlap-{kind}: +{findings.Count - MaxFindingsPrinted} more");
                    break;
                }
                GD.Print(findings[index].ToLine());
            }
        }

        public void WriteMarkdown(string path, string rootPath)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("# Mosque overlap audit (runtime detector)");
            writer.WriteLine();
            writer.WriteLine(FormattableString.Invariant($"Root: `{rootPath}`"));
            writer.WriteLine(FormattableString.Invariant($"Summary: `{SummaryLine}`"));
            writer.WriteLine(FormattableString.Invariant(
                $"Tolerance: {DefaultToleranceMetres * 1000f:0.#} mm mesh pairs, {StructuralToleranceMetres * 1000f:0.#} mm prop-vs-structure, contact displacement {ContactDisplacementMetres * 100f:0.#} cm."));
            writer.WriteLine();
            WriteSection(writer, "Mesh pairs (confirmed by triangle crossing)", Pairs);
            WriteSection(writer, "Props intersecting walls / floor / ceiling", Structural);
            WriteSection(writer, "Collision shapes without an exact visual counterpart", ContactIssues);
            writer.WriteLine("## Allowlisted intentional overlaps");
            writer.WriteLine();
            if (Allowlisted.Count == 0) writer.WriteLine("- none");
            foreach (var (a, b, reason) in Allowlisted) writer.WriteLine($"- `{a}` x `{b}` — {reason}");
            if (EnvelopeMeshesSkipped > 0)
            {
                writer.WriteLine();
                writer.WriteLine("Envelope meshes skipped (merged shells whose single AABB encloses the whole assembly): "
                    + string.Join(", ", EnvelopeNames));
            }
        }

        private static void WriteSection(TextWriter writer, string title, List<Finding> findings)
        {
            writer.WriteLine("## " + title);
            writer.WriteLine();
            if (findings.Count == 0) { writer.WriteLine("- none"); writer.WriteLine(); return; }
            foreach (var finding in findings)
                writer.WriteLine(FormattableString.Invariant(
                    $"- {finding.PenetrationMetres * 1000f:0.#} mm — `{finding.A}` x `{finding.B}` ({finding.Note})"));
            writer.WriteLine();
        }
    }

    /// <summary>
    /// Runs the audit on an already-built mosque subtree (any Node3D; the world
    /// passes Act1CoreWorldGreybox/VillageMosqueComplex). Returns and prints the
    /// summary and one machine-readable line per finding.
    /// </summary>
    public static Report Run(Node3D mosqueRoot, string? allowlistPath = null)
    {
        ArgumentNullException.ThrowIfNull(mosqueRoot);
        var report = new Report { RootPath = mosqueRoot.GetPath().ToString() };
        var rules = LoadAllowlist(allowlistPath);
        var entries = new List<Entry>(256);
        Collect(mosqueRoot, "", null, entries, report);
        report.Meshes = entries.Count;
        entries.Sort(static (left, right) => left.WorldBox.Position.X.CompareTo(right.WorldBox.Position.X));

        var triangleTests = 0L;
        for (var i = 0; i < entries.Count; i++)
        {
            var a = entries[i];
            for (var j = i + 1; j < entries.Count; j++)
            {
                var b = entries[j];
                if (b.WorldBox.Position.X > a.WorldBox.End.X) break;
                if (!AabbOverlap(a.WorldBox, b.WorldBox, out var penetration)) continue;
                if (penetration <= DefaultToleranceMetres) continue;
                report.CandidatePairs++;
                if (IsAllowlisted(a, b, rules, out var reason))
                {
                    report.Allowlisted.Add((a.Path, b.Path, reason));
                    continue;
                }
                if (!MeshesCross(a, b, ref triangleTests, out var note))
                {
                    report.TouchingPairs++;
                    continue;
                }
                var finding = new Finding("pair", a.Path, b.Path, penetration, note);
                report.Pairs.Add(finding);
                report.MaxPenetrationMetres = MathF.Max(report.MaxPenetrationMetres, penetration);
                if (penetration > StructuralToleranceMetres && IsPropStructurePair(a, b))
                    report.Structural.Add(finding with { Kind = "prop-structure" });
            }
        }

        if (triangleTests >= MaxTriangleTests) report.NarrowPhaseBudgetExceeded = true;
        AuditContacts(mosqueRoot, report);
        report.Pairs.Sort(static (left, right) => right.PenetrationMetres.CompareTo(left.PenetrationMetres));
        report.Structural.Sort(static (left, right) => right.PenetrationMetres.CompareTo(left.PenetrationMetres));
        report.ContactIssues.Sort(static (left, right) => right.PenetrationMetres.CompareTo(left.PenetrationMetres));
        return report;
    }

    // ---------------------------------------------------------------- screen

    private sealed class Entry
    {
        public required MeshInstance3D Node;
        public required string Path;
        public required Aabb WorldBox;
        public required string Group;
        public Tri3d[]? Triangles;
    }

    private static void Collect(Node node, string parentPath, string? group, List<Entry> entries, Report report)
    {
        foreach (var child in node.GetChildren())
        {
            var childGroup = group;
            if (child is Node3D marker)
            {
                if (marker.HasMeta("overlapAuditIgnore") && marker.GetMeta("overlapAuditIgnore").AsBool())
                {
                    report.MeshNodesIgnored++;
                    continue;
                }
                if (marker.HasMeta("characterId"))
                {
                    report.CharacterMeshesSkipped += CountMeshes(marker);
                    continue;
                }
                if (marker.HasMeta("overlapAuditGroup"))
                    childGroup = marker.GetMeta("overlapAuditGroup").AsString();
            }
            var name = child.Name.ToString();
            var path = parentPath.Length == 0 ? name : parentPath + "/" + name;
            if (child is MeshInstance3D mesh && mesh.Mesh is not null && mesh.VisibilityRangeBegin <= 0f
                && mesh.IsVisibleInTree())
            {
                var box = mesh.GlobalTransform * mesh.GetAabb();
                if (IsUsable(box))
                {
                    if (IsEnvelope(mesh, name))
                    {
                        report.EnvelopeMeshesSkipped++;
                        report.EnvelopeNames.Add(path);
                    }
                    else entries.Add(new Entry { Node = mesh, Path = path, WorldBox = box, Group = childGroup ?? "" });
                }
            }
            Collect(child, path, childGroup, entries, report);
        }
    }

    private static int CountMeshes(Node node)
    {
        var count = node is MeshInstance3D ? 1 : 0;
        foreach (var child in node.GetChildren()) count += CountMeshes(child);
        return count;
    }

    private static bool IsUsable(Aabb box) =>
        float.IsFinite(box.Position.X) && float.IsFinite(box.Position.Y) && float.IsFinite(box.Position.Z)
        && float.IsFinite(box.Size.X) && float.IsFinite(box.Size.Y) && float.IsFinite(box.Size.Z);

    private static bool IsEnvelope(MeshInstance3D mesh, string surfaceName) =>
        // Batched facade trim paints thousands of tiny quads along every wall;
        // its single AABB encloses the whole hall and is not a solid volume.
        surfaceName == "MosqueTimberBoardJoints"
        || (mesh.HasMeta("overlapAuditEnvelope") && mesh.GetMeta("overlapAuditEnvelope").AsBool());

    private static bool AabbOverlap(in Aabb a, in Aabb b, out float penetration)
    {
        var x = MathF.Min(a.End.X, b.End.X) - MathF.Max(a.Position.X, b.Position.X);
        var y = MathF.Min(a.End.Y, b.End.Y) - MathF.Max(a.Position.Y, b.Position.Y);
        var z = MathF.Min(a.End.Z, b.End.Z) - MathF.Max(a.Position.Z, b.Position.Z);
        penetration = MathF.Min(x, MathF.Min(y, z));
        return x > 0f && y > 0f && z > 0f;
    }

    // ----------------------------------------------------------- narrow phase

    private static bool MeshesCross(Entry a, Entry b, ref long triangleTests, out string note)
    {
        a.Triangles ??= WorldTriangles(a.Node);
        b.Triangles ??= WorldTriangles(b.Node);
        if (a.Triangles is null || b.Triangles is null)
        {
            note = "aabb-only (mesh has no triangle faces or exceeds the narrow-phase budget)";
            return true;
        }
        var ta = a.Triangles;
        var tb = b.Triangles;
        if (ta.Length == tb.Length && SameBox(a.WorldBox, b.WorldBox))
        {
            note = "coincident duplicate meshes";
            return true;
        }
        for (var i = 0; i < ta.Length; i++)
            for (var j = 0; j < tb.Length; j++)
            {
                triangleTests++;
                if (triangleTests >= MaxTriangleTests)
                {
                    note = "budget-exceeded (aabb-confirmed)";
                    return true;
                }
                if (!TriBoxesOverlap(ta[i].Box, tb[j].Box)) continue;
                if (TrianglesIntersect(ta[i], tb[j], 1e-6)) { note = "confirmed triangle crossing"; return true; }
            }
        note = "aabb overlap without a triangle crossing (parts merely touch)";
        return false;
    }

    private static bool SameBox(in Aabb a, in Aabb b) =>
        a.Position.DistanceSquaredTo(b.Position) < 1e-8f && a.Size.DistanceSquaredTo(b.Size) < 1e-8f;

    private static Tri3d[]? WorldTriangles(MeshInstance3D mesh)
    {
        Vector3[] faces;
        try { faces = mesh.Mesh.GetFaces(); }
        catch (Exception) { return null; }
        if (faces.Length == 0 || faces.Length % 3 != 0 || faces.Length / 3 > MaxNarrowPhaseTriangles) return null;
        var transform = mesh.GlobalTransform;
        var triangles = new Tri3d[faces.Length / 3];
        for (var index = 0; index < triangles.Length; index++)
            triangles[index] = new Tri3d(
                transform * faces[index * 3], transform * faces[index * 3 + 1], transform * faces[index * 3 + 2]);
        return triangles;
    }

    private static bool TriBoxesOverlap(in Aabb a, in Aabb b) =>
        a.Position.X <= b.End.X && b.Position.X <= a.End.X
        && a.Position.Y <= b.End.Y && b.Position.Y <= a.End.Y
        && a.Position.Z <= b.End.Z && b.Position.Z <= a.End.Z;

    private readonly struct Tri3d
    {
        public readonly Vec3d A, B, C;
        public readonly Aabb Box;

        public Tri3d(Vector3 a, Vector3 b, Vector3 c)
        {
            A = Vec3d.From(a); B = Vec3d.From(b); C = Vec3d.From(c);
            var min = new Vector3(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), MathF.Min(a.Z, MathF.Min(b.Z, c.Z)));
            var max = new Vector3(MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)), MathF.Max(a.Z, MathF.Max(b.Z, c.Z)));
            Box = new Aabb(min, max - min);
        }
    }

    private readonly struct Vec3d
    {
        public readonly double X, Y, Z;
        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3d From(Vector3 v) => new(v.X, v.Y, v.Z);
        public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3d operator *(Vec3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);
        public double Dot(Vec3d b) => X * b.X + Y * b.Y + Z * b.Z;
        public Vec3d Cross(Vec3d b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public double LengthSquared => X * X + Y * Y + Z * Z;
    }

    // Moller-style triangle-triangle intersection in double precision.
    // Strict crossings only: coplanar triangles that merely share an edge (the
    // adjacent spiral treads) do not count as an overlap.
    private static bool TrianglesIntersect(in Tri3d t1, in Tri3d t2, double epsilon)
    {
        var n1 = (t1.B - t1.A).Cross(t1.C - t1.A);
        var n2 = (t2.B - t2.A).Cross(t2.C - t2.A);
        if (n1.LengthSquared < 1e-24 || n2.LengthSquared < 1e-24) return false;
        var d1 = -n1.Dot(t1.A);
        var d2 = -n2.Dot(t2.A);
        Span<double> dist1 = stackalloc double[3];
        Span<double> dist2 = stackalloc double[3];
        Span<Vec3d> vs1 = stackalloc Vec3d[3];
        Span<Vec3d> vs2 = stackalloc Vec3d[3];
        vs1[0] = t1.A; vs1[1] = t1.B; vs1[2] = t1.C;
        vs2[0] = t2.A; vs2[1] = t2.B; vs2[2] = t2.C;
        for (var index = 0; index < 3; index++)
        {
            dist1[index] = n1.Dot(vs2[index]) + d1;
            dist2[index] = n2.Dot(vs1[index]) + d2;
        }
        if (AllStrictlySameSign(dist1, epsilon) || AllStrictlySameSign(dist2, epsilon)) return false;
        if (AllNearZero(dist1, epsilon) && AllNearZero(dist2, epsilon)) return CoplanarIntersect(t1, t2, epsilon);

        // Segments of each triangle clipped by the other's plane; both lie on
        // the planes' intersection line, so their spans decide the test.
        if (!PlaneCrossingSegment(vs1, dist2, epsilon, out var s1a, out var s1b)) return false;
        if (!PlaneCrossingSegment(vs2, dist1, epsilon, out var s2a, out var s2b)) return false;
        var direction = n1.Cross(n2);
        var min1 = Math.Min(direction.Dot(s1a), direction.Dot(s1b));
        var max1 = Math.Max(direction.Dot(s1a), direction.Dot(s1b));
        var min2 = Math.Min(direction.Dot(s2a), direction.Dot(s2b));
        var max2 = Math.Max(direction.Dot(s2a), direction.Dot(s2b));
        return Math.Min(max1, max2) - Math.Max(min1, min2) > epsilon;
    }

    private static bool AllStrictlySameSign(ReadOnlySpan<double> values, double epsilon) =>
        (values[0] > epsilon && values[1] > epsilon && values[2] > epsilon)
        || (values[0] < -epsilon && values[1] < -epsilon && values[2] < -epsilon);

    private static bool AllNearZero(ReadOnlySpan<double> values, double epsilon) =>
        Math.Abs(values[0]) <= epsilon && Math.Abs(values[1]) <= epsilon && Math.Abs(values[2]) <= epsilon;

    private static bool PlaneCrossingSegment(ReadOnlySpan<Vec3d> triangle, ReadOnlySpan<double> distances, double epsilon,
        out Vec3d first, out Vec3d second)
    {
        Span<Vec3d> points = stackalloc Vec3d[3];
        var count = 0;
        for (var edge = 0; edge < 3; edge++)
        {
            var a = triangle[edge];
            var b = triangle[(edge + 1) % 3];
            var da = distances[edge];
            var db = distances[(edge + 1) % 3];
            if (Math.Abs(da) <= epsilon && count < 3) points[count++] = a;
            if (count < 3 && ((da > epsilon && db < -epsilon) || (da < -epsilon && db > epsilon)))
                points[count++] = a + (b - a) * (da / (da - db));
        }
        if (count < 2) { first = default; second = default; return false; }
        first = points[0];
        second = points[1];
        return true;
    }

    private static bool CoplanarIntersect(in Tri3d t1, in Tri3d t2, double epsilon)
    {
        var normal = (t1.B - t1.A).Cross(t1.C - t1.A);
        var ax = Math.Abs(normal.X);
        var ay = Math.Abs(normal.Y);
        var az = Math.Abs(normal.Z);
        var useX = ax >= ay && ax >= az;
        var useY = !useX && ay >= az;
        Span<double> p1x = stackalloc double[3]; Span<double> p1y = stackalloc double[3];
        Span<double> p2x = stackalloc double[3]; Span<double> p2y = stackalloc double[3];
        Project(t1, useX, useY, p1x, p1y);
        Project(t2, useX, useY, p2x, p2y);
        return AnyVertexInside(p1x, p1y, p2x, p2y, epsilon) || AnyVertexInside(p2x, p2y, p1x, p1y, epsilon)
            || AnyEdgeCrosses(p1x, p1y, p2x, p2y, epsilon);
    }

    private static void Project(in Tri3d triangle, bool useX, bool useY, Span<double> xs, Span<double> ys)
    {
        Span<Vec3d> vertices = stackalloc Vec3d[3];
        vertices[0] = triangle.A; vertices[1] = triangle.B; vertices[2] = triangle.C;
        for (var index = 0; index < 3; index++)
        {
            var v = vertices[index];
            if (useX) { xs[index] = v.Y; ys[index] = v.Z; }
            else if (useY) { xs[index] = v.X; ys[index] = v.Z; }
            else { xs[index] = v.X; ys[index] = v.Y; }
        }
    }

    private static bool AnyVertexInside(ReadOnlySpan<double> xs, ReadOnlySpan<double> ys,
        ReadOnlySpan<double> triX, ReadOnlySpan<double> triY, double epsilon)
    {
        for (var vertex = 0; vertex < 3; vertex++)
        {
            var s0 = Cross2(triX[1] - triX[0], triY[1] - triY[0], xs[vertex] - triX[0], ys[vertex] - triY[0]);
            var s1 = Cross2(triX[2] - triX[1], triY[2] - triY[1], xs[vertex] - triX[1], ys[vertex] - triY[1]);
            var s2 = Cross2(triX[0] - triX[2], triY[0] - triY[2], xs[vertex] - triX[2], ys[vertex] - triY[2]);
            if ((s0 > epsilon && s1 > epsilon && s2 > epsilon) || (s0 < -epsilon && s1 < -epsilon && s2 < -epsilon)) return true;
        }
        return false;
    }

    private static bool AnyEdgeCrosses(ReadOnlySpan<double> p1x, ReadOnlySpan<double> p1y,
        ReadOnlySpan<double> p2x, ReadOnlySpan<double> p2y, double epsilon)
    {
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                if (SegmentsCrossStrict(
                        p1x[i], p1y[i], p1x[(i + 1) % 3], p1y[(i + 1) % 3],
                        p2x[j], p2y[j], p2x[(j + 1) % 3], p2y[(j + 1) % 3], epsilon)) return true;
        return false;
    }

    private static bool SegmentsCrossStrict(double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy, double epsilon)
    {
        var o1 = Cross2(bx - ax, by - ay, cx - ax, cy - ay);
        var o2 = Cross2(bx - ax, by - ay, dx - ax, dy - ay);
        var o3 = Cross2(dx - cx, dy - cy, ax - cx, ay - cy);
        var o4 = Cross2(dx - cx, dy - cy, bx - cx, by - cy);
        return ((o1 > epsilon && o2 < -epsilon) || (o1 < -epsilon && o2 > epsilon))
            && ((o3 > epsilon && o4 < -epsilon) || (o3 < -epsilon && o4 > epsilon));
    }

    private static double Cross2(double ax, double ay, double bx, double by) => ax * by - ay * bx;

    // -------------------------------------------------------------- allowlist

    private static readonly (string A, string B, string Reason)[] DefaultAllowlist =
    [
        ("*MosqueWindow*", "*MosqueWindow*", "window frame, sill and arch members meet inside the pierced opening"),
        ("*Mihrab*", "*Mihrab*", "mihrab niche backing, pilasters and arch share the recess"),
        ("*Mihrab*", "*MosqueQiblaWall*", "mihrab assembly is mounted on the qibla wall frame"),
        ("*MosqueLibrary*", "*MosqueCeiling*", "library partitions meet the hall ceiling plane"),
        ("*MosqueLibraryNorthWall*", "*MosqueWestDado*", "library partition meets the west dado trim"),
        ("*MosqueBookcase*", "*MosqueBookShelf*", "shelf boards are housed in the bookcase side panels"),
        ("*MinbarTread*", "*MinbarPlatform*", "minbar steps run into the platform"),
        ("*MinbarRail*", "*MinbarPost*", "minbar rail is fixed into the posts"),
        ("*SpiralHandrail*", "*SpiralBaluster*", "spiral rail is fixed on its balusters"),
        ("*MosqueStairHandrail*", "*MosqueStairPost*", "entrance stair rail is fixed on its posts"),
        ("*MosqueLanding*Handrail*", "*MosqueLandingPost*", "landing rail is fixed on its posts"),
        ("*MinaretStairLamp*", "*SpiralTimberMast*", "stair lamps are mounted on the centre mast"),
        ("*Lantern*", "*Lantern*", "roof lantern frame, jambs, header and apron are one assembly"),
        ("*TimberShaftFacet*", "*ShaftCornerTrim*", "corner boards cover the shaft facet joints"),
        ("*MosqueMinaretCrown*", "*MosqueMinaretCrown*", "crown cornice, tent roof, finial and crescent are one ornament"),
        ("*MosqueBasinDrain*", "*MosqueWashCabinet*", "basin drain runs into the wash cabinet"),
        ("*MosqueWaterRiser*", "*MosqueWashCabinet*", "water riser is fixed to the wash cabinet"),
        ("*MosqueVestibule*", "*MosqueVestibule*", "vestibule walls, lintel and ceilings form one partition"),
        ("*MosqueVestibule*", "*MosqueWindowWall*", "vestibule partition meets the window wall plane"),
        ("*MosqueCeilingTimberBeam*", "*MosqueCeiling*", "timber beams carry the ceiling boards"),
        ("*MosqueClad*", "*MosqueWindow*", "exterior cladding meets the white window trim"),
        ("*MosqueEntranceLeaf*", "*MosqueEntrancePlankJoint*", "plank joints are inset in the door leaf"),
        ("*MosqueEntranceLeaf*", "*MosqueEntranceHandleFittings*", "handle fittings are inset in the door leaf"),
        ("*MosquePlinth*", "*MosqueHall*", "hall shell is founded on the plinth"),
        ("*MosquePlinth*", "*MosqueWindowWall*", "window walls are founded on the plinth"),
        ("*MosquePlinth*", "*MosqueVestibule*", "vestibule partitions are founded on the plinth"),
        ("*MosquePlinth*", "*MosqueLibrary*", "library partitions are founded on the plinth"),
        ("*MosquePlinth*", "*MosqueTimberFloor*", "timber floor is laid on the plinth"),
        ("*MosquePlinth*", "*MosqueEntry*", "entrance masonry merges with the plinth"),
        ("*MosqueEntry*", "*MosqueEntry*", "entrance treads, landings and rails form one stair"),
        ("*MosqueEntry*", "*MosqueStairHandrail*", "entrance stair rail runs along the masonry"),
        ("*MosqueEntry*", "*MosqueStairPost*", "entrance stair posts stand on the masonry"),
    ];

    private static List<(string A, string B, string Reason)> LoadAllowlist(string? path)
    {
        var rules = new List<(string, string, string)>(DefaultAllowlist);
        path ??= OS.GetEnvironment("URMAN_MOSQUE_OVERLAP_ALLOWLIST");
        if (string.IsNullOrEmpty(path)) path = DefaultAllowlistPath;
        try
        {
            string? json = null;
            if (path.StartsWith("res://", StringComparison.Ordinal))
            {
                if (global::Godot.FileAccess.FileExists(path)) json = global::Godot.FileAccess.GetFileAsString(path);
            }
            else if (File.Exists(path)) json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return rules;
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("rules", out var array) || array.ValueKind != JsonValueKind.Array)
                return rules;
            foreach (var element in array.EnumerateArray())
            {
                var a = element.TryGetProperty("a", out var av) ? av.GetString() : null;
                var b = element.TryGetProperty("b", out var bv) ? bv.GetString() : null;
                if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) continue;
                var reason = element.TryGetProperty("reason", out var rv) ? rv.GetString() : null;
                rules.Add((a!, b!, reason ?? "configured allowlist"));
            }
        }
        catch (Exception error)
        {
            GD.PushWarning("mosque-overlap-audit: allowlist '" + path + "' could not be read: " + error.Message);
        }
        return rules;
    }

    private static bool IsAllowlisted(Entry a, Entry b, List<(string A, string B, string Reason)> rules, out string reason)
    {
        if (a.Group.Length > 0 && a.Group == b.Group)
        {
            reason = "same overlapAuditGroup '" + a.Group + "'";
            return true;
        }
        foreach (var (patternA, patternB, why) in rules)
            if ((Matches(patternA, a.Path) && Matches(patternB, b.Path))
                || (Matches(patternA, b.Path) && Matches(patternB, a.Path)))
            {
                reason = why;
                return true;
            }
        reason = "";
        return false;
    }

    // '*' wildcards; everything else is a case-insensitive literal.
    private static bool Matches(string pattern, string path)
    {
        var p = 0;
        var s = 0;
        var star = -1;
        var resume = 0;
        while (s < path.Length)
        {
            if (p < pattern.Length && (pattern[p] == '*' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(path[s])))
            {
                if (pattern[p] == '*') { star = p++; resume = s; }
                else { p++; s++; }
            }
            else if (star >= 0) { p = star + 1; s = ++resume; }
            else return false;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    // ----------------------------------------------------------- structural

    private static readonly string[] StructuralMarkers =
    [
        "MosquePlinth", "MosqueTimberFloor", "MosqueCeiling", "MosqueRoof", "MosqueGable", "MosqueFascia",
        "MosqueRidge", "MosqueWindowWall", "MosqueWindowPier", "MosqueVestibule", "MosqueHall", "MosqueDado",
        "MosqueWestDado", "MosqueCornice", "MosqueLibraryEastWall", "MosqueLibraryNorthWall", "MosqueLibraryLintel",
        "MosqueDoorHeaderInfill", "MosqueClad", "MosqueFrieze", "MosqueCornerBoard", "MosqueEntry", "MosqueLanding",
        "MosqueStair", "TimberShaftFacet", "ShaftCornerTrim", "LanternTimberApron", "MosqueYard",
        "MosqueMinaretCrown",
    ];

    private static bool IsStructural(Entry entry)
    {
        foreach (var marker in StructuralMarkers)
            if (entry.Path.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool IsPropStructurePair(Entry a, Entry b) => IsStructural(a) ^ IsStructural(b);

    // ------------------------------------------------------- collision audit

    private static void AuditContacts(Node3D root, Report report)
    {
        foreach (var body in Descendants<StaticBody3D>(root))
        {
            if (body.HasMeta("overlapAuditIgnore") && body.GetMeta("overlapAuditIgnore").AsBool()) continue;
            foreach (var child in body.GetChildren())
            {
                if (child is not CollisionShape3D shape || shape.Shape is null) continue;
                if (ShapeAabb(shape.Shape) is not { } localBox) continue;
                var world = shape.GlobalTransform * localBox;
                report.ContactsChecked++;
                var source = ResolveVisualSource(shape, body, root);
                if (source is { } mesh)
                {
                    if (!mesh.IsVisibleInTree())
                    {
                        report.ContactIssues.Add(new Finding("contact",
                            shape.GetPath().ToString(), mesh.GetPath().ToString(), 0f,
                            "visual counterpart exists but is hidden"));
                        continue;
                    }
                    var meshBox = mesh.GlobalTransform * mesh.GetAabb();
                    var delta = world.GetCenter().DistanceTo(meshBox.GetCenter());
                    if (delta > ContactDisplacementMetres)
                        report.ContactIssues.Add(new Finding("contact",
                            shape.GetPath().ToString(), mesh.GetPath().ToString(), delta,
                            FormattableString.Invariant($"shape is {delta * 100f:0.#} cm from its mesh center")));
                    continue;
                }
                if (NearestAssemblyBox(body, root) is not { } assemblyBox)
                {
                    report.ContactIssues.Add(new Finding("contact",
                        shape.GetPath().ToString(), body.GetPath().ToString(), 0f,
                        "no visible mesh counterpart found for this shape"));
                    continue;
                }
                var inflated = assemblyBox.Grow(ContactDisplacementMetres);
                if (inflated.Encloses(world)) continue;
                var center = world.GetCenter();
                var clamped = new Vector3(
                    Mathf.Clamp(center.X, inflated.Position.X, inflated.End.X),
                    Mathf.Clamp(center.Y, inflated.Position.Y, inflated.End.Y),
                    Mathf.Clamp(center.Z, inflated.Position.Z, inflated.End.Z));
                var distance = center.DistanceTo(clamped);
                report.ContactIssues.Add(new Finding("contact",
                    shape.GetPath().ToString(), body.GetPath().ToString(), distance,
                    FormattableString.Invariant($"shape is {distance * 100f:0.#} cm outside its prop assembly box")));
            }
        }
    }

    private static MeshInstance3D? ResolveVisualSource(CollisionShape3D shape, StaticBody3D body, Node3D root)
    {
        var path = shape.HasMeta("authoredSourceMesh") ? shape.GetMeta("authoredSourceMesh").AsString()
            : body.HasMeta("authoredSourceMesh") ? body.GetMeta("authoredSourceMesh").AsString() : "";
        if (path.Length > 0)
        {
            if (root.GetNodeOrNull(path) is MeshInstance3D exact) return exact;
            if (root.GetNodeOrNull(path) is Node3D node && FindFirstMesh(node) is { } nested) return nested;
        }
        var name = body.Name.ToString();
        foreach (var suffix in new[] { "_SurfaceContact", "_Blocker", "_Contact", "Body" })
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length];
                break;
            }
        if (name.Length == 0 || body.GetParent() is not Node3D parent) return null;
        if (parent.FindChild(name, true, false) is MeshInstance3D match) return match;
        return parent.FindChild(name, false, false) is Node3D direct ? FindFirstMesh(direct) : null;
    }

    private static MeshInstance3D? FindFirstMesh(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is not null) return mesh;
        foreach (var child in node.GetChildren())
            if (FindFirstMesh(child) is { } found) return found;
        return null;
    }

    private static Aabb? NearestAssemblyBox(Node3D body, Node3D root)
    {
        for (Node? ancestor = body.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is not Node3D spatial)
            {
                if (ancestor == root) return null;
                continue;
            }
            if (spatial == root) return null;
            if (CombinedMeshBox(spatial) is { } box) return box;
        }
        return null;
    }

    private static Aabb? CombinedMeshBox(Node3D node)
    {
        Aabb? result = null;
        var count = 0;
        foreach (var mesh in Descendants<MeshInstance3D>(node))
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null || mesh.VisibilityRangeBegin > 0f) continue;
            if (++count > AssemblyMeshLimit) return null;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            result = result is { } merged ? merged.Merge(box) : box;
        }
        return result;
    }

    private static Aabb? ShapeAabb(Shape3D shape)
    {
        switch (shape)
        {
            case BoxShape3D box: return new Aabb(-box.Size * .5f, box.Size);
            case SphereShape3D sphere:
            {
                var size = new Vector3(sphere.Radius * 2f, sphere.Radius * 2f, sphere.Radius * 2f);
                return new Aabb(-size * .5f, size);
            }
            case CylinderShape3D cylinder:
            {
                var size = new Vector3(cylinder.Radius * 2f, cylinder.Height, cylinder.Radius * 2f);
                return new Aabb(-size * .5f, size);
            }
            case CapsuleShape3D capsule:
            {
                var size = new Vector3(capsule.Radius * 2f, capsule.Height + capsule.Radius * 2f, capsule.Radius * 2f);
                return new Aabb(-size * .5f, size);
            }
            case ConvexPolygonShape3D convex:
            {
                var points = convex.Points;
                if (points.Length == 0) return null;
                var min = points[0];
                var max = points[0];
                foreach (var point in points) { min = min.Min(point); max = max.Max(point); }
                return new Aabb(min, max - min);
            }
            case ConcavePolygonShape3D concave:
            {
                var faces = concave.GetFaces();
                if (faces.Length == 0) return null;
                var min = faces[0];
                var max = faces[0];
                foreach (var point in faces) { min = min.Min(point); max = max.Max(point); }
                return new Aabb(min, max - min);
            }
            default: return null;
        }
    }

    private static IEnumerable<T> Descendants<T>(Node root) where T : Node
    {
        if (root is T self) yield return self;
        foreach (var node in Walk(root))
            if (node is T typed) yield return typed;
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Walk(child)) yield return nested;
        }
    }
}
