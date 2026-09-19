using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private async Task CheckPlayerFootwear()
    {
        var target = Target(Act1ConnectedWorld.PlayerFootwearInteraction);
        var initialKnowledge = _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        Check(!_carry.ActionInProgress && _carry.HeldItem is null && _player.FootwearPresentationReady,
            "shoe-bench episode starts after the ordinary visit with free hands and the existing player body");
        Check(!_bridge.SelectWorldProps().TryGetProperty(Act1ConnectedWorld.PlayerFootwearKey, out _),
            "a pre-footwear save contains no new footwear property");
        CheckPlayerFootwearProjection(false, "old/default state");
        const string baseline = "facilities-footwear-legacy";
        const string removedSlot = "facilities-footwear-removed";
        Check(await _bridge.SaveSlotAsync(baseline), "save the attained indoor visit before any shoe action");
        var store = typeof(RuntimeBridge).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bridge)
            as AtomicSaveGameStore ?? throw new InvalidOperationException("The ordinary footwear test requires its existing session store.");
        var baselineBytes = await File.ReadAllBytesAsync(store.SlotPath(baseline));

        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, -.30f)), "walk away from the shoe bench before the remote attempt");
        var actions = target.GetMeta("footwearActionNumber", 0).AsInt32();
        Aim(target.GlobalPosition); await Frames(3);
        Check(!AimedAt(target), "the distant bench is outside the actual 2.7m interaction ray");
        await Press("interact");
        Check(target.GetMeta("footwearActionNumber", 0).AsInt32() == actions && !_world.PlayerFootwearRemoved,
            "a distant mapped press cannot remove shoes or deposit a remote pair");
        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, 1.50f)), "return to the existing bench through the vestibule");

        // Exercise a genuinely pending ordinary transaction. The extra invocation
        // is an explicitly recorded reentrant API attempt, not a second player
        // click or an injected _facilityBusy flag; the first action comes from E.
        var original = target.PresentationRepeat ?? throw new InvalidOperationException("Shoe bench has no production action.");
        var busyObserved = false; var busyResult = string.Empty;
        try
        {
            target.PresentationRepeat = () =>
            {
                original();
                busyObserved = !FacilityIdle();
                if (busyObserved)
                {
                    original();
                    busyResult = target.GetMeta("footwearActionResult", "missing").AsString();
                }
            };
            await Use(target);
        }
        finally { target.PresentationRepeat = original; }
        Check(busyObserved && busyResult == "busy" && target.GetMeta("footwearActionNumber", 0).AsInt32() == actions + 2,
            "reentrant use during the real pending save is refused while the one ordinary removal finishes");
        _events.Add(new { kind = "footwear-busy-refusal", busyObserved, busyResult,
            firstAction = "mapped interaction", extraAttempt = "explicit reentrant production callback", injectedFlags = 0 });
        CheckPlayerFootwearProjection(true, "one committed removal");
        await CapturePlayerFootwear("09f_player_socks");
        await Capture("09f_player_boots_on_shelf", _mosque.ToGlobal(new(3.65f, .20f, 3.44f)));
        Check(await _bridge.SaveSlotAsync(removedSlot), "save the attained sock-and-shelf state through the normal store");
        var removedBytes = await File.ReadAllBytesAsync(store.SlotPath(removedSlot));

        await Load(baseline); CheckPlayerFootwearProjection(false, "legacy slot restored");
        for (var repeat = 0; repeat < 2; repeat++)
        {
            await Load(removedSlot); CheckPlayerFootwearProjection(true, "removed slot restored " + repeat);
        }
        Check((await File.ReadAllBytesAsync(store.SlotPath(baseline))).SequenceEqual(baselineBytes)
            && (await File.ReadAllBytesAsync(store.SlotPath(removedSlot))).SequenceEqual(removedBytes),
            "load and repeated projection rewrite neither source slot and duplicate no pair");

        actions = target.GetMeta("footwearActionNumber", 0).AsInt32();
        await Use(target);
        Check(target.GetMeta("footwearActionNumber", 0).AsInt32() == actions + 1,
            "one further explicit use puts the same boots back on");
        CheckPlayerFootwearProjection(false, "ordinary wear action");
        await CapturePlayerFootwear("09f_player_boots_restored");

        // Same disposable failing-store pattern as the existing notebook proof.
        // The live committed fact and both projections survive a failed disk write.
        var storeField = typeof(RuntimeBridge).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var faultRoot = ProjectSettings.GlobalizePath("user://footwear-write-fault-" + Guid.NewGuid().ToString("N"));
        var blocked = Path.Combine(faultRoot, "not-a-directory");
        Directory.CreateDirectory(faultRoot);
        try
        {
            await File.WriteAllTextAsync(blocked, "test-owned regular file");
            storeField.SetValue(_bridge, new AtomicSaveGameStore(blocked));
            await Use(target);
            CheckPlayerFootwearProjection(true, "live removal after checkpoint write refusal");
            Check(!await _bridge.SaveSlotAsync("footwear-write-denied"), "explicit save reports the actual test-owned disk failure");
            Check((await File.ReadAllBytesAsync(store.SlotPath(baseline))).SequenceEqual(baselineBytes)
                && (await File.ReadAllBytesAsync(store.SlotPath(removedSlot))).SequenceEqual(removedBytes),
                "failed save touches neither earlier real slot");
        }
        finally
        {
            storeField.SetValue(_bridge, store);
            if (File.Exists(blocked)) File.Delete(blocked);
            if (Directory.Exists(faultRoot)) Directory.Delete(faultRoot);
        }
        await CheckFootwearPhysicalRollback(baseline);
        await Load(baseline);

        var lamp = _carry.Items.Single(item => item.ItemId == "carry-lantern");
        Check(lamp.Class != CarryableProp.ItemClass.Bulky, "the held-light fixture uses the ordinary interaction passthrough");
        await ApproachCarry(lamp); await Press("interact"); await _carry.PendingAction;
        Check(_carry.HeldItem == lamp, "the occupied-hands refusal uses the real acquired portable lantern");
        await LocalStart(_mosque.ToGlobal(new(4.30f, .035f, 1.50f)), "explicit shoe-bench refusal fixture with the normally acquired lantern");
        Aim(target.GlobalPosition); await Frames(4);
        Check(_carry.HasValidHeldPose && AimedAt(target), "held lantern has a real pose and leaves the bench interaction ray clear");
        var heldProps = _bridge.SelectWorldProps().GetRawText();
        await Use(target);
        Check(_carry.HeldItem == lamp && _bridge.SelectWorldProps().GetRawText() == heldProps
            && target.GetMeta("footwearActionResult", "missing").AsString().Contains("освободите руки", StringComparison.Ordinal),
            "occupied hands refuse shoe removal without dropping the object or changing its custody");
        CheckPlayerFootwearProjection(false, "occupied hands");
        await Load(removedSlot);

        // Leaving a room is not an automatic wardrobe action or a religious gate.
        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, 0)), "return in socks to the normal doorway approach");
        await WalkTo(_mosque.ToGlobal(new(6.65f, 0, -.25f)), "walk out normally while the saved shoes remain on their shelf");
        CheckPlayerFootwearProjection(true, "exterior state remains explicit");
        await WalkTo(_mosque.ToGlobal(new(4.65f, 0, -.25f)), "return through the same open doorway without an automatic shoe change");
        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, 1.50f)), "return to the shoe bench");
        CheckPlayerFootwearProjection(true, "return preserves the explicit removal");
        await CheckLegacyMosqueCarpetSupport(baseline, store);
        Check(_bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText() == initialKnowledge,
            "footwear actions, failures and returning add no narrative knowledge");
        Check(await _bridge.StartNewGameAsync(), "ordinary New Game resets the single footwear fact");
        await Frames(10); CheckPlayerFootwearProjection(false, "new game");
        await Load(baseline); CheckPlayerFootwearProjection(false, "restore the attained visit for the remaining facility proof");
    }

    private async Task CheckLegacyMosqueCarpetSupport(string baseline, AtomicSaveGameStore store)
    {
        await Load(baseline);
        CheckPlayerFootwearProjection(false, "legacy hall fixture starts from the saved visit without a footwear property");
        var carpet = _mosque.GetNode<MeshInstance3D>("MosquePrayerCarpet");
        var contact = _mosque.GetNode<StaticBody3D>("MosquePrayerCarpetBody").GetNode<CollisionShape3D>("Contact");
        Check(!contact.Disabled, "legacy support fixture starts with the real new carpet contact active");
        var top = carpet.ToGlobal(carpet.Mesh.GetAabb().GetCenter() + Vector3.Up * carpet.Mesh.GetAabb().Size.Y * .5f).Y;
        const string oldHallSlot = "facilities-legacy-timber-hall";
        var savedFeet = Vector3.Zero;
        byte[] savedBytes = Array.Empty<byte>();
        try
        {
            // Reproduce only the previously shipped support geometry: the rug
            // was visible, and its new paired shape did not exist. Reach the
            // retained timber with normal input before making an ordinary save.
            contact.Disabled = true;
            await Frames(3);
            await WalkTo(_mosque.ToGlobal(new(3.65f, 0, 0)), "legacy support fixture walks through the vestibule opening");
            await WalkTo(_mosque.ToGlobal(new(1.20f, 0, 0)), "legacy support fixture reaches the original timber beneath the rug");
            savedFeet = _player.GlobalPosition;
            using var ray = PhysicsRayQueryParameters3D.Create(savedFeet + Vector3.Up * .10f, savedFeet - Vector3.Up * .15f,
                3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
            var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            Check(hit.Count > 0 && hit["collider"].AsGodotObject() == _mosque.GetNode<StaticBody3D>("MosqueTimberFloorBody")
                && Math.Abs(savedFeet.Y - hit["position"].AsVector3().Y) < .001f && _player.IsOnFloor(),
                "the old-support save is physically reached on the preserved timber, without editing player coordinates or flags");
            Check(await _bridge.SaveSlotAsync(oldHallSlot), "save the normally reached pre-carpet-support position");
            savedBytes = await File.ReadAllBytesAsync(store.SlotPath(oldHallSlot));
            await WalkTo(_mosque.ToGlobal(new(3.65f, 0, 0)), "leave the changed support area before restoring its current contact");
        }
        finally { contact.Disabled = false; await Frames(3); }

        for (var repeat = 0; repeat < 2; repeat++)
        {
            Check(await _bridge.LoadSlotAsync(oldHallSlot), "load the actual old timber-support save against the current carpet: " + repeat);
            await Frames(6);
            var actual = _player.GlobalPosition;
            var horizontal = new Vector2(actual.X - savedFeet.X, actual.Z - savedFeet.Z).Length();
            Check(!_bridge.NeedsPhysicalRecovery && !contact.Disabled && horizontal < .001f
                && Math.Abs(actual.Y - top) < .001f && _player.IsOnFloor() && _player.CanStandAt(actual),
                "the bounded physical migration preserves horizontal placement and lands on the exact visible rug without cumulative lift");
            CheckPlayerFootwearProjection(false, "old hall load retains default footwear " + repeat);
            Check((await File.ReadAllBytesAsync(store.SlotPath(oldHallSlot))).SequenceEqual(savedBytes),
                "projecting the old support does not rewrite its saved bytes");
            _events.Add(new { kind = "legacy-mosque-carpet-support", repeat, savedFeet = savedFeet.ToString(),
                loadedFeet = actual.ToString(), horizontal, carpetTop = top, lifted = actual.Y - savedFeet.Y,
                fixture = "only the newly added carpet contact was temporarily disabled for an ordinary reached/save state",
                savedFlagsEdited = false, currentContactRestored = !contact.Disabled, slotUnchanged = true });
        }
        await Load(baseline);
    }

    private async Task CheckFootwearPhysicalRollback(string oldSlot)
    {
        var savedFeet = _player.GlobalPosition;
        await WalkTo(_mosque.ToGlobal(new(4.30f, 0, -.30f)), "walk away before obstructing only the old saved stance");
        var freeFeet = _player.GlobalPosition;
        var session = _bridge.SessionIdentity;
        using var shape = new BoxShape3D { Size = new(.85f, 2.2f, .85f) };
        var blocker = new StaticBody3D { Name = "FootwearBlockedSavedFeet", CollisionLayer = 1, CollisionMask = 0 };
        blocker.AddChild(new CollisionShape3D { Shape = shape }); AddChild(blocker);
        blocker.GlobalPosition = savedFeet + Vector3.Up * 1.1f;
        try
        {
            await Frames(3);
            Check(!_player.CanCrouchAt(savedFeet) && _player.CanStandAt(freeFeet),
                "test obstacle blocks the old saved capsule while the walked rollback position remains clear");
            Check(!await _bridge.LoadSlotAsync(oldSlot), "the actual physical load rejects the blocked earlier stance");
            await Frames(4);
            Check(!_bridge.NeedsPhysicalRecovery && _bridge.SessionIdentity is not null
                && !ReferenceEquals(session, _bridge.SessionIdentity) && _player.GlobalPosition.DistanceTo(freeFeet) < .08f,
                "failed physical load recreates the prior live session at its actual free position");
            CheckPlayerFootwearProjection(true, "physical rollback returns the live removed pair after projecting an older worn state");
            await Capture("09f_player_footwear_after_rollback", _mosque.ToGlobal(new(3.65f, .20f, 3.44f)));
        }
        finally { blocker.QueueFree(); await Frames(3); }
    }

    private void CheckPlayerFootwearProjection(bool removed, string phase)
    {
        var records = _player.FootwearPresentation;
        Check(records.Count == 2 && _player.VisibleBodyMeshCount == 6 && _player.IndoorFootwearActive == removed
            && _world.PlayerFootwearRemoved == removed, phase + ": same body and single projected footwear choice");
        foreach (var item in records)
        {
            Check(item.Node.Mesh == (removed ? item.Indoor : item.Boots) && item.Node.Skin == item.Skin
                && item.Node.Skeleton == item.Skeleton && item.Node.Transform.IsEqualApprox(item.LocalPose),
                phase + ": the existing foot node retains its final ankle skin and local transform");
            var ankleBindings = Enumerable.Range(0, item.Skin.GetBindCount()).Select(item.Skin.GetBindName).Select(name => name.ToString());
            Check(ankleBindings.Any(name => name.StartsWith("AidarAnkle", StringComparison.Ordinal)), phase + ": articulated ankle binding survives");
            var bounds = item.Node.Mesh.GetAabb(); var original = item.Boots.GetAabb();
            Check(Math.Abs(bounds.Position.Y - original.Position.Y) < .00001f && Math.Abs(bounds.End.Y - original.End.Y) < .00001f,
                phase + ": sole plane and top cuff keep their original height");
        }
        var pair = _world.StoredPlayerBoots;
        Check(pair.Count == 2 && pair.Select(item => item.GetInstanceId()).Distinct().Count() == 2
            && pair.All(item => item.Visible && item.GetParent<Node3D>().Visible == removed && item.Skin is null),
            phase + ": one static pair on the shelf agrees with the two feet");
        foreach (var item in pair)
        {
            var bounds = item.Mesh.GetAabb();
            foreach (var x in new[] { -.052f, .052f }) foreach (var z in new[] { -.10f, .10f })
            {
                var at = item.ToGlobal(new(x, bounds.Position.Y, z));
                using var ray = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * .006f, at - Vector3.Up * .025f, 3,
                    new global::Godot.Collections.Array<Rid> { _player.GetRid() });
                var hit = _world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Check(hit.Count > 0 && (hit["collider"].AsGodotObject() as Node)?.Name == "MosqueShoeShelf0Body"
                    && Math.Abs(at.Y - hit["position"].AsVector3().Y) < .002f,
                    phase + ": stored boot rests on the exact original lower shelf");
            }
        }
        _events.Add(new { kind = "player-footwear-projection", phase, removed,
            feet = records.Select(item => new { name = item.Node.Name.ToString(), mesh = item.Node.Mesh.ResourceName,
                skin = item.Node.Skin.GetInstanceId(), local = item.Node.Transform.ToString() }).ToArray(),
            shelfPair = pair.Select(item => new { name = item.Name.ToString(), id = item.GetInstanceId(), pose = item.GlobalTransform.ToString() }).ToArray() });
    }

    private async Task CapturePlayerFootwear(string name)
    {
        var look = _player.CapturePortableTransform();
        try { await Capture(name, _player.GlobalPosition - _player.GlobalBasis.Z * .64f + Vector3.Up * .04f); }
        finally { _player.ApplySmokeLook((float)look.RotationDegrees.X, (float)look.RotationDegrees.Y); }
    }
}
