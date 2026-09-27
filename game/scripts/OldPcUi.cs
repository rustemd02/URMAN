using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class OldPcUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Control _computer = null!;
    private LineEdit _query = null!;
    private ItemList _results = null!;
    private Label _title = null!;
    private Label _documentTitle = null!;
    private RichTextLabel _reader = null!;
    private DocumentImageReader _images = null!;
    private Label _status = null!;
    private Button _save = null!;
    private SourceExcerptSelection _excerpts = null!;
    private RuntimeBridge? _bridge;
    private string? _activeDocumentId;
    private long _readerVersion;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public string StatusText => _status?.Text ?? string.Empty;

    /// <summary>Test-visible id of the document currently open in the reader.</summary>
    public string? ActiveDocumentId => _activeDocumentId;

    public override void _Ready()
    {
        AddToGroup("old_pc_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _computer = GetNode<Control>("Screen/Computer");
        _title = GetNode<Label>("Screen/Computer/Layout/Header/Title");
        _query = GetNode<LineEdit>("Screen/Computer/Layout/SearchRow/Query");
        _results = GetNode<ItemList>("Screen/Computer/Layout/WorkArea/Results");
        _documentTitle = GetNode<Label>("Screen/Computer/Layout/WorkArea/ReaderArea/DocumentTitle");
        _reader = GetNode<RichTextLabel>("Screen/Computer/Layout/WorkArea/ReaderArea/Reader");
        _reader.MetaClicked += meta =>
        {
            if (HandleSearchTermMeta(meta.AsString())) return;
            NavigateBrowser(meta.AsString());
        };
        _images = DocumentImageReader.Attach(_reader);
        _status = GetNode<Label>("Screen/Computer/Layout/Footer/Status");
        _save = GetNode<Button>("Screen/Computer/Layout/Footer/Save");
        _excerpts = SourceExcerptSelection.Attach(_reader, _save);
        var close = GetNode<Button>("Screen/Computer/Layout/Header/Close");
        var closeLabel = close.Text;
        _excerpts.SelectionModeChanged += selecting => close.Text = selecting ? "Закрыть" : closeLabel;
        close.Pressed += Close;
        close.Pressed += () => PlayFoley("ui_click");
        GetNode<Button>("Screen/Computer/Layout/SearchRow/Search").Pressed += Search;
        GetNode<Button>("Screen/Computer/Layout/SearchRow/Search").Pressed += () => PlayFoley("keyboard_key");
        _query.TextSubmitted += _ => Search();
        _results.ItemSelected += OpenDocument;
        _save.Pressed += SaveDocument;
        _save.Pressed += () => PlayFoley("paper_open");
        // AUDIO-010: presentation-only interaction foley on the SFX bus.
        _foley = UiFoley.Attach(this);
        BuildDesktop();
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
    }

    private AudioStreamPlayer? _foley;

    private void PlayFoley(string sample) => UiFoley.Play(_foley, sample);

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        // ACT1-UI.5: the XP shell is part of the 2000s world, not the game's
        // interface. Its own (empty) theme keeps the shared Urman theme off it,
        // so the accessibility pass recolours it node by node as before.
        _computer.Theme ??= new Theme();
        AccessibilityPresentation.ApplyToControl(_computer, settings);
        _title.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("a5c7ad"));
        _documentTitle.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("b7c9b0"));
        _reader.AddThemeColorOverride("default_color", settings.HighContrast ? Colors.White : new Color("b9c8b1"));
        _status.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("94b19a"));
        _results.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("aebcac"));
        _results.AddThemeColorOverride("font_selected_color", settings.HighContrast ? Colors.White : new Color("d0b46d"));
        _excerpts?.ApplyPresentation();
        ApplyDesktopAccessibility(settings);
    }

    public override void _ExitTree()
    {
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        // AUDIO-010 hygiene: release the foley stream before teardown so a
        // still-playing sample cannot leak renderer resources at exit.
        if (_foley is not null)
        {
            _foley.Stop();
            _foley.Stream = null;
        }

    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_screen.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            if (_startMenu.Visible)
            {
                _startMenu.Hide();
                _taskbar.GetNode<Button>("Layout/Start").GrabFocus();
            }
            else Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(RuntimeBridge bridge)
    {
        _readerVersion++;
        _excerpts.Clear();
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        _bridge = bridge;
        _bridge.RuntimeStateChanged += OnRuntimeStateChanged;
        _screen.Visible = true;
        RestoreDesktop(bridge);
        _activeDocumentId = null;
        _images.SetImages(null);
        _documentTitle.Text = "АРХИВ КАРА-УРМАНА";
        _reader.Text = "Введите слово или выберите запись слева. ✓ — запись уже открывали. Закрытые записи появятся после связанной улики или понятого татарского слова.";
        _status.Text = "Локальный архив · Кара-Урман";
        _save.Disabled = true;
        _query.Text = bridge.OldPcState().GetProperty("query").GetString() ?? string.Empty;
        RefreshResults();
        SetPlayerModal(true);
        var state = bridge.OldPcState();
        var previous = state.GetProperty("activeDocumentId").GetString();
        if (previous is not null && bridge.IsOldPcDocumentAccessible(previous)
            && bridge.SelectRuntimeState().GetProperty("presentation").GetProperty("openedDocumentIds")
                .EnumerateArray().Any(id => id.GetString() == previous))
        {
            var document = bridge.OldPcDocuments.Single(item => item.Id == previous);
            _activeDocumentId = previous;
            _documentTitle.Text = document.Title;
            _reader.BbcodeEnabled = true;
            _reader.Text = RenderLinkedSource(document.BodyMarkdown);
            _images.SetImages(document.Images);
            _excerpts.Bind(bridge, previous);
            _save.Disabled = false;
            RefreshResults();
        }
        FocusActiveDesktopWindow();
    }

    private void OnRuntimeStateChanged()
    {
        if (!IsInsideTree() || !_screen.Visible || _bridge?.SessionIdentity is null) return;
        RefreshVocabulary();
    }

    private async void Search()
    {
        // The archive's own search leaves the global mode: it is the ordinary
        // keyword search over the archive sections.
        _globalSearch = false;
        PlayFoley("keyboard_key");
        if (_bridge is not { } bridge)
        {
            return;
        }
        var version = ++_readerVersion;
        var session = bridge.SessionIdentity;
        try
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "search", query = _query.Text }));
            if (!IsCurrentReader(bridge, session, version)) return;
            _activeDocumentId = null;
            _excerpts.Clear();
            _images.SetImages(null);
            _documentTitle.Text = "АРХИВ КАРА-УРМАНА";
            _reader.Text = "Выберите запись слева, чтобы открыть документ. Некоторые записи пока закрыты для Айдара.";
            _save.Disabled = true;
            RefreshResults();
            var found = Enumerable.Range(0, _results.ItemCount).Count(_results.IsItemSelectable);
            _status.Text = $"Найдено записей: {found}";
            if (found == 0)
            {
                FruitlessSearches++;
                _reader.Text = "Совпадений нет. Попробуйте имя, название места или короткое слово из найденной записи.";
                // §4.1: the archive line is the second hint carrier, and it names
                // a term to try instead of quoting whatever the record says.
                if (NextHint() is { } hint && SurfaceNextHint() is { } hintText)
                    _reader.Text += $"\n\nПодсказка: {hintText}\nПопробуйте поиск: {hint.PointsTo}";
            }
            else FruitlessSearches = 0;
        }
        catch (Exception exception)
        {
            if (IsCurrentReader(bridge, session, version)) _status.Text = exception.Message;
        }
    }

    private async void OpenDocument(long index)
    {
        if (_bridge is not { } bridge
            || index < 0 || index >= _results.ItemCount
            || !_results.IsItemSelectable((int)index))
        {
            return;
        }

        var documentId = _results.GetItemMetadata((int)index).AsString();
        if (OpenGlobalTarget(documentId)) return;
        var version = ++_readerVersion;
        var session = bridge.SessionIdentity;
        _excerpts.Clear();
        try
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId }));
            if (!IsCurrentReader(bridge, session, version)) return;
            var document = bridge.OldPcDocuments.Single(item => item.Id == documentId);
            _activeDocumentId = documentId;
            _documentTitle.Text = document.Title;
            _reader.BbcodeEnabled = true;
            _reader.Text = RenderLinkedSource(document.BodyMarkdown);
            _images.SetImages(document.Images);
            _excerpts.Bind(bridge, documentId);
            _status.Text = $"Мои документы · {SectionLabel(document.Section)}";
            _save.Disabled = false;
            RefreshResults();
            // ACT1-LANG.2: an opened PC document auto-collects unknown words.
            _ = bridge.ObserveVocabularyTextAsync(document.Title + "\n" + document.BodyMarkdown, documentId);
        }
        catch (Exception exception)
        {
            if (!IsCurrentReader(bridge, session, version)) return;
            _activeDocumentId = null;
            _excerpts.Clear();
            _documentTitle.Text = "ДОСТУП ОГРАНИЧЕН";
            _images.SetImages(null);
            _reader.Text = exception.Message;
            _status.Text = "Нужна ещё одна связь в расследовании";
            _save.Disabled = true;
        }
    }

    private async void SaveDocument()
    {
        if (_bridge is not { } bridge || _activeDocumentId is not { } documentId)
        {
            return;
        }

        _save.Disabled = true;
        var version = _readerVersion;
        var session = bridge.SessionIdentity;
        try
        {
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "save", documentId }));
            if (!IsCurrentReader(bridge, session, version)) return;
            _status.Text = $"Документ добавлен в журнал · Выйдите из ПК, затем {JournalShortcutLabel()}";
        }
        catch (Exception exception)
        {
            if (!IsCurrentReader(bridge, session, version)) return;
            _status.Text = exception.Message;
            _save.Disabled = false;
        }
    }

    private void RefreshResults()
    {
        if (_bridge is null)
        {
            return;
        }
        if (_globalSearch)
        {
            RefreshGlobalResults();
            return;
        }

        var query = _query.Text.Trim();
        var opened = _bridge.SelectRuntimeState().GetProperty("presentation")
            .GetProperty("openedDocumentIds").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        _results.Clear();
        var hasRestrictedRecords = false;
        foreach (var document in _bridge.OldPcDocuments)
        {
            if (!_bridge.IsOldPcDocumentAccessible(document.Id))
            {
                hasRestrictedRecords = true;
                continue;
            }
            if (!Matches(document, query)) continue;
            var wasOpened = opened.Contains(document.Id);
            var index = _results.AddItem(wasOpened ? $"✓ {document.Title}" : document.Title);
            _results.SetItemTooltip(index, wasOpened ? "Уже открывали" : "Ещё не открывали");
            _results.SetItemMetadata(index, document.Id);
            if (document.Id == _activeDocumentId)
            {
                _results.Select(index);
                _results.EnsureCurrentIsVisible();
            }
        }
        if (hasRestrictedRecords)
        {
            var index = _results.AddItem("🔒 Закрытые записи");
            _results.SetItemSelectable(index, false);
        }
    }

    private void Close()
    {
        PersistDesktop();
        _browserVersion++;
        _pictureVersion++;
        _browserExcerpts?.Clear();
        _browserImages?.SetImages(null);
        _pictureReader?.SetImages(null);
        _dragWindow = null;
        _readerVersion++;
        _screen.Visible = false;
        _excerpts.Clear();
        _images.SetImages(null);
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        _bridge = null;
        _activeDocumentId = null;
        SetPlayerModal(false);
    }

    private bool IsCurrentReader(RuntimeBridge bridge, object? session, long version) =>
        IsInsideTree() && _screen.Visible && _bridge == bridge && _readerVersion == version
        && ReferenceEquals(session, bridge.SessionIdentity);

    private void SetPlayerModal(bool open)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            player.SetModalOpen(open);
        }
    }

    private string JournalShortcutLabel() =>
        InputBindingService.ActionHint("journal",
            GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { CurrentInputDevice: "gamepad" });

    private static bool Matches(OldPcDocumentContent document, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return new[] { document.Title, document.BodyMarkdown, document.Section }
            .Concat(document.SearchTerms)
            .Concat(document.SuggestedTerms)
            .Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }
}
