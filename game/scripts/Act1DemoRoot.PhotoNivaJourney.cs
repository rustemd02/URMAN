using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private bool _photoNivaKeepRiding;
    private bool _photoNivaArrivalRequested;
    private float _photoNivaJourneyCut = -1f;
    private float _photoNivaJourneyCutDuration;
    private bool _photoNivaJourneyCutPlaced;
    private bool _photoNivaJourneyShowsTitle;

    private void ResetPhotoNivaJourney()
    {
        _photoNivaKeepRiding = false;
        _photoNivaArrivalRequested = false;
        _photoNivaBookRechecked = _photoNivaBookClosedForHome = false;
        _photoNivaJourneyCut = -1f;
        _photoNivaJourneyCutPlaced = false;
    }

    private void BeginPhotoNivaJourneyCut()
    {
        if (!_photoNivaDriving || _photoNivaJourneyCut >= 0f || _photoNivaGateClock >= 0f
            || _photoNivaLanguageActive || !_photoNivaLanguageWrite.IsCompleted || PhotoNivaBookIsClosing) return;
        _photoNivaArrivalRequested = true;
        _photoNivaKeepRiding = false;
        if (!TryPreparePhotoNivaArrival()) return;
        _photoNivaEvidenceClock = -1f;
        _photoNivaJourneyCut = 0f;
        _photoNivaJourneyCutPlaced = false;
        _photoNivaJourneyShowsTitle = _photoNivaTitleClock < 0f;
        _photoNivaJourneyCutDuration = _photoNivaJourneyShowsTitle ? PhotoNivaTitleSeconds + .8f : 1.3f;
        if (_photoNivaEvidenceCue is { } cue) cue.Visible = false;
        ShowPrologueCaption(string.Empty, .01);
        StopRideRadio(.35);
        PrologueVoice.Stop();
    }

    private bool UpdatePhotoNivaJourney(float delta)
    {
        if (_photoNivaJourneyCut < 0f && !_photoNivaKeepRiding && _photoNivaWoke
            && _photoNivaGateClock < 0f && !PhotoNivaDialogueOpen && !_photoNivaLanguageActive
            && _photoNivaEvidenceClock < 0f && _photoNivaDriveElapsed >= _photoNivaNextBarkReady
            && (_photoNivaArrivalRequested
                || !PhotoNivaBookInUse && _photoNivaDriveElapsed >= _photoNivaWokeAt + 16f))
            BeginPhotoNivaJourneyCut();
        if (_photoNivaJourneyCut < 0f) return false;
        _photoNivaJourneyCut += delta;
        var t = _photoNivaJourneyCut;
        if (!_photoNivaJourneyCutPlaced && t >= .4f)
        {
            _photoNivaJourneyCutPlaced = true;
            // Relocate only under opaque black, to the real parked Niva's validated pose.
            if (_rideNiva is { } niva) niva.GlobalTransform = _photoNivaStopTransform;
            if (_rideCamera is { } camera) camera.Position = RideCameraRest;
            _photoNivaVillageStarted = true;
            _photoNivaApproachDistance = PhotoNivaApproachLength;
            _photoNivaVillageDistance = _photoNivaVillageLength;
            _photoNivaDriveSpeed = 0f;
            _rideLook = PhotoNivaRoadGaze;
            ApplyPrologueRideLook();
            if (_photoNivaJourneyShowsTitle) _photoNivaTitleClock = 0f;
        }
        var fadeOutAt = _photoNivaJourneyCutDuration - .45f;
        var alpha = t < .4f ? Mathf.Clamp(t / .4f, 0f, 1f)
            : 1f - Mathf.Clamp((t - fadeOutAt) / .45f, 0f, 1f);
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Black with { A = alpha };
        _photoNivaBars = 0f;
        LayoutPhotoNivaFrame();
        if (t < _photoNivaJourneyCutDuration) return true;
        _photoNivaJourneyCut = -1f;
        if (PhotoNivaGatePending && _photoNivaDriveVisual is { } visual) BeginPhotoNivaParkedGate(visual);
        else FinishPhotoNivaDrive();
        return true;
    }

    private void PresentParkedPhotoNiva(bool speak = true)
    {
        ReleasePrologueOverlay();
        BuildPrologueOverlay("выйти из Нивы", "interact");
        BuildPhotoNivaLapBook();
        if (speak)
            ShowPrologueCaption(_photoNivaPhotoShown
                ? "Мансур бабай: «Приехали. Про снимок у Гөлсинә сам спроси. Она дома»."
                : "Мансур бабай: «Приехали. Гөлсинә дома. Книгу ей занеси сам».", 4.0);
        UpdateParkedPhotoNivaCue(0f);
        UpdateRouteCue();
    }

    private void UpdateParkedPhotoNivaCue(float delta)
    {
        RefreshPhotoNivaLapBook(delta);
        var visible = !MainMenuVisible && !_menuBusy && _pauseMenu?.IsOpen != true
            && !PhotoNivaDialogueOpen && _photoNivaPlayer?.UiModalOpen == false
            && GetTree().GetFirstNodeInGroup("settings_ui") is not SettingsUi { IsOpen: true };
        if (_prologueOverlay is { } overlay) overlay.Visible = visible;
        if (_photoNivaEvidenceCue is { } cue && IsInstanceValid(cue))
        {
            cue.Visible = visible && !_photoNivaExitPending;
            cue.Text = InputBindingService.ActionHint("interact", _photoNivaPlayer?.CurrentInputDevice == "gamepad") + " — Выйти из Нивы";
        }
    }
}
