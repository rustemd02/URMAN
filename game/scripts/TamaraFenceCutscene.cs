using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

/// <summary>
/// A short directed replay of the accident: the guy with the phone turns the
/// player's crash into a village video while Tamara Gennadievna treats it as a
/// household problem. Presentation only — the crash, the quest gate and the
/// broken fence were already committed before this scene starts, and a skip,
/// load or error restores the controls and leaves people at their rest spots.
/// </summary>
public partial class TamaraFenceCutscene : Node
{
    private readonly TamaraFenceQuest _quest;
    private readonly RuntimeBridge _bridge;
    private readonly object _session;
    private readonly FirstPersonController _player;
    private readonly VehicleController? _car;
    private readonly Vector3 _impact;

    private Camera3D _camera = null!;
    private CaptionStrip _captions = null!;
    private Label _skipHint = null!;
    private int _generation;
    private bool _skipRequested;
    private double _speed = 1.0;
    private Vector3 _camFrom;
    private Vector3 _camTo;
    private Vector3 _lookFrom;
    private Vector3 _lookTo;
    private float _fovFrom;
    private float _fovTo;
    private double _shotElapsed;
    private double _shotLength;
    private bool _handheld;

    public bool SkipRequested => _skipRequested;

    /// <summary>Test/capture hook: fires when a subtitle line goes up.</summary>
    internal event Action<string>? LineShown;

    /// <summary>Test/capture hook: fires at every cut, with the shot's tag.</summary>
    internal event Action<string>? ShotShown;

    internal void SetLineListenerForTest(Action<string> listener) => LineShown += listener;

    internal void SetShotListenerForTest(Action<string> listener) => ShotShown += listener;

    public TamaraFenceCutscene(TamaraFenceQuest quest, RuntimeBridge bridge, object session,
        FirstPersonController player, VehicleController? car, Vector3 impact)
    {
        _quest = quest;
        _bridge = bridge;
        _session = session;
        _player = player;
        _car = car;
        _impact = impact;
    }

