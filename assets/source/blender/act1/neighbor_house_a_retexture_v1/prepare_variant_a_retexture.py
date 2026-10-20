"""Source-only VariantA opaque donor; no service calls or runtime writes.

Reuse the hero-house atlas/mesh preparation helpers. The one local crown routine
is the nonhero branch of Act1ConnectedWorld.DwellingLogWalls.cs: same 14-sided
ellipses, half-course offset, 0.18 m main-wall overhangs and aperture subtraction.
The canonical GLB and C# remain untouched; original opaque source geometry stays
authoritative. This is a texture donor, never a runtime replacement of its own.
"""
from pathlib import Path
import hashlib, importlib.util, json, math, sys
import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
HELPER = HERE.parent / "hero_house_pbr_v1/prepare_hero_house_retexture.py"
spec = importlib.util.spec_from_file_location("existing_hero_house_prepare", HELPER)
h = importlib.util.module_from_spec(spec)
sys.dont_write_bytecode = True
spec.loader.exec_module(h)
SOURCE = ROOT / "game/assets/models/act1/urman_village_exterior_kit.glb"
RUNTIME = ROOT / "game/scripts/Act1ConnectedWorld.DwellingLogWalls.cs"
COMPONENT = "VillageParcel_VariantA_TimberGable_Dwelling"
GLB = HERE / "variant-a-opaque-for-retexture.glb"
BLEND = HERE / "variant-a-opaque-for-retexture.blend"
RECEIPT = HERE / "candidate-receipt.json"
WALL_COURSE = .26
WALL_LOG_SEGMENTS = 14
blender_to_runtime = h.blender_to_runtime
axis_aligned_bounds = h.axis_aligned_bounds
bounds_to_runtime_local = h.bounds_to_runtime_local
mesh_from_triangles = h.mesh_from_triangles
mesh_triangles = h.mesh_triangles
matrix_rows = h.matrix_rows

