"""Verify the generated modular kit's provenance and LOD1 contract in Blender."""

import bpy


scene = bpy.context.scene
lod1 = [
    obj
    for obj in scene.objects
    if obj.type == "MESH" and "_LOD1" in obj.name
]
expected = scene.get("lod1_mesh_count")
legacy_lod1 = [obj for obj in lod1 if not obj.name.startswith("HouseInterior_")]
house_interior_lod1 = [obj for obj in lod1 if obj.name.startswith("HouseInterior_")]
# Keep the existing 49-mesh environment contract fail-closed while adding the
# authored 84-mesh interior family and its restrained lived-in clusters.
if expected != len(lod1) or expected != 133 or len(legacy_lod1) != 49 or len(house_interior_lod1) != 84:
    raise RuntimeError(
        "Expected 133 LOD1 meshes (49 legacy + 84 HouseInterior), "
        f"got scene={expected!r}, actual={len(lod1)}, "
        f"legacy={len(legacy_lod1)}, house_interior={len(house_interior_lod1)}"
    )

legacy_family_counts = {
    "HouseA_": 12,
    "FenceA_": 6,
    "RoadDirt_": 1,
    "PineA_": 2,
    "TableA_": 5,
    "OldPc_": 8,
    "WellA_": 7,
    "WoodpileA_": 4,
    "GateA_": 4,
}
for prefix, count in legacy_family_counts.items():
    actual = sum(1 for obj in legacy_lod1 if obj.name.startswith(prefix))
    if actual != count:
        raise RuntimeError(f"Legacy {prefix} LOD1 contract changed: expected={count}, actual={actual}")

