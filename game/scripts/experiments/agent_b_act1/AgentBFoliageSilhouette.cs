using Godot;

namespace Urman.Godot;

/// <summary>
/// VIS-027 / VIS-030 measurement primitives for planted vegetation.
///
/// Everything here is a pure read of runtime geometry and material parameters:
/// no visibility, transform, material or state write happens in this class.
/// The numbers the LOD silhouette-parity gate and the culling-volume check are
/// judged by are measured from the meshes the world actually instantiates
/// (after the rebase to the rooted pivot and after the region material pass),
/// not from authored estimates, because the cards require the bounds and the
/// distances to be "подтверждены", not asserted.
///
/// Why the crown is measured in bands: the three tiers of one plant share one
/// rooted mesh origin and one instance basis, so any silhouette difference at
/// the switch can only come from the tier geometry itself. Comparing whole
/// bounding boxes is not enough — a tier can keep its box while losing crown
/// width, which is exactly the "the tree shrinks when I cross the boundary"
/// defect VIS-027 is about.
/// </summary>
internal static class AgentBFoliageSilhouette
{
    /// <summary>Fraction of the plant height above which the crown band is measured.</summary>
    internal const float CrownBandStart = .45f;

    /// <summary>Fraction of the plant height below which the base/root band is measured.</summary>
    internal const float BaseBandEnd = .12f;

    /// <summary>
    /// Non-wind vertex-stage offsets the painterly shader applies (snow relief up
    /// to <c>0.004 * snow_relief_scale</c> along the normal, plus the trample
    /// pressed_height). Small but real, so the culling reserve keeps a fixed
    /// centimetre allowance instead of an arbitrary one.
    /// </summary>
    internal const float VertexReliefMargin = .05f;

    /// <summary>
    /// One measured silhouette. Widths are the larger horizontal extent of the
    /// band: transform-independent for a fixed instance basis, therefore
    /// comparable between the tiers of the same plant.
    /// </summary>
    internal readonly record struct Sample(
        float Height,
        float Width,
        float CrownWidth,
        float BaseWidth,
        float TopLocalY,
        float SwayMarginX,
        float SwayMarginZ,
        int Surfaces,
        int AlphaClippedSurfaces,
        int Vertices,
        int Triangles)
    {
        public bool IsUsable => Surfaces > 0 && Height > .01f;
    }

