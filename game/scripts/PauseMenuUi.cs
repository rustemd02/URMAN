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

    /// <summary>The demo root decides when the pause shell may open.</summary>
    public Func<bool>? CanOpenPause { get; set; }

    /// <summary>Raised when the player confirms returning to the main menu.</summary>
    public event Action? ShowMainMenuRequested;

    public bool IsOpen => _open;

    public Button? ResumeButton => _resumeButton;
    public Button? SaveButton => _saveButton;
    public Button? LoadButton => _loadButton;
    public Button? SettingsButton => _settingsButton;
    public Button? RestartButton => _restartButton;
    public Button? MainMenuButton => _mainMenuButton;

    public override void _Ready()
    {
        AddToGroup("pause_menu");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        Layer = 90;
        Name = "Act1PauseMenu";
        Visible = false;
        BuildLayout();
        ApplyAccessibilitySettings(
            (GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController)?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut
            && !_open
            && CanOpenPause?.Invoke() == true
            && FindPlayer() is { ModalOpen: false })
        {
            Open();
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        var scale = Mathf.Clamp((float)settings.TextScale, 0.8f, 1.6f);
        var textColor = settings.HighContrast ? Colors.White : new Color(0.89f, 0.84f, 0.73f);

        if (_title is not null)
        {
            _title.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(42 * scale));
            _title.AddThemeColorOverride("font_color", settings.HighContrast
                ? Colors.White
                : new Color(0.88f, 0.78f, 0.59f));
            _title.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _title.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
            _title.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        }

        if (_status is not null)
        {
            _status.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(17 * scale));
            _status.AddThemeColorOverride("font_color", textColor);
            _status.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _status.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
            _status.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        }

        if (_panel is not null)
        {
            _panel.SetMeta("accessibilityTextScale", settings.TextScale);
            _panel.SetMeta("accessibilityHighContrast", settings.HighContrast);
            _panel.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
            _panel.AddThemeStyleboxOverride("panel", PanelStyle(settings.HighContrast));
        }

        foreach (var button in new[]
                 {
                     _resumeButton, _saveButton, _loadButton, _settingsButton,
                     _restartButton, _mainMenuButton, _quitButton
                 })
        {
            if (button is null)
            {
                continue;
            }

            button.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(24 * scale));
            button.AddThemeColorOverride("font_color", textColor);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Colors.White);
            button.AddThemeColorOverride("font_focus_color", Colors.White);
            button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.62f));
            button.AddThemeColorOverride("font_outline_color", Colors.Black);
            button.AddThemeConstantOverride("outline_size", settings.HighContrast ? 3 : 2);
            ApplyButtonStyles(button, settings.HighContrast);
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!inputEvent.IsActionPressed("pause"))
        {
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
            Open();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
    {
        _open = true;
        Visible = true;
        DisarmAll();
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(true);
        UiFoley.SetWorldPaused(GetTree(), true);
        FindPlayer()?.SetModalOpen(true);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetStatus(string.Empty);
        _resumeButton?.GrabFocus();
    }

    public void Resume()
    {
        _open = false;
        Visible = false;
        DisarmAll();
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(false);
        UiFoley.SetWorldPaused(GetTree(), false);
        FindPlayer()?.SetModalOpen(false);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void BuildLayout()
    {
        var screen = new ColorRect
        {
            Name = "PauseScreen",
            Color = new Color(0.008f, 0.012f, 0.011f, 0.86f),
            AnchorRight = 1f,
            AnchorBottom = 1f
        };
        AddChild(screen);

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
            CustomMinimumSize = new Vector2(560, 0)
        };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle(false));
        center.AddChild(_panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 54);
        margin.AddThemeConstantOverride("margin_top", 38);
        margin.AddThemeConstantOverride("margin_right", 54);
        margin.AddThemeConstantOverride("margin_bottom", 38);
        _panel.AddChild(margin);

        var layout = new VBoxContainer
        {
            Name = "Layout",
            Alignment = BoxContainer.AlignmentMode.Center
        };
        layout.AddThemeConstantOverride("separation", 10);
        margin.AddChild(layout);

        _title = new Label
        {
            Name = "Title",
            Text = "Пауза",
            HorizontalAlignment = HorizontalAlignment.Center
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
        _quitButton.Pressed += () => RunConfirmed(_quitButton, "Выход", () => GetTree().Quit());
        layout.AddChild(_quitButton);

        _status = new Label
        {
            Name = "Status",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(0.75f, 0.72f, 0.65f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(360, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layout.AddChild(_status);

        _resumeButton.GrabFocus();
    }

    private Button ShellButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(360, 56),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        ApplyButtonStyles(button, highContrast: false);
        return button;
    }

    private static StyleBoxFlat PanelStyle(bool highContrast) => new()
    {
        BgColor = highContrast
            ? new Color(0.01f, 0.015f, 0.015f, 0.98f)
            : new Color(0.025f, 0.043f, 0.038f, 0.97f),
        BorderColor = highContrast
            ? Colors.White
            : new Color(0.42f, 0.38f, 0.28f, 0.92f),
        BorderWidthLeft = highContrast ? 2 : 1,
        BorderWidthTop = highContrast ? 2 : 1,
        BorderWidthRight = highContrast ? 2 : 1,
        BorderWidthBottom = highContrast ? 2 : 1,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomRight = 6,
        CornerRadiusBottomLeft = 6
    };

    private static void ApplyButtonStyles(Button button, bool highContrast)
    {
        var border = highContrast ? Colors.White : new Color(0.46f, 0.41f, 0.30f, 0.95f);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(
            new Color(0.045f, 0.075f, 0.066f, 0.98f), border, 1));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(
            new Color(0.10f, 0.16f, 0.13f, 1f), highContrast ? Colors.White : new Color(0.88f, 0.78f, 0.59f), 2));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(
            new Color(0.14f, 0.19f, 0.15f, 1f), Colors.White, 2));
        button.AddThemeStyleboxOverride("focus", ButtonStyle(
            new Color(0.10f, 0.16f, 0.13f, 1f), highContrast ? Colors.White : new Color(0.88f, 0.78f, 0.59f), 3));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(
            new Color(0.04f, 0.05f, 0.05f, 0.88f), new Color(0.23f, 0.27f, 0.24f, 0.9f), 1));
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomRight = 4,
        CornerRadiusBottomLeft = 4,
        ContentMarginLeft = 22,
        ContentMarginTop = 10,
        ContentMarginRight = 22,
        ContentMarginBottom = 10
    };

    private async Task SaveAsync()
    {
        var bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            SetStatus("Сохранение недоступно.");
            return;
        }

        SetStatus(await bridge.SaveSlotAsync(ContinueSlot) ? "Сохранено." : "Не удалось сохранить.");
    }

    private async Task LoadAsync()
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

    private void OpenSettings()
    {
        if (GetTree().GetFirstNodeInGroup("settings_ui") as SettingsUi is { } settings
            && FindPlayer() is { } player)
        {
            settings.Open(player);
        }
    }

    private async Task RestartAsync()
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

    private void ShowMainMenu()
    {
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.ResetPresentation();
        UiFoley.StopWorld(GetTree());
        Resume();
        ShowMainMenuRequested?.Invoke();
    }

    private void RunConfirmed(Button button, string baseText, Action action)
    {
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
