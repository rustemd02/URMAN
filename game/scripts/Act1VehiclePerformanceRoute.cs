using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Explicit Release performance fixture. It supplies ordinary input to existing
/// owners; it never writes an actor pose, a quest, a save, or a collision shape.
/// Setup and safe exit are outside the timed 12+60 second gameplay interval.
/// </summary>
public partial class Act1VehiclePerformanceRoute : Node
{
    private enum Phase { Waiting, Walking, Aiming, Entering, Ignition, Radio, Driving, Finishing, Done }
    private static readonly string[] Actions =
        ["move_forward", "move_backward", "move_left", "move_right", "jump", "interact", "carry_use", "carry_place"];
    private Main _main = null!;
    private FirstPersonController _player = null!;
    private VehicleFleet? _fleet;
    private VehicleController? _vehicle;
    private Camera3D _footCamera = null!;
    private RuntimeBridge _bridge = null!;
    private object? _session;
    private string _vehicleId = string.Empty;
    private Phase _phase;
    private readonly List<Vector2> _walk = [];
    private readonly List<object> _path = [];
    private readonly List<object> _contactEvents = [];
    private bool _contactDiagnostics;
    private int _walkIndex, _frames, _settleFrames, _reversals, _edgeClamps, _fallRecoveries, _transformRevision;
    private double _setupSeconds, _phaseSeconds, _stalledSeconds, _radioSilentSeconds, _nextTrace;
    private double _drivingSeconds, _distance, _measurementSeconds, _movingSeconds, _measurementDistance;
    private Vector3 _previousPlayer, _previousVehicle;
    private float _southZ, _northZ, _laneOffset;
    private bool _reverse, _braking, _measuring, _finishRequested, _exitSent;
    private string? _pulse;
    private string _initialZone = string.Empty;
    private TaskCompletionSource<bool>? _finish;
    private ulong _finishStarted;
    public bool MeasurementReady { get; private set; }
    public double ElapsedSeconds => _setupSeconds;
    public string Failure { get; private set; } = string.Empty;
    public bool SafeExit { get; private set; }
    public const double MinimumMovingFraction = .8;
    public const double MinimumMeasuredDistance = 20;

    public static string? VehicleForSample(string label) => label switch
    {
        "vehicle_niva_radio@main_road" => "babay-niva",
        "vehicle_motorcycle@main_road" => "village-motorcycle",
        "vehicle_horse_cart@zirat_road" => "forest-horse-cart",
        _ => null
    };

