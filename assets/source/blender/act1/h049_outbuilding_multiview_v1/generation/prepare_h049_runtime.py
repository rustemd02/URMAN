"""Make one 2K H049 GLB following prepare_aidar_player_runtime.py.

Use --restore-input explicitly to restore only the SHA-locked archive member,
without overwrite. Then --prepare resizes embedded PNGs, verifies the readback,
and removes that closed, exact archived input duplicate. No Blender re-export.
"""
if not __debug__:
    raise RuntimeError("Run without Python optimization; integrity guards require assertions")

from pathlib import Path
import argparse, copy, hashlib, io, json, struct, subprocess, tarfile

import PIL
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]
ARCHIVE = HERE / "h049-derived-source-v1.tar.xz"
SOURCE = HERE / "h049-roof-body-surfaces-v1.glb"
OUTPUT = ROOT / "game/assets/models/act1/h049-outbuilding-pbr-v1.glb"
RECEIPT = HERE / "h049-runtime-2k-receipt.json"
ARCHIVE_SHA = "ecc1811b5fab0d2101637e4553b5026b4106e44ebdb0512dd0c247826b8a979f"
SOURCE_SHA = "4e7c9d6cedf4eba93beedcde2c7bb2c031c85ab4af7f2050e444de1773c8bd68"
SOURCE_BYTES = 38681780


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verified_member():
    assert sha(ARCHIVE.read_bytes()) == ARCHIVE_SHA
    with tarfile.open(ARCHIVE, "r:xz") as archive:
        member = archive.getmember(SOURCE.name)
        assert member.isfile() and member.size == SOURCE_BYTES
        with archive.extractfile(member) as stream:
            data = stream.read()
    assert len(data) == SOURCE_BYTES and sha(data) == SOURCE_SHA
    return data


def restore():
    assert not SOURCE.exists(), "Explicit restoration never overwrites an existing file"
    data = verified_member()
    with SOURCE.open("xb") as stream:
        stream.write(data)
    print(json.dumps({"restored_only": SOURCE.name, "bytes": len(data), "sha256": SOURCE_SHA}))


def read_glb(data):
    assert struct.unpack_from("<III", data) == (0x46546C67, 2, len(data))
    size, kind = struct.unpack_from("<II", data, 12)
    assert kind == 0x4E4F534A
    doc = json.loads(data[20:20+size])
    length, kind = struct.unpack_from("<II", data, 20+size)
    assert kind == 0x004E4942 and len(data) == 28+size+length
    return doc, data[28+size:]


def view_bytes(binary, view):
    start = view.get("byteOffset", 0)
    return binary[start:start+view["byteLength"]]


def save(report):
    RECEIPT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


