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
/// end hangs 10 mm low. The painted face is a flat ImageGen unwrap of exactly
/// that brief on one quad; frame, angles, bolts and snow stay geometry. A separate newer plate gives the opening hours in
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
        Part("Sheet", new(1.76f, .46f, .004f), new(0, 0, faceZ), "315449", "metal");
        // The painted face is one textured sheet, "origami" from a flat
        // ImageGen unwrap (game/assets/textures/act1/README.md): border,
        // stencil letters, buds, bolt holes, rust runs and chips are all in
        // the paint. The UV window crops the image's white margins.
        var face = new MeshInstance3D
        {
            Name = "PaintedFace", Mesh = new QuadMesh { Size = new(1.76f, .46f) }, Position = new(0, 0, faceZ + .0021f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/act1/shop_sign_ashamlyklar_v1.png"),
                Uv1Scale = new(.991f, .742f, 1f), Uv1Offset = new(.004f, .131f, 0f),
                Roughness = .82f, Metallic = 0f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            }
        };
        sign.AddChild(face);
        // Washers and bolts in the four painted bolt holes.
        foreach (var (x, y) in new[] { (-.847f, .175f), (.813f, .175f), (-.847f, -.186f), (.815f, -.186f) })
        {
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
        }
        // Four steel angles to the wall.
        foreach (var x in new[] { -.7f, .7f })
        foreach (var y in new[] { -.27f, .27f })
        {
            Part($"AngleWall{x}_{y}", new(.04f, .003f, .06f), new(x, y, .025f), "494a46", "metal");
            Part($"AngleFace{x}_{y}", new(.04f, .04f, .003f), new(x, y - Math.Sign(y) * .018f, .002f), "494a46", "metal");
        }
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
