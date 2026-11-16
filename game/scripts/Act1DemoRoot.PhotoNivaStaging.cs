using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private bool _photoNivaStagedArrival, _photoNivaStagedPlaced;
    private string? _photoNivaArrivalFailure;
    private DirectionalLight3D? _photoNivaStagingSun;
    // Stop before the open village seam if preparation exceeds the real approach.
    private const float PhotoNivaStagingStopBeforeEnd = 55f;

    private VehicleController? FindParkedPhotoNiva()
    {
        var niva = (GetTree().GetFirstNodeInGroup("vehicle_fleet") as VehicleFleet)?.Vehicles
            .SingleOrDefault(vehicle => vehicle.Definition.Id == "babay-niva" && vehicle.Definition.Kind == VehicleKind.Niva);
        return niva is not null && IsInstanceValid(niva) && niva.IsInsideTree() && niva.Driver is null ? niva : null;
    }

    private void BindParkedPhotoNiva(VehicleController niva)
    {
        _parkedNivaForRide = niva;
        _parkedNivaWasVisible = niva.Visible;
        niva.Visible = false;
    }

    private bool TryPlacePhotoNivaPassengerExit(object session)
    {
        if (!IsCurrentPhotoNivaArrival(session) || _photoNivaDriving || !TryPreparePhotoNivaArrival()
            || _photoNivaPlayer is not { } player || _parkedNivaForRide is not { } niva
            || !IsInstanceValid(niva) || !niva.IsInsideTree()
            || !niva.TryFindSafePassengerExit(player, out var feet)) return false;
        // The last house-path point is the open gate in WalkChain, transformed
        // by BabaiRelocation. Face that real route, not a compass default.
        var gate = AgentBAct1Layout.HousePathAxis[^1];
        var direction = gate - new Vector2(feet.X, feet.Z);
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Y));
        if (!_main.TryPlaceNivaPassengerExit(session, feet, yaw)) return false;
        GD.Print($"act1-niva-passenger-exit: feet={feet} yaw={yaw:0.00}");
        return true;
    }

    private void BuildPhotoNivaStagingLight()
    {
        if (!_photoNivaStagedArrival || _rideCamera is not { } camera) return;
        camera.Environment = AgentBAct1ExteriorLayer.CreateExteriorEnvironment();
        GraphicsQuality.ConfigureEnvironment(camera.Environment);
        _photoNivaStagingSun = AgentBAct1ExteriorLayer.CreateExteriorSun();
        _photoNivaStagingSun.Name = "PhotoNivaApproachSun";
        _main.AddChild(_photoNivaStagingSun);
        GraphicsQuality.ConfigureSun(_photoNivaStagingSun);
    }

    private void ReleasePhotoNivaStagingLight()
    {
        if (_rideCamera is { } camera && IsInstanceValid(camera)) camera.Environment = null;
        if (_photoNivaStagingSun is { } sun && IsInstanceValid(sun))
        {
            sun.Visible = false;
            sun.QueueFree();
        }
        _photoNivaStagingSun = null;
    }

    private bool TryPreparePhotoNivaArrival()
    {
        if (!_photoNivaStagedArrival || _photoNivaStagedPlaced) return true;
        if (_photoNivaArrivalFailure is not null || _photoNivaSession is not { } session
            || !IsCurrentPhotoNivaArrival(session, allowLiveMenu: true)) return false;
        if (_main.ArrivalVillageFailure(session) is { } failure)
        {
            SetPhotoNivaStagedFailure(failure);
            return false;
        }
        if (!_main.ArrivalVillageReady(session) || !_main.CompleteStagedArrivalPlacement(session)) return false;
        if (FindParkedPhotoNiva() is not { } niva)
        {
            SetPhotoNivaStagedFailure("The prepared arrival has no available authored Niva.");
            return false;
        }
        BindParkedPhotoNiva(niva);
        _photoNivaStopTransform = niva.GlobalTransform;
        _photoNivaStagedPlaced = true;
        ReleasePhotoNivaStagingLight();
        return true;
    }

    private void SetPhotoNivaStagedFailure(string reason)
    {
        if (_photoNivaArrivalFailure is not null) return;
        _photoNivaArrivalFailure = reason;
        _photoNivaEvidenceClock = -1f;
        StopRideRadio(.4);
        PrologueVoice.Stop();
        ShowPrologueCaption(string.Empty, .01);
        GD.PushWarning($"PhotoWorld staged arrival failed: {reason}");
    }

    private void HoldPhotoNivaStagedFailure()
    {
        _photoNivaDriveSpeed = 0f;
        _photoNivaBars = 0f;
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Transparent;
        LayoutPhotoNivaFrame();
        if (_photoNivaEvidenceCue is not { } cue || !IsInstanceValid(cue)) return;
        cue.Visible = true;
        cue.Text = "Не удалось подготовить деревню.\n"
            + InputBindingService.ActionHint("pause", _photoNivaPlayer?.CurrentInputDevice == "gamepad") + " — Меню";
    }

    private bool PhotoNivaWaitingAtVillage => _photoNivaStagedArrival && !_photoNivaStagedPlaced
        && _photoNivaApproachDistance >= PhotoNivaApproachLength - PhotoNivaStagingStopBeforeEnd - .1f;
}
