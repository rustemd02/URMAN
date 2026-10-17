#!/usr/bin/env python3
"""Send the current checkout to the private Windows station; never launch locally."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from remote_common import (MAX_ARCHIVE, MAX_SOURCE, REPOSITORY, canonical, candidates,
                           digest, git, source_state, STUDIO_AUTHORING_FILES,
                           studio_authoring_path, MAX_RESULT_BYTES, MAX_RESULT_FILES)

STUDIO_SCENE = 'res://studio/studio_main.tscn'
FULL_GAME_SCENE = 'res://scenes/full_game.tscn'
PHOTO_WORLDS_GAME_SCENE = 'res://scenes/photoworlds_game.tscn'
NORMAL_UI_TRANSPORT_VERSION = 2
UI_KEY_BASES = ('{ENTER}', '{ESC}', '{DOWN}', '{UP}', '{SPACE}', 'a', 'f', 'v', 'z')
UI_KEY_ADDITIONS = ('j', '{TAB}', '+{TAB}', '{LEFT}', '{RIGHT}')
UI_KEY_CHORDS = frozenset(
    [*UI_KEY_BASES,
     *(modifier + key for modifier in ('^', '+', '^+') for key in UI_KEY_BASES),
     *UI_KEY_ADDITIONS])
UI_MOVE_KEYS = frozenset(('w', 'a', 's', 'd', 'e'))
INTERACTIVE_FRAME_PNG_LIMIT = 16 * 1024**2

def check_result_destination(path):
    try:
        metadata=path.lstat()
    except FileNotFoundError:
        return
    if stat.S_ISLNK(metadata.st_mode) or getattr(metadata,'st_file_attributes',0) & 0x0400:
        raise ValueError('result destination contains a link')

def settings():
    path=Path(os.environ.get('URMAN_STATION_CONFIG',str(Path.home()/'.config/urman-station/client.json')))
    c=json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else {}
    url=os.environ.get('URMAN_STATION_URL',c.get('url','')).rstrip('/')
    parsed=urllib.parse.urlsplit(url)
    if parsed.scheme != 'https' and not (parsed.scheme == 'http' and parsed.hostname in ('127.0.0.1','localhost')):
        raise ValueError('configure a private HTTPS station URL (HTTP only for loopback diagnosis)')
    if parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path:
        raise ValueError('station URL must contain only scheme, host and port')
    token=os.environ.get('URMAN_STATION_TOKEN','')
    if not token and c.get('token_file'):
        token=Path(c['token_file']).expanduser().read_text(encoding='utf-8-sig').strip()
    if not token:
        raise ValueError('configure URMAN_STATION_TOKEN or a personal token_file outside Git')
    # Some Macs run the Tailscale daemon without root (userspace networking): there is no
    # system tunnel and no MagicDNS, so only the daemon's loopback HTTP proxy can resolve
    # and reach the tailnet. Take that proxy from the same out-of-Git config and keep it
    # loopback-only, so the bearer token never travels through a remote proxy.
    proxy=os.environ.get('URMAN_STATION_PROXY',c.get('proxy','')).strip()
    if proxy and not re.fullmatch(r'https?://(?:127\.0\.0\.1|localhost|\[::1\])(?::\d{1,5})?/?',proxy):
        raise ValueError('station proxy must be a loopback HTTP URL')
    return url,token,proxy

def build_manifest(root, *, studio_authoring=False):
    """Hash every file the station would receive, without building the archive first."""
    root=Path(root).resolve()
    origin=git(root,'remote','get-url','origin').decode().strip()
    if origin not in (REPOSITORY,'git@github.com:rustemd02/URMAN.git','ssh://git@github.com:rustemd02/URMAN.git'):
        raise ValueError('unexpected repository origin')
    before=source_state(root)
    names=candidates(root, studio_authoring=studio_authoring)
    manifest={'schema':1,'repository':REPOSITORY,**before,'files':{}}
    total=0
    for name in names:
        h=hashlib.sha256(); size=0
        path=studio_authoring_path(root,name) if name in STUDIO_AUTHORING_FILES else root/name
        with path.open('rb') as src:
            for chunk in iter(lambda: src.read(1024**2), b''):
                h.update(chunk); size+=len(chunk)
        total+=size
        if total>MAX_SOURCE:
            raise ValueError('snapshot exceeds source limit')
        manifest['files'][name]={'sha256':h.hexdigest(),'bytes':size}
    if names!=candidates(root, studio_authoring=studio_authoring) or before!=source_state(root):
        raise ValueError('checkout changed during packaging; retry after edits stop')
    manifest['snapshot_id']=hashlib.sha256(canonical(manifest)).hexdigest()
    return manifest

def write_archive(root, manifest, archive):
    """Write the archive for an already hashed manifest; every byte is verified again."""
    root=Path(root).resolve()
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as z:
        for name,meta in manifest['files'].items():
            h=hashlib.sha256(); size=0
            path=studio_authoring_path(root,name) if name in STUDIO_AUTHORING_FILES else root/name
            with path.open('rb') as src, z.open(name,'w',force_zip64=True) as dest:
                for chunk in iter(lambda: src.read(1024**2), b''):
                    dest.write(chunk); h.update(chunk); size+=len(chunk)
            if size!=meta['bytes'] or h.hexdigest()!=meta['sha256']:
                raise ValueError('file changed during packaging: '+name)
        z.writestr('snapshot-manifest.json', canonical(manifest))
    if archive.stat().st_size>MAX_ARCHIVE:
        raise ValueError('snapshot exceeds upload limit')

def publish_snapshot(client, root, manifest, log=print):
    """Send only the files the station lacks; fall back to the whole archive for a
    station that does not support incremental snapshots yet."""
    try:
        plan=client.request('/snapshots/plan',manifest)
    except urllib.error.HTTPError as error:
        if error.code not in (404,405):
            raise
        plan=None
    if plan is None:
        with tempfile.TemporaryDirectory(prefix='urman-snapshot-') as tmp:
            archive=Path(tmp)/'source.zip'
            write_archive(root,manifest,archive)
            log(f'Full snapshot: this station has no incremental support, sending '
                f'{archive.stat().st_size/1024**2:.1f} MB')
            with archive.open('rb') as stream:
                accepted=client.request('/snapshots',stream)
        if accepted['snapshot_id']!=manifest['snapshot_id'] or not accepted['hashes_verified']:
            raise ValueError('snapshot acknowledgement mismatch')
        return
    if plan.get('snapshot_id')!=manifest['snapshot_id']:
        raise ValueError('snapshot acknowledgement mismatch')
    names={meta['sha256']:name for name,meta in manifest['files'].items()}
    missing=[sha for sha in plan.get('missing',[]) if sha in names]
    sent=sum(manifest['files'][names[sha]]['bytes'] for sha in missing)
    log(f'Incremental snapshot: station already has {len(manifest["files"])-len(missing)} '
        f'of {len(manifest["files"])} files, sending {len(missing)} ({sent/1024**2:.1f} MB)')
    for index,sha in enumerate(missing,1):
        name=names[sha]; size=manifest['files'][name]['bytes']
        log(f'  [{index}/{len(missing)}] {name} ({size/1024**2:.2f} MB)')
        path=studio_authoring_path(root,name) if name in STUDIO_AUTHORING_FILES else Path(root)/name
        with path.open('rb') as stream:
            stored=client.request('/blobs/'+sha,stream)
        if stored.get('sha256')!=sha:
            raise ValueError('blob acknowledgement mismatch')

class Client:
    def __init__(self):
        self.url,self.token,self.proxy=settings()
        # urllib honors HTTPS_PROXY, HTTP_PROXY and platform proxy configuration.
        # Keep normal certificate verification and prohibit credential redirects.
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self,*args,**kwargs):
                raise ValueError('station redirect refused')
        handlers=[NoRedirect()]
        if self.proxy:
            # An explicit proxy replaces the environment-derived ProxyHandler.
            handlers.append(urllib.request.ProxyHandler({'http':self.proxy,'https':self.proxy}))
        self.opener=urllib.request.build_opener(*handlers)

    def request(self,path,data=None,raw=False):
        headers={'Authorization':'Bearer '+self.token}
        if hasattr(data,'read'):
            headers.update({'Content-Type':'application/zip','Content-Length':str(os.fstat(data.fileno()).st_size)})
        elif data is not None:
            data=canonical(data);headers['Content-Type']='application/json'
        req=urllib.request.Request(self.url+path,data=data,headers=headers)
        response=self.opener.open(req,timeout=300 if data is not None else 30)
        if raw:
            return response
        with response:
            return json.load(response)

    def ui_action(self,jid,action):
        return self.request('/jobs/'+jid+'/ui',action)

    def capture_current(self,jid):
        return self.request('/jobs/'+jid+'/ui',{'action':'capture-current'},raw=True)

    def fetch(self,jid,dest):
        status=self.request('/jobs/'+jid)
        if status['status']=='RUNNING':
            raise ValueError('job is still running')
        if not re.fullmatch('[a-f0-9]{64}',status.get('result_sha256','')):
            raise ValueError('job has no preserved result archive; inspect station error and retained source')
        dest=Path(os.path.abspath(dest))
        for current in [*reversed(dest.parents),dest]:
            check_result_destination(current)
        dest.mkdir(parents=True,exist_ok=True)
        dest=dest.resolve()
        archive=dest/'result.zip'
        check_result_destination(archive)
        with self.request('/jobs/'+jid+'/result',raw=True) as src, archive.open('wb') as out:
            written=0
            for chunk in iter(lambda:src.read(1024**2),b''):
                written+=len(chunk)
                if written>MAX_ARCHIVE:
                    raise ValueError('result archive exceeds download limit')
                out.write(chunk)
        if digest(archive)!=status['result_sha256']:
            raise ValueError('result archive checksum mismatch')
        with zipfile.ZipFile(archive) as z:
            entries=z.infolist()
            if len(entries)>MAX_RESULT_FILES or sum(i.file_size for i in entries)>MAX_RESULT_BYTES:
                raise ValueError('result entries exceed extraction limits')
            names=set()
            for info in entries:
                parts=info.filename.split('/')
                name=info.filename.casefold()
                if (info.is_dir() or not info.filename or PurePosixPath(info.filename).is_absolute()
                        or '\\' in info.filename or ':' in info.filename
                        or any(c in info.filename for c in '<>"|?*')
                        or any(ord(c)<32 for c in info.filename)
                        or name in names or name=='result.zip'
                        or stat.S_ISLNK(info.external_attr >> 16) or info.external_attr & 0x0400
                        or any(part in ('','.','..') or part.rstrip(' .')!=part
                               or re.fullmatch(r'(?i)(con|prn|aux|nul|com[0-9]|lpt[0-9])(\..*)?',part)
                               for part in parts)):
                    raise ValueError('unsafe result path')
                names.add(name)
                current=dest
                for part in parts:
                    current=current/part
                    check_result_destination(current)
            if any('/'.join(name.split('/')[:index]) in names
                   for name in names for index in range(1,len(name.split('/')))):
                raise ValueError('result file paths conflict')
            expanded=0
            for info in entries:
                path=dest/info.filename
                path.parent.mkdir(parents=True,exist_ok=True)
                with z.open(info) as src,path.open('wb') as out:
                    for chunk in iter(lambda:src.read(1024**2),b''):
                        expanded+=len(chunk)
                        if expanded>MAX_RESULT_BYTES:
                            raise ValueError('result exceeds extraction byte limit')
                        out.write(chunk)
        print('Artifacts:',dest.resolve())
        return status

def studio_readiness(client):
    try:
        result=client.request('/health/studio')
    except urllib.error.HTTPError as error:
        if error.code == 404:
            raise ValueError('Windows station does not support Studio readiness yet; update and restart its worker.') from error
        raise
    if not isinstance(result,dict) or type(result.get('ready')) is not bool:
        raise ValueError('Studio readiness response must contain a boolean ready field')
    return result

def native_ui_readiness(client, *, require_idle_ready):
    result=client.request('/health')
    if not isinstance(result,dict):
        raise ValueError('Windows station health response is invalid')
    if result.get('normal_ui_transport_version') != NORMAL_UI_TRANSPORT_VERSION:
        raise ValueError('Windows station worker lacks native play/UI transport; update and restart its worker.')
    desktop=result.get('desktop')
    if (not isinstance(desktop,dict) or desktop.get('interactive') is not True
            or desktop.get('input_desktop') is not True):
        raise ValueError('Windows station does not have an interactive input desktop')
    if require_idle_ready and result.get('ready') is not True:
        raise ValueError('station not ready: '+json.dumps(result,ensure_ascii=False))
    if require_idle_ready and result.get('busy_job') is not None:
        raise ValueError('station is busy; native play requires an idle station')
    return result

def new_interactive_capture_destination(path):
    if path is None or not path.is_absolute():
        raise ValueError('capture-current --output must be an absolute file path')
    for current in [*reversed(path.parents),path]:
        check_result_destination(current)
    if not path.parent.is_dir():
        raise ValueError('capture-current output parent directory does not exist')
    if path.exists() or path.is_symlink():
        raise ValueError('capture-current output already exists')
    return path

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('command',choices=['doctor','smoke','capture','studio','play','play-full','play-product','ui',
                                      'capture-current','status','fetch'])
    p.add_argument('--root',type=Path,default=Path(__file__).resolve().parent.parent)
    p.add_argument('--job-id',help='capture-current uses your own running play, play-full, or play-product job ID');p.add_argument('--scene')
    p.add_argument('--points');p.add_argument('--timeout',type=int)
    p.add_argument('--phase',help='capture only: authored atmosphere profile id (URMAN_ATMOSPHERE_PHASE)')
    p.add_argument('--fov',help="capture only: 'player' (player camera FOV) or a number; default control views use 70")
    p.add_argument('--headless',action='store_true');p.add_argument('--wait',type=int)
    p.add_argument('--output',type=Path);p.add_argument('--submit-only',action='store_true')
    p.add_argument('--ui-action',choices=['keys','move'])
    p.add_argument('--keys',help='ui keys only: one fixed key or key chord')
    p.add_argument('--key',help='ui move only: w, a, s, d, or e')
    p.add_argument('--duration-ms',type=int,help='ui move only: 1..2000 milliseconds')
    p.add_argument('--studio-authoring',action='store_true',
                   help='include the two fixed house generator inputs in the source snapshot')
    p.add_argument('--studio-readiness',action='store_true',
                   help='doctor only: check redacted native Studio, Codex, and Blender readiness')
    p.add_argument('--studio-restarts',type=int,default=0,
                   help='studio only: reopen the same authored workspace 0..2 times after clean exits')
    args=p.parse_args()
    if not 0<=args.studio_restarts<=2 or (args.studio_restarts and args.command!='studio'):
        p.error('--studio-restarts requires studio and a count of 0..2')
    if args.studio_readiness and args.command!='doctor':
        p.error('--studio-readiness is only valid with doctor')
    if args.command=='studio' and (args.scene is not None or args.headless or args.points is not None
                                   or args.phase is not None or args.fov is not None):
        p.error('studio uses a fixed native scene and does not accept --scene, --headless, --points, --phase, or --fov')
    studio=args.command=='studio'
    native_full=args.command=='play-full'
    native_product=args.command=='play-product'
    native_interactive=native_full or native_product
    play=args.command in ('play','play-full','play-product')
    capture_current=args.command=='capture-current'
    capture_destination=None
    if native_interactive and args.job_id is not None and not re.fullmatch('[a-f0-9]{32}',args.job_id):
        p.error('play job IDs must be 32 lowercase hexadecimal characters')
    if play and (args.scene is not None or args.headless or args.points is not None
                 or args.phase is not None or args.fov is not None or args.studio_restarts
                 or args.studio_authoring or args.ui_action is not None or args.keys is not None
                 or args.key is not None or args.duration_ms is not None
                 or (native_interactive and args.output is not None and args.wait is None)):
        p.error('play/play-full/play-product uses its fixed native scene; UI controls require the ui command')
    if args.command=='ui':
        if not args.job_id or not re.fullmatch('[a-f0-9]{32}',args.job_id):
            p.error('ui requires a 32-character lowercase hexadecimal --job-id from play')
        if args.scene is not None or args.points is not None or args.phase is not None or args.fov is not None or args.headless:
            p.error('ui does not accept scene, capture, or headless options')
        if args.timeout is not None:
            p.error('ui does not accept job timeout options')
        if args.ui_action=='keys':
            if args.keys not in UI_KEY_CHORDS or args.key is not None or args.duration_ms is not None:
                p.error('ui keys requires one supported --keys value and no movement options')
            ui_request={'action':'keys','keys':args.keys}
        elif args.ui_action=='move':
            if (args.key not in UI_MOVE_KEYS or type(args.duration_ms) is not int
                    or not 1<=args.duration_ms<=2000 or args.keys is not None):
                p.error('ui move requires --key w/a/s/d/e and --duration-ms 1..2000')
            ui_request={'action':'move','key':args.key,'durationMs':args.duration_ms}
        else:
            p.error('ui requires --ui-action keys or move')
        if args.wait is not None or args.output is not None or args.submit_only or args.studio_authoring or args.studio_restarts:
            p.error('ui does not accept job submission or output options')
    if capture_current:
        if not args.job_id or not re.fullmatch('[a-f0-9]{32}',args.job_id):
            p.error('capture-current requires a 32-character lowercase hexadecimal --job-id from your own play, play-full, or play-product job')
        if args.output is None:
            p.error('capture-current requires --output with an absolute file path')
        if (args.scene is not None or args.points is not None or args.phase is not None
                or args.fov is not None or args.headless or args.timeout is not None
                or args.wait is not None or args.submit_only or args.studio_authoring
                or args.studio_restarts):
            p.error('capture-current accepts only --job-id and --output')
        try:
            capture_destination=new_interactive_capture_destination(args.output)
        except ValueError as error:
            p.error(str(error))
    if studio and args.timeout is not None and not 1<=args.timeout<=3600:
        p.error('studio timeout must be 1..3600 seconds')
    if play and args.timeout is not None and not 1<=args.timeout<=1200:
        p.error('play/play-full/play-product timeout must be 1..1200 seconds')
    if args.command!='ui' and (args.ui_action is not None or args.keys is not None
                               or args.key is not None or args.duration_ms is not None):
        p.error('--ui-action, --keys, --key, and --duration-ms require the ui command')
    scene=STUDIO_SCENE if studio else (PHOTO_WORLDS_GAME_SCENE if native_product else FULL_GAME_SCENE if native_full else 'res://scenes/act1_demo.tscn' if play else (args.scene if args.scene is not None
                                       else 'res://tests/player_settings_smoke_test.tscn')
    )
    timeout=args.timeout if args.timeout is not None else (1200 if studio or play else 300)
    wait=args.wait if args.wait is not None else (3600 if studio else 1200)
    c=Client()
    if args.command=='doctor':
        result=studio_readiness(c) if args.studio_readiness else c.request('/health')
        print(json.dumps(result,ensure_ascii=False,indent=2));return 0 if result['ready'] else 1
    if args.command in ('status','fetch'):
        if not args.job_id or not re.fullmatch('[a-zA-Z0-9_-]{8,64}',args.job_id):
            p.error('--job-id required')
        result=c.request('/jobs/'+args.job_id) if args.command=='status' else c.fetch(args.job_id,args.output or args.root/'.codex-captures/remote'/args.job_id)
        print(json.dumps(result,indent=2));return 1 if result['status']=='FAIL' else 0
    if args.command=='ui':
        native_ui_readiness(c,require_idle_ready=False)
        result=c.ui_action(args.job_id,ui_request)
        print(json.dumps(result,ensure_ascii=False,indent=2))
        return 0 if result.get('ok') is True else 1
    if capture_current:
        native_ui_readiness(c,require_idle_ready=False)
        with c.capture_current(args.job_id) as response:
            if response.headers.get_content_type()!='image/png':
                raise ValueError('station did not return a verified PNG viewport')
            try:
                content_length=int(response.headers.get('Content-Length',''))
            except ValueError as error:
                raise ValueError('station viewport length is invalid') from error
            expected_hash=response.headers.get('X-URMAN-Frame-SHA256','')
            if (not 1<=content_length<=INTERACTIVE_FRAME_PNG_LIMIT
                    or not re.fullmatch('[a-f0-9]{64}',expected_hash)):
                raise ValueError('station viewport response is outside its verified bounds')
            png=response.read(INTERACTIVE_FRAME_PNG_LIMIT+1)
        if (len(png)!=content_length or len(png)>INTERACTIVE_FRAME_PNG_LIMIT
                or png[:8]!=b'\x89PNG\r\n\x1a\n'
                or hashlib.sha256(png).hexdigest()!=expected_hash):
            raise ValueError('station viewport PNG failed its length or checksum check')
        created=False
        try:
            with capture_destination.open('xb') as output:
                created=True
                output.write(png)
        except Exception:
            if created:
                capture_destination.unlink(missing_ok=True)
            raise
        print('Current game viewport saved:',capture_destination)
        return 0
    if studio:
        health=studio_readiness(c)
        if not health['ready']:
            raise ValueError('Studio station not ready: '+json.dumps(health,ensure_ascii=False))
    elif native_interactive:
        health=native_ui_readiness(c,require_idle_ready=True)
    else:
        health=c.request('/health')
        if not health['ready']:
            raise ValueError('station not ready: '+json.dumps(health))
    # Hash the checkout, then let the station say which files it still lacks; only
    # those are uploaded, so a rebuild after a code change costs kilobytes, not the
    # whole tree. The identity is printed before POST: a lost reply is recovered with
    # status/fetch on this job_id instead of a second run.
    manifest=build_manifest(args.root, studio_authoring=studio or args.studio_authoring)
    publish_snapshot(c,args.root,manifest)
    jid=args.job_id or uuid.uuid4().hex
    spec={'job_id':jid,'snapshot_id':manifest['snapshot_id'],'mode':'play' if play else args.command,
          'scene':'res://scenes/act1_demo.tscn' if args.command=='capture' else scene,
          'timeout':timeout,'headless':args.headless,'points':args.points or ''}
    if args.studio_restarts:
        spec['studio_restarts']=args.studio_restarts
    if args.phase:
        # Optional field: a station worker that predates it rejects the job loudly.
        spec['phase']=args.phase
    if args.fov:
        spec['fov']=args.fov
    print('Job:',jid,'Snapshot:',manifest['snapshot_id'],flush=True)
    try:
        result=c.request('/jobs',spec)
    except urllib.error.HTTPError:
        raise
    except Exception:
        print('Submission reply not confirmed; query status with this job_id before retrying.',file=sys.stderr)
        raise
    if native_interactive and args.wait is None:
        print('Native play submitted. Use ui --job-id '+jid+' for bounded input; use status/fetch after it exits.')
        return 0
    if args.submit_only:
        return 0
    deadline=time.monotonic()+wait
    while result['status']=='RUNNING' and time.monotonic()<deadline:
        time.sleep(2);result=c.request('/jobs/'+jid)
    if result['status']=='RUNNING':
        print('Bounded wait expired; use status/fetch with job_id. No local fallback.',file=sys.stderr);return 2
    result=c.fetch(jid,args.output or args.root/'.codex-captures/remote'/jid)
    print('Remote check:',result['status']);return 0 if result['status']=='PASS' else 1

if __name__=='__main__':
    try:
        raise SystemExit(main())
    except urllib.error.HTTPError as error:
        print('Station HTTP failure:',error.code,error.read().decode('utf-8','replace')[:2000],file=sys.stderr);raise SystemExit(1)
    except Exception as error:
        print('Remote check not-run/failed:',str(error),file=sys.stderr);raise SystemExit(1)
