"""Verify the exported floor finishes; this is not runtime or art acceptance."""
import hashlib
import json
import struct
from pathlib import Path
from fap_export_geometry_receipt import glb, geometry_snapshot


def transform(point, node):
    x, y, z = [point[i] * node.get('scale', [1, 1, 1])[i] for i in range(3)]
    qx, qy, qz, qw = node.get('rotation', [0, 0, 0, 1])
    tx, ty, tz = 2 * (qy*z-qz*y), 2 * (qz*x-qx*z), 2 * (qx*y-qy*x)
    rotated = (x+qw*tx+qy*tz-qz*ty, y+qw*ty+qz*tx-qx*tz, z+qw*tz+qx*ty-qy*tx)
    return tuple(rotated[i]+node.get('translation', [0, 0, 0])[i] for i in range(3))


folder = Path(__file__).resolve().parent
repo = folder.parents[2]
model = repo / 'game/assets/models/act1/urman_fap_clinic_kit.glb'
previous = json.loads((folder / 'fap-export-09-baseline.json').read_text())
snapshot = geometry_snapshot(model)
changed = sorted(name for name, value in snapshot['mesh_geometry'].items()
                 if previous['mesh_geometry'].get(name) != value)
expected = sorted([f'FapInteriorFloor_{part}_LOD{lod}' for part in
                   ('EntryRunner', 'ExamMat', 'WaitingMat', 'RecordsMat') for lod in (0, 1)]
                  + ['FapInteriorShell_EntryMat_LOD0'])
failures = []
if changed != expected:
    failures.append('Unexpected geometry changes: ' + repr(changed))
if previous['node_names'] != snapshot['node_names']:
    failures.append('Exported node names changed.')
doc, binary = glb(model)
nodes = {node['name']: node for node in doc['nodes']}


def positions(node):
    result = []
    for primitive in doc['meshes'][node['mesh']]['primitives']:
        accessor = doc['accessors'][primitive['attributes']['POSITION']]
        view = doc['bufferViews'][accessor['bufferView']]
        assert accessor['type'] == 'VEC3' and accessor['componentType'] == 5126
        offset = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
        stride = view.get('byteStride', 12)
        result.extend(transform(struct.unpack_from('<fff', binary, offset+i*stride), node)
                      for i in range(accessor['count']))
    return result


floor = positions(nodes['FapInteriorShell_Floor_LOD0'])
floor_top = max(point[1] for point in floor)
contacts = []
for name in expected:
    points = positions(nodes[name])
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    gap = minimum[1] - floor_top
    height = maximum[1] - minimum[1]
    supported = abs(gap) < .0002 and height <= .0181
    if not supported:
        failures.append(f'{name}: unsupported finish gap={gap}, height={height}')
    contacts.append(dict(name=name, minimum=minimum, maximum=maximum,
                         gap_to_floor=gap, thickness=height, supported=supported))
receipt = dict(glb_sha256=snapshot['glb_sha256'],
               source_sha256=hashlib.sha256((repo/'assets/source/blender/act1/urman_fap_clinic_kit.py').read_bytes()).hexdigest(),
               blend_sha256=hashlib.sha256((repo/'assets/source/blender/act1/urman_fap_clinic_kit.blend').read_bytes()).hexdigest(),
               baseline_glb_sha256=previous['glb_sha256'], changed_meshes=changed,
               unchanged_meshes=len(snapshot['mesh_geometry'])-len(changed),
               names_preserved=previous['node_names']==snapshot['node_names'],
               floor_top=floor_top, contacts=contacts, failures=failures,
               validation_scope='Actual exported geometry and preserved unaffected geometry only. Runtime contacts, visual quality and human acceptance remain pending.')
target = folder/'fap-export-09-geometry-receipt.json'
assert not target.exists(), 'Preserve earlier evidence rather than overwriting a receipt.'
target.write_text(json.dumps(receipt, indent=2)+'\n')
print(json.dumps(receipt, indent=2))
raise SystemExit(1 if failures else 0)
