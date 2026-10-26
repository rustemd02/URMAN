"""Build one shared-PBR two-LOD candidate from the approved Meshy remeshes."""

from __future__ import annotations

import hashlib
import json
import math
import struct
from array import array
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[6]
ASSET_DIR = ROOT / "assets/source/blender/photoworlds/W01/photographed_people_v1"
RAW_INPUT = ASSET_DIR / "bluecoat-human-meshy-raw-v1.glb"
NEAR_INPUT = ASSET_DIR / "bluecoat-human-meshy-near-v1.glb"
MID_INPUT = ASSET_DIR / "bluecoat-human-meshy-mid-v1.glb"
OUTPUT_GLB = ROOT / "game/assets/photoworlds/forest_entry/pforest-human-trace-meshy-v1.glb"
OUTPUT_MANIFEST = ASSET_DIR / "pforest-human-trace-meshy-v1.manifest.json"
LOCAL_ARCHIVE_NAME = "URMAN-W01-bluecoat-meshy-2026-10-26.zip"
TARGET_HEIGHT_M = 1.78
ATLAS_WIDTH = 2048
ATLAS_HEIGHT = 1024
TILE_SIZE = 1020
TILE_MARGIN = 2
PREVIEW_SIZE = 768
LOD_TASKS = {
    "HumanTrace_LOD0": {
        "task_id": "01a1778c-1405-740a-8b75-875a92399427",
        "status": "SUCCEEDED",
        "target_faces": 25000,
        "actual_consumed_credits": 5,
    },
    "HumanTrace_LOD1": {
        "task_id": "01a1778c-4d77-72d4-b5c8-e64ae8f5d871",
        "status": "SUCCEEDED",
        "target_faces": 8000,
        "actual_consumed_credits": 5,
    },
}
BASE_TASK_ID = "01a17788-e73a-75b6-aa85-8bed6809626e"
BASE_TASK_CREDITS = 30
IMAGE_INPUT = ASSET_DIR / "photographed_bluecoat_single_reference_v1.png"
TRIO_REFERENCE = ASSET_DIR / "photographed_people_reference_v1.png"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def import_single_mesh(path: Path) -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [obj for obj in bpy.data.objects if obj not in before and obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one mesh in {path.name}, found {len(meshes)}")
    return meshes[0]


def apply_world_to_mesh(obj: bpy.types.Object) -> None:
    matrix = obj.matrix_world.copy()
    copied = obj.data.copy()
    copied.transform(matrix)
    obj.data = copied
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


def bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    coords = [vertex.co for vertex in obj.data.vertices]
    if not coords:
        raise RuntimeError(f"{obj.name} has no vertices")
    low = Vector(tuple(min(point[axis] for point in coords) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in coords) for axis in range(3)))
    return low, high


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def uv_facts(mesh: bpy.types.Mesh) -> dict:
    if not mesh.uv_layers:
        raise RuntimeError(f"{mesh.name} has no UV map")
    uv = mesh.uv_layers.active
    values = [loop.uv[:] for loop in uv.data]
    if not values or not all(math.isfinite(component) for pair in values for component in pair):
        raise RuntimeError(f"{mesh.name} has empty or non-finite UV coordinates")
    return {
        "active_uv_name": uv.name,
        "loop_count": len(values),
        "min": [min(pair[i] for pair in values) for i in range(2)],
        "max": [max(pair[i] for pair in values) for i in range(2)],
    }


def material_image_nodes(material: bpy.types.Material) -> dict[str, bpy.types.Node]:
    result = {}
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        targets = []
        for link in node.outputs["Color"].links:
            targets.append((link.to_node.type, link.to_socket.name))
        if ("BSDF_PRINCIPLED", "Base Color") in targets:
            role = "base_color"
        elif any(node_type == "NORMAL_MAP" for node_type, _ in targets):
            role = "normal"
        elif any(node_type in ("SEPARATE_COLOR", "SEPRGB") for node_type, _ in targets):
            role = "metallic_roughness"
        else:
            continue
        if role in result:
            raise RuntimeError(f"{material.name} has multiple image nodes for PBR role {role}")
        result[role] = node
    if set(result) != {"base_color", "normal", "metallic_roughness"}:
        raise RuntimeError(f"{material.name} does not expose one Base Color, Normal and MetallicRoughness image")
    return result


