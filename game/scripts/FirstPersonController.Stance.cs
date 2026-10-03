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

    // Probe resources are pure functions of (capsule radius, requested height):
    // the walk audit calls CanFitAt thousands of times with the same one or two
    // heights, and every call used to create a native CapsuleShape3D, a query
    // wrapper and an exclusion array that only the finaliser reclaimed. The query
    // is rewritten per call (shape, transform, mask, exclude) exactly like the
    // previous object initialiser, and the exclusion set never changes.
    private readonly Dictionary<(float Height, float Radius), CapsuleShape3D> _stanceProbeShapes = new();
    private PhysicsShapeQueryParameters3D? _stanceProbeQuery;
    private global::Godot.Collections.Array<Rid>? _stanceProbeExclude;

    public bool CanStandAt(Vector3 feet) => CanFitAt(feet, _standingHeight);
    internal bool CanCrouchAt(Vector3 feet) => CanFitAt(feet, CrouchedHeight);

    private bool CanFitAt(Vector3 feet, float height)
    {
        if (_stanceCapsule is null || !IsInsideTree()) return false;
        var key = (height, _stanceCapsule.Radius);
        if (!_stanceProbeShapes.TryGetValue(key, out var shape))
            _stanceProbeShapes[key] = shape = new CapsuleShape3D
                { Radius = _stanceCapsule.Radius, Height = height - .015f };
        _stanceProbeExclude ??= new global::Godot.Collections.Array<Rid> { GetRid() };
        _stanceProbeQuery ??= new PhysicsShapeQueryParameters3D { Margin = .002f };
        _stanceProbeQuery.Shape = shape;
        _stanceProbeQuery.Transform = new(Basis.Identity, feet + Vector3.Up * (height * .5f + .01f));
        _stanceProbeQuery.CollisionMask = VehicleControlled ? _walkingCollisionMask : CollisionMask;
        _stanceProbeQuery.Exclude = _stanceProbeExclude;
        // IntersectShape returns an owned native array. It used to be left to the
        // finalizer on every probe, and the walk audit calls this thousands of
        // times. The element dictionaries are never materialized for a Count
        // check, so releasing the outer array covers the whole result.
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(_stanceProbeQuery, 1);
        using var hitsOwner = (global::Godot.Collections.Array)hits;
        return hits.Count == 0;
    }

    /// <summary>Releases the owned stance probe resources when the body leaves the
    /// tree, so shutdown leak checks stay clean. In GodotSharp 4.7.1 Array<T> is not
    /// IDisposable, so the untyped owner releases the same underlying array.</summary>
    private void ReleaseStanceProbes()
    {
        foreach (var shape in _stanceProbeShapes.Values) shape.Dispose();
        _stanceProbeShapes.Clear();
        _stanceProbeQuery?.Dispose(); _stanceProbeQuery = null;
        if (_stanceProbeExclude is not null)
        {
            ((global::Godot.Collections.Array)_stanceProbeExclude).Dispose();
            _stanceProbeExclude = null;
        }
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
