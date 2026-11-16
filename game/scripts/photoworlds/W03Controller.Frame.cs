using System;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class W03Controller : IAccessibilitySettingsTarget
{
    // Author-approved household note, adapted to the actual cloth and forged hook.
    private const string HouseFrameInscription = "Ткань приподними, а то крючок вырвешь.";
    private Label3D? _houseFrameInk;
    private CanvasLayer? _houseFrameReader;
    private ColorRect _houseFrameShade = null!;
    private PanelContainer _houseFramePanel = null!;
    private Label _houseFrameText = null!, _houseFrameStatus = null!;
    private Button _houseFrameRead = null!, _houseFrameClose = null!;
    private RuntimeBridge? _houseFrameBridge;
    private object? _houseFrameSession, _houseFrameRequest;
    private FirstPersonController? _houseFramePlayer;
    private Viewport? _houseFrameViewport;
    private bool _houseFrameSaving;
    private AccessibilitySettingsSnapshot _houseFrameAccessibility = AccessibilitySettingsSnapshot.Default;
    private bool HouseFrameReaderOpen => _houseFrameReader is { Visible: true };

    private void BuildHouseFrameInscription(Node3D window)
    {
        _houseFrameInk = new Label3D
        {
            Name = "InnerFramePencilNote", Text = HouseFrameInscription,
            Position = new Vector3(0f, 1.70f, -.181f), RotationDegrees = new(0f, 180f, 0f),
            Font = UrmanUiTheme.HandFont, FontSize = 64, PixelSize = .00065f,
            OutlineSize = 0, Shaded = true, DoubleSided = false,
            Modulate = Color.FromHtml("292721"), Visible = false
        };
        window.AddChild(_houseFrameInk);
    }

    private void ProjectHouseFrameInscription(bool visible)
    {
        if (_houseFrameInk is { } ink && GodotObject.IsInstanceValid(ink))
            ink.Visible = visible && _bridge?.PhotoWorldFactObserved(ClothFactId) == true;
        if (HouseFrameReaderOpen && (!visible || !HouseFrameReaderCurrent())) CloseHouseFrameReader();
    }

    private bool BeginHouseFrameReading()
    {
        if (HouseFrameReaderOpen || !CanBeginHouseAction(FrameInteractionId, out var bridge, out var session)
            || !bridge.PhotoWorldFactObserved(ClothFactId)
            || _houseFrameInk is not { } ink || !ink.IsVisibleInTree()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !player.IsOnFloor() || player.VehicleControlled || player.IsClimbingLadder) return false;
        BuildHouseFrameReader();
        _houseFrameBridge = bridge;
        _houseFrameSession = session;
        _houseFramePlayer = player;
        _houseFrameRequest = new object();
        _houseFrameSaving = false;
        var recorded = bridge.PhotoWorldFactObserved(FrameFactId);
        _houseFrameRead.Visible = !recorded;
        _houseFrameRead.Disabled = false;
        _houseFrameClose.Disabled = false;
        _houseFrameStatus.Text = recorded ? "Помета уже прочитана." : "Карандашом по дереву, на внутренней стороне рамки.";
        ApplyAccessibilitySettings(player.Accessibility);
        bridge.PlayTimeBoundary += OnHouseFrameBoundary;
        _houseFrameReader!.Visible = true;
        player.SetModalOpen(true);
        if (recorded) _houseFrameClose.GrabFocus(); else _houseFrameRead.GrabFocus();
        // Reading, rather than merely uncovering the note, owns the existing fact.
        return false;
    }

    private void BuildHouseFrameReader()
    {
        if (_houseFrameReader is not null) return;
        AddToGroup(AccessibilityPresentation.TargetGroup);
        _houseFrameReader = new CanvasLayer { Name = "HouseFrameReading", Layer = 80, Visible = false };
        AddChild(_houseFrameReader);
        _houseFrameShade = new ColorRect { MouseFilter = Control.MouseFilterEnum.Stop };
        _houseFrameReader.AddChild(_houseFrameShade);
        _houseFrameShade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _houseFramePanel = new PanelContainer { ThemeTypeVariation = UrmanUiTheme.Plate };
        _houseFrameShade.AddChild(_houseFramePanel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        _houseFramePanel.AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 18);
        margin.AddChild(layout);
        layout.AddChild(new Label { Text = "Внутри оконной рамки", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _houseFrameText = new Label
        {
            Text = HouseFrameInscription, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _houseFrameText.AddThemeFontOverride("font", UrmanUiTheme.HandFont);
        layout.AddChild(_houseFrameText);
        _houseFrameStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_houseFrameStatus);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 14);
        layout.AddChild(buttons);
        _houseFrameRead = new Button { Text = "Запомнить помету" };
        _houseFrameClose = new Button { Text = "Закрыть" };
        buttons.AddChild(_houseFrameRead);
        buttons.AddChild(_houseFrameClose);
        _houseFrameRead.Pressed += ConfirmHouseFrameReading;
        _houseFrameClose.Pressed += CloseHouseFrameReader;
        _houseFrameViewport = GetViewport();
        _houseFrameViewport.SizeChanged += RefitHouseFrameReader;
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _houseFrameAccessibility = settings;
        if (_houseFrameReader is null) return;
        _houseFrameShade.Color = (settings.HighContrast ? UrmanUiTheme.HighContrast : UrmanUiTheme.Normal).Shade;
        _houseFramePanel.Theme = UrmanUiTheme.For(settings);
        AccessibilityPresentation.ApplyToControl(_houseFramePanel, settings);
        _houseFrameText.AddThemeFontSizeOverride("font_size", (int)(36 * settings.TextScale));
        RefitHouseFrameReader();
    }

    private void RefitHouseFrameReader()
    {
        if (_houseFrameReader is null || !IsInsideTree()) return;
        var viewport = GetViewport().GetVisibleRect().Size;
        var scale = (float)_houseFrameAccessibility.TextScale;
        var size = new Vector2(Math.Max(1, Math.Min(viewport.X - 32, 720 * scale)),
            Math.Max(1, Math.Min(viewport.Y - 32, 350 * scale)));
        _houseFramePanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _houseFramePanel.OffsetLeft = -size.X / 2; _houseFramePanel.OffsetRight = size.X / 2;
        _houseFramePanel.OffsetTop = -size.Y / 2; _houseFramePanel.OffsetBottom = size.Y / 2;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!HouseFrameReaderOpen || !inputEvent.IsActionPressed("ui_cancel")) return;
        if (!_houseFrameSaving) CloseHouseFrameReader();
        GetViewport().SetInputAsHandled();
    }

    private bool OtherHouseFrameModalOpen() => MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("document_ui") is DocumentUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("journal_ui")?.GetNodeOrNull<Control>("Screen") is { Visible: true };

    private bool HouseFrameReaderCurrent() => IsInsideTree() && !IsQueuedForDeletion()
        && _houseFrameBridge is { } bridge && GodotObject.IsInstanceValid(bridge)
        && ReferenceEquals(_bridge, bridge) && !bridge.IsPhotoWorldPresentationDeferred
        && IsHousePhysicalActionCurrent(bridge, _houseFrameSession, allowModal: true)
        && bridge.PhotoWorldFactObserved(ClothFactId)
        && _houseFramePlayer is { } player && GodotObject.IsInstanceValid(player)
        && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
        && player.ModalOpen && !player.VehicleControlled && !player.IsClimbingLadder
        && !OtherHouseFrameModalOpen();

    private void ProcessHouseFrameReader()
    {
        if (HouseFrameReaderOpen && !HouseFrameReaderCurrent()) CloseHouseFrameReader();
    }

    private void OnHouseFrameBoundary(string boundary)
    {
        if (boundary == "load-start") CloseHouseFrameReader();
    }

    private async void ConfirmHouseFrameReading()
    {
        if (_houseFrameSaving || !HouseFrameReaderCurrent() || _houseFrameBridge is not { } bridge
            || bridge.PhotoWorldFactObserved(FrameFactId)) return;
        var request = _houseFrameRequest;
        var session = _houseFrameSession;
        _houseFrameSaving = true;
        _houseFrameRead.Disabled = _houseFrameClose.Disabled = true;
        var committed = false;
        try { committed = await bridge.DispatchInteractionAsync(FrameInteractionId); }
        catch (Exception error) { GD.PushWarning($"W03 frame reading failed: {error.Message}"); }
        if (!GodotObject.IsInstanceValid(this) || !ReferenceEquals(request, _houseFrameRequest)) return;
        _houseFrameSaving = false;
        if (!HouseFrameReaderCurrent()) { CloseHouseFrameReader(); return; }
        if (!committed || !bridge.PhotoWorldFactObserved(FrameFactId))
        {
            _houseFrameStatus.Text = "Не удалось отметить помету. Можно попробовать ещё раз.";
            _houseFrameRead.Disabled = _houseFrameClose.Disabled = false;
            return;
        }
        var player = _houseFramePlayer;
        CloseHouseFrameReader();
        if (ReferenceEquals(session, bridge.SessionIdentity) && player is not null && GodotObject.IsInstanceValid(player))
            player.ShowRemark("Айдар", "Почерк знакомый. Дома сравню с подписью на снимке.");
    }

    private void CloseHouseFrameReader()
    {
        if (!HouseFrameReaderOpen) return;
        var bridge = _houseFrameBridge;
        var player = _houseFramePlayer;
        var releaseModal = IsInsideTree() && bridge is not null && GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(_houseFrameSession, bridge.ProjectionSessionIdentity)
            && player is not null && GodotObject.IsInstanceValid(player)
            && ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
            && !OtherHouseFrameModalOpen();
        if (bridge is not null && GodotObject.IsInstanceValid(bridge)) bridge.PlayTimeBoundary -= OnHouseFrameBoundary;
        _houseFrameReader!.Visible = false;
        _houseFrameBridge = null; _houseFrameSession = null; _houseFramePlayer = null;
        _houseFrameRequest = null; _houseFrameSaving = false;
        if (releaseModal) player!.SetModalOpen(false);
    }

    private void ExitHouseFrameReader()
    {
        CloseHouseFrameReader();
        if (_houseFrameViewport is { } viewport && GodotObject.IsInstanceValid(viewport))
            viewport.SizeChanged -= RefitHouseFrameReader;
        _houseFrameViewport = null;
    }
}
