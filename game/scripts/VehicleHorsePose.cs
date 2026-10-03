using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>Presentation only: four world-planted feet and a two-joint solve.
/// The existing vehicle remains the sole movement, collision and save owner.</summary>
public partial class VehicleHorsePose : Node3D
{
    public sealed record LegBinding(Node3D Hip, Node3D Knee, Node3D Hoof,
        float UpperLength, float LowerLength, Vector3 RestSole, bool Hind);

    internal sealed class LegState
    {
        internal required LegBinding Binding;
        internal Vector3 Sole, Normal = Vector3.Up, Start, End, EndNormal = Vector3.Up;
        internal float Swing, Duration = .20f, Error;
        internal bool Moving, Grounded, Reachable, SupportAtSole;
        internal string Collider = string.Empty, Rejection = "not projected";
        internal Rid GroundRid;
        internal Transform3D HoofPose;
        // The lower leg swings with the knee while the hoof stays flat on the
        // ground, so the visible cannon needs its own articulated frame.
        internal Transform3D KneePose;
        internal LegState Copy() => (LegState)MemberwiseClone();
    }

    // Same element order and content as _legs.Select(leg => leg.Copy()).ToArray():
    // a fresh array per call, so plans never share a backing store, but no LINQ
    // iterator or List inside ToArray.
    private static LegState[] CopyLegs(IReadOnlyList<LegState> source)
    {
        var copy = new LegState[source.Count];
        for (var index = 0; index < copy.Length; index++) copy[index] = source[index].Copy();
        return copy;
    }

    internal sealed record PosePlan(Transform3D OwnerPose, Transform3D HorsePose,
        IReadOnlyList<LegState> Legs, float Phase, int Replants, int StepsStarted,
        float HorizontalTravel, float VerticalDelta, float YawTravel,
        float Steering, HorseDisposition Disposition);

    private readonly List<LegState> _legs = new();
    private Node3D? _horse, _head;
    private Vector3 _headRest;
    private bool _initialized;
    private Transform3D _previous;
    private float _phase;
    private int _replants, _stepsStarted;
    private float _horizontalTravel, _verticalDelta, _yawTravel;
    private Node3D[] _reins = Array.Empty<Node3D>();
    private Vector3[] _reinFrom = Array.Empty<Vector3>(), _reinTo = Array.Empty<Vector3>();
    public const float SoleDepth = .13f;
    public const float SoleHalfWidth = .074f;
    public const float SoleHalfLength = .098f;
    public const int SoleSegments = 16;
    public const float MaximumSwingDuration = .28f;
    // Straight, half and fully planted sole probes, in that order. Static and
    // read-only: the loop only reads it, so no per-leg array is allocated.
    private static readonly float[] StepProbeFractions = { 1f, .5f, 0f };

    public void Configure(Node3D horse, IReadOnlyList<LegBinding> legs, Node3D head)
    {
        if (_horse is not null) throw new InvalidOperationException("Horse pose is already configured.");
        if (legs.Count != 4) throw new ArgumentException("A horse needs four leg bindings.", nameof(legs));
        foreach (var binding in legs)
        {
            if (binding.Hip.GetParent() != horse || binding.Knee.GetParent() != binding.Hip
                || binding.Hoof.GetParent() != binding.Knee || binding.UpperLength <= 0 || binding.LowerLength <= 0)
                throw new ArgumentException("Horse leg must be Hip/Knee/Hoof along local -Y with positive lengths.");
            _legs.Add(new LegState { Binding = binding });
        }
        _horse = horse; _head = head; _headRest = head.RotationDegrees;
        SetMeta("presentationOwner", "VehicleHorsePose; no body, no runtime state or save owner");
    }

    public void ConfigureReins(IReadOnlyList<Node3D> reins, IReadOnlyList<Vector3> horseLocalFrom,
        IReadOnlyList<Vector3> headLocalTo)
    {
        if (reins.Count != horseLocalFrom.Count || reins.Count != headLocalTo.Count)
            throw new ArgumentException("Each rein needs both endpoints.");
        _reins = reins.ToArray(); _reinFrom = horseLocalFrom.ToArray(); _reinTo = headLocalTo.ToArray();
    }

