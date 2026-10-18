"""One manual image-landmark eye fit and localized secondary-spike cleanup.

Source-only DCC study. The immutable closed-skin TriPoSR OBJ is the only mesh
input. Eye centers are derived by inverting the exact orthographic front-camera
projection used by refine-v2 from the two human-reviewed pixel landmarks. No
depth-minimum search or runtime export is used.
"""

import hashlib
import math
import sys
from collections import defaultdict
from pathlib import Path
from statistics import median

import bpy
import bmesh
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def find_repo_root():
    for parent in Path(__file__).resolve().parents:
        if (parent / "AGENTS.md").is_file():
            return parent
    raise RuntimeError("Could not find repository root from this script")


ROOT = find_repo_root()
OUTPUT_DIR = ROOT / "assets/source/blender/photoworlds/W01/shurale_reconstruction_v1/refine-v3"
SOURCE_OBJ = ROOT / "assets/source/blender/photoworlds/W01/shurale_reconstruction_v1/triposr-output/head_mesh/0/mesh.obj"
SOURCE_SHA256 = "e770cd1dbd54a337ee2249b0720222345ffde4d119007eebd2cb0e30b8486455"
FRONT_DIRECTION = Vector((1.0, 0.0, 0.0))
LANDMARK_PIXELS = ((545.0, 655.0), (827.0, 677.0))
RENDER_RESOLUTION = (1200, 1400)


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection" and collection.users == 0:
            bpy.data.collections.remove(collection)


def read_first_obj_vertex(path):
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("v "):
                return tuple(float(value) for value in line.split()[1:4])
    raise RuntimeError("OBJ contains no vertex records")


def connected_components(mesh):
    parent = list(range(len(mesh.vertices)))
    sizes = [1] * len(mesh.vertices)

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(first, second):
        first_root, second_root = find(first), find(second)
        if first_root == second_root:
            return
        if sizes[first_root] < sizes[second_root]:
            first_root, second_root = second_root, first_root
        parent[second_root] = first_root
        sizes[first_root] += sizes[second_root]

    for polygon in mesh.polygons:
        for index in polygon.vertices[1:]:
            union(polygon.vertices[0], index)
    groups = defaultdict(list)
    for index in range(len(mesh.vertices)):
        groups[find(index)].append(index)
    return sorted(groups.values(), key=len, reverse=True)


def remove_disconnected_chips(obj, island_limit=100):
    mesh = obj.data
    components = connected_components(mesh)
    keep = set(components[0])
    removed = [part for part in components[1:] if len(part) < island_limit]
    if len(removed) != len(components) - 1:
        raise RuntimeError("Found a disconnected component above the conservative island limit")
    color = mesh.attributes.get("Color")
    if color is None or color.domain != "POINT":
        raise RuntimeError("Expected source POINT-domain Color attribute")
    colors = [tuple(item.color) for item in color.data]
    coords = [tuple(vertex.co) for vertex in mesh.vertices]
    remap, kept_coords, kept_colors = {}, [], []
    for old_index, coord in enumerate(coords):
        if old_index in keep:
            remap[old_index] = len(kept_coords)
            kept_coords.append(coord)
            kept_colors.append(colors[old_index])
    faces = [tuple(remap[index] for index in poly.vertices)
             for poly in mesh.polygons if all(index in remap for index in poly.vertices)]
    removed_faces = sum(1 for poly in mesh.polygons if any(index not in keep for index in poly.vertices))
    mesh.attributes.remove(color)
    mesh.clear_geometry()
    mesh.from_pydata(kept_coords, [], faces)
    mesh.update()
    new_color = mesh.attributes.new(name="Color", type="FLOAT_COLOR", domain="POINT")
    for item, value in zip(new_color.data, kept_colors):
        item.color = value
    return {
        "components_before": len(components),
        "retained_vertices": len(components[0]),
        "removed_component_sizes": [len(part) for part in removed],
        "removed_vertices": sum(len(part) for part in removed),
        "removed_faces": removed_faces,
    }


def make_review_camera(scene, bounds):
    data = bpy.data.cameras.new("RefineV3FrontProjectionCamera")
    camera = bpy.data.objects.new("RefineV3FrontProjectionCamera", data)
    scene.collection.objects.link(camera)
    center = Vector((0.0, 0.0, 0.0))
    scale = max(high - low for low, high in bounds)
    camera.location = center + FRONT_DIRECTION * (scale * 3.25)
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = scale * 1.20
    scene.camera = camera
    scene.render.resolution_x, scene.render.resolution_y = RENDER_RESOLUTION
    scene.render.resolution_percentage = 100
    bpy.context.view_layer.update()
    return camera, scale


def pixel_to_source_yz(scene, camera, pixel):
    """Invert Blender's actual orthographic projection on the X=0 plane."""
    width = scene.render.resolution_x * scene.render.resolution_percentage / 100.0
    height = scene.render.resolution_y * scene.render.resolution_percentage / 100.0
    px, py = pixel
    target = Vector((px / width, 1.0 - py / height))
    origin = world_to_camera_view(scene, camera, Vector((0.0, 0.0, 0.0)))
    y_projection = world_to_camera_view(scene, camera, Vector((0.0, 1.0, 0.0))) - origin
    z_projection = world_to_camera_view(scene, camera, Vector((0.0, 0.0, 1.0))) - origin
    a, b = y_projection.x, z_projection.x
    c, d = y_projection.y, z_projection.y
    determinant = a * d - b * c
    if abs(determinant) < 1e-10:
        raise RuntimeError("Front camera projection is singular on the YZ plane")
    dx, dy = target.x - origin.x, target.y - origin.y
    y = (dx * d - b * dy) / determinant
    z = (a * dy - dx * c) / determinant
    roundtrip = world_to_camera_view(scene, camera, Vector((0.0, y, z)))
    err_x = abs(roundtrip.x * width - px)
    err_y = abs((1.0 - roundtrip.y) * height - py)
    if max(err_x, err_y) > 0.05:
        raise RuntimeError(f"Landmark projection roundtrip failed: {err_x:.3f}px, {err_y:.3f}px")
    return {"pixel": tuple(pixel), "yz": (y, z), "roundtrip_error_px": (err_x, err_y)}


