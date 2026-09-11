using System.Globalization;
using Godot;
using Urman.Core.Persistence;

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
    private RuntimeBridge? _bridge;
    private AudioStreamPlayer? _doorFoley;
    private string? _lastHeardZone;
    private bool _performanceProbe;
    private const string PerformanceProbeModeMenu = "menu";
    private const string PerformanceProbeModeGameplay = "gameplay";
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
    private bool _performanceWindowed;
    private bool _performanceRealRenderer;
    private double _performanceWarmupSeconds = DefaultPerformanceWarmupSeconds;
    private double _performanceDurationSeconds = DefaultPerformanceDurationSeconds;
    private double _performanceWarmupElapsed;
    private double _performanceMeasurementElapsed;
    private ulong _performanceLastTicks;
    private readonly List<double> _performanceWarmupSamples = [];
    private readonly List<double> _performanceSamples = [];
    private bool _startupPerformanceGuard;
    private int _startupGuardWarmupFrames;
    private ulong _startupGuardLastTicks;
    private double _startupGuardElapsed;
    private readonly List<double> _startupGuardSamples = [];

    public Main DemoMain => _main;

    public bool DemoEnded => _endingShown;

    public bool EndingPending => _endingPending;

    public double EndingDelaySeconds => _endingDelay;

    public bool IntroVisible => _introScreen is not null;

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
        AddToGroup(AccessibilityPresentation.TargetGroup);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn")
            ?? throw new InvalidOperationException("Act 1 demo could not load the first-person main scene.");
        _main = packed.Instantiate<Main>()
            ?? throw new InvalidOperationException("Act 1 demo main scene did not instantiate Main.");
        _main.InitialZoneId = "village_day";
        _main.InitialSpawnPointId = "arrival";
        _main.EnableZoneTransitionFade = true;
        _main.EnableAct1ConnectedWorld = true;
        AddChild(_main);
        _player = _main.GetNodeOrNull<FirstPersonController>("Player");
        _doorFoley = UiFoley.Attach(this);
        BuildMainMenu();
        BuildPauseMenu();
        BuildRouteCue();
        CallDeferred(nameof(AttachRuntimeBridge));

        var commandLine = OS.GetCmdlineArgs();
        _performanceProbe = commandLine.Contains("--urman-perf-probe", StringComparer.Ordinal)
            || commandLine.Any(argument => argument.StartsWith(
                "--urman-perf-probe-mode=",
                StringComparison.Ordinal));
        _startupPerformanceGuard = !_performanceProbe
            && !commandLine.Contains("--no-auto-performance-fallback", StringComparer.Ordinal);
        if (_performanceProbe)
        {
            ConfigurePerformanceProbe(commandLine);
            _performanceLastTicks = Time.GetTicksUsec();
        }

        if (_startupPerformanceGuard)
        {
            _startupGuardWarmupFrames = 3;
            _startupGuardLastTicks = Time.GetTicksUsec();
        }
    }

    public override void _Process(double delta)
    {
        // AUDIO-010: a door creak when the player enters the house through
        // the front door (presentation-only, silent in headless runs).
        if (_bridge is not null && !string.IsNullOrWhiteSpace(_bridge.CurrentZoneId))
        {
            if (_lastHeardZone != _bridge.CurrentZoneId)
            {
                if (_lastHeardZone is not null && _bridge.CurrentZoneId == "house_old_pc")
                {
                    UiFoley.Play(_doorFoley, "door_creak");
                }

                _lastHeardZone = _bridge.CurrentZoneId;
            }
        }

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
        if (_player?.ModalOpen == true && _routeCue is not null && _routeCue.Modulate.A > 0f)
        {
            HideRouteCue();
        }

        if (_endingShown || !_endingPending)
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
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
        {
            _bridge.RuntimeStateChanged -= OnRuntimeStateChanged;
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
            EvaluateEndingState();
            return;
        }

        CallDeferred(nameof(AttachRuntimeBridge));
    }

    private void OnRuntimeStateChanged()
    {
        EvaluateEndingState();
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

        if (_performanceWarmupElapsed < _performanceWarmupSeconds)
        {
            _performanceWarmupSamples.Add(frameMilliseconds);
            _performanceWarmupElapsed += frameMilliseconds / 1000.0;
            return;
        }

        _performanceSamples.Add(frameMilliseconds);
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
        var camera = player?.GetNodeOrNull<Camera3D>("Head/Camera3D");
        var performancePass = _performanceWindowed
            && _performanceRealRenderer
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
                : performancePass ? "PASS" : "FAIL";

        GD.Print(string.Join(' ',
            "act1-demo-package-performance:",
            $"status={status}",
            $"mode={_performanceProbeMode}",
            "sample=village_day@arrival",
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
            $"fps={(_performanceWindowed && _performanceRealRenderer ? fps.ToString("F2", CultureInfo.InvariantCulture) : "n/a")}",
            $"window_fps_valid={(_performanceWindowed && _performanceRealRenderer).ToString().ToLowerInvariant()}",
            $"display_driver={ProbeToken(DisplayServer.GetName())}",
            $"adapter={ProbeToken(RenderingServer.GetVideoAdapterName())}",
            $"rendering_method={ProbeToken(renderingMethod)}",
            $"preset={player?.GraphicsPreset ?? "unknown"}",
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
            $"primitives={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame).ToString("F0", CultureInfo.InvariantCulture)}"));

        var exitCode = !string.Equals(_performanceProbeMode, PerformanceProbeModeGameplay, StringComparison.Ordinal)
            || shortProbe
            ? 2
            : performancePass ? 0 : 1;
        FinishPerformanceProbe(exitCode);
    }

    private void ConfigurePerformanceProbe(IReadOnlyList<string> commandLine)
    {
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
        }
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

    private void FinishPerformanceProbe(int exitCode)
    {
        _performanceProbe = false;
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
        if (_introScreen is null)
        {
            return;
        }

        if (inputEvent.IsActionPressed("interact")
            || inputEvent.IsActionPressed("ui_accept")
            || inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
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

    private void BuildMainMenu()
    {
        var player = _player;
        // UIUX-001: gameplay input stays gated behind the menu choice.
        player?.SetModalOpen(true);

        _mainMenu = new MainMenuUi { Name = "Act1MainMenu" };
        _mainMenu.NewGameRequested += () => _ = OnMenuStartSessionAsync(startNewGame: true);
        _mainMenu.ContinueRequested += () => _ = OnMenuStartSessionAsync(startNewGame: false);
        _mainMenu.SettingsRequested += () =>
        {
            if (_main.GetNodeOrNull<SettingsUi>("SettingsUi") is { } settings && player is not null)
            {
                settings.Open(player);
            }
        };
        _mainMenu.QuitRequested += () => GetTree().Quit();
        AddChild(_mainMenu);

        // Continue availability needs the bridge, which lives inside the
        // already-added main scene; refresh once the deferred attach runs.
        CallDeferred(nameof(RefreshMenuContinueAvailability));
    }

    private void RefreshMenuContinueAvailability()
    {
        if (_mainMenu is { IsDismissed: false } menu)
        {
            var bridge = _bridge;
            menu.SetContinueAvailable(
                bridge is not null
                && (bridge.HasLoadableSlot(MainMenuUi.ContinueSlot)
                    || bridge.HasLoadableSlot(MainMenuUi.CheckpointSlot)));
        }
    }

    private async Task OnMenuStartSessionAsync(bool startNewGame)
    {
        if (_mainMenu is null || !MainMenuVisible)
        {
            return;
        }

        var bridge = _bridge ?? GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (bridge is null)
        {
            return;
        }

        if (startNewGame)
        {
            await bridge.StartNewGameAsync();
        }
        else
        {
            // SAVE-004 (verified green): Continue prefers the quick slot and
            // falls back to the rolling checkpoint written after stable beats.
            var slot = bridge.HasLoadableSlot(MainMenuUi.ContinueSlot)
                ? MainMenuUi.ContinueSlot
                : bridge.HasLoadableSlot(MainMenuUi.CheckpointSlot)
                    ? MainMenuUi.CheckpointSlot
                    : null;
            if (slot is null)
            {
                return;
            }

            await bridge.LoadSlotAsync(slot);
        }

        ShowIntroAfterMenu();
    }

    private void ShowIntroAfterMenu()
    {
        if (!MainMenuVisible)
        {
            return;
        }

        _mainMenu?.Dismiss();
        _mainMenu = null;
        BuildIntro();
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
            Color = new Color(0.015f, 0.02f, 0.018f, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StretchFullScreen(shade);
        screen.AddChild(shade);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        StretchFullScreen(center);
        screen.AddChild(center);
        var stack = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 12);
        center.AddChild(stack);
        _introStack = stack;
        stack.AddChild(Label("УРМАН", 56, new Color(0.88f, 0.78f, 0.59f)));
        stack.AddChild(Label("Акт I — Возвращение", 24, new Color(0.72f, 0.72f, 0.66f)));
        stack.AddChild(Label("Я снова в Кырлае. Дождь. Десять лет молчания.", 18, new Color(0.57f, 0.62f, 0.59f)));
        stack.AddChild(Label("Первая цель: добраться до дома и проверить старый компьютер.", 18, new Color(0.72f, 0.74f, 0.68f)));
        _introControls = Label(string.Empty, 15, new Color(0.48f, 0.54f, 0.52f));
        stack.AddChild(_introControls);

        _introTween = CreateTween();
        // The first-time card must remain available until the player confirms
        // they are ready. Fade the backing shade to a readable resting alpha,
        // but never dismiss the modal or unlock movement on a timer.
        _introTween.TweenProperty(shade, "color", new Color(0.015f, 0.02f, 0.018f, 0.32f), 1.8f).SetDelay(1.4f);
        UpdateIntroControls();
        ApplyAccessibilitySettings(player?.Accessibility ?? AccessibilitySettingsSnapshot.Default);
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
            || _introScreen is not null
            || bridge is null
            || !GodotObject.IsInstanceValid(bridge))
        {
            HideRouteCue();
            return;
        }

        var text = RouteCueText(bridge);
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
        _routeCue.Modulate = new Color(1, 1, 1, 0);
        _routeCueTween = CreateTween();
        _routeCueTween.TweenProperty(_routeCue, "modulate", Colors.White, 0.22)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _routeCueTween.TweenInterval(RouteCueVisibleSeconds);
        _routeCueTween.TweenProperty(_routeCue, "modulate", new Color(1, 1, 1, 0), 0.42)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
    }

    private void HideRouteCue()
    {
        _routeCueTween?.Kill();
        _routeCueTween = null;
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
        "urman.chapter1:scene/arrival_vehicle_dusk" => AvailableCue(
            bridge,
            "arrival-enter-house",
            "Дом впереди — войти и осмотреться."),
        "urman.chapter1:scene/house" => HouseRouteCue(bridge),
        "urman.chapter1:scene/crossroad_signs_inspect" =>
            AvailableCue(bridge, "route-to-fap", "На улице ищу указатель «ФАП».")
            ?? AvailableCue(bridge, "talk-alsu", "Поговорить с Алсу у дороги.")
            ?? AvailableCue(bridge, "talk-rinat", "Поговорить с Ринатом."),
        "urman.chapter1:scene/fap_waiting_room_day" =>
            AvailableCue(bridge, "fap-to-document-desk", "На столе — документы о Марате.")
            ?? AvailableCue(bridge, "talk-naila", "Поговорить с Наилей."),
        "urman.chapter1:scene/fap_pressure_document_desk" => AvailableCue(
            bridge,
            "fap-document-desk-to-official-record",
            "Прочитать официальную справку о Марате."),
        "urman.chapter1:scene/evidence-official-death" => AvailableCue(
            bridge,
            "official-to-internal-register",
            "Вернуться в дом бабая: сверить справку с реестром на ПК."),
        "urman.chapter1:scene/evidence-internal-register" =>
            AvailableCue(bridge, "internal-register-to-rinat", "Спросить Рината о внутреннем реестре.")
            ?? AvailableCue(bridge, "internal-register-to-saved-message", "Открыть сохранённое сообщение Марата."),
        "urman.chapter1:scene/evidence-saved-message" => AvailableCue(
            bridge,
            "saved-message-to-boundary-source",
            "Найти статью о лесной границе."),
        "urman.chapter1:scene/evidence-tatarwiki-boundary" => AvailableCue(
            bridge,
            "boundary-source-to-reread",
            "Перечитать статью с понятыми словами."),
        "urman.chapter1:scene/evidence-tatarwiki-reread" => AvailableCue(
            bridge,
            "reread-to-edge-sketch",
            "Сопоставить статью с рисунком Марата."),
        "urman.chapter1:scene/evidence-edge-sketch" => AvailableCue(
            bridge,
            "edge-sketch-to-zirat-road",
            "Идти к дороге у зирата."),
        "urman.chapter1:scene/zirat-road" =>
            !KnowledgeConfirmed(bridge, "clue_marat_last_route_near_zirat")
                ? AvailableCue(bridge, "zirat-roadside-clue", "Осмотреть след у зиратской дороги.")
                ?? AvailableCue(bridge, "zirat-road-to-forest", "Идти к кромке Кара-Урмана.")
                : AvailableCue(bridge, "zirat-road-to-forest", "Идти к кромке Кара-Урмана."),
        "urman.chapter1:scene/forest" => null,
        _ => null
    };

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

        var player = _player;
        _introControls.Text = player?.CurrentInputDevice == "gamepad"
            ? "Левый стик — идти   ·   правый стик — смотреть   ·   A — начать / осмотреть\nY — журнал   ·   Start — меню   ·   Нажмите A, чтобы продолжить"
            : "WASD — идти   ·   мышь — смотреть   ·   E — начать / осмотреть\nJ — журнал   ·   Esc — меню   ·   Нажмите E или левую кнопку мыши, чтобы продолжить";
    }

    private void DismissIntro()
    {
        var screen = _introScreen;
        if (screen is null)
        {
            return;
        }

        _introTween?.Kill();
        _introTween = null;
        _introScreen = null;
        _introStack = null;
        _introControls = null;
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.QueueFree();
        }

        _player?.SetModalOpen(false);
        UpdateRouteCue();
    }

    private void ShowEnding()
    {
        _endingShown = true;
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
        var caption = Label("Конец демо", 20, new Color(0.65f, 0.68f, 0.64f));
        caption.Modulate = new Color(1, 1, 1, 0);
        stack.AddChild(caption);
        _endingTitle = title;
        _endingCaption = caption;

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(shade, "color", new Color(0.008f, 0.01f, 0.009f, 1f), 1.1f);
        tween.TweenProperty(title, "modulate", new Color(1, 1, 1, 1), 0.65f).SetDelay(0.85f);
        tween.TweenProperty(caption, "modulate", new Color(1, 1, 1, 1), 0.65f).SetDelay(1.1f);
        ApplyAccessibilitySettings(player?.Accessibility ?? AccessibilitySettingsSnapshot.Default);
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
