"""One four-triangle willow candidate; bake the existing paid near, never alter it."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import struct

import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = next(parent for parent in HERE.parents if (parent / "game/project.godot").is_file())
SOURCE = ROOT / "game/assets/photoworlds/W02/summer-willow-near-2k-v1.glb"
OUTPUT = HERE / "summer-willow-far-impostor-2k-v1.glb"
CANDIDATE = ROOT / "game/assets/photoworlds/W02" / OUTPUT.name
SOURCE_SHA = "0cf1b02366ff6a047a914873e7ef2723813986993a8056326c435f89069b3cfa"
if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != SOURCE_SHA:
    raise RuntimeError("The reviewed normalized near source changed.")

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
parts = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(parts) != 1:
    raise RuntimeError("Expected the one-mesh near willow source.")
near = parts[0]
transform = near.matrix_world.copy()
for vertex in near.data.vertices:
    vertex.co = transform @ vertex.co
near.parent = None
near.matrix_world.identity()
near.data.update()
near.data.calc_loop_triangles()
if len(near.data.loop_triangles) != 19031:
    raise RuntimeError("Near source triangle contract changed.")
points = [vertex.co for vertex in near.data.vertices]
source_bounds = [[min(point[axis] for point in points), max(point[axis] for point in points)] for axis in range(3)]
height = source_bounds[2][1]
if abs(source_bounds[2][0]) > 1e-5 or abs(height - 4.535) > 1e-4:
    raise RuntimeError("Near source lost its rooted normalized height.")

# Same neutral-albedo emission bake as winter_spruce_meshy_v2. The final
# material below is lit PBR: no baked daytime lighting or night emission.
original_materials = list(near.data.materials)
for slot, original in enumerate(original_materials):
    bake = original.copy()
    bake.name = "Temporary_WillowNeutralAlbedoBake"
    nodes, links = bake.node_tree.nodes, bake.node_tree.links
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
    emission = nodes.new("ShaderNodeEmission")
    color = shader.inputs["Base Color"]
    if color.is_linked:
        links.new(color.links[0].from_socket, emission.inputs["Color"])
    else:
        emission.inputs["Color"].default_value = color.default_value
    emission.inputs["Strength"].default_value = 1
    links.new(emission.outputs[0], output.inputs["Surface"])
    near.data.materials[slot] = bake

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 16
scene.render.film_transparent = True
tile_width, tile_height = 1024, 1280
scene.render.resolution_x, scene.render.resolution_y = tile_width, tile_height
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
# A little headroom, with the original ground at the lower card edge.
card_height = height * 1.01
card_width = card_height * tile_width / tile_height
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = "ORTHO"
camera.data.ortho_scale = card_height
scene.camera = camera
atlas_pixels = np.zeros((tile_height, tile_width * 2, 4), dtype=np.float32)
for index, position in enumerate(((0, -12, card_height / 2), (12, 0, card_height / 2))):
    camera.location = position
    camera.rotation_euler = (Vector((0, 0, card_height / 2)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(HERE / "temporary-bake-tile.png")
    bpy.ops.render.render(write_still=True)
    tile = bpy.data.images.load(scene.render.filepath, check_existing=False)
    rgba = np.array(tile.pixels[:], dtype=np.float32).reshape((tile_height, tile_width, 4))
    # RGB bleed only: retain exact coverage while protecting filtered mask edges
    # against transparent black texels. Never bleed across the atlas tile seam.
    filled = rgba[:, :, 3] > 0
    for _ in range(8):
        rgb_sum = np.zeros_like(rgba[:, :, :3])
        count = np.zeros_like(rgba[:, :, 3])
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            shifted = np.roll(filled, (dy, dx), (0, 1))
            if dy == -1: shifted[-1, :] = False
            if dy == 1: shifted[0, :] = False
            if dx == -1: shifted[:, -1] = False
            if dx == 1: shifted[:, 0] = False
            rgb_sum += np.roll(rgba[:, :, :3], (dy, dx), (0, 1)) * shifted[:, :, None]
            count += shifted
        spread = ~filled & (count > 0)
        rgba[:, :, :3][spread] = rgb_sum[spread] / count[spread, None]
        filled |= spread
    atlas_pixels[:, index * tile_width:(index + 1) * tile_width, :] = rgba
    bpy.data.images.remove(tile)
(HERE / "temporary-bake-tile.png").unlink()
for slot, material in enumerate(original_materials):
    near.data.materials[slot] = material
atlas = bpy.data.images.new("URMAN_SummerWillow_FarAtlas_v1", width=tile_width * 2, height=tile_height, alpha=True)
atlas.colorspace_settings.name = "sRGB"
atlas.pixels.foreach_set(atlas_pixels.ravel())
atlas.file_format = "PNG"
atlas.filepath_raw = str(HERE / "far-albedo-atlas.png")
atlas.save()
atlas.pack()

material = bpy.data.materials.new("URMAN_SummerWillow_FarMask_v1")
material.use_nodes = True
material.use_backface_culling = False
shader = material.node_tree.nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value = 1
shader.inputs["Metallic"].default_value = 0
shader.inputs["Emission Strength"].default_value = 0
texture = material.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = atlas
texture.extension = "EXTEND"
clip = material.node_tree.nodes.new("ShaderNodeMath")
clip.operation = "ROUND"
material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
material.node_tree.links.new(texture.outputs["Alpha"], clip.inputs[0])
material.node_tree.links.new(clip.outputs[0], shader.inputs["Alpha"])
half = card_width / 2
vertices = [(-half, 0, 0), (half, 0, 0), (half, 0, card_height), (-half, 0, card_height),
            (0, -half, 0), (0, half, 0), (0, half, card_height), (0, -half, card_height)]
mesh = bpy.data.meshes.new("SummerWillow_Far_LOD2_Mesh")
mesh.from_pydata(vertices, [], [(0, 1, 2, 3), (4, 5, 6, 7)])
mesh.materials.append(material)
uv = mesh.uv_layers.new(name="UVMap")
for index, polygon in enumerate(mesh.polygons):
    coords = ((index * .5, 0), ((index + 1) * .5, 0), ((index + 1) * .5, 1), (index * .5, 1))
    for loop, value in zip(polygon.loop_indices, coords):
        uv.data[loop].uv = value
far = bpy.data.objects.new("SummerWillow_Far_LOD2", mesh)
bpy.context.collection.objects.link(far)
far["source_sha256"] = SOURCE_SHA
far["collision"] = "none"
far["source_height_m"] = height
far["root_ground_m"] = 0.0
bpy.ops.object.select_all(action="DESELECT")
far.select_set(True)
bpy.context.view_layer.objects.active = far
bpy.ops.export_scene.gltf(filepath=str(OUTPUT), export_format="GLB", use_selection=True,
                          export_yup=True, export_texcoords=True, export_normals=True,
                          export_tangents=True, export_materials="EXPORT", export_animations=False,
                          export_image_format="AUTO")

# Inspect the saved payload, then reimport it for the one candidate preview.
blob = OUTPUT.read_bytes()
length, kind = struct.unpack_from("<II", blob, 12)
gltf = json.loads(blob[20:20 + length])
primitives = [primitive for entry in gltf["meshes"] for primitive in entry["primitives"]]
triangles = sum(gltf["accessors"][primitive["indices"]]["count"] // 3 for primitive in primitives)
exported_material = gltf["materials"][0]
if triangles != 4 or len(primitives) != 1 or exported_material.get("alphaMode") != "MASK" or exported_material.get("alphaCutoff", .5) != .5:
    raise RuntimeError("Exported far candidate lost its four-triangle/shared MASK material contract.")
if not exported_material.get("doubleSided"):
    raise RuntimeError("Crosscards must remain double-sided.")
shutil.copyfile(OUTPUT, CANDIDATE)

scene.render.film_transparent = False
scene.render.resolution_x, scene.render.resolution_y = 720, 960
scene.view_settings.view_transform = "AgX"
world = scene.world or bpy.data.worlds.new("WillowNeutralPreviewWorld")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (.36, .42, .48, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
bpy.ops.object.light_add(type="AREA", location=(-4, -6, 8))
bpy.context.object.data.energy = 650
bpy.context.object.data.size = 6
camera.data.ortho_scale = height * 1.2
far.hide_render = True
for name, position in (("source-front", (0, -12, height / 2)), ("source-side", (12, 0, height / 2))):
    camera.location = position
    camera.rotation_euler = (Vector((0, 0, height / 2)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(HERE / (name + ".png"))
    bpy.ops.render.render(write_still=True)
near.hide_render = True
far.hide_render = True
bpy.ops.import_scene.gltf(filepath=str(OUTPUT))
reimported = next(obj for obj in bpy.context.selected_objects if obj.type == "MESH")
camera.location = (7, -11, height / 2 + .9)
camera.rotation_euler = (Vector((0, 0, height / 2)) - camera.location).to_track_quat("-Z", "Y").to_euler()
scene.render.filepath = str(HERE / "candidate-preview.png")
bpy.ops.render.render(write_still=True)

coverage = atlas_pixels[:, :, 3]
position_accessor = gltf["accessors"][primitives[0]["attributes"]["POSITION"]]
report = {
    "status": "one_far_impostor_preparation_source_only_not_runtime_or_art_accepted",
    "recipe": Path(__file__).name,
    "pattern": "assets/source/blender/act1/winter_spruce_meshy_v2/prepare_far_impostor.py",
    "source": str(SOURCE.relative_to(ROOT)), "source_sha256": SOURCE_SHA,
    "source_triangles": 19031, "source_bounds_blender_xyz_m": source_bounds,
    "source_material_unchanged": True, "new_meshy_or_imagegen_spend": 0,
    "output": OUTPUT.name, "candidate": str(CANDIDATE.relative_to(ROOT)),
    "sha256": hashlib.sha256(blob).hexdigest(), "bytes": len(blob),
    "triangles": triangles, "surfaces": len(primitives),
    "exported_bounds_godot_xyz_m": {"min": position_accessor["min"], "max": position_accessor["max"]},
    "root": [0, 0, 0], "source_visible_height_m": height,
    "card_height_including_headroom_m": card_height, "card_width_m": card_width,
    "atlas": "far-albedo-atlas.png", "atlas_dimensions": [tile_width * 2, tile_height],
    "views_degrees": [0, 90], "rgb_bleed_pixels": 8,
    "alpha_zero_pixels": int(np.count_nonzero(coverage == 0)),
    "alpha_opaque_pixels": int(np.count_nonzero(coverage >= .5)),
    "alpha_fraction_covered": float(np.count_nonzero(coverage >= .5) / coverage.size),
    "material": exported_material,
    "material_contract": "same pine MASK/0.5 pattern; lit roughness1 metallic0 emission0 double-sided; no BLEND sorting, ordinary depth/fog/shadow paths; native mapping unverified",
    "source_views": ["source-front.png", "source-side.png"],
    "candidate_preview": "candidate-preview.png",
    "preview_scope": "Blender lit render of reimported candidate GLB; no 18-40m native acceptance",
    "runtime_changes": "none; near GLB and two near roots unchanged; no contact/state/terrain/placement edits",
    "limits": "Crosscard lighting/self-shadow/oblique X read and edge filtering at 18-40m require root art review and later targeted native view; this is the only preparation attempt."
}
(HERE / "candidate-structure.json").write_text(json.dumps(report, indent=2) + "\n")
print("WILLOW_FAR_COMPLETE " + json.dumps({key: report[key] for key in ("status", "bytes", "triangles", "surfaces", "sha256", "alpha_fraction_covered")}))
