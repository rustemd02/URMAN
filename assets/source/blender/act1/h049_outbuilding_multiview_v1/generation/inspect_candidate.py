"""Inspect one returned shed GLB using the existing source studio; no mesh export."""
from pathlib import Path
import hashlib, importlib.util, json, sys

sys.dont_write_bytecode = True
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    assert bpy.app.version[:3] == (4, 5, 12)
    filename, label = sys.argv[sys.argv.index("--") + 1:]
    assert label in ("raw", "remesh") and filename in ("meshy-raw.glb", "meshy-remesh-8000.glb")
    source = HERE / filename
    output = HERE / (label + "-inspection.json")
    assert not output.exists()
    helper = ROOT / "assets/source/blender/act1/hero_house_pbr_v1/attach_hero_house_retexture.py"
    spec = importlib.util.spec_from_file_location("existing_house_studio", helper)
    studio = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(studio)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    objects = list(bpy.context.scene.objects)
    meshes = [ob for ob in objects if ob.type == "MESH"]
    assert meshes
    inventory = []
    for ob in meshes:
        ob.data.calc_loop_triangles()
        inventory.append({"name": ob.name, "vertices": len(ob.data.vertices),
                          "triangles": len(ob.data.loop_triangles),
                          "uv_layers": [layer.name for layer in ob.data.uv_layers],
                          "material_slots": [slot.material.name if slot.material else None for slot in ob.material_slots]})
    original_bounds = studio.object_bounds(meshes)
    low, high = map(Vector, original_bounds)
    original_dimensions = high - low
    assert original_dimensions.z > 0
    # Preview only: uniform scale to original shed height, centered at footprint.
    # Width/depth mismatch remains measured; never warp it into the source envelope.
    factor = 3.10 / original_dimensions.z
    root = bpy.data.objects.new("H049_InspectionOnlyUniformHeight", None)
    bpy.context.scene.collection.objects.link(root)
    for ob in objects:
        if ob.parent is None:
            matrix = ob.matrix_world.copy()
            ob.parent = root
            ob.matrix_world = matrix
    root.scale = (factor, factor, factor)
    root.location = Vector((-(low.x + high.x) * .5 * factor,
                            -(low.y + high.y) * .5 * factor, -low.z * factor))
    bpy.context.view_layer.update()
    bounds = studio.object_bounds(meshes)
    low, high = map(Vector, bounds)
    focus = (low + high) * .5
    camera = studio.create_studio(bpy.context.scene, focus, bounds)
    scene = bpy.context.scene
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1440, 1080
    width = max(high - low) * 1.52
    requests = [("front-left", focus, Vector((-1, -1, .48)), width),
                ("rear-right", focus, Vector((1, 1, .45)), width),
                ("front", focus, Vector((0, -1, .14)), width),
                ("left", focus, Vector((-1, 0, .14)), width),
                ("roof-and-openings", focus + Vector((0, -.16, .25)), Vector((-.65, -1, .70)), width * .87)]
    views = []
    for name, target, direction, ortho in requests:
        path = HERE / (label + "-" + name + ".png")
        studio.render_view(scene, camera, target, direction, ortho, path)
        views.append({"path": path.name, "sha256": sha(path), "bytes": path.stat().st_size,
                      "target": list(target), "direction": list(direction), "ortho_scale": ortho})
    doc = studio.glb_document(source)
    report = {"schema": "h049-multiview-generated-shed-source-inspection/v1", "stage": label,
              "status": "awaiting_actual_view_review_not_runtime_acceptance",
              "source": {"path": source.name, "bytes": source.stat().st_size, "sha256": sha(source)},
              "mesh_nodes": len(meshes), "triangles": sum(item["triangles"] for item in inventory),
              "mesh_inventory": inventory, "original_bounds_blender_z_up": original_bounds,
              "original_dimensions": list(original_dimensions),
              "inspection_uniform_scale": factor, "inspection_bounds_blender_z_up": bounds,
              "inspection_dimensions": list(high - low), "canonical_source_dimensions": [4.14, 2.8757482, 3.10],
              "normalization_scope": "In-memory uniform scale to 3.10m height and footprint centering only; no warp, mesh edit, export, runtime placement or collision modification",
              "materials_gltf": doc.get("materials", []),
              "embedded_images": [{"name": im.name, "size": list(im.size), "channels": im.channels,
                                   "colorspace": im.colorspace_settings.name} for im in bpy.data.images if im.type == "IMAGE"],
              "gltf_texture_count": len(doc.get("textures", [])), "views": views,
              "limits": ["Five exterior views do not prove an explorable interior or runtime performance",
                         "Multi-view reconstruction still infers 3D topology from the four source images; exterior images are not a topology proof",
                         "Original source glass remains separate and unchanged in the frozen reference folder"],
              "producer_sha256": sha(Path(__file__)), "studio_helper_sha256": sha(helper),
              "blender": bpy.app.version_string}
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print("H049_GENERATED_INSPECTION", json.dumps({"stage": label, "triangles": report["triangles"],
          "dimensions_at_height3_1": report["inspection_dimensions"], "views": len(views)}), flush=True)


if __name__ == "__main__":
    main()
