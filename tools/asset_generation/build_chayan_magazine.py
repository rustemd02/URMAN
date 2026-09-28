#!/usr/bin/env python3
"""Build the date-free Chayan magazine print atlas and animated glTF prop."""

from __future__ import annotations

import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ART = ROOT / "assets/source/illustrations/chayan_winter_cover_art_v1.png"
FULL_COVER = ROOT / "assets/source/illustrations/chayan_full_cover_v1.png"
ATLAS_PATH = ROOT / "game/assets/textures/props/chayan_magazine_atlas_v1.png"
MODEL_PATH = ROOT / "game/assets/models/props/chayan_magazine_animated_v1.glb"
PREVIEW_PATH = ROOT / "game/assets/models/props/chayan_magazine_preview_v1.png"

ATLAS_SIZE = 4096
MAG_WIDTH = 0.155
MAG_HEIGHT = 0.215
MAG_HALF_H = MAG_HEIGHT / 2.0
FRONT_COVER = (1464, 64, 2824, 1877)
BACK_COVER = (64, 64, 1424, 1877)
SPINE = (1424, 64, 1464, 1877)
SWATCH = (4048, 4048, 4080, 4080)
PAGE_TILE_SIZE = (560, 746)

COLORS = {
    "violet": "#b67ddd",
    "violet_dark": "#7153a7",
    "red": "#d31439",
    "red_dark": "#7e173f",
    "blue": "#344fe0",
    "blue_dark": "#252e81",
    "cream": "#fff3d2",
    "paper": "#f5ecd4",
    "ink": "#252333",
    "muted": "#777181",
}

FONT_BOLD = ROOT / "game/assets/fonts/PT_Sans-Web-Bold.ttf"
FONT_REGULAR = ROOT / "game/assets/fonts/PT_Sans-Web-Regular.ttf"
FONT_ITALIC = ROOT / "game/assets/fonts/PT_Sans-Web-Italic.ttf"


def font(path: Path, size: int) -> ImageFont.FreeTypeFont:
    if not path.exists():
        fallback = Path("/System/Library/Fonts/Supplemental/Arial Bold.ttf")
        return ImageFont.truetype(str(fallback), size)
    return ImageFont.truetype(str(path), size)


def fit_font(text: str, path: Path, max_size: int, width: int, height: int) -> ImageFont.FreeTypeFont:
    size = max_size
    while size > 8:
        current = font(path, size)
        box = current.getbbox(text, stroke_width=0)
        if box[2] - box[0] <= width and box[3] - box[1] <= height:
            return current
        size -= 2
    return font(path, 10)


def centered_text(
    draw: ImageDraw.ImageDraw,
    center_x: float,
    y: float,
    text: str,
    face: ImageFont.FreeTypeFont,
    fill: str,
    *,
    stroke_width: int = 0,
    stroke_fill: str = "#ffffff",
) -> None:
    box = draw.textbbox((0, 0), text, font=face, stroke_width=stroke_width)
    x = center_x - (box[2] - box[0]) / 2.0
    draw.text(
        (round(x), round(y)),
        text,
        font=face,
        fill=fill,
        stroke_width=stroke_width,
        stroke_fill=stroke_fill,
    )


def draw_bezier(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], fill: str, width: int) -> None:
    p0, p1, p2, p3 = points
    sampled: list[tuple[float, float]] = []
    for step in range(41):
        t = step / 40.0
        u = 1.0 - t
        x = u**3 * p0[0] + 3 * u**2 * t * p1[0] + 3 * u * t**2 * p2[0] + t**3 * p3[0]
        y = u**3 * p0[1] + 3 * u**2 * t * p1[1] + 3 * u * t**2 * p2[1] + t**3 * p3[1]
        sampled.append((x, y))
    draw.line(sampled, fill=fill, width=width, joint="curve")


