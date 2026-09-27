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
public partial class TamaraFenceCutscene : Node, IAccessibilitySettingsTarget
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
    private bool _skipRequested;
    private bool _controlsReleased;
    private bool _playerWasInCar;
    internal bool InterruptedByLoad { get; private set; }
    private double _speed = 1.0;
    private Vector3 _camFrom;
    private Vector3 _camTo;
    private Vector3 _lookTo;
    private float _fovFrom;
    private float _fovTo;
    private double _shotElapsed;
    private double _shotLength;
    private bool _handheld;
    private bool _followTamara;
    private LookAtModifier3D? _tamaraLook;
    private Node3D? _tamaraLookTarget;
    private Vector3 _tamaraLookGoal;

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
        _skipHint.OffsetLeft = -460;
        _skipHint.OffsetRight = -28;
        _skipHint.OffsetTop = -64;
        _skipHint.OffsetBottom = -24;
        _skipHint.GrowHorizontal = Control.GrowDirection.Begin;
        _skipHint.GrowVertical = Control.GrowDirection.Begin;
        _skipHint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _skipHint.AddThemeConstantOverride("outline_size", 3);
        _skipHint.HorizontalAlignment = HorizontalAlignment.Right;
        canvas.AddChild(_skipHint);
        AddToGroup(AccessibilityPresentation.TargetGroup);
        ApplyAccessibilitySettings(_player.Accessibility);
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        if (!GodotObject.IsInstanceValid(_skipHint)) return;
        var scale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        _skipHint.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(22 * scale));
        _skipHint.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color(.95f, .92f, .84f));
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
        _playerWasInCar = _player.VehicleControlled;
        _bridge.PlayTimeBoundary += OnSessionBoundary;
        _player.SetModalOpen(true);
        try
        {
            await Script();
            return !_skipRequested && StillCurrent();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            ClearTamaraLook();
            if (GodotObject.IsInstanceValid(_skipHint)) _skipHint.GetParent().QueueFree();
            _bridge.PlayTimeBoundary -= OnSessionBoundary;
            ReleaseControls();
        }
    }

    private void OnSessionBoundary(string boundary)
    {
        if (boundary != "load-start") return;
        // Release this scene's gate before the loader owns the next session.
        InterruptedByLoad = true;
        _skipRequested = true;
        ClearTamaraLook();
        ReleaseControls();
    }

    private void ReleaseControls()
    {
        if (_controlsReleased || !StillCurrent()) return;
        _controlsReleased = true;
        _player.SetModalOpen(false);
        if (_playerWasInCar && _car is { } car && GodotObject.IsInstanceValid(car))
            car.VehicleCamera.MakeCurrent();
        else if (_player.GetNodeOrNull<Camera3D>("Head/Camera3D") is { } headCamera)
            headCamera.MakeCurrent();
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
        // The camera belongs on the street, i.e. opposite the breach: the
        // shoulders are the only ground around here without a neighbour's
        // frontage in the way.
        var towardFence = _impact - car;
        towardFence.Y = 0f;
        if (towardFence.LengthSquared() < .01f) towardFence = Vector3.Right;
        var streetSide = -towardFence.Normalized();
        var quarter = new Vector3(-streetSide.Z, 0, streetSide.X);
        Cut("hit", car + streetSide * 3.7f + quarter * 2.7f + Vector3.Up * 1.95f,
            car.Lerp(_impact, .45f) + Vector3.Up * .8f, 54f, 2.6f, handheld: true);
        await Wait(.8);
        await Say("АЙДАР", "tamara-cutscene-aidar-ouch", 1.0f);

        // The crash can stop the car across the authored approach. Stage the
        // filming marks north of its actual chassis, with room for arms/phone.
        var guyOffset = Vector3.Zero;
        if (_car is { } wreck && GodotObject.IsInstanceValid(wreck))
        {
            var hull = wreck.GlobalTransform * new Aabb(
                wreck.Definition.HullCenter - wreck.Definition.HullSize * .5f, wreck.Definition.HullSize);
            guyOffset.Z = Mathf.Max(0f, hull.End.Z + .7f - (-42.6f));
        }
        var arrival = new Vector2(.15f, -41.9f + guyOffset.Z);

        // §5.2 the guy arrives, already reaching for the phone.
        guy.Visible = true;
        var entryZ = Mathf.Max(-36.3f, arrival.Y + .5f);
        guy.GlobalPosition = new(-.8f, GroundAt(-.8f, entryZ), entryZ);
        FaceTowards(guy, new(arrival.X, guy.GlobalPosition.Y, arrival.Y));
        var approachCentreZ = (entryZ + arrival.Y) * .5f;
        Cut("guy-arrives", new(-.9f, 1.7f, arrival.Y - 1f), new(-.3f, .8f, approachCentreZ), 70f, 3.0f);
        await MoveAlong(guy, [arrival], 1.15f);
        GeneratedCharacterKitDressing.PlayClip(guy, "Idle");
        await TurnGuyTowards(new(2.3f, guy.GlobalPosition.Y, -44.6f));
        await Wait(.3);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-wait", 1.0f);
        _quest.SetGuyFilming(true);
        await TurnGuyTowards(_impact with { Y = guy.GlobalPosition.Y });
        Cut("filming", new Vector3(1.75f, 1.58f, -42.3f) + guyOffset, new Vector3(.2f, 1.42f, -41.95f) + guyOffset, 52f, 2.4f, handheld: true);
        await Wait(.9);
        // A step closer to the breach; the first quiet laugh.
        await MoveAlong(guy, [new(-.15f, -42.35f + guyOffset.Z)], .8f);
        GeneratedCharacterKitDressing.PlayClip(guy, "Idle", .18 / _speed);
        await Wait(.4);

        // §5.3 Tamara Gennadievna comes out.
        tamara.Visible = true;
        tamara.GlobalPosition = new(8.6f, GroundAt(8.6f, -41.3f), -41.3f);
        GeneratedCharacterKitDressing.PlayClip(tamara, "Walk");
        var followTamara = !_player.Accessibility.ReducedMotion;
        Cut("tamara-comes-out", new(2.7f, 1.7f, -38.9f),
            followTamara ? tamara.GlobalPosition + Vector3.Up * 1.2f : new(6.7f, 1.25f, -42.1f), 52f, 3.2f);
        _followTamara = followTamara;
        await MoveAlong(tamara, [new(6.4f, -42.9f), new(4.7f, -44.4f)], 1.2f,
            finalFacing: new(-.2f, -42.8f));
        GeneratedCharacterKitDressing.PlayClip(tamara, "Idle", .18 / _speed);
        BeginTamaraLook(tamara);
        FaceTowards(tamara, _impact with { Y = tamara.GlobalPosition.Y });
        await TurnGuyTowards(tamara.GlobalPosition);
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
        await TurnGuyTowards(new(.0f, guy.GlobalPosition.Y, -43.2f));
        await Say("АЙДАР", "tamara-cutscene-aidar-skid", 2.2f);

        // §5.5 the line the whole village will hear about.
        Cut("punchline", new Vector3(1.75f, 1.58f, -42.3f) + guyOffset, new Vector3(.2f, 1.42f, -41.95f) + guyOffset, 44f, 7.4f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-check", 1.0f);
        await Say("АЙДАР", "tamara-cutscene-aidar-yes", .9f);
        await TurnGuyTowards(_impact with { Y = guy.GlobalPosition.Y });
        await Wait(.3);
        await TurnGuyTowards(tamara.GlobalPosition);
        await Wait(.25);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-line-1", 1.3f);
        // He cannot keep the phone still through the punchline.
        SetGuyLaugh(.45f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-line-2", 2.0f);
        SetGuyLaugh(1f);
        // Her face from her own yard: the fallen shields reach x≈4.0, so the
        // camera stands outside that heap instead of behind it.
        Cut("tamara-reaction", new(3.5f, 1.62f, -42.0f), new(4.7f, 1.38f, -44.4f), 46f, 4.6f, pushIn: true);
        // §5.5: no next joke — a few seconds of people just being there.
        await Wait(2.6);
        SetGuyLaugh(.5f);

        // §5.6 the phone keeps the situation moving, in its own way.
        FaceTowards(tamara, guy.GlobalPosition);
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-filming", 1.1f);
        await Say("ПАРЕНЬ С ТЕЛЕФОНОМ", "tamara-cutscene-guy-yes", 1.0f);
        FaceTowards(tamara, new(-.4f, tamara.GlobalPosition.Y, -42.6f));
        await SayAs(tamara, "ТАМАРА ГЕННАДЬЕВНА", "tamara-cutscene-tamara-plate", 1.3f);
        await TurnGuyTowards(new(-.6f, guy.GlobalPosition.Y, -42.4f));
        Cut("boards-talk", new(3.3f, 1.68f, -41.4f), new(4.7f, 1.25f, -44.4f), 46f, 2.8f);
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
        var widePosition = new Vector3(6.7f, 2.1f, -40.2f);
        var wideDirection = (guy.GlobalPosition + Vector3.Up * .9f - widePosition).Normalized()
            + (tamara.GlobalPosition + Vector3.Up * .9f - widePosition).Normalized();
        Cut("final-wide", widePosition, widePosition + wideDirection.Normalized() * 5f, 70f, 2.8f);
        await Wait(.4);
        await MoveAlong(guy, [new(-.45f, -42.6f + guyOffset.Z)], .35f);
        GeneratedCharacterKitDressing.PlayClip(guy, "Idle", .18 / _speed);
        await Wait(1.0);
        SetGuyLaugh(0f);
    }

    // ---- direction helpers ----------------------------------------------------

    private static float GroundAt(float x, float z) =>
        Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(x, z);

    private void FaceTowards(Node3D actor, Vector3 worldPoint)
    {
        var direction = worldPoint - actor.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        if (actor == _quest.TamaraActor && _tamaraLookTarget is not null)
        {
            _tamaraLookGoal = actor.GlobalPosition + Vector3.Up * 1.56f + direction.Normalized() * 3f;
            return;
        }
        actor.RotationDegrees = new(0,
            Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z)), 0);
    }

    private async Task TurnGuyTowards(Vector3 worldPoint)
    {
        CheckContinuation();
        var guy = _quest.GuyActor;
        var direction = worldPoint - guy.GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        var from = guy.Rotation.Y;
        var to = Mathf.Atan2(direction.X, direction.Z);
        var angle = Mathf.Abs(Mathf.AngleDifference(from, to));
        if (angle < Mathf.DegToRad(3)) return;
        var duration = Mathf.Clamp(angle / Mathf.DegToRad(150), .15f, .8f);
        var elapsed = 0f;
        var last = Time.GetTicksMsec() / 1000.0;
        while (elapsed < duration)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckContinuation();
            var now = Time.GetTicksMsec() / 1000.0;
            elapsed += (float)((now - last) * _speed);
            last = now;
            var t = Mathf.Clamp(elapsed / duration, 0f, 1f);
            guy.Rotation = new(0, Mathf.LerpAngle(from, to, t * t * (3f - 2f * t)), 0);
        }
    }

    private void BeginTamaraLook(Node3D tamara)
    {
        var body = tamara.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().First(mesh => mesh.Name == "Tamara_Body_LOD0");
        var rig = body.GetNode<Skeleton3D>(body.Skeleton);
        _tamaraLookTarget = new Node3D { Name = "TamaraLookTarget" };
        AddChild(_tamaraLookTarget);
        _tamaraLookGoal = tamara.GlobalPosition + Vector3.Up * 1.56f + tamara.GlobalBasis.Z * 3f;
        _tamaraLookTarget.GlobalPosition = _tamaraLookGoal;
        _tamaraLook = new LookAtModifier3D
        {
            Name = "TamaraConversationLook", BoneName = "Head", Influence = 0,
            ForwardAxis = SkeletonModifier3D.BoneAxis.PlusZ,
            PrimaryRotationAxis = Vector3.Axis.Y, UseSecondaryRotation = false,
            UseAngleLimitation = true, SymmetryLimitation = true,
            PrimaryLimitAngle = Mathf.DegToRad(35), Duration = (float)(.25 / _speed)
        };
        rig.AddChild(_tamaraLook);
        _tamaraLook.TargetNode = _tamaraLook.GetPathTo(_tamaraLookTarget);
    }

    private void ClearTamaraLook()
    {
        if (_tamaraLook is not null && GodotObject.IsInstanceValid(_tamaraLook))
        {
            _tamaraLook.Active = false;
            _tamaraLook.QueueFree();
        }
        _tamaraLook = null;
        // The target is owned by this cutscene and freed with it.
        _tamaraLookTarget = null;
    }

    private void Cut(string tag, Vector3 position, Vector3 look, float fov, float seconds,
        bool handheld = false, bool pushIn = false)
    {
        CheckContinuation();
        ShotShown?.Invoke(tag);
        pushIn &= !_player.Accessibility.ReducedMotion;
        // A cut has its own camera base; never fly through the old composition.
        _camFrom = position;
        _camTo = pushIn ? position.MoveToward(look, .25f) : position;
        _lookTo = look;
        _followTamara = false;
        _fovFrom = fov;
        _fovTo = pushIn ? fov - 4f : fov;
        _shotElapsed = 0;
        _shotLength = pushIn ? seconds : .18;
        _handheld = handheld && !_player.Accessibility.ReducedMotion;
        _camera.GlobalPosition = position;
        _camera.LookAt(look);
        _camera.Fov = fov;
        _camera.MakeCurrent();
    }

    private float _guyLaughLevel;

    private void SetGuyLaugh(float level) => _guyLaughLevel = level;

    public override void _Process(double delta)
    {
        if (_skipRequested || !StillCurrent() || _shotLength <= 0) return;
        var scaled = delta * _speed;
        if (_tamaraLook is not null && _tamaraLookTarget is not null)
        {
            _tamaraLook.Influence = Mathf.MoveToward(_tamaraLook.Influence, 1f, (float)scaled / .25f);
            _tamaraLookTarget.GlobalPosition = _tamaraLookTarget.GlobalPosition.Lerp(
                _tamaraLookGoal, 1f - Mathf.Exp(-10f * (float)scaled));
        }
        _shotElapsed += scaled;
        var t = Mathf.Clamp((float)(_shotElapsed / _shotLength), 0f, 1f);
        t = t * t * (3f - 2f * t);
        _camera.GlobalPosition = _camFrom.Lerp(_camTo, t);
        _camera.Fov = Mathf.Lerp(_fovFrom, _fovTo, t);
        // Keep the walking subject in frame instead of holding on the empty porch.
        if (_followTamara && !_player.Accessibility.ReducedMotion)
            _lookTo = _lookTo.Lerp(_quest.TamaraActor.GlobalPosition + Vector3.Up * 1.2f,
                1f - Mathf.Exp(-5f * (float)scaled));
        _camera.LookAt(_lookTo);

        if (_handheld)
        {
            var time = (float)_shotElapsed;
            var sway = new Vector3(
                Mathf.Sin(time * 1.7f) * .018f + Mathf.Sin(time * 3.3f) * .008f,
                Mathf.Sin(time * 1.1f) * .014f + Mathf.Sin(time * 2.9f) * .006f,
                0f);
            _camera.GlobalPosition += _camera.GlobalBasis * sway * .4f + sway * .6f;
        }

        _quest.UpdateGuyFilming(scaled, _guyLaughLevel);
    }

    private void CheckContinuation()
    {
        if (_skipRequested || !StillCurrent()) throw new OperationCanceledException();
    }

    private async Task Wait(double seconds)
    {
        CheckContinuation();
        var remaining = seconds;
        var last = Time.GetTicksMsec() / 1000.0;
        while (remaining > 0 && !_skipRequested && StillCurrent())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksMsec() / 1000.0;
            remaining -= (now - last) * _speed;
            last = now;
        }
        CheckContinuation();
    }

    private async Task Say(string speaker, string textSuffix, float seconds, bool deadpan = false)
    {
        CheckContinuation();
        var text = _bridge.ResolveText($"urman.chapter1:text/{textSuffix}");
        // Deadpan lines get no beat before them; the others keep a small pause.
        if (!deadpan)
        {
            await Wait(.18);
        }

        LineShown?.Invoke(textSuffix);
        // The authored beat is a minimum: a line stays up long enough to read
        // it, so a longer caption never disappears mid-sentence.
        var readable = Mathf.Max(seconds, text.Length * .055f + .9f);
        await _captions.ShowAsync(speaker, text, (float)(readable / _speed),
            () => _skipRequested || !StillCurrent());
        CheckContinuation();
    }

    /// <summary>Whoever is speaking uses the kit's talk pose for the line; the
    /// others keep their own clip. The phone guy stays in his filming pose —
    /// his held arm is overlaid on the movement/idle clip by the quest owner.</summary>
    private async Task SayAs(Node3D actor, string speaker, string textSuffix, float seconds, bool deadpan = false)
    {
        CheckContinuation();
        var canTalk = GeneratedCharacterKitDressing.PlayClip(actor, "Talk", .18 / _speed);
        await Say(speaker, textSuffix, seconds, deadpan);
        if (canTalk && StillCurrent() && !_skipRequested)
        {
            GeneratedCharacterKitDressing.PlayClip(actor, "Idle", .18 / _speed);
        }
    }

    private async Task MoveAlong(Node3D actor, Vector2[] stops, float metresPerSecond,
        Vector2? finalFacing = null)
    {
        CheckContinuation();
        using var path = new Curve3D { BakeInterval = .05f };
        var points = new[] { new Vector3(actor.GlobalPosition.X, 0, actor.GlobalPosition.Z) }
            .Concat(stops.Select(stop => new Vector3(stop.X, 0, stop.Y))).ToArray();
        for (var index = 0; index < points.Length; index++)
        {
            var handle = Vector3.Zero;
            if (finalFacing is { } facing)
            {
                var direction = index == points.Length - 1
                    ? new Vector3(facing.X, 0, facing.Y) - points[index]
                    : points[index + 1] - points[Math.Max(0, index - 1)];
                handle = direction.Normalized() * .4f;
            }
            path.AddPoint(points[index], -handle, handle);
        }
        var distance = path.GetBakedLength();
        var firstFacing = actor.GlobalPosition + path.SampleBaked(Mathf.Min(.03f, distance), true) - points[0];
        if (actor == _quest.GuyActor)
        {
            await TurnGuyTowards(firstFacing);
            GeneratedCharacterKitDressing.PlayClip(actor, "Walk", .18 / _speed);
        }
        else FaceTowards(actor, firstFacing);
        // The curved arrival finishes with a step and a stop, not a full-speed
        // pivot. Slow the existing clip with the root; Play(Idle) resets its
        // per-play speed on completion, skip and session projection.
        var brakeDistance = finalFacing.HasValue ? Mathf.Min(.6f, distance) : 0f;
        var cruiseTime = (distance - brakeDistance) / metresPerSecond;
        var brakeTime = 2f * brakeDistance / metresPerSecond;
        var walkPlayer = finalFacing.HasValue
            ? actor.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>()
                .First(player => player.HasAnimation("Tamara_Walk")) : null;
        var elapsed = 0f;
        var travelled = 0f;
        var last = Time.GetTicksMsec() / 1000.0;
        while (travelled < distance)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckContinuation();
            var now = Time.GetTicksMsec() / 1000.0;
            elapsed += (float)((now - last) * _speed);
            last = now;
            var brake = brakeTime > 0f ? Mathf.Clamp((elapsed - cruiseTime) / brakeTime, 0f, 1f) : 0f;
            if (elapsed >= cruiseTime + brakeTime) travelled = distance;
            else if (elapsed <= cruiseTime) travelled = elapsed * metresPerSecond;
            else travelled = distance - brakeDistance + brakeDistance * (2f * brake - brake * brake);
            walkPlayer?.Play(customSpeed: (1f - brake) * (float)_speed);
            var next = path.SampleBaked(travelled, true);
            next.Y = GroundAt(next.X, next.Z);
            actor.GlobalPosition = next;
            var tangent = path.SampleBaked(Mathf.Min(distance, travelled + .03f), true)
                - path.SampleBaked(Mathf.Max(0, travelled - .03f), true);
            FaceTowards(actor, next + tangent);
        }
        CheckContinuation();
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
        panel.AnchorLeft = .14f;
        panel.AnchorRight = .86f;
        panel.AnchorTop = panel.AnchorBottom = 1f;
        panel.OffsetLeft = panel.OffsetRight = 0;
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
        _line.AddThemeColorOverride("font_color", settings.HighContrast ? Colors.White : new Color(.95f, .92f, .84f));
        _speaker.AddThemeColorOverride("font_color", settings.HighContrast ? new Color(1f, .85f, .55f) : new Color(.82f, .68f, .42f));
    }

    public async Task ShowAsync(string speaker, string text, float seconds, Func<bool>? cancelled = null)
    {
        // Ensure() can defer the first attachment when the scene is being built.
        if (!IsNodeReady()) await ToSignal(this, Node.SignalName.Ready);
        if (cancelled?.Invoke() == true) return;
        _generation++;
        var generation = _generation;
        _speaker.Text = speaker;
        _line.Text = text;
        _screen.Visible = true;
        var remaining = seconds;
        while (remaining > 0 && generation == _generation && IsInsideTree() && cancelled?.Invoke() != true)
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