house_interior_required = {
    *(f"HouseInterior_FloorBoard_{index:02d}" for index in range(6)),
    "HouseInterior_BaseTrimBack",
    "HouseInterior_BaseTrimFront",
    "HouseInterior_CeilingField",
    "HouseInterior_CeilingBeamLeft",
    "HouseInterior_CeilingBeamRight",
    "HouseInterior_BackWall",
    "HouseInterior_LeftWall",
    "HouseInterior_RightWall",
    "HouseInterior_FrontWallLeft",
    "HouseInterior_FrontWallRight",
    "HouseInterior_FrontWallLintel",
    "HouseInterior_EntryDoor",
    "HouseInterior_EntryFrameLeft",
    "HouseInterior_EntryFrameRight",
    "HouseInterior_EntryFrameTop",
    "HouseInterior_EntryThreshold",
    "HouseInterior_WindowRecess",
    "HouseInterior_WindowGlass",
    "HouseInterior_WindowFrameLeft",
    "HouseInterior_WindowFrameRight",
    "HouseInterior_WindowFrameTop",
    "HouseInterior_WindowFrameBottom",
    "HouseInterior_WindowMuntin",
    "HouseInterior_WindowSill",
    "HouseInterior_TableTop",
    "HouseInterior_TableLegLeft",
    "HouseInterior_TableLegRight",
    "HouseInterior_TableApron",
    "HouseInterior_ChairSeat",
    "HouseInterior_ChairBack",
    "HouseInterior_ChairLegLeft",
    "HouseInterior_ChairLegRight",
    "HouseInterior_CupboardBody",
    "HouseInterior_CupboardDoor",
    "HouseInterior_RugField",
    "HouseInterior_RugBandA",
    "HouseInterior_RugBandB",
    "HouseInterior_DaybedFrame",
    "HouseInterior_DaybedCushion",
    "HouseInterior_DaybedBack",
    "HouseInterior_StorageChestBody",
    "HouseInterior_StorageChestLid",
    "HouseInterior_StorageChestFront",
    "HouseInterior_RightShelfBoard",
    "HouseInterior_RightShelfBack",
    "HouseInterior_ShelfVesselA",
    "HouseInterior_ShelfVesselB",
    "HouseInterior_ShelfVesselC",
    "HouseInterior_RightRunnerField",
    "HouseInterior_RightRunnerBand",
    "HouseInterior_RightRunnerHem",
    "HouseInterior_HearthBase",
    "HouseInterior_HearthBody",
    "HouseInterior_HearthTop",
    "HouseInterior_HearthDoor",
    "HouseInterior_HearthHandle",
    "HouseInterior_HearthFlue",
    "HouseInterior_HearthFlueCollar",
    "HouseInterior_LeftWallCupboardBody",
    "HouseInterior_LeftWallCupboardDoor",
    "HouseInterior_LeftWallCupboardTop",
    "HouseInterior_TableTray",
    "HouseInterior_TableKettle",
    "HouseInterior_TableKettleLid",
    "HouseInterior_TableBowl",
    "HouseInterior_StorageBasketBody",
    "HouseInterior_StorageBasketRim",
    "HouseInterior_StorageBasketHandle",
    "HouseInterior_OldPcBackboard",
    "HouseInterior_OldPcHutchCleatLeft",
    "HouseInterior_OldPcHutchCleatRight",
    "HouseInterior_OldPcHutchShelf",
    "HouseInterior_OldPcHutchTop",
    "HouseInterior_OldPcHutchFolder",
    "HouseInterior_OldPcDocumentFolio",
    "HouseInterior_LeftWallWainscotField",
    "HouseInterior_LeftWallWainscotRail",
    "HouseInterior_TableFootRail",
}
house_interior_edge_required = {
    "HouseInterior_DaybedFrame",
    "HouseInterior_DaybedCushion",
    "HouseInterior_DaybedBack",
    "HouseInterior_StorageChestBody",
    "HouseInterior_StorageChestLid",
    "HouseInterior_StorageChestFront",
    "HouseInterior_RightShelfBoard",
    "HouseInterior_RightShelfBack",
    "HouseInterior_ShelfVesselA",
    "HouseInterior_ShelfVesselB",
    "HouseInterior_ShelfVesselC",
    "HouseInterior_RightRunnerField",
    "HouseInterior_RightRunnerBand",
    "HouseInterior_RightRunnerHem",
}
house_interior_lived_in_required = {
    "HouseInterior_HearthBase",
    "HouseInterior_HearthBody",
    "HouseInterior_HearthTop",
    "HouseInterior_HearthDoor",
    "HouseInterior_HearthHandle",
    "HouseInterior_HearthFlue",
    "HouseInterior_HearthFlueCollar",
    "HouseInterior_LeftWallCupboardBody",
    "HouseInterior_LeftWallCupboardDoor",
    "HouseInterior_LeftWallCupboardTop",
    "HouseInterior_TableTray",
    "HouseInterior_TableKettle",
    "HouseInterior_TableKettleLid",
    "HouseInterior_TableBowl",
    "HouseInterior_StorageBasketBody",
    "HouseInterior_StorageBasketRim",
    "HouseInterior_StorageBasketHandle",
}
house_interior_hero_required = {
    "HouseInterior_OldPcBackboard",
    "HouseInterior_OldPcHutchCleatLeft",
    "HouseInterior_OldPcHutchCleatRight",
    "HouseInterior_OldPcHutchShelf",
    "HouseInterior_OldPcHutchTop",
    "HouseInterior_OldPcHutchFolder",
    "HouseInterior_OldPcDocumentFolio",
    "HouseInterior_LeftWallWainscotField",
    "HouseInterior_LeftWallWainscotRail",
    "HouseInterior_TableFootRail",
}
house_interior_focus_groups = {
    "old-pc-document": {
        "HouseInterior_OldPcBackboard",
        "HouseInterior_OldPcHutchCleatLeft",
        "HouseInterior_OldPcHutchCleatRight",
        "HouseInterior_OldPcHutchShelf",
        "HouseInterior_OldPcHutchTop",
        "HouseInterior_OldPcHutchFolder",
        "HouseInterior_OldPcDocumentFolio",
    },
    "hearth-anchor": {
        "HouseInterior_HearthBase",
        "HouseInterior_HearthBody",
        "HouseInterior_HearthTop",
        "HouseInterior_HearthDoor",
        "HouseInterior_HearthFlue",
    },
    "domestic-cluster": {
        "HouseInterior_DaybedFrame",
        "HouseInterior_DaybedCushion",
        "HouseInterior_DaybedBack",
        "HouseInterior_StorageChestBody",
        "HouseInterior_StorageChestLid",
        "HouseInterior_StorageChestFront",
        "HouseInterior_TableTray",
        "HouseInterior_TableKettle",
        "HouseInterior_TableBowl",
    },
    "architectural-framing": {
        "HouseInterior_BaseTrimFront",
        "HouseInterior_BaseTrimBack",
        "HouseInterior_CeilingBeamLeft",
        "HouseInterior_CeilingBeamRight",
        "HouseInterior_EntryFrameLeft",
        "HouseInterior_EntryFrameRight",
        "HouseInterior_EntryFrameTop",
        "HouseInterior_WindowFrameLeft",
        "HouseInterior_WindowFrameRight",
        "HouseInterior_WindowFrameTop",
        "HouseInterior_WindowFrameBottom",
        "HouseInterior_WindowSill",
    },
}
house_interior_lod0 = [
    obj
    for obj in scene.objects
    if obj.type == "MESH" and obj.name.startswith("HouseInterior_") and "_LOD0" in obj.name
]
if scene.get("house_interior_lod0_count") != 84 or len(house_interior_lod0) != 84:
    raise RuntimeError(
        "Expected 84 HouseInterior LOD0 meshes, "
        f"got scene={scene.get('house_interior_lod0_count')!r}, actual={len(house_interior_lod0)}"
    )
