"""ACT1-VILLAGE-LAYOUT v1: scheme data + rendered plan for the author's review.

World metres, the game's axes: x east, z south (+z toward arrival, -z toward
the forest edge). Streets follow the research package profiles ST01 (16 m
between fences) and ST02 (12 m); plots follow H02 (front 22 m, palisade 3 m,
house 10 x 8 m with side seni, shed, firewood, bath, kitchen garden behind).
Canonical places stay where they are so the investigation route survives.
Plots are placed only on free ground (occupancy grid): never across a street,
the river, the ravine or a canonical place, and never on another plot.
"""
import json, math
from PIL import Image, ImageDraw, ImageFont

CANON = {  # name: (x, z, radius kept free) — v2 spreads the same chain over real distances
    "Въезд / остановка": (4, 168, 12), "Двор бабая и әби": (-24, 62, 20), "ФАП": (58, -40, 16),
    "Мечеть": (-86, 34, 18), "Магазин": (18, 96, 11), "Школа (закрыта)": (30, -8, 14),
    "Сельсовет / ДК": (-26, -12, 14), "Зират": (-24, -92, 18), "Труба через реку": (0, -128, 7),
    "Кромка Кара-Урмана": (0, -176, 12),
}
STREETS = [  # name, profile, points, accessible
    ("Тукай урамы — главная", "ST01", [(4, 176), (12, 138), (-2, 104), (6, 70), (-2, 34), (2, 4)], True),
    ("Мәчет урамы — к мечети", "ST02", [(-2, 104), (-30, 96), (-58, 70), (-78, 44)], True),
    ("Арткы тыкрык — петля", "ST02", [(-78, 44), (-70, 12), (-44, -2), (2, 4)], True),
    ("Дәү урам — к ФАПу и мосту", "ST01", [(2, 4), (34, -20), (70, -28), (104, -26)], True),
    ("Зират юлы", "ST02", [(2, 4), (-6, -50), (-4, -96), (0, -128), (0, -180)], True),
    ("Урам за мостом", "ST01", [(128, -26), (152, -10), (170, 22), (176, 60)], False),
    ("Тыкрык за мостом", "ST02", [(152, -10), (170, -44), (184, -70)], False),
]
RAVINE = [(118, 110), (114, 60), (117, 20), (114, -26), (110, -80), (96, -126)]
RIVER = [(-200, -124), (-80, -128), (0, -128), (60, -124), (96, -126), (220, -132)]
BRIDGE = ((106, -26), (126, -26))
X0, Z0, X1, Z1 = -190, -200, 230, 200
FREE, STREET, TAKEN = 0, 1, 2
grid = {}
def mark_line(pts, half, value):
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        length = math.hypot(bx - ax, bz - az)
        for i in range(int(length) + 1):
            t = i / max(length, 1); x, z = ax + (bx - ax) * t, az + (bz - az) * t
            for dx in range(-int(half) - 1, int(half) + 2):
                for dz in range(-int(half) - 1, int(half) + 2):
                    if dx * dx + dz * dz <= half * half: grid[(round(x + dx), round(z + dz))] = value
for _, profile, pts, _ in STREETS: mark_line(pts, 7.5 if profile == "ST01" else 5.5, STREET)
mark_line(RIVER, 7, STREET); mark_line(RAVINE, 7, STREET)
for _, (x, z, r) in CANON.items(): mark_line([(x, z), (x + .01, z)], r * .7, STREET)

def rect(front, n, t, w, d):
    fx, fz = front; return [(fx + t[0] * w / 2, fz + t[1] * w / 2), (fx - t[0] * w / 2, fz - t[1] * w / 2),
                            (fx - t[0] * w / 2 + n[0] * d, fz - t[1] * w / 2 + n[1] * d), (fx + t[0] * w / 2 + n[0] * d, fz + t[1] * w / 2 + n[1] * d)]
def cells(front, n, t, w, d):
    out = []
    for a in range(int(w) + 1):
        for b in range(1, int(d) + 1):
            s, q = a - w / 2, b
            out.append((round(front[0] + t[0] * s + n[0] * q), round(front[1] + t[1] * s + n[1] * q)))
    return out

plots = []
for name, profile, pts, acc in STREETS:
    if name.startswith("Зират"): continue
    half = 8.0 if profile == "ST01" else 6.0
    for (ax, az), (bx, bz) in zip(pts, pts[1:]):
        length = math.hypot(bx - ax, bz - az); t = ((bx - ax) / length, (bz - az) / length)
        for side in (1, -1):
            n = (-t[1] * side, t[0] * side)
            s = 11.0
            while s < length - 9:
                fx, fz = ax + t[0] * s + n[0] * (half + .5), az + t[1] * s + n[1] * (half + .5)
                placed = None
                for width in (22, 18):
                    for depth in (48, 40, 32, 26, 20):
                        cs = cells((fx, fz), n, t, width, depth)
                        if all(X0 < c[0] < X1 and Z0 < c[1] < Z1 and grid.get(c, FREE) == FREE for c in cs):
                            placed = (width, depth); break
                    if placed: break
                if placed and ((acc and fx > 106) or (not acc and fx < 124)): placed = None
                if placed:
                    width, depth = placed
                    for c in cells((fx, fz), n, t, width, depth): grid[c] = TAKEN
                    plots.append(dict(street=name, accessible=acc, front=(fx, fz), facing=n, along=t, depth=depth, width=width))
                s += (placed[0] + 1) if placed else 2

