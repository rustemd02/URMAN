"""Build a finite, original W01 prologue branch asset stage.

Run with:
  blender --background --python assets/source/blender/photoworlds/W01/urman_prologue_branch.py \
    -- --output-dir /private/tmp/urman-prologue-branch-YYYYMMDD

The exported asset is a single moving 2.9 m bough, a center-grip anchor,
and two static side-root supports. Preview ground/camera/lights are never
exported. The clips describe object transforms only; they do not implement
gameplay state or a prologue skip path.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


BRANCH_LENGTH = 2.900
PATH_WIDTH = 1.500
SIDE_CENTER_X = 1.300
LOWER_SEAT_Z = 1.350
MOVED_ROOT = (-2.350, 0.0, -0.780)
MOVED_YAW = -math.pi / 2
SUPPORT_TRUNK_TOP_Z = 1.550
SUPPORT_ARM_HALF_SPAN_X = 0.050
BRANCH_CENTER_Z = 1.402
SUPPORT_INNER_EDGE_MIN_X = PATH_WIDTH * 0.5 + 0.350
FPS = 24
TEXTURE_REPEAT_METRES = 1.0
F01_WORLD_BLENDER = Vector((12.0, -50.0, 1.0))
F01_YAW = math.atan2(12.0, 50.0)
OUTGOING_TURN = math.atan2(33.0, 45.0) - F01_YAW
METRIC_UV = "UVMap_metric_1m_repeat"
GLOBAL_UV = "UVMap_Global_Atlas"


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--preview-only", action="store_true")
    return parser.parse_args(tail)


def project_root():
    return Path(__file__).resolve().parents[5]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.actions):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 26
    scene.frame_set(0)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.world.color = (0.14, 0.16, 0.18)


def verify_texture_receipt(root: Path):
    receipt_path = root / "game/assets/textures/painterly/procedural_bake_receipt_v1.json"
    texture_path = root / "game/assets/textures/painterly/bark_spruce_v1_albedo.png"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    entry = receipt["textures"]["F03"]
    relative = "game/assets/textures/painterly/bark_spruce_v1_albedo.png"
    actual_hash = sha256(texture_path)
    if entry.get("file") != relative or entry.get("licence") != "project-original procedural (no third-party source)":
        raise RuntimeError("The spruce-bark provenance receipt does not match the selected project texture")
    if entry.get("sha256") != actual_hash or entry.get("metres_per_repeat") != TEXTURE_REPEAT_METRES:
        raise RuntimeError("The spruce-bark receipt hash or metric repeat no longer matches the texture")
    return texture_path, {
        "path": relative,
        "sha256": actual_hash,
        "receipt": "game/assets/textures/painterly/procedural_bake_receipt_v1.json#textures.F03",
        "licence": entry["licence"],
        "metres_per_repeat": entry["metres_per_repeat"],
    }


def bark_material(texture_path: Path):
    material = bpy.data.materials.new("Bark_Spruce_ProjectOriginal")
    material.use_nodes = True
    material.diffuse_color = (0.30, 0.25, 0.23, 1.0)
    material["semantic_surface"] = "project-original spruce bark; reused map"
    material["texture_repeat_metres"] = TEXTURE_REPEAT_METRES
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (420, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (160, 0)
    shader.inputs["Roughness"].default_value = 0.88
    shader.inputs["Metallic"].default_value = 0.0
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.name = "bark_spruce_v1_albedo_source_map"
    image.colorspace_settings.name = "sRGB"
    image.pack()
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = METRIC_UV
    uv.location = (-620, 0)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.extension = "REPEAT"
    tex.location = (-380, 0)
    tint = nodes.new("ShaderNodeMixRGB")
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 0.72
    tint.inputs[2].default_value = (0.82, 0.82, 0.82, 1.0)
    tint.location = (-80, 0)
    links = material.node_tree.links
    links.new(uv.outputs["UV"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], tint.inputs[1])
    links.new(tint.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def branch_profile(x: float):
    """Smooth irregular taper in metres, raised to its authored blocking height."""
    t = (x + BRANCH_LENGTH * 0.5) / BRANCH_LENGTH
    keys = ((0.0, 0.008), (0.010, 0.030), (0.025, 0.050), (0.975, 0.050),
            (0.990, 0.028), (1.0, 0.008))
    for index in range(len(keys) - 1):
        ta, ra = keys[index]
        tb, rb = keys[index + 1]
        if t <= tb:
            u = max(0.0, min(1.0, (t - ta) / (tb - ta)))
            u = u * u * (3.0 - 2.0 * u)
            base = ra + (rb - ra) * u
            break
    else:
        base = keys[-1][1]
    ripple = 0.0010 * math.sin(17.0 * x + 0.4) + 0.0005 * math.sin(31.0 * x - 0.8)
    radius = max(0.007, base + ripple)
    y = 0.010 * math.sin(5.1 * x + 0.3) + 0.004 * math.sin(13.0 * x)
    y += 0.0025 * math.sin(27.0 * x + 0.2)
    z = BRANCH_CENTER_Z + 0.0005 * math.sin(8.0 * x + 0.6)
    # Freeze the grip and contact rings AND their tangent neighbours. Between
    # those seats the bough has two unequal bends rather than a straight bar.
    if 0.05 < abs(x) < 1.20:
        bend = math.sin(math.pi * (abs(x) - 0.05) / 1.15) ** 2
        radius_delta = bend * (0.025 if x < 0.0 else -0.012)
        radius += radius_delta
        y -= bend * (0.180 if x < 0.0 else 0.100)
        z += max(0.0, radius_delta) + bend * 0.045
    return y, z, radius


def sweep_component(vertices, faces, uv_data, points, radii, sides=12, texture_repeat=TEXTURE_REPEAT_METRES):
    """Append a tapered swept mesh. Points and radii are local metres."""
    points = [Vector(point) for point in points]
    base = len(vertices)
    arc = [0.0]
    for index in range(1, len(points)):
        arc.append(arc[-1] + (points[index] - points[index - 1]).length)
    circumferences = [2.0 * math.pi * radius for radius in radii]
    for index, (center, radius) in enumerate(zip(points, radii)):
        if index == 0:
            tangent = (points[1] - points[0]).normalized()
        elif index == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[index + 1] - points[index - 1]).normalized()
        # A near-vertical support stem needs a horizontal reference to keep
        # its circular sweep frame finite.
        reference = Vector((0.0, 1.0, 0.0)) if abs(tangent.z) > 0.95 else Vector((0.0, 0.0, 1.0))
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        for side in range(sides + 1):
            angle = math.tau * side / sides
            co = center + radius * (math.cos(angle) * normal + math.sin(angle) * binormal)
            vertices.append(tuple(co))
            uv_data.append((circumferences[index] * side / sides / texture_repeat,
                            arc[index] / texture_repeat))
    for index in range(len(points) - 1):
        row0 = base + index * (sides + 1)
        row1 = row0 + sides + 1
        for side in range(sides):
            faces.append((row0 + side, row0 + side + 1, row1 + side + 1, row1 + side))
    # End caps use their own radial UV islands instead of inheriting the bark side scan.
    for ring, center_index, reverse in ((0, 0, True), (len(points) - 1, len(points) - 1, False)):
        center = points[center_index]
        center_vertex = len(vertices)
        vertices.append(tuple(center))
        uv_data.append((0.0, 0.0))
        row = base + ring * (sides + 1)
        cap_ring = len(vertices)
        radius = radii[center_index]
        for side in range(sides + 1):
            angle = math.tau * side / sides
            vertices.append(vertices[row + side])
            uv_data.append((radius * math.cos(angle) / texture_repeat,
                            radius * math.sin(angle) / texture_repeat))
        for side in range(sides):
            face = (center_vertex, cap_ring + side, cap_ring + side + 1)
            faces.append(tuple(reversed(face)) if reverse else face)


def make_branch_mesh(material):
    vertices, faces, uv_data = [], [], []
    # 58 equal intervals put the ±1.30 m supports and their ±0.05 m fork
    # contacts directly on authored cross-section rings.
    sample_count = 59
    ring_vertex_count = 17
    xs = [-BRANCH_LENGTH * 0.5 + BRANCH_LENGTH * index / (sample_count - 1)
          for index in range(sample_count)]
    radii = []
    points = []
    for x in xs:
        y, z, radius = branch_profile(x)
        points.append((x, y, z))
        radii.append(radius)
    sweep_component(vertices, faces, uv_data, points, radii, sides=16)

    def ring_vertices(target_x):
        index = round((target_x + BRANCH_LENGTH * 0.5) / BRANCH_LENGTH * (sample_count - 1))
        if abs(xs[index] - target_x) > 1e-7:
            raise RuntimeError(f"Bough sample ring does not meet required contact x={target_x} m")
        start = index * ring_vertex_count
        return vertices[start:start + ring_vertex_count]

    center_ring = ring_vertices(0.0)
    grip_location = max(center_ring, key=lambda point: point[2])
    support_contacts = {"Left": [], "Right": []}
    for label, support_x in (("Left", -SIDE_CENTER_X), ("Right", SIDE_CENTER_X)):
        for arm_sign in (-1, 1):
            target_x = support_x + arm_sign * SUPPORT_ARM_HALF_SPAN_X
            contact = min(ring_vertices(target_x), key=lambda point: point[2])
            support_contacts[label].append({
                "arm_sign": arm_sign,
                "x": contact[0] - support_x,
                "y": contact[1],
                "z": contact[2],
                "branch_contact_xyz_m": list(contact),
            })

    # Unequal broken offshoot; the grip's original central ring stays intact.
    fork_x = -0.450
    fork_y, fork_z, fork_r = branch_profile(fork_x)
    fork_points = [
        (fork_x, fork_y, fork_z + fork_r * 0.78),
        (fork_x + 0.016, fork_y + 0.035, fork_z + fork_r * 1.35),
        (fork_x + 0.085, fork_y - 0.081, fork_z + 0.150),
        (fork_x + 0.140, fork_y - 0.121, fork_z + 0.175),
    ]
    fork_radii = (0.0240, 0.0190, 0.0120, 0.0060)
    sweep_component(vertices, faces, uv_data, fork_points, fork_radii, sides=10)

    # A single descending fork grows from the left stem and crosses the central
    # walking lane with actual wood below the 1.08 m crouched capsule crown.
    junction_y, junction_z, _ = branch_profile(-0.920)
    fork_points, fork_radii = catmull_rom_samples([
        (-0.920, junction_y, junction_z), (-1.030, -0.200, 1.180),
        (-1.100, -0.330, 1.040), (-0.680, -0.380, 1.000),
        (-0.200, -0.410, 1.010), (0.280, -0.390, 1.035),
        (0.720, -0.360, 1.045), (1.080, -0.330, 1.040),
    ], (0.059, 0.057, 0.048, 0.050, 0.044, 0.041, 0.035, 0.025), subdivisions=4)
    first_lower_vertex = len(vertices)
    sweep_component(vertices, faces, uv_data, fork_points, fork_radii, sides=12)
    floor_delta = 0.950 - min(point[2] for point in vertices[first_lower_vertex:])
    for index in range(first_lower_vertex, len(vertices)):
        x, y, z = vertices[index]
        vertices[index] = (x, y, z + floor_delta)

    mesh = bpy.data.meshes.new("PrologueBranch_BodyMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    layer = mesh.uv_layers.new(name=METRIC_UV)
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            layer.data[loop_index].uv = uv_data[vertex_index]
    mesh["uv_units"] = "metres; project bark receipt repeats every 1.0 m"
    mesh["descending_fork_first_vertex"] = first_lower_vertex
    mesh.materials.append(material)
    return mesh, grip_location, support_contacts


def make_swept_object(name, vertices, faces, uv_data, material, location=(0.0, 0.0, 0.0)):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    layer = mesh.uv_layers.new(name=METRIC_UV)
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            layer.data[loop_index].uv = uv_data[mesh.loops[loop_index].vertex_index]
    mesh["uv_units"] = "metres; project bark receipt repeats every 1.0 m"
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj["part_role"] = "static side-root support; not animated"
    return obj


def catmull_rom_samples(points, radii, subdivisions=6):
    path = [Vector(point) for point in points]

    def interpolate(a, b, c, d, t):
        return 0.5 * ((2.0 * b) + (-a + c) * t
                      + (2.0 * a - 5.0 * b + 4.0 * c - d) * (t * t)
                      + (-a + 3.0 * b - 3.0 * c + d) * (t * t * t))

    smooth_points = []
    smooth_radii = []
    for segment in range(len(path) - 1):
        p0 = path[max(0, segment - 1)]
        p1 = path[segment]
        p2 = path[segment + 1]
        p3 = path[min(len(path) - 1, segment + 2)]
        r0 = radii[max(0, segment - 1)]
        r1 = radii[segment]
        r2 = radii[segment + 1]
        r3 = radii[min(len(radii) - 1, segment + 2)]
        for step in range(subdivisions):
            t = step / subdivisions
            point = interpolate(p0, p1, p2, p3, t)
            radius = interpolate(r0, r1, r2, r3, t)
            smooth_points.append(tuple(point))
            smooth_radii.append(max(0.001, float(radius)))
    smooth_points.append(tuple(path[-1]))
    smooth_radii.append(max(0.001, float(radii[-1])))
    return smooth_points, smooth_radii


def make_support(material, name, location, side, lower_contacts, ground_height, all_contacts):
    vertices, faces, uv_data = [], [], []
    offset = Vector(location)
    expected_contacts = []

    def limb(points, radii, sides=12, subdivisions=3):
        points, radii = catmull_rom_samples(points, radii, subdivisions)
        sweep_component(vertices, faces, uv_data, points, radii, sides=sides)

    def foot(x, y, radius):
        return (x, y, ground_height(x, y) + radius - 0.020)

    def saddle(start, contact):
        tip = Vector(contact)
        start = Vector(start)
        limb([start, start.lerp(tip, 0.55),
              (tip.x, tip.y, tip.z - 0.045), tip],
             (0.070, 0.042, 0.022, 0.014), sides=10)
        expected_contacts.append(tuple(tip))

    # Static roots use their actual triangles: one convex hull between the high
    # and low seats would fill the empty space below the moved bough. All source
    # vertices remain outside both incoming/outgoing standing-lane half-planes.
    if side == "Left":
        limb([(-1.85, -0.55, ground_height(-1.85, -0.55) - 0.020),
              (-1.75, -0.45, 0.28), (-1.58, -0.25, 0.72),
              (-1.36, -0.10, 1.12), (-1.45, 0.00, 1.46)],
             (0.24, 0.22, 0.17, 0.13, 0.075))
        for end_y in (-1.70, 1.70):
            limb([foot(-1.85, -0.55, 0.19), foot(-2.05, -0.35, 0.16),
                  foot(-2.15, end_y * 0.55, 0.13), foot(-2.20, end_y, 0.045)],
                 (0.19, 0.16, 0.13, 0.045))
        limb([foot(-1.85, -0.55, 0.18), foot(-2.35, -0.85, 0.13),
              foot(-2.38, -1.05, 0.035)], (0.18, 0.13, 0.035), sides=10)
        for contact in lower_contacts:
            saddle((-1.42, -0.10, 1.10), contact["branch_contact_xyz_m"])
        moved_transform = Matrix.Translation(Vector(MOVED_ROOT)) @ Matrix.Rotation(MOVED_YAW, 4, "Z")
        # All four final contacts belong to this low, outboard root mass.
        for contacts in all_contacts.values():
            for contact in contacts:
                tip = moved_transform @ Vector(contact["branch_contact_xyz_m"])
                saddle((-2.05, tip.y, ground_height(-2.05, tip.y) + 0.22), tip)
    else:
        limb([(1.95, -0.70, ground_height(1.95, -0.70) - 0.020),
              (1.80, -0.60, 0.35), (1.62, -0.40, 0.85),
              (1.45, -0.22, 1.23), (1.58, -0.30, 1.43)],
             (0.22, 0.19, 0.14, 0.09, 0.055))
        for x, y in ((2.35, -0.80), (2.20, -1.05), (1.55, -0.60)):
            limb([foot(1.95, -0.70, 0.16), foot((1.95 + x) / 2, (-0.70 + y) / 2, 0.10),
                  foot(x, y, 0.025)], (0.16, 0.10, 0.025), sides=10)
        for contact in lower_contacts:
            saddle((1.47, -0.22, 1.16), contact["branch_contact_xyz_m"])

    # Build in asset coordinates so the grounded root profile follows the actual
    # terrain triangles. Preserve the original two static object origins.
    local_vertices = [tuple(Vector(point) - offset) for point in vertices]
    obj = make_swept_object(name, local_vertices, faces, uv_data, material, location)
    bpy.context.view_layer.update()
    for expected in expected_contacts:
        if min((obj.matrix_world @ vertex.co - Vector(expected)).length
               for vertex in obj.data.vertices) > 1e-6:
            raise RuntimeError(f"{name} misses its measured bough contact {expected}")
    return obj


def new_empty(name, location=(0.0, 0.0, 0.0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.025
    obj.location = location
    if parent is not None:
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
        obj.location = location
    return obj


def add_clip(root, name, keys, contract):
    action = bpy.data.actions.new(name=name)
    anim = root.animation_data_create()
    anim.action = action
    for frame, location, yaw in keys:
        root.location = Vector(location)
        root.rotation_euler = (0.0, 0.0, yaw)
        root.keyframe_insert(data_path="location", frame=frame, group=name)
        root.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"
    anim.action = None
    track = anim.nla_tracks.new()
    track.name = name
    track.mute = False
    strip = track.strips.new(name, keys[0][0], action)
    strip.action_frame_start = keys[0][0]
    strip.action_frame_end = keys[-1][0]
    strip.blend_type = "REPLACE"
    strip.extrapolation = "NOTHING"
    strip.influence = 1.0
    strip.use_auto_blend = False
    return {
        "name": name,
        "node": root.name,
        "start_frame": keys[0][0],
        "end_frame": keys[-1][0],
        "duration_seconds": round((keys[-1][0] - keys[0][0]) / FPS, 4),
        "contract": contract,
    }


def add_render_fixture(root_path):
    """Use the existing ground triangles, transformed into this asset's frame."""
    before = set(bpy.context.scene.objects)
    ground_path = root_path / "game/assets/photoworlds/forest_ground/prologue-ground-f00-f16-v1.glb"
    bpy.ops.import_scene.gltf(filepath=str(ground_path))
    imported = list(set(bpy.context.scene.objects) - before)
    world_to_asset = Matrix.Rotation(-F01_YAW, 4, "Z") @ Matrix.Translation(-F01_WORLD_BLENDER)
    old_world = {obj: obj.matrix_world.copy() for obj in imported}
    for obj in imported:
        obj.parent = None
        obj.matrix_world = world_to_asset @ old_world[obj]
        obj.name = "RenderOnly_" + obj.name
    bpy.context.view_layer.update()
    vertices, triangles = [], []
    ground_material = bpy.data.materials.new("RenderOnly_ExistingTerrain_Neutral")
    ground_material.use_nodes = True
    ground_shader = ground_material.node_tree.nodes.get("Principled BSDF")
    ground_shader.inputs["Roughness"].default_value = 1.0
    for obj in imported:
        if obj.type != "MESH":
            continue
        # The source GLB stores route masks as vertex colors; Godot applies its
        # painterly shader later. Display those same triangles quietly offline.
        if obj.data.color_attributes:
            colors = ground_material.node_tree.nodes.new("ShaderNodeVertexColor")
            colors.layer_name = obj.data.color_attributes[0].name
            channels = ground_material.node_tree.nodes.new("ShaderNodeSeparateColor")
            mix = ground_material.node_tree.nodes.new("ShaderNodeMixRGB")
            mix.inputs[1].default_value = (0.26, 0.29, 0.27, 1.0)
            mix.inputs[2].default_value = (0.34, 0.30, 0.25, 1.0)
            links = ground_material.node_tree.links
            links.new(colors.outputs["Color"], channels.inputs["Color"])
            links.new(channels.outputs["Red"], mix.inputs[0])
            links.new(mix.outputs["Color"], ground_shader.inputs["Base Color"])
        obj.data.materials.clear()
        obj.data.materials.append(ground_material)
        offset = len(vertices)
        vertices.extend(tuple(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)
        obj.data.calc_loop_triangles()
        triangles.extend(tuple(offset + index for index in triangle.vertices)
                         for triangle in obj.data.loop_triangles)
    ground = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

    def ground_height(x, y):
        hit, _, _, _ = ground.ray_cast(Vector((x, y, 10.0)), Vector((0, 0, -1)), 30.0)
        if hit is None:
            raise RuntimeError(f"Existing ground has no triangle at asset XY={x,y}")
        return hit.z

    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("RenderOnly_Approach_F02_Camera")
    camera = bpy.data.objects.new("RenderOnly_Approach_F02_Camera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.sensor_fit = "VERTICAL"
    camera_data.sensor_height = 24.0
    camera_data.lens = 24.0 / (2 * math.tan(math.radians(75.0) / 2))
    camera.location = (0.0, 6.50, ground_height(0.0, 6.50) + 1.70)
    # Fixed approach camera: horizontal direction follows the actual F02 bend,
    # with a small downward pitch to keep the low left root-rest in both frames.
    f02_local = world_to_asset @ Vector((45.0, -95.0, 3.0))
    forward = Vector((f02_local.x - camera.location.x,
                      f02_local.y - camera.location.y, 0.0)).normalized()
    forward.z = -math.tan(math.radians(8.0))
    camera.rotation_euler = forward.to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    for name, location, power, size in (
        ("Key", (-2.0, 3.0, 6.0), 1000.0, 5.0),
        ("Fill", (3.5, -1.0, 4.0), 550.0, 4.0),
    ):
        data = bpy.data.lights.new("RenderOnly_" + name, "AREA")
        data.energy, data.size = power, size
        light = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((-0.5, 0.0, 0.7)) - light.location).to_track_quat("-Z", "Y").to_euler()
    return ground_height, ground_path


def pack_global_atlas(objects):
    # Keep the proven metric bark preview on UV1; a new global UV0 is reserved
    # for one maps-only retexture of all three meshes after macro acceptance.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        metric = obj.data.uv_layers[METRIC_UV]
        metric.name = GLOBAL_UV
        duplicate = obj.data.uv_layers.new(name=METRIC_UV)
        for source, target in zip(metric.data, duplicate.data):
            target.uv = source.uv
        obj.data.uv_layers.active = metric
        metric.active_render = True
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=False, scale=True, margin=0.006, shape_method="AABB")
    bpy.ops.object.mode_set(mode="OBJECT")

    rectangles = []
    for obj in objects:
        mesh = obj.data
        neighbours = {}
        for polygon in mesh.polygons:
            for vertex in polygon.vertices:
                neighbours.setdefault(vertex, []).append(polygon.index)
        remaining = set(range(len(mesh.polygons)))
        while remaining:
            pending = [remaining.pop()]
            island = []
            while pending:
                face = pending.pop()
                island.append(face)
                for vertex in mesh.polygons[face].vertices:
                    for adjacent in neighbours[vertex]:
                        if adjacent in remaining:
                            remaining.remove(adjacent)
                            pending.append(adjacent)
            points = [mesh.uv_layers[GLOBAL_UV].data[index].uv.copy()
                      for face in island for index in mesh.polygons[face].loop_indices]
            rectangle = (min(p.x for p in points), min(p.y for p in points),
                         max(p.x for p in points), max(p.y for p in points))
            if min(rectangle) < -1e-6 or max(rectangle) > 1.000001:
                raise RuntimeError("Global atlas escaped its unique0–1 tile")
            for other in rectangles:
                if min(rectangle[2], other[2]) - max(rectangle[0], other[0]) > 1e-6 and min(rectangle[3], other[3]) - max(rectangle[1], other[1]) > 1e-6:
                    raise RuntimeError("Global atlas component/cap island rectangles overlap")
            rectangles.append(rectangle)
    return {"primary": GLOBAL_UV, "preview_metric_secondary": METRIC_UV,
            "global_islands": len(rectangles), "overlap": False,
            "grain_axis": "V follows each swept limb length; packing rotation disabled",
            "endcaps": "separate radial islands", "new_texture_generated": False}


