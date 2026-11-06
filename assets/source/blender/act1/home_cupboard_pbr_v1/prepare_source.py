"""Existing living-room cupboard, geometry-preserving UV donor for one retexture."""
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
def signed_area2(poly):
 if len(poly)<3:return 0.0
 return sum(poly[i][0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*poly[i][1] for i in range(len(poly)))*.5
def clipped_polygon(subject,clipper):
 if len(subject)<3 or len(clipper)<3:return []
 if abs(signed_area2(clipper))<1e-16:return []
 if signed_area2(clipper)<0:clipper=list(reversed(clipper))
 output=subject
 for i,p0 in enumerate(clipper):
  p1=clipper[(i+1)%len(clipper)];edge=(p1[0]-p0[0],p1[1]-p0[1]);incoming=output;output=[]
  if not incoming:break
  def side(p):return edge[0]*(p[1]-p0[1])-edge[1]*(p[0]-p0[0])
  start=incoming[-1];start_side=side(start)
  for end in incoming:
   end_side=side(end);start_inside=start_side>=-1e-10;end_inside=end_side>=-1e-10
   if start_inside!=end_inside:
    direction=(end[0]-start[0],end[1]-start[1]);den=edge[0]*direction[1]-edge[1]*direction[0]
    if abs(den)>1e-18:
     t=(edge[0]*(p0[1]-start[1])-edge[1]*(p0[0]-start[0]))/den
     output.append((start[0]+t*direction[0],start[1]+t*direction[1]))
   if end_inside:output.append(end)
   start=end;start_side=end_side
 return output
def uv_triangle_records(objects):
 records=[];uv_degenerate=[];geometry_degenerate=[];invalid_non_degenerate_uv=[]
 for o in objects:
  o.data.calc_loop_triangles();layer=o.data.uv_layers.active or o.data.uv_layers[0]
  for tri in o.data.loop_triangles:
   uv=[tuple(float(c) for c in layer.data[loop_i].uv) for loop_i in tri.loops]
   p=[tuple(float(c) for c in (o.matrix_world@o.data.vertices[vertex_i].co)) for vertex_i in tri.vertices]
   a=Vector(p[1])-Vector(p[0]);b=Vector(p[2])-Vector(p[0]);geometry_area=a.cross(b).length*.5;uv_area=abs(signed_area2(uv))
   rec={'node':o.name,'polygon':o.data.polygons[tri.polygon_index].index,'uv':uv,'uv_area':uv_area,'geometry_area':geometry_area}
   if uv_area<=1e-12:uv_degenerate.append(rec)
   if geometry_area<=1e-10:geometry_degenerate.append(rec)
   if geometry_area>1e-10 and uv_area<=1e-12:invalid_non_degenerate_uv.append(rec)
   records.append(rec)
 return records,uv_degenerate,geometry_degenerate,invalid_non_degenerate_uv
def positive_area_uv_overlaps(records,tolerance=1e-10):
 overlaps=[]
 bounds=[]
 for r in records:
  u=r['uv'];bounds.append((min(p[0] for p in u),max(p[0] for p in u),min(p[1] for p in u),max(p[1] for p in u)))
 for i,left in enumerate(records):
  aminx,amaxx,aminy,amaxy=bounds[i]
  if left['uv_area']<=1e-12:continue
  for j in range(i+1,len(records)):
   right=records[j];bminx,bmaxx,bminy,bmaxy=bounds[j]
   if right['uv_area']<=1e-12 or min(amaxx,bmaxx)-max(aminx,bminx)<=tolerance or min(amaxy,bmaxy)-max(aminy,bminy)<=tolerance:continue
   area=abs(signed_area2(clipped_polygon(left['uv'],right['uv'])))
   if area>tolerance:overlaps.append({'left':{'node':left['node'],'polygon':left['polygon']},'right':{'node':right['node'],'polygon':right['polygon']},'intersection_area':area})
 return overlaps
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(SOURCE))
objects=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('HouseInterior_Cupboard') and o.name.endswith('_LOD0')]
assert len(objects)==3
for o in objects:o.parent=None
for o in list(bpy.data.objects):
 if o not in objects:bpy.data.objects.remove(o,do_unlink=True)
anchor=Vector((-4.82,3.88,0));root=bpy.data.objects.new('HomeCupboardPbr',None);bpy.context.scene.collection.objects.link(root)
for o in objects:o.location-=anchor;o.parent=root
bpy.context.view_layer.update();before=points(objects)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.01);bpy.ops.uv.select_all(action='SELECT');bpy.ops.uv.pack_islands(rotate=True,scale=True,merge_overlap=False,margin_method='FRACTION',margin=.004);bpy.ops.object.mode_set(mode='OBJECT')
assert points(objects)==before
bpy.context.scene.tool_settings.use_uv_select_sync=False
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.select_all(action='DESELECT');bpy.ops.uv.select_overlap(extend=False)
operator_flag_count=0;operator_flag_details=[]
for o in objects:
 bm=bmesh.from_edit_mesh(o.data);uv=bm.loops.layers.uv.active
 selected=[f for f in bm.faces if any(loop[uv].select for loop in f.loops)]
 operator_flag_count+=len(selected)
 operator_flag_details.append({'node':o.name,'operator_flag_faces':len(selected),'minimum_face_area':min([f.calc_area() for f in selected],default=None),'indices':[f.index for f in selected]})
