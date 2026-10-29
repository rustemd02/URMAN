#!/usr/bin/env python3
"""Create H002 source-only v3 by restoring the two original long Corner node names.

This writes only JSON node.name strings. The v2 GLB remains untouched; all BIN data,
materials, mesh/accessor arrays, transforms, UVs and embedded PBR payloads are retained.
No Blender, Meshy, rendering, Godot import, or runtime integration is performed.
"""
from __future__ import annotations
import copy
import hashlib
import json
import os
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
ASSET_DIR = HERE.parent
ROOT = HERE.parents[5]
V2 = HERE / "variant-b-roof-preserved-pbr-v2.glb"
V3 = HERE / "variant-b-roof-preserved-pbr-v3.glb"
SOURCE = ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
RECEIPT = ASSET_DIR / "candidate-receipt.json"
EXPECTED_V2_SHA256 = "abad5abd45ee8418a73efb55e8f0039f6140a113a2959ee82645578bd0f40006"
EXPECTED_SOURCE_SHA256 = "fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
PREFIX = "VariantB_PlasterAnnex_Dwelling_"
RENAMES = {
    "VariantB_PlasterAnnex_Dwelling_Corner_-2.75_5.1000000000000005_":
        "VariantB_PlasterAnnex_Dwelling_Corner_-2.75_5.1000000000000005_LOD0",
    "VariantB_PlasterAnnex_Dwelling_Corner_2.75_5.1000000000000005_L":
        "VariantB_PlasterAnnex_Dwelling_Corner_2.75_5.1000000000000005_LOD0",
}


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_glb(path: Path):
    raw = path.read_bytes()
    magic, version, declared_length = struct.unpack_from("<III", raw, 0)
    assert (magic, version, declared_length) == (0x46546C67, 2, len(raw)), path
    chunks = []
    offset = 12
    json_index = None
    document = None
    binary_chunks = []
    while offset < len(raw):
        size, kind = struct.unpack_from("<II", raw, offset)
        offset += 8
        payload = raw[offset:offset + size]
        assert len(payload) == size
        if kind == 0x4E4F534A:
            assert json_index is None
            json_index = len(chunks)
            document = json.loads(payload.decode("utf-8").rstrip(" \0"))
        elif kind == 0x004E4942:
            binary_chunks.append(payload)
        chunks.append((kind, payload))
        offset += size
    assert offset == len(raw) and json_index is not None and document is not None
    return raw, chunks, json_index, document, binary_chunks


def pack_glb(chunks: list[tuple[int, bytes]]) -> bytes:
    total = 12 + sum(8 + len(payload) for _, payload in chunks)
    output = bytearray(struct.pack("<III", 0x46546C67, 2, total))
    for kind, payload in chunks:
        output.extend(struct.pack("<II", len(payload), kind))
        output.extend(payload)
    assert len(output) == total
    return bytes(output)


assert V2.is_file() and SOURCE.is_file() and RECEIPT.is_file()
assert not V3.exists(), f"Refusing to overwrite {V3}"
v2_raw, v2_chunks, json_index, v2_doc, v2_bins = read_glb(V2)
source_raw, _, _, source_doc, _ = read_glb(SOURCE)
assert sha256(v2_raw) == EXPECTED_V2_SHA256
assert sha256(source_raw) == EXPECTED_SOURCE_SHA256
assert len(v2_bins) == 1
receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
assert receipt["source"]["path"] == "game/assets/models/act1/urman_village_exterior_kit.glb"
assert receipt["source"]["sha256"] == EXPECTED_SOURCE_SHA256

