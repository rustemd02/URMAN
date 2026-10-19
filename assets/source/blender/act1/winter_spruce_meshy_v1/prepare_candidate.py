"""Prepare a separate shared-PBR winter conifer visual candidate, never physics."""
from pathlib import Path
import hashlib,json,sys
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
SOURCE=HERE/'meshy71-pine-remesh-6k.glb'
OUTPUT=HERE/'winter-pine-meshy-v1.glb'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def tris(ob):ob.data.calc_loop_triangles();return len(ob.data.loop_triangles)
def bounds(ob):
    points=[v.co for v in ob.data.vertices]
    return [[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
assert meshes and not any(o.type=='ARMATURE' for o in bpy.data.objects)
bpy.ops.object.select_all(action='DESELECT')
for ob in meshes:
    ob.select_set(True);bpy.context.view_layer.objects.active=ob
    world=ob.matrix_world.copy();ob.parent=None;ob.matrix_world=world
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.context.view_layer.objects.active=meshes[0]
if len(meshes)>1:bpy.ops.object.join()
base=bpy.context.object
# Keep the lower trunk on X=Y=0, not the crown's asymmetric bounding-box centre.
before=bounds(base);height=before[2][1]-before[2][0]
foot=[v.co for v in base.data.vertices if v.co.z<=before[2][0]+height*.012]
root=Vector((sum(v.x for v in foot)/len(foot),sum(v.y for v in foot)/len(foot),before[2][0]))
for v in base.data.vertices:v.co=(v.co-root)/height
for ob in list(bpy.data.objects):
    if ob!=base:bpy.data.objects.remove(ob,do_unlink=True)
assert 1<=len(base.data.materials)<=2
image_report=[]
for material_index,material in enumerate(base.data.materials):
    assert material and material.use_nodes
    material.name=f'URMAN_WinterPine_Meshy_v1_PBR_{material_index}'
    for node in material.node_tree.nodes:
        if node.type!='TEX_IMAGE' or not node.image:continue
        image=node.image
        if any(x['name']==image.name for x in image_report):continue
        # Color is sampled in sRGB, data maps preserve non-color semantics.
        role='basecolor' if image.colorspace_settings.name!='Non-Color' else 'data'
        limit=2048 if role=='basecolor' else 1024
        old=list(image.size);ratio=min(1.,limit/max(old));new=[max(1,round(x*ratio)) for x in old]
        if new!=old:image.scale(*new)
        image.file_format='PNG';image.pack();image_report.append({'name':image.name,'role':role,'before':old,'after':new})
# No mesh is instanced in this export: the runtime shares these three mesh resources.
source_tris=tris(base);lods=[]
for lod,budget in enumerate((6000,2000)):
    ob=base if lod==0 else base.copy()
    if lod:ob.data=base.data.copy();bpy.context.collection.objects.link(ob)
    ob.name=f'WinterPine_LOD{lod}';ob.data.name=ob.name+'_Mesh'
    bpy.context.view_layer.objects.active=ob
    if tris(ob)>budget:
        mod=ob.modifiers.new('BudgetLOD','DECIMATE');mod.ratio=(budget-.5)/tris(ob)
        mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    assert tris(ob)<=budget,(ob.name,tris(ob),budget)
    ob.data.update();lods.append(ob)
bpy.ops.object.select_all(action='DESELECT')
for ob in lods:ob.select_set(True)
bpy.context.view_layer.objects.active=lods[0]
bpy.ops.export_scene.gltf(filepath=str(OUTPUT),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_tangents=True,export_materials='EXPORT',export_animations=False,export_image_format='AUTO')
report={'source':SOURCE.name,'source_sha256':sha(SOURCE),'source_triangles':source_tris,'output':OUTPUT.name,'output_sha256':sha(OUTPUT),'bytes':OUTPUT.stat().st_size,'normalization':{'source_bounds_blender':before,'source_height':height,'source_foot_center':list(root),'method':'World transforms applied to vertices; lower1.2% source vertex centroid rooted atXZ0; source ground0,height1; all LODs share coordinate system and materials, no perLOD translation/scale.'},'lods':[{'node':ob.name,'triangles':tris(ob),'bounds_blender':bounds(ob),'matrix_world':[list(row) for row in ob.matrix_world]} for ob in lods],'materials':[{'name':m.name,'blend_mode':getattr(m,'surface_render_method',None)} for m in base.data.materials],'images':image_report,'additional_meshy_credits':0,'status':'prepared_source_candidate_native_and_author_art_acceptance_open'}
(HERE/'candidate-structure.json').write_text(json.dumps(report,indent=2)+'\n')
# A source-only side-by-side review can use identical lights/camera for each LOD.
bpy.ops.object.camera_add(location=(1.9,-2.8,1.0));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.25
scene=bpy.context.scene;scene.camera=camera
world=scene.world or bpy.data.worlds.new('World');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=(-2,-3,4));bpy.context.object.data.energy=450;bpy.context.object.data.size=4
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=720;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
for lod,ob in enumerate(lods):
    for other in lods:other.hide_render=other!=ob
    scene.render.filepath=str(HERE/f'candidate-lod{lod}.png');bpy.ops.render.render(write_still=True)
print(json.dumps(report,indent=2))

# Complete the same export with the approved zero-credit far representation.
import runpy
runpy.run_path(str(HERE/'prepare_far_impostor.py'),run_name='__main__')
