"""Source-only Meshy willow inspection; preserve original GLB, no mesh trimming/export."""
from pathlib import Path
import hashlib
import json
import math
import struct
import sys

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = HERE / "meshy-willow-remesh25k.glb"
TARGET_HEIGHT = 4.535
NEAR_ONLY = "--near-only" in sys.argv
LOD_PREP = "--lod-prep" in sys.argv or NEAR_ONLY


def point_segment(p, a, b):
    ab = b - a
    if ab.length_squared < 1e-20:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def cross(a, b):
    return a.x * b.y - a.y * b.x


def segment_distance(a, b, c, d):
    # Include crossings, including projected triangle edges crossing the route.
    ab, cd = b - a, d - c
    denominator = cross(ab, cd)
    if abs(denominator) > 1e-12:
        t = cross(c - a, cd) / denominator
        u = cross(c - a, ab) / denominator
        if 0 <= t <= 1 and 0 <= u <= 1:
            return 0.0
    return min(point_segment(a, c, d), point_segment(b, c, d),
               point_segment(c, a, b), point_segment(d, a, b))


def inside(p, polygon):
    if abs(sum(cross(a, polygon[(i + 1) % len(polygon)]) for i, a in enumerate(polygon))) < 1e-10:
        return False
    signs = [cross(polygon[(i + 1) % len(polygon)] - a, p - a)
             for i, a in enumerate(polygon)]
    return all(s >= -1e-10 for s in signs) or all(s <= 1e-10 for s in signs)


def clip_height(polygon, maximum):
    result = []
    for i, a in enumerate(polygon):
        b = polygon[(i + 1) % len(polygon)]
        if a.z <= maximum:
            result.append(a)
        if (a.z <= maximum) != (b.z <= maximum):
            result.append(a + (b - a) * ((maximum - a.z) / (b.z - a.z)))
    return result


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
parts = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
raw_points = [obj.matrix_world @ vertex.co for obj in parts for vertex in obj.data.vertices]
low = Vector(tuple(min(p[i] for p in raw_points) for i in range(3)))
high = Vector(tuple(max(p[i] for p in raw_points) for i in range(3)))
raw_height = high.z - low.z
# Root comes from the lowest 1% of height, rather than the asymmetrical crown centre.
base = [p for p in raw_points if p.z <= low.z + raw_height * .01]
root = Vector(((min(p.x for p in base) + max(p.x for p in base)) * .5,
               (min(p.y for p in base) + max(p.y for p in base)) * .5, low.z))
scale = TARGET_HEIGHT / raw_height
for obj in parts:
    matrix = obj.matrix_world.copy()
    for vertex in obj.data.vertices:
        vertex.co = (matrix @ vertex.co - root) * scale
    obj.parent = None
    obj.matrix_world.identity()
    obj.data.update()
points = [vertex.co.copy() for obj in parts for vertex in obj.data.vertices]
minimum = [min(p[i] for p in points) for i in range(3)]
maximum = [max(p[i] for p in points) for i in range(3)]

# Godot route XZ -> Blender XY. Left root is (74.4,114); route y is 2 m.
a = Vector((66 - 74.4, -(103 - 114)))
b = Vector((78 - 74.4, -(117 - 114)))
root_distance = point_segment(Vector((0, 0)), a, b)
# This root lies within ShoreReach, so existing TerrainHeight is 2-.12*d.
root_ground = 2.0 - .12 * root_distance
route_above_root = 2.0 - root_ground
triangles = []
for obj in parts:
    obj.data.calc_loop_triangles()
    triangles.extend([[obj.data.vertices[i].co.copy() for i in tri.vertices]
                      for tri in obj.data.loop_triangles])


def route_clearance(height):
    distance = float("inf")
    eligible = 0
    for tri in triangles:
        poly = clip_height(tri, route_above_root + height)
        if len(poly) < 3:
            continue
        xy = [Vector((p.x, p.y)) for p in poly]
        eligible += 1
        if inside(a, xy) or inside(b, xy):
            distance = 0.0
            break
        distance = min(distance, *(segment_distance(a, b, c, xy[(i + 1) % len(xy)])
                                   for i, c in enumerate(xy)))
    return {"standing_height_m": height, "route_feet_above_root_m": route_above_root,
            "minimum_projected_geometry_distance_m": distance,
            "conservative_cylinder_radius_m": .35,
            "clearance_after_radius_m": distance - .35,
            "method": "all triangles clipped below route feet + height; projected exact segment/polygon distance; conservative standing cylinder, not physics/native proof"}


