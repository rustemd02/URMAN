using Godot;
using System.Text.Json;
using Urman.Core.Capabilities.OldPc;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class OldPcUi
{
    private sealed class DesktopWindow(string id, Control panel, Label title, OldPcWindowTitleBar header)
    {
        public string Id { get; } = id;
        public Control Panel { get; } = panel;
        public Label Title { get; } = title;
        public OldPcWindowTitleBar Header { get; } = header;
        public Button Task { get; set; } = null!;
        public OldPcWindowSnapshot State { get; set; } = new() { Id = id };
        public bool Open;
    }

    private readonly Dictionary<string, DesktopWindow> _windows = new(StringComparer.Ordinal);
    private OldPcDesktopSnapshot _desktop = new();
    private Control _desktopBackground = null!;
    private PanelContainer _taskbar = null!;
    private HBoxContainer _tasks = null!;
    private PanelContainer _startMenu = null!;
    private Label _clock = null!;
    private DesktopWindow? _dragWindow;
    private Vector2 _dragOffset;
    private string? _focusedWindow;
    private bool _restoringDesktop;
    private bool _desktopDirty;
    private double _desktopSaveDelay;
    private double _clockCheckpoint;
    private object? _desktopSession;
    private GridContainer _shortcuts = null!;
    private VBoxContainer _startMenuBody = null!;
    private float DesktopBarHeight => 44f * (float)_accessibility.TextScale + 14f;

    public IReadOnlyList<string> OpenApplicationIds =>
        _desktop.Windows.Select(window => window.Id).ToArray();
    public string? ActiveApplicationId => _focusedWindow;

    private void BuildDesktop()
    {
        _desktopBackground = new OldPcWallpaper { Name = "Desktop", MouseFilter = Control.MouseFilterEnum.Stop };
        _screen.AddChild(_desktopBackground);
        _screen.MoveChild(_desktopBackground, 1);
        _desktopBackground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _shortcuts = new GridContainer { Name = "Shortcuts", Position = new Vector2(18, 18), Columns = 1 };
        _shortcuts.AddThemeConstantOverride("v_separation", 10);
        _desktopBackground.AddChild(_shortcuts);
        foreach (var (id, label) in new[] {
            ("files", "Мой компьютер"), ("archive", "Архив"), ("browser", "Интернет"),
            ("pictures", "Фотографии"), ("notepad", "Заметки"), ("chat", "Сообщения"),
            ("tetris", "Тетрис"), ("trash", "Корзина · Чүплек") })
        {
            var app = id;
            var button = DesktopButton(_shortcuts, "Icon_" + id, label, () => LaunchApplication(app));
            button.CustomMinimumSize = new Vector2(190, 126);
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            var glyph = new OldPcDesktopIcon { Name = "Glyph", Kind = id };
            button.AddChild(glyph);
            glyph.AnchorLeft = glyph.AnchorRight = .5f;
            glyph.OffsetLeft = -26; glyph.OffsetRight = 26; glyph.OffsetTop = 6; glyph.OffsetBottom = 58;
        }

        _taskbar = new OldPcXpBar { Name = "Taskbar", Kind = OldPcXpBar.BarKind.Taskbar };
        _screen.AddChild(_taskbar);
        _taskbar.AnchorTop = _taskbar.AnchorBottom = 1;
        _taskbar.AnchorRight = 1;
        _taskbar.OffsetTop = -50;
        var row = new HBoxContainer { Name = "Layout" };
        row.AddThemeConstantOverride("separation", 6);
        _taskbar.AddChild(row);
        var start = DesktopButton(row, "Start", "пуск", () =>
        {
            _startMenu.Visible = !_startMenu.Visible;
            FrontShell();
            LayoutDesktop();
            if (_startMenu.Visible) (_startMenu.FindChild("Launch_archive", true, false) as Button)?.GrabFocus();
        });
        start.CustomMinimumSize = new Vector2(128, 38);
        start.TooltipText = "Пуск · Башлау";
        var startLogo = new OldPcDesktopIcon { Name = "Glyph", Kind = "tulip", CustomMinimumSize = new(28, 28) };
        start.AddChild(startLogo);
        startLogo.AnchorTop = startLogo.AnchorBottom = .5f;
        startLogo.OffsetLeft = 8; startLogo.OffsetRight = 36; startLogo.OffsetTop = -14; startLogo.OffsetBottom = 14;
        _tasks = new HBoxContainer { Name = "Tasks", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _tasks.AddThemeConstantOverride("separation", 4);
        row.AddChild(_tasks);
        _clock = new Label { Name = "Clock", CustomMinimumSize = new Vector2(96, 34),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            TooltipText = "Часы компьютера Мансура" };
        row.AddChild(_clock);

        // XP's two-column start menu: the user on a blue header edged with an
        // embroidered tulip band, programs on white, places on pale blue.
        _startMenu = new PanelContainer { Name = "StartMenu", Visible = false };
        _screen.AddChild(_startMenu);
        _startMenu.Resized += LayoutDesktop;
        var frame = new VBoxContainer { Name = "Frame" };
        frame.AddThemeConstantOverride("separation", 0);
        _startMenu.AddChild(frame);
        var header = new OldPcXpBar { Name = "Header", Kind = OldPcXpBar.BarKind.MenuHeader };
        frame.AddChild(header);
        var who = new HBoxContainer { Name = "User" };
        who.AddThemeConstantOverride("separation", 12);
        header.AddChild(who);
        who.AddChild(new OldPcDesktopIcon { Name = "Avatar", Kind = "tulip", CustomMinimumSize = new(48, 48) });
        var names = new VBoxContainer();
        who.AddChild(names);
        names.AddChild(new Label { Name = "UserName", Text = "Мансур" });
        names.AddChild(new Label { Name = "Greeting", Text = "Рәхим итегез · Кара-Урман" });
        var columns = new HBoxContainer { Name = "Columns" };
        columns.AddThemeConstantOverride("separation", 0);
        frame.AddChild(columns);
        var programs = new PanelContainer { Name = "Programs", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(programs);
        var places = new PanelContainer { Name = "Places", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(places);
        var menu = new VBoxContainer { Name = "ProgramList" };
        _startMenuBody = menu;
        programs.AddChild(menu);
        foreach (var (id, label) in new[] {
            ("archive", "Архивный поиск"), ("browser", "Интернет"), ("chat", "Ялкын · Сообщения"),
            ("notepad", "Блокнот"), ("writer", "Текстовый редактор"), ("tetris", "Тетрис") })
        {
            var app = id;
            DesktopButton(menu, "Launch_" + id, label, () => { _startMenu.Hide(); LaunchApplication(app); });
        }
        var placeList = new VBoxContainer { Name = "PlaceList" };
        places.AddChild(placeList);
        foreach (var (id, label) in new[] {
            ("files", "Мои документы"), ("pictures", "Мои рисунки"), ("vocabulary", "Татарский словарь · Сүзлек"),
            ("trash", "Корзина · Чүплек") })
        {
            var app = id;
            DesktopButton(placeList, "Launch_" + id, label, () => { _startMenu.Hide(); LaunchApplication(app); });
        }
        var footer = new OldPcXpBar { Name = "Footer", Kind = OldPcXpBar.BarKind.MenuFooter };
        frame.AddChild(footer);
        var footerRow = new HBoxContainer { Name = "Actions", Alignment = BoxContainer.AlignmentMode.End };
        footer.AddChild(footerRow);
        DesktopButton(footerRow, "Leave", "Отойти от компьютера [Esc]", Close);
        RegisterDesktopWindow("archive", _computer, _title,
            GetNode<Control>("Screen/Computer/Layout/Header"), false);
        BuildDesktopApplications();
        _screen.Resized += LayoutDesktop;
        ApplyDesktopAccessibility(_accessibility);
    }

    private void RegisterDesktopWindow(string id, Control panel, Label title, Control header, bool closeButton = true)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        // Keep the existing Header/Title paths and button handlers while giving
        // all programs the same visible active/inactive window frame.
        var parent = header.GetParent();
        var index = header.GetIndex();
        var bar = new OldPcWindowTitleBar { Name = header.Name, CustomMinimumSize = new(0, 42) };
        parent.RemoveChild(header);
        parent.AddChild(bar);
        parent.MoveChild(bar, index);
        foreach (var child in header.GetChildren()) { header.RemoveChild(child); bar.AddChild(child); }
        header.QueueFree();
        header = bar;
        var glyph = new OldPcDesktopIcon { Name = "Glyph", Kind = id, CustomMinimumSize = new(32, 32),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        header.AddChild(glyph); header.MoveChild(glyph, 0);
        var window = new DesktopWindow(id, panel, title, bar);
        _windows.Add(id, window);
        // Hidden programs also receive text-scale changes. Their wrapping
        // labels settle their minimum height after the container gets a width;
        // refit the window once that layout has settled, including folder changes.
        panel.MinimumSizeChanged += () => CallDeferred(nameof(LayoutDesktop));
        window.Task = DesktopButton(_tasks, "Task_" + id, title.Text, () =>
        {
            if (window.Open && window.Panel.Visible && _focusedWindow == id) MinimizeWindow(id);
            else LaunchApplication(id);
        });
        window.Task.CustomMinimumSize = new Vector2(100, 34);
        window.Task.ToggleMode = true;
        window.Task.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        window.Task.ClipText = true;
        window.Task.Visible = false;
        title.MouseFilter = Control.MouseFilterEnum.Stop;
        title.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
            RaiseWindow(id, focusContent: true);
            if (mouse.DoubleClick) { ToggleMaximize(id); return; }
            if (window.State.Maximized) return;
            _dragWindow = window;
            _dragOffset = _screen.GetGlobalMousePosition() - panel.GlobalPosition;
            title.AcceptEvent();
        };
        DesktopButton(header, "Minimize", "−", () => MinimizeWindow(id)).TooltipText = "Свернуть";
        DesktopButton(header, "Maximize", "□", () => ToggleMaximize(id)).TooltipText = "Развернуть / восстановить";
        DesktopButton(header, "CloseWindow", "×", () => CloseWindow(id)).TooltipText = "Закрыть окно";
        panel.Hide();
    }

    private VBoxContainer CreateDesktopWindow(string id, string title, out Label heading)
    {
        var panel = new PanelContainer { Name = "App_" + id };
        panel.AddThemeStyleboxOverride("panel", DesktopStyle("eeeadd", "376899", 3, 10));
        _screen.AddChild(panel);
        var layout = new VBoxContainer { Name = "Layout" };
        panel.AddChild(layout);
        var header = new HBoxContainer { Name = "Header" };
        layout.AddChild(header);
        heading = new Label { Name = "Title", Text = title, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        heading.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(heading);
        RegisterDesktopWindow(id, panel, heading, header);
        var content = new VBoxContainer { Name = "Content", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(content);
        return content;
    }

    public void LaunchApplication(string id)
    {
        if (_bridge is null || !_windows.TryGetValue(id, out var window)) return;
        var wasOpen = window.Open;
        if (!window.Open)
        {
            window.Open = true;
            window.State = new OldPcWindowSnapshot { Id = id, X = .15f, Y = .07f, Width = .82f, Height = .84f };
            _desktop.Windows.Add(window.State);
        }
        window.State.Minimized = false;
        window.Panel.Show();
        window.Task.Show();
        if (id is "files" or "trash") RefreshFileList(id == "trash");
        if (id == "pictures") RefreshPictures();
        if (id == "chat") RefreshChat();
        if (id == "vocabulary") RefreshVocabulary();
        if (id == "tetris") _tetris.Resume();
        if (id is "notepad" or "writer") RestoreEditor(id == "writer");
        if (id == "browser" && (!wasOpen || _browserHistoryAddress is null))
            ShowBrowserAddress(CurrentBrowserAddress(), false, restoreOnly: true);
        LayoutWindow(window);
        RaiseWindow(id, focusContent: true);
        MarkDesktopChanged();
    }

    private void RestoreDesktop(RuntimeBridge bridge)
    {
        _restoringDesktop = true;
        _desktopSession = bridge.SessionIdentity;
        var snapshot = bridge.OldPcState();
        _desktop = snapshot.TryGetProperty("desktop", out var value)
            ? value.Deserialize<OldPcDesktopSnapshot>(OldPcDesktopSnapshot.JsonOptions) ?? new()
            : new();
        _focusedWindow = _desktop.ActiveWindowId;
        _desktopDirty = false;
        _browserHistoryAddress = null;
        _browserDocumentId = null;
        _browserVersion++;
        _browserExcerpts.Clear();
        _browserImages.SetImages(null);
        foreach (var window in _windows.Values)
        {
            window.Open = false;
            window.Panel.Hide();
            window.Task.Hide();
        }
        if (_desktop.Windows.Count == 0)
            _desktop.Windows.Add(new OldPcWindowSnapshot { Id = "archive", X = .145f, Y = .04f, Width = .84f, Height = .9f });
        foreach (var saved in _desktop.Windows)
        {
            if (!_windows.TryGetValue(saved.Id, out var window)) continue;
            window.State = saved;
            window.Open = true;
            window.Task.Show();
            window.Panel.Visible = !saved.Minimized;
            _screen.MoveChild(window.Panel, _screen.GetChildCount() - 1);
            LayoutWindow(window);
        }
        RefreshFileList(false);
        RefreshFileList(true);
        RefreshPictures();
        RestoreEditor(false);
        RestoreEditor(true);
        RefreshVocabulary();
        if (_windows["browser"].Open) ShowBrowserAddress(CurrentBrowserAddress(), false, restoreOnly: true);
        _startMenu.Hide();
        FrontShell();
        // Triggers are ordinary conditions: opening the computer is when the
        // threads receive whatever the world has unlocked since the last visit.
        RefreshChat();
        RefreshHints();
        RestoreTetrisRecord();
        _restoringDesktop = false;
        LayoutDesktop();
    }

    private void RaiseWindow(string id, bool focusContent = false)
    {
        if (!_windows.TryGetValue(id, out var window) || !window.Open) return;
        _focusedWindow = id;
        _desktop.ActiveWindowId = id;
        _screen.MoveChild(window.Panel, _screen.GetChildCount() - 1);
        _desktop.Windows.Remove(window.State);
        _desktop.Windows.Add(window.State);
        foreach (var other in _windows.Values)
        {
            other.Task.ButtonPressed = other.Id == id && other.Panel.Visible;
            other.Header.Active = other.Id == id && other.Panel.Visible;
        }
        FrontShell();
        if (focusContent) FocusWindowContent(window);
        MarkDesktopChanged();
    }

    private void FocusWindowContent(DesktopWindow window)
    {
        if (!window.Panel.IsVisibleInTree()) return;
        var current = GetViewport().GuiGetFocusOwner();
        if (current is not null && current.IsVisibleInTree()
            && (current == window.Panel || window.Panel.IsAncestorOf(current))) return;
        // Keep typing with the foreground window, including clicks on its
        // non-focusable frame. GUI dispatch can still focus the clicked control.
        Control target = window.Id switch
        {
            "archive" => _query,
            "files" => _fileList,
            "trash" => _trashList,
            "browser" => _browserAddress,
            "pictures" => _pictureList,
            "chat" => _chatThreadList,
            "vocabulary" => _vocabularyList,
            "tetris" => _tetris,
            "writer" when _writerPreview.Visible => _writerPreview,
            "notepad" or "writer" when _editors[window.Id].Editable => _editors[window.Id],
            _ => window.Panel.GetNode<Button>("Layout/Content/Tools/New")
        };
        target.GrabFocus();
    }

    private void FocusActiveDesktopWindow()
    {
        var active = _focusedWindow is { } focused && _windows[focused].Panel.Visible
            ? focused : _desktop.Windows.LastOrDefault(window => !window.Minimized)?.Id;
        if (active is not null) RaiseWindow(active, focusContent: true);
        else _taskbar.GetNode<Button>("Layout/Start").GrabFocus();
    }

    private void MinimizeWindow(string id)
    {
        if (!_windows.TryGetValue(id, out var window) || !window.Open) return;
        window.State.Minimized = true;
        window.Panel.Hide();
        window.Header.Active = false;
        window.Task.ButtonPressed = false;
        if (id == "tetris") _tetris.Pause();
        if (_focusedWindow == id) _focusedWindow = _desktop.ActiveWindowId = null;
        FocusActiveDesktopWindow();
        MarkDesktopChanged();
    }

    public void CloseApplication(string id) => CloseWindow(id);

    private void CloseWindow(string id)
    {
        if (!_windows.TryGetValue(id, out var window) || !window.Open) return;
        window.Open = false;
        window.Panel.Hide();
        window.Task.Hide();
        window.Header.Active = false;
        window.Task.ButtonPressed = false;
        if (id == "tetris") _tetris.Pause();
        _desktop.Windows.Remove(window.State);
        if (_focusedWindow == id) _focusedWindow = _desktop.ActiveWindowId = null;
        if (id == "browser") { _browserVersion++; _browserExcerpts.Clear(); }
        FocusActiveDesktopWindow();
        MarkDesktopChanged();
    }

    private void ToggleMaximize(string id)
    {
        var window = _windows[id];
        window.State.Maximized = !window.State.Maximized;
        LayoutWindow(window);
        RaiseWindow(id);
    }

    private void LayoutDesktop()
    {
        if (_startMenu is null) return;
        _taskbar.OffsetTop = -DesktopBarHeight;
        _startMenu.Position = new Vector2(4, Math.Max(4, _screen.Size.Y - DesktopBarHeight - 4 - _startMenu.Size.Y));
        foreach (var window in _windows.Values.Where(window => window.Open)) LayoutWindow(window);
    }

    private void LayoutWindow(DesktopWindow window)
    {
        var workspace = new Vector2(Math.Max(320, _screen.Size.X), Math.Max(220, _screen.Size.Y - DesktopBarHeight - 4));
        if (window.State.Maximized)
        {
            window.Panel.Position = new Vector2(4, 4);
            window.Panel.Size = workspace - new Vector2(8, 8);
        }
        else
        {
            var minimum = window.Id == "archive" ? new Vector2(700, 360) : new Vector2(570, 330);
            var size = new Vector2(workspace.X * window.State.Width, workspace.Y * window.State.Height);
            size = new Vector2(Math.Clamp(size.X, Math.Min(minimum.X, workspace.X - 8), workspace.X - 8),
                Math.Clamp(size.Y, Math.Min(minimum.Y, workspace.Y - 8), workspace.Y - 8));
            window.Panel.Size = size;
            window.Panel.Position = new Vector2(
                Math.Clamp(workspace.X * window.State.X, 4, Math.Max(4, workspace.X - size.X - 4)),
                Math.Clamp(workspace.Y * window.State.Y, 4, Math.Max(4, workspace.Y - size.Y - 4)));
        }
    }

    private void FrontShell()
    {
        if (_taskbar is null || _startMenu is null) return;
        _screen.MoveChild(_taskbar, _screen.GetChildCount() - 1);
        _screen.MoveChild(_startMenu, _screen.GetChildCount() - 1);
    }

    public override void _Input(InputEvent input)
    {
        if (_screen is null || !_screen.Visible) return;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
        {
            if (!mouse.Pressed && _dragWindow is not null)
            {
                _dragWindow = null;
                MarkDesktopChanged();
            }
            if (mouse.Pressed && !_taskbar.GetGlobalRect().HasPoint(mouse.Position)
                && !(_startMenu.Visible && _startMenu.GetGlobalRect().HasPoint(mouse.Position)))
            {
                _startMenu.Hide();
                var hit = _windows.Values.Where(window => window.Open && window.Panel.Visible
                    && window.Panel.GetGlobalRect().HasPoint(mouse.Position))
                    .OrderByDescending(window => window.Panel.GetIndex()).FirstOrDefault();
                if (hit is not null) RaiseWindow(hit.Id, focusContent: true);
            }
        }
        else if (input is InputEventMouseMotion motion && _dragWindow is { } dragging)
        {
            var desired = motion.Position - _dragOffset;
            var width = Math.Max(1, _screen.Size.X);
            var height = Math.Max(1, _screen.Size.Y - DesktopBarHeight - 4);
            dragging.State.X = Math.Clamp(desired.X / width, 0, 1);
            dragging.State.Y = Math.Clamp(desired.Y / height, 0, 1);
            LayoutWindow(dragging);
            MarkDesktopChanged();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, AltPressed: true, Keycode: Key.Tab })
        {
            var open = _desktop.Windows.Where(window => !window.Minimized).Select(window => window.Id).ToArray();
            if (open.Length > 1) RaiseWindow(open[0], focusContent: true);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_screen is null || !_screen.Visible || _bridge is null) return;
        _desktop.ElapsedSeconds += delta;
        var clock = TimeSpan.FromMinutes(14 * 60 + 25) + TimeSpan.FromSeconds(_desktop.ElapsedSeconds);
        _clock.Text = $"{clock.Hours:00}:{clock.Minutes:00}";
        _clockCheckpoint += delta;
        if (_clockCheckpoint > 10) { _clockCheckpoint = 0; MarkDesktopChanged(); }
        if (_desktopDirty && (_desktopSaveDelay -= delta) <= 0) PersistDesktop();
    }

    private void MarkDesktopChanged()
    {
        if (_restoringDesktop || _bridge is null) return;
        _desktopDirty = true;
        _desktopSaveDelay = .25;
    }

    private async void PersistDesktop()
    {
        if (_restoringDesktop || _bridge is not { } bridge
            || !ReferenceEquals(_desktopSession, bridge.SessionIdentity)) return;
        _desktopDirty = false;
        try
        {
            // The presentation input is synchronous within the shared capability;
            // the Task API is reused so the UI has no parallel save owner.
            await bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new {
                type = "desktop", desktop = JsonSerializer.SerializeToElement(_desktop, OldPcDesktopSnapshot.JsonOptions)
            }));
        }
        catch (Exception error)
        {
            // A rejected desktop write leaves the capability with stale state;
            // it must be visible instead of failing silently.
            GD.PrintErr($"oldpc-desktop: desktop state was not saved: {error.Message}");
            if (ReferenceEquals(_desktopSession, bridge.SessionIdentity))
                _status.Text = "Не удалось сохранить рабочий стол: " + error.Message;
        }
    }

    private void ApplyDesktopAccessibility(AccessibilitySettingsSnapshot settings)
    {
        if (_desktopBackground is null) return;
        AccessibilityPresentation.ApplyToControl(_screen, settings);
        var paper = settings.HighContrast ? "131923" : "fffdf3";
        var ink = settings.HighContrast ? Colors.White : new Color("263143");
        var panel = settings.HighContrast ? "111722" : "eeeadd";
        foreach (var window in _windows.Values)
        {
            if (window.Panel is PanelContainer pane)
            {
                // XP window: beige body in a deep blue frame, rounded on top.
                var frame = DesktopStyle(settings.HighContrast ? panel : "ece9d8", settings.HighContrast ? "ffffff" : "0831d9", 3, 10);
                frame.CornerRadiusTopLeft = frame.CornerRadiusTopRight = 8;
                pane.AddThemeStyleboxOverride("panel", frame);
            }
            window.Title.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color("174773"));
        }
        foreach (var node in _screen.FindChildren("*", "Control", true, false))
        {
            if (node is Label label)
            {
                label.AddThemeFontSizeOverride("font_size", (int)(22 * settings.TextScale));
                label.AddThemeColorOverride("font_color", ink);
                label.AddThemeConstantOverride("shadow_offset_x", 0);
                label.AddThemeConstantOverride("shadow_offset_y", 0);
            }
            if (node is RichTextLabel rich)
            {
                foreach (var metric in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
                    rich.AddThemeFontSizeOverride(metric, (int)(24 * settings.TextScale));
                rich.AddThemeColorOverride("default_color", ink);
                rich.AddThemeStyleboxOverride("normal", DesktopStyle(paper, "b5b4aa", 1, 8));
                rich.AddThemeStyleboxOverride("focus", DesktopFocus(settings.HighContrast));
            }
            if (node is LineEdit line)
            {
                line.AddThemeFontSizeOverride("font_size", (int)(22 * settings.TextScale));
                line.AddThemeStyleboxOverride("normal", DesktopStyle(paper, "959b9e", 1, 6));
                line.AddThemeStyleboxOverride("focus", DesktopFocus(settings.HighContrast));
                line.AddThemeColorOverride("font_color", ink);
                line.AddThemeColorOverride("font_placeholder_color", new Color("747d85"));
            }
            if (node is TextEdit edit)
            {
                edit.AddThemeStyleboxOverride("normal", DesktopStyle(paper, "a6aaa9", 1, 9));
                edit.AddThemeStyleboxOverride("read_only", DesktopStyle(paper, "a6aaa9", 1, 9));
                edit.AddThemeStyleboxOverride("focus", DesktopFocus(settings.HighContrast));
                edit.AddThemeColorOverride("font_color", ink);
                edit.AddThemeFontSizeOverride("font_size", (int)(24 * settings.TextScale));
            }
            if (node is ItemList list)
            {
                list.AddThemeFontSizeOverride("font_size", (int)(24 * settings.TextScale));
                list.AddThemeStyleboxOverride("panel", DesktopStyle(paper, "a6aaa9", 1, 5));
                list.AddThemeStyleboxOverride("focus", DesktopFocus(settings.HighContrast));
                list.AddThemeColorOverride("font_color", ink);
                list.AddThemeColorOverride("font_selected_color", settings.HighContrast ? Colors.White : new Color("183c5d"));
            }
            if (node is Button button)
            {
                button.AddThemeFontSizeOverride("font_size", (int)(22 * settings.TextScale));
                button.AddThemeStyleboxOverride("normal", DesktopStyle(settings.HighContrast ? panel : "f7f6f0", settings.HighContrast ? "9ba6af" : "003c74", 1, 6));
                button.AddThemeStyleboxOverride("hover", DesktopStyle(settings.HighContrast ? "254461" : "fdf3dc", settings.HighContrast ? "5685ad" : "e8a33c", settings.HighContrast ? 1 : 2, 6));
                button.AddThemeStyleboxOverride("pressed", DesktopStyle(settings.HighContrast ? "254461" : "e2dfd2", settings.HighContrast ? "366995" : "003c74", 1, 6));
                button.AddThemeStyleboxOverride("focus", DesktopFocus(settings.HighContrast));
                button.AddThemeColorOverride("font_color", ink);
                button.AddThemeColorOverride("font_hover_color", ink);
                button.AddThemeColorOverride("font_pressed_color", ink);
                button.AddThemeColorOverride("font_focus_color", ink);
                button.AddThemeColorOverride("font_disabled_color", settings.HighContrast ? new Color("9da3aa") : new Color("7b828b"));
                button.AddThemeConstantOverride("outline_size", 0);
            }
        }
        foreach (var window in _windows.Values)
        {
            window.Title.AddThemeColorOverride("font_color", Colors.White);
            window.Title.AddThemeFontSizeOverride("font_size", (int)(24 * settings.TextScale));
            window.Header.Active = window.Id == _focusedWindow && window.Panel.Visible;
        }
        _shortcuts.Columns = settings.TextScale > 1.25 ? 2 : 1;
        foreach (var shortcut in _shortcuts.GetChildren().OfType<Button>())
        {
            var sizeScale = Math.Max(1f, (float)settings.TextScale);
            shortcut.CustomMinimumSize = new Vector2(190, 126) * sizeScale;
            var clear = DesktopStyle("00000000", "00000000", 0, 8); clear.ContentMarginTop = 66;
            var hover = DesktopStyle("2e628caa", "a6c4df", 1, 8); hover.ContentMarginTop = 66;
            var selected = DesktopStyle("1f537ecc", "e3eff8", 1, 8); selected.ContentMarginTop = 66;
            shortcut.AddThemeStyleboxOverride("normal", clear);
            shortcut.AddThemeStyleboxOverride("hover", hover);
            shortcut.AddThemeStyleboxOverride("pressed", selected);
            shortcut.AddThemeColorOverride("font_color", Colors.White);
            shortcut.AddThemeColorOverride("font_hover_color", Colors.White);
            shortcut.AddThemeColorOverride("font_pressed_color", Colors.White);
            shortcut.AddThemeColorOverride("font_focus_color", Colors.White);
            shortcut.AddThemeColorOverride("font_outline_color", new Color("233a49"));
            shortcut.AddThemeConstantOverride("outline_size", 2);
            shortcut.AddThemeColorOverride("font_shadow_color", new Color("233a49"));
            shortcut.AddThemeConstantOverride("shadow_offset_x", 1);
            shortcut.AddThemeConstantOverride("shadow_offset_y", 1);
        }
        _shortcuts.ResetSize();
        ApplyXpChrome(settings);
        ApplySocialPresentation();
        _clock.AddThemeColorOverride("font_color", Colors.White);
        _excerpts?.ApplyPresentation();
        _browserExcerpts?.ApplyPresentation();
        LayoutDesktop();
    }

    private static Button DesktopButton(Node parent, string name, string title, Action pressed)
    {
        var button = new Button { Name = name, Text = title, FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(36, 34) };
        button.AddThemeFontSizeOverride("font_size", 16);
        parent.AddChild(button);
        button.Pressed += pressed;
        return button;
    }

    private static StyleBoxFlat DesktopStyle(string background, string border, int thickness, int margin)
    {
        return new StyleBoxFlat {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = thickness, BorderWidthRight = thickness,
            BorderWidthTop = thickness, BorderWidthBottom = thickness,
            ContentMarginLeft = margin, ContentMarginRight = margin,
            ContentMarginTop = margin, ContentMarginBottom = margin,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2
        };
    }

    private static StyleBoxFlat DesktopFocus(bool highContrast)
    {
        // Godot draws this over the normal control. Keep the readable paper
        // surface; the legacy archive focus filled it with dark green.
        var focus = DesktopStyle("000000", highContrast ? "ffffff" : "326f9d", 2, 6);
        focus.DrawCenter = false;
        return focus;
    }
}
