using Godot;

namespace Urman.Godot;

/// <summary>
/// The Niva's rear-view-mirror charm (shamail medallion + tasbih, authored in
/// tools/blender/generate_niva.py with its local origin at the hanging point):
/// a measured damped pendulum instead of the legacy scripted swing.
///
/// Legacy behaviour, kept here only as the measured baseline:
/// VehicleController.SwingCharm rings the charm at w^2=38 (w=6.16 rad/s,
/// T=1.02 s) with damping 1.6 s^-1 (zeta=0.13, ~5 s to settle) and jumps the
/// equilibrium to the +-0.35 rad (20 deg) clamp on every brake and collision
/// stop, because its target is the raw one-tick Speed delta against gravity.
/// That file belongs to the physics lane (it is being rewritten there), so
/// this node is deliberately NOT returned as Visual.Charm (the factory returns
/// Charm:null for the Niva), which disables that code path without editing it.
/// Do not re-attach this node to Visual.Charm.
///
/// Replacement model (all measurements read-only from VehicleController):
///   a_fwd = LPF((Speed(t)-Speed(t-dt))/dt, tau=.07 s), clamped +-6 m/s^2;
///   a_lat = LPF(dYaw/dt * Speed, tau=.07 s), clamped +-6 m/s^2, the yaw rate
///           measured from the car's actual basis (identical to the applied
///           bicycle yaw used by the old code);
///   eq_pitch = clamp(atan2(-a_fwd, g), +-0.16)   (9.2 deg, windscreen side)
///   eq_roll  = clamp(atan2( a_lat, g), +-0.21)   (12.0 deg, side to side)
///   angle'' = w^2 (eq - angle) - 2 zeta w angle',  w=6.6 rad/s (L~0.23 m),
///             zeta=0.33 (2 zeta w=4.36 s^-1; 4 envelopes ~1.8 s).
/// Rendering adds a kinematic idle residual of 0.0040 rad pitch / 0.0028 rad
/// roll at 21.5 Hz while the engine idles at standstill; otherwise the
/// oscillator decays to exactly zero. The dynamic angle is hard-clamped to the
/// same equilibrium limits, so the worst tip excursion (the tassel 0.271 m
/// below the pivot, node scale 0.82) is <=0.045 m. The windscreen is ~0.17 m
/// ahead at that height, the dash top is 0.16 m below, and every charm vertex
/// hangs below the pivot, so rotation about the pivot cannot reach the mirror
/// or the glass.
/// </summary>
public partial class VehicleMirrorCharm : Node3D
{
    private const float Gravity = 9.81f;
    private const float AngularFrequency = 6.6f;
    private const float DampingRatio = 0.33f;
    private const float PitchLimit = 0.16f;
    private const float RollLimit = 0.21f;
    private const float SpecificForceLimit = 6f;
    private const float FilterTime = 0.07f;
    private const float IdlePitch = 0.0040f;
    private const float IdleRoll = 0.0028f;
    private const float IdleFrequency = 21.5f;
    private const float TeleportDistance = 2f;

    private VehicleController? _vehicle;
    private Vector3 _lastVehiclePosition = new(float.NaN, 0f, 0f);
    private float _lastSpeed, _lastYaw;
    private float _forwardAccel, _lateralAccel;
    private float _pitch, _pitchRate, _roll, _rollRate;
    private float _idleBlend, _idlePhase;
    private float _reportedPitch = float.NaN, _reportedRoll = float.NaN;

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("visualOnly", true);
        SetMeta("collisionOwner", "none");
        SetMeta("savePolicy", "session-only pendulum state; never serialized");
        SetMeta("charmSwingModel",
            "damped pendulum driven by measured specific force: w^2=43.6, 2*zeta*w=4.36 s^-1, limits 9.2 deg pitch / 12.0 deg roll, idle residual 0.23 deg @21.5 Hz");
        SetMeta("charmLegacySwing", "VehicleController.SwingCharm is bypassed on purpose (Visual.Charm is null for the Niva); do not re-attach");
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = Mathf.Min((float)delta, .05f);
        if (dt <= 0f) return;
        if (_vehicle is null || !GodotObject.IsInstanceValid(_vehicle) || !_vehicle.IsInsideTree())
        {
            _vehicle = FindVehicle();
            if (_vehicle is null) return;
            _lastSpeed = _vehicle.Speed;
            _lastYaw = Yaw(_vehicle);
            _lastVehiclePosition = _vehicle.GlobalPosition;
        }
        var position = _vehicle.GlobalPosition;
        // Session boundary or debug spawn: start the pendulum at rest instead
        // of carrying a swing across the loaded pose.
        if (!float.IsNaN(_lastVehiclePosition.X)
            && new Vector2(position.X - _lastVehiclePosition.X, position.Z - _lastVehiclePosition.Z).Length() > TeleportDistance)
        {
            _pitch = _pitchRate = _roll = _rollRate = 0f;
            _forwardAccel = _lateralAccel = 0f;
            _lastSpeed = _vehicle.Speed;
            _lastYaw = Yaw(_vehicle);
        }
        _lastVehiclePosition = position;

