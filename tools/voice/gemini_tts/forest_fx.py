#!/usr/bin/env python3
"""Forest sound design for the Act I prologue (numpy/scipy only; ffmpeg is not required).

1. Turns the raw synthetic keeper/Mansur forest clips into eerie, distant, distorted
   voices (echo, band-limit, bit-crush, detuned second layer). Raw clips are copied
   once to .tools/voice-preview/raw-forest/ and every run re-processes from them,
   so the effect can be tuned repeatedly without new API calls.
2. Synthesizes two foley sounds: the knock-out ring (thump + tinnitus) and a low
   dread drone for the start of the forest walk.

usage: forest_fx.py            # process voices and (re)write both foley sounds
"""

import shutil
import wave
from pathlib import Path

import numpy as np
from scipy import signal

REPO = Path(__file__).resolve().parents[3]
VOICE_DIR = REPO / "game/assets/audio/act1/voice_preview"
RAW_DIR = REPO / ".tools/voice-preview/raw-forest"
FOLEY_DIR = REPO / "game/assets/audio/act1/foley/forest"
RNG = np.random.default_rng(20260929)


def read(path):
    with wave.open(str(path), "rb") as w:
        assert w.getsampwidth() == 2 and w.getnchannels() == 1, f"{path}: expected mono 16-bit"
        rate = w.getframerate()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0
    return data, rate


def write(path, data, rate):
    data = np.clip(data, -1.0, 1.0)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes((data * 32767).astype("<i2").tobytes())


def band(x, rate, low=None, high=None):
    if low:
        x = signal.sosfilt(signal.butter(2, low, "highpass", fs=rate, output="sos"), x)
    if high:
        x = signal.sosfilt(signal.butter(4, high, "lowpass", fs=rate, output="sos"), x)
    return x


def echo(x, rate, taps, tail=1.6):
    """taps: [(delay_seconds, gain)], with light darkening of every repeat."""
    out = np.zeros(len(x) + int(rate * tail))
    out[: len(x)] += x
    for delay, gain in taps:
        offset = int(rate * delay)
        repeat = band(x, rate, high=max(900, 3200 - delay * 2500))
        out[offset: offset + len(x)] += repeat * gain
    return out


def crush(x, bits, mix):
    steps = 2 ** (bits - 1)
    return x * (1 - mix) + np.round(x * steps) / steps * mix


def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)


def detuned_layer(x, ratio):
    """A slower, lower copy (resampling changes pitch and length together)."""
    up = int(round(ratio * 100))
    return signal.resample_poly(x, 100, up)


def mix_layers(base, *layers):
    length = max(len(base), *(len(layer) for layer in layers))
    out = np.zeros(length)
    out[: len(base)] += base
    for layer in layers:
        out[: len(layer)] += layer
    return out


def normalize(x, peak=0.85):
    top = np.max(np.abs(x)) or 1.0
    return x / top * peak


def fade(x, rate, fade_in=0.0, fade_out=0.0):
    x = x.copy()
    a, b = int(rate * fade_in), int(rate * fade_out)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def far_voice(x, rate):
    """Called from deep among the trees: thin, hollow, long dark echoes, faint warble."""
    x = band(x, rate, low=240, high=2400)
    x = crush(drive(x, 2.2), 9, 0.35)
    t = np.arange(len(x)) / rate
    x = x * (1 - 0.22 + 0.22 * np.sin(2 * np.pi * 4.6 * t))
    x = echo(x, rate, [(0.23, 0.6), (0.52, 0.42), (0.91, 0.3), (1.4, 0.18)], tail=1.9)
    return normalize(fade(x, rate, 0.02, 0.9), 0.8)


def near_voice(x, rate):
    """Close and wrong: a second, lower and slower voice under the first, distorted."""
    low = detuned_layer(x, 0.8) * 0.75
    x = mix_layers(x, low)
    x = band(x, rate, low=140, high=4200)
    x = crush(drive(x, 3.0), 10, 0.3)
    x = echo(x, rate, [(0.11, 0.5), (0.27, 0.32), (0.6, 0.18)], tail=1.2)
    return normalize(fade(x, rate, 0.01, 0.6), 0.9)


def muffled_voice(x, rate):
    """Heard through a blow to the head: dull, swimming, fading in from far away."""
    x = band(x, rate, low=90, high=620)
    t = np.arange(len(x)) / rate
    x = x * (0.75 + 0.25 * np.sin(2 * np.pi * 2.1 * t))
    x = echo(x, rate, [(0.09, 0.45), (0.2, 0.3)], tail=0.8)
    x = fade(x, rate, 0.9, 0.5)
    return normalize(x, 0.7)


