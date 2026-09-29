#!/usr/bin/env python3
"""Generate deterministic, project-original night-forest sounds for the
Act I prologue flash-forward (author feedback 2026-09-29: owls, animals,
rustling in the bushes, something running past, strange sounds; and the
attack/fall/wake transition into the Niva).

Procedural placeholders, not recordings: they are made from filtered noise,
tone glides and a small diffuse forest reverb. Listening and replacement by
CC0 field recordings remain open (audio map, ACT1-AUDIO.MAP).

Usage: python3 tools/audio/generate_prologue_forest_sounds.py
"""

from __future__ import annotations

from pathlib import Path
import wave

import numpy as np
from scipy import signal

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/assets/audio/act1/foley/forest"
RATE = 32_000
RNG = np.random.default_rng(0x55524D33)


def t(seconds: float) -> np.ndarray:
    return np.arange(int(seconds * RATE)) / RATE


def env(n: int, attack: float, release: float) -> np.ndarray:
    a = max(1, int(attack * RATE))
    r = max(1, int(release * RATE))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a) ** 1.5
    e[-r:] *= np.linspace(1, 0, r) ** 2
    return e


def band(x: np.ndarray, low: float, high: float, order: int = 4) -> np.ndarray:
    sos = signal.butter(order, [low, high], btype="band", fs=RATE, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x: np.ndarray, cutoff: float, order: int = 4) -> np.ndarray:
    return signal.sosfilt(signal.butter(order, cutoff, btype="low", fs=RATE, output="sos"), x)


def highpass(x: np.ndarray, cutoff: float, order: int = 2) -> np.ndarray:
    return signal.sosfilt(signal.butter(order, cutoff, btype="high", fs=RATE, output="sos"), x)


def glide(f: np.ndarray) -> np.ndarray:
    return np.sin(2 * np.pi * np.cumsum(f) / RATE)


def forest_reverb(x: np.ndarray, seconds: float = 1.6, wet: float = .35) -> np.ndarray:
    n = int(seconds * RATE)
    ir = RNG.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, 3500)
    ir /= np.sqrt(np.sum(ir ** 2))
    tail = signal.fftconvolve(np.concatenate([x, np.zeros(n)]), ir)[: len(x) + n]
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - wet) + tail * wet


def write(name: str, x: np.ndarray, peak: float = .9) -> None:
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


def hoot(start_f: float, end_f: float, seconds: float, vibrato: float = 0) -> np.ndarray:
    tt = t(seconds)
    f = np.linspace(start_f, end_f, len(tt)) + vibrato * np.sin(2 * np.pi * 6 * tt)
    tone = glide(f) + .28 * glide(2 * f) + .08 * glide(3 * f)
    breath = band(RNG.standard_normal(len(tt)), start_f * .8, start_f * 2.5) * .08
    return (tone + breath) * env(len(tt), .06, seconds * .55)


def silence(seconds: float) -> np.ndarray:
    return np.zeros(int(seconds * RATE))


def owl_eagle() -> np.ndarray:
    # Eagle owl: a deep "oo-HOO", falling at the end.
    call = np.concatenate([hoot(310, 300, .22), silence(.12), hoot(345, 285, .55)])
    return forest_reverb(lowpass(call, 1600), 2.2, .45)


def owl_tawny() -> np.ndarray:
    # Tawny owl: a long hoot, a pause, then a quavering tail.
    call = np.concatenate([hoot(560, 520, .75, 3), silence(1.0), hoot(600, 560, .25), silence(.08),
                           hoot(620, 540, 1.1, 18)])
    return forest_reverb(call, 2.0, .45)


