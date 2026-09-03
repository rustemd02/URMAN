"""Rebuild the authored Act I FAP clinic kit.

The source blend is the contract baseline.  This pass replaces only mesh data
under the three architectural roots and extends the kit with one direct
presentation-only interior root, keeping every existing FAP child name intact.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/act1/urman_fap_clinic_kit.py -- --root <repo>
"""

from __future__ import annotations

import argparse
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
    "FapInteriorRadiatorBody_LOD0",
    "FapInteriorRadiatorRibLeft_LOD0",
    "FapInteriorRadiatorRibCenter_LOD0",
    "FapInteriorRadiatorRibRight_LOD0",
    "FapInteriorRadiatorPipeLeft_LOD0",
    "FapInteriorRadiatorPipeRight_LOD0",
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

INTERIOR_CHILDREN = INTERIOR_BASELINE_CHILDREN + INTERIOR_ADDED_CHILDREN


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
    mesh.validate(verbose=False)
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
    mesh.calc_loop_triangles()
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
        "restrained painterly low-poly clinic interior v2"
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
    obj["geometry_pass"] = "restrained painterly low-poly clinic silhouettes v2"
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
    vertices = [(x, front_y, z) for x, z in profile]
    vertices.extend((x, back_y, z) for x, z in profile)
    count = len(profile)
    faces: list[tuple[int, ...]] = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    faces.extend((i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count))
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
        if actual_children != set(expected_children):
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
    b("FapEntryPorch_Threshold_LOD0", parent, (-0.12, -0.98, 0.54), (1.48, 0.34, 0.14), "FapWetStone", 0.03, role="grounded entry threshold")
    b("FapEntryPorch_Step_00_LOD0", parent, (-0.02, -1.10, 0.29), (2.98, 0.42, 0.18), "FapWetStone", 0.04, role="top entry step")
    b("FapEntryPorch_Step_01_LOD0", parent, (-0.05, -1.52, 0.18), (2.70, 0.40, 0.18), "FapWetStone", 0.04, role="middle entry step")
    b("FapEntryPorch_Step_02_LOD0", parent, (-0.08, -1.91, 0.09), (2.42, 0.38, 0.16), "FapWetStone", 0.04, role="lowest entry step")
    b("FapEntryPorch_Post_L_LOD0", parent, (-1.42, -0.94, 1.73), (0.22, 0.22, 2.66), "FapPaintedTimber", 0.03, rotation=(0.0, 0.0, math.radians(-0.8)), role="left porch post")
    b("FapEntryPorch_Post_R_LOD0", parent, (1.42, -0.94, 1.74), (0.22, 0.22, 2.68), "FapPaintedTimber", 0.03, rotation=(0.0, 0.0, math.radians(0.6)), role="right porch post")
    b("FapEntryPorch_PostCap_L_LOD0", parent, (-1.42, -0.94, 3.10), (0.34, 0.32, 0.18), "FapDarkTimber", 0.03, role="left porch post cap")
    b("FapEntryPorch_PostCap_R_LOD0", parent, (1.42, -0.94, 3.11), (0.34, 0.32, 0.18), "FapDarkTimber", 0.03, role="right porch post cap")
    b("FapEntryPorch_RailPost_L_LOD0", parent, (-1.46, -1.55, 0.79), (0.15, 0.15, 0.76), "FapPaintedTimber", 0.025, role="left porch rail post")
    b("FapEntryPorch_RailPost_R_LOD0", parent, (1.46, -1.55, 0.79), (0.15, 0.15, 0.76), "FapPaintedTimber", 0.025, role="right porch rail post")
    b("FapEntryPorch_Rail_L_LOD0", parent, (-1.46, -1.23, 0.82), (0.17, 0.98, 0.15), "FapPaintedTimber", 0.025, role="left porch rail")
    b("FapEntryPorch_Rail_R_LOD0", parent, (1.46, -1.23, 0.82), (0.17, 0.98, 0.15), "FapPaintedTimber", 0.025, role="right porch rail")
    b("FapEntryPorch_BraceLeft_LOD0", parent, (-1.03, -1.26, 2.63), (1.12, 0.13, 0.13), "FapDarkTimber", 0.02, rotation=(0.0, 0.0, math.radians(27.0)), role="left porch knee brace")
    b("FapEntryPorch_BraceRight_LOD0", parent, (1.03, -1.26, 2.63), (1.12, 0.13, 0.13), "FapDarkTimber", 0.02, rotation=(0.0, 0.0, math.radians(-27.0)), role="right porch knee brace")
    b("FapEntryPorch_DoorHeaderPlaque_LOD0", parent, (-0.12, -1.84, 2.79), (1.28, 0.08, 0.18), "FapPaintedDustyBlue", 0.02, role="plain porch header plaque")
    # Sloping canopy with a generous rain overhang.  The mesh is a thin prism
    # so it reads as construction, not a floating flat card.
    prism_profile("FapEntryPorch_RainCanopy_LOD0", parent, ((-1.95, 2.88), (1.95, 2.88), (1.95, 2.68), (-1.95, 2.68)), -2.02, 0.16, ("FapRoofEdge",), [0] * 6, "deep sloped entry rain canopy")
    b("FapEntryPorch_CanopyFascia_LOD0", parent, (0.0, -2.03, 2.80), (4.04, 0.18, 0.22), "FapOldRoof", 0.03, role="canopy front fascia")


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


