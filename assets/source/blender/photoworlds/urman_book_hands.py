"""Build a winter-gloved first-person book-hold viewmodel.

Run with Blender 5.2 or newer:
  blender --background --python assets/source/blender/photoworlds/urman_book_hands.py \
    -- --output-dir /private/tmp/urman-book-hands-YYYYMMDD-stage \
       --book-glb /private/tmp/urman-family-book-YYYYMMDD/urman_family_book.glb

The exported GLB contains only the rigged hands and sleeves. The supplied book
GLB is imported as a temporary offline fixture for the two preview renders.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


FPS = 24
TILE_METRES = 0.080
BOOK_CLOSED = ((-0.110, -0.1425, -0.019), (0.110, 0.1425, 0.019))
BOOK_OPEN = ((-0.335, -0.1425, -0.019), (0.110, 0.1425, 0.019))
BOOK_OPEN_OCCUPIED = (
    ((-0.335, -0.1425, -0.0190), (-0.1150, 0.1425, -0.014625)),
    ((-0.110, -0.1425, -0.0190), (0.110, 0.1425, 0.0190)),
)
OPEN_LEFT_LAYOUT_Z = -0.035


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--book-glb", type=Path, required=True)
    return parser.parse_args(tail)


def project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.actions,
                       bpy.data.cameras, bpy.data.lights):
        for datablock in list(collection):
            if datablock.users == 0:
                collection.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.fps = FPS
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.frame_start = 0
    scene.frame_end = 24
    scene.frame_set(0)
    scene.world.color = (0.20, 0.20, 0.20)


def srgb_to_linear(channel: int) -> float:
    value = channel / 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def hex_rgba(value: str) -> tuple[float, float, float, float]:
    value = value.lstrip("#")
    return tuple(srgb_to_linear(int(value[index:index + 2], 16)) for index in (0, 2, 4)) + (1.0,)


def cloth_material(name: str, color: str, texture_path: Path) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = hex_rgba(color)
    material["shared_project_map"] = texture_path.as_posix()
    material["tile_metres"] = TILE_METRES
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.92
    shader.inputs["Metallic"].default_value = 0.0
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = "MetricUV"
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.colorspace_settings.name = "sRGB"
    image.pack()
    image_node = nodes.new("ShaderNodeTexImage")
    image_node.image = image
    image_node.extension = "REPEAT"
    multiply = nodes.new("ShaderNodeVectorMath")
    multiply.operation = "MULTIPLY"
    multiply.inputs[1].default_value = hex_rgba(color)[:3]
    links = material.node_tree.links
    links.new(uv.outputs["UV"], image_node.inputs["Vector"])
    links.new(image_node.outputs["Color"], multiply.inputs[0])
    links.new(multiply.outputs["Vector"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def recalc_normals(mesh: bpy.types.Mesh) -> None:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if bm.faces:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def assign_metric_uv(mesh: bpy.types.Mesh) -> None:
    uv = mesh.uv_layers.new(name="MetricUV")
    for polygon in mesh.polygons:
        axis = max(range(3), key=lambda index: abs(polygon.normal[index]))
        for loop_index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if axis == 2:
                u, v = point.x, point.y
            elif axis == 1:
                u, v = point.x, point.z
            else:
                u, v = point.y, point.z
            uv.data[loop_index].uv = (u / TILE_METRES, v / TILE_METRES)
    mesh["uv_contract"] = "metric planar projection; one source-map tile per 0.080 m"


def rigid_mesh(name: str, vertices, faces, material: bpy.types.Material,
               armature: bpy.types.Object, bone_name: str) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    recalc_normals(mesh)
    assign_metric_uv(mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = armature
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    obj.data.materials.append(material)
    group = obj.vertex_groups.new(name=bone_name)
    if mesh.vertices:
        group.add([vertex.index for vertex in mesh.vertices], 1.0, "REPLACE")
    modifier = obj.modifiers.new("BookHands_Skin", "ARMATURE")
    modifier.object = armature
    modifier.use_vertex_groups = True
    modifier.use_bone_envelopes = False
    obj["viewmodel_role"] = bone_name
    obj["source_status"] = "project-original viewmodel geometry; no body or actor mesh"
    return obj


def ellipsoid(name: str, center: Vector, radii: tuple[float, float, float],
              material: bpy.types.Material, armature: bpy.types.Object, bone: str,
              segments: int = 24, rings: int = 14) -> bpy.types.Object:
    vertices = []
    faces = []
    for row in range(rings + 1):
        latitude = -math.pi * 0.5 + math.pi * row / rings
        for column in range(segments):
            longitude = 2.0 * math.pi * column / segments
            vertices.append((
                center.x + radii[0] * math.cos(latitude) * math.cos(longitude),
                center.y + radii[1] * math.cos(latitude) * math.sin(longitude),
                center.z + radii[2] * math.sin(latitude),
            ))
    for row in range(rings):
        for column in range(segments):
            a = row * segments + column
            b = row * segments + (column + 1) % segments
            c = (row + 1) * segments + (column + 1) % segments
            d = (row + 1) * segments + column
            faces.append((a, b, c, d))
    return rigid_mesh(name, vertices, faces, material, armature, bone)


def capsule(name: str, start: Vector, end: Vector, radii: tuple[float, float],
            material: bpy.types.Material, armature: bpy.types.Object, bone: str,
            segments: int = 12, rings: int = 12) -> bpy.types.Object:
    """Make a smooth finite capsule along an arbitrary axis."""
    axis = (end - start).normalized()
    cap_radius = min(radii)
    reference = Vector((0.0, 0.0, 1.0))
    if abs(axis.dot(reference)) > 0.93:
        reference = Vector((0.0, 1.0, 0.0))
    basis_a = axis.cross(reference).normalized()
    basis_b = axis.cross(basis_a).normalized()
    length = (end - start).length
    vertices = [tuple(start - axis * cap_radius)]
    faces = []
    steps = rings + 4
    ring_rows = []
    for row in range(1, steps):
        along = -cap_radius + (length + 2.0 * cap_radius) * row / steps
        if along < 0.0:
            scale = math.sqrt(max(0.0, 1.0 - (along / cap_radius) ** 2))
        elif along > length:
            scale = math.sqrt(max(0.0, 1.0 - ((along - length) / cap_radius) ** 2))
        else:
            scale = 1.0
        point = start + axis * along
        ring_rows.append(len(vertices))
        for column in range(segments):
            angle = 2.0 * math.pi * column / segments
            co = point + basis_a * (radii[0] * scale * math.cos(angle)) \
                + basis_b * (radii[1] * scale * math.sin(angle))
            vertices.append(tuple(co))
    end_center = len(vertices)
    vertices.append(tuple(end + axis * cap_radius))
    first_ring, last_ring = ring_rows[0], ring_rows[-1]
    for column in range(segments):
        next_column = (column + 1) % segments
        faces.append((0, first_ring + next_column, first_ring + column))
        for row in range(len(ring_rows) - 1):
            a = ring_rows[row] + column
            b = ring_rows[row] + next_column
            c = ring_rows[row + 1] + next_column
            d = ring_rows[row + 1] + column
            faces.append((a, b, c, d))
        faces.append((last_ring + column, last_ring + next_column, end_center))
    return rigid_mesh(name, vertices, faces, material, armature, bone)


def curved_glove_digit(name: str, points: list[tuple[Vector, float, float]],
                       material: bpy.types.Material, armature: bpy.types.Object,
                       bone: str, side_sign: float, min_abs_x: float = 0.0,
                       samples_per_segment: int = 8, sections: int = 16) -> bpy.types.Object:
    """Sweep a rounded, tapered wool digit around a book edge.

    Control points contain a center and two cross-section radii. The sampled
    Catmull-Rom path keeps the knuckles soft while the rounded ends avoid the
    old disconnected, straight-capsule silhouette.
    """
    centers = [item[0] for item in points]
    radius_a = [item[1] for item in points]
    radius_b = [item[2] for item in points]

    def catmull(values, first: int, t: float):
        p0 = values[max(0, first - 1)]
        p1 = values[first]
        p2 = values[first + 1]
        p3 = values[min(len(values) - 1, first + 2)]
        t2, t3 = t * t, t * t * t
        if isinstance(p1, Vector):
            return 0.5 * ((2.0 * p1) + (-p0 + p2) * t
                          + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                          + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)
        return 0.5 * ((2.0 * p1) + (-p0 + p2) * t
                      + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                      + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)

    sampled = []
    for segment in range(len(points) - 1):
        for sample in range(samples_per_segment):
            t = sample / samples_per_segment
            center = catmull(centers, segment, t)
            if min_abs_x > 0.0:
                center.x = side_sign * max(side_sign * center.x, min_abs_x)
            sampled.append((center,
                            max(0.0, catmull(radius_a, segment, t)),
                            max(0.0, catmull(radius_b, segment, t))))
    final_center = centers[-1].copy()
    if min_abs_x > 0.0:
        final_center.x = side_sign * max(side_sign * final_center.x, min_abs_x)
    sampled.append((final_center, radius_a[-1], radius_b[-1]))

    # Cap the open ends with poles and keep all remaining rings smooth.
    active = [(center, ra, rb) for center, ra, rb in sampled
              if min(ra, rb) > 1e-5]
    if len(active) < 3:
        raise RuntimeError(f"Curved digit has too few nonzero rings: {name}")
    vertices = [tuple(sampled[0][0])]
    ring_rows = []
    for index, (center, ra, rb) in enumerate(active):
        before = active[max(0, index - 1)][0]
        after = active[min(len(active) - 1, index + 1)][0]
        tangent = (after - before).normalized()
        reference = Vector((0.0, 1.0, 0.0))
        if abs(tangent.dot(reference)) > 0.94:
            reference = Vector((0.0, 0.0, 1.0))
        axis_a = tangent.cross(reference).normalized()
        axis_b = tangent.cross(axis_a).normalized()
        ring_rows.append(len(vertices))
        for column in range(sections):
            angle = 2.0 * math.pi * column / sections
            co = center + axis_a * (ra * math.cos(angle)) + axis_b * (rb * math.sin(angle))
            vertices.append(tuple(co))
    end_pole = len(vertices)
    vertices.append(tuple(sampled[-1][0]))
    faces = []
    for column in range(sections):
        next_column = (column + 1) % sections
        faces.append((0, ring_rows[0] + next_column, ring_rows[0] + column))
        for row in range(len(ring_rows) - 1):
            a = ring_rows[row] + column
            b = ring_rows[row] + next_column
            c = ring_rows[row + 1] + next_column
            d = ring_rows[row + 1] + column
            faces.append((a, b, c, d))
        faces.append((ring_rows[-1] + column, ring_rows[-1] + next_column, end_pole))
    obj = rigid_mesh(name, vertices, faces, material, armature, bone)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj["glove_form"] = "rounded tapered curved digit; swept over/around book edge"
    return obj


def swept_sleeve(name: str, sign: float, material: bpy.types.Material,
                 armature: bpy.types.Object, bone: str) -> bpy.types.Object:
    centers = [Vector((sign * x, y, z)) for x, y, z in (
        (0.430, -0.208, -0.038),
        (0.360, -0.165, -0.026),
        (0.285, -0.115, -0.009),
        (0.216, -0.074, 0.004),
        (0.179, -0.062, 0.006),
    )]
    radii = [(0.052, 0.046), (0.053, 0.047), (0.050, 0.045),
             (0.047, 0.043), (0.046, 0.042)]
    vertices = []
    faces = []
    sections = 12
    for row, center in enumerate(centers):
        tangent = (centers[min(row + 1, len(centers) - 1)]
                   - centers[max(row - 1, 0)]).normalized()
        reference = Vector((0.0, 0.0, 1.0))
        if abs(tangent.dot(reference)) > 0.92:
            reference = Vector((0.0, 1.0, 0.0))
        u_axis = tangent.cross(reference).normalized()
        v_axis = tangent.cross(u_axis).normalized()
        for column in range(sections):
            angle = 2.0 * math.pi * column / sections
            co = center + u_axis * (radii[row][0] * math.cos(angle)) \
                + v_axis * (radii[row][1] * math.sin(angle))
            vertices.append(tuple(co))
    for row in range(len(centers) - 1):
        for column in range(sections):
            a = row * sections + column
            b = row * sections + (column + 1) % sections
            c = (row + 1) * sections + (column + 1) % sections
            d = (row + 1) * sections + column
            faces.append((a, b, c, d))
    faces.append(tuple(range(sections - 1, -1, -1)))
    last = (len(centers) - 1) * sections
    faces.append(tuple(last + column for column in range(sections)))
    return rigid_mesh(name, vertices, faces, material, armature, bone)


def create_rig() -> bpy.types.Object:
    armature_data = bpy.data.armatures.new("BookHands_RigData")
    armature = bpy.data.objects.new("BookHands_Root", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    armature.show_in_front = True
    armature_data.display_type = "BBONE"
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bones = armature_data.edit_bones
    root = bones.new("Root")
    root.head, root.tail = (0.0, 0.0, 0.0), (0.0, 0.0, 0.10)
    for side, sign in (("L", -1.0), ("R", 1.0)):
        layout = bones.new(f"Layout.{side}")
        layout.head, layout.tail = (0.0, 0.0, 0.0), (0.0, 0.0, 0.08)
        layout.parent = root
        forearm = bones.new(f"Forearm.{side}")
        forearm.head = (sign * 0.43, -0.208, -0.038)
        forearm.tail = (sign * 0.179, -0.062, 0.006)
        forearm.parent = layout
        forearm.use_connect = False
        wrist = bones.new(f"Wrist.{side}")
        wrist.head = (sign * 0.179, -0.062, 0.006)
        wrist.tail = (sign * 0.125, -0.060, 0.006)
        wrist.parent = forearm
        wrist.use_connect = False
        fingers = bones.new(f"FingerGroup.{side}")
        fingers.head = (sign * 0.145, -0.061, 0.024)
        fingers.tail = (sign * 0.105, -0.061, 0.024)
        fingers.parent = wrist
        fingers.use_connect = False
        thumb = bones.new(f"Thumb.{side}")
        thumb.head = (sign * 0.151, -0.031, 0.028)
        thumb.tail = (sign * 0.090, -0.052, 0.028)
        thumb.parent = wrist
        thumb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    armature["asset_role"] = "winter-gloved first-person book hold; no actor body"
    armature["bind_units"] = "metres; Blender X width, Y depth, Z thickness; GLB exports Y-up"
    armature["skin_policy"] = "rigid project-original viewmodel parts, explicit wrist/finger/thumb joints"
    return armature


def create_hands(armature: bpy.types.Object, cloth_texture: Path) -> list[bpy.types.Object]:
    sleeves = cloth_material("WinterSleeveCloth", "3c4546", cloth_texture)
    gloves = cloth_material("WinterGloveCloth", "454d4b", cloth_texture)
    cuffs = cloth_material("WinterCuffCloth", "4a514e", cloth_texture)
    objects = []
    for side, sign in (("L", -1.0), ("R", 1.0)):
        objects.append(swept_sleeve(f"Sleeve.{side}", sign, sleeves, armature, f"Forearm.{side}"))
        # A short doubled cuff joins the winter sleeve to its glove without a
        # visible skin gap. Both meshes use the same source fabric atlas.
        objects.append(capsule(f"CuffBand.{side}", Vector((sign * 0.196, -0.068, 0.005)),
                               Vector((sign * 0.167, -0.062, 0.006)), (0.047, 0.043),
                               cuffs, armature, f"Wrist.{side}"))
        center = Vector((sign * 0.153, -0.062, 0.003))
        objects.append(ellipsoid(f"GlovePalm.{side}", center, (0.039, 0.055, 0.021),
                                 gloves, armature, f"Wrist.{side}"))
        for finger_index, y in enumerate((-0.126, -0.100, -0.074, -0.048), start=1):
            digit_points = [
                (Vector((sign * 0.194, y + 0.004, 0.020)), 0.0, 0.0),
                (Vector((sign * 0.178, y + 0.002, 0.027)), 0.0060, 0.0065),
                (Vector((sign * 0.151, y, 0.028)), 0.0080, 0.0080),
                (Vector((sign * 0.128, y - 0.002, 0.028)), 0.0075, 0.0078),
                (Vector((sign * 0.119, y - 0.004, 0.022)), 0.0065, 0.0072),
                (Vector((sign * 0.119, y - 0.005, 0.008)), 0.0065, 0.0070),
                (Vector((sign * 0.119, y - 0.007, -0.008)), 0.0065, 0.0070),
                (Vector((sign * 0.123, y - 0.009, -0.023)), 0.0058, 0.0065),
                (Vector((sign * 0.141, y - 0.010, -0.028)), 0.0033, 0.0042),
                (Vector((sign * 0.145, y - 0.010, -0.028)), 0.0, 0.0),
            ]
            objects.append(curved_glove_digit(
                f"GloveFinger{finger_index}.{side}", digit_points, gloves, armature,
                f"FingerGroup.{side}", sign, min_abs_x=0.121))
        thumb_points = [
            (Vector((sign * 0.185, -0.022, 0.022)), 0.0, 0.0),
            (Vector((sign * 0.164, -0.021, 0.030)), 0.0070, 0.0065),
            (Vector((sign * 0.143, -0.026, 0.031)), 0.0080, 0.0070),
            (Vector((sign * 0.125, -0.039, 0.030)), 0.0075, 0.0065),
            (Vector((sign * 0.109, -0.056, 0.028)), 0.0065, 0.0058),
            (Vector((sign * 0.101, -0.067, 0.027)), 0.0035, 0.0038),
            (Vector((sign * 0.099, -0.070, 0.027)), 0.0, 0.0),
        ]
        objects.append(curved_glove_digit(f"GloveThumb.{side}", thumb_points, gloves, armature,
                                          f"Thumb.{side}", sign, samples_per_segment=8))
    return objects


def pose_bone(armature: bpy.types.Object, name: str):
    return armature.pose.bones[name]


def set_layout_world_delta(armature: bpy.types.Object, side: str,
                          lateral: float, vertical: float) -> None:
    """Set a Blender-world X/Z offset through the Layout bone's local basis."""
    bone = pose_bone(armature, f"Layout.{side}")
    desired_blender_delta = Vector((lateral, 0.0, vertical))
    bone.location = bone.bone.matrix_local.to_3x3().inverted() @ desired_blender_delta


