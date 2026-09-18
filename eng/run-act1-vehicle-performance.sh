#!/bin/sh
# Three moving samples in an identified Release. No export, import or build here.
set -eu
URMAN_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
exec python3 - "$URMAN_ROOT" "$@" <<'PY'
import argparse
import fcntl
import json
import os
from pathlib import Path
import runpy
import signal
import subprocess
import sys
import time

root = Path(sys.argv[1]).resolve()
samples = (
    "vehicle_niva_radio@main_road",
    "vehicle_motorcycle@main_road",
    "vehicle_horse_cart@zirat_road",
)
parser = argparse.ArgumentParser(description="Run identified Release vehicle gameplay: 12s warmup + 60s measurement, ordinary controls and safe exit.")
parser.add_argument("--candidate", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True, help="New evidence directory; existing directories are refused.")
parser.add_argument("--sample", action="append", choices=samples, help="Omit to run all three sequentially.")
args = parser.parse_args(sys.argv[2:])
candidate = args.candidate.resolve()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)
selected = list(dict.fromkeys(args.sample or samples))
results = []
receipt = {
    "schema": "urman.release_vehicle_performance.v1",
    "candidate": str(candidate), "status": "INVALID", "samples": results,
    "criteria": {"warmupSeconds": 12, "measurementSeconds": 60, "averageFpsMinimum": 58,
                 "p95MillisecondsMaximum": 18, "p99MillisecondsMaximum": 25,
                 "longFrameFractionMaximum": .005, "maximumMilliseconds": 100,
                 "focusedFractionMinimum": .95, "obscuredFramesMaximum": 0,
                 "window": "1920x1080"},
    "limits": ["Explicit diagnostic driver; not a human playthrough.",
               "Native radio rendering is not listening or language acceptance."],
}
child = None
interrupted = False
guard_log = None
guard_pending = False
recovery_attempted = False
guard_contract = runpy.run_path(str(root / "eng/protected_run.py"), run_name="urman_guard_contract")
userdata = guard_contract["default_userdata"]().absolute()
guard_lock = userdata.parent / f".{userdata.name}.protected-run.lock"

def terminate_group(process):
    # The shell can already have exited while its guard is still restoring.
    if process is not None:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass

def guard_has_restored(log_path):
    marker = "userdata guard: original files and permissions restored and verified"
    if log_path is None or marker not in log_path.read_text(encoding="utf-8"):
        return False
    # Inspect the existing guard's lock; never create, truncate or repair it.
    try:
        with guard_lock.open("r", encoding="utf-8") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            try:
                return not lock.read().strip()
            finally:
                fcntl.flock(lock, fcntl.LOCK_UN)
    except (FileNotFoundError, BlockingIOError):
        return False

def await_guard_restoration(timeout=20):
    global child, guard_pending, recovery_attempted
    recovery_attempted = True
    deadline = time.monotonic() + timeout
    while True:
        shell_finished = child is None or child.poll() is not None
        if shell_finished and guard_has_restored(guard_log):
            guard_pending = False
            child = None
            return True
        if time.monotonic() >= deadline:
            return False
        time.sleep(.05)

def on_signal(signum, _frame):
    global interrupted
    interrupted = True
    terminate_group(child)

for signum in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
    signal.signal(signum, on_signal)

def identity(label):
    path = output / (label + ".json")
    with (output / (label + ".log")).open("x", encoding="utf-8") as log:
        result = subprocess.run(
            [sys.executable, str(root / "eng/act1_candidate_identity.py"),
             "--root", str(root), "--candidate", str(candidate), "--output", str(path)],
            stdout=log, stderr=subprocess.STDOUT, check=False)
    if result.returncode:
        raise RuntimeError(f"{label}: strict candidate identity failed ({result.returncode})")
    value = json.loads(path.read_text(encoding="utf-8"))
    if (value["comparison"]["status"] != "matches-recorded-inputs"
            or value["candidate"]["buildProvenance"]["producer"]["mode"] != "release"):
        raise RuntimeError(f"{label}: current completed Release provenance is required")
    return value

