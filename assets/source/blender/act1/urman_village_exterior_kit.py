"""Deterministically author the Act I village exterior source kit.

The six canonical components already exist as a Blender source asset. This
script preserves their names and preview-board anchors, refreshes the
full-volume authored pass, and adds optional village parcel and ambient animal
variants for later composition. All geometry is presentation-only and uses
the existing project material library.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/act1/urman_village_exterior_kit.py -- --root <repo>
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


KIT_ROOT = "URMAN_VillageExteriorKit"
DWELLING_ROOT = "DwellingFacade_TimberPlaster"
HERO_DWELLING_ROOT = "HeroHouse_TimberPlaster"
HERO_YARD_SHED_ROOT = "HeroYardShed_Loft"
WELL_ROOT = "Well_YardLandmark"
WOODPILE_ROOT = "Woodpile_StackedLogs"
CAT_ROOT = "AmbientCat"
CROW_ROOT = "AmbientCrow"
GLB_NAME = "urman_village_exterior_kit.glb"

HERO_LOG_MATERIAL = "URMAN_Hero_Log"
HERO_LOG_END_MATERIAL = "URMAN_Hero_LogEnd"
HERO_TRIM_TEAL_MATERIAL = "URMAN_Hero_Trim_Teal"
HERO_TRIM_IVORY_MATERIAL = "URMAN_Hero_Trim_Ivory"
HERO_ROOF_SNOW_MATERIAL = "URMAN_Hero_RoofSnow"
HERO_LOG_COURSE_GAP = 0.008
HERO_LOG_BULGE = 0.060
HERO_LOG_EMBED = 0.020
HERO_LOG_HEIGHT = 0.222
HERO_LOG_END_LENGTH = 0.170

# Window and door casings are nailed over the hewn log faces. A casing whose
# outer face lands on the log face makes the two surfaces coplanar, so the
# rasteriser alternates between them per course and the surround reads as a
# stepped sawtooth instead of a nailed-on trim. The casing therefore clears the
# log face by HERO_CASING_PROUD while staying embedded into the log behind it.
HERO_CASING_PROUD = 0.009
HERO_CASING_BACK = -HERO_LOG_EMBED / 2.0
HERO_CASING_OUTER = HERO_LOG_BULGE + HERO_CASING_PROUD
HERO_CASING_DEPTH = HERO_CASING_OUTER - HERO_CASING_BACK
HERO_CASING_INSET = -(HERO_CASING_OUTER + HERO_CASING_BACK) / 2.0

# Parcel dwellings have a flat pierced shell and no hewn courses, so they keep
# the original casing section and their reviewed appearance.
PARCEL_CASING_INSET = -0.025
PARCEL_CASING_DEPTH = 0.070

# These are source-side preview colors only. Runtime maps the slots to the
# authored W01/W05/WoodCut/SnowRoof material families.
HERO_SOURCE_MATERIALS = {
    HERO_LOG_MATERIAL: (0.19, 0.14, 0.11, 1.0),
    HERO_LOG_END_MATERIAL: (0.34, 0.25, 0.17, 1.0),
    HERO_TRIM_TEAL_MATERIAL: (0.33, 0.50, 0.47, 1.0),
    HERO_TRIM_IVORY_MATERIAL: (0.76, 0.75, 0.67, 1.0),
    HERO_ROOF_SNOW_MATERIAL: (0.84, 0.89, 0.91, 1.0),
}

# Component-local metres after Blender -> Godot conversion. The previous
# facade was scaled by .82 in game; keep its street-left corner, threshold and
# exact doorway XZ while adding space to the rear/right for the real room.
HERO_HOUSE_CONTRACT = {
    "version": "hero-house-eight-by-seven-v1",
    "component": HERO_DWELLING_ROOT,
    "runtime_scale": 1.0,
    "shell_size_xz": [8.4, 7.4],
    "clear_room_size_xz": [8.0, 7.0],
    "wall_thickness": 0.20,
    "shell_min_xz": [-2.542, -6.334],
    "shell_max_xz": [5.858, 1.066],
    "room_center_xz": [1.658, -2.634],
    "floor_above_component_ground": 0.246,
    "clear_ceiling_height": 2.60,
    "eave_height": 3.05,
    "ridge_height": 4.80,
    "portal_probe_preserved_xyz": [-1.1644, 1.1685, 1.0865],
    "portal_clear_width_height": [1.30, 2.25],
    "room_door_x": -2.8224,
    "room_wall_centerlines_xz": [4.1, 3.6],
    "room_entry_xz": [-2.8224, 2.30],
    "room_exit_target_xz": [-2.8224, 3.40],
    "front_window_room_x": [-0.85, 1.0, 2.85],
    "rear_window_room_x": [-2.55, 2.55],
    "left_window_room_z": [0.60],
    "right_window_room_z": [0.90],
    "window_clear_width": 1.06,
    "window_sill_top_above_floor": [0.74, 2.14],
    "stove_room_xz": [-3.15, -0.45],
}

HERO_YARD_SHED_CONTRACT = {
    "version": "hero-yard-shed-two-level-v1",
    "component": HERO_YARD_SHED_ROOT,
    "runtime_scale": 1.0,
    "footprint_xz": [4.4, 4.4],
    "ground_floor_y": 0.04,
    "loft_floor_y": 1.46,
    "lowest_joist_y": 1.26,
    "underdeck_clear_height": 1.22,
    "eave_y": 3.42,
    "ridge_y": 4.36,
    "lower_landing_xyz": [0, 0.04, 3.92],
    "lower_grip_xyz": [0, 0.04, 3.75],
    "upper_grip_xyz": [0, 1.50, 2.65],
    "upper_landing_xyz": [0, 1.50, 1.45],
    "underdeck_route_x": -1.15,
    "loft_observation_xyz": [0, 2.52, -2.06],
    "underdeck_repair_root_xyz": [-1.51, 0.04, -1.37],
    "underdeck_rattle_xyz": [-1.15, 0.15, -0.75],
    "underdeck_mechanism_owner": "yard/loose-footboard",
}

ANIMAL_ROOTS = (CAT_ROOT, CROW_ROOT)
CAT_CHILDREN = (
    "CatBody",
    "CatHead",
    "CatTail",
    "CatLegFrontL",
    "CatLegFrontR",
    "CatLegBackL",
    "CatLegBackR",
)
CROW_CHILDREN = ("BirdBody", "BirdWingL", "BirdWingR")
CAT_MATERIALS = ("URMAN_Stone_Mossy", "URMAN_Roof_WetSlate", "URMAN_Stone_LightFace")
CROW_MATERIALS = ("URMAN_Roof_WetSlate", "URMAN_Stone_Mossy")

WOODPILE_GEOMETRY_PASS = "stable three-tier horizontal woodpile v1"
WOODPILE_LOG_NAMES = tuple(f"Woodpile_Log_{index:02d}_LOD0" for index in range(6))
WOODPILE_SUPPORT_NAMES = ("Woodpile_SupportLeft_LOD0", "Woodpile_SupportRight_LOD0")


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--component-only", choices=("hero-yard-shed", "hero-house"),
                        help="Update only one hero component; preserve every existing kit component")
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str) -> bpy.types.Material:
    result = bpy.data.materials.get(name)
    if result is None:
        raise RuntimeError(f"Missing baseline material: {name}")
    return result


def ensure_hero_materials() -> None:
    for name, color in HERO_SOURCE_MATERIALS.items():
        target = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        target.diffuse_color = color
        target["source_slot"] = name
        target["runtime_material_owner"] = {
            HERO_LOG_MATERIAL: "wood_log_uv / W01",
            HERO_LOG_END_MATERIAL: "wood_cut",
            HERO_TRIM_TEAL_MATERIAL: "wood_painted_trim / W05",
            HERO_TRIM_IVORY_MATERIAL: "wood_painted_trim / W05",
            HERO_ROOF_SNOW_MATERIAL: "snow_roof",
        }[name]


def mesh_object(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    materials: tuple[str, ...],
    material_indices: list[int] | None = None,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "dwelling facade geometry",
    asset_id: str = "urman.act1.village.dwelling_facade",
    component_root: str = DWELLING_ROOT,
    geometry_pass: str = "grounded asymmetrical painterly low-poly dwelling v2",
) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None:
        mesh = bpy.data.meshes.new(f"{name}Mesh")
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
    if obj.type != "MESH":
        raise RuntimeError(f"Expected mesh object for {name}, got {obj.type}")
    if obj.parent is not parent:
        obj.parent = parent

    old_mesh = obj.data
    mesh = bpy.data.meshes.new(f"{name}Mesh.__generated__")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update()
    mesh.materials.clear()
    for material_name in materials:
        mesh.materials.append(material(material_name))
    if material_indices is not None:
        if len(material_indices) != len(mesh.polygons):
            raise RuntimeError(f"Material index count mismatch for {name}")
        for polygon, index in zip(mesh.polygons, material_indices):
            polygon.material_index = index
    mesh.calc_loop_triangles()

    obj.data = mesh
    # The baseline keeps some cube-derived children in Blender's quaternion
    # axis-conversion mode.  New component-local meshes must use the source
    # scene's ordinary XYZ basis or a shallow beam would become a long wall.
    obj.rotation_mode = "XYZ"
    obj.location = location
    obj.rotation_euler = rotation
    obj.scale = (1.0, 1.0, 1.0)
    obj.delta_location = (0.0, 0.0, 0.0)
    obj.delta_rotation_euler = (0.0, 0.0, 0.0)
    obj.delta_scale = (1.0, 1.0, 1.0)
    obj.hide_render = False
    obj.hide_viewport = False
    obj["urman_asset_id"] = asset_id
    obj["component_root"] = component_root
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["triangle_count"] = len(mesh.loop_triangles)
    obj["geometry_pass"] = geometry_pass

    if old_mesh is not mesh and old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)
    mesh.name = f"{name}Mesh"
    return obj


def animal_empty(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    role: str,
    asset_id: str,
) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(obj)
    if obj.type != "EMPTY":
        raise RuntimeError(f"Expected animal empty for {name}, got {obj.type}")
    if obj.parent is not parent:
        obj.parent = parent
    obj.rotation_mode = "XYZ"
    obj.location = location
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.hide_render = False
    obj.hide_viewport = False
    obj["urman_asset_id"] = asset_id
    obj["component_root"] = name
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["forward_axis"] = "-Y (Blender) -> +Z (Godot)"
    obj["local_pivot"] = "ground anchor at local X=0, Y=0, Z=0"
    return obj


def append_y_profile(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
    sections: tuple[tuple[float, float, float, float], ...],
    segments: int,
    side_material,
    cap_material: int = 0,
) -> None:
    """Append a smooth, low-poly animal mass aligned to the forward -Y axis."""
    if len(sections) < 2 or segments < 6:
        raise RuntimeError("Invalid animal profile")
    rings: list[list[int]] = []
    for y, radius_x, radius_z, center_z in sections:
        ring: list[int] = []
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            ring.append(len(vertices))
            vertices.append((radius_x * math.cos(angle), y, center_z + radius_z * math.sin(angle)))
        rings.append(ring)
    faces.append(tuple(reversed(rings[0])))
    material_indices.append(cap_material)
    for ring_index in range(len(rings) - 1):
        for segment in range(segments):
            faces.append((
                rings[ring_index][segment],
                rings[ring_index][(segment + 1) % segments],
                rings[ring_index + 1][(segment + 1) % segments],
                rings[ring_index + 1][segment],
            ))
            material_indices.append(side_material(ring_index, segment))
    faces.append(tuple(rings[-1]))
    material_indices.append(cap_material)


def append_ear(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
    base: tuple[float, float, float],
    width: float,
    depth: float,
    apex: tuple[float, float, float],
    material_index: int,
) -> None:
    x, y, z = base
    start = len(vertices)
    vertices.extend(((x - width, y - depth, z), (x + width, y - depth, z),
                     (x + width * 0.82, y + depth, z + 0.008),
                     (x - width * 0.82, y + depth, z + 0.008), apex))
    faces.extend(((start, start + 1, start + 4), (start + 1, start + 2, start + 4),
                  (start + 2, start + 3, start + 4), (start + 3, start, start + 4),
                  (start + 3, start + 2, start + 1, start)))
    material_indices.extend((material_index,) * 5)


def append_muzzle(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
) -> None:
    append_y_profile(
        vertices,
        faces,
        material_indices,
        ((-0.30, 0.040, 0.038, 0.238), (-0.285, 0.055, 0.045, 0.240),
         (-0.255, 0.050, 0.042, 0.242)),
        12,
        lambda _ring, _segment: 2,
        cap_material=2,
    )


def append_tube(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
    points: tuple[tuple[float, float, float], ...],
    radii: tuple[float, ...],
    segments: int,
) -> None:
    if len(points) != len(radii) or len(points) < 2 or segments < 6:
        raise RuntimeError("Invalid animal tube")
    rings: list[list[int]] = []
    for index, point in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent = Vector(tuple(following[i] - previous[i] for i in range(3))).normalized()
        side = tangent.cross(Vector((0.0, 0.0, 1.0)))
        if side.length < 1e-5:
            side = tangent.cross(Vector((1.0, 0.0, 0.0)))
        side.normalize()
        up = side.cross(tangent).normalized()
        ring: list[int] = []
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            offset = side * (math.cos(angle) * radii[index]) + up * (math.sin(angle) * radii[index])
            ring.append(len(vertices))
            vertices.append(tuple(point[i] + offset[i] for i in range(3)))
        rings.append(ring)
    faces.append(tuple(reversed(rings[0])))
    material_indices.append(1)
    for ring_index in range(len(rings) - 1):
        for segment in range(segments):
            faces.append((rings[ring_index][segment], rings[ring_index][(segment + 1) % segments],
                          rings[ring_index + 1][(segment + 1) % segments], rings[ring_index + 1][segment]))
            material_indices.append(0 if (segment + ring_index) % 5 else 1)
    faces.append(tuple(rings[-1]))
    material_indices.append(1)


def append_vertical_leg(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
    joint_z: float,
    rear: bool,
    side_sign: float,
) -> None:
    rings: list[list[int]] = []
    centers = ((0.0, 0.0, 0.0), (0.006 * side_sign, -0.004, -0.080),
               (-0.004 * side_sign, -0.002, -0.155),
               (0.008 * side_sign, -0.010 if not rear else 0.012, -(joint_z - 0.030)),
               (0.010 * side_sign, -0.032 if not rear else 0.018, -joint_z))
    radii = (0.036, 0.031, 0.026, 0.028, 0.030)
    segments = 10
    for ring_index, ((cx, cy, cz), radius) in enumerate(zip(centers, radii)):
        ring: list[int] = []
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            ring.append(len(vertices))
            vertices.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle), cz))
        rings.append(ring)
    faces.append(tuple(reversed(rings[0])))
    material_indices.append(0)
    for ring_index in range(len(rings) - 1):
        for segment in range(segments):
            faces.append((rings[ring_index][segment], rings[ring_index][(segment + 1) % segments],
                          rings[ring_index + 1][(segment + 1) % segments], rings[ring_index + 1][segment]))
            material_indices.append(1 if ring_index == 1 and segment in (2, 3, 4) else 0)
    faces.append(tuple(rings[-1]))
    material_indices.append(1)


def smooth_animal(obj: bpy.types.Object) -> bpy.types.Object:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def animal_mesh(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    materials: tuple[str, ...],
    material_indices: list[int],
    role: str,
    asset_id: str,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    obj = mesh_object(
        name,
        parent,
        vertices,
        faces,
        materials,
        material_indices,
        location=location,
        role=role,
        asset_id=asset_id,
        component_root=parent.name,
        geometry_pass="optional ambient animal presentation v1",
    )
    return smooth_animal(obj)


def author_ambient_cat(parent: bpy.types.Object) -> None:
    asset_id = "urman.act1.village.ambient_cat"
    cat = animal_empty(CAT_ROOT, parent, (0.0, -28.0, 0.0), "optional ambient winter cat", asset_id)
    cat["cat_shoulder_height_m"] = 0.28
    cat["cat_torso_length_m"] = 0.42
    cat["cat_nose_to_rump_m"] = 0.60
    cat["cat_tail_length_m"] = 0.32
    cat["mesh_children_max"] = 9

    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    indices: list[int] = []
    body_sections = ((-0.12, 0.055, 0.060, 0.190), (-0.06, 0.092, 0.082, 0.195),
                     (0.04, 0.105, 0.085, 0.195), (0.15, 0.110, 0.085, 0.195),
                     (0.25, 0.112, 0.080, 0.190), (0.30, 0.080, 0.068, 0.180))
    append_y_profile(vertices, faces, indices, body_sections, 20,
                     lambda ring, segment: 1 if ring in (1, 3, 4) and segment in (2, 3, 4, 5) else 0)
    animal_mesh("CatBody", cat, vertices, faces, CAT_MATERIALS, indices,
                "softened low-poly feline torso and haunch", asset_id)

    vertices, faces, indices = [], [], []
    head_sections = ((-0.27, 0.045, 0.045, 0.245), (-0.25, 0.072, 0.065, 0.250),
                     (-0.22, 0.086, 0.078, 0.255), (-0.17, 0.088, 0.080, 0.255),
                     (-0.12, 0.064, 0.064, 0.250))
    append_y_profile(vertices, faces, indices, head_sections, 18,
                     lambda ring, segment: 1 if ring in (1, 2) and segment in (2, 3, 4, 5) else 0)
    append_muzzle(vertices, faces, indices)
    append_ear(vertices, faces, indices, (-0.050, -0.205, 0.310), 0.048, 0.034,
                (-0.042, -0.190, 0.388), 1)
    append_ear(vertices, faces, indices, (0.052, -0.175, 0.308), 0.044, 0.032,
                (0.061, -0.162, 0.378), 1)
    animal_mesh("CatHead", cat, vertices, faces, CAT_MATERIALS, indices,
                "feline head with muzzle and asymmetrical ears", asset_id)

    vertices, faces, indices = [], [], []
    append_tube(vertices, faces, indices,
                ((0.0, 0.27, 0.18), (0.0, 0.32, 0.20), (0.015, 0.37, 0.23),
                 (0.045, 0.42, 0.26), (0.070, 0.47, 0.23), (0.080, 0.52, 0.17)),
                (0.035, 0.032, 0.027, 0.022, 0.017, 0.010), 12)
    animal_mesh("CatTail", cat, vertices, faces, CAT_MATERIALS, indices,
                "connected tapered curved feline tail", asset_id)

    for name, location, rear, side_sign in (
        ("CatLegFrontL", (-0.065, -0.07, 0.230), False, -1.0),
        ("CatLegFrontR", (0.065, -0.07, 0.230), False, 1.0),
        ("CatLegBackL", (-0.076, 0.235, 0.210), True, -1.0),
        ("CatLegBackR", (0.076, 0.235, 0.210), True, 1.0),
    ):
        vertices, faces, indices = [], [], []
        append_vertical_leg(vertices, faces, indices, location[2], rear, side_sign)
        leg = animal_mesh(name, cat, vertices, faces, CAT_MATERIALS, indices,
                          "animation-safe feline leg with grounded paw", asset_id, location)
        leg["animation_pivot"] = "hip" if rear else "shoulder"
        leg["ground_paw_z_m"] = 0.0


def append_wing(
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_indices: list[int],
    sign: float,
) -> None:
    outline = ((0.0, 0.020, 0.050), (0.075 * sign, 0.008, 0.105),
               (0.170 * sign, -0.020, 0.080), (0.240 * sign, -0.055, -0.018),
               (0.180 * sign, -0.020, -0.075), (0.070 * sign, 0.010, -0.042))
    start = len(vertices)
    vertices.extend(outline)
    vertices.extend((x, y - 0.022, z - 0.006) for x, y, z in outline)
    faces.extend(((start, start + 1, start + 2), (start, start + 2, start + 3),
                  (start, start + 3, start + 4), (start, start + 4, start + 5),
                  (start + 6, start + 8, start + 7), (start + 6, start + 9, start + 8),
                  (start + 6, start + 10, start + 9), (start + 6, start + 11, start + 10)))
    material_indices.extend((0, 0, 0, 1, 0, 0, 0, 1))
    for index in range(6):
        next_index = (index + 1) % 6
        faces.append((start + index, start + next_index, start + 6 + next_index, start + 6 + index))
        material_indices.append(1 if index in (2, 3) else 0)


def author_ambient_crow(parent: bpy.types.Object) -> None:
    asset_id = "urman.act1.village.ambient_crow"
    crow = animal_empty(CROW_ROOT, parent, (3.0, -28.0, 1.0), "optional ambient crow in flight", asset_id)
    crow["bird_body_length_m"] = 0.30
    crow["bird_wingspan_m"] = 0.65
    crow["bird_flying_center"] = "local origin"
    crow["mesh_children_max"] = 3

    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    indices: list[int] = []
    body_sections = ((-0.13, 0.032, 0.030, 0.0), (-0.095, 0.064, 0.060, 0.0),
                     (-0.025, 0.085, 0.075, 0.0), (0.065, 0.072, 0.062, 0.0),
                     (0.13, 0.030, 0.032, 0.0))
    append_y_profile(vertices, faces, indices, body_sections, 12,
                     lambda ring, segment: 1 if ring == 1 and segment in (2, 3, 4, 5) else 0)
    beak_start = len(vertices)
    vertices.extend(((-0.018, -0.13, 0.018), (0.018, -0.13, 0.018),
                     (0.0, -0.235, 0.006), (-0.014, -0.13, -0.010), (0.014, -0.13, -0.010)))
    faces.extend(((beak_start, beak_start + 1, beak_start + 2),
                  (beak_start + 1, beak_start + 4, beak_start + 2),
                  (beak_start + 4, beak_start + 3, beak_start + 2),
                  (beak_start + 3, beak_start, beak_start + 2),
                  (beak_start + 3, beak_start + 4, beak_start + 1, beak_start)))
    indices.extend((1, 1, 1, 1, 1))
    tail_start = len(vertices)
    vertices.extend(((-0.040, 0.105, 0.018), (0.040, 0.105, 0.018),
                     (0.025, 0.275, 0.005), (-0.025, 0.275, 0.005),
                     (0.0, 0.205, -0.040)))
    faces.extend(((tail_start, tail_start + 1, tail_start + 2),
                  (tail_start, tail_start + 2, tail_start + 3),
                  (tail_start + 3, tail_start + 2, tail_start + 4),
                  (tail_start + 4, tail_start + 1, tail_start),
                  (tail_start, tail_start + 4, tail_start + 3)))
    indices.extend((0, 0, 1, 1, 1))
    animal_mesh("BirdBody", crow, vertices, faces, CROW_MATERIALS, indices,
                "plain corvid body with coherent beak and tail", asset_id)

    for name, location, sign in (
        ("BirdWingL", (-0.085, -0.020, 0.040), -1.0),
        ("BirdWingR", (0.085, -0.020, 0.040), 1.0),
    ):
        vertices, faces, indices = [], [], []
        append_wing(vertices, faces, indices, sign)
        wing = animal_mesh(name, crow, vertices, faces, CROW_MATERIALS, indices,
                           "animation-safe corvid wing in flight pose", asset_id, location)
        wing["animation_pivot"] = "shoulder"


def clear_animal_roots(root: bpy.types.Object) -> None:
    for name in ANIMAL_ROOTS:
        animal = bpy.data.objects.get(name)
        if animal is not None:
            for obj in reversed(list(animal.children_recursive)):
                mesh = obj.data if obj.type == "MESH" else None
                bpy.data.objects.remove(obj, do_unlink=True)
                if mesh is not None and mesh.users == 0:
                    bpy.data.meshes.remove(mesh)
            bpy.data.objects.remove(animal, do_unlink=True)
        for obj in list(bpy.data.objects):
            if obj.name.startswith(f"{name}."):
                mesh = obj.data if obj.type == "MESH" else None
                bpy.data.objects.remove(obj, do_unlink=True)
                if mesh is not None and mesh.users == 0:
                    bpy.data.meshes.remove(mesh)


def author_ambient_animals(root: bpy.types.Object) -> None:
    clear_animal_roots(root)
    author_ambient_cat(root)
    author_ambient_crow(root)


def animal_bounds(root: bpy.types.Object, children: tuple[str, ...]) -> tuple[list[float], list[float]]:
    points = [root.matrix_world.inverted() @ obj.matrix_world @ vertex.co
              for name in children
              for obj in (bpy.data.objects[name],)
              for vertex in obj.data.vertices]
    return ([min(point[i] for point in points) for i in range(3)],
            [max(point[i] for point in points) for i in range(3)])


def validate_animal_meshes(
    root: bpy.types.Object,
    animal_name: str,
    children: tuple[str, ...],
    expected_location: tuple[float, float, float],
    triangle_range: tuple[int, int],
    maximum_materials: int,
) -> tuple[int, list[float], list[float]]:
    animal = bpy.data.objects.get(animal_name)
    if animal is None or animal.parent is not root or animal.type != "EMPTY":
        raise RuntimeError(f"Animal root hierarchy changed: {animal_name}")
    if tuple(round(value, 5) for value in animal.location) != expected_location:
        raise RuntimeError(f"Animal preview anchor changed: {animal_name}={tuple(animal.location)}")
    actual_children = tuple(child.name for child in animal.children if child.type == "MESH")
    if set(actual_children) != set(children) or len(actual_children) != len(children):
        raise RuntimeError(f"Animal mesh hierarchy changed: {animal_name}={sorted(actual_children)}")
    materials = set()
    triangles = 0
    for name in children:
        child = bpy.data.objects.get(name)
        if child is None or child.parent is not animal or child.type != "MESH":
            raise RuntimeError(f"Missing animal mesh: {animal_name}/{name}")
        child.data.calc_loop_triangles()
        if not child.data.materials or not child.data.loop_triangles:
            raise RuntimeError(f"Invalid animal mesh: {animal_name}/{name}")
        if any(polygon.area <= 1e-10 for polygon in child.data.polygons):
            raise RuntimeError(f"Degenerate animal polygon: {animal_name}/{name}")
        triangles += len(child.data.loop_triangles)
        materials.update(material.name for material in child.data.materials if material is not None)
    if triangles < triangle_range[0] or triangles > triangle_range[1]:
        raise RuntimeError(f"Animal triangle budget changed: {animal_name}={triangles}")
    if len(materials) > maximum_materials:
        raise RuntimeError(f"Animal material budget changed: {animal_name}={sorted(materials)}")
    bounds_min, bounds_max = animal_bounds(animal, children)
    if any(bounds_max[i] - bounds_min[i] <= 0.01 for i in range(3)):
        raise RuntimeError(f"Animal bounds are degenerate: {animal_name}={bounds_min}..{bounds_max}")
    return triangles, bounds_min, bounds_max


def validate_animals(root: bpy.types.Object) -> None:
    cat_triangles, cat_min, cat_max = validate_animal_meshes(
        root, CAT_ROOT, CAT_CHILDREN, (0.0, -28.0, 0.0), (1000, 2000), 3)
    cat = bpy.data.objects[CAT_ROOT]
    if abs(cat_max[2] - 0.388) > 0.003 or abs(cat_min[2]) > 1e-6:
        raise RuntimeError(f"Cat ground/height bounds changed: {cat_min}..{cat_max}")
    body = bpy.data.objects["CatBody"]
    body.data.calc_loop_triangles()
    body_y = [vertex.co.y for vertex in body.data.vertices]
    body_z = [vertex.co.z for vertex in body.data.vertices]
    if abs(max(body_z) - 0.28) > 1e-6:
        raise RuntimeError(f"Cat shoulder height changed: {max(body_z)}")
    if abs(max(body_y) - min(body_y) - 0.42) > 1e-6:
        raise RuntimeError(f"Cat torso length changed: {min(body_y)}..{max(body_y)}")
    cat_head = bpy.data.objects["CatHead"]
    head_y = [vertex.co.y for vertex in cat_head.data.vertices]
    if abs(max(body_y) - min(head_y) - 0.60) > 1e-6:
        raise RuntimeError(f"Cat nose-to-rump length changed: {min(head_y)}..{max(body_y)}")
    for name in CAT_CHILDREN[3:]:
        leg = bpy.data.objects[name]
        if abs(leg.location.z - (0.23 if "Front" in name else 0.21)) > 1e-6:
            raise RuntimeError(f"Cat leg pivot changed: {name}={tuple(leg.location)}")
        paw_min = min((leg.matrix_world @ vertex.co).z for vertex in leg.data.vertices)
        if abs(paw_min) > 1e-6:
            raise RuntimeError(f"Cat paw is not grounded: {name} z={paw_min}")
    crow_triangles, crow_min, crow_max = validate_animal_meshes(
        root, CROW_ROOT, CROW_CHILDREN, (3.0, -28.0, 1.0), (120, 300), 3)
    crow = bpy.data.objects[CROW_ROOT]
    for name, expected in (("BirdBody", (0.0, 0.0, 0.0)),
                           ("BirdWingL", (-0.085, -0.020, 0.040)),
                           ("BirdWingR", (0.085, -0.020, 0.040))):
        if tuple(round(value, 5) for value in bpy.data.objects[name].location) != expected:
            raise RuntimeError(f"Crow child pivot changed: {name}")
    if abs(crow_max[0] - crow_min[0] - 0.65) > 0.015:
        raise RuntimeError(f"Crow wingspan changed: {crow_min[0]}..{crow_max[0]}")
    print(f"animal-pass: cat_meshes=7 cat_triangles={cat_triangles} cat_bounds={cat_min}..{cat_max} "
          f"crow_meshes=3 crow_triangles={crow_triangles} crow_bounds={crow_min}..{crow_max} "
          f"axes=Blender:-Y/Godot:+Z materials=cat<=3/crow<=3")


def chamfered_box(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    materials: tuple[str, ...],
    material_indices: list[int] | None = None,
    chamfer: float = 0.04,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "dwelling timber detail",
    asset_id: str = "urman.act1.village.dwelling_facade",
    component_root: str = DWELLING_ROOT,
    geometry_pass: str = "grounded asymmetrical painterly low-poly dwelling v2",
) -> bpy.types.Object:
    sx, sy, sz = (value / 2.0 for value in size)
    c = min(chamfer, sx * 0.45, sy * 0.45)
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
    faces.extend((index, (index + 1) % 8, (index + 1) % 8 + 8, index + 8) for index in range(8))
    if material_indices is None:
        material_indices = [0] * len(faces)
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        materials,
        material_indices,
        location=center,
        rotation=rotation,
        role=role,
        asset_id=asset_id,
        component_root=component_root,
        geometry_pass=geometry_pass,
    )


VILLAGE_GEOMETRY_PASS = "authored full-volume painterly village exterior v5; visible joinery and offset annex silhouettes"

VARIANT_GEOMETRY_PASS = "authored full-volume painterly village parcel variants v2"
VARIANT_COMPOSITION_PASS = "authored parcel contact composition v1; presentation-only edge dressing"
VARIANT_LAYOUT = {
    "VillageParcel_VariantA_TimberGable": (28.0, 0.0, 0.0),
    "VillageParcel_VariantB_PlasterAnnex": (41.0, 0.0, 0.0),
    "VillageParcel_VariantC_BanyaYard": (54.0, 0.0, 0.0),
}

PARCEL_COMPOSITION = {
    "VillageParcel_VariantA_TimberGable": {
        "component_root": "VariantA_Yard_OpenRail",
        "moss": (
            ((-3.22, 0.68, 0.04), (0.78, 0.42, 0.12)),
            ((2.62, 1.20, 0.04), (0.66, 0.34, 0.10)),
            ((-0.90, 2.02, 0.04), (0.58, 0.32, 0.09)),
        ),
        "shrubs": (
            ((-3.58, 1.55), 0.78),
            ((3.70, 1.84), 0.62),
        ),
        "branches": (
            ((-2.94, 1.72, 0.08), (-2.36, 2.18, 0.92), 0.085),
            ((3.40, 2.00, 0.08), (3.94, 2.32, 0.72), 0.075),
        ),
        "sedge": (
            ((-4.00, 0.36), 0.52, -0.22),
            ((-3.72, 0.58), 0.68, 0.18),
            ((2.98, 1.18), 0.46, -0.16),
            ((3.26, 1.40), 0.58, 0.24),
        ),
    },
    "VillageParcel_VariantB_PlasterAnnex": {
        "component_root": "VariantB_Yard_TallPlank",
        "moss": (
            ((-3.36, 1.10, 0.04), (0.72, 0.36, 0.11)),
            ((3.34, 1.02, 0.04), (0.82, 0.42, 0.13)),
            ((2.48, 2.06, 0.04), (0.56, 0.28, 0.08)),
        ),
        "shrubs": (
            ((-3.78, 1.88), 0.64),
            ((3.62, 1.72), 0.76),
        ),
        "branches": (
            ((-3.12, 2.18, 0.08), (-2.44, 2.46, 0.86), 0.08),
            ((3.46, 2.34, 0.08), (2.90, 2.74, 0.76), 0.09),
        ),
        "sedge": (
            ((-4.08, 0.90), 0.48, 0.20),
            ((-3.80, 1.14), 0.62, -0.16),
            ((3.46, 1.06), 0.56, 0.22),
            ((3.78, 1.30), 0.44, -0.18),
        ),
    },
    "VillageParcel_VariantC_BanyaYard": {
        "component_root": "VariantC_Yard_MixedRepair",
        "moss": (
            ((-3.28, 0.92, 0.04), (0.86, 0.46, 0.14)),
            ((2.94, 1.18, 0.04), (0.62, 0.34, 0.10)),
            ((3.56, 2.08, 0.04), (0.72, 0.36, 0.11)),
        ),
        "shrubs": (
            ((-3.66, 1.46), 0.70),
            ((3.66, 1.74), 0.58),
        ),
        "branches": (
            ((-3.00, 1.94, 0.08), (-2.54, 2.42, 0.82), 0.085),
            ((3.18, 2.18, 0.08), (3.80, 2.42, 0.68), 0.075),
        ),
        "sedge": (
            ((-4.02, 0.54), 0.62, -0.18),
            ((-3.70, 0.78), 0.48, 0.24),
            ((3.18, 1.06), 0.54, -0.20),
            ((3.52, 1.34), 0.66, 0.16),
        ),
    },
}


def variant_empty(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "authored village variant subroot",
    family: str = "village parcel variant",
) -> bpy.types.Object:
    obj = bpy.data.objects.get(name)
    if obj is None:
        obj = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(obj)
    if obj.type != "EMPTY":
        raise RuntimeError(f"Expected empty variant root for {name}, got {obj.type}")
    if obj.parent is not parent:
        obj.parent = parent
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    obj.hide_render = False
    obj.hide_viewport = False
    obj["urman_asset_id"] = f"urman.act1.village.{name.lower()}"
    obj["component_root"] = "URMAN_VillageExteriorKit"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["variant_family"] = family
    obj["geometry_pass"] = VARIANT_GEOMETRY_PASS
    return obj


def variant_box(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    materials: tuple[str, ...],
    component_root: str,
    role: str,
    material_indices: list[int] | None = None,
    chamfer: float = 0.04,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    geometry_pass: str = VARIANT_GEOMETRY_PASS,
) -> bpy.types.Object:
    return chamfered_box(
        name,
        parent,
        center,
        size,
        materials,
        material_indices,
        chamfer=chamfer,
        rotation=rotation,
        role=role,
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=geometry_pass,
    )


def variant_beam(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    depth: float,
    materials: tuple[str, ...],
    component_root: str,
    role: str,
    geometry_pass: str = VARIANT_GEOMETRY_PASS,
) -> bpy.types.Object:
    vector = tuple(end[index] - start[index] for index in range(3))
    length = math.sqrt(sum(component * component for component in vector))
    if length <= 0.01:
        raise RuntimeError(f"Degenerate variant beam: {name}")
    center = tuple((start[index] + end[index]) / 2.0 for index in range(3))
    yaw = -math.atan2(vector[2], vector[0])
    return variant_box(
        name,
        parent,
        center,
        (length, depth, width),
        materials,
        component_root,
        role,
        chamfer=min(width, depth) * 0.2,
        rotation=(0.0, yaw, 0.0),
        geometry_pass=geometry_pass,
    )


def variant_wall(
    name: str,
    parent: bpy.types.Object,
    footprint: list[tuple[float, float]],
    base_z: float,
    top_z: float,
    component_root: str,
    front_material: str = "URMAN_Plaster_Ochre",
    side_material: str = "URMAN_Plaster_Shadow",
    top_inset: float = 0.025,
    role: str = "full-volume variant wall",
) -> bpy.types.Object:
    if len(footprint) < 4 or top_z <= base_z:
        raise RuntimeError(f"Invalid variant wall dimensions: {name}")
    top = [(x * (1.0 - top_inset), y * (1.0 - top_inset), top_z) for x, y in footprint]
    vertices = [(x, y, base_z) for x, y in footprint] + top
    count = len(footprint)
    faces: list[tuple[int, ...]] = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces.extend((index, (index + 1) % count, (index + 1) % count + count, index + count) for index in range(count))
    material_indices = [1, 1] + [0 if index in (0, 1, count - 1) else 1 for index in range(count)]
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        (front_material, side_material),
        material_indices,
        role=role,
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=VARIANT_GEOMETRY_PASS,
    )


def variant_gable(
    name: str,
    parent: bpy.types.Object,
    width: float,
    front_y: float,
    back_y: float,
    base_z: float,
    ridge_x: float,
    ridge_z: float,
    component_root: str,
) -> bpy.types.Object:
    profile = [(-width / 2.0, base_z), (width / 2.0, base_z), (ridge_x, ridge_z)]
    vertices = [(x, front_y, z) for x, z in profile] + [(x, back_y, z) for x, z in profile]
    faces = [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        ("URMAN_Plaster_Ochre", "URMAN_Wood_Weathered"),
        [0, 0, 1, 1, 1],
        role="asymmetrical full-volume variant gable",
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=VARIANT_GEOMETRY_PASS,
    )


def variant_roof(
    name: str,
    parent: bpy.types.Object,
    width: float,
    front_y: float,
    back_y: float,
    base_z: float,
    ridge_x: float,
    ridge_z: float,
    overhang: float,
    component_root: str,
    thickness: float = 0.13,
) -> bpy.types.Object:
    half = width / 2.0 + overhang
    vertices = [
        (-half, front_y, base_z),
        (ridge_x, front_y, ridge_z),
        (half, front_y, base_z),
        (-half, back_y, base_z + 0.02),
        (ridge_x + 0.10, back_y, ridge_z - 0.06),
        (half, back_y, base_z + 0.01),
    ]
    vertices.extend((x, y, z - thickness) for x, y, z in tuple(vertices))
    faces = [
        (0, 3, 4, 1),
        (1, 4, 5, 2),
        (6, 7, 10, 9),
        (7, 8, 11, 10),
        (0, 1, 7, 6),
        (1, 2, 8, 7),
        (2, 5, 11, 8),
        (5, 4, 10, 11),
        (4, 3, 9, 10),
        (3, 0, 6, 9),
    ]
    roof = mesh_object(
        name,
        parent,
        vertices,
        [tuple(reversed(face)) for face in faces],
        ("URMAN_Roof_WetSlate", "URMAN_Roof_MossTone", "URMAN_Wood_Weathered"),
        [0, 1, 2, 2, 2, 2, 2, 1, 2, 2],
        role="faceted pitched variant roof with eave depth",
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=VARIANT_GEOMETRY_PASS,
    )
    assert all(roof.data.polygons[i].normal.z > 0 for i in (0, 1)), "Roof top faces must face the sky"
    return roof


def variant_lean_to_roof(
    name: str,
    parent: bpy.types.Object,
    x0: float,
    x1: float,
    back_y: float,
    front_y: float,
    back_z: float,
    front_z: float,
    component_root: str,
    thickness: float = 0.12,
) -> bpy.types.Object:
    vertices = [
        (x0, back_y, back_z),
        (x1, back_y, back_z),
        (x1, front_y, front_z),
        (x0, front_y, front_z),
        (x0, back_y, back_z - thickness),
        (x1, back_y, back_z - thickness),
        (x1, front_y, front_z - thickness),
        (x0, front_y, front_z - thickness),
    ]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        ("URMAN_Roof_WetSlate", "URMAN_Roof_MossTone"),
        [0, 1, 1, 0, 1, 0],
        role="shallow variant lean-to roof",
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=VARIANT_GEOMETRY_PASS,
    )


def variant_window(
    parent: bpy.types.Object,
    prefix: str,
    center: tuple[float, float, float],
    width: float,
    height: float,
    component_root: str,
    axis: str = "front",
    glass: str = "URMAN_Window_DimGlass",
) -> None:
    x, y, z = center
    if axis == "front":
        panel_size = (width, 0.09, height)
        trim_specs = (
            ("Top", (x, y - 0.07, z + height / 2.0 + 0.08), (width + 0.22, 0.12, 0.12)),
            ("Bottom", (x, y - 0.07, z - height / 2.0 - 0.08), (width + 0.22, 0.12, 0.12)),
            ("Left", (x - width / 2.0 - 0.08, y - 0.07, z), (0.12, 0.12, height + 0.20)),
            ("Right", (x + width / 2.0 + 0.08, y - 0.07, z), (0.12, 0.12, height + 0.20)),
        )
        mullions = (
            ("MullionH", (x, y - 0.12, z), (width, 0.06, 0.06)),
            ("MullionV", (x, y - 0.12, z), (0.06, 0.06, height)),
        )
        sill = (x, y - 0.13, z - height / 2.0 - 0.14), (width + 0.28, 0.18, 0.10)
    elif axis == "side":
        panel_size = (0.09, width, height)
        trim_specs = (
            ("Top", (x - 0.07, y, z + height / 2.0 + 0.08), (0.12, width + 0.22, 0.12)),
            ("Bottom", (x - 0.07, y, z - height / 2.0 - 0.08), (0.12, width + 0.22, 0.12)),
            ("Left", (x - 0.07, y - width / 2.0 - 0.08, z), (0.12, 0.12, height + 0.20)),
            ("Right", (x - 0.07, y + width / 2.0 + 0.08, z), (0.12, 0.12, height + 0.20)),
        )
        mullions = (
            ("MullionH", (x - 0.12, y, z), (0.06, width, 0.06)),
            ("MullionV", (x - 0.12, y, z), (0.06, 0.06, height)),
        )
        sill = (x - 0.13, y, z - height / 2.0 - 0.14), (0.18, width + 0.28, 0.10)
    else:
        raise RuntimeError(f"Unsupported variant window axis: {axis}")
    variant_box(f"{prefix}_Inset_LOD0", parent, center, panel_size, (glass,), component_root, "deep window inset", chamfer=0.035)
    for suffix, trim_center, trim_size in trim_specs:
        variant_box(f"{prefix}_{suffix}_LOD0", parent, trim_center, trim_size, ("URMAN_Wood_Weathered",), component_root, "window reveal trim", chamfer=0.025)
    for suffix, mullion_center, mullion_size in mullions:
        variant_box(f"{prefix}_{suffix}_LOD0", parent, mullion_center, mullion_size, ("URMAN_Wood_Dark",), component_root, "window mullion", chamfer=0.014)
    variant_box(f"{prefix}_Sill_LOD0", parent, sill[0], sill[1], ("URMAN_Wood_Dark",), component_root, "projecting window sill", chamfer=0.025)


def variant_door(
    parent: bpy.types.Object,
    prefix: str,
    center: tuple[float, float, float],
    component_root: str,
    width: float = 1.05,
    height: float = 2.18,
) -> None:
    x, y, z = center
    variant_box(
        f"{prefix}_Recess_LOD0",
        parent,
        (x, y + 0.03, z),
        (width + 0.30, 0.18, height + 0.28),
        ("URMAN_Wood_WetShadow",),
        component_root,
        "recessed exterior door surround",
        chamfer=0.06,
    )
    variant_box(
        f"{prefix}_Panel_LOD0",
        parent,
        (x, y - 0.09, z),
        (width, 0.10, height),
        ("URMAN_Wood_Weathered",),
        component_root,
        "plank door panel",
        chamfer=0.045,
    )
    for index in range(3):
        variant_box(
            f"{prefix}_Plank_{index:02d}_LOD0",
            parent,
            (x - width * 0.30 + index * width * 0.30, y - 0.15, z),
            (0.05, 0.06, height),
            ("URMAN_Wood_Dark",),
            component_root,
            "door plank seam",
            chamfer=0.014,
        )
    variant_box(
        f"{prefix}_Handle_LOD0",
        parent,
        (x + width * 0.34, y - 0.19, z - 0.04),
        (0.10, 0.07, 0.13),
        ("URMAN_Metal_Dulled",),
        component_root,
        "door handle",
        chamfer=0.025,
    )


def variant_moss_patch(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    component_root: str,
    phase: int = 0,
) -> bpy.types.Object:
    """Add a small faceted moss/contact patch without using a foliage cone."""
    sx, sy, height = size
    if min(sx, sy, height) <= 0.0:
        raise RuntimeError(f"Invalid moss patch dimensions: {name}")
    ring = (
        (-0.50, -0.16),
        (-0.22, -0.50),
        (0.30, -0.42),
        (0.52, 0.04),
        (0.18, 0.48),
        (-0.38, 0.34),
    )
    top_factors = (0.74, 0.98, 0.82, 1.0, 0.66, 0.86)
    vertices = [(x * sx, y * sy, 0.0) for x, y in ring]
    vertices.extend((x * sx, y * sy, height * top_factors[(index + phase) % len(top_factors)]) for index, (x, y) in enumerate(ring))
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((index, (index + 1) % 6, (index + 1) % 6 + 6, index + 6) for index in range(6))
    material_indices = [1, 0, 0, 1, 0, 1, 0, 1]
    return mesh_object(
        name,
        parent,
        vertices,
        faces,
        ("URMAN_Roof_MossTone", "URMAN_Wood_WetShadow"),
        material_indices,
        location=center,
        role="irregular moss contact patch",
        asset_id=f"urman.act1.village.{component_root.lower()}",
        component_root=component_root,
        geometry_pass=VARIANT_COMPOSITION_PASS,
    )


def variant_shrub_cluster(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float],
    scale: float,
    component_root: str,
) -> None:
    """Build a low irregular shrub from three offset faceted contact clumps."""
    if scale <= 0.0:
        raise RuntimeError(f"Invalid shrub scale: {name}")
    x, y = center
    clumps = (
        (-0.28, -0.04, 0.88, 0.76, 0.82),
        (0.12, 0.18, 0.70, 0.62, 1.04),
        (0.34, -0.20, 0.56, 0.54, 0.66),
    )
    for index, (offset_x, offset_y, width, depth, height) in enumerate(clumps):
        variant_moss_patch(
            f"{name}_Clump_{index:02d}_LOD0",
            parent,
            (x + offset_x * scale, y + offset_y * scale, 0.035 + index * 0.008),
            (scale * width, scale * depth, scale * 0.22 * height),
            component_root,
            phase=index,
        )


def variant_branch(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    component_root: str,
) -> bpy.types.Object:
    """Add one restrained fallen/leaning branch as an authored contact cue."""
    return variant_beam(
        name,
        parent,
        start,
        end,
        width,
        width * 0.72,
        ("URMAN_Bark_Muted", "URMAN_Wood_WetShadow"),
        component_root,
        "fallen/leaning branch contact cue",
        geometry_pass=VARIANT_COMPOSITION_PASS,
    )


def variant_sedge_cluster(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float],
    height: float,
    lean: float,
    component_root: str,
) -> None:
    """Add three sparse edge blades, leaving the parcel approach readable."""
    if height <= 0.0:
        raise RuntimeError(f"Invalid sedge height: {name}")
    x, y = center
    blades = (
        (-0.08, 0.84, 0.80),
        (0.02, 1.00, 1.00),
        (0.10, 0.70, 1.18),
    )
    for index, (offset, factor, lean_factor) in enumerate(blades):
        start = (x + offset * 0.34, y + (index - 1) * 0.055, 0.025)
        end = (x + offset * 0.34 + lean * lean_factor, y + (index - 1) * 0.055, height * factor)
        variant_beam(
            f"{name}_Blade_{index:02d}_LOD0",
            parent,
            start,
            end,
            0.035,
            0.028,
            ("URMAN_Roof_MossTone", "URMAN_Bark_Muted"),
            component_root,
            "sparse sedge edge contact",
            geometry_pass=VARIANT_COMPOSITION_PASS,
        )


def author_variant_composition(parent: bpy.types.Object, parcel_name: str) -> None:
    """Dress each optional parcel edge with sparse, non-blocking local history."""
    composition = PARCEL_COMPOSITION[parcel_name]
    component_root = composition["component_root"]
    parent["composition_pass"] = VARIANT_COMPOSITION_PASS
    parent["composition_clearance"] = "central approach lane left open"
    for index, (center, size) in enumerate(composition["moss"]):
        variant_moss_patch(
            f"{parcel_name}_Moss_{index:02d}_LOD0",
            parent,
            center,
            size,
            component_root,
            phase=index,
        )
    for index, (center, scale) in enumerate(composition["shrubs"]):
        variant_shrub_cluster(f"{parcel_name}_Shrub_{index:02d}", parent, center, scale, component_root)
    for index, (start, end, width) in enumerate(composition["branches"]):
        variant_branch(f"{parcel_name}_Branch_{index:02d}_LOD0", parent, start, end, width, component_root)
    for index, (center, height, lean) in enumerate(composition["sedge"]):
        variant_sedge_cluster(f"{parcel_name}_Sedge_{index:02d}", parent, center, height, lean, component_root)


def village_box(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    size: tuple[float, float, float],
    materials: tuple[str, ...],
    component_root: str,
    role: str,
    material_indices: list[int] | None = None,
    chamfer: float = 0.04,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    """Use the existing deterministic box primitive for new kit details.

    The exterior kit is presentation-only, so these additions deliberately use
    the established material library and carry no collision or runtime state.
    """
    return chamfered_box(
        name,
        parent,
        center,
        size,
        materials,
        material_indices,
        chamfer=chamfer,
        rotation=rotation,
        role=role,
        asset_id="urman.act1.village.exterior_kit",
        component_root=component_root,
        geometry_pass=VILLAGE_GEOMETRY_PASS,
    )


def beam_between(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    depth: float,
    materials: tuple[str, ...],
    role: str = "dwelling timber brace",
    asset_id: str = "urman.act1.village.dwelling_facade",
    component_root: str = DWELLING_ROOT,
    geometry_pass: str = "grounded asymmetrical painterly low-poly dwelling v2",
) -> bpy.types.Object:
    vector = tuple(end[index] - start[index] for index in range(3))
    length = math.sqrt(sum(component * component for component in vector))
    if length <= 0.01:
        raise RuntimeError(f"Degenerate beam: {name}")
    center = tuple((start[index] + end[index]) / 2.0 for index in range(3))
    # All authored facade beams lie in the X/Z plane, so a Y-axis rotation is
    # sufficient and keeps the mesh itself reusable as a plain chamfered box.
    yaw = -math.atan2(vector[2], vector[0])
    return chamfered_box(
        name,
        parent,
        center,
        (length, depth, width),
        materials,
        chamfer=min(width, depth) * 0.2,
        rotation=(0.0, yaw, 0.0),
        role=role,
        asset_id=asset_id,
        component_root=component_root,
        geometry_pass=geometry_pass,
    )


def well_mesh_object(*args: object, **kwargs: object) -> bpy.types.Object:
    kwargs.update(
        asset_id="urman.act1.village.well_landmark",
        component_root=WELL_ROOT,
        geometry_pass="weathered asymmetrical painterly low-poly well v1",
    )
    return mesh_object(*args, **kwargs)


def well_chamfered_box(*args: object, **kwargs: object) -> bpy.types.Object:
    kwargs.update(
        asset_id="urman.act1.village.well_landmark",
        component_root=WELL_ROOT,
        geometry_pass="weathered asymmetrical painterly low-poly well v1",
    )
    return chamfered_box(*args, **kwargs)


def cylinder_mesh(
    name: str,
    parent: bpy.types.Object,
    start: float,
    end: float,
    center: tuple[float, float, float],
    radius: float,
    materials: tuple[str, ...],
    axis: str,
    segments: int = 8,
    role: str = "well cylindrical detail",
) -> bpy.types.Object:
    if segments < 5 or end <= start or radius <= 0.0:
        raise RuntimeError(f"Invalid cylinder dimensions: {name}")
    cx, cy, cz = center
    vertices: list[tuple[float, float, float]] = []
    for value in (start, end):
        for index in range(segments):
            angle = 2.0 * math.pi * index / segments
            if axis == "X":
                vertices.append((value, cy + radius * math.cos(angle), cz + radius * math.sin(angle)))
            elif axis == "Z":
                vertices.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle), value))
            else:
                raise RuntimeError(f"Unsupported cylinder axis: {axis}")
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(segments))),
        tuple(range(segments, segments * 2)),
    ]
    faces.extend(
        (index, (index + 1) % segments, (index + 1) % segments + segments, index + segments)
        for index in range(segments)
    )
    return well_mesh_object(
        name,
        parent,
        vertices,
        faces,
        materials,
        [0] * len(faces),
        role=role,
    )


def woodpile_log(
    name: str,
    parent: bpy.types.Object,
    length: float,
    center: tuple[float, float, float],
    radius: float,
    row: int,
) -> bpy.types.Object:
    if not 1.60 <= length <= 1.85 or not 0.16 <= radius <= 0.18:
        raise RuntimeError(f"Woodpile log outside contract: {name} length={length} radius={radius}")
    log = cylinder_mesh(
        name,
        parent,
        -length / 2.0,
        length / 2.0,
        center,
        radius,
        ("URMAN_Bark_Muted", "URMAN_Wood_CutEnd"),
        axis="X",
        segments=8,
        role="stable horizontal stacked firewood log",
    )
    log["urman_asset_id"] = "urman.act1.village.woodpile"
    log["component_root"] = WOODPILE_ROOT
    log["geometry_pass"] = WOODPILE_GEOMETRY_PASS
    log["woodpile_axis"] = "X"
    log["woodpile_length_m"] = length
    log["woodpile_radius_m"] = radius
    log["woodpile_segments"] = 8
    log["woodpile_row"] = row
    # cylinder_mesh's two caps are the first two polygons; preserve the
    # existing bark side and endwood material contract without a new helper.
    end_uv = log.data.uv_layers.new(name="UVMap")
    for polygon in log.data.polygons[:2]:
        polygon.material_index = 1
        for loop_index in polygon.loop_indices:
            point = log.data.vertices[log.data.loops[loop_index].vertex_index].co
            end_uv.data[loop_index].uv = (
                (point.y - center[1]) / (2.0 * radius) + 0.5,
                (point.z - center[2]) / (2.0 * radius) + 0.5,
            )
    log.data.calc_loop_triangles()
    log["triangle_count"] = len(log.data.loop_triangles)
    return log


def author_woodpile(parent: bpy.types.Object) -> None:
    if parent.name != WOODPILE_ROOT or parent.type != "EMPTY":
        raise RuntimeError(f"Expected existing woodpile root, got {parent.name}:{parent.type}")
    if parent.get("local_pivot") != "ground anchor at local X=0, Y=0, Z=0":
        raise RuntimeError("Woodpile preview anchor metadata changed")

    anchor = (tuple(parent.location), tuple(parent.rotation_euler), tuple(parent.scale))
    expected_names = set(WOODPILE_LOG_NAMES + WOODPILE_SUPPORT_NAMES)
    for child in list(parent.children):
        if child.type == "MESH" and child.name not in expected_names:
            bpy.data.objects.remove(child, do_unlink=True)

    for index, (y, length) in enumerate(((-0.32, 1.78), (0.0, 1.76), (0.32, 1.74))):
        woodpile_log(WOODPILE_LOG_NAMES[index], parent, length, (0.0, y, 0.32), 0.16, 0)
    for index, y in enumerate((-0.16, 0.16), start=3):
        woodpile_log(WOODPILE_LOG_NAMES[index], parent, 1.72, (0.0, y, 0.57372583), 0.16, 1)
    woodpile_log(WOODPILE_LOG_NAMES[5], parent, 1.68, (0.0, 0.0, 0.82745166), 0.16, 2)

    for name, x in zip(WOODPILE_SUPPORT_NAMES, (-0.56, 0.56)):
        support = variant_box(
            name,
            parent,
            (x, 0.0, 0.08),
            (0.18, 0.92, 0.16),
            ("URMAN_Wood_WetShadow",),
            WOODPILE_ROOT,
            "woodpile transverse lower support beam",
            chamfer=0.025,
            geometry_pass=WOODPILE_GEOMETRY_PASS,
        )
        support["urman_asset_id"] = "urman.act1.village.woodpile"
        support["woodpile_support_base_z_m"] = 0.0
        support["woodpile_support_top_z_m"] = 0.16

    current_anchor = (tuple(parent.location), tuple(parent.rotation_euler), tuple(parent.scale))
    if any(abs(a - b) > 1e-6 for before, after in zip(anchor, current_anchor) for a, b in zip(before, after)):
        raise RuntimeError("Woodpile preview anchor transform changed")



def irregular_post(
    name: str,
    parent: bpy.types.Object,
    center: tuple[float, float, float],
    half_size: tuple[float, float, float],
    rotation: tuple[float, float, float],
) -> bpy.types.Object:
    sx, sy, sz = half_size
    footprint = [
        (-sx * 0.82, -sy),
        (sx * 0.72, -sy * 0.94),
        (sx, -sy * 0.24),
        (sx * 0.88, sy * 0.88),
        (sx * 0.12, sy),
        (-sx * 0.82, sy * 0.86),
        (-sx, sy * 0.12),
        (-sx * 0.91, -sy * 0.56),
    ]
    top_offset = (0.025 * sx / 0.11, -0.018 * sy / 0.11)
    vertices = [(x, y, -sz) for x, y in footprint]
    vertices.extend((x + top_offset[0], y + top_offset[1], sz) for x, y in footprint)
    faces: list[tuple[int, ...]] = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((index, (index + 1) % 8, (index + 1) % 8 + 8, index + 8) for index in range(8))
    return well_mesh_object(
        name,
        parent,
        vertices,
        faces,
        ("URMAN_Wood_WetShadow", "URMAN_Wood_Weathered", "URMAN_Wood_Dark"),
        [2, 1, 0, 1, 2, 0, 1, 0, 1, 2],
        location=center,
        rotation=rotation,
        role="irregular leaning well timber post",
    )


def stone_ring_mesh(parent: bpy.types.Object) -> None:
    segments = 12
    step = 2.0 * math.pi / segments
    outer_variation = (0.00, 0.025, -0.015, 0.012, 0.035, -0.01, 0.018, -0.02, 0.01, 0.028, -0.012, 0.016)
    inner_variation = (0.00, 0.012, -0.01, 0.018, -0.015, 0.008, 0.014, -0.008, 0.01, -0.012, 0.016, -0.006)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []
    for course in range(2):
        phase = step * (0.0 if course == 0 else 0.47)
        for index in range(segments):
            angle_start = phase + index * step + 0.018
            angle_end = phase + (index + 1) * step - 0.018
            outer = 0.84 + outer_variation[index] + (0.01 if course else 0.0)
            inner = 0.53 + inner_variation[index] + (0.008 if course else 0.0)
            z_bottom = 0.06 + 0.012 * ((index + course) % 3)
            z_top = 0.31 + 0.235 * course + 0.012 * ((index + 2 * course) % 4)
            block = [
                (outer, angle_start, z_bottom),
                (outer, angle_end, z_bottom),
                (inner, angle_end, z_bottom),
                (inner, angle_start, z_bottom),
                (outer, angle_start, z_top),
                (outer, angle_end, z_top),
                (inner, angle_end, z_top),
                (inner, angle_start, z_top),
            ]
            vertices.extend((radius * math.cos(angle), radius * math.sin(angle), z) for radius, angle, z in block)
            base = len(vertices) - 8
            faces.extend(
                (
                    (base + 3, base + 2, base + 1, base),
                    (base + 4, base + 5, base + 6, base + 7),
                    (base, base + 1, base + 5, base + 4),
                    (base + 1, base + 2, base + 6, base + 5),
                    (base + 2, base + 3, base + 7, base + 6),
                    (base + 3, base, base + 4, base + 7),
                )
            )
            top_material = 1 if (index + course) % 3 else 2
            outer_material = 2 if (index + 2 * course) % 4 == 0 else 0
            material_indices.extend((0, top_material, outer_material, 1, 2, 1))
    well_mesh_object(
        "Well_YardLandmark_StoneRing_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Stone_Mossy", "URMAN_Stone_LightFace", "URMAN_Stone_MossFace"),
        material_indices,
        role="staggered faceted masonry well ring",
    )


def water_inset_mesh(parent: bpy.types.Object) -> None:
    segments = 12
    vertices = [(0.0, 0.0, 0.523)]
    edge_variation = (0.0, 0.004, -0.002, 0.003, -0.003, 0.002, -0.001, 0.003, -0.002, 0.001, -0.003, 0.002)
    vertices.extend(
        (
            0.52 * math.cos(2.0 * math.pi * index / segments),
            0.52 * math.sin(2.0 * math.pi * index / segments),
            0.532 + edge_variation[index],
        )
        for index in range(segments)
    )
    faces = [(0, index + 1, (index + 1) % segments + 1) for index in range(segments)]
    well_mesh_object(
        "Well_YardLandmark_WaterInset_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Well_DarkWater",),
        [0] * len(faces),
        role="recessed dark water surface",
    )


def bucket_mesh(parent: bpy.types.Object) -> None:
    segments = 10
    cx, cy = 0.22, -0.08
    z_bottom, z_top = 0.57, 0.85
    vertices = [
        (cx + 0.16 * math.cos(2.0 * math.pi * index / segments), cy + 0.16 * math.sin(2.0 * math.pi * index / segments), z_bottom)
        for index in range(segments)
    ]
    vertices.extend(
        (cx + 0.215 * math.cos(2.0 * math.pi * index / segments), cy + 0.215 * math.sin(2.0 * math.pi * index / segments), z_top)
        for index in range(segments)
    )
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(segments))),
        tuple(range(segments, segments * 2)),
    ]
    faces.extend((index, (index + 1) % segments, (index + 1) % segments + segments, index + segments) for index in range(segments))
    well_mesh_object(
        "Well_YardLandmark_Bucket_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Wood_WetShadow", "URMAN_Wood_Weathered"),
        [0, 1] + [0 if index % 3 else 1 for index in range(segments)],
        role="small suspended faceted wooden bucket",
    )


def bucket_handle_mesh(parent: bpy.types.Object) -> None:
    cx, cy = 0.22, -0.08
    points = [
        (-0.19, 0.82),
        (-0.16, 1.00),
        (0.16, 1.00),
        (0.19, 0.82),
        (0.12, 0.84),
        (0.10, 0.94),
        (-0.10, 0.94),
        (-0.12, 0.84),
    ]
    depth = 0.035
    vertices = [(cx + x, cy - depth, z) for x, z in points]
    vertices.extend((cx + x, cy + depth, z) for x, z in points)
    faces: list[tuple[int, ...]] = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((index, (index + 1) % 8, (index + 1) % 8 + 8, index + 8) for index in range(8))
    well_mesh_object(
        "Well_YardLandmark_BucketHandle_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Metal_Dulled",),
        [0] * len(faces),
        role="restrained bucket bail handle",
    )


def well_roof_mesh(parent: bpy.types.Object) -> None:
    # The ridge runs between the posts; shallow section variation keeps the
    # small roof faceted without making it a second oversized structure.
    sections = (
        (-0.78, (-1.00, 2.11), (0.03, 2.62), (1.00, 2.10)),
        (-0.28, (-0.98, 2.10), (0.01, 2.65), (1.02, 2.11)),
        (0.25, (-1.01, 2.12), (0.04, 2.61), (0.98, 2.10)),
        (0.80, (-0.98, 2.10), (0.02, 2.63), (1.01, 2.12)),
    )
    vertices: list[tuple[float, float, float]] = []
    for y, left, ridge, right in sections:
        vertices.extend(((left[0], y, left[1]), (ridge[0], y, ridge[1]), (right[0], y, right[1])))
    top_count = len(vertices)
    vertices.extend((x, y, z - 0.11) for x, y, z in tuple(vertices))
    faces: list[tuple[int, ...]] = []
    material_indices: list[int] = []

    def add(face: tuple[int, ...], material_index: int) -> None:
        faces.append(face)
        material_indices.append(material_index)

    for index in range(len(sections) - 1):
        current = index * 3
        following = (index + 1) * 3
        add((current, following, following + 1), index % 2)
        add((current, following + 1, current + 1), 1 - index % 2)
        add((current + 1, following + 1, following + 2), (index + 1) % 2)
        add((current + 1, following + 2, current + 2), index % 2)
        bottom_current = top_count + current
        bottom_following = top_count + following
        add((bottom_current, bottom_current + 1, bottom_following + 1, bottom_following), 0)
        add((bottom_current + 1, bottom_current + 2, bottom_following + 2, bottom_following + 1), 0)
        add((current + 1, following + 1, bottom_following + 1, bottom_current + 1), 2)
        add((current + 2, following + 2, top_count + following + 2, bottom_current + 2), 2)
        add((current, bottom_current, top_count + following, following), 2)
    for section in (0, len(sections) - 1):
        current = section * 3
        bottom = top_count + current
        add((current, current + 1, bottom + 1, bottom), 2)
        add((current + 1, current + 2, bottom + 2, bottom + 1), 2)

    well_mesh_object(
        "Well_YardLandmark_Roof_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Roof_WetSlate", "URMAN_Roof_MossTone", "URMAN_Wood_Weathered"),
        material_indices,
        role="small pitched faceted roof with eaves",
    )




def author_shed_volume(parent: bpy.types.Object) -> None:
    """Rebuild the shared shed using the existing closed gable-shed construction."""
    for child in reversed(list(parent.children_recursive)):
        mesh = child.data if child.type == "MESH" else None
        bpy.data.objects.remove(child, do_unlink=True)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    author_variant_shed_a(parent, prefix="OutbuildingShed")


def author_fence_variation(parent: bpy.types.Object) -> None:
    """Anchor the existing picket run and give its middle a hand-built break."""
    fence = "FenceSegment_RoughPicket"
    for name, center, size in (
        ("FenceSegment_FoundationStoneLeft_LOD0", (-2.10, 0.01, 0.15), (0.62, 0.34, 0.22)),
        ("FenceSegment_FoundationStoneRight_LOD0", (2.10, 0.01, 0.16), (0.56, 0.32, 0.24)),
    ):
        village_box(
            name,
            parent,
            center,
            size,
            ("URMAN_Stone_Mossy", "URMAN_Stone_LightFace"),
            fence,
            "irregular fence foundation stone",
            [0, 1] + [0] * 8,
            chamfer=0.05,
        )
    village_box(
        "FenceSegment_RailMiddle_Crooked_LOD0",
        parent,
        (0.05, 0.02, 0.63),
        (4.75, 0.12, 0.13),
        ("URMAN_Wood_Weathered",),
        fence,
        "crooked middle fence rail",
        chamfer=0.025,
        rotation=(0.0, 0.0, math.radians(-1.4)),
    )


def author_gate_variation(parent: bpy.types.Object) -> None:
    """Add quiet threshold and post-cap variation to the existing gate."""
    gate = "Gate_CrookedTimber"
    for name, center, size, surface in (
        ("Gate_PostCapLeft_LOD0", (-1.32, 0.0, 1.86), (0.36, 0.36, 0.18), ("URMAN_Wood_Weathered",)),
        ("Gate_PostCapRight_LOD0", (1.32, 0.0, 1.91), (0.36, 0.36, 0.18), ("URMAN_Wood_Dark",)),
    ):
        village_box(
            name,
            parent,
            center,
            size,
            surface,
            gate,
            "uneven gate post cap",
            chamfer=0.035,
        )
    brace_angle = -math.atan2(1.18, 2.34)
    village_box(
        "Gate_BackBrace_LOD0",
        parent,
        (0.0, 0.08, 0.82),
        (2.44, 0.13, 0.15),
        ("URMAN_Wood_WetShadow",),
        gate,
        "rear gate diagonal brace",
        chamfer=0.025,
        rotation=(0.0, brace_angle, 0.0),
    )
    village_box(
        "Gate_ThresholdStoneRear_LOD0",
        parent,
        (0.0, 0.24, 0.12),
        (2.72, 0.50, 0.20),
        ("URMAN_Stone_Mossy", "URMAN_Stone_MossFace"),
        gate,
        "rear gate threshold stone",
        [0, 1] + [0] * 8,
        chamfer=0.05,
    )


def author_gate_joinery(parent: bpy.types.Object) -> None:
    """Complete the gate's visible ironwork without adding an interaction owner."""
    gate = "Gate_CrookedTimber"
    village_box(
        "Gate_HingeRight_LOD0",
        parent,
        (1.16, -0.25, 1.28),
        (0.12, 0.10, 0.20),
        ("URMAN_Metal_Dulled",),
        gate,
        "upper gate hinge plate",
        chamfer=0.025,
    )
    village_box(
        "Gate_LatchPlate_LOD0",
        parent,
        (0.84, -0.30, 0.96),
        (0.28, 0.08, 0.14),
        ("URMAN_Metal_Dulled",),
        gate,
        "worn gate latch plate",
        chamfer=0.022,
        rotation=(0.0, 0.0, math.radians(-3.0)),
    )
    village_box(
        "Gate_PostFootRight_LOD0",
        parent,
        (1.32, 0.02, 0.11),
        (0.44, 0.42, 0.18),
        ("URMAN_Stone_Mossy", "URMAN_Stone_MossFace"),
        gate,
        "right gate post footing stone",
        [0, 1] + [0] * 8,
        chamfer=0.05,
    )




