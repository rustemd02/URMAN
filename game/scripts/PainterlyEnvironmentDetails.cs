using Godot;

namespace Urman.Godot;

public static class PainterlyEnvironmentDetails
{
    /// <summary>
    /// Adds a small cluster of overlapping, irregular low-poly puddle patches.
    /// The silhouette is authored geometry only; specular/roughness tuning is a
    /// separate material gate and is intentionally not hidden in this helper.
    /// </summary>
    public static Node3D AddPuddleCluster(
        Node3D root,
        string name,
        Vector3 origin,
        Vector2 size,
        string color = "56646a",
        IReadOnlyList<float>? patchYOffsetCorrections = null)
    {
        if (patchYOffsetCorrections is not null && patchYOffsetCorrections.Count != 3)
        {
            throw new ArgumentException("Puddle patch Y corrections must contain exactly three entries.", nameof(patchYOffsetCorrections));
        }

        var cluster = new Node3D
        {
            Name = name,
            Position = origin
        };
        var material = PainterlyMaterialLibrary.ForColor(color);
        var patches = new[]
        {
            (Offset: new Vector3(-0.16f, 0f, 0.04f), Scale: new Vector3(0.76f, 1f, 0.54f), Rotation: -8f),
            (Offset: new Vector3(0.18f, 0.002f, -0.08f), Scale: new Vector3(0.58f, 1f, 0.72f), Rotation: 13f),
            (Offset: new Vector3(0.02f, 0.004f, 0.17f), Scale: new Vector3(0.42f, 1f, 0.36f), Rotation: -22f)
        };
        for (var patchIndex = 0; patchIndex < patches.Length; patchIndex++)
        {
            var (offset, patchScale, rotation) = patches[patchIndex];
            var yCorrection = patchYOffsetCorrections?[patchIndex] ?? 0f;
            cluster.AddChild(new MeshInstance3D
            {
                Name = "PuddlePatch",
                // Patches share an intentionally irregular silhouette. A
                // benchmark may additionally need a measured local seat on
                // its own relief cell; never turn this into a cluster-wide
                // translation because the three offsets sample different
                // crowns/ruts.
                Position = offset + new Vector3(0f, yCorrection, 0f),
                RotationDegrees = new Vector3(0f, rotation, 0f),
                Scale = new Vector3(size.X * patchScale.X, size.Y * patchScale.Y, size.X * patchScale.Z),
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.5f,
                    BottomRadius = 0.5f,
                    Height = 0.012f,
                    RadialSegments = 10,
                    Rings = 1
                },
                MaterialOverride = material
            });
        }

