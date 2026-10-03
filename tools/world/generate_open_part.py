#!/usr/bin/env python3
"""Generate the open part of the village (relayout plan v3, stage 4) from the approved plan.

Input:  docs/production/village_relayout_2026-10-01/plan.json (tools/world/plan_village_relayout.py)
Output: game/content/world/act1_open_part.world.v1.json   - executor=generic plot: the households of
        the open part as generic parcels with their address, plus top-level "roads" (the new cross
        streets) read by the terrain, the road graph and the winter road ribbons, and "lots" (the
        plan's plot rectangles) read by the legacy clean-up.
        game/content/world/act1_village_kit.world.v1.json     - the old kit houses of these households
        and their sheds, fences and woodpiles are retired ("retired": true); the shop, the school annex
        and the council keep their kit house and move to their plan plot with their yard pieces.
        game/content/urman.settlement.addresses.v1.json        - rows of households that are generic
        parcels now leave the manifest (the address travels in the plot); moved public rows and the
        kept house of Tamara Gennadievna are renumbered; four new streets are added.
        game/content/world/act1_north_street.world.v1.json    - its four households move here; the old
        square's well, benches and board move to Мәйдан; the ravine lookout to the end of Яңа урам.
        game/content/world/act1_north_street_residents.world.v1.json - residents follow their yard or
        the bench/well/board they stand at (rigid transform).

Address ids never change. House numbers follow the new streets (odd on the left, see number()).
The script is idempotent: a re-run starts from what the earlier run left (the plot, the retired kit
rows) and produces the same files.
"""
from __future__ import annotations

import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "docs/production/village_relayout_2026-10-01/plan.json"
OUT = ROOT / "game/content/world/act1_open_part.world.v1.json"
NORTH = ROOT / "game/content/world/act1_north_street.world.v1.json"
RESIDENTS = ROOT / "game/content/world/act1_north_street_residents.world.v1.json"
MANIFEST = ROOT / "game/content/urman.settlement.addresses.v1.json"
KIT = ROOT / "game/content/world/act1_village_kit.world.v1.json"

V = "urman.catalog:urman_village_exterior_kit/"
PARCELS = {  # catalog id -> width, depth (unscaled, front faces local +Z)
    "A": (V + "villageparcel-varianta-timbergable", 13.14, 13.33),
    "B": (V + "villageparcel-variantb-plasterannex", 12.67, 13.86),
    "C": (V + "villageparcel-variantc-banyayard", 13.31, 12.59),
}
MAX_SCALE, MIN_SCALE = .9, .72
SETBACK = 2.0           # m between the lot's street edge and the parcel's front fence (verge, palisadnik)
SETBACK_OVERRIDE = {"ADR-H018": 2.6}   # the bus pavilion stands on its verge
LOT_OVERRIDE = {"ADR-H016": {"z": 65.42, "w": 10.4},   # clear of the office porch (measured bounds)
                "ADR-H017": {"z": 22.0, "w": 13.0},    # the shop's yard reaches 5 m south of its house
                "ADR-H020": {"z": 38.8},
                # The arrival bus stop (bench, timetable) stands on the east verge at z 6, where H022's
                # gate opened: the corner plot fronts Яңа урам instead, its neighbours close up.
                "ADR-H022": {"x": 10.8, "z": 5.0, "yaw": 0.0, "w": 10.0, "d": 10.6},
                "ADR-H049": {"z": -6.2}, "ADR-H032": {"z": -16.0}}
# Old-layout road pieces lying on the new plots (the diagonal FAP branch is gone).
RETIRE_EXTRA = {"village_day@fap-branch-crown-variant"}
ROAD_WIDTH = 3.5

