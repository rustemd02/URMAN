"""Verify the generated character kit's provenance and LOD contract."""

import bpy


scene = bpy.context.scene
prefixes = [item for item in str(scene.get("character_prefixes", "")).split(",") if item]
if len(prefixes) != 9 or len(set(prefixes)) != len(prefixes):
    raise RuntimeError(f"Expected 9 character prefixes, got {prefixes!r}")

lod0 = [obj for obj in scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD0")]
lod1 = [obj for obj in scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD1")]
mesh_objects = [obj for obj in scene.objects if obj.type == "MESH"]
malformed_meshes = [
    obj.name
    for obj in mesh_objects
    if not (obj.name.endswith("_LOD0") or obj.name.endswith("_LOD1"))
]
if malformed_meshes:
    raise RuntimeError(f"Mesh names must end in _LOD0 or _LOD1: {malformed_meshes}")
unexpected_prefix_meshes = [
    obj.name
    for obj in mesh_objects
    if not any(obj.name.startswith(f"{prefix}_") for prefix in prefixes)
]
if unexpected_prefix_meshes:
    raise RuntimeError(f"Mesh names must belong to a published character prefix: {unexpected_prefix_meshes}")
expected = scene.get("lod1_mesh_count")
expected_lod0 = scene.get("lod0_mesh_count")
if expected != len(lod1) or expected_lod0 != len(lod0):
    raise RuntimeError(f"Expected matching LOD0/LOD1 meshes, got lod0={len(lod0)}, lod1={len(lod1)}, scene={expected!r}")
if expected != expected_lod0 or not isinstance(expected, int) or expected < 49:
    raise RuntimeError(f"Expected matching character LOD counts >=49, got lod0={expected_lod0!r}, lod1={expected!r}")

lod0_by_name = {obj.name: obj for obj in lod0}
lod1_by_name = {obj.name: obj for obj in lod1}
if len(lod0_by_name) != len(lod0) or len(lod1_by_name) != len(lod1):
    raise RuntimeError("LOD mesh names must be unique")
missing_pairs = [
    source.name
    for source in lod0
    if f"{source.name[:-5]}_LOD1" not in lod1_by_name
]
if missing_pairs:
    raise RuntimeError(f"LOD0 meshes missing matching LOD1 pairs: {missing_pairs}")
missing_sources = [
    obj.name
    for obj in lod1
    if obj.get("lod_source") not in lod0_by_name or obj.name != f"{obj.get('lod_source', '')[:-5]}_LOD1"
]
if missing_sources:
    raise RuntimeError(f"LOD1 meshes have invalid lod_source pairing: {missing_sources}")

collision_meshes = [obj.name for obj in mesh_objects if obj.get("collision") != "none"]
if collision_meshes:
    raise RuntimeError(f"Character kit must not publish collision meshes: {collision_meshes}")

degenerate_triangles = []
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    vertices = obj.data.vertices
    for triangle in obj.data.loop_triangles:
        a, b, c = (vertices[index].co for index in triangle.vertices)
        if (b - a).cross(c - a).length <= 1.0e-10:
            degenerate_triangles.append(f"{obj.name}[{triangle.index}]")
if degenerate_triangles:
    raise RuntimeError(f"Degenerate mesh triangles: {degenerate_triangles[:12]}")

missing_anchor = [
    prefix
    for prefix in prefixes
    if (
        scene.objects.get(f"{prefix}_Anchor") is None
        or scene.objects[f"{prefix}_Anchor"].type != "EMPTY"
        or scene.objects[f"{prefix}_Anchor"].get("anchor_policy") != "ground-origin"
        or abs(scene.objects[f"{prefix}_Anchor"].location.z) > 1.0e-6
    )
]
if missing_anchor:
    raise RuntimeError(f"Character anchors missing or invalid: {missing_anchor}")

missing_detail = [
    prefix
    for prefix in prefixes
    if not any(obj.name.startswith(f"{prefix}_Face") and obj.name.endswith("_LOD0") for obj in lod0)
]
if missing_detail:
    raise RuntimeError(f"Character face landmarks missing: {missing_detail}")

missing_human_detail = [
    prefix
    for prefix in prefixes
    if any(
        scene.objects.get(f"{prefix}_{name}") is None
        for name in ("Head_LOD0", "HeadHandLeft_LOD0", "HeadHandRight_LOD0", "ShoulderCuffLeft_LOD0", "ShoulderCuffRight_LOD0")
    )
]
if missing_human_detail:
    raise RuntimeError(f"Character silhouette detail missing: {missing_human_detail}")

detail_policy = scene.get("detail_policy")
if not isinstance(detail_policy, str) or "face landmarks" not in detail_policy or "layered clothing" not in detail_policy or "Idle/Tension" not in detail_policy:
    raise RuntimeError(f"Unexpected character detail policy: {detail_policy!r}")

armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
expected_armatures = scene.get("armature_count")
if expected_armatures != len(armatures) or expected_armatures != len(prefixes):
    raise RuntimeError(f"Expected one animation armature per prefix, got scene={expected_armatures!r}, actual={len(armatures)}")

missing_animation = []
for prefix in prefixes:
    rig = scene.objects.get(f"{prefix}_Rig")
    if (
        rig is None
        or rig.type != "ARMATURE"
        or rig.data.get("animation_clips") != "Idle,Tension"
        or rig.animation_data is None
        or rig.animation_data.action is None
        or rig.animation_data.action.name != f"{prefix}_Idle"
    ):
        missing_animation.append(f"{prefix}:rig")
        continue
    actions = {action.name for action in bpy.data.actions if action.name in {f"{prefix}_Idle", f"{prefix}_Tension"}}
    tension_strips = {
        strip.name
        for track in rig.animation_data.nla_tracks
        for strip in track.strips
        if strip.action is not None
    }
    if actions != {f"{prefix}_Idle", f"{prefix}_Tension"} or f"{prefix}_Tension" not in tension_strips:
        missing_animation.append(f"{prefix}:{sorted(actions)}")
    if prefix == "CouncilWitness":
        hand_bone = rig.data.bones.get("Hand.R")
        if hand_bone is None or hand_bone.parent is None or hand_bone.parent.name != "Arm.R":
            missing_animation.append(f"{prefix}:Hand.R must be a child of Arm.R")
        idle_action = bpy.data.actions.get(f"{prefix}_Idle")
        tension_action = bpy.data.actions.get(f"{prefix}_Tension")
        has_idle_hand_keys = idle_action is not None and any(
            'pose.bones["Hand.R"]' in curve.data_path for curve in idle_action.fcurves
        )
        has_tension_hand_keys = tension_action is not None and any(
            'pose.bones["Hand.R"]' in curve.data_path for curve in tension_action.fcurves
        )
        if not has_idle_hand_keys or not has_tension_hand_keys:
            missing_animation.append(f"{prefix}:Hand.R must be keyed in Idle and Tension")
if missing_animation:
    raise RuntimeError(f"Character animation clips missing: {missing_animation}")

for obj in mesh_objects:
    for side, bone in (("Left", "Arm.L"), ("Right", "Arm.R")):
        if f"Hand{side}" in obj.name or f"HandThumb{side}" in obj.name:
            witness_right = (
                obj.name.startswith("CouncilWitness_HeadHandRight_")
                or obj.name.startswith("CouncilWitness_HeadHandThumbRight_")
            )
            expected_bone = "Hand.R" if witness_right else bone
            if obj.parent_type != "BONE" or obj.parent_bone != expected_bone:
                raise RuntimeError(f"Hand must follow {expected_bone}: {obj.name} follows {obj.parent_bone}")

policy = scene.get("collision_policy")
if policy != "no collision meshes; Godot interaction targets and zone colliders own physics":
    raise RuntimeError(f"Unexpected collision policy: {policy!r}")

print(f"character-asset-smoke: {len(prefixes)} prefixes, {len(lod0)} LOD0/{len(lod1)} LOD1 meshes; face/clothing detail; Idle/Tension clips; no collision meshes")
