"""Pose-preserving Shurale animation actions for the MPFB game_engine rig.

`working_pose` is the reviewed, constraint-baked full pose produced by the
character exporter. Every sampled frame starts from a deep copy of that pose;
the sparse angle dictionaries below are additive Euler offsets in degrees,
converted by the exporter's existing `deg_pose` helper.

The current source has validated head/neck and finger axes, but no validated
arm Euler offsets: both arm chains are IK-baked. `arm_pose_overrides` is an
optional set of *absolute* bone channels captured from a reviewed pose. It is
intentionally limited to the right arm and right-hand digits so the left hand
continues to carry the attached work branch. Do not fill it with guessed
rotations.
"""

from copy import deepcopy


CLIPS = ("work", "noticed", "gesture", "speak", "yield", "step", "idle")
RIGHT_ARM_BONES = {"clavicle_r", "upperarm_r", "lowerarm_r", "hand_r"}
RIGHT_DIGIT_PREFIXES = ("thumb_", "index_", "middle_", "ring_", "pinky_")
ROOT_BONE_NAMES = {"root", "Root"}
STEP_FRAMES = (1, 5, 9, 13, 17, 21, 25, 29, 32)
LEG_CHAIN_BONES = ("thigh", "calf", "foot")
# Source-head measurement for the F14 approach point. The source face starts
# at -0.0045 degrees and the target heading is +40.3289 degrees, so this is a
# root-local vertical yaw delta, not a head-local Euler channel.
HEAD_NOTICE_YAW_DEG = 40.3334


def _with_offsets(working_pose, deg_pose, **bone_offsets):
    """Copy a full pose and add sparse degree offsets to existing channels."""
    pose = deepcopy(working_pose)
    for bone_name, offset in deg_pose(**bone_offsets).items():
        values = pose.setdefault(bone_name, {})
        current = values.get("rotation", (0.0, 0.0, 0.0))
        delta = offset["rotation"]
        values["rotation"] = tuple(float(a) + float(b) for a, b in zip(current, delta))
    return pose


def _is_right_arm_bone(bone_name):
    if bone_name in RIGHT_ARM_BONES:
        return True
    return bone_name.endswith("_r") and bone_name.startswith(RIGHT_DIGIT_PREFIXES)


def _compose_frame(base_pose, clip_name, frame, arm_pose_overrides):
    """Apply a reviewed right-arm pose sample without dropping other channels."""
    pose = deepcopy(base_pose)
    clip_overrides = arm_pose_overrides.get(clip_name, {})
    sample = clip_overrides.get(frame)
    if sample is None:
        return pose

    for bone_name, channels in sample.items():
        if bone_name in ROOT_BONE_NAMES or not _is_right_arm_bone(bone_name):
            raise ValueError(
                f"{clip_name} frame {frame} override may only pose the right arm; got {bone_name!r}"
            )
        if set(channels) - {"rotation"}:
            raise ValueError(
                f"{clip_name} frame {frame} override may not translate or scale {bone_name!r}"
            )
        if "rotation" in channels:
            if len(channels["rotation"]) != 3:
                raise ValueError(f"{clip_name} {bone_name!r} rotation must be XYZ radians")
            pose.setdefault(bone_name, {})["rotation"] = tuple(float(v) for v in channels["rotation"])
    return pose


def _pose_at(working_pose, deg_pose, clip_name, frame, offsets, arm_pose_overrides):
    pose = _with_offsets(working_pose, deg_pose, **offsets) if offsets else deepcopy(working_pose)
    return _compose_frame(pose, clip_name, frame, arm_pose_overrides)


def _apply_root_local_head_yaw(rig, pose, yaw_degrees):
    """Turn the evaluated head around root-up while preserving its pitch/up axis."""
    import bpy
    import math
    from mathutils import Quaternion, Vector

    root = rig.parent
    head = rig.pose.bones.get("head")
    if root is None or head is None:
        raise RuntimeError("Measured Shurale head yaw requires a parent root and head bone")

    animation_data = rig.animation_data
    saved_action = animation_data.action if animation_data else None
    saved_tracks = [(track, track.mute) for track in animation_data.nla_tracks] if animation_data else []
    saved_drivers = [(driver, driver.mute) for driver in animation_data.drivers] if animation_data else []
    scene = bpy.context.scene
    saved_frame = scene.frame_current
    try:
        if animation_data:
            animation_data.action = None
            for track, _ in saved_tracks:
                track.mute = True
            for driver, _ in saved_drivers:
                driver.mute = True
        scene.frame_set(1)
        _restore_working_pose(rig, pose)
        bpy.context.view_layer.update()

        source_matrix = head.matrix.copy()
        root_up_world = root.matrix_world.to_3x3() @ Vector((0.0, 0.0, 1.0))
        root_up_in_rig = rig.matrix_world.to_3x3().inverted() @ root_up_world
        if root_up_in_rig.length < 1e-6:
            raise RuntimeError("Measured Shurale head yaw has no stable root-up axis")
        yaw = Quaternion(root_up_in_rig.normalized(), math.radians(float(yaw_degrees)))
        target_matrix = source_matrix.copy()
        target_rotation = yaw.to_matrix() @ source_matrix.to_3x3()
        for row in range(3):
            for column in range(3):
                target_matrix[row][column] = target_rotation[row][column]

        head.matrix = target_matrix
        bpy.context.view_layer.update()
        channels = deepcopy(pose)
        head_channels = channels.setdefault("head", {})
        head_channels["rotation"] = tuple(float(value) for value in head.rotation_euler)
        head_channels["location"] = tuple(float(value) for value in head.location)

        # Ensure the local channels we return reproduce the desired evaluated
        # transform when key_action applies them to the full copied pose.
        head.rotation_euler = head_channels["rotation"]
        head.location = head_channels["location"]
        bpy.context.view_layer.update()
        actual_matrix = head.matrix
        matrix_error = max(
            abs(float(actual_matrix[row][column] - target_matrix[row][column]))
            for row in range(4)
            for column in range(4)
        )
        if matrix_error > 1e-4:
            raise RuntimeError(
                f"Measured Shurale head yaw channels miss the evaluated target by {matrix_error:.6f}"
            )
        return channels
    finally:
        if animation_data:
            animation_data.action = saved_action
            for track, muted in saved_tracks:
                track.mute = muted
            for driver, muted in saved_drivers:
                driver.mute = muted
        scene.frame_set(saved_frame)
        bpy.context.view_layer.update()


