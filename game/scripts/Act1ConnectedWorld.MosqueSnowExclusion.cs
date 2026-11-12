using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // These are the occupied shell and annex, not the former broad square
    // exclusion. Terrain_Main already has its paired render/physics cut.
    private static readonly (Vector3 Centre, Vector3 Half)[] MosqueGroundExclusions =
    [
        (new(-.7f, 0, 0), new(6.20f, 12f, 4.85f)),
        (new(-7.97f, 0, 2f), new(1.19f, 12f, 1.89f))
    ];

    private void RegisterMosqueOccupiedSnowVolumes()
    {
        if (_mosqueRoom is not { } room) return;
        ClipSnowInside(room, new(-.7f, 1.75f, 0), new(6.20f, 2.05f, 4.85f));
        ClipSnowInside(room, new(-7.97f, 1.45f, 2f), new(1.19f, 1.75f, 1.89f));
        ClipSnowInside(room, MinaretCentre + Vector3.Up * (MinaretGalleryHeight * .5f),
            new(1.30f, MinaretGalleryHeight * .5f, 1.30f));
        ClipSnowInside(room.GetNode<Node3D>("MosqueCarvedGardenEntrance"),
            new(6.70f, 1.35f, 0), new(1.40f, 1.50f, 1.70f));
    }

    private void ExcludeMosqueInteriorSnow()
    {
        if (_mosqueRoom is null) return;
        var kit = GetNode<Node3D>("Act1CoreWorldGreybox/AgentBExteriorWorld/AgentB_TerrainRoadKit");
        foreach (var mesh in FindDescendants<MeshInstance3D>(this).ToArray())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is not ArrayMesh) continue;
            var name = mesh.Name.ToString();
            var importedGround = kit.IsAncestorOf(mesh) && name != "Terrain_Main"
                && (name.StartsWith("Grade_", StringComparison.Ordinal)
                    || name.StartsWith("Ditch_", StringComparison.Ordinal)
                    || name.StartsWith("Apron_", StringComparison.Ordinal)
                    || name.StartsWith("Road_", StringComparison.Ordinal));
            if (importedGround || IsSnowRelief(mesh)) ExcludeSnowFromMosqueInterior(mesh);
        }
    }

    private void ExcludeSnowFromMosqueInterior(MeshInstance3D mesh)
    {
        if (_mosqueRoom is null || mesh.Mesh is not ArrayMesh source || source.GetSurfaceCount() == 0) return;
        var toRoom = _mosqueRoom.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
        var bounds = toRoom * source.GetAabb();
        if (!MosqueGroundExclusions.Any(box => new Aabb(box.Centre - box.Half, box.Half * 2).Intersects(bounds))) return;
        var result = new ArrayMesh();
        var overrides = new List<Material?>();
        var changed = false;
        for (var slot = 0; slot < source.GetSurfaceCount(); slot++)
        {
            using var arrays = source.SurfaceGetArrays(slot);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                ? Enumerable.Range(0, vertices.Length).ToArray() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var splitter = SnowSurfaceSplitter.TryCreate(arrays, vertices);
            var kept = new List<int>();
            for (var t = 0; t + 2 < indices.Length; t += 3)
            {
                var i0 = indices[t]; var i1 = indices[t + 1]; var i2 = indices[t + 2];
                var p0 = toRoom * vertices[i0]; var p1 = toRoom * vertices[i1]; var p2 = toRoom * vertices[i2];
                List<Vector3[]> pieces = [[Vector3.Right, Vector3.Up, Vector3.Back]];
                var split = false;
                foreach (var box in MosqueGroundExclusions)
                {
                    var next = new List<Vector3[]>();
                    foreach (var piece in pieces)
                        split |= SnowSurfaceSplitter.SubtractFootprint(piece, p0 - box.Centre, p1 - box.Centre,
                            p2 - box.Centre, box.Half, next);
                    pieces = next;
                }
                if (!split) { kept.AddRange([i0, i1, i2]); continue; }
                changed = true;
                // Ground sheets have static attributes. A future unsupported
                // format loses only intersecting triangles, never the whole sheet.
                if (splitter is null) continue;
                foreach (var piece in pieces)
                for (var k = 1; k + 1 < piece.Length; k++)
                {
                    kept.Add(splitter.Add(i0, i1, i2, piece[0]));
                    kept.Add(splitter.Add(i0, i1, i2, piece[k]));
                    kept.Add(splitter.Add(i0, i1, i2, piece[k + 1]));
                }
            }
            if (kept.Count == 0) continue;
            splitter?.WriteBack(arrays);
            arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
            result.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, source.SurfaceGetMaterial(slot));
            overrides.Add(mesh.GetSurfaceOverrideMaterial(slot));
        }
        if (!changed) { result.Dispose(); return; }
        var faces = result.GetSurfaceCount() == 0 ? Array.Empty<Vector3>() : result.GetFaces();
        foreach (var shape in FindDescendants<CollisionShape3D>(this).Where(shape =>
            shape.Shape is ConcavePolygonShape3D && (mesh.IsAncestorOf(shape)
                || shape.HasMeta("authoredSourceMesh") && shape.GetMeta("authoredSourceMesh").AsString() == mesh.GetPath().ToString())))
        {
            if (faces.Length == 0) { shape.Disabled = true; continue; }
            var toShape = shape.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            var clipped = (ConcavePolygonShape3D)shape.Shape.Duplicate();
            clipped.SetFaces(faces.Select(p => toShape * p).ToArray());
            shape.Shape = clipped;
        }
        if (faces.Length == 0) { mesh.Visible = false; result.Dispose(); }
        else
        {
            mesh.Mesh = result;
            for (var slot = 0; slot < overrides.Count; slot++) mesh.SetSurfaceOverrideMaterial(slot, overrides[slot]);
        }
        mesh.SetMeta("mosqueOccupiedSnowCut", true);
    }

    private void RegisterFacilityPrecipitationShelters()
    {
        var weather = GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        if (_mosqueRoom is { } mosque)
        {
            weather.RegisterWeatherShelter(mosque, new(-.7f, 1.87f, 0), new(6.20f, 1.87f, 4.85f));
            weather.RegisterWeatherShelter(mosque, new(-7.97f, 1.60f, 2f), new(1.19f, 1.60f, 1.89f));
            weather.RegisterWeatherShelter(mosque, MinaretCentre + Vector3.Up * (MinaretGalleryHeight * .5f),
                new(1.30f, MinaretGalleryHeight * .5f, 1.30f));
            weather.RegisterWeatherShelter(mosque.GetNode<Node3D>("MosqueCarvedGardenEntrance"),
                new(6.70f, 1.35f, 0), new(1.40f, 1.35f, 1.70f));
        }
        if (_bathhouse is { } bath) weather.RegisterWeatherShelter(bath, new(0, 1.275f, 0), new(2f, 1.275f, 2.5f));
        if (_policePost is { } police) weather.RegisterWeatherShelter(police, new(0, 1.5f, 0), new(5.8f, 1.5f, 4.3f));
        foreach (var room in _publicBuildings)
        {
            weather.RegisterWeatherShelter(room.Room, new(0, room.Ceiling * .5f, 0),
                new(room.HalfSize.X, room.Ceiling * .5f, room.HalfSize.Y));
            if (room.SeniVolume.Size.LengthSquared() > .01f)
                weather.RegisterWeatherShelter(room.Metric, room.SeniVolume.GetCenter(), room.SeniVolume.Size * .5f);
        }
    }
}
