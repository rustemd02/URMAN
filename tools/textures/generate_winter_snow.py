#!/usr/bin/env python3
"""Procedural winter snow albedos and micro maps for УРМАН (Act I winter season lock).

The winter brief (docs/production/URMAN_WINTER_TEXTURE_BRIEF_RU.md) lists the
same families for an optional ImageGen upgrade pass. These painterly albedos
are generated deterministically here so the snow has real relief, drifts and
packed detail instead of a flat white sheet; if the ImageGen files ever land
with the same names, the runtime picks them up automatically. The micro
response and normal maps are separate packed data maps, not albedo inputs:
response.R is normalized microheight, response.G is shader-mapped roughness
variation, response.B is a static grain mask, and normal encodes the matching
height gradient.

Design rules (design_style.md -> Winter):
- warm-white snow with cool blue hollows, never blown-out pure white;
- relief is painted into the albedo (sun from the upper left), matte;
- no photographic grain, no gloss highlights; live sparkle is a shader term.

Usage:
    python3 tools/textures/generate_winter_snow.py [--root <repo>]
"""

from __future__ import annotations

import argparse
import math
import pathlib
import sys

import numpy as np
from PIL import Image

SEED = 20260910
SIZE = 1024


def _value_noise(shape: tuple[int, int], frequency: float, seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    cells = max(2, int(frequency))
    grid = rng.random((cells + 1, cells + 1)).astype(np.float32)
    grid[-1, :] = grid[0, :]
    grid[:, -1] = grid[:, 0]
    ys = np.linspace(0, cells, shape[0], endpoint=False, dtype=np.float32)
    xs = np.linspace(0, cells, shape[1], endpoint=False, dtype=np.float32)
    y0 = np.floor(ys).astype(np.int32)
    x0 = np.floor(xs).astype(np.int32)
    fy = (ys - y0)[:, None]
    fx = (xs - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    a = grid[np.ix_(y0, x0)]
    b = grid[np.ix_(y0, x0 + 1)]
    c = grid[np.ix_(y0 + 1, x0)]
    d = grid[np.ix_(y0 + 1, x0 + 1)]
    return (a * (1 - fx) * (1 - fy) + b * fx * (1 - fy)
            + c * (1 - fx) * fy + d * fx * fy)


def _fbm(shape: tuple[int, int], octaves: int, seed: int, base: float = 3.0) -> np.ndarray:
    total = np.zeros(shape, dtype=np.float32)
    amplitude = 1.0
    norm = 0.0
    frequency = base
    for index in range(octaves):
        total += amplitude * _value_noise(shape, frequency, seed + index * 17)
        norm += amplitude
        amplitude *= 0.5
        frequency *= 2.0
    return total / norm


def _shade(relief: np.ndarray, strength: float = 0.5) -> np.ndarray:
    """Painted shading from the upper left, matching the scene sun."""
    gy, gx = np.gradient(relief)
    light = (-gx - gy) * strength
    return np.clip(relief + light, 0.0, 1.0)


def _to_snow_rgb(value: np.ndarray, warm: tuple[float, float, float],
                 cool: tuple[float, float, float], gamma: float = 1.0) -> np.ndarray:
    v = np.clip(value, 0.0, 1.0)[..., None] ** gamma
    warm_arr = np.array(warm, dtype=np.float32)
    cool_arr = np.array(cool, dtype=np.float32)
    return np.clip(cool_arr + (warm_arr - cool_arr) * v, 0.0, 1.0)


def _specks(shape: tuple[int, int], seed: int, density: float, brightness: float) -> np.ndarray:
    """Sparse soft micro-bright specks baked into the albedo."""
    rng = np.random.default_rng(seed)
    mask = (rng.random(shape) < density).astype(np.float32)
    for _ in range(2):
        mask = (mask + np.roll(mask, 1, 0) + np.roll(mask, -1, 0)
                + np.roll(mask, 1, 1) + np.roll(mask, -1, 1)) / 5.0
    return mask * brightness

def snow_fresh(variant: int) -> Image.Image:
    drift = _fbm((SIZE, SIZE), 5, SEED + variant * 7, base=2.4)
    wave = np.repeat(np.sin(np.linspace(0, math.tau * 3.1, SIZE, dtype=np.float32))[None, :],
                     SIZE, axis=0) * 0.035
    relief = _shade(0.55 + (drift - 0.5) * 0.55 + wave, 0.85)
    rgb = _to_snow_rgb(relief, (0.972, 0.968, 0.952), (0.700, 0.760, 0.835))
    if variant == 2:
        grass = _fbm((SIZE, SIZE), 4, SEED + 91, base=9.0)
        blades = np.clip((grass - 0.72) * 4.0, 0.0, 1.0)
        rgb = rgb * (1 - blades[..., None] * 0.55) + np.array([0.60, 0.53, 0.36]) * (blades[..., None] * 0.55)
        rgb -= blades[..., None] * np.array([0.05, 0.04, 0.02])
    rgb += _specks((SIZE, SIZE), SEED + variant, 0.0016, 0.14)[..., None]
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def snow_trampled() -> Image.Image:
    packed = _fbm((SIZE, SIZE), 5, SEED + 33, base=4.0)
    dents = _fbm((SIZE, SIZE), 4, SEED + 44, base=11.0)
    relief = 0.40 + (packed - 0.5) * 0.34 - np.clip((0.62 - dents) * 0.9, 0.0, 1.0) * 0.22
    relief = _shade(relief, 0.65)
    rgb = _to_snow_rgb(relief, (0.905, 0.915, 0.930), (0.585, 0.640, 0.710), gamma=0.95)
    grit = np.clip((_fbm((SIZE, SIZE), 3, SEED + 55, base=22.0) - 0.78) * 5.0, 0.0, 1.0)
    rgb -= grit[..., None] * np.array([0.04, 0.10, 0.16])
    rgb += _specks((SIZE, SIZE), SEED + 3, 0.0007, 0.10)[..., None]
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def snow_road() -> Image.Image:
    packed_field = _fbm((SIZE, SIZE), 5, SEED + 66, base=2.7)
    packed_mottle = _fbm((SIZE, SIZE), 4, SEED + 71, base=8.5)
    packed = np.clip((packed_field - 0.46) * 2.2, 0.0, 1.0)
    packed *= 0.58 + packed_mottle * 0.42
    grit = np.clip((_fbm((SIZE, SIZE), 4, SEED + 77, base=19.0) - 0.68) * 4.0, 0.0, 1.0)
    relief = _shade(0.74 + (packed_mottle - 0.5) * 0.18 - packed * 0.19, 0.7)
    rgb = _to_snow_rgb(relief, (0.910, 0.915, 0.920), (0.610, 0.655, 0.700), gamma=0.95)
    rgb -= grit[..., None] * np.array([0.025, 0.030, 0.035])
    rgb -= packed[..., None] * np.array([0.060, 0.065, 0.070])
    rgb += _specks((SIZE, SIZE), SEED + 9, 0.0009, 0.12)[..., None]
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def snow_grass_peek() -> Image.Image:
    rgb = np.asarray(snow_fresh(1)).astype(np.float32) / 255.0
    blades = np.clip((_fbm((SIZE, SIZE), 4, SEED + 88, base=13.0) - 0.70) * 3.4, 0.0, 1.0)
    rgb = rgb * (1 - blades[..., None] * 0.75) + np.array([0.62, 0.55, 0.38]) * (blades[..., None] * 0.75)
    rgb -= blades[..., None] * np.array([0.06, 0.05, 0.02])
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def ice_patch(variant: int) -> Image.Image:
    flow = _fbm((SIZE, SIZE), 5, SEED + 120 + variant * 5, base=2.2)
    cracks = np.clip((_fbm((SIZE, SIZE), 3, SEED + 140, base=16.0) - 0.80) * 6.0, 0.0, 1.0)
    relief = _shade(0.32 + (flow - 0.5) * 0.40, 0.5)
    rgb = _to_snow_rgb(relief, (0.36, 0.42, 0.46), (0.10, 0.15, 0.19))
    if variant == 2:
        dust = _fbm((SIZE, SIZE), 4, SEED + 160, base=6.0)
        rgb = rgb * (1 - dust[..., None] * 0.55) + np.array([0.80, 0.83, 0.87]) * (dust[..., None] * 0.55)
    rgb -= cracks[..., None] * np.array([0.06, 0.08, 0.10])
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def snow_roof() -> Image.Image:
    sag = np.repeat(np.sin(np.linspace(0, math.tau * 2.0, SIZE, dtype=np.float32))[None, :], SIZE, axis=0) * 0.03
    slab = _fbm((SIZE, SIZE), 4, SEED + 200, base=2.0)
    edge = np.repeat(
        np.exp(-((np.linspace(0, 1, SIZE, dtype=np.float32) - 0.88) ** 2) / 0.002)[None, :],
        SIZE, axis=0)
    crumbs = np.clip((_fbm((SIZE, SIZE), 4, SEED + 210, base=18.0) - 0.74) * 5.0, 0.0, 1.0)
    relief = _shade(0.86 + (slab - 0.5) * 0.16 + sag - edge * 0.22 - crumbs * 0.18, 0.6)
    rgb = _to_snow_rgb(relief, (0.965, 0.962, 0.950), (0.690, 0.750, 0.830))
    rgb += _specks((SIZE, SIZE), SEED + 21, 0.0012, 0.13)[..., None]
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))


