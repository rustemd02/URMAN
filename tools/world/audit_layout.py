#!/usr/bin/env python3
"""Village layout audit over the raw dump of game/tests/Act1LayoutAudit.cs.

usage: audit_layout.py layout-audit.json [out_dir]
Writes audit.md (findings by kind) and audit.png (top-down map with the findings marked).
Kinds: building-overlap, building-on-road, orphan-structure, fence-on-road, fence-through-building,
double-fence, floating, buried, access-unverified.
"""
from __future__ import annotations

import json
import math
import re
import sys
from collections import defaultdict
from pathlib import Path

src = Path(sys.argv[1])
out = Path(sys.argv[2]) if len(sys.argv) > 2 else src.parent
d = json.loads(src.read_text())
STEP = 0.5

road = {}
for x, z, c in d["road"]:
    road[(int(x), int(z))] = c


def clearance(x, z):
    return road.get((round(x), round(z)), 9.0)


def inside(poly, x, z):
    c = False
    n = len(poly)
    for i in range(n):
        x1, z1 = poly[i]; x2, z2 = poly[(i + 1) % n]
        if (z1 > z) != (z2 > z) and x < (x2 - x1) * (z - z1) / (z2 - z1 + 1e-12) + x1:
            c = not c
    return c


def cells(poly, shrink=0.0):
    xs = [p[0] for p in poly]; zs = [p[1] for p in poly]
    out = set()
    x = min(xs)
    while x <= max(xs):
        z = min(zs)
        while z <= max(zs):
            if inside(poly, x, z):
                if shrink <= 0 or all(inside(poly, x + dx, z + dz) for dx, dz in ((shrink, 0), (-shrink, 0), (0, shrink), (0, -shrink))):
                    out.add((round(x / STEP), round(z / STEP)))
            z += STEP
        x += STEP
    return out


buildings = [b for b in d["buildings"] if len(b["footprint"]) >= 3]
foot = {b["id"]: cells(b["footprint"]) for b in buildings}
core = {b["id"]: cells(b["footprint"], 0.7) for b in buildings}
cell_owner = defaultdict(list)
for bid, cs in foot.items():
    for c in cs: cell_owner[c].append(bid)

findings = []


def add(kind, x, z, text):
    findings.append((kind, x, z, text))


# 1. buildings overlapping each other
seen = set()
for c, owners in cell_owner.items():
    if len(owners) > 1:
        for i in range(len(owners)):
            for j in range(i + 1, len(owners)):
                seen.add(tuple(sorted((owners[i], owners[j]))))
byid = {b["id"]: b for b in buildings}
for a, b in sorted(seen):
    area = len(foot[a] & foot[b]) * STEP * STEP
    if area < 1.0: continue
    A, B = byid[a], byid[b]
    add("building-overlap", (A["x"] + B["x"]) / 2, (A["z"] + B["z"]) / 2,
        f"{a} ({A['address'] or A['role']}, {A['source']}) ∩ {b} ({B['address'] or B['role']}, {B['source']}): {area:.0f} м²")

# 2. buildings on the carriageway
for b in buildings:
    on = [c for c in core[b["id"]] if clearance(c[0] * STEP, c[1] * STEP) < -0.2]
    if len(on) * STEP * STEP >= 0.75:
        add("building-on-road", b["x"], b["z"], f"{b['id']} ({b['address'] or b['role']}, {b['source']}): {len(on) * STEP * STEP:.0f} м² на проезжей части")
    if b.get("astate") and b["astate"] != "verified":
        add("access-unverified", b["ax"], b["az"], f"{b['id']} ({b['address']}): вход {b['astate']}")

