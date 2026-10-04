#!/usr/bin/env python3
"""Prepare the four bath-spirit (мунча иясе) sounds from CC0 recordings.

Re-decodes the license-verified Freesound HQ previews kept in
``/tmp/urman_bath_spirit_sources`` (the tool verifies every source sha256 and
size against ``manifest.json`` before decoding and fails loudly on a mismatch),
resamples them to 24 kHz mono PCM16 WAV with ``ffmpeg``, then slices, mixes,
levels and fades with Python's standard library only.  No RNG is used and every
cut is a fixed constant, so the outputs are reproducible byte for byte.

Sources (all Freesound, CC0 1.0; the licence and link were read from the served
sound page, see the manifest and the generated ``credits.json``):

* D4XX - "Deep Breath (Male)", sound 567267.  Envelope: inhale 0.0-1.2 s,
  long exhale 2.5-4.3 s, quiet inhale 6.0-7.1 s, long exhale 8.0-9.4 s.
* RutgerMuller - "Fast Wet Footsteps in Small Shower Cabin.wav", sound 51148
  (bare feet in water on a shower mat).
* Rudmer_Rotteveel - "Wood Creak Single V10", sound 506665.
* JustineOu - "steam.WAV", sound 672248 (steady hiss section 26.0-31.5 s).

Outputs (``game/assets/audio/act1/bath_spirit/``):

* ``spirit_breath_loop.wav`` - the continuous spirit bed: one slow breath
  cycle (inhale + long exhale, 0.03-4.62 s of the source) with a faint steam
  hiss (26.0-31.5 s) mixed under it at about -16 dB relative level.  One
  stream for the shared pool's bed voice.
* ``spirit_exhale.wav`` - the source's second long exhale (7.88-9.68 s), the
  manifestation's closing cue.
* ``spirit_wet_step.wav`` - one isolated clean step from 5.46-6.02 s, slowed
  to 86 % speed so the step reads as heavier than a human's.
* ``spirit_creak.wav`` - one dry wooden creak, 0.02-0.62 s of the source.
* ``credits.json`` - provenance (page URL, author, licence, hashes, slice).

The same house gain policy as ``prepare_sound_mood_shots.py`` is used:
gain = min(0.13/rms, 0.65/peak), then 20 ms edge fades (30 ms for the loop).
Every output is small: the four files together stay under 0.5 MB of PCM.
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
DEFAULT_SOURCE_ROOT = Path("/tmp/urman_bath_spirit_sources")
DEFAULT_OUT = ROOT / "game/assets/audio/act1/bath_spirit"
RATE = 24_000
EDGE_FADE_SAMPLES = 480  # 20 ms at 24 kHz, as in prepare_sound_mood_shots.py
LOOP_FADE_SAMPLES = 720  # 30 ms, the loop seam is made in the source's own silence
GAIN_RMS = 0.13
GAIN_PEAK = 0.65

# Fixed, deterministic cuts (source-relative seconds).
LOOP_BREATH_START = 0.030
LOOP_BREATH_END = 4.620
LOOP_STEAM_START = 26.000
LOOP_STEAM_END = 31.500
EXHALE_START = 7.880
EXHALE_END = 9.680
STEP_START = 5.460
STEP_END = 6.020
STEP_SPEED = 0.86  # slower = heavier spirit step
CREAK_START = 0.020
CREAK_END = 0.620
BREATH_TARGET_RMS = 0.120
STEAM_TARGET_RMS = 0.020

SOURCES = {
    "breath_deep": "Deep Breath (Male)",
    "wet_steps": "Fast Wet Footsteps in Small Shower Cabin.wav",
    "wood_creak": "Wood Creak Single V10",
    "steam_hiss": "steam.WAV",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dyld_fallback_for(stderr: str) -> str | None:
    """Library directories that repair dyld "Library not loaded" errors.

    Homebrew upgrades replace versioned dylibs (for example libx265.216.dylib
    after an x265 bump) while an installed ffmpeg still links the old names.
    Mirrors the resolver in prepare_sound_mood_shots.py.
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
        environment["DYLD_FALLBACK_LIBRARY_PATH"] = (
            fallback + ":" + existing if existing else fallback
        )
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
            "ffmpeg decode failed for " + str(source) + ":\n"
            + result.stderr.decode(errors="replace")[-1000:]
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


