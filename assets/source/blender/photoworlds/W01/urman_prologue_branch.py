"""Build a finite, original W01 prologue branch asset stage.

Run with:
  blender --background --python assets/source/blender/photoworlds/W01/urman_prologue_branch.py \
    -- --output-dir /private/tmp/urman-prologue-branch-YYYYMMDD

The exported asset is a single moving 1.1 m branch, a center-grip anchor,
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
from mathutils import Vector


BRANCH_LENGTH = 1.100
PATH_WIDTH = 1.500
SIDE_CENTER_X = 1.400
SIDE_SUPPORT_TOP = 0.140
REST_BLOCKING_Z = 0.050
FPS = 24
TEXTURE_REPEAT_METRES = 1.0


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
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
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 600
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
    uv = nodes.new("ShaderNodeTexCoord")
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
    """Smooth irregular taper in metres, with the ground-facing surface at z=0."""
    t = (x + BRANCH_LENGTH * 0.5) / BRANCH_LENGTH
    keys = ((0.0, 0.010), (0.10, 0.032), (0.25, 0.037), (0.45, 0.031),
            (0.68, 0.025), (0.86, 0.017), (1.0, 0.008))
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
    z = radius - REST_BLOCKING_Z + 0.0025 * math.sin(8.0 * x + 0.6)
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
        reference = Vector((0.0, 0.0, 1.0))
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
    sample_count = 65
    xs = [-BRANCH_LENGTH * 0.5 + BRANCH_LENGTH * index / (sample_count - 1)
          for index in range(sample_count)]
    radii = []
    points = []
    for x in xs:
        y, z, radius = branch_profile(x)
        points.append((x, y, z))
        radii.append(radius)
    sweep_component(vertices, faces, uv_data, points, radii, sides=16)

    # One natural fork grows from the upper surface; it belongs to this same mesh/object.
    fork_x = 0.055
    fork_y, fork_z, fork_r = branch_profile(fork_x)
    fork_points = [
        (fork_x, fork_y, fork_z + fork_r * 0.78),
        (fork_x + 0.016, fork_y + 0.035, fork_z + fork_r * 1.35),
        (fork_x + 0.025, fork_y + 0.081, fork_z + 0.080),
        (fork_x + 0.011, fork_y + 0.121, fork_z + 0.112),
    ]
    fork_radii = (0.0105, 0.0090, 0.0062, 0.0028)
    sweep_component(vertices, faces, uv_data, fork_points, fork_radii, sides=10)

    mesh = bpy.data.meshes.new("PrologueBranch_BodyMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    layer = mesh.uv_layers.new(name="UVMap_metric_1m_repeat")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            layer.data[loop_index].uv = uv_data[vertex_index]
    mesh["uv_units"] = "metres; project bark receipt repeats every 1.0 m"
    mesh.materials.append(material)
    return mesh


def make_swept_object(name, vertices, faces, uv_data, material, location=(0.0, 0.0, 0.0)):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    layer = mesh.uv_layers.new(name="UVMap_metric_1m_repeat")
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


def make_support(material, name, location, side):
    vertices, faces, uv_data = [], [], []
    if side == "Left":
        points = [(-0.200, 0.045, 0.003), (-0.145, 0.034, 0.015),
                  (-0.080, 0.017, 0.036), (-0.035, 0.004, 0.074),
                  (0.000, 0.000, 0.132), (0.025, -0.011, 0.104),
                  (0.077, -0.027, 0.061), (0.140, -0.047, 0.021),
                  (0.195, -0.065, 0.003)]
        radii = (0.0025, 0.010, 0.016, 0.015, 0.010, 0.013, 0.017, 0.011, 0.0025)
    else:
        points = [(-0.195, -0.052, 0.003), (-0.150, -0.043, 0.014),
                  (-0.105, -0.022, 0.034), (-0.045, -0.006, 0.083),
                  (0.000, 0.000, 0.133), (0.035, 0.015, 0.100),
                  (0.088, 0.031, 0.052), (0.150, 0.049, 0.019),
                  (0.205, 0.061, 0.003)]
        radii = (0.0025, 0.009, 0.014, 0.015, 0.009, 0.012, 0.016, 0.010, 0.0025)
    points, radii = catmull_rom_samples(points, radii)
    sweep_component(vertices, faces, uv_data, points, radii, sides=12)
    z_min = min(vertex[2] for vertex in vertices)
    z_max = max(vertex[2] for vertex in vertices)
    z_scale = SIDE_SUPPORT_TOP / (z_max - z_min)
    vertices = [(x, y, (z - z_min) * z_scale) for x, y, z in vertices]
    return make_swept_object(name, vertices, faces, uv_data, material, location)


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
    for frame, location in keys:
        root.location = Vector(location)
        root.rotation_euler = (0.0, 0.0, 0.0)
        root.keyframe_insert(data_path="location", frame=frame, group=name)
        root.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
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


def add_render_fixture():
    """Temporary, offline-only ground and lighting used for the two review images."""
    scene = bpy.context.scene
    plane_mesh = bpy.data.meshes.new("RenderOnly_Ground_Mesh")
    plane_mesh.from_pydata([(-2.4, -1.5, -0.002), (2.4, -1.5, -0.002),
                            (2.4, 1.5, -0.002), (-2.4, 1.5, -0.002)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("RenderOnly_Ground", plane_mesh)
    scene.collection.objects.link(ground)
    ground.location.x = 0.65
    snow = bpy.data.materials.new("RenderOnly_NeutralSnow")
    snow.diffuse_color = (0.58, 0.62, 0.62, 1.0)
    snow.use_nodes = True
    snow.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (0.58, 0.62, 0.62, 1.0)
    snow.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = 0.94
    ground.data.materials.append(snow)
    ground["render_only"] = True

    # A muted 1.5 m route swatch makes the before/after clearance legible.
    route_mesh = bpy.data.meshes.new("RenderOnly_Path_Mesh")
    route_mesh.from_pydata([(-PATH_WIDTH / 2, -1.5, 0.0), (PATH_WIDTH / 2, -1.5, 0.0),
                            (PATH_WIDTH / 2, 1.5, 0.0), (-PATH_WIDTH / 2, 1.5, 0.0)], [], [(0, 1, 2, 3)])
    route = bpy.data.objects.new("RenderOnly_1p5m_Path", route_mesh)
    scene.collection.objects.link(route)
    route_mat = bpy.data.materials.new("RenderOnly_PathSurface")
    route_mat.diffuse_color = (0.33, 0.36, 0.36, 1.0)
    route_mat.use_nodes = True
    route_mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (0.33, 0.36, 0.36, 1.0)
    route_mat.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = 1.0
    route.data.materials.append(route_mat)
    route["render_only"] = True

    camera_data = bpy.data.cameras.new("RenderOnly_Camera")
    camera = bpy.data.objects.new("RenderOnly_Camera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    camera_data.lens = 45.0
    scene.camera = camera
    camera.location = (0.9, -3.2, 1.48)
    target = Vector((0.7, 0.0, 0.10))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()

    light_specs = (
        ("RenderOnly_Key", (0.0, -1.8, 2.5), 500.0, 2.0, (1.0, 0.86, 0.72)),
        ("RenderOnly_Fill", (1.8, 0.8, 1.6), 320.0, 1.5, (0.72, 0.84, 1.0)),
    )
    for name, location, power, size, color in light_specs:
        data = bpy.data.lights.new(name, type="AREA")
        data.energy = power
        data.shape = "DISK"
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (Vector((0.65, 0.0, 0.0)) - obj.location).to_track_quat("-Z", "Y").to_euler()
        obj["render_only"] = True
    scene.render.film_transparent = False
    return [obj for obj in scene.objects if obj.name.startswith("RenderOnly_")]


def render_pose(root, output_dir, pose_name, location):
    for track in root.animation_data.nla_tracks:
        track.mute = True
    root.location = location
    root.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    camera = bpy.context.scene.camera
    target = Vector((location[0], 0.0, 0.11))
    camera.location = (location[0] + 0.78, -3.2, 1.48)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
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


def build(output_dir: Path):
    root_path = project_root()
    texture_path, texture_provenance = verify_texture_receipt(root_path)
    output_dir = output_dir.expanduser().resolve()
    if output_dir.exists():
        raise FileExistsError(f"Refusing to overwrite stage: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)

    reset_scene()
    material = bark_material(texture_path)
    branch_mesh = make_branch_mesh(material)
    mesh_vertical_offset = -min(vertex.co.z for vertex in branch_mesh.vertices)
    for vertex in branch_mesh.vertices:
        vertex.co.z += mesh_vertical_offset
    branch_mesh.update()
    blocking_root = (0.0, 0.0, 0.0)
    moved_root = (SIDE_CENTER_X, 0.0, SIDE_SUPPORT_TOP)
    center_y, center_z, _ = branch_profile(0.0)
    grip_location = (0.0, center_y, center_z + mesh_vertical_offset)
    moving_root = new_empty("PrologueBranch_MovingRoot", blocking_root)
    moving_root["part_role"] = "single moving branch owner; object transforms only"
    body = bpy.data.objects.new("PrologueBranch_Body", branch_mesh)
    bpy.context.scene.collection.objects.link(body)
    body.parent = moving_root
    body.matrix_parent_inverse.identity()
    body.location = (0.0, 0.0, 0.0)
    body["part_role"] = "one tapered 1.1 m irregular branch mesh, including one natural fork"
    grip = new_empty("Branch_GripCenter", grip_location, moving_root)
    grip["part_role"] = "single center-grip pivot on the branch centerline at mid-length"

    support_locations = []
    for label, local_x in (("Left", -0.34), ("Right", 0.34)):
        x = SIDE_CENTER_X + local_x
        path_y, _, _ = branch_profile(local_x)
        support_locations.append((label, local_x, x, path_y))
        support = make_support(material, f"SIDE_RootSupport_{label}", (x, path_y, 0.0), label)
        support["contact_top_z_m"] = SIDE_SUPPORT_TOP

    clip_records = []
    clip_records.append(add_clip(moving_root, "rest_blocking", [
        (1, blocking_root), (12, blocking_root)], {
            "precondition": "branch begins across the 1.5 m route",
            "result": "branch rests on the route surface; grip anchor is on the branch centerline",
            "root_xyz_m": [0.0, 0.0, 0.0],
        }))
    clip_records.append(add_clip(moving_root, "rest_moved", [
        (1, moved_root), (12, moved_root)], {
            "precondition": "branch has been lifted clear of the route",
            "result": "branch rests on two static side-root supports beyond the route edge",
            "root_xyz_m": list(moved_root),
            "path_clearance_m": round(SIDE_CENTER_X - BRANCH_LENGTH * 0.5 - PATH_WIDTH * 0.5, 3),
    }))
    clip_records.append(add_clip(moving_root, "lift_and_place", [
        (1, blocking_root),
        (8, (0.06, 0.0, 0.31)),
        (18, (SIDE_CENTER_X, 0.0, 0.31)),
        (26, moved_root),
    ], {
        "precondition": "branch is in the blocking rest pose; center grip anchor is used by a later runtime owner",
        "result": "lift, clear the route edge, then lower onto the side-root supports",
        "root_xyz_m": [list(blocking_root), [0.06, 0.0, 0.31],
                       [SIDE_CENTER_X, 0.0, 0.31], list(moved_root)],
        "support_contact_top_z_m": SIDE_SUPPORT_TOP,
        "gameplay_effect": "none; no quest flag or skip behavior is implemented in this asset",
    }))

    for track in moving_root.animation_data.nla_tracks:
        track.mute = True
    render_objects = add_render_fixture()
    preview_paths = [
        render_pose(moving_root, output_dir, "branch_blocking", blocking_root),
        render_pose(moving_root, output_dir, "branch_moved", moved_root),
    ]
    for obj in list(render_objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for track in moving_root.animation_data.nla_tracks:
        track.mute = False

    moving_root.location = blocking_root
    moving_root.rotation_euler = (0.0, 0.0, 0.0)
    moving_root.scale = (1.0, 1.0, 1.0)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()

    branch_bounds = evaluated_bounds([body])
    branch_size = branch_bounds["size_blender_xyz_m"]
    if abs(branch_size[0] - BRANCH_LENGTH) > 0.003:
        raise RuntimeError(f"Branch evaluated length {branch_size[0]} m is not 1.1 m")
    if any(abs(s - 0.0) < 1e-9 for s in branch_size):
        raise RuntimeError(f"Branch has a degenerate mesh bounds: {branch_size}")
    if any(abs(actual - expected) > 1e-8 for actual, expected in zip(grip.location, grip_location)):
        raise RuntimeError("The grip anchor moved away from the measured centerline point")
    support_bounds = {obj.name: evaluated_bounds([obj]) for obj in bpy.context.scene.objects
                      if obj.name.startswith("SIDE_RootSupport_")}
    if len(support_bounds) != 2:
        raise RuntimeError("Expected exactly two static side-root support meshes")
    for name, bounds in support_bounds.items():
        if abs(bounds["max_blender_xyz_m"][2] - SIDE_SUPPORT_TOP) > 0.001:
            raise RuntimeError(f"{name} top is not at {SIDE_SUPPORT_TOP} m")

    bpy.ops.file.pack_all()
    blend_path = output_dir / "urman_prologue_branch.blend"
    glb_path = output_dir / "urman_prologue_branch.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    selected = [moving_root, body, grip, *[bpy.data.objects[name] for name in support_bounds]]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = moving_root
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_nla_strips=True,
        export_force_sampling=True,
        export_current_frame=False,
        export_yup=True,
    )
    if not glb_path.is_file():
        raise RuntimeError("glTF exporter did not create the GLB")
    gltf = glb_json(glb_path)
    node_names = [node.get("name", "") for node in gltf.get("nodes", [])]
    animations = sorted(animation.get("name", "") for animation in gltf.get("animations", []))
    expected_nodes = ("PrologueBranch_MovingRoot", "PrologueBranch_Body", "Branch_GripCenter",
                     "SIDE_RootSupport_Left", "SIDE_RootSupport_Right")
    for name in expected_nodes:
        if node_names.count(name) != 1:
            raise RuntimeError(f"GLB node {name!r} appeared {node_names.count(name)} times")
    expected_clips = sorted(("rest_blocking", "rest_moved", "lift_and_place"))
    if animations != expected_clips:
        raise RuntimeError(f"GLB clips {animations} do not match {expected_clips}")
    root_node = gltf["nodes"][node_names.index("PrologueBranch_MovingRoot")]
    default = root_node.get("translation", [0.0, 0.0, 0.0])
    # Blender Z-up to glTF Y-up remaps (x,y,z) to (x,z,-y).
    expected_default = [0.0, 0.0, 0.0]
    if any(abs(default[i] - expected_default[i]) > 0.001 for i in range(3)):
        raise RuntimeError(f"GLB default branch root {default} is not the blocking rest pose")

    manifest = {
        "schema": 1,
        "asset": "A-BRANCH / W01 F01",
        "stage_status": "offline asset export; runtime NOT_RUN; author review pending",
        "source": str(Path(__file__).resolve()),
        "source_sha256": sha256(Path(__file__).resolve()),
        "textures": [texture_provenance],
        "coordinate_contract": {
            "authoring": "Blender metres, X is branch length, Z is up; glTF exported Y-up",
            "branch_centerline_length_m": BRANCH_LENGTH,
            "route_clear_width_m": PATH_WIDTH,
            "blocking_root_blender_xyz_m": list(blocking_root),
            "moved_root_blender_xyz_m": list(moved_root),
            "moved_path_clearance_m": round(SIDE_CENTER_X - BRANCH_LENGTH * 0.5 - PATH_WIDTH * 0.5, 3),
            "side_support_contact_top_z_m": SIDE_SUPPORT_TOP,
            "grip_anchor": {"node": "Branch_GripCenter",
                            "root_local_xyz_m": [round(value, 6) for value in grip_location]},
        },
        "moving_geometry": {
            "root_node": "PrologueBranch_MovingRoot",
            "mesh_node": "PrologueBranch_Body",
            "mesh_count": 1,
            "evaluated_blocking_bounds": branch_bounds,
            "shape": "one irregular tapered branch with one small natural fork; no heavy log or duplicate branch",
        },
        "static_supports": support_bounds,
        "clips": clip_records,
        "render_previews": [
            {"path": path.name, "sha256": sha256(path)} for path in preview_paths
        ],
        "artifacts": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path), "bytes": glb_path.stat().st_size},
            "glb_node_names": node_names,
            "glb_animation_names": animations,
            "glb_default_moving_root_translation_yup_m": default,
        },
        "scope_limits": [
            "Render-only ground, route swatch, camera, and lights are absent from the GLB.",
            "No gameplay flag, prologue interaction, save/load behavior, or early-skip behavior is implemented.",
            "No runtime import, station run, or author visual acceptance is claimed.",
        ],
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "output_dir": str(output_dir),
        "blend": str(blend_path),
        "glb": str(glb_path),
        "manifest": str(manifest_path),
        "branch_bounds": branch_bounds,
        "clips": animations,
        "default_root_translation_yup_m": default,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    build(parse_args().output_dir)
