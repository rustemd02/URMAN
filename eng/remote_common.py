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
MAX_RESULT_BYTES = MAX_ARCHIVE
MAX_RESULT_FILES = 4096
MAX_BLOB = 512 * 1024**2
MAX_CANDIDATE_RECEIPT = 8 * 1024**2
CANDIDATE_SCHEMA = 'urman.windows_candidate_upload.v1'
STUDIO_AUTHORING_FILES = frozenset({
    'assets/source/blender/act1/urman_village_exterior_kit.py',
    'assets/source/blender/act1/urman_village_exterior_kit.blend',
})

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


def studio_authoring_path(root, name):
    """Return one opted-in authoring file after rejecting links and escapes."""
    if name not in STUDIO_AUTHORING_FILES or not allowed(name):
        raise ValueError('not an allowed Studio authoring source: ' + name)
    root = Path(root).resolve()
    path = root / name
    parts = PurePosixPath(name).parts
    reparse_point = getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0x0400)
    for index in range(1, len(parts) + 1):
        component = root.joinpath(*parts[:index])
        try:
            metadata = component.lstat()
        except OSError as error:
            raise ValueError('Studio authoring source is missing or unreadable: ' + name) from error
        if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, 'st_file_attributes', 0) & reparse_point:
            raise ValueError('Studio authoring source contains a symlink or junction: ' + name)
        if index < len(parts) and not stat.S_ISDIR(metadata.st_mode):
            raise ValueError('Studio authoring source parent is not a directory: ' + name)
        if index == len(parts) and not stat.S_ISREG(metadata.st_mode):
            raise ValueError('Studio authoring source is not a regular file: ' + name)
    resolved = path.resolve(strict=True)
    if root not in resolved.parents:
        raise ValueError('Studio authoring source escapes checkout: ' + name)
    return path


def candidates(root, *, studio_authoring=False):
    root = Path(root).resolve()
    names = git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').decode('utf-8').split('\0')
    result = []
    for name in sorted(set(names)):
        if not allowed(name):
            continue
        authoring_file = studio_authoring and name in STUDIO_AUTHORING_FILES
        parts = PurePosixPath(name).parts
        if len(parts) == 1 and parts[0] not in {'global.json','Directory.Build.props','Directory.Build.targets',
                'Directory.Packages.props','NuGet.Config','nuget.config','Urman.slnx','AGENTS.md'}:
            continue
        if (len(parts) > 1 and parts[0] not in {'content','game','src-dotnet','tools-dotnet','eng'}
                and not authoring_file):
            continue
        path = root / name
        # Runtime GLB is authoritative for execution; authoring sources are not imported.
        if path.suffix.lower() in {'.blend', '.blend1', '.blend2'} and not authoring_file:
            continue
        if authoring_file:
            studio_authoring_path(root, name)
            result.append(name)
            continue
        if path.is_symlink() or getattr(path, 'is_junction', lambda: False)():
            raise ValueError('source links are unsupported: ' + name)
        if path.is_file():
            if root not in path.resolve().parents:
                raise ValueError('source escapes checkout')
            result.append(name)
    if studio_authoring:
        missing = sorted(STUDIO_AUTHORING_FILES.difference(result))
        if missing:
            raise ValueError('missing required Studio authoring source(s): ' + ', '.join(missing))
    return result

def source_state(root):
    return {'base_commit': git(root, 'rev-parse', 'HEAD').decode().strip(),
            'dirty_paths': git(root, 'status', '--porcelain=v1', '-uall').decode('utf-8').splitlines()}

