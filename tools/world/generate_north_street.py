#!/usr/bin/env python3
"""Generate the north-street expansion plot (village expansion, slice 2).

Writes game/content/world/act1_north_street.world.v1.json, an executor=generic
plot that AuthoredWorldDirector builds from catalogue props. The plot is data:
edit the result in URMAN Studio or re-run this proposal generator (R026 - the
generator never overwrites entities the author renamed with the "keep" suffix).

Layout intent (level-design notes):
  * a rhythm of staggered parcels (alternating sides, varying setback) so the
    street reads as grown, not stamped;
  * landmarks in sight lines: the well at the first bend, the central civic square between
    inhabited quarters, the lookout over the ravine at the east street end;
  * household lanes continue behind the civic buildings to the forest edge.

Also runs geometric self-checks (footprint overlaps, road clearance, ravine
margin, overlap with the existing kit plot) and exits non-zero on a violation.
"""
from __future__ import annotations

import json
import math
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/content/world/act1_north_street.world.v1.json"
KIT = ROOT / "game/content/world/act1_village_kit.world.v1.json"
CATALOG = ROOT / "game/content/world/catalog.v1.json"

# Keep in sync with AgentBAct1HeightField / AgentBAct1Layout.
MAIN = [(-23, 218), (-23, 195), (-23, 175), (-23, 158), (-23, 143), (-20, 128), (-11,121), (-8, 114), (0.5, 105.5), (-1.4, 88), (-0.1, 75), (1.2, 62), (0, 40)]
EAST_STREET = [(-0.3, 75), (12, 73.5), (25, 75), (39, 74), (41, 74)]
WEST_SPUR = [(-23, 175), (-30, 178), (-40, 177), (-44, 178)]
RING_C, RING_R = (0.5, 114.0), 8.5
RING = [(round(RING_C[0] + RING_R * math.sin(math.radians(a)), 2), round(RING_C[1] - RING_R * math.cos(math.radians(a)), 2))
        for a in range(0, 361, 30)]
HALF = {"main": 2.8, "east": 2.0, "west": 1.75, "ring": 2.4}

V = "urman.catalog:urman_village_exterior_kit/"
PARCELS = {
    "A": (V + "villageparcel-varianta-timbergable", 13.14, 13.33),
    "B": (V + "villageparcel-variantb-plasterannex", 12.67, 13.86),
    "C": (V + "villageparcel-variantc-banyayard", 13.31, 12.59),
}
SHED = (V + "outbuildingshed-low", 4.14, 2.88)
WOOD = (V + "woodpile-stackedlogs", 1.78, 0.96)
WELL = (V + "well-yardlandmark", 2.04, 1.75)
FENCE = (V + "fencesegment-roughpicket", 5.4, 0.37)
HERO = (V + "herohouse-timberplaster", 11.23, 9.24)
BENCH = ("urman.catalog:urman_fap_clinic_kit/fapbench", 2.1, 0.58)
BOARD = ("urman.catalog:urman_fap_clinic_kit/fapnoticeboard", 2.34, 0.63)
CROWN = ("urman.catalog:urman_wet_village_road_kit/roadcrown-approachworn", 5.89, 14.0)
TREES = ["urman.catalog:agentb_foliage_kit/winterbirch-1", "urman.catalog:agentb_foliage_kit/winterbirch-2",
         "urman.catalog:agentb_foliage_kit/winterlinden-1", "urman.catalog:agentb_foliage_kit/winterrowan-1",
         "urman.catalog:agentb_foliage_kit/winterspruce-2"]

PARCEL_SCALE = 0.9


def axis_at(points, z):
    """Centre x of a north-south axis at z (points run in any order)."""
    pts = sorted(points, key=lambda p: p[1])
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            return x0 + (x1 - x0) * t
    return pts[0][0] if z < pts[0][1] else pts[-1][0]


def seg_dist(px, pz, ax, az, bx, bz):
    dx, dz = bx - ax, bz - az
    t = 0 if dx == dz == 0 else max(0, min(1, ((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz)))
    return math.hypot(px - (ax + dx * t), pz - (az + dz * t))


def road_gap(px, pz):
    best = 1e9
    for pts, half in ((MAIN, HALF["main"]), (EAST_STREET, HALF["east"]), (WEST_SPUR, HALF["west"]), (RING, HALF["ring"]), (NORTH_EAST, 1.75), (WEST_SERVICE, 1.75), (NORTH_CROSS, 2.0), (NORTH_RETURN, 2.4)):
        for a, b in zip(pts, pts[1:]):
            best = min(best, seg_dist(px, pz, *a, *b) - half)
    return best


def ravine_centre(z):
    return 50.5 + 1.6 * math.sin(z / 11) + 0.7 * math.sin(z / 4.1)


def rect_corners(cx, cz, w, d, yaw):
    """Corners of a w (local X) by d (local Z) box rotated by yaw degrees."""
    s, c = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))
    out = []
    for lx, lz in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)):
        out.append((cx + lx * c + lz * s, cz - lx * s + lz * c))
    return out


