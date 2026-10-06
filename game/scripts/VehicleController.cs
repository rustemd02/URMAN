using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Godot;

/// <summary>
/// One kinematic owner for each vehicle. Steering is bicycle-model motion with
/// swept collision; parked vehicles never receive gravity impulses from players.
/// The horse and shafts belong to the same chassis, avoiding unstable joints.
/// </summary>
public partial class VehicleController : CharacterBody3D
{
    private VehicleFleet _fleet = null!;
    private VehicleVisualFactory.Visual _visual = null!;
    private Camera3D _camera = null!;
    private Node3D _look = null!;
    private InteractionTarget _entry = null!;
    private VehicleMechanicalAudio _mechanical = null!;
    private VehicleMechanics? _mechanics;
    private float _steering;
    private float _wheelPhase;
    private float _pitch;
    private float _lookYaw;
    private float _engineWarmup;
    private bool _controlsNeedRelease;
    private bool _configured;
    private float _noticeSeconds;
    private string _notice = string.Empty;
    private double _lastDirtyTime;
    private double _driveTime;
    private CapsuleShape3D _exitShape = null!;
    private float _savedYawDegrees;
    private Basis _savedYawBasis;
    private bool _hasSavedYaw;
    private bool _collisionStopDiagnostics;
    private JsonObject? _firstCollisionStop;
    // Writes driven only by player input are skipped while the value is unchanged.
    private bool? _lampsVisible;
    // The authored visual is built once and its wheel lists never change, so the
    // per-tick wheel pass no longer walks IReadOnlyList through LINQ/boxed
    // enumerators: index -> wheel plus its precomputed front-wheel flag.
    private Node3D[] _visualWheels = Array.Empty<Node3D>();
    private bool[] _visualFrontWheel = Array.Empty<bool>();
    // The horse cart's authored driver figure, resolved once when the visual is
    // built (it does not exist on the other vehicles) instead of per tick.
    private Node3D? _cartDriverFigure;
    private bool? _cartDriverVisible;
    private static readonly string[] ControlReleaseActions = {"interact","carry_use","carry_rotate","carry_place","crouch",
        "jump","move_forward","move_backward","move_left","move_right"};

    public VehicleDefinition Definition { get; private set; } = null!;
    public FirstPersonController? Driver { get; private set; }
    public VehicleRadioPlayer? Radio { get; private set; }
    public bool EngineRunning { get; private set; }
    public bool ParkingBrake { get; private set; } = true;
    public bool Headlights { get; private set; }
    /// <summary>
    /// High-beam toggle state. Presentation only: the visual lane drives the
    /// actual light nodes (energy/range/angle). Requires Headlights to matter.
    /// </summary>
    public bool HighBeams { get; private set; }
    /// <summary>
    /// Windscreen wiper toggle state. Presentation only, no physics effect; the
    /// visual lane owns the wiper meshes and the windscreen snow.
    /// </summary>
    public bool Wipers { get; private set; }
    public float Speed { get; private set; }
    public float TotalTravelMetres { get; private set; }
    public HorseDisposition HorseState { get; private set; }
    public string LastRefusal { get; private set; } = string.Empty;
    public Camera3D VehicleCamera => _camera;
    public InteractionTarget EntryTarget => _entry;
    public int CollisionStops { get; private set; }
    internal void SetCollisionStopDiagnostics(bool enabled)
    {
        _collisionStopDiagnostics = enabled;
        if (enabled) _firstCollisionStop = null;
    }
    internal JsonObject? DescribeFirstCollisionStop() => _firstCollisionStop?.DeepClone() as JsonObject;
    public bool PlacementAvailable { get; private set; }
    public string PlacementFailure { get; private set; } = string.Empty;

    /// <summary>
    /// Fired once per real collision stop with the pre-stop speed and the first
    /// contacted collider. Listeners decide what counts as an impact; this
    /// remains a pure observation of the existing MoveAndSlide result.
    /// </summary>
    public event Action<VehicleController, float, Node?>? HardStop;
    internal Vector2 DriverLookAngles=>new(_lookYaw,_pitch);
    internal JsonObject DescribeDriverLookInput()=>new(){
        ["storedYaw"]=_lookYaw,["storedPitch"]=_pitch,["projectedRotation"]=_look.RotationDegrees.ToString(),
        ["hasDriver"]=Driver is not null,["driverModal"]=Driver?.ModalOpen,["vehicleControlled"]=Driver?.VehicleControlled,
        ["fleetSuspended"]=_fleet.Suspended,["placementAvailable"]=PlacementAvailable,
        ["treePaused"]=GetTree().Paused,["ownerCanProcess"]=CanProcess(),
        ["controlsNeedRelease"]=_controlsNeedRelease,["mouseMode"]=Input.MouseMode.ToString(),
        ["focus"]=DisplayServer.WindowIsFocused(),["physicsFrame"]=Engine.GetPhysicsFrames(),["processFrame"]=Engine.GetProcessFrames()};

