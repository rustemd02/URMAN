using Godot;

namespace Urman.Godot;

/// <summary>
/// Four instance-local leg/ankle placements, applied in Godot's supported
/// post-animation modifier phase. The skeleton restores its animation pose
/// outside that phase; callers read the captured applied pose for continuity.
/// </summary>
public partial class RinatFootPlacementModifier : SkeletonModifier3D
{
    private readonly List<int> _order = new();
    private readonly Dictionary<int, Transform3D> _wantedWorld = new();
    private readonly Dictionary<int, Transform3D> _appliedWorld = new();
    internal long RequestedRevision { get; private set; }
    internal long AppliedRevision { get; private set; }

    // Human-kit legs are thigh, calf and foot. A foot registered here is
    // reached by bending the knee (two-bone IK in the leg's own plane) instead
    // of stretching one rigid leg; the thigh's own requested pose is ignored.
    private readonly Dictionary<int, (int Thigh, int Calf)> _chains = new();
    private readonly HashSet<int> _chainThighs = new();

    internal void RegisterLegChain(int thigh, int calf, int foot)
    {
        _chains[foot] = (thigh, calf);
        _chainThighs.Add(thigh);
    }

    internal void SetWorldPose(int bone, Transform3D pose)
    {
        if (!_wantedWorld.ContainsKey(bone)) _order.Add(bone);
        _wantedWorld[bone] = pose;
        RequestedRevision++;
        Active = true;
    }

