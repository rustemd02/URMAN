using Godot;

namespace Urman.Godot;

public partial class CarryCoordinator
{
    // Held props are not rigid bodies: their entire volume is swept explicitly.
    // A single camera ray cannot keep the far end of a board out of a wall.
    private bool ClearVolume(CarryableProp prop, Vector3 feet, float yaw, Vector3 motion = default,
        float? sweepYaw = null)
    {
        if (_camera is null || _player is null) return false;
        using var shape = new BoxShape3D { Size = prop.Size };
        var basis = Basis.FromEuler(new(0, Mathf.DegToRad(sweepYaw ?? yaw), 0));
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new(basis, feet + Vector3.Up * prop.Height * .5f),
            CollisionMask = 3u,
            Margin = .008f,
            Exclude = new global::Godot.Collections.Array<Rid> { prop.GetRid(), _player.GetRid() }
        };
        var space = _camera.GetWorld3D().DirectSpaceState;
        if (space.IntersectShape(query, 1).Count > 0) return false;
        if (motion.LengthSquared() < .000001f) return true;
        query.Motion = motion;
        var fractions = space.CastMotion(query);
        return fractions.Length >= 2 && fractions[0] >= .999f;
    }

    private Vector3 DesiredHoldPoint(CarryableProp prop, Vector3 displacement)
    {
        var forward = -_camera!.GlobalBasis.Z;
        var horizontal = new Vector3(forward.X, 0, forward.Z).Normalized();
        if (horizontal.LengthSquared() < .01f) horizontal = -_player!.GlobalBasis.Z;
        return _camera.GlobalPosition + displacement + horizontal * prop.HoldDistance
            + Vector3.Up * (prop.HoldDrop - Mathf.Max(0, prop.Height - .45f) * .55f);
    }

    private bool TryHeldPose(CarryableProp prop, Vector3 displacement, float yaw, out Vector3 point)
    {
        point = Vector3.Zero;
        if (_camera is null || _player is null) return false;
        var desired = DesiredHoldPoint(prop, displacement);
        var forward = -_camera.GlobalBasis.Z;
        forward = new Vector3(forward.X, 0, forward.Z).Normalized();
        var right = new Vector3(-forward.Z, 0, forward.X);
        // Small retreats preserve the requested world rotation. We never
        // shrink the object, draw it through a wall, or teleport it behind one.
        foreach (var offset in new[] { Vector3.Zero, -forward * .18f, -forward * .35f,
                     right * .22f, -right * .22f })
        {
            var candidate = desired + offset;
            if (!ClearVolume(prop, candidate, yaw)) continue;
            if (_heldPoseValid && _heldPoseItem == prop.ItemId
                && !ClearVolume(prop, prop.GlobalPosition, yaw, candidate - prop.GlobalPosition)) continue;
            point = candidate;
            return true;
        }
        return false;
    }

    private bool CanRotateHeld(CarryableProp prop, float yaw)
    {
        if (!TryHeldPose(prop, Vector3.Zero, yaw, out var candidate)) return false;
        // Check intermediate orientations as well as the final 15-degree step.
        for (var fraction = 1; fraction <= 5; fraction++)
        {
            var angle = prop.YawDegrees + 15f * fraction / 5f;
            if (!ClearVolume(prop, candidate, angle)) return false;
        }
        return true;
    }

    internal Vector3 ConstrainCarriedMovement(Vector3 velocity, float delta)
    {
        if (_held is null || !_heldPoseValid || _camera is null || _player is null) return velocity;
        var move = new Vector3(velocity.X * delta, 0, velocity.Z * delta);
        if (move.LengthSquared() < .000001f || TryHeldPose(_held, move, _held.YawDegrees, out _)) return velocity;
        // Slide along the obstacle where one horizontal axis is still clear.
        var x = TryHeldPose(_held, new(move.X, 0, 0), _held.YawDegrees, out _) ? velocity.X : 0;
        var z = TryHeldPose(_held, new(0, 0, move.Z), _held.YawDegrees, out _) ? velocity.Z : 0;
        if (x != 0 && z != 0) z = 0;
        return new(x, velocity.Y, z);
    }

    internal bool IsSupportingSomething(CarryableProp support)
    {
        if (_player is null || _camera is null) return false;
        for (var i = 0; i < _player.GetSlideCollisionCount(); i++)
        {
            var collision = _player.GetSlideCollision(i);
            if (collision.GetCollider() == support && collision.GetNormal().Y > .5f) return true;
        }
        var underFeet = Trace(_player.GlobalPosition + Vector3.Up * .04f,
            _player.GlobalPosition - Vector3.Up * .10f);
        if (underFeet.Count > 0 && underFeet["collider"].AsGodotObject() == support) return true;
        foreach (var item in _props)
        {
            if (item == support || !item.IsVisibleInTree()
                || item.State is not (CarryableProp.CarryState.World or CarryableProp.CarryState.Placed or CarryableProp.CarryState.Combined)) continue;
            // Query just beneath the other thing, excluding that thing itself.
            using var ray = PhysicsRayQueryParameters3D.Create(item.GlobalPosition - Vector3.Up * .004f,
                item.GlobalPosition - Vector3.Up * .09f, 3u);
            ray.Exclude = new global::Godot.Collections.Array<Rid> { item.GetRid(), _player.GetRid() };
            var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count > 0 && hit["collider"].AsGodotObject() == support) return true;
        }
        return false;
    }
}
