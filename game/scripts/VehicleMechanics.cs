using System;

namespace Urman.Godot;

/// <summary>Surface family the tyre model uses for its friction coefficient.</summary>
public enum VehicleSurfaceKind { Road, Snow, Ice }

/// <summary>
/// Vehicle mechanics data for one authored vehicle, loaded from
/// <c>content/vehicles/*.json</c>. The field set mirrors the tunables of the
/// vendored engine: game/addons/gevp (MIT, see addons/gevp/LICENSE).
/// </summary>
public sealed class VehicleMechanicsProfile
{
    public float Mass { get; init; } = 1285f;
    public float MaxTorque { get; init; } = 116f;
    public float IdleRpm { get; init; } = 800f;
    public float MaxRpm { get; init; } = 4200f;
    public float EngineInertia { get; init; } = .4f;
    public float EngineBrakingTorque { get; init; } = 14f;
    public float[] ForwardGears { get; init; } = { 3.67f, 2.10f, 1.36f, 1.00f, 0.82f };
    public float ReverseGear { get; init; } = 3.53f;
    public float FinalDrive { get; init; } = 3.9f;
    public float ShiftTime { get; init; } = .35f;
    public float ShiftUpRpm { get; init; } = 2200f;
    public float ShiftDownRpm { get; init; } = 1200f;
    public float FrontTorqueSplit { get; init; } = .5f;
    public float FrontWeightDistribution { get; init; } = .52f;
    public float DragCoefficient { get; init; } = .52f;
    public float FrontalArea { get; init; } = 2.2f;
    public float RollingResistance { get; init; } = .02f;
    public float PeakGrip { get; init; } = .9f;
    public float SnowGrip { get; init; } = .62f;
    public float IceGrip { get; init; } = .3f;
    public float BrakeBias { get; init; } = .58f;
    /// <summary>Flat (rpm, torque multiplier) pairs, ascending rpm.</summary>
    public float[] TorqueCurve { get; init; } = { 800, .45f, 1400, .72f, 2400, .98f, 3200, 1f, 3800, .9f, 4200, .72f };
}

/// <summary>
/// Longitudinal/planar vehicle dynamics for the Niva: engine torque curve,
/// automatic gearbox with reverse, 4WD torque split, per-axle wheel spin,
/// brush-style tyre slip forces and a road/snow/ice friction table.
///
/// This is a C# adaptation of the vendored Godot Easy Vehicle Physics (GEVP)
/// model (game/addons/gevp/scripts/vehicle.gd, wheel.gd), MIT licence:
///   Motor torque curve / limiter      -> Step() engine section
///   Clutch + automatic transmission   -> Step() gearbox section (simplified as
///                                        a constrained engine-speed coupling)
///   front_torque_split (AWD)          -> front/rear drive split
///   Brake.max_handbrake_torque        -> rear-locked parking brake, clutch held
///                                        out only below walking speed
///   Brush tyre slip/saturating force  -> per-axle tanh slip curves with a
///                                        friction ellipse (combined slip)
///   coefficient_of_friction table     -> PeakGrip/SnowGrip/IceGrip per surface
///
/// The chassis itself stays the authored CharacterBody3D: this class only
/// produces the planar velocity and yaw the controller already knows how to
/// drive through MoveAndSlide, zone gates and collision checks. Axle spin is
/// integrated with a linearised (implicit) tyre derivative so the model stays
/// stable at the project's 60 Hz physics tick.
/// </summary>
public sealed class VehicleMechanics
{
    private const float Gravity = 9.81f;
    private const float AirDensity = 1.2f;
    private const float LongitudinalStiffness = 5.5f;
    private const float CorneringStiffness = 7.5f;
    private const float DriveEfficiency = .9f;
    private const float YawInertiaFactor = 1.5f;
    private const float LowSpeedBlend = 1.5f;
    private const float YawDamping = .9f;
    private const float HandbrakeHoldSpeed = .6f;
    private const float PedalCreep = .15f;

    private readonly VehicleMechanicsProfile _profile;
    private readonly float _wheelBase;
    private readonly float _wheelRadius;
    private readonly float _trackWidth;
    private readonly float _maxForwardSpeed;
    private readonly float _maxReverseSpeed;

