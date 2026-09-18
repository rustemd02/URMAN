"""Rebuild the authored Act I FAP clinic kit.

The source blend is the contract baseline.  This pass replaces only mesh data
under the three architectural roots and extends the kit with one direct
presentation-only interior root, keeping every existing FAP child name intact.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/act1/urman_fap_clinic_kit.py -- --root <repo>
"""

from __future__ import annotations

import argparse
import bmesh
import math
import sys
from pathlib import Path

import bpy


ROOT_NAME = "URMAN_FapClinicKit"
GLB_NAME = "urman_fap_clinic_kit.glb"
FACADE = "FapFacade_Main"
PORCH = "FapEntryPorch"
SHED = "FapServiceShed"
INTERIOR = "FapInteriorSet"

FACADE_CHILDREN = (
    "FapFacade_BackCornerPost_L_LOD0",
    "FapFacade_BackCornerPost_R_LOD0",
    "FapFacade_BackCourse_LOD0",
    "FapFacade_Body_LOD0",
    "FapFacade_CornerPost_L_LOD0",
    "FapFacade_CornerPost_R_LOD0",
    "FapFacade_DoorFrameLeft_LOD0",
    "FapFacade_DoorFrameRight_LOD0",
    "FapFacade_DoorFrameTop_LOD0",
    "FapFacade_DoorHandle_LOD0",
    "FapFacade_DoorPanelInset_LOD0",
    "FapFacade_DoorPanel_LOD0",
    "FapFacade_Foundation_LOD0",
    "FapFacade_FrontCourse_LOD0",
    "FapFacade_FrontPlinth_LOD0",
    "FapFacade_GableVent_LOD0",
    "FapFacade_Gable_LOD0",
    "FapFacade_RidgeCap_LOD0",
    "FapFacade_RoofEdgeBack_L_LOD0",
    "FapFacade_RoofEdgeBack_R_LOD0",
    "FapFacade_RoofEdge_L_LOD0",
    "FapFacade_RoofEdge_R_LOD0",
    "FapFacade_Roof_LOD0",
    "FapFacade_ServiceDoorFrame_LOD0",
    "FapFacade_ServiceDoor_LOD0",
    "FapFacade_SideBand_0_7_LOD0",
    "FapFacade_SideBand_1_95_LOD0",
    "FapFacade_WindowLeft_Glass_LOD0",
    "FapFacade_WindowLeft_MuntinHorizontal_LOD0",
    "FapFacade_WindowLeft_MuntinVertical_LOD0",
    "FapFacade_WindowLeft_TrimBottom_LOD0",
    "FapFacade_WindowLeft_TrimLeft_LOD0",
    "FapFacade_WindowLeft_TrimRight_LOD0",
    "FapFacade_WindowLeft_TrimTop_LOD0",
    "FapFacade_WindowRight_Glass_LOD0",
    "FapFacade_WindowRight_MuntinHorizontal_LOD0",
    "FapFacade_WindowRight_MuntinVertical_LOD0",
    "FapFacade_WindowRight_TrimBottom_LOD0",
    "FapFacade_WindowRight_TrimLeft_LOD0",
    "FapFacade_WindowRight_TrimRight_LOD0",
    "FapFacade_WindowRight_TrimTop_LOD0",
    "FapFacade_WindowRight_WarmInset_LOD0",
)

PORCH_CHILDREN = (
    "FapEntryPorch_BraceLeft_LOD0",
    "FapEntryPorch_BraceRight_LOD0",
    "FapEntryPorch_CanopyFascia_LOD0",
    "FapEntryPorch_Deck_LOD0",
    "FapEntryPorch_DoorHeaderPlaque_LOD0",
    "FapEntryPorch_PostCap_L_LOD0",
    "FapEntryPorch_PostCap_R_LOD0",
    "FapEntryPorch_Post_L_LOD0",
    "FapEntryPorch_Post_R_LOD0",
    "FapEntryPorch_RailPost_L_LOD0",
    "FapEntryPorch_RailPost_R_LOD0",
    "FapEntryPorch_Rail_L_LOD0",
    "FapEntryPorch_Rail_R_LOD0",
    "FapEntryPorch_RainCanopy_LOD0",
    "FapEntryPorch_Step_00_LOD0",
    "FapEntryPorch_Step_01_LOD0",
    "FapEntryPorch_Step_02_LOD0",
    "FapEntryPorch_Threshold_LOD0",
)

SHED_CHILDREN = (
    "FapServiceShed_Body_LOD0",
    "FapServiceShed_DoorFrameTop_LOD0",
    "FapServiceShed_DoorFrame_L_LOD0",
    "FapServiceShed_DoorFrame_R_LOD0",
    "FapServiceShed_Door_LOD0",
    "FapServiceShed_Foundation_LOD0",
    "FapServiceShed_FrontBatten_-0_42_LOD0",
    "FapServiceShed_FrontBatten_-1_25_LOD0",
    "FapServiceShed_FrontBatten_0_42_LOD0",
    "FapServiceShed_FrontBatten_1_25_LOD0",
    "FapServiceShed_RoofEdge_L_LOD0",
    "FapServiceShed_RoofEdge_R_LOD0",
    "FapServiceShed_Roof_LOD0",
    "FapServiceShed_VentSlat_2_06_LOD0",
    "FapServiceShed_VentSlat_2_15_LOD0",
    "FapServiceShed_VentSlat_2_24_LOD0",
    "FapServiceShed_Vent_LOD0",
)

INTERIOR_BASELINE_CHILDREN = (
    "FapInteriorCot_Frame_LOD0",
    "FapInteriorCot_Mattress_LOD0",
    "FapInteriorCot_Pillow_LOD0",
    "FapInteriorCot_HeadRail_LOD0",
    "FapInteriorCot_LegFrontLeft_LOD0",
    "FapInteriorCot_LegFrontRight_LOD0",
    "FapInteriorCot_LegBackLeft_LOD0",
    "FapInteriorCot_LegBackRight_LOD0",
    "FapInteriorCot_LowerBrace_LOD0",
    "FapInteriorScreen_PanelLeft_LOD0",
    "FapInteriorScreen_PanelCenter_LOD0",
    "FapInteriorScreen_PanelRight_LOD0",
    "FapInteriorScreen_PostLeft_LOD0",
    "FapInteriorScreen_PostCenter_LOD0",
    "FapInteriorScreen_PostRight_LOD0",
    "FapInteriorScreen_RailTopLeft_LOD0",
    "FapInteriorScreen_RailBottomLeft_LOD0",
    "FapInteriorScreen_RailTopCenter_LOD0",
    "FapInteriorScreen_RailBottomCenter_LOD0",
    "FapInteriorScreen_RailTopRight_LOD0",
    "FapInteriorScreen_RailBottomRight_LOD0",
    "FapInteriorCabinet_Body_LOD0",
    "FapInteriorCabinet_DoorLeft_LOD0",
    "FapInteriorCabinet_DoorRight_LOD0",
    "FapInteriorCabinet_InsetLeft_LOD0",
    "FapInteriorCabinet_InsetRight_LOD0",
    "FapInteriorCabinet_HandleLeft_LOD0",
    "FapInteriorCabinet_HandleRight_LOD0",
    "FapInteriorCabinet_TopShelf_LOD0",
    "FapInteriorTrolley_TopTray_LOD0",
    "FapInteriorTrolley_Basin_LOD0",
    "FapInteriorTrolley_BasinInset_LOD0",
    "FapInteriorTrolley_LowerShelf_LOD0",
    "FapInteriorTrolley_LegFrontLeft_LOD0",
    "FapInteriorTrolley_LegFrontRight_LOD0",
    "FapInteriorTrolley_LegBackLeft_LOD0",
    "FapInteriorTrolley_LegBackRight_LOD0",
    "FapInteriorTrolley_Handle_LOD0",
    "FapInteriorTrolley_WheelFrontLeft_LOD0",
    "FapInteriorTrolley_WheelFrontRight_LOD0",
    "FapInteriorTrolley_WheelBackLeft_LOD0",
    "FapInteriorTrolley_WheelBackRight_LOD0",
    "FapInteriorBench_Seat_LOD0",
    "FapInteriorBench_Back_LOD0",
    "FapInteriorBench_ArmLeft_LOD0",
    "FapInteriorBench_ArmRight_LOD0",
    "FapInteriorBench_LegFrontLeft_LOD0",
    "FapInteriorBench_LegFrontRight_LOD0",
    "FapInteriorBench_LegBackLeft_LOD0",
    "FapInteriorBench_LegBackRight_LOD0",
)

INTERIOR_ADDED_CHILDREN = (
    "FapInteriorRadiator_Body_LOD0",
    "FapInteriorRadiator_RibLeft_LOD0",
    "FapInteriorRadiator_RibCenter_LOD0",
    "FapInteriorRadiator_RibRight_LOD0",
    "FapInteriorRadiator_PipeLeft_LOD0",
    "FapInteriorRadiator_PipeRight_LOD0",
    "FapInteriorSupplyShelf_Low_LOD0",
    "FapInteriorSupplyShelf_Mid_LOD0",
    "FapInteriorSupplyShelf_Top_LOD0",
    "FapInteriorSupplyShelf_SideLeft_LOD0",
    "FapInteriorSupplyShelf_SideRight_LOD0",
    "FapInteriorSupplyBottle_A_LOD0",
    "FapInteriorSupplyBottle_B_LOD0",
    "FapInteriorSupplyTin_C_LOD0",
    "FapInteriorSupplyBottle_D_LOD0",
    "FapInteriorSupplyBottle_E_LOD0",
    "FapInteriorExamChart_Panel_LOD0",
    "FapInteriorExamChart_FrameTop_LOD0",
    "FapInteriorExamChart_FrameBottom_LOD0",
    "FapInteriorExamChart_FrameLeft_LOD0",
    "FapInteriorExamChart_FrameRight_LOD0",
    "FapInteriorCoatHookRail_LOD0",
    "FapInteriorCoatHookLeft_LOD0",
    "FapInteriorCoatHookCenter_LOD0",
    "FapInteriorCoatHookRight_LOD0",
)

INTERIOR_SHELL_CHILDREN = (
    "FapInteriorShell_Floor_LOD0",
    "FapInteriorShell_Ceiling_LOD0",
    "FapInteriorShell_BackWall_LOD0",
    "FapInteriorShell_LeftWall_LOD0",
    "FapInteriorShell_RightWall_LOD0",
    "FapInteriorShell_FrontWallLeft_LOD0",
    "FapInteriorShell_FrontWallRight_LOD0",
    "FapInteriorShell_FrontWallHeader_LOD0",
    "FapInteriorShell_DoorInset_LOD0",
    "FapInteriorShell_DoorFrameLeft_LOD0",
    "FapInteriorShell_DoorFrameRight_LOD0",
    "FapInteriorShell_DoorFrameTop_LOD0",
    "FapInteriorShell_DoorHandle_LOD0",
    "FapInteriorShell_Threshold_LOD0",
    "FapInteriorShell_LowerBandBack_LOD0",
    "FapInteriorShell_LowerBandLeft_LOD0",
    "FapInteriorShell_LowerBandRight_LOD0",
    "FapInteriorShell_LowerBandFrontLeft_LOD0",
    "FapInteriorShell_LowerBandFrontRight_LOD0",
    "FapInteriorShell_WindowLeftGlass_LOD0",
    "FapInteriorShell_WindowLeftTrimTop_LOD0",
    "FapInteriorShell_WindowLeftTrimBottom_LOD0",
    "FapInteriorShell_WindowLeftTrimFront_LOD0",
    "FapInteriorShell_WindowLeftTrimBack_LOD0",
    "FapInteriorShell_WindowRightGlass_LOD0",
    "FapInteriorShell_WindowRightTrimTop_LOD0",
    "FapInteriorShell_WindowRightTrimBottom_LOD0",
    "FapInteriorShell_WindowRightTrimFront_LOD0",
    "FapInteriorShell_WindowRightTrimBack_LOD0",
    "FapInteriorShell_CeilingFixtureHousing_LOD0",
    "FapInteriorShell_CeilingFixtureLens_LOD0",
)

# A small second-pass furnishing cluster makes the clinic read as a lived-in
# rural medical point from the reverse and side turns.  These are deliberately
# neutral silhouettes: no logos, signs, diagnostic symbols or cultural marks.
INTERIOR_DETAIL_LOD0_CHILDREN = (
    "FapInteriorWashUnit_Body_LOD0",
    "FapInteriorWashUnit_Basin_LOD0",
    "FapInteriorWashUnit_BasinInset_LOD0",
    "FapInteriorWashUnit_Splash_LOD0",
    "FapInteriorWashUnit_TowelRail_LOD0",
    "FapInteriorWashUnit_Towel_LOD0",
    "FapInteriorStool_Seat_LOD0",
    "FapInteriorStool_Stem_LOD0",
    "FapInteriorStool_Base_LOD0",
    "FapInteriorStool_FootRing_LOD0",
    "FapInteriorNoticeBoard_Panel_LOD0",
    "FapInteriorNoticeBoard_Rail_LOD0",
    "FapInteriorNoticeBoard_CardA_LOD0",
    "FapInteriorNoticeBoard_CardB_LOD0",
    # Wave17 composition anchors: broad floor zones and medium-scale joinery
    # make the existing room volumes read from entry, side and reverse turns.
    "FapInteriorFloor_EntryRunner_LOD0",
    "FapInteriorFloor_ExamMat_LOD0",
    "FapInteriorFloor_WaitingMat_LOD0",
    "FapInteriorFloor_RecordsMat_LOD0",
    "FapInteriorCeilingBeam_Entry_LOD0",
    "FapInteriorCot_Backboard_LOD0",
    "FapInteriorCot_InstrumentShelf_LOD0",
    "FapInteriorBench_BackRail_LOD0",
    "FapInteriorRecordsDesk_CubbyBody_LOD0",
    "FapInteriorRecordsDesk_CubbyShelfLow_LOD0",
    "FapInteriorRecordsDesk_CubbyShelfHigh_LOD0",
    "FapInteriorReceptionCounter_ServiceShelf_LOD0",
    "FapInteriorWallPanel_LeftEntry_LOD0",
    "FapInteriorWallPanel_RightEntry_LOD0",
)

INTERIOR_DETAIL_LOD1_CHILDREN = tuple(
    name.replace("_LOD0", "_LOD1") for name in INTERIOR_DETAIL_LOD0_CHILDREN
)

# Three larger neutral masses close the otherwise open front/side sightlines:
# a low reception counter, a tall storage cabinet and a partial-height zoning
# partition. They are visual-only and leave the center circulation lane open.
INTERIOR_ZONING_CHILDREN = (
    "FapInteriorReceptionCounter_Body_LOD0",
    "FapInteriorReceptionCounter_Top_LOD0",
    "FapInteriorReceptionCounter_Base_LOD0",
    "FapInteriorTallStorage_Body_LOD0",
    "FapInteriorTallStorage_DoorLeft_LOD0",
    "FapInteriorTallStorage_DoorRight_LOD0",
    "FapInteriorTallStorage_TopCap_LOD0",
    "FapInteriorTallStorage_HandleLeft_LOD0",
    "FapInteriorTallStorage_HandleRight_LOD0",
    "FapInteriorPartition_Panel_LOD0",
    "FapInteriorPartition_PostFront_LOD0",
    "FapInteriorPartition_PostBack_LOD0",
    "FapInteriorPartition_RailTop_LOD0",
)

# Wave-6 construction pass: the shell and furniture remain one direct
# presentation component, but gain a small amount of joined trim and joinery
# so the room reads as built space rather than a row of independent boxes.
INTERIOR_ARCHITECTURAL_CHILDREN = (
    "FapInteriorShell_BaseCapBack_LOD0",
    "FapInteriorShell_BaseCapLeft_LOD0",
    "FapInteriorShell_BaseCapRight_LOD0",
    "FapInteriorShell_BaseCapFrontLeft_LOD0",
    "FapInteriorShell_BaseCapFrontRight_LOD0",
    "FapInteriorShell_CeilingCorniceBack_LOD0",
    "FapInteriorShell_CeilingCorniceLeft_LOD0",
    "FapInteriorShell_CeilingCorniceRight_LOD0",
    "FapInteriorShell_CeilingCorniceFrontLeft_LOD0",
    "FapInteriorShell_CeilingCorniceFrontRight_LOD0",
    "FapInteriorShell_CornerPostBackLeft_LOD0",
    "FapInteriorShell_CornerPostBackRight_LOD0",
    "FapInteriorShell_CornerPostFrontLeft_LOD0",
    "FapInteriorShell_CornerPostFrontRight_LOD0",
    "FapInteriorShell_RevealLeftFront_LOD0",
    "FapInteriorShell_RevealLeftBack_LOD0",
    "FapInteriorShell_RevealRightFront_LOD0",
    "FapInteriorShell_RevealRightBack_LOD0",
    "FapInteriorShell_RevealLeftSill_LOD0",
    "FapInteriorShell_RevealRightSill_LOD0",
    "FapInteriorShell_EntryMat_LOD0",
)

