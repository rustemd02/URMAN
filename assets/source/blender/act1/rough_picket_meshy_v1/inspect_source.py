"""Inspect one Meshy fence source; never changes geometry or binds runtime."""
from pathlib import Path
import hashlib, json, struct, sys
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = HERE / ('meshy71-rough-picket-original.glb' if '--original' in sys.argv else 'meshy71-rough-picket-remesh-25k.glb')
OUT = HERE / 'inspection'
assert not OUT.exists(), 'Do not overwrite the one source inspection'
receipt = json.loads((HERE / ('download-original.json' if '--original' in sys.argv else 'download-remesh.json')).read_text())
assert SOURCE.stat().st_size == receipt['bytes'], 'Source download is incomplete'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == receipt['sha256'], 'Source download SHA mismatch'
with SOURCE.open('rb') as stream:
    magic, version, total = struct.unpack('<4sII', stream.read(12))
    assert magic == b'glTF' and version == 2
    assert total == SOURCE.stat().st_size, 'GLB length mismatch'
    length, kind = struct.unpack('<I4s', stream.read(8))
    assert kind == b'JSON'
    gltf = json.loads(stream.read(length))
OUT.mkdir()
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
objects = [ob for ob in bpy.data.objects if ob.type == 'MESH']
points = [ob.matrix_world @ v.co for ob in objects for v in ob.data.vertices]
low = Vector([min(v[i] for v in points) for i in range(3)])
high = Vector([max(v[i] for v in points) for i in range(3)])
span = high - low
center = (high + low) * .5
width = max(span.x, span.y)
along = Vector((1, 0, 0)) if span.x >= span.y else Vector((0, 1, 0))
depth = Vector((-along.y, along.x, 0))
distance = width * 3
for ob in objects:
    ob.data.calc_loop_triangles()
report = {
    'source': SOURCE.name,
    'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    'source_bytes': SOURCE.stat().st_size,
    'triangles': sum(len(ob.data.loop_triangles) for ob in objects),
    'mesh_objects': len(objects),
    'bounds_blenderXYZ': [[low[i], high[i]] for i in range(3)],
    'long_axis': list(along), 'depth_axis': list(depth),
    'materials': [{'name': m.get('name'), 'alphaMode': m.get('alphaMode', 'OPAQUE'), 'doubleSided': m.get('doubleSided', False)} for m in gltf.get('materials', [])],
    'images': [{'name': i.get('name'), 'mimeType': i.get('mimeType'), 'embedded': 'bufferView' in i} for i in gltf.get('images', [])],
    'skins': len(gltf.get('skins', [])), 'animations': len(gltf.get('animations', [])),
    'geometry_modified': False,
}
world = bpy.context.scene.world or bpy.data.worlds.new('World')
scene = bpy.context.scene
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.36, .42, .48, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
bpy.ops.object.light_add(type='AREA', location=center + depth * width * 1.5 + along * width + Vector((0, 0, width * 2)))
bpy.context.object.data.energy = 450 * width * width
bpy.context.object.data.size = width * 2
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = 'ORTHO'
scene.camera = camera
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1280
scene.render.resolution_y = 960
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
clay = bpy.data.materials.new('Inspection_ActualGeometry_Clay')
clay.use_nodes = True
shader = clay.node_tree.nodes.get('Principled BSDF')
shader.inputs['Base Color'].default_value = (.42, .42, .42, 1)
shader.inputs['Roughness'].default_value = .8
views = [
    ('face-positive-depth', depth * distance + Vector((0, 0, span.z * .08)), center, width * 1.15, False),
    ('face-negative-depth', -depth * distance + Vector((0, 0, span.z * .08)), center, width * 1.15, False),
    ('quarter-positive-depth', depth * distance * .85 + along * distance * .45 + Vector((0, 0, span.z * .30)), center, width * 1.25, False),
    ('quarter-negative-depth', -depth * distance * .85 - along * distance * .45 + Vector((0, 0, span.z * .30)), center, width * 1.25, False),
    ('gap-clay-positive-depth', depth * distance + along * distance * .12 + Vector((0, 0, span.z * .08)), center, width * .48, True),
    ('rail-clay-negative-depth', -depth * distance - along * distance * .20 + Vector((0, 0, span.z * .20)), center, width * .64, True),
]
for name, offset, target, ortho, clay_view in views:
    scene.view_layers[0].material_override = clay if clay_view else None
    camera.location = target + offset
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = ortho
    scene.render.filepath = str(OUT / (name + '.png'))
    bpy.ops.render.render(write_still=True)
report['renders'] = [name + '.png' for name, *_ in views]
report['inspection_method'] = 'Four whole views on both depth faces and two opaque clay closeups without albedo/normal/alpha textures. Actual gaps/rail backing must be judged from these, not alphaMode alone.'
report['status'] = 'Awaiting actual source view review; not runtime, metric or art acceptance.'
(OUT / 'source-structure.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
