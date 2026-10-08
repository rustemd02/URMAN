#!/usr/bin/env python3
"""VIS-108: deterministic, diffable structure receipt for generated GLB files.

Reads only the glTF JSON chunk (no Blender, no engine) and writes a sorted
summary: node names and parents, meshes with per-primitive material slot,
vertex and triangle counts, materials, skins and animation names. The same
generator input and seed must give the same receipt; a rebuild that changes
object names, counts or material slots shows up as a plain JSON diff.

Usage: tools/blender/glb_structure_receipt.py <file.glb> [...] [--out DIR] [--check]
  --check  compare with the stored receipt in DIR and exit 1 on any difference.
"""
import argparse
import json
import math
import itertools
import struct
import sys
from pathlib import Path


def gltf_json(path: Path) -> dict:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError(f"{path}: truncated GLB header")
    magic, version, _length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or _length != len(data):
        raise ValueError(f"{path}: not a glTF 2.0 binary")
    chunk_length, chunk_type = struct.unpack_from("<I4s", data, 12)
    if chunk_type != b"JSON" or chunk_length % 4 or 20 + chunk_length > len(data):
        raise ValueError(f"{path}: invalid JSON chunk")
    cursor = 20 + chunk_length
    while cursor < len(data):
        if cursor + 8 > len(data):
            raise ValueError(f"{path}: truncated chunk header")
        size, _kind = struct.unpack_from("<I4s", data, cursor)
        cursor += 8 + size
        if size % 4 or cursor > len(data):
            raise ValueError(f"{path}: invalid chunk length")
    doc = json.loads(data[20:20 + chunk_length])
    if not isinstance(doc, dict) or doc.get("asset", {}).get("version") != "2.0":
        raise ValueError(f"{path}: missing glTF 2.0 asset declaration")
    return doc


def item(values: list, index: int):
    if isinstance(index, bool) or not isinstance(index, int) or not 0 <= index < len(values):
        raise ValueError(f"invalid glTF index {index!r} for array of length {len(values)}")
    return values[index]


def triangle_count(primitive: dict, accessors: list) -> int:
    position = item(accessors, primitive["attributes"]["POSITION"])
    corners = item(accessors, primitive["indices"])["count"] if "indices" in primitive else position["count"]
    if isinstance(corners, bool) or not isinstance(corners, int) or corners < 0:
        raise ValueError("invalid primitive element count")
    mode = primitive.get("mode", 4)
    if mode == 4:
        if corners % 3:
            raise ValueError("triangle primitive count is not divisible by 3")
        return corners // 3
    if mode in (5, 6):
        return max(0, corners - 2)
    if mode in (0, 1, 2, 3):
        return 0
    raise ValueError(f"unknown primitive mode {mode}")


def node_matrix(node: dict) -> list:
    """glTF column-major matrix, converted to row-major for affine composition."""
    if "matrix" in node:
        if any(key in node for key in ("translation", "rotation", "scale")):
            raise ValueError("node declares both matrix and TRS")
        values = node["matrix"]
        if len(values) != 16 or not all(math.isfinite(v) for v in values):
            raise ValueError("invalid node matrix")
        if values[3:16:4] != [0, 0, 0, 1]:
            raise ValueError("node matrix is not affine")
        return [[values[c * 4 + r] for c in range(4)] for r in range(4)]
    t, q, s = node.get("translation", [0, 0, 0]), node.get("rotation", [0, 0, 0, 1]), node.get("scale", [1, 1, 1])
    if len(t) != 3 or len(q) != 4 or len(s) != 3 or not all(math.isfinite(v) for v in t + q + s):
        raise ValueError("invalid node TRS")
    if not math.isclose(sum(v * v for v in q), 1.0, abs_tol=1e-4):
        raise ValueError("node rotation is not a unit quaternion")
    x, y, z, w = q
    rotation = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    return [[rotation[r][c] * s[c] for c in range(3)] + [t[r]] for r in range(3)] + [[0, 0, 0, 1]]


