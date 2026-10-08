#!/usr/bin/env python3
"""Mechanical static audit for the visual-reset pass (no engine, no Blender, no station).

Each CHECK is a yes/no inventory with a list of offending paths or ids. The script
reports; it never edits. Run: python3 tools/visual_audit/static_audit.py [--out report.md]
"""
import argparse
import collections
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SKIP_DIRS = {".git", "outputs", ".tools", ".godot", "obj", "bin", "node_modules", ".codex-captures"}
TEXT_SUFFIX = {".cs", ".py", ".md", ".json", ".sh", ".ps1", ".txt", ".tscn", ".gd", ".cfg", ".import"}


def files(suffixes, under=None):
    base = ROOT / under if under else ROOT
    for path in base.rglob("*"):
        if any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        if path.is_file() and path.suffix in suffixes:
            yield path


def rel(path):
    return path.relative_to(ROOT).as_posix()


STATUS_FILE = ROOT / "docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07_status.json"
TASK_FILE = ROOT / "docs/URMAN_VISUAL_RESET_2026-10-07/TASKS_AND_REQUIREMENTS.json"


def load_status():
    return json.loads(STATUS_FILE.read_text(encoding="utf-8"))


results = []


def check(name, problems, note=""):
    results.append((name, problems, note))


# 1 JSON validity of visual-reset artefacts
json_paths = [p for p in files({".json"}) if any(s in rel(p) for s in (
    "visual_restyle_2026-10-07", "URMAN_VISUAL_RESET", "art/visual_reset_", "content/world/", "asset_registry", "checkpoints"))]
bad = []
for p in json_paths:
    try:
        json.loads(p.read_text(encoding="utf-8"))
    except Exception as error:
        bad.append(f"{rel(p)}: {error}")
check("1 JSON parses (visual artefacts)", bad, f"{len(json_paths)} files")

# 2 Python compiles (tools and eng)
bad = []
py = list(files({".py"}, "tools")) + list(files({".py"}, "eng"))
for p in py:
    r = subprocess.run([sys.executable, "-m", "py_compile", str(p)], capture_output=True, text=True)
    if r.returncode:
        bad.append(f"{rel(p)}: {r.stderr.strip().splitlines()[-1]}")
check("2 Python compiles (tools, eng)", bad, f"{len(py)} files")

# 3 Unresolved merge-conflict markers anywhere in text files
bad = []
marker = re.compile(r"^(<{7}|>{7})( |$)|^={7}$")
for p in files(TEXT_SUFFIX):
    try:
        for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
            if marker.match(line):
                bad.append(f"{rel(p)}:{n}")
                break
    except UnicodeDecodeError:
        pass
check("3 No conflict markers left", bad)

# 4 Every task has a canonical id VIS-001..VIS-118 exactly once
status = load_status()
ids = [t["id"] for t in status["tasks"]]
expected = [f"VIS-{i:03d}" for i in range(1, 119)]
problems = []
if sorted(ids) != expected:
    problems.append(f"missing: {sorted(set(expected)-set(ids))}; extra/dup: {[i for i,c in collections.Counter(ids).items() if c>1 or i not in expected]}")
check("4 Task ids are VIS-001..VIS-118, each once", problems, f"{len(ids)} tasks")

# 5 Status values from the allowed vocabulary
allowed = {"OPEN", "DONE", "CODE_DONE_VERIFY_PENDING", "STATIC_AUDIT_DONE_VERIFY_PENDING", "CAPTURE_PENDING",
           "PARTIAL", "AUTHORED_HUMAN_GATE_OPEN", "MAP_DONE_FIX_OPEN", "BLOCKED_PHASE_CAPTURE"}
bad = [f"{t['id']}: {t['status']}" for t in status["tasks"] if t["status"] not in allowed]
check("5 Status values in the allowed set", bad)

# 6 Every non-OPEN task carries evidence text
bad = [t["id"] for t in status["tasks"] if t["status"] != "OPEN" and not (t.get("evidence") or "").strip()]
check("6 Non-OPEN tasks have evidence", bad)

