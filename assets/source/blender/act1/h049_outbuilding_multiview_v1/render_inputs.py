"""Four canonical shed geometry inputs, no export or generated material edits."""
from pathlib import Path
import hashlib, importlib.util, json, sys

sys.dont_write_bytecode = True
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCE = HERE.parent / "h049_outbuilding_reference_v1/h049-outbuilding-source.glb"
STYLE = HERE.parent / "h049_outbuilding_reference_v1/imagegen-reference-v1.png"
COMPONENT = "VillageParcel_VariantA_TimberGable_Outbuilding"
SOURCE_SHA = "67fc359b6536ae801bfd84428b00780e918491d8ed11ec27365956bef1600ea3"
STYLE_SHA = "4511d043c56f36f87a2a3cfa9b49baf5bfeaeb03e33124dd947c8a3ad31335d9"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    assert bpy.app.version[:3] == (4, 5, 12)
    assert not (HERE / "input-receipt.json").exists()
    assert SOURCE.stat().st_size == 42880 and sha(SOURCE) == SOURCE_SHA
    assert sha(STYLE) == STYLE_SHA
    helper = HERE.parent / "hero_house_pbr_v1/attach_hero_house_retexture.py"
    spec = importlib.util.spec_from_file_location("existing_house_studio", helper)
    studio = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(studio)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    root = bpy.data.objects[COMPONENT]
    meshes = [ob for ob in root.children_recursive if ob.type == "MESH"]
    assert len(meshes) == 21
    assert len([ob for ob in meshes if ob.name == "VariantA_Shed_Door_Panel_LOD0"]) == 1
    assert len([ob for ob in meshes if ob.name == "VariantA_Shed_SideWindow_Inset_LOD0"]) == 1
    before = {ob.name: {"matrix_world": [list(row) for row in ob.matrix_world],
                       "vertices": [list(v.co) for v in ob.data.vertices],
                       "normals": [list(v.normal) for v in ob.data.vertices],
                       "materials": [m.name if m else None for m in ob.data.materials]}
              for ob in meshes}
    for ob in meshes:
        ob.data.calc_loop_triangles()
    assert sum(len(ob.data.loop_triangles) for ob in meshes) == 524
    assert not any(ob.data.uv_layers for ob in meshes)
    bounds = studio.object_bounds(meshes)
    low, high = map(Vector, bounds)
    target = (low + high) * .5
    ortho = max(high - low) * 1.5
    scene = bpy.context.scene
    camera = studio.create_studio(scene, target, bounds)
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    views = []
    for name, direction, semantic in [
        ("front", Vector((0, -1, .15)), "The actual source front gable, with the one door offset right; window is on the adjacent left wall"),
        ("back", Vector((0, 1, .15)), "Rear gable: no door or window; retains the existing repair batten"),
        ("left", Vector((-1, 0, .15)), "Left side: one small four-pane window, no door"),
        ("right", Vector((1, 0, .15)), "Right side: blank boarded wall, no door or window"),
    ]:
        path = HERE / (name + ".png")
        studio.render_view(scene, camera, target, direction, ortho, path)
        views.append({"semantic_view": name, "path": str(path.relative_to(ROOT)),
                      "sha256": sha(path), "bytes": path.stat().st_size,
                      "camera_position_blender_z_up": list(camera.location),
                      "target_blender_z_up": list(target), "direction": list(direction),
                      "orthographic_scale": ortho, "semantic_contract": semantic})
    assert sha(SOURCE) == SOURCE_SHA and sha(STYLE) == STYLE_SHA
    for ob in meshes:
        old = before[ob.name]
        assert [list(row) for row in ob.matrix_world] == old["matrix_world"]
        assert [list(v.co) for v in ob.data.vertices] == old["vertices"]
        assert [list(v.normal) for v in ob.data.vertices] == old["normals"]
        assert [m.name if m else None for m in ob.data.materials] == old["materials"]
    report = {
        "schema": "h049-canonical-multiview-input/v1",
        "status": "source_input_awaiting_root_review_no_api_task",
        "source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": SOURCE_SHA, "bytes": 42880,
                   "rights": "Project-original per inherited source provenance and assets/asset_registry.json"},
        "component": COMPONENT, "mesh_nodes": 21, "triangles": 524, "material_surfaces": 27,
        "uv_surfaces": 0, "source_geometry_and_object_transforms_unchanged": True,
        "source_material_roles_unchanged": True, "source_file_still_exact_sha": True,
        "bounds_blender_z_up": bounds, "dimensions_metres": list(high - low),
        "camera_contract": {"projection": "ORTHO", "ortho_scale": ortho, "tilt_ratio_z_to_horizontal": .15,
                            "target_blender_z_up": list(target), "resolution": [1280, 960],
                            "same_scale_target_background_lights_for_all_four": True,
                            "render_scale": 1, "cycles_samples": 24,
                            "studio_helper": str(helper.relative_to(ROOT)), "studio_helper_sha256": sha(helper)},
        "image_urls_order_for_future_api": [v["path"] for v in views],
        "views": views,
        "texture_reference_separate": {"path": str(STYLE.relative_to(ROOT)), "sha256": STYLE_SHA,
                                       "bytes": STYLE.stat().st_size, "future_field": "texture_image_url",
                                       "role": "Root-accepted ImageGen material family only; not an extra geometry view"},
        "topology_contract": "One front door, one left-side window, no rear/right openings. Four separate images, no collage.",
        "not_reproduced": ["runtime .731 parcel scale/.9 outbuilding compensation and edge-clamp", "world grounding/light/material overrides", "runtime collision/access"],
        "limits": "Multi-view inputs reduce unobserved-side ambiguity; they do not guarantee generated geometry, exact metric proportions or native acceptance.",
        "producer_sha256": sha(Path(__file__)), "blender": bpy.app.version_string,
        "new_meshy_api_calls": 0, "new_imagegen_calls": 0, "new_credit_spend": 0,
        "no_exports_or_uv_work": True,
    }
    (HERE / "input-receipt.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print("H049_MULTIVIEW_INPUT", json.dumps({"views": len(views), "bounds": bounds,
          "ortho_scale": ortho, "source_sha_unchanged": True}), flush=True)


if __name__ == "__main__":
    main()
