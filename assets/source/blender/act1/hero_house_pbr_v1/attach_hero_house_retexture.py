"""Attach a returned Meshy PBR texture set to the staged semantic house geometry.

Geometry from opaquehero-for-retexture.glb remains authoritative. The returned
GLB supplies only a material. Its geometry is intentionally ignored. UV-space
triangle coverage must match the staged source except for two documented
thin end-cap quads, which retain their original source materials.

Pass --normal-correction-v2 after Blender's `--` separator to create a separate
GLB-conversion candidate: it repairs only verified inward main-wall skins and
the Godot ArrayMesh clockwise winding on the log crowns. It does not change
the C# runtime geometry source or overwrite the v1 attachment.
"""

from __future__ import annotations

import collections
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE_GLB = ROOT / "opaquehero-for-retexture.glb"
RETEXTURE_GLB = ROOT / "meshy-retxt-4k-keepuv.glb"
BLEND_OUT = ROOT / "hero-house-semantic-pbr-v1.blend"
GLB_OUT = ROOT / "hero-house-semantic-pbr-v1.glb"
MANIFEST_OUT = ROOT / "manifest.json"
FRONT_RENDER = ROOT / "hero-house-semantic-pbr-v1-front-three-quarter.png"
PORCH_RENDER = ROOT / "hero-house-semantic-pbr-v1-porch-detail.png"
SIDE_RENDER = ROOT / "hero-house-semantic-pbr-v1-right-side.png"
MATERIAL_NAME = "URMAN_Hero_PBR_v1"
SOURCE_ROOT_NAME = "HeroHouse_TimberPlaster"
NORMAL_CORRECTION_V2 = "--normal-correction-v2" in sys.argv
UV_V3_MODE = "--uv-v3" in sys.argv
MANIFEST_REVIEW_KEY = "retexture_attachment_review"
if NORMAL_CORRECTION_V2:
    BLEND_OUT = ROOT / "hero-house-semantic-pbr-v2.blend"
    GLB_OUT = ROOT / "hero-house-semantic-pbr-v2.glb"
    FRONT_RENDER = ROOT / "hero-house-semantic-pbr-v2-front-three-quarter.png"
    PORCH_RENDER = None
    SIDE_RENDER = None
    MATERIAL_NAME = "URMAN_Hero_PBR_v2"
    MANIFEST_REVIEW_KEY = "retexture_attachment_review_v2"
if UV_V3_MODE:
    SOURCE_GLB = ROOT / "opaquehero-for-retexture-v3.glb"
    RETEXTURE_GLB = ROOT / "meshy-retxt-4k-keepuv-v3.glb"
    BLEND_OUT = ROOT / "hero-house-semantic-pbr-v3.blend"
    GLB_OUT = ROOT / "hero-house-semantic-pbr-v3.glb"
    FRONT_RENDER = ROOT / "hero-house-semantic-pbr-v3-front-three-quarter.png"
    PORCH_RENDER = None
    SIDE_RENDER = None
    MATERIAL_NAME = "URMAN_Hero_PBR_v3"
    REVIEW_OUT = ROOT / "hero-house-semantic-pbr-v3-attachment.metadata.json"

MAIN_SHELL_WALLS = {
    "HeroHouse_Street_Wall_LOD0": {
        "outward": (0.0, -1.0, 0.0), "skin_triangles": 60, "plane": -0.866,
    },
    "HeroHouse_Rear_Wall_LOD0": {
        "outward": (0.0, 1.0, 0.0), "skin_triangles": 26, "plane": 6.134,
    },
    "HeroHouse_Left_Wall_LOD0": {
        "outward": (-1.0, 0.0, 0.0), "skin_triangles": 16, "plane": -2.342,
    },
    "HeroHouse_Right_Wall_LOD0": {
        "outward": (1.0, 0.0, 0.0), "skin_triangles": 16, "plane": 5.658,
    },
}
CROWN_EXPECTED_COMPONENTS = {
    "HeroHouse_Street_LogCrown": (42, 0),
    "HeroHouse_Rear_LogCrown": (25, 0),
    "HeroHouse_Left_LogCrown": (16, 1),
    "HeroHouse_Right_LogCrown": (16, 1),
}
EXPECTED_SOURCE_CAPS = {
    "HeroHouse_PorchRoof1_LOD0": {
        ((0.7132813, 0.5834997), (0.7132813, 0.5842966), (0.7276255, 0.5834997)),
        ((0.7132813, 0.5842966), (0.7276255, 0.5834997), (0.7276255, 0.5842966)),
    },
    "HeroHouse_Prichelina1_LOD0": {
        ((0.4788584, 0.9554693), (0.4790887, 0.9554693), (0.4790887, 0.957197)),
        ((0.4788584, 0.9554693), (0.4788584, 0.957197), (0.4790887, 0.957197)),
    },
}
EXPECTED_SOURCE_CAP_POSITIONS_V3 = {
    "HeroHouse_PorchRoof1_LOD0": {
        ((-1.164399, -2.166, 2.8), (-1.164399, -1.086, 2.8), (-1.164399, -1.086, 2.86)),
        ((-1.164399, -2.166, 2.8), (-1.164399, -2.166, 2.86), (-1.164399, -1.086, 2.86)),
    },
    "HeroHouse_Prichelina1_LOD0": {
        ((1.658001, -1.651, 4.9), (1.658001, -1.611, 4.6), (1.658001, -1.611, 4.9)),
        ((1.658001, -1.651, 4.6), (1.658001, -1.651, 4.9), (1.658001, -1.611, 4.6)),
    },
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def glb_document(path: Path):
    data = path.read_bytes()
    offset = 12
    document = None
    while offset < len(data):
        size, kind = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset:offset + size]
        offset += size
        if kind == 0x4E4F534A:
            document = json.loads(chunk.decode("utf-8").rstrip("\0 "))
            break
    if document is None:
        raise RuntimeError(f"GLB has no JSON chunk: {path}")
    return document


def vector_key(vector, digits=6):
    return tuple(round(float(vector[i]), digits) for i in range(3))


def uv_key(vector, digits=7):
    return tuple(round(float(vector[i]), digits) for i in range(2))