        cluster.SetMeta("puddleGeometry", "low-poly-overlap-proxy");
        cluster.SetMeta("wetnessMaterialStatus", "OPEN-roughness-review");
        cluster.SetMeta("puddlePatchYCorrections", patchYOffsetCorrections is null
            ? "0.000000,0.000000,0.000000"
            : string.Join(",", patchYOffsetCorrections.Select(correction => correction.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture))));
        root.AddChild(cluster);
        return cluster;
    }

    /// <summary>
    /// Builds a small authored road relief instead of a single flat greybox.
    /// The visible surface is a deliberately coarse grid: a shallow crown,
    /// two broken wheel-rut families and quiet longitudinal variation.  The
    /// matching collision cells keep the benchmark walkable without bringing
    /// a heightmap or shader-specific dependency into the scene.
    /// </summary>
    public static StaticBody3D AddRoadRelief(
        Node3D root,
        string name,
        float width,
        float length,
        Vector3 origin,
        string color,
        string surface = "earth",
        float yawDegrees = 0f,
        int crossSections = 9,
        int lengthSections = 28)
    {
        if (width <= 0f || length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Road relief dimensions must be positive.");
        }

        if (crossSections < 3 || lengthSections < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(crossSections), "Road relief requires at least a 3x2 grid.");
        }

        var road = new StaticBody3D
        {
            Name = name,
            Position = origin,
            RotationDegrees = new Vector3(0f, yawDegrees, 0f)
        };
        var heights = new float[crossSections, lengthSections];
        var vertices = new Vector3[crossSections * lengthSections];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var crossStep = width / (crossSections - 1);
        var lengthStep = length / (lengthSections - 1);
        var minHeight = float.MaxValue;
        var maxHeight = float.MinValue;

        for (var zIndex = 0; zIndex < lengthSections; zIndex++)
        {
            var z = -length * 0.5f + zIndex * lengthStep;
            for (var xIndex = 0; xIndex < crossSections; xIndex++)
            {
                var x = -width * 0.5f + xIndex * crossStep;
                var height = RoadReliefHeight(x, z, width, length);
                heights[xIndex, zIndex] = height;
                var vertexIndex = zIndex * crossSections + xIndex;
                vertices[vertexIndex] = new Vector3(x, height, z);
                uvs[vertexIndex] = new Vector2(x / width + 0.5f, z / length + 0.5f);
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        for (var zIndex = 0; zIndex < lengthSections; zIndex++)
        {
            for (var xIndex = 0; xIndex < crossSections; xIndex++)
            {
                var left = heights[Mathf.Max(0, xIndex - 1), zIndex];
                var right = heights[Mathf.Min(crossSections - 1, xIndex + 1), zIndex];
                var back = heights[xIndex, Mathf.Max(0, zIndex - 1)];
                var forward = heights[xIndex, Mathf.Min(lengthSections - 1, zIndex + 1)];
                var normal = new Vector3(
                    -(right - left) / Mathf.Max(crossStep * 2f, 0.001f),
                    1f,
                    -(forward - back) / Mathf.Max(lengthStep * 2f, 0.001f));
                normals[zIndex * crossSections + xIndex] = normal.Normalized();
            }
        }

        var indices = new int[(crossSections - 1) * (lengthSections - 1) * 6];
        var index = 0;
        for (var zIndex = 0; zIndex < lengthSections - 1; zIndex++)
        {
            for (var xIndex = 0; xIndex < crossSections - 1; xIndex++)
            {
                var topLeft = zIndex * crossSections + xIndex;
                var topRight = topLeft + 1;
                var bottomLeft = (zIndex + 1) * crossSections + xIndex;
                var bottomRight = bottomLeft + 1;
                indices[index++] = topLeft;
                indices[index++] = bottomLeft;
                indices[index++] = topRight;
                indices[index++] = topRight;
                indices[index++] = bottomLeft;
                indices[index++] = bottomRight;
            }
        }

        var surfaceArrays = new global::Godot.Collections.Array();
        surfaceArrays.Resize((int)Mesh.ArrayType.Max);
        surfaceArrays[(int)Mesh.ArrayType.Vertex] = vertices;
        surfaceArrays[(int)Mesh.ArrayType.Normal] = normals;
        surfaceArrays[(int)Mesh.ArrayType.TexUV] = uvs;
        surfaceArrays[(int)Mesh.ArrayType.Index] = indices;
        var reliefMesh = new ArrayMesh();
        reliefMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArrays);
        road.AddChild(new MeshInstance3D
        {
            Name = "ReliefSurface",
            Mesh = reliefMesh,
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
        });

        // The coarse boxes intentionally follow the same grid as the visual
        // mesh. Their top is at the local cell maximum, so a first-person
        // traversal never falls through a relief valley or crosses a seam.
        for (var zIndex = 0; zIndex < lengthSections - 1; zIndex++)
        {
            for (var xIndex = 0; xIndex < crossSections - 1; xIndex++)
            {
                var cellHeight = Mathf.Max(
                    0.02f,
                    Mathf.Max(
                        heights[xIndex, zIndex],
                        Mathf.Max(
                            heights[xIndex + 1, zIndex],
                            Mathf.Max(heights[xIndex, zIndex + 1], heights[xIndex + 1, zIndex + 1]))));
                var cellCenter = new Vector3(
                    -width * 0.5f + (xIndex + 0.5f) * crossStep,
                    cellHeight * 0.5f,
                    -length * 0.5f + (zIndex + 0.5f) * lengthStep);
                road.AddChild(new CollisionShape3D
                {
                    Name = $"ReliefCollision_{xIndex}_{zIndex}",
                    Position = cellCenter,
                    Shape = new BoxShape3D
                    {
                        Size = new Vector3(crossStep, cellHeight, lengthStep)
                    }
                });
            }
        }

        road.SetMeta("reliefGrid", $"{crossSections}x{lengthSections}");
        road.SetMeta("reliefMinHeight", minHeight);
        road.SetMeta("reliefMaxHeight", maxHeight);
        road.SetMeta("reliefCollisionCells", (crossSections - 1) * (lengthSections - 1));
        road.SetMeta("reliefSurface", surface);
        root.AddChild(road);
        return road;
    }

    private static float RoadReliefHeight(float x, float z, float width, float length)
    {
        var halfWidth = width * 0.5f;
        var normalizedX = Mathf.Clamp(Mathf.Abs(x) / Mathf.Max(halfWidth, 0.001f), 0f, 1f);
        var crown = (1f - normalizedX) * 0.052f;
        var leftRut = -0.041f * Mathf.Exp(-Mathf.Pow((x + width * 0.24f) / 0.24f, 2f));
        var rightRut = -0.037f * Mathf.Exp(-Mathf.Pow((x - width * 0.24f) / 0.24f, 2f));
        var broadBreakup = 0.009f * Mathf.Sin(z * 0.34f + x * 0.83f)
            + 0.006f * Mathf.Sin(z * 0.71f - x * 1.17f + 1.4f);
        var edgeSoftening = -0.008f * Mathf.Pow(normalizedX, 3f);
        var endFade = 0.004f * Mathf.Cos(z / Mathf.Max(length, 0.001f) * Mathf.Pi);
        return Mathf.Clamp(0.038f + crown + leftRut + rightRut + broadBreakup + edgeSoftening + endFade, 0.018f, 0.12f);
    }

    public static void AddFence(Node3D root, float x, float z, int count)
    {
        var fence = new StaticBody3D { Name = "Fence" };
        var material = PainterlyMaterialLibrary.ForColor("5b4a37", "wood");
        for (var index = 0; index < count; index++)
        {
            var height = 1.05f + index % 4 * 0.09f;
            fence.AddChild(new MeshInstance3D
            {
                Name = "FencePlank",
                Position = new Vector3(x, height * 0.5f, z - index * 0.58f),
                RotationDegrees = new Vector3(0, 0, index % 3 - 1),
                Mesh = new BoxMesh { Size = new Vector3(0.12f, height, 0.5f) },
                MaterialOverride = material
            });
        }

        var length = count * 0.58f;
        foreach (var height in new[] { 0.38f, 0.86f })
        {
            fence.AddChild(new MeshInstance3D
            {
                Name = "FenceRail",
                Position = new Vector3(x, height, z - (count - 1) * 0.29f),
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.12f, length) },
                MaterialOverride = material
            });
        }

        fence.AddChild(new CollisionShape3D
        {
            Position = new Vector3(x, 0.62f, z - (count - 1) * 0.29f),
            Shape = new BoxShape3D { Size = new Vector3(0.18f, 1.24f, length) }
        });
        root.AddChild(fence);
    }

    public static void AddPine(Node3D root, Vector3 origin, float height, string foliageColor = "26372f")
    {
        var trunkMaterial = PainterlyMaterialLibrary.ForColor("40352d", "wood");
        var foliageMaterial = PainterlyMaterialLibrary.ForColor(foliageColor, "foliage");
        var body = new StaticBody3D { Name = "Pine", Position = origin };
        body.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, height * 0.28f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.2f, Height = height * 0.56f, RadialSegments = 7 },
            MaterialOverride = trunkMaterial
        });

        var layers = new[]
        {
            (Y: 0.46f, Radius: 0.25f, Height: 0.12f, Offset: -0.035f),
            (Y: 0.59f, Radius: 0.22f, Height: 0.115f, Offset: 0.045f),
            (Y: 0.71f, Radius: 0.18f, Height: 0.105f, Offset: -0.025f),
            (Y: 0.82f, Radius: 0.135f, Height: 0.095f, Offset: 0.02f)
        };
        foreach (var (y, radius, layerHeight, offset) in layers)
        {
            body.AddChild(new MeshInstance3D
            {
                Position = new Vector3(height * offset, height * y, -height * offset * 0.45f),
                RotationDegrees = new Vector3(0, y * 71, 0),
                Scale = new Vector3(height * radius, height * layerHeight, height * radius * 0.78f),
                // A shallow tapered tier keeps the readable low-poly silhouette
                // while avoiding the repeated inflated-sphere crown that made
                // the first Godot frames read as greybox placeholders.
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.12f,
                    BottomRadius = 0.92f,
                    Height = 1.0f,
                    RadialSegments = 7,
                    Rings = 1
                },
                MaterialOverride = foliageMaterial
            });
        }

        // Break the repeated four-cone silhouette with a few low-poly side
        // boughs. The offsets are deterministic per tree position, so captures
        // stay reproducible while the forest gains a hand-placed irregularity.
        var crownPhase = Mathf.Abs(Mathf.Sin(origin.X * 1.73f + origin.Z * 0.41f));
        var boughs = new[]
        {
            (Height: 0.55f, Side: -0.19f, Depth: 0.08f, Scale: 0.19f),
            (Height: 0.67f, Side: 0.16f, Depth: -0.12f, Scale: 0.16f),
            (Height: 0.77f, Side: -0.11f, Depth: -0.16f, Scale: 0.12f)
        };
        for (var index = 0; index < boughs.Length; index++)
        {
            var bough = boughs[index];
            var side = bough.Side * (0.82f + crownPhase * 0.38f);
            body.AddChild(new MeshInstance3D
            {
                Position = new Vector3(height * side, height * bough.Height, height * (bough.Depth + (index == 1 ? crownPhase * 0.08f : 0))),
                Scale = new Vector3(height * bough.Scale, height * bough.Scale * 0.58f, height * bough.Scale * 0.84f),
                RotationDegrees = new Vector3(0, crownPhase * 35 + index * 57, index % 2 == 0 ? -10 : 8),
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.08f,
                    BottomRadius = 0.62f,
                    Height = 1.0f,
                    RadialSegments = 6,
                    Rings = 1
                },
                MaterialOverride = foliageMaterial
            });
        }

        body.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, height * 0.91f, 0),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.02f,
                BottomRadius = height * 0.11f,
                Height = height * 0.24f,
                RadialSegments = 8
            },
            MaterialOverride = foliageMaterial
        });

        body.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, height * 0.28f, 0),
            Shape = new CylinderShape3D { Radius = 0.22f, Height = height * 0.56f }
        });
        root.AddChild(body);
    }

    public static void AddUtilityPole(Node3D root, Vector3 origin)
    {
        var material = PainterlyMaterialLibrary.ForColor("493d32", "wood");
        var body = new StaticBody3D { Name = "UtilityPole", Position = origin };
        body.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 2.9f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.16f, Height = 5.8f, RadialSegments = 7 },
            MaterialOverride = material
        });
        body.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, 5.2f, 0),
            Mesh = new BoxMesh { Size = new Vector3(1.55f, 0.12f, 0.12f) },
            MaterialOverride = material
        });
        body.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 2.9f, 0),
            Shape = new CylinderShape3D { Radius = 0.16f, Height = 5.8f }
        });
        root.AddChild(body);
    }

    public static void AddBirch(Node3D root, Vector3 origin, float height)
    {
        var body = new StaticBody3D { Name = "Birch", Position = origin };
        body.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, height * 0.42f, 0),
            Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.14f, Height = height * 0.84f, RadialSegments = 7 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("9a988a", "wood")
        });

        var foliage = PainterlyMaterialLibrary.ForColor("354b36", "foliage");
        foreach (var offset in new[]
                 {
                     new Vector3(-0.42f, height * 0.72f, 0.08f),
                     new Vector3(0.36f, height * 0.79f, -0.16f),
                     new Vector3(0.02f, height * 0.91f, 0.1f)
                 })
        {
            body.AddChild(new MeshInstance3D
            {
                Position = offset,
                Scale = new Vector3(1.15f, 0.82f, 1.0f),
                Mesh = new SphereMesh { Radius = height * 0.17f, Height = height * 0.34f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = foliage
            });
        }

        body.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, height * 0.42f, 0),
            Shape = new CylinderShape3D { Radius = 0.15f, Height = height * 0.84f }
        });
        root.AddChild(body);
    }

    public static void AddCable(Node3D root, Vector3 start, Vector3 end)
    {
        var direction = end - start;
        var cable = new MeshInstance3D
        {
            Name = "UtilityCable",
            Position = (start + end) * 0.5f,
            Mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = direction.Length(), RadialSegments = 6 },
            MaterialOverride = PainterlyMaterialLibrary.ForColor("202522")
        };
        root.AddChild(cable);
        cable.LookAt(end, Vector3.Up);
        cable.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
    }

    public static void AddGrassTuft(Node3D root, Vector3 origin, float height)
    {
        var tuft = new Node3D { Name = "GrassTuft", Position = origin };
        var material = PainterlyMaterialLibrary.ForColor("59634a", "foliage");
        for (var index = 0; index < 7; index++)
        {
            var bladeHeight = height * (0.72f + index % 3 * 0.14f);
            tuft.AddChild(new MeshInstance3D
            {
                Position = new Vector3((index - 3) * 0.028f, bladeHeight * 0.5f, (index % 3 - 1) * 0.035f),
                RotationDegrees = new Vector3(index % 2 == 0 ? 8 : -7, index * 37, index % 3 - 1),
                Mesh = new BoxMesh { Size = new Vector3(0.022f, bladeHeight, 0.045f) },
                MaterialOverride = material
            });
        }

        root.AddChild(tuft);
    }

    public static void AddShrub(Node3D root, Vector3 origin, float size, string color = "3f503a")
    {
        var shrub = new Node3D { Name = "Shrub", Position = origin };
        var material = PainterlyMaterialLibrary.ForColor(color, "foliage");
        foreach (var offset in new[]
                 {
                     new Vector3(-0.28f, 0.32f, 0.04f),
                     new Vector3(0.24f, 0.38f, -0.12f),
                     new Vector3(0.02f, 0.52f, 0.16f)
                 })
        {
            shrub.AddChild(new MeshInstance3D
            {
                Position = offset * size,
                Scale = new Vector3(size * 0.48f, size * 0.38f, size * 0.44f),
                Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 4 },
                MaterialOverride = material
            });
        }

        root.AddChild(shrub);
    }
}
