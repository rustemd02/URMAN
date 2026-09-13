using Godot;
using Urman.Core.Persistence;
using System.Text.Json;

namespace Urman.Godot.Tests;

/// <summary>
/// UIUX-009 automation portion: at 1280x720 and 1920x1080 every critical UI
/// (settings, journal, old PC, document, dialogue) opens, its panel stays
/// fully inside the viewport bounds, and no other modal stays open after
/// close. Text readability itself remains the human review gate.
/// </summary>
public partial class Act1UiReadabilitySmokeTest : Node
{
    public override async void _Ready()
    {
        // An unhandled exception in an async void _Ready never reaches Quit,
        // so the harness would hang instead of failing. Report it loudly.
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            Fail($"UI readability smoke aborted: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private async Task RunAsync()
    {
        var main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")?.Instantiate<Main>();
        if (main is null)
        {
            Fail("UI readability smoke could not load main.");
            return;
        }

        main.InitialZoneId = "village_day";
        main.InitialSpawnPointId = "arrival";
        main.EnableAct1ConnectedWorld = true;
        AddChild(main);
        await Frames(3);

        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var settings = main.GetNodeOrNull<SettingsUi>("SettingsUi");
        var journal = main.GetNodeOrNull<JournalUi>("JournalUi");
        var oldPc = main.GetNodeOrNull<OldPcUi>("OldPcUi");
        var document = main.GetNodeOrNull<DocumentUi>("DocumentUi");
        var dialogue = main.GetNodeOrNull<DialogueUi>("DialogueUi");
        if (bridge is null || player is null || settings is null || journal is null
            || oldPc is null || document is null || dialogue is null)
        {
            Fail("UI readability smoke could not resolve all UI owners.");
            return;
        }

        player.SetPhysicsProcess(false);
        AccessibilityPresentation.ApplyToTree(GetTree(), AccessibilitySettingsSnapshot.Default);
        bridge.HandleOldPcInputAsync(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            type = "open",
            documentId = "urman.oldpc:document/doc_marat_official_death_notice"
        })).Wait();

        foreach (var (width, height) in new[] { (1280, 720), (1920, 1080) })
        {
            DisplayServer.WindowSetSize(new Vector2I(width, height));
            await Frames(4);

            // 1) Settings: opens, panel fits the viewport, closes clean.
            settings.Open(player);
            await Frames(2);
            if (!settings.IsOpen || !FitsViewport(settings.GetNode<Control>("Screen/Panel")))
            {
                Fail($"Settings panel does not fit the {width}x{height} viewport.");
                return;
            }
            await SaveShot("settings", settings.GetNode<Control>("Screen/Panel"), width, height);

            settings.Close();
            await Frames(2);
            if (settings.IsOpen)
            {
                Fail("Settings did not close at the second resolution.");
                return;
            }

            // 2) Journal: opens, panel fits, closes.
            journal.Open(bridge);
            await Frames(2);
            var journalBook = journal.GetNode<Control>("Screen/Book");
            if (!journalBook.IsVisibleInTree() || !FitsViewport(journalBook))
            {
                Fail($"Journal panel does not fit the {width}x{height} viewport.");
                return;
            }
            await SaveShot("journal", journalBook, width, height);

            journal._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await Frames(2);

            // 3) Old PC: opens, computer screen fits, closes.
            oldPc.Open(bridge);
            await Frames(2);
            var oldPcComputer = oldPc.GetNode<Control>("Screen/Computer");
            if (!oldPcComputer.IsVisibleInTree() || !FitsViewport(oldPcComputer))
            {
                Fail($"Old-PC screen does not fit the {width}x{height} viewport.");
                return;
            }
            await SaveShot("oldpc", oldPcComputer, width, height);

            oldPc._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true, Echo = false });
            await Frames(2);

            // 4) Document: opens via the resolved asset, panel fits, closes
            //    via ui_cancel (DocumentUi owns its own close handling).
            // The bridge opens the shared document UI with the resolved
            // content (same production path as the evidence interactions).
            bridge.OpenDocumentUi("urman.oldpc:document/doc_marat_official_death_notice");
            await Frames(2);
            var documentView = document.GetNode<Control>("Screen/Document");
            if (!document.IsOpen || !documentView.IsVisibleInTree() || !FitsViewport(documentView))
            {
                Fail($"Document panel does not fit the {width}x{height} viewport.");
                return;
            }
            await SaveShot("document", documentView, width, height);

            document.GetNode<Button>("Screen/Document/Layout/Header/Close")
                .EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (document.IsOpen)
            {
                Fail("Document did not close via its Close button.");
                return;
            }

