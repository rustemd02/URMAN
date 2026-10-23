"""Neutral untextured upload material only; preserve complete canonical donor BIN.

The canonical old bark material explicitly usesUV1. This upload removes that
binding so the requested keepUV retexture targets primaryUV0. Both UV attributes,
all geometry/normals/indices/transforms/animations and oldunusedimagebytes remain
literal. Returned geometry never becomes authoritative. No runtime write.


Archived prerequisites: evidence/PW-093/prologue-branch-material-source-v1-20261023.tar.xz (repository-relative).
The canonical input must still match060b8; its frozen snapshot is archive member canonical-prologue-branch-v1.glb. Output donor/upload-receipt must be absent before replay. No paid call is needed or authorized.
Extract named members with tar -xJf ARCHIVE -C THIS_RECIPE_FOLDER MEMBER... .
"""
from pathlib import Path
import json,hashlib,struct,copy
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
SOURCE=ROOT/'game/assets/photoworlds/branch/prologue-branch-v1.glb';OUT=HERE/'f01-neutral-upload-donor.glb';RECEIPT=HERE/'upload-receipt.json'
def sha(b):return hashlib.sha256(b).hexdigest()
def read(b):
 assert struct.unpack_from('<III',b)==(0x46546c67,2,len(b));n,t=struct.unpack_from('<II',b,12);assert t==0x4e4f534a;size,t=struct.unpack_from('<II',b,20+n);assert t==0x004e4942;assert len(b)==28+n+size;return json.loads(b[20:20+n]),b[28+n:]
assert not OUT.exists() and not RECEIPT.exists()
original=SOURCE.read_bytes();assert sha(original)=='060b8af9939754ba3f6de866b703430f6f22a6498733104c253b5ff451b1e742'
doc,binary=read(original);output=copy.deepcopy(doc)
assert len(doc['materials'])==1 and doc['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['texCoord']==1
for m in output['materials']:
 m['pbrMetallicRoughness']={'baseColorFactor':[.5,.5,.5,1],'metallicFactor':0,'roughnessFactor':.88}
assert all(output[k]==v for k,v in doc.items() if k!='materials')
encoded=json.dumps(output,separators=(',',':'),ensure_ascii=False).encode();encoded+=b' '*((-len(encoded))%4)
packed=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
OUT.write_bytes(packed);actual,actualbin=read(OUT.read_bytes());assert actualbin==binary and actual==output
proof={'schema':'f01-neutral-material-upload/v1','canonical':{'path':str(SOURCE.relative_to(ROOT)),'sha256':sha(original),'bytes':len(original)},'upload':{'path':str(OUT.relative_to(ROOT)),'sha256':sha(packed),'bytes':len(packed)},'changed_only':'One materialoldUV1texturebinding→plainneutralgreyPBR; no imagetexturebinding. BothTEXCOORD0and1andallmesh/node/accessor/animation/bufferJSONremainidentical.','BIN_bytes':len(binary),'BIN_sha256':sha(binary),'BIN_literal_identical':True,'UV_target':'PrimaryTEXCOORD0 unique57islands; UV1metricretainedandunreferencedbyuploadmaterial. Actualreturnedmapalignmentmustbereviewed.','runtime_changed':False,'meshyposts':0}
RECEIPT.write_text('{\n'+',\n'.join('  '+json.dumps(k)+': '+json.dumps(v,separators=(',',':')) for k,v in proof.items())+'\n}\n');print(json.dumps(proof['upload']))
