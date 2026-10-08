#!/usr/bin/env python3
"""VIS-106: ingest checklist for a downloaded prop GLB (static, no engine, no Blender).

Checks, per file:
  1 provenance   an assets/asset_registry.json record with externalAsset=true whose
                 "derived" path is this file (licence/source/consumer gate: VIS-107)
  2 scale        transformed default-scene AABB extent within --min/--max metres
  3 pivot        transformed lowest bound within 5 cm of origin height (conservative)
  4 uv           every primitive has matching VEC2 TEXCOORD_0
  5 slots        <= --max-slots (default 4); unique named slots, all primitives assigned
  6 lights       no KHR_lights_punctual
  7 cameras      no cameras
  8 animations   none unless --allow-animation
  9 triangles    <= --max-triangles (default 6000), counting scene instances/strips/fans
 10 textures     embedded images <= 2048 px is not checked here (import size_limit owns it)
 11 rebind       every slot maps to rrggbb|surface or rrggbb|rural:kind from runtime code
Prints one line per check and exits 1 on any FAIL.
"""
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/blender"))
from glb_structure_receipt import gltf_json, item, scene_geometry, triangle_count


def provenance(record: dict, glb: Path) -> list:
    """Selected-file counterpart of VIS-107; never rewrites source hashes."""
    errors = []
    if record.get("externalAsset") is not True:
        errors.append("externalAsset must be true")
    if record.get("licenseSpdx") not in {"CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0", "OFL-1.1", "MIT", "Apache-2.0"}:
        errors.append("missing/unsupported licenseSpdx")
    for field in ("sourceUrl", "sourceLicenseUrl"):
        value = record.get(field)
        if not isinstance(value, str) or urlsplit(value).scheme != "https" or not urlsplit(value).hostname:
            errors.append(f"{field} must be an HTTPS URL")
    for field in ("sourceAuthor", "modification"):
        if not isinstance(record.get(field), str) or not record[field].strip():
            errors.append(f"missing {field}")

    def repo_file(value):
        if not isinstance(value, str) or not value or Path(value).is_absolute():
            return None
        path = (ROOT / value).resolve()
        return path if path.is_relative_to(ROOT) and path.is_file() else None

    consumers = record.get("consumerPaths")
    if not isinstance(consumers, list) or not consumers or any(repo_file(p) is None for p in consumers):
        errors.append("missing/unsafe consumerPaths")
    if not re.fullmatch(r"[0-9a-fA-F]{64}", str(record.get("sourceSha256", ""))):
        errors.append("missing original-download SHA256")
    original = repo_file(record.get("source"))
    source = record.get("source")
    if isinstance(source, str) and source.startswith(("assets/", "game/", "tools/")) and original is None:
        errors.append("missing/unsafe original source file")
    if original and hashlib.sha256(original.read_bytes()).hexdigest() != str(record.get("sourceSha256", "")).lower():
        errors.append("original source hash mismatch")
    if hashlib.sha256(glb.read_bytes()).hexdigest() != str(record.get("derivedSha256", "")).lower():
        errors.append("derived GLB hash mismatch")
    if record.get("integrated") is True and repo_file(record.get("receiverCapture")) is None:
        errors.append("integrated prop lacks receiver capture")
    return errors


