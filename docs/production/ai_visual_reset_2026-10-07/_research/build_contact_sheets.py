#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Собирает контактные листы (contact sheets) по категориям пака кадров."""
import glob
import json
import os
from PIL import Image, ImageDraw, ImageFont

BASE = "/Users/unterlantas/Documents/GitHub/URMAN/docs/production/ai_visual_reset_2026-10-07"
SHOTS = os.path.join(BASE, "screenshots")
OUT = os.path.join(SHOTS, "00_contact_sheets")

TITLES = {
    "01_world_exterior": "01 WORLD / EXTERIOR - village, streets, yards",
    "02_materials_snow_trees": "02 MATERIALS / SNOW / TREES",
    "03_interiors": "03 INTERIORS",
    "04_characters": "04 CHARACTERS / ANIMATION",
    "05_ui": "05 UI / HUD / DOCUMENTS",
    "06_night_forest_atmosphere": "06 NIGHT / FOREST / ATMOSPHERE",
    "07_vehicle_cutscene": "07 VEHICLE / CUTSCENE",
}

COLS = 5
MAX_ROWS = 4          # ограничение высоты листа: иначе чат сильно уменьшает картинку
THUMB_W = 360
THUMB_H = 210
PAD = 10
LABEL_H = 26


def pick_font(size):
    for p in ("/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
              "/System/Library/Fonts/Supplemental/Arial.ttf",
              "/Library/Fonts/Arial Unicode.ttf"):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def main():
    os.makedirs(OUT, exist_ok=True)
    for cat, title in TITLES.items():
        files = sorted(glob.glob(os.path.join(SHOTS, cat, "*.jpg")))
        if not files:
            continue
        thumbs = []
        for f in files:
            im = Image.open(f).convert("RGB")
            w, h = im.size
            scale = min(THUMB_W / w, THUMB_H / h)
            im = im.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.LANCZOS)
            cell = Image.new("RGB", (THUMB_W, THUMB_H), (16, 16, 18))
            cell.paste(im, ((THUMB_W - im.size[0]) // 2, (THUMB_H - im.size[1]) // 2))
            thumbs.append((os.path.basename(f)[:3], cell))
        cell_h = THUMB_H + LABEL_H
        per_page = COLS * MAX_ROWS
        pages = [thumbs[i:i + per_page] for i in range(0, len(thumbs), per_page)]
        num_font = pick_font(20)
        for pno, page in enumerate(pages, 1):
            rows = (len(page) + COLS - 1) // COLS
            head = 44
            W = PAD + COLS * (THUMB_W + PAD)
            H = head + PAD + rows * (cell_h + PAD)
            sheet = Image.new("RGB", (W, H), (26, 26, 30))
            d = ImageDraw.Draw(sheet)
            suffix = f"  [{pno}/{len(pages)}]" if len(pages) > 1 else ""
            d.text((PAD, 12), title + suffix, font=pick_font(24), fill=(255, 235, 200))
            for i, (num, t) in enumerate(page):
                r, c = divmod(i, COLS)
                x = PAD + c * (THUMB_W + PAD)
                y = head + PAD + r * (cell_h + PAD)
                sheet.paste(t, (x, y))
                d.rectangle([x, y + THUMB_H, x + THUMB_W, y + THUMB_H + LABEL_H], fill=(40, 40, 46))
                d.text((x + 6, y + THUMB_H + 3), num, font=num_font, fill=(255, 220, 120))
                d.rectangle([x, y, x + THUMB_W - 1, y + THUMB_H - 1], outline=(70, 70, 78))
            name = f"contact_{cat}.jpg" if len(pages) == 1 else f"contact_{cat}_p{pno}.jpg"
            out = os.path.join(OUT, name)
            sheet.save(out, quality=86, optimize=True)
            print(f"{out}  {sheet.size[0]}x{sheet.size[1]}  {len(page)} кадров")


if __name__ == "__main__":
    main()
