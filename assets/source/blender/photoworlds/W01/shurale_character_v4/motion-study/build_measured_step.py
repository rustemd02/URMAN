"""Generate and render measured v6 Shurale motion source studies.

Run with the pinned Blender against the frozen v6 source-stage blend:

    blender --background <shurale_v1_source.blend> --python build_measured_step.py

This creates only motion-study renders and measured pose input beside this
script. It does not
save or modify the input blend or export a runtime asset.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True

import bpy
from mathutils import Vector


STUDY_DIR = Path(__file__).resolve().parent
SOURCE_DIR = STUDY_DIR.parent
V6_SOURCE_DIR = SOURCE_DIR.parent / "shurale_character_v6"
OUTPUT_DIR = STUDY_DIR / "v6-gesture-yield"
BLEND_PATH = V6_SOURCE_DIR / "export-v1/shurale_v1_source.blend"
MANIFEST_PATH = V6_SOURCE_DIR / "export-v1/manifest.json"
EXPECTED_BLEND_SHA256 = "0b12f771611833f1d4b7251839bbdfb3690dbb623668e01bbfe62dd09e9016ca"
EXPECTED_SOURCE_LOOK_SHA256 = "8c49565c415b416ca962259da7f8f43e617835ee746a91abade95fe2d56ef9f2"
BAG_TARGET_GODOT_ROOT_LOCAL_M = (-0.31, 1.28, -0.70)
SAMPLE_FRAMES = tuple(range(1, 33))
RENDER_FRAMES = (1, 5, 9, 13, 17, 21, 25, 29, 32)


def load_export_helpers():
    if str(SOURCE_DIR) not in sys.path:
        sys.path.insert(0, str(SOURCE_DIR))
    path = SOURCE_DIR / "export-package-v1.py"
    spec = importlib.util.spec_from_file_location("shurale_export_helpers", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load existing exporter helpers from {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def world_point(rig, pose_point):
    return rig.matrix_world @ pose_point


def contact_measurements(rig, root, body, frame, support_side):
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()

    pose_foot = rig.pose.bones[f"foot_{support_side}"]
    endpoint_world = world_point(rig, pose_foot.tail)
    endpoint_root = root.matrix_world.inverted() @ endpoint_world

    body_eval = body.evaluated_get(depsgraph)
    evaluated_mesh = body_eval.to_mesh()
    try:
        original_vertices = body.data.vertices
        if len(evaluated_mesh.vertices) != len(original_vertices):
            raise RuntimeError(
                "The source mesh changed vertex indexing during evaluated pose; "
                "cannot report foot contact from its authored groups"
            )
        foot_bones = {
            name
            for name in rig.pose.bones.keys()
            if name.endswith(f"_{support_side}")
            and (name.startswith("foot_") or name.startswith("toe_"))
        }
        group_ids = {
            group.index
            for group in body.vertex_groups
            if group.name in foot_bones
        }
        if not group_ids:
            raise RuntimeError(f"No authored {support_side}-foot/toe vertex groups exist on {body.name}")

        points = []
        for vertex in original_vertices:
            foot_weight = sum(
                assignment.weight
                for assignment in vertex.groups
                if assignment.group in group_ids
            )
            if foot_weight < 0.15:
                continue
            point_world = body_eval.matrix_world @ evaluated_mesh.vertices[vertex.index].co
            points.append((point_world, foot_weight))
        if not points:
            raise RuntimeError(f"No evaluated {support_side}-foot surface points were found")

        min_z = min(float(point.z) for point, _ in points)
        contact_points = [point for point, _ in points if point.z <= min_z + 0.004]
        contact_center_world = sum(contact_points, Vector()) / len(contact_points)
        contact_center_root = root.matrix_world.inverted() @ contact_center_world
        return {
            "sole_min_z_world_m": min_z,
            "sole_contact_center_world_m": tuple(float(v) for v in contact_center_world),
            "sole_contact_center_root_local_m": tuple(float(v) for v in contact_center_root),
            "foot_endpoint_root_local_m": tuple(float(v) for v in endpoint_root),
            "contact_vertex_count": len(contact_points),
        }
    finally:
        body_eval.to_mesh_clear()


def face_pose_measurement(rig, root, body, action_name, frame):
    """Measure the authored nose heading and head-up axis in root-local space."""
    scene = bpy.context.scene
    rig.animation_data.action = bpy.data.actions[action_name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()

    body_eval = body.evaluated_get(depsgraph)
    evaluated_mesh = body_eval.to_mesh()
    try:
        head_group = body.vertex_groups.get("head")
        if head_group is None:
            raise RuntimeError("The source LOD0 has no authored head vertex group")
        weighted_points = []
        for vertex in body.data.vertices:
            for assignment in vertex.groups:
                if assignment.group == head_group.index and assignment.weight > 0.05:
                    weighted_points.append((vertex.index, assignment.weight))
                    break
        if not weighted_points:
            raise RuntimeError("The source LOD0 head group has no weighted vertices")
        centroid = Vector((0.0, 0.0, 0.0))
        total_weight = 0.0
        for vertex_index, weight in weighted_points:
            centroid += evaluated_mesh.vertices[vertex_index].co * weight
            total_weight += weight
        centroid /= total_weight
        nose = evaluated_mesh.vertices[5134].co
        face_world = body_eval.matrix_world.to_3x3() @ (nose - centroid)
        face_root = root.matrix_world.inverted().to_3x3() @ face_world
        heading = math.degrees(math.atan2(-face_root.x, face_root.y))

        head = rig.pose.bones["head"]
        head_matrix_root = root.matrix_world.inverted() @ rig.matrix_world @ head.matrix
        anatomical_up = head_matrix_root.to_3x3().col[1].normalized()
        return {
            "heading_degrees": float(heading),
            "head_up_dot_root_up": float(anatomical_up.z),
        }
    finally:
        body_eval.to_mesh_clear()


def aim_camera(camera, location, target):
    camera.location = location
    camera.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()


def render_full_figure(
    rig,
    scene,
    view_frames,
    action_name="step",
    filename_prefix="step",
    ortho_scale=3.15,
    focus_head=False,
):
    original_camera = scene.camera
    camera_data = bpy.data.cameras.new("ShuraleStepStudyFullCameraData")
    full_camera = bpy.data.objects.new("ShuraleStepStudyFullCamera", camera_data)
    scene.collection.objects.link(full_camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ortho_scale
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.camera = full_camera
        for frame in view_frames:
            rig.animation_data.action = bpy.data.actions[action_name]
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            if focus_head:
                head = rig.pose.bones["head"]
                head_center = rig.matrix_world @ ((head.head + head.tail) * 0.5)
                camera_target = Vector(head_center)
                camera_location = camera_target + Vector((0.0, 7.0, 0.0))
            else:
                camera_target = Vector((0.0, 0.0, 1.25))
                camera_location = Vector((0.0, 7.0, 1.25))
            aim_camera(full_camera, camera_location, camera_target)
            bpy.context.view_layer.update()
            suffix = "head" if focus_head else "full"
            scene.render.filepath = str(
                OUTPUT_DIR / f"{filename_prefix}-frame-{frame:02d}-{suffix}.png"
            )
            bpy.ops.render.render(write_still=True)
    finally:
        scene.camera = original_camera
        bpy.data.objects.remove(full_camera, do_unlink=True)
        if bpy.data.cameras.get(camera_data.name) is camera_data:
            bpy.data.cameras.remove(camera_data)


def render_contact_views(rig, scene, view_frames):
    original_camera = scene.camera
    original_resolution = (scene.render.resolution_x, scene.render.resolution_y)
    original_percentage = scene.render.resolution_percentage
    original_file_format = scene.render.image_settings.file_format
    camera_data = bpy.data.cameras.new("ShuraleStepStudyContactCameraData")
    contact_camera = bpy.data.objects.new("ShuraleStepStudyContactCamera", camera_data)
    scene.collection.objects.link(contact_camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.35

    try:
        scene.render.resolution_x = 900
        scene.render.resolution_y = 1200
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"
        for frame in view_frames:
            rig.animation_data.action = bpy.data.actions["step"]
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            for view, location, target in (
                ("front", Vector((0.0, -3.8, 0.78)), Vector((0.0, 0.0, 0.32))),
                ("side", Vector((2.7, 0.0, 0.78)), Vector((0.0, 0.0, 0.32))),
            ):
                aim_camera(contact_camera, location, target)
                scene.camera = contact_camera
                bpy.context.view_layer.update()
                scene.render.filepath = str(OUTPUT_DIR / f"step-frame-{frame:02d}-feet-{view}.png")
                bpy.ops.render.render(write_still=True)
    finally:
        scene.camera = original_camera
        scene.render.resolution_x, scene.render.resolution_y = original_resolution
        scene.render.resolution_percentage = original_percentage
        scene.render.image_settings.file_format = original_file_format
        bpy.data.objects.remove(contact_camera, do_unlink=True)
        if bpy.data.cameras.get(camera_data.name) is camera_data:
            bpy.data.cameras.remove(camera_data)


def render_hand_views(rig, scene, working_hand_target_root_local_m):
    """Render hand-scale work/gesture/yield pairs from one fixed root-local view."""
    original_camera = scene.camera
    original_resolution = (scene.render.resolution_x, scene.render.resolution_y)
    original_percentage = scene.render.resolution_percentage
    original_file_format = scene.render.image_settings.file_format
    camera_data = bpy.data.cameras.new("ShuraleV6HandStudyCameraData")
    camera = bpy.data.objects.new("ShuraleV6HandStudyCamera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.78
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.camera = camera
        views = (
            ("front", Vector((0.0, 2.2, 0.18))),
            ("three-quarter", Vector((1.2, 1.8, 0.42))),
        )
        samples = (
            ("work", 15),
            ("gesture", 8),
            ("gesture", 15),
            ("yield", 1),
            ("yield", 8),
            ("yield", 16),
        )
        for action_name, frame in samples:
            rig.animation_data.action = bpy.data.actions[action_name]
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            target = rig.parent.matrix_world @ Vector(working_hand_target_root_local_m)
            for view_name, offset in views:
                aim_camera(camera, target + offset, target)
                bpy.context.view_layer.update()
                scene.render.filepath = str(
                    OUTPUT_DIR / f"{action_name}-frame-{frame:02d}-hand-{view_name}.png"
                )
                bpy.ops.render.render(write_still=True)
    finally:
        scene.camera = original_camera
        scene.render.resolution_x, scene.render.resolution_y = original_resolution
        scene.render.resolution_percentage = original_percentage
        scene.render.image_settings.file_format = original_file_format
        bpy.data.objects.remove(camera, do_unlink=True)
        if bpy.data.cameras.get(camera_data.name) is camera_data:
            bpy.data.cameras.remove(camera_data)


def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    if not BLEND_PATH.is_file() or not MANIFEST_PATH.is_file():
        raise RuntimeError("Frozen v6 source blend or exporter manifest is missing")
    digest = hashlib.sha256(BLEND_PATH.read_bytes()).hexdigest()
    if digest != EXPECTED_BLEND_SHA256:
        raise RuntimeError(f"Frozen v6 source blend changed: expected {EXPECTED_BLEND_SHA256}, got {digest}")
    loaded = Path(bpy.data.filepath).resolve()
    if loaded != BLEND_PATH.resolve():
        raise RuntimeError(f"Load the frozen source blend first; Blender opened {loaded}")

    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    working_pose = manifest["verification"]["working_pose_channels"]
    root = bpy.data.objects.get("ShuraleRoot")
    rig = bpy.data.objects.get("ShuraleRig")
    body = bpy.data.objects.get("ShuraleBody_LOD0")
    if root is None or rig is None or body is None:
        raise RuntimeError("Frozen v6 source blend is missing ShuraleRoot, ShuraleRig, or LOD0")
    source_geometry_hash = manifest["source"].get("base_geometry_blend_sha256")
    if source_geometry_hash != EXPECTED_SOURCE_LOOK_SHA256:
        raise RuntimeError(
            f"Frozen v6 manifest geometry input changed: expected {EXPECTED_SOURCE_LOOK_SHA256}, got {source_geometry_hash}"
        )

    exporter = load_export_helpers()
    import shurale_motion_direction as motion

    if rig.animation_data:
        rig.animation_data.action = None
        for track in rig.animation_data.nla_tracks:
            track.mute = True
        for driver in rig.animation_data.drivers:
            driver.mute = True

    step_frames, measurements = motion.sample_measured_in_place_step(rig, working_pose)
    if tuple(frame for frame, _ in step_frames) != SAMPLE_FRAMES:
        raise RuntimeError(f"Unexpected measured step sample frames: {[frame for frame, _ in step_frames]}")
    for frame in (1, 13, 21, 32):
        sample_pose = dict(step_frames[frame - 1][1])
        print(
            f"  baked sample f{frame:02d}: "
            f"thigh_r={sample_pose['thigh_r']['rotation']} "
            f"calf_r={sample_pose['calf_r']['rotation']} "
            f"foot_r={sample_pose['foot_r']['rotation']}"
        )
    ik_targets = manifest["verification"]["working_ik_targets_root_local_m"]
    target_pose = ik_targets["targets"]
    pole_pose = ik_targets["poles"]
    bag_godot = Vector(BAG_TARGET_GODOT_ROOT_LOCAL_M)
    # Blender Z-up to Godot Y-up maps root-local (X,Y,Z)_B to (X,Z,-Y)_G.
    bag_root_blender = Vector((bag_godot.x, -bag_godot.z, bag_godot.y))
    arm_pose_overrides, arm_measurements = motion.sample_measured_gesture_yield_overrides(
        rig,
        root,
        body,
        working_pose,
        bag_root_blender,
        Vector(target_pose["r"]),
        Vector(pole_pose["r"]),
    )
    override_payload = {
        "format": "urman.shurale-arm-pose-overrides.v1",
        "source_blend": str(BLEND_PATH),
        "source_blend_sha256": digest,
        "source_look_sha256": EXPECTED_SOURCE_LOOK_SHA256,
        "bag_target_godot_root_local_m": list(BAG_TARGET_GODOT_ROOT_LOCAL_M),
        "bag_target_blender_root_local_m": [float(value) for value in bag_root_blender],
        "clips": arm_pose_overrides,
        "measurements": arm_measurements,
        "consumer": "motion.create_shurale_animations(..., arm_pose_overrides=json['clips'])",
    }
    (OUTPUT_DIR / "arm-pose-overrides.json").write_text(
        json.dumps(override_payload, ensure_ascii=False, indent=2, default=lambda value: list(value)),
        encoding="utf-8",
    )
    # This frozen source-stage blend already contains earlier action names.
    # The shared exporter helper creates new Blender Actions, which would get
    # suffixed names and leave lookups such as actions['step'] bound to stale
    # data unless this disposable study copy first clears those names.
    for clip_name in motion.CLIPS:
        old_clip = bpy.data.actions.get(clip_name)
        if old_clip is not None:
            bpy.data.actions.remove(old_clip)
    clip_ranges = motion.create_shurale_animations(
        rig,
        working_pose,
        exporter.key_action,
        exporter.deg_pose,
        exporter.FPS,
        arm_pose_overrides=arm_pose_overrides,
        step_pose_samples=step_frames,
    )

    scene = bpy.context.scene
    rig.animation_data.action = bpy.data.actions["step"]
    baseline = {
        side: contact_measurements(rig, root, body, 1, side)
        for side in ("l", "r")
    }
    all_contacts = {
        frame: {
            side: contact_measurements(rig, root, body, frame, side)
            for side in ("l", "r")
        }
        for frame in range(1, 33)
    }

    print("source mesh and generated step action bindings:")
    print(f"  body visibility render={body.hide_render} viewport={body.hide_get()}")
    print(
        "  armature modifiers="
        f"{[(modifier.name, modifier.object.name if modifier.object else None) for modifier in body.modifiers if modifier.type == 'ARMATURE']}"
    )
    print(
        "  leg vertex groups="
        f"{[group.name for group in body.vertex_groups if group.name.startswith(('foot_', 'toe_'))]}"
    )
    step_action = bpy.data.actions["step"]
    for frame in (1, 13, 21, 32):
        rig.animation_data.action = step_action
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        for side in ("l", "r"):
            foot = rig.pose.bones[f"foot_{side}"]
            endpoint = root.matrix_world.inverted() @ world_point(rig, foot.tail)
            print(
                f"  f{frame:02d} {side} foot-rz={tuple(round(float(v), 5) for v in foot.rotation_euler)} "
                f"tail={tuple(round(float(v), 5) for v in endpoint)}"
            )
    step_curves = [
        curve
        for curve in step_action.fcurves
        if any(name in curve.data_path for name in ('thigh_r', 'calf_r', 'foot_r'))
    ]
    print(
        f"  swing leg action curves={len(step_curves)} / total={len(step_action.fcurves)}; "
        f"paths={[curve.data_path for curve in step_action.fcurves[:12]]}"
    )

    render_full_figure(rig, scene, RENDER_FRAMES)
    render_full_figure(
        rig,
        scene,
        (1, 6, 12, 18, 32),
        action_name="noticed",
        filename_prefix="noticed",
    )
    render_full_figure(
        rig,
        scene,
        (12,),
        action_name="noticed",
        filename_prefix="noticed-close",
        ortho_scale=1.05,
        focus_head=True,
    )
    render_full_figure(
        rig,
        scene,
        (1, 8, 15, 30),
        action_name="gesture",
        filename_prefix="gesture",
    )
    render_full_figure(
        rig,
        scene,
        (15,),
        action_name="speak",
        filename_prefix="speak",
    )
    render_full_figure(
        rig,
        scene,
        (1, 8, 16, 32),
        action_name="yield",
        filename_prefix="yield",
    )
    render_full_figure(
        rig,
        scene,
        (15,),
        action_name="work",
        filename_prefix="work-return",
    )
    render_hand_views(rig, scene, target_pose["r"])
    render_contact_views(rig, scene, (1, 9, 13, 17, 21, 29, 32))

    print(f"source blend sha256: {digest}")
    print(f"clip ranges: {clip_ranges}")
    for action_name, frame in (("noticed", 1), ("noticed", 12), ("noticed", 32), ("gesture", 15), ("speak", 15), ("yield", 16), ("work", 15)):
        measurement = face_pose_measurement(rig, root, body, action_name, frame)
        print(
            f"  {action_name} f{frame:02d}: face heading={measurement['heading_degrees']:.4f} degrees, "
            f"head-up dot root-up={measurement['head_up_dot_root_up']:.5f}"
        )
    print(f"measured root-local noticed yaw delta: {motion.HEAD_NOTICE_YAW_DEG:.4f} degrees")
    print("measured right-arm gesture/yield source: ")
    print(json.dumps(arm_measurements, ensure_ascii=False, indent=2, default=lambda value: list(value)))
    print(f"measured arm pose override handoff: {OUTPUT_DIR / 'arm-pose-overrides.json'}")
    print(f"step/support: {measurements['step_side']} / {measurements['support_side']}")
    print(
        "measured leg/step/lift (m): "
        f"{measurements['leg_length_m']:.6f} / "
        f"{measurements['step_distance_m']:.6f} / {measurements['lift_m']:.6f}"
    )
    print(f"measured pole angles (rad): {measurements['pole_angles_rad']}")
    print(f"baseline source-pose IK errors (m): {measurements['baseline_pose_errors_m']}")
    print("measured target vs evaluated foot endpoint (root-local m):")
    for sample in measurements["samples"]:
        print(
            f"  f{sample['frame']:02d}: target={sample['step_target_root_local_m']} "
            f"actual={sample['step_foot_endpoint_root_local_m']} "
            f"support-end-error={sample['support_foot_endpoint_error_m']:.6f} m "
            f"swing-target-error={sample['step_foot_endpoint_target_error_m']:.6f} m"
        )
    print("evaluated support and swing sole contact (root-local/world):")
    support = measurements["support_side"]
    swing = measurements["step_side"]
    for frame in RENDER_FRAMES:
        values = all_contacts[frame]
        support_stats = values[support]
        swing_stats = values[swing]
        baseline_support = baseline[support]
        baseline_swing = baseline[swing]
        support_delta = Vector(support_stats["sole_contact_center_root_local_m"]) - Vector(
            baseline_support["sole_contact_center_root_local_m"]
        )
        swing_delta = Vector(swing_stats["sole_contact_center_root_local_m"]) - Vector(
            baseline_swing["sole_contact_center_root_local_m"]
        )
        print(
            f"  f{frame:02d}: support minZ={support_stats['sole_min_z_world_m']:.6f} m "
            f"dXY={support_delta.x:.6f},{support_delta.y:.6f} m; "
            f"swing minZ={swing_stats['sole_min_z_world_m']:.6f} m "
            f"dXYZ={swing_delta.x:.6f},{swing_delta.y:.6f},{swing_delta.z:.6f} m"
        )
    max_support_xy = 0.0
    max_support_z = 0.0
    for values in all_contacts.values():
        stats = values[support]
        base = baseline[support]
        delta = Vector(stats["sole_contact_center_root_local_m"]) - Vector(
            base["sole_contact_center_root_local_m"]
        )
        max_support_xy = max(max_support_xy, math.hypot(delta.x, delta.y))
        max_support_z = max(max_support_z, abs(stats["sole_min_z_world_m"] - base["sole_min_z_world_m"]))
    print(f"all-frame support sole maximum XY drift: {max_support_xy:.6f} m")
    print(f"all-frame support sole maximum height drift: {max_support_z:.6f} m")
    print(f"renders: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
