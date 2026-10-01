#!/usr/bin/env python3
"""Renders plan.json into a top-down PNG scheme: current position (grey) -> proposed plot."""
import json, math, pathlib
from PIL import Image, ImageDraw, ImageFont

D = pathlib.Path(__file__).resolve().parents[2] / "docs/production/village_relayout_2026-10-01"
plan = json.loads((D / "plan.json").read_text())
FONT = "/System/Library/Fonts/Supplemental/Arial.ttf"; BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
font, small, bold, title = (ImageFont.truetype(FONT, 15), ImageFont.truetype(FONT, 12),
                            ImageFont.truetype(BOLD, 17), ImageFont.truetype(BOLD, 21))
X0, X1, Z0, ZTOP, K = -62, 146, -140, 162, 4.0
def Q(x, z): return ((x - X0) * K, (ZTOP - z) * K)

img = Image.new("RGB", (int((X1 - X0) * K), int((ZTOP - Z0) * K) + 110), "#f7f6f2")
g = ImageDraw.Draw(img, "RGBA")
# forest beyond the gorge and the southern gorge itself (no river)
g.rectangle([*Q(X0, plan["gorge"][0] - 2), *Q(X1, Z0)], fill=(40, 70, 45, 120))
g.text(Q(-58, -126), "лес Кара-Урман", fill="#e8efe0", font=bold)
g.rectangle([*Q(X0, plan["gorge"][1]), *Q(X1, plan["gorge"][0])], fill=(120, 135, 150, 210))
g.text(Q(-58, -100), "ГЛУБОКИЙ ОВРАГ", fill="#eef2f5", font=bold)
rav = [Q(50.5 + 1.6 * math.sin(z / 11) + .7 * math.sin(z / 4.1) - 4.6, z) for z in range(-94, ZTOP + 1, 2)]
rav += [Q(50.5 + 1.6 * math.sin(z / 11) + .7 * math.sin(z / 4.1) + 4.6, z) for z in range(ZTOP, -95, -2)]
g.polygon(rav, fill=(159, 184, 200, 170)); g.text(Q(46, 100), "ОВРАГ", fill="#3d5a70", font=bold)
g.rectangle([*Q(138, ZTOP), *Q(X1, -92)], fill=(40, 70, 45, 120)); g.text(Q(139, 0), "лес", fill="#e8efe0", font=bold)
for name, pts in plan["roads"].items():
    g.line([Q(*p) for p in pts], fill=(185, 173, 147, 235), width=int(5 * K), joint="curve")
g.line([Q(45, -24.5), Q(62, -25)], fill="#6b4f33", width=int(3.4 * K)); g.text(Q(40, -15), "мост (сносит буран)", fill="#6b4f33", font=font)
g.line([Q(0, -90), Q(0, -122)], fill="#5a3f28", width=int(2.4 * K))
for z in range(-120, -90, 3): g.line([Q(-1.6, z), Q(1.6, z)], fill="#d9c7a0", width=2)
g.text(Q(4, -104), "навесной мост «на соплях»: рвётся на обратном пути", fill="#fff7e0", font=font)
p = plan["plaza"]
g.rectangle([*Q(p["x0"], p["z1"]), *Q(p["x1"], p["z0"])], fill=(216, 204, 160, 255), outline=(150, 128, 70, 255), width=3)
cx, cy = Q((p["x0"] + p["x1"]) / 2, (p["z0"] + p["z1"]) / 2)
g.ellipse([cx - 22, cy - 22, cx + 22, cy + 22], outline=(150, 128, 70, 255), width=2)
g.text((cx - 50, cy - 48), "МӘЙДАН / площадь", fill="#5a4a1a", font=bold)
gr = plan["green"]; gx, gy = Q(gr["x"], gr["z"])
g.ellipse([gx - gr["r"] * K, gy - gr["r"] * K, gx + gr["r"] * K, gy + gr["r"] * K], fill=(150, 175, 120, 200))
g.ellipse([gx - 6, gy - 6, gx + 6, gy + 6], fill="#5a5048"); g.text((gx + 10, gy - 30), "лужайка, колодец", fill="#3d5a2a", font=small)
for name, label, at in (("yar-north", "Урам за мостом", (64, 124)), ("yar-east", "Тау урамы", (114, -80)),
                        ("yar-lower", "Түбән урам", (86, -58)), ("main", "Тукай урамы", (3, 104)), ("bridge-road", "Дәү урам", (24, -21)),
                        ("yar-tykryk", "тыкрык", (120, 70))):
    g.text(Q(*at), label, fill="#6b5a3a", font=font)
