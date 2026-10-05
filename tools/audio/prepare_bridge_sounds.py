#!/usr/bin/env python3
"""Prepare the suspension-bridge sounds from CC0 recordings.

Re-decodes the licence-verified Freesound HQ previews kept in
``/tmp/urman_bridge_sources`` (the tool verifies every source sha256 and size
against ``manifest.json`` before decoding and fails loudly on a mismatch),
resamples them to 24 kHz mono PCM16 WAV with ``ffmpeg``, then slices, loop-seams
and levels with Python's standard library only.  No RNG is used and every cut is
a fixed constant, so the outputs are reproducible byte for byte.

Sources (both Freesound, CC0 1.0; the licence and link were read from the served
sound page on 2026-10-04, see the manifest and the generated ``credits.json``):

* Rmutt - "Mooring Rope.wav", sound 145721.  A large rope under tension on a
  ferry cleat; the loudest groan run sits around 30-38 s.  The loop takes the
  steady 36.10-43.10 s window, so the rope keeps straining without any one loud
  tongue dominating.
* Roxis_Boy - "Chain Rattling - 1", sound 401622.  One short chain rattle burst
  at 6.18-6.68 s serves as the hand-rope/metal rattle of the suspension bridge.

Outputs (``game/assets/audio/act1/bridge/``):

* ``bridge_groan_loop.wav`` - the sway/groan bed: 7.0 s of the mooring rope with
  a 0.5 s crossfade seam, looped at runtime by SuspensionBridgeDynamics.
* ``bridge_rope_rattle.wav`` - one 0.5 s metal rattle one-shot.
* ``credits.json`` - provenance (page URL, author, licence, hashes, slices).

The same house gain policy as ``prepare_sound_mood_shots.py`` is used:
gain = min(0.13/rms, 0.65/peak); the one-shot gets 20 ms edge fades, the loop
needs none because its seam is the crossfade.
"""

from __future__ import annotations

import argparse
import array
import hashlib
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE_ROOT = Path("/tmp/urman_bridge_sources")
DEFAULT_OUT = ROOT / "game/assets/audio/act1/bridge"
RATE = 24_000
EDGE_FADE_SAMPLES = 480  # 20 ms at 24 kHz, as in prepare_sound_mood_shots.py
GAIN_RMS = 0.13
GAIN_PEAK = 0.65

# Fixed, deterministic cuts (source-relative seconds).
GROAN_START = 36.10
GROAN_SECONDS = 7.0
GROAN_CROSSFADE_SECONDS = 0.5
RATTLE_START = 6.18
RATTLE_SECONDS = 0.50