data = SOURCE.read_bytes()
metadata_size = struct.unpack_from("<I", data, 12)[0]
header = json.loads(data[20:20 + metadata_size])
bin_offset = 28 + metadata_size


def image_metadata(item):
    view = header["bufferViews"][item["bufferView"]]
    width, height = struct.unpack_from(">II", data, bin_offset + view.get("byteOffset", 0) + 16)
    return {"name": item.get("name"), "mimeType": item.get("mimeType"), "png_width_height": [width, height]}


metrics = {
    "source": SOURCE.name, "source_bytes": len(data), "source_sha256": hashlib.sha256(data).hexdigest(),
    "blender": bpy.app.version_string, "triangles": len(triangles),
    "vertices": len(points), "material_count": len(header.get("materials", [])),
    "embedded_images": [image_metadata(item) for item in header.get("images", [])],
    "normalization": {"axes": "Blender X,Y,Z = Godot/glTF X,-Z,Y", "uniform_scale": scale,
                      "raw_blender_min": list(low), "raw_blender_max": list(high),
                      "raw_root": list(root), "root_method": "XZ centre of bottom 1% height vertex bounds; minimum Z ground",
                      "target_height_m": TARGET_HEIGHT, "trimming": False, "original_modified": False},
    "normalized_blender_min": minimum, "normalized_blender_max": maximum,
    "normalized_width_depth_height_m": [maximum[i] - minimum[i] for i in range(3)],
    "caps_width_depth_m": [4.03, 3.33],
    "original_measured_width_depth_m": [3.845845222, 3.087029815],
    "lowest_vertex_outside_trunk_radius_m": {
        str(radius): min((p.z for p in points if math.hypot(p.x, p.y) > radius), default=None)
        for radius in (.3, .4, .5, .75)},
    "lowest_geometry_above_root_flare_m": {
        "exclude_below_height_m": .75, "exclude_trunk_radius_m": .25,
        "lowest_vertex_height_m": min(p.z for p in points if p.z > .75 and math.hypot(p.x, p.y) > .25),
        "required_crown_minimum_m": 2.165,
        "method": "geometry radial bound, not semantic material separation; four views show a single bare trunk inside this lower radius"},
    "lower_boundary_caveat": "one baked material; radius thresholds are geometry measures, not semantic leaf/branch labels; verify rendered anatomy",
    "left_root_route_distance_m": root_distance, "left_root_ground_y_m": root_ground,
    "route_clearance": [route_clearance(1.8), route_clearance(2.15)],
    "runtime_collision": "original procedural tree has no trunk/canopy contact; no new contact created",
    "source_candidate": "uniformly normalized in memory by this recipe; no duplicated GLB or dependent glTF before root review",
}
if not LOD_PREP:
    (HERE / "inspection_metrics.json").write_text(json.dumps(metrics, indent=2) + "\n")
print("WILLOW_METRICS " + json.dumps(metrics), flush=True)

