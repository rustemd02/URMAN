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
    public const float MaxX = 66f;
    public const float MinZ = -152f;
    // Extend the arrival-side terrain beyond the reverse-view framing band.
    // The previous 44 m cap left the authored village silhouettes at z≈49–53
    // outside the shared ground envelope.
    public const float MaxZ = 56f;

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
        ((float X, float Z)[] Points, double HalfWidth)[] axes =
        {
            (MainAxis, HalfWidths[0]), (FapAxis, HalfWidths[1]),
            (HouseAxis, HalfWidths[2]), (ZiratAxis, HalfWidths[3]),
            (KaraAxis, HalfWidths[4])
        };
        foreach (var (points, halfWidth) in axes)
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
        return h;
    }

    public static double Ground(float x, float z)
    {
        var baseHeight = Terrain(x, z);
        var (distance, halfWidth) = RoadInfo(x, z);
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
