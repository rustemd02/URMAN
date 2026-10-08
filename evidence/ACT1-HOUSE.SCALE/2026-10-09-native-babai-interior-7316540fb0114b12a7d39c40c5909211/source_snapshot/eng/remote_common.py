"""Wire format and source identity shared by the station and portable client."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import zipfile

REPOSITORY = 'https://github.com/rustemd02/URMAN.git'
EXCLUDED = {'.git', '.tools', '.godot', '.codex-captures', 'bin', 'obj', 'node_modules',
            '__pycache__', 'graphify-out', '.cache', 'screenshots', 'receipts', 'cache', 'caches', '.secrets', '.ssh',
            'userdata', 'user_data', 'app_userdata', 'savegames'}
MAX_ARCHIVE = 2 * 1024**3
MAX_SOURCE = 5 * 1024**3

def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')).encode('utf-8')

def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024**2), b''):
            h.update(chunk)
    return h.hexdigest()

def allowed(name):
    p = PurePosixPath(name)
    if not name or '\\' in name or ':' in name or p.is_absolute() or any(
            part in ('', '.', '..') or part.rstrip(' .') != part or
            re.fullmatch(r'(?i)(con|prn|aux|nul|com[0-9]|lpt[0-9])(\..*)?', part)
            for part in name.split('/')):
        return False
    if any(part.lower() in EXCLUDED for part in p.parts):
        return False
    base = p.name.lower()
    return not (base.startswith('.env') or base in {'override.cfg', 'credentials', 'config.local.json'}
                or base.endswith(('.pem', '.key', '.pfx', '.p12', '.token', '.secret'))
                or base in {'secrets.json', 'tokens.json', 'token.txt', 'station-config.json'})

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])

def candidates(root):
    names = git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').decode('utf-8').split('\0')
    result = []
    for name in sorted(set(names)):
        if not allowed(name):
            continue
        parts = PurePosixPath(name).parts
        if len(parts) == 1 and parts[0] not in {'global.json','Directory.Build.props','Directory.Build.targets',
                'Directory.Packages.props','NuGet.Config','nuget.config','Urman.slnx','AGENTS.md'}:
            continue
        if len(parts) > 1 and parts[0] not in {'content','game','src-dotnet','tools-dotnet','eng'}:
            continue
        path = root / name
        # Runtime GLB is authoritative for execution; authoring sources are not imported.
        if path.suffix.lower() in {'.blend', '.blend1', '.blend2'}:
            continue
        if path.is_symlink() or getattr(path, 'is_junction', lambda: False)():
            raise ValueError('source links are unsupported: ' + name)
        if path.is_file():
            if root not in path.resolve().parents:
                raise ValueError('source escapes checkout')
            result.append(name)
    return result

def source_state(root):
    return {'base_commit': git(root, 'rev-parse', 'HEAD').decode().strip(),
            'dirty_paths': git(root, 'status', '--porcelain=v1', '-uall').decode('utf-8').splitlines()}

def make_snapshot(root, archive):
    root = Path(root).resolve()
    origin = git(root, 'remote', 'get-url', 'origin').decode().strip()
    if origin not in (REPOSITORY, 'git@github.com:rustemd02/URMAN.git', 'ssh://git@github.com/rustemd02/URMAN.git'):
        raise ValueError('unexpected repository origin')
    before = source_state(root)
    names = candidates(root)
    manifest = {'schema': 1, 'repository': REPOSITORY, **before, 'files': {}}
    total = 0
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        for name in names:
            path = root / name
            # Hash the exact bytes placed in the ZIP, then compare to disk again.
            h = hashlib.sha256(); size = 0
            with path.open('rb') as src, z.open(name, 'w', force_zip64=True) as dest:
                for chunk in iter(lambda: src.read(1024**2), b''):
                    dest.write(chunk); h.update(chunk); size += len(chunk)
            total += size
            if total > MAX_SOURCE:
                raise ValueError('snapshot exceeds source limit')
            manifest['files'][name] = {'sha256': h.hexdigest(), 'bytes': size}
        if names != candidates(root) or before != source_state(root):
            raise ValueError('checkout changed during packaging; retry after edits stop')
        for name, meta in manifest['files'].items():
            if digest(root / name) != meta['sha256']:
                raise ValueError('file changed during packaging: ' + name)
        if names != candidates(root) or before != source_state(root):
            raise ValueError('checkout changed during final source verification')
        manifest['snapshot_id'] = hashlib.sha256(canonical(manifest)).hexdigest()
        z.writestr('snapshot-manifest.json', canonical(manifest))
    if Path(archive).stat().st_size > MAX_ARCHIVE:
        raise ValueError('snapshot exceeds upload limit')
    return manifest

def validate_manifest(manifest):
    value = dict(manifest); sid = value.pop('snapshot_id', '')
    if not re.fullmatch('[a-f0-9]{64}', sid) or hashlib.sha256(canonical(value)).hexdigest() != sid:
        raise ValueError('manifest identity mismatch')
    if value.get('schema') != 1 or value.get('repository') != REPOSITORY:
        raise ValueError('unsupported manifest/repository')
    files = value.get('files', {})
    if not files or len(files) > 30000 or any(not allowed(name) for name in files):
        raise ValueError('invalid source file list')
    if sum(meta['bytes'] for meta in files.values()) > MAX_SOURCE:
        raise ValueError('source size limit')
    for required in ('global.json', 'eng/toolchain.json', 'game/project.godot', 'game/Urman.Game.csproj'):
        if required not in files:
            raise ValueError('missing source dependency: ' + required)
    return sid

def unpack_snapshot(archive, destination):
    with zipfile.ZipFile(archive) as z:
        entries = z.infolist()
        if len(entries) > 30001 or len({i.filename.casefold() for i in entries}) != len(entries):
            raise ValueError('duplicate or excessive ZIP entries')
        if z.getinfo('snapshot-manifest.json').file_size > 8 * 1024**2:
            raise ValueError('manifest size limit')
        manifest = json.loads(z.read('snapshot-manifest.json'))
        validate_manifest(manifest)
        if {i.filename for i in entries} != set(manifest['files']) | {'snapshot-manifest.json'}:
            raise ValueError('ZIP and manifest file sets differ')
        for i in entries:
            if i.filename == 'snapshot-manifest.json':
                continue
            if not allowed(i.filename) or stat.S_ISLNK(i.external_attr >> 16):
                raise ValueError('unsafe ZIP entry')
            meta = manifest['files'][i.filename]
            if i.file_size != meta['bytes']:
                raise ValueError('ZIP size mismatch')
            path = destination / i.filename; path.parent.mkdir(parents=True, exist_ok=True)
            h = hashlib.sha256(); size = 0
            with z.open(i) as src, path.open('xb') as dest:
                for chunk in iter(lambda: src.read(1024**2), b''):
                    size += len(chunk)
                    if size > meta['bytes']:
                        raise ValueError('ZIP expansion exceeded manifest')
                    dest.write(chunk); h.update(chunk)
            if h.hexdigest() != meta['sha256']:
                raise ValueError('source SHA256 mismatch: ' + i.filename)
        (destination / 'snapshot-manifest.json').write_bytes(canonical(manifest))
        return manifest

def verify_source(root, manifest):
    validate_manifest(manifest)
    for name, meta in manifest['files'].items():
        if (root / name).stat().st_size != meta['bytes'] or digest(root / name) != meta['sha256']:
            raise ValueError('source changed after upload: ' + name)

def bounded_command(command, timeout):
    """Used by the existing runner; arguments never pass through a shell."""
    process = subprocess.Popen(command)
    try:
        return process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        if os.name == 'nt':
            subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
        else:
            process.kill()
        process.wait(timeout=10)
        return 124

if __name__ == '__main__':
    import sys
    raise SystemExit(bounded_command(sys.argv[2:], float(sys.argv[1])))
