"""Author the opaque, walkable W01 hero-root spatial-link module.

Run with Blender 4.5+:
  blender --background --python assets/source/blender/photoworlds/W01/hero_root_turn_v1.py \
    -- --output-dir /private/tmp/urman-hero-root-turn-YYYYMMDD \
       --ground-manifest /private/tmp/prologue-ground-f00-f14-v1.manifest.json

The asset is authored in Godot-local metres then converted to Blender's Y-up
coordinates. The paired W01 instances align their `SpatialLinkFrame`; the
player enters at `WalkEntry`, crosses the link plane through the opaque bend,
and continues from `WalkExit`. Runtime transforms and collision ownership stay
in Godot. No frame, camera, light, or preview-only geometry is exported.
"""

from __future__ import annotations

import argparse
from array import array
import hashlib
import json
import math
import random
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


ASSET_ID = "A-HERO-ROOT"
ASSET_NAME = "hero-root-turn-v1"
FPS = 24
BAKE_SIZE = 2048
ROOT_BARK_REPEAT_M = 1.0
WOOD_GRAIN_TEXTURE = "game/assets/textures/painterly/wood_split_firewood_v1_albedo.png"
FRESH_SNOW_TEXTURE = "game/assets/textures/painterly/snow_fresh_v1_albedo.png"
TRAMPLED_SNOW_TEXTURE = "game/assets/textures/painterly/snow_trampled_v2_albedo.png"
ROUTE_HALF_WIDTH_M = 1.10
ROUTE_HEADROOM_M = 2.15
SPATIAL_LINK_LOCAL_GODOT = (-5.0, 0.0, 0.0)
WALK_ENTRY_LOCAL_GODOT = (0.0, 0.0, -8.0)
WALK_EXIT_LOCAL_GODOT = (0.0, 0.0, 8.0)
SPATIAL_LINK_WORLD_YAW_DEG = 27.85
ROUTE_WAYPOINTS_GODOT = [
    (0.0, 0.0, -8.0),
    (-2.5, 0.0, -4.0),
    (-5.0, 0.0, 0.0),
    (-2.5, 0.0, 4.0),
    (0.0, 0.0, 8.0),
]
GROUND_MANIFEST_INPUT = None
GROUND_MANIFEST_SHA256 = None
GROUND_SOURCE_SHA256 = None
GROUND_ENTRY_WORLD_GODOT = None
GROUND_EXIT_WORLD_GODOT = None


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--ground-manifest", required=True, type=Path)
    return parser.parse_args(tail)


def project_root() -> Path:
    return Path(__file__).resolve().parents[5]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def godot_to_blender(point):
    x, y, z = point
    return (x, -z, y)


def blender_to_godot(point):
    x, y, z = point
    return (x, z, -y)


