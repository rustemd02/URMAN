using System;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Presentation-only body motion of the Niva: the shell rolls out of a corner,
/// squats under throttle, dives under the brake and rides the bumps on soft
/// springs, while the wheels stay on the ground. No physics, save or collision
/// role: the kinematic owner and its hull never see these angles.
/// </summary>
public partial class VehicleController
{
    // Tuning. Angles are radians per (m/s²); springs are stiffness / damping.
    private const float BodyRollPerLateralG = .0075f;
    private const float BodyRollMax = .055f;
    private const float BodyPitchPerForwardG = .0075f;
    private const float BodyPitchMax = .04f;
    private const float BodySpring = 85f;
    private const float BodyDamping = 8.5f;
    private const float BodyHeaveSpring = 120f;
    private const float BodyHeaveDamping = 9f;
    private const float BodyBumpHeight = .011f;
    private const float BodyPivotHeight = .36f;
    private const float DriverViewFollow = .55f;

    private float _bodyRoll, _bodyRollRate, _bodyPitchAngle, _bodyPitchRate, _bodyHeave, _bodyHeaveRate;
    private float _bodyLastSpeed;
    private Vector3[]? _wheelRest;

    /// <summary>Degrees the driver's view leans with the shell (roll, pitch).</summary>
    private Vector2 DriverViewLean => new(Mathf.RadToDeg(_bodyRoll) * DriverViewFollow, Mathf.RadToDeg(_bodyPitchAngle) * DriverViewFollow);

    private Transform3D NivaBodyTransform(float delta)
    {
        if (Definition.Kind != VehicleKind.Niva) return Transform3D.Identity;
        if (_wheelRest is null)
        {
            _wheelRest = new Vector3[_visualWheels.Length];
            for (var index = 0; index < _visualWheels.Length; index++) _wheelRest[index] = _visualWheels[index].Position;
        }
        if (delta > 0)
        {
            var dt = Math.Min(delta, .05f);
            var yawRate = -Speed / Definition.WheelBase * Mathf.Tan(_steering);
            var lateral = yawRate * Speed;
            var forward = Mathf.Clamp((Speed - _bodyLastSpeed) / dt, -12f, 12f);
            _bodyLastSpeed = Speed;
            var rollTarget = Mathf.Clamp(-lateral * BodyRollPerLateralG, -BodyRollMax, BodyRollMax);
            var pitchTarget = Mathf.Clamp(forward * BodyPitchPerForwardG, -BodyPitchMax, BodyPitchMax);
            // Road texture: two incommensurate waves over distance, louder with speed.
            var pace = Mathf.Clamp(Math.Abs(Speed) / 5f, 0f, 1f);
            var heaveTarget = (Mathf.Sin(TotalTravelMetres * 1.7f) * .6f + Mathf.Sin(TotalTravelMetres * 4.3f + 1.1f) * .4f)
                * BodyBumpHeight * pace;
            pitchTarget += Mathf.Sin(TotalTravelMetres * 2.6f) * .004f * pace;
            Spring(ref _bodyRoll, ref _bodyRollRate, rollTarget, BodySpring, BodyDamping, dt);
            Spring(ref _bodyPitchAngle, ref _bodyPitchRate, pitchTarget, BodySpring, BodyDamping, dt);
            Spring(ref _bodyHeave, ref _bodyHeaveRate, heaveTarget, BodyHeaveSpring, BodyHeaveDamping, dt);
        }
        var rotation = new Basis(Vector3.Right, _bodyPitchAngle) * new Basis(Vector3.Back, _bodyRoll);
        var pivot = new Vector3(0f, BodyPivotHeight, 0f);
        var body = new Transform3D(rotation, pivot - rotation * pivot + Vector3.Up * _bodyHeave);
        // Wheels are children of the shell; put them back on the road.
        var inverse = body.AffineInverse();
        for (var index = 0; index < _visualWheels.Length; index++)
            _visualWheels[index].Position = inverse * _wheelRest[index];
        return body;
    }

    private static void Spring(ref float value, ref float rate, float target, float stiffness, float damping, float dt)
    {
        rate += ((target - value) * stiffness - rate * damping) * dt;
        value += rate * dt;
    }
}
