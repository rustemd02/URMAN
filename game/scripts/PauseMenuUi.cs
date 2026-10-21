using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// UIUX-005 pause shell. Owns the pause action routing (open/resume, settings
/// stacking, safe resume) and the in-pause save/load/restart/menu/quit
/// actions. Restart and quit require a two-step confirmation; the shell holds
/// no narrative state — everything runs through the RuntimeBridge lifecycle.
/// </summary>
public partial class PauseMenuUi : CanvasLayer, IAccessibilitySettingsTarget
{
    public const string ContinueSlot = MainMenuUi.ContinueSlot;

    private Label? _status;
    private Label? _title;
    private ColorRect? _screen;
    private PanelContainer? _panel;
    private Button? _resumeButton;
    private Button? _saveButton;
    private Button? _loadButton;
    private Button? _settingsButton;
    private Button? _restartButton;
    private Button? _mainMenuButton;
    private Button? _quitButton;
    private Button? _armedButton;
    private bool _open;
    private bool _backgroundInputReplay;
    private bool _reducedMotion;
    private bool _shutdownRequested;
    private int _activeAsyncActions;
    private TaskCompletionSource<bool>? _asyncActionsDrained;

    /// <summary>The demo root decides when the pause shell may open.</summary>
    public Func<bool>? CanOpenPause { get; set; }

    /// <summary>Raised when the player confirms returning to the main menu.</summary>
    public event Action? ShowMainMenuRequested;

    /// <summary>Raised after the player confirms the two-step quit action.</summary>
    public event Action? QuitRequested;

    /// <summary>Publishes this shell's actual pause state in the same call that changes it.</summary>
    public event Action<bool>? PauseChanged;

    public bool IsOpen => _open;
    internal string LastTransitionReason { get; private set; } = "none";
    internal ulong LastTransitionFrame { get; private set; }

    public Button? ResumeButton => _resumeButton;
    public Button? SaveButton => _saveButton;
    public Button? LoadButton => _loadButton;
    public Button? SettingsButton => _settingsButton;
    public Button? RestartButton => _restartButton;
    public Button? MainMenuButton => _mainMenuButton;

    internal void BeginShutdown() => _shutdownRequested = true;

    internal Task WaitForActiveActionsAsync() => _activeAsyncActions == 0
        ? Task.CompletedTask
        : _asyncActionsDrained?.Task ?? Task.CompletedTask;

