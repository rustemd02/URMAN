using Godot;

namespace Urman.Godot.Tests;

public partial class VehicleSmokeTest
{
    private static readonly Vector2 VehiclePhysicalMouseProbe=new(17,-7);
    private readonly List<object> _vehicleMouseSamples=new();
    private VehicleController? _observedMouseVehicle;
    private int _vehicleDeliveredMouseCount;
    private int _vehicleUnhandledMouseCount;
    private ulong _vehicleUnhandledProcessFrame;
    private Vector2 _vehicleDeliveredScreenTotal;
    private Vector2 _vehicleDeliveredRelative;

    public override void _Input(InputEvent inputEvent)=>ObserveVehicleMouse("input",inputEvent);
    public override void _UnhandledInput(InputEvent inputEvent)=>ObserveVehicleMouse("unhandled",inputEvent);

    private void ObserveVehicleMouse(string stage,InputEvent inputEvent)
    {
        if(_observedMouseVehicle is not {} vehicle||inputEvent is not InputEventMouseMotion motion)return;
        _vehicleMouseSamples.Add(new{stage,relative=motion.Relative.ToString(),screenRelative=motion.ScreenRelative.ToString(),
            motion.Device,state=vehicle.DescribeDriverLookInput()});
        if(stage=="input")_vehicleDeliveredScreenTotal+=motion.ScreenRelative;
        if(!motion.ScreenRelative.IsEqualApprox(VehiclePhysicalMouseProbe))return;
        if(stage=="input"){_vehicleDeliveredMouseCount++;_vehicleDeliveredRelative=motion.Relative;}
        else {_vehicleUnhandledMouseCount++;_vehicleUnhandledProcessFrame=Engine.GetProcessFrames();}
        // Deliberately do not consume the event. The actual owner still runs.
    }