def sat_overlap(a, b):
    for poly in (a, b):
        for i in range(4):
            x0, z0 = poly[i]
            x1, z1 = poly[(i + 1) % 4]
            nx, nz = z1 - z0, -(x1 - x0)
            pa = [nx * x + nz * z for x, z in a]
            pb = [nx * x + nz * z for x, z in b]
            if max(pa) <= min(pb) or max(pb) <= min(pa):
                return False
    return True


entities: list[dict] = []
footprints: list[tuple[str, list[tuple[float, float]]]] = []
rng = random.Random(0x4E53)  # "NS"


def add(name, cat, x, z, yaw, scale=1.0, size=None, note=None, collide=True, solid_check=True, kind="prop"):
    eid = f"urman.world:act1/north-street/{name}"
    params = {"catalogId": cat[0] if isinstance(cat, tuple) else cat, "position": [round(x, 2), 0, round(z, 2)],
              "yawDegrees": round(yaw, 1)}
    if scale != 1.0:
        params["scale"] = scale
    if not collide:
        params["collision"] = "none"
    e = {"id": eid, "kind": kind, "name": name, "params": params}
    if note:
        e["note"] = note
    entities.append(e)
    if size and solid_check:
        footprints.append((name, rect_corners(x, z, size[0] * scale, size[1] * scale, yaw)))


def parcel(name, side, z, variant, road_pts, half, *, setback=15.5, jitter=0, with_shed=True, wood=True, fence=True):
    """One household: parcel, shed, woodpile, front fence with a gate gap."""
    sign = -1 if side == "W" else 1
    ax = axis_at(road_pts, z)
    x = ax + sign * (setback + rng.uniform(-jitter, jitter))
    yaw = -sign * 90
    cat, w, d = PARCELS[variant]
    add(f"{name}-house", (cat,), x, z, yaw, PARCEL_SCALE, (w, d), note="Хозяйство: дом с двором, вход к дороге")
    if with_shed:
        add(f"{name}-shed", SHED, x + sign * 12.5, z, yaw, 1.0, SHED[1:])
    if wood:
        add(f"{name}-wood", WOOD, x + sign * 9, z + (3 if rng.random() < .5 else -3), 90, 1.0, WOOD[1:])
    # Front fence: two pieces either side of a 3.4 m gate gap, along the road.
    fx = ax + sign * (half + 3.4)
    for k, dz in enumerate((-3.6, 3.6) if fence else ()):
        add(f"{name}-fence{k}", FENCE, fx, z + dz, 90, .7, FENCE[1:], solid_check=False)


def line_tiles(points, prefix, half):
    """Road crown tiles laid along an axis, yawed with the segment; width scales to the road."""
    # Continuous ribbons are built from the shared street axes in runtime.
    # Source road-crown props were rectangular mud plates floating on winter snow.
    return


# ---- inhabited quarters, manually authored parcel schedule ------------------------------
# The civic square divides the quarters; houses continue behind it. No random
# household position/rotation: each entrance has an explicit street frontage.
line_tiles(list(reversed(MAIN)), "main", HALF["main"])
for name, side, z, variant, setback in [
    ("m-w1", "W", 80, "B", 16), ("m-e1", "E", 91, "A", 17),

    ("m-w3", "W", 163, "C", 19), ("m-e3", "E", 165, "B", 23),
    ("m-e3b", "E", 177, "C", 18),
    ("m-w4", "W", 190, "A", 17), ("m-e4", "E", 190, "B", 18),
    ("m-w4b", "W", 203.5, "B", 17), ("m-e4b", "E", 203.5, "C", 18),
    ("m-w5", "W", 217, "C", 17), ("m-e5", "E", 217, "A", 18),
]:
    parcel(name, side, z, variant, MAIN, HALF["main"], setback=setback, jitter=0, with_shed=name != "m-e1")

