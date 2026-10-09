using System.Globalization;
using System.Text.Json;
using Godot;
using Urman.Core.Persistence;
using PlayTimeBlock = Urman.Godot.RuntimeBridge.PlayTimeBlock;

namespace Urman.Godot;

/// <summary>
/// The current product entrypoint: a compact, playable Chapter 1 demo.
/// Acts 2–5 remain available to their own smoke/capture scenes, but are not
/// reachable from the demo launch path. This wrapper owns only presentation
/// framing; RuntimeBridge remains the sole narrative-state owner.
/// </summary>
public partial class Act1DemoRoot : Node, IAccessibilitySettingsTarget
{
    private const string CliffhangerBeat = "urman.chapter1:beat/cliffhanger-hard-cut";
    // The forest entry emits Marat's trace and Rinat's interruption together.
    // The final card waits for AudioCueUi to drain its presentation queue; this
    // minimum only prevents a same-frame hard cut when a cue is unavailable.
    private const double CliffhangerMinimumDelaySeconds = 0.6;
    private const double RouteCueVisibleSeconds = 3.2;

    private Main _main = null!;
    private FirstPersonController? _player;
    private MainMenuUi? _mainMenu;
    private PauseMenuUi? _pauseMenu;
    private Control? _introScreen;
    private bool _introReplay;
    private bool _playerSessionSelected;
    private VBoxContainer? _introStack;
    private Label? _introControls;
    private Tween? _introTween;
    private Label? _routeCue;
    private Tween? _routeCueTween;
    private string _lastRouteCue = string.Empty;
    private Control? _endingScreen;
    private VBoxContainer? _endingStack;
    private Label? _endingTitle;
    private Label? _endingCaption;
    private double _endingDelay;
    private bool _endingShown;
    private bool _endingPending;
    private bool _menuBusy;
    private RuntimeBridge? _bridge;
    private bool _performanceProbe;
    private bool _performanceTimingStarted;
    private const string PerformanceProbeModeMenu = "menu";
    private const string PerformanceProbeModeGameplay = "gameplay";
    private const string PerformanceProbeSampleArgumentPrefix = "--urman-perf-sample=";
    private readonly record struct PerformanceSampleTarget(string ZoneId, string SpawnPointId, string? VehicleId = null,
        string? SampleLabel = null, string? FacilitySampleId = null)
    {
        public string Label => SampleLabel ?? $"{ZoneId}@{SpawnPointId}";
    }

    private static readonly PerformanceSampleTarget DefaultPerformanceSample =
        new("village_day", "arrival");

    private const double DefaultPerformanceWarmupSeconds = 12.0;
    private const double DefaultPerformanceDurationSeconds = 60.0;
    private const double MaximumPerformanceWarmupSeconds = 300.0;
    private const double MaximumPerformanceDurationSeconds = 600.0;
    private const double TargetMinimumFps = 58.0;
    private const double TargetP95Milliseconds = 18.0;
    private const double TargetP99Milliseconds = 25.0;
    private const double TargetLongFrameFraction = 0.005;
    private string _performanceProbeMode = PerformanceProbeModeGameplay;
    private bool _performanceProbeConfigurationValid = true;
    private PerformanceSampleTarget _performanceSample = DefaultPerformanceSample;
    private bool _performanceWindowed;
    private bool _performanceRealRenderer;
    private double _performanceWarmupSeconds = DefaultPerformanceWarmupSeconds;
    private double _performanceDurationSeconds = DefaultPerformanceDurationSeconds;
    private double _performanceWarmupElapsed;
    private double _performanceMeasurementElapsed;
    private ulong _performanceLastTicks;
    private (int Gen0, int Gen1, int Gen2, long Allocated) _performanceLastGc;
    private (ulong Surface, ulong Draw) _performanceLastPipelines;
    private readonly List<double> _performanceWarmupSamples = [];
    private readonly List<double> _performanceSamples = [];
    private int _performanceFocusedSamples;
    private int _performanceObscuredSamples;
    private bool _performanceRequestFocus;
    private double _performanceNextFocusRequest;
    private readonly List<string> _performanceStallReports = new(8);
    private Act1VehiclePerformanceRoute? _vehiclePerformanceRoute;
    private bool _vehiclePerformanceTimingStarted;
    private bool _vehiclePerformanceMeasurementStarted;
    private Act1ConnectedWorld.FacilityPerformanceView? _facilityPerformanceView;
    private object? _facilityPerformanceSession;
    private ulong _facilityPerformanceStartedUsec, _facilityPerformanceLastPhysics;
    private Vector3 _facilityPerformanceFeet, _facilityPerformanceForward;
    private int _facilityPerformanceStableFrames, _facilityPerformancePoseWrites;
    private int _facilityPerformanceRevision, _facilityPerformanceRecoveries, _facilityPerformanceClamps;
    private bool _facilityPerformanceReady;
    private string _facilityPerformanceFailure = string.Empty;
    private string? _facilityPerformanceSupportOwner;
    private float? _facilityPerformanceSupportGap;
    private Camera3D? _staticPerformanceCamera;
    private RuntimeBridge? _staticPerformanceBridge;
    private object? _staticPerformanceSession;
    private Vector3 _staticPerformanceStartFeet, _staticPerformanceForward, _staticPerformanceMeasuredFeet, _staticPerformanceEyeOffset;
    private int _staticPerformanceRevision, _staticPerformanceRecoveries, _staticPerformanceClamps;
    private float _staticPerformanceFov, _staticPerformanceMaximumHorizontalDrift, _staticPerformanceMaximumVerticalDrift;
    private float _staticPerformanceMinimumForwardDot = 1f;
    private float _staticPerformanceMaximumEyeOffsetDrift;
    private bool _staticPerformanceMeasurementStarted;
    private string? _staticPerformanceFailure;
    private ulong _staticPerformanceFirstFailureFrame;
    private bool _performanceFinishing;
    private bool _startupPerformanceGuard;
    private int _startupGuardWarmupFrames;
    private ulong _startupGuardLastTicks;
    private double _startupGuardElapsed;
    private readonly List<double> _startupGuardSamples = [];
    private bool _m10TimingEnabled;
    private string _m10RunId = string.Empty;
    private string _m10BaseMode = "ordinary-observation";
    private string _m10LastMode = string.Empty;
    private ulong _m10StartedUsec, _m10LastSampleUsec, _m10LastRecordUsec;
    private PlayTimeBlock _m10PreviousBlocks = PlayTimeBlock.NotReady;
    private string? _m10LastScene, _m10LastZone, _m10LastSpawn;
    private JsonElement? _m10Beats;
    private double _m10ActiveSeconds, _m10PauseSeconds, _m10SettingsSeconds;
    private double _m10FocusLostSeconds, _m10LoadingSeconds, _m10MenuSeconds;
    private double _m10EndingSeconds, _m10NotReadySeconds;

    internal bool M10TimingEnabled => _m10TimingEnabled;
    internal double M10ElapsedSeconds => _m10TimingEnabled
        ? (Time.GetTicksUsec() - _m10StartedUsec) / 1_000_000.0 : 0;

    public Main DemoMain => _main;

    public bool DemoEnded => _endingShown;

    public bool EndingPending => _endingPending;

    public double EndingDelaySeconds => _endingDelay;

    public bool IntroVisible => _introScreen is not null || _prologueForestActive;

    /// <summary>The forest teaser or Niva ride owns the screen; E is ignored, Esc skips.</summary>
    internal bool PrologueActive => _prologueForestActive;

    /// <summary>UIUX-001: the public main menu gates gameplay until a choice.</summary>
    public bool MainMenuVisible => _mainMenu is not null && GodotObject.IsInstanceValid(_mainMenu) && !_mainMenu.IsDismissed;

    public MainMenuUi? MainMenu => _mainMenu;

    public string? IntroControlsText => _introControls?.Text;

    public string? EndingTitleText => _endingTitle?.Text;

    public string? EndingCaptionText => _endingCaption?.Text;

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        if (_introStack is not null && GodotObject.IsInstanceValid(_introStack))
        {
            AccessibilityPresentation.ApplyToControl(_introStack, settings);
            if (_introStack.GetParent() is PanelContainer panel)
                panel.OffsetTop = -245f - 115f * (float)(settings.TextScale - 1.0);
        }

        if (_endingStack is not null && GodotObject.IsInstanceValid(_endingStack))
        {
            AccessibilityPresentation.ApplyToControl(_endingStack, settings);
        }