# Streets of the open part. Plan road name -> (address street, polyline as built). Main street and
# the bridge road stay in code (AgentBAct1Layout.MainRoadAxis / FapBranchAxis + BridgeApproachAxis).
ROADS = {
    "arrival-east": "yana",
    "lane-36": "chishma",
    "cross-62": "usal",
    "cross84": "bakcha",
    "cross130": "kyr",
}
NEW_STREETS = [  # Proposal names (native check pending, OQ-LANG-STREETS)
    {"id": "yana", "tatar": "Яңа ур.", "russian": "ул. Новая", "origin": [0.0, 0.0, 12.0], "oddSide": "left", "protected": False},
    {"id": "chishma", "tatar": "Чишмә ур.", "russian": "ул. Родниковая", "origin": [0.0, 0.0, -36.0], "oddSide": "left", "protected": False},
    {"id": "bakcha", "tatar": "Бакча ур.", "russian": "ул. Садовая", "origin": [0.0, 0.0, 84.0], "oddSide": "left", "protected": False},
    {"id": "kyr", "tatar": "Кыр ур.", "russian": "ул. Полевая", "origin": [0.0, 0.0, 130.0], "oddSide": "left", "protected": False},
]
MAIN = [(0.0, 150.0), (0.0, -53.5)]               # numbered north to south
BRIDGE_ROAD = [(0.0, -24.0), (45.0, -24.0)]

# Households that keep their kit house and move with their yard pieces (public interiors are built
# relative to the facade node, so they follow).
KIT_MOVES = {
    "ADR-H020": ["zirat_road@west-return-mid-facade", "zirat_road@west-return-mid-yard"],          # shop
    "ADR-H031": ["zirat_road@east-return-mid-facade", "zirat_road@east-return-mid-shed"],          # school annex
    "ADR-H040": ["village_day@east-street-horizon-facade", "village_day@east-street-horizon-fence"],  # council
    # Houses that carry an optional discovery built on their own window or outbuilding.
    "ADR-H019": ["village_day@main-street-east-facade"],                                             # side-window shutter
    "ADR-H021": ["village_day@connective-street-deep-banya-yard-parcel"],                            # repair bench in the shed bay
}
# Kit houses are placed toward the street edge of their lot like the parcels (m from the lot centre).
KIT_SHIFT = {"ADR-H019": 3.0, "ADR-H021": 2.5}
# Kept in place (deviation from the plan, recorded in decision_log): Tamara Gennadievna's house owns
# her quest's fence, boards and markers; its lot in the plan is where it already stands.
KEEP_IN_PLACE = {"ADR-H023": "village-house@ReturnEastHouseA8Silhouette"}
# Kit rows never retired: the babai yard, the FAP, wells and the arrival landmarks.
KEEP_PREFIXES = ("house_old_pc@babai", "house_old_pc@yard", "house_old_pc@west-yard", "house_old_pc@depth-yard",
                 "house_old_pc@forward-right", "house_old_pc@east-depth", "house_old_pc@two-level")
HOUSE_FAMILY = ("VillageParcel", "DwellingFacade", "OutbuildingShed", "FenceSegment", "Gate_Crooked", "Woodpile")
OPEN_AREA = (-64.0, 46.0, -80.0, 160.0)          # x0, x1, z0, z1 of the open part

# Old square furniture (north-street plot) -> its place on Мәйдан / at the ravine lookout.
SQUARE_MOVES = {
    "sq-well": (-25.5, 61.3, 0.0),
    "sq-bench1": (-12.5, 34.2, 0.0),     # south edge, facing the garden, clear of the post office path
    "sq-bench2": (-8.5, 34.2, 0.0),
    "sq-board": (-4.6, 40.5, 90.0),
    "lookout-bench": (44.4, 15.5, 90.0),
    "lookout-board": (44.4, 8.6, -90.0),
}
# Residents standing at a piece of furniture follow it.
RESIDENT_ANCHORS = {"kid-1": "sq-well", "kid-2": "sq-well", "square-well-a": "sq-well", "square-well-b": "sq-well",
                    "kid-3": "sq-board", "square-sq-board": "sq-board", "sq1-sitter": "sq-bench1",
                    "lookout-sitter": "lookout-bench"}
NS_HOUSEHOLDS = {"ADR-NS-M-W1": "m-w1", "ADR-NS-M-E1": "m-e1", "ADR-NS-E-S1": "e-s1", "ADR-NS-E-N1": "e-n1"}
SCATTER_MOVES = {"trees-0": ([-60.0, 0, 100.0], 5), "trees-1": ([-60.0, 0, 145.0], 5), "trees-2": ([41.0, 0, 162.0], 7)}