    public override void _Ready()
    {
        _camera = new Camera3D { Name = "TamaraFenceCutsceneCamera", Fov = 55f, Near = .05f, Current = false };
        AddChild(_camera);
        _captions = CaptionStrip.Ensure(GetTree());
        var canvas = new CanvasLayer { Name = "SkipHintLayer", Layer = 16 };
        AddChild(canvas);
        _skipHint = new Label
        {
            Name = "SkipHint",
            Text = _bridge.ResolveText("urman.chapter1:text/tamara-cutscene-skip-hint"),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _skipHint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _skipHint.OffsetLeft = -240;
        _skipHint.OffsetRight = -28;
        _skipHint.OffsetTop = -46;
        _skipHint.OffsetBottom = -20;
        _skipHint.AddThemeFontSizeOverride("font_size", 15);
        _skipHint.AddThemeColorOverride("font_color", new Color(.8f, .76f, .68f, .85f));
        _skipHint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _skipHint.AddThemeConstantOverride("outline_size", 3);
        _skipHint.HorizontalAlignment = HorizontalAlignment.Right;
        canvas.AddChild(_skipHint);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("ui_cancel") || inputEvent is InputEventKey
            {
                Pressed: true, Echo: false, PhysicalKeycode: Key.Space
            })
        {
            _skipRequested = true;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Test hook: the same scene at an accelerated clock.</summary>
    internal void AccelerateForTest(double speed) => _speed = speed;

    internal void RequestSkip() => _skipRequested = true;

    public async Task<bool> RunAsync()
    {
        _generation++;
        var playerWasInCar = _player.VehicleControlled;
        _player.SetModalOpen(true);
        try
        {
            await Script();
            return !_skipRequested && StillCurrent();
        }
        finally
        {
            _skipHint.GetParent().QueueFree();
            if (StillCurrent())
            {
                _player.SetModalOpen(false);
                if (playerWasInCar && _car is { } car && GodotObject.IsInstanceValid(car))
                {
                    car.VehicleCamera.MakeCurrent();
                }
                else if (_player.GetNodeOrNull<Camera3D>("Head/Camera3D") is { } headCamera)
                {
                    headCamera.MakeCurrent();
                }
            }
        }
    }

    private bool StillCurrent() => IsInsideTree() && !IsQueuedForDeletion()
        && ReferenceEquals(_session, _bridge.SessionIdentity)
        && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree();

    // ---- script ---------------------------------------------------------------

    /// <summary>The scene's direction is authored data (URMAN Studio, spec CINE01), played action by action.</summary>
    public const string ScriptPath = "res://content/cutscenes/tamara_fence.cutscene.v1.json";

    /// <summary>Studio preview / tests: play edited direction instead of the saved file.</summary>
    internal static string? ScriptJsonOverride { get; set; }

    private async Task Script()
    {
        using var document = System.Text.Json.JsonDocument.Parse(ScriptJsonOverride ?? global::Godot.FileAccess.GetFileAsString(ScriptPath));
        var actors = new Dictionary<string, Node3D>(StringComparer.Ordinal) { ["guy"] = _quest.GuyActor, ["tamara"] = _quest.TamaraActor };
        // The wreck decides where car-anchored cameras stand: the Niva is
        // nose-in at the breach, so those shots play from beside its flank on
        // the street side, clear of the hull and the concrete pillars.
        var car = _car is { } liveCar && GodotObject.IsInstanceValid(liveCar)
            ? liveCar.GlobalPosition
            : _impact + new Vector3(-1.6f, 0f, .9f);
        var towardFence = _impact - car;
        towardFence.Y = 0f;
        if (towardFence.LengthSquared() < .01f) towardFence = Vector3.Right;
        var streetSide = -towardFence.Normalized();

        Vector3 Point(System.Text.Json.JsonElement spec)
        {
            if (spec.TryGetProperty("point", out var point))
            {
                return new Vector3(point[0].GetSingle(), point[1].GetSingle(), point[2].GetSingle());
            }

            var anchor = spec.GetProperty("car");
            var away = anchor.TryGetProperty("awayFromImpact", out var distance) ? distance.GetSingle() : 0f;
            return car + streetSide * away + Vector3.Up * anchor.GetProperty("height").GetSingle();
        }

        Vector3 Target(Node3D actor, System.Text.Json.JsonElement spec)
        {
            if (spec.TryGetProperty("impact", out _)) return _impact with { Y = actor.GlobalPosition.Y };
            if (spec.TryGetProperty("actor", out var other)) return actors[other.GetString()!].GlobalPosition;
            var point = spec.GetProperty("point");
            return new Vector3(point[0].GetSingle(), actor.GlobalPosition.Y, point[1].GetSingle());
        }

        foreach (var entity in document.RootElement.GetProperty("entities").EnumerateArray())
        {
            var p = entity.GetProperty("params");
            Node3D Actor() => actors[p.GetProperty("actor").GetString()!];
            bool Flag(string name) => p.TryGetProperty(name, out var value) && value.GetBoolean();
            switch (p.GetProperty("action").GetString())
            {
                case "show": Show(Actor(), p.GetProperty("visible").GetBoolean()); break;
                case "place":
                {
                    var at = p.GetProperty("at");
                    var x = at[0].GetSingle();
                    var z = at[1].GetSingle();
                    Place(Actor(), new Vector3(x, GroundAt(x, z), z));
                    break;
                }
                case "clip": Clip(Actor(), p.GetProperty("clip").GetString()!); break;
                case "cut":
                    Cut(p.GetProperty("tag").GetString()!, Point(p.GetProperty("position")), Point(p.GetProperty("look")),
                        p.GetProperty("fov").GetSingle(), p.GetProperty("seconds").GetSingle(), Flag("handheld"), Flag("pushIn"));
                    break;
                case "wait": await Pause(p.GetProperty("seconds").GetDouble()); break;
                case "say":
                    if (p.TryGetProperty("talk", out var talker))
                        await SayAs(actors[talker.GetString()!], p.GetProperty("speaker").GetString()!, p.GetProperty("text").GetString()!, p.GetProperty("seconds").GetSingle(), Flag("deadpan"));
                    else
                        await Say(p.GetProperty("speaker").GetString()!, p.GetProperty("text").GetString()!, p.GetProperty("seconds").GetSingle(), Flag("deadpan"));
                    break;
                case "move":
                    await MoveAlong(Actor(), p.GetProperty("stops").EnumerateArray().Select(stop => new Vector2(stop[0].GetSingle(), stop[1].GetSingle())).ToArray(), p.GetProperty("speed").GetSingle());
                    break;
                case "face": FaceTowards(Actor(), Target(Actor(), p.GetProperty("target"))); break;
                case "filming": Filming(p.GetProperty("on").GetBoolean()); break;
                case "laugh": SetGuyLaugh(p.GetProperty("level").GetSingle()); break;
                default:
                    GD.PushError($"Cutscene action {p.GetProperty("action").GetString()} ({entity.GetProperty("id").GetString()}) has no executor; it needs a new mechanic.");
                    break;
            }
        }
    }

    // ---- direction helpers ----------------------------------------------------

    /// <summary>Diagnostic: with URMAN_CUTSCENE_TRACE=&lt;file&gt; every direction call is recorded, so the scene played from data can be compared with the scripted original.</summary>
    private static ulong? _traceStart;

    private static void Trace(string line)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_CUTSCENE_TRACE") is { Length: > 0 } path)
        {
            _traceStart ??= Time.GetTicksMsec();
            System.IO.File.AppendAllText(path, FormattableString.Invariant($"{(Time.GetTicksMsec() - _traceStart.Value) / 1000.0:0.00} ") + line + "\n");
        }
    }

    private static string V(Vector3 v) => FormattableString.Invariant($"{v.X:0.###},{v.Y:0.###},{v.Z:0.###}");

    private string ActorName(Node3D actor) => actor == _quest.GuyActor ? "guy" : actor == _quest.TamaraActor ? "tamara" : actor.Name.ToString();

    private static float GroundAt(float x, float z) =>
        Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z);

