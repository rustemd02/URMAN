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
    private TabBar _tabs = null!;
    private Control _readerArea = null!;
    private ScrollContainer _comparison = null!;
    private readonly OptionButton[] _sourcePickers = new OptionButton[2];
    private VBoxContainer _hypotheses = null!;
    private Label _comparisonFeedback = null!;
    private bool _comparing;
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
        BuildComparisonUi();
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
        var tabFontSize = Mathf.RoundToInt(18 * Mathf.Clamp((float)settings.TextScale, .8f, 1.6f));
        _tabs.AddThemeFontSizeOverride("font_size", tabFontSize);
        _tabs.AddThemeColorOverride("font_selected_color", settings.HighContrast ? Colors.White : new Color("f0c46b"));
        _tabs.AddThemeColorOverride("font_unselected_color", settings.HighContrast ? Colors.White : new Color("e5dbc7"));
        foreach (var picker in _sourcePickers) picker.GetPopup().AddThemeFontSizeOverride("font_size", tabFontSize);
        _title.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("f0c46b"));
        _body.AddThemeColorOverride("default_color", settings.HighContrast ? Colors.White : new Color("e0d6c2"));
        _source.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("aaa18f"));
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

    public void Open(RuntimeBridge bridge, string? selectedEntryId = null)
    {
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        _bridge = bridge;
        _bridge.RuntimeStateChanged += OnRuntimeStateChanged;
        if (selectedEntryId is not null)
        {
            ActiveEntryId = selectedEntryId;
            _tabs.CurrentTab = 0;
        }
        Refresh();
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        SetPlayerModal(true);
        (_tabs.CurrentTab == 1 ? (Control)_sourcePickers[0] : _projection.Count > 0 ? _entries : _close).GrabFocus();
    }

    public override void _ExitTree()
    {
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
        _bridge = null;
    }

    private void OnRuntimeStateChanged()
    {
        if (IsInsideTree() && _screen.Visible && !_comparing) Refresh();
    }

    public void Refresh()
    {
        _projection = _bridge?.JournalEntries() ?? [];
        var objectives = _bridge?.ActiveObjectives() ?? [];
        // An active investigation is not yet an actionable source comparison.
        // Use the same readiness gate as the comparison tab, including both sources.
        var canCompareRecords = (_bridge?.JournalActions([
            "urman.oldpc:document/doc_marat_official_death_notice",
            "urman.oldpc:document/rec_marat_case_register_conflict"]).Count ?? 0) > 0;
        var objectiveTitles = objectives.Select(objective =>
            objective.QuestId == "urman.chapter1:quest/quest_marat_first_contradiction"
                && objective.ObjectiveId == "find-contradiction" && !canCompareRecords
                ? "Выяснить, что случилось с Маратом."
                : objective.Title);
        var vocabulary = _bridge?.LearnedVocabulary() ?? [];
        _objective.Text = objectives.Count == 0
            ? "ТЕКУЩАЯ ЦЕЛЬ\n—"
            : $"ТЕКУЩАЯ ЦЕЛЬ\n{string.Join("\n", objectiveTitles.Select(title => $"• {title}"))}";
        // A word the player only heard is written into the kernel as "guessed",
        // and the kernel refuses to step a word back down that ladder. The
        // journal must not read a heard word as a known one, so unconfirmed
        // entries carry an explicit marker and the phrase is explained once.
        var heardOnlyCount = vocabulary.Count(entry => entry.Status != "confirmed");
        _vocabulary.Text = vocabulary.Count == 0
            ? "ТАТАРСКИЕ СЛОВА\n—"
            : "ТАТАРСКИЕ СЛОВА\n"
              + string.Join(" · ", vocabulary.Select(entry => entry.Status == "confirmed"
                  ? $"{entry.Term} — {entry.Meaning}"
                  : $"{entry.Term} — {entry.Meaning} (услышано)"))
              + (heardOnlyCount == 0
                  ? string.Empty
                  : "\n«услышано» — Айдар слышал слово, но ещё не проверил его значением.");
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

        RefreshComparisonSources();

        if (_projection.Count == 0)
        {
            ActiveEntryId = null;
            _title.Text = "ЖУРНАЛ";
            _body.Text = "Пока здесь нет записей. Найденные ключевые источники появятся здесь; остальные документы можно сохранить вручную.";
            _source.Text = string.Empty;
            return;
        }

        var selected = _projection.ToList().FindIndex(entry => entry.EntryId == ActiveEntryId);
        if (selected < 0) selected = _projection.Count - 1;
        _entries.Select(selected);
        SelectEntry(selected);
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

    private void BuildComparisonUi()
    {
        var layout = GetNode<VBoxContainer>("Screen/Book/Layout");
        _readerArea = GetNode<Control>("Screen/Book/Layout/WorkArea");
        _tabs = new TabBar { Name = "Tabs", FocusMode = Control.FocusModeEnum.All, TabAlignment = TabBar.AlignmentMode.Left };
        _tabs.AddTab("Записи");
        _tabs.AddTab("Сопоставить");
        layout.AddChild(_tabs);
        layout.MoveChild(_tabs, _readerArea.GetIndex());
        _comparison = new ScrollContainer { Name = "Comparisons", Visible = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layout.AddChild(_comparison);
        var contents = new VBoxContainer { Name = "Layout", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddThemeConstantOverride("separation", 14);
        _comparison.AddChild(contents);
        contents.AddChild(new Label { Text = "Выберите два найденных источника, затем проверьте объяснение.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        for (var i = 0; i < 2; i++)
        {
            var slot = i;
            var row = new HBoxContainer { Name = $"Source{i + 1}" };
            contents.AddChild(row);
            var picker = new OptionButton { Name = "Source", FitToLongestItem = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
            _sourcePickers[i] = picker;
            row.AddChild(picker);
            picker.ItemSelected += _ => RefreshHypotheses();
            var read = new Button { Text = "Перечитать" };
            row.AddChild(read);
            read.Pressed += () =>
            {
                var id = SelectedSource(slot);
                var index = _projection.ToList().FindIndex(entry => entry.SourceId == id);
                if (index < 0) return;
                _entries.Select(index);
                SelectEntry(index);
                _tabs.CurrentTab = 0;
            };
        }
        _hypotheses = new VBoxContainer { Name = "Hypotheses" };
        _hypotheses.AddThemeConstantOverride("separation", 10);
        contents.AddChild(_hypotheses);
        _comparisonFeedback = new Label { Name = "Feedback", FocusMode = Control.FocusModeEnum.All, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        contents.AddChild(_comparisonFeedback);
        _tabs.TabChanged += index =>
        {
            _readerArea.Visible = index == 0;
            _comparison.Visible = index == 1;
            if (index == 1) _sourcePickers[0].GrabFocus();
            else if (_projection.Count > 0) _entries.GrabFocus();
        };
    }

    private string? SelectedSource(int slot)
    {
        var picker = _sourcePickers[slot];
        return picker.Selected > 0 ? picker.GetItemMetadata(picker.Selected).AsString() : null;
    }

    private void RefreshComparisonSources()
    {
        for (var i = 0; i < 2; i++)
        {
            var selected = SelectedSource(i);
            var picker = _sourcePickers[i];
            picker.Clear();
            picker.AddItem(i == 0 ? "Первый источник…" : "Второй источник…");
            foreach (var entry in _projection.DistinctBy(entry => entry.SourceId))
            {
                picker.AddItem(entry.Title);
                var index = picker.ItemCount - 1;
                picker.SetItemMetadata(index, entry.SourceId);
                picker.SetItemTooltip(index, entry.Title);
                if (entry.SourceId == selected) picker.Select(index);
            }
        }
        RefreshHypotheses();
    }

    private void RefreshHypotheses()
    {
        foreach (var child in _hypotheses.GetChildren()) { _hypotheses.RemoveChild(child); child.QueueFree(); }
        var first = SelectedSource(0);
        var second = SelectedSource(1);
        if (first is null || second is null || first == second)
        {
            _comparisonFeedback.Text = "Нужны два разных источника. Любой из них можно перечитать перед выводом.";
            return;
        }
        var actions = _bridge?.JournalActions(new[] { first, second }) ?? [];
        _comparisonFeedback.Text = actions.Count == 0
            ? "Эта пара пока не даёт нового вывода. Сверьте, об одном ли вопросе говорят источники; уже проверенные связи не требуют повторного выбора."
            : "Что следует из обоих источников? Неудачную гипотезу можно пересмотреть.";
        foreach (var action in actions)
        {
            var button = new Button { Text = _bridge!.ResolveText(action.LabelTextId),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Disabled = _comparing };
            button.Pressed += () => Compare(action, first, second);
            _hypotheses.AddChild(button);
        }
        AccessibilityPresentation.ApplyToControl(_comparison, _accessibility);
    }

    private async void Compare(CompiledInteractionContent action, string first, string second)
    {
        if (_comparing || _bridge is not { } bridge) return;
        _comparing = true;
        foreach (var picker in _sourcePickers) picker.Disabled = true;
        RefreshHypotheses();
        try
        {
            var committed = await bridge.CompareJournalSourcesAsync(action.Id, new[] { first, second });
            if (!IsInsideTree() || !IsInstanceValid(_screen) || _bridge != bridge || !_screen.Visible) return;
            Refresh();
            _comparisonFeedback.Text = committed
                ? bridge.ResolveText(action.JournalAction!.ResultTextId)
                : "Состояние изменилось. Выберите найденные источники ещё раз.";
            _comparisonFeedback.GrabFocus();
        }
        finally
        {
            _comparing = false;
            foreach (var picker in _sourcePickers) if (IsInstanceValid(picker)) picker.Disabled = false;
            if (IsInstanceValid(_hypotheses))
                foreach (var child in _hypotheses.GetChildren().OfType<Button>()) child.Disabled = false;
        }
    }

    private void Close()
    {
        _screen.Visible = false;
        if (_bridge is not null) _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
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

        var size = new Vector2(
            Mathf.Max(1f, Mathf.Min(viewport.X * 0.80f, 1200f * Mathf.Clamp((float)_accessibility.TextScale, 0.8f, 1.6f))),
            Mathf.Max(1f, Mathf.Min(viewport.Y * 0.86f, viewport.Y - 24f)));
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
