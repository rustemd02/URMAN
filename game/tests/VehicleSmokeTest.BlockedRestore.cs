using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private async Task AllParkingBlockedRestore(VehicleController vehicle,string slot,Vector3 savedPose)
    {
        var blockers=new List<StaticBody3D>();
        var engine=vehicle.EngineRunning;var lights=vehicle.Headlights;
        var radio=vehicle.Radio?.Enabled;var mileage=vehicle.TotalTravelMetres;
        var basis=Basis.FromEuler(new(0,Mathf.DegToRad(vehicle.Definition.YawDegrees),0));
        var points=new List<Vector3>{savedPose};
        foreach(var distance in new[]{0f,6f,-6f,12f,-12f})
        {
            var point=vehicle.Definition.Spawn+basis*new Vector3(0,0,distance);
            if(points.All(existing=>existing.DistanceTo(point)>.05f))points.Add(point);
        }
        try
        {
            foreach(var point in points)
            {
                var blocker=new StaticBody3D{Name="AllParkingBlocked"+blockers.Count,CollisionLayer=1,CollisionMask=0};
                blocker.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.65f,3.4f,.65f)}});
                AddChild(blocker);
                blocker.GlobalPosition=new(point.X,AgentBAct1HeightField.CollisionGround(point.X,point.Z)+1.7f,point.Z);
                blockers.Add(blocker);
            }
            await Frames(3);
            Require(await _bridge.LoadSlotAsync(slot),"blocked vehicle save restores the person to checked standing ground");
            await Frames(5);
            Require(!vehicle.PlacementAvailable&&!vehicle.ValidatePhysicalPlacement(out _),
                "all five blocked authored parking candidates leave a persistent placement gate");
            Require(_fleet.Occupied is null&&vehicle.Driver is null&&!_player.VehicleControlled,
                "rejected occupied parking releases possession exactly once");
            Require(_player.CanStandAt(_player.GlobalPosition),
                "rejected occupied parking places the real full player capsule clear of obstacles");
            Require(!vehicle.Enter(_player,restore:true)&&!_fleet.TryEnter(vehicle),
                "both restored and ordinary entry reject an unavailable chassis");
            Require(vehicle.EngineRunning==engine&&vehicle.Headlights==lights&&vehicle.Radio?.Enabled==radio
                &&Math.Abs(vehicle.TotalTravelMetres-mileage)<.1f,
                "parking rejection preserves ignition, lights, radio and accumulated travel");
            _records.Add(new{kind="all-parking-blocked",vehicle=vehicle.Definition.Id,fixtures=points.Count,
                savedPose=savedPose.ToString(),recoveryFeet=_player.GlobalPosition.ToString(),
                vehicle.PlacementAvailable,vehicle.PlacementFailure,playerCapsuleClear=true});
        }
        finally
        {
            foreach(var blocker in blockers)blocker.QueueFree();
            await Frames(3);
        }
        await Approach(vehicle);await Press("interact");
        Require(vehicle.PlacementAvailable&&vehicle.Driver==_player,
            "removing the actual obstruction restores ordinary entry without recreating the vehicle");
        Require(vehicle.TryExit(),"revalidated vehicle has a safe ordinary exit");await Frames(3);
    }
}
