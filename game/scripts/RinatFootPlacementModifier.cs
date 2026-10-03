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
        RequestedRevision++;
        AppliedRevision = RequestedRevision;
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (skeleton is null) return;
        var inverse = skeleton.GlobalTransform.AffineInverse();
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
        AppliedRevision = RequestedRevision;
    }

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
