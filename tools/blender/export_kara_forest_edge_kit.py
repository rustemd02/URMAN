"""Validate and export the authored Kara-Urman forest-edge kit reproducibly.

Run with the pinned Blender:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python tools/blender/export_kara_forest_edge_kit.py -- --root <repo>

The Kara forest-edge kit has no procedural generator: its `.blend` is authored
by hand and this script is the documented reproducible export path required by
tracker task ART-006.  It re-opens the saved source file, validates the
published component contract (names, parents, counts, no images/collision),
then exports the runtime GLB with fixed settings so repeated exports of an
unchanged source are byte-stable.  It deliberately adds no geometry: any
threshold or silhouette change must keep the restrained boundary motif, avoid
creature-like silhouettes, and preserve the central road sightline, and is
therefore subject to human 360 review before acceptance.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


ROOT_NAME = "URMAN_KaraForestEdgeKit"
EXPECTED_COMPONENTS = (
    "ForestBank_Left",
    "ForestBank_Right",
    "MixedTreeCluster_Left",
    "MixedTreeCluster_Right",
    "CrookedPineMass",
    "BirchEdgeMass",
    "RootWall_Left",
    "RootWall_Right",
    "FallenLogCluster",
    "MossyBoulderCluster",
    "CrookedStump",
    "DistantForestMass_Low",
    "DistantForestMass_Tall",
)
FORBIDDEN_NAME_PARTS = ("-col", "collision", "physics", "nav", "interact")


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    tokens: list[str] = []
    if "--" in sys.argv:
        tokens = sys.argv[sys.argv.index("--") + 1 :]
    return parser.parse_args(tokens)


def mesh_descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    meshes: list[bpy.types.Object] = []
    for child in root.children:
        if child.type == "MESH":
            meshes.append(child)
        meshes.extend(mesh_descendants(child))
    return meshes


def validate(root: bpy.types.Object) -> dict[str, object]:
    direct = tuple(child.name for child in root.children)
    missing = [name for name in EXPECTED_COMPONENTS if name not in direct]
    if missing:
        raise RuntimeError(f"Missing published component roots: {missing}")
    unexpected = [name for name in direct if name not in EXPECTED_COMPONENTS]
    if unexpected:
        raise RuntimeError(f"Unexpected direct component roots: {unexpected}")

    meshes = mesh_descendants(root)
    if not meshes:
        raise RuntimeError("Authored kit has no presentation meshes")

    triangles = sum(len(mesh.data.loop_triangles) for mesh in meshes)
    for mesh in meshes:
        if not mesh.data.loop_triangles:
            raise RuntimeError(f"Degenerate mesh: {mesh.name}")
        lower_name = mesh.name.lower()
        if any(part in lower_name for part in FORBIDDEN_NAME_PARTS):
            raise RuntimeError(f"Collision-like mesh name leaked: {mesh.name}")

    images = [item for item in bpy.data.images if item.name not in ("Render Result", "Viewer Node")]
    if images:
        raise RuntimeError(f"Image textures are not allowed: {[image.name for image in images]}")

    forbidden_types = [
        obj.name
        for obj in bpy.data.objects
        if obj.type in ("CAMERA", "LIGHT", "CollisionShape") or obj.rigid_body is not None
    ]
    if forbidden_types:
        raise RuntimeError(f"Camera/light/physics objects are not allowed: {forbidden_types}")

    for name in EXPECTED_COMPONENTS:
        component = bpy.data.objects[name]
        if component.parent is not root:
            raise RuntimeError(f"Component root is not parented to the kit root: {name}")

    return {
        "component_count": len(direct),
        "mesh_count": len(meshes),
        "triangle_count": triangles,
        "material_count": len({slot.material.name for mesh in meshes for slot in mesh.material_slots if slot.material}),
    }


def main() -> None:
    args = arguments()
    root_path = Path(args.root).resolve()
    blend_path = root_path / "assets/source/blender/act1/urman_kara_forest_edge_kit.blend"
    glb_path = root_path / "game/assets/models/act1/urman_kara_forest_edge_kit.glb"

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None or root.type != "EMPTY":
        raise RuntimeError(f"Missing authored root: {ROOT_NAME}")

    report = validate(root)

    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=False,
        export_apply=True,
    )
    for key, value in report.items():
        print(f"kara-export: {key}={value}")
    print(f"kara-export: exported {glb_path}")


if __name__ == "__main__":
    main()