plan = json.loads(PLAN.read_text(encoding="utf-8"))
manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
north = json.loads(NORTH.read_text(encoding="utf-8"))
kit = json.loads(KIT.read_text(encoding="utf-8"))
roads = {name: [tuple(p) for p in plan["roads"][name]] for name in ROADS}
previous = {}
if OUT.exists():
    for e in json.loads(OUT.read_text(encoding="utf-8"))["entities"]:
        if "address" in e["params"]:
            previous[e["params"]["address"]["id"]] = e["params"]


def rot(dx, dz, yaw):
    a = math.radians(yaw)
    return dx * math.cos(a) + dz * math.sin(a), -dx * math.sin(a) + dz * math.cos(a)


def facing(yaw):
    return math.sin(math.radians(yaw)), math.cos(math.radians(yaw))


def along(points, x, z):
    """(distance along the polyline, side +1 left/-1 right, nearest point, distance) of x,z."""
    best, acc, result = 1e18, 0.0, None
    for (ax, az), (bx, bz) in zip(points, points[1:]):
        dx, dz = bx - ax, bz - az
        L = math.hypot(dx, dz)
        t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (L * L)))
        px, pz = ax + dx * t, az + dz * t
        d = math.hypot(x - px, z - pz)
        if d < best:
            cross = dx * (z - az) - dz * (x - ax)
            best, result = d, (acc + t * L, 1 if cross > 0 else -1, (px, pz), d)
        acc += L
    return result


def kebab(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "-", name).lower()


# --- the households of the open part ----------------------------------------------------------
open_plan = [e for e in plan["plan"] if e["zone"] == "open"]
centre_plan = {e["id"]: e for e in plan["plan"] if e["zone"] == "centre"}
rows = {r["addressId"]: r for r in manifest["buildings"]}
ns_by_slug = {e["id"].rsplit("/", 1)[1]: e for e in north["entities"]}
kit_by_anchor = {e["params"]["logicalAnchor"]: e for e in kit["entities"]}


def lot_street(to):
    """The street a lot fronts: nearest road to the middle of its street edge."""
    fx, fz = facing(to["yaw"])
    px, pz = to["x"] + fx * to["d"] / 2, to["z"] + fz * to["d"] / 2
    options = [("tukay", MAIN), ("urman", BRIDGE_ROAD)] + [(ROADS[n], pts) for n, pts in roads.items()]
    street, pts = min(options, key=lambda o: along(o[1], px, pz)[3])
    return street, pts, (px, pz)


def house_kit_row(entry):
    """The kit row of the house this plan entry moves: by source name, else the nearest dwelling."""
    row = rows.get(entry["id"])
    if row:
        for anchor, e in kit_by_anchor.items():
            tail = anchor.split("@", 1)[1]
            if tail in (row["sourceName"], kebab(row["sourceName"])):
                return anchor
    best = min((a for a, e in kit_by_anchor.items() if any(k in e["params"]["component"] for k in ("Dwelling", "Facade", "VillageParcel"))
                and not e["params"]["component"].endswith("_Yard")),
               key=lambda a: math.hypot(kit_by_anchor[a]["params"]["position"][0] - entry["x"],
                                        kit_by_anchor[a]["params"]["position"][2] - entry["z"]))
    p = kit_by_anchor[best]["params"]["position"]
    if math.hypot(p[0] - entry["x"], p[2] - entry["z"]) > .6:
        raise SystemExit(f"{entry['id']}: no kit house at {entry['x']},{entry['z']} (nearest {best})")
    return best


households = []
for e in open_plan + [centre_plan["ADR-H020"]]:
    to = dict(e["to"], **LOT_OVERRIDE.get(e["id"], {}))
    to.setdefault("d", 18.0)
    street, pts, front = lot_street(to)
    households.append(dict(plan=e, id=e["id"], to=to, street=street, front=front,
                           kind="kit" if e["id"] in KIT_MOVES else "keep" if e["id"] in KEEP_IN_PLACE else "parcel"))

# --- numbering --------------------------------------------------------------------------------
babai_front = (-4.5, -1.0)


