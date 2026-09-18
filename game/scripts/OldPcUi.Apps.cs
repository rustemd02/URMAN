using Godot;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Urman.Core.Capabilities.OldPc;

namespace Urman.Godot;

public partial class OldPcUi
{
    private ItemList _fileList = null!;
    private ItemList _trashList = null!;
    private Label _folderLabel = null!;
    private Label _fileStatus = null!;
    private readonly Dictionary<string, TextEdit> _editors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LineEdit> _editorNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _editorStatuses = new(StringComparer.Ordinal);
    private RichTextLabel _writerPreview = null!;
    private LineEdit _browserAddress = null!;
    private RichTextLabel _browserReader = null!;
    private Label _browserTitle = null!;
    private Label _browserStatus = null!;
    private Button _browserSave = null!;
    private Button _browserBack = null!;
    private Button _browserForward = null!;
    private DocumentImageReader _browserImages = null!;
    private SourceExcerptSelection _browserExcerpts = null!;
    private string? _browserHistoryAddress;
    private string? _browserDocumentId;
    private long _browserVersion;
    private ItemList _pictureList = null!;
    private RichTextLabel _pictureText = null!;
    private DocumentImageReader _pictureReader = null!;
    private long _pictureVersion;
    private bool _settingEditor;

    public string? BrowserDocumentId => _browserDocumentId;
    public string BrowserAddress => _browserAddress?.Text ?? string.Empty;
    public IReadOnlyList<OldPcNoteSnapshot> PersonalFiles => _desktop.Files;

    private void BuildDesktopApplications()
    {
        BuildFiles();
        BuildEditor(false);
        BuildEditor(true);
        BuildBrowser();
        BuildPictures();
    }