def render_pose(root, output_dir, pose_name, location, yaw):
    for track in root.animation_data.nla_tracks:
        track.mute = True
    bpy.context.scene.frame_set(0)
    root.location = location
    root.rotation_euler = (0.0, 0.0, yaw)
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = str(output_dir / f"{pose_name}.png")
    bpy.ops.render.render(write_still=True)
    return output_dir / f"{pose_name}.png"


def evaluated_bounds(objects):
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        points.extend(evaluated.matrix_world @ vertex.co for vertex in mesh.vertices)
        evaluated.to_mesh_clear()
    if not points:
        raise RuntimeError("No evaluated mesh bounds are available")
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    return {"min_blender_xyz_m": [round(value, 6) for value in minimum],
            "max_blender_xyz_m": [round(value, 6) for value in maximum],
            "size_blender_xyz_m": [round(maximum[i] - minimum[i], 6) for i in range(3)]}


def glb_json(glb_path: Path):
    data = glb_path.read_bytes()
    if data[:4] != b"glTF":
        raise RuntimeError("Export did not produce a GLB container")
    json_length, chunk_type = struct.unpack_from("<II", data, 12)
    if chunk_type != 0x4E4F534A:
        raise RuntimeError("GLB has no JSON chunk")
    return json.loads(data[20:20 + json_length])


