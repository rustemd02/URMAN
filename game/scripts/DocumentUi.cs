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
    private const string PrologueSceneId = "urman.fullgame:scene/pw-prologue";
    private const string ProloguePhotoId = "P-FOREST";
    private const string ProloguePhotoImagePath = "res://assets/textures/photobook/p-forest-front-v2.png";

    private Control _screen = null!;
    private Control _documentView = null!;
    private StyleBox? _notebookSheet;
    private Label _title = null!;
    private RichTextLabel _body = null!;
    private DocumentImageReader _images = null!;
    private Label _status = null!;
    private Button _close = null!;
    private Button _save = null!;
    private SourceExcerptSelection _excerpts = null!;
    private RuntimeBridge? _bridge;
    private CompiledDocumentContent? _document;
    private RuntimeBridge? _sourcePhotoInspectionBridge;
    private object? _sourcePhotoInspectionSession;
    private Func<bool>? _sourcePhotoInspectionCurrent;
    private Func<Task<bool>>? _sourcePhotoInspectionConfirm;
    private int _sourcePhotoInspectionRevision;
    private string _saveLabel = string.Empty;
    private Viewport? _refitViewport;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public bool IsOpen => _screen.Visible;

    public string? OpenDocumentId => _document?.Id;

    public string StatusText => _status?.Text ?? string.Empty;

    private AudioStreamPlayer? _foley;

    public override void _EnterTree() => BindRefitSignal();

    public override void _ExitTree()
    {
        StopSourcePhotoInspectionMonitoring();
        UnbindRefitSignal();
    }

    public override void _Ready()
    {
        AddToGroup("document_ui");
        _foley = UiFoley.Attach(this);        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _documentView = GetNode<Control>("Screen/Document");
        _notebookSheet = _documentView.GetThemeStylebox("panel");
        _title = GetNode<Label>("Screen/Document/Layout/Header/Title");
        _body = GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body");
        _images = DocumentImageReader.Attach(_body);
        _status = GetNode<Label>("Screen/Document/Layout/Footer/Status");
        _close = GetNode<Button>("Screen/Document/Layout/Header/Close");
        _save = GetNode<Button>("Screen/Document/Layout/Footer/Save");
        _saveLabel = _save.Text;
        _excerpts = SourceExcerptSelection.Attach(_body, _save);
        var closeLabel = _close.Text;
        _excerpts.SelectionModeChanged += selecting => _close.Text = selecting ? "Закрыть" : closeLabel;
        _close.Pressed += Close;
        _save.Pressed += SaveToJournal;
        BindRefitSignal();
        if (FindPlayer() is { } player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        RefitToViewport();
        _documentView.Theme = UrmanUiTheme.Notebook;
        AccessibilityPresentation.ApplyToControl(_documentView, settings);
        // ACT1-UI.4: documents are a world object — an old Soviet squared
        // exercise book in a brown cover (scene StyleBoxTexture). High contrast
        // trades the aged grid for a plain white page so nothing competes
        // with the black ink.
        if (settings.HighContrast)
        {
            var plain = new StyleBoxFlat { BgColor = Colors.White, BorderColor = Colors.Black };
            plain.SetBorderWidthAll(6);
            plain.SetContentMarginAll(36);
            _documentView.AddThemeStyleboxOverride("panel", plain);
        }
        else if (_notebookSheet is not null)
        {
            _documentView.AddThemeStyleboxOverride("panel", _notebookSheet);
        }
        var ink = settings.HighContrast ? Colors.Black : UrmanUiTheme.NotebookInk;
        _title.AddThemeColorOverride("font_color", ink);
        _title.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        _title.AddThemeConstantOverride("shadow_offset_x", 0);
        _title.AddThemeConstantOverride("shadow_offset_y", 0);
        _body.AddThemeColorOverride("default_color", ink);
        _status.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.Black : new Color("5b4d39"));
        _status.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        _status.AddThemeConstantOverride("shadow_offset_x", 0);
        _status.AddThemeConstantOverride("shadow_offset_y", 0);
        // Buttons are cover-board tabs from the notebook theme; the per-node
        // accessibility pass above gives their light text. A disabled tab
        // fades onto the paper, so its text turns to faded ink instead.
        foreach (var button in new[] { _close, _save })
        {
            button.AddThemeColorOverride("font_disabled_color",
                settings.HighContrast ? Colors.Black : UrmanUiTheme.NotebookInk with { A = .55f });
            if (!settings.HighContrast) button.AddThemeConstantOverride("outline_size", 0);
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
        StopSourcePhotoInspectionMonitoring();
        _bridge = bridge;
        _document = document;
        _title.Text = document.Title;
        _body.Text = SourceExcerptSelection.FormatSourceText(document.BodyMarkdown);
        _images.SetImages(document.Images);
        _excerpts.Bind(bridge, document.Id);
        _status.Text = "Документ найден в зоне · можно добавить в журнал";
        _save.Visible = true;
        _save.Text = _saveLabel;
        _save.Disabled = false;
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        _close.GrabFocus();
        // ACT1-LANG.2: reading a document auto-collects unknown Tatar words.
        _ = bridge.ObserveVocabularyTextAsync(document.Title + "\n" + document.BodyMarkdown, document.Id);
    }

    /// <summary>Shows the physical F00 source print in the existing image reader without opening or recording a document.</summary>
    public bool OpenProloguePhotoInspection(RuntimeBridge bridge)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(bridge) || bridge.IsQueuedForDeletion()
            || !bridge.IsPhotoWorldsCampaign || bridge.SessionIdentity is null || bridge.ActiveSceneId != PrologueSceneId)
            return false;
        var photo = bridge.PhotoBookPhotos().FirstOrDefault(candidate => candidate.PhotoId == ProloguePhotoId);
        if (photo is null) return false;
        var description = string.IsNullOrWhiteSpace(photo.FrontBrief) ? photo.Title : photo.FrontBrief;
        return OpenSourcePhotoInspection(bridge, ProloguePhotoId, ProloguePhotoImagePath, photo.Title, description,
            () => bridge.ActiveSceneId == PrologueSceneId);
    }

    /// <summary>Inspects an original in place; only an explicit optional confirmation may publish its existing source action.</summary>
    internal bool OpenSourcePhotoInspection(RuntimeBridge bridge, string photoId, string imagePath,
        string title, string description, Func<bool> current, Func<Task<bool>>? confirm = null)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(bridge) || bridge.IsQueuedForDeletion()
            || !bridge.IsPhotoWorldsCampaign || bridge.SessionIdentity is not { } session || !current())
            return false;
        if (!ResourceLoader.Exists(imagePath)
            || !ReferenceEquals(session, bridge.SessionIdentity) || !current())
            return false;

        StopSourcePhotoInspectionMonitoring();
        _sourcePhotoInspectionBridge = bridge;
        _sourcePhotoInspectionSession = session;
        _sourcePhotoInspectionCurrent = current;
        _sourcePhotoInspectionConfirm = confirm;
        bridge.RuntimeStateChanged += OnSourcePhotoInspectionRuntimeStateChanged;
        _bridge = bridge;
        _document = null;
        _excerpts.Clear();
        _title.Text = title;
        _body.Text = title == description ? title : title + "\n\n" + description;
        _images.SetImages([new DocumentImageContent(photoId, imagePath, description)]);
        _status.Text = "Рассмотрите снимок · Esc или «Закрыть» — вернуться к месту";
        _save.Visible = confirm is not null;
        _save.Disabled = confirm is null;
        _save.Text = confirm is null ? _saveLabel : "Рассмотреть снимок";
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        _images.ShowPicture(true);
        return true;
    }

    private void OnSourcePhotoInspectionRuntimeStateChanged()
    {
        if (!IsInsideTree() || !_screen.Visible)
        {
            StopSourcePhotoInspectionMonitoring();
            return;
        }

        var bridge = _sourcePhotoInspectionBridge;
        if (bridge is null || !GodotObject.IsInstanceValid(bridge) || bridge.IsQueuedForDeletion()
            || !ReferenceEquals(_sourcePhotoInspectionSession, bridge.SessionIdentity)
            || _sourcePhotoInspectionCurrent?.Invoke() != true)
            Close();
    }

    internal void CloseSourcePhotoInspection(Func<bool> current)
    {
        if (ReferenceEquals(_sourcePhotoInspectionCurrent, current)) Close();
    }

    private void StopSourcePhotoInspectionMonitoring()
    {
        if (_sourcePhotoInspectionBridge is { } bridge && GodotObject.IsInstanceValid(bridge))
            bridge.RuntimeStateChanged -= OnSourcePhotoInspectionRuntimeStateChanged;
        _sourcePhotoInspectionBridge = null;
        _sourcePhotoInspectionSession = null;
        _sourcePhotoInspectionCurrent = null;
        _sourcePhotoInspectionConfirm = null;
        _sourcePhotoInspectionRevision++;
    }

    private async void SaveToJournal()
    {
        if (_sourcePhotoInspectionConfirm is { } confirm)
        {
            var revision = _sourcePhotoInspectionRevision;
            _save.Disabled = true;
            try
            {
                // Confirmation, rather than opening or cancelling the reader,
                // is the player's source action. The owner rechecks its session.
                await confirm();
            }
            catch (Exception error)
            {
                GD.PushWarning("Source photograph inspection failed: " + error.Message);
            }
            finally
            {
                // A pending write must never close a newly opened document,
                // another source, or the reader of a different loaded session.
                if (revision == _sourcePhotoInspectionRevision && IsInsideTree()) Close();
            }
            return;
        }
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
        StopSourcePhotoInspectionMonitoring();
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
        if (!IsInsideTree() || _documentView is null)
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

    private void BindRefitSignal()
    {
        if (!IsInsideTree() || _documentView is null || _refitViewport is not null)
        {
            return;
        }

        _refitViewport = GetViewport();
        _refitViewport.SizeChanged += RefitToViewport;
    }

    private void UnbindRefitSignal()
    {
        if (_refitViewport is null)
        {
            return;
        }

        _refitViewport.SizeChanged -= RefitToViewport;
        _refitViewport = null;
    }
}
