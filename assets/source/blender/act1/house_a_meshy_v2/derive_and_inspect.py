"""One free Blender-native house LOD experiment; never runtime or Council binding."""
from pathlib import Path
import hashlib,json,tempfile,zipfile
import bpy,bmesh
from mathutils import Vector
HERE=Path(__file__).resolve().parent
PREVIOUS=HERE.parent/'neighbor_log_house_a_meshy_v1'
ARCHIVE=PREVIOUS/'meshy71-neighbor-house-original.glb.zip'
MEMBER='meshy71-neighbor-house-original.glb'
EXPECTED_SHA='325c095cf1a39d5a8ec0391c2501951e1a0d77549a579831ed29a0298e80a1d5'
OUTPUT=HERE/'neighbor-house-a-blender100k.glb'
MANIFEST=HERE/'house-a-v2.manifest.json'
TARGET_TRIANGLES=99000
MAX_TRIANGLES=100000

def sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):digest.update(chunk)
    return digest.hexdigest()
def tris(mesh):mesh.calc_loop_triangles();return len(mesh.loop_triangles)
def uv(mesh):return [{'name':layer.name,'loops':len(layer.data)} for layer in mesh.uv_layers]
def bounds(objects):
    points=[ob.matrix_world@v.co for ob in objects for v in ob.data.vertices]
    return [[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]
def clear_scene():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.data.orphans_purge(do_local_ids=True,do_linked_ids=False,do_recursive=True)

assert tuple(bpy.app.version[:3])==(4,5,12),bpy.app.version_string
assert not OUTPUT.exists() and not MANIFEST.exists(),'Refusing to overwrite the one derived candidate'
report={'asset_id':'ACT1-NEIGHBOR-HOUSE-A-BLENDER-DERIVED-V2','status':'building_single_local_candidate','source_archive':str(ARCHIVE.relative_to(HERE.parent)),'source_archive_sha256':sha(ARCHIVE),'source_member':MEMBER,'source_original_sha256':EXPECTED_SHA,'reference_provenance':'../neighbor_log_house_a_meshy_v1/neighbor-log-house-a-v1.manifest.json','authorization':'One localderivedcandidate from originalraw through existingBlendercollapse <=100K; one samecamera claycorner +4wholeviews; noMeshy/reference/semanticrebuild/runtime/Councilintegration. Stopafterinspection.','additional_meshy_credits':0,'new_post_performed':False,'accounted_overall_unchanged':270}
with tempfile.TemporaryDirectory(prefix='urman-house-a-derive-') as expanded:
    expanded_path=Path(expanded);source=expanded_path/MEMBER
    with zipfile.ZipFile(ARCHIVE) as archive:
        assert archive.namelist()==[MEMBER]
        with archive.open(MEMBER) as incoming,source.open('xb') as outgoing:
            for chunk in iter(lambda:incoming.read(1024*1024),b''):outgoing.write(chunk)
    assert sha(source)==EXPECTED_SHA,'Expanded raw source SHA mismatch'
    clear_scene();bpy.ops.import_scene.gltf(filepath=str(source))
    objects=[ob for ob in bpy.data.objects if ob.type=='MESH'];assert len(objects)==1
    subject=objects[0];report['raw_triangles']=tris(subject.data);report['raw_vertices']=len(subject.data.vertices);report['raw_uv']=uv(subject.data);report['raw_bounds_blender']=bounds(objects)
    # Existing hero-house reduction pattern: restore coincident UV-seam vertices
    # without changing per-corner UV loops, then native quadric edge collapse.
    bm=bmesh.new();bm.from_mesh(subject.data);before=len(bm.verts)
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=.000001)
    bm.normal_update();bm.to_mesh(subject.data);bm.free();subject.data.update()
    report['weld']={'distance':.000001,'vertices_before':before,'vertices_after':len(subject.data.vertices),'uv_after':uv(subject.data)}
    assert report['raw_uv']==report['weld']['uv_after'],'Weld changed UV-layer identity or loop count'
    bpy.ops.object.select_all(action='DESELECT');subject.select_set(True);bpy.context.view_layer.objects.active=subject
    welded_triangles=tris(subject.data)
    modifier=subject.modifiers.new('Native quadric edge collapse <=100K','DECIMATE');modifier.decimate_type='COLLAPSE';modifier.ratio=min(1.,TARGET_TRIANGLES/welded_triangles);modifier.use_collapse_triangulate=True
    report['collapse']={'source_triangles':welded_triangles,'target_triangles':TARGET_TRIANGLES,'maximum_triangles':MAX_TRIANGLES,'ratio':modifier.ratio,'method_reference':'../hero_house_meshy_v1/reduce_meshy71_textured_native_100k.py'}
    bpy.ops.object.modifier_apply(modifier=modifier.name);subject.data.update()
    report['derived_triangles']=tris(subject.data);assert report['derived_triangles']<=MAX_TRIANGLES
    report['derived_uv']=uv(subject.data);report['derived_bounds_blender']=bounds([subject])
    bpy.ops.export_scene.gltf(filepath=str(OUTPUT),export_format='GLB',use_selection=True,export_apply=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_animations=False,export_image_format='AUTO')
    report['output']={'file':OUTPUT.name,'sha256':sha(OUTPUT),'bytes':OUTPUT.stat().st_size}
    # Inspect the exported/reimported model, rather than only modifier memory.
    clear_scene();bpy.ops.import_scene.gltf(filepath=str(OUTPUT))
    objects=[ob for ob in bpy.data.objects if ob.type=='MESH'];report['reimport_triangles']=sum(tris(ob.data) for ob in objects)
    reference=json.loads((PREVIOUS/'inspection/source-structure.json').read_text())['bounds_blenderXYZ']
    low=Vector([row[0] for row in reference]);span=Vector([row[1]-row[0] for row in reference]);center=low+span*.5;width=max(span.x,span.y);distance=width*3
    world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.36,.42,.48,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
    bpy.ops.object.light_add(type='AREA',location=center+Vector((-width,-width*1.5,width*2)));bpy.context.object.data.energy=450*width*width;bpy.context.object.data.size=width*2
    bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';bpy.context.scene.camera=camera
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=960;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
    clay=bpy.data.materials.new('Inspection_ActualGeometry_Clay');clay.use_nodes=True;shader=clay.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.42,.42,.42,1);shader.inputs['Roughness'].default_value=.8
    corner=Vector((low.x+span.x*.10,low.y+span.y*.32,low.z+span.z*.40))
    views=[('actual-front-corner-clay',Vector((-distance*.65,-distance*.55,span.z*.25)),corner,width*.55,True),('actual-front-negative-x',Vector((-distance,0,span.z*.10)),center,width*1.25,False),('right-side-negative-y',Vector((0,-distance,span.z*.10)),center,width*1.25,False),('rear-gable-positive-x',Vector((distance,0,span.z*.10)),center,width*1.25,False),('left-side-positive-y',Vector((0,distance,span.z*.10)),center,width*1.25,False)]
    for name,offset,target,ortho,clay_view in views:
        scene.view_layers[0].material_override=clay if clay_view else None
        camera.location=target+offset;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=ortho;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True)
    report['renders']=[name+'.png' for name,_,_,_,_ in views]
    report['clay_comparison']='Exactsamecamera target/offset/ortho andgrey material as raw/25Kactualfrontcorner pair from neighbor_log_house_a_meshy_v1; noalbedo/normalmap.'
    report['status']='single_local_derived_candidate_frozen_for_construction_review_not_runtime'
report['temporary_expanded_raw_removed']=not expanded_path.exists();assert report['temporary_expanded_raw_removed']
report['scope']='Onlynewhouse_a_meshy_v2folder. Originalarchive/frozen25Kreject/Councilnamedwalls/doors/physics/runtime/Git/sharedtracker/Windows unchanged.'
report['remaining']='Actuallyinspectoneclaycornerand4wholeviews. Rejectandstopifcourses/corners/roof/hiddenwallsunconvincing; nofurtherattemptinthisstage. FunctionalCouncilbindingstillopenregardlessvisualresult.'
MANIFEST.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
