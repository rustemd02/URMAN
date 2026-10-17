"""Build an original, finite W01 snagged satchel asset (Blender 4.5).

Run from the repository root:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python assets/source/blender/photoworlds/W01/bag_v1.py

The default pose catches one continuous strap on a root-contact marker. The
only whole-object movement owner is BagRoot; the three exported clips deform
the strap mesh and never move or duplicate the bag. Render-only props are not
exported. This asset contains no W01 gameplay flags or encounter logic.
"""

from __future__ import annotations

import hashlib
import json
import math
import random
import struct
from array import array
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[5]
SOURCE_DIR = Path(__file__).resolve().parent
RUNTIME_DIR = ROOT / "game/assets/photoworlds/bag"
SCRIPT_PATH = Path(__file__).resolve()
BLEND_OUT = SOURCE_DIR / "bag-v1.blend"
MANIFEST_OUT = SOURCE_DIR / "bag-v1.manifest.json"
SNAGGED_PREVIEW_OUT = SOURCE_DIR / "bag-v1-preview-snagged.png"
PLACED_PREVIEW_OUT = SOURCE_DIR / "bag-v1-preview-placed.png"
GLB_OUT = RUNTIME_DIR / "bag-v1.glb"
TEXTURE_OUT = RUNTIME_DIR / "bag-cloth-1k.png"

FPS = 24
BODY_SIZE_GODOT_M = (0.30, 0.20, 0.12)
TEXTURE_SIZE = 1024
TEXTURE_REPEAT_M = 0.085

RUNTIME_COLLECTION = None
RENDER_COLLECTION = None


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def godot_xyz_to_blender(x: float, y: float, z: float):
    """Project convention: Godot (X,Y,Z) -> Blender (X,-Z,Y), in metres."""
    return (x, -z, y)


def blender_xyz_to_godot(point):
    return [round(float(point[0]), 6), round(float(point[2]), 6), round(float(-point[1]), 6)]


def reset_scene():
    global RUNTIME_COLLECTION, RENDER_COLLECTION
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for dbs in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.actions,
                bpy.data.cameras, bpy.data.lights, bpy.data.collections):
        for datablock in list(dbs):
            if datablock.users == 0:
                dbs.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 30
    scene.frame_set(0)
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1320
    scene.render.resolution_y = 1040
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.35
    scene.view_settings.gamma = 1.0
    scene.render.image_settings.compression = 18
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 48
    world = bpy.data.worlds.new("BagPreview_WinterAir")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.085, 0.125, 0.14, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.52
    scene.world = world

    RUNTIME_COLLECTION = bpy.data.collections.new("BagRuntime")
    scene.collection.children.link(RUNTIME_COLLECTION)
    RENDER_COLLECTION = bpy.data.collections.new("RenderOnly_NotExported")
    scene.collection.children.link(RENDER_COLLECTION)