def material_families() -> tuple:
    # Read explicit runtime declarations so a misspelled family cannot silently
    # use either library's fallback. Fail if declarations move instead of guessing.
    source = (ROOT / "game/scripts/PainterlyMaterialLibrary.cs").read_text(encoding="utf-8")
    families = set()
    for declaration in ("SurfaceTextures", "TunedWithoutMap", "FlatByDesign"):
        block = re.search(r"\b" + declaration + r"\s*=.*?\{(.*?)\n    \};", source, re.S)
        if block is None:
            raise ValueError(f"cannot locate runtime declaration {declaration}")
        if declaration == "SurfaceTextures":
            families.update(re.findall(r'\["([^"\n]+)"\]\s*=', block[1]))
        else:
            families.update(re.findall(r'"([^"\n]+)"', block[1]))
    rural = (ROOT / "game/scripts/RuralPropMaterials.cs").read_text(encoding="utf-8")
    return families, set(re.findall(r'"([^"\n]+)"\s*=>', rural))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("glb", type=Path)
    parser.add_argument("--min", type=float, default=.02)
    parser.add_argument("--max", type=float, default=12.0)
    parser.add_argument("--max-slots", type=int, default=4)
    parser.add_argument("--max-triangles", type=int, default=6000)
    parser.add_argument("--allow-animation", action="store_true")
    parser.add_argument("--rebind", type=Path, help="JSON {source material name: 'hex|urman_surface'}")
    args = parser.parse_args()
    if not all(math.isfinite(v) for v in (args.min, args.max)) or not 0 < args.min <= args.max:
        parser.error("--min/--max must be finite positive metre bounds with min <= max")
    if args.max_slots < 1 or args.max_triangles < 1:
        parser.error("budgets must be positive")
    doc = gltf_json(args.glb)
    results = []

    def check(name, ok, detail):
        results.append(ok)
        print(f"{'PASS' if ok else 'FAIL'} {name}: {detail}")

    registry = json.loads((ROOT / "assets/asset_registry.json").read_text(encoding="utf-8"))
    rel = args.glb.resolve().relative_to(ROOT).as_posix() if args.glb.resolve().is_relative_to(ROOT) else str(args.glb)
    records = [a for a in registry.get("assets", []) if a.get("derived") == rel]
    errors = provenance(records[0], args.glb) if len(records) == 1 else [f"expected one registry record, got {len(records)} for {rel}"]
    check("provenance", not errors, "; ".join(errors) if errors else f"complete selected-file provenance for {rel}")

    accessors = doc.get("accessors", [])
    low, high, primitives = scene_geometry(doc)
    extent = max(high[i] - low[i] for i in range(3))
    check("scale", args.min <= extent <= args.max, f"transformed scene AABB extent {extent:.3f} m (conservative)")
    check("pivot", abs(low[1]) <= .05, f"transformed lowest bound {low[1]:.3f} m from the origin height")
    triangles, uv_invalid = 0, 0
    for primitive in primitives:
        triangles += triangle_count(primitive, accessors)
        position = item(accessors, primitive["attributes"]["POSITION"])
        uv_index = primitive["attributes"].get("TEXCOORD_0")
        uv = item(accessors, uv_index) if uv_index is not None else {}
        uv_invalid += uv.get("type") != "VEC2" or uv.get("count") != position["count"]
    check("uv", uv_invalid == 0, f"{uv_invalid} primitive instance(s) without matching VEC2 TEXCOORD_0")
    materials = [m.get("name") for m in doc.get("materials", [])]
    slots_valid = all(isinstance(m, str) and m.strip() for m in materials) and len(set(materials)) == len(materials)
    assigned = all(isinstance(p.get("material"), int) and 0 <= p["material"] < len(materials) for p in primitives)
    check("slots", 0 < len(materials) <= args.max_slots and slots_valid and assigned, f"{len(materials)} named unique assigned material slot(s): {materials}")
    lights = "KHR_lights_punctual" in doc.get("extensionsUsed", []) or "KHR_lights_punctual" in doc.get("extensions", {}) or any("KHR_lights_punctual" in n.get("extensions", {}) for n in doc.get("nodes", []))
    check("lights", not lights, "embedded punctual lights (declarations and node references)")
    check("cameras", not doc.get("cameras"), f"{len(doc.get('cameras', []))} camera(s)")
    animations = doc.get("animations", [])
    check("animations", args.allow_animation or not animations, f"{len(animations)} animation(s)")
    check("triangles", 0 < triangles <= args.max_triangles and all(p.get("mode", 4) in (4, 5, 6) for p in primitives), f"{triangles} instanced triangles incl. strips/fans (budget {args.max_triangles})")
    if args.rebind:
        mapping = json.loads(args.rebind.read_text(encoding="utf-8"))
        if not isinstance(mapping, dict):
            raise ValueError("rebind map must be an object")
        painterly, rural = material_families()
        invalid = []
        for name in materials:
            value = mapping.get(name)
            match = re.fullmatch(r"([0-9a-fA-F]{6})\|(?:(rural):)?([a-z0-9_]+)", value) if isinstance(value, str) else None
            if not match or match[3] not in (rural if match[2] else painterly):
                invalid.append(name)
        check("rebind", not invalid, f"missing/invalid bindings: {invalid}" if invalid else "every slot has a valid hex colour and explicit runtime family")
    else:
        check("rebind", False, "no --rebind map: the source look would be accepted as is")
    return 0 if all(results) else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, IndexError, TypeError) as exc:
        print(f"FAIL input: {exc}", file=sys.stderr)
        sys.exit(1)
