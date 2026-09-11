#!/usr/bin/env python3
"""Generate the legacy non-Act-I water ambience only.

Act I recordings are prepared by generate_act1_ambience_layers.py; never
overwrite them with procedural beds.

These are intentionally non-voice sound-design stems. They prove the runtime
audio path and give the first-person zones a quiet bed; they are not a claim of
final field recording, dialogue performance, or cultural sound approval.
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
    samples: list[float] = []
    for index in range(count):
        t = index / SAMPLE_RATE
        if kind == "water_edge":
            ripple = 0.045 * slow[index] + 0.035 * detail[index]
            shimmer = 0.018 * math.sin(2.0 * math.pi * (310 + 22 * math.sin(t * 0.8)) * t)
            value = ripple + shimmer
        else:
            raise ValueError(kind)

        # Short equal-power fades make the stems safe to loop in-engine.
        fade = min(1.0, index / (SAMPLE_RATE * 0.12), (count - index - 1) / (SAMPLE_RATE * 0.12))
        samples.append(max(-0.22, min(0.22, value * fade)))
    return samples


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(SAMPLE_RATE)
        frames = bytearray()
        for sample in samples:
            frames.extend(int(sample * 32767).to_bytes(2, "little", signed=True))
        stream.writeframes(frames)


def main() -> None:
    stems = {
        "water_edge_ambience.wav": ("water_edge", 4404),
    }
    for filename, (kind, seed) in stems.items():
        write_wav(OUT / filename, make_stem(kind, seed))
        print(f"ambient-audio: {filename} {SAMPLE_RATE}Hz/{DURATION_SECONDS}s seed={seed}")


if __name__ == "__main__":
    main()
