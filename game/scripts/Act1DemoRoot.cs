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
    private bool _performanceProbe;
    private int _performanceWarmupFrames;
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
        BuildMainMenu();
        BuildRouteCue();
        CallDeferred(nameof(AttachRuntimeBridge));

        var commandLine = OS.GetCmdlineArgs();
        _performanceProbe = commandLine.Contains("--urman-perf-probe", StringComparer.Ordinal);
        _startupPerformanceGuard = !_performanceProbe
            && !commandLine.Contains("--no-auto-performance-fallback", StringComparer.Ordinal);
        if (_performanceProbe)
        {
            _performanceWarmupFrames = 20;
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
        var now = Time.GetTicksUsec();
        var frameMilliseconds = (now - _performanceLastTicks) / 1000.0;
        _performanceLastTicks = now;
        if (_performanceWarmupFrames > 0)
        {
            _performanceWarmupSamples.Add(frameMilliseconds);
            _performanceWarmupFrames--;
            return;
        }

        _performanceSamples.Add(frameMilliseconds);
        if (_performanceSamples.Count < 60)
        {
            return;
        }

        var sorted = _performanceSamples.OrderBy(value => value).ToArray();
        var average = _performanceSamples.Average();
        var p95 = sorted[(int)Math.Floor((sorted.Length - 1) * 0.95)];
        var maximum = _performanceSamples.Max();
        var fps = 1000.0 / Math.Max(average, 0.001);
        var warmupAverage = _performanceWarmupSamples.Count == 0
            ? 0
            : _performanceWarmupSamples.Average();
        var warmupMaximum = _performanceWarmupSamples.Count == 0
            ? 0
            : _performanceWarmupSamples.Max();
        var player = _player;
        var renderer = RenderingServer.GetRenderingDevice() is null
            ? "unavailable"
            : "Godot RenderingDevice";

        GD.Print(string.Join(' ',
            "act1-demo-package-performance:",
            $"avg={average.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"p95={p95.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"max={maximum.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"warmup_avg={warmupAverage.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"warmup_max={warmupMaximum.ToString("F3", CultureInfo.InvariantCulture)}ms",
            $"fps={fps.ToString("F2", CultureInfo.InvariantCulture)}",
            $"renderer={renderer}",
            $"preset={player?.GraphicsPreset ?? "unknown"}",
            $"scale={GetViewport().Scaling3DScale.ToString("F2", CultureInfo.InvariantCulture)}",
            $"msaa={GetViewport().Msaa3D}"));

        _performanceProbe = false;
        _main.QueueFree();
        GetTree().Quit(fps >= 10.0 ? 0 : 1);
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
            menu.SetContinueAvailable(_bridge?.HasLoadableSlot(MainMenuUi.ContinueSlot) ?? false);
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
            if (!bridge.HasLoadableSlot(MainMenuUi.ContinueSlot))
            {
                return;
            }

            await bridge.LoadSlotAsync(MainMenuUi.ContinueSlot);
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
        stack.AddChild(Label("Сначала — домой, к бабаю и әби. Потом — понять, почему Марат перестал отвечать.", 17, new Color(0.68f, 0.70f, 0.64f)));
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
            "Сверить официальную запись с внутренним реестром."),
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
            ? "Левый стик — идти   ·   правый стик — смотреть   ·   A — начать / осмотреть   ·   Y — журнал   ·   Start — меню"
            : "WASD — идти   ·   мышь — смотреть   ·   E — начать / осмотреть   ·   J — журнал   ·   Esc — меню";
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
