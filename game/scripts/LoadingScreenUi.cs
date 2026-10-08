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
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;

    public static LoadingScreenUi Show(Node owner, string title, string detail = "")
    {
        var loading = new LoadingScreenUi
        {
            Name = "LoadingScreenUi",
            Layer = 130,
            ProcessMode = ProcessModeEnum.Always
        };
        loading._titleText = title;
        loading._detailText = detail;
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
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_screen is { Visible: true }) GetViewport().SetInputAsHandled();
    }

    public new void Hide()
    {
        SetProcessInput(false);
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
