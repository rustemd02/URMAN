"""Read completed 08b binary: exact scope, closed oriented surfaces and normals."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct

from fap_export_geometry_receipt import geometry_snapshot, glb


folder = Path(__file__).resolve().parent
repo = folder.parents[2]
model = repo / 'game/assets/models/act1/urman_fap_clinic_kit.glb'
before = json.loads((folder / 'fap-export-08b-baseline.json').read_text())
launch = json.loads((folder / 'fap-export-08b-launch.json').read_text())
after = geometry_snapshot(model)
expected = {
    'FapFacade_Gable_LOD0', 'FapServiceShed_Roof_LOD0',
    'FapInteriorScreen_PanelLeft_LOD0', 'FapInteriorScreen_PanelCenter_LOD0',
    'FapInteriorScreen_PanelRight_LOD0', 'FapInteriorTrolley_Handle_LOD0',
    'FapInteriorWashUnit_Towel_LOD0', 'FapInteriorWashUnit_Towel_LOD1',
}
changed = sorted(name for name, signature in after['mesh_geometry'].items()
                 if before['mesh_geometry'].get(name) != signature)
failures = []
if set(changed) != expected:
    failures.append('Changed member set differs from all affected helper callers: ' + ', '.join(sorted(set(changed) ^ expected)))
if after['node_names'] != before['node_names']:
    failures.append('Published node names changed.')
doc, binary = glb(model)


def accessor(index):
    info = doc['accessors'][index]
    view = doc['bufferViews'][info['bufferView']]
    kind = {5121: 'B', 5123: 'H', 5125: 'I', 5126: 'f'}[info['componentType']]
    length = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[info['type']]
    record = struct.Struct('<' + kind * length)
    stride = view.get('byteStride', record.size)
    offset = view.get('byteOffset', 0) + info.get('byteOffset', 0)
    return [record.unpack_from(binary, offset + i * stride) for i in range(info['count'])]


def subtract(a, b):
    return tuple(x - y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


checks = []
for node in doc['nodes']:
    if node.get('name') not in expected:
        continue
    edges = Counter()
    volume = 0.0
    triangles = 0
    flipped_vertex_normals = 0
    for primitive in doc['meshes'][node['mesh']]['primitives']:
        points = accessor(primitive['attributes']['POSITION'])
        normals = accessor(primitive['attributes']['NORMAL'])
        indices = [value[0] for value in accessor(primitive['indices'])]
        for start in range(0, len(indices), 3):
            ids = indices[start:start + 3]
            a, b, c = [points[index] for index in ids]
            normal = cross(subtract(b, a), subtract(c, a))
            if dot(normal, normal) < 1e-20:
                continue
            volume += dot(a, cross(b, c)) / 6.0
            triangles += 1
            flipped_vertex_normals += sum(dot(normals[index], normal) < -1e-10 for index in ids)
            welded = [tuple(round(value, 7) for value in point) for point in (a, b, c)]
            for i in range(3):
                edges[(welded[i], welded[(i + 1) % 3])] += 1
    unmatched_edges = sum(count for (a, b), count in edges.items() if count != edges[(b, a)])
    nonmanifold_edges = sum(1 for a, b in edges if a < b and edges[(a, b)] + edges[(b, a)] != 2)
    record = dict(name=node['name'], triangle_count=triangles, signed_volume=volume,
                  unmatched_directed_edges=unmatched_edges, nonmanifold_edges=nonmanifold_edges,
                  vertex_normals_opposed_to_faces=flipped_vertex_normals)
    checks.append(record)
    if volume <= 0 or unmatched_edges or nonmanifold_edges or flipped_vertex_normals:
        failures.append('Inconsistent closed outward surface: ' + node['name'])
signatures = dict(glb_sha256=after['glb_sha256'],
    source_sha256=hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.py').read_bytes()).hexdigest(),
    blend_sha256=hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.blend').read_bytes()).hexdigest())
for name, value in signatures.items():
    if value != launch[name]:
        failures.append(name + ' differs from the completed exporter launch.')
receipt = dict(**signatures, baseline_glb_sha256=before['glb_sha256'], export_exit_code=launch['exit_code'],
    changed_mesh_members=changed, unchanged_mesh_count=len(after['mesh_geometry']) - len(changed),
    existing_node_names_preserved=after['node_names'] == before['node_names'],
    actual_binary_winding_checks=checks, failures=failures,
    validation_scope='Actual GLB helper callers and existing LOD1: closed oriented topology, positive signed volumes, matching vertex normals and exact unrelated-member preservation. This does not establish runtime physics or artistic acceptance.')
(folder / 'fap-export-08b-geometry-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps(receipt, indent=2))
raise SystemExit(1 if failures else 0)