def _restore_working_pose(rig, working_pose):
    """Leave the source scene in the same reviewed pose used to build clips."""
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)
    for bone_name, channels in working_pose.items():
        bone = rig.pose.bones.get(bone_name)
        if bone is None:
            raise RuntimeError(f"Reviewed working pose references unavailable bone {bone_name!r}")
        if "rotation" in channels:
            bone.rotation_euler = channels["rotation"]
        if "location" in channels:
            bone.location = channels["location"]
        if "scale" in channels:
            bone.scale = channels["scale"]


def sample_measured_gesture_yield_overrides(
    rig,
    root,
    body,
    working_pose,
    bag_target_root_local,
    hand_target_root_local,
    hand_pole_root_local,
):
    """Solve source-measured right-arm gesture/yield poses on the frozen rig.

    The pointing clip moves the reviewed work wrist a small fraction of the
    measured wrist-to-bag gap and aligns the evaluated index ray to the bag
    point without claiming contact. Yield begins on that exact pose, withdraws
    the wrist to its work location, and turns the open palm toward root-forward.
    Only right-arm/digit rotation channels are returned for the existing
    ``arm_pose_overrides`` consumer; the left branch support is checked as an
    invariant. The source blend is never saved.
    """
    import math

    import bpy
    from mathutils import Matrix, Quaternion, Vector

    gesture_keys = (8, 15, 22, 30)
    yield_keys = (1, 8, 16, 24, 32)
    right_digits = ("thumb", "index", "middle", "ring", "pinky")
    long_digits = ("index", "middle", "ring", "pinky")
    pose_bone_order = (
        "upperarm_r",
        "lowerarm_r",
        "hand_r",
        *(f"{digit}_{segment:02d}_r" for digit in right_digits for segment in (1, 2, 3)),
    )
    scene = bpy.context.scene
    saved_frame = scene.frame_current
    animation_data = rig.animation_data
    saved_action = animation_data.action if animation_data else None
    saved_tracks = [(track, track.mute) for track in animation_data.nla_tracks] if animation_data else []
    saved_drivers = [(driver, driver.mute) for driver in animation_data.drivers] if animation_data else []
    root_matrix = root.matrix_world.copy()
    rig_matrix = rig.matrix_world.copy()
    root_location = root.location.copy()
    root_scale = root.scale.copy()
    rig_location = rig.location.copy()
    rig_scale = rig.scale.copy()
    root_inverse = root.matrix_world.inverted()
    target = None
    pole = None
    constraint = None
    arm_bone = rig.pose.bones.get("lowerarm_r")
    if arm_bone is None or body is None or rig.parent != root:
        raise RuntimeError("Measured Shurale hand motion requires the reviewed root, rig, body, and right forearm")
    if arm_bone.constraints:
        raise RuntimeError(
            "Measured Shurale hand motion expects the exported right-arm source pose to have baked constraints"
        )

    def root_point(pose_point):
        return root_inverse @ (rig.matrix_world @ pose_point)

    def matrix_error(a, b):
        return max(
            abs(float(a[row][column] - b[row][column]))
            for row in range(4)
            for column in range(4)
        )

    def average(points):
        result = Vector((0.0, 0.0, 0.0))
        for point in points:
            result += point
        return result / len(points)

    def set_target(point):
        target.location = Vector(point)
        bpy.context.view_layer.update()

    def set_finger_profile(profile):
        for digit in right_digits:
            values = profile[digit]
            for segment, value in enumerate(values, start=1):
                bone = rig.pose.bones[f"{digit}_{segment:02d}_r"]
                bone.rotation_mode = "XYZ"
                source = working_pose[bone.name].get("rotation", (0.0, 0.0, 0.0))
                bone.rotation_euler = (float(value), float(source[1]), float(source[2]))
        bpy.context.view_layer.update()

    def blended_profile(start, end, amount):
        return {
            digit: tuple(
                float(start[digit][index]) + (float(end[digit][index]) - float(start[digit][index])) * amount
                for index in range(3)
            )
            for digit in right_digits
        }

    def point_direction():
        start = root_point(rig.pose.bones["index_01_r"].head)
        end = root_point(rig.pose.bones["index_03_r"].tail)
        axis = end - start
        target_direction = Vector(bag_target_root_local) - start
        if axis.length < 1e-5 or target_direction.length < 1e-5:
            raise RuntimeError("Measured Shurale index ray or bag direction is degenerate")
        return axis.normalized().rotation_difference(target_direction.normalized()), axis.angle(target_direction)

    def apply_root_rotation(delta):
        hand = rig.pose.bones["hand_r"]
        hand_root_matrix = root_inverse @ (rig.matrix_world @ hand.matrix)
        rotated_basis = delta.to_matrix() @ hand_root_matrix.to_3x3()
        desired = hand_root_matrix.copy()
        for row in range(3):
            for column in range(3):
                desired[row][column] = rotated_basis[row][column]
        desired.translation = hand_root_matrix.translation
        hand.matrix = rig.matrix_world.inverted() @ (root.matrix_world @ desired)
        bpy.context.view_layer.update()

    def palm_basis(palm_side_sign):
        wrist = root_point(rig.pose.bones["hand_r"].head)
        base_points = [
            root_point(rig.pose.bones[f"{digit}_01_r"].head)
            for digit in long_digits
        ]
        finger_axis = average(base_points) - wrist
        across_axis = (
            root_point(rig.pose.bones["index_01_r"].head)
            - root_point(rig.pose.bones["pinky_01_r"].head)
        )
        if finger_axis.length < 1e-5:
            raise RuntimeError("Measured Shurale palm has no stable finger axis")
        finger_axis.normalize()
        across_axis -= finger_axis * across_axis.dot(finger_axis)
        if across_axis.length < 1e-5:
            raise RuntimeError("Measured Shurale palm has no stable across-palm axis")
        across_axis.normalize()
        normal_axis = finger_axis.cross(across_axis).normalized()
        if palm_side_sign < 0.0:
            across_axis.negate()
            normal_axis.negate()
        return Matrix((
            (finger_axis.x, across_axis.x, normal_axis.x),
            (finger_axis.y, across_axis.y, normal_axis.y),
            (finger_axis.z, across_axis.z, normal_axis.z),
        ))

    def palm_open_delta(palm_side_sign):
        current = palm_basis(palm_side_sign)
        # In root-local Blender axes, actor-forward is +Y and anatomical-up is
        # +Z. Keep the palm facing forward while the fingers relax upward.
        target_finger = Vector((0.0, 0.0, 1.0))
        target_across = Vector((1.0, 0.0, 0.0))
        target_normal = Vector((0.0, 1.0, 0.0))
        desired = Matrix((
            (target_finger.x, target_across.x, target_normal.x),
            (target_finger.y, target_across.y, target_normal.y),
            (target_finger.z, target_across.z, target_normal.z),
        ))
        return (desired @ current.transposed()).to_quaternion()

    def bake_right_channels():
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        depsgraph.update()
        evaluated_rig = rig.evaluated_get(depsgraph)
        matrices = {
            name: evaluated_rig.pose.bones[name].matrix.copy()
            for name in pose_bone_order
        }
        constraint.mute = True
        for name in pose_bone_order:
            rig.pose.bones[name].matrix = matrices[name]
            bpy.context.view_layer.update()
        evaluated_rig = rig.evaluated_get(depsgraph)
        error = max(
            matrix_error(evaluated_rig.pose.bones[name].matrix, matrices[name])
            for name in pose_bone_order
        )
        constraint.mute = False
        bpy.context.view_layer.update()
        if error > 2e-4:
            raise RuntimeError(f"Measured Shurale right-arm channel bake missed evaluated pose by {error:.6f}")
        return {
            name: {"rotation": tuple(float(value) for value in rig.pose.bones[name].rotation_euler)}
            for name in pose_bone_order
        }, error

    def support_matrices():
        branch = bpy.data.objects.get("ShuraleWorkBranch")
        if branch is None:
            raise RuntimeError("Measured Shurale support invariant requires the attached ShuraleWorkBranch")
        return {
            "hand_l": rig.pose.bones["hand_l"].matrix.copy(),
            "branch": branch.matrix_world.copy(),
        }

    def support_error(baseline):
        current = support_matrices()
        return max(matrix_error(current[name], baseline[name]) for name in baseline)

    def index_skin_tip_to_target():
        group = body.vertex_groups.get("index_03_r")
        if group is None:
            raise RuntimeError("Measured Shurale index tip has no authored index_03_r skin group")
        depsgraph = bpy.context.evaluated_depsgraph_get()
        depsgraph.update()
        body_eval = body.evaluated_get(depsgraph)
        evaluated_mesh = body_eval.to_mesh()
        try:
            if len(evaluated_mesh.vertices) != len(body.data.vertices):
                raise RuntimeError("Measured Shurale body topology changed during hand-pose evaluation")
            start = root_point(rig.pose.bones["index_01_r"].head)
            axis = root_point(rig.pose.bones["index_03_r"].tail) - start
            if axis.length < 1e-5:
                raise RuntimeError("Measured Shurale index skin tip has no stable evaluated ray")
            axis.normalize()
            candidates = []
            for vertex in body.data.vertices:
                weight = sum(
                    assignment.weight
                    for assignment in vertex.groups
                    if assignment.group == group.index
                )
                if weight < 0.10:
                    continue
                point = root_inverse @ (body_eval.matrix_world @ evaluated_mesh.vertices[vertex.index].co)
                candidates.append((float((point - start).dot(axis)), point))
            if not candidates:
                raise RuntimeError("Measured Shurale index_03_r has no evaluated skin vertices")
            tip = max(candidates, key=lambda item: item[0])[1]
            return float((Vector(bag_target_root_local) - tip).length)
        finally:
            body_eval.to_mesh_clear()

    try:
        if animation_data:
            animation_data.action = None
            for track, _ in saved_tracks:
                track.mute = True
            for driver, _ in saved_drivers:
                driver.mute = True
        scene.frame_set(1)
        _restore_working_pose(rig, working_pose)
        bpy.context.view_layer.update()

        wrist_source = Vector(hand_target_root_local)
        pole_source = Vector(hand_pole_root_local)
        bag_target = Vector(bag_target_root_local)
        current_wrist = root_point(rig.pose.bones["hand_r"].head)
        source_target_error = (current_wrist - wrist_source).length
        if source_target_error > 0.003:
            raise RuntimeError(
                f"Measured Shurale source wrist misses its manifest target by {source_target_error:.6f} m"
            )
        gap = (bag_target - wrist_source).length
        if gap < 0.25:
            raise RuntimeError("Measured Shurale bag point is too close to establish a non-contact pointing pose")
        toward_bag = (bag_target - wrist_source).normalized()
        gesture_wrist = wrist_source + toward_bag * (gap * 0.10)
        yield_wrist = wrist_source.copy()

        target = bpy.data.objects.new("Shurale_MeasuredGesture_Target", None)
        pole = bpy.data.objects.new("Shurale_MeasuredGesture_Pole", None)
        scene.collection.objects.link(target)
        scene.collection.objects.link(pole)
        for obj, location in ((target, wrist_source), (pole, pole_source)):
            obj.empty_display_type = "SPHERE"
            obj.empty_display_size = 0.025
            obj.parent = root
            obj.matrix_parent_inverse.identity()
            obj.location = location

        source_matrices = {
            name: rig.pose.bones[name].matrix.copy()
            for name in ("upperarm_r", "lowerarm_r", "hand_r")
        }
        constraint = arm_bone.constraints.new("IK")
        constraint.name = "Measured gesture/yield IK right arm"
        constraint.target = target
        constraint.pole_target = pole
        constraint.chain_count = 2
        constraint.iterations = 160
        constraint.use_stretch = False
        constraint.pole_angle = 0.0

        bpy.context.view_layer.update()
        ik_matrices = rig.evaluated_get(bpy.context.evaluated_depsgraph_get()).pose.bones
        baseline_ik_matrix_error = max(
            matrix_error(ik_matrices[name].matrix, source_matrices[name])
            for name in source_matrices
        )
        baseline_ik_wrist_error = (
            root_point(rig.pose.bones["hand_r"].head) - wrist_source
        ).length
        if baseline_ik_wrist_error > 0.003:
            raise RuntimeError(
                f"Measured Shurale baseline IK missed the reviewed wrist by {baseline_ik_wrist_error:.6f} m"
            )

        support_baseline = support_matrices()
        # Finger curl displaces the tips toward the palm. This evaluated
        # source delta fixes which side of the hand is the palm without an
        # assumed bone-axis sign.
        curled_tips = [
            root_point(rig.pose.bones[f"{digit}_03_r"].tail)
            for digit in long_digits
        ]
        curled_mean = average(curled_tips)
        for digit in right_digits:
            for segment in (1, 2, 3):
                rig.pose.bones[f"{digit}_{segment:02d}_r"].rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.view_layer.update()
        straight_tips = [
            root_point(rig.pose.bones[f"{digit}_03_r"].tail)
            for digit in long_digits
        ]
        curl_direction = curled_mean - average(straight_tips)
        if curl_direction.length < 1e-5:
            raise RuntimeError("Measured Shurale v6 hand curl does not expose a stable palm side")
        curl_basis = palm_basis(1.0)
        palm_side_sign = 1.0 if curl_basis.col[2].dot(curl_direction) >= 0.0 else -1.0

        work_profile = {
            digit: tuple(
                float(working_pose[f"{digit}_{segment:02d}_r"]["rotation"][0])
                for segment in (1, 2, 3)
            )
            for digit in right_digits
        }
        gesture_profile = {
            "thumb": (0.30, 0.38, 0.28),
            "index": (0.0, 0.0, 0.0),
            "middle": work_profile["middle"],
            "ring": work_profile["ring"],
            "pinky": work_profile["pinky"],
        }
        yield_profile = {
            "thumb": (0.10, 0.12, 0.08),
            "index": (0.08, 0.07, 0.05),
            "middle": (0.12, 0.11, 0.08),
            "ring": (0.12, 0.11, 0.08),
            "pinky": (0.10, 0.09, 0.07),
        }

        overrides = {"gesture": {}, "yield": {}}
        measurements = {
            "bag_target_root_local_m": tuple(float(value) for value in bag_target),
            "source_work_wrist_root_local_m": tuple(float(value) for value in wrist_source),
            "gesture_wrist_root_local_m": tuple(float(value) for value in gesture_wrist),
            "yield_wrist_root_local_m": tuple(float(value) for value in yield_wrist),
            "source_wrist_to_bag_distance_m": float(gap),
            "gesture_reach_toward_bag_m": float((gesture_wrist - wrist_source).length),
            "yield_retreat_from_gesture_m": float((gesture_wrist - yield_wrist).length),
            "source_wrist_manifest_error_m": float(source_target_error),
            "baseline_ik_wrist_error_m": float(baseline_ik_wrist_error),
            "baseline_ik_matrix_max_error": float(baseline_ik_matrix_error),
            "left_hand_branch_max_matrix_drift": 0.0,
            "max_right_pose_bake_matrix_error": 0.0,
            "gesture_index_aim_error_degrees": None,
            "gesture_index_tip_bone_to_bag_distance_m": None,
            "gesture_index_tip_to_bag_distance_m": None,
            "yield_palm_forward_dot": None,
            "yield_fingers_up_dot": None,
            "right_arm_bone_order": list(pose_bone_order),
        }

        def progress(frame, final_frame):
            amount = min(1.0, max(0.0, (float(frame) - 1.0) / (float(final_frame) - 1.0)))
            return amount * amount * (3.0 - 2.0 * amount)

        gesture_delta_final = Quaternion((1.0, 0.0, 0.0, 0.0))
        for frame in gesture_keys:
            amount = progress(frame, 15)
            _restore_working_pose(rig, working_pose)
            set_target(wrist_source.lerp(gesture_wrist, amount))
            set_finger_profile(blended_profile(work_profile, gesture_profile, amount))
            pointing_delta, _ = point_direction()
            delta = Quaternion((1.0, 0.0, 0.0, 0.0)).slerp(pointing_delta, amount)
            apply_root_rotation(delta)
            applied = delta.copy()
            if amount >= 0.999:
                for _ in range(4):
                    correction, error_radians = point_direction()
                    if math.degrees(error_radians) <= 0.15:
                        break
                    apply_root_rotation(correction)
                    applied = correction @ applied
                _, error_radians = point_direction()
                if math.degrees(error_radians) > 0.25:
                    raise RuntimeError(
                        f"Measured Shurale index ray misses bag point by {math.degrees(error_radians):.3f} degrees"
                    )
                gesture_delta_final = applied.copy()
                measurements["gesture_index_aim_error_degrees"] = float(math.degrees(error_radians))
                start = root_point(rig.pose.bones["index_01_r"].head)
                tip = root_point(rig.pose.bones["index_03_r"].tail)
                measurements["gesture_index_tip_bone_to_bag_distance_m"] = float((bag_target - tip).length)
                measurements["gesture_index_tip_to_bag_distance_m"] = index_skin_tip_to_target()

            baked, bake_error = bake_right_channels()
            overrides["gesture"][frame] = baked
            measurements["max_right_pose_bake_matrix_error"] = max(
                measurements["max_right_pose_bake_matrix_error"], float(bake_error)
            )
            measurements["left_hand_branch_max_matrix_drift"] = max(
                measurements["left_hand_branch_max_matrix_drift"], support_error(support_baseline)
            )
        gesture_final_pose = overrides["gesture"][30]
        overrides["yield"][1] = gesture_final_pose

        for frame in yield_keys[1:]:
            amount = progress(frame, 16)
            _restore_working_pose(rig, working_pose)
            set_target(gesture_wrist.lerp(yield_wrist, amount))
            set_finger_profile(blended_profile(gesture_profile, yield_profile, amount))
            open_delta = palm_open_delta(palm_side_sign)
            delta = gesture_delta_final.slerp(open_delta, amount)
            apply_root_rotation(delta)
            baked, bake_error = bake_right_channels()
            overrides["yield"][frame] = baked
            measurements["max_right_pose_bake_matrix_error"] = max(
                measurements["max_right_pose_bake_matrix_error"], float(bake_error)
            )
            measurements["left_hand_branch_max_matrix_drift"] = max(
                measurements["left_hand_branch_max_matrix_drift"], support_error(support_baseline)
            )
            if amount >= 0.999:
                final_basis = palm_basis(palm_side_sign)
                measurements["yield_palm_forward_dot"] = float(final_basis.col[2].dot(Vector((0.0, 1.0, 0.0))))
                measurements["yield_fingers_up_dot"] = float(final_basis.col[0].dot(Vector((0.0, 0.0, 1.0))))

        if measurements["left_hand_branch_max_matrix_drift"] > 1e-5:
            raise RuntimeError(
                "Measured Shurale right-arm motion moved the left branch support by "
                f"{measurements['left_hand_branch_max_matrix_drift']:.6f}"
            )
        if root.location != root_location or root.scale != root_scale or rig.location != rig_location or rig.scale != rig_scale:
            raise RuntimeError("Measured Shurale right-arm motion changed root/rig location or scale")
        if matrix_error(root.matrix_world, root_matrix) > 1e-6 or matrix_error(rig.matrix_world, rig_matrix) > 1e-6:
            raise RuntimeError("Measured Shurale right-arm motion changed root/rig world transforms")
        return overrides, measurements
    finally:
        if constraint is not None and arm_bone.constraints.get(constraint.name) is constraint:
            arm_bone.constraints.remove(constraint)
        for obj in (target, pole):
            if obj is not None and bpy.data.objects.get(obj.name) is obj:
                bpy.data.objects.remove(obj, do_unlink=True)
        _restore_working_pose(rig, working_pose)
        if animation_data:
            animation_data.action = saved_action
            for track, muted in saved_tracks:
                track.mute = muted
            for driver, muted in saved_drivers:
                driver.mute = muted
        scene.frame_set(saved_frame)
        bpy.context.view_layer.update()


