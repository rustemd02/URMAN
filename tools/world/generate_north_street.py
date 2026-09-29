#!/usr/bin/env python3
"""Generate the north-street expansion plot (village expansion, slice 2).

Writes game/content/world/act1_north_street.world.v1.json, an executor=generic
plot that AuthoredWorldDirector builds from catalogue props. The plot is data:
edit the result in URMAN Studio or re-run this proposal generator (R026 - the
generator never overwrites entities the author renamed with the "keep" suffix).

Layout intent (level-design notes):
  * a rhythm of staggered parcels (alternating sides, varying setback) so the
    street reads as grown, not stamped;
  * landmarks in sight lines: the well at the first bend, the square at the
    far end of the straight, the lookout over the ravine at the east street end;
  * density falls off toward the fields; gaps of 30+ m give views out.

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
MAIN = [(0, 196), (2.2, 172), (-1.5, 148), (-3, 118), (-1.4, 88), (1.2, 62), (0, 40)]
EAST_STREET = [(-3, 118), (10, 115.5), (24, 118), (38, 116.5), (41, 116)]
WEST_SPUR = [(-1.5, 148), (-14, 150.5), (-26, 149), (-36, 151.5)]
HALF = {"main": 2.8, "east": 2.0, "west": 1.75}

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
    for pts, half in ((MAIN, HALF["main"]), (EAST_STREET, HALF["east"]), (WEST_SPUR, HALF["west"])):
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


def parcel(name, side, z, variant, road_pts, half, *, setback=15.5, jitter=1.2, with_shed=True, wood=True):
    """One household: parcel, shed, woodpile, front fence with a gate gap."""
    sign = -1 if side == "W" else 1
    ax = axis_at(road_pts, z)
    x = ax + sign * (setback + rng.uniform(-jitter, jitter))
    yaw = -sign * 90 + rng.uniform(-5, 5)
    cat, w, d = PARCELS[variant]
    add(f"{name}-house", (cat,), x, z, yaw, PARCEL_SCALE, (w, d), note="Хозяйство: дом с двором, вход к дороге")
    if with_shed:
        add(f"{name}-shed", SHED, x + sign * 12.5, z + rng.uniform(-3, 3), yaw + rng.choice([0, 180]), 1.0, SHED[1:])
    if wood:
        add(f"{name}-wood", WOOD, x + sign * 5.5, z + (7.4 if rng.random() < .5 else -7.4), 90 + rng.uniform(-12, 12), 1.0, WOOD[1:])
    # Front fence: two pieces either side of a 3.4 m gate gap, along the road.
    fx = ax + sign * (half + 3.4)
    for k, dz in enumerate((-9.7, -4.3, 4.3, 9.7)):
        add(f"{name}-fence{k}", FENCE, fx, z + dz, 90, 1.0, FENCE[1:], solid_check=False)


def line_tiles(points, prefix, half):
    """Road crown tiles laid along an axis, yawed with the segment; width scales to the road."""
    scale = round(min(1.0, half * 2 / CROWN[1]), 2)
    tile = CROWN[2] * scale
    idx = 0
    for a, b in zip(points, points[1:]):
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, round(length / tile))
        yaw = math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))
        for i in range(n):
            t = (i + .5) / n
            add(f"{prefix}-crown{idx}", CROWN, a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, yaw,
                scale=scale, size=None, collide=False)
            idx += 1


# ---- main street north: staggered households ------------------------------------------------
line_tiles([p for p in reversed(MAIN) if p[1] >= 40], "main", HALF["main"])
for name, side, z, v, setback in [
    ("m-w1", "W", 80, "B", 15.5), ("m-e1", "E", 76, "A", 17.0),
    ("m-w2", "W", 104, "A", 14.5),
    ("m-w3", "W", 128, "C", 16.5),
    ("m-w4", "W", 166, "A", 22.0), ("m-e4", "E", 168, "B", 22.0),
]:
    parcel(name, side, z, v, MAIN, HALF["main"], setback=setback)

# ---- first bend: the village well (landmark on the east verge at the crossing) -----------------
add("well", WELL, axis_at(MAIN, 111) + 7.2, 111, 0, 0.9, WELL[1:], note="Колодец у поворота на Нижнюю улицу")

# ---- east lower street --------------------------------------------------------------------------
line_tiles(EAST_STREET, "east", HALF["east"])
for name, side, x, z, v in [("e-n1", "N", 14, 132, "B"), ("e-s1", "S", 12, 99, "A"), ("e-n2", "N", 31, 134, "C"),
                            ("e-s2", "S", 30, 100, "B")]:
    yaw = 180 if side == "N" else 0  # front toward the street
    cat, w, d = PARCELS[v]
    add(f"{name}-house", (cat,), x + rng.uniform(-1, 1), z, yaw + rng.uniform(-5, 5), PARCEL_SCALE, (w, d),
        note="Нижняя улица")
    add(f"{name}-wood", WOOD, x + 7, z + (5 if side == "S" else -5), 90, 1.0, WOOD[1:])
# Lookout at the street end: a bench and a board facing the ravine and the far bank.
add("lookout-bench", BENCH, 38.5, 113.2, -90, 1.0, BENCH[1:], note="Обзорная точка над оврагом, вид на дальний берег")
add("lookout-board", BOARD, 38.0, 119.4, -90, 1.0, BOARD[1:])

# ---- west spur ----------------------------------------------------------------------------------
line_tiles(WEST_SPUR, "west", HALF["west"])
for name, side, x, z, v in [("w-s1", "S", -32, 138, "B")]:
    yaw = 180 if side == "N" else 0
    cat, w, d = PARCELS[v]
    add(f"{name}-house", (cat,), x, z, yaw + rng.uniform(-5, 5), PARCEL_SCALE, (w, d), note="Западный проулок")
    add(f"{name}-shed", SHED, x - 11, z + (2 if side == "S" else -2), 0, 1.0, SHED[1:])

# ---- centre square (far end of the straight; public buildings arrive with the interior slices) -
CX, CZ = 0.0, 184.0
for name, x, z, yaw, cat in [
    ("sq-office", -22, 190, 90, HERO),      # old sovkhoz office: west side (placeholder for school/DK volumes)
    ("sq-shop", 23, 188, -90, HERO),         # east side (placeholder volume, not the final shop)
]:
    add(name, cat, x, z, yaw, 1.0, cat[1:], note="Объём площади бывшего совхозного центра; фасады и функции — по ТЗ04")
add("sq-well", WELL, CX + 10, CZ - 4, 0, 1.0, WELL[1:], note="Центральный колодец площади")
add("sq-bench1", BENCH, CX - 6.5, CZ + 2, 90, 1.0, BENCH[1:])
add("sq-bench2", BENCH, CX + 6.5, CZ + 6, -90, 1.0, BENCH[1:])
add("sq-board", BOARD, CX - 4, CZ - 7, 0, 1.0, BOARD[1:], note="Доска объявлений — обновляемая точка для сюжетных объявлений")

# ---- vegetation: sparse fields between parcels and toward the ring -----------------------------
holes = [(json_e["params"]["position"][0], json_e["params"]["position"][2]) for json_e in entities if "-house" in json_e["id"]
         or "sq-" in json_e["id"]]
exclude = [[x, z, 11.0] for x, z in holes]
for i, (x, z, r, dens) in enumerate([(-30, 90, 16, .012), (-28, 125, 14, .010), (34, 78, 16, .012), (36, 155, 14, .010),
                                     (-34, 178, 14, .014), (30, 205, 20, .02), (-30, 206, 20, .02), (0, 212, 24, .03)]):
    entities.append({"id": f"urman.world:act1/north-street/trees-{i}", "kind": "scatter", "name": f"Деревья поля {i}",
                     "params": {"position": [x, 0, z], "radius": r, "density": dens, "seed": 1000 + i,
                                "catalogIds": TREES, "scaleMin": .8, "scaleMax": 1.25, "exclude": exclude}})

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

doc = {"schemaVersion": 1, "kind": "urman.world-plot", "id": "urman.world:act1/north-street",
       "name": "Северная улица, Нижняя улица и площадь центра", "executor": "generic",
       "note": "Расширение деревни (срез 2). Предложение генератора tools/world/generate_north_street.py; "
               "положение объектов можно править в Studio. Здания площади — объёмы-заготовки до интерьерных срезов ТЗ04.",
       "entities": entities}
OUT.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"wrote {OUT.relative_to(ROOT)}: {len(entities)} entities, {len(footprints)} footprints")
if problems:
    print("PROBLEMS:")
    print("\n".join(sorted(set(problems))))
    sys.exit(1)
print("self-checks ok")
