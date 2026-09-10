"""Validate and export the authored zirat roadside kit reproducibly.

Run with the pinned Blender:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python tools/blender/export_zirat_roadside_kit.py -- --root <repo>

The zirat roadside kit is project-original geometry and this script is the
documented reproducible export path required by tracker task ART-005. It
    re-opens the saved source file, idempotently creates the declared Wave 17
linked-masses pass when needed, validates the published component contract
(names, parents, counts, no images/collision), then exports the runtime GLB
with fixed settings so repeated exports of an unchanged source are byte-stable.
Marker and boundary geometry stays neutral and requires cultural/religious
review before final acceptance.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT_NAME = "URMAN_ZiratRoadsideKit"
EXPECTED_COMPONENTS = (
    "WetRoadShoulder_Left",
    "WetRoadShoulder_Right",
    "RoadsideDitch",
    "CulvertStoneCluster",
    "ZiratBoundaryFence",
    "ZiratOpenGate",
    "ZiratMarkerGroup_Low",
    "ZiratMarkerGroup_Far",
    "ZiratPathEdge",
    "ZiratBirchShrubMass",
    "ZiratDistantVillageMass",
)
FORBIDDEN_NAME_PARTS = ("-col", "collision", "physics", "nav", "interact")
AUTHORED_PASS_ID = "wave17-zirat-cemetery-edge-v1"
FOLIAGE_PASS_ID = "branched-birch-leaves-v2"
MID_TREE_SPECS = (
    (-1.72, 0.78, 3.10, 0.14, 0.86),
    (0.42, 2.72, 3.85, -0.16, 0.72),
    (2.54, 4.72, 2.82, 0.20, 0.92),
)
WINDBREAK_SPECS = (
    (-2.68, 6.78, 4.18, 0.24, 0.30, "BirchBark", "BirchLeaves"),
    (3.36, 7.92, 3.62, -0.20, 0.22, "BirchBark", "ShrubGreen"),
)
AUTHORED_PASS_OBJECTS = (
    "WetRoadShoulder_Left_Relief_00_LOD0",
    "WetRoadShoulder_Left_RoadBand_LOD0",
    "WetRoadShoulder_Right_Relief_00_LOD0",
    "WetRoadShoulder_Right_RoadBand_LOD0",
    "RoadsideDitch_Relief_00_LOD0",
    "CulvertStoneCluster_Mouth_00_LOD0",
    "ZiratBoundaryFence_RailLower_West_LOD0",
    "ZiratOpenGate_Post_00_LOD0",
    "ZiratMarkerGroup_Low_Marker_00_LOD0",
    "ZiratMarkerGroup_Far_Marker_00_LOD0",
    "ZiratPathEdge_PathRibbon_00_LOD0",
    "ZiratBirchShrubMass_MidTrunk_00_LOD0",
    "ZiratDistantVillageMass_House_00_Body_LOD0",
    "ZiratBoundaryFence_Berm_West_LOD0",
    "ZiratBoundaryFence_Berm_East_LOD0",
    "ZiratMarkerGroup_Low_Companion_00_LOD0",
    "ZiratMarkerGroup_Far_Companion_00_LOD0",
    "ZiratBirchShrubMass_WindbreakTrunk_00_LOD0",
    "ZiratBirchShrubMass_WindbreakCanopy_00_LOD0",
)


def source_material(name: str) -> bpy.types.Material:
    material = bpy.data.materials.get(name)
    if material is None:
        raise RuntimeError(f"Missing source material for authored zirat pass: {name}")
    return material


def authored_mesh(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    material_name: str,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    role: str = "authored zirat edge dressing",
) -> bpy.types.Object:
    existing = bpy.data.objects.get(name)
    if existing is not None:
        if existing.parent is not parent or existing.type != "MESH":
            raise RuntimeError(f"Authored zirat object has an unexpected parent/type: {name}")
        return existing

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=False)
    mesh.update()
    mesh.calc_loop_triangles()
    mesh.materials.append(source_material(material_name))
    collection = parent.users_collection[0] if parent.users_collection else bpy.context.scene.collection
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = rotation
    obj["urman_asset_id"] = "urman.act1.zirat_roadside_kit"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["presentation_only"] = True
    obj["lod_status"] = "LOD0"
    obj["asset_role"] = role
    obj["geometry_pass"] = AUTHORED_PASS_ID
    obj["triangle_count"] = len(mesh.loop_triangles)
    return obj


def faceted_lobe(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    top_scale: float = 0.72,
    skew: tuple[float, float] = (0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    top_height_variation: tuple[float, ...] | None = None,
    role: str = "authored low-poly wet-ground lobe",
) -> bpy.types.Object:
    width, depth, height = size
    half_width = width * 0.5
    half_depth = depth * 0.5
    ring = [
        (-half_width * 0.82, -half_depth),
        (half_width * 0.72, -half_depth * 0.88),
        (half_width, -half_depth * 0.10),
        (half_width * 0.66, half_depth),
        (-half_width * 0.58, half_depth * 0.92),
        (-half_width, half_depth * 0.10),
    ]
    top_heights = top_height_variation or (1.0,) * len(ring)
    if len(top_heights) != len(ring) or any(value <= 0.0 for value in top_heights):
        raise RuntimeError(f"Invalid lobe top-height profile: {name}")
    vertices = [(x, y, 0.0) for x, y in ring]
    vertices.extend(
        (x * top_scale + skew[0], y * top_scale + skew[1], height * top_heights[index])
        for index, (x, y) in enumerate(ring)
    )
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((index, (index + 1) % 6, (index + 1) % 6 + 6, index + 6) for index in range(6))
    return authored_mesh(name, parent, vertices, faces, material_name, location, rotation, role=role)


def tapered_form(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    top_scale: float,
    lean: tuple[float, float] = (0.0, 0.0),
    top_height_variation: tuple[float, ...] | None = None,
    role: str = "authored quiet faceted form",
) -> bpy.types.Object:
    width, depth, height = size
    if min(width, depth, height) <= 0.0 or top_scale <= 0.0:
        raise RuntimeError(f"Invalid tapered form dimensions: {name}")
    ring = (
        (-0.50, -0.18),
        (-0.22, -0.50),
        (0.32, -0.42),
        (0.52, 0.04),
        (0.18, 0.48),
        (-0.38, 0.34),
    )
    top_heights = top_height_variation or (1.0,) * len(ring)
    if len(top_heights) != len(ring) or any(value <= 0.0 for value in top_heights):
        raise RuntimeError(f"Invalid tapered top-height profile: {name}")
    vertices = [(x * width, y * depth, 0.0) for x, y in ring]
    vertices.extend(
        (
            x * width * top_scale + lean[0],
            y * depth * top_scale + lean[1],
            height * top_heights[index],
        )
        for index, (x, y) in enumerate(ring)
    )
    faces: list[tuple[int, ...]] = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces.extend((index, (index + 1) % 6, (index + 1) % 6 + 6, index + 6) for index in range(6))
    return authored_mesh(name, parent, vertices, faces, material_name, location, role=role)


def sedge_cluster(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    height: float,
    spread: float,
    material_name: str,
    role: str,
) -> bpy.types.Object:
    """Create three irregular blades in one mesh; no repeated cone primitives."""
    if height <= 0.0 or spread <= 0.0:
        raise RuntimeError(f"Invalid sedge dimensions: {name}")
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for offset, factor, lean in ((-0.34, 0.76, -0.16), (0.0, 1.0, 0.08), (0.30, 0.84, 0.22)):
        x = offset * spread
        z = height * factor
        base = len(vertices)
        blade_width = spread * 0.08
        vertices.extend(
            (
                (x - blade_width, -blade_width * 0.40, 0.0),
                (x + blade_width, blade_width * 0.40, 0.0),
                (x + lean, blade_width * 0.30, z),
                (x + lean - blade_width * 0.38, -blade_width * 0.28, z),
            )
        )
        faces.extend(((base, base + 1, base + 2, base + 3), (base + 3, base + 2, base + 1, base)))
    return authored_mesh(name, parent, vertices, faces, material_name, location, role=role)


def weathered_beam(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius: float,
    material_name: str,
    role: str = "authored weathered boundary brace",
) -> bpy.types.Object:
    existing = bpy.data.objects.get(name)
    if existing is not None:
        if existing.parent is not parent or existing.type != "MESH":
            raise RuntimeError(f"Authored zirat beam has an unexpected parent/type: {name}")
        return existing

    start_vector = Vector(start)
    end_vector = Vector(end)
    direction = end_vector - start_vector
    if direction.length <= 0.01:
        raise RuntimeError(f"Degenerate authored zirat beam: {name}")
    axis = direction.normalized()
    reference = Vector((0.0, 0.0, 1.0))
    if abs(axis.dot(reference)) > 0.92:
        reference = Vector((0.0, 1.0, 0.0))
    side = axis.cross(reference).normalized()
    up = side.cross(axis).normalized()
    ring_size = 6
    vertices: list[tuple[float, float, float]] = []
    for endpoint, taper in ((start_vector, 1.10), (end_vector, 0.82)):
        for index in range(ring_size):
            angle = 2.0 * math.pi * index / ring_size
            radial = side * math.cos(angle) + up * math.sin(angle)
            vertices.append(tuple(endpoint + radial * radius * taper))
    faces: list[tuple[int, ...]] = [
        tuple(reversed(range(ring_size))),
        tuple(range(ring_size, ring_size * 2)),
    ]
    faces.extend(
        (index, (index + 1) % ring_size, (index + 1) % ring_size + ring_size, index + ring_size)
        for index in range(ring_size)
    )
    return authored_mesh(name, parent, vertices, faces, material_name, role=role)


def bent_trunk(
    name: str,
    parent: bpy.types.Object,
    points: tuple[tuple[float, float, float], ...],
    radii: tuple[float, ...],
    material_name: str,
    role: str,
) -> bpy.types.Object:
    """Create a few-ring windbreak trunk with a quiet, non-repeated lean."""
    if len(points) < 2 or len(points) != len(radii) or any(radius <= 0.0 for radius in radii):
        raise RuntimeError(f"Invalid authored zirat trunk profile: {name}")
    sides = 6
    vertices: list[tuple[float, float, float]] = []
    for ring_index, ((x, y, z), radius) in enumerate(zip(points, radii)):
        phase = 0.10 * (ring_index % 2)
        for side in range(sides):
            angle = math.tau * side / sides + phase
            vertices.append((x + math.cos(angle) * radius, y + math.sin(angle) * radius, z))
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    last_ring = (len(points) - 1) * sides
    faces.append(tuple(last_ring + side for side in range(sides)))
    for ring_index in range(len(points) - 1):
        start = ring_index * sides
        following = (ring_index + 1) * sides
        faces.extend(
            (start + side, start + (side + 1) % sides,
             following + (side + 1) % sides, following + side)
            for side in range(sides)
        )
    return authored_mesh(name, parent, vertices, faces, material_name, role=role)


def irregular_canopy(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material_name: str,
    lean: tuple[float, float],
    role: str,
) -> bpy.types.Object:
    """Build one anchored, three-ring canopy; the side rhythm is not a blob stack."""
    width, depth, height = size
    if min(width, depth, height) <= 0.0:
        raise RuntimeError(f"Invalid authored zirat canopy dimensions: {name}")
    sides = 6
    ring_profiles = (
        (0.00, 1.00, (1.00, 0.86, 1.08, 0.78, 0.94, 1.02), (0.0, 0.0)),
        (0.48, 0.78, (0.76, 1.04, 0.82, 0.98, 0.70, 0.90), (lean[0] * 0.38, lean[1] * 0.38)),
        (0.90, 0.34, (0.56, 0.78, 0.50, 0.72, 0.46, 0.66), lean),
    )
    vertices: list[tuple[float, float, float]] = []
    for level, scale, side_scales, offset in ring_profiles:
        for side, side_scale in enumerate(side_scales):
            angle = math.tau * side / sides + 0.12 * (side % 2)
            vertices.append(
                (
                    offset[0] + math.cos(angle) * width * scale * side_scale,
                    offset[1] + math.sin(angle) * depth * scale * side_scale,
                    height * level,
                )
            )
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(ring_profiles) - 1):
        start = ring_index * sides
        following = (ring_index + 1) * sides
        faces.extend(
            (start + side, start + (side + 1) % sides,
             following + (side + 1) % sides, following + side)
            for side in range(sides)
        )
    last = (len(ring_profiles) - 1) * sides
    faces.append(tuple(last + side for side in range(sides)))
    return authored_mesh(name, parent, vertices, faces, material_name, location, role=role)


def clear_component_meshes(root: bpy.types.Object) -> int:
    removed = 0
    for component in root.children:
        for obj in list(component.children_recursive):
            if obj.type != "MESH":
                continue
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
            removed += 1
    return removed


def relief_ribbon(
    name: str,
    parent: bpy.types.Object,
    points: tuple[tuple[float, float], ...],
    half_widths: tuple[float, ...],
    heights: tuple[float, ...],
    material_name: str,
    role: str,
) -> bpy.types.Object:
    """Build a shallow, beveled strip so long ground edges never read as boxes."""
    if len(points) < 2 or len(points) != len(half_widths) or len(points) != len(heights):
        raise RuntimeError(f"Invalid relief ribbon profile: {name}")
    if any(width <= 0.0 or height <= 0.0 for width, height in zip(half_widths, heights)):
        raise RuntimeError(f"Invalid relief ribbon dimensions: {name}")

    vertices: list[tuple[float, float, float]] = []
    for (x, y), width, height in zip(points, half_widths, heights):
        vertices.extend(
            (
                (x - width, y, 0.0),
                (x + width, y, 0.0),
                (x - width * 0.82, y, height),
                (x + width * 0.82, y, height),
            )
        )
    faces: list[tuple[int, ...]] = [(0, 1, 3, 2)]
    for index in range(len(points) - 1):
        current = index * 4
        following = (index + 1) * 4
        faces.extend(
            (
                (current + 2, current + 3, following + 3, following + 2),
                (current, following, following + 2, current + 2),
                (current + 1, current + 3, following + 3, following + 1),
                (current, current + 1, following + 1, following),
            )
        )
    last = (len(points) - 1) * 4
    faces.append((last, last + 2, last + 3, last + 1))
    return authored_mesh(name, parent, vertices, faces, material_name, role=role)


def gable_house(
    prefix: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    wall_material: str,
    roof_material: str,
    roof_height: float,
    role: str,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    width, depth, height = size
    if min(width, depth, height, roof_height) <= 0.0:
        raise RuntimeError(f"Invalid distant house dimensions: {prefix}")
    half_width = width * 0.5
    half_depth = depth * 0.5
    body = authored_mesh(
        f"{prefix}_Body_LOD0",
        parent,
        [
            (-half_width, -half_depth, 0.0),
            (half_width, -half_depth, 0.0),
            (half_width, half_depth, 0.0),
            (-half_width, half_depth, 0.0),
            (-half_width, -half_depth, height),
            (half_width, -half_depth, height),
            (half_width, half_depth, height),
            (-half_width, half_depth, height),
        ],
        (
            (0, 3, 2, 1),
            (4, 5, 6, 7),
            (0, 1, 5, 4),
            (1, 2, 6, 5),
            (2, 3, 7, 6),
            (3, 0, 4, 7),
        ),
        wall_material,
        location,
        role=role,
    )
    roof_width = width * 0.62
    roof_depth = depth * 0.56
    roof = authored_mesh(
        f"{prefix}_Roof_LOD0",
        parent,
        [
            (-roof_width, -roof_depth, 0.0),
            (0.0, -roof_depth, roof_height),
            (roof_width, -roof_depth, 0.0),
            (-roof_width, roof_depth, 0.0),
            (0.0, roof_depth, roof_height),
            (roof_width, roof_depth, 0.0),
        ],
        (
            (0, 1, 4, 3),
            (1, 2, 5, 4),
            (0, 3, 5, 2),
            (0, 1, 2),
            (3, 4, 5),
        ),
        roof_material,
        (location[0], location[1], location[2] + height),
        role=f"{role}; quiet pitched roof silhouette",
    )
    return body, roof


def ensure_authored_pass(root: bpy.types.Object) -> bool:
    if root.get("zirat_authored_pass") == AUTHORED_PASS_ID and all(
        bpy.data.objects.get(name) is not None for name in AUTHORED_PASS_OBJECTS
    ):
        return False

    clear_component_meshes(root)

    left_shoulder = bpy.data.objects["WetRoadShoulder_Left"]
    right_shoulder = bpy.data.objects["WetRoadShoulder_Right"]
    shoulder_profile = (
        ((-2.82, -8.1), (-2.58, -4.6), (-2.76, -1.0), (-2.54, 2.5), (-2.68, 5.6), (-2.49, 8.2)),
        (1.08, 1.25, 1.02, 1.18, 1.08, 1.22),
        (0.10, 0.15, 0.09, 0.16, 0.12, 0.10),
    )
    for component, side, sign in ((left_shoulder, "Left", -1.0), (right_shoulder, "Right", 1.0)):
        points = tuple((sign * abs(x), y) for x, y in shoulder_profile[0])
        relief_ribbon(
            f"WetRoadShoulder_{side}_Relief_00_LOD0",
            component,
            points,
            shoulder_profile[1],
            shoulder_profile[2],
            "DampEarth",
            "uneven wet shoulder ribbon with a low beveled road edge",
        )
        relief_ribbon(
            f"WetRoadShoulder_{side}_WetEdge_00_LOD0",
            component,
            tuple((x + sign * 0.18, y + 0.18) for x, y in points),
            (0.30, 0.38, 0.28, 0.36, 0.31, 0.34),
            (0.032, 0.045, 0.026, 0.040, 0.030, 0.036),
            "WetSheen",
            "shallow damp contact at the shoulder edge; no painted puddle card",
        )
        relief_ribbon(
            f"WetRoadShoulder_{side}_RoadBand_LOD0",
            component,
            tuple((x - sign * 0.42, y) for x, y in points),
            (0.42, 0.46, 0.40, 0.44, 0.42, 0.45),
            (0.045, 0.056, 0.040, 0.052, 0.044, 0.048),
            "WetSheen",
            "muted wet road edge band retained for the connected-world material hook",
        )
        for index, (x, y, width, depth, height) in enumerate(
            (
                (sign * 3.52, -6.3, 0.62, 0.72, 0.12),
                (sign * 3.66, -2.1, 0.48, 0.66, 0.09),
                (sign * 3.48, 2.0, 0.56, 0.80, 0.13),
                (sign * 3.72, 6.1, 0.44, 0.62, 0.10),
            )
        ):
            faceted_lobe(
                f"WetRoadShoulder_{side}_Contact_{index:02d}_LOD0",
                component,
                (x, y, 0.04),
                (width, depth, height),
                "DampEarthDark",
                top_scale=0.70 + index * 0.04,
                skew=(sign * 0.05, -0.02 if index % 2 else 0.03),
                role="irregular shoulder contact clod; ground edge explains road width",
            )
            sedge_cluster(
                f"WetRoadShoulder_{side}_Grass_{index:02d}_LOD0",
                component,
                (sign * (3.86 + 0.08 * (index % 2)), y + 0.28, 0.02),
                0.24 + 0.04 * (index % 3),
                0.30 + 0.04 * (index % 2),
                "RoadGrass",
                role="short grass at the maintained road shoulder edge",
            )

    ditch = bpy.data.objects["RoadsideDitch"]
    ditch_points = ((0.38, -8.0), (0.62, -5.0), (0.48, -2.1), (0.76, 1.0), (0.56, 4.2), (0.88, 8.0))
    relief_ribbon(
        "RoadsideDitch_Relief_00_LOD0",
        ditch,
        ditch_points,
        (0.68, 0.86, 0.72, 0.90, 0.76, 0.88),
        (0.10, 0.14, 0.08, 0.13, 0.09, 0.15),
        "DampEarthDark",
        "shallow uneven roadside ditch with visible bank transitions",
    )
    relief_ribbon(
        "RoadsideDitch_Water_00_LOD0",
        ditch,
        tuple((x, y + 0.04) for x, y in ditch_points),
        (0.25, 0.30, 0.22, 0.34, 0.24, 0.30),
        (0.020, 0.028, 0.018, 0.026, 0.020, 0.030),
        "DitchWater",
        "quiet ditch water shadow seated inside the authored relief",
    )
    for index, (x, y, width, depth, height) in enumerate(
        ((1.38, -6.2, 0.54, 0.82, 0.11), (1.52, -3.4, 0.46, 0.70, 0.09),
         (1.36, 0.2, 0.60, 0.88, 0.13), (1.66, 3.7, 0.48, 0.74, 0.10),
         (1.50, 6.6, 0.56, 0.78, 0.12))
    ):
        faceted_lobe(
            f"RoadsideDitch_Bank_{index:02d}_LOD0",
            ditch,
            (x, y, 0.05),
            (width, depth, height),
            "DampEarth",
            top_scale=0.72 + index * 0.03,
            skew=(0.04 if index % 2 else -0.05, 0.02),
            role="low eroded ditch-bank contact; no repeated block",
        )
        sedge_cluster(
            f"RoadsideDitch_Sedge_{index:02d}_LOD0",
            ditch,
            (x + 0.22, y + 0.18, 0.03),
            0.38 + 0.07 * (index % 3),
            0.32 + 0.05 * (index % 2),
            "DitchGrass",
            role="sedge concentrated in the wet ditch low point",
        )

    culvert = bpy.data.objects["CulvertStoneCluster"]
    relief_ribbon(
        "CulvertStoneCluster_Mouth_00_LOD0",
        culvert,
        ((0.52, -0.48), (1.62, -0.12), (2.02, 0.28)),
        (0.25, 0.30, 0.24),
        (0.26, 0.34, 0.28),
        "DitchWater",
        "dark culvert mouth tied into the roadside ditch profile",
    )
    for index, (x, y, width, depth, height, material) in enumerate(
        (
            (0.18, -0.66, 0.68, 0.82, 0.30, "MossyStone"),
            (0.82, -0.78, 0.58, 0.70, 0.24, "MossyStone"),
            (1.46, -0.62, 0.72, 0.76, 0.34, "MossGreen"),
            (2.12, -0.30, 0.62, 0.74, 0.28, "MossyStone"),
            (2.42, 0.24, 0.54, 0.66, 0.23, "MossGreen"),
        )
    ):
        faceted_lobe(
            f"CulvertStoneCluster_Stone_{index:02d}_LOD0",
            culvert,
            (x, y, 0.08),
            (width, depth, height),
            material,
            top_scale=0.70 + 0.05 * (index % 3),
            skew=(0.05 * (-1 if index % 2 else 1), 0.02),
            role="low mossy culvert stone with varied hand-placed silhouette",
        )
    faceted_lobe(
        "CulvertStoneCluster_WingLeft_LOD0", culvert, (-0.10, 0.08, 0.07),
        (0.96, 1.38, 0.28), "MossyStone", top_scale=0.70,
        skew=(-0.06, 0.03), role="left culvert wing, embedded into ditch edge",
    )
    faceted_lobe(
        "CulvertStoneCluster_WingRight_LOD0", culvert, (2.54, 0.26, 0.06),
        (0.82, 1.10, 0.25), "MossGreen", top_scale=0.66,
        skew=(0.05, -0.04), role="right culvert wing, embedded into ditch edge",
    )

    fence = bpy.data.objects["ZiratBoundaryFence"]
    for side, sign, x_values in (
        ("West", -1.0, (-6.95, -6.18, -5.32, -4.38, -3.36, -2.36)),
        ("East", 1.0, (2.36, 3.30, 4.18, 5.26, 6.16, 7.02)),
    ):
        for index, x in enumerate(x_values):
            height = (0.94, 1.12, 1.00, 1.20, 0.90, 1.06)[index]
            tapered_form(
                f"ZiratBoundaryFence_Picket_{side}_{index:02d}_LOD0",
                fence,
                (x, 0.06 * ((index % 3) - 1), 0.0),
                (0.18, 0.16, height),
                "WeatheredWood",
                top_scale=0.72 + 0.05 * (index % 3),
                lean=(sign * (0.06 if index % 2 else -0.03), 0.02 * ((index % 3) - 1)),
                top_height_variation=(0.94, 1.0, 0.92, 1.0, 0.96, 0.90),
                role="irregular low boundary picket; quiet weathered enclosure",
            )
        weathered_beam(
            f"ZiratBoundaryFence_RailLower_{side}_LOD0",
            fence,
            (x_values[0], 0.0, 0.38),
            (x_values[-1], 0.08, 0.45),
            0.055,
            "WeatheredWoodDark",
            role="low boundary rail following the uneven fence line",
        )
        weathered_beam(
            f"ZiratBoundaryFence_RailUpper_{side}_LOD0",
            fence,
            (x_values[0], 0.0, 0.76),
            (x_values[-1], 0.08, 0.84),
            0.050,
            "WeatheredWood",
            role="upper boundary rail following the uneven fence line",
        )
    weathered_beam(
        "ZiratBoundaryFence_BrokenBrace_West_LOD0", fence,
        (-6.9, 0.0, 0.20), (-3.28, 0.06, 1.06), 0.060, "WeatheredWoodDark",
        role="single repaired diagonal brace on the old west boundary",
    )
    weathered_beam(
        "ZiratBoundaryFence_BrokenBrace_East_LOD0", fence,
        (3.30, 0.06, 1.02), (6.95, 0.08, 0.22), 0.055, "WeatheredWoodDark",
        role="single repaired diagonal brace on the old east boundary",
    )
    for index, x in enumerate((-4.9, 4.9)):
        faceted_lobe(
            f"ZiratBoundaryFence_GroundContact_{index:02d}_LOD0",
            fence,
            (x, 0.10, 0.04),
            (1.55 - 0.12 * index, 0.36, 0.13 + 0.02 * index),
            "DampEarth",
            top_scale=0.68 + 0.08 * index,
            skew=(0.06 if index == 0 else -0.05, 0.01),
            role="low soil contact under a repaired boundary run",
        )
    faceted_lobe(
        "ZiratBoundaryFence_Deadwood_00_LOD0", fence, (-5.65, 0.34, 0.04),
        (1.46, 0.30, 0.16), "WeatheredWoodDark", top_scale=0.62,
        skew=(0.08, 0.0), rotation=(0.0, 0.0, -0.14),
        role="single peripheral fallen branch at the old fence edge",
    )
    faceted_lobe(
        "ZiratBoundaryFence_Deadwood_01_LOD0", fence, (5.35, 0.42, 0.04),
        (1.22, 0.26, 0.18), "WeatheredWoodDark", top_scale=0.72,
        skew=(-0.06, 0.0), rotation=(0.0, 0.0, 0.12),
        role="single peripheral fallen branch at the old fence edge",
    )
    for side, points, widths, heights in (
        (
            "West",
            ((-7.45, -0.22), (-6.05, -0.18), (-4.52, -0.02), (-2.30, 0.16)),
            (0.34, 0.42, 0.30, 0.24),
            (0.08, 0.12, 0.09, 0.07),
        ),
        (
            "East",
            ((2.30, 0.14), (4.10, 0.02), (5.64, 0.18), (7.42, 0.30)),
            (0.26, 0.36, 0.30, 0.38),
            (0.07, 0.10, 0.08, 0.11),
        ),
    ):
        relief_ribbon(
            f"ZiratBoundaryFence_Berm_{side}_LOD0",
            fence,
            points,
            widths,
            heights,
            "DampEarth",
            "low uneven cemetery boundary berm tying the old fence to the ground",
        )

    gate = bpy.data.objects["ZiratOpenGate"]
    for index, x in enumerate((-1.55, 1.55)):
        tapered_form(
            f"ZiratOpenGate_Post_{index:02d}_LOD0",
            gate,
            (x, 0.0, 0.0),
            (0.30, 0.28, 1.58),
            "WeatheredWoodDark",
            top_scale=0.78,
            lean=(0.0, 0.0),
            role="stout open-gate post; presentation-only boundary cue",
        )
        faceted_lobe(
            f"ZiratOpenGate_FootStone_{index:02d}_LOD0",
            gate,
            (x, -0.02, 0.10),
            (0.70, 0.62, 0.18),
            "MossyStone",
            top_scale=0.72,
            skew=(0.04 * (-1 if index else 1), 0.02),
            role="low foot stone seating the open gate post",
        )
        sign = -1.0 if index == 0 else 1.0
        pivot = (x, 0.10, 0.22)
        end = (sign * 2.72, 0.92, 0.24)
        weathered_beam(
            f"ZiratOpenGate_Leaf_{index:02d}_LowerRail_LOD0", gate,
            pivot, end, 0.050, "WeatheredWood",
            role="swung-open gate leaf lower rail",
        )
        weathered_beam(
            f"ZiratOpenGate_Leaf_{index:02d}_UpperRail_LOD0", gate,
            (x, 0.10, 0.82), (sign * 2.72, 0.92, 0.78), 0.046, "WeatheredWoodDark",
            role="swung-open gate leaf upper rail",
        )
        for picket_index, fraction in enumerate((0.22, 0.52, 0.82)):
            px = x + (end[0] - x) * fraction
            py = 0.10 + (end[1] - 0.10) * fraction
            weathered_beam(
                f"ZiratOpenGate_Leaf_{index:02d}_Picket_{picket_index:02d}_LOD0",
                gate,
                (px, py, 0.08), (px, py, 1.05 - 0.10 * picket_index),
                0.042, "WeatheredWood",
                role="varied open-gate leaf picket",
            )

    path = bpy.data.objects["ZiratPathEdge"]
    relief_ribbon(
        "ZiratPathEdge_PathRibbon_00_LOD0",
        path,
        ((0.12, 0.05), (-0.02, 2.1), (0.22, 4.05), (-0.08, 6.1), (-0.22, 8.0)),
        (0.64, 0.72, 0.62, 0.76, 0.66),
        (0.040, 0.052, 0.035, 0.060, 0.044),
        "PathDirt",
        "quiet curving footpath linking roadside edge to the boundary entry",
    )
    grade_masses = (
        ((-1.56, 1.0, 0.0), (1.08, 1.42, 0.10), 0.86, (-0.06, 0.02),
         (0.82, 0.94, 0.74, 0.90, 0.70, 0.86)),
        ((1.56, 2.72, 0.0), (1.02, 1.44, 0.12), 0.84, (0.05, -0.04),
         (0.74, 0.92, 0.84, 0.68, 0.90, 0.78)),
        ((-1.52, 4.55, 0.0), (0.96, 1.34, 0.09), 0.88, (0.03, 0.05),
         (0.76, 0.90, 0.70, 0.86, 0.80, 0.94)),
        ((1.58, 6.55, 0.0), (1.04, 1.30, 0.11), 0.86, (-0.04, -0.02),
         (0.88, 0.72, 0.94, 0.78, 0.90, 0.68)),
    )
    for index, (location, size, top_scale, skew, top_heights) in enumerate(grade_masses):
        faceted_lobe(
            f"ZiratPathEdge_GradeMass_{index:02d}_LOD0",
            path,
            location,
            size,
            "DampEarthDark",
            top_scale=top_scale,
            skew=skew,
            top_height_variation=top_heights,
            role="asymmetric low-poly grade mass; central path remains visibly open",
        )
    for index, (x, y, height, spread) in enumerate(((-0.88, 1.62, 0.34, 0.32), (0.86, 4.72, 0.40, 0.36), (-0.78, 7.15, 0.30, 0.30))):
        sedge_cluster(
            f"ZiratPathEdge_EdgeGrass_{index:02d}_LOD0",
            path,
            (x, y, 0.02),
            height,
            spread,
            "ZiratGrass",
            role="short grass following the footpath edge without blocking passage",
        )
    path["central_path_clearance_m"] = 1.10
    path["grade_mass_count"] = len(grade_masses)

    marker_group = bpy.data.objects["ZiratMarkerGroup_Low"]
    low_markers = (
        (-2.82, 1.62, 0.56, 0.40, 0.74, (0.05, -0.02), 0.86, "A"),
        (-2.12, 2.70, 0.46, 0.34, 0.58, (-0.04, 0.03), 0.92, "A"),
        (2.54, 1.86, 0.62, 0.43, 0.82, (-0.06, 0.02), 0.82, "B"),
        (3.28, 3.16, 0.48, 0.35, 0.64, (0.06, -0.03), 0.90, "B"),
        (-2.54, 5.30, 0.54, 0.38, 0.70, (-0.05, 0.04), 0.88, "C"),
    )
    for index, (x, y, width, depth, height, lean, top_scale, cluster) in enumerate(low_markers):
        faceted_lobe(
            f"ZiratMarkerGroup_Low_Mound_{index:02d}_LOD0", marker_group,
            (x, y, 0.035), (width + 0.38, depth + 0.30, 0.15), "DampEarth",
            top_scale=0.74, skew=(0.04 if index % 2 else -0.05, 0.02),
            role=f"quiet earth mound under marker cluster {cluster}; no inscription",
        )
        marker = tapered_form(
            f"ZiratMarkerGroup_Low_Marker_{index:02d}_LOD0", marker_group,
            (x, y, 0.15), (width, depth, height), "QuietStone",
            top_scale=top_scale, lean=lean,
            top_height_variation=(0.88, 0.96, 0.82, 0.94, 0.86, 0.92),
            role=f"plain low memorial marker cluster {cluster}; no inscription or symbol",
        )
        marker["marker_cluster"] = cluster
        faceted_lobe(
            f"ZiratMarkerGroup_Low_LeafLitter_{index:02d}_LOD0", marker_group,
            (x + 0.08, y - 0.04, 0.065), (width + 0.30, depth + 0.22, 0.06), "LeafLitter",
            top_scale=0.78, role=f"quiet ground contact in marker cluster {cluster}; no inscription",
        )

    far_marker_group = bpy.data.objects["ZiratMarkerGroup_Far"]
    far_markers = (
        (-3.02, 6.48, 0.38, 0.27, 0.32, (0.04, 0.00), 0.90, "A"),
        (-2.24, 7.34, 0.30, 0.23, 0.25, (-0.03, 0.02), 0.94, "A"),
        (2.70, 6.76, 0.40, 0.28, 0.36, (-0.04, 0.01), 0.86, "C"),
        (3.42, 7.62, 0.30, 0.23, 0.28, (0.03, -0.01), 0.92, "C"),
    )
    for index, (x, y, width, depth, height, lean, top_scale, cluster) in enumerate(far_markers):
        faceted_lobe(
            f"ZiratMarkerGroup_Far_Mound_{index:02d}_LOD0", far_marker_group,
            (x, y, 0.028), (width + 0.24, depth + 0.20, 0.10), "DampEarthDark",
            top_scale=0.76, skew=(0.03 if index % 2 else -0.04, 0.02),
            role=f"quiet far marker mound cluster {cluster}; no inscription",
        )
        marker = tapered_form(
            f"ZiratMarkerGroup_Far_Marker_{index:02d}_LOD0", far_marker_group,
            (x, y, 0.10), (width, depth, height), "QuietStone",
            top_scale=top_scale, lean=lean,
            top_height_variation=(0.90, 0.82, 0.96, 0.78, 0.88, 0.94),
            role=f"plain far memorial marker cluster {cluster}; no inscription or symbol",
        )
        marker["marker_cluster"] = cluster
        faceted_lobe(
            f"ZiratMarkerGroup_Far_LeafLitter_{index:02d}_LOD0", far_marker_group,
            (x - 0.02, y + 0.02, 0.045), (width + 0.20, depth + 0.14, 0.045), "LeafLitter",
            top_scale=0.80, role=f"quiet far marker ground contact cluster {cluster}; no inscription",
        )
    marker_group["marker_cluster_count"] = 3
    marker_group["marker_cluster_note"] = "three irregular near clusters with open central approach"
    for index, (x, y, width, depth, height, lean, top_scale, cluster) in enumerate(
        (
            (-3.54, 3.56, 0.34, 0.28, 0.42, (-0.03, 0.02), 0.88, "A"),
            (3.78, 4.24, 0.38, 0.30, 0.48, (0.04, -0.02), 0.82, "B"),
        )
    ):
        companion = tapered_form(
            f"ZiratMarkerGroup_Low_Companion_{index:02d}_LOD0", marker_group,
            (x, y, 0.10), (width, depth, height), "QuietStone",
            top_scale=top_scale,
            lean=lean,
            top_height_variation=(0.90, 0.82, 0.96, 0.78, 0.88, 0.94),
            role=f"plain companion marker in quiet cluster {cluster}; no inscription or symbol",
        )
        companion["marker_cluster"] = cluster
    marker_group["companion_marker_count"] = 2
    far_marker_group["marker_cluster_count"] = 3
    for index, (x, y, width, depth, height, lean, top_scale, cluster) in enumerate(
        (
            (-3.58, 8.36, 0.24, 0.19, 0.23, (0.02, 0.00), 0.90, "A"),
            (3.86, 8.78, 0.26, 0.20, 0.27, (-0.03, 0.01), 0.86, "C"),
        )
    ):
        companion = tapered_form(
            f"ZiratMarkerGroup_Far_Companion_{index:02d}_LOD0", far_marker_group,
            (x, y, 0.07), (width, depth, height), "QuietStone",
            top_scale=top_scale,
            lean=lean,
            top_height_variation=(0.88, 0.94, 0.82, 0.96, 0.86, 0.92),
            role=f"plain far companion marker in quiet cluster {cluster}; no inscription or symbol",
        )
        companion["marker_cluster"] = cluster
    far_marker_group["companion_marker_count"] = 2

    birch_shrub = bpy.data.objects["ZiratBirchShrubMass"]
    for index, (x, y, height, lean_x, canopy_scale) in enumerate(MID_TREE_SPECS):
        tapered_form(
            f"ZiratBirchShrubMass_MidTrunk_{index:02d}_LOD0", birch_shrub,
            (x, y, 0.0), (0.24 + index * 0.03, 0.22, height), "BirchBark",
            top_scale=0.62, lean=(lean_x, 0.04 if index == 1 else -0.05),
            role="mid-distance birch trunk with varied lean and grounded contact",
        )
        weathered_beam(
            f"ZiratBirchShrubMass_Branch_{index:02d}_LOD0", birch_shrub,
            (x, y, height * 0.56), (x + lean_x * 1.8 - 0.34, y + 0.16, height * 0.78),
            0.042, "BirchBark", role="one irregular birch branch for a broken silhouette",
        )
        for canopy_index, (offset_x, offset_y, width, depth, canopy_z, top_scale) in enumerate(
            ((-0.34, 0.02, 1.44, 0.84, 0.72, 0.70), (0.26, 0.16, 0.98, 0.66, 0.94, 0.58))
        ):
            faceted_lobe(
                f"ZiratBirchShrubMass_MidCanopy_{index * 2 + canopy_index:02d}_LOD0",
                birch_shrub,
                (x + offset_x * canopy_scale, y + offset_y, height * canopy_z),
                (width * canopy_scale, depth * canopy_scale, 0.72 + index * 0.10),
                "BirchLeaves", top_scale=top_scale,
                skew=(0.11 if index == 1 and canopy_index == 0 else -0.05, 0.02 * canopy_index),
                role="mid-distance birch/shrub canopy mass; varied windowed silhouette",
            )
        faceted_lobe(
            f"ZiratBirchShrubMass_ContactLobe_{index:02d}_LOD0", birch_shrub,
            (x - 0.08, y + 0.08, 0.05), (1.30 + index * 0.18, 0.82 + index * 0.10, 0.34),
            "ShrubGreen", top_scale=0.68 + index * 0.05,
            skew=(0.06 * (-1 if index == 1 else 1), 0.0),
            role="grounded shrub contact lobe at the birch transition edge",
        )
    birch_shrub["mass_group"] = "mid-distance irregular birch/shrub framing"
    birch_shrub["mass_group_role"] = "edge framing only; no repeated tree row or path blocker"
    for index, (x, y, height, lean_x, trunk_radius, trunk_material, canopy_material) in enumerate(WINDBREAK_SPECS):
        bent_trunk(
            f"ZiratBirchShrubMass_WindbreakTrunk_{index:02d}_LOD0",
            birch_shrub,
            ((x, y, 0.02), (x + lean_x * 0.28, y + 0.04, height * 0.46),
             (x + lean_x, y + 0.10, height * 0.84)),
            (trunk_radius, trunk_radius * 0.72, trunk_radius * 0.42),
            trunk_material,
            "windbreak birch trunk with a restrained lean and grounded three-ring profile",
        )
        weathered_beam(
            f"ZiratBirchShrubMass_WindbreakBranch_{index:02d}_LOD0",
            birch_shrub,
            (x + lean_x * 0.28, y + 0.04, height * 0.50),
            (x + lean_x - 0.54 + 0.26 * index, y + 0.20, height * 0.70),
            0.045,
            trunk_material,
            role="single broken windbreak branch supporting a non-repeated canopy",
        )
        irregular_canopy(
            f"ZiratBirchShrubMass_WindbreakCanopy_{index:02d}_LOD0",
            birch_shrub,
            (x + lean_x, y + 0.10, height * 0.68),
            (1.16 - 0.10 * index, 0.72 + 0.06 * index, 1.00 + 0.12 * index),
            canopy_material,
            (0.20 if index == 0 else -0.16, 0.05),
            "asymmetric windbreak canopy anchored to a visible birch trunk",
        )
        faceted_lobe(
            f"ZiratBirchShrubMass_WindbreakContact_{index:02d}_LOD0",
            birch_shrub,
            (x - 0.04, y + 0.08, 0.04),
            (1.24 - 0.12 * index, 0.80, 0.24),
            "ShrubGreen",
            top_scale=0.68 + 0.04 * index,
            skew=(0.06 if index == 0 else -0.04, 0.01),
            role="low windbreak understory contact; kept outside the approach",
        )

    distant = bpy.data.objects["ZiratDistantVillageMass"]
    house_specs = (
        (-7.0, 10.8, 2.5, 2.8, 1.18, 0.68),
        (-3.9, 12.6, 2.9, 3.4, 1.38, 0.82),
        (0.2, 11.6, 2.2, 2.5, 1.04, 0.58),
        (3.8, 13.2, 3.0, 3.5, 1.42, 0.76),
        (6.7, 15.8, 2.4, 2.9, 1.16, 0.88),
    )
    for index, (x, y, width, depth, height, roof_height) in enumerate(house_specs):
        gable_house(
            f"ZiratDistantVillageMass_House_{index:02d}", distant,
            (x, y, 0.16 + 0.04 * (index % 2)), (width, depth, height),
            "DistantWall", "DistantRoof", roof_height,
            "far low village volume with full wall/roof silhouette",
        )
    for index, (x, y, width, depth, height, top_scale) in enumerate(
        (
            (-8.0, 10.0, 3.1, 1.72, 1.42, 0.70),
            (-5.2, 14.9, 2.6, 1.55, 1.18, 0.86),
            (1.0, 14.2, 2.8, 1.62, 1.34, 0.74),
            (5.1, 11.0, 3.0, 1.74, 1.52, 0.82),
            (8.0, 17.4, 2.4, 1.48, 1.10, 0.62),
            (-1.8, 17.0, 3.4, 1.80, 1.28, 0.78),
        )
    ):
        faceted_lobe(
            f"ZiratDistantVillageMass_FarCanopy_{index:02d}_LOD0", distant,
            (x, y, 0.26 + 0.05 * (index % 2)), (width, depth, height), "DistantFoliage",
            top_scale=top_scale, skew=(0.16 if index % 3 == 0 else -0.12, 0.04 * (index % 2)),
            role="far mixed foliage value mass behind the village memory line",
        )
    for index, (x, y, width, depth, height, top_heights) in enumerate(
        (
            (-7.4, 10.2, 2.8, 1.24, 0.15, (0.82, 0.96, 0.72, 0.90, 0.76, 0.88)),
            (-1.4, 13.8, 2.6, 1.14, 0.12, (0.74, 0.92, 0.84, 0.68, 0.90, 0.78)),
            (5.8, 15.4, 2.9, 1.28, 0.16, (0.76, 0.90, 0.70, 0.86, 0.80, 0.94)),
        )
    ):
        faceted_lobe(
            f"ZiratDistantVillageMass_ForegroundBank_{index:02d}_LOD0", distant,
            (x, y, 0.05), (width, depth, height), "DistantFoliage",
            top_scale=0.70 + index * 0.06, skew=(0.14 if index == 0 else -0.10, 0.0),
            top_height_variation=top_heights,
            role="low far ground bank closing the village horizon without a wall",
        )
    weathered_beam(
        "ZiratDistantVillageMass_FarFence_LOD0", distant,
        (-8.5, 9.2, 0.34), (8.4, 9.5, 0.40), 0.035, "DistantFence",
        role="thin far fence line that ties the village silhouettes together",
    )
    for index, x in enumerate((-8.1, -5.0, -1.6, 2.0, 5.1, 8.0)):
        tapered_form(
            f"ZiratDistantVillageMass_FarFencePost_{index:02d}_LOD0", distant,
            (x, 9.32 + 0.04 * (index % 2), 0.0), (0.12, 0.12, 0.56 + 0.06 * (index % 3)),
            "DistantFence", top_scale=0.78,
            lean=(0.03 * (-1 if index % 2 else 1), 0.0),
            role="small far fence post supporting village depth",
        )
    distant["mass_group"] = "far irregular village and foliage horizon closure"
    distant["mass_group_role"] = "quiet village memory behind the zirat boundary; not a skyline wall"

    root["zirat_authored_pass"] = AUTHORED_PASS_ID
    root["zirat_authored_pass_objects"] = len(mesh_descendants(root))
    root["zirat_authored_pass_note"] = (
        "one linked source-authored pass: uneven road shoulders and ditch, "
        "culvert contact, low boundary with open gate, grouped neutral markers, "
        "curving path, anchored birch windbreak and far village memory"
    )
    return True


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def replace_foliage_geometry(obj, vertices, faces):
    """Component-space geometry keeps every published object's transform intact."""
    inverse = obj.matrix_local.inverted()
    mesh = obj.data
    mesh.clear_geometry()
    mesh.from_pydata([tuple(inverse @ Vector(v)) for v in vertices], [], faces)
    mesh.validate(verbose=False)
    mesh.update()
    mesh.calc_loop_triangles()
    obj["triangle_count"] = len(mesh.loop_triangles)
    obj["foliage_geometry_pass"] = FOLIAGE_PASS_ID


