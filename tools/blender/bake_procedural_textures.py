#!/usr/bin/env python3
"""Deterministic procedural albedo bake for URMAN painterly texture cards.

Run inside Blender (its bundled Python has numpy; bpy itself is not used):

  blender --background --factory-startup --python tools/blender/bake_procedural_textures.py \
      -- --root <repo> [--only S03,S05,...]

Also runs under plain `python3` with numpy.

Every card is a periodic function of (u, v) in [0, 1)^2: all noise is built from
integer-lattice gradient noise, integer-period Voronoi cells and wrapped splats, and
domain warps are themselves periodic, so the 1024 x 1024 result tiles on both axes.
Output: 8-bit sRGB RGB PNG written with zlib (no bpy.data.images, no timestamps), so
equal seeds give byte-identical files. Third-party sources: none (project-original).
"""
import argparse
import hashlib
import json
import os
import struct
import sys
import zlib

import numpy as np

N = 1024
GENERATOR = "tools/blender/bake_procedural_textures.py"
OUT_DIR = "game/assets/textures/painterly"
RECEIPT = "procedural_bake_receipt_v1.json"
LICENCE = "project-original procedural (no third-party source)"


# ---------------------------------------------------------------- periodic primitives
def grid():
    u = np.arange(N, dtype=np.float64) / N
    return np.broadcast_to(u[None, :], (N, N)).copy(), np.broadcast_to(u[:, None], (N, N)).copy()


def _fade(t):
    return t * t * t * (t * (t * 6 - 15) + 10)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def perlin(seed, fx, fy, X, Y):
    """Periodic gradient noise, roughly [-1, 1]; X, Y in turns (period 1)."""
    r = np.random.default_rng(seed)
    ang = r.random((fy, fx)) * 2 * np.pi
    gx, gy = np.cos(ang), np.sin(ang)
    px, py = X * fx, Y * fy
    x0, y0 = np.floor(px), np.floor(py)
    tx, ty = px - x0, py - y0
    i0 = x0.astype(np.int64) % fx
    j0 = y0.astype(np.int64) % fy
    i1, j1 = (i0 + 1) % fx, (j0 + 1) % fy
    n00 = gx[j0, i0] * tx + gy[j0, i0] * ty
    n10 = gx[j0, i1] * (tx - 1) + gy[j0, i1] * ty
    n01 = gx[j1, i0] * tx + gy[j1, i0] * (ty - 1)
    n11 = gx[j1, i1] * (tx - 1) + gy[j1, i1] * (ty - 1)
    u, v = _fade(tx), _fade(ty)
    a = n00 + (n10 - n00) * u
    b = n01 + (n11 - n01) * u
    return (a + (b - a) * v) * 1.41


def fbm(seed, fx, fy, octaves, X, Y, gain=0.5):
    total, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        total = total + amp * perlin(seed + o * 977, fx * 2 ** o, fy * 2 ** o, X, Y)
        norm += amp
        amp *= gain
    return total / norm


def warp(seed, X, Y, fx, fy, amp):
    return (X + amp * perlin(seed, fx, fy, X, Y),
            Y + amp * perlin(seed + 31, fx, fy, X, Y))


def blur(img, sigma):
    """Circular gaussian blur (FFT) so periodicity is preserved."""
    fx = np.fft.fftfreq(N)[None, :]
    fy = np.fft.fftfreq(N)[:, None]
    kern = np.exp(-2 * np.pi ** 2 * sigma ** 2 * (fx ** 2 + fy ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(img) * kern))


def tri(x):
    """Periodic triangle wave, period 1, range [0, 1]."""
    return np.abs(2 * (x - np.floor(x + 0.5)))


def ramp(t, stops):
    t = np.clip(t, 0.0, 1.0)
    pos = [s[0] for s in stops]
    out = np.empty(t.shape + (3,))
    for c in range(3):
        out[..., c] = np.interp(t, pos, [s[1][c] for s in stops])
    return out


def mix(a, b, t):
    return a + (b - a) * t[..., None]


def rgb(*c):
    return np.array(c, dtype=np.float64)


