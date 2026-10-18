using System;
using Godot;

namespace Urman.Godot;

public partial class W03Controller
{
    private const float HouseWorldGuardInset = 0.5f;
    private const string HouseGroundSurfaceName = "W03GroundSurface";
    private const string HouseGroundBodyName = "W03GroundBody";

    private Aabb _houseWorldGroundBoundsLocal;
    private bool _houseWorldGroundBoundsCached;
    private Func<Vector3, (Vector3 Position, float Ground)>? _ownedHouseWorldGuard;
    private object? _houseWorldGuardSessionIdentity;
    private bool _houseWorldGuardBoundsErrorReported;

    private void ApplyHouseWorldGuardProjection(
        RuntimeBridge.FullGamePhotoWorldRouteProjection? projection, bool visible)
    {
        if (!visible || !IsCurrentHouseWorldProjection(projection, out var bridge))
        {
            ReleaseHouseWorldGuard();
            return;
        }

        _houseWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureHouseWorldGuardInstalled(bridge);
    }

    private void ProcessHouseWorldGuard()
    {
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !IsValidHouseWorldBridge(bridge))
        {
            ReleaseHouseWorldGuard();
            return;
        }

        var projection = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (!IsCurrentHouseWorldProjection(projection, out bridge))
        {
            ReleaseHouseWorldGuard();
            return;
        }

        _houseWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureHouseWorldGuardInstalled(bridge);
    }

    private void ReleaseHouseWorldGuard()
    {
        var ownedGuard = _ownedHouseWorldGuard;
        if (ownedGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, ownedGuard))
            FirstPersonController.DetachedWorldGuard = null;

        _houseWorldGuardSessionIdentity = null;
    }

    private bool IsCurrentHouseWorldProjection(
        RuntimeBridge.FullGamePhotoWorldRouteProjection? projection,
        out RuntimeBridge bridge)
    {
        bridge = null!;
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } currentBridge || !IsValidHouseWorldBridge(currentBridge)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, currentBridge.SessionIdentity)
            || projection.ActiveSceneId != HouseSceneId)
            return false;

        var currentProjection = currentBridge.CurrentFullGamePhotoWorldRouteProjection();
        if (currentProjection is null
            || !ReferenceEquals(currentProjection.SessionIdentity, projection.SessionIdentity)
            || currentProjection.ActiveSceneId != projection.ActiveSceneId)
            return false;

        bridge = currentBridge;
        return true;
    }

    private bool IsValidHouseWorldBridge(RuntimeBridge bridge) =>
        GodotObject.IsInstanceValid(this)
        && !IsQueuedForDeletion()
        && IsInsideTree()
        && GodotObject.IsInstanceValid(bridge)
        && !bridge.IsQueuedForDeletion()
        && bridge.IsInsideTree()
        && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge treeBridge
        && ReferenceEquals(treeBridge, bridge);

    private void EnsureHouseWorldGuardInstalled(RuntimeBridge bridge)
    {
        if (!IsHouseGroundEnabled() || !TryCacheHouseWorldGroundBounds())
        {
            ReleaseHouseWorldGuard();
            return;
        }

        _houseWorldGuardSessionIdentity = bridge.SessionIdentity;
        _ownedHouseWorldGuard ??= ClampToHouseWorld;
        // A former W01/W02 node may clear its delegate while QueueFree runs.
        // Reassert only while this exact house photo-world projection is active.
        FirstPersonController.DetachedWorldGuard = _ownedHouseWorldGuard;
    }

    private bool IsHouseGroundEnabled()
    {
        var ground = _world?.GetNodeOrNull<MeshInstance3D>(HouseGroundSurfaceName);
        var body = _world?.GetNodeOrNull<CollisionObject3D>(HouseGroundBodyName);
        return _world is { } world
            && GodotObject.IsInstanceValid(world)
            && !world.IsQueuedForDeletion()
            && world.IsInsideTree()
            && world.Visible
            && ground is { } surface
            && GodotObject.IsInstanceValid(surface)
            && !surface.IsQueuedForDeletion()
            && surface.IsInsideTree()
            && surface.Visible
            && surface.Mesh is not null
            && body is { } support
            && GodotObject.IsInstanceValid(support)
            && !support.IsQueuedForDeletion()
            && support.IsInsideTree()
            && support.CollisionLayer != 0u;
    }

    private bool TryCacheHouseWorldGroundBounds()
    {
        if (_houseWorldGroundBoundsCached) return true;
        if (!IsHouseGroundEnabled() || _world is not { } world
            || world.GetNodeOrNull<MeshInstance3D>(HouseGroundSurfaceName) is not { } ground
            || ground.Mesh is not { } mesh)
            return false;

        var toWorld = world.GlobalTransform.AffineInverse();
        var bounds = toWorld * ground.GlobalTransform * mesh.GetAabb();
        if (bounds.Size.X <= HouseWorldGuardInset * 2f
            || bounds.Size.Z <= HouseWorldGuardInset * 2f)
        {
            if (!_houseWorldGuardBoundsErrorReported)
            {
                GD.PushError("W03 world guard could not derive bounds from W03GroundSurface.");
                _houseWorldGuardBoundsErrorReported = true;
            }
            return false;
        }

        _houseWorldGroundBoundsLocal = bounds;
        _houseWorldGroundBoundsCached = true;
        return true;
    }

    private (Vector3 Position, float Ground) ClampToHouseWorld(Vector3 worldPosition)
    {
        if (_houseWorldGuardSessionIdentity is not { } ownerSession
            || _bridge is not { } bridge || !IsValidHouseWorldBridge(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, ownerSession)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { } projection
            || !ReferenceEquals(projection.SessionIdentity, ownerSession)
            || projection.ActiveSceneId != HouseSceneId
            || !IsHouseGroundEnabled()
            || !_houseWorldGroundBoundsCached
            || _world is not { } world)
            return (worldPosition, worldPosition.Y);

        var localPosition = world.ToLocal(worldPosition);
        var localX = Mathf.Clamp(localPosition.X,
            _houseWorldGroundBoundsLocal.Position.X + HouseWorldGuardInset,
            _houseWorldGroundBoundsLocal.End.X - HouseWorldGuardInset);
        var localZ = Mathf.Clamp(localPosition.Z,
            _houseWorldGroundBoundsLocal.Position.Z + HouseWorldGuardInset,
            _houseWorldGroundBoundsLocal.End.Z - HouseWorldGuardInset);

        var clampedWorld = world.ToGlobal(new Vector3(localX, localPosition.Y, localZ));
        var groundLocalY = HouseGroundHeightAt(localX, localZ);
        var groundWorld = world.ToGlobal(new Vector3(localX, groundLocalY, localZ));
        return (new Vector3(clampedWorld.X, worldPosition.Y, clampedWorld.Z), groundWorld.Y);
    }

    private void ExitHouseWorldGuard()
    {
        ReleaseHouseWorldGuard();
        _houseWorldGroundBoundsCached = false;
        _houseWorldGuardBoundsErrorReported = false;
    }
}