# 3. meshes: structures and fences
STRUCT = re.compile(r"Dwelling|House|Facade|_Wall|Roof|Gable|Shed|Outbuilding|Annex|Barn|Banya|Silhouette|Volume|Hut|Kiosk|Garage", re.I)
FENCE = re.compile(r"Fence|Picket|Palisade|Paling|Wattle|Rail|Boundary|Plank|Board", re.I)
NOT_FENCE = re.compile(r"Roof|Floor|Shelf|Sign|Plate|Notice|Window|Door|Stair|Step|Deck|Bench|Table|Bridge|Suspension|Crib|Bus|Mosque|Square|Interior|Discovery|Snow|Woodpile|Log", re.I)
groups = defaultdict(list)
fences = []
for m in d["meshes"]:
    p = m["p"]
    name = p.rsplit("/", 1)[1]
    if "Act1CoreWorldGreybox" not in p and "village-main-road" not in p: continue
    sx, sz, sy = m["x1"] - m["x0"], m["z1"] - m["z0"], m["y1"] - m["y0"]
    if FENCE.search(name) and not NOT_FENCE.search(p) and max(sx, sz) > 1.2 and min(sx, sz) < 0.45 and sy < 2.6:
        fences.append(m)
    else:
        # a structure is the container one level above its meshes; it counts as a building
        # when any of its meshes is named like one
        groups["/".join(p.split("/")[:-1])].append(m)

PUBLIC = re.compile(r"FarBankPublic|YardRepairCorner|SovkhozSquare|Mosque|Minaret|Bathhouse|FapExterior|FapClinic|PublicInterior|Shop|School|Club|Council|Office|PostOffice|OpenPart|AuthoredWorldDirector|Babai|HouseExteriorApproach|BusStop|Pavilion|WatchingForms", re.I)
for key, ms in groups.items():
    if not any(STRUCT.search(m["p"].rsplit("/", 1)[1]) for m in ms): continue
    x0 = min(m["x0"] for m in ms); x1 = max(m["x1"] for m in ms)
    z0 = min(m["z0"] for m in ms); z1 = max(m["z1"] for m in ms)
    y0 = min(m["y0"] for m in ms); y1 = max(m["y1"] for m in ms)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    if not (-60 < cx < 150 and -95 < cz < 232): continue
    g = sum(m["g"] for m in ms) / len(ms)
    big = (x1 - x0) * (z1 - z0) > 6 and y1 - y0 > 2.0
    c = (round(cx / STEP), round(cz / STEP))
    owned = any(c2 in cell_owner for c2 in [(c[0] + i, c[1] + j) for i in range(-3, 4) for j in range(-3, 4)])
    short = key.split("Act1ConnectedWorld/")[-1]
    if big and not owned and not PUBLIC.search(key):
        add("orphan-structure", cx, cz, f"{short}: {x1 - x0:.1f}×{z1 - z0:.1f}×{y1 - y0:.1f} м, ни один адрес/участок не владеет")
    if big and y0 - g > 0.35:
        add("floating", cx, cz, f"{short}: низ на {y0 - g:.2f} м над землёй")
    if big and y1 < g + 0.5:
        add("buried", cx, cz, f"{short}: почти целиком под землёй")

# 4. fences
def orient(m):
    return "x" if (m["x1"] - m["x0"]) >= (m["z1"] - m["z0"]) else "z"


for m in fences:
    cx, cz = (m["x0"] + m["x1"]) / 2, (m["z0"] + m["z1"]) / 2
    short = m["p"].split("Act1ConnectedWorld/")[-1]
    if clearance(cx, cz) < -0.4:
        add("fence-on-road", cx, cz, f"{short}: забор на проезжей части")
    c = (round(cx / STEP), round(cz / STEP))
    for bid in cell_owner.get(c, []):
        slug = byid[bid]["source"].rsplit("/", 1)[-1]
        if slug and ("/" + slug + "/") in m["p"]:
            continue   # a plot's own fence
        if c in core[bid]:
            add("fence-through-building", cx, cz, f"{short}: внутри {bid} ({byid[bid]['address']})")
            break

buckets = defaultdict(list)
for i, m in enumerate(fences):
    buckets[(round((m["x0"] + m["x1"]) / 8), round((m["z0"] + m["z1"]) / 8))].append(i)
