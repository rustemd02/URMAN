#!/usr/bin/env python3
"""Convert the curated CC0 Act I footstep sources.

The winter runtime uses four source-backed families: packed snow, soft snow,
house wood and the FAP interior floor.  The checked-in wet-road, mud and grass
WAVs are retained as inactive historical assets and are intentionally not
regenerated here.  Conversion uses macOS's built-in ``afconvert``; no Python
packages or machine-specific source paths are required.
"""

from __future__ import annotations

import shutil
import struct
import subprocess
import tempfile
import wave
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "assets/source/audio/act1/footsteps"
OUT = ROOT / "game/assets/audio/act1/footsteps"

SOURCES = {
    **{
        f"step_snow_packed_{variant:02d}.wav":
        Path("corsica_snow")
        / f"Corsica_S-Walking_on_snow_covered_gravel_and_ice_{variant + 1:02d}.flac"
        for variant in range(3)
    },
    **{
        f"step_snow_soft_{variant:02d}.wav": Path("kenney") / f"footstep_snow_{variant:03d}.ogg"
        for variant in range(3)
    },
    **{
        f"step_wood_{variant:02d}.wav": Path("kenney") / f"footstep_wood_{variant:03d}.ogg"
        for variant in range(3)
    },
    **{
        f"step_interior_floor_{variant:02d}.wav": Path("kenney") / f"footstep_concrete_{variant:03d}.ogg"
        for variant in range(3)
    },
}


def convert(source: Path, target: Path) -> None:
    if not source.is_file():
        raise FileNotFoundError(f"missing footstep source: {source}")

    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="urman-footsteps-", dir=target.parent) as temporary:
        staged = Path(temporary) / target.name
        subprocess.run(
            [
                "afconvert",
                "-f",
                "WAVE",
                "-d",
                "LEI16@44100",
                "-c",
                "1",
                str(source),
                str(staged),
            ],
            check=True,
        )
        normalized = Path(temporary) / f"normalized-{target.name}"
        normalize_pcm_wav(staged, normalized)
        normalized.replace(target)


def normalize_pcm_wav(source: Path, target: Path, sample_rate: int = 44100) -> None:
    """Write afconvert's PCM payload with the standard 16-bit RIFF header."""
    raw = source.read_bytes()
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError(f"afconvert did not produce a RIFF/WAVE file: {source}")

    fmt: bytes | None = None
    data: bytes | None = None
    cursor = 12
    while cursor + 8 <= len(raw):
        chunk_id = raw[cursor : cursor + 4]
        chunk_size = struct.unpack_from("<I", raw, cursor + 4)[0]
        start = cursor + 8
        end = start + chunk_size
        if end > len(raw):
            raise ValueError(f"truncated RIFF chunk in {source}")
        if chunk_id == b"fmt " and fmt is None:
            fmt = raw[start:end]
        elif chunk_id == b"data" and data is None:
            data = raw[start:end]
        cursor = end + (chunk_size & 1)

    if fmt is None or data is None or len(fmt) < 16:
        raise ValueError(f"afconvert output has no usable PCM chunks: {source}")

    audio_format, channels, actual_rate, _, block_align, bits = struct.unpack_from(
        "<HHIIHH", fmt, 0
    )
    if audio_format == 0xFFFE:
        if len(fmt) < 40 or struct.unpack_from("<H", fmt, 24)[0] != 1:
            raise ValueError(f"unsupported WAVE_EXTENSIBLE subtype in {source}")
    elif audio_format != 1:
        raise ValueError(f"afconvert output is not PCM: format={audio_format}")
    if channels != 1 or actual_rate != sample_rate or bits != 16 or block_align != 2:
        raise ValueError(
            f"unexpected PCM format in {source}: {channels}ch/{actual_rate}Hz/{bits}bit"
        )

    with wave.open(str(target), "wb") as stream:
        stream.setnchannels(channels)
        stream.setsampwidth(bits // 8)
        stream.setframerate(sample_rate)
        stream.writeframes(data)


def main() -> None:
    if shutil.which("afconvert") is None:
        raise SystemExit("afconvert is required to convert the checked-in CC0 sources")

    for filename, relative_source in SOURCES.items():
        target = OUT / filename
        convert(SOURCE_ROOT / relative_source, target)
        print(f"footsteps: {filename} <- {relative_source}")


if __name__ == "__main__":
    main()
