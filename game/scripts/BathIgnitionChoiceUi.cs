using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>A local choice of an already available source; it owns no saved state.</summary>
public partial class BathIgnitionChoiceUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private PanelContainer _panel = null!;
    private Button _family = null!;
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private object? _session;
    private TaskCompletionSource<bool?>? _answer;
    private float _textScale = 1;
    internal bool IsOpen => _answer is not null;

    internal static Task<bool?> ChooseFor(Node context, RuntimeBridge bridge)
    {
        if (bridge.SessionIdentity is not { } session
            || context.GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController { ModalOpen: false } player)
            return Task.FromResult<bool?>(null);
        var ui = context.GetTree().GetFirstNodeInGroup("bath_ignition_choice_ui") as BathIgnitionChoiceUi;
        if (ui is null) { ui = new BathIgnitionChoiceUi { Name = "BathIgnitionChoice" }; context.AddChild(ui); }
        if (ui.IsOpen) return Task.FromResult<bool?>(null);
        ui._bridge = bridge;
        ui._session = session;
        ui._player = player;
        ui._answer = new TaskCompletionSource<bool?>();
        bridge.PlayTimeBoundary += ui.OnBoundary;
        ui.ApplyAccessibilitySettings(player.Accessibility);
        ui._screen.Show();
        player.SetModalOpen(true);
        ui._family.GrabFocus();
        return ui._answer.Task;
    }

    public override void _Ready()
    {
        Layer = 22;
        AddToGroup("bath_ignition_choice_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = new Control { Name = "Screen", Visible = false };
        AddChild(_screen);
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(.025f, .025f, .02f, .65f) };
        _screen.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = new PanelContainer { Name = "Ignition" };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color("27251f"), ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 20, ContentMarginBottom = 20 });
        _screen.AddChild(_panel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _panel.AddChild(scroll);
        var layout = new VBoxContainer { Name = "Choices", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(layout);
        layout.AddChild(new Label { Text = "Растопить печь", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        layout.AddChild(new Label { Text = "Сухое полено приготовлено. На полке есть семейный коробок; купленные спички — в кармане.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _family = Choice("FamilyMatches", "Спичками с полки", false);
        layout.AddChild(_family);
        layout.AddChild(Choice("PurchasedMatches", "Купленными спичками — оставить коробок в бане", true));
        var cancel = new Button { Name = "Cancel", Text = "Пока не растапливать", CustomMinimumSize = new(0, 44),
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        cancel.Pressed += () => Finish(null);
        layout.AddChild(cancel);
        GetViewport().SizeChanged += Refit;
        Refit();
    }

    private Button Choice(string name, string text, bool purchased)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new(0, 44),
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        button.Pressed += () => Finish(purchased);
        return button;
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _textScale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        AccessibilityPresentation.ApplyToControl(_panel, settings);
        Refit();
    }

    private void Refit()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var size = new Vector2(Mathf.Max(1, Mathf.Min(650 * _textScale, viewport.X - 32)),
            Mathf.Max(1, Mathf.Min(330 * _textScale, viewport.Y - 32)));
        _panel.Position = (viewport - size) * .5f;
        _panel.Size = size;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (IsOpen && inputEvent.IsActionPressed("ui_cancel"))
        {
            Finish(null);
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBoundary(string boundary)
    {
        if (boundary == "load-start") Finish(null);
    }

    public override void _Process(double delta)
    {
        if (IsOpen && (_bridge is null || !IsInstanceValid(_bridge)
            || !ReferenceEquals(_session, _bridge.SessionIdentity))) Finish(null);
    }

    private bool OtherModalOpen()
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
            || GetTree().GetNodesInGroup("main_menu").OfType<MainMenuUi>().Any(menu => !menu.IsDismissed)) return true;
        foreach (var group in new[] { "dialogue_ui", "journal_ui", "document_ui", "old_pc_ui", "village_shop_ui" })
            if (GetTree().GetFirstNodeInGroup(group)?.GetNodeOrNull<Control>("Screen") is { Visible: true }) return true;
        return _bridge?.GetParent()?.GetParent() is Act1DemoRoot { DemoEnded: true };
    }

    private void Finish(bool? purchased)
    {
        if (_answer is not { } answer) return;
        _answer = null;
        _screen.Hide();
        if (_bridge is not null && IsInstanceValid(_bridge)) _bridge.PlayTimeBoundary -= OnBoundary;
        if (_player is not null && IsInstanceValid(_player) && IsInsideTree() && !OtherModalOpen())
            _player.SetModalOpen(false);
        _bridge = null;
        _player = null;
        _session = null;
        // Close and release this modal before the caller rechecks the real stove.
        answer.TrySetResult(purchased);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Refit;
        Finish(null);
    }
}