line_tiles(EAST_STREET, "east", HALF["east"])
line_tiles(WEST_SPUR, "west", HALF["west"])
# Side-street households: full parcels with a woodpile and a real back shed.
for name, x, z, yaw, variant in [
    ("e-s1", 35, 60, 0, "A"), ("e-n1", 34, 90, 180, "C"),
    ("w-n1", -54, 125, 90, "B"), ("w-n2", -54, 91, 90, "A"),
    ("n-e1", 34, 163, -90, "C"), ("n-e1b", 34, 176, -90, "B"), ("n-e2", 34, 188, -90, "A"),
    ("n-e2b", 34, 201, -90, "C"), ("n-e3", 34, 214, -90, "B"),
]:
    cat, w, d = PARCELS[variant]
    add(f"{name}-house", (cat,), x, z, yaw, PARCEL_SCALE, (w, d),
        note="Жилой двор: ворота к улице, хозяйственное использование, зимняя тропа")
    back_z = z if yaw == -90 else z + (11 if yaw == 180 else -12)
    back_x = x + 7.6 if yaw == -90 else x
    # East-lane parcels already contain their own shed/banya. A second shed
    # would consume the pedestrian bank setback; retain the kit outbuilding.
    if yaw != -90:
        add(f"{name}-shed", SHED, back_x, back_z, yaw, .9, SHED[1:])
    add(f"{name}-wood", WOOD, x + 6.7 if yaw == -90 else x - (0 if yaw == 90 else 5.3),
        z + (5.2 if yaw == -90 else 8.2), 90, .9, WOOD[1:])

# East residential lane reaches the three rows beyond the civic buildings.
NORTH_EAST = [(12.5,151), (20,155), (20,180), (20,207), (20,229)]
WEST_SERVICE = [(-42,80),(-41,110),(-43,135),(-39,146),(-30,152),(-23,158)]
NORTH_CROSS = [(-23,151),(-16,150),(0,150),(12.5,151)]
NORTH_RETURN = [(-23,218),(-23,220.5),(-22.5,223),(-21,225),(-18,227),(15,227),(18,225),(20,223),(20,220)]
line_tiles(WEST_SERVICE,"west-service",1.75)
line_tiles(NORTH_EAST,"north-east",1.75)
add("well", WELL, 7.2, 82.5, 0, .9, WELL[1:], note="Колодец у Нижней улицы")
add("lookout-bench", BENCH, 38.5, 68.5, 90, 1, BENCH[1:], note="Обзор оврага: скамья смотрит на овраг, спинка с деревенской стороны")
add("lookout-board", BOARD, 38, 79, -90, 1, BOARD[1:])

# Square furnishings and building clearances use the same plan as C#.
SQUARE_BUILDING_CLEARANCE = [(-12,138,10),(-2,138,10),(8,138,10),(-26,111,13),
                             (25,112,10),(-32.5,140,8),(-9,96,4)]
add("sq-well",WELL,12.5,122,0,1,WELL[1:],note="Общий колодец площади")
add("sq-bench1",BENCH,-12,108,90,1,BENCH[1:])
add("sq-bench2",BENCH,12.5,118,-90,1,BENCH[1:])
add("sq-board",BOARD,-10.5,102,0,1,BOARD[1:],note="Объявления у входа на площадь")

# ---- vegetation: sparse fields between parcels and toward the ring -----------------------------
holes = [(json_e["params"]["position"][0], json_e["params"]["position"][2]) for json_e in entities if "-house" in json_e["id"]
         or "sq-" in json_e["id"] or "-shed" in json_e["id"]]
exclude = [[x, z, 11.0] for x, z in holes] + [[x, z, r] for x, z, r in SQUARE_BUILDING_CLEARANCE]
for i, (x, z, r, dens) in enumerate([(-52, 100, 9, .012), (-52, 145, 8, .010), (41, 140, 7, .012), (44, 180, 8, .010),
                                     (-52, 218, 10, .014), (35, 230, 12, .02), (-30, 230, 12, .02), (0, 230, 12, .03)]):
    entities.append({"id": f"urman.world:act1/north-street/trees-{i}", "kind": "scatter", "name": f"Деревья поля {i}",
                     "params": {"position": [x, 0, z], "radius": r, "density": dens, "seed": 1000 + i,
                                "roadClearance": 1.8, "catalogIds": TREES, "scaleMin": .8, "scaleMax": 1.25, "exclude": exclude}})