    /// <summary>Measures a rooted mesh surface by surface, vertex by vertex.</summary>
    internal static Sample Measure(ArrayMesh? mesh)
    {
        if (mesh is null || mesh.GetSurfaceCount() == 0)
        {
            return default;
        }

        var minX = float.MaxValue; var maxX = float.MinValue;
        var minY = float.MaxValue; var maxY = float.MinValue;
        var minZ = float.MaxValue; var maxZ = float.MinValue;
        var vertices = 0;
        var triangles = 0;
        var alphaSurfaces = 0;
        var surfaceCount = mesh.GetSurfaceCount();
        var vertexSets = new List<Vector3[]>(surfaceCount);
        var surfaceSway = new float[surfaceCount];
        for (var surface = 0; surface < surfaceCount; surface++)
        {
            using var arrays = mesh.SurfaceGetArrays(surface);
            var points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            vertexSets.Add(points);
            if (points.Length == 0)
            {
                continue;
            }

            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            vertices += points.Length;
            triangles += indices.Length > 0 ? indices.Length / 3 : points.Length / 3;
            if (mesh.SurfaceGetMaterial(surface) is ShaderMaterial shader)
            {
                // A shader parameter is a Variant: its Godot type is
                // Variant.VariantType, not System.Type, and Godot's float variant
                // is a double — there is no Variant.Type.Float64.
                if (shader.GetShaderParameter("cutout_texture").VariantType != Variant.Type.Nil) alphaSurfaces++;
                var wind = shader.GetShaderParameter("wind_sway");
                if (wind.VariantType == Variant.Type.Float) surfaceSway[surface] = wind.AsSingle();
            }
        }

        foreach (var points in vertexSets)
        {
            foreach (var point in points)
            {
                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
                if (point.Z < minZ) minZ = point.Z;
                if (point.Z > maxZ) maxZ = point.Z;
            }
        }

        if (maxX < minX || maxY < minY || maxZ < minZ)
        {
            return default;
        }

        var height = maxY - minY;
        var crownStart = minY + height * CrownBandStart;
        var baseEnd = minY + height * BaseBandEnd;
        var crownMinX = float.MaxValue; var crownMaxX = float.MinValue;
        var crownMinZ = float.MaxValue; var crownMaxZ = float.MinValue;
        var baseMinX = float.MaxValue; var baseMaxX = float.MinValue;
        var baseMinZ = float.MaxValue; var baseMaxZ = float.MinValue;
        for (var surface = 0; surface < surfaceCount; surface++)
        foreach (var point in vertexSets[surface])
        {
            if (point.Y >= crownStart)
            {
                if (point.X < crownMinX) crownMinX = point.X;
                if (point.X > crownMaxX) crownMaxX = point.X;
                if (point.Z < crownMinZ) crownMinZ = point.Z;
                if (point.Z > crownMaxZ) crownMaxZ = point.Z;
            }

            if (point.Y <= baseEnd)
            {
                if (point.X < baseMinX) baseMinX = point.X;
                if (point.X > baseMaxX) baseMaxX = point.X;
                if (point.Z < baseMinZ) baseMinZ = point.Z;
                if (point.Z > baseMaxZ) baseMaxZ = point.Z;
            }
        }

        // The wind branch of the painterly shader displaces the vertex before the
        // instance transform is applied: VERTEX.x += gust * wind_sway *
        // max(VERTEX.y, 0) with gust in [-1, 1] (0.65 + 0.35) and VERTEX.z at 0.6
        // of that. VERTEX.y is the rooted local height, so the envelope below is
        // exact in mesh space; the instance basis multiplies it again in world.
        var sway = 0f;
        for (var surface = 0; surface < surfaceCount; surface++)
            sway = Mathf.Max(sway, surfaceSway[surface]);
        var reach = Mathf.Max(sway * Mathf.Max(maxY, 0f) + VertexReliefMargin, VertexReliefMargin);

        return new Sample(
            height,
            Mathf.Max(maxX - minX, maxZ - minZ),
            crownMaxX > crownMinX ? Mathf.Max(crownMaxX - crownMinX, crownMaxZ - crownMinZ) : 0f,
            baseMaxX > baseMinX ? Mathf.Max(baseMaxX - baseMinX, baseMaxZ - baseMinZ) : 0f,
            Mathf.Max(maxY, 0f),
            reach,
            reach * .6f,
            surfaceCount,
            alphaSurfaces,
            vertices,
            triangles);
    }

    /// <summary>
    /// Percentage difference between two tiers measured under the same rooted
    /// transform. The first argument is the tier visible before the switch (the
    /// nearer, heavier one), so a positive value is what the player sees as shrink
    /// or swell at the boundary.
    /// </summary>
    internal static float DeltaPercent(float nearTier, float farTier)
    {
        if (nearTier <= .0001f) return farTier <= .0001f ? 0f : 100f;
        return 100f * Mathf.Abs(farTier - nearTier) / nearTier;
    }

    /// <summary>
    /// World-space horizontal sway reserve of one instance: the mesh-local envelope
    /// multiplied by that instance's own scale, because the vertex shader runs
    /// before the model transform.
    /// </summary>
    internal static Vector3 SwayExtent(in Sample sample, Vector3 instanceScale)
    {
        if (!sample.IsUsable) return Vector3.Zero;
        return new Vector3(
            sample.SwayMarginX * Mathf.Abs(instanceScale.X),
            0f,
            sample.SwayMarginZ * Mathf.Abs(instanceScale.Z));
    }
}