lods = parts
lod_report = None
if LOD_PREP:
    import bmesh
    import numpy as np

    output = HERE / ("summer-willow-near-2k-v1.glb" if NEAR_ONLY else "summer-willow-meshy-v1.glb")
    receipt = HERE / ("near-only-structure.json" if NEAR_ONLY else "candidate-structure.json")
    assert not output.exists() and not receipt.exists(), "Do not overwrite an existing LOD candidate"
    assert len(parts) == 1 and len(parts[0].data.materials) == 1
    near = parts[0]
    near.name = "SummerWillow_LOD0"
    near.data.name = near.name + "_Mesh"
    material = near.data.materials[0]
    material.name = "URMAN_SummerWillow_Meshy_v1_PBR"
    normal_images = {link.from_node.image for node in material.node_tree.nodes if node.type == "NORMAL_MAP"
                     for link in node.inputs["Color"].links if link.from_node.type == "TEX_IMAGE"}
    image_report = []
    handled = set()
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or not node.image or node.image in handled:
            continue
        image = node.image
        handled.add(image)
        old = list(image.size)
        role = "tangent_normal" if image in normal_images else (
            "basecolor" if image.colorspace_settings.name == "sRGB" else "metallic_roughness_data")
        ratio = min(1.0, 2048 / max(old))
        new = [max(1, round(x * ratio)) for x in old]
        image.scale(*new)
        if role == "tangent_normal":
            # Preserve tangent axes; average encoded data, then normalize the vector.
            pixels = np.empty(new[0] * new[1] * 4, dtype=np.float32)
            image.pixels.foreach_get(pixels)
            pixels = pixels.reshape((-1, 4))
            vectors = pixels[:, :3] * 2.0 - 1.0
            lengths = np.linalg.norm(vectors, axis=1)
            good = lengths > 1e-8
            vectors[good] /= lengths[good, None]
            vectors[~good] = (0, 0, 1)
            pixels[:, :3] = vectors * .5 + .5
            image.pixels.foreach_set(pixels.ravel())
            image.update()
        image.file_format = "PNG"
        image.pack()
        image_report.append({"name": image.name, "role": role, "colorspace": image.colorspace_settings.name,
                             "before": old, "after": new, "normal_vector_renormalized": role == "tangent_normal"})
    lods = [near]
    welds = []
    topology = None
    if NEAR_ONLY:
        # Read-only topology diagnosis on a disposable BMesh, never the retained near.
        bm = bmesh.new()
        bm.from_mesh(near.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=.000001)
        pending = set(bm.verts)
        components = []
        while pending:
            seed = pending.pop()
            stack, component = [seed], {seed}
            while stack:
                for edge in stack.pop().link_edges:
                    for other in edge.verts:
                        if other in pending:
                            pending.remove(other)
                            component.add(other)
                            stack.append(other)
            faces = {face for vertex in component for face in vertex.link_faces}
            components.append((len(component), len(faces)))
        histogram = {}
        for vertices, faces in components:
            histogram[str(faces)] = histogram.get(str(faces), 0) + 1
        topology = {"method": "transient bmesh coincident weld1e-6; connected vertices through actual edges",
                    "welded_vertices": len(bm.verts), "faces": len(bm.faces),
                    "components": len(components), "component_face_count_histogram": histogram,
                    "boundary_edges": sum(edge.is_boundary for edge in bm.edges),
                    "nonmanifold_edges": sum(not edge.is_manifold for edge in bm.edges),
                    "near_geometry_modified": False,
                    "limit": "observed topology is not proof of a theoretical decimator minimum"}
        bm.free()
    for level, budget in (() if NEAR_ONLY else ((1, 4500), (2, 800))):
        obj = near.copy()
        obj.data = near.data.copy()
        obj.name = f"SummerWillow_LOD{level}"
        obj.data.name = obj.name + "_Mesh"
        bpy.context.collection.objects.link(obj)
        # Existing hero/neighbor source pattern: restore coincident UV-seam vertices,
        # retaining per-corner UV loops before ordinary quadric edge collapse.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        before = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=.000001)
        bm.normal_update()
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        welds.append({"node": obj.name, "vertices_before": before, "vertices_after": len(obj.data.vertices),
                      "distance_m": .000001})
        obj.data.calc_loop_triangles()
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("BudgetLOD", "DECIMATE")
        modifier.ratio = (budget - .5) / len(obj.data.loop_triangles)
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.data.update()
        lods.append(obj)
    measurements = []
    for obj in lods:
        vertices = [v.co for v in obj.data.vertices]
        obj.data.calc_loop_triangles()
        triangles = [[obj.data.vertices[i].co.copy() for i in tri.vertices] for tri in obj.data.loop_triangles]
        box = [[min(p[i] for p in vertices), max(p[i] for p in vertices)] for i in range(3)]
        measurements.append({"node": obj.name, "triangles": len(triangles), "vertices": len(vertices),
                             "bounds_blender_xyz_m": box,
                             "width_depth_height_m": [hi - lo for lo, hi in box],
                             "root_ground_z_m": box[2][0],
                             "lowest_geometry_above_root_flare_m": min(
                                 p.z for p in vertices if p.z > .75 and math.hypot(p.x, p.y) > .25),
                             "route_clearance": [route_clearance(1.8), route_clearance(2.15)],
                             "matrix_world": [list(row) for row in obj.matrix_world]})
    assert measurements[0]["triangles"] == 19031
    budget_pass = NEAR_ONLY or (measurements[1]["triangles"] <= 4500 and measurements[2]["triangles"] <= 800)
    root_object = bpy.data.objects.new("SummerWillowMeshyRoot", None)
    bpy.context.collection.objects.link(root_object)
    for obj in lods:
        obj.parent = root_object
    bpy.ops.object.select_all(action="DESELECT")
    root_object.select_set(True)
    for obj in lods:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = near
    if budget_pass:
        bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB", use_selection=True,
                                  export_yup=True, export_texcoords=True, export_normals=True,
                                  export_tangents=True, export_materials="EXPORT", export_animations=False,
                                  export_image_format="AUTO")
    lod_report = {"status": "near_only_source_candidate_mid_far_HOLD_not_runtime_or_art_acceptance" if NEAR_ONLY else (
                      "source_candidate_pending_root_visual_review_not_runtime_or_art_acceptance" if budget_pass else "HOLD_collapse_did_not_reach_lod_budget_no_glb_export"),
                  "source": SOURCE.name, "source_sha256": metrics["source_sha256"],
                  "output": output.name if output.exists() else None, "bytes": output.stat().st_size if output.exists() else None,
                  "sha256": hashlib.sha256(output.read_bytes()).hexdigest() if output.exists() else None,
                  "budget_pass": None if NEAR_ONLY else budget_pass,
                  "budget_scope": "near19031 only; full three-LOD budget NOT passed" if NEAR_ONLY else "all three LODs",
                  "mid_far_status": "HOLD; candidate-structure.json and candidate-lod1/2.png preserve the failed ordinary collapse" if NEAR_ONLY else "inspect actual counts and images",
                  "source_topology": topology,
                  "normalization": metrics["normalization"], "lods": measurements,
                  "welds": welds, "images": image_report, "shared_material_count": 1,
                  "material": material.name, "new_contact": False,
                  "additional_meshy_credits": 0, "geometry_clamp_or_trimming": False,
                  "method_references": ["act1/winter_spruce_meshy_v2/prepare_candidate.py",
                                        "act1/house_a_meshy_v2/derive_and_inspect.py"],
                  "render_scope": "candidate-lod0.png is an in-memory pre-export Blender view of the normalized near mesh and resized2K maps; it is NOT a reimported-GLB render or a game frame",
                  "actual_export_verification": "near-export-header.json separately records exported GLB nodes, triangles, material, position accessor bounds, embedded PNG sizes/depth; no reimported pixels or native gameplay acceptance",
                  "renders": ["candidate-lod0.png"] if NEAR_ONLY else [f"candidate-lod{level}.png" for level in range(3)]}
    receipt.write_text(json.dumps(lod_report, indent=2) + "\n")
    print("WILLOW_LOD_METRICS " + json.dumps(lod_report), flush=True)

