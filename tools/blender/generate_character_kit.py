"""Generate the low-poly character kit with original clothing and CC0 head derivatives for URMAN.

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


CHARACTERS = (
    ("Mansur", (0.36, 0.25, 0.18, 1.0), (0.58, 0.42, 0.24, 1.0), True, True),
    ("Gulsina", (0.38, 0.28, 0.34, 1.0), (0.63, 0.45, 0.28, 1.0), True, False),
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
    "Alsu": 0.075,
    "TimurHazrat": -0.005,
    "CouncilElder": -0.030,
    "CouncilWitness": 0.015,
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
    """Build a low-poly torso with a chest, waist, and integrated shoulder slope."""
    prefix = name.split("_", 1)[0]
    bottom_lift = TORSO_BOTTOM_LIFTS.get(prefix, 0.04)
    rings = (
        # A fuller lower coat and a shallow waist taper keep the hem from reading
        # as a narrow box while the upper rings overlap the shoulder yoke.
        (0.51 + bottom_lift, 0.212, 0.146, -0.010),
        (0.74 + bottom_lift * 0.55, 0.206, 0.154, -0.009),
        (0.98 + bottom_lift * 0.18, 0.222, 0.162, -0.006),
        # Keep the shoulder crest slightly inside the sleeve volume; the
        # existing four-ring sleeve still overlaps this contour.
        (1.10, 0.222, 0.152, -0.001),
        # Shoulder crest, then a real trapezius slope into a neck. The crest used
        # to run straight into a 0.31 m top ring, so the head sat on the
        # shoulders with no neck at all and every figure read as a bell.
        (1.14, 0.228, 0.154, 0.001),
        (1.165, 0.152, 0.116, 0.002),
        (1.19, 0.092, 0.078, 0.003),
        (1.20, 0.078, 0.066, 0.003),
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
            (-0.075, 0.16, 0.145),
            (-0.035, 0.175, 0.155),
            (0.018, 0.165, 0.148),
            (0.060, 0.125, 0.112),
            (0.083, 0.065, 0.057),
        ),
        # A soft household head wrap follows the existing cap topology.
        "head_wrap": (
            (-0.065, 0.148, 0.142),
            (-0.032, 0.164, 0.154),
            (0.008, 0.160, 0.150),
            (0.043, 0.125, 0.116),
            (0.068, 0.062, 0.058),
        ),
        # Slightly narrower and taller so Timur reads as a distinct, neat
        # working cap while remaining culturally restrained.
        "prayer_cap": (
            (-0.050, 0.145, 0.13),
            (0.015, 0.15, 0.13),
            (0.065, 0.125, 0.11),
            (0.078, 0.08, 0.07),
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
    if style == "head_wrap":
        # The back of the wrap is one shallow folded cloth panel in this same
        # Hat mesh. Blender front is -Y, so the panel sits behind the head;
        # its closed thickness keeps both sides visible without z-fighting.
        panel = len(vertices)
        shell = []
        for z, rx, ry in ((-0.020, 0.145, 0.155), (-0.105, 0.140, 0.265), (-0.230, 0.080, 0.205)):
            for step in range(7):
                angle = math.pi * step / 6
                shell.append((rx * math.cos(angle), 0.01 + ry * math.sin(angle), z))
        vertices.extend(shell)
        vertices.extend((x, y + 0.009, z) for x, y, z in shell)
        n = len(shell)
        for ring in range(2):
            for step in range(6):
                a = panel + ring * 7 + step
                b, c, d = a + 1, a + 8, a + 7
                faces.extend(((a, b, c, d), (d + n, c + n, b + n, a + n)))
            a, d = panel + ring * 7, panel + (ring + 1) * 7
            faces.append((a, d, d + n, a + n))
            b, c = a + 6, d + 6
            faces.append((c, b, b + n, c + n))
        for step in range(6):
            a, b = panel + step, panel + step + 1
            faces.append((b, a, a + n, b + n))
            d, c = panel + 14 + step, panel + 15 + step
            faces.append((d, c, c + n, d + n))
        # A small faceted knot and two short folded ends complete the wrap
        # silhouette while staying inside the established Hat object/budget.
        knot = len(vertices)
        vertices.extend((
            (-0.034, 0.257, -0.079),
            (0.000, 0.287, -0.096),
            (0.034, 0.257, -0.079),
            (0.000, 0.257, -0.120),
            (0.000, 0.232, -0.096),
            (0.000, 0.257, -0.060),
        ))
        faces.extend((
            (knot + 1, knot + 0, knot + 5),
            (knot + 2, knot + 1, knot + 5),
            (knot + 4, knot + 2, knot + 5),
            (knot + 0, knot + 4, knot + 5),
            (knot + 0, knot + 1, knot + 3),
            (knot + 1, knot + 2, knot + 3),
            (knot + 2, knot + 4, knot + 3),
            (knot + 4, knot + 0, knot + 3),
        ))
        for side_sign in (-1.0, 1.0):
            tail = len(vertices)
            x0 = 0.032 * side_sign
            x1 = 0.058 * side_sign
            tip = 0.050 * side_sign
            vertices.extend((
                (x0, 0.260, -0.105),
                (x1, 0.260, -0.092),
                (tip, 0.248, -0.166),
                (x0, 0.272, -0.105),
                (x1, 0.272, -0.092),
                (tip, 0.260, -0.166),
            ))
            faces.extend((
                (tail + 0, tail + 1, tail + 2),
                (tail + 5, tail + 4, tail + 3),
                (tail + 0, tail + 3, tail + 4, tail + 1),
                (tail + 1, tail + 4, tail + 5, tail + 2),
                (tail + 2, tail + 5, tail + 3, tail + 0),
            ))
            if side_sign > 0:
                faces[-5:] = [tuple(reversed(face)) for face in faces[-5:]]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(surface)
    tag(obj, asset_id, 300)
    return obj


def face_features(
    prefix: str,
    head_obj,
    head_scale: float,
    materials: dict,
    asset_id: str,
) -> None:
    """Add readable facial features to the CC0 base head.

    The licensed base head has a flat front plane with recessed eye sockets
    and no nose or mouth, so at conversational distance it reads as a blank
    mask. Each feature is parented to the copied head object, so it inherits
    the head's exact transform (Blender front is -Y); names follow the
    vocabulary the Godot dressing already colours (FaceEyeWhite, FaceEyeIris,
    FaceNose*, FaceMouth*, Ear*), which keeps this pass to one file.
    """
    skin = materials["skin"]
    eye_white = material(f"{prefix}EyeWhite", (0.87, 0.85, 0.80, 1.0), 0.55)
    iris = material(f"{prefix}Iris", (0.17, 0.12, 0.09, 1.0), 0.45)
    mouth = material(f"{prefix}FaceMouth", (0.34, 0.17, 0.15, 1.0), 0.7)

    def put(name: str, size: tuple[float, float, float],
            local: tuple[float, float, float], surface, budget: int = 24) -> None:
        obj = cube(
            f"{prefix}_{name}_LOD0",
            tuple(value * head_scale for value in size),
            (0.0, 0.0, 0.0),
            surface,
            asset_id,
            budget,
        )
        obj.parent = head_obj
        obj.matrix_parent_inverse = head_obj.matrix_world.inverted()
        obj.location = tuple(value * head_scale for value in local)

    # Head-local anchors measured from the CC0 templates: face plane y=-0.161,
    # eyes recessed at y=-0.106, sockets open between them.
    for side, sign in (("L", -1.0), ("R", 1.0)):
        put(f"FaceEyeWhite{side}", (0.030, 0.016, 0.018),
            (sign * 0.036, -0.133, 0.026), eye_white, 12)
        put(f"FaceEyeIris{side}", (0.014, 0.012, 0.014),
            (sign * 0.036, -0.142, 0.024), iris, 12)
    put("FaceNoseBridge", (0.022, 0.018, 0.048),
        (0.0, -0.160, 0.010), skin, 24)
    put("FaceNoseTip", (0.028, 0.022, 0.024),
        (0.0, -0.169, -0.018), skin, 24)
    put("FaceMouthLine", (0.044, 0.010, 0.010),
        (0.0, -0.166, -0.050), mouth, 12)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        put(f"Ear{side}", (0.012, 0.030, 0.038),
            (sign * 0.120, -0.006, 0.004), skin, 24)


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
    head_templates: dict[str, bpy.types.Object],
) -> None:
    asset_id = "character.fullgame.lowpoly.v1"
    x = origin_x
    z = 0.0
    height_scale, shoulder_scale, head_scale, stance, torso_scale = SILHOUETTE_PROFILES[prefix]
    shoulder_width, shoulder_depth, sleeve_scale, leg_scale, hem_width, hem_depth, cuff_scale = WARDROBE_PROFILES[prefix]
    body_bottom = 0.51 + TORSO_BOTTOM_LIFTS.get(prefix, 0.04)
    coat_hem_z = 0.16 + body_bottom * height_scale + 0.025
    # N2: the arm carriage follows the human shoulder line. The old 0.29
    # factor put the sleeve centres ~0.63-0.85 m apart once the sleeve
    # radius was added - a coat rack, not a person.
    arm_x = 0.24 * shoulder_scale
    leg_offset = 0.165 + stance * 0.55
    arm_left_rotation = 0.14 + stance * 0.75
    arm_right_rotation = -0.14 + stance * 0.75
    head_z = 1.57 * height_scale
    body_top = 1.36 * height_scale
    if prefix == "Gulsina":
        # Blender front is -Y. Keep both shoulders fixed, then bend the
        # elbows outward before bringing the forearms forward and inward to
        # the towel. The towel sits just in front of the existing apron.
        towel_y = -0.218
        towel_z = coat_hem_z + 0.060
        wrist_y = towel_y + 0.068
        wrist_z = towel_z + 0.028
        sleeve_left_points = (
            (x - arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x - arm_x * 0.84, -0.034, 1.12 * height_scale),
            (x - arm_x * 0.70, -0.105, 0.98 * height_scale),
            (x - arm_x * 0.45, wrist_y, wrist_z),
        )
        sleeve_right_points = (
            (x + arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x + arm_x * 0.84, -0.034, 1.12 * height_scale),
            (x + arm_x * 0.70, -0.105, 0.98 * height_scale),
            (x + arm_x * 0.45, wrist_y, wrist_z),
        )
        # faceted_prism rotates around Y, so align its thin Z axis with the
        # elbow-to-wrist X/Z projection; the -Y depth remains front-facing.
        gulsina_cuff_angle = math.atan2(
            arm_x * 0.25,
            wrist_z - 0.98 * height_scale,
        )
    elif prefix == "Mansur":
        # Keep Mansur's shoulders and ground anchor unchanged, but bring both
        # forearms forward and together over the lower coat. This is a quiet
        # elder-at-rest silhouette for the house conversation; it uses the
        # existing Arm.L/Arm.R parentage and does not add a prop or bone.
        mansur_hand_y = -0.190
        mansur_hand_z = 0.79 * height_scale
        mansur_cuff_angle = math.atan2(arm_x * .76 - .085, mansur_hand_z - 1.02 * height_scale)
        sleeve_left_points = (
            (x - arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x - arm_x * 0.86, -0.025, 1.16 * height_scale),
            (x - arm_x * 0.76, -0.095, 1.02 * height_scale),
            (x - 0.085, mansur_hand_y, mansur_hand_z),
        )
        sleeve_right_points = (
            (x + arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x + arm_x * 0.86, -0.025, 1.16 * height_scale),
            (x + arm_x * 0.76, -0.095, 1.02 * height_scale),
            (x + 0.085, mansur_hand_y, mansur_hand_z),
        )
    elif prefix == "Alsu":
        # Keep the guide's shoulders and anchor fixed, but fold her forearms
        # together in front of the coat for a restrained cold-weather stance.
        # This reuses the existing Arm.L/Arm.R mesh ownership; no prop or bone
        # is introduced, and the existing Idle/Tension clips remain valid.
        alsu_hand_y = -0.188
        alsu_hand_z = 0.94 * height_scale
        alsu_cuff_angle = math.atan2(
            arm_x * 0.76 - 0.062,
            alsu_hand_z - 1.02 * height_scale,
        )
        sleeve_left_points = (
            (x - arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x - arm_x * 0.86, -0.025, 1.16 * height_scale),
            (x - arm_x * 0.76, -0.090, 1.02 * height_scale),
            (x - 0.062, alsu_hand_y, alsu_hand_z),
        )
        sleeve_right_points = (
            (x + arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x + arm_x * 0.86, -0.025, 1.16 * height_scale),
            (x + arm_x * 0.76, -0.090, 1.02 * height_scale),
            (x + 0.062, alsu_hand_y, alsu_hand_z),
        )
    else:
        sleeve_left_points = (
            (x - arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x - arm_x * 0.86, -0.004, 1.20 * height_scale),
            (x - arm_x * 1.00, -0.002, 1.04 * height_scale),
            (x - arm_x * 0.92, -0.012, 0.77 * height_scale),
        )
        sleeve_right_points = (
            (x + arm_x * 0.68, -0.010, 1.30 * height_scale),
            (x + arm_x * 0.86, -0.004, 1.20 * height_scale),
            (x + arm_x * 1.00, -0.002, 1.04 * height_scale),
            (x + arm_x * 0.92, -0.012, 0.77 * height_scale),
        )
    # The imported head includes the existing neck down to about -0.21;
    # use that boundary when fitting the smaller winter collar.
    head_bottom = head_z - 0.21
    scarf_bottom = body_top - 0.010
    scarf_height = max(0.065, head_bottom - 0.020 - scarf_bottom)
    scarf_z = scarf_bottom + scarf_height * 0.5
    coat_surface = material(f"{prefix}Coat", coat_color)
    accent_surface = material(f"{prefix}Accent", accent_color)
    empty_anchor(f"{prefix}_Anchor", (x, 0.0, z))
    torso = faceted_torso(
        f"{prefix}_Body_LOD0",
        (x, 0.0, 0.16),
        coat_surface,
        asset_id,
        512,
        width_scale=torso_scale,
        height_scale=(body_top - 0.16) / 1.20,
        depth_scale=0.98,
    )
    tapered_segment(
        f"{prefix}_SleeveLeft_LOD0",
        sleeve_left_points,
        (0.044 * shoulder_scale * sleeve_scale, 0.078 * shoulder_scale * sleeve_scale, 0.071 * shoulder_scale * sleeve_scale, 0.056 * shoulder_scale * sleeve_scale),
        coat_surface,
        asset_id,
        256,
        depth_scale=1.18,
        sides=7,
    )
    tapered_segment(
        f"{prefix}_SleeveRight_LOD0",
        sleeve_right_points,
        (0.044 * shoulder_scale * sleeve_scale, 0.078 * shoulder_scale * sleeve_scale, 0.071 * shoulder_scale * sleeve_scale, 0.056 * shoulder_scale * sleeve_scale),
        coat_surface,
        asset_id,
        256,
        depth_scale=1.18,
        sides=7,
    )
    # A small accent cuff gives the sleeve a readable end at conversation
    # distance. The name retains the existing Godot accent-material dispatch.
    if prefix == "Gulsina":
        cuff_specs = (
            ("Left", x - arm_x * 0.45, wrist_y, wrist_z, gulsina_cuff_angle),
            ("Right", x + arm_x * 0.45, wrist_y, wrist_z, -gulsina_cuff_angle),
        )
    elif prefix == "Mansur":
        cuff_specs = (
            ("Left", x - 0.085, mansur_hand_y, mansur_hand_z, mansur_cuff_angle),
            ("Right", x + 0.085, mansur_hand_y, mansur_hand_z, -mansur_cuff_angle),
        )
    elif prefix == "Alsu":
        cuff_specs = (
            ("Left", x - 0.062, alsu_hand_y, alsu_hand_z, alsu_cuff_angle),
            ("Right", x + 0.062, alsu_hand_y, alsu_hand_z, -alsu_cuff_angle),
        )
    else:
        cuff_specs = (
            ("Left", x - arm_x, -0.005, 0.755 * height_scale, arm_left_rotation),
            ("Right", x + arm_x, -0.005, 0.755 * height_scale, arm_right_rotation),
        )
    for side, side_x, side_y, side_z, side_rotation in cuff_specs:
        faceted_prism(
            f"{prefix}_ShoulderCuff{side}_LOD0",
            (0.132 * shoulder_scale * cuff_scale, 0.165 * cuff_scale, 0.055 * cuff_scale),
            (side_x, side_y, side_z),
            accent_surface,
            asset_id,
            128,
            bottom_ratio=0.86,
            top_ratio=0.98,
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
    if prefix not in {"Gulsina", "Naila"}:
        # Intersect each real front edge (sides 9 -> 0) with x=0. This keeps
        # the seam on the generated polygon after irregularity and scaling,
        # including the torso's actual z scale and object location.
        front_points = []
        for ring_index in (1, 2, 4):
            edge_left = torso.data.vertices[ring_index * 10 + 9].co
            edge_right = torso.data.vertices[ring_index * 10 + 0].co
            edge_t = -edge_left.x / (edge_right.x - edge_left.x)
            front_points.append(
                (
                    torso.location.x,
                    torso.location.y + edge_left.y + (edge_right.y - edge_left.y) * edge_t,
                    torso.location.z + edge_left.z + (edge_right.z - edge_left.z) * edge_t,
                )
            )
        placket_radius = 0.018 * shoulder_scale
        placket_depth_scale = 0.92
        placket_offset = placket_radius * placket_depth_scale - 0.003
        tapered_segment(
            f"{prefix}_CoatFrontPlacket_LOD0",
            tuple(
                (point[0], point[1] + placket_offset, point[2])
                for point in front_points
            ),
            (placket_radius, placket_radius, placket_radius),
            accent_surface,
            asset_id,
            96,
            depth_scale=placket_depth_scale,
            sides=6,
        )
    if prefix == "Gulsina":
        faceted_prism(
            f"{prefix}_ApronFront_LOD0",
            (0.38, 0.010, 0.64),
            (x, -0.200, 0.89),
            accent_surface,
            asset_id,
            128,
            bottom_ratio=1.07,
            top_ratio=0.65,
            vertices=8,
        )
        # One shallow folded bundle, held against the existing apron. Its
        # neutral project-original mesh defaults to Spine in _bone_for_mesh;
        # no new bone or runtime state is introduced.
        faceted_prism(
            f"{prefix}_TeaTowel_LOD0",
            (0.28, 0.05, 0.12),
            (x, towel_y, towel_z),
            accent_surface,
            asset_id,
            96,
            bottom_ratio=1.02,
            top_ratio=0.96,
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
    # A restrained knee/calf profile breaks the straight winter-trouser tubes.
    # Retain the ankle and existing rigid Leg.L / Leg.R attachment.
    shaped_trousers = prefix in {"Mansur", "CouncilWitness"}
    for side, direction in (("Left", -1), ("Right", 1)):
        if prefix == "Alsu":
            # Hide the upper trouser caps inside her narrow coat; feet stay put.
            rings = ((0.48, 0.000, 0.88), (0.78, 0.000, 0.65),
                     (0.94, -0.035, 0.47), (0.94, 0.008, 0.30),
                     (0.94, -0.010, 0.095))
            radii = (0.105, 0.112, 0.085, 0.096, 0.065)
        elif shaped_trousers:
            rings = ((1.0, 0.000, 0.88), (0.98, 0.000, 0.65),
                     (0.96, -0.035, 0.47), (0.94, 0.008, 0.30),
                     (0.94, -0.010, 0.095))
            radii = (0.105, 0.112, 0.085, 0.096, 0.065)
        else:
            rings = ((1.0, 0.000, 0.88), (0.96, -0.004, 0.57),
                     (0.94, -0.008, 0.30), (0.94, -0.010, 0.095))
            radii = (0.105, 0.092, 0.080, 0.065)
        tapered_segment(
            f"{prefix}_Trouser{side}_LOD0",
            tuple((x + direction * leg_offset * offset, y, z) for offset, y, z in rings),
            tuple(radius * leg_scale for radius in radii),
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
    gender = "Female" if prefix in {"Gulsina", "Alsu", "Naila"} else "Male"
    hair_style, hair_budget = ("Long", 600) if prefix == "Alsu" else ("Buns", 500) if prefix in {"Gulsina", "Naila"} else ("Buzzed", 250) if has_hat else ("SimpleParted", 400)
    parts = [(part, f"Quaternius{gender}_{part}", budget) for part, budget in (("Head", 900), ("FaceEyes", 192), ("FaceBrows", 80))]
    parts.append(("Hair", f"Quaternius_Hair{hair_style}", hair_budget))
    if has_beard:
        parts.append(("FaceBeard", "Quaternius_HairBeard", 250))
    head_object = None
    for part, source_name, budget in parts:
        source = head_templates[source_name]
        obj = source.copy()
        obj.data = source.data.copy()
        obj.name = f"{prefix}_{part}_LOD0"
        obj.location = (x, 0.0, head_z)
        for vertex in obj.data.vertices:
            vertex.co *= head_scale
        bpy.context.collection.objects.link(obj)
        tag(obj, asset_id, budget)
        obj["license"] = "CC0-1.0; derived from Quaternius Universal Base Characters"
        if part == "Head":
            head_object = obj
    face_features(prefix, head_object, head_scale, materials, asset_id)
    faceted_prism(
        f"{prefix}_ScarfBand_LOD0",
        # A scarf collar wraps the neck, so it takes the neck's width, not the
        # old 0.48 m shoulder-wide block that hid the neck completely.
        (0.105 * shoulder_scale, 0.082, scarf_height),
        (x, -0.010, scarf_z),
        material(f"{prefix}Scarf", accent_color),
        asset_id,
        128,
        bottom_ratio=0.88,
        top_ratio=0.98,
        vertices=8,
    )
    # Keep the established HeadHand names so the Godot adapter can route hands
    # to skin before the generic head mapping and the rig can bind them to arms.
    for side, side_sign in (("Left", -1.0), ("Right", 1.0)):
        if prefix == "Gulsina":
            side_x = x + side_sign * arm_x * 0.45
            hand_points = (
                # The first ring overlaps the rotated cuff; the remaining
                # rings move inward and toward the -Y face of the towel.
                (side_x, wrist_y - 0.004, wrist_z),
                (x + side_sign * 0.115, towel_y + 0.020, towel_z + 0.018),
                (x + side_sign * 0.098, towel_y - 0.004, towel_z + 0.004),
                (x + side_sign * 0.090, towel_y - 0.018, towel_z - 0.006),
                (x + side_sign * 0.078, towel_y - 0.026, towel_z - 0.016),
            )
            thumb_points = (
                (x + side_sign * 0.090, towel_y - 0.018, towel_z + 0.005),
                # Thumb tips turn across the front face, toward the centre.
                (x + side_sign * 0.060, towel_y - 0.028, towel_z + 0.030),
            )
        elif prefix == "Mansur":
            side_x = x + side_sign * 0.085
            hand_points = (
                # Overlap the cuff, then converge both palms just in front of
                # the coat. The last rings overlap at the centre without
                # changing the existing hand mesh or Arm.L/Arm.R binding.
                (side_x, mansur_hand_y, mansur_hand_z),
                (x + side_sign * 0.058, mansur_hand_y - 0.028, mansur_hand_z - 0.018),
                (x + side_sign * 0.030, mansur_hand_y - 0.034, mansur_hand_z - 0.036),
                (x + side_sign * 0.012, mansur_hand_y - 0.030, mansur_hand_z - 0.050),
                (x + side_sign * 0.006, mansur_hand_y - 0.024, mansur_hand_z - 0.063),
            )
            thumb_points = (
                (x + side_sign * 0.020, mansur_hand_y - 0.032, mansur_hand_z - 0.034),
                (x + side_sign * 0.004, mansur_hand_y - 0.050, mansur_hand_z - 0.024),
            )
        elif prefix == "Alsu":
            side_x = x + side_sign * 0.062
            hand_points = (
                # A smaller, lower convergence keeps the hands readable on
                # Alsu's narrow coat without changing the named hand meshes.
                (side_x, alsu_hand_y, alsu_hand_z),
                (x + side_sign * 0.042, alsu_hand_y - 0.024, alsu_hand_z - 0.012),
                (x + side_sign * 0.023, alsu_hand_y - 0.032, alsu_hand_z - 0.026),
                (x + side_sign * 0.010, alsu_hand_y - 0.030, alsu_hand_z - 0.038),
                (x + side_sign * 0.005, alsu_hand_y - 0.025, alsu_hand_z - 0.050),
            )
            thumb_points = (
                (x + side_sign * 0.015, alsu_hand_y - 0.030, alsu_hand_z - 0.025),
                (x + side_sign * 0.003, alsu_hand_y - 0.045, alsu_hand_z - 0.010),
            )
        else:
            side_x = x + side_sign * arm_x
            hand_points = (
                # Keep the wrist embedded in the rotated cuff. Four short rings
                # give the palm a rounded mitten-like end without changing the
                # established HeadHand mesh or Arm.L/Arm.R binding.
                (side_x, -0.018, 0.75 * height_scale),
                (side_x + side_sign * 0.004, -0.026, 0.69 * height_scale),
                (side_x + side_sign * 0.010, -0.040, 0.64 * height_scale),
                (side_x + side_sign * 0.014, -0.049, 0.61 * height_scale),
                (side_x + side_sign * 0.016, -0.055, 0.578 * height_scale),
            )
            thumb_points = (
                (side_x + side_sign * 0.012, -0.040, 0.65 * height_scale),
                (side_x + side_sign * 0.040, -0.058, 0.61 * height_scale),
            )
        tapered_segment(
            f"{prefix}_HeadHand{side}_LOD0",
            hand_points,
            (0.045 * shoulder_scale, 0.056 * shoulder_scale, 0.049 * shoulder_scale, 0.034 * shoulder_scale,
             0.026 * shoulder_scale),
            materials["skin"],
            asset_id,
            128,
            depth_scale=0.45 if prefix == "CouncilWitness" and side == "Right" else 0.96,
            sides=8,
        )
        tapered_segment(
            f"{prefix}_HeadHandThumb{side}_LOD0",
            thumb_points,
            (0.024 * shoulder_scale, 0.016 * shoulder_scale),
            materials["skin"],
            asset_id,
            64,
            depth_scale=0.96,
            sides=6,
        )
    if has_hat:
        hat_style = {
            "Gulsina": "head_wrap",
            "Mansur": "wool_cap",
            "TimurHazrat": "prayer_cap",
            "CouncilElder": "council_cap",
            "PactKeeper": "council_cap",
        }.get(prefix, "wool_cap")
        hat_offset = 0.16 if prefix == "Gulsina" else 0.20 if prefix == "Mansur" else 0.22
        hat(f"{prefix}_Hat_LOD0", (x, 0.0, head_z + hat_offset * head_scale), materials["hair"], asset_id, style=hat_style)

    for obj in bpy.context.scene.objects:
        if obj.name == f"{prefix}_Hat_LOD0":
            for vertex in obj.data.vertices:
                vertex.co.x *= .85


def _bone_for_mesh(name: str) -> str:
    """Map a generated mesh to the smallest useful presentation bone."""
    if "HandLeft" in name or "HandThumbLeft" in name or "ShoulderCuffLeft" in name:
        return "Arm.L"
    if "HandRight" in name or "HandThumbRight" in name or "ShoulderCuffRight" in name:
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
    cyclic: bool = True,
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

    if cyclic:
        for curve in action.fcurves:
            curve.modifiers.new(type="CYCLES")
    for curve in action.fcurves:
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
    armature.data["skin_policy"] = "rigid skin per piece: one vertex group per bone at weight 1.0 plus an Armature modifier; no bone parenting"

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
    if prefix == "CouncilWitness":
        # CouncilWitness alone gets an independent wrist.
        witness_height, witness_shoulder, *_ = SILHOUETTE_PROFILES[prefix]
        witness_arm_x = 0.29 * witness_shoulder
        hand_r = edit_bones.new("Hand.R")
        hand_r.head = (witness_arm_x, -0.018, 0.75 * witness_height)
        hand_r.tail = (witness_arm_x + 0.014, -0.049, 0.61 * witness_height)
        hand_r.parent = arm_r
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
        if prefix == "CouncilWitness" and (
            obj.name.startswith("CouncilWitness_HeadHandRight_")
            or obj.name.startswith("CouncilWitness_HeadHandThumbRight_")
        ):
            bone_name = "Hand.R"
        else:
            bone_name = _bone_for_mesh(obj.name)

        # Skin each piece to its bone instead of bone-parenting it. A
        # bone-parented mesh exports as a node hanging off a joint with no skin,
        # and Godot then imports the animation as tracks named after the bone
        # ("<rig>/Skeleton3D:Spine"), which move nothing at all: measured in
        # SceneSmokeTest, the bone pose, the bone's global pose and the visible
        # mesh all stayed put while the clip reported itself playing. With a
        # real skin the glTF carries joints and Godot lands the clips on bone
        # poses, so the authored Idle/Tension finally do something.
        obj.parent = armature
        obj.parent_type = "OBJECT"
        obj.matrix_world = world_matrix
        for group in list(obj.vertex_groups):
            obj.vertex_groups.remove(group)
        group = obj.vertex_groups.new(name=bone_name)
        group.add([vertex.index for vertex in obj.data.vertices], 1.0, "REPLACE")
        for modifier in [item for item in obj.modifiers if item.type == "ARMATURE"]:
            obj.modifiers.remove(modifier)
        skin = obj.modifiers.new("URMAN_CHARACTER_SKIN", "ARMATURE")
        skin.object = armature
        skin.use_vertex_groups = True
        skin.use_bone_envelopes = False

    frames = (1, 20, 40)
    idle_poses = {
        "Spine": ((0.0, -0.012, 0.0), (0.0, 0.016, 0.0), (0.0, -0.012, 0.0)),
        "Head": ((0.0, 0.008, 0.0), (0.0, -0.012, 0.0), (0.0, 0.008, 0.0)),
        "Arm.L": ((0.0, 0.0, -0.018), (0.0, 0.0, 0.012), (0.0, 0.0, -0.018)),
        "Arm.R": ((0.0, 0.0, 0.018), (0.0, 0.0, -0.012), (0.0, 0.0, 0.018)),
    }
    if prefix == "CouncilWitness":
        # Idle keys clear the stop wrist when the clip changes.
        idle_poses["Hand.R"] = ((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0))
    _make_action(
        armature,
        prefix,
        "Idle",
        frames,
        idle_poses,
    )
    tension_poses = {
        "Spine": ((0.0, -0.025, 0.0), (0.0, 0.035, 0.0), (0.0, -0.025, 0.0)),
        "Head": ((0.025, 0.016, 0.0), (-0.035, -0.022, 0.0), (0.025, 0.016, 0.0)),
        "Arm.L": ((0.0, 0.0, -0.05), (0.0, 0.0, 0.028), (0.0, 0.0, -0.05)),
        "Arm.R": ((0.0, 0.0, 0.05), (0.0, 0.0, -0.028), (0.0, 0.0, 0.05)),
    }
    tension_cyclic = True
    if prefix == "CouncilWitness":
        # CouncilWitness stop: Arm.R raises; Hand.R turns the palm upward.
        tension_poses = {
            "Spine": ((0.0, -0.012, 0.0), (0.0, 0.035, 0.0), (0.0, 0.035, 0.0)),
            "Head": ((0.0, 0.008, 0.0), (-0.035, -0.022, 0.0), (-0.035, -0.022, 0.0)),
            "Arm.L": ((0.0, 0.0, -0.018), (0.0, 0.0, 0.012), (0.0, 0.0, 0.012)),
            "Arm.R": ((0.0, 0.0, 0.018), (-1.2, 0.0, 0.12), (-1.2, 0.0, 0.12)),
            "Hand.R": ((0.0, 0.0, 0.0), (-1.64159265, -0.16, -3.02159265), (-1.64159265, -0.16, -3.02159265)),
        }
        tension_cyclic = False
    tension = _make_action(
        armature,
        prefix,
        "Tension",
        frames,
        tension_poses,
        cyclic=tension_cyclic,
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
    template_path = root / "assets/source/blender/characters/urman_cc0_head_templates.blend"
    names = [f"Quaternius{gender}_{part}" for gender in ("Female", "Male") for part in ("Head", "FaceEyes", "FaceBrows")]
    names += [f"Quaternius_Hair{style}" for style in ("Long", "Buns", "SimpleParted", "Buzzed", "Beard")]
    with bpy.data.libraries.load(str(template_path), link=False) as (available, loaded):
        if not set(names).issubset(available.objects):
            raise RuntimeError("CC0 head source is missing a required template")
        loaded.objects = names
    head_templates = {obj.name: obj for obj in loaded.objects}
    for index, (prefix, coat, accent, has_hat, has_beard) in enumerate(CHARACTERS):
        create_character(prefix, coat, accent, has_hat, has_beard, index * 2.4, materials, head_templates)

    lod_count = generate_lod1_variants()
    rigs = [add_animation_rig(prefix, index * 2.4) for index, (prefix, *_rest) in enumerate(CHARACTERS)]
    bpy.context.scene["generator"] = "tools/blender/generate_character_kit.py"
    bpy.context.scene["blender_version_lock"] = "4.5 LTS"
    bpy.context.scene["units"] = "meters"
    bpy.context.scene["character_prefixes"] = ",".join(prefix for prefix, *_ in CHARACTERS)
    bpy.context.scene["lod_policy"] = "LOD1 generated with deterministic Decimate ratio=0.50; Godot ranges are scene-specific"
    bpy.context.scene["lod1_mesh_count"] = lod_count
    bpy.context.scene["lod0_mesh_count"] = len([obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.endswith("_LOD0")])
    bpy.context.scene["detail_policy"] = "CC0 Quaternius head derivatives with face landmarks; project-original painterly low-poly tapered torsos, hands, layered clothing and restrained stance variants; faceted sleeves, trousers, boots and role-neutral apron/placket details; authored Idle/Tension rigs; final face/expression and cultural review remains open"
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
