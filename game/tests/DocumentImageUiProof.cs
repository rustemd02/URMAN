using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Called only after the containing test has reached and opened a real source.
/// These checks do not seed knowledge, override access or stand in for a player.
/// </summary>
internal static class DocumentImageUiProof
{
    public static async Task VerifyArchiveAsync(Node owner, RuntimeBridge bridge, string documentId, string tag)
    {
        var pc = (OldPcUi)owner.GetTree().GetFirstNodeInGroup("old_pc_ui");
        Require(pc.ActiveDocumentId == documentId && bridge.IsOldPcDocumentAccessible(documentId),
            "Image proof requires the actual unlocked archive reader.");
        var expected = bridge.RequireDocument(documentId).Images?.Single()
            ?? throw new InvalidOperationException("Opened source has no declared image: " + documentId);
        await VerifyReaderAsync(owner, pc.GetNode<DocumentImageReader>(
            "Screen/Computer/Layout/WorkArea/ReaderArea/DocumentImages"), expected, tag + "_archive");
        pc.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(owner, 3);
        await VerifyPaperAndJournalAsync(owner, bridge, documentId, tag);
        pc.Open(bridge);
        await Frames(owner, 2);
        // Restore the same reader through the real archive selection handler.
        var list = pc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        var index = Enumerable.Range(0, list.ItemCount).Single(i =>
            list.GetItemMetadata(i).AsString() == documentId);
        list.Select(index);
        list.EmitSignal(ItemList.SignalName.ItemSelected, index);
        for (var frame = 0; frame < 180 && pc.ActiveDocumentId != documentId; frame++) await Frames(owner, 1);
        Require(pc.ActiveDocumentId == documentId, "Image proof could not restore archive selection.");
    }

    public static async Task VerifyPaperAndJournalAsync(Node owner, RuntimeBridge bridge, string documentId, string tag)
    {
        Require(bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
            .EnumerateArray().Any(item => item.GetString() == documentId),
            "Image proof cannot invent discovery of a source.");
        var expected = bridge.RequireDocument(documentId).Images?.Single()
            ?? throw new InvalidOperationException("Found source has no declared image: " + documentId);
        var paper = (DocumentUi)owner.GetTree().GetFirstNodeInGroup("document_ui");
        bridge.OpenDocumentUi(documentId);
        await Frames(owner, 3);
        Require(paper.IsOpen && paper.OpenDocumentId == documentId, "Paper reader did not open the known source.");
        await VerifyReaderAsync(owner, paper.GetNode<DocumentImageReader>(
            "Screen/Document/Layout/Reader/DocumentImages"), expected, tag + "_paper");
        paper.GetNode<Button>("Screen/Document/Layout/Footer/Save").EmitSignal(BaseButton.SignalName.Pressed);
        for (var frame = 0; frame < 180 && !bridge.JournalEntries().Any(item => item.EntryId == documentId); frame++)
            await Frames(owner, 1);
        Require(bridge.JournalEntries().Count(item => item.EntryId == documentId) == 1,
            "Saving the known image failed or duplicated its journal entry.");
        paper.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(owner, 3);
        var journal = (JournalUi)owner.GetTree().GetFirstNodeInGroup("journal_ui");
        journal.Open(bridge, documentId);
        await Frames(owner, 3);
        Require(journal.ActiveEntryId == documentId, "Journal selected a different source.");
        await VerifyReaderAsync(owner, journal.GetNode<DocumentImageReader>(
            "Screen/Book/Layout/WorkArea/Reader/DocumentImages"), expected, tag + "_journal");
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(owner, 3);
    }

