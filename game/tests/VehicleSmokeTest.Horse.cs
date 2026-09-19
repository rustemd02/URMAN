using Godot;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private VehicleHorsePose HorsePose(VehicleController vehicle)
        =>vehicle.FindChildren("SupportedHorsePose",nameof(Node3D),true,false).OfType<VehicleHorsePose>().Single();

    private async Task CaptureSupportedHorseWalk(VehicleController vehicle)
    {
        var rig=HorsePose(vehicle);var before=vehicle.GlobalPosition;
        var previous=GetViewport().GetCamera3D();
        var camera=new Camera3D{Name="HorseWalkProofCamera",Fov=56,Near=.05f};AddChild(camera);
        var sawSwing=false;var sawStance=false;
        try
        {
            for(var sample=0;sample<3;sample++)
            {
                await Frames(12);
                var rejected=new List<string>();
                Require(TryPlaceWholeVehicleCamera(vehicle,camera,90,rejected),"horse walk has a clear complete side camera "+sample);
                camera.MakeCurrent();
                var proof=rig.CaptureSupportProof();var feet=proof["feet"]!.AsArray();
                _records.Add(new{kind="horse-supported-walk",sample,vehicleFeet=vehicle.GlobalPosition.ToString(),speed=vehicle.Speed,
                    camera=camera.GlobalPosition.ToString(),proof});
                Require(proof["initialized"]!.GetValue<bool>()&&feet.Count==4,"horse pose contains four actual articulated feet");
                foreach(var foot in feet)
                {
                    var swing=foot!["phase"]!.GetValue<string>()=="swing";
                    sawSwing|=swing;sawStance|=!swing;
                    Require(foot["reachable"]!.GetValue<bool>(),"moving horse joint chain reaches its actual sole: "+foot["name"]);
                    if(!swing)Require(foot["supported"]!.GetValue<bool>(),"planted moving hoof has real full-footprint support: "+foot["name"]);
                }
                await Capture(vehicle.Definition.Id+"-walking-side-"+(sample+1));
            }
            Require(sawSwing&&sawStance&&vehicle.GlobalPosition.DistanceTo(before)>.20f,
                "horse walk includes real travel, lifted foot and planted foot");
        }
        finally
        {
            Input.ActionRelease("move_forward");
            if(previous is not null&&GodotObject.IsInstanceValid(previous))previous.MakeCurrent();
            camera.QueueFree();await Frames(2);
        }
    }

    private async Task CheckHorseRest(VehicleController vehicle)
    {
        var rig=HorsePose(vehicle);
        var brakeStopped=rig.CaptureSupportProof();
        Require(Math.Abs(vehicle.Speed)<.01f&&vehicle.ParkingBrake,"hoof settlement starts after actual braking has stopped the vehicle");
        var settleFrames=(int)Math.Ceiling((VehicleHorsePose.MaximumSwingDuration+.05f)*Engine.PhysicsTicksPerSecond);
        // Braking time and the last in-progress swing are different intervals.
        // Only the existing bounded swing may finish after speed reaches zero.
        await Frames(settleFrames);
        var proof=rig.CaptureSupportProof();
        _records.Add(new{kind="horse-supported-rest",speed=vehicle.Speed,settleFrames,brakeStopped,proof});
        Require(proof["feet"]!.AsArray().All(foot=>foot!["supported"]!.GetValue<bool>()),
            "stopped horse settles all four actual hooves onto coherent ground");
        var steps=proof["stepsStarted"]!.GetValue<int>();
        var phase=proof["gaitPhase"]!.GetValue<float>();
        var soles=proof["feet"]!.AsArray().Select(foot=>foot!["actualSole"]!.AsArray()
            .Select(value=>value!.GetValue<float>()).ToArray()).ToArray();
        await Frames(settleFrames);
        var retained=rig.CaptureSupportProof();
        _records.Add(new{kind="horse-supported-idle",speed=vehicle.Speed,proof=retained});
        Require(retained["stepsStarted"]!.GetValue<int>()==steps&&retained["gaitPhase"]!.GetValue<float>()==phase,
            "parked horse starts no new step from floor settling or unchanged heading");
        var retainedFeet=retained["feet"]!.AsArray();
        for(var i=0;i<retainedFeet.Count;i++)
        {
            var foot=retainedFeet[i]!;var p=foot["actualSole"]!.AsArray();
            var before=new Vector3(soles[i][0],soles[i][1],soles[i][2]);
            var after=new Vector3(p[0]!.GetValue<float>(),p[1]!.GetValue<float>(),p[2]!.GetValue<float>());
            Require(foot["supported"]!.GetValue<bool>()&&before.DistanceTo(after)<.002f,
                "parked hoof retains its supported world contact without sliding: "+foot["name"]);
        }
        var previous=GetViewport().GetCamera3D();
        var camera=new Camera3D{Name="HorseStoppedProofCamera",Fov=56,Near=.05f};AddChild(camera);
        try
        {
            Require(TryPlaceWholeVehicleCamera(vehicle,camera,90,new List<string>()),"stopped horse has a clear complete side camera");
            camera.MakeCurrent();await Capture(vehicle.Definition.Id+"-stopped-side");
        }
        finally
        {
            if(previous is not null&&GodotObject.IsInstanceValid(previous))previous.MakeCurrent();
            camera.QueueFree();await Frames(2);
        }
    }
}