    internal Transform3D AppliedWorldPose(int bone)
    {
        if (_appliedWorld.TryGetValue(bone, out var pose)) return pose;
        var skeleton = GetSkeleton();
        return skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone);
    }

    internal void ClearPoses()
    {
        Active = false;
        _order.Clear();
        _wantedWorld.Clear();
        _appliedWorld.Clear();
        _appliedSkeleton.Clear();
        ClearWeightShift();
        RequestedRevision++;
        AppliedRevision = RequestedRevision;
    }

    /// <summary>
    /// VIS-046: the body's own weight transfer, in the one place that already owns
    /// this skeleton's legs. When a person steps, the pelvis moves over the planted
    /// leg and sinks a little on the single-support part of the stride; without it a
    /// statically correct model keeps walking like a mannequin. Both numbers are
    /// centimetres, eased by the caller, and applied at the pelvis only, so the
    /// actor's world position, its support validation and every interaction anchor
    /// stay exactly where their owners put them.
    /// </summary>
    internal void SetWeightShift(float dropMetres, Vector3 worldTowardsPlanted, float lateralMetres)
    {
        var wanted = new Vector3(worldTowardsPlanted.X, 0f, worldTowardsPlanted.Z);
        if (wanted.LengthSquared() > .000001f) wanted = wanted.Normalized();
        else wanted = Vector3.Zero;
        _weightDrop = Mathf.Max(0f, dropMetres);
        _weightLateral = Mathf.Max(0f, lateralMetres);
        _weightDirection = wanted;
        if (_weightDrop > 0f || _weightLateral > 0f) Active = true;
        RequestedRevision++;
    }

    internal void ClearWeightShift()
    {
        _weightDrop = 0f;
        _weightLateral = 0f;
        _weightDirection = Vector3.Zero;
    }

    /// <summary>The shift actually applied on the last modification pass.</summary>
    internal (float Drop, float Lateral) AppliedWeightShift => (_appliedWeightDrop, _appliedWeightLateral);

    private float _weightDrop;
    private float _weightLateral;
    private Vector3 _weightDirection;
    private float _appliedWeightDrop;
    private float _appliedWeightLateral;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (skeleton is null) return;
        var inverse = skeleton.GlobalTransform.AffineInverse();
        // The weight shift moves the pelvis first: every leg below it is solved
        // afterwards against the shifted hip, so the planted boot stays exactly on
        // the support point its owner asked for. Doing it in the other order would
        // drag a planted foot through the snow.
        ShiftWeightOntoPlantedLeg(skeleton, inverse);
        // Parent leg precedes its ankle. SetBonePose receives parent-relative
        // transforms, and Influence remains 1; Skeleton3D owns its blending.
        LowerPelvisToReach(skeleton, inverse);
        foreach (var bone in _order)
        {
            if (_chainThighs.Contains(bone)) continue;
            if (_chains.TryGetValue(bone, out var chain))
            {
                SolveLeg(skeleton, chain.Thigh, chain.Calf, bone, inverse * _wantedWorld[bone]);
                _appliedWorld[bone] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone);
                continue;
            }
            var parent = skeleton.GetBoneParent(bone);
            var parentPose = parent >= 0 ? skeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
            skeleton.SetBonePose(bone, parentPose.AffineInverse() * inverse * _wantedWorld[bone]);
            _appliedWorld[bone] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone);
            _appliedSkeleton[bone] = skeleton.GetBoneGlobalPose(bone);
        }
        ShiftWeightOntoPlantedLeg(skeleton, inverse);
        AppliedRevision = RequestedRevision;
    }

    // Applied before the leg solve so the legs answer the shifted hip; the planted
    // boot keeps the support point its owner asked for.
    private void ShiftWeightOntoPlantedLeg(Skeleton3D skeleton, Transform3D inverse)
    {
        _appliedWeightDrop = 0f;
        _appliedWeightLateral = 0f;
        if (_weightDrop <= 0f && _weightLateral <= 0f) return;
        var pelvis = -1;
        foreach (var chain in _chains.Values)
        {
            var parent = skeleton.GetBoneParent(chain.Thigh);
            if (parent < 0) continue;
            pelvis = parent;
            break;
        }
        if (pelvis < 0) return;
        var drop = Mathf.Min(_weightDrop, MaxWeightDropMetres);
        var distance = Mathf.Min(_weightLateral, MaxWeightLateralMetres);
        // Godot 4's C# Basis rotates a direction through the multiplication
        // operator; there is no Xform() member on it.
        var lateral = distance > 0f && _weightDirection.LengthSquared() > .000001f
            ? (inverse.Basis * _weightDirection) * distance
            : Vector3.Zero;
        var shift = lateral + Vector3.Up * -drop;
        if (shift.LengthSquared() < .00000001f) return;
        var pose = skeleton.GetBoneGlobalPose(pelvis);
        pose.Origin += shift;
        SetGlobal(skeleton, pelvis, pose);
        _appliedSkeleton[pelvis] = skeleton.GetBoneGlobalPose(pelvis);
        _appliedWeightDrop = drop;
        _appliedWeightLateral = lateral.Length();
    }

    // A villager in a coat shifts a couple of centimetres, not a dance move; past
    // this the cue reads as a bug rather than as weight.
    private const float MaxWeightDropMetres = .030f;
    private const float MaxWeightLateralMetres = .030f;

    /// <summary>
    /// Skeleton-space global poses as last applied for skinning, for every bone
    /// this modifier moved; the skeleton restores its animation pose afterwards.
    /// </summary>
    internal IReadOnlyDictionary<int, Transform3D> AppliedSkeletonPoses => _appliedSkeleton;
    private readonly Dictionary<int, Transform3D> _appliedSkeleton = new();

    // A straight leg is as far as a foot can go. When a planted or landing
    // foot is lower than that (a step down the slope), the body sinks at the
    // pelvis by exactly the missing length, as a person bends into the step.
    private void LowerPelvisToReach(Skeleton3D skeleton, Transform3D inverse)
    {
        var drop = 0f;
        var pelvis = -1;
        foreach (var bone in _order)
        {
            if (!_chains.TryGetValue(bone, out var chain)) continue;
            pelvis = skeleton.GetBoneParent(chain.Thigh);
            var hip = skeleton.GetBoneGlobalPose(chain.Thigh).Origin;
            var knee = skeleton.GetBoneGlobalPose(chain.Calf).Origin;
            var ankle = skeleton.GetBoneGlobalPose(bone).Origin;
            var length = (hip.DistanceTo(knee) + knee.DistanceTo(ankle)) * .985f;
            var target = (inverse * _wantedWorld[bone]).Origin;
            var horizontal = new Vector2(target.X - hip.X, target.Z - hip.Z).Length();
            if (horizontal >= length) continue;
            var lowest = hip.Y - Mathf.Sqrt(length * length - horizontal * horizontal);
            drop = Mathf.Max(drop, lowest - target.Y);
        }
        drop = Mathf.Min(drop, .30f);
        if (pelvis < 0 || drop <= 0f) return;
        var pose = skeleton.GetBoneGlobalPose(pelvis);
        pose.Origin -= Vector3.Up * drop;
        SetGlobal(skeleton, pelvis, pose);
        _appliedSkeleton[pelvis] = skeleton.GetBoneGlobalPose(pelvis);
    }

    private void SolveLeg(Skeleton3D skeleton, int thigh, int calf, int foot, Transform3D target)
    {
        var thighPose = skeleton.GetBoneGlobalPose(thigh);
        var hip = thighPose.Origin;
        var knee = skeleton.GetBoneGlobalPose(calf).Origin;
        var ankle = skeleton.GetBoneGlobalPose(foot).Origin;
        var upper = hip.DistanceTo(knee);
        var lower = knee.DistanceTo(ankle);
        var reach = target.Origin - hip;
        var distance = Mathf.Clamp(reach.Length(), .05f, (upper + lower) * .999f);
        var along = reach.Normalized();
        // Keep the knee in the plane the animation already bends it in.
        var bend = knee - hip - along * (knee - hip).Dot(along);
        if (bend.LengthSquared() < 1e-6f) bend = thighPose.Basis.Z - along * thighPose.Basis.Z.Dot(along);
        bend = bend.Normalized();
        var a = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        var h = Mathf.Sqrt(Mathf.Max(0f, upper * upper - a * a));
        var newKnee = hip + along * a + bend * h;
        SetGlobal(skeleton, thigh, new Transform3D(
            new Basis(new Quaternion((knee - hip).Normalized(), (newKnee - hip).Normalized())) * thighPose.Basis, hip));
        var calfPose = skeleton.GetBoneGlobalPose(calf);
        var currentAnkle = skeleton.GetBoneGlobalPose(foot).Origin;
        var ankleTarget = hip + along * distance;
        SetGlobal(skeleton, calf, new Transform3D(
            new Basis(new Quaternion((currentAnkle - calfPose.Origin).Normalized(),
                (ankleTarget - calfPose.Origin).Normalized())) * calfPose.Basis, calfPose.Origin));
        SetGlobal(skeleton, foot, new Transform3D(target.Basis, skeleton.GetBoneGlobalPose(foot).Origin));
        // Same three distinct keys as the previous `new[] { thigh, calf, foot }`, in
        // the same order, so the dictionary ends in the identical state without the
        // per-leg int[3] allocation.
        _appliedSkeleton[thigh] = skeleton.GetBoneGlobalPose(thigh);
        _appliedSkeleton[calf] = skeleton.GetBoneGlobalPose(calf);
        _appliedSkeleton[foot] = skeleton.GetBoneGlobalPose(foot);
    }

    private static void SetGlobal(Skeleton3D skeleton, int bone, Transform3D global)
    {
        var parent = skeleton.GetBoneParent(bone);
        var parentPose = parent >= 0 ? skeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
        skeleton.SetBonePose(bone, parentPose.AffineInverse() * global);
    }
}