def build(output_dir: Path, preview_only=False):
    root_path = project_root()
    texture_path, texture_provenance = verify_texture_receipt(root_path)
    manifest_path = root_path / "assets/source/blender/photoworlds/W01/prologue-branch-v1/manifest.json"
    existing_manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output_dir = output_dir.expanduser().resolve()
    if output_dir.exists():
        raise FileExistsError(f"Refusing to overwrite stage: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)
    reset_scene()
    ground_height, ground_path = add_render_fixture(root_path)
    material = bark_material(texture_path)
    branch_mesh, grip_location, support_contacts = make_branch_mesh(material)
    moving_root = new_empty("PrologueBranch_MovingRoot")
    body = bpy.data.objects.new("PrologueBranch_Body", branch_mesh)
    bpy.context.scene.collection.objects.link(body)
    body.parent = moving_root
    grip = new_empty("Branch_GripCenter", grip_location, moving_root)
    supports = []
    for label, x in (("Left", -SIDE_CENTER_X), ("Right", SIDE_CENTER_X)):
        supports.append(make_support(material, f"SIDE_RootSupport_{label}", (x, 0, 0),
                                     label, support_contacts[label], ground_height, support_contacts))
    bpy.context.view_layer.update()
    objects = [body, *supports]
    atlas_contract = pack_global_atlas(objects)
    triangle_count = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in objects)
    if triangle_count > 7344 or len(objects) != 3:
        raise RuntimeError(f"F01 exceeds its3mesh/7344triangle cap: {triangle_count}")

    old_contract = existing_manifest["coordinate_contract"]
    for side, contacts in support_contacts.items():
        old_points = old_contract["support_seat_contacts"][side]["blocking_contact_points_local_m"]
        for contact, old_point in zip(contacts, old_points):
            if math.dist(contact["branch_contact_xyz_m"], old_point) > 1e-6:
                raise RuntimeError(f"An original {side} main contact ring moved")
    if math.dist(grip_location, old_contract["grip_anchor"]["root_local_xyz_m"]) > 1e-6:
        raise RuntimeError(f"The original center grip moved: {grip_location}")
    branch_bounds = evaluated_bounds([body])
    if abs(branch_bounds["size_blender_xyz_m"][0] - BRANCH_LENGTH) > 0.003:
        raise RuntimeError("The bough no longer spans2.900m")
    lower_start = branch_mesh["descending_fork_first_vertex"]
    main_floor = min(vertex.co.z for vertex in branch_mesh.vertices[:lower_start])
    if abs(main_floor - 1.350) > 0.015 or abs(branch_bounds["min_blender_xyz_m"][2] - 0.950) > 1e-5:
        raise RuntimeError("Main seat or visible descending-fork floor changed")
    if branch_bounds["max_blender_xyz_m"][2] > 1.700:
        raise RuntimeError("The broken bough exceeds its1.700m top envelope")

    for support in supports:
        for vertex in support.data.vertices:
            point = support.matrix_world @ vertex.co
            local_z = -point.y
            if support == supports[0]:
                safe = point.x <= -1.10
            else:
                outgoing = point.x * math.cos(OUTGOING_TURN) - local_z * math.sin(OUTGOING_TURN)
                world = Matrix.Rotation(F01_YAW, 4, "Z") @ point + F01_WORLD_BLENDER
                safe = point.x >= 1.10 and outgoing >= 1.10 and -world.y >= 49.60
            if not safe:
                raise RuntimeError(f"{support.name} enters a standing lane/F11core: {tuple(point)}")
    support_bounds = {support.name: evaluated_bounds([support]) for support in supports}
    if max(bounds["max_blender_xyz_m"][2] for bounds in support_bounds.values()) > 1.570:
        raise RuntimeError("A support became a tall repeated post again")

    # The moving crouched/standing capsule must hit SOURCE WOOD for every
    # sampled center across the1.5m walking lane, before considering its hull.
    fork_vertices = [vertex.co for vertex in branch_mesh.vertices[lower_start:]]
    capsule_distances = {}
    for height in (1.08, 1.80):
        worst = 0.0
        for step in range(31):
            x = -0.75 + 1.50 * step / 30
            distance = math.inf
            for point in fork_vertices:
                floor = ground_height(x, point.y)
                axis_z = max(floor + 0.35, min(point.z, floor + height - 0.35))
                distance = min(distance, math.hypot(point.x - x, point.z - axis_z))
            worst = max(worst, distance)
        if worst >= 0.335:
            raise RuntimeError(f"The{height}m capsule can cross the sampled lane without source-wood contact: {worst}")
        capsule_distances[str(height)] = round(worst, 6)
    low_wood = [point.x for point in fork_vertices if point.z <= 1.080]
    if max(low_wood) - min(low_wood) < 2.10:
        raise RuntimeError("The low descending fork does not span2.10m of actual wood")

    moved_matrix = Matrix.Translation(Vector(MOVED_ROOT)) @ Matrix.Rotation(MOVED_YAW, 4, "Z")
    moved_points = [moved_matrix @ vertex.co for vertex in branch_mesh.vertices]
    final_ground_gap = min(point.z - ground_height(point.x, point.y) for point in moved_points)
    if final_ground_gap < 0.080 or max(point.x for point in moved_points) > -1.10:
        raise RuntimeError(f"Moved wood enters ground/standing corridor; gap={final_ground_gap}")
    final_seats = [list(moved_matrix @ Vector(contact["branch_contact_xyz_m"]))
                   for contacts in support_contacts.values() for contact in contacts]
    body_faces = [tuple(face.vertices) for face in body.data.polygons]
    body_surface = BVHTree.FromPolygons(moved_points, body_faces)
    surface_touches = {}
    for support in supports:
        points = [support.matrix_world @ vertex.co for vertex in support.data.vertices]
        faces = [tuple(face.vertices) for face in support.data.polygons]
        surface = BVHTree.FromPolygons(points, faces)
        overlaps = body_surface.overlap(surface)
        allowed = final_seats if support == supports[0] else []
        for body_face, support_face in overlaps:
            first = [moved_points[index] for index in body_faces[body_face]]
            second = [points[index] for index in faces[support_face]]
            center = Vector([(max(min(point[axis] for point in first), min(point[axis] for point in second))
                              + min(max(point[axis] for point in first), max(point[axis] for point in second))) / 2
                             for axis in range(3)])
            if not any(math.hypot(center.x - seat[0], center.y - seat[1]) <= 0.025
                       and abs(center.z - seat[2]) <= 0.004 for seat in allowed):
                raise RuntimeError(f"{support.name}: actual wood surfaces intersect away from a final seat: {tuple(center)}")
        surface_touches[support.name] = len(overlaps)

    blocking = (0.0, 0.0, 0.0)
    pose_contract = {"blocking_godot_position": [0, 0, 0], "blocking_yaw_degrees": 0,
                     "moved_godot_position": [-2.35, -0.78, 0], "moved_yaw_degrees": -90,
                     "final_main_seats_blender_xyz_m": final_seats}
    clip_records = [
        add_clip(moving_root, "rest_blocking", [(1, blocking, 0), (12, blocking, 0)], pose_contract),
        add_clip(moving_root, "rest_moved", [(1, MOVED_ROOT, MOVED_YAW), (12, MOVED_ROOT, MOVED_YAW)], pose_contract),
        add_clip(moving_root, "lift_and_place", [
            (1, blocking, 0), (6, (0, 0, 0.25), 0),
            (14, (-3.0, 0, 0.25), 0), (20, (-3.0, 0, 0.25), MOVED_YAW),
            (26, MOVED_ROOT, MOVED_YAW)], pose_contract),
    ]
    print(json.dumps({"source_geometry": {"triangles": triangle_count, "blocking_bounds": branch_bounds,
          "final_ground_clearance_m": final_ground_gap, "capsule_source_vertex_distance_m": capsule_distances,
          "uv": atlas_contract, "final_surface_contact_pairs": surface_touches}, "poses": pose_contract}, ensure_ascii=False), flush=True)
    preview_paths = [render_pose(moving_root, output_dir, "branch_blocking", blocking, 0),
                     render_pose(moving_root, output_dir, "branch_moved", MOVED_ROOT, MOVED_YAW)]
    for obj in list(bpy.data.objects):
        if obj.name.startswith("RenderOnly_"):
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.scene.camera = None
    bpy.data.orphans_purge(do_recursive=True)
    for track in moving_root.animation_data.nla_tracks:
        track.mute = False
    bpy.context.scene.frame_set(0)
    moving_root.location, moving_root.rotation_euler = blocking, (0, 0, 0)
    bpy.context.view_layer.update()
    blend_path = output_dir / "urman_prologue_branch.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False, compress=True)
    candidate = {
        "stage": "source candidate; whole-composition review pending; runtime unchanged",
        "source_sha256": sha256(Path(__file__).resolve()),
        "triangles": triangle_count, "mesh_count": 3,
        "blocking_bounds": branch_bounds, "static_supports": support_bounds,
        "main_body_bottom_m": round(main_floor, 6), "descending_fork_bottom_m": 0.95,
        "descending_fork_low_wood_span_m": round(max(low_wood) - min(low_wood), 6),
        "capsule_source_vertex_distance_m": capsule_distances,
        "minimum_final_wood_ground_gap_m": round(final_ground_gap, 6),
        "ground_source": str(ground_path.relative_to(root_path)), "ground_sha256": sha256(ground_path),
        "pose_contract": pose_contract, "clips": clip_records, "uv": atlas_contract,
        "final_surface_contact_pairs": surface_touches,
        "static_collision_contract": "actual triangle surfaces; one convex hull cannot represent the high/low root seats",
        "blend": {"path": str(blend_path), "sha256": sha256(blend_path)},
        "previews": [{"path": str(path), "sha256": sha256(path)} for path in preview_paths],
        "scope_limits": ["No claim that every forest bypass is prevented.",
                         "Source wood-surface checks do not prove native motion or author art acceptance.",
                         "Existing runtime GLB/manifest artifact hashes remain the earlier stage until explicit promotion."]
    }
    existing_manifest["candidate_stage"] = candidate
    manifest_path.write_text(json.dumps(existing_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if preview_only:
        print(json.dumps({"preview_only": True, "manifest": str(manifest_path), "candidate": candidate}, ensure_ascii=False), flush=True)
        return

    # Export only the three asset meshes and their existing root/grip owners.
    for track in moving_root.animation_data.nla_tracks:
        track.mute = False
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (moving_root, grip, *objects):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = moving_root
    glb_path = output_dir / "urman_prologue_branch.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True,
                             export_animations=True, export_animation_mode="ACTIONS", export_nla_strips=True,
                             export_force_sampling=True, export_yup=True)
    gltf = glb_json(glb_path)
    names = [node.get("name", "") for node in gltf.get("nodes", [])]
    expected = ["PrologueBranch_MovingRoot", "PrologueBranch_Body", "Branch_GripCenter",
                "SIDE_RootSupport_Left", "SIDE_RootSupport_Right"]
    if any(names.count(name) != 1 for name in expected) or sorted(animation.get("name") for animation in gltf.get("animations", [])) != sorted(record["name"] for record in clip_records):
        raise RuntimeError("Export changed the existing node/clip contract")
    print(json.dumps({"candidate_glb": str(glb_path), "sha256": sha256(glb_path)}, ensure_ascii=False))


if __name__ == "__main__":
    args = parse_args()
    build(args.output_dir, args.preview_only)
