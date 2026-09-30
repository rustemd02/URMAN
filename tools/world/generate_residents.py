#!/usr/bin/env python3
"""Generate the ambient residents of the north-street quarter (village expansion, slice 3).

Reads the free spots found in the running game by tests/Act1ResidentSpotCheck (the
"resident-spot|", "anchor-spot|" and "bench-spot|" lines of its log) and writes
game/content/world/act1_north_street_residents.world.v1.json - an executor=generic plot
of "npc" entities, each with a one-block schedule (place, facing, occupation).

Ambient only: no dialogue, no knowledge, no quest state. People stand, talk in pairs,
repair a fence, sit on a bench. Their positions were checked against physics bodies and
visible props; how they LOOK is not verified (no rendering in the cloud).

usage: generate_residents.py <spot-check log> [<anchor log>]
"""
from __future__ import annotations

import json
import math
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/content/world/act1_north_street_residents.world.v1.json"
rng = random.Random(0x5245)  # "RE"

ANIM = {
    "idle": "urman.anim:idle-plain",
    "talk": "urman.anim:talk-plain",
    "kneel": "urman.anim:work-kneel",
    "sit": "urman.anim:sit",
    "torch": "urman.anim:idle",
}

log = Path(sys.argv[1]).read_text(encoding="utf-8", errors="replace")
houses = {}
for line in log.splitlines():
    m = re.match(r"resident-spot\|([\w-]+)\|([-\d.]+)\|([-\d.]+)\|([-\d.]+)\|(.*)", line)
    if m:
        spots = [tuple(float(v) for v in s.split(",")) for s in m.group(5).split(";") if s]
        houses[m.group(1)] = {"x": float(m.group(2)), "z": float(m.group(3)), "yaw": float(m.group(4)), "spots": spots}
benches = {}
for line in log.splitlines():
    m = re.match(r"bench-spot\|([\w-]+)\|([-\d.]+)\|([-\d.]+)\|seatTop=([-\d.]+)\|faceYaw=([-\d.]+)", line)
    if m:
        benches[m.group(1)] = {"x": float(m.group(2)), "z": float(m.group(3)), "top": float(m.group(4)), "yaw": float(m.group(5))}
anchors = {}
for line in (Path(sys.argv[2]).read_text(encoding="utf-8", errors="replace").splitlines() if len(sys.argv) > 2 else log.splitlines()):
    m = re.match(r"anchor-spot\|([\w-]+)\|(.*)", line)
    if m:
        anchors[m.group(1)] = [tuple(float(v) for v in s.split(",")) for s in m.group(2).split(";") if s]

entities: list[dict] = []
count = {"idle": 0, "talk": 0, "kneel": 0, "sit": 0}


def yaw_to(ax, az, bx, bz):
    return round(math.degrees(math.atan2(bx - ax, bz - az)), 1)


def person(slug, name, x, z, yaw, motion, scale, note, y=0.0):
    entities.append({
        "id": f"urman.world:act1/residents/{slug}", "kind": "npc", "name": name, "note": note,
        "params": {
            "characterId": f"resident-{slug}", "kitPrefix": "Resident", "position": [round(x, 2), y, round(z, 2)],
            "yawDegrees": round(yaw, 1), "scale": scale,
            "schedule": [{"id": "day", "place": [round(x, 2), y, round(z, 2)], "yawDegrees": round(yaw, 1), "motion": ANIM[motion]}],
        }})
    count[motion] += 1


