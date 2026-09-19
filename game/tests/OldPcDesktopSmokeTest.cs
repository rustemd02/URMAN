using Godot;
using System.Text.Json;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

/// <summary>
/// Focused UI/capability integration proof. Native screenshots are optional;
/// a headless run proves state behaviour, never visual quality.
/// </summary>
public partial class OldPcDesktopSmokeTest : Node
{
    private const string LockedSource = "urman.oldpc:document/rec_marat_case_register_conflict";
    private const string VillageArticle = "urman.oldpc:document/tw_kara_urman_village";
    private const string LinkedJournalSource = "urman.oldpc:document/tw_local_names";
    private const string Slot = "oldpc-desktop-proof";
    private readonly List<string> _checks = [];

    public override async void _Ready()
    {
        Main? main = null;
        try
        {
            main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")!.Instantiate<Main>();
            AddChild(main);
            await Frames(3);
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge
                ?? throw new InvalidOperationException("Missing shared runtime.");
            var ui = GetTree().GetFirstNodeInGroup("old_pc_ui") as OldPcUi
                ?? throw new InvalidOperationException("Missing existing old-PC UI.");
            ui.Open(bridge);
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
            await Frames(4);
            Check(ui.OpenApplicationIds.Contains("archive"), "Archive remains available through the existing UI.");
            var query = ui.GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
            query.GrabFocus();
            Check(query.HasFocus(), "The archive query accepts keyboard focus.");
            Check(query.GetThemeStylebox("focus") is StyleBoxFlat { DrawCenter: false },
                "Focused input keeps its readable paper instead of a dark overlay.");
            var search = ui.GetNode<Button>("Screen/Computer/Layout/SearchRow/Search");
            Check(search.GetThemeStylebox("focus") is StyleBoxFlat { DrawCenter: false },
                "Focused archive buttons keep the same readable background.");
            ui.CloseApplication("archive");
            var shortcuts = ui.GetNode<GridContainer>("Screen/Desktop/Shortcuts");
            Check(shortcuts.GetChildren().OfType<Button>().All(button => button.GetNodeOrNull<OldPcDesktopIcon>("Glyph") is { Kind.Length: > 0 }
                && button.GetThemeStylebox("normal") is StyleBoxFlat { BgColor.A: 0 }),
                "Desktop shortcuts have drawn pictograms and readable captions over the wallpaper.");
            await Capture("01_desktop");
            var start = ui.GetNode<Button>("Screen/Taskbar/Layout/Start");
            var menu = ui.GetNode<Control>("Screen/StartMenu");
            start.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            Check(menu.Visible && (menu.FindChild("Launch_archive", true, false) as Button)!.HasFocus(),
                "Start opens its application menu and gives its first entry keyboard focus.");
            CheckInsideScreen(ui, menu, "Start menu remains inside the desktop.");
            await Capture("01b_start_menu");
            ui._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
            Check(!menu.Visible && ui.GetNode<Control>("Screen").Visible && start.HasFocus(),
                "Escape closes Start before leaving the computer and returns keyboard focus.");

            var knowledgeBefore = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
            ui.NavigateBrowser("tatwiki");
            await Frames(4);
            Check(ui.BrowserDocumentId is null, "Opening a site index does not read its articles.");
            Check(knowledgeBefore == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
                "Desktop navigation must not grant investigation knowledge.");
            ui.NavigateBrowser("doc:" + LockedSource);
            await Frames(4);
            Check(ui.BrowserDocumentId is null && !bridge.IsOldPcDocumentAccessible(LockedSource),
                "Typing the locked record ID cannot bypass its source conditions.");
            Check(ui.BrowserAddress == "about:blank", "Denied source titles cannot leak into the address bar.");
            Check(!ui.GetNode<RichTextLabel>("Screen/App_browser/Layout/Content/Page").GetParsedText()
                .Contains("реестр Марата", StringComparison.OrdinalIgnoreCase), "Denied browser page does not expose a locked title.");

            var addressField = ui.GetNode<LineEdit>("Screen/App_browser/Layout/Content/Navigation/Address");
            addressField.GrabFocus();
            addressField.Text = "https://tatwiki.local/кара-урман";
            addressField.EmitSignal(LineEdit.SignalName.TextSubmitted, addressField.Text);
            await Frames(8);
            Check(ui.BrowserDocumentId == VillageArticle, "The browser opens a compiled local article through shared runtime.");
            Check(ui.BrowserAddress == "tatwiki.local/кара-урман", "The browser displays a readable local URL without a content ID.");
            var page = ui.GetNode<RichTextLabel>("Screen/App_browser/Layout/Content/Page");
            Check(page.GetThemeFontSize("normal_font_size") == 24,
                "Article text uses a readable 24 design-pixel baseline at 720p.");
            page.EmitSignal(RichTextLabel.SignalName.MetaClicked, Variant.From("doc:urman.oldpc:document/tw_local_names"));
            await Frames(8);
            Check(ui.BrowserDocumentId == "urman.oldpc:document/tw_local_names", "A real page link reaches another compiled source.");
            await Capture("02_tatwiki");
            await VerifyLinkedJournalPage(bridge, ui, saveFromBrowser: true, "02b_journal_linked_source");
            ui.Open(bridge);

            ui.NavigateBrowser("yalkyn");
            await Frames(4);
            var social = ui.GetNode<ScrollContainer>("Screen/App_browser/Layout/Content/SocialPage");
            Check(ui.SocialPageVisible && !page.Visible, "The social site uses its visible feed rather than the article pane.");
            ClickSocialLink(social, "urman.oldpc:document/social_community_avyl");
            await Frames(8);
            var visibleSocial = SocialText(social);
            Check(visibleSocial.Contains("Мансур", StringComparison.Ordinal)
                && visibleSocial.Contains("Алсу", StringComparison.Ordinal), "Visible social cards render the authored post and reply authors.");
            Check(social.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
                .Any(panel => panel.HasMeta("socialRole") && panel.GetMeta("socialRole").AsString() == "comment"),
                "Replies have distinct comment cards inside the same source.");
            Check(visibleSocial.Contains("Раздел пока недоступен", StringComparison.Ordinal)
                && !visibleSocial.Contains(bridge.OldPcDocuments.Single(document => document.Id == "urman.oldpc:document/social_family_photo").Title, StringComparison.Ordinal),
                "A social card preserves the locked family-photo gate without exposing its title.");
            Check(!visibleSocial.Contains("(doc:", StringComparison.Ordinal) && !visibleSocial.Contains("urman.oldpc:", StringComparison.Ordinal),
                "Source-link punctuation is never mistaken for an author label or shown as an internal address.");
            Check(SocialLink(social, "urman.oldpc:document/social_alsu_profile").Text.Contains("][u]Профиль Алсу[/u][/url]", StringComparison.Ordinal),
                "The first profile link remains complete and visibly underlined.");
            await Capture("03_yalkyn");
            social.ScrollVertical = (int)social.GetVScrollBar().MaxValue;
            await Capture("03b_yalkyn_replies");
            ClickSocialLink(social, "urman.oldpc:document/social_alsu_profile");
            await Frames(8);
            Check(ui.BrowserDocumentId == "urman.oldpc:document/social_alsu_profile",
                "The formerly broken first profile link opens Alsu through the shared runtime.");
            ui.GetNode<Button>("Screen/App_browser/Layout/Content/Navigation/Back").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(8);
            ClickSocialLink(social, "urman.oldpc:document/social_mansur_profile");
            await Frames(8);
            Check(social.FindChildren("Avatar", "Control", true, false).OfType<OldPcDesktopIcon>()
                .Any(avatar => avatar.Kind == "person"), "A resident profile has its own identity card without inventing a portrait.");
            await Capture("03c_yalkyn_profile");
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default with { TextScale = 1.6, HighContrast = true });
            await Frames(5);
            Check(social.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
                .All(text => text.GetThemeFontSize("normal_font_size") == 38),
                "All currently visible social bodies follow the 1.6 text-scale setting.");
            CheckInsideScreen(ui, ui.GetNode<Control>("Screen/App_browser"), "Large text keeps browser controls inside the desktop.");
            CheckInsideScreen(ui, ui.GetNode<Control>("Screen/Taskbar"), "Large text keeps the taskbar inside the desktop.");
            await Capture("03d_yalkyn_large_text");
            ui.GetNode<Button>("Screen/App_browser/Layout/Header/Minimize").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(4);
            var computerShortcut = shortcuts.GetNode<Button>("Icon_files");
            Check(computerShortcut.GetThemeFont("font").GetStringSize("компьютер", HorizontalAlignment.Left, -1,
                computerShortcut.GetThemeFontSize("font_size")).X <= computerShortcut.Size.X - 16,
                "Large desktop captions fit their longest word without breaking it into fragments.");
            Check(shortcuts.GetNode<Button>("Icon_trash").GetThemeConstant("outline_size") > 0,
                "Desktop captions keep a dark outline over the snow in the wallpaper.");
            CheckInsideScreen(ui, shortcuts, "Large desktop shortcuts stay inside the screen.");
            var glyphBounds = shortcuts.GetChildren().OfType<Button>().Select(button =>
            {
                var glyph = button.GetNode<OldPcDesktopIcon>("Glyph");
                return new { button = button.Name.ToString(), buttonRect = button.GetGlobalRect(),
                    glyphRect = glyph.GetGlobalRect(), position = glyph.Position, size = glyph.Size,
                    anchors = new[] { glyph.AnchorLeft, glyph.AnchorTop, glyph.AnchorRight, glyph.AnchorBottom },
                    offsets = new[] { glyph.OffsetLeft, glyph.OffsetTop, glyph.OffsetRight, glyph.OffsetBottom } };
            }).ToArray();
            GD.Print(JsonSerializer.Serialize(new { test = "oldpc-large-desktop-glyphs",
                screen = ui.GetNode<Control>("Screen").GetGlobalRect().ToString(),
                glyphs = glyphBounds.Select(item => new { item.button, buttonRect = item.buttonRect.ToString(),
                    glyph = item.glyphRect.ToString(), position = item.position.ToString(), size = item.size.ToString(),
                    item.anchors, item.offsets }) }));
            await Capture("03e_large_desktop");
            foreach (var glyph in glyphBounds)
            {
                Check(ui.GetNode<Control>("Screen").GetGlobalRect().Grow(1).Encloses(glyph.buttonRect),
                    "Large desktop shortcut stays inside the actual screen: " + glyph.button);
                Check(glyph.buttonRect.Grow(1).Encloses(glyph.glyphRect),
                    "Large desktop glyph stays inside its actual shortcut: " + glyph.button);
                Check(ui.GetNode<Control>("Screen").GetGlobalRect().Grow(1).Encloses(glyph.glyphRect),
                    "Large desktop glyph stays inside the actual screen: " + glyph.button);
            }
            ui.LaunchApplication("browser");
            ui.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
            ui.GetNode<Button>("Screen/App_browser/Layout/Content/Favorites/History").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!ui.SocialPageVisible && page.Visible, "History clears the previous social feed before rendering navigation.");
            ui.NavigateBrowser("doc:" + VillageArticle);
            await Frames(8);
            Check(!ui.SocialPageVisible && page.Visible, "Returning to TatWiki restores the article reader without stale social cards.");
            ui.NavigateBrowser("doc:urman.oldpc:document/social_community_avyl");
            await Frames(8);

