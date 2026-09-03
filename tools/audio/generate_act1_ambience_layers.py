#!/usr/bin/env python3
"""Generate deterministic, project-original ambience layer stems for Act I.

AUDIO-006 (FAP institutional bed) and AUDIO-007 (zirat wind bed): these give
the FAP clinic and the zirat road their own sonic read instead of reusing the
village and house beds. Procedural project-original placeholders in the same
pattern as the ambience stems; authored mix and cultural listening review
remain release gates (AUDIO-014, CULTURE-004).
"""

from __future__ import annotations

import math
import random
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/assets/audio"
SAMPLE_RATE = 24_000
DURATION_SECONDS = 8


def smooth_noise(rng: random.Random, count: int, stride: int) -> list[float]:
    anchors = [rng.uniform(-1.0, 1.0) for _ in range(count // stride + 2)]
    values: list[float] = []
    for index in range(count):
        left = index // stride
        blend = (index % stride) / stride
        eased = blend * blend * (3.0 - 2.0 * blend)
        values.append(anchors[left] * (1.0 - eased) + anchors[left + 1] * eased)
    return values[:count]


def make_stem(kind: str, seed: int) -> list[float]:
    count = SAMPLE_RATE * DURATION_SECONDS
    rng = random.Random(seed)
    slow = smooth_noise(rng, count, SAMPLE_RATE // 3)
    detail = smooth_noise(rng, count, SAMPLE_RATE // 80)
    gust = smooth_noise(rng, count, SAMPLE_RATE // 2)
    samples: list[float] = []
    for index in range(count):
        t = index / SAMPLE_RATE
        if kind == "fap_institutional":
            # Cool fluorescent hum, distant corridor tap, paper rustle: an
            # institutional interior, deliberately drier than the house.
            hum = 0.02 * math.sin(2.0 * math.pi * 100 * t)
            hum += 0.012 * math.sin(2.0 * math.pi * 200 * t)
            tap = 0.0
            tap_period = SAMPLE_RATE * 2.7
            tap_phase = index % tap_period
            if tap_phase < SAMPLE_RATE * 0.02:
                tap = 0.03 * math.sin(2.0 * math.pi * 900 * (tap_phase / SAMPLE_RATE))
            paper = 0.01 * detail[index] * (0.5 + 0.5 * math.sin(t * 0.5))
            value = hum + tap + paper + 0.008 * slow[index]
        elif kind == "zirat_wind":
            # Restrained open-field wind with grass detail and a far tractor
            # line: quiet remembrance, no ornament.
            wind = 0.055 * slow[index] + 0.02 * gust[index] * (0.5 + 0.5 * math.sin(t * 0.35))
            grass = 0.014 * detail[index]
            far = 0.008 * math.sin(2.0 * math.pi * (98 + 3 * math.sin(t * 0.22)) * t)
            value = wind + grass + far
        elif kind == "village_arrival":
            # The arrival edge: same village bed, slightly more open sky.
            wind = 0.05 * slow[index]
            birds = 0.03 * math.sin(2.0 * math.pi * (2430 + 140 * math.sin(t * 0.6)) * t)
            distant = 0.016 * math.sin(2.0 * math.pi * 156 * t)
            value = wind + 0.02 * detail[index] + birds + distant
        elif kind == "village_yard":
            # Babai and ebi's yard: warmer, domestic layer behind the fence.
            hen = 0.028 * math.sin(2.0 * math.pi * (620 + 30 * math.sin(t * 1.1)) * t) * (0.5 + 0.5 * math.sin(t * 0.9))
            firewood = 0.012 * detail[index]
            home = 0.014 * math.sin(2.0 * math.pi * 220 * t)
            value = 0.045 * slow[index] + hen + firewood + home
        elif kind == "village_return":
            # The return street towards the outskirts: emptier, more wind,
            # household life fading behind.
            wind = 0.06 * slow[index] + 0.018 * gust[index]
            fading = 0.012 * detail[index] * (0.5 + 0.5 * math.sin(t * 0.4))
            value = wind + fading + 0.01 * math.sin(2.0 * math.pi * 130 * t)
        else:
            raise ValueError(f"Unknown layer kind: {kind}")
        samples.append(max(-0.8, min(0.8, value)))
    # Crossfade the loop seam so the stem loops without a pop.
    fade = SAMPLE_RATE // 4
    for index in range(fade):
        blend = index / fade
        samples[index] = samples[index] * blend + samples[count - fade + index] * (1.0 - blend)
    return samples[: count - fade]


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
    for kind, seed in (
        ("fap_institutional", 4601),
        ("zirat_wind", 5707),
        ("village_arrival", 6803),
        ("village_yard", 7901),
        ("village_return", 8117),
    ):
        samples = make_stem(kind, seed)
        path = OUT / f"{kind}.wav"
        write_wav(path, samples)
        print(f"act1-layers: {path.name} samples={len(samples)}")


if __name__ == "__main__":
    main()