def ensure_leaf_crowns() -> bool:
    """Reconstruct slender branching birches with vertically layered open crowns."""
    component = bpy.data.objects["ZiratBirchShrubMass"]
    if component.get("foliage_geometry_pass") == FOLIAGE_PASS_ID:
        return False
    crown_bounds = {}
    for windbreak, specs in ((False, MID_TREE_SPECS), (True, WINDBREAK_SPECS)):
        for index, spec in enumerate(specs):
            x, y, height, lean = spec[:4]
            prefix = "Windbreak" if windbreak else "Mid"
            trunk = bpy.data.objects[f"ZiratBirchShrubMass_{prefix}Trunk_{index:02d}_LOD0"]
            branch = bpy.data.objects[f"ZiratBirchShrubMass_{'Windbreak' if windbreak else ''}Branch_{index:02d}_LOD0"]
            phase = index * 1.7 + (0.8 if windbreak else 0.0)
            base = 0.02 if windbreak else 0.0
            stem = [Vector((x, y, base)), Vector((x + lean * 0.1, y - 0.03, height * 0.34)),
                    Vector((x + lean * 0.55, y + 0.06, height * 0.55)),
                    Vector((x + lean, y + 0.03, height * 0.78)),
                    Vector((x + lean * 1.25, y + 0.10, height))]

            def add_stem(vertices, faces, points, radii):
                start = len(vertices)
                for point, radius in zip(points, radii):
                    vertices.extend(tuple(point + Vector((math.cos(side * math.tau / 6) * radius,
                                                          math.sin(side * math.tau / 6) * radius, 0)))
                                    for side in range(6))
                faces.append(tuple(start + side for side in reversed(range(6))))
                for ring in range(len(points) - 1):
                    a = start + ring * 6
                    faces.extend((a + side, a + (side + 1) % 6,
                                  a + 6 + (side + 1) % 6, a + 6 + side) for side in range(6))
                faces.append(tuple(start + (len(points) - 1) * 6 + side for side in range(6)))

            vertices, faces = [], []
            radius = 0.085 if windbreak else 0.067 + index * 0.004
            add_stem(vertices, faces, stem, [radius * r for r in (1, 0.76, 0.52, 0.30, 0.07)])
            replace_foliage_geometry(trunk, vertices, faces)
            vertices, faces = [], []
            for shoot in range(7):
                level = 1 + shoot // 3
                origin = stem[level].lerp(stem[level + 1], 0.16 + (shoot % 3) * 0.23)
                angle = phase + shoot * 2.399963
                spread = (0.95 if windbreak else 0.78) * (1.0 - shoot * 0.035)
                tip = origin + Vector((math.cos(angle) * spread, math.sin(angle) * spread,
                                       height * (0.19 - shoot * 0.009)))
                elbow = origin.lerp(tip, 0.53) + Vector((0, 0, 0.12))
                add_stem(vertices, faces, [origin, elbow, tip], [radius * 0.46, radius * 0.22, 0.008])
            replace_foliage_geometry(branch, vertices, faces)
            if windbreak:
                crown_bounds[f"ZiratBirchShrubMass_WindbreakCanopy_{index:02d}_LOD0"] = (
                    (x + lean - 1.28, x + lean + 1.28), (y - 1.02, y + 1.02),
                    (height * 0.34, height * 1.12))
            else:
                crown_bounds[f"ZiratBirchShrubMass_MidCanopy_{index * 2:02d}_LOD0"] = (
                    (x - 0.98, x + 0.68), (y - 0.72, y + 0.72), (height * 0.34, height * 0.88))
                crown_bounds[f"ZiratBirchShrubMass_MidCanopy_{index * 2 + 1:02d}_LOD0"] = (
                    (x + lean - 0.62, x + lean + 0.86), (y - 0.60, y + 0.74),
                    (height * 0.62, height * 1.12))
    for obj in component.children_recursive:
        if obj.type != "MESH" or not any(
            part in obj.name for part in ("Canopy_", "ContactLobe_", "WindbreakContact_")
        ):
            continue
        mesh = obj.data
        if len(mesh.materials) != 1 or mesh.materials[0].name not in ("BirchLeaves", "ShrubGreen"):
            raise RuntimeError(f"Unexpected crown material: {obj.name}")
        original = [obj.matrix_local @ v.co for v in mesh.vertices]
        bounds = crown_bounds.get(obj.name, [(min(v[i] for v in original), max(v[i] for v in original))
                                             for i in range(3)])
        phase = sum((i + 1) * ord(char) for i, char in enumerate(obj.name)) * 0.017
        vertices, faces = [], []
        shoots = 64 if "WindbreakCanopy_" in obj.name else 48 if "MidCanopy_" in obj.name else 24
        for shoot in range(shoots):
            angle = shoot * 2.399963 + phase
            z = 1.0 - 2.0 * (shoot + 0.5) / shoots
            radius = math.sqrt(1.0 - z * z)
            direction = Vector((math.cos(angle) * radius, math.sin(angle) * radius, z))
            side = direction.cross(Vector((0, 0, 1))).normalized()
            normal = side.cross(direction).normalized()
            length = 1.0 + 0.15 * math.sin(shoot * 4.17 + phase)
            for leaf in range(5):
                along = 0.43 + leaf * 0.17
                hand = 1 if leaf % 2 else -1
                anchor = direction * (length * along) + side * (hand * 0.12 * along)
                anchor.z -= 0.20 * along * along
                axis = (direction * 0.45 + side * hand).normalized()
                across = normal.cross(axis).normalized()
                size = 0.25 + 0.035 * math.sin(shoot * 3.1 + leaf * 2.7 + phase)
                points = (anchor - axis * size,
                          anchor + across * size * 0.58 + normal * size * 0.18,
                          anchor + axis * size,
                          anchor - across * size * 0.58 + normal * size * 0.18)
                # Separate vertices preserve reverse faces through mesh.validate().
                for face in ((0, 1, 2), (0, 2, 3), (2, 1, 0), (3, 2, 0)):
                    start = len(vertices)
                    vertices.extend(tuple(points[v]) for v in face)
                    faces.append((start, start + 1, start + 2))
        leaf_bounds = [(min(v[i] for v in vertices), max(v[i] for v in vertices)) for i in range(3)]
        vertices = [tuple(bounds[i][0] + (v[i] - leaf_bounds[i][0])
                          * (bounds[i][1] - bounds[i][0]) / (leaf_bounds[i][1] - leaf_bounds[i][0])
                          for i in range(3)) for v in vertices]
        replace_foliage_geometry(obj, vertices, faces)
        obj["leaf_geometry_pass"] = FOLIAGE_PASS_ID
        obj["leaf_component_bounds"] = [value for pair in bounds for value in pair]
        obj["leaf_shoot_count"] = shoots
    component["foliage_geometry_pass"] = FOLIAGE_PASS_ID
    return True