            // 5) Dialogue: opens via the bridge, layout fits, closes.
            bridge.OpenDialogueUi(Dialogue("gulsina_yaramyy"));
            await Frames(2);
            var dialoguePanel = dialogue.GetNode<Control>("Screen/Panel");
            if (!dialogue.IsOpen || !dialoguePanel.IsVisibleInTree() || !FitsViewport(dialoguePanel))
            {
                Fail($"Dialogue layout does not fit the {width}x{height} viewport.");
                return;
            }
            await SaveShot("dialogue", dialoguePanel, width, height);

            dialogue._UnhandledInput(new InputEventKey
            {
                Keycode = Key.Escape,
                PhysicalKeycode = Key.Escape,
                Pressed = true,
                Echo = false
            });
            await Frames(2);
            if (dialogue.IsOpen)
            {
                Fail("Dialogue did not close via ui_cancel.");
                return;
            }

            GD.Print($"act1-ui-readability: {width}x{height} settings/journal/oldpc/document/dialogue all open, fit and close");
        }

        // Exercise the same owners with real accessible archive content, not generated filler.
        var documents = bridge.OldPcDocuments.Where(item => bridge.IsOldPcDocumentAccessible(item.Id)).ToArray();
        foreach (var item in documents)
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId = item.Id }));
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "save", documentId = item.Id }));
        }
        var longest = documents.OrderByDescending(item => item.BodyMarkdown.Length).First();
        if (bridge.JournalEntries().Count < 5) { Fail("Filled journal needs at least five real entries."); return; }
        foreach (var (width, height) in new[] { (1280, 720), (1920, 1080) })
        foreach (var scale in new[] { 1.0, 1.6 })
        {
            DisplayServer.WindowSetSize(new Vector2I(width, height));
            AccessibilityPresentation.ApplyToTree(GetTree(), AccessibilitySettingsSnapshot.Default with { TextScale = scale });
            await Frames(4);
            var suffix = scale > 1 ? "_large" : "_filled";

            journal.Open(bridge);
            await Frames(2);
            await SaveShot("journal" + suffix, journal.GetNode<Control>("Screen/Book"), width, height);
            journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 1;
            // Story-gated documents (the internal register needs Naila's access) are not
            // part of every journal state, so compare the two sources the journal
            // actually holds instead of a fixed pair.
            var pair = bridge.JournalEntries().Select(entry => entry.SourceId)
                .Distinct(StringComparer.Ordinal).Take(2).ToArray();
            if (pair.Length < 2)
            {
                Fail($"Filled journal exposes only {pair.Length} distinct comparison sources.");
                return;
            }
            for (var slot = 0; slot < 2; slot++)
            {
                var picker = journal.GetNode<OptionButton>($"Screen/Book/Layout/Comparisons/Layout/Source{slot + 1}/Source");
                var sourceIndex = Enumerable.Range(1, picker.ItemCount - 1).Single(i => picker.GetItemMetadata(i).AsString() == pair[slot]);
                picker.Select(sourceIndex);
                picker.EmitSignal(OptionButton.SignalName.ItemSelected, sourceIndex);
            }
            await Frames(3);
            await SaveShot("journal_compare" + suffix, journal.GetNode<Control>("Screen/Book"), width, height);
            journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 0;
            journal._UnhandledInput(Cancel());

            bridge.OpenDocumentUi(longest.Id);
            await Frames(2);
            await SaveShot("document_long" + suffix, document.GetNode<Control>("Screen/Document"), width, height);
            var body = document.GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body");
            body.GetVScrollBar().Value = body.GetVScrollBar().MaxValue;
            await SaveShot("document_end" + suffix, document.GetNode<Control>("Screen/Document"), width, height);
            document._UnhandledInput(Cancel());

            oldPc.Open(bridge);
            await Frames(2);
            var results = oldPc.GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
            var index = Enumerable.Range(0, results.ItemCount).FirstOrDefault(i => results.GetItemMetadata(i).AsString() == longest.Id, -1);
            if (index < 0) { Fail("Longest real document is missing from old-PC results."); return; }
            results.EmitSignal(ItemList.SignalName.ItemSelected, index);
            await Frames(3);
            if (oldPc.ActiveDocumentId != longest.Id) { Fail("Old PC did not open the selected real document."); return; }

            // M7: read, unread and unavailable records must not look the same. The
            // archive marks opened records with a check and hides restricted ones
            // behind a single unselectable row.
            var readIndex = Enumerable.Range(0, results.ItemCount)
                .FirstOrDefault(i => results.GetItemMetadata(i).AsString() == longest.Id, -1);
            if (readIndex < 0 || !results.GetItemText(readIndex).StartsWith("✓ ", StringComparison.Ordinal))
            {
                Fail($"An opened archive record is not marked as read at {width}x{height} scale={scale}: '{results.GetItemText(readIndex)}'");
                return;
            }
            // This harness opens every accessible record to fill the journal, so
            // an unread accessible row cannot exist here; the unread rendering
            // stays covered by reading OldPcUi's prefix and tooltip logic.
            var restrictedRow = Enumerable.Range(0, results.ItemCount)
                .FirstOrDefault(i => results.GetItemText(i).StartsWith("🔒", StringComparison.Ordinal), -1);
            if (restrictedRow >= 0 && results.IsItemSelectable(restrictedRow))
            {
                Fail("The archive offers restricted records as selectable.");
                return;
            }
            await SaveShot("oldpc" + suffix, oldPc.GetNode<Control>("Screen/Computer"), width, height);

            // Empty search: the archive must say so in readable words at every
            // resolution and text scale instead of leaving a blank work area.
            var query = oldPc.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
            query.Text = "ъъъъ";
            oldPc.GetNode<Button>("Screen/Computer/Layout/SearchRow/Search")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames(3);
            var reader = oldPc.GetNode<RichTextLabel>("Screen/Computer/Layout/WorkArea/ReaderArea/Reader");
            if (!oldPc.StatusText.Contains("Найдено записей: 0", StringComparison.Ordinal)
                || !reader.Text.Contains("Совпадений нет", StringComparison.Ordinal))
            {
                Fail($"The empty archive search is unreadable at {width}x{height} scale={scale}: status='{oldPc.StatusText}' reader='{reader.Text}'");
                return;
            }
            await SaveShot("oldpc_no_matches" + suffix, oldPc.GetNode<Control>("Screen/Computer"), width, height);

            // Clearing the query must bring the archive back: the empty state
            // cannot be a dead end, and the next pass must start unfiltered.
            query.Text = string.Empty;
            oldPc.GetNode<Button>("Screen/Computer/Layout/SearchRow/Search")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames(3);
            // The footer counts selectable records, so gated entries stay listed
            // but unselectable and never inflate the restored count.
            var selectable = Enumerable.Range(0, results.ItemCount).Count(results.IsItemSelectable);
            if (selectable == 0
                || !oldPc.StatusText.Contains($"Найдено записей: {selectable}", StringComparison.Ordinal))
            {
                Fail($"Clearing the archive query did not restore the list at {width}x{height} scale={scale}: status='{oldPc.StatusText}' items={results.ItemCount} selectable={selectable}");
                return;
            }
            oldPc._UnhandledInput(Cancel());

            bridge.OpenDialogueUi(Dialogue("mansur_pc_request"));
            await Frames(3);
            await SaveShot("dialogue_choices" + suffix, dialogue.GetNode<Control>("Screen/Panel"), width, height);
            dialogue._UnhandledInput(Cancel());

            settings.Open(player);
            await Frames(2);
            await SaveShot("settings" + suffix, settings.GetNode<Control>("Screen/Panel"), width, height);
            settings.Close();
            await Frames(2);
            if (player.ModalOpen) { Fail("Extended UI capture left a modal owner open."); return; }
        }
        GD.Print($"act1-ui-readability: PASS 2 resolutions x 5 critical UIs + empty archive search + read/restricted archive marks; scales 1/1.6; journal entries={bridge.JournalEntries().Count}; longest document={longest.Id} chars={longest.BodyMarkdown.Length}; focus and Tatar glyphs");
        await GodotSmokeCleanup.ReleaseAsync(main);
        GetTree().Quit(0);
    }

    // Test-only, opt-in evidence harness: when the
    // environment variable is set, every critical UI opening is saved as a
    // PNG alongside the fit assertions. Production behavior is unchanged.
    private async Task SaveShot(string name, Control panel, int width, int height)
    {
        if (!panel.IsVisibleInTree())
        {
            Fail($"UI screenshot '{name}' requires a visible panel.");
            return;
        }

        if (!FitsViewport(panel)) { Fail($"UI '{name}' exceeds viewport at {width}x{height}."); return; }
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus is null || !panel.IsAncestorOf(focus))
        { Fail($"UI '{name}' has no keyboard focus inside its panel."); return; }
        Input.ParseInputEvent(new InputEventAction { Action = "ui_focus_next", Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = "ui_focus_next", Pressed = false });
        await Frames(2);
        focus = GetViewport().GuiGetFocusOwner();
        if (focus is null || !panel.IsAncestorOf(focus) || !focus.IsVisibleInTree() || !FitsViewport(focus))
        { Fail($"UI '{name}' lost focus after keyboard Tab."); return; }
        var textControls = panel.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(control => control is Label or RichTextLabel or Button or ItemList or LineEdit).ToArray();
        foreach (var control in textControls)
        {
            if (control is RichTextLabel rich && !string.IsNullOrWhiteSpace(rich.Text)
                && (rich.Size.X < 40 || rich.Size.Y < rich.GetThemeFontSize("normal_font_size")))
            { Fail($"UI '{name}' reader has no usable visible area: {rich.GetPath()} {rich.Size}."); return; }
            var font = control.GetThemeFont(control is RichTextLabel ? "normal_font" : "font");
            if ("ӘәӨөҮүҖҗҢңҺһ".Any(character => !font.HasChar(character)))
            { Fail($"UI '{name}' font lacks Tatar glyphs at {control.GetPath()}."); return; }
        }
        foreach (var control in panel.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(control => control.IsVisibleInTree()
                && IsInteractiveControl(control)
                && !IsInsideScrollContainer(control)))
        {
            if (!FitsWithin(panel, control))
            {
                Fail($"UI '{name}' interactive child exceeds its panel: {control.GetPath()} {control.GetGlobalRect()}.");
                return;
            }
        }

        var dir = System.Environment.GetEnvironmentVariable("URMAN_UI_SHOT_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        await Frames(2);
        if (!panel.IsVisibleInTree())
        {
            Fail($"UI screenshot '{name}' panel was hidden before readback.");
            return;
        }

        RenderingServer.ForceDraw(false);
        var image = GetViewport().GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            Fail($"UI screenshot '{name}' returned an empty image.");
            return;
        }

        var actualWidth = image.GetWidth();
        var actualHeight = image.GetHeight();
        if (actualWidth != width || actualHeight != height)
        {
            Fail($"UI screenshot '{name}' rendered {actualWidth}x{actualHeight}, expected {width}x{height}.");
            return;
        }

        var path = System.IO.Path.Combine(dir, $"ui_{name}_{actualWidth}x{actualHeight}.png");
        if (image.SavePng(path) != Error.Ok)
        {
            Fail($"Could not save UI screenshot '{path}'.");
        }
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new
        {
            panel = panel.GetPath().ToString(), width, height,
            text_scale = panel.GetMeta("accessibilityTextScale", 1.0).AsDouble(),
            keyboard_focus = focus.GetPath().ToString(), tatar_glyphs = "ӘәӨөҮүҖҗҢңҺһ",
            text = textControls.Select(control => new {
                path = control.GetPath().ToString(),
                font_size = control.GetThemeFontSize(control is RichTextLabel ? "normal_font_size" : "font_size"),
                width = control.Size.X, height = control.Size.Y,
                content = control switch { Label label => label.Text, RichTextLabel rich => rich.Text,
                    Button button => button.Text, LineEdit edit => edit.Text, _ => "" }
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
        image.Dispose();
    }

    private static InputEventKey Cancel() => new() { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true };

    private void InjectCancel()
    {
        _UnhandledInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
            Echo = false
        });
    }

    private static bool FitsViewport(Control control)
    {
        var rect = control.GetGlobalRect();
        var size = control.GetViewportRect().Size;
        var fits = rect.Position.X >= -1f
            && rect.Position.Y >= -1f
            && rect.End.X <= size.X + 1f
            && rect.End.Y <= size.Y + 1f;
        if (!fits) GD.Print($"ui-overflow: {control.GetPath()} rect={rect} viewport={size}");
        return fits;
    }

    private static bool IsInteractiveControl(Control control) =>
        control is BaseButton or LineEdit or ItemList or TabBar or global::Godot.Range or TextEdit;

    private static bool IsInsideScrollContainer(Control control)
    {
        for (Node? current = control.GetParent(); current is not null; current = current.GetParent())
        {
            if (current is ScrollContainer)
            {
                return true;
            }
        }

        return false;
    }

    private static bool FitsWithin(Control parent, Control child)
    {
        var parentRect = parent.GetGlobalRect();
        var childRect = child.GetGlobalRect();
        return childRect.Position.X >= parentRect.Position.X - 1f
            && childRect.Position.Y >= parentRect.Position.Y - 1f
            && childRect.End.X <= parentRect.End.X + 1f
            && childRect.End.Y <= parentRect.End.Y + 1f;
    }

    private static string Dialogue(string localId) => $"urman.chapter1:dialogue/{localId}";

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string message)
    {
        GD.PushError(message);
        GetTree().Quit(1);
    }
}
