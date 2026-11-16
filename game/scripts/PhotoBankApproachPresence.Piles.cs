using System;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class PhotoBankApproachPresence
{
    private object? _pilesObservationSession;
    private int _pilesObservationGeneration;
    private int _pilesSeen;
    private bool _pilesCommitting;
    private MeshInstance3D? _brokenDeck;
    private MeshInstance3D? _nearPile;
    private CollisionObject3D? _nearPileBody;
    private readonly MeshInstance3D?[] _deckVisuals = new MeshInstance3D?[4];
    private readonly CollisionObject3D?[] _deckBodies = new CollisionObject3D?[4];
    private readonly global::Godot.Collections.Array<Rid> _pilesRayExclusions = new();

    internal bool TryGetPilesObservationObjective(object? identity, FirstPersonController player, string interactionId,
        out Vector3 point, out string label, out bool inspectionTarget)
    {
        point = default;
        label = string.Empty;
        inspectionTarget = false;
        if (_bridge is not { } bridge || identity is null || !ReferenceEquals(identity, bridge.SessionIdentity)
            || !PilesContextCurrent() || _bankEntryWitness is not { } stance || _oldPilesWitness is null)
            return false;
        if (!AtPilesStance(player))
        {
            point = stance.GlobalPosition;
            label = "Берег у старого подхода";
            return true;
        }
        inspectionTarget = true;
        var verifying = interactionId == VerifyBankInteractionId;
        if (verifying || !ReferenceEquals(_pilesObservationSession, identity))
        {
            var target = verifying ? _verifyBankTarget : _winterPilesTarget;
            if (target is null) return false;
            if (player.GetNodeOrNull<Camera3D>("Head/Camera3D") is { } camera
                && player.GetNodeOrNull<RayCast3D>("Head/Camera3D/InteractionRay") is { } ray
                && camera.GlobalPosition.DistanceTo(target.GlobalPosition) > ray.TargetPosition.Length() + .45f)
            {
                // One metre toward the pile still leaves the entire capsule on this bank.
                point = stance.GlobalPosition + _bankApproach!.GlobalBasis.X;
                label = "Берег перед сваями";
                inspectionTarget = false;
                return true;
            }
            point = target.GlobalPosition;
            label = (verifying ? "Сверьте сваи со схемой " : "Начните осмотр свай ")
                + InputBindingService.ActionHint("interact", player.CurrentInputDevice == "gamepad");
            return true;
        }
        return TryCurrentPilesWitness(out point, out label);
    }

    private bool PilesContextCurrent()
    {
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !bridge.IsPhotoWorldsCampaign || bridge.CurrentZoneId != "village_day"
            || bridge.SessionIdentity is not { } session) return false;
        var route = bridge.CurrentFullGamePhotoWorldRouteProjection();
        return route is { ActiveSceneId: BankSceneId } && ReferenceEquals(route.SessionIdentity, session)
            && bridge.ActiveSceneId == BankSceneId;
    }

    private bool AtPilesStance(FirstPersonController player)
    {
        if (_bankApproach is not { } bank || _bankEntryWitness is not { } stance) return false;
        var at = bank.ToLocal(player.GlobalPosition);
        var entry = bank.ToLocal(stance.GlobalPosition);
        var fromChannel = AgentBAct1HeightField.RavineCentre(at.Z) - at.X;
        var entryFromChannel = AgentBAct1HeightField.RavineCentre(entry.Z) - entry.X;
        return Mathf.Abs(at.Z - entry.Z) <= 2.5f
            && fromChannel >= AgentBAct1HeightField.RavineHalfWidth + player.BodyRadius + .45f
            && fromChannel <= entryFromChannel + 2f;
    }

    private bool BeginPilesObservation()
    {
        if (_pilesCommitting || _pilesObservationSession is not null || _bridge is not { } bridge
            || !PilesContextCurrent() || LeafModalOpen() || bridge.IsPhotoWorldPresentationDeferred
            || !IsInteractionReady(bridge, WinterPilesInteractionId)
            || bridge.PhotoWorldFactObserved(WinterPilesFactId)
            || bridge.SessionIdentity is not { } session
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled
            || !player.IsOnFloor() || player.IsClimbingLadder || !AtPilesStance(player)
            || !TryCurrentPilesWitness(out _, out _)) return false;
        _pilesObservationSession = session;
        _pilesSeen = 0;
        _pilesObservationGeneration++;
        return false;
    }

    private bool TryCurrentPilesWitness(out Vector3 point, out string label)
    {
        point = default;
        label = string.Empty;
        if (_bankApproach is not { } bank || _oldPilesWitness is null) return false;
        _nearPile ??= bank.GetNodeOrNull<MeshInstance3D>("OldApproachPile1");
        _nearPileBody ??= bank.GetNodeOrNull<CollisionObject3D>("OldApproachPile1Body");
        for (var index = 0; index < _deckVisuals.Length; index++)
        {
            _deckVisuals[index] ??= bank.GetNodeOrNull<MeshInstance3D>("OldApproachBrokenBoard" + (index + 1));
            _deckBodies[index] ??= bank.GetNodeOrNull<CollisionObject3D>("OldApproachBrokenBoard" + (index + 1) + "Body");
        }
        _brokenDeck = _deckVisuals[3];
        if (_nearPile?.Mesh is null || _nearPileBody is null || _brokenDeck?.Mesh is not { } mesh || _deckBodies[3] is null) return false;
        if ((_pilesSeen & 1) == 0)
        {
            point = _oldPilesWitness.GlobalPosition;
            label = "Посмотрите на ближнюю сваю";
            return true;
        }
        var bounds = mesh.GetAabb();
        var center = bounds.GetCenter();
        // Use the upper end of the actual tilted board, above the ice, not a floating hint.
        var a = _brokenDeck.ToGlobal(new Vector3(center.X, bounds.End.Y, bounds.Position.Z + bounds.Size.Z * .1f));
        var b = _brokenDeck.ToGlobal(new Vector3(center.X, bounds.End.Y, bounds.End.Z - bounds.Size.Z * .1f));
        point = a.Y > b.Y ? a : b;
        label = "Посмотрите на сломанный настил";
        return true;
    }

    private void ProcessPilesObservation()
    {
        if (_pilesObservationSession is null) return;
        if (_bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(_pilesObservationSession, bridge.SessionIdentity)
            || !PilesContextCurrent() || bridge.IsPhotoWorldPresentationDeferred)
        { ResetPilesObservation(); return; }
        if (_pilesCommitting) return;
        if (LeafModalOpen()
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder
            || bridge.PhotoWorldFactObserved(WinterPilesFactId))
        { ResetPilesObservation(); return; }
        if (!player.IsOnFloor() || !AtPilesStance(player)) return;
        if (!TryCurrentPilesWitness(out _, out var cue)) return;
        _winterPilesTarget!.Prompt = cue;
        _winterPilesTarget.PresentationGateHint = cue;
        if (player.GetNodeOrNull<Camera3D>("Head/Camera3D") is not { Current: true } camera) return;
        if (!_pilesRayExclusions.Contains(player.GetRid()))
        {
            _pilesRayExclusions.Clear();
            _pilesRayExclusions.Add(player.GetRid());
        }
        if (!PilesVisibleArea(camera)) return;
        if (_pilesSeen == 0) { _pilesSeen = 1; return; }
        _pilesSeen = 3;
        _pilesCommitting = true;
        CommitPilesObservationAsync(bridge, _pilesObservationSession, _pilesObservationGeneration);
    }

    private bool PilesVisibleArea(Camera3D camera)
    {
        bool VisiblePoint(Vector3 point)
        {
            var direction = point - camera.GlobalPosition;
            // A broad viewing area, not a reticle lock on one thin board's centre.
            if (!camera.IsPositionInFrustum(point) || direction.LengthSquared() > 12f * 12f
                || direction.Normalized().Dot(-camera.GlobalBasis.Z) < Mathf.Cos(Mathf.DegToRad(18f))) return false;
            using var ray = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, point, 3u, _pilesRayExclusions);
            using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count == 0) return true;
            var body = hit["collider"].AsGodotObject();
            if (_pilesSeen == 0) return ReferenceEquals(body, _nearPileBody);
            // A nearer broken board is the same visible subject, not an obstruction.
            foreach (var deckBody in _deckBodies)
                if (ReferenceEquals(body, deckBody)) return true;
            return false;
        }
        if (_pilesSeen == 0)
        {
            var bounds = _nearPile!.Mesh!.GetAabb();
            var center = bounds.GetCenter();
            return VisiblePoint(_oldPilesWitness!.GlobalPosition)
                || VisiblePoint(_nearPile.ToGlobal(center + new Vector3(bounds.Size.X * .2f, bounds.Size.Y * .25f, 0)))
                || VisiblePoint(_nearPile.ToGlobal(center + new Vector3(-bounds.Size.X * .2f, bounds.Size.Y * .25f, 0)));
        }
        foreach (var visual in _deckVisuals)
        {
            if (visual?.Mesh is not { } mesh || !visual.IsVisibleInTree()) continue;
            var bounds = mesh.GetAabb();
            for (var sample = 0; sample < 3; sample++)
                if (VisiblePoint(visual.ToGlobal(new Vector3(bounds.GetCenter().X, bounds.End.Y,
                    bounds.Position.Z + bounds.Size.Z * (.1f + .4f * sample))))) return true;
        }
        return false;
    }

    private async void CommitPilesObservationAsync(RuntimeBridge bridge, object session, int generation)
    {
        var committed = false;
        try { committed = await bridge.DispatchInteractionAsync(WinterPilesInteractionId); }
        catch (Exception error) { GD.PushWarning("Winter bank observation failed: " + error.Message); }
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
            || generation != _pilesObservationGeneration) return;
        var current = GodotObject.IsInstanceValid(bridge) && ReferenceEquals(session, bridge.SessionIdentity)
            && PilesContextCurrent() && !LeafModalOpen() && !bridge.IsPhotoWorldPresentationDeferred;
        ResetPilesObservation();
        if (committed && current && bridge.PhotoWorldFactObserved(WinterPilesFactId)
            && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            var hasCopy = false;
            foreach (var photo in bridge.PhotoBookPhotos())
                if (photo.PhotoId == "S-BANK") hasCopy = true;
            player.ShowRemark("Айдар", hasCopy
                ? "Один ряд свай. И косые обломки настила. Сверю со схемой из сельсовета."
                : "Один ряд свай. И косые обломки настила. Надо поискать старую схему этого места.");
        }
    }

    private void ResetPilesObservation()
    {
        if (_pilesObservationSession is null && !_pilesCommitting) return;
        _pilesObservationGeneration++;
        _pilesObservationSession = null;
        _pilesSeen = 0;
        _pilesCommitting = false;
        _pilesRayExclusions.Clear();
    }

    private void OnPilesPlayTimeBoundary(string boundary)
    {
        if (boundary == "load-start") ResetPilesObservation();
    }
}
