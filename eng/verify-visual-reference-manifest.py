#!/usr/bin/env python3
"""Static gate for docs/production/visual_restyle_2026-10-07/reference_manifest.json (VIS-066).

Checks: all fifteen author-selected codes are present; every entry has use/do_not_use;
negative references are marked anti_example; an entry without a stored image says so
and carries a source link or an explicit no_image marker (never a lookalike substitute).
Touches no engine, no network.
"""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PATH = ROOT / "docs/production/visual_restyle_2026-10-07/reference_manifest.json"
SELECTED = ["P1", "W1", "W2", "V1", "V2", "V4", "I1", "N1", "N2",
            "H2-1", "H2-2", "H2-3", "H3-1", "H4-2", "H4-3"]
AVAILABILITY = {"missing_locally_in_repo", "link_only", "no_image", "in_repo", "local_checkout_only"}

errors = []
m = json.loads(PATH.read_text(encoding="utf-8"))
refs = {r["id"]: r for r in m["references"]}
if len(refs) != len(m["references"]):
    errors.append("duplicate reference ids")
for code in SELECTED:
    if code not in refs:
        errors.append(f"selected code missing: {code}")
    elif not refs[code]["author_selected_code"]:
        errors.append(f"{code} must be flagged author_selected_code")
for rid, r in refs.items():
    if not r.get("use") or not r.get("do_not_use"):
        errors.append(f"{rid}: use/do_not_use required")
    if "negative" in r["status"] and r["role"] != "anti_example":
        errors.append(f"{rid}: negative status must have role anti_example")
    if r["role"] == "anti_example" and r["author_selected_code"]:
        errors.append(f"{rid}: an anti-example cannot be an author-selected target")
    if r["local_availability"] not in AVAILABILITY:
        errors.append(f"{rid}: bad local_availability {r['local_availability']!r}")
    if r["local_availability"] == "link_only" and not r.get("source"):
        errors.append(f"{rid}: link_only requires a source link")
    if r["local_availability"] in {"in_repo", "local_checkout_only"}:
        f = ROOT / (r.get("local_file") or "")
        if not f.is_file():
            errors.append(f"{rid}: declared local file not found")
        elif not r.get("sha256") or hashlib.sha256(f.read_bytes()).hexdigest() != r["sha256"]:
            errors.append(f"{rid}: exact local image SHA256 missing or mismatched")
if errors:
    print("FAIL")
    for e in errors:
        print(" -", e)
    sys.exit(1)
print(f"PASS: {len(refs)} references, {len(SELECTED)} author-selected codes present")
