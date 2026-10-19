using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class LoadingScreenUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private PanelContainer _panel = null!;
    private Label _title = null!;
    private Label _detail = null!;
    private LoadingSpinner _spinner = null!;
    private Label _resourceProgress = null!;
    private Button? _cancelButton;
    private Action? _cancel;
    private float? _resourceFraction;
    private bool _hidden;
    private bool _cancelPointerPressed;
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public static LoadingScreenUi Show(Node owner, string title, string detail = "", Action? cancel = null)
    {
        var loading = new LoadingScreenUi
        {
            Name = "LoadingScreenUi",
            Layer = 130,
            ProcessMode = ProcessModeEnum.Always
        };
        loading._titleText = title;
        loading._detailText = detail;
        loading._cancel = cancel;
        owner.AddChild(loading);
        return loading;
    }

    private string _titleText = "Подождите";
    private string _detailText = "Идёт загрузка";

    public override void _Ready()
    {
        AddToGroup(AccessibilityPresentation.TargetGroup);
        BuildScreen();
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            ApplyAccessibilitySettings(player.Accessibility);
        if (_cancelButton is not null)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _cancelButton.GrabFocus();
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_hidden || _screen is not { Visible: true }) return;
        if (_cancel is not null && _cancelButton is { Disabled: false } button)
        {
            if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("ui_accept"))
            {
                // Handle these before GUI dispatch so the same key cannot also
                // trigger the journal, pause menu or an underlying game action.
                if (!inputEvent.IsEcho()) CancelOnce();
            }
            else if (inputEvent.IsActionPressed("ui_focus_next") || inputEvent.IsActionPressed("ui_focus_prev")
                || inputEvent.IsActionPressed("ui_up") || inputEvent.IsActionPressed("ui_down")
                || inputEvent.IsActionPressed("ui_left") || inputEvent.IsActionPressed("ui_right"))
            {
                button.GrabFocus();
            }
            else if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            {
                var onButton = button.GetGlobalRect().HasPoint(mouse.Position);
                var ownsRelease = !mouse.Pressed && _cancelPointerPressed;
                _cancelPointerPressed = mouse.Pressed && onButton;
                if (onButton || ownsRelease) return; // Only this control may receive a GUI click.
            }
            else if (inputEvent is InputEventMouseMotion motion && button.GetGlobalRect().HasPoint(motion.Position))
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!_hidden && _screen is { Visible: true }) GetViewport().SetInputAsHandled();
    }

    public void SetResourceProgress(float fraction)
    {
        if (_hidden || IsQueuedForDeletion() || !float.IsFinite(fraction)) return;
        _resourceFraction = Mathf.Clamp(fraction, 0f, 1f);
        RefreshResourceProgress();
    }

    private void RefreshResourceProgress()
    {
        if (!GodotObject.IsInstanceValid(_resourceProgress)) return;
        _resourceProgress.Visible = _resourceFraction.HasValue;
        if (_resourceFraction is { } fraction)
            _resourceProgress.Text = $"Загрузка ресурсов: {Mathf.FloorToInt(fraction * 100f)}%";
    }

    private void CancelOnce()
    {
        if (_hidden || IsQueuedForDeletion() || _cancel is not { } cancel) return;
        _cancel = null;
        _cancelPointerPressed = false;
        if (_cancelButton is { } button) button.Disabled = true;
        _detail.Text = "Отменяем переход…";
        cancel();
    }

    public override void _ExitTree()
    {
        _hidden = true;
        _cancel = null;
        _cancelPointerPressed = false;
    }

    public new void Hide()
    {
        if (_hidden) return;
        _hidden = true;
        _cancel = null;
        _cancelPointerPressed = false;
        SetProcessInput(false);
        SetProcessUnhandledInput(false);
        if (GodotObject.IsInstanceValid(_screen)) _screen.Hide();
        QueueFree();
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        if (_panel is null) return;

        AccessibilityPresentation.ApplyToControl(_panel, settings);
        var palette = UrmanUiTheme.Colours(settings);
        var plate = new StyleBoxFlat
        {
            BgColor = palette.InkRaised,
            BorderColor = palette.Line,
            BorderWidthLeft = settings.HighContrast ? 2 : 1,
            BorderWidthTop = settings.HighContrast ? 2 : 1,
            BorderWidthRight = settings.HighContrast ? 2 : 1,
            BorderWidthBottom = settings.HighContrast ? 2 : 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 32,
            ContentMarginTop = 28,
            ContentMarginRight = 32,
            ContentMarginBottom = 28
        };
        _panel.AddThemeStyleboxOverride("panel", plate);
        _title.ThemeTypeVariation = UrmanUiTheme.Heading;
        _title.AddThemeColorOverride("font_color", palette.Text);
        _detail.AddThemeColorOverride("font_color", palette.TextMuted);
        _resourceProgress.AddThemeColorOverride("font_color", palette.TextMuted);
        _spinner.SetPresentation(palette.Accent, palette.Line, settings.ReducedMotion);
    }

    private void BuildScreen()
    {
        _screen = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_screen);

        var palette = UrmanUiTheme.Colours(_accessibility);
        var shade = new ColorRect { Color = palette.Ink, MouseFilter = Control.MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screen.AddChild(shade);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screen.AddChild(center);

        _panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(420, 246),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        center.AddChild(_panel);

        var margins = new MarginContainer();
        margins.AddThemeConstantOverride("margin_left", 36);
        margins.AddThemeConstantOverride("margin_top", 28);
        margins.AddThemeConstantOverride("margin_right", 36);
        margins.AddThemeConstantOverride("margin_bottom", 28);
        _panel.AddChild(margins);

        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            Theme = UrmanUiTheme.For(_accessibility)
        };
        content.AddThemeConstantOverride("separation", 12);
        margins.AddChild(content);

        var brand = new Label
        {
            Text = "УРМАН · КАРА-УРМАН",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UrmanUiTheme.Tag
        };
        brand.AddThemeColorOverride("font_color", palette.Accent);
        brand.AddThemeFontSizeOverride("font_size", UrmanUiTheme.Size.Small);
        content.AddChild(brand);

        _spinner = new LoadingSpinner
        {
            CustomMinimumSize = new Vector2(64, 64),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        content.AddChild(_spinner);

        _title = new Label
        {
            Text = _titleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UrmanUiTheme.Heading
        };
        content.AddChild(_title);

        _detail = new Label
        {
            Text = _detailText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        content.AddChild(_detail);
        _resourceProgress = new Label
        {
            Name = "ResourceProgress",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        content.AddChild(_resourceProgress);
        RefreshResourceProgress();
        if (_cancel is not null)
        {
            _cancelButton = new Button
            {
                Name = "CancelTransition",
                Text = "Отменить переход",
                FocusMode = Control.FocusModeEnum.All,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _cancelButton.Pressed += CancelOnce;
            content.AddChild(_cancelButton);
        }
        ApplyAccessibilitySettings(_accessibility);
    }

}

internal partial class LoadingSpinner : Control
{
    private float _angle;
    private Color _accent = Colors.White;
    private Color _track = Colors.Gray;
    private bool _reducedMotion;

    public override void _Process(double delta)
    {
        if (_reducedMotion) return;
        _angle = Mathf.Wrap(_angle + (float)delta * 2.2f, 0f, Mathf.Tau);
        QueueRedraw();
    }

    public void SetPresentation(Color accent, Color track, bool reducedMotion)
    {
        _accent = accent;
        _track = track;
        _reducedMotion = reducedMotion;
        SetProcess(!reducedMotion);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawArc(center, 22, 0, Mathf.Tau, 48, _track, 2, true);
        var start = _reducedMotion ? -Mathf.Pi * 0.5f : _angle;
        DrawArc(center, 22, start, start + Mathf.Pi * 1.2f, 40, _accent, 3, true);
        var marker = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * 22;
        DrawCircle(marker, 3, _accent);
    }
}
