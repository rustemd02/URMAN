using System;
using Godot;

namespace Urman.Godot;

public partial class W02Controller
{
    private const string WaterEncounterFactId = "water_encounter_resolved";
    private const float EncounterBoardRestY = 1.42f;
    private const float EncounterBoardLiftY = 1.47f;
    private const float EncounterPresenceRestY = -1.35f;
    private const float EncounterPresenceAnswerY = 0.15f;
    private const float EncounterLiftSeconds = 0.7f;
    private const float EncounterReturnSeconds = 0.9f;

    private enum EncounterPresentationPhase
    {
        Idle,
        LiftingBoard,
        ReturningBoard,
        AwaitingObservation,
        Committing
    }

    private EncounterPresentationPhase _encounterPresentationPhase;
    private object? _encounterActionSession;
    private string? _encounterActionSceneId;
    private object? _encounterProjectionSession;
    private int _encounterActionGeneration;
    private float _encounterActionElapsed;
    private float _encounterActionDuration;
    private Vector3 _encounterBoardStartPosition;
    private Vector3 _encounterBoardEndPosition;
    private Vector3 _encounterBoardStartRotation;
    private Vector3 _encounterBoardEndRotation;
    private float _encounterPresenceStartY;
    private float _encounterPresenceEndY;
    private readonly global::Godot.Collections.Array<Rid> _encounterRayExclusions = new();
    private FirstPersonController? _encounterRayExcludedPlayer;
    private bool? _encounterLastModalOpen;

    /// <summary>
    /// Called by the W02 encounter target before its ordinary interaction
    /// dispatch. Both presses are local until the board is back and the answer
    /// has actually entered the player's view.
    /// </summary>
    private bool BeginEncounterAction()
    {
        if (_encounterPresentationPhase != EncounterPresentationPhase.Idle
            || _encounterWasResolved
            || !TryGetCurrentEncounterProjection(allowModal: false, allowResolved: false, out var projection)
            || projection is null
            || _looseBoard is not { } board || !GodotObject.IsInstanceValid(board))
            return false;

        _encounterActionSession = projection.SessionIdentity;
        _encounterActionSceneId = projection.ActiveSceneId;
        _encounterActionGeneration++;
        _encounterActionElapsed = 0f;
        _encounterTarget?.SetPresentationEnabled(false);

        if (!_boardLifted)
        {
            BeginBoardMotion(
                EncounterPresentationPhase.LiftingBoard,
                new Vector3(board.Position.X, EncounterBoardLiftY, board.Position.Z),
                new Vector3(-7f, -128f, 3f),
                EncounterLiftSeconds,
                movePresence: false);
        }
        else
        {
            BeginBoardMotion(
                EncounterPresentationPhase.ReturningBoard,
                new Vector3(board.Position.X, EncounterBoardRestY, board.Position.Z),
                new Vector3(0f, -128f, 0f),
                EncounterReturnSeconds,
                movePresence: true);
        }

        return false;
    }

    /// <summary>Called from W02Controller._Process in place of its old free-running answer rise.</summary>
    private void ProcessEncounterPresentation(double delta)
    {
        if (_encounterPresentationPhase == EncounterPresentationPhase.Idle)
        {
            if (_encounterWasResolved && _suAnasy is { } resolvedPresence
                && GodotObject.IsInstanceValid(resolvedPresence))
            {
                var bob = CurrentPlayerUsesReducedMotion()
                    ? 0f
                    : Mathf.Sin((float)Time.GetTicksMsec() * 0.0004f) * 0.045f;
                resolvedPresence.Position = new Vector3(
                    resolvedPresence.Position.X, _suAnasyY + bob, resolvedPresence.Position.Z);
            }

            var modalOpen = EncounterModalOpen();
            if (_encounterLastModalOpen != modalOpen)
            {
                _encounterLastModalOpen = modalOpen;
                if (modalOpen && _boardLifted)
                    CancelEncounterAction(resetUnresolvedPose: true);
                else
                    RefreshEncounterTargetAvailability();
            }
            return;
        }

        if (_encounterPresentationPhase == EncounterPresentationPhase.Committing)
        {
            if (!TryGetCurrentEncounterProjection(allowModal: true, allowResolved: true, out var committingProjection)
                || committingProjection is null
                || !ReferenceEquals(committingProjection.SessionIdentity, _encounterActionSession)
                || committingProjection.ActiveSceneId != _encounterActionSceneId)
            {
                CancelEncounterAction(resetUnresolvedPose: true);
                return;
            }

            if (committingProjection.EncounterResolved)
                ProjectResolvedEncounterPose();
            return;
        }

        if (!TryGetCurrentEncounterProjection(allowModal: false, allowResolved: false, out var projection)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, _encounterActionSession)
            || projection.ActiveSceneId != _encounterActionSceneId)
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        if (_encounterPresentationPhase is EncounterPresentationPhase.LiftingBoard
            or EncounterPresentationPhase.ReturningBoard)
        {
            AdvanceEncounterBoardMotion(delta);
            return;
        }

