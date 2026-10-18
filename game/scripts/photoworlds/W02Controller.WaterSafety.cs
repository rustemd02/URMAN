using System;
using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class W02Controller
{
    private const float WaterSafetyRecoveryDepth = 0.5f;
    private const float WaterSafetyDryClearance = 0.1f;
    private const float WaterSafetyTerrainEdgeInset = 0.5f;
    private const float WaterSafetyRayAboveFeet = 0.15f;
    private const float WaterSafetyRayBelowFeet = 0.28f;

    private readonly HashSet<StaticBody3D> _waterSafetyDecks = [];
    private readonly global::Godot.Collections.Array<Rid> _waterSafetyRayExclusions = new();
    private Func<Vector3, (Vector3 Position, float Ground)>? _waterSafetyGuard;
    private object? _waterSafetySessionIdentity;
    private FirstPersonController? _waterSafetyPlayer;
    private Vector3 _waterSafetyLastSafePosition;
    private float _waterSafetyLastSafeYaw;
    private bool _waterSafetyHasLastSafePosition;
    private bool _waterSafetyActive;
    private bool _waterSafetyRestorePending;

    private void RegisterWaterSafetyDeck(StaticBody3D body)
    {
        if (GodotObject.IsInstanceValid(body))
            _waterSafetyDecks.Add(body);
    }

    private void ApplyWaterSafetyProjection(RuntimeBridge.W02WaterProjection? projection, bool visible)
    {
        var bridge = _bridge;
        var session = bridge is { } currentBridge && GodotObject.IsInstanceValid(currentBridge)
            ? currentBridge.SessionIdentity
            : null;

        if (session is not null && !ReferenceEquals(_waterSafetySessionIdentity, session))
            ResetWaterSafetySession(session);

        _waterSafetyActive = visible
            && session is not null
            && projection is { ActiveSceneId: RiverSceneId }
            && ReferenceEquals(projection.SessionIdentity, session)
            && bridge is { } liveBridge
            && !liveBridge.IsPhotoWorldSaveRestoreInProgress;

        if (!_waterSafetyActive)
        {
            if (projection is null && bridge?.ActiveSceneId == RiverSceneId)
            {
                // ApplyProjection can observe a save/load transition before the
                // next physics tick; never reuse a pre-restore support point.
                _waterSafetyHasLastSafePosition = false;
                _waterSafetyRestorePending = true;
            }

            ReleaseWaterSafetyGuard();
            return;
        }

        EnsureWaterSafetyGuard();
    }

    private void ProcessWaterSafety()
    {
        if (!IsInsideTree()
            || _bridge is not { } bridge
            || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge))
        {
            _waterSafetyActive = false;
            ReleaseWaterSafetyGuard();
            return;
        }

        var session = bridge.SessionIdentity;
        if (session is null)
        {
            // SessionIdentity is deliberately unavailable during restore. Do not
            // let a pre-load support point become the fallback for a loaded pose.
            if (bridge.ActiveSceneId == RiverSceneId)
            {
                _waterSafetyHasLastSafePosition = false;
                _waterSafetyRestorePending = true;
            }

            _waterSafetyActive = false;
            ReleaseWaterSafetyGuard();
            return;
        }

        if (!ReferenceEquals(_waterSafetySessionIdentity, session))
            ResetWaterSafetySession(session);

        var projection = bridge.CurrentW02WaterProjection();
        if (projection is null
            || projection.ActiveSceneId != RiverSceneId
            || !ReferenceEquals(projection.SessionIdentity, session))
        {
            _waterSafetyActive = false;
            if (bridge.ActiveSceneId == RiverSceneId)
            {
                // The route projector is suppressed while a load transaction is
                // preparing; discard any prior contact before accepting its pose.
                _waterSafetyHasLastSafePosition = false;
                _waterSafetyRestorePending = true;
            }
            else if (bridge.ActiveSceneId is not null)
            {
                _waterSafetyHasLastSafePosition = false;
            }

            ReleaseWaterSafetyGuard();
            return;
        }

        if (!_waterSafetyActive)
        {
            _waterSafetyActive = true;
            if (_waterSafetyRestorePending)
            {
                _waterSafetyHasLastSafePosition = false;
                _waterSafetyRestorePending = false;
            }
        }

        // A late queued W01/prologue teardown can clear the shared hook after
        // W02 projected. Reinstall only while this exact River session is live.
        EnsureWaterSafetyGuard();

        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !GodotObject.IsInstanceValid(player))
        {
            _waterSafetyPlayer = null;
            _waterSafetyHasLastSafePosition = false;
            _waterSafetyRayExclusions.Clear();
            return;
        }

        if (!ReferenceEquals(_waterSafetyPlayer, player))
        {
            _waterSafetyPlayer = player;
            _waterSafetyHasLastSafePosition = false;
            _waterSafetyRayExclusions.Clear();
            _waterSafetyRayExclusions.Add(player.GetRid());
        }

        if (IsWaterSafetySuspended(bridge, player)) return;

        // CharacterBody3D's authored player origin is at the feet. The water
        // cutoff is therefore expressed in the same world-space height.
        if (player.GlobalPosition.Y < WaterLevel - WaterSafetyRecoveryDepth)
        {
            RecoverWaterSafetyPlayer(player);
            return;
        }

        if (!player.IsOnFloor() || !HasWaterSafetySupport(player)) return;

        _waterSafetyLastSafePosition = player.GlobalPosition;
        _waterSafetyLastSafeYaw = player.GlobalRotationDegrees.Y;
        _waterSafetyHasLastSafePosition = true;
    }

    private bool IsWaterSafetySuspended(RuntimeBridge bridge, FirstPersonController player) =>
        bridge.IsPhotoWorldSaveRestoreInProgress
        || player.ModalOpen
        || player.VehicleControlled
        || player.IsClimbingLadder
        || MainMenuUi.AnyUndismissed(GetTree())
        || GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true }
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private bool HasWaterSafetySupport(FirstPersonController player)
    {
        var feet = player.GlobalPosition;
        var from = feet + Vector3.Up * WaterSafetyRayAboveFeet;
        var to = feet - Vector3.Up * WaterSafetyRayBelowFeet;
        using var ray = PhysicsRayQueryParameters3D.Create(from, to, 1u, _waterSafetyRayExclusions);
        ray.CollideWithAreas = false;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not StaticBody3D support)
            return false;

        if (_waterSafetyDecks.Contains(support))
            return true;

        var terrain = _world?.GetNodeOrNull<StaticBody3D>("W02TerrainContact");
        if (!ReferenceEquals(support, terrain)) return false;

        var surface = hit["position"].AsVector3();
        var dryHeight = WaterLevel + WaterSafetyDryClearance;
        return surface.Y >= dryHeight
            && TerrainHeight(surface.X, surface.Z) >= dryHeight;
    }

    private void RecoverWaterSafetyPlayer(FirstPersonController player)
    {
        if (_waterSafetyHasLastSafePosition
            && ReferenceEquals(_waterSafetyPlayer, player)
            && ReferenceEquals(_waterSafetySessionIdentity, _bridge?.SessionIdentity))
        {
            player.ApplyZoneSpawn(_waterSafetyLastSafePosition, _waterSafetyLastSafeYaw);
            return;
        }

        // Use the same named fallback accepted by Main.MountW02.
        var start = GetNodeOrNull<Node3D>("Anchors/R00") ?? GetNodeOrNull<Node3D>("Anchors/R08");
        if (start is not null
            && GodotObject.IsInstanceValid(start))
        {
            player.ApplyZoneSpawn(start.GlobalPosition, start.GlobalRotationDegrees.Y);
            return;
        }

        GD.PushError("W02 water safety recovery requires the authored R00 or R08 entry anchor.");
    }

    private void EnsureWaterSafetyGuard()
    {
        if (_waterSafetyGuard is null)
            _waterSafetyGuard = ClampWaterSafetyPosition;
        FirstPersonController.DetachedWorldGuard = _waterSafetyGuard;
    }

    private (Vector3 Position, float Ground) ClampWaterSafetyPosition(Vector3 position)
    {
        if (!_waterSafetyActive || !IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || _waterSafetySessionIdentity is null
            || !ReferenceEquals(_waterSafetySessionIdentity, bridge.SessionIdentity)
            || bridge.ActiveSceneId != RiverSceneId
            || _world is not { Visible: true })
            return (position, position.Y);

        var x = Mathf.Clamp(position.X, TerrainMinX + WaterSafetyTerrainEdgeInset, TerrainMaxX - WaterSafetyTerrainEdgeInset);
        var z = Mathf.Clamp(position.Z, TerrainMinZ + WaterSafetyTerrainEdgeInset, TerrainMaxZ - WaterSafetyTerrainEdgeInset);
        return (position with { X = x, Z = z }, TerrainHeight(x, z));
    }

    private void ResetWaterSafetySession(object session)
    {
        _waterSafetySessionIdentity = session;
        _waterSafetyPlayer = null;
        _waterSafetyHasLastSafePosition = false;
        _waterSafetyLastSafePosition = default;
        _waterSafetyLastSafeYaw = 0f;
        _waterSafetyRestorePending = false;
        _waterSafetyRayExclusions.Clear();
    }

    private void ReleaseWaterSafetyGuard()
    {
        if (_waterSafetyGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, _waterSafetyGuard))
            FirstPersonController.DetachedWorldGuard = null;
    }

    private void ReleaseWaterSafety()
    {
        _waterSafetyActive = false;
        ReleaseWaterSafetyGuard();
        _waterSafetySessionIdentity = null;
        _waterSafetyPlayer = null;
        _waterSafetyHasLastSafePosition = false;
        _waterSafetyRestorePending = false;
        _waterSafetyRayExclusions.Clear();
        _waterSafetyDecks.Clear();
        _waterSafetyGuard = null;
    }
}
