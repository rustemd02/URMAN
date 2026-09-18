using Godot;

namespace Urman.Godot;

/// <summary>
/// Answers whether a point is actually inside a portable lantern's light.
/// The lamp is a real OmniLight3D on the carried item; this only decides
/// whether the light reaches a place, so a lit detail never becomes a
/// detector: it still needs the ordinary interaction to be read.
/// </summary>
public static class PortableLight
{
    // Just under the lamp's own OmniRange, so a detail at the edge of the
    // visible pool is still readable while a lantern across a wall is not.
    public const float Reach = 3.4f;
    private const uint Occluders = 3u;
    private const float GrazeMargin = .22f;

    public static bool IsLit(Node3D owner, Vector3 point)
    {
        var coordinator = owner.GetTree()?.GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (coordinator is null) return false;
        foreach (var item in coordinator.Items)
        {
            if (item.Kind != CarryableProp.ItemKind.Lantern || !item.LightOn
                || item.IsConcealed || !item.IsVisibleInTree()) continue;
            var lamp = item.GlobalPosition + Vector3.Up * (item.Height * .5f);
            if (lamp.DistanceTo(point) > Reach) continue;
            if (ReachesWithSight(owner, item, lamp, point)) return true;
        }
        return false;
    }

    private static bool ReachesWithSight(Node3D owner, CarryableProp item, Vector3 lamp, Vector3 point)
    {
        var space = owner.GetWorld3D()?.DirectSpaceState;
        if (space is null) return false;
        var ray = PhysicsRayQueryParameters3D.Create(lamp, point, Occluders);
        // The lamp's own body must not shadow its light.
        ray.Exclude = new global::Godot.Collections.Array<Rid> { item.GetRid() };
        var hit = space.IntersectRay(ray);
        if (hit.Count == 0) return true;
        return hit["position"].AsVector3().DistanceTo(point) <= GrazeMargin;
    }
}
