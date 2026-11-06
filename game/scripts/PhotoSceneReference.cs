using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

// A temporary front-only reference beside the live view; observation and the
// physical book retain their own owners. It never records a match or a finding.
public partial class PhotoSceneReference : CanvasLayer
{
    private const string ForestSceneId = "urman.fullgame:scene/pw-forest";
    private FirstPersonController _player = null!;
    private RuntimeBridge _bridge = null!;
    private object _session = null!;
    private string _photoId = string.Empty;
    private Texture2D _front = null!;
    private PanelContainer _panel = null!;
    private Label _hint = null!;
    private AccessibilitySettingsSnapshot? _presentation;
    private bool _dismissed;

    internal static bool CanShow(FirstPersonController player, RuntimeBridge bridge, string photoId, bool allowJournalModal) =>
        (photoId is "P-FOREST" or "S-SCHOOL")
        && GodotObject.IsInstanceValid(player) && player.IsInsideTree() && !player.IsQueuedForDeletion()
        && !player.SessionTransitionActive && !player.InIntro
        && (allowJournalModal || !player.ModalOpen) && !player.VehicleControlled && !player.IsClimbingLadder
        && GodotObject.IsInstanceValid(bridge) && bridge.IsInsideTree() && !bridge.IsQueuedForDeletion()
        && bridge.CanInspectReceivedPhotoBook && bridge.ActiveSceneId == ForestSceneId
        && bridge.SessionIdentity is { } session
        && ReferenceEquals(session, bridge.PhotoWorldPresentationIdentity)
        && ReferenceEquals(player.GetTree().GetFirstNodeInGroup("player_controller"), player)
        && ReferenceEquals(player.GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
        && player.GetTree().GetFirstNodeInGroup("zone_manager") is Main
            { ActiveZoneScenePath: "res://scenes/photoworlds/W01/W01.tscn" }
        && player.GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { HeldItem: not null }
        && player.GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { ActionInProgress: true }
        && bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == photoId);

    internal void Prepare(FirstPersonController player, RuntimeBridge bridge, object session, string photoId, Texture2D front)
    {
        _player = player;
        _bridge = bridge;
        _session = session;
        _photoId = photoId;
        _front = front;
    }

    public override void _Ready()
    {
        Name = "PhotoSceneReference";
        Layer = 5;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel = new PanelContainer
        {
            Name = "AcquiredPhotoFront", MouseFilter = Control.MouseFilterEnum.Ignore,
            ThemeTypeVariation = UrmanUiTheme.Sheet,
            AnchorLeft = .02f, AnchorTop = .46f, AnchorRight = .38f, AnchorBottom = .96f
        };
        root.AddChild(_panel);
        var contents = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddChild(contents);
        contents.AddChild(new Label
        {
            Text = _bridge.PhotoBookPhotos().First(photo => photo.PhotoId == _photoId).Title,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore
        });
        contents.AddChild(new TextureRect
        {
            Texture = _front, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _hint = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        contents.AddChild(_hint);
        _bridge.RuntimeStateChanged += RefreshAcquiredPhoto;
        RefreshPresentation();
    }

    public override void _Process(double delta)
    {
        if (_dismissed) return;
        if (!GodotObject.IsInstanceValid(_bridge) || !GodotObject.IsInstanceValid(_player)
            || _player.SessionTransitionActive || !ReferenceEquals(_session, _bridge.SessionIdentity)
            || !_bridge.CanInspectReceivedPhotoBook || _bridge.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(_session, _bridge.PhotoWorldPresentationIdentity)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), _player)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), _bridge)
            || GetTree().GetFirstNodeInGroup("zone_manager") is not Main
                { ActiveZoneScenePath: "res://scenes/photoworlds/W01/W01.tscn" })
        {
            Dismiss();
            return;
        }
        var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        Visible = !_player.ModalOpen && !_player.VehicleControlled && !_player.IsClimbingLadder
            && carry is not { HeldItem: not null } and not { ActionInProgress: true };
        RefreshPresentation();
    }

    public override void _Input(InputEvent inputEvent)
    {
        // Polled gameplay actions ignore SetInputAsHandled: a remapped B must
        // not dismiss the photo and also interact or jump in the same frame.
        if (_dismissed || !Visible || !_player.IsInsideTree() || _player.ModalOpen
            || !ReferenceEquals(_session, _bridge.SessionIdentity)
            || !(inputEvent.IsActionPressed("pause") || inputEvent.IsActionPressed("ui_cancel"))
            || InputBindingService.RemappableActions.Any(action => action != "pause"
                && inputEvent.IsActionPressed(action))
            || !CanShow(_player, _bridge, _photoId, allowJournalModal: false)) return;
        Dismiss();
        GetViewport().SetInputAsHandled();
    }

    private void RefreshAcquiredPhoto()
    {
        if (!_bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == _photoId)) Dismiss();
    }

    private void RefreshPresentation()
    {
        if (_presentation != _player.Accessibility)
        {
            _presentation = _player.Accessibility;
            AccessibilityPresentation.ApplyToControl(_panel, _presentation);
        }
        var close = InputBindingService.ActionHint("pause", _player.CurrentInputDevice == "gamepad");
        _hint.Text = $"Можно идти и осматриваться. {close} — убрать снимок";
    }

    internal void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        Visible = false;
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_bridge)) _bridge.RuntimeStateChanged -= RefreshAcquiredPhoto;
    }
}
