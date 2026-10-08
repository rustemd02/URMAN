"""Private, single-job URMAN worker. Start in an interactive user session."""
import argparse
import ctypes
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import zipfile

from remote_common import (MAX_ARCHIVE, canonical, digest, unpack_snapshot,
                           validate_manifest, verify_source)

# Incremental snapshots: the station keeps one blob per distinct file content, so a
# new build sends only the files that actually changed instead of the whole tree.
MAX_BLOB = 512 * 1024**2
KEEP_MANIFESTS = 50
KEEP_ARCHIVES = 1        # the newest archive is what an empty blob store is seeded from
BLOB_GRACE = 24 * 3600   # content a client may still be uploading is not collected

def tree_bytes(root):
    return sum(p.stat().st_size for p in root.rglob('*') if p.is_file())

def blob_path(blobs, sha):
    return blobs / sha[:2] / sha

def store_blob(blobs, sha, stream, size):
    """Store one content-addressed file. The path is its hash, so no name can escape."""
    if not re.fullmatch('[a-f0-9]{64}', sha) or not 0 < size <= MAX_BLOB:
        raise ValueError('invalid blob')
    target = blob_path(blobs, sha)
    if target.is_file() and target.stat().st_size == size:
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    partial = target.with_name(target.name + '.part')
    h = hashlib.sha256(); written = 0
    with partial.open('wb') as out:
        while written < size:
            chunk = stream.read(min(1024**2, size - written))
            if not chunk:
                raise ValueError('incomplete blob upload')
            out.write(chunk); h.update(chunk); written += len(chunk)
    if h.hexdigest() != sha:
        partial.unlink(missing_ok=True)
        raise ValueError('blob hash mismatch')
    partial.replace(target)
    return True

def seed_blobs_from_zip(archive, blobs):
    """Reuse an already uploaded snapshot so an empty blob store is not paid for twice."""
    stored = 0
    with zipfile.ZipFile(archive) as z:
        manifest = json.loads(z.read('snapshot-manifest.json'))
        validate_manifest(manifest)
        for name, meta in manifest['files'].items():
            if store_blob(blobs, meta['sha256'], z.open(name), meta['bytes']):
                stored += 1
    return stored

def materialize_from_blobs(source, manifest, blobs):
    """Rebuild an exact source tree from cached blobs; every file hash is checked again."""
    validate_manifest(manifest)
    for name, meta in manifest['files'].items():
        src = blob_path(blobs, meta['sha256'])
        if not src.is_file() or src.stat().st_size != meta['bytes']:
            raise ValueError('missing blob for ' + name)
        dest = source / name; dest.parent.mkdir(parents=True, exist_ok=True)
        h = hashlib.sha256(); size = 0
        with src.open('rb') as incoming, dest.open('wb') as outgoing:
            for chunk in iter(lambda: incoming.read(1024**2), b''):
                outgoing.write(chunk); h.update(chunk); size += len(chunk)
        if size != meta['bytes'] or h.hexdigest() != meta['sha256']:
            raise ValueError('blob content mismatch: ' + name)
    (source / 'snapshot-manifest.json').write_bytes(canonical(manifest))
    return manifest

def write_json(path, value):
    tmp = path.with_suffix('.tmp')
    tmp.write_bytes(canonical(value)); tmp.replace(path)

def desktop():
    session = ctypes.c_ulong()
    ctypes.windll.kernel32.ProcessIdToSessionId(os.getpid(), ctypes.byref(session))
    user32 = ctypes.windll.user32
    user32.OpenInputDesktop.restype = ctypes.c_void_p
    handle = user32.OpenInputDesktop(0, False, 1)
    unlocked = bool(handle)
    if handle:
        user32.CloseDesktop.argtypes = [ctypes.c_void_p]; user32.CloseDesktop(handle)
    return {'session_id': session.value, 'interactive': session.value != 0, 'input_desktop': unlocked}