def make_wall_crown_geometry(wall, parts, house_center_world, crown_material):
    source_points = [blender_to_runtime(v.co) for v in wall.data.vertices]
    if not source_points:
        return None, None
    low, high = axis_aligned_bounds(source_points)
    size = high - low
    name = wall.name
    hero = name.startswith("HeroHouse_")
    if not name.endswith("_Wall_LOD0") or hero:
        return None, None
    if not (0.08 <= (size.z if size.x >= size.z else size.x) <= 0.4
            and size.y >= 1.2 and max(size.x, size.z) >= 1.0):
        return None, None
    along_x = size.x >= size.z
    thickness = size.z if along_x else size.x
    del thickness
    wall_inverse = wall.matrix_world.inverted_safe()
    centre_local = blender_to_runtime(wall_inverse @ house_center_world)
    mid = (low + high) * 0.5
    outward = (1.0 if ((mid.z - centre_local.z) if along_x else (mid.x - centre_local.x)) >= 0.0 else -1.0)
    face = high.z if along_x and outward > 0 else low.z if along_x else high.x if outward > 0 else low.x

    side_prefix = name[:name.index("Wall_LOD0")]
    openings = []
    for part in parts:
        part_name = part.name
        if part is wall or part.type != "MESH" or not part_name.startswith(side_prefix):
            continue
        if not ("Recess" in part_name or "_Glass" in part_name or "_Leaf" in part_name):
            continue
        if not part.data.vertices:
            continue
        corners = bounds_to_runtime_local(part, wall_inverse)
        op_low, op_high = axis_aligned_bounds(corners)
        openings.append((op_low, op_high))

    main = any(side in name for side in ("_Street_Wall_", "_Rear_Wall_", "_Left_Wall_", "_Right_Wall_"))
    side_wall = "_Left_Wall_" in name or "_Right_Wall_" in name
    courses = max(3, round(size.y / WALL_COURSE))
    course = size.y / courses
    start = (low.x if along_x else low.z) - (0.18 if main else 0.0)
    end = (high.x if along_x else high.z) + (0.18 if main else 0.0)
    radius_depth = 0.09
    depth_center = face + outward * (0.035 - radius_depth)
    triangles = []

    def p(along, depth, height):
        return Vector((along, height, depth)) if along_x else Vector((depth, height, along))

    def add_log(from_pos, to_pos, y, depth, ry, rd):
        ring = []
        arc = 0.0
        for i in range(WALL_LOG_SEGMENTS + 1):
            t = math.tau * i / WALL_LOG_SEGMENTS
            dd, yy = rd * math.cos(t), ry * math.sin(t)
            if ring:
                arc += math.hypot(dd - ring[-1][0], yy - ring[-1][1])
            normal = p(0.0, dd / (rd * rd), yy / (ry * ry)).normalized()
            ring.append((dd, yy, normal, arc))
        def uv(length, girth):
            return Vector((length, girth))  # referenceHero = false in runtime
        for i in range(WALL_LOG_SEGMENTS):
            r0, r1 = ring[i], ring[i + 1]
            a = (p(from_pos, depth + r0[0], y + r0[1]), r0[2], uv(from_pos, r0[3]))
            b = (p(to_pos, depth + r0[0], y + r0[1]), r0[2], uv(to_pos, r0[3]))
            c = (p(to_pos, depth + r1[0], y + r1[1]), r1[2], uv(to_pos, r1[3]))
            e = (p(from_pos, depth + r1[0], y + r1[1]), r1[2], uv(from_pos, r1[3]))
            triangles.append((a, b, c, 0, True))
            triangles.append((a, c, e, 0, True))
        for at, sign in ((from_pos, -1.0), (to_pos, 1.0)):
            normal = p(sign, 0.0, 0.0).normalized()
            hub = (p(at, depth, y), normal, Vector((0.5, 0.5)))
            for i in range(WALL_LOG_SEGMENTS):
                r0, r1 = ring[i], ring[i + 1]
                uv0 = Vector((0.5 + 0.5 * r0[0] / rd, 0.5 + 0.5 * r0[1] / ry))
                uv1 = Vector((0.5 + 0.5 * r1[0] / rd, 0.5 + 0.5 * r1[1] / ry))
                a = (p(at, depth, y), normal, Vector((0.5, 0.5)))
                b = (p(at, depth + r0[0], y + r0[1]), normal, uv0)
                c = (p(at, depth + r1[0], y + r1[1]), normal, uv1)
                triangles.append((a, b, c, 0, False))

    generated_log_count = 0
    for k in range(courses):
        centre_y = low.y + course * (k + 0.5) + (course * 0.5 if side_wall else 0.0)
        if centre_y + course * 0.5 > high.y + 0.01:
            continue
        cuts = []
        for op_low, op_high in openings:
            if centre_y + course * 0.42 > op_low.y and centre_y - course * 0.42 < op_high.y:
                cuts.append(((op_low.x - 0.02, op_high.x + 0.02) if along_x
                             else (op_low.z - 0.02, op_high.z + 0.02)))
        spans = [(start, end)]
        for c0, c1 in cuts:
            next_spans = []
            for s0, s1 in spans:
                if c1 <= s0 or c0 >= s1:
                    next_spans.append((s0, s1))
                else:
                    if c0 > s0:
                        next_spans.append((s0, c0))
                    if c1 < s1:
                        next_spans.append((c1, s1))
            spans = next_spans
        for from_pos, to_pos in spans:
            if to_pos - from_pos < 0.2:
                continue
            add_log(from_pos, to_pos, centre_y, depth_center, course * 0.64, radius_depth)
            generated_log_count += 1
    if not triangles:
        return None, {"wall": name, "runtime_log_spans": 0}

    mesh = mesh_from_triangles(
        name.replace("_Wall_LOD0", "_LogCrown") + "Runtime",
        triangles,
        [crown_material],
        weld_coincident_positions=True,
    )

    # The reused helper retains Godot CW faces. Blender/glTF use CCW faces;
    # reverse winding while retaining the source algorithm's outward normals.
    face_normals = [{mesh.loops[li].vertex_index: mesh.loops[li].normal.copy()
                     for li in face.loop_indices} for face in mesh.polygons]
    mesh.flip_normals()
    normals = [None] * len(mesh.loops)
    for polygon, lookup in zip(mesh.polygons, face_normals):
        for li in polygon.loop_indices:
            normals[li] = lookup[mesh.loops[li].vertex_index]
        assert polygon.normal.dot(sum((normals[li] for li in polygon.loop_indices), Vector())) > 0
    mesh.normals_split_custom_set(normals)
    mesh.update()

    return mesh, {
        "wall": name,
        "crown_node": name.replace("_Wall_LOD0", "_LogCrown"),
        "runtime_log_spans": generated_log_count,
        "runtime_log_segments": WALL_LOG_SEGMENTS,
        "wall_face_runtime_axis": "z" if along_x else "x",
        "wall_face_coordinate_runtime": round(float(face), 7),
        "wall_front_triangles_removed": 0,
        "wall_backing_triangles_after": mesh_triangles(wall.data),
        "opening_count": len(openings),
        "local_transform_source": matrix_rows(wall.matrix_basis),
        "godot_clockwise_to_blender_gltf_counterclockwise": True,
    }


