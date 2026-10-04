using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Sound-mood layers, the panel's weather on/off switch and the licensed adhan
/// hook. Human listening remains external; this checks the real streams,
/// gains and state transitions only.
/// </summary>
public partial class Act1SoundMoodSmokeTest : Node
{
    public override async void _Ready()
    {
        var code=1;Main? main=null;
        try
        {
            main=ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.EnableAct1ConnectedWorld=true;
            AddChild(main);await Frames(5);
            main.SwitchZone("village_day","default");await Frames(3);
            var world=main.ConnectedWorld??throw new InvalidOperationException("missing connected world");
            var mood=world.GetNode<VillageSoundMoodDirector>("VillageSoundMood");
            var life=world.GetNode<VillageHouseholdDirector>("InhabitedVillage");
            var bridge=(RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var state=bridge.SelectRuntimeState().GetRawText();
            Require(mood.VillageLayerPresent&&mood.DreadLayerPresent,"village life and dread layers load");
            var lifePlayer=mood.GetNode<AudioStreamPlayer>("VillageLifeLayer");
            Require(lifePlayer.Stream is AudioStreamWav{ LoopMode: AudioStreamWav.LoopModeEnum.Forward }&&lifePlayer.VolumeDb>-60f,
                "life layer loops and is audible at the authored mood");
            Require(mood.AdhanRecordingReady&&world.AdhanRecordingReady,"licensed adhan recording is present");
            Require(Math.Abs(mood.Mood-VillageSoundMoodDirector.DefaultMood)<.001f&&Math.Abs(life.Mood-mood.Mood)<.001f,
                "authored ordinary-village mood is shared with household events");
            mood.SetMood(1f);var cozyVillage=mood.VillageLayerDb;var cozyDread=mood.DreadLayerDb;
            mood.SetMood(0f);
            Require(mood.VillageLayerDb<cozyVillage&&mood.DreadLayerDb>cozyDread,"mood scale moves both layers in opposite directions");
            // Deep dread keeps the houses quiet; the layers carry the scene.
            var pane=world.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
                .First(p=>p.HasMeta("occupiedWindow")&&p.IsVisibleInTree());
            var at=pane.GlobalTransform*pane.Mesh!.GetAabb().GetCenter();
            var before=life.EventsStarted;
            for(var i=0;i<40;i++) life.Tick(.25,at,at,true,false);
            Require(life.EventsStarted==before,"deep dread keeps the houses quiet");
            mood.SetMood(VillageSoundMoodDirector.DefaultMood);
            for(var i=0;i<40;i++) life.Tick(.25,at,at,true,false);
            Require(life.EventsStarted>before,"authored mood brings household events back");
            // The panel's blizzard switch is a whole-bed on/off, not a stop.
            var ambient=(AmbientAudioDirector)GetTree().GetFirstNodeInGroup("ambient_audio");
            mood.SetWeatherEnabled(false);
            Require(!mood.WeatherEnabled&&!ambient.BedEnabled,"weather bed switches off");
            mood.SetWeatherEnabled(true);
            Require(mood.WeatherEnabled&&ambient.BedEnabled,"weather bed switches back on");
            // The adhan plays from the real minaret anchor and stops cleanly.
            Require(world.TryPlayAdhan(),"adhan starts at the mosque azanchi point");
            var adhanPlayer=world.GetNode<AudioStreamPlayer3D>("MosqueAdhanPlayer");
            Require(adhanPlayer.Stream is not null&&world.GetMeta("adhanState","").AsString()=="playing-licensed-recording",
                "adhan stream and state are real");
            world.StopAdhan();
            Require(!world.AdhanPlaying,"adhan stops cleanly");
            Require(bridge.SelectRuntimeState().GetRawText()==state,"sound mood changes no knowledge or quest state");
            GD.Print($"act1-sound-mood: PASS villageDb={mood.VillageLayerDb:0.0} dreadDb={mood.DreadLayerDb:0.0} weather={mood.WeatherEnabled} adhanReady={mood.AdhanRecordingReady}; human listening not asserted");
            code=0;
        }
        catch(Exception error){GD.PrintErr("act1-sound-mood: FAIL "+error);}
        finally
        {
            if(main is not null)await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(code);
        }
    }
    private static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
}
