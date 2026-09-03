"""Deterministically author the Act I village dwelling and well passes.

The six-component kit already exists as a Blender source asset. This script
keeps the other five components and materials intact, preserves the accepted
dwelling child geometry, and rebuilds the yard well as a grounded, faceted
landmark with restrained asymmetry.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/act1/urman_village_exterior_kit.py -- --root <repo>
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy


KIT_ROOT = "URMAN_VillageExteriorKit"
DWELLING_ROOT = "DwellingFacade_TimberPlaster"
WELL_ROOT = "Well_YardLandmark"
GLB_NAME = "urman_village_exterior_kit.glb"


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


def beam_between(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    depth: float,
    materials: tuple[str, ...],
    role: str = "dwelling timber brace",
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


def body_mesh(parent: bpy.types.Object) -> None:
    # Eight-sided, slightly tapered footprint: the front bows by a few
    # centimetres and the back is intentionally not a mirrored rectangle.
    bottom = [
        (-3.62, -1.18, 0.34),
        (-1.65, -1.27, 0.34),
        (0.95, -1.30, 0.34),
        (3.56, -1.17, 0.34),
        (3.63, 1.10, 0.34),
        (1.10, 1.23, 0.34),
        (-1.75, 1.18, 0.34),
        (-3.68, 1.06, 0.34),
    ]
    top = [
        (-3.50, -1.14, 3.06),
        (-1.60, -1.23, 3.06),
        (0.92, -1.26, 3.06),
        (3.43, -1.13, 3.06),
        (3.51, 1.07, 3.06),
        (1.05, 1.17, 3.06),
        (-1.70, 1.13, 3.06),
        (-3.56, 1.02, 3.06),
    ]
    vertices = bottom + top
    faces: list[tuple[int, ...]] = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((index, (index + 1) % 8, (index + 1) % 8 + 8, index + 8) for index in range(8))
    # Front courses stay warm plaster; the side/back faces are darker and
    # carry the mass without reading as a clean rectangular slab.
    material_indices = [1, 1, 0, 1, 1, 1, 1, 1, 1, 1]
    mesh_object(
        "DwellingFacade_Wall_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Plaster_Ochre", "URMAN_Plaster_Shadow"),
        material_indices,
        role="grounded uneven plaster wall volume",
    )


def gable_mesh(parent: bpy.types.Object, name: str, front_y: float, back_y: float, ridge_x: float, ridge_z: float) -> None:
    profile = [(-3.52, 3.00), (3.45, 3.00), (ridge_x, ridge_z)]
    vertices = [(x, front_y, z) for x, z in profile] + [(x, back_y, z) for x, z in profile]
    faces = [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
    mesh_object(
        name,
        parent,
        vertices,
        faces,
        ("URMAN_Plaster_Ochre", "URMAN_Wood_Weathered"),
        [0, 0, 1, 1, 1],
        role="asymmetrical plaster gable infill",
    )


def roof_mesh(parent: bpy.types.Object) -> None:
    # Four faceted slopes, offset ridge, and a broad front/back overhang give
    # the repeated module a readable silhouette from both street directions.
    front = [
        (-4.18, -1.72, 3.00),
        (-3.76, -1.72, 3.18),
        (0.30, -1.72, 4.62),
        (3.70, -1.72, 3.17),
        (4.18, -1.72, 3.00),
    ]
    back = [
        (-4.10, 1.66, 3.02),
        (-3.70, 1.66, 3.17),
        (0.36, 1.66, 4.56),
        (3.66, 1.66, 3.18),
        (4.10, 1.66, 3.02),
    ]
    vertices = front + back
    faces = [
        (0, 1, 6, 5),
        (1, 2, 7, 6),
        (2, 3, 8, 7),
        (3, 4, 9, 8),
        (0, 5, 9, 4),
    ]
    mesh_object(
        "DwellingFacade_Roof_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Roof_WetSlate", "URMAN_Roof_MossTone"),
        [0, 1, 0, 1, 1],
        role="faceted overhanging asymmetrical gable roof",
    )


def porch_roof_mesh(parent: bpy.types.Object) -> None:
    x0, x1 = -2.88, -0.02
    y_back, y_front = -1.34, -2.82
    top_back, top_front = 2.62, 2.27
    thickness = 0.14
    vertices = [
        (x0, y_back, top_back),
        (x1, y_back, top_back),
        (x1, y_front, top_front),
        (x0, y_front, top_front),
        (x0, y_back, top_back - thickness),
        (x1, y_back, top_back - thickness),
        (x1, y_front, top_front - thickness),
        (x0, y_front, top_front - thickness),
    ]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mesh_object(
        "DwellingFacade_PorchRoof_LOD0",
        parent,
        vertices,
        faces,
        ("URMAN_Roof_WetSlate", "URMAN_Roof_MossTone"),
        [0, 1, 1, 0, 1, 0],
        role="deeper lean-to porch roof",
    )


def door_and_windows(parent: bpy.types.Object) -> None:
    chamfered_box(
        "DwellingFacade_DoorRecess_LOD0",
        parent,
        (-1.42, -1.34, 1.39),
        (1.30, 0.16, 2.32),
        ("URMAN_Wood_WetShadow",),
        chamfer=0.07,
        role="recessed door surround",
    )
    chamfered_box(
        "DwellingFacade_DoorPanel_LOD0",
        parent,
        (-1.42, -1.47, 1.38),
        (0.96, 0.10, 2.00),
        ("URMAN_Wood_Weathered",),
        chamfer=0.045,
        role="recessed plank door panel",
    )
    for index, x in enumerate((-1.73, -1.42, -1.11)):
        chamfered_box(
            f"DwellingFacade_DoorPlank_{index:02d}_LOD0",
            parent,
            (x, -1.53, 1.38),
            (0.055, 0.055, 2.00),
            ("URMAN_Wood_Dark",),
            chamfer=0.014,
            role="door plank seam",
        )
    chamfered_box(
        "DwellingFacade_DoorHandle_LOD0",
        parent,
        (-1.04, -1.57, 1.36),
        (0.11, 0.07, 0.13),
        ("URMAN_Metal_Dulled",),
        chamfer=0.025,
        role="door handle",
    )

    chamfered_box(
        "DwellingFacade_WindowWarmInset_LOD0",
        parent,
        (1.48, -1.39, 1.60),
        (1.18, 0.08, 0.92),
        ("URMAN_Window_WarmInset",),
        chamfer=0.035,
        role="warm window inset",
    )
    chamfered_box(
        "DwellingFacade_WindowCoolInset_LOD0",
        parent,
        (2.70, -1.38, 1.65),
        (0.62, 0.08, 0.66),
        ("URMAN_Window_DimGlass",),
        chamfer=0.03,
        role="dim cool window inset",
    )

    frame_specs = [
        ("DwellingFacade_WindowFrameBottom_LOD0", (1.48, -1.46, 1.08), (1.45, 0.11, 0.11), "URMAN_Wood_Dark"),
        ("DwellingFacade_WindowFrameTop_LOD0", (1.48, -1.46, 2.12), (1.45, 0.11, 0.11), "URMAN_Wood_Dark"),
        ("DwellingFacade_WindowFrameLeft_LOD0", (0.78, -1.44, 1.60), (0.11, 0.11, 1.13), "URMAN_Wood_Dark"),
        ("DwellingFacade_WindowFrameRight_LOD0", (2.18, -1.44, 1.60), (0.11, 0.11, 1.13), "URMAN_Wood_Dark"),
        ("DwellingFacade_WindowFrameBottom_LOD0.001", (2.70, -1.45, 1.28), (0.86, 0.11, 0.10), "URMAN_Wood_Weathered"),
        ("DwellingFacade_WindowFrameTop_LOD0.001", (2.70, -1.45, 2.02), (0.86, 0.11, 0.10), "URMAN_Wood_Weathered"),
        ("DwellingFacade_WindowFrameLeft_LOD0.001", (2.25, -1.44, 1.65), (0.10, 0.11, 0.84), "URMAN_Wood_Weathered"),
        ("DwellingFacade_WindowFrameRight_LOD0.001", (3.15, -1.44, 1.65), (0.10, 0.11, 0.84), "URMAN_Wood_Weathered"),
    ]
    for name, center, size, surface in frame_specs:
        chamfered_box(name, parent, center, size, (surface,), chamfer=0.025, role="window timber frame")
    chamfered_box(
        "DwellingFacade_WindowMullionHorizontal_LOD0",
        parent,
        (1.48, -1.50, 1.60),
        (1.20, 0.06, 0.06),
        ("URMAN_Wood_Dark",),
        chamfer=0.014,
        role="warm window mullion",
    )
    chamfered_box(
        "DwellingFacade_WindowMullionVertical_LOD0",
        parent,
        (1.48, -1.50, 1.60),
        (0.06, 0.06, 0.92),
        ("URMAN_Wood_Dark",),
        chamfer=0.014,
        role="warm window mullion",
    )
    chamfered_box(
        "DwellingFacade_WindowMullionHorizontal_LOD0.001",
        parent,
        (2.70, -1.50, 1.65),
        (0.66, 0.06, 0.055),
        ("URMAN_Wood_Weathered",),
        chamfer=0.014,
        role="cool window mullion",
    )
    chamfered_box(
        "DwellingFacade_WindowMullionVertical_LOD0.001",
        parent,
        (2.70, -1.50, 1.65),
        (0.055, 0.06, 0.66),
        ("URMAN_Wood_Weathered",),
        chamfer=0.014,
        role="cool window mullion",
    )
    chamfered_box(
        "DwellingFacade_WindowSill_LOD0",
        parent,
        (1.48, -1.51, 1.04),
        (1.52, 0.18, 0.10),
        ("URMAN_Wood_Dark",),
        chamfer=0.025,
        role="projecting window sill",
    )


def author_dwelling(parent: bpy.types.Object) -> None:
    body_mesh(parent)
    roof_mesh(parent)
    gable_mesh(parent, "DwellingFacade_GableFront_LOD0", -1.34, -1.22, 0.30, 4.56)
    gable_mesh(parent, "DwellingFacade_GableBack_LOD0", 1.22, 1.34, 0.36, 4.52)
    porch_roof_mesh(parent)

    chamfered_box(
        "DwellingFacade_EaveFront_LOD0",
        parent,
        (0.12, -1.76, 3.06),
        (8.45, 0.18, 0.18),
        ("URMAN_Wood_Dark",),
        chamfer=0.035,
        role="front roof eave fascia",
    )
    chamfered_box(
        "DwellingFacade_TimberBase_LOD0",
        parent,
        (-0.02, -1.30, 0.47),
        (7.12, 0.22, 0.28),
        ("URMAN_Wood_Dark",),
        chamfer=0.035,
        role="low timber foundation beam",
    )
    chamfered_box(
        "DwellingFacade_TimberHeader_LOD0",
        parent,
        (0.00, -1.29, 2.90),
        (7.06, 0.20, 0.22),
        ("URMAN_Wood_Weathered",),
        chamfer=0.035,
        role="front timber header",
    )
    chamfered_box(
        "DwellingFacade_TimberPostLeft_LOD0",
        parent,
        (-3.46, -1.28, 1.68),
        (0.24, 0.22, 2.62),
        ("URMAN_Wood_Dark",),
        chamfer=0.04,
        rotation=(0.0, 0.0, math.radians(-0.8)),
        role="left front timber post",
    )
    chamfered_box(
        "DwellingFacade_TimberPostRight_LOD0",
        parent,
        (3.47, -1.26, 1.65),
        (0.24, 0.22, 2.57),
        ("URMAN_Wood_Dark",),
        chamfer=0.04,
        rotation=(0.0, 0.0, math.radians(0.7)),
        role="right front timber post",
    )

    beam_between(
        "DwellingFacade_TimberBraceLeft_LOD0",
        parent,
        (-3.08, -1.41, 0.58),
        (-2.18, -1.41, 2.78),
        0.16,
        0.16,
        ("URMAN_Wood_Weathered",),
    )
    beam_between(
        "DwellingFacade_TimberBraceCenter_LOD0",
        parent,
        (0.62, -1.42, 0.56),
        (0.92, -1.42, 2.76),
        0.14,
        0.15,
        ("URMAN_Wood_Weathered",),
    )
    beam_between(
        "DwellingFacade_TimberBraceRight_LOD0",
        parent,
        (3.05, -1.40, 0.58),
        (2.18, -1.40, 2.76),
        0.16,
        0.16,
        ("URMAN_Wood_Weathered",),
    )

    for index, z in enumerate((0.92, 1.48, 2.24)):
        chamfered_box(
            f"DwellingFacade_PlasterCourse_{index:02d}_LOD0",
            parent,
            (-0.02, -1.325, z),
            (7.08, 0.05, 0.055),
            ("URMAN_Plaster_Line",),
            chamfer=0.012,
            role="shallow plaster course",
        )

    chamfered_box(
        "DwellingFacade_PorchDeck_LOD0",
        parent,
        (-1.45, -1.98, 0.40),
        (2.82, 1.42, 0.22),
        ("URMAN_Wood_Weathered", "URMAN_Wood_Dark"),
        [0, 1] + [0] * 8,
        chamfer=0.08,
        role="deep covered porch deck",
    )
    chamfered_box(
        "DwellingFacade_PorchStep_LOD0",
        parent,
        (-1.45, -2.70, 0.16),
        (2.66, 0.48, 0.22),
        ("URMAN_Wood_WetShadow",),
        chamfer=0.06,
        role="porch approach step",
    )
    chamfered_box(
        "DwellingFacade_PorchHeader_LOD0",
        parent,
        (-1.45, -2.22, 2.52),
        (2.82, 0.20, 0.18),
        ("URMAN_Wood_Weathered",),
        chamfer=0.035,
        role="porch header beam",
    )
    chamfered_box(
        "DwellingFacade_PorchPostLeft_LOD0",
        parent,
        (-2.68, -2.22, 1.40),
        (0.20, 0.20, 2.16),
        ("URMAN_Wood_Dark",),
        chamfer=0.035,
        rotation=(0.0, 0.0, math.radians(-1.4)),
        role="left porch post",
    )
    chamfered_box(
        "DwellingFacade_PorchPostRight_LOD0",
        parent,
        (-0.22, -2.20, 1.39),
        (0.20, 0.20, 2.13),
        ("URMAN_Wood_Dark",),
        chamfer=0.035,
        rotation=(0.0, 0.0, math.radians(1.1)),
        role="right porch post",
    )

    beam_between(
        "DwellingFacade_GableRakeLeft_LOD0",
        parent,
        (-3.58, -1.76, 3.05),
        (0.30, -1.76, 4.60),
        0.11,
        0.14,
        ("URMAN_Wood_Weathered",),
        role="left gable rake trim",
    )
    beam_between(
        "DwellingFacade_GableRakeRight_LOD0",
        parent,
        (0.30, -1.76, 4.60),
        (3.58, -1.76, 3.05),
        0.11,
        0.14,
        ("URMAN_Wood_Weathered",),
        role="right gable rake trim",
    )
    door_and_windows(parent)


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


def validate(root: bpy.types.Object, dwelling: bpy.types.Object, well: bpy.types.Object) -> None:
    expected_components = {
        "DwellingFacade_TimberPlaster",
        "OutbuildingShed_Low",
        "FenceSegment_RoughPicket",
        "Gate_CrookedTimber",
        "Woodpile_StackedLogs",
        WELL_ROOT,
    }
    actual_components = {child.name for child in root.children}
    if actual_components != expected_components:
        raise RuntimeError(f"Component roots changed: expected={sorted(expected_components)} actual={sorted(actual_components)}")
    if any(abs(value) > 1e-6 for value in root.location) or any(abs(value) > 1e-6 for value in root.rotation_euler) or any(abs(value - 1.0) > 1e-6 for value in root.scale):
        raise RuntimeError("Kit root transform is not identity")
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.data.objects):
        raise RuntimeError("Cameras/lights are not allowed in the authored kit")
    if tuple(round(value, 5) for value in dwelling.location) != (-9.2, 0.8, 0.0):
        raise RuntimeError(f"Dwelling preview-board anchor changed: {tuple(dwelling.location)}")
    if len([child for child in dwelling.children if child.type == "MESH"]) != 44:
        raise RuntimeError("Accepted dwelling mesh count changed")
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
    # Blender creates an unexported Render Result datablock in background mode;
    # authored image datablocks are still forbidden.
    if any(image.type != "RENDER_RESULT" for image in bpy.data.images):
        raise RuntimeError("Image textures are not allowed in the authored kit")
    body = bpy.data.objects["DwellingFacade_Wall_LOD0"]
    min_z = min(vertex.co.z for vertex in body.data.vertices)
    max_z = max(vertex.co.z for vertex in body.data.vertices)
    if min_z > 0.40 or max_z < 3.0:
        raise RuntimeError(f"Dwelling wall is not grounded: z={min_z:.3f}..{max_z:.3f}")
    print(
        "dwelling-pass: components="
        f"{len(expected_components)} meshes={sum(obj.type == 'MESH' for obj in bpy.data.objects)} "
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


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_village_exterior_kit.blend"
    glb_path = root_path / "game/assets/models/act1" / GLB_NAME
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(KIT_ROOT)
    dwelling = bpy.data.objects.get(DWELLING_ROOT)
    well = bpy.data.objects.get(WELL_ROOT)
    if root is None or dwelling is None or well is None or dwelling.parent is not root or well.parent is not root:
        raise RuntimeError("Baseline kit root/component hierarchy is incomplete")

    author_dwelling(dwelling)
    author_well(well)
    scene = bpy.context.scene
    scene["generator"] = "assets/source/blender/act1/urman_village_exterior_kit.py"
    scene["asset_status"] = "authored production dwelling facade v2 and well landmark v1; presentation-only and not integrated"
    scene["dwelling_geometry_pass"] = "v2 grounded wall, asymmetrical faceted roof, deeper porch and restrained weathering"
    scene["well_geometry_pass"] = "v1 staggered masonry/timber ring, leaning posts, pitched eave roof and restrained bucket/rope cue"
    scene["texture_policy"] = "geometry and existing basic materials only; no texture files"
    bpy.context.view_layer.update()
    validate(root, dwelling, well)

    save_versions = bpy.context.preferences.filepaths.save_version
    bpy.context.preferences.filepaths.save_version = 0
    try:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    finally:
        bpy.context.preferences.filepaths.save_version = save_versions
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    print(f"village-exterior-pass: saved {blend_path}")
    print(f"village-exterior-pass: exported {glb_path}")


if __name__ == "__main__":
    main()