rgb = {"open": (63, 127, 191), "far-bank": (192, 97, 43), "centre": (122, 63, 168), "fixed": (46, 139, 87), "babai": (20, 120, 70), "far-public": (150, 40, 60)}
for e in plan["plan"]:
    t = e["to"]; c = rgb[e["zone"]]
    if math.hypot(t["x"] - e["x"], t["z"] - e["z"]) > 1:
        o = Q(e["x"], min(e["z"], ZTOP - 2))
        g.ellipse([o[0] - 3, o[1] - 3, o[0] + 3, o[1] + 3], fill=(150, 150, 150, 170))
        g.line([o, Q(t["x"], t["z"])], fill=c + (60,), width=1)
for e in plan["plan"]:
    t = e["to"]; c = rgb[e["zone"]]; w, d = t["w"], t["d"]
    a = math.radians(t["yaw"] or 0); ca, sa = math.cos(a), math.sin(a)
    cs = [Q(t["x"] + ca * u + sa * v, t["z"] - sa * u + ca * v) for u, v in ((-w/2, -d/2), (w/2, -d/2), (w/2, d/2), (-w/2, d/2))]
    g.polygon(cs, fill=c + (50,), outline=c + (255,))
    g.line([cs[0], cs[1]], fill=c + (255,), width=4)          # street side: facade, door, plate
    nx, ny = Q(t["x"], t["z"]); lab = e["id"].replace("ADR-", "").replace("SQUARE-", "")
    g.text((nx - 22, ny - 6), lab, fill="#222", font=small)
names = {"DK": "ДК", "SCHOOL": "школа", "OFFICE": "контора", "POST": "почта", "H020": "магазин",
         "MOSQUE": "мечеть", "BABAI": "бабай и әби", "FAP": "ФАП", "H023": "Тамара"}
for e in plan["plan"]:
    k = e["id"].replace("ADR-", "").replace("SQUARE-", "")
    if e.get("label"): names[k] = e["label"]
    if k in names:
        nx, ny = Q(e["to"]["x"], e["to"]["z"]); g.text((nx - 28, ny - 28), names[k], fill="#111", font=bold)
g.text((12, 10), "Кара-Урман: предложение v3 (север сверху, 1 клетка = 1 м × 5 px)", fill="#333", font=title)
ly = img.height - 100
for i, (k, v) in enumerate([("open", "открытая часть"), ("far-bank", "заречье: 9 → 35"), ("centre", "центр у площади"),
                            ("babai", "бабай и әби — на улице"), ("far-public", "заречье: полиция, лесничество, пекарня, молокозавод")]):
    g.rectangle([12 + i * 180, ly, 26 + i * 180, ly + 14], fill=rgb[k]); g.text((32 + i * 180, ly - 1), v, fill="#222", font=small)
g.text((12, ly + 26), f"Серая точка — где дом сейчас, линия — куда переезжает. Участки забор к забору, соседи делят забор: {plan['fences']['fenceBuiltM']} м заборов вместо {plan['fences']['plotEdgesM']} м; наложений: {len(plan['overlaps'])}.", fill="#444", font=small)
g.text((12, ly + 46), "Толстая кромка — сторона улицы: фасад, дверь и табличка у каждого дома смотрят на свою улицу. Бывший северный хвост (z>150) освобождается.", fill="#444", font=small)
img.save(D / "scheme.png"); print(D / "scheme.png")
