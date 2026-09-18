using Godot;

namespace Urman.Godot;

public partial class ShopUi : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private PanelContainer _panel = null!;
    private VBoxContainer _stock = null!;
    private Label _ledger = null!;
    private Label _feedback = null!;
    private Button _close = null!;
    private RuntimeBridge? _bridge;
    private object? _session;
    private bool _busy;

    public static void OpenFor(Node context, RuntimeBridge bridge)
    {
        var ui = context.GetTree().GetFirstNodeInGroup("village_shop_ui") as ShopUi;
        if (ui is null) { ui = new ShopUi(); context.AddChild(ui); }
        ui._bridge = bridge;
        ui._session = bridge.SessionIdentity;
        ui._feedback.Text = string.Empty;
        ui.Refresh();
        ui._screen.Show();
        ui.Player()?.SetModalOpen(true);
        ui._close.GrabFocus();
    }

    public override void _Ready()
    {
        Layer = 22;
        AddToGroup("village_shop_ui");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _screen = new Control { Name = "Screen", Visible = false };
        AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0.03f, .03f, .025f, .85f) };
        _screen.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = new PanelContainer { Name = "ShopCounter" };
        _screen.AddChild(_panel);
        var style = new StyleBoxFlat { BgColor = new Color("27251f"), ContentMarginLeft = 24,
            ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20 };
        _panel.AddThemeStyleboxOverride("panel", style);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 14); _panel.AddChild(layout);
        var header = new HBoxContainer(); layout.AddChild(header);
        header.AddChild(new Label { Text = "У Разили", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _close = new Button { Text = "Закрыть" }; header.AddChild(_close); _close.Pressed += Close;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        layout.AddChild(scroll);
        var contents = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddThemeConstantOverride("separation", 14); scroll.AddChild(contents);
        contents.AddChild(new Label { Text = "Что нужно домой? Запишу на бабая.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _stock = new VBoxContainer { Name = "Stock" }; _stock.AddThemeConstantOverride("separation", 10); contents.AddChild(_stock);
        _ledger = new Label { Name = "Ledger", AutowrapMode = TextServer.AutowrapMode.WordSmart }; contents.AddChild(_ledger);
        _feedback = new Label { Name = "Feedback", AutowrapMode = TextServer.AutowrapMode.WordSmart }; contents.AddChild(_feedback);
        GetViewport().SizeChanged += Refit;
        ApplyAccessibilitySettings(Player()?.Accessibility ?? Urman.Core.Persistence.AccessibilitySettingsSnapshot.Default);
    }

    public void ApplyAccessibilitySettings(Urman.Core.Persistence.AccessibilitySettingsSnapshot settings)
    {
        if (_panel is null) return;
        AccessibilityPresentation.ApplyToControl(_panel, settings);
        Refit();
    }

    private void Refit()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var size = new Vector2(Mathf.Min(760, viewport.X - 32), Mathf.Min(650, viewport.Y - 32));
        _panel.Position = (viewport - size) * .5f; _panel.Size = size;
    }

    private void Refresh()
    {
        foreach (var child in _stock.GetChildren()) { _stock.RemoveChild(child); child.QueueFree(); }
        var bought = _bridge?.ShopLedger().Select(entry => entry.Sku).ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var product in ShopCatalog.All)
        {
            var button = new Button { Text = product.Title + " — " + product.Unit + (bought.Contains(product.Sku) ? " · получено" : ""),
                Disabled = _busy || bought.Contains(product.Sku), AutowrapMode = TextServer.AutowrapMode.WordSmart,
                TooltipText = product.Description };
            _stock.AddChild(button);
            button.Pressed += async () =>
            {
                if (_busy || _bridge is not { } bridge) return;
                var session = bridge.SessionIdentity;
                _busy = true; _close.Disabled = true;
                foreach (var item in _stock.GetChildren().OfType<Button>()) item.Disabled = true;
                try
                {
                    var saved = await bridge.PurchaseShopItemAsync(product.Sku);
                    if (!IsInsideTree() || !ReferenceEquals(session, bridge.SessionIdentity)
                        || !ReferenceEquals(_bridge, bridge)) return;
                    _feedback.Text = saved ? product.Title + " — получено и записано на бабая." : "Покупку не удалось завершить.";
                }
                catch (Exception error)
                {
                    GD.PushError("Shop purchase failed: " + error.Message);
                    if (IsInsideTree() && ReferenceEquals(session, bridge.SessionIdentity))
                        _feedback.Text = "Не удалось завершить покупку. Можно попробовать ещё раз.";
                }
                finally
                {
                    _busy = false;
                    if (IsInsideTree())
                    {
                        _close.Disabled = false;
                        Refresh();
                        if (_screen.Visible) _close.GrabFocus();
                    }
                }
            };
        }
        _ledger.Text = _bridge?.ShopLedgerText() ?? string.Empty;
        ApplyAccessibilitySettings(Player()?.Accessibility ?? Urman.Core.Persistence.AccessibilitySettingsSnapshot.Default);
    }

    public override void _Process(double delta)
    {
        if (_screen.Visible && (_bridge is null || !ReferenceEquals(_session, _bridge.SessionIdentity)))
        {
            _screen.Hide(); _bridge = null; _session = null;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_screen.Visible && inputEvent.IsActionPressed("ui_cancel"))
        { Close(); GetViewport().SetInputAsHandled(); }
    }
    private FirstPersonController? Player() => GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
    private void Close()
    {
        if (_busy) return;
        _screen.Hide(); _bridge = null; _session = null; Player()?.SetModalOpen(false);
    }
}