def voronoi(seed, gx, gy, X, Y, jx=0.85, jy=0.85):
    """Periodic Voronoi: returns F1, F2 (cell units), nearest-cell random id, offset to feature."""
    r = np.random.default_rng(seed)
    pxs, pys, ids = r.random((gy, gx)), r.random((gy, gx)), r.random((gy, gx))
    px, py = X * gx, Y * gy
    cx, cy = np.floor(px).astype(np.int64), np.floor(py).astype(np.int64)
    fxp, fyp = px - cx, py - cy
    f1 = np.full(X.shape, 9.0)
    f2 = np.full(X.shape, 9.0)
    idn = np.zeros(X.shape)
    ox = np.zeros(X.shape)
    oy = np.zeros(X.shape)
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            ii, jj = (cx + di) % gx, (cy + dj) % gy
            sx = di + 0.5 + (pxs[jj, ii] - 0.5) * jx
            sy = dj + 0.5 + (pys[jj, ii] - 0.5) * jy
            d = np.hypot(fxp - sx, fyp - sy)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            idn = np.where(closer, ids[jj, ii], idn)
            ox = np.where(closer, fxp - sx, ox)
            oy = np.where(closer, fyp - sy, oy)
            f1 = np.minimum(f1, d)
    return f1, f2, idn, ox, oy


def splats(rng, items, canvas_shape=(N, N)):
    """Stamp wrapped soft shapes. items: iterable of (cx, cy, half_extent, fn(dx, dy)->alpha, tone).
    Returns (alpha, tone) canvases; alpha composited by max, tone from the strongest stamp."""
    A = np.zeros(canvas_shape)
    T = np.zeros(canvas_shape)
    for cx, cy, h, fn, tone in items:
        h = int(np.ceil(h))
        xs = np.arange(int(cx) - h, int(cx) + h + 1)
        ys = np.arange(int(cy) - h, int(cy) + h + 1)
        dx = (xs - cx)[None, :]
        dy = (ys - cy)[:, None]
        a = fn(dx, dy)
        xi, yi = xs % N, ys % N
        sub = A[np.ix_(yi, xi)]
        win = a > sub
        A[np.ix_(yi, xi)] = np.where(win, a, sub)
        T[np.ix_(yi, xi)] = np.where(win, tone, T[np.ix_(yi, xi)])
    return A, T


def soft_disc(rad, edge=0.55):
    def fn(dx, dy):
        d = np.hypot(dx, dy) / rad
        return 1.0 - smoothstep(edge, 1.0, d)
    return fn


def stroke(length, width, ang, bend):
    ca, sa = np.cos(ang), np.sin(ang)

    def fn(dx, dy):
        al = dx * ca + dy * sa
        ac = -dx * sa + dy * ca - bend * (al / max(length, 1e-6)) ** 2 * length
        along = np.clip(1.0 - (np.abs(al) / length) ** 3, 0, 1)
        across = 1.0 - smoothstep(0.35, 1.0, np.abs(ac) / width)
        return along * across
    return fn


def finish(img, seed, dither=0.8):
    r = np.random.default_rng(seed + 424242)
    img = img + (r.random(img.shape) - 0.5) * 2 * dither
    return np.clip(np.rint(img), 0, 255).astype(np.uint8)


