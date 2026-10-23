"""Existing H049 direct-GLB 2K pattern; preserve existing2K packed data map bytes.

Reproduction prerequisite (archive SHA is in compact candidate-receipt.json):
tar -xJf evidence/PW-093/babai-side-gate-source-v1.tar.xz \
  -C assets/source/blender/act1/babai_side_gate_meshy_v1 \
  babai-side-gate-wood-pbr-v1.glb babai-side-gate-wood-donor.glb
Restore only absent inputs and verify their SHA-locked recipe identities.
The runtime output must be absent: this recipe refuses to overwrite it.
Regeneration writes its full runtime receipt; final compact provenance is selected
by root after an accepted stage. No restoration mode or fallback is introduced.
"""
from pathlib import Path
import json,hashlib,copy,io,struct,importlib.util,sys
import PIL
from PIL import Image
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
METHOD=HERE.parent/'h049_outbuilding_multiview_v1/generation/prepare_h049_runtime.py'
spec=importlib.util.spec_from_file_location('existing_direct_glb_downsample',METHOD);h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
SOURCE=HERE/'babai-side-gate-wood-pbr-v1.glb';OUT=ROOT/'game/assets/models/act1/babai-side-gate-wood-pbr-2k-v1.glb';RECEIPT=HERE/'runtime-2k-receipt.json'
def sha(b):return hashlib.sha256(b).hexdigest()
def metadata(f):return {'path':str(f.relative_to(ROOT)),'sha256':sha(f.read_bytes()),'bytes':f.stat().st_size}
assert not OUT.exists() and not RECEIPT.exists()
assert sha(SOURCE.read_bytes())=='7a3d1975718b63885131e7cf8640b0e76f23b010cc946f2b0e180064676e4597'
doc,binary=h.read_glb(SOURCE.read_bytes());assert len(doc['images'])==3 and len(doc['meshes'])==8
image_views={x['bufferView'] for x in doc['images']};roles={}
for material in doc['materials']:
 for role,binding in [('basecolor_sRGB',material['pbrMetallicRoughness']['baseColorTexture']),('metallicRoughness_linear_data',material['pbrMetallicRoughness']['metallicRoughnessTexture']),('normal_linear_data',material['normalTexture'])]:
  im=doc['textures'][binding['index']]['source'];roles.setdefault(doc['images'][im]['bufferView'],set()).add(role)
output=copy.deepcopy(doc);outbin=bytearray();image_rows=[];other_rows=[]
for index,view in enumerate(doc['bufferViews']):
 payload=h.view_bytes(binary,view);original=payload
 if index in image_views:
  imindex=next(i for i,im in enumerate(doc['images']) if im['bufferView']==index);image=Image.open(io.BytesIO(payload));before_size=list(image.size);before_mode=image.mode;before_format=image.format;assert before_format=='JPEG' and before_mode=='RGB'
  if max(image.size)>2048:
   assert image.size==(4096,4096) and roles[index] in ({'basecolor_sRGB'},{'normal_linear_data'})
   image.thumbnail((2048,2048),Image.Resampling.LANCZOS);encoded=io.BytesIO();image.save(encoded,format='PNG',optimize=True);payload=encoded.getvalue();output['images'][imindex]['mimeType']='image/png';action='JPEG decodedRGB→max2048 LANCZOS→losslessPNG; originalRGBchannels only'
  else:
   assert image.size==(2048,2048) and roles[index]=={'metallicRoughness_linear_data'};action='original packed2K JPEG preserved byte-identical'
  after=Image.open(io.BytesIO(payload));assert after.size==(2048,2048) and after.mode==before_mode
  image_rows.append({'image_index':imindex,'buffer_view':index,'roles':sorted(roles[index]),'size_before':before_size,'size_after':list(after.size),'mode_before_after':before_mode,'format_before':before_format,'format_after':after.format,'action':action,'bytes_before':len(original),'bytes_after':len(payload),'sha256_before':sha(original),'sha256_after':sha(payload)})
 else:other_rows.append({'buffer_view':index,'bytes':len(payload),'sha256':sha(payload)})
 outbin.extend(b'\0'*((-len(outbin))%4));output['bufferViews'][index]['byteOffset']=len(outbin);output['bufferViews'][index]['byteLength']=len(payload);outbin.extend(payload)
output['buffers'][0]['byteLength']=len(outbin);encoded=json.dumps(output,separators=(',',':'),ensure_ascii=False).encode();encoded+=b' '*((-len(encoded))%4);outbin.extend(b'\0'*((-len(outbin))%4))
packed=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(outbin))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(outbin),0x004e4942)+outbin
OUT.parent.mkdir(parents=True,exist_ok=True)
with OUT.open('xb') as f:f.write(packed)
actual,actualbin=h.read_glb(OUT.read_bytes());unchanged=sorted(doc.keys()-{'bufferViews','buffers','images'});assert all(actual[k]==doc[k] for k in unchanged)
assert all({k:v for k,v in new.items() if k!='mimeType'}=={k:v for k,v in old.items() if k!='mimeType'} for old,new in zip(doc['images'],actual['images']))
for index,view in enumerate(doc['bufferViews']):
 if index not in image_views:assert h.view_bytes(binary,view)==h.view_bytes(actualbin,actual['bufferViews'][index])
for row in image_rows:
 payload=h.view_bytes(actualbin,actual['bufferViews'][row['buffer_view']]);assert sha(payload)==row['sha256_after'];assert Image.open(io.BytesIO(payload)).size==(2048,2048)
primitive_counts=[actual['accessors'][q['indices']]['count']//3 for m in actual['meshes'] for q in m['primitives']];assert primitive_counts==[12]*8
original_donor=HERE/'babai-side-gate-wood-donor.glb';donor_doc,donor_bin=h.read_glb(original_donor.read_bytes());assert actualbin[:len(donor_bin)]==donor_bin
receipt={'schema':'babai-side-gate-runtime-2k/v1','state':'runtime_asset_prepared_readback_verified_native_pending','input':metadata(SOURCE),'output':metadata(OUT),'method_reference':metadata(METHOD),'producer':metadata(Path(__file__)),'pillow_version':PIL.__version__,'method':'Existing direct GLB buffer-view repack with Pillow max2048 LANCZOS; only4Kbasecolor+normal resized, original2KpackedMR unchanged. No recolor, channel swizzle, normal reorientation, UV rebake or geometry export.','images':image_rows,'unchanged_json_sections':unchanged,'all_nonimage_buffer_views_byte_identical':other_rows,'literal_original_donor_bin_prefix_sha256':sha(donor_bin),'literal_original_donor_bin_prefix_bytes':len(donor_bin),'literal_original_donor_bin_prefix_preserved':True,'node_names':[n.get('name') for n in actual['nodes']],'geometry':{'meshes':8,'triangles':96,'primitive_triangle_counts':primitive_counts,'hinge_origin_unchanged':True,'positions_normals_UVs_indices_transforms_materialslots_texturebindings_identical_to_4k':True},'additional_meshy_calls':0,'additional_credits':0,'scope':'Only namedruntimeassetwritten; no C#world/state/hardware/collision changes, no Godot/game/build/import/native acceptance.','remaining':'Root ownsruntimeintegrationandfullscenephysical/native/art/performance acceptance. Original4K andrawinputsnotdeleted; compactarchive afterrootpointer approval.'}
RECEIPT.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'output':receipt['output'],'triangles':96,'maps':[r['size_after'] for r in image_rows],'donor_bin_literal':True},indent=2))
