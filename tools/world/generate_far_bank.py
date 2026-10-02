#!/usr/bin/env python3
"""Generate the far-bank quarter (Заречье) from the approved relayout plan v3.

Input:  docs/production/village_relayout_2026-10-01/plan.json (tools/world/plan_village_relayout.py)
Output: game/content/world/act1_far_bank.world.v1.json      - executor=generic plot: households,
        plus top-level "roads" and "publicBuildings" read by the terrain and Act1ConnectedWorld.
        game/content/world/act1_north_street_residents.world.v1.json - residents of the households
        that move here follow their yard (rigid transform from the old to the new pose).
        game/content/urman.settlement.addresses.v1.json          - the nine old far-bank rows leave the
        manifest: those households are generic parcels now and carry their address in the plot.
        game/content/world/act1_village_kit.world.v1.json        - their kit placements leave too.

Address ids never change. House numbers follow the new streets (odd on the left).
"""
from __future__ import annotations

import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "docs/production/village_relayout_2026-10-01/plan.json"
OUT = ROOT / "game/content/world/act1_far_bank.world.v1.json"
NORTH = ROOT / "game/content/world/act1_north_street.world.v1.json"
RESIDENTS = ROOT / "game/content/world/act1_north_street_residents.world.v1.json"
MANIFEST = ROOT / "game/content/urman.settlement.addresses.v1.json"
KIT = ROOT / "game/content/world/act1_village_kit.world.v1.json"

V = "urman.catalog:urman_village_exterior_kit/"
PARCELS = {  # catalog id, width, depth (unscaled, front faces local +Z)
    "A": (V + "villageparcel-varianta-timbergable", 13.14, 13.33),
    "B": (V + "villageparcel-variantb-plasterannex", 12.67, 13.86),
    "C": (V + "villageparcel-variantc-banyayard", 13.31, 12.59),
}
SCALE = 0.9
WELL = V + "well-yardlandmark"
STREET_OF = {"yar-north": "yar", "yar-south": "yar", "yar-cross-1": "yar", "yar-cross-2": "yar",
             "yar-east": "tau", "yar-cross-3": "tau", "yar-tykryk": "tau", "yar-lower": "tuben"}
NEW_STREETS = [  # address street records added to the manifest
    {"id": "tau", "tatar": "Тау ур.", "russian": "ул. Нагорная", "origin": [112.0, 0.0, -66.0], "oddSide": "left", "protected": False},
    {"id": "tuben", "tatar": "Түбән ур.", "russian": "ул. Нижняя", "origin": [76.0, 0.0, -62.0], "oddSide": "left", "protected": False},
]
OLD_FAR = {  # address id -> kit/source name of the presentation-only house it replaces
    "ADR-H033": "FapEastViewHouse", "ADR-H034": "FapEastHorizonAuthoredHouse", "ADR-H039": "MainStreetEastFarParcelHouse",
    "ADR-H048": "FapRightFieldHouse", "ADR-H050": "FapEastFieldViewHouse", "ADR-H069": "FarBankNorthHouse",
    "ADR-H070": "FarBankSouthHouse", "ADR-H071": "FarBankMidHouse", "ADR-H072": "FarBankEndHouse",
}

plan = json.loads(PLAN.read_text(encoding="utf-8"))
manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
north = json.loads(NORTH.read_text(encoding="utf-8"))
roads = plan["roads"]


def along(points, x, z):
    """(distance along the polyline, side +1 left/-1 right, nearest point) of x,z."""
    best, acc, result = 1e18, 0.0, None
    for (ax, az), (bx, bz) in zip(points, points[1:]):
        dx, dz = bx - ax, bz - az
        L = math.hypot(dx, dz)
        t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / (L * L)))
        px, pz = ax + dx * t, az + dz * t
        d = math.hypot(x - px, z - pz)
        if d < best:
            cross = dx * (z - az) - dz * (x - ax)
            best, result = d, (acc + t * L, 1 if cross > 0 else -1, (px, pz))
        acc += L
    return result