    private float _rpm;
    private float _throttle;
    private int _gear;
    private float _shiftTimer;
    private float _shiftCooldown;
    private float _frontSpin;
    private float _rearSpin;
    private float _forwardSpeed;
    private float _lateralSpeed;
    private float _yawRate;
    private float _slipRatio;
    private float _slipAngle;
    private float _tractionLoss;
    private float _longitudinalLoadTransfer;
    private bool _serviceBraking;
    private bool _driveEngaged;

    public VehicleMechanics(VehicleMechanicsProfile profile, float wheelBase, float trackWidth,
        float wheelRadius, float maxForwardSpeed, float maxReverseSpeed)
    {
        _profile = profile;
        _wheelBase = Math.Max(.5f, wheelBase);
        _trackWidth = Math.Max(.5f, trackWidth);
        _wheelRadius = Math.Max(.15f, wheelRadius);
        _maxForwardSpeed = Math.Max(.5f, maxForwardSpeed);
        _maxReverseSpeed = Math.Max(.2f, maxReverseSpeed);
    }

    public float Rpm => _rpm;
    public int Gear => _gear;
    public bool Shifting => _shiftTimer > 0;
    public float ForwardSpeed => _forwardSpeed;
    public float LateralSpeed => _lateralSpeed;
    public float YawRate => _yawRate;
    public float YawDelta { get; private set; }
    public float WheelSpin => (_frontSpin + _rearSpin) * .5f;
    public float SlipRatio => _slipRatio;
    public float SlipAngle => _slipAngle;
    public float TractionLoss => _tractionLoss;
    public bool ServiceBraking => _serviceBraking;
    public bool DriveEngaged => _driveEngaged;
    public float MaxRpm => _profile.MaxRpm;

    public string GearLabel => _gear switch { 0 => "нейтраль", < 0 => "задний ход", _ => _gear + " передача" };

    public void Reset()
    {
        _rpm = 0; _throttle = 0; _gear = 0; _shiftTimer = 0; _shiftCooldown = 0;
        _frontSpin = _rearSpin = 0;
        _forwardSpeed = _lateralSpeed = _yawRate = 0; YawDelta = 0;
        _slipRatio = _slipAngle = _tractionLoss = _longitudinalLoadTransfer = 0;
        _serviceBraking = false; _driveEngaged = false;
    }

    /// <summary>Zero all momentum without clearing ignition-dependent idle state.</summary>
    public void Block()
    {
        _forwardSpeed = _lateralSpeed = _yawRate = 0; YawDelta = 0;
        _slipRatio = _slipAngle = _tractionLoss = 0;
        _frontSpin = _rearSpin = 0;
    }

    /// <summary>Idle update for parked/suspended frames (no position change).</summary>
    public void Idle(bool engineRunning, float dt)
    {
        dt = Math.Clamp(dt, .0005f, .05f);
        _throttle = MoveTowards(_throttle, 0, dt * 4f);
        _driveEngaged = false; _serviceBraking = false;
        var target = engineRunning ? _profile.IdleRpm : 0f;
        _rpm = MoveTowards(_rpm, target, dt * 2500f);
        _frontSpin = _rearSpin = 0; _slipRatio = _slipAngle = _tractionLoss = 0;
        YawDelta = 0;
    }

