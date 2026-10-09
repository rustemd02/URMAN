using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// ACT1-DEPTH.9 / author photo reference T2 (09.10.2026): the village exterior
/// kit builds every hero wall course as its own 0.26 x 0.14 m chamfered box, so a
/// log wall lit by the sun reads as stacked planks with a flat face. Each slender
/// course is rebuilt here, in the same bounds, as a smooth elliptical log: the
/// sun now rolls over the top of every course, the underside turns away into
/// shade and adjacent courses pinch into a dark seam. Corner blocks with their
/// own end-grain surface, the boarded gable and anything that is not a single
/// slender URMAN_Hero_Log surface are left exactly as authored. Presentation
/// only: collision, material and placement stay with the kit.
/// </summary>
public partial class Act1ConnectedWorld
{
    private const string HeroLogMaterial = "URMAN_Hero_Log";
    private const string HeroLogEndMaterial = "URMAN_Hero_LogEnd";
    private const int RoundLogSegments = 20;
    private static readonly Dictionary<ulong, ArrayMesh?> RoundedLogCache = new();

    private static int RoundHeroLogCourses(Node3D root)
    {
        var rounded = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(root))
        {
            if (mesh.Mesh is not ArrayMesh source || source.GetSurfaceCount() is < 1 or > 2) continue;
            // A course is either one URMAN_Hero_Log surface or a log body plus its
            // own URMAN_Hero_LogEnd end-grain caps on the same mesh.
            Material? material = null, endGrain = null;
            for (var surface = 0; surface < source.GetSurfaceCount(); surface++)
            {
                var slot = source.SurfaceGetMaterial(surface);
                if (slot?.ResourceName == HeroLogMaterial) material = slot;
                else if (slot?.ResourceName == HeroLogEndMaterial) endGrain = slot;
                else { material = null; break; }
            }
            if (material is null || (source.GetSurfaceCount() == 2 && endGrain is null)) continue;
            var id = source.GetInstanceId();
            if (!RoundedLogCache.TryGetValue(id, out var replacement))
                RoundedLogCache[id] = replacement = BuildRoundLog(source, material, endGrain);
            if (replacement is null) continue;
            mesh.Mesh = replacement;
            mesh.SetMeta("roundedLogCourse", "ACT1-DEPTH.9/T2: chamfered kit course rebuilt as an elliptical log in the same bounds");
            rounded++;
        }
        if (rounded > 0) GD.Print($"act1-round-logs: rounded={rounded} sourceMeshes={RoundedLogCache.Count}");
        return rounded;
    }

    private static ArrayMesh? BuildRoundLog(ArrayMesh source, Material material, Material? endGrain)
    {
        var vertices = Enumerable.Range(0, source.GetSurfaceCount())
            .SelectMany(surface => source.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
        if (vertices.Length == 0) return null;
        var min = vertices[0];
        var max = vertices[0];
        foreach (var v in vertices) { min = min.Min(v); max = max.Max(v); }
        var size = max - min;
        var centre = (min + max) * .5f;
        // A wall course runs along X or Z and stands on Y; only clear log
        // proportions qualify, so boards, gable cladding and blocks are skipped.
        var alongX = size.X >= size.Z;
        var length = alongX ? size.X : size.Z;
        var depth = alongX ? size.Z : size.X;
        var height = size.Y;
        if (height is < .15f or > .45f || depth is < .06f or > .40f || length < height * 2.5f) return null;

        // Courses sit 0.28 m apart with 0.26 m bodies; a slightly taller ellipse
        // lets neighbours meet in a narrow chinked seam instead of a dark slot.
        var ry = height * .5f * 1.12f;
        var rd = depth * .5f;
        var half = length * .5f;
        Vector3 Point(float along, float d, float y) => alongX
            ? new Vector3(centre.X + along, centre.Y + y, centre.Z + d)
            : new Vector3(centre.X + d, centre.Y + y, centre.Z + along);
        Vector3 Dir(float along, float d, float y) => (alongX ? new Vector3(along, y, d) : new Vector3(d, y, along)).Normalized();

        var ring = new (float D, float Y, Vector3 N, float Arc)[RoundLogSegments + 1];
        var arc = 0f;
        for (var i = 0; i <= RoundLogSegments; i++)
        {
            var t = Mathf.Tau * i / RoundLogSegments;
            var d = rd * Mathf.Cos(t);
            var y = ry * Mathf.Sin(t);
            if (i > 0) arc += new Vector2(d - ring[i - 1].D, y - ring[i - 1].Y).Length();
            // Ellipse normal: gradient of (d/rd)^2 + (y/ry)^2.
            ring[i] = (d, y, Dir(0f, d / (rd * rd), y / (ry * ry)), arc);
        }

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        // Same front-face rule as TimberHomeStyle.AppendChamferedBox, which renders
        // correctly in this project: emit so that (b - a) x (c - a) faces the normal.
        void Tri((Vector3 P, Vector3 N, Vector2 Uv) a, (Vector3 P, Vector3 N, Vector2 Uv) b, (Vector3 P, Vector3 N, Vector2 Uv) c)
        {
            var face = (b.P - a.P).Cross(c.P - a.P);
            var normal = a.N + b.N + c.N;
            if (face.Dot(normal) < 0f) (b, c) = (c, b);
            foreach (var v in new[] { a, b, c })
            {
                surface.SetNormal(v.N);
                surface.SetUV(v.Uv);
                surface.AddVertex(v.P);
            }
        }
        for (var i = 0; i < RoundLogSegments; i++)
        {
            var r0 = ring[i];
            var r1 = ring[i + 1];
            // Grain runs along the course: UV x is the length in metres, UV y the
            // girth in metres, the metric convention of the kit's timber.
            var a = (Point(-half, r0.D, r0.Y), r0.N, new Vector2(-half, r0.Arc));
            var b = (Point(half, r0.D, r0.Y), r0.N, new Vector2(half, r0.Arc));
            var c = (Point(half, r1.D, r1.Y), r1.N, new Vector2(half, r1.Arc));
            var d = (Point(-half, r1.D, r1.Y), r1.N, new Vector2(-half, r1.Arc));
            Tri(a, b, c);
            Tri(a, c, d);
        }
        surface.SetMaterial(material);
        var result = surface.Commit();
        // End caps: on the course's own end-grain material when it has one, so
        // the rings stay where the kit put them; otherwise on the log material.
        if (endGrain is not null)
        {
            surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
        }
        foreach (var sign in new[] { -1f, 1f })
        {
            var normal = Dir(sign, 0f, 0f);
            var hub = (Point(sign * half, 0f, 0f), normal, new Vector2(.5f, .5f));
            for (var i = 0; i < RoundLogSegments; i++)
            {
                var r0 = ring[i];
                var r1 = ring[i + 1];
                Tri(hub, (Point(sign * half, r0.D, r0.Y), normal, new Vector2(.5f + .5f * r0.D / rd, .5f + .5f * r0.Y / ry)),
                    (Point(sign * half, r1.D, r1.Y), normal, new Vector2(.5f + .5f * r1.D / rd, .5f + .5f * r1.Y / ry)));
            }
        }
        if (endGrain is not null)
        {
            surface.SetMaterial(endGrain);
            surface.Commit(result);
        }
        else
        {
            surface.SetMaterial(material);
            result = surface.Commit();
        }
        result.ResourceName = source.ResourceName + "_Round";
        return result;
    }
}
