using Godot;

namespace Urman.Experiments.AgentBAct1;

/// <summary>
/// Deterministic port of the Agent B authored terrain height field
/// (assets/source/blender/agent_b_act1/agent_b_terrain.py). Used for the
/// traversal collider and for planting foliage exactly on the ground.
/// </summary>
public static class AgentBAct1HeightField
{
    public const int Step = 2;
    public const float MinX = -64f;
    // East of the FAP the village continues past the ravine (author, 2026-09-25):
    // the far bank carries the second half of the settlement, so the terrain
    // runs on to the forest ring there instead of stopping at the old rim.
    public const float MaxX = 94f;
    public const float MinZ = -152f;
    // Extend the arrival-side terrain with a low reverse-field grade beyond
    // the z≈52 framing band; the old 56 m cap ended immediately behind it.
    public const float MaxZ = 104f;

    private static readonly (double X, double Z, double RadiusX, double RadiusZ, double Rise)[] ForestShoulders =
    {
        (-8.0, -107.0, 5.5, 8.0, 1.1), (9.5, -113.0, 6.5, 9.0, 1.35),
        (-11.0, -121.0, 7.0, 10.0, 1.6), (12.0, -129.0, 8.0, 10.0, 1.4)
    };

    private static readonly (float X, float Z)[] MainAxis =
    {
        (0f, 40f), (0f, 9f), (-0.6f, -1.5f), (-1.2f, -8f), (0f, -19f),
        (-1f, -30f), (0f, -41.5f), (-0.4f, -53.5f)
    };

    private static readonly (float X, float Z)[] ZiratAxis =
    {
        (-0.4f, -53.5f), (0.3f, -64f), (0f, -76f), (-0.6f, -89.5f)
    };

    private static readonly (float X, float Z)[] KaraAxis =
    {
        (-0.6f, -89.5f), (0.9f, -96f), (-0.4f, -103f), (1.1f, -110f),
        (0f, -117f), (0.6f, -122.5f)
    };

    private static readonly (float X, float Z)[] FapAxis =
    {
        (0f, -10f), (4.5f, -12.5f), (10f, -17f), (17f, -21.5f), (23f, -25f),
        (28f, -26.2f)
    };

    private static readonly (float X, float Z)[] HouseAxis =
    {
        (-1.2f, -8f), (-6f, -5.5f), (-12f, -2.5f), (-19f, 0f), (-24f, 1.2f)
    };

    private static readonly float[] HalfWidths = { 2.8f, 2.3f, 1.4f, 2.1f, 1.75f };

    private static readonly ((float X, float Z)[] Points, double HalfWidth)[] RoadAxes =
    {
        (MainAxis, HalfWidths[0]), (FapAxis, HalfWidths[1]),
        (HouseAxis, HalfWidths[2]), (ZiratAxis, HalfWidths[3]), (KaraAxis, HalfWidths[4])
    };
    private static Vector3[]? _collisionFaces;
    private static Vector3[] CollisionFacesValue => _collisionFaces ??= BuildTerrainFaces();

    /// <summary>
    /// Author terrain strokes (URMAN Studio, spec WORLD10) from
    /// res://content/world/terrain.v1.json, applied on top of the generated
    /// field in authored order. The visual terrain and its collider are both
    /// built from <see cref="Ground"/>, so they always agree.
    /// </summary>
    private static TerrainStroke[]? _strokes;
    public const string StrokesPath = "res://content/world/terrain.v1.json";
    internal static string? StrokesOverrideForTest { get; set; }

    public readonly record struct TerrainStroke(string Mode, float X, float Z, float Radius, float Strength, float Target);

    private static TerrainStroke[] Strokes => _strokes ??= LoadStrokes(null);

    /// <summary>Studio preview / tests: replace the strokes (null = reread the file) and drop cached faces.</summary>
    public static void ReloadStrokes(string? json = null)
    {
        _strokes = LoadStrokes(json);
        _collisionFaces = null;
    }

