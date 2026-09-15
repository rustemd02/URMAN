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
        if (!IsCrouching) SetCrouched(true);
        else if (CanStandAt(GlobalPosition)) SetCrouched(false);
        // Staying crouched is intentional. Walking back out always remains
        // available, including after releasing the key or loading a save.
    }

    internal bool CanStandAt(Vector3 feet)
    {
        if (_stanceCapsule is null || !IsInsideTree()) return false;
        using var shape = new CapsuleShape3D
            { Radius = _stanceCapsule.Radius, Height = _standingHeight - .015f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new(Basis.Identity, feet + Vector3.Up * (_standingHeight * .5f + .01f)),
            CollisionMask = CollisionMask,
            Exclude = new global::Godot.Collections.Array<Rid> { GetRid() },
            Margin = .002f
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
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
    }
}