        var speed = _vehicle.Speed;
        var rawForward = (speed - _lastSpeed) / dt;
        _lastSpeed = speed;
        var yaw = Yaw(_vehicle);
        var yawRate = Mathf.Wrap(yaw - _lastYaw, -Mathf.Pi, Mathf.Pi) / dt;
        _lastYaw = yaw;
        var blend = dt / (FilterTime + dt);
        _forwardAccel += (Mathf.Clamp(rawForward, -12f, 12f) - _forwardAccel) * blend;
        _lateralAccel += (Mathf.Clamp(yawRate * speed, -12f, 12f) - _lateralAccel) * blend;

        var forward = Mathf.Clamp(_forwardAccel, -SpecificForceLimit, SpecificForceLimit);
        var lateral = Mathf.Clamp(_lateralAccel, -SpecificForceLimit, SpecificForceLimit);
        var pitchEquilibrium = Mathf.Clamp(Mathf.Atan2(-forward, Gravity), -PitchLimit, PitchLimit);
        var rollEquilibrium = Mathf.Clamp(Mathf.Atan2(lateral, Gravity), -RollLimit, RollLimit);
        var stiffness = AngularFrequency * AngularFrequency;
        var damping = 2f * DampingRatio * AngularFrequency;
        _pitchRate += (stiffness * (pitchEquilibrium - _pitch) - damping * _pitchRate) * dt;
        _rollRate += (stiffness * (rollEquilibrium - _roll) - damping * _rollRate) * dt;
        _pitch = Mathf.Clamp(_pitch + _pitchRate * dt, -PitchLimit, PitchLimit);
        _roll = Mathf.Clamp(_roll + _rollRate * dt, -RollLimit, RollLimit);
        if ((_pitch >= PitchLimit && _pitchRate > 0f) || (_pitch <= -PitchLimit && _pitchRate < 0f)) _pitchRate = 0f;
        if ((_roll >= RollLimit && _rollRate > 0f) || (_roll <= -RollLimit && _rollRate < 0f)) _rollRate = 0f;

        var idleTarget = _vehicle.EngineRunning && Mathf.Abs(speed) < .3f ? 1f : 0f;
        _idleBlend = Mathf.MoveToward(_idleBlend, idleTarget, dt * 4f);
        _idlePhase += dt;
        var idlePitch = _idleBlend * IdlePitch * Mathf.Sin(_idlePhase * Mathf.Tau * IdleFrequency);
        var idleRoll = _idleBlend * IdleRoll * Mathf.Sin(_idlePhase * Mathf.Tau * IdleFrequency * .83f + 1.1f);
        Rotation = new Vector3(_pitch + idlePitch, 0f, _roll + idleRoll);
        Report();
    }

    private void Report()
    {
        // Diagnostics are throttled to a quarter degree so the tick stays free
        // of per-frame metadata traffic.
        if (!float.IsNaN(_reportedPitch) && Mathf.Abs(_pitch - _reportedPitch) < .0044f
            && Mathf.Abs(_roll - _reportedRoll) < .0044f) return;
        _reportedPitch = _pitch;
        _reportedRoll = _roll;
        SetMeta("charmPitchDegrees", Mathf.RadToDeg(_pitch));
        SetMeta("charmRollDegrees", Mathf.RadToDeg(_roll));
    }

    private VehicleController? FindVehicle()
    {
        for (var node = GetParent(); node is not null; node = node.GetParent())
            if (node is VehicleController controller) return controller;
        return null;
    }

    private static float Yaw(VehicleController vehicle)
    {
        var forward = -vehicle.GlobalBasis.Z;
        return Mathf.Atan2(forward.X, forward.Z);
    }
}
