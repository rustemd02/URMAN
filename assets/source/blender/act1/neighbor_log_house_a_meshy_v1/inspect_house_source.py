"""Inspect a generated house in neutral source views before any runtime integration."""
from pathlib import Path
import hashlib,json,sys
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
SOURCE=HERE/('meshy71-neighbor-house-original.glb' if '--original-clay-cause' in sys.argv else 'meshy71-neighbor-house-remesh-25k.glb')
OUT=HERE/'inspection'
OUT.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
objects=[ob for ob in bpy.data.objects if ob.type=='MESH']
points=[ob.matrix_world@v.co for ob in objects for v in ob.data.vertices]
mins=Vector([min(p[i] for p in points) for i in range(3)]);maxs=Vector([max(p[i] for p in points) for i in range(3)]);span=maxs-mins;center=(mins+maxs)*.5
width=max(span.x,span.y);distance=width*3
for ob in objects:ob.data.calc_loop_triangles()
report={'source':SOURCE.name,'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'source_bytes':SOURCE.stat().st_size,'bounds_blenderXYZ':[[mins[i],maxs[i]] for i in range(3)],'triangles':sum(len(ob.data.loop_triangles) for ob in objects),'mesh_objects':len(objects),'materials':sorted(set(m.name for ob in objects for m in ob.data.materials if m)),'status':'Source3D inspection only; log-course/corner/opening/roof gates require actual image review, not these counts.'}
world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
bpy.ops.object.light_add(type='AREA',location=center+Vector((-width,-width*1.5,width*2)));bpy.context.object.data.energy=450*width*width;bpy.context.object.data.size=width*2
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';bpy.context.scene.camera=camera
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
views=[('negative-y',Vector((0,-distance,span.z*.10)),center,width*1.25),('front-right-quarter',Vector((distance*.72,-distance*.82,span.z*.40)),center,width*1.5),('positive-x',Vector((distance,0,span.z*.10)),center,width*1.25),('positive-y',Vector((0,distance,span.z*.10)),center,width*1.25)]
if '--front-correction' in sys.argv:
    views=[('negative-x-actual-front',Vector((-distance,0,span.z*.10)),center,width*1.25),('negative-x-negative-y-actual-front-quarter',Vector((-distance*.82,-distance*.72,span.z*.40)),center,width*1.5)]
if '--original-clay-cause' in sys.argv:views=[]
for name,offset,target,ortho in views:
    camera.location=target+offset;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=ortho;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
# Texture-free clay views answer the specific construction question: actual
# course/corner/opening volumes must survive removal of albedo and normal maps.
clay=bpy.data.materials.new('Inspection_ActualGeometry_Clay');clay.use_nodes=True
shader=clay.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.42,.42,.42,1);shader.inputs['Roughness'].default_value=.8
scene.view_layers[0].material_override=clay
corner=Vector((mins.x+span.x*.70,mins.y+span.y*.10,mins.z+span.z*.40))
clayviews=[('actual-geometry-clay-three-quarter',Vector((distance*.72,-distance*.82,span.z*.40)),center,width*1.5),('actual-geometry-clay-front-right-corner',Vector((distance*.55,-distance*.65,span.z*.25)),corner,width*.55)]
if '--front-correction' in sys.argv:
    corner=Vector((mins.x+span.x*.10,mins.y+span.y*.32,mins.z+span.z*.40))
    clayviews=[('actual-geometry-clay-actual-front-corner',Vector((-distance*.65,-distance*.55,span.z*.25)),corner,width*.55)]
if '--original-clay-cause' in sys.argv:
    previous=json.loads((OUT/'source-structure.json').read_text())
    reference_bounds=previous['bounds_blenderXYZ']
    reference_min=Vector([v[0] for v in reference_bounds]);reference_span=Vector([v[1]-v[0] for v in reference_bounds])
    reference_width=max(reference_span.x,reference_span.y);reference_distance=reference_width*3
    target=Vector((reference_min.x+reference_span.x*.10,reference_min.y+reference_span.y*.32,reference_min.z+reference_span.z*.40))
    clayviews=[('original-actual-geometry-clay-actual-front-corner',Vector((-reference_distance*.65,-reference_distance*.55,reference_span.z*.25)),target,reference_width*.55)]
    report['comparison']='Same camera target, offset and ortho scale as actual-geometry-clay-actual-front-corner.png from25K; original1.2M geometry, no texture/normal maps, no geometry modification.'
for name,offset,target,ortho in clayviews:
    camera.location=target+offset;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=ortho;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
scene.view_layers[0].material_override=None
previous=json.loads((OUT/'source-structure.json').read_text()) if (OUT/'source-structure.json').exists() else {}
report['renders']=list(dict.fromkeys(previous.get('renders',[])+[name+'.png' for name,_,_,_ in views+clayviews]))
report['geometry_review_method']='Two clay views use materialoverride with no albedo/normaltexture, so painted circles cannot by themselves prove interlocking log geometry.'
(OUT/('original-clay-structure.json' if '--original-clay-cause' in sys.argv else 'source-structure.json')).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
