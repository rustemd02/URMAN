#!/usr/bin/env python3
"""Package the reviewed Shurale material/rig study as a source-stage GLB."""

from __future__ import annotations

import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[5]
INPUT_BLEND = SCRIPT_DIR / "material-v2/shurale-character-look-v4-material-v1.blend"
OUTPUT_DIR = SCRIPT_DIR / "export-v1"
GLB_PATH = OUTPUT_DIR / "shurale_v1_candidate.glb"
BLEND_PATH = OUTPUT_DIR / "shurale_v1_source.blend"
ALBEDO_NAME = "Shurale_Body_Albedo_2K"
MASK_NAME = "Shurale_Body_AmbientOcclusion_Mask_1K"
PROFILE_NAME = "v4"
SOURCE_ROOT_NAME = "ShuraleCharacterRootV4"
SOURCE_RIG_NAME = "ShuraleCharacterRigV4"
SOURCE_BODY_NAME = "ShuraleCharacterBodyV4"
CAMERA_NAME = "ShuraleCharacterV4Camera"
V6_DIR = SCRIPT_DIR.parent / "shurale_character_v6"
V6_BASE_BLEND = V6_DIR / "shurale-character-look-v6.blend"
V6_SURFACE_BLEND = SCRIPT_DIR.parent / "shurale_surface_v1/shurale-surface-v3.blend"
V6_SURFACE_MANIFEST = SCRIPT_DIR.parent / "shurale_surface_v1/manifest.json"
V6_OUTPUT_DIR = V6_DIR / "export-v1"
V6_BASE_BLEND_SHA256 = "8c49565c415b416ca962259da7f8f43e617835ee746a91abade95fe2d56ef9f2"
V6_SURFACE_BLEND_SHA256 = "258f0cdb64d6d47f12cf25e56c4d7b781afc63110a2dd537d40a3222dcaaf335"
V6_OVERRIDE_SOURCE_BLEND_SHA256 = "0b12f771611833f1d4b7251839bbdfb3690dbb623668e01bbfe62dd09e9016ca"
V6_OVERRIDE_SOURCE_BLEND_ARCHIVE = V6_OUTPUT_DIR / "history/59f31b23/shurale_v1_source.blend"
V6_ARM_POSE_OVERRIDES = SCRIPT_DIR / "motion-study/v6-gesture-yield/arm-pose-overrides.json"
V6_ARM_POSE_OVERRIDES_SHA256 = "c68001cdf831f664add5b741482c17362f8084c86f40eec8a4f18ab2260787d8"
V6_ALBEDO_NAME = "shurale-surface-v3-albedo-2k.png"
V6_NORMAL_NAME = "shurale-surface-v3-normal-tangent-2k.png"
V6_AO_NAME = "shurale-surface-v3-ao-1k.png"
EXPECTED_CLIPS = ("work", "noticed", "gesture", "speak", "yield", "step", "idle")
TARGET_LOD0_TRIANGLES = 27000
ROOT_NAME = "ShuraleRoot"
RIG_NAME = "ShuraleRig"
LOD0_NAME = "ShuraleBody_LOD0"
LOD1_NAME = "ShuraleBody_LOD1"
BRANCH_NAME = "ShuraleWorkBranch"
FPS = 24
REVIEW_CAMERAS = {}

if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))
from shurale_motion_direction import create_shurale_animations, sample_measured_in_place_step

