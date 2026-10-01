using Godot;

namespace Urman.Godot;

/// <summary>
/// Snow relief (banks, drifts, aprons, trodden paths) is laid by many owners before the
/// buildings, bridges and porches around it settle. Once the world is complete, every
/// triangle of such relief that lies inside a solid box — a wall, foundation, porch,
/// bridge deck — is dropped, so no drift shows through a house or as a sheet across a deck.
/// Snow caps resting on props are not relief and stay untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private bool _snowReliefClipped;

    private void ClipSnowReliefUnderStructures()
    {
        if (_snowReliefClipped) return;
        _snowReliefClipped = true;
        var boxes = new List<(Transform3D Inverse, Vector3 Half, Aabb Bounds)>();
        foreach (var shape in FindDescendants<CollisionShape3D>(this))
        {
            if (shape.Disabled || shape.Shape is not BoxShape3D box || !shape.IsInsideTree()) continue;
            if (shape.GetParent() is not StaticBody3D body || body.CollisionLayer == 0) continue;
            // Solid architecture only: at least a metre across both ways (walls, plinths,
            // porches, decks); rails, posts and fence boards may stand in a drift.
            if (Mathf.Min(box.Size.X, box.Size.Z) < 1f) continue;
            var transform = shape.GlobalTransform;
            boxes.Add((transform.AffineInverse(), box.Size * .5f, transform * new Aabb(-box.Size * .5f, box.Size)));
        }
        int meshes = 0, dropped = 0;
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (mesh.Mesh is not ArrayMesh source || !IsSnowRelief(mesh)) continue;
            var bounds = mesh.GlobalTransform * source.GetAabb();
            var near = boxes.Where(box => box.Bounds.Grow(.6f).Intersects(bounds)).ToArray();
            if (near.Length == 0) continue;
            bool Inside(Vector3 point) => near.Any(box =>
            {
                var local = box.Inverse * point;
                // Up to 60 cm below a deck or slab still counts: snow does not lie under a
                // footbridge or porch, it would only show through the gaps.
                return Mathf.Abs(local.X) < box.Half.X && Mathf.Abs(local.Z) < box.Half.Z
                    && local.Y < box.Half.Y + .05f && local.Y > -box.Half.Y - .6f;
            });
            var result = new ArrayMesh();
            var changed = false;
            for (var s = 0; s < source.GetSurfaceCount(); s++)
            {
                var arrays = source.SurfaceGetArrays(s);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                    ? Enumerable.Range(0, vertices.Length).ToArray()
                    : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var kept = new List<int>(indices.Length);
                for (var t = 0; t + 2 < indices.Length; t += 3)
                {
                    var centre = mesh.ToGlobal((vertices[indices[t]] + vertices[indices[t + 1]] + vertices[indices[t + 2]]) / 3f);
                    if (Inside(centre)) { dropped++; changed = true; continue; }
                    kept.AddRange([indices[t], indices[t + 1], indices[t + 2]]);
                }
                if (kept.Count == 0) continue;
                arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
                result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, source.SurfaceGetMaterial(s));
            }
            if (!changed) continue;
            mesh.Mesh = result;
            mesh.SetMeta("snowClippedUnderStructures", true);
            meshes++;
        }
        GD.Print($"act1-snow-relief: clipped meshes={meshes} triangles={dropped} solids={boxes.Count}");
    }

    private static bool IsSnowRelief(MeshInstance3D mesh)
    {
        if (!mesh.IsVisibleInTree()) return false;
        var name = mesh.Name.ToString();
        if (name is "Terrain_Main" || name.StartsWith("Backdrop", StringComparison.Ordinal)) return false;
        var material = mesh.MaterialOverride ?? mesh.GetActiveMaterial(0);
        var surface = material?.GetMeta("surface", "").AsString() ?? "";
        if (surface is not ("snow_ground" or "snow_trampled")) return false;
        return mesh.HasMeta("snowBankHeight") || mesh.HasMeta("terrainRole")
            || mesh.GetParent()?.Name.ToString() == "URMAN_AgentB_TerrainRoadKit";
    }
}
