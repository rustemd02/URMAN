using Godot;

namespace Urman.Godot;

/// <summary>
/// Original procedural mechanical sound, distinct from radio speech/music.
/// Samples are generated at the physical vehicle and use the existing SFX bus.
/// This is an authored audio candidate, not a recorded Niva or field horse take.
/// </summary>
public partial class VehicleMechanicalAudio : Node3D
{
    private const int Rate = 22050;
    private AudioStreamPlayer3D? _player;
    private AudioStreamGeneratorPlayback? _playback;
    private readonly Vector2[] _buffer = new Vector2[2048];
    private VehicleKind _kind;
    private bool _running, _paused;
    private float _speed, _throttle, _gain;
    private double _phase, _wheelPhase, _hoofPhase;
    private float _hoofEnvelope, _wood, _lowNoise;
    private uint _noise = 0x7ac831u;

    public override void _Ready()
    {
        SetMeta("provenance", "original mathematical synthesis authored in VehicleMechanicalAudio.cs");
        SetMeta("listeningAcceptance", "not-run");
        AudioSettingsService.EnsureBuses();
        if (DisplayServer.GetName() == "headless") return;
        _player = new AudioStreamPlayer3D { Name="VehicleMechanicalSource",
            Bus=AudioSettingsService.SfxBus, VolumeDb=-10, UnitSize=3, MaxDistance=35,
            Stream = new AudioStreamGenerator { MixRate=Rate,BufferLength=.15f },Autoplay=false };
        AddChild(_player);
    }

    public void SetState(VehicleKind kind,bool running,float speed,float throttle,bool paused)
    { _kind=kind;_running=running;_speed=speed;_throttle=throttle;_paused=paused; }

    public void ResetForSession()
    {
        if(_player is not null){_player.Stop();_player.StreamPaused=false;}
        _playback=null;_running=_paused=false;_gain=_speed=_throttle=0;
        _phase=_wheelPhase=_hoofPhase=0;_hoofEnvelope=_wood=_lowNoise=0;
    }

    public override void _Process(double delta)
    {
        if(_player is null)return;
        _player.VolumeDb=GetTree().GetFirstNodeInGroup("audio_cue_ui") is AudioCueUi { IsPresenting:true } ? -18f : -10f;
        _player.StreamPaused=_paused;
        if(_paused)return;
        var shouldPlay=(_kind==VehicleKind.HorseCart?_speed>.02f:_running)||_gain>.001f;
        if(!shouldPlay){if(_player.Playing)_player.Stop();_playback=null;return;}
        if(!_player.Playing){_player.Play();_playback=_player.GetStreamPlayback() as AudioStreamGeneratorPlayback;}
        if(_playback is null)return;
        // One bounded batch per process; producer keeps ahead without an unbounded catch-up loop.
        var count=Math.Min(_playback.GetFramesAvailable(),_buffer.Length);
        if(count<=0)return;
        for(var i=0;i<count;i++){var sample=Next();_buffer[i]=new(sample,sample);}
        if(count==_buffer.Length)_playback.PushBuffer(_buffer);
        else for(var i=0;i<count;i++)_playback.PushFrame(_buffer[i]);
    }

    private float Next()
    {
        _noise^=_noise<<13;_noise^=_noise>>17;_noise^=_noise<<5;
        var noise=(_noise&65535)/32767.5f-1f;_lowNoise+=.06f*(noise-_lowNoise);
        var target=(_kind==VehicleKind.HorseCart?_speed>.02f:_running)?1f:0f;
        _gain+=(target-_gain)*.002f;
        if(_kind==VehicleKind.HorseCart)
        {
            _hoofPhase+=Math.Max(.0f,_speed)*1.45/Rate;
            if(_hoofPhase>=1){_hoofPhase-=1;_hoofEnvelope=.45f;_wood=.22f;}
            _hoofEnvelope*=.990f;_wood*=.997f;
            _phase+=390.0/Rate;
            _wheelPhase+=Math.Max(.0f,_speed)*2.2/Rate;
            var hoof=Math.Sin(_phase*Math.PI*2)*_hoofEnvelope+noise*_hoofEnvelope*.20;
            var axle=Math.Sin(_wheelPhase*Math.PI*2)*_wood*.35+_lowNoise*Math.Min(_speed,.7f)*.045;
            return (float)((hoof+axle)*_gain*.36);
        }
        var rpm=(_kind==VehicleKind.Motorcycle?1100f:780f)+_throttle*360f+_speed*210f;
        var hz=rpm/60f*(_kind==VehicleKind.Motorcycle?1f:2f);
        _phase=( _phase+hz/Rate )%1;
        _wheelPhase=(_wheelPhase+(_speed*.12+5)/Rate)%1;
        var angle=_phase*Math.PI*2;
        var exhaust=Math.Sin(angle)*.26+Math.Sin(angle*2+.7)*.11+Math.Sin(angle*3+1.2)*.055;
        var valve=noise*Math.Pow(Math.Max(0,Math.Sin(angle)),10)*.11;
        var load=1+.055*Math.Sin(_wheelPhase*Math.PI*2);
        return (float)((exhaust*load+valve+_lowNoise*.10)*_gain*.34);
    }

    public override void _ExitTree()
    { if(_player is not null&&GodotObject.IsInstanceValid(_player)){_player.Stop();_player.Stream=null;}_playback=null; }
}