def build_shared_pbr_atlas(near_material: bpy.types.Material,
                           mid_material: bpy.types.Material,
                           source_png_facts: dict) -> tuple[dict, dict, dict]:
    near_nodes = material_image_nodes(near_material)
    mid_nodes = material_image_nodes(mid_material)
    atlas_images = {}
    facts = {}
    defaults = {
        "base_color": (0.0, 0.0, 0.0, 0.0),
        "normal": (0.5, 0.5, 1.0, 1.0),
        "metallic_roughness": (1.0, 1.0, 0.0, 1.0),
    }
    source_image_names = {
        "base_color": "texture_0",
        "normal": "normal",
        "metallic_roughness": "texture_0_metallic_roughness",
    }
    tile_xs = {"near": TILE_MARGIN, "mid": ATLAS_WIDTH // 2 + TILE_MARGIN}
    tile_y = TILE_MARGIN
    for role in ("base_color", "normal", "metallic_roughness"):
        near_image = near_nodes[role].image
        mid_image = mid_nodes[role].image
        if near_image.colorspace_settings.name != mid_image.colorspace_settings.name:
            raise RuntimeError(f"Near/mid {role} image colorspaces differ")
        if int(near_image.channels) != int(mid_image.channels):
            raise RuntimeError(f"Near/mid {role} image channel counts differ")
        if list(near_image.size) != [4096, 4096] or list(mid_image.size) != [4096, 4096]:
            raise RuntimeError(f"Expected 4K square maps for {role}")

        source_facts = []
        source_images = (("near", near_image), ("mid", mid_image))
        for label, image in source_images:
            png_facts = source_png_facts[label][source_image_names[role]]
            source_facts.append({
                "lod": label,
                "image": image.name,
                "source_size_px": list(image.size),
                "source_png": png_facts,
                "blender_image_buffer_channels": int(image.channels),
                "colorspace": image.colorspace_settings.name,
                "material_node": (near_nodes if label == "near" else mid_nodes)[role].name,
            })
            image.scale(TILE_SIZE, TILE_SIZE)

        atlas = bpy.data.images.new(
            f"HumanTrace_{role}_Atlas_2K",
            width=ATLAS_WIDTH,
            height=ATLAS_HEIGHT,
            alpha=True,
            float_buffer=False,
        )
        atlas.colorspace_settings.name = near_image.colorspace_settings.name
        output = array("f", defaults[role]) * (ATLAS_WIDTH * ATLAS_HEIGHT)
        for label, image in source_images:
            pixels = array("f", image.pixels[:])
            pixel_count = TILE_SIZE * TILE_SIZE
            if len(pixels) not in (pixel_count * 3, pixel_count * 4):
                raise RuntimeError(f"Unexpected Blender pixel channel layout for {image.name}: {len(pixels)} floats")
            stride = len(pixels) // pixel_count
            tile_x = tile_xs[label]
            for row in range(TILE_SIZE):
                source_start = row * TILE_SIZE * stride
                source_stop = source_start + TILE_SIZE * stride
                if stride == 4:
                    rgba = pixels[source_start:source_stop]
                else:
                    rgb = pixels[source_start:source_stop]
                    rgba = array("f")
                    for pixel in range(TILE_SIZE):
                        i = pixel * 3
                        rgba.extend((rgb[i], rgb[i + 1], rgb[i + 2], 1.0))
                dest_start = ((tile_y + row) * ATLAS_WIDTH + tile_x) * 4
                output[dest_start:dest_start + TILE_SIZE * 4] = rgba
        atlas.pixels.foreach_set(output)
        atlas.pack()
        atlas_images[role] = atlas
        facts[role] = {
            "atlas_image": atlas.name,
            "atlas_size_px": [ATLAS_WIDTH, ATLAS_HEIGHT],
            "colorspace": atlas.colorspace_settings.name,
            "source_maps": source_facts,
            "tile_size_px": [TILE_SIZE, TILE_SIZE],
            "tile_origins_bottom_left_px": {"near": [tile_xs["near"], tile_y], "mid": [tile_xs["mid"], tile_y]},
            "input_was_resized_only": True,
        }
        near_nodes[role].image = atlas

    return atlas_images, facts, {"near": near_nodes, "mid": mid_nodes}


def remap_uv_to_atlas(obj: bpy.types.Object, tile_x: int) -> dict:
    if len(obj.data.uv_layers) != 1:
        raise RuntimeError(f"{obj.name} must have exactly one UV set")
    uv_layer = obj.data.uv_layers.active
    before = uv_facts(obj.data)
    uscale = (TILE_SIZE - 1) / ATLAS_WIDTH
    vscale = (TILE_SIZE - 1) / ATLAS_HEIGHT
    uoffset = (tile_x + 0.5) / ATLAS_WIDTH
    voffset = (TILE_MARGIN + 0.5) / ATLAS_HEIGHT
    for datum in uv_layer.data:
        datum.uv.x = uoffset + datum.uv.x * uscale
        datum.uv.y = voffset + datum.uv.y * vscale
    after = uv_facts(obj.data)
    return {"source": before, "atlas": after, "tile": {"x_px": tile_x, "y_px": TILE_MARGIN,
                                                            "size_px": [TILE_SIZE, TILE_SIZE]}}


def glb_document_and_binary(path: Path) -> tuple[dict, bytes]:
    with path.open("rb") as stream:
        header = stream.read(12)
        if len(header) != 12:
            raise RuntimeError("Output GLB header is truncated")
        magic, version, total_length = struct.unpack("<III", header)
        if magic != 0x46546C67 or version != 2:
            raise RuntimeError("Output is not a GLB v2 file")
        consumed = 12
        document = None
        binary = b""
        while consumed < total_length:
            chunk_header = stream.read(8)
            if len(chunk_header) != 8:
                raise RuntimeError("Output GLB chunk header is truncated")
            size, kind = struct.unpack("<II", chunk_header)
            payload = stream.read(size)
            if len(payload) != size:
                raise RuntimeError("Output GLB chunk is truncated")
            consumed += 8 + size
            if kind == 0x4E4F534A:
                document = json.loads(payload.decode("utf-8").rstrip(" \t\r\n\x00"))
            elif kind == 0x004E4942:
                binary = payload
        if document is None:
            raise RuntimeError("GLB has no JSON chunk")
        return document, binary


def glb_json(path: Path) -> dict:
    return glb_document_and_binary(path)[0]


def embedded_png_facts(path: Path) -> dict[str, dict]:
    document, binary = glb_document_and_binary(path)
    facts = {}
    for image in document.get("images", []):
        if "bufferView" not in image:
            continue
        view = document["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        data = binary[start:start + view["byteLength"]]
        if not data.startswith(b"\x89PNG\r\n\x1a\n") or len(data) < 26:
            raise RuntimeError(f"Source texture {image.get('name')} is not a readable PNG")
        width, height = struct.unpack_from(">II", data, 16)
        bit_depth = data[24]
        color_type = data[25]
        channels = {0: 1, 2: 3, 4: 2, 6: 4}.get(color_type)
        if channels is None:
            raise RuntimeError(f"Source texture has unexpected PNG color type {color_type}")
        facts[image.get("name", "")] = {
            "size_px": [width, height],
            "bit_depth": bit_depth,
            "color_type": color_type,
            "channels": channels,
            "mime_type": image.get("mimeType"),
            "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
        }
    return facts


def output_png_facts(document: dict, binary: bytes) -> list[dict]:
    result = []
    for image in document.get("images", []):
        if "bufferView" not in image:
            raise RuntimeError("Output GLB texture is not embedded")
        view = document["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        data = binary[start:start + view["byteLength"]]
        if not data.startswith(b"\x89PNG\r\n\x1a\n") or len(data) < 26:
            raise RuntimeError(f"Output texture {image.get('name')} is not a PNG")
        width, height = struct.unpack_from(">II", data, 16)
        result.append({
            "name": image.get("name"),
            "mime_type": image.get("mimeType"),
            "size_px": [width, height],
            "bit_depth": data[24],
            "color_type": data[25],
            "bytes": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
        })
    return result


def exported_mesh_facts(document: dict, name: str) -> dict:
    nodes = {node.get("name"): node for node in document.get("nodes", [])}
    node = nodes.get(name)
    if node is None or "mesh" not in node:
        raise RuntimeError(f"Missing exported mesh node {name}")
    mesh = document["meshes"][node["mesh"]]
    accessors = document.get("accessors", [])
    materials = document.get("materials", [])
    tris = 0
    low = [float("inf")] * 3
    high = [float("-inf")] * 3
    uv_sets = set()
    material_names = set()
    for primitive in mesh.get("primitives", []):
        if primitive.get("mode", 4) != 4:
            raise RuntimeError(f"{name} contains non-triangle geometry")
        index = primitive.get("indices")
        position = primitive.get("attributes", {}).get("POSITION")
        if position is None:
            raise RuntimeError(f"{name} has a primitive without POSITION")
        count = accessors[index]["count"] if index is not None else accessors[position]["count"]
        tris += count // 3
        pos = accessors[position]
        if "min" not in pos or "max" not in pos:
            raise RuntimeError(f"{name} POSITION accessor has no AABB")
        low = [min(a, b) for a, b in zip(low, pos["min"])]
        high = [max(a, b) for a, b in zip(high, pos["max"])]
        uv_sets.update(key for key in primitive["attributes"] if key.startswith("TEXCOORD_"))
        material_index = primitive.get("material")
        if material_index is not None:
            material_names.add(materials[material_index].get("name", f"material-{material_index}"))
    return {
        "triangles": tris,
        "primitive_count": len(mesh.get("primitives", [])),
        "material_names": sorted(material_names),
        "uv_sets": sorted(uv_sets),
        "aabb_gltf_y_up_m": {"min": low, "max": high},
        "node_transform": {
            "translation": node.get("translation", [0.0, 0.0, 0.0]),
            "rotation_xyzw": node.get("rotation", [0.0, 0.0, 0.0, 1.0]),
            "scale": node.get("scale", [1.0, 1.0, 1.0]),
        },
    }


def render_preview(scene: bpy.types.Scene, root: bpy.types.Object, lod0: bpy.types.Object,
                   lod1: bpy.types.Object) -> None:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = PREVIEW_SIZE
    scene.render.resolution_y = PREVIEW_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("MeshyLODReviewWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs["Color"].default_value = (0.24, 0.24, 0.24, 1.0)
    scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = 0.75

    low, high = bounds(lod0)
    center = (low + high) * 0.5
    height = high.z - low.z
    target = Vector((center.x, center.y, low.z + height * 0.52))
    camera_data = bpy.data.cameras.new("MeshyLODReviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.24
    camera = bpy.data.objects.new("MeshyLODReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    def area(name, offset, energy, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = target + Vector(offset)
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    area("MeshyReviewKey", (-3.0, -4.0, 4.0), 850.0, 4.0)
    area("MeshyReviewFill", (3.0, -2.0, 2.0), 400.0, 3.0)
    area("MeshyReviewRim", (0.0, 3.0, 3.0), 550.0, 3.0)

    directions = {"front": Vector((0.0, -1.0, 0.0)), "side": Vector((1.0, 0.0, 0.0))}
    for lod_name, obj in (("near", lod0), ("mid", lod1)):
        lod0.hide_render = obj is not lod0
        lod1.hide_render = obj is not lod1
        for view_name, direction in directions.items():
            camera.location = target + direction * (height * 3.0)
            camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = str(ASSET_DIR / f"bluecoat-human-meshy-{lod_name}-{view_name}.png")
            bpy.ops.render.render(write_still=True)
    lod0.hide_render = False
    lod1.hide_render = False


def render_exported_glb_preview() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    scene = bpy.context.scene
    objects = {obj.name: obj for obj in scene.objects}
    root = objects.get("HumanTrace_Root")
    lod0 = objects.get("HumanTrace_LOD0")
    lod1 = objects.get("HumanTrace_LOD1")
    if root is None or lod0 is None or lod1 is None:
        raise RuntimeError("Could not re-import candidate GLB nodes for preview")
    if lod0.type != "MESH" or lod1.type != "MESH":
        raise RuntimeError("Re-imported LOD nodes are not mesh objects")
    render_preview(scene, root, lod0, lod1)


def main() -> None:
    if not RAW_INPUT.is_file() or not NEAR_INPUT.is_file() or not MID_INPUT.is_file() or not IMAGE_INPUT.is_file() or not TRIO_REFERENCE.is_file():
        raise RuntimeError("Meshy source GLBs or image provenance inputs are missing")
    source_png_facts = {
        "near": embedded_png_facts(NEAR_INPUT),
        "mid": embedded_png_facts(MID_INPUT),
    }
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    near = import_single_mesh(NEAR_INPUT)
    near.name = "HumanTrace_LOD0"
    mid = import_single_mesh(MID_INPUT)
    mid.name = "HumanTrace_LOD1"
    if len(near.modifiers) or len(mid.modifiers):
        raise RuntimeError("Imported remesh GLBs unexpectedly contain modifiers")
    if near.data.uv_layers.__len__() != 1 or mid.data.uv_layers.__len__() != 1:
        raise RuntimeError("Near and mid remeshes must each provide exactly one UV set")
    if not near.data.materials or len(near.data.materials) != 1:
        raise RuntimeError("Near remesh does not have exactly one PBR material")
    if not mid.data.materials or len(mid.data.materials) != 1:
        raise RuntimeError("Mid remesh does not have exactly one PBR material")

    apply_world_to_mesh(near)
    apply_world_to_mesh(mid)
    near_uv_original = uv_facts(near.data)
    mid_uv_original = uv_facts(mid.data)
    near_low, near_high = bounds(near)
    mid_low, mid_high = bounds(mid)
    source_height = near_high.z - near_low.z
    if source_height <= 0:
        raise RuntimeError("Near remesh has no positive Z-up height")
    scale = TARGET_HEIGHT_M / source_height
    near_foot_root = Vector(((near_low.x + near_high.x) * 0.5,
                             (near_low.y + near_high.y) * 0.5,
                             near_low.z))
    for obj in (near, mid):
        for vertex in obj.data.vertices:
            vertex.co = (vertex.co - near_foot_root) * scale
        obj.data.update()

    shared_material = near.data.materials[0]
    shared_material.name = "HumanTrace_SharedPBR_2K"
    mid_material = mid.data.materials[0]
    atlas_images, image_facts, material_nodes = build_shared_pbr_atlas(
        shared_material, mid_material, source_png_facts
    )
    for role, node in material_nodes["mid"].items():
        node.image = atlas_images[role]
    uv_atlas_facts = {
        "near": remap_uv_to_atlas(near, TILE_MARGIN),
        "mid": remap_uv_to_atlas(mid, ATLAS_WIDTH // 2 + TILE_MARGIN),
    }
    mid.data.materials.clear()
    mid.data.materials.append(shared_material)
    near.data.materials.clear()
    near.data.materials.append(shared_material)

    root = bpy.data.objects.new("HumanTrace_Root", None)
    root.empty_display_type = "PLAIN_AXES"
    scene.collection.objects.link(root)
    for obj in (near, mid):
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

    for obj in scene.objects:
        obj.select_set(False)
    for obj in (root, near, mid):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_normals=True,
        export_texcoords=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )

    document, output_binary = glb_document_and_binary(OUTPUT_GLB)
    node_names = {node.get("name", "") for node in document.get("nodes", [])}
    for name in ("HumanTrace_Root", "HumanTrace_LOD0", "HumanTrace_LOD1"):
        if name not in node_names:
            raise RuntimeError(f"Output GLB missing node {name}")
    if len(document.get("materials", [])) != 1 or len(document.get("images", [])) != 3:
        raise RuntimeError("Output GLB does not contain one shared PBR material/texture set")
    exported_images = output_png_facts(document, output_binary)
    if any(image["size_px"] != [ATLAS_WIDTH, ATLAS_HEIGHT] for image in exported_images):
        raise RuntimeError("Output PBR maps do not use the expected common 2K-wide atlas")
    if document.get("skins") or document.get("animations"):
        raise RuntimeError("Static trace asset must not include skin or animation")
    lod0_facts = exported_mesh_facts(document, "HumanTrace_LOD0")
    lod1_facts = exported_mesh_facts(document, "HumanTrace_LOD1")
    for name, facts in (("HumanTrace_LOD0", lod0_facts), ("HumanTrace_LOD1", lod1_facts)):
        if facts["node_transform"] != {
            "translation": [0.0, 0.0, 0.0],
            "rotation_xyzw": [0.0, 0.0, 0.0, 1.0],
            "scale": [1.0, 1.0, 1.0],
        }:
            raise RuntimeError(f"{name} node transform is not identity: {facts['node_transform']}")
    root_node = next(node for node in document["nodes"] if node.get("name") == "HumanTrace_Root")
    root_transform = {
        "translation": root_node.get("translation", [0.0, 0.0, 0.0]),
        "rotation_xyzw": root_node.get("rotation", [0.0, 0.0, 0.0, 1.0]),
        "scale": root_node.get("scale", [1.0, 1.0, 1.0]),
    }
    if root_transform != {
        "translation": [0.0, 0.0, 0.0],
        "rotation_xyzw": [0.0, 0.0, 0.0, 1.0],
        "scale": [1.0, 1.0, 1.0],
    }:
        raise RuntimeError(f"Root transform is not identity: {root_transform}")
    if lod0_facts["triangles"] <= lod1_facts["triangles"]:
        raise RuntimeError("LOD0 must contain more triangles than LOD1")
    output_height = lod0_facts["aabb_gltf_y_up_m"]["max"][1] - lod0_facts["aabb_gltf_y_up_m"]["min"][1]
    if not math.isclose(output_height, TARGET_HEIGHT_M, rel_tol=0.0, abs_tol=0.003):
        raise RuntimeError(f"Exported GLTF height {output_height:.5f}m misses the shared fit target")
    if not all(fact["uv_sets"] == ["TEXCOORD_0"] for fact in (lod0_facts, lod1_facts)):
        raise RuntimeError("Expected one shared UV0 layout on both LOD nodes")

    render_exported_glb_preview()
    manifest = {
        "asset_id": "pforest-human-trace-meshy-v1",
        "status": "candidate_exported_deferred_by_gameplay_priority",
        "runtime_candidate": None,
        "candidate_glb_original_path": "game/assets/photoworlds/forest_entry/pforest-human-trace-meshy-v1.glb",
        "root_decision": "Defer a static photo-only figure until campaign clarity is addressed; no source-required interactive NPC is established.",
        "runtime_integration": False,
        "collision_or_interaction": False,
        "story_identity": "one anonymous static photo-only trace; no new cast identity or backend fact",
        "source": {
            "imagegen_input": IMAGE_INPUT.relative_to(ROOT).as_posix(),
            "imagegen_input_sha256": sha256(IMAGE_INPUT),
            "trio_concept": TRIO_REFERENCE.relative_to(ROOT).as_posix(),
            "trio_concept_sha256": sha256(TRIO_REFERENCE),
            "imagegen_generation_id": "exec-4656abc4-63bd-4a96-ad91-d3c2e491c8f1",
            "mesh_task": {"id": BASE_TASK_ID, "actual_consumed_credits": BASE_TASK_CREDITS,
                          "status": "SUCCEEDED",
                          "ai_model": "meshy-7.1", "model_type": "standard",
                          "texture_resolution": "4k", "enable_pbr": True},
            "remesh_tasks": LOD_TASKS,
            "raw_mesh_glb": RAW_INPUT.relative_to(ROOT).as_posix(),
            "raw_mesh_sha256": sha256(RAW_INPUT),
            "raw_mesh_bytes": RAW_INPUT.stat().st_size,
            "near_remesh_glb": NEAR_INPUT.relative_to(ROOT).as_posix(),
            "mid_remesh_glb": MID_INPUT.relative_to(ROOT).as_posix(),
            "near_remesh_sha256": sha256(NEAR_INPUT),
            "mid_remesh_sha256": sha256(MID_INPUT),
            "actual_meshy_credits_total": BASE_TASK_CREDITS + sum(x["actual_consumed_credits"] for x in LOD_TASKS.values()),
            "local_archive_name": LOCAL_ARCHIVE_NAME,
            "archived_receipts": [
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-single-request.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-single-status-3.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-single-download.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-near-remesh-request.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-near-remesh-status-4.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-near-remesh-download.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-mid-remesh-request.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-mid-remesh-status-4.log",
                "assets/source/blender/photoworlds/W01/photographed_people_v1/meshy-bluecoat-mid-remesh-download.log",
            ],
        },
        "export_recipe": {
            "script": "assets/source/blender/photoworlds/W01/photographed_people_v1/export_bluecoat_meshy_lods_v1.py",
            "mesh_transform_baked": True,
            "source_near_aabb_blender_z_up_m": {"min": list(near_low), "max": list(near_high)},
            "source_mid_aabb_blender_z_up_m": {"min": list(mid_low), "max": list(mid_high)},
            "common_uniform_scale": scale,
            "common_translation_blender_xyz_m": list(-near_foot_root * scale),
            "target_height_m": TARGET_HEIGHT_M,
            "near_uv_original": near_uv_original,
            "mid_uv_original": mid_uv_original,
            "uv_atlas_remap": uv_atlas_facts,
            "uv_method": "Each Meshy remesh keeps its own original UV islands; coordinates receive only a tile scale/offset into one shared atlas; no re-unwrap or cross-LOD surface reprojection.",
            "textures": image_facts,
            "texture_policy": "One shared 2048x1024 PBR atlas with a native near tile and native mid tile; input PBR maps downscaled only, no repaint; original per-LOD UV layout is preserved by affine tile remap.",
            "exported_texture_images": exported_images,
            "gltf_up_axis": "Y",
            "gltf_forward_axis": "+Z (in Blender imported raw model front faces -Y; Blender Z-up to glTF Y-up maps glTF +Z to Blender -Y)",
            "root_transform_gltf": root_transform,
            "lod_node_transforms_gltf": {
                "HumanTrace_LOD0": lod0_facts["node_transform"],
                "HumanTrace_LOD1": lod1_facts["node_transform"],
            },
        },
        "lod": {"HumanTrace_LOD0": lod0_facts, "HumanTrace_LOD1": lod1_facts},
        "preview": {
            "near_front": "assets/source/blender/photoworlds/W01/photographed_people_v1/bluecoat-human-meshy-near-front.png",
            "near_side": "assets/source/blender/photoworlds/W01/photographed_people_v1/bluecoat-human-meshy-near-side.png",
            "mid_front": "assets/source/blender/photoworlds/W01/photographed_people_v1/bluecoat-human-meshy-mid-front.png",
            "mid_side": "assets/source/blender/photoworlds/W01/photographed_people_v1/bluecoat-human-meshy-mid-side.png",
            "local_archive_name": LOCAL_ARCHIVE_NAME,
            "purpose": "neutral mesh-only front/side views; not game evidence",
        },
        "output_sha256": sha256(OUTPUT_GLB),
        "output_bytes": OUTPUT_GLB.stat().st_size,
        "limits": [
            "Candidate only. No runtime placement, C# integration, collision, animation, or author/art acceptance is implied.",
            "Both LODs use one shared PBR material, a 2K-wide atlas containing their own remesh texture tiles, and one common transform fit.",
            "Raw task models, API receipts and review previews are kept in one local archive outside the repository; no further Meshy calls are authorized or made.",
            "Blender glTF export logged a multiple-image-node warning; the candidate was re-imported and its PBR near/mid views were rendered from the exported GLB.",
        ],
    }
    OUTPUT_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("MESHY_CANDIDATE_GLB", OUTPUT_GLB)
    print("MESHY_CANDIDATE_SHA256", manifest["output_sha256"])
    print("MESHY_CANDIDATE_BYTES", manifest["output_bytes"])
    print("MESHY_LOD0", json.dumps(lod0_facts, ensure_ascii=False, sort_keys=True))
    print("MESHY_LOD1", json.dumps(lod1_facts, ensure_ascii=False, sort_keys=True))
    print("MESHY_TEXTURES", json.dumps(image_facts, ensure_ascii=False, sort_keys=True))
    print("MESHY_PREVIEW", json.dumps(manifest["preview"], ensure_ascii=False, sort_keys=True))
    print("MESHY_MANIFEST", OUTPUT_MANIFEST)


if __name__ == "__main__":
    main()
