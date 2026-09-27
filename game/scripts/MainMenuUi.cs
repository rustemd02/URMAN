using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// UIUX-001 public main menu overlay. Pure presentation: it owns no runtime,
/// session or save state — choices are reported as events and the Act I demo
/// root executes them through the RuntimeBridge lifecycle APIs. Gameplay input
/// stays gated (player modal) until a choice starts the session.
/// </summary>
public partial class MainMenuUi : CanvasLayer, IAccessibilitySettingsTarget
{
    public const string ContinueSlot = "quick";

    /// <summary>SAVE-004: the runtime also maintains a rolling checkpoint
    /// slot; Continue selects the newest valid quick/checkpoint payload.</summary>
    public const string CheckpointSlot = "checkpoint";

    private Label? _title;
    private Label? _subtitle;
    private Label? _continueHint;
    private ColorRect? _screen;
    private MarginContainer? _column;
    private PanelContainer? _panel;
    private Button? _newGameButton;
    private Button? _continueButton;
    private Button? _replayIntroButton;
    private Button? _settingsButton;
    private Button? _quitButton;
    private Button? _aboutButton;
    private Button? _debugButton;
    private VBoxContainer? _layout;
    private VBoxContainer? _about;
    private VBoxContainer? _debugZones;
    private ScrollContainer? _scroll;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private bool _continueAvailable;
    private bool _existingSavePresent;
    private bool _newGameArmed;
    private string? _continueDescription;

    // Development aid, not part of the game: jumping straight into an authored
    // zone so the author can look at a place without walking the route. The entry
    // appears only when user://debug-zones.enabled exists, so a build without that
    // file shows the ordinary menu and the check is testable.
    private static readonly (string ZoneId, string SpawnPointId, string Label)[] DebugZones =
    [
        ("village_day", "arrival", "Кара-Урман · остановка"),
        ("village_day", "from_house", "Кара-Урман · от дома"),
        ("house_old_pc", "entry", "Дом Мансура и Гөлсинә"),
        ("village_day", "shop", "Магазин"),
        ("village_day", "school", "Школа"),
        ("village_day", "council", "Сельсовет / ДК"),
        ("village_day", "mosque", "Мечеть"),
        ("village_day", "bathhouse", "Баня бабая"),
        ("fap_clinic", "waiting_room", "ФАП"),
        ("zirat_road", "village_side", "Зиратская дорога"),
        ("kara_urman_night", "village_path", "Кромка Кара-Урмана · ночь"),
        ("kara_urman_night", "forest-approach", "Кара-Урман · подход к лесу")
    ];

    public static bool DebugZonesEnabled =>
        global::Godot.FileAccess.FileExists("user://debug-zones.enabled");

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? ReplayIntroRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;
    public event Action<string, string>? DebugZoneRequested;

    public Button? NewGameButton => _newGameButton;
    public Button? ContinueButton => _continueButton;
    public Button? ReplayIntroButton => _replayIntroButton;
    public Button? SettingsButton => _settingsButton;
    public Button? AboutButton => _aboutButton;
    public bool IsDismissed { get; private set; }

