"""Nominal complete painted-palisadnik bay from existing runtime dimensions."""
from pathlib import Path
import bpy,bmesh,math,json,hashlib,importlib.util,sys
from mathutils import Matrix,Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
RUNTIME=ROOT/'game/scripts/Act1ConnectedWorld.YardFences.cs'
spec=importlib.util.spec_from_file_location('existing_fence_source',HERE.parent/'rough_picket_family_reference_v3/prepare_source.py')
k=importlib.util.module_from_spec(spec);spec.loader.exec_module(k)
k.HERE=HERE;k.meshes=[];k.members={}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def geometry(objects):
 for o in objects:o.data.calc_loop_triangles()
 return {o.name:sorted(tuple(sorted(tuple(round(float(x),5) for x in o.matrix_world@o.data.vertices[i].co) for i in t.vertices)) for t in o.data.loop_triangles) for o in objects}
bpy.ops.wm.read_factory_settings(use_empty=True)
root=k.empty('PaintedPalisadnik_Bay210',None)
basis=Matrix.Rotation(math.pi/2,3,'Y');length=2.10;height=1.10;foot=-.015;lean=math.radians(.8);paint='7f9a74'
def point(x,y):return Vector((x+y*math.tan(lean),foot+y,0))
rise=height+.11-foot
for i,x in enumerate((0,length)):
 k.box(f'Palisadnik_Post_{i}',root,root,(.11,rise/math.cos(lean),.11),point(x,rise*.5),basis@Matrix.Rotation(lean,3,'X'),paint,role='existing tilted timber post')
 k.box(f'Palisadnik_PostCap_{i}',root,root,(.15,.045,.15),point(x,rise+.0225),basis,'e6dfc6',role='existing pale post cap')
for i,y in enumerate((.26,height-.21)):
 first,last=point(0,y),point(length,y)
 k.box(f'Palisadnik_BearingRail_{i}',root,root,(.04,.07,(last-first).length),(first+last)*.5,basis,paint,role='existing two bearing rails')
count=math.floor(length/.17);assert count==12
for i in range(count):
 t=(i+.5)/count;x=t*length;top=height-.16*math.sin(t*math.pi);bottom=foot+.26-.07*.5-.01
 k.box(f'Palisadnik_Picket_{i:02d}',root,root,(.026,top-bottom,.095),(x,(top+bottom)*.5,0),basis,paint,role='existing scalloped-height vertical timber board')
first,last=point(0,height),point(length,height)
k.box('Palisadnik_CapRail',root,root,(.055,.06,length),(first+last)*.5,basis,'e3d6b8',role='existing straight pale cap rail')
k.colour_materials();objects=k.meshes
assert len(objects)==19
for o in objects:
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.calc_loop_triangles()
local_vertices_before={o.name:tuple(tuple(v.co) for v in o.data.vertices) for o in objects}
before=geometry(objects);transforms_before={o.name:[list(r) for r in o.matrix_world] for o in objects};bounds=k.a.object_bounds(objects)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=.0174533,island_margin=.004);bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,scale=True,merge_overlap=False,margin_method='FRACTION',margin=.004);bpy.ops.object.mode_set(mode='OBJECT')
assert all(tuple(tuple(v.co) for v in o.data.vertices)==local_vertices_before[o.name] for o in objects)
transform_delta=max(abs(a-b) for o in objects for old,new in zip(transforms_before[o.name],o.matrix_world) for a,b in zip(old,new))
assert transform_delta<1e-6
after_uv=geometry(objects)
if after_uv!=before:
 diagnostic={'state':'UV_GEOMETRY_HOLD','nodes':{o.name:{'before_count':len(before[o.name]),'after_count':len(after_uv[o.name]),'before_first':before[o.name][:2],'after_first':after_uv[o.name][:2],'before_transform':transforms_before[o.name],'after_transform':[list(r) for r in o.matrix_world]} for o in objects if before[o.name]!=after_uv[o.name]}}
 (HERE/'source-preflight-hold.json').write_text(json.dumps(diagnostic,indent=2)+'\n')
 raise RuntimeError('UV geometry invariant failed; diagnostic saved, no paid submission')
