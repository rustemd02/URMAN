#!/usr/bin/env python3
"""Validate and stage an immutable, untested URMAN Switch SD package.

Inputs must be real build products. Build receipts are auditable provenance,
not a signature or proof of on-device correctness. Requires pefile, dnfile and
pyelftools in this Python environment for the runtime's source validators.
No native binaries or downloaded desktop framework DLLs are synthesized here.
"""
from __future__ import annotations
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def regular(path: Path) -> None:
    require(path.is_file() and not path.is_symlink(), f'Expected regular input file: {path}')


def read_json(path: Path) -> dict:
    regular(path)
    value = json.loads(path.read_text())
    require(isinstance(value, dict), f'Expected JSON object: {path}')
    return value


def validate_nro(path: Path) -> None:
    regular(path)
    with path.open('rb') as stream:
        header = stream.read(0x80)
    require(len(header) == 0x80 and header[0x10:0x14] == b'NRO0', 'Invalid NRO header/magic')
    size = struct.unpack_from('<I', header, 0x18)[0]
    require(0x80 <= size <= path.stat().st_size, 'NRO declared size exceeds input')
    spans = [struct.unpack_from('<II', header, pos) for pos in (0x20, 0x28, 0x30)]
    require(all(length and offset + length <= size for offset, length in spans), 'Invalid NRO segment bounds')
    require(spans[0][0] == 0 and spans[0][1] >= 0x80, 'NRO text does not include header')
    require(all(spans[i][0] + spans[i][1] <= spans[i+1][0] for i in range(2)), 'Overlapping NRO segments')


def validate_elf(path: Path) -> None:
    regular(path)
    with path.open('rb') as stream:
        header = stream.read(64)
    require(len(header) == 64 and header[:6] == b'\x7fELF\x02\x01', 'Expected little-endian ELF64')
    require(struct.unpack_from('<H', header, 18)[0] == 183, 'ELF must be AArch64')


def validate_pck(path: Path) -> None:
    regular(path)
    with path.open('rb') as stream:
        header = stream.read(100)
    require(len(header) == 100 and header[:4] == b'GDPC', 'Invalid Godot PCK header/magic')


def validate_icu(path: Path) -> None:
    regular(path)
    with path.open('rb') as stream:
        header = stream.read(32)
    require(len(header) == 32 and header[2:4] == b'\xda\x27', 'Invalid ICU data header')
    require(header[8] == 0 and header[12:16] == b'CmnD', 'Expected little-endian ICU common data archive')
    require(re.fullmatch(r'icudt\d+l\.dat', path.name) is not None, 'Expected versioned icudt<number>l.dat filename')


def framework_inputs(corelib_dir: Path, framework_dir: Path) -> dict[str, Path]:
    corelib = corelib_dir / 'System.Private.CoreLib.dll'
    regular(corelib)
    files = {corelib.name: corelib}
    for path in sorted(framework_dir.glob('*.dll')):
        regular(path)
        if path.name == corelib.name:
            require(digest(path) == digest(corelib), 'Framework/CoreLib mismatch')
        else:
            files[path.name] = path
    require('System.Runtime.dll' in files, 'Source-built Horizon framework missing System.Runtime.dll')
    require(len({name.casefold() for name in files}) == len(files), 'Case-colliding framework assemblies')
    return files


def game_inputs(directory: Path, framework: dict[str, Path]) -> dict[str, Path]:
    require(directory.is_dir() and not directory.is_symlink(), 'Managed game directory missing')
    files: dict[str, Path] = {}
    reserved = {name.casefold() for name in framework}
    for path in sorted(directory.rglob('*')):
        require(not path.is_symlink(), f'Symlink forbidden in managed deployment: {path}')
        if not path.is_file():
            continue
        rel = path.relative_to(directory).as_posix()
        require(rel.casefold() not in reserved, f'Game input collides with source framework: {rel}')
        require(path.suffix.lower() not in ('.so', '.dylib', '.exe', '.a'), f'Native/desktop dependency forbidden: {rel}')
        files[rel] = path
    require(len({name.casefold() for name in files}) == len(files), 'Case-colliding game files')
    require(all(name in files for name in ('Urman.Game.dll', 'GodotSharp.dll')), 'Managed input needs Urman.Game.dll and GodotSharp.dll')
    return files


def check_receipts(args, framework: dict[str, Path]) -> dict:
    pins = read_json(args.source_pins)
    for key in ('engine_commit', 'runtime_commit'):
        require(re.fullmatch(r'[0-9a-f]{40}', pins.get(key, '')) is not None, f'Source pins need full {key}')
    actual = subprocess.check_output(['git', '-C', str(args.runtime_source), 'rev-parse', 'HEAD'], text=True).strip()
    require(actual == pins['runtime_commit'], 'Runtime checkout differs from source pin')
    receipt = read_json(args.framework_receipt)
    require(receipt.get('schema_version') == 1 and receipt.get('target_os') == 'libnx' and receipt.get('target_arch') == 'arm64', 'Framework receipt must identify libnx/arm64 source build')
    require(receipt.get('runtime_commit') == pins['runtime_commit'], 'Framework runtime revision mismatch')
    expected = {name: digest(path) for name, path in framework.items()}
    require(receipt.get('files') == expected, 'Framework file set/hashes differ from source-build receipt')
    require(receipt.get('icu') == {'name': args.icu_data.name, 'sha256': digest(args.icu_data)}, 'ICU differs from framework receipt')
    engine = read_json(args.engine_receipt)
    require(engine.get('schema_version') == 1, 'Unsupported engine receipt')
    require(all(engine.get(key) == pins[key] for key in ('engine_commit', 'runtime_commit')), 'Engine receipt source pins differ')
    require(engine.get('files') == {'urman.nro': digest(args.nro), 'urman.elf': digest(args.elf)}, 'ELF/NRO differ from matched engine build receipt')
    return pins