# ---- self-checks --------------------------------------------------------------------------------
problems: list[str] = []
kit = json.loads(KIT.read_text(encoding="utf-8"))["entities"]
existing = [(e["params"]["position"][0], e["params"]["position"][2]) for e in kit
            if "Dwelling" in e["params"]["component"] or "VillageParcel" in e["params"]["component"]]
for i, (na, pa) in enumerate(footprints):
    for nb, pb in footprints[i + 1:]:
        if sat_overlap(pa, pb):
            problems.append(f"overlap {na} x {nb}")
    cx = sum(p[0] for p in pa) / 4
    cz = sum(p[1] for p in pa) / 4
    corner_gap = min(road_gap(x, z) for x, z in pa)
    if corner_gap < 1.5 and "bench" not in na and "board" not in na and "fence" not in na:
        problems.append(f"{na} corner only {corner_gap:.1f} m from a road edge")
    if cx > ravine_centre(cz) - 4.6 - 3.0 and cz > 60 and cx < 60:
        problems.append(f"{na} within 3 m of the ravine")
    for ex, ez in existing:
        if math.hypot(cx - ex, cz - ez) < 11 and "north-street" not in na:
            problems.append(f"{na} too close to existing house at {ex},{ez}")
    if not (-58 < cx < 88 and cz < 226):
        problems.append(f"{na} outside terrain window")

for owner, polygon in footprints:
    for axis, half in [(MAIN, 2.8), (EAST_STREET, 2), (WEST_SPUR, 1.75),
                       (RING, 2.4), (NORTH_EAST, 1.75), (WEST_SERVICE, 1.75), (NORTH_CROSS, 2.0), (NORTH_RETURN, 2.4)]:
        for (ax, az), (bx, bz) in zip(axis, axis[1:]):
            road = rect_corners((ax + bx) / 2, (az + bz) / 2, half * 2,
                                math.hypot(bx - ax, bz - az), math.degrees(math.atan2(bx - ax, bz - az)))
            if sat_overlap(polygon, road): problems.append(f"{owner} intersects a road band")

# Public envelopes include their real entrance apron. Test whole road bands,
# not only corners: a road crossing the middle of a long wall must fail too.
for name, x, z, width, depth, apron in [
    ("school", -2, 138, 28, 11, 1.3), ("dk", -26, 111, 18, 14, 3),
    ("office", 25, 112, 14, 9, 1.3), ("post", -32.5, 140, 9, 7, 1.3),
]:
    yaw = math.degrees(math.atan2(RING_C[0] - x, RING_C[1] - z))
    local = depth / 2 + apron / 2
    envelopes = [rect_corners(x, z, width + .3, depth + .3, yaw),
                 rect_corners(x + math.sin(math.radians(yaw)) * local,
                              z + math.cos(math.radians(yaw)) * local,
                              10 if name == "dk" else 3.2, apron, yaw)]
    for envelope in envelopes:
        for owner, polygon in footprints:
            if sat_overlap(envelope, polygon): problems.append(f"{name} overlaps {owner}")
        for axis, half in [(MAIN, 2.8), (EAST_STREET, 2), (WEST_SPUR, 1.75),
                           (RING, 2.4), (NORTH_EAST, 1.75), (WEST_SERVICE, 1.75), (NORTH_CROSS, 2.0), (NORTH_RETURN, 2.4)]:
            for (ax, az), (bx, bz) in zip(axis, axis[1:]):
                road = rect_corners((ax + bx) / 2, (az + bz) / 2, half * 2,
                                    math.hypot(bx - ax, bz - az), math.degrees(math.atan2(bx - ax, bz - az)))
                if sat_overlap(envelope, road): problems.append(f"{name} intersects a road band")

