"""One source-only H002 donor using the existing constructive/atlas helpers."""
from pathlib import Path
import hashlib, importlib.util, json, sys, traceback
sys.dont_write_bytecode = True
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
SOURCE = ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
V2 = HERE.parent / "neighbor_house_a_retexture_v2/prepare_variant_a_retexture.py"
STUDIO = HERE.parent / "hero_house_pbr_v1/attach_hero_house_retexture.py"
REFERENCE = HERE.parent / "neighbor_house_b_reference_v1/imagegen-reference-v1.png"
SOURCE_RECEIPT = HERE.parent / "neighbor_house_b_reference_v1/source-receipt.json"
GLB = HERE / "variant-b-opaque-for-retexture.glb"
BLEND = HERE / "variant-b-opaque-for-retexture.blend"
RECEIPT = HERE / "candidate-receipt.json"
COMPONENT = "VillageParcel_VariantB_PlasterAnnex_Dwelling"
PREFIX = "VariantB_PlasterAnnex_Dwelling_"
SENI = {PREFIX + side + "_Wall_LOD0" for side in ("SeniEntry", "SeniOuter", "SeniRear")}
report = {"schema": "h002-variant-b-opaque-donor/v1", "status": "preparing_source_only"}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


p = module("existing_constructive_house_v2", V2)
p.HERE = HERE  # Every possible existing helper failure artifact belongs to B.
h = p.h
a = module("existing_house_studio", STUDIO)


def save():
    RECEIPT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")