    /// <summary>
    /// Advance one physics tick. The controller applies the resulting planar
    /// velocity and yaw through its existing rotation sweep and collision checks.
    /// </summary>
    public void Step(float dt, float throttleInput, float steerRadians, bool serviceBrake,
        bool parkingBrake, bool engineRunning, float speedFactor, VehicleSurfaceKind surface)
    {
        dt = Math.Clamp(dt, .0005f, .05f);
        var forwardLimit = Math.Max(.25f, _maxForwardSpeed * Math.Clamp(speedFactor, .1f, 1f));
        var reverseLimit = Math.Max(.15f, _maxReverseSpeed * Math.Clamp(speedFactor, .1f, 1f));
        var surfaceGrip = surface switch
        {
            VehicleSurfaceKind.Snow => _profile.SnowGrip,
            VehicleSurfaceKind.Ice => _profile.IceGrip,
            _ => _profile.PeakGrip,
        };
        var throttle = Math.Clamp(throttleInput, -1f, 1f);

        // GEVP throttle_speed: smoothed pedal keeps small taps from snapping rpm.
        _throttle = MoveTowards(_throttle, throttle, dt * 5f);
        _shiftCooldown = Math.Max(0, _shiftCooldown - dt);

        var speedAbs = Math.Abs(_forwardSpeed);
        var nearRest = speedAbs < .55f && Math.Abs(_lateralSpeed) < .55f;
        // Shifts follow the ideal (rolling) wheel speed rather than the
        // slipping engine: GEVP lets the tires spin without upshifting.
        var idealGearRpm = speedAbs / _wheelRadius * (float)(60.0 / (Math.PI * 2.0))
            * Math.Abs(GearRatio(_gear) * _profile.FinalDrive);

        // ---- Gear selection (GEVP process_transmission) -------------------
        if (_shiftTimer > 0) _shiftTimer = Math.Max(0, _shiftTimer - dt);
        if (_shiftTimer <= 0 && _shiftCooldown <= 0 && engineRunning)
        {
            // Small threshold: the authored reverse legs drive with a gentle
            // proportional throttle down to ~0.11, so any deliberate pedal
            // must engage the direction, like the kinematic model did.
            if (throttle < -.05f && nearRest && _gear >= 0) StartShift(-1);
            else if (throttle > .05f && _forwardSpeed > -.4f && _gear <= 0) StartShift(1);
            else if (_gear > 0 && speedAbs > .5f)
            {
                if (idealGearRpm > _profile.ShiftUpRpm && _gear < _profile.ForwardGears.Length) StartShift(1);
                else if (idealGearRpm < _profile.ShiftDownRpm && _gear > 1) StartShift(-1);
            }
        }
        var totalRatio = GearRatio(_gear) * _profile.FinalDrive;
        var shifting = _shiftTimer > 0;
        // The parking brake holds a stopped car with the clutch held out, so the
        // engine can be revved against it. Above walking speed the drivetrain
        // stays connected and the rear-axle lock becomes a handbrake turn, not a
        // clutch cut (GEVP keeps the drivetrain connected under handbrake too).
        var handbrakeHold = parkingBrake && speedAbs < HandbrakeHoldSpeed && Math.Abs(_lateralSpeed) < HandbrakeHoldSpeed;
        var clutchIn = _gear == 0 || shifting || !engineRunning || handbrakeHold;

        // The pedal in the gear's own direction; reverse differs only by the
        // sign of the gear ratio, which turns positive torque into reverse
        // wheel torque.
        var trip = _gear < 0 ? -_throttle : _throttle;

        // ---- Engine (GEVP Motor + process_clutch) --------------------------
        var drivetrainRpm = Math.Abs((_frontSpin + _rearSpin) * .5f) * Math.Abs(totalRatio) * (float)(60.0 / (Math.PI * 2.0));
        if (clutchIn)
        {
            var target = engineRunning
                ? _profile.IdleRpm + Math.Max(0, _throttle) * (_profile.MaxRpm - _profile.IdleRpm) * .92f
                : 0f;
            _rpm = MoveTowards(_rpm, target, dt * (engineRunning ? 3200f : 2000f));
        }
        else if (Math.Abs(_throttle) > .02f && speedAbs < 2.5f && drivetrainRpm < _profile.IdleRpm + 120f)
        {
            // Launch slip (GEVP need_clutch below idle): the engine revs toward
            // the pedal target while the clutch slips, so a standing start
            // pulls with real torque instead of idling against the drivetrain.
            var target = _profile.IdleRpm + Math.Max(0, trip) * (_profile.MaxRpm - _profile.IdleRpm) * .75f;
            _rpm = MoveTowards(_rpm, target, dt * 2600f);
        }
        else
        {
            // Locked: engine speed follows the wheels; wheelspin on ice
            // therefore raises rpm into the limiter, like GEVP torque feedback.
            _rpm = MoveTowards(_rpm, Math.Max(_profile.IdleRpm, drivetrainRpm), dt * 2600f);
        }
        _rpm = Math.Clamp(_rpm, 0, _profile.MaxRpm);

        var limiter = _rpm >= _profile.MaxRpm ? 0f : 1f;
        var torqueFactor = SampleTorqueCurve(_rpm);
        // Small idle creep: gentle pedal travel still delivers usable torque,
        // so a low-throttle reverse creep does not stall against rolling
        // resistance (full throttle is unchanged).
        var pedal = Math.Max(0, trip);
        if (pedal > 0) pedal = PedalCreep + (1f - PedalCreep) * pedal;
        var engineTorque = pedal * _profile.MaxTorque * torqueFactor * limiter;
        if (_throttle <= .02f && !clutchIn && speedAbs > .5f)
            engineTorque = -_profile.EngineBrakingTorque;
        var wheelDrive = clutchIn ? 0f : engineTorque * totalRatio * DriveEfficiency;
        if (_forwardSpeed > forwardLimit && wheelDrive > 0) wheelDrive = 0;
        if (_forwardSpeed < -reverseLimit && wheelDrive < 0) wheelDrive = 0;

        // ---- Brakes (GEVP Brake.max_torque / max_handbrake_torque) --------
        _serviceBraking = serviceBrake
            || (!parkingBrake && throttle < -.1f && _forwardSpeed > .4f)
            || (!parkingBrake && throttle > .1f && _forwardSpeed < -.4f);
        var brakeTorqueFront = 0f;
        var brakeTorqueRear = 0f;
        if (_serviceBraking)
        {
            var total = _profile.Mass * 7.5f * _wheelRadius;
            brakeTorqueFront = total * _profile.BrakeBias;
            brakeTorqueRear = total * (1 - _profile.BrakeBias);
        }
        if (parkingBrake)
        {
            // GEVP marks the rear axle as the handbrake axle. Above walking
            // speed the handbrake stays rear-only, so the rear slides and the
            // front keeps steering; a slow front share keeps the authored
            // parking hold (and its stop) firm.
            var total = _profile.Mass * 9f * _wheelRadius;
            brakeTorqueRear = Math.Max(brakeTorqueRear, total * .8f);
            if (speedAbs < 3f) brakeTorqueFront = Math.Max(brakeTorqueFront, total * .5f);
        }

        // ---- Axle loads with longitudinal transfer ------------------------
        var mass = _profile.Mass;
        var wheelBase = _wheelBase;
        var frontArm = wheelBase * (1 - _profile.FrontWeightDistribution);
        var rearArm = wheelBase * _profile.FrontWeightDistribution;
        var staticLoad = mass * Gravity;
        var transfer = mass * _longitudinalLoadTransfer * .55f / wheelBase;
        var frontLoad = Math.Clamp(staticLoad * rearArm / wheelBase - transfer, staticLoad * .1f, staticLoad * .9f);
        var rearLoad = Math.Clamp(staticLoad * frontArm / wheelBase + transfer, staticLoad * .1f, staticLoad * .9f);

        // ---- Per-axle tyre slip and forces (GEVP modified brush model) -----
        var steer = steerRadians;
        var frontLateralVelocity = _lateralSpeed - frontArm * _yawRate;
        var rearLateralVelocity = _lateralSpeed + rearArm * _yawRate;
        var speedReference = Math.Max(speedAbs, .8f);
        var slipAngleFront = (float)Math.Atan2(frontLateralVelocity, speedReference) - steer;
        var slipAngleRear = (float)Math.Atan2(rearLateralVelocity, speedReference);
        var slipRatioFront = (_frontSpin * _wheelRadius - _forwardSpeed) / Math.Max(speedAbs, 1f);
        var slipRatioRear = (_rearSpin * _wheelRadius - _forwardSpeed) / Math.Max(speedAbs, 1f);

        AxleForces(slipRatioFront, slipAngleFront, frontLoad, surfaceGrip,
            out var frontLongitudinal, out var frontLateral);
        AxleForces(slipRatioRear, slipAngleRear, rearLoad, surfaceGrip,
            out var rearLongitudinal, out var rearLateral);

        var frontFriction = surfaceGrip * frontLoad;
        var rearFriction = surfaceGrip * rearLoad;
        UpdateAxleSpin(ref _frontSpin, wheelDrive * _profile.FrontTorqueSplit, brakeTorqueFront,
            frontLongitudinal, totalRatio, SpinDamping(frontLongitudinal, frontFriction, speedReference), dt);
        UpdateAxleSpin(ref _rearSpin, wheelDrive * (1 - _profile.FrontTorqueSplit), brakeTorqueRear,
            rearLongitudinal, totalRatio, SpinDamping(rearLongitudinal, rearFriction, speedReference), dt);
        // Recompute slip after spin update so the reported force matches the
        // wheels that will actually push the chassis this tick.
        slipRatioFront = (_frontSpin * _wheelRadius - _forwardSpeed) / Math.Max(speedAbs, 1f);
        slipRatioRear = (_rearSpin * _wheelRadius - _forwardSpeed) / Math.Max(speedAbs, 1f);
        AxleForces(slipRatioFront, slipAngleFront, frontLoad, surfaceGrip, out frontLongitudinal, out frontLateral);
        AxleForces(slipRatioRear, slipAngleRear, rearLoad, surfaceGrip, out rearLongitudinal, out rearLateral);

        _slipRatio = Math.Max(Math.Abs(slipRatioFront), Math.Abs(slipRatioRear));
        _slipAngle = Math.Max(Math.Abs(slipAngleFront), Math.Abs(slipAngleRear));
        _tractionLoss = Math.Clamp((_slipRatio - .12f) * 2.2f + (_slipAngle - .10f) * 2.4f, 0, 1);

        // ---- Rigid planar body on the kinematic chassis --------------------
        var drag = .5f * AirDensity * _profile.DragCoefficient * _profile.FrontalArea
            * _forwardSpeed * speedAbs;
        var rolling = speedAbs > .1f ? _profile.RollingResistance * staticLoad * Math.Sign(_forwardSpeed) : 0;
        if (_forwardSpeed > forwardLimit) drag += surfaceGrip * mass * .8f;
        if (_forwardSpeed < -reverseLimit) drag -= surfaceGrip * mass * .8f;
        var longitudinalForce = frontLongitudinal + rearLongitudinal - drag - rolling;
        var lateralForce = frontLateral + rearLateral;
        var yawMoment = -frontArm * frontLateral + rearArm * rearLateral;
        var yawInertia = mass * (_wheelBase * _wheelBase + _trackWidth * _trackWidth) / 12f * YawInertiaFactor;

        var previousForward = _forwardSpeed;
        _forwardSpeed += (longitudinalForce / mass + _yawRate * _lateralSpeed) * dt;
        _lateralSpeed += (lateralForce / mass - _yawRate * _forwardSpeed) * dt;
        _yawRate += yawMoment / yawInertia * dt;
        // Light yaw stabiliser (GEVP stability_yaw_strength analogue): kills the
        // 60 Hz planar oscillation without holding a handbrake turn.
        _yawRate *= Math.Max(0f, 1f - YawDamping * dt);
        _lateralSpeed = Math.Clamp(_lateralSpeed, -15f, 15f);
        _yawRate = Math.Clamp(_yawRate, -3f, 3f);
        _longitudinalLoadTransfer = (_forwardSpeed - previousForward) / dt;

        // Low-speed blend with the authored bicycle model: parking and walking
        // speed handling keeps its existing, tested feel; the slip model takes
        // over above 1.5 m/s where drifting is actually possible.
        var blend = Math.Clamp(speedAbs / LowSpeedBlend, 0, 1);
        var bicycleYaw = -_forwardSpeed / wheelBase * (float)Math.Tan(steer);
        _yawRate = _yawRate * blend + bicycleYaw * (1 - blend);
        _lateralSpeed *= blend;

        if (Math.Abs(_forwardSpeed) < .02f && Math.Abs(_throttle) < .05f && !serviceBrake && !parkingBrake)
            _forwardSpeed = 0;

        if (parkingBrake && Math.Abs(_forwardSpeed) < .35f && Math.Abs(_lateralSpeed) < .35f)
        {
            _forwardSpeed = _lateralSpeed = _yawRate = 0;
            _frontSpin = _rearSpin = 0;
        }
        _driveEngaged = !clutchIn;
        YawDelta = _yawRate * dt;
    }

