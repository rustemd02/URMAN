"""Build an original, finite W01 F00→F03 prologue-ground terrain asset.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/photoworlds/W01/urman_forest_ground.py \
    -- --output-dir /private/tmp/urman-forest-ground-YYYYMMDD

Godot-authored coordinates are retained in the manifest as (X,Y,Z); Blender
vertices use (X,-Z,Y) before its Y-up export remap. The GLB contains only the terrain mesh. It is a visual
and collision-surface candidate, not a finished Godot scene or collision.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


X_BOUNDS = (-9.0, 89.0)
Z_BOUNDS = (-11.0, 151.0)
ROUTE_HALF_WIDTH = 1.05
GRID_STEP = 0.5
TEXTURE_SIZE = 1024
BAKE_SIZE = 2048
BAKED_ALBEDO_FILENAME = "prologue-ground-f00-f03-baked-albedo-2k.png"
METRES_PER_REPEAT = 8.0
FPS = 24

NODES = {
    "F00": {"label": "Тропа пролога", "xyz": (0.0, 0.0, 0.0), "role": "spawn_prologue"},
    "F01": {"label": "Перекладина на тропе", "xyz": (12.0, 1.0, 50.0), "role": "action"},
    "F02": {"label": "Светлая расщелина", "xyz": (45.0, 3.0, 95.0), "role": "vista"},
    "F03": {"label": "Дальний стук", "xyz": (80.0, 4.0, 140.0), "role": "prologue_end"},
    "F10": {"label": "Позиция семейной камеры", "xyz": (0.0, 0.0, 35.0), "role": "spawn_main"},
    "F11": {"label": "Пустое место за группой", "xyz": (14.0, 0.0, 48.0), "role": "observation"},
    "F12": {"label": "Корневой поворот", "xyz": (24.0, 1.0, 70.0), "role": "spatial_link_entry"},
    "F13": {"label": "Обратная сторона трёх стволов", "xyz": (95.0, 6.0, 120.0), "role": "spatial_link_exit"},
    "F14": {"label": "Площадка старого корня", "xyz": (112.0, 7.0, 148.0), "role": "encounter"},
    "F15": {"label": "Кроны над ложбиной", "xyz": (132.0, 9.0, 169.0), "role": "linger"},
    "F16": {"label": "Просвет к деревне", "xyz": (149.0, 8.0, 190.0), "role": "exit"},
}

# The prologue is the authored continuous F00→F01→F02→F03 physical route.
# Main-world F10–F16 and its F12↔F13 spatial transform remain outside this asset.
PROLOGUE_ROUTE_NODE_IDS = ("F00", "F01", "F02", "F03")
ROUTES = [
    {
        "edge": [first, second],
        "points": [NODES[first]["xyz"], NODES[second]["xyz"]],
        "kind": "authored_physical",
    }
    for first, second in zip(PROLOGUE_ROUTE_NODE_IDS, PROLOGUE_ROUTE_NODE_IDS[1:])
]
BUILD_NODE_IDS = PROLOGUE_ROUTE_NODE_IDS

PALETTE_SRGB = {
    "dominant": "#2F625D",
    "light": "#B9C7A0",
    "shadow": "#413A59",
    "warm": "#C89757",
}


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(tail)


def project_root() -> Path:
    return Path(__file__).resolve().parents[5]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_png(path: Path, width: int, height: int, channels: int, pixel):
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            color = pixel(x, y)
            raw.extend(max(0, min(255, int(round(value)))) for value in color)
    color_type = {3: 2, 4: 6}[channels]
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
    data = bytearray(b"\x89PNG\r\n\x1a\n")
    data.extend(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)))
    data.extend(chunk(b"IDAT", zlib.compress(bytes(raw), level=8)))
    data.extend(chunk(b"IEND", b""))
    path.write_bytes(data)


def smoothstep(a: float, b: float, value: float) -> float:
    if value <= a:
        return 0.0
    if value >= b:
        return 1.0
    t = (value - a) / (b - a)
    return t * t * (3.0 - 2.0 * t)


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def point_distance(px, pz, qx, qz):
    return math.hypot(px - qx, pz - qz)


def route_distance(x: float, z: float):
    nearest = math.inf
    for route in ROUTES:
        for first, second in zip(route["points"], route["points"][1:]):
            dx, dz = second[0] - first[0], second[2] - first[2]
            length_sq = dx * dx + dz * dz
            t = clamp01(((x - first[0]) * dx + (z - first[2]) * dz) / length_sq)
            qx, qz = first[0] + t * dx, first[2] + t * dz
            nearest = min(nearest, point_distance(x, z, qx, qz))
    return nearest


def route_height_and_x(z: float):
    first = NODES[PROLOGUE_ROUTE_NODE_IDS[0]]["xyz"]
    if z <= first[2]:
        return first[0], first[1]
    for first_id, second_id in zip(PROLOGUE_ROUTE_NODE_IDS, PROLOGUE_ROUTE_NODE_IDS[1:]):
        first, second = NODES[first_id]["xyz"], NODES[second_id]["xyz"]
        if z <= second[2]:
            t = clamp01((z - first[2]) / (second[2] - first[2]))
            return first[0] + (second[0] - first[0]) * t, first[1] + (second[1] - first[1]) * t
    last = NODES[PROLOGUE_ROUTE_NODE_IDS[-1]]["xyz"]
    return last[0], last[1]


def terrain_relief(x: float, z: float):
    # Quiet, broad landforms stay continuous across the whole patch. The
    # shallow shoulders flank the path without building a berm or ridge.
    shoulder_left = math.exp(-(((x - 3.2) / 5.5) ** 2 + ((z - 24.0) / 18.0) ** 2) * 0.5)
    shoulder_right = math.exp(-(((x - 15.5) / 4.7) ** 2 + ((z - 31.0) / 17.0) ** 2) * 0.5)
    shallow_hollow = math.exp(-(((x + 4.0) / 6.0) ** 2 + ((z - 42.0) / 11.0) ** 2) * 0.5)
    return 0.16 * shoulder_left + 0.12 * shoulder_right - 0.06 * shallow_hollow


def surface_height_and_masks(x: float, z: float):
    # Route height and center X interpolate between authored nodes by Z.
    # Relief is compensated against that centerline so path samples retain
    # the authored grade and node heights stay exact.
    route_x, route_y = route_height_and_x(z)
    height = route_y + terrain_relief(x, z) - terrain_relief(route_x, z)

    route_mask = clamp01(1.0 - smoothstep(ROUTE_HALF_WIDTH, 4.3, route_distance(x, z)))
    for node_id in BUILD_NODE_IDS:
        nx, _, nz = NODES[node_id]["xyz"]
        pad_mask = 1.0 - smoothstep(1.2, 2.0, point_distance(x, z, nx, nz))
        route_mask = max(route_mask, pad_mask)
    leaf_a = math.exp(-(((x - 4.8) / 3.6) ** 2 + ((z - 15.0) / 8.0) ** 2) * 0.5)
    leaf_b = math.exp(-(((x - 15.8) / 4.0) ** 2 + ((z - 39.0) / 9.0) ** 2) * 0.5)
    root_a = math.exp(-(((x - 16.0) / 3.2) ** 2 + ((z - 52.0) / 5.5) ** 2) * 0.5)
    root_b = math.exp(-(((x - 7.0) / 3.2) ** 2 + ((z - 48.0) / 5.0) ** 2) * 0.5)
    leaf_mask = (0.08 + 0.24 * max(leaf_a, leaf_b)) * (1.0 - 0.9 * route_mask)
    root_mask = clamp01(0.32 * max(root_a, root_b)) * (1.0 - 0.85 * route_mask)
    moisture = clamp01(0.12 + 0.16 * (1.0 - max(leaf_a, leaf_b)) + 0.12 * shallow_hollow_value(x, z))
    return height, (route_mask, leaf_mask, root_mask, moisture)


def shallow_hollow_value(x: float, z: float):
    return math.exp(-(((x + 4.0) / 6.0) ** 2 + ((z - 42.0) / 11.0) ** 2) * 0.5)


def mask_pixel(xi: int, yi: int):
    u = xi / (TEXTURE_SIZE - 1)
    v = yi / (TEXTURE_SIZE - 1)
    x = X_BOUNDS[0] + u * (X_BOUNDS[1] - X_BOUNDS[0])
    # PNG's first row is top; map it to the far end of Godot +Z.
    z = Z_BOUNDS[1] - v * (Z_BOUNDS[1] - Z_BOUNDS[0])
    _, masks = surface_height_and_masks(x, z)
    return tuple(round(255 * value) for value in masks)


def periodic_value_noise(x: int, y: int, cells: int, seed: int):
    u = x / TEXTURE_SIZE * cells
    v = y / TEXTURE_SIZE * cells
    ix, iy = math.floor(u), math.floor(v)
    fx, fy = u - ix, v - iy
    fx = fx * fx * (3.0 - 2.0 * fx)
    fy = fy * fy * (3.0 - 2.0 * fy)

    def lattice(a, b):
        # Integer hash is deterministic and wraps at the tile period.
        a %= cells
        b %= cells
        n = (a * 374761393 + b * 668265263 + seed * 1442695041) & 0xFFFFFFFF
        n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
        n ^= n >> 16
        return n / 0xFFFFFFFF

    a = lattice(ix, iy)
    b = lattice(ix + 1, iy)
    c = lattice(ix, iy + 1)
    d = lattice(ix + 1, iy + 1)
    lower = a * (1.0 - fx) + b * fx
    upper = c * (1.0 - fx) + d * fx
    return lower * (1.0 - fy) + upper * fy


def tile_pixel(kind: str):
    def pixel(x: int, y: int):
        field = (
            (periodic_value_noise(x, y, 4, 13) - 0.5) * 1.00
            + (periodic_value_noise(x, y, 11, 31) - 0.5) * 0.58
            + (periodic_value_noise(x, y, 29, 47) - 0.5) * 0.28
            + (periodic_value_noise(x, y, 71, 61) - 0.5) * 0.12
        ) * 2.0
        fine = (periodic_value_noise(x, y, 113, 97) - 0.5) * 2.0
        if kind == "soil":
            base = (47.0, 98.0, 93.0)
            shadow = clamp01((-field - 0.38) * 0.22)
            warm = clamp01((field - 0.65) * 0.07)
            variation = 7.0 * field + 1.5 * fine
            values = [base[c] + variation for c in range(3)]
            for c, shade in enumerate((65.0, 58.0, 89.0)):
                values[c] = values[c] * (1.0 - shadow) + shade * shadow
            for c, tint in enumerate((200.0, 151.0, 87.0)):
                values[c] = values[c] * (1.0 - warm) + tint * warm
        elif kind == "leaf":
            base = (171.0, 187.0, 143.0)
            warm = clamp01((field - 0.50) * 0.10)
            variation = 9.0 * field + 1.5 * fine
            values = [base[c] + variation for c in range(3)]
            for c, tint in enumerate((200.0, 151.0, 87.0)):
                values[c] = values[c] * (1.0 - warm) + tint * warm
        else:
            base = (54.0, 83.0, 72.0)
            shadow = clamp01((-field - 0.22) * 0.24)
            warm = clamp01((field - 0.61) * 0.08)
            variation = 7.0 * field + 1.6 * fine
            values = [base[c] + variation for c in range(3)]
            for c, shade in enumerate((65.0, 58.0, 89.0)):
                values[c] = values[c] * (1.0 - shadow) + shade * shadow
            for c, tint in enumerate((151.0, 132.0, 83.0)):
                values[c] = values[c] * (1.0 - warm) + tint * warm
        return tuple(max(0, min(255, int(round(v)))) for v in values)
    return pixel


def sample_values(first: float, second: float, step: float):
    count = max(1, math.ceil((second - first) / step))
    return [first + (second - first) * i / count for i in range(count + 1)]


def make_coordinate_axes():
    # Authored integer coordinates are exact half-metre-grid vertices, and
    # the 2.1 m route has more than four samples across its walkable width.
    return sample_values(*X_BOUNDS, GRID_STEP), sample_values(*Z_BOUNDS, GRID_STEP)


def png_material_images(output_dir: Path):
    image_paths = {
        "soil": output_dir / "prologue-ground-f00-f03-soil-1k.png",
        "leaves": output_dir / "prologue-ground-f00-f03-leaves-1k.png",
        "roots": output_dir / "prologue-ground-f00-f03-roots-1k.png",
        "masks": output_dir / "prologue-ground-f00-f03-masks-1k.png",
    }
    write_png(image_paths["soil"], TEXTURE_SIZE, TEXTURE_SIZE, 3, tile_pixel("soil"))
    write_png(image_paths["leaves"], TEXTURE_SIZE, TEXTURE_SIZE, 3, tile_pixel("leaf"))
    write_png(image_paths["roots"], TEXTURE_SIZE, TEXTURE_SIZE, 3, tile_pixel("root"))
    write_png(image_paths["masks"], TEXTURE_SIZE, TEXTURE_SIZE, 4, mask_pixel)
    return image_paths


def create_mesh(image_paths):
    xs, zs = make_coordinate_axes()
    nx, nz = len(xs), len(zs)
    verts = []
    mask_values = []
    heights = []
    for z in zs:
        for x in xs:
            height, masks = surface_height_and_masks(x, z)
            # GLTF Y-up export maps Blender (x,y,z) to (x,z,-y). Negating
            # Godot Z here preserves authored positive-Z positions on import.
            verts.append((x, -z, height))
            mask_values.extend(masks)
            heights.append(height)

    faces = []
    for iz in range(nz - 1):
        row0, row1 = iz * nx, (iz + 1) * nx
        for ix in range(nx - 1):
            v00 = row0 + ix
            v10 = v00 + 1
            v01 = row1 + ix
            v11 = v01 + 1
            if (ix + iz) & 1:
                faces.extend(((v11, v10, v00), (v01, v11, v00)))
            else:
                faces.extend(((v01, v10, v00), (v01, v11, v10)))

    mesh = bpy.data.meshes.new("W01_ForestGround_TerrainMesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update(calc_edges=True)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    color_attr = mesh.color_attributes.new(name="ForestMasks", type="FLOAT_COLOR", domain="POINT")
    color_attr.data.foreach_set("color", mask_values)
    color_attr_index = mesh.color_attributes.find("ForestMasks")
    mesh.color_attributes.active_color_index = color_attr_index
    mesh.color_attributes.render_color_index = color_attr_index

    uv_bake = mesh.uv_layers.new(name="BakeUV")
    uv_metric = mesh.uv_layers.new(name="MetricUV")
    uv_values = []
    metric_values = []
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            vi = mesh.loops[loop_index].vertex_index
            x, blender_y, _ = verts[vi]
            z = -blender_y
            uv_values.extend(((x - X_BOUNDS[0]) / (X_BOUNDS[1] - X_BOUNDS[0]), (z - Z_BOUNDS[0]) / (Z_BOUNDS[1] - Z_BOUNDS[0])))
            metric_values.extend((x / METRES_PER_REPEAT, z / METRES_PER_REPEAT))
    uv_bake.data.foreach_set("uv", uv_values)
    uv_metric.data.foreach_set("uv", metric_values)
    mesh.uv_layers.active = uv_bake
    uv_bake.active_render = True

    ground = bpy.data.objects.new("W01_ForestGround_Terrain", mesh)
    bpy.context.collection.objects.link(ground)

    # Original tiled source maps are all 1K and use an 8 m repeat. Their pixel
    # values are sRGB; Blender's image color space performs sRGB→linear decode.
    material = bpy.data.materials.new("W01_ForestGround_OriginalProceduralBake")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (920, 80)
    emission = nodes.new("ShaderNodeEmission")
    emission.location = (720, 80)
    links.new(emission.outputs[0], output.inputs["Surface"])
    uvnode = nodes.new("ShaderNodeUVMap")
    uvnode.uv_map = "MetricUV"
    uvnode.location = (-980, 250)
    masknode = nodes.new("ShaderNodeVertexColor")
    masknode.layer_name = "ForestMasks"
    masknode.location = (-980, -180)
    separate = nodes.new("ShaderNodeSeparateXYZ")
    separate.location = (-760, -180)
    links.new(masknode.outputs["Color"], separate.inputs["Vector"])

    def texture_node(label, path, y):
        image = bpy.data.images.load(str(path), check_existing=False)
        image.name = label
        image.colorspace_settings.name = "sRGB"
        image.pack()
        node = nodes.new("ShaderNodeTexImage")
        node.name = label
        node.label = label
        node.image = image
        node.extension = "REPEAT"
        node.location = (-720, y)
        links.new(uvnode.outputs["UV"], node.inputs["Vector"])
        return node

    soil = texture_node("SourceTile_Soil_1K", image_paths["soil"], 440)
    leaves = texture_node("SourceTile_Leaves_1K", image_paths["leaves"], 190)
    roots = texture_node("SourceTile_Roots_1K", image_paths["roots"], -80)

    def math_node(operation, a, b, location):
        node = nodes.new("ShaderNodeMath")
        node.operation = operation
        node.location = location
        if hasattr(a, "node"):
            links.new(a, node.inputs[0])
        else:
            node.inputs[0].default_value = a
        if hasattr(b, "node"):
            links.new(b, node.inputs[1])
        else:
            node.inputs[1].default_value = b
        return node.outputs[0]

    path_dark = math_node("MULTIPLY", separate.outputs["X"], 0.23, (-560, -420))
    leaf_factor = math_node("MULTIPLY", separate.outputs["Y"], 1.0, (-560, -220))
    leaf_suppress = math_node("MULTIPLY", separate.outputs["X"], 0.88, (-560, -560))
    leaf_factor = math_node("MULTIPLY", leaf_factor, math_node("SUBTRACT", 1.0, leaf_suppress, (-360, -500)), (-160, -210))
    root_suppress = math_node("MULTIPLY", separate.outputs["X"], 0.82, (-560, -650))
    root_factor = math_node("MULTIPLY", separate.outputs["Z"], math_node("SUBTRACT", 1.0, root_suppress, (-360, -690)), (-160, -90))

    blend_leaves = nodes.new("ShaderNodeMixRGB")
    blend_leaves.blend_type = "MIX"
    blend_leaves.location = (-100, 300)
    links.new(leaf_factor, blend_leaves.inputs[0])
    links.new(soil.outputs["Color"], blend_leaves.inputs[1])
    links.new(leaves.outputs["Color"], blend_leaves.inputs[2])
    blend_roots = nodes.new("ShaderNodeMixRGB")
    blend_roots.blend_type = "MIX"
    blend_roots.location = (120, 240)
    links.new(root_factor, blend_roots.inputs[0])
    links.new(blend_leaves.outputs["Color"], blend_roots.inputs[1])
    links.new(roots.outputs["Color"], blend_roots.inputs[2])
    dark_path = nodes.new("ShaderNodeMixRGB")
    dark_path.blend_type = "MIX"
    dark_path.location = (360, 150)
    links.new(path_dark, dark_path.inputs[0])
    links.new(blend_roots.outputs["Color"], dark_path.inputs[1])
    dark_path.inputs[2].default_value = (0.55, 0.55, 0.55, 1.0)
    links.new(dark_path.outputs["Color"], emission.inputs["Color"])

    ground.data.materials.append(material)
    return ground, verts, heights, xs, zs, material, mask_values


def bake_material(material, ground, output_dir: Path):
    image = bpy.data.images.new("W01_ForestGround_BakedAlbedo_2K", width=BAKE_SIZE, height=BAKE_SIZE, alpha=False, float_buffer=False)
    image.colorspace_settings.name = "sRGB"
    image.filepath_raw = str(output_dir / BAKED_ALBEDO_FILENAME)
    image.file_format = "PNG"
    target = material.node_tree.nodes.new("ShaderNodeTexImage")
    target.name = "BAKE_TARGET_Ground_2K"
    target.label = "Cycles EMIT bake target"
    target.image = image
    target.location = (700, -360)
    for node in material.node_tree.nodes:
        node.select = False
    target.select = True
    material.node_tree.nodes.active = target

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.render.bake.margin = 12
    scene.render.bake.use_clear = True
    bpy.ops.object.select_all(action="DESELECT")
    ground.select_set(True)
    bpy.context.view_layer.objects.active = ground
    bpy.ops.object.bake(type="EMIT")
    image.save()
    image.pack()

    # The exported material is deliberately simple and glTF-friendly; the
    # three source tiles and vertex masks remain packed in the .blend as the
    # procedural provenance and the per-vertex channels remain in the mesh.
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (420, 80)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (120, 80)
    principled.inputs["Roughness"].default_value = 0.94
    baked_node = nodes.new("ShaderNodeTexImage")
    baked_node.name = "W01_Ground_CyclesBakedAlbedo_2K"
    baked_node.image = image
    baked_node.location = (-160, 80)
    links.new(baked_node.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return image


def configure_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.images):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.14, 0.18, 0.17, 1.0)
    background.inputs["Strength"].default_value = 0.55
    return scene


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_preview_rig(scene):
    sun_data = bpy.data.lights.new("Preview_SoftAfternoon_Sun", "SUN")
    sun_data.energy = 2.0
    sun = bpy.data.objects.new("Preview_SoftAfternoon_Sun", sun_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(24), math.radians(-18), math.radians(-28))

    fill_data = bpy.data.lights.new("Preview_SkyFill", "AREA")
    fill_data.energy = 18000
    fill_data.shape = "DISK"
    fill_data.size = 120
    fill = bpy.data.objects.new("Preview_SkyFill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (44, 35, 72)
    look_at(fill, (40, -75, 2))

    overview_data = bpy.data.cameras.new("Preview_Overview_Camera")
    overview = bpy.data.objects.new("Preview_Overview_Camera", overview_data)
    bpy.context.collection.objects.link(overview)
    overview.location = (46, -86, 76)
    look_at(overview, (40, -75, 2))
    overview_data.type = "ORTHO"
    overview_data.ortho_scale = 184
    overview_data.lens = 40

    path_data = bpy.data.cameras.new("Preview_F01_to_F02_Close_Camera")
    path_camera = bpy.data.objects.new("Preview_F01_to_F02_Close_Camera", path_data)
    bpy.context.collection.objects.link(path_camera)
    path_camera.location = (41.0, -58.0, 19.0)
    look_at(path_camera, (28.5, -72.5, 2.0))
    path_data.type = "ORTHO"
    path_data.ortho_scale = 56
    path_data.lens = 35
    return overview, path_camera


def route_grade_summary():
    summary = {}
    for route in ROUTES:
        parts = []
        for first, second in zip(route["points"], route["points"][1:]):
            dx, dz = second[0] - first[0], second[2] - first[2]
            length = math.hypot(dx, dz)
            parts.append(abs(second[1] - first[1]) / length)
        first, second = route["points"][0], route["points"][-1]
        dx, dz = second[0] - first[0], second[2] - first[2]
        length = math.hypot(dx, dz)
        samples = max(1, math.ceil(length / 0.25))
        heights = []
        for index in range(samples + 1):
            t = index / samples
            x = first[0] + dx * t
            z = first[2] + dz * t
            heights.append(surface_height_and_masks(x, z)[0])
        actual_grade = max(abs(b - a) / (length / samples) for a, b in zip(heights, heights[1:]))
        summary["↔".join(route["edge"])] = {
            "max_authored_or_default_segment_grade": max(parts),
            "max_sampled_surface_grade": actual_grade,
            "width_m": ROUTE_HALF_WIDTH * 2.0,
            "route_kind": route["kind"],
        }
    return summary


def verify_mesh(verts, xs, zs):
    actual = {}
    for node_id in BUILD_NODE_IDS:
        node = NODES[node_id]
        x, y, z = node["xyz"]
        try:
            ix = xs.index(x)
            iz = zs.index(z)
        except ValueError as error:
            raise RuntimeError(f"Authored anchor {node_id} is not a mesh vertex") from error
        index = iz * len(xs) + ix
        actual[node_id] = verts[index][2]
        if abs(actual[node_id] - y) > 1e-6:
            raise RuntimeError(f"Authored height mismatch at {node_id}: {actual[node_id]} vs {y}")
    return actual


def make_manifest(root, output_dir, ground, verts, xs, zs, node_heights, image_paths, bake_path):
    spec_path = root / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/data/assets.spec.json"
    level_path = root / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/levels/W01_Лес_за_спиной_RU.md"
    palette_path = root / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/docs/04_ART_DIRECTION_RU.md"
    route_points = {}
    for route in ROUTES:
        route_points["↔".join(route["edge"])] = [list(map(float, point)) for point in route["points"]]
    godot_bounds = {
        "x": [min(v[0] for v in verts), max(v[0] for v in verts)],
        "y": [min(v[2] for v in verts), max(v[2] for v in verts)],
        "z": [min(-v[1] for v in verts), max(-v[1] for v in verts)],
    }
    textures = {}
    for key, path in image_paths.items():
        textures[key] = {
            "file": path.name,
            "sha256": sha256(path),
            "size_px": [TEXTURE_SIZE, TEXTURE_SIZE],
            "provenance": "project-original deterministic tileable multiscale value-noise pixels; no third-party source",
            "sRGB": key != "masks",
        }
    textures["cycles_baked_albedo"] = {
        "file": bake_path.name,
        "sha256": sha256(bake_path),
        "size_px": [BAKE_SIZE, BAKE_SIZE],
        "provenance": "Blender Cycles EMIT bake from the three project-original 1K tiles blended by ForestMasks vertex channels",
        "sRGB": True,
    }
    preview_files = ["W01_ForestGround_F00_to_F03_Overview.png", "W01_ForestGround_F01_to_F02_Close.png"]
    return {
        "asset_id": "A-FOREST-GROUND",
        "consumer": "W01",
        "stage_only": True,
        "scene_ready": False,
        "native_or_godot_import_verified": False,
        "collision_surface_generated_or_verified": False,
        "source_script": str(Path(__file__).resolve()),
        "source_script_sha256": sha256(Path(__file__).resolve()),
        "input_specs": {
            "asset_spec": str(spec_path.relative_to(root)),
            "asset_spec_sha256": sha256(spec_path),
            "level": str(level_path.relative_to(root)),
            "level_sha256": sha256(level_path),
            "palette": str(palette_path.relative_to(root)),
            "palette_sha256": sha256(palette_path),
            "palette_srgb": PALETTE_SRGB,
            "palette_note": "PNG pixels are sRGB; Blender image nodes use sRGB so Blender decodes to linear for material evaluation.",
        },
        "dimensions_m": {"x": X_BOUNDS[1] - X_BOUNDS[0], "z": Z_BOUNDS[1] - Z_BOUNDS[0], "height_range": godot_bounds["y"]},
        "coordinate_mapping": {
            "Godot_xyz": ["X", "Y", "Z"],
            "Blender_xyz_before_export": ["Godot X", "-Godot Z", "Godot Y"],
            "GLTF_Y_up_export_mapping": "(Blender X, Blender Z, -Blender Y)",
            "imported_Godot_xyz": ["Godot X", "Godot Y", "Godot +Z"],
            "root": "world origin at F00",
        },
        "godot_bounds_m": godot_bounds,
        "mesh": {
            "file_blend": "prologue-ground-f00-f03-v1.blend",
            "file_glb": "prologue-ground-f00-f03-v1.glb",
            "object": ground.name,
            "vertex_count": len(verts),
            "triangle_count": len(ground.data.polygons),
            "x_axis_coordinate_count": len(xs),
            "z_axis_coordinate_count": len(zs),
            "regular_grid_step_m": GRID_STEP,
            "expected_triangles": 2 * (len(xs) - 1) * (len(zs) - 1),
            "gltf_vertex_mask_attribute": "COLOR_0 normalized VEC4",
            "gltf_vertex_mask_material_note": "Standard glTF PBR multiplies COLOR_0 into base color. The W01 Godot consumer must disable vertex_color_use_as_albedo or use a shader that consumes ForestMasks separately; standard glTF has no material flag to disable this multiplication.",
            "uv_sets": {
                "BakeUV": "world-aligned full terrain atlas UV for the Cycles-baked 2K albedo",
                "MetricUV": f"world X/Z divided by {METRES_PER_REPEAT:g} m; repeats every {METRES_PER_REPEAT:g} m",
            },
            "visual_and_collision_surface_candidate": "same single terrain mesh; no separate height/normal-only relief",
            "collision_note": "candidate geometry only; no Godot collision resource or engine contact check in this stage",
        },
        "coverage_scope": "authored F00→F01→F02→F03 prologue ground route only",
        "unproduced_authored_nodes": [node_id for node_id in NODES if node_id not in BUILD_NODE_IDS],
        "authored_nodes": {
            node_id: {
                "label": node["label"],
                "role": node["role"],
                "xyz_m": list(node["xyz"]),
                "terrain_vertex_y_m": node_heights[node_id],
                "height_matches_author": abs(node_heights[node_id] - node["xyz"][1]) < 1e-6,
                "walkable_pad_mask_radius_m": 1.2,
            }
            for node_id, node in NODES.items() if node_id in BUILD_NODE_IDS
        },
        "physical_routes": {
            edge: {
                "polyline_xyz_m": route_points[edge],
                "width_m": ROUTE_HALF_WIDTH * 2.0,
                "max_grade": route_grade_summary()[edge]["max_authored_or_default_segment_grade"],
                "max_sampled_surface_grade": route_grade_summary()[edge]["max_sampled_surface_grade"],
                "route_kind": route_grade_summary()[edge]["route_kind"],
            }
            for edge in route_points
        },
        "spatial_link": {
            "entry": "F12",
            "exit": "F13",
            "asset_surface_relation": "outside this bounded F00→F03 prologue route",
            "physical_corridor_generated_or_advertised": False,
            "note": "F12↔F13 is a spatial transform, not an ordinary physical route; neither anchor nor corridor is included in this prologue asset.",
        },
        "vertex_masks": {
            "attribute": "ForestMasks",
            "domain": "POINT",
            "glTF_attribute": "COLOR_0 normalized VEC4",
            "channels": {"R": "compacted readable route", "G": "local colored leaf coverage", "B": "soft root-mass coverage", "A": "subtle moisture variation"},
        },
        "gltf_vertex_color_export": {
            "mode": "NAME",
            "name": "ForestMasks",
            "export_all_vertex_colors": False,
            "material_semantics": "Standard glTF PBR multiplies COLOR_0 into base color. The W01 Godot consumer must disable vertex_color_use_as_albedo or use a shader that consumes ForestMasks separately; standard glTF has no material flag to disable this multiplication.",
        },
        "textures": textures,
        "texture_provenance": {
            "source": "original deterministic tileable multiscale value-noise patterns produced by this Blender source",
            "external_textures_used": [],
            "shared_repeat_m": METRES_PER_REPEAT,
            "bake_tool": "Blender Cycles EMIT bake",
            "bake_uv": "BakeUV",
            "bake_input": "three shared 1K albedo maps and ForestMasks vertex channels",
        },
        "production_defaults": [
            "A continuous, low-relief surface follows the exact F00→F01→F02→F03 node heights; its shallow shoulders are a production default.",
            "This asset deliberately stops at F03 and does not represent the main W01 F10–F16 route or spatial link.",
            "Leaf and root fields are terrain materials/masks; no separate debris, fence, tree, or gameplay prop is included.",
        ],
        "offline_previews": preview_files,
        "verification_scope": {
            "source_generated": True,
            "mesh_metrics_checked": True,
            "authored_node_heights_checked": True,
            "textures_written_and_hashed": True,
            "blender_rendered_overview_and_f01_f02_close": True,
            "godot_import": "not run",
            "walk_and_collision_contact": "not run",
            "art_author_review": "pending",
        },
    }


def main():
    args = parse_args()
    output_dir = args.output_dir.expanduser().resolve()
    if output_dir.exists():
        raise RuntimeError(f"Refusing to overwrite existing staging directory: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)
    root = project_root()
    scene = configure_scene()
    image_paths = png_material_images(output_dir)
    ground, verts, heights, xs, zs, material, mask_values = create_mesh(image_paths)
    node_heights = verify_mesh(verts, xs, zs)
    bake_image = bake_material(material, ground, output_dir)
    bake_path = output_dir / BAKED_ALBEDO_FILENAME

    overview, close_camera = add_preview_rig(scene)
    scene.camera = overview
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output_dir / "W01_ForestGround_F00_to_F03_Overview.png")
    bpy.ops.render.render(write_still=True)

    scene.camera = close_camera
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.filepath = str(output_dir / "W01_ForestGround_F01_to_F02_Close.png")
    bpy.ops.render.render(write_still=True)

    scene.camera = overview
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.filepath = "//"
    blend_path = output_dir / "prologue-ground-f00-f03-v1.blend"
    glb_path = output_dir / "prologue-ground-f00-f03-v1.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)

    bpy.ops.object.select_all(action="DESELECT")
    ground.select_set(True)
    bpy.context.view_layer.objects.active = ground
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_vertex_color="NAME",
        export_vertex_color_name="ForestMasks",
        export_all_vertex_colors=False,
    )
    if not glb_path.is_file() or not blend_path.is_file() or not bake_path.is_file():
        raise RuntimeError("Asset export missed one or more required binary outputs")

    manifest = make_manifest(root, output_dir, ground, verts, xs, zs, node_heights, image_paths, bake_path)
    manifest["mesh"]["file_blend_sha256"] = sha256(blend_path)
    manifest["mesh"]["file_glb_sha256"] = sha256(glb_path)
    manifest["mesh"]["file_blend_bytes"] = blend_path.stat().st_size
    manifest["mesh"]["file_glb_bytes"] = glb_path.stat().st_size
    for preview in manifest["offline_previews"]:
        path = output_dir / preview
        manifest.setdefault("preview_receipts", {})[preview] = {"sha256": sha256(path), "bytes": path.stat().st_size}
    manifest["mesh"]["triangle_count"] = len(ground.data.polygons)
    manifest["mesh"]["vertex_count"] = len(ground.data.vertices)
    manifest_path = output_dir / "prologue-ground-f00-f03-v1.manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "output_dir": str(output_dir),
        "blend": str(blend_path),
        "glb": str(glb_path),
        "manifest": str(manifest_path),
        "vertex_count": len(ground.data.vertices),
        "triangle_count": len(ground.data.polygons),
        "x_count": len(xs),
        "z_count": len(zs),
        "node_heights": node_heights,
        "routes": route_grade_summary(),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
