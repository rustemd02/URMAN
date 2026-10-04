#!/usr/bin/env python3
"""Prepare the six source-backed Act I sound-mood one-shots.

Re-decodes the license-verified downloads kept in ``/tmp/urman_sound_hunt``
(the tool verifies each source sha256 against ``manifest.json`` before any
decode and fails loudly on a mismatch), resamples them to 24 kHz mono PCM16
WAV with ``ffmpeg``, then trims, levels and fades with Python's standard
library only.  Selection scans are deterministic: no RNG is used.

Outputs (``game/assets/audio/act1/sound_mood/``):

* ``adhan.wav`` - the whole Istanbul adhan field recording, leading/trailing
  silence trimmed, nothing else cut.
* ``stove.wav`` - 10 s of the steadier, livelier crackle region.
* ``tv.wav`` - 10 s of the unintelligible TV murmur from a mid section.
* ``laughter.wav`` - 7 s around the liveliest group talk/cheer.
* ``chatter.wav`` - 10 s of room murmur, 2 s clear of both source edges.
* ``dog_distant.wav`` - 6 s of the busiest distant rural barking (panel
  audition one-shot, not a household-pool clip).
* ``credits.json`` - provenance (page URL, author, license, hashes, slice).
* ``shots_manifest.json`` - machine summary of hashes, license readings and
  the ffmpeg decode step.

The four household shots are delivered to the runtime one-shot pool
(``stove``/``tv``/``laughter``/``chatter``), which rejects single clips above
600000 PCM bytes, so each is kept at or below 12.0 s (48000 bytes/s at
24 kHz mono PCM16).  Only these eight files are written; the village beds
(``village_dread_layer.wav``, ``beds_manifest.json``) and every other file
under ``sound_mood/`` are left untouched.
"""

from __future__ import annotations

import argparse
import array
import hashlib
import itertools
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
DEFAULT_SOURCE_ROOT = Path("/tmp/urman_sound_hunt")
DEFAULT_OUT = ROOT / "game/assets/audio/act1/sound_mood"
RATE = 24_000
EDGE_FADE_SAMPLES = 480  # 20 ms edge fades at 24 kHz, as in prepare_village_life.py
GAIN_RMS = 0.13
GAIN_PEAK = 0.65
POOL_MAX_BYTES = 600_000
POOL_MAX_SECONDS = 12.0
POOL_CLIPS = ("stove", "tv", "laughter", "chatter")
ADHAN_SILENCE_AMPLITUDE = 0.005  # ~ -46 dBFS trim threshold on the full-scale signal

# id -> (manifest slot key, output seconds or None for the whole adhan)
CLIPS = (
    ("adhan", "adhan", None),
    ("stove", "stove_fire", 10.0),
    ("tv", "tv_murmur", 10.0),
    ("laughter", "laughter", 7.0),
    ("chatter", "chatter", 10.0),
    ("dog_distant", "dog", 6.0),
)

TV_WINDOW_SECONDS = 60.0
TV_TARGET_SECONDS = 10.0
STOVE_TARGET_SECONDS = 10.0
STOVE_BLOCK_SECONDS = 0.5
LAUGHTER_TARGET_SECONDS = 7.0
CHATTER_TARGET_SECONDS = 10.0
CHATTER_EDGE_MARGIN_SECONDS = 2.0
DOG_DISTANT_TARGET_SECONDS = 6.0


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_ffmpeg(explicit: str | None) -> tuple[str, dict, str, str | None]:
    """Find a working ffmpeg; fall back to Cellar libraries if linkage broke.

    Returns (binary, environment, first version line, dyld fallback path or None).
    Homebrew upgrades can replace a dylib an older ffmpeg links against (seen
    with libx265); retrying with DYLD_FALLBACK_LIBRARY_PATH over the Cellar
    libraries repairs that without touching the repository.
    """
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


