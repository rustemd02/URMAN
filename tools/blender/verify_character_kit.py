"""Verify the generated character kit's provenance and LOD contract."""

import bpy


scene = bpy.context.scene
prefixes = [item for item in str(scene.get("character_prefixes", "")).split(",") if item]
if len(prefixes) != 9:
    raise RuntimeError(f"Expected 9 character prefixes, got {prefixes!r}")

lod0 = [obj for obj in scene.objects if obj.type == "MESH" and "_LOD0" in obj.name]
lod1 = [obj for obj in scene.objects if obj.type == "MESH" and "_LOD1" in obj.name]
expected = scene.get("lod1_mesh_count")
expected_lod0 = scene.get("lod0_mesh_count")
if expected != len(lod1) or expected_lod0 != len(lod0):
    raise RuntimeError(f"Expected matching LOD0/LOD1 meshes, got lod0={len(lod0)}, lod1={len(lod1)}, scene={expected!r}")
if expected != expected_lod0 or not isinstance(expected, int) or expected < 49:
    raise RuntimeError(f"Expected matching character LOD counts >=49, got lod0={expected_lod0!r}, lod1={expected!r}")

missing_sources = [obj.name for obj in lod1 if not obj.get("lod_source")]
if missing_sources:
    raise RuntimeError(f"LOD1 meshes missing lod_source: {missing_sources}")

missing_anchor = [prefix for prefix in prefixes if scene.objects.get(f"{prefix}_Anchor") is None]
if missing_anchor:
    raise RuntimeError(f"Character anchors missing: {missing_anchor}")

missing_detail = [
    prefix
    for prefix in prefixes
    if not any(obj.name.startswith(f"{prefix}_Face") and "_LOD0" in obj.name for obj in lod0)
]
if missing_detail:
    raise RuntimeError(f"Character face landmarks missing: {missing_detail}")

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
    if rig is None or rig.type != "ARMATURE":
        missing_animation.append(f"{prefix}:rig")
        continue
    actions = {action.name for action in bpy.data.actions if action.name in {f"{prefix}_Idle", f"{prefix}_Tension"}}
    if actions != {f"{prefix}_Idle", f"{prefix}_Tension"}:
        missing_animation.append(f"{prefix}:{sorted(actions)}")
if missing_animation:
    raise RuntimeError(f"Character animation clips missing: {missing_animation}")

policy = scene.get("collision_policy")
if policy != "no collision meshes; Godot interaction targets and zone colliders own physics":
    raise RuntimeError(f"Unexpected collision policy: {policy!r}")

print(f"character-asset-smoke: {len(prefixes)} prefixes, {len(lod0)} LOD0/{len(lod1)} LOD1 meshes; face/clothing detail; Idle/Tension clips; no collision meshes")
