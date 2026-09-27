#!/usr/bin/env python3
"""Run one local game check without changing the player's Godot userdata.

The original directory is kept outside the child's path and restored by rename,
including on a failing child or SIGINT/SIGTERM. An interrupted recovery is never
silently discarded. --clean gives smoke tests a separate, empty session; normal
invocations begin with a copy of the player's data. Backups stay outside git.
"""

from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import time


def fingerprint(root: Path) -> dict[str, tuple[int, str]]:
    result: dict[str, tuple[int, str]] = {}
    for path in [root, *sorted(root.rglob("*"))]:
        metadata = path.lstat()
        if path.is_symlink():
            value = "link:" + os.readlink(path)
        elif path.is_file():
            digest = hashlib.sha256()
            with path.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            value = digest.hexdigest()
        elif path.is_dir():
            value = "directory"
        else:
            raise RuntimeError(f"unsupported userdata entry: {path}")
        result[str(path.relative_to(root))] = (stat.S_IMODE(metadata.st_mode), value)
    return result


def default_userdata() -> Path:
    if sys.platform == "darwin":
        return Path.home() / "Library/Application Support/Godot/app_userdata/URMAN"
    if sys.platform.startswith("linux"):
        return Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "godot/app_userdata/URMAN"
    raise RuntimeError("protected_run currently supports the macOS/Linux development host")


def signal_group(pid: int, sig: int) -> None:
    """Signal the child's process group; a group that is already gone is fine.

    macOS answers EPERM rather than ESRCH for a group whose leader has exited,
    and an escaping error here used to skip the restore below entirely.
    """
    try:
        os.killpg(pid, sig)
    except (ProcessLookupError, PermissionError):
        pass


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--clean", action="store_true")
    parser.add_argument("--userdata", type=Path, help="explicit userdata path for an isolated guard test")
    parser.add_argument("--timeout", type=float, default=0.0,
                        help="stop the child after this many seconds and exit 124; userdata is still restored")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a child command is required")
    userdata = (args.userdata or default_userdata()).absolute()
    if userdata.is_symlink() or len(userdata.parts) < 4 or userdata == Path.home():
        raise RuntimeError("refusing an unsafe userdata path")
    userdata.parent.mkdir(parents=True, exist_ok=True)
    lock_path = userdata.parent / f".{userdata.name}.protected-run.lock"
    # Keep the lock inode: unlinking it permits a third process to bypass a waiter.
    with lock_path.open("a+", encoding="utf-8") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print("userdata guard: another protected check is active", file=sys.stderr)
            return 73
        lock.seek(0)
        unfinished = lock.read().strip()
        if unfinished:
            raise RuntimeError(f"userdata recovery is pending; inspect {lock_path} before another run")

        existed = userdata.exists()
        before = fingerprint(userdata) if existed else None
        backup = Path(tempfile.mkdtemp(prefix=f".{userdata.name}-protected-", dir=userdata.parent))
        original = backup / "original"
        lock.write(json.dumps({"pid": os.getpid(), "userdata": str(userdata), "backup": str(backup), "existed": existed}))
        lock.flush()
        os.fsync(lock.fileno())
        child: subprocess.Popen | None = None
        interrupted = 0
        interrupted_at = 0.0
        original_moved = False
        timed_out = False
        code = 1

        def interrupt(signum: int, _frame: object) -> None:
            nonlocal interrupted, interrupted_at
            if not interrupted:
                interrupted_at = time.monotonic()
            interrupted = signum
            if child is not None and child.poll() is None:
                signal_group(child.pid, signal.SIGTERM)

        previous = {sig: signal.signal(sig, interrupt) for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP)}
        try:
            if existed:
                userdata.rename(original)
                original_moved = True
            if args.clean or not existed:
                userdata.mkdir(mode=0o700)
            else:
                # Dereference links in the child copy so it cannot write through
                # a symlink into the protected original or another save folder.
                shutil.copytree(original, userdata, symlinks=False)
            if not interrupted:
                # Tell the child it runs inside the guard; URMAN Studio and its
                # "Play from here" refuse to start without this marker.
                child_env = dict(os.environ, URMAN_PROTECTED_RUN="1")
                child = subprocess.Popen(command, start_new_session=True, env=child_env)
                started = time.monotonic()
                while child.poll() is None:
                    # A smoke that throws inside async void can keep its window
                    # alive forever; a deadline turns that into a failure.
                    if args.timeout > 0 and not timed_out and time.monotonic() - started >= args.timeout:
                        timed_out = True
                        interrupted_at = time.monotonic()
                        print(f"userdata guard: child exceeded {args.timeout:g}s; stopping it", file=sys.stderr, flush=True)
                        signal_group(child.pid, signal.SIGTERM)
                    if (interrupted or timed_out) and time.monotonic() - interrupted_at >= 5:
                        signal_group(child.pid, signal.SIGKILL)
                    try:
                        child.wait(timeout=0.25)
                    except subprocess.TimeoutExpired:
                        pass
                code = child.returncode
        finally:
            if child is not None:
                signal_group(child.pid, signal.SIGTERM)
                try:
                    child.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    signal_group(child.pid, signal.SIGKILL)
                    child.wait(timeout=5)
            try:
                if original_moved or not existed:
                    if userdata.exists() or userdata.is_symlink():
                        userdata.rename(backup / "test-userdata")
                    if original_moved:
                        original.rename(userdata)
                after = fingerprint(userdata) if userdata.exists() else None
                if after != before:
                    raise RuntimeError(f"userdata verification failed; preserved recovery directory: {backup}")
                shutil.rmtree(backup)
                lock.seek(0)
                lock.truncate()
                lock.flush()
                os.fsync(lock.fileno())
                print("userdata guard: original files and permissions restored and verified", flush=True)
            finally:
                for sig, handler in previous.items():
                    signal.signal(sig, handler)
        if interrupted:
            return 128 + interrupted
        if timed_out:
            return 124
        return code if code >= 0 else 128 - code


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"userdata guard: {error}", file=sys.stderr)
        sys.exit(1)
