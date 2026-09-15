using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    /// <summary>
    /// The zirat kit was authored on a flat preview board. Its remaining small
    /// shoulder clods and grass retained that height; the culvert received only
    /// its root's ground height, leaving the offset stones above the ditch.
    /// Seat those independent solids without deforming the source geometry or
    /// changing the separate usable footbridge and its approaches.
    /// </summary>
    private void GroundZiratRoadsideContacts()
    {
        var presentation = GetNode<Node3D>(
            "Act1CoreWorldGreybox/ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation");
        var count = 0;
        foreach (var name in new[]
        {
            "ZiratCulvertStoneCluster", "ZiratWetRoadShoulderLeft",
            "ZiratWetRoadShoulderRight", "ZiratRoadsideDitch"
        })
        {
            var placement = presentation.GetNode<Node3D>(name);
            foreach (var mesh in FindDescendants<MeshInstance3D>(placement))
            {
                if (!mesh.IsVisibleInTree() || mesh.Mesh is null || mesh.HasMeta("roadsideGrounding")) continue;
                var worldVertices = mesh.Mesh.GetFaces().Select(mesh.ToGlobal).Distinct().ToArray();
                if (worldVertices.Length == 0) throw new InvalidOperationException($"Empty roadside mesh: {mesh.GetPath()}");
                var baseY = worldVertices.Min(point => point.Y);
                var footing = worldVertices.Where(point => point.Y <= baseY + .006f).ToArray();
                // All repaired components have a small, flat lower ring. Include
                // ring-edge midpoints to cover crossings of terrain triangles.
                var support = footing.Concat(footing.SelectMany((a, index) => footing.Skip(index + 1)
                    .Select(b => (a + b) * .5f)));
                var shift = support.Min(point => AgentBAct1HeightField.CollisionGround(point.X, point.Z) - point.Y) - .015f;
                mesh.SetMeta("roadsideSourcePosition", mesh.Position);
                mesh.SetMeta("roadsideGrounding", "rigid-lower-ring-to-terrain-triangles-v1");
                mesh.GlobalPosition += Vector3.Up * shift;
                count++;
            }
        }
        SetMeta("roadsideGroundedContactMeshCount", count);
        GD.Print($"act1-roadside-grounding: independent contact meshes={count}; source meshes and usable footbridge preserved");
    }
}
