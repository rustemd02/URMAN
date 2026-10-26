"""One finite three-view source inspection; preserve raw GLB and runtime."""
import hashlib
import json
import time
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = HERE / 'meshy71-birch-original.glb'
EXPECTED_SHA = '3618cb70c9bbe1e8568d014ea6429698e3d774a6bb75500ee64039b9e657349a'
started = time.time()
if bpy.app.version[:3] != (4, 5, 12):
    raise RuntimeError('Use the pinned Blender 4.5.12 source renderer')
if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != EXPECTED_SHA:
    raise RuntimeError('Raw source SHA mismatch')
for label in ('front', 'right', 'three-quarter'):
    if (HERE / f'raw-{label}.png').exists():
        raise RuntimeError('Existing preview must not be overwritten')
if (HERE / 'raw-preview-receipt.json').exists():
    raise RuntimeError('Existing receipt must not be overwritten')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes = [ob for ob in bpy.data.objects if ob.type == 'MESH']
bpy.context.view_layer.update()
points = [ob.matrix_world @ Vector(corner) for ob in meshes for corner in ob.bound_box]
lo = Vector(tuple(min(point[k] for point in points) for k in range(3)))
hi = Vector(tuple(max(point[k] for point in points) for k in range(3)))
centre = (lo + hi) / 2
extent = max(hi - lo)
records = []
for ob in meshes:
    ob.data.calc_loop_triangles()
    records.append({'node': ob.name, 'vertices': len(ob.data.vertices),
                    'triangles': len(ob.data.loop_triangles),
                    'uv_layers': list(ob.data.uv_layers.keys()),
                    'materials': [m.name if m else None for m in ob.data.materials]})

scene = bpy.context.scene
world = scene.world or bpy.data.worlds.new('World')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.28, .29, .30, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
bpy.ops.object.light_add(type='AREA', location=centre + Vector((-1.1, -1.6, 1.7)) * extent)
bpy.context.object.data.energy = 1000 * extent * extent
bpy.context.object.data.size = extent * 1.6
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = 'ORTHO'
camera.data.ortho_scale = extent * 1.16
scene.camera = camera
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.resolution_x = 800
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
views = []
for label, direction in (('front', (0, -1, .035)),
                         ('right', (1, 0, .035)),
                         ('three-quarter', (1, -1, .13))):
    camera.location = centre + Vector(direction).normalized() * extent * 2.8
    camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
    output = HERE / f'raw-{label}.png'
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    views.append({'path': output.name, 'sha256': hashlib.sha256(output.read_bytes()).hexdigest(),
                  'bytes': output.stat().st_size, 'camera_direction': direction})
if hashlib.sha256(SOURCE.read_bytes()).hexdigest() != EXPECTED_SHA:
    raise RuntimeError('Raw source changed during inspection')
report = {'schema': 'urman-meshy-source-preview/v1',
          'scope': 'One finite front/right/three-quarter raw material preview; no GLB edits/exports, mesh preparation, runtime/game/performance acceptance.',
          'source': SOURCE.name, 'source_sha256': EXPECTED_SHA,
          'blender': bpy.app.version_string, 'producer': Path(__file__).name,
          'producer_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
          'method': 'Existing archived AS05 inspect_candidate.py source-render pattern. One import, Cycles16, orthographic, 800x960, shared neutral world/area lighting, AgX/None/exposure0/gamma1; original imported PBR retained.',
          'bounds_blender_z_up': [list(lo), list(hi)], 'meshes': records,
          'views': views, 'elapsed_seconds': round(time.time() - started, 3),
          'next_action': 'Root inspect all three actual views before any conditional5credit remesh.'}
(HERE / 'raw-preview-receipt.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