def draw_scorpion_mark(
    draw: ImageDraw.ImageDraw,
    origin: tuple[int, int],
    scale: float = 1.0,
    *,
    red: str = COLORS["red"],
    cream: str = COLORS["cream"],
) -> None:
    ox, oy = origin

    def p(x: float, y: float) -> tuple[int, int]:
        return round(ox + x * scale), round(oy + y * scale)

    # A new, deliberately simple mascot interpretation: round comic face,
    # two pincers, six legs, and a curling tail with a diamond stinger.
    draw.ellipse((p(35, 46), p(147, 153)), fill=red, outline=COLORS["red_dark"], width=max(1, round(5 * scale)))
    draw.ellipse((p(45, 57), p(135, 140)), fill=cream, outline=COLORS["red_dark"], width=max(1, round(3 * scale)))
    draw.ellipse((p(61, 78), p(94, 119)), fill="#ffffff", outline=COLORS["blue_dark"], width=max(1, round(3 * scale)))
    draw.ellipse((p(91, 76), p(123, 116)), fill="#ffffff", outline=COLORS["blue_dark"], width=max(1, round(3 * scale)))
    draw.ellipse((p(77, 91), p(87, 108)), fill=COLORS["ink"])
    draw.ellipse((p(103, 88), p(113, 105)), fill=COLORS["ink"])
    draw.arc((p(70, 98), p(112, 133)), start=5, end=160, fill=red, width=max(2, round(6 * scale)))

    for start, end in [((48, 85), (13, 68)), ((44, 114), (7, 143)), ((67, 142), (46, 190)),
                       ((100, 144), (114, 193)), ((132, 112), (168, 143)), ((136, 81), (174, 55))]:
        draw_bezier(
            draw,
            [p(*start), p(start[0] - 9, start[1]), p(end[0] + 10, end[1]), p(*end)],
            red,
            max(2, round(7 * scale)),
        )

    # Pincers and their curled tips.
    for cx, cy, direction in [(3, 63, -1), (165, 43, 1)]:
        draw.ellipse((p(cx, cy), p(cx + 34, cy + 26)), fill=red, outline=COLORS["red_dark"], width=max(1, round(3 * scale)))
        draw.arc((p(cx - 6, cy - 10), p(cx + 31, cy + 31)), start=205 if direction < 0 else 30,
                 end=320 if direction < 0 else 145, fill=cream, width=max(2, round(5 * scale)))

    draw_bezier(
        draw,
        [p(130, 61), p(175, 31), p(188, 3), p(166, 1)],
        red,
        max(3, round(12 * scale)),
    )
    draw.polygon([p(158, 7), p(172, -4), p(181, 12)], fill=red, outline=COLORS["red_dark"])


def create_front_cover(full_cover: Image.Image) -> Image.Image:
    width = FRONT_COVER[2] - FRONT_COVER[0]
    height = FRONT_COVER[3] - FRONT_COVER[1]
    # ImageGen produced the complete front face, including all typography and
    # illustration. Keep its design intact and only fit it to the UV island.
    return ImageOps.fit(
        full_cover.convert("RGB"),
        (width, height),
        method=Image.Resampling.LANCZOS,
        centering=(0.5, 0.5),
    )


def create_back_cover(art: Image.Image) -> Image.Image:
    width = BACK_COVER[2] - BACK_COVER[0]
    height = BACK_COVER[3] - BACK_COVER[1]
    back = Image.new("RGB", (width, height), COLORS["violet"])
    draw = ImageDraw.Draw(back)
    draw.rectangle((0, 0, width, 122), fill=COLORS["blue"])
    centered_text(draw, width / 2, 26, "Көлү – сихәт, көлеп яшәү мәслихәт!", font(FONT_ITALIC, 28), "#ffffff")
    draw.rectangle((28, 150, width - 28, height - 34), fill=COLORS["paper"])
    draw.rectangle((28, 150, width - 28, 208), fill=COLORS["red"])
    centered_text(draw, width / 2, 160, "ЧАЯНВОРД", fit_font("ЧАЯНВОРД", FONT_BOLD, 39, width - 100, 48), "#ffffff")

    puzzle_x, puzzle_y, puzzle_size = 82, 262, 640
    cells = 8
    cell = puzzle_size // cells
    for row in range(cells):
        for col in range(cells):
            rect = (puzzle_x + col * cell, puzzle_y + row * cell, puzzle_x + (col + 1) * cell, puzzle_y + (row + 1) * cell)
            blocked = ((row * 3 + col * 5 + row * col) % 9 == 0)
            draw.rectangle(rect, fill=COLORS["blue_dark"] if blocked else "#fff9e8", outline=COLORS["ink"], width=3)

    # A fresh crop of the generated cover cartoon becomes a small back-cover spot.
    thumbnail = ImageOps.fit(art.convert("RGB"), (430, 590), centering=(0.74, 0.47), method=Image.Resampling.LANCZOS)
    back.paste(thumbnail, (825, 260))
    draw = ImageDraw.Draw(back)
    draw.rectangle((825, 260, 1255, 850), outline=COLORS["red"], width=8)
    for i, width_px in enumerate((1080, 1150, 1000, 1175, 965)):
        y = 963 + i * 72
        draw.rounded_rectangle((86, y, 86 + width_px, y + 12), radius=6, fill="#65566d")
    draw_scorpion_mark(draw, (1000, 1360), 0.68)
    centered_text(draw, 455, 1402, "САТИРА ҺӘМ ЮМОР ЖУРНАЛЫ", font(FONT_BOLD, 28), COLORS["blue_dark"])
    return back