def make_snapshot(root, archive, *, studio_authoring=False):
    root = Path(root).resolve()
    origin = git(root, 'remote', 'get-url', 'origin').decode().strip()
    if origin not in (REPOSITORY, 'git@github.com:rustemd02/URMAN.git', 'ssh://git@github.com/rustemd02/URMAN.git'):
        raise ValueError('unexpected repository origin')
    before = source_state(root)
    names = candidates(root, studio_authoring=studio_authoring)
    manifest = {'schema': 1, 'repository': REPOSITORY, **before, 'files': {}}
    total = 0
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        for name in names:
            path = (studio_authoring_path(root, name)
                    if studio_authoring and name in STUDIO_AUTHORING_FILES else root / name)
            # Hash the exact bytes placed in the ZIP, then compare to disk again.
            h = hashlib.sha256(); size = 0
            with path.open('rb') as src, z.open(name, 'w', force_zip64=True) as dest:
                for chunk in iter(lambda: src.read(1024**2), b''):
                    dest.write(chunk); h.update(chunk); size += len(chunk)
            total += size
            if total > MAX_SOURCE:
                raise ValueError('snapshot exceeds source limit')
            manifest['files'][name] = {'sha256': h.hexdigest(), 'bytes': size}
        if names != candidates(root, studio_authoring=studio_authoring) or before != source_state(root):
            raise ValueError('checkout changed during packaging; retry after edits stop')
        for name, meta in manifest['files'].items():
            path = (studio_authoring_path(root, name)
                    if studio_authoring and name in STUDIO_AUTHORING_FILES else root / name)
            if digest(path) != meta['sha256']:
                raise ValueError('file changed during packaging: ' + name)
        if names != candidates(root, studio_authoring=studio_authoring) or before != source_state(root):
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


def validate_candidate_manifest(manifest):
    fields = {'schema', 'receipt_sha256', 'receipt_bytes', 'windows_zip_sha256',
              'windows_zip_bytes', 'windows_exe_sha256', 'windows_exe_bytes'}
    if not isinstance(manifest, dict) or set(manifest) != fields or manifest.get('schema') != CANDIDATE_SCHEMA:
        raise ValueError('unsupported Windows candidate manifest')
    for name, maximum in (('receipt', MAX_CANDIDATE_RECEIPT), ('windows_zip', MAX_BLOB),
                          ('windows_exe', MAX_ARCHIVE)):
        sha, size = manifest.get(name + '_sha256'), manifest.get(name + '_bytes')
        if (not isinstance(sha, str) or not re.fullmatch('[a-f0-9]{64}', sha)
                or type(size) is not int or not 0 < size <= maximum):
            raise ValueError('invalid Windows candidate ' + name + ' binding')
    return hashlib.sha256(canonical(manifest)).hexdigest()


def validate_candidate_receipt(receipt, manifest):
    validate_candidate_manifest(manifest)
    if (not isinstance(receipt, dict) or receipt.get('schema') != 'urman.desktop_artifact_receipt.v1'
            or receipt.get('status') != 'PASS' or receipt.get('windowsHostExecution') != 'OPEN'):
        raise ValueError('candidate must have a passing desktop export receipt with host execution still open')
    from act1_candidate_identity import artifact_bindings, validate_build_provenance
    provenance = receipt.get('buildProvenance')
    validate_build_provenance(provenance, receipt.get('artifacts'))
    if (provenance.get('producer', {}).get('mode') != 'release'
            or artifact_bindings(receipt['artifacts'])[('Windows', 'zip')] !=
                (manifest['windows_zip_bytes'], manifest['windows_zip_sha256'])
            or artifact_bindings(receipt['artifacts'])[('Windows', 'executable')] !=
                (manifest['windows_exe_bytes'], manifest['windows_exe_sha256'])):
        raise ValueError('candidate Windows artifacts do not match completed release provenance')


def _candidate_file(root, relative):
    root = Path(root).resolve(strict=True)
    path = root
    reparse_point = getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0x0400)
    for index, part in enumerate(PurePosixPath(relative).parts):
        path = path / part
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & reparse_point:
            raise ValueError('candidate contains a symlink or reparse point: ' + relative)
        if index < len(PurePosixPath(relative).parts) - 1 and not stat.S_ISDIR(info.st_mode):
            raise ValueError('candidate path parent is not a directory: ' + relative)
    if (not stat.S_ISREG(path.lstat().st_mode) or root not in path.resolve(strict=True).parents):
        raise ValueError('candidate file is not a regular file inside its release directory: ' + relative)
    return path


