#!/usr/bin/env python3
"""Validate and install declared human radio recordings; never generates audio.

Usage: python3 eng/apply-act1-radio-recordings.py DELIVERY [--dry-run] [--require-complete]
The delivery contract is documented in eng/radio-recording-delivery.md.
No engine/build/import is started here: the integration owner runs them serially.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import tempfile
from urllib.parse import urlparse


STATION = Path("game/content/vehicles/avyl_radio.v1.json")
RUNTIME = Path("game/assets/audio/act1/radio")
SOURCES = Path("assets/source/audio/act1/radio")
LICENSES = {
    "CC0-1.0": "https://creativecommons.org/publicdomain/zero/1.0/",
    "CC-BY-4.0": "https://creativecommons.org/licenses/by/4.0/",
    "CC-BY-SA-4.0": "https://creativecommons.org/licenses/by-sa/4.0/",
    "CC-BY-3.0": "https://creativecommons.org/licenses/by/3.0/",
    "CC-BY-SA-3.0": "https://creativecommons.org/licenses/by-sa/3.0/",
}


class DeliveryError(ValueError):
    pass


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def nonempty(value, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise DeliveryError(f"{label}: nonempty text is required")
    return value


def https(value, label: str) -> str:
    value = nonempty(value, label)
    parsed = urlparse(value)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password:
        raise DeliveryError(f"{label}: a public HTTPS source is required")
    return value


def sha(value, label: str) -> str:
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
        raise DeliveryError(f"{label}: a lowercase SHA-256 is required")
    return value


def strict_json(data: bytes):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise DeliveryError("duplicate JSON key: " + key)
            result[key] = value
        return result

    def constant(value):
        raise DeliveryError("non-finite JSON value: " + value)

    return json.loads(data, object_pairs_hook=pairs, parse_constant=constant)


def encoded(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def local_blob(root: Path, relative, expected, retained: dict[str, bytes]) -> bytes:
    relative = nonempty(relative, "delivery file")
    name = Path(relative)
    candidate = root / name
    if name.is_absolute() or ".." in name.parts or not candidate.resolve().is_relative_to(root):
        raise DeliveryError("delivery file escapes its package: " + relative)
    if not candidate.is_file() or candidate.stat().st_size > 160 * 1024 * 1024:
        raise DeliveryError("delivery file is absent or exceeds 160 MiB: " + relative)
    data = candidate.read_bytes()
    if not data or digest(data) != sha(expected, relative):
        raise DeliveryError("delivery file hash differs or file is empty: " + relative)
    retained[name.as_posix()] = data
    return data


def read_wav_pcm(data: bytes):
    if len(data) < 44 or data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise DeliveryError("recording must be a real RIFF/WAVE file")
    if struct.unpack_from("<I", data, 4)[0] + 8 != len(data):
        raise DeliveryError("WAV RIFF length differs from actual bytes")
    fmt = samples = None
    cursor = 12
    while cursor < len(data):
        if cursor + 8 > len(data):
            raise DeliveryError("truncated WAV chunk header")
        name = data[cursor:cursor + 4]
        size = struct.unpack_from("<I", data, cursor + 4)[0]
        start = cursor + 8
        end = start + size
        if end + (size & 1) > len(data):
            raise DeliveryError("truncated WAV chunk")
        if name == b"fmt ":
            if fmt is not None or size < 16:
                raise DeliveryError("invalid or duplicate WAV format chunk")
            fmt = struct.unpack_from("<HHIIHH", data, start)
            if fmt[0] == 65534:
                pcm_guid = bytes.fromhex("0100000000001000800000aa00389b71")
                if size < 40 or struct.unpack_from("<H", data, start + 16)[0] < 22 \
                        or struct.unpack_from("<H", data, start + 18)[0] != fmt[5] \
                        or data[start + 24:start + 40] != pcm_guid:
                    raise DeliveryError("unsupported extensible WAV subtype or valid sample bits")
                fmt = (1, *fmt[1:])
        elif name == b"data":
            if samples is not None:
                raise DeliveryError("duplicate WAV sample chunk")
            samples = memoryview(data)[start:end]
        cursor = end + (size & 1)
    if fmt is None or samples is None:
        raise DeliveryError("WAV requires format and sample chunks")
    tag, channels, rate, byte_rate, align, bits = fmt
    if tag != 1 or channels not in (1, 2) or rate not in (44100, 48000) or bits not in (16, 24):
        raise DeliveryError("WAV must be integer PCM, mono/stereo, 44.1/48 kHz, 16/24 bit")
    if align != channels * bits // 8 or byte_rate != rate * align or len(samples) % align:
        raise DeliveryError("WAV sample alignment or byte rate is invalid")
    return fmt, samples


def inspect_wav(data: bytes, kind: str) -> dict:
    fmt, samples = read_wav_pcm(data)
    _tag, channels, rate, _byte_rate, align, bits = fmt
    frames = len(samples) // align
    seconds = frames / rate
    minimum, maximum = (10, 900) if kind == "music" else (.4, 180)
    if not minimum <= seconds <= maximum:
        raise DeliveryError(f"recording duration {seconds:.3f}s is outside {minimum}–{maximum}s")
    step = bits // 8
    full = 1 << (bits - 1)
    peak = square_sum = 0
    first_active = last_active = None
    for index, offset in enumerate(range(0, len(samples), step)):
        value = int.from_bytes(samples[offset:offset + step], "little", signed=True)
        magnitude = abs(value)
        peak = max(peak, magnitude)
        square_sum += value * value
        if magnitude / full >= .001:
            frame = index // channels
            if first_active is None:
                first_active = frame
            last_active = frame
    if not peak or first_active is None:
        raise DeliveryError("recording is silent or below the -60 dBFS activity floor")
    peak_db = 20 * math.log10(peak / full)
    rms_db = 10 * math.log10(square_sum / (frames * channels * full * full))
    if peak_db > -1:
        raise DeliveryError(f"recording peak {peak_db:.2f} dBFS exceeds the -1 dBFS ceiling")
    if rms_db < -45:
        raise DeliveryError("recording RMS is below -45 dBFS; inspect the supplied recording")
    head = first_active / rate
    tail = (frames - 1 - last_active) / rate
    silence_limit = 2 if kind == "music" else .75
    if head > silence_limit or tail > silence_limit:
        raise DeliveryError("recording has excessive leading or trailing silence")
    return {"durationSeconds": seconds, "sampleRate": rate, "channels": channels,
            "bitsPerSample": bits, "frames": frames, "peakDbfs": peak_db, "rmsDbfs": rms_db,
            "leadingSilenceSeconds": head, "trailingSilenceSeconds": tail}


def prepare_delivery(delivery: Path, project: Path, require_complete: bool = False) -> dict:
    delivery, project = delivery.resolve(), project.resolve()
    manifest_bytes = (delivery / "provenance.json").read_bytes()
    manifest = strict_json(manifest_bytes)
    station_bytes = (project / STATION).read_bytes()
    station = strict_json(station_bytes)
    if not isinstance(manifest, dict) or not isinstance(station, dict):
        raise DeliveryError("station and delivery must be JSON objects")
    if manifest.get("schemaVersion") != 1 or station.get("schemaVersion") != 1:
        raise DeliveryError("station and delivery require schemaVersion 1")
    if manifest.get("stationId") != station.get("stationId"):
        raise DeliveryError("delivery stationId differs from the existing station")
    segments = {segment["id"]: segment for segment in station["segments"]}
    if len(segments) != len(station["segments"]):
        raise DeliveryError("existing station has duplicate segment IDs")
    records = manifest.get("recordings")
    if not isinstance(records, list) or not records:
        raise DeliveryError("delivery needs at least one selected human recording")
    retained = {"provenance.json": manifest_bytes}
    outputs: dict[str, bytes] = {}
    reports = []
    seen = set()
    delivery_id = digest(manifest_bytes)
    archive = SOURCES / delivery_id
    for record in records:
        if not isinstance(record, dict):
            raise DeliveryError("each selected recording must be a JSON object")
        segment_id = record.get("segmentId")
        if segment_id not in segments or segment_id in seen:
            raise DeliveryError("unknown or duplicate segmentId: " + str(segment_id))
        seen.add(segment_id)
        segment = segments[segment_id]
        if not re.fullmatch(r"[a-z0-9][a-z0-9-]*", segment_id):
            raise DeliveryError("segment ID cannot form a stable runtime filename")
        kind = segment["kind"]
        origin = "recorded-music" if kind == "music" else "human-recording"
        if record.get("origin") != origin:
            raise DeliveryError("recording origin must be " + origin + "; fixtures and synthesis are not accepted")
        nonempty(record.get("credit"), "recording.credit")
        nonempty(record.get("performer"), "recording.performer")
        https(record.get("sourceURL"), "recording.sourceURL")
        if record.get("language") != segment["language"]:
            raise DeliveryError("recording language differs from its programmed segment")
        transcript = nonempty(record.get("transcript"), "recording.transcript")
        if kind != "music" and transcript != segment["transcript"]:
            raise DeliveryError("spoken transcript differs from the existing authored programme")
        required = {"recording", "performance"}
        if kind == "music":
            required |= {"composition", "arrangement"}
            if not record.get("instrumental", False):
                required.add("lyrics")
        permissions = record.get("rights")
        if not isinstance(permissions, list) or not permissions:
            raise DeliveryError("recording rights and evidence are required")
        covered = set()
        for permission in permissions:
            if not isinstance(permission, dict):
                raise DeliveryError("each rights declaration must be a JSON object")
            license_id = permission.get("licenseId")
            if license_id not in LICENSES:
                raise DeliveryError("unsupported open recording licence: " + str(license_id))
            if https(permission.get("licenseURL"), "licenseURL").rstrip("/") != LICENSES[license_id].rstrip("/"):
                raise DeliveryError("licence ID and canonical URL differ")
            nonempty(permission.get("copyrightHolder"), "rights.copyrightHolder")
            https(permission.get("sourceURL"), "rights.sourceURL")
            scopes = permission.get("covers")
            if not isinstance(scopes, list) or not scopes or any(not isinstance(scope, str) for scope in scopes):
                raise DeliveryError("rights.covers must identify recording/performance and musical layers")
            covered.update(scopes)
            local_blob(delivery, permission.get("evidenceFile"), permission.get("evidenceSha256"), retained)
        if not required <= covered:
            raise DeliveryError("rights do not cover: " + ", ".join(sorted(required - covered)))
        processing = record.get("processing")
        if not isinstance(processing, list) or not processing or any(not isinstance(step, str) or not step.strip() for step in processing):
            raise DeliveryError("recording.processing must describe conversion/editing or unchanged delivery")
        local_blob(delivery, record.get("sourceFile"), record.get("sourceSha256"), retained)
        wav = local_blob(delivery, record.get("file"), record.get("sha256"), retained)
        audio = inspect_wav(wav, kind)
        declared_duration = record.get("durationSeconds")
        if isinstance(declared_duration, bool) or not isinstance(declared_duration, (int, float)) \
                or abs(declared_duration - audio["durationSeconds"]) > 1 / audio["sampleRate"]:
            raise DeliveryError("declared duration must match the actual decoded sample count")
        target = RUNTIME / (segment_id + ".wav")
        outputs[target.as_posix()] = wav
        # An explicit delivered recording supersedes this segment's preview or
        # repeat identity; unrelated story/author metadata remains intact.
        segment.pop("modelLicense", None)
        segment.pop("repeatsSegmentId", None)
        segment.update(streamPath="res://assets/audio/act1/radio/" + segment_id + ".wav",
                       durationSeconds=audio["durationSeconds"], sha256=digest(wav), rightsStatus="approved",
                       origin=origin, acceptance="recording-delivered; listening-and-language-review-pending",
                       provenance=(archive / "provenance.json").as_posix() + "#" + segment_id)
        if kind == "music":
            segment["title"] = nonempty(record.get("title"), "recording.title")
            segment["transcript"] = transcript
        reports.append({"segmentId": segment_id, "sourceURL": record["sourceURL"],
                        "sha256": digest(wav), "audio": audio, "credit": record["credit"],
                        "performer": record["performer"], "rights": permissions, "processing": processing})
    for segment_id, segment in segments.items():
        if segment_id in seen or segment.get("rightsStatus") != "approved":
            continue
        expected_path = "res://assets/audio/act1/radio/" + segment_id + ".wav"
        if segment.get("streamPath") != expected_path:
            raise DeliveryError("existing approved segment has an unexpected runtime path: " + segment_id)
        path = project / RUNTIME / (segment_id + ".wav")
        if not path.resolve().is_relative_to(project) or not path.is_file():
            raise DeliveryError("existing approved recording is absent: " + segment_id)
        wav = path.read_bytes()
        if digest(wav) != sha(segment.get("sha256"), segment_id):
            raise DeliveryError("existing approved recording hash differs: " + segment_id)
        audio = inspect_wav(wav, segment["kind"])
        duration = segment.get("durationSeconds")
        if not isinstance(duration, (int, float)) or isinstance(duration, bool) \
                or not math.isfinite(duration) or abs(duration - audio["durationSeconds"]) > 1 / audio["sampleRate"]:
            raise DeliveryError("existing approved recording duration differs: " + segment_id)
        provenance = nonempty(segment.get("provenance"), "existing recording provenance")
        source_path, separator, source_id = provenance.partition("#")
        source = project / source_path
        if separator != "#" or source_id != segment_id or not source.resolve().is_relative_to(project / SOURCES) or not source.is_file():
            raise DeliveryError("existing approved recording provenance is absent: " + segment_id)
    approved = sum(segment.get("rightsStatus") == "approved" for segment in station["segments"])
    if require_complete and approved != len(segments):
        raise DeliveryError("the existing programme still has missing recordings")
    station["audioAcceptance"] = ("recordings-delivered" if approved == len(segments) else "partial-recordings-delivered") \
        + "; native playback, listening, mix and language acceptance remain separate"
    credits = {"schemaVersion": 1, "stationId": station["stationId"], "deliveryId": delivery_id,
               "recordings": reports, "acceptance": "declared source/licence and byte validation only"}
    outputs[(RUNTIME / ("credits-" + delivery_id + ".json")).as_posix()] = encoded(credits)
    for name, data in retained.items():
        outputs[(archive / name).as_posix()] = data
    outputs[STATION.as_posix()] = encoded(station)
    return {"project": project, "outputs": outputs, "stationBefore": station_bytes,
            "archive": archive, "report": {"deliveryId": delivery_id, "recordings": reports,
            "approvedSegments": approved, "totalSegments": len(segments),
            "remainingSegmentIds": [row["id"] for row in station["segments"] if row.get("rightsStatus") != "approved"],
            "followup": "Register supplied assets; serial Godot import/build; native radio smoke; listening and language review."}}


def atomic_write(path: Path, data: bytes, mode: int | None = None):
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=".radio-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, mode if mode is not None else 0o644)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def apply_delivery(plan: dict):
    project = plan["project"]
    lock = project / STATION.parent / ".radio-install.lock"
    try:
        descriptor = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError as error:
        raise DeliveryError("another radio installation owns " + str(lock)) from error
    written = []
    try:
        os.close(descriptor)
        if (project / STATION).read_bytes() != plan["stationBefore"]:
            raise DeliveryError("station changed since validation; revalidate the delivery")
        for name, data in plan["outputs"].items():
            path = project / name
            if not path.resolve().is_relative_to(project) or path.is_symlink():
                raise DeliveryError("installation target escapes the project or is a symlink: " + name)
            previous = path.read_bytes() if path.is_file() else None
            mode = path.stat().st_mode & 0o777 if previous is not None else None
            if previous == data:
                continue
            if path.is_relative_to(project / plan["archive"]) and previous is not None:
                raise DeliveryError("immutable radio source delivery differs: " + name)
            atomic_write(path, data, mode)
            written.append((path, previous, mode, digest(data)))
    except BaseException:
        for path, previous, mode, installed_hash in reversed(written):
            if not path.is_file() or digest(path.read_bytes()) != installed_hash:
                raise DeliveryError("external edit detected during rollback; preserved " + str(path))
            if previous is None:
                path.unlink()
            else:
                atomic_write(path, previous, mode)
        raise
    finally:
        lock.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("delivery", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    try:
        plan = prepare_delivery(args.delivery, Path(__file__).resolve().parents[1], args.require_complete)
        if not args.dry_run:
            apply_delivery(plan)
        print(json.dumps({"mode": "dry-run" if args.dry_run else "installed", **plan["report"]}, ensure_ascii=False, indent=2))
        return 0
    except (DeliveryError, OSError, ValueError, KeyError, TypeError) as error:
        print("radio delivery rejected: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
