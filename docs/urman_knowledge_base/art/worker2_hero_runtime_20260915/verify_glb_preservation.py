"""Compare existing GLB nodes and actual accessor bytes after a bounded export."""
import hashlib
import json
from pathlib import Path
import struct
import sys


def records(path: str) -> dict:
    data = Path(path).read_bytes()
    offset, chunks = 12, {}
    while offset < len(data):
        size, kind = struct.unpack_from("<II", data, offset)
        chunks[kind] = data[offset + 8:offset + 8 + size]
        offset += 8 + size
    doc, binary = json.loads(chunks[0x4E4F534A]), chunks[0x004E4942]

    def accessor(index: int) -> tuple:
        value = doc["accessors"][index]
        view = doc["bufferViews"][value["bufferView"]]
        start = view.get("byteOffset", 0) + value.get("byteOffset", 0)
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[value["type"]]
        width *= {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[value["componentType"]]
        stride = view.get("byteStride", width)
        content = b"".join(binary[start + i * stride:start + i * stride + width] for i in range(value["count"]))
        return value["type"], value["componentType"], value["count"], hashlib.sha256(content).hexdigest()

    result = {}
    for node in doc["nodes"]:
        record = {key: value for key, value in node.items() if key not in ("mesh", "children")}
        # This is the only permitted change to an old parent's child list.
        record["children"] = sorted(doc["nodes"][i].get("name", "") for i in node.get("children", [])
                                    if not doc["nodes"][i].get("name", "").startswith("HeroYardShed"))
        if "mesh" in node:
            record["mesh"] = [
                {"attributes": {key: accessor(index) for key, index in primitive["attributes"].items()},
                 "indices": accessor(primitive["indices"]) if "indices" in primitive else None,
                 "material": doc["materials"][primitive["material"]].get("name") if "material" in primitive else None}
                for primitive in doc["meshes"][node["mesh"]]["primitives"]
            ]
        result[node.get("name", "")] = record
    return result


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: verify_glb_preservation.py BEFORE.glb AFTER.glb")
    previous, current = (records(path) for path in sys.argv[1:])
    missing = sorted(set(previous) - set(current))
    changed = [name for name in previous if name in current and previous[name] != current[name]]
    print(json.dumps({"before": len(previous), "after": len(current), "preserved": len(previous) - len(missing) - len(changed),
                      "missing": missing, "changed": changed, "added": len(set(current) - set(previous))}))
    raise SystemExit(1 if missing or changed else 0)