def evaluated_bone_world_delta(armature: bpy.types.Object, bone_name: str) -> Vector:
    bone = pose_bone(armature, bone_name)
    bpy.context.view_layer.update()
    posed_world = armature.matrix_world @ bone.matrix
    rest_world = armature.matrix_world @ bone.bone.matrix_local
    return posed_world.translation - rest_world.translation


def validate_layout_animation_offsets(armature: bpy.types.Object) -> list[dict]:
    checks = []
    target_by_clip = {
        "hold_closed": (24, {"L": Vector((0.0, 0.0, 0.0)), "R": Vector((0.0, 0.0, 0.0))}),
        "hold_open": (24, {"L": Vector((-0.224, 0.0, OPEN_LEFT_LAYOUT_Z)),
                           "R": Vector((0.0, 0.0, 0.0))}),
        "return": (1, {"L": Vector((-0.224, 0.0, OPEN_LEFT_LAYOUT_Z)),
                       "R": Vector((0.0, 0.0, 0.0))}),
    }
    tracks = {track.name: track for track in armature.animation_data.nla_tracks}
    for clip_name, (frame, expected_by_side) in target_by_clip.items():
        if clip_name not in tracks:
            raise RuntimeError(f"Missing NLA track for world-offset check: {clip_name}")
        for track in tracks.values():
            track.mute = track.name != clip_name
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for side, expected in expected_by_side.items():
            actual_layout = evaluated_bone_world_delta(armature, f"Layout.{side}")
            actual_wrist = evaluated_bone_world_delta(armature, f"Wrist.{side}")
            if (actual_layout - expected).length > 1e-5 or (actual_wrist - expected).length > 1e-5:
                raise RuntimeError(
                    f"{clip_name} {side} evaluated Blender-world delta mismatch: "
                    f"layout={tuple(actual_layout)}, wrist={tuple(actual_wrist)}, "
                    f"expected={tuple(expected)}")
            checks.append({"clip": clip_name, "frame": frame, "side": side,
                           "expected_blender_world_delta_m": list(expected),
                           "layout_blender_world_delta_m": list(actual_layout),
                           "wrist_blender_world_delta_m": list(actual_wrist)})
    for track in tracks.values():
        track.mute = False
    set_neutral_pose(armature)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    return checks


