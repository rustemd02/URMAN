using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Capture-only diagnostic for a visible surface that has no physics body.
/// Reports actual triangle intersections along chosen camera pixels, so an
/// exterior snow mesh or rail inside a room can be traced to its owner.
/// It neither moves the player nor changes runtime state or normal rendering.
/// </summary>
internal static class Act1VisibleSurfaceProbe
{
    internal sealed record CapturedStrip(Vector2I[] Pixels, MeshInstance3D[] Candidates);
    private sealed record SurfaceHit(float Distance, MeshInstance3D Mesh, Vector3 Point, int Triangle, Vector3 A, Vector3 B, Vector3 C);

    internal static void Log(Node root, Camera3D camera, string label, params Vector2[] normalizedPixels)
    {
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var candidates = Candidates(root, camera);
        foreach (var pixel in normalizedPixels)
        {
            var hits = Hits(camera, candidates, pixel * viewport);
            PrintHits(label, pixel, hits);
        }
    }

    internal static (MeshInstance3D Mesh, float Distance, Vector3 Point)? Nearest(Node root, Camera3D camera, Vector2 normalizedPixel)
    {
        var hit = Hits(camera, Candidates(root, camera), normalizedPixel * camera.GetViewport().GetVisibleRect().Size).FirstOrDefault();
        return hit is null ? null : (hit.Mesh, hit.Distance, hit.Point);
    }

    /// <summary>Derive sample coordinates from this actual image, rather than
    /// reusing UVs from another camera pose. Triangle hits remain candidates;
    /// optional isolated render captures establish the visible fragment owner.</summary>
    internal static CapturedStrip LogCapturedStrip(Node root, Camera3D camera, string label, Image frame)
    {
        var width = frame.GetWidth(); var height = frame.GetHeight();
        const int stride = 2;
        var left = (int)(width * .55f); var right = (int)(width * .88f);
        var top = (int)(height * .25f); var bottom = (int)(height * .95f);
        var columns = (right - left) / stride + 1; var rows = (bottom - top) / stride + 1;
        var green = new bool[columns * rows]; var seen = new bool[green.Length];
        for (var y = 0; y < rows; y++) for (var x = 0; x < columns; x++)
            green[y * columns + x] = IsStripColor(frame.GetPixel(left + x * stride, top + y * stride));
        var components = new List<List<Vector2I>>();
        for (var index = 0; index < green.Length; index++)
        {
            if (!green[index] || seen[index]) continue;
            var component = new List<Vector2I>(); var queue = new Queue<int>(); queue.Enqueue(index); seen[index] = true;
            while (queue.TryDequeue(out var at))
            {
                var x = at % columns; var y = at / columns;
                component.Add(new(left + x * stride, top + y * stride));
                foreach (var dx in new[] { -1, 0, 1 }) foreach (var dy in new[] { -1, 0, 1 })
                {
                    var nx = x + dx; var ny = y + dy;
                    if (nx < 0 || nx >= columns || ny < 0 || ny >= rows) continue;
                    var next = ny * columns + nx;
                    if (green[next] && !seen[next]) { seen[next] = true; queue.Enqueue(next); }
                }
            }
            if (component.Count >= 40 && component.Max(p => p.Y) - component.Min(p => p.Y) >= height * .20f)
                components.Add(component);
        }
        var strip = components.OrderByDescending(c => c.Max(p => p.Y) - c.Min(p => p.Y)).FirstOrDefault();
        if (strip is null)
        {
            GD.Print($"act1-visible-strip: {label} image={width}x{height} no-long-green-component-in-current-frame; historical UVs not substituted");
            return new([], []);
        }
        var minY = strip.Min(p => p.Y); var maxY = strip.Max(p => p.Y);
        var pixels = new[] { .25f, .55f, .80f }.Select(fraction =>
        {
            var row = strip.GroupBy(p => p.Y).OrderBy(g => Math.Abs(g.Key - Mathf.Lerp(minY, maxY, fraction))).First();
            var ordered = row.OrderBy(p => p.X).ToArray(); return ordered[ordered.Length / 2];
        }).Distinct().ToArray();
        GD.Print($"act1-visible-strip: {label} image={width}x{height} camera={camera.GlobalTransform} componentSamples={strip.Count} bounds=({strip.Min(p => p.X)},{minY})..({strip.Max(p => p.X)},{maxY})");
        var meshes = Candidates(root, camera); var owners = new List<MeshInstance3D>();
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        foreach (var pixel in pixels)
        {
            var normalized = new Vector2((pixel.X + .5f) / width, (pixel.Y + .5f) / height);
            GD.Print($"act1-visible-strip-pixel: {label} pixel={pixel} rgb={Rgb(frame.GetPixel(pixel.X, pixel.Y))} uv={normalized}");
            var hits = Hits(camera, meshes, normalized * viewport);
            PrintHits(label, normalized, hits);
            owners.AddRange(hits.Take(3).Select(hit => hit.Mesh));
        }
        return new(pixels, owners.Distinct().Take(4).ToArray());
    }

    internal static void LogIsolatedPixels(string label, string hiddenOwner, Image baseline, Image isolated, CapturedStrip strip)
    {
        foreach (var pixel in strip.Pixels)
        {
            var before = baseline.GetPixel(pixel.X, pixel.Y); var after = isolated.GetPixel(pixel.X, pixel.Y);
            GD.Print($"act1-visible-strip-isolation: {label} hidden={hiddenOwner} pixel={pixel} beforeRGB={Rgb(before)} afterRGB={Rgb(after)} greenBefore={IsStripColor(before)} greenAfter={IsStripColor(after)}");
        }
    }

