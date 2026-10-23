"""Exact eight runtime gate wood boxes; new donor-only unique UV, no hardware."""
from pathlib import Path
import bpy, json, hashlib, math, sys, importlib.util
from mathutils import Vector, Matrix
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
OWNER=ROOT/'game/scripts/Act1ConnectedWorld.BabaiYardSideGate.cs'
BOXOWNER=ROOT/'game/scripts/RuralPropGeometry.cs'
ATLAS=HERE.parent/'neighbor_house_a_retexture_v2/prepare_variant_a_retexture.py'
spec=importlib.util.spec_from_file_location('existing_atlas_receipt_helpers',ATLAS);p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)
GLB=HERE/'babai-side-gate-wood-donor.glb';BLEND=HERE/'babai-side-gate-wood-donor.blend';RECEIPT=HERE/'source-receipt.json'
report={'schema':'babai-side-gate-exact-eight-wood-parts/v1','status':'preparing','scope':'New source-only donor; exact existing 8 BoxMesh solid geometries and hinge-local transforms; new unique UV permitted by root. No hardware/collider/hinge state/runtime changes.'}
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
def write():RECEIPT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
def meta(f):return {'path':str(f.relative_to(ROOT)),'bytes':f.stat().st_size,'sha256':sha(f)}
# Proper Y-up to Z-up rotation, with no reflection, scale or root offset.
C=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
parts=[]
tones=['6f5440','7a5d45','65503d','735842','6a523e'];tops=[1.08,1.04,1.09,1.02,1.06]
for i,(tone,top) in enumerate(zip(tones,tops)):
 parts.append({'name':f'LooseBoardPlank{i}','size':[.03,top-.04,.235],'at':[0,.04+(top-.04)*.5,.1275+i*.2813],'rotation_x':0,'tone':tone,'role':'upright-board'})
for name,y in [('LooseBoardBattenLow',.24),('LooseBoardBattenHigh',.84)]:
 parts.append({'name':name,'size':[.035,.09,1.30],'at':[-.032,y,.69],'rotation_x':0,'tone':'8a6b50','role':'bearing-batten'})
