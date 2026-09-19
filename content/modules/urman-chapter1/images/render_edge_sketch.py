"""Render the three existing edge-sketch landmarks, without a new story claim.

This is an authored source-image candidate, not a capture of the current level.
Requires Pillow. Does not start Godot, import assets or overwrite an existing PNG.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


WIDTH, HEIGHT, SCALE = 1536, 1024, 3
OUTPUT = Path(__file__).with_name("kara_urman_edge_sketch_candidate_v1.png")
RNG = random.Random(20260915)


def main() -> None:
    if OUTPUT.exists():
        raise FileExistsError(f"Refusing to overwrite {OUTPUT}")

    image = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), (230, 221, 201))
    draw = ImageDraw.Draw(image, "RGBA")

    def points(values: list[tuple[float, float]]) -> list[tuple[int, int]]:
        return [(round(x * SCALE), round(y * SCALE)) for x, y in values]

    def pencil(values: list[tuple[float, float]], width: float = 1.4,
               opacity: int = 140, jitter: float = .7) -> None:
        for pass_index in range(3):
            displaced = [(x + RNG.uniform(-jitter, jitter), y + RNG.uniform(-jitter, jitter))
                         for x, y in values]
            draw.line(points(displaced), fill=(58, 57, 51, opacity // (pass_index + 1)),
                      width=max(1, round(width * SCALE)), joint="curve")

    def fill(values: list[tuple[float, float]], color: tuple[int, int, int, int]) -> None:
        draw.polygon(points(values), fill=color)

    def curve(a: tuple[float, float], b: tuple[float, float],
              c: tuple[float, float], d: tuple[float, float],
              count: int = 80) -> list[tuple[float, float]]:
        result = []
        for step in range(count + 1):
            t = step / count
            u = 1 - t
            result.append((u**3*a[0] + 3*u*u*t*b[0] + 3*u*t*t*c[0] + t**3*d[0],
                           u**3*a[1] + 3*u*u*t*b[1] + 3*u*t*t*c[1] + t**3*d[1]))
        return result

    def arrow(values: list[tuple[float, float]], width: float = 2) -> None:
        pencil(values, width, 155)
        x, y = values[-1]
        px, py = values[-4]
        angle = math.atan2(y-py, x-px)
        head = [(x - 20*math.cos(angle-.42), y - 20*math.sin(angle-.42)), (x, y),
                (x - 20*math.cos(angle+.42), y - 20*math.sin(angle+.42))]
        pencil(head, width, 155)

    def label(value: str, position: tuple[int, int], size: int = 25) -> None:
        font_candidates = (
            "/System/Library/Fonts/Supplemental/Georgia Italic.ttf",
            "/System/Library/Fonts/Supplemental/Times New Roman Italic.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Italic.ttf",
        )
        font_path = next((path for path in font_candidates if Path(path).is_file()), None)
        if font_path is None:
            raise FileNotFoundError("A local Cyrillic-capable italic font is required; no font is bundled.")
        font = ImageFont.truetype(font_path, size * SCALE)
        draw.text((position[0]*SCALE, position[1]*SCALE), value, font=font, fill=(55, 53, 47, 210))

    # The old receipt's reverse: no invented shop, date, author or inventory.
    paper = [(48,35),(1486,47),(1500,964),(72,985),(32,821),(42,280),(48,35)]
    fill(paper, (241, 234, 216, 255))
    pencil(paper, .8, 65, .5)
    for _ in range(26000):
        x, y = RNG.randint(55,1480)*SCALE, RNG.randint(55,960)*SCALE
        draw.point((x,y), fill=(91,82,60,RNG.randint(8,35)))
    for fold in [[(482,48),(479,341),(493,673),(485,972)],
                 [(45,750),(402,738),(811,746),(1493,726)]]:
        pencil(fold, 1, 25, .2)
    label("Схема кромки", (98,82), 38)

    # The dense tree line is a backdrop. It contains no figure or minaret.
    for index in range(57):
        x = 527 + index*15.3 + RNG.uniform(-12,12)
        base = 320 + 42*math.sin(index*.15) + RNG.uniform(-13,13)
        height = RNG.uniform(92,192)
        pencil([(x,base+12),(x+RNG.uniform(-3,3),base-height)], 1, 108)
        for level in range(7):
            progress = (level+1)/8
            y = base-height + progress*height
            half = 5 + progress*RNG.uniform(21,38)
            pencil([(x-half,y+14),(x,y-5),(x+half,y+17)], .85, 94)

    # The road bends along the outside of the fence. A second winding bank
    # separates the ditch from the walking route rather than becoming a river.
    road_left = curve((485,883),(438,656),(665,426),(814,299))
    road_right = curve((677,894),(565,663),(797,443),(884,310))
    pencil(road_left,2.1,150,1)
    pencil(road_right,2.1,150,1)
    for offset in (23,43,61):
        pencil(curve((506+offset,848),(477+offset,684),(656+offset,465),(822+offset*.22,340)), .7, 38,1)

    ditch_near = curve((137,704),(310,609),(333,503),(663,477))
    ditch_far = curve((146,750),(351,656),(387,541),(691,521))
    fill(ditch_near + list(reversed(ditch_far)), (100, 105, 101, 24))
    pencil(ditch_near,1.7,150)
    pencil(ditch_far,1.7,150)
    for i in range(4,len(ditch_near)-5,3):
        a,b = ditch_near[i],ditch_far[i]
        pencil([(a[0]+2,a[1]+4), (a[0]*.58+b[0]*.42,a[1]*.58+b[1]*.42)], .9,90)
    label("канава", (131,775), 24)

    # A small upright board on a post, with exactly two short horizontal cuts.
    # This is a roadside tag, never a grave marker or a cross.
    fill([(328,517),(339,516),(346,667),(334,673)], (102,87,62,40))
    pencil([(328,517),(339,516),(346,667),(334,673),(328,517)],1.5,170)
    board = [(291,486),(363,480),(368,573),(296,579),(291,486)]
    fill(board, (193,180,150,45))
    pencil(board,1.8,185)
    pencil([(311,517),(346,514)],3.4,215,.18)
    pencil([(312,544),(347,541)],3.4,215,.18)
    pencil([(305,492),(308,507)], .65, 65)
    pencil([(353,548),(355,565)], .65, 65)
    pencil([(288,671),(316,663),(352,666),(366,675)],1,60)

    # Repeated posts and rails establish the fence, while the route arrow
    # remains consistently outside it. The rejected turn is visibly crossed.
    fence = curve((908,816),(880,622),(992,402),(1084,302),50)
    pencil([(x,y-32) for x,y in fence],2.2,146)
    pencil([(x,y-57) for x,y in fence],1.8,135)
    for i in range(0,len(fence),3):
        x,y = fence[i]
        pencil([(x-3,y+9),(x-5,y-75),(x+3,y-79),(x+5,y+8)],1.2,144)
    for i in range(12):
        x,y = 1034 + RNG.uniform(-30,280), 429+i*28
        pencil([(x,y),(x+RNG.uniform(20,40),y-11)],.65,28)
    arrow(curve((715,815),(702,645),(844,436),(935,351)),2.2)
    arrow(curve((790,595),(859,589),(964,583),(1076,573)),1.4)
    pencil([(932,548),(982,613)],3.6,200,.4)
    pencil([(932,612),(983,549)],3.6,200,.4)

    # The doubled arcs illustrate the note already described in the document.
    for radius in (19,32):
        values = [(879+radius*math.cos(t), 346+radius*math.sin(t))
                  for t in [(-.8 + step*.08) for step in range(24)]]
        pencil(values,1.2,96)
    label("Р. знает, где стоять", (719,910), 30)
    image.resize((WIDTH,HEIGHT), Image.Resampling.LANCZOS).save(OUTPUT, "PNG")
    print(OUTPUT)


if __name__ == "__main__":
    main()
