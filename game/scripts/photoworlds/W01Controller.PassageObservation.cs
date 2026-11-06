using System;
using System.Threading.Tasks;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class W01Controller
{
    private const string ForestBeyondFrameInteractionId = "urman.fullgame:interaction/pw-forest-photographer";
    private const string ForestBeyondFrameFactId = "forest_beyond_frame_seen";
    private const float RootPassageCueLookAhead = 1.5f;
    private Vector3 _rootPassageWalkEntry;
    private Vector3 _rootPassageWalkExit;

    private bool _rootPassageObservationDispatching;
    private long _rootPassageObservationGeneration;
    private float _rootPassageObservedExitSide;
    private (Vector3 Point, CollisionShape3D Contact)[] _rootPassageObservationSubjects = [];
    private int _rootPassageSeenSubjects;
    private bool _rootPassageObservationFailed;
    private Vector3 _rootPassageObservationFailedPosition;
    private Vector3 _rootPassageObservationFailedForward;

    internal ForestPassageObservationSnapshot? CaptureForestPassageObservation(object session, string visitId)
    {
        if (!_rootPassageEnabled || _rootPassageObservedExitSide == 0f
            || !ReferenceEquals(_rootPassageSessionIdentity, session)
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || bridge.PhotoWorldFactObserved(ForestBeyondFrameFactId)) return null;

        var seen = _rootPassageSeenSubjects;
        // If all three views are in flight, require a fresh look at one
        // subject after Continue. Loading alone must never commit the fact.
        if (seen == 0b111) seen &= 0b011;
        return new ForestPassageObservationSnapshot(visitId, (int)_rootPassageObservedExitSide, seen);
    }

    internal void RestoreForestPassageObservation(object session, ForestPassageObservationSnapshot observation)
    {
        if (!ForestEpisodeReady || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: ForestSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, session)
            || !bridge.PhotoWorldFactObserved(ForestCameraBackFactId)
            || bridge.PhotoWorldFactObserved(ForestBeyondFrameFactId)) return;

        ApplyRootPassageProjection(projection);
        _rootPassageObservedExitSide = observation.ExitSide;
        _rootPassageSeenSubjects = observation.SeenSubjects;
    }

    /// <summary>Projects the F11 observation route from the live W01 presentation without recording progress.</summary>
    internal bool TryGetForestCameraBackObjectiveTarget(
        object? identity,
        Vector3 playerPosition,
        out Vector3 point,
        out string label,
        out bool inspectionPoint)
    {
        point = default;
        label = string.Empty;
        inspectionPoint = false;
        if (identity is null
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || !ReferenceEquals(bridge.PhotoWorldPresentationIdentity, identity)
            || bridge.CurrentW01BranchProjection() is not { ActiveSceneId: ForestSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, identity)
            || !ReferenceEquals(bridge.SessionIdentity, identity)
            || GetNodeOrNull<Marker3D>("Anchors/F11") is not { } frame
            || !GodotObject.IsInstanceValid(frame))
            return false;

        // Save/Continue may clear traversal-side state; choose the nearer authored endpoint for this cue only.
        if (_rootLinkFrameF12 is { } entryFrame && GodotObject.IsInstanceValid(entryFrame)
            && _rootLinkFrameF13 is { } returnFrame && GodotObject.IsInstanceValid(returnFrame))
        {
            var position = new Vector2(playerPosition.X, playerPosition.Z);
            var entry = new Vector2(entryFrame.GlobalPosition.X, entryFrame.GlobalPosition.Z);
            var exit = new Vector2(returnFrame.GlobalPosition.X, returnFrame.GlobalPosition.Z);
            if (position.DistanceSquaredTo(exit) < position.DistanceSquaredTo(entry))
            {
                point = RootPassageApproachCue(returnFrame, portal: 1, playerPosition);
                label = "Вернитесь через проход";
                return true;
            }
        }

        // Preserve the observation owner's existing ±1.5 m square around F11.
        // Outside it, keep the route cue at F11; inside it, point at the real
        // crossbar observation surface from the active tableau.
        point = frame.GlobalPosition;
        label = "За тремя стволами";
        var offset = playerPosition - frame.GlobalPosition;
        if (Math.Abs(offset.X) > 1.5f || Math.Abs(offset.Z) > 1.5f)
            return true;

        if (_photoTableauVisual is not { Visible: true } tableau
            || !GodotObject.IsInstanceValid(tableau) || !tableau.IsVisibleInTree()
            || _forestSplitBirchContact is not { } splitBirch || !GodotObject.IsInstanceValid(splitBirch)
            || _forestCrossbarContact is not { } crossbar || !GodotObject.IsInstanceValid(crossbar)
            || _forestRootArchContact is not { } rootArch || !GodotObject.IsInstanceValid(rootArch))
            return true;

        point = _forestCrossbarObservationPoint;
        label = "Посмотрите на всю группу";
        inspectionPoint = true;
        return true;
    }

    /// <summary>Guides the player along the actual root ribbon and then to the next observation subject.</summary>
    internal bool TryGetRootPassageObjectiveTarget(object? identity, Vector3 playerPosition,
        out Vector3 point, out string label, out bool inspectionPoint)
    {
        point = default;
        label = string.Empty;
        inspectionPoint = false;
        if (identity is null || !_rootPassageEnabled
            || !ReferenceEquals(_rootPassageSessionIdentity, identity)
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || bridge.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(bridge.PhotoWorldPresentationIdentity, identity)
            || _rootLinkFrameF12 is not { } entry || !GodotObject.IsInstanceValid(entry)
            || _rootLinkFrameF13 is not { } exit || !GodotObject.IsInstanceValid(exit)
            || _rootPassageObservationSubjects.Length != 3)
            return false;

        var exitDistance = exit.ToLocal(playerPosition).Z * _rootPassageObservedExitSide;
        if (_rootPassageObservedExitSide == 0f || exitDistance < -RootPassagePlaneHalfDepth)
        {
            // A save may preserve feet on the far side without the live crossing proof.
            var returning = playerPosition.DistanceSquaredTo(exit.GlobalPosition)
                < playerPosition.DistanceSquaredTo(entry.GlobalPosition);
            point = RootPassageApproachCue(returning ? exit : entry, returning ? 1 : 0, playerPosition);
            label = returning ? "Вернитесь через проход" : "Проход у корней";
            return true;
        }

        if (exitDistance <= RootPassagePlaneHalfDepth)
        {
            var mouth = _rootPassageObservedExitSide > 0f ? _rootPassageWalkExit : _rootPassageWalkEntry;
            point = exit.ToGlobal(RootPassageRibbonPoint(mouth, Mathf.Max(exitDistance, 0f) + RootPassageCueLookAhead));
            label = "Отойдите от поворота";
            return true;
        }

        // Keep the last subject while its earned dispatch finishes, rather than pointing back into the passage.
        point = _rootPassageObservationSubjects[^1].Point;
        var unseenMask = (1 << _rootPassageObservationSubjects.Length) - 1;
        var seenCount = 0;
        for (var bits = _rootPassageSeenSubjects & unseenMask; bits != 0; bits &= bits - 1)
            seenCount++;
        for (var index = 0; index < _rootPassageObservationSubjects.Length; index++)
        {
            if ((_rootPassageSeenSubjects & (1 << index)) != 0) continue;
            point = _rootPassageObservationSubjects[index].Point;
            break;
        }
        label = $"Осмотрите стволы: {seenCount}/3";
        inspectionPoint = true;
        return true;
    }

    private Vector3 RootPassageApproachCue(Marker3D frame, int portal, Vector3 playerPosition)
    {
        var position = frame.ToLocal(playerPosition);
        var side = _rootLinkTrackedPortal == portal && _rootLinkPreviousPlaneSide != 0
            ? _rootLinkPreviousPlaneSide : position.Z > 0f ? 1 : -1;
        var mouth = side > 0 ? _rootPassageWalkExit : _rootPassageWalkEntry;
        var distance = position.Z * side;
        var centre = RootPassageRibbonPoint(mouth, distance);
        if (distance >= Mathf.Abs(mouth.Z) || Mathf.Abs(position.X - centre.X) > 1.1f)
            return frame.ToGlobal(mouth);
        // Put the final cue beyond the trigger plane so walking to it crosses.
        var next = distance > RootPassageCueLookAhead
            ? RootPassageRibbonPoint(mouth, distance - RootPassageCueLookAhead)
            : RootPassageRibbonPoint(side > 0 ? _rootPassageWalkEntry : _rootPassageWalkExit, .35f);
        return frame.ToGlobal(next);
    }

    private static Vector3 RootPassageRibbonPoint(Vector3 mouth, float distance)
    {
        // Same centreline as the shipped HeroRoot_WalkRibbon, expressed from
        // SpatialLinkFrame to the GLB's WalkEntry or WalkExit.
        var progress = Mathf.Clamp(distance / Mathf.Abs(mouth.Z), 0f, 1f);
        var bow = Mathf.Sin(progress * Mathf.Pi * .5f);
        return new Vector3(mouth.X * bow * bow, mouth.Y * Mathf.SmoothStep(0f, 1f, progress), mouth.Z * progress);
    }

    private void BuildRootPassageObservationSubjects()
    {
        if (_heroRootAtF13 is not { } visual || _heroRootContactAtF13 is not { } contact)
            return;

        var names = new[] { "HeroRoot_Trunk_Center_LOD0", "HeroRoot_Trunk_Left_LOD0", "HeroRoot_Trunk_Right_LOD0" };
        _rootPassageObservationSubjects = new (Vector3, CollisionShape3D)[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            var mesh = RequireDescendant<MeshInstance3D>(visual, names[index]);
            var bounds = mesh.GetAabb();
            // These are solid trunks, not an arch: a point inside each lower
            // bole lets the ray prove that its actual visible surface is clear.
            var point = bounds.Position + bounds.Size * new Vector3(0.5f, 0.35f, 0.5f);
            _rootPassageObservationSubjects[index] = (mesh.ToGlobal(point),
                RequireDescendant<CollisionShape3D>(contact, $"{names[index]}_Contact"));
        }
    }

    /// <summary>Crossing arms inspection; it does not itself award the finding.</summary>
    private void ArmRootPassageObservation(float exitSide, FirstPersonController player)
    {
        _rootPassageObservedExitSide = exitSide;
        _rootPassageSeenSubjects = 0;
        _rootPassageObservationFailed = false;
        if (exitSide != 0f && _bridge is { } bridge
            && bridge.PhotoWorldFactObserved(ForestCameraBackFactId)
            && !bridge.PhotoWorldFactObserved(ForestBeyondFrameFactId))
            player.NotifyTraversal(RuntimeBridge.ForestBackInspectionCue);
    }

    private void TryObserveRootPassageBack()
    {
        if (_rootPassageObservedExitSide == 0f || _rootPassageObservationDispatching
            || !IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || bridge.SessionIdentity is not { } session
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor()
            || MainMenuUi.AnyUndismissed(GetTree())
            || player.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } camera
            || _rootLinkFrameF13 is not { } exitFrame
            || _heroRootContactAtF13 is not { } contact
            || _heroRootAtF13 is not { } visual || !visual.IsVisibleInTree()
            || _rootPassageObservationSubjects.Length != 3)
        {
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (projection is not { ActiveSceneId: ForestSceneId }
            || !ReferenceEquals(projection.SessionIdentity, session)
            || !ReferenceEquals(session, bridge.SessionIdentity)
            || !bridge.PhotoWorldFactObserved(ForestCameraBackFactId)
            || bridge.PhotoWorldFactObserved(ForestBeyondFrameFactId)
            || !bridge.IsInteractionAvailable(ForestBeyondFrameInteractionId))
        {
            return;
        }

        var position = player.GlobalPosition;
        var exitLocal = exitFrame.ToLocal(position);
        // Walk out of the bend on the crossed side, then inspect freely from
        // its surroundings; there is no hidden E target or exact stand point.
        if (exitLocal.Z * _rootPassageObservedExitSide <= RootPassagePlaneHalfDepth
            || new Vector2(exitLocal.X, exitLocal.Z).LengthSquared() > 24f * 24f)
            return;

        var forward = (-camera.GlobalBasis.Z).Normalized();
        if (_rootPassageObservationFailed
            && position.DistanceSquaredTo(_rootPassageObservationFailedPosition) < 0.2f * 0.2f
            && forward.Dot(_rootPassageObservationFailedForward) > 0.99f)
            return;

        if (!_forestObservationRayExclusions.Contains(player.GetRid()))
        {
            _forestObservationRayExclusions.Clear();
            _forestObservationRayExclusions.Add(player.GetRid());
        }
        var space = GetWorld3D().DirectSpaceState;
        // Inspect the group by walking and looking around, as with W03's separate subjects.
        for (var index = 0; index < _rootPassageObservationSubjects.Length; index++)
        {
            var bit = 1 << index;
            if ((_rootPassageSeenSubjects & bit) != 0) continue;
            var subject = _rootPassageObservationSubjects[index];
            if (ForestLandmarkFramedAndVisible(camera, camera.GlobalPosition, subject.Point,
                    subject.Contact, contact, space))
                _rootPassageSeenSubjects |= bit;
        }
        if (_rootPassageSeenSubjects != (1 << _rootPassageObservationSubjects.Length) - 1) return;

        var generation = _rootPassageObservationGeneration;
        _rootPassageObservationDispatching = true;
        _ = DispatchRootPassageObservationAsync(bridge, session, player, position, forward, generation);
    }

    /// <summary>Invalidates pending passage feedback when its W01 presentation/session is reset or exits.</summary>
    private void ResetRootPassageObservation()
    {
        unchecked
        {
            _rootPassageObservationGeneration++;
        }

        _rootPassageObservationDispatching = false;
        _rootPassageObservedExitSide = 0f;
        _rootPassageSeenSubjects = 0;
        _rootPassageObservationFailed = false;
    }

    private async Task DispatchRootPassageObservationAsync(
        RuntimeBridge bridge,
        object session,
        FirstPersonController player,
        Vector3 position,
        Vector3 forward,
        long generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(ForestBeyondFrameInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W01 root-passage observation failed before commit: {error.Message}");
        }
        finally
        {
            if (generation == _rootPassageObservationGeneration)
                _rootPassageObservationDispatching = false;
        }

        if (!IsCurrentRootPassageObservation(bridge, session, generation)) return;
        if (!committed)
        {
            _rootPassageSeenSubjects = 0;
            _rootPassageObservationFailed = true;
            _rootPassageObservationFailedPosition = position;
            _rootPassageObservationFailedForward = forward;
            return;
        }

        if (!bridge.PhotoWorldFactObserved(ForestBeyondFrameFactId)
            || !GodotObject.IsInstanceValid(player)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
            || player.ModalOpen || MainMenuUi.AnyUndismissed(GetTree()))
        {
            return;
        }

        player.NotifyTraversal("Проход вывел меня за край снимка.");
    }

    private bool IsCurrentRootPassageObservation(RuntimeBridge bridge, object session, long generation)
    {
        if (generation != _rootPassageObservationGeneration
            || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || !GodotObject.IsInstanceValid(bridge) || bridge.IsQueuedForDeletion() || !bridge.IsInsideTree()
            || !IsValidCurrentBridge(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, session))
        {
            return false;
        }

        var projection = bridge.CurrentW01BranchProjection();
        return projection is { ActiveSceneId: ForestSceneId }
            && ReferenceEquals(projection.SessionIdentity, session)
            && ReferenceEquals(session, bridge.SessionIdentity);
    }
}
