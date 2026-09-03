#!/usr/bin/env python3
"""Generate deterministic, project-original footstep samples for Act I.

AUDIO-004: one short step sample per surface family (wet road, mud, grass,
wood, interior floor). These are procedural sound-design placeholders in the
same project-original pattern as the ambience stems; authored foley and human
listening review remain release gates (AUDIO-014, CULTURE-004).
"""

from __future__ import annotations

import math
import random
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/assets/audio/act1/footsteps"
SAMPLE_RATE = 24_000

# Surface family -> (seed, decay power, thump Hz, crunch level, brightness)
SURFACES = {
    "wet_road": (11, 9.0, 95.0, 0.55, 0.30),
    "mud": (23, 6.5, 70.0, 0.80, 0.12),
    "grass": (37, 11.0, 110.0, 0.45, 0.45),
    "wood": (51, 8.0, 130.0, 0.25, 0.20),
    "interior_floor": (67, 12.0, 85.0, 0.35, 0.25),
}
VARIANTS = 3


def step_sample(rng: random.Random, surface: dict, index: int) -> list[float]:
    seed, decay, thump, crunch, brightness = surface
    duration = 0.16 + 0.02 * ((index + seed) % 3)
    count = int(SAMPLE_RATE * duration)
    samples: list[float] = []
    local = random.Random(seed * 100 + index)
    for position in range(count):
        t = position / SAMPLE_RATE
        envelope = math.exp(-decay * (t * SAMPLE_RATE / SAMPLE_RATE) * 40.0 / decay)
        attack = min(1.0, position / (SAMPLE_RATE * 0.004))
        thump_wave = math.sin(2.0 * math.pi * thump * t) * (1.0 - min(1.0, t * 14.0))
        noise = local.uniform(-1.0, 1.0)
        crunch_wave = 0.0
        if crunch > 0.0:
            gate = 1.0 if local.random() < crunch else 0.0
            crunch_wave = noise * gate * brightness * 3.0
        splash = noise * brightness * 0.35 * math.exp(-t * 60.0)
        value = envelope * attack * (0.5 * thump_wave + 0.28 * crunch_wave + 0.20 * splash)
        samples.append(max(-0.85, min(0.85, value)))
    return samples


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


def main() -> None:
    for surface_name, surface in SURFACES.items():
        for variant in range(VARIANTS):
            rng = random.Random(surface[0] * 7 + variant)
            _ = rng
            samples = step_sample(rng, surface, variant)
            path = OUT / f"step_{surface_name}_{variant:02d}.wav"
            write_wav(path, samples)
            print(f"footsteps: {path.name} samples={len(samples)}")


if __name__ == "__main__":
    main()
