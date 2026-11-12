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
                && record.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String && state.GetString() is ("placed" or "combined")
                && record.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number && y.TryGetSingle(out var height)
                && float.IsFinite(height) && height < -20f) return true;
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
            if (!Legacy(oldLocal)) continue;
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
}
