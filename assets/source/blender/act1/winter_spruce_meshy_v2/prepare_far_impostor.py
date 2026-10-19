"""Build a neutral-albedo far crosscard from the same approved25Knear."""
from pathlib import Path
import hashlib,json,math
import bpy
import numpy as np
from mathutils import Vector

HERE=Path(__file__).resolve().parent
GLB=HERE/'winter-pine-meshy-v2.glb'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))
near=bpy.data.objects['WinterPine_LOD0'];mid=bpy.data.objects['WinterPine_LOD1']
for ob in list(bpy.data.objects):
    if ob not in (near,mid):bpy.data.objects.remove(ob,do_unlink=True)
source_materials=list(near.data.materials)
# Render basecolor through an emission shader ONLY during baking; the exported
# far material below is ordinary lit PBR with zero emission, including at night.
for i,material in enumerate(source_materials):
    bake=material.copy();bake.name='Temporary_NeutralAlbedoBake'
    nodes=bake.node_tree.nodes;links=bake.node_tree.links
    shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    color=shader.inputs['Base Color'];emission=nodes.new('ShaderNodeEmission')
    if color.is_linked:links.new(color.links[0].from_socket,emission.inputs['Color'])
    else:emission.inputs['Color'].default_value=color.default_value
    emission.inputs['Strength'].default_value=1
    links.new(emission.outputs[0],out.inputs['Surface']);near.data.materials[i]=bake
mid.hide_render=True
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.film_transparent=True;scene.render.resolution_x=512;scene.render.resolution_y=1024;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=1;scene.camera=camera
pixels=np.zeros((1024,1024,4),dtype=np.float32)
for index,position in enumerate(((0,-3,.5),(3,0,.5))):
    camera.location=position;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(HERE/'far-bake-tile.png');bpy.ops.render.render(write_still=True)
    tile=bpy.data.images.load(scene.render.filepath,check_existing=False)
    pixels[:,index*512:(index+1)*512,:]=np.array(tile.pixels[:],dtype=np.float32).reshape((1024,512,4))
    bpy.data.images.remove(tile)
(HERE/'far-bake-tile.png').unlink()
for i,material in enumerate(source_materials):near.data.materials[i]=material
atlas=bpy.data.images.new('URMAN_WinterPine_Meshy_v2_FarAtlas',width=1024,height=1024,alpha=True)
atlas.colorspace_settings.name='sRGB';atlas.pixels.foreach_set(pixels.ravel());atlas.file_format='PNG';atlas.filepath_raw=str(HERE/'far-albedo-atlas.png');atlas.save();atlas.pack()
material=bpy.data.materials.new('URMAN_WinterPine_Meshy_v2_FarMask');material.use_nodes=True
shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=1;shader.inputs['Metallic'].default_value=0;shader.inputs['Emission Strength'].default_value=0
texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=atlas;texture.extension='EXTEND'
clip=material.node_tree.nodes.new('ShaderNodeMath');clip.operation='ROUND'
material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
material.node_tree.links.new(texture.outputs['Alpha'],clip.inputs[0]);material.node_tree.links.new(clip.outputs[0],shader.inputs['Alpha'])
# The exporter recognizes ROUND(alpha) as alphaMode MASK with alphaCutoff 0.5.
vertices=[(-.25,0,0),(.25,0,0),(.25,0,1),(-.25,0,1),(0,-.25,0),(0,.25,0),(0,.25,1),(0,-.25,1)]
mesh=bpy.data.meshes.new('WinterPine_LOD2_Mesh');mesh.from_pydata(vertices,[],[(0,1,2,3),(4,5,6,7)]);mesh.materials.append(material)
uv=mesh.uv_layers.new(name='UVMap')
for index,polygon in enumerate(mesh.polygons):
    coords=((index*.5,0),((index+1)*.5,0),((index+1)*.5,1),(index*.5,1))
    for loop,value in zip(polygon.loop_indices,coords):uv.data[loop].uv=value
far=bpy.data.objects.new('WinterPine_LOD2',mesh);bpy.context.collection.objects.link(far)
bpy.ops.object.select_all(action='DESELECT')
for ob in (near,mid,far):ob.select_set(True)
bpy.context.view_layer.objects.active=near
bpy.ops.export_scene.gltf(filepath=str(GLB),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_tangents=True,export_materials='EXPORT',export_animations=False,export_image_format='AUTO')
# Save a lit source preview, so the final result cannot hide behind an unlit bake.
near.hide_render=True;mid.hide_render=True;far.hide_render=False
camera.location=(1.9,-2.8,1);camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=1.25
scene.render.film_transparent=False;scene.render.resolution_x=720;scene.render.resolution_y=960;scene.view_settings.view_transform='AgX'
world=scene.world or bpy.data.worlds.new('World');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=(-2,-3,4));bpy.context.object.data.energy=450;bpy.context.object.data.size=4
scene.render.filepath=str(HERE/'candidate-lod2.png');bpy.ops.render.render(write_still=True)
f=HERE/'candidate-structure.json';report=json.loads(f.read_text());report['output_sha256']=hashlib.sha256(GLB.read_bytes()).hexdigest();report['bytes']=GLB.stat().st_size
report['lods']=[x for x in report['lods'] if x['node']!='WinterPine_LOD2']+[{'node':'WinterPine_LOD2','triangles':4,'bounds_blender':[[-.25,.25],[-.25,.25],[0,1]],'matrix_world':[list(row) for row in far.matrix_world],'method':'Two crossed opaque-test cards with shared two-view neutral-albedo atlas; generated LOD0 is the only visual source.'}]
report['far_impostor']={'producer':Path(__file__).name,'source':'WinterPine_LOD0 from same generated candidate','atlas':'far-albedo-atlas.png','atlas_dimensions':[1024,1024],'alpha_mode':'MASK','alpha_cutoff':.5,'roughness':1,'metallic':0,'emissive':False,'view_angles_degrees':[0,90],'transparent_blending':False,'additional_meshy_credits':0,'budget_exception':'Root approved19Oct: nearmax26K,midmax6K,far4trisfromsame25Knear,2sharedmaterials andoneextra1Katlas.'}
report['materials'].append({'name':material.name,'alpha_mode':'MASK','alpha_cutoff':.5,'lit':True,'emission_strength':0})
report['status']='prepared_far_corrected_candidate_native_and_author_art_acceptance_open';f.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'output_sha256':report['output_sha256'],'bytes':report['bytes'],'far_triangles':4,'status':report['status']}))