def build_interior(parent: bpy.types.Object) -> None:
    # The source uses the same Blender convention as the exterior kit: +Z is
    # up, while exported Godot depth is -Y.  These anchors therefore mirror
    # the benchmark room's Godot X/Z positions without adding a runtime owner.
    ib(
        "FapInteriorCot_Frame_LOD0",
        parent,
        (-3.70, 1.55, 0.83),
        (2.55, 1.08, 0.18),
        "FapPaintedTimber",
        0.045,
        role="modest examination cot faceted frame",
    )
    ib(
        "FapInteriorCot_Mattress_LOD0",
        parent,
        (-3.70, 1.55, 1.00),
        (2.40, 0.94, 0.18),
        "FapPaintedDustyBlue",
        0.075,
        role="muted enamel-blue cot mattress",
    )
    ib(
        "FapInteriorCot_Pillow_LOD0",
        parent,
        (-3.70, 1.10, 1.16),
        (1.18, 0.50, 0.20),
        "FapNoticeBlank",
        0.07,
        rotation=(math.radians(-9.0), 0.0, 0.0),
        role="angled cot pillow with softened chamfered silhouette",
    )
    ib(
        "FapInteriorCot_HeadRail_LOD0",
        parent,
        (-3.70, 2.04, 1.17),
        (2.55, 0.11, 0.18),
        "FapDarkTimber",
        0.025,
        role="cot head frame rail",
    )
    for name, x, y in (
        ("FapInteriorCot_LegFrontLeft_LOD0", -4.72, 1.17),
        ("FapInteriorCot_LegFrontRight_LOD0", -2.68, 1.17),
        ("FapInteriorCot_LegBackLeft_LOD0", -4.72, 1.93),
        ("FapInteriorCot_LegBackRight_LOD0", -2.68, 1.93),
    ):
        ib(name, parent, (x, y, 0.42), (0.16, 0.16, 0.78), "FapDarkTimber", 0.025, role="cot tapered timber leg")
    ib(
        "FapInteriorCot_LowerBrace_LOD0",
        parent,
        (-3.70, 1.55, 0.48),
        (2.15, 0.10, 0.10),
        "FapPaintedTimber",
        0.018,
        role="cot lower support brace",
    )

    # Three shallow trapezoidal cloth panels fold just ahead of the cot.  They
    # are deliberately compact and shifted toward the cot so standard entry
    # and left views keep Naila and the document path legible.
    screen_x = lambda x: -0.20 + 0.65 * x
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
        (4.55, 5.35, 1.68),
        (1.52, 0.46, 1.65),
        "FapPaintedSage",
        0.055,
        role="wall medicine cabinet enamel carcass",
    )
    ib(
        "FapInteriorCabinet_DoorLeft_LOD0",
        parent,
        (4.15, 5.08, 1.69),
        (0.68, 0.07, 1.30),
        "FapShedWall",
        0.035,
        role="left inset medicine cabinet door",
    )
    ib(
        "FapInteriorCabinet_DoorRight_LOD0",
        parent,
        (4.95, 5.08, 1.69),
        (0.68, 0.07, 1.30),
        "FapShedWall",
        0.035,
        role="right inset medicine cabinet door",
    )
    ib(
        "FapInteriorCabinet_InsetLeft_LOD0",
        parent,
        (4.15, 5.03, 1.69),
        (0.50, 0.028, 1.00),
        "FapDoorInset",
        0.018,
        role="left medicine cabinet door inset",
    )
    ib(
        "FapInteriorCabinet_InsetRight_LOD0",
        parent,
        (4.95, 5.03, 1.69),
        (0.50, 0.028, 1.00),
        "FapDoorInset",
        0.018,
        role="right medicine cabinet door inset",
    )
    ib("FapInteriorCabinet_HandleLeft_LOD0", parent, (4.46, 4.99, 1.69), (0.07, 0.08, 0.18), "FapRainMetal", 0.018, role="left cabinet handle")
    ib("FapInteriorCabinet_HandleRight_LOD0", parent, (4.64, 4.99, 1.69), (0.07, 0.08, 0.18), "FapRainMetal", 0.018, role="right cabinet handle")
    ib(
        "FapInteriorCabinet_TopShelf_LOD0",
        parent,
        (4.55, 5.35, 2.58),
        (1.78, 0.58, 0.10),
        "FapRainMetalDark",
        0.025,
        role="plain medicine cabinet top shelf",
    )

    ib(
        "FapInteriorTrolley_TopTray_LOD0",
        parent,
        (4.45, 1.30, 1.18),
        (1.35, 0.55, 0.12),
        "FapRainMetal",
        0.04,
        role="enamel instrument trolley top tray",
    )
    ib(
        "FapInteriorTrolley_Basin_LOD0",
        parent,
        (4.45, 1.30, 1.31),
        (0.72, 0.36, 0.12),
        "FapPaintedSage",
        0.055,
        role="small faceted enamel instrument basin",
    )
    ib(
        "FapInteriorTrolley_BasinInset_LOD0",
        parent,
        (4.45, 1.10, 1.38),
        (0.44, 0.20, 0.035),
        "FapWindowCool",
        0.012,
        role="cool inset in enamel instrument basin",
    )
    ib(
        "FapInteriorTrolley_LowerShelf_LOD0",
        parent,
        (4.45, 1.30, 0.54),
        (1.14, 0.44, 0.10),
        "FapRainMetalDark",
        0.035,
        role="enamel trolley lower shelf",
    )
    for name, x, y in (
        ("FapInteriorTrolley_LegFrontLeft_LOD0", 3.90, 1.10),
        ("FapInteriorTrolley_LegFrontRight_LOD0", 5.00, 1.10),
        ("FapInteriorTrolley_LegBackLeft_LOD0", 3.90, 1.50),
        ("FapInteriorTrolley_LegBackRight_LOD0", 5.00, 1.50),
    ):
        ib(name, parent, (x, y, 0.86), (0.11, 0.11, 0.70), "FapRainMetalDark", 0.022, role="instrument trolley metal leg")
    ib(
        "FapInteriorTrolley_Handle_LOD0",
        parent,
        (4.45, 1.58, 1.66),
        (0.98, 0.10, 0.10),
        "FapRainMetalDark",
        0.024,
        role="instrument trolley push handle",
    )
    for name, x, y in (
        ("FapInteriorTrolley_WheelFrontLeft_LOD0", 3.90, 1.10),
        ("FapInteriorTrolley_WheelFrontRight_LOD0", 5.00, 1.10),
        ("FapInteriorTrolley_WheelBackLeft_LOD0", 3.90, 1.50),
        ("FapInteriorTrolley_WheelBackRight_LOD0", 5.00, 1.50),
    ):
        ib(name, parent, (x, y, 0.18), (0.18, 0.18, 0.12), "FapDarkTimber", 0.035, role="small trolley wheel")

    ib(
        "FapInteriorBench_Seat_LOD0",
        parent,
        (3.35, -1.80, 0.60),
        (2.60, 0.62, 0.16),
        "FapPaintedTimber",
        0.045,
        role="compact rural waiting bench seat",
    )
    ib(
        "FapInteriorBench_Back_LOD0",
        parent,
        (3.35, -2.10, 1.16),
        (2.60, 0.12, 0.92),
        "FapDarkTimber",
        0.04,
        role="compact rural waiting bench back",
    )
    ib("FapInteriorBench_ArmLeft_LOD0", parent, (2.18, -1.80, 0.88), (0.16, 0.56, 0.12), "FapPaintedTimber", 0.025, role="waiting bench left arm")
    ib("FapInteriorBench_ArmRight_LOD0", parent, (4.52, -1.80, 0.88), (0.16, 0.56, 0.12), "FapPaintedTimber", 0.025, role="waiting bench right arm")
    for name, x, y in (
        ("FapInteriorBench_LegFrontLeft_LOD0", 2.38, -1.52),
        ("FapInteriorBench_LegFrontRight_LOD0", 4.32, -1.52),
        ("FapInteriorBench_LegBackLeft_LOD0", 2.38, -2.02),
        ("FapInteriorBench_LegBackRight_LOD0", 4.32, -2.02),
    ):
        ib(name, parent, (x, y, 0.29), (0.14, 0.14, 0.54), "FapDarkTimber", 0.025, role="waiting bench timber leg")

    # Presentation-only reverse/side dressing keeps the room legible from a
    # first-person turn without narrowing the center route.  The open shelf
    # sits above the existing document desk; the radiator and chart use the
    # otherwise bare left wall; the hook rail anchors the front wall.
    ib(
        "FapInteriorRadiatorBody_LOD0",
        parent,
        (-5.56, 1.30, 0.94),
        (0.20, 1.76, 0.52),
        "FapRainMetal",
        0.035,
        role="compact wall-mounted radiator body",
    )
    for name, y in (
        ("FapInteriorRadiatorRibLeft_LOD0", 0.78),
        ("FapInteriorRadiatorRibCenter_LOD0", 1.30),
        ("FapInteriorRadiatorRibRight_LOD0", 1.82),
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
        ("FapInteriorRadiatorPipeLeft_LOD0", 0.60),
        ("FapInteriorRadiatorPipeRight_LOD0", 2.00),
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
            (1.55, 5.37, z),
            (2.34, 0.30, 0.10),
            "FapDarkTimber",
            0.022,
            role="shallow open clinic supply shelf",
        )
    for name, x in (
        ("FapInteriorSupplyShelf_SideLeft_LOD0", 0.42),
        ("FapInteriorSupplyShelf_SideRight_LOD0", 2.68),
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
            (0.78, 5.15, 1.46),
            (0.30, 0.18, 0.36),
            "FapNoticeBlank",
            "short blank supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyBottle_B_LOD0",
            (1.22, 5.14, 1.43),
            (0.22, 0.16, 0.30),
            "FapPaintedDustyBlue",
            "narrow muted supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyTin_C_LOD0",
            (1.72, 5.14, 1.40),
            (0.38, 0.18, 0.24),
            "FapRainMetal",
            "low enamel supply tin silhouette",
        ),
        (
            "FapInteriorSupplyBottle_D_LOD0",
            (2.18, 5.14, 1.94),
            (0.22, 0.18, 0.46),
            "FapWindowCool",
            "tall cool-toned supply bottle silhouette",
        ),
        (
            "FapInteriorSupplyBottle_E_LOD0",
            (2.53, 5.14, 1.91),
            (0.16, 0.16, 0.40),
            "FapShedWall",
            "small muted supply bottle silhouette",
        ),
    ):
        ib(name, parent, center, size, mat, 0.028, role=role)

    ib(
        "FapInteriorExamChart_Panel_LOD0",
        parent,
        (-5.54, -3.32, 2.05),
        (0.12, 1.46, 1.02),
        "FapNoticeBlank",
        0.024,
        role="small blank examination chart panel",
    )
    ib(
        "FapInteriorExamChart_FrameTop_LOD0",
        parent,
        (-5.40, -3.32, 2.59),
        (0.16, 1.64, 0.10),
        "FapPaintedTimber",
        0.018,
        role="examination chart upper frame",
    )
    ib(
        "FapInteriorExamChart_FrameBottom_LOD0",
        parent,
        (-5.40, -3.32, 1.51),
        (0.16, 1.64, 0.10),
        "FapPaintedTimber",
        0.018,
        role="examination chart lower frame",
    )
    for name, y in (
        ("FapInteriorExamChart_FrameLeft_LOD0", -4.09),
        ("FapInteriorExamChart_FrameRight_LOD0", -2.55),
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
        (3.20, -5.46, 2.15),
        (2.10, 0.16, 0.12),
        "FapDarkTimber",
        0.022,
        role="plain front-wall coat hook rail",
    )
    for name, x in (
        ("FapInteriorCoatHookLeft_LOD0", 2.45),
        ("FapInteriorCoatHookCenter_LOD0", 3.20),
        ("FapInteriorCoatHookRight_LOD0", 3.95),
    ):
        ib(
            name,
            parent,
            (x, -5.29, 1.92),
            (0.16, 0.24, 0.20),
            "FapRainMetal",
            0.028,
            role="small plain coat hook peg",
        )

    parent["silhouette_count"] = 9
    parent["mesh_count"] = len(INTERIOR_CHILDREN)
    parent["source_coordinate_note"] = "Godot depth is -Blender Y; origin is benchmark-room safe transform"


