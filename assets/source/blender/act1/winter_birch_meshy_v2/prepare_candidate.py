"""One source-only birch LOD family, following the deployed pine V2 recipe."""
from pathlib import Path
import hashlib
import json
import math
import struct
import time

import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCE = HERE / 'meshy71-birch-remesh-25k.glb'
OUTPUT = HERE / 'winter-birch-meshy-v2.glb'
SOURCE_SHA = '3df252f7be0c96076d9f3c0d788fb1efda0740adef708b85bafa393a0c73af49'
DONOR = ROOT / 'game/assets/models/agent_b_act1/agentb_foliage_kit.glb'
DONOR_SHA = '2e04434de764ece1cdb72367636cf0daafd1c0d539e59d9e417ad4dc255b5529'
started = time.time()

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def tris(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)

def bounds(ob):
    return [[min(v.co[k] for v in ob.data.vertices),
             max(v.co[k] for v in ob.data.vertices)] for k in range(3)]

require(bpy.app.version[:3] == (4, 5, 12), 'Use pinned Blender 4.5.12')
require(sha(SOURCE) == SOURCE_SHA, 'Remesh input SHA mismatch')
require(sha(DONOR) == DONOR_SHA, 'Current native birch donor SHA mismatch')
for name in ('winter-birch-meshy-v2.glb', 'candidate-structure.json',
             'far-albedo-atlas.png', 'candidate-near-front.png',
             'candidate-near-right.png', 'candidate-mid-front.png',
             'candidate-mid-right.png', 'far-bake-tile.png'):
    require(not (HERE / name).exists(), f'Will not overwrite {name}')

# Read the same native root pivots/geometry used by PlantFoliage.Geometry.
# This is provenance for the integration owner; the old GLB is never changed.
donor_bytes = DONOR.read_bytes()
json_size = struct.unpack_from('<I', donor_bytes, 12)[0]
donor = json.loads(donor_bytes[20:20 + json_size])
bin_offset = 20 + json_size
bin_size, bin_kind = struct.unpack_from('<II', donor_bytes, bin_offset)
require(bin_kind == 0x004e4942, 'Expected embedded native donor BIN')
donor_bin = memoryview(donor_bytes)[bin_offset + 8:bin_offset + 8 + bin_size]

def donor_positions(accessor_id):
    accessor = donor['accessors'][accessor_id]
    view = donor['bufferViews'][accessor['bufferView']]
    require(accessor['componentType'] == 5126 and accessor['type'] == 'VEC3'
            and not accessor.get('sparse'), 'Unsupported donor position accessor')
    offset = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
    stride = view.get('byteStride', 12)
    return [struct.unpack_from('<3f', donor_bin, offset + i * stride)
            for i in range(accessor['count'])]

native_variants = []
for prefix in ('WinterBirch', 'WinterLightBirch', 'WinterFarBirch'):
    for variant_id in (1, 2):
        variant = f'{prefix}_{variant_id}'
        root_node = next(node for node in donor['nodes'] if node.get('name') == variant + '_Root')
        pivot = root_node.get('translation', [0, 0, 0])
        require(not any(root_node.get(k) for k in ('matrix', 'rotation', 'scale')),
                'Unexpected native root transform')
        points = []
        for child_id in root_node['children']:
            child = donor['nodes'][child_id]
            require(not any(child.get(k) for k in ('matrix', 'rotation', 'scale', 'children')),
                    'Unexpected native mesh transform')
            translation = child.get('translation', [0, 0, 0])
            for primitive in donor['meshes'][child['mesh']]['primitives']:
                for position in donor_positions(primitive['attributes']['POSITION']):
                    # Parent root translation cancels when rebased to that root.
                    points.append([position[k] + translation[k] for k in range(3)])
        native_variants.append({'variant': variant, 'native_root_gltf_y_up': pivot,
                                'rooted_bounds_godot_y_up': [[min(p[k] for p in points),
                                                              max(p[k] for p in points)]
                                                             for k in range(3)]})

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes = [ob for ob in bpy.data.objects if ob.type == 'MESH']
require(len(meshes) == 1 and not any(ob.type == 'ARMATURE' for ob in bpy.data.objects),
        'Expected one unrigged remesh')
