using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private readonly Dictionary<string, StandardMaterial3D> _mosquePrints = new();

    private StandardMaterial3D MosquePrint(string file, int cell = -1, string folder = "signs_imagegen_v1", int rows = 3)
    {
        var key = folder + "/" + file + "/" + cell + "/" + rows;
        if (_mosquePrints.TryGetValue(key, out var material)) return material;
        var texture = MosqueTexture("res://assets/materials/mosque/" + folder + "/" + file + ".png");
        using var source = texture.GetImage();
        if (source.IsCompressed()) source.Decompress();
        var width = source.GetWidth(); var height = source.GetHeight();
        // Extract cells before mipmapping so neighbouring words cannot bleed
        // onto a sign at a distance. All lettering is in the generated bitmap.
        var region = cell < 0 ? new Rect2I(0, 0, width, height)
            : new Rect2I(cell % 2 * width / 2 + 3, cell / 2 * height / rows + 3,
                width / 2 - 6, (cell / 2 + 1) * height / rows - cell / 2 * height / rows - 6);
        using var print = source.GetRegion(region);
        print.GenerateMipmaps();
        material = new StandardMaterial3D {
            AlbedoTexture = ImageTexture.CreateFromImage(print), Roughness = .92f, MetallicSpecular = .12f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        material.SetMeta("printAspect", (float)region.Size.X / region.Size.Y);
        material.SetMeta("artworkOrigin", "image_gen; author supplied handwritten Cyrillic reference; no runtime typesetting");
        _mosquePrints.Add(key, material);
        return material;
    }

    private Label3D MosquePrintedSign(Node3D parent, string name, string text, Vector3 at, float yaw,
        float width, string file, int cell)
    {
        // Keep the former Label3D node/path for consumers, but do not render
        // font glyphs. Its child carries the actual ImageGen inscription.
        var root = new Label3D { Name = name, Text = string.Empty, Position = at, RotationDegrees = new(0, yaw, 0) };
        parent.AddChild(root);
        root.SetMeta("sourceText", text);
        var material = MosquePrint(file, cell);
        var size = new Vector2(width, width / material.GetMeta("printAspect").AsSingle());
        MosqueWood(root, "Backing", new(size.X + .018f, size.Y + .018f, .016f), new(0, 0, -.01f));
        root.AddChild(new MeshInstance3D { Name = "PrintedFace", Mesh = new QuadMesh { Size = size },
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return root;
    }

    private void ApplyMosqueAddressArtwork(AddressSignVisualComponent plate)
    {
        foreach (var line in plate.GetChildren().OfType<Label3D>()) line.Visible = false;
        var material = MosquePrint("sign-address");
        const float height = .205f;
        plate.AddChild(new MeshInstance3D { Name = "MosquePrintedAddress", Position = new(0, 0, .020f),
            Mesh = new QuadMesh { Size = new(height * material.GetMeta("printAspect").AsSingle(), height) },
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        plate.SetMeta("artworkOrigin", "image_gen; exact existing ADR-MOSQUE street and house number");
    }
}
