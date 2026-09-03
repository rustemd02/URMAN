using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class FirstPersonController : CharacterBody3D
{
    [Export(PropertyHint.Range, "1,8,0.1")]
    public float WalkSpeed { get; set; } = 3.4f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float MouseSensitivity { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "30,240,1")]
    public float GamepadLookSpeed { get; set; } = 105f;

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private RayCast3D _interactionRay = null!;
    private Label _interactionPrompt = null!;
    private float _pitch;
    private float _gravity;
    private bool _modalOpen;
    private bool _motionBlur;
    private bool _headBob;
    private string _graphicsPreset = "medium";
    private string _inputDevice = "keyboard-mouse";
    private AccessibilitySettingsSnapshot _accessibility = AccessibilitySettingsSnapshot.Default;
    private Vector3 _headBasePosition;
    private float _headBobPhase;

    public bool ModalOpen => _modalOpen;

    public string CurrentInputDevice => _inputDevice;

    public string InteractionHint => _inputDevice == "gamepad" ? "[A]" : "[E]";

    public string GraphicsPreset => _graphicsPreset;

    public bool HeadBobEnabled => _headBob && !_accessibility.ReducedMotion;

    public AccessibilitySettingsSnapshot Accessibility => _accessibility;

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
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _interactionRay = GetNode<RayCast3D>("Head/Camera3D/InteractionRay");
        _interactionPrompt = GetNode<Label>("Hud/InteractionPrompt");
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
            _interactionPrompt.Text = string.Empty;
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
        var target = _interactionRay.IsColliding() ? _interactionRay.GetCollider() as InteractionTarget : null;
        if (target is not null && !target.IsAvailable())
        {
            target = null;
        }
        _interactionPrompt.Text = target is null ? string.Empty : $"{InteractionHint} {target.Prompt}";
        if (target is not null && Input.IsActionJustPressed("interact"))
        {
            target.Interact();
        }
    }
}
