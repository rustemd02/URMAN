using Godot;
using Urman.Core.Persistence;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class FirstPersonController : CharacterBody3D, IAccessibilitySettingsTarget
{
    private const string InteractionAction = "interact";
    // A player more than this far below the terrain height at their own X/Z has
    // left the walkable surface (world edge, hole in collision) and is put back
    // on the ground instead of falling through the world.
    private const float FallRecoveryDepth = 2.5f;
    // ponytail: two physics frames keep overlapping targets stable; replace
    // with measured dwell/angle selection if authored target density grows.
    private const int FocusSwitchFrames = 2;

    [Export(PropertyHint.Range, "1,8,0.1")]
    public float WalkSpeed { get; set; } = 3.4f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float MouseSensitivity { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "30,240,1")]
    public float GamepadLookSpeed { get; set; } = 105f;

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private RayCast3D _interactionRay = null!;
    private ColorRect _reticle = null!;
    private Label _interactionPrompt = null!;
    private InteractionTarget? _focusedTarget;
    private InteractionTarget? _focusCandidate;
    private int _focusCandidateFrames;
    private InteractionTarget? _promptTarget;
    private bool _promptTargetAvailable;
    private string _promptHint = string.Empty;
    private string _promptLabel = string.Empty;
    private Tween? _interactionPromptTween;
    private float _pitch;
    private float _gravity;
    private bool _modalOpen;
    private bool _sessionTransition;
    private bool _worldInteractionNeedsRelease;
    private bool _motionBlur;
    private bool _headBob;
    private string _graphicsPreset = GraphicsQuality.DefaultPreset();
    private string _inputDevice = "keyboard-mouse";
    /// <summary>ACT1-LANG.5: chosen starting Tatar knowledge level (none/some/fluent).</summary>
    public string TatarLanguageLevel
    {
        get => _tatarLanguageLevel;
        set => _tatarLanguageLevel = NormalizeTatarLanguageLevel(value);
    }

    private string _tatarLanguageLevel = "none";
    private string _keyboardInteractionHint = "[E]";
    private string _gamepadInteractionHint = "[A]";
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private Vector3 _headBasePosition;
    private float _headBobPhase;
    private CarryCoordinator? _carryCoordinator;

    public bool ModalOpen => _modalOpen || _sessionTransition;
    internal string ModalDiagnostic => $"modalFlag={_modalOpen} sessionTransition={_sessionTransition}";
    internal bool InIntro => GetParent()?.GetParent() is Act1DemoRoot { IntroVisible: true };
    internal bool InteractionNoticeActive => Time.GetTicksMsec() < _traversalNoticeUntil;
    internal string FocusedInteractionId => _focusedTarget is not null && GodotObject.IsInstanceValid(_focusedTarget)
        ? _focusedTarget.InteractionId : string.Empty;

    /// <summary>Times the player was pushed back inside the authored world window.</summary>
    public int EdgeClamps { get; private set; }

    /// <summary>Times the player was recovered after leaving the walkable surface.</summary>
    public int FallRecoveries { get; private set; }
    internal int PresentationTransformRevision { get; private set; }

    public string CurrentInputDevice => _inputDevice;

    public string InteractionHint => FormatInteractionHint();

    public string GraphicsPreset => _graphicsPreset;

    public bool HeadBobEnabled => _headBob && !_accessibility.ReducedMotion;

    /// <summary>UIUX-010: presentation owners gate nonessential motion on this.</summary>
    public bool ReducedMotion => _accessibility.ReducedMotion;

    public AccessibilitySettingsSnapshot Accessibility => _accessibility;

    /// <summary>
    /// The target the interaction ray chooses. A target that is only open for
    /// a repeat (reread a finished source) must not shadow a live action
    /// behind it: the old PC's finished document boxes sit in front of its
    /// power button, and a later "switch the PC on" step was unreachable.
    /// </summary>
    internal InteractionTarget? PeekRayTarget()
    {
        _interactionRay.ForceRaycastUpdate();
        var first = _interactionRay.IsColliding() ? _interactionRay.GetCollider() as InteractionTarget : null;
        if (first is null || !first.IsRepeatOnly) return first;
        var skipped = new List<CollisionObject3D>();
        try
        {
            var current = first;
            for (var depth = 0; depth < 3 && current is { IsRepeatOnly: true }; depth++)
            {
                _interactionRay.AddException(current);
                skipped.Add(current);
                _interactionRay.ForceRaycastUpdate();
                current = _interactionRay.IsColliding() ? _interactionRay.GetCollider() as InteractionTarget : null;
                if (current is { IsLiveAvailable: true }) return current;
            }
            return first;
        }
        finally
        {
            foreach (var body in skipped) _interactionRay.RemoveException(body);
            _interactionRay.ForceRaycastUpdate();
        }
    }

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        if (_interactionPrompt is null)
        {
            return;
        }

        // ACT1-UI.3: prompt type and colour come from the shared theme tokens.
        UrmanUiTheme.ApplyWorldText(_interactionPrompt, settings, UrmanUiTheme.Size.WorldPrompt);
        _interactionPrompt.SetMeta("accessibilityTextScale", settings.TextScale);
        _interactionPrompt.SetMeta("accessibilityHighContrast", settings.HighContrast);
        _interactionPrompt.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
        PainterlyMaterialLibrary.SetWindMotion(!settings.ReducedMotion);
        ApplyRemarkAccessibility(settings);
        if (settings.ReducedMotion)
        {
            _interactionPromptTween?.Kill();
            _interactionPrompt.Modulate = new Color(
                1,
                1,
                1,
                string.IsNullOrEmpty(_interactionPrompt.Text) ? 0 : 1);
        }

        if (_reticle is not null)
        {
            _reticle.Color = UrmanUiTheme.ReticleColour(settings);
            _reticle.SetMeta("accessibilityHighContrast", settings.HighContrast);
        }
    }

    /// <summary>
    /// Presentation-only rescue for machines that cannot sustain the default
    /// medium profile. It is intentionally session-scoped: the player can
    /// choose a graphics preset again in Settings, and no save/narrative state
    /// is changed by the automatic guard.
    /// </summary>
    public bool ActivateLowPerformanceFallback()
    {
        if (_graphicsPreset == "low")
        {
            return false;
        }

        _graphicsPreset = "low";
        ApplyGraphicsPreset();
        return true;
    }

    public override void _Ready()
    {
        AddToGroup("player_controller");
        AddToGroup(AccessibilityPresentation.TargetGroup);
        // UIUX-008: snapshot pristine bindings before any rebind or stored
        // settings can mutate the input map, so restore-defaults is honest.
        InputBindingService.InitializeDefaults();
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _interactionRay = GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        _reticle = GetNode<ColorRect>("Hud/Reticle");
        _interactionPrompt = GetNode<Label>("Hud/InteractionPrompt");
        // Keep the bottom safe margin when a result wraps to several lines.
        // Default downward growth let the final action line leave a 720p view.
        _interactionPrompt.GrowVertical = Control.GrowDirection.Begin;
        _interactionPrompt.VerticalAlignment = VerticalAlignment.Bottom;
        RefreshInteractionHints();
        // Eyes sit in front of the neck, inside the existing head capsule.
        // Keeping them over the hip axis makes even a correctly shaped coat
        // hide both boots when the player looks down. Stance and carried-object
        // movement share this head anchor, including save/vehicle restoration.
        _head.Position += Vector3.Forward * .18f;
        _headBasePosition = _head.Position;
        InitializeStance();
        InitializeStepMotion();
        InitializeVisibleBody();
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity").AsDouble();
        _camera.Fov = 75;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        if (OS.GetCmdlineArgs().Contains("--urman-safe-mode", StringComparer.Ordinal))
        {
            _graphicsPreset = "low";
        }
        ApplyGraphicsPreset();
        // UIUX-007: user preferences survive cold launches independently of
        // any story save slot.
        if (UserSettingsStore.TryLoad() is { } storedSettings)
        {
            ApplySettings(storedSettings);
        }
        AccessibilityPresentation.ApplyToTree(GetTree(), _accessibility);
    }

    public PlayerTransform CapturePortableTransform() => new(
        new(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z),
        new(_pitch, RotationDegrees.Y, 0));

    public void ApplyPortableTransform(PlayerTransform transform)
    {
        ReleaseLadderForPlacement();
        ResetStepMotion();
        PresentationTransformRevision++;
        GlobalPosition = new Vector3(
            (float)transform.Position.X,
            (float)transform.Position.Y,
            (float)transform.Position.Z);
        RotationDegrees = new Vector3(0, (float)transform.RotationDegrees.Y, 0);
        _pitch = Mathf.Clamp((float)transform.RotationDegrees.X, -82f, 82f);
        _head.RotationDegrees = new Vector3(_pitch, 0, 0);
        _head.Position = _headBasePosition;
        _headBobPhase = 0;
        Velocity = Vector3.Zero;
        // Start small until the destination's restored collision has reached
        // the physics server. A save underneath a deck must not stand up inside it.
        RestoreStanceAtDestination();
        ResetVisibleBodyMotion();
    }

    /// <summary>
    /// Applies the presentation-facing transform used when a compact zone
    /// transition places the player at a new doorway or route entrance. The
    /// destination owns the yaw so a player is not silently turned back toward
    /// the door they just used; narrative state and the saved transform remain
    /// owned by RuntimeBridge/SaveGameV3.
    /// </summary>
    public void ApplyZoneSpawn(Vector3 position, float yawDegrees)
    {
        ApplyPortableTransform(new PlayerTransform(
            new(position.X, position.Y, position.Z),
            new(0, yawDegrees, 0)));
    }

    /// <summary>
    /// Test-only look setter for Godot smoke scenes that have no real mouse
    /// device. It deliberately leaves position, velocity and collision state
    /// untouched; production input remains the only player-facing look path.
    /// </summary>
    internal void ApplySmokeLook(float pitchDegrees, float yawDegrees)
    {
        RotationDegrees = new Vector3(0, yawDegrees, 0);
        _pitch = Mathf.Clamp(pitchDegrees, -82f, 82f);
        _head.RotationDegrees = new Vector3(_pitch, 0, 0);
    }

    public GameSettingsSnapshot CaptureSettings() => new(
        _camera.Fov,
        MouseSensitivity,
        MotionBlur: _motionBlur,
        HeadBob: _headBob,
        GraphicsPreset: _graphicsPreset,
        InputDevice: _inputDevice,
        InputBindings: InputBindingService.Capture())
    {
        Accessibility = _accessibility,
        TatarLanguageLevel = TatarLanguageLevel
    };

    public void ApplySettings(GameSettingsSnapshot settings)
    {
        var tatarLanguageLevel = NormalizeTatarLanguageLevel(settings.TatarLanguageLevel);
        _camera.Fov = (float)Mathf.Clamp(settings.FieldOfView, 65, 90);
        MouseSensitivity = (float)settings.MouseSensitivity;
        _motionBlur = settings.MotionBlur;
        _headBob = settings.HeadBob;
        _graphicsPreset = settings.GraphicsPreset;
        _inputDevice = settings.InputDevice;
        // ACT1-LANG.5: this is a profile choice only. RuntimeBridge reads it
        // while creating the next New Game; applying settings never reseeds
        // the vocabulary in the active session.
        TatarLanguageLevel = tatarLanguageLevel;
        _accessibility = settings.Accessibility ?? AccessibilitySettingsSnapshot.Default;
        InputBindingService.Apply(settings.InputBindings);
        RefreshInteractionHints();
        ApplyGraphicsPreset();
        AccessibilityPresentation.ApplyToTree(GetTree(), _accessibility);
        // UIUX-007: the latest applied preferences persist for the next cold
        // launch, independent of any story save slot.
        UserSettingsStore.Save(settings with { TatarLanguageLevel = tatarLanguageLevel });
    }

    private static string NormalizeTatarLanguageLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "some" => "some",
        "fluent" => "fluent",
        _ => "none"
    };

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_sessionTransition) return;
        if (inputEvent is InputEventJoypadButton
            || inputEvent is InputEventJoypadMotion joypadMotion && Math.Abs(joypadMotion.AxisValue) >= 0.18f)
        {
            if (_inputDevice != "gamepad")
            {
                // AUDIO-004 diagnostics: log the switch so spurious gamepad
                // events are traceable in headless evidence runs.
                GD.Print($"input-device: gamepad via {inputEvent.AsText()} from {inputEvent.Device}");
            }

            _inputDevice = "gamepad";
        }
        else if (inputEvent is InputEventKey or InputEventMouseButton or InputEventMouseMotion)
        {
            _inputDevice = "keyboard-mouse";
        }

        if (inputEvent is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured && !_modalOpen && !VehicleControlled)
        {
            // Captured aiming uses physical screen motion. Relative is scaled
            // by the viewport stretch and changes sensitivity with resolution.
            RotateView(-mouseMotion.ScreenRelative.X * MouseSensitivity, -mouseMotion.ScreenRelative.Y * MouseSensitivity);
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        // RuntimeBridge may need multiple physics frames to project saved world
        // contacts. Hold the exact supplied pose until that transaction finishes.
        if (_sessionTransition)
        {
            Velocity = Vector3.Zero;
            IsSprinting = false;
            SetInteractionPrompt(string.Empty);
            return;
        }
        // The vehicle owns its seat transform and camera while occupied. Running
        // the pedestrian world clamp here would drag a moving seat onto terrain.
        if (VehicleControlled)
        {
            Velocity = Vector3.Zero;
            SetInteractionPrompt(string.Empty);
            return;
        }
        // The world guard runs before the modal gate: a modal (dialogue, menu,
        // ending) suspends movement, and a player parked outside the world during
        // one of those must still be brought back instead of waiting for input.
        ClampToAuthoredWorld();
        ResolveRestoredStance();
        if (_modalOpen)
        {
            Velocity = Vector3.Zero;
            UpdateHeadBob(delta, moving: false);
            UpdateVisibleBody(delta, moving: false);
            SetInteractionPrompt(string.Empty);
            return;
        }

        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        RotateView(-look.X * GamepadLookSpeed * (float)delta, -look.Y * GamepadLookSpeed * (float)delta);

        if (UpdateLadderMotion(delta))
        {
            UpdateVisibleBody(delta, moving: false);
            return;
        }

        if (!_worldInteractionNeedsRelease && Input.IsActionJustPressed("crouch")) ToggleCrouch();

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        // GetVector already limits diagonals. Keep analogue stick strength so
        // a small deflection can also make a small movement near an address.
        var direction = Transform.Basis * new Vector3(input.X, 0, input.Y);
        IsSprinting = !IsCrouching && !_worldInteractionNeedsRelease && Input.IsActionPressed("sprint")
            && input.LengthSquared() > .01f;
        var speed = IsCrouching ? WalkSpeed * .58f : IsSprinting ? WalkSpeed * SprintMultiplier : WalkSpeed;
        Velocity = new Vector3(direction.X * speed, Velocity.Y, direction.Z * speed);
        TryJumpFromGround();
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - _gravity * (float)delta, Velocity.Z);
        }

        _carryCoordinator ??= GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (_carryCoordinator is not null)
        {
            var requested = Velocity;
            Velocity = _carryCoordinator.ConstrainCarriedMovement(Velocity, (float)delta);
            _carryCoordinator.ObserveCarriedMovement(requested, Velocity);
        }
        if (Velocity.Y > .05f || !TryWalkUpStep((float)delta)) MoveAndSlide();
        ClampToAuthoredWorld();
        UpdateHeadBob(delta, new Vector2(Velocity.X, Velocity.Z).LengthSquared() > 0.01f && IsOnFloor());
        UpdateVisibleBody(delta, input.LengthSquared() > 0.01f);
        UpdateInteraction();
    }

    /// <summary>
    /// Keeps the player inside the authored terrain window and on its surface.
    /// That window is the only place with walkable collision, so without this a
    /// curious player walks off the grid, passes the end of the ground and falls
    /// through the world. The same guard recovers from any unforeseen hole
    /// instead of leaving a player falling forever.
    /// </summary>
    /// <summary>
    /// A presentation location outside the village terrain (the prologue's deep
    /// forest) owns its own walkable window while it is active: it returns the
    /// clamped position and the ground height under it. Null restores the
    /// village terrain guard.
    /// </summary>
    internal static Func<Vector3, (Vector3 Position, float Ground)>? DetachedWorldGuard { get; set; }

    /// <summary>
    /// A sealed interior volume authored below the terrain (the house wing) owns
    /// its own floor: inside it the terrain fall guard must not lift the player.
    /// </summary>
    internal static Func<Vector3, bool>? SealedInteriorVolume { get; set; }

    private void ClampToAuthoredWorld()
    {
        const float edgeMargin = 3f;
        var position = GlobalPosition;
        if (SealedInteriorVolume?.Invoke(position) == true) return;
        if (DetachedWorldGuard is { } detached)
        {
            var (inside, detachedGround) = detached(position);
            if (inside.X != position.X || inside.Z != position.Z)
            {
                GlobalPosition = inside with { Y = position.Y };
                EdgeClamps++;
            }
            if (GlobalPosition.Y < detachedGround - FallRecoveryDepth)
            {
                GlobalPosition = GlobalPosition with { Y = detachedGround + 0.05f };
                Velocity = Vector3.Zero;
                FallRecoveries++;
            }
            return;
        }
        // The west mosque courtyard wall is inside the terrain, past the old 3 m clamp.
        var clampedX = Mathf.Clamp(position.X, AgentBAct1HeightField.MinX + 1.5f, AgentBAct1HeightField.MaxX - edgeMargin);
        var clampedZ = Mathf.Clamp(position.Z, AgentBAct1HeightField.MinZ + edgeMargin, AgentBAct1HeightField.MaxZ - edgeMargin);
        if (clampedX != position.X || clampedZ != position.Z)
        {
            position = new Vector3(clampedX, position.Y, clampedZ);
            GlobalPosition = position;
            EdgeClamps++;
        }

        var ground = (float)AgentBAct1HeightField.Ground(position.X, position.Z);
        if (position.Y < ground - FallRecoveryDepth)
        {
            GlobalPosition = new Vector3(position.X, ground + 0.05f, position.Z);
            Velocity = Vector3.Zero;
            FallRecoveries++;
            GD.Print(
                $"act1-fall-recovery: count={FallRecoveries} was=({position.X:0.0},{position.Y:0.0},{position.Z:0.0}) "
                + $"ground={ground:0.0}");
        }
    }

    internal void SetSessionTransition(bool active)
    {
        _sessionTransition = active;
        _worldInteractionNeedsRelease = true;
        Velocity = Vector3.Zero;
        IsSprinting = false;
        _focusedTarget = _focusCandidate = _promptTarget = null;
        _focusCandidateFrames = 0;
        SetInteractionPrompt(string.Empty);
    }

    public void SetModalOpen(bool open)
    {
        if (_modalOpen != open)
            _worldInteractionNeedsRelease = true;
        _modalOpen = open;
        RefreshInteractionHints();
        if (open)
        {
            SetInteractionPrompt(string.Empty);
        }

        Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private void RotateView(float yawDegrees, float pitchDegrees)
    {
        RotateY(Mathf.DegToRad(yawDegrees));
        _pitch = Mathf.Clamp(_pitch + pitchDegrees, -82f, 82f);
        _head.RotationDegrees = new Vector3(_pitch, 0, 0);
    }

    private void UpdateHeadBob(double delta, bool moving)
    {
        _stepEyeDrop = Math.Max(0f, _stepEyeDrop - (float)delta * 1.8f);
        var rest = _headBasePosition - Vector3.Up * _stepEyeDrop;
        if (HeadBobEnabled && moving)
        {
            _headBobPhase += (float)delta * 8.2f;
            var offset = new Vector3(
                Mathf.Cos(_headBobPhase * 0.5f) * 0.012f,
                Mathf.Sin(_headBobPhase) * 0.024f,
                0);
            _head.Position = rest + offset;
            return;
        }

        _head.Position = _head.Position.Lerp(rest, Mathf.Clamp((float)delta * 10, 0, 1));
    }

    private void ApplyGraphicsPreset()
    {
        PainterlyMaterialLibrary.SetGraphicsPreset(_graphicsPreset);
        GraphicsQuality.Apply(GetViewport(), _graphicsPreset);
    }

    private void UpdateInteraction()
    {
        if (InIntro)
        {
            _worldInteractionNeedsRelease = true;
            SetInteractionPrompt(string.Empty);
            return;
        }
        // UI accept and world interaction can share E or the gamepad A button.
        // Input's action state survives GUI event consumption. Require a neutral
        // physics frame after a modal closes before offering any world action,
        // so the closing press cannot also repair, pick up or enter a ladder.
        if (_worldInteractionNeedsRelease)
        {
            var actions = new[] { InteractionAction, "carry_use", "carry_place", "carry_rotate", "crouch", "jump", "sprint" };
            if (actions.All(action => !Input.IsActionPressed(action) && !Input.IsActionJustPressed(action)))
                _worldInteractionNeedsRelease = false;
            SetInteractionPrompt(string.Empty);
            return;
        }
        if (_carryCoordinator is null || !GodotObject.IsInstanceValid(_carryCoordinator))
            _carryCoordinator = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (OfferLadderInteraction()) return;
        if (Time.GetTicksMsec() < _traversalNoticeUntil)
        {
            // A rejected vehicle entry or a read address uses the same brief
            // notice as traversal. Refresh the target after it expires even
            // when the player has kept looking at the same door or plaque.
            _promptTarget = null;
            // A new deliberate object action may dismiss the notice immediately;
            // a failed jump must not make the player wait before setting a box down.
            if (new[] { InteractionAction, "carry_use", "carry_place", "carry_rotate" }
                .Any(action => Input.IsActionJustPressed(action)))
                _traversalNoticeUntil = 0;
            else
            {
                // A short result message must not freeze the old door's focus
                // while the player looks at another object. Otherwise the next
                // deliberate press only begins the two-frame focus change and
                // its JustPressed edge is lost. Keep passive focus current;
                // the notice still displays and no action is performed here.
                ResolveFocusedTarget(PeekRayTarget());
                SetInteractionPrompt(_traversalNotice);
                return;
            }
        }
        if (_carryCoordinator is not null && _carryCoordinator.HandlePlayerInput(this, _camera, out var carryPrompt))
        {
            _focusedTarget = null;
            _focusCandidate = null;
            _promptTarget = null;
            SetInteractionPrompt(carryPrompt);
            return;
        }
        var candidate = PeekRayTarget();
        var target = ResolveFocusedTarget(candidate);
        if (target is null)
        {
            SetInteractionPrompt(string.Empty);
            return;
        }

        var available = target.IsAvailable();
        var hint = InteractionHint;
        var completed = !available && target.GetMeta("discoveryCompleted", false).AsBool();
        if (!available && !completed)
        {
            // A shut world gate is not "nothing here": the place is authored and
            // the player is looking straight at it, so name what is missing.
            SetInteractionPrompt(target.HeldByGate
                ? target.PresentationGateHint
                : target.IsSemanticallyAvailable() && target.HasMeta("observationBlockedHint")
                    ? target.GetMeta("observationBlockedHint").AsString()
                    : string.Empty);
            _promptTarget = null;
            return;
        }
        var label = available ? target.Prompt : "Осмотрено";
        if (target != _promptTarget
            || available != _promptTargetAvailable
            || !string.Equals(hint, _promptHint, StringComparison.Ordinal)
            || !string.Equals(label, _promptLabel, StringComparison.Ordinal))
        {
            _promptTarget = target;
            _promptTargetAvailable = available;
            _promptHint = hint;
            _promptLabel = label;
            SetInteractionPrompt(completed ? label : $"{hint} {label}");
        }

        if (candidate == target && available && Input.IsActionJustPressed(InteractionAction))
        {
            target.Interact();
        }
    }

    private InteractionTarget? ResolveFocusedTarget(InteractionTarget? candidate)
    {
        if (candidate is null || !GodotObject.IsInstanceValid(candidate))
        {
            if (_focusedTarget is { } unavailable
                // A zone change frees the previous zone's targets, so the cached
                // reference can be disposed before this branch reads it.
                && GodotObject.IsInstanceValid(unavailable)
                && string.IsNullOrEmpty(unavailable.JournalEntryId)
                && !unavailable.IsAvailable()
                && unavailable.GetMeta("discoveryCompleted", false).AsBool()
                && IsStillFocused(unavailable))
            {
                _focusCandidate = null;
                _focusCandidateFrames = 0;
                return unavailable;
            }

            _focusedTarget = null;
            _focusCandidate = null;
            _focusCandidateFrames = 0;
            return null;
        }

        if (_focusedTarget is null || !GodotObject.IsInstanceValid(_focusedTarget))
        {
            _focusedTarget = candidate;
            _focusCandidate = null;
            _focusCandidateFrames = 0;
            return _focusedTarget;
        }

        if (candidate == _focusedTarget)
        {
            _focusCandidate = null;
            _focusCandidateFrames = 0;
            return _focusedTarget;
        }

        if (candidate != _focusCandidate)
        {
            _focusCandidate = candidate;
            _focusCandidateFrames = 1;
        }
        else
        {
            _focusCandidateFrames++;
        }

        if (_focusCandidateFrames >= FocusSwitchFrames)
        {
            _focusedTarget = candidate;
            _focusCandidate = null;
            _focusCandidateFrames = 0;
        }

        return _focusedTarget;
    }

    private bool IsStillFocused(InteractionTarget target)
    {
        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree())
        {
            return false;
        }

        var toTarget = target.GlobalPosition - _camera.GlobalPosition;
        var maxDistance = _interactionRay.TargetPosition.Length() + 0.3f;
        if (toTarget.LengthSquared() > maxDistance * maxDistance)
        {
            return false;
        }

        return (-_camera.GlobalTransform.Basis.Z).Normalized().Dot(toTarget.Normalized()) >= 0.9f;
    }

    private void SetInteractionPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _promptTarget = null;
            _promptTargetAvailable = false;
            _promptHint = string.Empty;
            _promptLabel = string.Empty;
        }

        if (string.Equals(_interactionPrompt.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        var wasVisible = !string.IsNullOrEmpty(_interactionPrompt.Text);
        var isVisible = !string.IsNullOrEmpty(text);
        _interactionPrompt.Text = text;
        _interactionPromptTween?.Kill();

        if (!isVisible)
        {
            _interactionPrompt.Modulate = new Color(1, 1, 1, 0);
            return;
        }

        if (ReducedMotion || wasVisible)
        {
            _interactionPrompt.Modulate = new Color(1, 1, 1, 1);
            return;
        }

        _interactionPrompt.Modulate = new Color(1, 1, 1, 0);
        _interactionPromptTween = CreateTween();
        _interactionPromptTween.TweenProperty(
            _interactionPrompt,
            "modulate",
            new Color(1, 1, 1, 1),
            0.1f);
    }

    private string FormatInteractionHint()
    {
        return _inputDevice == "gamepad" ? _gamepadInteractionHint : _keyboardInteractionHint;
    }

    // InputMap has no change signal; existing settings/modal boundaries are
    // the invalidation points, keeping the physics prompt path allocation-free.
    private void RefreshInteractionHints()
    {
        _keyboardInteractionHint = InputBindingService.ActionHint(InteractionAction, false);
        _gamepadInteractionHint = InputBindingService.ActionHint(InteractionAction, true);
    }
}