        if (_routeCue is not null && GodotObject.IsInstanceValid(_routeCue))
        {
            _routeCue.Scale = Vector2.One * (float)settings.TextScale;
            _routeCue.SetMeta("accessibilityTextScale", settings.TextScale);
            _routeCue.SetMeta("accessibilityHighContrast", settings.HighContrast);
            _routeCue.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
            _routeCue.AddThemeColorOverride(
                "font_color",
                settings.HighContrast ? Colors.White : new Color(0.94f, 0.89f, 0.76f));
            _routeCue.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _routeCue.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
            _routeCue.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        }
    }

    public override void _Ready()
    {
        DevRideCaptureBoot();
        DevViewCaptureBoot();
        DevWalkProbeBoot();
        DevQuickStartBoot();
        _ = InitializeDemoWithLoadingScreenAsync();
    }

    private async Task InitializeDemoWithLoadingScreenAsync()
    {
        LoadingScreenUi? loading = null;
        try
        {
            if (DisplayServer.GetName() == "headless")
            {
                InitializeDemo();
                return;
            }

            loading = LoadingScreenUi.Show(this, "Кара-Урман", "Подготавливаем деревню и первую встречу");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (!IsInsideTree()) return;
            InitializeDemo();
        }
        catch (Exception exception)
        {
            GD.PushError($"Act I startup failed: {exception}");
            var arguments = OS.GetCmdlineArgs();
            var unattendedSmoke = arguments.Contains("--urman-smoke-background-input", StringComparer.Ordinal)
                && arguments.Any(argument => argument.StartsWith("res://tests/", StringComparison.Ordinal)
                    && argument.EndsWith(".tscn", StringComparison.Ordinal));
            // Native smoke failures must reach their exit code and userdata
            // guard without waiting for acknowledgement of a blocking OS alert.
            if (DisplayServer.GetName() != "headless" && !unattendedSmoke)
            {
                OS.Alert(
                    "Не удалось запустить Акт I.\nЗаново распакуйте архив игры и повторите запуск.",
                    "УРМАН — ошибка запуска");
            }

            GetTree().Quit(1);
        }
        finally
        {
            if (loading is { } screen && GodotObject.IsInstanceValid(screen)) screen.Hide();
        }
    }

    private void InitializeDemo()
    {
        AddToGroup(AccessibilityPresentation.TargetGroup);
        AddToGroup("act1_demo_root");
        DebugVillageMinimap.AttachIfEnabled(this);
        var commandLine = OS.GetCmdlineArgs();
        _performanceProbe = commandLine.Contains("--urman-perf-probe", StringComparer.Ordinal)
            || commandLine.Any(argument => argument.StartsWith(
                "--urman-perf-probe-mode=",
                StringComparison.Ordinal));
        ConfigureM10Timing(commandLine);
        if (_performanceProbe)
        {
            // Select the already-authored connected-world placement before
            // Main enters the tree. With no probe flag this remains the exact
            // ordinary arrival bootstrap; menu diagnostics also stay there.
            _performanceSample = ParsePerformanceSample(
                commandLine,
                out _performanceProbeConfigurationValid);
        }

        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Act 1 demo could not load the first-person main scene.");
        _main = packed.Instantiate<Main>()
            ?? throw new InvalidOperationException("Act 1 demo main scene did not instantiate Main.");
        _main.InitialZoneId = _performanceSample.ZoneId;
        _main.InitialSpawnPointId = _performanceSample.SpawnPointId;
        _main.EnableZoneTransitionFade = true;
        _main.EnableAct1ConnectedWorld = true;
        AddChild(_main);
        _player = _main.GetNodeOrNull<FirstPersonController>("Player")
            ?? throw new InvalidOperationException("Act I player did not load.");
        // A child _Ready exception is logged by Godot instead of propagating
        // through AddChild. Check the completed owners before exposing a menu.
        if (_main.ConnectedWorld?.IsBuilt != true
            || _main.GetNodeOrNull<RuntimeBridge>("RuntimeBridge")?.SessionIdentity is null)
        {
            throw new InvalidOperationException("Act I world or campaign did not finish loading.");
        }

        // The protected view-capture comparison applies its requested player preset once,
        // before the startup performance guard can rescue a slow machine to Low.
        ApplyDevViewCaptureGraphicsPresetBeforeStartupGuard();

        DebugWorldGrid.AttachIfEnabled(this);
        DebugSoundPanel.AttachIfEnabled(this);

        BuildMainMenu();
        if (_mainMenu?.NewGameButton is null)
        {
            throw new InvalidOperationException("Act I main menu did not load.");
        }

        BuildPauseMenu();
        BuildRouteCue();
        CallDeferred(nameof(AttachRuntimeBridge));
        if (StudioPlayRequest.Parse([.. commandLine, .. OS.GetCmdlineUserArgs()]) is { } studioPlay)
        {
            _ = StartStudioPlayAsync(studioPlay);
        }

        _startupPerformanceGuard = !_performanceProbe
            && !commandLine.Contains("--no-auto-performance-fallback", StringComparer.Ordinal);
        if (_performanceProbe)
        {
            ConfigurePerformanceProbe(commandLine);
            ConfigureRendererDiagnostics();
            _performanceLastTicks = Time.GetTicksUsec();
            _performanceLastGc = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalAllocatedBytes(false));
            _performanceLastPipelines = (RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSurface),
                RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsDraw));
        }

        if (_startupPerformanceGuard)
        {
            _startupGuardWarmupFrames = 3;
            _startupGuardLastTicks = Time.GetTicksUsec();
        }
    }

    public override void _Process(double delta)
    {
        RecordM10Timing();
        if (_performanceProbe)
        {
            RecordPerformanceProbeFrame();
            if (!_performanceProbe)
            {
                return;
            }
        }

        if (_startupPerformanceGuard)
        {
            RecordStartupPerformanceGuard(delta);
        }

        UpdateIntroControls();
        UpdateIntroFlyover(delta);
        if (_routeCueTween is { } cueTween && cueTween.IsValid() && _routeCue is not null)
        {
            // As with side-quest banners, keep the unread time for gameplay.
            // Killing this tween would leave the cached text permanently hidden.
            var modal = _player?.ModalOpen == true;
            _routeCue.Visible = !modal;
            if (modal) cueTween.Pause();
            else cueTween.Play();
        }

        if (_endingShown || !_endingPending || MainMenuVisible || IntroVisible || _pauseMenu?.IsOpen == true)
        {
            return;
        }

        _endingDelay += delta;
        if (_endingDelay < CliffhangerMinimumDelaySeconds)
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup("audio_cue_ui") is AudioCueUi { IsPresenting: true })
        {
            return;
        }

        if (_endingPending)
        {
            ShowEnding();
        }
    }

    public override void _ExitTree()
    {
        StopIntroFlyover();
        StopRendererDiagnostics();
        _vehiclePerformanceRoute?.ReleaseInput();
        RecordM10Timing("process-exit", force: true);
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
            _bridge.PlayTimeBoundary -= OnPlayTimeBoundary;
        }

        _bridge = null;
    }

    private void AttachRuntimeBridge()
    {
        if (!IsInsideTree())
        {
            return;
        }

        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            return;
        }

        _bridge = null;
        if (GetTree().GetFirstNodeInGroup("runtime_bridge") is RuntimeBridge bridge)
        {
            _bridge = bridge;
            _bridge.RuntimeStateChanged += OnRuntimeStateChanged;
            _bridge.PlayTimeBoundary += OnPlayTimeBoundary;
            RecordM10Timing("runtime-ready", force: true);
            RefreshMenuContinueAvailability();
            EvaluateEndingState();
            return;
        }

        CallDeferred(nameof(AttachRuntimeBridge));
    }

    private void OnRuntimeStateChanged()
    {
        if (_m10TimingEnabled && _bridge is { } bridge)
        {
            var state = bridge.SelectRuntimeState();
            _m10Beats = state.TryGetProperty("beats", out var beats) ? beats.Clone() : null;
            RecordM10Timing("runtime-state", force: true);
        }
        EvaluateEndingState();
    }

    private void ConfigureM10Timing(IReadOnlyList<string> arguments)
    {
        _m10TimingEnabled = arguments.Contains("--urman-m10-timing", StringComparer.Ordinal);
        if (!_m10TimingEnabled) return;
        _m10RunId = Guid.NewGuid().ToString("N");
        _m10StartedUsec = _m10LastSampleUsec = _m10LastRecordUsec = Time.GetTicksUsec();
        var testScene = arguments.Any(argument => argument.StartsWith("res://tests/", StringComparison.Ordinal));
        for (Node? node = this; node is not null && !testScene; node = node.GetParent())
            testScene = node.SceneFilePath.StartsWith("res://tests/", StringComparison.Ordinal);
        _m10BaseMode = _performanceProbe ? "technical-performance"
            : testScene || arguments.Contains("--urman-smoke-background-input", StringComparer.Ordinal)
                ? "technical-test" : "ordinary-observation";
    }

    private void OnPlayTimeBoundary(string boundary) => RecordM10Timing(boundary, force: true);

    private void RecordM10Timing(string? boundary = null, bool force = false)
    {
        if (!_m10TimingEnabled) return;
        var now = Time.GetTicksUsec();
        var elapsed = (now - _m10LastSampleUsec) / 1_000_000.0;
        // These categories overlap (for example focus loss opens pause). Active
        // time is their complement, so adding exclusions must not double-count.
        if (_m10PreviousBlocks == PlayTimeBlock.None) _m10ActiveSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.Pause) != 0) _m10PauseSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.Settings) != 0) _m10SettingsSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.Unfocused) != 0) _m10FocusLostSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.Loading) != 0) _m10LoadingSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.MainMenu) != 0) _m10MenuSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.Ending) != 0) _m10EndingSeconds += elapsed;
        if ((_m10PreviousBlocks & PlayTimeBlock.NotReady) != 0) _m10NotReadySeconds += elapsed;
        _m10LastSampleUsec = now;

        var bridge = _bridge is { } candidate && IsInstanceValid(candidate) && candidate.IsInsideTree()
            ? candidate : null;
        var blocks = bridge?.CapturePlayTimeBlocks() ?? PlayTimeBlock.NotReady;
        var scene = bridge?.ActiveSceneId ?? _m10LastScene;
        var zone = bridge?.CurrentZoneId ?? _m10LastZone;
        var spawn = bridge?.CurrentSpawnPointId ?? _m10LastSpawn;
        var mode = bridge?.IsDebugSession == true ? "technical-debug" : _m10BaseMode;
        var changed = blocks != _m10PreviousBlocks || scene != _m10LastScene
            || zone != _m10LastZone || spawn != _m10LastSpawn || mode != _m10LastMode;
        _m10PreviousBlocks = blocks;
        _m10LastScene = scene;
        _m10LastZone = zone;
        _m10LastSpawn = spawn;
        _m10LastMode = mode;
        if (!force && !changed && now - _m10LastRecordUsec < 10_000_000) return;
        _m10LastRecordUsec = now;
        GD.Print("act1-m10-time: " + JsonSerializer.Serialize(new
        {
            run_id = _m10RunId,
            utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            elapsed_seconds = (now - _m10StartedUsec) / 1_000_000.0,
            @event = boundary ?? (changed ? "state" : "heartbeat"),
            mode,
            human_verified = false,
            scene, zone, spawn, beats = _m10Beats,
            blocks = blocks.ToString(),
            active_seconds = _m10ActiveSeconds,
            pause_seconds = _m10PauseSeconds,
            settings_seconds = _m10SettingsSeconds,
            focus_lost_seconds = _m10FocusLostSeconds,
            loading_seconds = _m10LoadingSeconds,
            menu_seconds = _m10MenuSeconds,
            ending_seconds = _m10EndingSeconds,
            not_ready_seconds = _m10NotReadySeconds,
            categories_overlap = true,
            sampling = "process-frame; exact load boundaries",
            saved_play_time_seconds = bridge?.PlayTimeSeconds,
            act_completed = DemoEnded,
            intro_visible = IntroVisible
        }));
    }

    private void EvaluateEndingState()
    {
        var bridge = _bridge;
        if (_endingShown || bridge is null || !GodotObject.IsInstanceValid(bridge))
        {
            return;
        }

        if (bridge.ActiveSceneId != "urman.chapter1:scene/forest"
            && bridge.CurrentZoneId != "kara_urman_night")
        {
            _endingPending = false;
            _endingDelay = 0;
            UpdateRouteCue();
            return;
        }

        try
        {
            var state = bridge.SelectRuntimeState();
            _endingPending = state.TryGetProperty("beats", out var beats)
                && beats.TryGetProperty(CliffhangerBeat, out var beat)
                && beat.GetString() == "completed";
        }
        catch (Exception)
        {
            _endingPending = false;
        }

        if (!_endingPending)
        {
            _endingDelay = 0;
        }
        else
        {
            HideRouteCue();
        }

        UpdateRouteCue();
    }

    private void RecordPerformanceProbeFrame()
    {
        if (_main.ConnectedWorld?.IsBuilt != true)
        {
            GD.Print("act1-demo-package-performance: status=INVALID reason=world-build-failed");
            FinishPerformanceProbe(2);
            return;
        }
        var now = Time.GetTicksUsec();
        // World construction before the first process frame can take longer
        // than warmup. Start timing on the following frame.
        if (!_performanceTimingStarted)
        {
            _performanceTimingStarted = true;
            _performanceLastTicks = now;
            return;
        }
        var frameMilliseconds = (now - _performanceLastTicks) / 1000.0;
        _performanceLastTicks = now;
        if (!_performanceProbeConfigurationValid)
        {
            GD.Print(string.Join(' ',
                "act1-demo-package-performance:",
                "status=INVALID",
                $"mode={_performanceProbeMode}",
                "reason=invalid-probe-arguments"));
            FinishPerformanceProbe(2);
            return;
        }

        if (!_performanceWindowed || !_performanceRealRenderer)
        {
            GD.Print(string.Join(' ',
                "act1-demo-package-performance:",
                "status=HEADLESS_INVALID",
                $"mode={_performanceProbeMode}",
                $"display_driver={ProbeToken(DisplayServer.GetName())}",
                $"adapter={ProbeToken(RenderingServer.GetVideoAdapterName())}",
                "window_fps_valid=false",
                "reason=windowed-rendering-device-required"));
            FinishPerformanceProbe(2);
            return;
        }

        // Only the explicitly requested probe samples managed counters. Keep
        // individual stalls attributable instead of hiding them in percentiles.
        var gc = (Gen0: GC.CollectionCount(0), Gen1: GC.CollectionCount(1),
            Gen2: GC.CollectionCount(2), Allocated: GC.GetTotalAllocatedBytes(false));
        var pipelines = (Surface: RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSurface),
            Draw: RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsDraw));
        var focused = DisplayServer.WindowIsFocused();
        // An explicit native benchmark may ask for foreground during warmup.
        // Measurement never steals focus or closes an overlay: losing either
        // condition remains visible in the receipt and invalidates the run.
        if (_performanceRequestFocus && _performanceWarmupElapsed < _performanceWarmupSeconds)
        {
            var focusRequestClock = _vehiclePerformanceRoute?.ElapsedSeconds
                ?? (_facilityPerformanceView is not null && !_facilityPerformanceReady
                    ? (now - _facilityPerformanceStartedUsec) / 1_000_000.0 : _performanceWarmupElapsed);
            if (!focused && focusRequestClock >= _performanceNextFocusRequest)
            {
                DisplayServer.WindowMoveToForeground();
                _performanceNextFocusRequest = focusRequestClock + 1.0;
            }
            else if (focused && _pauseMenu?.IsOpen == true)
            {
                _pauseMenu.Resume();
                GD.Print("act1-perf: resumed focus-loss pause during requested foreground warmup");
            }
        }
        if (_performanceSample.FacilitySampleId is not null && !UpdateFacilityPerformancePlacement())
        {
            _performanceLastGc = gc;
            _performanceLastPipelines = pipelines;
            if (!string.IsNullOrEmpty(_facilityPerformanceFailure))
                FinishPerformanceProbe(2,
                    $"act1-demo-package-performance: status=LOCATION_INVALID mode={_performanceProbeMode} sample={ProbeToken(_performanceSample.Label)} reason={ProbeToken(_facilityPerformanceFailure)}");
            return;
        }
        if (_vehiclePerformanceRoute is { } route)
        {
            if (!string.IsNullOrEmpty(route.Failure))
            {
                FinishPerformanceProbe(2);
                return;
            }
            if (!route.MeasurementReady)
            {
                _performanceLastGc = gc;
                _performanceLastPipelines = pipelines;
                return; // Real walking, entry and ignition are outside the timed interval.
            }
            if (!_vehiclePerformanceTimingStarted)
            {
                _vehiclePerformanceTimingStarted = true;
                _performanceLastGc = gc;
                _performanceLastPipelines = pipelines;
                return;
            }
        }
        if (frameMilliseconds > 100.0)
        {
            var warming = _performanceWarmupElapsed < _performanceWarmupSeconds;
            var life = _main.ConnectedWorld.GetNodeOrNull<Node3D>("Act1CoreWorldGreybox/VillageLife");
            _performanceStallReports.Add(string.Create(CultureInfo.InvariantCulture, $"act1-perf-stall: utc={DateTimeOffset.UtcNow:O} ticks_us={now} "
                + $"phase={(warming ? "warmup" : "measurement")} sample={(warming ? _performanceWarmupSamples.Count : _performanceSamples.Count) + 1} "
                + $"target={ProbeToken(_performanceSample.Label)} "
                + $"elapsed_s={_performanceWarmupElapsed + _performanceMeasurementElapsed + frameMilliseconds / 1000.0:F3} frame_ms={frameMilliseconds:F3} "
                + $"gc0_delta={gc.Gen0 - _performanceLastGc.Gen0} gc1_delta={gc.Gen1 - _performanceLastGc.Gen1} gc2_delta={gc.Gen2 - _performanceLastGc.Gen2} "
                + $"allocated_delta={gc.Allocated - _performanceLastGc.Allocated} focused={focused} "
                + $"life_event={life?.GetMeta("event", 0).AsInt32() ?? 0} life_time={life?.GetMeta("eventTime", 0f).AsDouble() ?? 0:F3} "
                + $"last_process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:F3} "
                + $"last_physics_ms={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0:F3} "
                + $"pipeline_surface_delta={pipelines.Surface - _performanceLastPipelines.Surface} "
                + $"pipeline_draw_delta={pipelines.Draw - _performanceLastPipelines.Draw}"));
        }
        _performanceLastGc = gc;
        _performanceLastPipelines = pipelines;

        ObserveStaticPerformancePose(_performanceWarmupElapsed >= _performanceWarmupSeconds);
        if (_performanceWarmupElapsed < _performanceWarmupSeconds)
        {
            _performanceWarmupSamples.Add(frameMilliseconds);
            _performanceWarmupElapsed += frameMilliseconds / 1000.0;
            ResetRendererInputObservation();
            return;
        }

        if (_vehiclePerformanceRoute is { } measuringRoute && !_vehiclePerformanceMeasurementStarted)
        {
            _vehiclePerformanceMeasurementStarted = true;
            measuringRoute.BeginMeasurement();
            return;
        }
        _performanceSamples.Add(frameMilliseconds);
        RecordRendererDiagnosticSample(frameMilliseconds, focused);
        if (focused) _performanceFocusedSamples++;
        if (_pauseMenu?.IsOpen == true || IntroVisible || _mainMenu is not null || _player?.ModalOpen != false)
            _performanceObscuredSamples++;
        _performanceMeasurementElapsed += frameMilliseconds / 1000.0;
        if (_performanceMeasurementElapsed < _performanceDurationSeconds)
        {
            return;
        }

        var sorted = _performanceSamples.OrderBy(value => value).ToArray();
        var average = _performanceSamples.Average();
        var p95 = Percentile(sorted, 0.95);
        var p99 = Percentile(sorted, 0.99);
        var maximum = _performanceSamples.Max();
        var fps = 1000.0 / Math.Max(average, 0.001);
        var longFrameFraction = _performanceSamples.Count(value => value > 33.3)
            / (double)_performanceSamples.Count;
        var warmupAverage = _performanceWarmupSamples.Count == 0
            ? 0
            : _performanceWarmupSamples.Average();
        var warmupMaximum = _performanceWarmupSamples.Count == 0
            ? 0
            : _performanceWarmupSamples.Max();
        var player = _player;
        var viewport = GetViewport();
        var windowSize = DisplayServer.WindowGetSize();
        var viewportSize = viewport.GetVisibleRect().Size;
        var renderingMethod = ProjectSettings
            .GetSetting("rendering/renderer/rendering_method", "unknown")
            .AsString();
        var camera = viewport.GetCamera3D();
        var vehicleRouteValid = _vehiclePerformanceRoute?.ValidateMeasurement() ?? true;
        var facilityLocationValid = _facilityPerformanceView is null
            || _facilityPerformanceReady && ValidateFacilityPerformancePose(checkSupport: true);
        var staticLocationValid = _performanceSample.VehicleId is not null || _performanceSample.FacilitySampleId is not null
            || _staticPerformanceMeasurementStarted && _staticPerformanceFailure is null;
        var focusValid = _performanceFocusedSamples / (double)_performanceSamples.Count >= .95;
        var sceneVisible = _performanceObscuredSamples == 0;
        var performancePass = _performanceWindowed
            && _performanceRealRenderer
            && focusValid && sceneVisible && vehicleRouteValid && facilityLocationValid && staticLocationValid
            && string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal)
            && _performanceWarmupSeconds >= DefaultPerformanceWarmupSeconds
            && _performanceDurationSeconds >= DefaultPerformanceDurationSeconds
            && fps >= TargetMinimumFps
            && p95 <= TargetP95Milliseconds
            && p99 <= TargetP99Milliseconds
            && longFrameFraction <= TargetLongFrameFraction
            && maximum <= 100.0;
        var shortProbe = _performanceWarmupSeconds < DefaultPerformanceWarmupSeconds
            || _performanceDurationSeconds < DefaultPerformanceDurationSeconds;
        var status = !_performanceWindowed || !_performanceRealRenderer
            ? "HEADLESS_INVALID"
            : !string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal)
                ? "MENU_DIAGNOSTIC"
                : shortProbe ? "DIAGNOSTIC_SHORT"
                : !focusValid ? "FOCUS_INVALID"
                : !sceneVisible ? "OVERLAY_INVALID"
                : !facilityLocationValid || !staticLocationValid ? "LOCATION_INVALID"
                : !vehicleRouteValid ? "ROUTE_INVALID"
                : performancePass ? "PASS" : "FAIL";

        foreach (var stallReport in _performanceStallReports)
        {
            GD.Print(stallReport);
        }

        var report = string.Join(' ',
            "act1-demo-package-performance:",
            $"status={status}",
            $"mode={_performanceProbeMode}",
            $"sample={ProbeToken(_performanceSample.Label)}",
            $"avg={average.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"p95={p95.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"p99={p99.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"max={maximum.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"long_frame_fraction={longFrameFraction.ToString("F5", CultureInfo.InvariantCulture)}",
            $"warmup_avg={warmupAverage.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"warmup_max={warmupMaximum.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"warmup_seconds={_performanceWarmupElapsed.ToString("F2", CultureInfo.InvariantCulture)}",
            $"sample_seconds={_performanceMeasurementElapsed.ToString("F2", CultureInfo.InvariantCulture)}",
            $"sample_count={_performanceSamples.Count}",
            $"focused_sample_count={_performanceFocusedSamples}",
            $"obscured_sample_count={_performanceObscuredSamples}",
            $"foreground_requested={_performanceRequestFocus.ToString().ToLowerInvariant()}",
            $"focused_sample_fraction={(_performanceFocusedSamples / (double)_performanceSamples.Count).ToString("F5", CultureInfo.InvariantCulture)}",
            $"fps={(_performanceWindowed && _performanceRealRenderer ? fps.ToString("F2", CultureInfo.InvariantCulture) : "n/a")}",
            $"window_fps_valid={(_performanceWindowed && _performanceRealRenderer).ToString().ToLowerInvariant()}",
            $"display_driver={ProbeToken(DisplayServer.GetName())}",
            $"adapter={ProbeToken(RenderingServer.GetVideoAdapterName())}",
            $"rendering_method={ProbeToken(renderingMethod)}",
            $"preset={player?.GraphicsPreset ?? "unknown"}",
            $"graphics_overrides={GraphicsQuality.ProbeOverrideSummary()}",
            $"scale={viewport.Scaling3DScale.ToString("F2", CultureInfo.InvariantCulture)}",
            $"msaa={viewport.Msaa3D}",
            $"fov={(camera?.Fov ?? 0).ToString("F1", CultureInfo.InvariantCulture)}",
            $"window={windowSize.X}x{windowSize.Y}",
            $"viewport={viewportSize.X.ToString("F0", CultureInfo.InvariantCulture)}x{viewportSize.Y.ToString("F0", CultureInfo.InvariantCulture)}",
            $"vsync={DisplayServer.WindowGetVsyncMode()}",
            $"engine_fps={Performance.GetMonitor(Performance.Monitor.TimeFps).ToString("F2", CultureInfo.InvariantCulture)}",
            $"process_ms={(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0).ToString("F3", CultureInfo.InvariantCulture)}",
            $"physics_ms={(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0).ToString("F3", CultureInfo.InvariantCulture)}",
            $"draw_calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame).ToString("F0", CultureInfo.InvariantCulture)}",
            $"primitives={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame).ToString("F0", CultureInfo.InvariantCulture)}");

        var exitCode = !string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal)
            || shortProbe || !focusValid || !sceneVisible || !vehicleRouteValid || !facilityLocationValid || !staticLocationValid
            ? 2
            : performancePass ? 0 : 1;
        FinishPerformanceProbe(exitCode, report, status);
    }

    private static PerformanceSampleTarget ParsePerformanceSample(
        IReadOnlyList<string> commandLine,
        out bool valid)
    {
        valid = true;
        var modeArgument = commandLine.FirstOrDefault(argument => argument.StartsWith(
            "--urman-perf-probe-mode=",
            StringComparison.Ordinal));
        if (modeArgument is not null
            && string.Equals(
                modeArgument["--urman-perf-probe-mode=".Length..].Trim(),
                PerformanceProbeModeMenu,
                StringComparison.OrdinalIgnoreCase))
        {
            // Preserve the existing menu diagnostic's arrival surface. The
            // location selector is meaningful only for gameplay measurements.
            return DefaultPerformanceSample;
        }

        var sampleArgument = commandLine.FirstOrDefault(argument => argument.StartsWith(
            PerformanceProbeSampleArgumentPrefix,
            StringComparison.Ordinal));
        if (sampleArgument is null)
        {
            return DefaultPerformanceSample;
        }

        var value = sampleArgument[PerformanceProbeSampleArgumentPrefix.Length..].Trim();
        if (Act1VehiclePerformanceRoute.VehicleForSample(value) is { } vehicleId)
            return new PerformanceSampleTarget("village_day", "arrival", vehicleId, value);
        if (Act1ConnectedWorld.IsFacilityPerformanceSample(value))
            return new PerformanceSampleTarget("village_day", "arrival", SampleLabel: value, FacilitySampleId: value);
        var separator = value.IndexOf('@');
        if (separator <= 0
            || separator == value.Length - 1
            || separator != value.LastIndexOf('@'))
        {
            valid = false;
            return DefaultPerformanceSample;
        }

        var zoneId = value[..separator].Trim();
        var spawnPointId = value[(separator + 1)..].Trim();
        if (!Act1WorldLayout.TryGetWorldSpawn(zoneId, spawnPointId, out _))
        {
            valid = false;
            return DefaultPerformanceSample;
        }

        return new PerformanceSampleTarget(zoneId, spawnPointId);
    }

    private void ConfigurePerformanceProbe(IReadOnlyList<string> commandLine)
    {
        _performanceStallReports.Clear();
        _performanceRequestFocus = commandLine.Contains("--urman-perf-request-focus", StringComparer.Ordinal);
        var modeArgument = commandLine.FirstOrDefault(argument => argument.StartsWith(
            "--urman-perf-probe-mode=",
            StringComparison.Ordinal));
        if (modeArgument is not null)
        {
            _performanceProbeMode = modeArgument["--urman-perf-probe-mode=".Length..]
                .Trim()
                .ToLowerInvariant();
            if (_performanceProbeMode is not PerformanceProbeModeMenu
                and not PerformanceProbeModeGameplay)
            {
                _performanceProbeConfigurationValid = false;
            }
        }

        _performanceWarmupSeconds = ParsePerformanceSeconds(
            commandLine,
            "--urman-perf-warmup-seconds=",
            DefaultPerformanceWarmupSeconds,
            0,
            MaximumPerformanceWarmupSeconds);
        _performanceDurationSeconds = ParsePerformanceSeconds(
            commandLine,
            "--urman-perf-duration-seconds=",
            DefaultPerformanceDurationSeconds,
            1,
            MaximumPerformanceDurationSeconds);
        if (double.IsNaN(_performanceWarmupSeconds) || double.IsNaN(_performanceDurationSeconds))
        {
            _performanceProbeConfigurationValid = false;
            _performanceWarmupSeconds = DefaultPerformanceWarmupSeconds;
            _performanceDurationSeconds = DefaultPerformanceDurationSeconds;
        }

        _performanceWindowed = !commandLine.Contains("--headless", StringComparer.Ordinal)
            && !string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase);
        _performanceRealRenderer = RenderingServer.GetRenderingDevice() is not null;
        if (_performanceWindowed
            && commandLine.Contains("--urman-perf-no-vsync", StringComparer.Ordinal))
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }

        if (string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal))
        {
            // A gameplay probe must never report the main-menu overlay as a
            // world baseline. This is presentation-only and does not touch
            // RuntimeBridge, SaveGameV3 or the user's settings.
            _mainMenu?.Dismiss();
            _mainMenu = null;
            _player?.SetModalOpen(false);
            if (_performanceSample.FacilitySampleId is { } facilitySample
                && _performanceProbeConfigurationValid && _performanceWindowed && _performanceRealRenderer)
            {
                _facilityPerformanceStartedUsec = Time.GetTicksUsec();
                _facilityPerformanceLastPhysics = Engine.GetPhysicsFrames();
                if (_main.ConnectedWorld?.TryGetFacilityPerformanceView(facilitySample, out _facilityPerformanceView) != true)
                    _facilityPerformanceFailure = "authored-facility-view-unavailable";
            }
            if (_performanceSample.VehicleId is { } vehicleId && _player is { } player
                && _performanceProbeConfigurationValid && _performanceWindowed && _performanceRealRenderer)
            {
                _vehiclePerformanceRoute = new Act1VehiclePerformanceRoute();
                _vehiclePerformanceRoute.Configure(_main, player, vehicleId);
                AddChild(_vehiclePerformanceRoute);
            }
            else if (_performanceSample.FacilitySampleId is null && _player is { } stationaryPlayer)
            {
                _staticPerformanceCamera = GetViewport().GetCamera3D();
                _staticPerformanceBridge = _main.GetNode<RuntimeBridge>("RuntimeBridge");
                _staticPerformanceSession = _staticPerformanceBridge.SessionIdentity;
                _staticPerformanceStartFeet = stationaryPlayer.GlobalPosition;
                _staticPerformanceForward = -(_staticPerformanceCamera?.GlobalBasis.Z ?? Vector3.Zero).Normalized();
                _staticPerformanceFov = _staticPerformanceCamera?.Fov ?? 0;
                _staticPerformanceRevision = stationaryPlayer.PresentationTransformRevision;
                _staticPerformanceRecoveries = stationaryPlayer.FallRecoveries;
                _staticPerformanceClamps = stationaryPlayer.EdgeClamps;
            }
        }
    }

    private void ObserveStaticPerformancePose(bool measuring)
    {
        if (_performanceProbeMode != PerformanceProbeModeGameplay || _performanceSample.VehicleId is not null
            || _performanceSample.FacilitySampleId is not null) return;
        var camera = GetViewport().GetCamera3D();
        string? failure = null;
        if (_player is not { } player || camera is null || camera != _staticPerformanceCamera)
            failure = "player-or-camera-owner-changed";
        else
        {
            // Allow the normal initial vertical settling, while retaining the
            // authored horizontal placement and view throughout warmup too.
            var offset = player.GlobalPosition - _staticPerformanceStartFeet;
            var horizontal = new Vector2(offset.X, offset.Z).Length();
            var forwardDot = _staticPerformanceForward.Dot(-camera.GlobalBasis.Z.Normalized());
            _staticPerformanceMaximumHorizontalDrift = Math.Max(_staticPerformanceMaximumHorizontalDrift, horizontal);
            _staticPerformanceMinimumForwardDot = Math.Min(_staticPerformanceMinimumForwardDot, forwardDot);
            if (measuring && !_staticPerformanceMeasurementStarted)
            {
                _staticPerformanceMeasuredFeet = player.GlobalPosition;
                // Spawn starts with a protective crouched collider until the
                // first physical standing-clearance check. Do not use that
                // temporary eye height as the ordinary standing-view baseline.
                _staticPerformanceEyeOffset = camera.GlobalPosition - player.GlobalPosition;
                _staticPerformanceMeasurementStarted = true;
            }
            var eyeDrift = measuring ? (camera.GlobalPosition - player.GlobalPosition).DistanceTo(_staticPerformanceEyeOffset) : 0;
            _staticPerformanceMaximumEyeOffsetDrift = Math.Max(_staticPerformanceMaximumEyeOffsetDrift, eyeDrift);
            var vertical = measuring ? Math.Abs(player.GlobalPosition.Y - _staticPerformanceMeasuredFeet.Y) : 0;
            _staticPerformanceMaximumVerticalDrift = Math.Max(_staticPerformanceMaximumVerticalDrift, vertical);
            if (_staticPerformanceBridge is null || !GodotObject.IsInstanceValid(_staticPerformanceBridge)
                || !ReferenceEquals(_staticPerformanceSession, _staticPerformanceBridge.SessionIdentity)
                || player.PresentationTransformRevision != _staticPerformanceRevision
                || player.FallRecoveries != _staticPerformanceRecoveries || player.EdgeClamps != _staticPerformanceClamps
                || player.VehicleControlled)
                failure = "session-or-physical-placement-owner-changed";
            else if (!float.IsFinite(horizontal) || horizontal >= .01f || !float.IsFinite(forwardDot) || forwardDot <= .99999f
                || !float.IsFinite(eyeDrift) || eyeDrift >= .005f
                || !float.IsFinite(camera.Fov) || Math.Abs(camera.Fov - _staticPerformanceFov) > .001f)
                failure = "authored-horizontal-placement-or-view-changed";
            else if (measuring && (vertical >= .005f || player.IsCrouching || !player.IsOnFloor()
                || player.Velocity.LengthSquared() >= .0001f))
                failure = "measured-standing-pose-changed";
        }
        if (failure is not null && _staticPerformanceFailure is null)
        {
            _staticPerformanceFailure = failure;
            _staticPerformanceFirstFailureFrame = Engine.GetProcessFrames();
        }
    }

    private bool UpdateFacilityPerformancePlacement()
    {
        if (!string.IsNullOrEmpty(_facilityPerformanceFailure)) return false;
        if (_facilityPerformanceView is not { } view || _player is null)
        { _facilityPerformanceFailure = "facility-view-or-player-missing"; return false; }
        if (_facilityPerformanceReady) return ValidateFacilityPerformancePose(checkSupport: false);
        if (Time.GetTicksUsec() - _facilityPerformanceStartedUsec > 10_000_000)
        { _facilityPerformanceFailure = "facility-placement-timeout"; return false; }
        var physics = Engine.GetPhysicsFrames();
        if (physics == _facilityPerformanceLastPhysics) return false;
        _facilityPerformanceLastPhysics = physics;
        if (!DisplayServer.WindowIsFocused() || _player.ModalOpen
            || _main.GetNode<RuntimeBridge>("RuntimeBridge").SessionIdentity is not { } session) return false;
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        if (_facilityPerformancePoseWrites == 0)
        {
            if (!TryFacilityPerformanceFloor(view.StandingCandidate, out var floor))
            { _facilityPerformanceFailure = "authored-facility-floor-missing"; return false; }
            var feet = floor + Vector3.Up * .02f;
            if (!_player.CanStandAt(feet))
            { _facilityPerformanceFailure = "authored-facility-standing-body-blocked"; return false; }
            var delta = view.AimPoint - feet;
            var yaw = Mathf.Atan2(-delta.X, -delta.Z);
            var eyeOffset = _player.GlobalBasis.Inverse() * (camera.GlobalPosition - _player.GlobalPosition);
            var eye = feet + new Basis(Vector3.Up, yaw) * eyeOffset;
            var direction = view.AimPoint - eye;
            var pitch = Mathf.RadToDeg(Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length()));
            // Only this explicitly selected, native gameplay performance fixture
            // places the player; ordinary spawn, quest and save owners are unchanged.
            _player.ApplyPortableTransform(new PlayerTransform(new(feet.X, feet.Y, feet.Z),
                new(pitch, Mathf.RadToDeg(yaw), 0)));
            _facilityPerformancePoseWrites++;
            _facilityPerformanceSession = session;
            _facilityPerformanceFeet = feet;
            _facilityPerformanceForward = -camera.GlobalBasis.Z.Normalized();
            _facilityPerformanceRevision = _player.PresentationTransformRevision;
            _facilityPerformanceRecoveries = _player.FallRecoveries;
            _facilityPerformanceClamps = _player.EdgeClamps;
            GD.Print($"act1-facility-performance-setup: sample={view.SampleId} fixture=single-explicit-presentation-placement room={view.Room.GetPath()} floor={view.FloorOwner.GetPath()} subject={view.ViewSubject.GetPath()} feet={feet} aim={view.AimPoint} quest_writes=0");
            return false;
        }
        if (!ValidateFacilityPerformancePose(checkSupport: true, settling: true)) return false;
        if (!_player.IsOnFloor() || _player.Velocity.LengthSquared() > .0001f)
        { _facilityPerformanceStableFrames = 0; return false; }
        _facilityPerformanceStableFrames++;
        if (_facilityPerformanceStableFrames < 6) return false;
        _facilityPerformanceFeet = _player.GlobalPosition;
        _facilityPerformanceReady = true;
        GD.Print($"act1-facility-performance-ready: sample={view.SampleId} stable_physics_frames={_facilityPerformanceStableFrames} feet={_facilityPerformanceFeet} floor={_facilityPerformanceSupportOwner} gap={_facilityPerformanceSupportGap} warmup_seconds={_performanceWarmupSeconds.ToString(CultureInfo.InvariantCulture)} measurement_seconds={_performanceDurationSeconds.ToString(CultureInfo.InvariantCulture)}");
        return false; // Setup, including this physics result, is outside the timed interval.
    }

    private bool TryFacilityPerformanceFloor(Vector3 feet, out Vector3 floor)
    {
        floor = default;
        if (_facilityPerformanceView is not { } view || _player is null) return false;
        using var query = PhysicsRayQueryParameters3D.Create(feet + Vector3.Up * .12f, feet - Vector3.Up * .22f,
            3, new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _main.ConnectedWorld!.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var owner = hit.Count == 0 ? null : hit["collider"].AsGodotObject() as CollisionObject3D;
        _facilityPerformanceSupportOwner = owner?.GetPath().ToString();
        _facilityPerformanceSupportGap = null;
        if (owner != view.FloorOwner || hit["normal"].AsVector3().Y < .99f) return false;
        floor = hit["position"].AsVector3();
        _facilityPerformanceSupportGap = feet.Y - floor.Y;
        return true;
    }

    private bool ValidateFacilityPerformancePose(bool checkSupport, bool settling = false)
    {
        if (_facilityPerformanceView is not { } view || _player is null) return false;
        var camera = _player.GetNode<Camera3D>("Head/Camera3D");
        var delta = _player.GlobalPosition - _facilityPerformanceFeet;
        var valid = ReferenceEquals(_facilityPerformanceSession, _main.GetNode<RuntimeBridge>("RuntimeBridge").SessionIdentity)
            && _player.PresentationTransformRevision == _facilityPerformanceRevision
            && _player.FallRecoveries == _facilityPerformanceRecoveries && _player.EdgeClamps == _facilityPerformanceClamps
            && !_player.VehicleControlled && GetViewport().GetCamera3D() == camera
            && new Vector2(delta.X, delta.Z).Length() < .01f && Math.Abs(delta.Y) < (settling ? .025f : .005f)
            && _facilityPerformanceForward.Dot(-camera.GlobalBasis.Z.Normalized()) > .99999f
            && (view.AimPoint - camera.GlobalPosition).Normalized().Dot(-camera.GlobalBasis.Z.Normalized()) > .999f
            && _main.ConnectedWorld!.FacilityInteriorAt(_player.GlobalPosition) == view.InteriorId
            && _main.ConnectedWorld.GetMeta("physicalInterior", "").AsString() == view.InteriorId;
        if (!settling) valid &= _player.IsOnFloor() && _player.Velocity.LengthSquared() < .0001f;
        if (checkSupport)
            valid &= TryFacilityPerformanceFloor(_player.GlobalPosition, out _) && _player.CanStandAt(_player.GlobalPosition);
        if (!valid) _facilityPerformanceFailure = "facility-physical-placement-or-camera-changed";
        return valid;
    }

    private static double ParsePerformanceSeconds(
        IReadOnlyList<string> commandLine,
        string prefix,
        double fallback,
        double minimum,
        double maximum)
    {
        var argument = commandLine.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null)
        {
            return fallback;
        }

        var valueText = argument[prefix.Length..];
        if (!double.TryParse(
                valueText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            || !double.IsFinite(value)
            || value < minimum
            || value > maximum)
        {
            return double.NaN;
        }

        return value;
    }

    private static double Percentile(double[] sortedValues, double fraction)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(sortedValues.Length * fraction) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static string ProbeToken(string value) => string.IsNullOrWhiteSpace(value)
        ? "unknown"
        : value.Replace(' ', '_').Replace('\t', '_');

    private async void FinishPerformanceProbe(int exitCode, string? report = null, string? status = null)
    {
        if (_performanceFinishing) return;
        _performanceFinishing = true;
        _performanceProbe = false;
        if (_performanceProbeMode == PerformanceProbeModeGameplay && _performanceSample.VehicleId is null
            && _performanceSample.FacilitySampleId is null)
            GD.Print("act1-static-performance-location: " + JsonSerializer.Serialize(new
            {
                sample = _performanceSample.Label,
                locationValid = _staticPerformanceMeasurementStarted && _staticPerformanceFailure is null,
                failure = _staticPerformanceFailure, firstFailureFrame = _staticPerformanceFirstFailureFrame,
                initialFeet = _staticPerformanceStartFeet.ToString(), measuredStartFeet = _staticPerformanceMeasuredFeet.ToString(),
                initialForward = _staticPerformanceForward.ToString(), initialFov = _staticPerformanceFov,
                measuredStartEyeOffset = _staticPerformanceEyeOffset.ToString(), maximumEyeOffsetDrift = _staticPerformanceMaximumEyeOffsetDrift,
                finalFeet = _player?.GlobalPosition.ToString(), finalCamera = GetViewport().GetCamera3D()?.GlobalTransform.ToString(),
                maximumHorizontalDrift = _staticPerformanceMaximumHorizontalDrift,
                maximumMeasuredVerticalDrift = _staticPerformanceMaximumVerticalDrift,
                minimumForwardDot = _staticPerformanceMinimumForwardDot,
                scope = "observed fixed authored view; ordinary input remains enabled; no pose or input writes"
            }));
        if (_performanceSample.FacilitySampleId is { } facilitySample)
            GD.Print("act1-facility-performance-location: " + JsonSerializer.Serialize(new
            {
                sample = facilitySample, setupValid = _facilityPerformanceReady,
                locationValid = _facilityPerformanceReady && string.IsNullOrEmpty(_facilityPerformanceFailure),
                failure = _facilityPerformanceFailure, poseWrites = _facilityPerformancePoseWrites,
                stablePhysicsFrames = _facilityPerformanceStableFrames,
                room = _facilityPerformanceView?.Room.GetPath().ToString(),
                expectedFloor = _facilityPerformanceView?.FloorOwner.GetPath().ToString(),
                actualFloor = _facilityPerformanceSupportOwner, supportGap = _facilityPerformanceSupportGap,
                subject = _facilityPerformanceView?.ViewSubject.GetPath().ToString(),
                feet = _player?.GlobalPosition.ToString(), aim = _facilityPerformanceView?.AimPoint.ToString(),
                camera = GetViewport().GetCamera3D()?.GlobalTransform.ToString(),
                warmupSeconds = _performanceWarmupSeconds, measurementSeconds = _performanceDurationSeconds,
                traversalProof = false, questWrites = 0, humanDuration = "not-measured"
            }));
        CaptureCompletedPerformanceFrame();
        CaptureRendererCensus();
        if (_vehiclePerformanceRoute is { } route)
        {
            var exited = await route.FinishAsync();
            GD.Print("act1-vehicle-performance-route: " + route.Receipt());
            if (!exited || !string.IsNullOrEmpty(route.Failure))
            {
                exitCode = 2;
                report = report is not null && status is not null
                    ? report.Replace($"status={status}", "status=ROUTE_INVALID", StringComparison.Ordinal)
                    : $"act1-demo-package-performance: status=ROUTE_INVALID mode={_performanceProbeMode} sample={ProbeToken(_performanceSample.Label)} reason={ProbeToken(route.Failure)}";
            }
        }
        FinishRendererDiagnostics(ref exitCode, ref report);
        if (report is not null) GD.Print(report);
        if (!IsInsideTree()) return;
        _main.QueueFree();
        GetTree().Quit(exitCode);
    }

    private void RecordStartupPerformanceGuard(double delta)
    {
        var now = Time.GetTicksUsec();
        var frameMilliseconds = (now - _startupGuardLastTicks) / 1000.0;
        _startupGuardLastTicks = now;
        _startupGuardElapsed += delta;

        if (_startupGuardWarmupFrames > 0)
        {
            _startupGuardWarmupFrames--;
            return;
        }

        _startupGuardSamples.Add(frameMilliseconds);
        if (_startupGuardSamples.Count < 4 && _startupGuardElapsed < 6.0)
        {
            return;
        }

        var ordered = _startupGuardSamples.OrderBy(value => value).ToArray();
        var average = _startupGuardSamples.Average();
        var median = ordered[ordered.Length / 2];
        _startupPerformanceGuard = false;

        // A single shader-compilation hitch must not downgrade the demo. The
        // median gate requires several consistently slow frames; it only acts
        // when the first-person presentation is effectively unusable.
        if (average <= 120.0 || median <= 80.0)
        {
            return;
        }

        var player = _player;
        if (player?.ActivateLowPerformanceFallback() == true)
        {
            GD.Print(string.Join(' ',
                "act1-demo-performance-rescue:",
                "preset=low",
                $"avg={average.ToString("F1", CultureInfo.InvariantCulture)}ms",
                $"median={median.ToString("F1", CultureInfo.InvariantCulture)}ms",
                "reason=slow-startup"));
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_endingScreen is not null && inputEvent.IsActionPressed("ui_cancel"))
        {
            ReturnFromEnding();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_prologueForestActive)
        {
            return;
        }

        if (_introScreen is null || inputEvent is InputEventKey { Echo: true })
        {
            return;
        }

        if (inputEvent.IsActionPressed("interact")
            || inputEvent.IsActionPressed("ui_accept")
            || inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            GD.Print($"act1-intro: confirmed with {inputEvent.AsText()}");
            DismissIntro();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildPauseMenu()
    {
        _pauseMenu = new PauseMenuUi { Name = "Act1PauseMenu" };
        _pauseMenu.CanOpenPause = () =>
            !MainMenuVisible
            && !IntroVisible
            && !DemoEnded;
        _pauseMenu.ShowMainMenuRequested += () =>
        {
            if (_mainMenu is null)
            {
                BuildMainMenu();
            }
        };
        AddChild(_pauseMenu);
    }

    private const string LoadPlacementFailureStatus = "Не удалось найти свободное место для игрока. Игра приостановлена. Можно повторить загрузку или начать новую игру; файлы сохранений оставлены без изменений.";

    internal void ShowLoadPlacementFailure()
    {
        if (_pauseMenu?.IsOpen == true) _pauseMenu.Resume();
        if (!MainMenuVisible) BuildMainMenu();
        if (GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true } settings)
            settings.Close();
        UiFoley.StopWorld(GetTree());
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(true);
        _mainMenu?.ShowStatus(LoadPlacementFailureStatus);
    }

    private void RefreshGameplayAudioPauseState()
    {
        var paused = MainMenuVisible || IntroVisible || _pauseMenu?.IsOpen == true
            || GetTree().GetFirstNodeInGroup("settings_ui") is SettingsUi { IsOpen: true }
            || _bridge?.NeedsPhysicalRecovery == true;
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.SetPaused(paused);
    }

    private void BuildMainMenu()
    {
        (GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge)?.CancelRinatPresentation();
        var player = _player;
        // UIUX-001: gameplay input stays gated behind the menu choice.
        player?.SetModalOpen(true);

        _mainMenu = new MainMenuUi { Name = "Act1MainMenu" };
        _mainMenu.NewGameRequested += () => _ = OnMenuStartSessionAsync(startNewGame: true);
        _mainMenu.ContinueRequested += () => _ = OnMenuStartSessionAsync(startNewGame: false);
        _mainMenu.ReplayIntroRequested += () => _ = OnMenuReplayIntroAsync();
        _mainMenu.DebugZoneRequested += (zoneId, spawnPointId) => _ = OnMenuStartDebugZoneAsync(zoneId, spawnPointId);
        _mainMenu.SettingsRequested += () =>
        {
            if (_main.GetNodeOrNull<SettingsUi>("SettingsUi") is { } settings && player is not null)
            {
                settings.Open(player);
            }
        };
        _mainMenu.QuitRequested += () => GetTree().Quit();
        AddChild(_mainMenu);
        if (_mainMenu.ReplayIntroButton is { } replay) replay.Visible = !_playerSessionSelected;

        // Initial lookup begins when the bridge attaches. Do not allow a
        // fresh start before we know whether the confirmation is needed.
        if (_mainMenu.NewGameButton is { } newGame) newGame.Disabled = true;
        if (_bridge is not null) CallDeferred(nameof(RefreshMenuContinueAvailability));
        RecordM10Timing("main-menu", force: true);
    }

    private async void RefreshMenuContinueAvailability()
    {
        if (_mainMenu is { IsDismissed: false } menu)
        {
            var bridge = _bridge;
            var candidate = bridge is null ? null : await bridge.FindContinueAsync();
            var existingSavePresent = bridge is not null
                && (bridge.IsPlayerSlotAvailable(MainMenuUi.ContinueSlot)
                    || bridge.IsPlayerSlotAvailable(RuntimeBridge.CheckpointSlot));
            if (IsInstanceValid(menu) && !menu.IsDismissed)
            {
                menu.SetContinueAvailable(candidate is not null, candidate?.Description, existingSavePresent,
                    bridge?.HasIncompatiblePhotoWorldSaves == true);
                if (bridge?.NeedsPhysicalRecovery == true) menu.ShowStatus(LoadPlacementFailureStatus);
                if (menu.NewGameButton is { } newGame) { newGame.Disabled = false; newGame.GrabFocus(); }
            }
        }
    }

    private async Task OnMenuStartSessionAsync(bool startNewGame)
    {
        if (_menuBusy || _mainMenu is null || !MainMenuVisible)
        {
            return;
        }

        var bridge = _bridge ?? GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            return;
        }

        _menuBusy = true;
        try
        {
            var candidate = startNewGame ? null : await bridge.FindContinueAsync();
            var success = startNewGame
                ? await bridge.StartNewGameAsync()
                : candidate is { } save && await bridge.LoadPlayerSlotAsync(save.Slot);
            if (!success)
            {
                _mainMenu?.ShowStatus(bridge.NeedsPhysicalRecovery
                    ? LoadPlacementFailureStatus
                    : "Не удалось загрузить сеанс. Сохранения оставлены без изменений.");
                return;
            }
            _playerSessionSelected = true;
            _introReplay = false;
            _endingShown = false;
            _endingPending = false;
            _endingDelay = 0;
            if (startNewGame) await ShowIntroAfterMenuAsync();
            else
            {
                _mainMenu?.Dismiss();
                _mainMenu = null;
                _player?.SetModalOpen(false);
                RefreshGameplayAudioPauseState();
                EvaluateEndingState();
            }
            RecordM10Timing(startNewGame ? "new-game" : "continue", force: true);
        }
        finally { _menuBusy = false; }
    }

    private async Task OnMenuReplayIntroAsync()
    {
        if (_menuBusy || _playerSessionSelected || !MainMenuVisible || _bridge is not { } bridge) return;
        _menuBusy = true;
        try
        {
            // A separate debug session supplies the arrival world without changing player saves.
            if (!await bridge.StartDebugSessionAsync())
            {
                _mainMenu?.ShowStatus("Не удалось показать вступление.");
                return;
            }
            _introReplay = true;
            await ShowIntroAfterMenuAsync();
        }
        finally { _menuBusy = false; }
    }

    /// <summary>
    /// Debug zone jump from the main menu: start a fresh session, then place the
    /// player in the chosen authored zone. The review aid exists so a place can be
    /// inspected without walking the route; it grants no progression and is only
    /// reachable while user://debug-zones.enabled exists.
    /// </summary>
    private async Task OnMenuStartDebugZoneAsync(string zoneId, string spawnPointId)
    {
        if (_menuBusy || _mainMenu is null || !MainMenuVisible)
        {
            return;
        }

        if (!await StartDebugZoneAsync(zoneId, spawnPointId))
        {
            _mainMenu?.ShowStatus("Отладочный переход: не удалось начать сеанс.");
        }
    }

    /// <summary>A "Play from here" request from URMAN Studio, passed on the command line.</summary>
    internal sealed record StudioPlayRequest(string ZoneId, string SpawnPointId, Vector3? Position, float Yaw, string Label)
    {
        public const string PlayPrefix = "--urman-studio-play=";
        public const string AtPrefix = "--urman-studio-at=";
        public const string LabelPrefix = "--urman-studio-label=";

        public static StudioPlayRequest? Parse(IReadOnlyList<string> arguments)
        {
            var play = arguments.FirstOrDefault(argument => argument.StartsWith(PlayPrefix, StringComparison.Ordinal));
            if (play is null)
            {
                return null;
            }

            // Studio runs only inside the userdata guard; a run outside it would
            // write test state into the player's saves (spec PLAY07).
            if (System.Environment.GetEnvironmentVariable("URMAN_PROTECTED_RUN") != "1")
            {
                GD.PushError("URMAN Studio play request refused: not running under eng/protected_run.py.");
                return null;
            }

            var target = play[PlayPrefix.Length..].Split('@');
            Vector3? position = null;
            var yaw = 0f;
            if (arguments.FirstOrDefault(argument => argument.StartsWith(AtPrefix, StringComparison.Ordinal)) is { } at)
            {
                var parts = at[AtPrefix.Length..].Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                position = new Vector3(parts[0], parts[1], parts[2]);
                yaw = parts.Length > 3 ? parts[3] : 0f;
            }

            var label = arguments.FirstOrDefault(argument => argument.StartsWith(LabelPrefix, StringComparison.Ordinal))?[LabelPrefix.Length..] ?? "";
            return new(target[0], target.Length > 1 ? target[1] : "arrival", position, yaw, label);
        }
    }

    private async Task StartStudioPlayAsync(StudioPlayRequest request)
    {
        for (var frame = 0; frame < 600 && (_bridge is null || !MainMenuVisible); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!await StartDebugZoneAsync(request.ZoneId, request.SpawnPointId, request.Position, request.Yaw))
        {
            GD.PushError($"URMAN Studio play request failed for {request.ZoneId}@{request.SpawnPointId}.");
            return;
        }

        // The run always says what it is: a Studio test with its own saves,
        // started at an arbitrary point rather than reached by playing (PLAY03).
        var layer = new CanvasLayer { Name = "StudioPlayBanner", Layer = 90 };
        AddChild(layer);
        var banner = new Label
        {
            Text = $"Пробный запуск URMAN Studio · {request.Label} · старт в выбранной точке · отдельные сохранения",
            Position = new Vector2(12, 8),
            Modulate = new Color(1, 1, 1, .78f)
        };
        banner.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(banner);
        GD.Print($"urman-studio-play: started {request.ZoneId}@{request.SpawnPointId} label={request.Label}");
    }

    /// <summary>
    /// URMAN Studio entry: the same isolated debug session as the menu's zone
    /// jump, optionally placed at an exact authored point. Used by Studio's own
    /// world preview and by "Play from here" (<c>--urman-studio-play</c>); it
    /// grants no progression and saves only into the debug session.
    /// </summary>
    public async Task<bool> StartDebugZoneAsync(string zoneId, string spawnPointId, Vector3? position = null, float yawDegrees = 0f)
    {
        var bridge = _bridge ?? GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null || _menuBusy)
        {
            return false;
        }

        // Refuse before a session exists. A destination the connected world
        // cannot place used to start a debug run, leave the player where they
        // were, and still record the requested zone in the bridge and the save.
        if (position is null && _main.ConnectedWorld is { } world
            && !world.TryGetWorldSpawn(zoneId, spawnPointId, out _))
        {
            GD.PushError($"Debug zone jump refused: the Act I world has no spawn '{zoneId}@{spawnPointId}'.");
            _mainMenu?.ShowStatus("Отладочный переход: такой точки в мире нет.");
            return false;
        }

        _menuBusy = true;
        var started = false;
        try
        {
            if (!await bridge.StartDebugSessionAsync())
            {
                return false;
            }

            _endingShown = false;
            _endingPending = false;
            _endingDelay = 0;
            _playerSessionSelected = true;
            _mainMenu?.Dismiss();
            _mainMenu = null;
            _player?.SetModalOpen(false);
            RefreshGameplayAudioPauseState();
            _main.SwitchZone(zoneId, spawnPointId);
            bridge.CurrentZoneId = zoneId;
            bridge.CurrentSpawnPointId = spawnPointId;
            if (position is { } at)
            {
                _player?.ApplyZoneSpawn(at, yawDegrees);
            }

            _player?.NotifyTraversal("Отладочный сеанс: отдельные сохранения.");
            EvaluateEndingState();
            GD.Print($"act1-debug-zone: {zoneId}@{spawnPointId}{(position is { } p ? $" at {p}" : "")}");
            started = true;
        }
        finally { _menuBusy = false; }

        // Outside the busy window: while it is open a menu press is dropped
        // without a word, so nothing may wait inside. A jump that lands in the
        // air or inside geometry looks like a working button in the menu,
        // hence the line that says where the player actually ended up.
        if (started)
        {
            for (var frame = 0; frame < 20 && _player is not null; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            if (_player is { } standing)
            {
                GD.Print($"act1-debug-zone-standing: {zoneId}@{spawnPointId} at {standing.GlobalPosition} yaw={standing.RotationDegrees.Y:0.#} on-floor={standing.IsOnFloor()} speed={standing.Velocity.Length():0.00}");
            }
        }

        return started;
    }

    private void BuildIntro()
    {
        var player = _player;
        player?.SetModalOpen(true);

        var layer = new CanvasLayer { Layer = 100, Name = "Act1DemoIntro" };
        AddChild(layer);
        var screen = FullScreenControl("IntroScreen");
        layer.AddChild(screen);
        _introScreen = screen;

        var shade = new ColorRect
        {
            Color = new Color(0.015f, 0.02f, 0.018f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StretchFullScreen(shade);
        screen.AddChild(shade);

        var stack = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 7);
        var panel = new PanelContainer { Name = "IntroPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AnchorTop = panel.AnchorBottom = 1f;
        panel.AnchorRight = 1f;
        panel.OffsetTop = -245;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.015f, 0.025f, 0.023f, 0.88f),
            ContentMarginLeft = 40, ContentMarginRight = 40,
            ContentMarginTop = 20, ContentMarginBottom = 20
        });
        screen.AddChild(panel);
        panel.AddChild(stack);
        _introStack = stack;
        Label IntroLine(string text, int size, Color color)
        {
            var label = Label(text, size, color);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return label;
        }
        stack.AddChild(IntroLine("УРМАН  /  Акт I — Возвращение", 32, new Color(0.94f, 0.84f, 0.65f)));
        stack.AddChild(IntroLine("Я снова в Кара-Урмане. Десять лет молчания.", 19, new Color(0.87f, 0.89f, 0.83f)));
        _introControls = IntroLine(string.Empty, 17, new Color(0.72f, 0.78f, 0.74f));
        stack.AddChild(_introControls);

        _introTween = CreateTween();
        // Keep the village visible while the input gate remains until confirmation.
        _introTween.TweenProperty(shade, "color", new Color(0.015f, 0.02f, 0.018f, 0.07f), 1.5f);
        UpdateIntroControls();
        ApplyAccessibilitySettings(player?.Accessibility ?? AccessibilitySettingsSnapshot.Default);
        StartIntroFlyover();
    }

    private void BuildRouteCue()
    {
        var layer = new CanvasLayer { Layer = 85, Name = "Act1DemoRouteCue" };
        AddChild(layer);

        _routeCue = Label(string.Empty, 19, new Color(0.94f, 0.89f, 0.76f));
        _routeCue.Name = "RoutePrompt";
        _routeCue.HorizontalAlignment = HorizontalAlignment.Left;
        _routeCue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _routeCue.AnchorTop = 1f;
        _routeCue.AnchorBottom = 1f;
        _routeCue.OffsetLeft = 36;
        _routeCue.OffsetTop = -112;
        _routeCue.OffsetRight = 620;
        _routeCue.OffsetBottom = -76;
        _routeCue.Modulate = new Color(1, 1, 1, 0);
        layer.AddChild(_routeCue);
        ApplyAccessibilitySettings(_player?.Accessibility
            ?? AccessibilitySettingsSnapshot.Default);
    }

    private void UpdateRouteCue()
    {
        var bridge = _bridge;
        if (_routeCue is null
            || _endingShown
            || IntroVisible
            || bridge is null
            || !GodotObject.IsInstanceValid(bridge))
        {
            HideRouteCue();
            return;
        }

        var text = (OpeningRouteCue(bridge) ?? RouteCueText(bridge))?.Replace("[J]",
            InputBindingService.ActionHint("journal", _player?.CurrentInputDevice == "gamepad"), StringComparison.Ordinal);
        if (string.Equals(text, _lastRouteCue, StringComparison.Ordinal))
        {
            return;
        }

        _lastRouteCue = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            HideRouteCue();
            return;
        }

        _routeCueTween?.Kill();
        _routeCue.Text = text;
        _routeCue.Visible = _player?.ModalOpen != true;
        _routeCue.Modulate = new Color(1, 1, 1, 0);
        _routeCueTween = CreateTween();
        var currentTween = _routeCueTween;
        currentTween.Finished += () =>
        {
            if (_routeCueTween == currentTween) _routeCueTween = null;
        };
        _routeCueTween.TweenProperty(_routeCue, "modulate", Colors.White, 0.22)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _routeCueTween.TweenInterval(RouteCueVisibleSeconds);
        _routeCueTween.TweenProperty(_routeCue, "modulate", new Color(1, 1, 1, 0), 0.42)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        if (!_routeCue.Visible) _routeCueTween.Pause();
    }

    private void HideRouteCue()
    {
        _routeCueTween?.Kill();
        _routeCueTween = null;
        _lastRouteCue = string.Empty;
        if (_routeCue is not null && GodotObject.IsInstanceValid(_routeCue))
        {
            _routeCue.Modulate = new Color(1, 1, 1, 0);
        }
    }

    // Presentation-only wording derived from the runtime scene and its
    // currently available authored interaction; this does not store route
    // progress or replace the journal's objective projection.
    private static string? RouteCueText(RuntimeBridge bridge) => bridge.ActiveSceneId switch
    {
        "urman.chapter1:scene/arrival_vehicle_dusk" => ArrivalRouteCue(bridge),
        "urman.chapter1:scene/house" => HouseRouteCue(bridge),
        "urman.chapter1:scene/crossroad_signs_inspect" =>
            AvailableCue(bridge, "route-to-fap", "На улице ищу указатель «ФАП».")
            // R059: the FAP opens after the first comparison; between Alsu's
            // versions and that comparison the cue names the notebook step.
            ?? (KnowledgeConfirmed(bridge, "clue_alsu_heard_versions")
                && !KnowledgeConfirmed(bridge, "clue_marat_versions_conflict")
                    ? $"Открыть книжку ({InputBindingService.Label("journal")}): сравнить, что слышала Алсу, со справкой о смерти."
                    : null)
            ?? AvailableCue(bridge, "talk-alsu", "Спросить Алсу, что ей говорили о Марате.")
            ?? AvailableCue(bridge, "talk-rinat", "Поговорить с Ринатом."),
        "urman.chapter1:scene/fap_waiting_room_day" =>
            AvailableCue(bridge, "fap-to-document-desk", "На столе — документы о Марате.")
            ?? AvailableCue(bridge, "talk-naila", "Поговорить с Наилей."),
        "urman.chapter1:scene/fap_pressure_document_desk" => AvailableCue(
            bridge,
            "fap-document-desk-to-official-record",
            "Прочитать амбулаторную карту Марата."),
        "urman.chapter1:scene/evidence-official-death" => AvailableCue(
            bridge,
            "official-to-internal-register",
            "Вернуться в дом бабая: сверить справку с реестром на ПК."),
        "urman.chapter1:scene/evidence-internal-register" =>
            AvailableCue(bridge, "internal-register-to-rinat", "Показать Ринату категорию из внутреннего реестра.")
            ?? AvailableCue(bridge, "internal-register-to-saved-message", "Открыть сохранённое сообщение Марата.")
            ?? (!KnowledgeConfirmed(bridge, "clue_record_wording_mismatch")
                ? "В журнале [J] сопоставить справку и внутренний реестр."
                : !KnowledgeConfirmed(bridge, "clue_naila_record_scope")
                    ? "Показать Наиле категорию из реестра в ФАПе."
                    : "В журнале [J] сверить ответ Наили с внутренним реестром."),
        "urman.chapter1:scene/evidence-saved-message" => SavedMessageRouteCue(bridge),
        "urman.chapter1:scene/evidence-tatarwiki-boundary" =>
            AvailableCue(bridge, "boundary-source-to-reread", "Перечитать статью с понятыми словами.")
            // The first reading does not unlock Timur. Its existing journal
            // comparison supplies the bounded inference and understood words.
            ?? AvailableCue(bridge, "revise-echo", "В журнале [J] проверить версию об эхе по сообщению и статье.")
            ?? AvailableCue(bridge, "revise-creature", "В журнале [J] проверить, достаточно ли одного заголовка для моей версии.")
            ?? "В журнале [J] сопоставить сообщение Марата со статьёй о границе.",
        "urman.chapter1:scene/evidence-tatarwiki-reread" => RereadRouteCue(bridge),
        "urman.chapter1:scene/evidence-edge-sketch" =>
            !KnowledgeConfirmed(bridge, "clue_route_check_intent")
                ? "В журнале [J] сопоставить схему с сообщением Марата: что я смогу проверить на дороге?"
                : !KnowledgeConfirmed(bridge, "clue_route_check_discussed")
                    ? "Обсудить с Тимуром у мечети, что можно проверить по схеме."
                    : AvailableCue(bridge, "edge-sketch-to-zirat-road", "Идти к дороге у зирата."),
        "urman.chapter1:scene/zirat-road" =>
            !KnowledgeConfirmed(bridge, "clue_rinat_at_roadside")
                ? bridge.ResolveText("urman.chapter1:text/objective-rinat-roadside")
                : !KnowledgeConfirmed(bridge, "clue_zirat_roadside_marks")
                ? AvailableCue(bridge, "zirat-roadside-clue", "Осмотреть след у зиратской дороги.")
                : !KnowledgeConfirmed(bridge, "clue_sketch_field_landmarks")
                    ? "Отойти от бирки на дорогу и сверить расположение канавы, бирки и внешней ограды."
                : !KnowledgeConfirmed(bridge, "clue_marat_last_route_near_zirat")
                    ? "В журнале [J] сопоставить схему Мансура со следом у дороги."
                    : AvailableCue(bridge, "zirat-road-to-forest", "Идти к кромке Кара-Урмана."),
        "urman.chapter1:scene/forest-approach" => AvailableCue(
            bridge,
            "forest-approach-to-forest",
            "Дальше — к кромке леса."),
        "urman.chapter1:scene/forest" => null,
        _ => null
    };

    private static string? SavedMessageRouteCue(RuntimeBridge bridge)
    {
        if (!KnowledgeConfirmed(bridge, "clue_marat_message_read"))
            return "На ПК прочитать сохранённое сообщение Марата.";
        if (!KnowledgeConfirmed(bridge, "clue_message_question_prepared"))
            return "В журнале [J] сопоставить сообщение Марата с пересказом Алсу.";
        if (!KnowledgeConfirmed(bridge, "clue_alsu_message_reply"))
            return "Показать Алсу на улице выписку из сообщения и спросить о его словах.";
        return bridge.CurrentZoneId == "house_old_pc"
            ? AvailableCue(bridge, "saved-message-to-boundary-source", "На ПК найти статью о лесной границе.")
            : "Вернуться к ПК в доме бабая и найти статью о лесной границе.";
    }

    private static string? RereadRouteCue(RuntimeBridge bridge)
    {
        if (AvailableCue(bridge, "reread-to-edge-sketch", "Проверить схему Мансура.") is { } sketch)
            return sketch;
        if (!KnowledgeConfirmed(bridge, "clue_internal_wording_reread"))
            return "В журнале [J] сверить строку реестра с заметкой о границе.";

        // The authored objectives own the two source returns and their order.
        // Showing the current lead must not invent a second progression state.
        return bridge.ActiveObjectives().FirstOrDefault(objective => objective.ObjectiveId is
            "find-owner-draft" or "question-owner-draft" or "find-damaged-fragment"
            or "compare-damaged-fragment" or "question-damaged-fragment")?.Title;
    }

    private static string? ArrivalRouteCue(RuntimeBridge bridge) =>
        AvailableCue(bridge, "arrival-enter-house", "Приехали. Войти домой — бабай и әби ждут к чаю.");

    private static string? HouseRouteCue(RuntimeBridge bridge)
    {
        if (KnowledgeConfirmed(bridge, "clue_marat_official_death_version"))
        {
            return AvailableCue(bridge, "house-to-route", "Выйти на улицу и продолжить расследование.")
                ?? AvailableCue(bridge, "talk-gulsina", "Поговорить с әби Гөлсинә перед уходом.");
        }

        if (string.Equals(
                LastInteraction(bridge),
                "urman.chapter1:interaction/oldpc-power",
                StringComparison.Ordinal))
        {
            return AvailableCue(bridge, "house-to-route", "Выйти на улицу и продолжить расследование.")
                ?? AvailableCue(bridge, "oldpc-power", "В архиве — найти справку о Марате.");
        }

        return AvailableCue(bridge, "oldpc-power", "Старый компьютер — проверить записи.")
            ?? AvailableCue(bridge, "talk-mansur", "Сначала поговорить с бабаем Мансуром.")
            ?? AvailableCue(bridge, "house-to-route", "Выйти на улицу и продолжить расследование.");
    }

    private static string? AvailableCue(RuntimeBridge bridge, string localInteractionId, string text) =>
        bridge.IsInteractionAvailable($"urman.chapter1:interaction/{localInteractionId}") ? text : null;

    private static string? LastInteraction(RuntimeBridge bridge)
    {
        try
        {
            return bridge.SelectRuntimeState().GetProperty("lastInteraction").GetString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool KnowledgeConfirmed(RuntimeBridge bridge, string localKnowledgeId)
    {
        try
        {
            var state = bridge.SelectRuntimeState();
            return state.GetProperty("knowledge")
                .GetProperty($"urman.chapter1:knowledge/{localKnowledgeId}")
                .GetProperty("status")
                .GetString() == "confirmed";
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void UpdateIntroControls()
    {
        if (_introControls is null)
        {
            return;
        }

        var gamepad = _player?.CurrentInputDevice == "gamepad";
        string Hint(string action) => InputBindingService.ActionHint(action, gamepad).Trim('[', ']');
        var directions = new[] { "move_forward", "move_left", "move_backward", "move_right" }.Select(Hint).ToArray();
        var movement = gamepad && directions.SequenceEqual(new[] { "Левый стик ↑", "Левый стик ←", "Левый стик ↓", "Левый стик →" })
            ? "Левый стик" : !gamepad && directions.SequenceEqual(new[] { "W", "A", "S", "D" })
                ? "WASD" : string.Join(" / ", directions);
        var interact = Hint("interact");
        _introControls.Text = $"{movement} — идти   ·   {(gamepad ? "правый стик" : "мышь")} — смотреть   ·   {interact} — начать / осмотреть\n"
            + $"{interact} — пропустить вступление   ·   {Hint("journal")} — журнал   ·   {Hint("pause")} — меню";
    }

    private void DismissIntro()
    {
        var screen = _introScreen;
        if (screen is null)
        {
            return;
        }

        _introTween?.Kill();
        StopIntroFlyover();
        _introTween = null;
        _introScreen = null;
        _introStack = null;
        _introControls = null;
        if (GodotObject.IsInstanceValid(screen))
        {
            (screen.GetParent() ?? screen).QueueFree();
        }

        if (_introReplay)
        {
            _introReplay = false;
            BuildMainMenu();
            RefreshGameplayAudioPauseState();
            return;
        }

        _player?.SetModalOpen(false);
        RefreshGameplayAudioPauseState();
        UpdateRouteCue();
        RecordM10Timing("intro-dismissed", force: true);
    }

    private void ShowEnding()
    {
        _endingShown = true;
        RecordM10Timing("act-ending", force: true);
        (GetTree().GetFirstNodeInGroup("ambient_audio") as AmbientAudioDirector)?.StopForEnding();
        HideRouteCue();
        var player = _player;
        player?.SetModalOpen(true);

        var layer = new CanvasLayer { Layer = 120, Name = "Act1DemoEnding" };
        AddChild(layer);
        var screen = FullScreenControl("EndingScreen");
        layer.AddChild(screen);
        _endingScreen = screen;

        var shade = new ColorRect
        {
            Color = new Color(0.008f, 0.01f, 0.009f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StretchFullScreen(shade);
        screen.AddChild(shade);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        StretchFullScreen(center);
        screen.AddChild(center);
        var stack = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 14);
        center.AddChild(stack);
        _endingStack = stack;
        var title = Label("НЕ ОТВЕЧАЙ", 58, new Color(0.88f, 0.78f, 0.59f));
        title.Modulate = new Color(1, 1, 1, 0);
        stack.AddChild(title);
        var caption = Label("Конец Акта I", 20, new Color(0.65f, 0.68f, 0.64f));
        caption.Modulate = new Color(1, 1, 1, 0);
        stack.AddChild(caption);
        _endingTitle = title;
        _endingCaption = caption;
        var returnButton = new Button { Name = "ReturnToMenu", Text = "В главное меню", CustomMinimumSize = new Vector2(280, 48) };
        stack.AddChild(returnButton);
        returnButton.Pressed += ReturnFromEnding;
        returnButton.GrabFocus();

        var tween = screen.CreateTween().SetParallel(true);
        tween.TweenProperty(shade, "color", new Color(0.008f, 0.01f, 0.009f, 1f), 1.1f);
        tween.TweenProperty(title, "modulate", new Color(1, 1, 1, 1), 0.65f).SetDelay(0.85f);
        tween.TweenProperty(caption, "modulate", new Color(1, 1, 1, 1), 0.65f).SetDelay(1.1f);
        ApplyAccessibilitySettings(player?.Accessibility ?? AccessibilitySettingsSnapshot.Default);
    }

    private void ReturnFromEnding()
    {
        if (_endingScreen is not null && IsInstanceValid(_endingScreen)) _endingScreen.GetParent().QueueFree();
        _endingScreen = null;
        _endingStack = null;
        _endingTitle = null;
        _endingCaption = null;
        _endingPending = false;
        // Keep the completed card and any source-positioned foley suppressed while the main menu is open.
        UiFoley.StopWorld(GetTree());
        (GetTree().GetFirstNodeInGroup("audio_cue_ui") as AudioCueUi)?.ResetPresentation();
        BuildMainMenu();
    }

    private static Control FullScreenControl(string name) => new Control
    {
        Name = name,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        AnchorRight = 1f,
        AnchorBottom = 1f,
        GrowHorizontal = Control.GrowDirection.Both,
        GrowVertical = Control.GrowDirection.Both
    };

    private static void StretchFullScreen(Control control)
    {
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.GrowHorizontal = Control.GrowDirection.Both;
        control.GrowVertical = Control.GrowDirection.Both;
    }

    private static Label Label(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