def author_variant_shed_a(parent: bpy.types.Object, prefix: str = "VariantA_Shed") -> None:
    root_name = parent.name if prefix == "OutbuildingShed" else "VariantA_Outbuilding_GableShed"
    variant_wall(f"{prefix}_Wall_LOD0", parent, [(-1.78, -1.18), (1.78, -1.18), (1.72, 1.24), (-1.70, 1.24)], 0.24, 2.10, root_name, front_material="URMAN_Wood_Weathered", side_material="URMAN_Wood_WetShadow", role="full-volume gable storage shed")
    variant_box(f"{prefix}_Foundation_LOD0", parent, (0.0, 0.05, 0.16), (3.72, 2.54, 0.28), ("URMAN_Stone_Mossy", "URMAN_Wood_WetShadow"), root_name, "shed foundation plinth", [0, 1] + [0] * 8, chamfer=0.05)
    variant_gable(f"{prefix}_GableFront_LOD0", parent, 3.60, -1.20, -1.04, 2.06, -0.18, 3.00, root_name)
    variant_gable(f"{prefix}_GableBack_LOD0", parent, 3.56, 1.06, 1.20, 2.06, 0.06, 2.94, root_name)
    variant_roof(f"{prefix}_Roof_LOD0", parent, 3.78, -1.38, 1.40, 2.10, -0.12, 3.12, 0.18, root_name, thickness=0.11)
    variant_door(parent, f"{prefix}_Door", (0.42, -1.22, 1.15), root_name, width=1.16, height=1.68)
    variant_window(parent, f"{prefix}_SideWindow", (-1.77, 0.34, 1.30), 0.62, 0.52, root_name, axis="side", glass="URMAN_Window_DimGlass")
    variant_box(f"{prefix}_RearBatten_LOD0", parent, (-0.94, 1.28, 1.24), (0.14, 0.12, 1.74), ("URMAN_Wood_Dark",), root_name, "rear vertical repair batten", chamfer=0.025, rotation=(0.0, 0.0, math.radians(-1.2)))
    variant_box(f"{prefix}_SideEave_LOD0", parent, (1.96, 0.04, 2.20), (0.16, 2.78, 0.14), ("URMAN_Roof_MossTone",), root_name, "side roof eave edge", chamfer=0.025, rotation=(0.0, 0.0, math.radians(1.0)))


