"""One read-only close3quarter view of root-approved canonicalF01 donor.

Archived prerequisites: evidence/PW-093/prologue-branch-material-source-v1-20261023.tar.xz (repository-relative).
The canonical input must still match060b8; its frozen byte-identical snapshot is archive member canonical-prologue-branch-v1.glb. Do not overwrite a later canonical asset when inspecting history.
Extract named members with tar -xJf ARCHIVE -C THIS_RECIPE_FOLDER MEMBER... .
"""
from pathlib import Path
import bpy,json,hashlib,struct,sys
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
SOURCE=ROOT/'game/assets/photoworlds/branch/prologue-branch-v1.glb';EXPECTED='060b8af9939754ba3f6de866b703430f6f22a6498733104c253b5ff451b1e742'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(SOURCE)==EXPECTED
raw=SOURCE.read_bytes();length,_=struct.unpack_from('<II',raw,12);doc=json.loads(raw[20:20+length]);assert len(doc['meshes'])==3 and len(doc['animations'])==3
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
# Keep defaultcanonicalblocking transforms, independent of imported animationtracks.
for o in list(bpy.data.objects):
 world=o.matrix_world.copy()
 if o.animation_data:o.animation_data_clear();o.matrix_world=world
objects=[o for o in bpy.data.objects if o.type=='MESH'];assert len(objects)==3
bpy.context.view_layer.update();points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)));target=(lo+hi)*.5;extent=max(hi-lo);counts=[]
for ob in objects:
 ob.data.calc_loop_triangles();counts.append({'node':ob.name,'triangles':len(ob.data.loop_triangles),'UV_layers':list(ob.data.uv_layers.keys())})
assert sum(x['triangles'] for x in counts)==5928
world=bpy.data.worlds.new('SourceNeutralWorld');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.23,.24,.26,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
for direction,energy in [((-1,-2,3),350),((2,1,1),160)]:
 bpy.ops.object.light_add(type='AREA',location=target+Vector(direction)*extent*.7);bpy.context.object.data.energy=energy*extent*extent;bpy.context.object.data.size=extent*1.8
bpy.ops.object.camera_add(location=target+Vector((.35,-1,.27)).normalized()*extent*2.5);cam=bpy.context.object;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.14;bpy.context.scene.camera=cam
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.render.resolution_x=1440;s.render.resolution_y=900;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.view_settings.view_transform='AgX';s.render.filepath=str(HERE/'actual-source-three-quarter.png');bpy.ops.render.render(write_still=True)
assert sha(SOURCE)==EXPECTED
receipt={'schema':'f01-whole-three-part-surface-source/v1','source':{'path':str(SOURCE.relative_to(ROOT)),'sha256':EXPECTED,'bytes':len(raw)},'scope':'Single read-only canonicaldonor sourceview. No source save/export/geometry/material/UV changes, no game/native capture. OriginalPBRmetricbark usesUV1; newgeneratedmaps must useUV0 while bothUVsets remainliteral.','geometry':{'meshes':counts,'triangles':5928,'UV0':'root-accepted57uniqueislands','UV1':'originalmetricrepeat, retained','clips':[a['name'] for a in doc['animations']],'bounds_blender_z_up':[list(lo),list(hi)]},'camera':{'direction':[.35,-1,.27],'target':list(target),'orthographic_scale':extent*1.14,'no_ground_or_other_geometry':True},'image':{'path':str(Path(s.render.filepath).relative_to(ROOT)),'sha256':sha(Path(s.render.filepath)),'bytes':Path(s.render.filepath).stat().st_size},'blender':bpy.app.version_string,'root_functional_geometry_review':'Approved for donorprep; lowerforkrailart and nativewholeforestdecision remainopen. Shape remainslocked duringmaterialstage.','next':'OnebuiltinImageGen materialreference in W01coldblueviolet originalbarkfamily; rootmustview before anypaidPOST10.','meshyposts':0,'new_spend':0}
(HERE/'source-receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');print('F01_SOURCE_VIEW',json.dumps(receipt['image']),flush=True)