def slice_seconds(samples: array.array, start: float, end: float) -> array.array:
    begin = round(start * RATE)
    count = round((end - start) * RATE)
    if begin < 0 or begin + count > len(samples):
        raise SystemExit(
            f"slice {start:.3f}-{end:.3f} s does not fit the {len(samples) / RATE:.3f} s source"
        )
    return samples[begin : begin + count]


def rms_of(samples: array.array) -> float:
    values = [value / 32768.0 for value in samples]
    return math.sqrt(sum(value * value for value in values) / len(values))


def scale_to_rms(samples: array.array, target: float) -> array.array:
    current = rms_of(samples)
    if current <= 0:
        raise SystemExit("silent source slice; refusing to scale")
    gain = target / current
    return array.array("h", (max(-32768, min(32767, round(value * gain))) for value in samples))


def mix(*layers: array.array) -> array.array:
    length = min(len(layer) for layer in layers)
    return array.array(
        "h",
        (
            max(-32768, min(32767, sum(layer[index] for layer in layers)))
            for index in range(length)
        ),
    )


def slow_down(samples: array.array, speed: float) -> array.array:
    """Linear-interpolation resample by ``speed`` (<1 = slower, lower)."""
    if not 0 < speed <= 1:
        raise SystemExit(f"speed must be within (0, 1]: {speed}")
    count = int(round(len(samples) / speed))
    output = array.array("h", [0]) * count
    for index in range(count):
        position = index * speed
        left = int(position)
        right = min(left + 1, len(samples) - 1)
        fraction = position - left
        output[index] = round(samples[left] * (1 - fraction) + samples[right] * fraction)
    return output


def quantize(samples: array.array, fade_samples: int = EDGE_FADE_SAMPLES) -> tuple[array.array, float, int]:
    """House gain policy: gain = min(0.13/rms, 0.65/peak), then edge fades."""
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
            round(value * gain * 32767 * min(1, index / fade, (len(values) - 1 - index) / fade))
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


