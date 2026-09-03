using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// UIUX-005 pause shell. Owns the pause action routing (open/resume, settings
/// stacking, safe resume) and the in-pause save/load/restart/menu/quit
/// actions. Restart and quit require a two-step confirmation; the shell holds
/// no narrative state — everything runs through the RuntimeBridge lifecycle.
/// </summary>
public partial class PauseMenuUi : CanvasLayer
{
    public const string ContinueSlot = MainMenuUi.ContinueSlot;

    private Label? _status;
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
        Layer = 90;
        Name = "Act1PauseMenu";
        Visible = false;
        BuildLayout();
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

        if (CanOpenPause?.Invoke() == true)
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

        var layout = new VBoxContainer
        {
            Name = "Layout",
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        layout.AddThemeConstantOverride("separation", 10);
        screen.AddChild(layout);

        var title = new Label
        {
            Name = "Title",
            Text = "Пауза",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 42);
        layout.AddChild(title);

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
            Modulate = new Color(0.75f, 0.72f, 0.65f)
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
            CustomMinimumSize = new Vector2(280, 0)
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        return button;
    }

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