def object_bounds(objects):
    points = [obj.matrix_world @ vertex.co for obj in objects if obj.type == "MESH"
              for vertex in obj.data.vertices]
    if not points:
        return None
    low = [min(float(point[i]) for point in points) for i in range(3)]
    high = [max(float(point[i]) for point in points) for i in range(3)]
    return [[round(v, 7) for v in low], [round(v, 7) for v in high]]


def triangle_signatures(objects):
    geometry = collections.Counter()
    uv_space = collections.Counter()
    paired = collections.Counter()
    triangle_count = 0
    uv_coordinate_count = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        if not mesh.uv_layers:
            raise RuntimeError(f"Imported mesh has no UV layer: {obj.name}")
        uv_layer = mesh.uv_layers.active or mesh.uv_layers[0]
        if len(uv_layer.data) != len(mesh.loops):
            raise RuntimeError(f"Imported UV loop count differs: {obj.name}")
        mesh.calc_loop_triangles()
        uv_coordinate_count += len(uv_layer.data)
        for triangle in mesh.loop_triangles:
            positions = []
            uvs = []
            paired_points = []
            for loop_index in triangle.loops:
                vertex_index = mesh.loops[loop_index].vertex_index
                world = obj.matrix_world @ mesh.vertices[vertex_index].co
                position = vector_key(world)
                uv = uv_key(uv_layer.data[loop_index].uv)
                positions.append(position)
                uvs.append(uv)
                paired_points.append((position, uv))
            geometry[tuple(sorted(positions))] += 1
            uv_space[tuple(sorted(uvs))] += 1
            paired[tuple(sorted(paired_points))] += 1
            triangle_count += 1
    return {
        "triangle_count": triangle_count,
        "uv_coordinate_count": uv_coordinate_count,
        "geometry": geometry,
        "uv_space": uv_space,
        "paired": paired,
    }


def counter_delta(reference, candidate):
    missing = sum((reference - candidate).values())
    extra = sum((candidate - reference).values())
    return {"missing": missing, "extra": extra, "exact": missing == 0 and extra == 0}


def world_normal(obj, local_normal):
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    result = normal_matrix @ local_normal
    if result.length_squared == 0:
        raise RuntimeError(f"Zero surface normal on {obj.name}")
    return result.normalized()


def paired_triangle_key(obj, polygon):
    mesh = obj.data
    layer = mesh.uv_layers.active or mesh.uv_layers[0]
    return tuple(sorted(
        (
            vector_key(obj.matrix_world @ mesh.vertices[mesh.loops[index].vertex_index].co),
            uv_key(layer.data[index].uv),
        )
        for index in polygon.loop_indices
    ))


def wall_reveal_signatures(wall_objects, selected_keys):
    """Capture untouched reveal winding and custom loops by paired position/UV."""
    signatures = collections.Counter()
    pending_skin = collections.Counter(selected_keys)
    for obj in wall_objects:
        for polygon in obj.data.polygons:
            key = paired_triangle_key(obj, polygon)
            if pending_skin[key] > 0:
                pending_skin[key] -= 1
                continue
            paired_loops = []
            for index in polygon.loop_indices:
                loop = obj.data.loops[index]
                position = vector_key(obj.matrix_world @ obj.data.vertices[loop.vertex_index].co)
                uv = uv_key((obj.data.uv_layers.active or obj.data.uv_layers[0]).data[index].uv)
                normal = world_normal(obj, loop.normal)
                paired_loops.append((position, uv, vector_key(normal)))
            signatures[(key, tuple(sorted(paired_loops)), vector_key(world_normal(obj, polygon.normal)))] += 1
    if any(pending_skin.values()):
        raise RuntimeError(f"Selected wall-skin faces were not found after winding repair: {dict(pending_skin)}")
    return signatures


def reverse_polygon_indices(obj, polygon_indices):
    """Reverse only named source polygon indices, leaving loop data on its vertices."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    source_index_layer = bm.faces.layers.int.new("urman_source_polygon_index")
    for face in bm.faces:
        face[source_index_layer] = face.index
    selected = [face for face in bm.faces if face[source_index_layer] in polygon_indices]
    if len(selected) != len(polygon_indices):
        bm.free()
        raise RuntimeError(
            f"BMesh face map mismatch on {obj.name}: selected={len(selected)} expected={len(polygon_indices)}"
        )
    bmesh.ops.reverse_faces(bm, faces=selected)
    bm.faces.layers.int.remove(source_index_layer)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update(calc_edges=True)


def rounded_crown_components(obj, expected_axis):
    """Derive each log's cross-section center from its own welded course geometry."""
    mesh = obj.data
    coordinates = [
        tuple(round(float(value), 5) for value in (obj.matrix_world @ vertex.co))
        for vertex in mesh.vertices
    ]
    adjacency = collections.defaultdict(set)
    for polygon in mesh.polygons:
        points = [coordinates[index] for index in polygon.vertices]
        for point in points:
            adjacency[point]
        for index, point in enumerate(points):
            adjacency[point].add(points[(index + 1) % len(points)])
            adjacency[point].add(points[(index - 1) % len(points)])

    components = []
    unseen = set(adjacency)
    while unseen:
        start = unseen.pop()
        stack = [start]
        points = {start}
        while stack:
            point = stack.pop()
            for neighbor in adjacency[point]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    points.add(neighbor)
                    stack.append(neighbor)
        rows = list(points)
        spans = [max(point[axis] for point in rows) - min(point[axis] for point in rows) for axis in range(3)]
        axis = max(range(3), key=lambda candidate: spans[candidate])
        if axis != expected_axis:
            raise RuntimeError(f"Unexpected course axis on {obj.name}: {axis} != {expected_axis}")
        cross_axes = [candidate for candidate in range(3) if candidate != axis]
        cross_center = [
            (max(point[index] for point in rows) + min(point[index] for point in rows)) * 0.5
            for index in cross_axes
        ]
        cross_spans = [spans[index] for index in cross_axes]
        if min(spans) < 0.23 or max(cross_spans) > 0.26 or abs(cross_spans[0] - cross_spans[1]) > 0.015:
            raise RuntimeError(f"Unexpected non-round crown component on {obj.name}: spans={spans}")
        components.append({
            "points": points,
            "axis": axis,
            "cross_axes": cross_axes,
            "cross_center": cross_center,
            "axis_min": min(point[axis] for point in rows),
            "axis_max": max(point[axis] for point in rows),
            "spans": spans,
        })

    component_for_point = {
        point: component
        for component in components
        for point in component["points"]
    }
    return coordinates, components, component_for_point


