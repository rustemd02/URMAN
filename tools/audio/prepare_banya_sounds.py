#!/usr/bin/env python3
"""Prepare the old-banya interior bed and water cues from CC0 recordings.

Re-decodes the license-verified Freesound HQ previews kept in
``/tmp/urman_banya_sources`` (the tool verifies every source sha256 and size
against ``manifest.json`` before decoding and fails loudly on a mismatch) and
the already-prepared bank clip
``game/assets/audio/act1/sound_mood/stove.wav`` (verified against that
folder's ``credits.json``: same D.jones 525253 CC0 fire, reused as the faint
crackle layer).  Everything is resampled to 24 kHz mono PCM16 with ``ffmpeg``,
then sliced, mixed, seam-faded and levelled with Python's standard library
only.  No RNG is used and every cut is a fixed constant, so the outputs are
reproducible byte for byte.

Sources (all Freesound, CC0 1.0; the licence and link were read from the
served sound pages, see the source manifest and the generated credits.json):

* JustineOu - "steam.WAV", sound 672248.  Steady hiss section 26.0-31.5 s.
* Rudmer_Rotteveel - "Wood Creak Single V10", sound 506665.
* Squidems - "Water drops drip", sound 709949 (single drops with ~0.5 s gaps).
* AardsReal - "Pouring Water Short Free", sound 842163 (audible 0.19-0.87 s).
* D.jones - "04 Wood Fire, Inside Stove", sound 525253 (through the bank clip).

Outputs (``game/assets/audio/act1/banya/``):

* ``banya_room_loop.wav`` - 5.0 s interior bed: steady steam hiss + the bank
  fire crackle + two dry wood creaks; crossfade-seam loop.
* ``drip_loop.wav`` - 4.45 s loop of quiet water drips; both ends sit in the
  source's own silence.
* ``water_scoop.wav`` - 0.77 s short water pour for the "Зачерпнуть воды"
  look-target.
* ``steam_burst.wav`` - 1.6 s hiss puff when the steam-room door opens or the
  player walks into the steam room.
* ``credits.json`` - provenance (page URLs, authors, licences, hashes, slices).

The same house gain policy as ``prepare_sound_mood_shots.py`` and
``prepare_bath_spirit_sounds.py`` is used: gain = min(0.13/rms, 0.65/peak),
then 20 ms edge fades (30 ms for the crossfade-loop bed).  The four files
together stay under 0.6 MB of PCM.
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
DEFAULT_SOURCE_ROOT = Path("/tmp/urman_banya_sources")
DEFAULT_OUT = ROOT / "game/assets/audio/act1/banya"
BANK_CREDITS = ROOT / "game/assets/audio/act1/sound_mood/credits.json"
RATE = 24_000
EDGE_FADE_SAMPLES = 480  # 20 ms at 24 kHz, as in the other prepare_* tools
LOOP_FADE_SAMPLES = 720  # 30 ms
GAIN_RMS = 0.13
GAIN_PEAK = 0.65

# Fixed, deterministic cuts (source-relative seconds).
HISS_START = 26.000
HISS_END = 31.500
CREAK_START = 0.020
CREAK_END = 0.620
DRIP_START = 2.550
DRIP_END = 7.000
POUR_START = 0.150
POUR_END = 0.920
BURST_START = 26.000
BURST_END = 27.600
BANK_START = 2.000
BANK_END = 7.500

BED_SECONDS = 5.0
BED_SEAM_SECONDS = 0.5
HISS_TARGET_RMS = 0.040
BANK_TARGET_RMS = 0.028
DRIP_TARGET_RMS = 0.080
CREAK_AT_SECONDS = (1.400, 3.600)
CREAK_TARGET_RMS = 0.050
CREAK_GAIN = (0.70, 0.50)
BURST_ATTACK_SECONDS = 0.05
BURST_DECAY = 2.2

SOURCES = {
    "steam_hiss": "steam.WAV",
    "wood_creak": "Wood Creak Single V10",
    "drip": "Water drops drip",
    "pour": "Pouring Water Short Free",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dyld_fallback_for(stderr: str) -> str | None:
    """Library directories that repair dyld "Library not loaded" errors.

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


def place(buffer: array.array, piece: array.array, at_seconds: float, gain: float) -> None:
    """Add a quieter copy of ``piece`` into ``buffer`` at a fixed offset."""
    start = round(at_seconds * RATE)
    if start < 0 or start + len(piece) > len(buffer):
        raise SystemExit(f"placement at {at_seconds:.3f} s does not fit the output buffer")
    for index, value in enumerate(piece):
        merged = buffer[start + index] + round(value * gain)
        buffer[start + index] = max(-32768, min(32767, merged))