def set_neutral_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def add_clip(armature: bpy.types.Object, name: str, keys, contract: dict) -> dict:
    action = bpy.data.actions.new(name=name)
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, layout_values, wrist_values in keys:
        for side, offset in zip(("L", "R"), layout_values):
            lateral, vertical = offset if isinstance(offset, tuple) else (offset, 0.0)
            set_layout_world_delta(armature, side, lateral, vertical)
            bone = pose_bone(armature, f"Layout.{side}")
            bone.keyframe_insert(data_path="location", frame=frame, group=f"Layout.{side}")
        for side, rotation in zip(("L", "R"), wrist_values):
            bone = pose_bone(armature, f"Wrist.{side}")
            bone.rotation_euler = rotation
            bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=f"Wrist.{side}")
    # Blender 5.2 stores keys in Action channel bags rather than exposing the
    # pre-4.4 Action.fcurves collection. keyframe_insert uses finite Bezier
    # keys by default; avoid mutating undocumented/legacy curve handles here.
    armature.animation_data.action = None
    track = armature.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, keys[0][0], action)
    strip.action_frame_start = keys[0][0]
    strip.action_frame_end = keys[-1][0]
    strip.blend_type = "REPLACE"
    strip.extrapolation = "NOTHING"
    strip.influence = 1.0
    strip.use_auto_blend = False
    return {"name": name, "action": action.name,
            "start_frame": keys[0][0], "end_frame": keys[-1][0],
            "duration_seconds": round((keys[-1][0] - keys[0][0]) / FPS, 4),
            "contract": contract}


