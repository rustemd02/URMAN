"""Bind one returned Meshy atlas to the original living-room cupboard meshes."""
from pathlib import Path
import bpy,json,hashlib,importlib.util,sys
from collections import Counter
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('existing_house_pbr',HERE.parent/'hero_house_pbr_v1'/'attach_hero_house_retexture.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(HERE/'source-cupboard-uv.glb'))
source_objects=list(bpy.context.scene.objects);source_meshes=[o for o in source_objects if o.type=='MESH'];root=bpy.data.objects['HomeCupboardPbr']
source_names=sorted(o.name for o in source_meshes)
before=a.triangle_signatures(source_meshes)
bounds=a.object_bounds(source_meshes)
bpy.ops.import_scene.gltf(filepath=str(HERE/'meshy-retexture-returned.glb'))
returned=[o for o in bpy.context.scene.objects if o not in source_objects]
returned_meshes=[o for o in returned if o.type=='MESH']
def tri_records(objects,source_center=None,returned_center=None,scale=1.0):
 records=[];degenerate=[]
 for ob in objects:
  ob.data.calc_loop_triangles();uv=ob.data.uv_layers.active or ob.data.uv_layers[0]
  for tri in ob.data.loop_triangles:
   positions=[ob.matrix_world@ob.data.vertices[vi].co for vi in tri.vertices]
   area=(positions[1]-positions[0]).cross(positions[2]-positions[0]).length*.5
   if area<=1e-10:
    degenerate.append({'node':ob.name,'polygon':ob.data.polygons[tri.polygon_index].index,'area':area});continue
   world=[]
   for p in positions:
    if returned_center is not None:p=(p-returned_center)*scale+source_center
    world.append(tuple(round(float(p[i]),5) for i in range(3)))
   uvs=[tuple(round(float(c),7) for c in uv.data[li].uv) for li in tri.loops]
   records.append({'geometry':tuple(sorted(world)),'uv':tuple(sorted(uvs)),'paired':tuple(sorted(zip(world,uvs)))})
 return records,degenerate
source_records,source_degenerate=tri_records(source_meshes);source_bounds=bounds
source_low,source_high=map(Vector,source_bounds);source_center=(source_low+source_high)*.5;source_dimensions=source_high-source_low
returned_bounds=a.object_bounds(returned_meshes);returned_low,returned_high=map(Vector,returned_bounds);returned_center=(returned_low+returned_high)*.5;returned_dimensions=returned_high-returned_low
scale_factors=[float(source_dimensions[i]/returned_dimensions[i]) for i in range(3)];uniform_scale=sum(scale_factors)/3
returned_records,returned_degenerate=tri_records(returned_meshes,source_center,returned_center,uniform_scale)
geometry_comparison=a.counter_delta(Counter(r['geometry'] for r in source_records),Counter(r['geometry'] for r in returned_records))
uv_comparison=a.counter_delta(Counter(r['uv'] for r in source_records),Counter(r['uv'] for r in returned_records))
paired_comparison=a.counter_delta(Counter(r['paired'] for r in source_records),Counter(r['paired'] for r in returned_records))
scale_spread=max(scale_factors)-min(scale_factors)
report={'schema':'urman-home-cupboard-pbr-candidate/v1','status':'checking_returned_atlas','task_id':'01a184a4-5aca-7534-8851-0323465d09d3','consumed_credits':10,'source_triangles':before['triangle_count'],'returned_triangles':len(returned_records)+len(returned_degenerate),'source_zero_area_triangles':len(source_degenerate),'returned_zero_area_triangles':len(returned_degenerate),'source_bounds':source_bounds,'returned_bounds_normalized':returned_bounds,'returned_to_source_uniform_scale':uniform_scale,'uniform_scale_spread':scale_spread,'nondegenerate_geometry_comparison_after_normalization':geometry_comparison,'nondegenerate_uv_comparison':uv_comparison,'nondegenerate_position_uv_comparison_after_normalization':paired_comparison,'source':str(HERE/'source-cupboard-uv.glb'),'source_sha256':sha(HERE/'source-cupboard-uv.glb'),'returned':str(HERE/'meshy-retexture-returned.glb'),'returned_sha256':sha(HERE/'meshy-retexture-returned.glb'),'runtime_integrated':False}
(HERE/'candidate-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
assert scale_spread<=1e-5,'Meshy returned model has non-uniform scale/deformation'
assert geometry_comparison['exact'],'Meshy returned non-degenerate geometry differs beyond uniform scale'
assert uv_comparison['exact'] and paired_comparison['exact'],'Meshy returned non-degenerate UV layout differs'
assert len(returned_degenerate)==0,'Returned model contains degenerate geometry not present as surfaces'
materials=list(dict.fromkeys(m for o in returned_meshes for m in o.data.materials if m))
assert len(materials)==1,f'Expected one generated material atlas, got {len(materials)}'
material=materials[0].copy();material.name='URMAN_HomeCupboard_GeneratedPBR'
report['material']=a.material_report(material)
for ob in source_meshes:
 ob.data.materials.clear();ob.data.materials.append(material)
 for poly in ob.data.polygons:poly.material_index=0
assert a.triangle_signatures(source_meshes)==before
for ob in returned:bpy.data.objects.remove(ob,do_unlink=True)
# Keep the returned 4K source GLB above; prepare a 2K candidate for later game review.
for node in material.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  image=node.image
  size=1024 if image.colorspace_settings.name=='Non-Color' and not any(l.to_node.type=='NORMAL_MAP' for l in node.outputs['Color'].links) else 2048
  if max(image.size)>size:image.scale(size,size)
  image.pack()
bpy.ops.object.select_all(action='DESELECT')
for o in source_objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
output=HERE/'home-cupboard-generated-pbr-2k.glb'
bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
# Re-import the actual candidate and verify geometry, UV pairings, nodes, and bounds.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(output))
actual=[o for o in bpy.context.scene.objects if o.type=='MESH']
after=a.triangle_signatures(actual)
report['candidate_geometry_comparison']=a.counter_delta(before['geometry'],after['geometry'])
report['candidate_paired_uv_comparison']=a.counter_delta(before['paired'],after['paired'])
assert report['candidate_geometry_comparison']['exact'] and report['candidate_paired_uv_comparison']['exact']
assert a.object_bounds(actual)==bounds
report.update(status='source_candidate_maps_attached',candidate=output.name,candidate_sha256=sha(output),candidate_bytes=output.stat().st_size,candidate_nodes=len(actual),candidate_triangles=after['triangle_count'],bounds_blender=bounds,source_semantic_nodes_preserved=sorted(o.name for o in actual)==source_names)
report['remaining']='Source-only candidate. Runtime integration, contextual scene/motion/performance review, original LOD1, cultural/art and author acceptance remain open; no runtime or collision owner changed.'
(HERE/'candidate-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5
camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
a.render_view(scene,camera,focus,Vector((1,-1,.5)),max(high-low)*1.5,HERE/'candidate-front-right.png')
print('CUPBOARD_CANDIDATE_READY',json.dumps(report,ensure_ascii=False))
