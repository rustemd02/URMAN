using System.Text.Json;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>Location typography and disk feedback; runtime remains the state owner.</summary>
public partial class ExplorationFeedbackUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private sealed record Place(string Key, string Title, string Subtitle, string? Zone);
    private readonly List<Place> _places = [];
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);
    private readonly Queue<Place> _pendingTitles = new();
    private RuntimeBridge _bridge = null!;
    private Control _screen = null!;
    private VBoxContainer _title = null!;
    private Label _name = null!, _subtitle = null!, _saveText = null!;
    private SaveGlyph _glyph = null!;
    private HBoxContainer _save = null!;
    private object? _session;
    private Tween? _titleTween;
    private double _scan, _saveRemaining;
    private int _writes;
    private bool _writeFailed, _reducedMotion;

    public override void _Ready()
    {
        Layer = 18;
        _bridge = (RuntimeBridge)GetParent();
        _bridge.SaveFeedback += OnSaveFeedback;
        _bridge.PlayTimeBoundary += OnBoundary;
        AddToGroup(AccessibilityPresentation.TargetGroup);
        using var data = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/world/location_titles.v1.json"));
        foreach (var row in data.RootElement.GetProperty("places").EnumerateArray())
            _places.Add(new(row.GetProperty("addressId").GetString()!, row.GetProperty("title").GetString()!, row.GetProperty("subtitle").GetString()!, null));
        foreach (var row in data.RootElement.GetProperty("zones").EnumerateArray())
            _places.Add(new(row.GetProperty("key").GetString()!, row.GetProperty("title").GetString()!, row.GetProperty("subtitle").GetString()!, row.GetProperty("zoneId").GetString()));
        _screen = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_screen);
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _title = new VBoxContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _screen.AddChild(_title);
        _title.AnchorLeft = .08f; _title.AnchorRight = .92f;
        _title.AnchorTop = _title.AnchorBottom = .42f;
        _title.AddThemeConstantOverride("separation", 10);
        _title.AddChild(new HSeparator { Modulate = new Color(1, 1, 1, .45f), MouseFilter = Control.MouseFilterEnum.Ignore });
        _name = MakeLabel("", 50, UrmanUiTheme.DisplayRegularFont);
        _subtitle = MakeLabel("", 19, UrmanUiTheme.ItalicFont);
        _title.AddChild(_name); _title.AddChild(_subtitle);
        _title.AddChild(new HSeparator { Modulate = new Color(1, 1, 1, .45f), MouseFilter = Control.MouseFilterEnum.Ignore });
        _save = new HBoxContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _screen.AddChild(_save);
        _save.AnchorLeft = .5f; _save.AnchorRight = 1;
        _save.AnchorTop = _save.AnchorBottom = 1;
        _save.OffsetLeft = 0; _save.OffsetRight = -32; _save.OffsetTop = -66; _save.OffsetBottom = -24;
        _save.Alignment = BoxContainer.AlignmentMode.End;
        _save.AddThemeConstantOverride("separation", 12);
        _glyph = new SaveGlyph { CustomMinimumSize = new Vector2(32, 32), MouseFilter = Control.MouseFilterEnum.Ignore };
        _saveText = MakeLabel("", 16, UrmanUiTheme.BodyFont);
        _save.AddChild(_saveText); _save.AddChild(_glyph);
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            ApplyAccessibilitySettings(player.Accessibility);
    }

    private static Label MakeLabel(string text, int size, Font font)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", UrmanUiTheme.Normal.Text);
        label.AddThemeColorOverride("font_outline_color", UrmanUiTheme.Normal.Ink);
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    public override void _Process(double delta)
    {
        if (_writes == 0 && _saveRemaining > 0)
        {
            _saveRemaining -= delta;
            if (_saveRemaining <= 0) _save.Visible = false;
        }
        var playable = _bridge.CapturePlayTimeBlocks() == RuntimeBridge.PlayTimeBlock.None
            && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: false };
        if (_titleTween?.IsValid() == true)
        {
            _title.Visible = playable;
            if (playable) _titleTween.Play(); else _titleTween.Pause();
        }
        if (!playable || (_scan -= delta) > 0) return;
        _scan = .35;
        if (!ReferenceEquals(_session, _bridge.SessionIdentity))
        {
            _session = _bridge.SessionIdentity;
            _shown.Clear();
            _pendingTitles.Clear();
            _titleTween?.Kill();
            _titleTween = null;
            _title.Visible = false;
        }
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (player is null) return;
        var registry = _bridge.NotebookSettlement;
        foreach (var place in _places)
        {
            if (_shown.Contains(place.Key)) continue;
            var reached = place.Zone is not null && _bridge.CurrentZoneId == place.Zone;
            if (place.Zone is null && registry is not null && registry.CanonicalAddressId(place.Key) is { } addressId
                && registry.Addresses.TryGetValue(addressId, out var address)
                && registry.AccessPoints.TryGetValue(address.AccessId, out var access))
            {
                var at = new Vector3((float)access.Position.X, (float)access.Position.Y, (float)access.Position.Z);
                reached = new Vector2(player.GlobalPosition.X - at.X, player.GlobalPosition.Z - at.Z).Length() < 9
                    && Mathf.Abs(player.GlobalPosition.Y - at.Y) < 4;
            }
            if (!reached) continue;
            _shown.Add(place.Key);
            _pendingTitles.Enqueue(place);
            break;
        }
        if (_titleTween?.IsRunning() != true && _pendingTitles.TryDequeue(out var next)) Present(next);
    }

    private void OnBoundary(string boundary)
    {
        if (boundary != "load-start") return;
        _titleTween?.Kill();
        _titleTween = null;
        _pendingTitles.Clear();
        _title.Visible = false;
    }

    private void Present(Place place)
    {
        _titleTween?.Kill();
        _name.Text = place.Title; _subtitle.Text = place.Subtitle;
        _title.Visible = true;
        _title.Modulate = new Color(1, 1, 1, _reducedMotion ? 1 : 0);
        _titleTween = CreateTween();
        if (!_reducedMotion) _titleTween.TweenProperty(_title, "modulate:a", 1f, .75).SetTrans(Tween.TransitionType.Sine);
        _titleTween.TweenInterval(3.2);
        if (!_reducedMotion) _titleTween.TweenProperty(_title, "modulate:a", 0f, 1.0);
        _titleTween.TweenCallback(Callable.From(() => { _title.Visible = false; _titleTween = null; }));
    }

    private void OnSaveFeedback(bool? result)
    {
        if (result is null)
        {
            if (_writes == 0) _writeFailed = false;
            _writes++;
        }
        else
        {
            _writes = Math.Max(0, _writes - 1);
            _writeFailed |= result == false;
        }
        _save.Visible = true;
        _glyph.Busy = _writes > 0;
        _glyph.Failed = _writeFailed;
        _glyph.QueueRedraw();
        _saveText.Text = _writes > 0 ? "Сохранение…" : _writeFailed ? "Не удалось сохранить · повторите в меню" : "Сохранено";
        _saveRemaining = _writeFailed ? 8 : 2.5;
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _reducedMotion = settings.ReducedMotion;
        _glyph.ReducedMotion = _reducedMotion;
        var palette = UrmanUiTheme.Colours(settings);
        _glyph.Tint = palette.Text;
        foreach (var (label, size) in new[] { (_name, 50), (_subtitle, 19), (_saveText, 16) })
        {
            label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(size * (float)settings.TextScale));
            label.AddThemeColorOverride("font_color", palette.Text);
        }
    }

    public override void _ExitTree()
    {
        _bridge.SaveFeedback -= OnSaveFeedback;
        _bridge.PlayTimeBoundary -= OnBoundary;
        _titleTween?.Kill();
    }
}

