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
    private PanelContainer? _panel;
    private Button? _newGameButton;
    private Button? _continueButton;
    private Button? _settingsButton;
    private Button? _quitButton;
    private Button? _aboutButton;
    private VBoxContainer? _layout;
    private VBoxContainer? _about;
    private ScrollContainer? _scroll;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private bool _continueAvailable;
    private bool _newGameArmed;
    private string? _continueDescription;

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public Button? NewGameButton => _newGameButton;
    public Button? ContinueButton => _continueButton;
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
    }

    public void SetContinueAvailable(bool available, string? description = null)
    {
        _continueAvailable = available;
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
            _continueHint.Text = available ? description ?? "Последнее сохранение" : "Продолжить · подходящее сохранение не найдено";
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
        var scale = Mathf.Clamp((float)settings.TextScale, 0.8f, 1.6f);
        var textColor = settings.HighContrast ? Colors.White : new Color(0.89f, 0.84f, 0.73f);
        if (_title is not null)
        {
            _title.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(58 * scale));
            _title.AddThemeColorOverride("font_color", settings.HighContrast
                ? Colors.White
                : new Color(0.88f, 0.78f, 0.59f));
            _title.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _title.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
            _title.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        }

        if (_subtitle is not null)
        {
            _subtitle.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(20 * scale));
            _subtitle.AddThemeColorOverride("font_color", textColor);
        }

        if (_continueHint is not null)
        {
            _continueHint.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(17 * scale));
            _continueHint.AddThemeColorOverride("font_color", settings.HighContrast
                ? Colors.White
                : new Color(0.58f, 0.62f, 0.58f));
            _continueHint.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _continueHint.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
            _continueHint.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        }

        if (_panel is not null)
        {
            _panel.SetMeta("accessibilityTextScale", settings.TextScale);
            _panel.SetMeta("accessibilityHighContrast", settings.HighContrast);
            _panel.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
            _panel.AddThemeStyleboxOverride("panel", PanelStyle(settings.HighContrast));
        }

        if (_about is not null) AccessibilityPresentation.ApplyToControl(_about, settings);
        foreach (var button in new[] { _newGameButton, _continueButton, _settingsButton, _aboutButton, _quitButton })
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

    private void BuildLayout()
    {
        var screen = new ColorRect
        {
            Name = "MenuScreen",
            Color = new Color(0.008f, 0.012f, 0.011f, 0.92f),
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
            Name = "MenuPanel",
            CustomMinimumSize = new Vector2(560, 0)
        };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle(false));
        center.AddChild(_panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 54);
        margin.AddThemeConstantOverride("margin_top", 42);
        margin.AddThemeConstantOverride("margin_right", 54);
        margin.AddThemeConstantOverride("margin_bottom", 42);
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
        layout.AddThemeConstantOverride("separation", 12);
        var contents = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scroll.AddChild(contents);
        contents.AddChild(layout);
        _layout = layout;

        _title = new Label
        {
            Name = "Title",
            Text = "УРМАН",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        layout.AddChild(_title);

        _subtitle = new Label
        {
            Name = "Subtitle",
            Text = "Акт I · Кырлай",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(0.75f, 0.72f, 0.65f)
        };
        layout.AddChild(_subtitle);

        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        _newGameButton = MenuButton("NewGameButton", "Новая игра");
        _newGameButton.Pressed += () =>
        {
            if (_continueAvailable && !_newGameArmed)
            {
                _newGameArmed = true;
                _newGameButton.Text = "Начать новую игру";
                ShowStatus("Автосохранение будет заменяться.\nРучное сохранение останется. Esc — отмена.");
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
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(360, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layout.AddChild(_continueHint);

        _settingsButton = MenuButton("SettingsButton", "Настройки");
        _settingsButton.Pressed += () => { DisarmNewGame(); SettingsRequested?.Invoke(); };
        layout.AddChild(_settingsButton);

        _aboutButton = MenuButton("AboutButton", "Об игре и титры");
        _aboutButton.Pressed += OpenAbout;
        layout.AddChild(_aboutButton);

        _quitButton = MenuButton("QuitButton", "Выход");
        _quitButton.Pressed += () => QuitRequested?.Invoke();
        layout.AddChild(_quitButton);

        _newGameButton.GrabFocus();
        SetContinueAvailable(_continueAvailable);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_about is not null && inputEvent.IsActionPressed("ui_cancel"))
        { CloseAbout(); GetViewport().SetInputAsHandled(); }
        else if (_newGameArmed && inputEvent.IsActionPressed("ui_cancel"))
        { DisarmNewGame(); GetViewport().SetInputAsHandled(); }
    }

    private void DisarmNewGame()
    {
        _newGameArmed = false;
        if (_newGameButton is not null) _newGameButton.Text = "Новая игра";
        SetContinueAvailable(_continueAvailable, _continueDescription);
    }

    private void OpenAbout()
    {
        if (_about is not null || _scroll is null || _layout is null) return;
        DisarmNewGame();
        _layout.Hide();
        _about = new VBoxContainer { Name = "About", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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

    private Button MenuButton(string name, string text)
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
}