def sample_measured_in_place_step(rig, working_pose):
    """Bake a restrained, root-local right-foot settling step from measured IK.

    The left foot stays at its measured source-world endpoint. The right foot
    makes a short forward-and-return arc whose distance/lift are proportions of
    the source leg length. Pole locations come from the source knee bend plane;
    no leg Euler signs or root motion are authored by hand.

    Returns ``(frames, measurements)``. Each frame pose is a complete copy of
    the reviewed work pose plus the two sampled IK leg chains.
    """
    import math

    import bpy
    from mathutils import Vector

    root = bpy.data.objects.get("ShuraleRoot")
    if root is None or rig.parent != root:
        raise RuntimeError("Measured Shurale step requires ShuraleRig parented to ShuraleRoot")
    required = {
        f"{part}_{side}"
        for side in ("l", "r")
        for part in LEG_CHAIN_BONES
    }
    missing = required.difference(rig.pose.bones.keys())
    if missing:
        raise RuntimeError(f"Measured Shurale step is missing leg-chain bones: {sorted(missing)}")

    for side in ("l", "r"):
        foot = rig.pose.bones[f"foot_{side}"]
        if len(foot.constraints):
            raise RuntimeError(
                f"Measured Shurale step expects an un-constrained foot bone; found constraints on {foot.name}"
            )

    scene = bpy.context.scene
    old_action = rig.animation_data.action if rig.animation_data else None
    root_matrix = root.matrix_world.copy()
    root_inverse = root_matrix.inverted()
    root_location = root.location.copy()
    root_scale = root.scale.copy()
    rig_location = rig.location.copy()
    rig_scale = rig.scale.copy()
    targets = {}
    poles = {}
    constraints = {}
    constraint_names = {}
    baked_action = None
    try:
        if rig.animation_data:
            rig.animation_data.action = None
        _restore_working_pose(rig, working_pose)
        scene.frame_set(1)
        bpy.context.view_layer.update()

        def to_world(pose_point):
            return rig.matrix_world @ pose_point

        def to_root(point_world):
            return root_inverse @ point_world

        source = {}
        for side in ("l", "r"):
            thigh = rig.pose.bones[f"thigh_{side}"]
            calf = rig.pose.bones[f"calf_{side}"]
            foot = rig.pose.bones[f"foot_{side}"]
            hip = to_root(to_world(thigh.head))
            knee = to_root(to_world(calf.head))
            endpoint = to_root(to_world(foot.tail))
            foot_root_matrix = root_inverse @ (rig.matrix_world @ foot.matrix)
            _, foot_rotation, _ = foot_root_matrix.decompose()
            line = endpoint - hip
            if line.length < 1e-4:
                raise RuntimeError(f"Measured Shurale {side} leg has a degenerate hip-to-foot axis")
            line_direction = line.normalized()
            knee_projection = hip + line_direction * (knee - hip).dot(line_direction)
            bend = knee - knee_projection
            if bend.length < 1e-4:
                raise RuntimeError(f"Measured Shurale {side} leg has no stable source knee bend plane")
            leg_length = sum(float(rig.pose.bones[f"{part}_{side}"].bone.length) for part in LEG_CHAIN_BONES)
            if leg_length <= 0.1:
                raise RuntimeError(f"Measured Shurale {side} leg length is invalid: {leg_length:.6f} m")
            pole = knee + bend.normalized() * (leg_length * 0.5)
            source[side] = {
                "hip": hip,
                "knee": knee,
                "endpoint": endpoint,
                "foot_rotation": foot_rotation,
                "pole": pole,
                "leg_length": leg_length,
            }

        def make_root_empty(name, root_local):
            obj = bpy.data.objects.new(name, None)
            scene.collection.objects.link(obj)
            obj.empty_display_type = "SPHERE"
            obj.empty_display_size = 0.018
            obj.parent = root
            obj.matrix_parent_inverse.identity()
            obj.location = root_local
            return obj

        for side in ("l", "r"):
            endpoint = source[side]["endpoint"]
            targets[side] = make_root_empty(f"ShuraleStepIKTarget_{side}", endpoint)
            targets[side].rotation_mode = "XYZ"
            targets[side].rotation_euler = source[side]["foot_rotation"].to_euler("XYZ")
            poles[side] = make_root_empty(f"ShuraleStepIKPole_{side}", source[side]["pole"])
            constraint = rig.pose.bones[f"foot_{side}"].constraints.new("IK")
            constraint.name = f"Measured in-place step IK {side}"
            constraint.target = targets[side]
            constraint.pole_target = poles[side]
            constraint.chain_count = len(LEG_CHAIN_BONES)
            constraint.iterations = 160
            constraint.use_stretch = False
            constraint.use_rotation = True
            if hasattr(constraint, "use_tail"):
                constraint.use_tail = True
            constraints[side] = constraint
            constraint_names[side] = constraint.name

        def update_pose():
            bpy.context.view_layer.update()
            bpy.context.evaluated_depsgraph_get().update()

        def solve_error(side):
            update_pose()
            source_knee = source[side]["knee"]
            source_end = source[side]["endpoint"]
            current_knee = to_root(to_world(rig.pose.bones[f"calf_{side}"].head))
            current_end = to_root(to_world(rig.pose.bones[f"foot_{side}"].tail))
            knee_error = float((current_knee - source_knee).length)
            endpoint_error = float((current_end - source_end).length)
            return knee_error + endpoint_error, knee_error, endpoint_error

        def foot_orientation_error(side):
            update_pose()
            foot = rig.pose.bones[f"foot_{side}"]
            foot_root_matrix = root_inverse @ (rig.matrix_world @ foot.matrix)
            _, current_rotation, _ = foot_root_matrix.decompose()
            return float(current_rotation.rotation_difference(source[side]["foot_rotation"]).angle)

        # Calibrate the solver pole angle against the measured source knee and
        # endpoint. This handles the rig's authored bone rolls without guessed
        # Euler signs or mirrored-side assumptions.
        pole_angles = {}
        baseline_pose_errors = {}
        for side in ("l", "r"):
            constraint = constraints[side]
            sample_count = 36
            step_angle = math.tau / sample_count
            candidates = []
            for index in range(sample_count):
                angle = step_angle * index
                constraint.pole_angle = angle
                total_error, knee_error, endpoint_error = solve_error(side)
                candidates.append((total_error, angle, knee_error, endpoint_error))
            _, chosen, knee_error, endpoint_error = min(candidates)
            window = step_angle
            for _ in range(5):
                refined = []
                for offset in (-0.5, -0.25, 0.0, 0.25, 0.5):
                    angle = (chosen + window * offset) % math.tau
                    constraint.pole_angle = angle
                    total_error, knee_error, endpoint_error = solve_error(side)
                    refined.append((total_error, angle, knee_error, endpoint_error))
                _, chosen, knee_error, endpoint_error = min(refined)
                window *= 0.5
            constraint.pole_angle = chosen
            update_pose()
            orientation_error = foot_orientation_error(side)
            if knee_error > 0.035 or endpoint_error > 0.008 or orientation_error > 0.02:
                raise RuntimeError(
                    f"Measured Shurale {side} IK does not reproduce the source knee/foot pose "
                    f"(knee {knee_error:.4f} m, endpoint {endpoint_error:.4f} m, "
                    f"foot orientation {orientation_error:.4f} rad)"
                )
            pole_angles[side] = chosen
            baseline_pose_errors[side] = {
                "knee_m": knee_error,
                "endpoint_m": endpoint_error,
                "foot_orientation_rad": orientation_error,
            }

        step_side, support_side = "r", "l"
        leg_length = source[step_side]["leg_length"]
        step_distance = min(0.10, leg_length * 0.11)
        lift = min(0.05, leg_length * 0.055)
        # Two gently eased arcs: a small forward settling step, brief plant,
        # then a short return to the measured standing footprint.
        normalized_path = {
            1: (0.00, 0.00),
            5: (0.00, 0.00),
            9: (0.08, 0.28),
            13: (0.42, 0.82),
            17: (0.82, 0.62),
            21: (1.00, 0.00),
            25: (0.92, 0.00),
            29: (0.55, 0.42),
            32: (0.00, 0.00),
        }

        # Animate the measured endpoints and bake the evaluated IK result.
        # PoseBone.rotation_euler remains the pre-constraint channel while IK
        # is active, so reading it directly would silently produce a static
        # action. The existing character exporter uses this same visual-bake
        # route for its reviewed work pose.
        for frame in STEP_FRAMES:
            forward_fraction, lift_fraction = normalized_path[frame]
            offset = Vector((0.0, step_distance * forward_fraction, lift * lift_fraction))
            targets[support_side].location = source[support_side]["endpoint"]
            targets[support_side].keyframe_insert(data_path="location", frame=frame)
            targets[step_side].location = source[step_side]["endpoint"] + offset
            targets[step_side].keyframe_insert(data_path="location", frame=frame)

        for target in targets.values():
            action = target.animation_data.action if target.animation_data else None
            if action is None:
                raise RuntimeError(f"Measured Shurale IK target {target.name} has no location action")
            for curve in action.fcurves:
                for key in curve.keyframe_points:
                    key.interpolation = "BEZIER"
                    key.handle_left_type = "AUTO_CLAMPED"
                    key.handle_right_type = "AUTO_CLAMPED"

        scene.frame_set(1)
        update_pose()
        if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")
        bpy.ops.object.select_all(action="DESELECT")
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.mode_set(mode="POSE")
        bpy.ops.nla.bake(
            frame_start=STEP_FRAMES[0],
            frame_end=STEP_FRAMES[-1],
            step=1,
            only_selected=False,
            visual_keying=True,
            clear_constraints=False,
            clear_parents=False,
            use_current_action=False,
            clean_curves=False,
            bake_types={"POSE"},
        )
        bpy.ops.object.mode_set(mode="OBJECT")
        baked_action = rig.animation_data.action if rig.animation_data else None
        if baked_action is None or not baked_action.fcurves:
            raise RuntimeError("Measured Shurale visual IK bake produced no pose action")

        for side, constraint in constraints.items():
            bone = rig.pose.bones[f"foot_{side}"]
            baked_constraint = bone.constraints.get(constraint_names[side])
            if baked_constraint is not None:
                bone.constraints.remove(baked_constraint)

        frames = []
        sample_data = []
        for frame in range(STEP_FRAMES[0], STEP_FRAMES[-1] + 1):
            scene.frame_set(frame)
            update_pose()

            pose = deepcopy(working_pose)
            chain_data = {}
            for side in ("l", "r"):
                chain_data[side] = {}
                for part in LEG_CHAIN_BONES:
                    bone_name = f"{part}_{side}"
                    bone = rig.pose.bones[bone_name]
                    channels = {
                        "rotation": tuple(float(v) for v in bone.rotation_euler),
                        "location": tuple(float(v) for v in bone.location),
                        "scale": tuple(float(v) for v in bone.scale),
                    }
                    pose[bone_name] = channels
                    chain_data[side][bone_name] = channels
            frames.append((frame, pose))
            current_support_end = to_root(to_world(rig.pose.bones[f"foot_{support_side}"].tail))
            current_step_end = to_root(to_world(rig.pose.bones[f"foot_{step_side}"].tail))
            current_step_orientation = foot_orientation_error(step_side)
            support_target = source[support_side]["endpoint"]
            # The endpoint is also checked against the generated target curve
            # at this frame; use the evaluated target object, not the sparse
            # control sample, so Bézier interpolation is included.
            target_obj = targets[step_side]
            target_obj_eval = target_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            step_target = to_root(target_obj_eval.matrix_world.translation)
            sample_data.append({
                "frame": frame,
                "support_foot_endpoint_error_m": float((current_support_end - support_target).length),
                "step_foot_endpoint_target_error_m": float((current_step_end - step_target).length),
                "step_foot_orientation_error_rad": current_step_orientation,
                "step_foot_endpoint_root_local_m": tuple(float(v) for v in current_step_end),
                "step_target_root_local_m": tuple(float(v) for v in step_target),
                "support_chain_pose": chain_data[support_side],
                "step_chain_pose": chain_data[step_side],
            })
            if (current_support_end - support_target).length > 0.002:
                raise RuntimeError(
                    f"Measured Shurale support foot drifted at frame {frame}: "
                    f"{(current_support_end - support_target).length:.6f} m"
                )
            if (current_step_end - step_target).length > 0.012:
                raise RuntimeError(
                    f"Measured Shurale step foot missed its IK target at frame {frame}: "
                    f"{(current_step_end - step_target).length:.6f} m"
                )

        measurements = {
            "support_side": support_side,
            "step_side": step_side,
            "leg_length_m": leg_length,
            "step_distance_m": step_distance,
            "lift_m": lift,
            "baseline_endpoints_root_local_m": {
                side: tuple(float(v) for v in data["endpoint"]) for side, data in source.items()
            },
            "baseline_knees_root_local_m": {
                side: tuple(float(v) for v in data["knee"]) for side, data in source.items()
            },
            "pole_angles_rad": pole_angles,
            "baseline_pose_errors_m": baseline_pose_errors,
            "samples": sample_data,
        }
        return frames, measurements
    finally:
        if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")
        for side, constraint in constraints.items():
            bone = rig.pose.bones.get(f"foot_{side}")
            if bone is not None:
                remaining_constraint = bone.constraints.get(constraint_names[side])
                if remaining_constraint is not None:
                    bone.constraints.remove(remaining_constraint)
        for obj in (*targets.values(), *poles.values()):
            if bpy.data.objects.get(obj.name) is obj:
                bpy.data.objects.remove(obj, do_unlink=True)
        _restore_working_pose(rig, working_pose)
        if rig.animation_data:
            rig.animation_data.action = old_action
        if baked_action is not None and bpy.data.actions.get(baked_action.name) is baked_action:
            bpy.data.actions.remove(baked_action)
        scene.frame_set(1)
        bpy.context.view_layer.update()
        if any(abs(float(a) - float(b)) > 1e-6 for a, b in zip(root.location, root_location)):
            raise RuntimeError("Measured Shurale step changed the ShuraleRoot location")
        if any(abs(float(a) - float(b)) > 1e-6 for a, b in zip(root.scale, root_scale)):
            raise RuntimeError("Measured Shurale step changed the ShuraleRoot scale")
        if any(abs(float(a) - float(b)) > 1e-6 for a, b in zip(rig.location, rig_location)):
            raise RuntimeError("Measured Shurale step changed the ShuraleRig location")
        if any(abs(float(a) - float(b)) > 1e-6 for a, b in zip(rig.scale, rig_scale)):
            raise RuntimeError("Measured Shurale step changed the ShuraleRig scale")


