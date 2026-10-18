using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private const float HousePassageHalfWidth = 0.72f;
    private const float HousePassageHalfDepth = 1.65f;
    private const float HousePassageGroundTolerance = 0.48f;
    private const float HousePassageExitSweepDistance = 0.36f;
    private const float HousePassageSideEpsilon = 0.05f;
    private const float HousePassageCooldownClearance = 0.9f;
    private const float HouseViewMaximumDistance = 12f;
    private const float HousePhotographerViewMaximumDistance = 48f;

    private readonly record struct HouseLinkFrame(Node3D Frame, Node3D Other, int PairIndex, bool IsEntry);

    private readonly KinematicCollision3D _housePassageSweep = new();
    private readonly List<HouseLinkFrame> _houseLinkFrames = [];
    private readonly global::Godot.Collections.Array<Rid> _houseViewRayExclusions = new();
    private Node3D? _houseAppleWitness;
    private Node3D? _houseChairWitness;
    private CollisionObject3D? _houseAppleWitnessCollider;
    private CollisionObject3D? _houseChairWitnessCollider;
    private Node3D? _housePassageCooldownFrame;
    private object? _houseTraversalSession;
    private object? _pendingHouseViewSession;
    private int _pendingHouseViewPair = -1;
    private int _houseTrackedFrame = -1;
    private int _housePreviousPlaneSide;
    private bool _houseViewDispatching;
    private int _houseViewDispatchGeneration;
    private bool _houseSpatialMissingMarkerWarningSent;

    private void BuildHouseSpatialLinks()
    {
        _houseLinkFrames.Clear();
        var h02 = GetNodeOrNull<Node3D>("W03World/H02PassageFrame");
        var h02Exit = GetNodeOrNull<Node3D>("W03World/H02PassageExitFrame");
        var h03 = GetNodeOrNull<Node3D>("W03World/H03PassageFrame");
        var h03Exit = GetNodeOrNull<Node3D>("W03World/H03PassageExitFrame");
        _houseAppleWitness = GetNodeOrNull<Node3D>("W03World/H04AppleTreeWitness");
        _houseChairWitness = GetNodeOrNull<Node3D>("W03World/H04GardenChairWitness");

        if (h02 is not null && h02Exit is not null)
        {
            _houseLinkFrames.Add(new HouseLinkFrame(h02, h02Exit, 0, true));
            _houseLinkFrames.Add(new HouseLinkFrame(h02Exit, h02, 0, false));
        }
        if (h03 is not null && h03Exit is not null)
        {
            _houseLinkFrames.Add(new HouseLinkFrame(h03, h03Exit, 1, true));
            _houseLinkFrames.Add(new HouseLinkFrame(h03Exit, h03, 1, false));
        }

        _houseAppleWitnessCollider = _houseAppleWitness is null ? null : FindHouseCollisionAncestor(_houseAppleWitness);
        _houseChairWitnessCollider = _houseChairWitness is null ? null : FindHouseCollisionAncestor(_houseChairWitness);
        if (_houseLinkFrames.Count != 4 || _houseAppleWitness is null || _houseChairWitness is null)
        {
            if (!_houseSpatialMissingMarkerWarningSent)
            {
                GD.PushWarning("W03 spatial links need H02/H03 entry and landing frames plus both H04 composition witness markers.");
                _houseSpatialMissingMarkerWarningSent = true;
            }
        }
    }

    private void ProcessHouseSpatialLinks()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session)
            || _houseLinkFrames.Count != 4)
        {
            ResetHouseDoorObservationSession(null);
            ResetHousePassageTracking();
            return;
        }

        if (!ReferenceEquals(_houseTraversalSession, session))
            ResetHouseDoorObservationSession(session);

        if (HouseActionModalOpen()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)
            || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
        {
            ResetHousePassageTracking();
            return;
        }

        if (_housePassageCooldownFrame is { } cooldown && GodotObject.IsInstanceValid(cooldown))
        {
            var cooldownLocal = cooldown.GlobalTransform.AffineInverse() * player.GlobalTransform;
            if (Math.Abs(cooldownLocal.Origin.Z) < HousePassageCooldownClearance)
            {
                ResetHousePassageTracking();
                TryObservePendingHouseDoorView(bridge, session, player);
                return;
            }
            _housePassageCooldownFrame = null;
            ResetHousePassageTracking();
        }

        if (TryObservePendingHouseDoorView(bridge, session, player)) return;

        for (var index = 0; index < _houseLinkFrames.Count; index++)
        {
            var binding = _houseLinkFrames[index];
            if (!GodotObject.IsInstanceValid(binding.Frame) || !GodotObject.IsInstanceValid(binding.Other)) continue;
            var local = binding.Frame.GlobalTransform.AffineInverse() * player.GlobalTransform;
            if (Math.Abs(local.Origin.X) > HousePassageHalfWidth
                || Math.Abs(local.Origin.Y) > HousePassageGroundTolerance
                || Math.Abs(local.Origin.Z) > HousePassageHalfDepth)
            {
                if (_houseTrackedFrame == index) ResetHousePassageTracking();
                continue;
            }

            var distance = local.Origin.Z;
            if (_houseTrackedFrame != index)
            {
                _houseTrackedFrame = index;
                _housePreviousPlaneSide = HousePassagePlaneSide(distance);
                return;
            }

            var previousSide = _housePreviousPlaneSide;
            var currentSide = HousePassagePlaneSide(distance);
            if (currentSide == 0) return;

            _housePreviousPlaneSide = currentSide;
            var crossedForward = previousSide < 0 && currentSide > 0;
            var crossedReverse = previousSide > 0 && currentSide < 0;
            if (!crossedForward && !crossedReverse) return;

            var crossingDirection = currentSide > previousSide ? 1f : -1f;
            TraverseHouseLink(player, binding, local, crossingDirection, bridge, session);
            return;
        }

        if (_houseTrackedFrame >= 0) ResetHousePassageTracking();
    }

    private void TraverseHouseLink(
        FirstPersonController player,
        HouseLinkFrame binding,
        Transform3D localPlayer,
        float crossingDirection,
        RuntimeBridge bridge,
        object session)
    {
        // Once an observation dispatch has started, the actual view has already
        // been witnessed. Do not let a later step through another frame revoke
        // that in-flight reducer action or start a duplicate proof.
        if (!_houseViewDispatching && binding.IsEntry && crossingDirection > 0f)
        {
            _pendingHouseViewPair = binding.PairIndex;
            _pendingHouseViewSession = session;
            _houseViewDispatchGeneration++;
        }
        else if (!_houseViewDispatching && _pendingHouseViewPair >= 0)
        {
            // A return through a landing frame, or leaving via another portal,
            // cancels only the uncommitted observation proof. Recorded facts
            // remain kernel-owned and are never rolled back here.
            ClearPendingHouseDoorView();
        }

        var projection = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (!ReferenceEquals(bridge.SessionIdentity, session)
            || projection is not { ActiveSceneId: HouseSceneId }
            || !ReferenceEquals(projection.SessionIdentity, session)
            || HouseActionModalOpen())
        {
            ResetHousePassageTracking();
            return;
        }

        var destinationTransform = binding.Other.GlobalTransform * localPlayer;
        var destinationMotion = binding.Other.GlobalBasis.Z.Normalized()
            * crossingDirection * HousePassageExitSweepDistance;
        if (player.TestMove(destinationTransform, destinationMotion, _housePassageSweep, .001f, false, 4))
        {
            // Keep the portal uncommitted when the matching exit cannot accept
            // the real player capsule; no hidden progress or fallback placement.
            if (!_houseViewDispatching) ClearPendingHouseDoorView();
            ResetHousePassageTracking();
            return;
        }

        var relativeVelocity = binding.Frame.GlobalBasis.Orthonormalized().Inverse() * player.Velocity;
        var destinationVelocity = binding.Other.GlobalBasis.Orthonormalized() * relativeVelocity;
        player.ApplyTraversalTransform(destinationTransform, destinationVelocity);
        _housePassageCooldownFrame = binding.Other;
        ResetHousePassageTracking();
    }

    private bool TryObservePendingHouseDoorView(
        RuntimeBridge bridge, object session, FirstPersonController player)
    {
        var pair = _pendingHouseViewPair;
        if (pair is < 0 or > 1 || !ReferenceEquals(_pendingHouseViewSession, session))
            return false;
        if (bridge.PhotoWorldFactObserved(pair == 0 ? DoorLeftFactId : DoorRightFactId))
        {
            ClearPendingHouseDoorView();
            return false;
        }
        if (_houseViewDispatching
            || _houseAppleWitness is not { } apple || !GodotObject.IsInstanceValid(apple)
            || _houseChairWitness is not { } chair || !GodotObject.IsInstanceValid(chair)
            || !IsHouseDoorObservationContextCurrent(bridge, session, player))
            return false;

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is not { Current: true }) return false;
        var eye = camera.GlobalPosition;
        if (eye.DistanceTo(apple.GlobalPosition) > HouseViewMaximumDistance
            || eye.DistanceTo(chair.GlobalPosition) > HouseViewMaximumDistance)
            return false;

        if (!_houseViewRayExclusions.Contains(player.GetRid()))
        {
            _houseViewRayExclusions.Clear();
            _houseViewRayExclusions.Add(player.GetRid());
        }

        var space = GetWorld3D().DirectSpaceState;
        if (!HouseWitnessFramedAndVisible(camera, apple.GlobalPosition, _houseAppleWitnessCollider, space)
            || !HouseWitnessFramedAndVisible(camera, chair.GlobalPosition, _houseChairWitnessCollider, space))
            return false;

        _houseViewDispatching = true;
        var generation = ++_houseViewDispatchGeneration;
        DispatchHouseDoorObservationAsync(bridge, session, pair, generation);
        return true;
    }

    private bool HouseWitnessFramedAndVisible(
        Camera3D camera, Vector3 point, CollisionObject3D? expectedCollider, PhysicsDirectSpaceState3D space)
    {
        if (!camera.IsPositionInFrustum(point)) return false;

        using var ray = PhysicsRayQueryParameters3D.Create(
            camera.GlobalPosition, point, 3u, _houseViewRayExclusions);
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0) return true;
        return expectedCollider is not null
            && hit["collider"].AsGodotObject() is CollisionObject3D actual
            && ReferenceEquals(actual, expectedCollider);
    }

    private static CollisionObject3D? FindHouseCollisionAncestor(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (current is CollisionObject3D collision) return collision;
        return null;
    }

    private bool IsHouseDoorObservationContextCurrent(
        RuntimeBridge bridge, object session, FirstPersonController player)
    {
        var projection = bridge.CurrentFullGamePhotoWorldRouteProjection();
        return ReferenceEquals(bridge.SessionIdentity, session)
            && projection is { ActiveSceneId: HouseSceneId }
            && ReferenceEquals(projection.SessionIdentity, session)
            && !HouseActionModalOpen()
            && !player.VehicleControlled && !player.IsClimbingLadder && player.IsOnFloor();
    }

    private async void DispatchHouseDoorObservationAsync(
        RuntimeBridge bridge, object session, int pair, int generation)
    {
        var factId = pair == 0 ? DoorLeftFactId : DoorRightFactId;
        var interactionId = pair == 0 ? DoorLeftInteractionId : DoorRightInteractionId;
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(interactionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W03 doorway observation {interactionId} failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        if (generation != _houseViewDispatchGeneration) return;
        _houseViewDispatching = false;
        if (!GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, session)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge))
        {
            ResetHouseDoorObservationSession(null);
            return;
        }

        var projection = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (committed && projection is { ActiveSceneId: HouseSceneId }
            && ReferenceEquals(projection.SessionIdentity, session)
            && bridge.PhotoWorldFactObserved(factId))
        {
            _pendingHouseViewPair = -1;
            _pendingHouseViewSession = null;
            TryDispatchHouseTwoViews();
            return;
        }

        // A failed/rejected observation must be earned again by crossing forward;
        // retaining a stale camera sample could silently award it later.
        _pendingHouseViewPair = -1;
        _pendingHouseViewSession = null;
    }

    private void ClearPendingHouseDoorView()
    {
        _pendingHouseViewPair = -1;
        _pendingHouseViewSession = null;
        _houseViewDispatchGeneration++;
        _houseViewDispatching = false;
    }

    private void ResetHouseDoorObservationSession(object? session)
    {
        if (session is not null && ReferenceEquals(_houseTraversalSession, session)) return;
        _houseViewDispatchGeneration++;
        _houseTraversalSession = session;
        _pendingHouseViewSession = null;
        _pendingHouseViewPair = -1;
        _houseViewDispatching = false;
        _houseViewRayExclusions.Clear();
        _housePassageCooldownFrame = null;
        _twoViewsDispatching = false;
        _twoViewsDispatchSession = null;
    }

    private void ResetHousePassageTracking()
    {
        _houseTrackedFrame = -1;
        _housePreviousPlaneSide = 0;
    }

    private static int HousePassagePlaneSide(float distance) =>
        distance > HousePassageSideEpsilon ? 1 : distance < -HousePassageSideEpsilon ? -1 : 0;

    private void ExitHouseSpatialLinks()
    {
        _houseViewDispatchGeneration++;
        _housePassageSweep.Dispose();
        _houseLinkFrames.Clear();
        _houseViewRayExclusions.Clear();
        _houseAppleWitness = null;
        _houseChairWitness = null;
        _houseAppleWitnessCollider = null;
        _houseChairWitnessCollider = null;
        _housePassageCooldownFrame = null;
        _houseTraversalSession = null;
        _pendingHouseViewSession = null;
        _pendingHouseViewPair = -1;
        _houseTrackedFrame = -1;
        _housePreviousPlaneSide = 0;
        _houseViewDispatching = false;
    }
}