def fox_scream() -> np.ndarray:
    tt = t(.85)
    f = 780 + 380 * np.sin(np.pi * tt / .85) - 120 * tt
    rough = 1 + .5 * np.sin(2 * np.pi * 33 * tt)
    voice = (glide(f) + .7 * glide(2 * f) + .45 * glide(3 * f) + .25 * glide(4.1 * f)) * rough
    voice += band(RNG.standard_normal(len(tt)), 900, 4200) * .6
    voice *= env(len(tt), .03, .35)
    call = np.concatenate([voice, silence(.5), voice[: int(.55 * RATE)] * .8])
    return forest_reverb(call, 2.4, .5)


def wolf_far() -> np.ndarray:
    tt = t(4.2)
    f = 330 + 190 * np.sin(np.pi * np.clip(tt / 3.2, 0, 1)) + 6 * np.sin(2 * np.pi * 5.5 * tt)
    howl = (glide(f) + .35 * glide(2 * f) + .12 * glide(3 * f)) * env(len(tt), .5, 1.4)
    return forest_reverb(lowpass(howl, 1300), 3.0, .6)


def rustle(seconds: float = 1.3, density: float = 26) -> np.ndarray:
    n = int(seconds * RATE)
    bursts = np.zeros(n)
    for _ in range(int(seconds * density)):
        at = RNG.integers(0, n - 800)
        length = RNG.integers(250, 1400)
        bursts[at:at + length] += RNG.uniform(.3, 1) * np.exp(-np.linspace(0, 5, length))
    noise = RNG.standard_normal(n)
    leaves = band(noise, 1800, 7000) * bursts
    twigs = band(noise, 500, 1800) * bursts * .5
    return (leaves + twigs) * env(n, .05, .3)


def crunch(seconds: float = .16) -> np.ndarray:
    n = int(seconds * RATE)
    grains = np.zeros(n)
    for _ in range(40):
        at = RNG.integers(0, n - 200)
        grains[at:at + 120] += RNG.uniform(.2, 1) * np.exp(-np.linspace(0, 6, 120))
    body = lowpass(RNG.standard_normal(n), 900) * np.exp(-np.linspace(0, 9, n)) * 2
    return band(RNG.standard_normal(n), 1200, 6500) * grains + body


def runner_past() -> np.ndarray:
    # Light fast steps in deep snow with brush, getting quicker then fading.
    total = int(2.4 * RATE)
    out = np.zeros(total)
    at = 0.0
    step = .21
    while at < 2.1:
        c = crunch(.12) * RNG.uniform(.6, 1)
        i = int(at * RATE)
        out[i:i + len(c)] += c[: max(0, total - i)]
        at += step * RNG.uniform(.85, 1.15)
    out += rustle(2.4, 18) * .6
    return out * env(total, .15, .5)


def branch_crack() -> np.ndarray:
    n = int(.6 * RATE)
    click = np.zeros(n)
    click[:60] = RNG.standard_normal(60)
    wood = sum(band(click, f * .96, f * 1.04, 2) * a for f, a in ((420, 1), (870, .6), (1650, .35)))
    snap = highpass(click, 2000) * 3
    return forest_reverb(wood * 25 + snap, 1.2, .35)


def strange_clicks() -> np.ndarray:
    # Hollow throat clicks with no rhythm to hold on to.
    out = silence(2.6)
    at = .05
    while at < 2.3:
        n = int(.05 * RATE)
        imp = np.zeros(n)
        imp[:30] = RNG.standard_normal(30)
        tok = band(imp, 700, 1300, 2) * 20 + band(imp, 180, 320, 2) * 10
        i = int(at * RATE)
        out[i:i + n] += tok * RNG.uniform(.5, 1)
        at += RNG.choice([.09, .11, .35, .5, .13])
    return forest_reverb(out, 1.6, .5)


def low_moan() -> np.ndarray:
    tt = t(3.6)
    f = 74 + 9 * np.sin(2 * np.pi * .4 * tt) + 5 * np.sin(2 * np.pi * 4.7 * tt)
    voice = glide(f) + .6 * glide(2 * f) + .4 * glide(3 * f) + .3 * glide(5 * f)
    breath = band(RNG.standard_normal(len(tt)), 200, 900) * .35
    formant = band(voice + breath, 180, 700, 2) * 2 + voice * .3
    return forest_reverb(formant * env(len(tt), .8, 1.4), 2.6, .55)