def scene_geometry(doc: dict) -> tuple:
    """Default-scene instances and transformed POSITION AABB corners; no engine.

    Bounds are conservative after rotation. Skins/morphs/instancing are rejected
    rather than pretending their undeformed mesh bounds describe a static prop.
    """
    scenes, nodes, accessors = doc.get("scenes", []), doc.get("nodes", []), doc.get("accessors", [])
    if not scenes:
        raise ValueError("no scene: cannot determine the imported prop hierarchy")
    if "scene" not in doc and len(scenes) != 1:
        raise ValueError("multiple scenes without an explicit default scene")
    roots = item(scenes, doc.get("scene", 0)).get("nodes", [])
    points, primitives, visited = [], [], set()
    identity = [[int(r == c) for c in range(4)] for r in range(4)]

    def visit(index, parent):
        if index in visited:
            raise ValueError("cyclic or multiply parented node in the default scene")
        visited.add(index)
        node = item(nodes, index)
        local = node_matrix(node)
        world = [[sum(parent[r][k] * local[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
        if "skin" in node or "EXT_mesh_gpu_instancing" in node.get("extensions", {}):
            raise ValueError("skinned/instanced prop requires a baked static export")
        if "mesh" in node:
            for primitive in item(doc["meshes"], node["mesh"]).get("primitives", []):
                if primitive.get("targets"):
                    raise ValueError("morphed prop requires a baked static export")
                position = item(accessors, primitive["attributes"]["POSITION"])
                low, high = position.get("min", []), position.get("max", [])
                if position.get("type") != "VEC3" or position.get("count", 0) <= 0 or len(low) != 3 or len(high) != 3:
                    raise ValueError("empty POSITION or missing VEC3 bounds")
                if not all(math.isfinite(v) for v in low + high) or any(a > b for a, b in zip(low, high)):
                    raise ValueError("invalid POSITION bounds")
                for corner in itertools.product(*zip(low, high)):
                    point = [sum(world[r][c] * corner[c] for c in range(3)) + world[r][3] for r in range(3)]
                    if not all(math.isfinite(v) for v in point):
                        raise ValueError("non-finite transformed bounds")
                    points.append(point)
                primitives.append(primitive)
        for child in node.get("children", []):
            visit(child, world)

    for root in roots:
        visit(root, identity)
    if not primitives:
        raise ValueError("default scene has no mesh primitives")
    return ([min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)], primitives)


def receipt(path: Path) -> dict:
    doc = gltf_json(path)
    accessors = doc.get("accessors", [])
    materials = [m.get("name", f"material_{i}") for i, m in enumerate(doc.get("materials", []))]
    parents = {}
    source_nodes = doc.get("nodes", [])
    for index, node in enumerate(source_nodes):
        for child in node.get("children", []):
            item(source_nodes, child)
            if child in parents:
                raise ValueError(f"node {child} has multiple parent references")
            parents[child] = index
    for index in range(len(source_nodes)):
        chain = set()
        current = index
        while current in parents:
            if current in chain:
                raise ValueError(f"cyclic node hierarchy at {current}")
            chain.add(current)
            current = parents[current]
    names = [n.get("name", f"node_{i}") for i, n in enumerate(doc.get("nodes", []))]
    nodes = []
    for index, node in enumerate(doc.get("nodes", [])):
        entry = {"index": index, "name": names[index], "parent": names[parents[index]] if index in parents else None,
                 "parentIndex": parents.get(index), "children": node.get("children", []),
                 "transform": node_matrix(node)}
        if "mesh" in node:
            entry["mesh"] = item(doc["meshes"], node["mesh"]).get("name", f"mesh_{node['mesh']}")
        if "skin" in node:
            entry["skin"] = node["skin"]
        if node.get("extras"):
            # Runtime consumes values such as cloth_uv_units, not merely names.
            entry["extras"] = node["extras"]
        nodes.append(entry)
    meshes = []
    triangles_total = 0
    for index, mesh in enumerate(doc.get("meshes", [])):
        primitives = []
        for primitive in mesh.get("primitives", []):
            position = item(accessors, primitive["attributes"]["POSITION"])
            vertices = position["count"]
            triangles = triangle_count(primitive, accessors)
            triangles_total += triangles
            primitives.append({
                "material": item(materials, primitive["material"]) if "material" in primitive else None,
                "attributes": sorted(primitive["attributes"].keys()),
                "vertices": vertices,
                "triangles": triangles,
                "mode": primitive.get("mode", 4),
                "positionBounds": {key: position.get(key) for key in ("min", "max")},
            })
        meshes.append({"name": mesh.get("name", f"mesh_{index}"), "primitives": primitives})
    return {
        "schema": "urman.glb_structure_receipt.v2",
        "file": path.name,
        "generator": doc.get("asset", {}).get("generator"),
        "nodes": sorted(nodes, key=lambda n: (n["name"], n["parent"] or "")),
        "meshes": sorted(meshes, key=lambda m: m["name"]),
        "materials": sorted(materials),
        "skins": len(doc.get("skins", [])),
        "animations": sorted(a.get("name", "") for a in doc.get("animations", [])),
        "defaultScene": doc.get("scene"),
        "scenes": doc.get("scenes", []),
        "totals": {"nodes": len(nodes), "meshes": len(meshes), "materials": len(materials),
                   "triangles": triangles_total},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--out", type=Path, default=Path(__file__).resolve().parents[2] / "assets/source/blender/receipts")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    targets = [args.out / (path.stem + ".structure.json") for path in args.files]
    if len({target.resolve() for target in targets}) != len(targets):
        raise ValueError("input GLB basenames collide: use separate receipt directories")
    if not args.check:
        args.out.mkdir(parents=True, exist_ok=True)
    changed = 0
    for path, target in zip(args.files, targets):
        text = json.dumps(receipt(path), ensure_ascii=False, indent=1, sort_keys=True) + "\n"
        if args.check:
            if not target.is_file() or target.read_text(encoding="utf-8") != text:
                print(f"glb-structure: CHANGED {path} (receipt {target})")
                changed += 1
            else:
                print(f"glb-structure: same {path}")
        else:
            target.write_text(text, encoding="utf-8")
            print(f"glb-structure: wrote {target}")
    return 1 if changed else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, IndexError, TypeError) as exc:
        print(f"glb-structure: FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
