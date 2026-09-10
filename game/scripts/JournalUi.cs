using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class JournalUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Control _book = null!;
    private ItemList _entries = null!;
    private Label _title = null!;
    private RichTextLabel _body = null!;
    private Label _source = null!;
    private Label _objective = null!;
    private Label _vocabulary = null!;
    private Button _close = null!;
    private RuntimeBridge? _bridge;
    private IReadOnlyList<ResolvedJournalEntry> _projection = [];
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public int RenderedEntryCount => _projection.Count;

    public string? ActiveEntryId { get; private set; }

    public string CurrentObjectiveText => _objective?.Text ?? string.Empty;

    public string LearnedVocabularyText => _vocabulary?.Text ?? string.Empty;

    private AudioStreamPlayer? _foley;

    public override void _Ready()
    {
        AddToGroup("journal_ui");
        _foley = UiFoley.Attach(this);        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _book = GetNode<Control>("Screen/Book");
        _entries = GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries");
        _title = GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Title");
        _body = GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
        _source = GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Source");
        _objective = GetNode<Label>("Screen/Book/Layout/Objective");
        _vocabulary = GetNode<Label>("Screen/Book/Layout/Vocabulary");
        _close = GetNode<Button>("Screen/Book/Layout/Header/Close");
        _close.Pressed += Close;
        _entries.ItemSelected += SelectEntry;
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
        AccessibilityPresentation.ApplyToControl(_book, settings);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("journal"))
        {
            if (_screen.Visible)
            {
                Close();
            }
            else if (FindPlayer() is not { ModalOpen: true }
                     && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
            {
                Open(bridge);
            }

            GetViewport().SetInputAsHandled();
        }
        else if (_screen.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(RuntimeBridge bridge)
    {
        _bridge = bridge;
        Refresh();
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        (_projection.Count > 0 ? (Control)_entries : _close).GrabFocus();
    }

    public void Refresh()
    {
        _projection = _bridge?.JournalEntries() ?? [];
        var objectives = _bridge?.ActiveObjectives() ?? [];
        var vocabulary = _bridge?.LearnedVocabulary() ?? [];
        _objective.Text = objectives.Count == 0
            ? "ТЕКУЩАЯ ЦЕЛЬ\n—"
            : $"ТЕКУЩАЯ ЦЕЛЬ\n{string.Join("\n", objectives.Select(objective => $"• {objective.Title}"))}";
        _vocabulary.Text = vocabulary.Count == 0
            ? "ТАТАРСКИЕ СЛОВА\n—"
            : $"ТАТАРСКИЕ СЛОВА\n{string.Join(" · ", vocabulary.Select(entry => $"{entry.Term} — {entry.Meaning}"))}";
        _entries.Clear();
        for (var index = 0; index < _projection.Count; index++)
        {
            var entry = _projection[index];
            _entries.AddItem($"{index + 1:D2} · {entry.Title}");
        }

        // Archive list styling: warm ink slots with ochre selection.
        _entries.AddThemeColorOverride("font_color", new Color(0.74f, 0.70f, 0.60f));
        _entries.AddThemeColorOverride("font_selected_color", new Color(0.95f, 0.82f, 0.55f));
        _entries.AddThemeConstantOverride("line_separation", 8);
        _entries.AddThemeConstantOverride("v_separation", 4);

        if (_projection.Count == 0)
        {
            ActiveEntryId = null;
            _title.Text = "ЖУРНАЛ";
            _body.Text = "Пока здесь нет записей. Документы можно сохранить в журнал со старого компьютера.";
            _source.Text = string.Empty;
            return;
        }

        var latest = _projection.Count - 1;
        _entries.Select(latest);
        SelectEntry(latest);
    }

    public void RefreshProjection() => Refresh();

    private void SelectEntry(long index)
    {
        if (index < 0 || index >= _projection.Count)
        {
            return;
        }

        var entry = _projection[(int)index];
        ActiveEntryId = entry.EntryId;
        _title.Text = entry.Title;
        _body.Text = entry.Body;
        _source.Text = $"Источник: {entry.SourceTitle}";
    }

    private void Close()
    {
        _screen.Visible = false;
        _bridge = null;
        SetPlayerModal(false);
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;

    private void SetPlayerModal(bool open)
    {
        FindPlayer()?.SetModalOpen(open);
    }

    private void RefitToViewport()
    {
        if (_book is null)
        {
            return;
        }

        var viewport = _book.GetViewportRect().Size;
        if (viewport.X < 1 || viewport.Y < 1)
        {
            return;
        }

        var scale = Mathf.Clamp((float)_accessibility.TextScale, 0.8f, 1.6f);
        var size = new Vector2(
            Mathf.Max(1f, Mathf.Min(viewport.X * 0.76f, viewport.X / scale - 24f)),
            Mathf.Max(1f, Mathf.Min(viewport.Y * 0.82f, viewport.Y / scale - 24f)));
        _book.AnchorLeft = 0.5f;
        _book.AnchorTop = 0.5f;
        _book.AnchorRight = 0.5f;
        _book.AnchorBottom = 0.5f;
        _book.OffsetLeft = -size.X / 2f;
        _book.OffsetTop = -size.Y / 2f;
        _book.OffsetRight = size.X / 2f;
        _book.OffsetBottom = size.Y / 2f;
        _book.PivotOffset = size / 2f;
    }
}
