using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W02Controller
{
    private const double ReflectionObservationPollSeconds = 0.2;
    private const float ReflectionObservationRetryDistance = 0.2f;
    private const float ReflectionObservationRetryForwardDot = 0.99f;
    private const string ReflectionFactId = "water_reflection_relation_seen";
    private const string ReflectionSketchAssetId = "urman.fullgame:asset/w02-water-reflection-relation-sketch";

    // Geometry is authored by W02Controller after the R03 outcrop is built.
    // Points are world-space samples on three visible supports and the deck edge;
    // the stand transform and planar half extents come from its safe footprint.
    private Vector3[] _reflectionObservationPoints = [];
    private Node3D? _reflectionObservationStand;
    private Vector2 _reflectionObservationStandHalfExtents;
    private readonly global::Godot.Collections.Array<Rid> _reflectionObservationRayExclusions = new();
    private double _reflectionObservationPollElapsed;
    private object? _reflectionObservationSessionIdentity;
    private (Vector3 Position, Vector3 Forward)? _reflectionObservationInitialPose;
    private bool _reflectionObservationArmed;
    private object? _reflectionObservationFailedSession;
    private Vector3 _reflectionObservationFailedPosition;
    private Vector3 _reflectionObservationFailedForward;
    private object? _reflectionObservationDispatchSession;
    private bool _reflectionObservationDispatching;
    private int _reflectionObservationGeneration;
    private object? _reflectionRemarkShownSession;
    private object? _reflectionRemarkPendingSession;

    private void ResetReflectionObservation(object? sessionIdentity)
    {
        if (sessionIdentity is null)
        {
            _reflectionObservationGeneration++;
            _reflectionObservationSessionIdentity = null;
            _reflectionObservationPollElapsed = 0;
            _reflectionObservationInitialPose = null;
            _reflectionObservationArmed = false;
            _reflectionObservationFailedSession = null;
            _reflectionRemarkShownSession = null;
            _reflectionRemarkPendingSession = null;
            _reflectionObservationDispatchSession = null;
            _reflectionObservationDispatching = false;
            _reflectionObservationRayExclusions.Clear();
            return;
        }

        if (ReferenceEquals(_reflectionObservationSessionIdentity, sessionIdentity)) return;

        _reflectionObservationGeneration++;
        _reflectionObservationSessionIdentity = sessionIdentity;
        _reflectionObservationPollElapsed = 0;
        _reflectionObservationInitialPose = null;
        _reflectionObservationArmed = false;
        _reflectionObservationFailedSession = null;
        _reflectionRemarkShownSession = null;
        _reflectionRemarkPendingSession = null;
        _reflectionObservationDispatchSession = null;
        _reflectionObservationDispatching = false;
        _reflectionObservationRayExclusions.Clear();
    }

    private void ProcessReflectionObservation(double delta)
    {
        if (!IsInsideTree()
            || _bridge is not { } bridge
            || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.SessionIdentity is not { } session)
        {
            _reflectionObservationFailedSession = null;
            _reflectionRemarkPendingSession = null;
            return;
        }

        if (!ReferenceEquals(_reflectionObservationSessionIdentity, session))
            ResetReflectionObservation(session);

        var projection = bridge.CurrentW02WaterProjection();
        if (projection is null || projection.ActiveSceneId != RiverSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            _reflectionObservationFailedSession = null;
            if (!bridge.IsW01PresentationDeferred)
                _reflectionRemarkPendingSession = null;
            _reflectionObservationPollElapsed = 0;
            return;
        }

        if (bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            _reflectionObservationFailedSession = null;
            if (!ReferenceEquals(_reflectionRemarkPendingSession, session))
            {
                _reflectionObservationPollElapsed = 0;
                return;
            }

            _reflectionObservationPollElapsed += Math.Max(0, delta);
            if (_reflectionObservationPollElapsed >= ReflectionObservationPollSeconds)
            {
                _reflectionObservationPollElapsed = 0;
                _ = ShowRecordedReflectionRemark(bridge, session);
            }
            return;
        }
        _reflectionRemarkPendingSession = null;
        if (!bridge.IsInteractionAvailable(ReflectionInteractionId)) return;

        _reflectionObservationPollElapsed += Math.Max(0, delta);
        if (_reflectionObservationPollElapsed < ReflectionObservationPollSeconds) return;
        _reflectionObservationPollElapsed = 0;

        if (_reflectionObservationDispatching
            || MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
            return;

        if (_reflectionObservationStand is not { } stand
            || !GodotObject.IsInstanceValid(stand)
            || !stand.IsVisibleInTree()
            || _reflectionObservationPoints.Length != 4)
            return;

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is null || !GodotObject.IsInstanceValid(camera) || !camera.Current)
            return;

        var playerPosition = player.GlobalPosition;
        var forward = (-camera.GlobalBasis.Z).Normalized();
        // Entry shares this stand; observe only after the player moves or looks.
        // Sample outside the footprint too, so an ordinary approach can arm it.
        if (!_reflectionObservationArmed)
        {
            if (_reflectionObservationInitialPose is not { } initialPose)
            {
                _reflectionObservationInitialPose = (playerPosition, forward);
                return;
            }
            if (new Vector2(playerPosition.X, playerPosition.Z)
                    .DistanceTo(new Vector2(initialPose.Position.X, initialPose.Position.Z)) < ReflectionObservationRetryDistance
                && forward.Dot(initialPose.Forward) >= ReflectionObservationRetryForwardDot)
                return;
            _reflectionObservationArmed = true;
        }

        var standLocal = stand.ToLocal(playerPosition);
        if (Math.Abs(standLocal.X) > _reflectionObservationStandHalfExtents.X
            || Math.Abs(standLocal.Z) > _reflectionObservationStandHalfExtents.Y)
        {
            _reflectionObservationFailedSession = null;
            return;
        }

        if (ObservationFailureStillSame(session, playerPosition, forward)) return;

        if (!_reflectionObservationRayExclusions.Contains(player.GetRid()))
        {
            _reflectionObservationRayExclusions.Clear();
            _reflectionObservationRayExclusions.Add(player.GetRid());
        }

        var eye = camera.GlobalPosition;
        var space = GetWorld3D().DirectSpaceState;
        foreach (var point in _reflectionObservationPoints)
        {
            if (!ReflectionPointFramedAndVisible(camera, eye, point, space)) return;
        }

        _reflectionObservationDispatching = true;
        _reflectionObservationDispatchSession = session;
        DispatchReflectionObservationAsync(
            bridge, session, player, playerPosition, forward, _reflectionObservationGeneration);
    }

    private bool ReflectionPointFramedAndVisible(
        Camera3D camera,
        Vector3 eye,
        Vector3 point,
        PhysicsDirectSpaceState3D space)
    {
        var local = camera.ToLocal(point);
        if (local.Z >= -0.05f) return false;

        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var aspect = viewport.X / Math.Max(viewport.Y, 1f);
        var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        var halfWidth = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent : tangent * aspect;
        var halfHeight = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent / aspect : tangent;
        if (Math.Abs(local.X / -local.Z) > halfWidth || Math.Abs(local.Y / -local.Z) > halfHeight)
            return false;

        using var ray = PhysicsRayQueryParameters3D.Create(eye, point, 3u, _reflectionObservationRayExclusions);
        using var hit = space.IntersectRay(ray);
        return hit.Count == 0;
    }

    private async void DispatchReflectionObservationAsync(
        RuntimeBridge bridge,
        object session,
        FirstPersonController player,
        Vector3 playerPosition,
        Vector3 forward,
        int generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(ReflectionInteractionId);
        }
        catch (Exception exception)
        {
            GD.PushError($"W02 water-reflection observation failed before commit: {exception.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _reflectionObservationGeneration) return;
        if (ReferenceEquals(_reflectionObservationDispatchSession, session))
        {
            _reflectionObservationDispatchSession = null;
            _reflectionObservationDispatching = false;
        }

        if (!GodotObject.IsInstanceValid(bridge) || !ReferenceEquals(bridge.SessionIdentity, session)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge))
        {
            ApplyProjection();
            return;
        }

        var projection = bridge.CurrentW02WaterProjection();
        if (committed && bridge.IsW01PresentationDeferred && bridge.ActiveSceneId == RiverSceneId)
        {
            // A failed pre-projection load can return to this same kernel.
            // Keep only this newly earned payoff; normal save restoration never seeds it.
            _reflectionRemarkPendingSession = session;
            return;
        }
        if (committed && projection is { ActiveSceneId: RiverSceneId } current
            && ReferenceEquals(current.SessionIdentity, session)
            && bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            _reflectionObservationFailedSession = null;
            await ShowRecordedReflectionRemark(bridge, session, deferIfModal: true);
            return;
        }

        if (projection is { ActiveSceneId: RiverSceneId } stillCurrent
            && ReferenceEquals(stillCurrent.SessionIdentity, session)
            && !bridge.PhotoWorldFactObserved(ReflectionFactId))
        {
            _reflectionObservationFailedSession = session;
            _reflectionObservationFailedPosition = playerPosition;
            _reflectionObservationFailedForward = forward;
            return;
        }

        ApplyProjection();
    }

    private bool ObservationFailureStillSame(object session, Vector3 position, Vector3 forward)
    {
        if (!ReferenceEquals(_reflectionObservationFailedSession, session))
        {
            _reflectionObservationFailedSession = null;
            return false;
        }

        if (position.DistanceTo(_reflectionObservationFailedPosition) < ReflectionObservationRetryDistance
            && forward.Dot(_reflectionObservationFailedForward) >= ReflectionObservationRetryForwardDot)
            return true;

        _reflectionObservationFailedSession = null;
        return false;
    }

    private Task<bool> ShowRecordedReflectionRemark(
        RuntimeBridge bridge,
        object? session,
        bool deferIfModal = false)
    {
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || session is null || !ReferenceEquals(session, bridge.SessionIdentity)
            || bridge.CurrentW02WaterProjection() is not { ActiveSceneId: RiverSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session)
            || !bridge.PhotoWorldFactObserved(ReflectionFactId))
            return Task.FromResult(true);

        if (ReferenceEquals(_reflectionRemarkShownSession, session))
        {
            _reflectionRemarkPendingSession = null;
            return Task.FromResult(true);
        }

        var sketchRecorded = bridge.PhotoWorldKnowledgeEntries().Any(entry =>
            entry.SourceId == ReflectionInteractionId
            && entry.Images?.Any(image => image.AssetId == ReflectionSketchAssetId) == true);
        if (!sketchRecorded)
        {
            _reflectionRemarkPendingSession = null;
            return Task.FromResult(true);
        }

        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var modalOpen = MainMenuUi.AnyUndismissed(GetTree())
            || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
            || player?.ModalOpen == true;
        if (player is null || !GodotObject.IsInstanceValid(player) || player.IsQueuedForDeletion() || modalOpen)
        {
            if (deferIfModal || ReferenceEquals(_reflectionRemarkPendingSession, session))
                _reflectionRemarkPendingSession = session;
            return Task.FromResult(true);
        }

        _reflectionRemarkPendingSession = null;
        _reflectionRemarkShownSession = session;
        var journalHint = InputBindingService.ActionHint("journal", player.CurrentInputDevice == "gamepad");
        player.ShowRemark("Айдар",
            $"Край настила теряется за камышом. Я набросал его в книжке — обойду и проверю доски. {journalHint} — открыть записную книжку.");
        return Task.FromResult(true);
    }
}
