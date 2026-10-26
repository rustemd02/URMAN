"""Standalone VariantC art donor. Close only the confirmed roof support gap."""
from pathlib import Path
import bpy,bmesh,json,hashlib,importlib.util,sys
from mathutils import Vector
from mathutils.geometry import intersect_ray_tri
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
SOURCE=ROOT/'game/assets/models/act1/urman_village_exterior_kit.glb'
COMPONENT='VillageParcel_VariantC_BanyaYard_Outbuilding'
spec=importlib.util.spec_from_file_location('existing_house_pbr',HERE.parent/'hero_house_pbr_v1/attach_hero_house_retexture.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def geometry(objects):
 result={}
 for o in objects:
  o.data.calc_loop_triangles()
  result[o.name]=sorted(tuple(sorted(tuple(round(float(x),6) for x in o.matrix_world@o.data.vertices[i].co) for i in t.vertices)) for t in o.data.loop_triangles)
 return result
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
root=bpy.data.objects[COMPONENT];keep={root,*root.children_recursive}
for o in list(bpy.data.objects):
 if o not in keep:bpy.data.objects.remove(o,do_unlink=True)
root.parent=None;root.location=(0,0,0);bpy.context.view_layer.update()
objects=[o for o in root.children_recursive if o.type=='MESH'];assert len(objects)==10
wall=bpy.data.objects['VariantC_Utility_Wall_LOD0'];roof=bpy.data.objects['VariantC_Utility_Roof_LOD0']
others=[o for o in objects if o!=wall];unchanged=geometry(others)
transforms={o.name:[list(row) for row in o.matrix_local] for o in objects}
bounds=a.object_bounds(objects)
roof.data.calc_loop_triangles();triangles=[tuple(roof.matrix_world@roof.data.vertices[i].co for i in t.vertices) for t in roof.data.loop_triangles]
original_vertices=[v.co.copy() for v in wall.data.vertices];top=max(v.z for v in original_vertices)
assert abs(top-1.96)<1e-5
contacts=[];lower_ids=[];inverse=wall.matrix_world.inverted()
for v,old in zip(wall.data.vertices,original_vertices):
 if abs(old.z-top)>1e-5:lower_ids.append(v.index);continue
 world=wall.matrix_world@old
 hits=[hit for tri in triangles if (hit:=intersect_ray_tri(*tri,Vector((0,0,1)),Vector((world.x,world.y,-10)),True)) is not None]
 assert hits,'Every moved top vertex must be inside the actual roof footprint'
 underside=min(hits,key=lambda hit:hit.z)
 assert .02<underside.z-world.z<.5
 v.co=inverse@underside
 assert abs(v.co.x-old.x)<1e-6 and abs(v.co.y-old.y)<1e-6
 contacts.append({'vertex':v.index,'before':list(old),'after':list(v.co),'actual_roof_underside':list(underside)})
assert contacts and all(wall.data.vertices[i].co==original_vertices[i] for i in lower_ids)
# Only this wall's altered face normals are rebuilt; other source meshes stay exact.
wall.data.normals_split_custom_set([(0,0,0)]*len(wall.data.loops))
for polygon in wall.data.polygons:polygon.use_smooth=False
wall.data.update();bpy.context.view_layer.update()
assert geometry(others)==unchanged and a.object_bounds(objects)==bounds
assert all([list(row) for row in o.matrix_local]==transforms[o.name] for o in objects)
fixed=geometry(objects)
# One degree separates bevel faces instead of wrapping incompatible slivers.
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=.0174533,island_margin=.004)
bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,scale=True,merge_overlap=False,margin_method='FRACTION',margin=.004)
bpy.ops.object.mode_set(mode='OBJECT');assert geometry(objects)==fixed
bpy.context.scene.tool_settings.use_uv_select_sync=False
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='DESELECT');bpy.ops.uv.select_overlap(extend=False)
overlap={}
for o in objects:
 bm=bmesh.from_edit_mesh(o.data);uv=bm.loops.layers.uv.active;selected=[f.index for f in bm.faces if any(loop[uv].select for loop in f.loops)]
 if selected:overlap[o.name]=selected
bpy.ops.object.mode_set(mode='OBJECT')
report={'schema':'urman-variant-c-outbuilding-art-source/v1','status':'UV_HOLD' if overlap else 'source_prepared','source':str(SOURCE.relative_to(ROOT)),'source_sha256':sha(SOURCE),'rights':'Project-original URMAN kit per assets/asset_registry.json','component':COMPONENT,'source_meshes':len(objects),'source_triangles':sum(len(o.data.loop_triangles) for o in objects),'wall_top_contacts':contacts,'wall_lower_vertices_preserved':len(lower_ids),'unaffected_meshes_exact':list(unchanged),'all_transforms_preserved':True,'whole_bounds_blender':bounds,'UV_overlap_faces':overlap,'scope':'Separate art candidate only. Original kit, source Blend, runtime, original collision meshes, OpenBay, door, threshold, repairbench and bypass untouched. No claim about engine LOD/ShadowMesh preservation or native acceptance.','canon':'Current Kara-Urman,Tatarstan,winter2026; ordinary working utility lean-to, not a documented ethnic antique. No invented carving/script/religious motifs.','family_consumers':'10addressed VariantC parcels in act1_open_part plus existing connective kit. Source node names stay exact; root owns all future integration.'}
(HERE/'source-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
assert not overlap,'UV overlap blocks further generation and paid submission'
root.select_set(True)
output=HERE/'variant-c-support-fixed-uv.glb'
bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
# Inspect actual delivered geometry and contact after roundtrip.
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(output))
actual=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert geometry(actual)==fixed and a.object_bounds(actual)==bounds
report.update(status='source_preflight_passed',output=output.name,output_sha256=sha(output),actual_reimport_geometry_equal=True,actual_reimport_nodes=len(actual))
(HERE/'source-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5;camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
a.render_view(scene,camera,focus,Vector((1,-1,.4)),max(high-low)*1.45,HERE/'source-front-right.png')
print('VARIANT_C_SOURCE_READY',report['output_sha256'],report['source_triangles'],len(contacts))
