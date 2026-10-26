using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private const float PhotoNivaDriveSeconds = 20f;
    private bool _photoNivaDriving;
    private float _photoNivaDriveElapsed;
    private bool _photoNivaVillageStarted;
    private float _photoNivaVillageDistance, _photoNivaVillageLength, _photoNivaDriveSpeed;
    private Vector3[] _photoNivaVillagePath = Array.Empty<Vector3>();
    private bool _photoNivaPlayerWasVisible;
    private Transform3D _photoNivaStopTransform;
    private VehicleVisualFactory.Visual? _photoNivaDriveVisual;
    private VehicleMechanicalAudio? _photoNivaDriveAudio;
    private bool PhotoNivaDrivePaused => _photoNivaPlayer?.ModalOpen != false || _pauseMenu?.IsOpen == true
        || !DisplayServer.WindowIsFocused()
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private void BeginPhotoNivaDrive(VehicleVisualFactory.Visual visual)
    {
        // Reuse the real bridge/FAP/street route up to the house, then pull
        // over in its direction of travel instead of touring the square.
        var pullOverAt = Array.FindIndex(PrologueRidePath, point => point == new Vector3(1.3f, 0f, 10f));
        if (pullOverAt < 0) throw new InvalidOperationException("The Niva arrival street is missing from the ride route.");
        _photoNivaVillagePath = PrologueRidePath.Take(pullOverAt + 1)
            .Concat(new[] { new Vector3(-1.3f, 0f, 13f), new Vector3(-1.3f, 0f, 17f) }).ToArray();
        // This live opening is presentation only: the player's validated arrival
        // pose remains untouched, so Continue reconstructs the stationary cabin.
        _photoNivaDriving = true;
        _photoNivaDriveElapsed = 0f;
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
        _approachRoad = new PrologueApproachRoad();
        _main.AddChild(_approachRoad);
        PlaceOnApproach(_approachRoad, -130f, snap: true);
        _photoNivaDriveAudio = new VehicleMechanicalAudio { Name = "PhotoNivaDriveAudio" };
        visual.Root.AddChild(_photoNivaDriveAudio);
        _photoNivaDriveAudio.SetState(VehicleKind.Niva, true, 8.5f, .35f, false);
        BuildPrologueOverlay("пропустить поездку", "skip_prologue");
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Black;
        PrologueVoice.SetPaused(false);
        var waking = PrologueVoice.PlayClip(this, "forest-wake-v2");
        ShowPrologueCaption("Мансур бабай: «Айдар… Айдар, уян. Килеп җитәбез.»\n(Айдар… Айдар, проснись. Уже подъезжаем.)", Math.Max(5, waking + .3));
        HideRouteCue();
    }

    private void UpdatePhotoNivaDrive(float delta)
    {
        var paused = PhotoNivaDrivePaused;
        PrologueVoice.SetPaused(paused);
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, true, _photoNivaDriveSpeed, .35f, paused);
        if (paused) return;

        // A focus change or slow import must not jump the entire presentation.
        delta = Mathf.Clamp(delta, 0f, .1f);
        _photoNivaDriveElapsed += delta;
        var t = _photoNivaDriveElapsed;
        if (t < PhotoNivaDriveSeconds)
        {
            if (_approachRoad is { } road && _photoNivaDriveVisual is { } visual)
            {
                var before = visual.Root.GlobalPosition;
                PlaceOnApproach(road, -130f - t * 8.5f, snap: false);
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, 8.5f);
            }
            if (_prologueBlackout is { } blackout)
                blackout.Color = Colors.Black with { A = 1f - Mathf.Clamp(t / .7f, 0f, 1f) };
        }
        else if (t < PhotoNivaDriveSeconds + .3f)
        {
            if (_prologueBlackout is { } blackout)
                blackout.Color = Colors.Black with { A = (t - PhotoNivaDriveSeconds) / .3f };
        }
        else
        {
            if (!_photoNivaVillageStarted)
            {
                // The approach is a separate presentation location. Only this
                // brief cut crosses locations; the village road is driven in view.
                ReleaseApproachRoad();
                PlaceRideAt(0f, eased: false, route: _photoNivaVillagePath);
                _photoNivaVillageStarted = true;
                _photoNivaDriveSpeed = 4.2f;
            }
            if (_prologueBlackout is { } blackout)
                blackout.Color = Colors.Black with { A = 1f - Mathf.Clamp((t - PhotoNivaDriveSeconds - .3f) / .4f, 0f, 1f) };

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
        PrologueVoice.SetPaused(false);
    }
}