json.dump({"canon": {k: v[:2] for k, v in CANON.items()},
           "streets": [dict(name=a, profile=b, points=c, accessible=d) for a, b, c, d in STREETS],
           "ravine": RAVINE, "river": RIVER, "bridge": BRIDGE, "plots": plots},
          open("layout_v1.json", "w"), ensure_ascii=False, indent=1)

S = 3; W, H = (X1 - X0) * S, (Z1 - Z0) * S + 110
img = Image.new("RGB", (W, H), (236, 238, 234)); d = ImageDraw.Draw(img)
def P(x, z): return ((x - X0) * S, (z - Z0) * S)
F = lambda size, bold=False: ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial%s.ttf" % (" Bold" if bold else ""), size)
d.ellipse([P(-184, -196), P(224, 196)], outline=(38, 66, 46), width=30)
d.line([P(*p) for p in RIVER], fill=(122, 170, 200), width=9 * S)
d.line([P(*p) for p in RAVINE], fill=(105, 135, 155), width=10 * S)
for name, profile, pts, acc in STREETS:
    d.line([P(*p) for p in pts], fill=(200, 204, 208) if acc else (214, 214, 214), width=(16 if profile == "ST01" else 12) * S, joint="curve")
    d.line([P(*p) for p in pts], fill=(150, 152, 155) if acc else (185, 185, 185), width=int((4.5 if profile == "ST01" else 3.5) * S), joint="curve")
for p in plots:
    n, t, f, dep = p["facing"], p["along"], p["front"], p["depth"]
    d.polygon([P(*c) for c in rect(f, n, t, p["width"], dep)], fill=(228, 218, 196) if p["accessible"] else (218, 218, 218), outline=(160, 140, 110))
    garden_front = (f[0] + n[0] * max(dep - 18, 13), f[1] + n[1] * max(dep - 18, 13))
    d.polygon([P(*c) for c in rect(garden_front, n, t, p["width"] - 4, 16)], fill=(206, 214, 186) if p["accessible"] else (205, 205, 205))
    house_front = (f[0] + n[0] * 3 - t[0] * 3, f[1] + n[1] * 3 - t[1] * 3)
    d.polygon([P(*c) for c in rect(house_front, n, t, 10, 8)], fill=(118, 86, 58) if p["accessible"] else (150, 150, 150))
    seni = (house_front[0] + t[0] * 6.5, house_front[1] + t[1] * 6.5)
    d.polygon([P(*c) for c in rect((seni[0] + n[0] * 2, seni[1] + n[1] * 2), n, t, 3, 4)], fill=(160, 124, 84) if p["accessible"] else (170, 170, 170))
    shed = (f[0] + n[0] * 16 - t[0] * 4, f[1] + n[1] * 16 - t[1] * 4)
    if dep >= 26: d.polygon([P(*c) for c in rect(shed, n, t, 7, 6)], fill=(140, 120, 100) if p["accessible"] else (165, 165, 165))
(bx0, bz0), (bx1, bz1) = BRIDGE
d.line([P(bx0, bz0), P(bx1, bz1)], fill=(205, 55, 40), width=6 * S)
d.text(P(96, -44), "Аварийный мост — закрыт в Акте I", fill=(170, 40, 30), font=F(22, True))
d.text(P(120, 70), "Овраг с ручьём", fill=(60, 90, 110), font=F(18))
for name, (x, z, r) in CANON.items():
    d.ellipse([P(x - 2.5, z - 2.5), P(x + 2.5, z + 2.5)], fill=(30, 90, 160)); d.text(P(x + 4, z - 4), name, fill=(20, 60, 130), font=F(20, True))
for name, profile, pts, acc in STREETS:
    m = pts[1]; d.text(P(m[0] + 9, m[1]), f"{name} ({profile})", fill=(70, 70, 70), font=F(16))
d.text(P(-180, -199), "Высокий густой лес — кольцо вокруг деревни; между домами видны дальние лесистые склоны", fill=(30, 60, 40), font=F(18))
y0 = (Z1 - Z0) * S + 10
acc_n = sum(p["accessible"] for p in plots)
for i, line in enumerate([
    f"Цветные участки — доступная половина Акта I ({acc_n}); серые — вторая половина за оврагом ({len(plots) - acc_n}), видна с моста и в просветах, закрыта аварийным мостом.",
    "Участок H02: фронт 22 м, палисадник 3 м, дом 10×8 м с боковыми сенями, сарай и дрова во дворе, огород в глубине. 3 px = 1 м.",
    "Улицы: ST01 — дорога 4,5 + обочины 2×0,75 + канавы 2×1 + полосы 2×4 = 16 м между заборами; ST02 — 12 м. Главная улица изогнута: от въезда не виден противоположный край.",
    "Синие — общественные и сюжетные места: цепочка расследования сохранена (въезд → двор бабая → ФАП → мечеть → зират → река → кромка), расстояния увеличены."]):
    d.text((20, y0 + i * 24), line, fill=(40, 40, 40), font=F(16))
d.line([(W - 140, H - 20), (W - 140 + 20 * S, H - 20)], fill=(0, 0, 0), width=4); d.text((W - 140, H - 44), "20 м", fill=(0, 0, 0), font=F(16))
img.save("layout_v1.png")
print("plots", len(plots), "accessible", acc_n)
