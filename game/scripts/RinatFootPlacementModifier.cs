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
        foreach (var bone in _order)
        {
            var parent = skeleton.GetBoneParent(bone);
            var parentPose = parent >= 0 ? skeleton.GetBoneGlobalPose(parent) : Transform3D.Identity;
            skeleton.SetBonePose(bone, parentPose.AffineInverse() * inverse * _wantedWorld[bone]);
            _appliedWorld[bone] = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone);
        }
        AppliedRevision = RequestedRevision;
    }
}
