"""Verify the generated modular kit's provenance and LOD1 contract in Blender."""

import bpy


scene = bpy.context.scene
lod1 = [
    obj
    for obj in scene.objects
    if obj.type == "MESH" and "_LOD1" in obj.name
]
expected = scene.get("lod1_mesh_count")
# The rebuilt project-original kit is the 49-mesh contract documented in the
# registry and Godot's GeneratedModularKitContractSmokeTest.  Keep the exact
# count here so stale or partial Blender outputs fail closed, but do not retain
# the superseded 54-mesh expectation.
if expected != len(lod1) or expected != 49:
    raise RuntimeError(f"Expected 49 LOD1 meshes, got scene={expected!r}, actual={len(lod1)}")

missing_sources = [obj.name for obj in lod1 if not obj.get("lod_source")]
if missing_sources:
    raise RuntimeError(f"LOD1 meshes missing lod_source: {missing_sources}")

old_pc_expected = {
    "OldPc_DriveSlot_LOD0",
    "OldPc_DriveSlot_LOD1",
    "OldPc_LabelPlate_LOD0",
    "OldPc_LabelPlate_LOD1",
}
old_pc_names = {obj.name for obj in scene.objects if obj.type == "MESH" and obj.name in old_pc_expected}
if old_pc_names != old_pc_expected:
    raise RuntimeError(
        "OldPc hero-detail pairs are incomplete: "
        f"expected={sorted(old_pc_expected)!r}, actual={sorted(old_pc_names)!r}"
    )

for name in sorted(old_pc_expected):
    detail = scene.objects.get(name)
    if detail is None or detail.get("urman_asset_id") != "prop.oldpc.crt":
        raise RuntimeError(f"{name} missing prop.oldpc.crt asset id")
    if name.endswith("_LOD0") and detail.get("collision") != "none":
        raise RuntimeError(f"{name} must remain presentation-only collision=none")
    if name.endswith("_LOD1"):
        if detail.get("lod_source") != name.replace("_LOD1", "_LOD0"):
            raise RuntimeError(f"{name} has incorrect lod_source={detail.get('lod_source')!r}")
        if detail.get("collision") != "none":
            raise RuntimeError(f"{name} must remain presentation-only collision=none")

policy = scene.get("lod_policy")
if not isinstance(policy, str) or "deterministic Decimate" not in policy:
    raise RuntimeError(f"Unexpected lod_policy: {policy!r}")

detail_policy = scene.get("detail_policy")
if not isinstance(detail_policy, str) or "edge breaks" not in detail_policy or "HouseA facade" not in detail_policy or "WellA" not in detail_policy or "WoodpileA" not in detail_policy or "GateA" not in detail_policy or "four-tier" not in detail_policy or "OldPc tower" not in detail_policy or "drive-slot" not in detail_policy or "label-plate" not in detail_policy:
    raise RuntimeError(f"Unexpected detail_policy: {detail_policy!r}")

print(f"asset-lod-smoke: {len(lod1)} LOD1 meshes; policy={policy}; detail={detail_policy}")
