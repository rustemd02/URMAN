using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Ordinary shelf stock is scenery. The existing counter, purchased SKUs
    // and atomic household ledger remain the sole owners of shop actions.
    private static void PublicGoods(Node3D parent, Vector3 at, bool compact, int stock = 0)
    {
        var goods = new Node3D { Name = "Stock_" + parent.GetChildCount(), Position = at };
        parent.AddChild(goods);
        if (compact)
        {
            PublicBox(goods, "BatteryBlisterCard", new(.10f, .018f, .15f), new(-.12f, .01f, 0), "8e473a", "paper");
            foreach (var x in new[] { -.145f, -.098f })
            {
                var battery = DiscoveryCylinder(goods, "AABattery" + x, .014f, .014f, .050f, new(x, .032f, 0), "5a5e59");
                battery.RotationDegrees = new(90, 0, 0);
                PublicBox(goods, "BatteryCap" + x, new(.022f, .022f, .005f), new(x, .032f, .025f), "bbbeb3", "metal");
            }
            PublicBox(goods, "MatchboxDrawer", new(.052f, .020f, .036f), new(.015f, .011f, -.03f), "bb9b65", "wood");
            PublicBox(goods, "MatchboxSleeve", new(.055f, .021f, .037f), new(.015f, .012f, .017f), "9b5442", "paper");
            return;
        }

        switch (stock)
        {
            case 1:
            case 2:
                var flour = stock == 1;
                var width = flour ? .155f : .13f;
                var height = flour ? .27f : .205f;
                var depth = flour ? .105f : .09f;
                ShopFoldedBag(goods, width, height, depth, flour ? "c2b18c" : "c9d0bf");
                PublicBox(goods, "PrintedBand", new(width + .002f, height * .38f, .003f),
                    new(0, height * .47f, -depth * .5f - .001f), flour ? "866148" : "648797", "paper");
                ShopPackageLabel(goods, flour ? "ОН\nМУКА" : "ТОЗ\nСОЛЬ", new(0, height * .48f, -depth * .5f - .003f),
                    .00068f, "eee3ca");
                break;
            case 3:
                DiscoveryCylinder(goods, "PreserveTin", .055f, .055f, .135f, new(0, .0675f, 0), "a6aba0");
                foreach (var y in new[] { .006f, .129f })
                    DiscoveryCylinder(goods, "RolledRim" + y, .058f, .058f, .009f, new(0, y, 0), "b7bbaf");
                DiscoveryCylinder(goods, "PrintedTinWrap", .0558f, .0558f, .091f, new(0, .068f, 0), "99594a");
                ShopPackageLabel(goods, "БОРЧАК\nГОРОШЕК", new(0, .068f, -.0565f), .00043f, "eee0bc");
                break;
            case 4:
                DiscoveryCylinder(goods, "OilBottle", .045f, .042f, .18f, new(0, .09f, 0), "a2934c");
                DiscoveryCylinder(goods, "BottleShoulder", .017f, .045f, .044f, new(0, .202f, 0), "b2a264");
                DiscoveryCylinder(goods, "BottleNeck", .017f, .017f, .047f, new(0, .2475f, 0), "b2a264");
                DiscoveryCylinder(goods, "ScrewCap", .020f, .020f, .025f, new(0, .2685f, 0), "c5bc79");
                // Follow the bottle's tapered wall with a thin printed sleeve.
                DiscoveryCylinder(goods, "BottlePaperLabel", .04473f, .04340f, .08f, new(0, .106f, 0), "ddd2a7");
                ShopPackageLabel(goods, "МАЙ\nМАСЛО", new(0, .107f, -.0448f), .00055f, "5b623e");
                break;
            case 5:
                foreach (var y in new[] { .023f, .070f })
                {
                    PublicBox(goods, "WrappedSoap" + y, new(.13f, .044f, .077f), new(0, y, 0), "a9bec0", "paper");
                    PublicBox(goods, "PaperFold" + y, new(.119f, .004f, .067f), new(0, y + .022f, 0), "ccd4c7", "paper");
                }
                ShopPackageLabel(goods, "САБЫН", new(0, .070f, -.0395f), .0006f, "486a6c");
                break;
            default:
                var cereal = stock == 6;
                var size = cereal ? new Vector3(.135f, .235f, .085f) : new Vector3(.115f, .17f, .075f);
                PublicBox(goods, cereal ? "GrainCarton" : "TeaCarton", size, Vector3.Up * size.Y * .5f,
                    cereal ? "aa8b49" : "42684e", "paper");
                PublicBox(goods, "FoldedCartonLid", new(size.X - .003f, .004f, size.Z - .003f),
                    new(0, size.Y - .001f, 0), cereal ? "c1a46b" : "638064", "paper");
                PublicBox(goods, "CartonLabel", new(size.X * .81f, size.Y * .46f, .003f),
                    new(0, size.Y * .55f, -size.Z * .5f - .001f), "c2b88a", "paper");
                ShopPackageLabel(goods, cereal ? "ЯРМА\nКРУПА" : "ЧӘЙ\nЧАЙ",
                    new(0, size.Y * .55f, -size.Z * .5f - .003f), .00068f, "3e503a");
                break;
        }
        goods.SetMeta("sceneryStock", stock);
    }

    private static void ShopFoldedBag(Node3D parent, float width, float height, float depth, string color)
    {
        // A filled paper bag narrows to its folded seam rather than ending in
        // the flat top of a carton. All rings share vertices along their faces.
        var rings = new[] { new Vector3(width * .90f, 0, depth * .88f),
            new Vector3(width, height * .22f, depth), new Vector3(width * .96f, height * .78f, depth * .92f),
            new Vector3(width * .78f, height - .012f, depth * .20f) };
        using var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Corner(Vector3 ring, int i) => new((i is 0 or 3 ? -.5f : .5f) * ring.X,
            ring.Y, (i < 2 ? -.5f : .5f) * ring.Z);
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // Godot's clockwise front faces and GenerateNormals share this order.
            tool.AddVertex(a); tool.AddVertex(c); tool.AddVertex(b);
            tool.AddVertex(a); tool.AddVertex(d); tool.AddVertex(c);
        }
        for (var level = 0; level < rings.Length - 1; level++)
        for (var side = 0; side < 4; side++)
        {
            var next = (side + 1) % 4;
            Face(Corner(rings[level], side), Corner(rings[level + 1], side),
                Corner(rings[level + 1], next), Corner(rings[level], next));
        }
        Face(Corner(rings[0], 0), Corner(rings[0], 1), Corner(rings[0], 2), Corner(rings[0], 3));
        Face(Corner(rings[^1], 3), Corner(rings[^1], 2), Corner(rings[^1], 1), Corner(rings[^1], 0));
        tool.GenerateNormals();
        parent.AddChild(new MeshInstance3D { Name = "FoldedPaperBag", Mesh = tool.Commit(),
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "paper", sheltered: true) });
        PublicBox(parent, "FoldedPaperSeam", new(width * .80f, .013f, depth * .20f),
            new(0, height - .0065f, 0), color, "paper");
    }

    private static void ShopPackageLabel(Node3D parent, string text, Vector3 at, float pixel, string ink)
    {
        parent.AddChild(new Label3D { Name = "PackagePrint", Text = text, Position = at,
            RotationDegrees = new(0, 180, 0), FontSize = 22, PixelSize = pixel,
            OutlineSize = 0, Modulate = Color.FromHtml(ink), Shaded = true, DoubleSided = false });
    }
}
