#!/usr/bin/env python3
"""Prepare recorded Act I beds from hash-verified local CC0 previews.

Usage: python3 tools/audio/generate_act1_ambience_layers.py SOURCE_DIRECTORY
The directory contains the unmodified sourcePreviewUrl basenames declared in
ambient_manifest.json. No downloads or procedural fallback are performed.
Listening and final mix acceptance remain separate from PCM preparation.
"""
from __future__ import annotations

import argparse
from array import array
import hashlib
import json
import math
from pathlib import Path
import subprocess
import sys
import tempfile
from urllib.parse import urlparse
import wave

from generate_act1_footsteps import normalize_pcm_wav

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game/assets/audio"
RATE = 24_000


def prepare(samples: array, settings: dict) -> tuple[array, dict]:
    start = round(settings["startSeconds"] * RATE)
    count = round(settings["durationSeconds"] * RATE)
    overlap = round(settings["crossfadeSeconds"] * RATE)
    if start < 0 or overlap < 2 or count <= 2 * overlap or start + count > len(samples):
        raise ValueError("Recorded bed slice or loop overlap is outside its source")
    segment = samples[start:start + count]
    # Retain the uninterrupted middle, then join its tail to its head. The
    # boundary after the join is the original pair source[overlap-1:overlap+1].
    values = [value / 32768.0 for value in segment[overlap:-overlap]]
    for i in range(overlap):
        angle = i / (overlap - 1) * math.pi / 2
        values.append((segment[-overlap + i] * math.cos(angle)
                       + segment[i] * math.sin(angle)) / 32768.0)
    rms = math.sqrt(sum(value * value for value in values) / len(values))
    peak = max(abs(value) for value in values)
    target = settings["targetRms"]
    if not 0 < target < .70 or rms == 0 or not math.isfinite(rms):
        raise ValueError("Invalid target level or silent source")
    gain = min(target / rms, .70 / peak)
    output = array('h', (round(value * gain * 32767) for value in values))
    return output, {
        "durationSeconds": len(output) / RATE,
        "gainDb": 20 * math.log10(gain),
        "peak": max(abs(value) for value in output) / 32768,
        "rms": math.sqrt(sum(value * value for value in output) / len(output)) / 32768,
        "loopBoundaryDelta": abs(output[-1] - output[0]) / 32768,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_directory", type=Path)
    args = parser.parse_args()
    manifest = json.loads((OUT / "ambient_manifest.json").read_text())
    stems = [stem for stem in manifest["stems"] if "preparation" in stem]
    if not stems:
        raise ValueError("No recorded preparations are declared")
    sources = {}
    # Verify every source before replacing any game asset.
    for stem in stems:
        filename = Path(urlparse(stem["sourcePreviewUrl"]).path).name
        source = args.source_directory / filename
        if hashlib.sha256(source.read_bytes()).hexdigest() != stem["sourcePreviewSha256"]:
            raise ValueError(f"Source hash mismatch: {source}")
        sources[filename] = source
    receipts = []
    with tempfile.TemporaryDirectory(prefix="urman-ambience-") as temporary:
        temp = Path(temporary)
        decoded = {}
        for filename, source in sources.items():
            stage, pcm = temp / (filename + '.stage.wav'), temp / (filename + '.pcm.wav')
            subprocess.run(['afconvert', '-f', 'WAVE', '-d', f'LEI16@{RATE}', '-c', '1',
                            str(source), str(stage)], check=True)
            normalize_pcm_wav(stage, pcm, sample_rate=RATE)
            with wave.open(str(pcm), 'rb') as stream:
                samples = array('h', stream.readframes(stream.getnframes()))
            if sys.byteorder != 'little':
                samples.byteswap()
            decoded[filename] = samples
        for stem in stems:
            filename = Path(urlparse(stem["sourcePreviewUrl"]).path).name
            samples, metrics = prepare(decoded[filename], stem["preparation"])
            destination = OUT / Path(stem["file"]).name
            if destination.suffix != '.wav':
                raise ValueError(f"Unexpected output: {destination}")
            staged = temp / destination.name
            if sys.byteorder != 'little':
                samples.byteswap()
            with wave.open(str(staged), 'wb') as stream:
                stream.setnchannels(1)
                stream.setsampwidth(2)
                stream.setframerate(RATE)
                stream.writeframes(samples.tobytes())
            # Stage beside the destination for an atomic same-filesystem replace.
            replacement = destination.with_suffix('.wav.tmp')
            replacement.write_bytes(staged.read_bytes())
            replacement.replace(destination)
            receipts.append(dict(id=stem['id'], file=stem['file'],
                                 sha256=hashlib.sha256(destination.read_bytes()).hexdigest(), **metrics))
    print(json.dumps(receipts, indent=2))


if __name__ == "__main__":
    main()
