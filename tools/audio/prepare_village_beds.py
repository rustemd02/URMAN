#!/usr/bin/env python3
"""Deterministic ordinary-village sound-mood beds for VillageSoundMoodDirector.

Writes ``village_dread_layer.wav`` (weather wind, low dread drone, sparse
alternating owl calls) and, when the optional household one-shots are present,
``village_life_layer.wav`` (weather wind plus faint chatter / TV / stove /
laughter / music / dog texture).  Layer gains are dB relative to that source's
own RMS; the summed mix is normalized once to the bed RMS target with a 0.70
peak ceiling and joined with a 1 s equal-power tail-to-head crossfade so the
engine can loop the whole file.

Only the Python standard library and macOS ``afconvert`` are used.  Sources
already at 24 kHz mono PCM16 are mixed directly after a header check; other
sources are resampled with afconvert and their 16-bit RIFF header is rewritten
by ``generate_act1_footsteps.normalize_pcm_wav``.  Nothing is downloaded, no
synthetic voice substitute is generated, and no file other than the two bed
WAVs and ``beds_manifest.json`` is written or replaced.
"""

from __future__ import annotations

import argparse
import array
import hashlib
import json
import math
import random
import shutil
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

from generate_act1_footsteps import normalize_pcm_wav

ROOT = Path(__file__).resolve().parents[2]
RATE = 24_000
SEED = 20261004
JOIN_SECONDS = 1.0
PEAK_CEILING = 0.70

DREAD_DURATION = 120.0
DREAD_WIND_DB = -9.0
DREAD_DRONE_DB = -20.0
DREAD_OWL_DB = -16.0
DREAD_TARGET_RMS = 0.035
OWL_GAP_SECONDS = (14.0, 40.0)
OWL_FIRST_SECONDS = (10.0, 16.0)
OWL_TAIL_MARGIN = 6.0

LIFE_DURATION = 112.0
LIFE_WIND_DB = -12.0
LIFE_CHATTER_DB = -14.0
LIFE_TV_DB = -16.0
LIFE_STOVE_DB = -15.0
LIFE_LAUGHTER_DB = -17.0
LIFE_MUSIC_DB = -19.0
LIFE_DOG_DB = -18.0
LIFE_TARGET_RMS = 0.03
SHOT_TAIL_MARGIN = 2.0

WIND_SOURCE = ROOT / "game/assets/audio/kara_urman_edge_ambience.wav"
FOREST_SOURCES = ROOT / "game/assets/audio/act1/foley/forest"
VILLAGE_LIFE_SOURCES = ROOT / "game/assets/audio/act1/village_life"
OPTIONAL_SHOTS = ("chatter", "tv", "stove", "laughter")

WIND_LICENSE = (
    "CC0-1.0 (Freesound bruno.auzet/670307 January wind in pine trees; "
    "declared in game/assets/audio/ambient_manifest.json)"
)
FOLEY_LICENSE = (
    "Project-original (procedural forest Foley from "
    "tools/audio/generate_prologue_forest_sounds.py; documented in "
    "docs/urman_knowledge_base/audio/act1_sound_map.md)"
)
MUSIC_LICENSE = (
    "CC-BY-SA-4.0 (Aisa Hakimcan violin excerpt via res://assets/audio/act1/radio; "
    "see game/assets/audio/act1/village_life/credits.json; attribution required)"
)
DOG_LICENSE = (
    "CC0-1.0 (Freesound TeTeNoise/518727; "
    "see game/assets/audio/act1/village_life/credits.json)"
)

LOOP_DESCRIPTION = (
    "whole-file forward loop; the engine (VillageSoundMoodDirector.ConfigureLoop) "
    "sets LoopMode; the file is built with a 1 s equal-power tail-to-head crossfade join"
)

_HASH_CACHE: dict[Path, str] = {}


def sha256_file(path: Path) -> str:
    if path not in _HASH_CACHE:
        _HASH_CACHE[path] = hashlib.sha256(path.read_bytes()).hexdigest()
    return _HASH_CACHE[path]


