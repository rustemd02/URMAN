"""Existing literal-GLB maps-only binder; canonical source never written.

Reproduction restores only locked inputs from the local archive named in the
compact candidate receipt. This Blender script makes a new source-only4K output;
it does not alter the canonical runtimeasset. Both sourceUVs and3clips stayliteral.


Archived prerequisites: evidence/PW-093/prologue-branch-material-source-v1-20261023.tar.xz (repository-relative).
Before replay extract f01-neutral-upload-donor.glb, meshy-returned.glb and download.json from the archive into this recipe folder; the4K output must be absent. The shared canonical input must still match060b8; never overwrite a later canonical asset.
Extract named members with tar -xJf ARCHIVE -C THIS_RECIPE_FOLDER MEMBER... .
"""
from pathlib import Path
import bpy,json,hashlib,importlib.util,sys
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
SOURCE=ROOT/'game/assets/photoworlds/branch/prologue-branch-v1.glb';RETURNED=HERE/'meshy-returned.glb';OUT=HERE/'f01-branch-whole-pbr-4k-v1.glb'
BINDER=ROOT/'assets/source/blender/act1/rough_picket_family_reference_v3/pbr_candidate_v3/attach_maps.py'
spec=importlib.util.spec_from_file_location('existing_literal_pbr_writer',BINDER);b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
b.SOURCE=SOURCE;b.RETURNED=RETURNED;b.GLB=OUT
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not OUT.exists()
assert sha(SOURCE)=='060b8af9939754ba3f6de866b703430f6f22a6498733104c253b5ff451b1e742'
download=json.loads((HERE/'download.json').read_text());assert sha(RETURNED)==download['sha256']
proof=b.write_material_only();doc=b.a.glb_document(OUT);original=b.a.glb_document(SOURCE)
assert doc['animations']==original['animations'] and len(doc['animations'])==3
assert doc['meshes']==original['meshes'] and doc['accessors']==original['accessors']
for mesh in doc['meshes']:
 for q in mesh['primitives']:assert 'TEXCOORD_0' in q['attributes'] and 'TEXCOORD_1' in q['attributes']
assert len(doc['materials'])==1
for binding in [doc['materials'][0]['pbrMetallicRoughness']['baseColorTexture'],doc['materials'][0]['pbrMetallicRoughness']['metallicRoughnessTexture'],doc['materials'][0]['normalTexture']]:assert binding.get('texCoord',0)==0
proof['changed_json_scope']='AppendonlygeneratedPBRimagebytes/bindings; intentionaloldbarkmaterialUV1→newPBRUV0. OriginaldonorBIN/allmeshes/nodes/accessors/TEXCOORD0and1/normals/indices/transforms/3animationsliteralunchanged;returnedMeshygeometrydiscarded.'
r={'schema':'f01-literal-three-mesh-PBR/v1','canonical_sha256':sha(SOURCE),'upload_sha256':sha(HERE/'f01-neutral-upload-donor.glb'),'returned':download,'output':{'path':str(OUT.relative_to(ROOT)),'sha256':sha(OUT),'bytes':OUT.stat().st_size},'task':{'id':'01a16a54-4ed5-72da-9272-9bc8ef087354','status':'SUCCEEDED','consumed_credits':10,'POSTs':1},'proof':proof,'clips':[x['name'] for x in doc['animations']],'material_sampling':'AllgeneratedmapsUV0;oldUV1attributeretainedliteral','state':'Sourceonly4Kcandidate;2Kprep/actualviews/rootreviewpending;noart/nativePASS','canonical_unchanged':True,'new_calls':0}
(HERE/'attachment-receipt.json').write_text(json.dumps(r,indent=2)+'\n');print('F01_PBR4K',json.dumps(r['output']),flush=True)