    private void FaceTowards(Node3D actor, Vector3 worldPoint)
    {
        Trace($"face {ActorName(actor)} {V(worldPoint)}");
        var direction = worldPoint - actor.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        actor.RotationDegrees = new(0,
            Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z)), 0);
    }

    private void Cut(string tag, Vector3 position, Vector3 look, float fov, float seconds,
        bool handheld = false, bool pushIn = false)
    {
        ShotShown?.Invoke(tag);
        Trace(FormattableString.Invariant($"cut {tag} {V(position)} {V(look)} {fov:0.##} {seconds:0.##} {handheld} {pushIn}"));
        _camFrom = _camTo != default ? _camTo : position;
        _camTo = position;
        _lookFrom = _lookTo != default ? _lookTo : look;
        _lookTo = look;
        _fovFrom = _fovTo != 0 ? _fovTo : fov;
        _fovTo = pushIn ? fov - 4f : fov;
        _shotElapsed = 0;
        _shotLength = pushIn ? seconds : .18;
        _handheld = handheld;
        _camera.GlobalPosition = position;
        _camera.LookAt(look);
        _camera.Fov = fov;
        _camera.MakeCurrent();
    }

    private float _guyLaughLevel;

    private void Clip(Node3D actor, string clip)
    {
        Trace($"clip {ActorName(actor)} {clip}");
        GeneratedCharacterKitDressing.PlayClip(actor, clip);
    }

    private void Show(Node3D actor, bool visible)
    {
        Trace($"show {ActorName(actor)} {visible}");
        actor.Visible = visible;
    }

    private void Filming(bool on)
    {
        Trace($"filming {on}");
        _quest.SetGuyFilming(on);
    }

    private void Place(Node3D actor, Vector3 position)
    {
        Trace($"place {ActorName(actor)} {V(position)}");
        actor.GlobalPosition = position;
    }

    private void SetGuyLaugh(float level)
    {
        Trace(FormattableString.Invariant($"laugh {level:0.##}"));
        _guyLaughLevel = level;
    }

    public override void _Process(double delta)
    {
        var scaled = delta * _speed;
        _shotElapsed += scaled;
        if (_shotLength > 0 && _shotElapsed < _shotLength)
        {
            var t = (float)(_shotElapsed / _shotLength);
            _camera.GlobalPosition = _camFrom.Lerp(_camTo, t);
            _camera.Fov = Mathf.Lerp(_fovFrom, _fovTo, t);
            _camera.LookAt(_lookTo);
        }

        if (_handheld)
        {
            var time = Time.GetTicksMsec() * .001f;
            var sway = new Vector3(
                Mathf.Sin(time * 1.7f) * .018f + Mathf.Sin(time * 3.3f) * .008f,
                Mathf.Sin(time * 1.1f) * .014f + Mathf.Sin(time * 2.9f) * .006f,
                0f);
            _camera.GlobalPosition += _camera.GlobalBasis * sway * .4f + sway * .6f;
        }

        _quest.UpdateGuyFilming(scaled, _guyLaughLevel);
    }

    private async Task Pause(double seconds)
    {
        Trace(FormattableString.Invariant($"wait {seconds:0.###}"));
        await Wait(seconds);
    }

    private async Task Wait(double seconds)
    {
        var remaining = seconds / _speed;
        var last = Time.GetTicksMsec() / 1000.0;
        while (remaining > 0 && !_skipRequested && StillCurrent())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksMsec() / 1000.0;
            remaining -= (now - last) * _speed;
            last = now;
        }
    }

    private async Task Say(string speaker, string textSuffix, float seconds, bool deadpan = false)
    {
        if (_skipRequested || !StillCurrent())
        {
            return;
        }

        var text = _bridge.ResolveText($"urman.chapter1:text/{textSuffix}");
        // Deadpan lines get no beat before them; the others keep a small pause.
        if (!deadpan)
        {
            await Wait(.18);
        }

        LineShown?.Invoke(textSuffix);
        Trace(FormattableString.Invariant($"say {speaker} {textSuffix} {seconds:0.##} {deadpan}"));
        // The authored beat is a minimum: a line stays up long enough to read
        // it, so a longer caption never disappears mid-sentence.
        var readable = Mathf.Max(seconds, text.Length * .055f + .9f);
        await _captions.ShowAsync(speaker, text, (float)(readable / _speed));
    }

    /// <summary>Whoever is speaking uses the kit's talk pose for the line; the
    /// others keep their own clip. The phone guy stays in his filming pose —
    /// his player is paused while he holds the phone, so the talk clip would
    /// drop the arm the whole scene is built around.</summary>
    private async Task SayAs(Node3D actor, string speaker, string textSuffix, float seconds, bool deadpan = false)
    {
        if (_skipRequested || !StillCurrent()) return;
        Trace($"talk {ActorName(actor)}");
        var canTalk = GeneratedCharacterKitDressing.PlayClip(actor, "Talk");
        await Say(speaker, textSuffix, seconds, deadpan);
        if (canTalk && StillCurrent() && !_skipRequested)
        {
            GeneratedCharacterKitDressing.PlayClip(actor, "Idle");
        }
    }

    private async Task MoveAlong(Node3D actor, Vector2[] stops, float metresPerSecond)
    {
        Trace(FormattableString.Invariant($"move {ActorName(actor)} {string.Join(";", stops.Select(stop => FormattableString.Invariant($"{stop.X:0.###},{stop.Y:0.###}")))} {metresPerSecond:0.##}"));
        foreach (var stop in stops)
        {
            if (_skipRequested || !StillCurrent()) return;
            var from = actor.GlobalPosition;
            var to = new Vector3(stop.X, GroundAt(stop.X, stop.Y), stop.Y);
            var direction = to - from;
            direction.Y = 0;
            FaceTowards(actor, to);
            var distance = direction.Length();
            var travelled = 0.0;
            var last = Time.GetTicksMsec() / 1000.0;
            while (travelled < distance && !_skipRequested && StillCurrent())
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksMsec() / 1000.0;
                // Real seconds, not frames: at 120 Hz a frame-counted walk made
                // everyone sprint.
                travelled += metresPerSecond * (now - last) * _speed;
                last = now;
                var t = Mathf.Clamp((float)(travelled / Math.Max(distance, .001f)), 0f, 1f);
                var next = from.Lerp(to, t);
                next.Y = GroundAt(next.X, next.Z);
                actor.GlobalPosition = next;
            }
        }
    }
}

/// <summary>
/// Bottom-centre caption strip shared by the cutscene and ambient remarks.
/// Presentation only: it never blocks input or owns state.
/// </summary>
public partial class CaptionStrip : CanvasLayer, IAccessibilitySettingsTarget
{
    private Control _screen = null!;
    private Label _speaker = null!;
    private Label _line = null!;
    private int _generation;

    public static CaptionStrip Ensure(SceneTree tree)
    {
        if (tree.GetFirstNodeInGroup("caption_strip") is CaptionStrip existing
            && GodotObject.IsInstanceValid(existing))
        {
            return existing;
        }

        var strip = new CaptionStrip { Name = "CaptionStrip" };
        tree.Root.CallDeferred(Node.MethodName.AddChild, strip);
        return strip;
    }

    public override void _Ready()
    {
        AddToGroup("caption_strip");
        Layer = 14;
        _screen = new Control
        {
            Name = "Screen",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        AddChild(_screen);
        // Without a full-rect screen the panel's anchors resolve against a
        // zero-sized control in the top-left corner and every caption lands
        // off-screen. The strip is programmatic, so the preset has to be set
        // here rather than in a scene file.
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Name = "Panel" };
        _screen.AddChild(panel);
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.1f, .09f, .08f, .9f),
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 10, ContentMarginBottom = 12,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };
        panel.AddThemeStyleboxOverride("panel", style);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 2);
        panel.AddChild(layout);
        _speaker = new Label
        {
            Name = "Speaker",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _speaker.AddThemeFontSizeOverride("font_size", 14);
        _speaker.AddThemeColorOverride("font_color", new Color(.82f, .68f, .42f));
        _line = new Label
        {
            Name = "Line",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _line.AddThemeFontSizeOverride("font_size", 20);
        _line.AddThemeColorOverride("font_color", new Color(.95f, .92f, .84f));
        _line.AddThemeColorOverride("font_outline_color", Colors.Black);
        _line.AddThemeConstantOverride("outline_size", 3);
        layout.AddChild(_speaker);
        layout.AddChild(_line);
        AddToGroup(AccessibilityPresentation.TargetGroup);
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
        {
            ApplyAccessibilitySettings(player.Accessibility);
        }
        panel.AnchorLeft = panel.AnchorRight = .5f;
        panel.AnchorTop = panel.AnchorBottom = 1f;
        panel.OffsetLeft = -430;
        panel.OffsetRight = 430;
        panel.OffsetTop = -186;
        panel.OffsetBottom = -104;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        // Two wrapped lines at the largest accessibility scale still have to
        // fit: the strip grows upward from its bottom anchor instead of
        // pushing the text past the viewport edge.
        panel.GrowVertical = Control.GrowDirection.Begin;
    }

    /// <summary>Captions are the only speech a player gets here: they grow with
    /// the accessibility text scale and the strip rises with them.</summary>
    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        var scale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        _speaker.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(14 * scale));
        _line.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(20 * scale));
        if (settings.HighContrast)
        {
            _line.AddThemeColorOverride("font_color", Colors.White);
            _speaker.AddThemeColorOverride("font_color", new Color(1f, .85f, .55f));
        }
    }

    public async Task ShowAsync(string speaker, string text, float seconds)
    {
        _generation++;
        var generation = _generation;
        _speaker.Text = speaker;
        _line.Text = text;
        _screen.Visible = true;
        var remaining = seconds;
        while (remaining > 0 && generation == _generation && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            remaining -= (float)GetProcessDeltaTime();
        }

        if (generation == _generation && IsInsideTree())
        {
            _screen.Visible = false;
        }
    }
}
