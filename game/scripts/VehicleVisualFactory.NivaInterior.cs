using Godot;

namespace Urman.Godot;

public static partial class VehicleVisualFactory
{
    private static void BuildNivaInterior(Node3D root)
    {
        var cabin = new Batch(root, "NivaInterior", interiorFinishes: true);
        // Continuous padded dash, with the live instruments and imported radio
        // inset into it. The old GLB interior is never added to this vehicle.
        cabin.RoundedBox(new(1.52f, .22f, .31f), new(0, .992f, -.556f), .035f, "262826", "plastic");
        cabin.RoundedBox(new(.46f, .205f, .15f), new(-.44f, 1.085f, -.481f), .028f, "262826", "plastic");
        cabin.RoundedBox(new(.405f, .151f, .006f), new(-.44f, 1.092f, -.402f), .012f, "151b18", "plastic");
        cabin.RoundedBox(new(.30f, .30f, .075f), new(.10f, .929f, -.412f), .018f, "262826", "plastic");
        for (var i = 0; i < 3; i++)
        {
            cabin.Box(new(.16f, .006f, .003f), new(.10f, .91f - i * .025f, -.373f), "141614", "plastic");
            cabin.RoundedBox(new(.018f, .013f, .013f), new(.06f + i * .04f, .91f - i * .025f, -.367f),
                .003f, "8f948c", "metal");
        }
        foreach (var x in new[] { -.69f, .30f, .67f })
        {
            cabin.RoundedBox(new(.115f, .048f, .009f), new(x, 1.034f, -.397f), .008f, "141614", "plastic");
            for (var slat = 0; slat < 3; slat++)
                cabin.Box(new(.097f, .003f, .009f), new(x, 1.022f + slat * .012f, -.390f), "383c36", "plastic");
        }
        // Glovebox is a narrow recessed seam in the same fascia, not a second slab.
        cabin.Beam(new(.325f, .955f, -.399f), new(.665f, .955f, -.399f), .0015f, "141614", "plastic");
        cabin.RoundedBox(new(.045f, .012f, .009f), new(.495f, .971f, -.397f), .003f, "1b1d1b", "plastic");
        cabin.Beam(new(.36f, 1.083f, -.413f), new(.38f, 1.119f, -.393f), .009f, "1b1d1b", "plastic");
        cabin.Beam(new(.38f, 1.119f, -.393f), new(.62f, 1.119f, -.393f), .009f, "1b1d1b", "plastic");
        cabin.Beam(new(.62f, 1.119f, -.393f), new(.64f, 1.083f, -.413f), .009f, "1b1d1b", "plastic");
        // Column ends at the wheel pivot. Keeping both on the same axis avoids
        // a floating rim; all controller-owned pivots retain their old coordinates.
        var hub = new Vector3(-.40f, 1.10f, -.16f);
        var columnAxis = Basis.FromEuler(new(Mathf.DegToRad(NivaSteeringTiltDegrees), 0, 0)) * Vector3.Back;
        cabin.Beam(hub - columnAxis * .32f, hub, .035f, "1b1d1b", "plastic");
        cabin.Beam(hub - columnAxis * .11f, hub - columnAxis * .11f + new Vector3(-.14f, 0, 0),
            .008f, "1b1d1b", "plastic");
        cabin.Beam(new(-.58f, .80f, -.60f), new(-.58f, .64f, -.53f), .012f, "2e332e", "metal");
        cabin.RoundedBox(new(.07f, .075f, .014f), new(-.58f, .62f, -.52f), .005f, "141614", "rubber", new(-35, 0, 0));
        cabin.RoundedBox(new(.29f, .13f, 1.28f), new(0, .558f, .02f), .05f, "33312d", "carpet");
        cabin.Cylinder(.045f, .025f, new(.015f, .635f, -.19f), "141614", "rubber");
        cabin.Beam(new(.015f, .635f, -.19f), new(.02f, .855f, -.14f), .012f, "2e332e", "metal");
        cabin.Sphere(new(.025f, .028f, .025f), new(.02f, .855f, -.14f), "1b1d1b", "plastic");
        foreach (var x in new[] { -.07f, .075f })
        {
            cabin.Cylinder(.025f, .02f, new(x, .632f, .02f), "141614", "rubber");
            cabin.Beam(new(x, .64f, .02f), new(x, .76f, .06f), .009f, "2e332e", "metal");
            cabin.Sphere(new(.018f, .022f, .018f), new(x, .76f, .06f), "1b1d1b", "plastic");
        }
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * .40f;
            NivaFrontSeat(cabin, x, side < 0);
            cabin.RoundedBox(new(.46f, .012f, .65f), new(x, .506f, -.35f), .005f, "141614", "rubber");
            cabin.RoundedBox(new(.46f, .012f, .37f), new(x, .506f, .77f), .005f, "141614", "rubber");
            cabin.RoundedBox(new(.025f, .49f, 1.07f), new(side * .757f, .835f, -.07f), .01f, "3a3833", "vinyl");
            cabin.RoundedBox(new(.032f, .20f, .70f), new(side * .743f, .86f, -.10f), .012f, "5a5046", "cloth");
            cabin.RoundedBox(new(.066f, .055f, .31f), new(side * .727f, .925f, .07f), .02f, "302f2b", "vinyl");
            cabin.RoundedBox(new(.026f, .10f, .50f), new(side * .737f, .645f, -.08f), .008f, "302f2b", "vinyl");
            cabin.Beam(new(side * .729f, .86f, -.33f), new(side * .722f, .81f, -.38f), .008f, "8f948c", "metal");
            cabin.Sphere(new(.013f, .013f, .013f), new(side * .719f, .81f, -.38f), "1b1d1b", "plastic");
            cabin.RoundedBox(new(.016f, .025f, .10f), new(side * .731f, 1.003f, -.42f), .007f, "8f948c", "metal");
            cabin.RoundedBox(new(.025f, .39f, 1.17f), new(side * .766f, .90f, 1.10f), .008f, "3a3833", "vinyl");
            cabin.Beam(new(side * .739f, 1.52f, .50f), new(side * .67f, .72f, .42f), .005f, "2a2b28", "cloth");
        }
        cabin.RoundedBox(new(1.26f, .13f, .45f), new(0, .69f, 1.13f), .04f, "302f2b", "vinyl");
        cabin.RoundedBox(new(1.26f, .46f, .11f), new(0, .96f, 1.42f), .035f, "302f2b", "vinyl", new(10, 0, 0));
        cabin.RoundedBox(new(1.03f, .014f, .35f), new(0, .756f, 1.12f), .006f, "5a5046", "cloth");
        cabin.RoundedBox(new(.42f, .065f, .30f), new(.30f, .793f, 1.12f), .025f, "7b3a2d", "wool");
        cabin.RoundedBox(new(1.46f, .012f, 1.84f), new(0, 1.636f, .615f), .005f, "c9c3b3", "headliner");
        foreach (var x in new[] { -.40f, .40f })
            cabin.RoundedBox(new(.30f, .015f, .10f), new(x, 1.619f, -.27f), .006f, "bfb9a8", "vinyl");
        cabin.Beam(new(0, 1.63f, -.33f), new(0, 1.60f, -.345f), .012f, "1b1d1b", "plastic");
        cabin.RoundedBox(new(.17f, .05f, .022f), new(0, 1.578f, -.355f), .01f, "1b1d1b", "plastic", new(8, 0, 0));
        cabin.Box(new(.151f, .033f, .002f), new(0, 1.578f, -.342f), "9aa8a2", "glass", new(8, 0, 0));
        cabin.RoundedBox(new(.12f, .021f, .07f), new(0, 1.622f, .40f), .008f, "d8d4c4", "plastic");
        cabin.Finish();
        root.GetNode<MeshInstance3D>("NivaInterior").SetMeta("assetSource", "procedural fitted cabin");
    }

    private static void NivaFrontSeat(Batch cabin, float x, bool driver)
    {
        // Existing driver hip: (-.40,.80,.19). Cushion top .74 (+.025 cover),
        // lower back's front .32: space for the pelvis and coat behind that hip.
        foreach (var rail in new[] { -.17f, .17f })
            cabin.RoundedBox(new(.03f, .03f, .60f), new(x + rail, .515f, .08f), .006f, "2e332e", "metal");
        cabin.RoundedBox(new(.43f, .085f, .45f), new(x, .573f, .12f), .01f, "2e332e", "metal");
        cabin.RoundedBox(new(.50f, .13f, .52f), new(x, .675f, .12f), .04f, "302f2b", "vinyl");
        var backRotation = new Vector3(12, 0, 0);
        cabin.RoundedBox(new(.50f, .57f, .105f), new(x, 1.02f, .43f), .035f, "302f2b", "vinyl", backRotation);
        var backBasis = Basis.FromEuler(backRotation * Mathf.Pi / 180f);
        var finish = driver ? "sheepskin" : "cloth";
        var color = driver ? "9a8562" : "5a5046";
        cabin.RoundedBox(new(.38f, driver ? .026f : .012f, .43f), new(x, driver ? .747f : .739f, .11f),
            .005f, color, finish);
        cabin.RoundedBox(new(.36f, .45f, driver ? .026f : .012f),
            new Vector3(x, 1.02f, .43f) + backBasis * new Vector3(0, 0, -.052f), .005f, color, finish, backRotation);
        foreach (var post in new[] { -.075f, .075f })
            cabin.Beam(new(x + post, 1.28f, .485f), new(x + post, 1.36f, .502f), .009f, "8f948c", "metal");
        cabin.RoundedBox(new(.28f, .16f, .085f), new(x, 1.405f, .512f), .029f, "302f2b", "vinyl", backRotation);
    }

    private sealed partial class Batch
    {
        public void RoundedBox(Vector3 size, Vector3 pos, float radius, string color, string surface, Vector3 rotation = default)
        {
            using var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            var half = size * .5f;
            radius = Mathf.Min(radius, Mathf.Min(half.X, Mathf.Min(half.Y, half.Z)) * .95f);
            var core = half - Vector3.One * radius;
            float Axis(float h, int i) => i switch { 0 => -h, 1 => -h + radius * .293f,
                2 => -h + radius, 3 => h - radius, 4 => h - radius * .293f, _ => h };
            void Vertex(Vector3 point, Vector2 uv)
            {
                var closest = new Vector3(Mathf.Clamp(point.X, -core.X, core.X),
                    Mathf.Clamp(point.Y, -core.Y, core.Y), Mathf.Clamp(point.Z, -core.Z, core.Z));
                var normal = (point - closest).Normalized();
                tool.SetNormal(normal);
                tool.SetUV(uv);
                tool.AddVertex(closest + normal * radius);
            }
            for (var axis = 0; axis < 3; axis++) foreach (var side in new[] { -1f, 1f })
            {
                var u = (axis + 1) % 3; var v = (axis + 2) % 3;
                Vector3 Point(int i, int j)
                {
                    var p = Vector3.Zero; p[axis] = side * half[axis];
                    p[u] = Axis(half[u], i); p[v] = Axis(half[v], j); return p;
                }
                for (var i = 0; i < 5; i++) for (var j = 0; j < 5; j++)
                {
                    var a = Point(i, j); var b = Point(i + 1, j);
                    var c = Point(i + 1, j + 1); var d = Point(i, j + 1);
                    // Godot's outward face winding is clockwise.
                    foreach (var p in side > 0 ? new[] { a, c, b, a, d, c } : new[] { a, b, c, a, c, d })
                        Vertex(p, new(p[u] / size[u] + .5f, p[v] / size[v] + .5f));
                }
            }
            using var mesh = tool.Commit();
            Add(mesh, pos, rotation, color, surface);
        }
    }
}
