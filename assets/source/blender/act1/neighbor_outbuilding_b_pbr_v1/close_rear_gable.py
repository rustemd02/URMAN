"""Close only the confirmed missing rear gable; reuse front generated UV/PBR."""
from pathlib import Path
import bpy,bmesh,json,hashlib,importlib.util,sys
from mathutils import Vector,Matrix
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('existing_house_pbr',HERE.parent/'hero_house_pbr_v1/attach_hero_house_retexture.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
SOURCE=HERE/'variant-b-outbuilding-generated-pbr-2k.glb'
OUTPUT=HERE/'variant-b-outbuilding-generated-pbr-2k-closed-gable.glb'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
root=bpy.data.objects['VillageParcel_VariantB_PlasterAnnex_Outbuilding']
originals=[o for o in root.children_recursive if o.type=='MESH']
original_names=sorted(o.name for o in originals)
before=a.triangle_signatures(originals);bounds=a.object_bounds(originals)
front=bpy.data.objects['VariantB_Banya_GableFront_LOD0']
rear=front.copy();rear.data=front.data.copy();rear.name='VariantB_Banya_GableRear_LOD0'
bpy.context.scene.collection.objects.link(rear);rear.parent=root;rear.matrix_parent_inverse=Matrix.Identity(4);rear.matrix_basis=Matrix.Identity(4)
# Sample the existing roof underside at each rear cap plane. The source
# roof is deliberately skewed: merely mirroring the front triangle leaves
# a small wedge open below its shifted rear ridge. Close exactly to that
# underside, with 5mm visual overlap; all points remain in the roof envelope.
roof=bpy.data.objects['VariantB_Banya_Roof_LOD0']
roof_points=[roof.matrix_world@v.co for v in roof.data.vertices]
front_y=min(p.y for p in roof_points);back_y=max(p.y for p in roof_points)
def underside(y):
 profiles=[]
 for plane in (front_y,back_y):
  points=[p for p in roof_points if abs(p.y-plane)<1e-5]
  xs=sorted(set(round(p.x,5) for p in points))
  profiles.append([Vector((x,plane,min(p.z for p in points if abs(p.x-x)<1e-4))) for x in xs])
 assert all(len(profile)==3 for profile in profiles)
 t=(y-front_y)/(back_y-front_y)
 return [a.lerp(b,t) for a,b in zip(*profiles)]
for vertex in rear.data.vertices:
 p=front.matrix_world@vertex.co;y=-p.y
 section=underside(y)
 role=1 if p.z>3 else (0 if p.x<0 else 2)
 q=section[role]+Vector((0,0,.005))
 q.x=max(bounds[0][0]+1e-6,min(bounds[1][0]-1e-6,q.x))
 vertex.co=q
bm=bmesh.new();bm.from_mesh(rear.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(rear.data);bm.free();rear.data.update()
bpy.context.view_layer.update()
assert a.triangle_signatures(originals)==before
rear_bounds=a.object_bounds([rear]);assert rear_bounds[0][2]>=2.12-1e-5
assert rear_bounds[0][1]>=1.22-1e-5 and rear_bounds[1][1]<=1.42+1e-5
assert a.object_bounds([*originals,rear])==bounds
for o in bpy.context.scene.objects:o.select_set(False)
root.select_set(True)
for o in [*originals,rear]:o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.gltf(filepath=str(OUTPUT),export_format='GLB',use_selection=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
# Validate actual exported source result rather than the in-memory plan.
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(OUTPUT))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];actual_originals=[o for o in meshes if o.name in original_names];actual_rear=bpy.data.objects['VariantB_Banya_GableRear_LOD0']
after=a.triangle_signatures(actual_originals)
assert len(actual_originals)==18 and a.counter_delta(before['paired'],after['paired'])['exact']
assert a.object_bounds(meshes)==bounds
actual_rear.data.calc_loop_triangles()
report={'schema':'urman-variant-b-outbuilding-closed-gable/v1','source':SOURCE.name,'source_sha256':sha(SOURCE),'output':OUTPUT.name,'output_sha256':sha(OUTPUT),'output_bytes':OUTPUT.stat().st_size,'confirmed_cause':'Existing author_variant_shed_b creates only GableFront; no rear gable. RearWindow is at1.14–1.70m, below this closure.','scope':'Added rear visual gable only; existing18opaque nodes and444triangles preserve paired world-position/UV signatures; no opening/collision/placement changes.','original_paired_triangle_comparison':a.counter_delta(before['paired'],after['paired']),'comparison_precision':{'world_position_decimals':5,'uv_decimals':7},'rear_gable_name':actual_rear.name,'rear_gable_triangles':len(actual_rear.data.loop_triangles),'rear_gable_bounds_blender':a.object_bounds([actual_rear]),'whole_bounds_blender':bounds,'total_mesh_nodes':len(meshes),'total_triangles':sum(len(o.data.loop_triangles) for o in meshes),'material':'Existing generated front-gable atlas UV and PBR reused; no additional bitmap or Meshy spend.','runtime_integrated':False,'remaining':'Root callsite and asset import; native scene/motion/art acceptance open.'}
(HERE/'closed-gable-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5;camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
a.render_view(scene,camera,focus,Vector((-1,1,.5)),max(high-low)*1.5,HERE/'closed-gable-rear-left.png')
print('CLOSED_GABLE_READY',json.dumps(report))