def seam_loop(samples: array.array, seam_samples: int) -> array.array:
    """Crossfade the tail of ``samples`` onto its head for a seamless loop."""
    seam = min(seam_samples, len(samples) // 4)
    length = len(samples) - seam
    output = array.array("h", samples[:length])
    for index in range(seam):
        head = output[index] * (index / seam)
        tail = samples[length + index] * (1 - index / seam)
        output[index] = max(-32768, min(32767, round(head + tail)))
    return output


def burst_envelope(samples: array.array, attack_seconds: float, decay: float) -> array.array:
    """Fast attack and exponential decay for a one-shot hiss puff."""
    attack = max(1, round(attack_seconds * RATE))
    count = len(samples)
    output = array.array("h", [0]) * count
    for index, value in enumerate(samples):
        grow = min(1.0, index / attack)
        fall = math.exp(-decay * index / count)
        output[index] = max(-32768, min(32767, round(value * grow * fall)))
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


def read_bank_crackle() -> tuple[array.array, dict]:
    """Load the already-prepared bank fire and verify it against its credits."""
    credits = json.loads(BANK_CREDITS.read_text())
    record = next((clip for clip in credits["clips"] if clip["id"] == "stove"), None)
    if record is None:
        raise SystemExit(f"{BANK_CREDITS} has no 'stove' clip record")
    # res:// paths are relative to the Godot project root (game/), not the repo root.
    path = ROOT / "game" / record["file"].removeprefix("res://")
    if not path.is_file():
        raise SystemExit(f"missing bank clip: {path}")
    actual = sha256_file(path)
    if actual != record["sha256"]:
        raise SystemExit(
            "bank stove.wav does not match its credits.json record; refusing to reuse:\n"
            f"  file:     {path}\n  credits:  {record['sha256']}\n  actual:   {actual}"
        )
    return read_wav16(path), record


def prepare(decoded: dict[str, array.array], bank: array.array) -> list[tuple[str, array.array, dict]]:
    """Return (output name, samples, receipt extras) in a fixed order."""
    # Interior bed: steady steam hiss under the bank crackle, two dry creaks.
    hiss = seam_loop(
        scale_to_rms(slice_seconds(decoded["steam_hiss"], HISS_START, HISS_END), HISS_TARGET_RMS),
        round(BED_SEAM_SECONDS * RATE),
    )
    crackle = seam_loop(
        scale_to_rms(slice_seconds(bank, BANK_START, BANK_END), BANK_TARGET_RMS),
        round(BED_SEAM_SECONDS * RATE),
    )
    bed = mix(hiss, crackle)
    creak = scale_to_rms(slice_seconds(decoded["wood_creak"], CREAK_START, CREAK_END), CREAK_TARGET_RMS)
    for at_seconds, gain in zip(CREAK_AT_SECONDS, CREAK_GAIN, strict=True):
        place(bed, creak, at_seconds, gain)

    droplet = slice_seconds(decoded["drip"], DRIP_START, DRIP_END)
    drip_loop = scale_to_rms(droplet, DRIP_TARGET_RMS)

    scoop = slice_seconds(decoded["pour"], POUR_START, POUR_END)
    burst = burst_envelope(
        slice_seconds(decoded["steam_hiss"], BURST_START, BURST_END),
        BURST_ATTACK_SECONDS,
        BURST_DECAY,
    )
    return [
        (
            "banya_room_loop",
            bed,
            {
                "role": "interior-bed",
                "sourceSlots": ["steam_hiss", "wood_creak", "bank_stove"],
                "slices": {
                    "steam": f"{HISS_START:.3f}-{HISS_END:.3f} s",
                    "creaks": [
                        f"{CREAK_START:.3f}-{CREAK_END:.3f} s at {at:.3f} s x{gain:.2f}"
                        for at, gain in zip(CREAK_AT_SECONDS, CREAK_GAIN, strict=True)
                    ],
                    "crackle": f"bank clip {BANK_START:.3f}-{BANK_END:.3f} s",
                },
                "preparation": (
                    "steam slice scaled to RMS 0.040 and crossfade-seam looped to 5.0 s; "
                    "bank crackle scaled to RMS 0.028 and seam looped; both summed; "
                    "creaks scaled to RMS 0.050 and placed at 1.40 s (x0.70) and 3.60 s (x0.50)"
                ),
            },
        ),
        (
            "drip_loop",
            drip_loop,
            {
                "role": "interior-drip-loop",
                "sourceSlots": ["drip"],
                "slices": {"drip": f"{DRIP_START:.3f}-{DRIP_END:.3f} s"},
                "preparation": (
                    "drops slice scaled to RMS 0.080; both loop ends sit in the source's "
                    "own silence; 30 ms loop edge fades"
                ),
            },
        ),
        (
            "water_scoop",
            scoop,
            {
                "role": "interaction-cue",
                "sourceSlots": ["pour"],
                "slices": {"pour": f"{POUR_START:.3f}-{POUR_END:.3f} s"},
                "preparation": "short pour/scoop slice; ",
            },
        ),
        (
            "steam_burst",
            burst,
            {
                "role": "door-and-entry-cue",
                "sourceSlots": ["steam_hiss"],
                "slices": {"steam": f"{BURST_START:.3f}-{BURST_END:.3f} s"},
                "preparation": (
                    f"steady hiss slice with a {BURST_ATTACK_SECONDS * 1000:.0f} ms attack and "
                    f"exp(-{BURST_DECAY:.1f}) decay envelope"
                ),
            },
        ),
    ]


def main(argv: list[str] | None = None) -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT,
                        help="directory holding manifest.json and files/ (default: %(default)s)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT,
                        help="output directory (default: game/assets/audio/act1/banya)")
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
    with tempfile.TemporaryDirectory(prefix="urman-banya-") as temporary:
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

        bank, bank_record = read_bank_crackle()
        expected_seconds = round(bank_record["seconds"])
        if abs(len(bank) / RATE - expected_seconds) > 0.05:
            raise SystemExit(
                f"bank stove.wav decoded {len(bank) / RATE:.3f} s but credits say "
                f"{bank_record['seconds']:.3f} s"
            )

        for name, samples, extras in prepare(decoded, bank):
            loop = name in ("banya_room_loop", "drip_loop")
            data, gain, fade = quantize(samples, LOOP_FADE_SAMPLES if loop else EDGE_FADE_SAMPLES)
            seconds = len(data) / RATE
            pcm_bytes = len(data) * 2
            sha = write_wav(out_dir, name, data)
            offsets = []
            for slot in extras["sourceSlots"]:
                if slot == "bank_stove":
                    offsets.append({
                        "slot": slot,
                        "sourcePage": bank_record["source"],
                        "author": bank_record["author"] if "author" in bank_record else "D.jones",
                        "title": "04 Wood Fire, Inside Stove (already-prepared bank clip)",
                        "license": bank_record["license"],
                        "licenseUrl": bank_record["licenseUrl"],
                        "attributionRequired": bool(bank_record["attributionRequired"]),
                        "sourceSha256": bank_record["sha256"],
                        "bankFile": bank_record["file"],
                        "creditsSource": str(BANK_CREDITS.relative_to(ROOT)),
                    })
                    continue
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
                "file": f"res://assets/audio/act1/banya/{name}.wav",
                "id": name,
                "role": extras["role"],
                "seconds": round(seconds, 6),
                "pcmBytes": pcm_bytes,
                "sha256": sha,
                "loop": loop,
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
        "kind": "urman.act1_banya_sounds",
        "generator": "tools/audio/prepare_banya_sounds.py",
        "subject": "old-banya interior atmosphere: room bed, drip loop, water scoop and steam burst",
        "listening": "external/not-run",
        "sourceManifest": {
            "path": str(manifest_path),
            "sha256": hashlib.sha256(manifest_bytes).hexdigest(),
        },
        "bankSource": {
            "path": str(BANK_CREDITS.relative_to(ROOT)),
            "clip": "stove",
            "note": "the already-prepared D.jones 525253 CC0 fire clip is the bed's crackle layer",
        },
        "gainPolicy": "min(0.13/rms, 0.65/peak), 20 ms (30 ms loop) edge fades, 24 kHz mono PCM16",
        "runtime": {
            "voiceBudget": 3,
            "voices": ["banya_room_loop (loop)", "drip_loop (loop)", "water_scoop / steam_burst / bank stove_kindling (one-shots)"],
        },
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
        ] + [
            {
                "slot": "bank_stove",
                "source": bank_record["source"],
                "author": "D.jones",
                "title": "04 Wood Fire, Inside Stove",
                "license": bank_record["license"],
                "licenseUrl": bank_record["licenseUrl"],
                "readFrom": str(BANK_CREDITS.relative_to(ROOT)),
            }
        ],
        "clips": receipts,
    }
    credits_path = write_json(out_dir, "credits.json", credits)
    total_bytes = sum(receipt["pcmBytes"] for receipt in receipts)
    print(f"credits: {credits_path}", flush=True)
    print(f"total PCM bytes: {total_bytes}", flush=True)


if __name__ == "__main__":
    main()
