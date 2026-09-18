using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Older hall saves rested on timber beneath the visible rug. Only this
    // measured support change may lift those feet; arbitrary blocked saves
    // still follow RuntimeBridge's ordinary rejection and rollback path.
    internal bool TryResolveLegacyMosqueCarpetFeet(FirstPersonController player, out Vector3 destination)
    {
        destination = player.GlobalPosition;
        if (!FacilityExteriorActive || _mosqueRoom is null || !_mosqueRoom.IsVisibleInTree()
            || player.VehicleControlled
            || _mosqueRoom.GetNodeOrNull<StaticBody3D>("MosquePrayerCarpetBody") is not { } carpet
            || _mosqueRoom.GetNodeOrNull<StaticBody3D>("MosqueTimberFloorBody") is not { } timber
            || carpet.GetNodeOrNull<CollisionShape3D>("Contact") is not { Disabled: false, Shape: BoxShape3D carpetBox } carpetShape
            || timber.GetNodeOrNull<CollisionShape3D>("Contact") is not { Disabled: false, Shape: BoxShape3D timberBox } timberShape
            || (carpet.CollisionLayer & player.CollisionMask) == 0
            || (timber.CollisionLayer & player.CollisionMask) == 0)
            return false;

        var oldFeet = player.GlobalPosition;
        var onCarpet = carpetShape.ToLocal(oldFeet);
        var onTimber = timberShape.ToLocal(oldFeet);
        if (Math.Abs(onCarpet.X) > carpetBox.Size.X * .5f || Math.Abs(onCarpet.Z) > carpetBox.Size.Z * .5f
            || Math.Abs(onTimber.X) > timberBox.Size.X * .5f || Math.Abs(onTimber.Z) > timberBox.Size.Z * .5f
            || Math.Abs(onTimber.Y - timberBox.Size.Y * .5f) > .003f
            || carpetShape.GlobalBasis.Y.Normalized().Dot(Vector3.Up) < .9999f
            || timberShape.GlobalBasis.Y.Normalized().Dot(Vector3.Up) < .9999f)
            return false;

        var space = player.GetWorld3D().DirectSpaceState;
        var exclusions = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var ray = PhysicsRayQueryParameters3D.Create(oldFeet + Vector3.Up * .05f,
            oldFeet - Vector3.Up * .05f, player.CollisionMask, exclusions);
        var rugHit = space.IntersectRay(ray);
        if (rugHit.Count == 0 || rugHit["collider"].AsGodotObject() != carpet
            || rugHit["normal"].AsVector3().Dot(Vector3.Up) < .9999f)
            return false;
        // A 3 mm starting clearance clears the existing 2 mm query margin.
        // Ordinary gravity then settles onto this exact surface after loading.
        var candidate = new Vector3(oldFeet.X, rugHit["position"].AsVector3().Y + .003f, oldFeet.Z);
        var motion = candidate - oldFeet;
        if (motion.Y <= 0 || motion.Y > .020f) return false;
        ray.Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid(), carpet.GetRid() };
        var floorHit = space.IntersectRay(ray);
        if (floorHit.Count == 0 || floorHit["collider"].AsGodotObject() != timber
            || Math.Abs(floorHit["position"].AsVector3().Y - oldFeet.Y) > .003f)
            return false;

        var capsule = player.GetNode<CollisionShape3D>("CollisionShape3D");
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule.Shape,
            Transform = capsule.GlobalTransform,
            CollisionMask = player.CollisionMask,
            Exclude = new global::Godot.Collections.Array<Rid> { player.GetRid(), carpet.GetRid(), timber.GetRid() },
            Margin = .002f
        };
        // The two known supports may touch the old capsule. No other initial
        // overlap or obstruction anywhere along the small vertical sweep is allowed.
        if (space.IntersectShape(query, 1).Count != 0) return false;
        query.Motion = motion;
        var travel = space.CastMotion(query);
        if (travel.Length < 2 || travel[0] < .999999f || travel[1] < .999999f) return false;
        query.Motion = Vector3.Zero;
        query.Transform = new Transform3D(capsule.GlobalBasis, capsule.GlobalPosition + motion);
        query.Exclude = exclusions;
        if (space.IntersectShape(query, 1).Count != 0 || !player.CanCrouchAt(candidate)
            || (_carryCoordinator is not null && !_carryCoordinator.CanCarryThroughStep(motion, Vector3.Zero, Vector3.Zero)))
            return false;
        destination = candidate;
        return true;
    }
}