# 7 VIS ids cited in game code comments exist in the task list
known = set(ids)
bad = collections.defaultdict(set)
for p in files({".cs"}, "game"):
    for vid in re.findall(r"VIS-\d{3}", p.read_text(encoding="utf-8")):
        if vid not in known:
            bad[vid].add(rel(p))
check("7 VIS ids cited in code exist", [f"{k}: {sorted(v)[:3]}" for k, v in sorted(bad.items())])

# 8 Painterly .import files with mipmaps disabled
bad = []
for p in files({".import"}, "game/assets/textures/painterly"):
    m = re.search(r"^mipmaps/generate=(\w+)", p.read_text(encoding="utf-8"), re.M)
    if m and m.group(1) == "false":
        bad.append(rel(p))
check("8 Painterly imports have mipmaps on", bad)

# 9 Every painterly texture named in PainterlyMaterialLibrary exists on disk
lib = (ROOT / "game/scripts/PainterlyMaterialLibrary.cs").read_text(encoding="utf-8")
paths = sorted({p for p in re.findall(r'"res://(assets/textures/[^"]+)"', lib)
                if re.search(r"\.(png|jpg|webp)$", p) and "{" not in p})
bad = [p for p in paths if not (ROOT / "game" / p).is_file()]
check("9 Painterly textures referenced by the library exist", bad, f"{len(paths)} references")

# 10 Phase ids in checkpoints exist in the atmosphere profiles
atm = json.loads((ROOT / "game/content/world/atmosphere.v1.json").read_text(encoding="utf-8"))
profile_ids = {e.get("id", "").split("/")[-1] for e in atm.get("entities", [])}
ck = json.loads((ROOT / "docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07_checkpoints.json").read_text(encoding="utf-8"))
phases = [c["phase"] for c in ck.get("phaseCaptures", [])]
check("10 Checkpoint phases exist as atmosphere profiles", [p for p in phases if p not in profile_ids],
      f"{len(phases)} phases, {len(profile_ids)} profiles")

# 11 Checkpoint point ids unique and every point has a spec
points = ck.get("points", [])
pid = [p["id"] for p in points]
bad = [p["id"] for p in points if "spec" not in p or "@" not in p["spec"] and ">" not in p["spec"]]
dups = [i for i, c in collections.Counter(pid).items() if c > 1]
check("11 Checkpoint points unique with a spec", [f"duplicate: {d}" for d in dups] + [f"no spec: {b}" for b in bad],
      f"{len(points)} points")

# 12 Station flags present in client, worker and runner
client = (ROOT / "eng/remote-check.py").read_text(encoding="utf-8")
worker = (ROOT / "eng/windows_station_worker.py").read_text(encoding="utf-8")
runner = (ROOT / "eng/run-windows-check.ps1").read_text(encoding="utf-8")
bad = []
for flag, field, param in [("--phase", "phase", "AtmospherePhase"), ("--fov", "fov", "ViewFov"), ("--diagnostic", "diagnostic", "DiagnosticView")]:
    if flag not in client: bad.append(f"client lacks {flag}")
    if f"'{field}'" not in worker: bad.append(f"worker lacks field {field}")
    if f"-{param}" not in worker: bad.append(f"worker does not pass -{param}")
    if f"${param}" not in runner: bad.append(f"runner lacks ${param}")
check("12 Station capture flags consistent (client, worker, runner)", bad)

# 13 Relative markdown links resolve in the visual-reset docs
bad = []
link = re.compile(r"\]\((?!https?://|#)([^)\s]+)\)")
docs = list(files({".md"}, "docs/production/visual_restyle_2026-10-07")) + [
    ROOT / "docs/production/URMAN_VISUAL_RESET_EXECUTION_2026-10-07.md"]
for d in docs:
    for target in link.findall(d.read_text(encoding="utf-8")):
        target = target.split("#")[0]
        if target and not (d.parent / target).exists():
            bad.append(f"{rel(d)} -> {target}")
