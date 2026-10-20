"""Prepare only the existing generated fence mesh; no runtime or canonical kit writes."""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "meshy71-rough-picket-remesh-25k.glb"
OUT = HERE / "metric_candidate_v1"
EXPECTED_SHA = "33a0157c831a6533a214cc6d12da6f4e3f96901fc5214c23f1ff07e6f8bba7c9"


def section_intervals(objects, height):
    intervals = []
    for ob in objects:
        ob.data.calc_loop_triangles()
        for tri in ob.data.loop_triangles:
            points = [ob.matrix_world @ ob.data.vertices[i].co for i in tri.vertices]
            hits = []
            for a, b in zip(points, points[1:] + points[:1]):
                if (a.z <= height < b.z) or (b.z <= height < a.z):
                    hits.append(a.lerp(b, (height - a.z) / (b.z - a.z)))
            if len(hits) >= 2:
                intervals.append((min(p.x for p in hits), max(p.x for p in hits)))
    merged = []
    for low, high in sorted(intervals):
        if merged and low <= merged[-1][1] + .0001:
            merged[-1][1] = max(merged[-1][1], high)
        else:
            merged.append([low, high])
    return merged


def mesh_bounds(objects):
    points = [ob.matrix_world @ v.co for ob in objects for v in ob.data.vertices]
    return [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)]


def section_depth(objects, height, interval):
    depths = []
    for ob in objects:
        ob.data.calc_loop_triangles()
        for tri in ob.data.loop_triangles:
            points = [ob.matrix_world @ ob.data.vertices[i].co for i in tri.vertices]
            for a, b in zip(points, points[1:] + points[:1]):
                if (a.z <= height < b.z) or (b.z <= height < a.z):
                    hit = a.lerp(b, (height - a.z) / (b.z - a.z))
                    if interval[0] <= hit.x <= interval[1]:
                        depths.append(hit.y)
    return max(depths) - min(depths)


def weld_split_geometry(objects, reading_height):
    before = section_intervals(objects, reading_height)
    groups = {}
    vertices_before = sum(len(ob.data.vertices) for ob in objects)
    for ob in objects:
        uv = ob.data.uv_layers.active
        for loop in ob.data.loops:
            position = tuple(ob.matrix_world @ ob.data.vertices[loop.vertex_index].co)
            group = groups.setdefault(position, {"vertices": set(), "uvs": set()})
            group["vertices"].add((ob.name, loop.vertex_index))
            if uv:
                group["uvs"].add(tuple(uv.data[loop.index].uv))
    report = {"vertices_before": vertices_before, "unique_exact_positions": len(groups),
              "exact_coincident_vertex_excess": sum(len(g["vertices"])-1 for g in groups.values()),
              "coincident_position_groups_with_uv_seams": sum(len(g["vertices"]) > 1 and len(g["uvs"]) > 1 for g in groups.values()),
              "merge_distance_source_units": .000001,
              "minimum_actual_gap_source_units": min(b[0]-a[1] for a,b in zip(before, before[1:]))}
    assert report["exact_coincident_vertex_excess"] > 0, "Do not guess a split-vertex cause"
    assert report["minimum_actual_gap_source_units"] > .000001 * 100
    for ob in objects:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(ob.data)
        bm.free()
        bpy.context.view_layer.objects.active = ob
        if ob.data.has_custom_normals:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        ob.data.update()
    after = section_intervals(objects, reading_height)
    report["vertices_after"] = sum(len(ob.data.vertices) for ob in objects)
    report["source_section_solids_before_after"] = [len(before), len(after)]
    report["section_endpoint_max_change"] = max(abs(x-y) for a,b in zip(before, after) for x,y in zip(a,b))
    assert len(before) == len(after) == 15 and report["section_endpoint_max_change"] < .00001
    print("rough-picket-weld-proof: " + json.dumps(report))
    return report


