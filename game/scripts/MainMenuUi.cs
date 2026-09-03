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

    private Label? _title;
    private Label? _subtitle;
    private Button? _newGameButton;
    private Button? _continueButton;
    private Button? _settingsButton;
    private Button? _quitButton;

    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    public Button? NewGameButton => _newGameButton;
    public Button? ContinueButton => _continueButton;
    public Button? SettingsButton => _settingsButton;
    public bool IsDismissed { get; private set; }

    public override void _Ready()
    {
        AddToGroup("main_menu");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        Layer = 100;
        Name = "Act1MainMenu";
        BuildLayout();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        ApplyAccessibilitySettings(new AccessibilitySettingsSnapshot());
    }

    public void SetContinueAvailable(bool available)
    {
        if (_continueButton is not null)
        {
            _continueButton.Visible = available;
        }
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
        var scale = (float)settings.TextScale;
        if (_title is not null)
        {
            _title.AddThemeFontSizeOverride("font_size", (int)(58 * scale));
            _title.AddThemeColorOverride(
                "font_color",
                settings.HighContrast ? Colors.White : new Color(0.88f, 0.78f, 0.59f));
        }

        if (_subtitle is not null)
        {
            _subtitle.AddThemeFontSizeOverride("font_size", (int)(20 * scale));
        }

        foreach (var button in new[] { _newGameButton, _continueButton, _settingsButton, _quitButton })
        {
            button?.AddThemeFontSizeOverride("font_size", (int)(24 * scale));
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
        layout.AddThemeConstantOverride("separation", 14);
        screen.AddChild(layout);

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
        _newGameButton.Pressed += () => NewGameRequested?.Invoke();
        layout.AddChild(_newGameButton);

        _continueButton = MenuButton("ContinueButton", "Продолжить");
        _continueButton.Pressed += () => ContinueRequested?.Invoke();
        _continueButton.Visible = false;
        layout.AddChild(_continueButton);

        _settingsButton = MenuButton("SettingsButton", "Настройки");
        _settingsButton.Pressed += () => SettingsRequested?.Invoke();
        layout.AddChild(_settingsButton);

        _quitButton = MenuButton("QuitButton", "Выход");
        _quitButton.Pressed += () => QuitRequested?.Invoke();
        layout.AddChild(_quitButton);

        _newGameButton.GrabFocus();
    }

    private Button MenuButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(280, 0)
        };
        button.AddThemeConstantOverride("focus", 1);
        return button;
    }
}
