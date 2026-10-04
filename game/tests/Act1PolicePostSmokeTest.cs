using Godot;

namespace Urman.Godot.Tests;

public partial class Act1PolicePostSmokeTest : Node
{
    private Main _main=null!;
    private Act1ConnectedWorld _world=null!;
    private FirstPersonController _player=null!;
    private Node3D _room=null!;
    private Camera3D? _camera;
    private int _checks;
    public override async void _Ready()
    {
        var exit=1;ProcessMode=ProcessModeEnum.Always;
        try
        {
            _main=ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            _main.EnableAct1ConnectedWorld=true;AddChild(_main);await Frames(10);
            var bridge=(RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            Check(await bridge.StartNewGameAsync(),"ordinary session starts");
            _world=_main.ConnectedWorld!;_player=_main.GetNode<FirstPersonController>("Player");
            _world.SetActiveLogicalZone("village_day");await Frames(10);
            _room=(Node3D)_world.FindChild("FarBankPublic_police",true,false);
            Check(_world.TryGetWorldSpawn("village_day","police",out var spawn)&&spawn.Position.DistanceTo(_room.GetMeta("porchFoot").AsVector3())<2,"owner-derived debug entry");
            Check(_room.GetNode<Node3D>("Walls").Visible==false,"old solid shell suppressed");
            Check(_room.GetNode<StaticBody3D>("FarBankPublicBody").GetChildren().OfType<CollisionShape3D>().First().Disabled,"solid shell collision removed, porch retained");
            Check(_world.FacilityInteriorAt(_room.ToGlobal(new(0,.1f,0)))=="police","real indoor volume supports traversal and weather routing");
            await Capture("reception",new(-.10f,1.58f,2.96f),new(-3.05f,1.25f,1.51f));
            await Capture("zhiguli",new(12.2f,1.8f,5.15f),new(7.8f,.85f,1.6f));
            await Capture("exterior",new(12.8f,4.5f,12.0f),new(1.3f,1.4f,1.2f));
            await Capture("officer",new(-2.13f,1.35f,2.96f),new(-3.1f,1.15f,1.5f));
            await Capture("cell",new(.65f,1.45f,-2.38f),new(-.30f,.45f,-3.7f));
            await Capture("mouse",new(-.72f,.24f,-3.13f),new(-1,.045f,-3.72f));
            await Capture("zhiguli-front",new(10.5f,1.55f,-.9f),new(7.8f,.85f,1.6f));
            var officer=_room.GetNode<Node3D>("PoliceDutyOfficer");
            Check(officer.GetMeta("seatedClip").AsString().Contains("Sitting_Idle"),"real compatible seated animation");
            var skeleton=officer.FindChildren("*",nameof(Skeleton3D),true,false).OfType<Skeleton3D>().Single();
            var hip=_room.ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(skeleton.FindBone("pelvis")).Origin));
            Check(Math.Abs(hip.Y-.56f)<.12f,"sitting pelvis actually meets chair height; hip="+hip);
            Check(officer.FindChild("PolicePeakedCap",true,false) is Node3D,"bone mounted cap exists");
            Check(officer.FindChildren("PoliceShoulderBoard*",nameof(Node3D),true,false).Count==2,"two bone mounted shoulder boards");
            var car=_room.GetNode<Node3D>("DistrictZhiguli");
            Check(car.FindChild("LicensedVAZ2106",true,false) is Node3D,"downloaded licensed car is instantiated");
            Check(_room.FindChild("PoliceServiceDesk",true,false).GetMeta("licensedAsset").AsString()=="metal_office_desk","source PBR furniture is used in reception");
            Check(!car.GetMeta("drivable").AsBool()&&!car.GetMeta("roadworthy").AsBool(),"static broken car");
            Check(car.FindChildren("*",nameof(RigidBody3D),true,false).Count==0,"no vehicle physics or fleet owner");
            var letters=car.FindChildren("DistrictDoorLettering*",nameof(Label3D),true,false).OfType<Label3D>().ToArray();
            Check(letters.Length==2&&letters.All(x=>x.Text=="УЧАСТКОВЫЙ"),"both sides have exact period neutral lettering");
            Check(car.GetChildren().OfType<Node3D>().Any(x=>x.HasMeta("flatTyre")),"flat tyre explicitly modelled");
            Check(_room.GetNode<Node3D>("PoliceCellMouse").GetChildCount()>=7&&_room.GetNode<Node3D>("PoliceCellPotato") is not null,"mouse and potato in the unused cell");
            var foot=_room.GetMeta("porchFoot").AsVector3();
            foot.Y=Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(foot.X,foot.Z)+.035f;
            _player.ApplyZoneSpawn(foot,90);await Frames(14);
            await WalkTo(new(0,.035f,5.1f));
            var target=(InteractionTarget)_world.FindChild("PoliceEntranceUse",true,false);
            Check(target.IsAvailable(),"normal entrance action available");target.Interact();
            var hinge=_room.GetNode<Node3D>("PoliceEntranceHinge");
            for(var i=0;i<240&&Math.Abs(Mathf.AngleDifference(hinge.Rotation.Y,Mathf.DegToRad(170)))>.02f;i++)await Frames(1);
            Check(Math.Abs(Mathf.AngleDifference(hinge.Rotation.Y,Mathf.DegToRad(170)))<.02f,"real entrance completes its swing; yaw="+hinge.RotationDegrees+" blocks="+bridge.CapturePlayTimeBlocks()+" result="+target.GetMeta("lastDoorActionResult","none")+" probe="+target.GetMeta("doorSweepProbe","none"));
            var presentation=_room.GetNode<PolicePostPresentation>("PolicePostPresentation");
            await WalkTo(new(0,.035f,3.2f));
            Check(presentation.GreetingsStarted==1,"crossing ordinary entrance triggers one greeting");
            Check(_player.CurrentRemark.Contains("Эй"),"officer greets through existing nonmodal UI");
            var state=bridge.SelectRuntimeState().GetRawText();
            _world.FindChild("PoliceDutyOfficerTalk",true,false).SetMeta("testSeen",true);
            Check(! _player.ModalOpen,"greeting does not take control");
            await WalkTo(new(0,.035f,-.9f));
            await WalkTo(new(0,.035f,-3.15f));
            Check(_player.IsOnFloor()&&_world.FacilityInteriorAt(_player.GlobalPosition)=="police","normal input reaches cell on actual floor");
            Check(bridge.SelectRuntimeState().GetRawText()==state,"local remarks do not mutate narrative state");
            await Capture("cell",new(.65f,1.45f,-2.38f),new(-.30f,.45f,-3.7f));
            await Capture("mouse",new(-.72f,.24f,-3.13f),new(-1,.07f,-3.72f));
            await WalkTo(new(0,.035f,3.3f));
            await Capture("reception",new(-.10f,1.58f,2.96f),new(-3.05f,1.25f,1.51f));
            await Capture("officer",new(-2.13f,1.35f,2.96f),new(-3.1f,1.15f,1.5f));
            await Capture("corridor",new(.08f,1.62f,1.64f),new(0,1.1f,-3.15f));
            await Capture("exterior",new(12.8f,4.5f,12.0f),new(1.3f,1.4f,1.2f));
            await Capture("zhiguli",new(12.2f,1.8f,5.15f),new(7.8f,.85f,1.6f));
            await WalkTo(new(0,.035f,5.15f));
            await WalkTo(new(0,.035f,3.3f));
            Check(presentation.GreetingsStarted==1,"quick reentry respects cooldown");
            GD.Print($"act1-police-post: PASS {_checks} checks; optional background officer; human art/voice review external");exit=0;
        }
        catch(Exception e){GD.PrintErr("act1-police-post: FAIL "+e);}
        finally{Input.ActionRelease("move_forward");if(_main is not null)await GodotSmokeCleanup.ReleaseAsync(_main);GetTree().Quit(exit);}
    }
    private async Task WalkTo(Vector3 local)
    {
        var point=_room.ToGlobal(local);_player.GetNode<Camera3D>("Head/Camera3D").MakeCurrent();
        for(var i=0;i<240;i++)
        {
            var d=point-_player.GlobalPosition;d.Y=0;if(d.Length()<.1f)break;
            _player.ApplySmokeLook(0,Mathf.RadToDeg(Mathf.Atan2(-d.X,-d.Z)));Input.ActionPress("move_forward");await Frames(1);
        }
        Input.ActionRelease("move_forward");await Frames(8);
        Check(new Vector2(point.X-_player.GlobalPosition.X,point.Z-_player.GlobalPosition.Z).Length()<.2f,"ordinary walk to "+local+"; actual="+_room.ToLocal(_player.GlobalPosition));
    }
    private async Task Capture(string name,Vector3 eye,Vector3 look)
    {
        var output=OS.GetEnvironment("URMAN_POLICE_CAPTURE");if(output.Length==0||DisplayServer.GetName()=="headless")return;
        System.IO.Directory.CreateDirectory(output);_camera??=new Camera3D{Name="PoliceEvidenceCamera",Fov=65};
        if(!_camera.IsInsideTree())AddChild(_camera);
        var playerVisible=_player.Visible;
        try
        {
            // Free-camera evidence shows the room, not the avatar whose normal
            // first-person camera has been temporarily replaced. Physics stays live.
            _player.Visible=false;
            _camera.GlobalPosition=_room.ToGlobal(eye);_camera.LookAt(_room.ToGlobal(look),Vector3.Up);_camera.MakeCurrent();await Frames(10);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var texture=GetViewport().GetTexture();using var img=texture.GetImage();Check(img.SavePng(System.IO.Path.Combine(output,name+".png"))==Error.Ok,"native "+name);
        }
        finally{_player.Visible=playerVisible;}
    }
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Check(bool result,string label){_checks++;if(!result)throw new InvalidOperationException(label);}
}