scene = bpy.context.scene
world = scene.world or bpy.data.worlds.new("NeutralInspectionWorld")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (.36, .42, .48, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = .65
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.008))
ground = bpy.context.object
ground.name = "InspectionOnlyNeutralGround"
material = bpy.data.materials.new("InspectionOnlyNeutralGround")
material.diffuse_color = (.36, .36, .36, 1)
ground.data.materials.append(material)
bpy.ops.object.light_add(type="AREA", location=(-3, -6, 9))
bpy.context.object.data.energy = 900
bpy.context.object.data.size = 6
bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.type = "ORTHO"
camera.data.ortho_scale = max(6.1, *(1.6 * (maximum[i] - minimum[i]) for i in range(2)))
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"
views = [] if NEAR_ONLY else [(f"candidate-lod{level}", (7, -10, 5.5), level) for level in range(3)] if LOD_PREP else [
    ("normalized-willow-front-right", (7, -10, 5.5), 0),
    ("normalized-willow-front-left", (-7, -10, 5.5), 0),
    ("normalized-willow-rear-right", (7, 10, 5.5), 0),
    ("normalized-willow-rear-left", (-7, 10, 5.5), 0)]
for name, location, visible_level in views:
    for level, obj in enumerate(lods):
        obj.hide_render = level != visible_level
    camera.location = location
    camera.rotation_euler = (Vector((0, 0, 2.25)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(HERE / f"{name}.png")
    bpy.ops.render.render(write_still=True)
print("WILLOW_INSPECTION_COMPLETE", flush=True)
