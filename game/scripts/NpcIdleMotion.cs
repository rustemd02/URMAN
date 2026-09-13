using Godot;

namespace Urman.Godot;

/// <summary>
/// Continuous, restrained idle life for an authored character: a slow breath on
/// the spine and head plus a smaller arm sway, applied to bone poses.
///
/// Why this exists instead of the imported clips: the character kit exports
/// bone-parented meshes with no skin, so Blender's glTF animation lands in Godot
/// as tracks named after the bone (for example
/// "Alsu_Rig/Skeleton3D:Spine"). Those tracks are inert — playing the clip moves
/// neither the bone pose, nor the bone's global pose, nor the visible mesh —
/// which was measured in SceneSmokeTest. The authored Idle sway is also only
/// about one degree. This component therefore drives the same four bones the
/// clip targets, through the same API the woodpile resident already uses, until
/// the asset pipeline is changed to export a real skin.
///
/// Presentation only: no state, no collision, no interaction ownership.
/// </summary>
public partial class NpcIdleMotion : Node3D
{
    private Skeleton3D? _skeleton;
    private (int Bone, Quaternion Pose)[] _restPoses = [];
    private float _phase;
    private float _amplitudeScale = 1f;

    /// <summary>Tension states breathe less and hold the arms still.</summary>
    public void SetTension(bool tension) => _amplitudeScale = tension ? 0.45f : 1f;

    public override void _Ready()
    {
        SetMeta("presentationOnly", true);
        SetMeta("animationOwner", nameof(NpcIdleMotion));
        _skeleton = FindSkeleton(GetParent());
        if (_skeleton is null)
        {
            SetMeta("idleMotionStatus", "no-skeleton");
            SetProcess(false);
            return;
        }

        SetMeta("idleMotionBones", "Spine,Head,Arm.L,Arm.R");
        _restPoses = new[] { "Spine", "Head", "Arm.L", "Arm.R" }
            .Select(name => _skeleton.FindBone(name))
            .Where(bone => bone >= 0)
            .Select(bone => (bone, _skeleton.GetBonePoseRotation(bone)))
            .ToArray();
        if (_restPoses.Length == 0)
        {
            SetMeta("idleMotionStatus", "no-idle-bones");
            SetProcess(false);
            return;
        }

        // Desynchronise neighbours deterministically from the world position so
        // a group of characters never breathes in lockstep.
        var origin = GlobalPosition;
        _phase = Mathf.PosMod(origin.X * 1.7f + origin.Z * 2.3f, Mathf.Tau);
        SetMeta("idleMotionStatus", "bone-sway-active");
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_skeleton is null || _restPoses.Length == 0 || !GodotObject.IsInstanceValid(_skeleton))
        {
            return;
        }

        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
        {
            // Reduced motion stops the idle without snapping anyone: hold rest.
            foreach (var (bone, pose) in _restPoses)
            {
                _skeleton.SetBonePoseRotation(bone, pose);
            }
            return;
        }

        _phase += (float)delta;
        var breath = Mathf.Sin(_phase * 1.15f);
        var armSway = Mathf.Sin(_phase * 0.83f + 0.7f);
        for (var index = 0; index < _restPoses.Length; index++)
        {
            var (bone, pose) = _restPoses[index];
            var angle = index switch
            {
                0 => 0.022f * breath,
                1 => -0.014f * breath,
                2 => 0.011f * armSway,
                _ => -0.011f * armSway,
            } * _amplitudeScale;
            _skeleton.SetBonePoseRotation(bone, pose * new Quaternion(Vector3.Right, angle));
        }
    }

    private static Skeleton3D? FindSkeleton(Node? root)
    {
        if (root is null)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is Skeleton3D skeleton)
            {
                return skeleton;
            }

            if (FindSkeleton(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
