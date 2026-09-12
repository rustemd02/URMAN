using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class FirstPersonController : CharacterBody3D, IAccessibilitySettingsTarget
{
    private const string InteractionAction = "interact";
    private const string UnavailablePrompt = "Сейчас недоступно";
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
    private bool _motionBlur;
    private bool _headBob;
    private string _graphicsPreset = "medium";
    private string _inputDevice = "keyboard-mouse";
    private string _keyboardInteractionHint = "[E]";
    private string _gamepadInteractionHint = "[A]";
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private Vector3 _headBasePosition;
    private float _headBobPhase;

    public bool ModalOpen => _modalOpen;
    internal int PresentationTransformRevision { get; private set; }

    public string CurrentInputDevice => _inputDevice;

    public string InteractionHint => FormatInteractionHint();

    public string GraphicsPreset => _graphicsPreset;

    public bool HeadBobEnabled => _headBob && !_accessibility.ReducedMotion;

    /// <summary>UIUX-010: presentation owners gate nonessential motion on this.</summary>
    public bool ReducedMotion => _accessibility.ReducedMotion;

    public AccessibilitySettingsSnapshot Accessibility => _accessibility;

    public void ApplyAccessibilitySettings(AccessibilitySettingsSnapshot settings)
    {
        _accessibility = settings;
        if (_interactionPrompt is null)
        {
            return;
        }

        var textScale = Mathf.Clamp((float)settings.TextScale, 0.8f, 1.6f);
        _interactionPrompt.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(24f * textScale));
        _interactionPrompt.AddThemeColorOverride(
            "font_color",
            settings.HighContrast ? Colors.White : new Color(0.94f, 0.89f, 0.76f));
        _interactionPrompt.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _interactionPrompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _interactionPrompt.AddThemeConstantOverride("shadow_offset_x", settings.HighContrast ? 3 : 2);
        _interactionPrompt.AddThemeConstantOverride("shadow_offset_y", settings.HighContrast ? 3 : 2);
        _interactionPrompt.AddThemeConstantOverride("outline_size", settings.HighContrast ? 5 : 4);
        _interactionPrompt.SetMeta("accessibilityTextScale", settings.TextScale);
        _interactionPrompt.SetMeta("accessibilityHighContrast", settings.HighContrast);
        _interactionPrompt.SetMeta("accessibilityReducedMotion", settings.ReducedMotion);
        PainterlyMaterialLibrary.SetWindMotion(!settings.ReducedMotion);
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
            _reticle.Color = settings.HighContrast
                ? Colors.White
                : new Color(0.94f, 0.89f, 0.76f, 0.96f);
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
        RefreshInteractionHints();
        _headBasePosition = _head.Position;
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
        Accessibility = _accessibility
    };

    public void ApplySettings(GameSettingsSnapshot settings)
    {
        _camera.Fov = (float)Mathf.Clamp(settings.FieldOfView, 65, 90);
        MouseSensitivity = (float)settings.MouseSensitivity;
        _motionBlur = settings.MotionBlur;
        _headBob = settings.HeadBob;
        _graphicsPreset = settings.GraphicsPreset;
        _inputDevice = settings.InputDevice;
        _accessibility = settings.Accessibility ?? AccessibilitySettingsSnapshot.Default;
        InputBindingService.Apply(settings.InputBindings);
        RefreshInteractionHints();
        ApplyGraphicsPreset();
        AccessibilityPresentation.ApplyToTree(GetTree(), _accessibility);
        // UIUX-007: the latest applied preferences persist for the next cold
        // launch, independent of any story save slot.
        UserSettingsStore.Save(settings);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
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

        if (inputEvent is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured && !_modalOpen)
        {
            RotateView(-mouseMotion.Relative.X * MouseSensitivity, -mouseMotion.Relative.Y * MouseSensitivity);
        }

    }

    public override void _PhysicsProcess(double delta)
    {
        if (_modalOpen)
        {
            Velocity = Vector3.Zero;
            UpdateHeadBob(delta, moving: false);
            SetInteractionPrompt(string.Empty);
            return;
        }

        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        RotateView(-look.X * GamepadLookSpeed * (float)delta, -look.Y * GamepadLookSpeed * (float)delta);

        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        var direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        Velocity = new Vector3(direction.X * WalkSpeed, Velocity.Y, direction.Z * WalkSpeed);
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - _gravity * (float)delta, Velocity.Z);
        }

        MoveAndSlide();
        UpdateHeadBob(delta, input.LengthSquared() > 0.01f && IsOnFloor());
        UpdateInteraction();
    }

    public void SetModalOpen(bool open)
    {
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
        if (HeadBobEnabled && moving)
        {
            _headBobPhase += (float)delta * 8.2f;
            var offset = new Vector3(
                Mathf.Cos(_headBobPhase * 0.5f) * 0.012f,
                Mathf.Sin(_headBobPhase) * 0.024f,
                0);
            _head.Position = _headBasePosition + offset;
            return;
        }

        _head.Position = _head.Position.Lerp(_headBasePosition, Mathf.Clamp((float)delta * 10, 0, 1));
    }

    private void ApplyGraphicsPreset()
    {
        PainterlyMaterialLibrary.SetGraphicsPreset(_graphicsPreset);
        var viewport = GetViewport();
        viewport.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
        (viewport.Scaling3DScale, viewport.Msaa3D) = _graphicsPreset switch
        {
            "low" => (0.75f, Viewport.Msaa.Disabled),
            "high" => (1.0f, Viewport.Msaa.Msaa4X),
            _ => (0.9f, Viewport.Msaa.Msaa2X)
        };
    }

    private void UpdateInteraction()
    {
        var candidate = _interactionRay.IsColliding()
            ? _interactionRay.GetCollider() as InteractionTarget
            : null;
        var target = ResolveFocusedTarget(candidate);
        if (target is null)
        {
            SetInteractionPrompt(string.Empty);
            return;
        }

        var available = target.IsAvailable();
        var hint = InteractionHint;
        var completed = !available && target.GetMeta("discoveryCompleted", false).AsBool();
        var label = available ? target.Prompt : completed ? "Осмотрено" : UnavailablePrompt;
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
                && string.IsNullOrEmpty(unavailable.JournalEntryId)
                && !unavailable.IsAvailable()
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
        var keyboardHint = "[Key]";
        var gamepadHint = "[Gamepad]";
        if (!InputMap.HasAction(InteractionAction))
        {
            _keyboardInteractionHint = keyboardHint;
            _gamepadInteractionHint = gamepadHint;
            return;
        }

        InputEventKey? keyboard = null;
        InputEventJoypadButton? gamepadButton = null;
        InputEventJoypadMotion? gamepadAxis = null;
        foreach (var inputEvent in InputMap.ActionGetEvents(InteractionAction))
        {
            switch (inputEvent)
            {
                case InputEventKey key when keyboard is null:
                    keyboard = key;
                    break;
                case InputEventJoypadButton button when gamepadButton is null:
                    gamepadButton = button;
                    break;
                case InputEventJoypadMotion axis when gamepadAxis is null:
                    gamepadAxis = axis;
                    break;
            }
        }

        if (keyboard is not null)
        {
            var keyLabel = OS.GetKeycodeString(keyboard.PhysicalKeycode);
            if (!string.IsNullOrWhiteSpace(keyLabel) && keyLabel != nameof(Key.None))
            {
                keyboardHint = $"[{keyLabel}]";
            }
        }

        if (gamepadButton is not null)
        {
            gamepadHint = $"[{gamepadButton.ButtonIndex}]";
        }
        else if (gamepadAxis is not null)
        {
            gamepadHint = "[Axis]";
        }

        _keyboardInteractionHint = keyboardHint;
        _gamepadInteractionHint = gamepadHint;
    }
}