    public void Configure(VehicleFleet fleet, VehicleDefinition definition)
    {
        if (_configured) throw new InvalidOperationException("Vehicle is already configured.");
        _configured = true;
        _fleet = fleet; Definition = definition; Name = definition.Id;
        CollisionLayer = 1; CollisionMask = 3;
        FloorSnapLength = .42f; FloorMaxAngle = Mathf.DegToRad(25f); SafeMargin = .012f;
        FloorStopOnSlope = true; FloorConstantSpeed = true; MaxSlides = 4;
        _exitShape = new CapsuleShape3D { Radius = .35f, Height = 1.8f };
        _visual = VehicleVisualFactory.Build(definition); AddChild(_visual.Root);
        _visualWheels = new Node3D[_visual.Wheels.Count];
        _visualFrontWheel = new bool[_visualWheels.Length];
        for (var index = 0; index < _visualWheels.Length; index++)
        {
            _visualWheels[index] = _visual.Wheels[index];
            _visualFrontWheel[index] = _visual.FrontWheels.Contains(_visualWheels[index]);
        }
        _cartDriverFigure = _visual.Root.GetNodeOrNull("CartDriver") as Node3D;
        BuildCompoundCollision();
        BuildSteeringCollision();
        BuildSupportTopology();
        _look = new Node3D { Name = "DriverLook", Position = definition.Seat + Vector3.Up * .75f };
        AddChild(_look);
        _camera = new Camera3D { Name = "VehicleCamera", Fov = 75f, Near = .045f, Current = false };
        _look.AddChild(_camera);
        _entry = new InteractionTarget { Name = "DriverDoor", InteractionId = "vehicle/enter/" + definition.Id,
            Prompt = definition.Kind == VehicleKind.HorseCart ? "Сесть на телегу" : "Сесть: " + definition.DisplayName,
            CollisionLayer = 4, CollisionMask = 0,
            Position = new(-definition.HullSize.X * .5f - .10f, definition.Kind == VehicleKind.HorseCart ? 1.0f : .97f,
                definition.Kind == VehicleKind.HorseCart ? .39f : .15f),
            PresentationRepeatAvailable = () => Driver is null,
            PresentationRepeat = () => _fleet.TryEnter(this) };
        _entry.SetMeta("vehicleId", definition.Id);
        _entry.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.22f, .76f, .78f) } });
        AddChild(_entry);
        _mechanical = new VehicleMechanicalAudio { Name = "MechanicalAudio" }; AddChild(_mechanical);
        // The Niva gets the real drivetrain (vendored GEVP-derived model); the
        // other authored vehicles keep their existing kinematic character.
        if (definition.Kind == VehicleKind.Niva && definition.Mechanics is { } mechanics)
            _mechanics = new VehicleMechanics(mechanics, definition.WheelBase, definition.HullSize.X,
                definition.WheelRadius, definition.MaxForwardSpeed, definition.ReverseSpeed);
        if (definition.HasRadio)
        {
            Radio = new VehicleRadioPlayer { Name = "Radio" }; AddChild(Radio);
            Radio.Position = new(.12f, 1.05f, -.39f);
        }
        SetMeta("vehicleId", definition.Id);
        SetMeta("vehicleKind", definition.Kind.ToString());
        SetMeta("stateOwner", "RuntimeBridge world.props; VehicleFleet projects it");
        SetMeta("physicsOwner", "CharacterBody3D; constrained bicycle steering");
    }

    public override void _Ready()
    {
        if (!_configured) throw new InvalidOperationException("Vehicle must be configured before entering the tree.");
        AddToGroup("act1_vehicles"); ResetAuthored();
    }

    public void ResetAuthored()
    {
        ReleaseForSessionBoundary();
        SetPlacementAvailability(false,"Parking has not been checked for this session.");
        GlobalPosition = Definition.Spawn;
        RotationDegrees = new(0, Definition.YawDegrees, 0);
        RememberSavedYaw(Definition.YawDegrees);
        Velocity = Vector3.Zero; Speed = 0; EngineRunning = false; ParkingBrake = true;
        Headlights = false; HighBeams = false; Wipers = false;
        TotalTravelMetres = 0; HorseState = HorseDisposition.Calm;
        _steering = _pitch = _lookYaw = _engineWarmup = _wheelPhase = _motorcycleLean = 0;
        _mechanics?.Reset();
        _acceptedHorsePose = _pendingHorsePose = null;
        _rejectedHorsePose = null; _horseProjectionFailure = string.Empty;
        LastRefusal = string.Empty; _noticeSeconds = 0;
        Radio?.Restore(null);
        UpdateVisuals(0);
    }

    internal bool Enter(FirstPersonController player, bool restore = false)
    {
        if (!PlacementAvailable)
        { player.NotifyTraversal("Транспорт зажат. Сейчас сесть в него нельзя."); return false; }
        if (Driver is not null) return false;
        if (!restore && !player.TryBeginVehicleControl(out var reason))
        { player.NotifyTraversal(reason); return false; }
        if (restore) player.SetVehicleControl(true);
        Driver = player; _controlsNeedRelease = true;
        _pitch = _lookYaw = 0;
        player.GlobalPosition = ToGlobal(Definition.Seat);
        _camera.Fov = (float)player.CaptureSettings().FieldOfView;
        _camera.MakeCurrent(); _entry.CollisionLayer = 0;
        _fleet.MarkDirty(); return true;
    }

    public bool TryExit()
    {
        if (Driver is not { } player) return false;
        if (Math.Abs(Speed) > .25f)
        { Notice("Сначала остановитесь."); return false; }
        if (!TryFindSafeExit(out var feet))
        { Notice("Рядом негде встать. Переставьте транспорт."); return false; }
        // An empty vehicle is parked; the ignition and radio retain their actual state.
        ParkingBrake = true; Speed = 0; Velocity = Vector3.Zero; _mechanics?.Block();
        Driver = null;
        SyncCartDriverFigure();
        player.ApplyZoneSpawn(feet, RotationDegrees.Y);
        player.SetVehicleControl(false);
        _entry.CollisionLayer = _entry.ActiveCollisionLayer;
        UiFoley.PlayWorld(this, _entry.GlobalPosition, "metal_rattle");
        _fleet.MarkDirty(); return true;
    }

    internal void ReleaseForSessionBoundary()
    {
        // A world replacement releases a live player. During whole-scene
        // teardown the player may already have left the tree; restoring its
        // camera/stance then would query a nonexistent global transform.
        if (Driver is { } player && GodotObject.IsInstanceValid(player) && player.IsInsideTree())
            player.SetVehicleControl(false);
        Driver = null;
        if (_entry is not null) _entry.CollisionLayer = _entry.ActiveCollisionLayer;
        Speed = 0; Velocity = Vector3.Zero;
        _mechanical?.ResetForSession();
        _mechanics?.Reset();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Driver is not { ModalOpen: false } player || _fleet.Suspended
            || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (inputEvent is InputEventMouseMotion motion)
        {
            _lookYaw = Mathf.Clamp(_lookYaw - motion.ScreenRelative.X * player.MouseSensitivity, -125f, 125f);
            _pitch = Mathf.Clamp(_pitch - motion.ScreenRelative.Y * player.MouseSensitivity, -67f, 65f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_configured) return;
        var dt = Math.Min((float)delta, .05f);
        _noticeSeconds = Math.Max(0, _noticeSeconds - dt);
        Radio?.SetPaused(_fleet.Suspended || !PlacementAvailable);
        if (_fleet.Suspended || !PlacementAvailable)
        {
            if (!_fleet.Suspended && _horseProjectionFailure.Length != 0
                && Driver is { ModalOpen: false } && Input.IsActionJustPressed("interact")) TryExit();
            _controlsNeedRelease = true;
            _mechanical.SetState(Definition.Kind, EngineRunning, 0, 0, true);
            _mechanics?.Idle(EngineRunning, dt);
            return;
        }
        if (Driver is not { } player)
        {
            // Parked pose belongs to the snapshot; no drift or uncontrolled animal motion.
            Velocity = Vector3.Zero;
            _mechanical.SetState(Definition.Kind, EngineRunning, 0, 0, false);
            _mechanics?.Idle(EngineRunning, dt);
            UpdateVisuals(dt); return;
        }
        if (player.ModalOpen)
        {
            Speed = 0; Velocity = Vector3.Zero; _controlsNeedRelease = true;
            _mechanical.SetState(Definition.Kind, EngineRunning, 0, 0, true);
            _mechanics?.Idle(EngineRunning, dt);
            return;
        }
        if (_controlsNeedRelease)
        {
            // Same disjunction as All(!HasAction || (!Pressed && !JustPressed)):
            // an available action that is held or pressed right now blocks the
            // release, and HasAction still short-circuits before any input read.
            var released = true;
            for (var index = 0; index < ControlReleaseActions.Length; index++)
            {
                var action = ControlReleaseActions[index];
                if (!InputMap.HasAction(action)) continue;
                if (!Input.IsActionPressed(action) && !Input.IsActionJustPressed(action)) continue;
                released = false; break;
            }
            if (released) _controlsNeedRelease = false;
            _mechanics?.Idle(EngineRunning, dt);
            AttachDriver(); UpdateVisuals(dt); return;
        }
        if (Input.IsActionJustPressed("interact")) { TryExit(); if(Driver is null)return; }
        if (Input.IsActionJustPressed("carry_use"))
        {
            EngineRunning = !EngineRunning; _engineWarmup = EngineRunning ? .55f : 0;
            if (EngineRunning) ParkingBrake = false;
            UiFoley.PlayWorld(this, GlobalPosition + Vector3.Up, "metal_rattle");
            _fleet.MarkDirty();
        }
        if (Input.IsActionJustPressed("carry_rotate") && Definition.Kind != VehicleKind.HorseCart)
        { Headlights = !Headlights; _fleet.MarkDirty(); }
        if (InputMap.HasAction("vehicle_high_beam") && Input.IsActionJustPressed("vehicle_high_beam")
            && Definition.Kind != VehicleKind.HorseCart)
        { HighBeams = !HighBeams; _fleet.MarkDirty(); }
        if (InputMap.HasAction("vehicle_wipers") && Input.IsActionJustPressed("vehicle_wipers")
            && Definition.Kind != VehicleKind.HorseCart)
        { Wipers = !Wipers; _fleet.MarkDirty(); }
        if (Input.IsActionJustPressed("carry_place") && Radio is not null)
        { Radio.SetEnabled(!Radio.Enabled); _fleet.MarkDirty(); }
        if (Input.IsActionJustPressed("radio_station") && Radio is not null && Radio.NextStation())
        {
            // Retuning is its own mechanical click. The new frequency is readable
            // on the dashboard, so the switch does not interrupt the control hint.
            UiFoley.PlayWorld(this, GlobalPosition + Vector3.Up, "ui_click");
            _fleet.MarkDirty();
        }
        if (Input.IsActionJustPressed("crouch"))
        { ParkingBrake = !ParkingBrake; _fleet.MarkDirty(); }
        _engineWarmup = Math.Max(0, _engineWarmup - dt);
        var look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
        _lookYaw = Mathf.Clamp(_lookYaw - look.X * player.GamepadLookSpeed * dt, -125, 125);
        _pitch = Mathf.Clamp(_pitch - look.Y * player.GamepadLookSpeed * dt, -67, 65);
        var throttle = Input.GetAxis("move_backward", "move_forward");
        var brake = InputMap.HasAction("jump") && Input.IsActionPressed("jump");
        Advance(dt, throttle, Input.GetAxis("move_left", "move_right"), brake);
        AttachDriver(); UpdateVisuals(dt);
        _mechanical.SetState(Definition.Kind, EngineRunning, Math.Abs(Speed), Math.Abs(throttle), false);
    }

    private void Advance(float dt, float throttle, float steeringInput, bool footBrake)
    {
        var previous = GlobalPosition;
        var diagnosticBeforePose = _collisionStopDiagnostics ? GlobalTransform : default;
        var diagnosticBeforeSpeed = _collisionStopDiagnostics ? Speed : 0;
        var diagnosticBeforeVelocity = _collisionStopDiagnostics ? Velocity : default;
        var diagnosticBeforeFloor = _collisionStopDiagnostics && IsOnFloor();
        var decision = _fleet.EvaluateTravel(this, previous, previous - GlobalBasis.Z * Math.Max(Math.Abs(Speed)*dt,.12f)
            * (throttle < 0 ? -1 : 1));
        HorseState = Definition.Kind == VehicleKind.HorseCart ? _fleet.HorseMoodAt(previous) : HorseDisposition.Calm;
        var factor = Mathf.Clamp(decision.SpeedFactor, .1f, 1f);
        if (HorseState == HorseDisposition.Wary) factor = Math.Min(factor, .75f);
        if (HorseState == HorseDisposition.Slowing) factor = Math.Min(factor, .45f);
        if (HorseState == HorseDisposition.Refusing && throttle > 0 && -GlobalBasis.Z.Z < 0)
            decision = new(false,"Лошадь упёрлась. Можно отъехать назад или развернуться.");
        var running = EngineRunning && _engineWarmup <= 0;
        var enabled = running && !ParkingBrake && !footBrake;
        var previousSteering=_steering;
        var previousLean=_motorcycleLean;
        float lateral=0;float yaw;float desired;bool braking;
        if (_mechanics is not null)
        {
            // Niva: the vendored drivetrain/tyre model (game/addons/gevp,
            // adapted in VehicleMechanics) produces the planar velocity and
            // yaw; parking, zone gates and collision stops stay unchanged.
            _steering = ConstrainSteering(Mathf.MoveToward(_steering, steeringInput * Mathf.DegToRad(Definition.SteeringDegrees), dt * 1.6f));
            ApplySteeringCollision();
            _mechanics.Step(dt, throttle, _steering, footBrake, ParkingBrake, running,
                factor, _fleet.SurfaceAt(previous, -GlobalBasis.Z));
            Speed = _mechanics.ForwardSpeed;
            lateral = _mechanics.LateralSpeed;
            yaw = _mechanics.YawDelta;
            braking = footBrake || ParkingBrake || _mechanics.ServiceBraking;
            desired = Speed;
        }
        else
        {
            desired = enabled ? throttle * (throttle < 0 ? Definition.ReverseSpeed : Definition.MaxForwardSpeed) * factor : 0;
            // Opposite throttle is a brake until stopped; reversing never flips velocity instantaneously.
            braking = footBrake || ParkingBrake || (Math.Sign(throttle) != Math.Sign(Speed) && Math.Abs(Speed) > .25f);
            if (braking) desired = 0;
            Speed = Mathf.MoveToward(Speed, desired, (braking ? Definition.BrakeDeceleration
                : Math.Abs(throttle) < .03f || !enabled ? 1.5f : Definition.Acceleration) * dt);
            _steering = ConstrainSteering(Mathf.MoveToward(_steering, steeringInput * Mathf.DegToRad(Definition.SteeringDegrees), dt * 1.6f));
            ApplySteeringCollision();
            yaw = -Speed / Definition.WheelBase * Mathf.Tan(_steering) * dt;
        }
        if (!decision.Allowed)
        {
            Speed = 0; lateral = 0; yaw = 0; _mechanics?.BlockMotion(); LastRefusal = decision.Reason;
            if (Math.Abs(throttle) > .05f) Notice(decision.Reason);
        }
        else LastRefusal = string.Empty;
        if (Math.Abs(yaw) <= .00001f || !CanRotate(yaw)) yaw = 0;
        var requestedPose = new Transform3D(new Basis(Vector3.Up, yaw) * GlobalBasis, GlobalPosition);
        requestedPose.Origin -= requestedPose.Basis.Z * Speed * dt;
        var hoofFraction = PrepareHorseMovement(requestedPose, dt);
        if (yaw != 0) RotateY(yaw * hoofFraction);
        var forward = -GlobalBasis.Z;
        var finalDecision = _fleet.EvaluateTravel(this, previous,
            previous + new Vector3(forward.X,0,forward.Z) * Speed * dt);
        if(!finalDecision.Allowed && (Math.Abs(Speed)>.001f||Math.Abs(lateral)>.001f))
        { Speed=0;lateral=0;_mechanics?.BlockMotion();Notice(finalDecision.Reason); }
        var vertical = IsOnFloor() ? -.15f : Velocity.Y - 21.6f * dt;
        var planar = _mechanics is not null
            ? GlobalBasis.X * lateral - GlobalBasis.Z * Speed
            : forward * (Speed * hoofFraction);
        Velocity = new(planar.X, vertical, planar.Z);
        var diagnosticRequestedVelocity = _collisionStopDiagnostics ? Velocity : default;
        MoveAndSlide();
        var actual = new Vector2(GlobalPosition.X-previous.X, GlobalPosition.Z-previous.Z).Length();
        TotalTravelMetres += actual;
        if (_mechanics is not null) _wheelPhase -= _mechanics.WheelSpin * dt;
        else _wheelPhase -= Math.Sign(Speed)*actual/Definition.WheelRadius;
        if (IsOnWall() && actual < Math.Abs(Speed)*dt*.50f)
        {
            // Observe the same MoveAndSlide result before zeroing its commanded
            // speed. No query, movement, collision response or threshold changes.
            if (_collisionStopDiagnostics && _firstCollisionStop is null)
                CaptureCollisionStop(diagnosticBeforePose, diagnosticBeforeSpeed, diagnosticBeforeVelocity,
                    diagnosticBeforeFloor, diagnosticRequestedVelocity, dt, throttle, steeringInput,
                    footBrake, braking, desired, actual, decision, finalDecision);
            if (Math.Abs(Speed) >= 2.0f && GetSlideCollisionCount() > 0)
            {
                var contact = GetSlideCollision(0);
                Node? collider = null;
                for (var index = 0; index < contact.GetCollisionCount(); index++)
                {
                    if (contact.GetCollider(index) is Node node) { collider = node; break; }
                }
                HardStop?.Invoke(this, Math.Abs(Speed), collider);
            }
            Speed = 0; lateral = 0; _mechanics?.BlockMotion(); CollisionStops++;
        }
        if (hoofFraction < 1) Speed = 0;
        // MarkDirty only sets a flag; VehicleFleet retains its two-second commit
        // throttle. A short steering action must not be lost before .8 seconds.
        _driveTime += dt;
        if(Math.Abs(_steering-previousSteering)>.00001f||_motorcycleLean!=previousLean)_fleet.MarkDirty();
        if (actual > .001f && _driveTime-_lastDirtyTime >= .8)
        { _lastDirtyTime = _driveTime; _fleet.MarkDirty(); }
    }

    private void CaptureCollisionStop(Transform3D beforePose, float beforeSpeed, Vector3 beforeVelocity,
        bool beforeFloor, Vector3 requestedVelocity, float dt, float throttle, float steeringInput,
        bool footBrake, bool braking, float desiredSpeed, float actualXZ,
        VehicleTravelDecision decision, VehicleTravelDecision finalDecision)
    {
        static JsonArray V(Vector3 value) => new(value.X, value.Y, value.Z);
        static JsonObject Pose(Transform3D value) => new()
        {
            ["origin"] = V(value.Origin), ["basisX"] = V(value.Basis.X),
            ["basisY"] = V(value.Basis.Y), ["basisZ"] = V(value.Basis.Z)
        };
        static JsonObject? Owner(Node? node)
        {
            if (node is null || !GodotObject.IsInstanceValid(node)) return null;
            return new JsonObject
            {
                ["path"] = node.IsInsideTree() ? node.GetPath().ToString() : null,
                ["name"] = node.Name.ToString(), ["class"] = node.GetClass().ToString(),
                ["shapeType"] = (node as CollisionShape3D)?.Shape?.GetClass().ToString(),
                ["authoredSourceMesh"] = node.HasMeta("authoredSourceMesh")
                    ? node.GetMeta("authoredSourceMesh").AsString() : null,
                ["collisionOwner"] = node.HasMeta("collisionOwner")
                    ? node.GetMeta("collisionOwner").AsString() : null,
                ["pose"] = node is Node3D spatial && spatial.IsInsideTree() ? Pose(spatial.GlobalTransform) : null
            };
        }
        var slides = new JsonArray();
        for (var slide = 0; slide < GetSlideCollisionCount(); slide++)
        {
            var hit = GetSlideCollision(slide);
            var contacts = new JsonArray();
            for (var index = 0; index < hit.GetCollisionCount(); index++)
            {
                var collider = hit.GetCollider(index) as Node;
                var shapeIndex = hit.GetColliderShapeIndex(index);
                var shape = hit.GetColliderShape(index) as Node;
                if (shape is null && collider is CollisionObject3D body && shapeIndex >= 0)
                    shape = body.ShapeOwnerGetOwner(body.ShapeFindOwner(shapeIndex)) as Node;
                contacts.Add(new JsonObject
                {
                    ["index"] = index, ["collider"] = Owner(collider), ["shapeIndex"] = shapeIndex,
                    ["colliderShape"] = Owner(shape), ["localShape"] = Owner(hit.GetLocalShape(index) as Node),
                    ["position"] = V(hit.GetPosition(index)), ["normal"] = V(hit.GetNormal(index)),
                    ["colliderVelocity"] = V(hit.GetColliderVelocity(index)),
                    ["nearbyActualTerrainFaces"] = DescribeContactFaces(shape as CollisionShape3D, hit.GetPosition(index))
                });
            }
            slides.Add(new JsonObject
            {
                ["slide"] = slide, ["travel"] = V(hit.GetTravel()), ["remainder"] = V(hit.GetRemainder()),
                ["depth"] = hit.GetDepth(), ["contacts"] = contacts
            });
        }
        _firstCollisionStop = new JsonObject
        {
            ["schema"] = "urman.vehicle_collision_stop.v1", ["vehicleId"] = Definition.Id,
            ["physicsBackend"] = PhysicsServer3D.Singleton.GetClass().ToString(),
            ["configuredPhysicsEngine"] = ProjectSettings.GetSettingWithOverride("physics/3d/physics_engine").AsString(),
            ["physicsFrame"] = Engine.GetPhysicsFrames(), ["processFrame"] = Engine.GetProcessFrames(),
            ["nextCollisionStops"] = CollisionStops + 1, ["deltaSeconds"] = dt,
            ["beforePose"] = Pose(beforePose), ["afterPose"] = Pose(GlobalTransform),
            ["beforeSpeed"] = beforeSpeed, ["desiredSpeed"] = desiredSpeed, ["commandedSpeed"] = Speed,
            ["beforeVelocity"] = V(beforeVelocity), ["requestedVelocity"] = V(requestedVelocity),
            ["requestedMotion"] = V(requestedVelocity * dt), ["resultVelocity"] = V(Velocity),
            ["realVelocity"] = V(GetRealVelocity()), ["positionDelta"] = V(GetPositionDelta()),
            ["actualXZ"] = actualXZ, ["expectedXZ"] = Math.Abs(Speed) * dt,
            ["stopThresholdXZ"] = Math.Abs(Speed) * dt * .50f,
            ["throttle"] = throttle, ["steeringInput"] = steeringInput, ["steeringRadians"] = _steering,
            ["footBrake"] = footBrake, ["braking"] = braking, ["parkingBrake"] = ParkingBrake,
            ["engineRunning"] = EngineRunning, ["engineWarmup"] = _engineWarmup,
            ["horseState"] = HorseState.ToString(), ["wasOnFloor"] = beforeFloor,
            ["onFloor"] = IsOnFloor(), ["onWall"] = IsOnWall(),
            ["floorNormal"] = IsOnFloor() ? V(GetFloorNormal()) : null,
            ["wallNormal"] = IsOnWall() ? V(GetWallNormal()) : null,
            ["floorMaxAngle"] = FloorMaxAngle, ["safeMargin"] = SafeMargin, ["collisionMask"] = CollisionMask,
            ["initialTravelAllowed"] = decision.Allowed, ["initialTravelReason"] = decision.Reason,
            ["finalTravelAllowed"] = finalDecision.Allowed, ["finalTravelReason"] = finalDecision.Reason,
            ["slides"] = slides,
            ["scope"] = "Explicit contact diagnostic only; snapshot before Speed=0; not a performance result."
        };
        GD.Print("act1-vehicle-collision-stop: " + _firstCollisionStop.ToJsonString());
    }

    private void AttachDriver()
    {
        if (Driver is not null) Driver.GlobalPosition = ToGlobal(Definition.Seat);
        _look.RotationDegrees = new(_pitch,_lookYaw,0);
    }

    private bool CanRotate(float yaw)
    {
        // Rotation has no linear sweep in MoveAndSlide. Sample the actual hull
        // through the arc; an endpoint test or raised/shrunken proxy misses posts
        // and low fences. The unmodified collision hull retains its floor contact.
        var steps=Math.Max(1,(int)Math.Ceiling(Math.Abs(yaw)/Mathf.DegToRad(.25f)));
        var excluded=Excluded();
        // The array is owned by this call; the queries copy the reference, so releasing
        // our wrapper here cannot invalidate them.
        using var excludedOwner = (global::Godot.Collections.Array)excluded;
        for(var i=1;i<=steps;i++)
        {
            var basis=GlobalBasis.Rotated(Vector3.Up,yaw*i/steps);
            var rotationHits=VolumeOverlaps(new(basis,GlobalPosition),_steering,excluded,1);
            using var rotationHitsOwner=(global::Godot.Collections.Array)rotationHits;
            if(rotationHits.Count>0)return false;
        }
        return true;
    }

    public bool TryFindSafeExit(out Vector3 feet)
    {
        feet = default;
        if (Driver is not { } player) return false;
        var side = Definition.HullSize.X*.5f + player.BodyRadius + .19f;
        var seatZ = Definition.Kind == VehicleKind.HorseCart ? .39f : .15f;
        // Prefer the sides; a motorcycle can also be left behind its rear wheel.
        var offsets = new List<Vector3>{new(-side,0,seatZ),new(side,0,seatZ),
            new(-side,0,seatZ+.7f),new(side,0,seatZ+.7f)};
        if (Definition.Kind == VehicleKind.Motorcycle)
            offsets.Add(new(0,0,Definition.HullSize.Z*.5f+player.BodyRadius+.19f));
        foreach (var offset in offsets)
        {
            var candidate = ToGlobal(offset);
            // Owned per call, released here: the parameters, the exclusion array and
            // the returned dictionary otherwise all end up in the finalizer queue.
            using var ray = PhysicsRayQueryParameters3D.Create(candidate+Vector3.Up*2.0f,candidate-Vector3.Up*2.1f,3);
            var exclude = Excluded();
            using var excludeOwner = (global::Godot.Collections.Array)exclude;
            ray.Exclude = exclude;
            using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count == 0 || hit["normal"].AsVector3().Y < .82f) continue;
            candidate = hit["position"].AsVector3()+Vector3.Up*.035f;
            if (Math.Abs(candidate.Y-GlobalPosition.Y) > .65f || !player.CanStandAt(candidate)) continue;
            _exitShape.Height = player.StandingBodyHeight; _exitShape.Radius = player.BodyRadius;
            var start = offset.X == 0
                ? ToGlobal(new(0,0,Definition.HullSize.Z*.5f+.02f))
                : ToGlobal(new(Math.Sign(offset.X)*(Definition.HullSize.X*.5f+.02f),0,offset.Z));
            start.Y = candidate.Y;
            var sweepExclude = Excluded();
            using var sweepExcludeOwner = (global::Godot.Collections.Array)sweepExclude;
            using var sweep = new PhysicsShapeQueryParameters3D { Shape = _exitShape,
                Transform = new(Basis.Identity,start+Vector3.Up*(player.StandingBodyHeight*.5f+.025f)),
                Motion = candidate-start, CollisionMask = 3, Exclude = sweepExclude, Margin = .01f };
            // CastMotion explicitly ignores shapes already overlapped at its
            // origin. A thin fence beside the door must not be crossed on exit.
            var overlap = GetWorld3D().DirectSpaceState.IntersectShape(sweep,1);
            using var overlapOwner = (global::Godot.Collections.Array)overlap;
            if(overlap.Count>0)continue;
            var travel = GetWorld3D().DirectSpaceState.CastMotion(sweep);
            if (travel.Length >= 1 && travel[0] < .995f) continue;
            feet = candidate; return true;
        }
        return false;
    }

    private global::Godot.Collections.Array<Rid> Excluded()
    {
        var exclude = new global::Godot.Collections.Array<Rid> { GetRid(), _entry.GetRid() };
        if(Driver is not null)exclude.Add(Driver.GetRid());return exclude;
    }

    private void UpdateVisuals(float delta)
    {
        ApplySteeringCollision();
        for (var index = 0; index < _visualWheels.Length; index++)
            _visualWheels[index].Rotation = new(_wheelPhase,_visualFrontWheel[index]?RoadWheelYaw(_steering):0,0);
        // Visually switching the lamps is a pure presentation write of the same
        // boolean; the native set only happens when Headlights actually changes.
        if(_lampsVisible!=Headlights)
        { _lampsVisible=Headlights;foreach (var lamp in _visual.Lamps) lamp.Visible = Headlights; }
        if(_visual.SteeringWheel is {} steering)
            steering.RotationDegrees = new(VehicleVisualFactory.NivaSteeringTiltDegrees,0,-Mathf.RadToDeg(_steering)*2f);
        if(_visual.Charm is {} charm)SwingCharm(charm,delta);
        if(_visual.SpeedNeedle is {} speedNeedle)
            speedNeedle.RotationDegrees=new(0,0,130-Mathf.Clamp(Math.Abs(Speed)*3.6f/(Definition.Kind==VehicleKind.Niva?160:120),0,1)*260);
        if(_visual.EngineNeedle is {} engineNeedle)
        {
            var engineLoad=_mechanics is not null
                ? Mathf.Clamp(_mechanics.Rpm/_mechanics.MaxRpm,0,1)
                : (EngineRunning ? .9f+Math.Abs(Speed)*.28f : 0)/8;
            engineNeedle.RotationDegrees=new(0,0,130-engineLoad*260);
        }
        if(_visual.RadioDisplay is {} tuning)
        {
            var text=Radio?.Tuning??"— —";
            // Same string value, so an unchanged tuning label is not rewritten.
            if(tuning.Text!=text)tuning.Text=text;
        }
        // Roll around the rider's support, with the handlebar/mirrors staying
        // inside the real one-metre hull even at the maximum permitted bank.
        var visualTransform = MotorcycleLeanTransform();
        if (_visual.Root.Transform != visualTransform) _visual.Root.Transform = visualTransform;
        _visual.HorsePose?.UpdatePose(this,delta,Speed,_steering,HorseState);
        // Occupied-cart driver figure: visible exactly while this cart has a
        // driver, hidden otherwise. Presentation only, no physics or save role.
        // TryExit clears Driver without a following physics tick, so the figure
        // is synced eagerly there too; this call keeps every other path exact.
        SyncCartDriverFigure();
    }

    private Vector2 _charmAngle, _charmRate;
    private float _charmLastSpeed, _charmPhase;

    /// <summary>
    /// The mirror charm is a damped pendulum in the car's frame: it leans back
    /// when the car pulls away, forward under braking and outward in a turn,
    /// and trembles a little with the idling engine. Presentation only.
    /// </summary>
    private void SwingCharm(Node3D charm,float delta)
    {
        if(delta<=0)return;
        const float gravity=9.8f, omegaSquared=38f, damping=1.6f;
        var forwardAccel=Mathf.Clamp((Speed-_charmLastSpeed)/delta,-12f,12f);
        _charmLastSpeed=Speed;
        var yawRate=-Speed/Definition.WheelBase*Mathf.Tan(_steering);
        var lateralAccel=-yawRate*Speed;
        _charmPhase+=delta*31f;
        var tremble=EngineRunning ? .35f*Mathf.Sin(_charmPhase)+.2f*Mathf.Sin(_charmPhase*1.7f) : 0f;
        var target=new Vector2(-forwardAccel/gravity,-lateralAccel/gravity);
        var accel=(target-_charmAngle)*omegaSquared-_charmRate*damping+new Vector2(tremble,tremble*.6f);
        _charmRate+=accel*delta;
        _charmAngle=(_charmAngle+_charmRate*delta).Clamp(new Vector2(-.35f,-.35f),new Vector2(.35f,.35f));
        charm.Rotation=new(_charmAngle.X,0,_charmAngle.Y);
    }

    private void SyncCartDriverFigure()
    {
        // The authored driver figure is built once with the visual and is never
        // replaced, so the name lookup is resolved once instead of every tick. The
        // visible flag is compared like the other presentation writes here.
        if (_cartDriverFigure is null) return;
        var visible = Driver is not null;
        if (_cartDriverVisible == visible) return;
        _cartDriverVisible = visible;
        _cartDriverFigure.Visible = visible;
    }

    private void Notice(string message){_notice=message;_noticeSeconds=2.5f;LastRefusal=message;}

    public string ControlHint()
    {
        if(_noticeSeconds>0)return _notice;
        var pad=Driver?.CurrentInputDevice=="gamepad";
        string H(string action)=>InputBindingService.ActionHint(action,pad);
        var engine=Definition.Kind==VehicleKind.HorseCart?(EngineRunning?"остановить лошадь":"тронуться"):(EngineRunning?"заглушить":"завести");
        var state=Definition.Kind==VehicleKind.HorseCart
            ? HorseState switch {HorseDisposition.Wary=>"Лошадь насторожилась",HorseDisposition.Slowing=>"Лошадь сбавляет шаг",
                HorseDisposition.Refusing=>"Лошадь отказывается идти вперёд",_=>"Лошадь спокойна"}
            : $"{Math.Abs(Speed)*3.6f:0} км/ч · {(EngineRunning?"двигатель работает":"двигатель выключен")}"
                +(EngineRunning&&_mechanics is not null?" · "+_mechanics.GearLabel:string.Empty);
        var horse=Definition.Kind==VehicleKind.HorseCart;
        var parking=horse?"тормоз телеги":"стояночный тормоз";
        return Definition.DisplayName+" · "+state+(ParkingBrake?" · "+parking:"")
            +"\n"+H("move_forward")+"/"+H("move_backward")+(horse?" вперёд / осадить · ":" газ / тормоз / назад · ")
                +H("move_left")+"/"+H("move_right")+(horse?" направить":" поворот")
            +"\n"+H("carry_use")+" "+engine+" · "+H("jump")+(horse?" придержать · ":" тормоз · ")
                +H("crouch")+" "+parking+" · "+H("interact")+" выйти"
            +(Definition.Kind!=VehicleKind.HorseCart?" · "+H("carry_rotate")+" фары · "
                +H("vehicle_high_beam")+" дальний · "+H("vehicle_wipers")+" дворники":"")
            +(Radio is null?"":"\n"+H("carry_place")+" радио · "+(Radio.Enabled?Radio.Display:"выключено")
                +(Radio.StationCount>1?" · "+H("radio_station")+" канал":""));
    }

    public JsonObject Capture()
    {
        // A rejected hoof projection gates entry through the live placement check
        // and stays repairable in place, so it must not make the session
        // unsaveable: saving and reloading is how a player recovers from a
        // blocked cart. The record stays a pure function of the persisted state
        // (the articulated pose is re-derived and re-validated on restore), so a
        // repeated indoor round trip cannot compare two different records.
        JsonArray V(Vector3 vector)=>new(vector.X,vector.Y,vector.Z);
        return new JsonObject { ["propId"]=Definition.StateId,["version"]=1,["position"]=V(GlobalPosition),
            ["yawDegrees"]=CaptureYawDegrees(),["engineRunning"]=EngineRunning,["parkingBrake"]=ParkingBrake,
            ["headlights"]=Headlights,["highBeams"]=HighBeams,["wipers"]=Wipers,
            ["travelMetres"]=TotalTravelMetres,["radio"]=Radio?.Capture(),
            ["steeringRadians"]=_steering,["leanRadians"]=_motorcycleLean };
    }

    private void RememberSavedYaw(float yawDegrees)
    {
        _savedYawDegrees = yawDegrees;
        _savedYawBasis = Basis;
        _hasSavedYaw = true;
    }

    private float CaptureYawDegrees()
    {
        // Godot's degrees/radians round-trip need not return the input float.
        // Preserve that input only while the actual local rotation is exactly
        // unchanged. Even a one-component change records the new live angle.
        if (!_hasSavedYaw || Basis != _savedYawBasis) RememberSavedYaw(RotationDegrees.Y);
        return _savedYawDegrees;
    }

    public bool Restore(JsonElement record)
    {
        // Validate the complete transform before applying anything from disk.
        if(record.ValueKind!=JsonValueKind.Object || !record.TryGetProperty("version",out var version)
            ||version.ValueKind!=JsonValueKind.Number||!version.TryGetInt32(out var number)||number!=1)return false;
        Vector3 position;
        try{position=VehicleDefinition.ReadVector(record.GetProperty("position"));}catch{return false;}
        if(!record.TryGetProperty("yawDegrees",out var yawValue)||!yawValue.TryGetSingle(out var yaw)||!float.IsFinite(yaw))return false;
        if(!_fleet.IsWithinTerrain(position))return false;
        var steering=0f;
        if(record.TryGetProperty("steeringRadians",out var steeringValue)
            &&(steeringValue.ValueKind!=JsonValueKind.Number||!steeringValue.TryGetSingle(out steering)
                ||!float.IsFinite(steering)||Math.Abs(steering)>Mathf.DegToRad(Definition.SteeringDegrees)+.000001f))return false;
        var lean=0f;
        if(record.TryGetProperty("leanRadians",out var leanValue)
            &&(leanValue.ValueKind!=JsonValueKind.Number||!leanValue.TryGetSingle(out lean)
                ||!float.IsFinite(lean)||Math.Abs(lean)>.04f
                ||Definition.Kind!=VehicleKind.Motorcycle&&lean!=0))return false;
        ReleaseForSessionBoundary();
        GlobalPosition=position;RotationDegrees=new(0,yaw,0);Speed=0;Velocity=Vector3.Zero;
        RememberSavedYaw(yaw);
        _steering=steering;_motorcycleLean=lean;
        _mechanics?.Reset();
        _acceptedHorsePose = _pendingHorsePose = null;
        _rejectedHorsePose = null; _horseProjectionFailure = string.Empty;
        EngineRunning=record.TryGetProperty("engineRunning",out var engine)&&engine.ValueKind==JsonValueKind.True;
        ParkingBrake=!record.TryGetProperty("parkingBrake",out var park)||park.ValueKind!=JsonValueKind.False;
        Headlights=record.TryGetProperty("headlights",out var lights)&&lights.ValueKind==JsonValueKind.True;
        // Version-1 records written before the lamp/wiper toggles simply leave
        // both off; the optional reads keep every existing save loadable.
        HighBeams=record.TryGetProperty("highBeams",out var high)&&high.ValueKind==JsonValueKind.True;
        Wipers=record.TryGetProperty("wipers",out var wipers)&&wipers.ValueKind==JsonValueKind.True;
        TotalTravelMetres=record.TryGetProperty("travelMetres",out var metres)&&metres.TryGetSingle(out var value)&&float.IsFinite(value)?Math.Max(0,value):0;
        Radio?.Restore(record.TryGetProperty("radio",out var radio)&&radio.ValueKind==JsonValueKind.Object?radio:null);
        _controlsNeedRelease=true;UpdateVisuals(0);return true;
    }

    public bool ValidatePhysicalPlacement(out string reason)
        =>ValidatePhysicalPlacement(GlobalTransform,out reason);

    internal void SetPlacementAvailability(bool available,string reason= "")
    {
        if (available && !TryRepairHorseProjection()) { available = false; reason = _horseProjectionFailure; }
        PlacementAvailable=available;PlacementFailure=available?string.Empty:reason;
        SetMeta("placementAvailable",available);SetMeta("placementFailure",PlacementFailure);
        if(!available){Speed=0;Velocity=Vector3.Zero;_mechanics?.Block();_controlsNeedRelease=true;}
    }

    private bool ValidatePhysicalPlacement(Transform3D pose,out string reason,float? steering=null,float? lean=null)
    {
        reason=string.Empty;
        if(!_fleet.IsWithinTerrain(pose.Origin)){reason="outside the authored terrain";return false;}
        var access=_fleet.EvaluateTravel(this,pose.Origin,pose.Origin);
        if(!access.Allowed){reason="the current road graph rejects this parking: "+access.Reason;return false;}
        var exclude=PlacementExcluded();
        using var excludeOwner = (global::Godot.Collections.Array)exclude;
        var horseFrame = _horseProjectionFailure.Length != 0 && _visual.HorsePose is {} horse
            // PreparePose just re-grounded every leg of this plan, so its retained
            // soles are proven for this tick and this HorsePose.Basis; letting
            // SupportedHorseFrame reuse them skips only the duplicate Ground rays.
            ? SupportedHorseFrame(horse, horse.PreparePose(this, pose, 0, 0, _steering, HorseState, rest: true),
                freshlyPrepared: true)
            : HorseFrameForPose(pose);
        if(horseFrame is not null && !HoofEndpointClear(horseFrame,out var hoofContact))
        { reason="the actual articulated hoof pose is blocked or unsupported: "+hoofContact?.ToJsonString();return false; }
        var overlaps=PlacementOverlaps(pose,1,steering,lean);
        using var overlapsOwner=(global::Godot.Collections.Array)overlaps;
        if(overlaps.Count>0)
        {
            var contact=DescribePlacementContact(overlaps[0]);
            reason=$"the actual {contact["vehicleShape"]} intersects {contact["colliderPath"]}; shape={contact["shapePath"]}"
                +$" index={contact["shapeIndex"]}; chassisOrigin={pose.Origin}";
            return false;
        }
        var supports=0;
        foreach(var group in AppliedSupportGroups(steering,lean,pose))
        {
            var bottom=pose*group.Point;
            using var ray=PhysicsRayQueryParameters3D.Create(bottom+Vector3.Up*.24f,bottom-Vector3.Up*.42f,CollisionMask);
            ray.Exclude=exclude;
            using var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if(hit.Count==0||hit["normal"].AsVector3().Y<Mathf.Cos(FloorMaxAngle))continue;
            var gap=bottom.Y-hit["position"].AsVector3().Y;
            if(gap>=-.015f&&gap<=.40f)supports++;
        }
        if(supports<MinimumSupportedGroups){reason="the chassis lacks support for the required wheel/hoof groups on the actual ground";return false;}
        return true;
    }

    private IEnumerable<Vector3> SupportPoints(float? steering=null,float? lean=null)
        =>AppliedSupportGroups(steering,lean,restBindings:steering.HasValue&&lean.HasValue).Select(group=>group.Point);

    private global::Godot.Collections.Array<Rid> PlacementExcluded()
    {
        var excluded=Excluded();
        // SaveGameV3 restores the person's seated transform before possession is
        // projected; that temporary player capsule is not a world obstruction.
        if(GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player
            &&!excluded.Contains(player.GetRid()))excluded.Add(player.GetRid());
        return excluded;
    }

    internal bool TryRestoreAuthoredParking()
    {
        var basis=Basis.FromEuler(new(0,Mathf.DegToRad(Definition.YawDegrees),0));
        var exclude=PlacementExcluded();
        using var excludeOwner = (global::Godot.Collections.Array)exclude;
        // A changed object may occupy the saved position or original bay. Use
        // only a short, checked part of the existing road near authored parking.
        foreach(var distance in new[]{0f,6f,-6f,12f,-12f})
        {
            var candidate=Definition.Spawn+basis*new Vector3(0,0,distance);
            var highest=float.NegativeInfinity;var lowest=float.PositiveInfinity;var supports=0;
            foreach(var point in SupportPoints(0,0))
            {
                var sample=candidate+basis*point;
                using var ray=PhysicsRayQueryParameters3D.Create(sample+Vector3.Up*2,sample-Vector3.Up*2,CollisionMask);
                ray.Exclude=exclude;using var hit=GetWorld3D().DirectSpaceState.IntersectRay(ray);
                if(hit.Count==0||hit["normal"].AsVector3().Y<Mathf.Cos(FloorMaxAngle))continue;
                var floor=hit["position"].AsVector3().Y-point.Y;
                highest=Math.Max(highest,floor);lowest=Math.Min(lowest,floor);supports++;
            }
            if(supports<_supportGroups.Count||highest-lowest>.35f)continue;
            candidate.Y=highest+.025f;
            if(!_fleet.EvaluateTravel(this,candidate,candidate).Allowed)continue;
            var pose=new Transform3D(basis,candidate);
            if(!ValidatePhysicalPlacement(pose,out _,steering:0,lean:0))continue;
            ReleaseForSessionBoundary();GlobalTransform=pose;ParkingBrake=true;
            RememberSavedYaw(RotationDegrees.Y);
            _steering=_pitch=_lookYaw=_wheelPhase=_motorcycleLean=0;_controlsNeedRelease=true;
            _acceptedHorsePose = _pendingHorsePose = null;
            _rejectedHorsePose = null; _horseProjectionFailure = string.Empty;
            // Repair only the unsafe pose. The saved ignition, lamps, mileage
            // and radio programme remain the same persistent vehicle state.
            UpdateVisuals(0);return true;
        }
        return false;
    }
}
