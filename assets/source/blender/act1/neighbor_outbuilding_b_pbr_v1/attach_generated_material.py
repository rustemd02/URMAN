"""Bind one returned generated PBR atlas to authoritative village component."""
from pathlib import Path
import bpy,json,hashlib,importlib.util,sys
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('existing_house_pbr',HERE.parent/'hero_house_pbr_v1/attach_hero_house_retexture.py')
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(HERE/'source-opaque-uv.glb'))
source_objects=list(bpy.context.scene.objects);source_meshes=[o for o in source_objects if o.type=='MESH'];root=bpy.data.objects['VillageParcel_VariantB_PlasterAnnex_Outbuilding']
source_names=sorted(o.name for o in source_meshes)
before=a.triangle_signatures(source_meshes)
bounds=a.object_bounds(source_meshes)
bpy.ops.import_scene.gltf(filepath=str(HERE/'meshy-retexture-returned.glb'))
returned=[o for o in bpy.context.scene.objects if o not in source_objects]
returned_meshes=[o for o in returned if o.type=='MESH']
signature=a.triangle_signatures(returned_meshes)
uv_comparison=a.counter_delta(before['uv_space'],signature['uv_space'])
report={'schema':'urman-neighbor-outbuilding-b-pbr-candidate/v1','status':'checking_returned_atlas','task_id':'01a179a0-681b-7511-ba53-59866f70651c','consumed_credits':10,'source_triangles':before['triangle_count'],'returned_triangles':signature['triangle_count'],'uv_comparison':uv_comparison,'source':str(HERE/'source-opaque-uv.glb'),'source_sha256':sha(HERE/'source-opaque-uv.glb'),'returned_sha256':sha(HERE/'meshy-retexture-returned.glb'),'runtime_integrated':False}
(HERE/'candidate-receipt.json').write_text(json.dumps(report,indent=2)+'\n')
assert uv_comparison['exact'],'Returned atlas UV differs; no attachment allowed'
materials=list(dict.fromkeys(m for o in returned_meshes for m in o.data.materials if m))
assert len(materials)==1
material=materials[0].copy();material.name='URMAN_VariantB_Outbuilding_GeneratedPBR'
report['material']=a.material_report(material)
for ob in source_meshes:
 ob.data.materials.clear();ob.data.materials.append(material)
 for poly in ob.data.polygons:poly.material_index=0
assert a.triangle_signatures(source_meshes)==before
for ob in returned:bpy.data.objects.remove(ob,do_unlink=True)
# Runtime mip source: 2K color/normal and 1K scalar channels, same atlas/UV.
for node in material.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  image=node.image
  size=1024 if image.colorspace_settings.name=='Non-Color' and not any(l.to_node.type=='NORMAL_MAP' for l in node.outputs['Color'].links) else 2048
  if max(image.size)>size:image.scale(size,size)
  image.pack()
bpy.ops.object.select_all(action='DESELECT')
for o in source_objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
output=HERE/'variant-b-outbuilding-generated-pbr-2k.glb'
bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,export_yup=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
# Reimport actual candidate to check delivered geometry, transforms, UV coverage.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(output))
actual=[o for o in bpy.context.scene.objects if o.type=='MESH']
after=a.triangle_signatures(actual)
report['candidate_geometry_comparison']=a.counter_delta(before['geometry'],after['geometry'])
report['candidate_paired_uv_comparison']=a.counter_delta(before['paired'],after['paired'])
assert report['candidate_geometry_comparison']['exact'] and report['candidate_paired_uv_comparison']['exact']
assert a.object_bounds(actual)==bounds
report.update(status='source_candidate_maps_attached',candidate=output.name,candidate_sha256=sha(output),candidate_bytes=output.stat().st_size,candidate_nodes=len(actual),candidate_triangles=after['triangle_count'],bounds_blender=bounds,source_semantic_nodes_preserved=sorted(o.name for o in actual)==source_names)
# Source node wrappers and existing glass remain the runtime integration owner's responsibility.
report['remaining']='Root must inspect four actual candidate views and integrate only opaque VariantB_Outbuilding visual nodes at original local transforms; retain original separate glass, collision, root placement and snow. No game/native/motion/art acceptance claimed.'
(HERE/'candidate-receipt.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
low,high=map(Vector,bounds);focus=(low+high)*.5
camera=a.create_studio(bpy.context.scene,focus,bounds)
scene=bpy.context.scene;scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=4;scene.render.resolution_x=1280;scene.render.resolution_y=960
for name,direction in [('candidate-front',Vector((0,-1,.2))),('candidate-side',Vector((1,0,.2))),('candidate-front-right',Vector((1,-1,.5))),('candidate-rear-left',Vector((-1,1,.5)))]:
 a.render_view(scene,camera,focus,direction,max(high-low)*1.5,HERE/(name+'.png'))
print('CANDIDATE_READY',report['candidate_sha256'],report['candidate_triangles'],report['candidate_bytes'])