def triangle_signature(obj, with_uv=False):
    """Ignore GLB's vertex splitting and triangle order, retain surface geometry."""
    obj.data.calc_loop_triangles()
    uv = obj.data.uv_layers.active
    rows = []
    for tri in obj.data.loop_triangles:
        row = []
        for li in tri.loops:
            vertex = obj.data.vertices[obj.data.loops[li].vertex_index].co
            value = tuple(round(float(c), 5) for c in vertex)
            if with_uv:
                value += tuple(round(float(c), 6) for c in uv.data[li].uv)
            row.append(value)
        rows.append(tuple(sorted(row)))
    return hashlib.sha256(repr(sorted(rows)).encode()).hexdigest()


def triangle_uv_values(obj):
    obj.data.calc_loop_triangles()
    uv = obj.data.uv_layers.active
    rows = []
    for tri in obj.data.loop_triangles:
        vertices = []
        for li in tri.loops:
            position = obj.data.vertices[obj.data.loops[li].vertex_index].co
            vertices.append((tuple(round(float(c), 5) for c in position), tuple(float(c) for c in uv.data[li].uv)))
        rows.append(tuple(sorted(vertices)))
    return [value for row in sorted(rows) for _, point in row for value in point]


def actual_uv_overlap_count(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.context.scene.tool_settings.use_uv_select_sync = False
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="DESELECT")
    bpy.ops.uv.select_overlap(extend=False)
    count = 0
    for ob in objects:
        bm = bmesh.from_edit_mesh(ob.data)
        uv = bm.loops.layers.uv.active
        count += sum(any(loop[uv].select for loop in face.loops) for face in bm.faces)
    bpy.ops.object.mode_set(mode="OBJECT")
    return count


