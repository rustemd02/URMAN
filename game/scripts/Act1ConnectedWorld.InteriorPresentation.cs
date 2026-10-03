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
        // Uses the Act1ConnectedWorld._lifePlayer cache through LifePlayer(): the same
        // node the group query returned while it is alive, re-resolved only after it
        // was disposed. This pass runs before the cache line in _Process, so the
        // first frame still fills the cache here and no query is repeated after it.
        if (!IsBuilt || LifePlayer() is not { } player) return;
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