def create_clips(armature: bpy.types.Object) -> list[dict]:
    neutral_wrist = ((0.0, 0.0, 0.0), (0.0, 0.0, 0.0))
    open_wrist = ((0.0, 0.0, math.radians(3.0)), (0.0, 0.0, math.radians(-1.0)))
    clips = [
        add_clip(armature, "hold_closed", [
            (1, ((0.0, 0.0), (0.0, 0.0)), neutral_wrist),
            (24, ((0.0, 0.0), (0.0, 0.0)), neutral_wrist)], {
                "precondition": "closed book; neutral winter-glove grip at both side edges",
                "start": "default bind pose", "end": "same closed grip", "motion": "static reduced-motion hold"}),
        add_clip(armature, "hold_open", [
            (1, ((0.0, 0.0), (0.0, 0.0)), neutral_wrist),
            (12, ((-0.112, OPEN_LEFT_LAYOUT_Z * 0.5), (0.0, 0.0)),
             ((0.0, 0.0, math.radians(1.5)), (0.0, 0.0, 0.0))),
            (24, ((-0.224, OPEN_LEFT_LAYOUT_Z), (0.0, 0.0)), open_wrist)], {
                "precondition": "closed book; left hand moves to outer edge of the opened cover",
                "end_layout_translation_m": {"left_x": -0.224, "left_z": OPEN_LEFT_LAYOUT_Z,
                                              "right_x": 0.0, "right_z": 0.0},
                "support": "left glove drops to press the inverted outer cover; right glove remains at the right page edge",
                "reduced_motion": "one finite, eased lateral adjustment; no loop"}),
        add_clip(armature, "return", [
            (1, ((-0.224, OPEN_LEFT_LAYOUT_Z), (0.0, 0.0)), open_wrist),
            (12, ((-0.112, OPEN_LEFT_LAYOUT_Z * 0.5), (0.0, 0.0)),
             ((0.0, 0.0, math.radians(1.5)), (0.0, 0.0, 0.0))),
            (24, ((0.0, 0.0), (0.0, 0.0)), neutral_wrist)], {
                "precondition": "open book supported by the two gloves",
        "end": "neutral closed-book hold",
        "end_layout_translation_m": {"left_x": 0.0, "left_z": 0.0, "right_x": 0.0, "right_z": 0.0},
                "reduced_motion": "single finite return; no loop"}),
    ]
    set_neutral_pose(armature)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    return clips


