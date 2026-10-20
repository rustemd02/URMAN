"""One guarded, two-face H049 source repair; never a general mesh repair.

Run with system Python --prepare, then pinned Blender -- --render.
The direct GLB chunk writer follows the existing Aidar/VariantA source producers.
"""
from pathlib import Path
import collections, copy, hashlib, importlib.util, io, json, math, struct, sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[5]
SOURCE = HERE / ".tools/h049-topology/meshy-remesh-8000.glb"
ARCHIVE = HERE / "meshy-remesh-8000.glb.zip"
OUTPUT = HERE / "meshy-remesh-local-candidate-v1.glb"
RECEIPT = HERE / "local-candidate-v1-receipt.json"
SOURCE_SHA = "179b7e3c308207a2b701216b816024ac3bc311657970145dcc9e8181d0a8a2bd"
ARCHIVE_SHA = "e33c87089ea22529e7ab933e44e23bf6a99fa0292a738bf8051b64dc7be2adf4"
CHANGED = {3366, 5354}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def save(report):
    RECEIPT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


def read_glb(path):
    raw = path.read_bytes()
    assert struct.unpack_from("<III", raw) == (0x46546C67, 2, len(raw))
    size, kind = struct.unpack_from("<II", raw, 12)
    assert kind == 0x4E4F534A
    doc = json.loads(raw[20:20 + size])
    length, kind = struct.unpack_from("<II", raw, 20 + size)
    assert kind == 0x004E4942 and 28 + size + length == len(raw)
    return doc, raw[28 + size:]


def rows(doc, binary, accessor_id):
    accessor = doc["accessors"][accessor_id]
    view = doc["bufferViews"][accessor["bufferView"]]
    assert not accessor.get("sparse") and not view.get("byteStride")
    kind = {5126: "f", 5123: "H"}[accessor["componentType"]]
    count = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[accessor["type"]]
    fmt = "<" + kind * count
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    return [struct.unpack_from(fmt, binary, start + i * struct.calcsize(fmt))
            for i in range(accessor["count"])]


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def unit(a):
    length = math.sqrt(dot(a, a))
    return tuple(x / length for x in a)


def bary(point, a, b, c):
    x, y, z = sub(b, a), sub(c, a), sub(point, a)
    xx, xy, yy, zx, zy = dot(x, x), dot(x, y), dot(y, y), dot(z, x), dot(z, y)
    denominator = xx * yy - xy * xy
    v, w = (yy*zx-xy*zy)/denominator, (xx*zy-xy*zx)/denominator
    return (1-v-w, v, w)


def uv_area(points):
    a, b, c = points
    return abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])) * .5


def fit_chart(target_xyz, donor_xyz, donor_uv, base_image):
    """Preserve the donor's affine direction, fit within its existing material patch.

    Positive uniform UV scale and translation only after projection onto the donor
    plane. Inset all three triangle half-planes by three 4K texels, so the entire
    new triangle lies in a known coplanar semantic chart, not arbitrary atlas space.
    """
    original_bary = [bary(p, *donor_xyz) for p in target_xyz]
    area = uv_area(donor_uv)
    altitudes = [2 * area / math.dist(donor_uv[(j+1)%3], donor_uv[(j+2)%3]) for j in range(3)]
    gutter_bary = [3 / 4096 / altitude for altitude in altitudes]
    minima = [min(point[j] for point in original_bary) for j in range(3)]
    scale = (1 - sum(gutter_bary)) / (1 - sum(minima))
    assert 0 < scale <= 1
    translation = [gutter_bary[j] - scale * minima[j] for j in range(3)]
    result = [tuple(sum((translation[j]+scale*point[j])*donor_uv[j][k] for j in range(3))
                    for k in range(2)) for point in original_bary]
    # The actual exported float32 UVs, not merely the double precision fit.
    result = [struct.unpack("<2f", struct.pack("<2f", *point)) for point in result]
    distances = [[weight*altitudes[j]*4096 for j, weight in enumerate(bary(point, *donor_uv))]
                 for point in result]
    assert min(map(min, distances)) >= 2.99
    colors = []
    for i in range(11):
        for j in range(11-i):
            weights = (i/10, j/10, 1-(i+j)/10)
            u, v = (sum(weights[q]*result[q][k] for q in range(3)) for k in range(2))
            colors.append(base_image.getpixel((min(4095, max(0, int(u*4096))), min(4095, max(0, int(v*4096))))))
    assert not any(max(color) < 12 for color in colors)
    target_normal = unit(cross(sub(target_xyz[1], target_xyz[0]), sub(target_xyz[2], target_xyz[0])))
    donor_normal = unit(cross(sub(donor_xyz[1], donor_xyz[0]), sub(donor_xyz[2], donor_xyz[0])))
    angle = math.degrees(math.acos(max(-1, min(1, dot(target_normal, donor_normal)))))
    assert angle < 3
    return result, {"donor_uv_gltf_v_down": donor_uv, "positive_uniform_uv_scale": scale,
                    "translation_in_donor_barycentric_coordinates": translation,
                    "donor_normal_angle_degrees": angle,
                    "minimum_actual_float32_inset_texels_4096": min(map(min, distances)),
                    "new_uv_area_texels_4096": uv_area(result)*4096**2,
                    "basecolor_grid_samples": len(colors), "black_samples_below_12_of_255": 0,
                    "basecolor_mean_RGB_255": [sum(c[k] for c in colors)/len(colors) for k in range(3)],
                    "mapping_scope": "Intentional reuse of an existing same-surface triangle material patch; not a unique UV atlas. No texture editing, mirroring, painting or baking."}