    private void BuildFiles()
    {
        var body = CreateDesktopWindow("files", "Мой компьютер", out _);
        var tools = new HBoxContainer { Name = "Tools" };
        body.AddChild(tools);
        DesktopButton(tools, "Up", "↑", () => { _desktop.Folder = "computer"; RefreshFileList(false); MarkDesktopChanged(); });
        DesktopButton(tools, "NewText", "Создать заметку", () => NewPersonalFile(false));
        DesktopButton(tools, "Open", "Открыть", () => OpenFileSelection(false));
        DesktopButton(tools, "Delete", "В корзину", DeleteSelectedNote);
        _folderLabel = new Label { Name = "Folder", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_folderLabel);
        _fileList = new ItemList { Name = "Files", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(_fileList);
        _fileList.ItemActivated += _ => OpenFileSelection(false);
        _fileStatus = new Label { Name = "Status", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_fileStatus);
        var trash = CreateDesktopWindow("trash", "Корзина · Чүплек", out _);
        trash.AddChild(new Label { Text = "Удалённые личные заметки. Их можно восстановить." });
        _trashList = new ItemList { Name = "Files", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        trash.AddChild(_trashList);
        DesktopButton(trash, "Restore", "Восстановить выбранное", RestoreSelectedNote);
    }

    private void RefreshFileList(bool trash)
    {
        if (_bridge is null) return;
        var list = trash ? _trashList : _fileList;
        if (list is null) return;
        list.Clear();
        if (trash)
        {
            foreach (var note in _desktop.Files.Where(note => note.Deleted))
                AddFileRow(list, note.Title, "note:" + note.Id);
            return;
        }
        var folder = _desktop.Folder;
        _folderLabel.Text = folder == "computer" ? "C:\\Мои документы" : "C:\\Мои документы\\" + SectionLabel(folder);
        if (folder == "computer")
        {
            AddFileRow(list, "▸ Заметки Айдара", "folder:notes");
            AddFileRow(list, "▸ Фотографии", "folder:pictures");
            foreach (var section in _bridge.OldPcDocuments.Where(document => _bridge.IsOldPcDocumentAccessible(document.Id))
                         .Select(document => document.Section).Distinct().OrderBy(SectionLabel, StringComparer.CurrentCulture))
                AddFileRow(list, "▸ " + SectionLabel(section), "folder:" + section);
            _fileStatus.Text = "Дважды нажмите папку или выделите её и выберите «Открыть».";
        }
        else if (folder == "notes")
        {
            foreach (var note in _desktop.Files.Where(note => !note.Deleted))
                AddFileRow(list, note.Title, "note:" + note.Id);
            _fileStatus.Text = list.ItemCount == 0 ? "Здесь будут личные заметки Айдара." : "Личные файлы можно редактировать или восстановить из корзины.";
        }
        else
        {
            foreach (var document in _bridge.OldPcDocuments.Where(document =>
                         _bridge.IsOldPcDocumentAccessible(document.Id)
                         && (folder == "pictures" ? document.Images is { Count: > 0 } : document.Section == folder)))
                AddFileRow(list, document.Title, "doc:" + document.Id);
            _fileStatus.Text = "Архивные источники открываются для чтения; выписки сохраняются в книжку.";
        }
    }

    private static void AddFileRow(ItemList list, string title, string value)
    {
        var row = list.AddItem(title);
        list.SetItemMetadata(row, value);
    }

    private static string? SelectedFile(ItemList list)
    {
        var selected = list.GetSelectedItems();
        return selected.Length == 0 ? null : list.GetItemMetadata(selected[0]).AsString();
    }

    private void OpenFileSelection(bool trash)
    {
        var selected = SelectedFile(trash ? _trashList : _fileList);
        if (selected is null) return;
        if (selected.StartsWith("folder:", StringComparison.Ordinal))
        {
            _desktop.Folder = selected[7..];
            RefreshFileList(false);
            MarkDesktopChanged();
        }
        else if (selected.StartsWith("note:", StringComparison.Ordinal))
        {
            var note = _desktop.Files.FirstOrDefault(item => item.Id == selected[5..] && !item.Deleted);
            if (note is null) return;
            if (note.RichText) _desktop.WriterId = note.Id; else _desktop.NoteId = note.Id;
            LaunchApplication(note.RichText ? "writer" : "notepad");
        }
        else if (selected.StartsWith("doc:", StringComparison.Ordinal)) OpenArchiveSource(selected[4..]);
    }

    public void OpenArchiveSource(string documentId)
    {
        if (_bridge is null || !_bridge.IsOldPcDocumentAccessible(documentId)) return;
        LaunchApplication("archive");
        _query.Text = string.Empty;
        RefreshResults();
        var index = Enumerable.Range(0, _results.ItemCount)
            .FirstOrDefault(index => _results.GetItemMetadata(index).AsString() == documentId, -1);
        if (index >= 0) OpenDocument(index);
    }

    private void DeleteSelectedNote()
    {
        var selected = SelectedFile(_fileList);
        if (selected is null || !selected.StartsWith("note:", StringComparison.Ordinal))
        {
            _fileStatus.Text = "В корзину можно отправить собственную заметку из папки «Заметки Айдара».";
            return;
        }
        var note = _desktop.Files.FirstOrDefault(item => item.Id == selected[5..]);
        if (note is null) return;
        note.Deleted = true;
        if (_desktop.NoteId == note.Id) _desktop.NoteId = null;
        if (_desktop.WriterId == note.Id) _desktop.WriterId = null;
        RestoreEditor(false);
        RestoreEditor(true);
        RefreshFileList(false);
        RefreshFileList(true);
        MarkDesktopChanged();
    }

    private void RestoreSelectedNote()
    {
        var selected = SelectedFile(_trashList);
        if (selected is null) return;
        var note = _desktop.Files.FirstOrDefault(item => "note:" + item.Id == selected);
        if (note is null) return;
        note.Deleted = false;
        RefreshFileList(false);
        RefreshFileList(true);
        MarkDesktopChanged();
    }

    private void BuildEditor(bool rich)
    {
        var app = rich ? "writer" : "notepad";
        var body = CreateDesktopWindow(app, rich ? "Текстовый редактор" : "Блокнот", out _);
        var row = new HBoxContainer { Name = "Tools" };
        body.AddChild(row);
        DesktopButton(row, "New", "Новый", () => NewPersonalFile(rich));
        DesktopButton(row, "Save", "Сохранить", () => { UpdatePersonalFile(rich); PersistDesktop(); });
        _editorNames[app] = new LineEdit { Name = "Filename", PlaceholderText = "Имя файла",
            MaxLength = 96, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(_editorNames[app]);
        if (rich)
        {
            var format = new HBoxContainer { Name = "Format" };
            body.AddChild(format);
            DesktopButton(format, "Bold", "Ж", () => FormatSelection("b"));
            DesktopButton(format, "Italic", "К", () => FormatSelection("i"));
            DesktopButton(format, "Heading", "Заголовок", () => FormatSelection("font_size=26", "font_size"));
            DesktopButton(format, "Preview", "Лист / редактор", () =>
            {
                _writerPreview.Visible = !_writerPreview.Visible;
                _editors["writer"].Visible = !_writerPreview.Visible;
                _writerPreview.Text = _editors["writer"].Text;
            });
        }
        var edit = new TextEdit { Name = "Text", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary, SelectingEnabled = true,
            ContextMenuEnabled = true, ScrollFitContentWidth = false };
        _editors[app] = edit;
        body.AddChild(edit);
        if (rich)
        {
            _writerPreview = new RichTextLabel { Name = "Page", BbcodeEnabled = true, Visible = false,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollActive = true,
                FocusMode = Control.FocusModeEnum.All };
            body.AddChild(_writerPreview);
        }
        _editorStatuses[app] = new Label { Name = "Status", Text = "Личная заметка Айдара; архивные источники остаются отдельными.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_editorStatuses[app]);
        edit.TextChanged += () => UpdatePersonalFile(rich);
        _editorNames[app].TextChanged += _ => UpdatePersonalFile(rich);
        edit.GuiInput += input =>
        {
            if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.S } key
                && (key.CtrlPressed || key.MetaPressed))
            {
                UpdatePersonalFile(rich);
                PersistDesktop();
                edit.AcceptEvent();
            }
        };
    }

    public void NewPersonalFile(bool rich)
    {
        if (_desktop.Files.Count >= 32)
        {
            _editorStatuses[rich ? "writer" : "notepad"].Text = "В папке уже 32 заметки. Можно продолжить существующую.";
            return;
        }
        var note = new OldPcNoteSnapshot { Id = "note-" + Guid.NewGuid().ToString("N"),
            Title = "Заметка " + (_desktop.Files.Count + 1) + (rich ? ".rtf" : ".txt"), RichText = rich };
        _desktop.Files.Add(note);
        if (rich) _desktop.WriterId = note.Id; else _desktop.NoteId = note.Id;
        LaunchApplication(rich ? "writer" : "notepad");
        RestoreEditor(rich);
        RefreshFileList(false);
        MarkDesktopChanged();
        _editors[rich ? "writer" : "notepad"].GrabFocus();
    }

    private void RestoreEditor(bool rich)
    {
        var app = rich ? "writer" : "notepad";
        if (!_editors.TryGetValue(app, out var edit)) return;
        var id = rich ? _desktop.WriterId : _desktop.NoteId;
        var note = _desktop.Files.FirstOrDefault(item => item.Id == id && !item.Deleted);
        _settingEditor = true;
        edit.Text = note?.Text ?? string.Empty;
        edit.Editable = note is not null;
        _editorNames[app].Text = note?.Title ?? string.Empty;
        _editorNames[app].Editable = note is not null;
        _editorStatuses[app].Text = note is null ? "Нажмите «Новый», чтобы начать свою заметку." : "Заметка хранится на компьютере.";
        if (rich) _writerPreview.Text = edit.Text;
        _settingEditor = false;
    }

    private void UpdatePersonalFile(bool rich)
    {
        if (_settingEditor) return;
        var app = rich ? "writer" : "notepad";
        var id = rich ? _desktop.WriterId : _desktop.NoteId;
        var note = _desktop.Files.FirstOrDefault(item => item.Id == id && !item.Deleted);
        if (note is null) return;
        var text = _editors[app].Text;
        if (text.Length > 32768)
        {
            _editorStatuses[app].Text = "Заметка слишком длинная: максимум 32 768 знаков. Сократите текст, чтобы сохранить.";
            return;
        }
        note.Text = text;
        var name = _editorNames[app].Text.Trim();
        if (name.Length > 0) note.Title = name;
        _editorStatuses[app].Text = "Изменения записаны на компьютере.";
        if (rich) _writerPreview.Text = text;
        MarkDesktopChanged();
    }

    private void FormatSelection(string open, string? close = null)
    {
        var editor = _editors["writer"];
        if (!editor.Editable) return;
        var selected = editor.GetSelectedText();
        editor.InsertTextAtCaret("[" + open + "]" + selected + "[/" + (close ?? open) + "]");
        editor.GrabFocus();
    }

    private void BuildBrowser()
    {
        var body = CreateDesktopWindow("browser", "Интернет", out _);
        var navigation = new HBoxContainer { Name = "Navigation" };
        body.AddChild(navigation);
        _browserBack = DesktopButton(navigation, "Back", "←", () => BrowseHistory(-1));
        _browserForward = DesktopButton(navigation, "Forward", "→", () => BrowseHistory(1));
        DesktopButton(navigation, "Home", "Домой", () => ShowBrowserAddress("home"));
        _browserAddress = new LineEdit { Name = "Address", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "tatwiki.local", MaxLength = 192 };
        navigation.AddChild(_browserAddress);
        DesktopButton(navigation, "Go", "Перейти", () => ShowBrowserAddress(_browserAddress.Text));
        _browserAddress.TextSubmitted += text => ShowBrowserAddress(text);
        var favorites = new HFlowContainer { Name = "Favorites" };
        body.AddChild(favorites);
        foreach (var (route, label) in new[] {
            ("tatwiki", "TatWiki"), ("yalkyn", "Ялкын"), ("village", "Сайт авыла"), ("mail", "Почта") })
        {
            var address = route;
            DesktopButton(favorites, "Site_" + route, label, () => ShowBrowserAddress(address));
        }
        DesktopButton(favorites, "History", "История", ShowBrowserHistory);
        _browserTitle = new Label { Name = "PageTitle", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _browserTitle.AddThemeFontSizeOverride("font_size", 22);
        body.AddChild(_browserTitle);
        _browserReader = new RichTextLabel { Name = "Page", BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollActive = true,
            SelectionEnabled = true, FocusMode = Control.FocusModeEnum.All };
        body.AddChild(_browserReader);
        _browserReader.MetaClicked += meta => ShowBrowserAddress(meta.AsString());
        _browserImages = DocumentImageReader.Attach(_browserReader);
        BuildSocialBrowser(body);
        var footer = new HBoxContainer { Name = "Footer" };
        body.AddChild(footer);
        _browserStatus = new Label { Name = "Status", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_browserStatus);
        _browserSave = DesktopButton(footer, "Save", "В книжку", SaveBrowserDocument);
        _browserExcerpts = SourceExcerptSelection.Attach(_browserReader, _browserSave);
    }

    public void NavigateBrowser(string address)
    {
        LaunchApplication("browser");
        ShowBrowserAddress(address);
    }

    private string CurrentBrowserAddress() => _desktop.BrowserIndex >= 0
        && _desktop.BrowserIndex < _desktop.BrowserHistory.Count
        ? _desktop.BrowserHistory[_desktop.BrowserIndex] : "home";

    private string NormalizeBrowserAddress(string address)
    {
        var route = address.Trim();
        if (route.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) route = route[7..];
        else if (route.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) route = route[8..];
        route = route.TrimEnd('/');
        // URLs are a readable projection. History, documents and gates retain
        // their stable content IDs, including when a street/title is renamed.
        if (_bridge is not null)
        {
            var source = _bridge.OldPcDocuments.FirstOrDefault(document =>
                string.Equals(DocumentBrowserAddress(document), route, StringComparison.OrdinalIgnoreCase));
            if (source is not null) return "doc:" + source.Id;
        }
        return route.ToLowerInvariant() switch
        {
            "" or "home" or "about:home" => "home",
            "tatwiki" or "tatwiki.local" => "tatwiki",
            "yalkyn" or "yalkyn.local" => "yalkyn",
            "village" or "kara-urman.local" => "village",
            "mail" or "mail.local" => "mail",
            _ => route
        };
    }

    private string DisplayBrowserAddress(string address)
    {
        if (address.StartsWith("doc:", StringComparison.Ordinal))
        {
            var document = _bridge?.OldPcDocuments.FirstOrDefault(item => item.Id == address[4..]);
            return document is not null && _bridge!.IsOldPcDocumentAccessible(document.Id)
                ? DocumentBrowserAddress(document) : "about:blank";
        }
        return address switch
        {
            "home" => "about:home", "tatwiki" => "tatwiki.local",
            "yalkyn" => "yalkyn.local", "village" => "kara-urman.local", "mail" => "mail.local", _ => address
        };
    }

    private static string DocumentBrowserAddress(OldPcDocumentContent document)
    {
        var host = document.SearchTerms.Contains("site:mail") ? "mail.local"
            : document.SearchTerms.Contains("site:village") ? "kara-urman.local"
            : document.Section == "tatarwiki" ? "tatwiki.local"
            : document.Section == "saved_messages" ? "yalkyn.local" : "archive.local";
        var title = document.Title;
        var separator = title.IndexOf(':');
        if (separator >= 0) title = title[(separator + 1)..];
        var page = Regex.Replace(title.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');
        return host + "/" + page;
    }

    private async void ShowBrowserAddress(string address, bool addHistory = true, bool restoreOnly = false)
    {
        if (_bridge is not { } bridge) return;
        var session = bridge.SessionIdentity;
        var version = ++_browserVersion;
        address = NormalizeBrowserAddress(address);
        _browserExcerpts.Clear();
        _browserImages.SetImages(null);
        ResetSocialPage();
        _browserDocumentId = null;
        _browserSave.Disabled = true;
        _browserAddress.Text = DisplayBrowserAddress(address);
        var document = address.StartsWith("doc:", StringComparison.Ordinal)
            ? bridge.OldPcDocuments.FirstOrDefault(document => document.Id == address[4..]) : null;
        if (document is not null)
        {
            if (!bridge.IsOldPcDocumentAccessible(document.Id))
            {
                BrowserUnavailable("Эта страница пока недоступна. Можно вернуться назад или открыть другую закладку.");
                return;
            }
            if (restoreOnly)
            {
                // Restoring a desktop is not a reading action. Only a previously
                // opened source may be projected; no openEffects are replayed.
                var opened = bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
                    .EnumerateArray().Any(id => id.GetString() == document.Id);
                if (!opened) { RenderBrowserIndex("home"); return; }
            }
            else
            {
                try
                {
                    await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId = document.Id }));
                }
                catch (Exception error)
                {
                    if (BrowserRequestCurrent(bridge, session, version)) BrowserUnavailable(error.Message);
                    return;
                }
                if (!BrowserRequestCurrent(bridge, session, version)) return;
            }
            // Resolve after the source's effects, using the shared content and
            // address projection rather than a copied source body.
            document = bridge.OldPcDocuments.Single(item => item.Id == document.Id);
            _browserDocumentId = document.Id;
            _browserTitle.Text = document.Title;
            _browserReader.Text = RenderLinkedSource(document.BodyMarkdown);
            var social = RenderSocialDocument(document);
            _browserImages.SetImages(social ? null : document.Images);
            _browserReader.Visible = !social;
            _browserExcerpts.Bind(bridge, document.Id);
            _browserSave.Disabled = false;
            _browserStatus.Text = "Страница открыта. Ссылки ведут к связанным материалам.";
            if (!restoreOnly && !_desktop.VisitedDocumentIds.Contains(document.Id))
                _desktop.VisitedDocumentIds.Add(document.Id);
            RefreshResults();
        }
        else if (address is "home" or "tatwiki" or "yalkyn" or "village" or "mail")
            RenderBrowserIndex(address);
        else
        {
            BrowserUnavailable("Адрес не найден. Проверьте написание или выберите сохранённый сайт.");
            return;
        }
        _browserHistoryAddress = address;
        if (addHistory)
        {
            if (_desktop.BrowserIndex + 1 < _desktop.BrowserHistory.Count)
                _desktop.BrowserHistory.RemoveRange(_desktop.BrowserIndex + 1,
                    _desktop.BrowserHistory.Count - _desktop.BrowserIndex - 1);
            if (_desktop.BrowserHistory.Count == 0 || _desktop.BrowserHistory[^1] != address)
                _desktop.BrowserHistory.Add(address);
            if (_desktop.BrowserHistory.Count > 64) _desktop.BrowserHistory.RemoveAt(0);
            _desktop.BrowserIndex = _desktop.BrowserHistory.Count - 1;
        }
        _browserBack.Disabled = _desktop.BrowserIndex <= 0;
        _browserForward.Disabled = _desktop.BrowserIndex >= _desktop.BrowserHistory.Count - 1;
        MarkDesktopChanged();
    }

    private bool BrowserRequestCurrent(RuntimeBridge bridge, object? session, long version) =>
        IsInsideTree() && _screen.Visible && _bridge == bridge && _browserVersion == version
        && ReferenceEquals(session, bridge.SessionIdentity);

    private void BrowserUnavailable(string message)
    {
        ResetSocialPage();
        _browserHistoryAddress = null;
        _browserTitle.Text = "Страница недоступна";
        _browserReader.Text = EscapeBbCode(message) + "\n\n[url=home][u]Домашняя страница[/u][/url]";
        _browserStatus.Text = "Другие программы и архив доступны.";
    }

    private void RenderBrowserIndex(string address)
    {
        ResetSocialPage();
        _browserReader.Visible = true;
        _browserTitle.Text = address switch
        {
            "tatwiki" => "TatWiki · Татарвики", "yalkyn" => "Ялкын · Люди рядом",
            "village" => "Кара-Урман · сайт авыла", "mail" => "Почта · сохранённые письма",
            _ => "Домашняя страница Мансура"
        };
        var builder = new StringBuilder(address switch
        {
            "tatwiki" => "Энциклопедия местной памяти. Названия, люди и рассказы: у каждой записи свой источник.\n\n",
            "yalkyn" => "Профили, старые сообщества и фотографии. Здесь остались разговоры, которые не помещаются в справку.\n\n",
            "village" => "Исәнмесез! Новости, гостевая книга и старые страницы нашего авыла.\n\n",
            "mail" => "Письма и черновики, сохранённые на этом компьютере.\n\n",
            _ => "[url=tatwiki][u]TatWiki[/u][/url]     [url=yalkyn][u]Ялкын[/u][/url]     [url=village][u]Сайт авыла[/u][/url]     [url=mail][u]Почта[/u][/url]\n\nИзбранное\n\n"
        });
        if (_bridge is not null)
        {
            foreach (var document in _bridge.OldPcDocuments.Where(document => _bridge.IsOldPcDocumentAccessible(document.Id)
                && (address switch
                {
                    "tatwiki" => document.Section == "tatarwiki",
                    "yalkyn" => document.SearchTerms.Contains("site:yalkyn") || document.Section == "saved_messages"
                        && !document.SearchTerms.Contains("site:mail"),
                    "village" => document.SearchTerms.Contains("site:village"),
                    "mail" => document.SearchTerms.Contains("site:mail"),
                    _ => document.SearchTerms.Contains("site:home")
                })))
            {
                builder.Append("[url=doc:").Append(document.Id).Append("][u]").Append(EscapeBbCode(document.Title))
                    .Append("[/u][/url]\n\n");
            }
        }
        _browserReader.Text = builder.ToString();
        if (address == "yalkyn") RenderSocialIndex();
        _browserStatus.Text = address == "village" ? "Сохранённая копия · архив страниц 2010–2026" : "Готово";
    }

    private void ShowBrowserHistory()
    {
        ResetSocialPage();
        _browserVersion++;
        _browserExcerpts.Clear();
        _browserImages.SetImages(null);
        _browserDocumentId = null;
        _browserSave.Disabled = true;
        _browserTitle.Text = "История посещений";
        var builder = new StringBuilder();
        foreach (var route in _desktop.BrowserHistory.AsEnumerable().Reverse().Distinct())
        {
            if (route.StartsWith("doc:", StringComparison.Ordinal))
            {
                var document = _bridge?.OldPcDocuments.FirstOrDefault(item => item.Id == route[4..]);
                if (document is null || !_bridge!.IsOldPcDocumentAccessible(document.Id)) continue;
                builder.Append("[url=").Append(route).Append("][u]").Append(EscapeBbCode(document.Title)).Append("[/u][/url]\n\n");
            }
            else builder.Append("[url=").Append(route).Append("][u]").Append(DisplayBrowserAddress(route)).Append("[/u][/url]\n\n");
        }
        _browserReader.Text = builder.Length == 0 ? "Вы ещё не открывали страницы." : builder.ToString();
    }

    private void BrowseHistory(int direction)
    {
        var index = _desktop.BrowserIndex + direction;
        if (index < 0 || index >= _desktop.BrowserHistory.Count) return;
        _desktop.BrowserIndex = index;
        ShowBrowserAddress(_desktop.BrowserHistory[index], false);
    }

    private async void SaveBrowserDocument()
    {
        if (_bridge is not { } bridge || _browserDocumentId is not { } documentId) return;
        var version = _browserVersion;
        var session = bridge.SessionIdentity;
        _browserSave.Disabled = true;
        try
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "save", documentId }));
            if (BrowserRequestCurrent(bridge, session, version))
                _browserStatus.Text = $"Документ добавлен в книжку · Откройте журнал [{JournalShortcutLabel()}]";
        }
        catch (Exception error)
        {
            if (!BrowserRequestCurrent(bridge, session, version)) return;
            _browserStatus.Text = error.Message;
            _browserSave.Disabled = false;
        }
    }

    private string RenderLinkedSource(string markdown)
    {
        var plain = SourceExcerptSelection.FormatSourceText(markdown);
        var output = new StringBuilder();
        var offset = 0;
        foreach (Match match in Regex.Matches(plain, @"\[([^\]]+)\]\((doc:[^\s)]+|home|tatwiki|yalkyn|village|mail)\)"))
        {
            output.Append(EscapeBbCode(plain[offset..match.Index]));
            var route = match.Groups[2].Value;
            var allowed = !route.StartsWith("doc:", StringComparison.Ordinal)
                || _bridge is not null && _bridge.OldPcDocuments.Any(document => document.Id == route[4..]
                    && _bridge.IsOldPcDocumentAccessible(document.Id));
            if (allowed)
                output.Append("[url=").Append(route).Append("][u]").Append(EscapeBbCode(match.Groups[1].Value)).Append("[/u][/url]");
            else output.Append("Раздел пока недоступен");
            offset = match.Index + match.Length;
        }
        output.Append(EscapeBbCode(plain[offset..]));
        return output.ToString();
    }

    private static string EscapeBbCode(string text) => text.Replace("[", "[lb]", StringComparison.Ordinal);

    private void BuildPictures()
    {
        var body = CreateDesktopWindow("pictures", "Просмотр изображений", out _);
        var layout = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(layout);
        _pictureList = new ItemList { Name = "Pictures", CustomMinimumSize = new Vector2(200, 0) };
        layout.AddChild(_pictureList);
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        layout.AddChild(right);
        _pictureText = new RichTextLabel { Name = "Source", SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            BbcodeEnabled = true, SelectionEnabled = true,
            Text = "Выберите фотографию. Можно приблизить изображение и прочитать подпись." };
        right.AddChild(_pictureText);
        _pictureText.MetaClicked += meta => NavigateBrowser(meta.AsString());
        _pictureReader = DocumentImageReader.Attach(_pictureText);
        _pictureList.ItemSelected += SelectPicture;
    }

    private void RefreshPictures()
    {
        if (_bridge is null || _pictureList is null) return;
        _pictureList.Clear();
        _pictureReader.SetImages(null);
        _pictureText.Text = "Выберите фотографию. Можно приблизить изображение и прочитать подпись.";
        foreach (var document in _bridge.OldPcDocuments.Where(document => document.Images is { Count: > 0 }
            && _bridge.IsOldPcDocumentAccessible(document.Id)))
            AddFileRow(_pictureList, document.Title, document.Id);
    }

    private async void SelectPicture(long index)
    {
        if (_bridge is not { } bridge || index < 0 || index >= _pictureList.ItemCount) return;
        var id = _pictureList.GetItemMetadata((int)index).AsString();
        var version = ++_pictureVersion;
        var session = bridge.SessionIdentity;
        try
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId = id }));
            if (_bridge != bridge || !_screen.Visible || version != _pictureVersion
                || !ReferenceEquals(session, bridge.SessionIdentity)) return;
            var document = bridge.OldPcDocuments.Single(document => document.Id == id);
            _pictureText.Text = RenderLinkedSource(document.BodyMarkdown);
            _pictureReader.SetImages(document.Images);
            _pictureReader.ShowPicture(true);
            RefreshResults();
        }
        catch (Exception error) { if (version == _pictureVersion) _pictureText.Text = error.Message; }
    }

    private static string SectionLabel(string section) => section switch
    {
        "archive_search" => "Архивный поиск", "documents_marat" => "Документы Марата",
        "tatarwiki" => "Татарвики", "saved_messages" => "Сообщения и письма",
        "internal_accounting" => "Внутренний учёт", "household_registry" => "Дома и семьи",
        "violations_compensation" => "Нарушения и компенсации", "kara_urman" => "Лес",
        "damaged_hidden" => "Повреждённые файлы", "household_misc" => "Хозяйство и фотографии",
        "notes" => "Заметки Айдара", "pictures" => "Фотографии", "trash" => "Корзина",
        _ => section
    };
}