def mesh_bounds(objects: list[bpy.types.Object]) -> dict:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    mesh_bounds = []
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        local_points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        evaluated.to_mesh_clear()
        if not local_points:
            continue
        minimum = [min(point[axis] for point in local_points) for axis in range(3)]
        maximum = [max(point[axis] for point in local_points) for axis in range(3)]
        mesh_bounds.append({"name": obj.name, "min_xyz": minimum, "max_xyz": maximum})
        points.extend(local_points)
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    return {"min_xyz": minimum, "max_xyz": maximum,
            "size_xyz": [maximum[axis] - minimum[axis] for axis in range(3)],
            "meshes": mesh_bounds}


def verify_no_penetration(objects: list[bpy.types.Object], book_bounds, open_pose: bool) -> dict:
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    minimum, maximum = book_bounds
    occupied_bounds = BOOK_OPEN_OCCUPIED if open_pose else ((minimum, maximum),)
    penetration_vertices = []
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        for vertex in mesh.vertices:
            point = evaluated.matrix_world @ vertex.co
            if any(all(bounds_min[axis] + 0.00015 < point[axis] < bounds_max[axis] - 0.00015
                       for axis in range(3))
                   for bounds_min, bounds_max in occupied_bounds):
                penetration_vertices.append((obj.name, tuple(round(value, 6) for value in point)))
        evaluated.to_mesh_clear()
    if penetration_vertices:
        raise RuntimeError(f"Viewmodel mesh vertices enter book volume: {penetration_vertices[:8]}")
    return {"method": "evaluated hand-vertex checks against occupied book component boxes; open cover and fixed block checked separately",
            "limitation": "box-envelope screening only; does not prove triangle contact or exclude all surface intersections",
            "book_envelope_min_xyz": list(minimum), "book_envelope_max_xyz": list(maximum),
            "penetrating_hand_vertices": 0}


