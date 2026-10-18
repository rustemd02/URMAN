using System;
using Godot;

namespace Urman.Godot;

public partial class W04Controller
{
    private const float CommonWorldGuardInset = 0.5f;
    private const string CommonGroundSurfaceName = "W04GroundSurface";
    private const string CommonGroundBodyName = "W04GroundBody";

    private Aabb _commonWorldGroundBoundsLocal;
    private bool _commonWorldGroundBoundsCached;
    private Func<Vector3, (Vector3 Position, float Ground)>? _ownedCommonWorldGuard;
    private object? _commonWorldGuardSessionIdentity;
    private bool _commonWorldGuardBoundsErrorReported;

    private void ApplyCommonWorldGuardProjection(
        RuntimeBridge.FullGamePhotoWorldRouteProjection? projection, bool visible)
    {
        if (!visible || !IsCurrentCommonWorldProjection(projection, out var bridge))
        {
            ReleaseCommonWorldGuard();
            return;
        }
        _commonWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureCommonWorldGuardInstalled(bridge);
    }

    private void ProcessCommonWorldGuard()
    {
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !IsValidCommonWorldBridge(bridge))
        {
            ReleaseCommonWorldGuard();
            return;
        }
        var projection = bridge.CurrentFullGamePhotoWorldRouteProjection();
        if (!IsCurrentCommonWorldProjection(projection, out bridge))
        {
            ReleaseCommonWorldGuard();
            return;
        }
        _commonWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureCommonWorldGuardInstalled(bridge);
    }

    private void ReleaseCommonWorldGuard()
    {
        var ownedGuard = _ownedCommonWorldGuard;
        if (ownedGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, ownedGuard))
            FirstPersonController.DetachedWorldGuard = null;
        _commonWorldGuardSessionIdentity = null;
    }

    private bool IsCurrentCommonWorldProjection(
        RuntimeBridge.FullGamePhotoWorldRouteProjection? projection, out RuntimeBridge bridge)
    {
        bridge = null!;
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } currentBridge || !IsValidCommonWorldBridge(currentBridge)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, currentBridge.SessionIdentity)
            || projection.ActiveSceneId != CommonSceneId)
            return false;
        var currentProjection = currentBridge.CurrentFullGamePhotoWorldRouteProjection();
        if (currentProjection is null
            || !ReferenceEquals(currentProjection.SessionIdentity, projection.SessionIdentity)
            || currentProjection.ActiveSceneId != projection.ActiveSceneId)
            return false;
        bridge = currentBridge;
        return true;
    }

    private bool IsValidCommonWorldBridge(RuntimeBridge bridge) =>
        GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion() && IsInsideTree()
        && GodotObject.IsInstanceValid(bridge) && !bridge.IsQueuedForDeletion() && bridge.IsInsideTree()
        && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge treeBridge
        && ReferenceEquals(treeBridge, bridge);

    private void EnsureCommonWorldGuardInstalled(RuntimeBridge bridge)
    {
        if (!IsCommonGroundEnabled() || !TryCacheCommonWorldGroundBounds())
        {
            ReleaseCommonWorldGuard();
            return;
        }
        _commonWorldGuardSessionIdentity = bridge.SessionIdentity;
        _ownedCommonWorldGuard ??= ClampToCommonWorld;
        FirstPersonController.DetachedWorldGuard = _ownedCommonWorldGuard;
    }

    private bool IsCommonGroundEnabled()
    {
        var ground = _world?.GetNodeOrNull<MeshInstance3D>(CommonGroundSurfaceName);
        var body = _world?.GetNodeOrNull<CollisionObject3D>(CommonGroundBodyName);
        return _world is { } world && GodotObject.IsInstanceValid(world)
            && !world.IsQueuedForDeletion() && world.IsInsideTree() && world.Visible
            && ground is { } surface && GodotObject.IsInstanceValid(surface)
            && !surface.IsQueuedForDeletion() && surface.IsInsideTree() && surface.Visible
            && surface.Mesh is not null
            && body is { } support && GodotObject.IsInstanceValid(support)
            && !support.IsQueuedForDeletion() && support.IsInsideTree() && support.CollisionLayer != 0u;
    }

    private bool TryCacheCommonWorldGroundBounds()
    {
        if (_commonWorldGroundBoundsCached) return true;
        if (!IsCommonGroundEnabled() || _world is not { } world
            || world.GetNodeOrNull<MeshInstance3D>(CommonGroundSurfaceName) is not { Mesh: { } mesh } ground)
            return false;
        var bounds = world.GlobalTransform.AffineInverse() * ground.GlobalTransform * mesh.GetAabb();
        if (bounds.Size.X <= CommonWorldGuardInset * 2f || bounds.Size.Z <= CommonWorldGuardInset * 2f)
        {
            if (!_commonWorldGuardBoundsErrorReported)
            {
                GD.PushError("W04 world guard could not derive bounds from W04GroundSurface.");
                _commonWorldGuardBoundsErrorReported = true;
            }
            return false;
        }
        _commonWorldGroundBoundsLocal = bounds;
        _commonWorldGroundBoundsCached = true;
        return true;
    }

    private (Vector3 Position, float Ground) ClampToCommonWorld(Vector3 worldPosition)
    {
        if (_commonWorldGuardSessionIdentity is not { } ownerSession
            || _bridge is not { } bridge || !IsValidCommonWorldBridge(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, ownerSession)
            || bridge.CurrentFullGamePhotoWorldRouteProjection() is not { } projection
            || !ReferenceEquals(projection.SessionIdentity, ownerSession)
            || projection.ActiveSceneId != CommonSceneId || !IsCommonGroundEnabled()
            || !_commonWorldGroundBoundsCached || _world is not { } world)
            return (worldPosition, worldPosition.Y);

        var localPosition = world.ToLocal(worldPosition);
        var localX = Mathf.Clamp(localPosition.X,
            _commonWorldGroundBoundsLocal.Position.X + CommonWorldGuardInset,
            _commonWorldGroundBoundsLocal.End.X - CommonWorldGuardInset);
        var localZ = Mathf.Clamp(localPosition.Z,
            _commonWorldGroundBoundsLocal.Position.Z + CommonWorldGuardInset,
            _commonWorldGroundBoundsLocal.End.Z - CommonWorldGuardInset);
        var clampedWorld = world.ToGlobal(new Vector3(localX, localPosition.Y, localZ));
        var groundWorld = world.ToGlobal(new Vector3(localX, CommonGroundHeightAt(localX, localZ), localZ));
        return (new Vector3(clampedWorld.X, worldPosition.Y, clampedWorld.Z), groundWorld.Y);
    }

    private void ExitCommonWorldGuard()
    {
        ReleaseCommonWorldGuard();
        _commonWorldGroundBoundsCached = false;
        _commonWorldGuardBoundsErrorReported = false;
    }
}