house_numbers = {
    "m-w1": ("tukay", "29"), "m-e1": ("tukay", "30"),
    "m-w3": ("tukay", "31"), "m-e3": ("tukay", "32"), "m-e3b": ("tukay", "34"),
    "m-w4": ("tukay", "33"), "m-e4": ("tukay", "36"),
    "m-w4b": ("tukay", "35"), "m-e4b": ("tukay", "38"),
    "m-w5": ("tukay", "37"), "m-e5": ("tukay", "40"),
    "w-n1": ("tukay", "29А"), "w-n2": ("tukay", "29Б"),
    "e-s1": ("urman", "13"), "e-n1": ("urman", "15"),
    "n-e1": ("urman", "17"), "n-e1b": ("urman", "19"), "n-e2": ("urman", "21"),
    "n-e2b": ("urman", "23"), "n-e3": ("urman", "25"),
}
# Persist parcel IDs independently of row order; adding a neighbour must not
# renumber existing registry/save references.
house_cadastral = {
    "m-w1": "URM-Q03-P0101", "m-e1": "URM-Q03-P0102",
    "m-w3": "URM-Q03-P0103", "m-e3": "URM-Q03-P0104", "m-e3b": "URM-Q03-P0105",
    "m-w4": "URM-Q03-P0106", "m-e4": "URM-Q03-P0107",
    "m-w4b": "URM-Q03-P0108", "m-e4b": "URM-Q03-P0109",
    "m-w5": "URM-Q03-P0110", "m-e5": "URM-Q03-P0111",
    "w-n1": "URM-Q03-P0112", "w-n2": "URM-Q03-P0113",
    "e-s1": "URM-Q03-P0114", "e-n1": "URM-Q03-P0115",
    "n-e1": "URM-Q03-P0116", "n-e1b": "URM-Q03-P0117", "n-e2": "URM-Q03-P0118",
    "n-e2b": "URM-Q03-P0119", "n-e3": "URM-Q03-P0120",
}
catalog_rows = {row["id"]: row for row in json.loads(CATALOG.read_text())["entities"]}
def nearest_street(x, z):
    candidates = []
    for axis in [MAIN, EAST_STREET, WEST_SPUR, NORTH_EAST, WEST_SERVICE, NORTH_CROSS, NORTH_RETURN]:
        for (ax, az), (bx, bz) in zip(axis, axis[1:]):
            dx, dz = bx - ax, bz - az
            t = max(0, min(1, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)))
            px, pz = ax + dx * t, az + dz * t
            candidates.append((math.hypot(x - px, z - pz), px, pz))
    _, px, pz = min(candidates)
    return [round(px, 3), round(pz, 3)]
for entity in entities:
    if entity["name"].endswith("-house"):
        slug = entity["name"].removesuffix("-house")
        street, number = house_numbers[slug]
        entity["params"]["collision"] = "surfaces"
        entity["params"]["address"] = {"id": "ADR-NS-" + slug.upper(), "street": street, "number": number,
                                       "cadastral": house_cadastral[slug]}
        params = entity["params"]
        size = catalog_rows[params["catalogId"]]["size"]
        params["terrainPad"] = {"halfSize": [round(size[0] * PARCEL_SCALE / 2 + .65, 3),
                                                   round(size[2] * PARCEL_SCALE / 2 + .65, 3)],
                                "streetAnchor": nearest_street(
                                    params["position"][0] + math.sin(math.radians(params["yawDegrees"])) * size[2] * PARCEL_SCALE / 2,
                                    params["position"][2] + math.cos(math.radians(params["yawDegrees"])) * size[2] * PARCEL_SCALE / 2),
                                "feather": 2.25}

doc = {"schemaVersion": 1, "kind": "urman.world-plot", "id": "urman.world:act1/north-street",
       "name": "Северная улица, Нижняя улица и площадь центра", "executor": "generic",
       "note": "Расширение деревни (срез 2). Предложение генератора tools/world/generate_north_street.py; "
               "положение объектов можно править в Studio. Центр между жилыми кварталами; школа и ДК с действующими интерьерами.",
       "entities": entities}
if problems:
    print("Layout validation failed before writing:\n"+"\n".join(sorted(set(problems))))
    sys.exit(1)
BASELINE = ROOT / "docs/production/visual_rework_2026-09-29/north_street_generated_baseline.json"
if OUT.exists() and BASELINE.exists():
    prior = {e["id"]: e for e in json.loads(BASELINE.read_text())["entities"]}
    current = {e["id"]: e for e in json.loads(OUT.read_text())["entities"]}
    merged = {e["id"]: e for e in doc["entities"]}
    for eid, entity in current.items():
        if eid not in prior or entity != prior[eid]: merged[eid] = entity
    for eid in prior.keys()-current.keys(): merged.pop(eid,None)
    proposal = dict(doc)
    doc["entities"] = list(merged.values())
    BASELINE.write_text(json.dumps(proposal,ensure_ascii=False,indent=2)+"\n")
else:
    BASELINE.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+"\n")
OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"wrote {OUT.relative_to(ROOT)}: {len(entities)} entities, {len(footprints)} footprints")
if problems:
    print("PROBLEMS:")
    print("\n".join(sorted(set(problems))))
    sys.exit(1)
print("self-checks ok")
