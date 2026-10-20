using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W04Controller
{
    private const string RelationsInteractionId = "urman.fullgame:interaction/pw-common-three-places";
    private const string ClothInteractionId = "urman.fullgame:interaction/pw-common-cloth";
    private const string HorizonInteractionId = "urman.fullgame:interaction/pw-common-horizon";
    private const string NoteInteractionId = "urman.fullgame:interaction/pw-common-note";
    private const string ReturnInteractionId = "urman.fullgame:interaction/pw-common-return";

    private const string RelationsFactId = "common_three_relations_seen";
    private const string ClothFactId = "common_view_uncovered";
    private const string HorizonFactId = "common_horizon_seen";
    private const string NoteFactId = "common_note_finished";

    private const float ObservationStanceRadius = 3.2f;
    private const float CommonObservationLift = 0.18f;

    private enum ClothPhase { Idle, Moving, Committing }

    private readonly List<InteractionTarget> _commonTargets = [];
    private readonly global::Godot.Collections.Array<Rid> _commonViewRayExclusions = new();
    private Node3D? _relationsAnchor;
    private Node3D? _horizonAnchor;
    private Node3D? _clothPanel;
    private Node3D? _clothFoldSupport;
    private Node3D? _relationsForestWitness;
    private Node3D? _relationsRiverWitness;
    private Node3D? _relationsHouseWitness;
    private Node3D? _commonHorizonWitness;
    private CollisionObject3D? _relationsForestCollider;
    private CollisionObject3D? _relationsRiverCollider;
    private CollisionObject3D? _relationsHouseCollider;
    private CollisionObject3D? _commonHorizonCollider;
    private InteractionTarget? _relationsTarget;
    private InteractionTarget? _horizonTarget;
    private InteractionTarget? _clothTarget;
    private InteractionTarget? _noteTarget;
    private InteractionTarget? _returnTarget;
    private Transform3D _clothHomeTransform;
    private Transform3D _clothFoldTransform;
    private Transform3D _clothActionStart;
    private bool _hasClothHomeTransform;
    private bool _hasClothFoldTransform;
    private ClothPhase _clothPhase;
    private InteractionTarget? _clothActionTarget;
    private object? _clothActionSession;
    private int _clothActionGeneration;
    private double _clothActionElapsed;
    private const double ClothActionDuration = 0.84;
    private object? _relationsSession;
    private int _relationsSeenMask;
    private int _relationsGeneration;
    private bool _relationsDispatching;
    private bool _relationsArmed;
    private object? _horizonSession;
    private int _horizonGeneration;
    private bool _horizonDispatching;
    private bool _horizonArmed;
    private bool _commonMissingMarkerWarningSent;

    public bool PhotoBookReturnActionInProgress => _clothPhase != ClothPhase.Idle
        || _relationsDispatching
        || _horizonDispatching
        || CommonNoteOpen;

    private void BuildCommonGameplay()
    {
        _relationsAnchor = RequireCommonMarker("Anchors/C03");
        _horizonAnchor = RequireCommonMarker("Anchors/C05");
        _clothPanel = RequireCommonMarker("W04World/C04ClothPanel");
        _clothFoldSupport = RequireCommonMarker("W04World/C04ClothFoldSupport");
        _relationsForestWitness = RequireCommonMarker("W04World/C01FrameWitness");
        _relationsRiverWitness = RequireCommonMarker("W04World/C02FrameWitness");
        _relationsHouseWitness = RequireCommonMarker("W04World/C03FrameWitness");
        _commonHorizonWitness = RequireCommonMarker("W04World/CommonHorizonWitness");

        if (_clothPanel is { } cloth && GodotObject.IsInstanceValid(cloth))
        {
            _clothHomeTransform = cloth.GlobalTransform;
            _hasClothHomeTransform = true;
        }
        if (_clothFoldSupport is { } support && GodotObject.IsInstanceValid(support))
        {
            _clothFoldTransform = support.GlobalTransform;
            _hasClothFoldTransform = true;
        }

        _relationsForestCollider = _relationsForestWitness is null ? null : FindCommonCollisionAncestor(_relationsForestWitness);
        _relationsRiverCollider = _relationsRiverWitness is null ? null : FindCommonCollisionAncestor(_relationsRiverWitness);
        _relationsHouseCollider = _relationsHouseWitness is null ? null
            : FindCommonCollisionAncestor(_relationsHouseWitness)
                ?? _world.GetNodeOrNull<CollisionObject3D>("CommonHouseBody");
        _commonHorizonCollider = _commonHorizonWitness is null ? null : FindCommonCollisionAncestor(_commonHorizonWitness);

        var bridge = _bridge is { } current && GodotObject.IsInstanceValid(current) ? current : null;
        _relationsTarget = EnsureCommonTarget("CommonRelationsTarget", RelationsInteractionId,
            "urman.fullgame:text/pw-label-common-three-places", "Anchors/C03", 0.42f, 1.0f, bridge);
        if (_relationsTarget is { } relationsTarget)
            relationsTarget.PresentationBeforeDispatch = BeginCommonRelationsObservation;

        _clothTarget = EnsureCommonTarget("CommonClothTarget", ClothInteractionId,
            "urman.fullgame:text/pw-label-common-cloth",
            "W04World/C04ClothPanel/ClothInteractionWitness", 0.42f, 0f, bridge);
        if (_clothTarget is { } clothTarget) clothTarget.PresentationBeforeDispatch = BeginCommonClothAction;

        _horizonTarget = EnsureCommonTarget("CommonHorizonTarget", HorizonInteractionId,
            "urman.fullgame:text/pw-label-common-horizon", "Anchors/C05", 0.42f, 1.0f, bridge);
        if (_horizonTarget is { } horizonTarget)
            horizonTarget.PresentationBeforeDispatch = BeginCommonHorizonObservation;

        _noteTarget = EnsureCommonTarget("CommonNoteTarget", NoteInteractionId,
            "urman.fullgame:text/pw-label-common-note",
            "W04World/C07CaptionDesk/CaptionPaperInteractionWitness", 0.28f, 0.30f, bridge);
        if (_noteTarget is { } noteTarget) noteTarget.PresentationBeforeDispatch = BeginCommonNote;

        _returnTarget = EnsureCommonTarget("CommonReturnTarget", ReturnInteractionId,
            "urman.fullgame:text/pw-label-common-return", "Anchors/C08", 0.56f, 1.0f, bridge);
        if (_returnTarget is { } returnTarget) returnTarget.PresentationBeforeDispatch = CanUseCommonReturn;
    }

    private Node3D? RequireCommonMarker(string path)
    {
        var marker = GetNodeOrNull<Node3D>(path);
        if (marker is not null) return marker;
        if (!_commonMissingMarkerWarningSent)
        {
            GD.PushWarning($"W04 gameplay needs its authored geometry marker: {path}.");
            _commonMissingMarkerWarningSent = true;
        }
        return null;
    }

    private InteractionTarget? EnsureCommonTarget(
        string name, string interactionId, string labelTextId, string anchorPath,
        float radius, float verticalOffset, RuntimeBridge? bridge)
    {
        var anchor = GetNodeOrNull<Node3D>(anchorPath);
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
        _commonTargets.Add(target);
        return target;
    }

    private void ProcessCommonGameplay(double delta)
    {
        ProcessCommonNote();
        ProcessCommonClothAction(delta);
        ProcessCommonRelationsObservation();
        ProcessCommonHorizonObservation();
    }

    private void ApplyCommonGameplayProjection(
        RuntimeBridge.FullGamePhotoWorldRouteProjection? route, bool visible)
    {
        ProjectCommonNote(visible);
        if (!visible || route is null || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge))
        {
            CancelCommonClothAction(restoreCommittedPose: true);
            ResetRelationsObservation(null);
            ResetHorizonObservation(null);
            foreach (var target in _commonTargets)
                if (GodotObject.IsInstanceValid(target)) target.SetPresentationEnabled(false);
            return;
        }

        ResetRelationsObservation(route.SessionIdentity);
        ResetHorizonObservation(route.SessionIdentity);
        if (_clothPhase != ClothPhase.Idle && !ReferenceEquals(_clothActionSession, route.SessionIdentity))
            CancelCommonClothAction(restoreCommittedPose: true);

        foreach (var target in _commonTargets)
        {
            if (!GodotObject.IsInstanceValid(target)) continue;
            target.Prompt = bridge.ResolveText(GetCommonLabelForInteraction(target.InteractionId));
            var alreadyCommitted = target.InteractionId switch
            {
                RelationsInteractionId => bridge.PhotoWorldFactObserved(RelationsFactId),
                ClothInteractionId => bridge.PhotoWorldFactObserved(ClothFactId),
                HorizonInteractionId => bridge.PhotoWorldFactObserved(HorizonFactId),
                // C07 stays readable after its one authored confirmation.
                NoteInteractionId => false,
                _ => false
            };
            var locallyBusy = ReferenceEquals(target, _clothActionTarget)
                || ReferenceEquals(target, _relationsTarget) && _relationsArmed
                || ReferenceEquals(target, _horizonTarget) && _horizonArmed;
            target.SetPresentationEnabled(!locallyBusy
                && !alreadyCommitted);
        }

        if (_clothPhase != ClothPhase.Idle && ReferenceEquals(_clothActionSession, route.SessionIdentity))
        {
            if (_clothPhase == ClothPhase.Committing && bridge.PhotoWorldFactObserved(ClothFactId))
                ApplyCommittedClothPose();
            return;
        }
        ApplyCommittedClothPose();
    }

    private static string GetCommonLabelForInteraction(string interactionId) => interactionId switch
    {
        ClothInteractionId => "urman.fullgame:text/pw-label-common-cloth",
        RelationsInteractionId => "urman.fullgame:text/pw-label-common-three-places",
        HorizonInteractionId => "urman.fullgame:text/pw-label-common-horizon",
        NoteInteractionId => "urman.fullgame:text/pw-label-common-note",
        ReturnInteractionId => "urman.fullgame:text/pw-label-common-return",
        _ => string.Empty
    };

    private void ExitCommonGameplay()
    {
        ExitCommonNote();
        CancelCommonClothAction(restoreCommittedPose: false);
        ResetRelationsObservation(null);
        ResetHorizonObservation(null);
        _commonTargets.Clear();
        _relationsTarget = null;
        _horizonTarget = null;
        _clothTarget = null;
        _noteTarget = null;
        _returnTarget = null;
        _relationsAnchor = null;
        _horizonAnchor = null;
        _clothPanel = null;
        _clothFoldSupport = null;
        _relationsForestWitness = null;
        _relationsRiverWitness = null;
        _relationsHouseWitness = null;
        _commonHorizonWitness = null;
        _relationsForestCollider = null;
        _relationsRiverCollider = null;
        _relationsHouseCollider = null;
        _commonHorizonCollider = null;
        _commonViewRayExclusions.Clear();
    }

    private bool BeginCommonClothAction()
    {
        if (_clothPhase != ClothPhase.Idle || !_hasClothHomeTransform || !_hasClothFoldTransform
            || _clothPanel is not { } cloth || !GodotObject.IsInstanceValid(cloth)
            || _clothTarget is not { } target || !GodotObject.IsInstanceValid(target)
            || !CanBeginCommonAction(ClothInteractionId, out var bridge, out var session)
            || bridge.PhotoWorldFactObserved(ClothFactId))
            return false;

        _clothActionGeneration++;
        _clothPhase = ClothPhase.Moving;
        _clothActionTarget = target;
        _clothActionSession = session;
        _clothActionStart = cloth.GlobalTransform;
        _clothActionElapsed = 0;
        target.SetPresentationEnabled(false);
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            ApplyCommonClothPose(1f);
            CompleteCommonClothAction(bridge, session, _clothActionGeneration);
        }
        return false;
    }

    private bool BeginCommonRelationsObservation() => BeginCommonObservation(
        RelationsInteractionId, _relationsAnchor, _relationsTarget);

    private bool BeginCommonHorizonObservation() => BeginCommonObservation(
        HorizonInteractionId, _horizonAnchor, _horizonTarget);

    private bool BeginCommonObservation(string interactionId, Node3D? stance, InteractionTarget? target)
    {
        if (interactionId == RelationsInteractionId) _relationsArmed = false;
        else _horizonArmed = false;
        if (target is null || !GodotObject.IsInstanceValid(target)
            || !CanBeginCommonAction(interactionId, out var bridge, out var session)
            || stance is not { } anchor || !GodotObject.IsInstanceValid(anchor)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.GlobalPosition.DistanceTo(anchor.GlobalPosition) > ObservationStanceRadius
            || bridge.PhotoWorldFactObserved(interactionId == RelationsInteractionId ? RelationsFactId : HorizonFactId))
            return false;

        if (interactionId == RelationsInteractionId)
        {
            ResetRelationsObservation(session);
            _relationsArmed = true;
        }
        else
        {
            ResetHorizonObservation(session);
            _horizonArmed = true;
        }
        target.SetPresentationEnabled(false);
        return false;
    }

    private void ProcessCommonClothAction(double delta)
    {
        if (_clothPhase == ClothPhase.Idle) return;
        var bridge = _bridge;
        if (!IsCommonClothActionCurrent(bridge, _clothActionSession,
                allowModal: _clothPhase == ClothPhase.Committing))
        {
            CancelCommonClothAction(restoreCommittedPose: true);
            return;
        }
        if (_clothPhase != ClothPhase.Moving) return;

        _clothActionElapsed = Math.Min(ClothActionDuration, _clothActionElapsed + Math.Max(0, delta));
        var progress = (float)(_clothActionElapsed / ClothActionDuration);
        ApplyCommonClothPose(progress);
        if (progress >= 1f)
            CompleteCommonClothAction(bridge!, _clothActionSession!, _clothActionGeneration);
    }

    private void ApplyCommonClothPose(float progress)
    {
        if (_clothPanel is not { } cloth || !GodotObject.IsInstanceValid(cloth)) return;
        var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(progress, 0f, 1f));
        var start = _clothActionStart;
        var end = _clothFoldTransform;
        var release = start.Basis.Z * .30f;
        Transform3D pose;
        if (eased < 0.22f)
        {
            var phase = eased / 0.22f;
            var released = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(phase * 2f, 0f, 1f));
            var lifted = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(phase * 2f - 1f, 0f, 1f));
            pose = new Transform3D(start.Basis,
                start.Origin + release * released + Vector3.Up * CommonObservationLift * lifted);
        }
        else if (eased < 0.78f)
        {
            var phase = Mathf.SmoothStep(0f, 1f, (eased - 0.22f) / 0.56f);
            var liftedStart = new Transform3D(start.Basis,
                start.Origin + release + Vector3.Up * CommonObservationLift);
            var liftedEnd = new Transform3D(end.Basis, end.Origin + Vector3.Up * CommonObservationLift);
            pose = liftedStart.InterpolateWith(liftedEnd, phase);
            pose.Origin += Vector3.Up * (.08f * Mathf.Sin(Mathf.Pi * phase));
        }
        else
        {
            var phase = Mathf.SmoothStep(0f, 1f, (eased - 0.78f) / 0.22f);
            pose = new Transform3D(end.Basis, end.Origin + Vector3.Up * CommonObservationLift * (1f - phase));
        }
        cloth.GlobalTransform = eased >= 1f ? end : pose;
    }

    private void CompleteCommonClothAction(RuntimeBridge bridge, object session, int generation)
    {
        if (!IsCommonClothActionCurrent(bridge, session, allowModal: false)
            || generation != _clothActionGeneration
            || !bridge.IsInteractionAvailable(ClothInteractionId))
        {
            CancelCommonClothAction(restoreCommittedPose: true);
            return;
        }
        _clothPhase = ClothPhase.Committing;
        CommitCommonClothActionAsync(bridge, session, generation);
    }

    private async void CommitCommonClothActionAsync(RuntimeBridge bridge, object session, int generation)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(ClothInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W04 cloth action failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _clothActionGeneration) return;
        var route = GodotObject.IsInstanceValid(bridge) ? bridge.CurrentFullGamePhotoWorldRouteProjection() : null;
        var stillOwned = GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(_bridge, bridge)
            && ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            && ReferenceEquals(bridge.SessionIdentity, session)
            && route is { ActiveSceneId: CommonSceneId }
            && ReferenceEquals(route.SessionIdentity, session);
        if (!committed || !stillOwned || !bridge.PhotoWorldFactObserved(ClothFactId))
        {
            CancelCommonClothAction(restoreCommittedPose: true);
            ApplyCommonGameplayProjection(route, route is { ActiveSceneId: CommonSceneId });
            return;
        }

        ApplyCommittedClothPose();
        _clothPhase = ClothPhase.Idle;
        _clothActionTarget?.SetPresentationEnabled(false);
        _clothActionTarget = null;
        _clothActionSession = null;
        _clothActionElapsed = 0;
    }

    private bool CanBeginCommonAction(string interactionId, out RuntimeBridge bridge, out object session)
    {
        bridge = null!;
        session = null!;
        if (_bridge is not { } currentBridge || !GodotObject.IsInstanceValid(currentBridge)
            || currentBridge.SessionIdentity is not { } currentSession
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), currentBridge)
            || currentBridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: CommonSceneId } route
            || !ReferenceEquals(route.SessionIdentity, currentSession)
            || !currentBridge.IsInteractionAvailable(interactionId)
            || CommonActionModalOpen()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.VehicleControlled || player.IsClimbingLadder || !player.IsOnFloor())
            return false;
        bridge = currentBridge;
        session = currentSession;
        return true;
    }

    private bool IsCommonClothActionCurrent(RuntimeBridge? bridge, object? session, bool allowModal)
    {
        if (bridge is null || !GodotObject.IsInstanceValid(bridge) || session is null
            || !ReferenceEquals(bridge.SessionIdentity, session)
            || !ReferenceEquals(_bridge, bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: CommonSceneId } route
            || !ReferenceEquals(route.SessionIdentity, session))
            return false;
        if (allowModal) return true;
        return !CommonActionModalOpen()
            && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            && !player.VehicleControlled && !player.IsClimbingLadder && player.IsOnFloor();
    }

    private bool CanUseCommonReturn() => CanBeginCommonAction(ReturnInteractionId, out _, out _);

    private bool CommonActionModalOpen() =>
        MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private void CancelCommonClothAction(bool restoreCommittedPose)
    {
        var wasActive = _clothPhase != ClothPhase.Idle;
        _clothActionGeneration++;
        if (restoreCommittedPose) ApplyCommittedClothPose();
        else if (wasActive && _clothPanel is { } cloth && GodotObject.IsInstanceValid(cloth))
            cloth.GlobalTransform = _clothActionStart;
        if (_clothActionTarget is { } target && GodotObject.IsInstanceValid(target))
        {
            var bridge = _bridge;
            target.SetPresentationEnabled(HasCurrentCommonRoute()
                && bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
                && currentBridge.IsInteractionAvailable(ClothInteractionId)
                && !currentBridge.PhotoWorldFactObserved(ClothFactId));
        }
        _clothPhase = ClothPhase.Idle;
        _clothActionTarget = null;
        _clothActionSession = null;
        _clothActionElapsed = 0;
    }

    private void ApplyCommittedClothPose()
    {
        if (!_hasClothHomeTransform || !_hasClothFoldTransform
            || _clothPanel is not { } cloth || !GodotObject.IsInstanceValid(cloth)) return;
        cloth.GlobalTransform = _bridge?.PhotoWorldFactObserved(ClothFactId) == true
            ? _clothFoldTransform : _clothHomeTransform;
    }

    private void ProcessCommonRelationsObservation()
    {
        if (!TryGetCommonObservationContext(RelationsInteractionId, out var bridge, out var session,
                out var player, out var camera, out var stance, RelationsFactId))
        {
            if (!HasCurrentCommonRoute()) ResetRelationsObservation(null);
            return;
        }
        ResetRelationsObservation(session);
        if (bridge.PhotoWorldFactObserved(RelationsFactId))
        {
            _relationsSeenMask = 0;
            return;
        }
        if (_relationsDispatching) return;
        if (player.GlobalPosition.DistanceTo(stance.GlobalPosition) > ObservationStanceRadius)
        {
            _relationsSeenMask = 0;
            _relationsArmed = false;
            UpdateCommonObservationTarget(_relationsTarget, RelationsInteractionId, RelationsFactId, false);
            return;
        }

        if (!_relationsArmed) return;
        EnsureCommonViewRayExclusion(player);
        var space = GetWorld3D().DirectSpaceState;
        MarkCommonWitness(camera, _relationsForestWitness, _relationsForestCollider, 1, space, ref _relationsSeenMask);
        MarkCommonWitness(camera, _relationsRiverWitness, _relationsRiverCollider, 2, space, ref _relationsSeenMask);
        MarkCommonWitness(camera, _relationsHouseWitness, _relationsHouseCollider, 4, space, ref _relationsSeenMask);
        if (_relationsSeenMask != 7) return;
        if (!bridge.IsInteractionAvailable(RelationsInteractionId))
        {
            _relationsSeenMask = 0;
            return;
        }
        _relationsDispatching = true;
        DispatchCommonRelationsAsync(bridge, session, ++_relationsGeneration);
    }

    private void MarkCommonWitness(
        Camera3D camera, Node3D? witness, CollisionObject3D? collider, int bit,
        PhysicsDirectSpaceState3D space, ref int seenMask)
    {
        if ((seenMask & bit) != 0 || witness is not { } point || !GodotObject.IsInstanceValid(point)
            || !point.IsVisibleInTree()) return;
        if (CommonWitnessFramedAndVisible(camera, point.GlobalPosition, collider, space))
            seenMask |= bit;
    }

    private void ProcessCommonHorizonObservation()
    {
        if (!TryGetCommonObservationContext(HorizonInteractionId, out var bridge, out var session,
                out var player, out var camera, out var stance, HorizonFactId))
        {
            if (!HasCurrentCommonRoute()) ResetHorizonObservation(null);
            return;
        }
        ResetHorizonObservation(session);
        if (_horizonDispatching || bridge.PhotoWorldFactObserved(HorizonFactId)) return;
        if (player.GlobalPosition.DistanceTo(stance.GlobalPosition) > ObservationStanceRadius)
        {
            _horizonArmed = false;
            UpdateCommonObservationTarget(_horizonTarget, HorizonInteractionId, HorizonFactId, false);
            return;
        }
        if (!_horizonArmed || _commonHorizonWitness is not { } witness
            || !GodotObject.IsInstanceValid(witness) || !witness.IsVisibleInTree()
            || !HasCommonHorizontalSector(camera, 35f)) return;

        EnsureCommonViewRayExclusion(player);
        if (!CommonWitnessFramedAndVisible(camera, witness.GlobalPosition, _commonHorizonCollider,
                GetWorld3D().DirectSpaceState)
            || !bridge.IsInteractionAvailable(HorizonInteractionId)) return;

        _horizonDispatching = true;
        DispatchCommonHorizonAsync(bridge, session, ++_horizonGeneration);
    }

    private void UpdateCommonObservationTarget(
        InteractionTarget? target, string interactionId, string factId, bool armed)
    {
        if (target is not { } observationTarget || !GodotObject.IsInstanceValid(observationTarget)) return;
        var bridge = _bridge;
        observationTarget.SetPresentationEnabled(!armed && HasCurrentCommonRoute()
            && bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
            && currentBridge.IsInteractionAvailable(interactionId)
            && !currentBridge.PhotoWorldFactObserved(factId));
    }

    private bool TryGetCommonObservationContext(
        string interactionId, out RuntimeBridge bridge, out object session,
        out FirstPersonController player, out Camera3D camera, out Node3D stance, string factId)
    {
        bridge = null!;
        session = null!;
        player = null!;
        camera = null!;
        stance = null!;
        if (_bridge is not { } currentBridge || !GodotObject.IsInstanceValid(currentBridge)
            || currentBridge.SessionIdentity is not { } currentSession
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), currentBridge)
            || currentBridge.CurrentFullGamePhotoWorldRouteProjection() is not { ActiveSceneId: CommonSceneId } route
            || !ReferenceEquals(route.SessionIdentity, currentSession)
            || currentBridge.PhotoWorldFactObserved(factId)
            || !currentBridge.IsInteractionAvailable(interactionId)
            || CommonActionModalOpen()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController currentPlayer
            || !GodotObject.IsInstanceValid(currentPlayer)
            || currentPlayer.VehicleControlled || currentPlayer.IsClimbingLadder || !currentPlayer.IsOnFloor()
            || currentPlayer.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } currentCamera)
            return false;

        var requiredStance = interactionId == RelationsInteractionId ? _relationsAnchor : _horizonAnchor;
        if (requiredStance is not { } currentStance || !GodotObject.IsInstanceValid(currentStance)
            || !currentStance.IsVisibleInTree()) return false;
        bridge = currentBridge;
        session = currentSession;
        player = currentPlayer;
        camera = currentCamera;
        stance = currentStance;
        return true;
    }

    private bool HasCurrentCommonRoute() => _bridge is { } bridge
        && GodotObject.IsInstanceValid(bridge)
        && bridge.CurrentFullGamePhotoWorldRouteProjection() is { ActiveSceneId: CommonSceneId } route
        && ReferenceEquals(route.SessionIdentity, bridge.SessionIdentity);

    private void EnsureCommonViewRayExclusion(FirstPersonController player)
    {
        if (_commonViewRayExclusions.Count == 1 && _commonViewRayExclusions[0] == player.GetRid()) return;
        _commonViewRayExclusions.Clear();
        _commonViewRayExclusions.Add(player.GetRid());
    }

    private bool CommonWitnessFramedAndVisible(
        Camera3D camera, Vector3 point, CollisionObject3D? expectedCollider, PhysicsDirectSpaceState3D space)
    {
        if (!camera.IsPositionInFrustum(point)) return false;
        using var ray = PhysicsRayQueryParameters3D.Create(
            camera.GlobalPosition, point, 3u, _commonViewRayExclusions);
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0) return true;
        return expectedCollider is not null
            && hit["collider"].AsGodotObject() is CollisionObject3D actual
            && ReferenceEquals(actual, expectedCollider);
    }

    private static bool HasCommonHorizontalSector(Camera3D camera, float minimumDegrees)
    {
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var aspect = viewport.X / Math.Max(viewport.Y, 1f);
        var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        var horizontalHalfAngle = camera.KeepAspect == Camera3D.KeepAspectEnum.Width
            ? Mathf.Atan(tangent)
            : Mathf.Atan(tangent * aspect);
        return Mathf.RadToDeg(horizontalHalfAngle * 2f) >= minimumDegrees;
    }

    private static CollisionObject3D? FindCommonCollisionAncestor(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (current is CollisionObject3D collision) return collision;
        return null;
    }

    private void ResetRelationsObservation(object? session)
    {
        if (ReferenceEquals(_relationsSession, session)) return;
        _relationsGeneration++;
        _relationsSession = session;
        _relationsSeenMask = 0;
        _relationsDispatching = false;
        _relationsArmed = false;
    }

    private void ResetHorizonObservation(object? session)
    {
        if (ReferenceEquals(_horizonSession, session)) return;
        _horizonGeneration++;
        _horizonSession = session;
        _horizonDispatching = false;
        _horizonArmed = false;
    }

    private async void DispatchCommonRelationsAsync(RuntimeBridge bridge, object session, int generation)
    {
        await DispatchCommonObservationAsync(bridge, session, generation,
            RelationsInteractionId, RelationsFactId, isRelations: true);
    }

    private async void DispatchCommonHorizonAsync(RuntimeBridge bridge, object session, int generation)
    {
        await DispatchCommonObservationAsync(bridge, session, generation,
            HorizonInteractionId, HorizonFactId, isRelations: false);
    }

    private async System.Threading.Tasks.Task DispatchCommonObservationAsync(
        RuntimeBridge bridge, object session, int generation, string interactionId, string factId, bool isRelations)
    {
        var committed = false;
        try
        {
            committed = await bridge.DispatchInteractionAsync(interactionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W04 observation {interactionId} failed: {error.Message}");
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()) return;
        var currentGeneration = isRelations ? _relationsGeneration : _horizonGeneration;
        if (generation != currentGeneration) return;
        var route = GodotObject.IsInstanceValid(bridge) ? bridge.CurrentFullGamePhotoWorldRouteProjection() : null;
        var stillOwned = GodotObject.IsInstanceValid(bridge)
            && ReferenceEquals(_bridge, bridge)
            && ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            && ReferenceEquals(bridge.SessionIdentity, session)
            && route is { ActiveSceneId: CommonSceneId }
            && ReferenceEquals(route.SessionIdentity, session);
        var factObserved = stillOwned && bridge.PhotoWorldFactObserved(factId);
        if (!isRelations) _horizonDispatching = false;
        else _relationsDispatching = false;
        if (!committed || !factObserved)
        {
            if (isRelations)
            {
                _relationsSeenMask = 0;
                _relationsArmed = false;
                UpdateCommonObservationTarget(_relationsTarget, RelationsInteractionId, RelationsFactId, false);
            }
            else
            {
                _horizonArmed = false;
                UpdateCommonObservationTarget(_horizonTarget, HorizonInteractionId, HorizonFactId, false);
            }
            return;
        }
        if (isRelations)
        {
            _relationsSeenMask = 0;
            _relationsArmed = false;
            UpdateCommonObservationTarget(_relationsTarget, RelationsInteractionId, RelationsFactId, false);
        }
        else
        {
            _horizonArmed = false;
            UpdateCommonObservationTarget(_horizonTarget, HorizonInteractionId, HorizonFactId, false);
        }
    }
}
