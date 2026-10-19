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
from mathutils import Vector


BRANCH_LENGTH = 2.900
PATH_WIDTH = 1.500
SIDE_CENTER_X = 1.300
LOWER_SEAT_Z = 1.350
UPPER_SEAT_Z = 2.200
SUPPORT_LIFT_DELTA_Z = 0.850
SUPPORT_TRUNK_TOP_Z = 2.320
SUPPORT_ARM_HALF_SPAN_X = 0.050
BRANCH_CENTER_Z = 1.402
SUPPORT_INNER_EDGE_MIN_X = PATH_WIDTH * 0.5 + 0.350
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
    scene.render.engine = "BLENDER_EEVEE_NEXT"
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

    # One natural fork grows from the upper surface; it belongs to this same mesh/object.
    fork_x = 0.055
    fork_y, fork_z, fork_r = branch_profile(fork_x)
    fork_points = [
        (fork_x, fork_y, fork_z + fork_r * 0.78),
        (fork_x + 0.016, fork_y + 0.035, fork_z + fork_r * 1.35),
        (fork_x + 0.025, fork_y + 0.081, fork_z + 0.200),
        (fork_x + 0.011, fork_y + 0.121, fork_z + 0.245),
    ]
    fork_radii = (0.0240, 0.0190, 0.0120, 0.0060)
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
    return mesh, grip_location, support_contacts


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