def create_shurale_animations(
    rig,
    working_pose,
    key_action,
    deg_pose,
    fps,
    arm_pose_overrides=None,
    step_pose_samples=None,
):
    """Create the seven established Shurale actions from the full work pose.

    `arm_pose_overrides` maps clip names to `{frame: {bone: channels}}`, where
    channels use the same absolute radians schema as `working_pose`. Samples
    can animate only right-arm joints/digits; the reviewed left branch grip and
    root position remain intact. Until visual samples are approved, gesture and
    yield retain the working arm pose rather than inventing arm-axis signs.
    """
    import bpy

    if fps <= 0:
        raise ValueError("Shurale animation FPS must be positive")
    if not any(name == "head" for name in working_pose):
        raise ValueError("Shurale working pose must include the head channel")
    if not any(name == "neck_01" for name in working_pose):
        raise ValueError("Shurale working pose must include neck_01")
    if any(
        any(abs(float(v)) > 1e-6 for v in channels.get("location", (0.0, 0.0, 0.0)))
        for name, channels in working_pose.items()
        if name in ROOT_BONE_NAMES
    ):
        raise ValueError("Shurale animation source pose must not translate the root bone")

    arm_pose_overrides = arm_pose_overrides or {}
    unknown_clips = set(arm_pose_overrides) - set(CLIPS)
    if unknown_clips:
        raise ValueError(f"Unknown Shurale arm override clips: {sorted(unknown_clips)}")

    # Head yaw comes from the measured F14 approach heading. Apply it around
    # root-up in evaluated space so the head keeps its authored upright axis;
    # it stays head-only until shoulder follow has its own evaluated evidence.
    work_glance = {"head": (-1.2, 0.0, 0.0), "neck_01": (-0.35, 0.0, 0.0)}
    noticed_turn_early = {
        "head": (-3.2, 0.0, 0.0),
        "neck_01": (-1.0, 0.0, 0.0),
    }
    noticed_lift = {
        "head": (-3.2, 0.0, 0.0),
        "neck_01": (-1.0, 0.0, 0.0),
    }
    noticed_hold = {
        "head": (-2.4, 0.0, 0.0),
        "neck_01": (-0.8, 0.0, 0.0),
    }
    strap_glance = {
        "head": (1.2, 0.0, 0.0),
        "neck_01": (0.35, 0.0, 0.0),
    }
    speech_up = {
        "head": (-0.8, 0.0, 0.0),
        "neck_01": (-0.25, 0.0, 0.0),
    }
    speech_down = {
        "head": (0.7, 0.0, 0.0),
        "neck_01": (0.2, 0.0, 0.0),
    }
    yield_look = {
        "head": (-0.8, 0.0, 0.0),
        "neck_01": (-0.2, 0.0, 0.0),
    }
    idle_breath_in = {"head": (0.45, 0.0, 0.0), "neck_01": (0.15, 0.0, 0.0)}
    idle_breath_out = {"head": (-0.35, 0.0, 0.0), "neck_01": (-0.1, 0.0, 0.0)}

    def frames(clip_name, authored):
        result = []
        for sample in authored:
            frame, offsets = sample[:2]
            pose = _pose_at(working_pose, deg_pose, clip_name, frame, offsets, arm_pose_overrides)
            yaw_degrees = float(sample[2]) if len(sample) > 2 else 0.0
            if yaw_degrees:
                pose = _apply_root_local_head_yaw(rig, pose, yaw_degrees)
            result.append((frame, pose))
        return result

    key_action(rig, "work", frames("work", [
        (1, {}), (15, work_glance), (30, {})
    ]))
    key_action(rig, "noticed", frames("noticed", [
        (1, {}),
        (6, noticed_turn_early, HEAD_NOTICE_YAW_DEG * 0.5),
        (12, noticed_lift, HEAD_NOTICE_YAW_DEG),
        (18, noticed_hold, HEAD_NOTICE_YAW_DEG),
        (25, noticed_hold, HEAD_NOTICE_YAW_DEG),
        (32, noticed_hold, HEAD_NOTICE_YAW_DEG),
    ]))
    key_action(rig, "gesture", frames("gesture", [
        (1, noticed_hold, HEAD_NOTICE_YAW_DEG),
        (8, noticed_hold, HEAD_NOTICE_YAW_DEG),
        (15, strap_glance, HEAD_NOTICE_YAW_DEG),
        (22, strap_glance, HEAD_NOTICE_YAW_DEG),
        (30, strap_glance, HEAD_NOTICE_YAW_DEG),
    ]))
    key_action(rig, "speak", frames("speak", [
        (1, strap_glance, HEAD_NOTICE_YAW_DEG),
        (8, speech_up, HEAD_NOTICE_YAW_DEG),
        (15, speech_down, HEAD_NOTICE_YAW_DEG),
        (22, speech_up, HEAD_NOTICE_YAW_DEG),
        (30, strap_glance, HEAD_NOTICE_YAW_DEG),
    ]))
    key_action(rig, "yield", frames("yield", [
        (1, strap_glance, HEAD_NOTICE_YAW_DEG),
        (8, yield_look, HEAD_NOTICE_YAW_DEG),
        (16, yield_look, HEAD_NOTICE_YAW_DEG),
        (24, yield_look, HEAD_NOTICE_YAW_DEG),
        (32, yield_look, HEAD_NOTICE_YAW_DEG),
    ]))
    if step_pose_samples is None:
        step_pose_samples, _ = sample_measured_in_place_step(rig, working_pose)
    step_pose_samples = [
        (int(frame), _compose_frame(pose, "step", int(frame), arm_pose_overrides))
        for frame, pose in step_pose_samples
    ]
    key_action(rig, "step", step_pose_samples)
    key_action(rig, "idle", frames("idle", [
        (1, {}), (9, idle_breath_in), (17, {}),
        (25, idle_breath_out), (32, {})
    ]))

    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 32
    scene.render.fps = int(round(float(fps)))
    rig.animation_data.action = None
    _restore_working_pose(rig, working_pose)
    bpy.context.view_layer.update()

    ranges = {}
    for name in CLIPS:
        action = bpy.data.actions.get(name)
        if action is None:
            raise RuntimeError(f"Shurale animation action {name!r} was not created")
        ranges[name] = tuple(float(value) for value in action.frame_range)
    return ranges