def relative(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def pcm16_mono(path: Path, rate: int = RATE) -> array.array:
    """Read a verified 24 kHz mono PCM16 WAV as little-endian 16-bit samples."""
    with wave.open(str(path), "rb") as stream:
        header = (stream.getnchannels(), stream.getsampwidth(), stream.getframerate())
        if header != (1, 2, rate):
            raise ValueError(f"not {rate} Hz mono PCM16: {path} has {header}")
        frames = stream.readframes(stream.getnframes())
    samples = array.array("h")
    samples.frombytes(frames)
    if sys.byteorder != "little":
        samples.byteswap()
    return samples


def to_floats(samples: array.array) -> list[float]:
    return [value / 32768.0 for value in samples]


def load_source(path: Path, temporary: Path) -> list[float]:
    """Header-check the source; resample non-24k mono PCM16 with afconvert."""
    if not path.is_file():
        raise FileNotFoundError(f"missing bed source: {path}")
    with wave.open(str(path), "rb") as stream:
        native = (stream.getnchannels(), stream.getsampwidth(), stream.getframerate())
    if native == (1, 2, RATE):
        return to_floats(pcm16_mono(path))
    if shutil.which("afconvert") is None:
        raise SystemExit(f"afconvert is required to resample {path}")
    stage = temporary / f"{path.stem}.stage.wav"
    decoded = temporary / f"{path.stem}.pcm.wav"
    subprocess.run(
        ["afconvert", "-f", "WAVE", "-d", f"LEI16@{RATE}", "-c", "1", str(path), str(stage)],
        check=True,
    )
    normalize_pcm_wav(stage, decoded, sample_rate=RATE)
    return to_floats(pcm16_mono(decoded))


def rms(values: list[float]) -> float:
    return math.sqrt(sum(value * value for value in values) / len(values))


def tile_seamless(values: list[float], total: int, fade: int) -> list[float]:
    """Repeat a source with equal-power crossfades so the join is continuous."""
    length = len(values)
    if fade < 2 or length <= 2 * fade:
        raise ValueError("source too short for seamless tiling")
    step = length - fade
    out = [0.0] * total
    position, first = 0, True
    while position < total:
        follows = position + step < total
        limit = min(length, total - position)
        for index in range(limit):
            envelope = 1.0
            if not first and index < fade:
                envelope *= math.sin(index / (fade - 1) * math.pi / 2)
            if follows and index >= length - fade:
                envelope *= math.cos((index - (length - fade)) / (fade - 1) * math.pi / 2)
            out[position + index] += values[index] * envelope
        position += step
        first = False
    return out


def add_layer(
    mix: list[float],
    values: list[float],
    start_sample: int,
    gain_db: float,
    micro_fade: int = 0,
) -> None:
    """Add a source at ``gain_db`` relative to its own RMS onto the timeline."""
    gain = 10.0 ** (gain_db / 20.0) / rms(values)
    length = len(values)
    if start_sample < 0 or start_sample + length > len(mix):
        raise ValueError("source does not fit inside the bed")
    for index in range(length):
        envelope = 1.0
        if micro_fade and micro_fade < length // 4:
            envelope = min(1.0, index / micro_fade, (length - 1 - index) / micro_fade)
        mix[start_sample + index] += values[index] * gain * envelope


def loop_join(values: list[float], fade: int) -> list[float]:
    """Drop the tail into the head with a 1 s equal-power crossfade."""
    length = len(values)
    if fade < 2 or length <= 2 * fade:
        raise ValueError("bed too short for the loop join")
    out = values[fade : length - fade]
    tail = values[length - fade :]
    for index in range(fade):
        angle = index / (fade - 1) * math.pi / 2
        out.append(tail[index] * math.cos(angle) + values[index] * math.sin(angle))
    return out


def quantize_and_metrics(
    values: list[float], target_rms: float, ceiling: float
) -> tuple[array.array, dict]:
    level = rms(values)
    peak = max(abs(value) for value in values)
    if not 0 < target_rms < ceiling < 1 or level == 0 or peak == 0:
        raise ValueError("invalid bed level or silent mix")
    gain = min(target_rms / level, ceiling / peak)
    data = array.array("h", (round(value * gain * 32767.0) for value in values))
    quantized = [value / 32768.0 for value in data]
    metrics = {
        "seconds": len(data) / RATE,
        "pcmBytes": len(data) * 2,
        "rms": rms(quantized),
        "peak": max(abs(value) for value in quantized),
        "loopBoundaryDelta": abs(quantized[-1] - quantized[0]),
    }
    return data, metrics


def write_wav(out_dir: Path, name: str, data: array.array) -> str:
    out_dir.mkdir(parents=True, exist_ok=True)
    destination = out_dir / name
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


def source_entry(
    path: Path,
    role: str,
    offset: float,
    gain_db: float,
    license_text: str,
    note: str | None = None,
) -> dict:
    entry = {
        "path": relative(path),
        "role": role,
        "offsetSeconds": round(offset, 3),
        "gainDb": gain_db,
        "license": license_text,
        "sourceSha256": sha256_file(path),
    }
    if note:
        entry["note"] = note
    return entry


def shot_license(shots_dir: Path, stem: str) -> str:
    """Copy a license string from the shots credits file when it declares one."""
    credits = shots_dir / "credits.json"
    if credits.is_file():
        try:
            data = json.loads(credits.read_text())
        except (OSError, ValueError):
            data = None
        entries = data.get("clips", []) if isinstance(data, dict) else []
        for entry in entries:
            file_name = Path(str(entry.get("file", ""))).name
            if entry.get("id") == stem or file_name == f"{stem}.wav":
                license_text = entry.get("license")
                source = entry.get("source") or entry.get("author")
                if license_text:
                    return f"{license_text} ({source})" if source else str(license_text)
    return "Unverified in-repo - record source and license before release"


def best_window(values: list[float], count: int, begin: int, end: int) -> list[float]:
    count = min(count, len(values))
    begin = max(0, min(begin, len(values) - count))
    end = max(begin, min(end, len(values) - count))
    step = max(1, RATE // 4)
    best_start, best_energy = begin, None
    start = begin
    while start <= end:
        energy = sum(value * value for value in values[start : start + count])
        if best_energy is None or energy > best_energy:
            best_energy, best_start = energy, start
        start += step
    return values[best_start : best_start + count]


def build_dread(
    wind: list[float],
    drone: list[float],
    owl_clips: tuple[list[float], list[float]],
    owl_paths: tuple[Path, Path],
    rng: random.Random,
) -> tuple[array.array, dict, list[dict], list[float]]:
    total = round((DREAD_DURATION + JOIN_SECONDS) * RATE)
    fade = round(JOIN_SECONDS * RATE)
    mix = [0.0] * total
    sources = [
        source_entry(
            WIND_SOURCE,
            "weather base (January wind in pine trees)",
            0.0,
            DREAD_WIND_DB,
            WIND_LICENSE,
            "tiled seamlessly across the bed with 1 s equal-power crossfades",
        ),
        source_entry(
            FOREST_SOURCES / "dread_drone.wav",
            "low dread drone",
            0.0,
            DREAD_DRONE_DB,
            FOLEY_LICENSE,
            "tiled seamlessly across the bed with 1 s equal-power crossfades",
        ),
    ]
    for values, gain_db in ((wind, DREAD_WIND_DB), (drone, DREAD_DRONE_DB)):
        tiled = tile_seamless(values, total, fade)
        add_layer(mix, tiled, 0, gain_db)
        del tiled

    lengths = [len(clip) / RATE for clip in owl_clips]
    requested = rng.randint(4, 6)
    starts: list[float] | None = None
    for count in range(requested, 3, -1):
        for _ in range(128):
            candidate = [rng.uniform(*OWL_FIRST_SECONDS)]
            for _ in range(count - 1):
                candidate.append(candidate[-1] + rng.uniform(*OWL_GAP_SECONDS))
            if all(
                candidate[index] + lengths[index % 2] <= DREAD_DURATION - OWL_TAIL_MARGIN
                for index in range(count)
            ):
                starts = candidate
                break
        if starts is not None:
            break
    if starts is None:
        raise RuntimeError("could not place 4 owl calls inside the dread bed")

    micro = round(0.010 * RATE)
    for index, start in enumerate(starts):
        add_layer(
            mix,
            owl_clips[index % 2],
            round((start + JOIN_SECONDS) * RATE),
            DREAD_OWL_DB,
            micro_fade=micro,
        )
        sources.append(
            source_entry(
                owl_paths[index % 2],
                "sparse owl call",
                start,
                DREAD_OWL_DB,
                FOLEY_LICENSE,
                "alternating owl_tawny / owl_eagle",
            )
        )
    joined = loop_join(mix, fade)
    del mix
    data, metrics = quantize_and_metrics(joined, DREAD_TARGET_RMS, PEAK_CEILING)
    return data, metrics, sources, starts


def build_life(
    wind: list[float],
    shots: dict[str, tuple[Path, list[float]]],
    music: list[float],
    dog: list[float],
    shots_dir: Path,
    rng: random.Random,
) -> tuple[array.array, dict, list[dict], dict[str, str]]:
    total = round((LIFE_DURATION + JOIN_SECONDS) * RATE)
    fade = round(JOIN_SECONDS * RATE)
    micro = round(0.010 * RATE)
    mix = [0.0] * total
    licenses = {name: shot_license(shots_dir, name) for name in shots}
    sources = [
        source_entry(
            WIND_SOURCE,
            "weather base (January wind in pine trees)",
            0.0,
            LIFE_WIND_DB,
            WIND_LICENSE,
            "tiled seamlessly across the bed with 1 s equal-power crossfades",
        )
    ]
    tiled = tile_seamless(wind, total, fade)
    add_layer(mix, tiled, 0, LIFE_WIND_DB)
    del tiled

    def clamp_start(start: float, clip: list[float]) -> float:
        return min(max(start, 3.0), LIFE_DURATION - SHOT_TAIL_MARGIN - len(clip) / RATE)

    if "chatter" in shots:
        path, clip = shots["chatter"]
        count = rng.randint(2, 3)
        anchors = (14.0, 78.0) if count == 2 else (10.0, 52.0, 95.0)
        for anchor in anchors:
            start = clamp_start(anchor + rng.uniform(-5.0, 5.0), clip)
            add_layer(
                mix,
                clip,
                round((start + JOIN_SECONDS) * RATE),
                LIFE_CHATTER_DB,
                micro_fade=micro,
            )
            sources.append(
                source_entry(
                    path,
                    "distant household talk",
                    start,
                    LIFE_CHATTER_DB,
                    licenses["chatter"],
                    "2-3 tiles with irregular gaps",
                )
            )

    if "tv" in shots:
        path, clip = shots["tv"]
        half = len(clip) // 2
        count_samples = round(min(6.0, len(clip) / RATE) * RATE)
        for anchor, (begin, end) in ((30.0, (0, half)), (88.0, (half, len(clip)))):
            segment = best_window(clip, count_samples, begin, end)
            start = clamp_start(anchor + rng.uniform(-4.0, 4.0), segment)
            add_layer(
                mix,
                segment,
                round((start + JOIN_SECONDS) * RATE),
                LIFE_TV_DB,
                micro_fade=micro,
            )
            sources.append(
                source_entry(
                    path,
                    "TV murmur segment",
                    start,
                    LIFE_TV_DB,
                    licenses["tv"],
                    "two short quiet segments",
                )
            )

    if music:
        count = rng.randint(1, 2)
        half = len(music) // 2
        count_samples = round(min(6.0, len(music) / RATE) * RATE)
        windows = ((0, half),) if count == 1 else ((0, half), (half, len(music)))
        anchors = (58.0,) if count == 1 else (24.0, 92.0)
        for anchor, (begin, end) in zip(anchors, windows):
            segment = best_window(music, count_samples, begin, end)
            start = clamp_start(anchor + rng.uniform(-4.0, 4.0), segment)
            add_layer(
                mix,
                segment,
                round((start + JOIN_SECONDS) * RATE),
                LIFE_MUSIC_DB,
                micro_fade=micro,
            )
            sources.append(
                source_entry(
                    VILLAGE_LIFE_SOURCES / "music.wav",
                    "faint violin bed music",
                    start,
                    LIFE_MUSIC_DB,
                    MUSIC_LICENSE,
                    "one or two faint segments",
                )
            )

    if dog:
        count_samples = round(min(2.0, len(dog) / RATE) * RATE)
        bark = best_window(dog, count_samples, 0, len(dog))
        for anchor in (20.0, 96.0):
            start = clamp_start(anchor + rng.uniform(-4.0, 4.0), bark)
            add_layer(
                mix,
                bark,
                round((start + JOIN_SECONDS) * RATE),
                LIFE_DOG_DB,
                micro_fade=micro,
            )
            sources.append(
                source_entry(
                    VILLAGE_LIFE_SOURCES / "dog.wav",
                    "distant dog bark",
                    start,
                    LIFE_DOG_DB,
                    DOG_LICENSE,
                    "two distant barks",
                )
            )

    if "stove" in shots:
        path, clip = shots["stove"]
        if len(clip) / RATE > 8.0:
            clip = best_window(clip, round(8.0 * RATE), 0, len(clip))
        start = clamp_start(LIFE_DURATION / 2 + rng.uniform(-3.0, 3.0), clip)
        add_layer(
            mix,
            clip,
            round((start + JOIN_SECONDS) * RATE),
            LIFE_STOVE_DB,
            micro_fade=micro,
        )
        sources.append(
            source_entry(
                path,
                "stove crackle",
                start,
                LIFE_STOVE_DB,
                licenses["stove"],
                "one crackle near the middle",
            )
        )

    if "laughter" in shots:
        path, clip = shots["laughter"]
        start = clamp_start(rng.uniform(30.0, 84.0), clip)
        add_layer(
            mix,
            clip,
            round((start + JOIN_SECONDS) * RATE),
            LIFE_LAUGHTER_DB,
            micro_fade=micro,
        )
        sources.append(
            source_entry(
                path,
                "faint laughter",
                start,
                LIFE_LAUGHTER_DB,
                licenses["laughter"],
                "one quiet burst",
            )
        )

    joined = loop_join(mix, fade)
    del mix
    data, metrics = quantize_and_metrics(joined, LIFE_TARGET_RMS, PEAK_CEILING)
    return data, metrics, sources, licenses


def bed_entry(
    bed_id: str,
    file_path: str,
    sha256: str,
    metrics: dict,
    preparation: str,
    sources: list[dict],
    notes: list[str] | None = None,
) -> dict:
    entry = {
        "id": bed_id,
        "file": file_path,
        "seconds": metrics["seconds"],
        "pcmBytes": metrics["pcmBytes"],
        "sha256": sha256,
        "seed": SEED,
        "rms": metrics["rms"],
        "peak": metrics["peak"],
        "loopBoundaryDelta": metrics["loopBoundaryDelta"],
        "loop": LOOP_DESCRIPTION,
        "preparation": preparation,
        "sources": sources,
    }
    if notes:
        entry["notes"] = notes
    return entry


def write_manifest(out_dir: Path, manifest: dict) -> Path:
    out_dir.mkdir(parents=True, exist_ok=True)
    destination = out_dir / "beds_manifest.json"
    staged = destination.with_name(destination.name + ".tmp")
    staged.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    staged.replace(destination)
    return destination


def report(name: str, metrics: dict, sha256: str) -> None:
    print(
        f"{name}: seconds={metrics['seconds']:.3f} pcmBytes={metrics['pcmBytes']} "
        f"sha256={sha256} rms={metrics['rms']:.6f} peak={metrics['peak']:.6f} "
        f"loopBoundaryDelta={metrics['loopBoundaryDelta']:.8f}",
        flush=True,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--shots-dir",
        type=Path,
        default=Path("game/assets/audio/act1/sound_mood"),
        help="directory with the optional chatter/tv/stove/laughter one-shots",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=Path("game/assets/audio/act1/sound_mood"),
        help="directory for the bed WAVs and beds_manifest.json",
    )
    args = parser.parse_args()
    shots_dir = args.shots_dir if args.shots_dir.is_absolute() else ROOT / args.shots_dir
    out_dir = args.out if args.out.is_absolute() else ROOT / args.out

    dread_rng = random.Random(SEED)
    life_rng = random.Random(SEED)
    beds: list[dict] = []
    skipped: list[dict] = []

    with tempfile.TemporaryDirectory(prefix="urman-village-beds-") as temporary:
        temp = Path(temporary)
        wind = load_source(WIND_SOURCE, temp)
        drone = load_source(FOREST_SOURCES / "dread_drone.wav", temp)
        owl_clips = (
            load_source(FOREST_SOURCES / "owl_tawny.wav", temp),
            load_source(FOREST_SOURCES / "owl_eagle.wav", temp),
        )
        owl_paths = (FOREST_SOURCES / "owl_tawny.wav", FOREST_SOURCES / "owl_eagle.wav")

        data, metrics, sources, owl_starts = build_dread(
            wind, drone, owl_clips, owl_paths, dread_rng
        )
        sha256 = write_wav(out_dir, "village_dread_layer.wav", data)
        report("village_dread_layer.wav", metrics, sha256)
        print(
            "dread owls: "
            + str(len(owl_starts))
            + " calls (owl_tawny/owl_eagle alternating) at "
            + ", ".join(f"{start:.2f} s" for start in owl_starts),
            flush=True,
        )
        beds.append(
            bed_entry(
                "village_dread_layer",
                "res://assets/audio/act1/sound_mood/village_dread_layer.wav",
                sha256,
                metrics,
                "24 kHz mono PCM16; weather wind -9 dB, dread drone -20 dB, 4-6 "
                "alternating owl calls -16 dB (first no earlier than 10 s, none in "
                "the last 6 s); each gain is dB relative to that source's own RMS; "
                "summed mix normalized once to RMS 0.035 with a 0.70 peak ceiling, "
                "then a 1 s equal-power loop join",
                sources,
                ["no fox/wolf/moan: those remain story cues, not bed texture"],
            )
        )

        optional = {name: shots_dir / f"{name}.wav" for name in OPTIONAL_SHOTS}
        missing = [name for name in OPTIONAL_SHOTS if not optional[name].is_file()]
        present = {name: path for name, path in optional.items() if path.is_file()}
        if not present:
            reason = (
                "optional one-shots absent in "
                + relative(shots_dir)
                + ": "
                + ", ".join(f"{name}.wav" for name in missing)
                + "; no synthetic voice substitute per design"
            )
            skipped.append(
                {
                    "id": "village_life_layer",
                    "file": "res://assets/audio/act1/sound_mood/village_life_layer.wav",
                    "reason": reason,
                }
            )
            print(f"village_life_layer.wav: SKIPPED - {reason}", flush=True)
        else:
            shots = {name: (path, load_source(path, temp)) for name, path in present.items()}
            music = (
                load_source(VILLAGE_LIFE_SOURCES / "music.wav", temp)
                if (VILLAGE_LIFE_SOURCES / "music.wav").is_file()
                else []
            )
            dog = (
                load_source(VILLAGE_LIFE_SOURCES / "dog.wav", temp)
                if (VILLAGE_LIFE_SOURCES / "dog.wav").is_file()
                else []
            )
            data, metrics, sources, _ = build_life(
                wind, shots, music, dog, shots_dir, life_rng
            )
            sha256 = write_wav(out_dir, "village_life_layer.wav", data)
            report("village_life_layer.wav", metrics, sha256)
            notes = [f"optional shot absent, not mixed: {name}.wav" for name in missing]
            beds.append(
                bed_entry(
                    "village_life_layer",
                    "res://assets/audio/act1/sound_mood/village_life_layer.wav",
                    sha256,
                    metrics,
                    "24 kHz mono PCM16; weather wind -12 dB; household one-shots "
                    "at faint bed levels (chatter -14 dB x2-3 irregular tiles, TV "
                    "murmur -16 dB x2, stove crackle -15 dB near the middle, "
                    "laughter -17 dB, violin bed music -19 dB x1-2, distant dog "
                    "barks -18 dB x2); each gain is dB relative to that source's own "
                    "RMS; summed mix normalized once to RMS 0.03 with a 0.70 peak "
                    "ceiling, then a 1 s equal-power loop join",
                    sources,
                    notes or None,
                )
            )

    manifest = {
        "schemaVersion": 1,
        "kind": "urman.village_sound_beds",
        "generator": "tools/audio/prepare_village_beds.py",
        "seed": SEED,
        "listening": "external/not-run",
        "beds": beds,
    }
    if skipped:
        manifest["skipped"] = skipped
    destination = write_manifest(out_dir, manifest)
    print(f"manifest: {relative(destination)}", flush=True)


if __name__ == "__main__":
    main()
