#!/usr/bin/env python3
"""Send the current checkout to the private Windows station; never launch locally."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from remote_common import (MAX_ARCHIVE, MAX_SOURCE, REPOSITORY, canonical, candidates,
                           digest, git, source_state)

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

def build_manifest(root):
    """Hash every file the station would receive, without building the archive first."""
    root=Path(root).resolve()
    origin=git(root,'remote','get-url','origin').decode().strip()
    if origin not in (REPOSITORY,'git@github.com:rustemd02/URMAN.git','ssh://git@github.com:rustemd02/URMAN.git'):
        raise ValueError('unexpected repository origin')
    before=source_state(root)
    names=candidates(root)
    manifest={'schema':1,'repository':REPOSITORY,**before,'files':{}}
    total=0
    for name in names:
        h=hashlib.sha256(); size=0
        with (root/name).open('rb') as src:
            for chunk in iter(lambda: src.read(1024**2), b''):
                h.update(chunk); size+=len(chunk)
        total+=size
        if total>MAX_SOURCE:
            raise ValueError('snapshot exceeds source limit')
        manifest['files'][name]={'sha256':h.hexdigest(),'bytes':size}
    if names!=candidates(root) or before!=source_state(root):
        raise ValueError('checkout changed during packaging; retry after edits stop')
    manifest['snapshot_id']=hashlib.sha256(canonical(manifest)).hexdigest()
    return manifest

def write_archive(root, manifest, archive):
    """Write the archive for an already hashed manifest; every byte is verified again."""
    root=Path(root).resolve()
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1) as z:
        for name,meta in manifest['files'].items():
            h=hashlib.sha256(); size=0
            with (root/name).open('rb') as src, z.open(name,'w',force_zip64=True) as dest:
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
        with (Path(root)/name).open('rb') as stream:
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

    def fetch(self,jid,dest):
        status=self.request('/jobs/'+jid)
        if status['status']=='RUNNING':
            raise ValueError('job is still running')
        dest.mkdir(parents=True,exist_ok=True)
        archive=dest/'result.zip'
        with self.request('/jobs/'+jid+'/result',raw=True) as src, archive.open('wb') as out:
            shutil.copyfileobj(src,out)
        if digest(archive)!=status['result_sha256']:
            raise ValueError('result archive checksum mismatch')
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                path=dest/info.filename
                if info.is_dir() or path.resolve().is_relative_to(dest.resolve()) is False or '\\' in info.filename or ':' in info.filename:
                    raise ValueError('unsafe result path')
                path.parent.mkdir(parents=True,exist_ok=True)
                with z.open(info) as src,path.open('wb') as out:
                    shutil.copyfileobj(src,out)
        print('Artifacts:',dest.resolve())
        return status

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('command',choices=['doctor','smoke','capture','status','fetch'])
    p.add_argument('--root',type=Path,default=Path(__file__).resolve().parent.parent)
    p.add_argument('--job-id');p.add_argument('--scene',default='res://tests/player_settings_smoke_test.tscn')
    p.add_argument('--points');p.add_argument('--timeout',type=int,default=300)
    p.add_argument('--phase',help='capture only: authored atmosphere profile id (URMAN_ATMOSPHERE_PHASE)')
    p.add_argument('--fov',help="capture only: 'player' (player camera FOV) or a number; default control views use 70")
    p.add_argument('--diagnostic',choices=['neutral','no-fog','neutral-no-fog'],help='capture only: VIS-039 light-vs-form decomposition')
    p.add_argument('--headless',action='store_true');p.add_argument('--wait',type=int,default=1200)
    p.add_argument('--output',type=Path);p.add_argument('--submit-only',action='store_true')
    args=p.parse_args();c=Client()
    if args.command=='doctor':
        result=c.request('/health');print(json.dumps(result,ensure_ascii=False,indent=2));return 0 if result['ready'] else 1
    if args.command in ('status','fetch'):
        if not args.job_id or not re.fullmatch('[a-zA-Z0-9_-]{8,64}',args.job_id):
            p.error('--job-id required')
        result=c.request('/jobs/'+args.job_id) if args.command=='status' else c.fetch(args.job_id,args.output or args.root/'.codex-captures/remote'/args.job_id)
        print(json.dumps(result,indent=2));return 1 if result['status']=='FAIL' else 0
    health=c.request('/health')
    if not health['ready']:
        raise ValueError('station not ready: '+json.dumps(health))
    # Hash the checkout, then let the station say which files it still lacks; only
    # those are uploaded, so a rebuild after a code change costs kilobytes, not the
    # whole tree. The identity is printed before POST: a lost reply is recovered with
    # status/fetch on this job_id instead of a second run.
    manifest=build_manifest(args.root)
    publish_snapshot(c,args.root,manifest)
    jid=args.job_id or uuid.uuid4().hex
    spec={'job_id':jid,'snapshot_id':manifest['snapshot_id'],'mode':args.command,
          'scene':'res://scenes/act1_demo.tscn' if args.command=='capture' else args.scene,
          'timeout':args.timeout,'headless':args.headless,'points':args.points or ''}
    if args.phase:
        # Optional field: a station worker that predates it rejects the job loudly.
        spec['phase']=args.phase
    if args.fov:
        spec['fov']=args.fov
    if args.diagnostic:
        spec['diagnostic']=args.diagnostic
    print('Job:',jid,'Snapshot:',manifest['snapshot_id'],flush=True)
    try:
        result=c.request('/jobs',spec)
    except urllib.error.HTTPError:
        raise
    except Exception:
        print('Submission reply not confirmed; query status with this job_id before retrying.',file=sys.stderr)
        raise
    if args.submit_only:
        return 0
    deadline=time.monotonic()+args.wait
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