def author_variant_shed_b(parent: bpy.types.Object) -> None:
    root_name = "VariantB_Outbuilding_Banya"
    variant_wall("VariantB_Banya_Wall_LOD0", parent, [(-1.56, -1.38), (1.56, -1.38), (1.48, 1.42), (-1.48, 1.42)], 0.26, 2.24, root_name, front_material="URMAN_Wood_WetShadow", side_material="URMAN_Wood_Dark", role="compact full-volume timber banya")
    variant_box("VariantB_Banya_Foundation_LOD0", parent, (0.0, 0.0, 0.17), (3.34, 2.94, 0.30), ("URMAN_Stone_Mossy", "URMAN_Wood_WetShadow"), root_name, "dark stone banya footing", [0, 1] + [0] * 8, chamfer=0.05)
    variant_roof("VariantB_Banya_Roof_LOD0", parent, 3.34, -1.60, 1.64, 2.24, 0.42, 3.30, 0.18, root_name, thickness=0.12)
    variant_gable("VariantB_Banya_GableFront_LOD0", parent, 3.16, -1.42, -1.22, 2.20, 0.38, 3.16, root_name)
    variant_box("VariantB_Banya_Door_LOD0", parent, (-0.62, -1.46, 1.20), (0.86, 0.12, 1.80), ("URMAN_Wood_Weathered",), root_name, "plain banya door", chamfer=0.035)
    variant_box("VariantB_Banya_DoorHandle_LOD0", parent, (-0.31, -1.56, 1.16), (0.10, 0.07, 0.12), ("URMAN_Metal_Dulled",), root_name, "small metal latch", chamfer=0.022)
    variant_window(parent, "VariantB_Banya_WindowFront", (0.76, -1.46, 1.50), 0.76, 0.64, root_name, glass="URMAN_Window_WarmInset")
    variant_box("VariantB_Banya_RearWindow_LOD0", parent, (-0.54, 1.44, 1.42), (0.64, 0.10, 0.56), ("URMAN_Window_DimGlass",), root_name, "small rear window inset", chamfer=0.03)
    variant_lean_to_roof("VariantB_Banya_EntryRoof_LOD0", parent, -1.34, 0.18, -1.40, -2.22, 2.28, 2.10, root_name, thickness=0.11)
    variant_box("VariantB_Banya_EntryStep_LOD0", parent, (-0.58, -2.25, 0.14), (1.24, 0.46, 0.20), ("URMAN_Stone_MossFace",), root_name, "banya threshold stone", chamfer=0.04)
    variant_box("VariantB_Banya_Chimney_LOD0", parent, (0.72, 0.62, 3.24), (0.46, 0.46, 1.22), ("URMAN_Stone_MossFace", "URMAN_Plaster_Shadow"), root_name, "short masonry chimney", [0, 1] + [0] * 8, chamfer=0.04)
    variant_box("VariantB_Banya_ChimneyCap_LOD0", parent, (0.72, 0.62, 3.88), (0.60, 0.58, 0.13), ("URMAN_Roof_MossTone",), root_name, "chimney cap", chamfer=0.03)
    variant_box("VariantB_Banya_SideRepair_LOD0", parent, (1.54, 0.18, 1.10), (0.12, 1.38, 1.48), ("URMAN_Wood_Weathered",), root_name, "side plank repair panel", chamfer=0.025, rotation=(0.0, 0.0, math.radians(1.0)))