    public override void _Ready()
    {
        AddToGroup("pause_menu");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        Layer = 90;
        Name = "Act1PauseMenu";
        Visible = false;
        // Windowed input replay can lose desktop focus to the test host. This
        // opt-out requires both an explicit diagnostic argument and a test
        // scene; the normal game always retains automatic focus-loss pause.
        var arguments = OS.GetCmdlineArgs();
        _backgroundInputReplay = arguments.Contains("--urman-smoke-background-input", StringComparer.Ordinal)
            && arguments.Any(argument => argument.StartsWith("res://tests/", StringComparison.Ordinal)
                && argument.EndsWith(".tscn", StringComparison.Ordinal));
        if (_backgroundInputReplay) GD.Print("act1-smoke: background input replay; focus-loss pause isolated from this test scene");
        BuildLayout();
        ApplyAccessibilitySettings(
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    public override void _Notification(int what)
    {
        if (!_shutdownRequested && what == NotificationWMWindowFocusOut
            && !_backgroundInputReplay
            && !_open
            && CanOpenPause?.Invoke() == true
            && FindPlayer() is { ModalOpen: false })
        {
            OpenFrom("window-focus-out");
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        // ACT1-UI.2: colour, type and plates come from the shared theme; the
        // accessibility pass swaps it for high contrast and scales the text.
        _reducedMotion = settings.ReducedMotion;
        if (_screen is not null)
        {
            AccessibilityPresentation.ApplyToControl(_screen, settings);
            _screen.Color = UrmanUiTheme.Colours(settings).Shade with { A = settings.HighContrast ? .92f : .78f };
        }

        if (_panel is not null)
        {
            _panel.SetMeta("accessibilityTextScale", settings.TextScale);
            _panel.SetMeta("accessibilityHighContrast", settings.HighContrast);
            _panel.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_shutdownRequested || !inputEvent.IsActionPressed("pause"))
        {
            return;
        }

        if (!_open && FindPlayer() is { ModalOpen: false, IsClimbingLadder: true } climbingPlayer)
        {
            climbingPlayer.RequestLadderReturn();
            GetViewport().SetInputAsHandled();
            return;
        }

        var settings = GetTree().GetFirstNodeInGroup("settings_ui") as SettingsUi;
        if (settings is { IsOpen: true })
        {
            // The settings panel is stacked on top of the pause shell: the
            // pause key closes the panel and returns to the shell. Close()
            // releases the player for gameplay, so the shell re-asserts the
            // modal gate while it stays open.
            settings.Close();
            FindPlayer()?.SetModalOpen(true);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_open)
        {
            Resume();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (CanOpenPause?.Invoke() == true
            && FindPlayer() is { ModalOpen: false })
        {
            OpenFrom("pause-action");
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
        => OpenFrom("explicit-call");

    private void OpenFrom(string reason)
    {
        _open = true;
        LastTransitionReason = reason;
        LastTransitionFrame = Engine.GetProcessFrames();
        Visible = true;
        DisarmAll();
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(true);
        UiFoley.SetWorldPaused(GetTree(), true);
        PauseChanged?.Invoke(true);
        FindPlayer()?.SetModalOpen(true);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetStatus(string.Empty);
        if (_panel is not null) UrmanUiTheme.PlayOpen(_panel, _reducedMotion);
        _resumeButton?.GrabFocus();
        GD.Print($"pause-menu: opened reason={reason} frame={LastTransitionFrame} focused={DisplayServer.WindowIsFocused()} mouseMode={Input.MouseMode}");
    }

    public void Resume()
    {
        if (_shutdownRequested) return;
        _open = false;
        LastTransitionReason = "resume";
        LastTransitionFrame = Engine.GetProcessFrames();
        Visible = false;
        DisarmAll();
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(false);
        UiFoley.SetWorldPaused(GetTree(), false);
        PauseChanged?.Invoke(false);
        FindPlayer()?.SetModalOpen(false);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print($"pause-menu: resumed frame={LastTransitionFrame} focused={DisplayServer.WindowIsFocused()} mouseMode={Input.MouseMode}");
    }

    private void BuildLayout()
    {
        var screen = new ColorRect
        {
            Name = "PauseScreen",
            Color = UrmanUiTheme.Normal.Shade with { A = .78f },
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(screen);
        _screen = screen;

        var center = new CenterContainer
        {
            Name = "Center",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both
        };
        screen.AddChild(center);

        _panel = new PanelContainer
        {
            Name = "PausePanel",
            ThemeTypeVariation = UrmanUiTheme.Plate,
            CustomMinimumSize = new Vector2(560, 0)
        };
        center.AddChild(_panel);

        var margin = new MarginContainer { Name = "Margin" };
        _panel.AddChild(margin);

        var layout = new VBoxContainer
        {
            Name = "Layout",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        layout.AddThemeConstantOverride("separation", UrmanUiTheme.Space.S + UrmanUiTheme.Space.Xs);
        margin.AddChild(layout);

        _title = new Label
        {
            Name = "Title",
            Text = "ПАУЗА",
            ThemeTypeVariation = UrmanUiTheme.Heading,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        layout.AddChild(_title);

        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        _resumeButton = ShellButton("ResumeButton", "Продолжить");
        _resumeButton.Pressed += Resume;
        layout.AddChild(_resumeButton);

        _saveButton = ShellButton("SaveButton", "Сохранить");
        _saveButton.Pressed += () => _ = SaveAsync();
        layout.AddChild(_saveButton);

        _loadButton = ShellButton("LoadButton", "Загрузить");
        _loadButton.Pressed += () => _ = LoadAsync();
        layout.AddChild(_loadButton);

        _settingsButton = ShellButton("SettingsButton", "Настройки");
        _settingsButton.Pressed += OpenSettings;
        layout.AddChild(_settingsButton);

        _restartButton = ShellButton("RestartButton", "Заново");
        _restartButton.Pressed += () => RunConfirmed(_restartButton, "Заново", () => _ = RestartAsync());
        layout.AddChild(_restartButton);

        _mainMenuButton = ShellButton("MainMenuButton", "В главное меню");
        _mainMenuButton.Pressed += () => RunConfirmed(_mainMenuButton, "В главное меню", ShowMainMenu);
        layout.AddChild(_mainMenuButton);

        _quitButton = ShellButton("QuitButton", "Выход");
        _quitButton.Pressed += () => RunConfirmed(_quitButton, "Выход", () => QuitRequested?.Invoke());
        layout.AddChild(_quitButton);

        _status = new Label
        {
            Name = "Status",
            ThemeTypeVariation = UrmanUiTheme.Hint,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layout.AddChild(_status);

        _resumeButton.GrabFocus();
    }

    private static Button ShellButton(string name, string text) => new()
    {
        Name = name,
        Text = text,
        ThemeTypeVariation = UrmanUiTheme.MenuButton,
        Alignment = HorizontalAlignment.Left,
        CustomMinimumSize = new Vector2(360, 56),
        FocusMode = Control.FocusModeEnum.All,
        MouseDefaultCursorShape = Control.CursorShape.PointingHand
    };

    private async Task SaveAsync()
    {
        if (_shutdownRequested) return;
        BeginAsyncAction();
        try
        {
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (bridge is null)
            {
                SetStatus("Сохранение недоступно.");
                return;
            }

            SetStatus(await bridge.SaveSlotAsync(ContinueSlot) ? "Сохранено."
                : FindPlayer()?.SaveBlockReason ?? "Не удалось сохранить.");
        }
        finally
        {
            EndAsyncAction();
        }
    }

    private async Task LoadAsync()
    {
        if (_shutdownRequested) return;
        BeginAsyncAction();
        try
        {
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (bridge is null || !bridge.HasLoadableSlot(ContinueSlot))
            {
                SetStatus("Загрузка недоступна.");
                return;
            }

            if (await bridge.LoadSlotAsync(ContinueSlot))
            {
                Resume();
            }
            else
            {
                SetStatus("Не удалось загрузить.");
            }
        }
        finally
        {
            EndAsyncAction();
        }
    }

    private void OpenSettings()
    {
        if (_shutdownRequested) return;
        if (GetTree().GetFirstNodeInGroup("settings_ui") as SettingsUi is { } settings
            && FindPlayer() is { } player)
        {
            settings.Open(player);
        }
    }

    private async Task RestartAsync()
    {
        if (_shutdownRequested) return;
        BeginAsyncAction();
        try
        {
            var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
            if (bridge is null)
            {
                return;
            }

            if (await bridge.StartNewGameAsync())
            {
                Resume();
            }
            else
            {
                SetStatus("Перезапуск недоступен.");
            }
        }
        finally
        {
            EndAsyncAction();
        }
    }

    private void ShowMainMenu()
    {
        if (_shutdownRequested) return;
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.ResetPresentation();
        UiFoley.StopWorld(GetTree());
        Resume();
        ShowMainMenuRequested?.Invoke();
    }

    private void RunConfirmed(Button button, string baseText, Action action)
    {
        if (_shutdownRequested) return;
        if (ReferenceEquals(_armedButton, button))
        {
            DisarmAll();
            button.Text = baseText;
            action();
            return;
        }

        DisarmAll();
        _armedButton = button;
        button.Text = $"{baseText} — точно?";
    }

    private void BeginAsyncAction()
    {
        if (_activeAsyncActions++ == 0)
            _asyncActionsDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void EndAsyncAction()
    {
        if (--_activeAsyncActions == 0)
        {
            _asyncActionsDrained?.TrySetResult(true);
            _asyncActionsDrained = null;
        }
    }

    private void DisarmAll()
    {
        _armedButton = null;
        if (_restartButton is not null)
        {
            _restartButton.Text = "Заново";
        }

        if (_mainMenuButton is not null)
        {
            _mainMenuButton.Text = "В главное меню";
        }

        if (_quitButton is not null)
        {
            _quitButton.Text = "Выход";
        }
    }

    private void SetStatus(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    private FirstPersonController? FindPlayer() =>
        GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
}
