#!/usr/bin/env python3
"""Run one local game check without changing the player's Godot userdata.

The original directory is kept outside the child's path and restored by rename,
including on a failing child or SIGINT/SIGTERM. An interrupted recovery is never
silently discarded. --clean gives smoke tests a separate, empty session; normal
invocations begin with a copy of the player's data. Backups stay outside git.
"""

from __future__ import annotations

import argparse
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

if sys.platform == "win32":
    import ctypes
    from ctypes import wintypes
    import msvcrt

    _kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

    class _IoCounters(ctypes.Structure):
        _fields_ = [
            ("ReadOperationCount", ctypes.c_ulonglong),
            ("WriteOperationCount", ctypes.c_ulonglong),
            ("OtherOperationCount", ctypes.c_ulonglong),
            ("ReadTransferCount", ctypes.c_ulonglong),
            ("WriteTransferCount", ctypes.c_ulonglong),
            ("OtherTransferCount", ctypes.c_ulonglong),
        ]

    class _JobObjectBasicLimitInformation(ctypes.Structure):
        _fields_ = [
            ("PerProcessUserTimeLimit", ctypes.c_longlong),
            ("PerJobUserTimeLimit", ctypes.c_longlong),
            ("LimitFlags", wintypes.DWORD),
            ("MinimumWorkingSetSize", ctypes.c_size_t),
            ("MaximumWorkingSetSize", ctypes.c_size_t),
            ("ActiveProcessLimit", wintypes.DWORD),
            ("Affinity", ctypes.c_size_t),
            ("PriorityClass", wintypes.DWORD),
            ("SchedulingClass", wintypes.DWORD),
        ]

    class _JobObjectExtendedLimitInformation(ctypes.Structure):
        _fields_ = [
            ("BasicLimitInformation", _JobObjectBasicLimitInformation),
            ("IoInfo", _IoCounters),
            ("ProcessMemoryLimit", ctypes.c_size_t),
            ("JobMemoryLimit", ctypes.c_size_t),
            ("PeakProcessMemoryUsed", ctypes.c_size_t),
            ("PeakJobMemoryUsed", ctypes.c_size_t),
        ]

    class _JobObjectBasicAccountingInformation(ctypes.Structure):
        _fields_ = [
            ("TotalUserTime", ctypes.c_longlong),
            ("TotalKernelTime", ctypes.c_longlong),
            ("ThisPeriodTotalUserTime", ctypes.c_longlong),
            ("ThisPeriodTotalKernelTime", ctypes.c_longlong),
            ("TotalPageFaultCount", wintypes.DWORD),
            ("TotalProcesses", wintypes.DWORD),
            ("ActiveProcesses", wintypes.DWORD),
            ("TotalTerminatedProcesses", wintypes.DWORD),
        ]

    class _ThreadEntry32(ctypes.Structure):
        _fields_ = [
            ("dwSize", wintypes.DWORD),
            ("cntUsage", wintypes.DWORD),
            ("th32ThreadID", wintypes.DWORD),
            ("th32OwnerProcessID", wintypes.DWORD),
            ("tpBasePri", wintypes.LONG),
            ("tpDeltaPri", wintypes.LONG),
            ("dwFlags", wintypes.DWORD),
        ]

    # shortcut: x64 station only; verify other ABI layouts before supporting them.
    if (ctypes.sizeof(ctypes.c_void_p) != 8
            or ctypes.sizeof(_JobObjectExtendedLimitInformation) != 144
            or ctypes.sizeof(_JobObjectBasicAccountingInformation) != 48
            or ctypes.sizeof(_ThreadEntry32) != 28):
        raise RuntimeError("the Windows userdata guard requires verified 64-bit Python ABI layouts")

    _kernel32.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
    _kernel32.CreateJobObjectW.restype = wintypes.HANDLE
    _kernel32.SetInformationJobObject.argtypes = [
        wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD,
    ]
    _kernel32.SetInformationJobObject.restype = wintypes.BOOL
    _kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    _kernel32.OpenProcess.restype = wintypes.HANDLE
    _kernel32.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
    _kernel32.AssignProcessToJobObject.restype = wintypes.BOOL
    _kernel32.TerminateJobObject.argtypes = [wintypes.HANDLE, wintypes.UINT]
    _kernel32.TerminateJobObject.restype = wintypes.BOOL
    _kernel32.QueryInformationJobObject.argtypes = [
        wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p,
        wintypes.DWORD, ctypes.POINTER(wintypes.DWORD),
    ]
    _kernel32.QueryInformationJobObject.restype = wintypes.BOOL
    _kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    _kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    _kernel32.Thread32First.argtypes = [wintypes.HANDLE, ctypes.POINTER(_ThreadEntry32)]
    _kernel32.Thread32First.restype = wintypes.BOOL
    _kernel32.Thread32Next.argtypes = [wintypes.HANDLE, ctypes.POINTER(_ThreadEntry32)]
    _kernel32.Thread32Next.restype = wintypes.BOOL
    _kernel32.OpenThread.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    _kernel32.OpenThread.restype = wintypes.HANDLE
    _kernel32.ResumeThread.argtypes = [wintypes.HANDLE]
    _kernel32.ResumeThread.restype = wintypes.DWORD
    _kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    _kernel32.CloseHandle.restype = wintypes.BOOL
