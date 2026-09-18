using Godot;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private void CheckMosqueShelfBooks()
    {
        var instances = new HashSet<ulong>();
        var space = _player.GetWorld3D().DirectSpaceState;
        using var ray = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Down, 2);
        for (var shelf = 0; shelf < 3; shelf++)
        for (var index = 0; index < 8; index++)
        {
            var book = _mosque.GetNode<MeshInstance3D>($"MosqueBook{index}_{shelf}");
            Check(book.Mesh is ArrayMesh && book.IsVisibleInTree() && book.MaterialOverride is null,
                "mosque shelf book has its actual visible multi-material binding " + book.Name);
            var mesh = (ArrayMesh)book.Mesh;
            instances.Add(mesh.GetInstanceId());
            Check(mesh.GetSurfaceCount() == 3 && Enumerable.Range(0, 3).All(i => mesh.SurfaceGetMaterial(i) is not null),
                "binding, paper block and fine seams all have a material");
            var surfaces = Enumerable.Range(0, 3).Select(i => mesh.SurfaceGetArrays(i)).ToArray();
            var vertices = surfaces.SelectMany(a => a[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
            var bounds = new Aabb(vertices[0], Vector3.Zero);
            foreach (var vertex in vertices) bounds = bounds.Expand(vertex);
            var height = .31f + index % 3 * .025f;
            var shelfBody = _mosque.GetNode<StaticBody3D>($"MosqueBookShelf{shelf}Body");
            var shelfContact = shelfBody.GetNode<CollisionShape3D>("Contact");
            var shelfTop = shelfContact.GlobalTransform * (Vector3.Up * ((BoxShape3D)shelfContact.Shape).Size.Y * .5f);
            var bottom = book.ToGlobal(new(0, bounds.Position.Y, 0));
            var expected = new Vector3(-4.63f + index * .15f, .1275f + shelf * .49f, 3.63f);
            Check(book.Position.DistanceTo(expected) < .000002f
                && Math.Abs(bounds.Position.X + .0325f) < .000002f
                && Math.Abs(bounds.Size.X - .065f) < .000002f
                && Math.Abs(bounds.Position.Y) < .000002f && Math.Abs(bounds.End.Y - height) < .000002f
                && bounds.Position.Z >= -.110002f && bounds.End.Z <= .110002f
                && Math.Abs(bottom.Y - shelfTop.Y) < .00002f,
                "all cover vertices retain the authored book envelope and rest on the real shelf top");
            var cloth = surfaces[0][(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var paper = surfaces[1][(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Check(cloth.Any(v => Math.Abs(v.X) < .00001f && v.Z < -.10999f)
                && paper.All(v => Math.Abs(v.X) < .029f && v.Y > .003f && v.Y < height - .003f && v.Z > -.098f),
                "a curved forward spine surrounds a genuinely recessed cream page block");
            var soundTriangles = true;
            foreach (var arrays in surfaces)
            {
                var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var n = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                soundTriangles &= v.Length % 3 == 0 && n.Length == v.Length && uv.Length == v.Length;
                if (n.Length != v.Length) continue;
                for (var t = 0; t + 2 < v.Length; t += 3)
                {
                    var cross = (v[t + 1] - v[t]).Cross(v[t + 2] - v[t]);
                    soundTriangles &= cross.LengthSquared() > 1e-18f && cross.Dot(n[t]) < 0;
                }
            }
            Check(soundTriangles, "book surfaces have real nonzero clockwise triangles, normals and UVs");
            var supports = new List<string>();
            foreach (var x in new[] { -.0305f, .0305f })
            foreach (var z in new[] { -.08f, .08f })
            {
                var sole = book.ToGlobal(new(x, 0, z));
                ray.From = sole + Vector3.Up * .012f; ray.To = sole - Vector3.Up * .025f;
                var hit = space.IntersectRay(ray);
                Check(hit.Count > 0 && hit["collider"].AsGodotObject() == shelfBody
                    && Math.Abs(hit["position"].AsVector3().Y - sole.Y) < .00002f,
                    "both cover boards stand on the authored shelf collision, without floating or burial");
                supports.Add(((Node)hit["collider"].AsGodotObject()).GetPath().ToString());
            }
            _events.Add(new { kind = "mosque-book-support", book = book.GetPath().ToString(),
                bounds = bounds.ToString(), position = book.Position.ToString(), shelfTop = shelfTop.ToString(),
                supportGap = bottom.Y - shelfTop.Y, triangles = vertices.Length / 3,
                surfaces = Enumerable.Range(0, 3).Select(mesh.SurfaceGetName).ToArray(), supports,
                limit = "actual geometry and shelf contact; visual judgement uses the ordinary 09d bookshelf frame; no authored sacred text" });
        }
        Check(instances.Count == 6, "twenty-four books reuse six mesh variants without duplicating shared resources");
    }
}