        if (_encounterPresentationPhase == EncounterPresentationPhase.AwaitingObservation
            && IsWaterAnswerFramed())
        {
            BeginEncounterCommit(projection.SessionIdentity, projection.ActiveSceneId);
        }
    }

    /// <summary>
    /// Called by ApplyProjection after the scene's world visibility is set. A
    /// save/load/session change never restores an unfinished local movement.
    /// </summary>
    private void ApplyEncounterProjection(RuntimeBridge.W02WaterProjection? projection, bool visible)
    {
        var projectionSession = projection?.SessionIdentity;
        if (!ReferenceEquals(_encounterProjectionSession, projectionSession))
        {
            _encounterProjectionSession = projectionSession;
            CancelEncounterAction(resetUnresolvedPose: true);
        }

        if (!visible || projection is null || projection.ActiveSceneId != RiverSceneId)
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            _encounterTarget?.SetPresentationEnabled(false);
            return;
        }

        if (projection.EncounterResolved)
        {
            ProjectResolvedEncounterPose();
            return;
        }

        _encounterWasResolved = false;
        if (_encounterPresentationPhase != EncounterPresentationPhase.Idle)
        {
            _encounterTarget?.SetPresentationEnabled(false);
            return;
        }

        if (!_boardLifted)
            SetEncounterBoardRestPose();
        SetEncounterPresenceHeight(EncounterPresenceRestY);
        UpdateEncounterPrompt(encounterResolved: false);
        RefreshEncounterTargetAvailability();
    }

    /// <summary>Called from W02Controller._ExitTree before the scene is freed.</summary>
    private void ExitEncounterPresentation()
    {
        CancelEncounterAction(resetUnresolvedPose: false);
        _encounterProjectionSession = null;
        _encounterRayExclusions.Clear();
        _encounterRayExcludedPlayer = null;
        _encounterLastModalOpen = null;
    }

    private void BeginBoardMotion(
        EncounterPresentationPhase phase,
        Vector3 endPosition,
        Vector3 endRotation,
        float duration,
        bool movePresence)
    {
        if (_looseBoard is not { } board || !GodotObject.IsInstanceValid(board))
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        _encounterPresentationPhase = phase;
        _encounterBoardStartPosition = board.Position;
        _encounterBoardEndPosition = endPosition;
        _encounterBoardStartRotation = board.RotationDegrees;
        _encounterBoardEndRotation = endRotation;
        _encounterActionDuration = duration;
        _encounterActionElapsed = 0f;
        _encounterPresenceStartY = _suAnasyY;
        _encounterPresenceEndY = movePresence ? EncounterPresenceAnswerY : _suAnasyY;

        if (phase == EncounterPresentationPhase.LiftingBoard && _boardMark is { } mark)
            mark.Visible = true;

        if (CurrentPlayerUsesReducedMotion())
        {
            ApplyEncounterMotionPose(1f);
            FinishEncounterBoardMotion();
        }
    }

    private void AdvanceEncounterBoardMotion(double delta)
    {
        if (_looseBoard is not { } board || !GodotObject.IsInstanceValid(board))
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        if (CurrentPlayerUsesReducedMotion())
        {
            _encounterActionElapsed = _encounterActionDuration;
            ApplyEncounterMotionPose(1f);
            FinishEncounterBoardMotion();
            return;
        }

        _encounterActionElapsed = Mathf.Min(
            _encounterActionDuration,
            _encounterActionElapsed + (float)Math.Max(0d, delta));
        var progress = _encounterActionDuration <= 0f
            ? 1f
            : Mathf.Clamp(_encounterActionElapsed / _encounterActionDuration, 0f, 1f);
        ApplyEncounterMotionPose(Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f)
            FinishEncounterBoardMotion();
    }

    private void ApplyEncounterMotionPose(float easedProgress)
    {
        if (_looseBoard is { } board && GodotObject.IsInstanceValid(board))
        {
            board.Position = _encounterBoardStartPosition.Lerp(_encounterBoardEndPosition, easedProgress);
            board.RotationDegrees = _encounterBoardStartRotation.Lerp(_encounterBoardEndRotation, easedProgress);
        }

        if (_encounterPresentationPhase == EncounterPresentationPhase.ReturningBoard)
        {
            _suAnasyY = Mathf.Lerp(_encounterPresenceStartY, _encounterPresenceEndY, easedProgress);
            SetEncounterPresenceHeight(_suAnasyY);
            if (easedProgress >= 0.8f && _boardMark is { } mark && GodotObject.IsInstanceValid(mark))
                mark.Visible = false;
        }
    }

    private void FinishEncounterBoardMotion()
    {
        if (_encounterPresentationPhase == EncounterPresentationPhase.LiftingBoard)
        {
            _boardLifted = true;
            _encounterPresentationPhase = EncounterPresentationPhase.Idle;
            _encounterActionSession = null;
            _encounterActionSceneId = null;
            _encounterActionElapsed = 0f;
            UpdateEncounterPrompt(encounterResolved: false);
            RefreshEncounterTargetAvailability();
            return;
        }

        if (_encounterPresentationPhase == EncounterPresentationPhase.ReturningBoard)
        {
            SetEncounterBoardRestPose();
            SetEncounterPresenceHeight(EncounterPresenceAnswerY);
            _boardLifted = false;
            _encounterPresentationPhase = EncounterPresentationPhase.AwaitingObservation;
            _encounterActionElapsed = 0f;
        }
    }

    private void BeginEncounterCommit(object session, string sceneId)
    {
        if (_encounterPresentationPhase != EncounterPresentationPhase.AwaitingObservation
            || !TryGetCurrentEncounterProjection(allowModal: false, allowResolved: false, out var projection)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, session)
            || projection.ActiveSceneId != sceneId
            || !IsWaterAnswerFramed())
            return;

        var bridge = _bridge;
        if (bridge is null || !GodotObject.IsInstanceValid(bridge))
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        _encounterPresentationPhase = EncounterPresentationPhase.Committing;
        _encounterActionSession = session;
        _encounterActionSceneId = sceneId;
        _encounterTarget?.SetPresentationEnabled(false);
        CommitEncounterAsync(bridge, session, sceneId, _encounterActionGeneration);
    }

    private async void CommitEncounterAsync(RuntimeBridge bridge, object session, string sceneId, int generation)
    {
        bool dispatched;
        try
        {
            dispatched = await bridge.DispatchInteractionAsync(EncounterInteractionId);
        }
        catch (System.Exception exception)
        {
            GD.PushError($"W02 encounter interaction failed: {exception.Message}");
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
                && generation == _encounterActionGeneration)
                CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || !GodotObject.IsInstanceValid(bridge))
            return;

        var projection = bridge.CurrentW02WaterProjection();
        if (projection is { EncounterResolved: true }
            && ReferenceEquals(session, bridge.SessionIdentity)
            && ReferenceEquals(projection.SessionIdentity, session))
        {
            ApplyProjection();
            return;
        }

        if (generation != _encounterActionGeneration)
            return;

        if (!dispatched || !ReferenceEquals(session, bridge.SessionIdentity)
            || projection is null || !ReferenceEquals(projection.SessionIdentity, session)
            || projection.ActiveSceneId != sceneId || projection.EncounterResolved)
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            if (projection is not null && ReferenceEquals(projection.SessionIdentity, session)
                && projection.ActiveSceneId == RiverSceneId)
                ApplyProjection();
        }
    }

    private void ProjectResolvedEncounterPose()
    {
        CancelEncounterAction(resetUnresolvedPose: false);
        _encounterWasResolved = true;
        _boardLifted = false;
        SetEncounterBoardRestPose();
        SetEncounterPresenceHeight(EncounterPresenceAnswerY);
        UpdateEncounterPrompt(encounterResolved: true);
        _encounterTarget?.SetPresentationEnabled(false);
    }

    private void CancelEncounterAction(bool resetUnresolvedPose)
    {
        _encounterActionGeneration++;
        _encounterPresentationPhase = EncounterPresentationPhase.Idle;
        _encounterActionSession = null;
        _encounterActionSceneId = null;
        _encounterActionElapsed = 0f;
        _encounterActionDuration = 0f;

        if (!resetUnresolvedPose) return;

        _encounterWasResolved = false;
        _boardLifted = false;
        SetEncounterBoardRestPose();
        SetEncounterPresenceHeight(EncounterPresenceRestY);
        UpdateEncounterPrompt(encounterResolved: false);
        RefreshEncounterTargetAvailability();
    }

    private void SetEncounterBoardRestPose()
    {
        if (_looseBoard is { } board && GodotObject.IsInstanceValid(board))
        {
            board.Position = new Vector3(board.Position.X, EncounterBoardRestY, board.Position.Z);
            board.RotationDegrees = new Vector3(0f, -128f, 0f);
        }

        if (_boardMark is { } mark && GodotObject.IsInstanceValid(mark))
            mark.Visible = false;
    }

    private void SetEncounterPresenceHeight(float y)
    {
        _suAnasyY = y;
        if (_suAnasy is { } presence && GodotObject.IsInstanceValid(presence))
            presence.Position = new Vector3(presence.Position.X, y, presence.Position.Z);
    }

    private void RefreshEncounterTargetAvailability()
    {
        if (_encounterTarget is not { } target || !GodotObject.IsInstanceValid(target)) return;
        var modalOpen = EncounterModalOpen();
        _encounterLastModalOpen = modalOpen;
        if (modalOpen && _boardLifted
            && _encounterPresentationPhase == EncounterPresentationPhase.Idle)
        {
            CancelEncounterAction(resetUnresolvedPose: true);
            return;
        }

        var activeUnresolvedScene = _world is { } world
            && GodotObject.IsInstanceValid(world)
            && world.Visible
            && !_encounterWasResolved;
        var busy = _encounterPresentationPhase != EncounterPresentationPhase.Idle;
        target.SetPresentationEnabled(activeUnresolvedScene && !busy && !modalOpen);
        if (activeUnresolvedScene && !busy && !modalOpen)
            UpdateEncounterPrompt(_encounterWasResolved);
    }

    private bool TryGetCurrentEncounterProjection(
        bool allowModal,
        bool allowResolved,
        out RuntimeBridge.W02WaterProjection? projection)
    {
        projection = null;
        var bridge = _bridge;
        if (bridge is null || !GodotObject.IsInstanceValid(bridge)
            || _world is not { } world || !GodotObject.IsInstanceValid(world) || !world.Visible
            || (!allowModal && EncounterModalOpen()))
            return false;

        projection = bridge.CurrentW02WaterProjection();
        return projection is not null
            && projection.ActiveSceneId == RiverSceneId
            && ReferenceEquals(projection.SessionIdentity, bridge.SessionIdentity)
            && (allowResolved || !projection.EncounterResolved);
    }

    private bool EncounterModalOpen() =>
        MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private bool CurrentPlayerUsesReducedMotion() =>
        GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
        && GodotObject.IsInstanceValid(player)
        && player.Accessibility.ReducedMotion;

    private bool IsWaterAnswerFramed()
    {
        if (_suAnasy is not { } presence || !GodotObject.IsInstanceValid(presence)
            || !presence.IsVisibleInTree()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder
            || !player.IsOnFloor())
            return false;

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is null || !GodotObject.IsInstanceValid(camera) || !camera.Current) return false;

        // The current W02 answer is a head and torso silhouette with no
        // collision body. These two points prove both that it is framed and
        // that no layer-1 pier/terrain collider lies in front of it.
        var torso = presence.GlobalPosition + Vector3.Up * 0.55f;
        var head = presence.GlobalPosition + Vector3.Up * 1.19f;
        if (camera.GlobalPosition.DistanceTo(head) > 8f) return false;
        return IsVisiblePoint(camera, player, torso) && IsVisiblePoint(camera, player, head);
    }

    private bool IsVisiblePoint(Camera3D camera, FirstPersonController player, Vector3 point)
    {
        if (!camera.IsPositionInFrustum(point)) return false;

        if (!ReferenceEquals(_encounterRayExcludedPlayer, player))
        {
            _encounterRayExclusions.Clear();
            _encounterRayExclusions.Add(player.GetRid());
            _encounterRayExcludedPlayer = player;
        }

        using var ray = PhysicsRayQueryParameters3D.Create(
            camera.GlobalPosition, point, 1u, _encounterRayExclusions);
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count == 0;
    }
}