    public void UpdatePose(VehicleController owner, float delta, float speed, float steering, HorseDisposition disposition)
    {
        if (_horse is null || _head is null || !IsInsideTree() || !_horse.IsInsideTree()
            || !owner.IsInsideTree() || !owner.PlacementAvailable)
        {
            _initialized = false;
            foreach (var leg in _legs) { leg.Grounded = false; leg.Rejection = "placement unavailable"; }
            return;
        }
        var plan = PreparePose(owner, owner.GlobalTransform, delta, speed, steering, disposition);
        owner.CommitHorsePose(this, plan);
    }

    internal PosePlan PreparePose(VehicleController owner, Transform3D pose, float delta, float speed,
        float steering, HorseDisposition disposition, bool rest = false)
    {
        if (_horse is null) throw new InvalidOperationException("Horse pose has no bindings.");
        var ownerPose = owner.GlobalTransform;
        var horsePose = pose == ownerPose ? _horse.GlobalTransform
            : pose * ownerPose.AffineInverse() * _horse.GlobalTransform;
        var legs = CopyLegs(_legs);
        var dt = Math.Clamp(float.IsFinite(delta) ? delta : 0, 0, .10f);
        var distance = _initialized ? pose.Origin.DistanceTo(_previous.Origin) : 0;
        var rotation = _initialized ? pose.Basis.GetRotationQuaternion().AngleTo(_previous.Basis.GetRotationQuaternion()) : 0;
        var discontinuity = rest || !_initialized || distance > Math.Max(.70f, Math.Abs(speed) * dt * 3 + .15f)
            || rotation > .45f;
        var phaseValue = _phase; var replants = _replants; var stepsStarted = _stepsStarted;
        var excluded = new global::Godot.Collections.Array<Rid> { owner.GetRid(), owner.EntryTarget.GetRid() };
        using var excludedOwner = (global::Godot.Collections.Array)excluded;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player) excluded.Add(player.GetRid());
        using var ray = new PhysicsRayQueryParameters3D { CollisionMask = 3, Exclude = excluded };
        if (discontinuity)
        {
            phaseValue = 0; replants++;
            foreach (var leg in legs)
            {
                leg.Moving = false;
                var wanted = horsePose * leg.Binding.RestSole;
                leg.Sole = wanted; leg.Normal = Vector3.Up;
                leg.Grounded = Ground(owner, ray, horsePose.Basis, wanted, out var floor, out var normal,
                    out var collider, out var groundRid, out var reason);
                if (leg.Grounded) { leg.Sole = floor; leg.Normal = normal; }
                leg.Collider = collider; leg.GroundRid = groundRid; leg.Rejection = reason;
            }
        }
        // Actual translation and turning advance gait. Merely holding throttle
        // against a wall, or an idling engine, cannot make the feet march.
        var horizontalTravel = discontinuity ? 0 : new Vector2(pose.Origin.X - _previous.Origin.X,
            pose.Origin.Z - _previous.Origin.Z).Length();
        var verticalDelta = discontinuity ? 0 : pose.Origin.Y - _previous.Origin.Y;
        var previousForward = new Vector2(_previous.Basis.Z.X, _previous.Basis.Z.Z).Normalized();
        var currentForward = new Vector2(pose.Basis.Z.X, pose.Basis.Z.Z).Normalized();
        // Vertical floor settling is not a step. Quaternion.AngleTo can also
        // report a nonzero self-angle after normalization rounding; atan2 of
        // the actual horizontal axes is exactly zero for an unchanged heading.
        var yawTravel = discontinuity ? 0 : Math.Abs(Mathf.Atan2(
            previousForward.Cross(currentForward), previousForward.Dot(currentForward)));
        var travel = horizontalTravel + yawTravel * 1.6f;
        var moving = dt > 0 && travel > .00015f;
        var stride = Math.Clamp(.60f + Math.Abs(speed) * .08f, .60f, .94f);
        if (moving) phaseValue = (phaseValue + travel / stride) % 1f;
        var direction = -pose.Basis.Z * (speed < -.025f ? -1 : 1);
        foreach (var leg in legs)
        {
            leg.SupportAtSole = false;
            var binding = leg.Binding;
            var wanted = horsePose * binding.RestSole;
            var sequence = binding.Hind ? (binding.Hip.Position.X < 0 ? 0 : 2) : (binding.Hip.Position.X < 0 ? 1 : 3);
            var phase = (phaseValue - sequence * .25f + 1f) % 1f;
            var drift = new Vector2(wanted.X - leg.Sole.X, wanted.Z - leg.Sole.Z).Length();
            var behind = (wanted - leg.Sole).Dot(direction);
            var overextended = !WithinReach(binding, horsePose, leg.Sole + leg.Normal * SoleDepth, .012f);
            if (!leg.Moving && moving && (phase < .20f && (behind > .025f || yawTravel > .0001f && drift > .045f) || overextended))
            {
                var lead = Math.Clamp(Math.Abs(speed) * .09f, .08f, .28f);
                foreach (var fraction in StepProbeFractions)
                {
                    var candidate = wanted + direction * lead * fraction;
                    if (!Ground(owner, ray, horsePose.Basis, candidate, out var floor, out var normal,
                        out var collider, out var groundRid, out var reason))
                    { leg.Rejection = reason; continue; }
                    if (!WithinReach(binding, horsePose, floor + normal * SoleDepth, .012f))
                    { leg.Rejection = "next supported sole is beyond unstretched joints"; continue; }
                    leg.Start = leg.Sole; leg.End = floor; leg.EndNormal = normal;
                    leg.Collider = collider; leg.GroundRid = groundRid; leg.Swing = 0; leg.Moving = true;
                    stepsStarted++;
                    leg.Duration = Math.Clamp(stride / Math.Max(Math.Abs(speed), .30f) * .22f, .09f, MaximumSwingDuration);
                    break;
                }
            }
            if (leg.Moving)
            {
                // Finish the current swing after stopping, then remain planted.
                leg.Swing = Math.Min(1, leg.Swing + dt / leg.Duration);
                var eased = leg.Swing * leg.Swing * (3 - 2 * leg.Swing);
                leg.Sole = leg.Start.Lerp(leg.End, eased) + Vector3.Up * (Mathf.Sin(leg.Swing * Mathf.Pi) * .06f);
                leg.Normal = leg.Normal.Lerp(leg.EndNormal, Math.Min(1, dt * 14)).Normalized();
                leg.Grounded = false;
                if (leg.Swing >= 1)
                {
                    leg.Moving = false;
                    leg.Grounded = Ground(owner, ray, horsePose.Basis, leg.End, out var floor, out var normal,
                        out var collider, out var groundRid, out var reason);
                    if (leg.Grounded) { leg.Sole = floor; leg.Normal = normal; }
                    leg.Collider = collider; leg.GroundRid = groundRid; leg.Rejection = reason;
                }
            }
            else
            {
                // Check retained world support as well: a missing surface is
                // never replaced by an invented plane or reported as grounded.
                leg.Grounded = Ground(owner, ray, horsePose.Basis, leg.Sole, out var floor, out var normal,
                    out var collider, out var groundRid, out var reason);
                leg.SupportAtSole = leg.Grounded && leg.Sole == floor;
                if (leg.Grounded) { leg.Sole = floor; leg.Normal = normal; }
                leg.Collider = collider; leg.GroundRid = groundRid; leg.Rejection = reason;
            }
            Solve(leg, horsePose, false);
        }
        return new(pose, horsePose, legs, phaseValue, replants, stepsStarted,
            horizontalTravel, verticalDelta, yawTravel, steering, disposition);
    }

    internal void PublishPose(PosePlan plan)
    {
        if (_horse is null || _head is null) throw new InvalidOperationException("Horse pose has no bindings.");
        _legs.Clear(); _legs.AddRange(CopyLegs(plan.Legs));
        _phase = plan.Phase; _replants = plan.Replants; _stepsStarted = plan.StepsStarted;
        _horizontalTravel = plan.HorizontalTravel; _verticalDelta = plan.VerticalDelta; _yawTravel = plan.YawTravel;
        foreach (var leg in _legs) Solve(leg, plan.HorsePose, true);
        var steering = plan.Steering; var disposition = plan.Disposition;
        var headRotation = _headRest + new Vector3(disposition >= HorseDisposition.Wary ? -7 : 0,
            disposition == HorseDisposition.Refusing ? 14 : Mathf.RadToDeg(steering) * .3f, 0);
        // Exact comparison only: assigning the same rotation is a no-op value
        // write, so skipping it leaves the identical head transform.
        if (_head.RotationDegrees != headRotation) _head.RotationDegrees = headRotation;
        UpdateReins();
        _previous = plan.OwnerPose; _initialized = true;
    }

    internal PosePlan RebasePose(PosePlan plan, Transform3D ownerPose)
    {
        var horsePose = ownerPose * plan.OwnerPose.AffineInverse() * plan.HorsePose;
        var legs = CopyLegs(plan.Legs);
        foreach (var leg in legs) Solve(leg, horsePose, false);
        return plan with { OwnerPose = ownerPose, HorsePose = horsePose, Legs = legs };
    }

    internal void MarkProjectionRejected(string reason)
    {
        _initialized = false;
        foreach (var leg in _legs) { leg.Grounded = false; leg.Rejection = reason; }
    }

    internal PosePlan SettlePose(VehicleController owner, PosePlan plan, bool reuseFreshSupport = false)
    {
        var legs = CopyLegs(plan.Legs);
        var excluded = new global::Godot.Collections.Array<Rid> { owner.GetRid(), owner.EntryTarget.GetRid() };
        using var excludedOwner = (global::Godot.Collections.Array)excluded;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player) excluded.Add(player.GetRid());
        using var ray = new PhysicsRayQueryParameters3D { CollisionMask = 3, Exclude = excluded };
        foreach (var leg in legs)
        {
            // SupportAtSole was set from this exact retained-sole ray in this
            // same tick (the `leg.SupportAtSole = leg.Grounded && leg.Sole ==
            // floor` line of PreparePose): it already returned floor ==
            // leg.Sole together with the current Normal/Collider/GroundRid and
            // an empty Rejection. Ground is deterministic for the same wanted,
            // mask and exclusion set, so re-running it would repeat exactly
            // those values; only the remaining writes of the grounded branch
            // (Start/End/Swing/Moving/EndNormal) are needed here. Callers that
            // pass a rebased plan must not enable this, because Ground's
            // corner validation also depends on HorsePose.Basis.
            if (reuseFreshSupport && !leg.Moving && leg.SupportAtSole)
            {
                leg.Start = leg.End = leg.Sole;
                leg.EndNormal = leg.Normal;
                leg.Swing = 0; leg.Moving = false;
                Solve(leg, plan.HorsePose, false);
                continue;
            }
            leg.Grounded = Ground(owner, ray, plan.HorsePose.Basis, leg.Sole, out var floor, out var normal,
                out var collider, out var groundRid, out var reason);
            if (leg.Grounded)
            {
                leg.Sole = leg.Start = leg.End = floor; leg.Normal = leg.EndNormal = normal;
                leg.Moving = false; leg.Swing = 0;
            }
            leg.GroundRid = groundRid; leg.Collider = collider; leg.Rejection = reason;
            Solve(leg, plan.HorsePose, false);
        }
        return plan with { Legs = legs };
    }

    private bool Ground(VehicleController owner, PhysicsRayQueryParameters3D ray, Basis horseBasis, Vector3 wanted,
        out Vector3 point, out Vector3 normal, out string collider, out Rid groundRid, out string rejection)
    {
        point = wanted; normal = Vector3.Up; collider = string.Empty; groundRid = default; rejection = string.Empty;
        var space = owner.GetWorld3D().DirectSpaceState;
        ray.From = wanted + Vector3.Up * .30f; ray.To = wanted - Vector3.Up * .35f;
        using var hit = space.IntersectRay(ray);
        if (hit.Count == 0) { rejection = "no ground below sole"; return false; }
        point = hit["position"].AsVector3(); normal = hit["normal"].AsVector3().Normalized();
        collider = (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "physics RID";
        groundRid = hit["rid"].AsRid();
        if (normal.Y < .78f || Math.Abs(point.Y - wanted.Y) > .25f)
        { rejection = "ground is too steep or outside the local step range"; return false; }
        var basis = FloorBasis(normal, horseBasis.Z);
        // The actual oval hoof footprint, including its toe and heel, needs
        // coherent support. A smaller proxy must not hide an unsupported edge.
        for(var edgeIndex=0;edgeIndex<SoleSegments;edgeIndex++)
        {
            var angle=Mathf.Tau*edgeIndex/SoleSegments;
            var x=Mathf.Cos(angle)*SoleHalfWidth;var z=Mathf.Sin(angle)*SoleHalfLength;
            var corner = point + basis.X * x + basis.Z * z;
            ray.From = corner + Vector3.Up * .16f; ray.To = corner - Vector3.Up * .20f;
            using var edge = space.IntersectRay(ray);
            if (edge.Count == 0 || edge["normal"].AsVector3().Y < .78f
                || Math.Abs((edge["position"].AsVector3() - point).Dot(normal)) > .012f)
            {
                if (edge.Count != 0) collider = (edge["collider"].AsGodotObject() as Node)?.GetPath().ToString() ?? "physics RID";
                rejection = "sole corner has no coherent support"; return false;
            }
        }
        return true;
    }

    private static bool WithinReach(LegBinding binding, Transform3D horsePose, Vector3 ankle, float reserve)
    {
        var distance = (horsePose * binding.Hip.Position).DistanceTo(ankle);
        return distance < binding.UpperLength + binding.LowerLength - reserve
            && distance > Math.Abs(binding.UpperLength - binding.LowerLength) + .015f;
    }

    private static void Solve(LegState leg, Transform3D horsePose, bool apply)
    {
        var binding = leg.Binding;
        var hip = horsePose * binding.Hip.Position;
        var ankle = leg.Sole + leg.Normal * SoleDepth;
        var offset = ankle - hip;
        var requested = offset.Length();
        var distance = Math.Clamp(requested, Math.Abs(binding.UpperLength - binding.LowerLength) + .002f,
            binding.UpperLength + binding.LowerLength - .002f);
        var direction = requested > .00001f ? offset / requested : Vector3.Down;
        var pole = horsePose.Basis.Z * (binding.Hind ? 1 : -1);
        pole -= direction * pole.Dot(direction);
        if (pole.LengthSquared() < .00001f) pole = horsePose.Basis.X.Cross(direction);
        pole = pole.Normalized();
        var along = (binding.UpperLength * binding.UpperLength - binding.LowerLength * binding.LowerLength
            + distance * distance) / (2 * distance);
        var bend = Mathf.Sqrt(Math.Max(0, binding.UpperLength * binding.UpperLength - along * along));
        var knee = hip + direction * along + pole * bend;
        var solvedAnkle = hip + direction * distance;
        leg.HoofPose = new(FloorBasis(leg.Normal, horsePose.Basis.Z), solvedAnkle);
        leg.KneePose = new(BoneBasis(solvedAnkle - knee, horsePose.Basis.X), knee);
        if (apply)
        {
            // Exact comparisons only, in the original write order: assigning an
            // already equal basis/position leaves the identical bone transform,
            // so a skipped repeated write cannot change the visible skeleton.
            var hipBasis = BoneBasis(knee - hip, horsePose.Basis.X);
            if (binding.Hip.GlobalBasis != hipBasis) binding.Hip.GlobalBasis = hipBasis;
            var kneeOffset = Vector3.Down * binding.UpperLength;
            if (binding.Knee.Position != kneeOffset) binding.Knee.Position = kneeOffset;
            if (binding.Knee.GlobalBasis != leg.KneePose.Basis) binding.Knee.GlobalBasis = leg.KneePose.Basis;
            var hoofOffset = Vector3.Down * binding.LowerLength;
            if (binding.Hoof.Position != hoofOffset) binding.Hoof.Position = hoofOffset;
            if (binding.Hoof.GlobalBasis != leg.HoofPose.Basis) binding.Hoof.GlobalBasis = leg.HoofPose.Basis;
        }
        var actualSole = leg.HoofPose * (Vector3.Down * SoleDepth);
        leg.Error = actualSole.DistanceTo(leg.Sole);
        leg.Reachable = leg.Error < .012f;
        if (!leg.Reachable) leg.Rejection = "IK reached its unstretched joint limit";
    }

    private static Basis BoneBasis(Vector3 down, Vector3 preferredRight)
    {
        var y = -down.Normalized();
        var x = preferredRight - y * preferredRight.Dot(y);
        if (x.LengthSquared() < .00001f) x = Vector3.Forward.Cross(y);
        x = x.Normalized();
        return new Basis(x, y, x.Cross(y).Normalized());
    }

    private static Basis FloorBasis(Vector3 normal, Vector3 back)
    {
        var z = back - normal * back.Dot(normal);
        if (z.LengthSquared() < .00001f) z = Vector3.Right.Cross(normal);
        z = z.Normalized();
        return new Basis(normal.Cross(z).Normalized(), normal, z);
    }

    private void UpdateReins()
    {
        for (var i = 0; i < _reins.Length; i++)
        {
            var from = _horse!.ToGlobal(_reinFrom[i]); var to = _head!.ToGlobal(_reinTo[i]);
            var span = to - from;
            if (span.LengthSquared() < .00001f) continue;
            var basis = BoneBasis(-span, _horse.GlobalBasis.X);
            _reins[i].GlobalTransform = new Transform3D(basis.ScaledLocal(new(1, span.Length(), 1)), (from + to) * .5f);
        }
    }

    public JsonObject CaptureSupportProof()
    {
        var feet = new JsonArray();
        foreach (var leg in _legs)
        {
            var actual = leg.Binding.Hoof.IsInsideTree()
                ? leg.Binding.Hoof.ToGlobal(Vector3.Down * SoleDepth) : Vector3.Zero;
            feet.Add(new JsonObject {
                ["name"] = leg.Binding.Hip.Name.ToString(), ["hind"] = leg.Binding.Hind,
                ["phase"] = leg.Moving ? "swing" : "stance",
                ["hasGround"] = leg.Grounded, ["supported"] = _initialized && !leg.Moving && leg.Grounded && leg.Reachable,
                ["reachable"] = leg.Reachable, ["actualSole"] = Point(actual), ["targetSole"] = Point(leg.Sole),
                ["normal"] = Point(leg.Normal), ["soleErrorMetres"] = leg.Error,
                ["swingProgress"] = leg.Swing, ["swingDurationSeconds"] = leg.Duration,
                ["collider"] = leg.Collider, ["rejection"] = leg.Rejection });
        }
        return new JsonObject { ["initialized"] = _initialized, ["replants"] = _replants,
            ["gaitPhase"] = _phase, ["stepsStarted"] = _stepsStarted,
            ["horizontalTravelMetres"] = _horizontalTravel, ["verticalDeltaMetres"] = _verticalDelta,
            ["yawTravelRadians"] = _yawTravel, ["feet"] = feet,
            ["scope"] = "presentation geometry and actual support rays; no vehicle collision or artistic acceptance" };
    }

    private static JsonArray Point(Vector3 point) => new(point.X, point.Y, point.Z);
}
