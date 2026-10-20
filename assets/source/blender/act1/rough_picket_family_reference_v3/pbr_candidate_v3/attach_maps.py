"""Direct GLB PBR binding only; canonical geometry is never Blender-exported."""
from pathlib import Path
import copy, hashlib, importlib.util, json, struct, sys, traceback
sys.dont_write_bytecode=True
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
FAMILY=HERE.parent
ROOT=FAMILY.parents[4]
SOURCE=FAMILY/'rough-picket-family-source.glb'
RETURNED=FAMILY/'retexture_maps_v1/meshy-returned.glb'
SOURCE_RECEIPT=FAMILY/'source-receipt.json'
MAP_RECEIPT=FAMILY/'retexture_maps_v1/maps-inspection-receipt.json'
WRITER=FAMILY.parent/'neighbor_house_a_retexture_v2/attach_variant_a_retexture.py'
GLB=HERE/'rough-picket-family-semantic-pbr-v3.glb'
RECEIPT=HERE/'candidate-receipt.json'
report={'schema':'rough-picket-literal-source-pbr/v3','status':'preparing_maps_only_source_candidate'}


def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


existing=load('existing_direct_material_writer',WRITER)
a=existing.a


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def save():RECEIPT.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')


def write_material_only():
    source=a.glb_document(SOURCE);returned=a.glb_document(RETURNED)
    output=copy.deepcopy(source)
    original=existing.glb_binary(SOURCE);binary=bytearray(original)
    other=existing.glb_binary(RETURNED)
    assert len(source['buffers'])==len(returned['buffers'])==1
    assert 'uri' not in source['buffers'][0] and 'uri' not in returned['buffers'][0]
    image_base=len(output.get('images',[]));sampler_base=len(output.get('samplers',[]));texture_base=len(output.get('textures',[]))
    image_rows=[]
    for index,image in enumerate(returned['images']):
        assert 'bufferView' in image and 'uri' not in image
        view=returned['bufferViews'][image['bufferView']];assert view['buffer']==0
        payload=other[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        binary.extend(b'\0'*((-len(binary))%4))
        new_view={'buffer':0,'byteOffset':len(binary),'byteLength':len(payload)}
        new_image=copy.deepcopy(image);new_image['bufferView']=len(output['bufferViews'])
        output['bufferViews'].append(new_view);output.setdefault('images',[]).append(new_image);binary.extend(payload)
        image_rows.append({'returned_image_index':index,'output_buffer_view':new_image['bufferView'],'mime_type':image['mimeType'],'bytes':len(payload),'sha256':hashlib.sha256(payload).hexdigest()})
    output.setdefault('samplers',[]).extend(copy.deepcopy(returned.get('samplers',[])))
    for texture in returned['textures']:
        new=copy.deepcopy(texture);new['source']+=image_base
        if 'sampler' in new:new['sampler']+=sampler_base
        output.setdefault('textures',[]).append(new)
    assert len(returned['materials'])==1
    generated=copy.deepcopy(returned['materials'][0])
    assert generated.get('alphaMode','OPAQUE')=='OPAQUE'
    for binding in (generated['pbrMetallicRoughness']['baseColorTexture'],generated['pbrMetallicRoughness']['metallicRoughnessTexture'],generated['normalTexture']):
        assert binding.get('texCoord',0)==0 and not binding.get('extensions');binding['index']+=texture_base
    roles=[]
    for index,material in enumerate(output['materials']):
        old=source['materials'][index]
        # Keep original role names and indices; show the actual generated PBR
        # without hiding it under the old colour factors or hand-correcting iron.
        material['pbrMetallicRoughness']=copy.deepcopy(generated['pbrMetallicRoughness'])
        material['normalTexture']=copy.deepcopy(generated['normalTexture'])
        assert material['name']==old['name']
        assert {k:v for k,v in material.items() if k not in ('pbrMetallicRoughness','normalTexture')}=={k:v for k,v in old.items() if k not in ('pbrMetallicRoughness','normalTexture')}
        roles.append({'index':index,'name':material['name'],'old_pbr_factors':old['pbrMetallicRoughness'],'new_pbr_factors_and_bindings':material['pbrMetallicRoughness']})
    output['buffers'][0]['byteLength']=len(binary)
    assert output['meshes']==source['meshes'] and output['nodes']==source['nodes'] and output['accessors']==source['accessors']
    assert output['bufferViews'][:len(source['bufferViews'])]==source['bufferViews']
    assert bytes(binary[:len(original)])==original
    allowed={'materials','textures','samplers','images','bufferViews','buffers'}
    assert all(output[key]==value for key,value in source.items() if key not in allowed)
    encoded=json.dumps(output,separators=(',',':'),ensure_ascii=False).encode();encoded+=b' '*((-len(encoded))%4)
    binary.extend(b'\0'*((-len(binary))%4))
    packed=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
    GLB.write_bytes(packed)
    actual=a.glb_document(GLB);actual_bin=existing.glb_binary(GLB)
    assert actual['nodes']==source['nodes'] and actual['meshes']==source['meshes'] and actual['accessors']==source['accessors']
    assert actual_bin[:len(original)]==original
    for row in image_rows:
        view=actual['bufferViews'][row['output_buffer_view']]
        assert hashlib.sha256(actual_bin[view['byteOffset']:view['byteOffset']+view['byteLength']]).hexdigest()==row['sha256']
    return {'literal_source_bin_bytes':len(original),'literal_source_bin_sha256':hashlib.sha256(original).hexdigest(),
            'candidate_original_bin_prefix_sha256':hashlib.sha256(actual_bin[:len(original)]).hexdigest(),
            'source_mesh_json_material_assignments_identical':True,'source_nodes_accessors_transforms_indices_normals_uv_literal_identical':True,
            'all_source_material_names_and_indices_retained':roles,'returned_image_bytes_identical':image_rows,'returned_geometry_incorporated':False,
            'changed_json_scope':'Append image bufferViews/images/samplers/textures, replace only PBR map bindings/factors and normalTexture within the existing five named materials; update BIN byteLength. No mesh/node/accessor edits.'}


def main():
    assert bpy.app.version[:3]==(4,5,12)
    assert not GLB.exists() and not RECEIPT.exists()
    assert sha(SOURCE)=='b0188bdb300a644126bbf495c4d4854aa4a11c0a5cd0485510297014a1a9e195'
    assert sha(RETURNED)=='23af51c6a821c512ae293a8f0dc3921b748a9a9b5be5548de46f00477cbde03d'
    assert sha(SOURCE_RECEIPT)=='703dd6d255878c30b27e5e03a9e51bd743f37d927a44dd500e571bb2798eb190'
    report.update({'source':{'path':str(SOURCE.relative_to(ROOT)),'sha256':sha(SOURCE)},'returned':{'path':str(RETURNED.relative_to(ROOT)),'sha256':sha(RETURNED),'geometry_authority':False},
                   'dependencies':[{'path':str(path.relative_to(ROOT)),'sha256':sha(path)} for path in (WRITER,Path(a.__file__),SOURCE_RECEIPT,MAP_RECEIPT)],
                   'spend_this_attachment':0,'material_policy':'One uncorrected generated PBR across the original five semantic role materials; no per-role artistic adjustments, flips or normal-map corrections.'})
    report['literal_byte_proof']=write_material_only()
    report['output']={'path':GLB.name,'bytes':GLB.stat().st_size,'sha256':sha(GLB)}
    report['phase']='actual_candidate_render';save()
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(GLB))
    root=bpy.data.objects['URMAN_RoughPicketFamilySourceV3'];meshes=[ob for ob in root.children_recursive if ob.type=='MESH']
    assert len(meshes)==79 and sum(existing.p.h.mesh_triangles(ob.data) for ob in meshes)==2212
    assert len([ob for ob in root.children_recursive if 'StaticAttachmentPivot' in ob.name])==2
    scene=bpy.context.scene;bounds=a.object_bounds(meshes);lo,hi=map(Vector,bounds);focus=(lo+hi)*.5
    camera=a.create_studio(scene,focus,bounds)
    for vertex in bpy.data.objects['PBRReview_Ground'].data.vertices:vertex.co.z=0
    scene.render.resolution_x,scene.render.resolution_y=1440,960;scene.cycles.samples=24;scene.render.threads_mode='FIXED';scene.render.threads=4
    source_report=json.loads(SOURCE_RECEIPT.read_text());report['actual_candidate_views']=[]
    for source_view in source_report['actual_source_views']:
        path=HERE/(source_view['path'].replace('actual-source','actual-pbr'))
        a.render_view(scene,camera,Vector(source_view['target']),Vector(source_view['direction']),source_view['orthographic_scale'],path)
        report['actual_candidate_views'].append({'path':path.name,'sha256':sha(path),'bytes':path.stat().st_size,'same_camera_as_source':source_view['path']})
    report['status']='source_pbr_candidate_pending_actual_material_review'
    report['technical_scope']={'literal_geometry_byteguard':True,'source_mesh_count':79,'source_triangle_count':2212,'frozen_source_material_role_indices_preserved':5,'new_paid_calls':0,'runtime_files_changed':False,'Blender_geometry_reexport':False}
    report['remaining']='Asset worker/root actual-view judgment of grain, baked frame markings, iron separation, paint and alignment. HOLD on bad appearance; no new paid calls or material variants.'
    assert sha(SOURCE)==report['source']['sha256'];save()
    print('FENCE_SOURCE_PBR',json.dumps({'status':report['status'],'glb_sha256':sha(GLB),'literal_geometry_bytes':True,'views':len(report['actual_candidate_views'])}),flush=True)


if __name__=='__main__':
    try:main()
    except Exception as error:
        report.update({'status':'source_attachment_failed_stop','failure':repr(error),'traceback':traceback.format_exc()});save();raise