check("13 Relative links in visual docs resolve", bad, f"{len(docs)} documents")

# 14 Every GLB under game/assets has a structure receipt
receipts = {p.stem.replace(".structure", "") for p in files({".json"}, "assets/source/blender/receipts")}
glbs = list(files({".glb"}, "game/assets"))
bad = [rel(g) for g in glbs if g.stem not in receipts]
check("14 Every GLB has a structure receipt", bad, f"{len(glbs)} GLB files, {len(receipts)} receipts")

# 15 Receipts match their GLB today
bad = []
for g in glbs:
    if g.stem not in receipts:
        continue
    r = subprocess.run([sys.executable, str(ROOT / "tools/blender/glb_structure_receipt.py"), "--check", str(g)],
                       capture_output=True, text=True)
    if r.returncode:
        bad.append(rel(g))
check("15 Structure receipts match current GLB files", bad)

# 16 asset_registry derived paths exist
reg = json.loads((ROOT / "assets/asset_registry.json").read_text(encoding="utf-8"))
missing = [a["id"] for a in reg["assets"] if isinstance(a.get("derived"), str) and "/" in a["derived"]
           and a["derived"].split()[0].startswith(("assets/", "game/")) and not (ROOT / a["derived"]).is_file()]
check("16 Registry derived files exist", missing, f"{len(reg['assets'])} records")

# 17 Police third-party GLBs without a registry record
registered = {a.get("derived") for a in reg["assets"]}
police = [rel(p) for p in files({".glb"}, "game/assets/third_party/police")]
check("17 Third-party GLBs are in the asset registry", [p for p in police if p not in registered],
      f"{len(police)} police GLBs")

# 18 Blender generators draw randomness only through seeded generators
r = subprocess.run([sys.executable, str(ROOT / "eng/verify-blender-determinism.py")], capture_output=True, text=True)
check("18 Blender generators are deterministic (static)", [] if r.returncode == 0 else [r.stdout.strip()[-300:]])

# 19 TODO/FIXME lines added in the local pass (HEAD vs first synced base)
diff = subprocess.run(["git", "diff", "747cb4d6", "HEAD", "-U0"], cwd=ROOT, capture_output=True, text=True).stdout
added = [l[1:].strip() for l in diff.splitlines() if l.startswith("+") and not l.startswith("+++")
         and re.search(r"\b(TODO|FIXME|XXX)\b", l)]
check("19 TODO/FIXME added since 747cb4d6", added[:20], f"{len(added)} lines")

# 20 Tracked files that are untracked-but-referenced by docs (missing from git)
refs = set()
for d in docs:
    refs.update(re.findall(r"`((?:tools|eng|game|docs|assets)/[^`\s]+\.(?:py|cs|sh|json|md|ps1))`", d.read_text(encoding="utf-8")))
untracked = set(subprocess.run(["git", "ls-files", "--others", "--exclude-standard"], cwd=ROOT,
                               capture_output=True, text=True).stdout.split())
tracked = set(subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True).stdout.split())
missing = sorted(r for r in refs if not (ROOT / r).exists() and r not in tracked)
check("20 Files named in visual docs exist", missing, f"{len(refs)} references, {len(untracked)} untracked files")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    lines = ["# Механический статический аудит визуального reset", "",
             "Инвентаризация без правок: каждая проверка выводит список нарушений. Пустой список — проверка пройдена.", ""]
    failed = 0
    for name, problems, note in results:
        status_text = "PASS" if not problems else f"FLAG ({len(problems)})"
        failed += bool(problems)
        lines.append(f"- **{name}** — {status_text}" + (f"; {note}" if note else ""))
        for item in problems[:25]:
            lines.append(f"    - {item}")
        if len(problems) > 25:
            lines.append(f"    - …ещё {len(problems) - 25}")
    lines.append("")
    lines.append(f"Итого: {len(results) - failed} PASS, {failed} FLAG из {len(results)}.")
    text = "\n".join(lines) + "\n"
    print(text)
    if args.out:
        args.out.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