def preview_positions(positions, inspection):
    low, high = inspection["original_bounds_blender_z_up"]
    scale = inspection["inspection_uniform_scale"]
    offset = (-(low[0]+high[0])*.5*scale, -(low[1]+high[1])*.5*scale, -low[2]*scale)
    return [tuple(value*scale+shift for value, shift in zip((x, -z, y), offset)) for x, y, z in positions]


def edge_counts(positions, indices):
    keys = [tuple(round(value, 6) for value in point) for point in positions]
    edges = collections.Counter()
    for start in range(0, len(indices), 3):
        triangle = [keys[indices[start+j]] for j in range(3)]
        for j in range(3):
            edges[tuple(sorted((triangle[j], triangle[(j+1)%3])))] += 1
    return edges


def attributes_digest(positions, normals, uv, indices, exclude=()):
    digest = hashlib.sha256()
    for face in range(len(indices)//3):
        if face in exclude:
            continue
        for vertex in indices[3*face:3*face+3]:
            digest.update(struct.pack("<8f", *positions[vertex], *normals[vertex], *uv[vertex]))
    return digest.hexdigest()


def prepare():
    from PIL import Image
    assert not OUTPUT.exists() and not RECEIPT.exists(), "Refuse overwrite or a repeated attempt"
    assert sha(ARCHIVE.read_bytes()) == ARCHIVE_SHA
    assert sha(SOURCE.read_bytes()) == SOURCE_SHA
    inspection = json.loads((HERE/"remesh-inspection.json").read_text())
    assert inspection["source"]["sha256"] == SOURCE_SHA
    report = {"schema": "h049-two-face-local-source-candidate/v1", "status": "preparing_not_accepted",
              "input": {"path": str(SOURCE.relative_to(HERE)), "bytes": SOURCE.stat().st_size, "sha256": SOURCE_SHA},
              "archive": {"path": ARCHIVE.name, "sha256": ARCHIVE_SHA, "immutable": True},
              "producer_sha256": sha(Path(__file__).read_bytes()), "paid_calls": 0,
              "authority": "Original H049 multi-view remesh source only; no runtime, collision or placement changes",
              "texture_source_provenance": ["submit.json", "status-02.json", "remesh-submit.json", "remesh-status-01.json"],
              "limits": ["Only two identified defects are addressed; this is not a global manifold/watertight repair",
                         "Material chart reuse can leave a seam or lower local texel density; actual five-view acceptance remains open",
                         "Source preview is not a native game frame; winter/snow and runtime performance gates remain open"]}
    save(report)
    try:
        doc, binary = read_glb(SOURCE)
        primitive = doc["meshes"][0]["primitives"][0]
        assert len(doc["meshes"]) == len(doc["meshes"][0]["primitives"]) == 1
        assert primitive["attributes"] == {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}
        assert primitive["indices"] == 3 and primitive.get("mode", 4) == 4
        positions, normals, uv = (rows(doc, binary, i) for i in range(3))
        indices = [row[0] for row in rows(doc, binary, 3)]
        assert len(positions) == 8549 and len(indices) == 5538*3
        assert indices[3366*3:3366*3+3] == [1166, 1167, 1168]
        assert indices[5354*3:5354*3+3] == [2106, 2107, 2108]
        assert [uv[i] for i in (1166, 1167, 1168)] == [(0., 1.)]*3
        image_rows = []
        for number, image in enumerate(doc["images"]):
            view = doc["bufferViews"][image["bufferView"]]
            payload = binary[view["byteOffset"]:view["byteOffset"]+view["byteLength"]]
            image_rows.append({"image": number, "name": image.get("name"), "bytes": len(payload), "sha256": sha(payload)})
        assert len(image_rows) == 4
        base_index = doc["textures"][doc["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
        view = doc["bufferViews"][doc["images"][base_index]["bufferView"]]
        base = Image.open(io.BytesIO(binary[view["byteOffset"]:view["byteOffset"]+view["byteLength"]])).convert("RGB")
        assert base.size == (4096, 4096)
        source_preview = preview_positions(positions, inspection)
        before_edges = edge_counts(source_preview, indices)
        new_positions, new_normals, new_uv, new_indices = (list(values) for values in (positions, normals, uv, indices))
        repairs = []
        for face, donor, corner_ids in [(3366, 3315, [1166, 1167, 1168]), (5354, 5360, [2091, 2108, 2107])]:
            original_ids = indices[face*3:face*3+3]
            donor_ids = indices[donor*3:donor*3+3]
            xyz = [positions[i] for i in corner_ids]
            chart, chart_receipt = fit_chart(xyz, [positions[i] for i in donor_ids], [uv[i] for i in donor_ids], base)
            geometric_normal = unit(cross(sub(xyz[1], xyz[0]), sub(xyz[2], xyz[0])))
            if face == 5354:
                assert geometric_normal[0] > .999
            new_corner_ids = []
            for j, original_id in enumerate(corner_ids):
                new_corner_ids.append(len(new_positions))
                new_positions.append(positions[original_id])
                new_normals.append(normals[original_id] if face == 3366 else geometric_normal)
                new_uv.append(chart[j])
            new_indices[face*3:face*3+3] = new_corner_ids
            repairs.append({"face_zero_based": face, "donor_chart_face": donor,
                            "original_indices": original_ids, "new_indices": new_corner_ids,
                            "position_source_indices": corner_ids,
                            "old_positions_gltf": [positions[i] for i in original_ids], "new_positions_gltf": xyz,
                            "old_positions_preview_metres": [source_preview[i] for i in original_ids],
                            "new_positions_preview_metres": [source_preview[i] for i in corner_ids],
                            "old_normals_gltf": [normals[i] for i in original_ids],
                            "new_normals_gltf": [new_normals[i] for i in new_corner_ids],
                            "old_uv_gltf_v_down": [uv[i] for i in original_ids], "new_uv_gltf_v_down": chart,
                            "uv_fit": chart_receipt,
                            "tangent": "TANGENT attribute absent in source and output; no stale tangent copied",
                            "scope": "UV only; ordered positions/normals exact" if face == 3366 else "DBC to ACB; positions copied from existing A,C,B; new flat outward face normal; UV chart mapping"})
        new_preview = preview_positions(new_positions, inspection)
        after_edges = edge_counts(new_preview, new_indices)
        expected_pairs = [(2091, 2107), (2091, 2108), (2107, 2108)]
        for left, right in expected_pairs:
            key = tuple(sorted(tuple(round(c, 6) for c in source_preview[i]) for i in (left, right)))
            assert after_edges[key] == 2
        assert sum(c == 1 for c in before_edges.values()) == 300
        assert sum(c == 1 for c in after_edges.values()) == 296
        assert sum(c > 2 for c in before_edges.values()) == sum(c > 2 for c in after_edges.values()) == 378
        assert attributes_digest(positions, normals, uv, indices, CHANGED) == attributes_digest(new_positions, new_normals, new_uv, new_indices, CHANGED)
        assert attributes_digest(positions, normals, uv, indices, set(range(5538))-{3366}) != attributes_digest(new_positions, new_normals, new_uv, new_indices, set(range(5538))-{3366})
        output_doc = copy.deepcopy(doc)
        output_binary = bytearray(binary)
        # Retain the entire original BIN literally, including all four embedded maps.
        # Append planar attributes, each with an exact original prefix plus six corners.
        for accessor_id, values, fmt in [(0, new_positions, "<3f"), (1, new_normals, "<3f"), (2, new_uv, "<2f"), (3, [(i,) for i in new_indices], "<H")]:
            output_binary.extend(b"\0"*((-len(output_binary))%4))
            payload = b"".join(struct.pack(fmt, *value) for value in values)
            old_accessor = doc["accessors"][accessor_id]
            old_view = doc["bufferViews"][old_accessor["bufferView"]]
            original_payload = binary[old_view["byteOffset"]:old_view["byteOffset"]+old_view["byteLength"]]
            if accessor_id < 3:
                assert payload[:len(original_payload)] == original_payload
            output_doc["accessors"][accessor_id]["bufferView"] = len(output_doc["bufferViews"])
            output_doc["accessors"][accessor_id]["count"] = len(values)
            output_doc["bufferViews"].append({"buffer": 0, "byteOffset": len(output_binary), "byteLength": len(payload), "target": old_view["target"]})
            output_binary.extend(payload)
        output_doc["buffers"][0]["byteLength"] = len(output_binary)
        encoded = json.dumps(output_doc, separators=(",", ":"), ensure_ascii=False).encode()
        encoded += b" "*((-len(encoded))%4)
        output_binary.extend(b"\0"*((-len(output_binary))%4))
        packed = (struct.pack("<III", 0x46546C67, 2, 28+len(encoded)+len(output_binary))
                  + struct.pack("<II", len(encoded), 0x4E4F534A)+encoded
                  + struct.pack("<II", len(output_binary), 0x004E4942)+output_binary)
        OUTPUT.write_bytes(packed)
        actual, actual_binary = read_glb(OUTPUT)
        assert actual_binary[:len(binary)] == binary
        assert all(actual[key] == doc[key] for key in doc if key not in {"buffers", "bufferViews", "accessors"})
        actual_rows = [rows(actual, actual_binary, i) for i in range(4)]
        actual_indices = [r[0] for r in actual_rows[3]]
        assert attributes_digest(*actual_rows[:3], actual_indices, CHANGED) == attributes_digest(positions, normals, uv, indices, CHANGED)
        assert len(actual_indices)//3 == 5538
        for face in range(5538):
            if face not in CHANGED:
                assert actual_indices[face*3:face*3+3] == indices[face*3:face*3+3]
        def bounds(pos, ids):
            return [[operation(pos[i][k] for i in ids) for k in range(3)] for operation in (min, max)]
        assert bounds(positions, indices) == bounds(actual_rows[0], actual_indices)
        report.update({"status": "source_patch_prepared_awaiting_actual_views_and_root_acceptance",
                       "source_bin_sha256": sha(binary), "output_original_bin_prefix_sha256": sha(actual_binary[:len(binary)]),
                       "original_binary_bytes_literal": True, "embedded_images_byte_identical": image_rows,
                       "all_four_material_bindings_nodes_meshes_samplers_unchanged": True,
                       "tangent_attribute_present": False, "source_interleaved": False, "output_interleaved": False,
                       "triangles_before": 5538, "triangles_after": 5538, "vertices_before": 8549, "vertices_after": len(actual_rows[0]),
                       "geometry_changed_face_ids": [5354], "uv_changed_face_ids": [3366, 5354], "normal_changed_face_ids": [5354],
                       "index_repointed_face_ids": [3366, 5354], "unchanged_face_attributes_digest": attributes_digest(positions, normals, uv, indices, CHANGED),
                       "other_5537_triangle_geometry_unchanged": True, "other_5536_triangle_all_attributes_unchanged": True,
                       "bounds_gltf_before_and_after": bounds(positions, indices), "face_repairs": repairs,
                       "local_topology": {"coordinate_key": "Preview metre positions rounded to 6 decimals; no mesh welding performed",
                                          "boundary_before": 300, "boundary_after": 296, "nonmanifold_before_and_after": 378,
                                          "repaired_AB_AC_BC_edge_owner_counts": [2, 2, 2],
                                          "interpretation": "Identified four-edge boundary removed by replacing one misconnected face; unrelated defects remain. Not a full watertight claim."},
                       "output": {"path": OUTPUT.name, "bytes": OUTPUT.stat().st_size, "sha256": sha(OUTPUT.read_bytes())}})
        save(report)
        print("H049_LOCAL_PREPARED", json.dumps({"sha256": report["output"]["sha256"], "triangles": 5538, "faces": sorted(CHANGED)}), flush=True)
    except Exception as error:
        report.update({"status": "HOLD_guard_or_safe_uv_not_proven", "error_type": type(error).__name__, "error": str(error)})
        save(report)
        raise


def render():
    import bpy
    from mathutils import Vector
    assert bpy.app.version[:3] == (4, 5, 12)
    report = json.loads(RECEIPT.read_text())
    assert report["status"] == "source_patch_prepared_awaiting_actual_views_and_root_acceptance"
    assert sha(OUTPUT.read_bytes()) == report["output"]["sha256"]
    helper = ROOT / "assets/source/blender/act1/hero_house_pbr_v1/attach_hero_house_retexture.py"
    spec = importlib.util.spec_from_file_location("existing_house_studio", helper)
    studio = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(studio)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT))
    objects = list(bpy.context.scene.objects)
    meshes = [ob for ob in objects if ob.type == "MESH"]
    assert len(meshes) == 1
    meshes[0].data.calc_loop_triangles()
    assert len(meshes[0].data.loop_triangles) == 5538
    low, high = map(Vector, studio.object_bounds(meshes))
    scale = 3.10 / (high.z-low.z)
    root = bpy.data.objects.new("H049_InspectionOnlyUniformHeight", None)
    bpy.context.scene.collection.objects.link(root)
    for ob in objects:
        if ob.parent is None:
            matrix = ob.matrix_world.copy()
            ob.parent = root
            ob.matrix_world = matrix
    root.scale = (scale,)*3
    root.location = Vector((-(low.x+high.x)*.5*scale, -(low.y+high.y)*.5*scale, -low.z*scale))
    bpy.context.view_layer.update()
    bounds = studio.object_bounds(meshes)
    low, high = map(Vector, bounds)
    focus = (low+high)*.5
    camera = studio.create_studio(bpy.context.scene, focus, bounds)
    scene = bpy.context.scene
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1440, 1080
    width = max(high-low)*1.52
    requests = [("front-left", focus, Vector((-1, -1, .48)), width),
                ("rear-right", focus, Vector((1, 1, .45)), width),
                ("front", focus, Vector((0, -1, .14)), width),
                ("left", focus, Vector((-1, 0, .14)), width),
                ("roof-and-openings", focus+Vector((0, -.16, .25)), Vector((-.65, -1, .70)), width*.87)]
    views = []
    for name, target, direction, ortho in requests:
        path = HERE / ("local-v1-"+name+".png")
        assert not path.exists()
        studio.render_view(scene, camera, target, direction, ortho, path)
        views.append({"path": path.name, "sha256": sha(path.read_bytes()), "bytes": path.stat().st_size,
                      "target": list(target), "direction": list(direction), "ortho_scale": ortho})
        report["actual_views"] = views
        save(report)
    report.update({"status": "source_candidate_frozen_awaiting_root_five_view_acceptance_not_runtime",
                   "blender": bpy.app.version_string, "studio_helper_sha256": sha(helper.read_bytes()),
                   "actual_imported_triangles": 5538, "preview_uniform_scale": scale,
                   "preview_bounds_blender_z_up": bounds, "preview_dimensions": list(high-low),
                   "normalization_scope": "Same in-memory height3.10m/footprint-centering convention as frozen remesh inspection; no transform baked/exported"})
    save(report)
    print("H049_LOCAL_VIEWS", json.dumps({"views": len(views), "output_sha256": report["output"]["sha256"]}), flush=True)


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:]
    assert arguments in (["--prepare"], ["--render"])
    prepare() if arguments == ["--prepare"] else render()