# Establish the complete raw-name contract: 113 original opaque nodes plus the
# three explicitly recorded generated log crowns. This is GLB raw-name matching;
# Godot importer normalization is intentionally outside this producer.
source_names = {
    node["name"] for node in source_doc["nodes"]
    if node.get("name", "").startswith(PREFIX)
}
excluded_names = {entry["node"] for entry in receipt["excluded"]}
opaque_names = source_names - excluded_names
crown_names = {entry["crown_node"] for entry in receipt["generated_crowns"]}
expected_names = opaque_names | crown_names
v2_target_nodes = [
    node for node in v2_doc["nodes"]
    if node.get("name", "").startswith(PREFIX)
]
v2_names = {node["name"] for node in v2_target_nodes}
assert len(source_names) == 123 and len(opaque_names) == 113
assert len(crown_names) == 3 and len(expected_names) == 116
assert len(v2_target_nodes) == 116 and len(v2_names) == 116
assert set(RENAMES.values()) <= source_names
assert v2_names - expected_names == set(RENAMES.keys())
assert expected_names - v2_names == set(RENAMES.values())
for old, new in RENAMES.items():
    assert sum(node.get("name") == old for node in v2_target_nodes) == 1
    assert sum(node.get("name") == new for node in source_doc["nodes"]) == 1

# Update the node.name fields structurally. Canonical compact serialization of v2
# must match its JSON bytes first, so re-serialization cannot reorder/reformat fields.
kind, original_json = v2_chunks[json_index]
assert kind == 0x4E4F534A
json_body = original_json.rstrip(b" ")
assert json.loads(json_body.decode("utf-8")) == v2_doc
compact_v2 = json.dumps(v2_doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
assert compact_v2 == json_body
new_doc = copy.deepcopy(v2_doc)
for old, new in RENAMES.items():
    matches = [node for node in new_doc["nodes"] if node.get("name") == old]
    assert len(matches) == 1, old
    matches[0]["name"] = new
json_body = json.dumps(new_doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
json_body += b" " * ((-len(json_body)) % 4)
assert json.loads(json_body.decode("utf-8").rstrip(" ")) == new_doc

# Parsed JSON comparison proves exactly two node.name values changed.
normalized = copy.deepcopy(new_doc)
for node in normalized["nodes"]:
    if node.get("name") in RENAMES.values():
        node["name"] = {value: key for key, value in RENAMES.items()}[node["name"]]
assert normalized == v2_doc
new_chunks = list(v2_chunks)
new_chunks[json_index] = (kind, json_body)
assert all(
    new_chunks[index] == v2_chunks[index]
    for index in range(len(v2_chunks)) if index != json_index
)
new_raw = pack_glb(new_chunks)
tmp = V3.with_name(V3.name + ".tmp")
assert not tmp.exists()
tmp.write_bytes(new_raw)
check_raw, check_chunks, check_json_index, check_doc, check_bins = read_glb(tmp)
assert check_doc == new_doc and check_bins == v2_bins
assert sha256(check_bins[0]) == sha256(v2_bins[0])
assert len(check_doc["nodes"]) == len(v2_doc["nodes"])
check_names = {
    node["name"] for node in check_doc["nodes"]
    if node.get("name", "").startswith(PREFIX)
}
assert check_names == expected_names and len(check_names) == 116
assert check_json_index == json_index and len(check_chunks) == len(v2_chunks)
os.replace(tmp, V3)

print(json.dumps({
    "input": str(V2), "input_sha256": sha256(v2_raw),
    "output": str(V3), "output_bytes": V3.stat().st_size,
    "output_sha256": sha256(V3.read_bytes()),
    "source_original_glb": str(SOURCE), "source_original_sha256": sha256(source_raw),
    "renamed_node_names": RENAMES,
    "raw_name_contract": {"original_opaque": len(opaque_names), "generated_crowns": len(crown_names), "exact_total": len(check_names), "exact_set_match": check_names == expected_names},
    "all_non_json_chunks_byte_identical": True,
    "bin_chunk_bytes": len(v2_bins[0]), "bin_chunk_sha256": sha256(v2_bins[0]),
    "only_two_node_name_values_changed": True,
    "godot_importer_normalization": "pending",
}, ensure_ascii=False, separators=(",", ":")))