bpy.context.scene.tool_settings.use_uv_select_sync=False
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='DESELECT');bpy.ops.uv.select_overlap(extend=False)
overlap={}
for o in objects:
 bm=bmesh.from_edit_mesh(o.data);uv=bm.loops.layers.uv.active;selected=[f.index for f in bm.faces if any(loop[uv].select for loop in f.loops)]
 if selected:overlap[o.name]=selected
bpy.ops.object.mode_set(mode='OBJECT')
r={'schema':'urman-painted-palisadnik-source/v1','status':'UV_HOLD' if overlap else 'source_prepared','runtime_source':str(RUNTIME.relative_to(ROOT)),'runtime_source_sha256':sha(RUNTIME),'source_helper':'assets/source/blender/act1/rough_picket_family_reference_v3/prepare_source.py','source_helper_sha256':sha(HERE.parent/'rough_picket_family_reference_v3/prepare_source.py'),'source_rights':'Project-original runtime/Blender geometry, existing box primitive. No third-party mesh.','family':'painted-palisadnik','runtime_consumer':'RebuildYardFences→StreetDesigns[1]→BuildTimberFenceRun; street-frontage designs distributed in street order. This is a nominal flat2.10m bay, not a captured live lot.','nominal_metrics':{'length':length,'height':height,'posts':2,'pickets':count,'picket_pitch':length/count,'clear_gap':length/count-.095,'runtime_original_picket_bottom':.305,'candidate_picket_bottom':.20,'source_only_adjustment':'Extend12lowerboardends105mm so they overlap bothbearingrails; source-only, not a runtime change.','picket_width':.095,'picket_thickness':.026,'board_top':'1.10-.16*sin(pi*t)','bearing_rails':[.26,.89],'cap_rail':True,'post_lean_degrees':.8,'post_foot':foot},'parity_limits':['Flat nominal support datum; actual runtime terrain/run lengths remain root responsibility.','Existing source box helper adds2mm arris inside the runtime member envelope.','Source member objects reconstruct existing batched primitives; future integration must preserve batching/terrain/boundaries.','No gate/wicket/iron parts or interaction semantics are replaced.','No claim of historically documented local construction; current project Kara-Urman,Tatarstan,winter2026. Low painted street fence follows selectedT1 property, not invented ethnic ornament.'],'triangulation':'Source primitive caps/quads explicitly triangulated before UV so exporter cannot switch coplanar diagonals; no vertex or member-envelope changes.','mesh_nodes':len(objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),'UV_overlap_faces':overlap,'local_vertices_exact_during_UV':True,'matrix_roundtrip_max_abs_delta':transform_delta,'world_geometry_decimal_places':5,'bounds_blender':bounds,'members':k.members,'runtime_integrated':False}
(HERE/'source-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
assert not overlap,'UV overlap blocks further generation'
root.select_set(True)
output=HERE/'painted-palisadnik-uv.glb';bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(output));actual=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in actual:o.data.calc_loop_triangles()
assert geometry(actual)==before
actual_bounds=k.a.object_bounds(actual)
bounds_error=max(abs(a-b) for old,new in zip(bounds,actual_bounds) for a,b in zip(old,new));assert bounds_error<1e-5
r['actual_bounds_roundtrip_max_abs_delta']=bounds_error
r.update(status='source_preflight_passed',output=output.name,output_sha256=sha(output),actual_reimport_geometry_equal=True)
(HERE/'source-receipt.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5;camera=k.a.create_studio(bpy.context.scene,focus,bounds);scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1440;scene.render.resolution_y=960
k.a.render_view(scene,camera,focus,Vector((.30,-1,.20)),max(high-low)*1.5,HERE/'source-front-right.png')
print('PALISADNIK_SOURCE_READY',r['triangles'],r['output_sha256'])