def prepare():
    assert not OUTPUT.exists() and not RECEIPT.exists(), "Refuse a second output or overwrite"
    archived = verified_member()
    source = SOURCE.read_bytes()
    assert source == archived
    del archived
    doc, binary = read_glb(source)
    assert len(doc["meshes"]) == 1 and len(doc["meshes"][0]["primitives"]) == 2
    assert len(doc["materials"]) == 2 and doc["materials"][1]["name"] == "H049_Roof_PBR"
    assert len(doc["buffers"]) == 1 and "uri" not in doc["buffers"][0]
    assert len(doc["images"]) == 4
    image_views = {image["bufferView"] for image in doc["images"]}
    assert len(image_views) == 4
    assert all(accessor.get("bufferView") not in image_views for accessor in doc["accessors"])
    roles = {}
    for material in doc["materials"]:
        for role, binding in (("baseColor_sRGB", material["pbrMetallicRoughness"]["baseColorTexture"]),
                              ("emissive_sRGB", material["emissiveTexture"]),
                              ("normal_linear_data", material["normalTexture"]),
                              ("metallicRoughness_linear_data", material["pbrMetallicRoughness"]["metallicRoughnessTexture"])):
            index = doc["textures"][binding["index"]]["source"]
            roles.setdefault(doc["images"][index]["bufferView"], set()).add(role)
    output_doc = copy.deepcopy(doc)
    output_binary = bytearray()
    images, nonimages = [], []
    for index, view in enumerate(doc["bufferViews"]):
        assert view["buffer"] == 0
        payload = view_bytes(binary, view)
        assert len(payload) == view["byteLength"]
        if index in image_views:
            image_index = next(i for i, image in enumerate(doc["images"]) if image["bufferView"] == index)
            assert doc["images"][image_index]["mimeType"] == "image/png"
            before = Image.open(io.BytesIO(payload))
            assert before.format == "PNG" and before.mode == "RGB" and before.size == (4096, 4096)
            assert not before.info and payload[24:26] == bytes((8, 2))
            before_hash, before_bytes = sha(payload), len(payload)
            # Same channel-preserving Pillow operation as the existing Aidar recipe.
            before.thumbnail((2048, 2048), Image.Resampling.LANCZOS)
            encoded = io.BytesIO()
            before.save(encoded, format="PNG", optimize=True)
            payload = encoded.getvalue()
            after = Image.open(io.BytesIO(payload))
            assert after.mode == "RGB" and after.size == (2048, 2048)
            assert after.getbands() == ("R", "G", "B") and payload[24:26] == bytes((8, 2))
            images.append({"image": image_index, "name": doc["images"][image_index].get("name"),
                           "buffer_view": index, "roles_and_color_space_semantics": sorted(roles[index]),
                           "format_before_and_after": "PNG", "mode_before_and_after": "RGB8",
                           "bands_before_and_after": ["R", "G", "B"],
                           "size_before": [4096, 4096], "size_after": [2048, 2048],
                           "bytes_before": before_bytes, "bytes_after": len(payload),
                           "sha256_before": before_hash, "sha256_after": sha(payload),
                           "channel_policy": "Independent existing RGB components only; no swizzle, normal reorientation/renormalization, color conversion, grade or UV rebake"})
        else:
            nonimages.append({"buffer_view": index, "bytes": len(payload), "sha256": sha(payload)})
        output_binary.extend(b"\0"*((-len(output_binary))%4))
        output_doc["bufferViews"][index]["byteOffset"] = len(output_binary)
        output_doc["bufferViews"][index]["byteLength"] = len(payload)
        output_binary.extend(payload)
    output_doc["buffers"][0]["byteLength"] = len(output_binary)
    encoded = json.dumps(output_doc, separators=(",", ":"), ensure_ascii=False).encode()
    encoded += b" "*((-len(encoded))%4)
    output_binary.extend(b"\0"*((-len(output_binary))%4))
    packed = (struct.pack("<III", 0x46546C67, 2, 28+len(encoded)+len(output_binary))
              + struct.pack("<II", len(encoded), 0x4E4F534A)+encoded
              + struct.pack("<II", len(output_binary), 0x004E4942)+output_binary)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT.open("xb") as stream:
        stream.write(packed)
    actual, actual_binary = read_glb(OUTPUT.read_bytes())
    unchanged = sorted(doc.keys()-{"bufferViews", "buffers"})
    assert all(actual[key] == doc[key] for key in unchanged)
    for index, original_view in enumerate(doc["bufferViews"]):
        original_other = {k: v for k, v in original_view.items() if k not in {"byteOffset", "byteLength"}}
        actual_view = actual["bufferViews"][index]
        assert {k: v for k, v in actual_view.items() if k not in {"byteOffset", "byteLength"}} == original_other
        if index not in image_views:
            assert view_bytes(binary, original_view) == view_bytes(actual_binary, actual_view)
    for row in images:
        payload = view_bytes(actual_binary, actual["bufferViews"][row["buffer_view"]])
        assert sha(payload) == row["sha256_after"] and len(payload) == row["bytes_after"]
        assert Image.open(io.BytesIO(payload)).size == (2048, 2048)
    primitive_counts = [actual["accessors"][p["indices"]]["count"]//3 for p in actual["meshes"][0]["primitives"]]
    assert primitive_counts == [5415, 123]
    report = {"schema": "h049-runtime-2k-pbr-candidate/v1", "status": "prepared_readback_pass_cleanup_pending",
              "producer": Path(__file__).name, "producer_sha256": sha(Path(__file__).read_bytes()),
              "method_reference": "assets/source/blender/act1/aidar_meshy_v1/prepare_aidar_player_runtime.py",
              "method": "Pillow thumbnail max2048 LANCZOS; same RGB8 PNG mode, optimize=True; direct GLB view repack only",
              "pillow_version": PIL.__version__, "archive": {"path": ARCHIVE.name, "bytes": ARCHIVE.stat().st_size,
              "sha256": ARCHIVE_SHA, "member": SOURCE.name, "member_sha256": SOURCE_SHA, "member_bytes": SOURCE_BYTES,
              "verification": "Full archive SHA, full decompressed member SHA/size, and restored file equality verified before conversion; archive retained"},
              "input": {"path": SOURCE.name, "sha256": SOURCE_SHA, "bytes": SOURCE_BYTES},
              "output": {"path": str(OUTPUT.relative_to(ROOT)), "sha256": sha(OUTPUT.read_bytes()), "bytes": OUTPUT.stat().st_size},
              "images": images, "byte_identical_nonimage_buffer_views": nonimages,
              "unchanged_json_sections": unchanged, "all_ordered_geometry_indices_positions_normals_uv_byte_identical": True,
              "materials_texture_bindings_image_roles_nodes_transforms_unchanged": True,
              "primitive_count": 2, "primitive_triangle_counts": primitive_counts, "total_triangles": 5538,
              "vertex_attribute_count": actual["accessors"][0]["count"], "additional_meshy_credits": 0,
              "acceptance": "Prepared asset candidate only. Godot import, native winter/full-scene/art/performance acceptance pending; no renderer/game/build/network called",
              "restore_command": "python3 "+str(Path(__file__).relative_to(ROOT))+" --restore-input",
              "restore_policy": "Only the locked member; explicit --restore-input refuses any existing destination. --prepare never overwrites output/receipt.",
              "cleanup": {"deleted_bytes": 0, "retained": "Unique original archive, recipe/receipt and only named 2K output; no external extracted map or duplicate generated GLB"}}
    save(report)
    assert sha(SOURCE.read_bytes()) == SOURCE_SHA
    handles = subprocess.run(["/usr/sbin/lsof", "-F", "p", "--", str(SOURCE)], capture_output=True, text=True)
    assert handles.returncode == 1 and not handles.stdout and not handles.stderr, "Source duplicate has open handles or lsof failed; retained for manual cleanup"
    SOURCE.unlink()
    report["cleanup"].update({"deleted_path": SOURCE.name, "deleted_bytes": SOURCE_BYTES,
                              "deleted_sha256": SOURCE_SHA, "lsof_no_handles": True,
                              "reason": "Exact restored duplicate of full-SHA-verified retained lossless archive member"})
    report["status"] = "runtime_asset_candidate_frozen_native_art_performance_pending"
    save(report)
    print(json.dumps({"output": report["output"], "primitive_triangles": primitive_counts,
                      "images_max_dimension": 2048, "freed_bytes": SOURCE_BYTES, "additional_credits": 0}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--restore-input", action="store_true")
    group.add_argument("--prepare", action="store_true")
    args = parser.parse_args()
    restore() if args.restore_input else prepare()