INTERIOR_RECORDS_CHILDREN = (
    "FapInteriorRecordsDesk_Body_LOD0",
    "FapInteriorRecordsDesk_Top_LOD0",
    "FapInteriorRecordsDesk_Base_LOD0",
    "FapInteriorRecordsDesk_FrontApron_LOD0",
    "FapInteriorRecordsDesk_DrawerBand_LOD0",
    "FapInteriorRecordsDesk_FrontLegLeft_LOD0",
    "FapInteriorRecordsDesk_FrontLegRight_LOD0",
    "FapInteriorRecordsDesk_BackRail_LOD0",
    "FapInteriorRecordsDesk_RecordTray_LOD0",
    "FapInteriorRecordsDesk_PaperStack_LOD0",
)

INTERIOR_JOINERY_CHILDREN = (
    "FapInteriorReceptionCounter_EndPanelLeft_LOD0",
    "FapInteriorReceptionCounter_EndPanelRight_LOD0",
    "FapInteriorReceptionCounter_KickPanel_LOD0",
    "FapInteriorReceptionCounter_DrawerBand_LOD0",
    "FapInteriorBench_LowerBrace_LOD0",
    "FapInteriorBench_FootRail_LOD0",
    "FapInteriorCot_FootRail_LOD0",
    "FapInteriorCot_UnderShelf_LOD0",
    "FapInteriorWashUnit_FaucetStem_LOD0",
    "FapInteriorWashUnit_FaucetSpout_LOD0",
    "FapInteriorTallStorage_Base_LOD0",
    "FapInteriorSupplyShelf_BackPanel_LOD0",
    "FapInteriorSupplyShelf_FrontLipLow_LOD0",
    "FapInteriorSupplyShelf_FrontLipMid_LOD0",
    "FapInteriorSupplyShelf_FrontLipTop_LOD0",
    "FapInteriorNoticeBoard_RailBottom_LOD0",
    "FapInteriorNoticeBoard_RailLeft_LOD0",
    "FapInteriorNoticeBoard_RailRight_LOD0",
    "FapInteriorPartition_RailBottom_LOD0",
)

INTERIOR_CHILDREN = (
    INTERIOR_BASELINE_CHILDREN
    + INTERIOR_ADDED_CHILDREN
    + INTERIOR_SHELL_CHILDREN
    + INTERIOR_DETAIL_LOD0_CHILDREN
    + INTERIOR_ZONING_CHILDREN
    + INTERIOR_ARCHITECTURAL_CHILDREN
    + INTERIOR_RECORDS_CHILDREN
    + INTERIOR_JOINERY_CHILDREN
)

# The room keeps its published furnishing coordinates and metric scale. The
# outer shell now shares its floor, wall planes and openings with that room.
FAP_ENVELOPE = {"outer_half": 5.99, "inner_half": 5.81, "floor": 0.40,
                "wall_top": 3.81, "door_z": 5.70, "side_window_z": -0.90}
FACADE_ENVELOPE_CHILDREN = tuple(
    f"FapFacade_SideWindow{side}_{part}_LOD0"
    for side in ("Left", "Right")
    for part in ("Glass", "TrimBottom", "TrimTop", "TrimLeft", "TrimRight", "MuntinHorizontal", "MuntinVertical")
)
INTERIOR_ENVELOPE_CHILDREN = tuple(
    f"FapInteriorShell_WindowFront{side}{part}_LOD0"
    for side in ("Left", "Right")
    for part in ("Glass", "TrimBottom", "TrimTop", "TrimLeft", "TrimRight", "MuntinHorizontal", "MuntinVertical")
) + tuple(f"FapInteriorShell_ServiceDoor{part}_LOD0" for part in ("Panel", "JambFront", "JambBack", "Header", "Handle")) \
  + ("FapInteriorWashUnit_FootFront_LOD0", "FapInteriorWashUnit_FootBack_LOD0")
INTERIOR_CHILDREN += INTERIOR_ENVELOPE_CHILDREN


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str) -> bpy.types.Material:
    result = bpy.data.materials.get(name)
    if result is None:
        raise RuntimeError(f"Missing baseline material: {name}")
    return result


def tune_painterly_materials() -> None:
    """Keep the fixed material set, but separate aged plaster, timber and metal."""
    palette = {
        "FapPaintedSage": ((0.46, 0.53, 0.48, 1.0), 0.92),
        "FapPaintedDustyBlue": ((0.34, 0.44, 0.49, 1.0), 0.88),
        "FapPaintedTimber": ((0.40, 0.32, 0.23, 1.0), 0.90),
        "FapDarkTimber": ((0.23, 0.19, 0.14, 1.0), 0.94),
        "FapShedWall": ((0.48, 0.40, 0.30, 1.0), 0.91),
        "FapDoorWood": ((0.37, 0.27, 0.19, 1.0), 0.88),
        "FapDoorInset": ((0.24, 0.18, 0.14, 1.0), 0.92),
        "FapNoticeBlank": ((0.63, 0.56, 0.43, 1.0), 0.95),
        "FapRainMetal": ((0.44, 0.52, 0.50, 1.0), 0.64),
        "FapRainMetalDark": ((0.24, 0.30, 0.29, 1.0), 0.74),
        "FapWetStone": ((0.36, 0.41, 0.39, 1.0), 0.87),
        "FapWindowCool": ((0.24, 0.39, 0.45, 1.0), 0.46),
        "FapWindowWarm": ((0.64, 0.45, 0.23, 1.0), 0.60),
    }
    for name, (color, roughness) in palette.items():
        target = material(name)
        target.diffuse_color = color
        target.use_nodes = True
        for node in target.node_tree.nodes:
            if node.type != "BSDF_PRINCIPLED":
                continue
            node.inputs["Base Color"].default_value = color
            node.inputs["Roughness"].default_value = roughness


def triangulate_mesh_data(mesh: bpy.types.Mesh) -> None:
    """Turn authored n-gons into deterministic triangles before export."""
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        if any(len(face.verts) > 3 for face in bm.faces):
            bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
            bm.to_mesh(mesh)
    finally:
        bm.free()
    mesh.validate(verbose=False, clean_customdata=False)
    mesh.update()
    mesh.calc_loop_triangles()


def replace_puddle_mesh(obj: bpy.types.Object, center: tuple[float, float], radii: tuple[float, float], phase: float) -> None:
    """Replace the old fan-prone puddle shell with a shallow triangulated solid."""
    cx, cy = center
    rx, ry = radii
    count = 10
    outer: list[tuple[float, float, float]] = []
    for index in range(count):
        angle = 2.0 * math.pi * index / count + phase
        scale = 1.0 + (0.08 * math.sin(index * 2.3 + phase))
        outer.append((cx + math.cos(angle) * rx * scale, cy + math.sin(angle) * ry * scale, 0.004 + 0.0015 * math.sin(angle * 2.0)))
    vertices = outer + [(cx + 0.045 * rx, cy - 0.035 * ry, 0.008)]
    vertices.extend((x, y, -0.008) for x, y, _ in outer)
    vertices.append((cx + 0.045 * rx, cy - 0.035 * ry, -0.008))
    top_center = count
    bottom_start = count + 1
    bottom_center = bottom_start + count
    faces: list[tuple[int, int, int]] = []
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((top_center, index, next_index))
        faces.append((index, bottom_start + index, bottom_start + next_index))
        faces.append((index, bottom_start + next_index, next_index))
        faces.append((bottom_center, bottom_start + next_index, bottom_start + index))
    mesh = bpy.data.meshes.new(f"{obj.name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material("FapPuddleWater"))
    mesh.calc_loop_triangles()
    old_mesh = obj.data
    obj.data = mesh
    obj["geometry_pass"] = "shallow irregular puddle with explicit triangulated rim"
    obj["triangle_count"] = len(mesh.loop_triangles)
    if old_mesh is not None and old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)
    mesh.name = f"{obj.name}Mesh"


def rebuild_path_puddles() -> None:
    puddle_specs = (
        ("FapPathPuddleCluster_Puddle_00_LOD0", (0.814, -1.382), (0.56, 0.24), 0.10),
        ("FapPathPuddleCluster_Puddle_01_LOD0", (-0.671, -2.763), (0.40, 0.20), 0.42),
        ("FapPathPuddleCluster_Puddle_02_LOD0", (0.757, -4.146), (0.61, 0.20), 0.18),
        ("FapPathPuddleCluster_Puddle_03_LOD0", (-0.869, -5.533), (0.47, 0.18), 0.58),
        ("FapPathPuddleCluster_Puddle_04_LOD0", (0.176, -6.832), (0.67, 0.22), 0.30),
    )
    for name, center, radii, phase in puddle_specs:
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Missing FAP path puddle mesh: {name}")
        replace_puddle_mesh(obj, center, radii, phase)


def loop_triangle_area(mesh: bpy.types.Mesh, triangle: bpy.types.MeshLoopTriangle) -> float:
    a, b, c = (mesh.vertices[index].co for index in triangle.vertices)
    return 0.5 * (b - a).cross(c - a).length


def mesh_object(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_names: tuple[str, ...],
    material_indices: list[int] | None = None,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "FAP architectural presentation geometry",
) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH" or obj.parent is not parent:
        raise RuntimeError(f"Baseline child contract is incomplete: {name}")
    old_mesh = obj.data
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False, clean_customdata=False)
    mesh.update()
    mesh.materials.clear()
    for material_name in material_names:
        mesh.materials.append(material(material_name))
    if material_indices is None:
        material_indices = [0] * len(mesh.polygons)
    if len(material_indices) != len(mesh.polygons):
        raise RuntimeError(f"Material index count mismatch for {name}")
    for polygon, index in zip(mesh.polygons, material_indices):
        polygon.material_index = index
    triangulate_mesh_data(mesh)
    obj.data = mesh
    obj.rotation_mode = "XYZ"
    obj.location = location
    obj.rotation_euler = rotation
    obj.scale = (1.0, 1.0, 1.0)
    obj.delta_location = (0.0, 0.0, 0.0)
    obj.delta_rotation_euler = (0.0, 0.0, 0.0)
    obj.delta_scale = (1.0, 1.0, 1.0)
    obj.hide_viewport = False
    obj.hide_render = False
    obj["urman_asset_id"] = "urman.act1.fap_clinic_kit"
    obj["component_root"] = parent.name
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["triangle_count"] = len(mesh.loop_triangles)
    obj["geometry_pass"] = (
        "restrained painterly low-poly clinic interior shell, Wave14 first-person composition and joinery"
        if parent.name == INTERIOR
        else "weathered modest rural clinic facade v2"
    )
    if old_mesh is not None and old_mesh is not mesh and old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)
    # The old datablock is removed only after reassignment (Blender keeps a
    # user while an object has no data), then the fresh name is canonicalized.
    mesh.name = f"{name}Mesh"
    return obj


def ensure_empty(name: str, parent: bpy.types.Object) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, None)
        collection = parent.users_collection[0] if parent.users_collection else bpy.context.scene.collection
        collection.objects.link(obj)
        obj.parent = parent
    if obj.type != "EMPTY" or obj.parent is not parent:
        raise RuntimeError(f"Component root contract is incomplete: {name}")
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj["urman_asset_id"] = "urman.act1.fap_clinic_kit"
    obj["component_root"] = name
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["asset_role"] = "compact rural FAP interior presentation set"
    obj["geometry_pass"] = "restrained painterly low-poly clinic shell, Wave14 first-person composition and joinery"
    return obj


def authored_mesh_object(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_names: tuple[str, ...],
    material_indices: list[int] | None = None,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "FAP interior presentation geometry",
) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, bpy.data.meshes.new(f"{name}Mesh"))
        collection = parent.users_collection[0] if parent.users_collection else bpy.context.scene.collection
        collection.objects.link(obj)
        obj.parent = parent
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        material_names,
        material_indices,
        location,
        rotation,
        role,
    )


def chamfered_box(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    chamfer: float = 0.035,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "FAP timber/plaster detail",
    allow_new: bool = False,
) -> bpy.types.Object:
    sx, sy, sz = (value / 2.0 for value in size)
    c = min(chamfer, sx * 0.42, sy * 0.42)
    ring = [
        (-sx + c, -sy),
        (sx - c, -sy),
        (sx, -sy + c),
        (sx, sy - c),
        (sx - c, sy),
        (-sx + c, sy),
        (-sx, sy - c),
        (-sx, -sy + c),
    ]
    vertices = [(x, y, -sz) for x, y in ring] + [(x, y, sz) for x, y in ring]
    faces: list[tuple[int, ...]] = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8))
    builder = authored_mesh_object if allow_new else mesh_object
    return builder(
        name,
        parent,
        vertices,
        faces,
        (material_name,),
        location=center,
        rotation=rotation,
        role=role,
    )


def beam_between(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    depth: float,
    width: float,
    material_name: str,
    role: str = "FAP weathered roof edge",
) -> bpy.types.Object:
    vector = tuple(end[i] - start[i] for i in range(3))
    length = math.sqrt(sum(value * value for value in vector))
    if length <= 0.01:
        raise RuntimeError(f"Degenerate beam: {name}")
    center = tuple((start[i] + end[i]) / 2.0 for i in range(3))
    rotation = (0.0, -math.atan2(vector[2], vector[0]), 0.0)
    return chamfered_box(
        name,
        parent,
        center,
        (length, depth, width),
        material_name,
        chamfer=min(depth, width) * 0.24,
        rotation=rotation,
        role=role,
    )


def prism_profile(
    name: str,
    parent: bpy.types.Object,
    profile: tuple[tuple[float, float], ...],
    front_y: float,
    back_y: float,
    material_names: tuple[str, ...],
    material_indices: list[int],
    role: str,
    allow_new: bool = False,
) -> bpy.types.Object:
    # Profiles run counter-clockwise in X/Z and extrude toward increasing Y.
    # Their front/back caps already face outward. Side loops must traverse the
    # front edge in reverse; the old order made every side point into the solid.
    signed_area = sum(profile[i][0] * profile[(i + 1) % len(profile)][1]
                      - profile[(i + 1) % len(profile)][0] * profile[i][1]
                      for i in range(len(profile))) * 0.5
    if len(profile) < 3 or signed_area <= 1e-8 or back_y <= front_y:
        raise RuntimeError(f"Invalid counter-clockwise extruded profile: {name}")
    vertices = [(x, front_y, z) for x, z in profile]
    vertices.extend((x, back_y, z) for x, z in profile)
    count = len(profile)
    faces: list[tuple[int, ...]] = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    faces.extend((i, i + count, (i + 1) % count + count, (i + 1) % count) for i in range(count))
    builder = authored_mesh_object if allow_new else mesh_object
    return builder(name, parent, vertices, faces, material_names, material_indices, role=role)


def roof_prism(name: str, parent: bpy.types.Object, material_names: tuple[str, ...]) -> bpy.types.Object:
    # The ridge is deliberately a little off-centre and the rear eave is a
    # few centimetres shorter, giving the compact roof a lived-in silhouette.
    top_front = ((-4.78, 3.12), (-0.18, 4.63), (4.78, 3.12))
    top_back = ((-4.70, 3.14), (-0.10, 4.58), (4.70, 3.14))
    vertices = [(x, -3.58, z) for x, z in top_front]
    vertices.extend((x, 3.48, z) for x, z in top_back)
    top_vertices = tuple(vertices)
    vertices.extend((x, y, z - 0.16) for (x, y, z) in top_vertices)
    faces = [
        (0, 3, 4, 1),
        (1, 4, 5, 2),
        (6, 7, 10, 9),
        (7, 8, 11, 10),
        (0, 6, 9, 3),
        (2, 5, 11, 8),
        (1, 7, 10, 4),
        (0, 1, 7, 6),
        (1, 2, 8, 7),
        (3, 9, 10, 4),
        (4, 10, 11, 5),
    ]
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        material_names,
        [0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1],
        role="faceted overhanging gable roof with deep eaves",
    )