CLIPS = (
    # id, manifest slot key, start seconds, seconds, loop crossfade seconds
    ("bridge_groan_loop", "groan", GROAN_START, GROAN_SECONDS, GROAN_CROSSFADE_SECONDS),
    ("bridge_rope_rattle", "rattle", RATTLE_START, RATTLE_SECONDS, None),
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dyld_fallback_for(stderr: str) -> str | None:
    """Library directories that repair dyld "Library not loaded" errors.

    Mirrors the resolver in prepare_sound_mood_shots.py; Homebrew upgrades can
    replace versioned dylibs (seen with libx265) while ffmpeg still links the
    old names.
    """
    missing = re.findall(r"Library not loaded: (\S+)", stderr)
    directories: list[str] = []
    for text in missing:
        path = Path(text)
        cellar = Path("/opt/homebrew/Cellar") / path.parent.parent.name
        candidates = sorted(cellar.glob(f"*/lib/{path.name}"))
        if not candidates:
            directories = []
            break
        for candidate in candidates:
            directory = str(candidate.parent)
            if directory not in directories:
                directories.append(directory)
    if missing and directories:
        return ":".join(directories)
    libraries = sorted(str(path) for path in Path("/opt/homebrew/Cellar").glob("*/*/lib"))
    return ":".join(libraries) if libraries else None


def resolve_ffmpeg(explicit: str | None) -> tuple[str, dict, str, str | None]:
    candidates: list[str] = []
    if explicit:
        candidates.append(explicit)
    candidates.append(shutil.which("ffmpeg") or "")
    candidates.extend(["/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg"])
    ordered: list[str] = []
    for candidate in candidates:
        if candidate and candidate not in ordered:
            ordered.append(candidate)

    fallback: str | None = None
    for binary in ordered:
        if not Path(binary).is_file():
            continue
        environment = dict(os.environ)
        probe = subprocess.run([binary, "-version"], capture_output=True)
        if probe.returncode == 0:
            version = probe.stdout.decode(errors="replace").splitlines()[0].strip()
            return binary, environment, version, fallback
        if sys.platform != "darwin" or not Path("/opt/homebrew/Cellar").is_dir():
            continue
        fallback = dyld_fallback_for(probe.stderr.decode(errors="replace"))
        if fallback is None:
            continue
        existing = environment.get("DYLD_FALLBACK_LIBRARY_PATH")
        environment["DYLD_FALLBACK_LIBRARY_PATH"] = fallback + ":" + existing if existing else fallback
        probe = subprocess.run([binary, "-version"], capture_output=True, env=environment)
        if probe.returncode == 0:
            version = probe.stdout.decode(errors="replace").splitlines()[0].strip()
            return binary, environment, version, fallback
    raise SystemExit(
        "ffmpeg is required and no usable binary was found "
        "(tried: " + ", ".join(ordered) + "); pass --ffmpeg <path>"
    )


def decode(ffmpeg: str, environment: dict, source: Path, destination: Path) -> None:
    command = [
        ffmpeg, "-y", "-v", "error", "-nostdin", "-i", str(source),
        "-ac", "1", "-ar", str(RATE), "-acodec", "pcm_s16le", "-f", "wav", str(destination),
    ]
    result = subprocess.run(command, capture_output=True, env=environment)
    if result.returncode != 0:
        raise SystemExit(
            "ffmpeg decode failed for " + str(source) + ":\n" + result.stderr.decode(errors="replace")[-1000:]
        )


def read_wav16(path: Path) -> array.array:
    with wave.open(str(path), "rb") as stream:
        header = (stream.getnchannels(), stream.getsampwidth(), stream.getframerate())
        if header != (1, 2, RATE):
            raise SystemExit(f"decoded file is not 24 kHz mono PCM16: {path} has {header}")
        frames = stream.readframes(stream.getnframes())
    samples = array.array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()
    return samples


def crossfade_loop(values: array.array, fade_samples: int) -> array.array:
    """Blend the window tail into its head so playback wraps without a click."""
    count = len(values)
    fade = min(fade_samples, count // 4)
    out = array.array("d", values)
    for index in range(fade):
        share = (index + 1) / fade
        out[count - fade + index] = values[count - fade + index] * (1.0 - share) + values[index] * share
    return out


def quantize(samples, fade_samples: int) -> tuple[array.array, float, float]:
    """House gain policy: gain = min(0.13/rms, 0.65/peak), then optional fades."""
    values = [value / 32768.0 for value in samples]
    peak = max(abs(value) for value in values)
    rms = math.sqrt(sum(value * value for value in values) / len(values))
    if peak == 0:
        raise SystemExit("silent clip")
    gain = min(GAIN_RMS / max(0.001, rms), GAIN_PEAK / peak)
    fade = min(fade_samples, len(values) // 4)
    data = array.array(
        "h",
        (
            round(value * gain * 32767 * (min(1, index / fade, (len(values) - 1 - index) / fade) if fade else 1))
            for index, value in enumerate(values)
        ),
    )
    return data, gain, fade


def write_wav(out_dir: Path, name: str, data: array.array) -> str:
    out_dir.mkdir(parents=True, exist_ok=True)
    destination = out_dir / f"{name}.wav"
    staged = destination.with_name(destination.name + ".tmp")
    payload = data.tobytes()
    if sys.byteorder != "little":
        swapped = array.array("h", data)
        swapped.byteswap()
        payload = swapped.tobytes()
    with wave.open(str(staged), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(payload)
    staged.replace(destination)
    return sha256_file(destination)


def write_json(out_dir: Path, name: str, document: dict) -> Path:
    out_dir.mkdir(parents=True, exist_ok=True)
    destination = out_dir / name
    staged = destination.with_name(destination.name + ".tmp")
    staged.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
    staged.replace(destination)
    return destination


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT,
                        help="directory holding manifest.json and files/ (default: %(default)s)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT,
                        help="output directory (default: game/assets/audio/act1/bridge)")
    parser.add_argument("--ffmpeg", default=None, help="ffmpeg binary to use")
    args = parser.parse_args(argv)

    source_root = args.source_root if args.source_root.is_absolute() else ROOT / args.source_root
    out_dir = args.out if args.out.is_absolute() else ROOT / args.out
    manifest_path = source_root / "manifest.json"
    if not manifest_path.is_file():
        raise SystemExit(f"missing source manifest: {manifest_path}")
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes)
    ffmpeg, environment, version, fallback = resolve_ffmpeg(args.ffmpeg)
    print(f"ffmpeg: {ffmpeg} ({version})", flush=True)
    if fallback:
        print(f"ffmpeg dyld fallback libraries: {fallback}", flush=True)

    receipts: list[dict] = []
    license_reading: list[dict] = []
    with tempfile.TemporaryDirectory(prefix="urman-bridge-sounds-") as temporary:
        temp = Path(temporary)
        for name, slot, start_seconds, seconds, crossfade_seconds in CLIPS:
            primary = manifest["slots"][slot]["primary"]
            source = source_root / Path(primary["local_path"]).name
            if not source.is_file():
                source = source_root / primary["local_path"]
            if not source.is_file():
                raise SystemExit(f"missing verified source: {source}")
            actual_sha = sha256_file(source)
            if actual_sha != primary["sha256"] or source.stat().st_size != primary["bytes"]:
                raise SystemExit(
                    "source sha256/size does not match the manifest, refusing to decode:\n"
                    f"  file:     {source}\n"
                    f"  manifest: {primary['sha256']} ({primary['bytes']} bytes)\n"
                    f"  actual:   {actual_sha} ({source.stat().st_size} bytes)\n"
                    f"  re-download from {primary['source_page']}"
                )

            decoded = temp / f"{name}.wav"
            decode(ffmpeg, environment, source, decoded)
            samples = read_wav16(decoded)
            decoded_seconds = len(samples) / RATE
            if abs(decoded_seconds - primary["duration_s"]) > 0.5:
                raise SystemExit(
                    f"{name}: decoded {decoded_seconds:.3f} s but the manifest says {primary['duration_s']:.3f} s"
                )
            begin = round(start_seconds * RATE)
            count = round(seconds * RATE)
            if begin + count > len(samples):
                raise SystemExit(f"{name}: window {start_seconds}..{start_seconds + seconds} s exceeds the source")
            clip = samples[begin : begin + count]
            if crossfade_seconds is not None:
                prepared = crossfade_loop(clip, round(crossfade_seconds * RATE))
                fade_note = f"{crossfade_seconds:.3f} s loop crossfade (no edge fades)"
                data, gain, fade = quantize(prepared, 0)
            else:
                prepared = clip
                fade_note = f"{EDGE_FADE_SAMPLES / RATE * 1000:.0f} ms edge fades"
                data, gain, fade = quantize(prepared, EDGE_FADE_SAMPLES)

            if len(data) != count:
                raise SystemExit(f"{name}: prepared {len(data)} samples, expected {count}")
            sha = write_wav(out_dir, name, data)
            receipt = {
                "file": f"res://assets/audio/act1/bridge/{name}.wav",
                "id": name,
                "role": "sway-groan loop" if crossfade_seconds is not None else "handrope metal rattle one-shot",
                "seconds": round(seconds, 6),
                "pcmBytes": len(data) * 2,
                "sha256": sha,
                "source": primary["source_page"],
                "directUrl": primary["direct_url"],
                "author": primary["author"],
                "title": primary["title"],
                "license": primary["license"],
                "licenseUrl": primary["license_url"],
                "sourceSha256": primary["sha256"],
                "startSeconds": round(start_seconds, 6),
                "endSeconds": round(start_seconds + seconds, 6),
                "sourceDurationSeconds": primary["duration_s"],
                "preparation": (
                    "ffmpeg decode to 24 kHz mono PCM16; "
                    f"slice {start_seconds:.3f}-{start_seconds + seconds:.3f} s; {fade_note}; "
                    f"gain min(0.13/rms, 0.65/peak) = {gain:.4f}"
                ),
                "description": primary["description"],
            }
            receipts.append(receipt)
            license_reading.append(
                {
                    "slot": slot,
                    "source": primary["source_page"],
                    "author": primary["author"],
                    "title": primary["title"],
                    "license": primary["license"],
                    "licenseUrl": primary["license_url"],
                    "readFrom": f"served Freesound page fetched 2026-10-04; recorded in {manifest_path} slots.{slot}.primary",
                }
            )
            print(
                f"{name}: seconds={seconds:.3f} pcmBytes={receipt['pcmBytes']} sha256={sha} "
                f"sourceStart={start_seconds:.3f}s gain={gain:.4f}",
                flush=True,
            )

    credits = {
        "schemaVersion": 1,
        "kind": "urman.act1_bridge_sounds",
        "generator": "tools/audio/prepare_bridge_sounds.py",
        "subject": "suspension bridge over the Kara-Urman gorge: sway/groan loop and hand-rope metal rattle; plank creaks are the existing parquet/creak_00..02.wav",
        "listening": "external/not-run",
        "sourceManifest": {
            "path": str(manifest_path),
            "sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        },
        "gainPolicy": "min(0.13/rms, 0.65/peak), 20 ms edge fades (loop: 0.5 s crossfade seam, no fades), 24 kHz mono PCM16",
        "licenseReading": license_reading,
        "clips": [
            {
                key: receipt[key]
                for key in (
                    "file", "id", "role", "seconds", "pcmBytes", "sha256", "source", "directUrl",
                    "author", "title", "license", "licenseUrl", "sourceSha256", "startSeconds",
                    "endSeconds", "sourceDurationSeconds", "preparation", "description",
                )
            }
            for receipt in receipts
        ],
    }
    credits_path = write_json(out_dir, "credits.json", credits)
    print(f"credits: {credits_path}", flush=True)
    print("total PCM bytes: " + str(sum(receipt["pcmBytes"] for receipt in receipts)), flush=True)


if __name__ == "__main__":
    main()
