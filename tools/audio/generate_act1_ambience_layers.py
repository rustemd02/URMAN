#!/usr/bin/env python3
"""Generate deterministic, project-original ambience layer stems for Act I.

The four procedural sub-zone beds have deliberately different sonic roles:
open wet arrival, warm domestic yard, fading return street and dry
institutional FAP. The source-backed ``zirat_wind.wav`` stem is deliberately
omitted from this generator; its external CC0 preview conversion is recorded
separately. These procedural beds remain production candidates, not field
recordings, voice performances or a final mix (AUDIO-014/CULTURE-004).
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
LOOP_FADE_SECONDS = 0.25


def smooth_noise(rng: random.Random, count: int, stride: int) -> list[float]:
    anchors = [rng.uniform(-1.0, 1.0) for _ in range(count // stride + 2)]
    values: list[float] = []
    for index in range(count):
        left = index // stride
        blend = (index % stride) / stride
        eased = blend * blend * (3.0 - 2.0 * blend)
        values.append(anchors[left] * (1.0 - eased) + anchors[left + 1] * eased)
    return values[:count]


def event_mix(t: float, events: tuple[tuple[float, float, float, float, float, float], ...]) -> float:
    """Return restrained deterministic tonal events with soft attack/release."""
    value = 0.0
    for start, duration, start_hz, end_hz, amplitude, phase in events:
        progress = (t - start) / duration
        if not 0.0 < progress < 1.0:
            continue
        envelope = math.sin(math.pi * progress) ** 2
        frequency = start_hz + (end_hz - start_hz) * progress
        local = t - start
        cycle = start_hz * local + 0.5 * (end_hz - start_hz) * local * progress
        value += amplitude * envelope * math.sin(2.0 * math.pi * cycle + phase)
        # A second, quieter partial makes short material events less synthetic
        # without turning them into a foreground cue.
        value += amplitude * 0.22 * envelope * math.sin(2.0 * math.pi * frequency * 0.5 * local + phase * 0.7)
    return value


def close_loop(samples: list[float]) -> list[float]:
    """Ease the tail to the first sample while retaining the full 8 s stem."""
    fade = min(int(SAMPLE_RATE * LOOP_FADE_SECONDS), len(samples) // 2)
    first = samples[0]
    for index in range(fade):
        progress = index / max(1, fade - 1)
        eased = progress * progress * (3.0 - 2.0 * progress)
        tail_index = len(samples) - fade + index
        samples[tail_index] = samples[tail_index] * (1.0 - eased) + first * eased
    samples[-1] = first
    return samples


def make_stem(kind: str, seed: int) -> list[float]:
    count = SAMPLE_RATE * DURATION_SECONDS
    rng = random.Random(seed)
    slow = smooth_noise(rng, count, SAMPLE_RATE // 3)
    detail = smooth_noise(rng, count, SAMPLE_RATE // 80)
    fine = smooth_noise(rng, count, max(1, SAMPLE_RATE // 400))
    gust = smooth_noise(rng, count, SAMPLE_RATE // 2)
    samples: list[float] = []
    for index in range(count):
        t = index / SAMPLE_RATE
        if kind == "fap_institutional":
            # Cool fluorescent/radiator air, sparse paperwork and wall ticks:
            # dry and legible under dialogue, with no metronomic repetition.
            hum = 0.016 * math.sin(2.0 * math.pi * 100 * t)
            hum += 0.007 * math.sin(2.0 * math.pi * 200 * t)
            radiator = 0.008 * math.sin(2.0 * math.pi * 50 * t) * (0.7 + 0.3 * slow[index])
            paper = 0.006 * detail[index] + 0.003 * fine[index]
            ticks = event_mix(t, (
                (1.18, 0.050, 920.0, 680.0, 0.010, 0.3),
                (3.76, 0.042, 760.0, 560.0, 0.008, 1.7),
                (6.42, 0.055, 840.0, 620.0, 0.009, 2.4),
            ))
            value = hum + radiator + paper + ticks + 0.009 * slow[index]
        elif kind == "village_arrival":
            # Arrival under wet open sky: broad rain/air bed, road resonance and
            # two very quiet distant calls rather than a constant bird tone.
            rain_air = 0.034 * slow[index] + 0.010 * gust[index]
            rain_air += 0.006 * detail[index] + 0.003 * fine[index]
            road = 0.010 * math.sin(2.0 * math.pi * 73 * t)
            road += 0.004 * math.sin(2.0 * math.pi * 146 * t)
            distant_calls = event_mix(t, (
                (0.82, 0.18, 2180.0, 2760.0, 0.011, 0.6),
                (5.56, 0.21, 2480.0, 1900.0, 0.009, 2.0),
            ))
            drip = event_mix(t, (
                (2.46, 0.11, 1320.0, 860.0, 0.008, 1.1),
                (6.86, 0.10, 1180.0, 780.0, 0.007, 2.6),
            ))
            value = rain_air + road + distant_calls + drip
        elif kind == "village_yard":
            # Babai and әби's yard: warmer low-mid resonance, fence/wood
            # movement and roof drips; domestic but never a foreground cue.
            air = 0.037 * slow[index] + 0.006 * detail[index]
            air += 0.003 * fine[index]
            home = 0.012 * math.sin(2.0 * math.pi * 185 * t)
            home += 0.004 * math.sin(2.0 * math.pi * 370 * t)
            wood = event_mix(t, (
                (1.24, 0.24, 240.0, 150.0, 0.011, 0.2),
                (4.72, 0.19, 190.0, 120.0, 0.009, 1.4),
            ))
            drips = event_mix(t, (
                (2.92, 0.10, 960.0, 640.0, 0.007, 0.9),
                (6.34, 0.12, 1040.0, 700.0, 0.006, 2.3),
            ))
            value = air + home + wood + drips
        elif kind == "village_return":
            # Return street: emptier and windier, with the domestic high band
            # receding across the bed instead of a repeated warning motif.
            wind = 0.058 * slow[index] + 0.020 * gust[index]
            wind += 0.004 * fine[index]
            fading = 0.010 * detail[index] * max(0.15, 1.0 - t / DURATION_SECONDS)
            low = 0.009 * math.sin(2.0 * math.pi * 118 * t)
            low += 0.003 * math.sin(2.0 * math.pi * 236 * t)
            fence = event_mix(t, (
                (2.68, 0.33, 210.0, 110.0, 0.009, 0.7),
                (6.46, 0.28, 180.0, 96.0, 0.008, 2.0),
            ))
            value = wind + fading + low + fence
        else:
            raise ValueError(f"Unknown layer kind: {kind}")
        samples.append(max(-0.8, min(0.8, value)))
    return close_loop(samples)


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
