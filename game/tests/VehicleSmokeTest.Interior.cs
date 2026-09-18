using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task InteriorParkingSurvivesSaveLoad()
    {
        const string prefix="urman.chapter1:";
        const string slot="vehicle-interior-parking";
        Require(_fleet.Occupied is null&&_bridge.CurrentZoneId=="village_day",
            "interior parking proof begins with an ordinary unoccupied exterior fleet");
        var entryVehicle=_fleet.Vehicles[0];
        await Approach(entryVehicle);
        var entryFeet=_player.GlobalPosition;
        var entryYaw=_player.RotationDegrees.Y;
        var parked=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture());

        // The existing source and dialogue helpers earn the real house route.
        // Main.SwitchZone then uses the same physical presentation path as its
        // ordinary doorway caller; no knowledge or saved pose is manufactured.
        await Act1ArrivalFlowProof.CompleteAsync(this,_bridge);
        Require(await _bridge.DispatchInteractionAsync(prefix+"interaction/arrival-enter-house"),
            "interior parking proof earns house entry from the actual arrival sources");
        _demo.DemoMain.SwitchZone("house_old_pc","entry");
        await Frames(4);
        Require(_fleet.PlacementValidationDeferred&&_fleet.Vehicles.All(vehicle=>!vehicle.PlacementAvailable),
            "indoor presentation defers exterior placement checks for all three vehicles");
        foreach(var vehicle in _fleet.Vehicles)
        {
            var before=parked[vehicle.Definition.Id];var after=vehicle.Capture();
            foreach(var key in new[]{"position","yawDegrees","engineRunning","parkingBrake","headlights","travelMetres"})
                Require(JsonNode.DeepEquals(before[key],after[key]),
                    "house entry preserves parked "+key+" for "+vehicle.Definition.Id);
        }
        Require(await _bridge.EnterDialogueNodeAsync(prefix+"dialogue/mansur_pc_request","ask-for-help")
            &&await _bridge.ChooseDialogueAsync(prefix+"dialogue/mansur_pc_request","ask-for-help","offer-help"),
            "the house visit retains Mansur's ordinary computer request");
        await _bridge.HandleOldPcInputAsync(JsonSerializer.SerializeToElement(new
            {type="open",documentId="urman.oldpc:document/doc_marat_official_death_notice"}));
        Require(await _bridge.EnterDialogueNodeAsync(prefix+"dialogue/gulsina_yaramyy","home-warning")
            &&await _bridge.ChooseDialogueAsync(prefix+"dialogue/gulsina_yaramyy","home-warning","ask-marat")
            &&await Act1FamilyMealProof.CompleteAsync(this,_bridge)
            &&_bridge.IsInteractionAvailable(prefix+"interaction/house-to-route"),
            "the computer source and family conversation earn the real house exit");
        await Frames(4);
        var inside=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture());
        var rejectionBefore=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,
            vehicle=>vehicle.HasMeta("rejectedPlacementProbe")?vehicle.GetMeta("rejectedPlacementProbe").AsString():null);
        Require(await _bridge.SaveSlotAsync(slot),"the sole save owner saves the attained indoor visit");
        var savedParking=_bridge.SelectWorldProps().Clone();
        Require(await _bridge.LoadSlotAsync(slot),"an indoor snapshot loads without querying disabled exterior terrain");
        await Frames(6);
        Require(_bridge.CurrentZoneId=="house_old_pc"&&_fleet.PlacementValidationDeferred
            &&_fleet.ProjectionFailure is null&&_fleet.Occupied is null
            &&!_player.VehicleControlled&&!_player.ModalOpen&&!_fleet.Suspended,
            "indoor load releases the ordinary player and leaves only vehicle placement deferred");
        foreach(var vehicle in _fleet.Vehicles)
        {
            var before=inside[vehicle.Definition.Id];var after=vehicle.Capture();
            var differentKeys=before.Select(field=>field.Key).Union(after.Select(field=>field.Key))
                .Where(key=>!JsonNode.DeepEquals(before[key],after[key])).ToArray();
            var yawPreserved=ParkedYawWithinSerializationPrecision(before["yawDegrees"],after["yawDegrees"],
                out var yawErrorDegrees,out var yawLimitDegrees);
            _records.Add(new{kind="indoor-parked-state-roundtrip",vehicle=vehicle.Definition.Id,
                before,after,differentKeys,yawErrorDegrees,yawLimitDegrees,
                saved=savedParking.TryGetProperty(vehicle.Definition.StateId,out var savedVehicle)
                    ?JsonNode.Parse(savedVehicle.GetRawText()):null,
                actualTransform=vehicle.GlobalTransform.ToString(),
                limit="all other fields exact; yaw allows at most two adjacent float values through Godot's degrees/radians conversion"});
            Require(yawPreserved&&differentKeys.All(key=>key=="yawDegrees"),
                "indoor save-load preserves parked orientation within float serialization precision and every other state field exactly for "+vehicle.Definition.Id);
            Require((vehicle.HasMeta("rejectedPlacementProbe")?vehicle.GetMeta("rejectedPlacementProbe").AsString():null)
                ==rejectionBefore[vehicle.Definition.Id],
                "disabled terrain produces no new rejection probe for "+vehicle.Definition.Id);
        }
        await RepeatedIndoorAngularRoundtrips(slot);
        await RejectBlockedIndoorFeet(slot);
        var session=_bridge.SessionIdentity;
        Require(await _bridge.DispatchInteractionAsync(prefix+"interaction/house-to-route"),
            "return uses the earned house exit interaction");
        _demo.DemoMain.SwitchZone("village_day","from_house");
        Require(ReferenceEquals(session,_bridge.SessionIdentity)&&_fleet.PlacementValidationDeferred
            &&_fleet.Vehicles.All(vehicle=>!vehicle.PlacementAvailable),
            "exterior return retains the same session and blocks entry until collision propagation");
        // Reuse the actually clear approach already checked above. This local
        // coordinate fixture isolates the synchronous entry guard from travel.
        _player.ApplyZoneSpawn(entryFeet,entryYaw);
        Require(_player.GlobalPosition.DistanceTo(entryVehicle.EntryTarget.GlobalPosition)<2.8f
            &&!_fleet.TryEnter(entryVehicle)&&_fleet.Occupied is null,
            "a nearby vehicle cannot be entered on the collision-switch frame");
        await Frames(5);
        Require(!_fleet.PlacementValidationDeferred&&_fleet.ProjectionFailure is null,
            "exterior physics completes the deferred placement validation");
        foreach(var vehicle in _fleet.Vehicles)
        {
            Require(vehicle.PlacementAvailable&&vehicle.ValidatePhysicalPlacement(out _),
                "actual exterior support and hull are valid again for "+vehicle.Definition.Id);
            var before=inside[vehicle.Definition.Id];var after=vehicle.Capture();
            foreach(var key in new[]{"position","yawDegrees","engineRunning","parkingBrake","headlights","travelMetres"})
                Require(key=="yawDegrees"
                    ?ParkedYawWithinSerializationPrecision(before[key],after[key],out _,out _)
                    :JsonNode.DeepEquals(before[key],after[key]),
                    "exterior revalidation preserves live "+key+" for "+vehicle.Definition.Id);
            await Approach(vehicle);await Press("interact");
            Require(_fleet.Occupied==vehicle&&vehicle.Driver==_player,
                "the restored parked "+vehicle.Definition.Id+" admits the actual mapped interaction");
            Require(vehicle.TryExit(),"the restored parked "+vehicle.Definition.Id+" still has a safe exit");
            await Frames(4);
        }
        _records.Add(new{kind="interior-fleet-save-load-return",slot,zone="house_old_pc",
            attainedHouseVisit=true,parkedStatePreserved=true,exteriorValidationDeferred=true,
            returnedThroughEarnedExit=true,allThreeVehiclesEnteredAndExited=true,
            limit="source/dialogue and checked local approach fixtures; not a human route or transport art acceptance"});
    }

    private static bool ParkedYawWithinSerializationPrecision(JsonNode? before,JsonNode? after,
        out double errorDegrees,out double limitDegrees)
    {
        // Vehicle09 records a single-float-step change, 1.6026496 -> 1.6026495,
        // after the real RotationDegrees setter/getter. Keep all non-angular
        // fields byte-equivalent and bound this numeric conversion by two ULPs.
        var expected=before?.GetValue<float>()??float.NaN;
        var actual=after?.GetValue<float>()??float.NaN;
        errorDegrees=Math.Abs(Math.IEEERemainder((double)actual-expected,360.0));
        limitDegrees=2*Math.Max(Math.Abs((double)MathF.BitIncrement(expected)-expected),
            Math.Abs((double)MathF.BitIncrement(actual)-actual));
        return float.IsFinite(expected)&&float.IsFinite(actual)
            &&double.IsFinite(errorDegrees)&&double.IsFinite(limitDegrees)&&errorDegrees<=limitDegrees;
    }

    private async Task RejectBlockedIndoorFeet(string slot)
    {
        var savedFeet=_player.GlobalPosition;
        var savedYaw=_player.RotationDegrees.Y;
        var space=_player.GetWorld3D().DirectSpaceState;
        var exclusions=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
        using var ray=PhysicsRayQueryParameters3D.Create(Vector3.Zero,Vector3.Down,1,exclusions);
        Vector3? previousFeet=null;
        foreach(var offset in new[]{new Vector3(1.4f,0,0),new(-1.4f,0,0),new(0,0,1.4f),new(0,0,-1.4f),
            new(1.4f,0,1.4f),new(-1.4f,0,-1.4f),new(1.4f,0,-1.4f),new(-1.4f,0,1.4f)})
        {
            var candidate=savedFeet+offset;
            if(!_player.CanStandAt(candidate))continue;
            ray.From=candidate+Vector3.Up*.12f;ray.To=candidate+Vector3.Down*.18f;
            var hit=space.IntersectRay(ray);
            if(hit.Count==0||hit["normal"].AsVector3().Y<.75f)continue;
            previousFeet=candidate;break;
        }
        Require(previousFeet is not null,"blocked indoor load has a separate actually supported standing position");
        // A local placement fixture changes only the player's actual pose. Both
        // positions are ordinary supported indoor space, not fabricated state.
        _player.ApplyZoneSpawn(previousFeet!.Value,savedYaw);await Frames(3);
        var freeFeet=_player.GlobalPosition;
        Require(_player.CanStandAt(freeFeet),"the previous indoor position is clear before placing the obstacle");
        var blocker=new StaticBody3D{Name="BlockedSavedIndoorFeet",CollisionLayer=1,CollisionMask=0};
        blocker.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.85f,2.2f,.85f)}});
        AddChild(blocker);blocker.GlobalPosition=savedFeet+Vector3.Up*1.1f;
        var store=new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var files=new[]{store.SlotPath(slot),store.BackupPath(slot),"user://settings.json"};
        var disk=files.ToDictionary(path=>path,FileDigest);
        var before=_bridge.SelectRuntimeState().Clone();
        try
        {
            await Frames(3);
            Require(!_player.CanCrouchAt(savedFeet)&&_player.CanStandAt(freeFeet),
                "the real obstacle blocks even a crouched saved capsule while the previous pose remains clear");
            var atLoad=_bridge.SelectRuntimeState().Clone();
            var liveAtLoad=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture());
            Require(!await _bridge.LoadSlotAsync(slot),"a real indoor save refuses a newly obstructed player position");
            Require(_bridge.CurrentZoneId=="house_old_pc"&&!_bridge.NeedsPhysicalRecovery
                &&_bridge.SessionIdentity is not null&&_fleet.PlacementValidationDeferred
                &&_fleet.ProjectionFailure is null&&!_player.ModalOpen&&!_player.VehicleControlled
                &&_player.GlobalPosition.DistanceTo(freeFeet)<.08f&&_player.CanStandAt(_player.GlobalPosition),
                "blocked indoor load rolls back to the previous free feet with ordinary control");
            var afterRollback=_bridge.SelectRuntimeState().Clone();
            var afterDisk=files.ToDictionary(path=>path,FileDigest);
            var unchanged=JsonNode.DeepEquals(JsonNode.Parse(before.GetRawText()),JsonNode.Parse(afterRollback.GetRawText()));
            var filesUnchanged=files.All(path=>afterDisk[path]==disk[path]);
            _records.Add(new{kind="blocked-indoor-load-state-diagnostics",slot,
                runtimeStateUnchanged=unchanged,filesUnchanged,
                settlingDifferences=DescribeIndoorStateDifferences(before,atLoad),
                rollbackDifferences=DescribeIndoorStateDifferences(before,afterRollback),
                loadBoundaryDifferences=DescribeIndoorStateDifferences(atLoad,afterRollback),
                files=files.Select(path=>new{path,beforeSha256=disk[path],afterSha256=afterDisk[path],unchanged=afterDisk[path]==disk[path]}).ToArray(),
                liveAtLoad,liveAfterRollback=_fleet.Vehicles.ToDictionary(vehicle=>vehicle.Definition.Id,vehicle=>vehicle.Capture()),
                limit="observation only; the original full runtime-state and file-byte equality assertion remains strict"});
            Require(unchanged&&filesUnchanged,
                "blocked indoor load changes neither attained progress, source slot, backup nor settings profile");
        }
        finally{blocker.QueueFree();await Frames(3);}
        Require(await _bridge.LoadSlotAsync(slot),"the same unchanged indoor save loads once the real obstacle is removed");
        await Frames(4);
        Require(_player.GlobalPosition.DistanceTo(savedFeet)<.08f&&_player.CanCrouchAt(_player.GlobalPosition)
            &&!_bridge.NeedsPhysicalRecovery&&files.All(path=>FileDigest(path)==disk[path]),
            "positive indoor pair restores the original usable feet without rewriting its slot");
        _records.Add(new{kind="blocked-indoor-saved-feet",slot,savedFeet=savedFeet.ToString(),
            rollbackFeet=freeFeet.ToString(),actualObstacle=true,negativeLoad=false,positiveLoad=true,
            progressAndSourceFilesPreserved=true});
    }

    private static List<object> DescribeIndoorStateDifferences(JsonElement before,JsonElement after)
    {
        var changes=new List<object>();
        void Visit(JsonNode? a,JsonNode? b,string path,bool beforePresent=true,bool afterPresent=true)
        {
            if(beforePresent==afterPresent&&JsonNode.DeepEquals(a,b))return;
            if(a is JsonObject left&&b is JsonObject right)
            {
                foreach(var key in left.Select(item=>item.Key).Union(right.Select(item=>item.Key)))
                {
                    var had=left.TryGetPropertyValue(key,out var av);var has=right.TryGetPropertyValue(key,out var bv);
                    Visit(av,bv,path+"/"+key.Replace("~","~0").Replace("/","~1"),had,has);
                }
                return;
            }
            if(a is JsonArray aa&&b is JsonArray ba)
            {
                for(var i=0;i<Math.Max(aa.Count,ba.Count);i++)
                    Visit(i<aa.Count?aa[i]:null,i<ba.Count?ba[i]:null,path+"/"+i,i<aa.Count,i<ba.Count);
                return;
            }
            changes.Add(new{path,beforePresent,afterPresent,before=a?.DeepClone(),after=b?.DeepClone()});
        }
        Visit(JsonNode.Parse(before.GetRawText()),JsonNode.Parse(after.GetRawText()),string.Empty);
        return changes;
    }
}
