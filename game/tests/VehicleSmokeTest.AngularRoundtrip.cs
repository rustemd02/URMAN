using Godot;
using System.Text.Json.Nodes;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task RepeatedIndoorAngularRoundtrips(string originalSlot)
    {
        Require(_bridge.CurrentZoneId=="house_old_pc"&&_fleet.Occupied is null
            &&_fleet.PlacementValidationDeferred,
            "repeated angle serialization starts in the attained, unoccupied indoor visit");
        var companion=AlsuStreetWalkPresentation.SessionOwner(GetTree())
            ??throw new InvalidOperationException("The attained indoor session has no Alsu pose owner.");
        var ids=_fleet.Vehicles.Select(vehicle=>vehicle.Definition.StateId)
            .Append(AlsuStreetWalkPresentation.SavePropId).ToArray();
        JsonObject OwnerRecords()
        {
            var props=_bridge.SelectWorldProps();var records=new JsonObject();
            foreach(var id in ids)
            {
                Require(props.TryGetProperty(id,out var record),"saved pose record exists: "+id);
                records[id]=JsonNode.Parse(record.GetRawText());
            }
            return records;
        }
        var original=OwnerRecords();
        var live=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture());
        var transforms=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.GlobalTransform);
        var actorTransform=companion.Actor.GlobalTransform;
        var store=new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var protectedFiles=new[]{store.SlotPath(originalSlot),store.BackupPath(originalSlot),"user://settings.json"};
        var hashes=protectedFiles.ToDictionary(path=>path,FileDigest);
        for(var cycle=1;cycle<=3;cycle++)
        {
            var slot="vehicle-angular-roundtrip-"+cycle;
            Require(await _bridge.SaveSlotAsync(slot),"ordinary indoor save for angle roundtrip "+cycle);
            var afterSave=OwnerRecords();
            Require(await _bridge.LoadSlotAsync(slot),"ordinary indoor load for angle roundtrip "+cycle);
            await Frames(4);
            companion=AlsuStreetWalkPresentation.SessionOwner(GetTree())
                ??throw new InvalidOperationException("The loaded indoor session lost Alsu's pose owner.");
            var afterLoad=OwnerRecords();
            var actual=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture());
            var ownersExact=JsonNode.DeepEquals(original,afterSave)&&JsonNode.DeepEquals(original,afterLoad);
            var liveExact=_fleet.Vehicles.All(vehicle=>JsonNode.DeepEquals(live[vehicle.Definition.Id],actual[vehicle.Definition.Id]));
            var posesExact=_fleet.Vehicles.All(vehicle=>vehicle.GlobalTransform==transforms[vehicle.Definition.Id])
                &&companion.Actor.GlobalTransform==actorTransform;
            var filesExact=protectedFiles.All(path=>FileDigest(path)==hashes[path]);
            _records.Add(new{kind="repeated-indoor-angle-roundtrip",cycle,slot,original,afterSave,afterLoad,
                liveBefore=live,liveAfter=actual,ownersExact,liveExact,posesExact,filesExact,
                vehicleTransforms=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>new
                    {before=transforms[vehicle.Definition.Id].ToString(),after=vehicle.GlobalTransform.ToString()}),
                alsuTransformBefore=actorTransform.ToString(),alsuTransformAfter=companion.Actor.GlobalTransform.ToString(),
                files=protectedFiles.Select(path=>new{path,beforeSha256=hashes[path],afterSha256=FileDigest(path)}).ToArray(),
                limit="three ordinary save/load cycles; full owner records and actual transforms exact; no rounded angle oracle"});
            Require(ownersExact&&liveExact&&posesExact,
                "repeated indoor load preserves all vehicle and Alsu pose records and actual transforms exactly: "+cycle);
            Require(filesExact,"angle roundtrip writes only its own debug slot and preserves original source/profile: "+cycle);
            Require(_bridge.CurrentZoneId=="house_old_pc"&&!_bridge.NeedsPhysicalRecovery
                &&_fleet.PlacementValidationDeferred&&!_player.VehicleControlled&&!_player.ModalOpen
                &&_player.CanCrouchAt(_player.GlobalPosition),
                "angle roundtrip releases ordinary indoor control at actual clear feet: "+cycle);
        }
    }
}
