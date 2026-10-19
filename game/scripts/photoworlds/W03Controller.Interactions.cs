using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private const string ShutterInteractionId = "urman.fullgame:interaction/pw-house-shutter";
    private const string DoorLeftInteractionId = "urman.fullgame:interaction/pw-house-door-left";
    private const string DoorRightInteractionId = "urman.fullgame:interaction/pw-house-door-right";
    private const string TwoViewsInteractionId = "urman.fullgame:interaction/pw-house-two-views";
    private const string CameraPositionInteractionId = "urman.fullgame:interaction/pw-house-camera-position";
    private const string ClothInteractionId = "urman.fullgame:interaction/pw-house-cloth";
    private const string FrameInteractionId = "urman.fullgame:interaction/pw-house-frame";
    private const string ReturnInteractionId = "urman.fullgame:interaction/pw-house-return";

    private const string ShutterFactId = "house_shutter_opened";
    private const string DoorLeftFactId = "house_door_left_seen";
    private const string DoorRightFactId = "house_door_right_seen";
    private const string TwoViewsFactId = "house_two_views_seen";
    private const string CameraPositionFactId = "house_camera_position_seen";
    private const string ClothFactId = "cloth_cleared";
    private const string FrameFactId = "house_frame_seen";

    private enum HousePhysicalAction { None, Shutter, Cloth }
    private enum HousePhysicalPhase { Idle, Moving, Committing }

    private readonly List<InteractionTarget> _houseTargets = [];
    private InteractionTarget? _shutterInteractionTarget;
    private InteractionTarget? _clothInteractionTarget;
    private Node3D? _shutterPivot;
    private Node3D? _shutterTargetAnchor;
    private Node3D? _cameraWitness;
    private Node3D? _cameraShadowWitness;
    private Node3D? _cameraWindowWitness;
    private CollisionObject3D? _cameraShadowCollider;
    private CollisionObject3D? _cameraWindowCollider;
    private Node3D? _cloth;
    private Node3D? _clothHook;
    private Node3D? _clothShelf;
    private Node3D? _windowFrame;
    private Node3D? _houseExitAnchor;
    private Transform3D _shutterClosedTransform;
    private Transform3D _shutterOpenTransform;
    private Transform3D _clothHookTransform;
    private Transform3D _clothShelfTransform;
    private HousePhysicalAction _housePhysicalAction;
    private HousePhysicalPhase _housePhysicalPhase;
    private InteractionTarget? _housePhysicalActionTarget;
    private object? _housePhysicalActionSession;
    private string? _housePhysicalActionSceneId;
    private int _housePhysicalActionGeneration;
    private Transform3D _housePhysicalActionStart;
    private Transform3D _housePhysicalActionEnd;
    private double _housePhysicalActionElapsed;
    private double _housePhysicalActionDuration;
    private bool _twoViewsDispatching;
    private object? _twoViewsDispatchSession;
    private object? _housePhotographerObservationSession;
    private int _housePhotographerGeneration;
    private bool _housePhotographerDispatching;
    private bool _houseSawPhotographerShadow;
    private bool _houseSawPhotographerWindow;
    private bool _houseMissingMarkerWarningSent;

    public bool PhotoBookReturnActionInProgress => _housePhysicalPhase != HousePhysicalPhase.Idle
        || _houseViewDispatching
        || _twoViewsDispatching
        || _housePhotographerDispatching;

    private void BuildHouseGameplay()
    {
        _shutterPivot = RequireHouseMarker("W03World/ShutterPivot");
        _shutterTargetAnchor = RequireHouseMarker("W03World/ShutterPivot/ShutterTarget");
        _cameraWitness = RequireHouseMarker("W03World/H05PhotographerWitness");
        _cameraShadowWitness = RequireHouseMarker("W03World/H05StrapShadowWitness");
        _cameraWindowWitness = RequireHouseMarker("W03World/H05WindowCompositionWitness");
        _cloth = RequireHouseMarker("W03World/H06WindowFrame/Cloth");
        _clothHook = RequireHouseMarker("W03World/H06WindowFrame/ClothHook");
        _clothShelf = RequireHouseMarker("W03World/H06WindowFrame/ClothShelf");
        _windowFrame = RequireHouseMarker("W03World/H06WindowFrame");
        _houseExitAnchor = RequireHouseMarker("Anchors/H08");

        if (_shutterPivot is { } shutter)
        {
            _shutterClosedTransform = shutter.GlobalTransform;
            _shutterOpenTransform = new Transform3D(
                shutter.GlobalBasis * new Basis(Vector3.Up, Mathf.DegToRad(72f)), shutter.GlobalPosition);
        }

        if (_cloth is { } cloth)
            _clothHookTransform = cloth.GlobalTransform;
        if (_clothShelf is { } shelf)
            _clothShelfTransform = shelf.GlobalTransform;

        var bridge = _bridge is { } current && GodotObject.IsInstanceValid(current) ? current : null;
        if (_shutterPivot is not null && _shutterTargetAnchor is not null)
        {
            _shutterInteractionTarget = EnsureHouseTarget(
                "HouseShutterTarget", ShutterInteractionId, "urman.fullgame:text/pw-label-house-shutter",
                _shutterTargetAnchor, 0.42f, 0f, bridge);
            if (_shutterInteractionTarget is { } shutterTarget)
                shutterTarget.PresentationBeforeDispatch = BeginHouseShutterAction;
        }

        _cameraShadowCollider = _cameraShadowWitness is null ? null : FindHouseCollisionAncestor(_cameraShadowWitness);
        _cameraWindowCollider = _cameraWindowWitness is null ? null : FindHouseCollisionAncestor(_cameraWindowWitness);
        if (_clothHook is not null)
        {
            _clothInteractionTarget = EnsureHouseTarget("HouseClothHookTarget", ClothInteractionId,
                "urman.fullgame:text/pw-label-house-cloth", _clothHook, 0.36f, 0f, bridge);
            if (_clothInteractionTarget is { } clothTarget)
                clothTarget.PresentationBeforeDispatch = BeginHouseClothAction;
        }

        EnsureHouseTarget("HouseFrameInspectionTarget", FrameInteractionId,
            "urman.fullgame:text/pw-label-house-frame", _windowFrame, 0.40f, 1.1f, bridge);
        EnsureHouseTarget("HouseReturnTarget", ReturnInteractionId,
            "urman.fullgame:text/pw-label-house-return", _houseExitAnchor, 0.55f, 1.1f, bridge);
        BuildHouseSpatialLinks();
    }

    private Node3D? RequireHouseMarker(string path)
    {
        var marker = GetNodeOrNull<Node3D>(path);
        if (marker is not null) return marker;
        if (!_houseMissingMarkerWarningSent)
        {
            GD.PushWarning($"W03 gameplay needs its authored geometry marker: {path}.");
            _houseMissingMarkerWarningSent = true;
        }
        return null;
    }

    private InteractionTarget? EnsureHouseTarget(
        string name, string interactionId, string labelTextId, Node3D? anchor, float radius,
        float verticalOffset, RuntimeBridge? bridge)
    {
        if (anchor is null || !GodotObject.IsInstanceValid(anchor)) return null;
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = interactionId,
            Prompt = bridge?.ResolveText(labelTextId) ?? string.Empty,
            CollisionLayer = 4u,
            CollisionMask = 0u
        };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D
        {
            Name = name + "Ray",
            Shape = new SphereShape3D { Radius = radius }
        });
        _world.AddChild(target);
        target.GlobalPosition = anchor.GlobalPosition + Vector3.Up * verticalOffset;
        target.SetPresentationEnabled(false);
        _houseTargets.Add(target);
        return target;
    }

    private void ProcessHouseGameplay(double delta)
    {
        ProcessHousePhysicalAction(delta);
        ProcessHousePhotographerObservation();
        TryDispatchHouseTwoViews();
    }

    private void ApplyHouseGameplayProjection(RuntimeBridge.FullGamePhotoWorldRouteProjection? route, bool visible)
    {
        if (!visible || route is null || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
        {
            CancelHousePhysicalAction(restoreCommittedPose: true);
            ResetHouseDoorObservationSession(null);
            ResetHousePhotographerObservationSession(null);
            foreach (var target in _houseTargets)
                if (GodotObject.IsInstanceValid(target)) target.SetPresentationEnabled(false);
            return;
        }

        ResetHouseDoorObservationSession(route.SessionIdentity);
        ResetHousePhotographerObservationSession(route.SessionIdentity);
        if (_housePhysicalPhase != HousePhysicalPhase.Idle
            && (!ReferenceEquals(_housePhysicalActionSession, route.SessionIdentity)
                || _housePhysicalActionSceneId != HouseSceneId))
            CancelHousePhysicalAction(restoreCommittedPose: true);

        foreach (var target in _houseTargets)
        {
            if (!GodotObject.IsInstanceValid(target)) continue;
            target.Prompt = bridge.ResolveText(GetHouseLabelForInteraction(target.InteractionId));
            target.SetPresentationEnabled(!ReferenceEquals(target, _housePhysicalActionTarget));
        }

        var actionStillOwnsPose = _housePhysicalPhase != HousePhysicalPhase.Idle
            && ReferenceEquals(_housePhysicalActionSession, route.SessionIdentity)
            && _housePhysicalActionSceneId == HouseSceneId;
        if (actionStillOwnsPose)
        {
            var actionFact = _housePhysicalAction == HousePhysicalAction.Shutter ? ShutterFactId : ClothFactId;
            if (_housePhysicalPhase == HousePhysicalPhase.Committing && bridge.PhotoWorldFactObserved(actionFact))
                ApplyCommittedHousePose();
            return;
        }

        ApplyCommittedHousePose();
    }

    private static string GetHouseLabelForInteraction(string interactionId) => interactionId switch
    {
        ShutterInteractionId => "urman.fullgame:text/pw-label-house-shutter",
        DoorLeftInteractionId => "urman.fullgame:text/pw-label-house-door-left",
        DoorRightInteractionId => "urman.fullgame:text/pw-label-house-door-right",
        CameraPositionInteractionId => "urman.fullgame:text/pw-label-house-camera-position",
        ClothInteractionId => "urman.fullgame:text/pw-label-house-cloth",
        FrameInteractionId => "urman.fullgame:text/pw-label-house-frame",
        ReturnInteractionId => "urman.fullgame:text/pw-label-house-return",
        _ => string.Empty
    };

    private void ExitHouseGameplay()
    {
        CancelHousePhysicalAction(restoreCommittedPose: false);
        ResetHouseDoorObservationSession(null);
        ResetHousePhotographerObservationSession(null);
        ExitHouseSpatialLinks();
        _twoViewsDispatching = false;
        _twoViewsDispatchSession = null;
        _houseTargets.Clear();
        _shutterInteractionTarget = null;
        _clothInteractionTarget = null;
        _shutterPivot = null;
        _shutterTargetAnchor = null;
        _cameraWitness = null;
        _cameraShadowWitness = null;
        _cameraWindowWitness = null;
        _cameraShadowCollider = null;
        _cameraWindowCollider = null;
        _cloth = null;
        _clothHook = null;
        _clothShelf = null;
        _windowFrame = null;
        _houseExitAnchor = null;
    }

    private bool BeginHouseShutterAction() => BeginHousePhysicalAction(
        HousePhysicalAction.Shutter, _shutterInteractionTarget, ShutterInteractionId);

    private bool BeginHouseClothAction() => BeginHousePhysicalAction(
        HousePhysicalAction.Cloth, _clothInteractionTarget, ClothInteractionId);

    private bool BeginHousePhysicalAction(
        HousePhysicalAction action, InteractionTarget? target, string interactionId)
    {
        if (action == HousePhysicalAction.None || target is null
            || _housePhysicalPhase != HousePhysicalPhase.Idle
            || !CanBeginHouseAction(interactionId, out var bridge, out var session)
            || !GodotObject.IsInstanceValid(target))
            return false;

        var actionNode = action == HousePhysicalAction.Shutter ? _shutterPivot : _cloth;
        if (actionNode is null || !GodotObject.IsInstanceValid(actionNode)) return false;

        _housePhysicalActionGeneration++;
        _housePhysicalAction = action;
        _housePhysicalPhase = HousePhysicalPhase.Moving;
        _housePhysicalActionTarget = target;
        _housePhysicalActionSession = session;
        _housePhysicalActionSceneId = HouseSceneId;
        _housePhysicalActionStart = actionNode.GlobalTransform;
        _housePhysicalActionEnd = action == HousePhysicalAction.Shutter
            ? new Transform3D(_shutterOpenTransform.Basis, _shutterOpenTransform.Origin)
            : _clothShelfTransform;
        _housePhysicalActionElapsed = 0;
        _housePhysicalActionDuration = action == HousePhysicalAction.Shutter ? 0.72 : 0.76;
        target.SetPresentationEnabled(false);

        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            ApplyHousePhysicalActionPose(1f);
            var factId = action == HousePhysicalAction.Shutter ? ShutterFactId : ClothFactId;
            CompleteHousePhysicalAction(bridge, session, _housePhysicalActionGeneration, interactionId, factId);
        }

        // This press starts the physical action; its authored fact is committed only
        // after the same object reaches the real shutter/shelf pose.
        return false;
    }

    private bool CanBeginHouseAction(string interactionId, out RuntimeBridge bridge, out object session)
    {
        bridge = null!;
        session = null!;
        if (_bridge is not { } currentBridge || !GodotObject.IsInstanceValid(currentBridge)
            || currentBridge.SessionIdentity is not { } currentSession
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), currentBridge)
            || currentBridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } projection
            || !ReferenceEquals(projection.SessionIdentity, currentSession)
            || !currentBridge.IsInteractionAvailable(interactionId)
            || HouseActionModalOpen())
            return false;

        bridge = currentBridge;
        session = currentSession;
        return true;
    }

    private void ProcessHousePhysicalAction(double delta)
    {
        if (_housePhysicalPhase == HousePhysicalPhase.Idle) return;
        var bridge = _bridge;
        if (!IsHousePhysicalActionCurrent(bridge, _housePhysicalActionSession,
                allowModal: _housePhysicalPhase == HousePhysicalPhase.Committing))
        {
            CancelHousePhysicalAction(restoreCommittedPose: true);
            return;
        }

        if (_housePhysicalPhase != HousePhysicalPhase.Moving) return;
        _housePhysicalActionElapsed = Math.Min(_housePhysicalActionDuration,
            _housePhysicalActionElapsed + Math.Max(0, delta));
        var progress = _housePhysicalActionDuration <= 0
            ? 1f
            : (float)(_housePhysicalActionElapsed / _housePhysicalActionDuration);
        ApplyHousePhysicalActionPose(progress);
        if (progress < 1f) return;

        var interactionId = _housePhysicalAction == HousePhysicalAction.Shutter
            ? ShutterInteractionId : ClothInteractionId;
        var factId = _housePhysicalAction == HousePhysicalAction.Shutter ? ShutterFactId : ClothFactId;
        CompleteHousePhysicalAction(bridge!, _housePhysicalActionSession!, _housePhysicalActionGeneration,
            interactionId, factId);
    }

    private void ApplyHousePhysicalActionPose(float progress)
    {
        var node = _housePhysicalAction == HousePhysicalAction.Shutter ? _shutterPivot : _cloth;
        if (node is null || !GodotObject.IsInstanceValid(node)) return;
        var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(progress, 0f, 1f));
        if (_housePhysicalAction == HousePhysicalAction.Shutter)
        {
            node.GlobalTransform = _housePhysicalActionStart.InterpolateWith(_housePhysicalActionEnd, eased);
            return;
        }

        var lift = Vector3.Up * 0.18f;
        var start = _housePhysicalActionStart;
        var end = _housePhysicalActionEnd;
        Transform3D pose;
        if (eased < 0.22f)
        {
            var phase = Mathf.SmoothStep(0f, 1f, eased / 0.22f);
            pose = new Transform3D(start.Basis, start.Origin + lift * phase);
        }
        else if (eased < 0.78f)
        {
            var phase = Mathf.SmoothStep(0f, 1f, (eased - 0.22f) / 0.56f);
            var liftedStart = new Transform3D(start.Basis, start.Origin + lift);
            var liftedEnd = new Transform3D(end.Basis, end.Origin + lift);
            pose = liftedStart.InterpolateWith(liftedEnd, phase);
        }
        else
        {
            var phase = Mathf.SmoothStep(0f, 1f, (eased - 0.78f) / 0.22f);
            pose = new Transform3D(end.Basis, end.Origin + lift * (1f - phase));
        }
        node.GlobalTransform = eased >= 1f ? end : pose;
    }

    private void CompleteHousePhysicalAction(
        RuntimeBridge bridge, object session, int generation, string interactionId, string factId)
    {
        if (!IsHousePhysicalActionCurrent(bridge, session, allowModal: false)
            || generation != _housePhysicalActionGeneration
            || !bridge.IsInteractionAvailable(interactionId))
        {
            CancelHousePhysicalAction(restoreCommittedPose: true);
            return;
        }

        _housePhysicalPhase = HousePhysicalPhase.Committing;
        CommitHousePhysicalActionAsync(bridge, session, generation, interactionId, factId);
    }

    private async void CommitHousePhysicalActionAsync(
        RuntimeBridge bridge, object session, int generation, string interactionId, string factId)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(interactionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W03 physical action {interactionId} failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _housePhysicalActionGeneration)
            return;

        var currentBridge = _bridge;
        var route = GodotObject.IsInstanceValid(bridge) ? bridge.CurrentFullGamePhotoWorldRouteProjection() : null;
        var stillOwned = GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(bridge.SessionIdentity, session)
            && ReferenceEquals(currentBridge, bridge)
            && route is { ActiveSceneId: HouseSceneId }
            && ReferenceEquals(route.SessionIdentity, session);
        if (!committed || !stillOwned || !bridge.PhotoWorldFactObserved(factId))
        {
            CancelHousePhysicalAction(restoreCommittedPose: true);
            ApplyHouseGameplayProjection(route, route is { ActiveSceneId: HouseSceneId });
            return;
        }

        ApplyCommittedHousePose();
        _housePhysicalPhase = HousePhysicalPhase.Idle;
        _housePhysicalAction = HousePhysicalAction.None;
        _housePhysicalActionTarget?.SetPresentationEnabled(true);
        _housePhysicalActionTarget = null;
        _housePhysicalActionSession = null;
        _housePhysicalActionSceneId = null;
        _housePhysicalActionElapsed = 0;
        _housePhysicalActionDuration = 0;
    }

    private bool IsHousePhysicalActionCurrent(RuntimeBridge? bridge, object? session, bool allowModal)
    {
        if (bridge is null || !GodotObject.IsInstanceValid(bridge) || session is null
            || !ReferenceEquals(bridge.SessionIdentity, session)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session))
            return false;
        if (allowModal) return true;
        if (HouseActionModalOpen()) return false;
        return GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && !player.VehicleControlled && !player.IsClimbingLadder && player.IsOnFloor();
    }

    private bool HouseActionModalOpen() =>
        MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private void CancelHousePhysicalAction(bool restoreCommittedPose)
    {
        _housePhysicalActionGeneration++;
        if (restoreCommittedPose) ApplyCommittedHousePose();
        else if (_housePhysicalAction != HousePhysicalAction.None) RestoreHouseActionStartPose();
        if (_housePhysicalActionTarget is { } target && GodotObject.IsInstanceValid(target))
            target.SetPresentationEnabled(true);
        _housePhysicalAction = HousePhysicalAction.None;
        _housePhysicalPhase = HousePhysicalPhase.Idle;
        _housePhysicalActionTarget = null;
        _housePhysicalActionSession = null;
        _housePhysicalActionSceneId = null;
        _housePhysicalActionElapsed = 0;
        _housePhysicalActionDuration = 0;
    }

    private void RestoreHouseActionStartPose()
    {
        var node = _housePhysicalAction == HousePhysicalAction.Shutter ? _shutterPivot : _cloth;
        if (node is not null && GodotObject.IsInstanceValid(node))
            node.GlobalTransform = _housePhysicalActionStart;
    }

    private void ApplyCommittedHousePose()
    {
        if (_shutterPivot is { } shutter && GodotObject.IsInstanceValid(shutter))
            shutter.GlobalTransform = _bridge?.PhotoWorldFactObserved(ShutterFactId) == true
                ? _shutterOpenTransform : _shutterClosedTransform;
        if (_cloth is { } cloth && GodotObject.IsInstanceValid(cloth))
            cloth.GlobalTransform = _bridge?.PhotoWorldFactObserved(ClothFactId) == true
                ? _clothShelfTransform : _clothHookTransform;
    }

    private void ProcessHousePhotographerObservation()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } route
            || !ReferenceEquals(route.SessionIdentity, bridge.SessionIdentity)
            || bridge.PhotoWorldFactObserved(CameraPositionFactId))
        {
            ResetHousePhotographerObservationSession(null);
            return;
        }

        ResetHousePhotographerObservationSession(route.SessionIdentity);
        if (!bridge.IsInteractionAvailable(CameraPositionInteractionId))
        {
            ClearHousePhotographerSightings();
            return;
        }
        if (_housePhotographerDispatching) return;

        if (_cameraWitness is not { } stance || !GodotObject.IsInstanceValid(stance)
            || _cameraShadowWitness is not { } shadow || !GodotObject.IsInstanceValid(shadow)
            || _cameraWindowWitness is not { } window || !GodotObject.IsInstanceValid(window)
            || !stance.IsVisibleInTree() || !shadow.IsVisibleInTree() || !window.IsVisibleInTree()
            || HouseActionModalOpen()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player)
            || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
        {
            ClearHousePhotographerSightings();
            return;
        }

        if (player.GlobalPosition.DistanceTo(stance.GlobalPosition) > 1.65f)
        {
            ClearHousePhotographerSightings();
            return;
        }

        var camera = player.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is not { Current: true })
        {
            ClearHousePhotographerSightings();
            return;
        }

        if (!_houseViewRayExclusions.Contains(player.GetRid()))
        {
            _houseViewRayExclusions.Clear();
            _houseViewRayExclusions.Add(player.GetRid());
        }

        var space = GetWorld3D().DirectSpaceState;
        if (!_houseSawPhotographerShadow
            && camera.GlobalPosition.DistanceTo(shadow.GlobalPosition) <= HousePhotographerViewMaximumDistance
            && HouseWitnessFramedAndVisible(camera, shadow.GlobalPosition, _cameraShadowCollider, space))
            _houseSawPhotographerShadow = true;
        if (!_houseSawPhotographerWindow
            && camera.GlobalPosition.DistanceTo(window.GlobalPosition) <= HousePhotographerViewMaximumDistance
            && HouseWitnessFramedAndVisible(camera, window.GlobalPosition, _cameraWindowCollider, space))
            _houseSawPhotographerWindow = true;

        if (_houseSawPhotographerShadow && _houseSawPhotographerWindow)
        {
            _housePhotographerDispatching = true;
            var generation = ++_housePhotographerGeneration;
            DispatchHousePhotographerObservationAsync(bridge, route.SessionIdentity, generation);
        }
    }

    private void ResetHousePhotographerObservationSession(object? session)
    {
        if (ReferenceEquals(_housePhotographerObservationSession, session)) return;
        _housePhotographerGeneration++;
        _housePhotographerDispatching = false;
        _housePhotographerObservationSession = session;
        ClearHousePhotographerSightings();
    }

    private void ClearHousePhotographerSightings()
    {
        _houseSawPhotographerShadow = false;
        _houseSawPhotographerWindow = false;
    }

    private async void DispatchHousePhotographerObservationAsync(
        RuntimeBridge bridge, object session, int generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(CameraPositionInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W03 photographer-position observation failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _housePhotographerGeneration)
            return;

        _housePhotographerDispatching = false;
        if (!GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(_bridge, bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || !ReferenceEquals(bridge.SessionIdentity, session))
        {
            ResetHousePhotographerObservationSession(null);
            return;
        }

        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (committed && route is { ActiveSceneId: HouseSceneId }
            && ReferenceEquals(route.SessionIdentity, session)
            && bridge.PhotoWorldFactObserved(CameraPositionFactId))
        {
            ClearHousePhotographerSightings();
            return;
        }

        // A rejected commit is earned again by looking away and returning to
        // both real subjects; the observation itself never becomes saved state.
        ClearHousePhotographerSightings();
    }

    private void TryDispatchHouseTwoViews()
    {
        if (_twoViewsDispatching || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.SessionIdentity is not { } session
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: HouseSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session)
            || !bridge.PhotoWorldFactObserved(DoorLeftFactId)
            || !bridge.PhotoWorldFactObserved(DoorRightFactId)
            || bridge.PhotoWorldFactObserved(TwoViewsFactId)
            || !bridge.IsInteractionAvailable(TwoViewsInteractionId))
            return;

        _twoViewsDispatching = true;
        _twoViewsDispatchSession = session;
        DispatchHouseTwoViewsAsync(bridge, session);
    }

    private async void DispatchHouseTwoViewsAsync(RuntimeBridge bridge, object session)
    {
        try
        {
            await bridge.DispatchInteractionAsync(TwoViewsInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W03 paired doorway observation aggregation failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        if (ReferenceEquals(_twoViewsDispatchSession, session))
        {
            _twoViewsDispatchSession = null;
            _twoViewsDispatching = false;
        }
    }
}