else:
    import fcntl


def _windows_error(operation: str) -> OSError:
    code = ctypes.get_last_error()
    return OSError(code, f"{operation}: {ctypes.FormatError(code).strip()}")


def _windows_close_handle(handle: int) -> None:
    if handle and not _kernel32.CloseHandle(handle):
        raise _windows_error("CloseHandle")


def _windows_create_kill_job() -> int:
    handle = _kernel32.CreateJobObjectW(None, None)
    if not handle:
        raise _windows_error("CreateJobObjectW")
    info = _JobObjectExtendedLimitInformation()
    info.BasicLimitInformation.LimitFlags = 0x00002000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
    try:
        if not _kernel32.SetInformationJobObject(
                handle, 9, ctypes.byref(info), ctypes.sizeof(info)):
            raise _windows_error("SetInformationJobObject")
    except BaseException:
        try:
            _windows_close_handle(handle)
        except OSError:
            pass
        raise
    return handle


def _windows_assign_process(job: int, pid: int) -> None:
    process = _kernel32.OpenProcess(0x0100 | 0x0001, False, pid)  # SET_QUOTA | TERMINATE
    if not process:
        raise _windows_error("OpenProcess")
    try:
        if not _kernel32.AssignProcessToJobObject(job, process):
            raise _windows_error("AssignProcessToJobObject")
    finally:
        _windows_close_handle(process)


def _windows_resume_primary_thread(pid: int) -> None:
    snapshot = _kernel32.CreateToolhelp32Snapshot(0x00000004, 0)  # TH32CS_SNAPTHREAD
    invalid_handle = ctypes.c_void_p(-1).value
    if not snapshot or snapshot == invalid_handle:
        raise _windows_error("CreateToolhelp32Snapshot")
    thread_id: int | None = None
    try:
        entry = _ThreadEntry32()
        entry.dwSize = ctypes.sizeof(entry)
        if not _kernel32.Thread32First(snapshot, ctypes.byref(entry)):
            raise _windows_error("Thread32First")
        while True:
            if entry.th32OwnerProcessID == pid:
                if thread_id is not None:
                    raise RuntimeError("suspended child has multiple threads; refusing to resume it")
                thread_id = int(entry.th32ThreadID)
            entry.dwSize = ctypes.sizeof(entry)
            ctypes.set_last_error(0)
            if not _kernel32.Thread32Next(snapshot, ctypes.byref(entry)):
                code = ctypes.get_last_error()
                if code not in (0, 18):  # ERROR_NO_MORE_FILES
                    raise _windows_error("Thread32Next")
                break
        if thread_id is None:
            raise RuntimeError("could not find the suspended child's primary thread")
    finally:
        _windows_close_handle(snapshot)

    thread = _kernel32.OpenThread(0x0002, False, thread_id)  # THREAD_SUSPEND_RESUME
    if not thread:
        raise _windows_error("OpenThread")
    try:
        previous_suspend_count = _kernel32.ResumeThread(thread)
        if previous_suspend_count == 0xFFFFFFFF:
            raise _windows_error("ResumeThread")
        if previous_suspend_count != 1:
            raise RuntimeError("suspended child's primary thread was not suspended exactly once")
    finally:
        _windows_close_handle(thread)


def _windows_terminate_job(job: int) -> None:
    if not _kernel32.TerminateJobObject(job, 1):
        raise _windows_error("TerminateJobObject")


def _windows_job_active_processes(job: int) -> int:
    info = _JobObjectBasicAccountingInformation()
    if not _kernel32.QueryInformationJobObject(
            job, 1, ctypes.byref(info), ctypes.sizeof(info), None):
        raise _windows_error("QueryInformationJobObject")
    return int(info.ActiveProcesses)


