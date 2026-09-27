using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// Diegetic reader for physical documents found in authored 3D zones.
/// Opening applies the document's data-driven effects through RuntimeBridge;
/// recording it in the journal remains an explicit player action, matching the old-PC contract.
/// </summary>
public partial class DocumentUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Control _documentView = null!;
    private Label _title = null!;
    private RichTextLabel _body = null!;
    private DocumentImageReader _images = null!;
    private Label _status = null!;
    private Button _close = null!;
    private Button _save = null!;
    private SourceExcerptSelection _excerpts = null!;
    private RuntimeBridge? _bridge;
    private CompiledDocumentContent? _document;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public bool IsOpen => _screen.Visible;

    public string? OpenDocumentId => _document?.Id;

    public string StatusText => _status?.Text ?? string.Empty;

    private AudioStreamPlayer? _foley;

    public override void _Ready()
    {
        AddToGroup("document_ui");
        _foley = UiFoley.Attach(this);        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _documentView = GetNode<Control>("Screen/Document");
        _title = GetNode<Label>("Screen/Document/Layout/Header/Title");
        _body = GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body");
        _images = DocumentImageReader.Attach(_body);
        _status = GetNode<Label>("Screen/Document/Layout/Footer/Status");
        _close = GetNode<Button>("Screen/Document/Layout/Header/Close");
        _save = GetNode<Button>("Screen/Document/Layout/Footer/Save");
        _excerpts = SourceExcerptSelection.Attach(_body, _save);
        var closeLabel = _close.Text;
        _excerpts.SelectionModeChanged += selecting => _close.Text = selecting ? "Закрыть" : closeLabel;
        _close.Pressed += Close;
        _save.Pressed += SaveToJournal;
        GetViewport().SizeChanged += RefitToViewport;
        if (FindPlayer() is { } player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        RefitToViewport();
        AccessibilityPresentation.ApplyToControl(_documentView, settings);
        var ink = settings.HighContrast ? Colors.Black : new Color("34291c");
        _title.AddThemeColorOverride("font_color", ink);
        _title.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        _title.AddThemeConstantOverride("shadow_offset_x", 0);
        _title.AddThemeConstantOverride("shadow_offset_y", 0);
        _body.AddThemeColorOverride("default_color", ink);
        _status.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.Black : new Color("5b4d39"));
        _status.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        _status.AddThemeConstantOverride("shadow_offset_x", 0);
        _status.AddThemeConstantOverride("shadow_offset_y", 0);
        foreach (var button in new[] { _close, _save })
        {
            button.AddThemeColorOverride("font_color", ink);
            button.AddThemeColorOverride("font_hover_color", Colors.Black);
            button.AddThemeColorOverride("font_pressed_color", Colors.Black);
            button.AddThemeColorOverride("font_focus_color", Colors.Black);
            button.AddThemeColorOverride("font_outline_color", Colors.Transparent);
            button.AddThemeConstantOverride("outline_size", 0);
        }
        _excerpts?.ApplyPresentation();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_screen.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(RuntimeBridge bridge, CompiledDocumentContent document)
    {
        _bridge = bridge;
        _document = document;
        _title.Text = document.Title;
        _body.Text = SourceExcerptSelection.FormatSourceText(document.BodyMarkdown);
        _images.SetImages(document.Images);
        _excerpts.Bind(bridge, document.Id);
        _status.Text = "Документ найден в зоне · можно добавить в журнал";
        _save.Disabled = false;
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        _close.GrabFocus();
        // ACT1-LANG.2: reading a document auto-collects unknown Tatar words.
        _ = bridge.ObserveVocabularyTextAsync(document.Title + "\n" + document.BodyMarkdown, document.Id);
    }

    private async void SaveToJournal()
    {
        if (_bridge is null || _document is null)
        {
            return;
        }

        _save.Disabled = true;
        if (await _bridge.RecordJournalEntryAsync(_document.Id, _document.Id))
        {
            _status.Text = $"Документ добавлен в журнал · Закройте документ, затем {JournalShortcutLabel()}";
            if (GetTree().GetFirstNodeInGroup("journal_ui") is JournalUi journalUi)
            {
                journalUi.RefreshProjection();
            }
        }
        else
        {
            _status.Text = "Не удалось записать документ в журнал";
            _save.Disabled = false;
        }
    }

    private void Close()
    {
        UiFoley.Play(_foley, "ui_click");
        _screen.Visible = false;
        _excerpts.Clear();
        _images.SetImages(null);
        _bridge = null;
        _document = null;
        SetPlayerModal(false);
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;

    private void SetPlayerModal(bool open) => FindPlayer()?.SetModalOpen(open);

    private string JournalShortcutLabel() =>
        InputBindingService.ActionHint("journal", FindPlayer()?.CurrentInputDevice == "gamepad");

    private void RefitToViewport()
    {
        if (_documentView is null)
        {
            return;
        }

        var viewport = _documentView.GetViewportRect().Size;
        if (viewport.X < 1 || viewport.Y < 1)
        {
            return;
        }

        var size = new Vector2(
            Mathf.Max(1f, Mathf.Min(viewport.X * 0.72f, 860f * Mathf.Clamp((float)_accessibility.TextScale, 0.8f, 1.6f))),
            Mathf.Max(1f, Mathf.Min(viewport.Y * 0.84f, viewport.Y - 24f)));
        _documentView.AnchorLeft = 0.5f;
        _documentView.AnchorTop = 0.5f;
        _documentView.AnchorRight = 0.5f;
        _documentView.AnchorBottom = 0.5f;
        _documentView.OffsetLeft = -size.X / 2f;
        _documentView.OffsetTop = -size.Y / 2f;
        _documentView.OffsetRight = size.X / 2f;
        _documentView.OffsetBottom = size.Y / 2f;
        _documentView.PivotOffset = size / 2f;
    }
}
