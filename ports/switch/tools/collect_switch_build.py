#!/usr/bin/env python3
"""Collect validated CI intermediates for local art assembly, not an SD release.

Requires successful runtime/native/editor/game stages. The immutable payload is
written to WORK_DIR/cloud-payload only after input validation and byte checks.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace

from package_switch import (check_receipts, digest, framework_inputs, game_inputs,
                            publish_new, read_json, regular, require,
                            validate_elf, validate_icu, validate_nro, validate_pck)


def collect(work: Path) -> Path:
    work = work.resolve(strict=True)
    destination = work / 'cloud-payload'
    require(not destination.exists() and not destination.is_symlink(), 'cloud-payload already exists; choose a fresh build directory')
    status = read_json(work / 'build-status.json')
    require(status.get('status') == 'requested_build_stages_completed', 'Build did not successfully complete requested stages')
    stages = status.get('stages')
    require(isinstance(stages, list), 'Build status lacks stage results')
    require([item.get('name') for item in stages] == ['runtime', 'native', 'editor', 'game'],
            'Collector requires exactly runtime/native/editor/game stages; use --stage game')
    require(all(item.get('status') == 'completed' for item in stages), 'A prerequisite build stage is incomplete')
    project = Path(__file__).resolve().parents[1]
    runtime = work / 'sources/dotnet-runtime'
    engine = work / 'sources/godot'
    game = work / 'game-export'
    native = work / 'native'
    args = SimpleNamespace(
        corelib_dir=runtime / 'artifacts/bin/coreclr/libnx.arm64.Release/IL',
        framework_dir=runtime / 'artifacts/bin/runtime/net10.0-libnx-Release-arm64',
        managed_dir=game / 'managed', framework_receipt=work / 'framework-receipt.json',
        engine_receipt=work / 'engine-receipt.json', runtime_source=runtime,
        source_pins=project / 'engine/source-pins.json', nro=native / 'urman.nro', elf=native / 'urman.elf',
        icu_data=runtime / 'artifacts/horizon/icu/share/icu/77.1/icudt77l.dat')
    validate_nro(args.nro)
    validate_elf(args.elf)
    validate_pck(game / 'urman.pck')
    validate_icu(args.icu_data)
    framework = framework_inputs(args.corelib_dir, args.framework_dir)
    managed = game_inputs(args.managed_dir, framework)
    pins = check_receipts(args, framework)
    actual_engine = subprocess.check_output(['git', '-C', str(engine), 'rev-parse', 'HEAD'], text=True).strip()
    require(actual_engine == pins['engine_commit'], 'Engine checkout differs from pinned revision')
    regular(native / 'urman.nacp')
    require((native / 'urman.nacp').stat().st_size == 0x4000, 'Invalid NACP size')
    report = read_json(game / 'export-report.json')
    require(report.get('status') == 'exported_hardware_validation_not_run', 'Game export report is not successful')
    require(report.get('engine_commit') == pins['engine_commit'], 'Game export uses another engine revision')
    require(report.get('pck_sha256') == digest(game / 'urman.pck'), 'PCK differs from export report')
    external = read_json(game / 'external-managed-manifest.json')
    require(external.get('engine_commit') == pins['engine_commit'], 'Managed export engine revision mismatch')
    require(external.get('game_patch', {}).get('sha256') == pins.get('game_patch_sha256'), 'Managed game patch differs from source pins')
    require(external.get('files') == {name: digest(path) for name, path in managed.items()}, 'Managed files differ from exporter manifest')
    validators = runtime / 'src/coreclr/pal/tests/libnx/host'
    for name in ('validate-il.py', 'validate-qcalls.py'):
        regular(validators / name)
    # Run the actual runtime validators on source build outputs before copying.
    all_dlls = sorted([*framework.values(), *[p for p in managed.values() if p.suffix.lower() == '.dll']])
    validated_hashes = {path.resolve(): digest(path) for path in [*all_dlls, args.elf]}
    for index in range(0, len(all_dlls), 100):
        subprocess.run([sys.executable, str(validators / 'validate-il.py'), *map(str, all_dlls[index:index+100])], check=True)
    qcall_report = subprocess.check_output([sys.executable, str(validators / 'validate-qcalls.py'),
        str(args.corelib_dir / 'System.Private.CoreLib.dll'), str(args.elf)], text=True)
    require(not json.loads(qcall_report).get('missing'), 'QCall validation reported missing runtime imports')

    inputs = {
        'native/urman.nro': args.nro, 'native/urman.elf': args.elf, 'native/urman.nacp': native / 'urman.nacp',
        'game-export/urman.pck': game / 'urman.pck',
        'corelib/System.Private.CoreLib.dll': args.corelib_dir / 'System.Private.CoreLib.dll',
        'icu/icudt77l.dat': args.icu_data,
        'engine-receipt.json': args.engine_receipt, 'framework-receipt.json': args.framework_receipt,
        'source-pins.json': args.source_pins, 'build-status.json': work / 'build-status.json',
        'game-export/export-report.json': game / 'export-report.json',
        'game-export/external-managed-manifest.json': game / 'external-managed-manifest.json',
        'licenses/godot/LICENSE.txt': engine / 'LICENSE.txt',
        'licenses/godot/COPYRIGHT.txt': engine / 'COPYRIGHT.txt',
        'licenses/dotnet/LICENSE.TXT': runtime / 'LICENSE.TXT',
        'licenses/dotnet/THIRD-PARTY-NOTICES.TXT': runtime / 'THIRD-PARTY-NOTICES.TXT',
    }
    icu_source = runtime / 'artifacts/horizon/sources/icu/icu'
    icu_licenses = [icu_source / name for name in ('LICENSE', 'license.html') if (icu_source / name).is_file()]
    require(icu_licenses, 'Source ICU license is missing')
    for path in icu_licenses:
        inputs['licenses/icu/' + path.name] = path
    for name in ('LICENSE', 'LICENSE.md', 'LICENSE.MD'):
        path = runtime / 'artifacts/horizon/sources/libnx' / name
        if path.is_file():
            inputs['licenses/libnx/' + name] = path
    for relative, path in managed.items():
        inputs['game-export/managed/' + relative] = path
    for path in sorted(args.framework_dir.glob('*.dll')):
        inputs['framework/' + path.name] = path
    for path in inputs.values():
        regular(path)
    expected = {relative: digest(path) for relative, path in inputs.items()}
    for relative, source in inputs.items():
        if source.resolve() in validated_hashes:
            require(expected[relative] == validated_hashes[source.resolve()], f'Validated input changed: {relative}')
    with tempfile.TemporaryDirectory(prefix='.cloud-payload-', dir=work) as temporary:
        staging = Path(temporary) / 'payload'
        staging.mkdir()
        for relative, source in inputs.items():
            target = staging / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            require(digest(target) == expected[relative], f'Input changed during collection: {relative}')
        (staging / 'qcall-validation.json').write_text(qcall_report)
        manifest = {'schema_version': 1, 'status': 'cloud_intermediates_local_art_assembly_pending',
            'hardware_validation': 'not_run', 'source_pins': pins,
            'validator_sha256': {name: digest(validators / name) for name in ('validate-il.py', 'validate-qcalls.py')},
            'files': {p.relative_to(staging).as_posix(): digest(p) for p in sorted(staging.rglob('*')) if p.is_file()},
            'next_step': 'Download and use package_switch.py with this payload plus local art-root/art-manifest. This is not a final SD package.'}
        (staging / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
        publish_new(staging, destination)
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work-dir', type=Path, required=True)
    args = parser.parse_args()
    try:
        destination = collect(args.work_dir)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Collection rejected: {error}\n')
    print(f'Validated cloud intermediates collected; local art assembly pending: {destination}')


if __name__ == '__main__':
    main()
