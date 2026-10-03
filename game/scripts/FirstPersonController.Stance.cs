using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    private const float CrouchedHeight = 1.08f;
    private CollisionShape3D? _stanceCollision;
    private CapsuleShape3D? _stanceCapsule;
    private float _standingHeight;
    private Vector3 _standingHeadPosition;
    private bool _restoreStancePending;

    public bool IsCrouching { get; private set; }
    internal float BodyHeight => _stanceCapsule?.Height ?? 1.8f;
    public float StandingBodyHeight => _standingHeight;
    public float BodyRadius => _stanceCapsule?.Radius ?? .35f;

    private void InitializeStance()
    {
        _stanceCollision = GetNode<CollisionShape3D>("CollisionShape3D");
        _stanceCapsule = (_stanceCollision.Shape as CapsuleShape3D)?.Duplicate() as CapsuleShape3D
            ?? throw new InvalidOperationException("The player requires a capsule collision shape.");
        // PackedScene instances must not share mutable capsule dimensions.
        _stanceCollision.Shape = _stanceCapsule;
        _standingHeight = _stanceCapsule.Height;
        _standingHeadPosition = _headBasePosition;
    }

    private void RestoreStanceAtDestination()
    {
        if (_stanceCapsule is null) return;
        SetCrouched(true);
        _restoreStancePending = true;
    }

    private void ResolveRestoredStance()
    {
        if (!_restoreStancePending) return;
        _restoreStancePending = false;
        SetCrouched(!CanStandAt(GlobalPosition));
    }

    private void ToggleCrouch()
    {
        var crouched = !IsCrouching;
        if (!crouched && !CanStandAt(GlobalPosition)) return;
        var targetHead = _standingHeadPosition
            - Vector3.Up * (crouched ? _standingHeight - CrouchedHeight : 0);
        _carryCoordinator ??= GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        // Changing the eye height also moves the actual held object. Reject a
        // blocked sweep before changing either stance: retaining an overhead
        // lantern after crouching leaves its light and the player's hand apart.
        if (_carryCoordinator is not null
            && !_carryCoordinator.TryMoveHeldForStanceChange(GlobalBasis * (targetHead - _head.Position))) return;
        SetCrouched(crouched);
        // Staying crouched is intentional. Walking back out always remains
        // available, including after releasing the key or loading a save.
    }

    public bool CanStandAt(Vector3 feet) => CanFitAt(feet, _standingHeight);
    internal bool CanCrouchAt(Vector3 feet) => CanFitAt(feet, CrouchedHeight);

    private bool CanFitAt(Vector3 feet, float height)
    {
        if (_stanceCapsule is null || !IsInsideTree()) return false;
        using var shape = new CapsuleShape3D
            { Radius = _stanceCapsule.Radius, Height = height - .015f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new(Basis.Identity, feet + Vector3.Up * (height * .5f + .01f)),
            CollisionMask = VehicleControlled ? _walkingCollisionMask : CollisionMask,
            Exclude = new global::Godot.Collections.Array<Rid> { GetRid() },
            Margin = .002f
        };
        // IntersectShape returns an owned native array. It used to be left to the
        // finalizer on every probe, and the walk audit calls this thousands of
        // times. The element dictionaries are never materialized for a Count
        // check, so releasing the outer array covers the whole result.
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
        using var hitsOwner = (global::Godot.Collections.Array)hits;
        return hits.Count == 0;
    }

    private void SetCrouched(bool crouched)
    {
        if (_stanceCapsule is null || _stanceCollision is null) return;
        IsCrouching = crouched;
        var height = crouched ? CrouchedHeight : _standingHeight;
        _stanceCapsule.Height = height;
        _stanceCollision.Position = Vector3.Up * height * .5f;
        _headBasePosition = _standingHeadPosition
            - Vector3.Up * (crouched ? _standingHeight - CrouchedHeight : 0);
        _head.Position = _headBasePosition;
        _headBobPhase = 0;
        // Capsule and eye height change together. The lower-body pose must use
        // that same stance immediately, including a restored low-space save;
        // otherwise the lowered camera can enter the still-standing coat.
        _bodyCrouch = crouched ? 1f : 0f;
        UpdateVisibleBody(0, moving: false);
    }
}
