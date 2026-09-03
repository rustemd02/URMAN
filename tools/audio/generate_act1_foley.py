#!/usr/bin/env python3
"""Generate deterministic, project-original UI foley samples for Act I.

AUDIO-010: short interaction feedback sounds — UI click, old-PC keyboard key,
paper document handling. Procedural project-original placeholders; final
authored foley and listening review remain gates (AUDIO-014, CULTURE-004).
"""

from __future__ import annotations

import math
import random
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/assets/audio/act1/foley"
SAMPLE_RATE = 24_000


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for value in samples:
            frames += int(max(-1.0, min(1.0, value)) * 32767).to_bytes(2, "little", signed=True)
        stream.writeframes(bytes(frames))


def click() -> list[float]:
    samples = []
    count = int(SAMPLE_RATE * 0.045)
    for position in range(count):
        t = position / SAMPLE_RATE
        envelope = math.exp(-t * 180.0)
        body = math.sin(2.0 * math.pi * 1150.0 * t) * math.exp(-t * 260.0)
        tick = random.Random(position).uniform(-1.0, 1.0) * 0.12 * math.exp(-t * 400.0)
        samples.append(0.5 * envelope * body + tick)
    return samples


def keyboard_key() -> list[float]:
    samples = []
    count = int(SAMPLE_RATE * 0.06)
    for position in range(count):
        t = position / SAMPLE_RATE
        envelope = math.exp(-t * 120.0)
        thock = math.sin(2.0 * math.pi * 210.0 * t) * math.exp(-t * 150.0)
        clack = random.Random(700 + position).uniform(-1.0, 1.0) * 0.22 * math.exp(-t * 300.0)
        samples.append(0.55 * envelope * (thock + clack))
    return samples


def paper_open() -> list[float]:
    samples = []
    count = int(SAMPLE_RATE * 0.22)
    rng = random.Random(4242)
    for position in range(count):
        t = position / SAMPLE_RATE
        envelope = math.sin(math.pi * min(1.0, t / 0.22)) * 0.4
        rustle = rng.uniform(-1.0, 1.0) * (0.35 + 0.65 * math.sin(math.pi * min(1.0, t / 0.22)))
        crisp = rustle * math.exp(-t * 9.0)
        samples.append(0.3 * envelope * crisp)
    return samples


def main() -> None:
    for name, samples in (
        ("ui_click", click()),
        ("keyboard_key", keyboard_key()),
        ("paper_open", paper_open()),
    ):
        path = OUT / f"{name}.wav"
        write_wav(path, samples)
        print(f"foley: {path.name} samples={len(samples)}")


if __name__ == "__main__":
    main()