def semantic(obj):
    """Triangle-corner contract permits UV seam vertex splits, not surface edits."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active
    rows = []
    for tri in mesh.loop_triangles:
        mat = mesh.materials[tri.material_index].name if tri.material_index < len(mesh.materials) else None
        points = []
        for li in tri.loops:
            loop = mesh.loops[li]
            position = tuple(round(float(v), 5) for v in mesh.vertices[loop.vertex_index].co)
            normal = tuple(float(v) for v in mesh.corner_normals[li].vector)
            texcoord = tuple(float(v) for v in uv.data[li].uv) if uv else None
            points.append((position, normal, texcoord))
        points.sort(key=lambda row: (row[0], row[1], row[2] or ()))
        rows.append((mat, tuple(points)))
    rows.sort(key=lambda row: (row[0] or "", tuple(point[0] for point in row[1]), row[1]))
    return rows


def compare_semantic(before, after, compare_uv):
    assert len(before) == len(after), "Triangle count changed"
    maximum_normal, maximum_uv = 0.0, 0.0
    for old, new in zip(before, after):
        assert old[0] == new[0], "Source material surface changed"
        for first, second in zip(old[1], new[1]):
            assert first[0] == second[0], "Triangle-corner geometry changed"
            maximum_normal = max(maximum_normal, *(abs(x-y) for x, y in zip(first[1], second[1])))
            if compare_uv:
                assert first[2] is not None and second[2] is not None
                maximum_uv = max(maximum_uv, *(abs(x-y) for x, y in zip(first[2], second[2])))
    return maximum_normal, maximum_uv


def describe(obj, inverse):
    rows = semantic(obj)
    return {"triangles": h.mesh_triangles(obj.data), "vertices": len(obj.data.vertices),
            "materials": h.object_material_names(obj), "source_uv": h.uv_layer_summary(obj.data),
            "root_relative_transform": h.matrix_rows(inverse @ obj.matrix_world),
            "intrinsic_transform": h.matrix_rows(obj.matrix_basis),
            "geometry_material_normal_contract_sha256": hashlib.sha256(repr(rows).encode()).hexdigest()}


def render_views(root, objects):
    scene = bpy.context.scene
    bounds = a.object_bounds(objects)
    low, high = map(Vector, bounds)
    focus = (low + high) * .5
    camera = a.create_studio(scene, focus, bounds)
    scene.cycles.samples = 24
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 4
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    width = max(high-low) * 1.42
    requests = [("actual-glb-front", focus, Vector((0, -1, .18)), width),
                ("actual-glb-right", focus, Vector((1, 0, .18)), width),
                ("actual-glb-rear", focus, Vector((0, 1, .18)), width),
                ("actual-glb-left", focus, Vector((-1, 0, .18)), width)]
    for side, direction in (("SeniEntry", Vector((1, -1, .35))),
                            ("SeniRear", Vector((1, 1, .35)))):
        selected = [ob for ob in objects if ob.name.startswith(PREFIX+side+"_")]
        lo, hi = map(Vector, a.object_bounds(selected))
        requests.append(("actual-glb-"+side.lower()+"-corner-opening", (lo+hi)*.5, direction, max(hi-lo)*1.6))
    views = []
    for name, target, direction, width in requests:
        path = HERE / (name + ".png")
        a.render_view(scene, camera, target, direction, width, path)
        views.append({"path": path.name, "bytes": path.stat().st_size, "sha256": h.sha256_file(path),
                      "target_blender_z_up": list(target), "direction": list(direction), "ortho_scale": width})
    return views


def main():
    assert bpy.app.version[:3] == (4, 5, 12), bpy.app.version_string
    assert not any(path.exists() for path in (GLB, BLEND, RECEIPT)), "Refusing candidate overwrite"
    assert h.sha256_file(SOURCE) == "fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
    assert h.sha256_file(REFERENCE) == "a53bdb1407061568b786d76cef2017fc9971b49feff51a2ade7820e3c3b7d341"
    report.update({"source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": h.sha256_file(SOURCE),
                             "bytes": SOURCE.stat().st_size, "rights": "Project-original per assets/asset_registry.json urman.act1.village_exterior_kit"},
                   "dependencies": [{"path": str(path.relative_to(ROOT)), "sha256": h.sha256_file(path)}
                                    for path in (V2, STUDIO, Path(h.__file__), ROOT/"game/scripts/Act1ConnectedWorld.DwellingLogWalls.cs")],
                   "material_reference": {"path": str(REFERENCE.relative_to(ROOT)), "sha256": h.sha256_file(REFERENCE),
                                          "role": "Accepted built-in ImageGen material/style only; no geometry from concept roof/steps"},
                   "source_reference_receipt": {"path": str(SOURCE_RECEIPT.relative_to(ROOT)), "sha256": h.sha256_file(SOURCE_RECEIPT)},
                   "scope": "Source-only private H002 VariantB. No upload, paid call, runtime file, source helper edit, LOD or material attachment.",
                   "component": COMPONENT, "blender": bpy.app.version_string})
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    root = bpy.data.objects[COMPONENT]
    parts = [ob for ob in root.children_recursive if ob.type == "MESH"]
    assert len(parts) == 123 and sum(h.mesh_triangles(ob.data) for ob in parts) == 3704
    matrix = root.matrix_world.copy()
    root.parent = None
    root.matrix_world = matrix
    keep = {root, *root.children_recursive}
    for ob in list(bpy.data.objects):
        if ob not in keep: bpy.data.objects.remove(ob, do_unlink=True)
    preview = list(root.location)
    assert preview == [41.0, 0.0, 0.0], preview
    root.location = (0, 0, 0)
    assert tuple(root.scale) == (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    inverse = root.matrix_world.inverted_safe()
    records = {ob.name: describe(ob, inverse) for ob in parts}
    originals = {ob.name: semantic(ob) for ob in parts}
    walls = [ob for ob in parts if ob.name.endswith("_Wall_LOD0") and h.object_material_names(ob) == ["URMAN_Wood_Weathered"]]
    assert {ob.name for ob in walls} == SENI
    assert all(h.matrix_rows(ob.matrix_world) == h.matrix_rows(walls[0].matrix_world) for ob in walls)
    centre, centre_source = h.hero_centre_world(root, parts)
    material = bpy.data.materials.new("Runtime_VariantB_Seni_Constructive_85715c_PreRegrade")
    material.use_nodes = True
    rgb = [int(v, 16)/255 for v in ("85", "71", "5c")]
    linear = [c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in rgb]
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*linear, 1)
    material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .82
    report.update({"phase": "constructive_seni", "preview_translation_removed": preview,
                   "root_scale": list(root.scale), "centre_source": centre_source,
                   "seni_wall_transforms": {ob.name: h.matrix_rows(ob.matrix_world) for ob in walls}})
    save()
    crowns, crown_stats, ignored_hidden, construction = p.build_constructive_crowns(root, parts, centre, material)
    assert len(crowns) == 3 and {ob.name for ob in crowns} == {n.replace("_Wall_LOD0", "_LogCrown") for n in SENI}
    included, excluded = [], []
    for ob in parts:
        names = h.object_material_names(ob)
        reason = "semantic glass" if names == ["URMAN_Window_DimGlass"] else "separate roof snow layer" if names == ["URMAN_Hero_RoofSnow"] else None
        if reason:
            excluded.append({"node": ob.name, "reason": reason, "triangles": h.mesh_triangles(ob.data), "materials": names})
            ob.hide_render = True
            ob.hide_set(True)
        else: included.append(ob)
    assert len(excluded) == 10 and len(included) == 113
    assert sum(h.mesh_triangles(ob.data) for ob in included) == 3296
    assert len([ob for ob in included if "_Corner_" in ob.name]) == 4
    assert len([ob for ob in included if "Recess" in ob.name]) == 10
    assert h.object_material_names(next(ob for ob in included if ob.name == PREFIX+"Roof_LOD0")) == ["URMAN_Roof_WetSlate", "URMAN_Wood_WetShadow"]
    for ob in parts:
        assert originals[ob.name] == semantic(ob), ob.name
        assert records[ob.name]["intrinsic_transform"] == h.matrix_rows(ob.matrix_basis), ob.name
    included += crowns
    assert len(included) == 116
    for ob in crowns: records[ob.name] = describe(ob, inverse)
    report.update({"phase": "single_global_uv_pack", "construction_geometry": construction,
                   "generated_crowns": crown_stats, "ignored_hidden_corner_list": sorted(ignored_hidden),
                   "excluded": excluded, "source_mesh_count": 123, "source_triangles": 3704,
                   "original_opaque_mesh_count": 113, "original_opaque_triangles": 3296,
                   "semantic_nodes": records, "original_corner_posts_retained": True, "original_recess_nodes_retained": True})
    before = {ob.name: semantic(ob) for ob in included}
    original_bounds = a.object_bounds(parts)
    crown_bounds = a.object_bounds(crowns)
    report["placement_envelope"] = {"original_component_bounds_blender": original_bounds,
                                    "constructive_seni_bounds_blender": crown_bounds, "specified_end_overhang_m": .18,
                                    "claim": "Measured source envelopes only; unchanged runtime placement not claimed."}
    save()
    atlas = p.build_constructive_atlas(included, records)
    atlas["method"] = "Existing V2 Smart UV Project 66 degrees and one multi-object Pack Islands; original H002 geometry plus three constructive Seni meshes"
    atlas["source_missing_uv_cause"] = "All 113 original opaque H002 meshes/117 glTF primitives had no TEXCOORD; all retained and generated meshes packed together once."
    atlas["actual_blender_overlap_selected_face_count"] = p.actual_uv_overlap_count(included)
    atlas["overlap_diagnostic"] = p.OVERLAP_DETAILS
    for ob in included:
        normal_delta, _ = compare_semantic(before[ob.name], semantic(ob), False)
        assert normal_delta == 0.0, (ob.name, "UV pass changed normals", normal_delta)
        records[ob.name]["input_uv_sha256"] = h.uv_loop_sha256(ob.data)
        records[ob.name]["input_triangles"] = h.mesh_triangles(ob.data)
    report["uv_atlas"] = atlas
    report["phase"] = "export_roundtrip"
    save()
    expected = {ob.name: (semantic(ob), h.matrix_rows(inverse@ob.matrix_world)) for ob in included}
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.object.select_all(action="DESELECT")
    for ob in [root, *included]: ob.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(GLB), export_format="GLB", use_selection=True, export_apply=False,
                              export_texcoords=True, export_normals=True, export_materials="EXPORT", use_visible=False)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    actual_root = bpy.data.objects[COMPONENT]
    actual = [ob for ob in actual_root.children_recursive if ob.type == "MESH"]
    assert {ob.name for ob in actual} == set(expected)
    inv = actual_root.matrix_world.inverted_safe()
    maximum_normal, maximum_uv, maximum_transform = 0.0, 0.0, 0.0
    for ob in actual:
        old, transform = expected[ob.name]
        normal_delta, uv_delta = compare_semantic(old, semantic(ob), True)
        maximum_normal = max(maximum_normal, normal_delta)
        maximum_uv = max(maximum_uv, uv_delta)
        maximum_transform = max(maximum_transform, *(abs(x-y) for first, second in zip(transform, h.matrix_rows(inv@ob.matrix_world)) for x,y in zip(first,second)))
    overlap_after_import = p.actual_uv_overlap_count(actual)
    invariants = {"original_source_mesh_geometry_materials_normals_intrinsic_transforms_exact_before_export": True,
                  "semantic_triangle_geometry_materials_preserved_after_export": True,
                  "normal_component_max_abs_delta": maximum_normal, "normal_component_tolerance": 1e-4,
                  "uv_component_max_abs_delta": maximum_uv, "uv_component_tolerance": 1e-6,
                  "root_relative_transform_max_abs_delta": maximum_transform, "transform_tolerance": 1e-5,
                  "uv_seam_vertex_reindexing_allowed": True, "byte_identical_accessor_indices_claimed": False,
                  "root_scale_one_no_point9_bake": list(actual_root.scale) == [1,1,1],
                  "actual_imported_mesh_count": len(actual), "actual_imported_triangles": sum(h.mesh_triangles(ob.data) for ob in actual),
                  "actual_imported_overlap_selected_face_count": overlap_after_import}
    report["invariants"] = invariants
    report["outputs"] = [{"path": path.name, "bytes": path.stat().st_size, "sha256": h.sha256_file(path)} for path in (GLB, BLEND)]
    report["phase"] = "actual_imported_glb_views"
    save()
    report["actual_glb_views"] = render_views(actual_root, actual)
    gates = {"uv_overlap_zero_before_and_after": atlas["actual_blender_overlap_selected_face_count"] == 0 and overlap_after_import == 0,
             "normal_semantic_tolerance": maximum_normal <= 1e-4,
             "uv_roundtrip_tolerance": maximum_uv <= 1e-6,
             "transform_roundtrip_tolerance": maximum_transform <= 1e-5,
             "root_scale_one": invariants["root_scale_one_no_point9_bake"]}
    report["technical_gates"] = gates
    report["status"] = "source_candidate_parent_review_required" if all(gates.values()) else "source_candidate_technical_hold_stop"
    report["remaining"] = "Parent actual four-side/corner review; no material attachment, upload, runtime/native/art acceptance. Any failed gate stops this single attempt."
    report["phase"] = "frozen"
    assert h.sha256_file(SOURCE) == report["source"]["sha256"]
    save()
    print("H002_SOURCE_CANDIDATE", json.dumps({"status": report["status"], "gates": gates, "meshes": len(actual), "triangles": invariants["actual_imported_triangles"]}), flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        report.update({"status": "source_preparation_failed_stop", "failure": repr(error), "traceback": traceback.format_exc()})
        save()
        raise