def _windows_cleanup_job(job: int, child: subprocess.Popen, assigned: bool) -> None:
    if child.poll() is None:
        if assigned:
            _windows_terminate_job(job)
        else:
            child.kill()  # A process not yet assigned is still suspended.
        try:
            child.wait(timeout=5)
        except subprocess.TimeoutExpired:
            if assigned:
                _windows_terminate_job(job)
            else:
                child.kill()
            child.wait(timeout=5)

    # Release Popen's retained HANDLE after confirmed exit, before job drain.
    process_handle = getattr(child, "_handle", None)
    close_process_handle = getattr(process_handle, "Close", None)
    if not callable(close_process_handle):
        raise RuntimeError("could not release the child process handle before job accounting")
    close_process_handle()

    active = _windows_job_active_processes(job)
    if active:
        _windows_terminate_job(job)
        deadline = time.monotonic() + 10.0
        while active:
            if time.monotonic() >= deadline:
                raise TimeoutError(f"Windows job still has {active} active process(es)")
            time.sleep(0.1)
            active = _windows_job_active_processes(job)


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


def rename_userdata(source: Path, destination: Path) -> None:
    """Windows may briefly retain a directory handle after the child exits."""
    deadline = time.monotonic() + 5.0
    while True:
        try:
            source.rename(destination)
            return
        except PermissionError:
            if sys.platform != "win32" or time.monotonic() >= deadline:
                raise
            time.sleep(0.1)


def default_userdata() -> Path:
    if sys.platform == "win32":
        if not os.environ.get("APPDATA"):
            raise RuntimeError("APPDATA is required on the Windows development host")
        return Path(os.environ["APPDATA"]) / "Godot/app_userdata/URMAN"
    if sys.platform == "darwin":
        return Path.home() / "Library/Application Support/Godot/app_userdata/URMAN"
    if sys.platform.startswith("linux"):
        return Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "godot/app_userdata/URMAN"
    raise RuntimeError("unsupported development host")


