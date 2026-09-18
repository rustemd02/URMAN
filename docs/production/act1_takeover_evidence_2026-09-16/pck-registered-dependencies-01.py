import ast, contextlib, hashlib, io, json, re, struct, tempfile
from pathlib import Path

script = Path('eng/verify-act1-release-package.sh').read_text()
blocks = re.findall(r"<<'PY'\n(.*?)\nPY", script, re.S)
for index, block in enumerate(blocks):
    compile(block, f'verify-act1-release-package.sh:python{index}', 'exec')
print('PASS all embedded Python blocks compile:', len(blocks))
block = next(block for block in blocks if 'from __future__ import annotations' in block)
prefix = block.split('\nfor pck_path in map(Path, sys.argv[1:]):', 1)[0]
prefix = prefix.replace('sys.path.insert(0, sys.argv.pop(1))',
                        'sys.path.insert(0, str(Path("eng").resolve()))')
scope = {'__name__': 'pck_contract_fixture'}
exec(compile(prefix, '<actual release verifier definitions>', 'exec'), scope)
base = {}
def add(name, data=b'fixture-presence-only'):
    if name in base:
        assert base[name] == data, name
    base[name] = data
for scene in scope['required_scene_names']:
    add(f'scenes/{scene}.tscn.remap')
    add('.godot/exported/fixture-' + scene.rsplit('/', 1)[-1] + '.scn')
add('project.binary')
add('content/credits.ru.txt')
content = 'content/urman.chapter1.compiled.v1.json'
add(content, Path('game', content).read_bytes())
for root, kit in (*scope['act1_kits'], *scope['agent_b_kits']):
    add(f'{root}/{kit}.glb.import')
    add(f'.godot/imported/{kit}.glb-fixture.scn')
add('assets/audio/ambient_manifest.json')
for root, stems in [
    ('assets/audio', scope['ambient_stems']),
    ('assets/audio/act1/foley', scope['foley_stems']),
    ('assets/audio/act1/footsteps', scope['footstep_stems']),
]:
    for stem in stems:
        add(f'{root}/{stem}.wav.import')
        add(f'.godot/imported/{stem}.wav-fixture.sample')
for texture in scope['production_textures']:
    add(f'assets/textures/painterly/{texture}.png.import')
    add(f'.godot/imported/{texture}.png-fixture.ctex')
add('assets/textures/ui/act1_menu_winter_v1.png.import')
add('.godot/imported/act1_menu_winter_v1.png-fixture.ctex')
for name in ['act1_menu_winter_v1', 'urman_app_icon_v1']:
    add(f'assets/textures/ui/{name}.png', b'\x89PNG\r\n\x1a\nfixture')
add('assets/models/act1/urman_winter_pine_Leaf_Pine_C.png.import')
add('.godot/imported/urman_winter_pine_Leaf_Pine_C.png-fixture.ctex')
for name in ['content/vehicles/act1_vehicles.v1.json', 'content/vehicles/avyl_radio.v1.json']:
    add(name, Path('game', name).read_bytes())
radio = json.loads(base['content/vehicles/avyl_radio.v1.json'])
paths = sorted({row['streamPath'].removeprefix('res://') for row in radio['segments']})
assert len(radio['segments']) == 12 and len(paths) == 11
assets = json.loads(base[content])['registries']['assets']
photo_ids = {'urman.chapter1:asset/school-class-photo', 'urman.chapter1:asset/council-photo-album'}
photo_paths = ['assets/' + asset['file'] for asset in assets if asset['id'] in photo_ids]
assert len(photo_paths) == 2
remaps = {}
for name in paths + photo_paths:
    data = Path('game', name + '.import').read_bytes()
    add(name + '.import', data)
    targets = re.findall(r'^path(?:\.[A-Za-z0-9_]+)?="res://([^"]+)"$', data.decode(), re.M)
    assert targets, name
    remaps[name] = targets
    for target in targets:
        add(target)
assert not any(name in base for name in paths + photo_paths)

def pack(mapping):
    raw = bytearray(0x70)
    raw[:4] = b'GDPC'
    struct.pack_into('<I', raw, 4, 4)
    indexed = []
    for name, data in mapping.items():
        offset = len(raw) - 0x70
        raw.extend(data)
        indexed.append((name, offset, data))
    index_base = len(raw)
    struct.pack_into('<QQ', raw, 0x18, 0x70, index_base)
    raw.extend(struct.pack('<I', len(indexed)))
    for name, offset, data in indexed:
        encoded = name.encode() + b'\0'
        encoded += b'\0' * ((-len(encoded)) % 4)
        raw.extend(struct.pack('<I', len(encoded)) + encoded)
        raw.extend(struct.pack('<QQ16sI', offset, len(data), hashlib.md5(data).digest(), 0))
    return bytes(raw)

