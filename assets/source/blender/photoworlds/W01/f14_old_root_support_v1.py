"""Author a fused, walk-edge old-root support for the W01 F14 bag contact.

Run from the repository root with Blender 4.5+:
  .tools/blender/Blender.app/Contents/MacOS/Blender --background \
    --python assets/source/blender/photoworlds/W01/f14_old_root_support_v1.py \
    -- --output-dir /private/tmp/urman-w01-f14-root-support-YYYYMMDD

The object is authored in F14-local Godot metres. Its lower roots bury into
the sampled F14 terrain; the fork is one fused mesh, so its visible branch and
runtime trimesh contact share the same authored surface.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ASSET_NAME = "f14-old-root-support-v1"
SNAG_POINT_GODOT = (2.20, 1.28, 0.46)
PLACEMENT_POINT_GODOT = (2.45, 0.0, 0.46)
VOXEL_SIZE_M = 0.025
SOURCE_DIR = Path(__file__).resolve().parent
REPO_ROOT = Path(__file__).resolve().parents[5]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_args():
    tail = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args(tail)


def load_hero_helpers():
    helper_path = SOURCE_DIR / "hero_root_turn_v1.py"
    spec = importlib.util.spec_from_file_location("urman_w01_hero_root_helpers", helper_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load shared W01 root geometry helpers: {helper_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    module.ASSET_NAME = ASSET_NAME
    return module


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.actions, bpy.data.images, bpy.data.cameras,
                       bpy.data.lights, bpy.data.collections):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.25
    scene.view_settings.gamma = 1.0
    scene.eevee.taa_render_samples = 48

    world = bpy.data.worlds.new("F14_OldRoot_WinterAir")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.10, 0.15, 0.17, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.56
    scene.world = world

    runtime = bpy.data.collections.new("Runtime_F14_OldRoot")
    scene.collection.children.link(runtime)
    render = bpy.data.collections.new("RenderOnly_F14_Preview")
    scene.collection.children.link(render)
    return scene, runtime, render


def create_runtime_asset(helpers, output_dir: Path, bark_material):
    root = bpy.data.objects.new("F14_OldRootSupport", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.2
    bpy.context.collection.objects.link(root)
    root["semantic_role"] = "visible, grounded fork where the old bag strap catches"
    root["coordinate_frame"] = "F14-local Godot metres; scene owns placement"

    # The lower body is buried below terrain Y=0. The upright fork leans away
    # from the route center so its collision stays at the edge of the approach.
    branch_specs = [
        ("F14_OldRoot_MainSpine",
         [(1.72, -0.34, -0.92), (1.72, -0.08, -0.58), (1.57, 0.10, -0.22),
          (1.45, 0.22, 0.14), (1.55, 0.34, 0.43), (1.73, 0.57, 0.62),
          (1.79, 0.84, 0.65), (1.93, 1.05, 0.59), (2.11, 1.20, 0.49),
          (2.20, 1.28, 0.46)],
         [0.48, 0.39, 0.31, 0.25, 0.22, 0.18, 0.15, 0.115, 0.075, 0.04]),
        ("F14_OldRoot_SpreadingButtress",
         [(2.20, -0.34, -0.62), (2.03, -0.08, -0.36), (1.84, 0.06, -0.05),
          (1.72, 0.16, 0.29), (1.67, 0.27, 0.55)],
         [0.42, 0.34, 0.27, 0.20, 0.15]),
        ("F14_OldRoot_LeftFork",
         [(1.79, 0.76, 0.65), (1.64, 0.96, 0.75), (1.49, 1.12, 0.88),
          (1.39, 1.25, 1.00), (1.34, 1.37, 1.09)],
         [0.16, 0.13, 0.095, 0.06, 0.025]),
        ("F14_OldRoot_RightFork",
         [(1.79, 0.78, 0.64), (1.94, 0.98, 0.60), (2.07, 1.13, 0.53),
          (2.16, 1.23, 0.48), (2.20, 1.28, 0.46)],
         [0.15, 0.12, 0.085, 0.055, 0.03]),
        ("F14_OldRoot_TrailwardRoot",
         [(1.50, -0.33, 0.52), (1.47, -0.08, 0.28), (1.49, 0.06, 0.05),
          (1.58, 0.13, -0.19)],
         [0.34, 0.27, 0.20, 0.12]),
    ]

    for index, (name, points, radii) in enumerate(branch_specs):
        obj = helpers.add_root_tube(name, points, radii, bark_material, root,
                                    sides=24, phase=0.61 + index * 1.13)
        obj["semantic_role"] = "fused exposed root / fork limb"

    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    if len(meshes) != len(branch_specs):
        raise RuntimeError(f"Expected {len(branch_specs)} authored root limbs, got {len(meshes)}")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    fused = meshes[0]
    bpy.context.view_layer.objects.active = fused
    bpy.ops.object.join()
    fused = bpy.context.view_layer.objects.active
    fused.name = "F14_OldRootSupport_LOD0"
    fused.data.name = "F14_OldRootSupport_FusedMesh"
    fused.parent = root

    # Voxel remesh fuses overlapping limbs so the root reads as one grown form,
    # rather than a collection of intersecting capped pipes.
    fused.data.remesh_voxel_size = VOXEL_SIZE_M
    fused.data.use_remesh_fix_poles = True
    fused.data.use_remesh_preserve_volume = True
    bpy.ops.object.voxel_remesh()
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.vertices_smooth(factor=0.22, repeat=2)
    bpy.ops.uv.smart_project(island_margin=0.012)
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in fused.data.polygons:
        polygon.use_smooth = True
    helpers.orient_normals(fused)
    fused.data.materials.clear()
    fused.data.materials.append(bark_material)
    fused["semantic_role"] = "single fused visible mesh; runtime collision derives from these exact triangles"
    fused["voxel_remesh_size_m"] = VOXEL_SIZE_M

    contact = bpy.data.objects.new("BagSnagContact", None)
    bpy.context.collection.objects.link(contact)
    contact.parent = root
    contact.matrix_parent_inverse.identity()
    contact.location = helpers.godot_to_blender(SNAG_POINT_GODOT)
    contact.empty_display_type = "SPHERE"
    contact.empty_display_size = 0.055
    contact["interface_role"] = "scene marker coordinate copied to Anchors/F14/BagSnagContact"

    return root, fused, meshes


def create_preview(scene, render_collection, helpers):
    ground_mat = bpy.data.materials.new("Preview_F14_SnowSurface")
    ground_mat.diffuse_color = (0.29, 0.34, 0.35, 1.0)
    ground_mat.use_nodes = True
    ground_shader = ground_mat.node_tree.nodes.get("Principled BSDF")
    ground_shader.inputs["Base Color"].default_value = (0.29, 0.34, 0.35, 1.0)
    ground_shader.inputs["Roughness"].default_value = 0.92

    bpy.ops.mesh.primitive_plane_add(size=9.0, location=(1.7, -0.46, 0.0))
    ground = bpy.context.object
    ground.name = "PreviewOnly_SampledSnowGround"
    ground.data.materials.append(ground_mat)
    for collection in list(ground.users_collection):
        collection.objects.unlink(ground)
    render_collection.objects.link(ground)

    camera_data = bpy.data.cameras.new("Preview_F14_OldRoot_Camera")
    camera = bpy.data.objects.new("Preview_F14_OldRoot_Camera", camera_data)
    render_collection.objects.link(camera)
    camera.location = helpers.godot_to_blender((4.45, 2.45, 3.35))
    target = Vector(helpers.godot_to_blender((1.78, 0.70, 0.25)))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 55
    scene.camera = camera

    sun_data = bpy.data.lights.new("Preview_F14_SoftWinterSun", "SUN")
    sun_data.energy = 1.7
    sun = bpy.data.objects.new("Preview_F14_SoftWinterSun", sun_data)
    render_collection.objects.link(sun)
    sun.rotation_euler = (math.radians(28), math.radians(-18), math.radians(-32))

    fill_data = bpy.data.lights.new("Preview_F14_WarmBarkFill", "AREA")
    fill_data.energy = 720
    fill_data.shape = "DISK"
    fill_data.size = 4.0
    fill = bpy.data.objects.new("Preview_F14_WarmBarkFill", fill_data)
    render_collection.objects.link(fill)
    fill.location = helpers.godot_to_blender((0.2, 3.4, 2.2))
    fill.rotation_euler = (Vector(helpers.godot_to_blender((1.75, 0.55, 0.25))) - fill.location).to_track_quat("-Z", "Y").to_euler()
    return camera


def object_metrics(root, helpers):
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("F14 old-root asset contains no mesh objects")
    verts = [vertex.co for obj in meshes for vertex in obj.data.vertices]
    triangles = sum(max(0, len(poly.vertices) - 2) for obj in meshes for poly in obj.data.polygons)
    return {
        "mesh_objects": {obj.name: {
            "vertices": len(obj.data.vertices),
            "triangles": sum(max(0, len(poly.vertices) - 2) for poly in obj.data.polygons),
        } for obj in meshes},
        "vertex_count": len(verts),
        "triangle_count": triangles,
        "godot_f14_local_bounds_m": {
            "x": [round(min(helpers.blender_to_godot(v)[0] for v in verts), 5),
                  round(max(helpers.blender_to_godot(v)[0] for v in verts), 5)],
            "y": [round(min(helpers.blender_to_godot(v)[1] for v in verts), 5),
                  round(max(helpers.blender_to_godot(v)[1] for v in verts), 5)],
            "z": [round(min(-v.y for v in verts), 5), round(max(-v.y for v in verts), 5)],
        },
    }


def build_manifest(output_dir, helper_path, ground_manifest, bark_path, bark_receipt_path,
                   bark_entry, mask_path, albedo_path, blend_path, glb_path, preview_path,
                   metrics):
    spec_path = REPO_ROOT / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/data/assets.spec.json"
    passport_path = REPO_ROOT / "docs/URMAN_PHOTOWORLDS_SPEC_2026-10-08/levels/W01_Лес_за_спиной_RU.md"
    ground_doc = json.loads(ground_manifest.read_text(encoding="utf-8"))
    manifest = {
        "schema": 1,
        "asset_id": "A-HERO-ROOT / W01 F14 visible support",
        "version": ASSET_NAME,
        "source_script": str(helper_path.relative_to(REPO_ROOT)),
        "source_script_sha256": sha256(helper_path),
        "references": {
            "asset_spec": str(spec_path.relative_to(REPO_ROOT)),
            "asset_spec_sha256": sha256(spec_path),
            "level_passport": str(passport_path.relative_to(REPO_ROOT)),
            "level_passport_sha256": sha256(passport_path),
            "ground_manifest": str(ground_manifest.relative_to(REPO_ROOT)),
            "ground_manifest_sha256": sha256(ground_manifest),
        },
        "consumer": "W01 F14 visible old-root support; same fused mesh supplies runtime static contact and the physical bag snag point",
        "coordinate_contract": {
            "runtime_scene_path": "res://assets/photoworlds/hero_root/f14-old-root-support-v1.glb",
            "root_node": "F14_OldRootSupport",
            "root_transform_owner": "W01 scene parents the instance under Anchors/F14 at identity",
            "coordinates": "F14-local Godot metres; Blender source maps (X,-Z,Y)",
            "sampled_terrain_height_y_m": 4.0,
            "passport_design_local_y_m": 7.0,
            "terrain_fit_note": "F14 X/Z are retained; terrain-fit Y=4.0 follows the unchanged generated shared ground surface rather than floating at the design-local Y=7.0",
            "scene_owned_markers": {
                "BagSnagContact": {"path": "Anchors/F14/BagSnagContact", "local_godot_xyz_m": list(SNAG_POINT_GODOT)},
                "BagPlacementGround": {"path": "Anchors/F14/BagPlacementGround", "local_godot_xyz_m": list(PLACEMENT_POINT_GODOT)},
            },
        },
        "geometry": {
            **metrics,
            "voxel_remesh_size_m": VOXEL_SIZE_M,
            "construction": "five smooth tapered root limbs, fused by voxel remesh into one closed visible mesh; basal ends buried below terrain; upper fork tips taper to small rounded ends",
            "bag_contact": "BagSnagContact lands at the measured outer right fork tip; the scene marker and GLB empty share the same F14-local coordinates",
            "runtime_collision": "StaticBody3D concave contact shapes are created from this exact visible mesh by W01Controller",
            "art_status": "offline asset preview only; full F14 composition and native play inspection remain pending",
        },
        "material": {
            "bark_source": str(bark_path.relative_to(REPO_ROOT)),
            "bark_sha256": sha256(bark_path),
            "bark_receipt": str(bark_receipt_path.relative_to(REPO_ROOT)),
            "bark_receipt_entry": bark_entry,
            "mask": {"file": mask_path.name, "sha256": sha256(mask_path), "size_px": [2048, 2048],
                     "provenance": "project-original deterministic W01 root mask from shared hero-root authoring helper"},
            "baked_albedo": {"file": albedo_path.name, "sha256": sha256(albedo_path),
                             "runtime_embedded": True, "size_px": [2048, 2048]},
        },
        "artifacts": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path), "bytes": blend_path.stat().st_size},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path), "bytes": glb_path.stat().st_size},
            "preview": {"path": preview_path.name, "sha256": sha256(preview_path), "bytes": preview_path.stat().st_size},
        },
        "verification": {
            "source_generated": True,
            "mesh_fused_and_bounded": True,
            "snag_marker_distance_to_root_mesh_m": "measured by deterministic surface-nearest query in generator",
            "native_godot_import": "not run",
            "gameplay_collision_and_bag_placement": "not run",
            "full_scene_and_art_acceptance": "pending",
        },
    }
    path = output_dir / f"{ASSET_NAME}.manifest.json"
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


def main():
    args = parse_args()
    output_dir = args.output_dir.expanduser().resolve()
    if output_dir.exists():
        raise RuntimeError(f"Refusing to overwrite existing staging directory: {output_dir}")
    output_dir.mkdir(parents=True, exist_ok=False)
    scene, runtime_collection, render_collection = reset_scene()
    helpers = load_hero_helpers()
    mask_path = output_dir / f"{ASSET_NAME}-mask-2k.png"
    helpers.write_png_rgb(mask_path, helpers.BAKE_SIZE, helpers.BAKE_SIZE, helpers.make_mask_pixel())
    bark_material, bark_path, bark_receipt_path, bark_entry, albedo_path = helpers.bark_material(
        REPO_ROOT, mask_path, output_dir)
    root, fused, authored_meshes = create_runtime_asset(helpers, output_dir, bark_material)
    metrics = object_metrics(root, helpers)
    bounds = metrics["godot_f14_local_bounds_m"]
    if bounds["y"][0] >= 0.0 or bounds["y"][1] < SNAG_POINT_GODOT[1] - 0.08:
        raise RuntimeError(f"Old-root bounds do not bury the base and reach the snag contact: {bounds}")
    if bounds["x"][0] < 1.0:
        raise RuntimeError(f"Old-root support crosses too close to the approach center: {bounds['x']}")
    if metrics["triangle_count"] > 100_000:
        raise RuntimeError(f"F14 support mesh unexpectedly dense: {metrics['triangle_count']} triangles")

    # Confirm the authored strap point is on (or within 12 cm of) the fused
    # root surface before writing the scene marker contract.
    snag_blender = Vector(helpers.godot_to_blender(SNAG_POINT_GODOT))
    closest = fused.closest_point_on_mesh(snag_blender)
    if not closest[0]:
        raise RuntimeError("Blender could not find a nearest surface point for BagSnagContact")
    contact_distance = (closest[1] - snag_blender).length
    if contact_distance > 0.12:
        raise RuntimeError(f"BagSnagContact is {contact_distance:.3f} m from the fused root surface")

    camera = create_preview(scene, render_collection, helpers)
    preview_path = output_dir / f"{ASSET_NAME}-preview.png"
    scene.render.filepath = str(preview_path)
    bpy.ops.render.render(write_still=True)

    blend_path = output_dir / f"{ASSET_NAME}.blend"
    glb_path = output_dir / f"{ASSET_NAME}.glb"
    manifest_path = output_dir / f"{ASSET_NAME}.manifest.json"
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    scene.camera = camera
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=True, export_texcoords=True,
        export_normals=True, export_tangents=True, export_materials="EXPORT",
        export_cameras=False, export_lights=False,
    )
    ground_manifest = SOURCE_DIR / "prologue-ground-f00-f14-v1.manifest.json"
    if not all(path.is_file() for path in (blend_path, glb_path, preview_path, mask_path, albedo_path)):
        raise RuntimeError("F14 old-root authoring missed a required output")
    manifest_path = build_manifest(output_dir, Path(__file__).resolve(), ground_manifest,
                                   bark_path, bark_receipt_path, bark_entry,
                                   mask_path, albedo_path, blend_path, glb_path,
                                   preview_path, metrics)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["geometry"]["bag_contact_nearest_surface_distance_m"] = round(contact_distance, 6)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "output_dir": str(output_dir),
        "blend": str(blend_path),
        "glb": str(glb_path),
        "manifest": str(manifest_path),
        "preview": str(preview_path),
        "marker_f14_local_godot_xyz_m": list(SNAG_POINT_GODOT),
        "nearest_surface_distance_m": round(contact_distance, 6),
        "mesh": metrics,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