    private static TerrainStroke[] LoadStrokes(string? json)
    {
        var path = StrokesOverrideForTest ?? StrokesPath;
        if (json is null)
        {
            if (!global::Godot.FileAccess.FileExists(path)) return [];
            json = global::Godot.FileAccess.GetFileAsString(path);
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty("entities").EnumerateArray()
            .Where(entity => entity.GetProperty("kind").GetString() == "terrain-stroke")
            .Select(entity => entity.GetProperty("params"))
            .Select(p => new TerrainStroke(
                p.GetProperty("mode").GetString()!,
                p.GetProperty("position")[0].GetSingle(), p.GetProperty("position")[2].GetSingle(),
                p.GetProperty("radius").GetSingle(),
                p.TryGetProperty("strength", out var strength) ? strength.GetSingle() : 0f,
                p.TryGetProperty("target", out var target) ? target.GetSingle() : 0f))
            .ToArray();
    }

    private static double ApplyStrokes(float x, float z, double height, System.Func<float, float, double> generated)
    {
        foreach (var stroke in Strokes)
        {
            var dx = x - stroke.X;
            var dz = z - stroke.Z;
            var t = (dx * dx + dz * dz) / (stroke.Radius * stroke.Radius);
            if (t >= 1f) continue;
            var falloff = (1.0 - t) * (1.0 - t);
            height = stroke.Mode switch
            {
                "raise" => height + stroke.Strength * falloff,
                "lower" => height - stroke.Strength * falloff,
                "flatten" => height + (stroke.Target - height) * falloff * System.Math.Clamp(stroke.Strength, 0, 1),
                "smooth" => height + (Average(x, z, stroke.Radius * .5f, generated) - height) * falloff * System.Math.Clamp(stroke.Strength, 0, 1),
                _ => height
            };
        }

        return height;
    }

    private static double Average(float x, float z, float reach, System.Func<float, float, double> generated) =>
        (generated(x + reach, z) + generated(x - reach, z) + generated(x, z + reach) + generated(x, z - reach) + generated(x, z)) / 5.0;

    private static readonly (float X, float Z, float Radius)[] Yards =
    {
        (-30f, 0f, 9.0f), (31f, -28f, 7.5f), (6.5f, -70f, 8.5f),
        (-9.5f, 0f, 4.2f), (-9f, -10f, 4.2f), (8f, -2f, 4.2f),
        (8f, -12f, 4.2f), (-9f, -32f, 4.2f), (8f, -30f, 4.2f),
        (-9.5f, -42f, 4.2f), (8.5f, -40f, 4.2f)
    };

    public static double ValueNoise(double x, double y)
    {
        var ix = System.Math.Floor(x);
        var iy = System.Math.Floor(y);
        var fx = x - ix;
        var fy = y - iy;
        var sx = fx * fx * (3.0 - 2.0 * fx);
        var sy = fy * fy * (3.0 - 2.0 * fy);
        double c00 = Hash2(ix, iy);
        double c10 = Hash2(ix + 1, iy);
        double c01 = Hash2(ix, iy + 1);
        double c11 = Hash2(ix + 1, iy + 1);
        var top = c00 + (c10 - c00) * sx;
        var bottom = c01 + (c11 - c01) * sx;
        return top + (bottom - top) * sy;
    }

    private static double Hash2(double ix, double iy)
    {
        var value = System.Math.Sin(ix * 127.1 + iy * 311.7) * 43758.5453123;
        return value - System.Math.Floor(value);
    }

    public static double Fbm(double x, double y, int octaves = 3)
    {
        double total = 0.0;
        double weight = 0.5;
        double frequency = 1.0;
        for (var i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, y * frequency) * weight;
            frequency *= 2.0;
            weight *= 0.5;
        }
        return total;
    }

