#!/usr/bin/env python3
"""Capture stable inputs around a real desktop exporter run and bind its outputs."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import uuid

from act1_candidate_identity import (ARTIFACT_PATHS, EXPORT_PRESETS, PROVENANCE_SCHEMA, sha256_file,
                                    source_manifest, validate_build_provenance,
                                    validate_source_manifest)


def utc_now():
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def capture_start(root, mode):
    return {"schema": "urman.desktop_export_start.v1", "runId": str(uuid.uuid4()),
            "startedAtUtc": utc_now(),
            "producer": {"script": f"eng/export-desktop-{mode}.sh", "mode": mode,
                         "presets": EXPORT_PRESETS[mode]},
            "sourceBefore": source_manifest(root)}


def complete_export(root, start, artifacts):
    if start.get("schema") != "urman.desktop_export_start.v1":
        raise ValueError("export start record has an unsupported schema")
    before = validate_source_manifest(start.get("sourceBefore"))
    after = source_manifest(root)
    if before["files"] != after["files"]:
        changed = sorted(name for name in set(before["files"]) | set(after["files"])
                         if before["files"].get(name) != after["files"].get(name))
        raise ValueError("inputs changed during export; stabilize compile/import before retrying: " + ", ".join(changed))
    result = {"schema": PROVENANCE_SCHEMA, "status": "completed", "runId": start["runId"],
              "startedAtUtc": start["startedAtUtc"], "completedAtUtc": utc_now(),
              "producer": start["producer"], "sourceBefore": before, "sourceAfter": after,
              "artifacts": artifacts,
              "limits": ["Local exporter evidence, not independent compiler attestation.",
                         "Requires coordinated source freeze; transient edit/revert is not detectable."]}
    validate_build_provenance(result, artifacts)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    subcommands = parser.add_subparsers(dest="action", required=True)
    start_parser = subcommands.add_parser("start")
    start_parser.add_argument("--mode", choices=("debug", "release"), required=True)
    start_parser.add_argument("--output", type=Path, required=True)
    finish_parser = subcommands.add_parser("complete")
    finish_parser.add_argument("--start", type=Path, required=True)
    finish_parser.add_argument("--mac-zip", type=Path, required=True)
    finish_parser.add_argument("--windows-zip", type=Path, required=True)
    finish_parser.add_argument("--windows-exe", type=Path, required=True)
    finish_parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.action == "start":
            result = capture_start(args.root, args.mode)
        else:
            start = json.loads(args.start.read_text(encoding="utf-8"))
            paths = {("macOS", "zip"): args.mac_zip, ("Windows", "zip"): args.windows_zip,
                     ("Windows", "executable"): args.windows_exe}
            artifacts = [{"platform": platform, "kind": kind, "sizeBytes": paths[(platform, kind)].stat().st_size,
                          "sha256": sha256_file(paths[(platform, kind)])} for platform, kind in ARTIFACT_PATHS]
            result = complete_export(args.root, start, artifacts)
        # Every invocation uses a fresh task-owned path. A failed or repeated
        # exporter cannot replace evidence from an earlier successful run.
        with args.output.open("x", encoding="utf-8") as output:
            json.dump(result, output, ensure_ascii=False, indent=2)
            output.write("\n")
        print(f"desktop-export: provenance {args.action} run={result['runId']} path={args.output}")
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"desktop-export: provenance failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
