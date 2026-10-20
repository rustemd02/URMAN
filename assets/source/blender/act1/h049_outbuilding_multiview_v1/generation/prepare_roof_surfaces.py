"""Split the accepted H049 roof face list into one stable PBR surface.

System Python --prepare performs a direct GLB index/material partition following
the existing local source writer. Pinned Blender -- --render only imports/views it.
No mesh re-export, snow shader, texture editing or geometry modification.
"""
from pathlib import Path
import copy, hashlib, importlib.util, json, struct, sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
SOURCE = HERE / "meshy-remesh-local-candidate-v1.glb"
SELECTION = HERE / "roof-face-selection-v1.json"
OUTPUT = HERE / "h049-roof-body-surfaces-v1.glb"
RECEIPT = HERE / "roof-body-surfaces-v1-receipt.json"
SOURCE_SHA = "33c5358d476ed76a8aa608ec03feead72486088f0bef04a6686b355ff24821be"
SELECTION_SHA = "4c9de21e8226d8133ade521516db8e40c7346d07932413e0087419d1be19f3eb"
ROOF_MATERIAL = "H049_Roof_PBR"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


existing = module("existing_h049_source_writer", HERE / "prepare_local_candidate.py")


def save(report):
    RECEIPT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


def prepare():
    assert not OUTPUT.exists() and not RECEIPT.exists(), "Refuse overwrite or repeated source export"
    assert sha(SOURCE.read_bytes()) == SOURCE_SHA
    assert sha(SELECTION.read_bytes()) == SELECTION_SHA
    selected = json.loads(SELECTION.read_text())
    assert selected["source_sha256"] == SOURCE_SHA
    left, right = selected["left_face_ids"], selected["right_face_ids"]
    assert len(left) == 61 and len(right) == 62 and not set(left) & set(right)
    assert left == sorted(set(left)) and right == sorted(set(right))
    assert sha(b"".join(struct.pack("<I", f) for f in left)) == "9f803e1b784b7979521c11c1bac013dbc76cc11f9700a3a15470116c43dae54b"
    assert sha(b"".join(struct.pack("<I", f) for f in right)) == "18b2d8bbf19c5f1940e9ca2083b8173685a36d35e7bffbbfbeae34b38b89b2f8"
    roof = sorted(left + right)
    body = sorted(set(range(5538)) - set(roof))
    assert len(body) == 5415 and sorted(body + roof) == list(range(5538))
    report = {"schema": "h049-roof-body-source-surfaces/v1", "status": "preparing_not_accepted",
              "source": {"path": SOURCE.name, "bytes": SOURCE.stat().st_size, "sha256": SOURCE_SHA},
              "face_selection": {"path": SELECTION.name, "sha256": SELECTION_SHA},
              "producer_sha256": sha(Path(__file__).read_bytes()), "paid_calls": 0,
              "scope": "Two deterministic index/material primitives only; no vertex attributes, images, transforms, collision or shader changes",
              "excluded_roof_details": selected["excluded"],
              "drawcall_change": "One source primitive becomes two: one extra material surface/drawcall per visible instance, before any future snow adapter",
              "limits": ["Stable roof-only input is not a snow implementation or full roof snow coverage acceptance",
                         "Small front-edge facets/slivers and raised right strip deliberately remain body",
                         "Source view does not prove native winter scene, runtime memory/performance, collision or art acceptance"]}
    save(report)
    try:
        doc, binary = existing.read_glb(SOURCE)
        assert len(doc["meshes"]) == len(doc["meshes"][0]["primitives"]) == len(doc["materials"]) == 1
        original = doc["meshes"][0]["primitives"][0]
        assert original["attributes"] == {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}
        assert original["indices"] == 3 and original["material"] == 0 and original.get("mode", 4) == 4
        indices = [r[0] for r in existing.rows(doc, binary, 3)]
        assert len(indices) == 5538 * 3
        attrs = [existing.rows(doc, binary, i) for i in range(3)]
        output = copy.deepcopy(doc)
        output_binary = bytearray(binary)
        roof_material = copy.deepcopy(doc["materials"][0])
        roof_material["name"] = ROOF_MATERIAL
        output["materials"].append(roof_material)
        output["meshes"][0]["primitives"] = []
        mapping = []
        for slot, (name, face_ids) in enumerate((("body", body), ("roof", roof))):
            subset = [indices[3*f+j] for f in face_ids for j in range(3)]
            payload = struct.pack("<"+"H"*len(subset), *subset)
            output_binary.extend(b"\0"*((-len(output_binary))%4))
            view = len(output["bufferViews"])
            output["bufferViews"].append({"buffer": 0, "byteOffset": len(output_binary), "byteLength": len(payload), "target": 34963})
            output_binary.extend(payload)
            accessor = len(output["accessors"])
            output["accessors"].append({"bufferView": view, "componentType": 5123, "count": len(subset), "type": "SCALAR"})
            primitive = copy.deepcopy(original)
            primitive["indices"], primitive["material"] = accessor, slot
            output["meshes"][0]["primitives"].append(primitive)
            mapping.append({"primitive": slot, "name": name, "material_slot": slot, "triangles": len(face_ids),
                            "output_triangle_ordinal_to_source_face_id": face_ids,
                            "ordered_index_bytes_sha256": sha(payload), "index_accessor": accessor})
        output["buffers"][0]["byteLength"] = len(output_binary)
        encoded = json.dumps(output, separators=(",", ":"), ensure_ascii=False).encode()
        encoded += b" "*((-len(encoded))%4)
        output_binary.extend(b"\0"*((-len(output_binary))%4))
        # Existing Aidar/VariantA/local H049 GLB chunk writer; original BIN literal prefix.
        packed = (struct.pack("<III", 0x46546C67, 2, 28+len(encoded)+len(output_binary))
                  + struct.pack("<II", len(encoded), 0x4E4F534A)+encoded
                  + struct.pack("<II", len(output_binary), 0x004E4942)+output_binary)
        OUTPUT.write_bytes(packed)
        actual, actual_binary = existing.read_glb(OUTPUT)
        assert actual_binary[:len(binary)] == binary
        assert actual["accessors"][:len(doc["accessors"])] == doc["accessors"]
        assert actual["bufferViews"][:len(doc["bufferViews"])] == doc["bufferViews"]
        assert all(actual[k] == doc[k] for k in doc if k not in {"buffers", "bufferViews", "accessors", "meshes", "materials"})
        assert actual["materials"][0] == doc["materials"][0]
        comparison = copy.deepcopy(actual["materials"][1]); comparison["name"] = doc["materials"][0]["name"]
        assert comparison == doc["materials"][0]
        digest = hashlib.sha256()
        for row in mapping:
            actual_indices = [r[0] for r in existing.rows(actual, actual_binary, row["index_accessor"])]
            for face, source_face in enumerate(row["output_triangle_ordinal_to_source_face_id"]):
                original_ids = indices[3*source_face:3*source_face+3]
                assert actual_indices[3*face:3*face+3] == original_ids
                for vertex in original_ids:
                    digest.update(struct.pack("<8f", *attrs[0][vertex], *attrs[1][vertex], *attrs[2][vertex]))
        images = []
        for number, image in enumerate(doc["images"]):
            view = doc["bufferViews"][image["bufferView"]]
            payload = binary[view["byteOffset"]:view["byteOffset"]+view["byteLength"]]
            images.append({"image": number, "name": image.get("name"), "bytes": len(payload), "sha256": sha(payload)})
        assert len(images) == 4
        report.update({"status": "partition_prepared_awaiting_one_source_view_and_root_acceptance",
                       "triangles_before_and_after": 5538, "vertices_before_and_after": 8555,
                       "primitive_count_before": 1, "primitive_count_after": 2,
                       "roof_material_name": ROOF_MATERIAL, "source_bin_sha256": sha(binary),
                       "output_original_bin_prefix_sha256": sha(actual_binary[:len(binary)]),
                       "all_original_bin_bytes_literal": True, "all_source_accessors_and_attribute_bytes_unchanged": True,
                       "every_output_ordered_triangle_indices_xyz_normal_uv_identical_to_mapped_source": True,
                       "partition_ordered_corner_digest": digest.hexdigest(), "global_draw_stream_grouped_by_body_then_roof": True,
                       "nodes_scenes_transforms_mesh_name_unchanged": True,
                       "material_zero_unchanged_roof_material_exact_copy_except_name": True,
                       "all_four_image_bytes_and_texture_bindings_unchanged": images,
                       "tangent_attribute_present": False, "source_face_mapping": mapping,
                       "output": {"path": OUTPUT.name, "bytes": OUTPUT.stat().st_size, "sha256": sha(OUTPUT.read_bytes())}})
        save(report)
        print("H049_ROOF_SURFACES_PREPARED", json.dumps({"sha256": report["output"]["sha256"], "body": 5415, "roof": 123}), flush=True)
    except Exception as error:
        report.update({"status": "HOLD_partition_invariant_failure", "error_type": type(error).__name__, "error": str(error)})
        save(report)
        raise