def make_support(material, name, location, side, lower_contacts):
    vertices, faces, uv_data = [], [], []
    side_sign = -1.0 if side == "Left" else 1.0

    # Rooted crooked sapling just beyond the lane. Its open forks reach toward
    # the bough at both the blocking and overhead resting heights.
    depth_bias = -0.030 if side == "Left" else 0.040
    trunk_points = [
        (0.000, 0.340 + depth_bias, 0.000),
        (0.018 * side_sign, 0.300 + depth_bias, 0.260),
        (0.055 * side_sign, 0.230 + depth_bias, 0.690),
        (0.015 * side_sign, 0.150 + depth_bias, 1.120),
        (-0.045 * side_sign, 0.120 + depth_bias, 1.560),
        (-0.020 * side_sign, 0.200 + depth_bias, 1.980),
        (0.035 * side_sign, 0.280 + depth_bias, 2.200),
        (0.060 * side_sign, 0.300 + depth_bias, SUPPORT_TRUNK_TOP_Z),
    ]
    trunk_radii = (0.112, 0.096, 0.077, 0.064, 0.052, 0.042, 0.034, 0.024)
    control_points, control_radii = trunk_points, trunk_radii
    root_x, root_y, root_z = control_points[0]
    # Keep the authored curved root while tilting its end cap only enough for
    # the lowest vertex of the first ring to touch local ground at z=0.
    for _ in range(24):
        adjusted_points = list(control_points)
        adjusted_points[0] = (root_x, root_y, root_z)
        sampled_points, sampled_radii = catmull_rom_samples(
            adjusted_points, control_radii, subdivisions=5)
        tangent = (Vector(sampled_points[1]) - Vector(sampled_points[0])).normalized()
        reference = Vector((0.0, 1.0, 0.0)) if abs(tangent.z) > 0.95 else Vector((0.0, 0.0, 1.0))
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        ground_contact_z = -min(
            control_radii[0] * (math.cos(math.tau * side / 16) * normal.z
                                + math.sin(math.tau * side / 16) * binormal.z)
            for side in range(17))
        if abs(ground_contact_z - root_z) <= 1e-12:
            trunk_points, trunk_radii = sampled_points, sampled_radii
            break
        root_z = ground_contact_z
    else:
        raise RuntimeError(f"{name} root-endcap ground contact did not converge")
    sweep_component(vertices, faces, uv_data, trunk_points, trunk_radii, sides=16)

    expected_contacts = []

    def add_seat_fork(contact, lift_delta):
        seat_z = contact["z"] + lift_delta
        origin_z = seat_z - 0.245
        start_point = None
        for first, second in zip(trunk_points, trunk_points[1:]):
            if first[2] <= origin_z <= second[2]:
                ratio = (origin_z - first[2]) / (second[2] - first[2])
                start_point = Vector(first).lerp(Vector(second), ratio)
                break
        if start_point is None:
            raise RuntimeError(f"No trunk section supports the {seat_z} m fork seat")
        start_x, start_y, _ = start_point
        arm_sign = float(contact["arm_sign"])
        tip_x, tip_y = contact["x"], contact["y"]
        points = [
            (start_x, start_y, origin_z),
            (start_x + arm_sign * 0.025, start_y * 0.65 + tip_y * 0.35, seat_z - 0.140),
            (tip_x, tip_y, seat_z - 0.060),
            (tip_x, tip_y, seat_z),
        ]
        radii = (0.044, 0.032, 0.020, 0.014)
        points, radii = catmull_rom_samples(points, radii, subdivisions=5)
        sweep_component(vertices, faces, uv_data, points, radii, sides=12)
        expected_contacts.append((tip_x, tip_y, seat_z))

    for contact in lower_contacts:
        add_seat_fork(contact, 0.0)
        add_seat_fork(contact, SUPPORT_LIFT_DELTA_Z)

    obj = make_swept_object(name, vertices, faces, uv_data, material, location)
    base_min_z = min(vertex.co.z for vertex in obj.data.vertices)
    if abs(base_min_z) > 1e-6:
        raise RuntimeError(f"{name} swept root base does not touch local ground at z=0: {base_min_z} m")
    # Each fork has a flat-ended terminal vertex at an actual underside vertex
    # from the branch mesh; the moved copy is exactly the authored .85 m higher.
    for expected in expected_contacts:
        if min((vertex.co - Vector(expected)).length for vertex in obj.data.vertices) > 1e-6:
            raise RuntimeError(f"{name} fork does not touch its measured branch contact {expected}")
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
    # Frame the complete bough, both rooted supports, and ground with margin.
    camera_data.ortho_scale = 4.80
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
    highest_part = max(SUPPORT_TRUNK_TOP_Z, BRANCH_CENTER_Z + 0.250 + location[2])
    target = Vector((location[0], 0.0, highest_part * 0.5))
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
    branch_mesh, grip_location, support_contacts = make_branch_mesh(material)
    # Preserve the authored rest height; normalizing this mesh to its floor
    # would erase the head-height blocking pose and move the grip to ground.
    branch_mesh.update()
    blocking_root = (0.0, 0.0, 0.0)
    moved_root = (0.0, 0.0, SUPPORT_LIFT_DELTA_Z)
    moving_root = new_empty("PrologueBranch_MovingRoot", blocking_root)
    moving_root["part_role"] = "single moving branch owner; object transforms only"
    body = bpy.data.objects.new("PrologueBranch_Body", branch_mesh)
    bpy.context.scene.collection.objects.link(body)
    body.parent = moving_root
    body.matrix_parent_inverse.identity()
    body.location = (0.0, 0.0, 0.0)
    body["part_role"] = "one tapered 2.9 m irregular bough, including one natural fork"
    grip = new_empty("Branch_GripCenter", grip_location, moving_root)
    grip["part_role"] = "visible upper-surface grip point near the bough centerline"

    for label, x in (("Left", -SIDE_CENTER_X), ("Right", SIDE_CENTER_X)):
        contacts = support_contacts[label]
        support = make_support(material, f"SIDE_RootSupport_{label}", (x, 0.0, 0.0), label, contacts)
        support["lower_seat_z_m"] = [round(contact["z"], 6) for contact in contacts]
        support["upper_seat_z_m"] = [round(contact["z"] + SUPPORT_LIFT_DELTA_Z, 6) for contact in contacts]
        support["branch_contact_points_local_m"] = [contact["branch_contact_xyz_m"] for contact in contacts]
        support["part_role"] = "rooted forked sapling with lower blocking and upper moved-branch seats"

    seat_contact_contract = {
        side: {
            "lower_contact_z_m": [round(contact["z"], 6) for contact in contacts],
            "upper_contact_z_m": [round(contact["z"] + SUPPORT_LIFT_DELTA_Z, 6) for contact in contacts],
            "lower_contact_points_local_m": [contact["branch_contact_xyz_m"] for contact in contacts],
            "upper_contact_points_local_m": [
                [contact["branch_contact_xyz_m"][0],
                 contact["branch_contact_xyz_m"][1],
                 contact["branch_contact_xyz_m"][2] + SUPPORT_LIFT_DELTA_Z]
                for contact in contacts
            ],
        }
        for side, contacts in support_contacts.items()
    }

    clip_records = []
    clip_records.append(add_clip(moving_root, "rest_blocking", [
        (1, blocking_root), (12, blocking_root)], {
            "precondition": "bough begins across the 1.5 m route at head height",
            "result": "bough rests in two grounded lower fork seats; grip anchor is on the visible upper surface near centerline",
            "root_xyz_m": [0.0, 0.0, 0.0],
            "measured_support_contacts": seat_contact_contract,
        }))
    clip_records.append(add_clip(moving_root, "rest_moved", [
        (1, moved_root), (12, moved_root)], {
            "precondition": "bough has been lifted above the standing lane",
            "result": "bough rests in two grounded upper fork seats beyond the lane edges",
            "root_xyz_m": list(moved_root),
            "measured_support_contacts": seat_contact_contract,
    }))
    clip_records.append(add_clip(moving_root, "lift_and_place", [
        (1, blocking_root),
        (8, (0.0, 0.0, 0.50)),
        (18, (0.0, 0.0, 1.10)),
        (26, moved_root),
    ], {
        "precondition": "bough is in the blocking rest pose; runtime uses the visible center grip anchor",
        "result": "lift above the standing lane, then lower into the upper rooted fork seats",
        "root_xyz_m": [list(blocking_root), [0.0, 0.0, 0.50],
                       [0.0, 0.0, 1.10], list(moved_root)],
        "measured_support_contacts": seat_contact_contract,
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
        raise RuntimeError(f"Bough evaluated length {branch_size[0]} m is not 2.9 m")
    if abs(branch_bounds["min_blender_xyz_m"][2] - LOWER_SEAT_Z) > 0.015:
        raise RuntimeError("Blocking bough bottom no longer meets its 1.35 m lower-seat profile")
    if not 1.60 <= branch_bounds["max_blender_xyz_m"][2] <= 1.70:
        raise RuntimeError("Blocking bough and fork must remain within the authored 1.60-1.70 m top band")
    if any(abs(s - 0.0) < 1e-9 for s in branch_size):
        raise RuntimeError(f"Branch has a degenerate mesh bounds: {branch_size}")
    if any(abs(actual - expected) > 1e-8 for actual, expected in zip(grip.location, grip_location)):
        raise RuntimeError("The grip anchor moved away from the measured centerline point")
    if abs(grip.location.z - 1.45) > 0.02:
        raise RuntimeError("The visible center grip must remain near local height 1.45 m")
    if not any(math.dist(grip.location, vertex.co) <= 1e-6 for vertex in branch_mesh.vertices):
        raise RuntimeError("The grip anchor is not on a source bough vertex")
    support_bounds = {obj.name: evaluated_bounds([obj]) for obj in bpy.context.scene.objects
                      if obj.name.startswith("SIDE_RootSupport_")}
    if len(support_bounds) != 2:
        raise RuntimeError("Expected exactly two static side-root support meshes")
    for name, bounds in support_bounds.items():
        root_min_z = bounds["min_blender_xyz_m"][2]
        if abs(root_min_z) > 0.002:
            raise RuntimeError(f"{name} root base must touch local ground at z=0; measured {root_min_z} m")
        if abs(bounds["max_blender_xyz_m"][2] - SUPPORT_TRUNK_TOP_Z) > 0.025:
            raise RuntimeError(f"{name} rooted sapling does not reach its authored top")
    left_inner_edge = abs(support_bounds["SIDE_RootSupport_Left"]["max_blender_xyz_m"][0])
    right_inner_edge = support_bounds["SIDE_RootSupport_Right"]["min_blender_xyz_m"][0]
    if min(left_inner_edge, right_inner_edge) < SUPPORT_INNER_EDGE_MIN_X:
        raise RuntimeError("A rooted support enters the standing-lane clearance band")
    for side_contacts in support_contacts.values():
        for contact in side_contacts:
            if abs(contact["z"] - LOWER_SEAT_Z) > 0.015:
                raise RuntimeError(f"Measured lower bough contact {contact['z']} m drifted from the 1.35 m blocking seat")
            if abs((contact["z"] + SUPPORT_LIFT_DELTA_Z) - UPPER_SEAT_Z) > 0.015:
                raise RuntimeError("Upper support contact is not exactly the lower seat plus the .85 m lift")
    final_bough_min = branch_bounds["min_blender_xyz_m"][2] + moved_root[2]
    if final_bough_min < 2.15:
        raise RuntimeError(f"Final bough underside {final_bough_min} m is below the 2.15 m clearance passport")

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
        "stage_status": "offline PW-046 restage export; runtime NOT_RUN; author review pending",
        "production_metric_delta": {
            "supersedes": "PW-045 1.10 m light-branch production metric",
            "previous_centerline_length_m": 1.100,
            "current_centerline_length_m": BRANCH_LENGTH,
            "reason": "intentional first-action restage: visible head-height bough with a raised, physically supported final pose",
            "previous_metric_pass_reused": False,
        },
        "source": str(Path(__file__).resolve()),
        "source_sha256": sha256(Path(__file__).resolve()),
        "textures": [texture_provenance],
        "coordinate_contract": {
            "authoring": "Blender metres, X is branch length, Z is up; glTF exported Y-up",
            "asset_local_ground_z_m": 0.0,
            "F01_scene_anchor_world_y_m": 1.0,
            "branch_centerline_length_m": BRANCH_LENGTH,
            "route_clear_width_m": PATH_WIDTH,
            "blocking_bough_z_m": [round(branch_bounds["min_blender_xyz_m"][2], 3), round(branch_bounds["max_blender_xyz_m"][2], 3)],
            "grip_center_z_m": round(grip.location.z, 3),
            "nominal_lower_support_seat_z_m": LOWER_SEAT_Z,
            "nominal_upper_support_seat_z_m": UPPER_SEAT_Z,
            "support_seat_contacts": seat_contact_contract,
            "blocking_root_blender_xyz_m": list(blocking_root),
            "moved_root_blender_xyz_m": list(moved_root),
            "final_bough_bottom_z_m": round(final_bough_min, 3),
            "final_standing_clearance_m": round(final_bough_min, 3),
            "support_inner_edge_min_x_m": round(min(left_inner_edge, right_inner_edge), 3),
            "grip_anchor": {"node": "Branch_GripCenter",
                            "root_local_xyz_m": [round(value, 6) for value in grip_location]},
        },
        "moving_geometry": {
            "root_node": "PrologueBranch_MovingRoot",
            "mesh_node": "PrologueBranch_Body",
            "mesh_count": 1,
            "evaluated_blocking_bounds": branch_bounds,
            "shape": "one light irregular 2.9 m bough with one natural fork; geometry rests at local 1.35-1.65 m in the blocking pose",
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
