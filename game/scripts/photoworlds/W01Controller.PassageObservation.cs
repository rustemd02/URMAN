using System;
using System.Threading.Tasks;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const string ForestBeyondFrameInteractionId = "urman.fullgame:interaction/pw-forest-photographer";
    private const string ForestBeyondFrameFactId = "forest_beyond_frame_seen";

    private bool _rootPassageObservationDispatching;
    private long _rootPassageObservationGeneration;
    private float _rootPassageObservedExitSide;
    private (Vector3 Point, CollisionShape3D Contact)[] _rootPassageObservationSubjects = [];
    private int _rootPassageSeenSubjects;
    private bool _rootPassageObservationFailed;
    private Vector3 _rootPassageObservationFailedPosition;
    private Vector3 _rootPassageObservationFailedForward;

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

    /// <summary>Projects the next real subject from live traversal proof without recording progress.</summary>
    internal bool TryGetRootPassageInspectionTarget(object? identity, out Vector3 point, out int seenCount)
    {
        point = default;
        seenCount = 0;
        if (identity is null || !_rootPassageEnabled || _rootPassageObservedExitSide == 0f
            || !ReferenceEquals(_rootPassageSessionIdentity, identity)
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || bridge.ActiveSceneId != ForestSceneId
            || !ReferenceEquals(bridge.PhotoWorldPresentationIdentity, identity)
            || _rootPassageObservationSubjects.Length != 3)
            return false;

        // Keep the last subject while its earned dispatch finishes, rather than pointing back into the passage.
        point = _rootPassageObservationSubjects[^1].Point;
        var unseenMask = (1 << _rootPassageObservationSubjects.Length) - 1;
        for (var bits = _rootPassageSeenSubjects & unseenMask; bits != 0; bits &= bits - 1)
            seenCount++;
        for (var index = 0; index < _rootPassageObservationSubjects.Length; index++)
        {
            if ((_rootPassageSeenSubjects & (1 << index)) != 0) continue;
            point = _rootPassageObservationSubjects[index].Point;
            break;
        }
        return true;
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
