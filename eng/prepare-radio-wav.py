#!/usr/bin/env python3
"""Canonicalise decoded PCM16 and attenuate its peak without changing its timeline.

This edits an existing recording. It creates no speech/music and leaves the
original untouched. Both output WAV and processing receipt must be new files.
"""

import argparse
import array
import hashlib
import importlib.util
import io
import json
import math
from pathlib import Path
import sys
import wave

sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location("radio_delivery", Path(__file__).with_name("apply-act1-radio-recordings.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--peak-db", type=float, default=-3)
    args = parser.parse_args()
    receipt_path = args.destination.with_suffix(args.destination.suffix + ".processing.json")
    if args.destination.exists() or receipt_path.exists() or args.source.resolve() == args.destination.resolve():
        parser.error("destination and processing receipt must both be new files")
    if not math.isfinite(args.peak_db) or not -12 <= args.peak_db <= -1:
        parser.error("peak ceiling must be between -12 and -1 dBFS")
    original = args.source.read_bytes()
    fmt, data = MODULE.read_wav_pcm(original)
    if fmt[5] != 16:
        parser.error("this bounded preparation step accepts already decoded PCM16")
    samples = array.array("h")
    samples.frombytes(data.tobytes())
    if sys.byteorder != "little":
        samples.byteswap()
    peak = max(abs(value) for value in samples)
    if not peak:
        parser.error("input recording is silent")
    gain = min(1, (10 ** (args.peak_db / 20)) * 32768 / peak)
    processed = array.array("h", (round(value * gain) for value in samples))
    if sys.byteorder != "little":
        processed.byteswap()
    output = io.BytesIO()
    with wave.open(output, "wb") as stream:
        stream.setnchannels(fmt[1])
        stream.setsampwidth(2)
        stream.setframerate(fmt[2])
        stream.writeframes(processed.tobytes())
    prepared = output.getvalue()
    audio = MODULE.inspect_wav(prepared, "music")
    if audio["frames"] != len(data) // fmt[4]:
        raise ValueError("preparation changed the decoded frame count")
    args.destination.parent.mkdir(parents=True, exist_ok=True)
    with args.destination.open("xb") as target:
        target.write(prepared)
    receipt = {"schemaVersion": 1, "tool": "eng/prepare-radio-wav.py", "pythonVersion": sys.version.split()[0],
               "source": args.source.name, "sourceSha256": hashlib.sha256(original).hexdigest(),
               "output": args.destination.name, "outputSha256": hashlib.sha256(prepared).hexdigest(),
               "processing": "canonical integer PCM16 WAVE; constant attenuation only; no resampling, trimming, time stretch, or generated samples",
               "linearGain": gain, "gainDb": 20 * math.log10(gain), "audio": audio,
               "acceptance": "technical preparation only; source rights and listening remain separate"}
    with receipt_path.open("x") as output:
        json.dump(receipt, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
