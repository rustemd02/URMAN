#!/usr/bin/env python3
"""Build production WinterDeadTree .blend and GLB from the pinned CC0 GLTF.
Run in Blender 4.5 LTS: blender --background --python this_file.py -- --root <repo>
"""
from __future__ import annotations
import argparse, hashlib, sys
from pathlib import Path
import bpy
SOURCE_SHA256 = 'aa74a47c69259f217388d7b3e2871d76c0385449fc1efd92299c2e55417fdffc'
EXPECTED_LOD0, EXPECTED_LOD1 = (5802, 2901)

def parse_root() -> Path:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return parser.parse_args(argv).root.resolve()

def triangle_count(mesh: bpy.types.Mesh) -> int:
    return sum((max(0, len(poly.vertices) - 2) for poly in mesh.polygons))

def make_lod(source: bpy.types.Object, material: bpy.types.Material, name: str, ratio: float) -> bpy.types.Object:
    result = source.copy()
    result.data = source.data.copy()
    result.name = name
    result.data.materials.clear()
    result.data.materials.append(material)
    source.users_collection[0].objects.link(result)
    bpy.ops.object.select_all(action='DESELECT')
    result.select_set(True)
    bpy.context.view_layer.objects.active = result
    modifier = result.modifiers.new(f'{name}_Decimate', 'DECIMATE')
    modifier.ratio = ratio
    if hasattr(modifier, 'use_collapse_triangulate'):
        modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result.select_set(False)
    return result

def main() -> None:
    root = parse_root()
    source_path = root / 'assets/source/quaternius/stylized_nature/DeadTree_3.gltf'
    blend_path = root / 'assets/source/blender/urman_winter_dead_tree.blend'
    glb_path = root / 'game/assets/models/act1/urman_winter_dead_tree.glb'
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for group in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for block in list(group):
            group.remove(block, do_unlink=True)
    digest = hashlib.sha256(source_path.read_bytes()).hexdigest()
    if digest != SOURCE_SHA256:
        raise RuntimeError(f'unexpected source SHA256 {digest}; expected {SOURCE_SHA256}')
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in set(bpy.context.scene.objects) - before if obj.type == 'MESH']
    if len(meshes) != 1:
        raise RuntimeError(f'expected one imported mesh, found {len(meshes)}')
    source = meshes[0]
    world = source.matrix_world.copy()
    source.parent = None
    source.matrix_world = world
    for obj in list(bpy.context.scene.objects):
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)
    material = bpy.data.materials.new('AB_bark')
    material.use_nodes = True
    nodes, links = (material.node_tree.nodes, material.node_tree.links)
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Base Color'].default_value = (0.42, 0.39, 0.34, 1.0)
    shader.inputs['Roughness'].default_value = 0.92
    shader.inputs['Metallic'].default_value = 0.0
    links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    source.data.materials.clear()
    source.data.materials.append(material)
    bpy.ops.object.select_all(action='DESELECT')
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    low, high = (min((v.co.z for v in source.data.vertices)), max((v.co.z for v in source.data.vertices)))
    raw_height = high - low
    if raw_height <= 0.001:
        raise RuntimeError('DeadTree_3 has no usable vertical extent')
    scale = 1.0 / raw_height
    for v in source.data.vertices:
        v.co.x *= scale
        v.co.y *= scale
        v.co.z = (v.co.z - low) * scale
    source.data.update()
    source.name = 'WinterDeadTree_LOD0'
    lod1, lod2 = (make_lod(source, material, 'WinterDeadTree_LOD1', 0.5), make_lod(source, material, 'WinterDeadTree_LOD2', 0.2))
    objects = (source, lod1, lod2)
    counts = [triangle_count(obj.data) for obj in objects]
    if counts[:2] != [EXPECTED_LOD0, EXPECTED_LOD1]:
        raise RuntimeError(f'LOD0/LOD1 contract changed: {counts[:2]}')
    if not 0.16 <= counts[2] / counts[0] <= 0.24:
        raise RuntimeError(f'LOD2 ratio {counts[2] / counts[0]:.3f} is outside .2 band')
    for obj, level in zip(objects, range(3)):
        for key, value in {'asset_id': 'winter.dead_tree.v1', 'source_sha256': SOURCE_SHA256, 'license': 'CC0 1.0 Universal', 'collision': 'none', 'lod_level': level, 'lod_source': 'WinterDeadTree_LOD0' if level else ''}.items():
            obj[key] = value
    asset_root = bpy.data.objects.new('URMAN_WinterDeadTree', None)
    source.users_collection[0].objects.link(asset_root)
    for obj in objects:
        obj.parent = asset_root
    for key, value in {'asset_id': 'winter.dead_tree.v1', 'source_asset': 'assets/source/quaternius/stylized_nature/DeadTree_3.gltf', 'source_sha256': SOURCE_SHA256, 'source_license': 'CC0 1.0 Universal', 'normalized_base_z_m': 0.0, 'normalized_height_m': 1.0, 'raw_source_height_m': raw_height, 'lod0_triangles': counts[0], 'lod1_triangles': counts[1], 'lod2_triangles': counts[2], 'lod_policy': 'LOD0 0-26m; LOD1 24-64m; LOD2 60m+', 'collision_policy': 'none; existing Agent B terrain/road guards own traversal'}.items():
        asset_root[key] = value
    bpy.context.scene['generator'] = 'prepare_winter_dead_tree.py'
    bpy.context.scene['source_sha256'] = SOURCE_SHA256
    for old in list(bpy.data.materials):
        if old != material:
            bpy.data.materials.remove(old, do_unlink=True)
    for image in list(bpy.data.images):
        bpy.data.images.remove(image, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (asset_root, *objects):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = asset_root
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format='GLB', use_selection=True, export_apply=True, export_materials='EXPORT', export_yup=True, export_cameras=False, export_lights=False)
    print(f'winter-dead-tree: lod0={counts[0]} lod1={counts[1]} lod2={counts[2]} raw_height={raw_height:.6f} normalized=1.0 base_z=0.0')
    print(f'winter-dead-tree: blend={blend_path} glb={glb_path}')
if __name__ == '__main__':
    main()
