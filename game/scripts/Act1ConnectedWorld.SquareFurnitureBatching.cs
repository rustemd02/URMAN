using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private static readonly Dictionary<(string Family, ulong Material), ArrayMesh> SquareFurnitureMeshes = new();

    // Called only after the immutable Chair/Desk builders. Each material batch
    // remains within one piece of furniture; identical pieces reuse resources.
    private static void BatchSquareFurniture(Node3D furniture, string family)
    {
        var sources = furniture.GetChildren().OfType<MeshInstance3D>().ToArray();
        foreach (var group in sources.GroupBy(mesh => mesh.MaterialOverride))
        {
            var members = group.ToArray();
            if (members.Length < 2 || group.Key is not StandardMaterial3D material
                || material.Uv1Triplanar || material.UV2Triplanar || material.NextPass is not null
                || members.Any(m => m.Mesh is null || m.Mesh.GetSurfaceCount() != 1
                    || m.GetChildCount() != 0 || m.MaterialOverlay is not null
                    || m.GetSurfaceOverrideMaterial(0) is not null
                    || m.CastShadow != members[0].CastShadow || !m.Visible)) continue;
            var key = (family, material.GetRid().Id);
            if (!SquareFurnitureMeshes.TryGetValue(key, out var combined))
            {
                using var surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var member in members) surface.AppendFrom(member.Mesh, 0, member.Transform);
                combined = surface.Commit();
                SquareFurnitureMeshes[key] = combined;
            }
            furniture.AddChild(new MeshInstance3D
            {
                Name = "FixedMaterialBatch" + furniture.GetChildCount(), Mesh = combined,
                MaterialOverride = material, CastShadow = members[0].CastShadow,
                Layers = members[0].Layers, GIMode = members[0].GIMode
            });
            foreach (var member in members)
            {
                member.Visible = false;
                member.SetMeta("presentationOwnership", "same immutable geometry in furniture-local material batch");
            }
        }
    }
}
