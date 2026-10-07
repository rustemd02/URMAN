#!/usr/bin/env python3
"""Windows station disk housekeeping: report, prune regenerable data, move the working set off C:.

Read-only unless --apply. The station can rebuild everything this tool deletes (source trees
from blobs, blobs' contents from the newest archive), so no receipt, log, frame or result.zip is
touched. Game saves, the userdata guard marker, tokens and Git state are out of scope.
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# The worker derives these four from config['data']; together they are the whole growth.
WORKING = ('snapshots', 'runs', 'blobs', 'manifests')
# A client that lost its reply may still be uploading this content; do not collect it mid-flight.
GRACE = 24 * 3600
# The newest archive is what an empty blob store is seeded from, so exactly one stays.
KEEP_ZIPS = 1


def human(value):
    return f'{value / 1024**2:.1f} MB'


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def files_of(root):
    return [p for p in sorted(root.rglob('*')) if p.is_file() and not p.is_symlink()]


def dir_bytes(root):
    return sum(p.stat().st_size for p in files_of(root)) if root.is_dir() else 0


def load_config(path):
    config = read_json(path)
    for key in ('data', 'repository', 'python'):
        if key not in config:
            raise SystemExit(f'{path} has no "{key}"; refusing to guess the station layout')
    return config


def volumes(*paths):
    rows = []
    for path in paths:
        probe = Path(path)
        while not probe.exists() and str(probe) != probe.anchor:
            probe = probe.parent
        use = shutil.disk_usage(probe)
        rows.append((str(path), round(use.free / 1024**3, 2), round(use.total / 1024**3, 2)))
    return rows


def running_jobs(data):
    active = []
    for status in (data / 'runs').glob('*/status.json'):
        try:
            if read_json(status).get('status') == 'RUNNING':
                active.append(status.parent.name)
        except (ValueError, OSError):
            continue
    return active


def manifest_blobs(data):
    """Blob hashes the stored manifests still point at."""
    live = set()
    for path in (data / 'manifests').glob('*.json'):
        try:
            live |= {meta['sha256'] for meta in read_json(path)['files'].values()}
        except (ValueError, KeyError, OSError):
            continue
    return live


def plan(data, now=None, keep=()):
    now = now or time.time()
    items = []
    for source in sorted((data / 'runs').glob('*/source')):
        if source.is_dir() and source.parent.name not in keep:
            items.append((source, dir_bytes(source), 'source tree, rebuilt from blobs on the next job'))
    for part in (data / 'blobs').glob('*/*.part'):
        if part.stat().st_mtime < now - GRACE:
            items.append((part, part.stat().st_size, 'interrupted blob upload'))
    archives = sorted((data / 'snapshots').glob('*.zip'), key=lambda p: p.stat().st_mtime, reverse=True)
    for path in archives[KEEP_ZIPS:]:
        if path.stat().st_mtime < now - GRACE:
            items.append((path, path.stat().st_size, 'superseded snapshot archive'))
    live = manifest_blobs(data)
    for path in (data / 'blobs').glob('*/*'):
        if path.is_file() and path.suffix != '.part' and path.name not in live \
                and path.stat().st_mtime < now - GRACE:
            items.append((path, path.stat().st_size, 'blob no stored manifest references'))
    return items


def report(config, data, target):
    print(f'station data : {data}')
    print(f'repository   : {config["repository"]}')
    for path, free, total in volumes(data, config['repository'], target):
        print(f'  on {path}\n    free {free} GB of {total} GB')
    print('\nworking set')
    for name in WORKING:
        root = data / name
        print(f'  {name:<10} {human(dir_bytes(root)):>12}  {len(files_of(root)) if root.is_dir() else 0} files')
    print(f'  {".tools":<10} {human(dir_bytes(Path(config["repository"]) / ".tools")):>12}  pinned Godot/.NET, moved only by hand')
    jobs = [p for p in (data / 'runs').glob('*') if p.is_dir()]
    active = running_jobs(data)
    print(f'\n{len(jobs)} job directories; RUNNING: {active or "none"}')
    items = plan(data, keep=active)
    print(f'prunable now : {human(sum(size for _, size, _ in items))} in {len(items)} entries')
    for path, size, reason in sorted(items, key=lambda item: -item[1])[:15]:
        print(f'  {human(size):>12}  {path.relative_to(data)}  ({reason})')
    if len(items) > 15:
        print(f'  ... and {len(items) - 15} more')
    unknown = [p.name for p in sorted(data.iterdir()) if p.is_dir() and p.name not in WORKING]
    if unknown:
        print('\nWARNING these directories under data are not the known working set, so --relocate '
              f'refuses until you say what writes them: {unknown}')


def apply_plan(data, items):
    freed = 0
    for path, size, reason in sorted(items, key=lambda item: -item[1]):
        try:
            if path.is_dir():
                shutil.rmtree(path)
            else:
                path.unlink()
        except OSError as error:
            print(f'  SKIPPED {path}: {error}', file=sys.stderr)
            continue
        freed += size
        print(f'  removed {human(size):>12}  {path.relative_to(data)}  ({reason})')
    return freed


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024**2), b''):
            digest.update(chunk)
    return digest.hexdigest()


def tree_signature(root):
    """Relative path and size of every file, plus the hash of each large one: enough to prove a copy."""
    out = {}
    for path in files_of(root):
        entry = {'bytes': path.stat().st_size}
        if entry['bytes'] > 512 * 1024:
            entry['sha256'] = sha256(path)
        out[path.relative_to(root).as_posix()] = entry
    return out


def robocopy(src, dst, log, files_only=False):
    mode = '/FILES' if files_only else '/E'
    code = subprocess.run(['robocopy', str(src), str(dst), mode, '/COPY:DAT', '/DCOPY:DAT',
                           '/R:1', '/W:1', '/NP', '/NFL', '/NDL', f'/LOG:{log}']).returncode
    if code >= 8:
        raise SystemExit(f'robocopy {src} -> {dst} failed with code {code}; see {log}')


def verify(src, dst):
    before, after = tree_signature(src), tree_signature(dst)
    if not before or before != after:
        missing = sorted(set(before) - set(after))[:5]
        differing = [name for name in before if name in after and before[name] != after[name]][:5]
        raise SystemExit(f'{src} and {dst} differ: {len(before) - len(after)} missing {missing}, changed {differing}')
    return len(before), sum(entry['bytes'] for entry in before.values())


def rewrite_config(config_path, data_value, apply):
    original = read_json(config_path)
    text = json.dumps({**original, 'data': str(data_value)}, indent=2, ensure_ascii=False)
    restored = json.loads(text)
    for key, value in original.items():
        if key != 'data' and restored.get(key) != value:
            raise SystemExit(f'config round trip changed "{key}"; refusing to rewrite {config_path}')
    print(f'config       : {config_path}\n  data -> {data_value}')
    if not apply:
        print('  (--apply not given: nothing written)')
        return
    backup = config_path.with_name(config_path.name + '.before-' + datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S'))
    shutil.copy2(config_path, backup)
    temp = config_path.with_suffix('.tmp')
    temp.write_text(text, encoding='utf-8')
    os.replace(temp, config_path)
    print(f'  backup     : {backup}')


def task(repository, action):
    script = Path(repository) / 'eng' / 'manage-windows-worker.ps1'
    if os.name != 'nt':
        print(f'  stop or start the scheduled task "URMAN Windows Test Worker" on the station: '
              f'powershell -File {script} -Action {action}')
        return
    code = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                           '-File', str(script), '-Action', action]).returncode
    if code:
        raise SystemExit(f'worker {action} failed with code {code}')
    print(f'worker       : {action}ed')


def relocate(data, target, apply, log):
    if os.name != 'nt':
        raise SystemExit('relocation runs on the Windows station, where robocopy is available')
    target = Path(target)
    if target.exists() and any(target.iterdir()):
        raise SystemExit(f'{target} exists and is not empty; pick a fresh directory')
    unknown = [p for p in sorted(data.iterdir()) if p.is_dir() and p.name not in WORKING]
    if unknown:
        raise SystemExit('these directories under data are not part of the known working set, so '
                         'moving only the known ones would leave them behind: '
                         + ', '.join(str(p) for p in unknown)
                         + '. Find out what writes them, add them to WORKING, then rerun.')
    needed = dir_bytes(data)
    if not target.parent.exists():
        raise SystemExit(f'{target.parent} does not exist; is the D: volume attached?')
    free = shutil.disk_usage(target.parent).free
    if free < needed + 20 * 1024**3:
        raise SystemExit(f'{target.parent} has {round(free / 1024**3, 1)} GB free, needs '
                         f'{round(needed / 1024**3, 1)} GB plus a 20 GB margin')
    loose = [p for p in sorted(data.iterdir()) if p.is_file()]
    print(f'relocate     : {data} -> {target} ({human(needed)} in '
          f'{len(WORKING)} trees plus {len(loose)} loose files)')
    if not apply:
        print('  (--apply not given: nothing copied)')
        return
    dirs = []
    for name in WORKING:
        src = data / name
        if not src.is_dir():
            continue
        robocopy(src, target / name, log)
        copied, size = verify(src, target / name)
        print(f'  {name:<10} copied {copied} files, {human(size)}, hashes verified')
        dirs.append(name)
    if loose:
        robocopy(data, target, log, files_only=True)
        for path in loose:
            src, dst = data / path.name, target / path.name
            if src.stat().st_size != dst.stat().st_size or sha256(src) != sha256(dst):
                raise SystemExit(f'copied file differs: {path.name}')
        print(f'  loose      copied {len(loose)} files, {human(sum(p.stat().st_size for p in loose))}')
    (target / 'maintenance.json').write_text(json.dumps({
        'moved_from': str(data), 'dirs': dirs, 'files': [p.name for p in loose],
        'at': datetime.now(timezone.utc).isoformat()}, indent=2), encoding='utf-8')
    print(f'  recorded in {target/"maintenance.json"}; the originals are removed only by --remove-old-copy')


def drop_old(config_path, apply):
    data = Path(load_config(config_path)['data'])
    marker = data / 'maintenance.json'
    if not marker.is_file():
        raise SystemExit(f'{marker} is missing; nothing is recorded as relocated')
    record = read_json(marker)
    old = Path(record['moved_from'])
    total = 0
    for name in record.get('dirs', []):
        src, dst = old / name, data / name
        if not src.is_dir():
            continue
        verify(src, dst)
        size = dir_bytes(src)
        total += size
        print(f'  {name:<10} {human(size)} at {src}')
        if apply:
            shutil.rmtree(src)
            print('             removed')
    for name in record.get('files', []):
        src, dst = old / name, data / name
        if not src.is_file():
            continue
        if src.stat().st_size != dst.stat().st_size or sha256(src) != sha256(dst):
            raise SystemExit(f'{src} and {dst} differ; keeping the original')
        total += src.stat().st_size
        print(f'  {name:<10} {human(src.stat().st_size)} at {src}')
        if apply:
            src.unlink()
            print('             removed')
    print(f'reclaimed on the old volume: {human(total)}' + ('' if apply else '  (--apply not given)'))
    if apply and old.is_dir():
        left = sorted(p.name for p in old.iterdir())
        print(f'  left at {old} on purpose, config and tokens are read before data: {left or "nothing"}')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--config', type=Path, default=Path(os.environ.get('LOCALAPPDATA') or Path.home()) / 'URMAN-STATION' / 'station-config.json')
    parser.add_argument('--relocate', metavar='TARGET', help='move the working set here, e.g. D:\\URMAN-STATION\\data')
    parser.add_argument('--remove-old-copy', action='store_true', help='delete the recorded copies left on the old volume')
    parser.add_argument('--apply', action='store_true', help='perform deletions, the copy and the config rewrite')
    parser.add_argument('--keep-worker-stopped', action='store_true', help='leave the scheduled worker stopped afterwards')
    args = parser.parse_args()

    if not args.config.is_file():
        raise SystemExit(f'{args.config} not found; pass --config with the station path')
    config = load_config(args.config)
    data = Path(config['data'])
    if not data.is_dir():
        raise SystemExit(f'config["data"] = {data} does not exist')

    if args.remove_old_copy:
        if args.apply and os.name != 'nt':
            raise SystemExit('deleting the recorded old copies belongs on the Windows station')
        drop_old(args.config, args.apply)
        return 0

    if args.apply and os.name != 'nt':
        raise SystemExit('prune and relocate changes belong on the Windows station')
    active = running_jobs(data)
    if active and args.apply and args.relocate:
        raise SystemExit(f'job(s) RUNNING: {active}; relocation needs an idle station, pruning does not')

    report(config, data, Path(args.relocate) if args.relocate else data)
    if not args.apply:
        print('\nDry run. Add --apply to prune, or --relocate <path> --apply to move the working set.')
        return 0

    stopped = False
    if args.relocate:
        task(config['repository'], 'stop')
        stopped = True
    print('\npruning')
    print(f'  freed {human(apply_plan(data, plan(data, keep=running_jobs(data))))}')
    if args.relocate:
        log = Path(os.environ.get('TEMP', str(data))) / f'urman-station-robocopy-{int(time.time())}.log'
        relocate(data, args.relocate, apply=True, log=log)
        rewrite_config(args.config, Path(args.relocate), apply=True)
        print(f'  the copies on the old volume stay at {data}; free them with --remove-old-copy --apply')
    if stopped and not args.keep_worker_stopped:
        task(config['repository'], 'start')
    elif stopped:
        print('worker       : left stopped')
    print('\nnext: python3.12 eng/remote-check.py doctor   (free space appears in "disk")')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
