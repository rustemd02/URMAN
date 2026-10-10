#!/usr/bin/env python3
"""VIS-004: compare two per-frame JSON sidecars without launching the engine.

This checks capture conditions only, never image quality or artistic acceptance.
Old metadata with missing fields is rejected; obtain a fresh pair when runs resume.
An intentional atmosphere change needs --allow-changed FIELD and --reason TEXT.
Camera, resolution, preset and engine fields cannot be waived.
"""
import argparse
import json
import math
import sys
from pathlib import Path

FIXED = (
    "metadataVersion", "point", "spec", "cameraName", "cameraPath",
    "cameraPosition", "cameraTarget", "cameraForward", "cameraSpace", "targetSpace",
    "cameraOwnerPath", "targetOwnerPath", "cameraOwnerZone", "targetOwnerZone",
    "fov", "fovSource", "near", "far", "viewportWidth", "viewportHeight",
    "scaling3DScale", "scaling3DMode", "graphicsPreset", "activeZoneId", "requestedZoneId",
    "engineVersion", "os", "studioPreviewProfile", "atmosphereOwner", "environmentPath",
)
ATMOSPHERE = (
    "atmosphereProfile", "tonemapMode", "fogEnabled", "volumetricFogEnabled",
    "fogColor", "fogDensity", "snowActive", "snowAmount", "blizzardActive",
    "diagnosticView", "diagnosticNeutralScope",
)
NULLABLE = {
    "cameraOwnerPath", "targetOwnerPath", "cameraOwnerZone", "targetOwnerZone",
    "requestedZoneId", "studioPreviewProfile", "diagnosticView", "diagnosticNeutralScope",
}
NUMERIC = {"metadataVersion", "fov", "near", "far", "viewportWidth", "viewportHeight", "scaling3DScale", "fogDensity", "snowAmount"}
VECTORS = {"cameraPosition", "cameraTarget", "cameraForward"}
FLAGS = {"fogEnabled", "volumetricFogEnabled", "snowActive", "blizzardActive"}
TEXT = set(FIXED + ATMOSPHERE) - NUMERIC - VECTORS - FLAGS


def same(a, b):
    if isinstance(a, bool) or isinstance(b, bool):
        return type(a) is type(b) and a == b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return math.isfinite(a) and math.isfinite(b) and math.isclose(a, b, rel_tol=0, abs_tol=1e-5)
    if type(a) is not type(b):
        return False
    if isinstance(a, dict):
        return a.keys() == b.keys() and all(same(a[key], b[key]) for key in a)
    return a == b


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("--allow-changed", action="append", choices=ATMOSPHERE, default=[])
    parser.add_argument("--reason", help="why the named atmosphere fields deliberately differ")
    args = parser.parse_args()
    if args.allow_changed and not (args.reason and args.reason.strip()):
        parser.error("--allow-changed requires --reason")
    before, after = [json.loads(p.read_text(encoding="utf-8")) for p in (args.before, args.after)]
    if not isinstance(before, dict) or not isinstance(after, dict):
        raise ValueError("expected per-frame JSON objects")
    failures, waived = [], []
    for field in FIXED + ATMOSPHERE:
        if field not in before or field not in after:
            failures.append(f"{field}: missing metadata")
            continue
        a, b = before[field], after[field]
        if (a is None or b is None) and field not in NULLABLE:
            failures.append(f"{field}: unknown capture condition")
            continue
        if field in NUMERIC and any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) or v < 0 for v in (a, b)):
            failures.append(f"{field}: invalid numeric capture condition")
            continue
        if field in FLAGS and any(not isinstance(v, bool) for v in (a, b)):
            failures.append(f"{field}: expected a boolean flag")
            continue
        if field in TEXT and any(v is not None and (not isinstance(v, str) or not v.strip()) for v in (a, b)):
            failures.append(f"{field}: expected a non-empty text value or an allowed null")
            continue
        if field in VECTORS:
            if any(not isinstance(v, dict) or set(v) != {"x", "y", "z"}
                   or any(isinstance(n, bool) or not isinstance(n, (int, float)) or not math.isfinite(n) for n in v.values()) for v in (a, b)):
                failures.append(f"{field}: invalid world vector")
                continue
        if not same(a, b):
            detail = f"{field}: {a!r} -> {b!r}"
            (waived if field in args.allow_changed else failures).append(detail)
    if before.get("metadataVersion") != 2 or after.get("metadataVersion") != 2:
        failures.append("metadataVersion: requires version 2 on both sides")
    for label, frame in (("before", before), ("after", after)):
        # Equality alone does not make an impossible camera setup evidence.
        values = {field: frame.get(field) for field in NUMERIC}
        if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in values.values()):
            continue  # The field-level failures above already explain invalid types.
        if not (0 < values["near"] < values["far"] and 0 < values["fov"] < 180 and values["scaling3DScale"] > 0):
            failures.append(f"{label}: invalid camera clipping, FOV or rendering scale")
        if any(values[field] <= 0 or not float(values[field]).is_integer() for field in ("viewportWidth", "viewportHeight")):
            failures.append(f"{label}: viewport dimensions must be positive integers")
    for detail in waived:
        print(f"INTENTIONAL {detail}; reason: {args.reason}")
    for detail in failures:
        print(f"FAIL {detail}")
    if failures:
        return 1
    print("PASS capture conditions comparable; image quality, NPC poses, lighting readback and snapshot receipts still require review")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, TypeError) as exc:
        print(f"FAIL input: {exc}", file=sys.stderr)
        sys.exit(1)
