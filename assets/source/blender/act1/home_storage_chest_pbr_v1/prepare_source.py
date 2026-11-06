"""Existing living-room storage chest, geometry-preserving UV donor for one retexture."""
from pathlib import Path
import bpy,bmesh,json,hashlib,importlib.util,sys
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
SOURCE=ROOT/'game/assets/generated/urman_modular_kit.glb'
spec=importlib.util.spec_from_file_location('existing_house_pbr',HERE.parent/'hero_house_pbr_v1/attach_hero_house_retexture.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def points(objects):
 result={}
 for o in objects:
  o.data.calc_loop_triangles();result[o.name]=sorted(tuple(sorted(tuple(round(float(x),5) for x in o.matrix_world@o.data.vertices[i].co) for i in t.vertices)) for t in o.data.loop_triangles)
 return result
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
objects=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('HouseInterior_StorageChest') and o.name.endswith('_LOD0')]
assert len(objects)==3
for o in objects:o.parent=None
for o in list(bpy.data.objects):
 if o not in objects:bpy.data.objects.remove(o,do_unlink=True)
anchor=Vector((3.98,4.20,0));root=bpy.data.objects.new('HomeStorageChestPbr',None);bpy.context.scene.collection.objects.link(root)
for o in objects:o.location-=anchor;o.parent=root
bpy.context.view_layer.update();before=points(objects)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=.0174533,island_margin=.01);bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,scale=True,merge_overlap=False,margin_method='FRACTION',margin=.004);bpy.ops.object.mode_set(mode='OBJECT')
assert points(objects)==before
bpy.context.scene.tool_settings.use_uv_select_sync=False
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='DESELECT');bpy.ops.uv.select_overlap(extend=False)
overlap_count=0;overlap_details=[]
for o in objects:
 bm=bmesh.from_edit_mesh(o.data);uv=bm.loops.layers.uv.active
 selected=[f for f in bm.faces if any(loop[uv].select for loop in f.loops)]
 overlap_count+=len(selected)
 overlap_details.append({'node':o.name,'overlapping_faces':len(selected),'minimum_face_area':min([f.calc_area() for f in selected],default=None),'indices':[f.index for f in selected]})
bpy.ops.object.mode_set(mode='OBJECT')
(HERE/'uv-preflight.json').write_text(json.dumps({'overlap_count':overlap_count,'details':overlap_details},indent=2)+'\n')
assert overlap_count==0,'UV overlap blocks paid submission'
root.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(HERE/'source-storage-chest-uv.glb'),export_format='GLB',use_selection=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
bounds=a.object_bounds(objects)
r={'schema':'urman-home-storage-chest-pbr-source/v1','source':str(SOURCE.relative_to(ROOT)),'source_sha256':sha(SOURCE),'rights':'Project-original URMAN modular kit, assets/asset_registry.json','source_nodes':sorted(o.name for o in objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),'geometry_unchanged_during_UV':True,'actual_blender_uv_overlap_faces':overlap_count,'anchor_blender':list(anchor),'bounds_blender':bounds,'output':'source-storage-chest-uv.glb','output_sha256':sha(HERE/'source-storage-chest-uv.glb'),'consumer':'StyleBenchmarkInteriorFactory.AttachFurniture→GeneratedHouseInteriorAct1; StorageChest source(3.98,0,-4.20)→ChestAnchor(-.75,-.02,2.85),yaw180. Existing lid, storage interactions and collision owner remain unchanged.','remaining':'Generated source candidate only. Original LOD1 and actual game import/motion/art acceptance remain open.'}
(HERE/'source-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5;camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
a.render_view(scene,camera,focus,Vector((1,-1,.35)),max(high-low)*1.48,HERE/'source-front-right.png')
print('STORAGE_CHEST_SOURCE_READY',json.dumps(r))