def dyld_fallback_for(stderr: str) -> str | None:
    """Library directories that repair dyld "Library not loaded" errors.

    Homebrew upgrades replace versioned dylibs (for example libx265.216.dylib
    after an x265 bump) while an installed ffmpeg still links the old names.
    Each missing absolute path is mapped onto the same file name in an older
    Cellar version; if none can be resolved the whole Cellar tree is offered
    as a last resort.
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


def decode(
    ffmpeg: str,
    environment: dict,
    source: Path,
    destination: Path,
    start_seconds: float | None = None,
    length_seconds: float | None = None,
) -> None:
    command = [ffmpeg, "-y", "-v", "error", "-nostdin"]
    if start_seconds is not None:
        command += ["-ss", f"{start_seconds:.6f}"]  # accurate input seek when transcoding
    command += ["-i", str(source)]
    if length_seconds is not None:
        command += ["-t", f"{length_seconds:.6f}"]
    command += ["-ac", "1", "-ar", str(RATE), "-acodec", "pcm_s16le", "-f", "wav", str(destination)]
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


def squared_prefix(samples: array.array) -> array.array:
    """Cumulative energy prefix sums, exact in int64 for PCM16 input."""
    return array.array("q", itertools.accumulate((value * value for value in samples), initial=0))


def block_rms(prefix: array.array, start: int, count: int) -> float:
    return math.sqrt((prefix[start + count] - prefix[start]) / count)


def best_energy_start(
    prefix: array.array,
    count: int,
    step: int,
    begin: int = 0,
    end: int | None = None,
) -> int:
    """Start of the highest-energy window; ties keep the earliest start."""
    length = len(prefix) - 1
    end = length - count if end is None else min(end, length - count)
    if begin > end:
        raise SystemExit(f"no {count / RATE:.1f} s window fits between samples {begin} and {end}")
    best_start, best_energy = begin, None
    start = begin
    while start <= end:
        energy = prefix[start + count] - prefix[start]
        if best_energy is None or energy > best_energy:
            best_start, best_energy = start, energy
        start += step
    return best_start


def steadiest_start(prefix: array.array, count: int, block: int) -> int:
    """Start of the window whose quietest 0.5 s block is the loudest.

    Start is aligned to the block grid; the second-weakest block RMS is the
    primary score (one lone quiet block is tolerated, a lull is not), the
    window mean breaks ties.
    """
    values = [block_rms(prefix, index * block, block) for index in range((len(prefix) - 1) // block)]
    block_count = count // block
    if len(values) < block_count:
        raise SystemExit("source too short for the steady-window scan")
    best_start, best_score = 0, None
    for index in range(0, len(values) - block_count + 1):
        window = sorted(values[index : index + block_count])
        score = (window[1] if len(window) > 1 else window[0], sum(window) / len(window))
        if best_score is None or score > best_score:
            best_start, best_score = index * block, score
    return best_start


def select_clip(
    name: str,
    samples: array.array,
    decode_start_seconds: float | None,
) -> tuple[array.array, float, str]:
    """Return (clip, source-relative start seconds, selection description)."""
    if name == "adhan":
        threshold = round(ADHAN_SILENCE_AMPLITUDE * 32768)
        first = next((index for index, value in enumerate(samples) if abs(value) >= threshold), None)
        last = next(
            (len(samples) - 1 - index for index, value in enumerate(reversed(samples)) if abs(value) >= threshold),
            None,
        )
        if first is None or last is None or last <= first:
            raise SystemExit("adhan source is silent at the chosen threshold")
        clip = samples[first : last + 1]
        seconds = len(clip) / RATE
        if not 20.0 <= seconds <= 55.0:
            raise SystemExit(f"adhan trim kept {seconds:.3f} s, outside the expected 45-50 s range")
        return clip, first / RATE, (
            "leading/trailing silence (amplitude below -46 dBFS) trimmed; whole call kept"
        )

    prefix = squared_prefix(samples)
    if name == "stove":
        count = round(STOVE_TARGET_SECONDS * RATE)
        start = steadiest_start(prefix, count, round(STOVE_BLOCK_SECONDS * RATE))
        criterion = (
            "highest second-weakest 0.5 s RMS block (ties: louder window), 0.5 s start step"
        )
    elif name == "tv":
        count = round(TV_TARGET_SECONDS * RATE)
        start = best_energy_start(prefix, count, RATE // 4)
        criterion = (
            "highest-RMS 10 s inside a 60 s window centred on the source midpoint "
            "(skip the first 60 s), 0.25 s start step"
        )
    elif name == "laughter":
        count = round(LAUGHTER_TARGET_SECONDS * RATE)
        start = best_energy_start(prefix, count, RATE // 10)
        criterion = "highest-energy 7 s around the liveliest group reaction, 0.1 s start step"
    elif name == "chatter":
        count = round(CHATTER_TARGET_SECONDS * RATE)
        margin = round(CHATTER_EDGE_MARGIN_SECONDS * RATE)
        start = best_energy_start(prefix, count, RATE // 10, begin=margin, end=len(samples) - count - margin)
        criterion = (
            "highest-energy 10 s keeping 2 s clear of both source edges, 0.1 s start step"
        )
    elif name == "dog_distant":
        count = round(DOG_DISTANT_TARGET_SECONDS * RATE)
        start = best_energy_start(prefix, count, RATE // 10)
        criterion = "highest-energy 6 s around the busiest distant barking, 0.1 s start step"
    else:
        raise SystemExit(f"unknown clip: {name}")

    expected = count
    if len(samples) - start < expected:
        raise SystemExit(f"{name}: source too short for {expected / RATE:.1f} s")
    clip = samples[start : start + expected]
    source_start = start / RATE + (decode_start_seconds or 0.0)
    return clip, source_start, criterion


def quantize(samples: array.array) -> tuple[array.array, float, float]:
    """House gain policy: gain = min(0.13/rms, 0.65/peak), then 20 ms fades."""
    values = [value / 32768.0 for value in samples]
    peak = max(abs(value) for value in values)
    rms = math.sqrt(sum(value * value for value in values) / len(values))
    if peak == 0:
        raise SystemExit("silent clip")
    gain = min(GAIN_RMS / max(0.001, rms), GAIN_PEAK / peak)
    fade = min(EDGE_FADE_SAMPLES, len(values) // 4)
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


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT,
                        help="directory holding manifest.json and files/ (default: %(default)s)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT,
                        help="output directory (default: game/assets/audio/act1/sound_mood)")
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
    with tempfile.TemporaryDirectory(prefix="urman-sound-mood-") as temporary:
        temp = Path(temporary)
        for name, slot, target_seconds in CLIPS:
            primary = manifest["slots"][slot]["primary"]
            source = source_root / "files" / Path(primary["local_path"]).name
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

            decode_start = None
            decode_length = None
            if name == "tv":
                decode_start = max(0.0, primary["duration_s"] / 2 - TV_WINDOW_SECONDS / 2)
                decode_length = TV_WINDOW_SECONDS
            decoded = temp / f"{name}.wav"
            decode(ffmpeg, environment, source, decoded, start_seconds=decode_start, length_seconds=decode_length)
            samples = read_wav16(decoded)
            if name != "tv":
                decoded_seconds = len(samples) / RATE
                if abs(decoded_seconds - primary["duration_s"]) > 0.5:
                    raise SystemExit(
                        f"{name}: decoded {decoded_seconds:.3f} s but the manifest says "
                        f"{primary['duration_s']:.3f} s"
                    )

            clip, source_start, criterion = select_clip(name, samples, decode_start)
            if target_seconds is not None and len(clip) != round(target_seconds * RATE):
                raise SystemExit(
                    f"{name}: prepared {len(clip) / RATE:.3f} s but {target_seconds:.3f} s was specified"
                )
            data, gain, fade = quantize(clip)
            seconds = len(data) / RATE
            pcm_bytes = len(data) * 2
            if name in POOL_CLIPS:
                if pcm_bytes > POOL_MAX_BYTES or seconds > POOL_MAX_SECONDS:
                    raise SystemExit(
                        f"{name}: {pcm_bytes} PCM bytes / {seconds:.3f} s exceeds the runtime pool "
                        f"limit ({POOL_MAX_BYTES} bytes / {POOL_MAX_SECONDS} s)"
                    )

            sha = write_wav(out_dir, name, data)
            receipt = {
                "file": f"res://assets/audio/act1/sound_mood/{name}.wav",
                "id": name,
                "seconds": round(seconds, 6),
                "pcmBytes": pcm_bytes,
                "sha256": sha,
                "source": primary["source_page"],
                "directUrl": primary["direct_url"],
                "author": primary["author"],
                "license": primary["license"],
                "licenseUrl": primary["license_url"],
                "sourceSha256": primary["sha256"],
                "startSeconds": round(source_start, 6),
                "endSeconds": round(source_start + seconds, 6),
                "sourceDurationSeconds": primary["duration_s"],
                "attributionRequired": bool(primary["attribution_required"]),
                "preparation": (
                    "ffmpeg decode to 24 kHz mono PCM16; "
                    + criterion
                    + f"; gain min(0.13/rms, 0.65/peak) = {gain:.4f}; "
                    + f"{fade / RATE * 1000:.0f} ms edge fades"
                ),
                "description": primary["description"],
            }
            receipts.append(receipt)
            license_reading.append(
                {
                    "id": name,
                    "license": primary["license"],
                    "licenseUrl": primary["license_url"],
                    "attributionRequired": bool(primary["attribution_required"]),
                    "source": primary["source_page"],
                    "readFrom": f"{manifest_path} slots.{slot}.primary",
                }
            )
            print(
                f"{name}: seconds={seconds:.3f} pcmBytes={pcm_bytes} sha256={sha} "
                f"sourceStart={source_start:.3f}s gain={gain:.4f}",
                flush=True,
            )

    credits = {
        "schemaVersion": 1,
        "clips": receipts,
        "listening": "external/not-run",
    }
    credits_path = write_json(out_dir, "credits.json", credits)

    shots_manifest = {
        "schemaVersion": 1,
        "kind": "urman.act1_sound_mood_shots",
        "generator": "tools/audio/prepare_sound_mood_shots.py",
        "listening": "external/not-run",
        "sourceManifest": {
            "path": str(manifest_path),
            "sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        },
        "decode": {
            "tool": ffmpeg,
            "version": version,
            "command": (
                "ffmpeg -y -v error [-ss <start>] -i <source> [-t <length>] "
                "-ac 1 -ar 24000 -acodec pcm_s16le -f wav <temp.wav>"
            ),
            "note": (
                "ffmpeg decodes and resamples each sha256-verified source into a temp 24 kHz mono "
                "PCM16 WAV (input seek for the TV mid-section window); Python wave/array then slice, "
                "gain, fade and author the final files"
            ),
            "dyldFallbackLibraryPath": fallback,
        },
        "poolGuard": {
            "maxPcmBytes": POOL_MAX_BYTES,
            "maxSeconds": POOL_MAX_SECONDS,
            "clips": list(POOL_CLIPS),
            "note": (
                "these four clips are kept inside the runtime one-shot pool limits; "
                "adhan.wav (whole call) and dog_distant.wav (panel audition one-shot) are not pooled"
            ),
        },
        "licenseReading": license_reading,
        "clips": [
            {
                key: receipt[key]
                for key in (
                    "id",
                    "file",
                    "seconds",
                    "pcmBytes",
                    "sha256",
                    "sourceSha256",
                    "startSeconds",
                    "sourceDurationSeconds",
                )
            }
            for receipt in receipts
        ],
    }
    manifest_out = write_json(out_dir, "shots_manifest.json", shots_manifest)
    print(f"credits: {credits_path}", flush=True)
    print(f"manifest: {manifest_out}", flush=True)
    print(
        "total PCM bytes: " + str(sum(receipt["pcmBytes"] for receipt in receipts)),
        flush=True,
    )


if __name__ == "__main__":
    main()