def smoothstep01(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def load_ground_contract(path: Path):
    global GROUND_MANIFEST_INPUT, GROUND_MANIFEST_SHA256, GROUND_SOURCE_SHA256
    global GROUND_ENTRY_WORLD_GODOT, GROUND_EXIT_WORLD_GODOT
    global SPATIAL_LINK_LOCAL_GODOT, WALK_ENTRY_LOCAL_GODOT, WALK_EXIT_LOCAL_GODOT
    global SPATIAL_LINK_WORLD_YAW_DEG, ROUTE_WAYPOINTS_GODOT

    path = path.expanduser().resolve()
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("asset_id") != "A-FOREST-GROUND":
        raise RuntimeError("--ground-manifest must identify the generated A-FOREST-GROUND input")
    contract = document.get("spatial_link", {}).get("module_contract")
    if not isinstance(contract, dict):
        raise RuntimeError("Ground manifest is missing its spatial_link.module_contract")

    frame = tuple(float(value) for value in contract.get("frame_local_godot_xyz_m", ()))
    entry = tuple(float(value) for value in contract.get("walk_entry_local_godot_xyz_m", ()))
    exit_point = tuple(float(value) for value in contract.get("walk_exit_local_godot_xyz_m", ()))
    yaw = float(contract.get("world_frame_yaw_default_deg", math.nan))
    if frame != SPATIAL_LINK_LOCAL_GODOT:
        raise RuntimeError(f"Unexpected hero-root SpatialLinkFrame contract: {frame}")
    if len(entry) != 3 or abs(entry[0]) > 1e-6 or abs(entry[2] + 8.0) > 1e-6:
        raise RuntimeError(f"Unexpected hero-root WalkEntry contract: {entry}")
    if len(exit_point) != 3 or abs(exit_point[0]) > 1e-6 or abs(exit_point[2] - 8.0) > 1e-6:
        raise RuntimeError(f"Unexpected hero-root WalkExit contract: {exit_point}")
    if not math.isfinite(yaw) or abs(yaw - SPATIAL_LINK_WORLD_YAW_DEG) > 0.02:
        raise RuntimeError(f"Unexpected hero-root world-frame yaw: {yaw}")
    if not all(math.isfinite(value) for value in (*entry, *exit_point)):
        raise RuntimeError("Ground manifest contains a non-finite hero-root endpoint")

    SPATIAL_LINK_LOCAL_GODOT = frame
    WALK_ENTRY_LOCAL_GODOT = entry
    WALK_EXIT_LOCAL_GODOT = exit_point
    SPATIAL_LINK_WORLD_YAW_DEG = yaw
    ROUTE_WAYPOINTS_GODOT = [
        (entry[0], entry[1], entry[2]),
        (-2.5, entry[1] * 0.5, -4.0),
        (frame[0], frame[1], frame[2]),
        (-2.5, exit_point[1] * 0.5, 4.0),
        (exit_point[0], exit_point[1], exit_point[2]),
    ]
    GROUND_MANIFEST_INPUT = "assets/source/blender/photoworlds/W01/prologue-ground-f00-f14-v1.manifest.json"
    GROUND_MANIFEST_SHA256 = sha256(path)
    GROUND_SOURCE_SHA256 = document.get("source_script_sha256")
    GROUND_ENTRY_WORLD_GODOT = tuple(float(value) for value in contract["walk_entry_world_godot_xyz_m"])
    GROUND_EXIT_WORLD_GODOT = tuple(float(value) for value in contract["walk_exit_world_godot_xyz_m"])
    if not GROUND_SOURCE_SHA256:
        raise RuntimeError("Ground manifest is missing its source-script hash")
    return document, contract


def route_x(z: float) -> float:
    t = (z + 8.0) / 16.0
    return -5.0 * math.sin(math.pi * t) ** 2


def route_center(z: float) -> Vector:
    if z <= 0.0:
        y = WALK_ENTRY_LOCAL_GODOT[1] * (1.0 - smoothstep01((z + 8.0) / 8.0))
    else:
        y = WALK_EXIT_LOCAL_GODOT[1] * smoothstep01(z / 8.0)
    return Vector((route_x(z), y, z))


def route_curvature_radius(z: float) -> float:
    # x(z)=-5 sin²(pi(z+8)/16); calculate planar curvature in metres.
    t = (z + 8.0) / 16.0
    first = -(5.0 * math.pi / 16.0) * math.sin(2.0 * math.pi * t)
    second = -(5.0 * math.pi * math.pi / 128.0) * math.cos(2.0 * math.pi * t)
    curvature = abs(second) / ((1.0 + first * first) ** 1.5)
    return math.inf if curvature < 1e-9 else 1.0 / curvature


def write_png_rgb(path: Path, width: int, height: int, pixel):
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (len(payload).to_bytes(4, "big") + kind + payload
                + (zlib.crc32(kind + payload) & 0xFFFFFFFF).to_bytes(4, "big"))

    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(max(0, min(255, int(v))) for v in pixel(x, y))
    data = bytearray(b"\x89PNG\r\n\x1a\n")
    data.extend(chunk(b"IHDR", width.to_bytes(4, "big") + height.to_bytes(4, "big")
                      + bytes((8, 2, 0, 0, 0))))
    data.extend(chunk(b"IDAT", zlib.compress(bytes(raw), level=8)))
    data.extend(chunk(b"IEND", b""))
    path.write_bytes(data)


def make_mask_pixel():
    # Deterministic tileable value-noise mask: R bark relief, G lichen, B
    # worn/root grain. It is unique to this authored asset and used in material.
    cells = (5, 17, 43, 103)
    seeds = (79, 181, 353, 701)

    def lattice(ix, iy, count, seed):
        ix %= count
        iy %= count
        value = (ix * 374761393 + iy * 668265263 + seed * 1442695041) & 0xFFFFFFFF
        value = ((value ^ (value >> 13)) * 1274126177) & 0xFFFFFFFF
        value ^= value >> 16
        return value / 0xFFFFFFFF

    def noise(x, y, count, seed):
        u, v = x / BAKE_SIZE * count, y / BAKE_SIZE * count
        ix, iy = math.floor(u), math.floor(v)
        fx, fy = smoothstep01(u - ix), smoothstep01(v - iy)
        a = lattice(ix, iy, count, seed)
        b = lattice(ix + 1, iy, count, seed)
        c = lattice(ix, iy + 1, count, seed)
        d = lattice(ix + 1, iy + 1, count, seed)
        lower = a * (1.0 - fx) + b * fx
        upper = c * (1.0 - fx) + d * fx
        return lower * (1.0 - fy) + upper * fy

    def pixel(x, y):
        broad = noise(x, y, cells[0], seeds[0])
        mid = noise(x, y, cells[1], seeds[1])
        fine = noise(x, y, cells[2], seeds[2])
        grain = noise(x, y, cells[3], seeds[3])
        relief = max(0.0, min(1.0, 0.47 * broad + 0.33 * mid + 0.20 * fine))
        lichen = max(0.0, min(1.0, (broad * 0.58 + mid * 0.42 - 0.63) * 2.8))
        ridges = 0.5 + 0.5 * math.sin((x / BAKE_SIZE * 42.0 + 0.14 * math.sin(y / BAKE_SIZE * 20.0)) * math.tau)
        worn = max(0.0, min(1.0, 0.55 * grain + 0.45 * ridges))
        return (round(255 * relief), round(255 * lichen), round(255 * worn))
    return pixel


def configure_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.actions, bpy.data.images, bpy.data.cameras,
                       bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = FPS
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1050
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs["Color"].default_value = (0.14, 0.17, 0.16, 1.0)
    scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.62
    return scene


def bake_bark_mask_albedo(bark, mask, output_dir: Path):
    bark_width, bark_height = bark.size
    mask_width, mask_height = mask.size
    if (mask_width, mask_height) != (BAKE_SIZE, BAKE_SIZE):
        raise RuntimeError("Hero-root material mask size does not match the 2K albedo bake")

    bark_pixels = array("f", [0.0]) * len(bark.pixels)
    mask_pixels = array("f", [0.0]) * len(mask.pixels)
    bark.pixels.foreach_get(bark_pixels)
    mask.pixels.foreach_get(mask_pixels)
    baked_pixels = array("f")
    lichen_color = (0.20, 0.25, 0.18)
    for y in range(BAKE_SIZE):
        source_y = y * bark_height // mask_height
        for x in range(BAKE_SIZE):
            source_x = x * bark_width // mask_width
            bark_index = (source_y * bark_width + source_x) * 4
            mask_index = (y * mask_width + x) * 4
            lichen = mask_pixels[mask_index + 1] * 0.42
            for channel in range(3):
                base = bark_pixels[bark_index + channel]
                baked_pixels.append(base * (1.0 - lichen) + lichen_color[channel] * lichen)
            baked_pixels.append(1.0)

    output_path = output_dir / f"{ASSET_NAME}-albedo-2k.png"
    albedo = bpy.data.images.new(f"{ASSET_NAME}_BakedAlbedo_2K", width=BAKE_SIZE,
                                 height=BAKE_SIZE, alpha=False, float_buffer=False)
    albedo.colorspace_settings.name = "sRGB"
    albedo.filepath_raw = str(output_path)
    albedo.file_format = "PNG"
    albedo.pixels.foreach_set(baked_pixels)
    albedo.update()
    albedo.save()
    albedo.pack()
    return albedo, output_path


def bark_material(root: Path, mask_path: Path, output_dir: Path):
    receipt_path = root / "game/assets/textures/painterly/procedural_bake_receipt_v1.json"
    bark_path = root / "game/assets/textures/painterly/bark_spruce_v1_albedo.png"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    entry = receipt["textures"]["F03"]
    actual_hash = sha256(bark_path)
    if entry.get("file") != "game/assets/textures/painterly/bark_spruce_v1_albedo.png":
        raise RuntimeError("Project bark provenance receipt points at another input")
    if entry.get("licence") != "project-original procedural (no third-party source)" or entry.get("sha256") != actual_hash:
        raise RuntimeError("Project bark provenance hash/licence does not match selected input")
    if entry.get("metres_per_repeat") != ROOT_BARK_REPEAT_M:
        raise RuntimeError("Project bark repeat distance changed; update the authored UV scale")

    material = bpy.data.materials.new("HeroRoot_ProjectOriginalBark_Masked")
    material.use_nodes = True
    material.diffuse_color = (0.31, 0.28, 0.22, 1.0)
    material["semantic_surface"] = "project-original bark with asset-specific lichen/root mask"
    material["bark_source_receipt"] = "game/assets/textures/painterly/procedural_bake_receipt_v1.json#textures.F03"
    material["bark_repeat_m"] = ROOT_BARK_REPEAT_M
    material["unique_mask"] = mask_path.name
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (760, 80)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (500, 80)
    shader.inputs["Roughness"].default_value = 0.83
    shader.inputs["Metallic"].default_value = 0.0
    uv = nodes.new("ShaderNodeTexCoord")
    uv.location = (-760, 100)
    bark = bpy.data.images.load(str(bark_path), check_existing=True)
    bark.name = "HeroRoot_Reused_ProjectBark"
    bark.colorspace_settings.name = "sRGB"
    bark.pack()
    mask = bpy.data.images.load(str(mask_path), check_existing=False)
    mask.name = "HeroRoot_Unique_2K_MaterialMask"
    mask.colorspace_settings.name = "Non-Color"
    mask.pack()
    albedo, albedo_path = bake_bark_mask_albedo(bark, mask, output_dir)
    albedo_node = nodes.new("ShaderNodeTexImage")
    albedo_node.image = albedo
    albedo_node.extension = "REPEAT"
    albedo_node.location = (-300, 120)
    links = material.node_tree.links
    links.new(uv.outputs["UV"], albedo_node.inputs["Vector"])
    links.new(albedo_node.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material["baked_albedo"] = albedo_path.name
    material["material_mask_usage"] = "Green channel blends the project bark with subtle lichen color before the GLTF-safe albedo bake."
    return material, bark_path, receipt_path, entry, albedo_path


def textured_material(root: Path, name: str, relative_texture: str, roughness: float):
    """Use a project-owned runtime map for the exposed wood and snow surfaces."""
    texture_path = root / relative_texture
    if not texture_path.is_file():
        raise RuntimeError(f"Required project material texture is missing: {relative_texture}")
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (0.72, 0.72, 0.72, 1.0)
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    uv = nodes.new("ShaderNodeTexCoord")
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    image.colorspace_settings.name = "sRGB"
    image.pack()
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.extension = "REPEAT"
    material.node_tree.links.new(uv.outputs["UV"], tex.inputs["Vector"])
    material.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    material["source_texture"] = relative_texture
    material["source_texture_sha256"] = sha256(texture_path)
    material["surface_role"] = name
    return material


def make_mesh_object(name, godot_vertices, faces, uv_coords, material, parent):
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    blender_vertices = [godot_to_blender(point) for point in godot_vertices]
    mesh.from_pydata(blender_vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap_metric")
    for polygon in mesh.polygons:
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = uv_coords[vertex_index]
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.data.materials.append(material)
    orient_normals(obj)
    return obj


def make_tube(name, points, radii, material, parent, sides=20, repeat=ROOT_BARK_REPEAT_M,
              phase=0.0, ridge_strength=0.04):
    points = [Vector(point) for point in points]
    if len(points) != len(radii) or len(points) < 2:
        raise ValueError(f"Tube {name} needs paired points and radii")
    vertices, faces, uv = [], [], []
    arc = [0.0]
    for a, b in zip(points, points[1:]):
        arc.append(arc[-1] + (b - a).length)
    for index, (center, radius) in enumerate(zip(points, radii)):
        if index == 0:
            tangent = (points[1] - points[0]).normalized()
        elif index == len(points) - 1:
            tangent = (points[-1] - points[-2]).normalized()
        else:
            tangent = (points[index + 1] - points[index - 1]).normalized()
        reference = Vector((0.0, 1.0, 0.0))
        if abs(tangent.dot(reference)) > 0.88:
            reference = Vector((1.0, 0.0, 0.0))
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        for side in range(sides):
            angle = math.tau * side / sides
            rib = 1.0 + ridge_strength * math.cos(7.0 * angle + phase) + 0.018 * math.sin(13.0 * angle + phase * 1.7)
            point = center + radius * rib * (math.cos(angle) * normal + math.sin(angle) * binormal)
            vertices.append(tuple(point))
            uv.append((arc[index] / repeat, side / sides * 4.0))
    for index in range(len(points) - 1):
        for side in range(sides):
            a = index * sides + side
            b = index * sides + (side + 1) % sides
            c = (index + 1) * sides + (side + 1) % sides
            d = (index + 1) * sides + side
            faces.append((a, b, c, d))
    for end_index, reverse in ((0, True), (len(points) - 1, False)):
        center_index = len(vertices)
        vertices.append(tuple(points[end_index]))
        uv.append((0.0, 0.0))
        ring_start = end_index * sides
        for side in range(sides):
            vertices.append(vertices[ring_start + side])
            uv.append((0.0, side / sides))
        for side in range(sides):
            tri = (center_index, center_index + 1 + side, center_index + 1 + (side + 1) % sides)
            faces.append(tuple(reversed(tri)) if reverse else tri)
    obj = make_mesh_object(name, vertices, faces, uv, material, parent)
    return obj


def make_trunk(name, center_x, center_z, height, base_radius, lean_x, lean_z,
               material, parent, rings=38, sides=48, phase=0.0):
    vertices, faces, uv = [], [], []
    for ring in range(rings + 1):
        t = ring / rings
        y = 0.18 + height * t
        profile = (1.0 - t) ** 0.78
        radius = 0.16 + base_radius * profile
        cx = center_x + lean_x * t + 0.10 * math.sin(t * 5.2 + phase) * t
        cz = center_z + lean_z * t + 0.12 * math.sin(t * 4.4 + phase * 0.7) * t
        for side in range(sides):
            angle = math.tau * side / sides
            rib = 1.0 + 0.045 * math.sin(5.0 * angle + phase) + 0.025 * math.cos(11.0 * angle - t * 2.0)
            rib *= 1.0 + 0.018 * math.sin(19.0 * angle + t * 28.0 + phase)
            r = radius * rib
            vertices.append((cx + math.cos(angle) * r, y, cz + math.sin(angle) * r))
            uv.append((angle / math.tau * 4.0, y / ROOT_BARK_REPEAT_M))
    for ring in range(rings):
        for side in range(sides):
            a = ring * sides + side
            b = ring * sides + (side + 1) % sides
            c = (ring + 1) * sides + (side + 1) % sides
            d = (ring + 1) * sides + side
            faces.append((a, b, c, d))
    bottom = len(vertices)
    vertices.append((center_x, 0.18, center_z))
    uv.append((0.0, 0.0))
    top = len(vertices)
    vertices.append((center_x + lean_x, 0.18 + height, center_z + lean_z))
    uv.append((0.0, height))
    for side in range(sides):
        faces.append((bottom, (side + 1) % sides, side))
        ring = rings * sides
        faces.append((top, ring + side, ring + (side + 1) % sides))
    return make_mesh_object(name, vertices, faces, uv, material, parent)


def make_irregular_ellipsoid(name, center, radii, material, parent,
                             sectors=64, rings=32, phase=0.0):
    cx, cy, cz = center
    rx, ry, rz = radii
    vertices, faces, uv = [], [], []
    for ring in range(rings + 1):
        phi = -math.pi / 2 + math.pi * ring / rings
        cp, sp = math.cos(phi), math.sin(phi)
        for side in range(sectors):
            theta = math.tau * side / sectors
            irregular = (1.0 + 0.055 * math.sin(3.0 * theta + phase) * cp
                         + 0.028 * math.sin(7.0 * theta - 3.0 * phi + phase)
                         + 0.014 * math.cos(13.0 * theta + 4.0 * phi))
            vertices.append((cx + rx * cp * math.cos(theta) * irregular,
                             cy + ry * sp * irregular,
                             cz + rz * cp * math.sin(theta) * irregular))
            uv.append((side / sectors * rx * math.tau / ROOT_BARK_REPEAT_M,
                       (ring / rings) * math.pi * max(rz, ry) / ROOT_BARK_REPEAT_M))
    for ring in range(rings):
        for side in range(sectors):
            a = ring * sectors + side
            b = ring * sectors + (side + 1) % sectors
            c = (ring + 1) * sectors + (side + 1) % sectors
            d = (ring + 1) * sectors + side
            faces.append((a, b, c, d))
    return make_mesh_object(name, vertices, faces, uv, material, parent)


def make_fallen_bole(name, profiles, bark_material, wood_material, parent, sides=48):
    """Create a grounded, tapered bole with one exposed split end.

    Profile rows are Godot-local (x, y, z, vertical radius, lateral radius).
    The final ring remains open so the separate end-grain mesh and fibers define
    the break instead of a smooth bark-colored cone.
    """
    centers = [tuple(row[:3]) for row in profiles]
    vertical_radii = [row[3] for row in profiles]
    lateral_radii = [row[4] for row in profiles]
    sampled, sampled_vertical = tube_points(centers, vertical_radii, samples_per_segment=10)
    _, sampled_lateral = tube_points(centers, lateral_radii, samples_per_segment=10)
    vertices, faces, uv = [], [], []
    arc = [0.0]
    for first, second in zip(sampled, sampled[1:]):
        arc.append(arc[-1] + (Vector(second) - Vector(first)).length)

    frames = []
    for index, center_tuple in enumerate(sampled):
        center = Vector(center_tuple)
        if index == 0:
            tangent = (Vector(sampled[1]) - center).normalized()
        elif index == len(sampled) - 1:
            tangent = (center - Vector(sampled[index - 1])).normalized()
        else:
            tangent = (Vector(sampled[index + 1]) - Vector(sampled[index - 1])).normalized()
        reference = Vector((0.0, 1.0, 0.0))
        if abs(tangent.dot(reference)) > 0.88:
            reference = Vector((0.0, 0.0, 1.0))
        lateral_axis = tangent.cross(reference).normalized()
        vertical_axis = lateral_axis.cross(tangent).normalized()
        if vertical_axis.y < 0.0:
            lateral_axis = -lateral_axis
            vertical_axis = -vertical_axis
        frames.append((center, tangent, vertical_axis, lateral_axis))

        t = index / max(len(sampled) - 1, 1)
        for side in range(sides):
            angle = math.tau * side / sides
            lobes = (1.0 + 0.075 * math.sin(5.0 * angle + 1.4 * t)
                     + 0.035 * math.cos(9.0 * angle - 2.2 * t)
                     + 0.018 * math.sin(15.0 * angle + t * 4.0))
            point = center + vertical_axis * (sampled_vertical[index] * math.sin(angle) * lobes)
            point += lateral_axis * (sampled_lateral[index] * math.cos(angle) * lobes)
            vertices.append(tuple(point))
            uv.append((arc[index] / ROOT_BARK_REPEAT_M, side / sides * 4.0))

    for index in range(len(sampled) - 1):
        for side in range(sides):
            a = index * sides + side
            b = index * sides + (side + 1) % sides
            c = (index + 1) * sides + (side + 1) % sides
            d = (index + 1) * sides + side
            faces.append((a, b, c, d))

    # Close only the buried root end. The exposed broken end remains open.
    root_center, root_tangent, root_up, root_side = frames[0]
    cap_index = len(vertices)
    vertices.append(tuple(root_center))
    uv.append((0.0, 0.0))
    for side in range(sides):
        faces.append((cap_index, (side + 1) % sides, side))
    bole = make_mesh_object(name, vertices, faces, uv, bark_material, parent)
    bole["semantic_role"] = "grounded fallen conifer bole; split end is authored separately"

    end_center, end_tangent, end_up, end_side = frames[-1]
    ry, rz = sampled_vertical[-1], sampled_lateral[-1]
    grain_vertices, grain_faces, grain_uv = [], [], []
    radial_steps = (0.24, 0.45, 0.69, 0.86, 1.0)
    grain_center_index = len(grain_vertices)
    grain_vertices.append(tuple(end_center))
    grain_uv.append((0.5, 0.5))
    for radial in radial_steps:
        for side in range(sides):
            angle = math.tau * side / sides
            fracture = (1.0 + 0.16 * math.sin(3.0 * angle + 0.45)
                        + 0.09 * math.sin(7.0 * angle - 0.8)
                        + 0.045 * math.cos(11.0 * angle + 0.2))
            r = radial * fracture
            point = end_center + end_up * (ry * math.sin(angle) * r)
            point += end_side * (rz * math.cos(angle) * r)
            grain_vertices.append(tuple(point))
            grain_uv.append((0.5 + 0.5 * r * math.cos(angle),
                             0.5 + 0.5 * r * math.sin(angle)))
    for side in range(sides):
        a = 1 + side
        b = 1 + (side + 1) % sides
        grain_faces.append((grain_center_index, a, b))
    for ring in range(len(radial_steps) - 1):
        lower = 1 + ring * sides
        upper = 1 + (ring + 1) * sides
        for side in range(sides):
            a = lower + side
            b = lower + (side + 1) % sides
            c = upper + (side + 1) % sides
            d = upper + side
            grain_faces.append((a, b, c, d))
    end_grain = make_mesh_object(name.replace("_LOD0", "_SplitEndGrain_LOD0"),
                                 grain_vertices, grain_faces, grain_uv,
                                 wood_material, parent)
    end_grain["semantic_role"] = "fresh exposed split wood at broken bole end"

    splinter_vertices, splinter_faces, splinter_uv = [], [], []
    splinter_specs = ((0.15, 0.29, 0.42), (1.95, 0.21, 0.62),
                      (3.55, 0.17, 0.48), (5.16, 0.14, 0.36))
    for angle, half_width, length in splinter_specs:
        across = end_up * math.cos(angle) - end_side * math.sin(angle)
        radial = end_up * math.sin(angle) + end_side * math.cos(angle)
        base = end_center + end_up * (ry * math.sin(angle) * 0.82)
        base += end_side * (rz * math.cos(angle) * 0.82)
        front = [
            base - across * half_width,
            base + across * half_width,
            base + across * (half_width * 0.12) + end_tangent * length + radial * 0.10,
        ]
        back = [point + radial * 0.055 for point in front]
        offset = len(splinter_vertices)
        splinter_vertices.extend(tuple(point) for point in (*front, *back))
        splinter_uv.extend(((0.0, 0.0), (0.0, 0.3), (length, 0.15),
                            (0.0, 0.0), (0.0, 0.3), (length, 0.15)))
        splinter_faces.extend(((offset, offset + 1, offset + 2),
                               (offset + 5, offset + 4, offset + 3),
                               (offset, offset + 3, offset + 4, offset + 1),
                               (offset + 1, offset + 4, offset + 5, offset + 2),
                               (offset + 2, offset + 5, offset + 3, offset)))
    splinters = make_mesh_object(name.replace("_LOD0", "_SplitFibers_LOD0"),
                                 splinter_vertices, splinter_faces, splinter_uv,
                                 wood_material, parent)
    splinters["semantic_role"] = "short irregular split fibers extending from the broken bole"
    return bole, end_grain, splinters


def make_floor(material, parent, slices=256, across=6, half_width=ROUTE_HALF_WIDTH_M):
    vertices, faces, uv = [], [], []
    z_values = [-8.0 + 16.0 * i / slices for i in range(slices + 1)]
    lengths = [0.0]
    centers = [route_center(z) for z in z_values]
    for a, b in zip(centers, centers[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    # Two top and bottom layers give the walk surface a visible, load-bearing edge.
    rows = slices + 1
    for layer in range(2):
        for index, (z, center) in enumerate(zip(z_values, centers)):
            before = centers[max(0, index - 1)]
            after = centers[min(slices, index + 1)]
            tangent = Vector((after.x - before.x, 0.0, after.z - before.z)).normalized()
            lateral = Vector((tangent.z, 0.0, -tangent.x)).normalized()
            surface_y = center.y
            y = surface_y if layer == 0 else surface_y - 0.46
            for cross in range(across + 1):
                u = cross / across
                offset = (u * 2.0 - 1.0) * half_width
                point = center + lateral * offset
                vertices.append((point.x, y, point.z))
                uv.append((lengths[index] / ROOT_BARK_REPEAT_M, offset / ROOT_BARK_REPEAT_M))
    stride = across + 1
    for layer in range(2):
        base = layer * rows * stride
        for index in range(slices):
            for cross in range(across):
                a = base + index * stride + cross
                b = a + 1
                c = base + (index + 1) * stride + cross + 1
                d = c - 1
                faces.append((a, b, c, d) if layer == 0 else (d, c, b, a))
    top_base, bottom_base = 0, rows * stride
    for index in range(slices):
        for side in (0, across):
            a = top_base + index * stride + side
            b = top_base + (index + 1) * stride + side
            c = bottom_base + (index + 1) * stride + side
            d = bottom_base + index * stride + side
            faces.append((a, b, c, d))
    for end in (0, slices):
        for cross in range(across):
            a = top_base + end * stride + cross
            b = a + 1
            c = bottom_base + end * stride + cross + 1
            d = c - 1
            faces.append((a, b, c, d) if end == 0 else (d, c, b, a))
    return make_mesh_object("HeroRoot_WalkRibbon_LOD0", vertices, faces, uv, material, parent)


def make_washout_bank(name, material, parent, slices=96):
    """A buried, uneven root bank that follows one side of the walkable bend."""
    vertices, faces, uv = [], [], []
    offsets = (1.40, 1.62, 1.90, 2.20, 2.52, 2.82)
    heights = (0.00, 0.05, 0.14, 0.29, 0.18, 0.00)
    z_values = [-8.0 + 16.0 * index / slices for index in range(slices + 1)]
    centers = [route_center(z) for z in z_values]
    lengths = [0.0]
    for before, after in zip(centers, centers[1:]):
        lengths.append(lengths[-1] + (after - before).length)

    columns = len(offsets)
    top_count = len(z_values) * columns
    for index, (z, center) in enumerate(zip(z_values, centers)):
        before = centers[max(0, index - 1)]
        after = centers[min(slices, index + 1)]
        tangent = Vector((after.x - before.x, 0.0, after.z - before.z)).normalized()
        lateral = Vector((tangent.z, 0.0, -tangent.x)).normalized()
        mouth_distance = min(z + 8.0, 8.0 - z)
        mouth_taper = smoothstep01(mouth_distance / 2.6)
        roughness = 0.92 + 0.08 * math.sin(z * 1.17 + 0.8) + 0.04 * math.sin(z * 2.31 + 1.6)
        lateral_wander = 0.10 * math.sin(z * 0.73 + 1.1) + 0.05 * math.sin(z * 2.05 + 0.4)
        for offset, height in zip(offsets, heights):
            point = center + lateral * -(offset + lateral_wander)
            vertices.append((point.x, center.y + height * mouth_taper * roughness, point.z))
            uv.append((lengths[index] / ROOT_BARK_REPEAT_M, offset / ROOT_BARK_REPEAT_M))

    for index, (z, center) in enumerate(zip(z_values, centers)):
        before = centers[max(0, index - 1)]
        after = centers[min(slices, index + 1)]
        tangent = Vector((after.x - before.x, 0.0, after.z - before.z)).normalized()
        lateral = Vector((tangent.z, 0.0, -tangent.x)).normalized()
        lateral_wander = 0.10 * math.sin(z * 0.73 + 1.1) + 0.05 * math.sin(z * 2.05 + 0.4)
        for offset in offsets:
            point = center + lateral * -(offset + lateral_wander)
            vertices.append((point.x, center.y - 0.36, point.z))
            uv.append((lengths[index] / ROOT_BARK_REPEAT_M, offset / ROOT_BARK_REPEAT_M))

    lower_base = top_count
    for index in range(slices):
        for cross in range(columns - 1):
            a = index * columns + cross
            b = a + 1
            c = (index + 1) * columns + cross + 1
            d = c - 1
            faces.append((a, b, c, d))
            faces.append((lower_base + d, lower_base + c, lower_base + b, lower_base + a))
        for cross in (0, columns - 1):
            a = index * columns + cross
            b = (index + 1) * columns + cross
            c = lower_base + (index + 1) * columns + cross
            d = lower_base + index * columns + cross
            faces.append((a, b, c, d))

    for cross in range(columns - 1):
        entry_a, entry_b = cross, cross + 1
        entry_c, entry_d = lower_base + cross + 1, lower_base + cross
        faces.append((entry_a, entry_b, entry_c, entry_d))
        exit_base = slices * columns
        exit_a, exit_b = exit_base + cross, exit_base + cross + 1
        exit_c, exit_d = lower_base + exit_base + cross + 1, lower_base + exit_base + cross
        faces.append((exit_d, exit_c, exit_b, exit_a))

    bank = make_mesh_object(name, vertices, faces, uv, material, parent)
    bank["semantic_role"] = "low exposed-root washout bank; its buried base and tapered ends leave both path mouths open"
    return bank


def make_anchor(root, name, godot_position, yaw_degrees=0.0):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = root
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.45
    obj.location = godot_to_blender(godot_position)
    obj.rotation_euler = (0.0, math.radians(yaw_degrees), 0.0)
    return obj


def tube_points(points, radii, samples_per_segment=14):
    """Smooth Catmull-Rom sampling for curved organic root branches."""
    vectors = [Vector(point) for point in points]
    output_points, output_radii = [], []
    for segment in range(len(vectors) - 1):
        p0 = vectors[max(0, segment - 1)]
        p1 = vectors[segment]
        p2 = vectors[segment + 1]
        p3 = vectors[min(len(vectors) - 1, segment + 2)]
        r0, r1, r2, r3 = [radii[max(0, min(len(radii) - 1, i))]
                          for i in (segment - 1, segment, segment + 1, segment + 2)]
        for step in range(samples_per_segment):
            t = step / samples_per_segment
            t2, t3 = t * t, t * t * t
            point = 0.5 * ((2 * p1) + (-p0 + p2) * t
                           + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                           + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
            radius = 0.5 * ((2 * r1) + (-r0 + r2) * t
                            + (2 * r0 - 5 * r1 + 4 * r2 - r3) * t2
                            + (-r0 + 3 * r1 - 3 * r2 + r3) * t3)
            output_points.append(tuple(point))
            output_radii.append(max(0.025, radius))
    output_points.append(tuple(vectors[-1]))
    output_radii.append(radii[-1])
    return output_points, output_radii


def add_root_tube(name, points, radii, material, root, sides=20, phase=0.0):
    points, radii = tube_points(points, radii, samples_per_segment=8)
    return make_tube(name, points, radii, material, root, sides=sides, phase=phase)


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_preview_rig(scene):
    sun_data = bpy.data.lights.new("Preview_W01_WinterSun", "SUN")
    sun_data.energy = 2.1
    sun = bpy.data.objects.new("Preview_W01_WinterSun", sun_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(28), math.radians(-15), math.radians(-24))
    return [sun]


def module_point_world(frame, local_point):
    """Map a source-local Godot point through the generated link frame."""
    yaw = math.radians(SPATIAL_LINK_WORLD_YAW_DEG)
    cosine, sine = math.cos(yaw), math.sin(yaw)
    frame_x, frame_y, frame_z = frame
    local_frame_x = SPATIAL_LINK_LOCAL_GODOT[0]
    root_x = frame_x - (local_frame_x * cosine)
    root_z = frame_z + (local_frame_x * sine)
    x, y, z = local_point
    return (root_x + x * cosine + z * sine,
            frame_y + y,
            root_z - x * sine + z * cosine)


def set_preview_root_frame(root, frame):
    yaw = math.radians(SPATIAL_LINK_WORLD_YAW_DEG)
    local_frame_x = SPATIAL_LINK_LOCAL_GODOT[0]
    frame_x, frame_y, frame_z = frame
    root.location = godot_to_blender((
        frame_x - local_frame_x * math.cos(yaw),
        frame_y,
        frame_z + local_frame_x * math.sin(yaw),
    ))
    root.rotation_euler = (0.0, 0.0, -yaw)


def append_preview_ground(scene, ground_blend: Path):
    with bpy.data.libraries.load(str(ground_blend), link=False) as (source, destination):
        if "W01_ForestGround_Terrain" not in source.objects:
            raise RuntimeError("Generated W01 terrain source is missing W01_ForestGround_Terrain")
        destination.objects = ["W01_ForestGround_Terrain"]
    terrain = destination.objects[0]
    if terrain is None or terrain.type != "MESH":
        raise RuntimeError("Could not append the generated W01 terrain mesh for grounded previews")
    terrain.name = "PreviewOnly_W01_ForestGround_Terrain"
    scene.collection.objects.link(terrain)
    terrain.hide_render = False
    return terrain


def setup_live_ground_preview_material(terrain, root_path: Path):
    """Mirror the existing W01 snow shader over the generated terrain masks."""
    texture_inputs = {
        "fresh": root_path / "game/assets/textures/painterly/snow_fresh_v1_albedo.png",
        "packed": root_path / "game/assets/textures/painterly/snow_trampled_v2_albedo.png",
        "roots": root_path / "game/assets/photoworlds/forest_ground/w01_forest_roots_1k.png",
        "leaves": root_path / "game/assets/photoworlds/forest_ground/w01_forest_leaves_1k.png",
    }
    for relative, path in texture_inputs.items():
        if not path.is_file():
            raise RuntimeError(f"Runtime W01 ground texture is missing: {path}")

    material = bpy.data.materials.new("PreviewOnly_W01_RuntimeSnowShader")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Roughness"].default_value = 0.94
    shader.inputs["Metallic"].default_value = 0.0
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = "MetricUV"
    mask = nodes.new("ShaderNodeAttribute")
    mask.attribute_name = "ForestMasks"
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    links.new(mask.outputs["Color"], separate.inputs["Color"])

    def texture(name, path):
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = "sRGB"
        image.pack()
        node = nodes.new("ShaderNodeTexImage")
        node.name = f"PreviewOnly_{name}"
        node.image = image
        node.extension = "REPEAT"
        links.new(uv.outputs["UV"], node.inputs["Vector"])
        return node.outputs["Color"]

    def math_node(operation, first, second=None, use_clamp=False):
        node = nodes.new("ShaderNodeMath")
        node.operation = operation
        node.use_clamp = use_clamp
        for source, socket in ((first, node.inputs[0]), (second, node.inputs[1])):
            if source is None:
                continue
            if isinstance(source, (float, int)):
                socket.default_value = float(source)
            else:
                links.new(source, socket)
        return node.outputs[0]

    def smooth_mask(source, minimum, maximum):
        node = nodes.new("ShaderNodeMapRange")
        node.interpolation_type = "SMOOTHSTEP"
        node.clamp = True
        node.inputs["From Min"].default_value = minimum
        node.inputs["From Max"].default_value = maximum
        node.inputs["To Min"].default_value = 0.0
        node.inputs["To Max"].default_value = 1.0
        links.new(source, node.inputs["Value"])
        return node.outputs["Result"]

    def multiply_color(color_output, tint):
        node = nodes.new("ShaderNodeMixRGB")
        node.blend_type = "MULTIPLY"
        node.inputs[0].default_value = 1.0
        node.inputs[2].default_value = (*tint, 1.0)
        links.new(color_output, node.inputs[1])
        return node.outputs[0]

    def mix_color(first, second, factor):
        node = nodes.new("ShaderNodeMixRGB")
        node.blend_type = "MIX"
        links.new(factor, node.inputs[0])
        links.new(first, node.inputs[1])
        links.new(second, node.inputs[2])
        return node.outputs[0]

    fresh = multiply_color(texture("FreshSnow", texture_inputs["fresh"]), (0.94, 0.97, 0.91))
    packed = multiply_color(texture("PackedSnow", texture_inputs["packed"]), (0.56, 0.65, 0.68))
    roots = multiply_color(texture("ForestRoots", texture_inputs["roots"]), (0.84, 0.83, 0.76))
    leaves = multiply_color(texture("ForestLeaves", texture_inputs["leaves"]), (0.90, 0.83, 0.69))
    track = smooth_mask(separate.outputs["Red"], 0.72, 1.0)
    shoulder = smooth_mask(separate.outputs["Red"], 0.04, 0.60)
    # Keep the narrow path blend comparable to the current shader weights.
    shoulder_factor = math_node("MULTIPLY", shoulder, 0.22)
    snow = mix_color(fresh, packed, shoulder_factor)
    track_factor = math_node("MULTIPLY", track, 0.94)
    snow = mix_color(snow, packed, track_factor)
    inverse_track = math_node("SUBTRACT", 1.0, track)
    root_factor = math_node("MINIMUM", math_node("MULTIPLY", separate.outputs["Blue"], 0.48), 0.38)
    root_factor = math_node("MULTIPLY", root_factor, inverse_track)
    snow = mix_color(snow, roots, root_factor)
    leaf_factor = math_node("MINIMUM", math_node("MULTIPLY", separate.outputs["Green"], 0.30), 0.24)
    leaf_factor = math_node("MULTIPLY", leaf_factor, inverse_track)
    snow = mix_color(snow, leaves, leaf_factor)
    links.new(snow, shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    terrain.data.materials.clear()
    terrain.data.materials.append(material)
    return material


def add_preview_pines(scene, root_path: Path, terrain):
    """Use the runtime winter-pine mesh at representative authored placements."""
    source_path = root_path / "game/assets/models/act1/urman_winter_pine_meshy_v1.glb"
    if not source_path.is_file():
        raise RuntimeError("The runtime winter-pine GLB is required for grounded root previews")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    imported = set(bpy.data.objects) - before
    template = next((obj for obj in imported if obj.type == "MESH" and obj.name == "WinterPine_LOD0"), None)
    if template is None:
        raise RuntimeError("Runtime winter-pine GLB has no WinterPine_LOD0 mesh")
    template_data = template.data
    template_materials = list(template.data.materials)
    for obj in imported:
        if obj.type == "MESH":
            obj.hide_render = True

    depsgraph = bpy.context.evaluated_depsgraph_get()
    placements = (
        # Existing W01 near/mid/corridor tree placements around F11→F12.
        (-4.0, 39.0, 229.0, 14.0), (-7.0, 48.0, 127.0, 13.0),
        (20.7, 41.3, 34.0, 12.5), (27.3, 53.1, 211.0, 13.5),
        (23.0, 50.0, 97.0, 11.5), (16.0, 26.0, 241.0, 12.0),
        (47.0, 72.0, 36.0, 14.0), (7.0, 74.0, 286.0, 13.0),
        (20.0, 88.0, 54.0, 13.5), (38.0, 67.6, 128.0, 12.5),
        # Existing W01 placements around F13→F14.
        (69.0, 138.0, 112.0, 14.0), (60.0, 149.0, 72.0, 13.0),
        (64.8, 137.6, 143.0, 12.5), (84.0, 129.0, 22.0, 11.5),
        (59.3, 96.3, 194.0, 12.0), (63.0, 89.0, 221.0, 11.5),
    )
    preview_trees = []
    for index, (x, z, yaw_degrees, height) in enumerate(placements):
        origin = Vector((x, -z, 30.0))
        hit, location, normal, face, collider, matrix = scene.ray_cast(
            depsgraph, origin, Vector((0.0, 0.0, -1.0)), distance=80.0)
        if not hit or collider != terrain:
            raise RuntimeError(f"Could not sample the real generated ground below preview pine ({x}, {z})")
        tree = bpy.data.objects.new(f"PreviewOnly_WinterPine_{index + 1:02d}", template_data)
        scene.collection.objects.link(tree)
        tree.data.materials.clear()
        for material in template_materials:
            tree.data.materials.append(material)
        tree.location = (x, -z, location.z)
        tree.rotation_euler[2] = -math.radians(yaw_degrees)
        girth = 0.84
        tree.scale = (height * girth, height * girth, height)
        tree.hide_render = False
        preview_trees.append(tree)
    return preview_trees + list(imported)


def render_grounded_preview_suite(scene, root, root_path: Path, output_dir: Path, ground_manifest):
    source_dir = root_path / "assets/source/blender/photoworlds/W01"
    terrain = append_preview_ground(scene, source_dir / "prologue-ground-f00-f14-v1.blend")
    setup_live_ground_preview_material(terrain, root_path)
    preview_objects = [terrain]
    preview_objects.extend(add_preview_pines(scene, root_path, terrain))
    preview_objects.extend(add_preview_rig(scene))

    def camera(name, position, target, lens):
        data = bpy.data.cameras.new(name)
        data.lens = lens
        data.sensor_width = 36
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = godot_to_blender(position)
        look_at(obj, godot_to_blender(target))
        preview_objects.append(obj)
        return obj

    f11 = ground_manifest["authored_nodes"]["F11"]
    f12 = ground_manifest["authored_nodes"]["F12"]
    f13 = ground_manifest["authored_nodes"]["F13"]
    f11_x, _, f11_z = f11["xyz_m"]
    f11_y = float(f11["terrain_vertex_y_m"])
    frame12 = (float(f12["xyz_m"][0]), float(f12["terrain_vertex_y_m"]), float(f12["xyz_m"][2]))
    frame13 = (float(f13["xyz_m"][0]), float(f13["terrain_vertex_y_m"]), float(f13["xyz_m"][2]))

    for index, frame in enumerate((frame12, frame13)):
        fill_data = bpy.data.lights.new(f"Preview_RootBoleFill_{index + 1}", "AREA")
        fill_data.energy = 1500
        fill_data.shape = "DISK"
        fill_data.size = 11
        fill = bpy.data.objects.new(fill_data.name, fill_data)
        scene.collection.objects.link(fill)
        fill.location = godot_to_blender(module_point_world(frame, (0.0, 7.0, -7.0)))
        look_at(fill, godot_to_blender(module_point_world(frame, (1.5, 0.6, 0.0))))
        preview_objects.append(fill)

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.eevee.taa_render_samples = 64

    # The first camera is the exact F11 world marker at normal first-person eye height.
    set_preview_root_frame(root, frame12)
    scene.camera = camera(
        "Preview_F11_Approach_Camera",
        (f11_x, f11_y + 1.62, f11_z),
        module_point_world(frame12, (0.2, 1.15, -0.2)),
        26,
    )
    render_jobs = [("hero-root-turn-v1-f11-approach.png", scene.camera, frame12)]

    entry_local = route_center(-8.0)
    entry_camera_position = (entry_local.x, entry_local.y + 1.62, entry_local.z)
    entry_look = route_center(-3.0)
    scene.camera = camera(
        "Preview_WalkEntry_Ground_Camera",
        module_point_world(frame12, entry_camera_position),
        module_point_world(frame12, (entry_look.x, entry_look.y + 1.20, entry_look.z)),
        32,
    )
    render_jobs.append(("hero-root-turn-v1-entry-ground.png", scene.camera, frame12))

    # Show the same source geometry seated in the separate F13 ground host.
    set_preview_root_frame(root, frame13)
    exit_camera_position = (0.0, route_center(8.0).y + 1.62, 10.0)
    exit_look = route_center(4.0)
    scene.camera = camera(
        "Preview_F13_Exit_Ground_Camera",
        module_point_world(frame13, exit_camera_position),
        module_point_world(frame13, (exit_look.x, exit_look.y + 1.15, exit_look.z)),
        32,
    )
    render_jobs.append(("hero-root-turn-v1-f13-exit-ground.png", scene.camera, frame13))

    for name, view_camera, view_frame in render_jobs:
        set_preview_root_frame(root, view_frame)
        scene.camera = view_camera
        scene.render.filepath = str(output_dir / name)
        bpy.ops.render.render(write_still=True)
    return preview_objects, [name for name, _, _ in render_jobs]


def orient_normals(obj):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def build_asset(root_path: Path, output_dir: Path):
    mask_path = output_dir / f"{ASSET_NAME}-mask-2k.png"
    write_png_rgb(mask_path, BAKE_SIZE, BAKE_SIZE, make_mask_pixel())
    material, bark_path, bark_receipt_path, bark_receipt, albedo_path = bark_material(root_path, mask_path, output_dir)
    wood_material = textured_material(root_path, "HeroRoot_SplitWood_ProjectTexture", WOOD_GRAIN_TEXTURE, 0.9)
    snow_material = textured_material(root_path, "HeroRoot_FreshSnow_ProjectTexture", FRESH_SNOW_TEXTURE, 0.94)
    packed_snow_material = textured_material(root_path, "HeroRoot_TrampledSnow_ProjectTexture", TRAMPLED_SNOW_TEXTURE, 0.94)
    root = bpy.data.objects.new("A_HERO_ROOT_Turn", None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.6

    # The solid route remains exactly on its F12/F13 link contract. Snow uses
    # the same project texture family as the live W01 terrain instead of a dark
    # bark slab that reads as a floating test platform.
    floor = make_floor(packed_snow_material, root, slices=180, across=3)
    floor["semantic_role"] = "solid trampled-snow walk ribbon; unchanged 2.2 m curved route"

    # A low root crown sits east of the route, buried into the local ground.
    # The horizontal bole now blocks the straight sightline while leaving the
    # existing westward bend open for a player-sized passage.
    buttress = make_irregular_ellipsoid(
        "HeroRoot_CentralButtress_LOD0", (3.15, 0.43, 0.05), (2.38, 0.64, 2.48),
        material, root, sectors=56, rings=28, phase=0.56)
    buttress["semantic_role"] = "low buried root crown beneath the fallen bole; no upright tower"

    central_plate = make_irregular_ellipsoid(
        "HeroRoot_CentralRootPlate_LOD0", (4.45, 0.98, -0.08), (0.68, 1.18, 1.68),
        material, root, sectors=44, rings=24, phase=1.1)
    central_plate["semantic_role"] = "uprisen root plate at the grounded trunk base"

    main_trunk, main_end_grain, main_splinters = make_fallen_bole(
        "HeroRoot_Trunk_Center_LOD0",
        [(4.72, 1.30, -0.16, 1.08, 1.00),
         (3.20, 1.23, -0.13, 1.06, 0.98),
         (1.62, 1.15, -0.02, 0.98, 0.88),
         (0.12, 1.05, 0.17, 0.88, 0.78),
         (-1.14, 0.93, 0.26, 0.76, 0.66)],
        material, wood_material, root, sides=48)
    main_trunk["semantic_role"] = "fallen old conifer bole laid into the root plate; fractured toward the bend"

    # Two low, broken bole sections mark the ground on either end of the link.
    # Their tops stay below eye height and their nearest edges remain east of
    # the 2.2 m walk ribbon.
    entry_ground = route_center(-8.0).y
    exit_ground = route_center(8.0).y
    left_trunk, left_grain, left_fibers = make_fallen_bole(
        "HeroRoot_Trunk_Left_LOD0",
        [(5.00, entry_ground + 0.58, -8.22, 0.70, 0.66),
         (3.95, entry_ground + 0.52, -8.06, 0.66, 0.61),
         (2.78, entry_ground + 0.35, -7.89, 0.49, 0.45)],
        material, wood_material, root, sides=36)
    left_trunk["semantic_role"] = "low snapped bole laid beside the F12 entry; buried base meets the field"
    right_trunk, right_grain, right_fibers = make_fallen_bole(
        "HeroRoot_Trunk_Right_LOD0",
        [(5.05, exit_ground + 0.58, 8.20, 0.72, 0.68),
         (3.98, exit_ground + 0.51, 8.03, 0.65, 0.60),
         (2.82, exit_ground + 0.34, 7.82, 0.48, 0.44)],
        material, wood_material, root, sides=36)
    right_trunk["semantic_role"] = "low snapped bole laid beside the F13 exit; buried base meets the field"

    # Entry, central, and exit root plates extend below their sampled route
    # levels so no part floats when the same module is aligned to F12 or F13.
    entry_plate = make_irregular_ellipsoid(
        "HeroRoot_EntryButtress_LOD0", (4.65, entry_ground + 0.30, -8.08),
        (1.48, 0.72, 1.62), material, root, sectors=40, rings=20, phase=1.45)
    exit_plate = make_irregular_ellipsoid(
        "HeroRoot_ExitButtress_LOD0", (4.62, exit_ground + 0.30, 8.08),
        (1.48, 0.72, 1.62), material, root, sectors=40, rings=20, phase=2.05)
    entry_plate["semantic_role"] = "buried root mass at the lower F12 ground seam"
    exit_plate["semantic_role"] = "buried root mass at the raised F13 ground seam"

    root_specs = [
        ("HeroRoot_Buttress_Root_East", [(3.55, 0.58, 0.0), (4.52, 0.31, -0.48), (5.62, 0.08, -1.20), (6.28, -0.08, -1.58)], [0.88, 0.68, 0.34, 0.035]),
        ("HeroRoot_Buttress_Root_South", [(3.62, 0.52, 0.20), (4.28, 0.28, 1.05), (5.18, 0.06, 1.86), (5.82, -0.08, 2.20)], [0.78, 0.58, 0.30, 0.035]),
        ("HeroRoot_Buttress_Root_North", [(4.26, 0.47, -0.34), (3.20, 0.26, -1.02), (2.18, 0.07, -1.70), (1.52, -0.08, -2.02)], [0.76, 0.56, 0.28, 0.035]),
        ("HeroRoot_Entry_Root_Spur", [(4.72, entry_ground + 0.32, -8.1), (5.52, entry_ground + 0.13, -8.88), (6.32, entry_ground - 0.06, -9.48)], [0.62, 0.34, 0.035]),
        ("HeroRoot_Exit_Root_Spur", [(4.74, exit_ground + 0.32, 8.1), (5.56, exit_ground + 0.13, 8.88), (6.35, exit_ground - 0.06, 9.48)], [0.64, 0.35, 0.035]),
    ]
    for index, (name, points, radii) in enumerate(root_specs):
        add_root_tube(name, points, radii, material, root, sides=18, phase=0.6 + index * 0.47)

    # Snow settles against the root plates and the outside shoulder. These
    # compact, buried ellipsoids sit clear of the player's swept path.
    snow_mounds = [
        make_irregular_ellipsoid("HeroRoot_SnowMound_CentralEast_LOD0",
            (2.42, -0.11, 1.78), (1.82, 0.43, 1.08), snow_material, root, sectors=36, rings=18, phase=0.3),
        make_irregular_ellipsoid("HeroRoot_SnowMound_CentralNorth_LOD0",
            (4.65, -0.08, -1.64), (1.46, 0.38, 1.08), snow_material, root, sectors=36, rings=18, phase=1.2),
        make_irregular_ellipsoid("HeroRoot_SnowCap_TrunkCenter_A_LOD0",
            (1.62, 2.02, -0.02), (0.98, 0.18, 0.74), snow_material, root, sectors=32, rings=14, phase=0.9),
        make_irregular_ellipsoid("HeroRoot_SnowCap_TrunkCenter_B_LOD0",
            (3.72, 2.20, -0.13), (0.78, 0.15, 0.62), snow_material, root, sectors=32, rings=14, phase=2.4),
        make_irregular_ellipsoid("HeroRoot_SnowMound_Entry_LOD0",
            (4.64, entry_ground - 0.06, -8.22), (1.62, 0.40, 1.32), snow_material, root, sectors=32, rings=16, phase=2.1),
        make_irregular_ellipsoid("HeroRoot_SnowMound_Exit_LOD0",
            (4.66, exit_ground - 0.06, 8.20), (1.62, 0.40, 1.32), snow_material, root, sectors=32, rings=16, phase=2.8),
    ]
    for mound in snow_mounds:
        mound["semantic_role"] = "snow packed onto the exposed bole and buried root shoulders"

    # Snow follows the curved bank and lane without changing either route.
    washout_bank = make_washout_bank("HeroRoot_WashoutBank_West_LOD0", snow_material, root)
    washout_bank["semantic_role"] = "low snow-covered outside bank following the preserved bend"

    make_anchor(root, "WalkEntry", WALK_ENTRY_LOCAL_GODOT, 0.0)
    make_anchor(root, "WalkExit", WALK_EXIT_LOCAL_GODOT, 0.0)
    make_anchor(root, "SpatialLinkFrame", SPATIAL_LINK_LOCAL_GODOT, 0.0)

    for obj in [floor, buttress, central_plate, main_trunk, left_trunk, right_trunk,
                entry_plate, exit_plate, washout_bank, *snow_mounds]:
        orient_normals(obj)
    # Keep only root and its geometry selected for export; preview-only nodes
    # are created later and remain unselected.
    return root, material, bark_path, bark_receipt_path, bark_receipt, mask_path, albedo_path


def ray_from_godot(scene, origin, direction, max_distance=100.0):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    blender_origin = Vector(godot_to_blender(origin))
    blender_direction = Vector(godot_to_blender(direction)).normalized()
    return scene.ray_cast(depsgraph, blender_origin, blender_direction, distance=max_distance)


def verify_geometry(scene, root):
    # Eye-height chord between the two walk ends must be occluded by authored geometry.
    start = Vector((WALK_ENTRY_LOCAL_GODOT[0], WALK_ENTRY_LOCAL_GODOT[1] + 1.62, WALK_ENTRY_LOCAL_GODOT[2]))
    end = Vector((WALK_EXIT_LOCAL_GODOT[0], WALK_EXIT_LOCAL_GODOT[1] + 1.62, WALK_EXIT_LOCAL_GODOT[2]))
    chord = end - start
    hit, location, normal, face, obj, matrix = ray_from_godot(scene, tuple(start), tuple(chord), chord.length)
    if not hit or obj.name not in {"HeroRoot_CentralButtress_LOD0", "HeroRoot_Trunk_Center_LOD0"}:
        raise RuntimeError("The root passage does not occlude the straight player eye-height chord")

    min_clearance = math.inf
    min_side_clearance = math.inf
    min_sampled_headroom = 4.16
    for index in range(321):
        z = -8.0 + 16.0 * index / 320.0
        center = route_center(z)
        radius = route_curvature_radius(z)
        min_clearance = min(min_clearance, radius)
        prev, nxt = route_center(max(-8.0, z - 0.01)), route_center(min(8.0, z + 0.01))
        tangent = Vector((nxt.x - prev.x, 0.0, nxt.z - prev.z)).normalized()
        lateral = Vector((tangent.z, 0.0, -tangent.x)).normalized()
        for side in (-1.0, 1.0):
            start_point = center + Vector((0.0, 0.30, 0.0))
            ray_direction = lateral * side
            hit, loc, norm, face, obj, matrix = ray_from_godot(scene, tuple(start_point), tuple(ray_direction), 8.0)
            if hit and obj.name != "HeroRoot_WalkRibbon_LOD0":
                hit_godot = Vector(blender_to_godot(loc))
                distance = (hit_godot - start_point).length
                min_side_clearance = min(min_side_clearance, distance)
        # Check the whole capsule-height sample column at five across-lane points.
        for lateral_offset in (-1.0, -0.5, 0.0, 0.5, 1.0):
            sample = center + lateral * lateral_offset
            origin = Vector((sample.x, center.y + 0.16, sample.z))
            hit, loc, norm, face, obj, matrix = ray_from_godot(scene, tuple(origin), (0.0, 1.0, 0.0), 4.0)
            if hit and obj.name != "HeroRoot_WalkRibbon_LOD0":
                headroom = blender_to_godot(loc)[1] - center.y
                min_sampled_headroom = min(min_sampled_headroom, headroom)
                if headroom < ROUTE_HEADROOM_M:
                    raise RuntimeError(f"Overhead passage clearance is only {headroom:.3f} m at z={z:.3f}")
    if min_clearance < 1.8:
        raise RuntimeError(f"Root passage turn radius is only {min_clearance:.3f} m")
    if min_side_clearance < ROUTE_HALF_WIDTH_M:
        raise RuntimeError(f"Root passage side clearance is only {min_side_clearance:.3f} m")
    return {
        "straight_eye_height_chord_occluded": True,
        "eye_height_m": 1.62,
        "minimum_route_turn_radius_m": min_clearance,
        "clear_walkway_width_m": ROUTE_HALF_WIDTH_M * 2.0,
        "minimum_sampled_side_clearance_m": min_side_clearance,
        "minimum_sampled_headroom_m": min_sampled_headroom,
        "headroom_requirement_m": ROUTE_HEADROOM_M,
        "headroom_sample_method": "raycast up from five lateral stations at 0.16 m above ribbon, 321 longitudinal stations; recorded ground-to-first-obstruction height (4.16 m lower bound when no hit within 4 m)",
        "walkable_floor": "HeroRoot_WalkRibbon_LOD0, solid mesh with closed sides and underside",
    }


def object_mesh_metrics(root):
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    return {
        "objects": {obj.name: {"vertices": len(obj.data.vertices), "triangles": sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons)} for obj in meshes},
        "vertex_count": sum(len(obj.data.vertices) for obj in meshes),
        "triangle_count": sum(sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons) for obj in meshes),
        "godot_bounds_m": {
            "x": [min(blender_to_godot(v.co)[0] for obj in meshes for v in obj.data.vertices), max(blender_to_godot(v.co)[0] for obj in meshes for v in obj.data.vertices)],
            "y": [min(blender_to_godot(v.co)[1] for obj in meshes for v in obj.data.vertices), max(blender_to_godot(v.co)[1] for obj in meshes for v in obj.data.vertices)],
            "z": [min(-v.co.y for obj in meshes for v in obj.data.vertices), max(-v.co.y for obj in meshes for v in obj.data.vertices)],
        },
    }


def manifest(root_path, output_dir, root, safety, bark_path, bark_receipt_path, bark_entry, mask_path, albedo_path):
    spec_path = root_path / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/data/assets.spec.json"
    level_path = root_path / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/levels/W01_Лес_за_спиной_RU.md"
    metrics = object_mesh_metrics(root)
    if not (15000 <= metrics["triangle_count"] <= 35000):
        raise RuntimeError(f"A-HERO-ROOT must contain 15k–35k LOD0 triangles, got {metrics['triangle_count']}")
    if metrics["godot_bounds_m"]["y"][1] < 2.0:
        raise RuntimeError("The fallen root does not rise far enough to obscure the passage chord")
    z_samples = [(-8.0 + i * 16.0 / 320.0) for i in range(321)]
    curvature = [route_curvature_radius(z) for z in z_samples]
    waypoints = [list(point) for point in ROUTE_WAYPOINTS_GODOT]
    mask_output = {
        "file": mask_path.name,
        "sha256": sha256(mask_path),
        "size_px": [BAKE_SIZE, BAKE_SIZE],
        "channels": {"R": "bark relief", "G": "asset-specific lichen blend", "B": "worn root grain"},
        "provenance": "project-original deterministic tileable multiscale value-noise mask; no third-party source",
    }
    albedo_output = {
        "file": albedo_path.name,
        "sha256": sha256(albedo_path),
        "size_px": [BAKE_SIZE, BAKE_SIZE],
        "provenance": "GLTF-compatible linear-space blend of project-original bark albedo and the authored mask Green channel; packed into the Blender source",
        "runtime_embedded": True,
    }
    return {
        "asset_id": ASSET_ID,
        "asset": ASSET_NAME,
        "consumer": "W01 F12↔F13 spatial passage; paired copies share one local SpatialLinkFrame",
        "stage_only": True,
        "scene_ready": False,
        "native_or_godot_import_verified": False,
        "collision_surface_generated_or_verified": False,
        "source_script": str(Path(__file__).resolve()),
        "source_script_sha256": sha256(Path(__file__).resolve()),
        "input_specs": {
            "asset_spec": str(spec_path.relative_to(root_path)),
            "asset_spec_sha256": sha256(spec_path),
            "level_passport": str(level_path.relative_to(root_path)),
            "level_passport_sha256": sha256(level_path),
            "bark_texture": str(bark_path.relative_to(root_path)),
            "bark_texture_sha256": sha256(bark_path),
            "bark_receipt": str(bark_receipt_path.relative_to(root_path)),
            "bark_receipt_sha256": sha256(bark_receipt_path),
            "bark_receipt_entry": bark_entry,
            "ground_manifest": GROUND_MANIFEST_INPUT,
            "ground_manifest_sha256": GROUND_MANIFEST_SHA256,
            "ground_source_sha256": GROUND_SOURCE_SHA256,
            "wood_grain_texture": WOOD_GRAIN_TEXTURE,
            "wood_grain_texture_sha256": sha256(root_path / WOOD_GRAIN_TEXTURE),
            "fresh_snow_texture": FRESH_SNOW_TEXTURE,
            "fresh_snow_texture_sha256": sha256(root_path / FRESH_SNOW_TEXTURE),
            "trampled_snow_texture": TRAMPLED_SNOW_TEXTURE,
            "trampled_snow_texture_sha256": sha256(root_path / TRAMPLED_SNOW_TEXTURE),
        },
        "coordinate_contract": {
            "source_local": "Godot X/Y/Z in metres; Blender source maps (X,-Z,Y)",
            "root_node": "A_HERO_ROOT_Turn",
            "imported_scale": 1.0,
            "godot_forward": "+Z",
            "walk_surface_y_m": "sampled from generated ground endpoint heights; zero at SpatialLinkFrame",
            "world_frame_yaw_default_deg": SPATIAL_LINK_WORLD_YAW_DEG,
            "world_frame_yaw_basis": "mean of F11→F12 and F13→F14 horizontal passport tangents",
        },
        "mesh": {
            "file_blend": f"{ASSET_NAME}.blend",
            "file_glb": f"{ASSET_NAME}.glb",
            "root_node": "A_HERO_ROOT_Turn",
            "vertex_count": metrics["vertex_count"],
            "triangle_count": metrics["triangle_count"],
            "mesh_objects": metrics["objects"],
            "godot_bounds_m": metrics["godot_bounds_m"],
            "unique_mask": mask_output,
            "baked_albedo": albedo_output,
            "reused_bark": {
                "file": str(bark_path.relative_to(root_path)),
                "sha256": sha256(bark_path),
                "repeat_m": ROOT_BARK_REPEAT_M,
                "source": str(bark_receipt_path.relative_to(root_path)) + "#textures.F03",
                "provenance": bark_entry["licence"],
            },
        },
        "spatial_link": {
            "frame_node": "SpatialLinkFrame",
            "frame_local_godot_xyz_m": list(SPATIAL_LINK_LOCAL_GODOT),
            "frame_local_yaw_deg": 0.0,
            "player_entry_node": "WalkEntry",
            "player_entry_local_godot_xyz_m": list(WALK_ENTRY_LOCAL_GODOT),
            "player_exit_node": "WalkExit",
            "player_exit_local_godot_xyz_m": list(WALK_EXIT_LOCAL_GODOT),
            "route_waypoints_godot_local_m": waypoints,
            "centerline_function": "x=-5*sin(pi*(z+8)/16)^2; y smoothly joins generated WalkEntry/WalkExit ground deltas to y=0 at SpatialLinkFrame; z in [-8,+8]",
            "minimum_sampled_turn_radius_m": min(curvature),
            "route_half_width_m": ROUTE_HALF_WIDTH_M,
            "route_headroom_m": ROUTE_HEADROOM_M,
            "pairing": "rootTransform = worldSpatialLinkFrame * inverse(local SpatialLinkFrame); apply reciprocal transform at the same local plane; retain local relative yaw and horizontal velocity",
            "walk_endpoint_world_positions_from_ground_manifest": {
                "WalkEntry": list(GROUND_ENTRY_WORLD_GODOT),
                "WalkExit": list(GROUND_EXIT_WORLD_GODOT),
            },
            "ground_manifest_input": GROUND_MANIFEST_INPUT,
            "ground_manifest_sha256": GROUND_MANIFEST_SHA256,
        },
        "geometry_safety": safety,
        "coverage_scope": "one opaque, physically walkable curved root passage module; not a complete W01 forest or encounter",
        "offline_previews": [
            "hero-root-turn-v1-f11-approach.png",
            "hero-root-turn-v1-entry-ground.png",
            "hero-root-turn-v1-f13-exit-ground.png",
        ],
        "verification_scope": {
            "source_generated": True,
            "node_names_checked_in_blender_scene": True,
            "mesh_metrics_checked": True,
            "sightline_and_clearance_sampled": True,
            "blender_rendered_f11_entry_and_f13_views_with_generated_terrain_and_runtime_pines": True,
            "godot_import": "not run",
            "in_game_frame_mapping_and_physics": "not run",
            "art_author_review": "pending",
        },
    }


def main():
    args = parse_args()
    output_dir = args.output_dir.expanduser().resolve()
    if output_dir.exists():
        raise RuntimeError(f"Refusing to overwrite existing staging directory: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)
    root_path = project_root()
    ground_manifest, ground_contract = load_ground_contract(args.ground_manifest)
    scene = configure_scene()
    root, material, bark_path, bark_receipt_path, bark_entry, mask_path, albedo_path = build_asset(root_path, output_dir)
    safety = verify_geometry(scene, root)
    preview_objects, preview_names = render_grounded_preview_suite(
        scene, root, root_path, output_dir, ground_manifest)

    # Preview-only terrain, trees, cameras, and lights are not authored into
    # the reusable module; the source blend remains a clean local asset.
    scene.camera = None
    for preview_object in preview_objects:
        if bpy.data.objects.get(preview_object.name) is not None:
            bpy.data.objects.remove(preview_object, do_unlink=True)
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = (0.0, 0.0, 0.0)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)

    blend_path = output_dir / f"{ASSET_NAME}.blend"
    glb_path = output_dir / f"{ASSET_NAME}.glb"
    manifest_path = output_dir / f"{ASSET_NAME}.manifest.json"
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    scene.camera = None
    scene.render.filepath = "//"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=True, export_texcoords=True,
        export_normals=True, export_tangents=True, export_materials="EXPORT",
        export_cameras=False, export_lights=False,
    )
    if not all(path.is_file() for path in (blend_path, glb_path, mask_path, albedo_path)):
        raise RuntimeError("Hero-root source export missed a required binary output")
    document = manifest(root_path, output_dir, root, safety, bark_path,
                        bark_receipt_path, bark_entry, mask_path, albedo_path)
    document["mesh"].update({
        "file_blend_sha256": sha256(blend_path),
        "file_glb_sha256": sha256(glb_path),
        "file_blend_bytes": blend_path.stat().st_size,
        "file_glb_bytes": glb_path.stat().st_size,
    })
    if preview_names != document["offline_previews"]:
        raise RuntimeError("Grounded preview outputs do not match the manifest contract")
    document["offline_preview_receipts"] = {
        name: {"sha256": sha256(output_dir / name), "bytes": (output_dir / name).stat().st_size}
        for name in document["offline_previews"]
    }
    manifest_path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "output_dir": str(output_dir),
        "blend": str(blend_path),
        "glb": str(glb_path),
        "manifest": str(manifest_path),
        "mask": str(mask_path),
        "safety": safety,
        "mesh": document["mesh"],
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