    /// <summary>
    /// Saturating tyre force for one axle: combined longitudinal/lateral slip
    /// clamped to the friction circle. GEVP's brush model caps the force at the
    /// surface friction; this adaptation keeps the saturating shape with a
    /// tanh curve so the kinematic chassis stays stable at 60 Hz.
    /// </summary>
    private static float AxleForces(float slipRatio, float slipAngle, float load, float grip,
        out float longitudinal, out float lateral)
    {
        var friction = grip * load;
        longitudinal = friction * (float)Math.Tanh(LongitudinalStiffness * Math.Clamp(slipRatio, -8f, 8f));
        lateral = friction * (float)Math.Tanh(-CorneringStiffness * Math.Clamp(slipAngle, -1.2f, 1.2f));
        var magnitude = (float)Math.Sqrt(longitudinal * longitudinal + lateral * lateral);
        if (magnitude > friction && magnitude > .0001f)
        {
            var scale = friction / magnitude;
            longitudinal *= scale;
            lateral *= scale;
        }
        return magnitude;
    }

    /// <summary>
    /// Linearised d(tyre torque)/d(axle spin) at the current operating point,
    /// in N m per rad/s. Subtracting it in the denominator of the spin update
    /// makes the launch integration implicit, which is what keeps the tanh
    /// slip curve stable at 60 Hz (the explicit form rings at ~2.6 ms).
    /// </summary>
    private float SpinDamping(float longitudinalForce, float friction, float speedReference)
    {
        if (friction <= .0001f) return 0f;
        var slope = Math.Clamp(longitudinalForce / friction, -1f, 1f);
        return friction * LongitudinalStiffness * (1f - slope * slope) * _wheelRadius * _wheelRadius
            / Math.Max(speedReference, .5f);
    }