def signal_group(pid: int, sig: int) -> None:
    """Signal the child's process group; a group that is already gone is fine.

    macOS answers EPERM rather than ESRCH for a group whose leader has exited,
    and an escaping error here used to skip the restore below entirely.
    """
    if sys.platform == "win32":
        # The station invokes Godot directly. Kill its tree before restoring saves.
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
        return
    try:
        os.killpg(pid, sig)
    except (ProcessLookupError, PermissionError):
        pass


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--clean", action="store_true")
    parser.add_argument("--userdata", type=Path, help="explicit userdata path for an isolated guard test")
    parser.add_argument("--timeout", type=float, default=0.0,
                        help="stop the child after this many seconds and exit 124 after confirmed cleanup")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a child command is required")
    userdata = (args.userdata or default_userdata()).absolute()
    if (userdata.is_symlink() or getattr(userdata, "is_junction", lambda: False)()
            or len(userdata.parts) < 4 or userdata == Path.home()):
        raise RuntimeError("refusing an unsafe userdata path")
    userdata.parent.mkdir(parents=True, exist_ok=True)
    lock_path = userdata.parent / f".{userdata.name}.protected-run.lock"
    # Keep the lock inode: unlinking it permits a third process to bypass a waiter.
    with lock_path.open("a+", encoding="utf-8") as lock:
        # Windows locks byte zero; keep recovery metadata outside that byte.
        metadata_offset = 1 if sys.platform == "win32" else 0
        try:
            if sys.platform == "win32":
                if lock_path.stat().st_size == 0:
                    lock.write(" ")
                    lock.flush()
                lock.seek(0)
                try:
                    msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
                except OSError as error:
                    raise BlockingIOError("Windows userdata lock is unavailable") from error
            else:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            print("userdata guard: another protected check is active", file=sys.stderr)
            return 73
        lock.seek(metadata_offset)
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
        job_handle: int | None = None
        job_assigned = False
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
                if sys.platform == "win32":
                    try:
                        if job_assigned and job_handle is not None:
                            _windows_terminate_job(job_handle)
                        else:
                            child.kill()  # Still suspended until job assignment succeeds.
                    except OSError:
                        pass
                else:
                    signal_group(child.pid, signal.SIGTERM)

        signals = [signal.SIGINT, signal.SIGTERM]
        if hasattr(signal, "SIGHUP"):
            signals.append(signal.SIGHUP)
        force_signal = getattr(signal, "SIGKILL", signal.SIGTERM)
        previous = {sig: signal.signal(sig, interrupt) for sig in signals}
        try:
            if existed:
                rename_userdata(userdata, original)
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
                if sys.platform == "win32":
                    job_handle = _windows_create_kill_job()
                    # Keep the process suspended until it is a member of the
                    # kill-on-close job. A guard crash before assignment can
                    # leave only a suspended orphan; it cannot execute against
                    # userdata, which remains in the recorded recovery backup.
                    process_options = {
                        "creationflags": subprocess.CREATE_NEW_PROCESS_GROUP | 0x00000004,
                    }
                else:
                    process_options = {"start_new_session": True}
                child = subprocess.Popen(command, env=child_env, **process_options)
                if sys.platform == "win32":
                    _windows_assign_process(job_handle, child.pid)
                    job_assigned = True
                    if interrupted:
                        _windows_terminate_job(job_handle)
                    else:
                        _windows_resume_primary_thread(child.pid)
                started = time.monotonic()
                while child.poll() is None:
                    # A smoke that throws inside async void can keep its window
                    # alive forever; a deadline turns that into a failure.
                    if args.timeout > 0 and not timed_out and time.monotonic() - started >= args.timeout:
                        timed_out = True
                        interrupted_at = time.monotonic()
                        print(f"userdata guard: child exceeded {args.timeout:g}s; stopping it", file=sys.stderr, flush=True)
                        if sys.platform == "win32":
                            _windows_terminate_job(job_handle)
                        else:
                            signal_group(child.pid, signal.SIGTERM)
                    if ((interrupted or timed_out) and sys.platform != "win32"
                            and time.monotonic() - interrupted_at >= 5):
                        signal_group(child.pid, force_signal)
                    if (sys.platform == "win32" and (interrupted or timed_out)
                            and time.monotonic() - interrupted_at >= 10):
                        raise TimeoutError("Windows child did not exit after job termination")
                    try:
                        child.wait(timeout=0.25)
                    except subprocess.TimeoutExpired:
                        pass
                code = child.returncode
        finally:
            restore_allowed = True
            cleanup_error: BaseException | None = None
            signal_restore_error: BaseException | None = None
            try:
                if sys.platform == "win32":
                    if child is not None:
                        if job_handle is None:
                            raise RuntimeError("Windows child started without a cleanup job")
                        _windows_cleanup_job(job_handle, child, job_assigned)
                    if job_handle is not None:
                        _windows_close_handle(job_handle)
                        job_handle = None
                elif child is not None:
                    signal_group(child.pid, signal.SIGTERM)
                    try:
                        child.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        signal_group(child.pid, force_signal)
                        child.wait(timeout=5)
            except BaseException as error:
                restore_allowed = False
                cleanup_error = error
            finally:
                if job_handle is not None:
                    try:
                        _windows_close_handle(job_handle)
                    except BaseException as error:
                        restore_allowed = False
                        if cleanup_error is None:
                            cleanup_error = error
                    job_handle = None
                for sig, handler in previous.items():
                    try:
                        signal.signal(sig, handler)
                    except BaseException as error:
                        if signal_restore_error is None:
                            signal_restore_error = error
            if restore_allowed:
                try:
                    if original_moved or not existed:
                        if userdata.exists() or userdata.is_symlink():
                            rename_userdata(userdata, backup / "test-userdata")
                        if original_moved:
                            rename_userdata(original, userdata)
                    after = fingerprint(userdata) if userdata.exists() else None
                    if after != before:
                        raise RuntimeError(f"userdata verification failed; preserved recovery directory: {backup}")
                    shutil.rmtree(backup)
                    lock.seek(metadata_offset)
                    lock.truncate()
                    lock.flush()
                    os.fsync(lock.fileno())
                    if sys.platform == "win32":
                        print("userdata guard: original files restored by rename and verified (Windows ACLs not compared)", flush=True)
                    else:
                        print("userdata guard: original files and permissions restored and verified", flush=True)
                except BaseException:
                    # Keep the recovery marker and backup if restoration itself fails.
                    raise
            else:
                print(
                    f"userdata guard: process cleanup was not proven; "
                    f"left recovery data at {backup} and lock metadata at {lock_path}",
                    file=sys.stderr,
                    flush=True,
                )
                raise RuntimeError(
                    f"process cleanup failed; preserved recovery directory: {backup}"
                ) from cleanup_error
            if signal_restore_error is not None:
                raise RuntimeError("could not restore one or more signal handlers") from signal_restore_error
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
