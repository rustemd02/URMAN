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

    public int RenderedEntryCount => _projection.Count;

    public string? ActiveEntryId { get; private set; }

    public string CurrentObjectiveText => _objective?.Text ?? string.Empty;

    public string LearnedVocabularyText => _vocabulary?.Text ?? string.Empty;

    public override void _Ready()
    {
        AddToGroup("journal_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
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
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings) =>
        AccessibilityPresentation.ApplyToControl(_book, settings);

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
        foreach (var entry in _projection)
        {
            _entries.AddItem(entry.Title);
        }

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
}
