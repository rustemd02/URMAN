using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class Act1FacilitiesSmokeTest
{
    private async Task CheckMosqueDoorMigration(InteractionTarget entrance, Node3D hinge)
    {
        const string key = "mosque/entrance";
        var store = new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var codec = new SaveGameV3Codec();
        foreach (var open in new[] { false, true })
        {
            var phase = open ? "open" : "closed";
            if (Flag(key, "open") != open) await Use(entrance);
            await DoorAt(hinge, open ? -95 : 0, "attain ordinary inward mosque door " + phase + " before migration");
            var angle = hinge.Rotation.Y;
            var sourceSlot = "facilities-mosque-door-v1-" + phase;
            Check(await _bridge.SaveSlotAsync(sourceSlot), "save attained version-1 mosque door " + phase);
            var source = codec.Decode(await File.ReadAllBytesAsync(store.SlotPath(sourceSlot)));
            Check(Number(key, "geometryVersion") == 1, "mosque angle and geometry version use one normal save transaction");

            // Change only the representation of this physically attained pose;
            // the existing save codec preserves its player, items and knowledge.
            var state = JsonNode.Parse(source.Runtime.State.GetRawText())!.AsObject();
            var door = state["world.props"]![key]!.AsObject();
            door["angle"] = -angle;
            door.Remove("geometryVersion");
            var legacy = source with { Runtime = source.Runtime with { State = JsonSerializer.SerializeToElement(state) } };
            var legacySlot = "facilities-mosque-door-legacy-" + phase;
            if (File.Exists(store.SlotPath(legacySlot))) throw new IOException("Refusing to replace " + legacySlot);
            await store.SaveAsync(legacySlot, legacy);
            var digest = SHA256.HashData(await File.ReadAllBytesAsync(store.SlotPath(legacySlot)));
            _events.Add(new { kind = "legacy-mosque-door-representation-fixture", sourceSlot, legacySlot,
                open, oldAngle = -angle, expectedAngle = angle,
                scope = "only attained door angle representation; no invented gameplay progress" });
            for (var repeat = 0; repeat < 2; repeat++)
            {
                await Load(legacySlot);
                Check(Flag(key, "open") == open && Math.Abs(hinge.Rotation.Y - angle) < .002f
                    && Number(key, "geometryVersion") == 0 && _player.IsOnFloor(),
                    $"legacy mosque {phase} load {repeat + 1} projects inward once without rewriting the snapshot");
                var bytesAfterLoad = await File.ReadAllBytesAsync(store.SlotPath(legacySlot));
                Check(digest.AsSpan().SequenceEqual(SHA256.HashData(bytesAfterLoad)),
                    "mosque migration leaves the original legacy slot bytes intact");
            }
            var migratedSlot = "facilities-mosque-door-migrated-" + phase;
            Check(await _bridge.SaveSlotAsync(migratedSlot), "normal mosque save publishes projected angle and version together");
            var migrated = codec.Decode(await File.ReadAllBytesAsync(store.SlotPath(migratedSlot)));
            var savedDoor = migrated.Runtime.State.GetProperty("world.props").GetProperty(key);
            Check(savedDoor.GetProperty("geometryVersion").GetInt32() == 1
                && Math.Abs(savedDoor.GetProperty("angle").GetDouble() - angle) < .002,
                "new negative mosque angle is explicitly versioned and cannot migrate twice");
            for (var repeat = 0; repeat < 2; repeat++)
            {
                await Load(migratedSlot);
                Check(Flag(key, "open") == open && Math.Abs(hinge.Rotation.Y - angle) < .002f
                    && Number(key, "geometryVersion") == 1 && _player.IsOnFloor(),
                    $"version-1 mosque {phase} load {repeat + 1} retains its inward physical pose");
            }
        }
    }

    private async Task CheckBathDoorMigration(InteractionTarget entrance, Node3D hinge)
    {
        var store = new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var codec = new SaveGameV3Codec();
        foreach (var open in new[] { false, true })
        {
            var phase = open ? "open" : "closed";
            if (Flag("bathhouse/entrance", "open") != open) await Use(entrance);
            await DoorAt(hinge, open ? 82 : 180, "attain ordinary " + phase + " door before representation migration");
            var angle = hinge.Rotation.Y;
            var sourceSlot = "facilities-door-v2-" + phase;
            Check(await _bridge.SaveSlotAsync(sourceSlot), "save attained version-2 " + phase + " door");
            var source = codec.Decode(await File.ReadAllBytesAsync(store.SlotPath(sourceSlot)));
            Check(Number("bathhouse/entrance", "geometryVersion") == 2,
                "new door pose and geometry version share the existing save transaction");
            foreach (var legacyVersion in new[] { 0, 1 })
            {
                // This fixture changes only the representation of an attained
                // door pose. It introduces no items, knowledge, story flags or
                // player transform; the production codec/store own its file.
                var state = JsonNode.Parse(source.Runtime.State.GetRawText())!.AsObject();
                var door = state["world.props"]!["bathhouse/entrance"]!.AsObject();
                door["angle"] = Mathf.Pi - angle;
                if (legacyVersion == 0) door.Remove("geometryVersion");
                else door["geometryVersion"] = legacyVersion;
                var legacy = source with { Runtime = source.Runtime with { State = JsonSerializer.SerializeToElement(state) } };
                var legacySlot = $"facilities-door-legacy{legacyVersion}-{phase}";
                if (File.Exists(store.SlotPath(legacySlot))) throw new IOException("Refusing to replace " + legacySlot);
                await store.SaveAsync(legacySlot, legacy);
                var digest = SHA256.HashData(await File.ReadAllBytesAsync(store.SlotPath(legacySlot)));
                _events.Add(new { kind = "legacy-door-representation-fixture", sourceSlot, legacySlot,
                    legacyVersion, open, oldAngle = Mathf.Pi - angle, expectedAngle = angle,
                    scope = "only attained door angle representation; no invented gameplay progress" });
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    await Load(legacySlot);
                    Check(Flag("bathhouse/entrance", "open") == open && Math.Abs(hinge.Rotation.Y - angle) < .002f
                        && Number("bathhouse/entrance", "geometryVersion") == legacyVersion && _player.IsOnFloor(),
                        $"legacy {phase} load {repeat + 1} projects once without rewriting its snapshot");
                    var legacyBytesAfterLoad = await File.ReadAllBytesAsync(store.SlotPath(legacySlot));
                    Check(digest.AsSpan().SequenceEqual(SHA256.HashData(legacyBytesAfterLoad)),
                        "read-only migration leaves the original legacy slot bytes intact");
                }
                var migratedSlot = $"facilities-door-migrated{legacyVersion}-{phase}";
                Check(await _bridge.SaveSlotAsync(migratedSlot), "ordinary save publishes migrated pose and version together");
                var migrated = codec.Decode(await File.ReadAllBytesAsync(store.SlotPath(migratedSlot)));
                var migratedDoor = migrated.Runtime.State.GetProperty("world.props").GetProperty("bathhouse/entrance");
                Check(migratedDoor.GetProperty("geometryVersion").GetInt32() == 2
                    && Math.Abs(migratedDoor.GetProperty("angle").GetDouble() - angle) < .002,
                    "saved version-2 angle cannot be mistaken for an unversioned legacy angle");
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    await Load(migratedSlot);
                    Check(Flag("bathhouse/entrance", "open") == open && Math.Abs(hinge.Rotation.Y - angle) < .002f
                        && Number("bathhouse/entrance", "geometryVersion") == 2 && _player.IsOnFloor(),
                        $"version-2 {phase} load {repeat + 1} keeps the same physical door pose");
                }
            }
        }
        await Use(entrance);
        await DoorAt(hinge, 180, "close the normally operated door after old/new save migration cases");
    }
}
