#!/usr/bin/env python3
"""Build one integrated working-tree revision and write an immutable receipt.

Runs the three steps the project uses for every checked revision:

1. ``eng/compile-game-content.sh`` — compile authored content to game/content.
2. ``dotnet build game/Urman.Game.csproj`` with the repository toolchain.
3. guarded Godot headless import of ``game/``.

Before and after every step the script hashes the source and input manifests so
a receipt can prove whether anything changed while the build was running. The
file sets are defined by the rules in MANIFESTS below and are recorded in the
receipt itself, so two receipts can be compared entry by entry.

Usage:
    python3 eng/integrated-build.py --name integrated-build-55
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / "docs/production/act1_takeover_evidence_2026-09-16"
DOTNET = ROOT / ".tools/dotnet/dotnet"
GODOT = ROOT / ".tools/godot/Godot_mono.app/Contents/MacOS/Godot"

SOURCE_SUFFIXES = {".cs", ".json", ".tscn", ".gd", ".py", ".sh", ".godot", ".csproj", ".props", ".md", ".svg"}
INPUT_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp", ".wav", ".ogg", ".glb", ".blend", ".tscn", ".json", ".csproj", ".godot"}
SKIP_PARTS = {"bin", "obj", "__pycache__", ".godot", "node_modules", ".git"}

SOURCE_ROOTS = ["game/scripts", "game/tests", "game/scenes", "game/project.godot", "game/Urman.Game.csproj",
                "src-dotnet", "tests-dotnet", "tools-dotnet", "tools/blender", "content", "assets/source", "eng"]
INPUT_ROOTS = ["game/assets", "game/tests", "game/scenes", "game/content", "content/modules", "assets/asset_registry.json",
               "game/Urman.Game.csproj", "game/project.godot"]


def manifest(roots: list[str], suffixes: set[str]) -> dict[str, str]:
    result: dict[str, str] = {}
    for root in roots:
        path = ROOT / root
        if path.is_file():
            candidates = [path]
        elif path.is_dir():
            candidates = sorted(p for p in path.rglob("*") if p.is_file())
        else:
            continue
        for candidate in candidates:
            if SKIP_PARTS & set(candidate.relative_to(ROOT).parts) or candidate.suffix.lower() not in suffixes:
                continue
            result[str(candidate.relative_to(ROOT))] = hashlib.sha256(candidate.read_bytes()).hexdigest()
    return result


def artifact_manifest() -> dict[str, str]:
    result: dict[str, str] = {}
    for root in ["game/.godot/imported", "game/.godot/mono/temp/bin/Debug", "game/assets/generated", "game/content"]:
        path = ROOT / root
        if not path.is_dir():
            continue
        for candidate in sorted(p for p in path.rglob("*") if p.is_file()):
            if candidate.name.endswith((".pdb", ".deps.json")) or candidate.suffix in {".log", ".tmp"}:
                continue
            result[str(candidate.relative_to(ROOT))] = hashlib.sha256(candidate.read_bytes()).hexdigest()
    return result


def git_revision() -> tuple[str, str]:
    branch = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    return branch, head


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", required=True, help="receipt name, e.g. integrated-build-55")
    parser.add_argument("--skip-content", action="store_true", help="skip eng/compile-game-content.sh")
    parser.add_argument("--skip-dotnet", action="store_true", help="skip the C# build")
    parser.add_argument("--skip-import", action="store_true", help="skip the Godot import")
    args = parser.parse_args()

    environment = {key: os.environ.get(key, "") for key in
                   ("DOTNET_ROOT", "DOTNET_ROOT_ARM64", "DOTNET_CLI_HOME", "NUGET_PACKAGES", "MSBUILDUSESERVER", "PATH")}
    branch, head = git_revision()
    receipt: dict[str, object] = {
        "build": args.name,
        "environment": environment,
        "started": time.strftime("%Y-%m-%dT%H:%M:%S+00:00", time.gmtime()),
        "branch": branch,
        "head": head,
        "manifests": {"rules": {"source_roots": SOURCE_ROOTS, "source_suffixes": sorted(SOURCE_SUFFIXES),
                                "input_roots": INPUT_ROOTS, "input_suffixes": sorted(INPUT_SUFFIXES),
                                "skipped_parts": sorted(SKIP_PARTS)}},
    }
    source_before = manifest(SOURCE_ROOTS, SOURCE_SUFFIXES)
    input_before = manifest(INPUT_ROOTS, INPUT_SUFFIXES)
    log_lines: list[str] = []

    steps = []
    if not args.skip_content:
        steps.append(("compile-content", ["sh", "eng/compile-game-content.sh"]))
    if not args.skip_dotnet:
        steps.append(("csharp", [str(DOTNET), "build", "game/Urman.Game.csproj", "--no-restore",
                                 "--disable-build-servers", "--verbosity", "minimal", "-nodeReuse:false", "-m:1"]))
    if not args.skip_import:
        steps.append(("godot-import", [sys.executable, "eng/protected_run.py", "--clean", str(GODOT),
                                       "--headless", "--path", "game", "--import"]))

    failed = False
    for name, command in steps:
        started = time.time()
        process = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        seconds = round(time.time() - started, 2)
        log_lines.append(f"\n{name}\n{process.stdout}{process.stderr}")
        receipt.setdefault("steps", []).append({"name": name, "exit_code": process.returncode, "seconds": seconds,
                                                "command": command})
        print(f"{name}: exit {process.returncode} in {seconds}s", flush=True)
        if process.returncode != 0:
            failed = True
            break

    source_after = manifest(SOURCE_ROOTS, SOURCE_SUFFIXES)
    input_after = manifest(INPUT_ROOTS, INPUT_SUFFIXES)
    receipt["source_changed_during_build"] = sorted(k for k in set(source_before) | set(source_after)
                                                    if source_before.get(k) != source_after.get(k))
    receipt["input_changed_during_build"] = sorted(k for k in set(input_before) | set(input_after)
                                                   if input_before.get(k) != input_after.get(k))
    receipt["artifacts"] = artifact_manifest()
    error_lines = [line for line in "\n".join(log_lines).splitlines()
                   if ": error " in line or line.startswith("ERROR") or "Build FAILED" in line]
    receipt["reported_error_lines"] = sorted({line.strip() for line in error_lines if line.strip()})
    receipt["finished"] = time.strftime("%Y-%m-%dT%H:%M:%S+00:00", time.gmtime())
    receipt["status"] = "FAIL" if failed else "PASS"
    receipt["scope"] = "working-tree integration; not a published Release or gameplay acceptance"
    receipt["runtime_identity_scope"] = ("All game assets, runtime content/scenes/test scenes, managed outputs and "
                                         "Godot imported resources; asset registry and Blender generators additionally captured.")

    EVIDENCE.mkdir(parents=True, exist_ok=True)
    receipt_path = EVIDENCE / f"{args.name}.json"
    if receipt_path.exists():
        print(f"refusing to overwrite the existing receipt {receipt_path}", file=sys.stderr)
        return 3
    receipt_path.write_text(json.dumps(receipt, indent=2, ensure_ascii=False))
    for suffix, data in (("source", source_after), ("input", input_after)):
        path = EVIDENCE / f"{args.name}-{suffix}-sha256.json"
        if path.exists():
            print(f"refusing to overwrite the existing manifest {path}", file=sys.stderr)
            return 3
        path.write_text(json.dumps(data, indent=1, sort_keys=True))
    (EVIDENCE / f"{args.name}.log").write_text("\n".join(log_lines))
    print(f"{args.name}: {receipt['status']}; {len(receipt['artifacts'])} artifacts hashed")
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