def author_variant_shed_c(parent: bpy.types.Object) -> None:
    root_name = "VariantC_Outbuilding_UtilityLeanTo"
    variant_wall("VariantC_Utility_Wall_LOD0", parent, [(-2.16, -1.08), (2.18, -1.08), (2.10, 1.04), (-2.04, 1.12)], 0.20, 1.96, root_name, front_material="URMAN_Wood_Weathered", side_material="URMAN_Wood_WetShadow", role="long low utility outbuilding volume")
    variant_box("VariantC_Utility_Foundation_LOD0", parent, (0.0, 0.04, 0.14), (4.50, 2.34, 0.25), ("URMAN_Stone_Mossy", "URMAN_Wood_WetShadow"), root_name, "low utility footing", [0, 1] + [0] * 8, chamfer=0.045)
    variant_lean_to_roof("VariantC_Utility_Roof_LOD0", parent, -2.34, 2.36, 1.28, -1.34, 2.48, 2.20, root_name, thickness=0.14)
    variant_box("VariantC_Utility_OpenBay_LOD0", parent, (1.12, -1.16, 1.04), (1.62, 0.14, 1.64), ("URMAN_Wood_Dark",), root_name, "wide open utility bay header", chamfer=0.03)
    for index, x in enumerate((-1.92, 1.88)):
        variant_box(f"VariantC_Utility_Post_{index:02d}_LOD0", parent, (x, -1.16, 1.05), (0.18, 0.18, 1.72), ("URMAN_Wood_Dark",), root_name, "utility bay post", chamfer=0.03, rotation=(0.0, 0.0, math.radians(-0.9 if index == 0 else 1.1)))
    variant_box("VariantC_Utility_Door_LOD0", parent, (-0.72, -1.15, 1.06), (1.08, 0.12, 1.64), ("URMAN_Wood_WetShadow",), root_name, "offset utility plank door", chamfer=0.035)
    variant_box("VariantC_Utility_SideBatten_LOD0", parent, (-2.10, 0.32, 1.04), (0.14, 0.12, 1.68), ("URMAN_Wood_Weathered",), root_name, "side repair batten", chamfer=0.024)
    variant_box("VariantC_Utility_RearBatten_LOD0", parent, (0.60, 1.14, 1.02), (0.14, 0.12, 1.62), ("URMAN_Wood_Dark",), root_name, "rear repair batten", chamfer=0.024, rotation=(0.0, 0.0, math.radians(-1.0)))
    variant_box("VariantC_Utility_Threshold_LOD0", parent, (1.16, -1.36, 0.13), (1.74, 0.34, 0.18), ("URMAN_Stone_MossFace",), root_name, "utility bay threshold", chamfer=0.04)