def ensure_contract(root: bpy.types.Object) -> dict[str, bpy.types.Object]:
    if root.type != "EMPTY" or root.parent is not None:
        raise RuntimeError("FAP authored root must be a scene-level empty")
    if tuple(root.location) != (0.0, 0.0, 0.0) or tuple(root.rotation_euler) != (0.0, 0.0, 0.0) or tuple(root.scale) != (1.0, 1.0, 1.0):
        raise RuntimeError("FAP authored root transform is not identity")
    expected_components = {
        "FapFacade_Main",
        "FapEntryPorch",
        "FapWayfindingBoard",
        "FapServiceShed",
        "FapFenceRun",
        "FapGate",
        "FapBench",
        "FapNoticeBoard",
        "FapRainAwning",
        "FapPathPuddleCluster",
        "FapBirchShrubMass",
        INTERIOR,
    }
    interior = ensure_empty(INTERIOR, root)
    actual_components = {child.name for child in root.children}
    if actual_components != expected_components:
        raise RuntimeError(f"Direct component contract changed: {sorted(actual_components ^ expected_components)}")
    result = {}
    for component_name, expected_children in ((FACADE, FACADE_CHILDREN), (PORCH, PORCH_CHILDREN), (SHED, SHED_CHILDREN)):
        component = bpy.data.objects.get(component_name)
        if component is None or component.parent is not root or component.type != "EMPTY":
            raise RuntimeError(f"Missing component root: {component_name}")
        actual_children = {child.name for child in component.children}
        permitted = set(expected_children) | (set(FACADE_ENVELOPE_CHILDREN) if component_name == FACADE else set())
        if not set(expected_children).issubset(actual_children) or not actual_children.issubset(permitted):
            raise RuntimeError(f"{component_name} child contract changed: {sorted(actual_children ^ set(expected_children))}")
        result[component_name] = component
    result[INTERIOR] = interior
    return result


def b(name: str, parent: bpy.types.Object, center: tuple[float, float, float], size: tuple[float, float, float], mat: str, chamfer: float = 0.035, rotation: tuple[float, float, float] = (0.0, 0.0, 0.0), role: str = "FAP architectural detail") -> bpy.types.Object:
    return chamfered_box(name, parent, center, size, mat, chamfer, rotation, role)


def ib(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    mat: str,
    chamfer: float = 0.035,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "FAP interior presentation detail",
) -> bpy.types.Object:
    return chamfered_box(name, parent, center, size, mat, chamfer, rotation, role, allow_new=True)


def ibeam(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    depth: float,
    width: float,
    mat: str,
    role: str = "FAP interior timber rail",
) -> bpy.types.Object:
    vector = tuple(end[i] - start[i] for i in range(3))
    length = math.sqrt(sum(value * value for value in vector))
    if length <= 0.01:
        raise RuntimeError(f"Degenerate interior beam: {name}")
    center = tuple((start[i] + end[i]) / 2.0 for i in range(3))
    rotation = (0.0, -math.atan2(vector[2], vector[0]), 0.0)
    return ib(
        name,
        parent,
        center,
        (length, depth, width),
        mat,
        chamfer=min(depth, width) * 0.24,
        rotation=rotation,
        role=role,
    )


def rotate_xy_point(
    point: tuple[float, float],
    pivot: tuple[float, float],
    angle: float,
) -> tuple[float, float]:
    """Rotate one floor-plan point around an authored room anchor."""
    px, py = pivot
    x, y = point
    cosine = math.cos(angle)
    sine = math.sin(angle)
    dx = x - px
    dy = y - py
    return (px + dx * cosine - dy * sine, py + dx * sine + dy * cosine)


def reframe_mesh_group(
    parent: bpy.types.Object,
    prefix: str,
    pivot: tuple[float, float],
    angle: float,
    translation: tuple[float, float] = (0.0, 0.0),
) -> None:
    """Reframe direct LOD0 children without introducing another scene root."""
    tx, ty = translation
    for child in parent.children:
        if child.type != "MESH" or not child.name.startswith(prefix) or not child.name.endswith("_LOD0"):
            continue
        x, y = rotate_xy_point((child.location.x, child.location.y), pivot, angle)
        child.location.x = x + tx
        child.location.y = y + ty
        child.rotation_euler.z += angle


def reframe_screen_group(
    parent: bpy.types.Object,
    pivot: tuple[float, float],
    angle: float,
    translation: tuple[float, float] = (0.0, 0.0),
) -> None:
    """Rotate the screen's global-profile meshes and its anchored joinery together."""
    tx, ty = translation
    for child in parent.children:
        if child.type != "MESH" or not child.name.startswith("FapInteriorScreen_") or not child.name.endswith("_LOD0"):
            continue
        if child.name.startswith("FapInteriorScreen_Panel"):
            for vertex in child.data.vertices:
                x, y = rotate_xy_point((vertex.co.x, vertex.co.y), pivot, angle)
                vertex.co.x = x + tx
                vertex.co.y = y + ty
            child.data.update()
            continue
        x, y = rotate_xy_point((child.location.x, child.location.y), pivot, angle)
        child.location.x = x + tx
        child.location.y = y + ty
        child.rotation_euler.z += angle


def rebuild_interior_lod1(source_name: str, ratio: float = 0.62) -> bpy.types.Object:
    """Build one deterministic, presentation-only LOD1 sibling for a detail."""
    source = bpy.data.objects.get(source_name)
    if source is None or source.type != "MESH" or source.parent is None:
        raise RuntimeError(f"Missing FAP interior LOD0 source: {source_name}")
    lod_name = source_name.replace("_LOD0", "_LOD1")
    previous = bpy.data.objects.get(lod_name)
    if previous is not None:
        previous_mesh = previous.data if previous.type == "MESH" else None
        bpy.data.objects.remove(previous, do_unlink=True)
        if previous_mesh is not None and previous_mesh.users == 0:
            bpy.data.meshes.remove(previous_mesh)

    lod = source.copy()
    lod.data = source.data.copy()
    lod.name = lod_name
    lod.parent = source.parent
    lod.matrix_parent_inverse = source.matrix_parent_inverse.copy()
    lod["lod_source"] = source_name
    lod["lod_status"] = f"authored LOD1 sibling from {source_name}; ratio={ratio:.2f}"
    lod["collision"] = "none"
    lod["presentation_only"] = True
    lod["asset_role"] = "FAP interior authored detail LOD1"
    collection = source.users_collection[0] if source.users_collection else bpy.context.scene.collection
    collection.objects.link(lod)

    bpy.ops.object.select_all(action="DESELECT")
    lod.select_set(True)
    bpy.context.view_layer.objects.active = lod
    modifier = lod.modifiers.new("URMAN_FAP_INTERIOR_LOD1_Decimate", "DECIMATE")
    modifier.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    lod.select_set(False)
    return lod


def build_facade(parent: bpy.types.Object) -> None:
    # A low, broad foundation and slightly tapered wall keep the one-storey
    # clinic modest while leaving the existing parcel/approach footprint.
    b("FapFacade_Foundation_LOD0", parent, (0.02, 0.02, 0.20), (9.42, 6.86, 0.40), "FapFoundationStone", 0.08, role="grounded uneven stone foundation")
    b("FapFacade_Body_LOD0", parent, (0.04, 0.02, 1.78), (8.98, 6.48, 2.82), "FapPaintedSage", 0.055, role="slightly irregular plaster clinic wall")
    b("FapFacade_FrontPlinth_LOD0", parent, (0.04, -3.28, 0.72), (8.36, 0.16, 0.54), "FapPaintedDustyBlue", 0.025, role="weathered painted front plinth")
    b("FapFacade_FrontCourse_LOD0", parent, (0.02, -3.29, 3.16), (8.58, 0.18, 0.20), "FapPaintedTimber", 0.025, role="front eave course")
    b("FapFacade_BackCourse_LOD0", parent, (0.02, 3.28, 3.16), (8.52, 0.18, 0.20), "FapPaintedTimber", 0.025, role="rear eave course")
    b("FapFacade_CornerPost_L_LOD0", parent, (-4.30, -3.24, 1.78), (0.28, 0.25, 2.92), "FapPaintedTimber", 0.035, rotation=(0.0, 0.0, math.radians(-0.7)), role="front left weathered corner post")
    b("FapFacade_CornerPost_R_LOD0", parent, (4.27, -3.25, 1.78), (0.30, 0.25, 2.92), "FapPaintedTimber", 0.035, rotation=(0.0, 0.0, math.radians(0.45)), role="front right weathered corner post")
    b("FapFacade_BackCornerPost_L_LOD0", parent, (-4.27, 3.24, 1.78), (0.27, 0.24, 2.90), "FapPaintedTimber", 0.035, role="rear left corner post")
    b("FapFacade_BackCornerPost_R_LOD0", parent, (4.24, 3.24, 1.78), (0.27, 0.24, 2.90), "FapPaintedTimber", 0.035, role="rear right corner post")
    prism_profile("FapFacade_Gable_LOD0", parent, ((-4.26, 3.10), (4.24, 3.10), (-0.18, 4.55)), -3.25, 3.23, ("FapPaintedSage", "FapPaintedTimber"), [0, 0, 1, 1, 1], "plain asymmetrical gable infill")
    roof_prism("FapFacade_Roof_LOD0", parent, ("FapOldRoof", "FapRoofEdge"))
    b("FapFacade_RidgeCap_LOD0", parent, (-0.14, -0.04, 4.66), (0.24, 7.20, 0.18), "FapRoofEdge", 0.035, role="ridge cap")
    beam_between("FapFacade_RoofEdge_L_LOD0", parent, (-4.82, -3.61, 3.12), (-0.18, -3.61, 4.63), 0.20, 0.18, "FapRoofEdge")
    beam_between("FapFacade_RoofEdge_R_LOD0", parent, (-0.18, -3.61, 4.63), (4.82, -3.61, 3.12), 0.20, 0.18, "FapRoofEdge")
    beam_between("FapFacade_RoofEdgeBack_L_LOD0", parent, (-4.74, 3.51, 3.14), (-0.10, 3.51, 4.58), 0.20, 0.18, "FapRoofEdge")
    beam_between("FapFacade_RoofEdgeBack_R_LOD0", parent, (-0.10, 3.51, 4.58), (4.74, 3.51, 3.14), 0.20, 0.18, "FapRoofEdge")
    b("FapFacade_GableVent_LOD0", parent, (-0.24, -3.32, 4.06), (0.58, 0.10, 0.34), "FapVentDark", 0.025, role="plain gable vent")

    # Centered entry: the windows deliberately have slightly different sill
    # heights so the front reads as maintained, but not factory-perfect.
    b("FapFacade_DoorPanel_LOD0", parent, (-0.12, -3.37, 1.57), (1.24, 0.14, 2.24), "FapDoorWood", 0.045, role="recessed clinic entry door")
    b("FapFacade_DoorPanelInset_LOD0", parent, (-0.12, -3.45, 1.26), (0.78, 0.05, 0.68), "FapDoorInset", 0.025, role="door lower inset")
    b("FapFacade_DoorFrameLeft_LOD0", parent, (-0.83, -3.48, 1.60), (0.16, 0.20, 2.46), "FapPaintedTimber", 0.025, role="entry door left jamb")
    b("FapFacade_DoorFrameRight_LOD0", parent, (0.59, -3.48, 1.60), (0.16, 0.20, 2.46), "FapPaintedTimber", 0.025, role="entry door right jamb")
    b("FapFacade_DoorFrameTop_LOD0", parent, (-0.12, -3.48, 2.82), (1.58, 0.20, 0.17), "FapPaintedTimber", 0.025, role="entry door lintel")
    b("FapFacade_DoorHandle_LOD0", parent, (0.29, -3.59, 1.58), (0.11, 0.11, 0.17), "FapRainMetal", 0.025, role="dulled door handle")

    def window(prefix: str, x: float, z: float, width: float, material_name: str, warm_inset: bool = False) -> None:
        b(f"FapFacade_Window{prefix}_Glass_LOD0", parent, (x, -3.39, z), (width, 0.08, 1.16 if prefix == "Left" else 1.22), material_name, 0.025, role="set-back glazed window")
        b(f"FapFacade_Window{prefix}_MuntinHorizontal_LOD0", parent, (x, -3.46, z), (width + 0.04, 0.11, 0.10), "FapPaintedTimber", 0.018, role="window horizontal muntin")
        b(f"FapFacade_Window{prefix}_MuntinVertical_LOD0", parent, (x, -3.46, z), (0.10, 0.11, 1.12 if prefix == "Left" else 1.18), "FapPaintedTimber", 0.018, role="window vertical muntin")
        half = width / 2.0 + 0.18
        bottom = z - (0.58 if prefix == "Left" else 0.61)
        top = z + (0.58 if prefix == "Left" else 0.61)
        b(f"FapFacade_Window{prefix}_TrimBottom_LOD0", parent, (x, -3.46, bottom), (width + 0.36, 0.14, 0.14), "FapPaintedTimber", 0.020, role="window lower trim")
        b(f"FapFacade_Window{prefix}_TrimTop_LOD0", parent, (x, -3.46, top), (width + 0.36, 0.14, 0.14), "FapPaintedTimber", 0.020, role="window upper trim")
        b(f"FapFacade_Window{prefix}_TrimLeft_LOD0", parent, (x - half, -3.46, z), (0.14, 0.14, top - bottom), "FapPaintedTimber", 0.020, role="window left trim")
        b(f"FapFacade_Window{prefix}_TrimRight_LOD0", parent, (x + half, -3.46, z), (0.14, 0.14, top - bottom), "FapPaintedTimber", 0.020, role="window right trim")
        if warm_inset:
            b("FapFacade_WindowRight_WarmInset_LOD0", parent, (x, -3.51, z + 0.28), (width * 0.62, 0.035, 0.20), "FapWindowWarm", 0.015, role="subtle warm inner window reflection")

    window("Left", -2.65, 1.92, 1.54, "FapWindowCool")
    window("Right", 2.56, 1.98, 1.48, "FapWindowWarm", warm_inset=True)

    # The side access is intentionally smaller and lower than the front entry.
    b("FapFacade_ServiceDoor_LOD0", parent, (4.36, 1.10, 1.38), (0.14, 1.06, 2.00), "FapDoorWood", 0.035, role="lateral service door")
    b("FapFacade_ServiceDoorFrame_LOD0", parent, (4.43, 1.10, 2.45), (0.20, 1.32, 0.16), "FapPaintedTimber", 0.025, role="lateral service door lintel")
    b("FapFacade_SideBand_0_7_LOD0", parent, (4.43, 0.48, 2.24), (0.18, 0.14, 0.16), "FapPaintedTimber", 0.025, role="side door lower jamb cue")
    b("FapFacade_SideBand_1_95_LOD0", parent, (4.43, 1.72, 2.24), (0.18, 0.14, 0.16), "FapPaintedTimber", 0.025, role="side door upper jamb cue")


def build_porch(parent: bpy.types.Object) -> None:
    b("FapEntryPorch_Deck_LOD0", parent, (0.0, -0.34, 0.40), (3.30, 1.62, 0.20), "FapPaintedTimber", 0.045, role="raised timber entry deck")
    b("FapEntryPorch_Threshold_LOD0", parent, (-0.12, 0.03, 0.54), (1.50, 0.34, 0.08), "FapWetStone", 0.018, role="full leaf-width threshold resting on the unchanged deck; leaf closes into its upper rebate")
    # Solid masonry risers meet the ground and overlap in plan. The old
    # separate shallow slabs floated above each other and widened uphill.
    for index, (y, width, height) in enumerate(((-1.20, 2.74, 0.38), (-1.55, 2.92, 0.25), (-1.90, 3.10, 0.12))):
        b(f"FapEntryPorch_Step_{index:02d}_LOD0", parent,
                 (-0.04, y, height / 2), (width, 0.46, height),
                 "FapWetStone", 0.018, role="grounded solid masonry entry step")
    # The old flat canopy cut through the entry leaf/header and its own post
    # caps. Give it a real outward fall, above the matched 3.23m door frame.
    # The porch mount is 5.73m in front of the facade origin. Terminate the
    # canopy at its outer wall face (5.99m), with 3cm construction overlap;
    # the former rear edge continued through the front wall into the room.
    front_y, back_y = -2.02, -.23
    front_under, back_under, thickness = 3.18, 3.45, .14
    post_y = -.94
    under_at_post = front_under + (post_y - front_y) / (back_y - front_y) * (back_under - front_under)
    cap_top = under_at_post + .010
    cap_bottom = cap_top - .16
    post_bottom, post_top = .40, cap_bottom + .012
    for token, sign, brace_name in (("L", -1, "Left"), ("R", 1, "Right")):
        b(f"FapEntryPorch_Post_{token}_LOD0", parent,
          (sign * 1.42, post_y, (post_bottom + post_top) / 2),
          (.22, .22, post_top - post_bottom), "FapPaintedTimber", .025,
          role="porch post bearing from the unchanged deck into its roof cap")
        cap_ring = [(sign * 1.42 - .17, post_y - .16), (sign * 1.42 + .17, post_y - .16),
                    (sign * 1.42 + .17, post_y + .16), (sign * 1.42 - .17, post_y + .16)]
        cap_vertices = [(x, y, cap_bottom) for x, y in cap_ring]
        cap_vertices += [(x, y, front_under + (y - front_y) / (back_y - front_y)
                           * (back_under - front_under) + .010) for x, y in cap_ring]
        mesh_object(f"FapEntryPorch_PostCap_{token}_LOD0", parent, cap_vertices,
                    [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
                     (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)],
                    ("FapDarkTimber",), role="cap scribed to the underside of the sloped canopy")
        # Lower posts stand on Step_01. The former feet stopped 16cm above it,
        # and the right post overhung its tread. The masonry stays unchanged.
        b(f"FapEntryPorch_RailPost_{token}_LOD0", parent,
          (sign * 1.33, -1.55, .71), (.15, .15, .92), "FapPaintedTimber", .018,
          role="rail post resting on the existing middle masonry tread")
        b(f"FapEntryPorch_Rail_{token}_LOD0", parent,
          (sign * 1.33, -1.23, .82), (.17, .98, .15), "FapPaintedTimber", .018,
          role="porch side rail joined to both support posts")
        beam_between(f"FapEntryPorch_Brace{brace_name}_LOD0", parent,
          (sign * 1.42, post_y, 2.56), (sign * .56, post_y, under_at_post - .012),
          .14, .14, "FapDarkTimber", role="vertical knee brace joining the post to the roof")
    lower = [(-1.95, front_y, front_under), (1.95, front_y, front_under),
             (1.95, back_y, back_under), (-1.95, back_y, back_under)]
    vertices = lower + [(x, y, z + thickness) for x, y, z in lower]
    faces = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    mesh_object("FapEntryPorch_RainCanopy_LOD0", parent, vertices, faces,
                ("FapRoofEdge",), role="sloped solid entry canopy joined above the visible door header")
    b("FapEntryPorch_CanopyFascia_LOD0", parent, (0, -2.03, 3.25),
      (4.04, .18, .24), "FapOldRoof", .018, role="fascia joined to the low canopy edge")
    b("FapEntryPorch_DoorHeaderPlaque_LOD0", parent, (-.12, -2.14, 3.25),
      (1.28, .08, .18), "FapPaintedDustyBlue", .018, role="existing plain plaque mounted on the canopy fascia")


