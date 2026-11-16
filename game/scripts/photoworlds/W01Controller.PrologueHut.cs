using System;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    internal const string PrologueHutKey = "prologue/hut-door";
    internal const string PrologueHutInteractionId = "urman.fullgame:interaction/pw-prologue-hut-door";
    private const string PrologueHutLogMaterialPath = "res://assets/materials/act1/w01_winter_hut_log_pbr_v1/w01_winter_hut_log_pbr_v1.tres";
    private static Vector3 PrologueHutPosition => StopPoint(42f, -8.5f);
    private Node3D? _hutHinge;
    private bool _writingKnock;
    private OmniLight3D? _prologueHutLamp;
    private StandardMaterial3D? _prologueHutGlass;
    private bool _prologueHutExtinguished;
    private InteractionTarget? _hutDoorTarget;
    private int _hutReplyBeat;
    private double _hutKnockTime;
    private Node3D? _hutDoorLatch;
    private CanvasLayer? _prologueOpeningEyes;

    private void BuildPrologueForestLandmarks(Node3D owner)
    {
        var hut=new Node3D { Name="ForestersWinterHut",Position=PrologueHutPosition };
        var towards=HutPathStart-hut.Position;
        towards.Y=0;
        hut.Basis=Basis.LookingAt(-towards.Normalized(),Vector3.Up);
        owner.AddChild(hut);
        var wood=PainterlyMaterialLibrary.ForColor("655346","wood_facade");
        var generatedLogWood=ResourceLoader.Load<StandardMaterial3D>(PrologueHutLogMaterialPath);
        Material logWood=generatedLogWood??wood;
        if(generatedLogWood is null)GD.PushWarning("W01 hut log PBR material is missing; retaining its painterly fallback.");
        using var logs=new SurfaceTool(); logs.Begin(Mesh.PrimitiveType.Triangles);
        using var round=new CylinderMesh { TopRadius=.105f,BottomRadius=.105f,Height=1f,RadialSegments=10,Rings=1 };
        var logIndex=0;
        void Log(Vector3 a,Vector3 b)
        {
            // Roll by whole faces and alternate cut direction. The ten-sided
            // profile and atlas islands stay intact; knots no longer stack up at the door.
            var index=logIndex++;
            var delta=(index&2)==0?b-a:a-b;
            var roll=new Basis(Vector3.Up,((index*7+index/5)%10)*Mathf.Tau/10f);
            logs.AppendFrom(round,0,new Transform3D(new Basis(new Quaternion(Vector3.Up,delta.Normalized()))
                *roll*Basis.FromScale(new Vector3(1,delta.Length(),1)),(a+b)*.5f));
        }
        for(var row=0;row<12;row++)
        {
            var y=.13f+row*.195f;
            Log(new(-2.08f,y,-1.70f),new(2.08f,y,-1.70f));
            foreach(var x in new[]{-1.90f,1.90f})Log(new(x,y+.09f,-1.84f),new(x,y+.09f,1.84f));
            // The small closed hut has a real door and a window opening.
            var segments=y<1.90f ? new[]{(-2.08f,-1.40f),(-.50f,2.08f)} : new[]{(-2.08f,2.08f)};
            foreach(var (from,to) in segments)
            {
                if(y>1.05f&&y<1.91f&&to>.25f)
                { Log(new(from,y,1.70f),new(.25f,y,1.70f)); Log(new(1.15f,y,1.70f),new(to,y,1.70f)); }
                else Log(new(from,y,1.70f),new(to,y,1.70f));
            }
        }
        hut.AddChild(new MeshInstance3D { Name="JoinedLogWalls",Mesh=logs.Commit(),MaterialOverride=logWood });
        MeshInstance3D Box(string name,Vector3 size,Vector3 at,string color,string material,Vector3 rotation=default)
        {
            var part = new MeshInstance3D { Name=name,Position=at,Rotation=rotation,
                Mesh=RuralPropGeometry.BevelBox(size,.006f),MaterialOverride=PainterlyMaterialLibrary.ForColor(color,material) };
            hut.AddChild(part);
            return part;
        }
        Box("LowStoneFooting",new(4.03f,.20f,3.53f),new(0,0,0),"77716a","stone");
        _hutHinge=new Node3D {Name="DoorHinge",Position=new(-1.39f,0,1.72f)};
        hut.AddChild(_hutHinge);
        void OnHinge(Node3D part) { var offset=part.Position-_hutHinge.Position; part.Reparent(_hutHinge,false); part.Position=offset; }
        Box("DoorThreshold",new(1.55f,.10f,.55f),new(-.95f,.02f,2.02f),"726454","wood_facade");
        OnHinge(Box("ClosedBoardDoor",new(.88f,1.86f,.065f),new(-.95f,.98f,1.72f),"514438","wood_furniture"));
        for(var i=0;i<5;i++)OnHinge(Box("DoorBoard"+i,new(.16f,1.82f,.025f),new(-1.29f+i*.17f,.98f,1.76f),"675543","wood_furniture"));
        _hutDoorLatch = Box("DoorLatch",new(.15f,.025f,.06f),new(-.66f,1.0f,1.80f),"6f716c","metal");
        OnHinge(_hutDoorLatch);
        foreach(var side in new[]{-1f,1f})
        {
            var rotation=new Vector3(side*Mathf.DegToRad(31f),0,0);
            Box("RoofBoards"+side,new(4.50f,.10f,2.20f),new(0,2.89f,side*.94f),"625449","wood_facade",rotation);
            Box("SettledRoofSnow"+side,new(4.55f,.075f,2.24f),new(0,2.98f,side*.94f),"bfcad5","snow_roof",rotation);
        }
        using(var gables=new SurfaceTool())
        {
            gables.Begin(Mesh.PrimitiveType.Triangles);
            foreach(var side in new[]{-1f,1f})
            {
                var a=new Vector3(side*1.94f,2.38f,-1.72f);
                var b=new Vector3(side*1.94f,2.38f,1.72f);
                var c=new Vector3(side*1.94f,3.45f,0);
                var normal=Vector3.Right*side;
                foreach(var point in (b-a).Cross(c-a).Dot(normal)>0?new[]{a,c,b}:new[]{a,b,c})
                {gables.SetNormal(normal);gables.SetUV(new(point.Z,point.Y));gables.AddVertex(point);}
            }
            hut.AddChild(new MeshInstance3D {Name="BoardedGables",Mesh=gables.Commit(),MaterialOverride=wood});
        }
        foreach(var x in new[]{.21f,1.19f})Box("WindowJamb"+x,new(.10f,.98f,.13f),new(x,1.47f,1.76f),"8d8068","wood_furniture");
        foreach(var y in new[]{1.0f,1.94f})Box("WindowRail"+y,new(1.08f,.10f,.13f),new(.7f,y,1.76f),"8d8068","wood_furniture");
        Box("WindowMullion",new(.045f,.85f,.07f),new(.7f,1.47f,1.79f),"6d6252","wood_furniture");
        _prologueHutGlass=new StandardMaterial3D { AlbedoColor=new Color("a67742"),Roughness=.25f,
            EmissionEnabled=true,Emission=new Color("e0a457"),EmissionEnergyMultiplier=3.5f };
        hut.AddChild(new MeshInstance3D { Name="WarmWindow",Position=new(.7f,1.47f,1.73f),
            Mesh=new BoxMesh { Size=new(.88f,.84f,.018f) },MaterialOverride=_prologueHutGlass });
        _prologueHutLamp=new OmniLight3D { Name="WindowLight",Position=new(.7f,1.50f,1.96f),
            LightColor=new Color("ffc884"),LightEnergy=2.2f,OmniRange=11f,ShadowEnabled=GraphicsQuality.Preset=="high",LightSize=.22f,
            DistanceFadeEnabled=true,DistanceFadeBegin=52f,DistanceFadeShadow=16f,DistanceFadeLength=10f };
        _prologueHutLamp.AddToGroup(GraphicsQuality.SoftShadowLampGroup);
        hut.AddChild(_prologueHutLamp);
        var contact=new StaticBody3D { Name="ClosedHutContact",CollisionLayer=1,CollisionMask=0 };
        contact.AddChild(new CollisionShape3D { Position=new(0,1.18f,0),Shape=new BoxShape3D { Size=new(3.9f,2.5f,3.5f) } });
        hut.AddChild(contact);
        // The thicket batches own walking collisions. Keep the actionable face
        // on the controller's ray layer, clear of the closed door's solid face.
        _hutDoorTarget = new InteractionTarget
        {
            Name = "WinterHutKnock", CollisionLayer = 4, CollisionMask = 0,
            InteractionId = PrologueHutInteractionId, Prompt = "Постучать в дверь",
            PresentationBeforeDispatch = () => { KnockOnHut(); return false; }
        };
        _hutDoorTarget.ConfigureRayOnly();
        _hutDoorTarget.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.86f, 1.6f, .23f) } });
        AddChild(_hutDoorTarget);
        _hutDoorTarget.GlobalTransform = hut.GlobalTransform * new Transform3D(Basis.Identity, new(-.95f, 1.02f, 1.85f));
        // A few stacked firewood lengths are ordinary evidence of use.
        var stack=new Node3D { Name="CoveredFirewood",Position=new(2.3f,0,-.8f) };hut.AddChild(stack);
        for(var i=0;i<7;i++)RuralPropGeometry.Tube(stack,"SplitLog"+i,new(-.35f,.13f+i*.085f,-.32f),
            new(.35f,.13f+i*.085f,.32f),.075f,wood,8);
    }

    private async void KnockOnHut()
    {
        if (!_nightmareMoodApplied || _nightmareClimax || _prologueHutExtinguished
            || _prologueHutLamp is not { } lamp || _prologueHutGlass is not { } glass
            || _writingKnock || _hutDoorTarget is not { } door || _bridge is not { } bridge
            || bridge.SessionIdentity is not { } session || bridge.ActiveSceneId != PrologueSceneId) return;
        _writingKnock = true;
        try
        {
            if (!await bridge.DispatchWorldPropsAsync(new JsonArray(new JsonObject { ["propId"] = PrologueHutKey, ["knocked"] = true }))
                || !GodotObject.IsInstanceValid(this) || !IsInsideTree() || !GodotObject.IsInstanceValid(bridge)
                || !ReferenceEquals(bridge.SessionIdentity, session) || bridge.ActiveSceneId != PrologueSceneId) return;
        }
        catch (Exception e) { GD.PushWarning($"Could not record the knock: {e.Message}"); return; }
        finally { _writingKnock = false; }
        _prologueHutExtinguished = true;
        door.SetPresentationEnabled(false);
        UiFoley.PlayWorld(this, door.GlobalPosition, "wood_tap", -5f, 10f, 2f);
        lamp.LightEnergy = 0;
        glass.EmissionEnabled = false;
        glass.AlbedoColor = new Color("161a1b");
        _hutReplyBeat = 0;
        _hutKnockTime = _nightmareElapsed;
    }

    // No timer runs under the journal or during scene changes: the reply
    // advances on the same playable clock as the rest of the walk.
    private void ProcessPrologueHut()
    {
        if (!_prologueHutExtinguished || _hutReplyBeat >= 5 || _hutDoorTarget is not { } door) return;
        var wait = _hutReplyBeat switch { 0 => .7, 1 => 1.13, 2 => 1.66, 3 => 2.45, _ => 4.8 };
        if (_nightmareElapsed - _hutKnockTime < wait) return;
        if (_hutReplyBeat < 3)
        {
            UiFoley.PlayWorld(this, door.GlobalPosition, "wood_tap", -4f, 22f, 4f);
            if (_hutReplyBeat == 0 && _accessibility.Subtitles)
                ShowNightmareThought("[Стук с другой стороны двери]", 2.2);
        }
        else if (_hutReplyBeat == 3)
        {
            if (_hutHinge is { } hinge) hinge.CreateTween().TweenProperty(hinge,"rotation:y",.72f,.85).SetTrans(Tween.TransitionType.Sine);
            PlayPrologueForestVoice(new Vector2(PrologueHutPosition.X,PrologueHutPosition.Z),"forest-name-call","«Айдар…»",0f);
            UiFoley.PlayWorld(this, door.GlobalPosition, "door_creak", -6f, 20f, 3f);
            if (_hutDoorLatch is { } latch)
            {
                var lift = latch.CreateTween();
                lift.TweenProperty(latch, "rotation:z", -.6f, .24);
                lift.TweenInterval(.52);
                lift.TweenProperty(latch, "rotation:z", 0f, .12);
            }
        }
        else if (_bridge is { } bridge && bridge.SessionIdentity is { } session
            && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            RunNightmareClimaxAsync(bridge,session,player,atHut:true);
        _hutReplyBeat++;
    }

    private void BeginPrologueWaking()
    {
        ReleasePrologueWaking();
        ResetHutDoor();
        var layer=new CanvasLayer { Name="PrologueOpeningEyes",Layer=98 };AddChild(layer);
        _prologueOpeningEyes=layer;
        var dark=new ColorRect { Color=Colors.Black,AnchorRight=1,AnchorBottom=1,MouseFilter=Control.MouseFilterEnum.Ignore };
        layer.AddChild(dark);
        var open=layer.CreateTween();
        open.TweenInterval(.25);
        open.TweenProperty(dark,"color:a",0f,_accessibility.ReducedMotion?1.2:2.4).SetTrans(Tween.TransitionType.Sine);
        open.TweenCallback(Callable.From(layer.QueueFree));
        UiFoley.PlayWorld(this, GlobalPosition, "forest/wind_gust", -10f, 30f, 6f);
    }

    private void ResetHutDoor()
    {
        _prologueHutExtinguished=false;
        if (_hutHinge is { } hinge) hinge.Rotation=Vector3.Zero;
        _hutDoorTarget?.SetPresentationEnabled(true);
        if (_hutDoorLatch is { } latch) latch.Rotation = Vector3.Zero;
        if(_prologueHutLamp is { } lamp)lamp.LightEnergy=2.2f;
        if(_prologueHutGlass is { } glass){glass.EmissionEnabled=true;glass.AlbedoColor=new Color("a67742");}
    }

    private void ReleasePrologueWaking()
    {
        var layer=_prologueOpeningEyes;
        _prologueOpeningEyes=null;
        if(layer is not null && GodotObject.IsInstanceValid(layer))
        {
            layer.Hide();
            layer.QueueFree();
        }
    }

}