with tempfile.TemporaryDirectory(prefix='urman-pck-registry-fixture-') as folder:
    path = Path(folder) / 'fixture.pck'
    def validate(mapping):
        path.write_bytes(pack(mapping))
        raw, entries = scope['read_pck'](path)
        scope['assert_scope'](raw, entries, 'synthetic-presence-fixture')
    validate(base)
    print('PASS real PCK-v4 parser + actual assert_scope; 2 JSON, 12 rows/11 imported WAV, 2 imported PNG; no raw WAV/PNG needed')
    cases = []
    def remove(name):
        return lambda m: m.pop(name)
    def replace(name, data):
        return lambda m: m.__setitem__(name, data)
    for name in ['content/vehicles/act1_vehicles.v1.json', 'content/vehicles/avyl_radio.v1.json']:
        cases.append(('missing ' + name, remove(name), 'missing or empty'))
    cases.append(('stale JSON', replace('content/vehicles/avyl_radio.v1.json', b'{}'), 'stale vehicle/radio'))
    for name, suffix in [(paths[0], '.sample'), (photo_paths[0], '.ctex')]:
        target = remaps[name][0]
        cases.extend([
            ('missing import ' + suffix, remove(name + '.import'), 'missing or empty'),
            ('missing target ' + suffix, remove(target), 'missing or empty'),
            ('empty target ' + suffix, replace(target, b''), 'missing or empty'),
            ('wrong remap ' + suffix, replace(name + '.import', b'[remap]\npath="res://.godot/imported/unrelated-fixture' + suffix.encode() + b'"\n'), 'unexpected imported resource remap'),
        ])
    cases.append(('missing remap path', replace(paths[0] + '.import', b'[remap]\n'), 'no export remap'))
    cases.append(('invalid remap UTF8', replace(paths[0] + '.import', b'\xff'), 'not UTF-8'))
    def omit_photo(mapping):
        document = json.loads(mapping[content])
        document['registries']['assets'] = [a for a in document['registries']['assets'] if a['id'] != 'urman.chapter1:asset/school-class-photo']
        mapping[content] = json.dumps(document, ensure_ascii=False).encode()
    cases.append(('missing public asset record', omit_photo, 'public photograph is missing or stale'))
    def stale_photo(mapping):
        document = json.loads(mapping[content])
        next(a for a in document['registries']['assets'] if a['id'] == 'urman.chapter1:asset/council-photo-album')['file'] = 'images/wrong.png'
        mapping[content] = json.dumps(document, ensure_ascii=False).encode()
    cases.append(('stale public asset path', stale_photo, 'public photograph is missing or stale'))
    for label, mutation, expected in cases:
        mapping = dict(base)
        mutation(mapping)
        error = io.StringIO()
        try:
            with contextlib.redirect_stderr(error), contextlib.redirect_stdout(io.StringIO()):
                validate(mapping)
        except SystemExit as exc:
            assert exc.code == 1 and expected in error.getvalue(), (label, str(exc), error.getvalue())
        else:
            raise AssertionError('unexpected acceptance: ' + label)
        print('PASS refusal:', label)
    for invalid in ['../escape.wav', '/absolute.wav', 'res://bad.wav', 'assets/../escape.wav', 'assets\\escape.wav', '', None]:
        error = io.StringIO()
        try:
            with contextlib.redirect_stderr(error):
                scope['require_imported_resource'](b'', {}, invalid, '.sample')
        except SystemExit as exc:
            assert exc.code == 1 and 'invalid registered resource path' in error.getvalue()
        else:
            raise AssertionError('unexpected invalid path acceptance')
    print('PASS 7 invalid registered-path refusals')
    mapping = dict(base)
    mapping['../escape.sample'] = b'x'
    path.write_bytes(pack(mapping))
    try:
        scope['read_pck'](path)
    except ValueError as exc:
        assert 'unsafe resource path' in str(exc)
    else:
        raise AssertionError('PCK unsafe directory entry accepted')
    print('PASS unsafe PCK-v4 directory entry rejected by existing reader')
print('PASS', len(cases) + 9, 'bounded cases; synthetic structural fixtures only; native export/listening/art not run')
