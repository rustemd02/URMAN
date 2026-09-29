#!/usr/bin/env python3
"""Generate the prologue radio "catches the name" sound (review idea 2,
docs/urman_knowledge_base/gameplay/act1_prologue_review_2026-09-29.md):
static rises over the Niva radio near the forest edge and, once, a vowel
glide close to "Ай…" surfaces for half a second.

Procedural placeholder (formant-synthesised vowel through a radio band and
crackle), not a recording; listening remains open. Only writes its own file,
so the tuned forest sounds are not regenerated.

Usage: python3 tools/audio/generate_prologue_radio_evp.py
"""

from __future__ import annotations

import numpy as np
from scipy import signal

from generate_prologue_forest_sounds import RATE, band, lowpass, t, write

RNG = np.random.default_rng(0x55524D35)


def resonator(x: np.ndarray, freq: np.ndarray, bandwidth: float) -> np.ndarray:
    """Time-varying two-pole resonator (formant)."""
    y = np.zeros_like(x)
    r = np.exp(-np.pi * bandwidth / RATE)
    y1 = y2 = 0.0
    for i in range(len(x)):
        c = 2 * r * np.cos(2 * np.pi * freq[i] / RATE)
        value = x[i] + c * y1 - r * r * y2
        y[i] = value
        y2, y1 = y1, value
    return y


def vowel_ai(seconds: float = .55) -> np.ndarray:
    tt = t(seconds)
    k = np.clip(tt / seconds, 0, 1)
    f0 = 150 - 30 * k
    phase = np.cumsum(f0) / RATE
    pulses = (np.diff(np.floor(phase), prepend=0) > 0).astype(float)
    source = lowpass(pulses, 3500) + .08 * RNG.standard_normal(len(tt))
    glide = np.clip((k - .35) / .45, 0, 1)
    f1 = 760 - 460 * glide
    f2 = 1250 + 1050 * glide
    f3 = np.full(len(tt), 2650.0)
    voice = resonator(source, f1, 90) + .7 * resonator(source, f2, 120) + .3 * resonator(source, f3, 180)
    shape = np.sin(np.pi * np.clip(tt / seconds, 0, 1)) ** .7
    return voice / (np.max(np.abs(voice)) + 1e-9) * shape


def radio_evp() -> np.ndarray:
    total = t(3.4)
    n = len(total)
    crackle = band(RNG.standard_normal(n), 500, 5200)
    pops = np.zeros(n)
    for _ in range(90):
        at = RNG.integers(0, n - 60)
        pops[at:at + 40] += RNG.uniform(.3, 1.2) * np.exp(-np.linspace(0, 6, 40))
    hiss = crackle * (.45 + .55 * (1 + np.sin(2 * np.pi * 7.3 * total)) / 2) + band(RNG.standard_normal(n), 300, 3000) * pops
    rise = np.clip(total / 1.4, 0, 1) ** 1.5
    fall = np.clip((3.4 - total) / .25, 0, 1)
    static = hiss * rise * fall * .55
    # The syllable comes through once, band-limited like a far station.
    voice = band(vowel_ai(), 350, 2800, 2)
    start = int(1.55 * RATE)
    static[start:start + len(voice)] += voice * 1.1
    # Tuning whistle under it.
    static += np.sin(2 * np.pi * np.cumsum(1800 + 400 * np.sin(2 * np.pi * .6 * total)) / RATE) * .04 * rise * fall
    return static


if __name__ == "__main__":
    write("radio_evp", radio_evp(), .8)
    print("prologue-radio-evp: radio_evp.wav")