def number_streets():
    groups = {}
    for h in households:
        groups.setdefault(h["street"], []).append(h)
    for street, hs in groups.items():
        # Numbers of rows that stay in the manifest are taken (hidden legacy rows included).
        taken = {r["number"] for r in manifest["buildings"] if r["streetId"] == street and r["addressId"] not in
                 {x["id"] for x in households}}
        if street == "tukay":
            # Main street: odd on the west, numbered north to south; babai keeps 17 (canon address).
            west = sorted([h for h in hs if h["front"][0] < 0], key=lambda h: -h["front"][1])
            east = sorted([h for h in hs if h["front"][0] > 0], key=lambda h: -h["front"][1])
            north_of_babai = sum(1 for h in west if h["front"][1] > babai_front[1])
            n = 17 - 2 * north_of_babai
            for h in west:
                while str(n) in taken: n += 2
                h["number"] = str(n); n += 2
                if n == 17: n = 19
            n = 2
            for h in east:
                while str(n) in taken: n += 2
                h["number"] = str(n); n += 2
            continue
        pts = BRIDGE_ROAD if street == "urman" else roads[next(k for k, v in ROADS.items() if v == street)]
        # Урман: the FAP and the square buildings registered in code keep theirs too.
        taken |= {"2", "12", "14", "16", "18", "20"} if street == "urman" else set()
        odd, even = 1, 2
        for h in sorted(hs, key=lambda h: along(pts, *h["front"])[0]):
            if along(pts, *h["front"])[1] > 0:
                while str(odd) in taken: odd += 2
                h["number"], odd = str(odd), odd + 2
            else:
                while str(even) in taken: even += 2
                h["number"], even = str(even), even + 2


number_streets()

# --- plot entities ------------------------------------------------------------------------------
entities, retired, kit_moves, residents_follow = [], set(), {}, {}
variants = "ABCBACCABCAB"
for i, h in enumerate(sorted(households, key=lambda h: h["id"])):
    aid, to = h["id"], h["to"]
    if h["kind"] == "keep":
        continue
    if h["kind"] == "kit":
        # Move the kit house and its yard pieces by one rigid transform onto the plan plot.
        house = kit_by_anchor[KIT_MOVES[aid][0]]["params"]
        ox, _, oz = house.get("originalPosition", house["position"])
        dyaw = to["yaw"] - house.get("originalYawDegrees", house["yawDegrees"])
        fx, fz = facing(to["yaw"])
        tx, tz = to["x"] + fx * KIT_SHIFT.get(aid, 0.0), to["z"] + fz * KIT_SHIFT.get(aid, 0.0)
        for anchor in KIT_MOVES[aid]:
            kit_moves[anchor] = (ox, oz, dyaw, tx, tz)
        continue
    slug = aid[4:].lower()
    if aid in NS_HOUSEHOLDS:
        source = ns_by_slug.get(NS_HOUSEHOLDS[aid] + "-house")
        if source:
            cat, cadastral = source["params"]["catalogId"], source["params"]["address"]["cadastral"]
            residents_follow[NS_HOUSEHOLDS[aid]] = (source["params"]["position"], source["params"]["yawDegrees"])
        else:
            cat, cadastral = previous[aid]["catalogId"], previous[aid]["address"]["cadastral"]
            residents_follow[NS_HOUSEHOLDS[aid]] = (previous[aid]["position"], previous[aid]["yawDegrees"])
    elif aid in previous:
        cat, cadastral = previous[aid]["catalogId"], previous[aid]["address"]["cadastral"]
    else:
        anchor = house_kit_row(h["plan"])
        component = kit_by_anchor[anchor]["params"]["component"]
        letter = "A" if "VariantA" in component else "B" if "VariantB" in component else "C" if "VariantC" in component \
            else variants[i % len(variants)]
        cat, cadastral = PARCELS[letter][0], rows[aid]["cadastral"]
    _, mw, md = next(p for p in PARCELS.values() if p[0] == cat)
    scale = round(min(MAX_SCALE, (to["w"] - .4) / mw), 3)
    if scale < MIN_SCALE:
        raise SystemExit(f"{aid}: lot {to['w']} m too narrow for {cat} ({scale})")
    fx, fz = facing(to["yaw"])
    setback = max(.6, min(SETBACK_OVERRIDE.get(aid, SETBACK), to["d"] / 2 - md * scale / 2))
    shift = to["d"] / 2 - setback - md * scale / 2
    x, z = to["x"] + fx * shift, to["z"] + fz * shift
    anchor_pt = along(MAIN if h["street"] == "tukay" else BRIDGE_ROAD if h["street"] == "urman"
                      else roads[next(k for k, v in ROADS.items() if v == h["street"])], x, z)[2]
    h["parcel"] = (x, z, to["yaw"], mw * scale / 2, md * scale / 2)
    entities.append({
        "id": f"urman.world:act1/open-part/{slug}-house", "kind": "prop", "name": f"{slug}-house",
        "params": {"catalogId": cat, "position": [round(x, 3), 0, round(z, 3)], "yawDegrees": round(to["yaw"], 1),
                   "scale": scale, "collision": "surfaces",
                   "address": {"id": aid, "street": h["street"], "number": h["number"], "cadastral": cadastral},
                   "terrainPad": {"halfSize": [round(mw * scale / 2 + .65, 3), round(md * scale / 2 + .65, 3)],
                                  "streetAnchor": [round(anchor_pt[0], 3), round(anchor_pt[1], 3)], "feather": 2.25}},
        "note": "Открытая часть: хозяйство, ворота к своей улице"})

