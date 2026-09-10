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
    private Label _status = null!;
    private Button _close = null!;
    private Button _save = null!;
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
        _status = GetNode<Label>("Screen/Document/Layout/Footer/Status");
        _close = GetNode<Button>("Screen/Document/Layout/Header/Close");
        _save = GetNode<Button>("Screen/Document/Layout/Footer/Save");
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
        _body.Text = document.BodyMarkdown;
        _status.Text = "Документ найден в зоне · можно добавить в журнал";
        _save.Disabled = false;
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        _close.GrabFocus();
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
            _status.Text = $"Документ добавлен в журнал · Откройте журнал [{JournalShortcutLabel()}]";
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
        _bridge = null;
        _document = null;
        SetPlayerModal(false);
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;

    private void SetPlayerModal(bool open) => FindPlayer()?.SetModalOpen(open);

    private string JournalShortcutLabel() =>
        FindPlayer()?.CurrentInputDevice == "gamepad" ? "Y" : "J";

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

        var scale = Mathf.Clamp((float)_accessibility.TextScale, 0.8f, 1.6f);
        var size = new Vector2(
            Mathf.Max(1f, Mathf.Min(viewport.X * 0.68f, viewport.X / scale - 24f)),
            Mathf.Max(1f, Mathf.Min(viewport.Y * 0.80f, viewport.Y / scale - 24f)));
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
