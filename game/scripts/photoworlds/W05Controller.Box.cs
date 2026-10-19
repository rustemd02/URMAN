using System;
using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    private CarryCoordinator _carryCoordinator = null!;
    private bool _carryAttached;
    private bool _carryDuplicateWarning;
    private bool _boxPlacementWasEligible;
    private bool _boxHeldThisVisit;

    private void TryAttachCellarCarry()
    {
        if (_carryAttached || _carryCoordinator is null || !IsInsideTree()) return;
        foreach (var node in GetTree().GetNodesInGroup("carry_coordinator"))
        {
            if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()) continue;
            if (!_carryDuplicateWarning)
            {
                GD.PushWarning("W05 waits for the previous world's canonical CarryCoordinator to retire before registering its family box.");
                _carryDuplicateWarning = true;
            }
            return;
        }
        _world.AddChild(_carryCoordinator);
        _carryAttached = true;
        if (_bridge is not null) ProjectCellarCarry(_active, _bridge);
    }

    private void ProjectCellarCarry(bool active, RuntimeBridge? bridge)
    {
        if (!_carryAttached) return;
        if (bridge is not null && active)
        {
            _carryCoordinator.AttachRuntimeState(bridge);
            _carryCoordinator.SetZonePresentation(string.Empty, exterior: true);
        }
        else _carryCoordinator.SetZonePresentation(string.Empty, exterior: false);
        _familyBox.SetPresentationEnabled(active);
    }

    private bool HasPendingEarnedBoxPlacement => _active && _boxHeldThisVisit
        && !_boxPlacementWasEligible && TryCurrentCellar(out var bridge, out _)
        && bridge.PhotoWorldFactObserved(OrderFactId) && !bridge.PhotoWorldFactObserved(BoxFactId)
        && BoxOnAuthoredRest();

    private bool BoxOnAuthoredRest()
    {
        if (!_carryAttached || _carryCoordinator.ActionInProgress
            || _familyBox.State != CarryableProp.CarryState.Placed || !_familyBox.IsVisibleInTree()) return false;
        var rest = _boxRest.GlobalPosition;
        var feet = _familyBox.GlobalPosition;
        // The small pad is deliberately wider than the box in every yaw. The canonical
        // carry owner has already checked its whole footprint, contact and clear volume.
        var basis = _familyBox.GlobalBasis;
        var extentX = Mathf.Abs(basis.X.X) * _familyBox.Size.X * .5f + Mathf.Abs(basis.Z.X) * _familyBox.Size.Z * .5f;
        var extentZ = Mathf.Abs(basis.X.Z) * _familyBox.Size.X * .5f + Mathf.Abs(basis.Z.Z) * _familyBox.Size.Z * .5f;
        var local = _boxRest.ToLocal(feet);
        return Mathf.Abs(local.X) + extentX <= .5f && Mathf.Abs(local.Z) + extentZ <= .425f
            && Mathf.Abs(feet.Y - rest.Y - .006f) <= .016f;
    }

    private void ProcessCellarBox(RuntimeBridge bridge, object session)
    {
        if (!_carryAttached || _committing || bridge.PhotoWorldFactObserved(BoxFactId)) return;
        if (_familyBox.State == CarryableProp.CarryState.Held) _boxHeldThisVisit = true;
        var eligible = BoxOnAuthoredRest();
        // A fresh canonical placement after ordinary pickup is the only trigger. Rejected
        // commits wait for another deliberate pickup/place, never an every-frame retry.
        if (!eligible) { _boxPlacementWasEligible = false; return; }
        if (_boxPlacementWasEligible || !_boxHeldThisVisit) return;
        if (!CanUseCellarAction(BoxInteractionId, out _, out _)) return;
        _boxPlacementWasEligible = true;
        _actionSession = session;
        _committing = true;
        CommitCellarBoxAsync(bridge, session, ++_actionGeneration);
    }

    private async void CommitCellarBoxAsync(RuntimeBridge bridge, object session, int generation)
    {
        try
        {
            await bridge.DispatchInteractionAsync(BoxInteractionId);
        }
        catch (Exception error)
        {
            GD.PushWarning($"W05 family box authored commit failed: {error.Message}");
        }
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _actionGeneration || !TryCurrentCellar(out var current, out var identity)
            || !ReferenceEquals(current, bridge) || !ReferenceEquals(identity, session)) return;
        _committing = false;
        _actionSession = null;
        // The fact never sets a physical transform: the existing custody snapshot owns it.
        ApplyProjection();
    }
}
