using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private const float PhotoNivaApproachStartZ = -160f;
    private const float PhotoNivaApproachEndZ = PrologueApproachRoad.RoadEndZ;
    private const float PhotoNivaApproachLength = PhotoNivaApproachStartZ - PhotoNivaApproachEndZ;
    private bool _photoNivaDriving;
    private float _photoNivaDriveElapsed;
    private float _photoNivaApproachDistance;
    private double _photoNivaWakeCaptionSeconds;
    private bool _photoNivaVillageStarted;
    private float _photoNivaVillageDistance, _photoNivaVillageLength, _photoNivaDriveSpeed;
    private Vector3[] _photoNivaVillagePath = Array.Empty<Vector3>();
    private bool _photoNivaPlayerWasVisible;
    private Transform3D _photoNivaStopTransform;
    private VehicleVisualFactory.Visual? _photoNivaDriveVisual;
    private VehicleMechanicalAudio? _photoNivaDriveAudio;
    private bool PhotoNivaDrivePaused => MainMenuVisible || _photoNivaPlayer?.ModalOpen != false || _pauseMenu?.IsOpen == true
        || !DisplayServer.WindowIsFocused()
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private void BeginPhotoNivaDrive(VehicleVisualFactory.Visual visual)
    {
        // Continue the authored northern street out of the neighborhood, then
        // follow the existing bridge/FAP/street route to the house.
        var pullOverAt = Array.FindIndex(PrologueRidePath, point => point == new Vector3(1.3f, 0f, 10f));
        if (pullOverAt < 0) throw new InvalidOperationException("The Niva arrival street is missing from the ride route.");
        var northRoad = Act1ConnectedWorld.FarBankPlot().Roads.Single(road => road.Id == "yar-north");
        var northernApproach = northRoad.Points.Skip(3).Reverse()
            .Select(point => new Vector3(point.X, 0f, point.Y));
        _photoNivaVillagePath = northernApproach.Concat(PrologueRidePath.Take(pullOverAt + 1))
            .Concat(new[] { new Vector3(-1.3f, 0f, 13f), new Vector3(-1.3f, 0f, 17f) }).ToArray();
        // This live opening is presentation only: the player's validated arrival
        // pose remains untouched, so Continue reconstructs the stationary cabin.
        _photoNivaDriving = true;
        _photoNivaDriveElapsed = 0f;
        _photoNivaApproachDistance = 0f;
        _photoNivaVillageStarted = false;
        _photoNivaVillageDistance = _photoNivaVillageLength = 0f;
        for (var index = 1; index < _photoNivaVillagePath.Length; index++)
            _photoNivaVillageLength += _photoNivaVillagePath[index - 1].DistanceTo(_photoNivaVillagePath[index]);
        _photoNivaDriveSpeed = 8.5f;
        _photoNivaDriveVisual = visual;
        _photoNivaStopTransform = visual.Root.GlobalTransform;
        if (_photoNivaPlayer is { } player)
        {
            _photoNivaPlayerWasVisible = player.Visible;
            player.Visible = false;
        }
        _approachRoad = new PrologueApproachRoad { TrimGroundAtRoadEnd = true };
        _main.AddChild(_approachRoad);
        AlignPhotoNivaApproachToVillage(_approachRoad);
        PlacePhotoNivaOnApproach(_approachRoad, PhotoNivaApproachStartZ, snap: true);
        _photoNivaDriveAudio = new VehicleMechanicalAudio { Name = "PhotoNivaDriveAudio" };
        visual.Root.AddChild(_photoNivaDriveAudio);
        _photoNivaDriveAudio.SetState(VehicleKind.Niva, true, 8.5f, .35f, false);
        BuildPrologueOverlay("пропустить поездку", "skip_prologue");
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Black;
        PrologueVoice.SetPaused(false);
        var waking = PrologueVoice.PlayClip(this, "forest-wake-v2");
        _photoNivaWakeCaptionSeconds = Math.Max(5, waking + .3);
        if (_prologueCaption is { } caption)
        {
            caption.Text = "Мансур бабай: «Айдар… Айдар, уян. Килеп җитәбез.»\n(Айдар… Айдар, проснись. Уже подъезжаем.)";
            caption.Visible = true;
        }
        HideRouteCue();
    }

    private void UpdatePhotoNivaDrive(float delta)
    {
        var paused = PhotoNivaDrivePaused;
        PrologueVoice.SetPaused(paused);
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, true, _photoNivaDriveSpeed, .35f, paused);
        var metresToStop = (_photoNivaVillageStarted ? 0f : PhotoNivaApproachLength - _photoNivaApproachDistance)
            + (_photoNivaVillageLength - _photoNivaVillageDistance);
        UpdatePhotoNivaScore(delta, paused, metresToStop);
        if (_prologueOverlay is { } overlay)
            overlay.Visible = !MainMenuVisible && _photoNivaPlayer?.ModalOpen == false && _pauseMenu?.IsOpen != true;
        if (paused) return;

        // A focus change or slow import must not jump the entire presentation.
        delta = Mathf.Clamp(delta, 0f, .1f);
        _photoNivaDriveElapsed += delta;
        var t = _photoNivaDriveElapsed;
        if (_prologueCaption is { } caption) caption.Visible = t < _photoNivaWakeCaptionSeconds;
        if (!_photoNivaVillageStarted)
        {
            if (_approachRoad is { } road && _photoNivaDriveVisual is { } visual)
            {
                var remaining = PhotoNivaApproachLength - _photoNivaApproachDistance;
                var targetSpeed = Mathf.Min(8.5f, Mathf.Sqrt(4.2f * 4.2f + 2f * 1.4f * Mathf.Max(0f, remaining)));
                _photoNivaDriveSpeed = Mathf.MoveToward(_photoNivaDriveSpeed, targetSpeed, 1.4f * delta);
                _photoNivaApproachDistance = Mathf.Min(PhotoNivaApproachLength,
                    _photoNivaApproachDistance + _photoNivaDriveSpeed * delta);
                var before = visual.Root.GlobalPosition;
                PlacePhotoNivaOnApproach(road, PhotoNivaApproachStartZ - _photoNivaApproachDistance, snap: false);
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, _photoNivaDriveSpeed);
            }
            if (_prologueBlackout is { } blackout)
                blackout.Color = Colors.Black with { A = 1f - Mathf.Clamp(t / .7f, 0f, 1f) };
            if (_photoNivaApproachDistance >= PhotoNivaApproachLength)
            {
                _photoNivaVillageStarted = true;
                _photoNivaDriveSpeed = 4.2f;
                // The road endpoint and this point share the same world pose;
                // hand the route owner across without a blackout or relocation.
                PlaceRideAt(0f, eased: false, route: _photoNivaVillagePath);
            }
        }
        else
        {
            var remaining = _photoNivaVillageLength - _photoNivaVillageDistance;
            // Brake along the final straight into the existing parked pose.
            var targetSpeed = Mathf.Min(4.2f, Mathf.Sqrt(2f * 1.4f * Mathf.Max(0f, remaining)));
            _photoNivaDriveSpeed = Mathf.MoveToward(_photoNivaDriveSpeed, targetSpeed, 1.4f * delta);
            _photoNivaVillageDistance = Mathf.Min(_photoNivaVillageLength,
                _photoNivaVillageDistance + Mathf.Max(.2f, _photoNivaDriveSpeed) * delta);
            if (_photoNivaDriveVisual is { } visual)
            {
                var before = visual.Root.GlobalPosition;
                PlaceRideAt(_photoNivaVillageDistance / _photoNivaVillageLength, eased: false, route: _photoNivaVillagePath);
                // The parked controller grounds its whole hull rather than the
                // road centre. Ease only that height difference into the last 4m.
                var settle = Mathf.Clamp(1f - (_photoNivaVillageLength - _photoNivaVillageDistance) / 4f, 0f, 1f);
                visual.Root.GlobalPosition = visual.Root.GlobalPosition with
                {
                    Y = Mathf.Lerp(visual.Root.GlobalPosition.Y, _photoNivaStopTransform.Origin.Y, settle)
                };
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, _photoNivaDriveSpeed);
            }
            if (_photoNivaVillageDistance >= _photoNivaVillageLength) FinishPhotoNivaDrive();
        }
        UpdateRideLook(false, delta);
    }

    private static Vector3 PhotoNivaApproachPoint(float localZ)
    {
        var x = PrologueApproachRoad.RoadX(localZ);
        return new Vector3(x + 1.35f, PrologueApproachRoad.Ground(x, localZ) + .06f, localZ);
    }

    private void AlignPhotoNivaApproachToVillage(PrologueApproachRoad road)
    {
        var localEnd = PhotoNivaApproachPoint(PhotoNivaApproachEndZ);
        var localForward = PhotoNivaApproachPoint(PhotoNivaApproachEndZ - 3f) - localEnd;
        var villageForward = _photoNivaVillagePath[1] - _photoNivaVillagePath[0];
        var localYaw = Mathf.Atan2(-localForward.X, -localForward.Z);
        var villageYaw = Mathf.Atan2(-villageForward.X, -villageForward.Z);
        var basis = new Basis(Vector3.Up, Mathf.Wrap(villageYaw - localYaw, -Mathf.Pi, Mathf.Pi));
        var villageStart = _photoNivaVillagePath[0];
        villageStart.Y = AgentBAct1HeightField.CollisionGround(villageStart.X, villageStart.Z) + .02f;
        road.GlobalTransform = new Transform3D(basis, villageStart - basis * localEnd);
    }

    private void PlacePhotoNivaOnApproach(PrologueApproachRoad road, float localZ, bool snap)
    {
        if (_photoNivaDriveVisual is not { } visual) return;
        var here = PhotoNivaApproachPoint(localZ);
        var ahead = PhotoNivaApproachPoint(localZ - 3f);
        var transformedHere = road.GlobalTransform * here;
        var forward = road.GlobalTransform.Basis * (ahead - here);
        var yaw = Mathf.Atan2(-forward.X, -forward.Z);
        var pitch = Mathf.Atan2(forward.Y, new Vector2(forward.X, forward.Z).Length());
        var smoothing = snap ? 1f : 1f - MathF.Exp(-6f * (float)GetProcessDeltaTime());
        visual.Root.Rotation = new Vector3(Mathf.LerpAngle(visual.Root.Rotation.X, pitch, smoothing),
            Mathf.LerpAngle(visual.Root.Rotation.Y, yaw, smoothing), 0);
        visual.Root.GlobalPosition = transformedHere;
    }

    private void FinishPhotoNivaDrive()
    {
        if (!_photoNivaDriving) return;
        if (_rideNiva is { } niva) niva.GlobalTransform = _photoNivaStopTransform;
        if (_rideCamera is { } camera) camera.Position = RideCameraRest;
        ReleasePhotoNivaDrive();
        _bridge!.OpenDialogueUi(_photoNivaLanguageActive ? PrologueRideDialogue : PhotoNivaArrivalDialogue);
        UpdateRouteCue();
    }

    private void ReleasePhotoNivaDrive()
    {
        if (!_photoNivaDriving) return;
        _photoNivaDriving = false;
        _photoNivaDriveVisual = null;
        ReleasePhotoNivaScore();
        if (_photoNivaPlayer is { } player && IsInstanceValid(player))
            player.Visible = _photoNivaPlayerWasVisible;
        if (_photoNivaDriveAudio is { } audio && IsInstanceValid(audio))
        {
            audio.ResetForSession();
            audio.QueueFree();
        }
        _photoNivaDriveAudio = null;
        ReleaseApproachRoad();
        ReleasePrologueOverlay();
        PrologueVoice.Stop();
        PrologueVoice.SetPaused(false);
    }
}
