#!/usr/bin/env python3
"""Proposal v3: compact linear village (author requests 2026-10-01).

* Plots stand fence to fence along streets; neighbours share one fence. Every street side
  is probed metre by metre against what is already placed (roads, civic buildings, plots,
  ravine, gorge) and each free run is filled completely with plots stretched to 10-14.5 m,
  so nothing overlaps and no cavity is left along a street.
* Babai's house stands on the main street; Tamara's plot is an ordinary main-street plot;
  the FAP stands on the bridge road (Дәү урам) next to the bridge.
* The square is a paved place (Мәйдан) west of the main street, drawn on the map.
* No river: the village ends in the south at a wide deep gorge; the forest road crosses it
  on a suspension bridge that barely holds (it breaks on Aidar's way back).
* The far bank grows to ~35 households along a bending street with a fork, a loop lane and
  dead ends, plus a police point, a forestry office, a tiny dairy and a bakery.

Usage: plan_village_relayout.py <address-world-receipt.json>
Writes docs/production/village_relayout_2026-10-01/plan.json.
"""
import json, math, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/production/village_relayout_2026-10-01/plan.json"
FRONT, MIN_FRONT, MAX_FRONT = 12.0, 10.0, 14.5
DEPTH, SETBACK = 18.0, 4.0
REACH = SETBACK + DEPTH
STEP = .5

# ---------------------------------------------------------------- geometry helpers
def rect(x, z, w, d, yaw):
    """Corners of a w (along the front) x d rectangle centred at x,z; yaw 0 = front faces -z."""
    a = math.radians(yaw or 0); c, s = math.cos(a), math.sin(a)
    return [(x + c * u + s * v, z - s * u + c * v) for u, v in ((-w/2, -d/2), (w/2, -d/2), (w/2, d/2), (-w/2, d/2))]

def overlap(p, q, eps=.05):
    for poly in (p, q):
        for i in range(len(poly)):
            (x1, z1), (x2, z2) = poly[i], poly[(i + 1) % len(poly)]
            nx, nz = z2 - z1, x1 - x2; L = math.hypot(nx, nz)
            if L < 1e-9: continue
            a = [nx * x + nz * z for x, z in p]; c = [nx * x + nz * z for x, z in q]
            if max(a) <= min(c) + eps * L or max(c) <= min(a) + eps * L: return False
    return True

def ravine_x(z): return 50.5 + 1.6 * math.sin(z / 11) + .7 * math.sin(z / 4.1)
FOREST_EAST = 138.0  # new forest-ring inner edge
GORGE = (-120.0, -92.0)  # z range of the southern gorge (rim to rim)
def in_water(poly):
    for x, z in poly:
        if GORGE[0] - 1 < z < GORGE[1] + 2: return True
        if z > GORGE[1] and abs(x - ravine_x(z)) < 5.4: return True
        if x > FOREST_EAST: return True
    return False

placed = []   # (name, polygon)
def blocked(poly): return in_water(poly) or any(overlap(poly, q) for _, q in placed)
def place(name, poly): placed.append((name, poly)); return poly

ROADS = {}
def road(name, pts, width=5.0):
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, bz - az); yaw = math.degrees(math.atan2(bx - ax, bz - az))
        place("road:" + name, rect((ax + bx) / 2, (az + bz) / 2, width, L + width * .4, yaw))
    ROADS[name] = pts

def fill_run(length):
    n = max(1, round(length / FRONT))
    while length / n > MAX_FRONT: n += 1
    while n > 1 and length / n < MIN_FRONT: n -= 1
    return n, length / n