parts.append({'name':'LooseBoardBrace','size':[.03,.08,1.27],'at':[-.032,.54,.69],'rotation_x':-math.atan2(.60,1.12),'tone':'7d6249','role':'diagonal-brace'})
bpy.ops.wm.read_factory_settings(use_empty=True)
root=bpy.data.objects.new('BabaiSideGateWoodLeaf',None);bpy.context.collection.objects.link(root)
meshes=[];islands=[]
try:
 for i,r in enumerate(parts):
  sx,sy,sz=(x*.5 for x in r['size'])
  runtime=[(-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),(-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)]
  verts=[C.to_3x3()@Vector(v) for v in runtime]
  faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
  mesh=bpy.data.meshes.new(r['name']+'Mesh');mesh.from_pydata(verts,[],faces);mesh.update();uv=mesh.uv_layers.new(name='UVMap')
  ob=bpy.data.objects.new(r['name'],mesh);bpy.context.collection.objects.link(ob);ob.parent=root
  matrix=Matrix.Rotation(r['rotation_x'],4,'X');matrix.translation=Vector(r['at']);ob.matrix_local=C@matrix@C.inverted()
  dims=[r['size'][0],r['size'][2],r['size'][1]];long_axis=max(range(3),key=lambda k:dims[k])
  for face in mesh.polygons:
   face_axis=max(range(3),key=lambda k:abs(face.normal[k]));axes=[k for k in range(3) if k!=face_axis]
   vaxis=long_axis if long_axis in axes else max(axes,key=lambda k:dims[k]);uaxis=next(k for k in axes if k!=vaxis)
   index=i*6+face.index;col=index%8;row=index//8;cw=1/8;ch=1/6;pad=.002
   factor=min((cw-2*pad)/dims[uaxis],(ch-2*pad)/dims[vaxis]);uw=dims[uaxis]*factor;vh=dims[vaxis]*factor
   centre=((col+.5)*cw,(row+.5)*ch)
   for li in face.loop_indices:
    point=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(centre[0]+point[uaxis]*factor,centre[1]+point[vaxis]*factor)
   islands.append({'part':r['name'],'face':face.index,'grid_cell':index,'u_axis_blender':uaxis,'v_grain_axis_blender':vaxis,'rect':[[centre[0]-uw*.5,centre[1]-vh*.5],[centre[0]+uw*.5,centre[1]+vh*.5]],'pad_global_uv':pad})
  mat=bpy.data.materials.new('OriginalWood_'+r['name']);mat.use_nodes=True
  rgb=[int(r['tone'][j:j+2],16)/255 for j in (0,2,4)];linear=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in rgb]
  mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*linear,1);mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85;mesh.materials.append(mat)
  meshes.append(ob)
 bpy.context.view_layer.update()
 before=p.actual_uv_overlap_count(meshes)
 expected={o.name:(p.triangle_signature(o,True),[list(x) for x in o.matrix_world]) for o in meshes}
 assert before==0,('source UV overlap',before)
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for o in meshes:o.select_set(True)
 bpy.context.view_layer.objects.active=meshes[0]
 bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
 bpy.ops.export_scene.gltf(filepath=str(GLB),export_format='GLB',use_selection=True,export_apply=False,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_animations=False)
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(GLB));actual=[o for o in bpy.data.objects if o.type=='MESH']
 assert len(actual)==8
 max_matrix_error=0
 for o in actual:
  old_sig,old_matrix=expected[o.name];assert old_sig==p.triangle_signature(o,True),o.name
  max_matrix_error=max(max_matrix_error,max(abs(float(o.matrix_world[j][k])-old_matrix[j][k]) for j in range(4) for k in range(4)))
  o.data.calc_loop_triangles();assert len(o.data.loop_triangles)==12
 after=p.actual_uv_overlap_count(actual);assert after==0 and max_matrix_error<1e-6
 pts=[o.matrix_world@v.co for o in actual for v in o.data.vertices]
 lo=Vector(tuple(min(q[k] for q in pts) for k in range(3)));hi=Vector(tuple(max(q[k] for q in pts) for k in range(3)))
 for r in parts:r['runtime_basis']='X rotation only; original exact C# brace angle, otherwise identity'
 report.update({'source_owners':[meta(OWNER),meta(BOXOWNER)],'helper':meta(ATLAS),'source_kind':'Reconstructed directly from current C# dimensions/positions/rotations. Not a native runtime export/frame. No bevel, deformation, topology reduction or automatic UV repack.','parts':parts,'mesh_count':8,'triangles':96,'hardware_excluded':['LooseBoardHingePin (sibling under gate)','LooseBoardLatch (under existing hinge)'],'hinge_contract':'Donor origin remains existing LooseBoardHinge origin; no root recentering/height normalization. Godot width+Z,up+Y,thicknessX. Root alone integrates visual; existing pivot/hardware/colliders/state kept.','uv_contract':{'new_donor_uv_not_original_BoxMesh_uv':True,'method':'48 explicitly disjoint face rectangles in an8×6 grid; physical face aspect retained. Side-face V follows each timber member long axis; cap faces use remaining local cross-section axes. All8parts share one global atlas.','islands':islands,'overlap_selected_faces_before':before,'overlap_selected_faces_after':after,'loop_tri_geometry_uv_signature_identical_after_export':True,'max_transform_roundtrip_error':max_matrix_error},'bounds_blender_z_up':[list(lo),list(hi)],'new_atlas_uv_runtime_mapping':'Future generated visual uses literal new donor UVs. It does not assert compatibility with old overlapping BoxMeshUVs.','outputs':[meta(GLB),meta(BLEND)],'status':'source_export_verified_root_view_pending','spend':0,'future_maps_only_boundary':'Only append returned texture image bytes/bindings to authoritative donor GLB; keep original donor BIN bytes/nodes/accessors/UVs/normals/transforms/material ownership. Never import returnedMeshygeometry into candidate.'})
 target=(lo+hi)*.5;width=max((hi-lo).y,(hi-lo).z)*1.17
 scene=bpy.context.scene;world=bpy.data.worlds.new('InspectionWorld');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.24,.24,.24,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.75
 for at in [(3,-2,4),(-3,2,3)]:
  bpy.ops.object.light_add(type='AREA',location=at);bpy.context.object.data.energy=230;bpy.context.object.data.size=3
 bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=width;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';views=[]
 for label,direction in [('front',(1,0,0)),('yard',(-1,0,0))]:
  cam.location=target+Vector(direction)*3;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(HERE/f'actual-source-{label}.png');bpy.ops.render.render(write_still=True);f=Path(scene.render.filepath);views.append({**meta(f),'camera_direction':direction,'orthographic_scale':width})
 report['actual_source_views']=views;write();print('BABAI_SOURCE',json.dumps({'status':report['status'],'glb_sha256':sha(GLB),'triangles':96,'UV_overlap':[before,after],'matrix_error':max_matrix_error}),flush=True)
except Exception as e:
 report['status']='source_preparation_failed_stop';report['error']=repr(e);write();raise
