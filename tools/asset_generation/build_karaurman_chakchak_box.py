#!/usr/bin/env python3
"""Build a windowed chak-chak carton with an ImageGen food cutout."""

from __future__ import annotations

import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE_FRONT = ROOT / "assets/source/illustrations/karaurman_chakchak_front_window_v1.png"
CONTENTS_IMAGE = ROOT / "assets/source/illustrations/karaurman_chakchak_contents_v02_experiment.png"
ATLAS_PATH = ROOT / "game/assets/textures/props/chakchak/karaurman_chakchak_box_atlas_v1.png"
MODEL_PATH = ROOT / "game/assets/models/props/chakchak/karaurman_chakchak_box_v1.glb"
PREVIEW_PATH = ROOT / "game/assets/models/props/chakchak/karaurman_chakchak_box_preview_v1.png"

ATLAS_SIZE = (3072, 2048)
FRONT_RECT = (64, 64, 964, 1189)       # 900 × 1125, portrait 4:5
BACK_RECT = (1016, 64, 1916, 1189)     # 900 × 1125
TOP_RECT = (1968, 64, 2868, 604)       # 900 × 540
BOTTOM_RECT = (1968, 640, 2868, 1180)  # 900 × 540
LEFT_RECT = (64, 1256, 352, 1856)      # 288 × 600
RIGHT_RECT = (400, 1256, 688, 1856)    # 288 × 600

BOX_W = 0.120
BOX_H = 0.150
BOX_D = 0.072
PAPER = (245, 234, 211)
NAVY = (30, 61, 111)
CRANBERRY = (164, 41, 54)
LEAF = (72, 116, 77)
INK = (43, 43, 43)
FONT_BOLD = ROOT / "game/assets/fonts/PT_Sans-Web-Bold.ttf"
FONT_REGULAR = ROOT / "game/assets/fonts/PT_Sans-Web-Regular.ttf"


