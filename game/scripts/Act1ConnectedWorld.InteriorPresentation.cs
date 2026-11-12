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

    private bool _mosqueAtmosphereInside;
    private string _physicalInterior = string.Empty;
    private AgentBAct1ExteriorLayer? _physicalWeather;
    private AmbientAudioDirector? _physicalAmbience;

    internal bool? PhysicalShelterForCurrentView
    {
        get
        {
            if (!IsBuilt || LifePlayer() is not { } player) return null;
            // Keep routing aligned with the presentation owner: a protected
            // capture follows its DevViewCamera; ordinary play follows the player.
            var viewer = GetViewport().GetCamera3D() is { } camera && camera.Name == "DevViewCamera"
                ? camera.GlobalPosition : player.GlobalPosition;
            return FacilityInteriorAt(viewer).Length > 0;
        }
    }

    private void UpdatePhysicalInteriorPresentation()
    {
        // Uses the Act1ConnectedWorld._lifePlayer cache through LifePlayer(): the same
        // node the group query returned while it is alive, re-resolved only after it
        // was disposed. This pass runs before the cache line in _Process, so the
        // first frame still fills the cache here and no query is repeated after it.
        if (!IsBuilt || LifePlayer() is not { } player) return;
        // A protected view capture flies its own camera into rooms while the player
        // stays parked outside; shelter follows that camera so a frame of the mosque
        // hall is not snowed on (station capture 10.10, mosque_hall).
        var viewer = GetViewport().GetCamera3D() is { } camera && camera.Name == "DevViewCamera"
            ? camera.GlobalPosition : player.GlobalPosition;
        var interior = FacilityInteriorAt(viewer);
        // The hall shelter AABB ends at its ceiling; it must not also cut the
        // visual warmth halfway up the enclosed minaret. This is presentation
        // only: sanctuary interactions, shelter and saved state keep their bounds.
        var mosqueAtmosphere = interior == "mosque";
        if (FacilityExteriorActive && _mosqueRoom is not null)
        {
            var local = _mosqueRoom.ToLocal(viewer) - MinaretCentre;
            mosqueAtmosphere |= local.X * local.X + local.Z * local.Z < 1.30f * 1.30f
                && local.Y > -.25f && local.Y < MinaretGalleryHeight;
        }
        var gradeChanged = mosqueAtmosphere != _mosqueAtmosphereInside;
        _mosqueAtmosphereInside = mosqueAtmosphere;
        if (interior == _physicalInterior)
        {
            if (gradeChanged) RefreshIndoorAtmosphereGrade();
            return;
        }
        _physicalInterior = interior;
        _physicalWeather ??= GetNode<AgentBAct1ExteriorLayer>("Act1CoreWorldGreybox/AgentBExteriorWorld");
        _physicalAmbience ??= GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector;
        _physicalWeather.SetSheltered(interior.Length > 0);
        _physicalAmbience?.SetPhysicalShelter(interior.Length > 0);
        RefreshIndoorAtmosphereGrade();
        SetMeta("physicalInterior", interior);
    }
}
