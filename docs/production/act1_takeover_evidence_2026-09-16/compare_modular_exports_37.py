"""Compare two static modular GLBs without importing or changing either file.

Only the six agreed kettle/lid/keyboard meshes may change accessor data. Node
identity/order, hierarchy, transforms, extras, materials and primitive contracts
remain strict. Mesh datablock names are reported separately. No triangle sorting,
float decoding/rounding or welding is used: winding and every component bit count.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct


EXPECTED = frozenset(f"{stem}_LOD{lod}" for stem in (
    "HouseInterior_TableKettle", "HouseInterior_TableKettleLid", "OldPc_Keyboard")
    for lod in (0, 1))
COMPONENT_BYTES = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
WIDTHS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
LIBRARIES = {"nodes", "meshes", "accessors", "bufferViews", "buffers"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def without(value, keys):
    return {key: item for key, item in value.items() if key not in keys}


def inspect_bytes(raw):
    require(len(raw) >= 20, "Truncated GLB header")
    magic, version, length = struct.unpack_from("<4sII", raw)
    require((magic, version, length) == (b"glTF", 2, len(raw)), "Invalid GLB header")
    chunks, offset = [], 12
    while offset < len(raw):
        require(offset + 8 <= len(raw), "Truncated GLB chunk header")
        size, kind = struct.unpack_from("<I4s", raw, offset)
        require(size % 4 == 0 and offset + 8 + size <= len(raw), "Invalid GLB chunk bounds")
        chunks.append((kind, raw[offset + 8:offset + 8 + size]))
        offset += 8 + size
    require([kind for kind, _ in chunks] == [b"JSON", b"BIN\0"], "Expected one JSON and one BIN chunk")
    doc, binary = json.loads(chunks[0][1]), chunks[1][1]
    require(doc.get("asset", {}).get("version") == "2.0", "Not glTF 2.0")
    buffers = doc.get("buffers", [])
    require(len(buffers) == 1 and "uri" not in buffers[0], "External or multiple buffers are unsupported")
    size = buffers[0]["byteLength"]
    require(0 <= size <= len(binary) and len(binary) - size <= 3, "Invalid embedded buffer length")
    binary = binary[:size]
    # This known static export has no compressed, sparse, animated or skinned
    # content. Fail closed if a later exporter adds an unexamined data owner.
    require(not doc.get("animations") and not doc.get("skins"), "Animated/skinned input is outside this static comparison")
    def extensions(value):
        if isinstance(value, dict):
            require(not value.get("extensions"), "An extension requires explicit comparison support")
            for child in value.values():
                extensions(child)
        elif isinstance(value, list):
            for child in value:
                extensions(child)
    extensions(doc)
    require(not doc.get("extensionsUsed") and not doc.get("extensionsRequired"), "Extended export is unsupported")
    accessors, views = doc.get("accessors", []), doc.get("bufferViews", [])
    used_accessors, used_views = set(), set()
    cache = {}

    def accessor(index):
        require(type(index) is int and 0 <= index < len(accessors), "Invalid accessor reference")
        used_accessors.add(index)
        if index in cache:
            return cache[index]
        item = accessors[index]
        require("sparse" not in item, "Sparse accessor requires explicit comparison support")
        require(item.get("componentType") in COMPONENT_BYTES and item.get("type") in WIDTHS,
                "Unsupported accessor component/layout")
        width = COMPONENT_BYTES[item["componentType"]] * WIDTHS[item["type"]]
        count = item["count"]
        require(type(count) is int and 0 < count <= 10000000, "Invalid accessor count")
        if "bufferView" not in item:
            require(item.get("byteOffset", 0) == 0, "Accessor without buffer has an offset")
            packed, view_metadata = bytes(width * count), {}
        else:
            view_index = item["bufferView"]
            require(type(view_index) is int and 0 <= view_index < len(views), "Invalid bufferView reference")
            used_views.add(view_index)
            view = views[view_index]
            start, view_length = view.get("byteOffset", 0), view["byteLength"]
            relative, stride = item.get("byteOffset", 0), view.get("byteStride", width)
            require(view.get("buffer", 0) == 0 and start >= 0 and view_length >= 0
                    and start + view_length <= len(binary), "bufferView leaves the embedded buffer")
            require(relative >= 0 and stride >= width
                    and relative + (count - 1) * stride + width <= view_length, "Accessor leaves its bufferView")
            packed = b"".join(binary[start + relative + n * stride:start + relative + n * stride + width]
                              for n in range(count))
            view_metadata = without(view, {"buffer", "byteOffset", "byteLength", "byteStride"})
        descriptor = without(item, {"bufferView", "byteOffset", "extras", "name"})
        # Padding/packing offsets may change; the ordered component bytes may not.
        result = {"descriptor": descriptor, "componentBytes": len(packed), "sha256": sha(packed),
                  "metadata": {"accessor": {k: item[k] for k in ("name", "extras") if k in item},
                               "bufferView": view_metadata}}
        cache[index] = result
        return result

    nodes, meshes = doc.get("nodes", []), doc.get("meshes", [])
    names = [node.get("name") for node in nodes]
    require(all(isinstance(name, str) and name for name in names) and len(set(names)) == len(names),
            "Every node needs a unique preserved source name")
    parents = {name: [] for name in names}
    for parent in nodes:
        for child in parent.get("children", []):
            require(type(child) is int and 0 <= child < len(nodes), "Invalid child node reference")
            parents[names[child]].append(parent["name"])
    result, used_meshes = {}, set()
    for node_index, node in enumerate(nodes):
        record = {"index": node_index, "parents": parents[node["name"]],
                  "node": without(node, {"mesh"}), "mesh": None}
        if "mesh" in node:
            mesh_index = node["mesh"]
            require(type(mesh_index) is int and 0 <= mesh_index < len(meshes), "Invalid mesh reference")
            used_meshes.add(mesh_index)
            mesh, primitives = meshes[mesh_index], []
            for primitive in mesh["primitives"]:
                attrs = {key: accessor(value) for key, value in primitive["attributes"].items()}
                require("POSITION" in attrs, "Primitive is missing POSITION")
                require(len({value["descriptor"]["count"] for value in attrs.values()}) == 1,
                        "Primitive attributes disagree on vertex count")
                indices = accessor(primitive["indices"]) if "indices" in primitive else None
                if indices is not None:
                    require(indices["descriptor"]["type"] == "SCALAR"
                            and indices["descriptor"]["componentType"] in (5121, 5123, 5125), "Invalid index accessor")
                targets = [{key: accessor(value) for key, value in target.items()}
                           for target in primitive.get("targets", [])]
                contract = without(primitive, {"attributes", "indices", "targets"})
                contract["mode"] = primitive.get("mode", 4)
                contract["attributeSemantics"] = sorted(attrs)
                contract["attributeMetadata"] = {key: value["metadata"] for key, value in attrs.items()}
                contract["indexMetadata"] = indices["metadata"] if indices else None
                contract["targetMetadata"] = [{key: value["metadata"] for key, value in target.items()} for target in targets]
                primitives.append({"contract": contract, "geometry": {"attributes": attrs, "indices": indices, "targets": targets}})
            record["mesh"] = {"name": mesh.get("name"), "metadata": without(mesh, {"name", "primitives"}),
                              "primitives": primitives}
        result[node["name"]] = record
    require(used_meshes == set(range(len(meshes))), "Unreferenced mesh would escape per-node comparison")
    require(used_accessors == set(range(len(accessors))), "Unreferenced accessor would escape comparison")
    require(used_views == set(range(len(views))), "Unreferenced bufferView would escape comparison")
    return {"sha256": sha(raw), "bytes": len(raw), "names": names, "nodes": result,
            "documentMetadata": without(doc, LIBRARIES),
            "bufferMetadata": without(buffers[0], {"byteLength"})}


def compare(old, new):
    failures, changed, name_changes, details = [], [], [], []
    if old["names"] != new["names"]:
        failures.append("Node identities/order changed; nodes may not be added, removed or renumbered")
    for key in ("documentMetadata", "bufferMetadata"):
        if old[key] != new[key]:
            failures.append(key + " changed (includes scene, asset, licence and material data)")
    for name in sorted(set(old["nodes"]) | set(new["nodes"])):
        before, after = old["nodes"].get(name), new["nodes"].get(name)
        if before is None or after is None:
            failures.append(name + ": node added/removed")
            continue
        if without(before, {"mesh"}) != without(after, {"mesh"}):
            failures.append(name + ": node identity/parent/transform/extras changed")
        a, b = before["mesh"], after["mesh"]
        if a is None or b is None:
            if a != b:
                failures.append(name + ": mesh attachment changed")
            continue
        if a["name"] != b["name"]:
            name_changes.append({"node": name, "before": a["name"], "after": b["name"]})
        if a["metadata"] != b["metadata"] or len(a["primitives"]) != len(b["primitives"]):
            failures.append(name + ": mesh metadata or primitive count changed")
        contracts_preserved = [p["contract"] for p in a["primitives"]] == [p["contract"] for p in b["primitives"]]
        if not contracts_preserved:
            failures.append(name + ": primitive mode/material/attribute metadata changed")
        if [p["geometry"] for p in a["primitives"]] != [p["geometry"] for p in b["primitives"]]:
            changed.append(name)
            details.append({"node": name, "expected": name in EXPECTED,
                            "primitiveContractsPreserved": contracts_preserved,
                            "before": a["primitives"], "after": b["primitives"]})
            if name not in EXPECTED:
                failures.append(name + ": unrelated accessor bytes or descriptors changed")
    missing_expected = sorted(EXPECTED - set(changed))
    if missing_expected:
        failures.append("Expected household changes absent: " + ", ".join(missing_expected))
    return {"pass": not failures, "allowedGeometryNodes": sorted(EXPECTED), "changedGeometryNodes": changed,
            "nodeCount": len(new["nodes"]), "unchangedGeometryNodes": len(new["nodes"]) - len(changed),
            "metadataOnlyMeshNames": name_changes, "changes": details, "failures": failures,
            "comparison": "ordered accessor component bytes and descriptors; no sorting, float tolerance or winding normalization",
            "excludedPacking": "buffer offsets, strides and unreferenced padding bytes only",
            "nativeVisualAcceptance": "not-run"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("old_glb", type=Path)
    parser.add_argument("new_glb", type=Path)
    parser.add_argument("output_json", type=Path)
    args = parser.parse_args()
    paths = [args.old_glb.resolve(), args.new_glb.resolve()]
    output = args.output_json.resolve()
    require(output not in paths and not output.exists(), "Never overwrite an input or historical report")
    report = {"oldPath": str(paths[0]), "newPath": str(paths[1]), "pass": False}
    try:
        raw = [path.read_bytes() for path in paths]
        report.update(oldSha256=sha(raw[0]), newSha256=sha(raw[1]))
        report.update(compare(inspect_bytes(raw[0]), inspect_bytes(raw[1])))
        stable = all(sha(path.read_bytes()) == sha(data) for path, data in zip(paths, raw))
        report["inputsUnchangedDuringComparison"] = stable
        if not stable:
            report["failures"].append("An input changed while being compared")
            report["pass"] = False
    except (ValueError, KeyError, IndexError, TypeError, OSError, struct.error) as error:
        report.update({"pass": False, "error": f"{type(error).__name__}: {error}", "nativeVisualAcceptance": "not-run"})
    serialized = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    with output.open("x", encoding="utf-8") as handle:
        handle.write(serialized)
    print(json.dumps({key: value for key, value in report.items() if key != "changes"}, ensure_ascii=False))
    return 0 if report["pass"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
