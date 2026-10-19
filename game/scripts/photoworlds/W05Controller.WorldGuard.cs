using System;
using Godot;

namespace Urman.Godot;

public partial class W05Controller
{
    private Func<Vector3, (Vector3 Position, float Ground)>? _ownedCellarGuard;
    private object? _guardSession;

    private void ProcessCellarGuard()
    {
        if (!TryCurrentCellar(out _, out var session) || !_active || !CellarFloorEnabled())
        {
            ReleaseCellarGuard();
            if (_active) ApplyProjection();
            return;
        }
        if (!ReferenceEquals(_session, session))
        {
            ApplyProjection();
            return;
        }
        EnsureCellarGuard();
    }

    private bool CellarFloorEnabled() => _world is not null && GodotObject.IsInstanceValid(_world)
        && _world.IsInsideTree() && !_world.IsQueuedForDeletion() && _world.Visible
        && GodotObject.IsInstanceValid(_floorSurface) && _floorSurface.Visible && _floorSurface.Mesh is not null
        && GodotObject.IsInstanceValid(_floorBody) && _floorBody.CollisionLayer != 0u;

    private void EnsureCellarGuard()
    {
        if (!CellarFloorEnabled() || !TryCurrentCellar(out var bridge, out var session)
            || bridge.IsPhotoWorldPresentationDeferred
            || !ReferenceEquals(bridge.SessionIdentity, session))
        {
            ReleaseCellarGuard();
            return;
        }
        _guardSession = session;
        _ownedCellarGuard ??= ClampToCellar;
        FirstPersonController.DetachedWorldGuard = _ownedCellarGuard;
    }

    private void ReleaseCellarGuard()
    {
        if (_ownedCellarGuard is not null
            && ReferenceEquals(FirstPersonController.DetachedWorldGuard, _ownedCellarGuard))
            FirstPersonController.DetachedWorldGuard = null;
        _guardSession = null;
    }

    private (Vector3 Position, float Ground) ClampToCellar(Vector3 position)
    {
        if (_guardSession is null || !TryCurrentCellar(out _, out var session)
            || !ReferenceEquals(_guardSession, session) || !CellarFloorEnabled())
            return (position, position.Y);
        var local = _world.ToLocal(position);
        // Stay inside the visible perimeter walls, including the player's capsule.
        var x = Mathf.Clamp(local.X, FloorMinX + 1.1f, FloorMaxX - 1.1f);
        var z = Mathf.Clamp(local.Z, FloorMinZ + 1.1f, FloorMaxZ - 1.1f);
        var clamped = _world.ToGlobal(new Vector3(x, local.Y, z));
        var ground = _world.ToGlobal(new Vector3(x, CellarFloorHeight(z), z));
        return (new Vector3(clamped.X, position.Y, clamped.Z), ground.Y);
    }
}