def snow_micro_maps() -> tuple[Image.Image, Image.Image]:
    """Return packed response/normal maps for a periodic 1 m snow tile.

    The normal uses the exact 8-bit height stored in response.R, converted back
    to metres, with central differences at a 2 px radius (2 / 1024 m).
    """
    height = sum(
        weight * _value_noise((SIZE, SIZE), frequency, SEED + 300 + index * 11)
        for index, (frequency, weight) in enumerate(
            ((4, 0.50), (8, 0.25), (16, 0.15), (32, 0.10)))
    ).astype(np.float32)
    height = (height - height.min()) / (height.max() - height.min())
    height_u8 = np.rint(height * 255.0).astype(np.uint8)
    encoded_height = height_u8.astype(np.float32) / 255.0

    radius = 2
    texel_m = 1.0 / SIZE
    height_m = (encoded_height - 0.5) * 0.004
    dh_dx = (np.roll(height_m, -radius, axis=1) - np.roll(height_m, radius, axis=1)) / (2 * radius * texel_m)
    dh_dz = (np.roll(height_m, -radius, axis=0) - np.roll(height_m, radius, axis=0)) / (2 * radius * texel_m)
    normal = np.stack((-dh_dx, -dh_dz, np.ones_like(height_m)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    normal_rgb = np.clip(normal * 0.5 + 0.5, 0.0, 1.0)

    slope = np.hypot(dh_dx, dh_dz)
    slope_variation = np.clip(slope / (np.percentile(slope, 95) + 1e-8), 0.0, 1.0)
    roughness_variation = np.clip(
        0.5 + 0.35 * (encoded_height - 0.5) + 0.35 * (slope_variation - 0.5), 0.0, 1.0)

    neighbors = [
        np.roll(encoded_height, (row, column), axis=(0, 1))
        for row in (-1, 0, 1)
        for column in (-1, 0, 1)
        if row or column
    ]
    local_peaks = encoded_height > np.maximum.reduce(neighbors)
    peak_indices = np.flatnonzero(local_peaks)
    grain_count = round(SIZE * SIZE * 0.0025)
    if peak_indices.size < grain_count:
        peak_indices = np.flatnonzero(encoded_height >= np.percentile(encoded_height, 99.75))
    strongest = peak_indices[np.argsort(encoded_height.flat[peak_indices])[-grain_count:]]
    grain = np.zeros((SIZE, SIZE), dtype=np.uint8)
    grain.flat[strongest] = 255

    response = np.stack((height_u8, np.rint(roughness_variation * 255.0).astype(np.uint8), grain), axis=-1)
    assert np.isfinite(response).all() and np.isfinite(normal_rgb).all()
    assert response.min() >= 0 and response.max() <= 255
    assert normal_rgb.min() >= 0.0 and normal_rgb.max() <= 1.0
    assert np.allclose(np.linalg.norm(normal, axis=-1), 1.0, atol=1e-6)
    assert 0.001 <= float(np.count_nonzero(grain)) / grain.size <= 0.005
    assert np.isfinite(dh_dx[:, (0, -1)]).all() and np.isfinite(dh_dz[(0, -1), :]).all()
    assert max(float(np.abs(dh_dx).max()), float(np.abs(dh_dz).max())) < 1.0

    return (Image.fromarray(response),
            Image.fromarray(np.rint(normal_rgb * 255.0).astype(np.uint8)))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=None)
    args = parser.parse_args()
    root = pathlib.Path(args.root).resolve() if args.root else pathlib.Path(__file__).resolve().parents[2]
    out = root / "game/assets/textures/painterly"
    out.mkdir(parents=True, exist_ok=True)

    micro_response, micro_normal = snow_micro_maps()
    outputs = {
        "snow_fresh_v1_albedo.png": snow_fresh(1),
        "snow_fresh_v2_albedo.png": snow_fresh(2),
        "snow_trampled_v1_albedo.png": snow_trampled(),
        "snow_road_v1_albedo.png": snow_road(),
        "snow_grass_peek_v1_albedo.png": snow_grass_peek(),
        "ice_patch_v1_albedo.png": ice_patch(1),
        "ice_patch_v2_albedo.png": ice_patch(2),
        "snow_roof_v1_albedo.png": snow_roof(),
        "snow_micro_response.png": micro_response,
        "snow_micro_normal.png": micro_normal,
    }
    for name, image in outputs.items():
        image.save(out / name)
        print(f"wrote {out / name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