# Root-local Blender metres after normalize_root() has applied the facing fix.
# The hands stay at this waist-height baseline while the body proportions and
# actual F14 support reach are being resolved independently.
WORK_IK_TARGETS_ROOT_LOCAL_M = {
    "l": (-0.30, 0.43, 1.35),
    "r": (0.25, 0.444110, 1.392320),
}
WORK_IK_POLES_ROOT_LOCAL_M = {
    "l": (-0.48, 0.70, 1.12),
    "r": (0.48, 0.70, 1.12),
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_v6_arm_pose_overrides() -> tuple[dict, dict]:
    if not V6_ARM_POSE_OVERRIDES.is_file():
        raise RuntimeError(f"Missing measured v6 arm-pose overrides: {V6_ARM_POSE_OVERRIDES}")
    override_sha = sha256(V6_ARM_POSE_OVERRIDES)
    if override_sha != V6_ARM_POSE_OVERRIDES_SHA256:
        raise RuntimeError(f"Measured v6 arm-pose override SHA changed: {override_sha}")
    source_archive = V6_OVERRIDE_SOURCE_BLEND_ARCHIVE
    if not source_archive.is_file() or sha256(source_archive) != V6_OVERRIDE_SOURCE_BLEND_SHA256:
        raise RuntimeError("The immutable source blend used to measure v6 gesture/yield poses is missing or changed")

    metadata = json.loads(V6_ARM_POSE_OVERRIDES.read_text(encoding="utf-8"))
    expected_source_path = (V6_OUTPUT_DIR / "shurale_v1_source.blend").resolve()
    if metadata.get("format") != "urman.shurale-arm-pose-overrides.v1":
        raise RuntimeError(f"Unknown v6 arm-pose override format: {metadata.get('format')!r}")
    if Path(metadata.get("source_blend", "")).resolve() != expected_source_path:
        raise RuntimeError("Measured v6 arm-pose overrides refer to a different source blend path")
    if metadata.get("source_blend_sha256") != V6_OVERRIDE_SOURCE_BLEND_SHA256:
        raise RuntimeError("Measured v6 arm-pose overrides refer to a different source blend SHA")
    if metadata.get("source_look_sha256") != V6_BASE_BLEND_SHA256:
        raise RuntimeError("Measured v6 arm-pose overrides refer to a different v6 body/rig input")
    raw_clips = metadata.get("clips")
    if not isinstance(raw_clips, dict) or set(raw_clips) != {"gesture", "yield"}:
        raise RuntimeError("Measured v6 arm-pose overrides must contain only gesture and yield")
    clips = {
        clip_name: {int(frame): pose for frame, pose in frames.items()}
        for clip_name, frames in raw_clips.items()
    }
    return clips, {
        "file": V6_ARM_POSE_OVERRIDES.relative_to(PROJECT_ROOT).as_posix(),
        "sha256": override_sha,
        "format": metadata["format"],
        "source_blend_sha256": metadata["source_blend_sha256"],
        "source_blend_archive": source_archive.relative_to(PROJECT_ROOT).as_posix(),
        "source_look_sha256": metadata["source_look_sha256"],
        "clip_frames": {name: sorted(samples) for name, samples in clips.items()},
        "measurements": metadata.get("measurements", {}),
    }


def triangulated_count(mesh) -> int:
    return sum(max(1, len(poly.vertices) - 2) for poly in mesh.polygons)


def evaluated_triangle_count(obj) -> int:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return triangulated_count(mesh)
    finally:
        evaluated.to_mesh_clear()


def apply_modifier(obj, modifier_name: str) -> None:
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if modifier_name not in obj.modifiers:
        raise RuntimeError(f"Missing modifier {modifier_name!r} on {obj.name}")
    bpy.ops.object.modifier_apply(modifier=modifier_name)
    obj.select_set(False)


def ensure_object_mode() -> None:
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


def make_gltf_materials(body) -> None:
    if PROFILE_NAME == "v6":
        expected_images = {
            V6_ALBEDO_NAME: "sRGB",
            V6_NORMAL_NAME: "Non-Color",
            V6_AO_NAME: "Non-Color",
        }
        for image_name, color_space in expected_images.items():
            image = bpy.data.images.get(image_name)
            expected_dimensions = (1024, 1024) if image_name == V6_AO_NAME else (2048, 2048)
            if image is None or image.packed_file is None or tuple(image.size) != expected_dimensions:
                raise RuntimeError(f"Missing packed v6 surface image {image_name!r} at {expected_dimensions}")
            image.colorspace_settings.name = color_space
        for material in body.data.materials:
            if not material or not material.use_nodes:
                continue
            nodes = material.node_tree.nodes
            principled = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
            if principled is None:
                raise RuntimeError(f"Missing Principled shader in v6 material {material.name!r}")
            principled.inputs["Alpha"].default_value = 1.0
            emission = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
            if emission is not None:
                emission.default_value = (0.0, 0.0, 0.0, 1.0)
            if "eyes" in material.name.lower():
                principled.inputs["Roughness"].default_value = 0.1
                continue

            albedo = next((node for node in nodes if node.type == "TEX_IMAGE" and node.image and node.image.name == V6_ALBEDO_NAME), None)
            normal = next((node for node in nodes if node.type == "TEX_IMAGE" and node.image and node.image.name == V6_NORMAL_NAME), None)
            ao = next((node for node in nodes if node.type == "TEX_IMAGE" and node.image and node.image.name == V6_AO_NAME), None)
            normal_map = next((node for node in nodes if node.type == "NORMAL_MAP"), None)
            occlusion_group = next((node for node in nodes if node.type == "GROUP" and "Occlusion" in node.name), None)
            links = material.node_tree.links
            required_links = (
                (albedo, principled, "Base Color"),
                (normal, normal_map, "Color"),
                (normal_map, principled, "Normal"),
                (ao, occlusion_group, "Occlusion"),
            )
            if any(source is None or target is None for source, target, _ in required_links):
                raise RuntimeError(f"v6 PBR image path is incomplete in {material.name!r}")
            for source, target, target_socket in required_links:
                if not any(link.from_node == source and link.to_node == target and link.to_socket.name == target_socket
                           for link in links):
                    raise RuntimeError(f"v6 texture link {source.name!r} -> {target.name}.{target_socket} is missing")
        return

    for material in body.data.materials:
        if not material or not material.use_nodes:
            continue
        nodes = material.node_tree.nodes
        principled = next((node for node in nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            continue
        principled.inputs["Alpha"].default_value = 1.0
        if material.name.endswith("eyes") or "eyes" in material.name.lower():
            emission = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
            if emission is not None:
                emission.default_value = (0.0, 0.0, 0.0, 1.0)
            principled.inputs["Roughness"].default_value = 0.1
            continue

        albedo_node = next((node for node in nodes if node.type == "TEX_IMAGE" and node.image and node.image.name == ALBEDO_NAME), None)
        mask_node = next((node for node in nodes if node.type == "TEX_IMAGE" and node.image and node.image.name == MASK_NAME), None)
        if albedo_node is None or mask_node is None:
            raise RuntimeError(f"Expected packed 2K albedo and 1K mask in material {material.name}")
        albedo_node.image.colorspace_settings.name = "sRGB"
        mask_node.image.colorspace_settings.name = "Non-Color"
        for link in list(material.node_tree.links):
            if link.to_node == principled and link.to_socket.name in {"Base Color", "Roughness"}:
                material.node_tree.links.remove(link)
        material.node_tree.links.new(albedo_node.outputs["Color"], principled.inputs["Base Color"])
        # The grayscale source mask carries the already-authored subtle surface
        # variation. A direct PBR roughness link survives glTF without MapRange.
        material.node_tree.links.new(mask_node.outputs["Color"], principled.inputs["Roughness"])
        for node in list(nodes):
            if node.type == "MAP_RANGE" and not any(link.from_node == node or link.to_node == node for link in material.node_tree.links):
                nodes.remove(node)


def bake_working_pose(rig):
    scene = bpy.context.scene
    rig.animation_data_create()
    rig.animation_data.action = None
    scene.frame_set(1)
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_rig = rig.evaluated_get(depsgraph)
    target_matrices = {bone.name: evaluated_rig.pose.bones[bone.name].matrix.copy() for bone in rig.pose.bones}

    ensure_object_mode()
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.nla.bake(
        frame_start=1,
        frame_end=1,
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
    baked_action = rig.animation_data.action
    if baked_action is None or not baked_action.fcurves:
        raise RuntimeError("Visual IK bake did not produce keyed pose channels")

    for pose_bone in rig.pose.bones:
        for constraint in list(pose_bone.constraints):
            pose_bone.constraints.remove(constraint)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    depsgraph.update()
    evaluated_rig = rig.evaluated_get(depsgraph)
    matrix_error = max(
        abs(evaluated_rig.pose.bones[name].matrix[row][col] - matrix[row][col])
        for name, matrix in target_matrices.items()
        for row in range(4)
        for col in range(4)
    )
    if matrix_error > 0.002:
        raise RuntimeError(f"Baked action does not reproduce the IK pose after constraint removal; max matrix error={matrix_error:.6f}")

    values = {}
    for bone in rig.pose.bones:
        rotation = tuple(float(component) for component in bone.rotation_euler)
        location = tuple(float(component) for component in bone.location)
        scale = tuple(float(component) for component in bone.scale)
        if (max(abs(v) for v in rotation) > 1e-6 or max(abs(v) for v in location) > 1e-6
                or max(abs(v - 1.0) for v in scale) > 1e-6):
            values[bone.name] = {"rotation": rotation, "location": location, "scale": scale}
    bpy.data.actions.remove(baked_action)
    rig.animation_data.action = None
    for helper in list(bpy.context.scene.objects):
        if helper.name.startswith("Shurale_PoseIK_Target_") or helper.name.startswith("Shurale_PoseIK_Pole_"):
            bpy.data.objects.remove(helper, do_unlink=True)
    if "Root" in values and any(abs(v) > 1e-6 for v in values["Root"]["location"]):
        raise RuntimeError("Working pose contains root-bone translation")
    return values, matrix_error


def reset_pose(rig) -> None:
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def apply_pose_values(rig, pose_values) -> None:
    reset_pose(rig)
    for bone_name, values in pose_values.items():
        bone = rig.pose.bones[bone_name]
        bone.rotation_euler = values["rotation"]
        bone.location = values["location"]
        bone.scale = values["scale"]
    bpy.context.view_layer.update()


def find_branch_hand_contact(body, branch, group_name):
    """Return a measured branch-vertex/skin-surface pair for one authored grip."""
    group = body.vertex_groups.get(group_name)
    if group is None:
        raise RuntimeError(f"Missing reviewed branch-contact vertex group {group_name!r}")

    mask_modifiers = [modifier for modifier in body.modifiers if modifier.type == "MASK"]
    mask_states = [(modifier, modifier.show_viewport) for modifier in mask_modifiers]
    for modifier in mask_modifiers:
        # The export mask removes unrelated helper faces, changing vertex
        # indices. Contact is measured on the same armature-deformed skin but
        # without that topology-changing mask; only weighted finger faces enter
        # the BVH below. The source setting is restored before leaving.
        modifier.show_viewport = False
    evaluated_body = None
    evaluated_branch = None
    body_mesh = None
    branch_mesh = None
    try:
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated_body = body.evaluated_get(depsgraph)
        body_mesh = evaluated_body.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
        evaluated_branch = branch.evaluated_get(depsgraph)
        branch_mesh = evaluated_branch.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
        if len(body_mesh.vertices) != len(body.data.vertices):
            raise RuntimeError(
                f"Evaluated body topology changed while measuring {group_name}: "
                f"{len(body.data.vertices)} source vs {len(body_mesh.vertices)} evaluated vertices"
            )
        selected = {
            vertex.index
            for vertex in body.data.vertices
            if any(item.group == group.index and item.weight >= 0.02 for item in vertex.groups)
        }
        polygons = [
            list(polygon.vertices)
            for polygon in body_mesh.polygons
            if sum(index in selected for index in polygon.vertices) >= 2
        ]
        if not polygons:
            raise RuntimeError(f"No evaluated hand surface faces matched {group_name!r}")

        body_points = [evaluated_body.matrix_world @ vertex.co for vertex in body_mesh.vertices]
        hand_tree = BVHTree.FromPolygons(body_points, polygons, all_triangles=False)
        branch_points = [evaluated_branch.matrix_world @ vertex.co for vertex in branch_mesh.vertices]
        best = None
        for point in branch_points:
            nearest = hand_tree.find_nearest(point)
            if nearest is not None and (best is None or nearest[3] < best["distance_m"]):
                best = {
                    "branch_point": point.copy(),
                    "hand_point": nearest[0].copy(),
                    "distance_m": float(nearest[3]),
                }
        if best is None:
            raise RuntimeError(f"No branch/skin closest-point pair found for {group_name!r}")
        return best
    finally:
        if evaluated_body is not None and body_mesh is not None:
            evaluated_body.to_mesh_clear()
        if evaluated_branch is not None and branch_mesh is not None:
            evaluated_branch.to_mesh_clear()
        for modifier, show_viewport in mask_states:
            modifier.show_viewport = show_viewport
        bpy.context.view_layer.update()


def align_branch_to_working_hands(body, branch, root):
    """Anchor the bough at the reviewed left grip, then solve the right IK target."""
    contact_groups = ("index_01_l", "thumb_02_r")
    before = {name: find_branch_hand_contact(body, branch, name) for name in contact_groups}
    left = before[contact_groups[0]]
    right = before[contact_groups[1]]
    branch_translation = left["hand_point"] - left["branch_point"]
    branch.matrix_world = Matrix.Translation(branch_translation) @ branch.matrix_world
    bpy.context.view_layer.update()

    right_branch_point = right["branch_point"] + branch_translation
    target_helper = bpy.data.objects.get("Shurale_PoseIK_Target_r")
    if target_helper is None:
        raise RuntimeError("Missing right arm IK target needed for branch contact solve")
    # Preserve the reviewed world-space offset from the right IK target to its
    # actual thumb skin, then place the target at the authored branch shoot.
    right_target_world = target_helper.matrix_world.translation.copy()
    right_thumb_offset = right["hand_point"] - right_target_world
    root_inverse = root.matrix_world.inverted()
    target_helper.location = root_inverse @ (right_branch_point - right_thumb_offset)
    bpy.context.view_layer.update()

    correction_count = 0
    for _ in range(6):
        current = find_branch_hand_contact(body, branch, contact_groups[1])
        if current["distance_m"] <= 0.002:
            break
        # Move the wrist target by the measured current skin-to-branch error;
        # no bone Euler direction or branch stretch is guessed.
        target_delta_world = current["branch_point"] - current["hand_point"]
        target_helper.location += root_inverse.to_3x3() @ target_delta_world
        correction_count += 1
        bpy.context.view_layer.update()
    after = {name: find_branch_hand_contact(body, branch, name) for name in contact_groups}
    max_contact_distance = max(value["distance_m"] for value in after.values())
    if max_contact_distance > 0.002:
        raise RuntimeError(
            "Measured waist-height branch contact failed after IK target solve: "
            f"max skin gap={max_contact_distance:.6f} m after {correction_count} corrections"
        )
    return {
        "left_contact_group": contact_groups[0],
        "right_contact_group": contact_groups[1],
        "left_gap_before_m": before[contact_groups[0]]["distance_m"],
        "right_gap_before_m": before[contact_groups[1]]["distance_m"],
        "left_gap_after_m": after[contact_groups[0]]["distance_m"],
        "right_gap_after_m": after[contact_groups[1]]["distance_m"],
        "branch_translation_m": [float(value) for value in branch_translation],
        "right_ik_target_initial_root_local_m": [float(value) for value in root_inverse @ right_target_world],
        "right_ik_target_solved_root_local_m": [float(value) for value in target_helper.location],
        "left_ik_target_root_local_m": [float(value) for value in bpy.data.objects["Shurale_PoseIK_Target_l"].location],
        "right_thumb_surface_offset_initial_m": [float(value) for value in right_thumb_offset],
        "right_ik_corrections": correction_count,
        "method": "Rigid translation maps authored branch to evaluated left index skin contact; right IK target is initialized from the measured thumb-surface offset and corrected by measured evaluated skin/branch error.",
    }


def key_action(rig, name: str, frames: list[tuple[int, dict]]) -> None:
    action = bpy.data.actions.new(name=name)
    action.use_fake_user = True
    rig.animation_data.action = action
    animated_bones = sorted({bone_name for _, pose in frames for bone_name in pose})
    for bone_name in animated_bones:
        if bone_name not in rig.pose.bones:
            raise RuntimeError(f"Animation {name} references unavailable bone {bone_name}")
    for frame, pose in frames:
        reset_pose(rig)
        for bone_name, values in pose.items():
            bone = rig.pose.bones[bone_name]
            if "rotation" in values:
                bone.rotation_euler = values["rotation"]
            if "location" in values:
                bone.location = values["location"]
            if "scale" in values:
                bone.scale = values["scale"]
        for bone_name in animated_bones:
            bone = rig.pose.bones[bone_name]
            bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)
            bone.keyframe_insert(data_path="location", frame=frame, group=bone_name)
            bone.keyframe_insert(data_path="scale", frame=frame, group=bone_name)
    for fcurve in action.fcurves:
        for point in fcurve.keyframe_points:
            point.interpolation = "BEZIER"
            point.handle_left_type = "AUTO_CLAMPED"
            point.handle_right_type = "AUTO_CLAMPED"


def deg_pose(**items):
    return {name: {"rotation": tuple(math.radians(v) for v in value)} for name, value in items.items()}


def prune_weights(mesh_obj, bone_names) -> dict:
    assignments = {}
    bone_groups = {group.name: group for group in mesh_obj.vertex_groups if group.name in bone_names}
    missing_vertices = []
    influence_histogram = {}
    max_sum_error = 0.0
    for vertex in mesh_obj.data.vertices:
        candidates = [(mesh_obj.vertex_groups[item.group].name, float(item.weight))
                      for item in vertex.groups
                      if item.group < len(mesh_obj.vertex_groups)
                      and mesh_obj.vertex_groups[item.group].name in bone_names
                      and item.weight > 1e-7]
        candidates.sort(key=lambda item: (-item[1], item[0]))
        chosen = candidates[:4]
        total = sum(weight for _, weight in chosen)
        if total <= 1e-9:
            missing_vertices.append(vertex.index)
            continue
        influence_histogram[len(chosen)] = influence_histogram.get(len(chosen), 0) + 1
        normalized = [(name, weight / total) for name, weight in chosen]
        max_sum_error = max(max_sum_error, abs(sum(weight for _, weight in normalized) - 1.0))
        for name, weight in normalized:
            assignments.setdefault(name, []).append((vertex.index, weight))
    if missing_vertices:
        raise RuntimeError(f"Skinned output contains vertices without a rig-bone weight: {missing_vertices[:8]}")
    for group in list(mesh_obj.vertex_groups):
        mesh_obj.vertex_groups.remove(group)
    for name in sorted(assignments):
        group = mesh_obj.vertex_groups.new(name=name)
        for vertex_index, weight in assignments[name]:
            group.add([vertex_index], weight, "REPLACE")
    stored_histogram = {}
    stored_max_sum_error = 0.0
    stored_max_influences = 0
    for vertex in mesh_obj.data.vertices:
        weights = [float(item.weight) for item in vertex.groups if item.weight > 1e-7]
        stored_max_influences = max(stored_max_influences, len(weights))
        stored_histogram[len(weights)] = stored_histogram.get(len(weights), 0) + 1
        stored_max_sum_error = max(stored_max_sum_error, abs(sum(weights) - 1.0))
    max_influences = max(influence_histogram, default=0)
    max_sum_error = max(max_sum_error, stored_max_sum_error)
    if max_influences > 4 or max_sum_error > 1e-5:
        raise RuntimeError(f"Weight prune failed: max influences={max_influences}, sum error={max_sum_error}")
    if stored_max_influences > 4 or stored_max_sum_error > 1e-5:
        raise RuntimeError(f"Stored skin weights failed validation: max influences={stored_max_influences}, sum error={stored_max_sum_error}")
    return {"max_influences": stored_max_influences, "histogram": stored_histogram,
            "normalized": True, "max_sum_error": max_sum_error,
            "used_bones": sorted(assignments)}


def new_lod_source(source_body, rig, collection, name):
    obj = source_body.copy()
    obj.data = source_body.data.copy()
    obj.name = name
    obj.data.name = f"{name}Mesh"
    collection.objects.link(obj)
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.hide_viewport = False
    obj.hide_render = False
    obj.hide_set(False)
    if len([modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]) != 1:
        raise RuntimeError("Expected one native armature modifier on the MPFB body")
    if len([modifier for modifier in obj.modifiers if modifier.type == "MASK"]) != 1:
        raise RuntimeError("Expected one helper-geometry mask on the MPFB body")
    # Blender does not apply arbitrary modifiers to meshes with shape keys.
    # Bake the currently evaluated source targets at the neutral rig pose into
    # a new, shape-key-free mesh while preserving UV/material/deform layers.
    depsgraph = bpy.context.evaluated_depsgraph_get()
    bpy.context.view_layer.update()
    evaluated = obj.evaluated_get(depsgraph)
    baked_mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    if baked_mesh is None or len(baked_mesh.vertices) == 0 or len(baked_mesh.polygons) == 0:
        raise RuntimeError(f"Evaluated shape-key/mask bake returned an empty mesh for {name}")
    previous_mesh = obj.data
    obj.data = baked_mesh
    baked_mesh.name = f"{name}Mesh"
    if previous_mesh.users == 0:
        bpy.data.meshes.remove(previous_mesh)
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    if obj.data.shape_keys is not None:
        raise RuntimeError(f"Evaluated mesh unexpectedly retained shape keys: {name}")
    if not obj.data.uv_layers:
        raise RuntimeError(f"Evaluated mesh lost the MPFB UV atlas: {name}")
    return obj


def make_lod0(source_body, rig, collection, bone_names):
    obj = new_lod_source(source_body, rig, collection, LOD0_NAME)
    initial_triangles = triangulated_count(obj.data)
    if 25000 <= initial_triangles <= 30000:
        # The evaluated source already meets the authored runtime budget; do
        # not decimate merely to reach an approximate midpoint.
        expected_triangles = initial_triangles
        chosen_ratio = None
    else:
        decimate = obj.modifiers.new("Shurale_LOD0_TargetBudget", "DECIMATE")
        decimate.decimate_type = "COLLAPSE"
        candidates = []
        for step in range(50, 101):
            decimate.ratio = step / 100.0
            bpy.context.view_layer.update()
            triangles = evaluated_triangle_count(obj)
            candidates.append((abs(triangles - TARGET_LOD0_TRIANGLES), triangles, decimate.ratio))
        _, expected_triangles, chosen_ratio = min(candidates)
    if not 25000 <= expected_triangles <= 30000:
        raise RuntimeError(f"Could not find LOD0 reduction near target; closest was {expected_triangles} triangles")
    if chosen_ratio is not None:
        decimate.ratio = chosen_ratio
        apply_modifier(obj, decimate.name)
    if triangulated_count(obj.data) != expected_triangles:
        raise RuntimeError("LOD0 decimation result differed from inspected evaluated result")
    prune_info = prune_weights(obj, bone_names)
    skin = obj.modifiers.new("Shurale_Skin", "ARMATURE")
    skin.object = rig
    skin.use_vertex_groups = True
    skin.use_bone_envelopes = False
    obj["lod_role"] = "runtime LOD0; visible by default after runtime explicitly hides LOD1"
    obj["triangle_count"] = triangulated_count(obj.data)
    return obj, prune_info


def make_lod1(lod0, rig, collection, bone_names):
    obj = lod0.copy()
    obj.data = lod0.data.copy()
    obj.name = LOD1_NAME
    obj.data.name = f"{LOD1_NAME}Mesh"
    collection.objects.link(obj)
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    for modifier in list(obj.modifiers):
        obj.modifiers.remove(modifier)
    decimate = obj.modifiers.new("Shurale_LOD1_Decimate", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = 0.43
    apply_modifier(obj, decimate.name)
    prune_info = prune_weights(obj, bone_names)
    skin = obj.modifiers.new("Shurale_Skin", "ARMATURE")
    skin.object = rig
    skin.use_vertex_groups = True
    skin.use_bone_envelopes = False
    obj["lod_role"] = "runtime LOD1; runtime must explicitly hide unless selected"
    obj["triangle_count"] = triangulated_count(obj.data)
    if obj["triangle_count"] >= lod0["triangle_count"]:
        raise RuntimeError("LOD1 did not reduce triangle count")
    return obj, prune_info


def convert_branch(branch, root, rig):
    ensure_object_mode()
    bpy.ops.object.select_all(action="DESELECT")
    branch.select_set(True)
    bpy.context.view_layer.objects.active = branch
    bpy.ops.object.convert(target="MESH")
    branch = bpy.context.view_layer.objects.active
    branch.name = BRANCH_NAME
    branch.data.name = "ShuraleWorkBranchMesh"
    branch.parent = root
    branch.parent_type = "OBJECT"
    branch.matrix_parent_inverse = Matrix.Identity(4)
    branch.location = (0.0, 0.0, 0.0)
    branch.rotation_euler = (0.0, 0.0, 0.0)
    branch.scale = (1.0, 1.0, 1.0)
    return branch


def make_gltf_branch_material(branch) -> None:
    """Replace the source-only procedural bark with a supported PBR color."""
    source = next((material for material in branch.data.materials if material), None)
    base_color = tuple(float(value) for value in source.diffuse_color) if source else (0.055, 0.042, 0.031, 1.0)
    material = bpy.data.materials.new("Shurale | exportable darkwood branch")
    material.diffuse_color = base_color
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader is None:
        raise RuntimeError("New branch material has no Principled BSDF")
    for link in list(material.node_tree.links):
        if link.to_node == shader and link.to_socket.name in {"Base Color", "Roughness", "Metallic", "Emission Color", "Emission"}:
            material.node_tree.links.remove(link)
    shader.inputs["Base Color"].default_value = base_color
    shader.inputs["Roughness"].default_value = 0.78
    shader.inputs["Metallic"].default_value = 0.0
    emission = shader.inputs.get("Emission Color") or shader.inputs.get("Emission")
    if emission is not None:
        emission.default_value = (0.0, 0.0, 0.0, 1.0)
    alpha = shader.inputs.get("Alpha")
    if alpha is not None:
        alpha.default_value = 1.0
    material["source_material"] = source.name if source else "none"
    material["export_note"] = "The source bark uses Generated-coordinate procedural nodes; this Principled base color and roughness are glTF-compatible."
    branch.data.materials.clear()
    branch.data.materials.append(material)


def normalize_root(root, rig, branch, helpers) -> tuple[float, float]:
    if root.parent is not None:
        raise RuntimeError("Shurale root unexpectedly has a parent")
    scale_values = tuple(float(component) for component in root.scale)
    if min(scale_values) <= 0 or max(scale_values) - min(scale_values) > 1e-5:
        raise RuntimeError(f"Expected positive uniform source-root scale, got {scale_values}")
    scale = scale_values[0]
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)
    rig.location = (
        -rig.location.x * scale,
        -rig.location.y * scale,
        rig.location.z * scale,
    )
    rig.scale = tuple(component * scale for component in rig.scale)
    # Blender's Y-up glTF export maps the authored Blender -Y front to glTF
    # +Z. Godot-facing actors need -Z, so correct the visual rig only and keep
    # the gameplay root transform at identity.
    rig.rotation_euler = (0.0, 0.0, math.pi)
    # The authored work pose and branch are in the original Blender -Y-facing
    # space. Rotate their local positions with the rig correction before the
    # IK pose is baked, otherwise the hands solve toward targets behind the
    # corrected character and the branch stays behind the body.
    branch.location = (
        -branch.location.x * scale,
        -branch.location.y * scale,
        branch.location.z * scale,
    )
    branch.scale = tuple(component * scale for component in branch.scale)
    branch.rotation_euler.z += math.pi
    helper_by_name = {helper.name: helper for helper in helpers}
    for helper in helpers:
        helper.location = (
            -helper.location.x * scale,
            -helper.location.y * scale,
            helper.location.z * scale,
        )
        helper.scale = (1.0, 1.0, 1.0)
    if PROFILE_NAME == "v4":
        for side, coords in WORK_IK_TARGETS_ROOT_LOCAL_M.items():
            target_name = f"Shurale_PoseIK_Target_{side}"
            pole_name = f"Shurale_PoseIK_Pole_{side}"
            if target_name not in helper_by_name or pole_name not in helper_by_name:
                raise RuntimeError(f"Missing arm IK target/pole helpers for side {side}")
            helper_by_name[target_name].location = coords
            helper_by_name[pole_name].location = WORK_IK_POLES_ROOT_LOCAL_M[side]
    root.name = ROOT_NAME
    rig.name = RIG_NAME
    root["runtime_scale"] = 1.0
    root["origin_contract"] = "feet rest on local z=0; root transform identity"
    root["facing_contract"] = "Source face Blender -Y; rig yaw 180 degrees about Blender Z; exported Godot-facing direction -Z; root remains identity"
    root["collision_owner"] = "W01 runtime; visual GLB has no collision proxies"
    return scale, -float(root.location.z)


def describe_v6_authored_workbase(root, helpers) -> dict:
    """Record the reviewed frozen-v6 work pose without running a grip solver."""
    root_inverse = root.matrix_world.inverted()
    targets = {}
    poles = {}
    for side in ("l", "r"):
        target = bpy.data.objects.get(f"Shurale_PoseIK_Target_{side}")
        pole = bpy.data.objects.get(f"Shurale_PoseIK_Pole_{side}")
        if target is None or pole is None or target not in helpers or pole not in helpers:
            raise RuntimeError(f"Frozen v6 workbase is missing the authored {side} arm IK helpers")
        targets[side] = [float(v) for v in root_inverse @ target.matrix_world.translation]
        poles[side] = [float(v) for v in root_inverse @ pole.matrix_world.translation]
    return {
        "acceptance_decision": "functional_workbase_only",
        "method": "Retained the root-reviewed frozen v6 authored pose and main-bough placement; applied only the reviewed index curls and measured side-shoot endpoint, with no hand-target solver or dual-hand pinch claim.",
        "left_hand_role": "supports the work branch; prop is parented to hand_l so it follows the accepted support hand",
        "right_hand_role": "touches/works on the thin side shoot; expressive thumb/index opposition remains an art review item",
        "dual_hand_grip_verified": False,
        "ik_targets_root_local_m": {"targets": targets, "poles": poles},
    }


def apply_reviewed_v6_contact_adjustment(rig, branch) -> dict:
    """Apply the one root-reviewed v6 fingertip/side-shoot correction set."""
    curls = {"l": 0.15, "r": 0.20}
    for side, delta in curls.items():
        for segment in (1, 2, 3):
            bone = rig.pose.bones.get(f"index_0{segment}_{side}")
            if bone is None:
                raise RuntimeError(f"Frozen v6 rig is missing index_0{segment}_{side}")
            bone.rotation_mode = "XYZ"
            bone.rotation_euler.x += delta
    if len(branch.data.splines) < 2 or branch.data.splines[1].type != "BEZIER" or len(branch.data.splines[1].bezier_points) != 3:
        raise RuntimeError("Frozen v6 work branch no longer has the reviewed three-point side shoot")
    endpoint = (-0.116686, -0.415850, 1.314126)
    branch.data.splines[1].bezier_points[-1].co = endpoint
    bpy.context.view_layer.update()
    return {
        "index_curl_additive_radians": {"left": curls["l"], "right": curls["r"]},
        "side_shoot_endpoint_source_blender_m": list(endpoint),
        "basis": "single root-reviewed measured v6 contact candidate; right expressive opposed-pad pinch remains unverified",
    }


def parent_branch_to_working_hand(branch, rig) -> float:
    world_matrix = branch.matrix_world.copy()
    branch.parent = rig
    branch.parent_type = "BONE"
    branch.parent_bone = "hand_l"
    branch.matrix_parent_inverse = Matrix.Identity(4)
    branch.matrix_world = world_matrix
    bpy.context.view_layer.update()
    return max(abs(branch.matrix_world[row][col] - world_matrix[row][col])
               for row in range(4) for col in range(4))


def matrix_bounds(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        return (
            tuple(min(point[axis] for point in points) for axis in range(3)),
            tuple(max(point[axis] for point in points) for axis in range(3)),
        )
    finally:
        evaluated.to_mesh_clear()


def glb_chunks(path: Path):
    data = path.read_bytes()
    if len(data) < 20:
        raise RuntimeError("GLB file is too small")
    magic, version, total = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or total != len(data):
        raise RuntimeError("Not a complete glTF 2.0 binary")
    offset = 12
    doc = None
    binary = b""
    while offset < total:
        length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset:offset + length]
        offset += length
        if chunk_type == 0x4E4F534A:
            doc = json.loads(chunk.rstrip(b"\x00 \t\r\n"))
        elif chunk_type == 0x004E4942:
            binary = chunk
    if doc is None:
        raise RuntimeError("GLB has no JSON chunk")
    return doc, binary


def glb_float_accessor_vectors(doc, binary: bytes, accessor_index: int) -> list[tuple[float, ...]]:
    accessor = doc["accessors"][accessor_index]
    if accessor["componentType"] != 5126 or "bufferView" not in accessor:
        raise RuntimeError(f"Expected a packed float animation accessor: {accessor_index}")
    component_count = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[accessor["type"]]
    view = doc["bufferViews"][accessor["bufferView"]]
    element_bytes = 4 * component_count
    stride = view.get("byteStride", element_bytes)
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    return [
        tuple(struct.unpack_from("<" + "f" * component_count, binary, start + index * stride))
        for index in range(accessor["count"])
    ]


def verify_glb(path: Path, lod_metrics, root, rig, actions, normalization_scale,
               bake_matrix_error, branch_parent_error, branch_alignment, weight_info,
               working_pose):
    doc, binary = glb_chunks(path)
    nodes = doc.get("nodes", [])
    node_names = {node.get("name") for node in nodes}
    expected_nodes = {ROOT_NAME, RIG_NAME, LOD0_NAME, LOD1_NAME, BRANCH_NAME}
    if not expected_nodes.issubset(node_names):
        raise RuntimeError(f"Missing expected GLB nodes: {sorted(expected_nodes - node_names)}")
    animations = doc.get("animations", [])
    animation_names = {item.get("name") for item in animations}
    if animation_names != set(EXPECTED_CLIPS):
        raise RuntimeError(f"Expected exactly seven named actions; got {sorted(animation_names)}")
    by_name = {node.get("name"): node for node in nodes}
    node_determinants = []
    for index, node in enumerate(nodes):
        for field in ("translation", "rotation", "scale", "matrix"):
            values = node.get(field, [])
            if any(not math.isfinite(float(value)) for value in values):
                raise RuntimeError(f"GLB node {index} ({node.get('name')}) has non-finite {field}: {values}")
        if "matrix" in node:
            flat = node["matrix"]
            transform = Matrix(tuple(tuple(flat[column * 4 + row] for column in range(4)) for row in range(4)))
            determinant = float(transform.to_3x3().determinant())
        else:
            scale_values = node.get("scale", [1.0, 1.0, 1.0])
            if any(float(value) <= 0.0 for value in scale_values):
                raise RuntimeError(f"GLB node {index} ({node.get('name')}) has non-positive scale: {scale_values}")
            rotation = node.get("rotation", [0.0, 0.0, 0.0, 1.0])
            rotation_length = math.sqrt(sum(float(value) ** 2 for value in rotation))
            if abs(rotation_length - 1.0) > 1e-4:
                raise RuntimeError(f"GLB node {index} ({node.get('name')}) has a non-unit quaternion: {rotation}")
            determinant = math.prod(float(value) for value in scale_values)
        if not math.isfinite(determinant) or determinant <= 0.0:
            raise RuntimeError(f"GLB node {index} ({node.get('name')}) has invalid transform determinant {determinant}")
        node_determinants.append(determinant)
    for name in (LOD0_NAME, LOD1_NAME):
        if "skin" not in by_name[name]:
            raise RuntimeError(f"{name} is not skinned in exported GLB")
    parent_by_child = {
        child: node.get("name")
        for node in nodes
        for child in node.get("children", [])
    }
    branch_parent_name = parent_by_child.get(nodes.index(by_name[BRANCH_NAME]))
    if PROFILE_NAME == "v6" and branch_parent_name != "hand_l":
        raise RuntimeError(f"The authored work branch is not attached to hand_l in GLB; parent={branch_parent_name!r}")
    if by_name[ROOT_NAME].get("scale", [1.0, 1.0, 1.0]) != [1.0, 1.0, 1.0]:
        raise RuntimeError(f"Root node is not unit scale: {by_name[ROOT_NAME].get('scale')}")
    if by_name[ROOT_NAME].get("translation", [0.0, 0.0, 0.0]) != [0.0, 0.0, 0.0]:
        raise RuntimeError(f"Root origin is not at the feet origin: {by_name[ROOT_NAME].get('translation')}")
    image_dims = []
    for image in doc.get("images", []):
        if "uri" in image:
            raise RuntimeError(f"GLB contains a non-embedded image URI: {image['uri']}")
        view = doc["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        width, height = struct.unpack_from(">II", binary, start + 16)
        image_dims.append([width, height])
    if PROFILE_NAME == "v6":
        if image_dims.count([2048, 2048]) < 2 or image_dims.count([1024, 1024]) < 1:
            raise RuntimeError(f"Expected embedded 2K albedo + tangent normal and 1K AO; found image dimensions {image_dims}")
        body_material = next((material for material in doc.get("materials", [])
                              if "surface v3" in material.get("name", "").lower()), None)
        if body_material is None:
            raise RuntimeError("GLB is missing the frozen v6 surface-v3 body material")
        pbr = body_material.get("pbrMetallicRoughness", {})
        texture_roles = {
            "base_color": "baseColorTexture" in pbr,
            "normal": "normalTexture" in body_material,
            "ambient_occlusion": "occlusionTexture" in body_material,
        }
        if not all(texture_roles.values()):
            raise RuntimeError(f"GLB did not preserve all three v6 material maps: {texture_roles}")
    else:
        if [2048, 2048] not in image_dims or [1024, 1024] not in image_dims:
            raise RuntimeError(f"Expected embedded 2K albedo and 1K mask; found image dimensions {image_dims}")
        texture_roles = {"base_color": True, "normal": False, "ambient_occlusion": False}
    if any(material.get("emissiveFactor", [0.0, 0.0, 0.0]) != [0.0, 0.0, 0.0]
           for material in doc.get("materials", [])):
        raise RuntimeError("A Shurale material is unexpectedly emissive")
    nodes_by_index = {index: node.get("name") for index, node in enumerate(nodes)}
    root_motion_tracks = []
    constant_root_transform_tracks = []
    for animation in animations:
        for channel in animation.get("channels", []):
            target = channel.get("target", {})
            node_name = nodes_by_index.get(target.get("node"))
            if node_name in {ROOT_NAME, "Root"} and target.get("path") in {"translation", "scale"}:
                sampler = animation["samplers"][channel["sampler"]]
                samples = glb_float_accessor_vectors(doc, binary, sampler["output"])
                reference = samples[0] if samples else ()
                if any(abs(value - reference[axis]) > 1e-6
                       for sample in samples[1:]
                       for axis, value in enumerate(sample)):
                    root_motion_tracks.append((animation["name"], node_name, target.get("path")))
                else:
                    constant_root_transform_tracks.append((animation["name"], node_name, target.get("path")))
    if root_motion_tracks:
        raise RuntimeError(f"GLB contains root translation/scale animation: {root_motion_tracks}")
    if len(doc.get("skins", [])) != 1:
        raise RuntimeError(f"Expected one shared 53-bone skin, got {len(doc.get('skins', []))}")
    joints = len(doc["skins"][0].get("joints", []))
    if joints != 53:
        raise RuntimeError(f"Expected 53 native rig bones in GLB skin, got {joints}")
    inverse_bind_determinants = []
    for skin_index, skin in enumerate(doc["skins"]):
        accessor_index = skin.get("inverseBindMatrices")
        if accessor_index is None:
            raise RuntimeError(f"Skin {skin_index} is missing inverse bind matrices")
        for matrix_values in glb_float_accessor_vectors(doc, binary, accessor_index):
            matrix = Matrix(tuple(tuple(matrix_values[column * 4 + row] for column in range(4)) for row in range(4)))
            determinant = float(matrix.to_3x3().determinant())
            if not math.isfinite(determinant) or determinant <= 0.0:
                raise RuntimeError(f"Skin {skin_index} has invalid inverse-bind determinant {determinant}")
            inverse_bind_determinants.append(determinant)
    texture_images = len(doc.get("images", []))
    if texture_images < 2:
        raise RuntimeError(f"Expected embedded material textures; got {texture_images}")
    return {
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
        "node_names": sorted(node_names),
        "animation_names": sorted(animation_names),
        "animation_durations_seconds": {
            item["name"]: round(max((doc["accessors"][sampler["input"]].get("max", [0.0])[0]
                                     for sampler in item.get("samplers", [])), default=0.0), 4)
            for item in animations
        },
        "skins": len(doc.get("skins", [])),
        "joint_count": joints,
        "node_transform_determinant_range": [min(node_determinants), max(node_determinants)],
        "inverse_bind_matrix_count": len(inverse_bind_determinants),
        "inverse_bind_determinant_range": [min(inverse_bind_determinants), max(inverse_bind_determinants)],
        "embedded_images": texture_images,
        "embedded_image_dimensions": image_dims,
        "material_count": len(doc.get("materials", [])),
        "material_texture_roles": texture_roles,
        "lod_runtime_selection": (
            "Both visual LODs stay visible and use Self fade: ShuraleBody_LOD0 begin=0m/margin=0m/end=12m/margin=2m; ShuraleBody_LOD1 begin=12m/margin=2m/end=0m/margin=0m. This paired overlap matches game/scripts/photoworlds/W01Controller.ShuraleLod.cs."
            if PROFILE_NAME == "v6" else
            "Both visual LODs are present. Runtime must set ShuraleBody_LOD0.visible=true and ShuraleBody_LOD1.visible=false before showing the instance; later switch visibility so only one LOD is rendered."
        ),
        "branch_runtime_selection": "ShuraleWorkBranch is a child of ShuraleRig/hand_l; keep visible throughout the Shurale interaction and hide only when the actor is hidden. The reviewed left hand supports the branch; the right works the thin shoot, but expressive opposed-pad pinch is not verified. The prop is not a world-root or F14 bag-support anchor.",
        "branch_parent_node": branch_parent_name,
        "root_motion_tracks": root_motion_tracks,
        "constant_root_transform_tracks": constant_root_transform_tracks,
        "lod_triangles": lod_metrics,
        "root_scale": tuple(float(v) for v in root.scale),
        "rig_scale": tuple(float(v) for v in rig.scale),
        "normalization_scale_absorbed_from_source_root": normalization_scale,
        "working_pose_bake_max_matrix_error": bake_matrix_error,
        "working_pose_channels": working_pose,
        "working_ik_targets_root_local_m": (
            branch_alignment["ik_targets_root_local_m"]
            if "ik_targets_root_local_m" in branch_alignment
            else {
                "targets": {
                    "l": branch_alignment["left_ik_target_root_local_m"],
                    "r": branch_alignment["right_ik_target_solved_root_local_m"],
                },
                "poles": {side: list(coords) for side, coords in WORK_IK_POLES_ROOT_LOCAL_M.items()},
            }
        ),
        "branch_working_pose": branch_alignment,
        "branch_reparent_max_matrix_error": branch_parent_error,
        "weight_limits": weight_info,
        "root_bounds_m": {"min": [round(v, 6) for v in root["bounds_min"]],
                           "max": [round(v, 6) for v in root["bounds_max"]]},
    }


def make_review_camera(scene, name, location, target, ortho_scale):
    camera_object = bpy.data.objects.get(name)
    if camera_object is not None:
        bpy.data.objects.remove(camera_object, do_unlink=True)
    camera_data = bpy.data.cameras.new(f"{name}Data")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = float(ortho_scale)
    camera_object = bpy.data.objects.new(name, camera_data)
    scene.collection.objects.link(camera_object)
    camera_object.location = location
    direction = Vector(target) - camera_object.location
    if direction.length < 1e-6:
        raise RuntimeError(f"Review camera {name} has coincident location and target")
    camera_object.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return camera_object


def setup_v6_review_cameras(scene):
    return {
        "full": make_review_camera(scene, "ShuraleReviewCameraFull", (0.35, 4.3, 2.15), (0.0, 0.0, 1.20), 3.35),
        "three_quarter": make_review_camera(scene, "ShuraleReviewCameraThreeQuarter", (2.15, 3.8, 2.25), (0.0, 0.0, 1.24), 3.5),
        "face": make_review_camera(scene, "ShuraleReviewCameraFace", (0.0, 2.55, 2.18), (0.0, 0.0, 2.08), 1.14),
        "hands": make_review_camera(scene, "ShuraleReviewCameraHands", (0.1, 2.75, 1.48), (0.0, 0.0, 1.35), 1.15),
    }


def render_frame(scene, rig, action_name, frame, output: Path, camera_view="full"):
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Missing render action {action_name}")
    rig.animation_data.action = action
    if PROFILE_NAME == "v6":
        camera_object = REVIEW_CAMERAS.get(camera_view)
        if camera_object is None:
            raise RuntimeError(f"Missing v6 review camera {camera_view!r}")
        scene.camera = camera_object
    else:
        scene.camera = bpy.data.objects.get(CAMERA_NAME) or scene.camera
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def configure_profile(profile: str) -> None:
    global PROFILE_NAME, INPUT_BLEND, OUTPUT_DIR, GLB_PATH, BLEND_PATH
    global SOURCE_ROOT_NAME, SOURCE_RIG_NAME, SOURCE_BODY_NAME, CAMERA_NAME
    global ALBEDO_NAME, MASK_NAME
    if profile not in {"v4", "v6"}:
        raise RuntimeError(f"Unknown Shurale export profile {profile!r}; expected v4 or v6")
    PROFILE_NAME = profile
    if profile == "v4":
        return
    INPUT_BLEND = V6_SURFACE_BLEND
    OUTPUT_DIR = V6_OUTPUT_DIR
    GLB_PATH = OUTPUT_DIR / "shurale_v1_candidate.glb"
    BLEND_PATH = OUTPUT_DIR / "shurale_v1_source.blend"
    SOURCE_ROOT_NAME = "ShuraleCharacterRootV6"
    SOURCE_RIG_NAME = "ShuraleCharacterRigV6"
    SOURCE_BODY_NAME = "ShuraleCharacterBodyV6"
    CAMERA_NAME = "ShuraleCharacterV6Camera"
    ALBEDO_NAME = V6_ALBEDO_NAME
    MASK_NAME = V6_AO_NAME


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    profile = args[args.index("--profile") + 1] if "--profile" in args else "v4"
    configure_profile(profile)
    input_blend = INPUT_BLEND
    if "--input" in args:
        input_blend = Path(args[args.index("--input") + 1])
        if not input_blend.is_absolute():
            input_blend = PROJECT_ROOT / input_blend
    input_blend = input_blend.resolve()
    if input_blend != INPUT_BLEND.resolve():
        raise RuntimeError(f"Refusing unapproved base blend: {input_blend}")
    if not input_blend.is_file():
        raise RuntimeError(f"Missing reviewed {PROFILE_NAME} source blend: {input_blend}")
    if PROFILE_NAME == "v6":
        if sha256(V6_BASE_BLEND) != V6_BASE_BLEND_SHA256:
            raise RuntimeError("Frozen v6 geometry/rig input SHA changed; refusing to export a different base")
        if sha256(V6_SURFACE_BLEND) != V6_SURFACE_BLEND_SHA256:
            raise RuntimeError("Frozen surface-v3 input SHA changed; refusing to export a different material pass")
        surface_manifest = json.loads(V6_SURFACE_MANIFEST.read_text(encoding="utf-8"))
        if surface_manifest.get("source", {}).get("inputBlendSHA256") != V6_BASE_BLEND_SHA256:
            raise RuntimeError("Surface-v3 manifest does not point at the frozen v6 base")
        arm_pose_overrides, arm_pose_override_metadata = load_v6_arm_pose_overrides()
        if not V6_OUTPUT_DIR.resolve().is_relative_to(V6_DIR.resolve()):
            raise RuntimeError(f"Refusing v6 output outside the owned character folder: {V6_OUTPUT_DIR}")
    elif OUTPUT_DIR.resolve().parent != SCRIPT_DIR.resolve():
        raise RuntimeError(f"Refusing output outside the owned Shurale source folder: {OUTPUT_DIR}")
    else:
        arm_pose_overrides = {}
        arm_pose_override_metadata = None
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(input_blend))
    scene = bpy.context.scene
    root = bpy.data.objects.get(SOURCE_ROOT_NAME)
    rig = bpy.data.objects.get(SOURCE_RIG_NAME)
    source_body = bpy.data.objects.get(SOURCE_BODY_NAME)
    branch = bpy.data.objects.get("StagingOnly_Branch_ExcludeFromRuntime")
    if not all((root, rig, source_body, branch)):
        raise RuntimeError(f"Reviewed {PROFILE_NAME} blend is missing its accepted root, rig, body, or work branch")
    if len(rig.data.bones) != 53:
        raise RuntimeError(f"Native source rig changed: expected 53 bones, got {len(rig.data.bones)}")
    if len(bpy.data.actions) != 0:
        raise RuntimeError(f"Expected no pre-existing animations in reviewed base blend, got {[a.name for a in bpy.data.actions]}")

    source_root_scale = tuple(float(component) for component in root.scale)
    source_root_location = tuple(float(component) for component in root.location)
    if max(abs(value) for value in source_root_location) > 0.01:
        raise RuntimeError(f"Unexpected source root offset: {source_root_location}")
    normalized_scale = source_root_scale[0]
    root.name = ROOT_NAME
    rig.name = RIG_NAME
    source_body.name = "ShuraleSourceBodyArchive"
    source_body.data.name = "ShuraleSourceBodyArchiveMesh"
    contact_adjustment = None
    if PROFILE_NAME == "v6":
        contact_adjustment = apply_reviewed_v6_contact_adjustment(rig, branch)
    branch = convert_branch(branch, root, rig)
    make_gltf_branch_material(branch)
    # Absorb the source root's uniform scale into its direct children so the
    # runtime root is identity-scale with feet at the world origin.
    helpers = [obj for obj in list(root.children) if obj.type == "EMPTY"]
    normalization_scale, _ = normalize_root(root, rig, branch, helpers)
    bpy.context.view_layer.update()
    rig.animation_data_create()
    if PROFILE_NAME == "v6":
        branch_alignment = describe_v6_authored_workbase(root, helpers)
        branch_alignment["reviewed_pose_adjustments"] = contact_adjustment
    else:
        branch_alignment = align_branch_to_working_hands(source_body, branch, root)
    working_pose, bake_matrix_error = bake_working_pose(rig)
    apply_pose_values(rig, working_pose)
    if PROFILE_NAME == "v4":
        post_bake_contacts = {
            group_name: find_branch_hand_contact(source_body, branch, group_name)
            for group_name in ("index_01_l", "thumb_02_r")
        }
        branch_alignment["left_gap_after_bake_m"] = post_bake_contacts["index_01_l"]["distance_m"]
        branch_alignment["right_gap_after_bake_m"] = post_bake_contacts["thumb_02_r"]["distance_m"]
        if max(value["distance_m"] for value in post_bake_contacts.values()) > 0.002:
            raise RuntimeError(
                "Baked pose no longer preserves measured branch contact: "
                f"left={branch_alignment['left_gap_after_bake_m']:.6f} m, "
                f"right={branch_alignment['right_gap_after_bake_m']:.6f} m"
            )
    branch_parent_error = parent_branch_to_working_hand(branch, rig)
    if branch_parent_error > 1e-5:
        raise RuntimeError(f"Work branch moved while parenting to the stabilizing hand: matrix error={branch_parent_error:.7f}")
    reset_pose(rig)
    scene.frame_set(1)
    bpy.context.view_layer.update()

    source_body.hide_render = True
    source_body.hide_viewport = True
    # Keep the original generator geometry in the input .blend; the staged
    # package scene contains only the masked, weighted export meshes.
    make_gltf_materials(source_body)
    runtime_collection = bpy.data.collections.new("Shurale_Runtime_Visual")
    scene.collection.children.link(runtime_collection)
    bone_names = set(rig.data.bones.keys())
    lod0, lod0_weights = make_lod0(source_body, rig, runtime_collection, bone_names)
    lod1, lod1_weights = make_lod1(lod0, rig, runtime_collection, bone_names)
    lod_metrics = {LOD0_NAME: triangulated_count(lod0.data), LOD1_NAME: triangulated_count(lod1.data)}
    if lod_metrics[LOD0_NAME] < 25000 or lod_metrics[LOD0_NAME] > 30000:
        raise RuntimeError(f"LOD0 did not meet the requested ~27k triangle target: {lod_metrics[LOD0_NAME]}")
    weight_info = {LOD0_NAME: lod0_weights, LOD1_NAME: lod1_weights}
    source_mesh_data = source_body.data
    bpy.data.objects.remove(source_body, do_unlink=True)
    if source_mesh_data.users == 0:
        bpy.data.meshes.remove(source_mesh_data)
    for obj in (root, rig, lod0, lod1, branch):
        if any(not math.isfinite(value) or value <= 0 for value in obj.scale):
            raise RuntimeError(f"Non-finite or non-positive scale on {obj.name}: {tuple(obj.scale)}")
    if abs(root.scale.x - 1.0) > 1e-8 or abs(root.scale.y - 1.0) > 1e-8 or abs(root.scale.z - 1.0) > 1e-8:
        raise RuntimeError(f"ShuraleRoot is not unit scale: {tuple(root.scale)}")
    root_bounds = matrix_bounds(lod0)
    if root_bounds[0][2] < -0.002 or root_bounds[0][2] > 0.002:
        raise RuntimeError(f"Feet origin drifted from local z=0: {root_bounds[0][2]:.6f} m")
    if not 2.32 <= root_bounds[1][2] - root_bounds[0][2] <= 2.48:
        raise RuntimeError(f"Exported character height outside ~2.4m: {root_bounds[1][2] - root_bounds[0][2]:.6f} m")
    root["bounds_min"] = [float(v) for v in root_bounds[0]]
    root["bounds_max"] = [float(v) for v in root_bounds[1]]
    step_measurements = None
    if PROFILE_NAME == "v6":
        step_pose_samples, step_measurements = sample_measured_in_place_step(rig, working_pose)
        animations = create_shurale_animations(
            rig, working_pose, key_action, deg_pose, FPS,
            arm_pose_overrides=arm_pose_overrides,
            step_pose_samples=step_pose_samples,
        )
    else:
        animations = create_shurale_animations(rig, working_pose, key_action, deg_pose, FPS)
    if set(animations) != set(EXPECTED_CLIPS):
        raise RuntimeError(f"Animation set mismatch: {sorted(animations)}")

    # Source renders use LOD0 alone for clarity. The exported GLB carries both paired-fade LODs.
    lod1.hide_render = True
    if PROFILE_NAME == "v6":
        REVIEW_CAMERAS.clear()
        REVIEW_CAMERAS.update(setup_v6_review_cameras(scene))
    else:
        preview_camera = bpy.data.objects.get(CAMERA_NAME)
        if preview_camera is not None:
            preview_camera.matrix_world = Matrix.Rotation(math.pi, 4, "Z") @ preview_camera.matrix_world
            bpy.context.view_layer.update()
    preview_dir = OUTPUT_DIR
    if PROFILE_NAME == "v6":
        render_frame(scene, rig, "work", 15, preview_dir / "shurale_v1_working_full.png", "full")
        render_frame(scene, rig, "work", 15, preview_dir / "shurale_v1_working_three-quarter.png", "three_quarter")
        render_frame(scene, rig, "work", 15, preview_dir / "shurale_v1_working_face.png", "face")
        render_frame(scene, rig, "work", 15, preview_dir / "shurale_v1_working_hands.png", "hands")
        render_frame(scene, rig, "noticed", 18, preview_dir / "shurale_v1_noticed_full.png", "full")
        render_frame(scene, rig, "gesture", 15, preview_dir / "shurale_v1_gesture_full.png", "full")
        render_frame(scene, rig, "yield", 16, preview_dir / "shurale_v1_yield_full.png", "full")
        render_frame(scene, rig, "step", 17, preview_dir / "shurale_v1_step_full.png", "three_quarter")
    else:
        render_frame(scene, rig, "work", 15, preview_dir / "shurale_v1_working.png")
        branch.hide_render = True
        render_frame(scene, rig, "noticed", 11, preview_dir / "shurale_v1_noticed_head-pause.png")
        render_frame(scene, rig, "noticed", 21, preview_dir / "shurale_v1_noticed_shoulders.png")
        branch.hide_render = False
    rig.animation_data.action = bpy.data.actions["work"]
    scene.frame_set(15)
    bpy.context.view_layer.update()

    # Export only the stable visual hierarchy; cameras, lights, floor and the
    # source-body archive remain source-preview-only.
    ensure_object_mode()
    bpy.ops.object.select_all(action="DESELECT")
    lod1.hide_render = False
    lod1.hide_viewport = False
    for obj in (root, rig, lod0, lod1, branch):
        obj.hide_select = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_skins=True,
    )
    bpy.ops.object.select_all(action="DESELECT")
    lod1.hide_render = True
    lod1.hide_viewport = True
    scene.camera = (REVIEW_CAMERAS.get("full") if PROFILE_NAME == "v6" else None) or bpy.data.objects.get(CAMERA_NAME) or scene.camera
    rig.animation_data.action = bpy.data.actions["work"]
    branch.hide_render = False
    scene.frame_set(15)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)

    metrics = verify_glb(GLB_PATH, lod_metrics, root, rig, animations, normalization_scale,
                         bake_matrix_error, branch_parent_error, branch_alignment, weight_info,
                         working_pose)
    if PROFILE_NAME == "v6" and step_measurements is not None:
        step_samples = step_measurements["samples"]
        metrics["step_motion"] = {
            "support_side": step_measurements["support_side"],
            "step_side": step_measurements["step_side"],
            "leg_length_m": step_measurements["leg_length_m"],
            "step_distance_m": step_measurements["step_distance_m"],
            "lift_m": step_measurements["lift_m"],
            "max_support_foot_drift_m": max(sample["support_foot_endpoint_error_m"] for sample in step_samples),
            "max_step_foot_target_error_m": max(sample["step_foot_endpoint_target_error_m"] for sample in step_samples),
            "root_motion": False,
        }
    input_manifest_path = V6_SURFACE_MANIFEST if PROFILE_NAME == "v6" else SCRIPT_DIR / "material-v2/manifest.json"
    history_manifest_path = V6_DIR / "manifest.json" if PROFILE_NAME == "v6" else SCRIPT_DIR / "manifest.json"
    history_manifest = json.loads(history_manifest_path.read_text(encoding="utf-8"))
    source_meta = history_manifest.get("source", {})
    if PROFILE_NAME == "v6":
        albedo_path = V6_SURFACE_BLEND.parent / V6_ALBEDO_NAME
        normal_path = V6_SURFACE_BLEND.parent / V6_NORMAL_NAME
        mask_path = V6_SURFACE_BLEND.parent / V6_AO_NAME
        base_asset_name = source_meta.get("name")
        source_version = source_meta.get("tag")
        source_commit = source_meta.get("commit")
        base_license = source_meta.get("baseMeshTargetsRigAndWeightsLicense")
        source_code_license = source_meta.get("sourceLicense")
        generated_rights = source_meta.get("generatedOutputStatus")
        archive_sha = source_meta.get("archiveSHA256")
    else:
        albedo_path = SCRIPT_DIR / "material-v2/shurale-v4-body-albedo-2k.png"
        normal_path = None
        mask_path = SCRIPT_DIR / "material-v2/shurale-v4-body-mask-ao-1k.png"
        base_asset_name = source_meta.get("name")
        source_version = source_meta.get("tag")
        source_commit = source_meta.get("commit")
        base_license = source_meta.get("baseMeshTargetsRigAndWeightsLicense")
        source_code_license = source_meta.get("sourceLicense")
        generated_rights = source_meta.get("generatedOutputStatus")
        archive_sha = source_meta.get("archiveSHA256")
    manifest = {
        "schema": "urman.asset_candidate.v1",
        "asset_id": "photoworld.w01.shurale",
        "version": "shurale_v1_source_stage",
        "status": "source-stage package candidate; visual/art acceptance and native runtime integration remain open; do not promote to runtime yet",
        "source": {
            "input_blend": input_blend.relative_to(PROJECT_ROOT).as_posix(),
            "input_blend_sha256": sha256(input_blend),
            "input_material_manifest": input_manifest_path.relative_to(PROJECT_ROOT).as_posix(),
            "base_asset_name": source_meta.get("name"),
            "source_version": source_meta.get("tag"),
            "source_commit": source_meta.get("commit"),
            "base_mesh_targets_rig_and_weights_license": source_meta.get("baseMeshTargetsRigAndWeightsLicense"),
            "source_code_license_scope": source_meta.get("sourceLicense"),
            "generated_output_rights_note": source_meta.get("generatedOutputStatus"),
            "source_archive_sha256": source_meta.get("archiveSHA256"),
            "authoring": "Derived only from the reviewed, locally generated MPFB2 body/53-bone rig and project-authored material-v2 images. No source model or reference image is modified or copied into runtime. Working branch is the existing authored staging prop, baked to mesh.",
            "external_rights_or_art_acceptance": "pending; this package is not final art acceptance",
        },
        "runtime_contract": {
            "source_stage_glb": GLB_PATH.relative_to(PROJECT_ROOT).as_posix(),
            "runtime_copy_target_after_root_review_only": "game/assets/photoworlds/shurale/shurale_v1.glb",
            "root_node": ROOT_NAME,
            "root_transform": "identity scale/rotation/translation; feet rest at local z=0",
            "rig_node": RIG_NAME,
            "rig_type": "native MPFB game_engine armature; 53 bones",
            "visual_lod_nodes": [LOD0_NAME, LOD1_NAME],
            "lod_visibility_before_instance_is_shown": (
                "Keep both LOD nodes visible and configure Self fade exactly as W01Controller.ShuraleLod.cs: LOD0 begin=0m, beginMargin=0m, end=12m, endMargin=2m; LOD1 begin=12m, beginMargin=2m, end=0m, endMargin=0m."
                if PROFILE_NAME == "v6" else
                "Set ShuraleBody_LOD0.visible=true and ShuraleBody_LOD1.visible=false, then switch visibility so exactly one LOD renders."
            ),
            "origin": "feet at local z=0; neutral standing pose; metres in the root coordinate space",
            "facing": "Godot -Z; source face is Blender -Y, corrected by a 180-degree Blender-Z yaw on ShuraleRig while ShuraleRoot remains identity",
            "facing_correction": {"node": RIG_NAME, "source_axis": "Blender -Y", "export_yaw_degrees_about_Blender_Z": 180, "target_axis": "Godot -Z"},
            "root_motion": "off; ShuraleRoot and the root rig bone have no translation or scale animation",
            "animation_playback": {
                "looping_clips": ["work", "idle"],
                "one_shot_clips": ["noticed", "gesture", "speak", "yield", "step"],
                "handoff_contract": "These seven names and frame ranges are present, but gesture/yield arm release and step motion are not authored yet. noticed currently changes only head/neck over the retained work grip; gesture/yield retain the work arm channels; step is a static planted hold. Do not treat the names as completed interaction motion.",
                "note": "glTF animation clips do not carry Godot loop modes; the runtime controller assigns loop modes during integration.",
                "unresolved_motion": ["noticed shoulder follow", "gesture hand indication", "yield open hands", "measured in-place step/weightshift"],
            },
            "collision": "none in visual GLB; W01 runtime owns physical and interaction proxies",
            "animation_player": "Godot importer creates it; integration should find the AnimationPlayer recursively under ShuraleRoot",
            "work_prop": {
                "node": BRANCH_NAME,
                "parent": "ShuraleRig/hand_l",
                "intent": "separate thin branch prop holding the reviewed work-pose hand contact",
                "visibility": "keep visible throughout the Shurale interaction; hide only when the actor is hidden",
                "is_world_root_support": False,
                "is_f14_bag_snag_anchor": False,
            },
            "clips": [
                {"name": "work", "loop": True, "intent": "holds the reviewed left stabilizing grip/right pinch pose around the thin branch; no hand drift"},
                {"name": "noticed", "loop": False, "intent": "currently changes head/neck over the retained work-arm grip; shoulder follow remains unimplemented"},
                {"name": "gesture", "loop": False, "intent": "currently glances toward the strap while both work grips remain; hand indication awaits reviewed arm samples"},
                {"name": "speak", "loop": False, "intent": "subtle head/neck cadence; no lip-sync dependency"},
                {"name": "yield", "loop": False, "intent": "currently changes head/neck while retaining the work grip; open-hand yielding motion remains unimplemented"},
                {"name": "step", "loop": False, "intent": "currently holds the planted work pose; measured in-place weightshift remains unimplemented"},
                {"name": "idle", "loop": True, "intent": "small, restrained head/neck movement"},
            ],
            "clip_frames_at_fps": {name: [int(frames[0]), int(frames[1])] for name, frames in animations.items()},
        },
        "geometry": {
            "lod_triangles": lod_metrics,
            "lod0_target_triangles": TARGET_LOD0_TRIANGLES,
            "native_bone_count": len(rig.data.bones),
            "maximum_skin_influences": 4,
            "weights_normalized": True,
            "weight_histograms": {name: info["histogram"] for name, info in weight_info.items()},
            "uv_layer": "UVMap",
            "skin_albedo": {"file": albedo_path.relative_to(PROJECT_ROOT).as_posix(), "sha256": sha256(albedo_path), "size": [2048, 2048]},
            "surface_mask": {"file": mask_path.relative_to(PROJECT_ROOT).as_posix(), "sha256": sha256(mask_path), "size": [1024, 1024], "usage": "non-color grayscale texture linked to PBR roughness for stable glTF embedding"},
            "eye_material": {"emissive": False, "roughness": 0.1},
        },
        "verification": metrics,
        "source_blend": BLEND_PATH.relative_to(PROJECT_ROOT).as_posix(),
        "offline_views": [
            (preview_dir / "shurale_v1_working.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_noticed_head-pause.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_noticed_shoulders.png").relative_to(PROJECT_ROOT).as_posix(),
        ],
        "candidate_review": "Engineering package only. The root author must review actual exported-source renders and GLB before any runtime copy; PW047 art acceptance and final cultural/author review are not claimed.",
    }
    if PROFILE_NAME == "v6":
        manifest["version"] = "shurale_v1_source_stage_v6"
        manifest["status"] = "source-stage v6 engineering candidate; reviewed functional workbase only; right opposed-pad pinch, character art acceptance, and native runtime integration remain open; do not promote to runtime yet"
        manifest["source"].update({
            "base_geometry_blend": V6_BASE_BLEND.relative_to(PROJECT_ROOT).as_posix(),
            "base_geometry_blend_sha256": sha256(V6_BASE_BLEND),
            "surface_material_blend": V6_SURFACE_BLEND.relative_to(PROJECT_ROOT).as_posix(),
            "surface_material_blend_sha256": sha256(V6_SURFACE_BLEND),
            "surface_manifest": V6_SURFACE_MANIFEST.relative_to(PROJECT_ROOT).as_posix(),
            "authoring": "Derived from the frozen v6 same-topology sculpt/native 53-bone CC0 body and reviewed surface-v3 baked maps. The exporter retains the root-reviewed authored workbase, applies the separately reviewed index-curl/side-shoot correction and measured gesture/yield right-arm pose overrides, creates the measured step/head actions, prepares paired-fade LODs, and attaches the visible branch to hand_l. No reference pixels or external model are copied.",
        })
        contract = manifest["runtime_contract"]
        contract["lod_fade_runtime_config"] = {
            "fade_mode": "Self",
            LOD0_NAME: {"visible": True, "begin_m": 0.0, "begin_margin_m": 0.0, "end_m": 12.0, "end_margin_m": 2.0},
            LOD1_NAME: {"visible": True, "begin_m": 12.0, "begin_margin_m": 2.0, "end_m": 0.0, "end_margin_m": 0.0},
            "source": "game/scripts/photoworlds/W01Controller.ShuraleLod.cs",
        }
        contract["animation_playback"].update({
            "handoff_contract": "Seven named actions are derived from the frozen v6 working pose and the reviewed gesture/yield right-arm samples. work and idle loop; noticed includes measured root-local head yaw toward the approach; gesture visibly points toward the lower bag target; yield opens the right palm toward the player while the left hand keeps supporting the branch; step is a measured in-place right-foot settling step with the left support foot fixed. Gesture is an indication, not contact: the evaluated index tip remains about 0.370m from the bag target. Shoulder follow, expressive right thumb/index opposition, overall character art, and author/cultural acceptance remain open.",
            "note": "Godot clip loop modes are assigned by W01Controller.Shurale.cs: Linear for work/idle and None for noticed/gesture/speak/yield/step.",
            "unresolved_motion": [
                "noticed shoulder follow",
                "expressive right thumb/index pinch and contact normals",
            ],
        })
        contract["animation_playback"]["godot_loop_modes"] = {
            name: ("Linear" if name in {"work", "idle"} else "None")
            for name in EXPECTED_CLIPS
        }
        for clip in contract["clips"]:
            clip["godot_loop_mode"] = "Linear" if clip["name"] in {"work", "idle"} else "None"
        contract["work_prop"].update({
            "intent": "visible thin work branch; the left hand supports it and the right hand touches/works on the side shoot in the root-reviewed frozen v6 workbase",
            "right_hand_contact_status": "functional work contact only; expressive opposed thumb/index pad pinch is not verified",
            "reviewed_pose_adjustments": contact_adjustment,
        })
        contract["reviewed_arm_pose_overrides"] = arm_pose_override_metadata
        clip_intents = {
            "work": "retains the root-reviewed functional v6 workbase: left hand supports the visible branch; right hand works the thin side shoot; no verified dual-hand pinch claim",
            "noticed": "turns the head toward the measured F14 approach heading; shoulders remain on the workbase and shoulder follow is unresolved",
            "gesture": "glances toward the lower bag target and points with the right hand; the evaluated index tip remains about 0.370m short, so this is visual indication rather than contact",
            "speak": "subtle head/neck cadence; no lip-sync dependency; arms retain the workbase",
            "yield": "turns toward the player and presents a distinct open right palm; left hand continues to support the branch",
            "step": "measured short in-place right-foot step/lift/settle with left support foot fixed and no root motion; arms retain workbase",
            "idle": "small, restrained head/neck movement over the workbase",
        }
        for clip in contract["clips"]:
            clip["intent"] = clip_intents[clip["name"]]
        manifest["geometry"].update({
            "skin_albedo": {
                "file": albedo_path.relative_to(PROJECT_ROOT).as_posix(),
                "sha256": sha256(albedo_path),
                "size": [2048, 2048],
                "color_space": "sRGB",
            },
            "tangent_normal": {
                "file": normal_path.relative_to(PROJECT_ROOT).as_posix(),
                "sha256": sha256(normal_path),
                "size": [2048, 2048],
                "color_space": "Non-Color, +Y tangent convention",
            },
            "ambient_occlusion": {
                "file": mask_path.relative_to(PROJECT_ROOT).as_posix(),
                "sha256": sha256(mask_path),
                "size": [1024, 1024],
                "color_space": "Non-Color",
            },
        })
        manifest["geometry"].pop("surface_mask", None)
        manifest["verification"]["step_motion"] = metrics.get("step_motion")
        manifest["offline_views"] = [
            (preview_dir / "shurale_v1_working_full.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_working_three-quarter.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_working_face.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_working_hands.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_noticed_full.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_gesture_full.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_yield_full.png").relative_to(PROJECT_ROOT).as_posix(),
            (preview_dir / "shurale_v1_step_full.png").relative_to(PROJECT_ROOT).as_posix(),
        ]
        manifest["candidate_review"] = "Engineering package only. Root has reviewed the measured gesture/yield source poses; the exact exported GLB must still be reimported and checked for all-clip left-branch stability, both LODs, and current full/face/motion views before runtime copy. Right-hand work remains functional rather than an expressive opposed-pad pinch; PW047 character art and final cultural/author acceptance are not claimed."
    manifest_path = OUTPUT_DIR / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "source_blend": str(BLEND_PATH),
        "source_stage_glb": str(GLB_PATH),
        "manifest": str(manifest_path),
        "lod_triangles": lod_metrics,
        "bone_count": len(rig.data.bones),
        "clips": sorted(animations),
        "glb_images": metrics["embedded_image_dimensions"],
        "root_origin_bounds": metrics["root_bounds_m"],
        "working_pose_bake_max_matrix_error": bake_matrix_error,
        "branch_parent_max_matrix_error": branch_parent_error,
        "branch_grip_alignment": branch_alignment,
        "output_sha256": metrics["sha256"],
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