def project_original_cloth_image() -> bpy.types.Image:
    """Create a deterministic, tileable dyed-canvas weave; no external pixels."""
    image = bpy.data.images.new("Bag_Cloth_Albedo_1K_ProjectOriginal",
                                width=TEXTURE_SIZE, height=TEXTURE_SIZE, alpha=False)
    image.colorspace_settings.name = "sRGB"
    image.file_format = "PNG"
    rng = random.Random(41017)
    base = (0.30, 0.34, 0.29)
    pixels = array("f")
    row_noise = [rng.uniform(-0.018, 0.018) for _ in range(TEXTURE_SIZE)]
    col_noise = [rng.uniform(-0.014, 0.014) for _ in range(TEXTURE_SIZE)]
    row_warp = [math.cos(math.tau * y / 9.0) for y in range(TEXTURE_SIZE)]
    col_weft = [math.cos(math.tau * x / 9.0) for x in range(TEXTURE_SIZE)]
    for y in range(TEXTURE_SIZE):
        coarse_y = 0.011 * math.sin(math.tau * y / 173.0 + 0.7)
        for x in range(TEXTURE_SIZE):
            warp = abs(col_weft[x]) ** 10
            weft = abs(row_warp[y]) ** 10
            crosshatch = 0.034 * warp - 0.027 * weft + 0.018 * warp * weft
            mottling = 0.010 * math.sin(math.tau * x / 149.0 + 0.3) + coarse_y
            variation = crosshatch + mottling + row_noise[y] + col_noise[x]
            pixels.extend((
                max(0.0, min(1.0, base[0] + variation * 0.86)),
                max(0.0, min(1.0, base[1] + variation)),
                max(0.0, min(1.0, base[2] + variation * 0.72)),
                1.0,
            ))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(TEXTURE_OUT)
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def principled_material(name, color, roughness, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (420, 40)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (120, 40)
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def make_materials(cloth_image):
    cloth = bpy.data.materials.new("Bag_Canvas_ProjectOriginal_Cloth1K")
    cloth.diffuse_color = (0.30, 0.34, 0.29, 1.0)
    cloth.use_nodes = True
    cloth["semantic_surface"] = "Worn woven canvas; project-original tileable 1K albedo"
    cloth["texture_repeat_m"] = TEXTURE_REPEAT_M
    cloth_nodes = cloth.node_tree.nodes
    cloth_nodes.clear()
    texcoord = cloth_nodes.new("ShaderNodeTexCoord")
    texcoord.location = (-620, 80)
    image = cloth_nodes.new("ShaderNodeTexImage")
    image.image = cloth_image
    image.extension = "REPEAT"
    image.location = (-390, 80)
    image.interpolation = "Linear"
    shader = cloth_nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (-90, 80)
    shader.inputs["Roughness"].default_value = 0.89
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Sheen Weight"].default_value = 0.16
    shader.inputs["Sheen Roughness"].default_value = 0.82
    output = cloth_nodes.new("ShaderNodeOutputMaterial")
    output.location = (220, 80)
    bump = cloth_nodes.new("ShaderNodeBump")
    bump.location = (-80, -140)
    bump.inputs["Strength"].default_value = 0.12
    bump.inputs["Distance"].default_value = 0.0007
    cloth.node_tree.links.new(texcoord.outputs["UV"], image.inputs["Vector"])
    cloth.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])
    cloth.node_tree.links.new(image.outputs["Color"], bump.inputs["Height"])
    cloth.node_tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    cloth.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    leather = principled_material("Bag_Leather_Aged_WarmBrown", (0.205, 0.123, 0.073), 0.82)
    leather["semantic_surface"] = "Project-original worn leather reinforcement"
    leather_shader = next(node for node in leather.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    leather_shader.inputs["Sheen Weight"].default_value = 0.08
    metal = principled_material("Bag_Hardware_Dulled_Brass", (0.36, 0.255, 0.115), 0.54, 0.62)
    thread = principled_material("Bag_Stitch_Weathered_Flax", (0.54, 0.43, 0.27), 0.92)
    wood = principled_material("RenderOnly_Root_WarmBark", (0.22, 0.15, 0.095), 0.88)
    ground = principled_material("RenderOnly_Ground_ForestSoil", (0.075, 0.095, 0.083), 0.96)
    return cloth, leather, metal, thread, wood, ground


def link_runtime(obj):
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    RUNTIME_COLLECTION.objects.link(obj)
    return obj


def new_empty(name, parent, local_location=(0.0, 0.0, 0.0), role=""):
    obj = bpy.data.objects.new(name, None)
    RUNTIME_COLLECTION.objects.link(obj)
    obj.empty_display_type = "SPHERE"
    obj.empty_display_size = 0.008
    obj.parent = parent
    obj.matrix_parent_inverse.identity()
    obj.location = local_location
    if role:
        obj["interface_role"] = role
    return obj


def create_mesh_object(name, vertices, faces, material, parent, uv_mode=None, smooth=True,
                       uv_values=None, material_slots=None, face_material_indices=None):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    for slot in (material_slots or [material]):
        mesh.materials.append(slot)
    mesh.update()
    if uv_mode:
        uv_layer = mesh.uv_layers.new(name="UVMap")
        for polygon in mesh.polygons:
            for loop_index in polygon.loop_indices:
                vertex_index = mesh.loops[loop_index].vertex_index
                x, y, z = vertices[vertex_index]
                if uv_values is not None:
                    uv = uv_values[vertex_index]
                elif uv_mode == "body":
                    angle_index, ring_index = vertex_index % 64, vertex_index // 64
                    uv = (angle_index / 64.0 * 3.0,
                          BODY_PROFILES[ring_index][0] / TEXTURE_REPEAT_M)
                elif uv_mode == "strap":
                    sample_index, corner_index = divmod(vertex_index, 4)
                    uv = (STRAP_DISTANCE[sample_index] / TEXTURE_REPEAT_M, corner_index / 3.0)
                else:
                    uv = (x / TEXTURE_REPEAT_M, z / TEXTURE_REPEAT_M)
                uv_layer.data[loop_index].uv = uv
    for index, polygon in enumerate(mesh.polygons):
        polygon.use_smooth = smooth
        if face_material_indices is not None:
            polygon.material_index = face_material_indices[index]
    obj = bpy.data.objects.new(name, mesh)
    RUNTIME_COLLECTION.objects.link(obj)
    obj.parent = parent
    obj.matrix_parent_inverse.identity()
    obj.location = (0.0, 0.0, 0.0)
    obj["asset_part"] = name
    return obj


BODY_PROFILES = (
    (0.000, 0.70, 0.72),
    (0.006, 0.79, 0.84),
    (0.018, 0.91, 0.93),
    (0.035, 0.975, 0.98),
    (0.070, 1.000, 1.000),
    (0.125, 1.000, 1.000),
    (0.160, 0.985, 0.985),
    (0.181, 0.950, 0.970),
    (0.194, 0.900, 0.940),
    (0.200, 0.800, 0.900),
)
STRAP_DISTANCE = []


def bag_body_vertices_faces():
    count = 64
    vertices = []
    faces = []
    for z, width_scale, depth_scale in BODY_PROFILES:
        for index in range(count):
            angle = math.tau * index / count
            c, s = math.cos(angle), math.sin(angle)
            # Rounded-rectangle superellipse with a gently padded front/back.
            x = 0.1500 * width_scale * math.copysign(abs(c) ** 0.57, c)
            y = 0.0580 * depth_scale * math.copysign(abs(s) ** 0.57, s)
            y *= 1.0 + 0.035 * math.sin(math.pi * min(1.0, z / 0.20))
            vertices.append((x, y, z))
    for ring in range(len(BODY_PROFILES) - 1):
        for index in range(count):
            nxt = (index + 1) % count
            a = ring * count + index
            b = ring * count + nxt
            c = (ring + 1) * count + nxt
            d = (ring + 1) * count + index
            faces.append((a, b, c, d))
    faces.append(tuple(range(count - 1, -1, -1)))
    last = (len(BODY_PROFILES) - 1) * count
    faces.append(tuple(last + index for index in range(count)))
    return vertices, faces


def rounded_polygon_panel(name, outline_xz, front_y, thickness, material, parent):
    vertices = []
    faces = []
    count = len(outline_xz)
    for y in (front_y, front_y + thickness):
        for x, z in outline_xz:
            vertices.append((x, y, z))
    faces.append(tuple(range(count)))
    faces.append(tuple(reversed(range(count, 2 * count))))
    for i in range(count):
        j = (i + 1) % count
        faces.append((i, j, count + j, count + i))
    obj = create_mesh_object(name, vertices, faces, material, parent, uv_mode="planar", smooth=True)
    bevel = obj.modifiers.new("SoftSewnEdges", "BEVEL")
    bevel.width = 0.0015
    bevel.segments = 3
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    obj.data.update()
    return obj


def tube_mesh_data(paths, radius, sides=6):
    vertices = []
    faces = []
    for raw_points in paths:
        points = [Vector(point) for point in raw_points]
        if len(points) < 2:
            continue
        start = len(vertices)
        for i, point in enumerate(points):
            tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
            reference = Vector((0.0, 1.0, 0.0))
            if abs(tangent.dot(reference)) > 0.94:
                reference = Vector((0.0, 0.0, 1.0))
            u = tangent.cross(reference).normalized()
            v = tangent.cross(u).normalized()
            for side in range(sides):
                angle = math.tau * side / sides
                vertex = point + radius * (math.cos(angle) * u + math.sin(angle) * v)
                vertices.append(tuple(vertex))
        for i in range(len(points) - 1):
            for side in range(sides):
                a = start + i * sides + side
                b = start + i * sides + (side + 1) % sides
                faces.append((a, b, b + sides, a + sides))
    return vertices, faces


def add_tubes(name, paths, radius, material, parent, sides=6):
    vertices, faces = tube_mesh_data(paths, radius, sides)
    return create_mesh_object(name, vertices, faces, material, parent, smooth=True)


def torus_xz(name, center, major_radius, minor_radius, material, parent, major_steps=24, minor_steps=8):
    cx, cy, cz = center
    vertices = []
    faces = []
    for i in range(major_steps):
        theta = math.tau * i / major_steps
        radial = Vector((math.cos(theta), 0.0, math.sin(theta)))
        ring_center = Vector(center) + major_radius * radial
        for j in range(minor_steps):
            phi = math.tau * j / minor_steps
            point = ring_center + minor_radius * (math.cos(phi) * radial + math.sin(phi) * Vector((0, 1, 0)))
            vertices.append(tuple(point))
    for i in range(major_steps):
        for j in range(minor_steps):
            a = i * minor_steps + j
            b = i * minor_steps + (j + 1) % minor_steps
            c = ((i + 1) % major_steps) * minor_steps + (j + 1) % minor_steps
            d = ((i + 1) % major_steps) * minor_steps + j
            faces.append((a, b, c, d))
    return create_mesh_object(name, vertices, faces, material, parent, smooth=True)


def box_mesh(name, location, dimensions, material, parent, bevel=0.0):
    dx, dy, dz = (value * 0.5 for value in dimensions)
    x, y, z = location
    vertices = [
        (x - dx, y - dy, z - dz), (x + dx, y - dy, z - dz),
        (x + dx, y + dy, z - dz), (x - dx, y + dy, z - dz),
        (x - dx, y - dy, z + dz), (x + dx, y - dy, z + dz),
        (x + dx, y + dy, z + dz), (x - dx, y + dy, z + dz),
    ]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    obj = create_mesh_object(name, vertices, faces, material, parent, smooth=True)
    if bevel:
        modifier = obj.modifiers.new("RoundedEdges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
        normal = obj.modifiers.new("WeightedCornerNormals", "WEIGHTED_NORMAL")
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.modifier_apply(modifier=normal.name)
        obj.select_set(False)
    return obj


def catmull_rom(points, steps=6):
    vectors = [Vector(point) for point in points]
    samples = []
    for i in range(len(vectors) - 1):
        p0 = vectors[max(0, i - 1)]
        p1 = vectors[i]
        p2 = vectors[i + 1]
        p3 = vectors[min(len(vectors) - 1, i + 2)]
        for step in range(steps):
            t = step / steps
            t2, t3 = t * t, t * t * t
            q = 0.5 * ((2 * p1) + (-p0 + p2) * t +
                       (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                       (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            samples.append(q)
    samples.append(vectors[-1])
    return samples


def strap_mesh(path_points):
    global STRAP_DISTANCE
    centers = catmull_rom(path_points, steps=7)
    STRAP_DISTANCE = [0.0]
    for i in range(1, len(centers)):
        STRAP_DISTANCE.append(STRAP_DISTANCE[-1] + (centers[i] - centers[i - 1]).length)
    vertices = []
    faces = []
    uv_values = []
    vertex_samples = []
    face_material_indices = []
    frames = []
    half_width = 0.0135
    half_thickness = 0.0023
    for i, center in enumerate(centers):
        tangent = (centers[min(i + 1, len(centers) - 1)] - centers[max(i - 1, 0)]).normalized()
        across = tangent.cross(Vector((0.0, 1.0, 0.0)))
        if across.length < 1e-6:
            across = tangent.cross(Vector((0.0, 0.0, 1.0)))
        across.normalize()
        thickness = tangent.cross(across).normalized()
        frames.append((center, across, thickness))
        for side, depth in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            point = center + across * (side * half_width) + thickness * (depth * half_thickness)
            vertices.append(tuple(point))
            uv_values.append((STRAP_DISTANCE[i] / TEXTURE_REPEAT_M, (side + 1.0) * 0.5))
            vertex_samples.append(i)
    for i in range(len(centers) - 1):
        for side in range(4):
            a = i * 4 + side
            b = i * 4 + (side + 1) % 4
            faces.append((a, b, b + 4, a + 4))
            face_material_indices.append(0)
    faces.append((3, 2, 1, 0))
    end = (len(centers) - 1) * 4
    faces.append((end, end + 1, end + 2, end + 3))
    face_material_indices.extend((0, 0))

    # Add fine transverse sewing dashes into the deforming strap mesh itself.
    # This keeps thread attached through every shape-key pose (no floating seam
    # object and no second movement/skin owner).
    dash_step = max(1, int(0.010 / max(STRAP_DISTANCE[-1] / max(1, len(centers) - 1), 1e-6)))
    for sample_index in range(dash_step, len(centers) - dash_step, dash_step):
        center, across, thickness = frames[sample_index]
        front_offset = thickness * (half_thickness + 0.00042)
        dash_points = [center - across * 0.009 + front_offset,
                       center + across * 0.009 + front_offset]
        dash_vertices, dash_faces = tube_mesh_data([[tuple(point) for point in dash_points]],
                                                   radius=0.00048, sides=4)
        vertex_start = len(vertices)
        vertices.extend(dash_vertices)
        uv_values.extend([(STRAP_DISTANCE[sample_index] / TEXTURE_REPEAT_M, 0.5)] * len(dash_vertices))
        vertex_samples.extend([sample_index] * len(dash_vertices))
        faces.extend([tuple(vertex_start + index for index in face) for face in dash_faces])
        face_material_indices.extend([1] * len(dash_faces))
    return vertices, faces, centers, frames, uv_values, vertex_samples, face_material_indices


def add_curve_shape_keys(strap, base_vertices, vertex_samples, samples):
    basis = strap.shape_key_add(name="Basis")
    release = strap.shape_key_add(name="Released")
    placed = strap.shape_key_add(name="Placed")
    strap.data.shape_keys.name = "BagStrap_PoseKeys"
    for vertex_index, sample_index in enumerate(vertex_samples):
        point = Vector(base_vertices[vertex_index])
        center = samples[sample_index]
        if len(samples) > 1:
            t = sample_index / (len(samples) - 1)
        else:
            t = 0.0
        influence = math.sin(math.pi * t) ** 0.8
        # Pull the crown free toward the player and let it fall clear of the root.
        released_point = point + Vector((0.0, -0.105 * influence, -0.055 * influence))
        release.data[vertex_index].co = released_point
        # The final pose drapes the same strap over the satchel front; BagRoot
        # (controlled by the W01 scene owner) remains stationary in these clips.
        target_z = 0.158 + 0.014 * math.cos(math.tau * t)
        target_y = -0.066
        placed_delta = Vector((0.0, (target_y - center.y) * influence,
                               (target_z - center.z) * influence))
        placed.data[vertex_index].co = point + placed_delta
    basis.value = 0.0
    release.value = 0.0
    placed.value = 0.0
    return release, placed


def add_shape_key_clip(key_data, name, keyframes, contract):
    action = bpy.data.actions.new(name=name)
    anim = key_data.animation_data_create()
    anim.action = action
    for frame, released_value, placed_value in keyframes:
        release_block = key_data.key_blocks["Released"]
        placed_block = key_data.key_blocks["Placed"]
        release_block.value = released_value
        placed_block.value = placed_value
        release_block.keyframe_insert(data_path="value", frame=frame, group=name)
        placed_block.keyframe_insert(data_path="value", frame=frame, group=name)
    anim.action = None
    track = anim.nla_tracks.new()
    track.name = name
    track.mute = False
    strip = track.strips.new(name, keyframes[0][0], action)
    strip.action_frame_start = keyframes[0][0]
    strip.action_frame_end = keyframes[-1][0]
    strip.blend_type = "REPLACE"
    strip.extrapolation = "NOTHING"
    strip.influence = 1.0
    strip.use_auto_blend = False
    return {
        "name": name,
        "node": "BagStrap",
        "start_frame": keyframes[0][0],
        "end_frame": keyframes[-1][0],
        "duration_seconds": round((keyframes[-1][0] - keyframes[0][0]) / FPS, 4),
        "contract": contract,
    }


def add_render_fixture(wood_material, ground_material, include_root):
    scene = bpy.context.scene
    plane = bpy.data.meshes.new("RenderOnly_Ground_Mesh")
    plane.from_pydata([(-1.2, -1.0, -0.002), (1.2, -1.0, -0.002),
                       (1.2, 1.0, -0.002), (-1.2, 1.0, -0.002)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("RenderOnly_Ground", plane)
    RENDER_COLLECTION.objects.link(ground)
    plane.materials.append(ground_material)
    ground["not_exported"] = True
    render_objects = [ground]

    if include_root:
        # A render-only warm root nub shows the physical strap contact. The
        # environment/root model remains an independently owned W01 asset.
        points = [(-0.34, 0.11, 0.275), (-0.24, 0.035, 0.33),
                  (-0.12, -0.005, 0.365), (0.00, 0.015, 0.345),
                  (0.14, 0.07, 0.38), (0.26, 0.13, 0.335), (0.36, 0.16, 0.29)]
        root_vertices, root_faces = tube_mesh_data([catmull_rom(points, steps=8)], 0.031, sides=10)
        root_mesh = bpy.data.meshes.new("RenderOnly_SnagRoot_Mesh")
        root_mesh.from_pydata(root_vertices, [], root_faces)
        root_mesh.materials.append(wood_material)
        root_mesh.update()
        root = bpy.data.objects.new("RenderOnly_SnagRoot", root_mesh)
        RENDER_COLLECTION.objects.link(root)
        for polygon in root_mesh.polygons:
            polygon.use_smooth = True
        root["not_exported"] = True
        render_objects.append(root)

    camera_data = bpy.data.cameras.new("RenderOnly_Camera")
    camera = bpy.data.objects.new("RenderOnly_Camera", camera_data)
    RENDER_COLLECTION.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 0.78
    camera.location = (0.54, -1.28, 0.76)
    target = Vector((0.0, -0.015, 0.205))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 66
    scene.camera = camera
    render_objects.append(camera)

    for name, location, energy, size, color in (
        ("RenderOnly_Key", (-0.35, -0.48, 0.82), 35.0, 0.70, (1.0, 0.82, 0.59)),
        ("RenderOnly_Fill", (0.45, -0.1, 0.50), 16.0, 0.86, (0.61, 0.79, 0.82)),
        ("RenderOnly_Rim", (0.05, 0.54, 0.67), 42.0, 0.66, (0.91, 0.70, 0.43)),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        light = bpy.data.objects.new(name, data)
        RENDER_COLLECTION.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0.0, -0.01, 0.18)) - light.location).to_track_quat("-Z", "Y").to_euler()
        render_objects.append(light)
    scene.render.resolution_percentage = 100
    return render_objects


def remove_render_fixture(render_objects):
    for obj in list(render_objects):
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for datablock in (bpy.data.meshes, bpy.data.cameras, bpy.data.lights):
        for item in list(datablock):
            if item.users == 0 and item.name.startswith("RenderOnly_"):
                datablock.remove(item)


def glb_json(glb_path: Path):
    data = glb_path.read_bytes()
    if data[:4] != b"glTF":
        raise RuntimeError("Export did not produce a GLB container")
    json_length, chunk_type = struct.unpack_from("<II", data, 12)
    if chunk_type != 0x4E4F534A:
        raise RuntimeError("GLB has no JSON chunk")
    return json.loads(data[20:20 + json_length])


def evaluated_world_bounds(objects):
    coords = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        coords.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
    if not coords:
        raise RuntimeError("Cannot measure empty geometry")
    minimum = [min(point[i] for point in coords) for i in range(3)]
    maximum = [max(point[i] for point in coords) for i in range(3)]
    return {
        "blender_min_xyz_m": [round(value, 6) for value in minimum],
        "blender_max_xyz_m": [round(value, 6) for value in maximum],
        "blender_size_xyz_m": [round(maximum[i] - minimum[i], 6) for i in range(3)],
        "godot_size_xyz_m": [round(maximum[i] - minimum[i], 6) for i in (0, 2, 1)],
    }


def assert_outputs_are_unique():
    outputs = (BLEND_OUT, MANIFEST_OUT, SNAGGED_PREVIEW_OUT, PLACED_PREVIEW_OUT,
               GLB_OUT, TEXTURE_OUT)
    conflicts = [str(path) for path in outputs if path.exists()]
    if conflicts:
        raise FileExistsError("Refusing to overwrite existing bag stage outputs: " + ", ".join(conflicts))
    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)


def build():
    assert_outputs_are_unique()
    reset_scene()
    cloth_image = project_original_cloth_image()
    cloth, leather, metal, thread, preview_wood, preview_ground = make_materials(cloth_image)

    bag_root = bpy.data.objects.new("BagRoot", None)
    RUNTIME_COLLECTION.objects.link(bag_root)
    bag_root.empty_display_type = "CIRCLE"
    bag_root.empty_display_size = 0.045
    bag_root.location = (0.0, 0.0, 0.0)
    bag_root["movement_owner"] = "W01 scene/controller; only this node may move the complete bag"
    bag_root["origin_contract"] = "center of the 30 x 12 cm body footprint at its bottom; Blender Z-up"

    body_vertices, body_faces = bag_body_vertices_faces()
    body = create_mesh_object("BagBody", body_vertices, body_faces, cloth, bag_root, uv_mode="body")
    body["nominal_godot_size_xyz_m"] = list(BODY_SIZE_GODOT_M)
    body["shape"] = "rounded, lightly padded canvas satchel with sewn volume"

    front_flap_outline = [
        (-0.108, 0.190), (-0.094, 0.194), (0.094, 0.194), (0.108, 0.190),
        (0.105, 0.155), (0.087, 0.132), (0.054, 0.119), (0.0, 0.114),
        (-0.054, 0.119), (-0.087, 0.132), (-0.105, 0.155),
    ]
    flap = rounded_polygon_panel("Bag_Flap", front_flap_outline, -0.0597, 0.0042, cloth, bag_root)
    flap["detail"] = "softened, stitched front closure flap; no printed marks"

    # Gusset bindings and a restrained curved front seam keep the body readable
    # as sewn cloth from both front and three-quarter views.
    front_seam = [
        (-0.135, -0.0585, 0.025), (-0.142, -0.057, 0.041), (-0.145, -0.056, 0.075),
        (-0.145, -0.056, 0.125), (-0.141, -0.057, 0.158), (-0.129, -0.059, 0.175),
        (-0.095, -0.060, 0.183), (0.0, -0.060, 0.184), (0.095, -0.060, 0.183),
        (0.129, -0.059, 0.175), (0.141, -0.057, 0.158), (0.145, -0.056, 0.125),
        (0.145, -0.056, 0.075), (0.142, -0.057, 0.041), (0.135, -0.0585, 0.025),
    ]
    add_tubes("Bag_FrontGussetSeam", [front_seam], 0.00125, leather, bag_root, sides=7)

    # Two stitched leather tabs, each sewn over an aged brass attachment loop.
    ring_centers = ((-0.124, -0.020, 0.157), (0.124, -0.020, 0.157))
    for side, center in zip(("Left", "Right"), ring_centers):
        tab_x = center[0] * 0.91
        tab_outline = [(tab_x - 0.017, 0.177), (tab_x + 0.017, 0.177),
                       (tab_x + 0.014, 0.133), (tab_x, 0.125), (tab_x - 0.014, 0.133)]
        rounded_polygon_panel(f"Bag_LeatherTab_{side}", tab_outline, -0.0621, 0.0034, leather, bag_root)
        torus_xz(f"Bag_AttachmentRing_{side}", center, 0.012, 0.0025, metal, bag_root)
        for rivet_z in (0.139, 0.166):
            torus_xz(f"Bag_Rivet_{side}_{int(rivet_z * 1000)}",
                     (tab_x, -0.0673, rivet_z), 0.0025, 0.0009, metal, bag_root,
                     major_steps=12, minor_steps=6)

    # A quiet pair of leather closure tongues and small rounded brass keepers.
    for side, x in (("Left", -0.064), ("Right", 0.064)):
        rounded_polygon_panel(f"Bag_ClosureTab_{side}",
                              [(x - 0.012, 0.147), (x + 0.012, 0.147),
                               (x + 0.011, 0.098), (x, 0.091), (x - 0.011, 0.098)],
                              -0.0692, 0.003, leather, bag_root)
        box_mesh(f"Bag_ClosureKeeper_{side}", (x, -0.0732, 0.113),
                 (0.022, 0.004, 0.017), metal, bag_root, bevel=0.0025)
        box_mesh(f"Bag_ClosureTongue_{side}", (x, -0.0755, 0.112),
                 (0.011, 0.002, 0.003), leather, bag_root, bevel=0.001)

    # A narrow folded-canvas lower hem and subtle corner reinforcement provide
    # scale cues without turning the small bag into a prop showcase.
    hem_outline = [(-0.137, 0.034), (0.137, 0.034), (0.134, 0.021),
                   (0.112, 0.014), (-0.112, 0.014), (-0.134, 0.021)]
    rounded_polygon_panel("Bag_FrontHem", hem_outline, -0.0596, 0.0028, leather, bag_root)
    for side, x in (("Left", -0.112), ("Right", 0.112)):
        patch = [(x - 0.018, 0.039), (x + 0.018, 0.039),
                 (x + 0.017, 0.073), (x, 0.081), (x - 0.017, 0.073)]
        rounded_polygon_panel(f"Bag_RepairPatch_{side}", patch, -0.0618, 0.0027, leather, bag_root)

    # Running hand-stitch marks around the flap perimeter. Each short dash is
    # geometry, not a screen-space decal or decorative symbol.
    stitch_paths = []
    for a, b in zip(front_flap_outline, front_flap_outline[1:] + front_flap_outline[:1]):
        start = Vector((a[0], -0.0650, a[1]))
        end = Vector((b[0], -0.0650, b[1]))
        direction = end - start
        length = direction.length
        direction.normalize()
        count = max(1, int(length / 0.009))
        for dash_index in range(1, count):
            center = start + direction * (length * dash_index / count)
            half = min(0.0017, length / (count * 4.0))
            stitch_paths.append([tuple(center - direction * half), tuple(center + direction * half)])
    add_tubes("Bag_Flap_Stitches", stitch_paths, 0.00072, thread, bag_root, sides=5)

    snag_path = [
        (-0.124, -0.020, 0.157), (-0.139, -0.040, 0.220), (-0.127, -0.054, 0.287),
        (-0.094, -0.062, 0.333), (-0.053, -0.067, 0.362), (-0.024, -0.064, 0.378),
        (-0.012, -0.042, 0.389), (0.000, -0.004, 0.379), (0.014, 0.025, 0.355),
        (0.035, 0.026, 0.337), (0.063, -0.021, 0.333), (0.100, -0.055, 0.308),
        (0.130, -0.052, 0.268), (0.140, -0.040, 0.220), (0.124, -0.020, 0.157),
    ]
    (strap_vertices, strap_faces, strap_centers, strap_frames, strap_uvs,
     strap_vertex_samples, strap_face_materials) = strap_mesh(snag_path)
    strap = create_mesh_object("BagStrap", strap_vertices, strap_faces, cloth, bag_root,
                               uv_mode="strap", smooth=True, uv_values=strap_uvs,
                               material_slots=[cloth, thread],
                               face_material_indices=strap_face_materials)
    strap["interface_role"] = "single continuous sewn shoulder strap; only this mesh is pose-animated"
    strap["snag_pose_contact_marker"] = "Strap_SnagContact"
    strap["do_not_move_root"] = "W01 controller owns BagRoot placement; strap clips do not transform it"
    release_key, placed_key = add_curve_shape_keys(strap, strap_vertices,
                                                   strap_vertex_samples, strap_centers)

    new_empty("Strap_SnagContact", bag_root, (0.0, -0.004, 0.379),
              "align this crown point to the visible W01 root contact; no root geometry is included")
    new_empty("Strap_Attach_Left", bag_root, ring_centers[0], "left sewn loop attachment center")
    new_empty("Strap_Attach_Right", bag_root, ring_centers[1], "right sewn loop attachment center")

    clips = []
    clips.append(add_shape_key_clip(strap.data.shape_keys, "strap_snagged_rest", [
        (1, 0.0, 0.0), (12, 0.0, 0.0)], {
            "pose": "default snagged pose; strap crown passes over Strap_SnagContact",
            "BagRoot": "unchanged; remains the sole whole-object movement owner",
        }))
    clips.append(add_shape_key_clip(strap.data.shape_keys, "strap_release", [
        (1, 0.0, 0.0), (8, 0.42, 0.0), (18, 1.0, 0.0)], {
            "precondition": "bag is in snagged rest pose and the physical action has been authorized by W01 interaction logic",
            "result": "one continuous strap crown moves toward the player and falls clear of the root contact",
            "gameplay_effect": "none; no quest flag or encounter reducer is implemented here",
        }))
    clips.append(add_shape_key_clip(strap.data.shape_keys, "strap_placed_rest", [
        (1, 0.0, 0.0), (8, 0.42, 0.0), (18, 0.0, 1.0)], {
            "precondition": "strap has cleared the root; W01 controller moves the same BagRoot once to its authored side placement",
            "result": "strap lies over the bag front; body is stable on its broad side when BagRoot uses the placement rotation",
            "root_transform_owner": "W01 scene/controller; this clip changes strap shape only",
        }))

    # Align all materialized geometry and verify only the body contract bounds.
    bpy.context.view_layer.update()
    body_bounds = evaluated_world_bounds([body])
    expected = BODY_SIZE_GODOT_M
    actual = body_bounds["godot_size_xyz_m"]
    if any(abs(a - e) > 0.0015 for a, e in zip(actual, expected)):
        raise RuntimeError(f"BagBody bounds {actual} do not match requested {expected} m")

    scene = bpy.context.scene
    scene.render.resolution_x = 1320
    scene.render.resolution_y = 1040
    preview_render_objects = add_render_fixture(preview_wood, preview_ground, include_root=True)
    release_key.value = 0.0
    placed_key.value = 0.0
    bag_root.location = (0.0, 0.0, 0.0)
    bag_root.rotation_euler = (0.0, 0.0, 0.0)
    scene.frame_set(0)
    scene.render.filepath = str(SNAGGED_PREVIEW_OUT)
    bpy.ops.render.render(write_still=True)

    # The placed review pose shows the same bag laid on its broad back face.
    for obj in list(preview_render_objects):
        if obj.name == "RenderOnly_SnagRoot":
            bpy.data.objects.remove(obj, do_unlink=True)
            preview_render_objects.remove(obj)
    release_key.value = 0.0
    placed_key.value = 1.0
    bag_root.rotation_euler.x = -math.pi * 0.5
    bag_root.location = (0.0, 0.0, 0.061)
    bpy.context.view_layer.update()
    scene.render.filepath = str(PLACED_PREVIEW_OUT)
    bpy.ops.render.render(write_still=True)
    remove_render_fixture(preview_render_objects)

    # Return the file and runtime export to the default snagged pose.
    release_key.value = 0.0
    placed_key.value = 0.0
    bag_root.location = (0.0, 0.0, 0.0)
    bag_root.rotation_euler = (0.0, 0.0, 0.0)
    scene.frame_set(0)
    bpy.context.view_layer.update()

    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT), check_existing=False)

    runtime_objects = [obj for obj in RUNTIME_COLLECTION.objects
                       if obj.type in {"MESH", "EMPTY"}]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in runtime_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bag_root
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_OUT),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_nla_strips=True,
        export_force_sampling=True,
        export_current_frame=False,
        export_yup=True,
    )
    if not GLB_OUT.is_file() or not TEXTURE_OUT.is_file():
        raise RuntimeError("Expected GLB and project-original cloth texture were not exported")
    gltf = glb_json(GLB_OUT)
    node_names = [node.get("name", "") for node in gltf.get("nodes", [])]
    animation_names = sorted(animation.get("name", "") for animation in gltf.get("animations", []))
    expected_nodes = ("BagRoot", "BagBody", "BagStrap", "Strap_SnagContact",
                      "Strap_Attach_Left", "Strap_Attach_Right")
    for name in expected_nodes:
        if node_names.count(name) != 1:
            raise RuntimeError(f"GLB node {name!r} appeared {node_names.count(name)} times")
    expected_clips = sorted(("strap_snagged_rest", "strap_release", "strap_placed_rest"))
    if animation_names != expected_clips:
        raise RuntimeError(f"GLB clips {animation_names} do not match {expected_clips}")
    animation_channels = {}
    for animation in gltf.get("animations", []):
        channels = []
        for channel in animation.get("channels", []):
            node_index = channel.get("target", {}).get("node")
            path = channel.get("target", {}).get("path")
            node_name = node_names[node_index] if isinstance(node_index, int) else None
            channels.append({"node": node_name, "path": path})
        if channels != [{"node": "BagStrap", "path": "weights"}]:
            raise RuntimeError(f"Clip {animation.get('name')!r} must animate only BagStrap shape-key weights; got {channels}")
        animation_channels[animation["name"]] = channels
    root_node = gltf["nodes"][node_names.index("BagRoot")]
    default_translation = root_node.get("translation", [0.0, 0.0, 0.0])
    if any(abs(value) > 0.001 for value in default_translation):
        raise RuntimeError(f"GLB BagRoot default translation {default_translation} is not at the body-bottom origin")
    if TEXTURE_OUT.stat().st_size < 100_000:
        raise RuntimeError("The exported 1K cloth map is unexpectedly small")

    source_relative = SCRIPT_PATH.relative_to(ROOT).as_posix()
    manifest = {
        "schema": 1,
        "asset_id": "A-BAG / W01 F14",
        "version": "bag-v1",
        "stage_status": "offline Blender source, GLB, texture and previews generated; runtime NOT_RUN; art review pending",
        "source": source_relative,
        "source_sha256": sha256(SCRIPT_PATH),
        "passport": "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/10_ASSET_PASSPORTS_RU.md#A-BAG",
        "level_contract": "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/levels/W01_Лес_за_спиной_RU.md#F14",
        "coordinate_contract": {
            "authoring": "Blender metres, Z up; project mapping Godot (X,Y,Z) -> Blender (X,-Z,Y); glTF export Y-up",
            "godot_body_size_m": list(BODY_SIZE_GODOT_M),
            "origin": "BagRoot at body-bottom center; same BagRoot remains the single scene/controller placement owner",
            "default_pose": "upright snagged bag; body bottom at local Y=0 in Godot",
            "placed_pose_root_rotation_godot_euler_degrees": [-90.0, 0.0, 0.0],
            "body_bounds": body_bounds,
            "snag_contact_marker": {
                "node": "Strap_SnagContact",
                "godot_local_xyz_m": blender_xyz_to_godot((0.0, -0.004, 0.379)),
            },
            "attachment_markers": {
                "Strap_Attach_Left": blender_xyz_to_godot(ring_centers[0]),
                "Strap_Attach_Right": blender_xyz_to_godot(ring_centers[1]),
            },
        },
        "node_contract": {
            "root": "BagRoot",
            "body_mesh": "BagBody",
            "single_continuous_strap_mesh": "BagStrap",
            "snag_contact": "Strap_SnagContact",
            "left_attachment": "Strap_Attach_Left",
            "right_attachment": "Strap_Attach_Right",
            "whole_object_motion_owner": "W01 scene/controller moves BagRoot once; no GLB clip moves BagRoot",
        },
        "runtime_handoff": {
            "instantiate_once": True,
            "node_owner": "W01 scene/controller is the sole owner of BagRoot world transform; do not duplicate or animate BagRoot from an AnimationPlayer",
            "unit_scale": [1.0, 1.0, 1.0],
            "snagged": {
                "clip": "strap_snagged_rest",
                "root_rotation_godot_euler_degrees": [0.0, 0.0, 0.0],
                "root_local_origin": "center of BagBody bottom footprint",
                "contact_alignment": "BagRoot.to_global(Strap_SnagContact.position) must equal the authored visible-root snag point",
                "contact_marker_local_godot_xyz_m": blender_xyz_to_godot((0.0, -0.004, 0.379)),
                "body_bottom_relative_to_contact_m": -0.379,
            },
            "action_sequence": [
                "Instantiate one BagRoot and align its Strap_SnagContact marker to the F14 visible-root snag point; the controller owns this transform.",
                "After the authorized physical release action, play strap_release to completion while BagRoot remains fixed.",
                "Move and rotate that same BagRoot to the scene-authored boardwalk-side contact point; do not spawn a second bag.",
                "At the placement ground point, set BagRoot Godot rotation X=-90 degrees, unit scale, and origin Y=ground_y+0.060 m; keep scene-authored X/Z placement and optional yaw in the W01 controller.",
                "Play strap_placed_rest on BagStrap after the BagRoot placement transform is applied.",
            ],
            "placed": {
                "clip": "strap_placed_rest",
                "root_rotation_godot_euler_degrees": [-90.0, 0.0, 0.0],
                "root_translation_godot_xyz_m": ["scene_authored_X", "ground_Y+0.060", "scene_authored_Z"],
                "ground_support": "BagBody lies on its broad back face; 0.060 m is half the 0.12 m body depth",
                "clip_targets": "BagStrap shape-key weights only; BagRoot is not animated",
            },
        },
        "texture": {
            "path": "game/assets/photoworlds/bag/bag-cloth-1k.png",
            "sha256": sha256(TEXTURE_OUT),
            "size_px": [TEXTURE_SIZE, TEXTURE_SIZE],
            "color_space": "sRGB",
            "provenance": "project-original deterministic tileable woven-canvas albedo generated by bag_v1.py; no external image source",
            "repeat_m": TEXTURE_REPEAT_M,
        },
        "clips": clips,
        "previews": [
            {"path": SNAGGED_PREVIEW_OUT.name, "sha256": sha256(SNAGGED_PREVIEW_OUT)},
            {"path": PLACED_PREVIEW_OUT.name, "sha256": sha256(PLACED_PREVIEW_OUT)},
        ],
        "artifacts": {
            "blend": {"path": BLEND_OUT.name, "sha256": sha256(BLEND_OUT), "bytes": BLEND_OUT.stat().st_size},
            "glb": {"path": "game/assets/photoworlds/bag/bag-v1.glb", "sha256": sha256(GLB_OUT), "bytes": GLB_OUT.stat().st_size},
            "glb_nodes": node_names,
            "glb_animations": animation_names,
            "glb_animation_channels": animation_channels,
            "glb_default_root_translation_yup_m": default_translation,
        },
        "scope_limits": [
            "The render-only contact root, ground, camera, and lights are absent from the GLB.",
            "This asset does not implement dialogue, quest facts, encounter resolution, collisions, or save/load behavior.",
            "No Godot import/runtime use, Windows station check, or author art acceptance is claimed.",
        ],
    }
    MANIFEST_OUT.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "blend": str(BLEND_OUT),
        "glb": str(GLB_OUT),
        "texture": str(TEXTURE_OUT),
        "manifest": str(MANIFEST_OUT),
        "previews": [str(SNAGGED_PREVIEW_OUT), str(PLACED_PREVIEW_OUT)],
        "body_size_godot_m": body_bounds["godot_size_xyz_m"],
        "nodes": expected_nodes,
        "clips": animation_names,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    build()
