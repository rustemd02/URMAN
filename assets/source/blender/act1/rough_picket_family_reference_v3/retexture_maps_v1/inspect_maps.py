"""Extract embedded PBR images and compare UV coverage; never attach geometry."""
from pathlib import Path
import collections, hashlib, json, struct

HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent/'rough-picket-family-source.glb'
RETURNED=HERE/'meshy-returned.glb'


def read_glb(path):
    raw=path.read_bytes();assert raw[:4]==b'glTF';offset=12;document=binary=None
    while offset<len(raw):
        size,kind=struct.unpack_from('<II',raw,offset);offset+=8
        block=raw[offset:offset+size];offset+=size
        if kind==0x4e4f534a:document=json.loads(block)
        elif kind==0x004e4942:binary=block
    assert document is not None and binary is not None
    return document,binary


def accessor(doc,binary,index):
    item=doc['accessors'][index];view=doc['bufferViews'][item['bufferView']]
    assert not item.get('sparse') and view.get('buffer',0)==0
    fmt={5126:'f',5125:'I',5123:'H',5121:'B'}[item['componentType']]
    width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[item['type']]
    stride=view.get('byteStride',struct.calcsize('<'+fmt)*width)
    start=view.get('byteOffset',0)+item.get('byteOffset',0)
    return [struct.unpack_from('<'+fmt*width,binary,start+i*stride) for i in range(item['count'])]


def uv_triangles(doc,binary):
    result=collections.Counter();count=0
    for node in doc['nodes']:
        if 'mesh' not in node:continue
        for primitive in doc['meshes'][node['mesh']]['primitives']:
            assert primitive.get('mode',4)==4
            uv=accessor(doc,binary,primitive['attributes']['TEXCOORD_0'])
            indices=[v[0] for v in accessor(doc,binary,primitive['indices'])]
            for i in range(0,len(indices),3):
                points=[tuple(round(float(c),7) for c in uv[j]) for j in indices[i:i+3]]
                result[tuple(sorted(points))]+=1;count+=1
    return result,count


def jpeg_size(payload):
    assert payload[:2]==b'\xff\xd8';offset=2
    while offset<len(payload):
        assert payload[offset]==255
        while payload[offset]==255:offset+=1
        marker=payload[offset];offset+=1
        if marker in (0xd8,0xd9) or 0xd0<=marker<=0xd7:continue
        size=struct.unpack_from('>H',payload,offset)[0]
        if marker in (0xc0,0xc1,0xc2,0xc3,0xc5,0xc6,0xc7,0xc9,0xca,0xcb,0xcd,0xce,0xcf):
            height,width=struct.unpack_from('>HH',payload,offset+3);return [width,height]
        offset+=size
    raise ValueError('JPEG dimensions not found')


def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='b0188bdb300a644126bbf495c4d4854aa4a11c0a5cd0485510297014a1a9e195'
    original,original_binary=read_glb(SOURCE);returned,binary=read_glb(RETURNED)
    assert len(returned['materials'])==1
    material=returned['materials'][0];pbr=material['pbrMetallicRoughness']
    slots={'base-color':pbr['baseColorTexture'],'metallic-roughness':pbr['metallicRoughnessTexture'],'normal':material['normalTexture']}
    maps=[]
    for role,info in slots.items():
        assert info.get('texCoord',0)==0 and not info.get('extensions'),info
        texture=returned['textures'][info['index']];image=returned['images'][texture['source']]
        assert image['mimeType']=='image/jpeg' and 'uri' not in image
        view=returned['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
        payload=binary[start:start+view['byteLength']]
        output=HERE/(role+'.jpg');assert not output.exists();output.write_bytes(payload)
        maps.append({'role':role,'path':output.name,'mime_type':image['mimeType'],'dimensions':jpeg_size(payload),
                     'bytes':len(payload),'sha256':hashlib.sha256(payload).hexdigest(),'embedded_image_index':texture['source'],
                     'texcoord':info.get('texCoord',0),'texture_transform_extension_present':False})
    old,old_count=uv_triangles(original,original_binary);new,new_count=uv_triangles(returned,binary)
    missing=old-new;extra=new-old
    status=json.loads((HERE/'status-initial.json').read_text())
    before=json.loads((HERE/'balance-before.json').read_text());after=json.loads((HERE/'balance-after.json').read_text())
    result={'schema':'rough-picket-pbr-maps-only/v1','status':'maps_extracted_parent_inspection_required_no_attachment',
            'task_id':status['id'],'task_status':status['status'],'consumed_credits_reported':status['consumed_credits'],
            'balance_before':before['balance'],'balance_after':after['balance'],'observed_balance_delta':before['balance']-after['balance'],
            'author_ledger_before':350,'author_ledger_after_this_one_task':350+status['consumed_credits'],'author_cap':4000,
            'source':{'path':str(SOURCE.relative_to(HERE.parent)),'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'semantic_meshes':79,'triangles':2212},
            'returned_artifact':{'path':RETURNED.name,'sha256':hashlib.sha256(RETURNED.read_bytes()).hexdigest(),'bytes':RETURNED.stat().st_size,'meshes':len(returned['meshes']),'nodes':len(returned['nodes']),'triangles':new_count,'geometry_authority':False},
            'maps':maps,'material_chain':{'alpha_mode':material.get('alphaMode','OPAQUE'),'double_sided':material.get('doubleSided',False),'metallic_factor':pbr.get('metallicFactor',1),'roughness_factor':pbr.get('roughnessFactor',1),'base_color_factor':pbr.get('baseColorFactor',[1,1,1,1]),'normal_scale':material['normalTexture'].get('scale',1),'samplers':returned.get('samplers',[])},
            'uv_coverage':{'method':'UV triangle multiset rounded to7 decimal places; preserves triangle multiplicity independently of returned node merging','source_triangle_count':old_count,'returned_triangle_count':new_count,'missing_triangles':sum(missing.values()),'extra_triangles':sum(extra.values()),'equal':old==new,'missing_first8':list(missing.items())[:8],'extra_first8':list(extra.items())[:8]},
            'orientation':'Standard unmodified glTF TEXCOORD_0 and image bytes; no image flips, UV remap, texture transforms or tangent-normal correction performed. Actual applied material orientation not yet previewed.',
            'rights':'Owner-authorized paid Meshy retexture of project-original source and own accepted ImageGen reference. No Community publication or new external license asserted.',
            'limits':'Exactly one paid retexture task. Returned merged geometry is NOT canonical. No Blender attachment/runtime/UV changes. Root reviews raw maps before any source candidate attachment.'}
    (HERE/'maps-inspection-receipt.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'task_id':status['id'],'charged':status['consumed_credits'],'maps':maps,'uv_coverage':result['uv_coverage']},ensure_ascii=False))


if __name__=='__main__':main()