def build_shed(parent: bpy.types.Object) -> None:
    b("FapServiceShed_Foundation_LOD0", parent, (0.02, 0.04, 0.15), (3.42, 2.58, 0.30), "FapFoundationStone", 0.07, role="shed stone foot")
    b("FapServiceShed_Body_LOD0", parent, (0.0, 0.0, 1.25), (3.16, 2.34, 2.20), "FapShedWall", 0.045, role="compact service shed wall")
    prism_profile("FapServiceShed_Roof_LOD0", parent, ((-1.78, 2.28), (1.75, 2.28), (-0.08, 3.15)), -1.42, 1.46, ("FapOldRoof", "FapRoofEdge"), [0, 0, 1, 1, 1], "small asymmetrical shed roof")
    beam_between("FapServiceShed_RoofEdge_L_LOD0", parent, (-1.87, -1.53, 2.28), (-0.08, -1.53, 3.18), 0.15, 0.14, "FapRoofEdge", role="shed left roof edge")
    beam_between("FapServiceShed_RoofEdge_R_LOD0", parent, (-0.08, -1.53, 3.18), (1.84, -1.53, 2.28), 0.15, 0.14, "FapRoofEdge", role="shed right roof edge")
    b("FapServiceShed_Door_LOD0", parent, (0.02, -1.25, 1.17), (1.02, 0.13, 1.74), "FapDoorWood", 0.035, role="shed service door")
    b("FapServiceShed_DoorFrame_L_LOD0", parent, (-0.54, -1.33, 1.18), (0.15, 0.16, 1.94), "FapPaintedTimber", 0.025, role="shed door left frame")
    b("FapServiceShed_DoorFrame_R_LOD0", parent, (0.58, -1.33, 1.18), (0.15, 0.16, 1.94), "FapPaintedTimber", 0.025, role="shed door right frame")
    b("FapServiceShed_DoorFrameTop_LOD0", parent, (0.02, -1.33, 2.10), (1.28, 0.16, 0.15), "FapPaintedTimber", 0.025, role="shed door lintel")
    for x, token in ((-1.16, "-1_25"), (-0.40, "-0_42"), (0.40, "0_42"), (1.16, "1_25")):
        b(f"FapServiceShed_FrontBatten_{token}_LOD0", parent, (x, -1.29, 1.31), (0.12, 0.08, 1.92), "FapDarkTimber", 0.018, role="shed vertical batten")
    b("FapServiceShed_Vent_LOD0", parent, (1.00, -1.31, 2.32), (0.60, 0.08, 0.30), "FapVentDark", 0.02, role="shed ventilation slot")
    for z, token in ((2.23, "2_06"), (2.32, "2_15"), (2.41, "2_24")):
        b(f"FapServiceShed_VentSlat_{token}_LOD0", parent, (1.00, -1.37, z), (0.50, 0.05, 0.035), "FapPaintedTimber", 0.01, role="shed vent slat")


def build_wash_basin(parent: bpy.types.Object) -> None:
    """A supported enamel bowl, not a solid shelf with a painted inset.

    Coordinates retain the existing right-wall fitting. The cabinet has an
    open top below the bowl and two bearing side panels; the shallow basin
    keeps a closed ceramic shell and an actual descending inner surface.
    """
    vertices, faces = [], []

    def panel(center, size):
        x, y, z = center
        width, depth, height = size
        offset = len(vertices)
        vertices.extend((x + dx * width / 2, y + dy * depth / 2, z + dz * height / 2)
                        for dx, dy, dz in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                           (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)))
        faces.extend(tuple(offset + i for i in face) for face in
                     ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)))

    panel((5.055, -1.56, .22), (.53, .88, .06))
    panel((5.300, -1.56, .5325), (.04, .88, .645))
    for side in (-1, 1):
        panel((5.055, -1.56 + side * .4175, .5375), (.53, .045, .635))
        panel((4.815, -1.56 + side * .216, .4925), (.04, .416, .475))
        # Small real handles face the room; no new interaction is promised.
        panel((4.775, -1.56 + side * .065, .69), (.06, .025, .105))
    mesh_object("FapInteriorWashUnit_Body_LOD0", parent, vertices, faces,
                ("FapPaintedSage",), role="two-door wash cupboard with open bowl clearance and two bearing side panels")

    vertices, faces = [], []
    # Each octagonal ring is counter-clockwise viewed from above. The rim
    # overhangs its two .045m cabinet sides by 3cm. The bowl's floor descends
    # 16cm below the rim and stays clear of the cupboard's rear/front panels.
    rings = (
        (5.04, .310, .470, .055, .855),
        (5.04, .310, .470, .070, .940),
        (5.00, .220, .360, .060, .940),
        (4.99, .190, .310, .065, .850),
        (4.97, .090, .190, .035, .780),
        (4.97, .120, .220, .045, .750),
        (5.00, .240, .380, .060, .855),
    )
    for cx, rx, ry, corner, z in rings:
        vertices.extend((cx + x, -1.56 + y, z) for x, y in
                        ((rx, -ry + corner), (rx, ry - corner), (rx - corner, ry),
                         (-rx + corner, ry), (-rx, ry - corner), (-rx, -ry + corner),
                         (-rx + corner, -ry), (rx - corner, -ry)))
    for first, second in ((0,1), (1,2), (2,3), (3,4), (5,6), (6,0)):
        for i in range(8):
            j = (i + 1) % 8
            faces.append((first * 8 + i, first * 8 + j, second * 8 + j, second * 8 + i))
    faces.append(tuple(4 * 8 + i for i in range(8)))
    faces.append(tuple(5 * 8 + i for i in reversed(range(8))))
    mesh_object("FapInteriorWashUnit_Basin_LOD0", parent, vertices, faces,
                ("FapRainMetal",), role="closed enamel basin shell; .94m rim, .78m inner floor and visible bowl depth")

    # Reuse the old inset's stable node as a small physical waste strainer at
    # the bottom. It is no longer a narrow painted mark on top of a solid box.
    vertices, faces = [], []
    for radius, z in ((.024, .781), (.019, .784), (.010, .783)):
        vertices.extend((4.97 + math.cos(i * math.tau / 12) * radius,
                         -1.56 + math.sin(i * math.tau / 12) * radius, z) for i in range(12))
    for first, second in ((0,1), (1,2)):
        for i in range(12):
            j = (i + 1) % 12
            faces.append((first * 12 + i, first * 12 + j, second * 12 + j, second * 12 + i))
    faces.append(tuple(24 + i for i in range(12)))
    faces.append(tuple(reversed(range(12))))
    mesh_object("FapInteriorWashUnit_BasinInset_LOD0", parent, vertices, faces,
                ("FapRainMetalDark",), role="small drain strainer seated on the actual bowl floor")