actual_house_interior = {obj.name.replace("_LOD0", "") for obj in house_interior_lod0}
if actual_house_interior != house_interior_required:
    raise RuntimeError(
        "HouseInterior required mesh set changed: "
        f"missing={sorted(house_interior_required - actual_house_interior)!r}, "
        f"unexpected={sorted(actual_house_interior - house_interior_required)!r}"
    )
for group_name, required in house_interior_focus_groups.items():
    missing = required - actual_house_interior
    if missing:
        raise RuntimeError(f"HouseInterior {group_name} semantic contract missing={sorted(missing)!r}")
actual_house_interior_edge = actual_house_interior & house_interior_edge_required
if scene.get("house_interior_edge_lod0_count") != 14 or actual_house_interior_edge != house_interior_edge_required:
    raise RuntimeError(
        "HouseInterior edge-cluster contract changed: "
        f"expected=14, metadata={scene.get('house_interior_edge_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_edge)!r}"
    )
actual_house_interior_lived_in = actual_house_interior & house_interior_lived_in_required
if scene.get("house_interior_lived_in_lod0_count") != 17 or actual_house_interior_lived_in != house_interior_lived_in_required:
    raise RuntimeError(
        "HouseInterior lived-in cluster contract changed: "
        f"expected=17, metadata={scene.get('house_interior_lived_in_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_lived_in)!r}"
    )
actual_house_interior_hero = actual_house_interior & house_interior_hero_required
if scene.get("house_interior_hero_lod0_count") != 10 or actual_house_interior_hero != house_interior_hero_required:
    raise RuntimeError(
        "HouseInterior hero joinery contract changed: "
        f"expected=10, metadata={scene.get('house_interior_hero_lod0_count')!r}, "
        f"actual={sorted(actual_house_interior_hero)!r}"
    )
for obj in house_interior_lod0:
    if obj.get("urman_asset_id") != "env.house.interior.a" or obj.get("collision") != "none":
        raise RuntimeError(f"{obj.name} must be presentation-only env.house.interior.a geometry")

for obj in house_interior_lod1:
    if obj.get("urman_asset_id") != "env.house.interior.a" or obj.get("collision") != "none":
        raise RuntimeError(f"{obj.name} must be presentation-only env.house.interior.a geometry")
    if obj.get("lod_source") != obj.name.replace("_LOD1", "_LOD0"):
        raise RuntimeError(f"{obj.name} has incorrect lod_source={obj.get('lod_source')!r}")

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
if not isinstance(detail_policy, str) or "edge breaks" not in detail_policy or "HouseA facade" not in detail_policy or "HouseInterior" not in detail_policy or "threshold/window framing" not in detail_policy or "tapered furniture silhouettes" not in detail_policy or "hearth/cupboard/table/storage cluster" not in detail_policy or "WellA" not in detail_policy or "WoodpileA" not in detail_policy or "GateA" not in detail_policy or "four-tier" not in detail_policy or "OldPc tower" not in detail_policy or "drive-slot" not in detail_policy or "label-plate" not in detail_policy:
    raise RuntimeError(f"Unexpected detail_policy: {detail_policy!r}")

print(f"asset-lod-smoke: {len(lod1)} LOD1 meshes; policy={policy}; detail={detail_policy}")