def prepare(decoded: dict[str, array.array]) -> list[tuple[str, array.array, dict]]:
    """Return (output name, samples, receipt extras) in a fixed order."""
    loop_breath = scale_to_rms(
        slice_seconds(decoded["breath_deep"], LOOP_BREATH_START, LOOP_BREATH_END),
        BREATH_TARGET_RMS,
    )
    loop_steam = scale_to_rms(
        slice_seconds(decoded["steam_hiss"], LOOP_STEAM_START, LOOP_STEAM_END),
        STEAM_TARGET_RMS,
    )
    breath_loop = mix(loop_breath, loop_steam)
    exhale = slice_seconds(decoded["breath_deep"], EXHALE_START, EXHALE_END)
    wet_step = slow_down(
        slice_seconds(decoded["wet_steps"], STEP_START, STEP_END), STEP_SPEED
    )
    creak = slice_seconds(decoded["wood_creak"], CREAK_START, CREAK_END)
    return [
        (
            "spirit_breath_loop",
            breath_loop,
            {
                "role": "continuous-bed",
                "sourceSlots": ["breath_deep", "steam_hiss"],
                "slices": {
                    "breath": f"{LOOP_BREATH_START:.3f}-{LOOP_BREATH_END:.3f} s",
                    "steam": f"{LOOP_STEAM_START:.3f}-{LOOP_STEAM_END:.3f} s",
                },
                "preparation": (
                    "breath slice scaled to RMS 0.120, steam slice to RMS 0.020 "
                    "(about -16 dB under the breath), summed; "
                ),
            },
        ),
        (
            "spirit_exhale",
            exhale,
            {
                "role": "manifestation-cue",
                "sourceSlots": ["breath_deep"],
                "slices": {"breath": f"{EXHALE_START:.3f}-{EXHALE_END:.3f} s"},
                "preparation": "closing long exhale slice; ",
            },
        ),
        (
            "spirit_wet_step",
            wet_step,
            {
                "role": "manifestation-cue",
                "sourceSlots": ["wet_steps"],
                "slices": {"wet_steps": f"{STEP_START:.3f}-{STEP_END:.3f} s"},
                "preparation": (
                    f"single isolated step sliced, linear-interpolation slowdown to "
                    f"{STEP_SPEED:.2f}x for a heavier-than-human step; "
                ),
            },
        ),
        (
            "spirit_creak",
            creak,
            {
                "role": "manifestation-cue",
                "sourceSlots": ["wood_creak"],
                "slices": {"wood_creak": f"{CREAK_START:.3f}-{CREAK_END:.3f} s"},
                "preparation": "single dry creak, trimmed to its decay; ",
            },
        ),
    ]


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT,
                        help="directory holding manifest.json and files/ (default: %(default)s)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT,
                        help="output directory (default: game/assets/audio/act1/bath_spirit)")
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
    with tempfile.TemporaryDirectory(prefix="urman-bath-spirit-") as temporary:
        temp = Path(temporary)
        decoded: dict[str, array.array] = {}
        for slot in SOURCES:
            entry = manifest["slots"][slot]
            source = source_root / entry["file"]
            if not source.is_file():
                raise SystemExit(f"missing verified source: {source}")
            actual_sha = sha256_file(source)
            if actual_sha != entry["sha256"] or source.stat().st_size != entry["bytes"]:
                raise SystemExit(
                    "source sha256/size does not match the manifest, refusing to decode:\n"
                    f"  file:     {source}\n"
                    f"  manifest: {entry['sha256']} ({entry['bytes']} bytes)\n"
                    f"  actual:   {actual_sha} ({source.stat().st_size} bytes)\n"
                    f"  re-download from {entry['source_page']}"
                )
            decoded_path = temp / f"{slot}.wav"
            decode(ffmpeg, environment, source, decoded_path)
            samples = read_wav16(decoded_path)
            decoded_seconds = len(samples) / RATE
            if abs(decoded_seconds - entry["duration_s"]) > 0.5:
                raise SystemExit(
                    f"{slot}: decoded {decoded_seconds:.3f} s but the manifest says "
                    f"{entry['duration_s']:.3f} s"
                )
            decoded[slot] = samples

        for name, samples, extras in prepare(decoded):
            data, gain, fade = quantize(samples, LOOP_FADE_SAMPLES if name == "spirit_breath_loop" else EDGE_FADE_SAMPLES)
            seconds = len(data) / RATE
            pcm_bytes = len(data) * 2
            sha = write_wav(out_dir, name, data)
            slots = extras["sourceSlots"]
            offsets = []
            for slot in slots:
                entry = manifest["slots"][slot]
                offsets.append({
                    "slot": slot,
                    "sourcePage": entry["source_page"],
                    "directUrl": entry["direct_url"],
                    "author": entry["author"],
                    "title": entry["title"],
                    "license": entry["license"],
                    "licenseUrl": entry["license_url"],
                    "attributionRequired": bool(entry["attribution_required"]),
                    "sourceSha256": entry["sha256"],
                    "sourceDurationSeconds": entry["duration_s"],
                })
            receipt = {
                "file": f"res://assets/audio/act1/bath_spirit/{name}.wav",
                "id": name,
                "role": extras["role"],
                "seconds": round(seconds, 6),
                "pcmBytes": pcm_bytes,
                "sha256": sha,
                "slices": extras["slices"],
                "sources": offsets,
                "preparation": extras["preparation"] + (
                    f"gain min(0.13/rms, 0.65/peak) = {gain:.4f}; "
                    f"{fade / RATE * 1000:.0f} ms edge fades; 24 kHz mono PCM16"
                ),
            }
            receipts.append(receipt)
            print(
                f"{name}: seconds={seconds:.3f} pcmBytes={pcm_bytes} sha256={sha} "
                f"gain={gain:.4f}",
                flush=True,
            )

    credits = {
        "schemaVersion": 1,
        "kind": "urman.act1_bath_spirit_sounds",
        "generator": "tools/audio/prepare_bath_spirit_sounds.py",
        "subject": "мунча иясе (bath spirit) presentation: continuous breath/steam bed and three cues",
        "listening": "external/not-run",
        "sourceManifest": {
            "path": str(manifest_path),
            "sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        },
        "gainPolicy": "min(0.13/rms, 0.65/peak), 20 ms (30 ms loop) edge fades, 24 kHz mono PCM16",
        "licenseReading": [
            {
                "slot": slot,
                "source": manifest["slots"][slot]["source_page"],
                "author": manifest["slots"][slot]["author"],
                "title": SOURCES[slot],
                "license": manifest["slots"][slot]["license"],
                "licenseUrl": manifest["slots"][slot]["license_url"],
                "readFrom": f"{manifest_path} slots.{slot}",
            }
            for slot in SOURCES
        ],
        "clips": receipts,
    }
    credits_path = write_json(out_dir, "credits.json", credits)
    total_bytes = sum(receipt["pcmBytes"] for receipt in receipts)
    print(f"credits: {credits_path}", flush=True)
    print(f"total PCM bytes: {total_bytes}", flush=True)


if __name__ == "__main__":
    main()