def build_bvh(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        # Decimation can leave n-gons even when collapse triangulation is
        # requested. Build the query BVH from Blender's evaluated tessellation
        # without changing the retained source mesh.
        mesh.calc_loop_triangles()
        return BVHTree.FromPolygons(
            [vertex.co.copy() for vertex in mesh.vertices],
            [list(triangle.vertices) for triangle in mesh.loop_triangles],
            all_triangles=True,
        )
    finally:
        evaluated.to_mesh_clear()


def build_color_surface_sampler(mesh):
    color = mesh.attributes.get("Color")
    if color is None or color.domain != "POINT" or color.data_type != "FLOAT_COLOR":
        raise RuntimeError("Color transfer requires the source POINT/FLOAT_COLOR attribute")
    mesh.calc_loop_triangles()
    positions = [vertex.co.copy() for vertex in mesh.vertices]
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    colors = [tuple(item.color) for item in color.data]
    max_source_rgb = max(max(value[:3]) for value in colors)
    if max_source_rgb >= 0.98:
        raise RuntimeError(f"Source skin already contains near-white points: max={max_source_rgb}")
    tree = BVHTree.FromPolygons(positions, triangles, all_triangles=True)
    return {"positions": positions, "triangles": triangles, "colors": colors,
            "tree": tree, "max_source_rgb": max_source_rgb}


def barycentric_weights(point, first, second, third):
    edge_a = second - first
    edge_b = third - first
    offset = point - first
    dot_aa = edge_a.dot(edge_a)
    dot_ab = edge_a.dot(edge_b)
    dot_bb = edge_b.dot(edge_b)
    dot_pa = offset.dot(edge_a)
    dot_pb = offset.dot(edge_b)
    denominator = dot_aa * dot_bb - dot_ab * dot_ab
    if abs(denominator) < 1e-16:
        raise RuntimeError("Color transfer encountered a degenerate source triangle")
    weight_b = (dot_bb * dot_pa - dot_ab * dot_pb) / denominator
    weight_c = (dot_aa * dot_pb - dot_ab * dot_pa) / denominator
    weights = [1.0 - weight_b - weight_c, weight_b, weight_c]
    weights = [max(0.0, value) for value in weights]
    total = sum(weights)
    if total <= 1e-10:
        raise RuntimeError("Could not derive stable source-surface color weights")
    return [value / total for value in weights]


def transfer_generated_white_colors(target_mesh, sampler, max_distance=0.02):
    color = target_mesh.attributes.get("Color")
    if color is None or color.domain != "POINT" or color.data_type != "FLOAT_COLOR":
        raise RuntimeError("Boolean target lost its POINT/FLOAT_COLOR attribute")
    generated = [vertex for vertex in target_mesh.vertices
                 if min(color.data[vertex.index].color[:3]) >= 0.98]
    distances = []
    for vertex in generated:
        location, _normal, triangle_index, distance = sampler["tree"].find_nearest(vertex.co)
        if location is None or triangle_index < 0 or distance > max_distance:
            raise RuntimeError(
                f"Generated Boolean vertex has no nearby source color surface: {distance}"
            )
        triangle = sampler["triangles"][triangle_index]
        weights = barycentric_weights(
            location,
            sampler["positions"][triangle[0]],
            sampler["positions"][triangle[1]],
            sampler["positions"][triangle[2]],
        )
        rgb = [sum(weights[i] * sampler["colors"][triangle[i]][channel]
                   for i in range(3)) for channel in range(3)]
        color.data[vertex.index].color = (*rgb, 1.0)
        distances.append(distance)
    target_mesh.update()
    remaining = sum(1 for item in color.data if min(item.color[:3]) >= 0.98)
    if remaining:
        raise RuntimeError(f"Color transfer left {remaining} near-white Boolean points")
    return {
        "method": "closest source triangle with barycentric POINT Color interpolation",
        "source_max_rgb": sampler["max_source_rgb"],
        "transferred_vertices": len(generated),
        "max_surface_distance": max(distances, default=0.0),
        "remaining_near_white_vertices": remaining,
    }


def mesh_manifold_report(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary = [edge for edge in bm.edges if len(edge.link_faces) == 1]
    report = {
        "boundary_edges": len(boundary),
        "wire_edges": sum(1 for edge in bm.edges if len(edge.link_faces) == 0),
        "nonmanifold_edges_gt2_faces": sum(1 for edge in bm.edges if len(edge.link_faces) > 2),
    }
    adjacency = defaultdict(set)
    for edge in boundary:
        a, b = edge.verts
        adjacency[a.index].add(b.index)
        adjacency[b.index].add(a.index)
    remaining = set(adjacency)
    components = []
    while remaining:
        pending = [remaining.pop()]
        members = set(pending)
        while pending:
            current = pending.pop()
            for neighbor in adjacency[current]:
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    members.add(neighbor)
                    pending.append(neighbor)
        coordinates = [mesh.vertices[index].co for index in members]
        degree_histogram = defaultdict(int)
        for index in members:
            degree_histogram[len(adjacency[index])] += 1
        components.append({
            "vertices": len(members),
            "edges": sum(len(adjacency[index]) for index in members) // 2,
            "degree_histogram": dict(degree_histogram),
            "bounds": tuple((min(point[axis] for point in coordinates),
                             max(point[axis] for point in coordinates)) for axis in range(3)),
        })
    bm.free()
    report["nonmanifold_edges_total"] = (
        report["boundary_edges"] + report["wire_edges"]
        + report["nonmanifold_edges_gt2_faces"]
    )
    size_histogram = defaultdict(int)
    for component in components:
        size_histogram[component["edges"]] += 1
    report["boundary_component_count"] = len(components)
    report["boundary_loop_size_histogram"] = dict(sorted(size_histogram.items()))
    report["largest_boundary_components"] = sorted(
        components, key=lambda item: item["edges"], reverse=True
    )[:8]
    return report


def repair_tiny_source_holes(obj, max_loop_edges=6):
    """Fill only the measured 3-6 edge boundary loops in this source mesh."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    boundary_edges = {edge for edge in bm.edges if len(edge.link_faces) == 1}
    remaining = set(boundary_edges)
    components = []
    while remaining:
        seed = remaining.pop()
        pending = [seed]
        component = {seed}
        while pending:
            edge = pending.pop()
            for vertex in edge.verts:
                for adjacent in vertex.link_edges:
                    if adjacent in remaining:
                        remaining.remove(adjacent)
                        component.add(adjacent)
                        pending.append(adjacent)
        components.append(component)

    repaired_cycles = []
    skipped_components = []
    for component in components:
        degrees = defaultdict(int)
        for edge in component:
            for vertex in edge.verts:
                degrees[vertex] += 1
        cycle_edge_sets = []
        if len(component) <= max_loop_edges and all(degree == 2 for degree in degrees.values()):
            cycle_edge_sets = [component]
        elif (
            len(component) == 6
            and sorted(degrees.values()).count(4) == 1
            and all(degree in (2, 4) for degree in degrees.values())
        ):
            branch = next(vertex for vertex, degree in degrees.items() if degree == 4)
            cycles = {}
            for first_edge in branch.link_edges:
                if first_edge not in component:
                    continue
                cycle = [first_edge]
                previous = first_edge
                current = first_edge.other_vert(branch)
                while current is not branch and len(cycle) <= max_loop_edges:
                    choices = [edge for edge in current.link_edges
                               if edge in component and edge is not previous]
                    if len(choices) != 1:
                        break
                    following = choices[0]
                    cycle.append(following)
                    previous, current = following, following.other_vert(current)
                if current is branch and 3 <= len(cycle) <= max_loop_edges:
                    cycles[frozenset(cycle)] = set(cycle)
            cycle_edge_sets = list(cycles.values())
            if len(cycle_edge_sets) != 2 or set.union(*cycle_edge_sets) != component:
                cycle_edge_sets = []

        if not cycle_edge_sets:
            skipped_components.append({"edges": len(component), "vertices": len(degrees),
                                       "degrees": sorted(degrees.values())})
            continue
        for cycle_edges in cycle_edge_sets:
            local_adjacency = defaultdict(list)
            local_vertices = set()
            for edge in cycle_edges:
                first, second = edge.verts
                local_adjacency[first].append(edge)
                local_adjacency[second].append(edge)
                local_vertices.update((first, second))
            if any(len(edges) != 2 for edges in local_adjacency.values()):
                bm.free()
                raise RuntimeError("Boundary-loop decomposition was not a simple cycle")
            start = min(local_vertices, key=lambda vertex: vertex.index)
            edge = min(local_adjacency[start], key=lambda current: current.index)
            ordered_vertices = [start]
            visited_edges = set()
            current = start
            while True:
                visited_edges.add(edge)
                following = edge.other_vert(current)
                if following is start:
                    break
                ordered_vertices.append(following)
                choices = [candidate for candidate in local_adjacency[following]
                           if candidate not in visited_edges]
                if len(choices) != 1:
                    bm.free()
                    raise RuntimeError("Could not order a measured source boundary loop")
                current, edge = following, choices[0]
            if len(visited_edges) != len(cycle_edges):
                bm.free()
                raise RuntimeError("Boundary-loop walk did not consume every source edge")
            face = bm.faces.new(ordered_vertices)
            repaired_cycles.append({"boundary_edges": len(cycle_edges), "faces_added": 1,
                                    "face_vertices": len(face.verts)})

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    color = obj.data.attributes.get("Color")
    if color is None or color.domain != "POINT":
        raise RuntimeError("Targeted source-hole repair lost the POINT Color attribute")
    result_topology = mesh_manifold_report(obj.data)
    return {
        "source_boundary_edges_before": len(boundary_edges),
        "boundary_components_before": len(components),
        "filled_cycles": repaired_cycles,
        "skipped_components": skipped_components,
        "topology_after_repair": result_topology,
    }


def front_surface_x(tree, y, z):
    hit = tree.ray_cast(Vector((1.2, y, z)), Vector((-1.0, 0.0, 0.0)), 2.4)
    return hit[0].x if hit[0] is not None else None


def almond_half_height(orbit, y):
    dy = y - orbit["y"]
    u = dy / orbit["half_width"]
    if abs(u) >= 1.0:
        return 0.0
    return orbit["half_height"] * (1.0 - u * u) ** 0.68


def almond_centerline(orbit, y):
    return orbit["z"] + orbit["lid_slope"] * (y - orbit["y"])


def inside_almond(orbit, y, z, scale=1.0):
    dy = y - orbit["y"]
    u = dy / max(1e-8, orbit["half_width"] * scale)
    if abs(u) >= 1.0:
        return False
    half_height = orbit["half_height"] * scale * (1.0 - u * u) ** 0.68
    return abs(z - almond_centerline(orbit, y)) < half_height


def build_manual_orbits(scene, camera, tree):
    mappings = [pixel_to_source_yz(scene, camera, pixel) for pixel in LANDMARK_PIXELS]
    # This one source-mesh pass is tied to two reviewed front-image landmarks;
    # it does not search for or rank valleys/peaks.
    eye_shapes = (
        {"side": "L", "half_width": 0.0225, "half_height": 0.0105, "lid_slope": -0.018},
        {"side": "R", "half_width": 0.0225, "half_height": 0.0105, "lid_slope": 0.018},
    )
    orbits = []
    for mapping, shape in zip(mappings, eye_shapes):
        y, z = mapping["yz"]
        orbit = {**shape, "y": y, "z": z, "source_pixel": mapping["pixel"]}
        center_x = front_surface_x(tree, y, z)
        if center_x is None:
            raise RuntimeError(f"No source surface beneath manual {shape['side']} pixel landmark")
        contour_x = []
        for index in range(64):
            angle = math.tau * index / 64.0
            u = math.cos(angle)
            dy = orbit["half_width"] * u
            y_edge = y + dy
            z_edge = almond_centerline(orbit, y_edge)
            z_edge += math.copysign(orbit["half_height"] * abs(math.sin(angle)) ** 1.36,
                                    math.sin(angle))
            value = front_surface_x(tree, y_edge, z_edge)
            if value is not None:
                contour_x.append(value)
        if len(contour_x) < 40:
            raise RuntimeError(
                f"Only {len(contour_x)}/64 rays hit the manual {shape['side']} eyelid contour"
            )
        orbit["closed_skin_center_x"] = center_x
        orbit["measured_lid_surface_x"] = median(contour_x)
        orbit["lid_surface_samples"] = len(contour_x)
        orbits.append(orbit)
    return orbits, mappings


def trim_selected_secondary_spikes(obj):
    """Blunt only high-curvature tips in explicitly bounded crown/back zones."""
    mesh = obj.data
    mesh.update()
    neighbors = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    coordinates = [vertex.co.copy() for vertex in mesh.vertices]
    normal_sums = [Vector() for _ in mesh.vertices]
    for polygon in mesh.polygons:
        weighted = polygon.normal * polygon.area
        for index in polygon.vertices:
            normal_sums[index] += weighted
    normals = [normal.normalized() if normal.length > 1e-10 else Vector()
               for normal in normal_sums]
    candidates, protected_horn = [], 0
    # Spatial boxes are limited to secondary crown, back-crown and outer-flank
    # growth. The main +Y crown horn, ears, and central face are excluded.
    for vertex in mesh.vertices:
        x, y, z = vertex.co
        main_horn = y > 0.22 and z > 0.27 and x > 0.08
        ear_mass = abs(y) > 0.23 and -0.045 < z < 0.145
        central_face = x > 0.145 and abs(y) < 0.225 and -0.04 < z < 0.265
        if main_horn:
            protected_horn += 1
        crown_zone = z > 0.22 and abs(y) < 0.275 and x < 0.22
        rear_crown_zone = x < -0.12 and z > 0.06 and abs(y) < 0.28
        outer_flank_zone = abs(y) > 0.275 and z > 0.13 and x < 0.20
        if (crown_zone or rear_crown_zone or outer_flank_zone) and not (
            main_horn or ear_mass or central_face
        ):
            candidates.append(vertex.index)
    moves = {}
    peak_values = []
    max_move = 0.0
    thresholds = {"crown_z": 0.0025, "rear_normal": 0.0025, "flank_y": 0.0025}
    trim_axes = defaultdict(int)
    for index in candidates:
        adjacent = neighbors[index]
        if len(adjacent) < 4:
            continue
        if normals[index].length < 0.5:
            continue
        mean = sum((coordinates[other] for other in adjacent), Vector()) / len(adjacent)
        outward_peak = (coordinates[index] - mean).dot(normals[index])
        peak_values.append(outward_peak)
        x, y, z = coordinates[index]
        main_horn = y > 0.22 and z > 0.27 and x > 0.08
        ear_mass = abs(y) > 0.23 and -0.045 < z < 0.145
        central_face = x > 0.145 and abs(y) < 0.225 and -0.04 < z < 0.265
        if main_horn or ear_mass or central_face:
            continue
        if z > 0.22 and abs(y) < 0.275 and x > 0.08 and x < 0.25:
            directional_peak = z - mean.z
            if directional_peak > thresholds["crown_z"]:
                amount = min(0.018, max(0.0018, directional_peak * 0.76))
                moves[index] = Vector((0.0, 0.0, -amount))
                trim_axes["crown_z"] += 1
        elif x < -0.12 and z > 0.06 and abs(y) < 0.28:
            if outward_peak > thresholds["rear_normal"]:
                amount = min(0.018, max(0.0018, outward_peak * 0.72))
                moves[index] = normals[index] * -amount
                trim_axes["rear_normal"] += 1
        elif abs(y) > 0.275 and z > 0.13 and x < 0.20:
            directional_peak = abs(y) - sum(abs(coordinates[other].y) for other in adjacent) / len(adjacent)
            if directional_peak > thresholds["flank_y"]:
                amount = min(0.018, max(0.0018, directional_peak * 0.76))
                moves[index] = Vector((0.0, -math.copysign(amount, y), 0.0))
                trim_axes["flank_y"] += 1
        if index in moves:
            max_move = max(max_move, moves[index].length)
    for index, delta in moves.items():
        mesh.vertices[index].co += delta
    mesh.update()
    if len(moves) < 8:
        peak_values.sort(reverse=True)
        raise RuntimeError(
            f"Explicit secondary-spike selection found only {len(moves)} movable tips; "
            f"candidates={len(candidates)}, valid_normals={sum(n.length >= 0.5 for n in normals)}, "
            f"top_peaks={peak_values[:12]}, per-axis={dict(trim_axes)}"
        )
    return {
        "selected_spatial_candidates": len(candidates),
        "trimmed_convex_tips": len(moves),
        "maximum_outward_peaks": sorted(peak_values, reverse=True)[:12],
        "trim_axis_counts": dict(trim_axes),
        "protected_primary_horn_vertices": protected_horn,
        "ear_and_face_protected_by_masks": True,
        "directional_thresholds": thresholds,
        "max_inward_move": max_move,
        "zones": "bounded crown/back-crown/outer-flank; main horn, ears, central face excluded",
    }


def apply_feature_cleanup(obj, orbits):
    group = obj.vertex_groups.new(name="V3CrownAndFacePreservingCleanup")
    buckets = defaultdict(list)
    for vertex in obj.data.vertices:
        x, y, z = vertex.co
        main_horn = y > 0.22 and z > 0.27 and x > 0.08
        ear_mass = abs(y) > 0.23 and -0.045 < z < 0.145
        eye_region = any(
            abs(y - orbit["y"]) < orbit["half_width"] * 2.8
            and abs(z - orbit["z"]) < orbit["half_height"] * 3.3
            for orbit in orbits
        )
        central_face = x > 0.145 and abs(y) < 0.22 and -0.06 < z < 0.24
        crown_back = (z > 0.20 and abs(y) < 0.28 and x < 0.22) or x < -0.12
        if main_horn or ear_mass or eye_region or central_face:
            weight = 0.06
        elif crown_back:
            weight = 0.46
        else:
            weight = 0.28
        buckets[weight].append(vertex.index)
    for weight, indices in buckets.items():
        group.add(indices, weight, "REPLACE")
    group_name = group.name
    smooth = obj.modifiers.new("Single masked ripple cleanup", "LAPLACIANSMOOTH")
    smooth.lambda_factor = 0.22
    smooth.lambda_border = 0.06
    smooth.iterations = 4
    smooth.use_volume_preserve = True
    smooth.use_normalized = True
    smooth.vertex_group = group_name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.update()
    color = obj.data.attributes.get("Color")
    if color is None or color.domain != "POINT":
        raise RuntimeError("Cleanup lost the source point-color attribute")
    return {"mask": group_name, "lambda": 0.22, "iterations": 4,
            "vertices": len(obj.data.vertices), "polygons": len(obj.data.polygons)}


def reduce_source_mesh(obj, target_faces=7600):
    modifier = obj.modifiers.new("Budgeted head source reduction", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target_faces / max(1, len(obj.data.polygons)))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.update()
    return {"target_body_faces": target_faces, "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons)}


def make_almond_cutter(orbit, entry_x, floor_x):
    segments = 48
    outline = []
    for index in range(segments):
        angle = math.tau * index / segments
        y = orbit["y"] + orbit["half_width"] * math.cos(angle)
        z = almond_centerline(orbit, y)
        z += math.copysign(orbit["half_height"] * abs(math.sin(angle)) ** 1.36,
                           math.sin(angle))
        outline.append((y, z))
    vertices = [(entry_x, y, z) for y, z in outline]
    vertices.extend((floor_x, y, z) for y, z in outline)
    faces = [tuple(range(segments)), tuple(reversed(range(segments, 2 * segments)))]
    for index in range(segments):
        following = (index + 1) % segments
        faces.append((index, segments + index, segments + following, following))
    mesh = bpy.data.meshes.new(f"V3_{orbit['side']}_almond_socket_cutter_geometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    cutter = bpy.data.objects.new(f"V3_{orbit['side']}_almond_socket_cutter", mesh)
    bpy.context.scene.collection.objects.link(cutter)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return cutter


def cut_almond_sockets(head, orbits, baseline_topology):
    report = {}
    pre_boolean_mesh = head.data.copy()
    color_sampler = build_color_surface_sampler(pre_boolean_mesh)
    for orbit in orbits:
        tree = build_bvh(head)
        contour_samples = []
        for index in range(96):
            angle = math.tau * index / 96.0
            y = orbit["y"] + orbit["half_width"] * math.cos(angle)
            z = almond_centerline(orbit, y)
            z += math.copysign(orbit["half_height"] * abs(math.sin(angle)) ** 1.36,
                               math.sin(angle))
            hit = front_surface_x(tree, y, z)
            if hit is not None:
                contour_samples.append(hit)
        if len(contour_samples) < 72:
            raise RuntimeError(f"Only {len(contour_samples)}/96 source rays hit {orbit['side']} almond outline")
        rim_x = median(contour_samples)
        frontmost_x = max(contour_samples)
        center_x = front_surface_x(tree, orbit["y"], orbit["z"])
        if center_x is None:
            raise RuntimeError(f"No front surface under {orbit['side']} manual eye center")
        floor_x = min(center_x, rim_x) - 0.018
        entry_x = frontmost_x + 0.008
        if entry_x - floor_x < 0.025:
            floor_x = entry_x - 0.035
        cutter = make_almond_cutter(orbit, entry_x, floor_x)
        modifier = head.modifiers.new(f"V3 shallow almond socket {orbit['side']}", "BOOLEAN")
        modifier.operation = "DIFFERENCE"
        modifier.solver = "EXACT"
        modifier.object = cutter
        bpy.context.view_layer.objects.active = head
        head.select_set(True)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bpy.data.objects.remove(cutter, do_unlink=True)
        orbit["closed_skin_center_x"] = center_x
        orbit["measured_lid_surface_x"] = rim_x
        orbit["socket_entry_x"] = entry_x
        orbit["socket_floor_x"] = floor_x
        orbit["socket_depth"] = entry_x - floor_x
        report[orbit["side"]] = {
            "outline_segments": 96,
            "measured_outline_hits": len(contour_samples),
            "rim_median_x": rim_x,
            "entry_x": entry_x,
            "floor_x": floor_x,
            "shallow_socket_depth": entry_x - floor_x,
        }
    color_transfer_report = transfer_generated_white_colors(head.data, color_sampler)
    bpy.data.meshes.remove(pre_boolean_mesh)
    head.data.update()
    after_topology = mesh_manifold_report(head.data)
    if after_topology["nonmanifold_edges_total"] > baseline_topology["nonmanifold_edges_total"]:
        raise RuntimeError(
            "Exact almond sockets increased nonmanifold edges: "
            f"before={baseline_topology}, after={after_topology}"
        )
    report["topology_before"] = baseline_topology
    report["topology_after"] = after_topology
    report["body_color_transfer"] = color_transfer_report
    return report


def make_principled(name, color, roughness, specular=0.25):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in shader.inputs:
        shader.inputs["Specular IOR Level"].default_value = specular
    return material


def add_vertex_color_material():
    material = bpy.data.materials.new("TriPoSR preserved vertex color")
    material.diffuse_color = (0.24, 0.18, 0.13, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.82
    color = nodes.new("ShaderNodeVertexColor")
    color.layer_name = "Color"
    material.node_tree.links.new(color.outputs["Color"], shader.inputs["Base Color"])
    return material


def add_eyes(orbits):
    collection = bpy.data.collections.new("V3ManualLandmarkEyes")
    bpy.context.scene.collection.children.link(collection)
    mats = {
        "sclera": make_principled("Muted ash-umber sclera", (0.064, 0.055, 0.044), 0.42, 0.26),
        "iris": make_principled("Dark restrained umber iris", (0.021, 0.012, 0.006), 0.31, 0.34),
        "pupil": make_principled("Near-black pupil", (0.004, 0.003, 0.002), 0.24, 0.38),
    }
    objects, eye_report = [], []
    for orbit in orbits:
        # Eye surface sits inside the measured outer lid surface; the broader
        # globe is occluded by the retained face around the narrow opening.
        rim_x = orbit["measured_lid_surface_x"]
        center_x = orbit["closed_skin_center_x"]
        eye_front_x = min(rim_x - 0.004, orbit["socket_floor_x"] + orbit["socket_depth"] * 0.66)
        rx = 0.014
        ry = orbit["half_width"] * 1.12
        rz = orbit["half_height"] * 1.55
        eye_center = Vector((eye_front_x - rx, orbit["y"], orbit["z"]))
        prefix = f"ShuraleEye_{orbit['side']}"

        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0,
                                             location=eye_center)
        globe = bpy.context.object
        globe.name = prefix + "_Sclera"
        globe.data.name = globe.name + "_geometry"
        globe.scale = (rx, ry, rz)
        globe.data.materials.append(mats["sclera"])
        for polygon in globe.data.polygons:
            polygon.use_smooth = True
        for current in list(globe.users_collection):
            current.objects.unlink(globe)
        collection.objects.link(globe)
        objects.append(globe)
        iris_x = eye_front_x + 0.0004
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1.0,
                                             location=(iris_x, orbit["y"], orbit["z"]))
        iris = bpy.context.object
        iris.name = prefix + "_Iris"
        iris.data.name = iris.name + "_geometry"
        iris.scale = (0.0011, 0.0064, 0.0060)
        iris.data.materials.append(mats["iris"])
        for polygon in iris.data.polygons:
            polygon.use_smooth = True
        for current in list(iris.users_collection):
            current.objects.unlink(iris)
        collection.objects.link(iris)
        objects.append(iris)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1.0,
                                             location=(iris_x + 0.0008, orbit["y"], orbit["z"]))
        pupil = bpy.context.object
        pupil.name = prefix + "_Pupil"
        pupil.data.name = pupil.name + "_geometry"
        pupil.scale = (0.00075, 0.0038, 0.0043)
        pupil.data.materials.append(mats["pupil"])
        for polygon in pupil.data.polygons:
            polygon.use_smooth = True
        for current in list(pupil.users_collection):
            current.objects.unlink(pupil)
        collection.objects.link(pupil)
        objects.append(pupil)
        orbit["eye_front_x"] = eye_front_x
        orbit["eye_rim_recess"] = rim_x - eye_front_x
        orbit["eye_center_xyz"] = tuple(eye_center)
        eye_report.append({
            "side": orbit["side"],
            "pixel": orbit["source_pixel"],
            "center_yz": (orbit["y"], orbit["z"]),
            "closed_skin_center_x": center_x,
            "median_lid_surface_x": rim_x,
            "eye_front_x": eye_front_x,
            "recess_from_lid_surface": rim_x - eye_front_x,
            "opening_half_width_height": (orbit["half_width"], orbit["half_height"]),
            "gaze": "straight along +X; iris centered, no convergence offset",
            "objects": (globe.name, iris.name, pupil.name),
        })
    return collection, objects, mats, eye_report


def configure_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = RENDER_RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.18, 0.19, 0.21, 1.0)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.20
    return scene


def point_object(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def make_review_lights(scene, scale):
    lights = []
    for name, energy, size, color in (
        ("Key softbox", 42.0, scale * 1.1, (1.0, 0.97, 0.93)),
        ("Fill softbox", 18.0, scale * 1.35, (0.86, 0.91, 1.0)),
        ("Rim softbox", 26.0, scale * 0.95, (1.0, 0.98, 0.94)),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        lights.append(obj)
    return lights


def position_review_lights(camera, lights, center, scale):
    rotation = camera.rotation_euler.to_matrix()
    right = rotation @ Vector((1.0, 0.0, 0.0))
    up = rotation @ Vector((0.0, 1.0, 0.0))
    toward_camera = (camera.location - Vector(center)).normalized()
    key, fill, rim = lights
    key.location = Vector(center) + toward_camera * (1.8 * scale) - right * (0.52 * scale) + up * (0.58 * scale)
    fill.location = Vector(center) + toward_camera * (1.45 * scale) + right * (0.72 * scale) + up * (0.08 * scale)
    rim.location = Vector(center) - toward_camera * (1.0 * scale) + up * (0.48 * scale)
    for light in lights:
        point_object(light, center)


def render_views(head, eye_collection, eye_objects, camera, scale, clay):
    scene = bpy.context.scene
    center = Vector((0.0, 0.0, 0.0))
    distance = scale * 3.25
    lights = make_review_lights(scene, scale)
    saved_body = list(head.data.materials)
    saved_eyes = {obj.name: list(obj.data.materials) for obj in eye_objects}
    head.data.materials.clear()
    head.data.materials.append(clay)
    for obj in eye_objects:
        obj.data.materials.clear()
        obj.data.materials.append(clay)
    clay_views = (
        ("refined-v3-head-clay-front.png", Vector((1.0, 0.0, 0.0))),
        ("refined-v3-head-clay-three-quarter.png", Vector((0.839, 0.515, 0.16)).normalized()),
        ("refined-v3-head-clay-profile.png", Vector((0.0, 1.0, 0.0))),
        ("refined-v3-head-clay-back.png", Vector((-1.0, 0.0, 0.0))),
    )
    for filename, direction in clay_views:
        camera.location = center + direction * distance
        point_object(camera, center)
        position_review_lights(camera, lights, center, scale)
        scene.render.filepath = str(OUTPUT_DIR / filename)
        bpy.ops.render.render(write_still=True)
    head.data.materials.clear()
    for material in saved_body:
        head.data.materials.append(material)
    for obj in eye_objects:
        obj.data.materials.clear()
        for material in saved_eyes[obj.name]:
            obj.data.materials.append(material)
    color_views = (
        ("refined-v3-head-color-front.png", Vector((1.0, 0.0, 0.0))),
        ("refined-v3-head-color-three-quarter.png", Vector((0.839, 0.515, 0.16)).normalized()),
        ("refined-v3-head-color-profile.png", Vector((0.0, 1.0, 0.0))),
    )
    for filename, direction in color_views:
        camera.location = center + direction * distance
        point_object(camera, center)
        position_review_lights(camera, lights, center, scale)
        scene.render.filepath = str(OUTPUT_DIR / filename)
        bpy.ops.render.render(write_still=True)
    camera.location = center + FRONT_DIRECTION * distance
    point_object(camera, center)
    position_review_lights(camera, lights, center, scale)
    scene.camera = camera


def render_saved_color_views(head, eye_objects, camera, scale, lights):
    scene = bpy.context.scene
    center = Vector((0.0, 0.0, 0.0))
    distance = scale * 3.25
    color_views = (
        ("refined-v3-head-color-front.png", Vector((1.0, 0.0, 0.0))),
        ("refined-v3-head-color-three-quarter.png", Vector((0.839, 0.515, 0.16)).normalized()),
        ("refined-v3-head-color-profile.png", Vector((0.0, 1.0, 0.0))),
    )
    for filename, direction in color_views:
        camera.location = center + direction * distance
        point_object(camera, center)
        position_review_lights(camera, lights, center, scale)
        scene.render.filepath = str(OUTPUT_DIR / filename)
        bpy.ops.render.render(write_still=True)
    camera.location = center + FRONT_DIRECTION * distance
    point_object(camera, center)
    position_review_lights(camera, lights, center, scale)
    scene.camera = camera


def repair_saved_candidate_colors():
    candidate_path = OUTPUT_DIR / "shurale-head-refine-v3.blend"
    if Path(bpy.data.filepath).resolve() != candidate_path.resolve():
        raise RuntimeError(f"Color-only repair must open the exact saved candidate: {bpy.data.filepath}")
    digest = sha256_file(SOURCE_OBJ)
    if digest != SOURCE_SHA256:
        raise RuntimeError(f"Raw OBJ changed: expected {SOURCE_SHA256}, got {digest}")
    head = bpy.data.objects.get("ShuraleReconstructedHeadV3")
    if head is None or head.type != "MESH":
        raise RuntimeError("Saved candidate head mesh is missing")
    eye_objects = sorted((obj for obj in bpy.data.objects if obj.name.startswith("ShuraleEye_")),
                         key=lambda obj: obj.name)
    if len(eye_objects) != 6:
        raise RuntimeError(f"Expected six saved eye parts, found {len(eye_objects)}")
    camera = bpy.context.scene.camera
    if camera is None or camera.type != "CAMERA":
        raise RuntimeError("Saved candidate review camera is missing")
    lights = [bpy.data.objects.get(name) for name in ("Key softbox", "Fill softbox", "Rim softbox")]
    if any(light is None or light.type != "LIGHT" for light in lights):
        raise RuntimeError("Saved candidate review lights are missing")

    coordinates_before = [tuple(vertex.co) for vertex in head.data.vertices]
    faces_before = [tuple(poly.vertices) for poly in head.data.polygons]
    bpy.ops.wm.obj_import(filepath=str(SOURCE_OBJ), forward_axis="NEGATIVE_Y", up_axis="Z")
    raw = next((obj for obj in bpy.context.selected_objects if obj.type == "MESH"), None)
    if raw is None:
        raise RuntimeError("Could not import the immutable original color source")
    sampler = build_color_surface_sampler(raw.data)
    color_report = transfer_generated_white_colors(head.data, sampler)
    raw_mesh = raw.data
    bpy.data.objects.remove(raw, do_unlink=True)
    if raw_mesh.users == 0:
        bpy.data.meshes.remove(raw_mesh)
    if coordinates_before != [tuple(vertex.co) for vertex in head.data.vertices]:
        raise RuntimeError("Color-only repair changed saved candidate vertex positions")
    if faces_before != [tuple(poly.vertices) for poly in head.data.polygons]:
        raise RuntimeError("Color-only repair changed saved candidate face topology")

    scene = bpy.context.scene
    scene["boolean_vertex_color_transfer"] = str({
        **color_report,
        "method": "closest immutable raw OBJ triangle with barycentric POINT Color interpolation",
        "source_obj_sha256": digest,
        "geometry_unchanged": True,
    })
    scale = camera.data.ortho_scale / 1.20
    render_saved_color_views(head, eye_objects, camera, scale, lights)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(candidate_path))
    print("SAVED_CANDIDATE_COLOR_REPAIR", scene["boolean_vertex_color_transfer"])
    print("SAVED_CANDIDATE_GEOMETRY", len(head.data.vertices), len(head.data.polygons),
          "positions_and_faces_unchanged=True")


def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    clear_scene()
    digest = sha256_file(SOURCE_OBJ)
    if digest != SOURCE_SHA256:
        raise RuntimeError(f"Raw OBJ changed: expected {SOURCE_SHA256}, got {digest}")
    scene = configure_scene()
    first_raw_vertex = read_first_obj_vertex(SOURCE_OBJ)
    bpy.ops.wm.obj_import(filepath=str(SOURCE_OBJ), forward_axis="NEGATIVE_Y", up_axis="Z")
    meshes = [obj for obj in bpy.context.selected_objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one raw closed-skin mesh, imported {len(meshes)}")
    head = meshes[0]
    head.name = "ShuraleReconstructedHeadV3"
    head.data.name = "ShuraleReconstructedHeadV3Geometry"
    head.location = (0.0, 0.0, 0.0)
    head.rotation_euler = (0.0, 0.0, 0.0)
    head.scale = (1.0, 1.0, 1.0)
    if any(abs(a - b) > 1e-6 for a, b in zip(first_raw_vertex, head.data.vertices[0].co)):
        raise RuntimeError("OBJ importer changed source coordinates or +X front axis")
    raw_topology = mesh_manifold_report(head.data)
    print("RAW_SOURCE_TOPOLOGY", raw_topology)
    raw_bounds = tuple((min(vertex.co[axis] for vertex in head.data.vertices),
                        max(vertex.co[axis] for vertex in head.data.vertices)) for axis in range(3))
    camera, scale = make_review_camera(scene, raw_bounds)
    projections = [pixel_to_source_yz(scene, camera, pixel) for pixel in LANDMARK_PIXELS]
    raw_tree = build_bvh(head)
    orbits, mappings = build_manual_orbits(scene, camera, raw_tree)
    islands = remove_disconnected_chips(head)
    spike_report = trim_selected_secondary_spikes(head)
    cleanup_report = apply_feature_cleanup(head, orbits)
    reduction_report = reduce_source_mesh(head, target_faces=6200)
    topology_after_reduction = mesh_manifold_report(head.data)
    hole_repair_report = repair_tiny_source_holes(head)
    print("TOPOLOGY_AFTER_REDUCTION", topology_after_reduction)
    print("TARGETED_SOURCE_HOLE_REPAIR", hole_repair_report)
    if hole_repair_report["topology_after_repair"]["nonmanifold_edges_total"]:
        raise RuntimeError(
            "Targeted repair did not close all measured source boundaries: "
            f"{hole_repair_report['topology_after_repair']}"
        )
    reduced_tree = build_bvh(head)
    for orbit in orbits:
        orbit["closed_skin_center_x"] = front_surface_x(reduced_tree, orbit["y"], orbit["z"])
        if orbit["closed_skin_center_x"] is None:
            raise RuntimeError(f"Reduced mesh lost the {orbit['side']} manual eye center surface")
        contour = []
        for index in range(64):
            angle = math.tau * index / 64.0
            y_edge = orbit["y"] + orbit["half_width"] * math.cos(angle)
            z_edge = almond_centerline(orbit, y_edge)
            z_edge += math.copysign(orbit["half_height"] * abs(math.sin(angle)) ** 1.36,
                                    math.sin(angle))
            value = front_surface_x(reduced_tree, y_edge, z_edge)
            if value is not None:
                contour.append(value)
        if len(contour) < 36:
            raise RuntimeError(f"Budget reduction lost the {orbit['side']} measured lid surface")
        orbit["measured_lid_surface_x"] = median(contour)
        orbit["lid_surface_samples"] = len(contour)

    body_material = add_vertex_color_material()
    head.data.materials.clear()
    head.data.materials.append(body_material)
    topology_before_sockets = mesh_manifold_report(head.data)
    print("TOPOLOGY_BEFORE_SOCKETS", topology_before_sockets)
    if topology_before_sockets["nonmanifold_edges_total"]:
        raise RuntimeError(
            "Reduced source is already nonmanifold before the eye Boolean; "
            "do not treat that baseline as a successful repair: "
            f"{topology_before_sockets}"
        )
    aperture_report = cut_almond_sockets(head, orbits, topology_before_sockets)
    eye_collection, eye_objects, eye_materials, eye_report = add_eyes(orbits)
    clay = make_principled("Neutral clay anatomical review", (0.14, 0.15, 0.16), 0.92, 0.18)

    scene["source_obj"] = str(SOURCE_OBJ.relative_to(ROOT))
    scene["source_obj_sha256"] = digest
    scene["source_front_axis"] = "+X (raw TriPoSR coordinates; unchanged)"
    scene["input_units"] = "normalized TriPoSR source coordinates; not calibrated meters"
    scene["manual_landmark_pixels_top_left"] = str(LANDMARK_PIXELS)
    scene["manual_landmark_projection_inverse"] = str(projections)
    scene["manual_eye_centers_yz"] = str(mappings)
    scene["eye_fit_report"] = str(eye_report)
    scene["aperture_report"] = str(aperture_report)
    scene["secondary_spike_trim_report"] = str(spike_report)
    scene["raw_source_topology_report"] = str(raw_topology)
    scene["targeted_source_hole_repair_report"] = str(hole_repair_report)
    scene["closed_skin_source"] = "immutable original OBJ, not refine-v2 apertures"
    total_triangles = sum(max(0, len(poly.vertices) - 2)
                          for obj in [head, *eye_objects] for poly in obj.data.polygons)
    if not 7000 <= total_triangles <= 10000:
        raise RuntimeError(f"Head plus eyes outside the 7k-10k triangle budget: {total_triangles}")
    scene["head_plus_eyes_triangles"] = total_triangles
    scene["body_reduction_report"] = str(reduction_report)
    scene["masked_cleanup_report"] = str(cleanup_report)
    scene["removed_island_report"] = str(islands)

    render_views(head, eye_collection, eye_objects, camera, scale, clay)
    bpy.context.view_layer.objects.active = head
    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_DIR / "shurale-head-refine-v3.blend"))

    print("SOURCE_SHA256", digest)
    print("SOURCE_FIRST_VERTEX", first_raw_vertex)
    print("RAW_BOUNDS", raw_bounds)
    print("FRONT_CAMERA_ORTHO", camera.data.ortho_scale)
    print("LANDMARK_PROJECTIONS", projections)
    print("MANUAL_ORBIT_CENTERS", mappings)
    print("MANUAL_EYE_REPORT", eye_report)
    print("ISLAND_CLEANUP", islands)
    print("SECONDARY_SPIKE_TRIM", spike_report)
    print("FEATURE_CLEANUP", cleanup_report)
    print("BUDGET_REDUCTION", reduction_report)
    print("ALMOND_APERTURES", aperture_report)
    print("TOTAL_TRIANGLES", total_triangles)


if __name__ == "__main__":
    if "--repair-saved-color" in sys.argv:
        repair_saved_candidate_colors()
    else:
        main()