# --- who lives here ---------------------------------------------------------------------------
far = [e for e in plan["plan"] if e["zone"] == "far-bank"]
north_by_slug = {e["id"].rsplit("/", 1)[1]: e for e in north["entities"]}
ns_address = {e["params"]["address"]["id"]: (slug.removesuffix("-house"), e) for slug, e in north_by_slug.items()
              if "address" in e["params"]}
manifest_rows = {row["addressId"]: row for row in manifest["buildings"]}
# A re-run keeps what an earlier run resolved (catalog variant, cadastral) for households whose
# original source (north-street plot, manifest row) has already been retired.
previous = {}
if OUT.exists():
    for e in json.loads(OUT.read_text(encoding="utf-8"))["entities"]:
        if "address" in e["params"]: previous[e["params"]["address"]["id"]] = e["params"]

new_ids = iter(f"ADR-H{n:03d}" for n in range(74, 200))
households = []
for e in far:
    aid = e["id"]
    if aid.startswith("NEW-FAR-"):
        aid = next(new_ids)
    households.append(dict(plan=e, address=aid))

# numbering: per address street, ordered by distance along its street (odd left, even right)
groups: dict[str, list] = {}
for h in households:
    t = h["plan"]["to"]
    street = STREET_OF[t["street"]]
    pts = roads[t["street"]]
    dist, side, anchor = along(pts, t["x"], t["z"])
    h.update(street=street, dist=dist + 1000 * list(STREET_OF).index(t["street"]), side=side, anchor=anchor)
    groups.setdefault(street, []).append(h)
for street, hs in groups.items():
    odd, even = 1, 2
    for h in sorted(hs, key=lambda h: h["dist"]):
        if h["side"] > 0: h["number"], odd = str(odd), odd + 2
        else: h["number"], even = str(even), even + 2

# --- plot entities ----------------------------------------------------------------------------
entities = []
lots = []
variants = "ABCBACCAB"
moved_residents = {}
for i, h in enumerate(sorted(households, key=lambda h: h["address"])):
    t = h["plan"]["to"]
    aid = h["address"]
    slug = aid[4:].lower()
    old_ns = ns_address.get(aid)
    res_key = None
    if old_ns:
        slug_ns, source = old_ns
        cat = source["params"]["catalogId"]
        cadastral = source["params"]["address"]["cadastral"]
        res_key = slug_ns
        moved_residents[slug_ns] = (source["params"]["position"], source["params"]["yawDegrees"], None)
    elif aid in previous:
        cat, cadastral = previous[aid]["catalogId"], previous[aid]["address"]["cadastral"]
        # Its residents (if any) follow from the pose of the previous run.
        res_key = slug.removeprefix("ns-")
        moved_residents[res_key] = (previous[aid]["position"], previous[aid]["yawDegrees"], None)
    else:
        cat = PARCELS[variants[i % len(variants)]][0]
        cadastral = manifest_rows[aid]["cadastral"] if aid in manifest_rows else f"URM-Q04-P{100 + i:04d}"
    w, d = next((pw, pd) for c, pw, pd in PARCELS.values() if c == cat)
    # Fit the parcel to its lot (as the open part does): never wider than the lot, front fence
    # a short verge from the street edge, garden behind.
    scale = round(min(SCALE, (t["w"] - .4) / w), 3)
    fx, fz = math.sin(math.radians(t["yaw"])), math.cos(math.radians(t["yaw"]))
    shift = t["d"] / 2 - max(.6, min(1.6, t["d"] / 2 - d * scale / 2)) - d * scale / 2
    px, pz = round(t["x"] + fx * shift, 3), round(t["z"] + fz * shift, 3)
    if res_key:
        moved_residents[res_key] = (moved_residents[res_key][0], moved_residents[res_key][1], {"x": px, "z": pz, "yaw": t["yaw"]})
    lots.append({"id": aid, "kind": "parcel", "position": [t["x"], t["z"]], "yawDegrees": round(t["yaw"], 1),
                 "size": [t["w"], t["d"]], "street": h["street"], "number": h["number"]})
    entities.append({
        "id": f"urman.world:act1/far-bank/{slug}-house", "kind": "prop", "name": f"{slug}-house",
        "params": {"catalogId": cat, "position": [px, 0, pz], "yawDegrees": round(t["yaw"], 1), "scale": scale,
                   "collision": "surfaces",
                   "address": {"id": aid, "street": h["street"], "number": h["number"], "cadastral": cadastral},
                   "terrainPad": {"halfSize": [round(w * scale / 2 + .65, 3), round(d * scale / 2 + .65, 3)],
                                  "streetAnchor": [round(h["anchor"][0], 3), round(h["anchor"][1], 3)], "feather": 2.25}},
        "note": "Заречье: хозяйство, ворота к своей улице"})
