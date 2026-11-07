using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // Prologue direction pass (author request 2026-11-06): one unbroken shot from
    // waking in the Niva to stepping out at babai's gate. The car starts at the
    // district bus stop and drives the whole winter road - fields, groves, the
    // village sign, the forest the road cuts - then hands over to the village
    // street without a blackout. Mansur's authored road talk, the radio and the
    // language conversation happen on the move; the score rises as the village
    // opens up and settles under the last lines at the gate.
    private const float PhotoNivaApproachStartZ = 18f;
    private const float PhotoNivaApproachEndZ = PrologueApproachRoad.RoadEndZ;
    private const float PhotoNivaApproachLength = PhotoNivaApproachStartZ - PhotoNivaApproachEndZ;
    private const float PhotoNivaCruise = 7.5f;
    private const float PhotoNivaForestCruise = 6f;
    private const float PhotoNivaTalkingCruise = 3f;
    private const float PhotoNivaVillageCruise = 4.2f;
    private const float PhotoNivaLanguageAt = 262f;
    private const double PhotoNivaBlinkAfterVoice = 1.4;
    private double _photoNivaWakeHold = PhotoNivaBlinkAfterVoice;
    private double _photoNivaVoiceDelay;
    private bool _photoNivaVoiceStarted;
    private bool _photoNivaDriving;
    private float _photoNivaDriveElapsed;
    private float _photoNivaApproachDistance;
    private bool _photoNivaVillageStarted;
    private float _photoNivaVillageDistance, _photoNivaVillageLength, _photoNivaDriveSpeed;
    private Vector3[] _photoNivaVillagePath = Array.Empty<Vector3>();
    private bool _photoNivaPlayerWasVisible;
    private Transform3D _photoNivaStopTransform;
    private VehicleVisualFactory.Visual? _photoNivaDriveVisual;
    private VehicleMechanicalAudio? _photoNivaDriveAudio;
    private int _photoNivaBarkIndex;
    private int _photoNivaVillageBarkIndex;
    private double _photoNivaNextBarkReady;
    private bool _photoNivaWoke;
    private bool _photoNivaLanguageOpened;
    private bool _photoNivaLanguageCaptioned;

    // Mansur's road talk (urman.chapter1 texts with Tatar density variants), by
    // metres driven from the bus stop. Each line waits for the previous one.
    private static readonly (float At, string Id)[] PhotoNivaRoadBarks =
    {
        (10f, "prologue-ride-bark-nightmare"), (58f, "prologue-ride-bark-wake"), (100f, "prologue-ride-bark-bus"),
        (138f, "prologue-ride-bark-radio"), (166f, "prologue-ride-bark-kazan"), (200f, "prologue-ride-bark-sign"),
        (ApproachForestAt - 40f, "prologue-ride-bark-field"), (ApproachForestAt + 10f, "prologue-ride-bark-forest"),
        (ApproachForestAt + 150f, "prologue-ride-bark-marat")
    };

    // Through the village: by place on the street rather than by distance.
    private static readonly (Vector3 At, float Reach, string Id)[] PhotoNivaVillageBarks =
    {
        (Vector3.Inf, 0f, "prologue-ride-bark-bridge"),
        (new Vector3(32f, 0f, -24.2f), 8f, "prologue-ride-bark-fap"),
        (new Vector3(0f, 0f, -10f), 7f, "prologue-ride-bark-street"),
        (new Vector3(-1.3f, 0f, 17f), 34f, "prologue-ride-bark-home")
    };

    private bool PhotoNivaDialogueOpen =>
        GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true };

    // A conversation in the car is part of the ride, not a pause of it.
    private bool PhotoNivaDrivePaused => MainMenuVisible || _pauseMenu?.IsOpen == true
        || (_photoNivaPlayer?.ModalOpen != false && !PhotoNivaDialogueOpen)
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
        _photoNivaBarkIndex = _photoNivaVillageBarkIndex = 0;
        _photoNivaNextBarkReady = _photoNivaWakeHold + 4.5;
        _photoNivaWoke = false;
        _photoNivaLanguageOpened = _photoNivaLanguageCaptioned = false;
        for (var index = 1; index < _photoNivaVillagePath.Length; index++)
            _photoNivaVillageLength += _photoNivaVillagePath[index - 1].DistanceTo(_photoNivaVillagePath[index]);
        _photoNivaDriveSpeed = PhotoNivaCruise;
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
        _photoNivaDriveAudio.SetState(VehicleKind.Niva, true, PhotoNivaCruise, .35f, false);
        // A warm dome light: the cabin read as a cold grey box against the white road.
        visual.Root.AddChild(new OmniLight3D
        {
            Name = "PhotoNivaCabinLight",
            Position = new(0f, 1.5f, .05f),
            LightColor = new Color(1f, .86f, .66f),
            LightEnergy = .45f,
            LightSpecular = 0f,
            OmniRange = 2.6f,
            ShadowEnabled = false
        });
        BuildPrologueOverlay("пропустить поездку", "skip_prologue");
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Black;
        // The ride's own black now covers the frame; the nightmare cut hands over without a flash.
        PrologueDreamCut.Release();
        // Slumped against the side window, eyes shut: the first thing is the voice.
        _rideLook = new(62f, -30f);
        ApplyPrologueRideLook();
        PrologueVoice.SetPaused(false);
        // The dream's voice blend may still be finishing: babai's «уян» follows it,
        // never on top of it, and the eyes open just after he speaks.
        _photoNivaVoiceDelay = PrologueDreamCut.MorphRemainingSeconds;
        _photoNivaVoiceStarted = false;
        _photoNivaWakeHold = _photoNivaVoiceDelay + PhotoNivaBlinkAfterVoice;
        _photoNivaNextBarkReady = _photoNivaWakeHold + 4.5;
        if (_photoNivaVoiceDelay <= .05) StartPhotoNivaWakeVoice();
        HideRouteCue();
    }

    private void UpdatePhotoNivaDrive(float delta)
    {
        var paused = PhotoNivaDrivePaused;
        var talking = PhotoNivaDialogueOpen;
        PrologueVoice.SetPaused(paused);
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, true, _photoNivaDriveSpeed, .35f, paused);
        var metresToStop = (_photoNivaVillageStarted ? 0f : PhotoNivaApproachLength - _photoNivaApproachDistance)
            + (_photoNivaVillageLength - _photoNivaVillageDistance);
        UpdatePhotoNivaScore(delta, paused, metresToStop);
        if (_prologueOverlay is { } overlay)
            overlay.Visible = !MainMenuVisible && !talking && _photoNivaPlayer?.ModalOpen == false && _pauseMenu?.IsOpen != true;
        if (paused) return;

        // A focus change or slow import must not jump the entire presentation.
        delta = Mathf.Clamp(delta, 0f, .1f);
        _photoNivaDriveElapsed += delta;
        var t = _photoNivaDriveElapsed;
        UpdatePhotoNivaWake(t);
        UpdatePhotoNivaLanguage(talking);

        if (!_photoNivaVillageStarted)
        {
            if (_approachRoad is { } road && _photoNivaDriveVisual is { } visual)
            {
                var remaining = PhotoNivaApproachLength - _photoNivaApproachDistance;
                var inForest = _photoNivaApproachDistance > ApproachForestAt - 20f;
                var cruise = talking ? PhotoNivaTalkingCruise : inForest ? PhotoNivaForestCruise : PhotoNivaCruise;
                // Arrive at the village street at its own speed, never with a lurch.
                var targetSpeed = Mathf.Min(cruise,
                    Mathf.Sqrt(PhotoNivaVillageCruise * PhotoNivaVillageCruise + 2f * 1.4f * Mathf.Max(0f, remaining)));
                _photoNivaDriveSpeed = Mathf.MoveToward(_photoNivaDriveSpeed, targetSpeed, 1.6f * delta);
                _photoNivaApproachDistance = Mathf.Min(PhotoNivaApproachLength,
                    _photoNivaApproachDistance + _photoNivaDriveSpeed * delta);
                var before = visual.Root.GlobalPosition;
                PlacePhotoNivaOnApproach(road, PhotoNivaApproachStartZ - _photoNivaApproachDistance, snap: false);
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, _photoNivaDriveSpeed);
                // Someone stands in the snow where the forest begins - until the car is close.
                if (road.EdgeFigure is { Visible: true } figure
                    && visual.Root.GlobalPosition.DistanceTo(figure.GlobalPosition) < 34f)
                    figure.Visible = false;
                if (!talking) UpdatePhotoNivaRoadBarks();
            }
            if (_photoNivaApproachDistance >= PhotoNivaApproachLength)
            {
                _photoNivaVillageStarted = true;
                _photoNivaDriveSpeed = Mathf.Min(_photoNivaDriveSpeed, PhotoNivaVillageCruise);
                // The road endpoint and this point share the same world pose;
                // hand the route owner across without a blackout or relocation.
                PlaceRideAt(0f, eased: false, route: _photoNivaVillagePath);
            }
        }
        else
        {
            var remaining = _photoNivaVillageLength - _photoNivaVillageDistance;
            // Brake along the final straight into the existing parked pose.
            var targetSpeed = Mathf.Min(PhotoNivaVillageCruise, Mathf.Sqrt(2f * 1.4f * Mathf.Max(0f, remaining)));
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
                if (!talking) UpdatePhotoNivaVillageBarks(visual.Root.GlobalPosition, remaining);
            }
            // Let the last line at the gate finish before the conversation opens.
            if (_photoNivaVillageDistance >= _photoNivaVillageLength && t >= _photoNivaNextBarkReady - 1.0)
                FinishPhotoNivaDrive();
        }
        UpdateRideLook(talking, delta);
    }

    // Black, the voice, then the eyes open twice - a blink, not a fade - and
    // Aidar comes up from the window with a start.
    private void StartPhotoNivaWakeVoice()
    {
        _photoNivaVoiceStarted = true;
        var waking = PrologueVoice.PlayClip(this, "forest-wake-v2");
        ShowPrologueCaption("Мансур бабай: «Айдар… Айдар, уян. Килеп җитәбез.»\n(Айдар… Айдар, проснись. Уже подъезжаем.)",
            Math.Max(4.5, waking + .3));
    }

    private void UpdatePhotoNivaWake(float t)
    {
        if (!_photoNivaVoiceStarted && t >= _photoNivaVoiceDelay) StartPhotoNivaWakeVoice();
        if (_prologueBlackout is { } blackout && t < 3f)
        {
            float alpha;
            if (_player?.ReducedMotion == true) alpha = 1f - Mathf.Clamp((t - (float)_photoNivaWakeHold) / .5f, 0f, 1f);
            else if (t < _photoNivaWakeHold) alpha = 1f;
            else if (t < _photoNivaWakeHold + .22) alpha = Mathf.Lerp(1f, .32f, (t - (float)_photoNivaWakeHold) / .22f);
            else if (t < _photoNivaWakeHold + .4) alpha = Mathf.Lerp(.32f, .86f, (t - (float)_photoNivaWakeHold - .22f) / .18f);
            else alpha = Mathf.Lerp(.86f, 0f, Mathf.Clamp((t - (float)_photoNivaWakeHold - .4f) / .75f, 0f, 1f));
            blackout.Color = Colors.Black with { A = alpha };
        }
        if (_photoNivaWoke || t < _photoNivaWakeHold) return;
        _photoNivaWoke = true;
        if (_rideCamera is { } camera)
            UiFoley.PlayWorld(this, camera.GlobalPosition, "forest/gasp", -8f, 6f, 2f);
        if (_player?.ReducedMotion == true)
        {
            _rideLook = new(35f, -2f);
            ApplyPrologueRideLook();
            return;
        }
        PhotoNivaWakeLook();
    }

    private async void PhotoNivaWakeLook()
    {
        var from = _rideLook;
        var to = new Vector2(35f, -2f);
        var elapsed = 0f;
        while (elapsed < .6f && _photoNivaDriving && _rideCamera is { } camera && IsInstanceValid(camera))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (PhotoNivaDrivePaused) continue;
            elapsed += (float)GetProcessDeltaTime();
            var k = Mathf.Clamp(elapsed / .38f, 0f, 1f);
            _rideLook = from.Lerp(to, 1f - (1f - k) * (1f - k) * (1f - k));
            ApplyPrologueRideLook();
            var shake = (1f - Mathf.Clamp(elapsed / .6f, 0f, 1f)) * .5f;
            camera.RotationDegrees += new Vector3(Mathf.Sin(elapsed * 71f) * shake, Mathf.Sin(elapsed * 53f) * shake, 0);
        }
    }

    private void UpdatePhotoNivaRoadBarks()
    {
        if (_photoNivaBarkIndex >= PhotoNivaRoadBarks.Length || _photoNivaDriveElapsed < _photoNivaNextBarkReady) return;
        var (at, id) = PhotoNivaRoadBarks[_photoNivaBarkIndex];
        if (_photoNivaApproachDistance < at) return;
        _photoNivaBarkIndex++;
        _photoNivaNextBarkReady = _photoNivaDriveElapsed + RideBark(_bridge, id) + 1.1;
    }

    private void UpdatePhotoNivaVillageBarks(Vector3 carAt, float metresToStop)
    {
        if (_photoNivaVillageBarkIndex >= PhotoNivaVillageBarks.Length || _photoNivaDriveElapsed < _photoNivaNextBarkReady) return;
        var (at, reach, id) = PhotoNivaVillageBarks[_photoNivaVillageBarkIndex];
        var isHome = _photoNivaVillageBarkIndex == PhotoNivaVillageBarks.Length - 1;
        var arrived = isHome
            ? metresToStop <= reach
            : at == Vector3.Inf ? _photoNivaVillageDistance >= 3f
            : new Vector2(carAt.X - at.X, carAt.Z - at.Z).Length() <= reach;
        // A place already behind the car is no longer worth a remark.
        var passed = !isHome && at != Vector3.Inf && metresToStop <= PhotoNivaVillageBarks[^1].Reach;
        if (!arrived && !passed) return;
        _photoNivaVillageBarkIndex++;
        if (passed && !arrived) return;
        _photoNivaNextBarkReady = _photoNivaDriveElapsed + RideBark(_bridge, id) + 1.1;
    }

    // The language talk happens on the road, between the sign and the fields,
    // as in the authored beat sheet; the car slows while they talk.
    private void UpdatePhotoNivaLanguage(bool talking)
    {
        if (!_photoNivaLanguageActive || _bridge is not { } bridge) return;
        if (!_photoNivaLanguageOpened)
        {
            if (_photoNivaVillageStarted || _photoNivaApproachDistance < PhotoNivaLanguageAt
                || _photoNivaDriveElapsed < _photoNivaNextBarkReady || talking) return;
            _photoNivaLanguageOpened = true;
            bridge.OpenDialogueUi(PrologueRideDialogue);
            return;
        }
        if (talking || !_photoNivaLanguageWrite.IsCompleted) return;
        // ChoiceAccepted has recorded the answer (Esc only ends the offer). The
        // stop then opens the arrival conversation directly.
        _photoNivaLanguageActive = false;
        if (_photoNivaLanguageCaptioned || _player is not { } player) return;
        _photoNivaLanguageCaptioned = true;
        ShowPrologueCaption($"Татарский в разговорах: {LanguageLevelLabel(player.TatarLanguageLevel)}. Это можно поменять в настройках.", 5);
        _photoNivaNextBarkReady = Math.Max(_photoNivaNextBarkReady, _photoNivaDriveElapsed + 5.5);
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
        PrologueDreamCut.Release();
        PrologueVoice.Stop();
        PrologueVoice.SetPaused(false);
    }
}
