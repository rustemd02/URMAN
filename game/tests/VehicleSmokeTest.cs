using Godot;
using System.Text.Json;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>
/// The ordinary connected game plus explicit local approach/obstacle fixtures.
/// This proves controls and snapshot projection, not art, hearing or a human hour.
/// </summary>
public partial class VehicleSmokeTest : Node
{
    private Act1DemoRoot _demo=null!;
    private FirstPersonController _player=null!;
    private RuntimeBridge _bridge=null!;
    private VehicleFleet _fleet=null!;
    private Camera3D _camera=null!;
    private readonly List<object> _records=new();
    private int _checks;
    private string _directory=string.Empty;

    public override async void _Ready()
    {
        var exit=1;string? failure=null;
        var wheelsOnly=System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_WHEELS_ONLY")=="1";
        var mouseOnly=System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_MOUSE_ONLY")=="1";
        var compoundOnly=System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_COMPOUND_ONLY")=="1";
        try
        {
            _directory=System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_OUTPUT")??string.Empty;
            if(string.IsNullOrWhiteSpace(_directory)||!Path.IsPathFullyQualified(_directory))
                throw new InvalidOperationException("URMAN_VEHICLE_OUTPUT must name a new absolute evidence directory.");
            Directory.CreateDirectory(_directory);
            if(File.Exists(Path.Combine(_directory,"receipt.json")))throw new IOException("Vehicle receipt already exists.");
            TimelineChecks();
            _demo=ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo),"ordinary New Game");
            _demo._UnhandledInput(new InputEventKey{Keycode=Key.E,PhysicalKeycode=Key.E,Pressed=true});
            await Frames(8);
            _player=_demo.DemoMain.GetNode<FirstPersonController>("Player");
            _camera=_player.GetNode<Camera3D>("Head/Camera3D");
            _bridge=(RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            _fleet=GetTree().GetFirstNodeInGroup("vehicle_fleet") as VehicleFleet
                ??throw new InvalidOperationException("The connected world did not create its vehicle fleet.");
            Require(_fleet.Vehicles.Count==3,"three vehicle instances in the actual connected world");
            Require(_fleet.Vehicles.Select(vehicle=>vehicle.Definition.Id).Distinct().Count()==3,"stable unique fleet IDs");
            Require(!_player.ModalOpen,"ordinary free control after arrival card");
            // Check the complete fresh fleet before any obstacle fixture can
            // legitimately send a neighbouring vehicle to a fallback bay.
            foreach(var parked in _fleet.Vehicles)
            {
                var initialProbe=parked.DescribePlacementProbe(parked.GlobalTransform);
                _records.Add(new{kind="initial-fleet-parking",vehicle=parked.Definition.Id,probe=initialProbe,
                    rejectedBeforeRecovery=parked.HasMeta("rejectedPlacementProbe")?parked.GetMeta("rejectedPlacementProbe").AsString():null});
                Require(!parked.HasMeta("rejectedPlacementProbe"),parked.Definition.Id+" ordinary authored start needs no parking recovery");
                Require(parked.GlobalPosition.IsEqualApprox(parked.Definition.Spawn),
                    parked.Definition.Id+" starts at its authored grounded position");
                Require(parked.PlacementAvailable&&parked.ValidatePhysicalPlacement(out _),
                    parked.Definition.Id+" initial chassis and actual support are valid");
            }
            if(AddressReadabilityOnly)
            {
                Require(!mouseOnly&&!wheelsOnly&&!compoundOnly,"one explicit narrow vehicle proof is selected");
                await RunAddressReadabilityChecks();exit=0;return;
            }
            if(mouseOnly)
            {
                Require(!wheelsOnly&&!compoundOnly,"one explicit narrow vehicle proof is selected");
                var vehicle=_fleet.Vehicles.Single(vehicle=>vehicle.Definition.Kind==VehicleKind.Niva);
                await Approach(vehicle);await Press("interact");await Frames(4);
                await VehicleMouseSensitivityAcrossResolutions(vehicle);
                Require(vehicle.TryExit(),"mouse-only proof returns through an ordinary safe exit");
                exit=0;return;
            }
            if(wheelsOnly)
            {
                Require(!compoundOnly,"one explicit narrow vehicle proof is selected");
                await RunWheelOnlyChecks(_fleet.Vehicles.Single(vehicle=>vehicle.Definition.Kind==VehicleKind.Niva));
                exit=0;return;
            }
            if(compoundOnly)
            {
                await RunCompoundOnlyChecks();exit=0;return;
            }
            foreach(var vehicle in _fleet.Vehicles)
            {
                var authoredPose=new Transform3D(Basis.FromEuler(new(0,Mathf.DegToRad(vehicle.Definition.YawDegrees),0)),vehicle.Definition.Spawn);
                _records.Add(new{kind="authored-parking-probe",vehicle=vehicle.Definition.Id,
                    authored=vehicle.DescribePlacementProbe(authoredPose),actual=vehicle.DescribePlacementProbe(vehicle.GlobalTransform),
                    rejectedBeforeRecovery=vehicle.HasMeta("rejectedPlacementProbe")?vehicle.GetMeta("rejectedPlacementProbe").AsString():null});
                ValidateMeshIndices(vehicle);
                CheckCompoundModelEnvelope(vehicle,"full-fleet-parking");
                ValidateRinatParkingClearance(vehicle);
                await CaptureWholeVehicle(vehicle);
                await Approach(vehicle);
                await Capture(vehicle.Definition.Id+"-outside");
                await Press("interact");
                Require(_fleet.Occupied==vehicle&&vehicle.Driver==_player&&_player.VehicleControlled,
                    vehicle.Definition.Id+" enters through the actual ray and mapped action");
                Require(vehicle.VehicleCamera.Current&&!_camera.Current,"vehicle camera owns the view");
                await Frames(4);
                Require(!vehicle.EngineRunning&&vehicle.ParkingBrake,"parked start keeps ignition off");
                if(vehicle.Definition.Kind==VehicleKind.Niva)
                {
                    await VehicleMouseSensitivityAcrossResolutions(vehicle);
                    await SteeringWheelCollisionChecks(vehicle);
                }
                await Press("carry_use");await Frames(40);
                Require(vehicle.EngineRunning&&!vehicle.ParkingBrake,"context engine/horse action starts and releases parking");
                var instrument=vehicle.FindChildren("Speedometer","Node3D",true,false).OfType<Node3D>().SingleOrDefault();
                if(vehicle.Definition.Kind!=VehicleKind.HorseCart)
                    Require(instrument?.GetNodeOrNull<Node3D>("Needle") is not null
                        &&instrument.FindChildren("Scale*","Label3D",true,false).Count==9,
                        "vehicle has a marked physical speedometer with its actual needle");
                if(vehicle.Definition.Kind!=VehicleKind.HorseCart)
                {
                    await Press("carry_rotate");Require(vehicle.Headlights,"mapped headlights work");
                }
                if(vehicle.Radio is {} radio)
                {
                    await Press("carry_place");var offset=radio.SegmentOffset;await Frames(10);
                    Require(radio.Enabled&&radio.SegmentOffset>offset,"radio programme has an advancing saved playhead");
                    await DeliveredRadioChecks(radio);
                    await NativeRadioChecks(vehicle);
                }
                if(vehicle.Definition.Kind==VehicleKind.Niva)await MovingWheelCollisionChecks(vehicle);
                var before=vehicle.GlobalPosition;
                Input.ActionPress("move_forward");await Frames(80);
                if(vehicle.Definition.Kind==VehicleKind.HorseCart)await CaptureSupportedHorseWalk(vehicle);
                Input.ActionRelease("move_forward");await Frames(2);
                Require(vehicle.GlobalPosition.DistanceTo(before)>.25f,"ordinary throttle moves "+vehicle.Definition.Id);
                if(instrument is not null)
                {
                    var maximum=instrument.GetMeta("instrumentMaximum").AsInt32();
                    var expected=130-Mathf.Clamp(Math.Abs(vehicle.Speed)*3.6f/maximum,0,1)*260;
                    Require(Math.Abs(instrument.GetNode<Node3D>("Needle").RotationDegrees.Z-expected)<.05f,
                        "physical speedometer needle follows real vehicle speed");
                }
                var yawBefore=vehicle.Rotation.Y;
                var turn=vehicle.Definition.Spawn.X<0?"move_right":"move_left";
                Input.ActionPress("move_forward");Input.ActionPress(turn);await Frames(12);
                Input.ActionRelease(turn);Input.ActionRelease("move_forward");await Frames(2);
                Require(Math.Abs(Mathf.AngleDifference(yawBefore,vehicle.Rotation.Y))>.002f,
                    "the actual full chassis can steer on the village road");
                Require(Math.Abs(vehicle.Speed)>.25f&&!vehicle.TryExit(),"moving exit is refused without changing possession");
                await Press("crouch");await Frames(35);
                Require(Math.Abs(vehicle.Speed)<.01f&&vehicle.ParkingBrake,"parking brake stops "+vehicle.Definition.Id);
                if(vehicle.Definition.Kind==VehicleKind.HorseCart)await CheckHorseRest(vehicle);
                await ReverseAndIgnition(vehicle);
                await PauseResumeHeldInput(vehicle);
                await Capture(vehicle.Definition.Id+"-driver");
                if(vehicle.Definition.Kind==VehicleKind.HorseCart)
                {
                    var occupiedDriver=vehicle.GetNodeOrNull("VehicleVisual/CartDriver") as Node3D;
                    Require(occupiedDriver is not null&&occupiedDriver.Visible,
                        "occupied cart shows its seated driver figure for "+vehicle.Definition.Id);
                    _records.Add(new{kind="occupied-cart-driver-visible",vehicle=vehicle.Definition.Id,
                        visible=true,scope="bench is occupied: driver figure on, empty-bench geometry unchanged"});
                }
                var saved=vehicle.GlobalPosition;
                var savedYaw=vehicle.RotationDegrees.Y;
                var radioBeforeSave=vehicle.Radio?.Capture();
                var slot="vehicle-smoke-"+vehicle.Definition.Id;
                Require(await _bridge.SaveSlotAsync(slot),"one existing SaveGameV3 saves "+vehicle.Definition.Id);
                Require(_bridge.SelectWorldProps().TryGetProperty(VehicleFleet.FleetStateId,out _),"fleet is in the existing world.props state");
                var savedVehicleState=_bridge.SelectWorldProps().GetProperty(vehicle.Definition.StateId).Clone();
                await BlockedExit(vehicle);
                await BlockedExit(vehicle,startOnly:true);
                Require(vehicle.TryExit(),"unblocked stationary exit succeeds");
                Require(!_player.VehicleControlled&&_camera.Current&&vehicle.Driver is null,
                    "standing body and first-person camera return");
                if(vehicle.Definition.Kind==VehicleKind.HorseCart)
                {
                    var emptiedDriver=vehicle.GetNodeOrNull("VehicleVisual/CartDriver") as Node3D;
                    Require(emptiedDriver is not null&&!emptiedDriver.Visible,
                        "exited cart hides its driver figure and shows the empty bench for "+vehicle.Definition.Id);
                    _records.Add(new{kind="occupied-cart-driver-hidden",vehicle=vehicle.Definition.Id,
                        visible=false,scope="bench is empty again after exit"});
                }
                Require(_player.CanStandAt(_player.GlobalPosition),"exit restores a physically clear standing capsule");
                await Frames(4);
                Require(await _bridge.LoadSlotAsync(slot),"occupied save reloads through RuntimeBridge");
                await Frames(6);
                Require(_fleet.Occupied==vehicle&&_player.VehicleControlled,"saved possession restores without a duplicate actor");
                Require(vehicle.GlobalPosition.DistanceTo(saved)<.08f&&Math.Abs(vehicle.RotationDegrees.Y-savedYaw)<.1f,
                    "parked transform survives save and load");
                Require(vehicle.EngineRunning&&vehicle.ParkingBrake,"ignition and parking state survive load");
                if(vehicle.Radio is {} restoredRadio)
                    CheckSavedRadioPosition(restoredRadio,savedVehicleState.GetProperty("radio"),radioBeforeSave);
                await Frames(3);Require(vehicle.TryExit(),"loaded vehicle can be exited");
                await Frames(4);
                await ChangedObstacleRestore(vehicle,slot);
                await AllParkingBlockedRestore(vehicle,slot,saved);
            }
            var niva=_fleet.Vehicles.Single(vehicle=>vehicle.Definition.Kind==VehicleKind.Niva);
            var forestBefore=new Vector3(0,AgentBAct1HeightField.CollisionGround(0,-91),-91);
            var forestAfter=new Vector3(0,AgentBAct1HeightField.CollisionGround(0,-93),-93);
            Require(!_fleet.EvaluateTravel(niva,forestBefore,forestAfter).Allowed,"vehicles cannot silently enter the gated forest");
            Require(_fleet.TryFindRecoveryStanding(_player,forestAfter,out var forestRecovery)
                &&forestRecovery.Z>=-91.5f&&_player.CanStandAt(forestRecovery),
                "actual standing recovery stays before the unopened forest approach");
            Require(!_fleet.EvaluateTravel(niva,new(0,0,0),new(55,0,0)).Allowed,"driving rejects unsupported cross-country paths");
            await InteriorParkingSurvivesSaveLoad();
            await EntireRecoveryBlockedRollsBack(niva);
            Require(await _bridge.StartNewGameAsync(),"ordinary new session after vehicle saves");
            await Frames(8);
            Require(_fleet.Occupied is null&&!_player.VehicleControlled,"new session releases possession");
            Require(_fleet.Vehicles.All(vehicle=>!vehicle.EngineRunning&&!vehicle.Headlights&&vehicle.ParkingBrake),
                "new session restores authored parking states");
            await DoubleOccupiedRecoveryFailureStaysClosed(niva);
            await DoubleOccupiedRecoveryFailureStaysClosed(niva,recoverWithNewGame:true);
            exit=0;
        }
        catch(Exception exception){failure=exception.ToString();GD.PushError("vehicle-smoke: "+failure);}
        finally
        {
            foreach(var action in new[]{"interact","move_forward","move_backward","move_left","move_right",
                "carry_use","carry_rotate","carry_place","crouch","jump"})Input.ActionRelease(action);
            if(!string.IsNullOrEmpty(_directory))
            {
                var path=Path.Combine(_directory,"receipt.json");
                if(!File.Exists(path))File.WriteAllText(path,JsonSerializer.Serialize(new{
                    kind="actual-game-vehicle-smoke",scope=AddressReadabilityOnly?"driver-address-observation":mouseOnly?"vehicle-captured-mouse":wheelsOnly?"niva-front-wheel-collision":compoundOnly?"motorcycle-cart-compound-collision":"full-fleet",
                    exitCode=exit,checks=_checks,failure,records=_records,
                    limit="local fixtures; not human experience, final art, listening or performance",
                    stationRecordings="actual delivered and missing programme rows reported separately; playback fixtures are not station content"},new JsonSerializerOptions{WriteIndented=true}));
            }
            GetTree().Quit(exit);
        }
    }

    private void TimelineChecks()
    {
        VehicleRadioSegment Segment(string id,double length)=>new(id,"speech","ru","title","text","",length,"pending","");
        var a=Segment("a",2);var b=Segment("b",3);var timeline=new VehicleRadioTimeline(new[]{a,b});
        Require(timeline.Advance(2.5)&&timeline.Current.Id=="b"&&Math.Abs(timeline.Offset-.5)<.001,
            "radio boundary carries fractional offset into the next segment");
        Require(timeline.Advance(5)&&timeline.Current.Id=="b"&&Math.Abs(timeline.Offset-.5)<.001,
            "whole-loop advance still reports a playback boundary");
        var reordered=new VehicleRadioTimeline(new[]{b,a});reordered.Restore("b",.5);
        Require(reordered.Current.Id=="b"&&Math.Abs(reordered.Offset-.5)<.001,
            "saved radio segment survives input reordering");
        reordered.Restore("b",double.NaN);Require(reordered.Offset==0,"non-finite playback offsets are rejected");
        reordered.Restore("pending-removed-from-playback",1.5);
        Require(reordered.Current.Id=="b"&&reordered.Offset==0,
            "a retired unavailable segment starts the remaining programme at zero instead of copying an unrelated offset");
    }

    private async Task Approach(VehicleController vehicle)
    {
        var target=vehicle.EntryTarget;var point=target.GlobalPosition;
        foreach(var radius in new[]{.8f,1.0f,1.3f,1.6f})
        for(var i=0;i<16;i++)
        {
            var offset=new Vector3(Mathf.Sin(i*Mathf.Tau/16),0,Mathf.Cos(i*Mathf.Tau/16))*radius;
            var candidate=point+offset;candidate.Y=AgentBAct1HeightField.CollisionGround(candidate.X,candidate.Z)+.055f;
            if(!_player.CanStandAt(candidate))continue;
            _player.ApplyZoneSpawn(candidate,0);await Frames(5);
            var to=point-_camera.GlobalPosition;
            _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(to.Y,new Vector2(to.X,to.Z).Length())),
                Mathf.RadToDeg(Mathf.Atan2(-to.X,-to.Z)));
            await Frames(3);
            using var ray=PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
                _camera.GlobalPosition-_camera.GlobalBasis.Z*2.7f,7);
            ray.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
            var hit=_camera.GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if(hit.Count==0||hit["collider"].AsGodotObject()!=target)continue;
            _records.Add(new{kind="local-fixture",vehicle=vehicle.Definition.Id,feet=_player.GlobalPosition.ToString(),
                target=point.ToString(),isTraversalMeasurement=false});return;
        }
        throw new InvalidOperationException("No physically valid approach to "+vehicle.Definition.Id+" at "+point);
    }

    private void ValidateMeshIndices(VehicleController vehicle)
    {
        var meshes=vehicle.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().ToArray();
        var corners=0;var manual=0;
        var low=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        var high=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        var chassisVisualLowY=float.PositiveInfinity;
        var supportedHoofMeshes=0;
        foreach(var instance in meshes)
        {
            var mesh=instance.Mesh??throw new InvalidOperationException("Vehicle mesh instance is empty.");
            var groundConformingHoof=vehicle.Definition.Kind==VehicleKind.HorseCart&&instance.Name=="HoofWallAndSole";
            if(groundConformingHoof)supportedHoofMeshes++;
            var local=vehicle.GlobalTransform.AffineInverse()*instance.GlobalTransform;
            var actual=0;
            for(var surface=0;surface<mesh.GetSurfaceCount();surface++)
            {
                var arrays=mesh.SurfaceGetArrays(surface);
                var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                foreach(var vertex in vertices)
                {
                    var point=local*vertex;
                    low=new(Math.Min(low.X,point.X),Math.Min(low.Y,point.Y),Math.Min(low.Z,point.Z));
                    high=new(Math.Max(high.X,point.X),Math.Max(high.Y,point.Y),Math.Max(high.Z,point.Z));
                    if(!groundConformingHoof)chassisVisualLowY=Math.Min(chassisVisualLowY,point.Y);
                }
                Require(indices.All(index=>index>=0&&index<vertices.Length),"vehicle mesh indices refer to actual vertices");
                actual+=indices.Length>0?indices.Length:vertices.Length;
            }
            Require(actual==mesh.GetMeta("expectedTriangleCorners").AsInt32(),
                "all primitive and authored triangles survive batching in "+instance.Name);
            corners+=actual;manual+=mesh.GetMeta("manualTriangleCorners").AsInt32();
        }
        Require(meshes.Length>2&&corners>100&&manual>0,"complete body, wheels and authored surfaces for "+vehicle.Definition.Id);
        if(vehicle.Definition.Kind==VehicleKind.HorseCart)
        {
            var cartDriver=vehicle.GetNodeOrNull("VehicleVisual/CartDriver") as Node3D;
            var driverBody=vehicle.GetNodeOrNull("VehicleVisual/CartDriver/CartDriverBody") as MeshInstance3D;
            Require(cartDriver is not null&&driverBody is not null&&driverBody.Mesh is not null,
                "occupied-cart driver figure exists as presentation-only geometry for "+vehicle.Definition.Id);
            var driverMesh=(ArrayMesh)driverBody!.Mesh!;
            Require(driverMesh.GetMeta("expectedTriangleCorners").AsInt32()>0,
                "occupied-cart driver figure keeps its authored triangles for "+vehicle.Definition.Id);
            Require(cartDriver!.GetMeta("presentationOwnership").AsString().Contains("presentation-only"),
                "occupied-cart driver figure owns no physics or save state for "+vehicle.Definition.Id);
            _records.Add(new{kind="occupied-cart-driver-figure",vehicle=vehicle.Definition.Id,
                visibleWhileParked=cartDriver.Visible,bodyMesh=driverBody.Name.ToString(),
                corners=driverMesh.GetMeta("expectedTriangleCorners").AsInt32(),
                scope="figure hidden while parked; occupancy visibility is proven in the occupied branch below"});
        }
        var hullLow=vehicle.Definition.HullCenter-vehicle.Definition.HullSize*.5f-Vector3.One*.012f;
        var hullHigh=vehicle.Definition.HullCenter+vehicle.Definition.HullSize*.5f+Vector3.One*.012f;
        if(supportedHoofMeshes>0)
        {
            var proof=HorsePose(vehicle).CaptureSupportProof();
            _records.Add(new{kind="articulated-hoof-mesh-bounds",supportedHoofMeshes,wholeVisualLow=low.ToString(),
                chassisVisualLowY,proof,limit="only the four supported hoof soles may conform below the flat chassis; no additional physical owner"});
            Require(supportedHoofMeshes==4&&proof["feet"]!.AsArray().All(foot=>foot!["supported"]!.GetValue<bool>()),
                "all four ground-conforming hoof meshes have actual full-footprint support");
        }
        Require(low.X>=hullLow.X&&chassisVisualLowY>=hullLow.Y&&low.Z>=hullLow.Z
            &&high.X<=hullHigh.X&&high.Y<=hullHigh.Y&&high.Z<=hullHigh.Z,
            "generated vehicle body fits its retained outer envelope with separately proven ground-conforming hooves for "+vehicle.Definition.Id
            +"; visual "+low+".."+high+", hull "+hullLow+".."+hullHigh);
        _records.Add(new{kind="actual-mesh-index-audit",vehicle=vehicle.Definition.Id,meshes=meshes.Length,corners,manual,
            visualLow=low.ToString(),visualHigh=high.ToString(),hullLow=hullLow.ToString(),hullHigh=hullHigh.ToString()});
    }

    private async Task BlockedExit(VehicleController vehicle,bool startOnly=false)
    {
        var fixtures=new List<StaticBody3D>();
        try
        {
            var side=vehicle.Definition.HullSize.X*.5f+_player.BodyRadius+.19f;
            var wallSide=startOnly?vehicle.Definition.HullSize.X*.5f+.06f:side;
            var z=vehicle.Definition.Kind==VehicleKind.HorseCart ? .39f : .15f;
            foreach(var sign in new[]{-1,1})
            {
                var wall=new StaticBody3D{Name="VehicleExitProof"+(sign<0?"Left":"Right"),CollisionLayer=1,CollisionMask=0};
                wall.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(startOnly ? .02f : .48f,2.4f,2.4f)}});
                AddChild(wall);wall.GlobalTransform=new(vehicle.GlobalBasis,vehicle.ToGlobal(new(sign*wallSide,1.2f,z+.3f)));
                fixtures.Add(wall);
            }
            await Frames(3);
            var position=_player.GlobalPosition;
            Require(!vehicle.TryExit()&&vehicle.Driver==_player&&_player.GlobalPosition.IsEqualApprox(position),
                startOnly?"thin fences overlapping sweep origins reject exit even with clear endpoints"
                    :"two actual blocked door exits preserve driver state");
        }
        finally{foreach(var wall in fixtures)wall.QueueFree();await Frames(3);}
    }

    private async Task Press(string action)
    {Input.ActionPress(action);await Frames(2);Input.ActionRelease(action);await Frames(4);}

    private async Task ChangedObstacleRestore(VehicleController vehicle,string slot)
    {
        var savedPosition=vehicle.GlobalPosition;
        var engine=vehicle.EngineRunning;var headlights=vehicle.Headlights;var radioEnabled=vehicle.Radio?.Enabled;
        var obstruction=new StaticBody3D{Name="ChangedVehicleParkingProof",CollisionLayer=1,CollisionMask=0};
        obstruction.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.6f,.6f,.6f)}});
        AddChild(obstruction);obstruction.GlobalPosition=savedPosition+Vector3.Up;
        try
        {
            await Frames(3);
            Require(await _bridge.LoadSlotAsync(slot),"existing snapshot loads with a changed parking obstacle");
            await Frames(8);
            Require(vehicle.GlobalPosition.DistanceTo(savedPosition)>.25f&&vehicle.ValidatePhysicalPlacement(out _),
                "a saved chassis inside a new obstacle recovers to checked parking");
            Require(_fleet.Occupied==vehicle&&vehicle.Driver==_player,
                "recovered parked vehicle retains one driver after load");
            Require(vehicle.EngineRunning==engine&&vehicle.Headlights==headlights&&vehicle.Radio?.Enabled==radioEnabled,
                "parking repair preserves saved ignition, lights and radio power");
            Require(vehicle.TryExit(),"recovered parking has a safe physical exit");await Frames(3);
        }
        finally{obstruction.QueueFree();await Frames(3);}
    }

    private async Task ReverseAndIgnition(VehicleController vehicle)
    {
        await Press("crouch");var position=vehicle.GlobalPosition;
        var reverseBefore=vehicle.DescribeDriverLookInput();
        Input.ActionPress("move_backward");await Frames(45);Input.ActionRelease("move_backward");await Frames(2);
        // What the ordinary control actually did, or refused, is part of the evidence.
        _records.Add(new{kind="reverse-from-rest",vehicle=vehicle.Definition.Id,
            engineRunning=vehicle.EngineRunning,parkingBrake=vehicle.ParkingBrake,speed=vehicle.Speed,
            travelled=vehicle.GlobalPosition.DistanceTo(position),lastRefusal=vehicle.LastRefusal,
            hint=vehicle.ControlHint(),before=reverseBefore,after=vehicle.DescribeDriverLookInput()});
        Require(vehicle.Speed<-.1f&&vehicle.GlobalPosition.DistanceTo(position)>.1f,
            "reverse starts from rest through the ordinary controls");
        await Press("crouch");await Frames(35);
        await Press("carry_use");Require(!vehicle.EngineRunning,"ignition/horse command stops the power state");
        await Press("crouch");Require(!vehicle.ParkingBrake,"ignition check releases the parking brake");
        position=vehicle.GlobalPosition;
        Input.ActionPress("move_forward");await Frames(20);Input.ActionRelease("move_forward");await Frames(3);
        Require(Math.Abs(vehicle.Speed)<.01f&&vehicle.GlobalPosition.DistanceTo(position)<.02f,
            "throttle does not move a stopped engine or halted horse");
        await Press("carry_use");await Frames(40);await Press("crouch");await Frames(5);
        Require(vehicle.EngineRunning&&vehicle.ParkingBrake,"parking and ignition remain separate after restart");
    }

    private async Task PauseResumeHeldInput(VehicleController vehicle)
    {
        var pause=GetTree().GetFirstNodeInGroup("pause_menu") as PauseMenuUi
            ??throw new InvalidOperationException("The actual pause menu is unavailable.");
        var before=vehicle.Capture().ToJsonString();
        var position=vehicle.GlobalPosition;
        var engine=vehicle.EngineRunning;
        pause.Open();await Frames(3,allowPause:true);
        Input.ActionPress("interact");Input.ActionPress("carry_use");Input.ActionPress("move_forward");
        pause.Resume();await Frames(5);
        Require(vehicle.Driver==_player&&vehicle.EngineRunning==engine&&vehicle.GlobalPosition.DistanceTo(position)<.02f,
            "pause resume does not leak held activation or throttle into "+vehicle.Definition.Id);
        Input.ActionRelease("interact");Input.ActionRelease("carry_use");Input.ActionRelease("move_forward");
        await Frames(4);
        _records.Add(new{kind="pause-held-input",vehicle=vehicle.Definition.Id,before,after=vehicle.Capture().ToJsonString()});
    }

    private async Task Frames(int count,bool allowPause=false)
    {
        for(var i=0;i<count;i++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(!allowPause&&GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi{IsOpen:true})
                throw new InvalidOperationException("Vehicle proof paused by the actual pause menu.");
        }
    }

    private void Require(bool condition,string label)
    {
        if(!condition)throw new InvalidOperationException(label);_checks++;
        _records.Add(new{kind="check",label,passed=true});GD.Print("vehicle-smoke: "+label);
    }

    private async Task Capture(string name)
    {
        if(System.Environment.GetEnvironmentVariable("URMAN_VEHICLE_CAPTURE")!="1")return;
        if(RenderingServer.GetRenderingDevice() is null)throw new InvalidOperationException("Native vehicle capture requires a render device.");
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this,"vehicle/"+name);
        var path=Path.Combine(_directory,name+".png");
        if(File.Exists(path))throw new IOException("Refusing to overwrite "+path);
        using var frame=GetViewport().GetTexture().GetImage();
        if(frame.IsEmpty()||frame.SavePng(path)!=Error.Ok)throw new IOException("Vehicle frame is empty or cannot be saved.");
        _records.Add(new{kind="actual-rendered-frame",path,camera=GetViewport().GetCamera3D()?.GetPath().ToString()});
    }
}
