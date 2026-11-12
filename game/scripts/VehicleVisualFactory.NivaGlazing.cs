using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    private static ArrayMesh? _nivaBodyPresentationMesh;

    // The source joins windows and parked wipers into NivaBody. Preserve its
    // painted shell and closed doors, replacing only those presentation parts.
    private static ArrayMesh NivaBodyPresentationMesh(MeshInstance3D source)
    {
        if (_nivaBodyPresentationMesh is { } cached && GodotObject.IsInstanceValid(cached)) return cached;
        var mesh = new ArrayMesh();
        var corners = 0;
        for (var surface = 0; surface < source.Mesh.GetSurfaceCount(); surface++)
        {
            var material = source.Mesh.SurfaceGetMaterial(surface);
            var finish = material?.ResourceName ?? string.Empty;
            if (finish == "83988c__glass") continue;
            using var arrays = source.Mesh.SurfaceGetArrays(surface);
            if (finish is "1b1d1b__plastic" or "141614__rubber")
            {
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (indices.Length == 0) indices = Enumerable.Range(0, vertices.Length).ToArray();
                var kept = new List<int>(indices.Length);
                bool BakedFrontWiper(int vertex)
                {
                    var p = source.Transform * vertices[vertex];
                    // Bounds of NivaWiper_L/R Arm/Blade in generate_niva.py.
                    // Cowl/seals are below 1.14; the rear wiper is outside Z.
                    return p.X > -.54f && p.X < .66f && p.Y > 1.14f && p.Y < 1.23f
                        && p.Z > -.72f && p.Z < -.64f;
                }
                for (var i = 0; i < indices.Length; i += 3)
                {
                    if (BakedFrontWiper(indices[i]) && BakedFrontWiper(indices[i + 1])
                        && BakedFrontWiper(indices[i + 2])) continue;
                    kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]);
                }
                if (kept.Count == 0) continue;
                arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();
            }
            var indexArray = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            corners += indexArray.Length > 0 ? indexArray.Length : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            if (material is not null) mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }
        mesh.SetMeta("expectedTriangleCorners", corners);
        mesh.SetMeta("manualTriangleCorners", corners);
        mesh.SetMeta("replacementScope", "window glass and baked front wipers only");
        return _nivaBodyPresentationMesh = mesh;
    }

    private static void BuildNivaGlazing(Node3D root)
    {
        var glass = (StandardMaterial3D)GlassFinish("83988c").Duplicate();
        glass.AlbedoColor = new Color(.48f, .57f, .53f, .14f);
        glass.SetMeta("vehicleFinish", "glass");
        var seals = new Batch(root, "NivaWindowSeals");
        void Pane(string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // One optical surface, not the six faces of a transparent box.
            // Edges overlap their rubber gasket so no daylight slit remains.
            using var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            var normal = (b - a).Cross(d - a).Normalized();
            foreach (var point in new[] { a, c, b, a, d, c })
            {
                tool.SetNormal(normal);
                tool.SetUV(Vector2.Zero);
                tool.AddVertex(point);
            }
            var mesh = tool.Commit();
            mesh.SetMeta("expectedTriangleCorners", 6);
            mesh.SetMeta("manualTriangleCorners", 6);
            var pane = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = glass,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            pane.SetMeta("assetSource", "procedural fitted glazing");
            root.AddChild(pane);
            seals.Beam(a, b, .010f, "1f2320", "rubber");
            seals.Beam(b, c, .010f, "1f2320", "rubber");
            seals.Beam(c, d, .010f, "1f2320", "rubber");
            seals.Beam(d, a, .010f, "1f2320", "rubber");
        }
        // Same plane as VehicleNivaCabin's snow/wiper frame, extended into the
        // A-pillar seals. No change to the driver's sightline or camera.
        Pane("NivaGlass_Windshield", new(-.766f, 1.12f, -.69f), new(.766f, 1.12f, -.69f),
            new(.728f, 1.63f, -.31f), new(-.728f, 1.63f, -.31f));
        foreach (var side in new[] { -1f, 1f })
        {
            Vector3 P(float z, float y) => new(side * (.788f - (y - 1.12f) / .51f * .045f), y, z);
            Pane("NivaGlass_Door" + (side < 0 ? "Left" : "Right"), P(-.685f, 1.112f), P(.483f, 1.112f),
                P(.483f, 1.641f), P(-.293f, 1.641f));
            Pane("NivaGlass_Quarter" + (side < 0 ? "Left" : "Right"), P(.527f, 1.112f), P(1.35f, 1.112f),
                P(1.31f, 1.641f), P(.527f, 1.641f));
        }
        Pane("NivaGlass_Hatch", new(-.743f, 1.121f, 1.816f), new(.743f, 1.121f, 1.816f),
            new(.697f, 1.636f, 1.541f), new(-.697f, 1.636f, 1.541f));
        seals.Finish();
    }
}
