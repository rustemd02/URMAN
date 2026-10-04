using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private VillageHouseholdDirector? _households;
    private void BuildVillageHouseholds()
    {
        _households=new VillageHouseholdDirector {Name="InhabitedVillage"};
        AddChild(_households);_households.Initialize(this);
    }
    private void UpdateVillageHouseholds(double delta)
    {
        if(_households is null || LifePlayer() is not { } player)return;
        var camera=GetViewport().GetCamera3D();
        var audible=ActiveZoneId=="village_day" && _physicalInterior.Length==0 && !player.ModalOpen
            && _lifeCue?.IsPresenting!=true && !GetTree().Paused && !ArrivalFlyoverCameraActive();
        _households.Tick(delta,camera?.GlobalPosition??player.GlobalPosition,player.GlobalPosition,audible,
            ActiveZoneId=="kara_urman_night");
    }
}
