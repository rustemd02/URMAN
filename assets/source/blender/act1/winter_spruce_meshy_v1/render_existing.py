from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[5]
OUT=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'game/assets/models/act1/urman_winter_pine.glb'))
for ob in list(bpy.data.objects):
    if ob.type=='MESH' and ob.name!='WinterPine_LOD0': bpy.data.objects.remove(ob,do_unlink=True)
bpy.ops.object.camera_add(location=(1.9,-2.8,1.0))
camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.camera=camera
camera.data.type='ORTHO';camera.data.ortho_scale=1.25
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=(-2,-3,4));bpy.context.object.data.energy=450;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=4
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.render.resolution_x=720;s.render.resolution_y=960;s.render.resolution_percentage=100
s.render.image_settings.file_format='PNG';s.render.filepath=str(OUT/'existing-winter-pine-lod0.png');s.view_settings.view_transform='AgX';bpy.ops.render.render(write_still=True)
