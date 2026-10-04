using System.Text.Json;
using Godot;

namespace Urman.Godot;

/// <summary>Local, optional lobby remarks. No quest, save, fleet or resident simulation.</summary>
public partial class PolicePostPresentation : Node
{
    public Node3D Room=null!;
    public Node3D Officer=null!;
    public Node3D Mouse=null!;
    public Func<bool> IsActive=()=>true;
    private static JsonElement? _content;
    private static JsonElement Content => _content ??= LoadContent();
    private static JsonElement LoadContent()
    {
        using var document=JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString("res://content/world/act1_police_post.v1.json"));
        return document.RootElement.Clone();
    }
    public static string ContentText(string key)=>Content.GetProperty(key).GetString()!;
    private FirstPersonController? _player;
    private bool _wasInside;
    private double _elapsed,_next,_cooldown,_mouseTime;
    private int _line=-1;
    private Vector3 _mouseRest;
    public int GreetingsStarted {get;private set;}

    public override void _Ready()
    {
        _mouseRest=Mouse.Position;
        var played=AnimationCatalog.Play(Officer,"urman.anim:sit",0);
        if(!played.Played)throw new InvalidOperationException("Duty officer cannot sit: "+played.Problem);
        var animator=Officer.FindChildren("*",nameof(AnimationPlayer),true,false).OfType<AnimationPlayer>().Single();
        animator.Advance(0);
        var skeleton=Officer.FindChildren("*",nameof(Skeleton3D),true,false).OfType<Skeleton3D>().Single();
        var pelvis=skeleton.FindBone("pelvis");
        if(pelvis<0)throw new InvalidOperationException("Seated officer requires the pelvis bone.");
        var hip=Room.ToLocal(skeleton.ToGlobal(skeleton.GetBoneGlobalPose(pelvis).Origin));
        // Library root translation is owned by staging. Place the sitting pelvis
        // on this chair rather than retaining the standing ground-anchor height.
        var seatHip=new Vector3(-3.1f,.56f,1.43f);
        Officer.GlobalPosition+=Room.GlobalTransform.Basis*(seatHip-hip);
        Officer.SetMeta("seatedClip",played.Clip);
        Officer.SetMeta("seatContactHeight",.56f);
        GD.Print("police-seat: sourcePelvis="+hip+" mounted="+seatHip);
        SetProcess(true);
    }

    public void BeginGreeting()
    {
        _player ??=GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if(_player is null||_player.ModalOpen||!IsActive()||_line>=0)return;
        _line=0;_next=0;_cooldown=Content.GetProperty("greetingCooldownSeconds").GetDouble();
        GreetingsStarted++;SetMeta("greetingsStarted",GreetingsStarted);
        AnimationCatalog.Play(Officer,"urman.anim:sit-talk");
    }

    public override void _Process(double delta)
    {
        _elapsed+=delta;if(_elapsed<.25)return;var dt=_elapsed;_elapsed=0;
        _cooldown=Math.Max(0,_cooldown-dt);
        _player ??=GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if(_player is null)return;
        Officer.ProcessMode=Officer.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition)>1600 ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        var p=Room.ToLocal(_player.GlobalPosition);
        var inside=IsActive()&&Math.Abs(p.X)<5.78f&&Math.Abs(p.Z)<4.29f&&p.Y>-.2f&&p.Y<3;
        if(inside&&!_wasInside&&_cooldown<=0)BeginGreeting();
        _wasInside=inside;
        if(!inside||_player.ModalOpen)
        {
            if(_line>=0){_line=-1;AnimationCatalog.Play(Officer,"urman.anim:sit");}
            Mouse.Position=_mouseRest;return;
        }
        if(p.Z<-1.0f)
        {
            _mouseTime+=dt;
            Mouse.Rotation=new(0,Mathf.Sin((float)_mouseTime*1.2f)*.06f,0);
            Mouse.Position=_mouseRest+Vector3.Up*(Mathf.Sin((float)_mouseTime*3f)*.0012f);
        }
        if(_line<0)return;
        _next-=dt;if(_next>0)return;
        var lines=Content.GetProperty("lines");
        if(_line>=lines.GetArrayLength()){_line=-1;AnimationCatalog.Play(Officer,"urman.anim:sit");return;}
        _player.ShowRemark(ContentText("speaker"),lines[_line++].GetString()!);
        _next=6.1;
    }
}
