using Godot;

namespace Urman.Godot;

public partial class W01Controller
{
    private static Vector3 PrologueHutPosition => PrologueRoutePoint(PrologueRouteLength * .58f, -6.8f);
    private OmniLight3D? _prologueHutLamp;
    private StandardMaterial3D? _prologueHutGlass;
    private bool _prologueHutExtinguished;
    private bool _prologueHutNoticed;
    private bool _prologueHutPassed;
    private CanvasLayer? _prologueOpeningEyes;

    private void BuildPrologueForestLandmarks(Node3D owner)
    {
        var hut=new Node3D { Name="ForestersWinterHut",Position=PrologueHutPosition };
        var towards=PrologueRoutePoint(PrologueRouteLength*.58f,0)-hut.Position;
        towards.Y=0;
        hut.Basis=Basis.LookingAt(-towards.Normalized(),Vector3.Up);
        owner.AddChild(hut);
        var wood=PainterlyMaterialLibrary.ForColor("655346","wood_facade");
        using var logs=new SurfaceTool(); logs.Begin(Mesh.PrimitiveType.Triangles);
        using var round=new CylinderMesh { TopRadius=.105f,BottomRadius=.105f,Height=1f,RadialSegments=10,Rings=1 };
        void Log(Vector3 a,Vector3 b)
        {
            var delta=b-a;
            logs.AppendFrom(round,0,new Transform3D(new Basis(new Quaternion(Vector3.Up,delta.Normalized()))
                *Basis.FromScale(new Vector3(1,delta.Length(),1)),(a+b)*.5f));
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
        hut.AddChild(new MeshInstance3D { Name="JoinedLogWalls",Mesh=logs.Commit(),MaterialOverride=wood });
        void Box(string name,Vector3 size,Vector3 at,string color,string material,Vector3 rotation=default)
        {
            hut.AddChild(new MeshInstance3D { Name=name,Position=at,Rotation=rotation,
                Mesh=RuralPropGeometry.BevelBox(size,.006f),MaterialOverride=PainterlyMaterialLibrary.ForColor(color,material) });
        }
        Box("LowStoneFooting",new(4.03f,.20f,3.53f),new(0,0,0),"77716a","stone");
        Box("ClosedBoardDoor",new(.88f,1.86f,.065f),new(-.95f,.98f,1.72f),"514438","wood_furniture");
        for(var i=0;i<5;i++)Box("DoorBoard"+i,new(.16f,1.82f,.025f),new(-1.29f+i*.17f,.98f,1.76f),"675543","wood_furniture");
        Box("DoorLatch",new(.15f,.025f,.06f),new(-.66f,1.0f,1.80f),"6f716c","metal");
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
            EmissionEnabled=true,Emission=new Color("e0a457"),EmissionEnergyMultiplier=1.3f };
        hut.AddChild(new MeshInstance3D { Name="WarmWindow",Position=new(.7f,1.47f,1.73f),
            Mesh=new BoxMesh { Size=new(.88f,.84f,.018f) },MaterialOverride=_prologueHutGlass });
        _prologueHutLamp=new OmniLight3D { Name="WindowLight",Position=new(.7f,1.50f,1.96f),
            LightColor=new Color("ffc884"),LightEnergy=.75f,OmniRange=8f,ShadowEnabled=GraphicsQuality.Preset=="high",LightSize=.22f,
            DistanceFadeEnabled=true,DistanceFadeBegin=36f,DistanceFadeShadow=16f,DistanceFadeLength=10f };
        _prologueHutLamp.AddToGroup(GraphicsQuality.SoftShadowLampGroup);
        hut.AddChild(_prologueHutLamp);
        var contact=new StaticBody3D { Name="ClosedHutContact",CollisionLayer=1,CollisionMask=0 };
        contact.AddChild(new CollisionShape3D { Position=new(0,1.18f,0),Shape=new BoxShape3D { Size=new(3.9f,2.5f,3.5f) } });
        hut.AddChild(contact);
        // A few stacked firewood lengths are ordinary evidence of use.
        var stack=new Node3D { Name="CoveredFirewood",Position=new(2.3f,0,-.8f) };hut.AddChild(stack);
        for(var i=0;i<7;i++)RuralPropGeometry.Tube(stack,"SplitLog"+i,new(-.35f,.13f+i*.085f,-.32f),
            new(.35f,.13f+i*.085f,.32f),.075f,wood,8);
    }