# Square furniture and the lookout move from the north-street plot to this one.
furniture_moves = {}
for name, (x, z, yaw) in SQUARE_MOVES.items():
    source = ns_by_slug.get(name)
    prior = next((e for e in json.loads(OUT.read_text(encoding="utf-8"))["entities"] if e["name"] == name), None) \
        if OUT.exists() else None
    base = source or prior
    if base is None:
        raise SystemExit("missing furniture " + name)
    old = base["params"]
    furniture_moves[name] = (old["position"], old.get("yawDegrees", 0.0), (x, z, yaw))
    entities.append({"id": f"urman.world:act1/open-part/{name}", "kind": "prop", "name": name,
                     "params": {**{k: v for k, v in old.items() if k not in ("position", "yawDegrees")},
                                "position": [x, 0, z], "yawDegrees": yaw},
                     "note": "Мәйдан" if name.startswith("sq-") else "Смотровая над оврагом в конце Яңа урам"})

lots = [{"id": h["id"], "kind": h["kind"], "position": [h["to"]["x"], h["to"]["z"]], "yawDegrees": h["to"]["yaw"],
         "size": [h["to"]["w"], h["to"]["d"]], "street": h["street"], "number": h.get("number")} for h in households]
# A street that crosses the main street is two roads from the junction, so the road graph
# joins both halves at the crossing (it links an end lying on another road, not mid-crossings).
out_roads = []
for name, pts in roads.items():
    (ax, az), (bx, bz) = pts[0], pts[-1]
    halves = [(name + "-west", [(0.0, az), (ax, az)]), (name + "-east", [(0.0, bz), (bx, bz)])] \
        if len(pts) == 2 and az == bz and ax < 0 < bx else [(name, pts)]
    for road_id, half in halves:
        # A graph node every few metres: a door's road anchor is the nearest node of its edge.
        dense = [half[0]]
        for (ax, az), (bx, bz) in zip(half, half[1:]):
            n = max(1, math.ceil(math.hypot(bx - ax, bz - az) / 6.0))
            dense += [(ax + (bx - ax) * k / n, az + (bz - az) * k / n) for k in range(1, n + 1)]
        out_roads.append({"id": road_id, "street": ROADS[name], "points": [[round(x, 2), round(z, 2)] for x, z in dense],
                          "width": ROAD_WIDTH})
doc = {"schemaVersion": 1, "kind": "urman.world-plot", "id": "urman.world:act1/open-part", "name": "Открытая часть деревни",
       "executor": "generic",
       "note": "Открытая часть по утверждённой схеме v3: прямая Тукай урамы, поперечные улицы Яңа, Чишмә, Усал, Бакча, Кыр, "
               "участки с садом за домом. Генератор tools/world/generate_open_part.py; положения можно править в Studio.",
       "openArea": list(OPEN_AREA), "roads": out_roads, "lots": lots, "entities": entities}
OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# --- kit: retire old houses and yard pieces of the open part, move the three public houses ------
x0, x1, z0, z1 = OPEN_AREA
for e in kit["entities"]:
    p = e["params"]
    anchor = p["logicalAnchor"]
    if anchor in kit_moves:
        ox, oz, dyaw, tx, tz = kit_moves[anchor]
        px, py, pz = p.get("originalPosition", p["position"])
        oyaw = p.get("originalYawDegrees", p["yawDegrees"])
        p.setdefault("originalPosition", [px, py, pz]); p.setdefault("originalYawDegrees", oyaw)
        p.setdefault("groundingReferenceXZ", [px, pz])
        dx, dz = rot(px - ox, pz - oz, dyaw) if (px, pz) != (ox, oz) else (0.0, 0.0)
        p["position"] = [round(tx + dx, 3), py, round(tz + dz, 3)]
        p["yawDegrees"] = round(((oyaw + dyaw + 180) % 360) - 180, 3)
        e["note"] = "Перепланировка v3, этап 4: перенесён на участок схемы вместе с двором"
        continue
    px, _, pz = p.get("originalPosition", p["position"])
    if not (x0 <= px <= x1 and z0 <= pz <= z1): continue
    if anchor in KEEP_IN_PLACE.values() or anchor.startswith(KEEP_PREFIXES): continue
    if not any(k in p["component"] for k in HOUSE_FAMILY) and anchor not in RETIRE_EXTRA: continue
    if anchor in {a for moves in KIT_MOVES.values() for a in moves}: continue
    p["retired"] = True
    p.pop("hidden", None)
    retired.add(anchor)
