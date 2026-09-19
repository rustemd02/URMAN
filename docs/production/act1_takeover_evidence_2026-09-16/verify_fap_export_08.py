"""Verify the completed 08 export without launching Blender or rewriting history."""
import hashlib
import json
from pathlib import Path

from fap_export_geometry_receipt import geometry_snapshot


folder = Path(__file__).resolve().parent
repo = folder.parents[2]
baseline = json.loads((folder / 'fap-export-08-baseline.json').read_text())
launch = json.loads((folder / 'fap-export-08-launch.json').read_text())
current = geometry_snapshot(repo / 'game/assets/models/act1/urman_fap_clinic_kit.glb')
expected = {
    'FapEntryPorch_Threshold_LOD0',
    'FapFacade_DoorFrameLeft_LOD0',
    'FapFacade_DoorFrameRight_LOD0',
    'FapFacade_Foundation_LOD0',
    'FapInteriorTrolley_Handle_LOD0',
    'FapInteriorWashUnit_Body_LOD0',
    'FapInteriorWashUnit_Body_LOD1',
    'FapInteriorWashUnit_Towel_LOD0',
    'FapInteriorWashUnit_Towel_LOD1',
    'FapInteriorWashUnit_TowelRail_LOD0',
    'FapInteriorWashUnit_TowelRail_LOD1',
}
changed = sorted(name for name, signature in current['mesh_geometry'].items()
                 if baseline['mesh_geometry'].get(name) != signature)
changed_components = sorted(name for name, record in current['components'].items()
                            if baseline['components'].get(name) != record)
failures = []
if set(changed) != expected:
    failures.append('Unexpected changed/missing member set: ' + ', '.join(sorted(set(changed) ^ expected)))
if current['node_names'] != baseline['node_names']:
    failures.append('Published node names changed.')
if set(current['mesh_geometry']) != set(baseline['mesh_geometry']):
    failures.append('Mesh member names changed.')
if changed_components != ['FapEntryPorch', 'FapFacade_Main', 'FapInteriorSet']:
    failures.append('Unexpected component scope: ' + ', '.join(changed_components))
if launch['exit_code'] != 0:
    failures.append('Exporter did not exit successfully.')
signatures = {
    'glb_sha256': current['glb_sha256'],
    'source_sha256': hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.py').read_bytes()).hexdigest(),
    'blend_sha256': hashlib.sha256((repo / 'assets/source/blender/act1/urman_fap_clinic_kit.blend').read_bytes()).hexdigest(),
}
for name, signature in signatures.items():
    if launch[name] != signature:
        failures.append(name + ' differs from the completed export launch receipt.')
receipt = {
    **signatures,
    'baseline_glb_sha256': baseline['glb_sha256'],
    'export_exit_code': launch['exit_code'],
    'changed_geometry_components': changed_components,
    'changed_mesh_members': changed,
    'expected_mesh_members': sorted(expected),
    'unchanged_mesh_count': len(current['mesh_geometry']) - len(changed),
    'unchanged_component_count': len(current['components']) - len(changed_components),
    'existing_node_names_preserved': current['node_names'] == baseline['node_names'],
    'first_receipt': 'fap-export-08-geometry-receipt.json',
    'first_receipt_correction': 'The original expected set listed only eight LOD0 meshes. The exporter intentionally rebuilt the existing Body, Towel and TowelRail LOD1 meshes from the changed LOD0 sources. This final member-preservation check includes all eleven intended members and preserves the first failed receipt as history.',
    'validation_scope': 'Actual binary member preservation and completed export identity. Engine contacts, player movement, rendering and artistic acceptance remain pending.',
    'failures': failures,
}
output = folder / 'fap-export-08-member-preservation-receipt.json'
output.write_text(json.dumps(receipt, indent=2) + '\n')
print(json.dumps(receipt, indent=2))
raise SystemExit(1 if failures else 0)
