"""Read actual exported GLB geometry; preserve a pre-export component baseline.

This is an export check. It does not substitute for Godot contacts or visual QA.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path


def glb(path):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from('<III', raw)
    assert magic == 0x46546c67 and version == 2 and total == len(raw)
    offset = 12
    document = binary = None
    while offset < len(raw):
        size, kind = struct.unpack_from('<II', raw, offset)
        offset += 8
        chunk = raw[offset:offset + size]
        if kind == 0x4e4f534a:
            document = json.loads(chunk)
        elif kind == 0x004e4942:
            binary = chunk
        offset += size
    assert document is not None and binary is not None
    return document, binary


def geometry_snapshot(path, root_name='URMAN_FapClinicKit'):
    doc, binary = glb(path)
    nodes = doc['nodes']
    by_name = {node['name']: node for node in nodes}

    def accessor(index):
        a = doc['accessors'][index]
        view = doc['bufferViews'][a['bufferView']]
        component_bytes = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[a['componentType']]
        components = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
        element_bytes = component_bytes * components
        stride = view.get('byteStride', element_bytes)
        offset = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        payload = b''.join(binary[offset + n * stride:offset + n * stride + element_bytes] for n in range(a['count']))
        return {'type': a['type'], 'componentType': a['componentType'], 'count': a['count'], 'sha256': hashlib.sha256(payload).hexdigest()}

    def shape(node):
        record = {key: node[key] for key in ('name', 'translation', 'rotation', 'scale', 'matrix') if key in node}
        if 'mesh' in node:
            record['primitives'] = []
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                part = {'attributes': {key: accessor(value) for key, value in primitive['attributes'].items()}}
                if 'indices' in primitive:
                    part['indices'] = accessor(primitive['indices'])
                record['primitives'].append(part)
        if 'children' in node:
            record['children'] = sorted((shape(nodes[index]) for index in node['children']), key=lambda item: item['name'])
        return record

    root = by_name[root_name]
    components = {}
    for index in root['children']:
        component = nodes[index]
        record = shape(component)
        components[component['name']] = {'geometry_sha256': hashlib.sha256(json.dumps(record, sort_keys=True).encode()).hexdigest(), 'direct_children': len(component.get('children', []))}
    mesh_geometry = {name: hashlib.sha256(json.dumps(shape(node), sort_keys=True).encode()).hexdigest()
                     for name, node in by_name.items() if 'mesh' in node}
    return {'glb_sha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'components': components,
            'node_names': sorted(by_name), 'mesh_geometry': mesh_geometry}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', action='store_true')
    args = parser.parse_args()
    folder = Path(__file__).resolve().parent
    repo = folder.parents[2]
    model = repo / 'game/assets/models/act1/urman_fap_clinic_kit.glb'
    snapshot = geometry_snapshot(model)
    baseline = folder / 'fap-export-06-baseline.json'
    if args.baseline:
        assert not baseline.exists(), 'Preserve existing baseline; do not overwrite it.'
        baseline.write_text(json.dumps(snapshot, indent=2) + '\n')
        print(json.dumps({'baseline': str(baseline), 'glb_sha256': snapshot['glb_sha256'], 'components': snapshot['components']}, indent=2))
        return
    previous = json.loads(baseline.read_text())
    changed = [name for name, item in snapshot['components'].items() if previous['components'].get(name, {}).get('geometry_sha256') != item['geometry_sha256']]
    expected = {'FapFacade_Main', 'FapInteriorSet'}
    failures = []
    if set(snapshot['components']) != set(previous['components']):
        failures.append('Component roots changed.')
    if not set(changed) <= expected:
        failures.append('Geometry changed outside the facade/interior scope: ' + ', '.join(sorted(set(changed) - expected)))
    missing = sorted(set(previous['node_names']) - set(snapshot['node_names']))
    if missing:
        failures.append('Existing nodes disappeared: ' + ', '.join(missing))
    snapshot.update({'changed_geometry_components': changed, 'existing_node_names_preserved': not missing, 'failures': failures, 'source_sha256': hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.py').read_bytes()).hexdigest(), 'blend_sha256': hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.blend').read_bytes()).hexdigest(), 'validation_scope': 'Actual exported geometry and component preservation only; runtime, physical contacts and artistic acceptance pending.'})
    receipt = folder / 'fap-export-06-geometry-receipt.json'
    receipt.write_text(json.dumps(snapshot, indent=2) + '\n')
    print(json.dumps({key: snapshot[key] for key in ('glb_sha256', 'source_sha256', 'blend_sha256', 'changed_geometry_components', 'existing_node_names_preserved', 'failures')}, indent=2))
    raise SystemExit(1 if failures else 0)


if __name__ == '__main__':
    main()