def load_font(size: int, *, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = FONT_BOLD if bold else FONT_REGULAR
    if path.exists():
        return ImageFont.truetype(str(path), size)
    fallback = Path("/System/Library/Fonts/Supplemental/Arial.ttf")
    if not fallback.exists():
        fallback = Path("/System/Library/Fonts/Supplemental/Arial Bold.ttf")
    return ImageFont.truetype(str(fallback), size)


def contain_text(draw: ImageDraw.ImageDraw, text: str, rect: tuple[int, int, int, int], *, bold: bool = False,
                 fill: tuple[int, int, int] = INK, max_size: int = 54) -> None:
    x0, y0, x1, y1 = rect
    size = max_size
    while size > 8:
        face = load_font(size, bold=bold)
        box = draw.textbbox((0, 0), text, font=face)
        if box[2] - box[0] <= x1 - x0 and box[3] - box[1] <= y1 - y0:
            draw.text((x0 + (x1 - x0 - (box[2] - box[0])) / 2, y0 + (y1 - y0 - (box[3] - box[1])) / 2),
                      text, font=face, fill=fill)
            return
        size -= 2


def draw_tulip(draw: ImageDraw.ImageDraw, center: tuple[float, float], scale: float = 1.0,
               colors: tuple[tuple[int, int, int], ...] = (CRANBERRY, NAVY, LEAF)) -> None:
    x, y = center
    red, blue, green = colors
    w, h = 13 * scale, 17 * scale
    draw.ellipse((x - w, y - h, x, y + 2 * scale), fill=red)
    draw.ellipse((x, y - h, x + w, y + 2 * scale), fill=red)
    draw.polygon([(x - w, y - 2 * scale), (x, y + 5 * scale), (x, y - h * 1.45)], fill=red)
    draw.polygon([(x, y + 4 * scale), (x - 20 * scale, y - 6 * scale),
                  (x - 11 * scale, y + 10 * scale)], fill=green)
    draw.polygon([(x, y + 4 * scale), (x + 20 * scale, y - 6 * scale),
                  (x + 11 * scale, y + 10 * scale)], fill=green)
    draw.line((x, y + 3 * scale, x, y + 29 * scale), fill=blue, width=max(1, round(2 * scale)))


def fill_panel(size: tuple[int, int], *, color: tuple[int, int, int] = PAPER) -> Image.Image:
    return Image.new("RGB", size, color)


def front_panel_with_real_window() -> tuple[Image.Image, tuple[float, float, float, float]]:
    generated = Image.open(SOURCE_FRONT).convert("RGBA")
    alpha = generated.getchannel("A")

    # The generated image has transparent space around its ornament and a separate
    # connected transparent display opening. Flood only that interior component.
    transparent = alpha.point(lambda value: 255 if value < 16 else 0)
    opening_component = transparent.copy()
    ImageDraw.floodfill(opening_component, (generated.width // 2, round(generated.height * 0.62)), 128, thresh=0)
    opening_mask = opening_component.point(lambda value: 255 if value == 128 else 0)
    opening_mask = opening_mask.filter(ImageFilter.GaussianBlur(0.9))
    bounds = opening_mask.getbbox()
    if bounds is None:
        raise SystemExit("Could not locate the clear display window in the generated label")

    paper = Image.new("RGBA", generated.size, PAPER + (255,))
    face = Image.alpha_composite(paper, generated)
    face.putalpha(ImageChops.subtract(face.getchannel("A"), opening_mask))
    face = ImageOps.fit(face, (FRONT_RECT[2] - FRONT_RECT[0], FRONT_RECT[3] - FRONT_RECT[1]),
                        method=Image.Resampling.LANCZOS)
    normalized = tuple(bounds[i] / (generated.width if i in (0, 2) else generated.height) for i in range(4))
    return face, normalized


def make_back_panel(size: tuple[int, int]) -> Image.Image:
    panel = fill_panel(size)
    draw = ImageDraw.Draw(panel)
    margin = 22
    draw.rounded_rectangle((margin, margin, size[0] - margin, size[1] - margin), radius=18, outline=NAVY, width=5)
    draw.line((margin + 12, 84, size[0] - margin - 12, 84), fill=CRANBERRY, width=4)
    contain_text(draw, "КАРА-УРМАН ИКМӘКХАНӘСЕ", (28, 34, size[0] - 28, 78), bold=True, fill=NAVY, max_size=38)
    contain_text(draw, "ЧӘК-ЧӘК", (36, 108, size[0] - 36, 188), bold=True, fill=CRANBERRY, max_size=72)
    contain_text(draw, "ТАТАР ТӘМЕ", (36, 196, size[0] - 36, 244), bold=True, fill=NAVY, max_size=42)
    draw_tulip(draw, (size[0] // 2, 316), 1.1)
    draw.line((54, 362, size[0] - 54, 362), fill=(190, 174, 143), width=2)
    contain_text(draw, "Бал белән татлы камыр", (42, 382, size[0] - 42, 438), fill=INK, max_size=34)
    contain_text(draw, "КАРА-УРМАН", (42, size[1] - 104, size[0] - 42, size[1] - 62), bold=True, fill=NAVY, max_size=38)
    draw.text((size[0] - 54, size[1] - 50), "200 г", font=load_font(25, bold=True), fill=INK, anchor="ra")
    return panel


def make_top_panel(size: tuple[int, int]) -> Image.Image:
    panel = fill_panel(size, color=(239, 226, 199))
    draw = ImageDraw.Draw(panel)
    draw.rectangle((8, 8, size[0] - 9, size[1] - 9), outline=NAVY, width=7)
    draw.rectangle((22, 22, size[0] - 23, size[1] - 23), outline=CRANBERRY, width=3)
    for x in range(52, size[0] - 30, 58):
        draw_tulip(draw, (x, size[1] // 2), 0.55)
    # Turn the repeated border pattern to match the top-face UV orientation.
    return panel.rotate(180)


def make_side_panel(size: tuple[int, int], *, reverse: bool = False) -> Image.Image:
    panel = fill_panel(size, color=(239, 226, 199))
    draw = ImageDraw.Draw(panel)
    draw.rectangle((7, 7, size[0] - 8, size[1] - 8), outline=NAVY, width=5)
    for y in range(48, size[1] - 20, 92):
        draw_tulip(draw, (size[0] // 2, y), 0.72, (NAVY, CRANBERRY, LEAF) if reverse else (CRANBERRY, NAVY, LEAF))
    return panel


def create_atlas(front: Image.Image) -> dict[str, tuple[int, int, int, int]]:
    atlas = Image.new("RGBA", ATLAS_SIZE, PAPER + (255,))
    atlas.paste(front, FRONT_RECT[:2])

    back_size = (BACK_RECT[2] - BACK_RECT[0], BACK_RECT[3] - BACK_RECT[1])
    atlas.paste(make_back_panel(back_size), BACK_RECT[:2])
    top_size = (TOP_RECT[2] - TOP_RECT[0], TOP_RECT[3] - TOP_RECT[1])
    atlas.paste(make_top_panel(top_size), TOP_RECT[:2])
    bottom_size = (BOTTOM_RECT[2] - BOTTOM_RECT[0], BOTTOM_RECT[3] - BOTTOM_RECT[1])
    bottom = fill_panel(bottom_size, color=(226, 214, 189))
    ImageDraw.Draw(bottom).rectangle((5, 5, bottom.width - 6, bottom.height - 6), outline=NAVY, width=5)
    atlas.paste(bottom, BOTTOM_RECT[:2])
    side_size = (LEFT_RECT[2] - LEFT_RECT[0], LEFT_RECT[3] - LEFT_RECT[1])
    atlas.paste(make_side_panel(side_size), LEFT_RECT[:2])
    atlas.paste(make_side_panel(side_size, reverse=True), RIGHT_RECT[:2])

    ATLAS_PATH.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(ATLAS_PATH, format="PNG", optimize=True)
    return {
        "front": FRONT_RECT,
        "back": BACK_RECT,
        "top": TOP_RECT,
        "bottom": BOTTOM_RECT,
        "left": LEFT_RECT,
        "right": RIGHT_RECT,
    }


class Mesh:
    def __init__(self) -> None:
        self.positions: list[tuple[float, float, float]] = []
        self.normals: list[tuple[float, float, float]] = []
        self.uvs: list[tuple[float, float]] = []
        self.indices: list[int] = []

    def quad(self, points: list[tuple[float, float, float]], normal: tuple[float, float, float],
             rect: tuple[int, int, int, int] | None = None, *, reverse: bool = False) -> None:
        start = len(self.positions)
        self.positions.extend(points)
        self.normals.extend([normal] * 4)
        if rect is None:
            self.uvs.extend([(0.0, 1.0), (1.0, 1.0), (1.0, 0.0), (0.0, 0.0)])
        else:
            x0, y0, x1, y1 = rect
            u0, v0 = x0 / ATLAS_SIZE[0], y0 / ATLAS_SIZE[1]
            u1, v1 = x1 / ATLAS_SIZE[0], y1 / ATLAS_SIZE[1]
            self.uvs.extend([(u0, v1), (u1, v1), (u1, v0), (u0, v0)])
        if reverse:
            self.indices.extend((start, start + 2, start + 1, start, start + 3, start + 2))
        else:
            self.indices.extend((start, start + 1, start + 2, start, start + 2, start + 3))

    def gltf_mesh(self, name: str, buffer: "GltfBuffer", material: int) -> dict:
        positions = buffer.float_accessor(self.positions, "VEC3", target=34962, bounds=True)
        normals = buffer.float_accessor(self.normals, "VEC3", target=34962)
        uvs = buffer.float_accessor(self.uvs, "VEC2", target=34962)
        packed = struct.pack("<" + "H" * len(self.indices), *self.indices)
        indices = buffer.raw_accessor(packed, "SCALAR", 5123, len(self.indices), target=34963)
        return {"name": name, "primitives": [{
            "attributes": {"POSITION": positions, "NORMAL": normals, "TEXCOORD_0": uvs},
            "indices": indices, "material": material,
        }]}


def build_box_mesh(regions: dict[str, tuple[int, int, int, int]]) -> Mesh:
    x0, x1 = -BOX_W / 2, BOX_W / 2
    y0, y1 = 0.0, BOX_H
    z0, z1 = -BOX_D / 2, BOX_D / 2
    mesh = Mesh()
    mesh.quad([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1), regions["front"], reverse=True)
    mesh.quad([(x1, y0, z1), (x0, y0, z1), (x0, y1, z1), (x1, y1, z1)], (0, 0, 1), regions["back"], reverse=True)
    mesh.quad([(x0, y0, z1), (x0, y0, z0), (x0, y1, z0), (x0, y1, z1)], (-1, 0, 0), regions["left"])
    mesh.quad([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0), regions["right"])
    mesh.quad([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0), regions["top"], reverse=True)
    mesh.quad([(x0, y0, z1), (x1, y0, z1), (x1, y0, z0), (x0, y0, z0)], (0, -1, 0), regions["bottom"], reverse=True)
    return mesh


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
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(raw)}
        if target is not None:
            view["target"] = target
        self.views.append(view)
        return len(self.views) - 1

    def raw_accessor(self, raw: bytes, kind: str, component: int, count: int, *, target: int | None = None,
                     minimum: list[float] | None = None, maximum: list[float] | None = None) -> int:
        view_index = self.view(raw, target)
        accessor = {"bufferView": view_index, "componentType": component, "count": count, "type": kind}
        if minimum is not None:
            accessor["min"] = minimum
        if maximum is not None:
            accessor["max"] = maximum
        self.accessors.append(accessor)
        return len(self.accessors) - 1

    def float_accessor(self, values: list[tuple[float, ...]], kind: str, *, target: int | None = None,
                       bounds: bool = False) -> int:
        flat = [value for row in values for value in row]
        raw = struct.pack("<" + "f" * len(flat), *flat)
        minimum = maximum = None
        if bounds and values:
            axes = len(values[0])
            minimum = [min(row[i] for row in values) for i in range(axes)]
            maximum = [max(row[i] for row in values) for i in range(axes)]
        return self.raw_accessor(raw, kind, 5126, len(values), target=target, minimum=minimum, maximum=maximum)


def window_box(normalized: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    x0, y0, x1, y1 = normalized
    left = -BOX_W / 2 + x0 * BOX_W
    right = -BOX_W / 2 + x1 * BOX_W
    top = BOX_H - y0 * BOX_H
    bottom = BOX_H - y1 * BOX_H
    return left, bottom, right, top


def make_window_mesh(buffer: GltfBuffer, window: tuple[float, float, float, float]) -> tuple[dict, dict]:
    x0, y0, x1, y1 = window
    inset_x = 0.0015
    inset_y = 0.0015
    z = -BOX_D / 2 - 0.00014
    mesh = Mesh()
    mesh.quad([(x0 + inset_x, y0 + inset_y, z), (x1 - inset_x, y0 + inset_y, z),
               (x1 - inset_x, y1 - inset_y, z), (x0 + inset_x, y1 - inset_y, z)], (0, 0, -1), reverse=True)
    gltf_mesh = mesh.gltf_mesh("ClearPETWindow", buffer, 2)
    node = {"name": "TransparentWindowFilm", "mesh": 2}
    return gltf_mesh, node


def make_contents_mesh(buffer: GltfBuffer, window: tuple[float, float, float, float]) -> tuple[dict, dict]:
    x0, y0, x1, y1 = window
    inset_x = 0.0022
    inset_y = 0.0022
    z = -BOX_D / 2 + 0.00042
    mesh = Mesh()
    mesh.quad([(x0 + inset_x, y0 + inset_y, z), (x1 - inset_x, y0 + inset_y, z),
               (x1 - inset_x, y1 - inset_y, z), (x0 + inset_x, y1 - inset_y, z)], (0, 0, -1), reverse=True)
    return mesh.gltf_mesh("ImageGenChakchakContents", buffer, 1), {
        "name": "ChakchakVisibleThroughWindow",
        "mesh": 1,
    }


def write_glb(regions: dict[str, tuple[int, int, int, int]], window: tuple[float, float, float, float]) -> None:
    buffer = GltfBuffer()
    box_mesh = build_box_mesh(regions)
    contents_mesh, contents_node = make_contents_mesh(buffer, window)
    film_mesh, film_node = make_window_mesh(buffer, window)
    meshes = [box_mesh.gltf_mesh("PrintedCartonWithWindow", buffer, 0), contents_mesh, film_mesh]
    nodes = [{"name": "KaraUrmanChakchakBox", "children": []}, {"name": "PrintedCarton", "mesh": 0}]
    children = [1]
    nodes.extend((contents_node, film_node))
    children.extend((2, 3))
    nodes[0]["children"] = children

    atlas_bytes = ATLAS_PATH.read_bytes()
    image_view = buffer.view(atlas_bytes)
    contents_view = buffer.view(CONTENTS_IMAGE.read_bytes())
    document = {
        "asset": {"version": "2.0", "generator": "URMAN Kara-Urman chak-chak window-box builder"},
        "scene": 0,
        "scenes": [{"name": "KaraUrmanChakchakPackage", "nodes": [0]}],
        "nodes": nodes,
        "meshes": meshes,
        "materials": [
            {"name": "KaraUrmanPrintedCardboard", "doubleSided": True, "alphaMode": "BLEND",
             "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "baseColorTexture": {"index": 0},
                                        "metallicFactor": 0, "roughnessFactor": 0.86}},
            {"name": "KaraUrmanChakchakIllustration", "doubleSided": True, "alphaMode": "BLEND",
             "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "baseColorTexture": {"index": 1},
                                        "metallicFactor": 0, "roughnessFactor": 0.72}},
            {"name": "ClearPETFilm", "doubleSided": True, "alphaMode": "BLEND",
             "pbrMetallicRoughness": {"baseColorFactor": [0.78, 0.92, 0.98, 0.16], "metallicFactor": 0.08, "roughnessFactor": 0.18}},
        ],
        "textures": [{"sampler": 0, "source": 0}, {"sampler": 0, "source": 1}],
        "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 33071, "wrapT": 33071}],
        "images": [
            {"mimeType": "image/png", "bufferView": image_view, "name": "KaraUrmanChakchakPrintAtlas"},
            {"mimeType": "image/png", "bufferView": contents_view, "name": "KaraUrmanChakchakContents"},
        ],
        "bufferViews": buffer.views,
        "accessors": buffer.accessors,
        "buffers": [{"byteLength": len(buffer.data)}],
        "extras": {"units": "meters", "dimensions": {"width": BOX_W, "height": BOX_H, "depth": BOX_D},
                   "contents": "alpha-textured ImageGen illustration behind PET window",
                   "visibleThroughWindow": True, "manufacturerIsFictional": True,
                   "sourceTexture": ATLAS_PATH.name, "contentsImage": CONTENTS_IMAGE.name},
    }
    json_bytes = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    while len(buffer.data) % 4:
        buffer.data.append(0)
    binary = bytes(buffer.data)
    total = 12 + 8 + len(json_bytes) + 8 + len(binary)
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    with MODEL_PATH.open("wb") as glb:
        glb.write(struct.pack("<4sII", b"glTF", 2, total))
        glb.write(struct.pack("<I4s", len(json_bytes), b"JSON"))
        glb.write(json_bytes)
        glb.write(struct.pack("<I4s", len(binary), b"BIN\x00"))
        glb.write(binary)


def perspective_coefficients(destination: list[tuple[float, float]], source_size: tuple[int, int]) -> tuple[float, ...]:
    source_w, source_h = source_size
    source = [(0.0, 0.0), (source_w, 0.0), (source_w, source_h), (0.0, source_h)]
    matrix: list[list[float]] = []
    values: list[float] = []
    for (x, y), (u, v) in zip(destination, source):
        matrix.append([x, y, 1, 0, 0, 0, -u * x, -u * y]); values.append(u)
        matrix.append([0, 0, 0, x, y, 1, -v * x, -v * y]); values.append(v)
    result = np.linalg.solve(np.asarray(matrix, dtype=np.float64), np.asarray(values, dtype=np.float64))
    return tuple(float(value) for value in result)


def warp_to_canvas(image: Image.Image, canvas: Image.Image, quad: list[tuple[int, int]]) -> None:
    coeff = perspective_coefficients(quad, image.size)
    warped = image.transform(canvas.size, Image.Transform.PERSPECTIVE, coeff, Image.Resampling.BICUBIC)
    alpha = image.getchannel("A") if image.mode == "RGBA" else Image.new("L", image.size, 255)
    mask = alpha.transform(canvas.size, Image.Transform.PERSPECTIVE, coeff, Image.Resampling.BICUBIC)
    canvas.alpha_composite(warped.convert("RGBA"), (0, 0)) if mask.getbbox() is None else canvas.paste(warped, (0, 0), mask)


def create_preview(front: Image.Image, regions: dict[str, tuple[int, int, int, int]], contents_image: Image.Image,
                   normalized_window: tuple[float, float, float, float]) -> None:
    width, height = 1360, 1040
    canvas = Image.new("RGBA", (width, height), (235, 229, 217, 255))
    draw = ImageDraw.Draw(canvas)
    draw.ellipse((265, 800, 1125, 984), fill=(205, 196, 180, 255))

    front_quad = [(390, 196), (875, 260), (850, 864), (346, 790)]
    back_quad = [(452, 118), (945, 186), (921, 790), (408, 711)]
    draw.polygon([front_quad[0], front_quad[1], back_quad[1], back_quad[0]], fill=(225, 213, 189, 255), outline=(117, 104, 83, 255))
    draw.polygon([front_quad[1], back_quad[1], back_quad[2], front_quad[2]], fill=(218, 205, 180, 255), outline=(117, 104, 83, 255))

    atlas = Image.open(ATLAS_PATH).convert("RGBA")
    top = atlas.crop(regions["top"])
    top_quad = [front_quad[0], front_quad[1], back_quad[1], back_quad[0]]
    warp_to_canvas(top, canvas, top_quad)
    side = atlas.crop(regions["right"])
    side_quad = [front_quad[1], back_quad[1], back_quad[2], front_quad[2]]
    warp_to_canvas(side, canvas, side_quad)

    front_size = front.size
    content = Image.new("RGBA", front_size, (0, 0, 0, 0))
    x0, y0, x1, y1 = normalized_window
    aperture = (round(x0 * front_size[0]), round(y0 * front_size[1]),
                round(x1 * front_size[0]), round(y1 * front_size[1]))
    contents = contents_image.resize((aperture[2] - aperture[0], aperture[3] - aperture[1]), Image.Resampling.LANCZOS)
    content.alpha_composite(contents, aperture[:2])
    content.alpha_composite(front)
    # A faint film sheen is rendered only on the clear area in this illustrative preview.
    sheen = Image.new("RGBA", front_size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(sheen)
    sx0, sy0, sx1, sy1 = aperture
    sd.line((sx0 + 55, sy0 + 23, sx0 + 180, sy0 + 8), fill=(255, 255, 255, 94), width=7)
    sd.line((sx1 - 35, sy1 - 74, sx1 - 17, sy1 - 28), fill=(255, 255, 255, 58), width=5)
    content.alpha_composite(sheen)
    warp_to_canvas(content, canvas, front_quad)

    out = canvas.convert("RGB")
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    out.save(PREVIEW_PATH, format="PNG", optimize=True)


def main() -> None:
    if not SOURCE_FRONT.exists():
        raise SystemExit(f"Missing ImageGen label: {SOURCE_FRONT}")
    if not CONTENTS_IMAGE.exists():
        raise SystemExit(f"Missing ImageGen contents cutout: {CONTENTS_IMAGE}")
    front, normalized_window = front_panel_with_real_window()
    regions = create_atlas(front)
    window = window_box(normalized_window)
    write_glb(regions, window)
    create_preview(front, regions, Image.open(CONTENTS_IMAGE).convert("RGBA"), normalized_window)
    print(f"wrote {ATLAS_PATH.relative_to(ROOT)}")
    print(f"wrote {MODEL_PATH.relative_to(ROOT)} with alpha-textured chak-chak contents behind clear film")
    print(f"wrote {PREVIEW_PATH.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
