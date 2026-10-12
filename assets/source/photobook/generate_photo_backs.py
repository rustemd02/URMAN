"""Produce editable-source photo-back layouts from authored captions and PT Sans.

SVG uses font outlines because the game's SVG importer does not require a text
layout engine. Captions remain editable in photos.spec.json and the album UI.
Only a new output directory is accepted; existing art is never overwritten.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

SVG = "http://www.w3.org/2000/svg"
ET.register_namespace("", SVG)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def produce(output: Path) -> None:
    root = Path(__file__).resolve().parents[3]
    source = root / "content/campaigns/urman.fullgame/photoworlds-sources/photos.spec.json"
    font_path = root / "game/assets/fonts/PT_Sans-Web-Regular.ttf"
    license_path = root / "game/assets/fonts/OFL.txt"
    photos = json.loads(source.read_text(encoding="utf-8"))["photos"]
    if len(photos) != 13 or len({p["id"] for p in photos}) != 13:
        raise ValueError("This version produces the thirteen authored photo backs.")
    if output.exists():
        raise FileExistsError(f"Refusing to overwrite art: {output}")
    font = TTFont(font_path)
    cmap = font.getBestCmap()
    glyphs = font.getGlyphSet()
    missing = sorted({c for p in photos for c in p["back_text_ru"] if ord(c) not in cmap})
    if missing:
        raise ValueError(f"Missing caption glyphs: {missing}")
    output.mkdir(parents=True)
    scale = 65.0 / font["head"].unitsPerEm
    records = []
    for photo in photos:
        caption = photo["back_text_ru"]
        if not caption.strip():
            raise ValueError(f"Empty authored caption: {photo['id']}")
        pen = SVGPathPen(glyphs)
        advance = 0
        for char in caption:
            name = cmap[ord(char)]
            glyphs[name].draw(TransformPen(pen, (1, 0, 0, 1, advance, 0)))
            advance += font["hmtx"][name][0]
        if advance * scale > 1520:
            raise ValueError(f"Caption exceeds the print-safe width: {photo['id']}")
        svg = ET.Element(f"{{{SVG}}}svg", {
            "width": "1800", "height": "1200", "viewBox": "0 0 1800 1200",
        })
        ET.SubElement(svg, f"{{{SVG}}}title").text = caption
        ET.SubElement(svg, f"{{{SVG}}}metadata").text = json.dumps({
            "photo_id": photo["id"], "author_caption": caption,
            "source_kind": "fictional_game_print_layout", "physical_size_mm": [150, 100],
        }, ensure_ascii=False)
        ET.SubElement(svg, f"{{{SVG}}}rect", {
            "width": "1800", "height": "1200", "fill": "#eee5d1",
        })
        ET.SubElement(svg, f"{{{SVG}}}path", {
            "d": pen.getCommands(), "fill": "#554b40",
            "transform": f"translate(140 780) scale({scale} {-scale})",
        })
        path = output / (photo["id"].lower() + "-back.svg")
        ET.ElementTree(svg).write(path, encoding="utf-8", xml_declaration=True)
        records.append({
            "photo_id": photo["id"], "caption": caption, "file": path.name,
            "sha256": digest(path), "dimensions_px": [1800, 1200],
            "physical_size_mm": [150, 100], "caption_width_px": advance * scale,
        })
    manifest = {
        "schema": "urman-photo-back-layouts/v1", "records": records,
        "source": str(source.relative_to(root)), "source_sha256": digest(source),
        "generator": str(Path(__file__).resolve().relative_to(root)),
        "generator_sha256": digest(Path(__file__)),
        "font": str(font_path.relative_to(root)), "font_sha256": digest(font_path),
        "font_license": str(license_path.relative_to(root)), "missing_glyphs": [],
        "method": "PT Sans outlines from exact authored caption; no text generation or translation",
        "rights": "Original fictional print layout, licensed unchanged font; no archive authenticity claim",
        "remaining": "Protected album view and author art/language acceptance; other photo representations",
    }
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Produced {len(records)} photo backs in {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, required=True)
    produce(parser.parse_args().output_dir.resolve())