# ---------------------------------------------------------------- PNG writer
def write_png(path, arr):
    h, w, _ = arr.shape
    raw = b"".join(b"\x00" + arr[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(raw, 9))
           + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


# ---------------------------------------------------------------- cards
def card_s03(seed):
    """Compacted footpath snow at doors, 1 m / repeat."""
    X, Y = grid()
    Xw, Yw = warp(seed + 1, X, Y, 3, 3, 0.07)
    big = fbm(seed + 2, 3, 3, 4, Xw, Yw)
    comp = smoothstep(-0.10, 0.38, big)
    field = fbm(seed + 3, 2, 2, 3, X, Y)
    light = rgb(224, 232, 240)
    cool = rgb(196, 211, 228)
    pressed = rgb(172, 188, 208)
    col = mix(np.broadcast_to(light, (N, N, 3)).copy(), np.broadcast_to(cool, (N, N, 3)).copy(),
              0.5 + 0.5 * field)
    col = mix(col, np.broadcast_to(pressed, (N, N, 3)).copy(), comp * 0.8)
    # broad soft pigment mottling
    col *= (1.0 + 0.025 * fbm(seed + 4, 7, 7, 3, Xw, Yw))[..., None]
    # faint boot-tread rhythm: broken chevrons only inside a few compressed patches
    Xt, Yt = warp(seed + 5, X, Y, 6, 6, 0.06)
    phase = 22 * Yt + 1.6 * tri(4 * Xt + 0.6 * perlin(seed + 6, 4, 4, X, Y)) \
        + 1.2 * perlin(seed + 12, 9, 9, X, Y)
    tread = blur(0.5 + 0.5 * np.sin(2 * np.pi * phase), 3.2)
    where = smoothstep(0.25, 0.65, fbm(seed + 7, 4, 4, 2, X, Y)) * comp
    col *= (1.0 - 0.030 * (tread - 0.5) * 2 * where)[..., None]
    # sparse dull grit, never mud
    rng = np.random.default_rng(seed + 8)
    items = []
    for _ in range(140):
        items.append((rng.integers(0, N), rng.integers(0, N), 4, soft_disc(rng.uniform(0.9, 1.9)),
                      rng.uniform(0.35, 0.65)))
    A, T = splats(rng, items)
    col = mix(col, np.broadcast_to(rgb(142, 138, 134), (N, N, 3)).copy(), A * T)
    return finish(col, seed)


def card_s05(seed):
    """Frozen, slightly dirty roadside / wheel snow, 1 m / repeat."""
    X, Y = grid()
    Xw, Yw = warp(seed + 1, X, Y, 3, 3, 0.06)
    t = 0.5 + 0.5 * fbm(seed + 2, 3, 3, 4, Xw, Yw)
    streak = fbm(seed + 3, 16, 2, 3, Xw, Yw)  # longitudinal wear along v
    t = np.clip(t + 0.10 * streak, 0, 1)
    col = ramp(t, [(0.0, rgb(150, 162, 176)), (0.45, rgb(176, 188, 201)), (1.0, rgb(204, 212, 222))])
    # flat irregular patches of trapped fine grit: grey-taupe tint (not brown)
    g = smoothstep(0.18, 0.55, fbm(seed + 4, 5, 5, 4, Xw, Yw))
    col = mix(col, np.broadcast_to(rgb(150, 152, 154), (N, N, 3)).copy(), 0.30 * g)
    # dense tiny grit only inside those patches
    hf = perlin(seed + 5, 340, 340, X, Y)
    pix = smoothstep(0.40, 0.78, hf) * g
    col = mix(col, np.broadcast_to(rgb(104, 106, 108), (N, N, 3)).copy(), 0.55 * pix)
    # sparse fine specks outside
    rng = np.random.default_rng(seed + 6)
    items = [(rng.integers(0, N), rng.integers(0, N), 3, soft_disc(rng.uniform(0.8, 1.5)),
              rng.uniform(0.4, 0.75)) for _ in range(380)]
    A, T = splats(rng, items)
    col = mix(col, np.broadcast_to(rgb(100, 100, 102), (N, N, 3)).copy(), A * T)
    return finish(col, seed)


def card_s08(seed):
    """Matte small ice patch, 1 m / repeat."""
    X, Y = grid()
    Xw, Yw = warp(seed + 1, X, Y, 3, 3, 0.08)
    t = 0.5 + 0.5 * fbm(seed + 2, 2, 2, 4, Xw, Yw)
    col = ramp(t, [(0.0, rgb(130, 154, 163)), (0.5, rgb(150, 172, 180)), (1.0, rgb(170, 190, 196))])
    # milky cloud islands
    cloud = smoothstep(0.12, 0.62, fbm(seed + 3, 4, 4, 4, Xw, Yw))
    layering = fbm(seed + 4, 5, 22, 3, Xw, Yw)
    col = mix(col, np.broadcast_to(rgb(204, 217, 220), (N, N, 3)).copy(),
              np.clip(0.60 * cloud + 0.10 * layering, 0, 0.8))
    # milky air bubbles, clustered where the ice is cloudy
    rng = np.random.default_rng(seed + 5)
    items = []
    while len(items) < 260:
        x, y = rng.integers(0, N), rng.integers(0, N)
        if rng.random() > 0.15 + 0.85 * cloud[y, x]:
            continue
        r = float(np.clip(rng.gamma(2.0, 2.2) + 1.2, 1.2, 9.5))
        items.append((x, y, r + 2, soft_disc(r, 0.30), rng.uniform(0.35, 0.60)))
    A, T = splats(rng, items)
    col = mix(col, np.broadcast_to(rgb(228, 236, 236), (N, N, 3)).copy(), A * T)
    # fine hairline cracks (frost-white), broken up so no radial drama
    Xc, Yc = warp(seed + 6, X, Y, 3, 3, 0.05)
    n1 = fbm(seed + 7, 3, 3, 3, Xc, Yc)
    n2 = fbm(seed + 8, 5, 4, 3, Xc, Yc)
    m1 = smoothstep(0.0, 0.45, fbm(seed + 9, 3, 3, 2, X, Y))
    crack = np.exp(-(n1 / 0.010) ** 2) * m1 + 0.55 * np.exp(-(n2 / 0.008) ** 2) * (1 - m1) * 0.8
    col = mix(col, np.broadcast_to(rgb(214, 226, 232), (N, N, 3)).copy(), np.clip(crack * 0.45, 0, 0.5))
    return finish(col, seed)


def card_s09(seed):
    """Snow with scattered dry grass inclusions, 2 m / repeat."""
    X, Y = grid()
    Xw, Yw = warp(seed + 1, X, Y, 2, 2, 0.08)
    t = 0.5 + 0.5 * fbm(seed + 2, 3, 3, 4, Xw, Yw)
    col = ramp(t, [(0.0, rgb(196, 208, 222)), (0.5, rgb(218, 226, 236)), (1.0, rgb(238, 242, 246))])
    col *= (1.0 + 0.02 * fbm(seed + 3, 9, 9, 3, X, Y))[..., None]
    rng = np.random.default_rng(seed + 4)
    palette = [rgb(176, 164, 138), rgb(158, 150, 132), rgb(190, 178, 148), rgb(150, 140, 120)]
    items, ids = [], []
    for _ in range(16):  # clusters
        cx, cy = rng.integers(0, N), rng.integers(0, N)
        base = rng.uniform(0, np.pi)
        for _k in range(int(rng.integers(3, 9))):
            ang = base + rng.normal(0, 0.55)
            x, y = cx + rng.normal(0, 26), cy + rng.normal(0, 26)
            L = rng.uniform(9, 30)
            items.append((x, y, L + 6, stroke(L, rng.uniform(1.0, 2.0), ang, rng.normal(0, 0.12)),
                          int(rng.integers(0, len(palette))) + rng.uniform(0.2, 0.95)))
    for _ in range(18):  # singles
        ang = rng.uniform(0, np.pi)
        L = rng.uniform(8, 20)
        items.append((rng.integers(0, N), rng.integers(0, N), L + 6,
                      stroke(L, rng.uniform(1.0, 1.8), ang, rng.normal(0, 0.1)),
                      int(rng.integers(0, len(palette))) + rng.uniform(0.2, 0.9)))
    A, T = splats(rng, items)
    idx = np.floor(T).astype(int)
    strength = (T - idx) * A
    straw = np.array(palette)[idx]
    # soft thin-snow halo around the inclusions (no cast shadow)
    halo = np.clip(blur(A, 6.0) * 3.0, 0, 1)
    col = mix(col, np.broadcast_to(rgb(204, 212, 222), (N, N, 3)).copy(), 0.30 * halo)
    col = col * (1 - strength[..., None]) + straw * strength[..., None]
    return finish(col, seed)


def _planks(X, count):
    xl = (X * count) % 1.0
    idx = np.floor(X * count).astype(np.int64) % count
    return xl, idx


def card_w07(seed):
    """Dark weathered shed boards, 1 m / repeat, 6 planks (~0.167 m)."""
    X, Y = grid()
    count = 6
    xl, idx = _planks(X, count)
    rng = np.random.default_rng(seed)
    tone = rng.uniform(0.90, 1.10, count)[idx]
    warm = rng.uniform(-4.0, 4.0, count)[idx]
    yo = rng.random(count)[idx]
    xo = rng.random(count)[idx]
    Xw = X + 0.006 * perlin(seed + 1, 3, 2, X, Y) + xo
    Yw = Y + yo
    grain = (0.55 * perlin(seed + 2, 30, 3, Xw, Yw)
             + 0.30 * perlin(seed + 3, 96, 4, Xw, Yw)
             + 0.15 * perlin(seed + 4, 230, 5, Xw, Yw))
    t = np.clip(0.5 + 0.55 * grain, 0, 1)
    col = ramp(t, [(0.0, rgb(70, 63, 59)), (0.5, rgb(94, 87, 80)), (1.0, rgb(120, 112, 103))])
    col *= tone[..., None]
    col[..., 0] += warm
    col[..., 2] -= warm
    # weathering streaks (vertical) and broad moisture discolouration
    streak = fbm(seed + 5, 44, 2, 3, Xw, Y)
    col *= (1.0 + 0.10 * streak)[..., None]
    wet = smoothstep(0.10, 0.50, fbm(seed + 6, 3, 2, 3, X, Y))
    col = mix(col, np.broadcast_to(rgb(70, 70, 70), (N, N, 3)).copy(), 0.30 * wet)
    # faint local silver fibres
    fib = smoothstep(0.52, 0.80, perlin(seed + 7, 210, 3, Xw, Yw))
    loc = smoothstep(0.0, 0.5, fbm(seed + 8, 4, 3, 2, X, Y))
    col = mix(col, np.broadcast_to(rgb(150, 148, 142), (N, N, 3)).copy(), 0.55 * fib * loc)
    # plank seams: narrow and soft, never a black gap
    d = np.minimum(xl, 1 - xl) * (N / count)
    seam = 0.38 * np.exp(-(d / 1.8) ** 2) + 0.10 * np.exp(-(d / 7.0) ** 2)
    col *= (1.0 - seam)[..., None]
    return finish(col, seed)


def card_w10(seed):
    """Light linden bathhouse boards, 0.75 m / repeat, 6 boards (0.125 m), seams kept soft."""
    X, Y = grid()
    count = 6
    xl, idx = _planks(X, count)
    rng = np.random.default_rng(seed)
    tone = rng.uniform(0.97, 1.03, count)[idx]
    yo = rng.random(count)[idx]
    xo = rng.random(count)[idx]
    Xw = X + 0.004 * perlin(seed + 1, 3, 2, X, Y) + xo
    Yw = Y + yo
    grain = (0.45 * perlin(seed + 2, 42, 2, Xw, Yw)
             + 0.35 * perlin(seed + 3, 130, 3, Xw, Yw)
             + 0.20 * perlin(seed + 4, 300, 4, Xw, Yw))
    t = np.clip(0.52 + 0.80 * grain, 0, 1)
    col = ramp(t, [(0.0, rgb(184, 148, 100)), (0.5, rgb(218, 190, 142)), (1.0, rgb(236, 218, 178))])
    col *= tone[..., None]
    use = smoothstep(0.05, 0.55, fbm(seed + 5, 3, 2, 3, X, Y))
    col = mix(col, np.broadcast_to(rgb(190, 150, 96), (N, N, 3)).copy(), 0.16 * use)
    col *= (1.0 + 0.03 * fbm(seed + 6, 8, 3, 3, Xw, Yw))[..., None]
    d = np.minimum(xl, 1 - xl) * (N / count)
    seam = 0.11 * np.exp(-(d / 2.2) ** 2) + 0.04 * np.exp(-(d / 9.0) ** 2)
    col *= (1.0 - seam)[..., None]
    return finish(col, seed)


def card_w11(seed):
    """Split firewood side faces, 0.5 m / repeat: long torn fibres, bark-edge streaks."""
    X, Y = grid()
    Xw = X + 0.020 * perlin(seed + 1, 5, 3, X, Y) + 0.007 * perlin(seed + 2, 38, 2, X, Y)
    Yw = Y
    fibre = (0.45 * perlin(seed + 3, 60, 4, Xw, Yw)
             + 0.35 * perlin(seed + 4, 150, 6, Xw, Yw)
             + 0.20 * perlin(seed + 5, 320, 8, Xw, Yw))
    t = np.clip(0.5 + 0.55 * fibre + 0.12 * fbm(seed + 6, 3, 2, 3, X, Y), 0, 1)
    col = ramp(t, [(0.0, rgb(196, 160, 110)), (0.5, rgb(224, 196, 148)), (1.0, rgb(240, 222, 178))])
    # torn fibre separations: thin darker slivers, uneven in length
    sl = np.exp(-(perlin(seed + 7, 72, 3, Xw, Yw) / 0.09) ** 2)
    lenmask = smoothstep(-0.1, 0.45, perlin(seed + 8, 24, 3, Xw, Yw))
    col = mix(col, np.broadcast_to(rgb(150, 112, 74), (N, N, 3)).copy(), 0.40 * sl * lenmask)
    # dry lifted splinters (light)
    spl = smoothstep(0.55, 0.85, perlin(seed + 9, 110, 5, Xw, Yw))
    col = mix(col, np.broadcast_to(rgb(246, 232, 196), (N, N, 3)).copy(), 0.35 * spl)
    # darker bark-edge streaks inside a few vertical zones only
    zone = smoothstep(0.12, 0.45, perlin(seed + 10, 3, 1, Xw, Yw + 0.1 * perlin(seed + 11, 2, 2, X, Y)))
    bark = smoothstep(0.05, 0.55, fbm(seed + 12, 80, 4, 3, Xw, Yw))
    col = mix(col, np.broadcast_to(rgb(112, 80, 52), (N, N, 3)).copy(), np.clip(1.1 * zone * bark, 0, 0.85))
    return finish(col, seed)


def card_f03(seed):
    """Spruce bark: grey-brown thin scaly plates, shallow vertical fissures, 1 m / repeat."""
    X, Y = grid()
    Xw, Yw = warp(seed + 1, X, Y, 3, 5, 0.014)
    Xw = Xw + 0.008 * perlin(seed + 2, 11, 9, X, Y)
    # tall plates (about 4.5 x 11 cm): their long soft boundaries read as shallow fissures
    f1, f2, idn, ox, oy = voronoi(seed + 3, 22, 9, Xw, Yw, jx=0.9, jy=1.0)
    # small scale chips overlaid on the plates
    g1, g2, idc, qx, qy = voronoi(seed + 9, 41, 30, Xw, Yw, jx=0.9, jy=0.9)
    plate = np.array([[104, 94, 90], [98, 87, 88], [110, 102, 98], [106, 92, 82], [94, 86, 85]],
                     dtype=np.float64)
    pi = np.minimum((idn * len(plate)).astype(int), len(plate) - 1)
    col = plate[pi] * (0.94 + 0.12 * idc)[..., None]
    col *= (1.0 - 0.14 * smoothstep(0.15, 0.6, f1))[..., None]
    # chip rims: slightly paler scale edges, darker seats beneath
    chip = smoothstep(0.0, 0.16, g2 - g1)
    col *= (0.90 + 0.10 * chip + 0.05 * (0.5 - qy))[..., None]
    # shallow separations between plates, broken so they fade in and out
    fade = 0.35 + 0.65 * smoothstep(-0.2, 0.4, fbm(seed + 10, 4, 6, 2, X, Y))
    edge = (1.0 - smoothstep(0.0, 0.12, f2 - f1)) * fade
    col = mix(col, np.broadcast_to(rgb(60, 53, 54), (N, N, 3)).copy(), 0.60 * edge)
    # vertical fissures: thin, wandering, partial
    n = fbm(seed + 5, 10, 2, 3, Xw, Yw)
    fis = np.exp(-(n / 0.05) ** 2) * smoothstep(-0.05, 0.4, fbm(seed + 6, 3, 3, 2, X, Y))
    col = mix(col, np.broadcast_to(rgb(54, 48, 50), (N, N, 3)).copy(), 0.35 * fis)
    # muted violet-brown mottling
    mot = fbm(seed + 7, 5, 5, 4, Xw, Yw)
    col[..., 0] *= 1.0 + 0.04 * mot
    col[..., 2] *= 1.0 + 0.08 * mot
    col *= (1.0 + 0.035 * fbm(seed + 8, 28, 28, 2, X, Y))[..., None]
    return finish(col, seed)


CARDS = {
    "S03": dict(file="snow_trampled_v2_albedo.png", seed=20261010, fn=card_s03, metres=1.0,
                consumer="PML snow_trampled (replace snow_trampled_v1_albedo.png; CW approach to FAP, road crest)"),
    "S05": dict(file="snow_frozen_dirty_v1_albedo.png", seed=20261015, fn=card_s05, metres=1.0,
                consumer="new local road/wheel snow surface (suggest snow_dirty) for CW road sections near wheels"),
    "S08": dict(file="ice_small_v1_albedo.png", seed=20261018, fn=card_s08, metres=1.0,
                consumer="PML ice (BoardCrossing DrainIceMaterial, CW FapPuddleWater ice patches)"),
    "S09": dict(file="snow_grass_v2_albedo.png", seed=20261019, fn=card_s09, metres=2.0,
                consumer="PML snow_grass (CW grass-edge snow, yard borders)"),
    "W07": dict(file="wood_shed_dark_v1_albedo.png", seed=20261107, fn=card_w07, metres=1.0,
                consumer="new shed timber slot (suggest wood_shed_dark): CW HeroYardShed_LoftBoard_* and AddVisualShed"),
    "W10": dict(file="wood_bath_light_v1_albedo.png", seed=20261110, fn=card_w10, metres=0.75,
                consumer="new bathhouse timber slot (suggest wood_bath_light): Bathhouse.cs floor, partitions, benches (wood_furniture/wood today)"),
    "W11": dict(file="wood_split_firewood_v1_albedo.png", seed=20261111, fn=card_w11, metres=0.5,
                consumer="new split-wood slot (suggest wood_split): CW CutWood, Bathhouse BathDryFirewood* side faces only"),
    "F03": dict(file="bark_spruce_v1_albedo.png", seed=20260303, fn=card_f03, metres=1.0,
                consumer="new spruce bark slot (suggest bark_spruce): AgentBAct1ExteriorLayer WinterSpruce_* trunks, non-pine bark path"),
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=os.getcwd())
    ap.add_argument("--only", default="")
    args = ap.parse_args(argv)
    only = [c.strip().upper() for c in args.only.split(",") if c.strip()] or list(CARDS)
    unknown = [c for c in only if c not in CARDS]
    if unknown:
        sys.exit("unknown card(s): " + ",".join(unknown))
    out_dir = os.path.join(args.root, OUT_DIR)
    os.makedirs(out_dir, exist_ok=True)
    receipt_path = os.path.join(out_dir, RECEIPT)
    receipt = {"generator": GENERATOR, "licence": LICENCE, "textures": {}}
    if os.path.exists(receipt_path):
        with open(receipt_path, encoding="utf-8") as f:
            receipt["textures"] = json.load(f).get("textures", {})
    for card in only:
        spec = CARDS[card]
        arr = spec["fn"](spec["seed"])
        path = os.path.join(out_dir, spec["file"])
        write_png(path, arr)
        with open(path, "rb") as f:
            digest = hashlib.sha256(f.read()).hexdigest()
        receipt["textures"][card] = {
            "card": card,
            "file": OUT_DIR + "/" + spec["file"],
            "seed": spec["seed"],
            "generator": GENERATOR,
            "sha256": digest,
            "size": "%dx%d RGB8 sRGB" % (N, N),
            "metres_per_repeat": spec["metres"],
            "consumer": spec["consumer"],
            "licence": LICENCE,
        }
        print("%s %s %s" % (card, spec["file"], digest))
    receipt["textures"] = dict(sorted(receipt["textures"].items()))
    with open(receipt_path, "w", encoding="utf-8") as f:
        json.dump(receipt, f, ensure_ascii=False, indent=2, sort_keys=True)
        f.write("\n")


if __name__ == "__main__":
    main()
