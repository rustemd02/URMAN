using System;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private const float ForestWorldGuardInset = 0.5f;
    private const string ForestWorldGroundName = "F00F16GroundVisual";

    private Aabb _forestWorldGroundBoundsLocal;
    private bool _forestWorldGroundBoundsCached;
    private Func<Vector3, (Vector3 Position, float Ground)>? _ownedForestWorldGuard;
    private object? _forestWorldGuardSessionIdentity;
    private bool _forestWorldGuardBoundsErrorReported;

    private void ApplyForestWorldGuardProjection(RuntimeBridge.W01BranchProjection? projection)
    {
        if (!IsCurrentForestWorldProjection(projection, out var bridge))
        {
            ReleaseForestWorldGuard();
            return;
        }

        _forestWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureForestWorldGuardInstalled(bridge);
    }

    private void ProcessForestWorldGuard()
    {
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge))
        {
            ReleaseForestWorldGuard();
            return;
        }

        var projection = bridge.CurrentW01BranchProjection();
        if (!IsCurrentForestWorldProjection(projection, out bridge))
        {
            ReleaseForestWorldGuard();
            return;
        }

        _forestWorldGuardSessionIdentity = projection!.SessionIdentity;
        EnsureForestWorldGuardInstalled(bridge);
    }

    private void ReleaseForestWorldGuard()
    {
        var ownedGuard = _ownedForestWorldGuard;
        if (ownedGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, ownedGuard))
        {
            FirstPersonController.DetachedWorldGuard = null;
        }

        _forestWorldGuardSessionIdentity = null;
    }

    private bool IsCurrentForestWorldProjection(
        RuntimeBridge.W01BranchProjection? projection,
        out RuntimeBridge bridge)
    {
        bridge = null!;
        if (!IsInsideTree() || IsQueuedForDeletion()
            || _bridge is not { } currentBridge || !IsValidCurrentBridge(currentBridge)
            || projection is null
            || !ReferenceEquals(projection.SessionIdentity, currentBridge.SessionIdentity)
            || projection.ActiveSceneId is not (PrologueSceneId or ForestSceneId))
        {
            return false;
        }

        var currentProjection = currentBridge.CurrentW01BranchProjection();
        if (currentProjection is null
            || !ReferenceEquals(currentProjection.SessionIdentity, projection.SessionIdentity)
            || currentProjection.ActiveSceneId != projection.ActiveSceneId)
        {
            return false;
        }

        bridge = currentBridge;
        return true;
    }

    private bool IsValidCurrentBridge(RuntimeBridge bridge) =>
        GodotObject.IsInstanceValid(this)
        && !IsQueuedForDeletion()
        && IsInsideTree()
        && GodotObject.IsInstanceValid(bridge)
        && !bridge.IsQueuedForDeletion()
        && bridge.IsInsideTree()
        && GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge treeBridge
        && ReferenceEquals(treeBridge, bridge);

    private void EnsureForestWorldGuardInstalled(RuntimeBridge bridge)
    {
        if (!IsForestGroundEnabled())
        {
            ReleaseForestWorldGuard();
            return;
        }

        if (!TryCacheForestWorldGroundBounds())
        {
            ReleaseForestWorldGuard();
            return;
        }

        _forestWorldGuardSessionIdentity = bridge.SessionIdentity;
        _ownedForestWorldGuard ??= ClampToForestWorld;
        // Another scene may clear its own guard during QueueFree after this
        // projection. Reassert only while this exact W01 presentation is active.
        FirstPersonController.DetachedWorldGuard = _ownedForestWorldGuard;
    }

    private bool IsForestGroundEnabled() =>
        _groundVisual is { } visual
        && GodotObject.IsInstanceValid(visual)
        && !visual.IsQueuedForDeletion()
        && visual.IsInsideTree()
        && visual.Name == ForestWorldGroundName
        && visual.Visible
        && _groundBody is { } body
        && GodotObject.IsInstanceValid(body)
        && !body.IsQueuedForDeletion()
        && body.IsInsideTree()
        && body.CollisionLayer != 0u;

    private bool TryCacheForestWorldGroundBounds()
    {
        if (_forestWorldGroundBoundsCached) return true;
        if (!IsForestGroundEnabled() || _groundVisual is not { } ground)
            return false;

        var toController = GlobalTransform.AffineInverse();
        var anyMesh = false;
        var bounds = new Aabb();
        foreach (var mesh in Descendants<MeshInstance3D>(ground))
        {
            if (!GodotObject.IsInstanceValid(mesh) || mesh.IsQueuedForDeletion()
                || mesh.Mesh is not { } resource)
                continue;

            var meshBounds = toController * mesh.GlobalTransform * resource.GetAabb();
            bounds = anyMesh ? bounds.Merge(meshBounds) : meshBounds;
            anyMesh = true;
        }

        if (!anyMesh
            || bounds.Size.X <= ForestWorldGuardInset * 2f
            || bounds.Size.Z <= ForestWorldGuardInset * 2f)
        {
            if (!_forestWorldGuardBoundsErrorReported)
            {
                GD.PushError("W01 world guard could not derive a usable bounds union from F00F16GroundVisual.");
                _forestWorldGuardBoundsErrorReported = true;
            }

            return false;
        }

        _forestWorldGroundBoundsLocal = bounds;
        _forestWorldGroundBoundsCached = true;
        return true;
    }

    private (Vector3 Position, float Ground) ClampToForestWorld(Vector3 worldPosition)
    {
        if (_forestWorldGuardSessionIdentity is not { } ownerSession
            || _bridge is not { } bridge || !IsValidCurrentBridge(bridge)
            || !ReferenceEquals(bridge.SessionIdentity, ownerSession)
            || bridge.CurrentW01BranchProjection() is not { } projection
            || !ReferenceEquals(projection.SessionIdentity, ownerSession)
            || projection.ActiveSceneId is not (PrologueSceneId or ForestSceneId)
            || !IsForestGroundEnabled()
            || !_forestWorldGroundBoundsCached)
        {
            return (worldPosition, worldPosition.Y);
        }

        var localPosition = ToLocal(worldPosition);
        var localX = Mathf.Clamp(
            localPosition.X,
            _forestWorldGroundBoundsLocal.Position.X + ForestWorldGuardInset,
            _forestWorldGroundBoundsLocal.End.X - ForestWorldGuardInset);
        var localZ = Mathf.Clamp(
            localPosition.Z,
            _forestWorldGroundBoundsLocal.Position.Z + ForestWorldGuardInset,
            _forestWorldGroundBoundsLocal.End.Z - ForestWorldGuardInset);

        var clampedWorld = ToGlobal(new Vector3(localX, localPosition.Y, localZ));
        var groundWorld = ToGlobal(new Vector3(localX, ForestGroundHeight(localX, localZ), localZ));
        return (new Vector3(clampedWorld.X, worldPosition.Y, clampedWorld.Z), groundWorld.Y);
    }
}