near = meshes[0]
bpy.ops.object.select_all(action='DESELECT')
near.select_set(True)
bpy.context.view_layer.objects.active = near
world_transform = near.matrix_world.copy()
near.parent = None
near.matrix_world = world_transform
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
before = bounds(near)
height = before[2][1] - before[2][0]
foot = [v.co.copy() for v in near.data.vertices if v.co.z <= before[2][0] + height * .012]
require(height > 0 and len(foot) > 0, 'Missing raw ground/root envelope')
root = Vector((sum(v.x for v in foot) / len(foot),
               sum(v.y for v in foot) / len(foot), before[2][0]))
for vertex in near.data.vertices:
    vertex.co = (vertex.co - root) / height
near.name = 'WinterBirch_LOD0'
near.data.name = near.name + '_Mesh'
near.data.update()
source_triangles = tris(near)
require(source_triangles <= 26000, 'Near source exceeds approved approximate25K donor')
near_bounds = bounds(near)
require(len(near.data.materials) == 1, 'Expected one shared source PBR material')
images = []
for material in near.data.materials:
    require(material is not None and material.use_nodes, 'Missing source PBR graph')
    material.name = 'URMAN_WinterBirch_Meshy_v2_PBR'
    for node in material.node_tree.nodes:
        if node.type != 'TEX_IMAGE' or node.image is None:
            continue
        image = node.image
        if any(record['name'] == image.name for record in images):
            continue
        role = 'basecolor' if image.colorspace_settings.name != 'Non-Color' else 'data'
        limit = 2048 if role == 'basecolor' else 1024
        old = list(image.size)
        scale = min(1, limit / max(old))
        size = [max(1, round(axis * scale)) for axis in old]
        if size != old:
            image.scale(*size)
        image.file_format = 'PNG'
        image.pack()
        images.append({'name': image.name, 'role': role, 'before': old, 'after': size,
                       'colorspace': image.colorspace_settings.name})

mid = near.copy()
mid.data = near.data.copy()
bpy.context.collection.objects.link(mid)
mid.name = 'WinterBirch_LOD1'
mid.data.name = mid.name + '_Mesh'
bpy.context.view_layer.objects.active = mid
modifier = mid.modifiers.new('BudgetLOD', 'DECIMATE')
modifier.ratio = (8000 - .5) / tris(mid)
modifier.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=modifier.name)
require(tris(mid) <= 8000, 'Mid exceeds approved8K budget')
for vertex in mid.data.vertices:
    for axis, (low, high) in enumerate(near_bounds):
        vertex.co[axis] = max(low, min(high, vertex.co[axis]))
mid.data.update()

# Same neutral-albedo two-view far crosscard as pine V2. Birch is wider:
# square512tiles avoid the pine portrait crop; projected UV intervals map each
# actual near bound exactly, retaining padding outside those intervals.
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.film_transparent = True
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
source_materials = list(near.data.materials)
for index, material in enumerate(source_materials):
    bake = material.copy()
    bake.name = 'Temporary_NeutralBirchAlbedoBake'
    nodes, links = bake.node_tree.nodes, bake.node_tree.links
    shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
    output = next(node for node in nodes if node.type == 'OUTPUT_MATERIAL')
    color = shader.inputs['Base Color']
    emission = nodes.new('ShaderNodeEmission')
    if color.is_linked:
        links.new(color.links[0].from_socket, emission.inputs['Color'])
    else:
        emission.inputs['Color'].default_value = color.default_value
    emission.inputs['Strength'].default_value = 1
    links.new(emission.outputs[0], output.inputs['Surface'])
    near.data.materials[index] = bake
mid.hide_render = True
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = 'ORTHO'
scene.camera = camera
field_height = 1.06
camera.data.ortho_scale = field_height
frame = camera.data.view_frame(scene=scene)
frame_height = max(v.y for v in frame) - min(v.y for v in frame)
camera.data.ortho_scale *= field_height / frame_height
frame = camera.data.view_frame(scene=scene)
field_width = max(v.x for v in frame) - min(v.x for v in frame)
require(abs(field_width - field_height) < 1e-5, 'Expected square atlas tile projection')
require(max(near_bounds[0][1] - near_bounds[0][0],
            near_bounds[1][1] - near_bounds[1][0]) < field_width,
        'Near crown does not fit neutral atlas field')