# ---- households: one or two people in the yard, by the occupation the yard suggests ------------
cycle = ["idle", "talk", "kneel", "idle", "talk", "idle", "kneel", "talk", "idle", "idle"]
for index, slug in enumerate(sorted(houses)):
    h = houses[slug]
    spots = h["spots"]
    if not spots:
        continue
    plan = cycle[index % len(cycle)]
    facing_street = h["yaw"]                      # the house front looks at its street
    sx, sz = spots[0]
    scale = round(rng.uniform(.95, 1.04), 2)
    if plan == "talk":
        partner = next((p for p in spots[1:] if 1.0 <= math.hypot(p[0] - sx, p[1] - sz) <= 2.6), None)
        if partner:
            person(f"{slug}-a", f"Житель двора {slug} (беседа)", sx, sz, yaw_to(sx, sz, *partner), "talk", scale,
                   "Разговаривает через двор с соседом.")
            person(f"{slug}-b", f"Сосед двора {slug} (беседа)", partner[0], partner[1], yaw_to(*partner, sx, sz), "talk",
                   round(rng.uniform(.9, 1.0), 2), "Собеседник.")
            continue
        plan = "idle"
    if plan == "kneel":
        person(f"{slug}-a", f"Житель двора {slug} (чинит)", sx, sz, facing_street + rng.choice([-25, 0, 25]), "kneel", scale,
               "Чинит калитку или доску у ворот.")
        continue
    person(f"{slug}-a", f"Житель двора {slug}", sx, sz, facing_street + rng.choice([-30, -10, 10, 30]), "idle", scale,
           "Стоит во дворе, смотрит на улицу.")

# ---- children: two small figures near the square and one lane --------------------------------------
kids = [("sq-well", .74), ("sq-well", .70), ("sq-board", .78)]
used = set()
for i, (anchor, scale) in enumerate(kids):
    for spot in anchors.get(anchor, []):
        if spot not in used and all(math.hypot(spot[0] - u[0], spot[1] - u[1]) > 1.8 for u in used):
            used.add(spot)
            person(f"kid-{i + 1}", f"Ребёнок {i + 1}", spot[0], spot[1], rng.uniform(0, 360), "idle", scale,
                   "Ребёнок у колодца/доски: в школе одиннадцать детей, остальные дома.")
            break

# ---- square: adults at the notice board and the well --------------------------------------------------
for i, anchor in enumerate(["sq-board", "sq-well"]):
    spots = anchors.get(anchor, [])
    placed = [s for s in spots if s not in used]
    if len(placed) >= 2 and anchor == "sq-well":
        a = placed[0]
        b = next((p for p in placed[1:] if 1.0 <= math.hypot(p[0] - a[0], p[1] - a[1]) <= 2.6), None)
        if b:
            person("square-well-a", "Женщина у колодца", a[0], a[1], yaw_to(*a, *b), "talk", 1.0, "Разговаривает у колодца площади.")
            person("square-well-b", "Соседка у колодца", b[0], b[1], yaw_to(*b, *a), "talk", .97, "Собеседница.")
            used.update([a, b])
            continue
    if placed:
        a = placed[0]
        bx, bz = (-10.5, 102.0) if anchor == "sq-board" else (12.5, 122.0)
        person(f"square-{anchor}", "Читает объявления" if anchor == "sq-board" else "Стоит у колодца", a[0], a[1],
               yaw_to(*a, bx, bz), "idle", 1.0, "Читает доску объявлений." if anchor == "sq-board" else "Ждёт очереди с вёдрами.")
        used.add(a)

# ---- benches: the lookout bench is promised by the tour ("люди сидят, на тот берег смотрят") ---------
SEAT_LIFT = 0.30  # seat top is ~0.77 m; the sitting clip is authored for a ~0.45 m chair
for bench, who, note in [("lookout-bench", "Старик на лавочке", "Сидит над оврагом и смотрит на дальний берег."),
                         ("sq-bench1", "Женщина на лавочке", "Сидит на площади.")]:
    b = benches.get(bench)
    if b:
        if bench == "lookout-bench":
            b = {**b, "yaw": 90.0}  # the bench was turned to face the ravine after the spot check ran
        person(bench.replace("-bench", "") + "-sitter", who, b["x"], b["z"], b["yaw"], "sit", 1.0, note, y=round(b["top"] - .45, 2))

doc = {"schemaVersion": 1, "kind": "urman.world-plot", "id": "urman.world:act1/residents", "executor": "generic",
       "name": "Жители северного квартала (фоновые)",
       "note": "Ambient: стоят, беседуют парами, чинят, сидят. Без диалогов и без сюжетного состояния. Точки подобраны диагностикой "
               "tests/Act1ResidentSpotCheck (физика и видимые предметы свободны); внешний вид не проверялся. Генератор — "
               "tools/world/generate_residents.py. Имена собственные и реплики добавляются отдельно (ACT1-NPC.2/3).",
       "entities": entities}
OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"wrote {OUT.relative_to(ROOT)}: {len(entities)} residents {count}")