            ui.NewPersonalFile(false);
            await Frames(3);
            var editor = ui.GetNode<TextEdit>("Screen/App_notepad/Layout/Content/Text");
            editor.Text = "Әби попросила позвонить маме.\nУточнить название улицы.";
            ui.GetNode<Button>("Screen/App_notepad/Layout/Content/Tools/Save").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            var noteId = ui.PersonalFiles.Single().Id;
            Check(ui.PersonalFiles[0].Text.Contains("Әби", StringComparison.Ordinal), "Personal note keeps Tatar characters.");
            editor.GrabFocus();
            ui.LaunchApplication("browser");
            Check(addressField.HasFocus() && !editor.HasFocus(),
                "Activating the browser moves keyboard input out of the background note.");
            ui._Input(new InputEventKey { Pressed = true, AltPressed = true, Keycode = Key.Tab });
            Check(ui.ActiveApplicationId == "notepad" && editor.HasFocus(),
                "Alt+Tab returns keyboard input to the foreground note.");
            Check(ui.GetNode<OldPcWindowTitleBar>("Screen/App_notepad/Layout/Header").Active
                && !ui.GetNode<OldPcWindowTitleBar>("Screen/App_browser/Layout/Header").Active,
                "Window-title appearance follows the application that receives keyboard input.");

