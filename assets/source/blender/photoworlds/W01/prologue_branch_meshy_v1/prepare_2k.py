"""Existing H049/Pillow 2Kmap policy with canonical BIN prefix kept literal.

Restore only locked4Ksource from the local archive before reproducing. This
source-only output never overwrites canonicalruntime; roots/UV0/UV1/3clips retain
all source bytes. No tint, repaint, channel swizzle or normal correction.


Archived prerequisites: evidence/PW-093/prologue-branch-material-source-v1-20261023.tar.xz (repository-relative).
Before replay extract f01-branch-whole-pbr-4k-v1.glb from the archive into this recipe folder; the2K output must be absent. The shared canonical input must still match060b8.
Extract named members with tar -xJf ARCHIVE -C THIS_RECIPE_FOLDER MEMBER... .
"""
from pathlib import Path
import json,hashlib,struct,copy,io,importlib.util
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
METHOD=ROOT/'assets/source/blender/act1/h049_outbuilding_multiview_v1/generation/prepare_h049_runtime.py'
spec=importlib.util.spec_from_file_location('existing_glb_reader',METHOD);h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
CANONICAL=ROOT/'game/assets/photoworlds/branch/prologue-branch-v1.glb';SOURCE=HERE/'f01-branch-whole-pbr-4k-v1.glb';OUT=HERE/'f01-branch-whole-pbr-2k-v1.glb'
sha=lambda b:hashlib.sha256(b).hexdigest()
assert not OUT.exists()
assert sha(CANONICAL.read_bytes())=='060b8af9939754ba3f6de866b703430f6f22a6498733104c253b5ff451b1e742'
assert sha(SOURCE.read_bytes())=='264cdc86905556a777fe5a43222f2636a9be8c98529f8f036aa052b108c8b0d8'
original,base=h.read_glb(CANONICAL.read_bytes());doc,binary=h.read_glb(SOURCE.read_bytes());assert binary[:len(base)]==base
output=copy.deepcopy(doc);outbin=bytearray(base);roles={}
for name,b in [('basecolor_sRGB',doc['materials'][0]['pbrMetallicRoughness']['baseColorTexture']),('metallicRoughness_linear',doc['materials'][0]['pbrMetallicRoughness']['metallicRoughnessTexture']),('normal_linear',doc['materials'][0]['normalTexture'])]:
 imindex=doc['textures'][b['index']]['source'];roles[imindex]=name;assert b.get('texCoord',0)==0
rows=[]
for imindex,role in roles.items():
 im=doc['images'][imindex];viewindex=im['bufferView'];v=doc['bufferViews'][viewindex];payload=h.view_bytes(binary,v);before=payload;image=Image.open(io.BytesIO(payload));size=list(image.size);mode=image.mode;fmt=image.format;assert mode=='RGB' and fmt in ('PNG','JPEG')
 if max(image.size)>2048:
  assert image.size==(4096,4096);image.thumbnail((2048,2048),Image.Resampling.LANCZOS);stream=io.BytesIO();image.save(stream,format='PNG',optimize=True);payload=stream.getvalue();output['images'][imindex]['mimeType']='image/png'
 else:assert image.size==(2048,2048)
 outbin.extend(b'\0'*((-len(outbin))%4));output['bufferViews'][viewindex]={'buffer':0,'byteOffset':len(outbin),'byteLength':len(payload)};outbin.extend(payload)
 after=Image.open(io.BytesIO(payload));assert after.size==(2048,2048) and after.mode==mode
 rows.append({'image_index':imindex,'role':role,'size_before':size,'size_after':list(after.size),'format_before':fmt,'format_after':after.format,'RGB_preserved':True,'bytes_before':len(before),'bytes_after':len(payload),'sha256_before':sha(before),'sha256_after':sha(payload),'byte_identical_if_already_2K':payload==before if size==[2048,2048] else None})
output['buffers'][0]['byteLength']=len(outbin);encoded=json.dumps(output,separators=(',',':'),ensure_ascii=False).encode();encoded+=b' '*((-len(encoded))%4);outbin.extend(b'\0'*((-len(outbin))%4));packed=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(outbin))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(outbin),0x004e4942)+outbin
OUT.write_bytes(packed);actual,actualbin=h.read_glb(OUT.read_bytes());assert actualbin[:len(base)]==base
unchanged=sorted(doc.keys()-{'images','bufferViews','buffers'});assert all(actual[k]==doc[k] for k in unchanged)
assert actual['meshes']==original['meshes'] and actual['accessors']==original['accessors'] and actual['animations']==original['animations'] and actual['nodes']==original['nodes']
for row in rows:
 im=actual['images'][row['image_index']];payload=h.view_bytes(actualbin,actual['bufferViews'][im['bufferView']]);assert sha(payload)==row['sha256_after'];assert Image.open(io.BytesIO(payload)).size==(2048,2048)
counts=[actual['accessors'][p['indices']]['count']//3 for m in actual['meshes'] for p in m['primitives']];assert counts==[2664,2132,1132]
r={'schema':'F01-whole-three-mesh-2K/v1','source4K_sha256':sha(SOURCE.read_bytes()),'output':{'path':str(OUT.relative_to(ROOT)),'sha256':sha(packed),'bytes':len(packed)},'canonical_BIN':{'bytes':len(base),'sha256':sha(base),'literal_preserved':True},'unchanged_sections':unchanged,'maps':rows,'geometry':{'meshes':3,'triangles':5928,'both_UVs_nodes_normals_indices_transforms_3clips_byteidentical':True,'newPBR_UV':0,'oldmetric_UV1_retained':True},'status':'Source-only2Kcandidate; actualviews/rootart/nativepending','new_POSTs':0,'canonical_runtime_written':False}
(HERE/'2k-receipt.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r['output']))