pixels = np.zeros((512, 1024, 4), dtype=np.float32)
atlas_uvs = []
temporary_tile = HERE / 'far-bake-tile.png'
for index, horizontal_axis in enumerate((0, 1)):
    low, high = near_bounds[horizontal_axis]
    centre = (low + high) / 2
    if index == 0:
        target = Vector((centre, 0, .5))
        camera.location = (centre, -3, .5)
    else:
        target = Vector((0, centre, .5))
        camera.location = (3, centre, .5)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(temporary_tile)
    bpy.ops.render.render(write_still=True)
    tile = bpy.data.images.load(str(temporary_tile), check_existing=False)
    pixels[:, index * 512:(index + 1) * 512, :] = np.array(tile.pixels[:], dtype=np.float32).reshape((512, 512, 4))
    bpy.data.images.remove(tile)
    u0 = index * .5 + .5 * (.5 + (low - centre) / field_width)
    u1 = index * .5 + .5 * (.5 + (high - centre) / field_width)
    v0, v1 = .5 - .5 / field_height, .5 + .5 / field_height
    atlas_uvs.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
temporary_tile.unlink()
for index, material in enumerate(source_materials):
    near.data.materials[index] = material
atlas = bpy.data.images.new('URMAN_WinterBirch_Meshy_v2_FarAtlas', width=1024, height=512, alpha=True)
atlas.colorspace_settings.name = 'sRGB'
atlas.pixels.foreach_set(pixels.ravel())
atlas.file_format = 'PNG'
atlas.filepath_raw = str(HERE / 'far-albedo-atlas.png')
atlas.save()
atlas.pack()
far_material = bpy.data.materials.new('URMAN_WinterBirch_Meshy_v2_FarMask')
far_material.use_nodes = True
shader = far_material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value = 1
shader.inputs['Metallic'].default_value = 0
shader.inputs['Emission Strength'].default_value = 0
texture = far_material.node_tree.nodes.new('ShaderNodeTexImage')
texture.image = atlas
texture.extension = 'EXTEND'
clip = far_material.node_tree.nodes.new('ShaderNodeMath')
clip.operation = 'ROUND'
far_material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
far_material.node_tree.links.new(texture.outputs['Alpha'], clip.inputs[0])
far_material.node_tree.links.new(clip.outputs[0], shader.inputs['Alpha'])
x0, x1 = near_bounds[0]
y0, y1 = near_bounds[1]
vertices = [(x0, 0, 0), (x1, 0, 0), (x1, 0, 1), (x0, 0, 1),
            (0, y0, 0), (0, y1, 0), (0, y1, 1), (0, y0, 1)]
far_mesh = bpy.data.meshes.new('WinterBirch_LOD2_Mesh')
far_mesh.from_pydata(vertices, [], [(0, 1, 2, 3), (4, 5, 6, 7)])
far_mesh.materials.append(far_material)
uv_layer = far_mesh.uv_layers.new(name='UVMap')
for polygon, coords in zip(far_mesh.polygons, atlas_uvs):
    for loop, value in zip(polygon.loop_indices, coords):
        uv_layer.data[loop].uv = value
far = bpy.data.objects.new('WinterBirch_LOD2', far_mesh)
bpy.context.collection.objects.link(far)
bpy.ops.object.select_all(action='DESELECT')
for ob in (near, mid, far):
    ob.select_set(True)
bpy.context.view_layer.objects.active = near
bpy.ops.export_scene.gltf(filepath=str(OUTPUT), export_format='GLB', use_selection=True,
                          export_yup=True, export_texcoords=True, export_normals=True,
                          export_tangents=True, export_materials='EXPORT',
                          export_animations=False, export_image_format='AUTO')

