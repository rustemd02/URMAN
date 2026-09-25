using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// A spoken remark subtitle above the interaction prompt: a passer-by's
/// greeting in Tatar with its Russian gloss. Presentation only; it never
/// opens a dialogue, changes knowledge or blocks input.
/// </summary>
public partial class FirstPersonController
{
    private const double RemarkSeconds = 5.2;
    private Label? _remarkLabel;
    private Tween? _remarkTween;

    /// <summary>The remark currently on screen, or empty.</summary>
    public string CurrentRemark => _remarkLabel is { Visible: true } label && label.Modulate.A > 0 ? label.Text : string.Empty;

    public void ShowRemark(string speaker, string line)
    {
        if (ModalOpen || string.IsNullOrWhiteSpace(line)) return;
        var label = EnsureRemarkLabel();
        label.Text = $"{speaker}: {line}";
        label.Visible = true;
        _remarkTween?.Kill();
        if (_accessibility.ReducedMotion)
        {
            label.Modulate = Colors.White;
            _remarkTween = CreateTween();
            _remarkTween.TweenInterval(RemarkSeconds);
            _remarkTween.TweenCallback(Callable.From(() => label.Modulate = new Color(1, 1, 1, 0)));
            return;
        }
        label.Modulate = new Color(1, 1, 1, 0);
        _remarkTween = CreateTween();
        _remarkTween.TweenProperty(label, "modulate:a", 1f, .25f);
        _remarkTween.TweenInterval(RemarkSeconds);
        _remarkTween.TweenProperty(label, "modulate:a", 0f, .6f);
    }

    private Label EnsureRemarkLabel()
    {
        if (_remarkLabel is not null && IsInstanceValid(_remarkLabel)) return _remarkLabel;
        _remarkLabel = new Label
        {
            Name = "Remark",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AnchorLeft = .5f, AnchorRight = .5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -440, OffsetRight = 440, OffsetTop = -250, OffsetBottom = -140,
            GrowVertical = Control.GrowDirection.Begin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        GetNode<CanvasLayer>("Hud").AddChild(_remarkLabel);
        ApplyRemarkAccessibility(_accessibility);
        return _remarkLabel;
    }

    private void ApplyRemarkAccessibility(AccessibilitySettingsSnapshot settings)
    {
        if (_remarkLabel is null || !IsInstanceValid(_remarkLabel)) return;
        UrmanUiTheme.ApplyWorldText(_remarkLabel, settings, UrmanUiTheme.Size.Subtitle);
        _remarkLabel.SetMeta("accessibilityTextScale", settings.TextScale);
    }
}