def crown_orientation_report(obj, expected_axis):
    mesh = obj.data
    coordinates, components, component_for_point = rounded_crown_components(obj, expected_axis)
    side_custom_dots = []
    side_geometry_dots = []
    cap_custom_dots = []
    cap_geometry_dots = []
    side_faces = 0
    cap_faces = 0
    for polygon in mesh.polygons:
        vertex_indices = list(polygon.vertices)
        if len(vertex_indices) != 3:
            raise RuntimeError(f"Non-triangle crown face on {obj.name}: polygon={polygon.index}")
        points = [coordinates[index] for index in vertex_indices]
        component = component_for_point[points[0]]
        if any(component_for_point[point] is not component for point in points):
            raise RuntimeError(f"Crown face spans disconnected courses on {obj.name}: polygon={polygon.index}")
        axis = component["axis"]
        low = component["axis_min"]
        high = component["axis_max"]
        is_low_cap = all(abs(point[axis] - low) < 0.0001 for point in points)
        is_high_cap = all(abs(point[axis] - high) < 0.0001 for point in points)
        face_normal = world_normal(obj, polygon.normal)
        if is_low_cap or is_high_cap:
            side = -1.0 if is_low_cap else 1.0
            target = Vector([(1.0 if index == axis else 0.0) * side for index in range(3)])
            cap_geometry_dots.append(face_normal.dot(target))
            loop_normals = [world_normal(obj, mesh.loops[index].normal) for index in polygon.loop_indices]
            cap_custom_dots.extend(normal.dot(target) for normal in loop_normals)
            cap_faces += 1
            continue

        cross_axes = component["cross_axes"]
        center = component["cross_center"]
        centroid = [sum(point[index] for point in points) / len(points) for index in range(3)]
        radial = Vector([centroid[index] - center[position] for position, index in enumerate(cross_axes)])
        if radial.length_squared == 0:
            raise RuntimeError(f"Crown side face lies on its course centerline: {obj.name}/{polygon.index}")
        radial.normalize()
        projected_face_normal = Vector([face_normal[index] for index in cross_axes])
        projected_face_normal.normalize()
        side_geometry_dots.append(projected_face_normal.dot(radial))
        for index in polygon.loop_indices:
            normal = world_normal(obj, mesh.loops[index].normal)
            projected_normal = Vector([normal[axis] for axis in cross_axes])
            projected_normal.normalize()
            vertex = coordinates[mesh.loops[index].vertex_index]
            vertex_radial = Vector([vertex[axis] - center[position] for position, axis in enumerate(cross_axes)])
            vertex_radial.normalize()
            side_custom_dots.append(projected_normal.dot(vertex_radial))
        side_faces += 1

    if not components or cap_faces + side_faces != len(mesh.polygons):
        raise RuntimeError(f"Incomplete crown orientation accounting on {obj.name}")
    return {
        "course_components": len(components),
        "course_axis": "XYZ"[expected_axis],
        "triangle_faces": len(mesh.polygons),
        "side_faces": side_faces,
        "end_cap_faces": cap_faces,
        "side_custom_normal_radial_dot_min": round(min(side_custom_dots), 6),
        "side_custom_normal_radial_dot_median": round(sorted(side_custom_dots)[len(side_custom_dots) // 2], 6),
        "side_face_normal_radial_dot_min": round(min(side_geometry_dots), 6),
        "side_face_normal_radial_dot_median": round(sorted(side_geometry_dots)[len(side_geometry_dots) // 2], 6),
        "cap_custom_normal_axial_dot_min": round(min(cap_custom_dots), 6),
        "cap_face_normal_axial_dot_min": round(min(cap_geometry_dots), 6),
    }


def correct_source_normals_for_glb(source_meshes):
    """Convert only proven inward wall skins and Godot-wound log crowns in this GLB copy."""
    by_name = {obj.name: obj for obj in source_meshes}
    missing_walls = set(MAIN_SHELL_WALLS) - set(by_name)
    if missing_walls:
        raise RuntimeError(f"Missing main shell walls for outward-frame derivation: {sorted(missing_walls)}")
    wall_objects = [by_name[name] for name in MAIN_SHELL_WALLS]
    body_points = [
        obj.matrix_world @ vertex.co
        for obj in wall_objects
        for vertex in obj.data.vertices
    ]
    body_low = Vector([min(point[index] for point in body_points) for index in range(3)])
    body_high = Vector([max(point[index] for point in body_points) for index in range(3)])
    body_center = (body_low + body_high) * 0.5
    wall_plans = {}
    selected_wall_keys = []
    for name, contract in MAIN_SHELL_WALLS.items():
        obj = by_name[name]
        outward = Vector(contract["outward"])
        axis = max(range(3), key=lambda index: abs(outward[index]))
        selected_indices = []
        selected_plane = []
        selected_keys = []
        for polygon in obj.data.polygons:
            normal = world_normal(obj, polygon.normal)
            centroid = obj.matrix_world @ polygon.center
            side_distance = (centroid - body_center).dot(outward)
            if normal.dot(outward) < -0.999:
                if side_distance <= 0.25:
                    raise RuntimeError(
                        f"Inward wall-facing triangle is not on the exterior side: {name}/{polygon.index} "
                        f"side={side_distance:.4f}"
                    )
                selected_indices.append(polygon.index)
                selected_plane.append(float(centroid[axis]))
                selected_keys.append(paired_triangle_key(obj, polygon))
            elif normal.dot(outward) > 0.999 and side_distance > 0.25:
                raise RuntimeError(f"Unexpected duplicate already-outward wall skin on {name}/{polygon.index}")
        plane_mean = sum(selected_plane) / len(selected_plane) if selected_plane else None
        if len(selected_indices) != contract["skin_triangles"]:
            raise RuntimeError(
                f"Unexpected broad wall-skin coverage on {name}: {len(selected_indices)} != {contract['skin_triangles']}"
            )
        if plane_mean is None or abs(plane_mean - contract["plane"]) > 0.002:
            raise RuntimeError(f"Unexpected main-shell skin plane on {name}: {plane_mean} != {contract['plane']}")
        wall_plans[name] = {
            "object": obj,
            "outward": outward,
            "selected_indices": selected_indices,
            "selected_keys": selected_keys,
            "plane": round(plane_mean, 6),
            "reveal_faces": len(obj.data.polygons) - len(selected_indices),
        }
        selected_wall_keys.extend(selected_keys)

    wall_reveals_before = wall_reveal_signatures(wall_objects, selected_wall_keys)
    before_signatures = triangle_signatures(source_meshes)
    before_vertex_positions = {
        obj.name: sorted(vector_key(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)
        for obj in source_meshes
    }
    before_transforms = {
        obj.name: [[round(float(value), 9) for value in row] for row in obj.matrix_local]
        for obj in source_meshes
    }

    wall_report = {}
    for name, plan in wall_plans.items():
        obj = plan["object"]
        reverse_polygon_indices(obj, set(plan["selected_indices"]))
        corrected = [
            world_normal(obj, obj.data.polygons[index].normal).dot(plan["outward"])
            for index in plan["selected_indices"]
        ]
        if min(corrected) < 0.999:
            raise RuntimeError(f"Wall skin winding did not turn outward on {name}: min dot={min(corrected)}")
        wall_report[name] = {
            "outward_world_normal": [round(float(value), 3) for value in plan["outward"]],
            "skin_plane_source_world": plan["plane"],
            "skin_triangles_reversed": len(plan["selected_indices"]),
            "untouched_window_door_reveal_faces": plan["reveal_faces"],
            "corrected_skin_normal_dot_min": round(min(corrected), 6),
        }

    crown_report = {}
    for name, (expected_components, expected_axis) in CROWN_EXPECTED_COMPONENTS.items():
        obj = by_name.get(name)
        if obj is None:
            raise RuntimeError(f"Missing source log crown for GLB winding correction: {name}")
        mesh = obj.data
        before_report = crown_orientation_report(obj, expected_axis)
        if before_report["course_components"] != expected_components:
            raise RuntimeError(
                f"Unexpected separate log-course count on {name}: "
                f"{before_report['course_components']} != {expected_components}"
            )
        if before_report["side_custom_normal_radial_dot_min"] < 0.9:
            raise RuntimeError(f"Source custom side normals are not outward from log centers on {name}")
        if before_report["side_face_normal_radial_dot_median"] > -0.9:
            raise RuntimeError(f"Source side-face winding is not consistently inward on {name}")
        if before_report["cap_custom_normal_axial_dot_min"] < 0.99 or before_report["cap_face_normal_axial_dot_min"] > -0.99:
            raise RuntimeError(f"Source log end-cap normals do not establish an outward flip on {name}")

        uv_layer = mesh.uv_layers.active or mesh.uv_layers[0]
        normal_maps_by_face = collections.defaultdict(list)
        for polygon in mesh.polygons:
            corners = {}
            for loop_index in polygon.loop_indices:
                loop = mesh.loops[loop_index]
                key = (
                    vector_key(obj.matrix_world @ mesh.vertices[loop.vertex_index].co),
                    uv_key(uv_layer.data[loop_index].uv),
                )
                if key in corners:
                    raise RuntimeError(f"Crown face has a duplicate position/UV corner on {name}/{polygon.index}")
                corners[key] = loop.normal.copy()
            normal_maps_by_face[paired_triangle_key(obj, polygon)].append(corners)
        vertex_positions = [tuple(vertex.co) for vertex in mesh.vertices]
        face_count = len(mesh.polygons)
        reverse_polygon_indices(obj, set(range(face_count)))
        if len(mesh.vertices) != len(vertex_positions) or len(mesh.polygons) != face_count:
            raise RuntimeError(f"Crown topology changed during GLB winding conversion on {name}")
        if any(tuple(mesh.vertices[index].co) != vertex_positions[index] for index in range(len(vertex_positions))):
            raise RuntimeError(f"Crown vertex positions changed during GLB winding conversion on {name}")
        uv_layer = mesh.uv_layers.active or mesh.uv_layers[0]
        restored_loop_normals = []
        for polygon in mesh.polygons:
            face_key = paired_triangle_key(obj, polygon)
            candidates = normal_maps_by_face.get(face_key)
            if not candidates:
                raise RuntimeError(f"Crown face lost its corner-normal source key on {name}/{polygon.index}")
            corner_keys = []
            current_normals = []
            for loop_index in polygon.loop_indices:
                loop = mesh.loops[loop_index]
                corner_keys.append((
                    vector_key(obj.matrix_world @ mesh.vertices[loop.vertex_index].co),
                    uv_key(uv_layer.data[loop_index].uv),
                ))
                current_normals.append(loop.normal.copy())
            candidate_index = max(
                range(len(candidates)),
                key=lambda index: sum(
                    current.dot(candidates[index][corner_key])
                    for current, corner_key in zip(current_normals, corner_keys)
                ),
            )
            corner_normals = candidates.pop(candidate_index)
            for corner_key in corner_keys:
                normal = corner_normals.get(corner_key)
                if normal is None:
                    raise RuntimeError(f"Crown loop lost its corner-normal source key on {name}/{polygon.index}")
                restored_loop_normals.append(normal)
        if any(candidates for candidates in normal_maps_by_face.values()):
            raise RuntimeError(f"Crown winding conversion left unmatched loop-normal records on {name}")
        mesh.normals_split_custom_set(restored_loop_normals)
        mesh.update()

        after_report = crown_orientation_report(obj, expected_axis)
        if after_report["side_face_normal_radial_dot_min"] < 0.9:
            raise RuntimeError(f"Corrected log side winding is not outward from its course centers on {name}")
        if after_report["cap_face_normal_axial_dot_min"] < 0.99:
            raise RuntimeError(f"Corrected log end-cap winding is not outward on {name}")
        if after_report["side_custom_normal_radial_dot_min"] < 0.9 or after_report["cap_custom_normal_axial_dot_min"] < 0.99:
            raise RuntimeError(f"Outward log custom normals were not preserved on {name}")
        crown_report[name] = {
            "source_custom_normals": {
                "side_radial_dot_min": before_report["side_custom_normal_radial_dot_min"],
                "side_radial_dot_median": before_report["side_custom_normal_radial_dot_median"],
                "cap_axial_dot_min": before_report["cap_custom_normal_axial_dot_min"],
            },
            "source_face_winding": {
                "side_radial_dot_median": before_report["side_face_normal_radial_dot_median"],
                "cap_axial_dot_min": before_report["cap_face_normal_axial_dot_min"],
            },
            "faces_reversed": face_count,
            "corrected_face_winding": {
                "side_radial_dot_min": after_report["side_face_normal_radial_dot_min"],
                "cap_axial_dot_min": after_report["cap_face_normal_axial_dot_min"],
            },
            "preserved_custom_normals": {
                "side_radial_dot_min": after_report["side_custom_normal_radial_dot_min"],
                "cap_axial_dot_min": after_report["cap_custom_normal_axial_dot_min"],
            },
        }

    after_signatures = triangle_signatures(source_meshes)
    for key in ("geometry", "uv_space", "paired"):
        delta = counter_delta(before_signatures[key], after_signatures[key])
        if not delta["exact"]:
            raise RuntimeError(f"GLB winding conversion changed source {key} position/UV coverage: {delta}")
    after_vertex_positions = {
        obj.name: sorted(vector_key(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)
        for obj in source_meshes
    }
    after_transforms = {
        obj.name: [[round(float(value), 9) for value in row] for row in obj.matrix_local]
        for obj in source_meshes
    }
    if before_vertex_positions != after_vertex_positions or before_transforms != after_transforms:
        raise RuntimeError("GLB winding conversion changed source vertex positions or semantic-node transforms")
    wall_reveals_after = wall_reveal_signatures(wall_objects, selected_wall_keys)
    if wall_reveals_before != wall_reveals_after:
        raise RuntimeError("Window/door reveal winding or loop data changed during wall-skin correction")

    return {
        "method": "guarded GLB conversion repair in staged Blender copy; no Godot/C# runtime geometry changed",
        "source_glb_geometry_sha256": sha256_file(SOURCE_GLB),
        "main_shell_body_center_source_world": [round(float(value), 6) for value in body_center],
        "wall_skins": wall_report,
        "crown_winding_conversion": crown_report,
        "preserved": {
            "position_uv_triangle_signatures_exact": True,
            "per_node_world_vertex_positions_exact": True,
            "semantic_node_transforms_exact": True,
            "window_door_reveal_winding_and_loop_data_exact": True,
        },
    }


def missing_uv_triangle_sources(source_objects, returned_uv_counter):
    remaining = returned_uv_counter.copy()
    missing = []
    for obj in source_objects:
        mesh = obj.data
        layer = mesh.uv_layers.active or mesh.uv_layers[0]
        mesh.calc_loop_triangles()
        for triangle in mesh.loop_triangles:
            uv_triangle = tuple(sorted(uv_key(layer.data[index].uv) for index in triangle.loops))
            if remaining[uv_triangle] > 0:
                remaining[uv_triangle] -= 1
                continue
            positions = []
            for loop_index in triangle.loops:
                vertex_index = mesh.loops[loop_index].vertex_index
                positions.append(vector_key(obj.matrix_world @ mesh.vertices[vertex_index].co))
            missing.append({
                "semantic_node": obj.name,
                "uv_triangle": [list(point) for point in uv_triangle],
                "world_position_triangle": [list(point) for point in sorted(positions)],
            })
    return missing


def cap_uv_key(obj, triangle):
    layer = obj.data.uv_layers.active or obj.data.uv_layers[0]
    return tuple(sorted(uv_key(layer.data[index].uv) for index in triangle.loops))


def cap_position_key(obj, triangle):
    return tuple(sorted(
        vector_key(obj.matrix_world @ obj.data.vertices[obj.data.loops[index].vertex_index].co)
        for index in triangle.loops
    ))


def approved_cap_key(obj, triangle):
    return cap_position_key(obj, triangle) if UV_V3_MODE else cap_uv_key(obj, triangle)


def collect_cap_face_materials(source_meshes, allowed_missing):
    """Find the exact approved cap triangles and retain their source slots."""
    approved_caps = EXPECTED_SOURCE_CAP_POSITIONS_V3 if UV_V3_MODE else EXPECTED_SOURCE_CAPS
    allowed = {
        (name, triangle)
        for name, triangles in approved_caps.items()
        for triangle in triangles
    }
    observed = collections.Counter()
    face_triangles = collections.defaultdict(list)

    for obj in source_meshes:
        if obj.name not in approved_caps:
            continue
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            key = (obj.name, approved_cap_key(obj, triangle))
            if key in allowed:
                observed[key] += 1
                face_triangles[(obj.name, triangle.polygon_index)].append(key[1])

    expected_counts = collections.Counter({key: 1 for key in allowed})
    if observed != expected_counts:
        raise RuntimeError(
            "Approved cap triangles do not map one-to-one to the expected source faces: "
            f"observed={dict(observed)}, expected={dict(expected_counts)}"
        )

    missing_keys = collections.Counter()
    for row in allowed_missing:
        points_field = "world_position_triangle" if UV_V3_MODE else "uv_triangle"
        missing_keys[(row["semantic_node"], tuple(tuple(point) for point in row[points_field]))] += 1
    if missing_keys != expected_counts:
        raise RuntimeError(
            "Returned atlas mismatch is not limited to the approved cap triangles: "
            f"missing={dict(missing_keys)}, expected={dict(expected_counts)}"
        )

    preserved_by_object = {}
    exception_rows = []
    for obj in source_meshes:
        polygon_indices = {
            polygon_index
            for name, polygon_index in face_triangles
            if name == obj.name
        }
        if not polygon_indices:
            continue
        mesh = obj.data
        original_materials = list(mesh.materials)
        preserved_faces = {}
        obj_triangles = {
            triangle.polygon_index: [] for triangle in mesh.loop_triangles
        }
        for triangle in mesh.loop_triangles:
            obj_triangles[triangle.polygon_index].append(approved_cap_key(obj, triangle))

        for polygon_index in sorted(polygon_indices):
            polygon = mesh.polygons[polygon_index]
            triangles = obj_triangles[polygon_index]
            expected_for_face = [
                triangle for triangle in triangles
                if (obj.name, triangle) in allowed
            ]
            if len(expected_for_face) != len(triangles) or len(expected_for_face) != 1:
                raise RuntimeError(
                    f"Approved cap polygon does not map exactly one source triangle: {obj.name} "
                    f"polygon={polygon_index}, triangles={triangles}"
                )
            material_index = int(polygon.material_index)
            if material_index < 0 or material_index >= len(original_materials):
                raise RuntimeError(f"Approved cap has invalid source material slot: {obj.name}")
            material = original_materials[material_index]
            if material is None:
                raise RuntimeError(f"Approved cap has no source material: {obj.name}")
            if polygon.loop_total != 3:
                raise RuntimeError(
                    f"Approved cap face is not the expected imported triangle: {obj.name} polygon={polygon_index} "
                    f"loops={polygon.loop_total}"
                )
            preserved_faces[polygon_index] = material
            for triangle in mesh.loop_triangles:
                if triangle.polygon_index != polygon_index:
                    continue
                uv_triangle = cap_uv_key(obj, triangle)
                world_points = [
                    obj.matrix_world @ mesh.vertices[mesh.loops[index].vertex_index].co
                    for index in triangle.loops
                ]
                area = 0.5 * (world_points[1] - world_points[0]).cross(
                    world_points[2] - world_points[0]
                ).length
                exception_rows.append({
                    "semantic_node": obj.name,
                    "polygon_index": polygon_index,
                    "source_material_slot": material_index,
                    "source_material_name": material.name,
                    "uv_triangle": [list(point) for point in uv_triangle],
                    "uv_area_pixels_at_4096": round(abs(sum(
                        uv_triangle[i][0] * uv_triangle[(i + 1) % 3][1]
                        - uv_triangle[(i + 1) % 3][0] * uv_triangle[i][1]
                        for i in range(3)
                    )) * 0.5 * 4096 * 4096, 3),
                    "world_position_triangle": [list(vector_key(point)) for point in sorted(world_points, key=lambda p: tuple(p))],
                    "world_triangle_area_m2": round(float(area), 9),
                    "reason": (
                        "The returned v3 atlas omits this exact thin semantic end-cap triangle; preserve its source material slot."
                        if UV_V3_MODE else
                        "Returned base-color UV atlas omitted this subpixel thin end-cap triangle; preserve the original source material on the source face."
                    ),
                })
        preserved_by_object[obj.name] = {
            "faces": preserved_faces,
            "original_materials": original_materials,
        }

    if len(exception_rows) != 4:
        raise RuntimeError(f"Expected four approved cap triangles, got {len(exception_rows)}")
    return preserved_by_object, exception_rows


def images_upstream(material, target_socket):
    found = set()
    visited = set()

    def walk(socket):
        socket_id = socket.as_pointer()
        if socket_id in visited:
            return
        visited.add(socket_id)
        for link in socket.links:
            node = link.from_node
            if node.type == "TEX_IMAGE" and node.image:
                found.add(node.image)
            else:
                for input_socket in node.inputs:
                    if input_socket.is_linked:
                        walk(input_socket)

    walk(target_socket)
    return sorted(found, key=lambda image: image.name)


def material_report(material):
    if not material.use_nodes or not material.node_tree:
        return None
    principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if principled is None:
        return None
    binding = {}
    for socket_name in ("Base Color", "Metallic", "Roughness", "Normal"):
        socket = principled.inputs.get(socket_name)
        if socket:
            binding[socket_name] = [
                {
                    "name": image.name,
                    "size": [int(image.size[0]), int(image.size[1])],
                    "colorspace": image.colorspace_settings.name,
                    "channels": int(image.channels),
                }
                for image in images_upstream(material, socket)
            ]
    images = {image["name"]: image for socket_images in binding.values() for image in socket_images}
    return {
        "name": material.name,
        "principled_bsdf": True,
        "image_bindings": binding,
        "images": sorted(images.values(), key=lambda image: image["name"]),
    }


def create_studio(scene, focus, house_bounds):
    low, high = [Vector(row) for row in house_bounds]
    span = max(high - low)
    ground_z = low.z - 0.06
    ground = bpy.data.objects.new("PBRReview_Ground", bpy.data.meshes.new("PBRReview_GroundMesh"))
    scene.collection.objects.link(ground)
    extent = max(24.0, span * 2.2)
    ground.data.from_pydata(
        [(-extent, -extent, ground_z), (extent, -extent, ground_z),
         (extent, extent, ground_z), (-extent, extent, ground_z)], [], [(0, 1, 2, 3)])
    ground_mat = bpy.data.materials.new("PBRReview_NeutralGround")
    ground_mat.diffuse_color = (0.20, 0.20, 0.20, 1.0)
    ground_mat.use_nodes = True
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.20, 0.20, 0.20, 1.0)
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    ground.data.materials.append(ground_mat)

    camera_data = bpy.data.cameras.new("PBRReview_Camera")
    camera = bpy.data.objects.new("PBRReview_Camera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    scene.camera = camera

    def area(name, offset, energy, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = focus + Vector(offset)
        obj.rotation_euler = (focus - obj.location).to_track_quat("-Z", "Y").to_euler()

    area("PBRReview_Key", (-7.0, -8.0, 10.0), 1700, 8.0)
    area("PBRReview_Fill", (8.0, -3.0, 6.0), 900, 7.0)
    area("PBRReview_Rim", (1.0, 8.0, 9.0), 1300, 6.0)

    world = bpy.data.worlds.new("PBRReview_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.075, 0.075, 0.075, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    scene.world = world
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    return camera


def render_view(scene, camera, target, direction, ortho_scale, path):
    distance = max(12.0, ortho_scale * 1.45)
    camera.location = target + direction.normalized() * distance
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = ortho_scale
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def update_manifest(review):
    manifest = json.loads(MANIFEST_OUT.read_text(encoding="utf-8"))
    manifest[MANIFEST_REVIEW_KEY] = review
    MANIFEST_OUT.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_review(review):
    if UV_V3_MODE:
        REVIEW_OUT.write_text(json.dumps(review, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    else:
        update_manifest(review)


def main():
    if bpy.app.version[:3] != (4, 5, 12):
        raise RuntimeError(f"Expected pinned Blender 4.5.12, got {bpy.app.version_string}")
    required_inputs = [SOURCE_GLB, RETEXTURE_GLB]
    if not UV_V3_MODE:
        required_inputs.append(MANIFEST_OUT)
    for path in required_inputs:
        if not path.exists():
            raise FileNotFoundError(path)
    output_paths = [BLEND_OUT, GLB_OUT, FRONT_RENDER, PORCH_RENDER, SIDE_RENDER]
    if UV_V3_MODE:
        output_paths.append(REVIEW_OUT)
    for path in output_paths:
        if path is None:
            continue
        if path.exists():
            raise FileExistsError(f"Refusing to overwrite production output: {path}")

    source_doc = glb_document(SOURCE_GLB)
    returned_doc = glb_document(RETEXTURE_GLB)
    source_sha = sha256_file(SOURCE_GLB)
    returned_sha = sha256_file(RETEXTURE_GLB)
    source_mesh_names = [node.get("name") for node in source_doc.get("nodes", []) if "mesh" in node]
    returned_mesh_names = [node.get("name") for node in returned_doc.get("nodes", []) if "mesh" in node]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    source_objects = list(bpy.context.scene.objects)
    source_meshes = [obj for obj in source_objects if obj.type == "MESH"]
    source_root = bpy.data.objects.get(SOURCE_ROOT_NAME)
    if source_root is None or not source_meshes:
        raise RuntimeError("Could not import the staged semantic source hierarchy")
    source_set = set(source_objects)
    normal_correction_report = (
        correct_source_normals_for_glb(source_meshes)
        if NORMAL_CORRECTION_V2 else None
    )

    bpy.ops.import_scene.gltf(filepath=str(RETEXTURE_GLB))
    returned_objects = [obj for obj in bpy.context.scene.objects if obj not in source_set]
    returned_meshes = [obj for obj in returned_objects if obj.type == "MESH"]
    returned_materials = [material for obj in returned_meshes for material in obj.data.materials if material]
    unique_materials = list(dict.fromkeys(returned_materials))
    candidate_material = next((material for material in unique_materials if material_report(material)), None)
    if candidate_material is None:
        raise RuntimeError("Returned GLB contains no imported PBR material with a Principled shader")
    pbr_report = material_report(candidate_material)

    source_triangles = triangle_signatures(source_meshes)
    returned_triangles = triangle_signatures(returned_meshes)
    geometry_match = counter_delta(source_triangles["geometry"], returned_triangles["geometry"])
    uv_space_match = counter_delta(source_triangles["uv_space"], returned_triangles["uv_space"])
    paired_match = counter_delta(source_triangles["paired"], returned_triangles["paired"])
    missing_uv_sources = missing_uv_triangle_sources(source_meshes, returned_triangles["uv_space"])
    cap_materials = {}
    cap_exception_rows = []
    cap_validation_error = None
    uv_compatible = uv_space_match["exact"]
    if not uv_compatible and uv_space_match["extra"] == 0:
        try:
            cap_materials, cap_exception_rows = collect_cap_face_materials(
                source_meshes, missing_uv_sources
            )
            uv_compatible = True
        except RuntimeError as error:
            cap_validation_error = str(error)
            uv_compatible = False
    review = {
        "status": "uv_compatible" if uv_space_match["exact"] else (
            "uv_compatible_with_named_cap_exceptions" if uv_compatible else "blocked_uv_mismatch"
        ),
        "blender": {"version": bpy.app.version_string, "build_hash": bpy.app.build_hash.decode("ascii", "replace")},
        "source_input": {
            "path": SOURCE_GLB.name,
            "sha256": source_sha,
            "bytes": SOURCE_GLB.stat().st_size,
            "glb_mesh_node_count": len(source_mesh_names),
            "glb_node_names": source_mesh_names,
            "imported_mesh_object_count": len(source_meshes),
            "world_bounds": object_bounds(source_meshes),
            "triangles": source_triangles["triangle_count"],
            "uv_coordinates": source_triangles["uv_coordinate_count"],
        },
        "returned_retexture": {
            "path": RETEXTURE_GLB.name,
            "sha256": returned_sha,
            "bytes": RETEXTURE_GLB.stat().st_size,
            "glb_mesh_node_count": len(returned_mesh_names),
            "glb_node_names": returned_mesh_names,
            "imported_mesh_object_count": len(returned_meshes),
            "world_bounds": object_bounds(returned_meshes),
            "triangles": returned_triangles["triangle_count"],
            "uv_coordinates": returned_triangles["uv_coordinate_count"],
            "geometry_triangle_match": geometry_match,
            "uv_space_triangle_match": uv_space_match,
            "uv_space_triangles_missing_from_returned": missing_uv_sources,
            "position_uv_triangle_match": paired_match,
            "approved_original_material_cap_triangles": cap_exception_rows,
            "cap_exception_validation_error": cap_validation_error,
            "material": pbr_report,
        },
        **({"normal_correction": normal_correction_report} if NORMAL_CORRECTION_V2 else {}),
        "decision": (
            "Attach the returned PBR material to all covered source faces; retain original source slots on exactly the four triangles of the two verified thin end-cap quads. Ignore returned geometry and retain all source semantic nodes, transforms, mesh positions, faces, and UV loops."
            if UV_V3_MODE and cap_exception_rows else
            "Attach the returned PBR material to source faces; retain the original source material only on the two named subpixel cap quads. Ignore returned geometry and retain all source semantic nodes, transforms, mesh positions, faces, and UV loops."
            if cap_exception_rows else
            "Attach only the returned material. Retain source semantic nodes, transforms, mesh positions, faces, and UV loops."
        ),
        "render_files": [],
    }
    if not uv_compatible:
        review["decision"] = "Stopped before attachment: returned UV-space triangle coverage differs from the exact atlas or the two specifically approved cap quads."
        write_review(review)
        raise RuntimeError("Returned UVs exceed the approved cap-only exception; no material attachment or output was created")

    new_material = candidate_material.copy()
    new_material.name = MATERIAL_NAME
    new_material["meshymaterial_source"] = RETEXTURE_GLB.name
    new_material["meshymaterial_sha256"] = returned_sha
    for obj in source_meshes:
        original_bounds = object_bounds([obj])
        original_transform = [
            [round(float(value), 9) for value in row] for row in obj.matrix_local
        ]
        uv_before = [uv_key(item.uv) for item in (obj.data.uv_layers.active or obj.data.uv_layers[0]).data]
        cap_data = cap_materials.get(obj.name)
        obj.data.materials.clear()
        obj.data.materials.append(new_material)
        preserved_slot_indices = {}
        if cap_data:
            for material in cap_data["faces"].values():
                pointer = material.as_pointer()
                if pointer not in preserved_slot_indices:
                    obj.data.materials.append(material)
                    preserved_slot_indices[pointer] = len(obj.data.materials) - 1
        for polygon in obj.data.polygons:
            preserved_material = cap_data["faces"].get(polygon.index) if cap_data else None
            polygon.material_index = (
                preserved_slot_indices[preserved_material.as_pointer()]
                if preserved_material else 0
            )
        uv_after = [uv_key(item.uv) for item in (obj.data.uv_layers.active or obj.data.uv_layers[0]).data]
        if uv_before != uv_after or object_bounds([obj]) != original_bounds or original_transform != [
                [round(float(value), 9) for value in row] for row in obj.matrix_local]:
            raise RuntimeError(f"Material binding changed geometry, transform, or UV loops: {obj.name}")

    for obj in returned_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

    bpy.context.view_layer.update()
    input_bounds = object_bounds(source_meshes)
    low, high = [Vector(row) for row in input_bounds]
    focus = (low + high) * 0.5
    camera = create_studio(bpy.context.scene, focus, input_bounds)

    door_objects = [obj for obj in source_meshes if "StreetDoorClosed" in obj.name]
    if not door_objects:
        door_objects = [obj for obj in source_meshes if "Street_Portal0" in obj.name]
    porch_bounds = object_bounds(door_objects) if door_objects else input_bounds
    porch_low, porch_high = [Vector(row) for row in porch_bounds]
    porch_focus = (porch_low + porch_high) * 0.5
    side_objects = [obj for obj in source_meshes if "HeroHouse_Right_Window" in obj.name]
    side_bounds = object_bounds(side_objects) if side_objects else input_bounds
    side_low, side_high = [Vector(row) for row in side_bounds]
    side_focus = (side_low + side_high) * 0.5

    render_view(bpy.context.scene, camera, focus, Vector((-0.82, -1.10, 0.62)), max(high.x - low.x, high.y - low.y, high.z - low.z) * 1.55, FRONT_RENDER)
    if not NORMAL_CORRECTION_V2:
        render_view(bpy.context.scene, camera, porch_focus, Vector((-0.30, -1.0, 0.24)), 5.4, PORCH_RENDER)
        render_view(bpy.context.scene, camera, side_focus, Vector((1.0, 0.22, 0.27)), max(high.y - low.y, high.z - low.z) * 1.45, SIDE_RENDER)

    for image in bpy.data.images:
        if image.source == "FILE" and not image.packed_file:
            image.pack()
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUT))

    bpy.ops.object.select_all(action="DESELECT")
    for obj in [source_root, *source_root.children_recursive]:
        if obj in source_meshes or obj is source_root:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = source_root
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_OUT),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        use_visible=False,
    )

    review["status"] = "pbr_attached_to_semantic_geometry"
    review["decision"] = (
        "Returned PBR material was attached to the v3 semantic geometry over every covered UV-space triangle. The source slots remain on the four exact triangles of two verified thin end-cap quads omitted by Meshy. Returned geometry was ignored; v3 input transforms, positions, faces, and UV loops remain authoritative."
        if UV_V3_MODE and cap_exception_rows else
        "Returned PBR material was attached to the input semantic geometry over the 19,898 shared UV-space triangles. "
        "The original source materials remain on the four unmatched subpixel end-cap triangles (two thin cap quads). "
        "Returned geometry was ignored; all input semantic nodes, transforms, positions, faces, and UV loops remain authoritative."
        if cap_exception_rows else
        "Returned PBR material was attached to the input semantic geometry after exact UV-space triangle comparison. "
        "Returned geometry was ignored; input nodes, transforms, positions, faces, and UV loops remain authoritative."
    )
    review["attached_material"] = pbr_report
    review["derived_output"] = {
        "blend": BLEND_OUT.name,
        "blend_sha256": sha256_file(BLEND_OUT),
        "glb": GLB_OUT.name,
        "glb_sha256": sha256_file(GLB_OUT),
        "glb_bytes": GLB_OUT.stat().st_size,
        "mesh_nodes": len(source_meshes),
        "triangles": source_triangles["triangle_count"],
        "world_bounds": input_bounds,
        "material_name": MATERIAL_NAME,
    }
    review["render_files"] = [
        {"path": path.name, "sha256": sha256_file(path), "bytes": path.stat().st_size}
        for path in (FRONT_RENDER, PORCH_RENDER, SIDE_RENDER)
        if path is not None and path.exists()
    ]
    write_review(review)
    print("hero-house-retexture-attachment: " + json.dumps({
        "status": review["status"],
        "geometry_match": geometry_match,
        "uv_space_match": uv_space_match,
        "paired_match": paired_match,
        "material": MATERIAL_NAME,
        "output_glb": review["derived_output"],
        "renders": review["render_files"],
    }, sort_keys=True))


if __name__ == "__main__":
    main()
