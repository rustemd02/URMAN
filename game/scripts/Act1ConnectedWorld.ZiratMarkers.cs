using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Zīrat markers, 10.10.2026. The roadside kit's markers are six-sided tapered
/// prisms with a flat top, and after the stones were relocated their pale
/// octagonal earth mounds stayed behind at the old kit positions as loose
/// slabs. Each marker is rebuilt here in its own node and local frame as a
/// plain upright village headstone in the manner of Tatar rural cemeteries:
/// a thin slab with a rounded, pointed or shouldered top, chamfered edges, a
/// low plinth sunk into the snow, a darker damp base and a slight lean; a few
/// quiet stones are weathered wooden posts with a small gabled cap. Snow
/// collars replace the orphaned mounds and two quiet graves get a low painted
/// fence. The family pair keeps its node names, transforms and a flat front
/// face at +Z, so the compiled inscriptions, the reading point and the
/// ReadZiratFamilyInscriptions target are rebuilt from the same contracts.
/// No text or script is added; a small raised crescent on three quiet stones
/// and the headstone/fence orientation await the cultural consultant
/// (open_questions.md, 10.10.2026). Presentation only: no collision, no
/// interaction, node positions and the VIS-022 axis audit are untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private enum ZiratTop { Arch, Keel, Shoulder, WoodPost }

    private static readonly string[] ZiratStoneTones = ["9c998d", "8f8e84", "a39f92", "86857b"];

    private static void ReshapeZiratMarkers(Node3D lowMarkerPlacement, Node3D farMarkerPlacement)
    {
        var markers = new[] { lowMarkerPlacement, farMarkerPlacement }
            .SelectMany(group => FindDescendants<MeshInstance3D>(group))
            .Where(mesh => mesh.Mesh is not null && (mesh.Name.ToString().Contains("_Marker_", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("_Companion_", StringComparison.Ordinal)))
            .OrderBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal)
            .ToArray();
        var hiddenContacts = 0;
        foreach (var contact in new[] { lowMarkerPlacement, farMarkerPlacement }
                     .SelectMany(group => FindDescendants<MeshInstance3D>(group))
                     .Where(mesh => mesh.Name.ToString().Contains("_Mound_", StringComparison.Ordinal)
                         || mesh.Name.ToString().Contains("_LeafLitter_", StringComparison.Ordinal)))
        {
            // The kit mounds were left at the old roadside positions when the
            // stones moved; the snow collars below are their replacement.
            contact.Visible = false;
            contact.SetMeta("suppressionReason", "orphaned kit mound/litter; replaced by per-marker snow collar");
            hiddenContacts++;
        }

        var quietIndex = 0;
        var family = 0;
        var crescents = 0;
        var fences = 0;
        foreach (var marker in markers)
        {
            var name = marker.Name.ToString();
            var isFamily = name is "ZiratMarkerGroup_Low_Marker_00_LOD0" or "ZiratMarkerGroup_Low_Marker_01_LOD0";
            var companion = name.Contains("_Companion_", StringComparison.Ordinal);
            var old = marker.Mesh!.GetAabb();
            var seed = ZiratSeed(name);
            ZiratTop top;
            float height;
            var crescent = false;
            var fence = false;
            if (isFamily)
            {
                // Same height and footprint centre as the authored pair: the
                // inscription rows are fractions of this height.
                top = ZiratTop.Arch;
                height = old.Size.Y;
                family++;
            }
            else
            {
                top = (quietIndex % 5) switch { 0 => ZiratTop.Keel, 1 => ZiratTop.Arch, 2 => ZiratTop.Shoulder, 3 => ZiratTop.WoodPost, _ => ZiratTop.Arch };
                // Far kit stones were 0.25-0.36 m stubs; in the relocated rows they
                // stand next to the near ones, so every quiet marker gets a village
                // headstone height.
                height = Mathf.Max(old.Size.Y, (companion ? .52f : .62f) + ZiratJitter(seed, 1) * .12f);
                crescent = (top is ZiratTop.Arch or ZiratTop.Keel) && quietIndex % 3 == 0;
                // One stone and one wooden post, both in the relocated rows clear
                // of the path and the bench.
                fence = name is "ZiratMarkerGroup_Low_Marker_03_LOD0" or "ZiratMarkerGroup_Far_Marker_01_LOD0";
                quietIndex++;
            }

            var width = Mathf.Clamp(old.Size.X * .86f, .36f, .54f);
            var thickness = Mathf.Clamp(old.Size.Z * .36f, .11f, .15f);
            var tone = ZiratStoneTones[(int)(seed % (uint)ZiratStoneTones.Length)];
            var lean = isFamily
                ? new Vector3(-.8f, 0f, ZiratJitter(seed, 2) * .5f)
                : new Vector3(-1.2f + ZiratJitter(seed, 3) * 2.4f, ZiratJitter(seed, 4) * 4f, ZiratJitter(seed, 5) * 2.2f);
            marker.Mesh = top == ZiratTop.WoodPost
                ? BuildZiratWoodPost(height * 1.08f, seed, lean)
                : BuildZiratHeadstone(top, width, thickness, height, tone, seed, lean, crescent);
            marker.MaterialOverride = null;
            for (var surface = 0; surface < marker.GetSurfaceOverrideMaterialCount(); surface++)
                marker.SetSurfaceOverrideMaterial(surface, null);
            marker.SetMeta("culturalPlaceholder", isFamily
                ? "plain upright headstone; authored inscription only; no symbol"
                : crescent ? "plain upright headstone with small raised crescent; no text; consultant review"
                : "plain upright village marker; no text; no symbol");
            marker.SetMeta("ziratMarkerShape", top.ToString());
            if (crescent) crescents++;

            var footprint = top == ZiratTop.WoodPost ? new Vector2(.16f, .16f) : new Vector2(width + .09f, thickness + .10f);
            marker.AddChild(BuildZiratSnowCollar(footprint, seed));
            if (fence)
            {
                marker.AddChild(BuildZiratGraveFence(marker, seed));
                fences++;
            }
        }

        lowMarkerPlacement.SetMeta("ziratMarkerReshape",
            $"markers={markers.Length} family={family} crescents={crescents} fences={fences} hiddenMounds={hiddenContacts}");
        GD.Print($"zirat-markers: reshaped={markers.Length} family={family} crescents={crescents} "
            + $"fences={fences} hiddenMounds={hiddenContacts}");
    }

    private static uint ZiratSeed(string name)
    {
        var hash = 2166136261u;
        foreach (var character in name) hash = (hash ^ character) * 16777619u;
        return hash;
    }

    private static float ZiratJitter(uint seed, int channel)
    {
        var value = seed ^ (uint)(channel * 0x9E3779B9);
        value ^= value >> 15; value *= 0x2C1B3C6D; value ^= value >> 12; value *= 0x297A2D39; value ^= value >> 15;
        return value / (float)uint.MaxValue * 2f - 1f;
    }

    /// <summary>Half outline of a slab front, left to right over the top, in slab XY.</summary>
    private static List<(Vector2 Point, bool Smooth)> ZiratOutline(ZiratTop top, float halfWidth, float height, float bottom, uint seed)
    {
        var points = new List<(Vector2, bool)> { (new(halfWidth, bottom), false) };
        switch (top)
        {
            case ZiratTop.Arch:
            {
                // Segmental arch springing at 80 % of the height.
                var spring = height * .80f;
                var centre = (height * height - halfWidth * halfWidth - spring * spring) / (2f * (height - spring));
                var radius = height - centre;
                var start = Mathf.Atan2(spring - centre, halfWidth);
                points.Add((new(halfWidth, spring), false));
                const int steps = 12;
                for (var step = 1; step < steps; step++)
                {
                    var angle = Mathf.Lerp(start, Mathf.Pi - start, step / (float)steps);
                    points.Add((new(Mathf.Cos(angle) * radius, centre + Mathf.Sin(angle) * radius), true));
                }
                points.Add((new(-halfWidth, spring), false));
                break;
            }
            case ZiratTop.Keel:
            {
                // Pointed top of two arcs; the rise stays above the half width so
                // each arc meets the apex without a dimple.
                var rise = Mathf.Max(height * .26f, halfWidth * 1.08f);
                var spring = height - rise;
                var radius = (halfWidth * halfWidth + rise * rise) / (2f * halfWidth);
                var centreX = halfWidth - radius;
                var end = Mathf.Atan2(rise, -centreX);
                points.Add((new(halfWidth, spring), false));
                const int steps = 7;
                var right = new List<Vector2>();
                for (var step = 1; step < steps; step++)
                {
                    var angle = end * step / steps;
                    right.Add(new(centreX + Mathf.Cos(angle) * radius, spring + Mathf.Sin(angle) * radius));
                }
                points.AddRange(right.Select(point => (point, true)));
                points.Add((new(0f, height), false));
                points.AddRange(Enumerable.Reverse(right).Select(point => (new Vector2(-point.X, point.Y), true)));
                points.Add((new(-halfWidth, spring), false));
                break;
            }
            default:
            {
                // Shouldered top: chamfered upper corners, a slightly uneven crest.
                var shoulder = halfWidth * .30f;
                var tilt = ZiratJitter(seed, 6) * .012f;
                points.Add((new(halfWidth, height - shoulder * .85f), false));
                points.Add((new(halfWidth - shoulder, height + tilt), false));
                points.Add((new(-halfWidth + shoulder, height - tilt), false));
                points.Add((new(-halfWidth, height - shoulder * .85f), false));
                break;
            }
        }
        points.Add((new(-halfWidth, bottom), false));
        return points;
    }

    private static ArrayMesh BuildZiratHeadstone(ZiratTop top, float width, float thickness, float height,
        string tone, uint seed, Vector3 leanDegrees, bool crescent)
    {
        const float chamfer = .014f;
        const float plinthHeight = .07f;
        var bottom = plinthHeight - .025f;
        var outer = ZiratOutline(top, width * .5f, height, bottom, seed);
        var inner = ZiratOutline(top, width * .5f - chamfer, height - chamfer, bottom, seed);
        // A few weathered chips: tiny in-plane nudges on outer outline points
        // keep the front face planar for the inscriptions.
        for (var index = 1; index < outer.Count - 1; index++)
        {
            var nudge = ZiratJitter(seed, 20 + index) * .0035f;
            var (point, smooth) = outer[index];
            outer[index] = (point + point.Normalized() * nudge, smooth);
            var (innerPoint, innerSmooth) = inner[index];
            inner[index] = (innerPoint + innerPoint.Normalized() * nudge, innerSmooth);
        }

        var pivot = new Vector3(0f, bottom, 0f);
        var lean = Basis.FromEuler(new Vector3(Mathf.DegToRad(leanDegrees.X), Mathf.DegToRad(leanDegrees.Y),
            Mathf.DegToRad(leanDegrees.Z)));
        Vector3 Slab(Vector3 local) => pivot + lean * (local - pivot);

        var stone = new SurfaceTool();
        stone.Begin(Mesh.PrimitiveType.Triangles);
        var half = thickness * .5f;

        // Front and back faces (convex outlines, fan from the centroid).
        foreach (var side in new[] { 1f, -1f })
        {
            var centroid = inner.Aggregate(Vector2.Zero, (sum, point) => sum + point.Point) / inner.Count;
            var normal = lean * new Vector3(0f, 0f, side);
            for (var index = 0; index < inner.Count; index++)
            {
                var a = inner[index].Point;
                var b = inner[(index + 1) % inner.Count].Point;
                var tri = new[] { new Vector3(centroid.X, centroid.Y, side * half), new Vector3(a.X, a.Y, side * half), new Vector3(b.X, b.Y, side * half) };
                ZiratTriangle(stone, Slab(tri[0]), Slab(tri[side > 0 ? 1 : 2]), Slab(tri[side > 0 ? 2 : 1]), normal, normal, normal);
            }
        }

        // Edge band (outer outline) and the two chamfer bands, open at the
        // bottom where the slab sits in its plinth.
        var edgeNormals = new Vector2[outer.Count - 1];
        for (var index = 0; index < outer.Count - 1; index++)
        {
            var direction = outer[index + 1].Point - outer[index].Point;
            edgeNormals[index] = new Vector2(direction.Y, -direction.X).Normalized();
        }
        Vector2 PointNormal(int point, int edge)
        {
            if (!outer[point].Smooth) return edgeNormals[edge];
            var before = edgeNormals[Math.Max(point - 1, 0)];
            var after = edgeNormals[Math.Min(point, edgeNormals.Length - 1)];
            return (before + after).Normalized();
        }
        for (var edge = 0; edge < outer.Count - 1; edge++)
        {
            var a = outer[edge].Point;
            var b = outer[edge + 1].Point;
            var ia = inner[edge].Point;
            var ib = inner[edge + 1].Point;
            var na = PointNormal(edge, edge);
            var nb = PointNormal(edge + 1, edge);
            var flatA = new Vector3(na.X, na.Y, 0f);
            var flatB = new Vector3(nb.X, nb.Y, 0f);
            var band = half - chamfer;
            ZiratQuad(stone, Slab(new(a.X, a.Y, band)), Slab(new(b.X, b.Y, band)),
                Slab(new(b.X, b.Y, -band)), Slab(new(a.X, a.Y, -band)),
                lean * flatA, lean * flatB, lean * flatB, lean * flatA);
            foreach (var side in new[] { 1f, -1f })
            {
                var ca = lean * (flatA + new Vector3(0, 0, side)).Normalized();
                var cb = lean * (flatB + new Vector3(0, 0, side)).Normalized();
                var outerA = Slab(new(a.X, a.Y, side * band));
                var outerB = Slab(new(b.X, b.Y, side * band));
                var innerA = Slab(new(ia.X, ia.Y, side * half));
                var innerB = Slab(new(ib.X, ib.Y, side * half));
                if (side > 0) ZiratQuad(stone, innerA, innerB, outerB, outerA, ca, cb, cb, ca);
                else ZiratQuad(stone, outerA, outerB, innerB, innerA, ca, cb, cb, ca);
            }
        }

        if (crescent) AddZiratCrescent(stone, top, width, height, half, lean, Slab);
        stone.SetMaterial(PainterlyMaterialLibrary.ForColor(tone, "stone"));
        var mesh = stone.Commit();

        // Plinth: a low chamfered block in a darker, damp stone.
        var plinth = new SurfaceTool();
        plinth.Begin(Mesh.PrimitiveType.Triangles);
        var px = width * .5f + .045f;
        var pz = half + .05f;
        const float inset = .016f;
        var lower = new[] { new Vector3(px, -.04f, pz), new Vector3(-px, -.04f, pz), new Vector3(-px, -.04f, -pz), new Vector3(px, -.04f, -pz) };
        var upper = new[] { new Vector3(px - inset, plinthHeight, pz - inset), new Vector3(-px + inset, plinthHeight, pz - inset),
            new Vector3(-px + inset, plinthHeight, -pz + inset), new Vector3(px - inset, plinthHeight, -pz + inset) };
        for (var index = 0; index < 4; index++)
        {
            var next = (index + 1) % 4;
            var mid = (lower[index] + lower[next]) * .5f;
            var normal = new Vector3(mid.X, 0f, mid.Z).Normalized();
            ZiratQuad(plinth, lower[index], lower[next], upper[next], upper[index], normal, normal, normal, normal);
        }
        ZiratQuad(plinth, upper[0], upper[1], upper[2], upper[3], Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up);
        plinth.SetMaterial(PainterlyMaterialLibrary.ForColor("67665d", "stone"));
        return plinth.Commit(mesh);
    }

    /// <summary>A small raised crescent, horns up, in the upper part of the front.</summary>
    private static void AddZiratCrescent(SurfaceTool stone, ZiratTop top, float width, float height, float half,
        Basis lean, Func<Vector3, Vector3> slab)
    {
        var outerRadius = Mathf.Min(width * .13f, height * .075f);
        var innerRadius = outerRadius * .80f;
        var offset = outerRadius * .40f;
        var centre = new Vector2(0f, height * (top == ZiratTop.Keel ? .62f : .70f));
        var yCut = (outerRadius * outerRadius - innerRadius * innerRadius + offset * offset) / (2f * offset);
        var xCut = Mathf.Sqrt(Mathf.Max(outerRadius * outerRadius - yCut * yCut, 0f));
        var right = Mathf.Atan2(yCut, xCut);
        var polygon = new List<Vector2>();
        const int outerSteps = 18;
        for (var step = 0; step <= outerSteps; step++)
        {
            var angle = Mathf.Lerp(right, Mathf.Pi - right - Mathf.Tau, step / (float)outerSteps);
            polygon.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius);
        }
        var innerLeft = Mathf.Atan2(yCut - offset, -xCut);
        var innerRight = Mathf.Atan2(yCut - offset, xCut);
        // The inner arc runs back through the bottom of the inner circle.
        while (innerRight < innerLeft) innerRight += Mathf.Tau;
        const int innerSteps = 12;
        for (var step = 1; step < innerSteps; step++)
        {
            var angle = Mathf.Lerp(innerLeft, innerRight, step / (float)innerSteps);
            polygon.Add(centre + new Vector2(0f, offset) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius);
        }
        var indices = Geometry2D.TriangulatePolygon(polygon.ToArray());
        if (indices.Length == 0) return;
        const float relief = .006f;
        var front = half + relief;
        var normal = lean * Vector3.Back;
        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            var a = polygon[indices[index]];
            var b = polygon[indices[index + 1]];
            var c = polygon[indices[index + 2]];
            var va = slab(new(a.X, a.Y, front));
            var vb = slab(new(b.X, b.Y, front));
            var vc = slab(new(c.X, c.Y, front));
            if ((vb - va).Cross(vc - va).Dot(normal) < 0) (vb, vc) = (vc, vb);
            ZiratTriangle(stone, va, vb, vc, normal, normal, normal);
        }
        var area = 0f;
        for (var index = 0; index < polygon.Count; index++)
            area += polygon[index].Cross(polygon[(index + 1) % polygon.Count]);
        for (var index = 0; index < polygon.Count; index++)
        {
            var a = polygon[index];
            var b = polygon[(index + 1) % polygon.Count];
            var direction = b - a;
            var outward = area > 0 ? new Vector2(direction.Y, -direction.X) : new Vector2(-direction.Y, direction.X);
            var wall = lean * new Vector3(outward.X, outward.Y, 0f).Normalized();
            var a0 = slab(new(a.X, a.Y, half - .002f));
            var b0 = slab(new(b.X, b.Y, half - .002f));
            var a1 = slab(new(a.X, a.Y, front));
            var b1 = slab(new(b.X, b.Y, front));
            if ((b0 - a0).Cross(a1 - a0).Dot(wall) < 0) ZiratQuad(stone, a0, a1, b1, b0, wall, wall, wall, wall);
            else ZiratQuad(stone, a0, b0, b1, a1, wall, wall, wall, wall);
        }
    }

    /// <summary>A weathered square wooden post with a small two-pitch cap.</summary>
    private static ArrayMesh BuildZiratWoodPost(float height, uint seed, Vector3 leanDegrees)
    {
        var lean = Basis.FromEuler(new Vector3(Mathf.DegToRad(leanDegrees.X), Mathf.DegToRad(leanDegrees.Y),
            Mathf.DegToRad(leanDegrees.Z)));
        var wood = new SurfaceTool();
        wood.Begin(Mesh.PrimitiveType.Triangles);
        const float post = .055f;
        var shaft = height - .07f;
        ZiratBox(wood, lean, new Vector3(0f, (shaft - .04f) * .5f, 0f), new Vector3(post, shaft + .04f, post));
        // A plain board below the cap.
        ZiratBox(wood, lean, new Vector3(0f, shaft - .16f, post + .012f), new Vector3(.10f, .14f, .024f));
        // Two-pitch cap boards.
        foreach (var side in new[] { 1f, -1f })
        {
            var pitch = Basis.FromEuler(new Vector3(0f, 0f, side * Mathf.DegToRad(-38f)));
            var centre = new Vector3(side * .045f, shaft + .03f, 0f);
            ZiratBox(wood, lean * pitch, pitch.Inverse() * centre, new Vector3(.12f, .016f, .15f));
        }
        wood.SetMaterial(PainterlyMaterialLibrary.ForColor(ZiratJitter(seed, 7) > 0 ? "6d655a" : "625b51", "wood"));
        return wood.Commit();
    }

    /// <summary>Soft low snow drift banked round the base of a marker.</summary>
    private static MeshInstance3D BuildZiratSnowCollar(Vector2 footprint, uint seed)
    {
        var snow = new SurfaceTool();
        snow.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 16;
        var rings = new[] { (Scale: 1.0f, Y: -.06f), (Scale: .78f, Y: .045f), (Scale: .52f, Y: .085f) };
        var radiusX = footprint.X * .5f + .16f;
        var radiusZ = footprint.Y * .5f + .22f;
        Vector3 Ring(int ring, int segment)
        {
            var angle = segment / (float)segments * Mathf.Tau;
            var wobble = 1f + ZiratJitter(seed, 40 + segment) * .12f;
            var scale = rings[ring].Scale * (ring == 0 ? wobble : 1f + (wobble - 1f) * .5f);
            return new Vector3(Mathf.Cos(angle) * radiusX * scale, rings[ring].Y, Mathf.Sin(angle) * radiusZ * scale);
        }
        snow.SetSmoothGroup(0);
        for (var ring = 0; ring < rings.Length - 1; ring++)
        for (var segment = 0; segment < segments; segment++)
        {
            var next = (segment + 1) % segments;
            ZiratQuadAuto(snow, Ring(ring, segment), Ring(ring + 1, segment), Ring(ring + 1, next), Ring(ring, next));
        }
        var top = new Vector3(0f, rings[^1].Y + .01f, 0f);
        for (var segment = 0; segment < segments; segment++)
            ZiratTriangleAuto(snow, Ring(rings.Length - 1, segment), top, Ring(rings.Length - 1, (segment + 1) % segments));
        snow.GenerateNormals();
        snow.SetMaterial(PainterlyMaterialLibrary.ForColor("f1f5f9", "snow_ground"));
        var collar = new MeshInstance3D
        {
            Name = "ZiratMarkerSnowCollar",
            Mesh = snow.Commit(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        collar.SetMeta("visualOnly", true);
        return collar;
    }

    /// <summary>
    /// A low painted picket fence round one quiet grave, its posts seated on
    /// the real terrain. The stone stands at the front end, the plot runs back.
    /// </summary>
    private static MeshInstance3D BuildZiratGraveFence(MeshInstance3D marker, uint seed)
    {
        var fence = new SurfaceTool();
        fence.Begin(Mesh.PrimitiveType.Triangles);
        const float frontZ = .42f, backZ = -1.9f, halfX = .62f;
        float Ground(float x, float z)
        {
            var world = marker.ToGlobal(new Vector3(x, 0f, z));
            var ground = (float)AgentBAct1HeightField.CollisionGround(world.X, world.Z);
            return marker.ToLocal(new Vector3(world.X, ground, world.Z)).Y;
        }
        var corners = new[] { new Vector2(halfX, frontZ), new Vector2(-halfX, frontZ), new Vector2(-halfX, backZ), new Vector2(halfX, backZ) };
        var identity = Basis.Identity;
        for (var side = 0; side < 4; side++)
        {
            var a = corners[side];
            var b = corners[(side + 1) % 4];
            var length = a.DistanceTo(b);
            var groundA = Ground(a.X, a.Y);
            var groundB = Ground(b.X, b.Y);
            ZiratBox(fence, identity, new Vector3(a.X, groundA + .30f, a.Y), new Vector3(.05f, .76f, .05f));
            var pickets = Mathf.Max(2, (int)(length / .12f));
            for (var picket = 1; picket < pickets; picket++)
            {
                var t = picket / (float)pickets;
                var at = a.Lerp(b, t);
                var ground = Mathf.Lerp(groundA, groundB, t);
                var tall = .56f + ZiratJitter(seed, 60 + side * 40 + picket) * .015f;
                ZiratBox(fence, identity, new Vector3(at.X, ground + tall * .5f - .06f, at.Y),
                    new Vector3(side % 2 == 0 ? .016f : .055f, tall, side % 2 == 0 ? .055f : .016f));
            }
            foreach (var railHeight in new[] { .16f, .42f })
            {
                var start = new Vector3(a.X, groundA + railHeight, a.Y);
                var end = new Vector3(b.X, groundB + railHeight, b.Y);
                var direction = (end - start).Normalized();
                var up = Vector3.Up;
                var across = direction.Cross(up).Normalized();
                var railBasis = new Basis(direction, across.Cross(direction).Normalized(), across);
                ZiratBox(fence, railBasis, (start + end) * .5f, new Vector3(start.DistanceTo(end), .04f, .022f), worldCentre: true);
            }
        }
        fence.SetMaterial(PainterlyMaterialLibrary.ForColor(ZiratJitter(seed, 8) > 0 ? "8fa5b2" : "93a99a",
            ZiratJitter(seed, 8) > 0 ? "wood_painted_blue" : "wood_painted_green"));
        var instance = new MeshInstance3D { Name = "ZiratGraveFence", Mesh = fence.Commit() };
        instance.SetMeta("visualOnly", true);
        instance.SetMeta("culturalPlaceholder", "low painted grave fence; orientation awaits consultant");
        return instance;
    }

    private static void ZiratBox(SurfaceTool tool, Basis basis, Vector3 centre, Vector3 size, bool worldCentre = false)
    {
        var half = size * .5f;
        var origin = worldCentre ? centre : basis * centre;
        Vector3 At(float x, float y, float z) => origin + basis * new Vector3(x * half.X, y * half.Y, z * half.Z);
        var faces = new (Vector3 Normal, Vector3 U, Vector3 V)[]
        {
            (Vector3.Right, Vector3.Back, Vector3.Up), (Vector3.Left, Vector3.Forward, Vector3.Up),
            (Vector3.Up, Vector3.Right, Vector3.Forward), (Vector3.Back, Vector3.Left, Vector3.Up),
            (Vector3.Forward, Vector3.Right, Vector3.Up)
        };
        foreach (var (normal, u, v) in faces)
        {
            var n = (basis * normal).Normalized();
            var c = normal;
            ZiratQuad(tool, At(c.X - u.X - v.X, c.Y - u.Y - v.Y, c.Z - u.Z - v.Z),
                At(c.X + u.X - v.X, c.Y + u.Y - v.Y, c.Z + u.Z - v.Z),
                At(c.X + u.X + v.X, c.Y + u.Y + v.Y, c.Z + u.Z + v.Z),
                At(c.X - u.X + v.X, c.Y - u.Y + v.Y, c.Z - u.Z + v.Z), n, n, n, n);
        }
    }

    // Quads and triangles are wound to face their given normal, so the
    // painterly shader's default back-face culling keeps every face.
    private static void ZiratQuad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd)
    {
        ZiratTriangle(tool, a, b, c, na, nb, nc);
        ZiratTriangle(tool, a, c, d, na, nc, nd);
    }

    private static void ZiratTriangle(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
    {
        // Godot front faces are clockwise seen from outside.
        if ((b - a).Cross(c - a).Dot(na + nb + nc) > 0) (b, c, nb, nc) = (c, b, nc, nb);
        tool.SetNormal(na); tool.AddVertex(a);
        tool.SetNormal(nb); tool.AddVertex(b);
        tool.SetNormal(nc); tool.AddVertex(c);
    }

    private static void ZiratQuadAuto(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        ZiratTriangleAuto(tool, a, b, c);
        ZiratTriangleAuto(tool, a, c, d);
    }

    private static void ZiratTriangleAuto(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        // Snow collar faces point outward/upward from the marker's base.
        var centre = (a + b + c) / 3f;
        var outward = new Vector3(centre.X, Mathf.Max(centre.Y + .2f, .05f), centre.Z);
        if ((b - a).Cross(c - a).Dot(outward) > 0) (b, c) = (c, b);
        tool.AddVertex(a); tool.AddVertex(b); tool.AddVertex(c);
    }
}
