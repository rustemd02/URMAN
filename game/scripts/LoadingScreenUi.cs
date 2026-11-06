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
        // «Проявка»: no card. High contrast keeps a solid reading plate.
        _panel.AddThemeStyleboxOverride("panel", settings.HighContrast
            ? UrmanUiTheme.For(true).GetStylebox("panel", "PanelContainer")
            : new StyleBoxEmpty());
        _title.ThemeTypeVariation = UrmanUiTheme.Heading;
        _title.AddThemeColorOverride("font_color", palette.Text);
        _detail.AddThemeColorOverride("font_color", palette.TextMuted);
        _resourceProgress.AddThemeColorOverride("font_color", palette.TextMuted);
        _loadingWord.AddThemeColorOverride("font_color", palette.Text);
        _spinner.SetPresentation(palette.Accent, palette.Line, settings.ReducedMotion);
        if (_polaroids is not null) _polaroids.ReducedMotion = settings.ReducedMotion;
    }

    private Label _loadingWord = null!;
    private PolaroidWall? _polaroids;
    private double _dotsElapsed;
    private int _dots;

    public override void _Process(double delta)
    {
        if (_hidden || _accessibility.ReducedMotion || !GodotObject.IsInstanceValid(_loadingWord)) return;
        _dotsElapsed += delta;
        if (_dotsElapsed < .5) return;
        _dotsElapsed = 0;
        _dots = (_dots + 1) % 4;
        _loadingWord.Text = "Загрузка" + new string('.', _dots);
    }

    private void BuildScreen()
    {
        _screen = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_screen);

        var palette = UrmanUiTheme.Colours(_accessibility);
        var shade = new ColorRect { Color = new Color(.024f, .025f, .03f), MouseFilter = Control.MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screen.AddChild(shade);

        // A print develops in the middle of the dark while the place is prepared.
        // Now and then something stands in it that should not be there.
        _polaroids = new PolaroidWall
        {
            Name = "LoadingPolaroids",
            Slots = new[] { new Vector3(.5f, .42f, -3f), new Vector3(.52f, .43f, 4f) },
            PrintWidth = 330f,
            MaxVisible = 2,
            Interval = 5.2,
            FigureEvery = 2,
            ReducedMotion = _accessibility.ReducedMotion,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _polaroids.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screen.AddChild(_polaroids);

        var bottom = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bottom.AddThemeConstantOverride("margin_left", 80);
        bottom.AddThemeConstantOverride("margin_right", 80);
        bottom.AddThemeConstantOverride("margin_bottom", 56);
        _screen.AddChild(bottom);

        _panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        bottom.AddChild(_panel);

        var row = new HBoxContainer { Theme = UrmanUiTheme.For(_accessibility), MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 32);
        _panel.AddChild(row);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd
        };
        content.AddThemeConstantOverride("separation", 4);
        row.AddChild(content);

        _spinner = new LoadingSpinner { Visible = false };

        _title = new Label
        {
            Text = _titleText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UrmanUiTheme.Heading
        };
        content.AddChild(_title);

        _detail = new Label
        {
            Text = _detailText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        content.AddChild(_detail);
        _resourceProgress = new Label
        {
            Name = "ResourceProgress",
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
                MouseFilter = Control.MouseFilterEnum.Stop,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
            };
            _cancelButton.Pressed += CancelOnce;
            content.AddChild(_cancelButton);
        }

        _loadingWord = new Label
        {
            Name = "LoadingWord",
            Text = "Загрузка",
            CustomMinimumSize = new Vector2(190, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            ThemeTypeVariation = UrmanUiTheme.Heading
        };
        row.AddChild(_loadingWord);
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