def author_variant_yard_a(parent: bpy.types.Object) -> None:
    root_name = "VariantA_Yard_OpenRail"
    posts = ((-4.55, -3.72, 1.10), (-3.30, -3.72, 1.38), (-1.00, -3.72, 1.16), (1.48, -3.72, 1.34), (3.78, -3.72, 1.12), (4.60, -3.10, 1.30))
    for index, (x, y, height) in enumerate(posts):
        variant_box(f"VariantA_Yard_Post_{index:02d}_LOD0", parent, (x, y, height / 2.0), (0.18, 0.18, height), ("URMAN_Wood_Dark",), root_name, "uneven open-rail fence post", chamfer=0.032, rotation=(0.0, 0.0, math.radians((-1.2, 0.8, -0.6, 1.1, -0.8, 1.5)[index])))
    for index, z in enumerate((0.48, 0.91)):
        variant_beam(f"VariantA_Yard_FrontRail_{index:02d}_LOD0", parent, (-4.50, -3.72, z), (4.54, -3.10, z + (0.05 if index else -0.03)), 0.12, 0.12, ("URMAN_Wood_Weathered",), root_name, "long crooked open fence rail")
    for index, z in enumerate((0.54, 0.96)):
        variant_box(f"VariantA_Yard_SideRail_{index:02d}_LOD0", parent, (-4.56, -1.25, z), (0.12, 4.78, 0.12), ("URMAN_Wood_Weathered",), root_name, "side yard fence rail", chamfer=0.025, rotation=(0.0, 0.0, math.radians(-1.0 if index == 0 else 0.6)))
    for index, (x, z) in enumerate(((-2.92, 0.12), (2.92, 0.14))):
        variant_box(f"VariantA_Yard_FoundationStone_{index:02d}_LOD0", parent, (x, -3.68, z), (0.62, 0.34, 0.22), ("URMAN_Stone_Mossy", "URMAN_Stone_LightFace"), root_name, "fence foundation stone", [0, 1] + [0] * 8, chamfer=0.05)
    variant_box("VariantA_Yard_GatePostLeft_LOD0", parent, (-0.34, -3.74, 1.12), (0.24, 0.26, 2.24), ("URMAN_Wood_Dark",), root_name, "wide gate post", chamfer=0.04, rotation=(0.0, 0.0, math.radians(-1.2)))
    variant_box("VariantA_Yard_GatePostRight_LOD0", parent, (1.18, -3.60, 1.16), (0.24, 0.26, 2.32), ("URMAN_Wood_Dark",), root_name, "wide gate post", chamfer=0.04, rotation=(0.0, 0.0, math.radians(1.0)))
    variant_beam("VariantA_Yard_GateHeader_LOD0", parent, (-0.38, -3.76, 2.15), (1.22, -3.62, 2.20), 0.18, 0.18, ("URMAN_Wood_Weathered",), root_name, "uneven gate header")
    variant_beam("VariantA_Yard_GateBrace_LOD0", parent, (-0.20, -3.86, 0.46), (1.05, -3.86, 1.92), 0.12, 0.12, ("URMAN_Wood_WetShadow",), root_name, "diagonal gate repair brace")
    variant_box("VariantA_Yard_GateThreshold_LOD0", parent, (0.42, -3.54, 0.10), (1.78, 0.52, 0.18), ("URMAN_Stone_MossFace",), root_name, "gate threshold stone", chamfer=0.045)
    variant_box("VariantA_Yard_BenchSeat_LOD0", parent, (3.02, 1.55, 0.70), (1.36, 0.38, 0.16), ("URMAN_Wood_Weathered",), root_name, "plain yard bench seat", chamfer=0.035)
    for index, x in enumerate((2.55, 3.49)):
        variant_box(f"VariantA_Yard_BenchLeg_{index:02d}_LOD0", parent, (x, 1.55, 0.36), (0.16, 0.24, 0.68), ("URMAN_Wood_Dark",), root_name, "bench leg", chamfer=0.025)


def author_variant_yard_b(parent: bpy.types.Object) -> None:
    root_name = "VariantB_Yard_TallPlank"
    board_specs = ((-4.42, 1.54), (-4.02, 1.40), (-3.62, 1.66), (-3.22, 1.46), (-2.82, 1.56), (-2.42, 1.38), (-2.02, 1.62), (-1.62, 1.44), (-1.22, 1.55), (-0.82, 1.36), (-0.42, 1.60), (0.00, 1.43), (0.42, 1.52), (0.84, 1.38), (1.26, 1.64), (1.68, 1.45), (2.10, 1.54), (2.52, 1.40), (2.94, 1.62), (3.36, 1.46), (3.78, 1.56), (4.18, 1.40))
    for index, (x, height) in enumerate(board_specs):
        variant_box(f"VariantB_Yard_Plank_{index:02d}_LOD0", parent, (x, -3.50, height / 2.0), (0.28, 0.16, height), ("URMAN_Wood_WetShadow", "URMAN_Wood_Weathered"), root_name, "staggered tall plank fence board", [0 if index % 3 else 1] + [0] * 9, chamfer=0.025, rotation=(0.0, math.radians((-1.2, 0.4, 1.1, -0.8)[index % 4]), 0.0))
    for index, x in enumerate((-4.55, -2.20, 0.12, 2.40, 4.48)):
        variant_box(f"VariantB_Yard_Post_{index:02d}_LOD0", parent, (x, -3.54, 0.92), (0.30, 0.30, 1.84), ("URMAN_Wood_Dark",), root_name, "heavy repaired plank fence post", chamfer=0.045, rotation=(0.0, 0.0, math.radians((-0.7, 0.8, -1.0, 0.6, -0.5)[index])))
    variant_box("VariantB_Yard_RailUpper_LOD0", parent, (0.0, -3.62, 1.45), (8.88, 0.18, 0.14), ("URMAN_Wood_Dark",), root_name, "upper plank fence rail", chamfer=0.03, rotation=(0.0, 0.0, math.radians(-0.5)))
    variant_box("VariantB_Yard_RailLower_LOD0", parent, (0.0, -3.60, 0.54), (8.88, 0.18, 0.14), ("URMAN_Wood_Weathered",), root_name, "lower plank fence rail", chamfer=0.03, rotation=(0.0, 0.0, math.radians(0.7)))
    variant_box("VariantB_Yard_GateLeft_LOD0", parent, (1.22, -3.76, 1.02), (0.20, 0.18, 2.04), ("URMAN_Wood_Dark",), root_name, "narrow plank gate leaf", chamfer=0.03, rotation=(0.0, math.radians(-5.0), 0.0))
    variant_box("VariantB_Yard_GateRight_LOD0", parent, (1.80, -3.75, 0.96), (0.20, 0.18, 1.92), ("URMAN_Wood_Dark",), root_name, "narrow plank gate leaf", chamfer=0.03, rotation=(0.0, math.radians(4.0), 0.0))
    variant_box("VariantB_Yard_GateHeader_LOD0", parent, (1.52, -3.68, 2.08), (1.08, 0.22, 0.18), ("URMAN_Wood_Weathered",), root_name, "low gate header", chamfer=0.03)
    variant_box("VariantB_Yard_Threshold_LOD0", parent, (1.52, -3.46, 0.10), (1.14, 0.48, 0.18), ("URMAN_Stone_Mossy",), root_name, "narrow gate threshold", chamfer=0.045)
    variant_box("VariantB_Yard_SideRail_LOD0", parent, (-4.56, -1.12, 0.74), (0.16, 4.52, 0.14), ("URMAN_Wood_Weathered",), root_name, "side boundary rail", chamfer=0.025, rotation=(0.0, 0.0, math.radians(-1.4)))
    variant_box("VariantB_Yard_WoodStackBase_LOD0", parent, (3.62, 1.18, 0.22), (1.46, 0.74, 0.34), ("URMAN_Wood_WetShadow",), root_name, "utility wood stack base", chamfer=0.05)
    for index, z in enumerate((0.48, 0.80)):
        variant_box(f"VariantB_Yard_WoodStackLog_{index:02d}_LOD0", parent, (3.62, 1.18 + index * 0.02, z), (1.34, 0.24, 0.18), ("URMAN_Wood_Weathered",), root_name, "stacked utility log", chamfer=0.045, rotation=(0.0, math.radians(4.0 if index else -3.0), 0.0))


def author_variant_yard_c(parent: bpy.types.Object) -> None:
    root_name = "VariantC_Yard_MixedRepair"
    for index, (x, height) in enumerate(((-4.56, 1.24), (-3.20, 1.00), (-1.82, 1.30), (-0.44, 1.06), (0.92, 1.34), (2.30, 1.12), (3.68, 1.24))):
        variant_box(f"VariantC_Yard_Post_{index:02d}_LOD0", parent, (x, -3.14, height / 2.0), (0.18, 0.18, height), ("URMAN_Wood_Dark",), root_name, "mixed repair fence post", chamfer=0.03, rotation=(0.0, 0.0, math.radians((-1.0, 0.8, -0.6, 1.2, -0.9, 0.6, -0.7)[index])))
    for index, z in enumerate((0.46, 0.86)):
        variant_beam(f"VariantC_Yard_Rail_{index:02d}_LOD0", parent, (-4.50, -3.14, z), (3.72, -3.14, z + (0.08 if index else -0.04)), 0.12, 0.12, ("URMAN_Wood_Weathered",), root_name, "broken-height mixed fence rail")
    for index, (x, height) in enumerate(((-4.20, 0.78), (-3.78, 1.02), (-3.34, 0.70), (2.82, 0.94), (3.24, 0.66), (3.66, 0.88))):
        variant_box(f"VariantC_Yard_Picket_{index:02d}_LOD0", parent, (x, -3.20, height / 2.0), (0.22, 0.14, height), ("URMAN_Wood_Weathered",), root_name, "short repaired picket", chamfer=0.025, rotation=(0.0, 0.0, math.radians((-2.0, 1.0, 2.4)[index % 3])))
    variant_box("VariantC_Yard_CornerRail_LOD0", parent, (-4.60, -0.78, 0.72), (0.14, 4.64, 0.14), ("URMAN_Wood_WetShadow",), root_name, "side boundary corner rail", chamfer=0.025, rotation=(0.0, 0.0, math.radians(-1.6)))
    variant_box("VariantC_Yard_GatePostLeft_LOD0", parent, (0.92, -3.28, 1.02), (0.24, 0.24, 2.04), ("URMAN_Wood_Dark",), root_name, "wide gate post", chamfer=0.04, rotation=(0.0, 0.0, math.radians(-1.0)))
    variant_box("VariantC_Yard_GatePostRight_LOD0", parent, (2.26, -3.22, 1.08), (0.24, 0.24, 2.16), ("URMAN_Wood_Dark",), root_name, "wide gate post", chamfer=0.04, rotation=(0.0, 0.0, math.radians(1.3)))
    variant_beam("VariantC_Yard_GateHeader_LOD0", parent, (0.88, -3.34, 2.06), (2.32, -3.28, 2.13), 0.18, 0.18, ("URMAN_Wood_Weathered",), root_name, "crooked wide gate header")
    variant_beam("VariantC_Yard_GateBrace_LOD0", parent, (1.08, -3.42, 0.42), (2.14, -3.42, 1.90), 0.12, 0.12, ("URMAN_Wood_WetShadow",), root_name, "gate repair brace")
    variant_box("VariantC_Yard_GateThreshold_LOD0", parent, (1.60, -3.08, 0.10), (1.72, 0.52, 0.18), ("URMAN_Stone_MossFace",), root_name, "wide gate threshold stone", chamfer=0.045)
    variant_box("VariantC_Yard_BenchSeat_LOD0", parent, (-0.94, 1.52, 0.64), (1.52, 0.36, 0.16), ("URMAN_Wood_Weathered",), root_name, "plain yard bench", chamfer=0.035)
    variant_box("VariantC_Yard_BenchBack_LOD0", parent, (-0.94, 1.66, 1.02), (1.52, 0.14, 0.72), ("URMAN_Wood_WetShadow",), root_name, "plain yard bench back", chamfer=0.03, rotation=(math.radians(-4.0), 0.0, 0.0))
    for index, x in enumerate((-1.48, -0.40)):
        variant_box(f"VariantC_Yard_BenchLeg_{index:02d}_LOD0", parent, (x, 1.52, 0.32), (0.16, 0.22, 0.58), ("URMAN_Wood_Dark",), root_name, "bench leg", chamfer=0.025)


def clear_variant_roots(root: bpy.types.Object) -> None:
    for name in VARIANT_LAYOUT:
        variant = bpy.data.objects.get(name)
        if variant is None:
            continue
        descendants = list(variant.children_recursive)
        for obj in reversed(descendants):
            mesh = obj.data if obj.type == "MESH" else None
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh is not None and mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        bpy.data.objects.remove(variant, do_unlink=True)


def author_variant_parcels(root: bpy.types.Object) -> None:
    parcels = (
        ("VillageParcel_VariantA_TimberGable", "timber gable dwelling with open rail yard", "author_variant_shed_a", "author_variant_yard_a"),
        ("VillageParcel_VariantB_PlasterAnnex", "plaster dwelling with enclosed seni and tall plank boundary", "author_variant_shed_b", "author_variant_yard_b"),
        ("VillageParcel_VariantC_BanyaYard", "dark timber dwelling with low utility shed and mixed repair fence", "author_variant_shed_c", "author_variant_yard_c"),
    )
    for parcel_name, description, shed_author, yard_author in parcels:
        parcel = variant_empty(parcel_name, root, VARIANT_LAYOUT[parcel_name], role="optional full village parcel variant", family=description)
        parcel["preview_origin"] = VARIANT_LAYOUT[parcel_name]
        house = variant_empty(f"{parcel_name}_Dwelling", parcel, role="full-volume variant dwelling", family=description)
        shed = variant_empty(f"{parcel_name}_Outbuilding", parcel, (-6.0, 2.3, 0.0), role="full-volume variant outbuilding", family=description)
        yard = variant_empty(f"{parcel_name}_Yard", parcel, (0.0, -4.5, 0.0), role="full-volume variant yard boundary", family=description)
        dimensions = {
            "VillageParcel_VariantA_TimberGable": (6.0, 5.8, 2.85, 4.55, "URMAN_Wood_Weathered"),
            "VillageParcel_VariantB_PlasterAnnex": (5.5, 6.4, 2.80, 4.25, "URMAN_Plaster_Ochre"),
            "VillageParcel_VariantC_BanyaYard": (5.8, 5.5, 2.95, 4.65, "URMAN_Wood_WetShadow"),
        }
        author_rural_dwelling(house, *dimensions[parcel_name])
        globals()[shed_author](shed)
        globals()[yard_author](yard)
        author_variant_composition(yard, parcel_name)


