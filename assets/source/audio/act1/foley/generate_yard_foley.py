#!/usr/bin/env python3
"""Project-original, deterministic short contacts for the three yard mechanisms.

These are synthesized material contacts, not field recordings. No source audio,
voice, downloaded sample or model output is embedded. Run from any directory.
Only the three named source/runtime WAVs and this generator's manifest are owned.
"""
from __future__ import annotations

import array
import hashlib
import json
import math
import random
import sys
import wave
from pathlib import Path

RATE = 48_000
ROOT = Path(__file__).resolve().parents[5]
SOURCE = Path(__file__).resolve().parent
OUTPUT = ROOT / "game/assets/audio/act1/foley"


def contact(duration: float, seed: int, modes: tuple[tuple[float, float, float], ...],
            strikes: tuple[tuple[float, float], ...], grit_decay: float) -> bytes:
    """Damped inharmonic modes plus filtered impact noise, with quiet endpoints."""
    rng = random.Random(seed)
    count = round(duration * RATE)
    samples = [0.0] * count
    noise = [rng.uniform(-1.0, 1.0) for _ in range(count)]
    smooth = 0.0
    for index, value in enumerate(noise):
        smooth = smooth * .71 + value * .29
        noise[index] = smooth
    for onset, force in strikes:
        begin = round(onset * RATE)
        for index in range(begin, count):
            time = (index - begin) / RATE
            attack = min(1.0, time / .0008)
            signal = sum(gain * math.sin(math.tau * frequency * time)
                         * math.exp(-decay * time) for frequency, gain, decay in modes)
            signal += .22 * noise[index] * math.exp(-grit_decay * time)
            samples[index] += force * attack * signal
    # Remove any tiny residual mean, then fade 15ms rather than clip an oscillator.
    mean = sum(samples) / count
    fade = round(.015 * RATE)
    samples = [(value - mean) * min(1.0, index / fade, (count - 1 - index) / fade)
               for index, value in enumerate(samples)]
    peak = max(abs(value) for value in samples) or 1.0
    pcm = array.array("h", (round(value / peak * .63 * 32767) for value in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sources = {
        "wood_tap": (.46, 271,
                     ((187, .7, 34), (421, .31, 49), (863, .11, 70)),
                     ((0, 1), (.173, .44)), 180.0, "Loose wooden side panel against a batten"),
        "metal_rattle": (.66, 509,
                         ((1181, .44, 16), (1769, .29, 13), (2909, .16, 21), (4333, .08, 35)),
                         ((0, .8), (.072, .56), (.157, .26)), 110.0, "Small loose metal shutter latch"),
        "hollow_board": (.63, 811,
                         ((96, .78, 16), (183, .33, 25), (367, .18, 34), (718, .09, 52)),
                         ((0, 1), (.238, .28)), 150.0, "Short board tapping two spacers above a hollow"),
    }
    receipt = []
    for name, (duration, seed, modes, strikes, decay, purpose) in sources.items():
        pcm = contact(duration, seed, modes, strikes, decay)
        for folder in (SOURCE, OUTPUT):
            with wave.open(str(folder / f"{name}.wav"), "wb") as stream:
                stream.setnchannels(1)
                stream.setsampwidth(2)
                stream.setframerate(RATE)
                stream.writeframes(pcm)
        path = OUTPUT / f"{name}.wav"
        receipt.append({"file": str(path.relative_to(ROOT)), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "durationSeconds": duration, "sampleRate": RATE, "channels": 1, "bits": 16,
                        "loop": False, "purpose": purpose, "listeningReview": "external/not-run"})
    (SOURCE / "manifest.json").write_text(json.dumps({
        "origin": "URMAN project-original procedural material foley; not a field recording",
        "generator": str(Path(__file__).resolve().relative_to(ROOT)), "seeded": True,
        "gameplayStateOwner": "RuntimeBridge/world.props; audio never changes progress",
        "files": receipt}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == "__main__":
    main()