def validate(root: bpy.types.Object, components: dict[str, bpy.types.Object]) -> None:
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.data.objects):
        raise RuntimeError("Cameras/lights are not allowed in the authored kit")
    if any(image.type != "RENDER_RESULT" for image in bpy.data.images):
        raise RuntimeError("Image textures are not allowed in the authored kit")
    if components[INTERIOR].type != "EMPTY" or components[INTERIOR].parent is not root:
        raise RuntimeError("FapInteriorSet must remain one direct scene-level component")
    expected = {
        FACADE: FACADE_CHILDREN,
        PORCH: PORCH_CHILDREN,
        SHED: SHED_CHILDREN,
        INTERIOR: INTERIOR_CHILDREN,
    }
    for component_name, names in expected.items():
        actual_children = {child.name for child in components[component_name].children}
        if actual_children != set(names):
            raise RuntimeError(f"{component_name} child contract changed: {sorted(actual_children ^ set(names))}")
        for child_name in names:
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
    non_lod_meshes = [
        obj.name
        for obj in bpy.data.objects
        if obj.type == "MESH" and not obj.name.endswith("_LOD0")
    ]
    if non_lod_meshes:
        raise RuntimeError(f"Mesh names must end in _LOD0: {non_lod_meshes}")
    interior_meshes = len(INTERIOR_CHILDREN)
    interior_triangles = sum(len(bpy.data.objects[name].data.loop_triangles) for name in INTERIOR_CHILDREN)
    print(
        "fap-pass: root=%s components=%d facade_meshes=%d porch_meshes=%d shed_meshes=%d interior_meshes=%d interior_triangles=%d "
        "meshes=%d triangles=%d cameras=0 lights=0 images=0"
        % (
            root.name,
            len(root.children),
            len(FACADE_CHILDREN),
            len(PORCH_CHILDREN),
            len(SHED_CHILDREN),
            interior_meshes,
            interior_triangles,
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
    build_facade(components[FACADE])
    build_porch(components[PORCH])
    build_shed(components[SHED])
    build_interior(components[INTERIOR])
    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_fap_clinic_kit.py"
    scene["blender_version_lock"] = "4.5.12 LTS"
    scene["asset_status"] = "authored modest weathered rural clinic facade, porch, service shed and interior set v4; presentation-only"
    scene["geometry_pass"] = "deeper eaves, grounded foundation/steps, credible openings, restrained asymmetry"
    scene["texture_policy"] = "existing named basic materials only; no texture files"
    scene["component_scope"] = "FapFacade_Main|FapEntryPorch|FapServiceShed|FapInteriorSet"
    scene["interior_scope"] = "nine compact authored silhouettes: examination cot, folding privacy screen, wall medicine cabinet, enamel instrument trolley, waiting bench, wall radiator and pipes, open supply shelf, blank examination chart and coat hook rail"
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
