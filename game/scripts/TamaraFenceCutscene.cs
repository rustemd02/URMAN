using Godot;

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

    private async Task Script()
    {
        var guy = _quest.GuyActor;
        var tamara = _quest.TamaraActor;
        // The crash transaction stages both witnesses at their rest spots one
        // frame before this scene exists; the script decides when they appear,
        // so nothing pops in behind the first shot.
        tamara.Visible = false;
        guy.Visible = false;

        // Every camera stands clear of the wreck: the Niva sits nose-in at the
        // breach, so the shots are placed off its quarter rather than looking
        // through it. §5.1 the hit itself: no lines for a second, the settling
        // village and the car still ticking.
        // The wreck decides where the cameras stand: the Niva is nose-in at the
        // breach, so the hit plays from beside its left flank, clear of both
        // the hull and the new concrete pillars.
        var car = _car is { } liveCar && GodotObject.IsInstanceValid(liveCar)
            ? liveCar.GlobalPosition
            : _impact + new Vector3(-1.6f, 0f, .9f);
        var alongFence = _car is { } facing && GodotObject.IsInstanceValid(facing)
            ? facing.GlobalBasis.Z with { Y = 0 }
            : new Vector3(0f, 0f, 1f);
        alongFence = alongFence.LengthSquared() < .0001f ? Vector3.Forward : alongFence.Normalized();
        var acrossHull = alongFence.Cross(Vector3.Up).Normalized();
        Cut("hit", car + acrossHull * 4.1f + alongFence * .5f + Vector3.Up * 1.78f,
            car + Vector3.Up * .92f, 54f, 2.6f, handheld: true);
        await Wait(.8);
        await Say("АЙДАР", "tamara-cutscene-aidar-ouch", 1.0f);

        // §5.2 the guy arrives, already reaching for the phone.
        guy.Visible = true;
        guy.GlobalPosition = new(-0.8f, GroundAt(-0.8f, -36.3f), -36.3f);
        GeneratedCharacterKitDressing.PlayClip(guy, "Walk");
        Cut("guy-arrives", new(-1.55f, 1.78f, -39.2f), new(0.4f, 1.15f, -41.4f), 58f, 3.0f);
        await MoveAlong(guy, [new(.15f, -41.9f)], 1.45f);
        GeneratedCharacterKitDressing.PlayClip(guy, "Idle");
        FaceTowards(guy, new(2.3f, guy.GlobalPosition.Y, -44.6f));
        await Wait(.3);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-wait", 1.0f);
        _quest.SetGuyFilming(true);
        FaceTowards(guy, _impact with { Y = guy.GlobalPosition.Y });
        Cut("filming", new(.7f, 1.75f, -40.7f), new(2.3f, 1.0f, -44.3f), 50f, 2.4f, handheld: true);
        await Wait(.9);
        // A step closer to the breach; the first quiet laugh.
        await MoveAlong(guy, [new(-.15f, -42.35f)], .8f);
        await Wait(.4);

        // §5.3 Tamara Gennadievna comes out.
        tamara.Visible = true;
        tamara.GlobalPosition = new(8.6f, GroundAt(8.6f, -41.3f), -41.3f);
        GeneratedCharacterKitDressing.PlayClip(tamara, "Walk");
        Cut("tamara-comes-out", new(2.7f, 1.7f, -38.9f), new(6.7f, 1.25f, -42.1f), 52f, 3.2f);
        await MoveAlong(tamara, [new(6.4f, -42.9f), new(4.7f, -44.4f)], 1.2f);
        GeneratedCharacterKitDressing.PlayClip(tamara, "Idle");
        FaceTowards(tamara, _impact with { Y = tamara.GlobalPosition.Y });
        FaceTowards(guy, tamara.GlobalPosition);
        // She simply registers what stands where.
        await Wait(.3);
        FaceTowards(tamara, new(-.2f, tamara.GlobalPosition.Y, -42.8f));
        await Wait(.5);
        FaceTowards(tamara, guy.GlobalPosition);

        // §5.4 Aidar explains himself as decently as he can. Two faces and the
        // wreck between them: the wreck never hides the people.
        await Say("АЙДАР", "tamara-cutscene-aidar-hello", 1.1f);
        await Say("АЙДАР", "tamara-cutscene-aidar-erm", 1.0f);
        Cut("explains", new(6.3f, 1.72f, -41.0f), new(4.75f, 1.32f, -44.4f), 50f, 8.2f);
        await Say("АЙДАР", "tamara-cutscene-aidar-name", 2.2f);
        FaceTowards(guy, new(.0f, guy.GlobalPosition.Y, -43.2f));
        await Say("АЙДАР", "tamara-cutscene-aidar-skid", 2.2f);

        // §5.5 the line the whole village will hear about.
        Cut("punchline", new(1.35f, 1.58f, -40.4f), new(.2f, 1.42f, -41.95f), 44f, 7.4f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-check", 1.0f);
        await Say("АЙДАР", "tamara-cutscene-aidar-yes", .9f);
        FaceTowards(guy, _impact with { Y = guy.GlobalPosition.Y });
        await Wait(.3);
        FaceTowards(guy, tamara.GlobalPosition);
        await Wait(.25);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-line-1", 1.3f);
        // He cannot keep the phone still through the punchline.
        SetGuyLaugh(.45f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-line-2", 2.0f);
        SetGuyLaugh(1f);
        // Her face from her own yard: the fallen shields reach x≈4.0, so the
        // camera stands outside that heap instead of behind it.
        Cut("tamara-reaction", new(5.6f, 1.6f, -47.4f), new(4.75f, 1.4f, -44.5f), 46f, 4.6f, pushIn: true);
        // §5.5: no next joke — a few seconds of people just being there.
        await Wait(2.6);
        SetGuyLaugh(.5f);

        // §5.6 the phone keeps the situation moving, in its own way.
        FaceTowards(tamara, guy.GlobalPosition);
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-filming", 1.1f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-yes", 1.0f);
        FaceTowards(tamara, new(-.4f, tamara.GlobalPosition.Y, -42.6f));
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-plate", 1.3f);
        FaceTowards(guy, new(-.6f, guy.GlobalPosition.Y, -42.4f));
        Cut("boards-talk", new(-1.8f, 1.8f, -40.2f), new(2.4f, 1.1f, -43.8f), 50f, 2.8f);
        await Wait(1.2);
        SetGuyLaugh(.2f);
        await Say("АЙДАР", "tamara-cutscene-aidar-stay", 1.2f);
        FaceTowards(tamara, _impact with { Y = tamara.GlobalPosition.Y });
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-good", 1.0f);
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-boards", 1.5f);
        await Wait(.35);
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-normal", 1.1f);
        await Say("АЙДАР", "tamara-cutscene-aidar-ok", 1.0f);
        FaceTowards(tamara, new(2.6f, tamara.GlobalPosition.Y, -44.2f));
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-not-these", 1.5f, deadpan: true);

        // §5.7 the whole street with the three of them, the guy still filming.
        Cut("final-wide", new(-2.6f, 1.9f, -47.6f), new(2.3f, 1.0f, -43.8f), 62f, 2.8f);
        await Wait(.4);
        await MoveAlong(guy, [new(-.45f, -42.6f)], .35f);
        await Wait(1.0);
        SetGuyLaugh(0f);
    }

    // ---- direction helpers ----------------------------------------------------

    private static float GroundAt(float x, float z) =>
        Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z);

    private static void FaceTowards(Node3D actor, Vector3 worldPoint)
    {
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

    private void SetGuyLaugh(float level) => _guyLaughLevel = level;

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
        await _captions.ShowAsync(speaker, text, (float)(seconds / _speed));
    }

    /// <summary>Whoever is speaking uses the kit's talk pose for the line; the
    /// others keep their own clip. The phone guy stays in his filming pose —
    /// his player is paused while he holds the phone, so the talk clip would
    /// drop the arm the whole scene is built around.</summary>
    private async Task SayAs(Node3D actor, string speaker, string textSuffix, float seconds, bool deadpan = false)
    {
        if (_skipRequested || !StillCurrent()) return;
        var canTalk = GeneratedCharacterKitDressing.PlayClip(actor, "Talk");
        await Say(speaker, textSuffix, seconds, deadpan);
        if (canTalk && StillCurrent() && !_skipRequested)
        {
            GeneratedCharacterKitDressing.PlayClip(actor, "Idle");
        }
    }

    private async Task MoveAlong(Node3D actor, Vector2[] stops, float metresPerSecond)
    {
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
public partial class CaptionStrip : CanvasLayer
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
