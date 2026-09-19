using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Exercises presentation on an already eligible, open source. It never presses
/// Record or calls a narrative command; the enclosing proof owns actual excerpts.
/// </summary>
internal static class SourceExcerptUiProof
{
    public static async Task VerifyAsync(Node host, RuntimeBridge bridge, SourceExcerptSelection selection)
    {
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print("source-excerpt-ui: external/not-run native keyboard/window/render proof requires a windowed display; source excerpt runtime checks continue in caller");
            return;
        }

        var editor = selection.ExcerptText;
        var body = (RichTextLabel)editor.GetParent();
        var begin = selection.GetNode<Button>("Actions/Begin");
        var back = selection.GetNode<Button>("Actions/Back");
        var record = selection.GetNode<Button>("Actions/Record");
        var feedback = selection.GetNode<Label>("Feedback");
        Require(selection.IsVisibleInTree() && body.IsVisibleInTree()
            && !begin.Disabled && bridge.SessionIdentity is not null,
            "Source excerpt UI proof requires the current eligible reader, reached through runtime.");

        var session = bridge.SessionIdentity;
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var journal = string.Join('|', bridge.JournalEntries().Select(entry => entry.EntryId).Order());
        var sourceText = body.GetParsedText();
        var root = host.GetTree().Root;
        var windowSize = DisplayServer.WindowGetSize();
        var windowPosition = DisplayServer.WindowGetPosition();
        var windowMode = DisplayServer.WindowGetMode();
        var wasSelecting = selection.Selecting;
        var hadSelection = editor.HasSelection();
        var originLine = editor.GetSelectionOriginLine();
        var originColumn = editor.GetSelectionOriginColumn();
        var caretLine = editor.GetCaretLine();
        var caretColumn = editor.GetCaretColumn();
        var editorScroll = editor.ScrollVertical;
        var bodyScroll = body.GetVScrollBar().Value;
        var focus = root.GuiGetFocusOwner();
        var feedbackText = feedback.Text;
        var feedbackVisible = feedback.Visible;
        try
        {
            if (selection.Selecting) back.EmitSignal(BaseButton.SignalName.Pressed);
            begin.GrabFocus();
            await KeyAsync(host, Key.Space, "begin");
            Require(selection.Selecting && editor.IsVisibleInTree() && editor.HasFocus()
                && !editor.Editable && editor.Text == sourceText,
                "Begin did not focus the actual source text in read-only selection mode.");
            var excerpt = await SelectWithShiftAsync(host, editor);
            Require(!record.Disabled, "A real keyboard selection did not enable the excerpt button.");

            await KeyAsync(host, Key.Escape, "return-to-reading");
            Require(!selection.Selecting && !editor.IsVisibleInTree() && body.IsVisibleInTree()
                && selection.IsVisibleInTree() && begin.IsVisibleInTree() && body.GetParsedText() == sourceText,
                "Esc closed the reader or failed to restore its formatted text.");

            begin.GrabFocus();
            await KeyAsync(host, Key.Space, "begin-again");
            Require(selection.Selecting && editor.HasFocus(), "The source cannot re-enter selection mode.");
            excerpt = await SelectWithShiftAsync(host, editor);
            await KeyAsync(host, Key.Tab, "tab-to-record");
            Require(record.HasFocus() && !record.Disabled && editor.GetSelectedText() == excerpt,
                "Tab lost the real selection or did not focus the enabled excerpt button.");

            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                DisplayServer.WindowSetSize(size);
                await RenderAsync(host, "resize-" + size);
                await RenderAsync(host, "layout-" + size);
                Require(selection.Selecting && editor.IsVisibleInTree() && editor.Text == sourceText
                    && editor.GetSelectedText() == excerpt && record.HasFocus(),
                    "Resizing changed the source, selection mode or keyboard focus.");
                var pixels = await CaptureAsync(host, size);
                var actualPixels = pixels ?? root.Size;
                var logicalSize = (Vector2)(root.ContentScaleMode != Window.ContentScaleModeEnum.Disabled
                    && root.ContentScaleSize.X > 0 && root.ContentScaleSize.Y > 0
                        ? root.ContentScaleSize : root.Size) / root.ContentScaleFactor;
                Require(actualPixels == size, $"Source excerpt capture has the wrong actual dimensions: {actualPixels}, expected {size}.");
                Require(logicalSize.X > 0 && logicalSize.Y > 0, "The source reader has no measurable root viewport.");
                Require(editor.GetViewport() == root && selection.GetViewport() == root,
                    "Source excerpt bounds must belong to the captured root viewport.");
                var pixelScale = (Vector2)actualPixels / logicalSize;
                var editorPixels = editor.Size * editor.GetGlobalTransformWithCanvas().Scale.Abs() * pixelScale;
                Require(editorPixels.X >= 180 && editorPixels.Y >= 100,
                    $"The source text has no usable scroll area at {size}: {editorPixels} actual pixels.");
                Require(Fits(body) && Fits(editor) && Fits(selection)
                    && body.GetGlobalRect().Grow(1).Encloses(editor.GetGlobalRect()),
                    "The source editor or excerpt controls are outside the visible reader.");
                foreach (var button in new[] { back, record })
                {
                    var buttonPixels = button.Size * button.GetGlobalTransformWithCanvas().Scale.Abs() * pixelScale;
                    Require(button.IsVisibleInTree() && Fits(button) && buttonPixels.Y >= 24
                        && button.Size.X + 1 >= button.GetMinimumSize().X,
                        $"Excerpt button is clipped at {size}: {button.GetPath()} pixels={buttonPixels}.");
                }
                Require(Fits(selection.GetNode<Label>("Hint")), "The excerpt instruction is clipped.");
                GD.Print($"source-excerpt-ui: bounds {size} actual={actualPixels} logical={logicalSize} textPixels={editorPixels} selectedCharacters={excerpt.Length} capture={(pixels.HasValue ? "PNG" : "not-requested")}");
            }

