using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    // The ride home, rebuilt from nothing (author request 2026-11-12).
    //
    // One unbroken first-person shot, about a minute and a half, in a frame with
    // film bars. He wakes with the photograph of that forest open on his lap;
    // babai has seen the dream on his face. The last field, the radio, then the
    // forest closes over the road: the music breaks up into a voice that says
    // his name, someone far too tall stands among the trunks, and babai, who
    // has not looked, says only «на лес не смотри». The trees let go, the
    // village opens and the title comes up over it. Nothing is explained.
    //
    // The contract with the arrival owner is unchanged: the car ends in the
    // parked pose it was given, the language talk and the arrival conversation
    // open at the gate, and none of this writes story state.

    // ---- Tuning ---------------------------------------------------------------------------
    private const float PhotoNivaFieldBeforeForest = 190f;                 // metres of open road before the trees
    private const float PhotoNivaApproachStartZ = PrologueApproachRoad.ForestStart + PhotoNivaFieldBeforeForest;
    private const float PhotoNivaApproachEndZ = PrologueApproachRoad.RoadEndZ;
    private const float PhotoNivaApproachLength = PhotoNivaApproachStartZ - PhotoNivaApproachEndZ;
    private const float PhotoNivaFieldCruise = 7.2f;
    private const float PhotoNivaForestCruise = 6.8f;
    private const float PhotoNivaVillageCruise = 4.6f;
    private const double PhotoNivaBlinkAfterVoice = 1.4;
    private const float PhotoNivaGazeDelay = 4.2f;                        // seconds on the photograph after waking
    private const float PhotoNivaGazeSeconds = 2.6f;                      // then the head comes up to the road
    private static readonly Vector2 PhotoNivaLapGaze = new(0f, -37f);
    private static readonly Vector2 PhotoNivaRoadGaze = new(9f, -4f);
    private const float PhotoNivaBarShare = .095f;                        // film bar height, share of the screen
    private const float PhotoNivaTitleBeforeEnd = 46f;                    // metres before the village street
    private const float PhotoNivaTitleSeconds = 6.5f;

    private double _photoNivaWakeHold = PhotoNivaBlinkAfterVoice;
    private double _photoNivaVoiceDelay;
    private bool _photoNivaVoiceStarted;
    private double _photoNivaWakeVoiceEnds;
    private bool _photoNivaPrintRecognized;
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
    private double _photoNivaNextBarkReady;
    private bool _photoNivaWoke;
    private float _photoNivaWokeAt;
    private readonly bool[] _photoNivaBeat = new bool[12];
    private float _photoNivaGaze;                 // 0 on the lap .. 1 on the road
    private bool _photoNivaGazeTaken;             // the player took the look over
    private float _photoNivaBars;                 // 0 open frame .. 1 film bars in
    private float _photoNivaTitleClock = -1f;
    private ColorRect? _photoNivaBarTop, _photoNivaBarBottom, _photoNivaGrade;
    private Control? _photoNivaTitle;
    private OmniLight3D? _photoNivaCabinLight;
    private float _photoNivaLanguageOpenedAt = -1f;   // the talk about Tatar happens on the road, early
    private float _photoNivaGateClock = -1f;          // the last scene at the gate; -1 until it starts
    private bool _photoNivaGateDone, _photoNivaGateCut;
    private float _photoNivaGateBlack;
    private Node3D? _photoNivaGateMitten;
    private readonly bool[] _photoNivaGateBeat = new bool[10];
    private const float PhotoNivaTalkCruise = 3.3f;   // babai eases off while they talk
    private const float PhotoNivaGateCutAt = 27.2f;   // seconds into the gate scene: black
    private static readonly bool PhotoNivaGateTest = System.Environment.GetEnvironmentVariable("URMAN_RIDE_GATE_TEST") == "1";

    /// <summary>What he brought out of the "dream" is still in his pocket when the engine stops.</summary>
    private bool PhotoNivaGatePending => (W01Controller.PrologueMittenTaken || PhotoNivaGateTest) && !_photoNivaGateDone;

    private bool PhotoNivaDialogueOpen =>
        GetTree().GetFirstNodeInGroup("dialogue_ui") is DialogueUi { IsOpen: true };

    // Development captures run unattended in the background.
    private static readonly bool PhotoNivaUnattended = W01Controller.DevForestCaptureActive
        || !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("URMAN_SHOT_DIR"));

    // A conversation in the car is part of the ride, not a pause of it.
    private bool PhotoNivaDrivePaused => MainMenuVisible || _pauseMenu?.IsOpen == true
        || (_photoNivaPlayer?.ModalOpen != false && !PhotoNivaDialogueOpen)
        || (!DisplayServer.WindowIsFocused() && !PhotoNivaUnattended)
        || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true };

    private bool PhotoNivaOnce(int beat, bool condition)
    {
        if (_photoNivaBeat[beat] || !condition) return false;
        _photoNivaBeat[beat] = true;
        return true;
    }

    // ---- Start ----------------------------------------------------------------------------

    private void BeginPhotoNivaDrive(VehicleVisualFactory.Visual visual)
    {
        // The street the car arrives by: the northern road in, the bridge, and the pull-over at the gate.
        var pullOverAt = Array.FindIndex(PrologueRidePath, point => point == new Vector3(1.3f, 0f, 10f));
        if (pullOverAt < 0) throw new InvalidOperationException("The Niva arrival street is missing from the ride route.");
        var northRoad = Act1ConnectedWorld.FarBankPlot().Roads.Single(road => road.Id == "yar-north");
        var northernApproach = northRoad.Points.Skip(3).Reverse()
            .Select(point => new Vector3(point.X, 0f, point.Y));
        _photoNivaVillagePath = northernApproach.Concat(PrologueRidePath.Take(pullOverAt + 1))
            .Concat(new[] { new Vector3(-1.3f, 0f, 13f), new Vector3(-1.3f, 0f, 17f) }).ToArray();
        _photoNivaDriving = true;
        _photoNivaDriveElapsed = 0f;
        _photoNivaApproachDistance = 0f;
        _photoNivaVillageStarted = false;
        _photoNivaVillageDistance = _photoNivaVillageLength = 0f;
        Array.Clear(_photoNivaBeat);
        _photoNivaWoke = false;
        _photoNivaWokeAt = 0f;
        _photoNivaPrintRecognized = false;
        _photoNivaGaze = 0f;
        _photoNivaGazeTaken = false;
        _photoNivaBars = 1f;
        _photoNivaTitleClock = -1f;
        _photoNivaLanguageOpenedAt = -1f;
        _photoNivaGateClock = -1f;
        _photoNivaGateDone = _photoNivaGateCut = false;
        _photoNivaGateBlack = 0f;
        for (var index = 1; index < _photoNivaVillagePath.Length; index++)
            _photoNivaVillageLength += _photoNivaVillagePath[index - 1].DistanceTo(_photoNivaVillagePath[index]);
        _photoNivaDriveSpeed = PhotoNivaFieldCruise;
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
        _photoNivaDriveAudio.SetState(VehicleKind.Niva, true, PhotoNivaFieldCruise, .35f, false);
        // The heater glow of an old cabin; it sinks when the forest takes the daylight.
        _photoNivaCabinLight = new OmniLight3D
        {
            Name = "PhotoNivaCabinLight",
            Position = new(0f, 1.5f, .05f),
            LightColor = new Color(1f, .84f, .62f),
            LightEnergy = .5f,
            LightSpecular = 0f,
            OmniRange = 2.6f,
            ShadowEnabled = false
        };
        visual.Root.AddChild(_photoNivaCabinLight);
        BuildPrologueOverlay("пропустить поездку", "skip_prologue");
        if (_prologueBlackout is { } blackout) blackout.Color = Colors.Black;
        BuildPhotoNivaFrame();
        // The ride's own black now covers the frame; the nightmare cut hands over without a flash.
        PrologueDreamCut.Release();
        // The same print is physically open on his lap when his eyes open.
        BuildPhotoNivaLapBook();
        _rideLook = PhotoNivaLapGaze;
        ApplyPrologueRideLook();
        PrologueVoice.SetPaused(false);
        // The dream's voice blend may still be finishing: babai's «уян» follows it,
        // never on top of it, and the eyes open just after he speaks.
        _photoNivaVoiceDelay = PrologueDreamCut.MorphRemainingSeconds;
        _photoNivaVoiceStarted = false;
        _photoNivaWakeHold = _photoNivaVoiceDelay + PhotoNivaBlinkAfterVoice;
        _photoNivaNextBarkReady = double.PositiveInfinity;
        if (_photoNivaVoiceDelay <= .05) StartPhotoNivaWakeVoice();
        HideRouteCue();
        GD.Print($"act1-photo-niva-ride: approach={PhotoNivaApproachLength:0}m village={_photoNivaVillageLength:0}m");
    }

    /// <summary>Film bars and the title card, on the ride's own overlay.</summary>
    private void BuildPhotoNivaFrame()
    {
        if (_prologueOverlay is not { } overlay) return;
        ColorRect Bar(string name, bool top) => new()
        {
            Name = name, Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = top ? 0 : 1, AnchorBottom = top ? 0 : 1
        };
        _photoNivaBarTop = Bar("FilmBarTop", true);
        _photoNivaBarBottom = Bar("FilmBarBottom", false);
        // A film grade over the whole view: colour drawn back, shadows cooled, the
        // corners let go. It lifts with the bars at the gate.
        _photoNivaGrade = new ColorRect
        {
            Name = "FilmGrade", MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = PhotoNivaGradeShader } }
        };
        _photoNivaGrade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(_photoNivaGrade);
        overlay.AddChild(_photoNivaBarTop);
        overlay.AddChild(_photoNivaBarBottom);
        // Behind the waking black and under the subtitles, which sit in the lower bar like a film's.
        overlay.MoveChild(_photoNivaGrade, 0);
        overlay.MoveChild(_photoNivaBarTop, 2);
        overlay.MoveChild(_photoNivaBarBottom, 3);

        var card = new Control { Name = "TitleCard", MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        card.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var scale = _player?.Accessibility.TextScale ?? 1f;
        var title = new Label
        {
            Name = "Title", Text = "УРМАН", HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = .19f, AnchorBottom = .19f, OffsetBottom = 150
        };
        title.AddThemeFontOverride("font", UrmanUiTheme.TitleFont);
        title.AddThemeFontSizeOverride("font_size", (int)(118 * scale));
        title.AddThemeColorOverride("font_color", new Color(.96f, .93f, .86f));
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .55f));
        title.AddThemeConstantOverride("shadow_offset_y", 3);
        title.AddThemeConstantOverride("shadow_outline_size", 10);
        // Legible over snow as well as over the dark of the cabin.
        title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .42f));
        title.AddThemeConstantOverride("outline_size", 7);
        var rule = new ColorRect
        {
            Name = "Rule", Color = new Color(.96f, .93f, .86f, .7f), MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = .5f, AnchorRight = .5f, AnchorTop = .19f, AnchorBottom = .19f,
            OffsetLeft = -70, OffsetRight = 70, OffsetTop = 158, OffsetBottom = 159.5f
        };
        var place = new Label
        {
            Name = "Place", Text = "Кара-Урман. Зима.", HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = .19f, AnchorBottom = .19f, OffsetTop = 172, OffsetBottom = 212
        };
        place.AddThemeFontOverride("font", UrmanUiTheme.HandFont);
        place.AddThemeFontSizeOverride("font_size", (int)(30 * scale));
        place.AddThemeColorOverride("font_color", new Color(.93f, .89f, .8f));
        place.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .6f));
        place.AddThemeConstantOverride("shadow_outline_size", 6);
        card.AddChild(title);
        card.AddChild(rule);
        card.AddChild(place);
        overlay.AddChild(card);
        _photoNivaTitle = card;
        LayoutPhotoNivaFrame();
    }

    private const string PhotoNivaGradeShader = """
        shader_type canvas_item;
        uniform sampler2D screen : hint_screen_texture, filter_linear;
        uniform float amount = 1.0;
        void fragment() {
            vec3 c = texture(screen, SCREEN_UV).rgb;
            float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));
            vec3 graded = mix(vec3(luma), c, 0.72);                      // colour drawn back
            graded = mix(graded * vec3(0.9, 0.97, 1.08), graded, luma);  // cool shadows, clean highlights
            graded = pow(graded, vec3(1.07));                            // a little weight
            vec2 p = UV * 2.0 - 1.0;
            float edge = smoothstep(0.45, 1.75, dot(p, p));
            graded *= 1.0 - edge * 0.55;
            COLOR = vec4(mix(c, graded, amount), 1.0);
        }
        """;

    private void LayoutPhotoNivaFrame()
    {
        if (_photoNivaGrade is { Material: ShaderMaterial grade } rect && IsInstanceValid(rect))
            grade.SetShaderParameter("amount", _photoNivaBars);
        var height = GetViewport().GetVisibleRect().Size.Y * PhotoNivaBarShare * _photoNivaBars;
        if (_photoNivaBarTop is { } top && IsInstanceValid(top)) { top.OffsetTop = 0; top.OffsetBottom = height; }
        if (_photoNivaBarBottom is { } bottom && IsInstanceValid(bottom)) { bottom.OffsetTop = -height; bottom.OffsetBottom = 0; }
        if (_photoNivaTitle is { } card && IsInstanceValid(card))
        {
            var t = _photoNivaTitleClock;
            var alpha = t < 0f ? 0f : Mathf.SmoothStep(0f, 1.6f, t) * (1f - Mathf.SmoothStep(PhotoNivaTitleSeconds - 1.8f, PhotoNivaTitleSeconds, t));
            card.Modulate = new Color(1, 1, 1, alpha);
        }
    }

    // ---- Every frame ----------------------------------------------------------------------

    private void UpdatePhotoNivaDrive(float delta)
    {
        var paused = PhotoNivaDrivePaused;
        var talking = PhotoNivaDialogueOpen;
        SetRideRadioPaused(paused);
        PrologueVoice.SetPaused(paused);
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, true, _photoNivaDriveSpeed, .35f, paused);
        var metresToStop = (_photoNivaVillageStarted ? 0f : PhotoNivaApproachLength - _photoNivaApproachDistance)
            + (_photoNivaVillageLength - _photoNivaVillageDistance);
        UpdatePhotoNivaScore(delta, paused, metresToStop);
        RefreshPhotoNivaLapBook();
        if (_prologueOverlay is { } overlay)
            overlay.Visible = !MainMenuVisible && !talking && _photoNivaPlayer?.ModalOpen == false && _pauseMenu?.IsOpen != true;
        if (paused) return;

        // A focus change or slow import must not jump the entire presentation.
        delta = Mathf.Clamp(delta, 0f, .1f);
        _photoNivaDriveElapsed += delta;
        var t = _photoNivaDriveElapsed;
        UpdatePhotoNivaWake(t);
        UpdatePhotoNivaGaze(delta);
        if (_photoNivaTitleClock >= 0f) _photoNivaTitleClock += delta;
        // The bars open for a conversation and leave for good at the gate — after its last scene, if there is one.
        var openFrame = talking || (metresToStop < 7f && (!PhotoNivaGatePending || _photoNivaGateCut));
        _photoNivaBars = Mathf.MoveToward(_photoNivaBars, openFrame ? 0f : 1f, delta * (talking ? 1.4f : .6f));
        LayoutPhotoNivaFrame();
        // Development: URMAN_RIDE_GATE_TEST=1 plays the scene at the gate half a minute after waking.
        if (PhotoNivaGateTest && _photoNivaWoke && _photoNivaGateClock < 0f && !_photoNivaGateDone && t > _photoNivaWokeAt + 34f)
            BeginPhotoNivaGate();
        if (_photoNivaGateClock >= 0f && UpdatePhotoNivaGate(delta)) return;

        if (!_photoNivaVillageStarted)
        {
            if (_approachRoad is { } road && _photoNivaDriveVisual is { } visual)
            {
                var d = _photoNivaApproachDistance;
                var remaining = PhotoNivaApproachLength - d;
                var inForest = d > PhotoNivaFieldBeforeForest - 12f;
                var cruise = talking ? PhotoNivaTalkCruise : inForest ? PhotoNivaForestCruise : PhotoNivaFieldCruise;
                // Arrive at the village street at its own speed, never with a lurch.
                var targetSpeed = Mathf.Min(cruise,
                    Mathf.Sqrt(PhotoNivaVillageCruise * PhotoNivaVillageCruise + 2f * 1.2f * Mathf.Max(0f, remaining)));
                _photoNivaDriveSpeed = Mathf.MoveToward(_photoNivaDriveSpeed, targetSpeed, 1.1f * delta);
                _photoNivaApproachDistance = Mathf.Min(PhotoNivaApproachLength, d + _photoNivaDriveSpeed * delta);
                var before = visual.Root.GlobalPosition;
                PlacePhotoNivaOnApproach(road, PhotoNivaApproachStartZ - _photoNivaApproachDistance, snap: false);
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, _photoNivaDriveSpeed);
                // Under the trees the cabin goes dim and warm.
                if (_photoNivaCabinLight is { } lamp && IsInstanceValid(lamp))
                    lamp.LightEnergy = Mathf.MoveToward(lamp.LightEnergy, inForest && remaining > 30f ? .26f : .5f, delta * .12f);
                UpdatePhotoNivaRoadBeats(road, visual, d, remaining, t);
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
            var targetSpeed = Mathf.Min(PhotoNivaVillageCruise, Mathf.Sqrt(2f * 1.1f * Mathf.Max(0f, remaining)));
            _photoNivaDriveSpeed = Mathf.MoveToward(_photoNivaDriveSpeed, targetSpeed, 1.1f * delta);
            _photoNivaVillageDistance = Mathf.Min(_photoNivaVillageLength,
                _photoNivaVillageDistance + Mathf.Max(.2f, _photoNivaDriveSpeed) * delta);
            if (_photoNivaDriveVisual is { } visual)
            {
                var before = visual.Root.GlobalPosition;
                PlaceRideAt(_photoNivaVillageDistance / _photoNivaVillageLength, eased: false, route: _photoNivaVillagePath);
                // The parked controller grounds its whole hull rather than the
                // road centre. Ease only that height difference into the last 4m.
                var settle = Mathf.Clamp(1f - remaining / 4f, 0f, 1f);
                visual.Root.GlobalPosition = visual.Root.GlobalPosition with
                {
                    Y = Mathf.Lerp(visual.Root.GlobalPosition.Y, _photoNivaStopTransform.Origin.Y, settle)
                };
                SpinRideWheels(visual, before);
                if (_player?.ReducedMotion != true) TickRideRoad(delta, _photoNivaDriveSpeed);
                // Two remarks only: where the village's heart is, and home.
                if (t >= _photoNivaNextBarkReady && _photoNivaTitleClock is < 0f or > PhotoNivaTitleSeconds)
                {
                    if (PhotoNivaOnce(8, remaining < _photoNivaVillageLength * .62f && remaining > 60f))
                        _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-street") + 1.2;
                    else if (PhotoNivaOnce(9, remaining <= 30f))
                        _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-home") + 1.2;
                }
            }
            // Let the last line at the gate finish before the conversation opens.
            if (_photoNivaVillageDistance >= _photoNivaVillageLength && t >= _photoNivaNextBarkReady - 1.0)
            {
                if (!PhotoNivaGatePending) FinishPhotoNivaDrive();
                else if (_photoNivaGateClock < 0f) BeginPhotoNivaGate();
            }
        }
        UpdateRideLook(talking || _photoNivaGateClock >= 0f, delta);
    }

    /// <summary>The road, in the order it happens. Each beat fires once; babai never talks over himself.</summary>
    private void UpdatePhotoNivaRoadBeats(PrologueApproachRoad road, VehicleVisualFactory.Visual visual, float d, float remaining, float t)
    {
        var talking = PhotoNivaDialogueOpen;
        var ready = _photoNivaWoke && !talking && t >= _photoNivaNextBarkReady;
        // He saw the dream on the boy's face.
        if (ready && PhotoNivaOnce(0, t >= _photoNivaWokeAt + 2.2f))
        {
            _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-nightmare") + .9;
            return;
        }
        // Then, still out in the fields, the old tease: does the city boy remember the language?
        // Everything he says for the rest of the road follows the answer.
        if (_photoNivaLanguageActive && _photoNivaBeat[0])
        {
            if (_photoNivaLanguageOpenedAt < 0f)
            {
                if (!ready) return;
                _photoNivaLanguageOpenedAt = t;
                _bridge!.OpenDialogueUi(PrologueRideDialogue);
                return;
            }
            // Development captures have nobody to answer: the question is shown, then let go.
            if (talking && PhotoNivaUnattended && t > _photoNivaLanguageOpenedAt + 7f)
                (GetTree().GetFirstNodeInGroup("dialogue_ui") as DialogueUi)?.CloseIfDialogue(PrologueRideDialogue);
            if (talking || t < _photoNivaLanguageOpenedAt + .6f || !_photoNivaLanguageWrite.IsCompleted) return;
            // Esc only ends the offer; an answer has already been written by its own checkpoint.
            _photoNivaLanguageActive = false;
            if (!_photoNivaLanguageWrite.GetAwaiter().GetResult())
                GD.PushWarning("PhotoWorld Niva: the language answer was not saved; it can be set in the settings.");
            else if (_bridge is { HasNivaLanguageResponse: true } answered)
                ShowPrologueCaption($"Татарский в разговорах: {LanguageLevelLabel(answered.TatarLanguageLevel)}. Это можно поменять в настройках.", 4.5);
            _photoNivaNextBarkReady = t + 5.2;
            return;
        }
        // The radio, for company.
        if (PhotoNivaOnce(1, _photoNivaBeat[0] && ready)) StartRideRadio();
        // The last field: the forest is a neighbour, and neighbours are greeted.
        if (ready && PhotoNivaOnce(2, _photoNivaBeat[1] && d >= 22f))
        {
            _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-fields") + 1.2;
            return;
        }
        // The trees close over the road and the music comes apart into a name.
        if (!talking && PhotoNivaOnce(3, d >= PhotoNivaFieldBeforeForest + 6f))
        {
            StartRideRadio();
            RadioCatchesName();
        }
        // Someone stands among the trunks, far too tall. Babai has not turned his head.
        var figure = road.EdgeFigure;
        if (figure is { Visible: true })
        {
            var gap = visual.Root.GlobalPosition.DistanceTo(figure.GlobalPosition);
            if (ready && PhotoNivaOnce(4, gap < 62f))
                _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-forest") + 1.6;
            // It is there until the car is close; then a trunk passes and it is not.
            if (gap < 30f) figure.Visible = false;
        }
        if (PhotoNivaOnce(5, figure is { Visible: false } && _photoNivaBeat[4] && t >= _photoNivaNextBarkReady - .4))
        {
            ShowPrologueCaption("Айдар (про себя): «Там кто-то стоял. Или показалось».", 4.2);
            _photoNivaNextBarkReady = Math.Max(_photoNivaNextBarkReady, t + 5.0);
            return;
        }
        // Deep in the forest, the thing they are both not saying.
        if (ready && PhotoNivaOnce(6, (_photoNivaBeat[5] || figure is not { Visible: true }) && remaining < PhotoNivaApproachLength * .5f && remaining > PhotoNivaTitleBeforeEnd + 40f))
        {
            _photoNivaNextBarkReady = t + RideBark(_bridge, "prologue-ride-bark-marat") + 1.2;
            return;
        }
        // The forest lets go, the village opens, and the film gets its name.
        // (When he carries the mitten, the name waits for the black after the gate.)
        if (!PhotoNivaGatePending && PhotoNivaOnce(7, remaining <= PhotoNivaTitleBeforeEnd))
        {
            _photoNivaTitleClock = 0f;
            _photoNivaNextBarkReady = Math.Max(_photoNivaNextBarkReady, t + PhotoNivaTitleSeconds + .5);
        }
    }

    /// <summary>After the photograph, his head comes up to the road by itself — unless the player is already looking.</summary>
    private void UpdatePhotoNivaGaze(float delta)
    {
        if (_photoNivaGazeTaken || _photoNivaGaze >= 1f || !_photoNivaWoke) return;
        if (Input.GetVector("look_left", "look_right", "look_up", "look_down").LengthSquared() > .04f
            || _rideLook.DistanceTo(PhotoNivaLapGaze.Lerp(PhotoNivaRoadGaze, Mathf.SmoothStep(0f, 1f, _photoNivaGaze))) > 3f)
        {
            _photoNivaGazeTaken = true;
            return;
        }
        if (_photoNivaDriveElapsed < _photoNivaWokeAt + PhotoNivaGazeDelay) return;
        _photoNivaGaze = Mathf.Min(1f, _photoNivaGaze + delta / (_player?.ReducedMotion == true ? .4f : PhotoNivaGazeSeconds));
        _rideLook = PhotoNivaLapGaze.Lerp(PhotoNivaRoadGaze, Mathf.SmoothStep(0f, 1f, _photoNivaGaze));
        ApplyPrologueRideLook();
    }

    // ---- Waking ---------------------------------------------------------------------------

    // Black, the familiar voice, then the eyes open over the physical print.
    private void StartPhotoNivaWakeVoice()
    {
        _photoNivaVoiceStarted = true;
        var waking = PrologueVoice.PlayClip(this, "forest-wake-v2");
        _photoNivaWakeVoiceEnds = _photoNivaDriveElapsed + waking;
        _photoNivaNextBarkReady = _photoNivaWakeVoiceEnds + 3.2;
        ShowPrologueCaption("Мансур бабай: «Айдар… Айдар, уян. Килеп җитәбез.»\n(Айдар… Айдар, проснись. Уже подъезжаем.)",
            Math.Max(4.5, waking + .3));
    }

    private void UpdatePhotoNivaWake(float t)
    {
        if (!_photoNivaVoiceStarted && t >= _photoNivaVoiceDelay) StartPhotoNivaWakeVoice();
        if (_photoNivaVoiceStarted && !_photoNivaPrintRecognized && t >= _photoNivaWakeVoiceEnds + .8)
        {
            _photoNivaPrintRecognized = true;
            if (_photoNivaLapBook is not null)
                ShowPrologueCaption("Айдар (про себя): «Тот же лес. Он на снимке».", 3.6);
        }
        if (_prologueBlackout is { } blackout)
        {
            // Two lids: a first heavy blink, a second, and the cabin stays.
            float alpha;
            var since = t - (float)_photoNivaWakeHold;
            if (_player?.ReducedMotion == true) alpha = 1f - Mathf.Clamp(since / .5f, 0f, 1f);
            else if (since < 0f) alpha = 1f;
            else if (since < .22f) alpha = Mathf.Lerp(1f, .32f, since / .22f);
            else if (since < .4f) alpha = Mathf.Lerp(.32f, .86f, (since - .22f) / .18f);
            else alpha = Mathf.Lerp(.86f, 0f, Mathf.Clamp((since - .4f) / .75f, 0f, 1f));
            blackout.Color = Colors.Black with { A = Mathf.Max(alpha, _photoNivaGateBlack) };
        }
        if (_photoNivaWoke || t < _photoNivaWakeHold) return;
        _photoNivaWoke = true;
        _photoNivaWokeAt = t;
        // Development: URMAN_RIDE_TITLE_TEST=1 shows the title card at once, to check it without the whole road.
        if (System.Environment.GetEnvironmentVariable("URMAN_RIDE_TITLE_TEST") == "1") _photoNivaTitleClock = 0f;

        if (_rideCamera is { } camera)
            UiFoley.PlayWorld(this, camera.GlobalPosition, "forest/gasp", -8f, 6f, 2f);
    }

    // ---- The road under the car -----------------------------------------------------------

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
        // A soft follow: the body leans into the road instead of snapping to it.
        var smoothing = snap ? 1f : 1f - MathF.Exp(-4f * (float)GetProcessDeltaTime());
        visual.Root.Rotation = new Vector3(Mathf.LerpAngle(visual.Root.Rotation.X, pitch, smoothing),
            Mathf.LerpAngle(visual.Root.Rotation.Y, yaw, smoothing), 0);
        visual.Root.GlobalPosition = transformedHere;
    }

    // ---- The gate --------------------------------------------------------------------------
    //
    // The engine stops. It was a dream: the bus, the tiredness, the road. He puts
    // his hand in his pocket. What babai says next is the end of the prologue.

    private void BeginPhotoNivaGate()
    {
        _photoNivaGateClock = 0f;
        _photoNivaGateCut = false;
        Array.Clear(_photoNivaGateBeat);
        StopRideRadio(1.4);
        _photoNivaDriveSpeed = 0f;
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, false, 0f, 0f, false);
    }

    /// <returns>True when the scene has ended and the ride has been handed over.</returns>
    private bool UpdatePhotoNivaGate(float delta)
    {
        _photoNivaGateClock += delta;
        var g = _photoNivaGateClock;
        bool At(int beat, float seconds)
        {
            if (_photoNivaGateBeat[beat] || g < seconds) return false;
            _photoNivaGateBeat[beat] = true;
            return true;
        }
        _photoNivaDriveAudio?.SetState(VehicleKind.Niva, false, 0f, 0f, false);
        if (At(0, 1.2f)) ShowPrologueCaption("Айдар (про себя): «Сон. Автобус, усталость, дорога. Просто сон».", 3.2);
        if (At(1, 4.9f))
        {
            // His hand comes out of the pocket with it and lays it on the open album.
            if (_rideNiva is { } car && IsInstanceValid(car))
            {
                _photoNivaGateMitten = PrologueDeepForest.AddMitten(car, new Vector3(.5f, 1.02f, -.2f), new Vector3(84f, 28f, 0f));
                _photoNivaGateMitten.Scale = Vector3.One * .01f;
                _photoNivaGateMitten.CreateTween().TweenProperty(_photoNivaGateMitten, "scale", Vector3.One * .82f, .5)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                foreach (var mesh in _photoNivaGateMitten.FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>())
                    mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
            if (_rideCamera is { } ear) UiFoley.PlayWorld(this, ear.GlobalPosition, "forest/brush_rustle", -16f, 4f, 2f);
            ShowPrologueCaption("Айдар (про себя): «В кармане мокро. Что-то шерстяное».", 3.0);
        }
        if (At(2, 8.3f)) ShowPrologueCaption("Айдар (про себя): «Синяя детская варежка. Та самая. Снег на ней ещё не растаял».", 4.2);
        if (At(3, 13.0f)) ShowPrologueCaption("Мансур бабай: «…Где взял?»", 2.3);
        if (At(4, 15.6f)) ShowPrologueCaption("Айдар: «В лесу. Во сне».", 2.3);
        if (At(5, 18.4f)) ShowPrologueCaption("Мансур бабай: «Убери. Әби не показывай».", 3.2);
        if (At(6, 22.2f))
        {
            if (_rideCamera is { } ear) UiFoley.PlayWorld(this, ear.GlobalPosition, "forest/dread_drone", -14f, 6f, 3f);
            ShowPrologueCaption("Мансур бабай: «Я тебя не на остановке подобрал, улым.\nТы вышел на дорогу из леса. Сам».", 4.8);
        }
        // His eyes: down to what is in his hand, then to the old man, who is not looking back.
        if (g >= 4.6f && g < PhotoNivaGateCutAt)
        {
            var target = g < 12.6f ? new Vector2(4f, -40f) : new Vector2(38f, -3f);
            _rideLook = _rideLook.Lerp(target, 1f - Mathf.Exp(-(_player?.ReducedMotion == true ? 9f : 2.6f) * delta));
            ApplyPrologueRideLook();
        }
        // Black. And only now, the name.
        if (At(7, PhotoNivaGateCutAt))
        {
            _photoNivaGateCut = true;
            _photoNivaGateBlack = 1f;
            _photoNivaTitleClock = 0f;
            ShowPrologueCaption(string.Empty, .01);
            if (_rideCamera is { } ear) UiFoley.PlayWorld(this, ear.GlobalPosition, "forest/body_fall", -5f, 6f, 3f);
            if (_photoNivaGateMitten is { } mitten && IsInstanceValid(mitten)) mitten.QueueFree();
            _photoNivaGateMitten = null;
        }
        var back = PhotoNivaGateCutAt + PhotoNivaTitleSeconds + .4f;
        if (g >= back)
        {
            _photoNivaGateBlack = Mathf.MoveToward(_photoNivaGateBlack, 0f, delta / 1.3f);
            _rideLook = new Vector2(35f, 0f);
            ApplyPrologueRideLook();
            if (At(8, back + .3f)) ShowPrologueCaption("Мансур бабай: «Не сейчас. Дома».", 2.6);
        }
        if (g < back + 3.2f) return false;
        _photoNivaGateDone = true;
        _photoNivaGateClock = -1f;
        _photoNivaGateBlack = 0f;
        FinishPhotoNivaDrive();
        return true;
    }

    // ---- End ------------------------------------------------------------------------------

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
        _photoNivaGateClock = -1f;
        _photoNivaGateBlack = 0f;
        if (_photoNivaGateMitten is { } mitten && IsInstanceValid(mitten)) mitten.QueueFree();
        _photoNivaGateMitten = null;
        ReleasePhotoNivaLapBook();
        _photoNivaDriveVisual = null;
        _photoNivaBarTop = _photoNivaBarBottom = _photoNivaGrade = null;
        _photoNivaTitle = null;
        if (_photoNivaCabinLight is { } lamp && IsInstanceValid(lamp)) lamp.QueueFree();
        _photoNivaCabinLight = null;
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
        PrologueDreamCut.Cancel();
        PrologueVoice.Stop();
        PrologueVoice.SetPaused(false);
    }
}