    public override void _Ready()
    {
        AddToGroup("main_menu");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        Layer = 100;
        Name = "Act1MainMenu";
        BuildLayout();
        GetViewport().SizeChanged += FitToViewport;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        ApplyAccessibilitySettings(
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= FitToViewport;

    private void FitToViewport()
    {
        var size = GetViewport().GetVisibleRect().Size;
        if (_scroll is not null) _scroll.CustomMinimumSize = new Vector2(0, Math.Max(200, Math.Min(560, size.Y - 116)));
        if (_panel is not null) _panel.CustomMinimumSize = new Vector2(Math.Min(560, size.X - 32), 0);
        // Persona-like composition: the menu column stands left of centre and
        // leaves the illustration open; a narrow window falls back to a gutter.
        if (_column is not null && _panel is not null)
            _column.AddThemeConstantOverride("margin_left",
                Mathf.RoundToInt(Math.Clamp(size.X * .07f, 16f, Math.Max(16f, size.X - _panel.CustomMinimumSize.X - 16f))));
    }

    public void SetContinueAvailable(
        bool available,
        string? description = null,
        bool existingSavePresent = false)
    {
        _continueAvailable = available;
        _existingSavePresent = existingSavePresent || available;
        _continueDescription = description;
        if (_continueButton is not null)
        {
            _continueButton.Visible = available;
            _continueButton.Disabled = !available;
            _continueButton.FocusMode = available
                ? Control.FocusModeEnum.All
                : Control.FocusModeEnum.None;
        }

        if (_continueHint is not null)
        {
            _continueHint.Visible = true;
            _continueHint.Text = available
                ? description ?? "Последнее сохранение"
                : existingSavePresent
                    ? "Продолжить · не удалось загрузить подходящее сохранение\nФайлы сохранений оставлены без изменений."
                    : "Продолжить · подходящее сохранение не найдено";
        }
    }

    public void ShowStatus(string text)
    {
        if (_continueHint is not null) { _continueHint.Text = text; _continueHint.Visible = true; }
    }

    public void Dismiss()
    {
        if (IsDismissed)
        {
            return;
        }

        IsDismissed = true;
        QueueFree();
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        // ACT1-UI.2: colour, type and plates come from the shared theme; the
        // accessibility pass swaps it for high contrast and scales the text.
        if (_screen is not null) AccessibilityPresentation.ApplyToControl(_screen, settings);
        if (_screen is not null) _screen.Color = UrmanUiTheme.Colours(settings).Shade with { A = settings.HighContrast ? .72f : .30f };
        if (_panel is not null)
        {
            _panel.SetMeta("accessibilityTextScale", settings.TextScale);
            _panel.SetMeta("accessibilityHighContrast", settings.HighContrast);
            _panel.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
            // ACT1-UI.2: per-widget colour and type overrides are gone; the
            // shared theme (and the high-contrast swap) carries them.
        }
        FitToViewport();
    }

    private void BuildLayout()
    {
        AddChild(new TextureRect
        {
            Name = "MenuArt",
            Texture = ResourceLoader.Load<Texture2D>("res://assets/textures/ui/act1_menu_winter_v1.png")
                ?? throw new InvalidOperationException("Act I menu illustration is missing."),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear
        });
        var screen = new ColorRect
        {
            Name = "MenuScreen",
            Color = new Color(0.008f, 0.012f, 0.011f, 0.30f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(screen);
        _screen = screen;

        _column = new MarginContainer
        {
            Name = "Center",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        screen.AddChild(_column);
        var column = new VBoxContainer { Name = "Column", Alignment = BoxContainer.AlignmentMode.Center };
        _column.AddChild(column);

        _panel = new PanelContainer
        {
            Name = "MenuPanel",
            ThemeTypeVariation = UrmanUiTheme.Plate,
            CustomMinimumSize = new Vector2(560, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        column.AddChild(_panel);

        var margin = new MarginContainer { Name = "Margin" };
        _panel.AddChild(margin);

        _scroll = new ScrollContainer { Name = "MenuScroll", FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, Math.Min(560, GetViewport().GetVisibleRect().Size.Y - 116)) };
        margin.AddChild(_scroll);
        var layout = new VBoxContainer
        {
            Name = "Layout",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        layout.AddThemeConstantOverride("separation", UrmanUiTheme.Space.S + UrmanUiTheme.Space.Xs);
        var contents = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll.AddChild(contents);
        contents.AddChild(layout);
        _layout = layout;

        _title = new Label
        {
            Name = "Title",
            Text = "УРМАН",
            ThemeTypeVariation = UrmanUiTheme.Title,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        layout.AddChild(_title);

        _subtitle = new Label
        {
            Name = "Subtitle",
            Text = "АКТ I · КАРА-УРМАН",
            ThemeTypeVariation = UrmanUiTheme.Tag,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        layout.AddChild(_subtitle);

        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        _newGameButton = MenuButton("NewGameButton", "Новая игра");
        _newGameButton.Pressed += () =>
        {
            if (_existingSavePresent && !_newGameArmed)
            {
                _newGameArmed = true;
                _newGameButton.Text = "Начать новую игру";
                ShowStatus("Автосохранение будет заменяться.\nРучное сохранение заменится, если сохранить игру. Esc — отмена.");
                return;
            }
            DisarmNewGame();
            NewGameRequested?.Invoke();
        };
        layout.AddChild(_newGameButton);

        _continueButton = MenuButton("ContinueButton", "Продолжить");
        _continueButton.Pressed += () => { DisarmNewGame(); ContinueRequested?.Invoke(); };
        _continueButton.Visible = false;
        layout.AddChild(_continueButton);

        _continueHint = new Label
        {
            Name = "ContinueUnavailable",
            Text = "Продолжить  ·  сохранение не найдено",
            ThemeTypeVariation = UrmanUiTheme.Hint,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(360, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layout.AddChild(_continueHint);

        _replayIntroButton = MenuButton("ReplayIntroButton", "Посмотреть вступление");
        _replayIntroButton.Pressed += () => { DisarmNewGame(); ReplayIntroRequested?.Invoke(); };
        layout.AddChild(_replayIntroButton);

        _settingsButton = MenuButton("SettingsButton", "Настройки");
        _settingsButton.Pressed += () => { DisarmNewGame(); SettingsRequested?.Invoke(); };
        layout.AddChild(_settingsButton);

        _aboutButton = MenuButton("AboutButton", "Об игре и титры");
        _aboutButton.Pressed += OpenAbout;
        layout.AddChild(_aboutButton);

        if (DebugZonesEnabled)
        {
            _debugButton = MenuButton("DebugZonesButton", "Отладка: локации");
            _debugButton.Pressed += OpenDebugZones;
            layout.AddChild(_debugButton);
        }

        _quitButton = MenuButton("QuitButton", "Выход");
        _quitButton.Pressed += () => QuitRequested?.Invoke();
        layout.AddChild(_quitButton);

        _newGameButton.GrabFocus();
        SetContinueAvailable(_continueAvailable);
        UrmanUiTheme.PlayOpen(_panel, _accessibility.ReducedMotion);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_about is not null && inputEvent.IsActionPressed("ui_cancel"))
        { CloseAbout(); GetViewport().SetInputAsHandled(); }
        else if (_debugZones is not null && inputEvent.IsActionPressed("ui_cancel"))
        { CloseDebugZones(); GetViewport().SetInputAsHandled(); }
        else if (_newGameArmed && inputEvent.IsActionPressed("ui_cancel"))
        { DisarmNewGame(); GetViewport().SetInputAsHandled(); }
    }

    private void DisarmNewGame()
    {
        _newGameArmed = false;
        if (_newGameButton is not null) _newGameButton.Text = "Новая игра";
        SetContinueAvailable(_continueAvailable, _continueDescription, _existingSavePresent);
    }

    private void OpenAbout()
    {
        if (_about is not null || _scroll is null || _layout is null) return;
        DisarmNewGame();
        _layout.Hide();        _about = new VBoxContainer { Name = "About", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _layout.GetParent().AddChild(_about);
        var back = MenuButton("Back", "Назад");
        back.Pressed += CloseAbout;
        _about.AddChild(back);
        var text = new RichTextLabel { Name = "Credits", FitContent = true, ScrollActive = false,
            SelectionEnabled = true, CustomMinimumSize = new Vector2(360, 0),
            Text = global::Godot.FileAccess.GetFileAsString("res://content/credits.ru.txt") };
        _about.AddChild(text);
        var licenses = MenuButton("Licenses", "Лицензии Godot");
        _about.AddChild(licenses);
        licenses.Pressed += () =>
        {
            text.Text = Engine.GetLicenseText() + "\n\n" + global::Godot.Json.Stringify(Engine.GetCopyrightInfo(), "  ")
                + "\n\n" + string.Join("\n\n", Engine.GetLicenseInfo().Select(entry => $"{entry.Key}\n{entry.Value}"));
            licenses.Disabled = true;
            _scroll.ScrollVertical = 0;
            back.GrabFocus();
        };
        AccessibilityPresentation.ApplyToControl(_about, _accessibility);
        _scroll.ScrollVertical = 0;
        back.GrabFocus();
    }

    private void CloseAbout()
    {
        _about?.QueueFree();
        _about = null;
        _layout?.Show();
        _aboutButton?.GrabFocus();
    }

    /// <summary>
    /// Development aid: pick an authored zone and drop straight into it instead of
    /// walking the route. Reachable only while user://debug-zones.enabled exists,
    /// so it never appears in a build that ships without that file.
    /// </summary>
    private void OpenDebugZones()
    {
        if (_debugZones is not null || _scroll is null || _layout is null) return;
        DisarmNewGame();
        _layout.Hide();
        _debugZones = new VBoxContainer { Name = "DebugZones", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _layout.GetParent().AddChild(_debugZones);
        var back = MenuButton("Back", "Назад");
        back.Pressed += CloseDebugZones;
        _debugZones.AddChild(back);
        _debugZones.AddChild(new Label
        {
            Name = "DebugZonesNote",
            ThemeTypeVariation = UrmanUiTheme.Hint,
            Text = "Отладка: переход в локацию начинает новый сеанс. "
                + "Меню появляется только при наличии файла debug-zones.enabled рядом с сохранениями и в игру для игроков не входит.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(360, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        foreach (var (zoneId, spawnPointId, label) in DebugZones)
        {
            var zone = zoneId;
            var spawn = spawnPointId;
            var button = MenuButton($"DebugZone_{zone}_{spawn}", label);
            button.Pressed += () =>
            {
                CloseDebugZones();
                DebugZoneRequested?.Invoke(zone, spawn);
            };
            _debugZones.AddChild(button);
        }

        AccessibilityPresentation.ApplyToControl(_debugZones, _accessibility);
        _scroll.ScrollVertical = 0;
        back.GrabFocus();
    }

    private void CloseDebugZones()
    {
        _debugZones?.QueueFree();
        _debugZones = null;
        _layout?.Show();
        _debugButton?.GrabFocus();
    }

    private static Button MenuButton(string name, string text) => new()
    {
        Name = name,
        Text = text,
        ThemeTypeVariation = UrmanUiTheme.MenuButton,
        Alignment = HorizontalAlignment.Left,
        CustomMinimumSize = new Vector2(360, 56),
        FocusMode = Control.FocusModeEnum.All,
        MouseDefaultCursorShape = Control.CursorShape.PointingHand
    };
}