def author_rural_dwelling(parent, width=6.2, depth=6.0, eave=2.9, ridge=4.65,
                          wall_material="URMAN_Plaster_Ochre", hero_layout=False):
    """One inhabited house: pierced wall shell, boarded gables and enclosed side seni.

    Parcel street elevations have windows only; the hero retains its portal.
    Openings belong to the wall topology,
    so dark reveals are real depth rather than panels pasted onto a solid box.
    """
    for child in reversed(list(parent.children_recursive)):
        mesh = child.data if child.type == "MESH" else None
        bpy.data.objects.remove(child, do_unlink=True)
        if mesh is not None and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    prefix = "HeroHouse" if hero_layout else "DwellingFacade" if parent.name == DWELLING_ROOT else parent.name.replace("VillageParcel_", "")
    half = width / 2
    front = -1.066 if hero_layout else -1.30
    back = front + depth
    wall_base = HERO_HOUSE_CONTRACT["floor_above_component_ground"] if hero_layout else .30
    root_name = parent.name
    trim = "URMAN_Wood_Weathered"
    # The hero reads as a dark inhabited srub: hewn log courses, muted teal
    # casings, a lighter outer sill. Parcels keep the previous weathered joinery.
    hero_joinery = HERO_TRIM_TEAL_MATERIAL if hero_layout else trim
    hero_sill = HERO_TRIM_IVORY_MATERIAL if hero_layout else trim
    wall_finish = HERO_LOG_MATERIAL if hero_layout else wall_material

    def box(suffix, center, size, mat=trim, chamfer=0.008):
        return variant_box(f"{prefix}_{suffix}_LOD0", parent, center, size,
                           (mat,), root_name, "rural dwelling joinery", chamfer=chamfer)

    def wall(suffix, origin, tangent, length, holes, top=eave, finish=None):
        finish = wall_finish if finish is None else finish
        # Front orientation is tangent +X, outward -Y; rotating the basis also
        # rotates wall thickness, frame, glass and sill as one architectural unit.
        tx, ty = tangent
        nx, ny = ty, -tx
        def point(u, inset, z):
            return (origin[0] + tx*u - nx*inset, origin[1] + ty*u - ny*inset, z)
        vertices, faces = [], []
        def quad(points):
            start = len(vertices)
            vertices.extend(points)
            faces.append(tuple(range(start, start+4)))
        xs = sorted(set([-length/2, length/2] + [h[k] for h in holes for k in (0, 1)]))
        zs = sorted(set([wall_base, top] + [h[k] for h in holes for k in (2, 3)]))
        for x0, x1 in zip(xs, xs[1:]):
            for z0, z1 in zip(zs, zs[1:]):
                if any(h[0] < (x0+x1)/2 < h[1] and h[2] < (z0+z1)/2 < h[3] for h in holes):
                    continue
                quad([point(x0, 0, z0), point(x1, 0, z0), point(x1, 0, z1), point(x0, 0, z1)])
                quad([point(x0, .20, z1), point(x1, .20, z1), point(x1, .20, z0), point(x0, .20, z0)])
        for x0, x1, z0, z1, kind in holes:
            quad([point(x1, 0, z0), point(x1, .20, z0), point(x0, .20, z0), point(x0, 0, z0)])
            quad([point(x0, 0, z1), point(x0, .20, z1), point(x1, .20, z1), point(x1, 0, z1)])
            for x in (x0, x1):
                reveal = [point(x, 0, z0), point(x, 0, z1), point(x, .20, z1), point(x, .20, z0)]
                quad(list(reversed(reveal)) if x == x0 else reveal)
        mesh_object(f"{prefix}_{suffix}_Wall_LOD0", parent, vertices, faces,
                    (finish,), role="continuous pierced wall and 20cm deep reveals", component_root=root_name)

        def local_box(name, u, inset, z, sx, sy, sz, mat):
            obj = box(name, point(u, inset, z), (sx, sy, sz), mat)
            obj.rotation_euler.z = math.atan2(ty, tx)
        casing_inset = HERO_CASING_INSET if hero_layout else PARCEL_CASING_INSET
        casing_depth = HERO_CASING_DEPTH if hero_layout else PARCEL_CASING_DEPTH
        for i, (x0, x1, z0, z1, kind) in enumerate(holes):
            tag = f"{suffix}_{kind}{i}"
            x, z, w, h = (x0+x1)/2, (z0+z1)/2, x1-x0, z1-z0
            if kind != "Portal":
                local_box(tag+"_Recess", x, .21, z, w, .045, h, "URMAN_Wood_Dark")
                local_box(tag+"_Glass" if kind == "Window" else tag+"_Leaf", x, .17, z,
                          w-.10, .035, h-.10, "URMAN_Window_DimGlass" if kind == "Window" else "URMAN_Wood_WetShadow")
            for side in (-1, 1):
                local_box(tag+f"_Jamb{side}", x+side*(w/2+.035), casing_inset, z,
                          .095, casing_depth, h+.14, hero_joinery)
                local_box(tag+f"_Rail{side}", x, casing_inset, z+side*(h/2+.035),
                          w+.16, casing_depth, .095, hero_joinery)
            if kind == "Window":
                # Mid-distance read: jambs + rails + mullion + sill frame the
                # opening; the former transom bar and tapered headboard crown
                # read as a timber lattice at street distance, so both are
                # retired. Concept target: solid wall, quiet dark opening.
                local_box(tag+"_Mullion", x, .12, z, .045, .06, h-.07, hero_joinery)
                local_box(tag+"_Sill", x, -.09, z0-.08, w+.25, .30, .075, hero_sill)
            elif kind == "Door":
                local_box(tag+"_Handle", x+w*.30, .08, z, .045, .07, .16, "URMAN_Metal_Dulled")

    street_windows = [(x-.48, x+.48, .93, 2.38, "Window") for x in (-width*.29, 0, width*.29)]
    if parent.name == DWELLING_ROOT:
        # Preserve the independently owned Babai doorway's original opening.
        street_windows = [(-2.07,-.77,.30,2.55,"Portal"),
                          (-.28,.68,.93,2.38,"Window"),(1.22,2.18,.93,2.38,"Window")]
    if hero_layout:
        door_x = HERO_HOUSE_CONTRACT["room_door_x"]
        sill, top = [wall_base + z for z in HERO_HOUSE_CONTRACT["window_sill_top_above_floor"]]
        street_windows = [(door_x-.65, door_x+.65, wall_base, wall_base+2.25, "Portal")]
        street_windows += [(x-.53, x+.53, sill, top, "Window")
                           for x in HERO_HOUSE_CONTRACT["front_window_room_x"]]
    wall("Street", (0,front), (1,0), width, street_windows)
    if parent.name == DWELLING_ROOT or hero_layout:
        # One removable visual leaf: runtime hides only this child at Babai's
        # independently owned portal, while neighboring houses stay closed.
        vertices, faces, indices = [], [], []
        door_x = HERO_HOUSE_CONTRACT["room_door_x"] if hero_layout else -1.42
        door_mid = wall_base + 1.125
        parts = [((door_x, front+.12, door_mid), (1.22,.08,2.17), 0)]
        parts += [((door_x, front+.065, wall_base+z), (1.18,.035,.095), 1) for z in (.34,1.88)]
        parts += [((door_x+x, front+.073, door_mid), (.012,.018,2.14), 1) for x in (-.4,-.2,0,.2,.4)]
        parts.append(((door_x+.48,front+.025,wall_base+1.05),(.055,.075,.15),2))
        for (x,y,z),(w,d,h),mat_index in parts:
            start=len(vertices)
            vertices += [(x+dx*w/2,y+dy*d/2,z+dz*h/2)
                         for dx,dy,dz in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
                                          (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))]
            faces += [tuple(start+i for i in f) for f in
                      ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))]
            indices += [mat_index]*6
        mesh_object(f"{prefix}_StreetDoorClosed_LOD0",parent,vertices,faces,
                    ("URMAN_Wood_WetShadow","URMAN_Wood_Weathered","URMAN_Metal_Dulled"),indices,
                    component_root=root_name,role="removable closed street door; hidden only at Babai gameplay portal")
    if hero_layout:
        rear_holes = [(-x-.53,-x+.53,sill,top,"Window")
                      for x in HERO_HOUSE_CONTRACT["rear_window_room_x"]]
        left_holes = [(z-.53,z+.53,sill,top,"Window")
                      for z in HERO_HOUSE_CONTRACT["left_window_room_z"]]
        right_holes = [(-z-.53,-z+.53,sill,top,"Window")
                       for z in HERO_HOUSE_CONTRACT["right_window_room_z"]]
        wall("Rear", (0,back), (-1,0), width, rear_holes)
        wall("Left", (-half,(front+back)/2), (0,-1), depth, left_holes)
        wall("Right", (half,(front+back)/2), (0,1), depth, right_holes)
        # Stacked hewn courses over the pierced shell. The shell keeps the wall
        # thickness, the deep reveals and every opening; the courses restore the
        # horizontal timber bond that a flat box loses. Courses are cut where an
        # opening crosses them, and the casing covers each cut.
        log_thickness = HERO_LOG_BULGE + HERO_LOG_EMBED
        log_setback = (HERO_LOG_BULGE - HERO_LOG_EMBED) / 2.0
        log_step = HERO_LOG_HEIGHT + HERO_LOG_COURSE_GAP

        def log_wall(suffix, origin, tangent, length, holes):
            tx, ty = tangent
            nx, ny = ty, -tx
            yaw = math.atan2(ty, tx)
            course = 0
            z = wall_base
            while z < eave - .06:
                z1 = min(z + HERO_LOG_HEIGHT, eave)
                spans = [(-length/2, length/2)]
                for x0, x1, hole_z0, hole_z1, _kind in holes:
                    if hole_z1 <= z or hole_z0 >= z1:
                        continue
                    cut = []
                    for a, b in spans:
                        if x0 - a > .06:
                            cut.append((a, min(b, x0)))
                        if b - max(a, x1) > .06:
                            cut.append((max(a, x1), b))
                    spans = cut
                for index, (a, b) in enumerate(spans):
                    u, v = (a + b) / 2.0, (z + z1) / 2.0
                    member = box(f"{suffix}_Log{course:02d}_{index}",
                                 (origin[0] + tx*u - nx*(-log_setback),
                                  origin[1] + ty*u - ny*(-log_setback), v),
                                 (b - a, log_thickness, z1 - z), HERO_LOG_MATERIAL,
                                 chamfer=.024)
                    member.rotation_euler.z = yaw
                course += 1
                z += log_step
            return course

        log_courses = log_wall("Street", (0,front), (1,0), width, street_windows)
        log_wall("Rear", (0,back), (-1,0), width, rear_holes)
        log_wall("Left", (-half,(front+back)/2), (0,-1), depth, left_holes)
        log_wall("Right", (half,(front+back)/2), (0,1), depth, right_holes)
        # Alternating corner bond: on even courses the street/rear logs run
        # through and show end grain past the corner, on odd courses the side
        # logs do. This replaces the old plywood-box corner posts inside the
        # hero branch only; parcels keep theirs.
        for course in range(log_courses):
            z0 = wall_base + course * log_step
            z1 = min(z0 + HERO_LOG_HEIGHT, eave)
            if z1 - z0 < .08:
                continue
            middle = (z0 + z1) / 2.0
            through_street = course % 2 == 0
            for sx in (-1, 1):
                for sy in (-1, 1):
                    corner_x, corner_y = sx * half, front if sy < 0 else back
                    out_y = -1.0 if sy < 0 else 1.0
                    if through_street:
                        center = (corner_x + sx * HERO_LOG_END_LENGTH / 2.0,
                                  corner_y + out_y * log_setback, middle)
                        size = (HERO_LOG_END_LENGTH, log_thickness, z1 - z0)
                    else:
                        center = (corner_x + sx * log_setback,
                                  corner_y + sy * HERO_LOG_END_LENGTH / 2.0, middle)
                        size = (log_thickness, HERO_LOG_END_LENGTH, z1 - z0)
                    box(f"CornerEnd{sx}_{sy}_Log{course:02d}", center, size,
                        HERO_LOG_END_MATERIAL, chamfer=.024)
    else:
        wall("Rear", (0,back), (-1,0), width, [(-1.45,-.49,.95,2.38,"Window"),(.49,1.45,.95,2.38,"Window")])
        wall("Left", (-half,(front+back)/2), (0,-1), depth,
             [(-1.65,-.65,.94,2.38,"Window"),(.65,1.65,.94,2.38,"Window")])
        wall("Right", (half,(front+back)/2), (0,1), depth,
             [(-depth/2+.80,-depth/2+1.80,.94,2.38,"Window")])
    if hero_layout:
        # The occupied room has its own timber floor. A full stone cap at
        # floor+55mm covered it with an exterior snow material. Keep both
        # published foundation names, but build the actual bearing perimeter
        # underneath the four walls, with no stone surface across the room.
        def foundation_ring(suffix, extension, bottom, top, material_name):
            x_outer = (width + extension) / 2
            y_front, y_back = front - extension / 2, back + extension / 2
            x_inner, inner_front, inner_back = half - .20, front + .20, back - .20
            rectangles = [(-x_outer, x_outer, y_front, inner_front),
                          (-x_outer, x_outer, inner_back, y_back),
                          (-x_outer, -x_inner, inner_front, inner_back),
                          (x_inner, x_outer, inner_front, inner_back)]
            vertices, faces = [], []
            for left, right, near, far in rectangles:
                offset = len(vertices)
                vertices += [(x, y, z) for z in (bottom, top)
                             for x, y in ((left, near), (right, near), (right, far), (left, far))]
                faces += [tuple(offset + i for i in face) for face in
                          ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))]
            mesh_object(f"{prefix}_{suffix}_LOD0", parent, vertices, faces,
                        (material_name,), component_root=root_name,
                        role="continuous perimeter support beneath hero room walls; clear timber floor inside")
        foundation_ring("Foundation", .06, 0, wall_base, "URMAN_Stone_Mossy")
        foundation_ring("FootingCap", .09, wall_base - .015, wall_base + .055, "URMAN_Stone_LightFace")
    else:
        box("Foundation", (0,(front+back)/2,wall_base/2), (width+.06,depth+.06,wall_base), "URMAN_Stone_Mossy")
        box("FootingCap", (0,(front+back)/2,wall_base+.02), (width+.09,depth+.09,.07), "URMAN_Stone_LightFace")
    if parent.name == DWELLING_ROOT or hero_layout:
        # A real threshold at the hero's street portal. The wall base sits
        # 0.30 m above grade, so without a step the doorway read as a door
        # standing in the snow. Hero-only, like the portal itself: parcel
        # yards are too tight for the protrusion and their street doors are
        # not entries. Presentation-only, clear of the portal opening.
        box("StreetStep", (door_x, front-.34, .10), (1.34, .68, .20), "URMAN_Stone_Mossy")
    for x in (-half,half):
        for y in (front,back):
            if hero_layout:
                # The alternating log bond above owns the hero's corners; the
                # parcels keep the previous corner posts.
                continue
            box(f"Corner_{x}_{y}",(x,y,(wall_base+eave)/2 if hero_layout else 1.60),
                (.105,.105,eave-wall_base if hero_layout else 2.60))
    # ACT1-DEPTH.12 roof mass: the hero carries the deep overhang and thicker
    # slab of the menu reference; parcels keep the established eave until the
    # architecture is accepted and propagated (ACT1-DEPTH.10).
    roof_overhang = .62 if hero_layout else .36
    roof_thickness = .16 if hero_layout else .09
    # Authored clipped gable boards form the triangle itself (no solid triangle
    # plus beam lattice); tiny gaps give actual self-shadow at grazing angles.
    for label,y in (("Front",front),("Back",back)):
        vertices,faces=[],[]
        count=round(width/.22)
        for i in range(count):
            x0=-half+i*width/count+.004
            x1=-half+(i+1)*width/count-.004
            z0=eave+(ridge-eave)*(1-abs(x0)/half)
            z1=eave+(ridge-eave)*(1-abs(x1)/half)
            start=len(vertices)
            vertices.extend([(x0,y,eave-.04),(x1,y,eave-.04),(x1,y,z1),(x0,y,z0)])
            indices = tuple(range(start,start+4))
            faces.append(tuple(reversed(indices)) if label == "Back" else indices)
        mesh_object(f"{prefix}_{label}_BoardedGable_LOD0",parent,vertices,faces,
                    (HERO_LOG_MATERIAL if hero_layout else trim,),
                    component_root=root_name,role="clipped vertical gable boarding")
        if hero_layout:
            # The hero's gable keeps the boarding as a dark backing and gains
            # the stepped ("samsovaya") log courses over it, so the roof does
            # not sit on a flat triangle above a log wall.
            outward = -1.0 if label == "Front" else 1.0
            gable_z = eave
            gable_course = 0
            while gable_z < ridge - .05:
                z1 = min(gable_z + HERO_LOG_HEIGHT, ridge)
                span = half * max(0.0, (ridge - z1) / (ridge - eave))
                if span > .16:
                    plane_y = y + outward * log_setback
                    box(f"{label}GableLog{gable_course:02d}",
                        (0.0, plane_y, (gable_z + z1) / 2.0),
                        (2.0 * span, log_thickness, z1 - gable_z), HERO_LOG_MATERIAL,
                        chamfer=.024)
                    for side in (-1, 1):
                        box(f"{label}GableEnd{gable_course:02d}_{side}",
                            (side * (span + HERO_LOG_END_LENGTH / 2.0), plane_y,
                             (gable_z + z1) / 2.0),
                            (HERO_LOG_END_LENGTH, log_thickness, z1 - gable_z),
                            HERO_LOG_END_MATERIAL, chamfer=.024)
                gable_course += 1
                gable_z += log_step
        edge_y = front-(roof_overhang+.005) if label == "Front" else back+(roof_overhang+.005)
        verge_width = .105 if hero_layout else .095
        verge_depth = .12 if hero_layout else .09
        variant_beam(f"{prefix}_{label}_VergeLeft_LOD0",parent,(-half-roof_overhang,edge_y,eave-.02),
                     (0,edge_y,ridge+.10),verge_width,verge_depth,(trim,),root_name,"narrow roof verge")
        variant_beam(f"{prefix}_{label}_VergeRight_LOD0",parent,(0,edge_y,ridge+.10),
                     (half+roof_overhang,edge_y,eave-.02),verge_width,verge_depth,(trim,),root_name,"narrow roof verge")
    roof_vertices = [(x,y,z) for y in (front-roof_overhang,back+roof_overhang)
                     for x,z in ((-half-roof_overhang,eave),(0,ridge+.12),(half+roof_overhang,eave))]
    roof_vertices += [(x,y,z-roof_thickness) for x,y,z in roof_vertices]
    mesh_object(f"{prefix}_Roof_LOD0",parent,roof_vertices,
                [(0,1,4,3),(1,2,5,4),(6,9,10,7),(7,10,11,8),(0,1,7,6),
                 (1,2,8,7),(2,5,11,8),(5,4,10,11),(4,3,9,10),(3,0,6,9)],
                ("URMAN_Roof_WetSlate","URMAN_Wood_WetShadow"),[0,0,1,1,1,1,1,1,1,1],
                component_root=root_name,
                role=f"continuous thick pitched roof with {round(roof_overhang*100)}cm overhang")
    if hero_layout:
        # ACT1-DEPTH.12 winter mass: the settled cap is a closed solid that
        # rides the slope 10cm above the slate, buries its underside inside the
        # thicker slab and rolls 13cm past the eave, so the street view reads a
        # loaded cornice with a dark gap above the fascia instead of a paper
        # sheet glued to the slope.
        snow_run = roof_overhang + .13
        snow_slope = (ridge + .12 - eave) / (half + roof_overhang)
        snow_eave_z = eave + .10 - snow_slope * .13
        snow = [(x, y, z) for y in (front-roof_overhang, back+roof_overhang)
                for x, z in ((-half-snow_run, snow_eave_z), (0, ridge+.22),
                             (half+snow_run, snow_eave_z))]
        snow += [(x, y, z-.13) for x, y, z in snow]
        mesh_object(f"{prefix}_RoofSnow_LOD0", parent, snow,
                    [(0,1,4,3),(1,2,5,4),(6,9,10,7),(7,10,11,8),(0,1,7,6),
                     (1,2,8,7),(2,5,11,8),(5,4,10,11),(4,3,9,10),(3,0,6,9)],
                    (HERO_ROOF_SNOW_MATERIAL,), component_root=root_name,
                    role="solid settled snow slab wrapping the hero eave")
    if hero_layout:
        for x in (-half-roof_overhang+.035,half+roof_overhang-.035):
            box(f"Eave_{x:.2f}",(x,(front+back)/2,eave-.085),(.09,depth+2*roof_overhang+.12,.19))
    else:
        for x in (-half-.35,half+.35):
            box(f"Eave_{x}",(x,(front+back)/2,eave-.065),(.085,depth+.72,.13))
    # Water and the rafter line. §12.5 wants every projection to earn its place:
    # rafter tails explain why the roof overhangs, the trough catches what they
    # shed, and one downpipe on the street corner carries it past the new stone
    # plinth onto a splash stone instead of down the wall face.
    slope = (ridge-eave)/half
    tail_span = roof_overhang if hero_layout else .41
    tail_count = int((depth+2*roof_overhang-.12)//.56) + 1
    tail_start = front-(roof_overhang-.06)
    for side in (-1,1):
        wall_x = side*(half-.02)
        eave_x = side*(half+roof_overhang)
        for i in range(tail_count):
            y = tail_start + i*.56
            if y > back+(roof_overhang-.06): break
            variant_beam(f"{prefix}_RafterTail_{side:+.0f}_{i:02d}_LOD0",parent,
                         (wall_x,y,eave+.23),(eave_x,y,eave-.02),.07,.10,
                         (trim,),root_name,"rafter tail carrying the eave overhang")
        variant_box(f"{prefix}_Gutter_{side:+.0f}_LOD0",parent,
                    (side*(half+roof_overhang+.08),(front+back)/2,
                     eave-(.24 if hero_layout else .19)),
                    (.11,depth+2*roof_overhang,.13),("URMAN_Wood_WetShadow",),root_name,
                    "eaves trough", [0,1]+[0]*8, chamfer=.03)
    box("Downpipe",(half+.06,front-.02,eave/2+.05),(.09,.09,eave-.50),"URMAN_Metal_Dulled")
    box("DownpipeSplash",(half+.15,front-.06,.08),(.34,.34,.16),"URMAN_Stone_Mossy")
    # Prod-ready phase 6 silhouette: a dark ridge beam caps the roofline and
    # a masonry chimney (on most dwellings, not all) breaks the roof plane
    # and catches the low sun. Presentation-only geometry, same materials.
    if hero_layout:
        # The settled snow slab owns the ridge line now: the beam sits fully
        # between the slab and the slate so no sliver or end cap shows in snow.
        box("RidgeBeam",(0,(front+back)/2,ridge+.10),(.13,depth+.72,.11))
    else:
        box("RidgeBeam",(0,(front+back)/2,ridge+.17),(.13,depth+.72,.11))
    if "VariantA" not in parent.name:
        chimney_z0 = eave + .55
        chimney_z1 = ridge + .85
        box("ChimneyStack",(-width*.22,(front+back)/2,(chimney_z0+chimney_z1)/2),
            (.52,.44,chimney_z1-chimney_z0),"URMAN_Stone_Mossy")
        box("ChimneyCap",(-width*.22,(front+back)/2,chimney_z1+.055),
            (.68,.60,.11),"URMAN_Roof_MossTone")
    # Side seni: subordinate enclosed entry volume, a full human-height door,
    # and a sloping roof meeting the main side below its eave.
    outer=half+1.50
    entry_y=back-2.55
    sx=(half+outer)/2
    wall("SeniEntry",(sx,entry_y),(1,0),1.50,[(-.47,.47,.30,2.32,"Door")],2.48,trim)
    wall("SeniOuter",(outer,(entry_y+back)/2),(0,1),2.55,[(-.45,.35,1.04,2.10,"Window")],2.48,trim)
    wall("SeniRear",(sx,back),(-1,0),1.50,[],2.48,trim)
    for label,y in (("Front",entry_y),("Rear",back)):
        mesh_object(f"{prefix}_Seni{label}RoofInfill_LOD0",parent,
                    [(half,y,2.48),(outer,y,2.48),(outer,y,2.51),(half,y,2.737)],
                    [(0,1,2,3) if label == "Front" else (3,2,1,0)],(trim,),component_root=root_name,role="seni sloped roof closure")
    roof_verts=[(half-.08,entry_y-.23,2.83),(outer+.25,entry_y-.23,2.54),
                (outer+.25,back+.25,2.54),(half-.08,back+.25,2.83)]
    roof_verts += [(x,y,z-.08) for x,y,z in roof_verts]
    mesh_object(f"{prefix}_SeniRoof_LOD0",parent,roof_verts,
                [(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],
                ("URMAN_Roof_WetSlate",),component_root=root_name,role="enclosed side seni lean-to roof")
    box("SeniFooting",(sx,(entry_y+back)/2,.15),(1.54,2.58,.30),"URMAN_Wood_WetShadow")
    box("SeniStep",(sx,entry_y-.34,.10),(1.20,.64,.20),"URMAN_Wood_WetShadow")
    # Seni drainage. The lean-to sheds toward its outer edge, so a trough along
    # that low edge catches the runoff and a spout at its street end puts it on
    # a splash stone - otherwise the water lands beside the entry step, the one
    # place the household walks.
    low_x=outer+.25
    variant_box(f"{prefix}_SeniTrough_LOD0",parent,
                (low_x,(entry_y+back)/2,2.40),
                (.13,back-entry_y+.48,.14),
                ("URMAN_Wood_WetShadow",),root_name,"seni eaves trough",
                [0,1]+[0]*8,chamfer=.03)
    box("SeniSpout",(low_x+.03,entry_y-.38,2.30),(.12,.30,.11))
    box("SeniSplashStone",(low_x+.12,entry_y-.42,.08),(.40,.36,.16),"URMAN_Stone_Mossy")
    if hero_layout:
        chimney_x, stove_z = HERO_HOUSE_CONTRACT["stove_room_xz"]
        chimney_y = (front+back)/2 - stove_z
        roof_at_flue = eave + (ridge-eave)*(1-abs(chimney_x)/half)
        box("ChimneyStack",(chimney_x,chimney_y,(roof_at_flue-.15+ridge+.35)/2),
            (.46,.51,ridge+.50-roof_at_flue),"URMAN_Plaster_Shadow")
        box("ChimneyCap",(chimney_x,chimney_y,ridge+.40),(.56,.61,.10),"URMAN_Roof_WetSlate")
    else:
        box("ChimneyStack",(.90,back-1.45,ridge-.05),(.46,.51,1.30),"URMAN_Plaster_Shadow")
        box("ChimneyCap",(.90,back-1.45,ridge+.63),(.56,.61,.10),"URMAN_Roof_WetSlate")
    parent["geometry_pass"] = "v6 pierced wall architecture; street gable and side seni"
    parent["eave_height_m"] = eave
    parent["door_clear_height_m"] = 2.02


def author_hero_house(root: bpy.types.Object) -> bpy.types.Object:
    """A metric hero variation in this kit, not a scale change to the village."""
    ensure_hero_materials()
    hero = variant_empty(HERO_DWELLING_ROOT, root, (34.0, 0.8, 0.0),
                         "hero house shell paired with an 8 by 7 metre clear room", "hero house")
    author_rural_dwelling(hero, width=8.4, depth=7.4, eave=3.05, ridge=4.8, hero_layout=True)
    for child in hero.children:
        child.location.x += HERO_HOUSE_CONTRACT["room_center_xz"][0]
        child["urman_asset_id"] = "urman.act1.village.hero_house_timberplaster"
        child["geometry_pass"] = HERO_HOUSE_CONTRACT["version"]
    hero["component_root"] = HERO_DWELLING_ROOT
    hero["urman_asset_id"] = "urman.act1.village.hero_house_timberplaster"
    hero["geometry_pass"] = HERO_HOUSE_CONTRACT["version"]
    hero["door_clear_height_m"] = 2.25
    hero["hero_room_contract"] = json.dumps(HERO_HOUSE_CONTRACT, sort_keys=True)
    return hero


def validate_hero_house(root: bpy.types.Object) -> None:
    hero = bpy.data.objects[HERO_DWELLING_ROOT]
    if hero.parent is not root or tuple(hero.scale) != (1.0, 1.0, 1.0):
        raise RuntimeError("Hero shell must be an independent, unscaled component")
    meshes = sorted((obj for obj in hero.children if obj.type == "MESH"), key=lambda obj: obj.name)
    if not meshes or any(not obj.name.startswith("HeroHouse_") for obj in meshes):
        raise RuntimeError("Hero shell mesh names overlap the existing dwelling family")
    wall = bpy.data.objects["HeroHouse_Street_Wall_LOD0"]
    points = [wall.matrix_local @ vertex.co for vertex in wall.data.vertices]
    if abs(min(point.x for point in points) + 2.542) > 1e-5 or abs(max(point.x for point in points) - 5.858) > 1e-5:
        raise RuntimeError("Hero shell width/left-corner registration drifted")
    # The preserved old interaction point still lies inside a genuine doorway
    # in the wall, and the taller hole also admits a standing person.
    for height in (.246+.08, 1.1685, .246+2.17):
        origin = wall.matrix_local.inverted() @ Vector((-1.1644, -1.8, height))
        if wall.ray_cast(origin, Vector((0, 1, 0)), distance=1.0)[0]:
            raise RuntimeError(f"Hero wall closes its doorway at height {height}")
    geometry = [(obj.name, [list(round(value, 6) for value in obj.matrix_local @ vertex.co)
                            for vertex in obj.data.vertices]) for obj in meshes]
    fingerprint = hashlib.sha256(json.dumps({"contract": HERO_HOUSE_CONTRACT, "geometry": geometry},
                                            sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    hero["component_geometry_sha256"] = fingerprint
    print(f"hero-house-pass: component={hero.name} meshes={len(meshes)} shell=8.4x7.4 clear=8x7 "
          f"ceiling=2.6 threshold=.246 door=1.3x2.25 fingerprint={fingerprint}")


def author_hero_yard_shed(root: bpy.types.Object) -> bpy.types.Object:
    """One metric storage loft with a real low undercroft and fixed ladder.

    Existing village sheds remain untouched. Blender coordinates below are
    X/lateral, Y/rear and Z/height; the contract above is in Godot coordinates.
    """
    shed = variant_empty(HERO_YARD_SHED_ROOT, root, (44.0, 0.8, 0.0),
                         "small hay storage loft with a low service passage", "hero yard")
    for child in reversed(list(shed.children_recursive)):
        data = child.data if child.type == "MESH" else None
        bpy.data.objects.remove(child, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    wood = "URMAN_Wood_Weathered"
    dark = "URMAN_Wood_Dark"

    def box(name, at, size, finish=wood, bevel=.008):
        return variant_box("HeroYardShed_" + name + "_LOD0", shed, at, size,
                           (finish,), HERO_YARD_SHED_ROOT, name.replace("_", " "),
                           chamfer=bevel, geometry_pass=HERO_YARD_SHED_CONTRACT["version"])

    # One level ground platform and the narrow fixed-ladder apron. Runtime
    # seats the component above the sampled terrain and scribes its plinth.
    box("Foundation", (0, 0, 0), (4.4, 4.4, .08), "URMAN_Stone_Mossy")
    box("LadderApron", (0, -3.15, 0), (.98, 1.90, .08), "URMAN_Stone_Mossy")
    for x in (-2.08, 2.08):
        for y in (-2.08, 2.08):
            suffix = ("L" if x < 0 else "R") + ("Front" if y < 0 else "Back")
            box("SupportPost_" + suffix, (x, y, 1.69), (.18, .18, 3.30), dark)
    # Board top 1.46; joist bottom 1.26; the 1.08m crouch capsule has 14cm
    # vertical clearance above the ground platform, including beneath joists.
    for index in range(11):
        x = -2.0 + index * .4
        box("LoftBoard_%02d" % index, (x, 0, 1.42), (.4, 4.4, .08))
    for index, y in enumerate((-2.0, 0.0, 2.0)):
        box("FloorJoist_%d" % index, (0, y, 1.32), (4.4, .16, .12), dark)
    box("Left_Wall", (-2.16, 0, 2.44), (.08, 4.4, 1.96))
    box("Right_Wall", (2.16, 0, 2.44), (.08, 4.4, 1.96))
    for side in (-1, 1):
        box("Front_WallPier_" + ("L" if side < 0 else "R"),
            (side * 1.35, -2.16, 2.44), (1.70, .08, 1.96))
        box("Rear_WallPier_" + ("L" if side < 0 else "R"),
            (side * 1.42, 2.16, 2.44), (1.56, .08, 1.96))
    box("FrontDoorLintel", (0, -2.16, 3.40), (1.0, .12, .12), dark)
    # The hay vent is a genuine unglazed opening with a timber sill, not a
    # picture on a solid wall. Its height prevents a crouched person exiting.
    box("Rear_WindowSillWall", (0, 2.16, 1.81), (1.28, .08, .70))
    box("Rear_WindowHeadWall", (0, 2.16, 3.21), (1.28, .08, .42))
    box("Rear_WindowSill", (0, 2.15, 2.12), (1.44, .28, .08), dark)
    for side in (-1, 1):
        box("Rear_WindowJamb_" + ("L" if side < 0 else "R"),
            (side * .68, 2.13, 2.58), (.08, .13, 1.02), dark)
    for name, a, b in (("Front", -2.20, -2.12), ("Back", 2.12, 2.20)):
        gable = variant_gable("HeroYardShed_" + name + "Gable_LOD0", shed,
                              4.4, a, b, 3.40, 0, 4.24, HERO_YARD_SHED_ROOT)
        gable.data.materials.clear()
        gable.data.materials.append(material(wood))
        for face in gable.data.polygons:
            face.material_index = 0
    variant_roof("HeroYardShed_Roof_LOD0", shed, 4.4, -2.42, 2.42,
                 3.42, 0, 4.36, .20, HERO_YARD_SHED_ROOT, thickness=.12)

    # Rails are fixed to the loft, not a carry item. Their cross-sections stay
    # outside the capsule's centre corridor; the player climbs in front of
    # the rungs and steps physically over the upper lip.
    start = Vector((0, -3.32, .04))
    end = Vector((0, -2.18, 1.64))
    delta = end - start
    for side in (-1, 1):
        rail = box("LadderRail_" + ("L" if side < 0 else "R"),
                   tuple((start + end) * .5 + Vector((side * .44, 0, 0))),
                   (.085, .085, delta.length), dark)
        rail.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    for index in range(7):
        center = start.lerp(end, .08 + index * .135)
        box("LadderRung_%02d" % index, tuple(center), (.84, .105, .07))
    for x in (-.44, .44):
        box("LadderFixing_" + ("L" if x < 0 else "R"), (x, -2.14, 1.59),
            (.16, .20, .07), "URMAN_Roof_WetSlate")

    for index, (x, y) in enumerate(((-1.50, -.9), (-1.50, .6), (1.50, -.9), (1.50, .6))):
        box("HayBundle_%d" % index, (x, y, 1.77), (.84, 1.05, .62), "URMAN_Wood_CutEnd", .10)
        for stripe in (-.26, .26):
            box("HayBinding_%d_%s" % (index, "A" if stripe < 0 else "B"),
                (x + stripe, y, 2.085), (.045, 1.02, .025), dark)
    # The existing physical QuietRattle board is mounted here by the runtime's
    # single yard-mechanism owner. No duplicate loose-board/metal-clip prop is
    # authored: its movement, sound and persisted quiet state share that owner.
    shed["component_root"] = HERO_YARD_SHED_ROOT
    shed["geometry_pass"] = HERO_YARD_SHED_CONTRACT["version"]
    shed["small_spaces_contract"] = json.dumps(HERO_YARD_SHED_CONTRACT, sort_keys=True)
    return shed


def validate_hero_yard_shed(root: bpy.types.Object) -> None:
    shed = bpy.data.objects[HERO_YARD_SHED_ROOT]
    if shed.parent is not root or tuple(shed.scale) != (1.0, 1.0, 1.0):
        raise RuntimeError("Hero shed must be an independent metric component")
    meshes = [obj for obj in shed.children if obj.type == "MESH"]
    if len(meshes) < 40 or any(not obj.name.startswith("HeroYardShed_") for obj in meshes):
        raise RuntimeError("Incomplete hero shed or a collision with existing mesh names")
    for obj in meshes:
        obj.data.calc_loop_triangles()
        if not obj.data.materials or any(face.area <= 1e-10 for face in obj.data.polygons):
            raise RuntimeError("Invalid hero shed mesh: " + obj.name)
    # Model-space rays through the undercroft at seated head height must be
    # unobstructed in both directions, including beneath all three joists.
    for height in (.12, .58, 1.14):
        for obj in meshes:
            origin = obj.matrix_local.inverted() @ Vector((-1.15, -2.30, height))
            direction = obj.matrix_local.inverted().to_3x3() @ Vector((0, 1, 0))
            if obj.ray_cast(origin, direction.normalized(), distance=4.60)[0]:
                raise RuntimeError(f"Underdeck passage blocked by {obj.name} at {height}m")
    print(f"hero-yard-shed-pass: meshes={len(meshes)} metric=4.4x4.4 floor=.04 loft=1.46 "
          "underdeck_clearance=1.22 fixed_ladder=7_rungs legacy_components_untouched")




def author_well(parent: bpy.types.Object) -> None:
    stone_ring_mesh(parent)
    water_inset_mesh(parent)
    irregular_post(
        "Well_YardLandmark_PostLeft_LOD0",
        parent,
        (-0.67, 0.035, 1.34),
        (0.11, 0.11, 0.86),
        (0.0, math.radians(-1.9), math.radians(-0.7)),
    )
    irregular_post(
        "Well_YardLandmark_PostRight_LOD0",
        parent,
        (0.69, 0.045, 1.33),
        (0.105, 0.115, 0.85),
        (0.0, math.radians(1.5), math.radians(0.6)),
    )
    well_chamfered_box(
        "Well_YardLandmark_Header_LOD0",
        parent,
        (0.01, 0.04, 2.14),
        (1.58, 0.18, 0.17),
        ("URMAN_Wood_Dark", "URMAN_Wood_Weathered"),
        [0, 1, 0, 1, 0, 0, 1, 0, 1, 0],
        chamfer=0.035,
        rotation=(0.0, 0.0, math.radians(0.5)),
        role="uneven well roof support header",
    )
    well_roof_mesh(parent)
    well_chamfered_box(
        "Well_YardLandmark_RoofEaveFront_LOD0",
        parent,
        (0.01, -0.80, 2.09),
        (2.04, 0.14, 0.14),
        ("URMAN_Wood_Weathered", "URMAN_Wood_Dark"),
        chamfer=0.03,
        role="front roof eave beam",
    )
    well_chamfered_box(
        "Well_YardLandmark_RoofEaveBack_LOD0",
        parent,
        (0.01, 0.81, 2.10),
        (2.02, 0.14, 0.14),
        ("URMAN_Wood_Weathered", "URMAN_Wood_Dark"),
        chamfer=0.03,
        role="rear roof eave beam",
    )
    well_chamfered_box(
        "Well_YardLandmark_RoofRidge_LOD0",
        parent,
        (0.025, 0.01, 2.63),
        (0.13, 1.62, 0.12),
        ("URMAN_Wood_Weathered",),
        chamfer=0.025,
        role="short roof ridge cap",
    )
    well_chamfered_box(
        "Well_YardLandmark_RimFront_LOD0",
        parent,
        (0.0, -0.62, 0.55),
        (1.07, 0.12, 0.12),
        ("URMAN_Wood_WetShadow", "URMAN_Wood_Weathered"),
        [0, 1, 0, 1, 0, 0, 1, 0, 1, 0],
        chamfer=0.025,
        role="front timber well rim",
    )
    well_chamfered_box(
        "Well_YardLandmark_RimBack_LOD0",
        parent,
        (0.0, 0.61, 0.55),
        (1.05, 0.12, 0.12),
        ("URMAN_Wood_WetShadow", "URMAN_Wood_Weathered"),
        [0, 1, 0, 1, 0, 0, 1, 0, 1, 0],
        chamfer=0.025,
        role="rear timber well rim",
    )
    cylinder_mesh(
        "Well_YardLandmark_CrankAxle_LOD0",
        parent,
        -0.79,
        0.81,
        (0.0, -0.01, 1.56),
        0.055,
        ("URMAN_Wood_Dark",),
        axis="X",
        role="wooden well axle",
    )
    well_chamfered_box(
        "Well_YardLandmark_CrankHandle_LOD0",
        parent,
        (0.86, -0.01, 1.34),
        (0.12, 0.11, 0.38),
        ("URMAN_Wood_Weathered",),
        chamfer=0.025,
        rotation=(0.0, math.radians(-15.0), 0.0),
        role="short offset well crank handle",
    )
    bucket_mesh(parent)
    bucket_handle_mesh(parent)
    cylinder_mesh(
        "Well_YardLandmark_Rope_LOD0",
        parent,
        0.98,
        1.56,
        (0.22, -0.08, 0.0),
        0.017,
        ("URMAN_Bark_Muted",),
        axis="Z",
        segments=6,
        role="restrained hanging rope cue",
    )


def validate(root: bpy.types.Object, dwelling: bpy.types.Object, well: bpy.types.Object, woodpile: bpy.types.Object) -> None:
    required_components = {
        "DwellingFacade_TimberPlaster",
        "OutbuildingShed_Low",
        "FenceSegment_RoughPicket",
        "Gate_CrookedTimber",
        WOODPILE_ROOT,
        WELL_ROOT,
    }
    actual_components = {child.name for child in root.children}
    variant_components = set(VARIANT_LAYOUT)
    if not required_components.issubset(actual_components):
        raise RuntimeError(f"Canonical component roots changed: expected at least={sorted(required_components)} actual={sorted(actual_components)}")
    if not variant_components.issubset(actual_components):
        raise RuntimeError(f"Variant parcel roots missing: expected={sorted(variant_components)} actual={sorted(actual_components)}")
    unexpected_components = actual_components - required_components - variant_components - set(ANIMAL_ROOTS) - {HERO_DWELLING_ROOT, HERO_YARD_SHED_ROOT}
    if unexpected_components:
        raise RuntimeError(f"Unexpected direct component roots: {sorted(unexpected_components)}")
    if any(abs(value) > 1e-6 for value in root.location) or any(abs(value) > 1e-6 for value in root.rotation_euler) or any(abs(value - 1.0) > 1e-6 for value in root.scale):
        raise RuntimeError("Kit root transform is not identity")
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.data.objects):
        raise RuntimeError("Cameras/lights are not allowed in the authored kit")
    if tuple(round(value, 5) for value in dwelling.location) != (-9.2, 0.8, 0.0):
        raise RuntimeError(f"Dwelling preview-board anchor changed: {tuple(dwelling.location)}")
    if dwelling.get("eave_height_m", 0) < 2.6:
        raise RuntimeError("Dwelling eave is below normal inhabited scale")
    if any("TimberPost" in child.name or "Porch" in child.name for child in dwelling.children):
        raise RuntimeError("Obsolete exposed frame or porch survived the architecture replacement")
    if bpy.data.objects.get("DwellingFacade_Street_Portal0_Leaf_LOD0") is not None:
        raise RuntimeError("Presentation geometry blocks the independently owned hero portal")
    door = bpy.data.objects.get("DwellingFacade_StreetDoorClosed_LOD0")
    if door is None or door.type != "MESH" or door.parent is not dwelling:
        raise RuntimeError("Removable closed street door is missing from the dwelling root")
    if door.get("collision") != "none" or door.get("presentation_only") is not True:
        raise RuntimeError("Removable street door must remain presentation-only")
    expected_added_meshes = {
        "DwellingFacade_Street_Wall_LOD0",
        "DwellingFacade_Rear_Wall_LOD0",
        "DwellingFacade_Left_Wall_LOD0",
        "DwellingFacade_Right_Wall_LOD0",
        "DwellingFacade_SeniEntry_Wall_LOD0",
        "DwellingFacade_SeniRoof_LOD0",
        "DwellingFacade_Front_BoardedGable_LOD0",
        "DwellingFacade_ChimneyStack_LOD0",
        "OutbuildingShed_Foundation_LOD0",
        "OutbuildingShed_Wall_LOD0",
        "OutbuildingShed_Roof_LOD0",
        "OutbuildingShed_GableFront_LOD0",
        "OutbuildingShed_GableBack_LOD0",
        "OutbuildingShed_RearBatten_LOD0",
        "FenceSegment_FoundationStoneLeft_LOD0",
        "FenceSegment_FoundationStoneRight_LOD0",
        "FenceSegment_RailMiddle_Crooked_LOD0",
        "Gate_PostCapLeft_LOD0",
        "Gate_PostCapRight_LOD0",
        "Gate_BackBrace_LOD0",
        "Gate_ThresholdStoneRear_LOD0",
        "Gate_HingeRight_LOD0",
        "Gate_LatchPlate_LOD0",
        "Gate_PostFootRight_LOD0",
    }
    for name in expected_added_meshes:
        child = bpy.data.objects.get(name)
        if child is None or child.type != "MESH":
            raise RuntimeError(f"Missing authored exterior mesh: {name}")
        if child.get("collision") != "none" or child.get("presentation_only") is not True:
            raise RuntimeError(f"Presentation-only boundary changed: {name}")
        child.data.calc_loop_triangles()
        if not child.data.materials or len(child.data.loop_triangles) == 0:
            raise RuntimeError(f"Invalid authored exterior mesh: {name}")
    if well.parent is not root or tuple(round(value, 5) for value in well.location) != (18.2, -0.35, 0.0):
        raise RuntimeError(f"Well preview-board anchor changed: {tuple(well.location)}")
    expected_well_meshes = {
        "Well_YardLandmark_StoneRing_LOD0",
        "Well_YardLandmark_WaterInset_LOD0",
        "Well_YardLandmark_Roof_LOD0",
        "Well_YardLandmark_PostLeft_LOD0",
        "Well_YardLandmark_PostRight_LOD0",
        "Well_YardLandmark_Bucket_LOD0",
        "Well_YardLandmark_BucketHandle_LOD0",
        "Well_YardLandmark_CrankAxle_LOD0",
        "Well_YardLandmark_CrankHandle_LOD0",
        "Well_YardLandmark_Header_LOD0",
    }
    actual_well_meshes = {child.name for child in well.children if child.type == "MESH"}
    if not expected_well_meshes.issubset(actual_well_meshes):
        raise RuntimeError(f"Missing well meshes: {sorted(expected_well_meshes - actual_well_meshes)}")
    well_min_z = float("inf")
    well_max_z = float("-inf")
    for child in dwelling.children:
        if child.type != "MESH":
            continue
        child.data.calc_loop_triangles()
        if not child.data.materials:
            raise RuntimeError(f"Missing materials on {child.name}")
        if len(child.data.loop_triangles) == 0:
            raise RuntimeError(f"Degenerate key mesh: {child.name}")
    for child in well.children:
        if child.type != "MESH":
            continue
        child.data.calc_loop_triangles()
        if not child.data.materials:
            raise RuntimeError(f"Missing materials on {child.name}")
        if len(child.data.loop_triangles) == 0:
            raise RuntimeError(f"Degenerate well mesh: {child.name}")
        for vertex in child.data.vertices:
            z = (child.matrix_world @ vertex.co).z
            well_min_z = min(well_min_z, z)
            well_max_z = max(well_max_z, z)
    if well_min_z > 0.12 or well_max_z < 2.45:
        raise RuntimeError(f"Well landmark is not grounded/readable: z={well_min_z:.3f}..{well_max_z:.3f}")
    if woodpile.parent is not root or woodpile.type != "EMPTY":
        raise RuntimeError("Woodpile root hierarchy changed")
    if woodpile.get("local_pivot") != "ground anchor at local X=0, Y=0, Z=0":
        raise RuntimeError("Woodpile local pivot contract changed")
    woodpile_meshes = {child.name: child for child in woodpile.children if child.type == "MESH"}
    expected_woodpile_meshes = set(WOODPILE_LOG_NAMES + WOODPILE_SUPPORT_NAMES)
    if set(woodpile_meshes) != expected_woodpile_meshes:
        raise RuntimeError(f"Woodpile mesh contract changed: expected={sorted(expected_woodpile_meshes)} actual={sorted(woodpile_meshes)}")
    row_bounds: dict[int, list[float]] = {}
    log_sections: dict[str, tuple[float, float, list[tuple[float, float]]]] = {}
    woodpile_min_z = float("inf")
    woodpile_max_z = float("-inf")
    for name in WOODPILE_LOG_NAMES:
        log = woodpile_meshes[name]
        if log.parent is not woodpile or log.get("woodpile_axis") != "X" or log.get("woodpile_segments") != 8:
            raise RuntimeError(f"Woodpile log parent/axis/segments changed: {name}")
        if any(abs(value) > 1e-6 for value in log.rotation_euler):
            raise RuntimeError(f"Woodpile log is no longer horizontal: {name}")
        length = float(log.get("woodpile_length_m", 0.0))
        radius = float(log.get("woodpile_radius_m", 0.0))
        if not 1.60 <= length <= 1.85 or not 0.16 <= radius <= 0.18:
            raise RuntimeError(f"Woodpile log bounds changed: {name} length={length} radius={radius}")
        if abs(log.dimensions.x - length) > 1e-5 or abs(log.dimensions.y - radius * 2.0) > 1e-5 or abs(log.dimensions.z - radius * 2.0) > 1e-5:
            raise RuntimeError(f"Woodpile log dimensions changed: {name} dimensions={tuple(log.dimensions)}")
        log.data.calc_loop_triangles()
        if len(log.data.loop_triangles) == 0 or len(log.data.materials) < 2:
            raise RuntimeError(f"Invalid woodpile log mesh/materials: {name}")
        if any(log.data.materials[face.material_index].name != "URMAN_Wood_CutEnd"
               for face in log.data.polygons[:2]):
            raise RuntimeError(f"Woodpile cut ends lost their material: {name}")
        row = int(log["woodpile_row"])
        local_z = [log.location.z + vertex.co.z for vertex in log.data.vertices]
        cross_section = [(log.location.y + vertex.co.y, log.location.z + vertex.co.z) for vertex in log.data.vertices[:8]]
        center_y = sum(point[0] for point in cross_section) / len(cross_section)
        center_z = sum(point[1] for point in cross_section) / len(cross_section)
        log_sections[name] = (center_y, center_z, cross_section)
        bottom, top = min(local_z), max(local_z)
        row_bounds.setdefault(row, [float("inf"), float("-inf")])
        row_bounds[row][0] = min(row_bounds[row][0], bottom)
        row_bounds[row][1] = max(row_bounds[row][1], top)
        woodpile_min_z = min(woodpile_min_z, bottom)
        woodpile_max_z = max(woodpile_max_z, top)
    support_bounds: list[tuple[float, float]] = []
    for name in WOODPILE_SUPPORT_NAMES:
        support = woodpile_meshes[name]
        if support.parent is not woodpile or any(abs(value) > 1e-6 for value in support.rotation_euler):
            raise RuntimeError(f"Woodpile support transform changed: {name}")
        local_z = [vertex.co.z + support.location.z for vertex in support.data.vertices]
        support_min_z, support_max_z = min(local_z), max(local_z)
        if abs(support_min_z) > 1e-5 or abs(support_max_z - 0.16) > 1e-5:
            raise RuntimeError(f"Woodpile support is not grounded: {name} z={support_min_z:.3f}..{support_max_z:.3f}")
        support_bounds.append((support_min_z, support_max_z))
        woodpile_min_z = min(woodpile_min_z, support_min_z)
        woodpile_max_z = max(woodpile_max_z, support_max_z)
    if set(row_bounds) != {0, 1, 2} or any(len([name for name in WOODPILE_LOG_NAMES if int(woodpile_meshes[name]["woodpile_row"]) == row]) != count for row, count in ((0, 3), (1, 2), (2, 1))):
        raise RuntimeError(f"Woodpile row composition changed: {row_bounds}")
    def projection_overlap(first_name: str, second_name: str) -> float:
        first_y, first_z, first_polygon = log_sections[first_name]
        second_y, second_z, second_polygon = log_sections[second_name]
        dy, dz = second_y - first_y, second_z - first_z
        distance = math.hypot(dy, dz)
        if distance <= 1e-6:
            raise RuntimeError(f"Coincident woodpile log centers: {first_name}, {second_name}")
        direction = (dy / distance, dz / distance)
        first_projection = [point[0] * direction[0] + point[1] * direction[1] for point in first_polygon]
        second_projection = [point[0] * direction[0] + point[1] * direction[1] for point in second_polygon]
        overlap = min(max(first_projection), max(second_projection)) - max(min(first_projection), min(second_projection))
        if overlap < -1e-5 or overlap > 0.020:
            raise RuntimeError(f"Woodpile geometric contact changed: {first_name}/{second_name} overlap={overlap:.6f}")
        return overlap

    contact_pairs = (
        (WOODPILE_LOG_NAMES[0], WOODPILE_LOG_NAMES[1]),
        (WOODPILE_LOG_NAMES[1], WOODPILE_LOG_NAMES[2]),
        (WOODPILE_LOG_NAMES[0], WOODPILE_LOG_NAMES[3]),
        (WOODPILE_LOG_NAMES[1], WOODPILE_LOG_NAMES[3]),
        (WOODPILE_LOG_NAMES[1], WOODPILE_LOG_NAMES[4]),
        (WOODPILE_LOG_NAMES[2], WOODPILE_LOG_NAMES[4]),
        (WOODPILE_LOG_NAMES[3], WOODPILE_LOG_NAMES[5]),
        (WOODPILE_LOG_NAMES[4], WOODPILE_LOG_NAMES[5]),
    )
    contact_overlaps = [projection_overlap(first, second) for first, second in contact_pairs]
    if woodpile_min_z < -1e-5 or abs(woodpile_max_z - 0.98745166) > 1e-5:
        raise RuntimeError(f"Woodpile overall bounds changed: z={woodpile_min_z:.3f}..{woodpile_max_z:.3f}")
    woodpile_triangles = sum(len(mesh.data.loop_triangles) for mesh in woodpile_meshes.values())
    print(
        "woodpile-pass: "
        f"meshes={len(woodpile_meshes)} triangles={woodpile_triangles} rows=3/2/1 "
        f"z={woodpile_min_z:.3f}..{woodpile_max_z:.3f} support_z=0.000..0.160 "
        f"contact_projection={','.join(f'{value:.6f}' for value in contact_overlaps)} "
        f"root={woodpile.name} local_pivot={woodpile.get('local_pivot')}"
    )
    variant_summaries: list[str] = []
    for variant_name, expected_location in VARIANT_LAYOUT.items():
        variant = bpy.data.objects[variant_name]
        if variant.parent is not root or tuple(round(value, 5) for value in variant.location) != expected_location:
            raise RuntimeError(f"Variant preview origin changed: {variant_name}={tuple(variant.location)}")
        variant_meshes = [obj for obj in variant.children_recursive if obj.type == "MESH"]
        house = bpy.data.objects[f"{variant_name}_Dwelling"]
        if house.get("door_clear_height_m", 0) < 2.0 or house.get("eave_height_m", 0) < 2.6:
            raise RuntimeError(f"Variant house has sub-human architecture scale: {variant_name}")
        group_bounds = {}
        for group in variant.children:
            points = [variant.matrix_world.inverted() @ obj.matrix_world @ vertex.co
                      for obj in group.children_recursive if obj.type == "MESH"
                      for vertex in obj.data.vertices]
            group_bounds[group.name] = ([min(p[i] for p in points) for i in range(3)],
                                       [max(p[i] for p in points) for i in range(3)])
        house_min, house_max = group_bounds[house.name]
        for suffix in ("Outbuilding", "Yard"):
            other_min, other_max = group_bounds[f"{variant_name}_{suffix}"]
            if all(house_min[i] < other_max[i] and other_min[i] < house_max[i] for i in range(3)):
                raise RuntimeError(f"Variant {suffix} overlaps inhabited dwelling bounds: {variant_name}")
        print(f"parcel-separation-pass: {variant_name} {group_bounds}")
        if len(variant_meshes) < 20:
            raise RuntimeError(f"Variant parcel is too sparse: {variant_name} meshes={len(variant_meshes)}")
        expected_composition_meshes = (
            len(PARCEL_COMPOSITION[variant_name]["moss"])
            + len(PARCEL_COMPOSITION[variant_name]["shrubs"]) * 3
            + len(PARCEL_COMPOSITION[variant_name]["branches"])
            + len(PARCEL_COMPOSITION[variant_name]["sedge"]) * 3
        )
        composition_meshes = [obj for obj in variant_meshes if obj.get("geometry_pass") == VARIANT_COMPOSITION_PASS]
        if len(composition_meshes) != expected_composition_meshes:
            raise RuntimeError(
                f"Variant contact composition changed: {variant_name} "
                f"expected={expected_composition_meshes} actual={len(composition_meshes)}"
            )
        bounds = [float("inf"), float("inf"), float("inf"), float("-inf"), float("-inf"), float("-inf")]
        for obj in variant_meshes:
            if obj.get("collision") != "none" or obj.get("presentation_only") is not True:
                raise RuntimeError(f"Variant presentation-only boundary changed: {obj.name}")
            obj.data.calc_loop_triangles()
            if not obj.data.materials or not obj.data.loop_triangles:
                raise RuntimeError(f"Invalid variant mesh: {obj.name}")
            for vertex in obj.data.vertices:
                world = obj.matrix_world @ vertex.co
                bounds[0] = min(bounds[0], world.x)
                bounds[1] = min(bounds[1], world.y)
                bounds[2] = min(bounds[2], world.z)
                bounds[3] = max(bounds[3], world.x)
                bounds[4] = max(bounds[4], world.y)
                bounds[5] = max(bounds[5], world.z)
        if bounds[2] < -0.20 or bounds[5] - bounds[2] < 1.20 or any(abs(bounds[index + 3] - bounds[index]) < 0.10 for index in range(3)):
            raise RuntimeError(f"Variant parcel has non-grounded/degenerate bounds: {variant_name}={tuple(round(value, 3) for value in bounds)}")
        variant_summaries.append(
            f"{variant_name}:meshes={len(variant_meshes)}:composition={len(composition_meshes)}:bounds="
            + ",".join(f"{value:.2f}" for value in bounds)
        )
    # Blender creates an unexported Render Result datablock in background mode;
    # authored image datablocks are still forbidden.
    if any(image.type != "RENDER_RESULT" for image in bpy.data.images):
        raise RuntimeError("Image textures are not allowed in the authored kit")
    body = bpy.data.objects["DwellingFacade_Street_Wall_LOD0"]
    min_z = min(vertex.co.z for vertex in body.data.vertices)
    max_z = max(vertex.co.z for vertex in body.data.vertices)
    if min_z > 0.40 or max_z < 2.6:
        raise RuntimeError(f"Dwelling wall is not grounded: z={min_z:.3f}..{max_z:.3f}")
    kit_meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    for obj in kit_meshes:
        obj.data.calc_loop_triangles()
        if not obj.data.materials or not obj.data.loop_triangles:
            raise RuntimeError(f"Invalid kit mesh: {obj.name}")
        if any(polygon.area <= 1e-10 for polygon in obj.data.polygons):
            raise RuntimeError(f"Degenerate kit polygon: {obj.name}")
    kit_triangles = sum(len(obj.data.loop_triangles) for obj in kit_meshes)
    kit_materials = {
        material.name
        for obj in kit_meshes
        for material in obj.data.materials
        if material is not None
    }
    if len(kit_meshes) <= 0 or kit_triangles <= 0:
        raise RuntimeError("Authored kit has no renderable geometry")
    validate_animals(root)
    print(
        "kit-pass: "
        f"direct_roots={len(root.children)} meshes={len(kit_meshes)} triangles={kit_triangles} "
        f"materials={len(kit_materials)} degenerate_polygons=0 cameras=0 lights=0"
    )
    print(
        "dwelling-pass: components="
        f"{len(required_components)} meshes={sum(obj.type == 'MESH' for obj in bpy.data.objects)} "
        f"dwelling_meshes={sum(obj.type == 'MESH' for obj in dwelling.children)} "
        f"wall_z={min_z:.3f}..{max_z:.3f} cameras=0 lights=0"
    )
    print(
        "well-pass: "
        f"well_meshes={sum(obj.type == 'MESH' for obj in well.children)} "
        f"well_tris={sum(len(obj.data.loop_triangles) for obj in well.children if obj.type == 'MESH')} "
        f"well_z={well_min_z:.3f}..{well_max_z:.3f} "
        f"materials={len({material.name for obj in well.children if obj.type == 'MESH' for material in obj.data.materials if material})} "
        "cameras=0 lights=0 images=0"
    )
    print("variant-pass: " + " | ".join(variant_summaries))


def save_kit(blend_path: Path, glb_path: Path) -> None:
    save_versions = bpy.context.preferences.filepaths.save_version
    bpy.context.preferences.filepaths.save_version = 0
    try:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    finally:
        bpy.context.preferences.filepaths.save_version = save_versions
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=False, export_apply=True)
    print(f"village-exterior-pass: saved {blend_path}")
    print(f"village-exterior-pass: exported {glb_path}")


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_village_exterior_kit.blend"
    glb_path = root_path / "game/assets/models/act1" / GLB_NAME
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(KIT_ROOT)
    dwelling = bpy.data.objects.get(DWELLING_ROOT)
    well = bpy.data.objects.get(WELL_ROOT)
    woodpile = bpy.data.objects.get(WOODPILE_ROOT)
    if root is None or dwelling is None or well is None or woodpile is None or dwelling.parent is not root or well.parent is not root or woodpile.parent is not root:
        raise RuntimeError("Baseline kit root/component hierarchy is incomplete")

    if args.component_only == "hero-yard-shed":
        # Do not reauthor the previous 1,016 exported nodes. This mode is the
        # bounded integration entry point while the main checkout is in use.
        author_hero_yard_shed(root)
        bpy.context.view_layer.update()
        validate_hero_yard_shed(root)
        save_kit(blend_path, glb_path)
        return

    if args.component_only == "hero-house":
        # Same bounded integration entry point for the hero dwelling: only
        # Babai's house is reauthored, so no other kit component can move.
        author_hero_house(root)
        bpy.context.view_layer.update()
        validate_hero_house(root)
        save_kit(blend_path, glb_path)
        return

    clear_variant_roots(root)
    author_rural_dwelling(dwelling)
    author_hero_house(root)
    author_hero_yard_shed(root)
    author_shed_volume(bpy.data.objects["OutbuildingShed_Low"])
    author_fence_variation(bpy.data.objects["FenceSegment_RoughPicket"])
    author_gate_variation(bpy.data.objects["Gate_CrookedTimber"])
    author_gate_joinery(bpy.data.objects["Gate_CrookedTimber"])
    author_well(well)
    cut_end = bpy.data.materials.get("URMAN_Wood_CutEnd") or bpy.data.materials.new("URMAN_Wood_CutEnd")
    cut_end.diffuse_color = (0.70, 0.60, 0.44, 1.0)
    author_woodpile(woodpile)
    author_variant_parcels(root)
    author_ambient_animals(root)
    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_village_exterior_kit.py"
    scene["asset_status"] = "v6 inhabited rural architecture candidate with pierced walls, vertical windows, boarded gables and side seni; requires in-game art review"
    scene["dwelling_geometry_pass"] = "v6 full-depth pierced wall shell; preserved hero doorway opening; no exposed structural lattice"
    scene["yard_geometry_pass"] = "v3 shed full volume, fence foundation/rhythm and gate threshold variations"
    scene["variant_geometry_pass"] = "v3 full-depth adult-scale dwellings with enclosed seni; preserved outbuilding and yard components"
    scene["variant_composition_pass"] = VARIANT_COMPOSITION_PASS
    scene["well_geometry_pass"] = "v1 staggered masonry/timber ring, leaning posts, pitched eave roof and restrained bucket/rope cue"
    scene["woodpile_geometry_pass"] = WOODPILE_GEOMETRY_PASS
    scene["texture_policy"] = "geometry and existing basic materials only; no texture files"
    bpy.context.view_layer.update()
    validate_hero_house(root)
    validate_hero_yard_shed(root)
    validate(root, dwelling, well, woodpile)

    save_kit(blend_path, glb_path)


if __name__ == "__main__":
    main()
