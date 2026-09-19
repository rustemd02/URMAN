"""Shared Godot v4 PCK reader, extracted unchanged from the Act I scope verifier."""

import struct
import unicodedata
from pathlib import Path


def fail(message: str) -> None:
    raise ValueError(message)


def read_pck(path: Path) -> tuple[bytes, dict[str, tuple[int, int]]]:
    raw = path.read_bytes()
    if len(raw) < 0x70 or raw[:4] != b"GDPC":
        fail(f"{path.name} is not a Godot PCK")
    if struct.unpack_from("<I", raw, 4)[0] != 4:
        fail(f"{path.name} is not a Godot 4 PCK")

    data_base, index_base = struct.unpack_from("<QQ", raw, 0x18)
    if not 0x70 <= data_base < index_base < len(raw):
        fail(f"{path.name} has invalid data/index bounds")

    count = struct.unpack_from("<I", raw, index_base)[0]
    if not 0 < count <= 10000:
        fail(f"{path.name} has an invalid file count: {count}")

    entries: dict[str, tuple[int, int]] = {}
    folded: dict[str, str] = {}
    cursor = index_base + 4
    for entry_number in range(count):
        if cursor + 4 > len(raw):
            fail(f"{path.name} index ends before entry {entry_number}")
        field_length = struct.unpack_from("<I", raw, cursor)[0]
        cursor += 4
        if field_length == 0 or field_length > 4096 or cursor + field_length > len(raw):
            fail(f"{path.name} has an invalid path length at entry {entry_number}")
        field = raw[cursor : cursor + field_length]
        cursor += field_length
        trimmed = field.rstrip(b"\0")
        if not trimmed or b"\0" in trimmed:
            fail(f"{path.name} has an invalid padded path at entry {entry_number}")
        try:
            name = trimmed.decode("utf-8")
        except UnicodeDecodeError as error:
            fail(f"{path.name} has a non-UTF-8 path at entry {entry_number}: {error}")

        if cursor + 8 + 8 + 16 + 4 > len(raw):
            fail(f"{path.name} index ends inside entry {entry_number}")
        data_offset, data_size = struct.unpack_from("<QQ", raw, cursor)
        cursor += 16 + 16 + 4
        # Godot PCK v4 stores payload offsets relative to the file-data base.
        # Offset zero is the first payload, not a missing-resource sentinel.
        data_offset += data_base
        if data_offset > len(raw) or data_size > len(raw) - data_offset:
            fail(f"{path.name} has an out-of-bounds payload: {name}")
        if data_offset < data_base or data_offset + data_size > index_base:
            fail(f"{path.name} has a payload overlapping its index: {name}")

        if "\\" in name or name.startswith("/") or any(part in {"", ".", ".."} for part in name.split("/")):
            fail(f"{path.name} has an unsafe resource path: {name}")
        normalized = unicodedata.normalize("NFKC", name).casefold()
        previous = folded.get(normalized)
        if previous is not None and previous != name:
            fail(f"{path.name} has a case-folded path collision: {previous} / {name}")
        folded[normalized] = name
        if name in entries:
            fail(f"{path.name} contains a duplicate resource path: {name}")
        entries[name] = (data_offset, data_size)

    # Native Windows exports append at most seven zero alignment bytes before
    # their size/magic footer; extracted PCKs retain that alignment.
    trailing = raw[cursor:]
    if len(trailing) > 7 or any(trailing):
        fail(f"{path.name} has non-alignment bytes after its resource index")
    return raw, entries