# One finite post-preparation batch: same whole-tree front/side, near and mid.
scene.render.film_transparent = False
scene.render.resolution_x = 800
scene.render.resolution_y = 960
scene.view_settings.view_transform = 'AgX'
world = scene.world or bpy.data.worlds.new('World')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.28, .29, .30, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
centre = Vector((0, 0, .5))
bpy.ops.object.light_add(type='AREA', location=centre + Vector((-1.1, -1.6, 1.7)))
bpy.context.object.data.energy = 1000
bpy.context.object.data.size = 1.6
camera.data.ortho_scale = 1.16
views = []
for label, direction in (('front', (0, -1, .035)), ('right', (1, 0, .035))):
    camera.location = centre + Vector(direction).normalized() * 2.8
    camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
    for tier, ob in (('near', near), ('mid', mid)):
        for other in (near, mid, far):
            other.hide_render = other != ob
        output = HERE / f'candidate-{tier}-{label}.png'
        scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        views.append({'path': output.name, 'sha256': sha(output), 'bytes': output.stat().st_size})
require(sha(SOURCE) == SOURCE_SHA and sha(DONOR) == DONOR_SHA, 'Source changed during preparation')
report = {'schema': 'urman-shared-tree-family-source/v2',
          'status': 'one_prepared_birch_source_family_root_visual_gate_pending_no_runtime',
          'source': SOURCE.name, 'source_sha256': SOURCE_SHA,
          'output': OUTPUT.name, 'output_sha256': sha(OUTPUT), 'bytes': OUTPUT.stat().st_size,
          'producer': Path(__file__).name, 'producer_sha256': sha(Path(__file__)),
          'blender': bpy.app.version_string, 'source_triangles': source_triangles,
          'normalization': {'source_bounds_blender': before, 'source_height': height,
                            'source_foot_center': list(root),
                            'method': 'Apply world transforms to vertices; lower1.2% footcentroid→rootXZ0, ground0,height1. AllLOD identity/sharedorigin; midcollapse overshoot clamped to nearenvelope. Provider native roots/dimensions below remain authoritative at integration.'},
          'native_provider': {'source': str(DONOR.relative_to(ROOT)), 'source_sha256': DONOR_SHA,
                              'hook': 'AgentBAct1ExteriorLayer.PlantFoliage→RegionalMesh; Geometry native variant_Root remains physical/support/placement owner; publicFoliageMesh feeds BindAuthoredWinterTrees.',
                              'variants': native_variants,
                              'runtime_path_proposed': 'res://assets/models/act1/urman_winter_birch_meshy_v2.glb'},
          'lods': [{'node': ob.name, 'triangles': tris(ob), 'bounds_blender': bounds(ob),
                    'matrix_world': [list(row) for row in ob.matrix_world]} for ob in (near, mid, far)],
          'materials': [{'name': material.name, 'shared_by_near_mid': True} for material in source_materials]
                       + [{'name': far_material.name, 'alpha_mode': 'MASK', 'alpha_cutoff': .5,
                           'lit': True, 'emission_strength': 0}],
          'images': images,
          'far_impostor': {'atlas': 'far-albedo-atlas.png', 'dimensions': [1024, 512],
                           'triangles': 4, 'source': 'Same preparedLOD0 neutral basecolor only',
                           'projection': 'Two square512tiles; field1.06, sameheight/centre; exact per-axis nearbounds projected UV intervals crop padded field onto matching0..1-height cards. Prevents wide-birch cropping by pine portraittile and avoids enlarged cullingAABB.',
                           'uvs': atlas_uvs, 'field_width': field_width, 'field_height': field_height,
                           'alpha_mode': 'MASK', 'transparent_blend': False,
                           'roughness': 1, 'metallic': 0, 'emissive': False},
          'views': views, 'elapsed_seconds': round(time.time() - started, 3),
          'paid_stage': {'image_task_id': '01a175ad-5bc0-7560-a1e0-1fcfbb0c46e5',
                         'image_actual_credits': 30, 'remesh_task_id': '01a175b9-840c-7357-bd85-03fb0586f8c1',
                         'remesh_actual_credits': 5, 'total': 35, 'further_posts': False},
          'remaining': 'Root sourcevisual decision and separate runtime integration/import, actual whole-scene motion/LOD/performance/art. No branchpolish or repeatedgeneration.'}
(HERE / 'candidate-structure.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