def side(name, a, b, s, depth=DEPTH, lo=0.0, hi=None, zone="open"):
    """Plots on side s (+1 left of a->b, -1 right) of the straight street a->b."""
    ax, az = a; bx, bz = b
    L = math.hypot(bx - ax, bz - az); ux, uz = (bx - ax) / L, (bz - az) / L
    nx, nz = -uz * s, ux * s
    yaw = math.degrees(math.atan2(-nx, -nz))
    hi = L if hi is None else min(hi, L)
    def centre(t): return ax + ux * t + nx * (SETBACK + depth / 2), az + uz * t + nz * (SETBACK + depth / 2)
    free, run, t = [], None, lo
    while t <= hi + 1e-6:
        ok = not blocked(rect(*centre(t), STEP * .9, depth - .2, yaw))
        if ok and run is None: run = t
        if run is not None and (not ok or t + STEP > hi):
            if t - run >= MIN_FRONT - STEP: free.append((run, t))
            run = None
        t += STEP
    out = []
    for r0, r1 in free:
        r0 += STEP / 2; r1 -= STEP / 2
        if r1 - r0 < MIN_FRONT - 1: continue
        n, w = fill_run(r1 - r0)
        for i in range(n):
            x, z = centre(r0 + (i + .5) * w)
            poly = rect(x, z, w, depth, yaw)
            if blocked(poly): continue
            place(name, poly)
            out.append(dict(street=name, x=round(x, 2), z=round(z, 2), yaw=round(yaw, 1), w=round(w, 2), d=depth))
    return out

def both(name, pts, depth_left=DEPTH, depth_right=DEPTH, start=0.0):
    out = []
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        lo = start if i == 0 else 0.0
        if depth_left: out += side(name, a, b, 1, depth_left, lo=lo)
        if depth_right: out += side(name, a, b, -1, depth_right, lo=lo)
    return out

# ---------------------------------------------------------------- open part: fixed pieces
PLAZA = dict(name="Мәйдан", x0=-28.0, x1=-3.0, z0=33.0, z1=63.0)
place("plaza", [(PLAZA["x0"], PLAZA["z0"]), (PLAZA["x1"], PLAZA["z0"]), (PLAZA["x1"], PLAZA["z1"]), (PLAZA["x0"], PLAZA["z1"])])
civic = {   # x, z, yaw, w (along front), d
    "ADR-SQUARE-DK": (-37.5, 48, 90, 18, 14), "ADR-SQUARE-SCHOOL": (-17.5, 71.5, 180, 28, 11),
    "ADR-SQUARE-POST": (-15.5, 27, 0, 9, 7), "ADR-MOSQUE": (-38, 25, 45, 16, 16),
    "ADR-SQUARE-OFFICE": (11, 52, -90, 14, 9), "ADR-H020": (10, 37, -90, 10, 8),
}
for k, (x, z, yaw, w, d) in civic.items(): place(k, rect(x, z, w, d, yaw))
# Зират (cemetery) and its road stay where the Act I clues are: no plots south of z -64.
ZIRAT = dict(x0=-40.0, x1=36.0, z0=-90.0, z1=-65.0)
place("zirat", [(ZIRAT["x0"], ZIRAT["z0"]), (ZIRAT["x1"], ZIRAT["z0"]), (ZIRAT["x1"], ZIRAT["z1"]), (ZIRAT["x0"], ZIRAT["z1"])])
BABAI = dict(x=-26.5, z=-1.0, yaw=90, w=26.0, d=44.0)   # house at the street, garden to x -48
place("ADR-BABAI", rect(BABAI["x"], BABAI["z"], BABAI["w"], BABAI["d"], BABAI["yaw"]))
FAP = dict(x=35, z=-35, yaw=0, w=16, d=16)               # own plot on the bridge road, by the bridge
place("ADR-FAP", rect(FAP["x"], FAP["z"], FAP["w"], FAP["d"], FAP["yaw"]))

road("main", [(0, -90), (0, 150)])
road("suspension-bridge", [(0, -90), (0, -122)], width=2.4)
road("kara", [(0, -122), (1, -142)])
road("bridge-road", [(0, -24), (45, -24)])          # Дәү урам: to the FAP and the bridge
road("bridge", [(45, -24.5), (62, -25)], width=3.4)
road("arrival-east", [(0, 12), (43, 12)])
road("lane-36", [(0, -36), (-54, -36)])
for z0 in (-62, 84, 130): road(f"cross{z0}", [(-54, z0), (43, z0)])

# ---------------------------------------------------------------- open part: plots
open_slots = []
open_slots += side("tukay-main", (0, -90), (0, 150), -1)                  # east side
open_slots += side("tukay-main", (0, -90), (0, 150), 1)                   # west side
for z0 in (-62, 84, 130):
    for s_ in (1, -1):
        open_slots += side("cross-east", (0, z0), (43, z0), s_, lo=REACH)
        open_slots += side("cross-west", (0, z0), (-54, z0), s_, lo=REACH)
