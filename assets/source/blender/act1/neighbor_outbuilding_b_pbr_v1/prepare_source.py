"""One exact existing VariantB outbuilding donor; UV/material changes only."""
from pathlib import Path
import bpy, json, hashlib, importlib.util, sys
from mathutils import Vector
sys.dont_write_bytecode = True
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
SOURCE=ROOT/'game/assets/models/act1/urman_village_exterior_kit.glb'
COMPONENT='VillageParcel_VariantB_PlasterAnnex_Outbuilding'
spec=importlib.util.spec_from_file_location('existing_house_studio', HERE.parent/'hero_house_pbr_v1/attach_hero_house_retexture.py')
studio=importlib.util.module_from_spec(spec);spec.loader.exec_module(studio)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def geometry(objects):
 out={}
 for ob in objects:
  ob.data.calc_loop_triangles()
  out[ob.name]={'triangles':len(ob.data.loop_triangles),'world_triangle_points':sorted(tuple(sorted(tuple(round(float(x),5) for x in ob.matrix_world@ob.data.vertices[i].co) for i in t.vertices)) for t in ob.data.loop_triangles)}
 return out
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
root=bpy.data.objects[COMPONENT]
kept={root,*root.children_recursive}
for ob in list(bpy.data.objects):
 if ob not in kept:bpy.data.objects.remove(ob,do_unlink=True)
root.parent=None;root.location=(0,0,0)
objects=[o for o in root.children_recursive if o.type=='MESH']
# Glass stays the separate original runtime owner; never paint it opaque.
glass=[o for o in objects if any('Window_' in m.name for m in o.data.materials if m)]
glass_names=[o.name for o in glass]
for o in glass:bpy.data.objects.remove(o,do_unlink=True)
objects=[o for o in root.children_recursive if o.type=='MESH']
bpy.context.view_layer.update()
before=geometry(objects)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.01)
bpy.ops.uv.pack_islands(rotate=True,margin=.004)
bpy.ops.object.mode_set(mode='OBJECT')
assert geometry(objects)==before
root.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(HERE/'source-opaque-uv.glb'),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
bounds=studio.object_bounds(objects)
report={'schema':'urman-neighbor-outbuilding-b-pbr/v1','source':str(SOURCE.relative_to(ROOT)),'source_sha256':digest(SOURCE),'source_rights':'Project-original, assets/asset_registry.json urman.act1.village_exterior_kit','component':COMPONENT,'excluded_original_glass':glass_names, 'opaque_nodes':len(objects),'triangles':sum(x['triangles'] for x in before.values()),'bounds_blender':bounds,'geometry_unchanged_during_uv':True,'mesh_names':sorted(before),'intended_operation':'Meshy retexture4KPBR, keepUV; maps-only attachment to this exact donor after geometry verification','runtime_promotion':False}
report['output_sha256']=digest(HERE/'source-opaque-uv.glb')
(HERE/'source-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5
camera=studio.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4
scene.render.resolution_x=1280;scene.render.resolution_y=960
studio.render_view(scene,camera,focus,Vector((1,-1,.55)),max(high-low)*1.5,HERE/'source-front-right.png')
print('SOURCE_READY',report['opaque_nodes'],report['triangles'],report['output_sha256'])
