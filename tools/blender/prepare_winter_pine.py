#!/usr/bin/env python3
"""Build the production WinterPine .blend and GLB from the pinned CC0 glTF.

Run in Blender 4.5 LTS:
  blender --background --python this_file.py -- --root <repo>

The source tree is read from the repository and the two production outputs are
written under the repository's existing Blender/model asset paths.  The script
does not download, copy, or modify source assets.
"""
from __future__ import annotations

import argparse
import hashlib
import json
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
EXPECTED_SOURCE_TRIANGLES = EXPECTED_SOURCE_BARK_TRIANGLES + EXPECTED_SOURCE_LEAF_TRIANGLES
ASSET_ID = "winter.pine.v1"


def parse_root() -> Path:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv).root.resolve()


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def triangle_count(mesh: bpy.types.Mesh) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)


def material_triangles(obj: bpy.types.Object, material_name: str) -> int:
    slots = {
        index for index, slot in enumerate(obj.data.materials)
        if slot is not None and slot.name == material_name
    }
    return sum(
        max(0, len(poly.vertices) - 2)
        for poly in obj.data.polygons
        if poly.material_index in slots
    )


def leaf_uv_signature(
    obj: bpy.types.Object,
    material_name: str = "AB_needles",
) -> tuple[tuple[float, float], ...]:
    """Return a stable multiset of one material's UV loops for an LOD contract."""
    layer = obj.data.uv_layers.active
    if layer is None:
        raise RuntimeError(f"{obj.name} has no TEXCOORD_0/active UV layer")
    slots = {
        index for index, slot in enumerate(obj.data.materials)
        if slot is not None and slot.name == material_name
    }
    coords: list[tuple[float, float]] = []
    for poly in obj.data.polygons:
        if poly.material_index not in slots:
            continue
        for loop_index in poly.loop_indices:
            uv = layer.data[loop_index].uv
            coords.append((round(float(uv.x), 7), round(float(uv.y), 7)))
    return tuple(sorted(coords))


def validate_source_files(source_path: Path) -> tuple[Path, str]:
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    if source_path.suffix.lower() != ".gltf":
        raise RuntimeError(f"expected .gltf source, found {source_path}")
    if sha256(source_path) != SOURCE_SHA256:
        raise RuntimeError(f"unexpected source SHA256 for {source_path}")

    document = json.loads(source_path.read_text(encoding="utf-8"))
    buffers = document.get("buffers", [])
    if len(buffers) != 1 or buffers[0].get("uri") != "Pine_3.bin":
        raise RuntimeError("Pine_3.gltf must reference exactly Pine_3.bin")
    buffer_path = source_path.parent / buffers[0]["uri"]
    if sha256(buffer_path) != SOURCE_BIN_SHA256:
        raise RuntimeError(f"unexpected source SHA256 for {buffer_path}")

    referenced_textures = {
        image.get("uri") for image in document.get("images", [])
        if image.get("uri")
    }
    if referenced_textures != set(TEXTURE_SHA256):
        raise RuntimeError(
            "Pine_3.gltf texture set changed: "
            f"{sorted(referenced_textures)}"
        )
    for name, expected in TEXTURE_SHA256.items():
        texture_path = source_path.parent / name
        if sha256(texture_path) != expected:
            raise RuntimeError(f"unexpected texture SHA256 for {texture_path}")
    return buffer_path, ";".join(f"{name}={digest}" for name, digest in sorted(TEXTURE_SHA256.items()))


def imported_material_slot(obj: bpy.types.Object, prefix: str) -> int:
    matches = [
        index for index, slot in enumerate(obj.data.materials)
        if slot is not None and slot.name == prefix
    ]
    if len(matches) != 1:
        raise RuntimeError(f"expected one {prefix} material slot on {obj.name}")
    return matches[0]


def make_lod(source: bpy.types.Object, name: str, ratio: float) -> bpy.types.Object:
    """Decimate bark only; leaf triangles and UV loops remain unchanged."""
    expected_leaf_triangles = material_triangles(source, "AB_needles")
    expected_leaf_uv = leaf_uv_signature(source)
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
    parts = [
        result,
        *(obj for obj in set(bpy.context.scene.objects) - before if obj.type == "MESH"),
    ]
    bark = next((obj for obj in parts if material_triangles(obj, "AB_bark") > 0), None)
    leaves = next((obj for obj in parts if material_triangles(obj, "AB_needles") > 0), None)
    if bark is None or leaves is None or bark is leaves:
        raise RuntimeError(f"{name} material split did not produce bark and leaf parts")

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
    if material_triangles(result, "AB_needles") != expected_leaf_triangles:
        raise RuntimeError(f"{name} changed leaf triangles")
    if leaf_uv_signature(result) != expected_leaf_uv:
        raise RuntimeError(f"{name} changed AB_needles UV coordinates")
    return result