def render():
    import bpy
    from mathutils import Vector
    assert bpy.app.version[:3] == (4, 5, 12)
    report = json.loads(RECEIPT.read_text())
    assert report["status"] == "partition_prepared_awaiting_one_source_view_and_root_acceptance"
    assert sha(OUTPUT.read_bytes()) == report["output"]["sha256"]
    helper = HERE.parents[5] / "assets/source/blender/act1/hero_house_pbr_v1/attach_hero_house_retexture.py"
    studio = module("existing_house_studio", helper)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT))
    meshes = [ob for ob in bpy.context.scene.objects if ob.type == "MESH"]
    assert len(meshes) == 1 and len(meshes[0].data.materials) == 2
    meshes[0].data.calc_loop_triangles()
    assert len(meshes[0].data.loop_triangles) == 5538
    slots = [sum(tri.material_index == slot for tri in meshes[0].data.loop_triangles) for slot in (0, 1)]
    assert slots == [5415, 123]
    low, high = map(Vector, studio.object_bounds(meshes))
    scale = 3.10/(high.z-low.z)
    root = bpy.data.objects.new("H049_InspectionOnlyUniformHeight", None)
    bpy.context.scene.collection.objects.link(root)
    ob = meshes[0]; matrix = ob.matrix_world.copy(); ob.parent = root; ob.matrix_world = matrix
    root.scale = (scale,)*3
    root.location = Vector((-(low.x+high.x)*.5*scale, -(low.y+high.y)*.5*scale, -low.z*scale))
    bpy.context.view_layer.update()
    bounds = studio.object_bounds(meshes)
    low, high = map(Vector, bounds)
    focus = (low+high)*.5
    camera = studio.create_studio(bpy.context.scene, focus, bounds)
    scene = bpy.context.scene; scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1440, 1080
    path = HERE / "roof-body-v1-front-left.png"
    assert not path.exists()
    studio.render_view(scene, camera, focus, Vector((-1, -1, .48)), max(high-low)*1.52, path)
    assert sha(SOURCE.read_bytes()) == SOURCE_SHA
    report.update({"status": "two_surface_source_frozen_awaiting_root_view_not_snow_or_runtime_acceptance",
                   "blender": bpy.app.version_string, "actual_imported_triangles": 5538,
                   "actual_imported_material_triangle_counts": slots,
                   "actual_imported_material_names": [mat.name for mat in meshes[0].data.materials],
                   "preview_uniform_scale": scale, "preview_bounds_blender_z_up": bounds,
                   "preview_normalization": "Same in-memory height3.10m/footprint-centering as local-v1; no transform baked or exported",
                   "actual_view": {"path": path.name, "bytes": path.stat().st_size, "sha256": sha(path.read_bytes()),
                                   "target": list(focus), "direction": [-1, -1, .48], "ortho_scale": max(high-low)*1.52},
                   "studio_helper_sha256": sha(helper.read_bytes())})
    save(report)
    print("H049_ROOF_SURFACES_VIEW", json.dumps({"view": path.name, "triangles": 5538, "material_counts": slots}), flush=True)


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:]
    assert arguments in (["--prepare"], ["--render"])
    prepare() if arguments == ["--prepare"] else render()
