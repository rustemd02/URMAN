using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    internal const string OrderInteractionId = "urman.fullgame:interaction/pw-cellar-order";
    internal const string BoxInteractionId = "urman.fullgame:interaction/pw-cellar-object";
    private const string ResponseInteractionId = "urman.fullgame:interaction/pw-cellar-response";
    private const string NicheInteractionId = "urman.fullgame:interaction/pw-cellar-niche";
    private const string ReturnInteractionId = "urman.fullgame:interaction/pw-cellar-return";
    private const string OrderFactId = "cellar_household_pattern_seen";
    private const string BoxFactId = "cellar_box_returned";
    private const string ResponseFactId = "cellar_response_seen";
    private const string NicheFactId = "cellar_niche_seen";
    private readonly List<InteractionTarget> _cellarTargets = [];
    private readonly InteractionTarget[] _traceTargets = new InteractionTarget[3];
    private InteractionTarget _responseTarget = null!;
    private InteractionTarget _nicheTarget = null!;
    private InteractionTarget _returnTarget = null!;
    private int _seenTraces;
    private object? _actionSession;
    private int _actionGeneration;
    private bool _committing;

    internal bool TryGetCellarObjectiveTarget(object? identity, string interactionId, FirstPersonController player,
        out Vector3 point, out string label, out bool inspectionPoint)
    {
        point = default;
        label = string.Empty;
        inspectionPoint = false;
        if (!_active || identity is null || !ReferenceEquals(identity, _session)) return false;

        if (interactionId == BoxInteractionId)
        {
            if (!_carryAttached || !_familyBox.IsVisibleInTree()) return false;
            var holding = ReferenceEquals(_carryCoordinator.HeldItem, _familyBox);
            point = holding ? _boxRest.GlobalPosition : _familyBox.GlobalPosition + Vector3.Up * (_familyBox.Size.Y * .5f);
            label = holding ? "Поставьте коробку на подставку" : "Возьмите семейную коробку";
        }
        else
        {
            InteractionTarget? nearestTrace = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var trace in _traceTargets)
            {
                if (!trace.PresentationEnabled || !trace.IsVisibleInTree()) continue;
                var distance = player.GlobalPosition.DistanceSquaredTo(trace.GlobalPosition);
                if (distance >= nearestDistance) continue;
                nearestTrace = trace;
                nearestDistance = distance;
            }
            if (nearestTrace is null) return false;
            point = nearestTrace.GlobalPosition;
            label = nearestTrace.Prompt;
        }

        inspectionPoint = player.GlobalPosition.DistanceSquaredTo(point) <= 3f * 3f;
        return true;
    }

    private void BuildCellarGameplay()
    {
        string[] traceNames = ["Осмотреть сухой круг на столе", "Осмотреть затёртый край стола", "Осмотреть сложенную ткань"];
        for (var index = 0; index < _householdTraces.Length; index++)
        {
            var captured = index;
            _traceTargets[index] = CellarTarget("HouseholdTrace" + index, OrderInteractionId,
                traceNames[index], _householdTraces[index], .16f, Vector3.Up * .065f);
            _traceTargets[index].PresentationBeforeDispatch = () => InspectHouseholdTrace(captured);
        }
        _responseTarget = CellarTarget("InspectBichuraResponse", ResponseInteractionId,
            "Осмотреть переставленную чашку", _responseRest, .19f, Vector3.Up * .10f);
        _responseTarget.PresentationBeforeDispatch = InspectCellarResponse;
        _nicheTarget = CellarTarget("InspectActualNiche", NicheInteractionId,
            "Осмотреть нишу под нижней полкой", _niche, .26f, Vector3.Zero);
        _nicheTarget.PresentationBeforeDispatch = () => CanUseCellarAction(NicheInteractionId, out _, out _);
        var returnWitness = _world.GetNode<Node3D>("B07HomeThreshold/ReturnWitness");
        _returnTarget = CellarTarget("CellarHomeReturn", ReturnInteractionId,
            "Вернуться к семье", returnWitness, .4f, Vector3.Zero);
        _returnTarget.PresentationBeforeDispatch = () => CanUseCellarAction(ReturnInteractionId, out _, out _);
    }

    private InteractionTarget CellarTarget(string name, string interactionId, string prompt,
        Node3D anchor, float radius, Vector3 offset)
    {
        var target = new InteractionTarget { Name = name, InteractionId = interactionId, Prompt = prompt,
            CollisionLayer = 4u, CollisionMask = 0u };
        target.ConfigureRayOnly();
        target.AddChild(new CollisionShape3D { Name = name + "Ray", Shape = new SphereShape3D { Radius = radius } });
        anchor.AddChild(target);
        target.Position = offset;
        target.SetPresentationEnabled(false);
        _cellarTargets.Add(target);
        return target;
    }

    private bool InspectHouseholdTrace(int index)
    {
        if (!CanUseCellarAction(OrderInteractionId, out var bridge, out _) || bridge.PhotoWorldFactObserved(OrderFactId)) return false;
        var observed = _seenTraces | (1 << index);
        // Keep the final ray available until the authored fact is committed.
        // A rejected dispatch must leave the player able to inspect it again.
        if (observed == (1 << _traceTargets.Length) - 1) return true;
        _seenTraces = observed;
        _traceTargets[index].SetPresentationEnabled(false);
        _traceTargets[index].Prompt = index switch
        {
            0 => "Сухой круг: чашку ставили на одно место",
            1 => "Край стола затёрт ладонями",
            _ => "Ткань аккуратно сложена после работы"
        };
        return false;
    }

    private bool CanUseCellarAction(string interactionId, out RuntimeBridge bridge, out object session)
    {
        if (!TryCurrentCellar(out bridge, out session) || !_active
            || bridge.IsPhotoWorldPresentationDeferred
            || bridge.SessionIdentity is not { } activeSession
            || !ReferenceEquals(activeSession, session)
            || CellarModalOpen()
            || !bridge.IsInteractionAvailable(interactionId)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player) || player.VehicleControlled || player.IsClimbingLadder
            || !player.IsOnFloor()) return false;
        return true;
    }

    private bool CellarModalOpen() => MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ModalOpen: true };

    private void ApplyCellarGameplayProjection(bool active, RuntimeBridge? bridge)
    {
        foreach (var target in _cellarTargets) target.SetPresentationEnabled(active);
        if (!active || bridge is null)
        {
            ProjectCellarCarry(false, bridge);
            return;
        }
        var orderKnown = bridge.PhotoWorldFactObserved(OrderFactId);
        for (var index = 0; index < _traceTargets.Length; index++)
            _traceTargets[index].SetPresentationEnabled(!orderKnown && (_seenTraces & (1 << index)) == 0);
        ProjectCellarCarry(active, bridge);
        _responseTarget.SetPresentationEnabled(!bridge.PhotoWorldFactObserved(ResponseFactId) && CellarResponseReady);
        _nicheTarget.SetPresentationEnabled(!bridge.PhotoWorldFactObserved(NicheFactId));
        ApplyCommittedCellarPoses(bridge);
    }

    private void ProcessCellarGameplay(double delta)
    {
        if (!TryCurrentCellar(out var bridge, out var session) || bridge.IsPhotoWorldPresentationDeferred
            || !ReferenceEquals(_session, session))
        {
            if (_actionSession is not null) CancelCellarActions();
            CancelCellarAudio();
            return;
        }
        if (_actionSession is not null && !ReferenceEquals(_actionSession, session)) CancelCellarActions();
        ProcessCellarAudio(delta, bridge, session);
        ProcessCellarBox(bridge, session);
        ProcessCellarResponse(delta, bridge, session);
    }

    private void CancelCellarActions()
    {
        CancelCellarAudio();
        _actionGeneration++;
        _actionSession = null;
        _committing = false;
        _seenTraces = 0;
        _boxPlacementWasEligible = false;
        _boxHeldThisVisit = false;
        CancelCellarResponse();
        if (_familyBox is not null && _responseObject is not null)
            ApplyCommittedCellarPoses(_bridge);
    }
}