def mesh_descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    meshes: list[bpy.types.Object] = []
    for child in root.children:
        if child.type == "MESH":
            meshes.append(child)
        meshes.extend(mesh_descendants(child))
    return meshes


def validate(root: bpy.types.Object) -> dict[str, object]:
    direct = tuple(child.name for child in root.children)
    missing = [name for name in EXPECTED_COMPONENTS if name not in direct]
    if missing:
        raise RuntimeError(f"Missing published component roots: {missing}")
    unexpected = [name for name in direct if name not in EXPECTED_COMPONENTS]
    if unexpected:
        raise RuntimeError(f"Unexpected direct component roots: {unexpected}")

    meshes = mesh_descendants(root)
    if not meshes:
        raise RuntimeError("Authored kit has no presentation meshes")

    missing_authored = [name for name in AUTHORED_PASS_OBJECTS if bpy.data.objects.get(name) is None]
    if missing_authored:
        raise RuntimeError(f"Wave 17 zirat authored pass is incomplete: {missing_authored}")

    for mesh in meshes:
        mesh.data.calc_loop_triangles()
        if not mesh.data.loop_triangles:
            raise RuntimeError(f"Degenerate mesh: {mesh.name}")
        if any(polygon.area <= 1e-10 for polygon in mesh.data.polygons):
            raise RuntimeError(f"Degenerate polygon: {mesh.name}")
        lower_name = mesh.name.lower()
        if any(part in lower_name for part in FORBIDDEN_NAME_PARTS):
            raise RuntimeError(f"Collision-like mesh name leaked: {mesh.name}")
    triangles = sum(len(mesh.data.loop_triangles) for mesh in meshes)

    crowns = [obj for obj in meshes if obj.get("leaf_geometry_pass") == FOLIAGE_PASS_ID]
    if len(crowns) != 13:
        raise RuntimeError(f"Expected 13 open birch/shrub crowns, found {len(crowns)}")
    for obj in crowns:
        mesh = obj.data
        if len(mesh.polygons) != obj["leaf_shoot_count"] * 20:
            raise RuntimeError(f"Incomplete leaf shoots: {obj.name}")
        for start in range(0, len(mesh.polygons), 4):
            for front, back in ((start, start + 2), (start + 1, start + 3)):
                a, b = mesh.polygons[front], mesh.polygons[back]
                if a.normal.dot(b.normal) > -0.999 or sorted(tuple(mesh.vertices[v].co) for v in a.vertices) != sorted(tuple(mesh.vertices[v].co) for v in b.vertices):
                    raise RuntimeError(f"Leaf back face missing or incorrectly wound: {obj.name}")
        values = [obj.matrix_local @ v.co for v in mesh.vertices]
        for axis in range(3):
            actual = (min(v[axis] for v in values), max(v[axis] for v in values))
            if any(abs(actual[i] - obj["leaf_component_bounds"][axis * 2 + i]) > 1e-5 for i in range(2)):
                raise RuntimeError(f"Leaf crown changed intentional component bounds: {obj.name}")

    foliage = bpy.data.objects["ZiratBirchShrubMass"]
    values = [obj.matrix_local @ v.co for obj in foliage.children_recursive if obj.type == "MESH"
              for v in obj.data.vertices]
    for axis, (low, high) in enumerate(((-4.34, 5.22), (-0.56, 9.75), (0.0, 4.94))):
        if min(v[axis] for v in values) < low - 1e-5 or max(v[axis] for v in values) > high + 1e-5:
            raise RuntimeError(f"Birch silhouette exceeds authorized envelope on axis {axis}")
    if abs(min(v.z for v in values)) > 1e-5:
        raise RuntimeError("Birch component lost its ground anchor")
    for windbreak, specs in ((False, MID_TREE_SPECS), (True, WINDBREAK_SPECS)):
        for index, spec in enumerate(specs):
            name = f"ZiratBirchShrubMass_{'Windbreak' if windbreak else 'Mid'}Trunk_{index:02d}_LOD0"
            trunk = bpy.data.objects[name]
            base = [trunk.matrix_local @ v.co for v in trunk.data.vertices[:6]]
            anchor = sum(base, Vector()) / 6
            expected = Vector((spec[0], spec[1], 0.02 if windbreak else 0.0))
            if (anchor - expected).length > 1e-5:
                raise RuntimeError(f"Birch trunk moved from its ground anchor: {name}")

    images = [item for item in bpy.data.images if item.name not in ("Render Result", "Viewer Node")]
    if images:
        raise RuntimeError(f"Image textures are not allowed: {[image.name for image in images]}")

    forbidden_types = [
        obj.name
        for obj in bpy.data.objects
        if obj.type in ("CAMERA", "LIGHT", "CollisionShape") or obj.rigid_body is not None
    ]
    if forbidden_types:
        raise RuntimeError(f"Camera/light/physics objects are not allowed: {forbidden_types}")

    for name in EXPECTED_COMPONENTS:
        component = bpy.data.objects[name]
        if component.parent is not root:
            raise RuntimeError(f"Component root is not parented to the kit root: {name}")

    grade_objects = [
        bpy.data.objects[f"ZiratPathEdge_GradeMass_{index:02d}_LOD0"]
        for index in range(4)
    ]
    for grade in grade_objects:
        world_x = [(grade.matrix_world @ vertex.co).x for vertex in grade.data.vertices]
        if min(world_x) < -0.75 < max(world_x) or min(world_x) < 0.75 < max(world_x):
            raise RuntimeError(f"Threshold grade mass intrudes on central passage: {grade.name}")
    marker_objects = [
        mesh
        for mesh in meshes
        if "MarkerGroup_" in mesh.name and "_Marker_" in mesh.name
    ]
    if len(marker_objects) != 9 or {mesh.get("marker_cluster") for mesh in marker_objects} != {"A", "B", "C"}:
        raise RuntimeError("Marker grouping contract is not three irregular clusters")
    for marker in marker_objects:
        world_x = [(marker.matrix_world @ vertex.co).x for vertex in marker.data.vertices]
        if min(world_x) < -0.90 < max(world_x) or min(world_x) < 0.90 < max(world_x):
            raise RuntimeError(f"Marker blocks central respectful passage: {marker.name}")

    bounds: dict[str, tuple[float, float, float, float, float, float]] = {}
    for component in root.children:
        component_meshes = [mesh for mesh in meshes if mesh in component.children_recursive]
        if not component_meshes:
            raise RuntimeError(f"Empty published component: {component.name}")
        values = [
            (mesh.matrix_world @ vertex.co)
            for mesh in component_meshes
            for vertex in mesh.data.vertices
        ]
        bounds[component.name] = (
            min(value.x for value in values),
            min(value.y for value in values),
            min(value.z for value in values),
            max(value.x for value in values),
            max(value.y for value in values),
            max(value.z for value in values),
        )
        if any(bounds[component.name][index + 3] - bounds[component.name][index] <= 0.05 for index in range(3)):
            raise RuntimeError(f"Degenerate component bounds: {component.name}={bounds[component.name]}")
    # Five branching stems and 536 shoots replace the former flat crown envelopes.
    if triangles > 16000:
        raise RuntimeError(f"Authored zirat triangle budget exceeded: {triangles}")

    return {
        "component_count": len(direct),
        "mesh_count": len(meshes),
        "triangle_count": triangles,
        "material_count": len({slot.material.name for mesh in meshes for slot in mesh.material_slots if slot.material}),
        "bounds": " | ".join(
            f"{name}=" + ",".join(f"{value:.2f}" for value in component_bounds)
            for name, component_bounds in bounds.items()
        ),
    }


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_zirat_roadside_kit.blend"
    glb_path = root_path / "game/assets/models/act1/urman_zirat_roadside_kit.glb"

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY":
        raise RuntimeError(f"Missing authored root: {ROOT_NAME}")

    changed = ensure_authored_pass(root)
    changed = ensure_leaf_crowns() or changed
    if changed:
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    report = validate(root)

    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    for key, value in report.items():
        print(f"zirat-export: {key}={value}")
    print(f"zirat-export: exported {glb_path}")


if __name__ == "__main__":
    main()
