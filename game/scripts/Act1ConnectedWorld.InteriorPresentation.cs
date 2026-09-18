using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal void ProjectLoadedPhysicalState()
    {
        _carryCoordinator?.ProjectLoadedState();
        ProjectLoadedFacilities();
        UpdateAct1Discoveries();
        ApplyShopSupplyState();
    }

    private string _physicalInterior = string.Empty;
    private AgentBAct1ExteriorLayer? _physicalWeather;
    private AmbientAudioDirector? _physicalAmbience;

    private void UpdatePhysicalInteriorPresentation()
    {
        if (!IsBuilt || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return;
        var interior = FacilityInteriorAt(player.GlobalPosition);
        if (interior == _physicalInterior) return;
        _physicalInterior = interior;
        _physicalWeather ??= GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        _physicalAmbience ??= GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        _physicalWeather.SetSheltered(interior.Length > 0);
        _physicalAmbience?.SetPhysicalShelter(interior.Length > 0);
        SetMeta("physicalInterior", interior);
    }
}
