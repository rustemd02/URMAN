using Godot;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task EntireRecoveryBlockedRollsBack(VehicleController vehicle)
    {
        const string targetSlot="vehicle-recovery-player-target";
        const string previousSlot="vehicle-recovery-debug-before";
        const string routedSlot="vehicle-recovery-debug-after";
        // The target is an ordinary attained occupied save. The previous state
        // is a different, reached home visit in the isolated debug store. No
        // kernel flag or invalid saved transform is manufactured by the test.
        await Approach(vehicle);await Press("interact");
        Require(vehicle.Driver==_player,"rollback target uses an ordinary occupied vehicle");
        Require(await _bridge.SaveSlotAsync(targetSlot),"occupied rollback target is saved by the existing owner");
        var savedPose=vehicle.GlobalPosition;
        Require(vehicle.TryExit(),"rollback setup exits its stationary vehicle");
        Require(await _bridge.StartDebugSessionAsync(),"rollback previous state starts through the existing isolated session API");
        await Frames(8);
        await Act1ArrivalFlowProof.CompleteAsync(this,_bridge);
        Require(await _bridge.DispatchInteractionAsync("urman.chapter1:interaction/arrival-enter-house"),
            "rollback previous state earns house access from the actual arrival sources");
        _demo.DemoMain.SwitchZone("house_old_pc","entry");
        await Frames(8);
        Require(_bridge.IsDebugSession&&_bridge.CurrentZoneId=="house_old_pc"&&_player.CanStandAt(_player.GlobalPosition),
            "previous debug home state has a clear standing capsule");
        Require(await _bridge.SaveSlotAsync(previousSlot),"previous attained debug state is captured through the sole save owner");

        var candidates=_fleet.RecoveryStandingCandidates(savedPose,"village_day");
        Require(candidates.Count>100,"diagnostic enumeration includes all rings and the existing entry anchor");
        Require(candidates.SequenceEqual(_fleet.RecoveryStandingCandidates(savedPose,"village_day")),
            "recovery candidates have deterministic identity and order");
        var points=candidates.ToList();
        // Block all fallback parking for every projected chassis, so neither
        // rejected load nor rollback may silently choose a different free bay.
        foreach(var item in _fleet.Vehicles)
        {
            points.Add(item.GlobalPosition);
            var basis=Basis.FromEuler(new(0,Mathf.DegToRad(item.Definition.YawDegrees),0));
            foreach(var distance in new[]{0f,6f,-6f,12f,-12f})
                points.Add(item.Definition.Spawn+basis*new Vector3(0,0,distance));
        }
        var blockers=new List<StaticBody3D>();
        var playerStore=new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var debugStore=new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://debug-savegames"));
        Require(File.Exists(playerStore.SlotPath(targetSlot))&&File.Exists(debugStore.SlotPath(previousSlot)),
            "both source slots exist at paths provided by AtomicSaveGameStore");
        var paths=new[]{playerStore.SlotPath(targetSlot),playerStore.BackupPath(targetSlot),
            debugStore.SlotPath(previousSlot),debugStore.BackupPath(previousSlot),"user://settings.json"};
        var diskBefore=paths.ToDictionary(path=>path,FileDigest);
        var stateBefore=_bridge.SelectRuntimeState().Clone();
        var settingsBefore=JsonSerializer.Serialize(_player.CaptureSettings());
        var positionBefore=_player.GlobalPosition;var rotationBefore=_player.RotationDegrees;
        var vehicleBefore=_fleet.Vehicles.ToDictionary(item=>item.Definition.Id,item=>item.Capture());
        var oldPcBefore=_bridge.OldPcState().GetRawText();
        var zoneBefore=_bridge.CurrentZoneId;var spawnBefore=_bridge.CurrentSpawnPointId;
        var sceneBefore=_bridge.ActiveSceneId;
        try
        {
            foreach(var point in points)
            {
                var blocker=new StaticBody3D{Name="NoRecoveryGround"+blockers.Count,CollisionLayer=1,CollisionMask=0};
                blocker.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.65f,3.4f,.65f)}});
                AddChild(blocker);
                blocker.GlobalPosition=new(point.X,AgentBAct1HeightField.CollisionGround(point.X,point.Z)+1.7f,point.Z);
                blockers.Add(blocker);
            }
            await Frames(3);
            Require(_player.CanStandAt(positionBefore),"all exterior recovery fixtures leave the previous home capsule clear");
            Require(candidates.All(candidate=>!_fleet.TryResolveRecoveryStandingCandidate(_player,candidate,out _)),
                "real obstacles reject every enumerated standing candidate before the load");
            Require(!await _bridge.LoadPlayerSlotAsync(targetSlot),
                "a real occupied save with no physical recovery returns false");
            // Check immediately after the awaited transaction, before ordinary
            // radio clocks or future user input can change the restored state.
            Require(_bridge.IsDebugSession&&_bridge.CurrentZoneId==zoneBefore&&_bridge.CurrentSpawnPointId==spawnBefore
                &&_bridge.ActiveSceneId==sceneBefore,"failed player-slot projection restores the previous debug home identity");
            Require(JsonNode.DeepEquals(JsonNode.Parse(stateBefore.GetRawText()),JsonNode.Parse(_bridge.SelectRuntimeState().GetRawText())),
                "rollback preserves the entire attained runtime including knowledge, journal, inventory and vehicle records");
            Require(_bridge.OldPcState().GetRawText()==oldPcBefore,"rollback preserves the existing computer capability");
            Require(JsonSerializer.Serialize(_player.CaptureSettings())==settingsBefore
                &&paths.All(path=>FileDigest(path)==diskBefore[path]),
                "rejected projection changes neither source slots nor the authoritative settings profile");
            Require(_player.GlobalPosition.DistanceTo(positionBefore)<.02f&&_player.RotationDegrees.DistanceTo(rotationBefore)<.1f
                &&_player.CanStandAt(_player.GlobalPosition)&&!_player.VehicleControlled&&!_player.ModalOpen,
                "rollback restores actual clear home feet, orientation and ordinary capsule without a transition hold");
            foreach(var item in _fleet.Vehicles)
            {
                var before=vehicleBefore[item.Definition.Id];var after=item.Capture();
                Require(JsonNode.DeepEquals(before,after),"rollback preserves actual parked vehicle power, pose and radio: "+item.Definition.Id);
            }
            Require(_fleet.Occupied is null&&_fleet.ProjectionFailure is null,"rollback clears only the rejected projection failure");
            await Frames(4);
            Require(!_fleet.Suspended&&!_player.ModalOpen&&!_player.VehicleControlled,
                "no load, suspension or capsule hold leaks into subsequent frames");
            Require(await _bridge.SaveSlotAsync(routedSlot)&&_bridge.IsSlotAvailable(routedSlot)&&!_bridge.IsPlayerSlotAvailable(routedSlot),
                "the next ordinary save remains in the restored debug store");
            _records.Add(new{kind="all-recovery-blocked-rollback",candidates=candidates.Select(point=>point.ToString()).ToArray(),
                fixtures=points.Count,previousZone=zoneBefore,previousFeet=positionBefore.ToString(),targetSlot,
                loadReturnedFalse=true,runtimePreserved=true,profileAndStoresPreserved=true,playerCapsuleClear=true});
        }
        finally{foreach(var blocker in blockers)blocker.QueueFree();await Frames(3);}
        Require(await _bridge.LoadPlayerSlotAsync(targetSlot),"the same unchanged save loads after removal of real obstructions");
        await Frames(4);
        Require(!_bridge.IsDebugSession&&vehicle.Driver==_player&&_fleet.Occupied==vehicle,
            "positive pair restores exactly one saved driver and the player store");
        Require(vehicle.TryExit(),"the recovered positive pair has an ordinary safe exit");
    }

    private static string FileDigest(string path)
    {
        var native=path.StartsWith("user://",StringComparison.Ordinal)?ProjectSettings.GlobalizePath(path):path;
        return File.Exists(native)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native))):"absent";
    }

    private async Task DoubleOccupiedRecoveryFailureStaysClosed(VehicleController vehicle,bool recoverWithNewGame=false)
    {
        const string targetSlot="vehicle-double-recovery-target";
        const string previousSlot="vehicle-double-recovery-previous";
        const string forbiddenSlot="vehicle-double-recovery-forbidden";
        await Approach(vehicle);await Press("interact");
        Require(vehicle.Driver==_player,"double-failure target starts with an ordinary driver");
        Require(await _bridge.SaveSlotAsync(targetSlot),"double-failure target uses a real occupied save");
        var targetPose=vehicle.GlobalPosition;
        await Press("carry_use");await Frames(40);
        Input.ActionPress("move_forward");await Frames(45);Input.ActionRelease("move_forward");
        await Press("crouch");await Frames(35);
        Require(vehicle.GlobalPosition.DistanceTo(targetPose)>.25f&&vehicle.ParkingBrake&&Math.Abs(vehicle.Speed)<.01f,
            "previous occupied state is a different position reached through throttle and parking brake");
        await Press("carry_rotate");
        Require(await _bridge.SaveSlotAsync(previousSlot),"previous occupied state is captured by the same save owner");
        var previousPose=vehicle.GlobalPosition;
        var previousFeet=_player.GlobalPosition;
        var previousZone=_bridge.CurrentZoneId;var previousSpawn=_bridge.CurrentSpawnPointId;
        var before=_bridge.SelectRuntimeState().GetRawText();
        var settings=JsonSerializer.Serialize(_player.CaptureSettings());
        var oldPc=_bridge.OldPcState().GetRawText();
        var store=new AtomicSaveGameStore(ProjectSettings.GlobalizePath("user://savegames"));
        var files=new[]{store.SlotPath(targetSlot),store.BackupPath(targetSlot),store.SlotPath(previousSlot),
            store.BackupPath(previousSlot),"user://settings.json"};
        Require(File.Exists(store.SlotPath(targetSlot))&&File.Exists(store.SlotPath(previousSlot)),
            "both occupied snapshots have actual source files");
        var disk=files.ToDictionary(path=>path,FileDigest);
        var candidates=_fleet.RecoveryStandingCandidates(targetPose,"village_day")
            .Concat(_fleet.RecoveryStandingCandidates(previousPose,"village_day")).Distinct().ToArray();
        var points=candidates.ToList();
        points.Add(targetPose);
        points.Add(previousPose);
        foreach(var item in _fleet.Vehicles)
        {
            points.Add(item.GlobalPosition);
            var basis=Basis.FromEuler(new(0,Mathf.DegToRad(item.Definition.YawDegrees),0));
            foreach(var distance in new[]{0f,6f,-6f,12f,-12f})
                points.Add(item.Definition.Spawn+basis*new Vector3(0,0,distance));
        }
        var pause=GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi
            ??throw new InvalidOperationException("The ordinary pause shell is missing.");
        var settingsUi=GetTree().GetFirstNodeInGroup("settings_ui") as SettingsUi
            ??throw new InvalidOperationException("The ordinary settings shell is missing.");
        var audio=GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi
            ??throw new InvalidOperationException("The existing audio cue owner is missing.");
        pause.Open();await Frames(3,allowPause:true);
        Require(pause.IsOpen&&_player.ModalOpen&&_fleet.Suspended,
            "ordinary pause holds the current driver while the external geometry fixture changes");
        (pause.SettingsButton??throw new InvalidOperationException("The pause settings button is missing."))
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3,allowPause:true);
        Require(settingsUi.IsOpen,"the actual pause settings button opens the overlapping settings shell");
        var blockers=new List<StaticBody3D>();
        try
        {
            foreach(var point in points)
            {
                var blocker=new StaticBody3D{Name="DoubleRecoveryBlocked"+blockers.Count,CollisionLayer=1,CollisionMask=0};
                blocker.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.65f,3.4f,.65f)}});
                AddChild(blocker);
                blocker.GlobalPosition=new(point.X,AgentBAct1HeightField.CollisionGround(point.X,point.Z)+1.7f,point.Z);
                blockers.Add(blocker);
            }
            await Frames(3,allowPause:true);
            Require(candidates.All(candidate=>!_fleet.TryResolveRecoveryStandingCandidate(_player,candidate,out _)),
                "actual obstacles reject every standing candidate of target and previous occupied poses");
            Require(!await _bridge.LoadSlotAsync(targetSlot),"target and occupied rollback both fail on real blocked geometry");
            await Frames(4);
            Require(_bridge.NeedsPhysicalRecovery&&_bridge.SessionIdentity is null&&_player.ModalOpen
                &&_fleet.Suspended&&_demo.MainMenuVisible&&!pause.IsOpen&&!settingsUi.IsOpen&&audio.IsPaused,
                "double failure closes pause/settings, keeps world input/audio paused and opens one existing main menu");
            if(recoverWithNewGame)
            {
                await RecoveryNewGameFromBlockedStateAsync(blockers,files,disk,settings);
                return;
            }
            var refreshFrames=120;
            while(_demo.MainMenu?.NewGameButton?.Disabled==true&&refreshFrames-->0)await Frames(1);
            var refreshedMenu=_demo.MainMenu??throw new InvalidOperationException("The recovery main menu disappeared.");
            var recoveryMessage=refreshedMenu.FindChild("ContinueUnavailable",true,false) as Label;
            Require(refreshedMenu.NewGameButton?.Disabled==false&&recoveryMessage is {Visible:true}
                &&recoveryMessage.Text.Contains("свободное место",StringComparison.Ordinal),
                "the physical recovery explanation remains after asynchronous Continue availability refresh");
            var recoveryField=typeof(RuntimeBridge).GetField("_physicalRecoverySave",BindingFlags.Instance|BindingFlags.NonPublic)
                ??throw new InvalidOperationException("The existing save owner's recovery snapshot field is missing.");
            var retained=recoveryField.GetValue(_bridge) as SaveGameV3
                ??throw new InvalidOperationException("Double failure did not retain the previous SaveGameV3.");
            var retainedBytes=new SaveGameV3Codec().Encode(retained);
            var retainedFeet=retained.PlayerTransform.Position;
            Require(retained.CurrentZone.Value==previousZone&&retained.SpawnPoint.Value==previousSpawn
                &&new Vector3((float)retainedFeet.X,(float)retainedFeet.Y,(float)retainedFeet.Z).DistanceTo(previousFeet)<.02f
                &&JsonNode.DeepEquals(JsonNode.Parse(before),JsonNode.Parse(retained.Runtime.State.GetRawText())),
                "the sole retained SaveGameV3 describes the previous attained occupied state and actual seated feet");
            Require(JsonNode.DeepEquals(JsonNode.Parse(before),JsonNode.Parse(_bridge.SelectRuntimeState().GetRawText()))
                &&_bridge.OldPcState().GetRawText()==oldPc,
                "double failure retains the previous attained runtime and computer state in memory");
            Require(!await _bridge.SaveSlotAsync(forbiddenSlot)&&!_bridge.IsSlotAvailable(forbiddenSlot),
                "recovery mode rejects a save before writing any unusable placement");
            Require(!await _bridge.DispatchWorldPropsAsync(new JsonArray(vehicle.Capture())),
                "recovery mode rejects an otherwise valid world-state write");
            var heldFeet=_player.GlobalPosition;var heldVehicle=vehicle.GlobalPosition;var heldEngine=vehicle.EngineRunning;
            try
            {
                Input.ActionPress("move_forward");Input.ActionPress("carry_use");Input.ActionPress("interact");await Frames(8);
                Require(_player.GlobalPosition.IsEqualApprox(heldFeet)&&vehicle.GlobalPosition.IsEqualApprox(heldVehicle)
                    &&vehicle.EngineRunning==heldEngine&&_bridge.NeedsPhysicalRecovery,
                    "held gameplay input cannot move, start or enter the rejected world behind the menu");
            }
            finally{Input.ActionRelease("move_forward");Input.ActionRelease("carry_use");Input.ActionRelease("interact");}
            Require(files.All(path=>FileDigest(path)==disk[path])&&JsonSerializer.Serialize(_player.CaptureSettings())==settings,
                "double failure preserves both source snapshots and the authoritative profile");
            Require(!await _bridge.LoadSlotAsync(targetSlot),
                "a retry while both real placements remain blocked also returns false");
            await Frames(4);
            Require(ReferenceEquals(recoveryField.GetValue(_bridge),retained)
                &&new SaveGameV3Codec().Encode(retained).SequenceEqual(retainedBytes)
                &&_bridge.NeedsPhysicalRecovery&&_bridge.SessionIdentity is null&&_player.ModalOpen&&_fleet.Suspended,
                "repeated rejection preserves the exact previous snapshot reference and bytes without flushing the unusable world");
            Require(files.All(path=>FileDigest(path)==disk[path]),"repeated rejection preserves source files and profile");
            _records.Add(new{kind="double-occupied-recovery-failure",targetPose=targetPose.ToString(),previousPose=previousPose.ToString(),
                candidates=candidates.Length,fixtures=points.Count,inputBlocked=true,saveBlocked=true,menuVisible=true,
                previousRuntimeRetained=true,sourceFilesPreserved=true,retrySnapshotReferencePreserved=true,
                retainedSnapshotSha256=Convert.ToHexString(SHA256.HashData(retainedBytes))});
        }
        finally{foreach(var blocker in blockers)blocker.QueueFree();await Frames(3);}
        Require(await _bridge.LoadSlotAsync(targetSlot),"the same target loads after actual recovery obstructions are removed");
        await Frames(4);
        Require(!_bridge.NeedsPhysicalRecovery&&_bridge.SessionIdentity is not null&&vehicle.Driver==_player
            &&vehicle.PlacementAvailable&&vehicle.ValidatePhysicalPlacement(out _),
            "successful retry clears the recovery gate only after restoring a physically valid occupied pose");
        Require(typeof(RuntimeBridge).GetField("_physicalRecoverySave",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(_bridge) is null,
            "successful retry releases the transient retained snapshot");
        Require(_demo.MainMenuVisible&&_player.ModalOpen&&_fleet.Suspended,
            "successful direct load keeps the main menu until an explicit player choice");
        Require(files.All(path=>FileDigest(path)==disk[path]),"successful retry also preserves both source snapshots");
        Require(await this.StartThroughMainMenuAsync(_demo),
            "an explicit ordinary New Game button exits the recovery menu into the arrival card");
        _demo._UnhandledInput(new InputEventKey{Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=true});
        await Frames(8);
        Require(!_demo.MainMenuVisible&&!_demo.IntroVisible&&!_player.ModalOpen&&!_fleet.Suspended
            &&!settingsUi.IsOpen&&!pause.IsOpen&&!audio.IsPaused&&!_bridge.NeedsPhysicalRecovery,
            "explicit new game and arrival dismissal release menu/settings/pause ownership and resume the real audio cue owner");
        Require(!_player.VehicleControlled&&_player.CanStandAt(_player.GlobalPosition),
            "the new ordinary session ends recovery with a clear standing player capsule");
        _records.Add(new{kind="recovery-menu-explicit-new-game",audioResumed=true,settingsClosed=true,standingCapsuleClear=true});
    }
}
