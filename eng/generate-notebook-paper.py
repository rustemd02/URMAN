#!/usr/bin/env python3
"""Generate the document reader's notebook sheet (ACT1-UI.4).

A Soviet school exercise book, as Aidar would find it in Babai's house: an
aged squared page with the red margin rule, bound in a dark brown
calico-board cover. Output is one 9-patch texture for Godot's StyleBoxTexture:

  - top / right / bottom margins (COVER + SHADOW px) are the cover and a thin
    band of page under the cover's shadow; each band continues the centre's
    pattern, so the tiles meet without a seam;
  - the left margin is the cover plus the page strip with the red rule;
  - the centre tiles in both axes, so its grid and paper noise are periodic
    with exactly the centre size.

Deterministic: the same seed writes byte-identical pixels. No input images.

usage: eng/generate-notebook-paper.py [output.png]
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image

SEED = 19861001
CELL = 24                      # one 5 mm square at reader scale
COVER = 22                     # cover width on every side
SHADOW = 3                     # page band under the cover's inner shadow
STRIP = 72                     # page strip left of the tiling centre
CENTER_W, CENTER_H = 22 * CELL, 24 * CELL
LEFT = COVER + STRIP
EDGE = COVER + SHADOW          # top / right / bottom texture margin
W, H = LEFT + CENTER_W + EDGE, EDGE + CENTER_H + EDGE

rng = np.random.default_rng(SEED)


def periodic_noise(h, w, scale):
    """Band-limited noise that tiles with period (h, w)."""
    spectrum = np.fft.fft2(rng.standard_normal((h, w)))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    radius = np.sqrt(fx * fx + fy * fy) * scale
    field = np.real(np.fft.ifft2(spectrum * np.exp(-radius * radius)))
    field -= field.min()
    return field / max(field.max(), 1e-9)


def page(h, w, phase_x):
    """Aged squared paper; phase_x places grid columns in page coordinates."""
    base = np.array([226, 214, 186], dtype=np.float64)       # yellowed newsprint-grade page
    tone = periodic_noise(h, w, 18.0)                        # broad foxing and yellowing
    grain = periodic_noise(h, w, 1.2)                        # paper fibre grain
    img = np.ones((h, w, 3)) * base
    img -= (tone[..., None] - .5) * np.array([26, 30, 38])
    img -= (grain[..., None] - .5) * 10
    # A few soft rust-brown foxing spots from the broad field's peaks.
    spots = np.clip((tone - .78) / .22, 0, 1) ** 2
    img -= spots[..., None] * np.array([20, 27, 36])
    # Faded blue-grey printed grid: every CELL px, one px wide, broken slightly by the paper.
    ys = np.arange(h)[:, None]
    xs = np.arange(w)[None, :]
    line = ((ys % CELL) == 0) | (((xs + phase_x) % CELL) == 0)
    ink = np.array([118, 142, 168], dtype=np.float64)
    strength = .36 * (0.75 + .25 * grain)
    img = np.where(line[..., None], img * (1 - strength[..., None]) + ink * strength[..., None], img)
    return img


def cover(h, w):
    """Dark brown calico-board: fine cross weave, worn lighter at the rim."""
    base = np.array([68, 44, 30], dtype=np.float64)
    weave = (np.sin(np.arange(w)[None, :] * np.pi / 2) * np.sin(np.arange(h)[:, None] * np.pi / 2))
    mottle = periodic_noise(h, w, 10.0)
    img = np.ones((h, w, 3)) * base
    img += weave[..., None] * 5
    img += (mottle[..., None] - .5) * np.array([18, 13, 9])
    return img


def build():
    img = cover(H, W)
    # Worn outer rim: the board edge rubs lighter.
    rim = np.zeros((H, W))
    for d in range(4):
        rim[d, :] = rim[H - 1 - d, :] = rim[:, d] = rim[:, W - 1 - d] = np.maximum(rim[d, 0], (4 - d) / 4)
    img += rim[..., None] * np.array([34, 26, 18])

    # One periodic page field for the centre. The bands around it are cut
    # from the opposite side of the same field, so every tile edge continues
    # the grid and the paper; the strip shares the grid's column phase.
    centre = page(CENTER_H, CENTER_W, 0)
    rows = np.concatenate([centre[-SHADOW:], centre, centre[:SHADOW]], axis=0)
    right = np.concatenate([centre[-SHADOW:, :SHADOW], centre[:, :SHADOW], centre[:SHADOW, :SHADOW]], axis=0)
    strip_core = page(CENTER_H, STRIP, STRIP % CELL)
    strip = np.concatenate([strip_core[-SHADOW:], strip_core, strip_core[:SHADOW]], axis=0)
    top, bottom = COVER, H - COVER
    img[top:bottom, LEFT:LEFT + CENTER_W] = rows
    img[top:bottom, LEFT + CENTER_W:W - COVER] = right
    img[top:bottom, COVER:LEFT] = strip

    # Red margin rule: double pinkish-red line near the strip's right edge.
    for x, alpha in ((LEFT - 14, .78), (LEFT - 11, .55)):
        column = img[top:bottom, x]
        img[top:bottom, x] = column * (1 - alpha) + np.array([196, 70, 72]) * alpha

    # Cover casts a thin shadow on the page along its inner edge; it stays in
    # the margins, never in the tiling centre.
    for d, alpha in enumerate((.34, .2, .1)):
        img[top + d, COVER:W - COVER] *= 1 - alpha
        img[bottom - 1 - d, COVER:W - COVER] *= 1 - alpha
        img[top:bottom, COVER + d] *= 1 - alpha
        img[top:bottom, W - COVER - 1 - d] *= 1 - alpha
    return Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8), "RGB")


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else (
        Path(__file__).resolve().parent.parent / "game/assets/ui/document_notebook_sheet.png")
    out.parent.mkdir(parents=True, exist_ok=True)
    build().save(out, optimize=True)
    print(f"{out} {W}x{H} margins left={LEFT} top/right/bottom={EDGE} cell={CELL}")


if __name__ == "__main__":
    main()
