"""Render the actual current Variant A dwelling subtree as a measured form reference."""
from pathlib import Path
import hashlib,json
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
SOURCE=ROOT/'game/assets/models/act1/urman_village_exterior_kit.glb'
COMPONENT='VillageParcel_VariantA_TimberGable_Dwelling'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
root=bpy.data.objects[COMPONENT];inverse=root.matrix_world.inverted();meshes=[ob for ob in root.children_recursive if ob.type=='MESH'];keep=set(meshes)
for ob in meshes:
    matrix=inverse@ob.matrix_world;ob.parent=None;ob.matrix_world=matrix
for ob in list(bpy.data.objects):
    if ob not in keep:bpy.data.objects.remove(ob,do_unlink=True)
points=[ob.matrix_world@Vector(corner) for ob in meshes for corner in ob.bound_box]
bounds=[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=(-3,-6,11));bpy.context.object.data.energy=1700;bpy.context.object.data.size=7
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=12;bpy.context.scene.camera=camera
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
for name,position,target in [('existing-front-right',(11,-12,8),(.65,1.3,2.2)),('existing-front',(0,-15,4.5),(0,1.3,2.4))]:
    camera.location=position;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True)
for ob in meshes:ob.data.calc_loop_triangles()
report={'source':str(SOURCE.relative_to(ROOT)),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'component':COMPONENT,'mesh_objects':len(meshes),'component_local_bounds_blenderXYZ':bounds,'triangles':sum(len(ob.data.loop_triangles) for ob in meshes),'renders':['existing-front-right.png','existing-front.png'],'status':'Own actual currentGLB reference only, not generatedcandidate or finalart acceptance.'}
(HERE/'existing-source-structure.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