def wind_gust() -> np.ndarray:
    tt = t(5.0)
    swell = np.sin(np.pi * tt / 5.0) ** 2 * (1 + .25 * np.sin(2 * np.pi * .9 * tt))
    wind = lowpass(RNG.standard_normal(len(tt)), 700) * .8 + band(RNG.standard_normal(len(tt)), 900, 2600) * .15
    return wind * swell


def heartbeat() -> np.ndarray:
    out = silence(3.0)
    for beat in (0.0, .95, 1.8):
        for offset, amp in ((0, 1), (.28, .7)):
            tt = t(.18)
            thump = np.sin(2 * np.pi * (55 - 20 * tt / .18) * tt) * np.exp(-tt * 22) * amp
            i = int((beat + offset) * RATE)
            out[i:i + len(thump)] += thump
    return lowpass(out, 180)


def gasp() -> np.ndarray:
    tt = t(.75)
    shape = np.sin(np.pi * np.clip(tt / .55, 0, 1)) ** .6 * env(len(tt), .02, .25)
    air = band(RNG.standard_normal(len(tt)), 700, 2600) * .8 + band(RNG.standard_normal(len(tt)), 2600, 5200) * .3
    return air * shape


def body_fall() -> np.ndarray:
    tt = t(.9)
    thud = np.sin(2 * np.pi * (70 - 30 * tt) * tt) * np.exp(-tt * 9) * 1.6
    snow = crunch(.35) * 1.4
    out = thud.copy()
    out[: len(snow)] += snow
    out[int(.18 * RATE): int(.18 * RATE) + len(snow)] += crunch(.35)[: len(out) - int(.18 * RATE)] * .7
    return out


def rush_close() -> np.ndarray:
    # Something heavy lunging close past the ear.
    tt = t(.9)
    whoosh = band(RNG.standard_normal(len(tt)), 250, 1800) * np.exp(-((tt - .35) / .16) ** 2) * 1.6
    growl = glide(62 + 20 * np.sin(2 * np.pi * 13 * tt)) * np.exp(-((tt - .4) / .22) ** 2)
    return whoosh + band(growl, 50, 400, 2) * 2


def scare_sting() -> np.ndarray:
    # The jump-scare stab: a dissonant bowed cluster with a hard attack,
    # a low boom and a noise burst, decaying into a ringing tail.
    tt = t(2.2)
    cluster = np.zeros(len(tt))
    for f in (233.1, 246.9, 349.2, 370.0, 466.2, 493.9, 740.0):
        saw = signal.sawtooth(2 * np.pi * f * tt * (1 + .003 * np.sin(2 * np.pi * 5 * tt)))
        cluster += saw
    cluster = lowpass(cluster, 5200) * np.exp(-tt * 1.9)
    boom = np.sin(2 * np.pi * (48 - 14 * tt) * tt) * np.exp(-tt * 3.5) * 3
    burst = highpass(RNG.standard_normal(len(tt)), 900) * np.exp(-tt * 14) * 2
    attack = np.minimum(1, tt / .006)
    return forest_reverb((cluster + boom + burst) * attack, 1.8, .3)


def main() -> None:
    for name, maker in {
        "owl_eagle": owl_eagle, "owl_tawny": owl_tawny, "fox_scream": fox_scream, "wolf_far": wolf_far,
        "brush_rustle": rustle, "runner_past": runner_past, "branch_crack": branch_crack,
        "strange_clicks": strange_clicks, "low_moan": low_moan, "wind_gust": wind_gust,
        "heartbeat": heartbeat, "gasp": gasp, "scare_sting": scare_sting, "body_fall": body_fall, "rush_close": rush_close,
    }.items():
        write(name, maker())
        print(f"prologue-forest-sound: {name}.wav")


if __name__ == "__main__":
    main()
