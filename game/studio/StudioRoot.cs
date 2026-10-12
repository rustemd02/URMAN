using System.Diagnostics;
using System.Text.RegularExpressions;
using Godot;
using Urman.Studio.Core.Editing;
using Urman.Studio.Core.Play;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>One left-navigation section of Studio, shown in its own persistent tab (UX01).</summary>
public interface IStudioSection
{
    string Key { get; }
    string Title { get; }
    Control View { get; }
    /// <summary>Show the selection's properties in the right panel.</summary>
    void FillProperties(StudioProperties panel);
    /// <summary>Select an entity by ID; false when this section does not show it.</summary>
    bool Reveal(string id);
    void Refresh();
    bool HasPendingChanges => false;
    bool SavePendingChanges() => true;
    void DiscardPendingChanges() { }
}

/// <summary>
/// URMAN Studio window: top bar, sections with persistent tabs, the centre view
/// and the properties panel. It edits the checkout's authored files through
/// Urman.Studio.Core and starts the real game for "Play from here". It only
/// runs inside eng/protected_run.py, so its own world preview and every test
/// run write to a disposable userdata folder, never the player's saves.
/// </summary>
public partial class StudioRoot : Control
{
    private const double AutosaveSeconds = 30;
    private const double WatchSeconds = 2;

    private readonly Dictionary<string, IStudioSection> _sections = new(StringComparer.Ordinal);
    private readonly HashSet<string> _shownViews = new(StringComparer.Ordinal);
    private readonly Stack<(string Section, string? Selection)> _back = new();
    private HFlowContainer _tabBar = null!;
    private PanelContainer _inspectorPanel = null!;
    private MenuButton _sectionsMenu = null!;
    private Button _navigationToggle = null!;
    private Button _inspectorToggle = null!;
    private Button _backButton = null!;
    private Button _campaignPlay = null!;
    private readonly Dictionary<string, Button> _sectionButtons = new(StringComparer.Ordinal);
    private Control _centre = null!;
    private StudioProperties _properties = null!;
    private Label _status = null!;
    private string? _historyStartupWarning;
    private Button _undo = null!;
    private Button _redo = null!;
    private Button _play = null!;
    private LineEdit _search = null!;
    private PopupPanel _results = null!;
    private ItemList _resultList = null!;
    private PanelContainer _banner = null!;
    private Label _bannerText = null!;
    private string? _current;
    private string? _saveError;
    private string? _conflictNote;
    private double _sinceAutosave;
    private double _sinceWatch;
    private Process? _run;
    private bool _lastRunWasHouseCapture;
    private bool _closing;
    private ConfirmationDialog? _unsavedCloseDialog;
    private bool _discardUnsavedOnClose;
    private bool _playPreparing;
    private bool _historyActionBusy;

    public StudioWorkspace Workspace { get; private set; } = null!;
    public EditSession Session { get; private set; } = null!;
    public EntityCatalog Catalog { get; private set; } = null!;
    public DraftStore Drafts { get; private set; } = null!;
    public string? Selection { get; private set; }
    public float UiScale { get; private set; } = 1f;
    public RunFolder? LastRun { get; private set; }
    public StudioCollabSection Collab { get; private set; } = null!;
    public StudioTour Tour { get; private set; } = null!;
    public Process? ActiveRun => _run;

