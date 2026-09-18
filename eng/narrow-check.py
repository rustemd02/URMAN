#!/usr/bin/env python3
"""Run one narrow native check under the userdata guard and write its receipt.

The receipt records the run's exact command, the build it belongs to, the
untouched-input proof and every engine error the run reported, so a later
reader can tell a pass from a silent diagnostic.

Usage:
    python3 eng/narrow-check.py --name vehicle-14 --scene res://tests/vehicle_smoke_test.tscn
    python3 eng/narrow-check.py --name address-standalone-access-08 \
        --scene res://tests/address_world_smoke_test.tscn --env URMAN_ADDRESS_SCOPE=standalone-access \
        --evidence-dir images-address-standalone-access-08
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / "docs/production/act1_takeover_evidence_2026-09-16"
GODOT = ROOT / ".tools/godot/Godot_mono.app/Contents/MacOS/Godot"
DOTNET_ENVIRONMENT = {
    "DOTNET_ROOT": str(ROOT / ".tools/dotnet"),
    "DOTNET_ROOT_ARM64": str(ROOT / ".tools/dotnet"),
    "DOTNET_CLI_HOME": str(ROOT / ".tools/dotnet-home"),
    "NUGET_PACKAGES": str(ROOT / ".tools/nuget"),
    "MSBUILDUSESERVER": "0",
}
ERROR_PATTERN = re.compile(r"^ERROR: (?P<message>.*)$", re.MULTILINE)
# A harness assertion prints its own label after "ERROR: <scene>: ". Engine
# errors without that prefix are frequently deliberate: the vehicle suite, for
# example, blocks every parking candidate on purpose and the save owner is
# required to refuse and roll that projection back. Only these markers mean the
# check itself did not complete.
FATAL_MARKERS = ("Act I startup failed", "did not finish loading", "Failed to load the scene")


def latest_build() -> tuple[str, dict[str, str]]:
    receipts = sorted(EVIDENCE.glob("integrated-build-*.json"))
    receipts = [path for path in receipts if "sha256" not in path.name]
    if not receipts:
        raise SystemExit("no integrated build receipt found; run eng/integrated-build.py first")
    latest = max(receipts, key=lambda path: path.stat().st_mtime)
    manifest = EVIDENCE / f"{latest.stem}-input-sha256.json"
    inputs = json.loads(manifest.read_text()) if manifest.exists() else {}
    return latest.name, inputs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", required=True, help="receipt name, e.g. vehicle-14")
    parser.add_argument("--scene", required=True, help="res:// scene to run")
    parser.add_argument("--resolution", default="1920x1080")
    parser.add_argument("--env", action="append", default=[], help="extra KEY=VALUE for the child")
    parser.add_argument("--evidence-dir", help="empty directory the scene may fill; created if missing")
    parser.add_argument("--note", default="", help="one line recorded with the receipt")
    parser.add_argument("--allow-exit", type=int, default=None, help="expected exit code when not 0")
    args = parser.parse_args()

    build_name, inputs = latest_build()
    command = [sys.executable, "eng/protected_run.py", "--clean", str(GODOT), "--path", "game",
               "--resolution", args.resolution, args.scene, "--urman-smoke-background-input",
               "--no-auto-performance-fallback"]
    environment = {**os.environ, **DOTNET_ENVIRONMENT}
    for entry in args.env:
        key, _, value = entry.partition("=")
        environment[key] = value
    if args.evidence_dir:
        directory = EVIDENCE / args.evidence_dir
        if directory.exists() and any(directory.iterdir()):
            raise SystemExit(f"{directory} is not empty; the scene requires a fresh evidence directory")
        directory.mkdir(parents=True, exist_ok=True)
        environment.setdefault("URMAN_ADDRESS_OUTPUT", str(directory))

    started = time.time()
    stamp = time.strftime("%Y-%m-%dT%H:%M:%S+00:00", time.gmtime())
    process = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, env=environment)
    finished = time.strftime("%Y-%m-%dT%H:%M:%S+00:00", time.gmtime())
    log = (process.stdout or "") + (process.stderr or "")
    errors = Counter(match.group("message").strip() for match in ERROR_PATTERN.finditer(log))

    changed = [path for path, digest in inputs.items()
               if (ROOT / path).exists() and hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest]
    guarded = "userdata guard: original files and permissions restored and verified" in log
    expected = args.allow_exit if args.allow_exit is not None else 0
    assertions = [message for message in errors if re.match(r"^[a-z0-9_-]+: ", message)]
    fatal = [message for message in errors if any(marker in message for marker in FATAL_MARKERS)]
    reason = ("exit code differs" if process.returncode != expected
              else "harness assertion failed: " + assertions[0][:160] if assertions
              else "fatal engine error: " + fatal[0][:160] if fatal
              else "userdata guard did not confirm restoration" if not guarded
              else "deliberate engine-side refusals only" if errors
              else "clean")
    status = "PASS" if process.returncode == expected and guarded and not assertions and not fatal else "FAIL"
    receipt = {
        "timestamp": stamp,
        "build_receipt": build_name,
        "args": command,
        "environment": {k: v for k, v in environment.items() if k in DOTNET_ENVIRONMENT or k.startswith("URMAN_")},
        "human_playtest": "external/not-run",
        "focus_policy": "Test-only background input replay; no foreground performance or human duration claim.",
        "note": args.note,
        "exit_code": process.returncode,
        "expected_exit_code": expected,
        "process_seconds": round(time.time() - started, 2),
        "finished": finished,
        "inputs_changed_during_run": changed,
        "guard_verified": guarded,
        "engine_error_count": sum(errors.values()),
        "harness_assertion_errors": assertions,
        "fatal_engine_errors": fatal,
        "status_rule": "FAIL on a wrong exit code, a harness assertion error, a fatal engine marker or a "
                       "failed userdata guard; engine-side refusals alone are recorded, not decisive",
        "engine_errors": [{"message": message, "count": count} for message, count in errors.most_common()],
        "log": f"{args.name}.log",
        "status": status,
    }
    if any((EVIDENCE / f"{args.name}{suffix}").exists() for suffix in (".json", ".log")):
        raise SystemExit(f"refusing to overwrite the existing {args.name} receipt")
    (EVIDENCE / f"{args.name}.log").write_text(log)
    (EVIDENCE / f"{args.name}.json").write_text(json.dumps(receipt, indent=2, ensure_ascii=False))
    print(f"{args.name}: {status} ({reason}); exit={process.returncode}; "
          f"engine errors={sum(errors.values())}; {round(time.time() - started, 1)}s")
    for message, count in errors.most_common(5):
        print(f"   x{count} {message[:160]}")
    return 0 if status == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
