using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private VillageHouseholdDirector? _households;
    private VillageSoundMoodDirector? _soundMood;
    private void BuildVillageHouseholds()
    {
        _households=new VillageHouseholdDirector {Name="InhabitedVillage"};
        AddChild(_households);_households.Initialize(this);
        _soundMood=new VillageSoundMoodDirector {Name="VillageSoundMood"};
        AddChild(_soundMood);
        _soundMood.Initialize(_households,GetTree()?.GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector);
    }
    private void UpdateVillageHouseholds(double delta)
    {
        if(_households is null || LifePlayer() is not { } player)return;
        var camera=GetViewport().GetCamera3D();
        var audible=ActiveZoneId=="village_day" && _physicalInterior.Length==0 && !player.ModalOpen
            && _lifeCue?.IsPresenting!=true && !GetTree().Paused && !ArrivalFlyoverCameraActive();
        _households.Tick(delta,camera?.GlobalPosition??player.GlobalPosition,player.GlobalPosition,audible,
            ActiveZoneId=="kara_urman_night");
        _soundMood?.Tick(delta,ActiveZoneId,audible);
    }
}