internal partial class SaveGlyph : Control
{
    public bool Busy, Failed, ReducedMotion;
    public Color Tint = UrmanUiTheme.Normal.Text;
    private float _angle;
    public override void _Process(double delta)
    {
        if (!Busy || ReducedMotion || !IsVisibleInTree()) return;
        _angle += (float)delta * 3;
        QueueRedraw();
    }
    public override void _Draw()
    {
        var center = Size / 2;
        if (Busy) DrawArc(center, 12, ReducedMotion ? 0 : _angle, (ReducedMotion ? 0 : _angle) + Mathf.Pi * 1.5f, 24, Tint, 2, true);
        else if (Failed)
        {
            DrawCircle(center, 12, Tint, false, 2, true);
            DrawLine(center + new Vector2(0, -6), center + new Vector2(0, 2), Tint, 2, true);
            DrawCircle(center + new Vector2(0, 6), 1.5f, Tint);
        }
        else
        {
            DrawRect(new Rect2(center - new Vector2(11, 11), new Vector2(22, 22)), Tint, false, 2);
            DrawRect(new Rect2(center - new Vector2(5, 10), new Vector2(10, 7)), Tint, false, 2);
            DrawRect(new Rect2(center + new Vector2(-6, 2), new Vector2(12, 8)), Tint, false, 2);
        }
    }
}