def mute_animation(obj: bpy.types.Object) -> None:
    if not obj.animation_data:
        return
    obj.animation_data.action = None
    for track in obj.animation_data.nla_tracks:
        track.mute = True


def set_viewmodel_pose(armature: bpy.types.Object, open_pose: bool) -> None:
    mute_animation(armature)
    set_neutral_pose(armature)
    if open_pose:
        set_layout_world_delta(armature, "L", -0.224, OPEN_LEFT_LAYOUT_Z)
        pose_bone(armature, "Wrist.L").rotation_euler.z = math.radians(3.0)
        pose_bone(armature, "Wrist.R").rotation_euler.z = math.radians(-1.0)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_fixture(output_dir: Path, armature: bpy.types.Object, hand_objects: list[bpy.types.Object],
                   book_glb: Path, cloth_texture: Path, paper_texture: Path) -> dict:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(book_glb))
    book_objects = [obj for obj in bpy.data.objects if obj not in before]
    for obj in book_objects:
        mute_animation(obj)
    fixture_materials = {
        "cloth": cloth_material("FixtureBookCloth", "674b45", cloth_texture),
        "paper": cloth_material("FixtureBookPaper", "d6ccb3", paper_texture),
    }
    for obj in book_objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if material is None:
                continue
            authored_name = material.name.split(".")[0].lower()
            if authored_name in fixture_materials:
                slot.material = fixture_materials[authored_name]
    hinge = next((obj for obj in book_objects if obj.name.split(".")[0] == "FrontCoverHinge"), None)
    if hinge is None:
        names = [obj.name for obj in book_objects if obj.type == "EMPTY"]
        raise RuntimeError(f"Book fixture has no FrontCoverHinge node; empties={names[:30]}")

    camera_data = bpy.data.cameras.new("FixturePreviewCamera")
    camera = bpy.data.objects.new("FixturePreviewCamera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = (0.20, -0.34, 0.31)
    point_at(camera, Vector((0.0, 0.0, 0.008)))
    camera_data.type = "ORTHO"
    bpy.context.scene.camera = camera
    camera_data.ortho_scale = 0.58

    light_data = bpy.data.lights.new("FixturePreviewKey", type="AREA")
    light_data.energy = 2.5
    light_data.shape = "DISK"
    light_data.size = 0.32
    light = bpy.data.objects.new("FixturePreviewKey", light_data)
    bpy.context.scene.collection.objects.link(light)
    light.location = (-0.28, -0.28, 0.46)
    point_at(light, Vector((0.0, 0.0, 0.0)))
    fill_data = bpy.data.lights.new("FixturePreviewFill", type="AREA")
    fill_data.energy = 0.8
    fill_data.shape = "DISK"
    fill_data.size = 0.28
    fill = bpy.data.objects.new("FixturePreviewFill", fill_data)
    bpy.context.scene.collection.objects.link(fill)
    fill.location = (0.34, 0.06, 0.28)
    point_at(fill, Vector((0.0, 0.0, 0.0)))
    floor_mesh = bpy.data.meshes.new("FixturePreviewFloor_Mesh")
    floor_mesh.from_pydata([(-2.0, -2.0, -0.115), (2.0, -2.0, -0.115),
                            (2.0, 2.0, -0.115), (-2.0, 2.0, -0.115)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("FixturePreviewFloor", floor_mesh)
    bpy.context.scene.collection.objects.link(floor)
    floor_mat = bpy.data.materials.new("FixturePreviewGround")
    floor_mat.diffuse_color = (0.27, 0.29, 0.29, 1.0)
    floor_mat.use_nodes = True
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.27, 0.29, 0.29, 1.0)
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    floor.data.materials.append(floor_mat)
    floor.hide_select = True

    scene = bpy.context.scene
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.film_transparent = False
    scene.world.color = (0.015, 0.015, 0.015)
    renders = {}
    for pose, filename, book_bounds in (
        ("closed", "hands_with_book_closed.png", BOOK_CLOSED),
        ("open", "hands_with_book_open.png", BOOK_OPEN),
    ):
        set_viewmodel_pose(armature, pose == "open")
        hinge.rotation_mode = "XYZ"
        hinge.rotation_euler = (0.0, math.radians(-180.0 if pose == "open" else 0.0), 0.0)
        scene.frame_set(0)
        bpy.context.view_layer.update()
        collision = verify_no_penetration(hand_objects, book_bounds, pose == "open")
        camera_data.ortho_scale = 0.64 if pose == "closed" else 0.96
        camera.location.x = 0.20 if pose == "closed" else -0.05
        point_at(camera, Vector((0.0 if pose == "closed" else -0.11, 0.0, 0.008)))
        scene.render.filepath = str(output_dir / filename)
        bpy.ops.render.render(write_still=True)
        renders[pose] = {"path": filename, "book_envelope_test": collision}

    remove_set = book_objects + [camera, light, fill, floor]
    for obj in remove_set:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    if floor_mat.name in bpy.data.materials and floor_mat.users == 0:
        bpy.data.materials.remove(floor_mat)
    scene.camera = None
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image.users == 0 and not image.packed_file:
            bpy.data.images.remove(image)
    set_neutral_pose(armature)
    for track in armature.animation_data.nla_tracks:
        track.mute = False
    scene.frame_set(0)
    return renders


def read_glb(path: Path) -> dict:
    data = path.read_bytes()
    if data[:4] != b"glTF":
        raise RuntimeError("Hand export is not GLB")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError("Hand GLB is missing its JSON chunk")
    return json.loads(data[20:20 + json_length])


def build(output_dir: Path, book_glb: Path) -> None:
    root = project_root()
    cloth_texture = root / "game/assets/textures/painterly/urman_t01_v02_basecolor.png"
    for source in (cloth_texture, book_glb):
        if not source.is_file():
            raise FileNotFoundError(source)
    output_dir = output_dir.expanduser().resolve()
    if output_dir.exists():
        raise FileExistsError(f"Refusing to overwrite stage: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)
    book_glb = book_glb.expanduser().resolve()

    reset_scene()
    armature = create_rig()
    hand_objects = create_hands(armature, cloth_texture)
    clips = create_clips(armature)
    animation_pose_checks = validate_layout_animation_offsets(armature)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    closed_bounds = mesh_bounds(hand_objects)
    if any(not math.isfinite(value) for value in closed_bounds["size_xyz"]):
        raise RuntimeError("Non-finite closed-hold bounds")
    if closed_bounds["size_xyz"][0] > 1.0 or closed_bounds["size_xyz"][1] > 0.60:
        raise RuntimeError(f"Unexpectedly large neutral viewmodel bounds: {closed_bounds}")

    # Keep default pose exactly neutral before authoring the export, save, and
    # render the actual book as a temporary fixture after the hands-only GLB.
    export_objects = [armature] + hand_objects
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()

    blend_path = output_dir / "urman_book_hands.blend"
    glb_path = output_dir / "urman_book_hands.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path), export_format="GLB", use_selection=True,
        export_animations=True, export_nla_strips=True,
        export_force_sampling=True, export_current_frame=False, export_yup=True,
    )
    gltf = read_glb(glb_path)
    exported_names = [animation.get("name") for animation in gltf.get("animations", [])]
    expected_names = [clip["name"] for clip in clips]
    if set(exported_names) != set(expected_names) or len(exported_names) != 3:
        raise RuntimeError(f"Expected hold_closed/hold_open/return GLB clips; got {exported_names}")
    nodes = {node.get("name"): node for node in gltf.get("nodes", [])}
    expected_bones = {"Wrist.L", "Wrist.R", "FingerGroup.L", "FingerGroup.R", "Thumb.L", "Thumb.R"}
    if not expected_bones.issubset(nodes):
        raise RuntimeError(f"GLB is missing explicit hand/wrist bones: {expected_bones - set(nodes)}")
    if len(gltf.get("skins", [])) != 1:
        raise RuntimeError(f"Expected one actual skin, got {len(gltf.get('skins', []))}")
    for animation in gltf.get("animations", []):
        if not animation.get("channels") or not animation.get("samplers"):
            raise RuntimeError(f"Empty clip in GLB: {animation.get('name')}")

    # NLA tracks are muted only while rendering each named pose; the saved
    # hand-only source and GLB contain no book model or camera child.
    paper_texture = root / "game/assets/textures/painterly/book_paper_fibers_v1_albedo.png"
    if not paper_texture.is_file():
        raise FileNotFoundError(paper_texture)
    renders = render_fixture(output_dir, armature, hand_objects, book_glb,
                             cloth_texture, paper_texture)
    bpy.context.view_layer.objects.active = armature
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for obj in export_objects:
        obj.select_set(True)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    bones = []
    for bone in armature.data.bones:
        bones.append({"name": bone.name, "parent": bone.parent.name if bone.parent else None,
                      "head_local_m": list(bone.head_local), "tail_local_m": list(bone.tail_local),
                      "matrix_local": [list(row) for row in bone.matrix_local]})
    manifest = {
        "schema": "urman-book-hands-stage/v1",
        "status": "offline viewmodel asset stage; no runtime hand attachment or human acceptance claimed",
        "authored_form": "original winter sleeves and wool gloves; no actor body, head, or duplicated book in hand GLB",
        "seasonal_contract": "default is gloved; no automatic bare-hand switch; no glove-removal event is assumed",
        "axis_contract": "Blender X=book width, Y=book depth, Z=thickness; GLB Y-up",
        "book_fixture": {"path": str(book_glb), "sha256": sha256(book_glb),
                          "closed_envelope_min_xyz": list(BOOK_CLOSED[0]),
                          "closed_envelope_max_xyz": list(BOOK_CLOSED[1]),
                          "open_envelope_min_xyz": list(BOOK_OPEN[0]),
                          "open_envelope_max_xyz": list(BOOK_OPEN[1]),
                          "included_in_hand_glb": False},
        "maps": {
            "cloth": {"source_map": "game/assets/textures/painterly/urman_t01_v02_basecolor.png",
                      "sha256": sha256(cloth_texture), "tile_metres": TILE_METRES,
                      "used_by": ["WinterSleeveCloth", "WinterGloveCloth", "WinterCuffCloth"]},
            "skin": {"used": False,
                     "reason": "winter gloves fully cover the hands; adding exposed skin would contradict this pose"}
        },
        "bounds_m": {"closed_default_hands_actual": closed_bounds},
        "bones": bones,
        "clips": clips,
        "evaluated_animation_world_offsets": animation_pose_checks,
        "glb_animation_names": exported_names,
        "skin": {"count": len(gltf.get("skins", [])), "vertex_weights": "rigid 1.0 per viewmodel part; explicit wrist/finger/thumb groups"},
        "contact_checks": renders,
        "render_review": {"closed": "hands_with_book_closed.png", "open": "hands_with_book_open.png",
                          "camera": "orthographic; same imported book origin as offline fixture"},
        "source": {"script": "assets/source/blender/photoworlds/urman_book_hands.py",
                   "script_sha256": sha256(Path(__file__).resolve()),
                   "used_maps": ["game/assets/textures/painterly/urman_t01_v02_basecolor.png"]},
        "outputs": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path), "bytes": glb_path.stat().st_size}
        }
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"BOOK_HANDS_STAGE_OK blend={blend_path} glb={glb_path} animations={','.join(exported_names)}")


if __name__ == "__main__":
    args = parse_args()
    build(args.output_dir, args.book_glb)