KIT.write_text(json.dumps(kit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# --- manifest -----------------------------------------------------------------------------------
parcel_ids = {h["id"] for h in households if h["kind"] == "parcel"}
manifest["buildings"] = [r for r in manifest["buildings"] if r["addressId"] not in parcel_ids]
for h in households:
    if h["kind"] in ("kit", "keep") and h["id"] in rows:
        row = next(r for r in manifest["buildings"] if r["addressId"] == h["id"])
        row["streetId"], row["number"] = h["street"], h["number"]
known = {s["id"] for s in manifest["streets"]}
manifest["streets"] += [s for s in NEW_STREETS if s["id"] not in known]
MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

# --- north-street plot: its households and the old square leave --------------------------------
leaving = {slug for slug in NS_HOUSEHOLDS.values()}
north["entities"] = [e for e in north["entities"]
                     if e["id"].rsplit("/", 1)[1].rsplit("-", 1)[0] not in leaving
                     and e["name"] not in SQUARE_MOVES and e["name"] != "well"]
for e in north["entities"]:
    if e["name"] in SCATTER_MOVES:
        e["params"]["position"], e["params"]["radius"] = SCATTER_MOVES[e["name"]]
NORTH.write_text(json.dumps(north, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# --- residents follow their yard or furniture ---------------------------------------------------
new_pose = {}
for e in entities:
    if "address" in e["params"] and e["params"]["address"]["id"] in NS_HOUSEHOLDS:
        new_pose[NS_HOUSEHOLDS[e["params"]["address"]["id"]]] = e["params"]
residents = json.loads(RESIDENTS.read_text(encoding="utf-8"))
moved = 0
for r in residents["entities"]:
    name = r["id"].rsplit("/", 1)[1]
    slug = name.rsplit("-", 1)[0]
    if slug in residents_follow and slug in new_pose:
        (ox, _, oz), oyaw = residents_follow[slug]
        tx, _, tz = new_pose[slug]["position"]; tyaw = new_pose[slug]["yawDegrees"]
    elif name in RESIDENT_ANCHORS:
        (ox, _, oz), oyaw, (tx, tz, tyaw) = furniture_moves[RESIDENT_ANCHORS[name]]
    else:
        continue
    if (ox, oz) == (tx, tz) and oyaw == tyaw: continue
    dyaw = tyaw - oyaw

    def move(p):
        dx, dz = rot(p[0] - ox, p[2] - oz, dyaw)
        return [round(tx + dx, 2), p[1], round(tz + dz, 2)]
    r["params"]["position"] = move(r["params"]["position"])
    r["params"]["yawDegrees"] = round(r["params"]["yawDegrees"] + dyaw, 1)
    for block in r["params"].get("schedule", []):
        block["place"] = move(block["place"]); block["yawDegrees"] = round(block["yawDegrees"] + dyaw, 1)
    moved += 1
RESIDENTS.write_text(json.dumps(residents, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


# --- self-checks: parcels against each other, the roads and the kept owners ---------------------
def corners(x, z, yaw, hx, hz):
    return [(x + dx, z + dz) for dx, dz in (rot(sx * hx, sz * hz, yaw) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)))]


def overlap(a, b):
    for poly in (a, b):
        for i in range(4):
            (ax, az), (bx, bz) = poly[i], poly[(i + 1) % 4]
            nx, nz = bz - az, -(bx - ax)
            pa = [nx * x + nz * z for x, z in a]; pb = [nx * x + nz * z for x, z in b]
            if max(pa) <= min(pb) + 1e-6 or max(pb) <= min(pa) + 1e-6:
                return False
    return True


def box(x0, x1, z0, z1):
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


parcels = {h["id"]: corners(*h["parcel"]) for h in households if "parcel" in h}
problems = []
names = sorted(parcels)
for i, a in enumerate(names):
    for b in names[i + 1:]:
        if overlap(parcels[a], parcels[b]): problems.append(f"parcels overlap: {a} {b}")
bands = [("tukay", MAIN, 2.25), ("urman", BRIDGE_ROAD, 1.9)] + [(ROADS[n], pts, ROAD_WIDTH / 2) for n, pts in roads.items()]
for aid, poly in parcels.items():
    for street, pts, half in bands:
        for (ax, az), (bx, bz) in zip(pts, pts[1:]):
            L = math.hypot(bx - ax, bz - az); yaw = math.degrees(math.atan2(bx - ax, bz - az))
            road = corners((ax + bx) / 2, (az + bz) / 2, yaw, half + .3, L / 2)
            if overlap(poly, road): problems.append(f"parcel on road: {aid} {street}")
kept = {"babai": box(-48.5, -4.5, -14.0, 12.0), "fap": box(20.4, 39.5, -40.5, -26.0), "tamara": box(8.0, 14.5, -48.2, -41.0),
        "plaza": box(-28, -3, 33, 63), "zirat": box(-40, 36, -90, -65), "mosque": box(-47.5, -26.5, 15.5, 32.5)}
for name, b in {"school": (-32.4, -2.6, 63.6, 78.6), "dk": (-44.9, -23.0, 38.6, 57.4), "office": (4.6, 17.0, 43.9, 60.1),
                "post": (-20.4, -10.6, 23.1, 31.8), "pavilion": (-5.75, -3.4, 17.5, 22.5)}.items():
    kept[name] = box(*b)   # measured bounds (Act1LayoutInventory bld lines), pavilion at (-4.4, 20)
for h in households:
    if h["kind"] == "kit":
        t = h["to"]; kept[h["id"]] = corners(t["x"], t["z"], t["yaw"], t["w"] / 2 - .3, t["d"] / 2 - .3)
for aid, poly in parcels.items():
    for name, other in kept.items():
        if overlap(poly, other): problems.append(f"parcel on {name}: {aid}")
for h in households:
    if h["kind"] == "parcel" and h["id"] not in NS_HOUSEHOLDS and house_kit_row(h["plan"]) not in retired \
            and not kit_by_anchor[house_kit_row(h["plan"])]["params"].get("retired"):
        problems.append(f"old kit house not retired: {h['id']} {house_kit_row(h['plan'])}")
for p in problems: print("CHECK:", p)

counts = {k: sum(1 for h in households if h["kind"] == k) for k in ("parcel", "kit", "keep")}
print(f"open part: {counts['parcel']} parcels, {counts['kit']} kit moves, {counts['keep']} kept; {len(out_roads)} roads; "
      f"retired kit rows {len(retired)}; residents moved {moved}; problems {len(problems)}")