def main() -> None:
    root = parse_root()
    source_path = root / "assets/source/quaternius/stylized_nature/Pine_3.gltf"
    blend_path = root / "assets/source/blender/urman_winter_pine.blend"
    glb_path = root / "game/assets/models/act1/urman_winter_pine.glb"
    _, texture_manifest = validate_source_files(source_path)
    leaf_path = source_path.with_name("Leaf_Pine_C.png")

    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for group in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
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

    bark_slot = imported_material_slot(source, "Bark_NormalTree")
    leaf_slot = imported_material_slot(source, "Leaves_Pine")
    if bark_slot == leaf_slot:
        raise RuntimeError("Pine_3 material slots are not distinct")
    if material_triangles(source, "Bark_NormalTree") != EXPECTED_SOURCE_BARK_TRIANGLES:
        raise RuntimeError("Pine_3 bark geometry contract changed")
    if material_triangles(source, "Leaves_Pine") != EXPECTED_SOURCE_LEAF_TRIANGLES:
        raise RuntimeError("Pine_3 leaf geometry contract changed")
    if triangle_count(source.data) != EXPECTED_SOURCE_TRIANGLES:
        raise RuntimeError("Pine_3 source triangle contract changed")
    source_leaf_uv = leaf_uv_signature(source, "Leaves_Pine")

    bark = bpy.data.materials.new("AB_bark")
    bark.use_nodes = True
    bark_nodes = bark.node_tree.nodes
    bark_links = bark.node_tree.links
    bark_nodes.clear()
    bark_output = bark_nodes.new("ShaderNodeOutputMaterial")
    bark_shader = bark_nodes.new("ShaderNodeBsdfPrincipled")
    bark_shader.inputs["Base Color"].default_value = (0.42, 0.39, 0.34, 1.0)
    bark_shader.inputs["Roughness"].default_value = 0.92
    bark_shader.inputs["Metallic"].default_value = 0.0
    bark_links.new(bark_shader.outputs["BSDF"], bark_output.inputs["Surface"])

    # Godot reserves the _alpha suffix and would rename the material/change its blend mode.
    leaves = bpy.data.materials.new("AB_needles")
    leaves.use_nodes = True
    leaves.use_backface_culling = False
    try:
        leaves.surface_render_method = "DITHERED"
    except (AttributeError, TypeError):
        pass
    leaf_image = bpy.data.images.load(str(leaf_path), check_existing=False)
    leaf_image.pack()
    leaf_nodes = leaves.node_tree.nodes
    leaf_links = leaves.node_tree.links
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

    original_material_indices = [poly.material_index for poly in source.data.polygons]
    source.data.materials.clear()
    source.data.materials.append(bark)
    source.data.materials.append(leaves)
    for polygon, material_index in zip(source.data.polygons, original_material_indices):
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
    if material_triangles(source, "AB_needles") != EXPECTED_SOURCE_LEAF_TRIANGLES:
        raise RuntimeError("normalized Pine_3 leaf geometry contract changed")
    if leaf_uv_signature(source) != source_leaf_uv:
        raise RuntimeError("normalized Pine_3 leaf UV contract changed")

    lod1 = make_lod(source, "WinterPine_LOD1", 0.5)
    lod2 = make_lod(source, "WinterPine_LOD2", 0.2)
    objects = (source, lod1, lod2)
    counts = [triangle_count(obj.data) for obj in objects]
    for obj in objects:
        if material_triangles(obj, "AB_needles") != EXPECTED_SOURCE_LEAF_TRIANGLES:
            raise RuntimeError(f"{obj.name} changed the 462 leaf triangle contract")
        if leaf_uv_signature(obj) != source_leaf_uv:
            raise RuntimeError(f"{obj.name} changed the leaf UV contract")

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
        "leaf_lod_policy": "all LODs retain the original 462 AB_needles triangles and UV loops; bark-only decimation",
        "material_policy": "AB_bark opaque + AB_needles MASK .2 double-sided; Leaf_Pine_C embedded",
        "collision_policy": "none; existing Agent B terrain/road guards own traversal",
    }.items():
        asset_root[key] = value
    bpy.context.scene["generator"] = "prepare_winter_pine.py"
    bpy.context.scene["asset_id"] = ASSET_ID
    bpy.context.scene["source_sha256"] = SOURCE_SHA256
    bpy.context.scene["source_bin_sha256"] = SOURCE_BIN_SHA256
    bpy.context.scene["source_texture_manifest"] = texture_manifest

    for old in list(bpy.data.materials):
        if old not in (bark, leaves):
            bpy.data.materials.remove(old, do_unlink=True)
    for image in list(bpy.data.images):
        if image != leaf_image:
            bpy.data.images.remove(image, do_unlink=True)

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
    print(
        f"winter-pine: lod0={counts[0]} lod1={counts[1]} lod2={counts[2]} "
        f"leaves={EXPECTED_SOURCE_LEAF_TRIANGLES}/"
        f"{material_triangles(lod1, 'AB_needles')}/"
        f"{material_triangles(lod2, 'AB_needles')} "
        f"raw_height={raw_height:.6f} normalized=1.0 base_z=0.0"
    )
    print(f"winter-pine: blend={blend_path} glb={glb_path}")


if __name__ == "__main__":
    main()
