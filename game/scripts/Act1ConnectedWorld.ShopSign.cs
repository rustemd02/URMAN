using Godot;

namespace Urman.Godot;

/// <summary>
/// The shop's sign, assembled part by part from a design brief researched for
/// URMAN (ChatGPT project, 2026-09-25): an old village-shop sign of the 1990s
/// that nobody replaced, only repainted. A pine frame 1800 × 520 × 24 mm behind
/// a galvanised sheet painted faded dark green, a cream border, АШАМЛЫКЛАР in
/// large warm-white stencil letters over ПРОДУКТЫ, two almost faded buds at the
/// ends, four rough steel angles on visible bolts, rust rings and runs from the
/// top bolts, chips down to zinc and rust, snow only on the top edge. The right
/// end hangs 10 mm low. A separate newer plate gives the opening hours in
/// Tatar and Russian. Presentation only; the shop's doors, address and
/// interactions are untouched.
/// </summary>
public partial class Act1ConnectedWorld
{
    private void BuildShopSign(Node3D building)
    {
        var fascia = FindDescendants<Node3D>(building).First(node => node.Name == "shopBuildingSign");
        foreach (var child in fascia.GetChildren().OfType<Node3D>().ToArray())
        {
            // The plain painted board and its label give way to the real sign.
            child.SetMeta("suppressionReason", "replaced by the assembled АШАМЛЫКЛАР / ПРОДУКТЫ sign");
            child.Visible = false;
        }
        var sign = new Node3D
        {
            Name = "AshamlyklarSign", Position = new(0, -.14f, 0),
            // The right end sits 10 mm lower than the left.
            RotationDegrees = new(0, 0, -Mathf.RadToDeg(Mathf.Atan2(.010f, 1.8f)))
        };
        fascia.AddChild(sign);
        sign.SetMeta("presentationOnly", true);
        sign.SetMeta("designBrief", "ChatGPT URMAN project 2026-09-25: village shop sign, faded green galvanised sheet on a pine frame");

        MeshInstance3D Part(string name, Vector3 size, Vector3 at, string colour, string surface, float roll = 0f)
        {
            var part = AddVisualBox(sign, name, size, at, colour, surface, 0f, roll);
            part.MaterialOverride = PainterlyMaterialLibrary.ForColor(colour, surface, sheltered: true);
            return part;
        }
        const float faceZ = .049f;
        // Frame and the painted sheet folded over it.
        Part("Frame", new(1.8f, .52f, .024f), new(0, 0, .035f), "55483a", "wood");
        Part("Sheet", new(1.76f, .48f, .004f), new(0, 0, faceZ), "315449", "metal");
        // Cream border strip, 15 mm wide and 25 mm in from the edge.
        foreach (var y in new[] { -.2075f, .2075f })
            Part($"Border{y:0.00}", new(1.695f, .015f, .002f), new(0, y, faceZ + .003f), "c8b98f", "metal");
        foreach (var x in new[] { -.8475f, .8475f })
            Part($"Border{x:0.00}", new(.015f, .43f, .002f), new(x, 0, faceZ + .003f), "c8b98f", "metal");
        // Old stencil lettering: Tatar large, Russian below.
        Label3D Letters(string name, string text, float capHeight, float y)
        {
            const int fontSize = 64;
            var label = new Label3D
            {
                Name = name, Text = text, FontSize = fontSize, PixelSize = capHeight / (.72f * fontSize),
                Position = new(0, y, faceZ + .004f), Modulate = new Color("e3ddc7"), OutlineSize = 0,
                Shaded = true, DoubleSided = false, AlphaCut = Label3D.AlphaCutMode.Discard
            };
            sign.AddChild(label);
            return label;
        }
        Letters("Ashamlyklar", "АШАМЛЫКЛАР", .138f, .07f);
        Letters("Produkty", "ПРОДУКТЫ", .105f, -.12f);
        // Almost faded buds at both ends.
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * .79f;
            Part($"BudPetal{side}", new(.05f, .06f, .001f), new(x, .03f, faceZ + .0025f), "a95645", "metal", 45f);
            Part($"BudLeafL{side}", new(.018f, .05f, .001f), new(x - .025f, -.03f, faceZ + .0025f), "77835e", "metal", 35f);
            Part($"BudLeafR{side}", new(.018f, .05f, .001f), new(x + .025f, -.03f, faceZ + .0025f), "77835e", "metal", -35f);
            Part($"BudStem{side}", new(.006f, .06f, .001f), new(x, -.04f, faceZ + .0025f), "77835e", "metal");
        }
        // Four steel angles to the wall, bolts with washers, rust at every bolt.
        foreach (var x in new[] { -.7f, .7f })
        foreach (var y in new[] { -.27f, .27f })
        {
            Part($"AngleWall{x}_{y}", new(.04f, .003f, .06f), new(x, y, .025f), "494a46", "metal");
            Part($"AngleFace{x}_{y}", new(.04f, .04f, .003f), new(x, y - Math.Sign(y) * .018f, .002f), "494a46", "metal");
        }
        foreach (var x in new[] { -.82f, .82f })
        foreach (var y in new[] { -.21f, .21f })
        {
            var rust = new MeshInstance3D
            {
                Name = $"RustRing{x}_{y}", Mesh = new CylinderMesh { TopRadius = .02f, BottomRadius = .02f, Height = .001f, RadialSegments = 12 },
                Position = new(x, y, faceZ + .0022f), RotationDegrees = new(90, 0, 0),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("713d29", "metal", sheltered: true)
            };
            sign.AddChild(rust);
            var washer = new MeshInstance3D
            {
                Name = $"Washer{x}_{y}", Mesh = new CylinderMesh { TopRadius = .009f, BottomRadius = .009f, Height = .0015f, RadialSegments = 10 },
                Position = new(x, y, faceZ + .003f), RotationDegrees = new(90, 0, 0),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("555650", "metal", sheltered: true)
            };
            sign.AddChild(washer);
            var bolt = new MeshInstance3D
            {
                Name = $"Bolt{x}_{y}", Mesh = new CylinderMesh { TopRadius = .005f, BottomRadius = .006f, Height = .005f, RadialSegments = 6 },
                Position = new(x, y, faceZ + .005f), RotationDegrees = new(90, 0, 0),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("555650", "metal", sheltered: true)
            };
            sign.AddChild(bolt);
            if (y > 0)
            {
                // Rust runs down from the two top bolts.
                var length = x < 0 ? .16f : .09f;
                Part($"RustRun{x}", new(.008f, length, .0008f), new(x + .004f, y - .02f - length * .5f, faceZ + .0021f), "8a5135", "metal");
                Part($"RustRunThin{x}", new(.003f, length * .6f, .0008f), new(x - .008f, y - .02f - length * .3f, faceZ + .0021f), "8a5135", "metal");
            }
        }
        // Chips to zinc and a few old peeled patches, heavier along the edges.
        foreach (var (x, y, w, h, colour) in new[]
                 {
                     (-.62f, .225f, .05f, .012f, "85877f"), (.31f, .228f, .03f, .01f, "85877f"), (.55f, -.226f, .06f, .014f, "85877f"),
                     (-.2f, -.228f, .04f, .01f, "85877f"), (-.86f, -.1f, .012f, .04f, "85877f"), (.4f, -.19f, .08f, .035f, "59372a"),
                     (-.45f, .19f, .05f, .03f, "59372a"), (.86f, .12f, .012f, .05f, "59372a")
                 })
            Part($"Chip{x}_{y}", new(w, h, .0008f), new(x, y, faceZ + .0021f), colour, "metal");
        // Snow only on the top edge, uneven, hanging a little over the front.
        for (var i = 0; i < 9; i++)
        {
            var x = -.8f + i * .2f;
            var height = .012f + .02f * Mathf.Abs(Mathf.Sin(i * 1.7f));
            Part($"Snow{i}", new(.21f, height, .045f), new(x, .26f + height * .5f, .038f), "e4e7e3", "snow_ground");
        }
        // Opening hours: a newer plate screwed on below the right end.
        var plate = new Node3D { Name = "OpeningHoursPlate", Position = new(.74f, -.66f, .012f) };
        sign.AddChild(plate);
        var backing = AddVisualBox(plate, "Plate", new(.3f, .21f, .003f), Vector3.Zero, "ddd8c7", "metal");
        backing.MaterialOverride = PainterlyMaterialLibrary.ForColor("ddd8c7", "metal", sheltered: true);
        plate.AddChild(new Label3D
        {
            Name = "Hours", Text = "ЭШ ВАКЫТЫ\nРЕЖИМ РАБОТЫ\n8:00 – 20:00\nТӨШКЕ ТӘНӘФЕС ЮК\nБЕЗ ПЕРЕРЫВА",
            FontSize = 48, PixelSize = .00042f, Position = new(0, 0, .0025f), Modulate = new Color("242522"),
            OutlineSize = 0, Shaded = true, LineSpacing = 2f, AlphaCut = Label3D.AlphaCutMode.Discard
        });
    }
}
