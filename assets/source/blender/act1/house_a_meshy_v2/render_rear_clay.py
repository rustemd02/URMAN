"""One missing construction view of the frozen exported candidate; no derivation."""
from pathlib import Path
import hashlib
import json
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
manifest = json.loads((HERE / 'house-a-v2.manifest.json').read_text())
source = HERE / manifest['output']['file']
output = HERE / 'actual-rear-left-quarter-clay.png'
assert not output.exists(), 'Refusing to overwrite the single control view'
assert hashlib.sha256(source.read_bytes()).hexdigest() == manifest['output']['sha256']
assert tuple(bpy.app.version[:3]) == (4, 5, 12)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
reference = json.loads((HERE.parent / 'neighbor_log_house_a_meshy_v1/inspection/source-structure.json').read_text())['bounds_blenderXYZ']
low = Vector([row[0] for row in reference])
span = Vector([row[1] - row[0] for row in reference])
center = low + span * .5
width = max(span.x, span.y)
distance = width * 3
scene = bpy.context.scene
world = scene.world or bpy.data.worlds.new('World')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.36, .42, .48, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
bpy.ops.object.light_add(type='AREA', location=center + Vector((-width, -width * 1.5, width * 2)))
bpy.context.object.data.energy = 450 * width * width
bpy.context.object.data.size = width * 2
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = 'ORTHO'
camera.data.ortho_scale = width * 1.25
camera.location = center + Vector((distance * .65, distance * .65, span.z * .35))
camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = camera
clay = bpy.data.materials.new('Inspection_ActualGeometry_Clay')
clay.use_nodes = True
shader = clay.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.42, .42, .42, 1)
shader.inputs['Roughness'].default_value = .8
scene.view_layers[0].material_override = clay
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1280
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)
print(json.dumps({'source_sha256': manifest['output']['sha256'], 'render': output.name, 'view': 'actual rear +X and left +Y, clay material override, no albedo/normal maps', 'geometry_unchanged': True}))