            Require(ReferenceEquals(session, bridge.SessionIdentity)
                && knowledge == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
                && journal == string.Join('|', bridge.JournalEntries().Select(entry => entry.EntryId).Order()),
                "Selection-only presentation changed knowledge, journal entries or the active session.");
        }
        finally
        {
            // Restoration is presentation-only, including a pre-existing selection.
            // Do not route cleanup through Enter/Space while Record has focus.
            if (GodotObject.IsInstanceValid(selection) && selection.IsInsideTree())
            {
                if (selection.Selecting) back.EmitSignal(BaseButton.SignalName.Pressed);
                if (wasSelecting && !begin.Disabled && body.IsVisibleInTree()
                    && ReferenceEquals(session, bridge.SessionIdentity))
                {
                    begin.EmitSignal(BaseButton.SignalName.Pressed);
                    editor.SetCaretLine(caretLine);
                    editor.SetCaretColumn(caretColumn);
                    if (hadSelection) editor.Select(originLine, originColumn, caretLine, caretColumn);
                    else editor.Deselect();
                    editor.ScrollVertical = editorScroll;
                }
                feedback.Text = feedbackText;
                feedback.Visible = feedbackVisible;
            }
            DisplayServer.WindowSetSize(windowSize);
            DisplayServer.WindowSetMode(windowMode);
            DisplayServer.WindowSetPosition(windowPosition);
            await RenderAsync(host, "restore-window");
            if (GodotObject.IsInstanceValid(body)) body.GetVScrollBar().Value = bodyScroll;
            if (wasSelecting && GodotObject.IsInstanceValid(editor) && editor.IsVisibleInTree())
                editor.ScrollVertical = editorScroll;
            if (focus is not null && GodotObject.IsInstanceValid(focus) && focus.IsInsideTree() && focus.IsVisibleInTree())
                focus.GrabFocus();
        }
        GD.Print("source-excerpt-ui: PASS real Shift+arrows, Esc-to-reader, re-entry, Tab-to-button, 720/1080 bounds; no excerpt recorded");
    }

    private static async Task<string> SelectWithShiftAsync(Node host, TextEdit editor)
    {
        var line = Enumerable.Range(0, editor.GetLineCount()).First(index =>
            editor.GetLine(index).Length >= 8 && !editor.GetLine(index).Any(char.IsSurrogate));
        editor.Deselect();
        editor.SetCaretLine(line);
        editor.SetCaretColumn(0);
        editor.GrabFocus();
        for (var character = 0; character < 8; character++)
            await KeyAsync(host, Key.Right, "select-character-" + character, shift: true);
        var selected = editor.GetSelectedText();
        Require(selected == editor.GetLine(line)[..8],
            "Shift+Right did not select characters from the visible source text.");
        return selected;
    }

    private static async Task KeyAsync(Node host, Key key, string label, bool shift = false)
    {
        using var press = new InputEventKey
        {
            Keycode = key, PhysicalKeycode = key, Pressed = true, Echo = false, ShiftPressed = shift
        };
        using var release = new InputEventKey
        {
            Keycode = key, PhysicalKeycode = key, Pressed = false, Echo = false, ShiftPressed = shift
        };
        Input.ParseInputEvent(press);
        try { await RenderAsync(host, label + "/press"); }
        finally { Input.ParseInputEvent(release); }
        await RenderAsync(host, label + "/release");
    }

    private static async Task<Vector2I?> CaptureAsync(Node host, Vector2I requested)
    {
        var output = System.Environment.GetEnvironmentVariable("URMAN_IMAGE_UI_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return null;
        Require(System.IO.Path.IsPathFullyQualified(output), "Source excerpt evidence directory must be absolute.");
        System.IO.Directory.CreateDirectory(output);
        var hostName = string.Concat(host.Name.ToString().Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        var path = System.IO.Path.Combine(output, $"source_excerpt_{hostName}_{requested.X}x{requested.Y}.png");
        Require(!System.IO.File.Exists(path), "Refusing to overwrite historical excerpt UI evidence: " + path);
        await RenderAsync(host, "capture-" + requested);
        using var image = host.GetViewport().GetTexture().GetImage();
        Require(!image.IsEmpty() && image.SavePng(path) == Error.Ok, "Source excerpt capture failed: " + path);
        GD.Print("source-excerpt-ui: capture " + path);
        return new Vector2I(image.GetWidth(), image.GetHeight());
    }

    private static bool Fits(Control control)
    {
        var rect = control.GetGlobalRect();
        if (!control.GetViewportRect().Grow(1).Encloses(rect)) return false;
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is Control { ClipContents: true } clip && !clip.GetGlobalRect().Grow(1).Encloses(rect)) return false;
        return true;
    }

    private static Task RenderAsync(Node host, string label) =>
        Act1StateFlowProof.WaitForRenderedFrameAsync(host, "source-excerpt-ui/" + label);

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }
}
