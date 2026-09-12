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
    private Label _status = null!;
    private Button _save = null!;
    private RuntimeBridge? _bridge;
    private string? _activeDocumentId;
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
        _status = GetNode<Label>("Screen/Computer/Layout/Footer/Status");
        _save = GetNode<Button>("Screen/Computer/Layout/Footer/Save");
        GetNode<Button>("Screen/Computer/Layout/Header/Close").Pressed += Close;
        GetNode<Button>("Screen/Computer/Layout/Header/Close").Pressed += () => PlayFoley("ui_click");
        GetNode<Button>("Screen/Computer/Layout/SearchRow/Search").Pressed += Search;
        GetNode<Button>("Screen/Computer/Layout/SearchRow/Search").Pressed += () => PlayFoley("keyboard_key");
        _query.TextSubmitted += _ => Search();
        _results.ItemSelected += OpenDocument;
        _save.Pressed += SaveDocument;
        _save.Pressed += () => PlayFoley("paper_open");
        // AUDIO-010: presentation-only interaction foley on the SFX bus.
        _foley = UiFoley.Attach(this);
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
        AccessibilityPresentation.ApplyToControl(_computer, settings);
        _title.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("a5c7ad"));
        _documentTitle.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("b7c9b0"));
        _reader.AddThemeColorOverride("default_color", settings.HighContrast ? Colors.White : new Color("b9c8b1"));
        _status.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("94b19a"));
        _results.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("aebcac"));
        _results.AddThemeColorOverride("font_selected_color", settings.HighContrast ? Colors.White : new Color("d0b46d"));
    }

    public override void _ExitTree()
    {
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
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(RuntimeBridge bridge)
    {
        _bridge = bridge;
        _screen.Visible = true;
        _activeDocumentId = null;
        _documentTitle.Text = "АРХИВ КЫРЛАЙ";
        _reader.Text = "Введите слово или выберите запись слева. ✓ — запись уже открывали. Закрытые записи появятся после связанной улики или понятого татарского слова.";
        _status.Text = "Локальный архив · Кырлай";
        _save.Disabled = true;
        RefreshResults();
        SetPlayerModal(true);
        _query.GrabFocus();
    }

    private async void Search()
    {
        PlayFoley("keyboard_key");
        if (_bridge is null)
        {
            return;
        }

        try
        {
            await _bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "search", query = _query.Text }));
            _activeDocumentId = null;
            _documentTitle.Text = "АРХИВ КЫРЛАЙ";
            _reader.Text = "Выберите запись слева, чтобы открыть документ. Некоторые записи пока закрыты для Айдара.";
            _save.Disabled = true;
            RefreshResults();
            var found = Enumerable.Range(0, _results.ItemCount).Count(_results.IsItemSelectable);
            _status.Text = $"Найдено записей: {found}";
            if (found == 0)
                _reader.Text = "Совпадений нет. Попробуйте имя, название места или короткое слово из найденной записи.";
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
    }

    private async void OpenDocument(long index)
    {
        if (_bridge is null
            || index < 0 || index >= _results.ItemCount
            || !_results.IsItemSelectable((int)index))
        {
            return;
        }

        var documentId = _results.GetItemMetadata((int)index).AsString();
        try
        {
            await _bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "open", documentId }));
            var document = _bridge.OldPcDocuments.Single(item => item.Id == documentId);
            _activeDocumentId = documentId;
            _documentTitle.Text = document.Title;
            _reader.Text = document.BodyMarkdown;
            _status.Text = $"C:\\KYRLAY\\{document.Section}\\{document.Id.Split('/')[^1]}.txt";
            _save.Disabled = false;
            RefreshResults();
        }
        catch (Exception exception)
        {
            _activeDocumentId = null;
            _documentTitle.Text = "ДОСТУП ОГРАНИЧЕН";
            _reader.Text = exception.Message;
            _status.Text = "Нужна ещё одна связь в расследовании";
            _save.Disabled = true;
        }
    }

    private async void SaveDocument()
    {
        if (_bridge is null || _activeDocumentId is null)
        {
            return;
        }

        _save.Disabled = true;
        try
        {
            await _bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new { type = "save", documentId = _activeDocumentId }));
            _status.Text = $"Документ добавлен в журнал · Откройте журнал [{JournalShortcutLabel()}]";
        }
        catch (Exception exception)
        {
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
        _screen.Visible = false;
        SetPlayerModal(false);
    }

    private void SetPlayerModal(bool open)
    {
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            player.SetModalOpen(open);
        }
    }

    private string JournalShortcutLabel() =>
        GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && player.CurrentInputDevice == "gamepad"
            ? "Y"
            : "J";

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