def publish_new(staging: Path, output: Path) -> None:
    """Atomic directory publish that cannot replace even an empty directory."""
    libc = ctypes.CDLL(None, use_errno=True)
    if sys.platform == 'darwin':
        fn = libc.renamex_np
        fn.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint]
        result = fn(os.fsencode(staging), os.fsencode(output), 0x4)  # RENAME_EXCL
    elif sys.platform.startswith('linux') and hasattr(libc, 'renameat2'):
        fn = libc.renameat2
        fn.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
        result = fn(-100, os.fsencode(staging), -100, os.fsencode(output), 1)  # RENAME_NOREPLACE
    else:
        raise ValueError('Atomic no-replace publishing requires macOS or Linux renameat2')
    if result != 0:
        code = ctypes.get_errno()
        raise OSError(code, os.strerror(code), str(output))


def stage(args) -> Path:
    output = args.output.absolute()
    require(not output.exists() and not output.is_symlink(), f'Output exists; choose a fresh release directory: {output}')
    validate_nro(args.nro)
    validate_elf(args.elf)
    validate_pck(args.pck)
    validate_icu(args.icu_data)
    framework = framework_inputs(args.corelib_dir, args.framework_dir)
    game = game_inputs(args.managed_dir, framework)
    pins = check_receipts(args, framework)
    validator_root = args.runtime_source / 'src/coreclr/pal/tests/libnx/host'
    for name in ('validate-il.py', 'validate-qcalls.py'):
        regular(validator_root / name)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.urman-stage-', dir=output.parent) as temp:
        staging = Path(temp) / 'package'
        app = staging / 'switch/urman'
        managed = app / 'managed'
        managed.mkdir(parents=True)
        for rel, source in {**framework, **game}.items():
            dest = managed / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, dest)
        shutil.copyfile(args.nro, app / 'urman.nro')
        shutil.copyfile(args.pck, app / 'urman.pck')
        icu = app / 'icu'
        icu.mkdir()
        shutil.copyfile(args.icu_data, icu / args.icu_data.name)
        staged_framework = {name: digest(managed / name) for name in framework}
        require(staged_framework == read_json(args.framework_receipt)['files'], 'Framework changed during staging')
        require(digest(app / 'urman.nro') == read_json(args.engine_receipt)['files']['urman.nro'], 'NRO changed during staging')
        require(digest(icu / args.icu_data.name) == read_json(args.framework_receipt)['icu']['sha256'], 'ICU changed during staging')
        # Validators read the final staged bytes, not a different build directory.
        dlls = sorted(p for p in managed.rglob('*') if p.suffix.lower() == '.dll')
        for index in range(0, len(dlls), 100):
            subprocess.run([sys.executable, str(validator_root / 'validate-il.py'), *map(str, dlls[index:index+100])], check=True)
        evidence = staging / 'evidence'
        evidence.mkdir()
        qcalls = subprocess.check_output([sys.executable, str(validator_root / 'validate-qcalls.py'), str(managed / 'System.Private.CoreLib.dll'), str(args.elf)], text=True)
        (evidence / 'qcall-validation.json').write_text(qcalls)
        for source, name in ((args.source_pins, 'source-pins.json'), (args.framework_receipt, 'framework-receipt.json'), (args.engine_receipt, 'engine-receipt.json')):
            shutil.copyfile(source, evidence / name)
        manifest = {'schema_version': 1, 'status': 'packaged_hardware_validation_not_run', 'source_pins': pins,
                    'elf_sha256': digest(args.elf), 'validation_tools': {name: digest(validator_root / name) for name in ('validate-il.py', 'validate-qcalls.py')}, 'files': {p.relative_to(staging).as_posix(): digest(p) for p in sorted(staging.rglob('*')) if p.is_file()},
                    'limitations': ['Format, provenance receipts and QCall compatibility checked; not a hardware test or complete API-compatibility proof.']}
        (staging / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
        # Refuse an existing destination a second time immediately before publish.
        require(not output.exists() and not output.is_symlink(), 'Output appeared during staging; refusing overwrite')
        publish_new(staging, output)
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('nro', 'elf', 'pck', 'corelib-dir', 'framework-dir', 'framework-receipt', 'engine-receipt', 'managed-dir', 'icu-data', 'runtime-source', 'source-pins', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    try:
        output = stage(args)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Package rejected: {error}\n')
    print(f'Package staged (hardware validation NOT RUN): {output}')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
