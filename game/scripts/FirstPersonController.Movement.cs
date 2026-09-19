using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    private const float SprintMultiplier = 1.5f;
    private const float JumpHeight = .42f;
    private uint _walkingCollisionLayer;
    private uint _walkingCollisionMask;

    public bool IsSprinting { get; private set; }
    public bool VehicleControlled { get; private set; }
    internal int GroundedJumps { get; private set; }

    private void TryJumpFromGround()
    {
        if (_worldInteractionNeedsRelease || !Input.IsActionJustPressed("jump") || !IsOnFloor()) return;
        _carryCoordinator ??= GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
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
            SetCrouched(false);
        }
        // A grounded press starts one ordinary hop. Holding the action does not
        // repeat on landing, and the step solver never cancels an upward jump.
        Velocity = new(Velocity.X, Mathf.Sqrt(2f * _gravity * JumpHeight), Velocity.Z);
        GroundedJumps++;
    }

    public bool CanEnterVehicle(out string reason)
    {
        _carryCoordinator ??= GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
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
