#!/usr/bin/env python3
"""Validate URMAN authoring specification. Does NOT run Godot or certify game quality.
Usage: python tooling/validate_spec.py [package_root] [--self-test]
Python 3.10+, standard library only. Exit 0 = specification checks passed.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
import sys
import zipfile

EXPECTED = {
    "worlds": ("worlds.spec.json", "worlds", 5),
    "photos": ("photos.spec.json", "photos", 13),
    "assets": ("assets.spec.json", "assets", 48),
    "tasks": ("tasks.spec.json", "tasks", 100),
    "migration": ("legacy_118_migration.json", "rows", 118),
    "references": ("references.json", "references", 24),
    "dialogues": ("dialogues.spec.json", "dialogues", 29),
    "coverage": ("author_coverage.json", "requirements", 10),
}

def read_package(root: Path) -> dict:
    result = {}
    for name, (filename, key, _) in EXPECTED.items():
        result[name] = json.loads((root / "data" / filename).read_text(encoding="utf-8"))[key]
    result["campaign"] = json.loads((root / "data/campaign.spec.json").read_text(encoding="utf-8"))
    return result

def evaluate(rule, flags: set[str]) -> bool:
    if isinstance(rule, str):
        return rule in flags
    if isinstance(rule, dict) and set(rule) == {"all"}:
        return all(evaluate(item, flags) for item in rule["all"])
    if isinstance(rule, dict) and set(rule) == {"any"}:
        return any(evaluate(item, flags) for item in rule["any"])
    raise ValueError(f"Unsupported project rule: {rule!r}")

def rule_leaves(rule) -> set[str]:
    if isinstance(rule, str):
        return {rule}
    return set().union(*(rule_leaves(item) for items in rule.values() for item in items))

def validate(data: dict, root: Path, check_files: bool = True) -> list[dict]:
    checks = []
    def check(name: str, ok: bool, details: str = ""):
        checks.append({"name": name, "status": "PASS" if ok else "FAIL", "details": details})
    idsets = {}
    for name, (_, _, count) in EXPECTED.items():
        rows = data[name]
        id_key = "legacy_id" if name == "migration" else "id"
        ids = [row[id_key] for row in rows]
        idsets[name] = set(ids)
        check(f"{name}: count and unique IDs", len(rows) == count and len(ids) == len(set(ids)), str(len(rows)))

    tasks = data["tasks"]
    by_id = {t["id"]: t for t in tasks}
    dependencies_exist = all(d in by_id for t in tasks for d in t["depends_on"])
    check("Every task dependency exists", dependencies_exist)
    visited, visiting = set(), set()
    def visit(node):
        if node in visiting:
            return False
        if node in visited:
            return True
        visiting.add(node)
        for other in by_id[node]["depends_on"]:
            if other not in by_id or not visit(other):
                return False
        visiting.remove(node)
        visited.add(node)
        return True
    check("Task DAG is acyclic", dependencies_exist and all(visit(t) for t in by_id))
    check("Tasks have actionable steps and acceptance", all(len(t["steps"]) >= 3 and len(t["acceptance"]) >= 2 and t["verification"] for t in tasks))
    check("All task asset/reference links resolve",
          all(a in idsets["assets"] for t in tasks for a in t["asset_ids"])
          and all(r in idsets["references"] for t in tasks for r in t["reference_ids"]))
    check("All asset families have at least one producing/integrating task",
          idsets["assets"] <= {a for t in tasks for a in t["asset_ids"]})
    check("No fabricated implementation status",
          all(t["status"] == "OPEN" and t["runtime_evidence"] == "NOT_RUN" and t["author_acceptance"] == "PENDING" for t in tasks)
          and all(a["runtime_status"] == "NOT_RUN" and a["production_status"] == "REQUESTED" for a in data["assets"]))

    worlds = data["worlds"]
    photo_by_id = {p["id"]: p for p in data["photos"]}
    passages = [p for p in data["photos"] if p["kind"] == "passage"]
    check("Five passage photos and eight supporting photos", len(passages) == 5 and len(data["photos"]) - len(passages) == 8)
    check("One passage photo per world",
          {p["world_id"] for p in passages} == idsets["worlds"]
          and all(w["photo_id"] in photo_by_id and photo_by_id[w["photo_id"]]["world_id"] == w["id"] for w in worlds))
    captures = []
    for world in worlds:
        ids = [n["id"] for n in world["nodes"]]
        node_ids = set(ids)
        valid_edges = all(a in node_ids and b in node_ids for a, b in world["edges"])
        check(f"{world['id']}: valid local graph", len(ids) == len(node_ids) and valid_edges)
        check(f"{world['id']}: finite positions", all(len(n["position"]) == 3 and all(isinstance(x, (int, float)) and abs(x) < 10000 for x in n["position"]) for n in world["nodes"]))
        adj = {n: set() for n in node_ids}
        if valid_edges:
            for a, b in world["edges"]:
                adj[a].add(b); adj[b].add(a)
        reached, todo = set(), [world["main_spawn"]]
        while todo:
            node = todo.pop()
            if node in reached:
                continue
            reached.add(node)
            todo += list(adj.get(node, ()) - reached)
        needed = {world["exit"], *[a["at"] for a in world["actions"]]}
        check(f"{world['id']}: main spawn can reach action nodes and exit", needed <= reached)
        check(f"{world['id']}: exactly three reachable linger nodes",
              len(world["linger"]) == 3 and len(set(world["linger"])) == 3 and set(world["linger"]) <= reached)
        check(f"{world['id']}: completion flags are supplied by actions",
              set(world["required"]) <= {a["effect"] for a in world["actions"]})
        check(f"{world['id']}: assets exist", set(world["assets"]) <= idsets["assets"])
        check(f"{world['id']}: six required capture views", len(world["capture_ids"]) == 6)
        captures += world["capture_ids"]
    check("Thirty distinct capture IDs", len(captures) == 30 and len(set(captures)) == 30)

    check("Every historical VIS task maps to current PW tasks",
          idsets["migration"] == {f"VIS-{i:03d}" for i in range(1, 119)}
          and all(r["successor_tasks"] and set(r["successor_tasks"]) <= idsets["tasks"] for r in data["migration"]))
    check("All author requirements have valid task coverage",
          idsets["coverage"] == {f"A{i:02d}" for i in range(1, 11)}
          and all(r["tasks"] and set(r["tasks"]) <= idsets["tasks"] for r in data["coverage"]))

    lines = [line for scene in data["dialogues"] for line in scene["lines"]]
    check("Eighty-one unique non-empty dialogue lines",
          len(lines) == 81 and len({l["id"] for l in lines}) == 81 and all(l["text_ru"].strip() for l in lines))
    check("Tatar translation is not falsely marked reviewed",
          all(l["tt_status"] == "NOT_TRANSLATED_REVIEW_REQUIRED" for l in lines))
    campaign = data["campaign"]
    rules = campaign["chapter_rules"]
    check("W05 remains optional for player but present in production",
          campaign["optional_worlds"] == ["W05"]
          and "W05" in idsets["worlds"] and "W05" not in campaign["main_worlds"]
          and not any(x.startswith("W05.") for x in rule_leaves(rules["W04"]) | rule_leaves(campaign["finale_rule"])))
    check("Final chapter requires both outside evidence chains",
          {"E-BANK-OUTSIDE.confirmed", "E-PHOTOGRAPHER-OUTSIDE.confirmed"} <= rule_leaves(rules["W04"]))
    all_flags = set().union(*(rule_leaves(r) for r in rules.values()))
    for order in campaign["supported_orders"]:
        state = {f for f in all_flags if not f.endswith(".completed")}
        valid_order = set(order) == set(campaign["main_worlds"]) and len(order) == 4
        for chapter in order:
            valid_order &= evaluate(rules[chapter], state)
            state.add(chapter + ".completed")
        check("Abstract chapter order " + " → ".join(order), valid_order,
              "Content/anchor prerequisites are supplied as an abstract fixture; not an end-to-end game test.")
    state_all = set(all_flags)
    state_all.discard("E-BANK-OUTSIDE.confirmed")
    check("W04 cannot unlock without river evidence", not evaluate(rules["W04"], state_all))
    state_all = set(all_flags); state_all.discard("E-PHOTOGRAPHER-OUTSIDE.confirmed")
    check("W04 cannot unlock without family evidence", not evaluate(rules["W04"], state_all))

    if check_files:
        check("Sixteen specification chapters", len(list((root / "docs").glob("*.md"))) == 16)
        check("Five detailed level passports", len(list((root / "levels").glob("W*.md"))) == 5)
        check("Coverage document paths exist", all((root / p).exists() for r in data["coverage"] for p in r["documents"]))
        for ref in data["references"]:
            if ref.get("local_file"):
                path = root / ref["local_file"]
                ok = path.is_file()
                if ok and ref.get("sha256"):
                    ok = hashlib.sha256(path.read_bytes()).hexdigest() == ref["sha256"]
                check("Local reference " + ref["id"], ok, ref["local_file"])
        forbidden = {".ttf", ".otf", ".woff", ".woff2"}
        fonts = [str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and p.suffix.lower() in forbidden]
        archive = root / "legacy/URMAN_VISUAL_RESET_PACKAGE.zip"
        if archive.is_file():
            with zipfile.ZipFile(archive) as z:
                fonts += [name for name in z.namelist() if Path(name).suffix.lower() in forbidden]
        check("No redistributable font files in package or historical ZIP", not fonts, ", ".join(fonts))
        task_text = (root / "docs/11_TASKS_RU.md").read_text(encoding="utf-8")
        check("Readable task cards match registry IDs", all(f"### {t['id']}. {t['title']}" in task_text for t in tasks))
    return checks

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path, nargs="?", default=Path(__file__).resolve().parents[1])
    parser.add_argument("--self-test", action="store_true", help="Verify that damaged fixtures are rejected.")
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        data = read_package(root)
        checks = validate(data, root)
        if args.self_test:
            cases = []
            broken = copy.deepcopy(data); broken["tasks"][0]["depends_on"] = ["PW-100"]
            cases.append(("cyclic dependency", broken, "Task DAG is acyclic"))
            broken = copy.deepcopy(data); broken["tasks"][0]["asset_ids"].append("A-NONEXISTENT")
            cases.append(("missing asset", broken, "All task asset/reference links resolve"))
            broken = copy.deepcopy(data); broken["campaign"]["chapter_rules"]["W04"]["all"].append("W05.completed")
            cases.append(("optional chapter made mandatory", broken, "W05 remains optional for player but present in production"))
            for label, damaged, expected in cases:
                rejection = next(c for c in validate(damaged, root, False) if c["name"] == expected)["status"] == "FAIL"
                checks.append({"name": "Negative self-test: " + label, "status": "PASS" if rejection else "FAIL", "details": "Damaged copy rejected; source unchanged."})
        passed = all(c["status"] == "PASS" for c in checks)
        report = {
            "kind": "SPECIFICATION_VALIDATION_NOT_GAME_QA",
            "version": 1,
            "result": "PASS" if passed else "FAIL",
            "checks_count": len(checks),
            "runtime_execution": "NOT_RUN",
            "human_art_review": "NOT_PERFORMED",
            "scope": "References, counts, graph consistency, abstract unlock ordering and historical coverage only.",
            "checks": checks,
        }
        (root / "VALIDATION_REPORT.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        md = "# Проверка целостности пакета\n\n"
        md += f"**Результат:** {report['result']}. **Проверок:** {len(checks)}.\n\n"
        md += "Это проверка спецификации: не запуск игры, не измерение FPS, не доказательство визуального качества и не приёмка автора. Проверка порядка глав использует абстрактные предпосылки, а не исполняемый контент URMAN.\n\n"
        md += "| Проверка | Результат | Примечание |\n|---|---|---|\n"
        md += "\n".join(f"| {c['name']} | {c['status']} | {c['details']} |" for c in checks) + "\n"
        (root / "VALIDATION_REPORT_RU.md").write_text(md, encoding="utf-8")
        print(json.dumps({"result": report["result"], "checks": len(checks), "failed": [c["name"] for c in checks if c["status"] == "FAIL"]}, ensure_ascii=False, indent=2))
        return 0 if passed else 1
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as exc:
        print(f"Specification validation failed: {exc}", file=sys.stderr)
        return 2

if __name__ == "__main__":
    raise SystemExit(main())
