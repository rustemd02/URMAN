using System.Diagnostics;
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
    private readonly List<string> _openTabs = [];
    private readonly HashSet<string> _shownViews = new(StringComparer.Ordinal);
    private readonly Stack<(string Section, string? Selection)> _back = new();
    private HBoxContainer _tabBar = null!;
    private Control _centre = null!;
    private StudioProperties _properties = null!;
    private Label _status = null!;
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

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetWindow().Title = "URMAN Studio";
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
            var pending = StudioFileTransaction.Recover(RepositoryRoot, StudioAiPanel.OwnsFile);
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
        foreach (var (key, title) in new[] { ("items", "Предметы"), ("ui", "Интерфейс игры") })
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
        RefreshStatus();
        if (StudioSelfCheck.OutputDirectory is { } selfCheck)
        {
            StudioSelfCheck.Run(this, selfCheck);
            return;
        }

        OfferRecoveredDrafts();
        if (!StudioTour.Done(Workspace.Root))
        {
            CallDeferred(nameof(StartTour));
        }
    }

    public override void _Process(double delta)
    {
        if (Workspace is null)
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

        if (_run is { HasExited: true })
        {
            _run = null;
            RefreshStatus();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
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
        else if (command && key.Keycode == Key.F)
        {
            _search.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (command && key.Keycode == Key.K)
        {
            OpenAiPanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Cmd+S saves even while typing: it never alters the text itself.
        if (@event is InputEventKey { Pressed: true, Keycode: Key.S } key && (key.MetaPressed || key.CtrlPressed) && Workspace is not null)
        {
            SaveAll(autosave: false);
            GetViewport().SetInputAsHandled();
        }
    }

    // ---- sections and navigation -------------------------------------------------

    private void Register(IStudioSection section) => _sections[section.Key] = section;

    public IStudioSection Section(string key) => _sections[key];

    public void Refresh() => OnEdited();

    private StudioAiPanel? _aiPanel;
    public void OpenAiPanel()
    {
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
        if (!_openTabs.Contains(key))
        {
            _openTabs.Add(key);
        }

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

        RebuildTabs();
        ShowProperties();
    }

    public void Navigate(string id)
    {
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
        Selection = id;
        ShowProperties();
    }

    public void ShowProperties()
    {
        _properties.Clear();
        if (_current is null)
        {
            return;
        }

        _sections[_current].FillProperties(_properties);
    }

    private void GoBack()
    {
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
        try
        {
            await Session.UndoAsync(() => ((StudioWorldSection)Section("world")).ReloadGeometryAsync());
        }
        catch (UndoConflictException error)
        {
            ShowBanner(error.Message + " Откройте сравнение, чтобы решить вручную.", error: true);
        }
        catch (IOException error)
        {
            ShowBanner("Отмена не завершена: " + error.Message, error: true);
        }
        catch (InvalidOperationException error)
        {
            ShowBanner(error.Message, error: true);
        }
    }

    public async void Redo()
    {
        try
        {
            await Session.RedoAsync(() => ((StudioWorldSection)Section("world")).ReloadGeometryAsync());
        }
        catch (UndoConflictException error)
        {
            ShowBanner(error.Message, error: true);
        }
        catch (IOException error)
        {
            ShowBanner("Повтор не завершён: " + error.Message, error: true);
        }
        catch (InvalidOperationException error)
        {
            ShowBanner(error.Message, error: true);
        }
    }

    private void OnEdited()
    {
        foreach (var section in _sections.Values)
        {
            section.Refresh();
        }

        ShowProperties();
        RefreshStatus();
    }

    public bool SaveAll(bool autosave)
    {
        if (Session.IsBusy)
        {
            if (!autosave) ShowBanner("Дождитесь изменения модели и обновления вида.", error: false);
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
                Drafts.Write(Workspace, DateTimeOffset.Now);
                RefreshStatus();
                return true;
            }

            foreach (var file in Workspace.Files.Where(file => file.Dirty))
            {
                file.Save();
                Drafts.Discard(file.RelativePath);
            }

            _saveError = null;
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
        catch (IOException error)
        {
            // The last complete version stays on disk; edits stay in memory (SAVE03).
            _saveError = error.Message;
            ShowBanner($"Не удалось сохранить: {error.Message}. Последняя целая версия на диске не тронута, ваши изменения остаются в Studio. Освободите место или проверьте права и сохраните снова.", error: true);
            RefreshStatus();
            return false;
        }
    }

    private void WatchDisk()
    {
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
        var drafts = Drafts.Recover();
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
            foreach (var draft in drafts.Where(draft => !draft.DiskChangedSince))
            {
                var recovered = Urman.Studio.Core.Storage.JsonSpanDocument.Parse(draft.DraftText);
                var file = Workspace.File(draft.RelativePath);
                using var scope = Session.Begin("восстановленный черновик");
                var node = recovered.ToNode();
                foreach (var item in (node as System.Text.Json.Nodes.JsonArray ?? node["entities"] as System.Text.Json.Nodes.JsonArray ?? []).OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    Session.Set(draft.RelativePath, (string)item["id"]!, item, "восстановленный черновик");
                }

                _ = file;
            }
        };
        AddChild(dialog);
        dialog.PopupCentered();
    }

    // ---- play ----------------------------------------------------------------------

    public async void PlayFromHere()
    {
        if (_run is not null)
        {
            ShowBanner("Пробный запуск уже идёт. Закройте окно игры, чтобы запустить новую версию.", error: false);
            return;
        }

        if (!SaveAll(autosave: false))
        {
            return;
        }

        RefreshStatus("Подготовка запуска…");
        var build = await PlayPreparation.CompileAsync(Workspace.Root);
        if (!build.Ready)
        {
            ShowBanner("Черновик не готов к запуску: " + string.Join("; ", build.Problems.Take(3)) + (build.Problems.Count > 3 ? $" и ещё {build.Problems.Count - 3}" : ""), error: true);
            RefreshStatus();
            return;
        }

        var start = _current is not null && _sections[_current] is StudioWorldSection world ? world.PlayStart() : null;
        var godot = OS.GetExecutablePath();
        var arguments = new List<string> { "--path", ProjectSettings.GlobalizePath("res://"), "res://scenes/act1_demo.tscn", "--" };
        arguments.Add($"--urman-studio-play={start?.Zone ?? "village_day"}@{start?.Spawn ?? "arrival"}");
        if (start?.Position is { } at)
        {
            arguments.Add(FormattableString.Invariant($"--urman-studio-at={at.X:0.###},{at.Y:0.###},{at.Z:0.###},{start.Yaw:0.#}"));
        }

        arguments.Add($"--urman-studio-label={build.RevisionLabel}");
        var folder = new RunFolders(Workspace.Root).Create(build.RevisionLabel, DateTimeOffset.Now);
        var info = new ProcessStartInfo(godot) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // The child inherits Studio's guarded, disposable userdata (PLAY07).
        _run = Process.Start(info)!;
        LastRun = folder;
        var log = new StreamWriter(folder.LogPath) { AutoFlush = true };
        void Write(string? line)
        {
            if (line is null) return;
            lock (log) log.WriteLine(line);
        }

        _run.OutputDataReceived += (_, data) => Write(data.Data);
        _run.ErrorDataReceived += (_, data) => Write(data.Data);
        _run.Exited += (_, _) => { lock (log) log.Dispose(); };
        _run.EnableRaisingEvents = true;
        _run.BeginOutputReadLine();
        _run.BeginErrorReadLine();
        RefreshStatus($"▶ Запущена проверка · {build.RevisionLabel}");
    }

    // ---- status and banners ---------------------------------------------------------------

    public void RefreshStatus(string? transient = null)
    {
        _undo.Disabled = !Session.CanUndo;
        _redo.Disabled = !Session.CanRedo;
        _undo.TooltipText = Session.UndoLabel is { } undo ? $"Отменить: {undo} (⌘Z)" : "Нечего отменять";
        _redo.TooltipText = Session.RedoLabel is { } redo ? $"Повторить: {redo} (⇧⌘Z)" : "Нечего повторять";
        var dirty = Workspace.Files.Count(file => file.Dirty);
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

        var top = Bar(root);
        top.AddChild(new Label { Text = "URMAN Studio", ThemeTypeVariation = "HeaderLabel" });
        top.AddChild(new Label { Text = "· проект «Урман», глава 1", ThemeTypeVariation = "MutedLabel" });
        _undo = Button(top, "↶ Отменить", Undo);
        _undo.Name = "UndoButton";
        _redo = Button(top, "↷", Redo);
        Button(top, "Изменить с ИИ", OpenAiPanel).TooltipText = "Выберите объект и опишите изменение (⌘/Ctrl+K)";
        Button(top, "Сохранить", () => SaveAll(autosave: false)).TooltipText = "Сохранить все изменения (⌘S)";
        _search = new LineEdit { PlaceholderText = "Поиск: имя, ID, тип, текст реплики — или вставьте ID от разработчика", CustomMinimumSize = new(420, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _search.TextChanged += OnSearch;
        _search.TextSubmitted += _ => PickResult(0);
        top.AddChild(_search);
        _status = new Label { CustomMinimumSize = new(220, 0), HorizontalAlignment = HorizontalAlignment.Right };
        top.AddChild(_status);
        Button(top, "Проверить", CheckContent);
        _play = Button(top, "▶ Играть отсюда", PlayFromHere);
        _play.ThemeTypeVariation = "PrimaryButton";
        _play.Name = "PlayButton";

        _tabBar = Bar(root);
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

        var navPanel = new PanelContainer { CustomMinimumSize = new(210, 0) };
        var nav = new VBoxContainer();
        navPanel.AddChild(nav);
        body.AddChild(navPanel);
        foreach (var (key, title) in new[] { ("world", "🏠  Мир"), ("quests", "❖  Квесты"), ("scenes", "🎬  Диалоги и сцены"), ("characters", "👤  Персонажи"), ("items", "🪵  Предметы"), ("content", "📄  Контент"), ("ui", "▭  Интерфейс игры"), ("collab", "⇅  Совместная работа") })
        {
            var button = Button(nav, title, () => OpenSection(key));
            button.ThemeTypeVariation = "NavButton";
            button.Alignment = HorizontalAlignment.Left;
            button.Name = $"Nav_{key}";
        }

        var again = Button(nav, "?  Знакомство заново", () => Tour.Start());
        again.ThemeTypeVariation = "NavButton";
        again.Alignment = HorizontalAlignment.Left;

        _centre = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipContents = true };
        body.AddChild(_centre);

        var sidePanel = new PanelContainer { CustomMinimumSize = new(330, 0) };
        var sideScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sidePanel.AddChild(sideScroll);
        _properties = new StudioProperties(this);
        sideScroll.AddChild(_properties);
        body.AddChild(sidePanel);

        Tour = new StudioTour(this) { Visible = false };
        AddChild(Tour);

        _results = new PopupPanel();
        _resultList = new ItemList { CustomMinimumSize = new(560, 320) };
        _resultList.ItemActivated += index => PickResult((int)index);
        _resultList.ItemClicked += (index, _, _) => PickResult((int)index);
        _results.AddChild(_resultList);
        AddChild(_results);
    }

    private void RebuildTabs()
    {
        foreach (var child in _tabBar.GetChildren())
        {
            child.QueueFree();
        }

        var back = Button(_tabBar, "← Назад", GoBack);
        back.Disabled = _back.Count == 0;
        foreach (var key in _openTabs)
        {
            var tab = Button(_tabBar, _sections[key].Title, () => OpenSection(key));
            tab.ThemeTypeVariation = key == _current ? "PrimaryButton" : "NavButton";
        }
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
        var dialog = new AcceptDialog
        {
            Title = "Проверка",
            DialogText = (result.IsSuccess
                    ? "Ошибки ссылок и данных: 0.\n"
                    : $"Ошибки ссылок и данных: {result.Diagnostics.Count}\n" + string.Join("\n", result.Diagnostics.Take(8).Select(d => $"• {d.Message}")) + "\n") +
                (unsupported.Count == 0 ? "Правила, которые игра не исполняет: 0.\n" : $"Правила, которые игра не исполняет: {unsupported.Count}\n" + string.Join("\n", unsupported.Take(6).Select(item => $"• «{Catalog.NameOf(item.EntityId)}»: {item.Op} — нужна новая механика")) + "\n") +
                (dirty > 0 ? $"\nПроверены сохранённые файлы; несохранённых изменений: {dirty} файл(ов).\n" : "") +
                "\nЛогические и художественные замечания здесь не проверяются. Проверка не доказывает, что проходимы все ветки игры."
        };
        AddChild(dialog);
        dialog.PopupCentered();
        RefreshStatus();
    }

    private static HBoxContainer Bar(Container parent)
    {
        var panel = new PanelContainer();
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 8);
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
