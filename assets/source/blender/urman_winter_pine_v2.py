#!/usr/bin/env python3
"""VIS-025 / VIS-084: rebuild the WinterPine hero silhouette as irregular masses.

The shipped `urman_winter_pine.glb` is a normalised Quaternius pine: its needle
cards sit in a small number of near-coplanar rings, which is what R025 reads as
"плоские повторяющиеся ярусы кроны". This script keeps every external contract
the runtime asserts and changes only the crown construction:

  * the same pinned source files and SHA-256 gates as `prepare_winter_pine.py`;
  * the same node names `WinterPine_LOD0/1/2` under the `URMAN_WinterPine` root,
    the same two material slots `AB_bark` / `AB_needles`, the same embedded
    `Leaf_Pine_C` alpha texture, the same normalisation (base z = 0, height = 1);
  * the same 462 needle triangles and the same bark-only decimation ratios, so
    the leaf/UV LOD contract and the triangle census do not drift.

What changes: the connected needle islands are re-seated into 3-5 uneven crown
masses with open gaps between them, per-mass heading, per-mass reach and
per-mass droop, and each island's own tip lift. Nothing is added or deleted, so
the cost is measurable against the v1 census; the silhouette stops repeating one
flat disc.

Run in Blender 4.5 LTS (this repository's prepared production step, not run by
the authoring agent):

    blender --background --python assets/source/blender/urman_winter_pine_v2.py -- --root .

Then verify with the existing capture pair P16 forest close / P16b medium and
the R025 framing, and record the printed census in
docs/production/visual_restyle_2026-10-07/ledger_TREES.md.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy


SOURCE_SHA256 = "54c72a4d614f053dd4b3d4404076a361de6085ec6a34560e42a76d7e79d89830"
SOURCE_BIN_SHA256 = "73edd69d7e789b12bfa32ab202c62d74ae98f0bba67975205211bf51906d8c82"
TEXTURE_SHA256 = {
    "Bark_NormalTree.png": "f36e94fdd73255347325b998e02a0b2575fdf714b0315bfe5e8ff34bc8e13603",
    "Bark_NormalTree_Normal.png": "852f6155404b8c39e100e41e26970e02a6a5c81b482d3726618dab28e3ad03a2",
    "Leaf_Pine_C.png": "86f7ff7a214f5424de48d269568f36a15485e61c80677e426c7be9f45181eef9",
}
EXPECTED_SOURCE_BARK_TRIANGLES = 4502
EXPECTED_SOURCE_LEAF_TRIANGLES = 462
ASSET_ID = "winter.pine.v2"
# Three to five masses. The count is chosen per seed inside this band; more
# masses would spend the crown budget on noise at 15 m, fewer would not read as
# a mass at all.
MASS_COUNTS = (3, 4, 5)


def parse_argv() -> tuple[Path, int]:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=20261008,
                        help="Deterministic crown composition seed. Same seed, same mesh bytes.")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    return args.root.resolve(), args.seed


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def triangle_count(mesh: bpy.types.Mesh) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)


def material_triangles(obj: bpy.types.Object, material_name: str) -> int:
    slots = {index for index, slot in enumerate(obj.data.materials)
             if slot is not None and slot.name == material_name}
    return sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons
               if poly.material_index in slots)


def validate_source(source_path: Path) -> str:
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    if sha256(source_path) != SOURCE_SHA256:
        raise RuntimeError(f"unexpected source SHA256 for {source_path}")
    document = json.loads(source_path.read_text(encoding="utf-8"))
    buffers = document.get("buffers", [])
    if len(buffers) != 1 or buffers[0].get("uri") != "Pine_3.bin":
        raise RuntimeError("Pine_3.gltf must reference exactly Pine_3.bin")
    if sha256(source_path.parent / buffers[0]["uri"]) != SOURCE_BIN_SHA256:
        raise RuntimeError("unexpected Pine_3.bin SHA256")
    referenced = {image.get("uri") for image in document.get("images", []) if image.get("uri")}
    if referenced != set(TEXTURE_SHA256):
        raise RuntimeError(f"Pine_3.gltf texture set changed: {sorted(referenced)}")
    for name, expected in TEXTURE_SHA256.items():
        if sha256(source_path.parent / name) != expected:
            raise RuntimeError(f"unexpected texture SHA256 for {name}")
    return ";".join(f"{name}={digest}" for name, digest in sorted(TEXTURE_SHA256.items()))


def needle_islands(obj: bpy.types.Object, material_name: str) -> list[list[int]]:
    """Connected components of one material's faces, welded by position.

    The glTF splits hard normals and UV seams, so vertices are welded only for
    selection. The source mesh, its UVs and its material channels stay intact;
    this returns groups of polygon indices that form one physical bough card.
    """
    mesh = obj.data
    slot = next(index for index, s in enumerate(mesh.materials)
                if s is not None and s.name == material_name)
    positions: dict[tuple, int] = {}
    per_face = []
    neighbors: dict[int, set[int]] = {}
    welds: dict[int, set[int]] = {}
    for poly in mesh.polygons:
        if poly.material_index != slot:
            continue
        welded = []
        for index in poly.vertices:
            key = tuple(round(c, 6) for c in mesh.vertices[index].co)
            vertex_id = positions.setdefault(key, len(positions))
            welded.append(vertex_id)
            welds.setdefault(vertex_id, set()).add(index)
        per_face.append((poly.index, welded))
        for a, b in zip(welded, welded[1:] + welded[:1]):
            neighbors.setdefault(a, set()).add(b)
            neighbors.setdefault(b, set()).add(a)
    face_of = {vertex: face for face, verts in per_face for vertex in verts}
    remaining = {vertex for vertex, _ in per_face}
    islands: list[list[int]] = []
    while remaining:
        pending, component = [next(iter(remaining))], set()
        while pending:
            vertex = pending.pop()
            if vertex in component:
                continue
            component.add(vertex)
            pending.extend(neighbors[vertex] - component)
        remaining -= component
        islands.append(sorted({face_of[vertex] for vertex in component}))
    return islands


def mass_schedule(index: int, height: float, seed: int):
    """Uneven crown masses: extents, heading, reach, droop and gap share."""
    rng = __import__("random").Random(seed * 7919 + index)
    mass_count = MASS_COUNTS[(index + seed) % len(MASS_COUNTS)]
    weights = [rng.uniform(0.55, 1.55) for _ in range(mass_count)]
    total = sum(weights)
    extents = [weight / total for weight in weights]
    gap = 0.045 + 0.03 * rng.random()
    usable = 1.0 - gap * (mass_count - 1)
    masses, walked = [], 0.0
    for mass in range(mass_count):
        extent = extents[mass] * usable
        masses.append({
            "base": mass / mass_count,
            "start": walked,
            "end": walked + extent,
            "heading": rng.uniform(0.0, math.tau),
            "reach": rng.uniform(0.80, 1.14),
            "droop": rng.uniform(-0.16, 0.10),
            "lift": rng.uniform(0.04, 0.16),
        })
        walked += extent + gap
    return masses


def reshape_crown(obj: bpy.types.Object, seed: int) -> dict:
    """Re-seat every needle island into one of the authored crown masses."""
    mesh = obj.data
    height = max(vertex.co.z for vertex in mesh.vertices)
    base = min(vertex.co.z for vertex in mesh.vertices)
    span = max(height - base, 1e-6)
    islands = needle_islands(obj, "AB_needles")
    if not islands:
        raise RuntimeError("no AB_needles islands found; the crown contract changed")
    masses = mass_schedule(seed, height, seed)
    moved = 0
    for island_index, faces in enumerate(islands):
        centre = [0.0, 0.0, 0.0]
        count = 0
        for poly_index in faces:
            for vertex_index in mesh.polygons[poly_index].vertices:
                point = mesh.vertices[vertex_index].co
                centre[0] += point.x
                centre[1] += point.y
                centre[2] += point.z
                count += 1
        centre = [value / max(count, 1) for value in centre]
        # The island's own normalised height decides which mass it belongs to,
        # so the re-arrangement is a re-reading of the existing ladder rather
        # than a random scatter of cards.
        level = (centre[2] - base) / span
        band = 1.0 / len(masses)
        mass_id = min(len(masses) - 1, int(level / band))
        mass = masses[mass_id]
        # Position inside that mass, 0..1.
        local = (level - mass_id * band) / band
        # Seat the card inside its mass: mass centre of extent, uneven radius
        # from the mass heading, mass reach plus this card's own share of the
        # mass, then the mass droop and tip lift.
        target = (mass["start"] + mass["end"]) * 0.5 * span + base
        rise = (local - 0.5) * (mass["end"] - mass["start"]) * span * 0.8
        angle = mass["heading"] + local * 2.39996
        radius = math.hypot(centre[0], centre[1])
        reach = mass["reach"] * (1.0 + 0.18 * math.sin(local * math.pi))
        shift = [
            math.cos(angle) * radius * (reach - 1.0) ,
            math.sin(angle) * radius * (reach - 1.0),
            target + rise - centre[2] + mass["droop"] * span * 0.05,
        ]
        for poly_index in faces:
            for vertex_index in mesh.polygons[poly_index].vertices:
                vertex = mesh.vertices[vertex_index]
                point = vertex.co
                tilt = mass["lift"] * 0.5
                # A small pitch about the card's own radial axis is what stops
                # two cards in one mass from sharing a plane.
                radial = Vector2D(point.x, point.y)
                turned = radial.rotated(tilt * (1.0 if radial.x >= 0 else -1.0))
                vertex.co = (
                    point.x + (turned.x - radial.x) + shift[0],
                    point.y + (turned.y - radial.y) + shift[1],
                    point.z + shift[2],
                )
        moved += len(faces)
    mesh.update()
    return {
        "needle_islands": len(islands),
        "needle_faces_moved": moved,
        "mass_count": len(masses),
        "mass_extents": [round(mass["end"] - mass["start"], 4) for mass in masses],
        "mass_headings": [round(mass["heading"] % math.tau, 4) for mass in masses],
        "mass_reach": [round(mass["reach"], 4) for mass in masses],
    }


class Vector2D:
    """Minimal 2D helper so the card tilt needs no extra dependency."""

    __slots__ = ("x", "y")

    def __init__(self, x: float, y: float):
        self.x, self.y = x, y

    def rotated(self, angle: float) -> "Vector2D":
        cosine, sine = math.cos(angle), math.sin(angle)
        return Vector2D(self.x * cosine - self.y * sine, self.x * sine + self.y * cosine)


def make_lod(source: bpy.types.Object, name: str, ratio: float) -> bpy.types.Object:
    """Decimate bark only; needle triangles and their UVs stay unchanged."""
    expected_leaf = material_triangles(source, "AB_needles")
    result = source.copy()
    result.data = source.data.copy()
    result.name = name
    source.users_collection[0].objects.link(result)
    before = set(bpy.context.scene.objects)
    bpy.ops.object.select_all(action="DESELECT")
    result.select_set(True)
    bpy.context.view_layer.objects.active = result
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [result, *(obj for obj in set(bpy.context.scene.objects) - before if obj.type == "MESH")]
    bark = next((obj for obj in parts if material_triangles(obj, "AB_bark") > 0), None)
    leaves = next((obj for obj in parts if material_triangles(obj, "AB_needles") > 0), None)
    if bark is None or leaves is None or bark is leaves:
        raise RuntimeError(f"{name} material split did not produce bark and needle parts")
    bpy.ops.object.select_all(action="DESELECT")
    bark.select_set(True)
    bpy.context.view_layer.objects.active = bark
    modifier = bark.modifiers.new(f"{name}_BarkDecimate", "DECIMATE")
    modifier.ratio = ratio
    if hasattr(modifier, "use_collapse_triangulate"):
        modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.select_all(action="DESELECT")
    bark.select_set(True)
    leaves.select_set(True)
    bpy.context.view_layer.objects.active = bark
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    if material_triangles(result, "AB_needles") != expected_leaf:
        raise RuntimeError(f"{name} changed the needle triangle contract")
    return result


def main() -> None:
    root, seed = parse_argv()
    source_path = root / "assets/source/quaternius/stylized_nature/Pine_3.gltf"
    blend_path = root / "assets/source/blender/urman_winter_pine.blend"
    glb_path = root / "game/assets/models/act1/urman_winter_pine.glb"
    manifest_path = root / "game/assets/models/act1/urman_winter_pine_v2_manifest.json"
    texture_manifest = validate_source(source_path)
    leaf_path = source_path.with_name("Leaf_Pine_C.png")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for group in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                  bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for block in list(group):
            group.remove(block, do_unlink=True)

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in set(bpy.context.scene.objects) - before if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"expected one imported mesh, found {len(meshes)}")
    source = meshes[0]
    world = source.matrix_world.copy()
    source.parent = None
    source.matrix_world = world
    for obj in list(bpy.context.scene.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)

    bark_slot = next(index for index, slot in enumerate(source.data.materials)
                     if slot is not None and slot.name == "Bark_NormalTree")
    leaf_slot = next(index for index, slot in enumerate(source.data.materials)
                     if slot is not None and slot.name == "Leaves_Pine")
    if material_triangles(source, "Bark_NormalTree") != EXPECTED_SOURCE_BARK_TRIANGLES:
        raise RuntimeError("Pine_3 bark geometry contract changed")
    if material_triangles(source, "Leaves_Pine") != EXPECTED_SOURCE_LEAF_TRIANGLES:
        raise RuntimeError("Pine_3 leaf geometry contract changed")

    bark = bpy.data.materials.new("AB_bark")
    bark.use_nodes = True
    nodes, links = bark.node_tree.nodes, bark.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    # VIS-082: cold grey-brown conifer bark, no red-brown baseline.
    shader.inputs["Base Color"].default_value = (0.30, 0.26, 0.225, 1.0)
    shader.inputs["Roughness"].default_value = 0.93
    shader.inputs["Metallic"].default_value = 0.0
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    leaves = bpy.data.materials.new("AB_needles")
    leaves.use_nodes = True
    leaves.use_backface_culling = False
    try:
        leaves.surface_render_method = "DITHERED"
    except (AttributeError, TypeError):
        pass
    leaf_image = bpy.data.images.load(str(leaf_path), check_existing=False)
    leaf_image.pack()
    leaf_nodes, leaf_links = leaves.node_tree.nodes, leaves.node_tree.links
    leaf_nodes.clear()
    leaf_output = leaf_nodes.new("ShaderNodeOutputMaterial")
    leaf_shader = leaf_nodes.new("ShaderNodeBsdfPrincipled")
    leaf_texture = leaf_nodes.new("ShaderNodeTexImage")
    leaf_texture.image = leaf_image
    alpha_clip = leaf_nodes.new("ShaderNodeMath")
    alpha_clip.operation = "GREATER_THAN"
    alpha_clip.inputs[1].default_value = 0.2
    leaf_shader.inputs["Roughness"].default_value = 0.9
    leaf_links.new(leaf_texture.outputs["Color"], leaf_shader.inputs["Base Color"])
    leaf_links.new(leaf_texture.outputs["Alpha"], alpha_clip.inputs[0])
    leaf_links.new(alpha_clip.outputs[0], leaf_shader.inputs["Alpha"])
    leaf_links.new(leaf_shader.outputs["BSDF"], leaf_output.inputs["Surface"])

    original_indices = [poly.material_index for poly in source.data.polygons]
    source.data.materials.clear()
    source.data.materials.append(bark)
    source.data.materials.append(leaves)
    for polygon, material_index in zip(source.data.polygons, original_indices):
        if material_index == bark_slot:
            polygon.material_index = 0
        elif material_index == leaf_slot:
            polygon.material_index = 1
        else:
            raise RuntimeError(f"unexpected Pine_3 material index {material_index}")

    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low = min(vertex.co.z for vertex in source.data.vertices)
    high = max(vertex.co.z for vertex in source.data.vertices)
    raw_height = high - low
    if raw_height <= 0.001:
        raise RuntimeError("Pine_3 has no usable vertical extent")
    scale = 1.0 / raw_height
    for vertex in source.data.vertices:
        vertex.co.x *= scale
        vertex.co.y *= scale
        vertex.co.z = (vertex.co.z - low) * scale
    source.data.update()
    source.name = "WinterPine_LOD0"

    census = reshape_crown(source, seed)
    if material_triangles(source, "AB_needles") != EXPECTED_SOURCE_LEAF_TRIANGLES:
        raise RuntimeError("the crown re-seat changed the 462 needle triangle contract")

    lod1 = make_lod(source, "WinterPine_LOD1", 0.5)
    lod2 = make_lod(source, "WinterPine_LOD2", 0.2)
    objects = (source, lod1, lod2)
    counts = [triangle_count(obj.data) for obj in objects]

    asset_root = bpy.data.objects.new("URMAN_WinterPine", None)
    source.users_collection[0].objects.link(asset_root)
    for obj in objects:
        obj.parent = asset_root
    for obj, level in zip(objects, range(3)):
        for key, value in {
            "asset_id": ASSET_ID,
            "source_sha256": SOURCE_SHA256,
            "source_bin_sha256": SOURCE_BIN_SHA256,
            "license": "CC0 1.0 Universal",
            "collision": "none",
            "lod_level": level,
            "lod_source": "WinterPine_LOD0" if level else "",
        }.items():
            obj[key] = value
    for key, value in {
        "asset_id": ASSET_ID,
        "source_asset": "assets/source/quaternius/stylized_nature/Pine_3.gltf",
        "source_sha256": SOURCE_SHA256,
        "source_bin_sha256": SOURCE_BIN_SHA256,
        "source_texture_manifest": texture_manifest,
        "source_license": "CC0 1.0 Universal",
        "generator": "urman_winter_pine_v2.py",
        "crown_seed": seed,
        "normalized_base_z_m": 0.0,
        "normalized_height_m": 1.0,
        "raw_source_height_m": raw_height,
        "lod0_triangles": counts[0],
        "lod1_triangles": counts[1],
        "lod2_triangles": counts[2],
        "lod_policy": "LOD0 0-26m; LOD1 24-64m; LOD2 60m+",
        "leaf_triangles_lod0": EXPECTED_SOURCE_LEAF_TRIANGLES,
        "leaf_triangles_lod1": material_triangles(lod1, "AB_needles"),
        "leaf_triangles_lod2": material_triangles(lod2, "AB_needles"),
        "leaf_lod_policy": "all LODs retain the original 462 AB_needles triangles; bark-only decimation",
        "crown_policy": ("3-5 uneven needle masses with open gaps, per-mass heading, reach and "
                         "droop; no repeated coplanar tier (VIS-025)"),
        "census": json.dumps(census, sort_keys=True),
        "material_policy": "AB_bark opaque + AB_needles MASK .2 double-sided; Leaf_Pine_C embedded",
        "collision_policy": "none; existing Agent B terrain/road guards own traversal",
    }.items():
        asset_root[key] = value
    bpy.context.scene["generator"] = "urman_winter_pine_v2.py"
    bpy.context.scene["asset_id"] = ASSET_ID
    bpy.context.scene["source_sha256"] = SOURCE_SHA256
    bpy.context.scene["crown_seed"] = seed

    for old in list(bpy.data.materials):
        if old not in (bark, leaves):
            bpy.data.materials.remove(old, do_unlink=True)
    for image in list(bpy.data.images):
        if image != leaf_image:
            bpy.data.images.remove(image, do_unlink=True)

    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (asset_root, *objects):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = asset_root
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_materials="EXPORT",
        export_yup=True,
        export_cameras=False,
        export_lights=False,
    )
    manifest_path.write_text(json.dumps({
        "asset_id": ASSET_ID,
        "generator": "assets/source/blender/urman_winter_pine_v2.py",
        "command": f"blender --background --python assets/source/blender/urman_winter_pine_v2.py -- --root . --seed {seed}",
        "crown_seed": seed,
        "source": {"gltf_sha256": SOURCE_SHA256, "bin_sha256": SOURCE_BIN_SHA256,
                   "textures": TEXTURE_SHA256},
        "outputs": {"blend": str(blend_path.relative_to(root)),
                    "glb": str(glb_path.relative_to(root))},
        "triangle_census": {"lod0": counts[0], "lod1": counts[1], "lod2": counts[2],
                            "needles": EXPECTED_SOURCE_LEAF_TRIANGLES},
        "crown": census,
        "glb_sha256": sha256(glb_path),
        "contracts_kept": ["WinterPine_LOD0/1/2 node names", "AB_bark + AB_needles slots",
                           "base z = 0, height = 1 normalisation", "462 needle triangles",
                           "no collision"],
        "verification": "P16 forest close + P16b medium + R025 before/after pair",
    }, indent=1, sort_keys=True), encoding="utf-8")

    print(f"winter-pine-v2: lod0={counts[0]} lod1={counts[1]} lod2={counts[2]} "
          f"islands={census['needle_islands']} masses={census['mass_count']} "
          f"raw_height={raw_height:.6f} normalized=1.0 base_z=0.0")
    print(f"winter-pine-v2: blend={blend_path} glb={glb_path} manifest={manifest_path}")


if __name__ == "__main__":
    main()
