"""Fetch the explicitly licensed inputs in the police manifest; verify hashes.

Usage: python3 tools/blender/fetch_police_sources.py /absolute/output-directory
Then run prepare_police_assets.py with that directory as --source.
"""
import hashlib
import json
import subprocess
import sys
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[2]
target=Path(sys.argv[1]).resolve();target.mkdir(parents=True,exist_ok=True)
manifest=json.loads((root/'game/assets/third_party/police/manifest.json').read_text())


def download(url,path,digest,algorithm='sha256'):
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists() or hashlib.new(algorithm,path.read_bytes()).hexdigest()!=digest:
        subprocess.run(['curl','--fail','--location','--retry','2',url,'--output',str(path)],check=True)
    if hashlib.new(algorithm,path.read_bytes()).hexdigest()!=digest:
        raise SystemExit('Source checksum mismatch: '+str(path))


for entry in manifest['models']:
    name=Path(entry['runtime']).stem
    if name in {'vaz2106_static','rotary_phone'}:
        filename='vaz2106-original.glb' if name=='vaz2106_static' else 'rotary-phone-original.glb'
        download(entry['download'],target/filename,entry['source_sha256'])
    elif name.startswith('office_'):
        archive=target/'OfficeSetcc0.zip'
        download(entry['download'],archive,entry['archive_sha256'])
        with zipfile.ZipFile(archive) as source:
            for member in ['OfficeSet/License.txt','OfficeSet/objfiles/chair.obj','OfficeSet/objfiles/dining_chair.obj',
                           'OfficeSet/textures/chair.png','OfficeSet/textures/dining_chair.png']:
                output=target/member;output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(source.read(member))
    else:
        source=entry['download_manifest'];folder=target/name
        download(source['url'],folder/(name+'_1k.gltf'),entry['source_sha256'])
        for relative,item in source['include'].items():
            download(item['url'],folder/relative,item['md5'],'md5')
print('police-sources: verified licensed inputs; runtime attribution in game/content/credits.ru.txt')