def make_candidate_upload(release_dir):
    release_dir = Path(release_dir)
    info = release_dir.lstat()
    if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0x0400):
        raise ValueError('candidate release directory cannot be a symlink or reparse point')
    root = release_dir.resolve(strict=True)
    if not stat.S_ISDIR(root.lstat().st_mode):
        raise ValueError('candidate release path is not a directory')
    receipt_path = _candidate_file(root, 'desktop-artifact-receipt.json')
    zip_path = _candidate_file(root, 'URMAN-windows-x86_64.zip')
    exe_path = _candidate_file(root, 'windows/URMAN.exe')
    if receipt_path.stat().st_size > MAX_CANDIDATE_RECEIPT:
        raise ValueError('desktop artifact receipt exceeds its size limit')
    receipt_bytes = receipt_path.read_bytes()
    receipt = json.loads(receipt_bytes.decode('utf-8-sig'))
    manifest = {
        'schema': CANDIDATE_SCHEMA,
        'receipt_sha256': hashlib.sha256(receipt_bytes).hexdigest(),
        'receipt_bytes': len(receipt_bytes),
        'windows_zip_sha256': digest(zip_path),
        'windows_zip_bytes': zip_path.stat().st_size,
        'windows_exe_sha256': digest(exe_path),
        'windows_exe_bytes': exe_path.stat().st_size,
    }
    validate_candidate_receipt(receipt, manifest)
    return manifest, {'receipt': receipt_path, 'windows_zip': zip_path}


def unpack_candidate(archive, destination, manifest):
    validate_candidate_manifest(manifest)
    archive = Path(archive)
    if archive.stat().st_size != manifest['windows_zip_bytes'] or digest(archive) != manifest['windows_zip_sha256']:
        raise ValueError('Windows candidate ZIP hash or size mismatch')
    destination = Path(destination)
    if destination.exists() or destination.is_symlink() or getattr(destination, 'is_junction', lambda: False)():
        raise ValueError('candidate extraction destination already exists')
    destination.mkdir(parents=True)
    with zipfile.ZipFile(archive) as z:
        entries = z.infolist()
        if not entries or len(entries) > MAX_RESULT_FILES:
            raise ValueError('Windows candidate ZIP entry count is invalid')
        names = set()
        total = 0
        exe_entry = None
        for entry in entries:
            mode = entry.external_attr >> 16
            directory = entry.is_dir() or stat.S_ISDIR(mode)
            name = entry.filename[:-1] if directory else entry.filename
            if (not allowed(name) or PurePosixPath(name).parts[0] != 'windows'
                    or name.casefold() in names or stat.S_ISLNK(mode)
                    or entry.flag_bits & 0x1 or entry.external_attr & 0x0400
                    or (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR))
                    or (directory and (not entry.filename.endswith('/') or entry.file_size != 0))):
                raise ValueError('Windows candidate ZIP contains an unsafe or duplicate path')
            names.add(name.casefold())
            if not directory:
                total += entry.file_size
                if total > MAX_ARCHIVE:
                    raise ValueError('Windows candidate ZIP expands beyond its size limit')
                if name.casefold() == 'windows/urman.exe':
                    exe_entry = entry
        if (exe_entry is None or exe_entry.file_size != manifest['windows_exe_bytes']
                or not any(name == 'windows/urman.exe' for name in names)):
            raise ValueError('Windows candidate ZIP is missing its bound executable')
        exe_hash = hashlib.sha256()
        actual_total = 0
        for entry in entries:
            mode = entry.external_attr >> 16
            directory = entry.is_dir() or stat.S_ISDIR(mode)
            name = entry.filename[:-1] if directory else entry.filename
            target = destination.joinpath(*PurePosixPath(name).parts)
            if destination.resolve() not in target.resolve().parents and target.resolve() != destination.resolve():
                raise ValueError('Windows candidate ZIP path escapes extraction root')
            if directory:
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            actual_size = 0
            with z.open(entry) as source, target.open('xb') as output:
                for chunk in iter(lambda: source.read(1024**2), b''):
                    actual_size += len(chunk)
                    actual_total += len(chunk)
                    if actual_size > entry.file_size or actual_total > MAX_ARCHIVE:
                        raise ValueError('Windows candidate ZIP expanded beyond its declared bounds')
                    if entry is exe_entry:
                        exe_hash.update(chunk)
                    output.write(chunk)
            if actual_size != entry.file_size:
                raise ValueError('Windows candidate ZIP entry size mismatch: ' + entry.filename)
        if exe_hash.hexdigest() != manifest['windows_exe_sha256']:
            raise ValueError('Windows candidate executable hash mismatch')

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