    private static bool IsStripColor(Color c) => c.R > .10f && c.R < .52f && c.G < .52f
        && c.G >= c.R * .97f && c.R > c.B * 1.15f && c.G > c.B * 1.22f;
    private static string Rgb(Color c) => $"({Mathf.RoundToInt(c.R * 255)},{Mathf.RoundToInt(c.G * 255)},{Mathf.RoundToInt(c.B * 255)})";
    private static MeshInstance3D[] Candidates(Node root, Camera3D camera) => Descendants(root).OfType<MeshInstance3D>()
        .Where(mesh => mesh.Mesh is not null && mesh.IsVisibleInTree() && (mesh.Layers & camera.CullMask) != 0).ToArray();

    private static List<SurfaceHit> Hits(Camera3D camera, MeshInstance3D[] candidates, Vector2 pixel)
    {
        var origin = camera.ProjectRayOrigin(pixel); var direction = camera.ProjectRayNormal(pixel).Normalized();
        var hits = new List<SurfaceHit>();
        foreach (var mesh in candidates)
        {
            var data = mesh.Mesh!; var inverse = mesh.GlobalTransform.AffineInverse();
            var localOrigin = inverse * origin; var localDirection = inverse.Basis * direction;
            if (!IntersectsBounds(data.GetAabb(), localOrigin, localDirection, 25f)) continue;
            var centreDistance = camera.GlobalPosition.DistanceTo(mesh.GlobalTransform * data.GetAabb().GetCenter());
            if (mesh.VisibilityRangeEnd > 0f && centreDistance > mesh.VisibilityRangeEnd + mesh.VisibilityRangeEndMargin) continue;
            if (mesh.VisibilityRangeBegin > 0f && centreDistance < mesh.VisibilityRangeBegin - mesh.VisibilityRangeBeginMargin) continue;
            var material = mesh.MaterialOverride ?? mesh.GetActiveMaterial(0);
            if (material is BaseMaterial3D { Transparency: not BaseMaterial3D.TransparencyEnum.Disabled } transparent
                && transparent.AlbedoColor.A <= .02f) continue;
            var faces = data.GetFaces(); var nearest = float.PositiveInfinity; var face = -1;
            for (var i = 0; i + 2 < faces.Length; i += 3)
                if (TriangleDistance(localOrigin, localDirection, faces[i], faces[i + 1], faces[i + 2], out var distance) && distance < nearest)
                { nearest = distance; face = i; }
            if (nearest < 25f && face >= 0)
                hits.Add(new(nearest, mesh, origin + direction * nearest, face / 3,
                    mesh.GlobalTransform * faces[face], mesh.GlobalTransform * faces[face + 1], mesh.GlobalTransform * faces[face + 2]));
        }
        return hits.OrderBy(hit => hit.Distance).Take(5).ToList();
    }

    private static void PrintHits(string label, Vector2 pixel, IReadOnlyList<SurfaceHit> hits)
    {
        foreach (var hit in hits)
        {
            var material = hit.Mesh.MaterialOverride ?? hit.Mesh.GetActiveMaterial(0);
            var detail = material is ShaderMaterial shader
                ? $"shader={shader.Shader?.ResourcePath} snow={shader.GetShaderParameter("snow_coverage")}"
                : material is BaseMaterial3D basic ? $"albedo={basic.AlbedoColor} texture={basic.AlbedoTexture?.ResourcePath}" : material?.GetClass() ?? "none";
            GD.Print($"act1-visible-surface: {label} pixel={pixel} distance={hit.Distance:F3} point={hit.Point} mesh={hit.Mesh.GetPath()} triangle={hit.Triangle} vertices=[{hit.A};{hit.B};{hit.C}] {detail}");
        }
        if (hits.Count == 0) GD.Print($"act1-visible-surface: {label} pixel={pixel} no-visible-triangle-within25m");
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool IntersectsBounds(Aabb bounds, Vector3 origin, Vector3 direction, float limit)
    {
        var near = 0f;
        var far = limit;
        for (var axis = 0; axis < 3; axis++)
        {
            var low = bounds.Position[axis] - .0001f;
            var high = bounds.End[axis] + .0001f;
            if (Math.Abs(direction[axis]) < .000001f)
            {
                if (origin[axis] < low || origin[axis] > high) return false;
                continue;
            }
            var a = (low - origin[axis]) / direction[axis];
            var b = (high - origin[axis]) / direction[axis];
            near = Math.Max(near, Math.Min(a, b));
            far = Math.Min(far, Math.Max(a, b));
            if (near > far) return false;
        }
        return true;
    }

    private static bool TriangleDistance(Vector3 origin, Vector3 direction,
        Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        var edge1 = b - a;
        var edge2 = c - a;
        var p = direction.Cross(edge2);
        var determinant = edge1.Dot(p);
        if (Math.Abs(determinant) < .0000001f) return false;
        var inverse = 1f / determinant;
        var offset = origin - a;
        var u = offset.Dot(p) * inverse;
        if (u < 0 || u > 1) return false;
        var q = offset.Cross(edge1);
        var v = direction.Dot(q) * inverse;
        if (v < 0 || u + v > 1) return false;
        distance = edge2.Dot(q) * inverse;
        return distance > .001f;
    }
}
