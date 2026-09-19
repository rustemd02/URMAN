"""Read-only semantic comparison of the retained B29 terrain and a new export."""
import hashlib
import json
import pathlib
import struct
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
EVIDENCE = pathlib.Path(__file__).resolve().parent
TYPES = {5126: 'f', 5125: 'I', 5123: 'H', 5122: 'h', 5121: 'B', 5120: 'b'}
WIDTHS = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def fingerprint(value):
    return hashlib.sha256(repr(value).encode()).hexdigest()


def inspect(path):
    raw = path.read_bytes()
    size = struct.unpack_from('<I', raw, 12)[0]
    document = json.loads(raw[20:20 + size])
    data = raw[28 + size:]

    def accessor(index):
        item = document['accessors'][index]
        view = document['bufferViews'][item['bufferView']]
        fmt = '<' + TYPES[item['componentType']] * WIDTHS[item['type']]
        stride = view.get('byteStride', struct.calcsize(fmt))
        start = view.get('byteOffset', 0) + item.get('byteOffset', 0)
        return [struct.unpack_from(fmt, data, start + n * stride) for n in range(item['count'])]

    result = {}
    for node in document['nodes']:
        if 'mesh' not in node:
            continue
        primitives = []
        for primitive in document['meshes'][node['mesh']]['primitives']:
            arrays = {key: accessor(index) for key, index in primitive['attributes'].items()}
            vertices = arrays['POSITION']
            indices = [item[0] for item in accessor(primitive['indices'])]
            triangles = sorted(tuple(sorted(vertices[i] for i in indices[k:k + 3]))
                               for k in range(0, len(indices), 3))
            attributes = sorted((vertices[i], tuple((key, arrays[key][i]) for key in sorted(arrays)
                                 if key not in ('NORMAL', 'TANGENT'))) for i in range(len(vertices)))
            normals = arrays.get('NORMAL', [])
            primitives.append({
                'triangles': len(indices) // 3,
                'positions': fingerprint(sorted(set(vertices))),
                'triangleGeometry': fingerprint(triangles),
                'attributesExceptNormals': fingerprint(attributes),
                'raw': fingerprint((arrays, indices)),
                'normalYRange': [min(n[1] for n in normals), max(n[1] for n in normals)],
                'downwardNormals': sum(n[1] < -.05 for n in normals),
                'material': document['materials'][primitive['material']],
            })
        result[node['name']] = {
            'transform': {key: node[key] for key in ('translation', 'rotation', 'scale', 'matrix') if key in node},
            'primitives': primitives,
        }
    return result


def main():
    output = EVIDENCE / sys.argv[1]
    assert not output.exists(), 'Never overwrite historical evidence'
    old = inspect(EVIDENCE / 'terrain-export-30-before/agentb_terrain_road_kit.glb')
    current = ROOT / 'game/assets/models/agent_b_act1/agentb_terrain_road_kit.glb'
    new = inspect(current)
    changes = []
    for name in sorted(old.keys() | new.keys()):
        if old.get(name) == new.get(name):
            continue
        before, after = old.get(name), new.get(name)
        expected = name.startswith(('Grade_', 'Ditch_FieldBank_')) or name in (
            'BabaiYard_EastFenceSwale', 'BabaiYard_WestDepthBank')
        preserved = {key: bool(before and after and
                     [p[key] for p in before['primitives']] == [p[key] for p in after['primitives']])
                     for key in ('triangles', 'positions', 'triangleGeometry', 'attributesExceptNormals', 'material')}
        preserved['transform'] = bool(before and after and before['transform'] == after['transform'])
        changes.append({'name': name, 'expectedLowBank': expected, 'preserved': preserved,
                        'before': before, 'after': after})
    unexpected = [item['name'] for item in changes
                  if not item['expectedLowBank'] or not all(item['preserved'].values())]
    report = {'sha256': hashlib.sha256(current.read_bytes()).hexdigest(),
              'meshNamesUnchanged': old.keys() == new.keys(), 'meshCount': len(new),
              'triangles': sum(p['triangles'] for mesh in new.values() for p in mesh['primitives']),
              'unchangedMeshes': len(new) - len(changes), 'changes': changes,
              'unexpected': unexpected, 'nativeVisualAcceptance': 'not-run'}
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({key: value for key, value in report.items() if key != 'changes'}
                     | {'changedNames': [item['name'] for item in changes]}, indent=2))
    return 0 if not unexpected and report['meshNamesUnchanged'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
