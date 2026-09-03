#!/usr/bin/env python3
"""Generate deterministic, project-original ambience stems for Godot smoke/runtime.

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
        if kind == "village_day":
            wind = 0.05 * slow[index]
            birds = 0.035 * math.sin(2.0 * math.pi * (2_430 + 120 * math.sin(t * 0.7)) * t)
            distant = 0.018 * math.sin(2.0 * math.pi * 156 * t)
            value = wind + 0.022 * detail[index] + birds + distant
        elif kind == "house_room":
            hum = 0.022 * math.sin(2.0 * math.pi * 50 * t)
            crt = 0.028 * math.sin(2.0 * math.pi * (1_850 + 18 * math.sin(t * 0.3)) * t)
            room = 0.016 * slow[index] + 0.008 * detail[index]
            value = hum + crt + room
        elif kind == "kara_urman_edge":
            wind = 0.09 * slow[index] + 0.025 * detail[index]
            branch = 0.018 * math.sin(2.0 * math.pi * (71 + 4 * math.sin(t * 0.45)) * t)
            pulse = 0.012 * math.sin(2.0 * math.pi * 31 * t) * (0.5 + 0.5 * math.sin(t * 0.8))
            value = wind + branch + pulse
        elif kind == "water_edge":
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
        "village_day_ambience.wav": ("village_day", 1101),
        "house_room_tone.wav": ("house_room", 2202),
        "kara_urman_edge_ambience.wav": ("kara_urman_edge", 3303),
        "water_edge_ambience.wav": ("water_edge", 4404),
    }
    for filename, (kind, seed) in stems.items():
        write_wav(OUT / filename, make_stem(kind, seed))
        print(f"ambient-audio: {filename} {SAMPLE_RATE}Hz/{DURATION_SECONDS}s seed={seed}")


if __name__ == "__main__":
    main()
