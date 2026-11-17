using Godot;

namespace Urman.Godot;

public partial class W04Controller
{
    private Label3D? _commonObservationCue;
    private object? _commonObservationInputSession;
    private bool _commonObservationNeedsRelease = true;

    private bool TryGetCommonObservationInput(out string interactionId, out FirstPersonController player)
    {
        interactionId = string.Empty;
        player = null!;
        if (!HasCurrentCommonRoute() || _bridge is not { } bridge || bridge.IsPhotoBookProjectionDeferred
            || GetTree().Paused || CommonActionModalOpen()
            || _clothPhase != ClothPhase.Idle || _relationsDispatching || _horizonDispatching
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController current
            || current.SessionTransitionActive || current.InIntro || current.VehicleControlled
            || current.IsClimbingLadder || !current.IsOnFloor()
            || GetTree().GetFirstNodeInGroup("carry_coordinator") is CarryCoordinator
                { HeldItem: not null } or CarryCoordinator { ActionInProgress: true })
            return false;

        if (!bridge.PhotoWorldFactObserved(RelationsFactId)
            && _relationsAnchor is { } relations && GodotObject.IsInstanceValid(relations)
            && current.GlobalPosition.DistanceSquaredTo(relations.GlobalPosition)
                <= ObservationStanceRadius * ObservationStanceRadius
            && _world.ToLocal(current.GlobalPosition).Z >= EntryFrameZ + .45f
            && bridge.IsInteractionAvailable(RelationsInteractionId))
            interactionId = RelationsInteractionId;
        else if (!bridge.PhotoWorldFactObserved(HorizonFactId)
            && _horizonAnchor is { } horizon && GodotObject.IsInstanceValid(horizon)
            && current.GlobalPosition.DistanceSquaredTo(horizon.GlobalPosition)
                <= ObservationStanceRadius * ObservationStanceRadius
            && bridge.IsInteractionAvailable(HorizonInteractionId))
            interactionId = HorizonInteractionId;
        else return false;
        player = current;
        return true;
    }

    private void ProcessCommonObservationInput()
    {
        var session = _bridge?.SessionIdentity;
        if (!ReferenceEquals(_commonObservationInputSession, session))
        {
            _commonObservationInputSession = session;
            _commonObservationNeedsRelease = true;
        }
        if (!Input.IsActionPressed("interact") && !Input.IsActionJustPressed("interact"))
            _commonObservationNeedsRelease = false;
        if (!TryGetCommonObservationInput(out var interactionId, out var player))
        {
            _commonObservationNeedsRelease = true;
            if (!_relationsDispatching && _relationsArmed)
            {
                _relationsArmed = false;
                _relationsSeenMask = 0;
                UpdateCommonObservationTarget(_relationsTarget, RelationsInteractionId, RelationsFactId, false);
            }
            if (!_horizonDispatching && _horizonArmed)
            {
                _horizonArmed = false;
                _horizonViewElapsed = 0;
                UpdateCommonObservationTarget(_horizonTarget, HorizonInteractionId, HorizonFactId, false);
            }
            if (_commonObservationCue is { } hidden) hidden.Visible = false;
            return;
        }

        if (_commonObservationCue is null)
        {
            _commonObservationCue = new Label3D
            {
                Name = "CommonObservationCue", Font = ThemeDB.FallbackFont, FontSize = 38,
                PixelSize = .0018f, Width = 760, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
                Modulate = new Color("f5efdd"), OutlineModulate = new Color("354037"), OutlineSize = 5
            };
            _world.AddChild(_commonObservationCue);
        }
        var relationsActive = interactionId == RelationsInteractionId;
        var armed = relationsActive ? _relationsArmed : _horizonArmed;
        var hint = InputBindingService.ActionHint("interact", player.CurrentInputDevice == "gamepad");
        var cancel = InputBindingService.ActionHint("ui_cancel", player.CurrentInputDevice == "gamepad");
        var seen = ((_relationsSeenMask & 1) != 0 ? 1 : 0)
            + ((_relationsSeenMask & 2) != 0 ? 1 : 0) + ((_relationsSeenMask & 4) != 0 ? 1 : 0);
        var text = !armed
            ? relationsActive ? $"{hint} Сопоставить лес, воду и дом" : $"{hint} Рассмотреть общий вид"
            : relationsActive ? $"Сопоставлено мест: {seen}/3. Посмотрите на лес, воду и дом.\n{cancel} — закончить осмотр"
                : $"Посмотрите на общий горизонт и удержите ракурс.\n{cancel} — закончить осмотр";
        if (_commonObservationCue.Text != text) _commonObservationCue.Text = text;
        if (player.GetNodeOrNull<Camera3D>("Head/Camera3D") is { Current: true } camera)
        {
            // A local cue in the player's current view; no invisible target at
            // the stance has to be found before they can turn to the landscape.
            _commonObservationCue.GlobalPosition = camera.GlobalPosition - camera.GlobalBasis.Z * 1.8f
                - camera.GlobalBasis.Y * .55f;
            _commonObservationCue.Visible = true;
        }
        else _commonObservationCue.Visible = false;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Echo: true } || CommonNoteOpen) return;
        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent.IsActionPressed("pause"))
        {
            if (!_relationsArmed && !_horizonArmed) return;
            _commonObservationNeedsRelease = true;
            _relationsArmed = _horizonArmed = false;
            _relationsSeenMask = 0;
            _horizonViewElapsed = 0;
            UpdateCommonObservationTarget(_relationsTarget, RelationsInteractionId, RelationsFactId, false);
            UpdateCommonObservationTarget(_horizonTarget, HorizonInteractionId, HorizonFactId, false);
            return;
        }
        if (_commonObservationNeedsRelease || !inputEvent.IsActionPressed("interact")
            || !TryGetCommonObservationInput(out var interactionId, out _)) return;
        if (interactionId == RelationsInteractionId) BeginCommonRelationsObservation();
        else BeginCommonHorizonObservation();
        if (!_relationsArmed && !_horizonArmed) return;
        _commonObservationNeedsRelease = true;
        GetViewport().SetInputAsHandled();
    }
}
