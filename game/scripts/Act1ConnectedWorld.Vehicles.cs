namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    public VehicleFleet? VehicleFleet { get; private set; }

    private void BuildAct1Vehicles()
    {
        if(VehicleFleet is not null)return;
        var fleet=new VehicleFleet();fleet.Configure(this);
        VehicleFleet=fleet;AddChild(fleet);
    }
}