    public static string RepositoryRoot =>
        OS.GetCmdlineUserArgs().LastOrDefault(argument => argument.StartsWith("--urman-studio-root=", StringComparison.Ordinal))?["--urman-studio-root=".Length..]
        ?? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));

    public static string ToolsRoot => Path.GetFullPath(
        OS.GetCmdlineUserArgs().LastOrDefault(argument => argument.StartsWith("--urman-studio-tools-root=", StringComparison.Ordinal))?["--urman-studio-tools-root=".Length..]
        ?? RepositoryRoot);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetTree().AutoAcceptQuit = false;
        var window = GetWindow();
        window.CloseRequested += OnWindowCloseRequested;
        var usableScreen = DisplayServer.ScreenGetUsableRect((int)DisplayServer.ScreenOfMainWindow);
        if (usableScreen.Size.X > 0 && usableScreen.Size.Y > 0)
        {
            var minimumSize = new Vector2I(Math.Min(1100, usableScreen.Size.X), Math.Min(650, usableScreen.Size.Y));
            var size = new Vector2I(
                Math.Clamp(usableScreen.Size.X * 96 / 100, minimumSize.X, usableScreen.Size.X),
                Math.Clamp(usableScreen.Size.Y * 96 / 100, minimumSize.Y, usableScreen.Size.Y));
            window.MinSize = minimumSize;
            window.InitialPosition = Window.WindowInitialPosition.Absolute;
            window.Size = size;
            window.Position = usableScreen.Position + (usableScreen.Size - size) / 2;
        }

        window.Title = "URMAN Studio";
        GetViewport().GuiEmbedSubwindows = true;
        Theme = StudioTheme.Build(UiScale);
        AddChild(new ColorRect { Color = StudioTheme.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        if (System.Environment.GetEnvironmentVariable("URMAN_PROTECTED_RUN") != "1")
        {
            ShowBlocked("Studio запускается только через eng/run-studio.sh.",
                "Так пробные запуски игры пишут в отдельные тестовые сохранения, а ваши обычные сохранения и настройки не затрагиваются.");
            return;
        }

        try
        {
            var pending = StudioFileTransaction.RecoverWithOwnedPathPolicy(RepositoryRoot,
                StudioAiPanel.OwnsFile,
                (owner, path) => StudioFileTransaction.IsAuthoredSourceDeletionOwner(owner)
                    && StudioWorkspace.IsCanonicalAuthoredJsonSourcePath(path)
                    || StudioFileTransaction.IsOwnedCatalogPath(owner, path));
            if (pending.Count > 0)
            {
                ShowBlocked("Нужно восстановить незавершённое изменение модели.",
                    string.Join("\n", pending.Select(item => item.Id + ": " + string.Join(", ", item.Conflicts))));
                return;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowBlocked("Не удалось проверить восстановление моделей.", error.Message);
            return;
        }
        Workspace = StudioWorkspace.Open(RepositoryRoot);
        Session = new EditSession(Workspace);
        string? pilotHistoryWarning;
        try
        {
            pilotHistoryWarning = Session.RestoreCommittedFileHistory(
                "изменить окна дома через Codex",
                "urman.world:act1/kit/house-old-pc-babai-approach",
                StudioAiPanel.OwnsFile,
                IsExactHeroHousePilotFileSet,
                StudioFileTransaction.IsAuthoredSourceDeletionOwner,
                StudioWorkspace.IsCanonicalAuthoredJsonSourcePath,
                includeOwnedCatalogs: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            pilotHistoryWarning = "История отмены после перезапуска не восстановлена: " + error.Message;
        }

        _historyStartupWarning = pilotHistoryWarning;
        Catalog = new EntityCatalog(Workspace);
        Drafts = new DraftStore(Workspace.Root);
        Session.Changed += OnEdited;
        BuildLayout();
        Register(new StudioWorldSection(this));
        Register(new StudioQuestSection(this));
        Register(new StudioDialogueSection(this));
        Register(Collab = new StudioCollabSection(this));
        Register(new StudioCharacterSection(this));
        Register(new StudioContentSection(this));
        Register(new StudioItemsSection(this));
        Register(new StudioVehiclesSection(this));
        foreach (var (key, title) in new[] { ("ui", "Интерфейс игры") })
        {
            Register(new StudioPendingSection(key, title));
        }

        if (OS.GetCmdlineUserArgs().Contains(StudioCatalogBuilder.Flag))
        {
            StudioCatalogBuilder.Run(this);
            return;
        }

        if (StudioSelfCheck.ImportOutputDirectory is { } importCheck)
        {
            OpenSection("collab");
            StudioSelfCheck.RunImport(this, importCheck);
            return;
        }

        if (StudioSelfCheck.CollabOutputDirectory is { } collabCheck)
        {
            OpenSection("collab");
            StudioSelfCheck.RunCollab(this, collabCheck);
            return;
        }

        GetWindow().FilesDropped += files =>
        {
            OpenSection("world");
            foreach (var file in files) ((StudioWorldSection)Section("world")).ShowImport(file);
        };

        OpenSection("world");
        GD.Print("urman-studio-editor-ready");
        RefreshStatus();
        if (pilotHistoryWarning is not null)
            ShowBanner(pilotHistoryWarning + " Редактор доступен; неоднозначную историю нужно проверить вручную.", error: true);
        if (StudioSelfCheck.OutputDirectory is { } selfCheck)
        {
            StudioSelfCheck.Run(this, selfCheck);
            return;
        }

        OfferRecoveredDrafts();
        if (Workspace.IdentityConflicts.Count > 0)
            ShowBanner("Повторные ID: неоднозначные объекты недоступны для выбора. " + Workspace.IdentityConflicts[0], error: true);
        if (!StudioTour.Done(Workspace.Root))
        {
            CallDeferred(nameof(StartTour));
        }
    }

    public override void _Process(double delta)
    {
        if (_closing || Workspace is null
            || _unsavedCloseDialog is { } closeDialog && GodotObject.IsInstanceValid(closeDialog) && closeDialog.Visible)
        {
            return;
        }

        _sinceAutosave += delta;
        _sinceWatch += delta;
        if (_sinceAutosave >= AutosaveSeconds)
        {
            _sinceAutosave = 0;
            SaveAll(autosave: true);
        }

        if (_sinceWatch >= WatchSeconds)
        {
            _sinceWatch = 0;
            WatchDisk();
        }

        if (_run is { HasExited: true } finishedRun)
        {
            ((StudioWorldSection)Section("world")).SetChildRunRenderingPaused(false);
            _run = null;
            RefreshStatus();
            if (_lastRunWasHouseCapture)
                ShowBanner(finishedRun.ExitCode == 0
                    ? "Запуск для снимков завершён. Откройте папку результатов; качество и сопоставимость кадров требуют проверки."
                    : "Съёмка прервана. Откройте папку результатов и журнал игры.", error: finishedRun.ExitCode != 0);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_closing || IsAiPanelVisible) return;
        if (@event is not InputEventKey { Pressed: true } key || Workspace is null)
        {
            return;
        }

        // Hotkeys never take keystrokes from a text field (UX05).
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            return;
        }

        var command = key.MetaPressed || key.CtrlPressed;
        if (command && key.Keycode == Key.Z)
        {
            if (key.ShiftPressed) Redo(); else Undo();
            GetViewport().SetInputAsHandled();
        }
        else if (command && (key.Keycode is Key.F or Key.K))
        {
            _search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (command && !key.Echo && (key.Keycode is Key.Enter or Key.KpEnter)
            && ((StudioWorldSection)Section("world")).HasViewportFocus)
        {
            OpenAiPanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_closing || IsAiPanelVisible) return;
        if (_results is not null && _results.Visible)
        {
            if (@event is InputEventMouseButton { Pressed: true } click
                && !new Rect2((Vector2)_results.Position, (Vector2)_results.Size).HasPoint(click.Position))
            {
                _results.Hide();
            }

            if (_search is not null && _resultList is not null
                && (_search.HasFocus() || _resultList.HasFocus())
                && @event is InputEventKey { Pressed: true } searchKey)
            {
                var handled = true;
                switch (searchKey.Keycode)
                {
                    case Key.Up:
                        MoveSearchSelection(-1);
                        break;
                    case Key.Down:
                        MoveSearchSelection(1);
                        break;
                    case Key.Enter:
                    case Key.KpEnter:
                        PickSelectedResult();
                        break;
                    case Key.Escape:
                        _results.Hide();
                        break;
                    default:
                        handled = false;
                        break;
                }

                if (handled)
                {
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        // Cmd+S saves even while typing: it never alters the text itself.
        if (@event is InputEventKey { Pressed: true, Keycode: Key.S } saveKey && (saveKey.MetaPressed || saveKey.CtrlPressed) && Workspace is not null)
        {
            SaveAll(autosave: false);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == (int)NotificationApplicationFocusOut && _results is not null)
        {
            _results.Hide();
        }
    }

    // ---- sections and navigation -------------------------------------------------

    private void Register(IStudioSection section) => _sections[section.Key] = section;

    public IStudioSection Section(string key) => _sections[key];

    public void Refresh() => OnEdited();

    private StudioAiPanel? _aiPanel;
    public bool IsAiPanelVisible => _aiPanel is { Visible: true };
    internal bool IsWorkspaceActionBusy => _closing || _playPreparing || _run is not null || _historyActionBusy || _checkpointBusy
        || Session is not null && Session.IsBusy;
    public void OpenAiPanel()
    {
        if (_closing) return;
        if (CanOpenTextAi(Selection))
        {
            OpenTextAi(Selection!);
            return;
        }
        if (IsTextAiBusy) return;
        _aiPanel ??= new StudioAiPanel(this);
        if (_aiPanel.GetParent() is null) AddChild(_aiPanel);
        _aiPanel.Open(Selection);
    }

    private void StartTour() => Tour.Start();

    /// <summary>Called when the author applied every conflict decision; external changes are then part of the session.</summary>
    public void ConflictsResolved()
    {
        _conflictNote = Collab.PendingCount > 0 ? _conflictNote : null;
        if (_conflictNote is null) HideBanner();
        OnEdited();
    }

    public void OpenSection(string key, string? select = null, bool recordBack = true)
    {
        if (_closing) return;
        if (_current is not null && recordBack && (_current != key || select is not null))
        {
            _back.Push((_current, Selection));
        }

        // Views stay in the tree and are only hidden: a section keeps its
        // camera, scroll and unfinished input (UX01), and the world preview's
        // runtime is never torn down by a tab switch.
        foreach (var shown in _shownViews.Where(item => item != key))
        {
            _sections[shown].View.Visible = false;
        }

        _current = key;
        var section = _sections[key];
        if (_shownViews.Add(key))
        {
            _centre.AddChild(section.View);
            section.View.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        section.View.Visible = true;
        if (select is not null)
        {
            section.Reveal(select);
        }

        RefreshNavigation();
        ShowProperties();
    }

    public void Navigate(string id)
    {
        if (_closing) return;
        foreach (var section in _sections.Values)
        {
            if (section.Reveal(id))
            {
                OpenSection(section.Key, id);
                return;
            }
        }

        Select(id);
    }

    public void Select(string? id)
    {
        if (_closing) return;
        Selection = id;
        ShowProperties();
    }

    public void ShowProperties()
    {
        if (_closing) return;
        _properties.Clear();
        if (_current is null)
        {
            return;
        }

        _sections[_current].FillProperties(_properties);
    }

    private void GoBack()
    {
        if (_closing) return;
        if (!_back.TryPop(out var previous))
        {
            return;
        }

        OpenSection(previous.Section, previous.Selection, recordBack: false);
        Selection = previous.Selection;
        ShowProperties();
    }

    // ---- editing -------------------------------------------------------------------

    public async void Undo()
    {
        if (IsAiActionBusy)
        {
            ShowBanner("Завершите текущее действие Studio, прежде чем отменять его.", error: false);
            return;
        }

        _historyActionBusy = true;
        try
        {
            if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
            RefreshStatus();
            AiActionIdentity? evidenceIdentity = null;
            string? transactionId = null;
            string? requestId = null;
            string? evidenceReadError = null;
            try { TryGetPilotActionIdentity(redo: false, out evidenceIdentity, out transactionId, out requestId); }
            catch (Exception error) { evidenceReadError = error.GetType().Name; }
            try
            {
                await Session.UndoAsync(HistoryViewBarrier(redo: false));
            }
            catch (UndoConflictException error)
            {
                ShowBanner(error.Message + " Откройте сравнение, чтобы решить вручную.", error: true);
                return;
            }
            catch (IOException error)
            {
                ShowBanner("Отмена не завершена: " + error.Message, error: true);
                return;
            }
            catch (InvalidOperationException error)
            {
                ShowBanner(error.Message, error: true);
                return;
            }

            if (transactionId is null) return;
            var moved = Session.History(redo: true).FirstOrDefault();
            if (!IsPilotStep(moved, requestId ?? string.Empty, transactionId))
            {
                ShowAiEvidenceBanner("Отмена завершена, но Studio не подтвердила перемещение точной транзакции; receipt не записан.", error: true);
                return;
            }
            if (evidenceIdentity is null)
            {
                ShowAiEvidenceBanner("Отмена завершена, но frozen request/proposal receipt не прочитан (" + (evidenceReadError ?? "unknown") + ").", error: true);
                return;
            }

            await RecordAiActionEvidenceAsync(evidenceIdentity, AiEvidenceAction.Undo, transactionId,
                () => IsPilotStep(Session.History(redo: true).FirstOrDefault(), evidenceIdentity.RequestId, transactionId));
        }
        finally
        {
            _historyActionBusy = false;
            if (GodotObject.IsInstanceValid(this))
            {
                if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
                RefreshStatus();
            }
        }
    }

    public async void Redo()
    {
        if (IsAiActionBusy)
        {
            ShowBanner("Завершите текущее действие Studio, прежде чем повторять его.", error: false);
            return;
        }

        _historyActionBusy = true;
        try
        {
            if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
            RefreshStatus();
            AiActionIdentity? evidenceIdentity = null;
            string? transactionId = null;
            string? requestId = null;
            string? evidenceReadError = null;
            try { TryGetPilotActionIdentity(redo: true, out evidenceIdentity, out transactionId, out requestId); }
            catch (Exception error) { evidenceReadError = error.GetType().Name; }
            try
            {
                await Session.RedoAsync(HistoryViewBarrier(redo: true));
            }
            catch (UndoConflictException error)
            {
                ShowBanner(error.Message, error: true);
                return;
            }
            catch (IOException error)
            {
                ShowBanner("Повтор не завершён: " + error.Message, error: true);
                return;
            }
            catch (InvalidOperationException error)
            {
                ShowBanner(error.Message, error: true);
                return;
            }

            if (transactionId is null) return;
            var moved = Session.History().FirstOrDefault();
            if (!IsPilotStep(moved, requestId ?? string.Empty, transactionId))
            {
                ShowAiEvidenceBanner("Повтор завершён, но Studio не подтвердила перемещение точной транзакции; receipt не записан.", error: true);
                return;
            }
            if (evidenceIdentity is null)
            {
                ShowAiEvidenceBanner("Повтор завершён, но frozen request/proposal receipt не прочитан (" + (evidenceReadError ?? "unknown") + ").", error: true);
                return;
            }

            await RecordAiActionEvidenceAsync(evidenceIdentity, AiEvidenceAction.Redo, transactionId,
                () => IsPilotStep(Session.History().FirstOrDefault(), evidenceIdentity.RequestId, transactionId));
        }
        finally
        {
            _historyActionBusy = false;
            if (GodotObject.IsInstanceValid(this))
            {
                if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
                RefreshStatus();
            }
        }
    }

    private void OnEdited()
    {
        if (_closing) return;
        foreach (var section in _sections.Values)
        {
            section.Refresh();
        }

        ShowProperties();
        RefreshStatus();
        if (Workspace.IdentityConflicts.Count > 0)
            ShowBanner("Повторные ID: неоднозначные объекты недоступны для выбора. " + Workspace.IdentityConflicts[0], error: true);
    }

    private static bool IsExactHeroHousePilotFileSet(IReadOnlyList<string> paths)
    {
        const string recipe = "game/content/studio/hero_house.recipe.json";
        if (paths.Count != 4 || paths.Distinct(StringComparer.Ordinal).Count() != 4 || !paths.Contains(recipe, StringComparer.Ordinal))
            return false;

        var models = paths.Where(path => path.EndsWith(".glb", StringComparison.Ordinal)).ToArray();
        var blends = paths.Where(path => path.EndsWith(".blend", StringComparison.Ordinal)).ToArray();
        var manifests = paths.Where(path => path.EndsWith(".glb.manifest.json", StringComparison.Ordinal)).ToArray();
        if (models.Length != 1 || blends.Length != 1 || manifests.Length != 1) return false;

        var model = Regex.Match(models[0],
            @"\Agame/assets/models/studio/hero-house-(?<id>[a-f0-9]{32})\.glb\z", RegexOptions.CultureInvariant);
        var blend = Regex.Match(blends[0],
            @"\Aassets/source/blender/studio/hero-house-(?<id>[a-f0-9]{32})\.blend\z", RegexOptions.CultureInvariant);
        var manifest = Regex.Match(manifests[0],
            @"\Agame/assets/models/studio/hero-house-(?<id>[a-f0-9]{32})\.glb\.manifest\.json\z", RegexOptions.CultureInvariant);
        return model.Success && blend.Success && manifest.Success
            && string.Equals(model.Groups["id"].Value, blend.Groups["id"].Value, StringComparison.Ordinal)
            && string.Equals(model.Groups["id"].Value, manifest.Groups["id"].Value, StringComparison.Ordinal);
    }

    public bool SaveAll(bool autosave)
    {
        if (Session.IsBusy)
        {
            if (!autosave) ShowBanner("Дождитесь изменения модели и обновления вида.", error: false);
            return false;
        }
        foreach (var section in _sections.Values.Where(section => !autosave && section.HasPendingChanges))
        {
            if (!section.SavePendingChanges())
            {
                OpenSection(section.Key);
                ShowBanner("Изменения не сохранены. Проверьте сообщение в разделе; правки оставлены в редакторе.", error: true);
                return false;
            }
        }
        if (!autosave && Workspace.IdentityConflicts.Count > 0)
        {
            _saveError = "Исходники не сохранены: исправьте повторные ID. " + Workspace.IdentityConflicts[0];
            ShowBanner(_saveError, error: true);
            RefreshStatus();
            return false;
        }
        if (Workspace.Files.All(file => !file.Dirty))
        {
            if (!autosave) RefreshStatus("Сохранено");
            return true;
        }

        try
        {
            if (autosave)
            {
                // Autosave keeps a crash-safe draft; the authored files change on Cmd+S.
                var dirtyCount = Workspace.Files.Count(file => file.Dirty);
                if (Drafts.Write(Workspace, DateTimeOffset.Now) < dirtyCount)
                {
                    _saveError = "Часть новых правок не записана в черновики: прежние артефакты ждут решения о восстановлении.";
                    ShowBanner(_saveError, error: true);
                    RefreshStatus();
                    return false;
                }
                RefreshStatus();
                return true;
            }

            var draftCleanupBlocked = false;
            foreach (var file in Workspace.Files.Where(file => file.Dirty))
            {
                file.Save();
                try { Drafts.Discard(file.RelativePath); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    draftCleanupBlocked = true;
                }
            }

            _saveError = null;
            if (draftCleanupBlocked)
                ShowBanner("Исходники сохранены, но очистка части черновиков запрещена или недоступна. Их артефакты оставлены на месте.", error: false);
            else
                HideBanner();
            RefreshStatus("Сохранено");
            return true;
        }
        catch (ExternalChangeException error)
        {
            _conflictNote = error.Message;
            WatchDisk();
            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _saveError = "Сохранение не завершено: проверьте доступ и допустимость путей. Часть файлов могла сохраниться; остальные правки остаются в Studio.";
            ShowBanner(_saveError, error: true);
            RefreshStatus();
            return false;
        }
    }

    private void WatchDisk()
    {
        if (Session.IsBusy) return;
        foreach (var file in Workspace.ChangedOnDisk().Where(file => !Collab.IsPendingSame(file)))
        {
            var change = file.PullFromDisk();
            Collab.Report(change);
            if (change.Conflicts.Count > 0)
            {
                _conflictNote = $"{file.RelativePath}: {change.Conflicts.Count} конфликт(а) с изменением вне Studio.";
                ShowBanner($"Файл изменён вне Studio, и те же поля изменены у вас: {file.RelativePath}. Ваши правки сохранены в Studio; сравнение — в разделе «Совместная работа».", error: true);
                RefreshStatus();
            }
            else
            {
                Workspace.Reindex();
                ShowBanner(file.Dirty
                    ? $"Получены изменения {file.RelativePath}; они объединены с вашими ({change.AutoMerged})."
                    : $"Файл {file.RelativePath} обновлён вне Studio и перечитан.", error: false);
                OnEdited();
            }
        }
    }

    private void OfferRecoveredDrafts()
    {
        var drafts = Drafts.Recover(Workspace, out var warnings);
        if (warnings.Count > 0) ShowBanner(string.Join("\n", warnings.Take(3)), error: true);
        if (drafts.Count == 0)
        {
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = "Восстановление после сбоя",
            DialogText = $"Найдены несохранённые черновики ({drafts.Count}) от {drafts.Max(draft => draft.SavedAt):g}:\n" +
                         string.Join("\n", drafts.Select(draft => $"• {draft.RelativePath}{(draft.DiskChangedSince ? " — файл с тех пор изменён" : "")}")) +
                         "\n\nВосстановить их в Studio? Последняя сохранённая версия на диске не меняется, пока вы не сохраните.",
            OkButtonText = "Восстановить",
            CancelButtonText = "Не сейчас"
        };
        dialog.Confirmed += () =>
        {
            if (IsAiActionBusy)
            {
                ShowBanner("Восстановление отложено: дождитесь завершения текущего действия. Черновики сохранены.", error: true);
                return;
            }
            var currentDrafts = Drafts.Recover(Workspace, out var currentWarnings);
            var skipped = currentWarnings.Count;
            foreach (var draft in drafts.Where(draft => !draft.DiskChangedSince))
            {
                var file = Workspace.File(draft.RelativePath);
                var current = currentDrafts.FirstOrDefault(item => item.RelativePath == draft.RelativePath);
                if (current is null || current.DiskChangedSince || current.DraftText != draft.DraftText
                    || current.DiskSha256AtDraft != draft.DiskSha256AtDraft || file.Dirty
                    || !string.Equals(file.LoadedSha256, draft.DiskSha256AtDraft, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }
                var recovered = AuthoredFile.CreateNew(Workspace.Root, draft.RelativePath, draft.DraftText);
                var keys = recovered.Keys().ToArray();
                if (recovered.RootIsArray && (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length
                    || file.Keys().Any(Workspace.IsAmbiguous)
                    || keys.Any(key => Workspace.IsAmbiguous(key)
                        || Workspace.Locate(key) is { } address && address.RelativePath != draft.RelativePath)))
                {
                    skipped++;
                    continue;
                }
                var expectedText = file.Text;
                try
                {
                    Session.ReplaceDraft(draft.RelativePath, expectedText, draft.DraftText, "восстановленный черновик");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                    or InvalidOperationException or KeyNotFoundException or FormatException or System.Text.Json.JsonException)
                {
                    skipped++;
                    continue;
                }
                Drafts.AcceptRecoveredDraft(draft.RelativePath);
            }
            if (skipped > 0) ShowBanner("Часть черновиков не восстановлена: данные изменились или недоступны. Артефакты сохранены; текущие правки не заменены.", error: true);
        };
        AddChild(dialog);
        dialog.PopupCentered();
    }

    // ---- play ----------------------------------------------------------------------

    public void PlayFromHere() => _ = StartPlayAsync(captureHouseWindows: false);

    public void PlayCampaign() => _ = StartPlayAsync(captureHouseWindows: false, fullCampaign: true);

    public void CaptureHouseWindows()
    {
        if (Selection != Urman.Godot.HeroHouseRecipe.EntityId)
        {
            ShowBanner("Выберите дом, чтобы снять его окна в игре.", error: false);
            return;
        }
        _ = StartPlayAsync(captureHouseWindows: true);
    }

    public void OpenHouseWindowCaptures()
    {
        if (!_lastRunWasHouseCapture || LastRun is not { } run)
        {
            ShowBanner("Сначала снимите окна в игре.", error: false);
            return;
        }
        try
        {
            var relative = Path.GetRelativePath(Workspace.Root, run.Directory).Replace('\\', '/');
            StudioFileTransaction.EnsureNoLinks(Workspace.Root, relative, allowStudioMetadata: true);
            if (OS.ShellOpen(run.Directory) != Error.Ok)
                throw new IOException("Не удалось открыть папку снимков.");
        }
        catch (Exception error) { ShowBanner(error.Message, error: true); }
    }

    private async Task StartPlayAsync(bool captureHouseWindows, bool fullCampaign = false)
    {
        if (_closing) return;
        if (_playPreparing)
        {
            ShowBanner("Подготовка пробного запуска уже идёт. Дождитесь её завершения.", error: false);
            return;
        }
        if (_run is not null)
        {
            ShowBanner("Пробный запуск уже идёт. Закройте окно игры, чтобы запустить новую версию.", error: false);
            return;
        }
        if (IsAiActionBusy)
        {
            ShowBanner("Завершите текущее действие Studio, прежде чем запускать игру.", error: false);
            return;
        }

        _playPreparing = true;
        try
        {
            if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
            RefreshStatus("Подготовка запуска…");
            if (!SaveAll(autosave: false)) return;

            var campaignContext = fullCampaign
                ? CampaignWorldCatalog.Discover(Workspace).Resolve("urman.fullgame", "res://scenes/full_game.tscn")
                : null;
            var start = campaignContext is null && _current is not null && _sections[_current] is StudioWorldSection world ? world.PlayStart() : null;
            var capturePoints = captureHouseWindows ? HouseWindowCapturePoints() : null;
            var houseSources = CapturePlayHouseSources(Workspace.Root);
            var build = campaignContext is null
                ? await PlayPreparation.CompileAsync(Workspace.Root)
                : await PlayPreparation.CompileAsync(Workspace.Root, campaignContext);
            if (_closing) return;
            if (!build.Ready)
            {
                ShowBanner("Черновик не готов к запуску: " + string.Join("; ", build.Problems.Take(3)) + (build.Problems.Count > 3 ? $" и ещё {build.Problems.Count - 3}" : ""), error: true);
                return;
            }

            if (!PlayHouseSourcesMatch(Workspace.Root, houseSources))
            {
                ShowBanner("Дом изменился во время подготовки запуска. Запустите текущую версию ещё раз.", error: true);
                return;
            }

            var godot = OS.GetExecutablePath();
            var arguments = new List<string>
            {
                "--path", ProjectSettings.GlobalizePath("res://"),
                campaignContext?.World.ScenePath ?? "res://scenes/act1_demo.tscn", "--"
            };
            if (!captureHouseWindows && campaignContext is null)
            {
                arguments.Add($"--urman-studio-play={start?.Zone ?? "village_day"}@{start?.Spawn ?? "arrival"}");
                if (start?.Position is { } at)
                {
                    arguments.Add(FormattableString.Invariant($"--urman-studio-at={at.X:0.###},{at.Y:0.###},{at.Z:0.###},{start.Yaw:0.#}"));
                }

                arguments.Add($"--urman-studio-label={build.RevisionLabel}");
            }
            var folder = new RunFolders(Workspace.Root).Create(build.RevisionLabel, DateTimeOffset.Now,
                build.CampaignFingerprint, houseSources, captureHouseWindows);
            arguments.Add($"--urman-studio-run={folder.RunId}");
            var info = new ProcessStartInfo(godot) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            foreach (var name in new[] { "URMAN_VIEW_CAPTURE", "URMAN_VIEW_POINTS", "URMAN_VIEW_ZONE", "URMAN_STUDIO_CAPTURE_RUN" })
                info.Environment.Remove(name);
            if (captureHouseWindows)
            {
                foreach (var name in new[] { "URMAN_DIAGNOSTIC_VIEW", "URMAN_VIEW_SHOW_PLAYER", "URMAN_WALK_PROBE" })
                    info.Environment.Remove(name);
                info.Environment["URMAN_VIEW_CAPTURE"] = folder.Directory;
                info.Environment["URMAN_VIEW_POINTS"] = capturePoints!;
                info.Environment["URMAN_VIEW_ZONE"] = "house_old_pc";
                info.Environment["URMAN_VIEW_FOV"] = "70";
                info.Environment["URMAN_STUDIO_CAPTURE_RUN"] = folder.RunId;
            }

            var log = new StreamWriter(folder.LogPath) { AutoFlush = true };
            var closedStreams = 0;
            var logClosed = false;
            void Write(string? line)
            {
                lock (log)
                {
                    if (line is null)
                    {
                        if (!logClosed && ++closedStreams == 2)
                        {
                            logClosed = true;
                            log.Dispose();
                        }
                        return;
                    }
                    if (!logClosed) log.WriteLine(line);
                }
            }

            // The child inherits Studio's guarded, disposable userdata (PLAY07).
            Process run;
            try { run = Process.Start(info) ?? throw new IOException("Не удалось начать процесс игры."); }
            catch
            {
                lock (log)
                {
                    logClosed = true;
                    log.Dispose();
                }
                throw;
            }
            _run = run;
            ((StudioWorldSection)Section("world")).SetChildRunRenderingPaused(true);
            LastRun = folder;
            _lastRunWasHouseCapture = captureHouseWindows;
            run.OutputDataReceived += (_, data) => Write(data.Data);
            run.ErrorDataReceived += (_, data) => Write(data.Data);
            run.EnableRaisingEvents = true;
            run.BeginOutputReadLine();
            run.BeginErrorReadLine();
            RefreshStatus($"▶ {(captureHouseWindows ? "Снимаем окна в игре · запрошен Low" : fullCampaign ? "Запущена полная кампания" : "Запущена проверка")} · {build.RevisionLabel}");
        }
        catch (Exception error)
        {
            if (!_closing && GodotObject.IsInstanceValid(this))
                ShowBanner((_run is null ? "Пробный запуск не начат: " : "Игра уже запущена, но Studio не смогла настроить журнал вывода: ") + error.Message, error: true);
        }
        finally
        {
            _playPreparing = false;
            if (!_closing && GodotObject.IsInstanceValid(this))
            {
                if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
                RefreshStatus();
            }
        }
    }

    private string HouseWindowCapturePoints()
    {
        var raw = ReadBoundedAuthoringFile(Workspace.Root, "game/content/world/visual_checkpoints.v1.json", 1024 * 1024);
        using var document = System.Text.Json.JsonDocument.Parse(StrictUtf8.GetString(raw));
        var batch = document.RootElement.GetProperty("batches").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == "B20_windows_six_fixed");
        var points = batch.GetProperty("points").GetString();
        if (string.IsNullOrWhiteSpace(points) || points.Length > 8 * 1024)
            throw new InvalidDataException("Набор игровых ракурсов окон неполон.");
        var entries = points.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var expectedIds = RunFolders.HouseWindowCaptureFiles.Where(path => path.EndsWith(".png", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        if (entries.Length != expectedIds.Count
            || !expectedIds.SetEquals(entries.Select(entry => entry.Split(':', 2)[0])))
            throw new InvalidDataException("Набор игровых ракурсов окон не совпадает с файлами запуска.");
        foreach (var entry in entries)
        {
            var parts = entry.Split(':', 2);
            var checkpoint = document.RootElement.GetProperty("points").EnumerateArray()
                .Single(row => row.GetProperty("id").GetString() == parts[0]);
            if (parts.Length != 2 || checkpoint.GetProperty("spec").GetString() != parts[1]
                || checkpoint.GetProperty("zoneId").GetString() != "house_old_pc"
                || checkpoint.GetProperty("graphicsPreset").GetString() != "low")
                throw new InvalidDataException("Ракурс окон изменился, относится к другой сцене или не запрашивает Low.");
        }
        return points;
    }

    private async void OnWindowCloseRequested()
    {
        if (_closing) return;
        if (!_discardUnsavedOnClose && !HasActiveShutdownAction()
            && (Workspace is not null && Workspace.Files.Any(file => file.Dirty)
                || _sections.Values.Any(section => section.HasPendingChanges)))
        {
            OfferSaveBeforeClose();
            return;
        }
        _closing = true;
        var run = _run;
        var childExitConfirmed = run is null;
        var world = _sections.TryGetValue("world", out var worldSection) ? worldSection as StudioWorldSection : null;
        if (HasActiveShutdownAction())
        {
            _closing = false;
            if (_banner is not null && GodotObject.IsInstanceValid(_banner))
                ShowBanner("Дождитесь завершения текущего действия Studio, прежде чем закрывать окно.", error: true);
            return;
        }
        GetViewport().GuiDisableInput = true;

        try
        {
            if (run is not null)
            {
                if (!run.HasExited)
                {
                    run.Kill(entireProcessTree: true);
                    if (!run.WaitForExit(5000)) throw new TimeoutException();
                }
                if (!run.HasExited) throw new TimeoutException();
                childExitConfirmed = true;
                ((StudioWorldSection)Section("world")).SetChildRunRenderingPaused(false);
                if (ReferenceEquals(_run, run)) _run = null;
                run.Dispose();
            }

            if (_aiPanel is { } panel && GodotObject.IsInstanceValid(panel))
            {
                if (!panel.PrepareForShutdown())
                {
                    panel.Show();
                    throw new InvalidOperationException("Временный предпросмотр AI не удалось безопасно снять.");
                }

                panel.QueueFree();
                _aiPanel = null;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (world?.HasTransientFencePreview == true)
                    throw new InvalidOperationException("Панель закрыта, но временный предпросмотр забора всё ещё активен.");
            }

            if (world is not null) await world.ReleasePreviewForShutdownAsync();
            Urman.Godot.Act1ConnectedWorld.ReleaseRoundLogCacheAfterSceneDisposal();
            Urman.Godot.WinterParticleSurfaces.ReleaseForFinalShutdown();
            Urman.Godot.PainterlyMaterialLibrary.ReleaseForFinalShutdown();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            if (world?.PreviewReleasedForShutdown == true)
            {
                GD.PushError($"Studio closed after its preview was released, but orderly resource cleanup failed ({error.GetType().Name}).");
                GetTree().Quit();
                return;
            }

            _closing = false;
            _discardUnsavedOnClose = false;
            GetViewport().GuiDisableInput = false;
            if (_banner is not null && GodotObject.IsInstanceValid(_banner))
                ShowBanner(!childExitConfirmed
                    ? "Не удалось подтвердить завершение пробного запуска. Окно Studio осталось открытым; закройте игру вручную и попробуйте ещё раз."
                    : "Не удалось подготовить предпросмотр Studio к закрытию: " + error.Message, error: true);
            GD.PushError($"Studio refused to close because orderly shutdown failed ({error.GetType().Name}).");
        }
    }

    private bool HasActiveShutdownAction() =>
        _playPreparing || _historyActionBusy || _checkpointBusy || _aiEvidenceCaptureBusy
        || Session is not null && Session.IsBusy
        || _aiPanel is { } panel && GodotObject.IsInstanceValid(panel) && panel.HasActiveOperation
        || _textAiPanel is { } textPanel && GodotObject.IsInstanceValid(textPanel) && textPanel.HasActiveOperation;

    private void OfferSaveBeforeClose()
    {
        if (_unsavedCloseDialog is { } existing && GodotObject.IsInstanceValid(existing))
        {
            existing.PopupCentered();
            return;
        }
        var dialog = new ConfirmationDialog
        {
            Title = "Сохранить изменения?",
            DialogText = "В редакторе есть несохранённые изменения.",
            OkButtonText = "Сохранить и закрыть",
            CancelButtonText = "Остаться"
        };
        _unsavedCloseDialog = dialog;
        dialog.AddButton("Закрыть без сохранения", true, "discard");
        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            _unsavedCloseDialog = null;
            if (SaveAll(autosave: false)) OnWindowCloseRequested();
        };
        dialog.CustomAction += action =>
        {
            if (action != "discard") return;
            try
            {
                foreach (var file in Workspace.Files.Where(file => file.Dirty)) Drafts.Discard(file.RelativePath);
                foreach (var section in _sections.Values.Where(section => section.HasPendingChanges))
                    section.DiscardPendingChanges();
                _discardUnsavedOnClose = true;
                dialog.QueueFree();
                _unsavedCloseDialog = null;
                OnWindowCloseRequested();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                ShowBanner("Закрытие отменено: " + error.Message, error: true);
            }
        };
        dialog.Canceled += () => { dialog.QueueFree(); _unsavedCloseDialog = null; };
        AddChild(dialog);
        dialog.PopupCentered();
    }

    // ---- status and banners ---------------------------------------------------------------

    public void RefreshStatus(string? transient = null)
    {
        if (_aiPanel is not null && GodotObject.IsInstanceValid(_aiPanel)) _aiPanel.RefreshBusyState();
        _play.Disabled = IsAiActionBusy;
        _campaignPlay.Disabled = IsAiActionBusy;
        _undo.Disabled = !Session.CanUndo || IsAiActionBusy;
        _redo.Disabled = !Session.CanRedo || IsAiActionBusy;
        _undo.TooltipText = Session.UndoLabel is { } undo ? $"Отменить: {undo} (⌘/Ctrl+Z)" : "Нечего отменять";
        _redo.TooltipText = Session.RedoLabel is { } redo ? $"Повторить: {redo} (⇧⌘/Ctrl+Z)" : "Нечего повторять";
        var dirty = Workspace.Files.Count(file => file.Dirty);
        dirty += _sections.Values.Count(section => section.HasPendingChanges);
        (string text, Color color) = (_saveError, _conflictNote, _run, dirty) switch
        {
            ({ } error, _, _, _) => ($"! Не сохранено: {error}", StudioTheme.Bad),
            (_, { }, _, _) => ("⚠ Конфликт — нужно решение", StudioTheme.Bad),
            (_, _, { }, _) => (transient ?? "▶ Запущена проверка", StudioTheme.Accent),
            (_, _, _, > 0) => ($"● Есть изменения ({dirty} файл.)", StudioTheme.Warn),
            _ => ($"✓ {transient ?? "Сохранено"}", StudioTheme.Ok)
        };
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", color);
    }

    public void ShowBanner(string text, bool error)
    {
        _bannerText.Text = text;
        _banner.ThemeTypeVariation = error ? "ErrorBanner" : "BannerPanel";
        _banner.Visible = true;
    }

    private void HideBanner() => _banner.Visible = false;

    private void ShowBlocked(string title, string text)
    {
        var box = new VBoxContainer { AnchorLeft = .5f, AnchorTop = .5f, AnchorRight = .5f, AnchorBottom = .5f, OffsetLeft = -320, OffsetRight = 320, OffsetTop = -80 };
        box.AddChild(new Label { Text = title, ThemeTypeVariation = "HeaderLabel", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center });
        AddChild(box);
    }

    // ---- layout -----------------------------------------------------------------------------

    private void BuildLayout()
    {
        var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        var header = Bar(root);
        header.AddChild(new Label { Text = "URMAN Studio", ThemeTypeVariation = "HeaderLabel" });
        header.AddChild(new Label { Text = "· проект «Урман»", ThemeTypeVariation = "MutedLabel" });

        _sectionsMenu = new MenuButton { Text = "Разделы ▾", Visible = false, TooltipText = "Открыть основной раздел Studio" };
        var sectionsPopup = _sectionsMenu.GetPopup();
        var mainSections = new (string Key, string Title)[]
        {
            ("world", "Мир"), ("quests", "Квесты"), ("scenes", "Диалоги"),
            ("characters", "Персонажи"), ("items", "Предметы"), ("vehicles", "Параметры"), ("content", "Контент")
        };
        for (var index = 0; index < mainSections.Length; index++)
            sectionsPopup.AddItem(mainSections[index].Title, index);
        sectionsPopup.IdPressed += id => OpenSection(mainSections[(int)id].Key);
        header.AddChild(_sectionsMenu);

        _navigationToggle = Button(header, "Скрыть навигацию", ToggleNavigation);
        _navigationToggle.TooltipText = "Свернуть или показать основную навигацию";
        _inspectorToggle = Button(header, "Скрыть свойства", ToggleInspector);
        _inspectorToggle.TooltipText = "Свернуть или показать инспектор выбранного объекта";

        var moreMenu = new MenuButton { Text = "Ещё ▾", TooltipText = "Совместная работа, история, контрольные точки и знакомство" };
        var morePopup = moreMenu.GetPopup();
        morePopup.AddItem("Совместная работа…", 0);
        morePopup.AddSeparator();
        morePopup.AddItem("История…", 1);
        morePopup.AddItem("Контрольные точки…", 2);
        morePopup.AddItem("Знакомство заново", 3);
        morePopup.IdPressed += id =>
        {
            switch ((int)id)
            {
                case 0: OpenSection("collab"); break;
                case 1: OpenHistory(); break;
                case 2: OpenCheckpoints(); break;
                case 3: Tour.Start(); break;
            }
        };
        header.AddChild(moreMenu);

        var actions = Bar(root);
        _undo = Button(actions, "↶ Отменить", Undo);
        _undo.Name = "UndoButton";
        _redo = Button(actions, "↷ Повторить", Redo);
        _redo.TooltipText = "Повторить отменённое изменение (⇧⌘/Ctrl+Z)";
        Button(actions, "Изменить с ИИ", OpenAiPanel).TooltipText = "Выберите объект и опишите изменение (⌘/Ctrl+Enter в окне мира)";
        Button(actions, "Сохранить", () => SaveAll(autosave: false)).TooltipText = "Сохранить все изменения (⌘/Ctrl+S)";
        _search = new LineEdit
        {
            PlaceholderText = "Поиск по имени, ID, типу или тексту",
            CustomMinimumSize = new(240, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Поиск по объектам и тексту. Фокус: ⌘/Ctrl+F или K; ↑/↓ и Enter — выбрать результат"
        };
        _search.TextChanged += OnSearch;
        _search.TextSubmitted += _ => PickSelectedResult();
        actions.AddChild(_search);
        _status = new Label
        {
            CustomMinimumSize = new(160, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        actions.AddChild(_status);
        Button(actions, "Проверить", CheckContent).TooltipText = "Проверить ссылки и данные во всём проекте";
        _play = Button(actions, "▶ Играть отсюда", PlayFromHere);
        _play.ThemeTypeVariation = "PrimaryButton";
        _play.Name = "PlayButton";
        _campaignPlay = Button(actions, "▶ Играть кампанию", PlayCampaign);
        _campaignPlay.Name = "CampaignPlayButton";
        _campaignPlay.TooltipText = "Сохранить правки и начать полную кампанию с её начальной сцены.";

        _tabBar = Bar(root);
        _backButton = Button(_tabBar, "← Назад", GoBack);
        _backButton.TooltipText = "Вернуться к предыдущему разделу и выделению";
        foreach (var (key, title) in mainSections)
        {
            var button = Button(_tabBar, title, () => OpenSection(key));
            button.ToggleMode = true;
            button.Name = $"Nav_{key}";
            _sectionButtons[key] = button;
        }
        _tabBar.TooltipText = "Основные разделы URMAN Studio";

        _banner = new PanelContainer { Visible = false, ThemeTypeVariation = "BannerPanel" };
        var bannerRow = new HBoxContainer();
        _bannerText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerRow.AddChild(_bannerText);
        Button(bannerRow, "Скрыть", HideBanner);
        _banner.AddChild(bannerRow);
        root.AddChild(_banner);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 0);
        root.AddChild(body);

        _centre = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        body.AddChild(_centre);

        _inspectorPanel = new PanelContainer { CustomMinimumSize = new(300, 0) };
        var sideScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _inspectorPanel.AddChild(sideScroll);
        _properties = new StudioProperties(this);
        sideScroll.AddChild(_properties);
        body.AddChild(_inspectorPanel);

        Tour = new StudioTour(this) { Visible = false };
        AddChild(Tour);

        _results = new PopupPanel { PopupWindow = false };
        _resultList = new ItemList { CustomMinimumSize = new(560, 320) };
        _resultList.ItemActivated += index => PickResult((int)index);
        _resultList.ItemClicked += (index, _, _) => PickResult((int)index);
        _results.AddChild(_resultList);
        AddChild(_results);
    }

    private void RefreshNavigation()
    {
        _backButton.Disabled = _back.Count == 0;
        foreach (var (key, button) in _sectionButtons)
        {
            var active = key == _current;
            button.ButtonPressed = active;
            button.ThemeTypeVariation = active ? "PrimaryButton" : "NavButton";
        }
    }

    private void ToggleNavigation()
    {
        _tabBar.Visible = !_tabBar.Visible;
        _sectionsMenu.Visible = !_tabBar.Visible;
        _navigationToggle.Text = _tabBar.Visible ? "Скрыть навигацию" : "Показать навигацию";
    }

    private void ToggleInspector()
    {
        _inspectorPanel.Visible = !_inspectorPanel.Visible;
        _inspectorToggle.Text = _inspectorPanel.Visible ? "Скрыть свойства" : "Показать свойства";
    }

    private void OnSearch(string text)
    {
        var hits = Catalog.Search(text, 25);
        _resultList.Clear();
        foreach (var hit in hits)
        {
            _resultList.AddItem($"{hit.Name}   ·   {hit.KindLabel}");
            _resultList.SetItemMetadata(_resultList.ItemCount - 1, hit.Id);
            _resultList.SetItemTooltip(_resultList.ItemCount - 1, hit.Id);
        }

        if (hits.Count > 0)
        {
            _resultList.Select(0, true);
        }

        if (hits.Count == 0 && text.Trim().Length > 0)
        {
            _resultList.AddItem(text.Contains(':')
                ? "Такого ID нет в этой копии проекта. Возможно, разработчик ещё не отправил изменения — получите их."
                : "Ничего не найдено.");
            _resultList.SetItemDisabled(0, true);
        }

        if (_resultList.ItemCount == 0)
        {
            _results.Hide();
            return;
        }

        var position = _search.GetScreenPosition() + new Vector2(0, _search.Size.Y + 4);
        _results.Popup(new Rect2I((Vector2I)position, new Vector2I(560, 320)));
        _search.GrabFocus();
        _search.CaretColumn = _search.Text.Length;
    }

    private void MoveSearchSelection(int direction)
    {
        var count = _resultList.ItemCount;
        if (count == 0)
        {
            return;
        }

        var selected = _resultList.GetSelectedItems();
        var index = selected.Length > 0 ? selected[0] : direction > 0 ? -1 : 0;
        for (var step = 0; step < count; step++)
        {
            index = (index + direction + count) % count;
            if (_resultList.IsItemDisabled(index))
            {
                continue;
            }

            _resultList.Select(index, true);
            _resultList.EnsureCurrentIsVisible();
            return;
        }
    }

    private void PickSelectedResult()
    {
        var selected = _resultList.GetSelectedItems();
        PickResult(selected.Length > 0 ? selected[0] : 0);
    }

    private void PickResult(int index)
    {
        if (index < 0 || index >= _resultList.ItemCount || _resultList.IsItemDisabled(index))
        {
            return;
        }

        var id = (string)_resultList.GetItemMetadata(index);
        _results.Hide();
        _search.Text = "";
        Navigate(id);
    }

    private async void CheckContent()
    {
        RefreshStatus("Проверка…");
        var result = await new Urman.Content.Compilation.ContentCompiler().CompileAsync(Workspace.Root, PlayPreparation.Campaign, []);
        var dirty = Workspace.Files.Count(file => file.Dirty);
        var unsupported = Urman.Studio.Core.Editing.ConditionPhrases.FindNotExecuted(Workspace);
        string coverageText;
        try
        {
            var sourceProblems = RuleCatalogCoverage.VerifyAgainstSources(Workspace.Root);
            if (sourceProblems.Count > 0)
                coverageText = "Проекция неполна; сравнение каталога не выполнено.\n" + string.Join("\n", sourceProblems.Take(6));
            else
            {
                var coverage = RuleCatalogCoverage.Inspect(Workspace);
                coverageText = coverage.Notes[0] + "\n" + coverage.Notes[1] + "\n" +
                    string.Join("\n", coverage.Problems.Take(6).Select(problem => "• " + problem));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            coverageText = "Анализ каталога недоступен: не удалось прочитать исходники игры. Результат проверки контента сохранён.";
        }
        var dialog = new AcceptDialog
        {
            Title = "Проверка",
            DialogText = "Ошибки идентичности в workspace: " + Workspace.IdentityConflicts.Count + "\n" +
                string.Join("\n", Workspace.IdentityConflicts.Take(6)) + "\n" + (result.IsSuccess
                    ? "Ошибки ссылок и данных: 0.\n"
                    : $"Ошибки ссылок и данных: {result.Diagnostics.Count}\n" + string.Join("\n", result.Diagnostics.Take(8).Select(d => $"• {d.Message}")) + "\n") +
                (unsupported.Count == 0 ? "Правила, которые игра не исполняет: 0.\n" : $"Правила, которые игра не исполняет: {unsupported.Count}\n" + string.Join("\n", unsupported.Take(6).Select(item => $"• «{Catalog.NameOf(item.EntityId)}»: {item.Op} — нужна новая механика")) + "\n") +
                "\nКаталог правил · весь workspace, включая несохранённые данные; только анализ исходников:\n" +
                coverageText + "\n" +
                (dirty > 0 ? $"\nПроверены сохранённые файлы; несохранённых изменений: {dirty} файл(ов).\n" : "") +
                "\nЛогические и художественные замечания здесь не проверяются. Проверка не доказывает, что проходимы все ветки игры."
        };
        AddChild(dialog);
        dialog.PopupCentered();
        RefreshStatus();
    }

    private static HFlowContainer Bar(Container parent)
    {
        var panel = new PanelContainer();
        var bar = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bar.AddThemeConstantOverride("h_separation", 8);
        bar.AddThemeConstantOverride("v_separation", 6);
        panel.AddChild(bar);
        parent.AddChild(panel);
        return bar;
    }

    public static Button Button(Container parent, string text, Action action)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.All };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }
}
