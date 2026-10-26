"""Reuse existing literal GLB maps-only writer; never re-export fence geometry."""
from pathlib import Path
import bpy,json,hashlib,importlib.util,sys
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
WRITER=HERE.parent/'rough_picket_family_reference_v3/pbr_candidate_v3/attach_maps.py'
spec=importlib.util.spec_from_file_location('existing_literal_pbr_writer',WRITER)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.SOURCE=HERE/'painted-palisadnik-uv.glb'
# This derivative supplies resized2K PBR images only. Its Blender-exported
# geometry failed the rounded-world comparison and is never incorporated.
m.RETURNED=HERE/'painted-palisadnik-generated-pbr-2k.glb'
m.GLB=HERE/'painted-palisadnik-literal-pbr-2k.glb'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(m.SOURCE)=='b7eda17d8f0ba43635d7ef8a5f36c891a39218db925032d06c630501f75c0f15'
proof=m.write_material_only()
proof['changed_json_scope']='Append2K/1K generatedimage data and texture bindings to existing three named source materials; source nodes/meshes/accessors/normal/UV/index bytes remain literal.'
r={'schema':'urman-painted-palisadnik-literal-pbr/v1','status':'source_candidate_pending_art_review','task_id':'01a17a75-f06d-72b4-801e-27e80073bdb8','consumed_credits':10,'source':m.SOURCE.name,'source_sha256':sha(m.SOURCE),'image_donor':m.RETURNED.name,'image_donor_sha256':sha(m.RETURNED),'image_donor_geometry_incorporated':False,'literal_byte_proof':proof,'output':m.GLB.name,'output_sha256':sha(m.GLB),'output_bytes':m.GLB.stat().st_size,'runtime_integrated':False,'scope':'Nominal painted-palisadnik complete bay only. No gate/iron/interaction/boundary/collision/runtime modifications; scene/style/cultural/performance acceptance remains open.','remaining':'Root actual-view review and later whole-scene/batching/terrain/support decision. No new POST or automatic runtime promotion.'}
(HERE/'candidate-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(m.GLB))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in meshes:o.data.calc_loop_triangles()
assert len(meshes)==19 and sum(len(o.data.loop_triangles) for o in meshes)==532
bounds=m.a.object_bounds(meshes);low,high=map(Vector,bounds);focus=(low+high)*.5;camera=m.a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1440;scene.render.resolution_y=960
m.a.render_view(scene,camera,focus,Vector((.3,-1,.2)),max(high-low)*1.5,HERE/'candidate-front-right.png')
r.update(actual_import_nodes=19,actual_import_triangles=532,bounds_blender=bounds,actual_candidate_view='candidate-front-right.png')
(HERE/'candidate-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
print('PALISADNIK_LITERAL_READY',r['output_sha256'],r['output_bytes'])
