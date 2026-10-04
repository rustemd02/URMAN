using Godot;

namespace Urman.Godot.Tests;

/// <summary>Actual village resources and bounded playback across its real houses.</summary>
public partial class Act1HouseholdLifeSmokeTest : Node
{
    public override async void _Ready()
    {
        var code=1;Main? main=null;
        try
        {
            main=ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            main.EnableAct1ConnectedWorld=true;
            AddChild(main);await Frames(5);
            var world=main.ConnectedWorld??throw new InvalidOperationException("missing connected world");
            var life=world.GetNode<VillageHouseholdDirector>("InhabitedVillage");
            var bridge=(RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var state=bridge.SelectRuntimeState().GetRawText();
            Require(life.HouseCount>=35 && life.WindowCount>=100,"actual open-part households and side/rear windows covered");
            Require(life.MotifCount==VillageHouseholdDirector.ExpectedMotifCount && life.MotifIds.Distinct().Count()==VillageHouseholdDirector.ExpectedMotifCount,"three requested motifs plus the additional winter household events");
            var lights=life.GetChildren().OfType<SpotLight3D>().ToArray();
            Require(lights.Length==VillageHouseholdDirector.SpillBudget && lights.All(l=>l.ShadowEnabled && l.SpotRange<5),"finite shadowed spill pool, independent of window count");
            var panes=world.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
                .Where(m=>m.HasMeta("occupiedWindow") && m.IsVisibleInTree()).ToArray();
            Require(panes.Length>=life.WindowCount && panes.All(p=>p.MaterialOverride is ShaderMaterial),"all marked glazing has textured emissive curtain material");
            Require(panes.Select(p=>p.MaterialOverride!.GetRid().Id).Distinct().Count()<=8,"window materials shared across houses");
            Require(life.LoadedClipCount==0,"no preloaded household audio bank");
            // The real player and normal physical positions remain the integration
            // context. Only the presentation clock is accelerated; no story time.
            var player=main.GetNode<FirstPersonController>("Player");
            player.SetModalOpen(true);world.SetProcess(false);
            var peaks=(Clips:0,Bytes:0,Voices:0,Lights:0);
            foreach(var pane in panes.Where(p=>p.Name.ToString().Contains("Glass",StringComparison.Ordinal)).Take(220))
            {
                var position=pane.GlobalTransform*pane.Mesh!.GetAabb().GetCenter();
                for(var tick=0;tick<40;tick++)
                {
                    life.Tick(.25,position+Vector3.Back*2,position,true,false);
                    peaks=(Math.Max(peaks.Clips,life.LoadedClipCount),Math.Max(peaks.Bytes,life.LoadedPcmBytes),
                        Math.Max(peaks.Voices,life.PlayingVoiceCount),Math.Max(peaks.Lights,life.VisibleSpillCount));
                    Require(life.LoadedClipCount<=8 && life.LoadedPcmBytes<=2_000_000 && life.PlayingVoiceCount<=4 && life.VisibleSpillCount<=6,"runtime pool/cache budgets during village traversal");
                }
            }
            Require(life.EventsStarted>VillageHouseholdDirector.ExpectedMotifCount && peaks.Clips>1,"actual nearby events run and cache evicts as the route changes");
            foreach(var light in lights.Where(l=>l.Visible))
            {
                var pane=GetNode<MeshInstance3D>(light.GetMeta("sourcePane").AsString());
                var centre=pane.GlobalTransform*pane.Mesh!.GetAabb().GetCenter();
                Require(light.GlobalPosition.DistanceTo(centre)<.2f,"pooled light follows the actual window after household relocation");
            }
            var far=new Vector3(10000,10000,10000);
            life.Tick(.5,far,far,true,false);
            Require(life.VisibleSpillCount==0 && life.PlayingVoiceCount==0,"distant homes have no active lights or audio");
            var count=life.EventsStarted;
            life.Tick(.5,panes[0].GlobalPosition,panes[0].GlobalPosition,false,true);
            Require(life.PlayingVoiceCount==0 && life.EventsStarted==count,"dialogue/shelter/pause gate stops all household voices");
            Require(bridge.SelectRuntimeState().GetRawText()==state,"ambience changes no quests, knowledge or persistent runtime state");
            await CaptureIfRequested(main,world,life);
            GD.Print($"act1-household-life: PASS homes={life.HouseCount} panes={life.WindowCount} motifs={life.MotifCount} events={life.EventsStarted} peakClips={peaks.Clips} peakPCMBytes={peaks.Bytes} peakVoices={peaks.Voices} peakLights={peaks.Lights}; human listening/art and FPS not asserted");
            code=0;
        }
        catch(Exception error){GD.PrintErr("act1-household-life: FAIL "+error);}
        finally
        {
            if(main is not null)await GodotSmokeCleanup.ReleaseAsync(main);
            GetTree().Quit(code);
        }
    }
    private static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}

    private async Task CaptureIfRequested(Main main,Act1ConnectedWorld world,VillageHouseholdDirector life)
    {
        var directory=System.Environment.GetEnvironmentVariable("URMAN_HOUSEHOLD_CAPTURE");
        if(string.IsNullOrWhiteSpace(directory) || DisplayServer.GetName()=="headless")return;
        System.IO.Directory.CreateDirectory(directory);
        var facade=world.FindChild("BabaiApproachDwellingFacade",true,false) as Node3D
            ??throw new InvalidOperationException("missing actual Babai facade");
        var pane=facade.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
            .First(p=>p.Mesh is not null && p.Name.ToString().Contains("Street_Window2_Glass",StringComparison.Ordinal));
        var bounds=pane.Mesh!.GetAabb();var centre=pane.GlobalTransform*bounds.GetCenter();
        var normal=pane.GlobalBasis*(bounds.Size.X<bounds.Size.Z?Vector3.Right:Vector3.Back);
        normal.Y=0;normal=normal.Normalized();if(normal.Dot(centre-facade.GlobalPosition)<0)normal=-normal;
        var camera=new Camera3D {Name="HouseholdReviewCamera",Fov=70,Far=120};main.AddChild(camera);
        camera.GlobalPosition=centre+normal*8+Vector3.Up*.65f;camera.LookAt(centre);camera.MakeCurrent();
        DisplayServer.WindowMoveToForeground();
        foreach(var (name,preset,night) in new[]{("day_high","high",false),("day_low","low",false),("night_high","high",true)})
        {
            main.SwitchZone(night?"kara_urman_night":"village_day",night?"village_path":"from_house");
            camera.MakeCurrent();GraphicsQuality.Apply(GetViewport(),preset);
            life.Tick(.5,camera.GlobalPosition,camera.GlobalPosition,false,night);
            await Frames(45);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var image=GetViewport().GetTexture().GetImage();
            Require(image.SavePng(System.IO.Path.Combine(directory,name+".png"))==Error.Ok,"native window capture saved");
            GD.Print($"household-capture: {name} preset={GraphicsQuality.Preset} size={image.GetWidth()}x{image.GetHeight()} spills={life.VisibleSpillCount}");
        }
    }
}
