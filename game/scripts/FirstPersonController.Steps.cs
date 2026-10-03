using Godot;

namespace Urman.Godot;

public partial class FirstPersonController
{
    // Ordinary human risers, including the FAP's .12/.13m steps. This is a
    // collision-tested movement path; it adds no ramp or destination snap.
    internal const float MaximumStepHeight = .22f;
    internal const float StepClearance = .008f;
    private KinematicCollision3D _stepObstacle = null!;
    private KinematicCollision3D _stepSweep = null!;
    private KinematicCollision3D _stepLanding = null!;
    private global::Godot.Collections.Array<Rid> _stepRayExclude = null!;
    private ulong _lastStepFrame;
    private float _stepEyeDrop;
    internal int StepsClimbed { get; private set; }
    internal string LastStepRejection { get; private set; } = string.Empty;

    private void InitializeStepMotion()
    {
        _stepObstacle = new(); _stepSweep = new(); _stepLanding = new();
        _stepRayExclude = new() { GetRid() };
        // Descending the same ordinary riser uses CharacterBody's floor snap,
        // preserving its existing floor-angle limit and real contact owner.
        FloorSnapLength = Math.Max(FloorSnapLength, MaximumStepHeight);
    }

    private void ResetStepMotion()
    {
        _lastStepFrame = 0;
        _stepEyeDrop = 0;
        LastStepRejection = string.Empty;
    }

    private bool TryWalkUpStep(float delta)
    {
        var forward = new Vector3(Velocity.X, 0, Velocity.Z) * delta;
        if (forward.LengthSquared() < .000001f || (!IsOnFloor() && Velocity.Y > .05f)
            || _stanceCapsule is null) return false;
        var from = GlobalTransform;
        // A rounded capsule can briefly classify the leading stair edge as a
        // wall. Continue only directly after a verified step with real contact.
        if (!IsOnFloor() && !(_lastStepFrame + 1 == Engine.GetPhysicsFrames()
            && TestMove(from, Vector3.Down * .025f, _stepSweep, .001f, true, 4))) return false;
        if (!TestMove(from, forward, _stepObstacle, .001f, false, 4)) return false;
        var direction = forward.Normalized();
        var floorNormalY = Mathf.Cos(FloorMaxAngle);
        var riser = -1;
        for (var index = 0; index < _stepObstacle.GetCollisionCount(); index++)
        {
            var normal = _stepObstacle.GetNormal(index);
            if (normal.Y < floorNormalY && normal.Dot(direction) < -.05f) { riser = index; break; }
        }
        if (riser < 0) return false;
        LastStepRejection = "no walkable tread within step height";
        var edge = _stepObstacle.GetPosition(riser);
        var side = new Vector3(-direction.Z, 0, direction.X) * Math.Min(.10f, _stanceCapsule.Radius * .3f);
        var highest = float.NegativeInfinity;
        var lowest = float.PositiveInfinity;
        // The surface itself must be walkable under both feet. A steep slope,
        // wall, thin rail or top beyond the height limit cannot become a stair.
        foreach (var depth in new[] { .04f, .18f })
        foreach (var offset in new[] { -side, Vector3.Zero, side })
        {
            var at = edge + direction * depth + offset;
            using var ray = PhysicsRayQueryParameters3D.Create(
                new(at.X, from.Origin.Y + MaximumStepHeight + StepClearance, at.Z),
                new(at.X, from.Origin.Y + .015f, at.Z), CollisionMask, _stepRayExclude);
            // IntersectRay() returns a fresh caller-owned Dictionary; the ray
            // parameters are disposed above and the returned values are already
            // read into locals below, so releasing it changes no tread sample.
            using var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
            if (hit.Count == 0) { LastStepRejection = $"no tread support at {at}; feet {from.Origin}"; return false; }
            if (hit["normal"].AsVector3().Y < floorNormalY)
            { LastStepRejection = $"steep tread support {hit["normal"].AsVector3()} at {at}"; return false; }
            var height = hit["position"].AsVector3().Y - from.Origin.Y;
            highest = Math.Max(highest, height); lowest = Math.Min(lowest, height);
        }
        if (highest > MaximumStepHeight || lowest < .015f || highest - lowest > .025f)
        { LastStepRejection = $"tread height range {lowest:0.000}..{highest:0.000} at {edge}; feet {from.Origin}"; return false; }
        var up = Vector3.Up * (highest + StepClearance);
        LastStepRejection = "ceiling above the step";
        if (TestMove(from, up, _stepSweep, .001f, false, 4)) return false;
        var raised = from; raised.Origin += up;
        LastStepRejection = "body blocked beyond the riser";
        if (TestMove(raised, forward, _stepSweep, .001f, false, 4))
        {
            LastStepRejection = $"body blocked beyond riser by {(_stepSweep.GetCollider() as Node)?.GetPath()} normal {_stepSweep.GetNormal()} travel {_stepSweep.GetTravel()} raised feet {raised.Origin}";
            return false;
        }
        raised.Origin += forward;
        var down = Vector3.Down * (up.Y + .025f);
        LastStepRejection = "no physical landing";
        if (!TestMove(raised, down, _stepLanding, .001f, false, 4)) return false;
        var landed = raised.Origin + _stepLanding.GetTravel();
        var rise = landed.Y - from.Origin.Y;
        if (rise <= .001f || rise > MaximumStepHeight + StepClearance
            || _stepLanding.GetNormal().Y <= .05f) return false;
        LastStepRejection = "carried object has no clear step path";
        if (_carryCoordinator?.CanCarryThroughStep(up, forward, _stepLanding.GetTravel()) == false) return false;

        // Execute those same three swept segments with the real character.
        // Horizontal travel remains exactly the input distance for this tick.
        var walkingVelocity = Velocity;
        MoveAndCollide(up, false, .001f, false, 4);
        if (GlobalPosition.Y < from.Origin.Y + up.Y - .003f) return false;
        MoveAndCollide(forward, false, .001f, false, 4);
        MoveAndCollide(down, false, .001f, false, 4);
        Velocity = Vector3.Down * .05f;
        MoveAndSlide(); // Refresh actual floor/slide contact for saves and sound.
        Velocity = new(walkingVelocity.X, 0, walkingVelocity.Z);
        _lastStepFrame = Engine.GetPhysicsFrames();
        var actualRise = Math.Max(0, GlobalPosition.Y - from.Origin.Y);
        _stepEyeDrop = Math.Min(MaximumStepHeight, _stepEyeDrop + actualRise);
        _head.Position -= Vector3.Up * actualRise;
        StepsClimbed++;
        LastStepRejection = string.Empty;
        return true;
    }

    public override void _ExitTree()
    {
        _stepObstacle?.Dispose(); _stepSweep?.Dispose(); _stepLanding?.Dispose();
    }
}