    private static double DistanceToSegment(double px, double py, double ax,
        double ay, double bx, double by)
    {
        var abx = bx - ax;
        var aby = by - ay;
        var lengthSq = abx * abx + aby * aby;
        var t = lengthSq <= 1e-9 ? 0.0 : System.Math.Clamp(
            ((px - ax) * abx + (py - ay) * aby) / lengthSq, 0.0, 1.0);
        var cx = ax + abx * t;
        var cy = ay + aby * t;
        return System.Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    /// <summary>(distance to nearest road axis, half width of that axis).</summary>
    public static (double Distance, double HalfWidth) RoadInfo(float x, float z)
    {
        double best = double.MaxValue;
        double width = 3.0;
        foreach (var (points, halfWidth) in RoadAxes)
        {
            for (var i = 0; i < points.Length - 1; i++)
            {
                var d = DistanceToSegment(x, z, points[i].X, points[i].Z,
                    points[i + 1].X, points[i + 1].Z);
                if (d < best)
                {
                    best = d;
                    width = halfWidth;
                }
            }
        }
        return (best, width);
    }

    public static double Terrain(float x, float z)
    {
        var h = (Fbm(x * 0.03 + 40.0, z * 0.03, 3) - 0.5) * 1.5;
        var west = System.Math.Clamp((-12.0 - x) / 34.0, 0.0, 1.0);
        h += west * (1.2 + Fbm(x * 0.05, z * 0.05 + 11.0, 2) * 0.9);
        var east = System.Math.Clamp((x - 40.0) / 24.0, 0.0, 1.0);
        h -= east * 0.7;
        var kara = System.Math.Clamp((-90.0 - z) / 20.0, 0.0, 1.0);
        h -= kara * 0.55;
        h += kara * (Fbm(x * 0.16, z * 0.16, 2) - 0.5) * 0.5;
        // Match the authored low forest shoulders; preserve x +/-3 m route.
        if (z < -90f)
        {
            var edge = System.Math.Clamp((System.Math.Abs(x) - 3.0) / 3.0, 0.0, 1.0);
            edge *= edge * (3.0 - 2.0 * edge);
            var approach = System.Math.Clamp((-z - 90.0) / 8.0, 0.0, 1.0);
            approach *= approach * (3.0 - 2.0 * approach);
            foreach (var (cx, cz, rx, rz, rise) in ForestShoulders)
                h += edge * approach * rise * System.Math.Exp(
                    -System.Math.Pow((x - cx) / rx, 2) - System.Math.Pow((z - cz) / rz, 2));
        }
        h += (Fbm(x * 0.23, z * 0.23, 2) - 0.5) * 0.22;
        var dx = x - 6.5;
        var dz = z - (-70.0);
        h += System.Math.Exp(-(dx * dx + dz * dz) / 60.0) * 0.55;
        foreach (var (cx, cz, radius) in Yards)
        {
            var d = System.Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
            if (d < radius)
            {
                var blend = (System.Math.Cos(
                    System.Math.Min(1.0, d / radius) * System.Math.PI) + 1.0)
                    * 0.5 * 0.85;
                h *= 1.0 - blend;
            }
        }
        h += ReverseFieldRise(x, z);
        // Same watershed grades as the authored Blender terrain; all starts
        // lie outside the yards and the final playable route endpoint.
        var westRim = System.Math.Clamp((-x - 40.0) / 24.0, 0.0, 1.0);
        var eastRim = System.Math.Clamp((x - 79.0) / 16.0, 0.0, 1.0);
        // The far bank stands a little higher than the village side, so the
        // second half is seen across the ravine rather than hidden by its lip.
        var farBank = System.Math.Clamp((x - RavineCentre(z) - 4.6) / 2.5, 0.0, 1.0);
        h += farBank * farBank * (3.0 - 2.0 * farBank) * 1.1;
        var forestRim = System.Math.Clamp((-z - 128.0) / 24.0, 0.0, 1.0);
        h += RimGrade(westRim, x, z, 0.7);
        h += RimGrade(eastRim, x, z, 2.1);
        h += RimGrade(forestRim, x, z, 4.3);
        return h;
    }

    private static double RimGrade(double rim, double x, double z, double phase)
        => rim * rim * (3.0 - 2.0 * rim)
            * (5.5 + 1.3 * System.Math.Sin(z * 0.055 + x * 0.035 + phase));

    private static double ReverseFieldRise(double x, double z)
    {
        var along = System.Math.Clamp((z - 52.0) / 52.0, 0.0, 1.0);
        along *= along * (3.0 - 2.0 * along);
        var centre = -8.0 + (z - 52.0) * 0.10;
        var lateral = System.Math.Clamp(1.0 - System.Math.Abs(x - centre) / 52.0,
            0.0, 1.0);
        lateral *= lateral * (3.0 - 2.0 * lateral);
        var variation = Fbm(x * 0.035 + 19.0, z * 0.035 - 7.0, 2);
        return along * (0.65 + 0.35 * lateral) * (5.0 + 3.0 * variation);
    }

    /// <summary>
    /// Canon river channel between the village and the forest. Carved into the
    /// terrain so the boundary is visible and physical instead of an invisible
    /// wall: a meandering ravine about 2.6 m deep and 13 m wide, flattened to zero
    /// across the road corridor where the authored culvert carries the route over
    /// the water (author-confirmed canon, 2026-09-14). Frozen bed and snow banks are
    /// presentation; the slopes themselves stop the player.
    /// </summary>
    public static double RiverChannel(float x, float z)
    {
        var meander = -88.0 + 3.2 * System.Math.Sin(x / 12.0) + 1.4 * System.Math.Sin(x / 4.3);
        var distance = System.Math.Abs(z - meander);
        const double halfWidth = 4.6;
        if (distance >= halfWidth)
        {
            return 0.0;
        }

        // 3.4 m deep over a 9.2 m channel keeps the banks past the controller's
        // 45-degree floor limit, so the ravine is the boundary and no invisible
        // wall is needed.
        var profile = System.Math.Cos(System.Math.PI * 0.5 * distance / halfWidth);
        var (roadDistance, roadHalfWidth) = RoadInfo(x, z);
        var roadGap = System.Math.Clamp((roadDistance - roadHalfWidth - 1.2f) / 3.0f, 0.0, 1.0);
        return -3.4 * profile * roadGap;
    }

    /// <summary>
    /// The ravine with a stream that splits the village (author, 2026-09-25).
    /// It runs north from the river east of the FAP; the accessible half of
    /// Act I lies west of it, the second half on the far bank. Same profile as
    /// the river channel: 3.4 m deep over 9.2 m, so its sides are past the
    /// controller's floor limit. Only the collapsed bridge spans it.
    /// </summary>
    public static double RavineCentre(double z)
        => 50.5 + 1.6 * System.Math.Sin(z / 11.0) + 0.7 * System.Math.Sin(z / 4.1);

    public const double RavineHalfWidth = 4.6;

    public static double RavineChannel(float x, float z)
    {
        if (z < -92f) return 0.0;
        var distance = System.Math.Abs(x - RavineCentre(z));
        if (distance >= RavineHalfWidth) return 0.0;
        return -3.4 * System.Math.Cos(System.Math.PI * 0.5 * distance / RavineHalfWidth);
    }

    public static double RiverMeander(float x)
        => -88.0 + 3.2 * System.Math.Sin(x / 12.0) + 1.4 * System.Math.Sin(x / 4.3);

    public static double Ground(float x, float z)
    {
        var generated = GeneratedGround(x, z);
        return Strokes.Length == 0 ? generated : ApplyStrokes(x, z, generated, GeneratedGround);
    }

    /// <summary>The generator's ground before author strokes.</summary>
    public static double GeneratedGround(float x, float z)
    {
        var baseHeight = Terrain(x, z);
        var (distance, halfWidth) = RoadInfo(x, z);
        var channel = System.Math.Min(RiverChannel(x, z), RavineChannel(x, z));
        if (channel != 0.0)
        {
            return baseHeight + channel;
        }

        if (distance >= halfWidth + 3.0)
        {
            return baseHeight;
        }
        var crown = baseHeight - 0.02;
        if (distance <= halfWidth)
        {
            return crown;
        }
        var blend = 1.0 - (distance - halfWidth) / 3.0;
        return baseHeight * (1.0 - blend) + crown * blend;
    }

    /// <summary>Ground with the same grid jitter as the authored mesh, so the collider hugs the visual terrain.</summary>
    public static double GroundWithJitter(float x, float z)
    {
        var (distance, halfWidth) = RoadInfo(x, z);
        var margin = distance < halfWidth + 1.2 ? 0.75 : 0.22;
        var jx = (ValueNoise(x * 0.9, z * 0.77) - 0.5) * 2.0 * margin;
        var jy = (ValueNoise(z * 0.9 + 3.1, x * 0.77) - 0.5) * 2.0 * margin;
        return Ground(x + (float)jx, z + (float)-jy * -1f);
    }

    /// <summary>Presentation support sampled from the unchanged traversal triangles.</summary>
    public static float CollisionGround(float x, float z)
    {
        var columns = (int)((MaxX - MinX) / Step);
        var rows = (int)((MaxZ - MinZ) / Step);
        var column = (int)System.Math.Floor((x - MinX) / Step);
        var row = (int)System.Math.Floor((z - MinZ) / Step);
        var faces = CollisionFacesValue;
        // Jitter is bounded below one grid cell; only these nine cells can
        // contain the query. This is the collider's actual diagonal/winding.
        for (var iy = System.Math.Max(0, row - 1); iy <= System.Math.Min(rows - 1, row + 1); iy++)
        for (var ix = System.Math.Max(0, column - 1); ix <= System.Math.Min(columns - 1, column + 1); ix++)
        for (var triangle = 0; triangle < 2; triangle++)
        {
            var index = (iy * columns + ix) * 6 + triangle * 3;
            var a = faces[index]; var b = faces[index + 1]; var c = faces[index + 2];
            var denominator = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / denominator;
            var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / denominator;
            if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
                return u * a.Y + v * b.Y + (1f - u - v) * c.Y;
        }
        // Beyond the physical terrain the existing analytic field supports
        // the presentation-only horizon; no traversal surface is invented.
        if (x > MinX + Step && x < MaxX - Step && z > MinZ + Step && z < MaxZ - Step)
            throw new System.InvalidOperationException($"Terrain triangle missing at {x}, {z}");
        return (float)Ground(x, z);
    }

    public static Vector3[] BuildTerrainFaces()
    {
        var cols = (int)((MaxX - MinX) / Step) + 1;
        var rows = (int)((MaxZ - MinZ) / Step) + 1;
        var verts = new Vector3[rows * cols];
        for (var iy = 0; iy < rows; iy++)
        {
            var z = MinZ + iy * Step;
            for (var ix = 0; ix < cols; ix++)
            {
                var x = MinX + ix * Step;
                var (distance, halfWidth) = RoadInfo(x, z);
                var margin = distance < halfWidth + 1.2f ? 0.75f : 0.22f;
                var jx = (float)((ValueNoise(x * 0.9, z * 0.77) - 0.5) * 2.0 * margin);
                var jy = (float)((ValueNoise(z * 0.9 + 3.1, x * 0.77) - 0.5) * 2.0 * margin);
                var xj = x + jx;
                var zj = z + jy;
                verts[iy * cols + ix] = new Vector3(xj, (float)Ground(xj, zj), zj);
            }
        }
        var faces = new Vector3[(rows - 1) * (cols - 1) * 6];
        var p = 0;
        for (var iy = 0; iy < rows - 1; iy++)
        {
            for (var ix = 0; ix < cols - 1; ix++)
            {
                var a = iy * cols + ix;
                // Match the authored winding (up-facing quads).
                faces[p++] = verts[a];
                faces[p++] = verts[a + cols + 1];
                faces[p++] = verts[a + cols];
                faces[p++] = verts[a];
                faces[p++] = verts[a + 1];
                faces[p++] = verts[a + cols + 1];
            }
        }
        return faces;
    }
}
