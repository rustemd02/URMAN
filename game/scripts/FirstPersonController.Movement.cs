using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    // Feel tuning (author pass 2026-11-12): brisk walk 2.4 m/s, run 4.2 m/s,
    // about .15 s to full walking speed and a slightly quicker stop.
    private const float SprintMultiplier = 4.2f / 2.4f;
    private const float JumpHeight = .72f;
    // Arcade jump model: a press a moment before landing or a moment after
    // leaving an edge still jumps; releasing early makes a short hop; the fall
    // is heavier than the rise, so the arc is snappy instead of floaty.
    private const float JumpBufferSeconds = .14f;
    private const float CoyoteSeconds = .12f;
    private const float RiseGravityScale = 1.15f;
    private const float ShortHopGravityScale = 2.4f;
    private const float FallGravityScale = 1.9f;
    private const float TerminalFallSpeed = 26f;
    private const float ReverseAccelerationBoost = 1.7f;
    private const float AirDrag = .6f;
    private const float HardLandingSpeed = 6.5f;
    private const float HardLandingKeep = .72f;
    // Running slide: crouch while sprinting.
    private const float SlideSeconds = .85f;
    private const float SlideStartSpeed = 5.4f;
    private const float SlideFriction = 3.6f;
    private const float SlideSteer = 1.6f;
    private const float SlideEndSpeed = 1.5f;
    private const float GroundAcceleration = 16f;
    private const float GroundDeceleration = 21f;
    // Camera feel. All of it is presentation only and is disabled by ReducedMotion.
    private const float WalkBobHeight = .024f;
    private const float SprintBobHeight = .04f;
    private const float BobSway = .012f;
    private const float StrafeRollDegrees = .9f;
    private const float SprintFovKick = 4.5f;
    private const float LandingDipPerMeterSecond = .012f;
    private const float LandingDipMax = .07f;
    private const float AirControlAcceleration = 12f;
    private uint _walkingCollisionLayer;
    private uint _walkingCollisionMask;

    public bool IsSprinting { get; private set; }
    public bool VehicleControlled { get; private set; }
    internal int GroundedJumps { get; private set; }

    private float _paceScale = 1f;
    private float _paceScaleHold;

    /// <summary>Presentation-only pace for a scene (deep dream snow). The caller
    /// refreshes it every tick; it lapses by itself a quarter second after the
    /// last call, so a scene that goes away can never leave the walker slow.</summary>
    public void HoldPaceScale(float scale)
    {
        _paceScale = Mathf.Clamp(scale, .4f, 1.2f);
        _paceScaleHold = .25f;
    }

    // A person's stride, not the leg solver's reach: a long easy step at a
    // walk, a bounding one at a run, short when crouched. Footfall sounds and the
    // head's rise and fall follow this cadence (roughly two steps a second
    // walking, three running), so the walk reads as unhurried and human.
    private const float WalkStepMeters = .95f;
    private const float RunStepMeters = 1.45f;
    private const float CrouchStepMeters = .55f;
    private float _strideTravel;
    private float _strideStep = WalkStepMeters;

    /// <summary>Footfalls since the scene began; each increment is one step landing.</summary>
    public ulong StrideContactCount { get; private set; }
    /// <summary>Length of the step that is being taken now, in metres.</summary>
    public float StrideStepMeters => _strideStep;
    /// <summary>0..1 across two steps (left then right).</summary>
    private float StridePhase => ((StrideContactCount & 1) == 0 ? 0f : .5f) + Mathf.Clamp(_strideTravel / _strideStep, 0f, 1f) * .5f;

    private void AdvanceStride(float distance, float delta)
    {
        var pace = delta > 0f ? distance / delta : 0f;
        var target = IsCrouching ? CrouchStepMeters
            : Mathf.Lerp(WalkStepMeters, RunStepMeters, Mathf.Clamp((pace - WalkSpeed) / Mathf.Max(WalkSpeed * (SprintMultiplier - 1f), .1f), 0f, 1f));
        _strideStep = Mathf.MoveToward(_strideStep, target, delta * 1.2f);
        if (distance <= 0f)
        {
            // Standing still or in the air: the next step starts fresh.
            if (pace < .05f) _strideTravel = Mathf.MoveToward(_strideTravel, 0f, delta * .6f);
            return;
        }
        _strideTravel += distance;
        if (_strideTravel < _strideStep) return;
        _strideTravel -= _strideStep;
        StrideContactCount++;
    }

    private float _jumpBuffer;
    private float _coyote;
    private bool _jumpRising;
    private float _slideLeft;
    private bool _moveWasGrounded = true;

    public bool IsSliding => _slideLeft > 0f;

    private void ApplyHorizontalMovement(Vector2 input, float speed, float delta, bool grounded)
    {
        if (_paceScaleHold > 0f && (_paceScaleHold -= delta) <= 0f) _paceScale = 1f;
        var current = new Vector2(Velocity.X, Velocity.Z);
        if (grounded && !_moveWasGrounded && _bodyVerticalSpeedBeforeMove < -HardLandingSpeed)
            current *= HardLandingKeep;
        _moveWasGrounded = grounded;
        if (UpdateSlide(input, delta, grounded, ref current))
        {
            Velocity = new(current.X, Velocity.Y, current.Y);
            return;
        }
        var target = input * speed * _paceScale;
        var hasInput = target.LengthSquared() > .0001f;
        if (grounded)
        {
            var acceleration = hasInput ? GroundAcceleration : GroundDeceleration;
            // Turning back on yourself bites harder than starting from rest.
            if (hasInput && current.Dot(target) < 0f) acceleration *= ReverseAccelerationBoost;
            current = current.MoveToward(target, acceleration * delta);
        }
        else
        {
            // In the air momentum is kept: steering turns the velocity, it does
            // not brake it down to walking speed (a running jump stays long).
            var carried = Mathf.Max(0f, current.Length() - AirDrag * delta);
            if (hasInput)
            {
                current = current.MoveToward(target, AirControlAcceleration * delta);
                if (current.Length() < carried && current.Dot(target) > 0f)
                    current = current.Normalized() * carried;
            }
            else if (current.LengthSquared() > .0001f) current = current.Normalized() * carried;
        }
        Velocity = new(current.X, Velocity.Y, current.Y);
    }

    private void BeginSlide()
    {
        var current = new Vector2(Velocity.X, Velocity.Z);
        if (current.Length() < WalkSpeed * 1.2f || !IsOnFloor()) return;
        current = current.Normalized() * Mathf.Max(current.Length(), SlideStartSpeed * _paceScale);
        Velocity = new(current.X, Velocity.Y, current.Y);
        _slideLeft = SlideSeconds;
    }

    private bool UpdateSlide(Vector2 input, float delta, bool grounded, ref Vector2 current)
    {
        if (_slideLeft <= 0f) return false;
        _slideLeft -= delta;
        var speed = current.Length();
        if (!grounded || !IsCrouching || speed < SlideEndSpeed || _slideLeft <= 0f)
        {
            _slideLeft = 0f;
            // Come up out of the slide by yourself where there is headroom.
            if (grounded && IsCrouching && CanStandAt(GlobalPosition)) SetCrouched(false, smoothPresentation: true);
            return false;
        }
        var heading = current / speed;
        if (input.LengthSquared() > .0001f) heading = (heading + input.Normalized() * SlideSteer * delta).Normalized();
        current = heading * Mathf.MoveToward(speed, 0f, SlideFriction * delta);
        return true;
    }

    private float VerticalGravityScale()
    {
        if (Velocity.Y <= 0f) { _jumpRising = false; return FallGravityScale; }
        return _jumpRising && !Input.IsActionPressed("jump") ? ShortHopGravityScale : RiseGravityScale;
    }

    private void ApplyAirGravity(float delta)
    {
        var vertical = Velocity.Y - _gravity * VerticalGravityScale() * delta;
        Velocity = new Vector3(Velocity.X, Mathf.Max(vertical, -TerminalFallSpeed), Velocity.Z);
    }

    private void TryJumpFromGround(float delta)
    {
        var grounded = IsOnFloor();
        _coyote = grounded ? CoyoteSeconds : Mathf.Max(0f, _coyote - delta);
        _jumpBuffer = Input.IsActionJustPressed("jump") ? JumpBufferSeconds : Mathf.Max(0f, _jumpBuffer - delta);
        if (_worldInteractionNeedsRelease || _jumpBuffer <= 0f || _coyote <= 0f || Velocity.Y > .05f) return;
        _jumpBuffer = 0f;
        RefreshCarryCoordinator();
        if (_carryCoordinator is { HeldItem: not null } or { ActionInProgress: true })
        {
            NotifyTraversal("Сначала поставьте предмет и освободите руки для прыжка.");
            return;
        }
        if (IsCrouching)
        {
            if (!CanStandAt(GlobalPosition))
            {
                NotifyTraversal("Сверху тесно. Сначала выйдите туда, где можно встать.");
                return;
            }
            SetCrouched(false, smoothPresentation: true);
        }
        // One hop per press. Out of a slide the horizontal speed is kept, so
        // slide + jump is the long jump. The step solver never cancels a rise.
        _slideLeft = 0f;
        _coyote = 0f;
        _jumpRising = true;
        Velocity = new(Velocity.X, Mathf.Sqrt(2f * _gravity * RiseGravityScale * JumpHeight), Velocity.Z);
        GroundedJumps++;
    }

    public bool CanEnterVehicle(out string reason)
    {
        RefreshCarryCoordinator();
        reason = VehicleControlled ? "Сначала выйдите из транспорта."
            : ModalOpen ? "Сначала закончите открытое действие."
            : IsClimbingLadder ? "Сначала сойдите с лестницы."
            : _carryCoordinator is { ActionInProgress: true } ? "Подождите завершения действия с предметом."
            : _carryCoordinator is { HeldItem: not null } ? "Сначала поставьте предмет."
            : !IsOnFloor() ? "Сначала встаньте на землю."
            : !CanStandAt(GlobalPosition) ? "Здесь тесно. Подойдите к двери снаружи."
            : string.Empty;
        return reason.Length == 0;
    }

    public bool TryBeginVehicleControl(out string reason)
    {
        if (!CanEnterVehicle(out reason)) return false;
        SetVehicleControl(true);
        return true;
    }

    /// <summary>
    /// The transport owner validates ordinary entry/exit and saved occupancy.
    /// This only hands over pedestrian presentation, with no narrative/save state.
    /// </summary>
    public void SetVehicleControl(bool active)
    {
        if (VehicleControlled == active) return;
        if (active)
        {
            ReleaseLadderForPlacement();
            _walkingCollisionLayer = CollisionLayer;
            _walkingCollisionMask = CollisionMask;
            CollisionLayer = 0;
            CollisionMask = 0;
        }
        else
        {
            CollisionLayer = _walkingCollisionLayer;
            CollisionMask = _walkingCollisionMask;
            _camera.MakeCurrent();
            RestoreStanceAtDestination();
        }
        VehicleControlled = active;
        _visibleBody.Visible = !active;
        _reticle.Visible = !active;
        Velocity = Vector3.Zero;
        IsSprinting = false;
        _worldInteractionNeedsRelease = true;
        _traversalNoticeUntil = 0;
        _focusedTarget = _focusCandidate = _promptTarget = null;
        PresentationTransformRevision++;
        ResetStepMotion();
        ResetVisibleBodyMotion();
        SetInteractionPrompt(string.Empty);
    }
}
