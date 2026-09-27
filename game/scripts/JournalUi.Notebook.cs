using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private IReadOnlyList<ResolvedJournalEntry> _sourceProjection = [];
    private OptionButton _notebookSection = null!;
    private VBoxContainer _mapPage = null!;
    private SettlementMapControl _map = null!;
    private VBoxContainer _notesPage = null!;
    private TextEdit _notesText = null!;
    private Label _notesStatus = null!;
    private Button _notesSave = null!;
    private object? _notesSession;
    private bool _notesDirty;
    private bool _notesLoading;
    private bool _notesSaving;
    private string _notesBaseline = string.Empty;
    private string _notesObservedText = string.Empty;
    private RuntimeBridge? _notesBoundaryBridge;
    private bool _notesLoadInProgress;
    private NotebookLoadDraft? _notesLoadDraft;
    private sealed record NotebookLoadDraft(string Text, string Baseline, string Status, bool Dirty);

    private void BuildNotebookUi()
    {
        TreeExiting += UnbindNotebookBoundary;
        var layout = GetNode<VBoxContainer>("Screen/Book/Layout");
        GetNode<Label>("Screen/Book/Layout/Header/Heading").Text = "ЗАПИСНАЯ КНИЖКА";
        _notebookSection = new OptionButton { Name = "NotebookSection", FitToLongestItem = false };
        foreach (var label in new[] { "Все записи", "Люди", "Адреса", "Источники", "Места", "Наблюдения" })
            _notebookSection.AddItem(label);
        layout.AddChild(_notebookSection);
        layout.MoveChild(_notebookSection, _readerArea.GetIndex());
        _notebookSection.ItemSelected += _ => { ActiveEntryId = null; Refresh(); };
        _tabs.SetTabTitle(2, "Дела и слова");
        _tabs.AddTab("Схема");
        _tabs.AddTab("Мои заметки");
        _mapPage = new VBoxContainer { Name = "VillageMap", Visible = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(_mapPage);
        _mapPage.AddChild(new Label { Text = "Дома, которые я нашёл по табличкам. Услышанные адреса — в разделе «Адреса»; приметы можно уточнить у жителей.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _map = new SettlementMapControl { Name = "Map", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _mapPage.AddChild(_map);
        _notesPage = new VBoxContainer { Name = "PersonalNotes", Visible = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(_notesPage);
        _notesPage.AddChild(new Label { Text = "Мои заметки", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _notesText = new TextEdit { Name = "Text", PlaceholderText = "Имена, вопросы, приметы по дороге…",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary };
        _notesPage.AddChild(_notesText);
        var footer = new HBoxContainer();
        _notesPage.AddChild(footer);
        _notesStatus = new Label { Name = "Status", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_notesStatus);
        _notesSave = new Button { Name = "Save", Text = "Записать" };
        footer.AddChild(_notesSave);
        _notesText.TextChanged += () =>
        {
            if (_notesLoading) return;
            // A Save in this same frame may already have observed this edit and
            // displayed its result. Its delayed signal must not erase that result.
            if (_notesObservedText == _notesText.Text) return;
            _notesObservedText = _notesText.Text;
            var wasDirty = _notesDirty;
            _notesDirty = HasNotebookDraftChanges();
            if (!_notesDirty)
            {
                if (wasDirty) _notesStatus.Text = string.Empty;
                return;
            }
            if (_notesText.Text.Length > 12000) _notesStatus.Text = "На странице помещается до 12000 знаков.";
            else if (_notesStatus.Text != "Есть новые незаписанные изменения")
                _notesStatus.Text = "Есть незаписанные изменения";
        };
        _notesSave.Pressed += async () => await SaveNotebookDraftAsync();
        _tabs.TabChanged += index =>
        {
            _notebookSection.Visible = index == 0;
            _mapPage.Visible = index == 3;
            _notesPage.Visible = index == 4;
            if (index != 0) _excerpts.Clear();
            if (_screen.Visible && index == 4) _notesText.GrabFocus();
        };
    }

    private IReadOnlyList<ResolvedJournalEntry> NotebookProjection()
    {
        return _notebookSection.Selected switch
        {
            1 => _bridge?.NotebookPeople() ?? [],
            2 => _bridge?.NotebookAddresses() ?? [],
            3 => _sourceProjection.Where(entry => entry.EntryId.Contains(":document/", StringComparison.Ordinal)).ToArray(),
            4 => _bridge?.NotebookPlaces() ?? [],
            5 => _sourceProjection.Where(entry => !entry.EntryId.Contains(":document/", StringComparison.Ordinal)
                && !entry.EntryId.Contains(":knowledge/address-", StringComparison.Ordinal)).ToArray(),
            _ => _sourceProjection
        };
    }

    private void RefreshNotebookPages()
    {
        if (_bridge is not { } bridge) return;
        BindNotebookBoundary(bridge);
        // RestoreSession creates a temporary kernel during physical projection.
        // Neither it nor a failed projection owns the player's open draft.
        if (_notesLoadInProgress || bridge.SessionIdentity is null) return;
        if (bridge.NotebookSettlement is { } registry) _map.Bind(registry, bridge.LocatedAddressIds());
        var sessionChanged = !ReferenceEquals(_notesSession, bridge.SessionIdentity);
        if (sessionChanged)
        {
            _notesLoadDraft = null;
            _notesSession = bridge.SessionIdentity;
            _notesDirty = false;
        }
        else
        {
            // TextEdit queues text_changed. A runtime projection in the same
            // frame must see the actual edit before that deferred signal runs.
            _notesDirty |= HasNotebookDraftChanges();
        }
        if (_notesDirty) return;
        _notesLoading = true;
        var restored = bridge.PersonalNotebookText();
        if (sessionChanged || restored != _notesBaseline) _notesStatus.Text = string.Empty;
        _notesBaseline = restored;
        _notesObservedText = restored;
        if (_notesText.Text != _notesBaseline) _notesText.Text = _notesBaseline;
        _notesLoading = false;
    }

    private async Task<bool> SaveNotebookDraftAsync()
    {
        if (_notesSaving) return false;
        // Also handles an edit followed immediately by Save/Close, before
        // text_changed is delivered; no UI event can silently lose that edit.
        _notesDirty |= HasNotebookDraftChanges();
        _notesObservedText = _notesText.Text;
        if (!_notesDirty) return true;
        if (_bridge is not { } bridge) return false;
        var session = bridge.SessionIdentity;
        var text = _notesText.Text;
        if (text.Length > 12000) { _notesStatus.Text = "На странице помещается до 12000 знаков."; return false; }
        _notesSaving = true;
        _notesSave.Disabled = true;
        try
        {
            var saved = await bridge.SavePersonalNotebookAsync(text);
            if (!IsInsideTree() || !ReferenceEquals(session, bridge.SessionIdentity)) return false;
            var currentSaved = saved && _notesText.Text == text;
            if (saved) _notesBaseline = text;
            _notesDirty = !saved || HasNotebookDraftChanges();
            _notesStatus.Text = currentSaved ? "Записано" : saved
                ? "Есть новые незаписанные изменения" : "Не удалось записать — текст остался на странице";
            return currentSaved;
        }
        finally
        {
            _notesSaving = false;
            if (GodotObject.IsInstanceValid(_notesSave)) _notesSave.Disabled = false;
        }
    }

    private bool HasNotebookDraftChanges() => _notesText.Text != _notesBaseline
        // A failed disk write may already have committed another value to the
        // runtime. Returning to the old page must undo that value explicitly.
        || (_bridge is { } bridge && ReferenceEquals(_notesSession, bridge.SessionIdentity)
            && _notesText.Text != bridge.PersonalNotebookText());

    private void BindNotebookBoundary(RuntimeBridge bridge)
    {
        if (ReferenceEquals(_notesBoundaryBridge, bridge)) return;
        UnbindNotebookBoundary();
        _notesBoundaryBridge = bridge;
        bridge.PlayTimeBoundary += OnNotebookLoadBoundary;
    }

    private void UnbindNotebookBoundary()
    {
        if (_notesBoundaryBridge is { } bridge) bridge.PlayTimeBoundary -= OnNotebookLoadBoundary;
        _notesBoundaryBridge = null;
        _notesLoadDraft = null;
        _notesLoadInProgress = false;
    }

    private void OnNotebookLoadBoundary(string boundary)
    {
        if (_notesBoundaryBridge is not { } bridge || !IsInsideTree()) return;
        if (boundary == "load-start")
        {
            _notesLoadInProgress = true;
            // Keep the original copy across a double failure and later retry.
            // Recovery New Game will explicitly discard it at load-restored.
            if (bridge.NeedsPhysicalRecovery && _notesLoadDraft is not null) return;
            _notesLoadDraft = _screen.Visible && ReferenceEquals(_bridge, bridge)
                && _notesSession is not null && ReferenceEquals(_notesSession, bridge.SessionIdentity)
                ? new NotebookLoadDraft(_notesText.Text, _notesBaseline, _notesStatus.Text,
                    _notesDirty || HasNotebookDraftChanges()) : null;
            return;
        }
        if (boundary is not ("load-failed" or "load-restored")) return;
        _notesLoadInProgress = false;
        if (boundary == "load-restored")
        {
            _notesLoadDraft = null;
            _notesSession = null;
            if (ReferenceEquals(_bridge, bridge)) RefreshNotebookPages();
            return;
        }
        if (bridge.SessionIdentity is not { } session) return;
        var draft = _notesLoadDraft;
        _notesLoadDraft = null;
        if (draft is null || !ReferenceEquals(_bridge, bridge)) return;
        _notesSession = session;
        _notesBaseline = draft.Baseline;
        _notesObservedText = draft.Text;
        _notesLoading = true;
        try { _notesText.Text = draft.Text; }
        finally { _notesLoading = false; }
        _notesDirty = draft.Dirty;
        _notesStatus.Text = draft.Status;
    }
}
