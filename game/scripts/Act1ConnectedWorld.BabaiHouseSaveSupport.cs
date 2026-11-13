using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal static bool NeedsLegacyBabaiHouseWorld(SaveGameV3 save)
    {
        if (save.CurrentZone.Value == "house_old_pc") return true;
        if (!save.Runtime.State.TryGetProperty("world.props", out var props) || props.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in props.EnumerateObject())
        {
            var record = property.Value;
            if (record.ValueKind == JsonValueKind.Object
                && record.TryGetProperty("zone", out var zone) && zone.ValueKind == JsonValueKind.String && zone.GetString() == "house_old_pc"
                && record.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String
                && state.GetString() is ("placed" or "combined")) return true;
        }
        return false;
    }

    // Content placement compatibility only: adapt the in-memory load candidate,
    // never the stored slot, history, custody, or story facts.
    internal SaveGameV3 AdaptLegacyBabaiHouseSave(SaveGameV3 save)
    {
        if (!_zoneInstances.TryGetValue("house_old_pc", out var room)
            || !room.HasNode("NewBabaiHouseInterior")) return save;

        static bool Legacy(Vector3 p) => p.IsFinite() && Math.Abs(p.X) < 5.6f
            && Math.Abs(p.Z) < 4.2f && p.Y > -31.5f && p.Y < -22.5f;
        static (Vector3 Safe, Vector3 Origin, float Yaw) Destination(Vector3 p)
        {
            if (p.Y >= -27.3f) return (new(5.85f, 5.65f, -2.60f), new(0, 2.8f, -2), 0);
            if (p.X < -1.5f) return (new(.25f, .05f, -5.55f), new(-2.6f, 0, -8.8f), 90);
            if (p.Z < .5f) return (new(-2.7f, 2.85f, -3.9f), new(-5, 2.8f, -3.8f), 0);
            if (p.X > 3.4f) return (new(2.85f, .05f, -5.4f), new(-1, 0, -7.4f), 0);
            return (new(1, 2.85f, 1), new(0, 2.8f, -.5f), 0);
        }

        var original = save.PlayerTransform;
        var feet = room.ToLocal(new((float)original.Position.X, (float)original.Position.Y, (float)original.Position.Z));
        var movedPlayer = save.CurrentZone.Value == "house_old_pc" && Legacy(feet);
        if (movedPlayer)
        {
            var target = room.ToGlobal(Destination(feet).Safe);
            save = save with { PlayerTransform = new(new(target.X, target.Y, target.Z), original.RotationDegrees) };
        }

        JsonObject? state = null;
        var movedItems = 0;
        if (save.Runtime.State.TryGetProperty("world.props", out var props) && props.ValueKind == JsonValueKind.Object)
        foreach (var property in props.EnumerateObject())
        {
            var record = property.Value;
            if (record.ValueKind != JsonValueKind.Object
                || !record.TryGetProperty("zone", out var zone) || zone.ValueKind != JsonValueKind.String || zone.GetString() != "house_old_pc"
                || !record.TryGetProperty("state", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() is not ("placed" or "combined")
                || !record.TryGetProperty("x", out var x) || x.ValueKind != JsonValueKind.Number || !x.TryGetSingle(out var px)
                || !record.TryGetProperty("y", out var y) || y.ValueKind != JsonValueKind.Number || !y.TryGetSingle(out var py)
                || !record.TryGetProperty("z", out var z) || z.ValueKind != JsonValueKind.Number || !z.TryGetSingle(out var pz)) continue;
            var oldLocal = room.ToLocal(new(px, py, pz));
            if (!Legacy(oldLocal))
            {
                if (status.GetString() != "placed" || !TryMoveCompactBabaiSupport(oldLocal, out var compact)) continue;
                var compactWorld = room.ToGlobal(compact);
                state ??= JsonNode.Parse(save.Runtime.State.GetRawText())!.AsObject();
                var compactRecord = state["world.props"]![property.Name]!.AsObject();
                compactRecord["x"] = compactWorld.X; compactRecord["y"] = compactWorld.Y; compactRecord["z"] = compactWorld.Z;
                movedItems++;
                continue;
            }
            var destination = Destination(oldLocal);
            // Keep a dropped object's offset from its original furniture/support
            // when that room is rigidly moved, including the kitchen's yaw.
            var local = oldLocal - StyleBenchmarkInteriorFactory.WingOrigin;
            var mapped = new Basis(Vector3.Up, Mathf.DegToRad(destination.Yaw)) * local + destination.Origin;
            var yawChange = destination.Yaw;
            // These two supports moved within their new rooms as well. A placed
            // item's origin is its bottom, saved .006 m above the support by
            // CarryCoordinator. Match only the old top interior (not its edge,
            // nearby floor, stacked items, held items, or combined assemblies).
            if (status.GetString() == "placed")
            {
                if (local.X > -2.00f && local.X < -1.58f
                    && local.Z > -2.83f && local.Z < -1.57f
                    && Math.Abs(local.Y-1.906f) < .004f)
                    mapped += Vector3.Right * 1.20f;
                else if (local.X > -1.37f && local.X < -.91f
                    && local.Z > -3.03f && local.Z < -1.97f
                    && Math.Abs(local.Y-.906f) < .004f)
                    mapped += Vector3.Forward * .30f;
            }
            // The retired stair's contact is a smooth ramp, NOT its fourteen
            // decorative treads. Its top plane is exactly progress * 2.8:
            // the .1 m box centre is lowered .05 m along its upper normal.
            // Map that support to the corresponding second-flight tread and
            // preserve the item's clearance above it (and its physical size).
            var stairProgress = Mathf.Clamp((3.3f-local.X)/4.7f, 0, 1);
            if (local.X >= -1.4f && local.X <= 3.3f && local.Z >= 2.45f && local.Z <= 3.45f
                && local.Y >= stairProgress*2.8f-.03f && local.Y <= 2.8f)
            {
                var progress = stairProgress;
                var top = Math.Max(1, Math.Ceiling(progress*18)) * (2.8f/18);
                mapped = new(5.85f-(local.Z-2.95f),
                    2.8f+(float)top+(local.Y-progress*2.8f), 3.14f-progress*5.04f);
                yawChange = -90;
            }
            // Underground saves first reach the wide house layout, then follow
            // the same measured support changes as an already upstairs save.
            if (status.GetString() == "placed" && TryMoveCompactBabaiSupport(mapped, out var compactMapped))
                mapped = compactMapped;
            var target = room.ToGlobal(mapped);
            state ??= JsonNode.Parse(save.Runtime.State.GetRawText())!.AsObject();
            var changed = state["world.props"]![property.Name]!.AsObject();
            changed["x"] = target.X; changed["y"] = target.Y; changed["z"] = target.Z;
            if (record.TryGetProperty("yaw", out var yaw) && yaw.ValueKind == JsonValueKind.Number && yaw.TryGetSingle(out var degrees))
                changed["yaw"] = degrees + yawChange;
            movedItems++;
        }
        // Held records deliberately retain their custody and flags. The existing
        // carry projection computes their actual pose from the restored player.
        if (state is not null)
            save = save with { Runtime = save.Runtime with { State = JsonSerializer.SerializeToElement(state) } };
        if (movedPlayer || movedItems > 0)
            GD.Print($"act1-house-load-adapter: legacy-wing player={(movedPlayer ? 1 : 0)} placed-items={movedItems}");
        return save;
    }

    private static bool TryMoveCompactBabaiSupport(Vector3 old, out Vector3 destination)
    {
        destination = old;
        if (!old.IsFinite()) return false;
        // A placed origin is the object's bottom, .006 m above its support.
        // Only the vanished part of each old top qualifies: the intersection
        // of old/new tops stays untouched, making repeated loads idempotent.
        // Loose floor objects, held/combined records, stacks and edges do not
        // establish which furnishing supported an item and are not guessed.
        static bool OnTop(Vector3 p, float x0, float x1, float z0, float z1, float top)
            => p.X > x0+.02f && p.X < x1-.02f && p.Z > z0+.02f && p.Z < z1-.02f
                && Math.Abs(p.Y-top-.006f) < .004f;
        var shift = 0f;
        if (OnTop(old,-6.09f,-5.51f,-6.60f,-3.90f,.84f)) shift=.82f; // Physical cabinet top; worktop has no contact.
        else if (OnTop(old,-3.63f,-3.03f,-5.60f,-4.30f,.765f)) shift=.60f; // Kitchen table's retired strip.
        else if (OnTop(old,-6.39f,-5.89f,-7.15f,-6.05f,3.70f)) shift=1.20f; // Bedroom dresser.
        else if (OnTop(old,-2.75f,-2.33f,-7.11f,-6.69f,3.38f)) shift=.60f; // Bedside table.
        else if (OnTop(old,-4.15f,-3.55f,-5.03f,-4.53f,3.30f)) shift=.60f; // Chest's retired strip.
        else if (OnTop(old,-4.55f,-3.95f,-7.25f,-5.15f,3.14f)) shift=.60f; // Bed frame's retired strip.
        else if (OnTop(old,-5.20f,-4.60f,-1.10f,-.30f,3.56f)) shift=.60f; // Reading table's retired strip.
        if (shift == 0) return false;
        destination += Vector3.Right * shift;
        return true;
    }

    internal bool TryResolveCompactBabaiHouseFeet(FirstPersonController player, out Vector3 destination)
    {
        destination = player.GlobalPosition;
        if (ActiveZoneId != "house_old_pc" || player.VehicleControlled
            || !_zoneInstances.TryGetValue("house_old_pc", out var room)
            || room.GetNodeOrNull<Node3D>("NewBabaiHouseInterior") is not { } interior) return false;
        var p = room.ToLocal(destination);
        if (!p.IsFinite() || p.X < -6.55f || p.X > 6.5f || p.Z < -7.5f || p.Z > 3.5f
            || p.Y < -.10f || p.Y > 7.1f) return false;
        var space = player.GetWorld3D().DirectSpaceState;
        var exclude = new global::Godot.Collections.Array<Rid> { player.GetRid() };
        using var excludeOwner = (global::Godot.Collections.Array)exclude;
        bool HouseFloorSupports(Vector3 feet)
        {
            using var ray = PhysicsRayQueryParameters3D.Create(feet+Vector3.Up*.10f,
                feet-Vector3.Up*.20f,player.CollisionMask,exclude);
            using var hit = space.IntersectRay(ray);
            return hit.Count != 0 && hit["normal"].AsVector3().Dot(Vector3.Up) > .95f
                && hit["collider"].AsGodotObject() is StaticBody3D floor && interior.IsAncestorOf(floor)
                && (floor.Name.ToString().StartsWith("Floor", StringComparison.Ordinal) || floor.Name == "GroundFloor")
                && Math.Abs(hit["position"].AsVector3().Y-feet.Y) < .12f;
        }
        var removedFloor = p.X < BabaiHousePlan.West && !HouseFloorSupports(destination);
        var changedObstacle = false;
        if (!player.CanCrouchAt(destination)
            && player.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is { Shape: { } shape } capsule)
        {
            using var query = new PhysicsShapeQueryParameters3D { Shape=shape,Transform=capsule.GlobalTransform,
                CollisionMask=player.CollisionMask,Exclude=exclude,Margin=.002f };
            var hits = space.IntersectShape(query,32);
            using var hitsOwner = (global::Godot.Collections.Array)hits;
            foreach (var hit in hits)
            {
                if (hit["collider"].AsGodotObject() is not Node body) continue;
                var name=body.Name.ToString();
                if (name == "BabaiContinuousShellContacts" && p.X < BabaiHousePlan.West+player.BodyRadius+.05f
                    || room.IsAncestorOf(body) && (name is "KitchenCabinets" or "KitchenTableTop"
                        or "BedroomDresser" or "BedFrame" or "BedroomChest" or "BedsideTable"
                        || name.StartsWith("KitchenStoolContact",StringComparison.Ordinal)
                        || name == "FurnitureContacts" && body.GetParent()?.Name.ToString() == "UpperFamilyRoomFurniture"
                            && p.X < -2.7f && p.Z > -2.1f && p.Z < .8f)) changedObstacle=true;
            }
        }
        if (!removedFloor && !changedObstacle) return false;
        var candidates = p.Y < 2.5f
            ? new[] { StyleBenchmarkInteriorFactory.Entry, StyleBenchmarkInteriorFactory.WingKitchenSpawn, new Vector3(2.4f,.05f,1.5f) }
            : p.Y < 5.3f
                ? new[] { new Vector3(-2.7f,2.85f,-3.9f), new Vector3(1,2.85f,1), new Vector3(-2.3f,2.85f,1.4f) }
                : new[] { StyleBenchmarkInteriorFactory.WingAtticSpawn, new Vector3(0,5.65f,0), new Vector3(-2,5.65f,-3) };
        foreach (var local in candidates)
        {
            var candidate=room.ToGlobal(local);
            if (!player.CanStandAt(candidate) || !HouseFloorSupports(candidate)) continue;
            destination=candidate;
            return true;
        }
        if (removedFloor)
            throw new System.IO.InvalidDataException("The loaded house pose lost its old floor and no supported replacement is clear.");
        return false;
    }
}