for s_ in (1, -1):
    open_slots += side("lane-west", (0, -36), (-54, -36), s_, lo=REACH)
    open_slots += side("bridge-road", (0, -24), (45, -24), s_, depth=12, lo=REACH)
    open_slots += side("arrival-east", (0, 12), (43, 12), s_, depth=14, lo=REACH)

# ---------------------------------------------------------------- far bank
# The terrain and the forest ring move east (x to ~140) so the far bank has two
# parallel bending streets with back-to-back yards between them, cross lanes that
# make small irregular blocks, a dead end, and a lane down to the gorge.
far_public = {   # x, z, yaw, w, d, label
    "FAR-POLICE": (84, -14, -90, 12, 9, "опорный пункт полиции"),
    "FAR-BAKERY": (88, -45, 180, 9, 7, "пекарня"),
    "FAR-DAIRY": (98, -74, 0, 16, 10, "молокозавод"),
    "FAR-FORESTRY": (94, 115, 0, 14, 10, "лесничество"),
}
GREEN = dict(x=69, z=-27, r=5)
place("green", rect(GREEN["x"], GREEN["z"], 10, 10, 0))
for k, (x, z, yaw, w, d, _) in far_public.items(): place(k, rect(x, z, w, d, yaw))
FAR_N = [(62, -25), (72, -16), (76, 4), (77.5, 34), (75.5, 62), (77, 92), (76, 120)]   # Урам за мостом
FAR_E = [(112, -66), (110, -30), (113, 6), (111, 44), (113, 80), (111, 104)]           # Тау урамы, under the rim
FAR_X1 = [(69.5, -36), (92, -36), (110, -34)]                                           # cross lane past the bakery
FAR_X2 = [(77.5, 34), (94, 38), (111.42, 36)]                                            # cross lane
FAR_X3 = [(76.57, 104), (111, 104)]                                                      # north lane to the forestry
FAR_S = [(62, -25), (75, -44), (77, -84)]                                               # down toward the gorge
FAR_X4 = [(75.9, -62), (112, -66)]                                                        # lower lane
FAR_TYK = [(112, 62), (134, 66)]                                                        # dead end into the forest rim
roads = (("yar-north", FAR_N), ("yar-east", FAR_E), ("yar-cross-1", FAR_X1), ("yar-cross-2", FAR_X2),
         ("yar-cross-3", FAR_X3), ("yar-south", FAR_S), ("yar-lower", FAR_X4), ("yar-tykryk", FAR_TYK))
for n, pts in roads: road(n, pts)
FAR_DEPTH = 14.0
far_slots = []
for n, pts in roads:
    far_slots += both(n, pts, depth_left=FAR_DEPTH, depth_right=FAR_DEPTH, start=6)

# ---------------------------------------------------------------- assignment
reg = json.load(open(pathlib.Path(sys.argv[1])))["registry"]
bmap = {x["BuildingId"]: x for x in reg["buildings"]}
houses = []
for a in reg["addresses"]:
    p = bmap[a["BuildingId"]]["Position"]
    houses.append(dict(id=a["AddressId"], role=bmap[a["BuildingId"]].get("Role"), x=round(p["X"], 1), z=round(p["Z"], 1)))
far_now = [h for h in houses if h["x"] > 48]
to_far = [h for h in houses if h["id"].startswith("ADR-NS-") and (h["z"] > 120 or h["x"] < -50)]
special = set(civic) | {"ADR-BABAI", "ADR-FAP"}
open_res = [h for h in houses if h not in far_now and h not in to_far and h["id"] not in special]
new_far = [dict(id=f"NEW-FAR-{i + 1:02d}", role="residential", x=74, z=30, new=True) for i in range(10)]
plan = []

def assign(group, slots, zone, centre, pin=None):
    if len(slots) < len(group): raise SystemExit(f"{zone}: {len(group)} households, {len(slots)} plots")
    free = sorted(slots, key=lambda s: math.hypot(s["x"] - centre[0], s["z"] - centre[1]))[:len(group)]
    chosen = list(free)
    for hid, target in (pin or {}).items():
        h = next(h for h in group if h["id"] == hid)
        s = min(free, key=lambda s: math.hypot(s["x"] - target[0], s["z"] - target[1]))
        free.remove(s); group = [g for g in group if g is not h]
        plan.append(dict(h, to=dict(s), zone=zone))
    for h in sorted(group, key=lambda h: (h["z"], h["x"])):
        s = min(free, key=lambda s: math.hypot(s["x"] - h["x"], s["z"] - h["z"]))
        free.remove(s); plan.append(dict(h, to=dict(s), zone=zone))
    return [s for s in slots if s not in chosen]