    private void UpdateAxleSpin(ref float spin, float driveTorque, float brakeTorque,
        float longitudinalForce, float totalRatio, float damping, float dt)
    {
        // Reflected drivetrain inertia keeps the wheel from changing speed
        // instantly under clutch/gear torque (GEVP wheel_moment + drive_inertia).
        var inertia = 2.6f + .02f * Math.Min(400f, totalRatio * totalRatio);
        var reaction = longitudinalForce * _wheelRadius;
        var net = driveTorque - reaction;
        if (brakeTorque > 0 && Math.Abs(spin) > .01f) net -= brakeTorque * Math.Sign(spin);
        var next = spin + net / (inertia + damping * dt) * dt;
        if (brakeTorque > 0 && spin != 0 && Math.Sign(next) != Math.Sign(spin)
            && brakeTorque > Math.Abs(driveTorque - reaction)) next = 0;
        if (brakeTorque > 0 && Math.Abs(next) < .6f && Math.Abs(_forwardSpeed) > .2f) next = 0;
        spin = Math.Clamp(next, -300f, 300f);
    }

    private void StartShift(int direction)
    {
        var target = _gear + direction;
        if (target < -1 || target > _profile.ForwardGears.Length) return;
        // GEVP complete_shift: engaging or leaving neutral is immediate because
        // the clutch is already out there; only a real gear change takes
        // shift_time. A short cooldown, not a full shift_time, follows so a
        // direction reversal at rest (1 -> N -> R) stays responsive.
        if (_gear == 0)
        {
            _gear = target;
            _shiftTimer = 0;
            _shiftCooldown = .05f;
            return;
        }
        _gear = target;
        _shiftTimer = Math.Max(.1f, _profile.ShiftTime);
        _shiftCooldown = _profile.ShiftTime + .05f;
    }

    private float GearRatio(int gear) => gear switch
    {
        0 => 0f,
        < 0 => -_profile.ReverseGear,
        _ => _profile.ForwardGears[Math.Min(gear, _profile.ForwardGears.Length) - 1],
    };

    private float SampleTorqueCurve(float rpm)
    {
        var curve = _profile.TorqueCurve;
        if (curve.Length < 4) return .8f;
        var previousRpm = curve[0];
        var previousValue = curve[1];
        if (rpm <= previousRpm) return previousValue;
        for (var index = 2; index + 1 < curve.Length; index += 2)
        {
            var nextRpm = curve[index];
            var nextValue = curve[index + 1];
            if (rpm <= nextRpm)
            {
                var t = (rpm - previousRpm) / Math.Max(1f, nextRpm - previousRpm);
                return previousValue + (nextValue - previousValue) * t;
            }
            previousRpm = nextRpm; previousValue = nextValue;
        }
        return previousValue;
    }

    private static float MoveTowards(float from, float to, float delta)
        => Math.Abs(to - from) <= delta ? to : from + Math.Sign(to - from) * delta;
}