def tile_positions() -> list[tuple[int, int]]:
    positions: list[tuple[int, int]] = []
    right_x = (2880, 3472)
    right_y = (64, 834, 1604, 2374, 3144)
    for y in right_y:
        for x in right_x:
            positions.append((x, y))
    bottom_x = (64, 664, 1264, 1864)
    bottom_y = (1912, 2682)
    for y in bottom_y:
        for x in bottom_x:
            positions.append((x, y))
    return positions


SECTION_HEADINGS = [
    "ШУМБАЙ",
    "АБАУ",
    "ЧАЯН ПОЧТАСЫ",
    "ЧАЯНВОРД",
    "ПОЛНЫЙ АБЗАЦ!",
    "ПРОКОЛ",
    "СМЕХ СКВОЗЬ ГОДЫ",
    "КАРИКАТУРАЛАР",
]


def draw_print_lines(draw: ImageDraw.ImageDraw, x: int, y: int, width: int, rows: int, seed: int) -> None:
    for row in range(rows):
        span = width - ((row * 37 + seed * 19) % max(80, width // 3))
        color = "#453e47" if row % 5 else "#82777d"
        draw.rounded_rectangle((x, y + row * 23, x + max(28, span), y + row * 23 + 7), radius=3, fill=color)


def make_page_tile(index: int, art: Image.Image) -> Image.Image:
    tile_w, tile_h = PAGE_TILE_SIZE
    page = Image.new("RGB", PAGE_TILE_SIZE, COLORS["paper"])
    draw = ImageDraw.Draw(page)

    if index == 0:
        draw.rectangle((0, 0, tile_w, 29), fill=COLORS["red"])
        draw.rectangle((0, tile_h - 26, tile_w, tile_h), fill=COLORS["blue"])
        face = fit_font("ЧАЯН", FONT_BOLD, 96, tile_w - 56, 124)
        centered_text(draw, tile_w / 2, 102, "ЧАЯН", face, COLORS["red"])
        draw_scorpion_mark(draw, (202, 270), 0.85)
        centered_text(draw, tile_w / 2, 474, "САТИРА ҺӘМ ЮМОР", font(FONT_BOLD, 27), COLORS["blue_dark"])
        draw_print_lines(draw, 56, 535, tile_w - 112, 5, 11)
        return page

    if index == 17:
        draw.rectangle((0, 0, tile_w, 29), fill=COLORS["blue"])
        centered_text(draw, tile_w / 2, 79, "БАСМА ЭЧЕНДӘ", font(FONT_BOLD, 32), COLORS["red"])
        draw_print_lines(draw, 60, 147, tile_w - 120, 8, 17)
        draw.line((60, 386, tile_w - 60, 386), fill=COLORS["red"], width=4)
        centered_text(draw, tile_w / 2, 410, "КӨЛЕП ЯШӘ!", font(FONT_BOLD, 42), COLORS["blue_dark"])
        draw_print_lines(draw, 60, 491, tile_w - 120, 7, 23)
        draw.rectangle((0, tile_h - 26, tile_w, tile_h), fill=COLORS["red"])
        return page

    story_index = index - 1
    heading = SECTION_HEADINGS[(story_index - 1) % len(SECTION_HEADINGS)]
    draw.rectangle((0, 0, tile_w, 19), fill=COLORS["blue"])
    draw.rectangle((28, 30, 36, tile_h - 42), fill=COLORS["red"])
    face = fit_font(heading, FONT_BOLD, 39, tile_w - 76, 54)
    draw.text((55, 42), heading, font=face, fill=COLORS["red_dark"])
    draw.line((55, 104, tile_w - 32, 104), fill=COLORS["blue"], width=4)

    if "ЧАЯНВОРД" in heading:
        grid_x, grid_y, grid_size = 48, 145, 395
        cells = 9
        cell = grid_size // cells
        for row in range(cells):
            for col in range(cells):
                blocked = ((row * 7 + col * 3 + row * col + story_index) % 11 == 0)
                rect = (grid_x + col * cell, grid_y + row * cell, grid_x + (col + 1) * cell, grid_y + (row + 1) * cell)
                draw.rectangle(rect, fill=COLORS["blue_dark"] if blocked else "#fffaf0", outline=COLORS["ink"], width=2)
        draw_print_lines(draw, 462, 152, 69, 16, story_index)
        draw_print_lines(draw, 55, 570, tile_w - 110, 5, story_index + 3)
    else:
        window = (
            (0.03, 0.04, 0.70, 0.64),
            (0.22, 0.09, 0.95, 0.68),
            (0.06, 0.34, 0.78, 0.95),
            (0.31, 0.18, 0.99, 0.79),
            (0.00, 0.19, 0.75, 0.86),
            (0.18, 0.29, 0.90, 0.95),
            (0.04, 0.02, 0.89, 0.55),
            (0.28, 0.40, 0.99, 0.99),
        )[story_index % 8]
        crop_box = (
            int(window[0] * art.width),
            int(window[1] * art.height),
            int(window[2] * art.width),
            int(window[3] * art.height),
        )
        spot = ImageOps.fit(art.crop(crop_box), (254, 254), method=Image.Resampling.LANCZOS)
        page.paste(spot, (52, 140))
        draw.rectangle((52, 140, 306, 394), outline=COLORS["red"], width=5)
        draw_print_lines(draw, 333, 145, 195, 11, story_index)
        draw_print_lines(draw, 54, 421, tile_w - 108, 10, story_index + 7)
        draw.line((54, 677, tile_w - 54, 677), fill="#c1b39c", width=2)
        draw.text((52, 690), "ЧАЯН", font=font(FONT_BOLD, 20), fill=COLORS["blue_dark"])
    draw.text((tile_w - 52, tile_h - 43), str(index), font=font(FONT_BOLD, 22), fill=COLORS["red_dark"], anchor="ra")
    return page


def build_atlas(art: Image.Image, full_cover: Image.Image) -> dict[str, tuple[int, int, int, int]]:
    atlas = Image.new("RGB", (ATLAS_SIZE, ATLAS_SIZE), "#29233d")

    front = create_front_cover(full_cover)
    back = create_back_cover(art)
    atlas.paste(back, (BACK_COVER[0], BACK_COVER[1]))
    atlas.paste(front, (FRONT_COVER[0], FRONT_COVER[1]))

    spine = Image.new("RGB", (SPINE[2] - SPINE[0], SPINE[3] - SPINE[1]), COLORS["blue"])
    spine_draw = ImageDraw.Draw(spine)
    spine_draw.line((1, 0, 1, spine.height), fill=COLORS["red"], width=4)
    spine_draw.line((spine.width - 3, 0, spine.width - 3, spine.height), fill=COLORS["cream"], width=2)
    vertical = Image.new("RGBA", (spine.height, spine.width), (0, 0, 0, 0))
    vertical_draw = ImageDraw.Draw(vertical)
    title_font = fit_font("ЧАЯН", FONT_BOLD, 22, spine.height - 30, spine.width - 4)
    vertical_draw.text((10, 4), "ЧАЯН", font=title_font, fill="#ffffff")
    spine.paste(vertical.rotate(90, expand=True), (0, 0), vertical.rotate(90, expand=True))
    atlas.paste(spine, (SPINE[0], SPINE[1]))

    positions = tile_positions()
    for index, position in enumerate(positions):
        tile = make_page_tile(index, art)
        atlas.paste(tile, position)

    draw = ImageDraw.Draw(atlas)
    draw.rectangle(SWATCH, fill=COLORS["paper"])
    draw.rectangle((SWATCH[0], SWATCH[1], SWATCH[2] - 1, SWATCH[3] - 1), outline="#cbbf9f", width=2)
    ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(ATLAS_PATH, format="PNG", optimize=True)

    regions: dict[str, tuple[int, int, int, int]] = {
        "front_cover": FRONT_COVER,
        "back_cover": BACK_COVER,
        "spine": SPINE,
        "inside_front": (positions[0][0], positions[0][1], positions[0][0] + PAGE_TILE_SIZE[0], positions[0][1] + PAGE_TILE_SIZE[1]),
        "inside_back": (positions[17][0], positions[17][1], positions[17][0] + PAGE_TILE_SIZE[0], positions[17][1] + PAGE_TILE_SIZE[1]),
        "edge_swatch": SWATCH,
    }
    for page_index in range(16):
        pos = positions[page_index + 1]
        regions[f"page_{page_index + 1:02}"] = (pos[0], pos[1], pos[0] + PAGE_TILE_SIZE[0], pos[1] + PAGE_TILE_SIZE[1])
    return regions


class Mesh:
    def __init__(self) -> None:
        self.positions: list[tuple[float, float, float]] = []
        self.normals: list[tuple[float, float, float]] = []
        self.uvs: list[tuple[float, float]] = []
        self.indices: list[int] = []

    @staticmethod
    def uv_for_rect(rect: tuple[int, int, int, int], atlas_size: int, *, backside: bool = False) -> list[tuple[float, float]]:
        x0, y0, x1, y1 = rect
        u0, v0 = x0 / atlas_size, y0 / atlas_size
        u1, v1 = x1 / atlas_size, y1 / atlas_size
        if backside:
            # Rotated 180 degrees so the printed back reads upright after a leaf turns.
            return [(u1, v0), (u1, v1), (u0, v1), (u0, v0)]
        return [(u0, v1), (u0, v0), (u1, v0), (u1, v1)]

    def quad(
        self,
        points: list[tuple[float, float, float]],
        normal: tuple[float, float, float],
        uvs: list[tuple[float, float]],
        *,
        reverse: bool = False,
    ) -> None:
        start = len(self.positions)
        self.positions.extend(points)
        self.normals.extend([normal] * 4)
        self.uvs.extend(uvs)
        if reverse:
            self.indices.extend((start, start + 2, start + 1, start, start + 3, start + 2))
        else:
            self.indices.extend((start, start + 1, start + 2, start, start + 2, start + 3))

    def page_face(self, y: float, rect: tuple[int, int, int, int], normal_y: float, atlas_size: int, *, backside: bool) -> None:
        points = [
            (0.0, y, -MAG_HALF_H),
            (0.0, y, MAG_HALF_H),
            (MAG_WIDTH, y, MAG_HALF_H),
            (MAG_WIDTH, y, -MAG_HALF_H),
        ]
        self.quad(points, (0.0, normal_y, 0.0), self.uv_for_rect(rect, atlas_size, backside=backside), reverse=normal_y < 0.0)

    def box(
        self,
        x0: float,
        x1: float,
        y0: float,
        y1: float,
        z0: float,
        z1: float,
        top_rect: tuple[int, int, int, int],
        bottom_rect: tuple[int, int, int, int],
        edge_rect: tuple[int, int, int, int],
        atlas_size: int,
    ) -> None:
        self.quad(
            [(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)],
            (0.0, 1.0, 0.0),
            self.uv_for_rect(top_rect, atlas_size),
        )
        self.quad(
            [(x0, y0, z0), (x0, y0, z1), (x1, y0, z1), (x1, y0, z0)],
            (0.0, -1.0, 0.0),
            self.uv_for_rect(bottom_rect, atlas_size, backside=True),
            reverse=True,
        )
        swatch_uv = self.uv_for_rect(edge_rect, atlas_size)
        self.quad([(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1.0, 0.0, 0.0), swatch_uv, reverse=True)
        self.quad([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1.0, 0.0, 0.0), swatch_uv, reverse=True)
        self.quad([(x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (x1, y0, z1)], (0.0, 0.0, 1.0), swatch_uv, reverse=True)
        self.quad([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0.0, 0.0, -1.0), swatch_uv, reverse=True)

    def gltf_mesh(self, name: str, buffer: "GltfBuffer") -> dict:
        positions = buffer.accessor_floats(self.positions, "VEC3", target=34962, include_bounds=True)
        normals = buffer.accessor_floats(self.normals, "VEC3", target=34962)
        uvs = buffer.accessor_floats(self.uvs, "VEC2", target=34962)
        index_type = 5123 if len(self.positions) < 65536 else 5125
        if index_type == 5123:
            packed_indices = struct.pack("<" + "H" * len(self.indices), *self.indices)
        else:
            packed_indices = struct.pack("<" + "I" * len(self.indices), *self.indices)
        indices = buffer.accessor_bytes(
            packed_indices,
            "SCALAR",
            index_type,
            len(self.indices),
            target=34963,
        )
        return {
            "name": name,
            "primitives": [
                {
                    "attributes": {"POSITION": positions, "NORMAL": normals, "TEXCOORD_0": uvs},
                    "indices": indices,
                    "material": 0,
                }
            ],
        }


class GltfBuffer:
    def __init__(self) -> None:
        self.data = bytearray()
        self.views: list[dict] = []
        self.accessors: list[dict] = []

    def view(self, raw: bytes, target: int | None = None) -> int:
        while len(self.data) % 4:
            self.data.append(0)
        offset = len(self.data)
        self.data.extend(raw)
        view: dict[str, int] = {"buffer": 0, "byteOffset": offset, "byteLength": len(raw)}
        if target is not None:
            view["target"] = target
        self.views.append(view)
        return len(self.views) - 1

    def accessor_bytes(
        self,
        raw: bytes,
        accessor_type: str,
        component_type: int,
        count: int,
        target: int | None = None,
        *,
        minimum: list[float] | None = None,
        maximum: list[float] | None = None,
    ) -> int:
        view_index = self.view(raw, target)
        accessor: dict = {
            "bufferView": view_index,
            "componentType": component_type,
            "count": count,
            "type": accessor_type,
        }
        if minimum is not None:
            accessor["min"] = minimum
        if maximum is not None:
            accessor["max"] = maximum
        self.accessors.append(accessor)
        return len(self.accessors) - 1

    def accessor_floats(
        self,
        values: list[tuple[float, ...]],
        accessor_type: str,
        target: int | None = None,
        *,
        include_bounds: bool = False,
    ) -> int:
        flattened = [number for item in values for number in item]
        raw = struct.pack("<" + "f" * len(flattened), *flattened)
        dims = len(values[0]) if values else 1
        minimum = maximum = None
        if include_bounds and values:
            minimum = [min(item[axis] for item in values) for axis in range(dims)]
            maximum = [max(item[axis] for item in values) for axis in range(dims)]
        return self.accessor_bytes(raw, accessor_type, 5126, len(values), target, minimum=minimum, maximum=maximum)


def rectangle_face(mesh: Mesh, rect: tuple[int, int, int, int], y: float, normal_y: float, atlas_size: int, *, backside: bool) -> None:
    mesh.page_face(y, rect, normal_y, atlas_size, backside=backside)


def make_cover_mesh(regions: dict[str, tuple[int, int, int, int]], atlas_size: int, edge_rect: tuple[int, int, int, int]) -> Mesh:
    mesh = Mesh()
    rectangle_face(mesh, regions["front_cover"], 0.0, 1.0, atlas_size, backside=False)
    rectangle_face(mesh, regions["inside_front"], 0.0, -1.0, atlas_size, backside=True)
    edge_uv = Mesh.uv_for_rect(edge_rect, atlas_size)
    y0, y1 = -0.00022, 0.00022
    x0, x1 = 0.0, MAG_WIDTH
    z0, z1 = -MAG_HALF_H, MAG_HALF_H
    mesh.quad([(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1.0, 0.0, 0.0), edge_uv, reverse=True)
    mesh.quad([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1.0, 0.0, 0.0), edge_uv, reverse=True)
    mesh.quad([(x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (x1, y0, z1)], (0.0, 0.0, 1.0), edge_uv, reverse=True)
    mesh.quad([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0.0, 0.0, -1.0), edge_uv, reverse=True)
    return mesh


def make_page_mesh(front_rect: tuple[int, int, int, int], back_rect: tuple[int, int, int, int], atlas_size: int) -> Mesh:
    mesh = Mesh()
    rectangle_face(mesh, front_rect, 0.0, 1.0, atlas_size, backside=False)
    rectangle_face(mesh, back_rect, 0.0, -1.0, atlas_size, backside=True)
    return mesh


def glb_animation(
    name: str,
    tracks: list[tuple[int, list[tuple[float, tuple[float, float, float, float]]]]],
    buffer: GltfBuffer,
) -> dict:
    samplers: list[dict] = []
    channels: list[dict] = []
    for node_index, keys in tracks:
        times = [key[0] for key in keys]
        rotations = [key[1] for key in keys]
        input_accessor = buffer.accessor_floats([(value,) for value in times], "SCALAR")
        output_accessor = buffer.accessor_floats(list(rotations), "VEC4")
        sampler_index = len(samplers)
        samplers.append({"input": input_accessor, "output": output_accessor, "interpolation": "LINEAR"})
        channels.append({"sampler": sampler_index, "target": {"node": node_index, "path": "rotation"}})
    return {"name": name, "samplers": samplers, "channels": channels}


def write_glb(regions: dict[str, tuple[int, int, int, int]]) -> None:
    buffer = GltfBuffer()
    meshes: list[dict] = []
    nodes: list[dict] = [{"name": "ChayanMagazine_Root", "children": []}]
    children: list[int] = []

    body = Mesh()
    body.box(
        0.0,
        MAG_WIDTH,
        -0.0040,
        -0.00050,
        -MAG_HALF_H,
        MAG_HALF_H,
        regions["inside_back"],
        regions["back_cover"],
        regions["edge_swatch"],
        ATLAS_SIZE,
    )
    meshes.append(body.gltf_mesh("PageBlock_BackCover", buffer))
    nodes.append({"name": "PageBlock_BackCover", "mesh": 0})
    children.append(1)

    cover_node_index = len(nodes)
    cover_mesh_index = len(meshes)
    meshes.append(make_cover_mesh(regions, ATLAS_SIZE, regions["edge_swatch"]).gltf_mesh("FrontCover", buffer))
    nodes.append({"name": "FrontCover", "mesh": cover_mesh_index, "translation": [0.0, 0.0027, 0.0]})
    children.append(cover_node_index)

    page_node_indices: list[int] = []
    first_page_y = -0.00035
    page_step = 0.00033
    for leaf in range(8):
        front_rect = regions[f"page_{leaf * 2 + 1:02}"]
        back_rect = regions[f"page_{leaf * 2 + 2:02}"]
        mesh_index = len(meshes)
        meshes.append(make_page_mesh(front_rect, back_rect, ATLAS_SIZE).gltf_mesh(f"Leaf_{leaf + 1:02}", buffer))
        node_index = len(nodes)
        nodes.append(
            {
                "name": f"Leaf_{leaf + 1:02}",
                "mesh": mesh_index,
                "translation": [0.0, first_page_y + leaf * page_step, 0.0],
            }
        )
        children.append(node_index)
        page_node_indices.append(node_index)
    nodes[0]["children"] = children

    image_bytes = ATLAS_PATH.read_bytes()
    image_view = buffer.view(image_bytes)

    identity = (0.0, 0.0, 0.0, 1.0)
    flipped = (0.0, 0.0, 1.0, 0.0)
    animations: list[dict] = []
    animations.append(glb_animation("OpenCover", [(cover_node_index, [(0.0, identity), (0.75, flipped)])], buffer))
    animations.append(glb_animation("CloseCover", [(cover_node_index, [(0.0, flipped), (0.75, identity)])], buffer))

    for page_number, node_index in enumerate(page_node_indices, start=1):
        animations.append(
            glb_animation(
                f"TurnPage_{page_number:02}",
                [(node_index, [(0.0, identity), (0.42, flipped)])],
                buffer,
            )
        )
        animations.append(
            glb_animation(
                f"ReturnPage_{page_number:02}",
                [(node_index, [(0.0, flipped), (0.42, identity)])],
                buffer,
            )
        )

    flip_duration = 0.75 + 8 * 0.50
    flip_tracks: list[tuple[int, list[tuple[float, tuple[float, float, float, float]]]]] = [
        (cover_node_index, [(0.0, identity), (0.75, flipped), (flip_duration, flipped)])
    ]
    for page_number, node_index in enumerate(page_node_indices):
        turn_start = 0.95 + page_number * 0.50
        turn_end = turn_start + 0.40
        flip_tracks.append(
            (
                node_index,
                [
                    (0.0, identity),
                    (turn_start, identity),
                    (turn_end, flipped),
                    (flip_duration, flipped),
                ],
            )
        )
    animations.append(glb_animation("OpenAndFlipThrough", flip_tracks, buffer))

    image_payload = {
        "mimeType": "image/png",
        "bufferView": image_view,
        "name": "ChayanMagazine_PrintAtlas",
    }
    document = {
        "asset": {"version": "2.0", "generator": "URMAN Chayan magazine glTF 2.0 builder"},
        "scene": 0,
        "scenes": [{"name": "ChayanMagazine", "nodes": [0]}],
        "nodes": nodes,
        "meshes": meshes,
        "materials": [
            {
                "name": "MagazinePrint",
                "doubleSided": True,
                "pbrMetallicRoughness": {
                    "baseColorFactor": [1.0, 1.0, 1.0, 1.0],
                    "baseColorTexture": {"index": 0},
                    "metallicFactor": 0.0,
                    "roughnessFactor": 0.93,
                },
            }
        ],
        "textures": [{"sampler": 0, "source": 0}],
        "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 33071, "wrapT": 33071}],
        "images": [image_payload],
        "animations": animations,
        "bufferViews": buffer.views,
        "accessors": buffer.accessors,
        "buffers": [{"byteLength": len(buffer.data)}],
        "extras": {
            "units": "meters",
            "dimensions": {"width": MAG_WIDTH, "height": MAG_HEIGHT, "closedThickness": 0.0065},
            "pageLeaves": 8,
            "dateVisible": False,
            "sourceTexture": ATLAS_PATH.name,
        },
    }
    json_bytes = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    while len(buffer.data) % 4:
        buffer.data.append(0)
    binary = bytes(buffer.data)
    total_length = 12 + 8 + len(json_bytes) + 8 + len(binary)
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    with MODEL_PATH.open("wb") as glb:
        glb.write(struct.pack("<4sII", b"glTF", 2, total_length))
        glb.write(struct.pack("<I4s", len(json_bytes), b"JSON"))
        glb.write(json_bytes)
        glb.write(struct.pack("<I4s", len(binary), b"BIN\x00"))
        glb.write(binary)


def perspective_coefficients(source_size: tuple[int, int], destination_quad: list[tuple[float, float]]) -> tuple[float, ...]:
    source_w, source_h = source_size
    source_points = [(0.0, 0.0), (source_w, 0.0), (source_w, source_h), (0.0, source_h)]
    matrix: list[list[float]] = []
    values: list[float] = []
    for (x, y), (u, v) in zip(destination_quad, source_points):
        matrix.append([x, y, 1.0, 0.0, 0.0, 0.0, -u * x, -u * y])
        values.append(u)
        matrix.append([0.0, 0.0, 0.0, x, y, 1.0, -v * x, -v * y])
        values.append(v)
    result = np.linalg.solve(np.asarray(matrix, dtype=np.float64), np.asarray(values, dtype=np.float64))
    return tuple(float(value) for value in result)


def create_preview(atlas_regions: dict[str, tuple[int, int, int, int]]) -> None:
    atlas = Image.open(ATLAS_PATH).convert("RGB")
    x0, y0, x1, y1 = atlas_regions["front_cover"]
    cover = atlas.crop((x0, y0, x1, y1))

    canvas = Image.new("RGBA", (1560, 1160), "#e8dfcf")
    draw = ImageDraw.Draw(canvas)
    draw.ellipse((260, 825, 1265, 1030), fill="#cfc4b2")
    quad = [(495, 73), (1100, 164), (1040, 910), (428, 808)]
    offset = (34, 48)
    shadow_quad = [(x + 17, y + 29) for x, y in quad]
    body_quad = [(x + offset[0], y + offset[1]) for x, y in quad]
    draw.polygon(shadow_quad, fill="#c7bdad")
    draw.polygon([quad[2], quad[3], body_quad[3], body_quad[2]], fill="#f0e8d8", outline="#9d8d86")
    draw.polygon([quad[1], quad[2], body_quad[2], body_quad[1]], fill="#e4d9c7", outline="#9d8d86")
    coefficients = perspective_coefficients(cover.size, quad)
    warped = cover.transform(canvas.size, Image.Transform.PERSPECTIVE, coefficients, Image.Resampling.BICUBIC)
    mask_source = Image.new("L", cover.size, 255)
    mask = mask_source.transform(canvas.size, Image.Transform.PERSPECTIVE, coefficients, Image.Resampling.BICUBIC)
    canvas.paste(warped, (0, 0), mask)

    draw = ImageDraw.Draw(canvas)
    draw.text((91, 103), "ЧАЯН", font=font(FONT_BOLD, 43), fill=COLORS["red"])
    draw.text((95, 158), "ИЗДАНИЕ БЕЗ ДАТЫ", font=font(FONT_BOLD, 23), fill=COLORS["blue_dark"])
    draw.line((92, 202, 355, 202), fill=COLORS["blue"], width=4)
    draw.text((1080, 1020), "8 листов  ·  19 клипов", font=font(FONT_REGULAR, 22), fill=COLORS["ink"], anchor="ra")
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(PREVIEW_PATH, format="PNG", optimize=True)


def main() -> None:
    if not SOURCE_ART.exists():
        raise SystemExit(f"Missing ImageGen source art: {SOURCE_ART}")
    if not FULL_COVER.exists():
        raise SystemExit(f"Missing ImageGen full-cover image: {FULL_COVER}")
    art = Image.open(SOURCE_ART).convert("RGB")
    full_cover = Image.open(FULL_COVER).convert("RGB")
    regions = build_atlas(art, full_cover)
    write_glb(regions)
    create_preview(regions)
    print(f"wrote {ATLAS_PATH.relative_to(ROOT)}")
    print(f"wrote {MODEL_PATH.relative_to(ROOT)}")
    print(f"wrote {PREVIEW_PATH.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