def prepare(objects, low, high, intervals, reading_height, weld_report=None):
    output_dir = OUT / "welded" if weld_report else OUT
    assert len(objects) == 1 and len(intervals) == 15, "Expected two posts and thirteen real pickets"
    assert not output_dir.exists(), "Never overwrite a prior candidate"
    targets = [[-1.235, -1.115]]
    targets += [[(i-6)*.17-.0475, (i-6)*.17+.0475] for i in range(13)]
    targets += [[1.115, 1.235]]
    knots = [(a, x) for (a, b), (x, y) in zip(intervals, targets)]
    knots += [(b, y) for (a, b), (x, y) in zip(intervals, targets)]
    knots.sort()

    def fit_x(value):
        for (a, x), (b, y) in zip(knots, knots[1:]):
            if a <= value <= b:
                return x + (value-a)/(b-a)*(y-x)
        a, x = knots[0] if value < knots[0][0] else knots[-2]
        b, y = knots[1] if value < knots[0][0] else knots[-1]
        return x + (value-a)/(b-a)*(y-x)

    depths = sorted(section_depth(objects, reading_height, interval) for interval in intervals[1:-1])
    depth_scale = .028 / depths[len(depths)//2]
    height_scale = 1.29 / (high.z-low.z)
    source = objects[0]
    source_matrix = source.matrix_world.copy()
    for vertex in source.data.vertices:
        p = source_matrix @ vertex.co
        vertex.co = Vector((fit_x(p.x), (p.y-(low.y+high.y)*.5)*depth_scale,
                            (p.z-low.z)*height_scale))
    source.matrix_world.identity()
    source.data.update()
    # Keep the actual generated surface and its UVs. The only geometry operations
    # are the measured monotone fit and ordinary collapse LODs, never box replacement.
    root = bpy.data.objects.new("URMAN_RoughPicketGeneratedCandidate", None)
    bpy.context.collection.objects.link(root)
    lods = []
    measurements = []
    for level, budget in enumerate((2800, 1000, 500)):
        ob = source.copy()
        ob.data = source.data.copy()
        ob.name = f"RoughPicketGenerated_LOD{level}"
        ob.parent = root
        bpy.context.collection.objects.link(ob)
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        ob.data.calc_loop_triangles()
        modifier = ob.modifiers.new("BoundedGeneratedLOD", "DECIMATE")
        modifier.ratio = min(1.0, budget / len(ob.data.loop_triangles))
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        ob.data.calc_loop_triangles()
        solids = section_intervals([ob], .70)
        gaps = [b[0]-a[1] for a,b in zip(solids, solids[1:])]
        measurements.append({"name": ob.name, "triangles": len(ob.data.loop_triangles),
                             "bounds_blenderXYZ_m": mesh_bounds([ob]),
                             "reading_band_height_m": .70, "solid_intervals": solids,
                             "through_gap_count": len(gaps), "minimum_through_gap_m": min(gaps),
                             "picket_widths_m": [b-a for a,b in solids[1:-1]],
                             "picket_depths_m": [section_depth([ob], .70, interval) for interval in solids[1:-1]],
                             "metric_gate": len(solids) == 15 and min(gaps) >= .045})
        lods.append(ob)
        ob.select_set(False)
    bpy.data.objects.remove(source, do_unlink=True)
    # All LODs share the original generated material/UV atlas, reduced once.
    for image in bpy.data.images:
        if image.size[0] and image.size[1] and max(image.size) > 1024:
            scale = 1024 / max(image.size)
            image.scale(max(1, round(image.size[0]*scale)), max(1, round(image.size[1]*scale)))
            image.pack()
    output_dir.mkdir(parents=True)
    for ob in bpy.context.selected_objects:
        ob.select_set(False)
    root.select_set(True)
    for ob in lods:
        ob.select_set(True)
    output = output_dir / "rough-picket-generated-metric-v1.glb"
    bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB", use_selection=True,
                              export_apply=True, export_tangents=True, export_animations=False,
                              export_image_format="AUTO")
    bpy.ops.wm.save_as_mainfile(filepath=str(output_dir / "rough-picket-generated-metric-v1.blend"))
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("CandidateInspectionWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.36, .42, .48, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
    scene.world = world
    bpy.ops.object.light_add(type="AREA", location=(1.5, -3.5, 4))
    bpy.context.object.data.energy, bpy.context.object.data.size = 600, 3
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type, camera.data.ortho_scale = "ORTHO", 3.1
    scene.camera = camera
    clay = bpy.data.materials.new("CandidateGeometryClay")
    clay.diffuse_color = (.4, .4, .4, 1)
    for ob in lods[1:]:
        ob.hide_render = True
    renders = []
    for name, location, clay_view in [("front-generated-textured", (0, -6, .95), False),
                                       ("rear-quarter-actual-clay", (3, 5, 2.5), True)]:
        target = Vector((0, 0, .65))
        camera.location = location
        camera.rotation_euler = (target-camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.view_layers[0].material_override = clay if clay_view else None
        scene.render.filepath = str(output_dir / (name+".png"))
        bpy.ops.render.render(write_still=True)
        renders.append(name+".png")
    receipt = {"status": "source_candidate_not_runtime_or_art_accepted",
               "source": SOURCE.name, "source_sha256": EXPECTED_SHA,
               "source_geometry_preserved": "Measured monotone vertex fit and collapse LODs; no replacement boxes",
               "split_geometry_repair": weld_report,
               "post_centres_target_m": [-1.175, 1.175], "picket_count": 13,
               "picket_width_target_m": .095, "picket_pitch_m": .17,
               "post_height_target_m": 1.29, "depth_scale": depth_scale,
               "lods": measurements, "renders": renders,
               "output": output.name, "output_bytes": output.stat().st_size,
               "output_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
               "collision_policy": "Visual only. Existing YardFences/YardFenceBody terrain/contact owner is unchanged; no gate or hinge.",
               "remaining": ["Source crown variation and upper-rail crease are retained, not repaired or accepted.",
                             "Final metric/shape review and export reimport inspection required.",
                             "Existing batched ArrayMesh/MultiMesh consumer, native framing/motion and performance proof remain future work."],
               "new_meshy_calls": 0, "new_meshy_credits": 0}
    (output_dir / "candidate-receipt.json").write_text(json.dumps(receipt, indent=2)+"\n")
    print("rough-picket-candidate: " + json.dumps(receipt))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--inspect-only", action="store_true")
    parser.add_argument("--weld", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == EXPECTED_SHA
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    objects = [ob for ob in bpy.data.objects if ob.type == "MESH"]
    points = [ob.matrix_world @ vertex.co for ob in objects for vertex in ob.data.vertices]
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    reading_height = low.z + .70 / 1.29 * (high.z - low.z)
    intervals = section_intervals(objects, reading_height)
    print("rough-picket-section: " + json.dumps({"bounds": [list(low), list(high)],
          "reading_height": reading_height, "intervals": intervals,
          "widths": [b-a for a,b in intervals],
          "gaps": [b[0]-a[1] for a,b in zip(intervals, intervals[1:])]}))
    print("rough-picket-interval-bounds: " + json.dumps([
        {"x": [a, b], "bounds": [[min(p[k] for p in points if a <= p.x <= b),
                                  max(p[k] for p in points if a <= p.x <= b)] for k in (1, 2)]}
        for a, b in intervals]))
    weld_report = weld_split_geometry(objects, reading_height) if args.weld else None
    if args.inspect_only:
        return
    prepare(objects, low, high, intervals, reading_height, weld_report)


if __name__ == "__main__":
    main()
