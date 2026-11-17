using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private PhotoSceneReference? _housePhotoReference;
    private Texture2D? _houseComparisonFront;
    private object? _housePhotoComparisonSession;
    private Label3D? _houseComparisonGuide;
    private bool _houseComparisonNeedsRelease = true;

    private void BuildHousePhotographerComparison()
    {
        _houseComparisonFront = JournalUi.PhotoBookFrontTexture("P-HOUSE");
        _houseComparisonGuide = new Label3D
        {
            Name = "HousePhotoComparisonGuide", Font = ThemeDB.FallbackFont,
            FontSize = 38, PixelSize = .0018f, Width = 760,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
            Modulate = new Color("f5efdd"), OutlineModulate = new Color("354037"),
            OutlineSize = 5, Visible = false
        };
        _world.AddChild(_houseComparisonGuide);
    }

    // The same generous H05 stance used by the original observation. Raising
    // the acquired print adds no foot-level ray target or new persistent flag.
    private bool TryGetHouseComparisonPlayer(out FirstPersonController player)
    {
        player = null!;
        if (_bridge is not { } bridge || !IsValidHouseWorldBridge(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session)
            || !ReferenceEquals(session, bridge.PhotoWorldPresentationIdentity)
            || bridge.IsPhotoWorldPresentationDeferred || GetTree().Paused
            || _world is null || !_world.IsVisibleInTree() || HouseActionModalOpen()
            || _housePhysicalPhase != HousePhysicalPhase.Idle || HouseFrameReaderOpen
            || _cameraWitness is not { } stance || !GodotObject.IsInstanceValid(stance) || !stance.IsVisibleInTree()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController current
            || current.SessionTransitionActive || current.InIntro || current.VehicleControlled
            || current.IsClimbingLadder || !current.IsOnFloor()
            || current.GlobalPosition.DistanceTo(stance.GlobalPosition) > HousePhotographerStanceRadius
            || GetTree().GetFirstNodeInGroup("carry_coordinator") is CarryCoordinator
                { HeldItem: not null } or CarryCoordinator { ActionInProgress: true }
            || current.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true })
            return false;
        player = current;
        return true;
    }

    private bool CanOfferHousePhotographerComparison(out FirstPersonController player)
    {
        if (!TryGetHouseComparisonPlayer(out player) || _houseComparisonNeedsRelease
            || _housePhotoComparisonSession is not null || _houseComparisonFront is null
            || _housePhotographerDispatching || _twoViewsDispatching || _houseViewDispatching
            || _bridge is not { } bridge
            || !bridge.PhotoWorldFactObserved(CameraPositionFactId)
                && (!bridge.PhotoWorldFactObserved(TwoViewsFactId)
                    || !bridge.IsInteractionAvailable(CameraPositionInteractionId))
            || !player.CanShowPhotoSceneReference(bridge, "P-HOUSE", allowJournalModal: false)
            // Ordinary physical interactions always get their own press.
            || player.PeekRayTarget() is not null)
            return false;

        var camera = player.GetNode<Camera3D>("Head/Camera3D");
        var space = GetWorld3D().DirectSpaceState;
        if (!_houseViewRayExclusions.Contains(player.GetRid()))
        {
            _houseViewRayExclusions.Clear();
            _houseViewRayExclusions.Add(player.GetRid());
        }
        return _cameraShadowWitness is { } shadow && GodotObject.IsInstanceValid(shadow)
                && HouseWitnessFramedAndVisible(camera, shadow.GlobalPosition, _cameraShadowCollider, space)
            || _cameraWindowWitness is { } window && GodotObject.IsInstanceValid(window)
                && HouseWitnessFramedAndVisible(camera, window.GlobalPosition, _cameraWindowCollider, space);
    }

    private void ProcessHousePhotographerComparison()
    {
        if (!Input.IsActionPressed("interact") && !Input.IsActionJustPressed("interact"))
            _houseComparisonNeedsRelease = false;
        if (_housePhotoComparisonSession is { } session)
        {
            if (!TryGetHouseComparisonPlayer(out var player) || _bridge is not { } bridge
                || !ReferenceEquals(session, bridge.SessionIdentity)
                || _housePhotoReference is not { } reference || !GodotObject.IsInstanceValid(reference)
                || !reference.IsRaisedFor(player, bridge, session, "P-HOUSE"))
                LowerHousePhotographerPhoto();
            _houseComparisonGuide!.Visible = false;
            return;
        }

        var offer = CanOfferHousePhotographerComparison(out var observer);
        _houseComparisonGuide!.Visible = offer;
        if (!offer)
        {
            _houseComparisonNeedsRelease = true;
            return;
        }
        var key = InputBindingService.ActionHint("interact", observer.CurrentInputDevice == "gamepad");
        _houseComparisonGuide.Text = $"{key} Поднять семейный снимок и сверить двор";
        var toward = -observer.GlobalBasis.Z;
        _houseComparisonGuide.GlobalPosition = observer.GlobalPosition + toward * 1.1f + Vector3.Up * 1.15f;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Echo: true } || !inputEvent.IsActionPressed("interact")
            || !CanOfferHousePhotographerComparison(out var player)
            || _bridge is not { } bridge || bridge.SessionIdentity is not { } session
            || !player.TryShowPhotoSceneReference(bridge, session, "P-HOUSE", _houseComparisonFront!))
            return;
        _housePhotoReference = player.GetChildren().OfType<PhotoSceneReference>()
            .FirstOrDefault(reference => reference.IsRaisedFor(player, bridge, session, "P-HOUSE"));
        if (_housePhotoReference is null) return;
        _housePhotoComparisonSession = session;
        _houseComparisonNeedsRelease = true;
        ClearHousePhotographerSightings();
        _houseComparisonGuide!.Visible = false;
        GetViewport().SetInputAsHandled();
    }

    private void LowerHousePhotographerPhoto()
    {
        if (_housePhotoReference is { } reference && GodotObject.IsInstanceValid(reference))
            reference.Dismiss();
        _housePhotoReference = null;
        _housePhotoComparisonSession = null;
        _houseComparisonNeedsRelease = true;
        ClearHousePhotographerSightings();
        if (_houseComparisonGuide is { } guide && GodotObject.IsInstanceValid(guide)) guide.Visible = false;
    }
}