    private void ProcessPrologueHut(FirstPersonController player,float progress)
    {
        var local=ToLocal(player.GlobalPosition);
        var distance=new Vector2(local.X-PrologueHutPosition.X,local.Z-PrologueHutPosition.Z).Length();
        if(!_prologueHutNoticed&&distance<24f)
        { _prologueHutNoticed=true;ShowNightmareThought("В окне свет. Попробую подойти.",4); }
        if(!_prologueHutExtinguished&&distance<5.3f&&_prologueHutLamp is { } lamp&&_prologueHutGlass is { } glass)
        {
            _prologueHutExtinguished=true;
            lamp.LightEnergy=0;glass.EmissionEnabled=false;glass.AlbedoColor=new Color("161a1b");
            UiFoley.PlayWorld(this,lamp.GlobalPosition,"wood_tap",-15f,18f,3f);
            ShowNightmareThought("Погас.",2.5);
            KnockFromInsideTheHut(lamp.GlobalPosition);
        }
        if(!_prologueHutPassed&&progress>PrologueRouteLength*.68f)
        { _prologueHutPassed=true;ShowNightmareThought("Тропа уходит между деревьями. Держаться следов.",4); }
    }

    // The light is out. Then, from inside, three slow knocks on the glass, and the door gives a little.
    private async void KnockFromInsideTheHut(Vector3 at)
    {
        var session = _nightmareSession;
        foreach (var pause in new[] { 2.6, 1.1, 1.1 })
        {
            await ToSignal(GetTree().CreateTimer(pause), SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || _nightmareClimax || !ReferenceEquals(session, _nightmareSession)) return;
            UiFoley.PlayWorld(this, at, "wood_tap", -4f, 22f, 4f);
        }
        await ToSignal(GetTree().CreateTimer(1.8), SceneTreeTimer.SignalName.Timeout);
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || _nightmareClimax || !ReferenceEquals(session, _nightmareSession)) return;
        UiFoley.PlayWorld(this, at, "door_creak", -6f, 22f, 4f);
        ShowNightmareThought("Не оборачивайся. Иди.", 3);
    }

    private void BeginPrologueWaking()
    {
        ReleasePrologueWaking();
        _prologueHutNoticed=_prologueHutExtinguished=_prologueHutPassed=false;
        if(_prologueHutLamp is { } lamp)lamp.LightEnergy=.75f;
        if(_prologueHutGlass is { } glass){glass.EmissionEnabled=true;glass.AlbedoColor=new Color("a67742");}
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

    private void BuildPrologueBackdropGround(Node3D owner,Aabb bounds)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        void Strip(float x0,float z0,float x1,float z1)
        {
            var nx=Mathf.CeilToInt((x1-x0)/4f);var nz=Mathf.CeilToInt((z1-z0)/4f);
            Vector3 Point(int x,int z)
            {
                var px=Mathf.Lerp(x0,x1,(float)x/nx);var pz=Mathf.Lerp(z0,z1,(float)z/nz);
                return new(px,ForestGroundHeight(px,pz)-.015f,pz);
            }
            for(var x=0;x<nx;x++)for(var z=0;z<nz;z++)
            foreach(var p in new[]{Point(x,z),Point(x,z+1),Point(x+1,z),Point(x+1,z),Point(x,z+1),Point(x+1,z+1)})
            {surface.SetUV(new(p.X*.25f,p.Z*.25f));surface.AddVertex(p);}
        }
        var a=bounds.Position;var b=bounds.End;
        Strip(a.X-32,a.Z-32,b.X+32,a.Z);Strip(a.X-32,b.Z,b.X+32,b.Z+32);
        Strip(a.X-32,a.Z,a.X,b.Z);Strip(b.X,a.Z,b.X+32,b.Z);
        surface.GenerateNormals();
        owner.AddChild(new MeshInstance3D { Name="ForestBeyondTrail",Mesh=surface.Commit(),
            MaterialOverride=PainterlyMaterialLibrary.ForColor("b9c3cf","snow_ground"),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
