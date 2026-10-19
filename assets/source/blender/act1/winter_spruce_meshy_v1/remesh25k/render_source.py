"""One25Ksourcepreview, matching the6K candidate camera; no runtime export."""
from pathlib import Path
import hashlib,json
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
SOURCE=HERE/'meshy71-pine-remesh-25k.glb'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
for ob in meshes:
    world=ob.matrix_world.copy();ob.parent=None;ob.matrix_world=world
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
points=[v.co for ob in meshes for v in ob.data.vertices]
bounds=[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)];height=bounds[2][1]-bounds[2][0]
foot=[v for v in points if v.z<=bounds[2][0]+height*.012]
root=Vector((sum(v.x for v in foot)/len(foot),sum(v.y for v in foot)/len(foot),bounds[2][0]))
for ob in meshes:
    for v in ob.data.vertices:v.co=(v.co-root)/height
    ob.data.calc_loop_triangles()
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.camera_add(location=(1.9,-2.8,1.0));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.25;bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-2,-3,4));bpy.context.object.data.energy=450;bpy.context.object.data.size=4
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=720;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(HERE/'pine-25k-source-preview.png');bpy.ops.render.render(write_still=True)
report={'source':SOURCE.name,'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'bytes':SOURCE.stat().st_size,'triangles':sum(len(ob.data.loop_triangles) for ob in meshes),'source_bounds_blenderXYZ':bounds,'preview':'pine-25k-source-preview.png','camera':'Same(1.9,-2.8,1),target(0,0,.5),ortho1.25,720x960,AgX,worldstrength.65,area450/size4 as6Kcandidate','render_normalization':'In-memory only: applyworldT/R/S, rootlower1.2%centroid, base0height1; no sourceGLB or runtimefamily mutation, no decimation/UVbake.','status':'Single25Ksourcepreview forrootreview; not runtime/art/performance accepted.'}
(HERE/'source-structure.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
