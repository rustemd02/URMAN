#!/usr/bin/env python3
"""VIS-025 / VIS-084: rebuild WinterPine with irregular volumetric boughs.

The shipped `urman_winter_pine.glb` is a normalised Quaternius pine: its needle
cards sit in a small number of near-coplanar rings, which is what R025 reads as
"плоские повторяющиеся ярусы кроны". This script keeps every external contract
the runtime asserts and changes only the crown construction. It starts from an
immutable production GLB pinned below so bark seating and positions are exact;
the original CC0 source is still hash- and geometry-validated:

  * the same pinned source files and SHA-256 gates as `prepare_winter_pine.py`;
  * the same node names `WinterPine_LOD0/1/2` under the `URMAN_WinterPine` root,
    the same two material slots `AB_bark` / `AB_needles`, the same embedded
    `Leaf_Pine_C` alpha texture, the same normalisation (base z = 0, height = 1);
  * the same node/LOD, material, placement-envelope and bark-decimation contracts.

What changes: the source needle cards are replaced by twenty closed, smoothly
shaded bough volumes and one narrow central leader. Their staggered heights,
unequal reach and gentle droop make one organic asymmetric crown instead of
repeating hard triangular card tiers. The foliage UV samples an opaque point in
the pinned alpha texture, preserving the existing runtime cutout material ABI.

Run in Blender 4.5 LTS:

    blender --background --python assets/source/blender/urman_winter_pine_v2.py -- --root .

Then verify with matched Windows C1/C2 daylight captures and LOD motion review.
"""
from __future__ import annotations

import argparse
import bmesh
import hashlib
import json
import math
import random
import subprocess
import sys
import tempfile
from pathlib import Path

import bpy
from mathutils import Vector


SOURCE_SHA256 = "54c72a4d614f053dd4b3d4404076a361de6085ec6a34560e42a76d7e79d89830"
SOURCE_BIN_SHA256 = "73edd69d7e789b12bfa32ab202c62d74ae98f0bba67975205211bf51906d8c82"
BASELINE_REVISION = "cf63e6a9cbbf280d1ad56755936ea00345e94733"
BASELINE_GLB_PATH = "game/assets/models/act1/urman_winter_pine.glb"
BASELINE_GLB_SHA256 = "6b36723a8a38fa37e44852f1c16b42b5dbc1a539d10e73cdd5c676daa705db38"
TEXTURE_SHA256 = {
    "Bark_NormalTree.png": "f36e94fdd73255347325b998e02a0b2575fdf714b0315bfe5e8ff34bc8e13603",
    "Bark_NormalTree_Normal.png": "852f6155404b8c39e100e41e26970e02a6a5c81b482d3726618dab28e3ad03a2",
    "Leaf_Pine_C.png": "86f7ff7a214f5424de48d269568f36a15485e61c80677e426c7be9f45181eef9",
}
EXPECTED_SOURCE_BARK_TRIANGLES = 4502
EXPECTED_SOURCE_LEAF_TRIANGLES = 462
EXPECTED_BASELINE_BARK_TRIANGLES = 3926
EXPECTED_BASELINE_LEAF_TRIANGLES = 462
EXPECTED_CROWN_TRIANGLES = 2692
ASSET_ID = "winter.pine.v2"
# Blender-space bounds converted from the current production GLB's AB_needles
# POSITION envelope (glTF Y-up maps to Blender Z-up with a sign flip on Y).
REFERENCE_CROWN_BOUNDS = ((-0.28594157, 0.20259976), (-0.23044090, 0.31010240),
                          (0.46301207, 1.0))
BOUGH_PROFILE = ((0.0, 0.038, 0.034), (0.16, 0.079, 0.060),
                 (0.36, 0.091, 0.057), (0.60, 0.079, 0.046),
                 (0.82, 0.054, 0.031), (1.0, 0.016, 0.012))


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