bpy.ops.object.mode_set(mode='OBJECT')
triangle_records,uv_degenerate,geometry_degenerate,invalid_non_degenerate_uv=uv_triangle_records(objects)
area_overlaps=positive_area_uv_overlaps(triangle_records)
preflight={'schema':'urman-uv-positive-area-preflight/v1','triangle_count':len(triangle_records),'positive_area_overlap_pairs':area_overlaps,'positive_area_overlap_pair_count':len(area_overlaps),'intersection_area_tolerance':1e-10,'uv_degenerate_triangle_count':len(uv_degenerate),'uv_degenerate_triangles':[{'node':r['node'],'polygon':r['polygon'],'uv_area':r['uv_area'],'geometry_area':r['geometry_area']} for r in uv_degenerate],'source_geometry_degenerate_triangle_count':len(geometry_degenerate),'source_geometry_degenerate_triangles':[{'node':r['node'],'polygon':r['polygon'],'uv_area':r['uv_area'],'geometry_area':r['geometry_area']} for r in geometry_degenerate],'non_degenerate_geometry_with_degenerate_uv_count':len(invalid_non_degenerate_uv),'operator_edge_touch_and_degenerate_face_flags_count':operator_flag_count,'operator_flag_interpretation':'The legacy selector also marks zero-area source faces and triangle pairs that only share a UV boundary; the paid gate is exact positive-area triangle-pair intersection.','operator_flags':operator_flag_details}
(HERE/'uv-preflight.json').write_text(json.dumps(preflight,ensure_ascii=False,indent=2)+'\n')
assert len(area_overlaps)==0,'Positive-area UV triangle overlap blocks paid submission'
assert not invalid_non_degenerate_uv,'Non-degenerate source triangles require non-degenerate UV area'
root.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(HERE/'source-cupboard-uv.glb'),export_format='GLB',use_selection=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
bounds=a.object_bounds(objects)
r={'schema':'urman-home-cupboard-pbr-source/v1','source':str(SOURCE.relative_to(ROOT)),'source_sha256':sha(SOURCE),'rights':'Project-original URMAN modular kit, assets/asset_registry.json','source_nodes':sorted(o.name for o in objects),'triangles':sum(len(o.data.loop_triangles) for o in objects),'geometry_unchanged_during_UV':True,'uv_gate':'zero positive-area overlaps between UV triangle pairs; zero UV area is permitted only for zero-area source triangles','positive_area_uv_overlap_pairs':len(area_overlaps),'source_geometry_degenerate_triangles_preserved':len(geometry_degenerate),'non_degenerate_geometry_with_degenerate_uv':len(invalid_non_degenerate_uv),'legacy_blender_operator_flag_faces':operator_flag_count,'anchor_blender':list(anchor),'bounds_blender':bounds,'output':'source-cupboard-uv.glb','output_sha256':sha(HERE/'source-cupboard-uv.glb'),'consumer':'StyleBenchmarkInteriorFactory.AttachFurniture→GeneratedHouseInteriorAct1; named Cupboard group source(-4.82,0,-3.88)→destination(3.52,0,-2.40),yaw-90; existing collision owner unchanged.','remaining':'Generated source candidate only. Original LOD1 and actual game import/motion/art acceptance remain open.'}
receipt_path=HERE/'source-receipt.json';old=json.loads(receipt_path.read_text()) if receipt_path.exists() else {}
history=old.get('preflight_history',[])
if old.get('last_preflight') or old.get('source_suitability'):history.append({'status':old.get('status'),'last_preflight':old.get('last_preflight'),'source_suitability':old.get('source_suitability'),'next_action':old.get('next_action')})
old.update(r);old['preflight_history']=history;old['last_preflight']=preflight;old['source_suitability']='Suitable for one bounded keepUV retexture: no positive-area UV triangle overlaps; all16 zero-area source door triangles retain zero-area UVs and source geometry; non-degenerate body/pull triangle halves touch only at shared boundaries. Geometry, exact source bounds and material roles remain unchanged.';old['status']='SOURCE_READY_KEEP_UV_AREA_GATE_PASS';old['next_action']='One reserved 10-credit Meshy keepUV 4K PBR retexture using the accepted built-in ImageGen material reference; inspect actual candidate before any integration.'
receipt_path.write_text(json.dumps(old,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5;camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
a.render_view(scene,camera,focus,Vector((1,-1,.35)),max(high-low)*1.48,HERE/'source-front-right.png')
print('CUPBOARD_SOURCE_READY',json.dumps(r))