g = plan["green"]
entities.append({"id": "urman.world:act1/far-bank/green-well", "kind": "prop", "name": "green-well",
                 "params": {"catalogId": WELL, "position": [g["x"], 0, g["z"]], "yawDegrees": 0, "scale": 1.0},
                 "note": "Колодец на лужайке у моста"})

public = []
for e in plan["plan"]:
    if e["zone"] != "far-public": continue
    t = e["to"]
    public.append({"id": e["id"].lower(), "label": e["label"], "position": [t["x"], 0, t["z"]], "yawDegrees": t["yaw"],
                   "size": [t["w"], t["d"]]})
far_roads = [{"id": name, "street": STREET_OF.get(name, "yar"), "points": [[round(x, 2), round(z, 2)] for x, z in pts],
              "width": 3.6 if "cross" in name or "tykryk" in name else 4.2}
             for name, pts in roads.items() if name.startswith("yar-")]
doc = {"schemaVersion": 1, "kind": "urman.world-plot", "id": "urman.world:act1/far-bank", "name": "Заречье",
       "executor": "generic",
       "note": "Заречье за оврагом: две изгибающиеся улицы с задами двор к двору, переулки, тупик и спуск к оврагу. "
               "Генератор tools/world/generate_far_bank.py по утверждённой схеме v3.",
       "green": g, "roads": far_roads, "publicBuildings": public, "lots": lots, "entities": entities}
OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# --- residents follow their yard ----------------------------------------------------------------
residents = json.loads(RESIDENTS.read_text(encoding="utf-8"))
moved = 0
for r in residents["entities"]:
    slug = r["id"].rsplit("/", 1)[1].rsplit("-", 1)[0]
    if slug not in moved_residents: continue
    (ox, _, oz), oyaw, t = moved_residents[slug]
    dyaw = t["yaw"] - oyaw
    a = math.radians(dyaw)
    def move(p):
        dx, dz = p[0] - ox, p[2] - oz
        return [round(t["x"] + dx * math.cos(a) + dz * math.sin(a), 2), p[1], round(t["z"] - dx * math.sin(a) + dz * math.cos(a), 2)]
    r["params"]["position"] = move(r["params"]["position"])
    r["params"]["yawDegrees"] = round(r["params"]["yawDegrees"] + dyaw, 1)
    for block in r["params"].get("schedule", []):
        block["place"] = move(block["place"]); block["yawDegrees"] = round(block["yawDegrees"] + dyaw, 1)
    moved += 1
RESIDENTS.write_text(json.dumps(residents, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# --- manifest and kit: the old far-bank presentation houses retire ----------------------------
manifest["buildings"] = [row for row in manifest["buildings"] if row["addressId"] not in OLD_FAR]
known = {s["id"] for s in manifest["streets"]}
manifest["streets"] += [s for s in NEW_STREETS if s["id"] not in known]
MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
kit = json.loads(KIT.read_text(encoding="utf-8"))
kit["entities"] = [e for e in kit["entities"] if e["params"]["logicalAnchor"] not in
                   {f"village-house@{name}" for name in OLD_FAR.values()}]
KIT.write_text(json.dumps(kit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

print(f"far bank: {len(households)} households ({sum(1 for h in households if h['plan']['id'].startswith('NEW-'))} new), "
      f"{len(public)} public buildings, {len(far_roads)} roads; residents moved {moved}")