def build_interior(parent: bpy.types.Object) -> None:
    # Remove the superseded Wave20 furnishing cluster when rebuilding an
    # existing source blend, so the rollback converges to the Wave18 contract.
    retired_wave20_names = (
        "FapInteriorExamCasework_Base_LOD0",
        "FapInteriorExamCasework_Worktop_LOD0",
        "FapInteriorExamCasework_SidePost_LOD0",
        "FapInteriorWaitingSideTable_Body_LOD0",
        "FapInteriorWaitingSideTable_Top_LOD0",
        "FapInteriorWaitingSideTable_Foot_LOD0",
        "FapInteriorRecordsHutch_Body_LOD0",
        "FapInteriorRecordsHutch_Shelf_LOD0",
        "FapInteriorRecordsHutch_TopCap_LOD0",
    )
    for retired_name in retired_wave20_names + tuple(name.replace("_LOD0", "_LOD1") for name in retired_wave20_names):
        retired = bpy.data.objects.get(retired_name)
        if retired is None:
            continue
        retired_mesh = retired.data if retired.type == "MESH" else None
        bpy.data.objects.remove(retired, do_unlink=True)
        if retired_mesh is not None and retired_mesh.users == 0:
            bpy.data.meshes.remove(retired_mesh)

    # The interrupted pass used radiator names without the runtime family's
    # separator. Remove those six legacy authored objects before rebuilding so
    # a rerun from that source blend converges to the exact child contract.
    legacy_radiator_names = (
        "FapInteriorRadiatorBody_LOD0",
        "FapInteriorRadiatorRibLeft_LOD0",
        "FapInteriorRadiatorRibCenter_LOD0",
        "FapInteriorRadiatorRibRight_LOD0",
        "FapInteriorRadiatorPipeLeft_LOD0",
        "FapInteriorRadiatorPipeRight_LOD0",
    )
    for legacy_name in legacy_radiator_names:
        legacy = bpy.data.objects.get(legacy_name)
        if legacy is None:
            continue
        legacy_mesh = legacy.data if legacy.type == "MESH" else None
        bpy.data.objects.remove(legacy, do_unlink=True)
        if legacy_mesh is not None and legacy_mesh.users == 0:
            bpy.data.meshes.remove(legacy_mesh)

    # The source uses the same Blender convention as the exterior kit: +Z is
    # up, while exported Godot depth is -Y.  These anchors therefore mirror
    # the benchmark room's Godot X/Z positions without adding a runtime owner.
    # Keep the shell as authored geometry as well as the furniture.  The
    # front wall is split around a recessed door so the entry reads as a
    # threshold from the waiting-room spawn while the existing Godot body
    # keeps its collision footprint.
    ib(
        "FapInteriorShell_Floor_LOD0",
        parent,
        (0.0, 0.0, -0.06),
        (11.55, 11.55, 0.12),
        "FapPaintedDustyBlue",
        0.045,
        role="authored worn linoleum floor field",
    )
    ib(
        "FapInteriorShell_Ceiling_LOD0",
        parent,
        (0.0, 0.0, 3.34),
        (11.55, 11.55, 0.14),
        "FapPaintedSage",
        0.045,
        role="authored modest clinic ceiling plane",
    )
    ib(
        "FapInteriorShell_BackWall_LOD0",
        parent,
        (0.0, 5.72, 1.68),
        (11.55, 0.18, 3.18),
        "FapPaintedSage",
        0.045,
        role="authored rear institutional wall volume",
    )
    ib(
        "FapInteriorShell_LeftWall_LOD0",
        parent,
        (-5.72, 0.0, 1.68),
        (0.18, 11.55, 3.18),
        "FapPaintedSage",
        0.045,
        role="authored left institutional wall volume",
    )
    ib(
        "FapInteriorShell_RightWall_LOD0",
        parent,
        (5.72, 0.0, 1.68),
        (0.18, 11.55, 3.18),
        "FapPaintedSage",
        0.045,
        role="authored right institutional wall volume",
    )
    for name, x in (
        ("FapInteriorShell_FrontWallLeft_LOD0", -3.42),
        ("FapInteriorShell_FrontWallRight_LOD0", 3.42),
    ):
        ib(
            name,
            parent,
            (x, -5.72, 1.68),
            (4.72, 0.18, 3.18),
            "FapPaintedSage",
            0.045,
            role="authored front wall volume beside entry",
        )
    ib(
        "FapInteriorShell_FrontWallHeader_LOD0",
        parent,
        (0.0, -5.59, 2.96),
        (2.08, 0.26, 0.46),
        "FapPaintedSage",
        0.045,
        role="recessed front-wall entry reveal header",
    )
    ib(
        "FapInteriorShell_DoorInset_LOD0",
        parent,
        (0.0, -5.59, 1.42),
        (1.50, 0.07, 2.50),
        "FapDoorWood",
        0.035,
        role="recessed clinic entry door plane",
    )
    ib(
        "FapInteriorShell_DoorFrameLeft_LOD0",
        parent,
        (-0.86, -5.63, 1.48),
        (0.12, 0.22, 2.72),
        "FapPaintedTimber",
        0.022,
        role="authored entry jamb left",
    )
    ib(
        "FapInteriorShell_DoorFrameRight_LOD0",
        parent,
        (0.86, -5.63, 1.48),
        (0.12, 0.22, 2.72),
        "FapPaintedTimber",
        0.022,
        role="authored entry jamb right",
    )
    ib(
        "FapInteriorShell_DoorFrameTop_LOD0",
        parent,
        (0.0, -5.63, 2.83),
        (1.84, 0.22, 0.14),
        "FapPaintedTimber",
        0.022,
        role="authored entry lintel",
    )
    ib(
        "FapInteriorShell_DoorHandle_LOD0",
        parent,
        (0.48, -5.73, 1.50),
        (0.10, 0.12, 0.16),
        "FapRainMetal",
        0.018,
        role="dulled entry handle",
    )
    ib(
        "FapInteriorShell_Threshold_LOD0",
        parent,
        (0.0, -5.66, 0.09),
        (1.86, 0.60, 0.18),
        "FapWetStone",
        0.018,
        role="floor-supported threshold reaching beneath the matched door leaf",
    )

    for name, center, size in (
        ("FapInteriorShell_LowerBandBack_LOD0", (0.0, 5.59, 0.58), (11.24, 0.06, 0.68)),
        ("FapInteriorShell_LowerBandLeft_LOD0", (-5.59, 0.0, 0.58), (0.06, 11.24, 0.68)),
        ("FapInteriorShell_LowerBandRight_LOD0", (5.59, 0.0, 0.58), (0.06, 11.24, 0.68)),
        ("FapInteriorShell_LowerBandFrontLeft_LOD0", (-3.42, -5.59, 0.58), (4.72, 0.06, 0.68)),
        ("FapInteriorShell_LowerBandFrontRight_LOD0", (3.42, -5.59, 0.58), (4.72, 0.06, 0.68)),
    ):
        ib(
            name,
            parent,
            center,
            size,
            "FapPaintedDustyBlue",
            0.018,
            role="restrained institutional lower wall band",
        )

    for side, x in (("Left", -5.59), ("Right", 5.59)):
        ib(
            f"FapInteriorShell_Window{side}Glass_LOD0",
            parent,
            (x, 1.35, 1.95),
            (0.06, 2.08, 1.16),
            "FapWindowCool",
            0.018,
            role="recessed frosted side window",
        )
        ib(
            f"FapInteriorShell_Window{side}TrimTop_LOD0",
            parent,
            (x, 1.35, 2.58),
            (0.14, 2.28, 0.12),
            "FapPaintedTimber",
            0.018,
            role="side window upper trim",
        )
        ib(
            f"FapInteriorShell_Window{side}TrimBottom_LOD0",
            parent,
            (x, 1.35, 1.32),
            (0.14, 2.28, 0.12),
            "FapPaintedTimber",
            0.018,
            role="side window lower trim",
        )
        for suffix, y in (("Front", 0.28), ("Back", 2.42)):
            ib(
                f"FapInteriorShell_Window{side}Trim{suffix}_LOD0",
                parent,
                (x, y, 1.95),
                (0.14, 0.12, 1.36),
                "FapPaintedTimber",
                0.018,
                role="side window vertical trim",
            )

    ib(
        "FapInteriorShell_CeilingFixtureHousing_LOD0",
        parent,
        (0.0, -0.20, 3.16),
        (1.66, 0.64, 0.16),
        "FapRainMetalDark",
        0.035,
        role="restrained ceiling practical housing",
    )
    ib(
        "FapInteriorShell_CeilingFixtureLens_LOD0",
        parent,
        (0.0, -0.20, 3.05),
        (1.18, 0.24, 0.05),
        "FapWindowCool",
        0.018,
        role="diffuse institutional ceiling practical lens",
    )

    # Construction pass: a restrained cap/cornice vocabulary and four corner
    # posts tie the wall planes together.  Window returns and a small entry mat
    # give the room thickness and a readable porch-to-room seam without adding
    # collision or narrowing the route.
    for name, center, size in (
        ("FapInteriorShell_BaseCapBack_LOD0", (0.0, 5.58, 0.97), (11.18, 0.12, 0.14)),
        ("FapInteriorShell_BaseCapLeft_LOD0", (-5.58, 0.0, 0.97), (0.12, 11.18, 0.14)),
        ("FapInteriorShell_BaseCapRight_LOD0", (5.58, 0.0, 0.97), (0.12, 11.18, 0.14)),
        ("FapInteriorShell_BaseCapFrontLeft_LOD0", (-3.42, -5.58, 0.97), (4.66, 0.12, 0.14)),
        ("FapInteriorShell_BaseCapFrontRight_LOD0", (3.42, -5.58, 0.97), (4.66, 0.12, 0.14)),
    ):
        ib(name, parent, center, size, "FapPaintedTimber", 0.020, role="joined lower-wall cap trim")

    for name, center, size in (
        ("FapInteriorShell_CeilingCorniceBack_LOD0", (0.0, 5.57, 3.10), (11.18, 0.14, 0.18)),
        ("FapInteriorShell_CeilingCorniceLeft_LOD0", (-5.57, 0.0, 3.10), (0.14, 11.18, 0.18)),
        ("FapInteriorShell_CeilingCorniceRight_LOD0", (5.57, 0.0, 3.10), (0.14, 11.18, 0.18)),
        ("FapInteriorShell_CeilingCorniceFrontLeft_LOD0", (-3.42, -5.57, 3.10), (4.66, 0.14, 0.18)),
        ("FapInteriorShell_CeilingCorniceFrontRight_LOD0", (3.42, -5.57, 3.10), (4.66, 0.14, 0.18)),
    ):
        ib(name, parent, center, size, "FapDarkTimber", 0.022, role="joined ceiling cornice trim")

    for name, center in (
        ("FapInteriorShell_CornerPostBackLeft_LOD0", (-5.57, 5.57, 2.00)),
        ("FapInteriorShell_CornerPostBackRight_LOD0", (5.57, 5.57, 2.00)),
        ("FapInteriorShell_CornerPostFrontLeft_LOD0", (-5.57, -5.57, 2.00)),
        ("FapInteriorShell_CornerPostFrontRight_LOD0", (5.57, -5.57, 2.00)),
    ):
        ib(name, parent, center, (0.18, 0.18, 2.22), "FapPaintedTimber", 0.025, role="interior corner construction post")

    for name, center in (
        ("FapInteriorShell_RevealLeftFront_LOD0", (-5.45, 0.28, 1.95)),
        ("FapInteriorShell_RevealLeftBack_LOD0", (-5.45, 2.42, 1.95)),
        ("FapInteriorShell_RevealRightFront_LOD0", (5.45, 0.28, 1.95)),
        ("FapInteriorShell_RevealRightBack_LOD0", (5.45, 2.42, 1.95)),
    ):
        ib(name, parent, center, (0.26, 0.14, 1.44), "FapPaintedTimber", 0.022, role="deep side-window reveal return")
    ib(
        "FapInteriorShell_RevealLeftSill_LOD0",
        parent,
        (-5.45, 1.35, 1.23),
        (0.26, 2.24, 0.10),
        "FapPaintedTimber",
        0.018,
        role="left side-window recessed sill",
    )
    ib(
        "FapInteriorShell_RevealRightSill_LOD0",
        parent,
        (5.45, 1.35, 1.23),
        (0.26, 2.24, 0.10),
        "FapPaintedTimber",
        0.018,
        role="right side-window recessed sill",
    )
    ib(
        "FapInteriorShell_EntryMat_LOD0",
        parent,
        (0.0, -4.62, 0.009),
        (1.82, 1.04, 0.018),
        "FapWetStone",
        0.020,
        role="plain grounded entry mat at threshold",
    )

    ib(
        "FapInteriorCot_Frame_LOD0",
        parent,
        (-4.18, 1.82, 0.83),
        (2.55, 1.08, 0.18),
        "FapPaintedTimber",
        0.045,
        role="modest examination cot faceted frame",
    )
    ib(
        "FapInteriorCot_Mattress_LOD0",
        parent,
        (-4.18, 1.82, 1.00),
        (2.40, 0.94, 0.18),
        "FapPaintedDustyBlue",
        0.075,
        role="muted enamel-blue cot mattress",
    )
    ib(
        "FapInteriorCot_Pillow_LOD0",
        parent,
        (-4.18, 1.37, 1.16),
        (1.18, 0.50, 0.20),
        "FapNoticeBlank",
        0.07,
        rotation=(math.radians(-9.0), 0.0, 0.0),
        role="angled cot pillow with softened chamfered silhouette",
    )
    ib(
        "FapInteriorCot_HeadRail_LOD0",
        parent,
        (-4.18, 2.31, 1.17),
        (2.55, 0.11, 0.18),
        "FapDarkTimber",
        0.025,
        role="cot head frame rail",
    )
    for name, x, y in (
        ("FapInteriorCot_LegFrontLeft_LOD0", -5.20, 1.44),
        ("FapInteriorCot_LegFrontRight_LOD0", -3.16, 1.44),
        ("FapInteriorCot_LegBackLeft_LOD0", -5.20, 2.20),
        ("FapInteriorCot_LegBackRight_LOD0", -3.16, 2.20),
    ):
        ib(name, parent, (x, y, 0.42), (0.16, 0.16, 0.78), "FapDarkTimber", 0.025, role="cot tapered timber leg")
    ib(
        "FapInteriorCot_LowerBrace_LOD0",
        parent,
        (-4.18, 1.82, 0.48),
        (2.15, 0.10, 0.10),
        "FapPaintedTimber",
        0.018,
        role="cot lower support brace",
    )
    ib(
        "FapInteriorCot_FootRail_LOD0",
        parent,
        (-4.18, 1.32, 1.15),
        (2.48, 0.10, 0.16),
        "FapDarkTimber",
        0.020,
        role="cot foot-end construction rail",
    )
    ib(
        "FapInteriorCot_UnderShelf_LOD0",
        parent,
        (-4.18, 1.82, 0.60),
        (1.80, 0.68, 0.10),
        "FapPaintedTimber",
        0.018,
        role="cot lower utility shelf",
    )

    # Three shallow trapezoidal cloth panels fold just ahead of the cot.  They
    # are deliberately compact and shifted toward the cot so standard entry
    # and left views keep Naila and the document path legible.
    screen_x = lambda x: -1.05 + 0.52 * x
    prism_profile(
        "FapInteriorScreen_PanelLeft_LOD0",
        parent,
        ((screen_x(-3.30), 0.16), (screen_x(-2.32), 0.22), (screen_x(-2.25), 2.08), (screen_x(-3.22), 2.14)),
        1.64,
        1.74,
        ("FapPaintedDustyBlue", "FapPaintedSage"),
        [0, 1, 0, 0, 1, 1],
        "folding privacy screen left faceted cloth panel",
        allow_new=True,
    )
    prism_profile(
        "FapInteriorScreen_PanelCenter_LOD0",
        parent,
        ((screen_x(-2.34), 0.18), (screen_x(-1.37), 0.18), (screen_x(-1.32), 2.10), (screen_x(-2.30), 2.06)),
        1.84,
        1.94,
        ("FapPaintedSage", "FapPaintedDustyBlue"),
        [0, 1, 0, 1, 0, 1],
        "folding privacy screen center faceted cloth panel",
        allow_new=True,
    )
    prism_profile(
        "FapInteriorScreen_PanelRight_LOD0",
        parent,
        ((screen_x(-1.39), 0.17), (screen_x(-0.42), 0.22), (screen_x(-0.48), 2.06), (screen_x(-1.42), 2.12)),
        1.68,
        1.78,
        ("FapPaintedDustyBlue", "FapPaintedSage"),
        [1, 0, 1, 0, 1, 0],
        "folding privacy screen right faceted cloth panel",
        allow_new=True,
    )
    for name, x, y in (
        ("FapInteriorScreen_PostLeft_LOD0", screen_x(-3.30), 1.69),
        ("FapInteriorScreen_PostCenter_LOD0", screen_x(-1.84), 1.89),
        ("FapInteriorScreen_PostRight_LOD0", screen_x(-0.42), 1.73),
    ):
        ib(name, parent, (x, y, 1.10), (0.11, 0.11, 2.10), "FapPaintedTimber", 0.025, role="folding screen timber post")
    for name, start, end in (
        ("FapInteriorScreen_RailTopLeft_LOD0", (screen_x(-3.30), 1.69, 2.13), (screen_x(-2.32), 1.69, 2.13)),
        ("FapInteriorScreen_RailBottomLeft_LOD0", (screen_x(-3.30), 1.69, 0.16), (screen_x(-2.32), 1.69, 0.16)),
        ("FapInteriorScreen_RailTopCenter_LOD0", (screen_x(-2.34), 1.89, 2.13), (screen_x(-1.37), 1.89, 2.13)),
        ("FapInteriorScreen_RailBottomCenter_LOD0", (screen_x(-2.34), 1.89, 0.16), (screen_x(-1.37), 1.89, 0.16)),
        ("FapInteriorScreen_RailTopRight_LOD0", (screen_x(-1.39), 1.73, 2.10), (screen_x(-0.42), 1.73, 2.10)),
        ("FapInteriorScreen_RailBottomRight_LOD0", (screen_x(-1.39), 1.73, 0.16), (screen_x(-0.42), 1.73, 0.16)),
    ):
        ibeam(name, parent, start, end, 0.10, 0.10, "FapDarkTimber", role="folding screen top or bottom rail")

    ib(
        "FapInteriorCabinet_Body_LOD0",
        parent,
        (4.48, 4.95, 1.56),
        (1.38, 0.42, 1.52),
        "FapPaintedSage",
        0.055,
        role="wall medicine cabinet enamel carcass",
    )
    ib(
        "FapInteriorCabinet_DoorLeft_LOD0",
        parent,
        (4.12, 4.69, 1.57),
        (0.61, 0.07, 1.20),
        "FapShedWall",
        0.035,
        role="left inset medicine cabinet door",
    )
    ib(
        "FapInteriorCabinet_DoorRight_LOD0",
        parent,
        (4.84, 4.69, 1.57),
        (0.61, 0.07, 1.20),
        "FapShedWall",
        0.035,
        role="right inset medicine cabinet door",
    )
    ib(
        "FapInteriorCabinet_InsetLeft_LOD0",
        parent,
        (4.12, 4.64, 1.57),
        (0.44, 0.028, 0.92),
        "FapDoorInset",
        0.018,
        role="left medicine cabinet door inset",
    )
    ib(
        "FapInteriorCabinet_InsetRight_LOD0",
        parent,
        (4.84, 4.64, 1.57),
        (0.44, 0.028, 0.92),
        "FapDoorInset",
        0.018,
        role="right medicine cabinet door inset",
    )
    ib("FapInteriorCabinet_HandleLeft_LOD0", parent, (4.38, 4.60, 1.57), (0.07, 0.08, 0.18), "FapRainMetal", 0.018, role="left cabinet handle")
    ib("FapInteriorCabinet_HandleRight_LOD0", parent, (4.58, 4.60, 1.57), (0.07, 0.08, 0.18), "FapRainMetal", 0.018, role="right cabinet handle")
    ib(
        "FapInteriorCabinet_TopShelf_LOD0",
        parent,
        (4.48, 4.95, 2.40),
        (1.60, 0.52, 0.10),
        "FapRainMetalDark",
        0.025,
        role="plain medicine cabinet top shelf",
    )

    ib(
        "FapInteriorTrolley_TopTray_LOD0",
        parent,
        (4.62, 2.10, 1.18),
        (1.35, 0.55, 0.12),
        "FapRainMetal",
        0.04,
        role="enamel instrument trolley top tray",
    )
    ib(
        "FapInteriorTrolley_Basin_LOD0",
        parent,
        (4.62, 2.10, 1.31),
        (0.72, 0.36, 0.12),
        "FapPaintedSage",
        0.055,
        role="small faceted enamel instrument basin",
    )
    ib(
        "FapInteriorTrolley_BasinInset_LOD0",
        parent,
        (4.62, 1.90, 1.38),
        (0.44, 0.20, 0.035),
        "FapWindowCool",
        0.012,
        role="cool inset in enamel instrument basin",
    )
    ib(
        "FapInteriorTrolley_LowerShelf_LOD0",
        parent,
        (4.62, 2.10, 0.54),
        (1.14, 0.44, 0.10),
        "FapRainMetalDark",
        0.035,
        role="enamel trolley lower shelf",
    )
    for name, x, y in (
        ("FapInteriorTrolley_LegFrontLeft_LOD0", 4.07, 1.90),
        ("FapInteriorTrolley_LegFrontRight_LOD0", 5.17, 1.90),
        ("FapInteriorTrolley_LegBackLeft_LOD0", 4.07, 2.30),
        ("FapInteriorTrolley_LegBackRight_LOD0", 5.17, 2.30),
    ):
        ib(name, parent, (x, y, 0.725), (0.11, 0.11, 0.97), "FapRainMetalDark", 0.022, role="instrument trolley metal leg")
    # One continuous push handle, including the two uprights welded into the
    # tray. The former horizontal grip floated 37cm above every support after
    # ordinary floor grounding; preserve its top, width and published node.
    prism_profile(
        "FapInteriorTrolley_Handle_LOD0",
        parent,
        ((4.13, 1.16), (4.195, 1.16), (4.195, 1.61), (5.045, 1.61),
         (5.045, 1.16), (5.11, 1.16), (5.11, 1.71), (4.13, 1.71)),
        2.27, 2.37,
        ("FapRainMetalDark",), [0] * 10,
        "continuous trolley push handle with two tray-supported uprights",
        allow_new=True,
    )
    for name, x, y in (
        ("FapInteriorTrolley_WheelFrontLeft_LOD0", 4.07, 1.90),
        ("FapInteriorTrolley_WheelFrontRight_LOD0", 5.17, 1.90),
        ("FapInteriorTrolley_WheelBackLeft_LOD0", 4.07, 2.30),
        ("FapInteriorTrolley_WheelBackRight_LOD0", 5.17, 2.30),
    ):
        ib(name, parent, (x, y, 0.18), (0.18, 0.18, 0.12), "FapDarkTimber", 0.035, role="small trolley wheel")

    ib(
        "FapInteriorBench_Seat_LOD0",
        parent,
        (3.72, -2.35, 0.60),
        (2.24, 0.58, 0.16),
        "FapPaintedTimber",
        0.045,
        role="compact rural waiting bench seat",
    )
    ib(
        "FapInteriorBench_Back_LOD0",
        parent,
        (3.72, -2.63, 0.90),
        (2.24, 0.12, 0.50),
        "FapDarkTimber",
        0.04,
        role="compact rural waiting bench back",
    )
    ib("FapInteriorBench_ArmLeft_LOD0", parent, (2.70, -2.35, 0.88), (0.16, 0.52, 0.12), "FapPaintedTimber", 0.025, role="waiting bench left arm")
    ib("FapInteriorBench_ArmRight_LOD0", parent, (4.74, -2.35, 0.88), (0.16, 0.52, 0.12), "FapPaintedTimber", 0.025, role="waiting bench right arm")
    for name, x, y in (
        ("FapInteriorBench_LegFrontLeft_LOD0", 2.86, -2.08),
        ("FapInteriorBench_LegFrontRight_LOD0", 4.58, -2.08),
        ("FapInteriorBench_LegBackLeft_LOD0", 2.86, -2.55),
        ("FapInteriorBench_LegBackRight_LOD0", 4.58, -2.55),
    ):
        ib(name, parent, (x, y, 0.29), (0.14, 0.14, 0.54), "FapDarkTimber", 0.025, role="waiting bench timber leg")
    ib(
        "FapInteriorBench_LowerBrace_LOD0",
        parent,
        (3.72, -2.35, 0.40),
        (1.84, 0.12, 0.10),
        "FapDarkTimber",
        0.018,
        role="waiting bench lower construction brace",
    )
    ib(
        "FapInteriorBench_FootRail_LOD0",
        parent,
        (3.72, -2.35, 0.20),
        (1.94, 0.14, 0.10),
        "FapPaintedTimber",
        0.018,
        role="waiting bench grounded foot rail",
    )

    # The document interaction sits on a real, compact records desk in the
    # rear bay.  The existing runtime desk slab and record target remain
    # untouched; these authored pieces give that target a grounded cabinet,
    # apron, tray and paper mass rather than an isolated plane.
    ib(
        "FapInteriorRecordsDesk_Body_LOD0",
        parent,
        (0.0, 4.12, 0.50),
        (3.20, 1.00, 0.76),
        "FapDarkTimber",
        0.065,
        role="full-volume rural records desk cabinet",
    )
    ib(
        "FapInteriorRecordsDesk_Top_LOD0",
        parent,
        (0.0, 4.12, 0.93),
        (3.52, 1.12, 0.14),
        "FapPaintedTimber",
        0.045,
        role="worn records desk work surface",
    )
    ib(
        "FapInteriorRecordsDesk_Base_LOD0",
        parent,
        (0.0, 4.12, 0.14),
        (3.05, 0.86, 0.16),
        "FapDarkTimber",
        0.035,
        role="recessed records desk plinth",
    )
    ib(
        "FapInteriorRecordsDesk_FrontApron_LOD0",
        parent,
        (0.0, 3.58, 0.55),
        (2.74, 0.10, 0.60),
        "FapPaintedDustyBlue",
        0.025,
        role="painted records desk front apron",
    )
    ib(
        "FapInteriorRecordsDesk_DrawerBand_LOD0",
        parent,
        (0.0, 3.54, 0.76),
        (2.20, 0.08, 0.16),
        "FapPaintedTimber",
        0.018,
        role="plain records desk drawer band",
    )
    for name, x in (
        ("FapInteriorRecordsDesk_FrontLegLeft_LOD0", -1.30),
        ("FapInteriorRecordsDesk_FrontLegRight_LOD0", 1.30),
    ):
        ib(name, parent, (x, 3.72, 0.46), (0.14, 0.20, 0.72), "FapDarkTimber", 0.022, role="records desk front timber leg")
    ib(
        "FapInteriorRecordsDesk_BackRail_LOD0",
        parent,
        (0.0, 4.61, 1.16),
        (2.90, 0.10, 0.28),
        "FapDarkTimber",
        0.022,
        role="records desk rear raised rail",
    )
    ib(
        "FapInteriorRecordsDesk_RecordTray_LOD0",
        parent,
        (-0.84, 3.92, 1.05),
        (0.84, 0.50, 0.12),
        "FapNoticeBlank",
        0.028,
        role="blank records tray on desk",
    )
    ib(
        "FapInteriorRecordsDesk_PaperStack_LOD0",
        parent,
        (0.20, 3.94, 1.04),
        (0.60, 0.38, 0.09),
        "FapNoticeBlank",
        0.020,
        role="unmarked paper stack on records desk",
    )

    # Presentation-only reverse/side dressing keeps the room legible from a
    # first-person turn without narrowing the center route.  The open shelf
    # sits above the existing document desk; the radiator and chart use the
    # otherwise bare left wall; the hook rail anchors the front wall.
    ib(
        "FapInteriorRadiator_Body_LOD0",
        parent,
        (-5.56, 0.70, 0.94),
        (0.20, 1.76, 0.52),
        "FapRainMetal",
        0.035,
        role="compact wall-mounted radiator body",
    )
    for name, y in (
        ("FapInteriorRadiator_RibLeft_LOD0", 0.18),
        ("FapInteriorRadiator_RibCenter_LOD0", 0.70),
        ("FapInteriorRadiator_RibRight_LOD0", 1.22),
    ):
        ib(
            name,
            parent,
            (-5.40, y, 0.94),
            (0.12, 0.10, 0.46),
            "FapRainMetal",
            0.018,
            role="faceted radiator face rib",
        )
    for name, y in (
        ("FapInteriorRadiator_PipeLeft_LOD0", 0.00),
        ("FapInteriorRadiator_PipeRight_LOD0", 1.40),
    ):
        ib(
            name,
            parent,
            (-5.27, y, 1.05),
            (0.10, 0.10, 1.48),
            "FapRainMetalDark",
            0.018,
            role="exposed radiator supply pipe",
        )

    for name, z in (
        ("FapInteriorSupplyShelf_Low_LOD0", 1.22),
        ("FapInteriorSupplyShelf_Mid_LOD0", 1.66),
        ("FapInteriorSupplyShelf_Top_LOD0", 2.10),
    ):
        ib(
            name,
            parent,
            (1.70, 5.37, z),
            (2.34, 0.30, 0.10),
            "FapDarkTimber",
            0.022,
            role="shallow open clinic supply shelf",
        )
    for name, x in (
        ("FapInteriorSupplyShelf_SideLeft_LOD0", 0.57),
        ("FapInteriorSupplyShelf_SideRight_LOD0", 2.83),
    ):
        ib(
            name,
            parent,
            (x, 5.37, 1.66),
            (0.12, 0.30, 0.98),
            "FapPaintedTimber",
            0.022,
            role="open supply shelf side upright",
        )
    for name, center, size, mat, role in (
        (
            "FapInteriorSupplyBottle_A_LOD0",
            (0.93, 5.15, 1.46),
            (0.30, 0.18, 0.36),
            "FapNoticeBlank",
            "short blank supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyBottle_B_LOD0",
            (1.37, 5.14, 1.43),
            (0.22, 0.16, 0.30),
            "FapPaintedDustyBlue",
            "narrow muted supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyTin_C_LOD0",
            (1.87, 5.14, 1.40),
            (0.38, 0.18, 0.24),
            "FapRainMetal",
            "low enamel supply tin silhouette",
        ),
        (
            "FapInteriorSupplyBottle_D_LOD0",
            (2.33, 5.14, 1.94),
            (0.22, 0.18, 0.46),
            "FapWindowCool",
            "tall cool-toned supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyBottle_E_LOD0",
            (2.68, 5.14, 1.91),
            (0.16, 0.16, 0.40),
            "FapShedWall",
            "small muted supply bottle silhouette",
        ),
    ):
        ib(name, parent, center, size, mat, 0.028, role=role)
    ib(
        "FapInteriorSupplyShelf_BackPanel_LOD0",
        parent,
        (1.70, 5.52, 1.66),
        (2.20, 0.08, 1.12),
        "FapPaintedSage",
        0.022,
        role="grounded open-shelf rear panel",
    )
    for name, z in (
        ("FapInteriorSupplyShelf_FrontLipLow_LOD0", 1.22),
        ("FapInteriorSupplyShelf_FrontLipMid_LOD0", 1.66),
        ("FapInteriorSupplyShelf_FrontLipTop_LOD0", 2.10),
    ):
        ib(
            name,
            parent,
            (1.70, 5.18, z),
            (2.24, 0.06, 0.08),
            "FapPaintedTimber",
            0.014,
            role="open supply shelf front retaining lip",
        )

    ib(
        "FapInteriorExamChart_Panel_LOD0",
        parent,
        (-5.54, -3.22, 2.05),
        (0.12, 1.46, 1.02),
        "FapNoticeBlank",
        0.024,
        role="small blank examination chart panel",
    )
    ib(
        "FapInteriorExamChart_FrameTop_LOD0",
        parent,
        (-5.40, -3.22, 2.59),
        (0.16, 1.64, 0.10),
        "FapPaintedTimber",
        0.018,
        role="examination chart upper frame",
    )
    ib(
        "FapInteriorExamChart_FrameBottom_LOD0",
        parent,
        (-5.40, -3.22, 1.51),
        (0.16, 1.64, 0.10),
        "FapPaintedTimber",
        0.018,
        role="examination chart lower frame",
    )
    for name, y in (
        ("FapInteriorExamChart_FrameLeft_LOD0", -3.99),
        ("FapInteriorExamChart_FrameRight_LOD0", -2.45),
    ):
        ib(
            name,
            parent,
            (-5.40, y, 2.05),
            (0.16, 0.10, 1.18),
            "FapPaintedTimber",
            0.018,
            role="examination chart side frame",
        )

    ib(
        "FapInteriorCoatHookRail_LOD0",
        parent,
        (3.25, -5.40, 2.42),
        (2.60, 0.16, 0.14),
        "FapDarkTimber",
        0.022,
        role="front-wall coat hook rail framing the entry reveal",
    )
    for name, x in (
        ("FapInteriorCoatHookLeft_LOD0", 2.28),
        ("FapInteriorCoatHookCenter_LOD0", 3.25),
        ("FapInteriorCoatHookRight_LOD0", 4.22),
    ):
        ib(
            name,
            parent,
            (x, -5.22, 2.16),
            (0.16, 0.24, 0.22),
            "FapRainMetal",
            0.028,
            role="front-wall coat hook peg",
        )

    # A quiet wash station, attendant stool and blank records pinboard add
    # believable reverse-view anchors while leaving Naila, the desk and the
    # entry corridor open.  Faces stay deliberately unmarked and text-free.
    build_wash_basin(parent)
    ib(
        "FapInteriorWashUnit_Splash_LOD0",
        parent,
        (5.39, -1.56, 1.70),
        (0.10, 1.24, 0.92),
        "FapPaintedDustyBlue",
        0.035,
        role="plain enamel wash splashback",
    )
    # Keep the existing towel fitting beside the basin on its real wall. The
    # previous bar crossed the faucet in empty space. Two short returns now
    # reach the wall; the towel folds over the bar rather than floating below.
    ib("FapInteriorWashUnit_TowelRail_LOD0", parent, (5.31, -2.62, 1.54),
       (.08, .76, .08), "FapRainMetalDark", .016, role="wall-mounted wash-station towel rail")
    rail_vertices, rail_faces = [], []
    for (x, y, z), (width, depth, height) in (
        ((5.31, -2.62, 1.54), (.08, .76, .08)),
        ((5.39, -2.94, 1.54), (.18, .06, .06)),
        ((5.39, -2.30, 1.54), (.18, .06, .06)),
    ):
        offset = len(rail_vertices)
        rail_vertices += [(x + dx * width / 2, y + dy * depth / 2, z + dz * height / 2)
                          for dx, dy, dz in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                             (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))]
        rail_faces += [tuple(offset + i for i in face) for face in
                       ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))]
    mesh_object("FapInteriorWashUnit_TowelRail_LOD0", parent, rail_vertices, rail_faces,
                ("FapRainMetalDark",), role="one towel bar with two short wall returns")
    towel_profile = ((5.25, 1.08), (5.25, 1.56), (5.266, 1.592), (5.29, 1.602),
                     (5.36, 1.602), (5.386, 1.576), (5.386, 1.34), (5.374, 1.34),
                     (5.374, 1.572), (5.353, 1.580), (5.295, 1.580), (5.278, 1.570),
                     (5.262, 1.556), (5.262, 1.08))
    prism_profile(
        "FapInteriorWashUnit_Towel_LOD0",
        parent,
        tuple(reversed(towel_profile)), -2.85, -2.39,
        ("FapNoticeBlank",), [0] * (len(towel_profile) + 2),
        "muted clinic towel with a supported top fold and short rear return",
        allow_new=True,
    )
    ib(
        "FapInteriorWashUnit_FaucetStem_LOD0",
        parent,
        (5.28, -1.56, 1.015),
        (0.065, 0.065, 0.15),
        "FapRainMetal",
        0.014,
        role="compact mixer mounted on the basin's rear rim",
    )
    ib(
        "FapInteriorWashUnit_FaucetSpout_LOD0",
        parent,
        (5.20, -1.56, 1.065),
        (0.22, 0.065, 0.05),
        "FapRainMetal",
        0.012,
        role="short mixer spout pointing into the actual bowl",
    )
    # Keep the authored floor-plan anchor, but use a low rounded cushion and
    # compact grounded support so the stool reads as household clinic furniture.
    # The initial ib() call keeps the existing allow_new contract; mesh_object()
    # then replaces only the seat silhouette with four tapered 12-sided rings.
    ib(
        "FapInteriorStool_Seat_LOD0",
        parent,
        (-2.15, 0.42, 0.56),
        (0.50, 0.50, 0.08),
        "FapPaintedDustyBlue",
        0.030,
        role="small attendant stool padded seat",
    )
    seat_center_z = 0.56
    seat_rings = ((0.52, 0.23), (0.535, 0.25), (0.585, 0.25), (0.60, 0.23))
    seat_vertices = [
        (radius * math.cos(2.0 * math.pi * index / 12.0),
         radius * math.sin(2.0 * math.pi * index / 12.0),
         z - seat_center_z)
        for z, radius in seat_rings
        for index in range(12)
    ]
    seat_faces: list[tuple[int, ...]] = [tuple(reversed(range(12))), tuple(range(36, 48))]
    seat_faces.extend(
        (ring * 12 + index,
         ring * 12 + (index + 1) % 12,
         (ring + 1) * 12 + (index + 1) % 12,
         (ring + 1) * 12 + index)
        for ring in range(3)
        for index in range(12)
    )
    mesh_object(
        "FapInteriorStool_Seat_LOD0",
        parent,
        seat_vertices,
        seat_faces,
        ("FapPaintedDustyBlue",),
        location=(-2.15, 0.42, seat_center_z),
        role="small attendant stool softly rounded padded seat",
    )
    ib(
        "FapInteriorStool_Stem_LOD0",
        parent,
        (-2.15, 0.42, 0.335),
        (0.10, 0.10, 0.37),
        "FapRainMetalDark",
        0.022,
        role="attendant stool compact metal stem",
    )
    ib(
        "FapInteriorStool_Base_LOD0",
        parent,
        (-2.15, 0.42, 0.105),
        (0.50, 0.42, 0.09),
        "FapRainMetal",
        0.028,
        role="attendant stool grounded low faceted base",
    )
    ib(
        "FapInteriorStool_FootRing_LOD0",
        parent,
        (-2.15, 0.42, 0.24),
        (0.42, 0.07, 0.07),
        "FapRainMetalDark",
        0.016,
        role="attendant stool compact foot ring",
    )
    ib(
        "FapInteriorNoticeBoard_Panel_LOD0",
        parent,
        (-1.70, 5.48, 2.30),
        (1.72, 0.10, 1.18),
        "FapNoticeBlank",
        0.035,
        role="blank rural clinic notice panel",
    )
    ib(
        "FapInteriorNoticeBoard_Rail_LOD0",
        parent,
        (-1.70, 5.38, 2.92),
        (1.96, 0.10, 0.10),
        "FapDarkTimber",
        0.022,
        role="plain notice panel top rail",
    )
    ib(
        "FapInteriorNoticeBoard_CardA_LOD0",
        parent,
        (-2.10, 5.30, 2.44),
        (0.44, 0.035, 0.32),
        "FapPaintedDustyBlue",
        0.018,
        role="unmarked muted notice card silhouette",
    )
    ib(
        "FapInteriorNoticeBoard_CardB_LOD0",
        parent,
        (-1.30, 5.30, 2.16),
        (0.32, 0.035, 0.40),
        "FapWindowCool",
        0.018,
        role="unmarked cool notice card silhouette",
    )
    ib(
        "FapInteriorNoticeBoard_RailBottom_LOD0",
        parent,
        (-1.70, 5.38, 1.68),
        (1.96, 0.10, 0.10),
        "FapDarkTimber",
        0.022,
        role="plain notice panel bottom rail",
    )
    for name, x in (
        ("FapInteriorNoticeBoard_RailLeft_LOD0", -2.62),
        ("FapInteriorNoticeBoard_RailRight_LOD0", -0.78),
    ):
        ib(
            name,
            parent,
            (x, 5.38, 2.30),
            (0.10, 0.10, 1.24),
            "FapDarkTimber",
            0.020,
            role="plain notice panel side rail",
        )

    # Large/medium zoning forms give the room a readable front/rear rhythm:
    # the counter stays on the front-right, the tall storage mass on the
    # front-left, and the open partition frames the right-side circulation.
    # All three are presentation-only; the center path remains clear.
    ib(
        "FapInteriorReceptionCounter_Body_LOD0",
        parent,
        (4.25, -0.48, 0.52),
        (1.78, 0.68, 0.90),
        "FapPaintedSage",
        0.065,
        role="low rural clinic reception counter body",
    )
    ib(
        "FapInteriorReceptionCounter_Top_LOD0",
        parent,
        (4.25, -0.48, 0.99),
        (2.02, 0.82, 0.06),
        "FapPaintedTimber",
        0.045,
        role="plain reception counter work surface",
    )
    ib(
        "FapInteriorReceptionCounter_Base_LOD0",
        parent,
        (4.25, -0.48, 0.14),
        (1.52, 0.52, 0.16),
        "FapDarkTimber",
        0.035,
        role="recessed reception counter plinth",
    )
    ib(
        "FapInteriorReceptionCounter_EndPanelLeft_LOD0",
        parent,
        (3.42, -0.48, 0.54),
        (0.16, 0.72, 0.74),
        "FapPaintedTimber",
        0.022,
        role="left reception counter end panel",
    )
    ib(
        "FapInteriorReceptionCounter_EndPanelRight_LOD0",
        parent,
        (5.08, -0.48, 0.54),
        (0.16, 0.72, 0.74),
        "FapPaintedTimber",
        0.022,
        role="right reception counter end panel",
    )
    ib(
        "FapInteriorReceptionCounter_KickPanel_LOD0",
        parent,
        (4.25, -0.83, 0.50),
        (1.34, 0.08, 0.58),
        "FapPaintedDustyBlue",
        0.018,
        role="painted reception counter kick panel",
    )
    ib(
        "FapInteriorReceptionCounter_DrawerBand_LOD0",
        parent,
        (4.25, -0.84, 0.77),
        (1.18, 0.06, 0.16),
        "FapPaintedTimber",
        0.016,
        role="plain reception counter drawer band",
    )

    ib(
        "FapInteriorTallStorage_Body_LOD0",
        parent,
        (-4.48, 3.26, 1.30),
        (1.34, 0.60, 2.52),
        "FapPaintedSage",
        0.06,
        role="tall rural clinic storage cabinet body",
    )
    ib(
        "FapInteriorTallStorage_DoorLeft_LOD0",
        parent,
        (-4.84, 2.91, 1.33),
        (0.58, 0.08, 2.06),
        "FapShedWall",
        0.035,
        role="left tall storage cabinet door",
    )
    ib(
        "FapInteriorTallStorage_DoorRight_LOD0",
        parent,
        (-4.12, 2.91, 1.33),
        (0.58, 0.08, 2.06),
        "FapShedWall",
        0.035,
        role="right tall storage cabinet door",
    )
    ib(
        "FapInteriorTallStorage_TopCap_LOD0",
        parent,
        (-4.48, 3.26, 2.65),
        (1.58, 0.74, 0.16),
        "FapDarkTimber",
        0.035,
        role="plain tall storage cabinet top cap",
    )
    ib(
        "FapInteriorTallStorage_HandleLeft_LOD0",
        parent,
        (-4.60, 2.84, 1.33),
        (0.07, 0.08, 0.18),
        "FapRainMetal",
        0.018,
        role="left tall storage cabinet handle",
    )
    ib(
        "FapInteriorTallStorage_HandleRight_LOD0",
        parent,
        (-4.38, 2.84, 1.33),
        (0.07, 0.08, 0.18),
        "FapRainMetal",
        0.018,
        role="right tall storage cabinet handle",
    )
    ib(
        "FapInteriorTallStorage_Base_LOD0",
        parent,
        (-4.48, 3.26, 0.14),
        (1.48, 0.70, 0.16),
        "FapDarkTimber",
        0.035,
        role="grounded tall storage cabinet plinth",
    )

    ib(
        "FapInteriorPartition_Panel_LOD0",
        parent,
        (4.14, 0.72, 0.86),
        (0.12, 1.78, 1.44),
        "FapPaintedDustyBlue",
        0.04,
        role="partial-height clinic zoning partition panel",
    )
    ib(
        "FapInteriorPartition_PostFront_LOD0",
        parent,
        (4.14, -0.18, 0.90),
        (0.16, 0.16, 1.76),
        "FapPaintedTimber",
        0.025,
        role="front zoning partition timber post",
    )
    ib(
        "FapInteriorPartition_PostBack_LOD0",
        parent,
        (4.14, 1.62, 0.90),
        (0.16, 0.16, 1.76),
        "FapPaintedTimber",
        0.025,
        role="rear zoning partition timber post",
    )
    ib(
        "FapInteriorPartition_RailTop_LOD0",
        parent,
        (4.14, 0.72, 1.72),
        (0.18, 1.96, 0.12),
        "FapDarkTimber",
        0.025,
        role="zoning partition top rail",
    )
    ib(
        "FapInteriorPartition_RailBottom_LOD0",
        parent,
        (4.14, 0.72, 0.12),
        (0.18, 1.96, 0.12),
        "FapDarkTimber",
        0.020,
        role="zoning partition grounded lower rail",
    )

    # Linoleum fields lie directly on the metric room floor at local Z=0.
    # The old 0.12m underside belonged to an earlier raised preview floor and
    # left every field floating after the actual room was seated on its slab.
    # Broad low floor fields, an overhead beam and a few
    # joined furniture/wall forms give the entry-to-records sightline a human
    # scale.  None of these presentation meshes narrows the center aisle.
    ib(
        "FapInteriorFloor_EntryRunner_LOD0",
        parent,
        (0.0, -1.95, 0.002),
        (2.12, 4.30, 0.004),
        "FapPaintedDustyBlue",
        0.018,
        role="worn linoleum entry-to-center runner",
    )
    ib(
        "FapInteriorFloor_ExamMat_LOD0",
        parent,
        (-3.65, 2.0, 0.002),
        (3.20, 2.65, 0.004),
        "FapShedWall",
        0.020,
        role="muted exam-bay floor field beneath cot",
    )
    ib(
        "FapInteriorFloor_WaitingMat_LOD0",
        parent,
        (3.25, -2.25, 0.002),
        (3.20, 1.95, 0.004),
        "FapPaintedSage",
        0.020,
        role="muted waiting-bay floor field beneath bench",
    )
    ib(
        "FapInteriorFloor_RecordsMat_LOD0",
        parent,
        (0.0, 4.05, 0.002),
        (3.65, 1.50, 0.004),
        "FapShedWall",
        0.020,
        role="worn records-bay floor field beneath desk",
    )
    ib(
        "FapInteriorCeilingBeam_Entry_LOD0",
        parent,
        (0.0, -2.30, 3.17),
        (4.80, 0.22, 0.20),
        "FapDarkTimber",
        0.030,
        role="short entry threshold construction beam",
    )
    ib(
        "FapInteriorCot_Backboard_LOD0",
        parent,
        (-4.18, 2.52, 1.55),
        (2.32, 0.16, 1.38),
        "FapPaintedSage",
        0.045,
        role="grounded exam cot headboard panel",
    )
    ib(
        "FapInteriorCot_InstrumentShelf_LOD0",
        parent,
        (-3.35, 2.48, 2.36),
        (1.28, 0.28, 0.10),
        "FapPaintedTimber",
        0.018,
        role="small exam-bay instrument shelf",
    )
    ib(
        "FapInteriorBench_BackRail_LOD0",
        parent,
        (3.72, -2.63, 1.17),
        (2.05, 0.16, 0.12),
        "FapPaintedTimber",
        0.020,
        role="joined waiting bench upper back rail",
    )
    ib(
        "FapInteriorRecordsDesk_CubbyBody_LOD0",
        parent,
        (-1.05, 4.66, 1.55),
        (1.52, 0.18, 1.20),
        "FapPaintedSage",
        0.045,
        role="rear records hutch back panel",
    )
    ib(
        "FapInteriorRecordsDesk_CubbyShelfLow_LOD0",
        parent,
        (-1.05, 4.35, 1.20),
        (1.30, 0.45, 0.08),
        "FapDarkTimber",
        0.018,
        role="lower records hutch shelf",
    )
    ib(
        "FapInteriorRecordsDesk_CubbyShelfHigh_LOD0",
        parent,
        (-1.05, 4.35, 1.76),
        (1.30, 0.45, 0.08),
        "FapDarkTimber",
        0.018,
        role="upper records hutch shelf",
    )
    ib(
        "FapInteriorReceptionCounter_ServiceShelf_LOD0",
        parent,
        (4.25, -0.15, 1.035),
        (1.25, 0.20, 0.03),
        "FapPaintedTimber",
        0.008,
        role="low service ledge resting on reception worktop",
    )
    ib(
        "FapInteriorWallPanel_LeftEntry_LOD0",
        parent,
        (-5.55, -4.0, 1.55),
        (0.10, 1.55, 1.25),
        "FapPaintedDustyBlue",
        0.020,
        role="left entry wall maintenance panel",
    )
    ib(
        "FapInteriorWallPanel_RightEntry_LOD0",
        parent,
        (5.55, -4.0, 1.55),
        (0.10, 1.55, 1.25),
        "FapPaintedDustyBlue",
        0.020,
        role="right entry wall maintenance panel",
    )

    # Wave25 composition pass: the room now reads as one diagonal sequence
    # from entry to Naila to the records desk.  Existing visual-only groups are
    # reframed in-place; collision, interaction coordinates and the shell
    # envelope remain untouched.
    def shift_group(prefix: str, dx: float = 0.0, dy: float = 0.0) -> None:
        for child in parent.children:
            if child.name.startswith(prefix):
                child.location.x += dx
                child.location.y += dy

    shift_group("FapInteriorBench_", dx=-0.65)
    shift_group("FapInteriorReceptionCounter_", dx=-1.30, dy=-0.22)
    shift_group("FapInteriorTrolley_", dx=-1.20, dy=-0.25)
    shift_group("FapInteriorCabinet_", dx=0.25, dy=-2.00)
    # Move the existing full-height storage silhouette onto the left wall so
    # the reverse turn has a grounded anchor instead of an empty plaster bay.
    shift_group("FapInteriorTallStorage_", dx=-0.42, dy=0.39)

    cot_pivot = (-4.18, 1.82)
    reframe_mesh_group(
        parent,
        "FapInteriorCot_",
        cot_pivot,
        math.radians(-8.0),
        translation=(0.24, -0.12),
    )
    reframe_screen_group(
        parent,
        (-1.84, 1.80),
        math.radians(-12.0),
        translation=(0.24, -0.12),
    )

    # The old center-of-ceiling beam was a floating test-room cue.  Keep its
    # published child name for contract stability, but ground it as a dark
    # front-wall header accent inside the new entry reveal.
    ib(
        "FapInteriorCeilingBeam_Entry_LOD0",
        parent,
        (0.0, -5.34, 2.82),
        (2.70, 0.18, 0.14),
        "FapDarkTimber",
        0.025,
        role="grounded front-wall entry reveal header accent",
    )
    for name in (
        "FapInteriorRecordsDesk_RecordTray_LOD0",
        "FapInteriorRecordsDesk_PaperStack_LOD0",
    ):
        bpy.data.objects[name].scale = (1.28, 1.22, 1.0)

    for detail_name in INTERIOR_DETAIL_LOD0_CHILDREN:
        rebuild_interior_lod1(detail_name)

    parent["silhouette_count"] = 31
    parent["mesh_count"] = len(INTERIOR_CHILDREN)
    parent["lod1_mesh_count"] = len(INTERIOR_DETAIL_LOD1_CHILDREN)
    parent["composition_pass"] = "Wave25 authored composition: entry-to-Naila-to-records diagonal, rotated examination bay, left-wall storage anchor, grounded front-wall reveal header and periphery staging; center aisle and interaction sightlines remain open"
    parent["source_coordinate_note"] = "Godot depth is -Blender Y; origin is benchmark-room safe transform; shell matches the 12 m benchmark envelope"