# Tamara stays on the main street where her fence meets the road (quest plot moves with her).
spare_open = assign(open_res, open_slots, "open", (0, 10), pin={"ADR-H023": (11, -44)})
spare_far = assign(far_now + to_far + new_far, far_slots, "far-bank", (90, 10))
for h in houses:
    if h["id"] in civic:
        x, z, yaw, w, d = civic[h["id"]]
        plan.append(dict(h, to=dict(x=x, z=z, yaw=yaw, street="square", w=w, d=d), zone="centre"))
    elif h["id"] == "ADR-BABAI":
        plan.append(dict(h, to=dict(BABAI, street="tukay-main"), zone="babai"))
    elif h["id"] == "ADR-FAP":
        plan.append(dict(h, to=dict(FAP, street="bridge-road"), zone="centre"))
for k, (x, z, yaw, w, d, label) in far_public.items():
    plan.append(dict(id=k, role="public", label=label, x=x, z=z, new=True,
                     to=dict(x=x, z=z, yaw=yaw, w=w, d=d, street="far"), zone="far-public"))

# ---------------------------------------------------------------- audit and shared fences
polys = [(e["id"], rect(e["to"]["x"], e["to"]["z"], e["to"]["w"], e["to"]["d"], e["to"]["yaw"])) for e in plan]
bad = [(a, b) for i, (a, p) in enumerate(polys) for b, q in polys[i + 1:] if overlap(p, q)]
bad += [(a, r) for a, p in polys for r, q in placed if r.startswith("road:") and overlap(p, q)]
bad += [(a, "water") for a, p in polys if in_water(p)]
# Fences: every plot edge except its street front. Colinear overlapping edges of
# neighbours are one fence, so lengths are merged per line before they are summed.
lines = {}
raw = 0.0
for e in plan:
    if e["zone"] not in ("open", "far-bank", "babai"): continue
    c = rect(e["to"]["x"], e["to"]["z"], e["to"]["w"], e["to"]["d"], e["to"]["yaw"])
    for i in (1, 2, 3):
        (ax, az), (bx, bz) = c[i], c[(i + 1) % 4]
        L = math.hypot(bx - ax, bz - az); ux, uz = (bx - ax) / L, (bz - az) / L
        if ux < -1e-6 or (abs(ux) < 1e-6 and uz < 0): ux, uz, ax, az, bx, bz = -ux, -uz, bx, bz, ax, az
        key = (round(math.degrees(math.atan2(uz, ux)), 1) % 180, round(-uz * ax + ux * az, 1))
        t0, t1 = ux * ax + uz * az, ux * bx + uz * bz
        lines.setdefault(key, []).append((min(t0, t1), max(t0, t1))); raw += L
fence = 0.0
for spans in lines.values():
    spans.sort(); cur = None
    for a_, b_ in spans:
        if cur and a_ <= cur[1] + .05: cur = (cur[0], max(cur[1], b_))
        else:
            if cur: fence += cur[1] - cur[0]
            cur = (a_, b_)
    if cur: fence += cur[1] - cur[0]
segs = {"plotEdgesM": round(raw), "fenceBuiltM": round(fence), "sharedM": round(raw - fence)}
shared = segs["sharedM"]
OUT.write_text(json.dumps(dict(roads=ROADS, zirat=ZIRAT, plaza=PLAZA, green=GREEN, gorge=GORGE, plan=plan, overlaps=bad,
                               spareOpen=spare_open, spareFar=spare_far,
                               fences=segs), ensure_ascii=False, indent=1))
print("houses", len(houses), "| open", len(open_res), "of", len(open_slots), "| far", len(far_now + to_far + new_far), "of", len(far_slots),
      "| overlaps", len(bad), "| fences", segs)
for a, b in bad[:20]: print("  ", a, b)
