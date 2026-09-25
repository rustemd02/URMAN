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
    private DocumentImageReader _images = null!;
    private Label _source = null!;
    private Label _objective = null!;
    private Label _vocabulary = null!;
    private Button _close = null!;
    private SourceExcerptSelection _excerpts = null!;
    private TabBar _tabs = null!;
    private Control _readerArea = null!;
    private ScrollContainer _overview = null!;
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
        _foley = UiFoley.Attach(this);
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = GetNode<Control>("Screen");
        _book = GetNode<Control>("Screen/Book");
        _entries = GetNode<ItemList>("Screen/Book/Layout/WorkArea/Entries");
        _title = GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Title");
        _body = GetNode<RichTextLabel>("Screen/Book/Layout/WorkArea/Reader/Body");
        _images = DocumentImageReader.Attach(_body);
        _source = GetNode<Label>("Screen/Book/Layout/WorkArea/Reader/Source");
        _source.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _objective = GetNode<Label>("Screen/Book/Layout/Overview/Contents/Objective");
        _vocabulary = GetNode<Label>("Screen/Book/Layout/Overview/Contents/Vocabulary");
        _close = GetNode<Button>("Screen/Book/Layout/Header/Close");
        _excerpts = SourceExcerptSelection.Attach(_body, _close);
        var closeLabel = _close.Text;
        _excerpts.SelectionModeChanged += selecting => _close.Text = selecting ? "Закрыть [J / Y]" : closeLabel;
        BuildComparisonUi();
        BuildNotebookUi();
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
        // ACT1-UI.4: colours and tab plates come from the shared theme; the
        // accessibility pass above already scaled tabs, pickers and the editor.
        _excerpts?.ApplyPresentation();
        _map?.ApplyAccessibilitySettings(settings);
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
            _notebookSection.Select(0);
            ActiveEntryId = selectedEntryId;
            _tabs.CurrentTab = 0;
        }
        Refresh();
        if (_projection.Count == 0 && _tabs.CurrentTab == 0) _tabs.CurrentTab = 2;
        UiFoley.Play(_foley, "paper_open");
        _screen.Visible = true;
        UrmanUiTheme.PlayOpen(_book, _accessibility.ReducedMotion);
        SetPlayerModal(true);
        FocusCurrentPage();
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
        // A candidate kernel is provisional until physical load validation has
        // succeeded. Tab changes and state callbacks share this same boundary.
        if (_notesLoadInProgress || _bridge?.SessionIdentity is null) return;
        _sourceProjection = _bridge?.JournalEntries() ?? [];
        _projection = NotebookProjection();
        RefreshNotebookPages();
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
        // The arrival is still a personal scene. Present its authored next action
        // before the investigation objective, without creating another quest state.
        if (FamilyHomeObjectiveText() is { } familyObjective) objectiveTitles = objectiveTitles.Prepend(familyObjective);
        if (ArrivalObjectiveText() is { } arrivalObjective) objectiveTitles = [arrivalObjective];
        var vocabulary = _bridge?.LearnedVocabulary() ?? [];
        var displayedObjectives = objectiveTitles.ToArray();
        _objective.Text = displayedObjectives.Length == 0
            ? "ТЕКУЩАЯ ЦЕЛЬ\n—"
            : $"ТЕКУЩАЯ ЦЕЛЬ\n{string.Join("\n", displayedObjectives.Select(title => $"• {title}"))}";
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
            _entries.AddItem($"{index + 1:D2} · {EntryListTitle(entry)}");
        }

        RefreshComparisonSources();

        if (_projection.Count == 0)
        {
            ActiveEntryId = null;
            _excerpts.Clear();
            _images.SetImages(null);
            _title.Text = _notebookSection.GetItemText(_notebookSection.Selected);
            _body.Text = "На этой странице пока нет записей. Разговоры, прочитанные таблички и найденные источники постепенно заполнят книжку.";
            _source.Text = string.Empty;
            return;
        }

        var selected = _projection.ToList().FindIndex(entry => entry.EntryId == ActiveEntryId);
        if (selected < 0) selected = _projection.Count - 1;
        _entries.Select(selected);
        SelectEntry(selected);
    }

    public void RefreshProjection() => Refresh();

    private string? ArrivalObjectiveText()
    {
        if (_bridge is not { ActiveSceneId: "urman.chapter1:scene/arrival_vehicle_dusk" } bridge) return null;
        if (bridge.IsInteractionAvailable("urman.chapter1:interaction/arrival-enter-house"))
            return bridge.ResolveWorldText("Дом бабая: {address:ADR-BABAI}. Найти его по маминым приметам и табличкам.");
        if (bridge.IsInteractionAvailable("urman.chapter1:interaction/arrival-answer-mother"))
            return "Телефон на скамье: ответить маме или пока промолчать.";
        var state = bridge.SelectRuntimeState();
        var messageRead = state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty("urman.chapter1:knowledge/arrival_mother_message_read", out var message)
            && message.TryGetProperty("status", out var status) && status.GetString() == "confirmed";
        return messageRead
            ? "Рядом с телефоном — старая фотография Марата."
            : "Телефон на скамье справа — прочитать сообщение мамы.";
    }

    private string? FamilyHomeObjectiveText()
    {
        if (_bridge is not { } bridge) return null;
        var state = bridge.SelectRuntimeState();
        if (state.TryGetProperty("beats", out var beats)
            && beats.TryGetProperty("urman.chapter1:beat/house-warmth-and-pause", out var beat)
            && (beat.ValueKind == System.Text.Json.JsonValueKind.String ? beat.GetString() == "completed"
                : beat.TryGetProperty("state", out var value) && value.GetString() == "completed")) return null;
        var invitationRead = state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty("urman.chapter1:knowledge/family_home_invitation", out var invitation)
            && invitation.TryGetProperty("status", out var status) && status.GetString() == "confirmed";
        // Earlier saves recorded this same spoken invitation as warning_heard.
        // Reading that earned state preserves the reminder without creating progress.
        var earlierInvitation = state.TryGetProperty("npc", out var people)
            && people.TryGetProperty("urman.chapter1:character/gulsina", out var gulsina)
            && gulsina.TryGetProperty("warning_heard", out var heard)
            && heard.ValueKind == System.Text.Json.JsonValueKind.True;
        if (!invitationRead && !earlierInvitation) return null;
        return bridge.ResolveText("urman.chapter1:text/objective-family-home-"
            + (bridge.CurrentZoneId == "house_old_pc" ? "inside" : "outside"));
    }

    private void SelectEntry(long index)
    {
        if (index < 0 || index >= _projection.Count)
        {
            return;
        }

        var entry = _projection[(int)index];
        var displayedBody = SourceExcerptSelection.FormatPlainSourceText(entry.Body);
        var sameSourceBody = _screen.Visible && ActiveEntryId == entry.EntryId && _body.Text == displayedBody;
        ActiveEntryId = entry.EntryId;
        _title.Text = entry.Title;
        if (!sameSourceBody)
        {
            _body.Text = displayedBody;
            _images.SetImages(entry.Images);
        }
        // Only a displayed original document may produce an excerpt. A clue's
        // summary is a different entry, even when it names the same source.
        _excerpts.Bind(_bridge, entry.EntryId == entry.SourceId
            && entry.EntryId.Contains(":document/", StringComparison.Ordinal) ? entry.EntryId : null);
        var status = entry.Status switch
        {
            "hypothesis" => "Версия",
            "confirmed" => "Подтверждено",
            "contradicted" => "Пересмотрено",
            _ => null
        };
        _source.Text = (status is null ? string.Empty : status + " · ") + $"Источник: {entry.SourceTitle}";
    }

    private static string EntryListTitle(ResolvedJournalEntry entry) => entry.Status switch
    {
        "hypothesis" => "Версия: " + entry.Title,
        "contradicted" => "Пересмотрено: " + entry.Title,
        _ => entry.Title
    };

    private void BuildComparisonUi()
    {
        var layout = GetNode<VBoxContainer>("Screen/Book/Layout");
        _readerArea = GetNode<Control>("Screen/Book/Layout/WorkArea");
        _overview = GetNode<ScrollContainer>("Screen/Book/Layout/Overview");
        _overview.GetVScrollBar().FocusMode = Control.FocusModeEnum.All;
        _tabs = new TabBar { Name = "Tabs", FocusMode = Control.FocusModeEnum.All, TabAlignment = TabBar.AlignmentMode.Left };
        _tabs.AddTab("Записи");
        _tabs.AddTab("Сопоставить");
        _tabs.AddTab("Цель и слова");
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
                _notebookSection.Select(0);
                _projection = _sourceProjection;
                Refresh();
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
            _overview.Visible = index == 2;
            if (_screen.Visible) FocusCurrentPage();
        };
    }

    private void FocusCurrentPage()
    {
        if (_tabs.CurrentTab == 4) _notesText.GrabFocus();
        else if (_tabs.CurrentTab == 3) _tabs.GrabFocus();
        else if (_tabs.CurrentTab == 1) _sourcePickers[0].GrabFocus();
        else if (_tabs.CurrentTab == 2)
        {
            var scrollBar = _overview.GetVScrollBar();
            if (scrollBar.IsVisibleInTree()) scrollBar.GrabFocus();
            else _tabs.GrabFocus();
        }
        else (_projection.Count > 0 ? (Control)_entries : _close).GrabFocus();
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
            foreach (var entry in _sourceProjection.DistinctBy(entry => entry.SourceId))
            {
                picker.AddItem(EntryListTitle(entry));
                var index = picker.ItemCount - 1;
                picker.SetItemMetadata(index, entry.SourceId);
                picker.SetItemTooltip(index, EntryListTitle(entry));
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

    private async void Close()
    {
        if (!await SaveNotebookDraftAsync()) return;
        _screen.Visible = false;
        _excerpts.Clear();
        _images.SetImages(null);
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
            Mathf.Max(1f, Mathf.Min(viewport.Y * 0.90f, viewport.Y - 24f)));
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