            ui.LaunchApplication("files");
            var files = ui.GetNode<ItemList>("Screen/App_files/Layout/Content/Files");
            SelectRow(files, "folder:notes");
            ui.GetNode<Button>("Screen/App_files/Layout/Content/Tools/Open").EmitSignal(BaseButton.SignalName.Pressed);
            SelectRow(files, "note:" + noteId);
            ui.GetNode<Button>("Screen/App_files/Layout/Content/Tools/Delete").EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.PersonalFiles.Single().Deleted, "Delete moves the personal note into the simulated recycle bin.");
            await Frames(5);
            var fileWindow = ui.GetNode<Control>("Screen/App_files");
            var fileStatus = fileWindow.GetNode<Label>("Layout/Content/Status");
            GD.Print(JsonSerializer.Serialize(new { test = "oldpc-file-layout", panel = fileWindow.GetGlobalRect().ToString(),
                minimum = fileWindow.GetCombinedMinimumSize().ToString(), status = fileStatus.GetGlobalRect().ToString(),
                listAutoHeight = files.AutoHeight, listMinimum = files.GetCombinedMinimumSize().ToString() }));
            CheckInsideScreen(ui, fileWindow, "File manager refits after large-text settings and folder changes.");
            Check(fileStatus.GetGlobalRect().End.Y <= ui.GetNode<Control>("Screen/Taskbar").GetGlobalRect().Position.Y,
                "The file-manager status remains above the taskbar.");
            await Capture("04a_file_manager");
            ui.LaunchApplication("trash");
            var trash = ui.GetNode<ItemList>("Screen/App_trash/Layout/Content/Files");
            SelectRow(trash, "note:" + noteId);
            await Capture("04b_recycle_bin");
            ui.GetNode<Button>("Screen/App_trash/Layout/Content/Restore").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!ui.PersonalFiles.Single().Deleted, "Recycle-bin restore returns the same stable personal file.");

            ui.NewPersonalFile(true);
            await Frames(3);
            var writer = ui.GetNode<TextEdit>("Screen/App_writer/Layout/Content/Text");
            writer.Text = "[b]Список[/b]\nПозвонить домой.";
            ui.GetNode<Button>("Screen/App_writer/Layout/Content/Format/Preview").EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.GetNode<RichTextLabel>("Screen/App_writer/Layout/Content/Page").Visible,
                "Text editor renders the formatted page.");
            await Capture("04_writer_and_windows");

            var browser = ui.GetNode<Control>("Screen/App_browser");
            ui.LaunchApplication("browser");
            var beforeMove = browser.Position;
            var title = browser.GetNode<Label>("Layout/Header/Title");
            title.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
                { ButtonIndex = MouseButton.Left, Pressed = true });
            ui._Input(new InputEventMouseMotion { Position = new Vector2(60, 60) });
            ui._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            Check(browser.Position != beforeMove, "Dragging the title updates a clamped window position.");
            browser.GetNode<Button>("Layout/Header/Minimize").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!browser.Visible, "Minimize hides only the selected application.");
            ui.GetNode<Button>("Screen/Taskbar/Layout/Tasks/Task_browser").EmitSignal(BaseButton.SignalName.Pressed);
            Check(browser.Visible && ui.ActiveApplicationId == "browser", "Taskbar restores and focuses the application.");
            Check(addressField.HasFocus(), "Taskbar restoration also restores keyboard focus to the browser.");

            ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            Check(await bridge.SaveSlotAsync(Slot), "The existing game slot stores desktop state.");
            var savedDesktop = bridge.OldPcState().GetProperty("desktop").GetRawText();
            Check(await bridge.StartNewGameAsync(), "A separate fresh session starts normally.");
            ui.Open(bridge);
            Check(ui.PersonalFiles.Count == 0, "New game does not inherit another session's notes.");
            ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            Check(await bridge.LoadSlotAsync(Slot), "The existing save loader restores the desktop capability.");
            ui.Open(bridge);
            await Frames(4);
            Check(ui.PersonalFiles.Count == 2 && ui.PersonalFiles.Any(note => note.Id == noteId && !note.Deleted),
                "Load restores both personal files and the recycle-bin consequence.");
            var restored = bridge.OldPcState().GetProperty("desktop").Deserialize<OldPcDesktopSnapshot>(OldPcDesktopSnapshot.JsonOptions)!;
            Check(restored.BrowserHistory.Contains("doc:" + VillageArticle), "Browser history survives the shared save.");
            Check(!ui.BrowserAddress.Contains("urman.oldpc:", StringComparison.Ordinal),
                "Restored history presents a local URL while preserving stable source IDs.");
            Check(ui.OpenApplicationIds.Contains("browser"), "Open windows survive the shared save.");
            Check(addressField.HasFocus(), "Reopening the saved desktop focuses its foreground application.");
            await Capture("05_restored_desktop");
            ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            await VerifyLinkedJournalPage(bridge, ui, saveFromBrowser: false, "06_journal_linked_source_after_load");
            await VerifyPictureSourceLinks(bridge, ui, main);
            GD.Print(JsonSerializer.Serialize(new { test = "oldpc-desktop", checks = _checks, status = "pass",
                visual = DisplayServer.GetName() == "headless" ? "not-run" : "captured-if-requested" }));
            await GodotSmokeCleanup.ReleaseAsync(main);
            main = null;
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("oldpc-desktop-smoke: " + error);
            if (main is not null) await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(1);
        }
    }

    private async Task VerifyLinkedJournalPage(RuntimeBridge bridge, OldPcUi ui, bool saveFromBrowser, string captureName)
    {
        var source = bridge.OldPcDocuments.Single(document => document.Id == LinkedJournalSource);
        Check(source.BodyMarkdown.Contains("[Кара-Урман](doc:", StringComparison.Ordinal)
            && source.BodyMarkdown.Contains("[Домовые и семейные записи](doc:", StringComparison.Ordinal),
            "The actual read article contains authored internal links, so the paper check exercises real content.");
        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        if (saveFromBrowser)
        {
            Check(ui.BrowserDocumentId == LinkedJournalSource
                && bridge.JournalEntries().All(entry => entry.EntryId != LinkedJournalSource),
                "The browser reached this source through its real link without automatically recording it.");
            var save = ui.GetNode<Button>("Screen/App_browser/Layout/Content/Footer/Save");
            Check(save.IsVisibleInTree() && !save.Disabled, "The read article offers the visible manual В книжку action.");
            save.EmitSignal(BaseButton.SignalName.Pressed);
            var status = ui.GetNode<Label>("Screen/App_browser/Layout/Content/Footer/Status");
            for (var frame = 0; frame < 240
                && !status.Text.StartsWith("Документ добавлен в книжку", StringComparison.Ordinal); frame++) await Frames(1);
            Check(save.Disabled && status.Text.StartsWith("Документ добавлен в книжку", StringComparison.Ordinal),
                "The ordinary browser save action completed through the shared runtime.");
            ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
        }
        Check(bridge.JournalEntries().Count(entry => entry.EntryId == LinkedJournalSource) == 1,
            "The linked article has exactly one stored source entry, including after the existing slot reload.");
        var journal = GetTree().GetFirstNodeInGroup("journal_ui") as JournalUi
            ?? throw new InvalidOperationException("Missing existing paper journal.");
        journal.ApplyAccessibilitySettings(AccessibilitySettingsSnapshot.Default);
        journal._UnhandledInput(new InputEventAction { Action = "journal", Pressed = true });
        await Frames(3);
        journal.GetNode<TabBar>("Screen/Book/Layout/Tabs").CurrentTab = 0;
        var entries = journal.GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries");
        var row = Enumerable.Range(0, entries.ItemCount).Single(index =>
            entries.GetItemText(index).EndsWith(source.Title, StringComparison.Ordinal));
        entries.Select(row);
        entries.EmitSignal(ItemList.SignalName.ItemSelected, (long)row);
        await Frames(3);
        var body = journal.GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
        Check(journal.ActiveEntryId == LinkedJournalSource && body.IsVisibleInTree()
            && body.Text.Contains("Кара-Урман · Домовые и семейные записи", StringComparison.Ordinal)
            && !body.Text.Contains("doc:", StringComparison.Ordinal)
            && !body.Text.Contains("urman.oldpc:", StringComparison.Ordinal),
            "The selected paper source preserves both visible link labels without exposing machine destinations.");
        Check(knowledge == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
            "Recording and displaying the already read linked source creates no additional knowledge.");
        body.ScrollToLine(Math.Max(0, body.GetLineCount() - 1));
        await Capture(captureName);
        journal.GetNode<Button>("Screen/Book/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        Check(!journal.GetNode<Control>("Screen").Visible, "Closing the paper source releases its existing journal screen.");
    }

    private async Task VerifyPictureSourceLinks(RuntimeBridge bridge, OldPcUi ui, Main main)
    {
        const string pictureId = "urman.oldpc:document/social_family_photo";
        const string relatedId = "urman.oldpc:document/doc_household_photo_box_labels";
        const string originalId = "urman.chapter1:document/arrival-photo-evidence";
        Check(!bridge.IsOldPcDocumentAccessible(pictureId),
            "The personal photograph remains gated before reading its original arrival source.");
        var before = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        ui.Open(bridge);
        ui.NavigateBrowser("doc:" + pictureId);
        await Frames(4);
        Check(ui.BrowserDocumentId is null && ui.BrowserAddress == "about:blank",
            "A typed photograph URL cannot reveal the unread personal source.");
        ui.LaunchApplication("pictures");
        var window = ui.GetNode<Control>("Screen/App_pictures");
        var pictures = (ItemList)window.FindChild("Pictures", true, false);
        Check(!Enumerable.Range(0, pictures.ItemCount).Any(index => pictures.GetItemMetadata(index).AsString() == pictureId)
            && before == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText(),
            "Browsing the picture list neither exposes the gated photograph nor grants its memory.");
        ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);

        // Use the existing arrival source/reader as in Act1ArrivalFlowProof. This
        // bounded desktop test checks reachable narrative state, not the walk to it.
        var original = main.FindChildren("ArrivalPhotoTarget", nameof(StaticBody3D), true, false)
            .OfType<InteractionTarget>().Single();
        Check(original.IsAvailable() && original.DocumentId == originalId,
            "The existing arrival photograph is the available source of the personal memory.");
        var documentUi = (DocumentUi)GetTree().GetFirstNodeInGroup("document_ui");
        original.Interact();
        for (var frame = 0; frame < 180 && !documentUi.IsOpen; frame++) await Frames(1);
        Check(documentUi.IsOpen && documentUi.OpenDocumentId == originalId
            && bridge.IsOldPcDocumentAccessible(pictureId),
            "Reading the actual arrival document unlocks its saved photograph through ordinary source effects.");
        documentUi._UnhandledInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await Frames(3);
        Check(!documentUi.IsOpen, "The original photograph reader closes before opening the desktop.");

        var knowledge = bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var vocabulary = bridge.SelectRuntimeState().GetProperty("vocabulary").GetRawText();
        var journal = JsonSerializer.Serialize(bridge.JournalEntries());
        ui.Open(bridge);
        ui.LaunchApplication("pictures");
        SelectRow(pictures, pictureId);
        pictures.EmitSignal(ItemList.SignalName.ItemSelected, (long)pictures.GetSelectedItems().Single());
        var source = (RichTextLabel)window.FindChild("Source", true, false);
        var reader = source.GetParent().GetNode<DocumentImageReader>("DocumentImages");
        for (var frame = 0; frame < 180 && !reader.ShowingImage; frame++) await Frames(1);
        Check(reader.ShowingImage && reader.DisplayedTexture is not null
            && reader.ActiveAssetId == "urman.chapter1:asset/photo-marat-childhood",
            "The picture application displays the real saved family photograph.");
        await Capture("07_picture_source");
        reader.GetNode<Button>("Tabs/Text").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        var route = "doc:" + relatedId;
        Check(source.IsVisibleInTree() && source.BbcodeEnabled && source.HasFocus()
            && source.Text.Contains("[url=" + route + "][u]Коробки с фотографиями[/u][/url]", StringComparison.Ordinal)
            && !source.GetParsedText().Contains("doc:", StringComparison.Ordinal)
            && !source.GetParsedText().Contains("urman.oldpc:", StringComparison.Ordinal),
            "The picture's Text tab preserves the visible source link without exposing its technical destination.");
        source.ScrollToLine(Math.Max(0, source.GetLineCount() - 1));
        await Capture("07b_picture_source_text");
        source.EmitSignal(RichTextLabel.SignalName.MetaClicked, Variant.From(route));
        for (var frame = 0; frame < 180 && ui.BrowserDocumentId != relatedId; frame++) await Frames(1);
        Check(ui.ActiveApplicationId == "browser" && ui.BrowserDocumentId == relatedId
            && ui.GetNode<RichTextLabel>("Screen/App_browser/Layout/Content/Page").GetParsedText()
                .Contains("Антресоль: три коробки", StringComparison.Ordinal),
            "The actual picture-source link opens its existing related document in the browser.");
        Check(knowledge == bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText()
            && vocabulary == bridge.SelectRuntimeState().GetProperty("vocabulary").GetRawText()
            && journal == JsonSerializer.Serialize(bridge.JournalEntries())
            && !bridge.IsOldPcDocumentAccessible(LockedSource),
            "Viewing the copy and following its household link grants no extra knowledge, words, journal entries or locked investigation access.");
        ui.GetNode<Button>("Screen/Computer/Layout/Header/Close").EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
    }

    private static void SelectRow(ItemList list, string metadata)
    {
        var row = Enumerable.Range(0, list.ItemCount).FirstOrDefault(index =>
            list.GetItemMetadata(index).AsString() == metadata, -1);
        if (row < 0) throw new InvalidOperationException("Missing file row: " + metadata);
        list.Select(row);
    }

    private static string SocialText(Control social) => string.Join("\n", social.FindChildren("*", "Control", true, false)
        .Select(node => node switch { RichTextLabel rich => rich.GetParsedText(), Label label => label.Text, _ => "" }));

    private static void ClickSocialLink(Control social, string documentId)
    {
        var route = "doc:" + documentId;
        SocialLink(social, documentId).EmitSignal(RichTextLabel.SignalName.MetaClicked, Variant.From(route));
    }

    private static RichTextLabel SocialLink(Control social, string documentId)
    {
        var route = "doc:" + documentId;
        var source = social.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
            .FirstOrDefault(text => text.Text.Contains("[url=" + route + "]", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Missing visible social link: " + documentId);
        return source;
    }

    private void CheckInsideScreen(OldPcUi ui, Control control, string description) =>
        Check(ui.GetNode<Control>("Screen").GetGlobalRect().Grow(1).Encloses(control.GetGlobalRect()), description);

    private void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        _checks.Add(description);
    }

    private async Task Frames(int count)
    {
        for (var index = 0; index < count; index++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Capture(string name)
    {
        var directory = OS.GetEnvironment("URMAN_OLDPC_DESKTOP_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless") return;
        if (!System.IO.Path.IsPathFullyQualified(directory) || !System.IO.Directory.Exists(directory))
            throw new InvalidOperationException("Capture directory must already exist and be absolute.");
        await Frames(3);
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "oldpc/" + name);
        var destination = System.IO.Path.Combine(directory, name + ".png");
        if (System.IO.File.Exists(destination)) throw new InvalidOperationException("Historical capture already exists: " + destination);
        using var picture = GetViewport().GetTexture().GetImage();
        if (picture.SavePng(destination) != Error.Ok) throw new InvalidOperationException("Could not save desktop frame.");
        GD.Print("oldpc-desktop-capture: " + destination);
    }
}
