"""Two whole-family actual2KPBR views; no model export or material correction.

Archived prerequisites: evidence/PW-093/prologue-branch-material-source-v1-20261023.tar.xz (repository-relative).
Before replay extract f01-branch-whole-pbr-2k-v1.glb and 2k-receipt.json from the archive into this recipe folder. The render updates only local preview files and that restored historical receipt.
Extract named members with tar -xJf ARCHIVE -C THIS_RECIPE_FOLDER MEMBER... .
"""
from pathlib import Path
import bpy,json,hashlib
from mathutils import Vector
HERE=Path(__file__).resolve().parent;SOURCE=HERE/'f01-branch-whole-pbr-2k-v1.glb';R=HERE/'2k-receipt.json'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r=json.loads(R.read_text());assert sha(SOURCE)==r['output']['sha256']
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
for o in list(bpy.data.objects):
 world=o.matrix_world.copy()
 if o.animation_data:o.animation_data_clear();o.matrix_world=world
objects=[o for o in bpy.data.objects if o.type=='MESH'];assert len(objects)==3
bpy.context.view_layer.update();points=[o.matrix_world@v.co for o in objects for v in o.data.vertices];lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)));target=(lo+hi)*.5;extent=max(hi-lo)
world=bpy.data.worlds.new('SameSourceNeutralWorld');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.23,.24,.26,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
for direction,energy in [((-1,-2,3),350),((2,1,1),160)]:
 bpy.ops.object.light_add(type='AREA',location=target+Vector(direction)*extent*.7);bpy.context.object.data.energy=energy*extent*extent;bpy.context.object.data.size=extent*1.8
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.14;bpy.context.scene.camera=cam
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.render.resolution_x=1440;s.render.resolution_y=900;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.view_settings.view_transform='AgX';views=[]
for label,direction in [('three-quarter',(.35,-1,.27)),('rear',(-.35,1,.27))]:
 cam.location=target+Vector(direction).normalized()*extent*2.5;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(HERE/f'actual-pbr-{label}.png');bpy.ops.render.render(write_still=True);f=Path(s.render.filepath);views.append({'path':f.name,'sha256':sha(f),'bytes':f.stat().st_size,'direction':direction,'target':list(target),'orthographic_scale':extent*1.14})
r['actual_PBR_views']=views;r['status']='Source-only2KPBRactualviewsready_rootreviewpending;noart/nativePASS';R.write_text(json.dumps(r,indent=2)+'\n');print('F01_ACTUAL_PBR_VIEWS',json.dumps(views),flush=True)
