using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private void BuildAuthoredWellContacts()
    {
        foreach (var placement in FindDescendants<Node3D>(this)
            .Where(node => HasTrueMeta(node, "authoredKitBlockerCandidate") && node.IsVisibleInTree()).ToArray())
        {
            var meshes = FindDescendants<MeshInstance3D>(placement).Where(mesh => mesh.Mesh is not null
                && mesh.IsVisibleInTree() && mesh.Name.ToString().StartsWith("Well_YardLandmark_", StringComparison.Ordinal)
                && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)).ToArray();
            if (meshes.Length == 0) continue;
            var contact = NewKitBlockerProxy();
            placement.AddChild(contact);
            foreach (var mesh in meshes) contact.AddChild(AuthoredSurfaceContact(placement, mesh));
            placement.SetMeta("wellContactPolicy", "exact authored rim, posts, roof and mechanism; grounded instance transform");
            placement.SetMeta("wellContactMemberCount", meshes.Length);
        }
    }

    /// <summary>
    /// A rigid house keeps a level threshold. Its plinth closes the remaining
    /// slope to the actual terrain instead of lowering the whole house, its
    /// entrance target, people and furniture to the deepest corner.
    /// </summary>
    private void GroundAuthoredBuildingSupports()
    {
        var count = 0;
        var maximumDrop = 0f;
        foreach (var placement in FindDescendants<Node3D>(this)
            .Where(node => HasTrueMeta(node, "authoredKitBlockerCandidate") && node.IsVisibleInTree()).ToArray())
        foreach (var footing in FindDescendants<MeshInstance3D>(placement).Where(mesh => mesh.Mesh is not null
            && mesh.IsVisibleInTree() && mesh.Name.ToString().EndsWith("_LOD0", StringComparison.Ordinal)
            && (mesh.Name.ToString().Contains("Foundation", StringComparison.Ordinal)
                || mesh.Name.ToString().Contains("Footing", StringComparison.Ordinal)
                || HasTrueMeta(mesh, "requiresTerrainSupport"))
            && !mesh.Name.ToString().Contains("Cap", StringComparison.Ordinal)).ToArray())
        {
            if (footing.HasMeta("foundationTerrainSupport")) continue;
            var (frame, bounds) = AuthoredKitMeshBounds(footing);
            var top = bounds.Position.Y + .012f;
            var min = bounds.Position;
            var max = bounds.End;
            var corners = new[] { new Vector2(min.X, min.Z), new Vector2(max.X, min.Z),
                new Vector2(max.X, max.Z), new Vector2(min.X, max.Z) };
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            var spans = 0;
            for (var edge = 0; edge < corners.Length; edge++)
            {
                var a = corners[edge];
                var b = corners[(edge + 1) % corners.Length];
                var length = a.DistanceTo(b);
                var inward = new Vector2(-(b - a).Y, (b - a).X).Normalized() * .14f;
                var divisions = Math.Max(1, Mathf.CeilToInt(length / .5f));
                for (var index = 0; index < divisions; index++)
                {
                    var first = a.Lerp(b, index / (float)divisions);
                    var last = a.Lerp(b, (index + 1) / (float)divisions);
                    var basePoints = new[] { first, last, last + inward, first + inward };
                    var vertices = new Vector3[8];
                    var drop = 0f;
                    for (var corner = 0; corner < 4; corner++)
                    {
                        var p = basePoints[corner];
                        var world = frame * new Vector3(p.X, top, p.Y);
                        var terrain = AgentBAct1HeightField.CollisionGround(world.X, world.Z) - .025f;
                        var bottom = Math.Min(top - .015f, terrain - frame.Origin.Y);
                        drop = Math.Max(drop, top - bottom);
                        vertices[corner] = new(p.X, bottom, p.Y);
                        vertices[corner + 4] = new(p.X, top, p.Y);
                    }
                    if (drop <= .04f) continue;
                    maximumDrop = Math.Max(maximumDrop, drop);
                    foreach (var triangle in new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7,
                        0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 })
                        surface.AddVertex(vertices[triangle]);
                    spans++;
                }
            }
            if (spans == 0) continue;
            surface.GenerateNormals();
            var support = new MeshInstance3D { Name = $"AuthoredFoundationSupport{count}", Mesh = surface.Commit(),
                Transform = placement.GlobalTransform.AffineInverse() * frame,
                MaterialOverride = PainterlyMaterialLibrary.ForColor("56534c", "stone") };
            support.SetMeta("foundationSourceMesh", footing.GetPath().ToString());
            support.SetMeta("terrainSupportPolicy", "level plinth; terrain-scribed perimeter; threshold unchanged");
            placement.AddChild(support);
            var body = placement.GetNodeOrNull<StaticBody3D>("AuthoredKitCollisionProxy");
            if (body is null) { body = NewKitBlockerProxy(); placement.AddChild(body); }
            body.AddChild(AuthoredSurfaceContact(placement, support));
            footing.SetMeta("foundationTerrainSupport", support.GetPath().ToString());
            count++;
        }
        SetMeta("authoredFoundationSupportCount", count);
        SetMeta("authoredFoundationSupportMaximumDrop", maximumDrop);
        GD.Print($"act1-building-supports: supported foundations={count}; maximum ground drop={maximumDrop:F3}m; level entrances preserved");
    }

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
