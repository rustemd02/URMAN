"""Append only MeshyPBR image bytes/bindings to exact authoritative gate donor.

Reproduction prerequisite (archive SHA is in compact candidate-receipt.json):
tar -xJf evidence/PW-093/babai-side-gate-source-v1.tar.xz \
  -C assets/source/blender/act1/babai_side_gate_meshy_v1 \
  babai-side-gate-wood-donor.glb meshy-returned.glb \
  source-receipt.json reference-receipt.json
Restore only absent inputs; verify member hashes in the archived full receipts.
This Blender recipe regenerates its full stage receipt; it refuses an existing
4K output. Root selects the compact provenance again after an accepted stage.
"""
from pathlib import Path
import bpy,json,hashlib,importlib.util,sys
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
DONOR=HERE/'babai-side-gate-wood-donor.glb';RETURNED=HERE/'meshy-returned.glb';OUT=HERE/'babai-side-gate-wood-pbr-v1.glb';RECEIPT=HERE/'candidate-receipt.json'
BINDER=HERE.parent/'rough_picket_family_reference_v3/pbr_candidate_v3/attach_maps.py'
spec=importlib.util.spec_from_file_location('existing_literal_pbr_binder',BINDER);binder=importlib.util.module_from_spec(spec);spec.loader.exec_module(binder)
binder.SOURCE=DONOR;binder.RETURNED=RETURNED;binder.GLB=OUT

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def meta(p):return {'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)}
report={'schema':'babai-side-gate-literal-donor-pbr/v1','status':'preparing'}
def write():RECEIPT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
try:
 assert not OUT.exists()
 assert sha(DONOR)=='aba31f8f9c832cca4e0d6924ba489d2660efc8592d8806e51ca62fb2d9685d34'
 assert sha(RETURNED)=='1eb05a7d1edfefbc6ca27f9674e7a16131adfeba6670047c01de0094ff729048'
 proof=binder.write_material_only();proof['changed_json_scope']='Only append returned image bytes/bufferViews/images/samplers/textures and attach generated PBR/normal bindings/factors within original8namedwoodmaterials. Originaldonor nodes/meshes/accessors/normals/UVs/transforms/BINprefix literalidentical. No returnedgeometry.'
 report.update({'source':meta(DONOR),'returned_maps_container':meta(RETURNED),'dependencies':[meta(BINDER),meta(HERE/'source-receipt.json'),meta(HERE/'reference-receipt.json')],'task':{'id':'01a169ce-1b21-7615-b1ad-090a25402579','type':'retexture','status':'SUCCEEDED','actual_consumed_credits':10,'post_count':1},'literal_byte_proof':proof,'output':meta(OUT),'material_policy':'Show original generatedPBR uncorrected on all8woodmembers. Keep source namedmaterialslots/indices; do not handrotate/flip/grain-repaint/tintmaproles. Hardwareabsent.','status':'literal_candidate_exported_actual_views_pending','remaining':'Root actualfront/yard/oblique materialreview, then later existinghinge/runtime/native/performance acceptance. No extraPOST or materialvariants.'});write()
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(OUT));objects=[o for o in bpy.data.objects if o.type=='MESH'];assert len(objects)==8
 total=0
 for ob in objects:ob.data.calc_loop_triangles();total+=len(ob.data.loop_triangles)
 assert total==96
 actual_overlap=binder.existing.p.actual_uv_overlap_count(objects);assert actual_overlap==0
 pts=[o.matrix_world@v.co for o in objects for v in o.data.vertices];lo=Vector(tuple(min(p[k] for p in pts) for k in range(3)));hi=Vector(tuple(max(p[k] for p in pts) for k in range(3)));target=(lo+hi)*.5;width=max((hi-lo).y,(hi-lo).z)*1.17
 scene=bpy.context.scene;world=bpy.data.worlds.new('SameSourceInspectionWorld');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.24,.24,.24,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
 for at in [(3,-2,4),(-3,2,3)]:
  bpy.ops.object.light_add(type='AREA',location=at);bpy.context.object.data.energy=230;bpy.context.object.data.size=3
 bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=width;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';views=[]
 for label,direction in [('front',(1,0,0)),('yard',(-1,0,0)),('oblique',(-1,-.30,.10))]:
  cam.location=target+Vector(direction).normalized()*3;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(HERE/f'actual-pbr-{label}.png');bpy.ops.render.render(write_still=True);f=Path(scene.render.filepath);views.append({**meta(f),'camera_direction':direction,'orthographic_scale':width,'same_camera_as_source':label in ('front','yard')})
 images=[]
 for im in bpy.data.images:
  if im.type=='IMAGE' and im.size[0]:images.append({'name':im.name,'size':list(im.size),'colorspace':im.colorspace_settings.name})
 report.update({'status':'literal_pbr_candidate_root_material_review_pending','actual_candidate_views':views,'actual_import':{'mesh_count':len(objects),'triangles':total,'UV_overlap_selected_faces':actual_overlap,'bounds_blender_z_up':[list(lo),list(hi)],'images':images},'additional_meshy_calls':0,'runtime_files_changed':False,'Blender_geometry_reexport':False});write();print('BABAI_PBR',json.dumps({'status':report['status'],'triangles':total,'UV_overlap':actual_overlap,'output_sha256':sha(OUT),'literal_geometry_bin_prefix':True,'views':len(views)}),flush=True)
except Exception as error:
 report.update({'status':'source_pbr_attachment_failed_stop','error':repr(error)});write();raise