def dying_voice(x, rate):
    """The forest voice calling his name as he blacks out: echoing, thinning, sinking away."""
    x = band(x, rate, low=120, high=3200)
    x = echo(x, rate, [(0.28, 0.55), (0.62, 0.4), (1.05, 0.28), (1.6, 0.18)], tail=2.2)
    t = np.arange(len(x)) / rate
    x = x * np.exp(-t / 3.2)
    return normalize(fade(x, rate, 0.02, 1.4), 0.85)


VOICE_FX = {
    "forest-name-fade": dying_voice,
    "forest-babai-name": muffled_voice,
    "forest-call-hey": far_voice,
    "forest-call-far": far_voice,
    "forest-name-call": far_voice,
    "forest-come-here": near_voice,
}


def process_voices():
    RAW_DIR.mkdir(parents=True, exist_ok=True)
    for clip in [*VOICE_FX, "forest-wake"]:
        current = VOICE_DIR / f"{clip}.wav"
        raw = RAW_DIR / f"{clip}.wav"
        if not raw.exists():
            if not current.exists():
                print(f"нет исходника: {clip}")
                continue
            shutil.copyfile(current, raw)
        data, rate = read(raw)
        if clip == "forest-wake":
            write(VOICE_DIR / "forest-wake-muffled.wav", muffled_voice(data, rate), rate)
            print("forest-wake-muffled")
        else:
            write(current, VOICE_FX[clip](data, rate), rate)
            print(clip)


def knockout_ring():
    """The blow: a sub thump, a burst of grit, then a tinnitus whine over a dull roar."""
    rate = 44100
    n = int(rate * 6.0)
    t = np.arange(n) / rate
    thump = np.sin(2 * np.pi * (48 + 60 * np.exp(-t * 18)) * t) * np.exp(-t / 0.16)
    grit = band(RNG.normal(0, 1, n), rate, high=1800) * np.exp(-t / 0.05) * 0.5
    envelope = np.clip(t / 0.25, 0, 1) * np.exp(-t / 2.4)
    ring = (np.sin(2 * np.pi * 3150 * t) * 0.55 + np.sin(2 * np.pi * 3187 * t) * 0.35
            + np.sin(2 * np.pi * 6310 * t) * 0.08) * envelope * 0.32
    roar = band(RNG.normal(0, 1, n), rate, high=260) * np.exp(-t / 1.6) * 0.5
    beat = np.zeros(n)
    for start, gain in [(0.9, .6), (1.55, .45), (2.35, .4), (3.25, .32), (4.3, .24)]:
        idx = int(rate * start)
        knock = np.sin(2 * np.pi * 58 * t[:int(rate * .3)]) * np.exp(-t[:int(rate * .3)] / .07)
        beat[idx: idx + len(knock)] += knock * gain
    out = normalize(thump * 1.0 + grit + ring + roar + beat, 0.9)
    write(FOLEY_DIR / "knockout_ring.wav", fade(out, rate, 0, 1.2), rate)
    print("knockout_ring")


def dread_drone():
    """A cold, slowly breathing low bed: beating sub tones plus a swell of airy hiss."""
    rate = 44100
    seconds = 26.0
    n = int(rate * seconds)
    t = np.arange(n) / rate
    swell = 0.55 + 0.45 * np.sin(2 * np.pi * t / 9.5 - 1.2)
    sub = (np.sin(2 * np.pi * 41.2 * t) + np.sin(2 * np.pi * 43.0 * t) * 0.9
           + np.sin(2 * np.pi * 82.4 * t) * 0.35) * 0.35 * swell
    dark = band(RNG.normal(0, 1, n), rate, high=340) * 0.5 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 13 + 2.1))
    air = band(RNG.normal(0, 1, n), rate, low=2600, high=6200) * 0.06 * np.clip(np.sin(2 * np.pi * t / 7.3) + 0.2, 0, 1)
    out = normalize(sub + dark + air, 0.7)
    write(FOLEY_DIR / "dread_drone.wav", fade(out, rate, 4.0, 5.0), rate)
    print("dread_drone")


if __name__ == "__main__":
    FOLEY_DIR.mkdir(parents=True, exist_ok=True)
    process_voices()
    knockout_ring()
    dread_drone()
