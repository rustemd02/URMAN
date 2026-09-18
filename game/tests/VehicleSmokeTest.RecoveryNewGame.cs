using Godot;
using System.Reflection;
using System.Text.Json;
using Urman.Core.Persistence;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    // Called only from the existing double-blocked occupied-save fixture, before
    // its cleanup or any successful retry. Reuse its real sources and obstacles.
    private async Task RecoveryNewGameFromBlockedStateAsync(List<StaticBody3D> blockers,
        string[] files,Dictionary<string,string> disk,string settings)
    {
        var field=typeof(RuntimeBridge).GetField("_physicalRecoverySave",BindingFlags.Instance|BindingFlags.NonPublic)
            ??throw new InvalidOperationException("The existing retained snapshot owner is missing.");
        var retained=field.GetValue(_bridge) as SaveGameV3
            ??throw new InvalidOperationException("The real double failure did not retain its previous snapshot.");
        var bytes=new SaveGameV3Codec().Encode(retained);
        Require(_bridge.NeedsPhysicalRecovery&&_bridge.SessionIdentity is null&&_demo.MainMenuVisible,
            "recovery New Game begins before any successful retry, with the actual retained snapshot");
        foreach(var blocker in blockers)blocker.QueueFree();
        blockers.Clear();await Frames(4);
        Require(_bridge.NeedsPhysicalRecovery&&ReferenceEquals(retained,field.GetValue(_bridge))
            &&new SaveGameV3Codec().Encode(retained).SequenceEqual(bytes)&&_player.ModalOpen&&_fleet.Suspended,
            "removing physical obstacles alone cannot dismiss recovery or replace its snapshot");
        var refreshFrames=120;
        while(_demo.MainMenu?.NewGameButton?.Disabled==true&&refreshFrames-->0)await Frames(1);
        Require(_demo.MainMenu?.NewGameButton?.Disabled==false&&_bridge.NeedsPhysicalRecovery,
            "the ordinary New Game button is available while physical recovery is still required");
        Require(await this.StartThroughMainMenuAsync(_demo),
            "actual New Game invokes the recovery-session branch and reaches the ordinary arrival card");
        Require(!_bridge.NeedsPhysicalRecovery&&_bridge.SessionIdentity is not null&&field.GetValue(_bridge) is null
            &&_fleet.ProjectionFailure is null&&!_fleet.PlacementValidationDeferred
            &&_fleet.Occupied is null&&!_player.VehicleControlled&&_player.CanStandAt(_player.GlobalPosition),
            "recovery New Game clears its retained snapshot only after a physically valid exterior projection");
        Require(_bridge.CurrentZoneId=="village_day"&&_bridge.CurrentSpawnPointId=="arrival"
            &&_fleet.Vehicles.All(vehicle=>!vehicle.EngineRunning&&!vehicle.Headlights&&vehicle.ParkingBrake),
            "recovery New Game creates ordinary arrival and fresh parked vehicle state");
        _demo._UnhandledInput(new InputEventKey{Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=true});
        await Frames(8);
        var audio=GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi
            ??throw new InvalidOperationException("The audio cue owner is missing after recovery.");
        Require(!_demo.MainMenuVisible&&!_demo.IntroVisible&&!_player.ModalOpen&&!_fleet.Suspended&&!audio.IsPaused,
            "recovery New Game and arrival dismissal release all input and audio holds");
        Require(files.All(path=>FileDigest(path)==disk[path])&&JsonSerializer.Serialize(_player.CaptureSettings())==settings,
            "recovery New Game preserves both occupied source slots and the authoritative settings profile");
        _records.Add(new{kind="physical-recovery-new-game",enteredWithNeedsPhysicalRecovery=true,
            noSuccessfulRetryBeforeNewGame=true,retainedSnapshotPreservedUntilChoice=true,
            actualArrivalValidated=true,sourceFilesAndProfilePreserved=true,audioResumed=true});
    }
}