    public void Configure(Main main, FirstPersonController player, string vehicleId)
    {
        _main = main; _player = player; _vehicleId = vehicleId;
        _footCamera = player.GetNode<Camera3D>("Head/Camera3D");
        _bridge = main.GetNode<RuntimeBridge>("RuntimeBridge");
        // This owner is only constructed for an explicitly selected native
        // performance route. Contact reproduction is additionally opt-in.
        _contactDiagnostics = OS.GetEnvironment("URMAN_VEHICLE_CONTACT_DIAGNOSTICS") == "1";
        Name = "ExplicitVehiclePerformanceRoute";
        // Input edges must be set before the existing player/vehicle physics tick.
        ProcessPhysicsPriority = -100;
        _previousPlayer = player.GlobalPosition;
        _edgeClamps = player.EdgeClamps; _fallRecoveries = player.FallRecoveries;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pulse is { } pulse) { Input.ActionRelease(pulse); _pulse = null; }
        if (_phase == Phase.Done) return;
        if (_finishRequested) { FinishTick(); return; }
        if (!string.IsNullOrEmpty(Failure)) { ReleaseInput(); return; }
        _setupSeconds += delta; _phaseSeconds += delta; _frames++;
        if (!MeasurementReady && _setupSeconds > 90) { Fail("setup-timeout"); return; }
        if (_session is not null && (!ReferenceEquals(_session, _bridge.SessionIdentity)
            || _bridge.CurrentZoneId != _initialZone)) { Fail("session-or-zone-changed"); return; }
        if (_player.EdgeClamps != _edgeClamps || _player.FallRecoveries != _fallRecoveries)
        { Fail("pedestrian-world-recovery"); return; }
        if (!DisplayServer.WindowIsFocused() || _player.ModalOpen)
        { ReleaseInput(); return; }
        if (_phase == Phase.Waiting)
        {
            _fleet = _main.ConnectedWorld?.VehicleFleet;
            if (_frames < 8 || _fleet is null || _fleet.PlacementValidationDeferred || _bridge.SessionIdentity is null) return;
            _vehicle = _fleet.Vehicles.SingleOrDefault(value => value.Definition.Id == _vehicleId);
            if (_vehicle is null || !_vehicle.PlacementAvailable || _vehicle.HasMeta("rejectedPlacementProbe"))
            { Fail("authored-vehicle-unavailable-or-recovered"); return; }
            if (_contactDiagnostics) _vehicle.SetCollisionStopDiagnostics(true);
            _session = _bridge.SessionIdentity; _initialZone = _bridge.CurrentZoneId;
            BuildWalkingRoute(_vehicle); ChangePhase(Phase.Walking); return;
        }
        var vehicle = _vehicle;
        if (vehicle is null) { Fail("missing-vehicle"); return; }
        if (_phase == Phase.Walking)
        {
            var target = _walk[_walkIndex];
            var feet = Flat(_player.GlobalPosition);
            if (feet.DistanceTo(target) < .16f)
            {
                ReleaseMotion();
                if (++_walkIndex == _walk.Count) ChangePhase(Phase.Aiming);
                return;
            }
            AimFoot(new Vector3(target.X, _footCamera.GlobalPosition.Y, target.Y));
            var distance = feet.DistanceTo(target);
            // GetVector applies the configured stick deadzone to walking. Keep
            // the final approach above it instead of stalling short of the goal.
            SetAxis("move_backward", "move_forward", Mathf.Clamp(distance / .65f, .6f, 1f));
            var moved = Flat(_player.GlobalPosition).DistanceTo(Flat(_previousPlayer));
            _stalledSeconds = moved < .001 ? _stalledSeconds + delta : 0;
            _previousPlayer = _player.GlobalPosition;
            if (_stalledSeconds > 3) Fail("walking-obstruction");
            return;
        }
        if (_phase == Phase.Aiming)
        {
            ReleaseMotion(); AimFoot(vehicle.EntryTarget.GlobalPosition);
            var ray = _footCamera.GetNode<RayCast3D>("InteractionRay"); ray.ForceRaycastUpdate();
            if (_phaseSeconds > .25 && ray.GetCollider() == vehicle.EntryTarget)
            { Pulse("interact"); ChangePhase(Phase.Entering); }
            else if (_phaseSeconds > 3) Fail("entry-target-not-visible");
            return;
        }
        if (_phase == Phase.Entering)
        {
            if (_fleet?.Occupied == vehicle && vehicle.Driver == _player && _player.VehicleControlled)
            {
                if (_phaseSeconds < .15) return; // neutral frames for the controller release gate
                if (vehicle.EngineRunning || !vehicle.ParkingBrake) { Fail("unexpected-initial-power"); return; }
                Pulse("carry_use"); ChangePhase(Phase.Ignition);
            }
            else if (_phaseSeconds > 3) Fail("ordinary-entry-refused");
            return;
        }
        if (!HasDriverCamera(vehicle)) { Fail("driver-or-active-camera-lost"); return; }
        if (_phase == Phase.Ignition)
        {
            if (_phaseSeconds < .75) return;
            if (!vehicle.EngineRunning || vehicle.ParkingBrake) { Fail("ordinary-ignition-refused"); return; }
            if (vehicle.Radio is not null) { Pulse("carry_place"); ChangePhase(Phase.Radio); }
            else StartDriving(vehicle);
            return;
        }
        if (_phase == Phase.Radio)
        {
            if (_phaseSeconds < .5) return;
            if (!RadioPlaying(vehicle)) { Fail("native-radio-not-playing"); return; }
            StartDriving(vehicle); return;
        }
        Drive(vehicle, delta);
    }

    private void BuildWalkingRoute(VehicleController vehicle)
    {
        var entry = vehicle.EntryTarget.GlobalPosition;
        // Walk around the original parked chassis, then approach its real left
        // entry. These are diagnostic walking goals, never transform assignments.
        if (vehicle.Definition.Kind == VehicleKind.Niva)
        {
            _walk.Add(new(0, 5)); _walk.Add(new(entry.X - .8f, 5));
        }
        else
        {
            foreach (var z in new[] { 5f, -2f, -8f, -19f, -25f })
            {
                if (z < vehicle.GlobalPosition.Z + 4) break;
                _walk.Add(new(RoadX(z) + .7f, z));
            }
            _walk.Add(new(entry.X - .8f, vehicle.GlobalPosition.Z + vehicle.Definition.HullSize.Z * .5f + 1.2f));
        }
        _walk.Add(new(entry.X - .8f, entry.Z));
    }

    private void StartDriving(VehicleController vehicle)
    {
        (_southZ, _northZ, _laneOffset) = vehicle.Definition.Kind switch
        {
            VehicleKind.Niva => (0f, -51f, -.95f),
            VehicleKind.Motorcycle => (-25f, -76f, -.75f),
            _ => (-35f, -84f, 0f)
        };
        _previousVehicle = vehicle.GlobalPosition;
        _transformRevision = _player.PresentationTransformRevision;
        _stalledSeconds = 0; ChangePhase(Phase.Driving);
    }

    private void Drive(VehicleController vehicle, double delta)
    {
        var position = vehicle.GlobalPosition;
        var movement = Flat(position).DistanceTo(Flat(_previousVehicle));
        _previousVehicle = position;
        if (_player.PresentationTransformRevision != _transformRevision
            || movement > vehicle.Definition.MaxForwardSpeed * delta + .25)
        { Fail("pose-discontinuity"); return; }
        if (!vehicle.PlacementAvailable || !vehicle.EngineRunning || vehicle.ParkingBrake)
        { Fail("drive-state-lost"); return; }
        if (!string.IsNullOrWhiteSpace(vehicle.LastRefusal)) { Fail("travel-refused:" + vehicle.LastRefusal); return; }
        if (vehicle.CollisionStops > 0)
        {
            if (_contactDiagnostics) _contactEvents.Add(new
            {
                kind = "collision-stop-observed", physicsFrame = Engine.GetPhysicsFrames(),
                position = Coordinates(position), reverse = _reverse, braking = _braking,
                drivingSeconds = _drivingSeconds, collisionStops = vehicle.CollisionStops
            });
            Fail("physical-collision-stop"); return;
        }
        if (vehicle.Radio is not null)
        {
            _radioSilentSeconds = RadioPlaying(vehicle) ? 0 : _radioSilentSeconds + delta;
            if (_radioSilentSeconds > .2) { Fail("native-radio-gap"); return; }
        }
        _drivingSeconds += delta; _distance += movement;
        if (_contactDiagnostics && _drivingSeconds >= 24)
        { Fail("contact-diagnostic-stop-not-reproduced"); return; }
        if (!MeasurementReady && movement > .002 && Math.Abs(vehicle.Speed) > .15f) MeasurementReady = true;
        if (_measuring)
        {
            _measurementSeconds += delta; _measurementDistance += movement;
            if (movement / Math.Max(delta, .0001) > .2) _movingSeconds += delta;
        }
        if (_drivingSeconds >= _nextTrace)
        {
            _nextTrace = _drivingSeconds + 1;
            _path.Add(new { seconds = _drivingSeconds, measuring = _measuring, position = Coordinates(position),
                speed = vehicle.Speed, reverse = _reverse, braking = _braking,
                radioSegment = vehicle.Radio?.CurrentSegmentId, radioOffset = vehicle.Radio?.SegmentOffset,
                camera = GetViewport().GetCamera3D()?.GetPath().ToString() });
        }
        var endZ = _reverse ? _southZ : _northZ;
        var remaining = Math.Abs(position.Z - endZ);
        var stopping = vehicle.Speed * vehicle.Speed / (2 * vehicle.Definition.BrakeDeceleration) + .7f;
        if (remaining < stopping && !_braking)
        {
            _braking = true;
            if (_contactDiagnostics) _contactEvents.Add(new
            {
                kind = "braking-start", physicsFrame = Engine.GetPhysicsFrames(),
                position = Coordinates(position), speed = vehicle.Speed, reverse = _reverse,
                remaining, stopping, drivingSeconds = _drivingSeconds
            });
        }
        if (_braking)
        {
            ReleaseMotion(); Input.ActionPress("jump");
            if (Math.Abs(vehicle.Speed) < .03f)
            {
                if (++_settleFrames >= 4)
                {
                    _reverse = !_reverse; _reversals++; _settleFrames = 0; _braking = false;
                    if (_contactDiagnostics) _contactEvents.Add(new
                    {
                        kind = "direction-reversed", physicsFrame = Engine.GetPhysicsFrames(),
                        position = Coordinates(position), speed = vehicle.Speed, reverse = _reverse,
                        drivingSeconds = _drivingSeconds
                    });
                }
            }
            _stalledSeconds = 0; return;
        }
        Input.ActionRelease("jump");
        var lookAheadZ = position.Z + (_reverse ? 2.4f : -3.2f);
        var lane = vehicle.Definition.Kind == VehicleKind.HorseCart ? 0
            : _laneOffset * Mathf.Clamp((lookAheadZ + 49f) / 9f, 0, 1);
        var target = new Vector2(RoadX(lookAheadZ) + lane, lookAheadZ);
        var direction = (target - Flat(position)).Normalized() * (_reverse ? -1 : 1);
        var forward = Flat(-vehicle.GlobalBasis.Z).Normalized();
        var desiredYaw = Mathf.Atan2(-direction.X, -direction.Y);
        var currentYaw = Mathf.Atan2(-forward.X, -forward.Y);
        var error = Mathf.AngleDifference(currentYaw, desiredYaw);
        var steering = Mathf.Clamp(-error * 2.2f * (_reverse ? -1 : 1), -1, 1);
        SetAxis("move_left", "move_right", steering);
        var targetSpeed = vehicle.Definition.Kind == VehicleKind.HorseCart ? 2.4f : 3.2f;
        var throttle = _reverse ? -.9f : targetSpeed / vehicle.Definition.MaxForwardSpeed;
        SetAxis("move_backward", "move_forward", throttle);
        _stalledSeconds = movement < .001 ? _stalledSeconds + delta : 0;
        if (_stalledSeconds > 2) Fail("driving-obstruction");
    }

    public void BeginMeasurement()
    {
        _measuring = true;
        _measurementSeconds = _movingSeconds = _measurementDistance = 0;
    }

    public bool ValidateMeasurement()
    {
        if (_contactDiagnostics) Fail("contact-diagnostic-is-not-performance-acceptance");
        if (_measurementDistance < MinimumMeasuredDistance) Fail("insufficient-measured-travel");
        if (_measurementSeconds <= 0 || _movingSeconds / _measurementSeconds < MinimumMovingFraction)
            Fail("insufficient-moving-time");
        return string.IsNullOrEmpty(Failure);
    }

    public async Task<bool> FinishAsync()
    {
        if (_finish is not null) return await _finish.Task;
        _finish = new TaskCompletionSource<bool>();
        _finishRequested = true; _finishStarted = Time.GetTicksMsec();
        ReleaseInput(); ChangePhase(Phase.Finishing);
        return await _finish.Task;
    }

    private void FinishTick()
    {
        if (Time.GetTicksMsec() - _finishStarted > 8000)
        { FinishResult(false, "safe-exit-timeout"); return; }
        var vehicle = _vehicle;
        if (vehicle is null || vehicle.Driver is null)
        {
            var clear = _player.IsInsideTree() && !_player.VehicleControlled
                && _player.CanStandAt(_player.GlobalPosition) && _footCamera.Current;
            FinishResult(clear, "standing-exit-not-clear"); return;
        }
        ReleaseMotion(); Input.ActionPress("jump");
        if (_player.ModalOpen || !DisplayServer.WindowIsFocused()) return;
        if (Math.Abs(vehicle.Speed) > .03f) { _settleFrames = 0; return; }
        if (++_settleFrames < 4 || _exitSent) return;
        Input.ActionRelease("jump"); Pulse("interact"); _exitSent = true;
    }

    private void FinishResult(bool success, string reason)
    {
        SafeExit = success; if (!success) Fail(reason);
        ReleaseInput(); _phase = Phase.Done; _finish?.TrySetResult(success);
    }

    public string Receipt() => JsonSerializer.Serialize(new
    {
        schema = "urman.vehicle_performance_route.v1", vehicle = _vehicleId,
        status = _contactDiagnostics ? "DIAGNOSTIC"
            : string.IsNullOrEmpty(Failure) && SafeExit ? "PASS" : "INVALID",
        failure = Failure, phase = _phase.ToString(), setupSeconds = _setupSeconds - _drivingSeconds,
        drivingSeconds = _drivingSeconds, distanceMetres = _distance,
        measurementSeconds = _measurementSeconds, measurementDistanceMetres = _measurementDistance,
        movingSeconds = _movingSeconds, movingFraction = _measurementSeconds > 0 ? _movingSeconds / _measurementSeconds : 0,
        minimumMovingFraction = MinimumMovingFraction, minimumMeasuredDistance = MinimumMeasuredDistance,
        reversals = _reversals, safeExit = SafeExit, actualInput = true, poseWrites = 0,
        zone = _initialZone, route = _path,
        contactDiagnosticsRequested = _contactDiagnostics, diagnosticDrivingLimitSeconds = _contactDiagnostics ? 24 : 0,
        contactEvents = _contactEvents, firstCollisionStop = _vehicle?.DescribeFirstCollisionStop(),
        limits = "Explicit diagnostic driver in ordinary gameplay; not a human playthrough or listening acceptance."
    });

    private bool HasDriverCamera(VehicleController vehicle) => _fleet?.Occupied == vehicle
        && vehicle.Driver == _player && _player.VehicleControlled
        && GetViewport().GetCamera3D() == vehicle.VehicleCamera;

    private static bool RadioPlaying(VehicleController vehicle) => vehicle.Radio is { Enabled: true, PlayableSegmentCount: > 0 } radio
        && radio.GetChildren().OfType<AudioStreamPlayer3D>().Any(speaker => speaker.Playing && !speaker.StreamPaused && speaker.Stream is not null);

    private void AimFoot(Vector3 target)
    {
        var desired = (target - _footCamera.GlobalPosition).Normalized();
        var current = -_footCamera.GlobalBasis.Z;
        var yaw = Mathf.AngleDifference(Mathf.Atan2(-current.X, -current.Z), Mathf.Atan2(-desired.X, -desired.Z));
        var pitch = Mathf.Asin(Mathf.Clamp(desired.Y, -1, 1)) - Mathf.Asin(Mathf.Clamp(current.Y, -1, 1));
        var motion = new Vector2(-Mathf.RadToDeg(yaw), -Mathf.RadToDeg(pitch)) / _player.MouseSensitivity;
        if (motion.LengthSquared() > .0001f)
            Input.ParseInputEvent(new InputEventMouseMotion { Relative = motion, ScreenRelative = motion });
    }

    private static float RoadX(float z)
    {
        var axis = z >= AgentBAct1Layout.MainRoadAxis[^1].Y ? AgentBAct1Layout.MainRoadAxis : AgentBAct1Layout.ZiratRoadAxis;
        for (var index = 1; index < axis.Length; index++)
            if (z >= axis[index].Y)
                return Mathf.Lerp(axis[index - 1].X, axis[index].X,
                    Mathf.Clamp((z - axis[index - 1].Y) / (axis[index].Y - axis[index - 1].Y), 0, 1));
        return axis[^1].X;
    }
    private static Vector2 Flat(Vector3 value) => new(value.X, value.Z);
    private static float[] Coordinates(Vector3 value) => [value.X, value.Y, value.Z];
    private void ChangePhase(Phase phase) { _phase = phase; _phaseSeconds = 0; }
    private void Pulse(string action) { Input.ActionPress(action); _pulse = action; }
    private static void SetAxis(string negative, string positive, float value)
    {
        Input.ActionRelease(value >= 0 ? negative : positive);
        Input.ActionPress(value >= 0 ? positive : negative, Math.Abs(value));
    }
    private static void ReleaseMotion()
    {
        foreach (var action in Actions.Take(4)) Input.ActionRelease(action);
    }
    public void ReleaseInput()
    {
        foreach (var action in Actions) Input.ActionRelease(action);
        _pulse = null;
    }
    private void Fail(string reason)
    {
        if (string.IsNullOrEmpty(Failure)) Failure = reason;
        ReleaseInput();
    }
    public override void _ExitTree()
    {
        if (_contactDiagnostics && _vehicle is { } vehicle && GodotObject.IsInstanceValid(vehicle))
            vehicle.SetCollisionStopDiagnostics(false);
        ReleaseInput(); _finish?.TrySetResult(false);
    }
}