    private static async Task VerifyReaderAsync(Node owner, DocumentImageReader reader, DocumentImageContent expected, string tag)
    {
        var player = (FirstPersonController)owner.GetTree().GetFirstNodeInGroup("player_controller");
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            // Headless cannot resize or render the window, so pixel layout is
            // not measurable here (as in SourceExcerptUiProof). Still prove the
            // reader holds the declared, actually imported source image.
            Require(reader.IsVisibleInTree() && reader.ImageCount == 1 && reader.ActiveAssetId == expected.AssetId,
                "Reader dropped or substituted the source image: " + tag);
            reader.GetNode<Button>("Tabs/Image").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(owner, 4);
            Require(reader.ShowingImage && reader.DisplayedTexture is { } headlessTexture && headlessTexture.GetWidth() > 100,
                "Reader has no actual imported texture: " + tag);
            GD.Print($"document-image-ui: {tag} source image present; external/not-run 720p/1080p layout requires a windowed display");
            return;
        }
        var window = DisplayServer.WindowGetSize();
        var preferences = player.Accessibility;
        try
        {
            foreach (var sample in new[] { (1280, 720, 1.6), (1920, 1080, 1.0) })
            {
                DisplayServer.WindowSetSize(new Vector2I(sample.Item1, sample.Item2));
                AccessibilityPresentation.ApplyToTree(owner.GetTree(), preferences with { TextScale = sample.Item3 });
                await Frames(owner, 5);
                Require(reader.IsVisibleInTree() && reader.ImageCount == 1 && reader.ActiveAssetId == expected.AssetId,
                    "Reader dropped or substituted the source image: " + tag);
                reader.GetNode<Button>("Tabs/Image").EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(owner, 4);
                Require(reader.ShowingImage && reader.DisplayedTexture is { } texture && texture.GetWidth() > 100,
                    "Reader has no actual imported texture: " + tag);
                var canvas = reader.GetNode<Control>("PicturePage/ImageCanvas");
                var picture = canvas.GetNode<TextureRect>("Image");
                // Keep the failing presentation as evidence, too. A size assertion
                // must not prevent inspection of the actual constrained layout.
                var capturedPixels = await Shot(owner, tag + $"_{sample.Item1}x{sample.Item2}_fit");
                var rootWindow = owner.GetTree().Root;
                Require(canvas.GetViewport() == rootWindow && picture.GetViewport() == rootWindow,
                    "Image proof requires controls in the captured root viewport: " + tag);
                // Canvas-items stretch uses the root's virtual content size.
                // GetScreenTransform remained identity in the native 720p capture:
                // the logical 759x506 picture occupied 506x338 PNG pixels.
                // Convert virtual coordinates exactly once, using that capture's
                // dimensions rather than the requested window size or its filename.
                var rootLogicalSize = (Vector2)(rootWindow.ContentScaleMode != Window.ContentScaleModeEnum.Disabled
                    && rootWindow.ContentScaleSize.X > 0 && rootWindow.ContentScaleSize.Y > 0
                        ? rootWindow.ContentScaleSize : rootWindow.Size) / rootWindow.ContentScaleFactor;
                var actualPixels = capturedPixels ?? rootWindow.Size;
                Require(rootLogicalSize.X > 0 && rootLogicalSize.Y > 0 && actualPixels.X > 0 && actualPixels.Y > 0,
                    "Image proof has no measurable root viewport: " + tag);
                Require(capturedPixels is null || capturedPixels.Value == new Vector2I(sample.Item1, sample.Item2),
                    $"Image capture differs from the requested resolution: {tag} actual={capturedPixels}");
                var pixelScale = (Vector2)actualPixels / rootLogicalSize;
                var canvasLogical = canvas.Size * canvas.GetGlobalTransformWithCanvas().Scale.Abs();
                var pictureLogical = picture.Size * picture.GetGlobalTransformWithCanvas().Scale.Abs();
                var canvasPixels = canvasLogical * pixelScale;
                var picturePixels = pictureLogical * pixelScale;
                GD.Print($"document-image-ui: area {tag} {sample.Item1}x{sample.Item2} canvas={canvasPixels} fit={picturePixels} logicalCanvas={canvasLogical} logicalFit={pictureLogical} rootLogical={rootLogicalSize} contentBase={rootWindow.ContentScaleSize} contentFactor={rootWindow.ContentScaleFactor} rootVisible={rootWindow.GetVisibleRect().Size} window={rootWindow.Size} captured={capturedPixels?.ToString() ?? "not-run"} pixelSource={(capturedPixels.HasValue ? "PNG" : "window")} pixelScale={pixelScale}");
                Require(canvasPixels.X >= 360 && canvasPixels.Y >= 240 && Fits(canvas),
                    $"Image canvas cannot support source reading: {tag} rendered={canvasPixels}");
                Require(picturePixels.Y >= 240,
                    $"Fit reduced the source to a thumbnail: {tag} rendered={picturePixels}");
                Require(Math.Abs(picture.Size.X / picture.Size.Y
                    - reader.DisplayedTexture!.GetWidth() / (float)reader.DisplayedTexture.GetHeight()) < .001f,
                    "The image is stretched: " + tag);
                foreach (var button in reader.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Where(button => button.IsVisibleInTree()))
                    Require(Fits(button) && button.Size.Y >= 43, "Image control is clipped or too small: " + button.GetPath());
                Require(canvas.HasFocus(), "Image reader did not receive keyboard focus.");
                var knowledgeBefore = ((RuntimeBridge)owner.GetTree().GetFirstNodeInGroup("runtime_bridge"))
                    .SelectRuntimeState().GetProperty("knowledge").GetRawText();
                reader.GetNode<Button>("PicturePage/Tools/ZoomIn").EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(owner, 2);
                Require(reader.ZoomFactor > 1 && reader.ActiveAssetId == expected.AssetId, "Zoom changed the source.");
                canvas.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
                canvas.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Relative = new Vector2(80, 60) });
                canvas.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
                await Shot(owner, tag + $"_{sample.Item1}x{sample.Item2}_detail");
                reader.GetNode<Button>("PicturePage/Tools/Fit").EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(owner, 2);
                Require(Math.Abs(reader.ZoomFactor - 1) < .001, "Fit did not reset image zoom.");
                reader.GetNode<Button>("Tabs/Text").EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(owner, 2);
                Require(reader.GetParent().GetChildren().OfType<RichTextLabel>()
                    .Any(body => body.IsVisibleInTree() && body.HasFocus()),
                    "Returning to document text did not restore keyboard focus: " + tag);
                if (owner.GetTree().GetFirstNodeInGroup("journal_ui") is JournalUi journal && journal.IsAncestorOf(reader))
                    await VerifyJournalPagesAsync(owner, journal, tag + $"_{sample.Item1}x{sample.Item2}");
                Require(!reader.ShowingImage && knowledgeBefore == ((RuntimeBridge)owner.GetTree()
                    .GetFirstNodeInGroup("runtime_bridge")).SelectRuntimeState().GetProperty("knowledge").GetRawText(),
                    "Presentation interaction changed knowledge.");
            }
        }
        finally
        {
            DisplayServer.WindowSetSize(window);
            AccessibilityPresentation.ApplyToTree(owner.GetTree(), preferences);
            await Frames(owner, 4);
        }
        GD.Print($"document-image-ui: PASS {tag} actual={expected.AssetId} 720p-large/1080p fit/zoom/pan/text, no knowledge mutation");
    }

    private static async Task VerifyJournalPagesAsync(Node owner, JournalUi journal, string tag)
    {
        var tabs = journal.GetNode<TabBar>("Screen/Book/Layout/Tabs");
        var source = journal.GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Source");
        var sourceText = source.Text;
        var entryId = journal.ActiveEntryId;
        var bridge = (RuntimeBridge)owner.GetTree().GetFirstNodeInGroup("runtime_bridge");
        var sourceCount = bridge.JournalEntries().Select(entry => entry.SourceId).Distinct().Count();
        var overviewTab = Enumerable.Range(0, tabs.TabCount)
            .SingleOrDefault(index => tabs.GetTabTitle(index) == "Дела", -1);
        var wordsTab = Enumerable.Range(0, tabs.TabCount)
            .SingleOrDefault(index => tabs.GetTabTitle(index) == "Слова", -1);
        Require(overviewTab >= 0 && tabs.IsVisibleInTree() && Fits(tabs),
            $"The notebook overview is not discoverable: tab={overviewTab} visible={tabs.IsVisibleInTree()} "
            + $"rect={tabs.GetGlobalRect()} viewport={tabs.GetViewportRect().Size} titles="
            + string.Join(",", Enumerable.Range(0, tabs.TabCount).Select(tabs.GetTabTitle)));
        Require(source.IsVisibleInTree() && sourceText.Length > 0 && Fits(source), "The source/status line is clipped.");
        tabs.CurrentTab = overviewTab;
        await Frames(owner, 3);
        var overview = journal.GetNode<ScrollContainer>("Screen/Book/Layout/Overview");
        var objective = overview.GetNode<Label>("Contents/Objective");
        Require(overview.IsVisibleInTree() && Fits(overview) && objective.IsVisibleInTree(),
            "Moving the journal overview made goals inaccessible.");
        if (overview.GetVScrollBar().IsVisibleInTree())
        {
            overview.ScrollVertical = (int)overview.GetVScrollBar().MaxValue;
            await Frames(owner, 2);
            overview.ScrollVertical = 0;
            await Frames(owner, 2);
        }
        await Shot(owner, tag + "_overview");
        Require(wordsTab >= 0, "The separate vocabulary tab is not discoverable.");
        tabs.CurrentTab = wordsTab;
        await Frames(owner, 3);
        var words = journal.GetNode<Control>("Screen/Book/Layout/Vocabulary");
        Require(words.IsVisibleInTree() && Fits(words)
            && words.GetNode<Label>("Count").IsVisibleInTree()
            && words.GetNode<LineEdit>("SearchRow/Search").IsVisibleInTree(),
            "The separate vocabulary page is not readable.");
        tabs.CurrentTab = 1;
        await Frames(owner, 2);
        for (var slot = 1; slot <= 2; slot++)
        {
            var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot}/Source");
            Require(picker.IsVisibleInTree() && Fits(picker) && picker.ItemCount == sourceCount + 1,
                "The layout change lost a source or hid a comparison picker.");
        }
        tabs.CurrentTab = 0;
        await Frames(owner, 2);
        Require(journal.ActiveEntryId == entryId && source.IsVisibleInTree() && source.Text == sourceText,
            "Switching journal pages lost the selected source or its status.");
    }

    private static bool Fits(Control control)
    {
        var rect = control.GetGlobalRect();
        var size = control.GetViewportRect().Size;
        return rect.Position.X >= -1 && rect.Position.Y >= -1 && rect.End.X <= size.X + 1 && rect.End.Y <= size.Y + 1;
    }

    private static async Task<Vector2I?> Shot(Node owner, string name)
    {
        var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (string.IsNullOrEmpty(output)) return null;
        await Frames(owner, 2);
        RenderingServer.ForceDraw(false);
        using var image = owner.GetViewport().GetTexture().GetImage();
        System.IO.Directory.CreateDirectory(output);
        Require(!image.IsEmpty() && image.SavePng(System.IO.Path.Combine(output, name + ".png")) == Error.Ok,
            "Image reader capture failed: " + name);
        return new Vector2I(image.GetWidth(), image.GetHeight());
    }

    private static async Task Frames(Node owner, int count)
    {
        for (var i = 0; i < count; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool success, string detail)
    {
        if (!success) throw new InvalidOperationException(detail);
    }
}
