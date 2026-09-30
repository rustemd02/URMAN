using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string CouncilPosterDocument = "urman.chapter1:document/council-sabantuy-poster";
    private const string CouncilPosterAlbum = "urman.chapter1:document/council-photo-album";

    private void BuildCouncilPosterPresentation()
    {
        if (_runtimeBridge is null) throw new InvalidOperationException("The council poster needs its existing content owner.");
        var room = _publicBuildings.Single(building => building.Id == "council").Room;
        var mount = room.GetNode<Node3D>("council_sabantuy_poster_Source");
        if (mount.HasNode("PrintedPoster")) return;
        var document = _runtimeBridge.RequireDocument(CouncilPosterDocument);
        var album = _runtimeBridge.RequireDocument(CouncilPosterAlbum);
        // The heading and date are consecutive Markdown lines, not separate
        // paragraphs. Only these three face lines precede the reverse-side note.
        var face = CouncilPosterFaceLines(document.BodyMarkdown);
        var albumLines = NonemptyPosterLines(album.BodyMarkdown);
        if (albumLines.Length < 2 || !albumLines[1].StartsWith("**", StringComparison.Ordinal))
            throw new InvalidOperationException("The existing album needs its authored photograph caption.");
        var caption = SourceExcerptSelection.FormatPlainSourceText(albumLines[1]);
        var image = album.Images?.Single()
            ?? throw new InvalidOperationException("The existing council album needs exactly one source photograph.");
        var texture = ResourceLoader.Load<Texture2D>(image.ResourcePath)
            ?? throw new InvalidOperationException("The council photograph could not be loaded: " + image.ResourcePath);
        if (texture.GetHeight() <= 0 || Math.Abs(texture.GetWidth() / (float)texture.GetHeight() - 1.5f) > .001f)
            throw new InvalidOperationException("The council preparation photograph must retain its original 3:2 aspect ratio.");

        var sheet = mount.GetNode<MeshInstance3D>("SourceFace");
        if (sheet.Mesh is not QuadMesh paper || paper.Size.DistanceTo(new(.65f, .86f)) > .0001f)
            throw new InvalidOperationException("The existing poster mount changed size.");
        sheet.MaterialOverride = CivicSurfaceLibrary.Face("quest_papers_v2_atlas.png",2,2,2);
        mount.GetNode<MeshInstance3D>("PaperOrFrame").MaterialOverride =
            PainterlyMaterialLibrary.ForColor("53614e", "wood_furniture", sheltered: true);

        var printed = new Node3D { Name = "PrintedPoster" };
        mount.AddChild(printed);
        printed.SetMeta("documentId", document.Id);
        printed.SetMeta("faceSourcePolicy", "first three nonempty authored lines; reverse remains in the manual reader");
        printed.SetMeta("photographDocumentId", album.Id);
        printed.SetMeta("photographAssetId", image.AssetId);
        printed.SetMeta("photographCaptionSource", "second nonempty album line; preparation, not a performance");

        PosterText(printed, "Heading", face[0], .345f, 82, .00077f, .57f, "304f44");
        PosterText(printed, "Date", face[1], .291f, 30, .00075f, .57f, "555640");
        PublicBox(printed, "PrintedRule", new(.556f, .002f, .0007f), new(0, .257f, .022f), "63735c", "paper");
        PublicBox(printed, "PhotoBorder", new(.574f, .390f, .001f), new(0, -.70f, .022f), "f0e7cf", "paper");
        printed.AddChild(new MeshInstance3D
        {
            Name = "PreparationPhotograph", Position = new(0, -.70f, .024f),
            Mesh = new QuadMesh { Size = new(.552f, .368f) }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = texture, AlbedoColor = Colors.White, Roughness = 1, MetallicSpecular = 0,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                CullMode = BaseMaterial3D.CullModeEnum.Back
            }
        });
        PosterText(printed, "PreparationCaption", caption, -.916f, 22, .00075f, .57f, "555640");
        var programme = PosterText(printed, "Programme", face[2], -.207f, 25, .00075f, .55f, "343e34");
        programme.VerticalAlignment = VerticalAlignment.Top;
        foreach (var label in printed.GetChildren().OfType<Label3D>()) label.Visible = false;
        printed.GetNode<MeshInstance3D>("PrintedRule").Visible = false;
        printed.SetMeta("handmadePrintedFace", "ImageGen source-bound paper; exact text retained in metadata/manual reader");
        // The source photograph has its own mount below the notice; it must not
        // cover the generated heading or the programme on the printed paper.
        foreach (var x in new[] { -.287f, .287f })
            PublicBox(printed,"PhotoSide"+x,new(.018f,.406f,.025f),new(x,-.70f,.016f),"53614e","wood_furniture");
        foreach (var y in new[] { -.895f, -.505f })
            PublicBox(printed,"PhotoEnd"+y,new(.592f,.018f,.025f),new(0,y,.016f),"53614e","wood_furniture");

        // The original mounting plane and source target stay fixed. These narrow
        // rails retain the paper, and four shafts cross the actual wall plane.
        foreach (var x in new[] { -.335f, .335f })
            PublicBox(printed, "FrameSide" + (x < 0 ? "Left" : "Right"), new(.018f, .887f, .025f), new(x, 0, .016f), "53614e", "wood_furniture");
        foreach (var y in new[] { -.435f, .435f })
            PublicBox(printed, "FrameEnd" + (y < 0 ? "Bottom" : "Top"), new(.688f, .018f, .025f), new(0, y, .016f), "53614e", "wood_furniture");
        var screw = 0;
        foreach (var x in new[] { -.334f, .334f }) foreach (var y in new[] { -.431f, .431f })
        {
            var fixing = new Node3D { Name = "WallFixing" + screw++, Position = new(x, y, 0) };
            printed.AddChild(fixing);
            var shaft = DiscoveryCylinder(fixing, "Shaft", .002f, .002f, .13f, new(0, 0, -.033f), "767c70");
            shaft.RotationDegrees = new(90, 0, 0);
            var head = DiscoveryCylinder(fixing, "Head", .006f, .006f, .003f, new(0, 0, .033f), "8a9081");
            head.RotationDegrees = new(90, 0, 0);
            PublicBox(fixing, "Slot", new(.007f, .0012f, .0008f), new(0, 0, .0347f), "414a40", "metal");
        }

        // Retire only this instance's generic placeholder after the binding has
        // succeeded. The same manual target still opens the complete document.
        mount.GetNode<Label3D>("DocumentTitle").Visible = false;
        for (var row = 0; row < 5; row++) if (mount.GetNodeOrNull<MeshInstance3D>("WrittenLine" + row) is { } line) line.Visible = false;
        mount.SetMeta("printedPosterBound", true);
    }

    internal static string[] CouncilPosterFaceLines(string markdown)
    {
        var lines = NonemptyPosterLines(markdown);
        if (lines.Length < 4 || !lines[0].StartsWith("# ", StringComparison.Ordinal)
            || !lines[1].StartsWith("**", StringComparison.Ordinal) || lines[2].StartsWith("#", StringComparison.Ordinal)
            || lines[2].StartsWith("- ", StringComparison.Ordinal))
            throw new InvalidOperationException("The poster must keep its authored heading, date and face paragraph before its reverse.");
        return lines.Take(3).Select(SourceExcerptSelection.FormatPlainSourceText).ToArray();
    }

    private static string[] NonemptyPosterLines(string markdown) => Regex.Split(markdown, @"\r?\n")
        .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private static Label3D PosterText(Node3D parent, string name, string text, float y, int fontSize,
        float pixelSize, float width, string color)
    {
        var label = new Label3D
        {
            Name = name, Text = text, Position = new(0, y, .026f), FontSize = fontSize, PixelSize = pixelSize,
            Width = width / pixelSize, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            OutlineSize = 0, Modulate = Color.FromHtml(color), Shaded = true, DoubleSided = false,
            NoDepthTest = false
        };
        parent.AddChild(label);
        return label;
    }
}
