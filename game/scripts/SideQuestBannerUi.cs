using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// Presentation-only toast for optional assignments: a short title, a goal and
/// a live counter. It never owns quest state; owners hand it resolved text.
/// </summary>
public partial class SideQuestBannerUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Label _kicker = null!;
    private Label _title = null!;
    private Label _goal = null!;
    private Label _counter = null!;
    private Tween? _tween;
    private AudioStreamPlayer? _foley;

    public bool IsVisibleNow => _screen is { } screen && screen.Visible;

    public override void _Process(double delta)
    {
        if (_tween is null || !_tween.IsValid()) return;
        // Read the conversation first; retain the whole toast for the return
        // to play instead of obscuring the speaker or expiring behind a modal.
        var modal = GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };
        _screen.Visible = !modal;
        if (modal) _tween.Pause();
        else _tween.Play();
    }

    public override void _Ready()
    {
        AddToGroup("side_quest_banner");
        Layer = 15;
        _foley = UiFoley.Attach(this);
        _screen = new Control
        {
            Name = "Screen",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        AddChild(_screen);
        // The panel below is anchored to the centre of its parent, so the
        // parent has to be the whole viewport — otherwise the toast is placed
        // against a zero-sized rect in the corner and half of it is cut off.
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Name = "Panel" };
        _screen.AddChild(panel);
        // Give wrapped labels a real width before they calculate their minimum height.
        panel.Size = new Vector2(520, 136);
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.12f, .11f, .09f, .92f),
            BorderColor = new Color(.78f, .63f, .38f, .8f),
            BorderWidthBottom = 2, BorderWidthTop = 0, BorderWidthLeft = 0, BorderWidthRight = 0,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 14, ContentMarginBottom = 16,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };
        panel.AddThemeStyleboxOverride("panel", style);
        var layout = new VBoxContainer { Name = "Layout" };
        layout.AddThemeConstantOverride("separation", 4);
        panel.AddChild(layout);
        _kicker = MakeLabel("ДОПОЛНИТЕЛЬНОЕ ЗАДАНИЕ", 15, new Color(.82f, .68f, .42f));
        _title = MakeLabel(string.Empty, 24, new Color(.96f, .92f, .82f));
        _goal = MakeLabel(string.Empty, 17, new Color(.85f, .81f, .72f));
        _counter = MakeLabel(string.Empty, 19, new Color(.9f, .76f, .5f));
        layout.AddChild(_kicker);
        layout.AddChild(_title);
        layout.AddChild(_goal);
        layout.AddChild(_counter);
        panel.AnchorLeft = panel.AnchorRight = .5f;
        panel.AnchorTop = panel.AnchorBottom = 0f;
        panel.OffsetLeft = -260;
        panel.OffsetRight = 260;
        panel.OffsetTop = 54;
        panel.OffsetBottom = 190;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
        AddToGroup(AccessibilityPresentation.TargetGroup);
        GetViewport().SizeChanged += FitPanel;
    }

    private static Label MakeLabel(string text, int fontSize, Color colour)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMaximumSize = new Vector2(468, -1),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", colour);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        return label;
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        var scale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        _kicker.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(15 * scale));
        _title.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(24 * scale));
        _goal.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(17 * scale));
        _counter.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(19 * scale));
        CallDeferred(nameof(FitPanel));
    }

    private void FitPanel()
    {
        var panel = _screen.GetNode<PanelContainer>("Panel");
        var width = Mathf.Min(520, _screen.Size.X - 48);
        var textWidth = Mathf.Max(1, width - 52);
        foreach (var label in new[] { _kicker, _title, _goal, _counter })
            label.CustomMaximumSize = new Vector2(textWidth, -1);
        panel.OffsetLeft = -width / 2;
        panel.OffsetRight = width / 2;
        // Containers grow for wrapped text but do not shrink back by themselves.
        panel.Size = new Vector2(width, 0);
    }

    /// <summary>Shows the toast. Pure presentation; the owner already committed any state.</summary>
    public void Present(string title, string goal, string counter)
    {
        if (!IsInsideTree()) return;
        _title.Text = title;
        _goal.Text = goal;
        _counter.Text = counter;
        _tween?.Kill();
        _screen.Visible = true;
        var panel = _screen.GetNode<PanelContainer>("Panel");
        CallDeferred(nameof(FitPanel));
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.Position = new Vector2(panel.Position.X, 40);
        _tween = CreateTween();
        var currentTween = _tween;
        currentTween.Finished += () =>
        {
            if (_tween == currentTween) _tween = null;
        };
        _tween.SetTrans(Tween.TransitionType.Cubic);
        _tween.SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(panel, "modulate:a", 1f, .35f);
        _tween.Parallel().TweenProperty(panel, "position:y", 54f, .35f);
        _tween.TweenInterval(4.2f);
        _tween.TweenProperty(panel, "modulate:a", 0f, .6f);
        _tween.TweenCallback(Callable.From(() => _screen.Visible = false));
        UiFoley.Play(_foley, "paper_open");
    }

    public override void _ExitTree()
    {
        _tween?.Kill();
        _tween = null;
    }

    public void Dismiss()
    {
        _tween?.Kill();
        _tween = null;
        _screen.Hide();
    }
}