    private async Task VehicleMouseSensitivityAcrossResolutions(VehicleController vehicle)
    {
        var originalSize=DisplayServer.WindowGetSize();var originalMode=DisplayServer.WindowGetMode();
        var originalPosition=DisplayServer.WindowGetPosition();var originalMouseMode=Input.MouseMode;
        var look=(Node3D)vehicle.VehicleCamera.GetParent();
        var initialLook=vehicle.DriverLookAngles;
        var turns=new List<Vector2>();
        var relativeSamples=new List<Vector2>();
        try
        {
            Require(DisplayServer.GetName()!="headless","vehicle mouse proof uses a native window");
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1920,1080)})
            {
                // Use the same native-window settling as the passed pedestrian
                // proof. Physics-frame count alone did not record event delivery
                // in B32, whose 1080p sample reported zero motion, not a ratio.
                DisplayServer.WindowSetSize(size);await Frames(8);
                Require(DisplayServer.WindowGetSize()==size,"vehicle mouse proof uses the actual requested window size "+size);
                Require(Input.MouseMode==Input.MouseModeEnum.Captured&&vehicle.Driver==_player&&!_player.ModalOpen&&!_fleet.Suspended
                    &&!GetTree().Paused&&vehicle.CanProcess(),
                    "vehicle mouse proof owns active captured input outside modal UI");
                var before=look.RotationDegrees;var beforeOwner=vehicle.DescribeDriverLookInput();
                // Resizing the native window queues its own motion; measure only
                // after event delivery has been quiet for two frames, so the
                // counts below belong to this probe alone.
                _observedMouseVehicle=vehicle;
                for(var quiet=0;quiet<2;)
                {
                    _vehicleMouseSamples.Clear();
                    await Frames(1);
                    quiet=_vehicleMouseSamples.Count==0?quiet+1:0;
                }
                _vehicleMouseSamples.Clear();_vehicleDeliveredMouseCount=0;_vehicleUnhandledMouseCount=0;_vehicleUnhandledProcessFrame=0;
                _vehicleDeliveredScreenTotal=Vector2.Zero;_observedMouseVehicle=vehicle;
                var centre=(Vector2)size*.5f;
                using var motion=new InputEventMouseMotion{Position=centre,GlobalPosition=centre,
                    Relative=VehiclePhysicalMouseProbe,ScreenRelative=VehiclePhysicalMouseProbe};
                Input.ParseInputEvent(motion);
                var immediatelyAfter=vehicle.DescribeDriverLookInput();
                var projectionReady=await WaitVehicleMouseProjection(vehicle,true,"sample "+size);
                _observedMouseVehicle=null;
                var after=look.RotationDegrees;
                var yaw=Mathf.AngleDifference(Mathf.DegToRad(before.Y),Mathf.DegToRad(after.Y))*180/Mathf.Pi;
                var pitch=after.X-before.X;
                _records.Add(new{kind="vehicle-mouse-screen-relative",requestedSize=size.ToString(),actualSize=DisplayServer.WindowGetSize().ToString(),
                    viewport=GetViewport().GetVisibleRect().Size.ToString(),physicalMotion=VehiclePhysicalMouseProbe.ToString(),
                    sensitivity=_player.MouseSensitivity,yaw,pitch,projectionReady,beforeOwner,immediatelyAfter,afterOwner=vehicle.DescribeDriverLookInput(),
                    deliveredMatching=_vehicleDeliveredMouseCount,unhandledMatching=_vehicleUnhandledMouseCount,
                    deliveredScreenTotal=_vehicleDeliveredScreenTotal.ToString(),events=_vehicleMouseSamples.ToArray()});
                Require(_vehicleDeliveredMouseCount==1&&_vehicleUnhandledMouseCount==1
                    &&_vehicleDeliveredScreenTotal.IsEqualApprox(VehiclePhysicalMouseProbe),
                    "one injected physical motion reaches the actual vehicle input pipeline without another motion cancelling it");
                Require(projectionReady,"vehicle camera publishes the accepted mouse angle after event delivery");
                Require(Math.Abs(yaw+VehiclePhysicalMouseProbe.X*_player.MouseSensitivity)<.015f
                    &&Math.Abs(pitch+VehiclePhysicalMouseProbe.Y*_player.MouseSensitivity)<.015f,
                    "captured vehicle camera keeps physical mouse sensitivity at "+size);
                turns.Add(new(yaw,pitch));
                relativeSamples.Add(_vehicleDeliveredRelative);
            }
            Require(relativeSamples[0].DistanceTo(relativeSamples[1])>.5f,
                "actual viewport transforms Relative differently while preserving physical screen motion");
            Require(turns[0].DistanceTo(turns[1])<.015f,"vehicle physical mouse turn agrees at both actual window sizes");
        }
        finally
        {
            _observedMouseVehicle=null;
            DisplayServer.WindowSetSize(originalSize);DisplayServer.WindowSetMode(originalMode);
            DisplayServer.WindowSetPosition(originalPosition);Input.MouseMode=originalMouseMode;await Frames(8);
            // Compensate what the owner actually accepted, not the sum sent.
            // Restoring the window emits its own motion, so the residual is
            // measured again after every ordinary injection instead of trusting
            // one computed correction; each attempt is recorded as evidence.
            var attempts=new List<object>();
            for(var attempt=0;attempt<4;attempt++)
            {
                var remaining=vehicle.DriverLookAngles-initialLook;
                if(remaining.LengthSquared()<=1e-8f||vehicle.Driver!=_player||_player.ModalOpen||_fleet.Suspended
                    ||Input.MouseMode!=Input.MouseModeEnum.Captured)break;
                if(remaining.DistanceTo(Vector2.Zero)<.015f)break;
                var correction=remaining/_player.MouseSensitivity;
                using var restoreMotion=new InputEventMouseMotion{Relative=correction,ScreenRelative=correction};
                Input.ParseInputEvent(restoreMotion);
                await WaitVehicleMouseProjection(vehicle,false,"restore",initialLook);
                attempts.Add(new{attempt=attempt+1,requested=correction.ToString(),
                    before=(vehicle.DriverLookAngles-remaining).ToString(),after=vehicle.DriverLookAngles.ToString(),
                    residual=(vehicle.DriverLookAngles-initialLook).ToString()});
                await Frames(2);
            }
            _records.Add(new{kind="vehicle-mouse-restoration",initial=initialLook.ToString(),
                after=vehicle.DriverLookAngles.ToString(), attempts=attempts.ToArray(),
                state=vehicle.DescribeDriverLookInput()});
        }
        Require(vehicle.DriverLookAngles.DistanceTo(initialLook)<.015f,
            "vehicle mouse proof restores the driver's accepted view through ordinary input");
    }

    private async Task<bool> WaitVehicleMouseProjection(VehicleController vehicle,bool observeDelivery,string phase,Vector2? expectedRaw=null)
    {
        // Vehicle06 delivered input after that process iteration's physics
        // projection. Three PhysicsFrame signals could therefore observe the
        // previous camera even though the owner accepted the exact right angle.
        // Wait for a later process iteration and the actual published pose;
        // sensitivity is still checked separately against the unchanged input.
        var startedProcess=Engine.GetProcessFrames();var started=Time.GetTicksMsec();var ready=false;var frames=0;
        var look=(Node3D)vehicle.VehicleCamera.GetParent();
        do
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);frames++;
            var raw=vehicle.DriverLookAngles;var projected=look.RotationDegrees;
            var delivered=!observeDelivery||_vehicleUnhandledMouseCount>0;
            var after=observeDelivery?_vehicleUnhandledProcessFrame:startedProcess;
            ready=delivered&&Engine.GetProcessFrames()>after
                &&Math.Abs(Mathf.RadToDeg(Mathf.AngleDifference(Mathf.DegToRad(raw.X),Mathf.DegToRad(projected.Y))))<.015f
                &&Math.Abs(raw.Y-projected.X)<.015f
                &&(!expectedRaw.HasValue||raw.DistanceTo(expectedRaw.Value)<.015f);
        }while(!ready&&Time.GetTicksMsec()-started<2000&&frames<180);
        _records.Add(new{kind="vehicle-mouse-projection-wait",phase,ready,frames,milliseconds=Time.GetTicksMsec()-started,
            startedProcess,deliveredProcess=_vehicleUnhandledProcessFrame,state=vehicle.DescribeDriverLookInput()});
        return ready;
    }
}
