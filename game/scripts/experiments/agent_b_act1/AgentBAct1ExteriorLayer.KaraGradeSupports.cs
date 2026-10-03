using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class AgentBAct1ExteriorLayer
{
    // These forest-edge snow grades are walkable beside the final approach.
    // Other Grade_* meshes cross authored buildings or timber paths; fitting
    // them needs a separate geometry pass, not a blanket collision rule.
    private void BuildKaraGradeSupports()
    {
        var grades = EnumerateDescendants<MeshInstance3D>(GetNode<Node3D>("AgentB_TerrainRoadKit"))
            .Where(mesh => mesh.Name.ToString().StartsWith("Grade_Kara_", StringComparison.Ordinal))
            .ToArray();
        if (grades.Length != 2)
            throw new InvalidOperationException($"Expected two Kara snow grades; found {grades.Length}.");

        foreach (var grade in grades)
        {
            if (grade.Mesh is not ArrayMesh original)
                throw new InvalidOperationException($"Kara snow grade has no authored mesh: {grade.Name}.");
            var bounds = (GlobalTransform.AffineInverse() * grade.GlobalTransform) * original.GetAabb();
            var fitted = new ArrayMesh();
            for (var surfaceIndex = 0; surfaceIndex < original.GetSurfaceCount(); surfaceIndex++)
            {
                using var sourceArrays = original.SurfaceGetArrays(surfaceIndex);
        using var arrays = sourceArrays.Duplicate(true);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                for (var index = 0; index < vertices.Length; index++)
                {
                    var point = ToLocal(grade.ToGlobal(vertices[index]));
                    var ground = AgentBAct1HeightField.CollisionGround(point.X, point.Z);
                    point.Y += ground - (float)AgentBAct1HeightField.Ground(point.X, point.Z);
                    var endDistance = Math.Min(point.Z - bounds.Position.Z, bounds.End.Z - point.Z);
                    point.Y = Mathf.Lerp(ground + .005f, point.Y, Mathf.SmoothStep(0, 1, endDistance / 3f));
                    vertices[index] = grade.ToLocal(ToGlobal(point));
                }
                arrays[(int)Mesh.ArrayType.Vertex] = vertices;
                var reshaped = new ArrayMesh();
                reshaped.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                var surface = new SurfaceTool();
                surface.CreateFrom(reshaped, 0);
                surface.GenerateNormals();
                surface.Commit(fitted);
                fitted.SurfaceSetMaterial(surfaceIndex, original.SurfaceGetMaterial(surfaceIndex));
            }
            grade.Mesh = fitted;
            var support = new StaticBody3D { Name = "SurfaceSupport", CollisionLayer = 1u, CollisionMask = 1u };
            grade.AddChild(support);
            support.SetMeta("collisionOwner", grade.GetPath().ToString());
            var contact = new CollisionShape3D { Name = $"{grade.Name}_Contact", Shape = fitted.CreateTrimeshShape() };
            contact.SetMeta("authoredSourceMesh", grade.GetPath().ToString());
            support.AddChild(contact);
            grade.SetMeta("supportOwner", support.GetPath().ToString());
        }
    }
}