def add_closed_branch(bm: bmesh.types.BMesh, angle: float, base_z: float,
                      length: float, width: float, height: float, droop: float,
                      lateral: float, material_index: int, uv_layer) -> tuple[list, list]:
    """Build one closed, rounded, swept foliage volume from six profile rings."""
    radial = Vector((math.cos(angle), math.sin(angle), 0.0))
    side = Vector((-math.sin(angle), math.cos(angle), 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rings, vertices, faces = [], [], []
    segments = 10
    for t, profile_width, profile_height in BOUGH_PROFILE:
        # The narrow first ring starts inside the central foliage core, so every
        # bough visibly grows out of the trunk instead of floating beside it.
        centre = radial * (length * t) + side * (lateral * math.sin(math.pi * t))
        centre.z = base_z + 0.040 * math.sin(math.pi * t) - droop * t * t
        ring = []
        for segment in range(segments):
            a = math.tau * segment / segments
            point = centre + side * (math.cos(a) * width * profile_width / 0.091)
            vertical = math.sin(a)
            # The underside stays shallow while the upper side holds a round
            # snow-bearing arc instead of a symmetric rubbery oval.
            vertical *= 1.0 if vertical >= 0.0 else 0.62
            vertical += 0.14 * (1.0 - math.cos(a))
            point += up * (vertical * height * profile_height / 0.060)
            vertex = bm.verts.new(point)
            ring.append(vertex)
            vertices.append(vertex)
        rings.append(ring)
    for lower, upper in zip(rings, rings[1:]):
        for segment in range(segments):
            faces.append(bm.faces.new((lower[segment], lower[(segment + 1) % segments],
                                       upper[(segment + 1) % segments], upper[segment])))
    for ring, reverse in ((rings[0], True), (rings[-1], False)):
        centre = bm.verts.new(sum((vertex.co for vertex in ring), Vector()) / segments)
        vertices.append(centre)
        for segment in range(segments):
            pair = (ring[(segment + 1) % segments], ring[segment]) if reverse else (ring[segment], ring[(segment + 1) % segments])
            faces.append(bm.faces.new((centre, *pair)))
    for face in faces:
        face.material_index = material_index
        face.smooth = True
        for loop in face.loops:
            loop[uv_layer].uv = (0.5, 0.5)
    return faces, vertices


def add_central_core(bm: bmesh.types.BMesh, material_index: int, uv_layer) -> tuple[list, list]:
    """Create a subtly irregular foliage spine that joins all radial branch fans."""
    profile = ((0.465, 0.006, 0.000, 0.000), (0.515, 0.039, 0.004, -0.004),
               (0.585, 0.068, 0.009, -0.001), (0.665, 0.082, 0.014, 0.005),
               (0.745, 0.073, 0.020, 0.008), (0.825, 0.052, 0.024, 0.003),
               (0.910, 0.026, 0.020, -0.002), (0.982, 0.004, 0.012, -0.004))
    segments = 12
    loops, vertices, faces = [], [], []
    for ring_index, (z, radius, lean_x, lean_y) in enumerate(profile):
        loop = []
        centre = Vector((lean_x, lean_y, z))
        for segment in range(segments):
            angle = math.tau * segment / segments
            irregularity = (1.0 + 0.055 * math.sin(3.0 * angle + ring_index * 0.83)
                            + 0.035 * math.cos(5.0 * angle - ring_index * 0.61))
            vertex = bm.verts.new(centre + Vector((math.cos(angle) * radius * irregularity,
                                                    math.sin(angle) * radius * irregularity * 0.92,
                                                    0.0)))
            loop.append(vertex)
            vertices.append(vertex)
        loops.append(loop)
    for lower, upper in zip(loops, loops[1:]):
        for segment in range(segments):
            faces.append(bm.faces.new((lower[segment], lower[(segment + 1) % segments],
                                       upper[(segment + 1) % segments], upper[segment])))
    for ring, reverse in ((loops[0], True), (loops[-1], False)):
        centre = bm.verts.new(sum((vertex.co for vertex in ring), Vector()) / segments)
        vertices.append(centre)
        for segment in range(segments):
            pair = (ring[(segment + 1) % segments], ring[segment]) if reverse else (ring[segment], ring[(segment + 1) % segments])
            faces.append(bm.faces.new((centre, *pair)))
    for face in faces:
        face.material_index = material_index
        face.smooth = True
        for loop in face.loops:
            loop[uv_layer].uv = (0.5, 0.5)
    return faces, vertices


def add_central_leader(bm: bmesh.types.BMesh, material_index: int, uv_layer) -> tuple[list, list]:
    """Make a slim, subtly leaning terminal shoot that completes the silhouette."""
    rings = ((0.0, 0.72, 0.050), (0.24, 0.79, 0.045), (0.51, 0.87, 0.032),
             (0.78, 0.95, 0.017), (1.0, 1.0, 0.001))
    segments = 10
    built, vertices, loops = [], [], []
    for lean, z, radius in rings:
        centre = Vector((0.012 + lean * 0.018, -0.012 + lean * 0.010, z))
        loop = []
        for i in range(segments):
            vertex = bm.verts.new(centre + Vector((math.cos(math.tau * i / segments) * radius,
                                                    math.sin(math.tau * i / segments) * radius,
                                                    0.0)))
            loop.append(vertex)
            vertices.append(vertex)
        loops.append(loop)
    for lower, upper in zip(loops, loops[1:]):
        for i in range(segments):
            built.append(bm.faces.new((lower[i], lower[(i + 1) % segments],
                                       upper[(i + 1) % segments], upper[i])))
    for ring, reverse in ((loops[0], True), (loops[-1], False)):
        centre = bm.verts.new(sum((vertex.co for vertex in ring), Vector()) / segments)
        vertices.append(centre)
        for i in range(segments):
            pair = (ring[(i + 1) % segments], ring[i]) if reverse else (ring[i], ring[(i + 1) % segments])
            built.append(bm.faces.new((centre, *pair)))
    for face in built:
        face.material_index = material_index
        face.smooth = True
        for loop in face.loops:
            loop[uv_layer].uv = (0.5, 0.5)
    return built, vertices


def rebuild_volumetric_crown(obj: bpy.types.Object, seed: int, leaf_image: bpy.types.Image) -> dict:
    """Replace alpha cards with closed bough lobes inside the accepted Pine AABB."""
    mesh = obj.data
    leaf_slot = next(index for index, material in enumerate(mesh.materials)
                     if material is not None and material.name == "AB_needles")
    # Keep the existing runtime alpha-cutout path. The opaque texel makes these
    # closed volumes solid while the shared shader still world-projects foliage.
    cx, cy = leaf_image.size[0] // 2, leaf_image.size[1] // 2
    alpha = leaf_image.pixels[(cy * leaf_image.size[0] + cx) * 4 + 3]
    if alpha <= 0.2:
        raise RuntimeError("Leaf_Pine_C center texel is no longer opaque for volumetric crown UVs")
    bark_triangles_before = material_triangles(obj, "AB_bark")
    bark_vertex_positions_before = sorted(
        tuple(round(float(component), 8) for component in obj.data.vertices[index].co)
        for polygon in obj.data.polygons if polygon.material_index != leaf_slot
        for index in polygon.vertices
    )

    bm = bmesh.new()
    bm.from_mesh(mesh)
    uv_layer = bm.loops.layers.uv.active or bm.loops.layers.uv.verify()
    old_needles = [face for face in bm.faces if face.material_index == leaf_slot]
    if not old_needles:
        bm.free()
        raise RuntimeError("no AB_needles faces found; the source mesh contract changed")
    bmesh.ops.delete(bm, geom=old_needles, context="FACES")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")

    rng = random.Random(seed * 7919 + 25)
    # One substantial swept bough at each level: unequal reach and angle keep
    # the silhouette organic without adding noisy needle-sized detail.
    branches = []
    branch_count = 20
    for index in range(branch_count):
        along_crown = index / max(branch_count - 1, 1)
        branches.append((0.480 + 0.370 * along_crown + rng.uniform(-0.016, 0.016),
                         (0.340 - 0.205 * along_crown) * rng.uniform(0.88, 1.12),
                         (0.102 - 0.045 * along_crown) * rng.uniform(0.80, 1.20),
                         (0.030 - 0.013 * along_crown) * rng.uniform(0.82, 1.18),
                         (0.040 - 0.020 * along_crown) * rng.uniform(0.85, 1.15),
                         0.22 + index * 2.399963229728653 + rng.uniform(-0.26, 0.26),
                         rng.uniform(-0.018, 0.018)))
    new_faces, new_vertices = [], []
    for base_z, length, width, height, droop, angle, lateral in branches:
        faces, vertices = add_closed_branch(bm, angle, base_z, length, width, height,
                                             droop, lateral, leaf_slot, uv_layer)
        new_faces.extend(faces)
        new_vertices.extend(vertices)
    core_faces, core_vertices = add_central_core(bm, leaf_slot, uv_layer)
    new_faces.extend(core_faces)
    new_vertices.extend(core_vertices)
    faces, vertices = add_central_leader(bm, leaf_slot, uv_layer)
    new_faces.extend(faces)
    new_vertices.extend(vertices)

    bounds = tuple((min(vertex.co[axis] for vertex in new_vertices),
                    max(vertex.co[axis] for vertex in new_vertices)) for axis in range(3))
    for vertex in new_vertices:
        for axis, (low, high) in enumerate(bounds):
            target_low, target_high = REFERENCE_CROWN_BOUNDS[axis]
            vertex.co[axis] = target_low + (vertex.co[axis] - low) * (target_high - target_low) / (high - low)
    bmesh.ops.recalc_face_normals(bm, faces=new_faces)
    crown_triangles = sum(len(face.verts) - 2 for face in new_faces)
    if crown_triangles != EXPECTED_CROWN_TRIANGLES:
        bm.free()
        raise RuntimeError(f"volumetric crown triangle census changed: {crown_triangles}")
    if any(not math.isfinite(component) for vertex in new_vertices for component in vertex.co):
        bm.free()
        raise RuntimeError("volumetric crown contains a non-finite vertex")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    bark_vertex_positions_after = sorted(
        tuple(round(float(component), 8) for component in mesh.vertices[index].co)
        for polygon in mesh.polygons if polygon.material_index != leaf_slot
        for index in polygon.vertices
    )
    if bark_vertex_positions_after != bark_vertex_positions_before:
        raise RuntimeError("crown replacement changed the existing bark vertex positions")
    bark_triangles_after = material_triangles(obj, "AB_bark")
    if bark_triangles_after != bark_triangles_before:
        raise RuntimeError("crown replacement changed the existing bark triangle census")
    return {
        "bark_triangles_preserved": bark_triangles_after,
        "bark_triangle_census_preserved": True,
        "bark_vertex_position_occurrences_preserved": len(bark_vertex_positions_after),
        "bark_vertex_positions_preserved": True,
        "source_alpha_card_faces_removed": len(old_needles),
        "closed_bough_volumes": len(branches),
        "central_core_volumes": 1,
        "central_leader_volumes": 1,
        "needle_triangles": crown_triangles,
        "alpha_sample_uv": [0.5, 0.5],
        "alpha_sample": round(float(alpha), 4),
        "reference_bounds_blender": [list(bounds) for bounds in REFERENCE_CROWN_BOUNDS],
        "bough_heights": [round(branch[0], 3) for branch in branches],
    }


def make_lod(source: bpy.types.Object, name: str, ratio: float) -> bpy.types.Object:
    """Decimate bark and closed crown separately, retaining material slots and UVs."""
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
    leaves.select_set(True)
    bpy.context.view_layer.objects.active = leaves
    foliage_modifier = leaves.modifiers.new(f"{name}_CrownDecimate", "DECIMATE")
    foliage_modifier.ratio = ratio
    if hasattr(foliage_modifier, "use_collapse_triangulate"):
        foliage_modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=foliage_modifier.name)
    bpy.ops.object.select_all(action="DESELECT")
    bark.select_set(True)
    leaves.select_set(True)
    bpy.context.view_layer.objects.active = bark
    bpy.ops.object.join()
    result = bpy.context.object
    result.name = name
    reduced_leaf = material_triangles(result, "AB_needles")
    if reduced_leaf <= 0 or reduced_leaf >= expected_leaf:
        raise RuntimeError(f"{name} did not reduce the crown for its LOD budget")
    return result


def main() -> None:
    root, seed = parse_argv()
    source_path = root / "assets/source/quaternius/stylized_nature/Pine_3.gltf"
    blend_path = root / "assets/source/blender/urman_winter_pine.blend"
    glb_path = root / "game/assets/models/act1/urman_winter_pine.glb"
    manifest_path = root / "game/assets/models/act1/urman_winter_pine_v2_manifest.json"
    texture_manifest = validate_source(source_path)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for group in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                  bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for block in list(group):
            group.remove(block, do_unlink=True)

    # Validate the pinned source mesh census and preserve its raw height as provenance.
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in set(bpy.context.scene.objects) - before if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"expected one pinned source mesh, found {len(meshes)}")
    source = meshes[0]
    if material_triangles(source, "Bark_NormalTree") != EXPECTED_SOURCE_BARK_TRIANGLES:
        raise RuntimeError("Pine_3 bark geometry contract changed")
    if material_triangles(source, "Leaves_Pine") != EXPECTED_SOURCE_LEAF_TRIANGLES:
        raise RuntimeError("Pine_3 leaf geometry contract changed")
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low = min(vertex.co.z for vertex in source.data.vertices)
    high = max(vertex.co.z for vertex in source.data.vertices)
    raw_height = high - low
    if raw_height <= 0.001:
        raise RuntimeError("Pine_3 has no usable vertical extent")

    # Discard the raw mesh and rebase from this immutable, hash-pinned production
    # GLB. This preserves the accepted trunk/bark vertices instead of resampling
    # them through a new foliage layout.
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for group in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                  bpy.data.images):
        for block in list(group):
            group.remove(block, do_unlink=True)
    result = subprocess.run(
        ["git", "show", f"{BASELINE_REVISION}:{BASELINE_GLB_PATH}"],
        cwd=root, check=True, capture_output=True,
    )
    baseline_bytes = result.stdout
    baseline_sha = hashlib.sha256(baseline_bytes).hexdigest()
    if baseline_sha != BASELINE_GLB_SHA256:
        raise RuntimeError(f"pinned Pine baseline SHA256 changed: {baseline_sha}")
    with tempfile.TemporaryDirectory(prefix="urman-pine-baseline-") as directory:
        baseline_path = Path(directory) / "urman_winter_pine.glb"
        baseline_path.write_bytes(baseline_bytes)
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=str(baseline_path))
    imported = set(bpy.context.scene.objects) - before
    source = next((obj for obj in imported if obj.name == "WinterPine_LOD0"), None)
    if source is None or source.type != "MESH":
        raise RuntimeError("pinned Pine baseline does not contain WinterPine_LOD0")
    source.parent = None
    for obj in imported:
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)
    source.name = "WinterPine_LOD0"
    bark = next((material for material in source.data.materials if material.name == "AB_bark"), None)
    leaves = next((material for material in source.data.materials if material.name == "AB_needles"), None)
    if bark is None or leaves is None:
        raise RuntimeError("pinned Pine baseline material ABI changed")
    if material_triangles(source, "AB_bark") != EXPECTED_BASELINE_BARK_TRIANGLES:
        raise RuntimeError("pinned Pine baseline bark triangle census changed")
    if material_triangles(source, "AB_needles") != EXPECTED_BASELINE_LEAF_TRIANGLES:
        raise RuntimeError("pinned Pine baseline foliage triangle census changed")
    leaf_image = next((node.image for node in leaves.node_tree.nodes
                       if node.type == "TEX_IMAGE" and node.image is not None), None)
    if leaf_image is None or leaf_image.name != "Leaf_Pine_C":
        raise RuntimeError("pinned Pine baseline embedded Leaf_Pine_C texture is missing")
    base_z = min(vertex.co.z for vertex in source.data.vertices)
    normalized_height = max(vertex.co.z for vertex in source.data.vertices) - base_z
    if abs(base_z) > 1e-5 or abs(normalized_height - 1.0) > 1e-5:
        raise RuntimeError("pinned Pine baseline is no longer normalized to base z=0, height=1")

    census = rebuild_volumetric_crown(source, seed, leaf_image)
    if material_triangles(source, "AB_needles") != EXPECTED_CROWN_TRIANGLES:
        raise RuntimeError("the generated crown differs from its fixed volumetric triangle census")

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
            "pinned_geometry_baseline_sha256": BASELINE_GLB_SHA256,
            "pinned_geometry_baseline_revision": BASELINE_REVISION,
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
        "raw_source_height_m": raw_height,
        "normalized_height_m": normalized_height,
        "pinned_geometry_baseline_sha256": BASELINE_GLB_SHA256,
        "pinned_geometry_baseline_revision": BASELINE_REVISION,
        "lod0_triangles": counts[0],
        "lod1_triangles": counts[1],
        "lod2_triangles": counts[2],
        "lod_policy": "LOD0 0-26m; LOD1 24-64m; LOD2 60m+",
        "leaf_triangles_lod0": material_triangles(source, "AB_needles"),
        "leaf_triangles_lod1": material_triangles(lod1, "AB_needles"),
        "leaf_triangles_lod2": material_triangles(lod2, "AB_needles"),
        "source_leaf_triangles": EXPECTED_SOURCE_LEAF_TRIANGLES,
        "leaf_lod_policy": "crown decimated with bark at 0.5 LOD1 / 0.2 LOD2 for affordable distant trees",
        "crown_policy": ("20 overlapping radial/spiral swept bough volumes around one irregular central core "
                         "plus one leaning leader; smooth-shaded, "
                         "asymmetric, and bounded to the previously shipped crown AABB (VIS-025/VIS-084)"),
        "census": json.dumps(census, sort_keys=True),
        "material_policy": "pinned production AB_bark/AB_needles materials and embedded Leaf_Pine_C retained",
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
                   "textures": TEXTURE_SHA256,
                   "pinned_geometry_baseline_revision": BASELINE_REVISION,
                   "pinned_geometry_baseline_sha256": BASELINE_GLB_SHA256},
        "outputs": {"blend": str(blend_path.relative_to(root)),
                    "glb": str(glb_path.relative_to(root))},
        "triangle_census": {"lod0": counts[0], "lod1": counts[1], "lod2": counts[2],
                            "needles": material_triangles(source, "AB_needles"),
                            "needles_lod1": material_triangles(lod1, "AB_needles"),
                            "needles_lod2": material_triangles(lod2, "AB_needles"),
                            "source_leaf_cards": EXPECTED_SOURCE_LEAF_TRIANGLES},
        "crown": census,
        "glb_sha256": sha256(glb_path),
        "contracts_kept": ["WinterPine_LOD0/1/2 node names", "AB_bark + AB_needles slots",
                           "base z = 0, height = 1 normalisation", "pinned CC0 source and embedded Leaf_Pine_C",
                           "exact bark vertex positions from pinned production baseline",
                           "current production crown AABB", "no collision"],
        "verification": "GLB structure/AABB and generator checks; matched Windows C1/C2 daylight plus LOD motion review still required",
    }, indent=1, sort_keys=True), encoding="utf-8")

    print(f"winter-pine-v2: lod0={counts[0]} lod1={counts[1]} lod2={counts[2]} "
          f"boughs={census['closed_bough_volumes']} "
          f"needles={census['needle_triangles']} source_cards={census['source_alpha_card_faces_removed']} "
          f"raw_height={raw_height:.6f} normalized=1.0 base_z=0.0")
    print(f"winter-pine-v2: blend={blend_path} glb={glb_path} manifest={manifest_path}")


if __name__ == "__main__":
    main()