def pierced_wall_geometry(origin, tangent, normal, width, low, high, thickness, holes):
    """Closed wall cells with real rectangular openings and full-depth reveals."""
    vertices, faces = [], []
    us = sorted(set([-width / 2, width / 2] + [u for hole in holes for u in hole[:2]]))
    zs = sorted(set([low, high] + [z for hole in holes for z in hole[2:]]))
    us = [u for u in us if -width / 2 <= u <= width / 2]
    zs = [z for z in zs if low <= z <= high]
    for u0, u1 in zip(us, us[1:]):
        for z0, z1 in zip(zs, zs[1:]):
            if any(a < (u0 + u1) / 2 < b and c < (z0 + z1) / 2 < d for a, b, c, d in holes):
                continue
            base = len(vertices)
            for z in (z0, z1):
                for u, n in ((u0, -thickness / 2), (u1, -thickness / 2),
                             (u1, thickness / 2), (u0, thickness / 2)):
                    vertices.append((origin[0] + tangent[0] * u + normal[0] * n,
                                     origin[1] + tangent[1] * u + normal[1] * n, z))
            for face in ((3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
                         (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
                ordered = tuple(reversed(face)) if tangent[0] * normal[1] - tangent[1] * normal[0] < 0 else face
                faces.append(tuple(base + i for i in ordered))
    return vertices, faces


def reconcile_clinic_envelope(components: dict[str, bpy.types.Object]) -> None:
    """One physical clinic: exact room dimensions, supported fittings, paired openings."""
    facade, room = components[FACADE], components[INTERIOR]
    floor, half = FAP_ENVELOPE["floor"], FAP_ENVELOPE["outer_half"]
    front_windows = (("Left", -3.45, 1.54, 1.16), ("Right", 3.45, 1.48, 1.22))
    front_holes = [(-.89, .89, floor, floor + 2.83)]
    front_holes += [(x - w / 2, x + w / 2, floor + 1.95 - h / 2, floor + 1.95 + h / 2)
                    for _, x, w, h in front_windows]
    side_hole = (.90 - 1.04, .90 + 1.04, floor + 1.37, floor + 2.53)
    walls = [((0, -5.90), (1, 0), (0, -1), front_holes),
             ((0, 5.90), (-1, 0), (0, 1), []),
             ((-5.90, 0), (0, -1), (-1, 0), [(-side_hole[1], -side_hole[0], side_hole[2], side_hole[3])]),
             ((5.90, 0), (0, 1), (1, 0), [side_hole, (4.32, 5.38, floor, floor + 2.17)])]
    vertices, faces = [], []
    for origin, tangent, normal, holes in walls:
        points, polygons = pierced_wall_geometry(origin, tangent, normal, half * 2, floor, 3.81, .18, holes)
        offset = len(vertices)
        vertices.extend(points)
        faces.extend(tuple(offset + vertex for vertex in polygon) for polygon in polygons)
    mesh_object("FapFacade_Body_LOD0", facade, vertices, faces, ("FapPaintedSage",),
                role="11.98m hollow clinic shell; four walls with door and window reveals")
    b("FapFacade_Foundation_LOD0", facade, (0, 0, .20), (12.30, 12.30, .40), "FapFoundationStone", .07)
    # The linoleum floor is a real 12cm slab at facade Y=.28.. .40.
    # Its former coplanar full foundation top became visible when looking out
    # of the room. Retain the octagonal outer footing and wall-bearing level,
    # while seating the floor on a continuous lower bed inside the clear room.
    outer = ((-6.08,-6.15),(6.08,-6.15),(6.15,-6.08),(6.15,6.08),
             (6.08,6.15),(-6.08,6.15),(-6.15,6.08),(-6.15,-6.08))
    inner = ((-5.63,-5.63),(5.63,-5.63),(5.63,5.63),(-5.63,5.63))
    foundation_vertices = [(x,y,0) for x,y in outer] + [(x,y,.40) for x,y in outer]
    foundation_vertices += [(x,y,.40) for x,y in inner] + [(x,y,.28) for x,y in inner]
    foundation_faces = [tuple(reversed(range(8)))]
    foundation_faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    for edge in range(4):
        first, last = 8 + edge * 2, 8 + edge * 2 + 1
        corner, next_corner = 16 + edge, 16 + (edge + 1) % 4
        foundation_faces += [(first,last,next_corner,corner),
                             (last,8 + (edge * 2 + 2) % 8,next_corner)]
    foundation_faces += [(16+i,16+(i+1)%4,20+(i+1)%4,20+i) for i in range(4)]
    foundation_faces.append((20,21,22,23))
    mesh_object("FapFacade_Foundation_LOD0", facade, foundation_vertices, foundation_faces,
                ("FapFoundationStone",), role="continuous foundation with wall-bearing perimeter and recessed floor support bed")
    b("FapFacade_FrontPlinth_LOD0", facade, (0, -6.01, .37), (11.98, .10, .10), "FapPaintedDustyBlue", .018)
    for name, y in (("Front", -5.99), ("Back", 5.99)):
        b(f"FapFacade_{name}Course_LOD0", facade, (0, y, 3.78), (12.08, .18, .20), "FapPaintedTimber", .025)
    for name, x, y in (("CornerPost_L", -5.92, -5.93), ("CornerPost_R", 5.92, -5.93),
                       ("BackCornerPost_L", -5.92, 5.93), ("BackCornerPost_R", 5.92, 5.93)):
        b(f"FapFacade_{name}_LOD0", facade, (x, y, 2.08), (.23, .23, 3.36), "FapPaintedTimber", .025)
    prism_profile("FapFacade_Gable_LOD0", facade, ((-5.99, 3.75), (5.99, 3.75), (-.15, 5.72)),
                  -5.99, 5.99, ("FapPaintedSage", "FapPaintedTimber"), [0, 0, 1, 1, 1], "gable supported by matched clinic walls")
    # Keep the established roof owner; its four facets retain real thickness.
    roof = bpy.data.objects["FapFacade_Roof_LOD0"]
    roof_top = ((-6.31, -6.26, 3.82), (-.20, -6.26, 5.78), (6.31, -6.26, 3.82),
                (-6.31, 6.26, 3.82), (-.20, 6.26, 5.78), (6.31, 6.26, 3.82))
    if len(roof.data.vertices) != 12:
        raise RuntimeError("Unexpected baseline FAP roof topology")
    for index, vertex in enumerate(roof.data.vertices):
        x, y, z = roof_top[index % 6]
        vertex.co = (x, y, z - (.16 if index >= 6 else 0))
    b("FapFacade_RidgeCap_LOD0", facade, (-.20, 0, 5.87), (.24, 12.62, .18), "FapRoofEdge", .03)
    for prefix, y in (("RoofEdge", -6.28), ("RoofEdgeBack", 6.28)):
        beam_between(f"FapFacade_{prefix}_L_LOD0", facade, (-6.32, y, 3.82), (-.20, y, 5.78), .20, .18, "FapRoofEdge")
        beam_between(f"FapFacade_{prefix}_R_LOD0", facade, (-.20, y, 5.78), (6.32, y, 3.82), .20, .18, "FapRoofEdge")
    b("FapFacade_GableVent_LOD0", facade, (-.20, -6.04, 4.70), (.58, .10, .34), "FapVentDark", .02)
    b("FapFacade_DoorPanel_LOD0", facade, (0, -5.70, floor + 1.42), (1.50, .07, 2.50), "FapDoorWood", .025)
    b("FapFacade_DoorPanelInset_LOD0", facade, (0, -5.748, floor + .88), (1.08, .035, .72), "FapDoorInset", .018)
    for token, x in (("Left", -.82), ("Right", .82)):
        b(f"FapFacade_DoorFrame{token}_LOD0", facade, (x, -5.97, floor + 1.465), (.14, .38, 2.73), "FapPaintedTimber", .018)
    b("FapFacade_DoorFrameTop_LOD0", facade, (0, -5.97, floor + 2.75), (1.78, .38, .16), "FapPaintedTimber", .018)
    b("FapFacade_DoorHandle_LOD0", facade, (.48, -5.79, floor + 1.50), (.10, .12, .16), "FapRainMetal", .016)

    def window(parent, prefix, center, width, height, side=False):
        x, y, z = center
        def part(suffix, u, v, w, h, depth, material_name):
            location = (x, y + u, z + v) if side else (x + u, y, z + v)
            setback = 0 if suffix == "Glass" else .03 if suffix.startswith("Muntin") else .25
            if parent is room:
                setback = 0 if suffix == "Glass" else -.03 if suffix.startswith("Muntin") else -.18
            location = (location[0] + math.copysign(setback, x), location[1], location[2]) if side else (location[0], location[1] - setback, location[2])
            size = (depth, w, h) if side else (w, depth, h)
            ib(prefix + suffix + "_LOD0", parent, location, size, material_name, .015)
        part("Glass", 0, 0, width, height, .06, "FapWindowCool")
        for token, v in (("Bottom", -height / 2 - .045), ("Top", height / 2 + .045)):
            part("Trim" + token, 0, v, width + .24, .12, .16, "FapPaintedTimber")
        for token, u in (("Left", -width / 2 - .06), ("Right", width / 2 + .06)):
            part("Trim" + token, u, 0, .12, height + .20, .16, "FapPaintedTimber")
        part("MuntinHorizontal", 0, 0, width, .07, .09, "FapPaintedTimber")
        part("MuntinVertical", 0, 0, .07, height, .09, "FapPaintedTimber")

    for side, x, width, height in front_windows:
        window(facade, f"FapFacade_Window{side}_", (x, -5.78, floor + 1.95), width, height)
        window(room, f"FapInteriorShell_WindowFront{side}", (x, -5.78, 1.95), width, height)
    b("FapFacade_WindowRight_WarmInset_LOD0", facade, (3.45, -5.82, floor + 2.23), (.92, .025, .20), "FapWindowWarm", .01)
    for side, x in (("Left", -5.78), ("Right", 5.78)):
        window(facade, f"FapFacade_SideWindow{side}_", (x, .90, floor + 1.95), 2.08, 1.16, True)
        # Existing side windows keep their names and size; all returns follow
        # their new matching plane, including the source's separate sills.
        for child in room.children:
            if child.name.startswith(f"FapInteriorShell_Window{side}"):
                child.location.x = x if "Glass" in child.name else math.copysign(5.60, x)
                child.location.y -= .45
            elif child.name.startswith(f"FapInteriorShell_Reveal{side}"):
                child.location.x = math.copysign(5.62, x)
                child.location.y -= .45
    side_door = (5.78, 4.85, floor + 1.025)
    b("FapFacade_ServiceDoor_LOD0", facade, side_door, (.07, .94, 2.05), "FapDoorWood", .02)
    b("FapFacade_ServiceDoorFrame_LOD0", facade, (5.95, 4.85, floor + 2.11), (.36, 1.18, .12), "FapPaintedTimber", .018)
    for token, y in (("0_7", 4.32), ("1_95", 5.38)):
        b(f"FapFacade_SideBand_{token}_LOD0", facade, (5.95, y, floor + 1.085), (.36, .12, 2.17), "FapPaintedTimber", .018)
    ib("FapInteriorShell_ServiceDoorPanel_LOD0", room, (5.78, 4.85, 1.025), (.07, .94, 2.05), "FapDoorWood", .02)
    for token, y in (("JambFront", 4.32), ("JambBack", 5.38)):
        ib(f"FapInteriorShell_ServiceDoor{token}_LOD0", room, (5.64, y, 1.085), (.14, .12, 2.17), "FapPaintedTimber", .018)
    ib("FapInteriorShell_ServiceDoorHeader_LOD0", room, (5.64, 4.85, 2.11), (.14, 1.18, .12), "FapPaintedTimber", .018)
    ib("FapInteriorShell_ServiceDoorHandle_LOD0", room, (5.70, 4.52, 1.05), (.12, .10, .14), "FapRainMetal", .014)

    # Rebuild the room wall owners with the same reveals. No solid wall remains
    # behind a window, and no old lower band crosses the closed service door.
    inner_front_holes = [(a, b, c - floor, d - floor) for a, b, c, d in front_holes]
    specs = [("FrontWallLeft", (0, -5.72), (1, 0), (0, -1), 11.62, 0, 3.27, .18, inner_front_holes),
             ("BackWall", (0, 5.72), (-1, 0), (0, 1), 11.62, 0, 3.27, .18, []),
             ("LeftWall", (-5.72, 0), (0, -1), (-1, 0), 11.62, 0, 3.27, .18, [(-1.94, .14, 1.37, 2.53)]),
             ("RightWall", (5.72, 0), (0, 1), (1, 0), 11.62, 0, 3.27, .18, [(-.14, 1.94, 1.37, 2.53), (4.32, 5.38, 0, 2.17)]),
             ("LowerBandRight", (5.59, 0), (0, 1), (1, 0), 11.24, .24, .92, .06, [(4.32, 5.38, 0, 2.17)]),
             ("BaseCapRight", (5.58, 0), (0, 1), (1, 0), 11.18, .90, 1.04, .12, [(4.32, 5.38, 0, 2.17)])]
    for name, origin, tangent, normal, width, low, high, thickness, holes in specs:
        vertices, faces = pierced_wall_geometry(origin, tangent, normal, width, low, high, thickness, holes)
        mat = "FapPaintedDustyBlue" if name.startswith("Lower") else "FapPaintedTimber" if name.startswith("Base") else "FapPaintedSage"
        mesh_object(f"FapInteriorShell_{name}_LOD0", room, vertices, faces, (mat,), role="matching clinic wall with physical openings")
    # The former right front wall/header names now denote their real jamb and
    # lintel, avoiding a second solid slab across the new front windows.
    ib("FapInteriorShell_FrontWallRight_LOD0", room, (.82, -5.72, 1.50), (.14, .18, 2.66), "FapPaintedTimber", .015)
    ib("FapInteriorShell_FrontWallHeader_LOD0", room, (0, -5.72, 2.75), (1.78, .18, .16), "FapPaintedTimber", .015)
    ib("FapInteriorShell_DoorInset_LOD0", room, (0, -5.70, 1.42), (1.50, .07, 2.50), "FapDoorWood", .025)
    for token, x in (("Left", -.82), ("Right", .82)):
        ib(f"FapInteriorShell_DoorFrame{token}_LOD0", room, (x, -5.63, 1.50), (.14, .18, 2.66), "FapPaintedTimber", .015)
    ib("FapInteriorShell_DoorFrameTop_LOD0", room, (0, -5.63, 2.75), (1.78, .18, .16), "FapPaintedTimber", .015)
    ib("FapInteriorShell_DoorHandle_LOD0", room, (.48, -5.60, 1.50), (.10, .12, .16), "FapRainMetal", .014)
    # A full-width coat rail cannot hang across the added front glazing. Keep
    # the existing rail as a rigid fitting on the empty rear wall.
    reframe_mesh_group(room, "FapInteriorCoatHook", (3.25, -5.40), math.pi, (-7.0, 10.95))
    # The medicine cupboard was suspended away from its wall after Wave25.
    # Rotate the unchanged cabinet onto the right wall, behind the side window.
    reframe_mesh_group(room, "FapInteriorCabinet_", (4.73, 2.90), -math.pi / 2, (.67, .30))
    for token, y in (("Front", -1.20), ("Back", -1.92)):
        ib(f"FapInteriorWashUnit_Foot{token}_LOD0", room, (5.055, y, .095), (.46, .12, .19), "FapRainMetalDark", .018)
    for child in room.children:
        if child.name.startswith("FapInteriorWashUnit_"):
            child.location.x += .19
    for name in INTERIOR_DETAIL_LOD0_CHILDREN:
        rebuild_interior_lod1(name)
    facade["envelope_contract"] = "fap-room-metric-v1: 11.98 outer shell; 11.55 floor; 0.40 floor offset; four paired windows"
    room["envelope_contract"] = facade["envelope_contract"]
    room["source_coordinate_note"] = "Godot depth is -Blender Y; room origin is exterior origin plus 0.40m vertical floor offset; furniture retains metric size"


def validate(root: bpy.types.Object, components: dict[str, bpy.types.Object]) -> None:
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.data.objects):
        raise RuntimeError("Cameras/lights are not allowed in the authored kit")
    if any(image.type != "RENDER_RESULT" for image in bpy.data.images):
        raise RuntimeError("Image textures are not allowed in the authored kit")
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        triangulate_mesh_data(obj.data)
        if any(len(poly.vertices) > 3 for poly in obj.data.polygons):
            raise RuntimeError(f"Untriangulated polygon remains: {obj.name}")
        if not obj.data.loop_triangles:
            raise RuntimeError(f"Mesh has no triangles: {obj.name}")
        minimum_area = min(loop_triangle_area(obj.data, triangle) for triangle in obj.data.loop_triangles)
        if minimum_area <= 1.0e-10:
            raise RuntimeError(f"Degenerate triangle remains: {obj.name} area={minimum_area:.3e}")
    if components[INTERIOR].type != "EMPTY" or components[INTERIOR].parent is not root:
        raise RuntimeError("FapInteriorSet must remain one direct scene-level component")
    expected = {
        FACADE: FACADE_CHILDREN + FACADE_ENVELOPE_CHILDREN,
        PORCH: PORCH_CHILDREN,
        SHED: SHED_CHILDREN,
        INTERIOR: INTERIOR_CHILDREN,
    }
    for component_name, names in expected.items():
        actual_children = {child.name for child in components[component_name].children}
        expected_children = set(names)
        if component_name == INTERIOR:
            expected_children |= set(INTERIOR_DETAIL_LOD1_CHILDREN)
        if actual_children != expected_children:
            raise RuntimeError(f"{component_name} child contract changed: {sorted(actual_children ^ expected_children)}")
        children_to_validate = names
        if component_name == INTERIOR:
            children_to_validate = names + INTERIOR_DETAIL_LOD1_CHILDREN
        for child_name in children_to_validate:
            obj = bpy.data.objects[child_name]
            if obj.parent is not components[component_name]:
                raise RuntimeError(f"Wrong component parent: {child_name}")
            obj.data.calc_loop_triangles()
            if not obj.data.loop_triangles:
                raise RuntimeError(f"Zero-area key mesh: {child_name}")
            area = sum(poly.area for poly in obj.data.polygons)
            if area <= 1.0e-8:
                raise RuntimeError(f"Zero-area key mesh: {child_name}")
            if not obj.data.materials:
                raise RuntimeError(f"Missing material: {child_name}")
        if component_name == INTERIOR:
            for child_name in INTERIOR_DETAIL_LOD1_CHILDREN:
                obj = bpy.data.objects[child_name]
                source_name = child_name.replace("_LOD1", "_LOD0")
                if obj.get("lod_source") != source_name:
                    raise RuntimeError(f"Incorrect FAP interior LOD1 source: {child_name}")
                if obj.get("collision") != "none" or obj.get("presentation_only") is not True:
                    raise RuntimeError(f"FAP interior LOD1 must remain presentation-only: {child_name}")
    non_lod_meshes = [
        obj.name
        for obj in bpy.data.objects
        if obj.type == "MESH" and not (obj.name.endswith("_LOD0") or obj.name.endswith("_LOD1"))
    ]
    if non_lod_meshes:
        raise RuntimeError(f"Mesh names must end in _LOD0: {non_lod_meshes}")
    interior_meshes = len(INTERIOR_CHILDREN)
    interior_lod1 = len(INTERIOR_DETAIL_LOD1_CHILDREN)
    interior_triangles = sum(len(bpy.data.objects[name].data.loop_triangles) for name in INTERIOR_CHILDREN)
    interior_lod1_triangles = sum(len(bpy.data.objects[name].data.loop_triangles) for name in INTERIOR_DETAIL_LOD1_CHILDREN)
    print(
        "fap-pass: root=%s components=%d facade_meshes=%d porch_meshes=%d shed_meshes=%d interior_meshes=%d interior_lod1=%d interior_triangles=%d interior_lod1_triangles=%d "
        "meshes=%d triangles=%d cameras=0 lights=0 images=0"
        % (
            root.name,
            len(root.children),
            len(FACADE_CHILDREN) + len(FACADE_ENVELOPE_CHILDREN),
            len(PORCH_CHILDREN),
            len(SHED_CHILDREN),
            interior_meshes,
            interior_lod1,
            interior_triangles,
            interior_lod1_triangles,
            sum(obj.type == "MESH" for obj in bpy.data.objects),
            sum(len(obj.data.loop_triangles) for obj in bpy.data.objects if obj.type == "MESH"),
        )
    )


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_fap_clinic_kit.blend"
    glb_path = root_path / "game/assets/models/act1" / GLB_NAME
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise RuntimeError(f"Missing authored root: {ROOT_NAME}")
    components = ensure_contract(root)
    tune_painterly_materials()
    build_facade(components[FACADE])
    build_porch(components[PORCH])
    build_shed(components[SHED])
    build_interior(components[INTERIOR])
    reconcile_clinic_envelope(components)
    rebuild_path_puddles()
    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_fap_clinic_kit.py"
    scene["blender_version_lock"] = "4.5.12 LTS"
    scene["asset_status"] = "metric matched clinic exterior/interior shell with four paired windows; retained authored furnishings, porch and service shed; runtime owns contacts"
    scene["geometry_pass"] = "11.98m hollow envelope matched to the existing metric room; actual door and window reveals; grounded cupboard and wash unit; existing palette and furnishing scale preserved"
    scene["texture_policy"] = "existing named basic materials only; no texture files"
    scene["component_scope"] = "FapFacade_Main|FapEntryPorch|FapServiceShed|FapInteriorSet"
    scene["interior_scope"] = "authored 12 m shell plus thirty-one compact silhouettes: entry-to-Naila-to-records diagonal, left-wall tall storage anchor, rotated examination cot/privacy screen, rear records desk goal, wall radiator/pipes, open supply shelf, blank examination chart, grounded front-wall coat rail/header reveal, wash unit, attendant stool, blank records pinboard and partial-height zoning partition; four broad floor fields, grounded entry header accent, cot backboard/instrument shelf, bench rail, records cubby, service shelf and paired entry wall panels; clear center aisle, wall/periphery staging, joined trim and furniture joinery; 28 authored LOD0/LOD1 detail pairs"
    bpy.context.view_layer.update()
    validate(root, components)
    save_version = bpy.context.preferences.filepaths.save_version
    bpy.context.preferences.filepaths.save_version = 0
    try:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    finally:
        bpy.context.preferences.filepaths.save_version = save_version
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    print(f"fap-pass: saved {blend_path}")
    print(f"fap-pass: exported {glb_path}")


if __name__ == "__main__":
    main()