def render_imported(root, meshes):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("DonorNeutralWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.2, .24, .28, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
    for position, energy, size in [((-6, -8, 12), 1800, 8), ((8, 0, 7), 1300, 7), ((0, 9, 10), 1400, 6)]:
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.data.energy, light.data.size = energy, size
        light.rotation_euler = (Vector((0, 1, 2)) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = "ORTHO"
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.render.image_settings.file_format = "PNG"
    views = [
        ("actual-glb-front", (0, -16, 5.8), (.65, 1.3, 2.4), 10.8),
        ("actual-glb-front-right", (12, -13, 8), (.65, 1.3, 2.4), 11.5),
        ("actual-glb-rear-left", (-12, 13, 8), (.65, 1.3, 2.4), 11.5),
        ("actual-glb-corner-openings", (-7, -8, 4.9), (-1.6, -.9, 1.7), 5.3),
    ]
    outputs = []
    for name, position, target, scale in views:
        camera.location = position
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = scale
        path = HERE / (name + ".png")
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append({"path": path.name, "sha256": h.sha256_file(path), "camera": position,
                        "target": target, "orthographic_scale": scale})
    return outputs


def main():
    if bpy.app.version[:3] != (4, 5, 12):
        raise RuntimeError("Pinned Blender 4.5.12 required")
    for path in (GLB, BLEND, RECEIPT):
        if path.exists():
            raise FileExistsError(path)
    assert h.sha256_file(SOURCE) == "fdc69de9f580276f6a9c831bde60a0dad1728acdc533f00320e8a9009c233bf8"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    root = bpy.data.objects[COMPONENT]
    parts = [ob for ob in root.children_recursive if ob.type == "MESH"]
    assert len(parts) == 122 and sum(mesh_triangles(ob.data) for ob in parts) == 3676
    keep = {root, *root.children_recursive}
    for ob in list(bpy.data.objects):
        if ob not in keep:
            if ob == root.parent:
                matrix = root.matrix_world.copy()
                root.parent = None
                root.matrix_world = matrix
            bpy.data.objects.remove(ob, do_unlink=True)
    # Remove only the component's preview-board translation, never its basis.
    preview_location = list(root.location)
    root.location = (0, 0, 0)
    bpy.context.view_layer.update()
    root_inverse = root.matrix_world.inverted_safe()
    records = {}
    for ob in parts:
        ob.data = ob.data.copy()
        records[ob.name] = {"source_triangles": mesh_triangles(ob.data),
                            "source_material_slots": h.object_material_names(ob),
                            "source_uv": h.uv_layer_summary(ob.data),
                            "source_geometry_sha256": triangle_signature(ob),
                            "source_root_relative_transform": matrix_rows(root_inverse @ ob.matrix_world)}
    centre, centre_source = h.hero_centre_world(root, parts)
    material = bpy.data.materials.new("Runtime_VariantA_LogCrown_85715c_PreRegrade")
    material.use_nodes = True
    rgb = [int(v, 16)/255 for v in ("85", "71", "5c")]
    linear = [c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in rgb]
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*linear, 1)
    material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .82
    crowns, crown_stats, hidden_posts = [], [], set()
    for wall in parts:
        if not wall.name.endswith("_Wall_LOD0") or h.object_material_names(wall) != ["URMAN_Wood_Weathered"]:
            continue
        mesh, stats = make_wall_crown_geometry(wall, parts, centre, material)
        if mesh is None:
            continue
        crown = bpy.data.objects.new(stats["crown_node"], mesh)
        bpy.context.scene.collection.objects.link(crown)
        crown.parent = wall.parent
        crown.matrix_basis = wall.matrix_basis.copy()
        crown["runtime_owner"] = "Act1ConnectedWorld.DwellingLogWalls.BuildDwellingLogWalls"
        crowns.append(crown)
        crown_stats.append(stats)
        records[crown.name] = {"source_uv": h.uv_layer_summary(mesh), "generated_from": wall.name,
                               "runtime_geometry": stats, "coincident_position_weld": dict(mesh["coincident_position_weld"])}
        if any(side in wall.name for side in ("_Street_Wall_", "_Rear_Wall_", "_Left_Wall_", "_Right_Wall_")):
            prefix = wall.name[:wall.name.index("Wall_LOD0")].rstrip("_").rsplit("_", 1)[0] + "_Corner_"
            hidden_posts.update(ob.name for ob in parts if ob.name.startswith(prefix))
    assert len(crowns) == 7, [ob.name for ob in crowns]
    included, excluded = [], []
    for ob in parts:
        mats = h.object_material_names(ob)
        reason = ("runtime-hidden plain corner post" if ob.name in hidden_posts else
                  "glass excluded from opaque donor" if any("Glass" in m for m in mats) else
                  "snow excluded from opaque donor" if any("RoofSnow" in m for m in mats) else None)
        if reason:
            excluded.append({"node": ob.name, "reason": reason})
            ob.hide_render = True
            ob.hide_set(True)
        else:
            included.append(ob)
    included += crowns
    geometry_before = {ob.name: triangle_signature(ob) for ob in included}
    atlas = h.build_global_uv_atlas(included, records)
    atlas["source_missing_uv_cause"] = "Selected VariantA opaque source nodes lack UVs; crowns used repeated length/girth UVs. All retained surfaces were reprojected and globally packed."
    assert not atlas["missing_after_pack"]
    atlas["actual_blender_overlap_selected_face_count"] = actual_uv_overlap_count(included)
    assert atlas["actual_blender_overlap_selected_face_count"] == 0
    assert all(triangle_signature(ob) == geometry_before[ob.name] for ob in included)
    assert all(triangle_signature(ob) == records[ob.name]["source_geometry_sha256"] for ob in parts)
    for ob in included:
        records[ob.name]["input_triangles"] = mesh_triangles(ob.data)
        records[ob.name]["input_geometry_sha256"] = triangle_signature(ob)
        records[ob.name]["input_geometry_uv_sha256"] = triangle_signature(ob, with_uv=True)
        records[ob.name]["input_root_relative_transform"] = matrix_rows(root_inverse @ ob.matrix_world)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.object.select_all(action="DESELECT")
    for ob in [root, *included]:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(GLB), export_format="GLB", use_selection=True,
                              export_apply=False, export_texcoords=True, export_normals=True,
                              export_materials="EXPORT", use_visible=False)
    expected = {ob.name: (mesh_triangles(ob.data), triangle_signature(ob), triangle_uv_values(ob),
                         matrix_rows(root_inverse @ ob.matrix_world)) for ob in included}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB))
    imported_root = bpy.data.objects[COMPONENT]
    imported = [ob for ob in imported_root.children_recursive if ob.type == "MESH"]
    assert {ob.name for ob in imported} == set(expected)
    inverse = imported_root.matrix_world.inverted_safe()
    maximum_uv_error = 0.0
    for ob in imported:
        tri, geometry, geometry_uv, transform = expected[ob.name]
        assert mesh_triangles(ob.data) == tri, ob.name
        assert triangle_signature(ob) == geometry, ob.name
        actual_uv = triangle_uv_values(ob)
        assert len(actual_uv) == len(geometry_uv), ob.name
        uv_error = max(abs(a-b) for a,b in zip(actual_uv, geometry_uv))
        maximum_uv_error = max(maximum_uv_error, uv_error)
        assert uv_error <= 1e-6, (ob.name, uv_error)
        actual = matrix_rows(inverse @ ob.matrix_world)
        assert max(abs(a-b) for rowa, rowb in zip(actual, transform) for a,b in zip(rowa,rowb)) < 1e-5, ob.name
    views = render_imported(imported_root, imported)
    receipt = {"schema": "variant-a-opaque-retexture-donor/v1", "status": "source_prepared_construction_review_required",
               "source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": h.sha256_file(SOURCE), "bytes": SOURCE.stat().st_size,
                          "rights": "Project-authored procedural kit; no external material incorporated; no new public license asserted."},
               "dependencies": [{"path": str(p.relative_to(ROOT)), "sha256": h.sha256_file(p)} for p in (HELPER, RUNTIME)],
               "component": COMPONENT, "preview_translation_removed": preview_location,
               "source_mesh_count": len(parts), "source_triangles": 3676,
               "excluded": excluded, "generated_crowns": crown_stats, "center_source": centre_source,
               "opaque_mesh_count": len(imported), "opaque_triangles": sum(mesh_triangles(ob.data) for ob in imported),
               "uv_atlas": atlas, "semantic_nodes": records,
               "invariants": {"all_original_geometry_unchanged": True, "all_export_mesh_names_preserved": True,
                              "export_reimport_triangle_geometry_signatures_equal": True,
                              "export_reimport_uv_max_absolute_error": maximum_uv_error,
                              "export_reimport_uv_error_tolerance": 1e-6,
                              "export_reimport_root_relative_transforms_equal_1e_5": True,
                              "glass_snow_excluded": True, "no_boolean_notches_invented": True},
               "geometry_scope": "Existing C# nonhero crown algorithm only. Original wall backing, roof, entry hall, opening reveals and joinery meshes retained; runtime-hidden corner posts excluded. Later TimberHomeStyle additions/material regrade are not claimed as extracted live state.",
               "output": [{"path": p.name, "sha256": h.sha256_file(p), "bytes": p.stat().st_size} for p in (GLB, BLEND)],
               "actual_glb_views": views, "blender": bpy.app.version_string,
               "construction_review": {
                   "result": "hold_before_paid_retexture",
                   "observed": "Four actual exported-GLB views preserve the original roof, source apertures/reveals and regular courses. Corner closeup shows paired thin elliptic log ends and visible dark diamond-shaped voids between them, rather than a convincing cut/notched corner joint.",
                   "causal_source": "Existing nonhero runtime algorithm uses 0.09 m depth radius, height radius course*0.64, side-wall half-course offset and 0.18 m overhangs; it does not construct saddle/notch geometry. This donor reproduces that algorithm without inventing replacement logs.",
                   "references": ["actual-glb-corner-openings.png", "actual-glb-front-right.png"],
                   "rights_or_api_spend_changed": False,
                   "not_drop_in_runtime": "Donor UVs are a new unique atlas and include runtime-generated crown nodes. Direct canonical replacement would require existing consumer integration to prevent duplicate runtime crown generation. No such integration is made here."},
               "remaining": "Parent construction decision required before any paid retexture. No API upload, generation, runtime promotion or native-performance claim."}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"status": receipt["status"], "meshes": len(imported), "triangles": receipt["opaque_triangles"], "glb_sha256": h.sha256_file(GLB)}))


if __name__ == "__main__":
    main()
