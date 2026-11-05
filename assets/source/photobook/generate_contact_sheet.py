"""Lay out the fictional S-PRINT contact sheet from shipped photo fronts."""
from pathlib import Path
import hashlib
import json

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[3]
BOOK = ROOT / "game/assets/textures/photobook"
FONTS = ROOT / "game/assets/fonts"
FRAMES = (
    ("01–03", "Двор со ставней", "p-house-front-v1.png"),
    ("04–06", "Дети у мостков", "p-water-front-v1.png"),
    ("07–09", "Соседский сад", "s-garden-front-v1.png"),
    ("10–11", "Люди у леса", "p-forest-front-v2.png"),
)
INK = "#28271f"
MUTED = "#5d5548"


def produce() -> None:
    sheet = Image.new("RGB", (1800, 1200), "#eee5d1")
    draw = ImageDraw.Draw(sheet)

    def label(x, y, text, size, *, bold=False, colour=INK):
        style = "Bold" if bold else "Regular"
        font = ImageFont.truetype(str(FONTS / f"PT_Sans-Web-{style}.ttf"), size)
        draw.text((x, y), text, font=font, fill=colour)

    label(88, 80, "СЕМЕЙНЫЕ СНИМКИ   /   ПЛЁНКА «ЛЕТО»", 38, bold=True, colour=MUTED)
    label(88, 137, "Контактный лист", 98, bold=True)
    draw.line((88, 281, 1712, 281), fill=MUTED, width=3)

    for index, (numbers, caption, filename) in enumerate(FRAMES):
        x = 88 + index * 414
        label(x, 322, numbers, 38, bold=True)
        draw.rectangle((x, 380, x + 382, 644), fill="#363229")
        with Image.open(BOOK / filename) as original:
            photo = ImageOps.contain(original.convert("RGB"), (368, 250), Image.Resampling.LANCZOS)
        sheet.paste(photo, (x + (382 - photo.width) // 2, 380 + (264 - photo.height) // 2))
        label(x, 673, caption, 35)

    draw.rectangle((88, 793, 1711, 1108), fill="#e1d5bc")
    draw.rectangle((88, 793, 98, 1108), fill="#8c3f34")
    label(125, 831, "КАДР 12   /   СОХРАНЁН ОТДЕЛЬНО", 36, bold=True, colour=MUTED)
    label(125, 900, "общий_кадр.jpg", 83, bold=True)

    front = BOOK / "s-print-front-v1.png"
    thumb = BOOK / "s-print-front-v1-thumbnail.png"
    sheet.save(front, optimize=True)
    sheet.resize((256, 171), Image.Resampling.LANCZOS).save(thumb, optimize=True)

    def source(path):
        return {"path": str(path.relative_to(ROOT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}

    receipt = {
        "asset_id": "S-PRINT",
        "method": "Original fictional print layout using exact shipped family-photo fronts as group thumbnails.",
        "source_spec": "content/campaigns/urman.fullgame/photoworlds-sources/photos.spec.json#S-PRINT",
        "sources": [source(BOOK / filename) for _, _, filename in FRAMES],
        "font_license": "game/assets/fonts/OFL.txt",
        "front": {**source(front), "size_px": [1800, 1200]},
        "thumbnail": {**source(thumb), "size_px": [256, 171]},
        "captioned_file": "общий_кадр.jpg",
    }
    (BOOK / "s-print-front-v1.provenance.json").write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    produce()