pairs = set()
for (bx, bz), idx in buckets.items():
    near = [j for dx in (-1, 0, 1) for dz in (-1, 0, 1) for j in buckets.get((bx + dx, bz + dz), [])]
    for i in idx:
        a = fences[i]
        for j in near:
            if j <= i: continue
            b = fences[j]
            if orient(a) != orient(b): continue
            if a["p"].rsplit("/", 1)[0] == b["p"].rsplit("/", 1)[0]: continue
            if a["p"].startswith(b["p"]) or b["p"].startswith(a["p"]): continue
            if orient(a) == "x":
                gap = abs((a["z0"] + a["z1"]) / 2 - (b["z0"] + b["z1"]) / 2)
                overlap = min(a["x1"], b["x1"]) - max(a["x0"], b["x0"])
            else:
                gap = abs((a["x0"] + a["x1"]) / 2 - (b["x0"] + b["x1"]) / 2)
                overlap = min(a["z1"], b["z1"]) - max(a["z0"], b["z0"])
            if gap < 0.8 and overlap > 1.0:
                pairs.add((i, j))
# collapse pairs into containers
dup = defaultdict(list)
for i, j in pairs:
    a, b = fences[i], fences[j]
    ka = "/".join(a["p"].split("Act1ConnectedWorld/")[-1].split("/")[1:3])
    kb = "/".join(b["p"].split("Act1ConnectedWorld/")[-1].split("/")[1:3])
    if ka == kb: continue
    dup[tuple(sorted((ka, kb)))].append(((a["x0"] + a["x1"]) / 2, (a["z0"] + a["z1"]) / 2))
for (ka, kb), pts in dup.items():
    x = sum(p[0] for p in pts) / len(pts); z = sum(p[1] for p in pts) / len(pts)
    add("double-fence", x, z, f"{ka}  ×  {kb}: {len(pts)} пар секций рядом (<0.8 м)")

# report
kinds = defaultdict(list)
for f in findings: kinds[f[0]].append(f)
TITLE = {"building-overlap": "Дома наезжают друг на друга", "building-on-road": "Дом на проезжей части",
         "access-unverified": "Вход не проверен", "orphan-structure": "Постройка без адреса (мусор/старое)",
         "floating": "Висит в воздухе", "buried": "Утоплено в землю", "fence-on-road": "Забор на дороге",
         "fence-through-building": "Забор сквозь дом", "double-fence": "Двойные заборы"}
lines = ["# Аудит раскладки деревни", "", f"Зданий в реестре: {len(buildings)}; секций заборов: {len(fences)}; построек (групп мешей): {len(groups)}", ""]
for k in TITLE:
    fs = kinds.get(k, [])
    lines.append(f"## {TITLE[k]} — {len(fs)}")
    for _, x, z, t in sorted(fs, key=lambda f: (f[2], f[1])):
        lines.append(f"- ({x:.0f}, {z:.0f}) {t}")
    lines.append("")
(out / "audit.md").write_text("\n".join(lines))
print("\n".join(f"{TITLE[k]}: {len(kinds.get(k, []))}" for k in TITLE))

try:
    from PIL import Image, ImageDraw
    X0, X1, Z0, Z1, S = -64, 150, -100, 232, 4
    W, H = (X1 - X0) * S, (Z1 - Z0) * S
    img = Image.new("RGB", (W, H), (245, 245, 240)); dr = ImageDraw.Draw(img)
    P = lambda x, z: ((x - X0) * S, (Z1 - z) * S)
    for (x, z), c in road.items():
        if c < 0: dr.rectangle([*P(x - .5, z + .5), *P(x + .5, z - .5)], fill=(205, 195, 175))
    for b in buildings:
        dr.polygon([P(*p) for p in b["footprint"]], outline=(60, 90, 160), fill=(200, 215, 240))
    for m in fences:
        dr.rectangle([*P(m["x0"], m["z1"]), *P(m["x1"], m["z0"])], fill=(120, 80, 40))
    COL = {"building-overlap": (220, 0, 0), "building-on-road": (255, 120, 0), "orphan-structure": (160, 0, 160),
           "double-fence": (0, 150, 0), "fence-on-road": (255, 0, 160), "fence-through-building": (0, 160, 200),
           "floating": (0, 0, 0), "buried": (90, 90, 90), "access-unverified": (255, 200, 0)}
    for k, x, z, _ in findings:
        px, pz = P(x, z); r = 7
        dr.ellipse([px - r, pz - r, px + r, pz + r], outline=COL[k], width=3)
    img.save(out / "audit.png")
except ImportError:
    pass
