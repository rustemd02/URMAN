using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class JournalUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private const string AlsuAccountId = "urman.chapter1:knowledge/clue_alsu_heard_versions";
    private const string MaratNoticeId = "urman.oldpc:document/doc_marat_official_death_notice";
    private const string FirstConclusionId = "urman.chapter1:knowledge/clue_marat_versions_conflict";
    private Control _screen = null!;
    private Control _book = null!;
    private ItemList _entries = null!;
    private Label _title = null!;
    private RichTextLabel _body = null!;
    private DocumentImageReader _images = null!;
    private Label _source = null!;
    private Label _objective = null!;
    private Label _vocabularySummary = null!;
    private VBoxContainer _tasks = null!;
    private Button _close = null!;
    private SourceExcerptSelection _excerpts = null!;
    private TabBar _tabs = null!;
    private Control _readerArea = null!;
    private ScrollContainer _overview = null!;
    private ScrollContainer _comparison = null!;
    private readonly OptionButton[] _sourcePickers = new OptionButton[2];
    private readonly PanelContainer[] _recordCards = new PanelContainer[2];
    private readonly Label[] _recordCardTitles = new Label[2];
    private readonly Label[] _recordCardQuotes = new Label[2];
    private VBoxContainer _hypotheses = null!;
    private Label _comparisonFeedback = null!;
    private bool _comparing;
    private RuntimeBridge? _bridge;
    private IReadOnlyList<ResolvedJournalEntry> _projection = [];
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public int RenderedEntryCount => _projection.Count;

    public string? ActiveEntryId { get; private set; }

    /// <summary>The work list as plain text. The overview page draws rows now,
    /// so the projection joins them for callers that only read text.</summary>
    public string CurrentObjectiveText => _taskRows.Count == 0
        ? string.Empty
        : string.Join("\n", _taskRows.Select(RowText));

    private static string RowText(Control row) => row is HBoxContainer box
        ? string.Join(" ", box.GetChildren().OfType<Label>().Select(label => label.Text))
        : string.Empty;

    public string LearnedVocabularyText => BuildWordSummary();

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
        _vocabularySummary = GetNode<Label>("Screen/Book/Layout/Overview/Contents/Vocabulary");
        _tasks = GetNode<VBoxContainer>("Screen/Book/Layout/Overview/Contents/TaskList");
        _close = GetNode<Button>("Screen/Book/Layout/Header/Close");
        _excerpts = SourceExcerptSelection.Attach(_body, _close);
        _excerpts.SelectionModeChanged += _ => RefreshCloseHint();
        RefreshCloseHint();
        BuildComparisonUi();
        BuildNotebookUi();
        BuildVocabularyUi();
        BuildInventoryUi();
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
        if (inputEvent.IsActionPressed("inventory"))
        {
            ToggleQuickAccessPage(6);
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("map"))
        {
            ToggleQuickAccessPage(3);
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("journal"))
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

    public override void _Process(double delta)
    {
        if (!_screen.Visible) _map.TrackExploration();
    }

    public void Open(RuntimeBridge bridge, string? selectedEntryId = null)
    {
        RefreshCloseHint();
        RefreshQuickAccessLabels();
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

    private void RefreshCloseHint() => _close.Text =
        $"Закрыть {InputBindingService.ActionHint("journal", false)} / {InputBindingService.ActionHint("journal", true)}"
        + (_excerpts.Selecting ? "" : " / [Esc]");

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
        var canCompareRecords = (_bridge?.JournalActions([AlsuAccountId, MaratNoticeId]).Count ?? 0) > 0;
        var objectiveTitles = objectives.Select(objective =>
            objective.QuestId == "urman.chapter1:quest/quest_marat_first_contradiction"
                && objective.ObjectiveId == "find-contradiction" && !canCompareRecords
                ? "Выяснить, что случилось с Маратом."
                : objective.Title).ToList();
        // The fence quest carries its own counter; the authored objective title
        // stays generic while these lines describe the actual hand-over state.
        if (_bridge?.TamaraFenceSnapshotNow() is { Crashed: true, Repaired: false } fence)
        {
            objectiveTitles.RemoveAll(title => title == _bridge.ResolveText(
                "urman.chapter1:text/objective-tamara-fence-boards"));
            objectiveTitles.Add($"Передано Тамаре Геннадьевне: {fence.Delivered} / 6");
            objectiveTitles.Add(fence.Carried > 0
                ? "Вернуться к Тамаре Геннадьевне с досками"
                : "Найти целые доски в деревне");
        }
        // The arrival is still a personal scene. Present its authored next action
        // before the investigation objective, without creating another quest state.
        if (FamilyHomeObjectiveText() is { } familyObjective) objectiveTitles.Insert(0, familyObjective);
        // The arrival is still a personal scene: the investigation objective it
        // would otherwise drag in is not offered before the house is found. The
        // fence quest keeps its own rows below, because that accident has
        // already happened.
        if (ArrivalObjectiveText() is { } arrivalObjective) objectiveTitles = [arrivalObjective];
        RefreshTaskList(objectiveTitles, _bridge?.TamaraFenceSnapshotNow());
        RefreshVocabularyPage();
        _vocabularySummary.Text = BuildWordSummary();
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
            _body.Text = _notebookSection.Selected switch
            {
                1 => "Пока нет записей о людях. Разговоры постепенно заполнят эту страницу.",
                2 => "Пока нет адресов. Их можно узнать в разговоре или прочитать на табличке.",
                3 => "Пока нет источников. Прочитанные документы появятся здесь.",
                4 => "Пока нет заметок о местах. Осмотренные и записанные находки появятся здесь.",
                5 => "Пока нет наблюдений. Осматривайте предметы и проверяйте услышанное.",
                _ => "Пока нет записей. Разговоры, документы и находки постепенно заполнят книжку."
            };
            _source.Text = string.Empty;
            return;
        }

        var selected = _projection.ToList().FindIndex(entry => entry.EntryId == ActiveEntryId);
        if (selected < 0) selected = _projection.Count - 1;
        _entries.Select(selected);
        SelectEntry(selected);
    }

    public void RefreshProjection() => Refresh();

    /// <summary>
    /// The notebook's work list: one line per live objective, the first one
    /// marked as the thing Aidar is doing right now, plus the fence quest's own
    /// counter and a struck-through line once it is done. Rows are Labels, so
    /// the long ones wrap like every other page of the book.
    /// </summary>
    private void RefreshTaskList(IReadOnlyList<string> titles, RuntimeBridge.TamaraFenceSnapshot? fence)
    {
        var gamepad = GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && player.CurrentInputDevice == "gamepad";
        var journalHint = InputBindingService.ActionHint("journal", gamepad);
        foreach (var row in _taskRows)
        {
            if (GodotObject.IsInstanceValid(row)) row.QueueFree();
        }
        _taskRows.Clear();
        if (titles.Count == 0)
        {
            AddTaskRow("—", "Пока никаких дел.", new Color(.62f, .58f, .48f), 18);
        }

        for (var index = 0; index < titles.Count; index++)
        {
            var current = index == 0;
            AddTaskRow(current ? "●" : "•", titles[index].Replace("[J]", journalHint, StringComparison.Ordinal),
                current ? new Color(.95f, .83f, .56f) : new Color(.86f, .82f, .72f),
                current ? 20 : 18, indent: current ? 0 : 12);
        }

        if (fence is { Repaired: true })
        {
            AddTaskRow("✓", "Доски для забора переданы Тамаре Геннадьевне.",
                new Color(.62f, .72f, .62f), 17, indent: 12);
        }
        else if (fence is { Crashed: true })
        {
            AddTaskRow("•", $"Забор Тамары Геннадьевны — досок у Тамары: {fence.Delivered} / 6",
                new Color(.86f, .82f, .72f), 18, indent: 12);
            AddTaskRow("·", fence.Carried + fence.Delivered >= 6
                    ? $"Целых досок при себе: {fence.Carried} — отнести Тамаре Геннадьевне."
                    : $"При себе: {fence.Carried}. Ищите доски у сараев и у придорожной поленницы по пути к дому бабая.",
                new Color(.66f, .62f, .52f), 16, indent: 26);
        }
    }

    private readonly List<Control> _taskRows = [];

    private void AddTaskRow(string marker, string text, Color colour, int fontSize, int indent = 0)
    {
        var row = new HBoxContainer { Name = "TaskRow" };
        _tasks.AddChild(row);
        row.AddThemeConstantOverride("separation", 10);
        if (indent > 0)
        {
            var spacer = new Control { CustomMinimumSize = new Vector2(indent, 0) };
            row.AddChild(spacer);
        }

        var markerLabel = new Label
        {
            Text = marker,
            CustomMinimumSize = new Vector2(16, 0),
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        markerLabel.AddThemeFontSizeOverride("font_size", fontSize);
        markerLabel.AddThemeColorOverride("font_color", colour);
        row.AddChild(markerLabel);
        var textLabel = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        textLabel.AddThemeFontSizeOverride("font_size", fontSize);
        textLabel.AddThemeColorOverride("font_color", colour);
        row.AddChild(textLabel);
        _taskRows.Add(row);
    }

    private string? ArrivalObjectiveText() =>
        _bridge is { ActiveSceneId: "urman.chapter1:scene/arrival_vehicle_dusk" }
            ? "Бабай довёз меня домой. Зайти к нему и әби — они ждут к чаю."
            : null;

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
        var displayedBody = DisplayJournalText(SourceExcerptSelection.FormatPlainSourceText(entry.Body));
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
        if ((entry.SourceId == AlsuAccountId || entry.SourceId == MaratNoticeId)
            && _sourceProjection.Any(source => source.SourceId == AlsuAccountId)
            && _sourceProjection.Any(source => source.SourceId == MaratNoticeId)
            && !_sourceProjection.Any(source => source.EntryId == FirstConclusionId))
            _source.Text += "\nТеперь обе записи у меня. Открою «Сопоставить» и проверю, что справка не объясняет.";
    }

    private static string EntryListTitle(ResolvedJournalEntry entry) => entry.Status switch
    {
        "hypothesis" => "Версия: " + entry.Title,
        "contradicted" => "Пересмотрено: " + entry.Title,
        _ => entry.Title
    };

    private string DisplayJournalText(string text) => text.Replace("[J]",
        InputBindingService.ActionHint("journal", FindPlayer()?.CurrentInputDevice == "gamepad"),
        StringComparison.Ordinal);

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
        // ACT1-UI.4/J2: the spread reads as Aidar's own page, not a candidate
        // picker: a spoken account and a read document are both records here.
        contents.AddChild(new Label { Text = "Кладу рядом две записи — из разговора или с бумаги — и смотрю, что из них следует.",
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
            var card = new PanelContainer { Name = $"RecordCard{i + 1}", Visible = false };
            card.AddThemeStyleboxOverride("panel", UrmanUiTheme.PlateBox(
                new Color(1f, 1f, 1f, .05f), new Color(1f, 1f, 1f, .16f), 1,
                marginX: UrmanUiTheme.Space.M, marginY: UrmanUiTheme.Space.S));
            contents.AddChild(card);
            var cardText = new VBoxContainer { Name = "Text" };
            cardText.AddThemeConstantOverride("separation", 4);
            card.AddChild(cardText);
            _recordCards[i] = card;
            _recordCardTitles[i] = new Label { Name = "Title", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _recordCardTitles[i].AddThemeFontSizeOverride("font_size", 18);
            cardText.AddChild(_recordCardTitles[i]);
            _recordCardQuotes[i] = new Label { Name = "Quote", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _recordCardQuotes[i].AddThemeFontSizeOverride("font_size", 15);
            _recordCardQuotes[i].AddThemeColorOverride("font_color", new Color(.82f, .78f, .68f));
            cardText.AddChild(_recordCardQuotes[i]);
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
        if (_tabs.CurrentTab == 6) _inventoryPage.GetChild<Label>(0).GrabFocus();
        else if (_tabs.CurrentTab == 5) _wordSearch.GrabFocus();
        else if (_tabs.CurrentTab == 4) _notesText.GrabFocus();
        else if (_tabs.CurrentTab == 3) _map.FocusSketch();
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
            picker.AddItem(i == 0 ? "Первая запись…" : "Вторая запись…");
            foreach (var entry in _sourceProjection.DistinctBy(entry => entry.SourceId))
            {
                picker.AddItem(EntryListTitle(entry));
                var index = picker.ItemCount - 1;
                picker.SetItemMetadata(index, entry.SourceId);
                picker.SetItemTooltip(index, EntryListTitle(entry));
                if (entry.SourceId == selected) picker.Select(index);
            }
        }
        // On the first case, place the two earned records on the page once.
        // The player's later choices remain untouched.
        if (SelectedSource(0) is null && SelectedSource(1) is null)
        {
            var firstPair = new[] { AlsuAccountId, MaratNoticeId };
            if (firstPair.All(id => _sourceProjection.Any(entry => entry.SourceId == id)))
            {
                for (var slot = 0; slot < firstPair.Length; slot++)
                {
                    var picker = _sourcePickers[slot];
                    for (var index = 1; index < picker.ItemCount; index++)
                        if (picker.GetItemMetadata(index).AsString() == firstPair[slot])
                        {
                            picker.Select(index);
                            break;
                        }
                }
            }
        }
        RefreshHypotheses();
    }

    private void RefreshHypotheses()
    {
        foreach (var child in _hypotheses.GetChildren()) { _hypotheses.RemoveChild(child); child.QueueFree(); }
        RefreshRecordCards();
        var first = SelectedSource(0);
        var second = SelectedSource(1);
        if (first is null || second is null || first == second)
        {
            var heard = _sourceProjection.Any(entry => entry.SourceId == AlsuAccountId);
            var read = _sourceProjection.Any(entry => entry.SourceId == MaratNoticeId);
            _comparisonFeedback.Text = heard && !read
                ? "Слова Алсу записаны. Найду и прочитаю официальную справку о Марате, затем положу записи рядом."
                : read && !heard
                    ? "Справка записана. Поговорю с Алсу о том, что она слышала, затем сравню её слова с документом."
                    : "Для сравнения нужны две разные записи — например, услышанная и прочитанная.";
            return;
        }
        var actions = _bridge?.JournalActions(new[] { first, second }) ?? [];
        _comparisonFeedback.Text = actions.Count == 0
            ? ((first == AlsuAccountId && second == MaratNoticeId || first == MaratNoticeId && second == AlsuAccountId)
                && _sourceProjection.Any(entry => entry.EntryId == FirstConclusionId)
                ? "Вывод о справке уже записан в «Записях». С ним можно задать Наиле конкретный вопрос."
                : "Эти две записи я уже сверил, или они пока ни к чему не ведут. Перечитаю их, когда появится новый вопрос.")
            : "Что из этих двух записей следует вместе? Если версия не выдержит сверки, выберу другую.";
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

    /// <summary>The chosen record as a short handwritten card: the saved title
    /// plus one trimmed line of the source itself, with its kind named.</summary>
    private void RefreshRecordCards()
    {
        for (var i = 0; i < 2; i++)
        {
            var id = SelectedSource(i);
            var entry = id is null
                ? null
                : _sourceProjection.FirstOrDefault(source => source.SourceId == id);
            _recordCards[i].Visible = entry is not null;
            if (entry is null) continue;
            _recordCardTitles[i].Text = entry.Title;
            _recordCardQuotes[i].Text =
                (entry.SourceId.Contains(":document/", StringComparison.Ordinal)
                    ? "Прочитанный документ" : "Моя запись")
                + ". «" + RecordExcerpt(entry) + "»";
        }
    }

    private string RecordExcerpt(ResolvedJournalEntry entry)
    {
        var plain = DisplayJournalText(SourceExcerptSelection.FormatPlainSourceText(entry.Body)).Replace("\r", string.Empty);
        var text = string.Join(" ", plain.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Trim();
        const int limit = 150;
        if (text.Length <= limit) return text;
        var cut = text[..limit];
        var space = cut.LastIndexOf(' ');
        return (space > limit / 2 ? cut[..space] : cut).TrimEnd() + "…";
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
                : "Пока я выбирал, записи изменились. Выберу две записи заново.";
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
        if (_notesSaving)
        {
            if (!await _notesSaveCompletion!.Task || !IsInsideTree() || !_screen.Visible) return;
        }
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

    private void ToggleQuickAccessPage(int tab)
    {
        if (_screen.Visible)
        {
            if (_tabs.CurrentTab == tab) Close();
            else { _tabs.CurrentTab = tab; FocusCurrentPage(); }
            return;
        }
        if (FindPlayer() is not { ModalOpen: false }
            || GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
        _tabs.CurrentTab = tab;
        Open(bridge);
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
