using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private const float PhotoNivaDriveSeconds = 20f;
    private bool _photoNivaDriving;
    private float _photoNivaDriveElapsed;
    private Transform3D _photoNivaStopTransform;
    private VehicleVisualFactory.Visual? _photoNivaDriveVisual;
    private VehicleMechanicalAudio? _photoNivaDriveAudio;
    private bool PhotoNivaDrivePaused => _photoNivaPlayer?.ModalOpen != false || _pauseMenu?.IsOpen == true
        || !DisplayServer.WindowIsFocused()
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private void BeginPhotoNivaDrive(VehicleVisualFactory.Visual visual)
    {
        // This live opening is presentation only: the player's validated arrival
        // pose remains untouched, so Continue reconstructs the stationary cabin.
        _photoNivaDriving = true;
        _photoNivaDriveElapsed = 0f;
        _photoNivaDriveVisual = visual;
        _photoNivaStopTransform = visual.Root.GlobalTransform;
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
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, true, 8.5f, .35f, paused);
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
            if (_approachRoad is not null)
            {
                ReleaseApproachRoad();
                if (_rideNiva is { } niva) niva.GlobalTransform = _photoNivaStopTransform;
                _photoNivaDriveAudio?.ResetForSession();
                if (_rideCamera is { } camera) camera.Position = RideCameraRest;
            }
            if (_prologueBlackout is { } blackout)
                blackout.Color = Colors.Black with { A = 1f - Mathf.Clamp((t - PhotoNivaDriveSeconds - .3f) / .3f, 0f, 1f) };
            if (t >= PhotoNivaDriveSeconds + .6f) FinishPhotoNivaDrive();
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