class Station:
    def __init__(self, config):
        self.config = config
        self.repo = Path(config['repository']).resolve()
        self.worker_code_sha256 = digest(Path(__file__))
        self.data = Path(config['data']).resolve(); self.data.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock(); self.busy = None
        self.snapshots = self.data / 'snapshots'; self.snapshots.mkdir(exist_ok=True)
        self.jobs = self.data / 'runs'; self.jobs.mkdir(exist_ok=True)
        self.blobs = self.data / 'blobs'; self.blobs.mkdir(exist_ok=True)
        self.manifests = self.data / 'manifests'; self.manifests.mkdir(exist_ok=True)
        self.seeded = False
        # One worker instance per account, including startup/restart races.
        import msvcrt
        self.instance = (self.data / 'worker.lock').open('a+b')
        self.instance.write(b' '); self.instance.flush(); self.instance.seek(0)
        msvcrt.locking(self.instance.fileno(), msvcrt.LK_NBLCK, 1)
        for status in self.jobs.glob('*/status.json'):
            value = json.loads(status.read_text(encoding='utf-8-sig'))
            if value['status'] == 'RUNNING':
                value.update(status='FAIL', error='Worker restarted during job; inspect recovery marker and processes')
                write_json(status, value)

    def manifest_for(self, sid):
        path = self.manifests / (sid + '.json')
        return json.loads(path.read_text(encoding='utf-8-sig')) if path.is_file() else None

    def blob_usage(self):
        files = 0; total = 0
        for path in self.blobs.glob('*/*'):
            if path.is_file():
                files += 1; total += path.stat().st_size
        return {'files': files, 'megabytes': round(total / 1024**2, 1)}

    def seed_from_latest_snapshot(self):
        """Once: fill the blob store from the newest cached snapshot, so the first
        incremental run sends only what changed instead of the whole tree again."""
        self.seeded = True
        archives = sorted(self.snapshots.glob('*.zip'), key=lambda p: p.stat().st_mtime, reverse=True)
        if not archives:
            return 0
        try:
            return seed_blobs_from_zip(archives[0], self.blobs)
        except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
            print('blob seed skipped:', error, file=sys.stderr)
            return 0

    def prune_manifests(self):
        extra = sorted(self.manifests.glob('*.json'), key=lambda p: p.stat().st_mtime, reverse=True)
        for path in extra[KEEP_MANIFESTS:]:
            path.unlink(missing_ok=True)

    def reclaim(self, used_snapshot):
        """Free what the station can rebuild by itself: unpacked source trees, superseded
        archives, interrupted uploads and blobs no stored manifest references. Receipts,
        logs, frames and result.zip stay, so finished checks remain auditable."""
        freed = 0
        # Only this job runs at a time, so every leftover source tree here is already finished.
        for source in self.jobs.glob('*/source'):
            if source.is_dir():
                freed += tree_bytes(source)
                shutil.rmtree(source, ignore_errors=True)
        cutoff = time.time() - BLOB_GRACE
        live = set()
        for manifest in self.manifests.glob('*.json'):
            try:
                live |= {meta['sha256'] for meta in
                         json.loads(manifest.read_text(encoding='utf-8-sig'))['files'].values()}
            except (ValueError, KeyError, OSError):
                continue
        archives = sorted(self.snapshots.glob('*.zip'), key=lambda p: p.stat().st_mtime, reverse=True)
        for path in archives[KEEP_ARCHIVES:]:
            if path.stem == used_snapshot or path.stat().st_mtime > cutoff:
                continue
            freed += path.stat().st_size
            path.unlink(missing_ok=True)
        for path in self.blobs.glob('*/*'):
            if not path.is_file() or path.stat().st_mtime > cutoff:
                continue
            if path.suffix != '.part' and path.name in live:
                continue
            freed += path.stat().st_size
            path.unlink(missing_ok=True)
        return freed

    def disk(self):
        use = shutil.disk_usage(self.data)
        return {'path': str(self.data), 'free_gb': round(use.free / 1024**3, 1),
                'total_gb': round(use.total / 1024**3, 1)}

    def recovery(self):
        marker = Path(os.environ['APPDATA']) / 'Godot/app_userdata/.URMAN.protected-run.lock'
        if not marker.exists():
            return False
        with marker.open('rb') as stream:
            stream.seek(1)
            return bool(stream.read().strip())

    def health(self):
        pins = json.loads((self.repo / 'eng/toolchain.json').read_text(encoding='utf-8-sig'))
        v = pins['godot']['version'].split('.stable')[0]
        godot = self.repo / f'.tools/godot/windows/Godot_v{v}-stable_mono_win64/Godot_v{v}-stable_mono_win64_console.exe'
        dotnet = self.repo / '.tools/dotnet/dotnet.exe'
        tools = (godot.is_file() and dotnet.is_file() and Path(self.config['python']).is_file()
                 and all((self.repo/'eng'/name).is_file() for name in
                         ('run-windows-check.ps1','protected_run.py','remote_common.py')))
        d = desktop(); pending = self.recovery()
        pipeline_locked = False
        try:
            with (self.repo/'.tools/windows-station.lock').open('ab'):
                pass
        except OSError:
            pipeline_locked = True
        # Restart with an orphan runner is blocked even if the source lock is gone.
        orphan = subprocess.run(['tasklist','/FI','IMAGENAME eq '+godot.name,'/FO','CSV','/NH'],capture_output=True,text=True).stdout
        main_name = godot.name.replace('_console.exe', '.exe')
        orphan += subprocess.run(['tasklist','/FI','IMAGENAME eq '+main_name,'/FO','CSV','/NH'],capture_output=True,text=True).stdout
        orphaned = (godot.name.lower() in orphan.lower() or main_name.lower() in orphan.lower() or pipeline_locked) and not self.busy
        return {'ready': bool(tools and d['interactive'] and d['input_desktop'] and not pending and not orphaned),
                'host': os.environ['COMPUTERNAME'], 'desktop': d, 'tools': tools,
                'userdata_recovery_pending': pending, 'orphan_game_process': bool(orphaned),
                'busy_job': self.busy, 'godot': pins['godot']['version'],
                'sdk': json.loads((self.repo/'global.json').read_text(encoding='utf-8-sig'))['sdk']['version'],
                'blob_store': self.blob_usage(), 'disk': self.disk(),
                'gpu_acceptance': self.config.get('gpu_acceptance', 'not-run')}

    def submit(self, spec):
        if set(spec) - {'job_id','snapshot_id','mode','scene','timeout','headless','points','phase','fov'}:
            raise ValueError('unknown job field')
        jid = spec.get('job_id', '')
        if not re.fullmatch('[a-zA-Z0-9_-]{8,64}', jid):
            raise ValueError('invalid job_id')
        sid = spec.get('snapshot_id', '')
        if not re.fullmatch('[a-f0-9]{64}', sid):
            raise ValueError('unknown snapshot')
        if not (self.snapshots / (sid + '.zip')).is_file():
            # Incremental snapshot: the manifest plus every referenced blob must be here.
            manifest = self.manifest_for(sid)
            if manifest is None:
                raise ValueError('unknown snapshot')
            missing = [meta['sha256'] for meta in manifest['files'].values()
                       if not blob_path(self.blobs, meta['sha256']).is_file()]
            if missing:
                raise ValueError(f'snapshot blobs incomplete ({len(missing)} missing); re-run the client')
        if spec.get('mode') not in ('smoke', 'capture'):
            raise ValueError('unsupported mode')
        if type(spec.get('timeout')) is not int or not 1 <= spec['timeout'] <= 300:
            raise ValueError('timeout must be 1..300 seconds')
        if type(spec.get('headless')) is not bool:
            raise ValueError('headless must be boolean')
        scene = spec.get('scene', '')
        if not re.fullmatch(r'res://[\w/.-]+\.tscn', scene) or '..' in scene:
            raise ValueError('invalid scene')
        if spec['mode'] == 'smoke' and not scene.startswith('res://tests/'):
            raise ValueError('smoke requires an existing test scene')
        if spec['mode'] == 'capture' and (scene != 'res://scenes/act1_demo.tscn' or spec['headless']):
            raise ValueError('capture requires native act1_demo')
        points = spec.get('points', '')
        number = r'-?\d+(?:\.\d+)?'
        vector = rf'(?:[A-Za-z][A-Za-z0-9_]*@)?{number},{number},{number}'
        entry = rf'[A-Za-z0-9_-]{{1,48}}:{vector}>{vector}'
        if not isinstance(points, str) or len(points) > 1000 or (points and not re.fullmatch(rf'{entry}(?:;{entry}){{0,7}}', points)):
            raise ValueError('invalid capture points')
        if spec['mode'] == 'capture' and not points:
            raise ValueError('capture points required')
        phase = spec.get('phase', '')
        if not isinstance(phase, str) or (phase and (spec['mode'] != 'capture' or not re.fullmatch('[a-z0-9-]{1,48}', phase))):
            raise ValueError('invalid atmosphere phase')
        fov = spec.get('fov', '')
        if not isinstance(fov, str) or (fov and (spec['mode'] != 'capture' or not re.fullmatch(r'player|\d{2,3}(?:\.\d+)?', fov))):
            raise ValueError('invalid view fov')
        with self.lock:
            path = self.jobs / jid
            if path.exists():
                if json.loads((path/'request.json').read_text(encoding='utf-8-sig')) != spec:
                    raise ValueError('job_id reused with different parameters')
                return 200, json.loads((path/'status.json').read_text(encoding='utf-8-sig'))
            if self.busy:
                return 409, {'error':'station busy', 'job_id':self.busy}
            health = self.health()
            if not health['ready']:
                return 503, {'error':'station not ready', 'health':health}
            path.mkdir(); write_json(path/'request.json', spec)
            status = {'job_id':jid, 'snapshot_id':sid, 'status':'RUNNING', 'started':time.time()}
            write_json(path/'status.json', status); self.busy = jid
            threading.Thread(target=self.execute, args=(path,spec,status), daemon=False).start()
            return 202, status

    def execute(self, path, spec, status):
        output = path/'artifacts'; output.mkdir()
        started = time.monotonic()
        try:
            source = path/'source'; source.mkdir()
            archive = self.snapshots / (spec['snapshot_id'] + '.zip')
            if archive.is_file():
                manifest = unpack_snapshot(archive, source)
            else:
                manifest = materialize_from_blobs(source, self.manifest_for(spec['snapshot_id']), self.blobs)
            verify_source(source, manifest)
            if not (source/'game'/spec['scene'][6:]).is_file():
                raise ValueError('scene does not exist in snapshot')
            for pin in ('global.json','eng/toolchain.json'):
                if json.loads((source/pin).read_text(encoding='utf-8-sig')) != json.loads((self.repo/pin).read_text(encoding='utf-8-sig')):
                    raise ValueError('snapshot toolchain differs from station: '+pin)
            execution_files = {}
            for name in ('eng/run-windows-check.ps1','eng/protected_run.py','eng/remote_common.py'):
                # Git normalizes Windows/Mac line endings differently. Require
                # identical source text, and record both actual byte hashes.
                if (source/name).read_text(encoding='utf-8-sig') != (self.repo/name).read_text(encoding='utf-8-sig'):
                    raise ValueError('station execution infrastructure differs from snapshot: '+name+'; update station before retrying')
                execution_files[name] = {'station_sha256':digest(self.repo/name),'snapshot_sha256':manifest['files'][name]['sha256'],'source_text_equal':True}
            # Station-local override is recorded separately; never overwrite a developer file.
            (source/'game/override.cfg').write_text('[filesystem]\nimport/blender/enabled=false\n')
            command = ['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',
                       str(self.repo/'eng/run-windows-check.ps1'), '-Root',str(source),
                       '-ToolsRoot',str(self.repo), '-Provenance',str(source/'snapshot-manifest.json'),
                       '-Output',str(output), '-Mode',spec['mode'], '-Scene',spec['scene'],
                       '-TimeoutSeconds',str(spec['timeout']), '-Python',self.config['python']]
            if spec['headless']:
                command += ['-Headless']
            if spec.get('points'):
                command += ['-ViewPoints', spec['points']]
            if spec.get('phase'):
                command += ['-AtmospherePhase', spec['phase']]
            if spec.get('fov'):
                command += ['-ViewFov', spec['fov']]
            with (output/'stdout.log').open('wb') as out, (output/'stderr.log').open('wb') as err:
                p = subprocess.Popen(command,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
                # Runner bounds build/content/import/game separately. This outer emergency
                # deadline preserves the guard's recovery marker instead of claiming restore.
                try:
                    code = p.wait(timeout=spec['timeout']*2+1050)
                except subprocess.TimeoutExpired:
                    subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True)
                    p.wait(timeout=10); raise RuntimeError('job deadline exceeded; inspect userdata recovery before next run')
            receipt = json.loads((output/'receipt.json').read_text(encoding='utf-8-sig'))
            receipt.update(station=os.environ['COMPUTERNAME'], job_id=spec['job_id'],
                           snapshot=manifest, snapshot_hashes_verified=True,
                           execution_files=execution_files, worker_code_sha256=self.worker_code_sha256,
                           desktop=desktop(), runner_exit_code=code, worker_elapsed_seconds=round(time.monotonic()-started,3))
            write_json(output/'receipt.json', receipt)
            if code != 0 or receipt.get('status') != 'PASS' or self.recovery():
                raise RuntimeError(receipt.get('error', f'runner failed with exit {code}'))
            write_json(output/'receipt.json', receipt)
            status.update(status='PASS', exit_code=code)
        except Exception as error:
            status.update(status='FAIL', error=str(error), exit_code=1)
            receipt_path = output/'receipt.json'
            receipt = json.loads(receipt_path.read_text(encoding='utf-8-sig')) if receipt_path.exists() else {}
            receipt.update(status='FAIL',error=str(error),job_id=spec['job_id'],
                           snapshot_id=spec['snapshot_id'],userdata_recovery_pending=self.recovery())
            write_json(receipt_path,receipt)
        finally:
            write_json(output/'request.json',spec)
            status['finished'] = time.time()
            with zipfile.ZipFile(path/'result.zip','w',zipfile.ZIP_DEFLATED) as archive:
                for f in output.rglob('*'):
                    if f.is_file() and not f.is_symlink():
                        archive.write(f,f.relative_to(output).as_posix())
            status['result_sha256'] = digest(path/'result.zip')
            with self.lock:
                # Reclaim the disk the next job can rebuild for itself, and never turn a
                # finished check into a failure because a file was still held open.
                try:
                    status['station_freed_bytes'] = self.reclaim(spec['snapshot_id'])
                except OSError as error:
                    print('station reclaim failed:', error, file=sys.stderr)
                write_json(path/'status.json',status)
                self.busy = None

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # No headers, tokens, source paths or credentials in access logs.

    def respond(self, code, value):
        data = canonical(value); self.send_response(code)
        self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(data)))
        self.end_headers(); self.wfile.write(data)

    def authorized(self):
        value = self.headers.get('Authorization','')
        return any(hmac.compare_digest(value, 'Bearer '+token) for token in self.server.station.config['tokens'].values())

    def do_GET(self):
        if not self.authorized():
            return self.respond(401, {'error':'authorization required'})
        station = self.server.station
        if self.path == '/health':
            return self.respond(200,station.health())
        m = re.fullmatch(r'/jobs/([a-zA-Z0-9_-]{8,64})(/result)?',self.path)
        if not m or not (station.jobs/m[1]/'status.json').is_file():
            return self.respond(404,{'error':'unknown job'})
        status = json.loads((station.jobs/m[1]/'status.json').read_text(encoding='utf-8-sig'))
        if not m[2]:
            return self.respond(200,status)
        if status['status'] == 'RUNNING':
            return self.respond(409,{'error':'result not ready'})
        result = station.jobs/m[1]/'result.zip'
        self.send_response(200); self.send_header('Content-Type','application/zip')
        self.send_header('Content-Length',str(result.stat().st_size)); self.end_headers()
        with result.open('rb') as stream:
            shutil.copyfileobj(stream,self.wfile)

    def do_POST(self):
        if not self.authorized():
            return self.respond(401,{'error':'authorization required'})
        self.connection.settimeout(300)
        station = self.server.station
        try:
            size = int(self.headers.get('Content-Length','0'))
            if size <= 0 or size > MAX_ARCHIVE:
                return self.respond(413,{'error':'invalid request size'})
            if self.path == '/snapshots':
                with tempfile.TemporaryDirectory(dir=station.data) as temp:
                    temp = Path(temp); archive = temp/'upload.zip'
                    with archive.open('wb') as dest:
                        remaining = size
                        while remaining:
                            chunk = self.rfile.read(min(1024**2,remaining))
                            if not chunk:
                                raise ValueError('incomplete upload')
                            dest.write(chunk); remaining -= len(chunk)
                    src = temp/'verify'; src.mkdir()
                    manifest = unpack_snapshot(archive,src)
                    sid = manifest['snapshot_id']
                    target = station.snapshots/(sid+'.zip')
                    with station.lock:
                        if not target.exists():
                            if shutil.disk_usage(station.data).free < size + 10*1024**3:
                                raise ValueError('insufficient free station disk')
                            archive.replace(target)
                    return self.respond(200,{'snapshot_id':sid,'hashes_verified':True,'files':len(manifest['files'])})
            blob = re.fullmatch(r'/blobs/([a-f0-9]{64})', self.path)
            if blob:
                if shutil.disk_usage(station.data).free < size + 10*1024**3:
                    raise ValueError('insufficient free station disk')
                with station.lock:
                    stored = store_blob(station.blobs, blob[1], self.rfile, size)
                return self.respond(200,{'sha256':blob[1],'stored':stored})
            if self.path == '/snapshots/plan':
                manifest = json.loads(self.rfile.read(size))
                sid = validate_manifest(manifest)
                if not station.seeded:
                    station.seed_from_latest_snapshot()
                with station.lock:
                    (station.manifests/(sid+'.json')).write_bytes(canonical(manifest))
                    station.prune_manifests()
                missing = sorted({meta['sha256'] for meta in manifest['files'].values()
                                  if not blob_path(station.blobs, meta['sha256']).is_file()})
                return self.respond(200,{'snapshot_id':sid,'known':not missing,
                                         'missing':missing,'files':len(manifest['files'])})
            if self.path == '/jobs' and size <= 16384:
                code, value = station.submit(json.loads(self.rfile.read(size)))
                return self.respond(code,value)
            return self.respond(404,{'error':'unknown operation'})
        except (ValueError, KeyError, OSError, zipfile.BadZipFile) as error:
            return self.respond(400,{'error':str(error)})

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--config',type=Path,required=True)
    args = parser.parse_args(); config=json.loads(args.config.read_text(encoding='utf-8-sig'))
    sys.stderr = (Path(config['data'])/'worker-error.log').open('a',encoding='utf-8',buffering=1)
    sys.stdout = sys.stderr
    station=Station(config)
    server=ThreadingHTTPServer(('127.0.0.1',8765),Handler); server.station=station
    server.serve_forever()

if __name__ == '__main__':
    main()
