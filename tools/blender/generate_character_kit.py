"""Generate the project-original low-poly character kit for URMAN.

Run with Blender 4.5 LTS:
  blender --background --python tools/blender/generate_character_kit.py -- --root <repo>

The kit intentionally contains readable silhouettes and a small animation-safe
rig, not final facial art. It is a real GLB production input with deterministic
LOD1 meshes, provenance, Idle/Tension clips and no physics geometry. Godot
selects one character prefix per authored zone.
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


CHARACTERS = (
    ("Mansur", (0.36, 0.25, 0.18, 1.0), (0.58, 0.42, 0.24, 1.0), True, True),
    ("Gulsina", (0.38, 0.28, 0.34, 1.0), (0.63, 0.45, 0.28, 1.0), False, False),
    ("Alsu", (0.22, 0.35, 0.38, 1.0), (0.56, 0.39, 0.24, 1.0), False, False),
    ("TimurHazrat", (0.28, 0.38, 0.34, 1.0), (0.47, 0.42, 0.29, 1.0), True, True),
    ("CouncilElder", (0.38, 0.30, 0.23, 1.0), (0.52, 0.40, 0.26, 1.0), True, True),
    ("CouncilWitness", (0.24, 0.34, 0.40, 1.0), (0.56, 0.40, 0.28, 1.0), False, False),
    ("Naila", (0.38, 0.30, 0.26, 1.0), (0.54, 0.51, 0.40, 1.0), False, False),
    ("ArchiveClerk", (0.27, 0.30, 0.32, 1.0), (0.48, 0.38, 0.29, 1.0), False, False),
    ("PactKeeper", (0.24, 0.22, 0.21, 1.0), (0.57, 0.40, 0.27, 1.0), True, True),
)

# Deliberate presentation differences keep the six Act I people readable as
# adults with different roles while retaining the same ground anchor and rig.
# height, shoulder width, head scale, stance bias, torso width
SILHOUETTE_PROFILES = {
    "Mansur": (0.99, 1.08, 0.97, 0.032, 1.06),       # broad, older winter coat
    "Gulsina": (0.95, 0.96, 0.95, -0.026, 1.00),     # shorter, sturdy apron shape
    "Alsu": (1.01, 0.88, 0.84, 0.018, 0.88),         # young, narrow shoulders
    "TimurHazrat": (1.08, 1.02, 0.88, -0.014, 0.95), # tall, composed silhouette
    "CouncilElder": (1.02, 1.06, 0.96, 0.026, 1.08),
    "CouncilWitness": (1.06, 0.97, 0.85, -0.034, 0.93), # upright field officer
    "Naila": (0.99, 0.94, 0.86, 0.024, 0.90),        # slim medical worker
    "ArchiveClerk": (1.00, 1.00, 0.89, -0.018, 0.95),
    "PactKeeper": (1.04, 1.05, 0.93, 0.028, 1.04),
}

# Lift only the lower coat rings. The upper body, armature targets and foot
# anchor stay fixed; role values give heavy coats a longer line and let shorter
# worker/cardigan hems expose the existing long trouser meshes.
TORSO_BOTTOM_LIFTS = {
    "Mansur": -0.035,
    "Gulsina": 0.115,
    "Alsu": 0.145,
    "TimurHazrat": -0.005,
    "CouncilElder": -0.030,
    "CouncilWitness": 0.085,
    "Naila": 0.135,
    "ArchiveClerk": 0.065,
    "PactKeeper": -0.025,
}

# shoulder width/depth, sleeve fullness, trouser fullness, hem width/depth,
# cuff scale. These only reshape existing named pieces; the ranges stay close
# to the shared winter-kit proportions so cuffs, hands and trouser anchors meet.
WARDROBE_PROFILES = {
    "Mansur": (0.56, 0.37, 1.10, 1.08, 0.52, 0.36, 1.08),
    "Gulsina": (0.49, 0.35, 1.04, 0.98, 0.50, 0.35, 1.02),
    "Alsu": (0.40, 0.27, 0.88, 0.90, 0.36, 0.26, 0.90),
    "TimurHazrat": (0.49, 0.30, 1.00, 1.00, 0.43, 0.29, 1.00),
    "CouncilElder": (0.55, 0.36, 1.08, 1.06, 0.51, 0.35, 1.05),
    "CouncilWitness": (0.45, 0.29, 0.96, 0.96, 0.39, 0.27, 0.98),
    "Naila": (0.42, 0.27, 0.90, 0.92, 0.35, 0.25, 0.92),
    "ArchiveClerk": (0.45, 0.29, 0.94, 0.96, 0.40, 0.27, 0.96),
    "PactKeeper": (0.53, 0.35, 1.06, 1.04, 0.49, 0.33, 1.04),
}

# eye spread/width/height, brow height, nose scale, mouth width/height/drop.
# The existing almond, eyelid and iris meshes stay in place; these modest
# role landmark scales make the face read at conversation distance without
# adding meshes or changing any published names.
FACE_PROFILES = {
    "Mansur": (0.058, 0.069, 0.033, 0.050, 1.20, 0.053, 0.012, 0.086),
    "Gulsina": (0.063, 0.078, 0.045, 0.063, 0.98, 0.061, 0.016, 0.078),
    "Alsu": (0.069, 0.082, 0.051, 0.068, 0.90, 0.064, 0.017, 0.074),
    "TimurHazrat": (0.059, 0.068, 0.038, 0.051, 1.02, 0.052, 0.012, 0.085),
    "CouncilElder": (0.058, 0.070, 0.033, 0.048, 1.18, 0.052, 0.012, 0.086),
    "CouncilWitness": (0.065, 0.077, 0.042, 0.060, 1.03, 0.060, 0.016, 0.079),
    "Naila": (0.068, 0.081, 0.048, 0.066, 0.93, 0.064, 0.017, 0.074),
    "ArchiveClerk": (0.063, 0.077, 0.042, 0.059, 1.01, 0.060, 0.016, 0.080),
    "PactKeeper": (0.058, 0.071, 0.035, 0.052, 1.12, 0.053, 0.013, 0.085),
}

BEARD_PROFILES = {
    "Mansur": (0.132, 0.038, 0.088, 0.134),
    "TimurHazrat": (0.105, 0.034, 0.072, 0.132),
    "CouncilElder": (0.128, 0.038, 0.086, 0.134),
    "PactKeeper": (0.126, 0.038, 0.082, 0.134),
}

HAIR_LOCK_ENDS = {"Alsu": 0.94, "Naila": 0.90}


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens = []
    if "--" in __import__("sys").argv:
        tokens = __import__("sys").argv[__import__("sys").argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def material(name: str, color: tuple[float, float, float, float], roughness: float = 0.9) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    return result


def tag(obj: bpy.types.Object, asset_id: str, budget: int, lod_status: str = "LOD0") -> None:
    obj["urman_asset_id"] = asset_id
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["triangle_budget"] = max(budget, 128) if "_Face" in obj.name or "_Ear" in obj.name else budget
    obj["collision"] = "none"
    obj["lod_status"] = lod_status
    if obj.type == "MESH":
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) <= 4


def cube(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def faceted_prism(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    bottom_ratio: float = 1.0,
    top_ratio: float = 1.0,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 8,
) -> bpy.types.Object:
    """Create a restrained faceted taper for readable human proportions."""
    # Facial landmarks face -Y: a vertical cone presents a rectangular side
    # to the camera. Keep existing hair names/attachments; every low-poly hair
    # crown uses the same rounded UV-sphere primitive as other soft features.
    hair_cap = "_Hair_LOD" in name
    rounded = "_Face" in name or "_Ear" in name or "_HairBun" in name
    if "_FaceNose" in name:
        # A continuous bridge and a shallow tip, rather than a ball attached
        # to the face. Back vertices are embedded by seat_face_feature below.
        mesh = bpy.data.meshes.new(f"{name}Mesh")
        mesh.from_pydata(
            [(-.18, .20, .50), (.18, .20, .50),
             (-.44, .20, -.50), (.44, .20, -.50),
             (-.32, -.60, -.18), (.32, -.60, -.18),
             (0, -.34, .22)],
            [],
            [(6, 1, 0), (2, 4, 6, 0), (5, 3, 1, 6),
             (4, 5, 6), (2, 3, 5, 4), (0, 1, 3, 2)],
        )
        mesh.update()
        bpy.ops.object.select_all(action="DESELECT")
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        obj.location = location
    elif hair_cap:
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=12, ring_count=6, radius=1.0, location=location,
        )
    elif rounded:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1.0, location=location)
    else:
        bpy.ops.mesh.primitive_cone_add(
            vertices=vertices, radius1=bottom_ratio, radius2=top_ratio,
            depth=1.0, location=location,
        )
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler = rotation
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def faceted_head(
    name: str,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    width_scale: float = 1.0,
    depth_scale: float = 1.0,
) -> bpy.types.Object:
    """Create a restrained, human-scale low-poly head.

    Sixteen-sided rings keep a readable jaw, cheek and crown silhouette without the
    square volume of the old mannequin-like head. A shallow forward chin and
    cheek transition gives the face a plane that can carry the small neutral
    landmarks without making them read as stickers.
    """
    rings = (
        # The same sixteen-sided mesh gets a clearer jaw/cheek/temple rhythm:
        # a narrower chin and crown frame the softer cheeks without changing
        # the established head budget or its object/anchor contract.
        (-0.18, 0.100, 0.086, -0.030),
        (-0.135, 0.132, 0.116, -0.022),
        (-0.055, 0.178, 0.151, -0.010),
        (0.045, 0.172, 0.153, -0.001),
        (0.130, 0.146, 0.128, 0.008),
        (0.18, 0.110, 0.094, 0.016),
    )
    sides = 16
    vertices: list[tuple[float, float, float]] = []
    for ring_index, (z_offset, half_width, half_depth, center_y) in enumerate(rings):
        half_width *= width_scale
        half_depth *= depth_scale
        for side in range(sides):
            angle = (2.0 * math.pi * side / sides) + (math.pi / sides)
            irregular = 1.0 + 0.018 * math.sin((side + 1) * 2.3 + ring_index * 0.7)
            vertices.append(
                (
                    math.sin(angle) * half_width * irregular,
                    center_y + (-math.cos(angle) * half_depth * irregular),
                    z_offset,
                )
            )

    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(rings) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((lower + side, lower + next_side, upper + next_side, upper + side))
    faces.append(tuple(range((len(rings) - 1) * sides, len(rings) * sides)))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def head_front_y(head: bpy.types.Object, x: float, z: float) -> float:
    """Return the actual front surface at one local face landmark."""
    bpy.context.view_layer.update()
    bvh = BVHTree.FromPolygons(
        [vertex.co for vertex in head.data.vertices],
        [polygon.vertices[:] for polygon in head.data.polygons],
    )
    hit, _normal, _index, _distance = bvh.ray_cast(
        (x - head.location.x, -1.0, z - head.location.z),
        (0.0, 1.0, 0.0),
        2.0,
    )
    if hit is None or hit.y >= 0.0:
        raise RuntimeError(f"No front surface for {head.name} at ({x}, {z})")
    return hit.y + head.location.y


def seat_face_feature(head: bpy.types.Object, feature: bpy.types.Object) -> None:
    """Seat the actual rotated prism 2.5 mm into the faceted front."""
    bpy.context.view_layer.update()
    points = [feature.matrix_world @ vertex.co for vertex in feature.data.vertices]
    depths = [point.y - head_front_y(head, point.x, point.z) for point in points]
    feature.location.y += 0.0025 - max(depths)
    bpy.context.view_layer.update()
    points = [feature.matrix_world @ vertex.co for vertex in feature.data.vertices]
    depths = [point.y - head_front_y(head, point.x, point.z) for point in points]
    assert 0.002 <= max(depths) <= 0.003 and min(depths) < -0.003, feature.name


def faceted_torso(
    name: str,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    width_scale: float = 1.0,
    height_scale: float = 1.0,
    depth_scale: float = 1.0,
) -> bpy.types.Object:
    """Build a low-poly torso with a chest, waist and gently tapered hem."""
    prefix = name.split("_", 1)[0]
    bottom_lift = TORSO_BOTTOM_LIFTS.get(prefix, 0.04)
    rings = (
        # Keep the upper chest fixed while the lower coat line varies by role.
        (0.51 + bottom_lift, 0.198, 0.139, -0.004),
        (0.74 + bottom_lift * 0.55, 0.184, 0.136, -0.006),
        (0.98 + bottom_lift * 0.18, 0.205, 0.146, -0.004),
        (1.10, 0.242, 0.156, 0.000),
        (1.20, 0.252, 0.154, 0.003),
    )
    sides = 10
    vertices: list[tuple[float, float, float]] = []
    for ring_index, (z_offset, half_width, half_depth, center_y) in enumerate(rings):
        half_width *= width_scale
        half_depth *= depth_scale
        for side in range(sides):
            angle = (2.0 * math.pi * side / sides) + (math.pi / sides)
            irregular = 1.0 + 0.012 * math.sin((side + 2) * 2.1 + ring_index * 0.55)
            vertices.append(
                (
                    math.sin(angle) * half_width * irregular,
                    center_y + (-math.cos(angle) * half_depth * irregular),
                    z_offset * height_scale,
                )
            )

    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(rings) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((lower + side, lower + next_side, upper + next_side, upper + side))
    faces.append(tuple(range((len(rings) - 1) * sides, len(rings) * sides)))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def tapered_segment(
    name: str,
    points: tuple[tuple[float, float, float], ...],
    radii: tuple[float, ...],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    depth_scale: float = 1.0,
    sides: int = 7,
) -> bpy.types.Object:
    """Build one bent, tapered low-poly segment with a readable joint flow."""
    if len(points) != len(radii) or len(points) < 2:
        raise ValueError("tapered_segment needs matching point/radius pairs")
    ring_vertices: list[tuple[float, float, float]] = []
    for point_index, (point, radius) in enumerate(zip(points, radii, strict=True)):
        current = Vector(point)
        if point_index == 0:
            tangent = Vector(points[1]) - current
        elif point_index == len(points) - 1:
            tangent = current - Vector(points[point_index - 1])
        else:
            tangent = Vector(points[point_index + 1]) - Vector(points[point_index - 1])
        tangent.normalize()
        side_axis = tangent.cross(Vector((0.0, 1.0, 0.0)))
        if side_axis.length < 0.001:
            side_axis = tangent.cross(Vector((1.0, 0.0, 0.0)))
        side_axis.normalize()
        depth_axis = tangent.cross(side_axis)
        depth_axis.normalize()
        for side in range(sides):
            angle = (2.0 * math.pi * side / sides) + (math.pi / sides)
            irregular = 1.0 + 0.035 * math.sin((side + 1) * 1.71 + point_index * 0.83)
            offset = (
                side_axis * (math.cos(angle) * radius * irregular)
                + depth_axis * (math.sin(angle) * radius * depth_scale * irregular)
            )
            ring_vertices.append(tuple(current + offset))

    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(points) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((lower + side, lower + next_side, upper + next_side, upper + side))
    top = (len(points) - 1) * sides
    faces.append(tuple(top + side for side in range(sides)))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(ring_vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def foot_shape(
    name: str,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    """Use a low-poly toe/heel wedge instead of a rectangular boot block."""
    rings = (
        (0.00, 0.102, 0.082, -0.160, 0.105, 0.024),
        (0.085, 0.096, 0.075, -0.140, 0.095, 0.020),
    )
    vertices: list[tuple[float, float, float]] = []
    sides = 8
    for z_offset, toe_width, heel_width, toe_y, heel_y, corner in rings:
        vertices.extend(
            (
                (-toe_width + corner, toe_y, z_offset),
                (toe_width - corner, toe_y, z_offset),
                (toe_width, toe_y + corner, z_offset),
                (heel_width, heel_y - corner, z_offset),
                (heel_width - corner, heel_y, z_offset),
                (-heel_width + corner, heel_y, z_offset),
                (-heel_width, heel_y - corner, z_offset),
                (-toe_width, toe_y + corner, z_offset),
            )
        )
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides))), tuple(range(sides, sides * 2))]
    for side in range(sides):
        next_side = (side + 1) % sides
        faces.append((side, next_side, sides + next_side, sides + side))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def almond_face_feature(
    name: str,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    """Build a shallow convex almond on an existing face-feature anchor."""
    width, depth, height = size
    outline = (
        (-0.50, 0.00),
        (-0.30, 0.34),
        (0.00, 0.50),
        (0.30, 0.34),
        (0.50, 0.00),
        (0.30, -0.34),
        (0.00, -0.50),
        (-0.30, -0.34),
    )
    half_depth = depth * 0.5
    vertices = [
        (x * width, -half_depth, z * height)
        for x, z in outline
    ] + [
        (x * width, half_depth, z * height)
        for x, z in outline
    ]
    sides = len(outline)
    front_center = len(vertices)
    vertices.append((0.0, -depth * 0.78, 0.0))
    back_center = len(vertices)
    vertices.append((0.0, depth * 0.35, 0.0))
    faces: list[tuple[int, ...]] = []
    for side in range(sides):
        next_side = (side + 1) % sides
        faces.append((front_center, side, next_side))
        faces.append((back_center, sides + next_side, sides + side))
        faces.append((side, sides + side, sides + next_side, next_side))

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def faceted_eye_with_brow(
    name: str,
    eye_location: tuple[float, float, float],
    eye_size: tuple[float, float, float],
    brow_location: tuple[float, float, float],
    brow_size: tuple[float, float, float],
    surface: bpy.types.Material,
    skin_surface: bpy.types.Material,
    asset_id: str,
    head: bpy.types.Object,
) -> bpy.types.Object:
    """Build a small layered eye with a separate tapered brow and iris."""
    eye = almond_face_feature(
        name,
        eye_size,
        eye_location,
        surface,
        asset_id,
        96,
        rotation=(0.0, 0.0, math.radians(8.0)),
    )
    brow = almond_face_feature(
        name.replace("_FaceEye", "_FaceBrow"),
        brow_size,
        brow_location,
        surface,
        asset_id,
        48,
        rotation=(0.0, 0.0, math.radians(8.0) if "Left" in name else math.radians(-8.0)),
    )
    seat_face_feature(head, eye)
    seat_face_feature(head, brow)
    eyelid = faceted_prism(
        name.replace("_FaceEye", "_FaceEyelidUpper"),
        (eye_size[0] * 1.24, 0.010, eye_size[2] * 0.34),
        (eye_location[0], eye_location[1] - 0.001, eye_location[2] + eye_size[2] * 0.27),
        skin_surface,
        asset_id,
        64,
        bottom_ratio=0.88,
        top_ratio=0.98,
        rotation=(0.0, 0.0, math.radians(2.0) if "Left" in name else math.radians(-2.0)),
        vertices=10,
    )
    seat_face_feature(head, eyelid)
    iris = faceted_prism(
        name.replace("_FaceEye", "_FaceEyeIris"),
        (eye_size[0] * 0.42, 0.008, eye_size[2] * 0.58),
        (eye_location[0], eye_location[1] - 0.008, eye_location[2] - 0.001),
        surface,
        asset_id,
        64,
        bottom_ratio=0.84,
        top_ratio=0.96,
        rotation=(0.0, 0.0, math.radians(8.0)),
        vertices=10,
    )
    seat_face_feature(head, iris)
    # Seat the iris on its own eye-white surface. Independent head seating
    # can bury one iris when the cheek curvature differs between the eyes.
    iris.location.y = eye.location.y - eye_size[1] * 0.6 - 0.004
    iris.rotation_euler = (0.0, 0.0, 0.0)
    assert iris.location.y < eye.location.y - eye_size[1] * 0.5
    # Keep the established eye object name and leave the brow as its own
    # Head-bound mesh so the adapter can retain a warm eye white beneath the
    # dark brow/iris instead of flattening both into one material override.
    tag(eye, asset_id, 96)
    tag(brow, asset_id, 48)
    return eye


def sphere(
    name: str,
    radius: float,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    budget: int,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(surface)
    tag(obj, asset_id, budget)
    return obj


def hat(
    name: str,
    location: tuple[float, float, float],
    surface: bpy.types.Material,
    asset_id: str,
    style: str = "wool_cap",
) -> bpy.types.Object:
    profiles = {
        # A low, softly domed winter cap for the elder silhouettes.
        "wool_cap": (
            (-0.075, 0.22, 0.18),
            (-0.035, 0.235, 0.19),
            (0.030, 0.18, 0.145),
            (0.088, 0.09, 0.07),
        ),
        # Slightly narrower and taller so Timur reads as a distinct, neat
        # working cap while remaining culturally restrained.
        "prayer_cap": (
            (-0.060, 0.17, 0.145),
            (-0.025, 0.18, 0.15),
            (0.040, 0.13, 0.11),
            (0.095, 0.06, 0.05),
        ),
        "council_cap": (
            (-0.075, 0.24, 0.19),
            (-0.040, 0.25, 0.20),
            (0.028, 0.19, 0.155),
            (0.090, 0.08, 0.06),
        ),
    }
    rings = profiles.get(style, profiles["wool_cap"])
    sides = 10
    vertices: list[tuple[float, float, float]] = []
    for z_offset, radius_x, radius_y in rings:
        for side in range(sides):
            angle = (2.0 * math.pi * side / sides) + (math.pi / sides)
            vertices.append((math.cos(angle) * radius_x, math.sin(angle) * radius_y, z_offset))
    faces: list[tuple[int, ...]] = [tuple(reversed(range(sides)))]
    for ring_index in range(len(rings) - 1):
        lower = ring_index * sides
        upper = (ring_index + 1) * sides
        for side in range(sides):
            next_side = (side + 1) % sides
            faces.append((lower + side, lower + next_side, upper + next_side, upper + side))
    faces.append(tuple((len(rings) - 1) * sides + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, 300)
    return obj


def empty_anchor(name: str, location: tuple[float, float, float]) -> bpy.types.Object:
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=location)
    obj = bpy.context.object
    obj.name = name
    obj["urman_asset_id"] = "character.anchor"
    obj["license"] = "Project-original"
    obj["scale_meters"] = 1.0
    obj["collision"] = "none"
    obj["anchor_policy"] = "ground-origin"
    return obj


def create_character(
    prefix: str,
    coat_color: tuple[float, float, float, float],
    accent_color: tuple[float, float, float, float],
    has_hat: bool,
    has_beard: bool,
    origin_x: float,
    materials: dict[str, bpy.types.Material],
) -> None:
    asset_id = "character.fullgame.lowpoly.v1"
    x = origin_x
    z = 0.0
    height_scale, shoulder_scale, head_scale, stance, torso_scale = SILHOUETTE_PROFILES[prefix]
    shoulder_width, shoulder_depth, sleeve_scale, leg_scale, hem_width, hem_depth, cuff_scale = WARDROBE_PROFILES[prefix]
    body_bottom = 0.51 + TORSO_BOTTOM_LIFTS.get(prefix, 0.04)
    coat_hem_z = 0.16 + body_bottom * height_scale + 0.025
    arm_x = 0.29 * shoulder_scale
    leg_offset = 0.165 + stance * 0.55
    arm_left_rotation = 0.14 + stance * 0.75
    arm_right_rotation = -0.14 + stance * 0.75
    head_z = 1.57 * height_scale
    hair_z = head_z + 0.17
    body_top = 1.36 * height_scale
    head_bottom = head_z - 0.17
    neck_bottom = body_top - 0.015
    neck_top = head_bottom + 0.030
    neck_center = (neck_bottom + neck_top) * 0.5
    neck_height = neck_top - neck_bottom
    scarf_bottom = body_top - 0.010
    scarf_height = max(0.085, head_bottom - 0.035 - scarf_bottom)
    scarf_z = scarf_bottom + scarf_height * 0.5
    coat_surface = material(f"{prefix}Coat", coat_color)
    accent_surface = material(f"{prefix}Accent", accent_color)
    empty_anchor(f"{prefix}_Anchor", (x, 0.0, z))
    faceted_torso(
        f"{prefix}_Body_LOD0",
        (x, 0.0, 0.16),
        coat_surface,
        asset_id,
        512,
        width_scale=torso_scale,
        height_scale=(body_top - 0.16) / 1.20,
        depth_scale=0.98,
    )
    faceted_prism(
        f"{prefix}_ShoulderWrap_LOD0",
        (shoulder_width * shoulder_scale, shoulder_depth, 0.075),
        (x, -0.005, body_top - 0.055),
        coat_surface,
        asset_id,
        256,
        bottom_ratio=0.94,
        top_ratio=1.0,
        vertices=8,
    )
    tapered_segment(
        f"{prefix}_SleeveLeft_LOD0",
        (
            (x - arm_x * 0.82, -0.005, 1.29 * height_scale),
            (x - arm_x * 1.04, 0.000, 1.02 * height_scale),
            (x - arm_x * 0.94, -0.012, 0.78 * height_scale),
        ),
        (0.108 * shoulder_scale * sleeve_scale, 0.094 * shoulder_scale * sleeve_scale, 0.070 * shoulder_scale * sleeve_scale),
        coat_surface,
        asset_id,
        256,
        depth_scale=1.18,
        sides=7,
    )
    tapered_segment(
        f"{prefix}_SleeveRight_LOD0",
        (
            (x + arm_x * 0.82, -0.005, 1.29 * height_scale),
            (x + arm_x * 1.04, 0.000, 1.02 * height_scale),
            (x + arm_x * 0.94, -0.012, 0.78 * height_scale),
        ),
        (0.108 * shoulder_scale * sleeve_scale, 0.094 * shoulder_scale * sleeve_scale, 0.070 * shoulder_scale * sleeve_scale),
        coat_surface,
        asset_id,
        256,
        depth_scale=1.18,
        sides=7,
    )
    # A small accent cuff gives the sleeve a readable end at conversation
    # distance. The name retains the existing Godot accent-material dispatch.
    for side, side_x, side_rotation in (
        ("Left", x - arm_x, arm_left_rotation),
        ("Right", x + arm_x, arm_right_rotation),
    ):
        faceted_prism(
            f"{prefix}_ShoulderCuff{side}_LOD0",
            (0.155 * shoulder_scale * cuff_scale, 0.205 * cuff_scale, 0.060 * cuff_scale),
            (side_x, -0.005, 0.76 * height_scale),
            accent_surface,
            asset_id,
            128,
            bottom_ratio=0.92,
            top_ratio=1.0,
            vertices=8,
            rotation=(0.0, side_rotation, 0.0),
        )
    faceted_prism(
        f"{prefix}_CoatHem_LOD0",
        (hem_width * torso_scale, hem_depth, 0.065),
        (x, -0.005, coat_hem_z),
        coat_surface,
        asset_id,
        128,
        bottom_ratio=0.94,
        top_ratio=1.0,
        vertices=8,
    )
    if prefix == "Gulsina":
        faceted_prism(
            f"{prefix}_ApronFront_LOD0",
            (0.34, 0.035, 0.56),
            (x, -0.172, coat_hem_z + 0.30),
            accent_surface,
            asset_id,
            128,
            bottom_ratio=1.04,
            top_ratio=0.90,
            vertices=8,
        )
    elif prefix == "Naila":
        faceted_prism(
            f"{prefix}_CardiganPlacket_LOD0",
            (0.065, 0.034, 0.68),
            (x, -0.174, body_top - 0.28),
            accent_surface,
            asset_id,
            96,
            bottom_ratio=0.90,
            top_ratio=1.0,
            vertices=6,
        )
    trouser_surface = material(
        f"{prefix}Trousers",
        tuple(max(0.0, channel * 0.72) for channel in coat_color[:3]) + (1.0,),
    )
    tapered_segment(
        f"{prefix}_TrouserLeft_LOD0",
        (
            (x - leg_offset, 0.000, 0.88),
            (x - leg_offset * 0.96, -0.004, 0.57),
            (x - leg_offset * 0.94, -0.008, 0.30),
            (x - leg_offset * 0.94, -0.010, 0.095),
        ),
        (0.105 * leg_scale, 0.092 * leg_scale, 0.080 * leg_scale, 0.065 * leg_scale),
        trouser_surface,
        asset_id,
        256,
        depth_scale=1.22,
        sides=7,
    )
    tapered_segment(
        f"{prefix}_TrouserRight_LOD0",
        (
            (x + leg_offset, 0.000, 0.88),
            (x + leg_offset * 0.96, -0.004, 0.57),
            (x + leg_offset * 0.94, -0.008, 0.30),
            (x + leg_offset * 0.94, -0.010, 0.095),
        ),
        (0.105 * leg_scale, 0.092 * leg_scale, 0.080 * leg_scale, 0.065 * leg_scale),
        trouser_surface,
        asset_id,
        256,
        depth_scale=1.22,
        sides=7,
    )
    for side, side_x in (("Left", x - leg_offset), ("Right", x + leg_offset)):
        foot_shape(
            f"{prefix}_Boot{side}_LOD0",
            (side_x, -0.035, 0.010),
            materials["boot"],
            asset_id,
            128,
        )
    faceted_prism(
        f"{prefix}_Neck_LOD0",
        (0.15, 0.145, neck_height),
        (x, 0.0, neck_center),
        materials["skin"],
        asset_id,
        128,
        bottom_ratio=0.90,
        top_ratio=1.0,
        vertices=8,
    )
    head = faceted_head(
        f"{prefix}_Head_LOD0",
        (x, 0.0, head_z),
        materials["skin"],
        asset_id,
        256,
        width_scale=head_scale,
        depth_scale=head_scale,
    )
    hair_profiles = {
        # width, depth, height, vertical offset, cap rotation
        "Mansur": (0.33, 0.28, 0.13, 0.014, -8.0),
        "Gulsina": (0.37, 0.29, 0.13, 0.026, 6.0),
        # Rounded, head-seated crown: the lower half overlaps the existing
        # crown slightly instead of rising as a tall tapered cap.
        "Alsu": (0.34, 0.27, 0.12, 0.005, -2.0),
        "TimurHazrat": (0.29, 0.24, 0.12, 0.020, 4.0),
        "CouncilElder": (0.35, 0.28, 0.13, 0.016, -10.0),
        "CouncilWitness": (0.30, 0.23, 0.10, 0.012, 0.0),
        "Naila": (0.34, 0.24, 0.12, 0.028, 8.0),
        "ArchiveClerk": (0.31, 0.24, 0.11, 0.012, -4.0),
        "PactKeeper": (0.34, 0.27, 0.13, 0.018, -6.0),
    }
    hair_width, hair_depth, hair_height, hair_offset, hair_rotation = hair_profiles[prefix]
    faceted_prism(
        f"{prefix}_Hair_LOD0",
        (hair_width * head_scale, hair_depth * head_scale, hair_height),
        (x, -0.010, hair_z + hair_offset),
        materials["hair"],
        asset_id,
        256,
        bottom_ratio=0.98,
        top_ratio=0.62,
        rotation=(0.0, 0.0, math.radians(hair_rotation)),
        vertices=10,
    )
    if prefix in {"Gulsina", "Naila"}:
        if prefix == "Naila":
            bun_size = (0.19 * head_scale, 0.16 * head_scale, 0.14)
            bun_location = (x + 0.13 * head_scale, 0.070, hair_z + 0.040)
        else:
            bun_size = (0.20 * head_scale, 0.20 * head_scale, 0.16)
            bun_location = (x, 0.105, hair_z - 0.004)
        faceted_prism(
            f"{prefix}_HairBun_LOD0",
            bun_size,
            bun_location,
            materials["hair"],
            asset_id,
            128,
            bottom_ratio=0.90,
            top_ratio=0.68,
            vertices=8,
        )
    if prefix in {"Alsu", "Naila"}:
        lock_end = HAIR_LOCK_ENDS[prefix] * height_scale
        for side, side_sign in (("Left", -1.0), ("Right", 1.0)):
            tapered_segment(
                f"{prefix}_HairLock{side}_LOD0",
                (
                    (x + side_sign * 0.12 * head_scale, -0.010, head_z + 0.130),
                    (x + side_sign * 0.18 * head_scale, -0.028, head_z - 0.085),
                    (x + side_sign * 0.20 * head_scale, 0.015, lock_end),
                ),
                (0.065 * head_scale, 0.055 * head_scale, 0.018 * head_scale),
                materials["hair"],
                asset_id,
                128,
                depth_scale=0.60,
                sides=7,
            )
    faceted_prism(
        f"{prefix}_ScarfBand_LOD0",
        (0.29 * shoulder_scale, 0.24, scarf_height),
        (x, -0.015, scarf_z),
        material(f"{prefix}Scarf", accent_color),
        asset_id,
        128,
        bottom_ratio=0.94,
        top_ratio=1.0,
        vertices=8,
    )
    for side, side_x in (("Left", x - 0.165 * head_scale), ("Right", x + 0.165 * head_scale)):
        faceted_prism(
            f"{prefix}_Ear{side}_LOD0",
            (0.050, 0.060, 0.095),
            (side_x, -0.005, head_z - 0.005),
            materials["skin"],
            asset_id,
            96,
            bottom_ratio=0.86,
            top_ratio=0.96,
            vertices=6,
        )
    # Keep the established HeadHand names so the Godot adapter can route hands
    # to skin before the generic head mapping and the rig can bind them to arms.
    for side, side_x in (("Left", x - arm_x), ("Right", x + arm_x)):
        side_sign = -1.0 if side == "Left" else 1.0
        tapered_segment(
            f"{prefix}_HeadHand{side}_LOD0",
            (
                (side_x, -0.018, 0.75 * height_scale),
                (side_x + side_sign * 0.012, -0.032, 0.66 * height_scale),
                (side_x + side_sign * 0.024, -0.052, 0.56 * height_scale),
            ),
            (0.073 * shoulder_scale, 0.064 * shoulder_scale, 0.053 * shoulder_scale),
            materials["skin"],
            asset_id,
            128,
            depth_scale=1.08,
            sides=7,
        )
        tapered_segment(
            f"{prefix}_HeadHandThumb{side}_LOD0",
            (
                (side_x + side_sign * 0.024, -0.052, 0.60 * height_scale),
                (side_x + side_sign * 0.066, -0.076, 0.57 * height_scale),
            ),
            (0.037 * shoulder_scale, 0.023 * shoulder_scale),
            materials["skin"],
            asset_id,
            64,
            depth_scale=1.06,
            sides=7,
        )
    face_z = head_z
    face_y = -0.157 * head_scale
    eye_spread_ratio, eye_width, eye_height, brow_lift, nose_scale, mouth_width, mouth_height, mouth_drop = FACE_PROFILES[prefix]
    eye_spread = eye_spread_ratio * head_scale
    faceted_eye_with_brow(
        f"{prefix}_FaceEyeLeft_LOD0",
        (x - eye_spread, face_y - 0.003, face_z + 0.035),
        (eye_width * head_scale, 0.012, eye_height * head_scale),
        (x - eye_spread, face_y + 0.006, face_z + brow_lift),
        (min(0.058, eye_width * 0.70) * head_scale, 0.006, max(0.007, eye_height * 0.20) * head_scale),
        materials["eye"],
        materials["skin"],
        asset_id,
        head,
    )
    faceted_eye_with_brow(
        f"{prefix}_FaceEyeRight_LOD0",
        (x + eye_spread, face_y - 0.003, face_z + 0.035),
        (eye_width * head_scale, 0.012, eye_height * head_scale),
        (x + eye_spread, face_y + 0.006, face_z + brow_lift),
        (min(0.058, eye_width * 0.70) * head_scale, 0.006, max(0.007, eye_height * 0.20) * head_scale),
        materials["eye"],
        materials["skin"],
        asset_id,
        head,
    )
    nose = faceted_prism(
        f"{prefix}_FaceNose_LOD0",
        (0.052 * nose_scale, 0.040 * nose_scale, 0.064 * nose_scale),
        (x, face_y - 0.008, face_z - 0.017),
        materials["skin"],
        asset_id,
        96,
        bottom_ratio=0.76,
        top_ratio=0.94,
        vertices=10,
    )
    seat_face_feature(head, nose)
    mouth = almond_face_feature(
        f"{prefix}_FaceMouth_LOD0",
        (mouth_width * head_scale, 0.010, mouth_height * head_scale),
        (x, face_y - 0.014, face_z - mouth_drop),
        materials["eye"],
        asset_id,
        96,
        rotation=(0.0, 0.0, math.radians(4.0)),
    )
    seat_face_feature(head, mouth)
    lower_lip = faceted_prism(
        f"{prefix}_FaceMouthLowerLip_LOD0",
        (0.030, 0.006, 0.009),
        (x, face_y - 0.012, face_z - 0.092),
        materials["skin"],
        asset_id,
        64,
        bottom_ratio=0.88,
        top_ratio=0.98,
        vertices=8,
    )
    seat_face_feature(head, lower_lip)
    chin = faceted_prism(
        f"{prefix}_FaceChin_LOD0",
        (0.084 * head_scale, 0.010, 0.028),
        (x, face_y - 0.006, face_z - 0.116),
        materials["skin"],
        asset_id,
        64,
        bottom_ratio=0.80,
        top_ratio=0.96,
        rotation=(0.0, 0.0, math.radians(22.5)),
        vertices=10,
    )
    seat_face_feature(head, chin)
    if has_beard:
        beard_width, beard_depth, beard_height, beard_drop = BEARD_PROFILES[prefix]
        beard = faceted_prism(
            f"{prefix}_FaceBeard_LOD0",
            (beard_width * head_scale, beard_depth, beard_height * head_scale),
            (x, face_y - 0.006, face_z - beard_drop),
            materials["hair"],
            asset_id,
            192,
            bottom_ratio=0.72,
            top_ratio=0.96,
            rotation=(0.0, 0.0, math.radians(22.5)),
            vertices=10,
        )
        seat_face_feature(head, beard)
    if has_hat:
        hat_style = {
            "Mansur": "wool_cap",
            "TimurHazrat": "prayer_cap",
            "CouncilElder": "council_cap",
            "PactKeeper": "council_cap",
        }.get(prefix, "wool_cap")
        hat(f"{prefix}_Hat_LOD0", (x, 0.0, head_z + 0.22 * head_scale), materials["hair"], asset_id, style=hat_style)


def _bone_for_mesh(name: str) -> str:
    """Map a generated mesh to the smallest useful presentation bone."""
    if "HandLeft" in name or "ShoulderCuffLeft" in name:
        return "Arm.L"
    if "HandRight" in name or "ShoulderCuffRight" in name:
        return "Arm.R"
    if any(token in name for token in ("Head", "Hair", "Face", "Hat", "Ear")):
        return "Head"
    if "SleeveLeft" in name:
        return "Arm.L"
    if "SleeveRight" in name:
        return "Arm.R"
    if "TrouserLeft" in name or "BootLeft" in name:
        return "Leg.L"
    if "TrouserRight" in name or "BootRight" in name:
        return "Leg.R"
    return "Spine"


def _make_action(
    armature: bpy.types.Object,
    prefix: str,
    name: str,
    frames: tuple[int, ...],
    poses: dict[str, tuple[tuple[float, float, float], ...]],
) -> bpy.types.Action:
    action = bpy.data.actions.new(f"{prefix}_{name}")
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    for bone_name, rotations in poses.items():
        bone = armature.pose.bones[bone_name]
        bone.rotation_mode = "XYZ"
        for frame, rotation in zip(frames, rotations, strict=True):
            bone.rotation_euler = rotation
            bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone_name)

    for curve in action.fcurves:
        curve.modifiers.new(type="CYCLES")
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
    return action


def add_animation_rig(prefix: str, origin_x: float) -> bpy.types.Object:
    """Add a lightweight bone rig and two authored presentation clips.

    The generated meshes remain separate low-poly pieces so LOD provenance is
    inspectable. Parenting them to bones preserves that ownership while making
    the GLB usable by Godot's Skeleton3D/AnimationPlayer importer.
    """
    bpy.ops.object.armature_add(enter_editmode=True, location=(origin_x, 0.0, 0.0))
    armature = bpy.context.object
    armature.name = f"{prefix}_Rig"
    armature.data.name = f"{prefix}_RigData"
    armature.data.display_type = "BBONE"
    armature.data["urman_asset_id"] = "character.fullgame.rig.v1"
    armature.data["license"] = "Project-original"
    armature.data["collision"] = "none"
    armature.data["animation_clips"] = "Idle,Tension"

    edit_bones = armature.data.edit_bones
    root = edit_bones.new("Root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.0, 0.12)
    spine = edit_bones.new("Spine")
    spine.head = (0.0, 0.0, 0.82)
    spine.tail = (0.0, 0.0, 1.40)
    spine.parent = root
    head = edit_bones.new("Head")
    head.head = (0.0, 0.0, 1.40)
    head.tail = (0.0, 0.0, 1.95)
    head.parent = spine
    arm_l = edit_bones.new("Arm.L")
    arm_l.head = (-0.28, 0.0, 1.29)
    arm_l.tail = (-0.32, 0.0, 0.76)
    arm_l.parent = spine
    arm_r = edit_bones.new("Arm.R")
    arm_r.head = (0.28, 0.0, 1.29)
    arm_r.tail = (0.32, 0.0, 0.76)
    arm_r.parent = spine
    leg_l = edit_bones.new("Leg.L")
    leg_l.head = (-0.16, 0.0, 0.82)
    leg_l.tail = (-0.16, 0.0, 0.04)
    leg_l.parent = root
    leg_r = edit_bones.new("Leg.R")
    leg_r.head = (0.16, 0.0, 0.82)
    leg_r.tail = (0.16, 0.0, 0.04)
    leg_r.parent = root
    bpy.ops.object.mode_set(mode="OBJECT")

    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or not obj.name.startswith(f"{prefix}_"):
            continue
        world_matrix = obj.matrix_world.copy()
        obj.parent = armature
        obj.parent_type = "BONE"
        obj.parent_bone = _bone_for_mesh(obj.name)
        obj.matrix_world = world_matrix

    frames = (1, 20, 40)
    _make_action(
        armature,
        prefix,
        "Idle",
        frames,
        {
            "Spine": ((0.0, -0.012, 0.0), (0.0, 0.016, 0.0), (0.0, -0.012, 0.0)),
            "Head": ((0.0, 0.008, 0.0), (0.0, -0.012, 0.0), (0.0, 0.008, 0.0)),
            "Arm.L": ((0.0, 0.0, -0.018), (0.0, 0.0, 0.012), (0.0, 0.0, -0.018)),
            "Arm.R": ((0.0, 0.0, 0.018), (0.0, 0.0, -0.012), (0.0, 0.0, 0.018)),
        },
    )
    tension = _make_action(
        armature,
        prefix,
        "Tension",
        frames,
        {
            "Spine": ((0.0, -0.025, 0.0), (0.0, 0.035, 0.0), (0.0, -0.025, 0.0)),
            "Head": ((0.025, 0.016, 0.0), (-0.035, -0.022, 0.0), (0.025, 0.016, 0.0)),
            "Arm.L": ((0.0, 0.0, -0.05), (0.0, 0.0, 0.028), (0.0, 0.0, -0.05)),
            "Arm.R": ((0.0, 0.0, 0.05), (0.0, 0.0, -0.028), (0.0, 0.0, 0.05)),
        },
    )
    # Blender's glTF exporter emits the active action for every armature, but
    # only discovers additional actions on a multi-armature scene through NLA
    # tracks. Keep Tension as a single-strip, non-muted track so each imported
    # character carries both presentation clips without relying on the
    # single-armature export shortcut.
    tension_track = armature.animation_data.nla_tracks.new()
    tension_track.name = f"{prefix}_Tension"
    tension_strip = tension_track.strips.new(f"{prefix}_Tension", 1, tension)
    tension_strip.action_frame_start = 1
    tension_strip.action_frame_end = 40
    tension_strip.frame_start = 1
    tension_strip.frame_end = 40
    armature.animation_data.action = bpy.data.actions.get(f"{prefix}_Idle")
    armature["urman_asset_id"] = "character.fullgame.rig.v1"
    armature["license"] = "Project-original"
    armature["scale_meters"] = 1.0
    armature["collision"] = "none"
    armature["animation_policy"] = "authored Idle/Tension clips; final expression and performance review remains open"
    return armature


def generate_lod1_variants() -> int:
    created = 0
    for source in [obj for obj in list(bpy.context.scene.objects) if obj.type == "MESH" and obj.name.endswith("_LOD0")]:
        lod = source.copy()
        lod.data = source.data.copy()
        lod.name = source.name.replace("_LOD0", "_LOD1")
        lod["urman_asset_id"] = source.get("urman_asset_id", "character.fullgame.lowpoly.v1")
        lod["license"] = source.get("license", "Project-original")
        lod["scale_meters"] = source.get("scale_meters", 1.0)
        lod["triangle_budget"] = max(12, round(int(source.get("triangle_budget", 256)) * 0.5))
        lod["collision"] = "none"
        lod["lod_status"] = f"LOD1 generated from {source.name}; ratio=0.50"
        lod["lod_source"] = source.name
        bpy.context.collection.objects.link(lod)

        bpy.context.view_layer.objects.active = lod
        lod.select_set(True)
        modifier = lod.modifiers.new("URMAN_CHARACTER_LOD1_Decimate", "DECIMATE")
        modifier.ratio = 0.5
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        lod.select_set(False)
        created += 1
    return created


def main() -> None:
    args = arguments()
    root = Path(args.root).resolve()
    blend_path = root / "assets" / "source" / "blender" / "urman_character_kit.blend"
    glb_path = root / "game" / "assets" / "generated" / "urman_character_kit.glb"
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(data):
            if block.users == 0:
                data.remove(block)

    materials = {
        "skin": material("StylizedSkin", (0.57, 0.39, 0.31, 1.0)),
        "hair": material("StylizedHair", (0.13, 0.10, 0.085, 1.0)),
        "eye": material("StylizedFaceInk", (0.10, 0.075, 0.065, 1.0)),
        "boot": material("StylizedBoot", (0.12, 0.09, 0.07, 1.0)),
    }
    for index, (prefix, coat, accent, has_hat, has_beard) in enumerate(CHARACTERS):
        create_character(prefix, coat, accent, has_hat, has_beard, index * 2.4, materials)

    lod_count = generate_lod1_variants()
    rigs = [add_animation_rig(prefix, index * 2.4) for index, (prefix, *_rest) in enumerate(CHARACTERS)]
    bpy.context.scene["generator"] = "tools/blender/generate_character_kit.py"
    bpy.context.scene["blender_version_lock"] = "4.5 LTS"
    bpy.context.scene["units"] = "meters"
    bpy.context.scene["character_prefixes"] = ",".join(prefix for prefix, *_ in CHARACTERS)
    bpy.context.scene["lod_policy"] = "LOD1 generated with deterministic Decimate ratio=0.50; Godot ranges are scene-specific"
    bpy.context.scene["lod1_mesh_count"] = lod_count
    bpy.context.scene["lod0_mesh_count"] = len([obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD0")])
    bpy.context.scene["detail_policy"] = "project-original painterly low-poly tapered torsos, necks, ears, face landmarks, hands, layered clothing and restrained stance variants; faceted sleeves, trousers, boots and role-neutral apron/placket details; authored Idle/Tension rigs; final face/expression and cultural review remains open"
    bpy.context.scene["animation_policy"] = "nine project-original armatures with Idle/Tension clips; Godot may select clips per presentation state"
    bpy.context.scene["armature_count"] = len(rigs)
    bpy.context.scene["collision_policy"] = "no collision meshes; Godot interaction targets and zone colliders own physics"
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_apply=True,
        export_materials="EXPORT",
        export_yup=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_force_sampling=True,
    )
    print(f"URMAN character kit: {blend_path}")
    print(f"URMAN character kit: {glb_path}")
    print(f"URMAN character LOD1 variants: {lod_count}")


if __name__ == "__main__":
    main()
