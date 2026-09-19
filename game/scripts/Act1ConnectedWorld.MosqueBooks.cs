using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly Dictionary<(Vector3 Size, string Cover), ArrayMesh> _mosqueBookMeshes = new();

    // Existing shelf contacts own support. These closed, blank cloth bindings
    // add no interaction, text, collision body or persistent state.
    private MeshInstance3D AddMosqueShelfBook(Node3D parent, string name, Vector3 size,
        Vector3 shelfAt, string coverColor)
    {
        var key = (size, coverColor);
        if (!_mosqueBookMeshes.TryGetValue(key, out var mesh))
        {
            mesh = BuildMosqueBookMesh(size, coverColor);
            _mosqueBookMeshes.Add(key, mesh);
        }
        var book = new MeshInstance3D { Name = name, Mesh = mesh, Position = shelfAt };
        book.SetMeta("geometryOwner", nameof(Act1ConnectedWorld));
        book.SetMeta("presentationRole", "closed cloth binding, rounded spine and recessed page block; no invented text");
        parent.AddChild(book);
        return book;
    }

    private static ArrayMesh BuildMosqueBookMesh(Vector3 size, string coverColor)
    {
        const float board = .004f, corner = .003f, spineBow = .010f;
        var half = size.X * .5f;
        var back = size.Z * .5f;
        var front = -back + spineBow;
        var cover = new MosqueBookSurface();
        var paper = new MosqueBookSurface();
        var detail = new MosqueBookSurface();

        // The two cover boards have actual thickness and clipped, softened
        // fore-edge corners. Their bottom faces establish the shelf plane.
        var outline = new[] { new Vector2(0, front + corner), new(corner, front),
            new(size.Y - corner, front), new(size.Y, front + corner),
            new(size.Y, back - corner), new(size.Y - corner, back),
            new(corner, back), new(0, back - corner) };
        foreach (var sign in new[] { -1f, 1f })
        {
            var x0 = sign < 0 ? -half : half - board;
            var x1 = x0 + board;
            var center0 = new Vector3(x0, size.Y * .5f, (front + back) * .5f);
            var center1 = center0 with { X = x1 };
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var b = outline[(i + 1) % outline.Length];
                var p = new Vector3(x0, a.X, a.Y); var q = new Vector3(x0, b.X, b.Y);
                var r = q with { X = x1 }; var s = p with { X = x1 };
                cover.Quad(p, q, r, s, (p + q) * .5f - center0);
                cover.Triangle(center0, q, p, Vector3.Left);
                cover.Triangle(center1, s, r, Vector3.Right);
            }
        }

        // A curved cloth strip joins the boards; its inside, end caps and
        // attachment faces are closed as well as the visible outer spine.
        const int spineSegments = 12;
        for (var i = 0; i < spineSegments; i++)
        {
            Vector3 Arc(int n, bool inner, float y)
            {
                var angle = Mathf.Pi - n * Mathf.Pi / spineSegments;
                return new((half - (inner ? board : 0)) * Mathf.Cos(angle), y,
                    front + (inner ? .002f : 0) - (inner ? .006f : spineBow) * Mathf.Sin(angle));
            }
            var a = Arc(i, false, 0); var b = Arc(i + 1, false, 0);
            var c = Arc(i, true, 0); var d = Arc(i + 1, true, 0);
            var up = Vector3.Up * size.Y;
            var seamHeights = new[] { 0f, .024f, .0255f, size.Y - .026f, size.Y - .0245f, size.Y };
            for (var strip = 0; strip < seamHeights.Length - 1; strip++)
            {
                // Adjacent surface strips have no depth-offset overlay, so
                // seams stay inside the authored envelope without z-fighting.
                var face = strip is 1 or 3 ? detail : cover;
                face.Quad(a + Vector3.Up * seamHeights[strip], b + Vector3.Up * seamHeights[strip],
                    b + Vector3.Up * seamHeights[strip + 1], a + Vector3.Up * seamHeights[strip + 1],
                    new((a.X + b.X) * .5f, 0, -spineBow));
            }
            cover.Quad(c, c + up, d + up, d, new(-(c.X + d.X) * .5f, 0, spineBow));
            cover.Quad(a, c, d, b, Vector3.Down);
            cover.Quad(a + up, b + up, d + up, c + up, Vector3.Up);
            if (i == 0) cover.Quad(a, a + up, c + up, c, Vector3.Left);
            if (i == spineSegments - 1) cover.Quad(b, d, d + up, b + up, Vector3.Right);
        }

        var paperMin = new Vector3(-half + board + .0003f, board, front + .003f);
        var paperMax = new Vector3(half - board - .0003f, size.Y - board, back - .004f);
        paper.Box(paperMin, paperMax);
        // Page leaves lie in YZ and stack across X. Fine signatures continue
        // across the top, fore-edge and bottom, rather than crossing the spine.
        for (var i = 1; i < 10; i++)
        {
            var x = Mathf.Lerp(paperMin.X, paperMax.X, i / 10f);
            const float line = .00022f, lift = .00012f;
            detail.Quad(new(x, paperMax.Y + lift, paperMin.Z), new(x + line, paperMax.Y + lift, paperMin.Z),
                new(x + line, paperMax.Y + lift, paperMax.Z), new(x, paperMax.Y + lift, paperMax.Z), Vector3.Up);
            detail.Quad(new(x, paperMin.Y, paperMax.Z + lift), new(x + line, paperMin.Y, paperMax.Z + lift),
                new(x + line, paperMax.Y, paperMax.Z + lift), new(x, paperMax.Y, paperMax.Z + lift), Vector3.Back);
            detail.Quad(new(x, paperMin.Y - lift, paperMin.Z), new(x, paperMin.Y - lift, paperMax.Z),
                new(x + line, paperMin.Y - lift, paperMax.Z), new(x + line, paperMin.Y - lift, paperMin.Z), Vector3.Down);
        }
        var mesh = new ArrayMesh { ResourceName = $"MosqueBook_{size.Y}_{coverColor}" };
        cover.Commit(mesh, "ClothBoardsAndSpine", PainterlyMaterialLibrary.ForColor(coverColor, "cloth", sheltered: true));
        paper.Commit(mesh, "RecessedPaperBlock", PainterlyMaterialLibrary.ForColor("d6ccb3", "paper", sheltered: true));
        detail.Commit(mesh, "BindingAndPageSeams", PainterlyMaterialLibrary.ForColor("a69779", "paper", sheltered: true));
        return mesh;
    }

    private sealed class MosqueBookSurface
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uv = new();

        internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var cross = (b - a).Cross(c - a);
            if (cross.Dot(outward) < 0) { (b, c) = (c, b); cross = -cross; }
            var normal = cross.Normalized();
            // Godot front faces are clockwise when viewed from outside.
            foreach (var point in new[] { a, c, b })
            {
                _vertices.Add(point); _normals.Add(normal);
                _uv.Add(Math.Abs(normal.X) > .5f ? new(point.Z, point.Y)
                    : Math.Abs(normal.Y) > .5f ? new(point.X, point.Z) : new(point.X, point.Y));
            }
        }

        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        { Triangle(a, b, c, outward); Triangle(a, c, d, outward); }

        internal void Box(Vector3 a, Vector3 b)
        {
            var p = new[] { new Vector3(a.X,a.Y,a.Z), new(b.X,a.Y,a.Z), new(b.X,b.Y,a.Z), new(a.X,b.Y,a.Z),
                new(a.X,a.Y,b.Z), new(b.X,a.Y,b.Z), new(b.X,b.Y,b.Z), new(a.X,b.Y,b.Z) };
            Quad(p[0],p[3],p[2],p[1],Vector3.Forward); Quad(p[4],p[5],p[6],p[7],Vector3.Back);
            Quad(p[0],p[4],p[7],p[3],Vector3.Left); Quad(p[1],p[2],p[6],p[5],Vector3.Right);
            Quad(p[0],p[1],p[5],p[4],Vector3.Down); Quad(p[3],p[7],p[6],p[2],Vector3.Up);
        }

        internal void Commit(ArrayMesh mesh, string name, Material material)
        {
            var arrays = new global::Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uv.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            var index = mesh.GetSurfaceCount() - 1;
            mesh.SurfaceSetName(index, name); mesh.SurfaceSetMaterial(index, material);
        }
    }
}