exit_code = 2
try:
    before = identity("candidate-before")
    binary = candidate / "launch/URMAN.app/Contents/MacOS/URMAN"
    if not os.access(binary, os.X_OK):
        raise RuntimeError("Identified candidate binary is not executable")
    # The established package runner owns flags and invokes its guard. This
    # adapter requests a fresh isolated profile without nesting two guards.
    guard = output / "guard-clean.py"
    guard.write_text("import os, sys\nos.execv(sys.executable, [sys.executable, "
                     + repr(str(root / "eng/protected_run.py")) + ", '--clean', *sys.argv[1:]])\n",
                     encoding="utf-8")
    guard.chmod(0o700)
    environment = os.environ.copy()
    environment.update(URMAN_ACT1_PACKAGE_BINARY=str(binary),
                       URMAN_ACT1_PACKAGE_ZIP="", URMAN_ACT1_HEADLESS="0",
                       URMAN_GUARD=str(guard))
    for index, sample in enumerate(selected, 1):
        if interrupted:
            raise RuntimeError("Interrupted before the next sample")
        label = f"{index:02d}-" + sample.replace("@", "-")
        print(f"vehicle-performance: starting {sample}; log={output / (label + '.log')}", flush=True)
        command = ["sh", str(root / "eng/benchmark-act1-demo-package.sh"),
                   "--resolution", "1920x1080",
                   "--urman-perf-probe-mode=gameplay", "--urman-perf-sample=" + sample,
                   "--urman-perf-warmup-seconds=12", "--urman-perf-duration-seconds=60",
                   "--urman-perf-request-focus", "--urman-perf-no-vsync"]
        started = time.monotonic()
        log_path = output / (label + ".log")
        with log_path.open("x", encoding="utf-8") as log:
            child = subprocess.Popen(command, cwd=root, env=environment, stdout=log,
                                     stderr=subprocess.STDOUT, start_new_session=True)
            guard_log, guard_pending, recovery_attempted = log_path, True, False
            try:
                code = child.wait(timeout=240)
            except subprocess.TimeoutExpired:
                terminate_group(child)
                restored = await_guard_restoration()
                raise RuntimeError(f"{sample}: bounded run timed out; guard restored={restored}")
            if not await_guard_restoration():
                raise RuntimeError(f"{sample}: guard restoration is unconfirmed; no subsequent run")
        text = log_path.read_text(encoding="utf-8")
        metrics = [line for line in text.splitlines() if line.startswith("act1-demo-package-performance: ")]
        route_lines = [line for line in text.splitlines() if line.startswith("act1-vehicle-performance-route: ")]
        route = json.loads(route_lines[-1].split(": ", 1)[1]) if len(route_lines) == 1 else None
        tokens = dict(token.split("=", 1) for token in metrics[-1].split()[1:] if "=" in token) if len(metrics) == 1 else {}
        window_valid = tokens.get("window") == "1920x1080"
        guard_restored = not guard_pending
        after = identity(label + "-identity-after")
        unchanged = before["candidate"] == after["candidate"]
        passed = (not interrupted and code == 0 and guard_restored and unchanged and window_valid
                  and tokens.get("status") == "PASS" and tokens.get("sample") == sample
                  and route is not None and route.get("status") == "PASS"
                  and route.get("safeExit") is True and route.get("poseWrites") == 0)
        results.append({"sample": sample, "exitCode": code, "seconds": time.monotonic() - started,
                        "log": log_path.name, "status": "PASS" if passed else "FAIL",
                        "guardRestored": guard_restored, "candidateUnchanged": unchanged,
                        "windowValid": window_valid,
                        "windowFailure": None if window_valid else
                            f"Expected actual window 1920x1080; reported {tokens.get('window', 'missing')}.",
                        "metrics": tokens, "route": route})
        print(f"vehicle-performance: {sample} {results[-1]['status']}", flush=True)
        if not guard_restored or not unchanged:
            raise RuntimeError(f"{sample}: guard restoration or candidate stability is unconfirmed; no subsequent run")
    receipt["status"] = "PASS" if not interrupted and all(item["status"] == "PASS" for item in results) else "FAIL"
    exit_code = 0 if receipt["status"] == "PASS" else 1
except Exception as error:
    receipt["failure"] = str(error)
    print("vehicle-performance: " + str(error), file=sys.stderr, flush=True)
finally:
    try:
        if guard_pending:
            terminate_group(child)
            if not recovery_attempted:
                await_guard_restoration()
            if guard_pending:
                receipt["guardRecovery"] = "not confirmed; inspect retained guard lock before another run"
                receipt["status"], exit_code = "INVALID", 2
    except Exception as error:
        receipt["guardRecovery"] = "not confirmed: " + str(error)
        receipt["status"], exit_code = "INVALID", 2
    if interrupted:
        receipt["interrupted"] = True
        receipt["status"], exit_code = "INVALID", 2
    with (output / "receipt.json").open("x", encoding="utf-8") as stream:
        json.dump(receipt, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
sys.exit(exit_code)
PY
